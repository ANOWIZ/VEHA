"""Сервис реестра рисков: CRUD с аудитом и портфельная сводка (матрица 3×3).

Балл (score) хранится денормализованно и пересчитывается здесь при изменении
вероятности/влияния. Портфель агрегируется set-based-запросами без N+1."""

from __future__ import annotations

import uuid
from datetime import UTC, datetime

from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.exceptions import NotFoundError
from app.models.enums import (
    ACTIVE_RISK_STATUSES,
    AuditAction,
    RiskCategory,
    RiskStatus,
    Role,
)
from app.models.risk import Risk
from app.models.user import User
from app.repositories.project_repo import ProjectRepository
from app.schemas.risk import RiskCreate, RiskUpdate
from app.services.audit_service import AuditService
from app.services.notification_service import NotificationService
from app.services.risk_rules import HIGH_THRESHOLD, compute_score, risk_level

# Внутренние роли — кому осмысленно слать уведомление о назначении владельцем.
_INTERNAL_ROLES = {Role.ADMIN, Role.DIRECTOR, Role.PM, Role.PRESALE, Role.ENGINEER, Role.FINANCE}


class RiskService:
    def __init__(self, session: AsyncSession) -> None:
        self.session = session
        self.audit = AuditService(session)
        self.notifier = NotificationService(session)
        self.projects = ProjectRepository(session)

    async def list_for_project(
        self, project_id: uuid.UUID, *, status: RiskStatus | None = None
    ) -> list[Risk]:
        stmt = select(Risk).where(
            Risk.project_id == project_id, Risk.deleted_at.is_(None)
        )
        if status is not None:
            stmt = stmt.where(Risk.status == status)
        # Сначала активные и более критичные, затем по дате.
        stmt = stmt.order_by(Risk.score.desc(), Risk.created_at.desc())
        return list((await self.session.execute(stmt)).scalars().all())

    async def get(self, risk_id: uuid.UUID) -> Risk:
        risk = await self.session.get(Risk, risk_id)
        if risk is None or risk.deleted_at is not None:
            raise NotFoundError("Риск не найден")
        return risk

    async def create(
        self, project_id: uuid.UUID, data: RiskCreate, actor_id: uuid.UUID
    ) -> Risk:
        score = compute_score(data.probability, data.impact)
        risk = Risk(
            project_id=project_id,
            created_by=actor_id,
            score=score,
            **data.model_dump(),
        )
        self.session.add(risk)
        await self.session.flush()
        await self.audit.record(
            entity="Risk",
            entity_id=risk.id,
            action=AuditAction.CREATE,
            actor_id=actor_id,
            diff={"title": risk.title, "score": score, "level": risk_level(score)},
        )
        if risk.owner_id and risk.owner_id != actor_id:
            await self._notify_owner(risk)
        return risk

    async def update(
        self, risk_id: uuid.UUID, data: RiskUpdate, actor_id: uuid.UUID
    ) -> Risk:
        risk = await self.get(risk_id)
        changes = data.model_dump(exclude_unset=True)
        prev_owner = risk.owner_id
        diff: dict = {}
        for field, value in changes.items():
            old = getattr(risk, field)
            if old != value:
                diff[field] = {"from": str(old), "to": str(value)}
                setattr(risk, field, value)

        # Пересчёт балла при изменении вероятности/влияния.
        if "probability" in changes or "impact" in changes:
            new_score = compute_score(risk.probability, risk.impact)
            if new_score != risk.score:
                diff["score"] = {"from": str(risk.score), "to": str(new_score)}
                risk.score = new_score

        # Метка закрытия: проставляется при переходе в closed, снимается при возврате.
        if "status" in changes:
            if risk.status == RiskStatus.CLOSED and risk.closed_at is None:
                risk.closed_at = datetime.now(UTC)
            elif risk.status != RiskStatus.CLOSED:
                risk.closed_at = None

        if diff:
            await self.audit.record(
                entity="Risk",
                entity_id=risk.id,
                action=AuditAction.UPDATE,
                actor_id=actor_id,
                diff=diff,
            )
        await self.session.flush()

        # Уведомить нового владельца, если сменился.
        if risk.owner_id and risk.owner_id != prev_owner and risk.owner_id != actor_id:
            await self._notify_owner(risk)
        return risk

    async def delete(self, risk_id: uuid.UUID, actor_id: uuid.UUID) -> None:
        risk = await self.get(risk_id)
        risk.deleted_at = datetime.now(UTC)
        await self.audit.record(
            entity="Risk",
            entity_id=risk.id,
            action=AuditAction.DELETE,
            actor_id=actor_id,
            diff={"title": risk.title},
        )
        await self.session.flush()

    async def _notify_owner(self, risk: Risk) -> None:
        if risk.owner_id is None:
            return
        owner = await self.session.get(User, risk.owner_id)
        if owner is None or not (_INTERNAL_ROLES & set(owner.roles)):
            return
        await self.notifier.notify(
            risk.owner_id,
            kind="risk_assigned",
            title="Вы назначены ответственным за риск",
            body=risk.title,
            link=f"/projects/{risk.project_id}",
        )

    # ---------- Портфель рисков (set-based) ----------
    async def portfolio(self, user: User) -> dict:
        # Доступные пользователю проекты (РП — свои; admin/director/finance — все).
        projects, _ = await self.projects.list_for_user(user, limit=500)
        by_id = {p.id: p for p in projects}
        ids = list(by_id)
        if not ids:
            return _empty_portfolio()

        stmt = select(
            Risk.project_id, Risk.probability, Risk.impact, Risk.score, Risk.category
        ).where(
            Risk.project_id.in_(ids),
            Risk.status.in_(ACTIVE_RISK_STATUSES),
            Risk.deleted_at.is_(None),
        )
        rows_raw = (await self.session.execute(stmt)).all()

        # Агрегаты.
        matrix: dict[tuple[int, int], int] = {}
        by_category: dict[RiskCategory, int] = {}
        per_project: dict[uuid.UUID, dict[str, int]] = {}
        total_high = 0
        for project_id, probability, impact, score, category in rows_raw:
            matrix[(probability, impact)] = matrix.get((probability, impact), 0) + 1
            cat = RiskCategory(category)
            by_category[cat] = by_category.get(cat, 0) + 1
            agg = per_project.setdefault(
                project_id, {"active_count": 0, "high_count": 0, "top_score": 0}
            )
            agg["active_count"] += 1
            agg["top_score"] = max(agg["top_score"], score)
            if score >= HIGH_THRESHOLD:
                agg["high_count"] += 1
                total_high += 1

        matrix_cells = [
            {
                "probability": p,
                "impact": i,
                "score": p * i,
                "level": risk_level(p * i),
                "count": matrix.get((p, i), 0),
            }
            for p in range(1, 4)
            for i in range(1, 4)
        ]
        rows = [
            {
                "project_id": pid,
                "code": by_id[pid].code,
                "name": by_id[pid].name,
                "stage": by_id[pid].stage,
                "status": by_id[pid].status,
                "manager_id": by_id[pid].manager_id,
                **agg,
            }
            for pid, agg in per_project.items()
            if pid in by_id
        ]
        rows.sort(key=lambda r: (r["top_score"], r["active_count"]), reverse=True)
        return {
            "projects_count": len(rows),
            "total_active": len(rows_raw),
            "total_high": total_high,
            "matrix": matrix_cells,
            "by_category": [
                {"category": c, "count": n}
                for c, n in sorted(by_category.items(), key=lambda kv: kv[1], reverse=True)
            ],
            "rows": rows,
        }

    @staticmethod
    def can_see_portfolio(user: User) -> bool:
        return bool(
            {Role.ADMIN, Role.DIRECTOR, Role.FINANCE, Role.PM} & set(user.roles)
        )


def _empty_portfolio() -> dict:
    return {
        "projects_count": 0,
        "total_active": 0,
        "total_high": 0,
        "matrix": [
            {
                "probability": p,
                "impact": i,
                "score": p * i,
                "level": risk_level(p * i),
                "count": 0,
            }
            for p in range(1, 4)
            for i in range(1, 4)
        ],
        "by_category": [],
        "rows": [],
    }
