"""API уведомлений текущего пользователя."""

from __future__ import annotations

import uuid

from fastapi import APIRouter

from app.core.deps import CurrentUser, DbSession
from app.schemas.common import Message
from app.schemas.notification import NotificationOut, UnreadCount
from app.services.notification_service import NotificationService

router = APIRouter(prefix="/notifications", tags=["notifications"])


@router.get("", response_model=list[NotificationOut])
async def list_notifications(
    session: DbSession, user: CurrentUser, unread_only: bool = False
) -> list[NotificationOut]:
    items = await NotificationService(session).list_for(user.id, unread_only=unread_only)
    return [NotificationOut.model_validate(n) for n in items]


@router.get("/unread-count", response_model=UnreadCount)
async def unread_count(session: DbSession, user: CurrentUser) -> UnreadCount:
    return UnreadCount(count=await NotificationService(session).unread_count(user.id))


@router.post("/{notification_id}/read", response_model=Message)
async def mark_read(
    notification_id: uuid.UUID, session: DbSession, user: CurrentUser
) -> Message:
    await NotificationService(session).mark_read(user.id, notification_id)
    return Message(message="Отмечено прочитанным")


@router.post("/read-all", response_model=Message)
async def mark_all_read(session: DbSession, user: CurrentUser) -> Message:
    n = await NotificationService(session).mark_all_read(user.id)
    return Message(message=f"Прочитано: {n}")
