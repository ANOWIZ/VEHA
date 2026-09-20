"""Базовый async-репозиторий с поддержкой soft delete."""

from __future__ import annotations

import uuid
from collections.abc import Sequence
from datetime import UTC
from typing import Any, Generic, TypeVar

from sqlalchemy import func, select
from sqlalchemy.ext.asyncio import AsyncSession

from app.db.base import DomainBase

ModelT = TypeVar("ModelT", bound=DomainBase)


class BaseRepository(Generic[ModelT]):
    model: type[ModelT]

    def __init__(self, session: AsyncSession) -> None:
        self.session = session

    def _base_query(self, *, include_deleted: bool = False):
        stmt = select(self.model)
        if not include_deleted:
            stmt = stmt.where(self.model.deleted_at.is_(None))
        return stmt

    async def get(self, id_: uuid.UUID, *, include_deleted: bool = False) -> ModelT | None:
        stmt = self._base_query(include_deleted=include_deleted).where(self.model.id == id_)
        return (await self.session.execute(stmt)).scalar_one_or_none()

    async def list(
        self,
        *,
        limit: int = 100,
        offset: int = 0,
        order_by: Any | None = None,
        include_deleted: bool = False,
        **filters: Any,
    ) -> Sequence[ModelT]:
        stmt = self._base_query(include_deleted=include_deleted)
        for field_, value in filters.items():
            stmt = stmt.where(getattr(self.model, field_) == value)
        if order_by is not None:
            stmt = stmt.order_by(order_by)
        stmt = stmt.limit(limit).offset(offset)
        return (await self.session.execute(stmt)).scalars().all()

    async def count(self, *, include_deleted: bool = False, **filters: Any) -> int:
        stmt = select(func.count()).select_from(self.model)
        if not include_deleted:
            stmt = stmt.where(self.model.deleted_at.is_(None))
        for field_, value in filters.items():
            stmt = stmt.where(getattr(self.model, field_) == value)
        return int((await self.session.execute(stmt)).scalar_one())

    def add(self, obj: ModelT) -> ModelT:
        self.session.add(obj)
        return obj

    async def create(self, **kwargs: Any) -> ModelT:
        obj = self.model(**kwargs)
        self.session.add(obj)
        await self.session.flush()
        return obj

    async def soft_delete(self, obj: ModelT) -> None:
        from datetime import datetime

        obj.deleted_at = datetime.now(UTC)
        await self.session.flush()

    async def flush(self) -> None:
        await self.session.flush()
