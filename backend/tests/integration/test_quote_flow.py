"""Интеграционные тесты калькулятора: версии, пересчёт, блокировка, клон."""

from __future__ import annotations

from decimal import Decimal

import pytest

from app.core.exceptions import AppError
from app.models.enums import LicensingModel, QuoteLineKind, QuoteStatus, Role
from app.schemas.quote import QuoteCreate, QuoteLineInput
from app.services.quote_service import QuoteService


async def _project(make_user, make_project):
    pm = await make_user([Role.PM], cost_rate=None)
    return pm, await make_project(pm)


async def test_create_quote_increments_version(session, make_user, make_project):
    pm, project = await _project(make_user, make_project)
    svc = QuoteService(session)
    q1 = await svc.create(project.id, QuoteCreate(title="ТКП"), actor_id=pm.id)
    q2 = await svc.create(project.id, QuoteCreate(title="ТКП"), actor_id=pm.id)
    assert q1.version == 1
    assert q2.version == 2


async def test_add_license_line_recomputes_totals(session, make_user, make_project):
    pm, project = await _project(make_user, make_project)
    svc = QuoteService(session)
    quote = await svc.create(
        project.id,
        QuoteCreate(currency_rates={"USD": "90"}, currency_buffer_pct=Decimal("2")),
        actor_id=pm.id,
    )
    quote = await svc.add_line(
        quote.id,
        QuoteLineInput(
            kind=QuoteLineKind.LICENSE,
            name="СУБД Lic",
            licensing_model=LicensingModel.PER_CORE,
            currency="USD",
            qty=Decimal("4"),
            unit_price=Decimal("100"),
            partner_discount_pct=Decimal("25"),
            client_discount_pct=Decimal("10"),
        ),
    )
    line = quote.lines[0]
    # база = 100×90×1.02 = 9180; cost=9180×0.75×4=27540; sell=9180×0.9×4=33048
    assert line.cost_amount == Decimal("27540.00")
    assert line.sell_amount == Decimal("33048.00")
    assert quote.totals["total_sell"] == "33048.00"
    assert quote.totals["licenses_sell"] == "33048.00"


async def test_work_line_and_combined_totals(session, make_user, make_project):
    pm, project = await _project(make_user, make_project)
    svc = QuoteService(session)
    quote = await svc.create(project.id, QuoteCreate(), actor_id=pm.id)
    await svc.add_line(
        quote.id,
        QuoteLineInput(
            kind=QuoteLineKind.WORK,
            name="Внедрение",
            qty=Decimal("100"),
            unit_price=Decimal("4000"),
            unit_cost=Decimal("1500"),
        ),
    )
    quote = await svc.get(quote.id)
    assert quote.totals["work_sell"] == "400000.00"
    assert quote.totals["total_cost"] == "150000.00"
    assert quote.totals["margin"] == "250000.00"


async def test_locked_quote_rejects_edits(session, make_user, make_project):
    pm, project = await _project(make_user, make_project)
    svc = QuoteService(session)
    quote = await svc.create(project.id, QuoteCreate(), actor_id=pm.id)
    await svc.set_status(quote.id, QuoteStatus.SENT, actor_id=pm.id)
    with pytest.raises(AppError):
        await svc.add_line(
            quote.id,
            QuoteLineInput(kind=QuoteLineKind.WORK, name="x", qty=Decimal("1"), unit_price=Decimal("1")),
        )


async def test_missing_rate_returns_error(session, make_user, make_project):
    pm, project = await _project(make_user, make_project)
    svc = QuoteService(session)
    quote = await svc.create(project.id, QuoteCreate(), actor_id=pm.id)  # нет курсов
    with pytest.raises(AppError):
        await svc.add_line(
            quote.id,
            QuoteLineInput(
                kind=QuoteLineKind.LICENSE,
                name="EUR lic",
                licensing_model=LicensingModel.PER_USER,
                currency="EUR",
                qty=Decimal("1"),
                unit_price=Decimal("100"),
            ),
        )


async def test_clone_creates_new_draft_with_lines(session, make_user, make_project):
    pm, project = await _project(make_user, make_project)
    svc = QuoteService(session)
    quote = await svc.create(project.id, QuoteCreate(), actor_id=pm.id)
    await svc.add_line(
        quote.id,
        QuoteLineInput(
            kind=QuoteLineKind.WORK, name="Работы", qty=Decimal("10"), unit_price=Decimal("1000")
        ),
    )
    await svc.set_status(quote.id, QuoteStatus.ACCEPTED, actor_id=pm.id)
    clone = await svc.clone_new_version(quote.id, actor_id=pm.id)
    assert clone.version == 2
    assert clone.status == QuoteStatus.DRAFT
    assert len(clone.lines) == 1
    assert clone.totals["work_sell"] == "10000.00"
