"""Alembic environment (async). target_metadata = Base.metadata всех моделей.

Миграции пишем только autogenerate с ревью diff:
    uv run alembic revision --autogenerate -m "описание"
    uv run alembic upgrade head
"""

from __future__ import annotations

import asyncio
from logging.config import fileConfig

from alembic import context
from sqlalchemy.engine import Connection
from sqlalchemy.ext.asyncio import async_engine_from_config

from app.core.config import settings

# Импорт пакета моделей регистрирует все таблицы в Base.metadata.
from app.models import Base  # noqa: F401  (side-effect import)

config = context.config
if config.config_file_name is not None:
    fileConfig(config.config_file_name)

config.set_main_option("sqlalchemy.url", settings.alembic_database_uri)
target_metadata = Base.metadata


def run_migrations_offline() -> None:
    context.configure(
        url=settings.alembic_database_uri,
        target_metadata=target_metadata,
        literal_binds=True,
        compare_type=True,
        compare_server_default=True,
        dialect_opts={"paramstyle": "named"},
    )
    with context.begin_transaction():
        context.run_migrations()


def do_run_migrations(connection: Connection) -> None:
    context.configure(
        connection=connection,
        target_metadata=target_metadata,
        compare_type=True,
        compare_server_default=True,
    )
    with context.begin_transaction():
        context.run_migrations()


async def run_migrations_online() -> None:
    connectable = async_engine_from_config(
        {"sqlalchemy.url": settings.alembic_database_uri},
        prefix="sqlalchemy.",
        future=True,
    )
    async with connectable.connect() as connection:
        await connection.run_sync(do_run_migrations)
    await connectable.dispose()


if context.is_offline_mode():
    run_migrations_offline()
else:
    asyncio.run(run_migrations_online())
