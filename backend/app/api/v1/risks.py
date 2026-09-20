"""API реестра рисков: CRUD рисков проекта и портфельная сводка (матрица 3×3).

Список/чтение — всем с доступом к проекту (риски не содержат финансовых полей).
Создание/изменение/удаление — управляющим проектом (РП/куратор/admin).
Портфель рисков — ролям руководства (director/admin/finance/pm)."""

from __future__ import annotations

import uuid
from typing import Annotated

from fastapi import APIRouter, Depends

from app.core.deps import CurrentUser, DbSession, ProjectAccess, ProjectManage, require_role
from app.core.exceptions import NotFoundError
from app.models.enums import RiskStatus, Role
from app.models.user import User
from app.schemas.common import Message
from app.schemas.risk import RiskCreate, RiskOut, RiskPortfolioResponse, RiskUpdate
from app.services.risk_service import RiskService

# Риски вложены в проект для единообразной проверки доступа.
router = APIRouter(prefix="/projects/{project_id}/risks", tags=["risks"])


@router.get("", response_model=list[RiskOut])
async def list_risks(
    project: ProjectAccess, session: DbSession, status: RiskStatus | None = None
) -> list[RiskOut]:
    risks = await RiskService(session).list_for_project(project.id, status=status)
    return [RiskOut.model_validate(r) for r in risks]


@router.post("", response_model=RiskOut, status_code=201)
async def create_risk(
    body: RiskCreate, project: ProjectManage, session: DbSession, user: CurrentUser
) -> RiskOut:
    risk = await RiskService(session).create(project.id, body, actor_id=user.id)
    return RiskOut.model_validate(risk)


@router.patch("/{risk_id}", response_model=RiskOut)
async def update_risk(
    risk_id: uuid.UUID,
    body: RiskUpdate,
    project: ProjectManage,
    session: DbSession,
    user: CurrentUser,
) -> RiskOut:
    svc = RiskService(session)
    risk = await svc.get(risk_id)
    if risk.project_id != project.id:
        raise NotFoundError("Риск не найден в этом проекте")
    risk = await svc.update(risk_id, body, actor_id=user.id)
    return RiskOut.model_validate(risk)


@router.delete("/{risk_id}", response_model=Message)
async def delete_risk(
    risk_id: uuid.UUID, project: ProjectManage, session: DbSession, user: CurrentUser
) -> Message:
    svc = RiskService(session)
    risk = await svc.get(risk_id)
    if risk.project_id != project.id:
        raise NotFoundError("Риск не найден в этом проекте")
    await svc.delete(risk_id, actor_id=user.id)
    return Message(message="Риск удалён")


# ---------- Портфель рисков ----------
portfolio_router = APIRouter(prefix="/risks", tags=["risks"])

_portfolio_roles = require_role(Role.DIRECTOR, Role.ADMIN, Role.FINANCE, Role.PM)


@portfolio_router.get("/portfolio", response_model=RiskPortfolioResponse)
async def risk_portfolio(
    session: DbSession, user: Annotated[User, Depends(_portfolio_roles)]
) -> RiskPortfolioResponse:
    data = await RiskService(session).portfolio(user)
    return RiskPortfolioResponse.model_validate(data)
