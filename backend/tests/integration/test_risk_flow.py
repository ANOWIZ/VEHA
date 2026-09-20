"""Интеграционные тесты реестра рисков: оценка/пересчёт, статусы, портфель, RBAC."""

from __future__ import annotations

import pytest
from app.core.exceptions import NotFoundError
from app.models.enums import RiskCategory, RiskResponse, RiskStatus, Role
from app.schemas.risk import RiskCreate, RiskUpdate
from app.services.risk_service import RiskService


async def test_create_computes_score_and_level(session, make_user, make_project):
    pm = await make_user([Role.PM], cost_rate=None)
    project = await make_project(pm)
    svc = RiskService(session)

    risk = await svc.create(
        project.id,
        RiskCreate(
            title="Срыв сроков миграции БД",
            category=RiskCategory.SCHEDULE,
            probability=3,
            impact=3,
            response_strategy=RiskResponse.MITIGATE,
            owner_id=pm.id,
        ),
        actor_id=pm.id,
    )
    assert risk.score == 9
    assert risk.status == RiskStatus.OPEN
    assert risk.closed_at is None


async def test_update_recomputes_score(session, make_user, make_project):
    pm = await make_user([Role.PM], cost_rate=None)
    project = await make_project(pm)
    svc = RiskService(session)
    risk = await svc.create(
        project.id,
        RiskCreate(title="Риск", probability=1, impact=1),
        actor_id=pm.id,
    )
    assert risk.score == 1

    risk = await svc.update(risk.id, RiskUpdate(probability=3, impact=2), actor_id=pm.id)
    assert risk.score == 6  # пересчитан: 3×2


async def test_close_sets_and_reopen_clears_closed_at(session, make_user, make_project):
    pm = await make_user([Role.PM], cost_rate=None)
    project = await make_project(pm)
    svc = RiskService(session)
    risk = await svc.create(
        project.id, RiskCreate(title="x", probability=2, impact=2), actor_id=pm.id
    )

    risk = await svc.update(risk.id, RiskUpdate(status=RiskStatus.CLOSED), actor_id=pm.id)
    assert risk.status == RiskStatus.CLOSED
    assert risk.closed_at is not None

    risk = await svc.update(risk.id, RiskUpdate(status=RiskStatus.OPEN), actor_id=pm.id)
    assert risk.closed_at is None


async def test_delete_soft_deletes(session, make_user, make_project):
    pm = await make_user([Role.PM], cost_rate=None)
    project = await make_project(pm)
    svc = RiskService(session)
    risk = await svc.create(
        project.id, RiskCreate(title="x", probability=1, impact=2), actor_id=pm.id
    )
    await svc.delete(risk.id, actor_id=pm.id)
    assert await svc.list_for_project(project.id) == []
    with pytest.raises(NotFoundError):
        await svc.get(risk.id)


async def test_portfolio_matrix_and_rows(session, make_user, make_project):
    pm = await make_user([Role.PM], cost_rate=None)
    project = await make_project(pm)
    svc = RiskService(session)

    # Два активных риска (9 и 6) и один закрытый — закрытый не попадает в портфель.
    await svc.create(
        project.id,
        RiskCreate(title="Высокий", category=RiskCategory.BUDGET, probability=3, impact=3),
        actor_id=pm.id,
    )
    await svc.create(
        project.id,
        RiskCreate(title="Тоже высокий", category=RiskCategory.BUDGET, probability=3, impact=2),
        actor_id=pm.id,
    )
    closed = await svc.create(
        project.id,
        RiskCreate(title="Закрытый", probability=1, impact=1),
        actor_id=pm.id,
    )
    await svc.update(closed.id, RiskUpdate(status=RiskStatus.CLOSED), actor_id=pm.id)

    data = await svc.portfolio(pm)
    assert data["total_active"] == 2
    assert data["total_high"] == 2
    assert data["projects_count"] == 1
    assert len(data["matrix"]) == 9  # 3×3
    cell_33 = next(c for c in data["matrix"] if c["probability"] == 3 and c["impact"] == 3)
    assert cell_33["count"] == 1
    assert cell_33["level"] == "high"
    # Категория BUDGET агрегирована (2 риска).
    budget = next(c for c in data["by_category"] if c["category"] == RiskCategory.BUDGET)
    assert budget["count"] == 2
    row = data["rows"][0]
    assert row["project_id"] == project.id
    assert row["active_count"] == 2
    assert row["high_count"] == 2
    assert row["top_score"] == 9


async def test_portfolio_scopes_to_accessible_projects(session, make_user, make_project):
    """РП видит риски только своих проектов; чужой проект в портфель не попадает."""
    pm1 = await make_user([Role.PM], cost_rate=None)
    pm2 = await make_user([Role.PM], cost_rate=None)
    p1 = await make_project(pm1)
    p2 = await make_project(pm2)
    svc = RiskService(session)
    await svc.create(p1.id, RiskCreate(title="r1", probability=2, impact=2), actor_id=pm1.id)
    await svc.create(p2.id, RiskCreate(title="r2", probability=3, impact=3), actor_id=pm2.id)

    data1 = await svc.portfolio(pm1)
    assert {r["project_id"] for r in data1["rows"]} == {p1.id}

    # Директор видит оба проекта.
    director = await make_user([Role.DIRECTOR], cost_rate=None)
    data_dir = await svc.portfolio(director)
    assert {p1.id, p2.id} <= {r["project_id"] for r in data_dir["rows"]}
