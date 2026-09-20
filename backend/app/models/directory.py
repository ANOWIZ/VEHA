"""Справочники: клиенты, вендоры, продукты, прайс-листы, курсы валют."""

from __future__ import annotations

import uuid
from datetime import date
from decimal import Decimal

from sqlalchemy import (
    Boolean,
    Date,
    ForeignKey,
    Index,
    Numeric,
    String,
)
from sqlalchemy.dialects.postgresql import JSONB
from sqlalchemy.dialects.postgresql import UUID as PG_UUID
from sqlalchemy.orm import Mapped, mapped_column, relationship

from app.db.base import DomainBase
from app.models.enums import LicensingModel


class Client(DomainBase):
    __tablename__ = "clients"

    name: Mapped[str] = mapped_column(String(255), index=True)
    inn: Mapped[str | None] = mapped_column(String(12), index=True)
    industry: Mapped[str | None] = mapped_column(String(150))
    contacts: Mapped[dict] = mapped_column(JSONB, default=dict)
    is_kii: Mapped[bool] = mapped_column(Boolean, default=False, nullable=False)


class Vendor(DomainBase):
    __tablename__ = "vendors"

    name: Mapped[str] = mapped_column(String(255), unique=True, index=True)
    country: Mapped[str | None] = mapped_column(String(100))

    products: Mapped[list[Product]] = relationship(back_populates="vendor")


class Product(DomainBase):
    __tablename__ = "products"
    # pg_trgm индекс по имени для быстрого поиска в каталоге создаётся в миграции.
    __table_args__ = (Index("ix_products_name_trgm", "name"),)

    vendor_id: Mapped[uuid.UUID] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("vendors.id"), index=True
    )
    name: Mapped[str] = mapped_column(String(255), index=True)
    edition: Mapped[str | None] = mapped_column(String(150))
    licensing_model: Mapped[LicensingModel] = mapped_column(String(32), nullable=False)
    price_currency: Mapped[str] = mapped_column(String(3), default="RUB")
    is_russian_registry: Mapped[bool] = mapped_column(Boolean, default=False)

    vendor: Mapped[Vendor] = relationship(back_populates="products")
    price_items: Mapped[list[PriceListItem]] = relationship(back_populates="product")


class PriceListItem(DomainBase):
    __tablename__ = "price_list_items"

    product_id: Mapped[uuid.UUID] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("products.id"), index=True
    )
    metric: Mapped[str] = mapped_column(String(64))  # «пользователь», «ядро», «сервер»...
    price: Mapped[Decimal] = mapped_column(Numeric(15, 2), nullable=False)
    currency: Mapped[str] = mapped_column(String(3), default="RUB")
    partner_discount_level: Mapped[str | None] = mapped_column(String(50))
    valid_from: Mapped[date] = mapped_column(Date, nullable=False)
    valid_to: Mapped[date | None] = mapped_column(Date, nullable=True)

    product: Mapped[Product] = relationship(back_populates="price_items")
