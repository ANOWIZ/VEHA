"""Репозиторий финансов: бюджет, фактические затраты, прогнозы."""

from __future__ import annotations

import uuid
from collections.abc import Sequence
from decimal import Decimal

from sqlalchemy import func, select

from app.models.enums import CostCategory, CostSource
from app.models.finance import ActualCost, Forecast, ProjectBudget
from app.repositories.base import BaseRepository


class FinanceRepository(BaseRepository[ProjectBudget]):
    model = ProjectBudget

    async def get_budget(self, project_id: uuid.UUID) -> ProjectBudget | None:
        stmt = select(ProjectBudget).where(
            ProjectBudget.project_id == project_id, ProjectBudget.deleted_at.is_(None)
        )
        return (await self.session.execute(stmt)).scalar_one_or_none()

    async def list_actuals(self, project_id: uuid.UUID) -> Sequence[ActualCost]:
        stmt = (
            select(ActualCost)
            .where(ActualCost.project_id == project_id, ActualCost.deleted_at.is_(None))
            .order_by(ActualCost.occurred_on.desc())
        )
        return (await self.session.execute(stmt)).scalars().all()

    async def actuals_by_category(
        self, project_id: uuid.UUID, *, exclude_source: CostSource | None = None
    ) -> dict[str, Decimal]:
        stmt = (
            select(ActualCost.category, func.coalesce(func.sum(ActualCost.amount), 0))
            .where(ActualCost.project_id == project_id, ActualCost.deleted_at.is_(None))
            .group_by(ActualCost.category)
        )
        if exclude_source is not None:
            stmt = stmt.where(ActualCost.source != exclude_source)
        rows = (await self.session.execute(stmt)).all()
        return {str(cat): Decimal(str(total)) for cat, total in rows}

    async def find_actual_by_external(
        self, project_id: uuid.UUID, external_id: str
    ) -> ActualCost | None:
        # Дедуп в пределах проекта: id документов 1С уникальны по организации/году,
        # один и тот же external_id может встретиться в разных проектах.
        stmt = select(ActualCost).where(
            ActualCost.project_id == project_id,
            ActualCost.external_id == external_id,
            ActualCost.deleted_at.is_(None),
        )
        return (await self.session.execute(stmt)).scalars().first()

    async def latest_forecast(self, project_id: uuid.UUID) -> Forecast | None:
        stmt = (
            select(Forecast)
            .where(Forecast.project_id == project_id, Forecast.deleted_at.is_(None))
            .order_by(Forecast.calculated_at.desc())
            .limit(1)
        )
        return (await self.session.execute(stmt)).scalar_one_or_none()

    # Категория ФОТ исключается из ручных затрат — считается из таймшитов.
    PAYROLL = CostCategory.PAYROLL
