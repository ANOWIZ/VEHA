"""Интеграционные тесты портала Заказчика: блокеры, provide→accept, RBAC-скоупинг."""

from __future__ import annotations

import uuid

import pytest

from app.core.exceptions import ForbiddenError
from app.models.customer import CustomerActionItem
from app.models.directory import Client
from app.models.enums import CustomerActionStatus, Role
from app.models.user import User
from app.schemas.customer import ActionItemCreate
from app.services.customer_service import CustomerService


async def _client_user(session, client_id: uuid.UUID, name="Контакт Заказчика") -> User:
    u = User(
        username=f"client-{uuid.uuid4().hex[:8]}",
        email=f"{uuid.uuid4().hex[:8]}@client.test",
        full_name=name,
        roles=[Role.CLIENT],
        client_id=client_id,
        is_active=True,
    )
    session.add(u)
    await session.flush()
    return u


async def test_blocker_lifecycle(session, make_user, make_project):
    pm = await make_user([Role.PM], cost_rate=None)
    project = await make_project(pm)
    client_user = await _client_user(session, project.client_id)
    svc = CustomerService(session)

    item = await svc.create_action_item(
        project.id,
        ActionItemCreate(
            title="Предоставить документы",
            responsible_name="Иванов И.И.",
            responsible_user_id=client_user.id,
        ),
        actor_id=pm.id,
    )
    assert item.status == CustomerActionStatus.WAITING

    # Портал Заказчика видит проект как заблокированный на его стороне.
    projects = await svc.portal_projects(client_user)
    assert len(projects) == 1
    assert projects[0]["blocked_on_customer"] is True
    assert projects[0]["open_actions"] == 1

    # Заказчик предоставляет данные → блокер снимается (не WAITING).
    await svc.provide(client_user, item.id, "Готово, файлы приложены")
    await session.refresh(item)
    assert item.status == CustomerActionStatus.PROVIDED
    projects = await svc.portal_projects(client_user)
    assert projects[0]["blocked_on_customer"] is False

    # РП принимает.
    await svc.accept(item.id, actor_id=pm.id)
    await session.refresh(item)
    assert item.status == CustomerActionStatus.ACCEPTED


async def test_client_cannot_access_foreign_project(session, make_user, make_project):
    pm = await make_user([Role.PM], cost_rate=None)
    project = await make_project(pm)
    # Контакт другого клиента.
    other = Client(name="Другой клиент")
    session.add(other)
    await session.flush()
    foreign_client = await _client_user(session, other.id)

    svc = CustomerService(session)
    with pytest.raises(ForbiddenError):
        await svc.portal_project_detail(foreign_client, project.id)


async def test_client_cannot_provide_foreign_item(session, make_user, make_project):
    pm = await make_user([Role.PM], cost_rate=None)
    project = await make_project(pm)
    other = Client(name="Чужой")
    session.add(other)
    await session.flush()
    foreign_client = await _client_user(session, other.id)
    svc = CustomerService(session)
    item = await svc.create_action_item(
        project.id,
        ActionItemCreate(title="x", responsible_name="y"),
        actor_id=pm.id,
    )
    with pytest.raises(ForbiddenError):
        await svc.provide(foreign_client, item.id, None)


async def test_portal_projects_empty_for_unlinked_user(session, make_user, make_project):
    pm = await make_user([Role.PM], cost_rate=None)
    await make_project(pm)
    # client без client_id не видит ничего.
    unlinked = User(
        username=f"c-{uuid.uuid4().hex[:8]}",
        email=f"{uuid.uuid4().hex[:8]}@c.test",
        full_name="No Client",
        roles=[Role.CLIENT],
        is_active=True,
    )
    session.add(unlinked)
    await session.flush()
    assert await CustomerService(session).portal_projects(unlinked) == []
