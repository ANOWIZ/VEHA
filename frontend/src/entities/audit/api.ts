import { useQuery } from "@tanstack/react-query";

import { api } from "@/shared/api/client";
import type { Page } from "@/shared/api/types";

export interface AuditEntry {
  id: string;
  entity: string;
  entity_id: string | null;
  action: string;
  actor_id: string | null;
  actor_name: string | null;
  diff: Record<string, unknown>;
  created_at: string;
}

export const ACTION_LABELS: Record<string, string> = {
  create: "Создание",
  update: "Изменение",
  delete: "Удаление",
  stage_change: "Смена стадии",
  approve: "Утверждение",
  reject: "Отклонение",
};

export function useAudit(entity?: string) {
  return useQuery({
    queryKey: ["audit", entity ?? ""],
    queryFn: () => api.get<Page<AuditEntry>>("/admin/audit", { entity, limit: 200 }),
  });
}
