"""API портала Заказчика (роль client). Доступ строго к проектам своего клиента;
финансы недоступны."""

from __future__ import annotations

import uuid
from typing import Annotated

from fastapi import APIRouter, Depends, File, Response, UploadFile

from app.core.config import settings
from app.core.deps import CurrentUser, DbSession, require_role
from app.core.exceptions import NotFoundError, ValidationError
from app.models.customer import Artifact
from app.models.enums import Role
from app.schemas.customer import (
    ActionItemOut,
    ActionItemProvide,
    ArtifactOut,
    PortalProject,
    PortalProjectDetail,
)
from app.services.artifact_storage import (
    content_disposition_attachment,
    read_artifact_async,
)
from app.services.customer_service import CustomerService

router = APIRouter(prefix="/portal", tags=["customer-portal"])

_client = require_role(Role.CLIENT)


@router.get("/projects", response_model=list[PortalProject], dependencies=[Depends(_client)])
async def my_projects(session: DbSession, user: CurrentUser) -> list[PortalProject]:
    data = await CustomerService(session).portal_projects(user)
    return [PortalProject.model_validate(p) for p in data]


@router.get(
    "/projects/{project_id}",
    response_model=PortalProjectDetail,
    dependencies=[Depends(_client)],
)
async def project_detail(
    project_id: uuid.UUID, session: DbSession, user: CurrentUser
) -> PortalProjectDetail:
    data = await CustomerService(session).portal_project_detail(user, project_id)
    return PortalProjectDetail(
        project_id=data["project_id"],
        code=data["code"],
        name=data["name"],
        stage=data["stage"],
        status=data["status"],
        blocked_on_customer=data["blocked_on_customer"],
        action_items=[ActionItemOut.model_validate(i) for i in data["action_items"]],
    )


@router.post(
    "/action-items/{item_id}/provide",
    response_model=ActionItemOut,
    dependencies=[Depends(_client)],
)
async def provide(
    item_id: uuid.UUID, body: ActionItemProvide, session: DbSession, user: CurrentUser
) -> ActionItemOut:
    item = await CustomerService(session).provide(user, item_id, body.note)
    return ActionItemOut.model_validate(item)


@router.post(
    "/action-items/{item_id}/artifacts",
    response_model=ArtifactOut,
    status_code=201,
    dependencies=[Depends(_client)],
)
async def upload_artifact(
    item_id: uuid.UUID,
    session: DbSession,
    user: CurrentUser,
    file: Annotated[UploadFile, File()],
) -> ArtifactOut:
    svc = CustomerService(session)
    item = await svc._get_item(item_id)
    await svc._assert_client_access(user, item.project_id)
    # Читаем максимум лимит+1 байт — не буферизуем гигантское тело в память до проверки.
    max_bytes = settings.artifact_max_mb * 1024 * 1024
    data = await file.read(max_bytes + 1)
    if len(data) > max_bytes:
        raise ValidationError(f"Файл больше {settings.artifact_max_mb} МБ")
    artifact = await svc.save_artifact(
        item.project_id,
        item_id=item_id,
        filename=file.filename or "file",
        content_type=file.content_type,
        data=data,
        uploaded_by=user.id,
    )
    return ArtifactOut.model_validate(artifact)


@router.get("/artifacts/{artifact_id}/download", dependencies=[Depends(_client)])
async def download(
    artifact_id: uuid.UUID, session: DbSession, user: CurrentUser
) -> Response:
    artifact = await session.get(Artifact, artifact_id)
    if artifact is None:
        raise NotFoundError("Файл не найден")
    await CustomerService(session)._assert_client_access(user, artifact.project_id)
    data = await read_artifact_async(artifact.storage_path)
    return Response(
        content=data,
        media_type=artifact.content_type or "application/octet-stream",
        headers={"Content-Disposition": content_disposition_attachment(artifact.filename)},
    )
