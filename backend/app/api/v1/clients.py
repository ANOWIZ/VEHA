"""API справочника клиентов."""

from __future__ import annotations

import uuid
from typing import Annotated

from fastapi import APIRouter, Depends, Query

from app.core.deps import CurrentUser, DbSession, require_role
from app.models.enums import Role
from app.schemas.client import ClientCreate, ClientOut, ClientUpdate
from app.schemas.common import Message, Page
from app.services.client_service import ClientService

router = APIRouter(prefix="/clients", tags=["clients"])

# Создание/изменение справочника — pm/presale/admin; чтение — любой авторизованный.
_manage = require_role(Role.ADMIN, Role.PM, Role.PRESALE, Role.DIRECTOR)


@router.get("", response_model=Page[ClientOut])
async def list_clients(
    session: DbSession,
    _user: CurrentUser,
    q: str | None = None,
    limit: Annotated[int, Query(ge=1, le=500)] = 50,
    offset: Annotated[int, Query(ge=0)] = 0,
) -> Page[ClientOut]:
    items, total = await ClientService(session).search(q, limit=limit, offset=offset)
    return Page[ClientOut](
        items=[ClientOut.model_validate(c) for c in items],
        total=total,
        limit=limit,
        offset=offset,
    )


@router.get("/{client_id}", response_model=ClientOut)
async def get_client(client_id: uuid.UUID, session: DbSession, _user: CurrentUser) -> ClientOut:
    return ClientOut.model_validate(await ClientService(session).get(client_id))


@router.post("", response_model=ClientOut, dependencies=[Depends(_manage)], status_code=201)
async def create_client(body: ClientCreate, session: DbSession) -> ClientOut:
    return ClientOut.model_validate(await ClientService(session).create(body))


@router.patch("/{client_id}", response_model=ClientOut, dependencies=[Depends(_manage)])
async def update_client(
    client_id: uuid.UUID, body: ClientUpdate, session: DbSession
) -> ClientOut:
    return ClientOut.model_validate(await ClientService(session).update(client_id, body))


@router.delete("/{client_id}", response_model=Message, dependencies=[Depends(_manage)])
async def delete_client(client_id: uuid.UUID, session: DbSession) -> Message:
    await ClientService(session).delete(client_id)
    return Message(message="Клиент удалён")
