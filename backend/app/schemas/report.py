"""Схемы отчётов: загрузка ресурсов (utilization)."""

from __future__ import annotations

import uuid
from datetime import date
from decimal import Decimal

from pydantic import BaseModel


class UtilizationRow(BaseModel):
    user_id: uuid.UUID
    full_name: str
    department: str | None
    billable_hours: Decimal
    capacity_hours: Decimal
    utilization_pct: Decimal
    projects_count: int


class UtilizationReport(BaseModel):
    date_from: date
    date_to: date
    working_days: int
    capacity_per_user: Decimal      # рабочих дней × 8 ч
    users_count: int
    total_billable_hours: Decimal
    total_capacity_hours: Decimal
    avg_utilization_pct: Decimal    # суммарная загрузка по всем сотрудникам
    rows: list[UtilizationRow]
