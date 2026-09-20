"""Сервис обмена с 1С: выгрузка утверждённых трудозатрат за период.

Идемпотентность по external_id (id записи): уже успешно выгруженные записи
пропускаются. Каждый обмен журналируется в integration_log."""

from __future__ import annotations

from datetime import date

from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.integrations.onec.base import TimesheetExportRow
from app.integrations.onec.http_client import build_onec_gateway
from app.models.audit import IntegrationLog
from app.models.enums import IntegrationDirection, TimeEntryStatus
from app.models.project import Project
from app.models.timesheet import TimeEntry
from app.models.user import User


class OneCService:
    SYSTEM = "onec"

    def __init__(self, session: AsyncSession) -> None:
        self.session = session

    async def _approved_in_period(
        self, period_from: date, period_to: date
    ) -> list[TimesheetExportRow]:
        stmt = (
            select(TimeEntry, Project.code, User.username)
            .join(Project, Project.id == TimeEntry.project_id)
            .join(User, User.id == TimeEntry.user_id)
            .where(
                TimeEntry.status == TimeEntryStatus.APPROVED,
                TimeEntry.work_date >= period_from,
                TimeEntry.work_date <= period_to,
                TimeEntry.deleted_at.is_(None),
            )
        )
        rows = []
        for entry, code, username in (await self.session.execute(stmt)).all():
            cost = entry.hours * (entry.cost_rate_snapshot or 0)
            rows.append(
                TimesheetExportRow(
                    external_id=str(entry.id),
                    project_code=code,
                    user_username=username,
                    work_date=entry.work_date,
                    hours=entry.hours,
                    cost_amount=cost,
                )
            )
        return rows

    async def _already_exported(self, external_ids: list[str]) -> set[str]:
        if not external_ids:
            return set()
        # Ограничиваем выборку кандидатами периода (IN), а не всем журналом.
        stmt = select(IntegrationLog.external_id).where(
            IntegrationLog.system == self.SYSTEM,
            IntegrationLog.direction == IntegrationDirection.OUTBOUND,
            IntegrationLog.status == "ok",
            IntegrationLog.external_id.in_(external_ids),
        )
        return {r for (r,) in (await self.session.execute(stmt)).all() if r}

    async def export_timesheets(self, period_from: date, period_to: date) -> dict:
        all_rows = await self._approved_in_period(period_from, period_to)
        done = await self._already_exported([r.external_id for r in all_rows])
        new_rows = [r for r in all_rows if r.external_id not in done]

        gateway = build_onec_gateway()
        result = await gateway.export_timesheets(new_rows)
        simulated = getattr(gateway, "simulated", False)

        # Помечаем "ok" только при полном успехе — иначе "error", чтобы строки
        # были повторно выгружены на следующем прогоне (а не считались успешными).
        succeeded = not result.errors and result.accepted >= len(new_rows)
        row_status = "ok" if succeeded else "error"
        error_text = "; ".join(result.errors) if result.errors else None

        for r in new_rows:
            self.session.add(
                IntegrationLog(
                    direction=IntegrationDirection.OUTBOUND,
                    system=self.SYSTEM,
                    external_id=r.external_id,
                    payload={
                        "project_code": r.project_code,
                        "user": r.user_username,
                        "date": r.work_date.isoformat(),
                        "hours": str(r.hours),
                        "cost": str(r.cost_amount),
                        "simulated": simulated,
                    },
                    status=row_status,
                    error=error_text,
                )
            )
        await self.session.flush()
        return {
            "period": [period_from.isoformat(), period_to.isoformat()],
            "total_approved": len(all_rows),
            "already_exported": len(all_rows) - len(new_rows),
            "exported_now": len(new_rows),
            "accepted": result.accepted,
            "errors": result.errors,
            "mode": "simulated" if simulated else "http",
        }
