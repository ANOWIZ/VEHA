"""Конфигурация приложения. 12-factor: всё через переменные окружения."""

from __future__ import annotations

from decimal import Decimal
from functools import lru_cache
from typing import Literal

from pydantic import Field, computed_field, field_validator, model_validator
from pydantic_settings import BaseSettings, SettingsConfigDict

Environment = Literal["development", "staging", "production"]


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=".env", env_file_encoding="utf-8", extra="ignore", case_sensitive=False
    )

    # --- Общие ---
    environment: Environment = "development"
    project_name: str = "Веха — личный кабинет управления проектами"
    api_v1_prefix: str = "/api/v1"
    backend_cors_origins: str = "http://localhost:5173,http://localhost:3000"
    default_tz: str = "Asia/Yekaterinburg"

    # --- PostgreSQL ---
    postgres_host: str = "localhost"
    postgres_port: int = 5432
    postgres_user: str = "psa"
    postgres_password: str = "psa"
    postgres_db: str = "psa"
    database_url: str | None = None  # явное переопределение DSN (напр. для тестов)

    # --- Redis ---
    redis_host: str = "localhost"
    redis_port: int = 6379
    redis_db: int = 0

    # --- Пул соединений БД (тюнинг под число реплик backend) ---
    # На каждый процесс gunicorn-воркера: pool_size постоянных + max_overflow
    # временных соединений. Суммарно по всем репликам/воркерам должно влезать в
    # postgres max_connections (по умолчанию 100). См. README.md.
    db_pool_size: int = 5
    db_max_overflow: int = 10
    db_pool_timeout: int = 30

    # --- Auth (Keycloak / OIDC) ---
    auth_dev_mode: bool = True
    keycloak_server_url: str = "http://localhost:8080"
    keycloak_realm: str = "psa"
    keycloak_client_id: str = "psa-backend"
    keycloak_client_secret: str = "change-me"
    keycloak_audience: str = "account"
    dev_jwt_secret: str = "dev-only-not-for-production"

    # --- Фоновые задачи ---
    # Если воркер/брокер недоступны (локально, тесты) — отключает постановку
    # задач в очередь, чтобы запросы не блокировались на подключении к Redis.
    tasks_enabled: bool = True

    # --- Портал Заказчика: хранилище артефактов ---
    # storage_backend: "local" (по умолчанию, каталог backend/var) или "s3"
    # (S3-совместимое — AWS S3 или MinIO; на VPS рекомендуется s3).
    storage_backend: Literal["local", "s3"] = "local"
    artifacts_dir: str = "var/artifacts"  # для backend=local
    artifact_max_mb: int = 25
    # S3/MinIO (используется при storage_backend=s3):
    s3_endpoint_url: str = ""           # пусто = AWS; для MinIO напр. http://minio:9000
    s3_region: str = "us-east-1"
    s3_bucket: str = "veha-artifacts"
    s3_access_key: str = ""
    s3_secret_key: str = ""

    # --- Интеграции ---
    onec_base_url: str = ""
    onec_token: str = ""

    # --- Бизнес-настройки по умолчанию ---
    default_currency: str = "RUB"
    default_week_norm_hours: Decimal = Decimal("40")
    currency_buffer_pct: Decimal = Field(default=Decimal("2.0"))

    @field_validator("default_week_norm_hours", "currency_buffer_pct", mode="before")
    @classmethod
    def _to_decimal(cls, v: object) -> Decimal:
        return Decimal(str(v))

    @computed_field  # type: ignore[prop-decorator]
    @property
    def sqlalchemy_database_uri(self) -> str:
        if self.database_url:
            return self.database_url
        return (
            f"postgresql+asyncpg://{self.postgres_user}:{self.postgres_password}"
            f"@{self.postgres_host}:{self.postgres_port}/{self.postgres_db}"
        )

    @computed_field  # type: ignore[prop-decorator]
    @property
    def alembic_database_uri(self) -> str:
        # Alembic работает синхронно — используем psycopg-совместимый драйвер не нужен,
        # но asyncpg тоже умеет через async engine. Здесь оставляем async DSN.
        return self.sqlalchemy_database_uri

    @computed_field  # type: ignore[prop-decorator]
    @property
    def redis_url(self) -> str:
        return f"redis://{self.redis_host}:{self.redis_port}/{self.redis_db}"

    @computed_field  # type: ignore[prop-decorator]
    @property
    def celery_broker_url(self) -> str:
        return self.redis_url

    @computed_field  # type: ignore[prop-decorator]
    @property
    def cors_origins(self) -> list[str]:
        return [o.strip() for o in self.backend_cors_origins.split(",") if o.strip()]

    @computed_field  # type: ignore[prop-decorator]
    @property
    def is_production(self) -> bool:
        return self.environment == "production"

    @computed_field  # type: ignore[prop-decorator]
    @property
    def dev_auth_allowed(self) -> bool:
        """Dev-аутентификация физически запрещена в production вне зависимости от флага."""
        return self.auth_dev_mode and not self.is_production

    @model_validator(mode="after")
    def _enforce_production_safety(self) -> Settings:
        """Fail-fast: в production отказываемся стартовать с небезопасной конфигурацией
        (дефолтные секреты, dev-режим, бесполезный audience, wildcard/пустой CORS).
        Превращает мисконфигурацию в явный отказ старта, а не тихую уязвимость."""
        if self.environment != "production":
            return self
        problems: list[str] = []
        if self.auth_dev_mode:
            problems.append("AUTH_DEV_MODE должен быть false в production")
        if self.keycloak_client_secret in ("", "change-me"):
            problems.append("KEYCLOAK_CLIENT_SECRET не задан (оставлен дефолт 'change-me')")
        if self.dev_jwt_secret == "dev-only-not-for-production":
            problems.append("DEV_JWT_SECRET оставлен публичным дефолтом")
        if self.postgres_password in ("", "psa"):
            problems.append("POSTGRES_PASSWORD оставлен дефолтным/пустым")
        if self.keycloak_audience == "account":
            problems.append(
                "KEYCLOAK_AUDIENCE='account' не ограничивает токены нашим клиентом — "
                "задайте client_id бэкенда (например veha-backend)"
            )
        if not self.cors_origins or any(o == "*" for o in self.cors_origins):
            problems.append("BACKEND_CORS_ORIGINS не должен быть пустым или содержать '*'")
        if problems:
            raise ValueError(
                "Небезопасная конфигурация production:\n  - " + "\n  - ".join(problems)
            )
        return self


@lru_cache
def get_settings() -> Settings:
    return Settings()


settings = get_settings()
