"""API финансов проекта: бюджет, факт, маржинальность, сводка «здоровье».

Доступ — только ролям с финансами (admin/director/finance/pm) и с доступом к
проекту (``ProjectFinancials``)."""

from __future__ import annotations

from fastapi import APIRouter, Depends

from app.core.deps import CurrentUser, DbSession, ProjectFinancials, require_role
from app.models.enums import Role
from app.schemas.finance import (
    ActualCostCreate,
    ActualCostOut,
    BudgetOut,
    BudgetUpsert,
    ForecastOut,
    MarginOut,
    ProjectFinance,
)
from app.services.finance_service import FinanceService

router = APIRouter(prefix="/projects/{project_id}/finance", tags=["finance"])


@router.get("", response_model=ProjectFinance)
async def project_finance(project: ProjectFinancials, session: DbSession) -> ProjectFinance:
    data = await FinanceService(session).project_finance(project.id)
    return ProjectFinance(
        project_id=data["project_id"],
        budget=BudgetOut.model_validate(data["budget"]) if data["budget"] else None,
        margin=MarginOut(**data["margin"]),
        forecast=ForecastOut(**data["forecast"]),
        planned_hours=data["planned_hours"],
        actual_hours=data["actual_hours"],
        hours_overrun_pct=data["hours_overrun_pct"],
    )


@router.get("/budget", response_model=BudgetOut | None)
async def get_budget(project: ProjectFinancials, session: DbSession) -> BudgetOut | None:
    budget = await FinanceService(session).get_budget(project.id)
    return BudgetOut.model_validate(budget) if budget else None


@router.put(
    "/budget",
    response_model=BudgetOut,
    dependencies=[Depends(require_role(Role.ADMIN, Role.FINANCE, Role.PM))],
)
async def upsert_budget(
    body: BudgetUpsert, project: ProjectFinancials, session: DbSession, user: CurrentUser
) -> BudgetOut:
    budget = await FinanceService(session).upsert_budget(project.id, body, actor_id=user.id)
    return BudgetOut.model_validate(budget)


@router.get("/actuals", response_model=list[ActualCostOut])
async def list_actuals(project: ProjectFinancials, session: DbSession) -> list[ActualCostOut]:
    items = await FinanceService(session).list_actuals(project.id)
    return [ActualCostOut.model_validate(a) for a in items]


@router.post(
    "/actuals",
    response_model=ActualCostOut,
    status_code=201,
    dependencies=[Depends(require_role(Role.ADMIN, Role.FINANCE, Role.PM))],
)
async def add_actual(
    body: ActualCostCreate, project: ProjectFinancials, session: DbSession, user: CurrentUser
) -> ActualCostOut:
    actual = await FinanceService(session).add_actual(project.id, body, actor_id=user.id)
    return ActualCostOut.model_validate(actual)


@router.get("/margin", response_model=MarginOut)
async def get_margin(project: ProjectFinancials, session: DbSession) -> MarginOut:
    result, breakdown = await FinanceService(session).compute_margin(project.id)
    return MarginOut(
        revenue=result.revenue,
        total_cost=result.total_cost,
        margin=result.margin,
        margin_pct=result.margin_pct,
        cost_breakdown=breakdown,
    )
