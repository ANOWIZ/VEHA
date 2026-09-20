"""Клиент Keycloak Admin API: синхронизация/деактивация пользователей.

Через service-account клиента ``psa-backend`` (client_credentials). Используется
ночной задачей деактивации уволенных. Без доступа к KC задачи деградируют мягко.
"""

from __future__ import annotations

import httpx

from app.core.config import settings


class KeycloakAdminClient:
    def __init__(self) -> None:
        self.base = settings.keycloak_server_url
        self.realm = settings.keycloak_realm

    async def _token(self, client: httpx.AsyncClient) -> str:
        resp = await client.post(
            f"{self.base}/realms/{self.realm}/protocol/openid-connect/token",
            data={
                "grant_type": "client_credentials",
                "client_id": settings.keycloak_client_id,
                "client_secret": settings.keycloak_client_secret,
            },
        )
        resp.raise_for_status()
        return resp.json()["access_token"]

    async def list_active_usernames(self) -> set[str]:
        async with httpx.AsyncClient(timeout=15.0) as client:
            token = await self._token(client)
            usernames: set[str] = set()
            first = 0
            page = 100
            while True:
                resp = await client.get(
                    f"{self.base}/admin/realms/{self.realm}/users",
                    params={"first": first, "max": page, "enabled": "true"},
                    headers={"Authorization": f"Bearer {token}"},
                )
                resp.raise_for_status()
                batch = resp.json()
                if not batch:
                    break
                usernames.update(u["username"] for u in batch if u.get("enabled", True))
                if len(batch) < page:
                    break
                first += page
            return usernames
