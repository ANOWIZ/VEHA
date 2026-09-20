"""Декларативная база SQLAlchemy 2.0 и общие миксины.

Все доменные таблицы: ``id UUID PK``, ``created_at``, ``updated_at`` и soft
delete через ``deleted_at`` (см. CLAUDE.md §7).
"""

from __future__ import annotations

import uuid
from datetime import datetime

from sqlalchemy import DateTime, MetaData, func
from sqlalchemy.dialects.postgresql import UUID as PG_UUID
from sqlalchemy.orm import DeclarativeBase, Mapped, declared_attr, mapped_column

# Единая конвенция имён ограничений — стабильные имена для Alembic autogenerate.
NAMING_CONVENTION = {
    "ix": "ix_%(column_0_label)s",
    "uq": "uq_%(table_name)s_%(column_0_name)s",
    "ck": "ck_%(table_name)s_%(constraint_name)s",
    "fk": "fk_%(table_name)s_%(column_0_name)s_%(referred_table_name)s",
    "pk": "pk_%(table_name)s",
}


class Base(DeclarativeBase):
    metadata = MetaData(naming_convention=NAMING_CONVENTION)


class UUIDMixin:
    # UUID генерируется на стороне приложения (default), что делает схему
    # переносимой и не привязывает создание таблиц к расширению uuid-ossp.
    # Расширения uuid-ossp / pg_trgm всё равно создаются в первой миграции.
    id: Mapped[uuid.UUID] = mapped_column(
        PG_UUID(as_uuid=True),
        primary_key=True,
        default=uuid.uuid4,
    )


class TimestampMixin:
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), server_default=func.now(), nullable=False
    )
    updated_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True),
        server_default=func.now(),
        onupdate=func.now(),
        nullable=False,
    )


class SoftDeleteMixin:
    deleted_at: Mapped[datetime | None] = mapped_column(
        DateTime(timezone=True), nullable=True, default=None
    )

    @property
    def is_deleted(self) -> bool:
        return self.deleted_at is not None


class DomainBase(Base, UUIDMixin, TimestampMixin, SoftDeleteMixin):
    """База для доменных сущностей: UUID PK + временные метки + soft delete."""

    __abstract__ = True

    @declared_attr.directive
    def __tablename__(cls) -> str:
        # CamelCase -> snake_case + множественное число по умолчанию переопределяется
        name = cls.__name__
        snake = "".join(f"_{c.lower()}" if c.isupper() else c for c in name).lstrip("_")
        return snake
