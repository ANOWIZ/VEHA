"""Проекты: проект, история стадий, участники, задачи, вехи."""

from __future__ import annotations

import uuid
from datetime import date
from decimal import Decimal

from sqlalchemy import Boolean, Date, ForeignKey, Numeric, String, Text
from sqlalchemy.dialects.postgresql import UUID as PG_UUID
from sqlalchemy.orm import Mapped, mapped_column, relationship

from app.db.base import DomainBase
from app.models.enums import (
    ProjectMemberRole,
    ProjectStatus,
    ProjectType,
    Stage,
    TaskStatus,
)


class Project(DomainBase):
    __tablename__ = "projects"

    code: Mapped[str] = mapped_column(String(20), unique=True, index=True)  # PRJ-YYYY-NNN
    name: Mapped[str] = mapped_column(String(255), index=True)
    client_id: Mapped[uuid.UUID] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("clients.id"), index=True
    )
    type: Mapped[ProjectType] = mapped_column(String(32), nullable=False)
    manager_id: Mapped[uuid.UUID] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("users.id"), index=True
    )
    curator_id: Mapped[uuid.UUID | None] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("users.id")
    )
    stage: Mapped[Stage] = mapped_column(String(32), default=Stage.PRESALE, nullable=False)
    status: Mapped[ProjectStatus] = mapped_column(
        String(32), default=ProjectStatus.ACTIVE, nullable=False
    )
    planned_start: Mapped[date | None] = mapped_column(Date)
    planned_end: Mapped[date | None] = mapped_column(Date)
    actual_start: Mapped[date | None] = mapped_column(Date)
    actual_end: Mapped[date | None] = mapped_column(Date)
    budget_revenue: Mapped[Decimal] = mapped_column(Numeric(15, 2), default=Decimal("0"))
    contract_ref: Mapped[str | None] = mapped_column(String(100))

    members: Mapped[list[ProjectMember]] = relationship(
        back_populates="project", cascade="all, delete-orphan"
    )
    tasks: Mapped[list[Task]] = relationship(
        back_populates="project", cascade="all, delete-orphan"
    )
    milestones: Mapped[list[Milestone]] = relationship(
        back_populates="project", cascade="all, delete-orphan"
    )
    stage_transitions: Mapped[list[ProjectStageTransition]] = relationship(
        back_populates="project", cascade="all, delete-orphan"
    )


class ProjectStageTransition(DomainBase):
    """История переходов по стадиям (вперёд на 1 / откат на 1 с причиной)."""

    __tablename__ = "project_stage_transitions"

    project_id: Mapped[uuid.UUID] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("projects.id", ondelete="CASCADE"), index=True
    )
    from_stage: Mapped[Stage | None] = mapped_column(String(32))
    to_stage: Mapped[Stage] = mapped_column(String(32), nullable=False)
    reason: Mapped[str | None] = mapped_column(Text)
    created_by: Mapped[uuid.UUID | None] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("users.id")
    )

    project: Mapped[Project] = relationship(back_populates="stage_transitions")


class ProjectMember(DomainBase):
    __tablename__ = "project_members"

    project_id: Mapped[uuid.UUID] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("projects.id", ondelete="CASCADE"), index=True
    )
    user_id: Mapped[uuid.UUID] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("users.id"), index=True
    )
    role: Mapped[ProjectMemberRole] = mapped_column(String(32), nullable=False)
    bill_rate: Mapped[Decimal] = mapped_column(Numeric(15, 2), default=Decimal("0"))
    period_from: Mapped[date | None] = mapped_column(Date)
    period_to: Mapped[date | None] = mapped_column(Date)

    project: Mapped[Project] = relationship(back_populates="members")


class Task(DomainBase):
    """Иерархия этап→задача. Задача — единица списания трудозатрат."""

    __tablename__ = "tasks"

    project_id: Mapped[uuid.UUID] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("projects.id", ondelete="CASCADE"), index=True
    )
    parent_id: Mapped[uuid.UUID | None] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("tasks.id", ondelete="CASCADE")
    )
    name: Mapped[str] = mapped_column(String(255))
    stage: Mapped[Stage | None] = mapped_column(String(32))
    planned_hours: Mapped[Decimal] = mapped_column(Numeric(7, 2), default=Decimal("0"))
    assignee_id: Mapped[uuid.UUID | None] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("users.id")
    )
    status: Mapped[TaskStatus] = mapped_column(String(32), default=TaskStatus.OPEN)
    due_date: Mapped[date | None] = mapped_column(Date)

    project: Mapped[Project] = relationship(back_populates="tasks")
    children: Mapped[list[Task]] = relationship(
        back_populates="parent", cascade="all, delete-orphan"
    )
    parent: Mapped[Task | None] = relationship(
        back_populates="children", remote_side="Task.id"
    )


class Milestone(DomainBase):
    __tablename__ = "milestones"

    project_id: Mapped[uuid.UUID] = mapped_column(
        PG_UUID(as_uuid=True), ForeignKey("projects.id", ondelete="CASCADE"), index=True
    )
    name: Mapped[str] = mapped_column(String(255))
    milestone_date: Mapped[date] = mapped_column(Date, nullable=False)
    is_payment: Mapped[bool] = mapped_column(Boolean, default=False)
    amount: Mapped[Decimal] = mapped_column(Numeric(15, 2), default=Decimal("0"))

    project: Mapped[Project] = relationship(back_populates="milestones")
