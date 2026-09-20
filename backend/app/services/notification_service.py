"""Сервис уведомлений (in-app). Email/Telegram могут подписаться на те же события."""

from __future__ import annotations

import uuid
from collections.abc import Sequence

from sqlalchemy import func, select, update
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.notification import Notification


class NotificationService:
    def __init__(self, session: AsyncSession) -> None:
        self.session = session

    async def notify(
        self,
        user_id: uuid.UUID,
        *,
        kind: str,
        title: str,
        body: str | None = None,
        link: str | None = None,
    ) -> Notification:
        n = Notification(user_id=user_id, kind=kind, title=title, body=body, link=link)
        self.session.add(n)
        await self.session.flush()
        return n

    async def list_for(
        self, user_id: uuid.UUID, *, unread_only: bool = False, limit: int = 50
    ) -> Sequence[Notification]:
        stmt = select(Notification).where(
            Notification.user_id == user_id, Notification.deleted_at.is_(None)
        )
        if unread_only:
            stmt = stmt.where(Notification.is_read.is_(False))
        stmt = stmt.order_by(Notification.created_at.desc()).limit(limit)
        return (await self.session.execute(stmt)).scalars().all()

    async def unread_count(self, user_id: uuid.UUID) -> int:
        stmt = select(func.count()).select_from(Notification).where(
            Notification.user_id == user_id,
            Notification.is_read.is_(False),
            Notification.deleted_at.is_(None),
        )
        return int((await self.session.execute(stmt)).scalar_one())

    async def mark_read(self, user_id: uuid.UUID, notification_id: uuid.UUID) -> None:
        await self.session.execute(
            update(Notification)
            .where(Notification.id == notification_id, Notification.user_id == user_id)
            .values(is_read=True)
        )
        await self.session.flush()

    async def mark_all_read(self, user_id: uuid.UUID) -> int:
        result = await self.session.execute(
            update(Notification)
            .where(Notification.user_id == user_id, Notification.is_read.is_(False))
            .values(is_read=True)
        )
        await self.session.flush()
        return result.rowcount or 0
