"""Абстракция обмена с 1С через Protocol — конкретный транспорт (HTTP-сервис
1С либо файл-обмен XLSX) подменяется без изменения сервисов. Все обмены
идемпотентны по ``external_id`` и журналируются в ``integration_log``."""

from __future__ import annotations

from dataclasses import dataclass, field
from datetime import date
from decimal import Decimal
from typing import Protocol, runtime_checkable


@dataclass(slots=True)
class TimesheetExportRow:
    external_id: str
    project_code: str
    user_username: str
    work_date: date
    hours: Decimal
    cost_amount: Decimal


@dataclass(slots=True)
class ActualCostRow:
    external_id: str
    project_code: str
    category: str
    amount: Decimal
    occurred_on: date
    description: str = ""


@dataclass(slots=True)
class ExportResult:
    accepted: int = 0
    skipped: int = 0
    errors: list[str] = field(default_factory=list)


@runtime_checkable
class OneCGateway(Protocol):
    """Контракт обмена с 1С."""

    async def export_timesheets(self, rows: list[TimesheetExportRow]) -> ExportResult:
        """Выгрузить утверждённые трудозатраты за период в 1С."""
        ...

    async def fetch_actual_costs(
        self, period_from: date, period_to: date
    ) -> list[ActualCostRow]:
        """Загрузить фактические закупки/затраты по проектам из 1С."""
        ...
