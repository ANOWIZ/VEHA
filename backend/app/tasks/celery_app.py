"""Celery-приложение. Брокер и backend — Redis. Расписание — Celery Beat.

Задачи: пересчёт маржинальности, выгрузки в 1С, деактивация уволенных,
отправка уведомлений. (Курсы ЦБ не используются — валютные расчёты ведутся по
ручному курсу, заданному в версии расчёта.)
"""

from __future__ import annotations

from celery import Celery
from celery.schedules import crontab

from app.core.config import settings

celery_app = Celery(
    "psa",
    broker=settings.celery_broker_url,
    backend=settings.celery_broker_url,
    include=["app.tasks.jobs"],
)

celery_app.conf.update(
    task_serializer="json",
    result_serializer="json",
    accept_content=["json"],
    timezone="UTC",
    enable_utc=True,
    task_track_started=True,
    # Если брокер недоступен — падать быстро, а не блокировать запрос ретраями.
    broker_connection_retry_on_startup=False,
    broker_connection_max_retries=0,
    broker_transport_options={"socket_timeout": 2, "socket_connect_timeout": 2},
)

# Периодические задачи.
celery_app.conf.beat_schedule = {
    "deactivate-left-users-nightly": {
        "task": "app.tasks.jobs.deactivate_left_users",
        "schedule": crontab(hour=2, minute=0),
    },
}
