"""Конкретные реализации обмена с 1С (через Protocol ``OneCGateway``).

OneCHttpGateway — обмен по HTTP-сервису 1С (JSON). SimulatedOneCGateway — для
локальной разработки без 1С: «принимает» строки, помечая режим симуляции.
"""

from __future__ import annotations

from datetime import date

import httpx

from app.core.config import settings
from app.integrations.onec.base import (
    ActualCostRow,
    ExportResult,
    OneCGateway,
    TimesheetExportRow,
)


class OneCHttpGateway:
    """Обмен с HTTP-сервисом 1С. Токен и базовый URL — из настроек."""

    simulated = False

    async def export_timesheets(self, rows: list[TimesheetExportRow]) -> ExportResult:
        if not rows:
            return ExportResult()
        payload = [
            {
                "external_id": r.external_id,
                "project_code": r.project_code,
                "user": r.user_username,
                "date": r.work_date.isoformat(),
                "hours": str(r.hours),
                "cost": str(r.cost_amount),
            }
            for r in rows
        ]
        async with httpx.AsyncClient(timeout=30.0) as client:
            resp = await client.post(
                f"{settings.onec_base_url}/timesheets",
                json={"rows": payload},
                headers={"Authorization": f"Bearer {settings.onec_token}"},
            )
            resp.raise_for_status()
        return ExportResult(accepted=len(rows))

    async def fetch_actual_costs(
        self, period_from: date, period_to: date
    ) -> list[ActualCostRow]:
        async with httpx.AsyncClient(timeout=30.0) as client:
            resp = await client.get(
                f"{settings.onec_base_url}/actual-costs",
                params={"from": period_from.isoformat(), "to": period_to.isoformat()},
                headers={"Authorization": f"Bearer {settings.onec_token}"},
            )
            resp.raise_for_status()
            data = resp.json()
        return [
            ActualCostRow(
                external_id=r["external_id"],
                project_code=r["project_code"],
                category=r["category"],
                amount=r["amount"],
                occurred_on=date.fromisoformat(r["date"]),
                description=r.get("description", ""),
            )
            for r in data.get("rows", [])
        ]


class SimulatedOneCGateway:
    """Локальная заглушка: принимает строки без реального 1С (помечает симуляцию)."""

    simulated = True

    async def export_timesheets(self, rows: list[TimesheetExportRow]) -> ExportResult:
        return ExportResult(accepted=len(rows))

    async def fetch_actual_costs(
        self, period_from: date, period_to: date
    ) -> list[ActualCostRow]:
        return []


def build_onec_gateway() -> OneCGateway:
    """Фабрика шлюза: HTTP при настроенном ONEC_BASE_URL, иначе симуляция."""
    if settings.onec_base_url:
        return OneCHttpGateway()
    return SimulatedOneCGateway()
