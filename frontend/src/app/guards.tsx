import type { ReactNode } from "react";
import { Navigate, useLocation } from "react-router-dom";

import type { Role } from "@/entities/user/model";
import { hasAnyRole } from "@/entities/user/model";
import { useAuthStore } from "@/shared/auth/authStore";
import { useMe } from "@/shared/auth/useAuth";
import { Button } from "@/shared/ui/button";
import { EmptyState } from "@/shared/ui/page";
import { PageLoader } from "@/shared/ui/spinner";

/** Требует аутентификации; иначе редирект на /login. */
export function RequireAuth({ children }: { children: ReactNode }) {
  const token = useAuthStore((s) => s.token);
  const location = useLocation();
  const { data: me, isLoading, isError, refetch } = useMe();

  if (!token) return <Navigate to="/login" state={{ from: location }} replace />;
  if (isLoading) return <PageLoader />;
  // 401 уже очистил токен в axios-интерсепторе → мы бы вышли по ветке !token выше.
  // Сюда попадаем при сетевой/5xx-ошибке /auth/me: НЕ сбрасываем сессию и не
  // редиректим на /login (иначе LoginPage с живым токеном уводит обратно —
  // петля редиректов), а показываем экран ошибки с возможностью повторить.
  if (isError || !me) {
    return (
      <div className="flex min-h-[60vh] flex-col items-center justify-center gap-4 text-center">
        <EmptyState
          title="Не удалось загрузить профиль"
          description="Проверьте соединение и повторите попытку."
        />
        <Button onClick={() => void refetch()}>Повторить</Button>
      </div>
    );
  }
  return <>{children}</>;
}

/** Требует одну из ролей; иначе показывает «нет доступа». */
export function RequireRole({ roles, children }: { roles: Role[]; children: ReactNode }) {
  const { data: me } = useMe();
  if (!hasAnyRole(me, roles)) {
    return (
      <EmptyState
        title="Недостаточно прав"
        description="У вашей роли нет доступа к этому разделу."
      />
    );
  }
  return <>{children}</>;
}
