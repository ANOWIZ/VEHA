"""Регресс-тесты для находок аудита (безопасность/корректность).

Покрывают: IDOR ТКП (привязка к проекту), терминальность принятого Quote,
идемпотентность actual costs и принудительный source, блокировку бюджета
закрытого проекта, схемные валидаторы (budget/quote-line/cost-rate)."""

from __future__ import annotations

from datetime import date
from decimal import Decimal

import pytest
from pydantic import ValidationError as PydanticValidationError

from app.core.exceptions import AppError, ConflictError, NotFoundError
from app.models.enums import (
    CostCategory,
    CostSource,
    LicensingModel,
    ProjectStatus,
    QuoteLineKind,
    QuoteStatus,
    Role,
)
from app.schemas.finance import ActualCostCreate, BudgetUpsert
from app.schemas.quote import QuoteCreate, QuoteLineInput
from app.schemas.user import CostRateCreate
from app.services.finance_service import FinanceService
from app.services.quote_service import QuoteService


# ---------- IDOR: ТКП привязан к проекту ----------
async def test_quote_get_rejects_other_project(session, make_user, make_project):
    pm = await make_user([Role.PM], cost_rate=None)
    project_a = await make_project(pm, name="A")
    project_b = await make_project(pm, name="B")
    svc = QuoteService(session)
    quote = await svc.create(project_a.id, QuoteCreate(), actor_id=pm.id)

    # В рамках своего проекта — доступен.
    assert (await svc.get(quote.id, project_a.id)).id == quote.id
    # Через чужой проект (IDOR) — не найден.
    with pytest.raises(NotFoundError):
        await svc.get(quote.id, project_b.id)


# ---------- Терминальность принятого ТКП ----------
async def test_accepted_quote_cannot_revert_to_draft(session, make_user, make_project):
    pm = await make_user([Role.PM], cost_rate=None)
    project = await make_project(pm)
    svc = QuoteService(session)
    quote = await svc.create(project.id, QuoteCreate(), actor_id=pm.id)
    await svc.set_status(quote.id, QuoteStatus.ACCEPTED, actor_id=pm.id)
    with pytest.raises(AppError):
        await svc.set_status(quote.id, QuoteStatus.DRAFT, actor_id=pm.id)


# ---------- Идемпотентность фактических затрат ----------
async def test_add_actual_idempotent_by_external_id(session, make_user, make_project):
    pm = await make_user([Role.PM], cost_rate=None)
    project = await make_project(pm)
    fin = FinanceService(session)
    await fin.upsert_budget(
        project.id, BudgetUpsert(planned_revenue=Decimal("100000")), actor_id=pm.id
    )
    payload = ActualCostCreate(
        category=CostCategory.LICENSES,
        amount=Decimal("5000"),
        occurred_on=date(2026, 3, 1),
        external_id="1C-INV-001",
    )
    await fin.add_actual(project.id, payload, actor_id=pm.id)
    with pytest.raises(ConflictError):
        await fin.add_actual(project.id, payload, actor_id=pm.id)


async def test_add_actual_forces_manual_source(session, make_user, make_project):
    pm = await make_user([Role.PM], cost_rate=None)
    project = await make_project(pm)
    fin = FinanceService(session)
    await fin.upsert_budget(
        project.id, BudgetUpsert(planned_revenue=Decimal("100000")), actor_id=pm.id
    )
    # Клиент пытается выдать ручную затрату за TIMESHEET — должна сохраниться как MANUAL.
    actual = await fin.add_actual(
        project.id,
        ActualCostCreate(
            category=CostCategory.TRAVEL,
            amount=Decimal("1000"),
            occurred_on=date(2026, 3, 1),
            source=CostSource.TIMESHEET,
        ),
        actor_id=pm.id,
    )
    assert actual.source == CostSource.MANUAL


async def test_budget_blocked_on_closed_project(session, make_user, make_project):
    pm = await make_user([Role.PM], cost_rate=None)
    project = await make_project(pm)
    project.status = ProjectStatus.CLOSED
    await session.flush()
    fin = FinanceService(session)
    with pytest.raises(AppError):
        await fin.upsert_budget(
            project.id, BudgetUpsert(planned_revenue=Decimal("1")), actor_id=pm.id
        )


# ---------- Схемные валидаторы ----------
def test_budget_rejects_unknown_category():
    with pytest.raises(PydanticValidationError):
        BudgetUpsert(planned_costs={"bogus": "100"})


def test_budget_rejects_non_numeric_cost():
    with pytest.raises(PydanticValidationError):
        BudgetUpsert(planned_costs={"licenses": "abc"})


def test_budget_rejects_negative_cost():
    with pytest.raises(PydanticValidationError):
        BudgetUpsert(planned_costs={"licenses": "-5"})


def test_quote_line_subscription_requires_term():
    with pytest.raises(PydanticValidationError):
        QuoteLineInput(
            kind=QuoteLineKind.LICENSE,
            name="Подписка без срока",
            licensing_model=LicensingModel.SUBSCRIPTION,
            qty=Decimal("1"),
            unit_price=Decimal("100"),
        )


def test_cost_rate_rejects_inverted_dates():
    with pytest.raises(PydanticValidationError):
        CostRateCreate(
            cost_rate=Decimal("1000"),
            valid_from=date(2026, 1, 10),
            valid_to=date(2026, 1, 1),
        )
