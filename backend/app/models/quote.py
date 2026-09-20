"""Калькулятор лицензий: расчёт (спецификация) и его строки."""

from __future__ import annotations

import uuid
from decimal import Decimal

from sqlalchemy import ForeignKey, Integer, Numeric, String, Text, UniqueConstraint
from sqlalchemy.dialects.postgresql import JSONB
from sqlalchemy.dialects.postgresql import UUID as PG_UUID
from sqlalchemy.orm import Mapped, mapped_column, relationship

from app.db.base import DomainBase
from app.models.enums import LicensingModel, QuoteLineKind, QuoteStatus


class Quote(DomainBase):
    """Версионируется по проекту: старые версии read-only. Курсы валют
    зафиксированы на момент создания версии (snapshot)."""

    __tablename__ = "quotes"
    __table_args__ = (
        UniqueConstraint("project_id", "version", name="quote_project_version"),
    )

    project_id: Mapped[uuid.UUID] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("projects.id"), index=True
    )
    version: Mapped[int] = mapped_column(Integer, default=1, nullable=False)
    title: Mapped[str] = mapped_column(String(255), default="ТКП")
    status: Mapped[QuoteStatus] = mapped_column(String(16), default=QuoteStatus.DRAFT)

    # Снимок курсов ЦБ на момент версии: {"USD": {"rate": "...", "nominal": 1}, ...}
    currency_rates_snapshot: Mapped[dict] = mapped_column(JSONB, default=dict)
    currency_buffer_pct: Mapped[Decimal] = mapped_column(
        Numeric(5, 2), default=Decimal("0")
    )

    # Кэш итогов: {"licenses": ..., "work": ..., "support": ..., "subcontract": ...,
    #              "total_sell": ..., "total_cost": ..., "margin": ..., "margin_pct": ...}
    totals: Mapped[dict] = mapped_column(JSONB, default=dict)

    lines: Mapped[list[QuoteLine]] = relationship(
        back_populates="quote", cascade="all, delete-orphan", lazy="selectin"
    )


class QuoteLine(DomainBase):
    __tablename__ = "quote_lines"

    quote_id: Mapped[uuid.UUID] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("quotes.id", ondelete="CASCADE"), index=True
    )
    kind: Mapped[QuoteLineKind] = mapped_column(String(16), nullable=False)
    name: Mapped[str] = mapped_column(String(255))

    # Лицензии
    product_id: Mapped[uuid.UUID | None] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("products.id")
    )
    licensing_model: Mapped[LicensingModel | None] = mapped_column(String(32))
    metric: Mapped[str | None] = mapped_column(String(64))
    currency: Mapped[str] = mapped_column(String(3), default="RUB")

    qty: Mapped[Decimal] = mapped_column(Numeric(15, 2), default=Decimal("1"))
    # Цена прайса за единицу в валюте строки (для лицензий — РРЦ вендора).
    unit_price: Mapped[Decimal] = mapped_column(Numeric(15, 2), default=Decimal("0"))
    # Себестоимость за единицу для работ/субподряда (часы×cost_rate, счёт подрядчика).
    # Для лицензий не используется — закупочная цена считается из партнёрской скидки.
    unit_cost: Mapped[Decimal | None] = mapped_column(Numeric(15, 2))
    # subscription: число месяцев; perpetual: процент техподдержки в год
    term_months: Mapped[int | None] = mapped_column(Integer)
    support_pct: Mapped[Decimal | None] = mapped_column(Numeric(5, 2))

    partner_discount_pct: Mapped[Decimal] = mapped_column(
        Numeric(5, 2), default=Decimal("0")
    )
    client_discount_pct: Mapped[Decimal] = mapped_column(
        Numeric(5, 2), default=Decimal("0")
    )

    # Рассчитанные значения (в валюте проекта, RUB), сохраняются при пересчёте.
    cost_amount: Mapped[Decimal] = mapped_column(Numeric(15, 2), default=Decimal("0"))
    sell_amount: Mapped[Decimal] = mapped_column(Numeric(15, 2), default=Decimal("0"))
    margin: Mapped[Decimal] = mapped_column(Numeric(15, 2), default=Decimal("0"))

    note: Mapped[str | None] = mapped_column(Text)

    quote: Mapped[Quote] = relationship(back_populates="lines")
