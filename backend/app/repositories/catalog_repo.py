"""Репозиторий каталога: вендоры, продукты (поиск pg_trgm), прайс-листы."""

from __future__ import annotations

import uuid
from collections.abc import Sequence
from datetime import date

from sqlalchemy import func, or_, select

from app.models.directory import PriceListItem, Product, Vendor
from app.repositories.base import BaseRepository


class VendorRepository(BaseRepository[Vendor]):
    model = Vendor


class ProductRepository(BaseRepository[Product]):
    model = Product

    async def search(
        self,
        query: str | None,
        *,
        vendor_id: uuid.UUID | None = None,
        limit: int = 50,
        offset: int = 0,
    ) -> tuple[list[Product], int]:
        stmt = select(Product).where(Product.deleted_at.is_(None))
        count_stmt = (
            select(func.count()).select_from(Product).where(Product.deleted_at.is_(None))
        )
        if vendor_id:
            stmt = stmt.where(Product.vendor_id == vendor_id)
            count_stmt = count_stmt.where(Product.vendor_id == vendor_id)
        if query:
            # Триграммный поиск (pg_trgm): по имени и редакции.
            pattern = f"%{query.lower()}%"
            cond = or_(
                func.lower(Product.name).like(pattern),
                func.lower(Product.edition).like(pattern),
            )
            stmt = stmt.where(cond)
            count_stmt = count_stmt.where(cond)
        stmt = stmt.order_by(Product.name).limit(limit).offset(offset)
        items = list((await self.session.execute(stmt)).scalars().all())
        total = int((await self.session.execute(count_stmt)).scalar_one())
        return items, total


class PriceListRepository(BaseRepository[PriceListItem]):
    model = PriceListItem

    async def for_product(self, product_id: uuid.UUID) -> Sequence[PriceListItem]:
        stmt = (
            select(PriceListItem)
            .where(
                PriceListItem.product_id == product_id,
                PriceListItem.deleted_at.is_(None),
            )
            .order_by(PriceListItem.valid_from.desc())
        )
        return (await self.session.execute(stmt)).scalars().all()

    async def effective(
        self, product_id: uuid.UUID, metric: str, on_date: date
    ) -> PriceListItem | None:
        stmt = (
            select(PriceListItem)
            .where(
                PriceListItem.product_id == product_id,
                PriceListItem.metric == metric,
                PriceListItem.valid_from <= on_date,
                or_(
                    PriceListItem.valid_to.is_(None),
                    PriceListItem.valid_to >= on_date,
                ),
                PriceListItem.deleted_at.is_(None),
            )
            .order_by(PriceListItem.valid_from.desc())
            .limit(1)
        )
        return (await self.session.execute(stmt)).scalar_one_or_none()
