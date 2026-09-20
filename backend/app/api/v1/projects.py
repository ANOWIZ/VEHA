"""API проектов: CRUD, стадии, участники, вехи, история."""

from __future__ import annotations

import uuid
from typing import Annotated

from fastapi import APIRouter, Depends, Query

from app.core.deps import (
    CurrentUser,
    DbSession,
    ProjectAccess,
    ProjectManage,
    can_see_financials,
    require_role,
)
from app.models.enums import ProjectStatus, Role, Stage
from app.models.project import ProjectMember
from app.models.user import User
from app.schemas.common import Message, Page
from app.schemas.project import (
    MemberCreate,
    MemberOut,
    MilestoneCreate,
    MilestoneOut,
    ProjectCreate,
    ProjectDetail,
    ProjectOut,
    ProjectUpdate,
    StageChangeRequest,
    StageTransitionOut,
)
from app.services.project_service import ProjectService

router = APIRouter(prefix="/projects", tags=["projects"])

_create_roles = require_role(Role.ADMIN, Role.PM, Role.PRESALE, Role.DIRECTOR)


def _members_out(members: list[ProjectMember], user: User) -> list[MemberOut]:
    """bill_rate (ставка продажи) — только ролям с доступом к финансам."""
    show_rate = can_see_financials(user)
    result: list[MemberOut] = []
    for m in members:
        out = MemberOut.model_validate(m)
        if not show_rate:
            out.bill_rate = None
        result.append(out)
    return result


@router.get("", response_model=Page[ProjectOut])
async def list_projects(
    session: DbSession,
    user: CurrentUser,
    status: ProjectStatus | None = None,
    stage: Stage | None = None,
    manager_id: uuid.UUID | None = None,
    q: str | None = None,
    limit: Annotated[int, Query(ge=1, le=500)] = 50,
    offset: Annotated[int, Query(ge=0)] = 0,
) -> Page[ProjectOut]:
    items, total = await ProjectService(session).list_for_user(
        user,
        status=status,
        stage=stage,
        manager_id=manager_id,
        query=q,
        limit=limit,
        offset=offset,
    )
    return Page[ProjectOut](
        items=[ProjectOut.model_validate(p) for p in items],
        total=total,
        limit=limit,
        offset=offset,
    )


@router.post("", response_model=ProjectOut, status_code=201)
async def create_project(
    body: ProjectCreate, session: DbSession, user: Annotated[User, Depends(_create_roles)]
) -> ProjectOut:
    project = await ProjectService(session).create(body, actor_id=user.id)
    return ProjectOut.model_validate(project)


@router.get("/{project_id}", response_model=ProjectDetail)
async def get_project(
    project: ProjectAccess, session: DbSession, user: CurrentUser
) -> ProjectDetail:
    svc = ProjectService(session)
    # Строим из ProjectOut (без relationship-полей), затем подставляем явно
    # загруженные коллекции — иначе model_validate(project) дёрнет lazy-загрузку
    # members/milestones вне greenlet (MissingGreenlet в async).
    base = ProjectOut.model_validate(project).model_dump()
    members = _members_out(list(await svc.list_members(project.id)), user)
    milestones = [MilestoneOut.model_validate(m) for m in await svc.list_milestones(project.id)]
    return ProjectDetail(**base, members=members, milestones=milestones)


@router.patch("/{project_id}", response_model=ProjectOut)
async def update_project(
    body: ProjectUpdate, project: ProjectManage, session: DbSession, user: CurrentUser
) -> ProjectOut:
    updated = await ProjectService(session).update(project.id, body, actor_id=user.id)
    return ProjectOut.model_validate(updated)


@router.delete("/{project_id}", response_model=Message)
async def delete_project(
    project: ProjectManage, session: DbSession, user: CurrentUser
) -> Message:
    await ProjectService(session).delete(project.id, actor_id=user.id)
    return Message(message="Проект удалён")


# --- Стадии ---
@router.post("/{project_id}/stage", response_model=ProjectOut)
async def change_stage(
    body: StageChangeRequest, project: ProjectManage, session: DbSession, user: CurrentUser
) -> ProjectOut:
    updated = await ProjectService(session).change_stage(
        project.id, body.to_stage, body.reason, actor_id=user.id
    )
    return ProjectOut.model_validate(updated)


@router.get("/{project_id}/transitions", response_model=list[StageTransitionOut])
async def stage_history(project: ProjectAccess, session: DbSession) -> list[StageTransitionOut]:
    items = await ProjectService(session).transitions(project.id)
    return [StageTransitionOut.model_validate(t) for t in items]


# --- Участники ---
@router.get("/{project_id}/members", response_model=list[MemberOut])
async def list_members(
    project: ProjectAccess, session: DbSession, user: CurrentUser
) -> list[MemberOut]:
    members = await ProjectService(session).list_members(project.id)
    return _members_out(list(members), user)


@router.post("/{project_id}/members", response_model=MemberOut, status_code=201)
async def add_member(
    body: MemberCreate, project: ProjectManage, session: DbSession
) -> MemberOut:
    member = await ProjectService(session).add_member(project.id, body)
    return MemberOut.model_validate(member)


@router.delete("/{project_id}/members/{member_id}", response_model=Message)
async def remove_member(
    member_id: uuid.UUID, project: ProjectManage, session: DbSession
) -> Message:
    await ProjectService(session).remove_member(project.id, member_id)
    return Message(message="Участник удалён")


# --- Вехи ---
@router.get("/{project_id}/milestones", response_model=list[MilestoneOut])
async def list_milestones(project: ProjectAccess, session: DbSession) -> list[MilestoneOut]:
    items = await ProjectService(session).list_milestones(project.id)
    return [MilestoneOut.model_validate(m) for m in items]


@router.post("/{project_id}/milestones", response_model=MilestoneOut, status_code=201)
async def add_milestone(
    body: MilestoneCreate, project: ProjectManage, session: DbSession
) -> MilestoneOut:
    milestone = await ProjectService(session).add_milestone(project.id, body)
    return MilestoneOut.model_validate(milestone)
