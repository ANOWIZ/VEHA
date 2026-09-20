"""Схемы проектов: проект, участники, вехи, переходы по стадиям.

Финансовое поле ``budget_revenue`` отдаётся всем с доступом к проекту, но
детальная финансовая аналитика — в отдельном финансовом модуле (Фаза 4)."""

from __future__ import annotations

import uuid
from datetime import date, datetime
from decimal import Decimal

from pydantic import BaseModel, Field

from app.models.enums import (
    ProjectMemberRole,
    ProjectStatus,
    ProjectType,
    Stage,
)
from app.schemas.common import ORMModel


# --- Проект ---
class ProjectCreate(BaseModel):
    name: str = Field(min_length=1, max_length=255)
    client_id: uuid.UUID
    type: ProjectType
    manager_id: uuid.UUID
    curator_id: uuid.UUID | None = None
    planned_start: date | None = None
    planned_end: date | None = None
    budget_revenue: Decimal = Field(default=Decimal("0"), ge=0)
    contract_ref: str | None = None


class ProjectUpdate(BaseModel):
    name: str | None = Field(default=None, max_length=255)
    type: ProjectType | None = None
    manager_id: uuid.UUID | None = None
    curator_id: uuid.UUID | None = None
    status: ProjectStatus | None = None
    planned_start: date | None = None
    planned_end: date | None = None
    actual_start: date | None = None
    actual_end: date | None = None
    budget_revenue: Decimal | None = Field(default=None, ge=0)
    contract_ref: str | None = None


class ProjectOut(ORMModel):
    id: uuid.UUID
    code: str
    name: str
    client_id: uuid.UUID
    type: ProjectType
    manager_id: uuid.UUID
    curator_id: uuid.UUID | None
    stage: Stage
    status: ProjectStatus
    planned_start: date | None
    planned_end: date | None
    actual_start: date | None
    actual_end: date | None
    budget_revenue: Decimal
    contract_ref: str | None
    created_at: datetime


# --- Стадии ---
class StageChangeRequest(BaseModel):
    to_stage: Stage
    reason: str | None = None


class StageTransitionOut(ORMModel):
    id: uuid.UUID
    from_stage: Stage | None
    to_stage: Stage
    reason: str | None
    created_by: uuid.UUID | None
    created_at: datetime


# --- Участники ---
class MemberCreate(BaseModel):
    user_id: uuid.UUID
    role: ProjectMemberRole
    bill_rate: Decimal = Field(default=Decimal("0"), ge=0)
    period_from: date | None = None
    period_to: date | None = None


class MemberOut(ORMModel):
    id: uuid.UUID
    user_id: uuid.UUID
    role: ProjectMemberRole
    # Плановая ставка продажи (bill_rate) — финансово-чувствительна: в роутере
    # обнуляется для ролей без доступа к финансам (engineer/presale/client).
    bill_rate: Decimal | None = None
    period_from: date | None
    period_to: date | None


# --- Вехи ---
class MilestoneCreate(BaseModel):
    name: str = Field(min_length=1, max_length=255)
    milestone_date: date
    is_payment: bool = False
    amount: Decimal = Field(default=Decimal("0"), ge=0)


class MilestoneOut(ORMModel):
    id: uuid.UUID
    name: str
    milestone_date: date
    is_payment: bool
    amount: Decimal


class ProjectDetail(ProjectOut):
    members: list[MemberOut] = Field(default_factory=list)
    milestones: list[MilestoneOut] = Field(default_factory=list)
