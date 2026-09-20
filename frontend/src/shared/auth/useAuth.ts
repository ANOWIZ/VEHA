import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/shared/api/client";
import type { Role, User } from "@/entities/user/model";
import { env } from "@/shared/config/env";
import { useAuthStore } from "./authStore";
import { keycloakLogoutUrl } from "./oidc";

interface DevLoginResponse {
  access_token: string;
  token_type: string;
}

/** Текущий пользователь (/auth/me). Загружается при наличии токена. */
export function useMe() {
  const token = useAuthStore((s) => s.token);
  return useQuery({
    queryKey: ["me"],
    queryFn: () => api.get<User>("/auth/me"),
    enabled: !!token,
    staleTime: 5 * 60 * 1000,
    retry: false,
  });
}

/** Dev-логин: выпуск локального токена по username+ролям (только dev-режим). */
export function useDevLogin() {
  const setToken = useAuthStore((s) => s.setToken);
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (vars: { username: string; roles: Role[] }) =>
      api.post<DevLoginResponse>("/auth/dev-login", vars),
    onSuccess: (data) => {
      setToken(data.access_token);
      void qc.invalidateQueries({ queryKey: ["me"] });
    },
  });
}

export function useLogout() {
  const logout = useAuthStore((s) => s.logout);
  const qc = useQueryClient();
  return () => {
    logout();
    qc.clear();
    // В production завершаем и сессию Keycloak (SSO single-logout).
    if (!env.authDevMode) {
      window.location.href = keycloakLogoutUrl();
    }
  };
}
