"""Репозиторий клиентов."""

from __future__ import annotations

from sqlalchemy import func, or_, select

from app.models.directory import Client
from app.repositories.base import BaseRepository


class ClientRepository(BaseRepository[Client]):
    model = Client

    async def search(
        self, query: str | None, *, limit: int = 50, offset: int = 0
    ) -> tuple[list[Client], int]:
        stmt = select(Client).where(Client.deleted_at.is_(None))
        count_stmt = select(func.count()).select_from(Client).where(Client.deleted_at.is_(None))
        if query:
            pattern = f"%{query.lower()}%"
            cond = or_(
                func.lower(Client.name).like(pattern),
                Client.inn.like(f"%{query}%"),
            )
            stmt = stmt.where(cond)
            count_stmt = count_stmt.where(cond)
        stmt = stmt.order_by(Client.name).limit(limit).offset(offset)
        items = list((await self.session.execute(stmt)).scalars().all())
        total = int((await self.session.execute(count_stmt)).scalar_one())
        return items, total
