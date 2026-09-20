"""Схемы ресурсного планирования и тепловой карты загрузки."""

from __future__ import annotations

import uuid
from datetime import date
from decimal import Decimal

from pydantic import BaseModel, Field

from app.schemas.common import ORMModel


class ResourcePlanUpsert(BaseModel):
    user_id: uuid.UUID
    project_id: uuid.UUID
    week_start: date
    planned_hours: Decimal = Field(ge=0, le=168)


class ResourcePlanOut(ORMModel):
    id: uuid.UUID
    user_id: uuid.UUID
    project_id: uuid.UUID
    week_start: date
    planned_hours: Decimal


class HeatmapCell(BaseModel):
    week_start: date
    planned_hours: Decimal
    utilization_pct: Decimal
    # over: >100% нормы; under: <70%; ok иначе
    load: str


class HeatmapRow(BaseModel):
    user_id: uuid.UUID
    user_name: str
    cells: list[HeatmapCell]
    total_hours: Decimal


class HeatmapResponse(BaseModel):
    weeks: list[date]
    norm_hours: Decimal
    rows: list[HeatmapRow]
