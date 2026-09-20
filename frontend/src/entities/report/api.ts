import { useQuery } from "@tanstack/react-query";

import { api } from "@/shared/api/client";
import { ApiError, type ApiErrorBody } from "@/shared/api/types";
import { getToken, useAuthStore } from "@/shared/auth/authStore";
import { env } from "@/shared/config/env";

export interface UtilizationRow {
  user_id: string;
  full_name: string;
  department: string | null;
  billable_hours: string;
  capacity_hours: string;
  utilization_pct: string;
  projects_count: number;
}

export interface UtilizationReport {
  date_from: string;
  date_to: string;
  working_days: number;
  capacity_per_user: string;
  users_count: number;
  total_billable_hours: string;
  total_capacity_hours: string;
  avg_utilization_pct: string;
  rows: UtilizationRow[];
}

export function useUtilization(dateFrom: string, dateTo: string, enabled = true) {
  return useQuery({
    queryKey: ["utilization", dateFrom, dateTo],
    queryFn: () =>
      api.get<UtilizationReport>("/reports/utilization", {
        date_from: dateFrom,
        date_to: dateTo,
      }),
    enabled: enabled && !!dateFrom && !!dateTo,
  });
}

/** Скачать XLSX-отчёт (с авторизацией через Bearer).
 *
 * XLSX нельзя тянуть через axios (он отдаёт JSON), поэтому используем fetch, но
 * воспроизводим поведение общего клиента: при 401 — logout, иначе пробрасываем
 * ApiError с серверным сообщением (чтобы вызывающий показал реальную причину). */
export async function downloadReport(path: string, filename: string): Promise<void> {
  const resp = await fetch(`${env.apiBaseUrl}${path}`, {
    headers: { Authorization: `Bearer ${getToken() ?? ""}` },
  });
  if (!resp.ok) {
    if (resp.status === 401) useAuthStore.getState().logout();
    const body = (await resp.json().catch(() => null)) as { error?: ApiErrorBody } | null;
    throw new ApiError(
      resp.status,
      body?.error ?? {
        code: "report_error",
        message: "Не удалось сформировать отчёт",
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

export function utilizationXlsxPath(dateFrom: string, dateTo: string): string {
  return `/reports/utilization/export.xlsx?date_from=${dateFrom}&date_to=${dateTo}`;
}

export function timesheetsXlsxPath(
  dateFrom: string,
  dateTo: string,
  opts: { projectId?: string; status?: string } = {},
): string {
  const params = new URLSearchParams({ date_from: dateFrom, date_to: dateTo });
  if (opts.projectId) params.set("project_id", opts.projectId);
  if (opts.status) params.set("status", opts.status);
  return `/reports/timesheets/export.xlsx?${params.toString()}`;
}

export function portfolioXlsxPath(): string {
  return "/reports/portfolio/export.xlsx";
}
