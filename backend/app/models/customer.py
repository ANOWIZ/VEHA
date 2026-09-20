"""Портал Заказчика: ожидания работ от Заказчика (блокеры) и артефакты.

Когда на стадии проекта мы ждём от Заказчика данные/свидетельства, заводится
CustomerActionItem со статусом WAITING — проект «стоит» на стороне Заказчика,
указан ответственный с его стороны. Заказчик предоставляет артефакты через
портал; мы принимаем — блокер снимается.
"""

from __future__ import annotations

import uuid
from datetime import datetime

from sqlalchemy import DateTime, ForeignKey, Integer, String, Text
from sqlalchemy.dialects.postgresql import UUID as PG_UUID
from sqlalchemy.orm import Mapped, mapped_column, relationship

from app.db.base import DomainBase
from app.models.enums import CustomerActionStatus, Stage


class CustomerActionItem(DomainBase):
    __tablename__ = "customer_action_items"

    project_id: Mapped[uuid.UUID] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("projects.id"), index=True
    )
    stage: Mapped[Stage | None] = mapped_column(String(20))  # на какой стадии ждём
    title: Mapped[str] = mapped_column(String(255))
    description: Mapped[str | None] = mapped_column(Text)

    # Ответственный со стороны Заказчика (может не быть пользователем системы).
    responsible_name: Mapped[str] = mapped_column(String(255))
    responsible_email: Mapped[str | None] = mapped_column(String(255))
    responsible_user_id: Mapped[uuid.UUID | None] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("users.id"), nullable=True
    )

    due_date: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))
    status: Mapped[CustomerActionStatus] = mapped_column(
        String(16), default=CustomerActionStatus.WAITING, index=True
    )
    created_by: Mapped[uuid.UUID | None] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("users.id")
    )
    provided_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))
    accepted_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))
    provided_note: Mapped[str | None] = mapped_column(Text)

    artifacts: Mapped[list[Artifact]] = relationship(
        back_populates="action_item", lazy="selectin"
    )


class Artifact(DomainBase):
    """Файл, загруженный Заказчиком/нами (свидетельства, документы)."""

    __tablename__ = "artifacts"

    project_id: Mapped[uuid.UUID] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("projects.id"), index=True
    )
    action_item_id: Mapped[uuid.UUID | None] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("customer_action_items.id"), index=True
    )
    filename: Mapped[str] = mapped_column(String(255))
    content_type: Mapped[str | None] = mapped_column(String(128))
    size_bytes: Mapped[int] = mapped_column(Integer, default=0)
    storage_path: Mapped[str] = mapped_column(String(512))
    uploaded_by: Mapped[uuid.UUID | None] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("users.id")
    )

    action_item: Mapped[CustomerActionItem | None] = relationship(back_populates="artifacts")
