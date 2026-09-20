"""Схемы уведомлений."""

from __future__ import annotations

import uuid
from datetime import datetime

from pydantic import BaseModel

from app.schemas.common import ORMModel


class NotificationOut(ORMModel):
    id: uuid.UUID
    kind: str
    title: str
    body: str | None
    link: str | None
    is_read: bool
    created_at: datetime


class UnreadCount(BaseModel):
    count: int
