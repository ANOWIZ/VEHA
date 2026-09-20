"""API трудозатрат: недельная сетка, ввод, отправка, утверждение, сторно."""

from __future__ import annotations

import uuid
from datetime import date
from typing import Annotated

from fastapi import APIRouter, Depends, Query

from app.core.deps import CurrentUser, DbSession, require_role
from app.models.enums import Role
from app.models.user import User
from app.repositories.project_repo import ProjectRepository
from app.repositories.user_repo import UserRepository
from app.schemas.common import Message
from app.schemas.timesheet import (
    ApprovalDecision,
    CopyWeekRequest,
    PendingEntry,
    SubmitWeekRequest,
    TimeEntryCreate,
    TimeEntryOut,
    TimeEntryUpdate,
    WeekResponse,
)
from app.services.timesheet_service import TimesheetService

router = APIRouter(prefix="/timesheets", tags=["timesheets"])

_approver = require_role(Role.PM, Role.ADMIN)


@router.get("/week", response_model=WeekResponse)
async def get_week(
    session: DbSession,
    user: CurrentUser,
    week_start: date | None = Query(default=None, description="Любая дата недели"),
) -> WeekResponse:
    data = await TimesheetService(session).get_week(user, week_start or date.today())
    return WeekResponse(
        week_start=data["week_start"],
        week_end=data["week_end"],
        status=data["status"],
        entries=[TimeEntryOut.model_validate(e) for e in data["entries"]],
        total_hours=data["total_hours"],
        daily_totals=data["daily_totals"],
    )


@router.post("/entries", response_model=TimeEntryOut, status_code=201)
async def create_entry(
    body: TimeEntryCreate, session: DbSession, user: CurrentUser
) -> TimeEntryOut:
    entry = await TimesheetService(session).create_entry(user, body)
    return TimeEntryOut.model_validate(entry)


@router.patch("/entries/{entry_id}", response_model=TimeEntryOut)
async def update_entry(
    entry_id: uuid.UUID, body: TimeEntryUpdate, session: DbSession, user: CurrentUser
) -> TimeEntryOut:
    entry = await TimesheetService(session).update_entry(user, entry_id, body)
    return TimeEntryOut.model_validate(entry)


@router.delete("/entries/{entry_id}", response_model=Message)
async def delete_entry(
    entry_id: uuid.UUID, session: DbSession, user: CurrentUser
) -> Message:
    await TimesheetService(session).delete_entry(user, entry_id)
    return Message(message="Запись удалена")


@router.post("/submit", response_model=Message)
async def submit_week(
    body: SubmitWeekRequest, session: DbSession, user: CurrentUser
) -> Message:
    n = await TimesheetService(session).submit_week(user, body.week_start)
    return Message(message=f"Отправлено на утверждение записей: {n}")


@router.post("/copy", response_model=Message)
async def copy_week(
    body: CopyWeekRequest, session: DbSession, user: CurrentUser
) -> Message:
    n = await TimesheetService(session).copy_week(
        user, body.source_week_start, body.target_week_start
    )
    return Message(message=f"Скопировано записей: {n}")


# ---------- Утверждение (РП / admin) ----------
@router.get("/pending", response_model=list[PendingEntry])
async def pending(
    session: DbSession, user: Annotated[User, Depends(_approver)]
) -> list[PendingEntry]:
    svc = TimesheetService(session)
    entries = await svc.pending_for(user)
    # Обогащаем именами пользователей и кодами проектов для списка утверждения.
    users_repo = UserRepository(session)
    proj_repo = ProjectRepository(session)
    user_cache: dict[uuid.UUID, str] = {}
    proj_cache: dict[uuid.UUID, str] = {}
    result: list[PendingEntry] = []
    for e in entries:
        if e.user_id not in user_cache:
            u = await users_repo.get(e.user_id)
            user_cache[e.user_id] = u.full_name if u else "—"
        if e.project_id not in proj_cache:
            p = await proj_repo.get(e.project_id)
            proj_cache[e.project_id] = p.code if p else "—"
        item = PendingEntry.model_validate(e)
        item.user_name = user_cache[e.user_id]
        item.project_code = proj_cache[e.project_id]
        result.append(item)
    return result


@router.post("/approve", response_model=Message)
async def approve(
    body: ApprovalDecision, session: DbSession, user: Annotated[User, Depends(_approver)]
) -> Message:
    n = await TimesheetService(session).approve(user, body.entry_ids)
    return Message(message=f"Утверждено записей: {n}")


@router.post("/reject", response_model=Message)
async def reject(
    body: ApprovalDecision, session: DbSession, user: Annotated[User, Depends(_approver)]
) -> Message:
    n = await TimesheetService(session).reject(user, body.entry_ids, body.reason)
    return Message(message=f"Отклонено записей: {n}")


@router.post("/entries/{entry_id}/reverse", response_model=TimeEntryOut)
async def reverse_entry(
    entry_id: uuid.UUID, session: DbSession, user: Annotated[User, Depends(_approver)]
) -> TimeEntryOut:
    entry = await TimesheetService(session).reverse(user, entry_id)
    return TimeEntryOut.model_validate(entry)
