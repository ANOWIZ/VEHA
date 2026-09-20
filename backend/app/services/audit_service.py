"""Аудит изменений доменных сущностей (Project, Quote, TimeEntry, ставки, финансы)."""

from __future__ import annotations

import uuid
from collections.abc import Sequence
from typing import Any

from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.audit import AuditLog
from app.models.enums import AuditAction


class AuditService:
    def __init__(self, session: AsyncSession) -> None:
        self.session = session

    async def record(
        self,
        *,
        entity: str,
        entity_id: uuid.UUID | None,
        action: AuditAction,
        actor_id: uuid.UUID | None,
        diff: dict[str, Any] | None = None,
    ) -> None:
        self.session.add(
            AuditLog(
                entity=entity,
                entity_id=entity_id,
                action=action,
                actor_id=actor_id,
                diff=diff or {},
            )
        )
        await self.session.flush()

    async def list(
        self,
        *,
        entity: str | None = None,
        entity_id: uuid.UUID | None = None,
        limit: int = 100,
        offset: int = 0,
    ) -> tuple[Sequence[AuditLog], int]:
        from sqlalchemy import func

        stmt = select(AuditLog)
        count_stmt = select(func.count()).select_from(AuditLog)
        if entity:
            stmt = stmt.where(AuditLog.entity == entity)
            count_stmt = count_stmt.where(AuditLog.entity == entity)
        if entity_id:
            stmt = stmt.where(AuditLog.entity_id == entity_id)
            count_stmt = count_stmt.where(AuditLog.entity_id == entity_id)
        stmt = stmt.order_by(AuditLog.created_at.desc()).limit(limit).offset(offset)
        items = (await self.session.execute(stmt)).scalars().all()
        total = int((await self.session.execute(count_stmt)).scalar_one())
        return items, total
