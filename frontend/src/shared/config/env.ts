/** Конфигурация фронта из Vite env (VITE_*). */
export const env = {
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL ?? "/api/v1",
  // authDevMode=true → демо-вход кнопками; false → реальный Keycloak (OIDC PKCE).
  authDevMode: (import.meta.env.VITE_AUTH_DEV_MODE ?? "true") === "true",
  keycloakUrl: import.meta.env.VITE_KEYCLOAK_URL ?? "http://localhost:8080",
  keycloakRealm: import.meta.env.VITE_KEYCLOAK_REALM ?? "veha",
  keycloakClientId: import.meta.env.VITE_KEYCLOAK_CLIENT_ID ?? "veha-frontend",
} as const;
