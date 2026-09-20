import { useState } from "react";

import { ACTION_LABELS, useAudit } from "@/entities/audit/api";
import { formatDateTime } from "@/shared/lib/format";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { EmptyState, PageHeader } from "@/shared/ui/page";
import { PageLoader } from "@/shared/ui/spinner";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/shared/ui/table";

const ENTITIES = [
  { key: "", label: "Все" },
  { key: "Project", label: "Проекты" },
  { key: "Quote", label: "Расчёты" },
  { key: "TimeEntry", label: "Трудозатраты" },
  { key: "ProjectBudget", label: "Бюджеты" },
  { key: "ActualCost", label: "Затраты" },
];

const ACTION_VARIANT: Record<string, "default" | "success" | "warning" | "destructive" | "secondary"> = {
  create: "default",
  update: "secondary",
  delete: "destructive",
  stage_change: "warning",
  approve: "success",
  reject: "destructive",
};

export function AuditLogPage() {
  const [entity, setEntity] = useState("");
  const { data, isLoading } = useAudit(entity || undefined);

  return (
    <div>
      <PageHeader
        title="Аудит-лог"
        description="Кто, что и когда менял: проекты, расчёты, трудозатраты, ставки, финансы"
      />
      <div className="mb-4 flex flex-wrap gap-1">
        {ENTITIES.map((e) => (
          <Button
            key={e.key}
            size="sm"
            variant={entity === e.key ? "default" : "outline"}
            onClick={() => setEntity(e.key)}
          >
            {e.label}
          </Button>
        ))}
      </div>
      {isLoading ? (
        <PageLoader />
      ) : !data || data.items.length === 0 ? (
        <EmptyState title="Записей аудита нет" />
      ) : (
        <div className="rounded-lg border bg-card">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Дата/время</TableHead>
                <TableHead>Кто</TableHead>
                <TableHead>Объект</TableHead>
                <TableHead>Действие</TableHead>
                <TableHead>Изменения</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.items.map((a) => (
                <TableRow key={a.id}>
                  <TableCell className="whitespace-nowrap text-sm text-muted-foreground tabular">
                    {formatDateTime(a.created_at)}
                  </TableCell>
                  <TableCell className="text-sm">{a.actor_name ?? "—"}</TableCell>
                  <TableCell className="text-sm">{a.entity}</TableCell>
                  <TableCell>
                    <Badge variant={ACTION_VARIANT[a.action] ?? "secondary"}>
                      {ACTION_LABELS[a.action] ?? a.action}
                    </Badge>
                  </TableCell>
                  <TableCell className="max-w-md truncate font-mono text-xs text-muted-foreground">
                    {Object.keys(a.diff).length > 0 ? JSON.stringify(a.diff) : "—"}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </div>
  );
}
