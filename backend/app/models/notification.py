"""Уведомления пользователю (in-app). Email/Telegram — поверх той же модели."""

from __future__ import annotations

import uuid

from sqlalchemy import Boolean, ForeignKey, String, Text
from sqlalchemy.dialects.postgresql import UUID as PG_UUID
from sqlalchemy.orm import Mapped, mapped_column

from app.db.base import DomainBase


class Notification(DomainBase):
    __tablename__ = "notifications"

    user_id: Mapped[uuid.UUID] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("users.id"), index=True
    )
    kind: Mapped[str] = mapped_column(String(48))  # timesheet_rejected / action_assigned / ...
    title: Mapped[str] = mapped_column(String(255))
    body: Mapped[str | None] = mapped_column(Text)
    link: Mapped[str | None] = mapped_column(String(255))
    is_read: Mapped[bool] = mapped_column(Boolean, default=False, index=True)
