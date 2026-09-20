"""Интеграционные тесты финансов и ресурсов: маржа из утверждённых таймшитов +
фактических затрат, тепловая карта загрузки. Требуют Postgres."""

from __future__ import annotations

from datetime import date
from decimal import Decimal

from app.models.enums import CostCategory, Role
from app.schemas.finance import ActualCostCreate, BudgetUpsert
from app.schemas.resource import ResourcePlanUpsert
from app.schemas.timesheet import TimeEntryCreate
from app.services.finance_service import FinanceService
from app.services.resource_service import ResourceService
from app.services.timesheet_service import TimesheetService


async def test_margin_combines_payroll_and_actuals(
    session, make_user, make_project, add_member
):
    pm = await make_user([Role.PM], cost_rate=None)
    eng = await make_user([Role.ENGINEER], cost_rate="1000.00")
    project = await make_project(pm)
    await add_member(project, eng)

    fin = FinanceService(session)
    await fin.upsert_budget(
        project.id,
        BudgetUpsert(planned_revenue=Decimal("100000"), planned_costs={}),
        actor_id=pm.id,
    )

    ts = TimesheetService(session)
    entry = await ts.create_entry(
        eng,
        TimeEntryCreate(
            project_id=project.id, work_date=date(2026, 3, 4), hours=Decimal("10"), comment="x"
        ),
    )
    await ts.submit_week(eng, date(2026, 3, 4))
    await ts.approve(pm, [entry.id])  # ФОТ = 10 × 1000 = 10000

    await fin.add_actual(
        project.id,
        ActualCostCreate(
            category=CostCategory.LICENSES, amount=Decimal("20000"), occurred_on=date(2026, 3, 1)
        ),
        actor_id=pm.id,
    )

    result, _breakdown = await fin.compute_margin(project.id)
    assert result.revenue == Decimal("100000.00")
    assert result.total_cost == Decimal("30000.00")  # 10000 ФОТ + 20000 лицензии
    assert result.margin == Decimal("70000.00")
    assert result.margin_pct == Decimal("70.00")


async def test_project_finance_hours_overrun(session, make_user, make_project, add_member):
    pm = await make_user([Role.PM], cost_rate=None)
    eng = await make_user([Role.ENGINEER], cost_rate="1000.00")
    project = await make_project(pm)
    await add_member(project, eng)

    # План часов задаём задачей; факт — утверждённый таймшит.
    from app.schemas.task import TaskCreate
    from app.services.task_service import TaskService

    await TaskService(session).create(
        project.id, TaskCreate(name="T", planned_hours=Decimal("8"))
    )
    ts = TimesheetService(session)
    e = await ts.create_entry(
        eng,
        TimeEntryCreate(
            project_id=project.id, work_date=date(2026, 3, 4), hours=Decimal("10"), comment="x"
        ),
    )
    await ts.submit_week(eng, date(2026, 3, 4))
    await ts.approve(pm, [e.id])

    data = await FinanceService(session).project_finance(project.id)
    assert data["planned_hours"] == Decimal("8.00")
    assert data["actual_hours"] == Decimal("10.00")
    # перерасход (10-8)/8*100 = 25%
    assert data["hours_overrun_pct"] == Decimal("25.00")


async def test_resource_heatmap_load_flags(session, make_user, make_project):
    pm = await make_user([Role.PM], cost_rate=None)
    project = await make_project(pm)
    eng = await make_user([Role.ENGINEER], cost_rate=None, full_name="Загруженный Инженер")

    rs = ResourceService(session)
    # 44 ч на неделе при норме 40 → перегрузка.
    await rs.upsert_plan(
        ResourcePlanUpsert(
            user_id=eng.id, project_id=project.id, week_start=date(2026, 3, 2),
            planned_hours=Decimal("44"),
        )
    )
    data = await rs.heatmap(date(2026, 3, 2), 2)
    assert data["norm_hours"] == Decimal("40")
    row = next(r for r in data["rows"] if r["user_id"] == eng.id)
    assert row["cells"][0]["planned_hours"] == Decimal("44")
    assert row["cells"][0]["utilization_pct"] == Decimal("110.00")
    assert row["cells"][0]["load"] == "over"
    # Вторая неделя пустая → недозагрузка.
    assert row["cells"][1]["load"] == "under"


async def test_resource_plan_upsert_overwrites(session, make_user, make_project):
    pm = await make_user([Role.PM], cost_rate=None)
    project = await make_project(pm)
    eng = await make_user([Role.ENGINEER], cost_rate=None)
    rs = ResourceService(session)
    await rs.upsert_plan(
        ResourcePlanUpsert(
            user_id=eng.id, project_id=project.id, week_start=date(2026, 3, 2),
            planned_hours=Decimal("20"),
        )
    )
    await rs.upsert_plan(
        ResourcePlanUpsert(
            user_id=eng.id, project_id=project.id, week_start=date(2026, 3, 2),
            planned_hours=Decimal("30"),
        )
    )
    data = await rs.heatmap(date(2026, 3, 2), 1)
    row = next(r for r in data["rows"] if r["user_id"] == eng.id)
    assert row["cells"][0]["planned_hours"] == Decimal("30")  # перезапись, не сумма
