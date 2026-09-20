"""Чистые правила трудозатрат (без БД): шаг часов, дневной лимит, себестоимость.

Часы — шаг 0.25, суммарно не более 24 в день. Себестоимость = часы × ставка,
действующая на дату записи. Деньги/часы — Decimal (без float)."""

from __future__ import annotations

from decimal import ROUND_HALF_UP, Decimal

HOUR_STEP = Decimal("0.25")
MAX_DAILY_HOURS = Decimal("24")
_CENTS = Decimal("0.01")


def validate_hours(hours: Decimal) -> None:
    if hours <= 0:
        raise ValueError("Количество часов должно быть положительным")
    if hours > MAX_DAILY_HOURS:
        raise ValueError("За один день нельзя списать более 24 часов")
    # Кратность шагу 0.25.
    if (hours / HOUR_STEP) % 1 != 0:
        raise ValueError("Часы указываются с шагом 0,25")


def validate_daily_total(existing_hours: Decimal, new_hours: Decimal) -> None:
    if existing_hours + new_hours > MAX_DAILY_HOURS:
        raise ValueError(
            f"Превышен суточный лимит: уже {existing_hours} ч + {new_hours} ч > 24 ч"
        )


def compute_cost(hours: Decimal, cost_rate: Decimal) -> Decimal:
    """Себестоимость записи, округление до копеек (half-up)."""
    return (hours * cost_rate).quantize(_CENTS, rounding=ROUND_HALF_UP)
