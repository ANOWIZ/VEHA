"""Celery-задачи. Блокирующая/фоновая работа: пересчёт маржи, выгрузки,
деактивация уволенных, генерация XLSX. Внутри — мост к async-сервисам."""

from __future__ import annotations

import asyncio
import uuid
from collections.abc import Awaitable
from typing import TypeVar

from app.db.session import SessionFactory
from app.tasks.celery_app import celery_app

T = TypeVar("T")


def _run(coro: Awaitable[T]) -> T:
    """Запуск async-кода в синхронном контексте Celery-воркера."""
    return asyncio.run(coro)  # type: ignore[arg-type]


@celery_app.task(name="app.tasks.jobs.recalc_project_margin")
def recalc_project_margin(project_id: str) -> dict:
    """Пересчитать и закэшировать маржинальность проекта (после approve/изменения
    затрат). Реализация — в финансовом сервисе (Фаза 4)."""

    async def _job() -> dict:
        from app.services.finance_service import FinanceService

        async with SessionFactory() as session:
            result = await FinanceService(session).recalc_margin(uuid.UUID(project_id))
            await session.commit()
            return result

    return _run(_job())


@celery_app.task(name="app.tasks.jobs.deactivate_left_users")
def deactivate_left_users() -> dict:
    """Ночная деактивация уволенных по данным Keycloak/AD."""

    async def _job() -> dict:
        from app.integrations.keycloak_admin import KeycloakAdminClient
        from app.repositories.user_repo import UserRepository

        async with SessionFactory() as session:
            try:
                active = await KeycloakAdminClient().list_active_usernames()
            except Exception:
                return {"deactivated": 0, "skipped": "keycloak_unavailable"}
            count = await UserRepository(session).deactivate_missing(active)
            await session.commit()
            return {"deactivated": count}

    return _run(_job())


@celery_app.task(name="app.tasks.jobs.export_quote_xlsx")
def export_quote_xlsx(quote_id: str) -> str:
    """Серверная генерация XLSX спецификации (Фаза 3). Возвращает путь/ключ."""

    async def _job() -> str:
        from app.services.quote_export import build_quote_xlsx

        async with SessionFactory() as session:
            return await build_quote_xlsx(session, uuid.UUID(quote_id))

    return _run(_job())
