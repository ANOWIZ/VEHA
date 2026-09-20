"""Аудит изменений и журнал интеграционных обменов."""

from __future__ import annotations

import uuid

from sqlalchemy import ForeignKey, String, Text
from sqlalchemy.dialects.postgresql import JSONB
from sqlalchemy.dialects.postgresql import UUID as PG_UUID
from sqlalchemy.orm import Mapped, mapped_column

from app.db.base import DomainBase
from app.models.enums import AuditAction, IntegrationDirection


class AuditLog(DomainBase):
    """Кто, что, когда, diff (JSON). Ведётся для Project, Quote, TimeEntry,
    ставок и финансов (см. CLAUDE.md §7)."""

    __tablename__ = "audit_log"

    entity: Mapped[str] = mapped_column(String(64), index=True)
    entity_id: Mapped[uuid.UUID | None] = mapped_column(PG_UUID(as_uuid=True), index=True)
    action: Mapped[AuditAction] = mapped_column(String(32), nullable=False)
    actor_id: Mapped[uuid.UUID | None] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("users.id")
    )
    diff: Mapped[dict] = mapped_column(JSONB, default=dict)


class IntegrationLog(DomainBase):
    """Журнал обменов. Идемпотентность по (system, external_id)."""

    __tablename__ = "integration_log"

    direction: Mapped[IntegrationDirection] = mapped_column(String(16), nullable=False)
    system: Mapped[str] = mapped_column(String(32), index=True)  # onec, cbr, keycloak
    external_id: Mapped[str | None] = mapped_column(String(128), index=True)
    payload: Mapped[dict] = mapped_column(JSONB, default=dict)
    status: Mapped[str] = mapped_column(String(32), default="ok")
    error: Mapped[str | None] = mapped_column(Text)
