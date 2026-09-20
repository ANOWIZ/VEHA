"""Аутентификация: dev-login (только dev) и информация о текущем пользователе."""

from __future__ import annotations

from fastapi import APIRouter

from app.core.config import settings
from app.core.deps import CurrentUser
from app.core.exceptions import ForbiddenError
from app.core.security import issue_dev_token
from app.schemas.user import DevLoginRequest, DevLoginResponse, MeResponse

router = APIRouter(prefix="/auth", tags=["auth"])


@router.post("/dev-login", response_model=DevLoginResponse)
async def dev_login(body: DevLoginRequest) -> DevLoginResponse:
    """Выпуск локального токена для разработки. В production недоступно.

    Пример: ``{"username": "pm.demo", "roles": ["pm"]}`` → Bearer-токен для Swagger.
    """
    if not settings.dev_auth_allowed:
        raise ForbiddenError("Dev-аутентификация отключена (включена только вне production)")
    token = issue_dev_token(
        username=body.username,
        roles=body.roles,
        email=body.email or "",
        name=body.full_name or "",
    )
    return DevLoginResponse(access_token=token)


@router.get("/me", response_model=MeResponse)
async def me(user: CurrentUser) -> MeResponse:
    """Текущий пользователь и его роли — основа навигации фронта по ролям."""
    return MeResponse.model_validate(user)
