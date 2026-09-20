"""Сервис ресурсного планирования: план загрузки и тепловая карта.

Норма недели берётся из настроек (``default_week_norm_hours``). Подсветка:
перегрузка >100%, недозагрузка <70% (CLAUDE.md §4 «Ресурсное планирование»)."""

from __future__ import annotations

import uuid
from datetime import date, timedelta
from decimal import Decimal

from sqlalchemy import func, select
from sqlalchemy.dialects.postgresql import insert as pg_insert
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.config import settings
from app.models.resource import ResourcePlan
from app.models.user import User
from app.repositories.resource_repo import ResourceRepository
from app.repositories.user_repo import UserRepository
from app.schemas.resource import ResourcePlanUpsert
from app.services import finance_calc as fc


def _monday(d: date) -> date:
    return d - timedelta(days=d.weekday())


class ResourceService:
    def __init__(self, session: AsyncSession) -> None:
        self.session = session
        self.repo = ResourceRepository(session)
        self.users = UserRepository(session)

    async def upsert_plan(self, data: ResourcePlanUpsert):
        week = _monday(data.week_start)
        # Атомарный upsert: INSERT ... ON CONFLICT DO UPDATE по уникальному ключу
        # (user, project, week). Исключает гонку двух параллельных правок одной
        # ячейки (была check-then-insert → возможен IntegrityError/500).
        stmt = (
            pg_insert(ResourcePlan)
            .values(
                user_id=data.user_id,
                project_id=data.project_id,
                week_start=week,
                planned_hours=data.planned_hours,
            )
            .on_conflict_do_update(
                index_elements=["user_id", "project_id", "week_start"],
                set_={"planned_hours": data.planned_hours, "updated_at": func.now()},
            )
            .returning(ResourcePlan)
        )
        cell = (await self.session.execute(stmt)).scalar_one()
        await self.session.flush()
        return cell

    async def heatmap(self, week_from: date, weeks: int) -> dict:
        start = _monday(week_from)
        week_list = [start + timedelta(weeks=i) for i in range(weeks)]
        week_to = week_list[-1]
        norm = Decimal(str(settings.default_week_norm_hours))

        plans = await self.repo.list_range(start, week_to)
        # Агрегируем часы по (user, week) суммарно по всем проектам.
        agg: dict[uuid.UUID, dict[date, Decimal]] = {}
        for p in plans:
            agg.setdefault(p.user_id, {}).setdefault(p.week_start, Decimal("0"))
            agg[p.user_id][p.week_start] += p.planned_hours

        # Имена пользователей — одним запросом (без N+1 в цикле по сотрудникам).
        user_ids = list(agg.keys())
        users_by_id: dict[uuid.UUID, User] = {}
        if user_ids:
            res = await self.session.execute(select(User).where(User.id.in_(user_ids)))
            users_by_id = {u.id: u for u in res.scalars()}

        rows = []
        for user_id, by_week in agg.items():
            user = users_by_id.get(user_id)
            cells = []
            total = Decimal("0")
            for wk in week_list:
                hours = by_week.get(wk, Decimal("0"))
                total += hours
                util = fc.utilization_pct(hours, norm)
                load = "over" if util > 100 else "under" if util < 70 else "ok"
                cells.append(
                    {
                        "week_start": wk,
                        "planned_hours": hours,
                        "utilization_pct": util,
                        "load": load,
                    }
                )
            rows.append(
                {
                    "user_id": user_id,
                    "user_name": user.full_name if user else "—",
                    "cells": cells,
                    "total_hours": total,
                }
            )
        rows.sort(key=lambda r: r["user_name"])
        return {"weeks": week_list, "norm_hours": norm, "rows": rows}
