"""API калькулятора лицензий: версии расчётов, строки, статусы, экспорт XLSX.

Вложено в проект — доступ через ``require_project_access``; изменения —
только presale/pm/admin и только для черновика."""

from __future__ import annotations

import uuid
from functools import partial

import anyio
from fastapi import APIRouter, Depends, Response

from app.core.deps import CurrentUser, DbSession, ProjectAccess, require_role
from app.models.enums import Role
from app.schemas.quote import (
    QuoteCreate,
    QuoteDetail,
    QuoteLineInput,
    QuoteOut,
    QuoteStatusUpdate,
)
from app.services.quote_export import build_quote_xlsx_bytes
from app.services.quote_service import QuoteService

router = APIRouter(prefix="/projects/{project_id}/quotes", tags=["quotes"])

_edit = require_role(Role.PRESALE, Role.PM, Role.ADMIN)
# Чтение ТКП раскрывает себестоимость/маржу — закрыто от engineer/client
# (инвариант CLAUDE.md §5). Калькулятор — инструмент presale, поэтому ему доступно.
_view = require_role(Role.PRESALE, Role.PM, Role.ADMIN, Role.DIRECTOR, Role.FINANCE)


@router.get("", response_model=list[QuoteOut], dependencies=[Depends(_view)])
async def list_quotes(project: ProjectAccess, session: DbSession) -> list[QuoteOut]:
    quotes = await QuoteService(session).list_for_project(project.id)
    return [QuoteOut.model_validate(q) for q in quotes]


@router.post("", response_model=QuoteDetail, status_code=201, dependencies=[Depends(_edit)])
async def create_quote(
    body: QuoteCreate, project: ProjectAccess, session: DbSession, user: CurrentUser
) -> QuoteDetail:
    quote = await QuoteService(session).create(project.id, body, actor_id=user.id)
    return QuoteDetail.model_validate(quote)


@router.get("/{quote_id}", response_model=QuoteDetail, dependencies=[Depends(_view)])
async def get_quote(
    quote_id: uuid.UUID, project: ProjectAccess, session: DbSession
) -> QuoteDetail:
    quote = await QuoteService(session).get(quote_id, project.id)
    return QuoteDetail.model_validate(quote)


@router.post(
    "/{quote_id}/lines", response_model=QuoteDetail, dependencies=[Depends(_edit)]
)
async def add_line(
    quote_id: uuid.UUID, body: QuoteLineInput, project: ProjectAccess, session: DbSession
) -> QuoteDetail:
    quote = await QuoteService(session).add_line(quote_id, body, project.id)
    return QuoteDetail.model_validate(quote)


@router.put(
    "/{quote_id}/lines/{line_id}",
    response_model=QuoteDetail,
    dependencies=[Depends(_edit)],
)
async def update_line(
    quote_id: uuid.UUID,
    line_id: uuid.UUID,
    body: QuoteLineInput,
    project: ProjectAccess,
    session: DbSession,
) -> QuoteDetail:
    quote = await QuoteService(session).update_line(quote_id, line_id, body, project.id)
    return QuoteDetail.model_validate(quote)


@router.delete(
    "/{quote_id}/lines/{line_id}",
    response_model=QuoteDetail,
    dependencies=[Depends(_edit)],
)
async def delete_line(
    quote_id: uuid.UUID, line_id: uuid.UUID, project: ProjectAccess, session: DbSession
) -> QuoteDetail:
    quote = await QuoteService(session).delete_line(quote_id, line_id, project.id)
    return QuoteDetail.model_validate(quote)


@router.post(
    "/{quote_id}/status", response_model=QuoteOut, dependencies=[Depends(_edit)]
)
async def set_status(
    quote_id: uuid.UUID,
    body: QuoteStatusUpdate,
    project: ProjectAccess,
    session: DbSession,
    user: CurrentUser,
) -> QuoteOut:
    quote = await QuoteService(session).set_status(
        quote_id, body.status, actor_id=user.id, project_id=project.id
    )
    return QuoteOut.model_validate(quote)


@router.post("/{quote_id}/clone", response_model=QuoteDetail, dependencies=[Depends(_edit)])
async def clone_quote(
    quote_id: uuid.UUID, project: ProjectAccess, session: DbSession, user: CurrentUser
) -> QuoteDetail:
    quote = await QuoteService(session).clone_new_version(
        quote_id, actor_id=user.id, project_id=project.id
    )
    return QuoteDetail.model_validate(quote)


@router.get("/{quote_id}/export.xlsx", dependencies=[Depends(_view)])
async def export_xlsx(
    quote_id: uuid.UUID, project: ProjectAccess, session: DbSession
) -> Response:
    quote = await QuoteService(session).get(quote_id, project.id)
    # Генерация openpyxl — синхронная CPU-bound: выносим в поток (CLAUDE.md §7).
    data = await anyio.to_thread.run_sync(
        partial(build_quote_xlsx_bytes, quote, project.code, project.name)
    )
    filename = f"TKP_{project.code}_v{quote.version}.xlsx"
    return Response(
        content=data,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": f'attachment; filename="{filename}"'},
    )
