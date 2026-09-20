"""Ресурсное планирование: плановые часы по пользователю/проекту/неделе."""

from __future__ import annotations

import uuid
from datetime import date
from decimal import Decimal

from sqlalchemy import Date, ForeignKey, Numeric, UniqueConstraint
from sqlalchemy.dialects.postgresql import UUID as PG_UUID
from sqlalchemy.orm import Mapped, mapped_column

from app.db.base import DomainBase


class ResourcePlan(DomainBase):
    __tablename__ = "resource_plans"
    __table_args__ = (
        UniqueConstraint(
            "user_id", "project_id", "week_start", name="resplan_user_proj_week"
        ),
    )

    user_id: Mapped[uuid.UUID] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("users.id"), index=True
    )
    project_id: Mapped[uuid.UUID] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("projects.id"), index=True
    )
    week_start: Mapped[date] = mapped_column(Date, index=True)  # понедельник
    planned_hours: Mapped[Decimal] = mapped_column(Numeric(5, 2), default=Decimal("0"))
