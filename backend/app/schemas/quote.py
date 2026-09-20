"""Схемы расчётов (Quote) и строк спецификации."""

from __future__ import annotations

import uuid
from datetime import datetime
from decimal import Decimal

from pydantic import BaseModel, Field, model_validator

from app.models.enums import LicensingModel, QuoteLineKind, QuoteStatus
from app.schemas.common import ORMModel


class QuoteCreate(BaseModel):
    title: str = Field(default="ТКП", max_length=255)
    # Снимок курсов на версию: {"USD": "90.5", "EUR": "98.2"} (рублей за 1 ед.)
    currency_rates: dict[str, str] = Field(default_factory=dict)
    currency_buffer_pct: Decimal = Field(default=Decimal("0"), ge=0, le=100)


class QuoteLineInput(BaseModel):
    kind: QuoteLineKind
    name: str = Field(min_length=1, max_length=255)
    product_id: uuid.UUID | None = None
    licensing_model: LicensingModel | None = None
    metric: str | None = None
    currency: str = "RUB"
    qty: Decimal = Field(default=Decimal("1"), gt=0)
    unit_price: Decimal = Field(default=Decimal("0"), ge=0)
    unit_cost: Decimal | None = Field(default=None, ge=0)
    term_months: int | None = Field(default=None, ge=1)
    support_pct: Decimal | None = Field(default=None, ge=0, le=100)
    partner_discount_pct: Decimal = Field(default=Decimal("0"), ge=0, le=100)
    client_discount_pct: Decimal = Field(default=Decimal("0"), ge=0, le=100)
    note: str | None = None

    @model_validator(mode="after")
    def _check_consistency(self) -> QuoteLineInput:
        # Подписочная лицензия без срока считалась бы как 1 месяц (молчаливо
        # заниженный итог в quote_calc._term_multiplier) — требуем term_months.
        if (
            self.kind == QuoteLineKind.LICENSE
            and self.licensing_model == LicensingModel.SUBSCRIPTION
            and self.term_months is None
        ):
            raise ValueError(
                "Для подписочной лицензии (subscription) укажите срок term_months (мес.)"
            )
        return self


class QuoteLineOut(ORMModel):
    id: uuid.UUID
    kind: QuoteLineKind
    name: str
    product_id: uuid.UUID | None
    licensing_model: LicensingModel | None
    metric: str | None
    currency: str
    qty: Decimal
    unit_price: Decimal
    unit_cost: Decimal | None
    term_months: int | None
    support_pct: Decimal | None
    partner_discount_pct: Decimal
    client_discount_pct: Decimal
    cost_amount: Decimal
    sell_amount: Decimal
    margin: Decimal
    note: str | None


class QuoteOut(ORMModel):
    id: uuid.UUID
    project_id: uuid.UUID
    version: int
    title: str
    status: QuoteStatus
    currency_rates_snapshot: dict
    currency_buffer_pct: Decimal
    totals: dict
    created_at: datetime


class QuoteDetail(QuoteOut):
    lines: list[QuoteLineOut] = Field(default_factory=list)


class QuoteStatusUpdate(BaseModel):
    status: QuoteStatus
