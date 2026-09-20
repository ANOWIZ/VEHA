"""Сервис каталога: вендоры, продукты, прайс-листы."""

from __future__ import annotations

import uuid

from sqlalchemy.ext.asyncio import AsyncSession

from app.core.exceptions import NotFoundError
from app.models.directory import PriceListItem, Product, Vendor
from app.repositories.catalog_repo import (
    PriceListRepository,
    ProductRepository,
    VendorRepository,
)
from app.schemas.catalog import (
    PriceItemCreate,
    ProductCreate,
    ProductUpdate,
    VendorCreate,
)


class CatalogService:
    def __init__(self, session: AsyncSession) -> None:
        self.session = session
        self.vendors = VendorRepository(session)
        self.products = ProductRepository(session)
        self.prices = PriceListRepository(session)

    # --- Вендоры ---
    async def list_vendors(self) -> list[Vendor]:
        return list(await self.vendors.list(limit=500, order_by=Vendor.name))

    async def create_vendor(self, data: VendorCreate) -> Vendor:
        return await self.vendors.create(**data.model_dump())

    # --- Продукты ---
    async def search_products(
        self,
        query: str | None,
        *,
        vendor_id: uuid.UUID | None = None,
        limit: int = 50,
        offset: int = 0,
    ) -> tuple[list[Product], int]:
        return await self.products.search(
            query, vendor_id=vendor_id, limit=limit, offset=offset
        )

    async def get_product(self, product_id: uuid.UUID) -> Product:
        product = await self.products.get(product_id)
        if product is None:
            raise NotFoundError("Продукт не найден")
        return product

    async def create_product(self, data: ProductCreate) -> Product:
        if await self.vendors.get(data.vendor_id) is None:
            raise NotFoundError("Вендор не найден")
        return await self.products.create(**data.model_dump())

    async def update_product(self, product_id: uuid.UUID, data: ProductUpdate) -> Product:
        product = await self.get_product(product_id)
        for field, value in data.model_dump(exclude_unset=True).items():
            setattr(product, field, value)
        await self.session.flush()
        return product

    # --- Прайс ---
    async def list_prices(self, product_id: uuid.UUID) -> list[PriceListItem]:
        await self.get_product(product_id)
        return list(await self.prices.for_product(product_id))

    async def add_price(self, product_id: uuid.UUID, data: PriceItemCreate) -> PriceListItem:
        await self.get_product(product_id)
        return await self.prices.create(product_id=product_id, **data.model_dump())
