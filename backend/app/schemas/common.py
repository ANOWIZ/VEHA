"""Общие схемы: пагинация, обёртка ошибок, базовые конфиги."""

from __future__ import annotations

from typing import Generic, TypeVar

from pydantic import BaseModel, ConfigDict, Field

T = TypeVar("T")


class ORMModel(BaseModel):
    """Базовая схема ответа, читающая атрибуты ORM-объектов."""

    model_config = ConfigDict(from_attributes=True)


class Page(BaseModel, Generic[T]):
    """Страница серверной пагинации (для списков >100 строк)."""

    items: list[T]
    total: int
    limit: int
    offset: int


class PageParams(BaseModel):
    limit: int = Field(default=50, ge=1, le=500)
    offset: int = Field(default=0, ge=0)


class ErrorBody(BaseModel):
    code: str
    message: str
    details: list = Field(default_factory=list)


class ErrorResponse(BaseModel):
    """Единый формат ошибки API."""

    error: ErrorBody


class Message(BaseModel):
    message: str
