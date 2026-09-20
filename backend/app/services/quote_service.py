"""Сервис калькулятора: версионирование расчётов, строки, пересчёт, статусы.

Старые/непустые версии и принятые расчёты — read-only. Корректировка —
новая версия (clone), не редактирование (CLAUDE.md §11)."""

from __future__ import annotations

import uuid
from decimal import Decimal

from sqlalchemy.ext.asyncio import AsyncSession

from app.core.exceptions import AppError, ErrorCode, NotFoundError
from app.models.enums import AuditAction, QuoteStatus
from app.models.quote import Quote, QuoteLine
from app.repositories.quote_repo import QuoteRepository
from app.schemas.quote import QuoteCreate, QuoteLineInput
from app.services import quote_calc as qc
from app.services.audit_service import AuditService


class QuoteService:
    def __init__(self, session: AsyncSession) -> None:
        self.session = session
        self.repo = QuoteRepository(session)
        self.audit = AuditService(session)

    # Машина состояний расчёта. Вперёд из draft/sent — можно (включая прямой
    # accept). Принятый/отклонённый ТКП — ТЕРМИНАЛЬНЫЙ и неизменяемый: переходов
    # из него нет, правка только через новую версию (clone). CLAUDE.md §11.
    _ALLOWED_TRANSITIONS: dict[QuoteStatus, set[QuoteStatus]] = {
        QuoteStatus.DRAFT: {QuoteStatus.SENT, QuoteStatus.ACCEPTED, QuoteStatus.REJECTED},
        QuoteStatus.SENT: {QuoteStatus.ACCEPTED, QuoteStatus.REJECTED, QuoteStatus.DRAFT},
        QuoteStatus.ACCEPTED: set(),
        QuoteStatus.REJECTED: set(),
    }

    async def list_for_project(self, project_id: uuid.UUID) -> list[Quote]:
        return list(await self.repo.list_for_project(project_id))

    async def get(self, quote_id: uuid.UUID, project_id: uuid.UUID | None = None) -> Quote:
        quote = await self.repo.get_with_lines(quote_id, project_id)
        if quote is None:
            raise NotFoundError("Расчёт не найден")
        return quote

    @staticmethod
    def _assert_editable(quote: Quote) -> None:
        if quote.status != QuoteStatus.DRAFT:
            raise AppError(
                "Расчёт не в статусе «черновик» — редактирование запрещено "
                "(создайте новую версию)",
                code=ErrorCode.QUOTE_LOCKED,
                status_code=409,
            )

    async def create(
        self, project_id: uuid.UUID, data: QuoteCreate, actor_id: uuid.UUID
    ) -> Quote:
        version = await self.repo.max_version(project_id) + 1
        quote = await self.repo.create(
            project_id=project_id,
            version=version,
            title=data.title,
            status=QuoteStatus.DRAFT,
            currency_rates_snapshot=data.currency_rates,
            currency_buffer_pct=data.currency_buffer_pct,
            totals={},
        )
        await self.audit.record(
            entity="Quote",
            entity_id=quote.id,
            action=AuditAction.CREATE,
            actor_id=actor_id,
            diff={"version": version},
        )
        await self.session.flush()
        return await self.get(quote.id)

    def _line_to_calc_input(self, line: QuoteLine) -> qc.LineCalcInput:
        return qc.LineCalcInput(
            kind=line.kind,
            qty=line.qty,
            unit_price=line.unit_price,
            unit_cost=line.unit_cost,
            currency=line.currency,
            licensing_model=line.licensing_model,
            term_months=line.term_months,
            support_pct=line.support_pct,
            partner_discount_pct=line.partner_discount_pct,
            client_discount_pct=line.client_discount_pct,
        )

    async def recompute(self, quote: Quote) -> Quote:
        """Пересчитать все строки и итоги; сохранить результаты."""
        rates = {k: Decimal(str(v)) for k, v in (quote.currency_rates_snapshot or {}).items()}
        buffer = quote.currency_buffer_pct or Decimal("0")
        computed: list[tuple[qc.LineCalcInput, qc.LineCalcResult]] = []
        for line in quote.lines:
            calc_in = self._line_to_calc_input(line)
            try:
                res = qc.compute_line(calc_in, rates=rates, buffer_pct=buffer)
            except qc.RateNotFound as exc:
                raise AppError(
                    str(exc), code=ErrorCode.RATE_NOT_FOUND, status_code=422
                ) from exc
            line.cost_amount = res.cost_amount
            line.sell_amount = res.sell_amount
            line.margin = res.margin
            computed.append((calc_in, res))
        quote.totals = qc.compute_totals(computed).as_dict()
        await self.session.flush()
        return quote

    async def add_line(
        self, quote_id: uuid.UUID, data: QuoteLineInput, project_id: uuid.UUID | None = None
    ) -> Quote:
        quote = await self.get(quote_id, project_id)
        self._assert_editable(quote)
        line = QuoteLine(quote_id=quote.id, **data.model_dump())
        self.session.add(line)
        await self.session.flush()
        await self.session.refresh(quote, ["lines"])
        return await self.recompute(quote)

    async def update_line(
        self,
        quote_id: uuid.UUID,
        line_id: uuid.UUID,
        data: QuoteLineInput,
        project_id: uuid.UUID | None = None,
    ) -> Quote:
        quote = await self.get(quote_id, project_id)
        self._assert_editable(quote)
        line = next((ln for ln in quote.lines if ln.id == line_id), None)
        if line is None:
            raise NotFoundError("Строка расчёта не найдена")
        for field, value in data.model_dump().items():
            setattr(line, field, value)
        await self.session.flush()
        return await self.recompute(quote)

    async def delete_line(
        self, quote_id: uuid.UUID, line_id: uuid.UUID, project_id: uuid.UUID | None = None
    ) -> Quote:
        quote = await self.get(quote_id, project_id)
        self._assert_editable(quote)
        line = next((ln for ln in quote.lines if ln.id == line_id), None)
        if line is None:
            raise NotFoundError("Строка расчёта не найдена")
        await self.session.delete(line)
        await self.session.flush()
        await self.session.refresh(quote, ["lines"])
        return await self.recompute(quote)

    async def set_status(
        self,
        quote_id: uuid.UUID,
        status: QuoteStatus,
        actor_id: uuid.UUID,
        project_id: uuid.UUID | None = None,
    ) -> Quote:
        quote = await self.get(quote_id, project_id)
        if status != quote.status and status not in self._ALLOWED_TRANSITIONS[quote.status]:
            raise AppError(
                f"Недопустимый переход статуса расчёта: «{quote.status}» → «{status}». "
                "Принятый/отклонённый расчёт неизменяем — создайте новую версию.",
                code=ErrorCode.QUOTE_LOCKED,
                status_code=409,
            )
        quote.status = status
        await self.audit.record(
            entity="Quote",
            entity_id=quote.id,
            action=AuditAction.UPDATE,
            actor_id=actor_id,
            diff={"status": str(status)},
        )
        await self.session.flush()
        return quote

    async def clone_new_version(
        self, quote_id: uuid.UUID, actor_id: uuid.UUID, project_id: uuid.UUID | None = None
    ) -> Quote:
        """Создать новую черновую версию на основе существующей (копия строк)."""
        src = await self.get(quote_id, project_id)
        version = await self.repo.max_version(src.project_id) + 1
        new = await self.repo.create(
            project_id=src.project_id,
            version=version,
            title=src.title,
            status=QuoteStatus.DRAFT,
            currency_rates_snapshot=dict(src.currency_rates_snapshot or {}),
            currency_buffer_pct=src.currency_buffer_pct,
            totals={},
        )
        for line in src.lines:
            self.session.add(
                QuoteLine(
                    quote_id=new.id,
                    kind=line.kind,
                    name=line.name,
                    product_id=line.product_id,
                    licensing_model=line.licensing_model,
                    metric=line.metric,
                    currency=line.currency,
                    qty=line.qty,
                    unit_price=line.unit_price,
                    unit_cost=line.unit_cost,
                    term_months=line.term_months,
                    support_pct=line.support_pct,
                    partner_discount_pct=line.partner_discount_pct,
                    client_discount_pct=line.client_discount_pct,
                    note=line.note,
                )
            )
        await self.session.flush()
        new = await self.get(new.id)
        await self.audit.record(
            entity="Quote",
            entity_id=new.id,
            action=AuditAction.CREATE,
            actor_id=actor_id,
            diff={"cloned_from": str(src.id), "version": version},
        )
        return await self.recompute(new)
