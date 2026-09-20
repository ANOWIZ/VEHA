"""API дашбордов: портфель проектов (директор/руководство)."""

from __future__ import annotations

from typing import Annotated

from fastapi import APIRouter, Depends

from app.core.deps import CurrentUser, DbSession, require_role
from app.models.enums import Role
from app.models.user import User
from app.schemas.dashboard import PortfolioResponse
from app.services.dashboard_service import DashboardService

router = APIRouter(prefix="/dashboards", tags=["dashboards"])

_fin_roles = require_role(Role.DIRECTOR, Role.ADMIN, Role.FINANCE, Role.PM)


@router.get("/portfolio", response_model=PortfolioResponse)
async def portfolio(
    session: DbSession, user: Annotated[User, Depends(_fin_roles)]
) -> PortfolioResponse:
    data = await DashboardService(session).portfolio(user)
    return PortfolioResponse.model_validate(data)


@router.get("/me", response_model=PortfolioResponse)
async def my_projects(session: DbSession, user: CurrentUser) -> PortfolioResponse:
    """Здоровье моих проектов (для РП — его проекты; для прочих — доступные)."""
    if not DashboardService.can_see_financials(user):
        # Инженер/пресейл финансы не видят — пустой портфель (статусы смотрят в проектах).
        return PortfolioResponse(
            projects_count=0, total_revenue=0, total_margin=0, avg_margin_pct=0, at_risk=0, rows=[]
        )
    data = await DashboardService(session).portfolio(user)
    return PortfolioResponse.model_validate(data)
