"""Схемы дашбордов."""

from __future__ import annotations

import uuid
from decimal import Decimal

from pydantic import BaseModel

from app.models.enums import ProjectStatus, Stage


class PortfolioRow(BaseModel):
    project_id: uuid.UUID
    code: str
    name: str
    stage: Stage
    status: ProjectStatus
    manager_id: uuid.UUID
    revenue: Decimal
    margin: Decimal
    margin_pct: Decimal
    planned_hours: Decimal
    actual_hours: Decimal
    hours_overrun_pct: Decimal
    risks: list[str]


class PortfolioResponse(BaseModel):
    projects_count: int
    total_revenue: Decimal
    total_margin: Decimal
    avg_margin_pct: Decimal
    at_risk: int
    rows: list[PortfolioRow]
