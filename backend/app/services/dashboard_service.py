"""Сервис дашбордов: портфель проектов (директор) и здоровье проектов (РП).

Агрегирует маржу, отклонения по часам и риски пакетными (set-based) запросами,
без N+1 по проектам. Маржа/себестоимость — только для ролей с финансами;
вызывающий роутер обязан это проверить."""

from __future__ import annotations

import uuid
from decimal import Decimal

from sqlalchemy import func, select
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.enums import ProjectStatus, Role, TimeEntryStatus
from app.models.finance import ActualCost, ProjectBudget
from app.models.project import Task
from app.models.timesheet import TimeEntry
from app.models.user import User
from app.repositories.project_repo import ProjectRepository
from app.services import finance_calc as fc

# Пороговые значения рисков.
LOW_MARGIN_PCT = Decimal("15")
HOURS_OVERRUN_PCT = Decimal("110")


class DashboardService:
    def __init__(self, session: AsyncSession) -> None:
        self.session = session
        self.projects = ProjectRepository(session)

    async def _approved_by_project(
        self, ids: list[uuid.UUID]
    ) -> dict[uuid.UUID, tuple[Decimal, Decimal]]:
        """{project_id: (себестоимость ФОТ, часы)} по approved-таймшитам — одним запросом."""
        if not ids:
            return {}
        stmt = (
            select(
                TimeEntry.project_id,
                func.coalesce(
                    func.sum(TimeEntry.hours * func.coalesce(TimeEntry.cost_rate_snapshot, 0)), 0
                ),
                func.coalesce(func.sum(TimeEntry.hours), 0),
            )
            .where(
                TimeEntry.project_id.in_(ids),
                TimeEntry.status == TimeEntryStatus.APPROVED,
                TimeEntry.deleted_at.is_(None),
            )
            .group_by(TimeEntry.project_id)
        )
        return {
            pid: (Decimal(str(cost)), Decimal(str(hours)))
            for pid, cost, hours in (await self.session.execute(stmt)).all()
        }

    async def _planned_hours_by_project(self, ids: list[uuid.UUID]) -> dict[uuid.UUID, Decimal]:
        if not ids:
            return {}
        stmt = (
            select(Task.project_id, func.coalesce(func.sum(Task.planned_hours), 0))
            .where(Task.project_id.in_(ids), Task.deleted_at.is_(None))
            .group_by(Task.project_id)
        )
        return {pid: Decimal(str(h)) for pid, h in (await self.session.execute(stmt)).all()}

    async def _actuals_by_project(self, ids: list[uuid.UUID]) -> dict[uuid.UUID, Decimal]:
        if not ids:
            return {}
        stmt = (
            select(ActualCost.project_id, func.coalesce(func.sum(ActualCost.amount), 0))
            .where(ActualCost.project_id.in_(ids), ActualCost.deleted_at.is_(None))
            .group_by(ActualCost.project_id)
        )
        return {pid: Decimal(str(a)) for pid, a in (await self.session.execute(stmt)).all()}

    async def _budgets_by_project(
        self, ids: list[uuid.UUID]
    ) -> dict[uuid.UUID, ProjectBudget]:
        if not ids:
            return {}
        stmt = select(ProjectBudget).where(
            ProjectBudget.project_id.in_(ids), ProjectBudget.deleted_at.is_(None)
        )
        return {b.project_id: b for b in (await self.session.execute(stmt)).scalars().all()}

    async def portfolio(self, user: User) -> dict:
        items, _ = await self.projects.list_for_user(
            user, status=ProjectStatus.ACTIVE, limit=500
        )
        ids = [p.id for p in items]
        approved = await self._approved_by_project(ids)
        planned = await self._planned_hours_by_project(ids)
        actuals = await self._actuals_by_project(ids)
        budgets = await self._budgets_by_project(ids)

        rows = []
        total_revenue = Decimal("0")
        total_cost = Decimal("0")
        for p in items:
            ts_cost, actual_hours = approved.get(p.id, (Decimal("0"), Decimal("0")))
            planned_h = planned.get(p.id, Decimal("0"))
            budget = budgets.get(p.id)
            revenue = budget.planned_revenue if budget else p.budget_revenue
            # ФОТ из таймшитов + прочие фактические затраты (payroll в actuals запрещён).
            cost = ts_cost + actuals.get(p.id, Decimal("0"))
            margin = fc.compute_margin(revenue, cost)
            overrun = fc.hours_overrun_pct(planned_h, actual_hours)
            risks = []
            if margin.margin_pct < LOW_MARGIN_PCT:
                risks.append("low_margin")
            if planned_h > 0 and actual_hours / planned_h * Decimal("100") > HOURS_OVERRUN_PCT:
                risks.append("hours_overrun")
            total_revenue += margin.revenue
            total_cost += margin.total_cost
            rows.append(
                {
                    "project_id": p.id,
                    "code": p.code,
                    "name": p.name,
                    "stage": p.stage,
                    "status": p.status,
                    "manager_id": p.manager_id,
                    "revenue": margin.revenue,
                    "margin": margin.margin,
                    "margin_pct": margin.margin_pct,
                    "planned_hours": planned_h,
                    "actual_hours": actual_hours,
                    "hours_overrun_pct": overrun,
                    "risks": risks,
                }
            )
        rows.sort(key=lambda r: r["margin_pct"])  # рискованные (низкая маржа) сверху
        margin_total = fc.compute_margin(total_revenue, total_cost)
        return {
            "projects_count": len(rows),
            "total_revenue": margin_total.revenue,
            "total_margin": margin_total.margin,
            "avg_margin_pct": margin_total.margin_pct,
            "at_risk": sum(1 for r in rows if r["risks"]),
            "rows": rows,
        }

    @staticmethod
    def can_see_financials(user: User) -> bool:
        return bool({Role.ADMIN, Role.DIRECTOR, Role.FINANCE, Role.PM} & set(user.roles))
