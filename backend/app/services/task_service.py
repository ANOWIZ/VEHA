"""Сервис задач проекта (иерархия этап→задача)."""

from __future__ import annotations

import uuid

from sqlalchemy.ext.asyncio import AsyncSession

from app.core.exceptions import NotFoundError, ValidationError
from app.models.project import Task
from app.repositories.task_repo import TaskRepository
from app.schemas.task import TaskCreate, TaskUpdate


class TaskService:
    def __init__(self, session: AsyncSession) -> None:
        self.session = session
        self.repo = TaskRepository(session)

    async def list_for_project(self, project_id: uuid.UUID) -> list[Task]:
        return list(await self.repo.list_for_project(project_id))

    async def get(self, task_id: uuid.UUID) -> Task:
        task = await self.repo.get(task_id)
        if task is None:
            raise NotFoundError("Задача не найдена")
        return task

    async def create(self, project_id: uuid.UUID, data: TaskCreate) -> Task:
        if data.parent_id is not None:
            parent = await self.repo.get(data.parent_id)
            if parent is None or parent.project_id != project_id:
                raise ValidationError("Родительская задача не найдена в этом проекте")
        return await self.repo.create(project_id=project_id, **data.model_dump())

    async def update(self, task_id: uuid.UUID, data: TaskUpdate) -> Task:
        task = await self.get(task_id)
        for field, value in data.model_dump(exclude_unset=True).items():
            setattr(task, field, value)
        await self.session.flush()
        return task

    async def delete(self, task_id: uuid.UUID) -> None:
        task = await self.get(task_id)
        await self.repo.soft_delete(task)
