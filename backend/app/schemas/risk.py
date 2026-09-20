"""Схемы реестра рисков: CRUD и портфельная сводка (матрица 3×3)."""

from __future__ import annotations

import uuid
from datetime import date, datetime

from pydantic import BaseModel, Field, computed_field

from app.models.enums import (
    ProjectStatus,
    RiskCategory,
    RiskResponse,
    RiskStatus,
    Stage,
)
from app.schemas.common import ORMModel
from app.services.risk_rules import risk_level


class RiskCreate(BaseModel):
    title: str = Field(min_length=1, max_length=255)
    description: str | None = None
    category: RiskCategory = RiskCategory.TECHNICAL
    probability: int = Field(ge=1, le=3)
    impact: int = Field(ge=1, le=3)
    response_strategy: RiskResponse = RiskResponse.MITIGATE
    mitigation_plan: str | None = None
    owner_id: uuid.UUID | None = None
    due_date: date | None = None


class RiskUpdate(BaseModel):
    title: str | None = Field(default=None, min_length=1, max_length=255)
    description: str | None = None
    category: RiskCategory | None = None
    probability: int | None = Field(default=None, ge=1, le=3)
    impact: int | None = Field(default=None, ge=1, le=3)
    status: RiskStatus | None = None
    response_strategy: RiskResponse | None = None
    mitigation_plan: str | None = None
    owner_id: uuid.UUID | None = None
    due_date: date | None = None


class RiskOut(ORMModel):
    id: uuid.UUID
    project_id: uuid.UUID
    title: str
    description: str | None
    category: RiskCategory
    probability: int
    impact: int
    score: int
    status: RiskStatus
    response_strategy: RiskResponse
    mitigation_plan: str | None
    owner_id: uuid.UUID | None
    due_date: date | None
    closed_at: datetime | None
    created_by: uuid.UUID | None
    created_at: datetime
    updated_at: datetime

    @computed_field  # type: ignore[prop-decorator]
    @property
    def level(self) -> str:
        return risk_level(self.score)


# ---------- Портфель рисков ----------
class RiskMatrixCell(BaseModel):
    """Ячейка матрицы вероятность×влияние с числом активных рисков."""

    probability: int
    impact: int
    score: int
    level: str
    count: int


class RiskCategoryCount(BaseModel):
    category: RiskCategory
    count: int


class ProjectRiskRow(BaseModel):
    """Строка портфеля: агрегаты активных рисков по проекту."""

    project_id: uuid.UUID
    code: str
    name: str
    stage: Stage
    status: ProjectStatus
    manager_id: uuid.UUID
    active_count: int
    high_count: int
    top_score: int


class RiskPortfolioResponse(BaseModel):
    projects_count: int          # проектов с активными рисками
    total_active: int            # всего активных рисков
    total_high: int              # из них высокой критичности (score ≥ 6)
    matrix: list[RiskMatrixCell]  # 9 ячеек 3×3
    by_category: list[RiskCategoryCount]
    rows: list[ProjectRiskRow]
