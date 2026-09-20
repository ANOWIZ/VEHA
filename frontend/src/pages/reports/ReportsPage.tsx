import { Download } from "lucide-react";
import { useState } from "react";

import { useProjects } from "@/entities/project/api";
import {
  downloadReport,
  portfolioXlsxPath,
  timesheetsXlsxPath,
  useUtilization,
  utilizationXlsxPath,
} from "@/entities/report/api";
import { canSeeFinancials, hasAnyRole } from "@/entities/user/model";
import { ApiError } from "@/shared/api/types";
import { useMe } from "@/shared/auth/useAuth";
import { cn } from "@/shared/lib/cn";
import { formatHours } from "@/shared/lib/format";
import { Button } from "@/shared/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/shared/ui/card";
import { Input } from "@/shared/ui/input";
import { Label } from "@/shared/ui/label";
import { PageHeader, StatCard } from "@/shared/ui/page";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/shared/ui/select";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/shared/ui/table";
import { toast } from "@/shared/ui/toast";

const TS_STATUS = {
  __all__: "Все статусы",
  draft: "Черновик",
  submitted: "На утверждении",
  approved: "Утверждён",
  rejected: "Отклонён",
};
const ALL_PROJECTS = "__all__";

function utilTone(pct: number): "success" | "warning" | "destructive" {
  if (pct > 100) return "destructive";
  if (pct >= 70) return "success";
  return "warning";
}
const TONE_BAR: Record<string, string> = {
  success: "bg-success",
  warning: "bg-warning",
  destructive: "bg-destructive",
};

function isoDaysAgo(days: number): string {
  const d = new Date();
  d.setDate(d.getDate() - days);
  return d.toISOString().slice(0, 10);
}

