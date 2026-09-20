"""Сервис отчётов: загрузка ресурсов (utilization), выгрузка таймшитов, портфель.

Агрегаты считаются set-based-запросами без N+1. Себестоимость/маржа доступны
только финансовым ролям — вызывающий роутер обязан это проверить."""

from __future__ import annotations

import uuid
from datetime import date, timedelta
from decimal import Decimal

from sqlalchemy import func, select
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.config import settings
from app.models.enums import Role, TimeEntryStatus
from app.models.project import Project, Task
from app.models.timesheet import TimeEntry
from app.models.user import User
from app.repositories.project_repo import ProjectRepository
from app.services import finance_calc as fc


def working_days(date_from: date, date_to: date) -> int:
    """Количество рабочих дней (Пн–Пт) в диапазоне включительно."""
    if date_to < date_from:
        return 0
    total = 0
    day = date_from
    while day <= date_to:
        if day.weekday() < 5:  # 0=Пн … 4=Пт
            total += 1
        day += timedelta(days=1)
    return total


class ReportService:
    def __init__(self, session: AsyncSession) -> None:
        self.session = session
        self.projects = ProjectRepository(session)

    def _daily_norm(self) -> Decimal:
        # Дневная норма = недельная норma / 5.
        return Decimal(str(settings.default_week_norm_hours)) / Decimal("5")

    async def utilization(self, date_from: date, date_to: date) -> dict:
        wd = working_days(date_from, date_to)
        capacity = (self._daily_norm() * wd).quantize(Decimal("0.01"))

        # Билируемые часы и число проектов по сотрудникам за период (одним запросом).
        agg_stmt = (
            select(
                TimeEntry.user_id,
                func.coalesce(func.sum(TimeEntry.hours), 0),
                func.count(func.distinct(TimeEntry.project_id)),
            )
            .where(
                TimeEntry.work_date >= date_from,
                TimeEntry.work_date <= date_to,
                TimeEntry.status == TimeEntryStatus.APPROVED,
                TimeEntry.deleted_at.is_(None),
            )
            .group_by(TimeEntry.user_id)
        )
        agg = {
            uid: (Decimal(str(hrs)), int(pc))
            for uid, hrs, pc in (await self.session.execute(agg_stmt)).all()
        }

        # Внутренние (не-client) активные сотрудники — у них есть ёмкость загрузки.
        users_stmt = select(User).where(
            User.is_active.is_(True), User.deleted_at.is_(None)
        )
        users = [
            u
            for u in (await self.session.execute(users_stmt)).scalars().all()
            if Role.CLIENT not in set(u.roles)
        ]

        rows = []
        total_billable = Decimal("0")
        for u in users:
            billable, projects_count = agg.get(u.id, (Decimal("0"), 0))
            # Пропускаем сотрудников без ёмкости и без часов (нерелевантны отчёту).
            if capacity == 0 and billable == 0:
                continue
            total_billable += billable
            rows.append(
                {
                    "user_id": u.id,
                    "full_name": u.full_name,
                    "department": u.department,
                    "billable_hours": billable,
                    "capacity_hours": capacity,
                    "utilization_pct": fc.utilization_pct(billable, capacity),
                    "projects_count": projects_count,
                }
            )
        rows.sort(key=lambda r: r["utilization_pct"], reverse=True)

        total_capacity = (capacity * len(rows)).quantize(Decimal("0.01"))
        return {
            "date_from": date_from,
            "date_to": date_to,
            "working_days": wd,
            "capacity_per_user": capacity,
            "users_count": len(rows),
            "total_billable_hours": total_billable.quantize(Decimal("0.01")),
            "total_capacity_hours": total_capacity,
            "avg_utilization_pct": fc.utilization_pct(total_billable, total_capacity),
            "rows": rows,
        }

    async def timesheet_rows(
        self,
        user: User,
        *,
        date_from: date,
        date_to: date,
        project_id: uuid.UUID | None = None,
        status: TimeEntryStatus | None = None,
        include_cost: bool,
    ) -> list[dict]:
        """Строки трудозатрат за период для выгрузки. Скоуп — доступные проекты."""
        # Доступные проекты (admin/director/finance — все; pm/инженер — свои).
        accessible, _ = await self.projects.list_for_user(user, limit=1000)
        accessible_ids = {p.id for p in accessible}
        if project_id is not None and project_id not in accessible_ids:
            return []
        scope_ids = {project_id} if project_id is not None else accessible_ids
        if not scope_ids:
            return []

        u = User.__table__.alias("u")
        p = Project.__table__.alias("p")
        t = Task.__table__.alias("t")
        stmt = (
            select(
                TimeEntry.work_date,
                u.c.full_name,
                p.c.code,
                p.c.name,
                t.c.name,
                TimeEntry.hours,
                TimeEntry.status,
                TimeEntry.comment,
                TimeEntry.cost_rate_snapshot,
            )
            .select_from(TimeEntry)
            .join(u, u.c.id == TimeEntry.user_id)
            .join(p, p.c.id == TimeEntry.project_id)
            .outerjoin(t, t.c.id == TimeEntry.task_id)
            .where(
                TimeEntry.work_date >= date_from,
                TimeEntry.work_date <= date_to,
                TimeEntry.project_id.in_(scope_ids),
                TimeEntry.deleted_at.is_(None),
            )
            .order_by(TimeEntry.work_date, p.c.code)
        )
        if status is not None:
            stmt = stmt.where(TimeEntry.status == status)

        out = []
        for wdate, full_name, code, pname, tname, hours, st, comment, rate in (
            await self.session.execute(stmt)
        ).all():
            row = {
                "work_date": wdate,
                "user": full_name,
                "project_code": code,
                "project_name": pname,
                "task": tname,
                "hours": Decimal(str(hours)),
                "status": st,
                "comment": comment,
            }
            if include_cost:
                rate_dec = Decimal(str(rate)) if rate is not None else Decimal("0")
                row["cost"] = (Decimal(str(hours)) * rate_dec).quantize(Decimal("0.01"))
            out.append(row)
        return out

    @staticmethod
    def can_see_financials(user: User) -> bool:
        return bool(
            {Role.ADMIN, Role.DIRECTOR, Role.FINANCE, Role.PM} & set(user.roles)
        )
