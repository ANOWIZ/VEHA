"""Async engine, фабрика сессий и FastAPI-зависимость ``get_db``."""

from __future__ import annotations

from collections.abc import AsyncGenerator

from sqlalchemy.ext.asyncio import AsyncSession, async_sessionmaker, create_async_engine

from app.core.config import settings

engine = create_async_engine(
    settings.sqlalchemy_database_uri,
    echo=False,
    pool_pre_ping=True,  # отсев «протухших» соединений (важно при репликах/idle)
    pool_size=settings.db_pool_size,
    max_overflow=settings.db_max_overflow,
    pool_timeout=settings.db_pool_timeout,
    pool_recycle=1800,  # пересоздавать соединения раз в 30 мин (server-side timeouts)
    future=True,
)

SessionFactory = async_sessionmaker(
    bind=engine,
    expire_on_commit=False,
    autoflush=False,
)


async def get_db() -> AsyncGenerator[AsyncSession, None]:
    """Зависимость FastAPI: одна сессия на запрос, коммит/rollback по исходу."""
    async with SessionFactory() as session:
        try:
            yield session
            await session.commit()
        except Exception:
            await session.rollback()
            raise
