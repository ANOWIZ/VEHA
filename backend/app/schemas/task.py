"""Схемы задач проекта."""

from __future__ import annotations

import uuid
from datetime import date
from decimal import Decimal

from pydantic import BaseModel, Field

from app.models.enums import Stage, TaskStatus
from app.schemas.common import ORMModel


class TaskCreate(BaseModel):
    name: str = Field(min_length=1, max_length=255)
    parent_id: uuid.UUID | None = None
    stage: Stage | None = None
    planned_hours: Decimal = Field(default=Decimal("0"), ge=0)
    assignee_id: uuid.UUID | None = None
    due_date: date | None = None


class TaskUpdate(BaseModel):
    name: str | None = Field(default=None, max_length=255)
    stage: Stage | None = None
    planned_hours: Decimal | None = Field(default=None, ge=0)
    assignee_id: uuid.UUID | None = None
    status: TaskStatus | None = None
    due_date: date | None = None


class TaskOut(ORMModel):
    id: uuid.UUID
    project_id: uuid.UUID
    parent_id: uuid.UUID | None
    name: str
    stage: Stage | None
    planned_hours: Decimal
    assignee_id: uuid.UUID | None
    status: TaskStatus
    due_date: date | None
