"""API отчётов: загрузка ресурсов (utilization) и выгрузки XLSX (таймшиты, портфель).

Доступ — управленческим/финансовым ролям. Себестоимость в выгрузке таймшитов
включается только для ролей с доступом к финансам."""

from __future__ import annotations

import uuid
from datetime import date
from functools import partial
from typing import Annotated

import anyio
from fastapi import APIRouter, Depends, Response
from sqlalchemy import select

from app.core.deps import DbSession, require_role
from app.core.exceptions import ValidationError
from app.models.enums import Role, TimeEntryStatus
from app.models.user import User
from app.schemas.report import UtilizationReport
from app.services.dashboard_service import DashboardService
from app.services.report_export import (
    build_portfolio_xlsx_bytes,
    build_timesheet_xlsx_bytes,
    build_utilization_xlsx_bytes,
)
from app.services.report_service import ReportService

router = APIRouter(prefix="/reports", tags=["reports"])

_XLSX = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
_MAX_RANGE_DAYS = 366


def _assert_range(date_from: date, date_to: date) -> None:
    """Валидация периода отчёта: конец не раньше начала и окно ограничено
    (защита от вырожденных/огромных выгрузок)."""
    if date_to < date_from:
        raise ValidationError("Дата окончания периода не может быть раньше даты начала")
    if (date_to - date_from).days > _MAX_RANGE_DAYS:
        raise ValidationError(f"Период отчёта не должен превышать {_MAX_RANGE_DAYS} дней")

# Таймшиты/портфель скоупятся по доступным проектам — их можно открыть РП.
_report_roles = require_role(Role.PM, Role.DIRECTOR, Role.ADMIN, Role.FINANCE)
# Utilization — компанийный отчёт по всем сотрудникам (НЕ скоупится по проектам),
# поэтому доступен только руководству/финансам, без РП (см. CLAUDE.md §5).
_mgmt_roles = require_role(Role.DIRECTOR, Role.ADMIN, Role.FINANCE)


def _xlsx_response(data: bytes, filename: str) -> Response:
    return Response(
        content=data,
        media_type=_XLSX,
        headers={"Content-Disposition": f'attachment; filename="{filename}"'},
    )


@router.get("/utilization", response_model=UtilizationReport)
async def utilization(
    date_from: date,
    date_to: date,
    session: DbSession,
    user: Annotated[User, Depends(_mgmt_roles)],
) -> UtilizationReport:
    _assert_range(date_from, date_to)
    data = await ReportService(session).utilization(date_from, date_to)
    return UtilizationReport.model_validate(data)


@router.get("/utilization/export.xlsx")
async def utilization_xlsx(
    date_from: date,
    date_to: date,
    session: DbSession,
    user: Annotated[User, Depends(_mgmt_roles)],
) -> Response:
    _assert_range(date_from, date_to)
    data = await ReportService(session).utilization(date_from, date_to)
    # Генерация книги openpyxl — CPU-bound и синхронная: выносим в поток, чтобы
    # не блокировать event loop (CLAUDE.md §7).
    payload = await anyio.to_thread.run_sync(build_utilization_xlsx_bytes, data)
    return _xlsx_response(payload, f"utilization_{date_from}_{date_to}.xlsx")


@router.get("/timesheets/export.xlsx")
async def timesheets_xlsx(
    date_from: date,
    date_to: date,
    session: DbSession,
    user: Annotated[User, Depends(_report_roles)],
    project_id: uuid.UUID | None = None,
    status: TimeEntryStatus | None = None,
) -> Response:
    _assert_range(date_from, date_to)
    svc = ReportService(session)
    include_cost = ReportService.can_see_financials(user)
    rows = await svc.timesheet_rows(
        user,
        date_from=date_from,
        date_to=date_to,
        project_id=project_id,
        status=status,
        include_cost=include_cost,
    )
    payload = await anyio.to_thread.run_sync(
        partial(
            build_timesheet_xlsx_bytes,
            rows,
            include_cost=include_cost,
            date_from=date_from,
            date_to=date_to,
        )
    )
    return _xlsx_response(payload, f"timesheets_{date_from}_{date_to}.xlsx")


@router.get("/portfolio/export.xlsx")
async def portfolio_xlsx(
    session: DbSession, user: Annotated[User, Depends(_report_roles)]
) -> Response:
    portfolio = await DashboardService(session).portfolio(user)
    names = dict((await session.execute(select(User.id, User.full_name))).all())
    payload = await anyio.to_thread.run_sync(
        partial(build_portfolio_xlsx_bytes, portfolio, names)
    )
    return _xlsx_response(payload, "portfolio.xlsx")
