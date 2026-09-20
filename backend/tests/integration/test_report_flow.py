"""Интеграционные тесты отчётов: utilization (агрегация, фильтры периода/статуса)."""

from __future__ import annotations

from datetime import date
from decimal import Decimal

from app.models.enums import Role, TimeEntryStatus
from app.models.timesheet import TimeEntry
from app.models.user import User
from app.services.report_service import ReportService


async def _entry(session, user, project, work_date, hours, status):
    e = TimeEntry(
        user_id=user.id,
        project_id=project.id,
        work_date=work_date,
        hours=Decimal(str(hours)),
        comment="работа",
        status=status,
    )
    session.add(e)
    await session.flush()
    return e


async def test_utilization_counts_only_approved_in_period(session, make_user, make_project):
    pm = await make_user([Role.PM], cost_rate=None)
    project = await make_project(pm)
    eng = await make_user([Role.ENGINEER], full_name="Инженер У.")

    # В периоде, утверждённые: 8 + 12 = 20 ч.
    await _entry(session, eng, project, date(2024, 1, 2), 8, TimeEntryStatus.APPROVED)
    await _entry(session, eng, project, date(2024, 1, 3), 12, TimeEntryStatus.APPROVED)
    # Черновик — не считается.
    await _entry(session, eng, project, date(2024, 1, 4), 5, TimeEntryStatus.DRAFT)
    # Вне периода — не считается.
    await _entry(session, eng, project, date(2024, 1, 20), 10, TimeEntryStatus.APPROVED)

    report = await ReportService(session).utilization(date(2024, 1, 1), date(2024, 1, 5))
    assert report["working_days"] == 5
    assert report["capacity_per_user"] == Decimal("40.00")  # 5 × 8

    rows = {r["user_id"]: r for r in report["rows"]}
    assert rows[eng.id]["billable_hours"] == Decimal("20")
    assert rows[eng.id]["utilization_pct"] == Decimal("50.00")  # 20/40
    assert rows[eng.id]["projects_count"] == 1
    # РП без часов присутствует с нулевой загрузкой.
    assert rows[pm.id]["billable_hours"] == Decimal("0")
    assert report["total_billable_hours"] == Decimal("20.00")


async def test_utilization_excludes_client_users(session, make_user, make_project):
    pm = await make_user([Role.PM], cost_rate=None)
    await make_project(pm)
    client = User(
        username="c-rep",
        email="c-rep@client.test",
        full_name="Контакт",
        roles=[Role.CLIENT],
        is_active=True,
    )
    session.add(client)
    await session.flush()

    report = await ReportService(session).utilization(date(2024, 1, 1), date(2024, 1, 5))
    ids = {r["user_id"] for r in report["rows"]}
    assert client.id not in ids
    assert pm.id in ids


async def test_timesheet_rows_scope_and_cost(session, make_user, make_project):
    pm = await make_user([Role.PM], cost_rate=None)
    project = await make_project(pm)
    eng = await make_user([Role.ENGINEER])
    e = await _entry(session, eng, project, date(2024, 1, 2), 8, TimeEntryStatus.APPROVED)
    e.cost_rate_snapshot = Decimal("1500")
    await session.flush()

    rows = await ReportService(session).timesheet_rows(
        pm,
        date_from=date(2024, 1, 1),
        date_to=date(2024, 1, 31),
        include_cost=True,
    )
    assert len(rows) == 1
    assert rows[0]["hours"] == Decimal("8")
    assert rows[0]["cost"] == Decimal("12000.00")  # 8 × 1500
    assert rows[0]["project_code"] == project.code
