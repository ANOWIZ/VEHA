"""Реестр рисков проекта.

Каждый риск оценивается по матрице вероятность×влияние (1–3 каждый), итоговый
балл (score = вероятность × влияние, 1–9) определяет уровень критичности. Балл
хранится денормализованно и пересчитывается сервисом при изменении оценки —
это позволяет агрегировать портфель рисков set-based-запросами без N+1.
"""

from __future__ import annotations

import uuid
from datetime import date, datetime

from sqlalchemy import (
    CheckConstraint,
    Date,
    DateTime,
    ForeignKey,
    SmallInteger,
    String,
    Text,
)
from sqlalchemy.dialects.postgresql import UUID as PG_UUID
from sqlalchemy.orm import Mapped, mapped_column

from app.db.base import DomainBase
from app.models.enums import RiskCategory, RiskResponse, RiskStatus


class Risk(DomainBase):
    __tablename__ = "risks"
    __table_args__ = (
        CheckConstraint("probability BETWEEN 1 AND 3", name="probability_range"),
        CheckConstraint("impact BETWEEN 1 AND 3", name="impact_range"),
    )

    project_id: Mapped[uuid.UUID] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("projects.id", ondelete="CASCADE"), index=True
    )
    title: Mapped[str] = mapped_column(String(255))
    description: Mapped[str | None] = mapped_column(Text)
    category: Mapped[RiskCategory] = mapped_column(String(20), default=RiskCategory.TECHNICAL)

    # Оценка по матрице 1–3; score = probability × impact (1–9), денормализован.
    probability: Mapped[int] = mapped_column(SmallInteger, default=1)
    impact: Mapped[int] = mapped_column(SmallInteger, default=1)
    score: Mapped[int] = mapped_column(SmallInteger, default=1, index=True)

    status: Mapped[RiskStatus] = mapped_column(
        String(16), default=RiskStatus.OPEN, index=True
    )
    response_strategy: Mapped[RiskResponse] = mapped_column(
        String(16), default=RiskResponse.MITIGATE
    )
    mitigation_plan: Mapped[str | None] = mapped_column(Text)

    # Ответственный за риск со стороны команды (внутренний пользователь).
    owner_id: Mapped[uuid.UUID | None] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("users.id")
    )
    due_date: Mapped[date | None] = mapped_column(Date)
    closed_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))
    created_by: Mapped[uuid.UUID | None] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("users.id")
    )
