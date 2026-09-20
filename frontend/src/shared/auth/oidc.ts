/** OIDC Authorization Code Flow + PKCE для входа через Keycloak.
 *
 * Активен только при `env.authDevMode === false` (production). Демо-режим
 * (вход кнопками) этот код не использует. Без внешних зависимостей — на
 * Web Crypto API. Токен кладётся в общий authStore (как и dev-токен). */

import { env } from "@/shared/config/env";

const VERIFIER_KEY = "veha-pkce-verifier";

function base64url(bytes: Uint8Array): string {
  let s = "";
  for (const b of bytes) s += String.fromCharCode(b);
  return btoa(s).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

function randomVerifier(): string {
  const arr = new Uint8Array(32);
  crypto.getRandomValues(arr);
  return base64url(arr);
}

async function challenge(verifier: string): Promise<string> {
  const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(verifier));
  return base64url(new Uint8Array(digest));
}

function authority(): string {
  return `${env.keycloakUrl}/realms/${env.keycloakRealm}/protocol/openid-connect`;
}

function redirectUri(): string {
  return `${window.location.origin}/login`;
}

/** Старт логина: PKCE-челлендж в sessionStorage и редирект на Keycloak. */
export async function startKeycloakLogin(): Promise<void> {
  const verifier = randomVerifier();
  sessionStorage.setItem(VERIFIER_KEY, verifier);
  const params = new URLSearchParams({
    client_id: env.keycloakClientId,
    redirect_uri: redirectUri(),
    response_type: "code",
    scope: "openid profile email",
    code_challenge: await challenge(verifier),
    code_challenge_method: "S256",
  });
  window.location.assign(`${authority()}/auth?${params.toString()}`);
}

/** Обработать callback (?code=...): обменять код на access_token. null — нет кода. */
export async function completeKeycloakLogin(): Promise<string | null> {
  const url = new URL(window.location.href);
  const code = url.searchParams.get("code");
  if (!code) return null;
  const verifier = sessionStorage.getItem(VERIFIER_KEY);
  sessionStorage.removeItem(VERIFIER_KEY);
  // Убрать code/state из адресной строки (чтобы не переиспользовался).
  window.history.replaceState({}, document.title, redirectUri());
  if (!verifier) return null;
  const resp = await fetch(`${authority()}/token`, {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams({
      grant_type: "authorization_code",
      client_id: env.keycloakClientId,
      code,
      redirect_uri: redirectUri(),
      code_verifier: verifier,
    }),
  });
  if (!resp.ok) return null;
  const data = (await resp.json()) as { access_token?: string };
  return data.access_token ?? null;
}

/** URL завершения сессии Keycloak (для logout в production). */
export function keycloakLogoutUrl(): string {
  const params = new URLSearchParams({
    client_id: env.keycloakClientId,
    post_logout_redirect_uri: redirectUri(),
  });
  return `${authority()}/logout?${params.toString()}`;
}
