"""Сервис пользователей: JIT-provisioning из Keycloak, ставки себестоимости."""

from __future__ import annotations

import uuid
from datetime import date
from decimal import Decimal

from sqlalchemy.ext.asyncio import AsyncSession

from app.core.exceptions import ConflictError, NotFoundError
from app.core.security import Principal
from app.models.user import User, UserCostRate
from app.repositories.user_repo import UserRepository


class UserService:
    def __init__(self, session: AsyncSession) -> None:
        self.session = session
        self.repo = UserRepository(session)

    async def sync_from_principal(self, principal: Principal) -> User:
        """Находит пользователя по keycloak sub / username, создаёт при первом
        входе, обновляет кэш ролей и контактные данные из токена."""
        user = await self.repo.get_by_keycloak_id(principal.subject)
        if user is None:
            user = await self.repo.get_by_username(principal.username)
        if user is None:
            user = await self.repo.create(
                keycloak_id=principal.subject,
                username=principal.username,
                email=principal.email or f"{principal.username}@unknown.local",
                full_name=principal.full_name or principal.username,
                roles=list(principal.roles),
                is_active=True,
            )
        else:
            user.keycloak_id = principal.subject
            if principal.email:
                user.email = principal.email
            # Не затираем «настоящее» ФИО dev-логином, где name == username.
            if principal.full_name and principal.full_name != principal.username:
                user.full_name = principal.full_name
            user.roles = list(principal.roles)
            if not user.is_active:
                user.is_active = True
            await self.session.flush()
        return user

    async def get(self, user_id: uuid.UUID) -> User:
        user = await self.repo.get(user_id)
        if user is None:
            raise NotFoundError("Пользователь не найден")
        return user

    async def list_users(
        self, *, limit: int = 100, offset: int = 0, active_only: bool = True
    ) -> tuple[list[User], int]:
        filters: dict = {}
        if active_only:
            filters["is_active"] = True
        users = await self.repo.list(
            limit=limit, offset=offset, order_by=User.full_name, **filters
        )
        total = await self.repo.count(**filters)
        return list(users), total

    async def set_cost_rate(
        self,
        user_id: uuid.UUID,
        cost_rate: Decimal,
        valid_from: date,
        valid_to: date | None = None,
    ) -> UserCostRate:
        """Назначить версию ставки. Закрывает открытую предыдущую и проверяет
        отсутствие пересечений интервалов."""
        await self.get(user_id)
        overlap = await self.repo.overlapping_rate(user_id, valid_from, valid_to)
        if overlap is not None and overlap.valid_to is None and overlap.valid_from < valid_from:
            # «Закрываем» предыдущую открытую ставку днём до начала новой.
            from datetime import timedelta

            overlap.valid_to = valid_from - timedelta(days=1)
        elif overlap is not None:
            raise ConflictError(
                "Период ставки пересекается с существующей версией",
            )
        rate = UserCostRate(
            user_id=user_id,
            cost_rate=cost_rate,
            valid_from=valid_from,
            valid_to=valid_to,
        )
        self.session.add(rate)
        await self.session.flush()
        return rate

    async def effective_cost_rate(self, user_id: uuid.UUID, on_date: date) -> Decimal | None:
        rate = await self.repo.effective_cost_rate(user_id, on_date)
        return rate.cost_rate if rate else None
