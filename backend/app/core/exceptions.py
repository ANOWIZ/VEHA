"""Единый формат ошибок API: {"error": {"code", "message", "details": [...]}}.

Коды ошибок — enum (см. CLAUDE.md §7). Доменные ошибки наследуются от
``AppError`` и автоматически конвертируются в ответ нужного формата
обработчиком в ``app.main``.
"""

from __future__ import annotations

from enum import StrEnum
from typing import Any


class ErrorCode(StrEnum):
    # generic
    INTERNAL = "internal_error"
    VALIDATION = "validation_error"
    NOT_FOUND = "not_found"
    CONFLICT = "conflict"
    # auth
    UNAUTHENTICATED = "unauthenticated"
    FORBIDDEN = "forbidden"
    # домен: проекты / стадии
    INVALID_STAGE_TRANSITION = "invalid_stage_transition"
    PROJECT_CLOSED = "project_closed"
    # домен: трудозатраты
    TIMESHEET_LOCKED = "timesheet_locked"
    TIMESHEET_INVALID_HOURS = "timesheet_invalid_hours"
    TIMESHEET_DAILY_LIMIT = "timesheet_daily_limit_exceeded"
    NOT_APPROVER = "not_approver"
    # домен: калькулятор
    QUOTE_LOCKED = "quote_locked"
    PRICE_NOT_FOUND = "price_not_found"
    RATE_NOT_FOUND = "currency_rate_not_found"


class AppError(Exception):
    """Базовая доменная ошибка. ``status_code`` — соответствующий HTTP-код."""

    status_code: int = 400
    code: ErrorCode = ErrorCode.VALIDATION

    def __init__(
        self,
        message: str,
        *,
        code: ErrorCode | None = None,
        status_code: int | None = None,
        details: list[Any] | None = None,
    ) -> None:
        super().__init__(message)
        self.message = message
        if code is not None:
            self.code = code
        if status_code is not None:
            self.status_code = status_code
        self.details = details or []

    def to_dict(self) -> dict[str, Any]:
        return {
            "error": {
                "code": str(self.code),
                "message": self.message,
                "details": self.details,
            }
        }


class NotFoundError(AppError):
    status_code = 404
    code = ErrorCode.NOT_FOUND


class ConflictError(AppError):
    status_code = 409
    code = ErrorCode.CONFLICT


class ValidationError(AppError):
    status_code = 422
    code = ErrorCode.VALIDATION


class UnauthenticatedError(AppError):
    status_code = 401
    code = ErrorCode.UNAUTHENTICATED


class ForbiddenError(AppError):
    status_code = 403
    code = ErrorCode.FORBIDDEN
