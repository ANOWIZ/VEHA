"""Тесты калькулятора лицензий и ТКП (чистая логика, без БД).

Это ядро ценности — покрытие обязательно (CLAUDE.md §7)."""

from __future__ import annotations

from decimal import Decimal

import pytest

from app.models.enums import LicensingModel, QuoteLineKind
from app.services import quote_calc as qc
from app.services.quote_calc import LineCalcInput, RateNotFound


def L(**kw) -> LineCalcInput:
    return LineCalcInput(**kw)


def test_rub_license_no_discounts():
    line = L(
        kind=QuoteLineKind.LICENSE,
        qty=Decimal("10"),
        unit_price=Decimal("1000"),
        currency="RUB",
        licensing_model=LicensingModel.PER_USER,
    )
    r = qc.compute_line(line, rates={}, buffer_pct=Decimal("0"))
    assert r.cost_amount == Decimal("10000.00")
    assert r.sell_amount == Decimal("10000.00")
    assert r.margin == Decimal("0.00")


def test_license_partner_and_client_discount_margin():
    # база 1000, партнёрская 30% → закупка 700; клиенту 10% → продажа 900; маржа 200/ед
    line = L(
        kind=QuoteLineKind.LICENSE,
        qty=Decimal("5"),
        unit_price=Decimal("1000"),
        currency="RUB",
        licensing_model=LicensingModel.PER_USER,
        partner_discount_pct=Decimal("30"),
        client_discount_pct=Decimal("10"),
    )
    r = qc.compute_line(line, rates={}, buffer_pct=Decimal("0"))
    assert r.cost_amount == Decimal("3500.00")  # 700×5
    assert r.sell_amount == Decimal("4500.00")  # 900×5
    assert r.margin == Decimal("1000.00")


def test_currency_conversion_with_buffer():
    # 100 USD × курс 90 × (1+2%) = 9180 база; партнёрская 0, клиент 0
    line = L(
        kind=QuoteLineKind.LICENSE,
        qty=Decimal("1"),
        unit_price=Decimal("100"),
        currency="USD",
        licensing_model=LicensingModel.PER_CORE,
    )
    r = qc.compute_line(line, rates={"USD": "90"}, buffer_pct=Decimal("2"))
    assert r.cost_amount == Decimal("9180.00")
    assert r.sell_amount == Decimal("9180.00")


def test_missing_rate_raises():
    line = L(
        kind=QuoteLineKind.LICENSE,
        unit_price=Decimal("100"),
        currency="EUR",
        licensing_model=LicensingModel.PER_USER,
    )
    with pytest.raises(RateNotFound):
        qc.compute_line(line, rates={}, buffer_pct=Decimal("0"))


def test_subscription_multiplies_by_term():
    # 500/мес × 12 мес × 2 шт = 12000
    line = L(
        kind=QuoteLineKind.LICENSE,
        qty=Decimal("2"),
        unit_price=Decimal("500"),
        currency="RUB",
        licensing_model=LicensingModel.SUBSCRIPTION,
        term_months=12,
    )
    r = qc.compute_line(line, rates={}, buffer_pct=Decimal("0"))
    assert r.sell_amount == Decimal("12000.00")


def test_perpetual_with_support_pct_adds_support():
    # лицензия 1000, поддержка 20% → cost/sell += 20%
    line = L(
        kind=QuoteLineKind.LICENSE,
        qty=Decimal("1"),
        unit_price=Decimal("1000"),
        currency="RUB",
        licensing_model=LicensingModel.PERPETUAL,
        support_pct=Decimal("20"),
    )
    r = qc.compute_line(line, rates={}, buffer_pct=Decimal("0"))
    assert r.sell_amount == Decimal("1200.00")
    assert r.cost_amount == Decimal("1200.00")


def test_work_line_margin_from_cost():
    # 80 часов × 5000 (продажа) − 80 × 2000 (себестоимость) = 240000 маржа
    line = L(
        kind=QuoteLineKind.WORK,
        qty=Decimal("80"),
        unit_price=Decimal("5000"),
        unit_cost=Decimal("2000"),
    )
    r = qc.compute_line(line, rates={}, buffer_pct=Decimal("0"))
    assert r.sell_amount == Decimal("400000.00")
    assert r.cost_amount == Decimal("160000.00")
    assert r.margin == Decimal("240000.00")


def test_subcontract_without_cost_is_zero_cost():
    line = L(kind=QuoteLineKind.SUBCONTRACT, qty=Decimal("1"), unit_price=Decimal("50000"))
    r = qc.compute_line(line, rates={}, buffer_pct=Decimal("0"))
    assert r.cost_amount == Decimal("0.00")
    assert r.sell_amount == Decimal("50000.00")


def test_totals_breakdown_and_margin_pct():
    lic = L(
        kind=QuoteLineKind.LICENSE,
        qty=Decimal("1"),
        unit_price=Decimal("1000"),
        currency="RUB",
        licensing_model=LicensingModel.PER_USER,
        partner_discount_pct=Decimal("40"),
    )  # cost 600, sell 1000
    work = L(
        kind=QuoteLineKind.WORK,
        qty=Decimal("10"),
        unit_price=Decimal("100"),
        unit_cost=Decimal("40"),
    )  # cost 400, sell 1000
    rl = qc.compute_line(lic, rates={}, buffer_pct=Decimal("0"))
    rw = qc.compute_line(work, rates={}, buffer_pct=Decimal("0"))
    totals = qc.compute_totals([(lic, rl), (work, rw)])
    assert totals.licenses_sell == Decimal("1000.00")
    assert totals.work_sell == Decimal("1000.00")
    assert totals.total_sell == Decimal("2000.00")
    assert totals.total_cost == Decimal("1000.00")  # 600 + 400
    assert totals.margin == Decimal("1000.00")
    assert totals.margin_pct == Decimal("50.00")


def test_empty_totals_no_division_by_zero():
    totals = qc.compute_totals([])
    assert totals.total_sell == Decimal("0.00")
    assert totals.margin_pct == Decimal("0.00")


def test_rounding_half_up_to_kopecks():
    # 3 × 333.335 = 1000.005 → 1000.01 (для work sell)
    line = L(kind=QuoteLineKind.SUPPORT, qty=Decimal("3"), unit_price=Decimal("333.335"))
    r = qc.compute_line(line, rates={}, buffer_pct=Decimal("0"))
    assert r.sell_amount == Decimal("1000.01")
