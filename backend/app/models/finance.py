"""Финансы проекта: бюджет, фактические затраты, прогноз."""

from __future__ import annotations

import uuid
from datetime import date, datetime
from decimal import Decimal

from sqlalchemy import Date, DateTime, ForeignKey, Index, Numeric, String, Text, text
from sqlalchemy.dialects.postgresql import JSONB
from sqlalchemy.dialects.postgresql import UUID as PG_UUID
from sqlalchemy.orm import Mapped, mapped_column

from app.db.base import DomainBase
from app.models.enums import CostCategory, CostSource


class ProjectBudget(DomainBase):
    __tablename__ = "project_budgets"

    project_id: Mapped[uuid.UUID] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("projects.id"), unique=True, index=True
    )
    planned_revenue: Mapped[Decimal] = mapped_column(Numeric(15, 2), default=Decimal("0"))
    # Плановые затраты по категориям: {"payroll": "...", "licenses": "...", ...}
    planned_costs: Mapped[dict] = mapped_column(JSONB, default=dict)


class ActualCost(DomainBase):
    """Фактическая затрата. ФОТ генерируется из approved-таймшитов
    (source=timesheet), закупки/прочее — вручную или из 1С."""

    __tablename__ = "actual_costs"

    # Идемпотентность обмена с 1С на уровне БД: одна активная затрата на
    # (project_id, external_id). Partial — чтобы NULL external_id и сторно
    # (deleted_at) не блокировали повторную загрузку (CLAUDE.md §6).
    __table_args__ = (
        Index(
            "uq_actual_project_external",
            "project_id",
            "external_id",
            unique=True,
            postgresql_where=text("external_id IS NOT NULL AND deleted_at IS NULL"),
        ),
    )

    project_id: Mapped[uuid.UUID] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("projects.id"), index=True
    )
    category: Mapped[CostCategory] = mapped_column(String(32), nullable=False)
    amount: Mapped[Decimal] = mapped_column(Numeric(15, 2), nullable=False)
    source: Mapped[CostSource] = mapped_column(String(16), default=CostSource.MANUAL)
    occurred_on: Mapped[date] = mapped_column(Date, index=True)
    external_id: Mapped[str | None] = mapped_column(String(128), index=True)  # идемпот.
    description: Mapped[str | None] = mapped_column(Text)


class Forecast(DomainBase):
    """Снимок прогноза затрат до завершения. Актуальный — кэшируется в Redis."""

    __tablename__ = "forecasts"

    project_id: Mapped[uuid.UUID] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("projects.id"), index=True
    )
    eac: Mapped[Decimal] = mapped_column(Numeric(15, 2), default=Decimal("0"))  # est at compl
    etc: Mapped[Decimal] = mapped_column(Numeric(15, 2), default=Decimal("0"))  # est to compl
    calculated_at: Mapped[datetime] = mapped_column(DateTime(timezone=True))
