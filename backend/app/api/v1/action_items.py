"""API ожиданий от Заказчика — внутренняя сторона (РП/команда проекта)."""

from __future__ import annotations

import uuid

from fastapi import APIRouter, Response

from app.core.deps import CurrentUser, DbSession, ProjectAccess, ProjectManage
from app.core.exceptions import NotFoundError
from app.models.customer import Artifact
from app.schemas.customer import ActionItemCreate, ActionItemOut
from app.services.artifact_storage import (
    content_disposition_attachment,
    read_artifact_async,
)
from app.services.customer_service import CustomerService

router = APIRouter(prefix="/projects/{project_id}/action-items", tags=["customer-portal"])


@router.get("", response_model=list[ActionItemOut])
async def list_items(project: ProjectAccess, session: DbSession) -> list[ActionItemOut]:
    items = await CustomerService(session).list_for_project(project.id)
    return [ActionItemOut.model_validate(i) for i in items]


@router.post("", response_model=ActionItemOut, status_code=201)
async def create_item(
    body: ActionItemCreate, project: ProjectManage, session: DbSession, user: CurrentUser
) -> ActionItemOut:
    item = await CustomerService(session).create_action_item(project.id, body, actor_id=user.id)
    return ActionItemOut.model_validate(item)


@router.post("/{item_id}/accept", response_model=ActionItemOut)
async def accept_item(
    item_id: uuid.UUID, project: ProjectManage, session: DbSession, user: CurrentUser
) -> ActionItemOut:
    svc = CustomerService(session)
    item = await svc._get_item(item_id)
    if item.project_id != project.id:
        raise NotFoundError("Ожидание не найдено в этом проекте")
    item = await svc.accept(item_id, actor_id=user.id)
    return ActionItemOut.model_validate(item)


@router.get("/artifacts/{artifact_id}/download")
async def download_artifact(
    artifact_id: uuid.UUID, project: ProjectAccess, session: DbSession
) -> Response:
    artifact = await session.get(Artifact, artifact_id)
    if artifact is None or artifact.project_id != project.id:
        raise NotFoundError("Файл не найден")
    data = await read_artifact_async(artifact.storage_path)
    return Response(
        content=data,
        media_type=artifact.content_type or "application/octet-stream",
        headers={"Content-Disposition": content_disposition_attachment(artifact.filename)},
    )
