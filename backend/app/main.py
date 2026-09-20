"""Точка входа FastAPI: middleware, обработчики ошибок, подключение роутеров."""

from __future__ import annotations

from collections.abc import AsyncIterator
from contextlib import asynccontextmanager

from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import ORJSONResponse
from sqlalchemy import text
from starlette.exceptions import HTTPException as StarletteHTTPException

from app.api.v1.router import api_router
from app.core.config import settings
from app.core.exceptions import AppError, ErrorCode
from app.core.redis import close_redis, get_redis
from app.db.session import engine


@asynccontextmanager
async def lifespan(_app: FastAPI) -> AsyncIterator[None]:
    yield
    await close_redis()


app = FastAPI(
    title=settings.project_name,
    version="0.1.0",
    default_response_class=ORJSONResponse,
    # Интерактивные доки и схема — только вне production (раскрытие поверхности API).
    openapi_url=None if settings.is_production else f"{settings.api_v1_prefix}/openapi.json",
    docs_url=None if settings.is_production else "/docs",
    redoc_url=None if settings.is_production else "/redoc",
    lifespan=lifespan,
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=settings.cors_origins,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


def _error_payload(code: str, message: str, details: list | None = None) -> dict:
    return {"error": {"code": code, "message": message, "details": details or []}}


@app.exception_handler(AppError)
async def app_error_handler(_request: Request, exc: AppError) -> ORJSONResponse:
    return ORJSONResponse(status_code=exc.status_code, content=exc.to_dict())


@app.exception_handler(RequestValidationError)
async def validation_handler(
    _request: Request, exc: RequestValidationError
) -> ORJSONResponse:
    return ORJSONResponse(
        status_code=422,
        content=_error_payload(
            ErrorCode.VALIDATION, "Ошибка валидации запроса", exc.errors()
        ),
    )


@app.exception_handler(StarletteHTTPException)
async def http_exception_handler(
    _request: Request, exc: StarletteHTTPException
) -> ORJSONResponse:
    code = ErrorCode.NOT_FOUND if exc.status_code == 404 else ErrorCode.INTERNAL
    return ORJSONResponse(
        status_code=exc.status_code,
        content=_error_payload(str(code), str(exc.detail)),
    )


@app.get("/health", tags=["system"])
async def health() -> dict[str, str]:
    """Liveness: процесс жив. Используется healthcheck'ом контейнера."""
    return {"status": "ok", "environment": settings.environment}


@app.get("/health/ready", tags=["system"])
async def health_ready() -> ORJSONResponse:
    """Readiness: проверяет зависимости (БД, Redis). 503 — реплику нельзя в LB.

    Для горизонтального масштабирования: оркестратор/балансировщик направляет
    трафик только в реплики, прошедшие readiness."""
    checks: dict[str, str] = {}
    ok = True
    try:
        async with engine.connect() as conn:
            await conn.execute(text("SELECT 1"))
        checks["database"] = "ok"
    except Exception as exc:  # pragma: no cover - зависит от окружения
        checks["database"] = f"error: {type(exc).__name__}"
        ok = False
    try:
        await get_redis().ping()
        checks["redis"] = "ok"
    except Exception as exc:  # pragma: no cover
        checks["redis"] = f"error: {type(exc).__name__}"
        ok = False
    return ORJSONResponse(
        status_code=200 if ok else 503,
        content={"status": "ready" if ok else "not_ready", "checks": checks},
    )


app.include_router(api_router, prefix=settings.api_v1_prefix)
