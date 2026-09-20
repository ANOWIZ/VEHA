"""Чистый калькулятор стоимости лицензий и ТКП (без БД, без float).

Поток расчёта строки-лицензии (CLAUDE.md §4):
  цена прайса (валюта) → ×курс ×(1+буфер%) = база в рублях →
    закупка = база×(1−партнёрская скидка%)   (наша себестоимость)
    продажа = база×(1−скидка клиенту%)        (цена клиенту)
  маржа строки = продажа − закупка.

Курсы ЦБ не используются — курс задаётся вручную в версии расчёта
(``rates``: словарь {валюта: курс рублей за 1 ед.}). Для RUB курс = 1.

Работы/субподряд/поддержка — в рублях: продажа = qty×unit_price,
себестоимость = qty×unit_cost.
"""

from __future__ import annotations

from dataclasses import dataclass, fields
from decimal import ROUND_HALF_UP, Decimal

from app.models.enums import LicensingModel, QuoteLineKind

CENTS = Decimal("0.01")
HUNDRED = Decimal("100")


def _q(value: Decimal) -> Decimal:
    return value.quantize(CENTS, rounding=ROUND_HALF_UP)


@dataclass(slots=True)
class LineCalcInput:
    kind: QuoteLineKind
    qty: Decimal = Decimal("1")
    unit_price: Decimal = Decimal("0")
    unit_cost: Decimal | None = None
    currency: str = "RUB"
    licensing_model: LicensingModel | None = None
    term_months: int | None = None
    support_pct: Decimal | None = None
    partner_discount_pct: Decimal = Decimal("0")
    client_discount_pct: Decimal = Decimal("0")


@dataclass(slots=True)
class LineCalcResult:
    cost_amount: Decimal
    sell_amount: Decimal
    margin: Decimal


class RateNotFound(Exception):
    """Курс валюты отсутствует в снимке курсов расчёта."""

    def __init__(self, currency: str) -> None:
        super().__init__(f"Не задан курс для валюты {currency}")
        self.currency = currency


def resolve_rate(currency: str, rates: dict[str, Decimal | str | float]) -> Decimal:
    cur = (currency or "RUB").upper()
    if cur == "RUB":
        return Decimal("1")
    if cur not in rates:
        raise RateNotFound(cur)
    return Decimal(str(rates[cur]))


def _term_multiplier(line: LineCalcInput) -> Decimal:
    if line.licensing_model == LicensingModel.SUBSCRIPTION and line.term_months:
        return Decimal(line.term_months)
    return Decimal("1")


def compute_line(
    line: LineCalcInput,
    *,
    rates: dict[str, Decimal | str | float],
    buffer_pct: Decimal,
) -> LineCalcResult:
    if line.kind == QuoteLineKind.LICENSE:
        rate = resolve_rate(line.currency, rates)
        base_rub = line.unit_price * rate * (Decimal("1") + buffer_pct / HUNDRED)
        mult = _term_multiplier(line) * line.qty
        cost_unit = base_rub * (Decimal("1") - line.partner_discount_pct / HUNDRED)
        sell_unit = base_rub * (Decimal("1") - line.client_discount_pct / HUNDRED)
        cost = cost_unit * mult
        sell = sell_unit * mult
        # Перпетуал-лицензия с техподдержкой: добавляем годовую поддержку и в
        # себестоимость, и в продажу (поддержка перепродаётся без доп. маржи здесь).
        if line.licensing_model == LicensingModel.PERPETUAL and line.support_pct:
            support_cost = cost * line.support_pct / HUNDRED
            support_sell = sell * line.support_pct / HUNDRED
            cost += support_cost
            sell += support_sell
        return LineCalcResult(_q(cost), _q(sell), _q(sell - cost))

    # work / subcontract / support — в рублях.
    sell = line.unit_price * line.qty
    cost = (line.unit_cost or Decimal("0")) * line.qty
    return LineCalcResult(_q(cost), _q(sell), _q(sell - cost))


@dataclass(slots=True)
class QuoteTotals:
    licenses_sell: Decimal
    work_sell: Decimal
    subcontract_sell: Decimal
    support_sell: Decimal
    total_sell: Decimal
    total_cost: Decimal
    margin: Decimal
    margin_pct: Decimal

    def as_dict(self) -> dict[str, str]:
        return {f.name: str(getattr(self, f.name)) for f in fields(self)}


_KIND_TO_BUCKET = {
    QuoteLineKind.LICENSE: "licenses_sell",
    QuoteLineKind.WORK: "work_sell",
    QuoteLineKind.SUBCONTRACT: "subcontract_sell",
    QuoteLineKind.SUPPORT: "support_sell",
}


def compute_totals(
    lines: list[tuple[LineCalcInput, LineCalcResult]],
) -> QuoteTotals:
    buckets = {
        "licenses_sell": Decimal("0"),
        "work_sell": Decimal("0"),
        "subcontract_sell": Decimal("0"),
        "support_sell": Decimal("0"),
    }
    total_sell = Decimal("0")
    total_cost = Decimal("0")
    for line, res in lines:
        buckets[_KIND_TO_BUCKET[line.kind]] += res.sell_amount
        total_sell += res.sell_amount
        total_cost += res.cost_amount
    margin = total_sell - total_cost
    margin_pct = (margin / total_sell * HUNDRED) if total_sell else Decimal("0")
    return QuoteTotals(
        licenses_sell=_q(buckets["licenses_sell"]),
        work_sell=_q(buckets["work_sell"]),
        subcontract_sell=_q(buckets["subcontract_sell"]),
        support_sell=_q(buckets["support_sell"]),
        total_sell=_q(total_sell),
        total_cost=_q(total_cost),
        margin=_q(margin),
        margin_pct=_q(margin_pct),
    )
