"""Трудозатраты: записи времени и недельный агрегат."""

from __future__ import annotations

import uuid
from datetime import date, datetime
from decimal import Decimal

from sqlalchemy import (
    Date,
    DateTime,
    ForeignKey,
    Numeric,
    String,
    Text,
    UniqueConstraint,
)
from sqlalchemy.dialects.postgresql import UUID as PG_UUID
from sqlalchemy.orm import Mapped, mapped_column

from app.db.base import DomainBase
from app.models.enums import TimeEntryStatus


class TimeEntry(DomainBase):
    """Списание часов. После утверждения запись блокируется; корректировка —
    сторнирующей записью (``reversal_of``), не редактированием."""

    __tablename__ = "time_entries"

    user_id: Mapped[uuid.UUID] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("users.id"), index=True
    )
    project_id: Mapped[uuid.UUID] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("projects.id"), index=True
    )
    task_id: Mapped[uuid.UUID | None] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("tasks.id")
    )
    work_date: Mapped[date] = mapped_column(Date, index=True, nullable=False)
    hours: Mapped[Decimal] = mapped_column(Numeric(5, 2), nullable=False)
    comment: Mapped[str] = mapped_column(Text, nullable=False)  # комментарий обязателен
    status: Mapped[TimeEntryStatus] = mapped_column(
        String(16), default=TimeEntryStatus.DRAFT, index=True
    )

    approved_by: Mapped[uuid.UUID | None] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("users.id")
    )
    approved_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))
    reject_reason: Mapped[str | None] = mapped_column(Text)

    # Снимок себестоимости часа на дату записи (фиксируется при утверждении).
    cost_rate_snapshot: Mapped[Decimal | None] = mapped_column(Numeric(15, 2))

    # Сторно: ссылка на исходную утверждённую запись.
    reversal_of: Mapped[uuid.UUID | None] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("time_entries.id")
    )


class TimesheetWeek(DomainBase):
    """Агрегат недели пользователя для пакетной отправки/утверждения."""

    __tablename__ = "timesheet_weeks"
    __table_args__ = (
        UniqueConstraint("user_id", "week_start", name="tsweek_user_week"),
    )

    user_id: Mapped[uuid.UUID] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("users.id"), index=True
    )
    week_start: Mapped[date] = mapped_column(Date, index=True)  # понедельник
    status: Mapped[TimeEntryStatus] = mapped_column(
        String(16), default=TimeEntryStatus.DRAFT
    )
