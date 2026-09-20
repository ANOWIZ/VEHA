"""Схемы клиента (заказчика)."""

from __future__ import annotations

import uuid

from pydantic import BaseModel, Field

from app.schemas.common import ORMModel


# ИНН организации — 10 цифр (юрлицо) или 12 (ИП/физлицо).
_INN_PATTERN = r"^(\d{10}|\d{12})$"


class ClientBase(BaseModel):
    name: str = Field(min_length=1, max_length=255)
    inn: str | None = Field(default=None, max_length=12, pattern=_INN_PATTERN)
    industry: str | None = None
    contacts: dict = Field(default_factory=dict)
    is_kii: bool = False


class ClientCreate(ClientBase):
    pass


class ClientUpdate(BaseModel):
    name: str | None = Field(default=None, max_length=255)
    inn: str | None = Field(default=None, max_length=12, pattern=_INN_PATTERN)
    industry: str | None = None
    contacts: dict | None = None
    is_kii: bool | None = None


class ClientOut(ORMModel, ClientBase):
    id: uuid.UUID
