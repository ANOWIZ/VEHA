"""Схемы трудозатрат: записи, недельная сетка, отправка и утверждение."""

from __future__ import annotations

import uuid
from datetime import date, datetime
from decimal import Decimal

from pydantic import BaseModel, Field

from app.models.enums import TimeEntryStatus
from app.schemas.common import ORMModel


class TimeEntryCreate(BaseModel):
    project_id: uuid.UUID
    task_id: uuid.UUID | None = None
    work_date: date
    hours: Decimal = Field(gt=0, le=24)
    comment: str = Field(min_length=1, max_length=2000)  # комментарий обязателен


class TimeEntryUpdate(BaseModel):
    task_id: uuid.UUID | None = None
    hours: Decimal | None = Field(default=None, gt=0, le=24)
    comment: str | None = Field(default=None, min_length=1, max_length=2000)


class TimeEntryOut(ORMModel):
    id: uuid.UUID
    user_id: uuid.UUID
    project_id: uuid.UUID
    task_id: uuid.UUID | None
    work_date: date
    hours: Decimal
    comment: str
    status: TimeEntryStatus
    approved_by: uuid.UUID | None
    approved_at: datetime | None
    reject_reason: str | None
    reversal_of: uuid.UUID | None


class WeekResponse(BaseModel):
    week_start: date
    week_end: date
    status: TimeEntryStatus
    entries: list[TimeEntryOut]
    total_hours: Decimal
    daily_totals: dict[str, Decimal]  # ISO-дата -> часы


class SubmitWeekRequest(BaseModel):
    week_start: date


class CopyWeekRequest(BaseModel):
    target_week_start: date
    source_week_start: date


class ApprovalDecision(BaseModel):
    entry_ids: list[uuid.UUID] = Field(min_length=1)
    reason: str | None = None  # обязателен при отклонении


class PendingEntry(TimeEntryOut):
    """Запись на утверждение с контекстом (имя пользователя/проекта)."""

    user_name: str | None = None
    project_code: str | None = None