export function ReportsPage() {
  const { data: me } = useMe();
  const showCost = canSeeFinancials(me);
  // Utilization — компанийный отчёт; РП его не видит (скоупинг по проектам там
  // невозможен). Доступен director/admin/finance — синхронно с бэком.
  const showUtilization = hasAnyRole(me, ["director", "admin", "finance"]);
  const [from, setFrom] = useState(isoDaysAgo(90));
  const [to, setTo] = useState(isoDaysAgo(0));
  const [tsProject, setTsProject] = useState(ALL_PROJECTS);
  const [tsStatus, setTsStatus] = useState("__all__");

  const { data: util, isFetching } = useUtilization(from, to, showUtilization);
  const { data: projects } = useProjects({ limit: 500 });

  const download = async (path: string, filename: string) => {
    try {
      await downloadReport(path, filename);
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Ошибка выгрузки");
    }
  };

  return (
    <div>
      <PageHeader
        title="Отчёты и выгрузки"
        description="Загрузка ресурсов, выгрузка трудозатрат и портфеля в XLSX."
      />

      {/* Период */}
      <div className="mb-6 flex flex-wrap items-end gap-3">
        <div className="space-y-1.5">
          <Label>Период с</Label>
          <Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} className="w-44" />
        </div>
        <div className="space-y-1.5">
          <Label>по</Label>
          <Input type="date" value={to} onChange={(e) => setTo(e.target.value)} className="w-44" />
        </div>
      </div>

      {/* Utilization — только для руководства/финансов */}
      {showUtilization && (
      <Card className="mb-6">
        <CardHeader className="flex flex-row items-center justify-between gap-2 space-y-0">
          <CardTitle className="text-base">Загрузка ресурсов (utilization)</CardTitle>
          <Button
            variant="outline"
            size="sm"
            onClick={() =>
              download(utilizationXlsxPath(from, to), `utilization_${from}_${to}.xlsx`)
            }
          >
            <Download className="h-4 w-4" /> XLSX
          </Button>
        </CardHeader>
        <CardContent>
          <div className="mb-4 grid grid-cols-2 gap-4 lg:grid-cols-4">
            <StatCard label="Рабочих дней" value={util?.working_days ?? "—"} />
            <StatCard
              label="Ёмкость/сотрудник"
              value={util ? formatHours(util.capacity_per_user) : "—"}
            />
            <StatCard
              label="Билируемых часов"
              value={util ? formatHours(util.total_billable_hours) : "—"}
            />
            <StatCard
              label="Средняя загрузка"
              value={util ? `${util.avg_utilization_pct} %` : "—"}
              tone={util ? utilTone(Number(util.avg_utilization_pct)) : "default"}
            />
          </div>

          {isFetching && !util ? (
            <p className="py-6 text-center text-sm text-muted-foreground">Загрузка…</p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Сотрудник</TableHead>
                  <TableHead>Подразделение</TableHead>
                  <TableHead className="text-right">Билируемые</TableHead>
                  <TableHead className="text-right">Ёмкость</TableHead>
                  <TableHead className="w-56">Загрузка</TableHead>
                  <TableHead className="text-right">Проектов</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {util?.rows.map((r) => {
                  const pct = Number(r.utilization_pct);
                  const tone = utilTone(pct);
                  return (
                    <TableRow key={r.user_id}>
                      <TableCell className="font-medium">{r.full_name}</TableCell>
                      <TableCell className="text-sm text-muted-foreground">
                        {r.department ?? "—"}
                      </TableCell>
                      <TableCell className="text-right tabular">
                        {formatHours(r.billable_hours)}
                      </TableCell>
                      <TableCell className="text-right tabular text-muted-foreground">
                        {formatHours(r.capacity_hours)}
                      </TableCell>
                      <TableCell>
                        <div className="flex items-center gap-2">
                          <div className="h-2.5 flex-1 overflow-hidden rounded-full bg-muted">
                            <div
                              className={cn("h-full rounded-full", TONE_BAR[tone])}
                              style={{ width: `${Math.min(100, pct)}%` }}
                            />
                          </div>
                          <span className="w-12 text-right text-sm tabular">{r.utilization_pct}%</span>
                        </div>
                      </TableCell>
                      <TableCell className="text-right tabular">{r.projects_count}</TableCell>
                    </TableRow>
                  );
                })}
                {util && util.rows.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={6} className="py-6 text-center text-muted-foreground">
                      Нет данных за период
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
      )}

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        {/* Выгрузка таймшитов */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Выгрузка трудозатрат</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <p className="text-sm text-muted-foreground">
              Трудозатраты за выбранный период{showCost ? " (с себестоимостью)" : ""}. Доступ к
              проектам учитывается.
            </p>
            <div className="space-y-1.5">
              <Label>Проект</Label>
              <Select value={tsProject} onValueChange={setTsProject}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={ALL_PROJECTS}>Все проекты</SelectItem>
                  {projects?.items.map((p) => (
                    <SelectItem key={p.id} value={p.id}>
                      {p.code} — {p.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5">
              <Label>Статус</Label>
              <Select value={tsStatus} onValueChange={setTsStatus}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {Object.entries(TS_STATUS).map(([v, l]) => (
                    <SelectItem key={v} value={v}>
                      {l}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <Button
              onClick={() =>
                download(
                  timesheetsXlsxPath(from, to, {
                    projectId: tsProject === ALL_PROJECTS ? undefined : tsProject,
                    status: tsStatus === "__all__" ? undefined : tsStatus,
                  }),
                  `timesheets_${from}_${to}.xlsx`,
                )
              }
            >
              <Download className="h-4 w-4" /> Скачать XLSX
            </Button>
          </CardContent>
        </Card>

        {/* Портфель */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Портфель проектов</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <p className="text-sm text-muted-foreground">
              Выручка, маржа, отклонение часов и риски по доступным активным проектам.
            </p>
            <Button onClick={() => download(portfolioXlsxPath(), "portfolio.xlsx")}>
              <Download className="h-4 w-4" /> Скачать XLSX
            </Button>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
