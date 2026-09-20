"""Сервис справочника клиентов."""

from __future__ import annotations

import uuid

from sqlalchemy.ext.asyncio import AsyncSession

from app.core.exceptions import NotFoundError
from app.models.directory import Client
from app.repositories.client_repo import ClientRepository
from app.schemas.client import ClientCreate, ClientUpdate


class ClientService:
    def __init__(self, session: AsyncSession) -> None:
        self.session = session
        self.repo = ClientRepository(session)

    async def search(
        self, query: str | None, *, limit: int = 50, offset: int = 0
    ) -> tuple[list[Client], int]:
        return await self.repo.search(query, limit=limit, offset=offset)

    async def get(self, client_id: uuid.UUID) -> Client:
        client = await self.repo.get(client_id)
        if client is None:
            raise NotFoundError("Клиент не найден")
        return client

    async def create(self, data: ClientCreate) -> Client:
        return await self.repo.create(**data.model_dump())

    async def update(self, client_id: uuid.UUID, data: ClientUpdate) -> Client:
        client = await self.get(client_id)
        for field, value in data.model_dump(exclude_unset=True).items():
            setattr(client, field, value)
        await self.session.flush()
        return client

    async def delete(self, client_id: uuid.UUID) -> None:
        client = await self.get(client_id)
        await self.repo.soft_delete(client)
