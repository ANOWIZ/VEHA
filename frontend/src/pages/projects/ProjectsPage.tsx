import { Plus, Search } from "lucide-react";
import { useState } from "react";
import { Link } from "react-router-dom";

import {
  PROJECT_TYPE_LABELS,
  STAGE_LABELS,
  STATUS_LABELS,
  STATUS_VARIANT,
  type ProjectStatus,
} from "@/entities/project/model";
import { useProjects } from "@/entities/project/api";
import { useUserMap } from "@/entities/user/api";
import { useMe } from "@/shared/auth/useAuth";
import { hasAnyRole } from "@/entities/user/model";
import { formatMoney } from "@/shared/lib/format";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Input } from "@/shared/ui/input";
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
import { CreateProjectDialog } from "./CreateProjectDialog";

const STATUSES: (ProjectStatus | "all")[] = ["all", "active", "on_hold", "closed", "cancelled"];

export function ProjectsPage() {
  const { data: me } = useMe();
  const [q, setQ] = useState("");
  const [status, setStatus] = useState<ProjectStatus | "all">("active");
  const [createOpen, setCreateOpen] = useState(false);
  const { data, isLoading } = useProjects({
    q: q || undefined,
    status: status === "all" ? undefined : status,
  });
  const users = useUserMap();
  const canCreate = hasAnyRole(me, ["admin", "pm", "presale", "director"]);

  return (
    <div>
      <PageHeader
        title="Проекты"
        description="Портфель проектов внедрения по стадиям жизненного цикла"
        actions={
          canCreate && (
            <Button onClick={() => setCreateOpen(true)}>
              <Plus className="h-4 w-4" /> Новый проект
            </Button>
          )
        }
      />

      <div className="mb-4 flex flex-wrap items-center gap-3">
        <div className="relative max-w-xs flex-1">
          <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input
            placeholder="Поиск по коду или названию…"
            value={q}
            onChange={(e) => setQ(e.target.value)}
            className="pl-8"
          />
        </div>
        <div className="flex gap-1">
          {STATUSES.map((s) => (
            <Button
              key={s}
              size="sm"
              variant={status === s ? "default" : "outline"}
              onClick={() => setStatus(s)}
            >
              {s === "all" ? "Все" : STATUS_LABELS[s]}
            </Button>
          ))}
        </div>
      </div>

      {isLoading ? (
        <PageLoader />
      ) : !data || data.items.length === 0 ? (
        <EmptyState title="Проектов не найдено" description="Измените фильтры или создайте новый проект." />
      ) : (
        <div className="rounded-lg border bg-card">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Код</TableHead>
                <TableHead>Название</TableHead>
                <TableHead>Тип</TableHead>
                <TableHead>Стадия</TableHead>
                <TableHead>Статус</TableHead>
                <TableHead>РП</TableHead>
                <TableHead className="text-right">Бюджет</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.items.map((p) => (
                <TableRow key={p.id} className="cursor-pointer">
                  <TableCell className="font-mono text-xs">
                    <Link to={`/projects/${p.id}`} className="text-primary hover:underline">
                      {p.code}
                    </Link>
                  </TableCell>
                  <TableCell className="font-medium">
                    <Link to={`/projects/${p.id}`} className="hover:underline">
                      {p.name}
                    </Link>
                  </TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {PROJECT_TYPE_LABELS[p.type]}
                  </TableCell>
                  <TableCell>
                    <Badge variant="default">{STAGE_LABELS[p.stage]}</Badge>
                  </TableCell>
                  <TableCell>
                    <Badge variant={STATUS_VARIANT[p.status]}>{STATUS_LABELS[p.status]}</Badge>
                  </TableCell>
                  <TableCell className="text-sm">
                    {users.get(p.manager_id)?.full_name ?? "—"}
                  </TableCell>
                  <TableCell className="text-right tabular">
                    {formatMoney(p.budget_revenue)}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      <CreateProjectDialog open={createOpen} onOpenChange={setCreateOpen} />
    </div>
  );
}
