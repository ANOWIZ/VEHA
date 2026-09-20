"""Репозиторий пользователей и версионированных ставок себестоимости."""

from __future__ import annotations

import uuid
from datetime import date

from sqlalchemy import and_, or_, select

from app.models.user import User, UserCostRate
from app.repositories.base import BaseRepository


class UserRepository(BaseRepository[User]):
    model = User

    async def get_by_username(self, username: str) -> User | None:
        stmt = select(User).where(User.username == username, User.deleted_at.is_(None))
        return (await self.session.execute(stmt)).scalar_one_or_none()

    async def get_by_keycloak_id(self, kc_id: str) -> User | None:
        stmt = select(User).where(User.keycloak_id == kc_id, User.deleted_at.is_(None))
        return (await self.session.execute(stmt)).scalar_one_or_none()

    async def effective_cost_rate(
        self, user_id: uuid.UUID, on_date: date
    ) -> UserCostRate | None:
        """Ставка, действующая на ``on_date`` (valid_from <= date <= valid_to|∞)."""
        stmt = (
            select(UserCostRate)
            .where(
                UserCostRate.user_id == user_id,
                UserCostRate.valid_from <= on_date,
                or_(
                    UserCostRate.valid_to.is_(None),
                    UserCostRate.valid_to >= on_date,
                ),
                UserCostRate.deleted_at.is_(None),
            )
            .order_by(UserCostRate.valid_from.desc())
            .limit(1)
        )
        return (await self.session.execute(stmt)).scalar_one_or_none()

    async def deactivate_missing(self, active_usernames: set[str]) -> int:
        """Деактивация пользователей, отсутствующих в источнике (ночной синк)."""
        stmt = select(User).where(User.is_active.is_(True), User.deleted_at.is_(None))
        users = (await self.session.execute(stmt)).scalars().all()
        count = 0
        for u in users:
            if u.username not in active_usernames:
                u.is_active = False
                count += 1
        await self.session.flush()
        return count

    async def overlapping_rate(
        self, user_id: uuid.UUID, valid_from: date, valid_to: date | None
    ) -> UserCostRate | None:
        end = valid_to
        stmt = select(UserCostRate).where(
            UserCostRate.user_id == user_id,
            UserCostRate.deleted_at.is_(None),
            and_(
                UserCostRate.valid_from <= (end if end else date.max),
                or_(
                    UserCostRate.valid_to.is_(None),
                    UserCostRate.valid_to >= valid_from,
                ),
            ),
        )
        return (await self.session.execute(stmt)).scalars().first()
