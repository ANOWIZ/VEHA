"""Сервис проектов: жизненный цикл по стадиям, участники, вехи, аудит."""

from __future__ import annotations

import uuid
from datetime import date

from sqlalchemy.ext.asyncio import AsyncSession

from app.core.exceptions import AppError, ConflictError, ErrorCode, NotFoundError
from app.models.enums import AuditAction, ProjectStatus, Stage
from app.models.project import Milestone, Project, ProjectMember, ProjectStageTransition
from app.models.user import User
from app.repositories.project_repo import ProjectRepository
from app.repositories.user_repo import UserRepository
from app.schemas.project import (
    MemberCreate,
    MilestoneCreate,
    ProjectCreate,
    ProjectUpdate,
)
from app.services import stage_rules
from app.services.audit_service import AuditService


class ProjectService:
    def __init__(self, session: AsyncSession) -> None:
        self.session = session
        self.repo = ProjectRepository(session)
        self.users = UserRepository(session)
        self.audit = AuditService(session)

    async def create(self, data: ProjectCreate, actor_id: uuid.UUID) -> Project:
        if await self.users.get(data.manager_id) is None:
            raise NotFoundError("Руководитель проекта не найден")
        year = (data.planned_start or date.today()).year
        code = await self.repo.next_code(year)
        project = await self.repo.create(
            code=code,
            name=data.name,
            client_id=data.client_id,
            type=data.type,
            manager_id=data.manager_id,
            curator_id=data.curator_id,
            stage=Stage.PRESALE,
            status=ProjectStatus.ACTIVE,
            planned_start=data.planned_start,
            planned_end=data.planned_end,
            budget_revenue=data.budget_revenue,
            contract_ref=data.contract_ref,
        )
        self.session.add(
            ProjectStageTransition(
                project_id=project.id,
                from_stage=None,
                to_stage=Stage.PRESALE,
                reason="Создание проекта",
                created_by=actor_id,
            )
        )
        await self.audit.record(
            entity="Project",
            entity_id=project.id,
            action=AuditAction.CREATE,
            actor_id=actor_id,
            diff={"code": code, "name": project.name},
        )
        await self.session.flush()
        return project

    async def get(self, project_id: uuid.UUID) -> Project:
        project = await self.repo.get(project_id)
        if project is None:
            raise NotFoundError("Проект не найден")
        return project

    async def list_for_user(self, user: User, **filters) -> tuple[list[Project], int]:
        return await self.repo.list_for_user(user, **filters)

    async def update(
        self, project_id: uuid.UUID, data: ProjectUpdate, actor_id: uuid.UUID
    ) -> Project:
        project = await self.get(project_id)
        if project.status in (ProjectStatus.CLOSED, ProjectStatus.CANCELLED):
            # Разрешаем только смену статуса (например, reopen администратором).
            changes = data.model_dump(exclude_unset=True)
            if set(changes) - {"status"}:
                raise AppError(
                    "Проект закрыт/отменён: доступно только изменение статуса",
                    code=ErrorCode.PROJECT_CLOSED,
                    status_code=409,
                )
        diff: dict = {}
        for field, value in data.model_dump(exclude_unset=True).items():
            old = getattr(project, field)
            if old != value:
                diff[field] = {"from": str(old), "to": str(value)}
                setattr(project, field, value)
        if diff:
            await self.audit.record(
                entity="Project",
                entity_id=project.id,
                action=AuditAction.UPDATE,
                actor_id=actor_id,
                diff=diff,
            )
        await self.session.flush()
        return project

    async def change_stage(
        self,
        project_id: uuid.UUID,
        to_stage: Stage,
        reason: str | None,
        actor_id: uuid.UUID,
    ) -> Project:
        project = await self.get(project_id)
        if project.status in (ProjectStatus.CLOSED, ProjectStatus.CANCELLED):
            raise AppError(
                "Нельзя менять стадию закрытого/отменённого проекта",
                code=ErrorCode.PROJECT_CLOSED,
                status_code=409,
            )
        try:
            stage_rules.validate_transition(project.stage, to_stage, reason=reason)
        except ValueError as exc:
            raise AppError(
                str(exc), code=ErrorCode.INVALID_STAGE_TRANSITION, status_code=409
            ) from exc
        from_stage = project.stage
        project.stage = to_stage
        # Авто-проставление фактических дат при ключевых переходах.
        if to_stage == Stage.IMPLEMENTATION and project.actual_start is None:
            project.actual_start = date.today()
        if to_stage == Stage.CLOSED:
            project.actual_end = date.today()
            project.status = ProjectStatus.CLOSED
        self.session.add(
            ProjectStageTransition(
                project_id=project.id,
                from_stage=from_stage,
                to_stage=to_stage,
                reason=reason,
                created_by=actor_id,
            )
        )
        await self.audit.record(
            entity="Project",
            entity_id=project.id,
            action=AuditAction.STAGE_CHANGE,
            actor_id=actor_id,
            diff={"from": str(from_stage), "to": str(to_stage), "reason": reason},
        )
        await self.session.flush()
        return project

    async def delete(self, project_id: uuid.UUID, actor_id: uuid.UUID) -> None:
        project = await self.get(project_id)
        await self.repo.soft_delete(project)
        await self.audit.record(
            entity="Project",
            entity_id=project.id,
            action=AuditAction.DELETE,
            actor_id=actor_id,
        )

    # --- Участники ---
    async def list_members(self, project_id: uuid.UUID) -> list[ProjectMember]:
        await self.get(project_id)
        return list(await self.repo.members(project_id))

    async def add_member(self, project_id: uuid.UUID, data: MemberCreate) -> ProjectMember:
        await self.get(project_id)
        if await self.users.get(data.user_id) is None:
            raise NotFoundError("Пользователь не найден")
        existing = await self.repo.members(project_id)
        if any(m.user_id == data.user_id and m.role == data.role for m in existing):
            raise ConflictError("Пользователь уже участвует в этой роли")
        member = ProjectMember(project_id=project_id, **data.model_dump())
        self.session.add(member)
        await self.session.flush()
        return member

    async def remove_member(self, project_id: uuid.UUID, member_id: uuid.UUID) -> None:
        members = await self.repo.members(project_id)
        member = next((m for m in members if m.id == member_id), None)
        if member is None:
            raise NotFoundError("Участник не найден")
        await self.repo.soft_delete(member)

    # --- Вехи ---
    async def add_milestone(
        self, project_id: uuid.UUID, data: MilestoneCreate
    ) -> Milestone:
        await self.get(project_id)
        milestone = Milestone(project_id=project_id, **data.model_dump())
        self.session.add(milestone)
        await self.session.flush()
        return milestone

    async def list_milestones(self, project_id: uuid.UUID) -> list[Milestone]:
        project = await self.get(project_id)
        # milestones — relationship; гарантируем загрузку.
        await self.session.refresh(project, ["milestones"])
        return [m for m in project.milestones if m.deleted_at is None]

    # --- История стадий ---
    async def transitions(self, project_id: uuid.UUID) -> list[ProjectStageTransition]:
        await self.get(project_id)
        return list(await self.repo.transitions(project_id))
