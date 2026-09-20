"""Схемы финансов проекта. Финансовые поля доступны только ролям с доступом
к финансам (admin/director/finance/pm) — проверка в роутере."""

from __future__ import annotations

import uuid
from datetime import date, datetime
from decimal import Decimal, InvalidOperation

from pydantic import BaseModel, Field, field_validator

from app.models.enums import CostCategory, CostSource
from app.schemas.common import ORMModel


class BudgetUpsert(BaseModel):
    planned_revenue: Decimal = Field(default=Decimal("0"), ge=0)
    # {"payroll": "...", "licenses": "...", "subcontract": "...", "travel": "...", "other": "..."}
    planned_costs: dict[str, str] = Field(default_factory=dict)

    @field_validator("planned_costs")
    @classmethod
    def _validate_planned_costs(cls, v: dict[str, str]) -> dict[str, str]:
        """Ключи — валидные CostCategory, значения — неотрицательные числа.
        Иначе ``Decimal(str(v))`` в finance_service падал бы InvalidOperation (500),
        а мусорные категории/отрицательные суммы тихо искажали бы прогноз."""
        allowed = {str(c) for c in CostCategory}
        normalized: dict[str, str] = {}
        for key, raw in v.items():
            if key not in allowed:
                raise ValueError(
                    f"Неизвестная категория затрат «{key}». "
                    f"Допустимо: {', '.join(sorted(allowed))}"
                )
            try:
                amount = Decimal(str(raw))
            except (InvalidOperation, ValueError, TypeError) as exc:
                raise ValueError(f"Сумма категории «{key}» не является числом: {raw!r}") from exc
            if amount < 0:
                raise ValueError(f"Сумма категории «{key}» не может быть отрицательной")
            normalized[key] = str(amount)
        return normalized


class BudgetOut(ORMModel):
    id: uuid.UUID
    project_id: uuid.UUID
    planned_revenue: Decimal
    planned_costs: dict


class ActualCostCreate(BaseModel):
    category: CostCategory
    amount: Decimal = Field(ge=0)
    source: CostSource = CostSource.MANUAL
    occurred_on: date
    external_id: str | None = None
    description: str | None = None


class ActualCostOut(ORMModel):
    id: uuid.UUID
    project_id: uuid.UUID
    category: CostCategory
    amount: Decimal
    source: CostSource
    occurred_on: date
    external_id: str | None
    description: str | None


class MarginOut(BaseModel):
    revenue: Decimal
    total_cost: Decimal
    margin: Decimal
    margin_pct: Decimal
    cost_breakdown: dict[str, str]


class ForecastOut(BaseModel):
    eac: Decimal
    etc: Decimal
    calculated_at: datetime | None = None


class ProjectFinance(BaseModel):
    """Сводка финансов проекта для страницы «здоровье проекта»."""

    project_id: uuid.UUID
    budget: BudgetOut | None
    margin: MarginOut
    forecast: ForecastOut
    planned_hours: Decimal
    actual_hours: Decimal
    hours_overrun_pct: Decimal
