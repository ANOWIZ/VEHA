"""Репозиторий задач проекта."""

from __future__ import annotations

import uuid
from collections.abc import Sequence

from sqlalchemy import select

from app.models.project import Task
from app.repositories.base import BaseRepository


class TaskRepository(BaseRepository[Task]):
    model = Task

    async def list_for_project(self, project_id: uuid.UUID) -> Sequence[Task]:
        stmt = (
            select(Task)
            .where(Task.project_id == project_id, Task.deleted_at.is_(None))
            .order_by(Task.created_at)
        )
        return (await self.session.execute(stmt)).scalars().all()
