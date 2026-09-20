"""Тесты правил трудозатрат (чистая логика, без БД)."""

from __future__ import annotations

from decimal import Decimal

import pytest

from app.services import timesheet_rules as r


@pytest.mark.parametrize("hours", ["0.25", "0.5", "1", "7.5", "8", "24"])
def test_valid_hours(hours):
    r.validate_hours(Decimal(hours))


@pytest.mark.parametrize("hours", ["0", "-1", "24.25", "0.1", "0.3", "1.2"])
def test_invalid_hours(hours):
    with pytest.raises(ValueError):
        r.validate_hours(Decimal(hours))


def test_daily_limit_ok():
    r.validate_daily_total(Decimal("8"), Decimal("4"))
    r.validate_daily_total(Decimal("0"), Decimal("24"))


def test_daily_limit_exceeded():
    with pytest.raises(ValueError, match="лимит"):
        r.validate_daily_total(Decimal("20"), Decimal("5"))


def test_compute_cost_rounds_to_kopecks():
    # 7.5 ч × 1234.567 = 9259.2525 → 9259.25 (half-up)
    assert r.compute_cost(Decimal("7.5"), Decimal("1234.567")) == Decimal("9259.25")
    # half-up на границе
    assert r.compute_cost(Decimal("1"), Decimal("0.125")) == Decimal("0.13")


def test_compute_cost_zero_rate():
    assert r.compute_cost(Decimal("8"), Decimal("0")) == Decimal("0.00")
