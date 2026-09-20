"""API задач проекта. Доступ — через проверку доступа к проекту."""

from __future__ import annotations

import uuid

from fastapi import APIRouter

from app.core.deps import DbSession, ProjectAccess, ProjectManage
from app.schemas.common import Message
from app.schemas.task import TaskCreate, TaskOut, TaskUpdate
from app.services.task_service import TaskService

# Задачи вложены в проект для единообразной проверки доступа.
router = APIRouter(prefix="/projects/{project_id}/tasks", tags=["tasks"])


@router.get("", response_model=list[TaskOut])
async def list_tasks(project: ProjectAccess, session: DbSession) -> list[TaskOut]:
    tasks = await TaskService(session).list_for_project(project.id)
    return [TaskOut.model_validate(t) for t in tasks]


@router.post("", response_model=TaskOut, status_code=201)
async def create_task(
    body: TaskCreate, project: ProjectManage, session: DbSession
) -> TaskOut:
    task = await TaskService(session).create(project.id, body)
    return TaskOut.model_validate(task)


@router.patch("/{task_id}", response_model=TaskOut)
async def update_task(
    task_id: uuid.UUID, body: TaskUpdate, project: ProjectManage, session: DbSession
) -> TaskOut:
    task = await TaskService(session).update(task_id, body)
    return TaskOut.model_validate(task)


@router.delete("/{task_id}", response_model=Message)
async def delete_task(
    task_id: uuid.UUID, project: ProjectManage, session: DbSession
) -> Message:
    await TaskService(session).delete(task_id)
    return Message(message="Задача удалена")
