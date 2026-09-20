"""API каталога: вендоры, продукты, прайс-листы. Чтение — авторизованным;
изменение — presale/pm/admin."""

from __future__ import annotations

import uuid
from typing import Annotated

from fastapi import APIRouter, Depends, Query

from app.core.deps import CurrentUser, DbSession, require_role
from app.models.enums import Role
from app.models.user import User
from app.schemas.catalog import (
    PriceItemCreate,
    PriceItemOut,
    ProductCreate,
    ProductDetail,
    ProductOut,
    ProductUpdate,
    VendorCreate,
    VendorOut,
)
from app.schemas.common import Page
from app.services.catalog_service import CatalogService

router = APIRouter(prefix="/catalog", tags=["catalog"])

_manage = require_role(Role.ADMIN, Role.PM, Role.PRESALE)

# Прайсовые/закупочные цены вендора (PriceItemOut.price, уровень партнёрской
# скидки) — финансово-чувствительны и НЕ должны видеть engineer/client
# (инвариант CLAUDE.md §5/§11). Доступ — ролям, которым цены нужны по работе.
_PRICE_ROLES = (Role.ADMIN, Role.PRESALE, Role.PM, Role.FINANCE, Role.DIRECTOR)
_prices_view = require_role(*_PRICE_ROLES)


def _can_see_prices(user: User) -> bool:
    return bool({str(r) for r in _PRICE_ROLES} & set(user.roles))


@router.get("/vendors", response_model=list[VendorOut])
async def list_vendors(session: DbSession, _u: CurrentUser) -> list[VendorOut]:
    return [VendorOut.model_validate(v) for v in await CatalogService(session).list_vendors()]


@router.post(
    "/vendors", response_model=VendorOut, status_code=201, dependencies=[Depends(_manage)]
)
async def create_vendor(body: VendorCreate, session: DbSession) -> VendorOut:
    return VendorOut.model_validate(await CatalogService(session).create_vendor(body))


@router.get("/products", response_model=Page[ProductOut])
async def search_products(
    session: DbSession,
    _u: CurrentUser,
    q: str | None = None,
    vendor_id: uuid.UUID | None = None,
    limit: Annotated[int, Query(ge=1, le=200)] = 50,
    offset: Annotated[int, Query(ge=0)] = 0,
) -> Page[ProductOut]:
    items, total = await CatalogService(session).search_products(
        q, vendor_id=vendor_id, limit=limit, offset=offset
    )
    return Page[ProductOut](
        items=[ProductOut.model_validate(p) for p in items],
        total=total,
        limit=limit,
        offset=offset,
    )


@router.get("/products/{product_id}", response_model=ProductDetail)
async def get_product(product_id: uuid.UUID, session: DbSession, _u: CurrentUser) -> ProductDetail:
    svc = CatalogService(session)
    product = await svc.get_product(product_id)
    # Не используем model_validate(product): ProductDetail имеет relationship-поля
    # (vendor, price_items), их lazy-загрузка вне greenlet падает в async.
    base = ProductOut.model_validate(product).model_dump()
    vendor = await svc.vendors.get(product.vendor_id)
    # Прайсы отдаём только ролям, которым закупочные цены доступны; остальным
    # (engineer/client) — пустой список (карточка продукта без цен).
    prices = (
        [PriceItemOut.model_validate(p) for p in await svc.list_prices(product_id)]
        if _can_see_prices(_u)
        else []
    )
    return ProductDetail(
        **base,
        vendor=VendorOut.model_validate(vendor) if vendor else None,
        price_items=prices,
    )


@router.post(
    "/products", response_model=ProductOut, status_code=201, dependencies=[Depends(_manage)]
)
async def create_product(body: ProductCreate, session: DbSession) -> ProductOut:
    return ProductOut.model_validate(await CatalogService(session).create_product(body))


@router.patch(
    "/products/{product_id}", response_model=ProductOut, dependencies=[Depends(_manage)]
)
async def update_product(
    product_id: uuid.UUID, body: ProductUpdate, session: DbSession
) -> ProductOut:
    return ProductOut.model_validate(
        await CatalogService(session).update_product(product_id, body)
    )


@router.get(
    "/products/{product_id}/prices",
    response_model=list[PriceItemOut],
    dependencies=[Depends(_prices_view)],
)
async def list_prices(
    product_id: uuid.UUID, session: DbSession, _u: CurrentUser
) -> list[PriceItemOut]:
    items = await CatalogService(session).list_prices(product_id)
    return [PriceItemOut.model_validate(p) for p in items]


@router.post(
    "/products/{product_id}/prices",
    response_model=PriceItemOut,
    status_code=201,
    dependencies=[Depends(_manage)],
)
async def add_price(
    product_id: uuid.UUID, body: PriceItemCreate, session: DbSession
) -> PriceItemOut:
    return PriceItemOut.model_validate(
        await CatalogService(session).add_price(product_id, body)
    )
