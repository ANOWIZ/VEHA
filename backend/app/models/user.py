"""Пользователь (синк из Keycloak/AD) и версионированная себестоимость часа."""

from __future__ import annotations

import uuid
from datetime import date
from decimal import Decimal

from sqlalchemy import Boolean, Date, ForeignKey, Numeric, String, UniqueConstraint
from sqlalchemy.dialects.postgresql import JSONB
from sqlalchemy.dialects.postgresql import UUID as PG_UUID
from sqlalchemy.orm import Mapped, mapped_column, relationship

from app.db.base import DomainBase


class User(DomainBase):
    __tablename__ = "users"

    keycloak_id: Mapped[str | None] = mapped_column(String(64), unique=True, index=True)
    username: Mapped[str] = mapped_column(String(150), unique=True, index=True)
    email: Mapped[str] = mapped_column(String(255), unique=True, index=True)
    full_name: Mapped[str] = mapped_column(String(255))
    department: Mapped[str | None] = mapped_column(String(150))
    position: Mapped[str | None] = mapped_column(String(150))
    grade: Mapped[str | None] = mapped_column(String(50))
    is_active: Mapped[bool] = mapped_column(Boolean, default=True, nullable=False)

    # Для пользователей-контактов Заказчика (роль client) — привязка к клиенту.
    # Ограничивает доступ к проектам этого клиента в портале Заказчика.
    client_id: Mapped[uuid.UUID | None] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("clients.id"), index=True, nullable=True
    )

    # Кэш realm-ролей из Keycloak (список строк). Источник истины — токен.
    roles: Mapped[list[str]] = mapped_column(JSONB, default=list, nullable=False)

    cost_rates: Mapped[list[UserCostRate]] = relationship(
        back_populates="user", cascade="all, delete-orphan", lazy="selectin"
    )


class UserCostRate(DomainBase):
    """Себестоимость часа пользователя, версионируется по датам.

    Действующая ставка на дату D — та, у которой ``valid_from <= D`` и
    (``valid_to is null`` или ``valid_to >= D``). Это критично: себестоимость
    таймшита берётся из ставки, действующей на дату записи, а не текущей.
    """

    __tablename__ = "user_cost_rates"
    __table_args__ = (UniqueConstraint("user_id", "valid_from", name="user_rate_from"),)

    user_id: Mapped[uuid.UUID] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("users.id", ondelete="CASCADE"), index=True
    )
    cost_rate: Mapped[Decimal] = mapped_column(Numeric(15, 2), nullable=False)
    valid_from: Mapped[date] = mapped_column(Date, nullable=False)
    valid_to: Mapped[date | None] = mapped_column(Date, nullable=True)

    user: Mapped[User] = relationship(back_populates="cost_rates")
