"""Реэкспорт всех моделей. Импорт этого модуля регистрирует таблицы в
``Base.metadata`` — необходимо для Alembic autogenerate."""

from app.db.base import Base
from app.models.audit import AuditLog, IntegrationLog
from app.models.customer import Artifact, CustomerActionItem
from app.models.directory import Client, PriceListItem, Product, Vendor
from app.models.finance import ActualCost, Forecast, ProjectBudget
from app.models.notification import Notification
from app.models.project import (
    Milestone,
    Project,
    ProjectMember,
    ProjectStageTransition,
    Task,
)
from app.models.quote import Quote, QuoteLine
from app.models.resource import ResourcePlan
from app.models.risk import Risk
from app.models.timesheet import TimeEntry, TimesheetWeek
from app.models.user import User, UserCostRate

__all__ = [
    "ActualCost",
    "Artifact",
    "AuditLog",
    "Base",
    "Client",
    "CustomerActionItem",
    "Forecast",
    "IntegrationLog",
    "Milestone",
    "Notification",
    "PriceListItem",
    "Product",
    "Project",
    "ProjectBudget",
    "ProjectMember",
    "ProjectStageTransition",
    "Quote",
    "QuoteLine",
    "ResourcePlan",
    "Risk",
    "Task",
    "TimeEntry",
    "TimesheetWeek",
    "User",
    "UserCostRate",
    "Vendor",
]
