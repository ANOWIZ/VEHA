"""Справочник пользователей и управление ставками себестоимости."""

from __future__ import annotations

import uuid
from typing import Annotated

from fastapi import APIRouter, Depends, Query

from app.core.deps import CurrentUser, DbSession, can_see_financials, require_role
from app.models.enums import Role
from app.schemas.common import Page
from app.schemas.user import (
    CostRateCreate,
    CostRateOut,
    UserPublic,
    UserWithRate,
)
from app.services.user_service import UserService

router = APIRouter(prefix="/users", tags=["users"])


@router.get("", response_model=Page[UserPublic])
async def list_users(
    session: DbSession,
    _user: CurrentUser,
    limit: Annotated[int, Query(ge=1, le=500)] = 50,
    offset: Annotated[int, Query(ge=0)] = 0,
    active_only: bool = True,
) -> Page[UserPublic]:
    users, total = await UserService(session).list_users(
        limit=limit, offset=offset, active_only=active_only
    )
    return Page[UserPublic](
        items=[UserPublic.model_validate(u) for u in users],
        total=total,
        limit=limit,
        offset=offset,
    )


@router.get("/{user_id}", response_model=UserWithRate)
async def get_user(
    user_id: uuid.UUID,
    session: DbSession,
    current: CurrentUser,
) -> UserWithRate:
    from datetime import date

    svc = UserService(session)
    user = await svc.get(user_id)
    out = UserWithRate.model_validate(user)
    # Финансовое поле — только для ролей с доступом к финансам.
    if can_see_financials(current):
        out.current_cost_rate = await svc.effective_cost_rate(user_id, date.today())
    return out


@router.post(
    "/{user_id}/cost-rates",
    response_model=CostRateOut,
    dependencies=[Depends(require_role(Role.ADMIN, Role.FINANCE))],
)
async def set_cost_rate(
    user_id: uuid.UUID,
    body: CostRateCreate,
    session: DbSession,
) -> CostRateOut:
    """Назначить версию ставки себестоимости. Только admin/finance."""
    rate = await UserService(session).set_cost_rate(
        user_id, body.cost_rate, body.valid_from, body.valid_to
    )
    return CostRateOut.model_validate(rate)
