"""Сервис трудозатрат: ввод, недельная сетка, отправка и утверждение.

Ключевые инварианты (CLAUDE.md §4):
- часы шаг 0.25, не более 24/день суммарно, комментарий обязателен;
- утверждает руководитель проекта; после утверждения запись блокируется;
- корректировка утверждённой записи — сторно (reversal), не редактирование;
- себестоимость берётся из ставки, действующей на дату записи (снимок при утв.).
"""

from __future__ import annotations

import uuid
from datetime import UTC, date, datetime, timedelta
from decimal import Decimal

from sqlalchemy.ext.asyncio import AsyncSession

from app.core.exceptions import (
    AppError,
    ConflictError,
    ErrorCode,
    ForbiddenError,
    NotFoundError,
    ValidationError,
)
from app.models.enums import AuditAction, Role, TimeEntryStatus
from app.models.project import Project
from app.models.timesheet import TimeEntry, TimesheetWeek
from app.models.user import User
from app.repositories.timesheet_repo import TimesheetRepository
from app.repositories.user_repo import UserRepository
from app.schemas.timesheet import TimeEntryCreate, TimeEntryUpdate
from app.services import timesheet_rules
from app.services.audit_service import AuditService

_EDITABLE = {TimeEntryStatus.DRAFT, TimeEntryStatus.REJECTED}


def monday_of(d: date) -> date:
    return d - timedelta(days=d.weekday())


