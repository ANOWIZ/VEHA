import { ChevronLeft, ChevronRight, Plus } from "lucide-react";
import { useState } from "react";

import { useProjects } from "@/entities/project/api";
import { useHeatmap, useUpsertPlan } from "@/entities/resource/api";
import { useUsers } from "@/entities/user/api";
import { ApiError } from "@/shared/api/types";
import { cn } from "@/shared/lib/cn";
import { formatNumber, formatPercent } from "@/shared/lib/format";
import { addDays, mondayOf, toISODate } from "@/shared/lib/week";
import { Button } from "@/shared/ui/button";
import { Input } from "@/shared/ui/input";
import { EmptyState, PageHeader } from "@/shared/ui/page";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/shared/ui/select";
import { PageLoader } from "@/shared/ui/spinner";
import { toast } from "@/shared/ui/toast";

const LOAD_STYLE: Record<string, string> = {
  over: "bg-destructive/15 text-destructive",
  under: "bg-muted text-muted-foreground",
  ok: "bg-success/15 text-success",
};

const WEEKS = 8;

export function ResourcesPage() {
  const [monday, setMonday] = useState(() => mondayOf(new Date()));
  const weekFrom = toISODate(monday);
  const { data: heat, isLoading } = useHeatmap(weekFrom, WEEKS);
  const { data: users } = useUsers();
  const { data: projects } = useProjects({ limit: 200 });
  const upsert = useUpsertPlan();

  const [userId, setUserId] = useState("");
  const [projectId, setProjectId] = useState("");
  const [week, setWeek] = useState(weekFrom);
  const [hours, setHours] = useState("");

  const addPlan = async () => {
    if (!userId || !projectId || !hours) {
      toast.error("Заполните сотрудника, проект и часы");
      return;
    }
    try {
      await upsert.mutateAsync({
        user_id: userId,
        project_id: projectId,
        week_start: week,
        planned_hours: hours,
      });
      toast.success("План загрузки сохранён");
      setHours("");
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Ошибка");
    }
  };

  const fmtWeek = (iso: string) => {
    const d = new Date(iso);
    return `${d.getDate()}.${String(d.getMonth() + 1).padStart(2, "0")}`;
  };

  return (
    <div>
      <PageHeader
        title="Ресурсное планирование"
        description={`Тепловая карта загрузки. Норма недели — ${heat?.norm_hours ?? 40} ч. Подсветка: перегрузка >100%, недозагрузка <70%.`}
      />

      <div className="mb-4 flex items-center gap-3">
        <Button variant="outline" size="icon" onClick={() => setMonday(addDays(monday, -7 * WEEKS))}>
          <ChevronLeft className="h-4 w-4" />
        </Button>
        <span className="text-sm text-muted-foreground">
          с недели {fmtWeek(weekFrom)} · {WEEKS} недель
        </span>
        <Button variant="outline" size="icon" onClick={() => setMonday(addDays(monday, 7 * WEEKS))}>
          <ChevronRight className="h-4 w-4" />
        </Button>
        <Button variant="ghost" size="sm" onClick={() => setMonday(mondayOf(new Date()))}>
          Сегодня
        </Button>
      </div>

      {isLoading ? (
        <PageLoader />
      ) : !heat || heat.rows.length === 0 ? (
        <EmptyState
          title="Нет плановой загрузки"
          description="Добавьте план загрузки сотрудника ниже, чтобы он появился на карте."
        />
      ) : (
        <div className="overflow-x-auto rounded-lg border bg-card">
          <table className="w-full text-sm">
            <thead className="bg-muted/40">
              <tr>
                <th className="sticky left-0 z-10 bg-muted/40 px-3 py-2 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                  Сотрудник
                </th>
                {heat.weeks.map((w) => (
                  <th key={w} className="px-2 py-2 text-center text-xs font-semibold text-muted-foreground">
                    {fmtWeek(w)}
                  </th>
                ))}
                <th className="px-3 py-2 text-center text-xs font-semibold uppercase text-muted-foreground">
                  Итого
                </th>
              </tr>
            </thead>
            <tbody>
              {heat.rows.map((row) => (
                <tr key={row.user_id} className="border-t">
                  <td className="sticky left-0 z-10 max-w-[220px] truncate bg-card px-3 py-2 font-medium">
                    {row.user_name}
                  </td>
                  {row.cells.map((c, i) => (
                    <td key={i} className="p-1 text-center">
                      <div
                        className={cn(
                          "rounded-md py-1.5 tabular",
                          c.planned_hours === "0.00" || Number(c.planned_hours) === 0
                            ? "text-muted-foreground/40"
                            : LOAD_STYLE[c.load],
                        )}
                        title={`${formatPercent(c.utilization_pct)} от нормы`}
                      >
                        {Number(c.planned_hours) > 0 ? formatNumber(c.planned_hours) : "·"}
                      </div>
                    </td>
                  ))}
                  <td className="px-3 text-center font-semibold tabular">
                    {formatNumber(row.total_hours)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <div className="mt-5 flex flex-wrap items-end gap-2 rounded-lg border bg-card p-4">
        <div className="space-y-1">
          <label className="micro-label">Сотрудник</label>
          <Select value={userId} onValueChange={setUserId}>
            <SelectTrigger className="w-52">
              <SelectValue placeholder="Выберите…" />
            </SelectTrigger>
            <SelectContent>
              {users?.items.map((u) => (
                <SelectItem key={u.id} value={u.id}>
                  {u.full_name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-1">
          <label className="micro-label">Проект</label>
          <Select value={projectId} onValueChange={setProjectId}>
            <SelectTrigger className="w-56">
              <SelectValue placeholder="Выберите…" />
            </SelectTrigger>
            <SelectContent>
              {projects?.items.map((p) => (
                <SelectItem key={p.id} value={p.id}>
                  {p.code}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-1">
          <label className="micro-label">Неделя (пн)</label>
          <Input type="date" value={week} onChange={(e) => setWeek(e.target.value)} className="w-40" />
        </div>
        <div className="space-y-1">
          <label className="micro-label">Часы</label>
          <Input
            type="number"
            value={hours}
            onChange={(e) => setHours(e.target.value)}
            className="w-24 tabular"
          />
        </div>
        <Button onClick={addPlan} disabled={upsert.isPending}>
          <Plus className="h-4 w-4" /> Запланировать
        </Button>
      </div>
    </div>
  );
}
