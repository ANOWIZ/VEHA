"""Сервис портала Заказчика: ожидания работ (блокеры), артефакты, статус проекта.

Блокер со статусом WAITING означает, что проект «стоит» на стороне Заказчика.
Заказчик видит свои проекты и то, что требуется от него, с ответственным."""

from __future__ import annotations

import uuid
from datetime import UTC, datetime

from sqlalchemy import func, select
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.exceptions import ConflictError, ForbiddenError, NotFoundError
from app.models.customer import Artifact, CustomerActionItem
from app.models.enums import CustomerActionStatus
from app.models.project import Project
from app.models.user import User
from app.schemas.customer import ActionItemCreate
from app.services.artifact_storage import save_artifact_async
from app.services.notification_service import NotificationService


class CustomerService:
    def __init__(self, session: AsyncSession) -> None:
        self.session = session
        self.notifier = NotificationService(session)

    # ---------- Внутренняя сторона (РП) ----------
    async def create_action_item(
        self, project_id: uuid.UUID, data: ActionItemCreate, actor_id: uuid.UUID
    ) -> CustomerActionItem:
        item = CustomerActionItem(
            project_id=project_id,
            created_by=actor_id,
            status=CustomerActionStatus.WAITING,
            **data.model_dump(),
        )
        self.session.add(item)
        await self.session.flush()
        if item.responsible_user_id:
            await self.notifier.notify(
                item.responsible_user_id,
                kind="customer_action_assigned",
                title="Требуется ваше участие по проекту",
                body=f"{item.title}",
                link="/portal",
            )
        return item

    async def list_for_project(self, project_id: uuid.UUID) -> list[CustomerActionItem]:
        stmt = (
            select(CustomerActionItem)
            .where(
                CustomerActionItem.project_id == project_id,
                CustomerActionItem.deleted_at.is_(None),
            )
            .order_by(CustomerActionItem.created_at.desc())
        )
        return list((await self.session.execute(stmt)).scalars().all())

    async def open_counts(self, project_ids: list[uuid.UUID]) -> dict[uuid.UUID, int]:
        """{project_id: число открытых (WAITING) блокеров} — пакетно."""
        if not project_ids:
            return {}
        stmt = (
            select(CustomerActionItem.project_id, func.count())
            .where(
                CustomerActionItem.project_id.in_(project_ids),
                CustomerActionItem.status == CustomerActionStatus.WAITING,
                CustomerActionItem.deleted_at.is_(None),
            )
            .group_by(CustomerActionItem.project_id)
        )
        return {pid: int(c) for pid, c in (await self.session.execute(stmt)).all()}

    async def _get_item(self, item_id: uuid.UUID) -> CustomerActionItem:
        item = await self.session.get(CustomerActionItem, item_id)
        if item is None or item.deleted_at is not None:
            raise NotFoundError("Ожидание не найдено")
        return item

    async def accept(self, item_id: uuid.UUID, actor_id: uuid.UUID) -> CustomerActionItem:
        item = await self._get_item(item_id)
        if item.status == CustomerActionStatus.ACCEPTED:
            raise ConflictError("Ожидание уже принято")
        item.status = CustomerActionStatus.ACCEPTED
        item.accepted_at = datetime.now(UTC)
        await self.session.flush()
        return item

    # ---------- Сторона Заказчика (портал) ----------
    async def _assert_client_access(self, user: User, project_id: uuid.UUID) -> Project:
        project = await self.session.get(Project, project_id)
        if project is None or project.deleted_at is not None:
            raise NotFoundError("Проект не найден")
        if user.client_id is None or project.client_id != user.client_id:
            raise ForbiddenError("Нет доступа к этому проекту")
        return project

    async def portal_projects(self, user: User) -> list[dict]:
        if user.client_id is None:
            return []
        stmt = select(Project).where(
            Project.client_id == user.client_id, Project.deleted_at.is_(None)
        ).order_by(Project.created_at.desc())
        projects = list((await self.session.execute(stmt)).scalars().all())
        counts = await self.open_counts([p.id for p in projects])
        return [
            {
                "project_id": p.id,
                "code": p.code,
                "name": p.name,
                "stage": p.stage,
                "status": p.status,
                "planned_end": None,
                "open_actions": counts.get(p.id, 0),
                "blocked_on_customer": counts.get(p.id, 0) > 0,
            }
            for p in projects
        ]

    async def portal_project_detail(self, user: User, project_id: uuid.UUID) -> dict:
        project = await self._assert_client_access(user, project_id)
        items = await self.list_for_project(project_id)
        open_n = sum(1 for i in items if i.status == CustomerActionStatus.WAITING)
        return {
            "project_id": project.id,
            "code": project.code,
            "name": project.name,
            "stage": project.stage,
            "status": project.status,
            "blocked_on_customer": open_n > 0,
            "action_items": items,
        }

    async def provide(
        self, user: User, item_id: uuid.UUID, note: str | None
    ) -> CustomerActionItem:
        item = await self._get_item(item_id)
        await self._assert_client_access(user, item.project_id)
        if item.status == CustomerActionStatus.ACCEPTED:
            raise ConflictError("Ожидание уже принято — изменения недоступны")
        item.status = CustomerActionStatus.PROVIDED
        item.provided_at = datetime.now(UTC)
        item.provided_note = note
        await self.session.flush()
        # Уведомляем РП проекта о предоставлении данных Заказчиком.
        project = await self.session.get(Project, item.project_id)
        if project is not None:
            await self.notifier.notify(
                project.manager_id,
                kind="customer_action_provided",
                title="Заказчик предоставил данные",
                body=f"{item.title} (проект {project.code})",
                link=f"/projects/{project.id}",
            )
        return item

    # ---------- Артефакты ----------
    async def save_artifact(
        self,
        project_id: uuid.UUID,
        *,
        item_id: uuid.UUID | None,
        filename: str,
        content_type: str | None,
        data: bytes,
        uploaded_by: uuid.UUID | None,
    ) -> Artifact:
        ref = await save_artifact_async(project_id, filename, data)
        artifact = Artifact(
            project_id=project_id,
            action_item_id=item_id,
            filename=filename,
            content_type=content_type,
            size_bytes=len(data),
            storage_path=ref,
            uploaded_by=uploaded_by,
        )
        self.session.add(artifact)
        await self.session.flush()
        return artifact
