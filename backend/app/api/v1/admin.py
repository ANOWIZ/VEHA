"""API администрирования: аудит-лог и выгрузка в 1С."""

from __future__ import annotations

import uuid
from datetime import date
from typing import Annotated

from fastapi import APIRouter, Depends, Query

from app.core.deps import DbSession, require_role
from app.models.enums import Role
from app.repositories.user_repo import UserRepository
from app.schemas.audit import AuditEntryOut
from app.schemas.common import Page
from app.services.audit_service import AuditService

router = APIRouter(prefix="/admin", tags=["admin"])

_audit_roles = require_role(Role.ADMIN, Role.DIRECTOR, Role.FINANCE)


@router.get("/audit", response_model=Page[AuditEntryOut], dependencies=[Depends(_audit_roles)])
async def list_audit(
    session: DbSession,
    entity: str | None = None,
    entity_id: uuid.UUID | None = None,
    limit: Annotated[int, Query(ge=1, le=500)] = 100,
    offset: Annotated[int, Query(ge=0)] = 0,
) -> Page[AuditEntryOut]:
    items, total = await AuditService(session).list(
        entity=entity, entity_id=entity_id, limit=limit, offset=offset
    )
    users_repo = UserRepository(session)
    cache: dict[uuid.UUID, str] = {}
    out: list[AuditEntryOut] = []
    for a in items:
        entry = AuditEntryOut.model_validate(a)
        if a.actor_id is not None:
            if a.actor_id not in cache:
                u = await users_repo.get(a.actor_id)
                cache[a.actor_id] = u.full_name if u else "—"
            entry.actor_name = cache[a.actor_id]
        out.append(entry)
    return Page[AuditEntryOut](items=out, total=total, limit=limit, offset=offset)


@router.post(
    "/integrations/1c/export-timesheets",
    dependencies=[Depends(require_role(Role.ADMIN, Role.FINANCE))],
)
async def export_timesheets_1c(
    session: DbSession,
    period_from: date,
    period_to: date,
) -> dict:
    """Выгрузка утверждённых трудозатрат в 1С за период (идемпотентно)."""
    from app.services.onec_service import OneCService

    result = await OneCService(session).export_timesheets(period_from, period_to)
    await session.commit()
    return result
