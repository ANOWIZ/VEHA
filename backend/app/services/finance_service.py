"""Сервис финансов: бюджет, фактические затраты, маржинальность, прогноз.

ФОТ считается автоматически из approved-таймшитов (часы × снимок ставки);
остальные затраты — из ``actual_costs`` (ручной ввод/1С). Маржинальность
кэшируется в Redis и пересчитывается при утверждении таймшитов/изменении затрат.
"""

from __future__ import annotations

import json
import uuid
from datetime import UTC, datetime
from decimal import Decimal

from sqlalchemy import func, select
from sqlalchemy.exc import IntegrityError
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.exceptions import (
    AppError,
    ConflictError,
    ErrorCode,
    NotFoundError,
    ValidationError,
)
from app.models.enums import AuditAction, CostCategory, CostSource, ProjectStatus
from app.models.finance import ActualCost, Forecast, ProjectBudget
from app.models.project import Project, Task
from app.repositories.finance_repo import FinanceRepository
from app.repositories.timesheet_repo import TimesheetRepository
from app.schemas.finance import ActualCostCreate, BudgetUpsert
from app.services import finance_calc as fc
from app.services.audit_service import AuditService

_MARGIN_CACHE_TTL = 3600


class FinanceService:
    def __init__(self, session: AsyncSession) -> None:
        self.session = session
        self.repo = FinanceRepository(session)
        self.timesheets = TimesheetRepository(session)
        self.audit = AuditService(session)

    async def _project(self, project_id: uuid.UUID) -> Project:
        project = await self.session.get(Project, project_id)
        if project is None or project.deleted_at is not None:
            raise NotFoundError("Проект не найден")
        return project

    # ---------- Бюджет ----------
    async def get_budget(self, project_id: uuid.UUID) -> ProjectBudget | None:
        return await self.repo.get_budget(project_id)

    async def upsert_budget(
        self, project_id: uuid.UUID, data: BudgetUpsert, actor_id: uuid.UUID
    ) -> ProjectBudget:
        project = await self._project(project_id)
        # Финансовая база закрытого/отменённого проекта неизменяема (как и стадии).
        if project.status in (ProjectStatus.CLOSED, ProjectStatus.CANCELLED):
            raise AppError(
                "Нельзя менять бюджет закрытого или отменённого проекта",
                code=ErrorCode.PROJECT_CLOSED,
                status_code=409,
            )
        budget = await self.repo.get_budget(project_id)
        old = (
            {
                "planned_revenue": str(budget.planned_revenue),
                "planned_costs": dict(budget.planned_costs or {}),
            }
            if budget is not None
            else None
        )
        if budget is None:
            budget = ProjectBudget(
                project_id=project_id,
                planned_revenue=data.planned_revenue,
                planned_costs=data.planned_costs,
            )
            self.session.add(budget)
        else:
            budget.planned_revenue = data.planned_revenue
            budget.planned_costs = data.planned_costs
        await self.audit.record(
            entity="ProjectBudget",
            entity_id=project_id,
            action=AuditAction.UPDATE,
            actor_id=actor_id,
            diff={
                "from": old,
                "to": {
                    "planned_revenue": str(data.planned_revenue),
                    "planned_costs": dict(data.planned_costs),
                },
            },
        )
        await self.session.flush()
        return budget

    # ---------- Фактические затраты ----------
    async def list_actuals(self, project_id: uuid.UUID) -> list[ActualCost]:
        return list(await self.repo.list_actuals(project_id))

    async def add_actual(
        self, project_id: uuid.UUID, data: ActualCostCreate, actor_id: uuid.UUID
    ) -> ActualCost:
        await self._project(project_id)
        # ФОТ считается автоматически из таймшитов — вручную его вносить нельзя.
        if data.category == CostCategory.PAYROLL:
            raise ValidationError(
                "Категория «ФОТ» считается автоматически из утверждённых таймшитов"
            )
        # Идемпотентность по external_id в рамках проекта (обмены с 1С): id из 1С
        # уникальны в пределах организации/проекта, не глобально. SELECT — быстрый
        # путь; гонку двух параллельных загрузок ловит partial unique constraint
        # (uq_actual_project_external) ниже через IntegrityError.
        if data.external_id:
            existing = await self.repo.find_actual_by_external(project_id, data.external_id)
            if existing is not None:
                raise ConflictError("Затрата с таким external_id уже загружена в этот проект")
        # Ручной ввод — источник всегда MANUAL; не доверяем полю source из запроса
        # (TIMESHEET/ONEC проставляют только внутренние процессы).
        actual = ActualCost(
            project_id=project_id,
            source=CostSource.MANUAL,
            **data.model_dump(exclude={"source"}),
        )
        self.session.add(actual)
        await self.audit.record(
            entity="ActualCost",
            entity_id=project_id,
            action=AuditAction.CREATE,
            actor_id=actor_id,
            diff={"category": str(data.category), "amount": str(data.amount)},
        )
        try:
            await self.session.flush()
        except IntegrityError as exc:
            raise ConflictError(
                "Затрата с таким external_id уже загружена в этот проект"
            ) from exc
        await self.recalc_margin(project_id)
        return actual

    # ---------- Часы ----------
    async def _planned_hours(self, project_id: uuid.UUID) -> Decimal:
        stmt = select(func.coalesce(func.sum(Task.planned_hours), 0)).where(
            Task.project_id == project_id, Task.deleted_at.is_(None)
        )
        return Decimal(str((await self.session.execute(stmt)).scalar_one()))

    async def _actual_hours(self, project_id: uuid.UUID) -> Decimal:
        return await self.timesheets.project_approved_hours(project_id)

    # ---------- Маржинальность ----------
    async def _cost_breakdown(self, project_id: uuid.UUID) -> dict[str, Decimal]:
        ts_payroll = await self.timesheets.project_approved_cost(project_id)
        breakdown = await self.repo.actuals_by_category(project_id)
        # ФОТ берётся ТОЛЬКО из утверждённых таймшитов — перезаписываем категорию,
        # чтобы не задвоить с возможными ручными payroll-затратами (add_actual их
        # запрещает, это дополнительная защита).
        breakdown[str(CostCategory.PAYROLL)] = ts_payroll
        return breakdown

    async def compute_margin(self, project_id: uuid.UUID) -> tuple[fc.MarginResult, dict]:
        project = await self._project(project_id)
        budget = await self.repo.get_budget(project_id)
        revenue = budget.planned_revenue if budget else project.budget_revenue
        breakdown = await self._cost_breakdown(project_id)
        total = fc.total_cost(breakdown)
        result = fc.compute_margin(revenue, total)
        return result, {k: str(v) for k, v in breakdown.items()}

    async def recalc_margin(self, project_id: uuid.UUID) -> dict:
        """Пересчитать маржу и закэшировать в Redis. Возвращает dict для Celery."""
        result, breakdown = await self.compute_margin(project_id)
        payload = {**result.as_dict(), "cost_breakdown": breakdown}
        try:
            from app.core.redis import get_redis

            await get_redis().set(
                f"margin:{project_id}", json.dumps(payload), ex=_MARGIN_CACHE_TTL
            )
        except Exception:
            pass
        # Снимок прогноза.
        await self._snapshot_forecast(project_id)
        return payload

    async def _snapshot_forecast(self, project_id: uuid.UUID) -> Forecast:
        budget = await self.repo.get_budget(project_id)
        planned_total = (
            fc.total_cost({k: Decimal(str(v)) for k, v in (budget.planned_costs or {}).items()})
            if budget
            else Decimal("0")
        )
        breakdown = await self._cost_breakdown(project_id)
        actual_to_date = fc.total_cost(breakdown)
        remaining = planned_total - actual_to_date
        f = fc.compute_forecast(actual_to_date, remaining)
        snapshot = Forecast(
            project_id=project_id,
            eac=f.eac,
            etc=f.etc,
            calculated_at=datetime.now(UTC),
        )
        self.session.add(snapshot)
        await self.session.flush()
        return snapshot

    async def project_finance(self, project_id: uuid.UUID) -> dict:
        project = await self._project(project_id)
        budget = await self.repo.get_budget(project_id)
        result, breakdown = await self.compute_margin(project_id)
        planned_hours = await self._planned_hours(project_id)
        actual_hours = await self._actual_hours(project_id)
        planned_total = (
            fc.total_cost({k: Decimal(str(v)) for k, v in (budget.planned_costs or {}).items()})
            if budget
            else Decimal("0")
        )
        forecast = fc.compute_forecast(result.total_cost, planned_total - result.total_cost)
        return {
            "project_id": project.id,
            "budget": budget,
            "margin": {
                "revenue": result.revenue,
                "total_cost": result.total_cost,
                "margin": result.margin,
                "margin_pct": result.margin_pct,
                "cost_breakdown": breakdown,
            },
            "forecast": {
                "eac": forecast.eac,
                "etc": forecast.etc,
                "calculated_at": datetime.now(UTC),
            },
            "planned_hours": planned_hours,
            "actual_hours": actual_hours,
            "hours_overrun_pct": fc.hours_overrun_pct(planned_hours, actual_hours),
        }
