"""API ресурсного планирования: тепловая карта загрузки и план."""

from __future__ import annotations

from datetime import date
from typing import Annotated

from fastapi import APIRouter, Depends, Query

from app.core.deps import DbSession, require_role
from app.models.enums import Role
from app.schemas.resource import HeatmapResponse, ResourcePlanOut, ResourcePlanUpsert
from app.services.resource_service import ResourceService

router = APIRouter(prefix="/resources", tags=["resources"])

# Планирование ресурсов — РП/директор/админ/финансы.
_access = require_role(Role.PM, Role.DIRECTOR, Role.ADMIN, Role.FINANCE)


@router.get("/heatmap", response_model=HeatmapResponse, dependencies=[Depends(_access)])
async def heatmap(
    session: DbSession,
    week_from: date | None = None,
    weeks: Annotated[int, Query(ge=1, le=26)] = 8,
) -> HeatmapResponse:
    data = await ResourceService(session).heatmap(week_from or date.today(), weeks)
    return HeatmapResponse.model_validate(data)


@router.post(
    "/plan", response_model=ResourcePlanOut, status_code=201, dependencies=[Depends(_access)]
)
async def upsert_plan(body: ResourcePlanUpsert, session: DbSession) -> ResourcePlanOut:
    cell = await ResourceService(session).upsert_plan(body)
    return ResourcePlanOut.model_validate(cell)
