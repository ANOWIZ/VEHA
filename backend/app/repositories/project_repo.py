"""Репозиторий проектов, участников и истории стадий."""

from __future__ import annotations

import uuid
from collections.abc import Sequence

from sqlalchemy import func, or_, select

from app.models.enums import ProjectStatus, Role, Stage
from app.models.project import Project, ProjectMember, ProjectStageTransition
from app.models.user import User
from app.repositories.base import BaseRepository


class ProjectRepository(BaseRepository[Project]):
    model = Project

    async def get_by_code(self, code: str) -> Project | None:
        stmt = select(Project).where(Project.code == code, Project.deleted_at.is_(None))
        return (await self.session.execute(stmt)).scalar_one_or_none()

    async def next_code(self, year: int) -> str:
        """Сгенерировать следующий код PRJ-YYYY-NNN для года.

        От максимального существующего суффикса (а не от count), чтобы при
        удалениях/непоследовательной нумерации не было коллизий с unique-кодом.
        Учитываем и soft-deleted (их коды зарезервированы unique-ограничением)."""
        prefix = f"PRJ-{year}-"
        # Без фильтра deleted_at — коды soft-deleted проектов тоже заняты unique.
        stmt = select(func.max(Project.code)).where(Project.code.like(f"{prefix}%"))
        max_code = (await self.session.execute(stmt)).scalar_one_or_none()
        next_num = 1
        if max_code:
            try:
                next_num = int(max_code.rsplit("-", 1)[-1]) + 1
            except ValueError:
                next_num = 1
        return f"{prefix}{next_num:03d}"

    async def list_for_user(
        self,
        user: User,
        *,
        status: ProjectStatus | None = None,
        stage: Stage | None = None,
        manager_id: uuid.UUID | None = None,
        query: str | None = None,
        limit: int = 50,
        offset: int = 0,
    ) -> tuple[list[Project], int]:
        stmt = select(Project).where(Project.deleted_at.is_(None))

        roles = set(user.roles)
        privileged = {Role.ADMIN, Role.DIRECTOR, Role.FINANCE} & roles
        if not privileged:
            # Свои проекты: РП, куратор или участник.
            member_subq = (
                select(ProjectMember.project_id)
                .where(ProjectMember.user_id == user.id, ProjectMember.deleted_at.is_(None))
                .scalar_subquery()
            )
            stmt = stmt.where(
                or_(
                    Project.manager_id == user.id,
                    Project.curator_id == user.id,
                    Project.id.in_(member_subq),
                )
            )

        if status is not None:
            stmt = stmt.where(Project.status == status)
        if stage is not None:
            stmt = stmt.where(Project.stage == stage)
        if manager_id is not None:
            stmt = stmt.where(Project.manager_id == manager_id)
        if query:
            pattern = f"%{query.lower()}%"
            stmt = stmt.where(
                or_(
                    func.lower(Project.name).like(pattern),
                    func.lower(Project.code).like(pattern),
                )
            )

        count_stmt = select(func.count()).select_from(stmt.subquery())
        total = int((await self.session.execute(count_stmt)).scalar_one())

        stmt = stmt.order_by(Project.created_at.desc()).limit(limit).offset(offset)
        items = list((await self.session.execute(stmt)).scalars().all())
        return items, total

    async def members(self, project_id: uuid.UUID) -> Sequence[ProjectMember]:
        stmt = select(ProjectMember).where(
            ProjectMember.project_id == project_id, ProjectMember.deleted_at.is_(None)
        )
        return (await self.session.execute(stmt)).scalars().all()

    async def transitions(self, project_id: uuid.UUID) -> Sequence[ProjectStageTransition]:
        stmt = (
            select(ProjectStageTransition)
            .where(ProjectStageTransition.project_id == project_id)
            .order_by(ProjectStageTransition.created_at)
        )
        return (await self.session.execute(stmt)).scalars().all()
