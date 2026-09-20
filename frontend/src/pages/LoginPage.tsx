import { useEffect, useState } from "react";
import { Navigate, useNavigate } from "react-router-dom";

import type { Role } from "@/entities/user/model";
import { ROLE_LABELS } from "@/entities/user/model";
import { ApiError } from "@/shared/api/types";
import { useAuthStore } from "@/shared/auth/authStore";
import { completeKeycloakLogin, startKeycloakLogin } from "@/shared/auth/oidc";
import { useDevLogin } from "@/shared/auth/useAuth";
import { env } from "@/shared/config/env";
import { Button } from "@/shared/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/shared/ui/card";
import { Spinner } from "@/shared/ui/spinner";

const DEMO: { username: string; roles: Role[] }[] = [
  { username: "admin.demo", roles: ["admin"] },
  { username: "pm.demo", roles: ["pm"] },
  { username: "eng.demo", roles: ["engineer"] },
  { username: "presale.demo", roles: ["presale"] },
  { username: "finance.demo", roles: ["finance"] },
  { username: "director.demo", roles: ["director"] },
  { username: "client.demo", roles: ["client"] },
];

export function LoginPage() {
  const navigate = useNavigate();
  const token = useAuthStore((s) => s.token);
  const setToken = useAuthStore((s) => s.setToken);
  const devLogin = useDevLogin();
  const [kcError, setKcError] = useState(false);

  // Production: обработка callback от Keycloak (?code=...) при возврате на /login.
  useEffect(() => {
    if (env.authDevMode) return;
    completeKeycloakLogin()
      .then((accessToken) => {
        if (accessToken) setToken(accessToken);
      })
      .catch(() => setKcError(true));
  }, [setToken]);

  // Редирект уже авторизованного пользователя — через <Navigate>, а не вызовом
  // navigate() в теле рендера (side-effect во время render → предупреждение React
  // и возможные двойные навигации).
  if (token) {
    return <Navigate to="/" replace />;
  }

  const submit = async (u: string, r: Role) => {
    try {
      await devLogin.mutateAsync({ username: u, roles: [r] });
      navigate("/", { replace: true });
    } catch {
      /* ошибка показывается ниже */
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-gradient-to-br from-background to-secondary p-4">
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle className="text-xl">Веха — управление проектами</CardTitle>
          <CardDescription>
            {env.authDevMode
              ? "Демо: организации, пользователи и суммы вымышлены. Выберите роль для входа."
              : "Вход через корпоративную учётную запись."}
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-5">
          {env.authDevMode ? (
            <div className="space-y-2">
              {DEMO.map((d) => (
                <Button
                  key={d.username}
                  className="w-full justify-center"
                  variant="outline"
                  disabled={devLogin.isPending}
                  onClick={() => submit(d.username, d.roles[0])}
                >
                  {devLogin.isPending && <Spinner />} {ROLE_LABELS[d.roles[0]]}
                </Button>
              ))}
            </div>
          ) : (
            <Button
              className="w-full"
              onClick={() => {
                setKcError(false);
                void startKeycloakLogin();
              }}
            >
              Войти через Keycloak
            </Button>
          )}

          {kcError && (
            <p className="text-sm text-destructive">
              Не удалось завершить вход через Keycloak. Попробуйте ещё раз.
            </p>
          )}

          {devLogin.isError && (
            <p className="text-sm text-destructive">
              {devLogin.error instanceof ApiError
                ? devLogin.error.message
                : "Не удалось войти"}
            </p>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