class TimesheetService:
    def __init__(self, session: AsyncSession) -> None:
        self.session = session
        self.repo = TimesheetRepository(session)
        self.users = UserRepository(session)
        self.audit = AuditService(session)

    # ---------- Недельная сетка ----------
    async def get_week(self, user: User, week_start: date) -> dict:
        start = monday_of(week_start)
        end = start + timedelta(days=6)
        entries = list(await self.repo.list_for_week(user.id, start, end))
        week = await self.repo.get_week(user.id, start)
        daily: dict[str, Decimal] = {}
        total = Decimal("0")
        for e in entries:
            total += e.hours
            key = e.work_date.isoformat()
            daily[key] = daily.get(key, Decimal("0")) + e.hours
        return {
            "week_start": start,
            "week_end": end,
            "status": week.status if week else TimeEntryStatus.DRAFT,
            "entries": entries,
            "total_hours": total,
            "daily_totals": daily,
        }

    # ---------- Ввод ----------
    async def _ensure_can_log(self, user: User, project_id: uuid.UUID) -> None:
        if not await self.repo.is_project_member(project_id, user.id):
            raise ForbiddenError("Вы не участник проекта — списание часов недоступно")

    async def create_entry(self, user: User, data: TimeEntryCreate) -> TimeEntry:
        await self._ensure_can_log(user, data.project_id)
        try:
            timesheet_rules.validate_hours(data.hours)
        except ValueError as exc:
            raise ValidationError(str(exc), code=ErrorCode.TIMESHEET_INVALID_HOURS) from exc
        existing = await self.repo.daily_total(user.id, data.work_date)
        try:
            timesheet_rules.validate_daily_total(existing, data.hours)
        except ValueError as exc:
            raise ValidationError(str(exc), code=ErrorCode.TIMESHEET_DAILY_LIMIT) from exc

        entry = await self.repo.create(
            user_id=user.id,
            project_id=data.project_id,
            task_id=data.task_id,
            work_date=data.work_date,
            hours=data.hours,
            comment=data.comment,
            status=TimeEntryStatus.DRAFT,
        )
        return entry

    async def _own_editable(self, user: User, entry_id: uuid.UUID) -> TimeEntry:
        entry = await self.repo.get(entry_id)
        if entry is None:
            raise NotFoundError("Запись не найдена")
        if entry.user_id != user.id:
            raise ForbiddenError("Можно редактировать только свои записи")
        if entry.status not in _EDITABLE:
            raise AppError(
                "Запись отправлена или утверждена — редактирование запрещено "
                "(используйте сторно для утверждённых)",
                code=ErrorCode.TIMESHEET_LOCKED,
                status_code=409,
            )
        return entry

    async def update_entry(
        self, user: User, entry_id: uuid.UUID, data: TimeEntryUpdate
    ) -> TimeEntry:
        entry = await self._own_editable(user, entry_id)
        new_hours = data.hours if data.hours is not None else entry.hours
        if data.hours is not None:
            try:
                timesheet_rules.validate_hours(new_hours)
            except ValueError as exc:
                raise ValidationError(
                    str(exc), code=ErrorCode.TIMESHEET_INVALID_HOURS
                ) from exc
            existing = await self.repo.daily_total(
                user.id, entry.work_date, exclude_id=entry.id
            )
            try:
                timesheet_rules.validate_daily_total(existing, new_hours)
            except ValueError as exc:
                raise ValidationError(
                    str(exc), code=ErrorCode.TIMESHEET_DAILY_LIMIT
                ) from exc
            entry.hours = new_hours
        if data.task_id is not None:
            entry.task_id = data.task_id
        if data.comment is not None:
            entry.comment = data.comment
        # Повторная отправка после отклонения возвращает в черновик.
        if entry.status == TimeEntryStatus.REJECTED:
            entry.status = TimeEntryStatus.DRAFT
            entry.reject_reason = None
        await self.session.flush()
        return entry

    async def delete_entry(self, user: User, entry_id: uuid.UUID) -> None:
        entry = await self._own_editable(user, entry_id)
        await self.repo.soft_delete(entry)

    # ---------- Отправка ----------
    async def submit_week(self, user: User, week_start: date) -> int:
        start = monday_of(week_start)
        end = start + timedelta(days=6)
        drafts = list(await self.repo.get_entries_for_week_submit(user.id, start, end))
        if not drafts:
            raise ValidationError("Нет черновиков для отправки на этой неделе")
        for e in drafts:
            e.status = TimeEntryStatus.SUBMITTED
        week = await self.repo.get_week(user.id, start)
        if week is None:
            week = TimesheetWeek(
                user_id=user.id, week_start=start, status=TimeEntryStatus.SUBMITTED
            )
            self.session.add(week)
        else:
            week.status = TimeEntryStatus.SUBMITTED
        await self.session.flush()
        return len(drafts)

    async def copy_week(
        self, user: User, source_week_start: date, target_week_start: date
    ) -> int:
        src = monday_of(source_week_start)
        tgt = monday_of(target_week_start)
        if src == tgt:
            raise ValidationError("Неделя-источник совпадает с целевой")
        src_entries = list(await self.repo.list_for_week(user.id, src, src + timedelta(days=6)))
        if not src_entries:
            raise ValidationError("В неделе-источнике нет записей для копирования")
        offset = (tgt - src).days
        created = 0
        for e in src_entries:
            new_date = e.work_date + timedelta(days=offset)
            existing = await self.repo.daily_total(user.id, new_date)
            if existing + e.hours > timesheet_rules.MAX_DAILY_HOURS:
                continue  # не превышаем суточный лимит — пропускаем
            await self.repo.create(
                user_id=user.id,
                project_id=e.project_id,
                task_id=e.task_id,
                work_date=new_date,
                hours=e.hours,
                comment=e.comment,
                status=TimeEntryStatus.DRAFT,
            )
            created += 1
        return created

    # ---------- Утверждение ----------
    @staticmethod
    def _is_privileged(approver: User) -> bool:
        return Role.ADMIN in set(approver.roles)

    async def _assert_can_approve(self, approver: User, entry: TimeEntry) -> None:
        if self._is_privileged(approver):
            return
        project = await self.session.get(Project, entry.project_id)
        if project is None:
            raise NotFoundError("Проект записи не найден")
        if project.manager_id != approver.id and project.curator_id != approver.id:
            raise AppError(
                "Утверждать может только руководитель/куратор проекта",
                code=ErrorCode.NOT_APPROVER,
                status_code=403,
            )

    async def pending_for(self, approver: User) -> list[TimeEntry]:
        return list(
            await self.repo.pending_for_approver(
                approver.id, is_privileged=self._is_privileged(approver)
            )
        )

    async def approve(self, approver: User, entry_ids: list[uuid.UUID]) -> int:
        approved = 0
        touched_projects: set[uuid.UUID] = set()
        for eid in entry_ids:
            entry = await self.repo.get(eid)
            if entry is None or entry.status != TimeEntryStatus.SUBMITTED:
                continue
            await self._assert_can_approve(approver, entry)
            rate_row = await self.users.effective_cost_rate(entry.user_id, entry.work_date)
            entry.status = TimeEntryStatus.APPROVED
            entry.approved_by = approver.id
            entry.approved_at = datetime.now(UTC)
            entry.cost_rate_snapshot = rate_row.cost_rate if rate_row else Decimal("0")
            touched_projects.add(entry.project_id)
            approved += 1
        if approved:
            await self.audit.record(
                entity="TimeEntry",
                entity_id=None,
                action=AuditAction.APPROVE,
                actor_id=approver.id,
                diff={"count": approved, "ids": [str(i) for i in entry_ids]},
            )
            await self.session.flush()
            self._schedule_margin_recalc(touched_projects)
        return approved

    async def reject(
        self, approver: User, entry_ids: list[uuid.UUID], reason: str | None
    ) -> int:
        if not reason or not reason.strip():
            raise ValidationError("Отклонение требует указания причины")
        from app.services.notification_service import NotificationService

        notifier = NotificationService(self.session)
        rejected = 0
        for eid in entry_ids:
            entry = await self.repo.get(eid)
            if entry is None or entry.status != TimeEntryStatus.SUBMITTED:
                continue
            await self._assert_can_approve(approver, entry)
            entry.status = TimeEntryStatus.REJECTED
            entry.reject_reason = reason
            await notifier.notify(
                entry.user_id,
                kind="timesheet_rejected",
                title="Трудозатраты отклонены",
                body=f"Запись от {entry.work_date.isoformat()} отклонена: {reason}",
                link="/timesheets",
            )
            rejected += 1
        if rejected:
            await self.audit.record(
                entity="TimeEntry",
                entity_id=None,
                action=AuditAction.REJECT,
                actor_id=approver.id,
                diff={"count": rejected, "reason": reason},
            )
            await self.session.flush()
        return rejected

    async def reverse(self, approver: User, entry_id: uuid.UUID) -> TimeEntry:
        """Сторно утверждённой записи: создаёт зеркальную запись с отрицательными
        часами и снимком стоимости, чтобы откатить себестоимость без правки оригинала."""
        entry = await self.repo.get(entry_id)
        if entry is None:
            raise NotFoundError("Запись не найдена")
        if entry.status != TimeEntryStatus.APPROVED:
            raise ConflictError("Сторнировать можно только утверждённую запись")
        await self._assert_can_approve(approver, entry)
        reversal = await self.repo.create(
            user_id=entry.user_id,
            project_id=entry.project_id,
            task_id=entry.task_id,
            work_date=entry.work_date,
            hours=-entry.hours,
            comment=f"Сторно записи от {entry.work_date.isoformat()}: {entry.comment}",
            status=TimeEntryStatus.APPROVED,
            approved_by=approver.id,
            approved_at=datetime.now(UTC),
            cost_rate_snapshot=entry.cost_rate_snapshot,
            reversal_of=entry.id,
        )
        await self.audit.record(
            entity="TimeEntry",
            entity_id=entry.id,
            action=AuditAction.UPDATE,
            actor_id=approver.id,
            diff={"reversal": str(reversal.id)},
        )
        self._schedule_margin_recalc({entry.project_id})
        return reversal

    @staticmethod
    def _schedule_margin_recalc(project_ids: set[uuid.UUID]) -> None:
        """Поставить пересчёт маржи в Celery (best-effort: брокер может быть недоступен)."""
        from app.core.config import settings

        if not settings.tasks_enabled:
            return
        try:
            from app.tasks.jobs import recalc_project_margin

            for pid in project_ids:
                recalc_project_margin.delay(str(pid))
        except Exception:
            pass
