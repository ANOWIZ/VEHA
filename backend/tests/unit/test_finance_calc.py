"""Тесты финансовых расчётов (чистая логика). Ядро ценности — покрытие обязательно."""

from __future__ import annotations

from decimal import Decimal

from app.services import finance_calc as fc


def test_margin_basic():
    r = fc.compute_margin(Decimal("1000000"), Decimal("700000"))
    assert r.margin == Decimal("300000.00")
    assert r.margin_pct == Decimal("30.00")


def test_margin_zero_revenue_no_division_error():
    r = fc.compute_margin(Decimal("0"), Decimal("5000"))
    assert r.margin == Decimal("-5000.00")
    assert r.margin_pct == Decimal("0.00")


def test_margin_negative_when_over_budget():
    r = fc.compute_margin(Decimal("100000"), Decimal("130000"))
    assert r.margin == Decimal("-30000.00")
    assert r.margin_pct == Decimal("-30.00")


def test_total_cost_sums_categories():
    assert fc.total_cost(
        {"payroll": Decimal("100"), "licenses": Decimal("250.50"), "travel": Decimal("0")}
    ) == Decimal("350.50")


def test_forecast_eac_etc():
    f = fc.compute_forecast(Decimal("400000"), Decimal("150000"))
    assert f.etc == Decimal("150000.00")
    assert f.eac == Decimal("550000.00")


def test_forecast_no_remaining():
    f = fc.compute_forecast(Decimal("400000"), Decimal("-10"))
    assert f.etc == Decimal("0.00")
    assert f.eac == Decimal("400000.00")


def test_hours_overrun():
    assert fc.hours_overrun_pct(Decimal("100"), Decimal("115")) == Decimal("15.00")
    assert fc.hours_overrun_pct(Decimal("0"), Decimal("10")) == Decimal("0")


def test_utilization():
    assert fc.utilization_pct(Decimal("44"), Decimal("40")) == Decimal("110.00")
    assert fc.utilization_pct(Decimal("28"), Decimal("40")) == Decimal("70.00")
