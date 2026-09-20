"""Интеграционные тесты ядра трудозатрат: ввод → отправка → утверждение,
снимок себестоимости на дату записи, сторно. Требуют Postgres."""

from __future__ import annotations

from datetime import date
from decimal import Decimal

import pytest

from app.core.exceptions import AppError, ForbiddenError, ValidationError
from app.models.enums import Role, TimeEntryStatus
from app.models.user import UserCostRate
from app.repositories.timesheet_repo import TimesheetRepository
from app.schemas.timesheet import TimeEntryCreate, TimeEntryUpdate
from app.services.timesheet_service import TimesheetService

WORK_DAY = date(2026, 3, 4)  # среда


async def test_full_flow_create_submit_approve_cost_snapshot(
    session, make_user, make_project, add_member
):
    pm = await make_user([Role.PM], cost_rate=None)
    eng = await make_user([Role.ENGINEER], cost_rate="1200.00")
    project = await make_project(pm)
    await add_member(project, eng)

    svc = TimesheetService(session)
    entry = await svc.create_entry(
        eng,
        TimeEntryCreate(
            project_id=project.id, work_date=WORK_DAY, hours=Decimal("8"), comment="Работа"
        ),
    )
    assert entry.status == TimeEntryStatus.DRAFT

    submitted = await svc.submit_week(eng, WORK_DAY)
    assert submitted == 1

    approved = await svc.approve(pm, [entry.id])
    assert approved == 1
    await session.refresh(entry)
    assert entry.status == TimeEntryStatus.APPROVED
    assert entry.approved_by == pm.id
    # Снимок ставки на дату записи = 1200, себестоимость 8*1200 = 9600
    assert entry.cost_rate_snapshot == Decimal("1200.00")

    cost = await TimesheetRepository(session).project_approved_cost(project.id)
    assert cost == Decimal("9600.00")


async def test_cost_rate_uses_rate_effective_on_work_date(
    session, make_user, make_project, add_member
):
    pm = await make_user([Role.PM], cost_rate=None)
    eng = await make_user([Role.ENGINEER], cost_rate="1000.00")
    # Новая ставка с 2026-01-01: 1500. Запись от 2025-12-31 должна взять старую 1000.
    session.add(
        UserCostRate(user_id=eng.id, cost_rate=Decimal("1500.00"), valid_from=date(2026, 1, 1))
    )
    await session.flush()
    project = await make_project(pm)
    await add_member(project, eng)

    svc = TimesheetService(session)
    old = await svc.create_entry(
        eng,
        TimeEntryCreate(
            project_id=project.id,
            work_date=date(2025, 12, 31),
            hours=Decimal("1"),
            comment="до повышения",
        ),
    )
    new = await svc.create_entry(
        eng,
        TimeEntryCreate(
            project_id=project.id,
            work_date=date(2026, 1, 2),
            hours=Decimal("1"),
            comment="после повышения",
        ),
    )
    # 2025-12-31 (ср) и 2026-01-02 (пт) — одна неделя (пн 2025-12-29): одна отправка.
    await svc.submit_week(eng, date(2025, 12, 31))
    await svc.approve(pm, [old.id, new.id])
    await session.refresh(old)
    await session.refresh(new)
    assert old.cost_rate_snapshot == Decimal("1000.00")
    assert new.cost_rate_snapshot == Decimal("1500.00")


async def test_daily_limit_enforced(session, make_user, make_project, add_member):
    pm = await make_user([Role.PM], cost_rate=None)
    eng = await make_user([Role.ENGINEER])
    project = await make_project(pm)
    await add_member(project, eng)
    svc = TimesheetService(session)
    await svc.create_entry(
        eng,
        TimeEntryCreate(
            project_id=project.id, work_date=WORK_DAY, hours=Decimal("20"), comment="много"
        ),
    )
    with pytest.raises(ValidationError):
        await svc.create_entry(
            eng,
            TimeEntryCreate(
                project_id=project.id, work_date=WORK_DAY, hours=Decimal("5"), comment="ещё"
            ),
        )


async def test_non_member_cannot_log(session, make_user, make_project):
    pm = await make_user([Role.PM], cost_rate=None)
    stranger = await make_user([Role.ENGINEER])
    project = await make_project(pm)
    svc = TimesheetService(session)
    with pytest.raises(ForbiddenError):
        await svc.create_entry(
            stranger,
            TimeEntryCreate(
                project_id=project.id, work_date=WORK_DAY, hours=Decimal("2"), comment="x"
            ),
        )


async def test_approved_entry_cannot_be_edited(
    session, make_user, make_project, add_member
):
    pm = await make_user([Role.PM], cost_rate=None)
    eng = await make_user([Role.ENGINEER])
    project = await make_project(pm)
    await add_member(project, eng)
    svc = TimesheetService(session)
    entry = await svc.create_entry(
        eng,
        TimeEntryCreate(
            project_id=project.id, work_date=WORK_DAY, hours=Decimal("4"), comment="x"
        ),
    )
    await svc.submit_week(eng, WORK_DAY)
    await svc.approve(pm, [entry.id])
    with pytest.raises(AppError):
        await svc.update_entry(eng, entry.id, TimeEntryUpdate(hours=Decimal("5")))


async def test_reversal_cancels_cost(session, make_user, make_project, add_member):
    pm = await make_user([Role.PM], cost_rate=None)
    eng = await make_user([Role.ENGINEER], cost_rate="1000.00")
    project = await make_project(pm)
    await add_member(project, eng)
    svc = TimesheetService(session)
    entry = await svc.create_entry(
        eng,
        TimeEntryCreate(
            project_id=project.id, work_date=WORK_DAY, hours=Decimal("8"), comment="x"
        ),
    )
    await svc.submit_week(eng, WORK_DAY)
    await svc.approve(pm, [entry.id])
    repo = TimesheetRepository(session)
    assert await repo.project_approved_cost(project.id) == Decimal("8000.00")
    # Сторно обнуляет себестоимость (8000 + (-8000))
    await svc.reverse(pm, entry.id)
    assert await repo.project_approved_cost(project.id) == Decimal("0.00")


async def test_only_project_manager_can_approve(
    session, make_user, make_project, add_member
):
    pm = await make_user([Role.PM], cost_rate=None)
    other_pm = await make_user([Role.PM], cost_rate=None)
    eng = await make_user([Role.ENGINEER])
    project = await make_project(pm)
    await add_member(project, eng)
    svc = TimesheetService(session)
    entry = await svc.create_entry(
        eng,
        TimeEntryCreate(
            project_id=project.id, work_date=WORK_DAY, hours=Decimal("4"), comment="x"
        ),
    )
    await svc.submit_week(eng, WORK_DAY)
    # Чужой РП не может утвердить.
    with pytest.raises(AppError):
        await svc.approve(other_pm, [entry.id])
