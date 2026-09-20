"""Чистые финансовые расчёты (без БД): маржинальность, отклонения, прогноз.

Деньги/часы — Decimal, без float. Маржинальность = (выручка − затраты)/выручка."""

from __future__ import annotations

from dataclasses import dataclass, fields
from decimal import ROUND_HALF_UP, Decimal

CENTS = Decimal("0.01")
HUNDRED = Decimal("100")


def _q(value: Decimal) -> Decimal:
    return value.quantize(CENTS, rounding=ROUND_HALF_UP)


@dataclass(slots=True)
class MarginResult:
    revenue: Decimal
    total_cost: Decimal
    margin: Decimal
    margin_pct: Decimal

    def as_dict(self) -> dict[str, str]:
        return {f.name: str(getattr(self, f.name)) for f in fields(self)}


def compute_margin(revenue: Decimal, total_cost: Decimal) -> MarginResult:
    margin = revenue - total_cost
    margin_pct = (margin / revenue * HUNDRED) if revenue else Decimal("0")
    return MarginResult(_q(revenue), _q(total_cost), _q(margin), _q(margin_pct))


def total_cost(cost_by_category: dict[str, Decimal]) -> Decimal:
    return _q(sum(cost_by_category.values(), Decimal("0")))


@dataclass(slots=True)
class Forecast:
    """EAC/ETC по простой модели: ETC = плановые остаточные затраты,
    EAC = понесённые фактические + ETC."""

    etc: Decimal
    eac: Decimal


def compute_forecast(
    actual_cost_to_date: Decimal, remaining_planned_cost: Decimal
) -> Forecast:
    etc = remaining_planned_cost if remaining_planned_cost > 0 else Decimal("0")
    return Forecast(etc=_q(etc), eac=_q(actual_cost_to_date + etc))


def hours_overrun_pct(planned_hours: Decimal, actual_hours: Decimal) -> Decimal:
    """Перерасход часов в %: (факт − план)/план×100. План 0 → 0."""
    if planned_hours <= 0:
        return Decimal("0")
    return _q((actual_hours - planned_hours) / planned_hours * HUNDRED)


def utilization_pct(planned_hours: Decimal, norm_hours: Decimal) -> Decimal:
    """Загрузка ресурса в % от нормы недели."""
    if norm_hours <= 0:
        return Decimal("0")
    return _q(planned_hours / norm_hours * HUNDRED)
