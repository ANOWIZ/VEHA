"""FastAPI-зависимости: текущий пользователь, проверки ролей и доступа к проекту.

Права проверяются здесь (в зависимостях роутера), а не в сервисах (CLAUDE.md §5).
"""

from __future__ import annotations

import uuid
from collections.abc import Awaitable, Callable
from typing import Annotated

from fastapi import Depends, Header, Path
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.exceptions import ForbiddenError, NotFoundError, UnauthenticatedError
from app.core.security import (
    Principal,
    principal_from_dev_header,
    principal_from_token,
)
from app.db.session import get_db
from app.models.enums import Role
from app.models.project import Project, ProjectMember
from app.models.user import User
from app.services.user_service import UserService

# auto_error=False — сами формируем 401 в едином формате ошибок.
_bearer = HTTPBearer(auto_error=False)

DbSession = Annotated[AsyncSession, Depends(get_db)]


async def get_principal(
    creds: Annotated[HTTPAuthorizationCredentials | None, Depends(_bearer)] = None,
    x_dev_user: Annotated[str | None, Header(alias="X-Dev-User")] = None,
) -> Principal:
    if creds is not None and creds.credentials:
        return await principal_from_token(creds.credentials)
    if x_dev_user:
        return principal_from_dev_header(x_dev_user)
    raise UnauthenticatedError("Требуется аутентификация")


async def get_current_user(
    session: DbSession,
    principal: Annotated[Principal, Depends(get_principal)],
) -> User:
    """Синхронизирует пользователя из токена (JIT-provisioning) и возвращает его."""
    user = await UserService(session).sync_from_principal(principal)
    if not user.is_active:
        raise ForbiddenError("Учётная запись деактивирована")
    return user


CurrentUser = Annotated[User, Depends(get_current_user)]


def require_role(*roles: Role | str) -> Callable[..., Awaitable[User]]:
    """Зависимость-фабрика: пропускает только пользователей с одной из ролей."""
    allowed = {str(r) for r in roles}

    async def _dep(user: CurrentUser) -> User:
        if not (allowed & set(user.roles)):
            raise ForbiddenError(
                f"Недостаточно прав. Требуется одна из ролей: {', '.join(sorted(allowed))}"
            )
        return user

    return _dep


# Роли, которым доступны финансовые поля (cost_rate, маржа, себестоимость).
FINANCIAL_ROLES = {Role.ADMIN, Role.DIRECTOR, Role.FINANCE, Role.PM}


def can_see_financials(user: User) -> bool:
    return bool({str(r) for r in FINANCIAL_ROLES} & set(user.roles))


async def require_project_access(
    project_id: Annotated[uuid.UUID, Path()],
    session: DbSession,
    user: CurrentUser,
) -> Project:
    """Доступ к проекту: admin/director/finance видят все; pm/инженер/аналитик —
    свои (руководитель, куратор или участник)."""
    project = (
        await session.execute(
            select(Project).where(Project.id == project_id, Project.deleted_at.is_(None))
        )
    ).scalar_one_or_none()
    if project is None:
        raise NotFoundError("Проект не найден")

    roles = set(user.roles)
    if {Role.ADMIN, Role.DIRECTOR, Role.FINANCE} & roles:
        return project
    if project.manager_id == user.id or project.curator_id == user.id:
        return project
    is_member = (
        await session.execute(
            select(ProjectMember.id).where(
                ProjectMember.project_id == project_id,
                ProjectMember.user_id == user.id,
                ProjectMember.deleted_at.is_(None),
            )
        )
    ).first()
    if is_member is not None:
        return project
    raise ForbiddenError("Нет доступа к этому проекту")


async def require_project_manage(
    project: Annotated[Project, Depends(require_project_access)],
    user: CurrentUser,
) -> Project:
    """Управление проектом (изменение, утверждение): admin или РП/куратор проекта."""
    if Role.ADMIN in set(user.roles):
        return project
    if project.manager_id == user.id or project.curator_id == user.id:
        return project
    raise ForbiddenError("Управлять проектом может администратор, РП или куратор")


async def require_project_financials(
    project: Annotated[Project, Depends(require_project_access)],
    user: CurrentUser,
) -> Project:
    """Доступ к финансам проекта: есть доступ к проекту И финансовая роль.
    Инженер-участник проект видит, но финансы (маржа/себестоимость) — нет."""
    if not can_see_financials(user):
        raise ForbiddenError("Финансовые данные доступны только ролям с доступом к финансам")
    return project


ProjectAccess = Annotated[Project, Depends(require_project_access)]
ProjectManage = Annotated[Project, Depends(require_project_manage)]
ProjectFinancials = Annotated[Project, Depends(require_project_financials)]
