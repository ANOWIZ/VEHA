"""Агрегатор роутеров API v1. Новые модули по фазам подключаются здесь."""

from __future__ import annotations

from fastapi import APIRouter

from app.api.v1 import auth, users

api_router = APIRouter()
api_router.include_router(auth.router)
api_router.include_router(users.router)

# Подключаются по мере готовности фаз:
try:  # Фаза 2 — проекты и трудозатраты
    from app.api.v1 import clients, projects, tasks, timesheets

    api_router.include_router(clients.router)
    api_router.include_router(projects.router)
    api_router.include_router(tasks.router)
    api_router.include_router(timesheets.router)
except ImportError:  # pragma: no cover
    pass

try:  # Фаза 3 — калькулятор лицензий (курсы ЦБ не используются)
    from app.api.v1 import catalog, quotes

    api_router.include_router(catalog.router)
    api_router.include_router(quotes.router)
except ImportError:  # pragma: no cover
    pass

try:  # Фаза 4 — финансы и ресурсы
    from app.api.v1 import finance, resources

    api_router.include_router(finance.router)
    api_router.include_router(resources.router)
except ImportError:  # pragma: no cover
    pass

try:  # Фаза 5 — дашборды, аудит/1С, уведомления
    from app.api.v1 import admin, dashboards, notifications

    api_router.include_router(dashboards.router)
    api_router.include_router(admin.router)
    api_router.include_router(notifications.router)
except ImportError:  # pragma: no cover
    pass

try:  # Фаза 6 — портал Заказчика (ожидания/блокеры, артефакты)
    from app.api.v1 import action_items, portal

    api_router.include_router(action_items.router)
    api_router.include_router(portal.router)
except ImportError:  # pragma: no cover
    pass

try:  # Реестр рисков проекта (матрица вероятность×влияние, портфель)
    from app.api.v1 import risks

    api_router.include_router(risks.router)
    api_router.include_router(risks.portfolio_router)
except ImportError:  # pragma: no cover
    pass

try:  # Отчёты и выгрузки (utilization, таймшиты, портфель — XLSX)
    from app.api.v1 import reports

    api_router.include_router(reports.router)
except ImportError:  # pragma: no cover
    pass
