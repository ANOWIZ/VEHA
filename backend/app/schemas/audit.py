"""Схемы аудит-лога."""

from __future__ import annotations

import uuid
from datetime import datetime

from app.models.enums import AuditAction
from app.schemas.common import ORMModel


class AuditEntryOut(ORMModel):
    id: uuid.UUID
    entity: str
    entity_id: uuid.UUID | None
    action: AuditAction
    actor_id: uuid.UUID | None
    diff: dict
    created_at: datetime
    actor_name: str | None = None
