"""Хранилище артефактов портала Заказчика: локальный диск или S3/MinIO.

Бэкенд выбирается настройкой ``storage_backend``. ``Artifact.storage_path``
хранит ссылку на объект: для local — путь на диске, для s3 — ключ объекта.
boto3 импортируется лениво — при backend=local зависимость не требуется."""

from __future__ import annotations

import re
import uuid
from pathlib import Path
from typing import Protocol
from urllib.parse import quote

import anyio

from app.core.config import settings

_SAFE = re.compile(r"[^A-Za-z0-9._-]+")


def _safe_name(filename: str) -> str:
    return _SAFE.sub("_", filename) or "file"


def content_disposition_attachment(filename: str) -> str:
    """Безопасный заголовок Content-Disposition для пользовательского имени файла.

    Имя файла контролируется пользователем (загрузка артефактов), поэтому
    кавычки/спецсимволы экранируются: ASCII-фолбэк в quoted-string + RFC 5987
    filename* с процентным кодированием UTF-8. Предотвращает инъекцию в заголовок."""
    ascii_fallback = _safe_name(filename.encode("ascii", "ignore").decode())
    star = quote(filename, safe="")
    return f"attachment; filename=\"{ascii_fallback}\"; filename*=UTF-8''{star}"


class ArtifactStorage(Protocol):
    def save(self, project_id: uuid.UUID, filename: str, data: bytes) -> str: ...
    def read(self, ref: str) -> bytes: ...


class LocalArtifactStorage:
    """Локальный диск (settings.artifacts_dir). Подходит для dev и single-host."""

    def save(self, project_id: uuid.UUID, filename: str, data: bytes) -> str:
        base = Path(settings.artifacts_dir) / str(project_id)
        base.mkdir(parents=True, exist_ok=True)
        stored = base / f"{uuid.uuid4().hex}_{_safe_name(filename)}"
        stored.write_bytes(data)
        return str(stored)

    def read(self, ref: str) -> bytes:
        return Path(ref).read_bytes()


class S3ArtifactStorage:
    """S3-совместимое хранилище (AWS S3 или MinIO через endpoint_url)."""

    def __init__(self) -> None:
        import boto3  # ленивый импорт — нужен только при backend=s3

        self._client = boto3.client(
            "s3",
            endpoint_url=settings.s3_endpoint_url or None,
            region_name=settings.s3_region,
            aws_access_key_id=settings.s3_access_key or None,
            aws_secret_access_key=settings.s3_secret_key or None,
        )
        self._bucket = settings.s3_bucket

    def save(self, project_id: uuid.UUID, filename: str, data: bytes) -> str:
        key = f"{project_id}/{uuid.uuid4().hex}_{_safe_name(filename)}"
        self._client.put_object(Bucket=self._bucket, Key=key, Body=data)
        return key

    def read(self, ref: str) -> bytes:
        obj = self._client.get_object(Bucket=self._bucket, Key=ref)
        return obj["Body"].read()


def get_artifact_storage() -> ArtifactStorage:
    if settings.storage_backend == "s3":
        return S3ArtifactStorage()
    return LocalArtifactStorage()


# Async-обёртки для request-пути: save/read — блокирующий disk/network IO
# (Path.*_bytes, boto3). В async-эндпоинтах выносим их в поток, чтобы не
# блокировать event loop (Celery-путь использует синхронные методы напрямую).
async def save_artifact_async(project_id: uuid.UUID, filename: str, data: bytes) -> str:
    storage = get_artifact_storage()
    return await anyio.to_thread.run_sync(storage.save, project_id, filename, data)


async def read_artifact_async(ref: str) -> bytes:
    storage = get_artifact_storage()
    return await anyio.to_thread.run_sync(storage.read, ref)
