"""Схемы каталога: вендоры, продукты, прайс-листы."""

from __future__ import annotations

import uuid
from datetime import date
from decimal import Decimal

from pydantic import BaseModel, Field

from app.models.enums import LicensingModel
from app.schemas.common import ORMModel


# --- Вендор ---
class VendorCreate(BaseModel):
    name: str = Field(min_length=1, max_length=255)
    country: str | None = None


class VendorOut(ORMModel):
    id: uuid.UUID
    name: str
    country: str | None


# --- Продукт ---
class ProductCreate(BaseModel):
    vendor_id: uuid.UUID
    name: str = Field(min_length=1, max_length=255)
    edition: str | None = None
    licensing_model: LicensingModel
    price_currency: str = "RUB"
    is_russian_registry: bool = False


class ProductUpdate(BaseModel):
    name: str | None = Field(default=None, max_length=255)
    edition: str | None = None
    licensing_model: LicensingModel | None = None
    price_currency: str | None = None
    is_russian_registry: bool | None = None


class ProductOut(ORMModel):
    id: uuid.UUID
    vendor_id: uuid.UUID
    name: str
    edition: str | None
    licensing_model: LicensingModel
    price_currency: str
    is_russian_registry: bool


# --- Прайс ---
class PriceItemCreate(BaseModel):
    metric: str = Field(min_length=1, max_length=64)
    price: Decimal = Field(ge=0)
    currency: str = "RUB"
    partner_discount_level: str | None = None
    valid_from: date
    valid_to: date | None = None


class PriceItemOut(ORMModel):
    id: uuid.UUID
    product_id: uuid.UUID
    metric: str
    price: Decimal
    currency: str
    partner_discount_level: str | None
    valid_from: date
    valid_to: date | None


class ProductDetail(ProductOut):
    vendor: VendorOut | None = None
    price_items: list[PriceItemOut] = Field(default_factory=list)
