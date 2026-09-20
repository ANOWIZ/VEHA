# syntax=docker/dockerfile:1
# Multi-stage: общая база -> dev (с dev-зависимостями и reload) -> prod (slim).
FROM python:3.12-slim AS base
ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    PIP_NO_CACHE_DIR=1
WORKDIR /app
# uv — менеджер Python-зависимостей.
COPY --from=ghcr.io/astral-sh/uv:latest /uv /uvx /bin/
RUN apt-get update && apt-get install -y --no-install-recommends \
    libpq5 curl && rm -rf /var/lib/apt/lists/*

# ---- dev ----
FROM base AS dev
COPY pyproject.toml ./
RUN uv sync --extra dev || true
COPY . .
RUN uv sync --extra dev
EXPOSE 8000
CMD ["uv", "run", "uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000", "--reload"]

# ---- prod ----
FROM base AS prod
COPY pyproject.toml ./
RUN uv sync --no-dev
COPY . .
RUN uv sync --no-dev
# Непривилегированный пользователь (defense-in-depth: ограничить эскалацию при
# RCE). HOME=/app (gunicorn пишет control-socket в $HOME). Каталог артефактов
# (том) chown'им — при первом монтировании пустой named-volume наследует
# владельца из образа, поэтому загрузка артефактов из портала работает.
RUN useradd --system --uid 10001 --home-dir /app appuser \
    && mkdir -p /app/var/artifacts \
    && chown -R appuser:appuser /app
USER appuser
EXPOSE 8000
CMD ["uv", "run", "gunicorn", "app.main:app", "-k", "uvicorn.workers.UvicornWorker", "-w", "4", "-b", "0.0.0.0:8000"]
