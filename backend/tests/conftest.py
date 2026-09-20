"""Фикстуры тестов.

Юнит-тесты (tests/unit) не требуют БД. Интеграционные (tests/integration)
работают с реальным Postgres: таблицы создаются из метаданных один раз, каждый
тест выполняется в транзакции с откатом — данные не сохраняются между тестами.
"""

from __future__ import annotations

import uuid
from collections.abc import AsyncIterator
from datetime import date
from decimal import Decimal

import pytest
import pytest_asyncio
from sqlalchemy.pool import NullPool
from sqlalchemy.ext.asyncio import AsyncSession, async_sessionmaker, create_async_engine

from app.core.config import settings
from app.models.directory import Client
from app.models.enums import ProjectType, Stage
from app.models.project import Project, ProjectMember
from app.models.enums import ProjectMemberRole
from app.models.user import User, UserCostRate


@pytest_asyncio.fixture
async def session() -> AsyncIterator[AsyncSession]:
    """Сессия в собственном движке/событийном цикле теста (NullPool — без
    переиспользования соединений между циклами). Каждый тест — транзакция с
    откатом. Таблицы создаёт миграция (`alembic upgrade head`) до запуска."""
    eng = create_async_engine(
        settings.sqlalchemy_database_uri, future=True, poolclass=NullPool
    )
    conn = await eng.connect()
    trans = await conn.begin()
    factory = async_sessionmaker(bind=conn, expire_on_commit=False)
    s = factory()
    try:
        yield s
    finally:
        await s.close()
        await trans.rollback()
        await conn.close()
        await eng.dispose()


# ---------- Фабрики-помощники ----------
@pytest_asyncio.fixture
async def make_user(session: AsyncSession):
    async def _make(
        roles: list[str], cost_rate: str | None = "1000.00", full_name: str = "Тест Пользователь"
    ) -> User:
        u = User(
            username=f"user-{uuid.uuid4().hex[:8]}",
            email=f"{uuid.uuid4().hex[:8]}@test.local",
            full_name=full_name,
            roles=roles,
            is_active=True,
        )
        session.add(u)
        await session.flush()
        if cost_rate is not None:
            session.add(
                UserCostRate(
                    user_id=u.id,
                    cost_rate=Decimal(cost_rate),
                    valid_from=date(2020, 1, 1),
                )
            )
            await session.flush()
        return u

    return _make


@pytest_asyncio.fixture
async def make_project(session: AsyncSession):
    async def _make(manager: User, *, name: str = "Проект") -> Project:
        client = Client(name=f"Клиент-{uuid.uuid4().hex[:6]}")
        session.add(client)
        await session.flush()
        p = Project(
            code=f"PRJ-2026-{uuid.uuid4().hex[:3]}",
            name=name,
            client_id=client.id,
            type=ProjectType.IMPLEMENTATION,
            manager_id=manager.id,
            stage=Stage.IMPLEMENTATION,
        )
        session.add(p)
        await session.flush()
        return p

    return _make


@pytest_asyncio.fixture
async def add_member(session: AsyncSession):
    async def _add(project: Project, user: User, role: ProjectMemberRole = ProjectMemberRole.ENGINEER):
        m = ProjectMember(project_id=project.id, user_id=user.id, role=role)
        session.add(m)
        await session.flush()
        return m

    return _add


def pytest_collection_modifyitems(config, items):
    """Помечаем интеграционные тесты маркером для возможного пропуска без БД."""
    for item in items:
        if "integration" in str(item.fspath):
            item.add_marker(pytest.mark.integration)
