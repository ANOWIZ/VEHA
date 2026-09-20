"""Репозиторий расчётов (Quote) и их строк."""

from __future__ import annotations

import uuid
from collections.abc import Sequence

from sqlalchemy import func, select
from sqlalchemy.orm import selectinload

from app.models.quote import Quote, QuoteLine
from app.repositories.base import BaseRepository


class QuoteRepository(BaseRepository[Quote]):
    model = Quote

    async def get_with_lines(
        self, quote_id: uuid.UUID, project_id: uuid.UUID | None = None
    ) -> Quote | None:
        stmt = (
            select(Quote)
            .where(Quote.id == quote_id, Quote.deleted_at.is_(None))
            .options(selectinload(Quote.lines))
        )
        # Привязка к проекту из пути закрывает IDOR: расчёт чужого проекта,
        # подставленный в /projects/{A}/quotes/{B}, не найдётся (CLAUDE.md §5).
        if project_id is not None:
            stmt = stmt.where(Quote.project_id == project_id)
        return (await self.session.execute(stmt)).scalar_one_or_none()

    async def list_for_project(self, project_id: uuid.UUID) -> Sequence[Quote]:
        stmt = (
            select(Quote)
            .where(Quote.project_id == project_id, Quote.deleted_at.is_(None))
            .order_by(Quote.version.desc())
        )
        return (await self.session.execute(stmt)).scalars().all()

    async def max_version(self, project_id: uuid.UUID) -> int:
        stmt = select(func.coalesce(func.max(Quote.version), 0)).where(
            Quote.project_id == project_id
        )
        return int((await self.session.execute(stmt)).scalar_one())

    async def get_line(self, line_id: uuid.UUID) -> QuoteLine | None:
        return (
            await self.session.execute(select(QuoteLine).where(QuoteLine.id == line_id))
        ).scalar_one_or_none()
