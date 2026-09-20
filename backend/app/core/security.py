"""Аутентификация: валидация токенов Keycloak (RS256/JWKS) + dev-fallback.

В production принимаются только подписанные Keycloak access-токены. В dev
(``settings.dev_auth_allowed``) дополнительно поддерживаются:
  * локальные HS256-токены, выпущенные ``POST /auth/dev-login``;
  * заголовок ``X-Dev-User: <username>:<role1,role2>`` для curl/тестов.
"""

from __future__ import annotations

import time
from dataclasses import dataclass, field
from typing import Any

import httpx
from jose import jwt
from jose.exceptions import JWTError

from app.core.config import settings
from app.core.exceptions import UnauthenticatedError

# Кэш JWKS Keycloak в памяти процесса (для prod хватает; TTL небольшой).
_jwks_cache: dict[str, Any] = {"keys": None, "fetched_at": 0.0}
_JWKS_TTL = 3600.0


@dataclass(slots=True)
class Principal:
    """Идентичность из токена (до синка с БД)."""

    subject: str  # keycloak sub / dev username
    username: str
    email: str
    full_name: str
    roles: list[str] = field(default_factory=list)

    def has_any(self, *roles: str) -> bool:
        return any(r in self.roles for r in roles)


def _realm_issuer() -> str:
    return f"{settings.keycloak_server_url}/realms/{settings.keycloak_realm}"


def _jwks_url() -> str:
    return f"{_realm_issuer()}/protocol/openid-connect/certs"


async def _fetch_jwks() -> dict[str, Any]:
    # Async-загрузка: get_principal — горячий путь каждого запроса; синхронный
    # httpx.get блокировал бы event loop на время сетевого round-trip к Keycloak.
    now = time.monotonic()
    if _jwks_cache["keys"] is not None and now - _jwks_cache["fetched_at"] < _JWKS_TTL:
        return _jwks_cache["keys"]
    try:
        async with httpx.AsyncClient(timeout=5.0) as client:
            resp = await client.get(_jwks_url())
            resp.raise_for_status()
    except httpx.HTTPError as exc:  # pragma: no cover - сеть
        raise UnauthenticatedError("Не удалось получить ключи Keycloak (JWKS)") from exc
    _jwks_cache["keys"] = resp.json()
    _jwks_cache["fetched_at"] = now
    return _jwks_cache["keys"]


def _principal_from_claims(claims: dict[str, Any]) -> Principal:
    realm_roles = list(claims.get("realm_access", {}).get("roles", []))
    return Principal(
        subject=str(claims.get("sub", claims.get("preferred_username", ""))),
        username=str(claims.get("preferred_username", claims.get("sub", ""))),
        email=str(claims.get("email", "")),
        full_name=str(claims.get("name", claims.get("preferred_username", ""))),
        roles=realm_roles,
    )


def issue_dev_token(username: str, roles: list[str], email: str = "", name: str = "") -> str:
    """Выпуск локального HS256-токена для dev. В production запрещено."""
    if not settings.dev_auth_allowed:
        raise UnauthenticatedError("Dev-аутентификация отключена")
    now = int(time.time())
    claims = {
        "sub": f"dev:{username}",
        "preferred_username": username,
        "email": email or f"{username}@dev.local",
        "name": name or username,
        "realm_access": {"roles": roles},
        "iat": now,
        "exp": now + 8 * 3600,
        "iss": "psa-dev",
    }
    return jwt.encode(claims, settings.dev_jwt_secret, algorithm="HS256")


async def _decode_keycloak(token: str) -> dict[str, Any]:
    jwks = await _fetch_jwks()
    try:
        return jwt.decode(
            token,
            jwks,
            algorithms=["RS256"],
            audience=settings.keycloak_audience,
            issuer=_realm_issuer(),
            options={"verify_aud": True},
        )
    except JWTError as exc:
        raise UnauthenticatedError("Невалидный токен Keycloak") from exc


def _decode_dev(token: str) -> dict[str, Any] | None:
    """Пытается декодировать локальный dev-токен. None — это не dev-токен.

    Проверяем ``iss="psa-dev"`` — чтобы dev-путь принимал только наши dev-токены,
    а не любой HS256, подписанный тем же секретом для иных целей.
    """
    try:
        return jwt.decode(
            token,
            settings.dev_jwt_secret,
            algorithms=["HS256"],
            issuer="psa-dev",
            options={"verify_aud": False},
        )
    except JWTError:
        return None


async def principal_from_token(token: str) -> Principal:
    if settings.dev_auth_allowed:
        dev_claims = _decode_dev(token)
        if dev_claims is not None:
            return _principal_from_claims(dev_claims)
    return _principal_from_claims(await _decode_keycloak(token))


def principal_from_dev_header(value: str) -> Principal:
    """X-Dev-User: ``username`` или ``username:role1,role2``."""
    if not settings.dev_auth_allowed:
        raise UnauthenticatedError("Dev-аутентификация отключена")
    username, _, roles_part = value.partition(":")
    username = username.strip()
    roles = [r.strip() for r in roles_part.split(",") if r.strip()] or ["engineer"]
    return Principal(
        subject=f"dev:{username}",
        username=username,
        email=f"{username}@dev.local",
        full_name=username,
        roles=roles,
    )
