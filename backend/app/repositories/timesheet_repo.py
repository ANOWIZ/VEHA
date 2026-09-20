"""Репозиторий трудозатрат: записи и недельные агрегаты."""

from __future__ import annotations

import uuid
from collections.abc import Sequence
from datetime import date
from decimal import Decimal

from sqlalchemy import func, or_, select

from app.models.enums import TimeEntryStatus
from app.models.project import Project, ProjectMember
from app.models.timesheet import TimeEntry, TimesheetWeek
from app.repositories.base import BaseRepository


class TimesheetRepository(BaseRepository[TimeEntry]):
    model = TimeEntry

    async def list_for_week(
        self, user_id: uuid.UUID, week_start: date, week_end: date
    ) -> Sequence[TimeEntry]:
        stmt = (
            select(TimeEntry)
            .where(
                TimeEntry.user_id == user_id,
                TimeEntry.work_date >= week_start,
                TimeEntry.work_date <= week_end,
                TimeEntry.deleted_at.is_(None),
            )
            .order_by(TimeEntry.work_date)
        )
        return (await self.session.execute(stmt)).scalars().all()

    async def daily_total(
        self,
        user_id: uuid.UUID,
        work_date: date,
        *,
        exclude_id: uuid.UUID | None = None,
    ) -> Decimal:
        stmt = select(func.coalesce(func.sum(TimeEntry.hours), 0)).where(
            TimeEntry.user_id == user_id,
            TimeEntry.work_date == work_date,
            TimeEntry.deleted_at.is_(None),
        )
        if exclude_id is not None:
            stmt = stmt.where(TimeEntry.id != exclude_id)
        total = (await self.session.execute(stmt)).scalar_one()
        return Decimal(str(total))

    async def get_week(
        self, user_id: uuid.UUID, week_start: date
    ) -> TimesheetWeek | None:
        stmt = select(TimesheetWeek).where(
            TimesheetWeek.user_id == user_id, TimesheetWeek.week_start == week_start
        )
        return (await self.session.execute(stmt)).scalar_one_or_none()

    async def pending_for_approver(
        self, approver_id: uuid.UUID, *, is_privileged: bool
    ) -> Sequence[TimeEntry]:
        """Записи в статусе submitted по проектам, где пользователь — РП/куратор
        (или все проекты для привилегированных ролей)."""
        stmt = (
            select(TimeEntry)
            .where(
                TimeEntry.status == TimeEntryStatus.SUBMITTED,
                TimeEntry.deleted_at.is_(None),
            )
            .order_by(TimeEntry.work_date)
        )
        if not is_privileged:
            managed = (
                select(Project.id)
                .where(
                    or_(
                        Project.manager_id == approver_id,
                        Project.curator_id == approver_id,
                    ),
                    Project.deleted_at.is_(None),
                )
                .scalar_subquery()
            )
            stmt = stmt.where(TimeEntry.project_id.in_(managed))
        return (await self.session.execute(stmt)).scalars().all()

    async def is_project_member(
        self, project_id: uuid.UUID, user_id: uuid.UUID
    ) -> bool:
        stmt = select(ProjectMember.id).where(
            ProjectMember.project_id == project_id,
            ProjectMember.user_id == user_id,
            ProjectMember.deleted_at.is_(None),
        )
        member = (await self.session.execute(stmt)).first()
        if member is not None:
            return True
        # РП/куратор тоже могут списывать часы.
        stmt2 = select(Project.id).where(
            Project.id == project_id,
            or_(Project.manager_id == user_id, Project.curator_id == user_id),
        )
        return (await self.session.execute(stmt2)).first() is not None

    async def project_approved_cost(self, project_id: uuid.UUID) -> Decimal:
        """Суммарная себестоимость approved-таймшитов проекта (часы × снимок ставки)."""
        stmt = select(
            func.coalesce(
                func.sum(TimeEntry.hours * func.coalesce(TimeEntry.cost_rate_snapshot, 0)),
                0,
            )
        ).where(
            TimeEntry.project_id == project_id,
            TimeEntry.status == TimeEntryStatus.APPROVED,
            TimeEntry.deleted_at.is_(None),
        )
        return Decimal(str((await self.session.execute(stmt)).scalar_one()))

    async def project_approved_hours(self, project_id: uuid.UUID) -> Decimal:
        stmt = select(func.coalesce(func.sum(TimeEntry.hours), 0)).where(
            TimeEntry.project_id == project_id,
            TimeEntry.status == TimeEntryStatus.APPROVED,
            TimeEntry.deleted_at.is_(None),
        )
        return Decimal(str((await self.session.execute(stmt)).scalar_one()))

    async def get_entries_for_week_submit(
        self, user_id: uuid.UUID, week_start: date, week_end: date
    ) -> Sequence[TimeEntry]:
        stmt = select(TimeEntry).where(
            TimeEntry.user_id == user_id,
            TimeEntry.work_date >= week_start,
            TimeEntry.work_date <= week_end,
            TimeEntry.status == TimeEntryStatus.DRAFT,
            TimeEntry.deleted_at.is_(None),
        )
        return (await self.session.execute(stmt)).scalars().all()

    async def last_week_entries(
        self, user_id: uuid.UUID, prev_week_start: date, prev_week_end: date
    ) -> Sequence[TimeEntry]:
        stmt = select(TimeEntry).where(
            TimeEntry.user_id == user_id,
            TimeEntry.work_date >= prev_week_start,
            TimeEntry.work_date <= prev_week_end,
            TimeEntry.deleted_at.is_(None),
        )
        return (await self.session.execute(stmt)).scalars().all()
