"""Схемы пользователя. Финансовое поле (cost_rate) — только в ролевой схеме
для admin/finance, см. CLAUDE.md §5."""

from __future__ import annotations

import uuid
from datetime import date
from decimal import Decimal

from pydantic import BaseModel, Field, model_validator

from app.schemas.common import ORMModel


class UserPublic(ORMModel):
    """Безопасное представление пользователя — без финансовых полей.
    Отдаётся всем ролям (engineer/presale в том числе)."""

    id: uuid.UUID
    username: str
    email: str
    full_name: str
    department: str | None = None
    position: str | None = None
    grade: str | None = None
    is_active: bool
    roles: list[str] = Field(default_factory=list)


class CostRateOut(ORMModel):
    id: uuid.UUID
    user_id: uuid.UUID
    cost_rate: Decimal
    valid_from: date
    valid_to: date | None = None


class UserWithRate(UserPublic):
    """Расширенное представление с действующей ставкой — только admin/finance."""

    current_cost_rate: Decimal | None = None


class CostRateCreate(BaseModel):
    cost_rate: Decimal = Field(gt=0)
    valid_from: date
    valid_to: date | None = None

    @model_validator(mode="after")
    def _check_dates(self) -> CostRateCreate:
        # Перевёрнутый интервал делает ставку «невыбираемой» (effective_cost_rate
        # требует valid_from <= дата <= valid_to) — снимок cost_rate стал бы None.
        if self.valid_to is not None and self.valid_to < self.valid_from:
            raise ValueError("Дата окончания ставки не может быть раньше даты начала")
        return self


class MeResponse(UserPublic):
    """Текущий пользователь (/me). Роли — источник для навигации фронта."""


class DevLoginRequest(BaseModel):
    username: str = Field(min_length=1, examples=["pm.demo"])
    roles: list[str] = Field(default_factory=lambda: ["engineer"], examples=[["pm"]])
    email: str | None = None
    full_name: str | None = None


class DevLoginResponse(BaseModel):
    access_token: str
    token_type: str = "Bearer"
