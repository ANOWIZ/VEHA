"""Схемы портала Заказчика: ожидания работ (блокеры) и артефакты."""

from __future__ import annotations

import uuid
from datetime import datetime

from pydantic import BaseModel, Field

from app.models.enums import CustomerActionStatus, ProjectStatus, Stage
from app.schemas.common import ORMModel

# Базовая проверка формата email (без внешней зависимости email-validator).
_EMAIL_PATTERN = r"^[^@\s]+@[^@\s]+\.[^@\s]+$"


class ArtifactOut(ORMModel):
    id: uuid.UUID
    filename: str
    content_type: str | None
    size_bytes: int
    created_at: datetime
    uploaded_by: uuid.UUID | None


class ActionItemCreate(BaseModel):
    title: str = Field(min_length=1, max_length=255)
    description: str | None = None
    stage: Stage | None = None
    responsible_name: str = Field(min_length=1, max_length=255)
    responsible_email: str | None = Field(default=None, pattern=_EMAIL_PATTERN)
    responsible_user_id: uuid.UUID | None = None
    due_date: datetime | None = None


class ActionItemProvide(BaseModel):
    note: str | None = None


class ActionItemOut(ORMModel):
    id: uuid.UUID
    project_id: uuid.UUID
    stage: Stage | None
    title: str
    description: str | None
    responsible_name: str
    responsible_email: str | None
    responsible_user_id: uuid.UUID | None
    due_date: datetime | None
    status: CustomerActionStatus
    provided_at: datetime | None
    accepted_at: datetime | None
    provided_note: str | None
    created_at: datetime
    artifacts: list[ArtifactOut] = Field(default_factory=list)


class PortalProject(BaseModel):
    """Карточка проекта для портала Заказчика (без финансов)."""

    project_id: uuid.UUID
    code: str
    name: str
    stage: Stage
    status: ProjectStatus
    planned_end: datetime | None = None
    open_actions: int
    blocked_on_customer: bool


class PortalProjectDetail(BaseModel):
    project_id: uuid.UUID
    code: str
    name: str
    stage: Stage
    status: ProjectStatus
    blocked_on_customer: bool
    action_items: list[ActionItemOut]
