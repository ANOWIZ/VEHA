import { ApiError, type ApiErrorBody } from "@/shared/api/types";
import { getToken, useAuthStore } from "@/shared/auth/authStore";
import { env } from "@/shared/config/env";

/** Скачать защищённый файл с Bearer-авторизацией (XLSX, артефакты).
 *
 * Браузерная навигация по <a href> НЕ добавляет заголовок Authorization, поэтому
 * защищённые эндпоинты скачивания нужно дёргать через fetch с токеном. При 401 —
 * logout (как в axios-интерсепторе), иначе пробрасываем ApiError с серверным
 * сообщением. `path` — относительно env.apiBaseUrl (без хардкода /api/v1). */
export async function downloadFile(path: string, filename: string): Promise<void> {
  const resp = await fetch(`${env.apiBaseUrl}${path}`, {
    headers: { Authorization: `Bearer ${getToken() ?? ""}` },
  });
  if (!resp.ok) {
    if (resp.status === 401) useAuthStore.getState().logout();
    const body = (await resp.json().catch(() => null)) as { error?: ApiErrorBody } | null;
    throw new ApiError(
      resp.status,
      body?.error ?? {
        code: "download_error",
        message: "Не удалось скачать файл",
        details: [],
      },
    );
  }
  const blob = await resp.blob();
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}
