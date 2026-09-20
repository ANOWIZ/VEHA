"""Репозиторий ресурсного планирования (план загрузки пользователь×проект×неделя)."""

from __future__ import annotations

import uuid
from collections.abc import Sequence
from datetime import date

from sqlalchemy import select

from app.models.resource import ResourcePlan
from app.repositories.base import BaseRepository


class ResourceRepository(BaseRepository[ResourcePlan]):
    model = ResourcePlan

    async def get_cell(
        self, user_id: uuid.UUID, project_id: uuid.UUID, week_start: date
    ) -> ResourcePlan | None:
        stmt = select(ResourcePlan).where(
            ResourcePlan.user_id == user_id,
            ResourcePlan.project_id == project_id,
            ResourcePlan.week_start == week_start,
            ResourcePlan.deleted_at.is_(None),
        )
        return (await self.session.execute(stmt)).scalar_one_or_none()

    async def list_range(
        self, week_from: date, week_to: date, *, project_id: uuid.UUID | None = None
    ) -> Sequence[ResourcePlan]:
        stmt = select(ResourcePlan).where(
            ResourcePlan.week_start >= week_from,
            ResourcePlan.week_start <= week_to,
            ResourcePlan.deleted_at.is_(None),
        )
        if project_id is not None:
            stmt = stmt.where(ResourcePlan.project_id == project_id)
        return (await self.session.execute(stmt)).scalars().all()
