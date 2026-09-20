import { ChevronLeft, ChevronRight, Copy, Plus, Send } from "lucide-react";
import { useMemo, useState } from "react";

import { useProjects } from "@/entities/project/api";
import {
  TS_STATUS_LABELS,
  TS_STATUS_VARIANT,
  type TimeEntry,
  useCopyWeek,
  useSubmitWeek,
  useWeek,
} from "@/entities/timesheet/api";
import { ApiError } from "@/shared/api/types";
import { cn } from "@/shared/lib/cn";
import { formatHours } from "@/shared/lib/format";
import {
  WEEKDAY_LABELS,
  addDays,
  mondayOf,
  toISODate,
  weekDays,
} from "@/shared/lib/week";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { PageHeader } from "@/shared/ui/page";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/shared/ui/select";
import { PageLoader } from "@/shared/ui/spinner";
import { toast } from "@/shared/ui/toast";
import { CellEditor, type CellTarget } from "./CellEditor";

export function TimesheetsPage() {
  const [monday, setMonday] = useState(() => mondayOf(new Date()));
  const weekStart = toISODate(monday);
  const { data: week, isLoading } = useWeek(weekStart);
  const { data: projects } = useProjects({ limit: 200 });
  const submit = useSubmitWeek();
  const copy = useCopyWeek();
  const [extraProjects, setExtraProjects] = useState<string[]>([]);
  const [cell, setCell] = useState<CellTarget | null>(null);

  const days = weekDays(monday);
  const locked = week?.status === "submitted" || week?.status === "approved";

  // Карта project -> day(ISO) -> entry (берём первую запись на день).
  const grid = useMemo(() => {
    const map = new Map<string, Map<string, TimeEntry>>();
    week?.entries.forEach((e) => {
      if (!map.has(e.project_id)) map.set(e.project_id, new Map());
      const day = map.get(e.project_id)!;
      // суммируем часы при нескольких записях, храним первую как редактируемую
      if (!day.has(e.work_date)) day.set(e.work_date, e);
    });
    return map;
  }, [week]);

  const projectMap = useMemo(() => {
    const m = new Map<string, string>();
    projects?.items.forEach((p) => m.set(p.id, `${p.code} · ${p.name}`));
    return m;
  }, [projects]);

  const rowProjectIds = useMemo(() => {
    const ids = new Set<string>([...grid.keys(), ...extraProjects]);
    return [...ids];
  }, [grid, extraProjects]);

  const dayTotal = (iso: string) =>
    Number(week?.daily_totals[iso] ?? 0);

  const rowTotal = (pid: string) => {
    let sum = 0;
    week?.entries.forEach((e) => {
      if (e.project_id === pid) sum += Number(e.hours);
    });
    return sum;
  };

  const onSubmit = async () => {
    try {
      const r = await submit.mutateAsync(weekStart);
      toast.success(r.message);
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Ошибка отправки");
    }
  };

  const onCopyLast = async () => {
    try {
      const r = await copy.mutateAsync({
        source_week_start: toISODate(addDays(monday, -7)),
        target_week_start: weekStart,
      });
      toast.success(r.message);
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Ошибка копирования");
    }
  };

  return (
    <div>
      <PageHeader
        title="Мои трудозатраты"
        description="Списание часов по проектам за неделю. Заполните ячейки и отправьте на утверждение."
        actions={
          <div className="flex items-center gap-2">
            {week && (
              <Badge variant={TS_STATUS_VARIANT[week.status]}>
                {TS_STATUS_LABELS[week.status]}
              </Badge>
            )}
            <Button variant="outline" onClick={onCopyLast} disabled={locked || copy.isPending}>
              <Copy className="h-4 w-4" /> Копировать прошлую
            </Button>
            <Button onClick={onSubmit} disabled={locked || submit.isPending}>
              <Send className="h-4 w-4" /> Отправить неделю
            </Button>
          </div>
        }
      />

      <div className="mb-4 flex items-center gap-3">
        <Button variant="outline" size="icon" onClick={() => setMonday(addDays(monday, -7))}>
          <ChevronLeft className="h-4 w-4" />
        </Button>
        <div className="min-w-[220px] text-center font-medium">
          {toISODate(days[0])} — {toISODate(days[6])}
        </div>
        <Button variant="outline" size="icon" onClick={() => setMonday(addDays(monday, 7))}>
          <ChevronRight className="h-4 w-4" />
        </Button>
        <Button variant="ghost" size="sm" onClick={() => setMonday(mondayOf(new Date()))}>
          Текущая неделя
        </Button>
        <span className="ml-auto text-sm text-muted-foreground">
          Итого за неделю:{" "}
          <span className="font-semibold text-foreground tabular">
            {formatHours(week?.total_hours ?? 0)}
          </span>
        </span>
      </div>

      {isLoading ? (
        <PageLoader />
      ) : (
        <div className="overflow-x-auto rounded-lg border bg-card">
          <table className="w-full text-sm">
            <thead className="bg-muted/40">
              <tr>
                <th className="sticky left-0 z-10 bg-muted/40 px-3 py-2 text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                  Проект
                </th>
                {days.map((d, i) => (
                  <th
                    key={i}
                    className={cn(
                      "px-2 py-2 text-center text-xs font-semibold",
                      (i === 5 || i === 6) && "text-muted-foreground",
                    )}
                  >
                    {WEEKDAY_LABELS[i]}
                    <div className="font-normal text-muted-foreground">{d.getDate()}</div>
                  </th>
                ))}
                <th className="px-3 py-2 text-center text-xs font-semibold uppercase text-muted-foreground">
                  Итого
                </th>
              </tr>
            </thead>
            <tbody>
              {rowProjectIds.map((pid) => (
                <tr key={pid} className="border-t">
                  <td className="sticky left-0 z-10 max-w-[260px] truncate bg-card px-3 py-2 font-medium">
                    {projectMap.get(pid) ?? "Проект"}
                  </td>
                  {days.map((d, i) => {
                    const iso = toISODate(d);
                    const entry = grid.get(pid)?.get(iso) ?? null;
                    const dayEntries = week?.entries.filter(
                      (e) => e.project_id === pid && e.work_date === iso,
                    );
                    const sum = dayEntries?.reduce((a, e) => a + Number(e.hours), 0) ?? 0;
                    return (
                      <td key={i} className="p-1 text-center">
                        <button
                          disabled={locked}
                          onClick={() =>
                            setCell({
                              projectId: pid,
                              projectName: projectMap.get(pid) ?? "Проект",
                              date: iso,
                              entry,
                            })
                          }
                          className={cn(
                            "h-9 w-full rounded-md text-center tabular transition-colors",
                            sum > 0
                              ? "bg-primary/10 font-medium text-primary hover:bg-primary/20"
                              : "text-muted-foreground hover:bg-accent",
                            locked && "cursor-not-allowed opacity-70",
                          )}
                        >
                          {sum > 0 ? sum.toString().replace(".", ",") : "·"}
                        </button>
                      </td>
                    );
                  })}
                  <td className="px-3 text-center font-semibold tabular">
                    {rowTotal(pid) || "—"}
                  </td>
                </tr>
              ))}
              {rowProjectIds.length === 0 && (
                <tr>
                  <td colSpan={9} className="px-3 py-8 text-center text-muted-foreground">
                    Добавьте проект, чтобы начать списывать часы
                  </td>
                </tr>
              )}
            </tbody>
            <tfoot className="border-t bg-muted/30">
              <tr>
                <td className="sticky left-0 z-10 bg-muted/30 px-3 py-2 text-xs font-semibold uppercase text-muted-foreground">
                  Итого за день
                </td>
                {days.map((d, i) => {
                  const t = dayTotal(toISODate(d));
                  return (
                    <td
                      key={i}
                      className={cn(
                        "px-2 py-2 text-center font-semibold tabular",
                        t > 24 && "text-destructive",
                      )}
                    >
                      {t > 0 ? t.toString().replace(".", ",") : "—"}
                    </td>
                  );
                })}
                <td className="px-3 text-center font-bold tabular">{week?.total_hours ?? 0}</td>
              </tr>
            </tfoot>
          </table>
        </div>
      )}

      {!locked && (
        <div className="mt-3 flex items-center gap-2">
          <Select
            value=""
            onValueChange={(v) => setExtraProjects((p) => [...new Set([...p, v])])}
          >
            <SelectTrigger className="w-72">
              <SelectValue placeholder="+ Добавить проект в таблицу" />
            </SelectTrigger>
            <SelectContent>
              {projects?.items
                .filter((p) => !rowProjectIds.includes(p.id))
                .map((p) => (
                  <SelectItem key={p.id} value={p.id}>
                    {p.code} · {p.name}
                  </SelectItem>
                ))}
            </SelectContent>
          </Select>
          <Plus className="h-4 w-4 text-muted-foreground" />
        </div>
      )}

      <CellEditor target={cell} onClose={() => setCell(null)} />
    </div>
  );
}
