import { Plus } from "lucide-react";
import { useEffect, useState } from "react";
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";

import { useProjects } from "@/entities/project/api";
import {
  COST_CATEGORY_LABELS,
  useActuals,
  useAddActual,
  useProjectFinance,
} from "@/entities/finance/api";
import { ApiError } from "@/shared/api/types";
import { formatHours, formatMoney, formatPercent } from "@/shared/lib/format";
import { Button } from "@/shared/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/shared/ui/card";
import { Input } from "@/shared/ui/input";
import { EmptyState, PageHeader, StatCard } from "@/shared/ui/page";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/shared/ui/select";
import { PageLoader } from "@/shared/ui/spinner";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/shared/ui/table";
import { toast } from "@/shared/ui/toast";

const CATS = ["payroll", "licenses", "subcontract", "travel", "other"];

export function FinancePage() {
  const { data: projects } = useProjects({ limit: 200 });
  const [projectId, setProjectId] = useState("");
  const { data: fin, isLoading } = useProjectFinance(projectId || undefined);
  const { data: actuals } = useActuals(projectId || undefined);
  const addActual = useAddActual(projectId);

  const [cat, setCat] = useState("licenses");
  const [amount, setAmount] = useState("");
  const [occurred, setOccurred] = useState(new Date().toISOString().slice(0, 10));
  const [desc, setDesc] = useState("");

  useEffect(() => {
    if (!projectId && projects && projects.items.length > 0) setProjectId(projects.items[0].id);
  }, [projects, projectId]);

  const marginPct = Number(fin?.margin.margin_pct ?? 0);
  const overrun = Number(fin?.hours_overrun_pct ?? 0);

  const chartData = CATS.map((c) => ({
    name: COST_CATEGORY_LABELS[c],
    План: Number(fin?.budget?.planned_costs?.[c] ?? 0),
    Факт: Number(fin?.margin.cost_breakdown?.[c] ?? 0),
  }));

  const addCost = async () => {
    if (!amount) {
      toast.error("Укажите сумму");
      return;
    }
    try {
      await addActual.mutateAsync({ category: cat, amount, occurred_on: occurred, description: desc });
      toast.success("Затрата добавлена");
      setAmount("");
      setDesc("");
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Ошибка");
    }
  };

  return (
    <div>
      <PageHeader
        title="Финансы проекта"
        description="План-факт, маржинальность и прогноз затрат в реальном времени"
      />

      <div className="mb-5 w-96">
        <Select value={projectId} onValueChange={setProjectId}>
          <SelectTrigger>
            <SelectValue placeholder="Выберите проект…" />
          </SelectTrigger>
          <SelectContent>
            {projects?.items.map((p) => (
              <SelectItem key={p.id} value={p.id}>
                {p.code} · {p.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {!projectId ? (
        <EmptyState title="Выберите проект" />
      ) : isLoading || !fin ? (
        <PageLoader />
      ) : (
        <div className="space-y-6">
          <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
            <StatCard label="Выручка по договору" value={formatMoney(fin.margin.revenue)} />
            <StatCard label="Затраты (факт)" value={formatMoney(fin.margin.total_cost)} />
            <StatCard
              label="Маржа"
              value={formatMoney(fin.margin.margin)}
              hint={`Маржинальность: ${formatPercent(fin.margin.margin_pct)}`}
              tone={marginPct >= 20 ? "success" : marginPct >= 10 ? "warning" : "destructive"}
            />
            <StatCard
              label="Прогноз затрат (EAC)"
              value={formatMoney(fin.forecast.eac)}
              hint={`Осталось (ETC): ${formatMoney(fin.forecast.etc)}`}
            />
          </div>

          <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
            <StatCard label="План часов" value={formatHours(fin.planned_hours)} />
            <StatCard label="Факт часов (утв.)" value={formatHours(fin.actual_hours)} />
            <StatCard
              label="Перерасход часов"
              value={formatPercent(fin.hours_overrun_pct)}
              tone={overrun > 10 ? "destructive" : overrun > 0 ? "warning" : "success"}
            />
          </div>

          <Card>
            <CardHeader>
              <CardTitle>Затраты: план vs факт по категориям</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="h-72">
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={chartData} margin={{ top: 8, right: 8, bottom: 4, left: 8 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="hsl(40 16% 88%)" vertical={false} />
                    <XAxis dataKey="name" tick={{ fontSize: 12 }} />
                    <YAxis
                      tick={{ fontSize: 11 }}
                      tickFormatter={(v) => (v >= 1000 ? `${Math.round(v / 1000)}к` : `${v}`)}
                    />
                    <Tooltip formatter={(v: number) => formatMoney(v)} />
                    <Legend />
                    <Bar dataKey="План" fill="hsl(222 28% 70%)" radius={[3, 3, 0, 0]} />
                    <Bar dataKey="Факт" fill="hsl(36 96% 50%)" radius={[3, 3, 0, 0]}>
                      {chartData.map((d, i) => (
                        <Cell key={i} fill={d.Факт > d.План ? "hsl(4 72% 55%)" : "hsl(36 96% 50%)"} />
                      ))}
                    </Bar>
                  </BarChart>
                </ResponsiveContainer>
              </div>
            </CardContent>
          </Card>

          <div className="grid gap-4 lg:grid-cols-2">
            <Card>
              <CardHeader>
                <CardTitle>Структура затрат</CardTitle>
              </CardHeader>
              <CardContent className="p-0">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Категория</TableHead>
                      <TableHead className="text-right">План</TableHead>
                      <TableHead className="text-right">Факт</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {CATS.map((c) => (
                      <TableRow key={c}>
                        <TableCell>{COST_CATEGORY_LABELS[c]}</TableCell>
                        <TableCell className="text-right tabular text-muted-foreground">
                          {formatMoney(fin.budget?.planned_costs?.[c] ?? 0)}
                        </TableCell>
                        <TableCell className="text-right tabular font-medium">
                          {formatMoney(fin.margin.cost_breakdown?.[c] ?? 0)}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Фактические затраты</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                <div className="flex flex-wrap items-end gap-2">
                  <Select value={cat} onValueChange={setCat}>
                    <SelectTrigger className="w-36">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {CATS.filter((c) => c !== "payroll").map((c) => (
                        <SelectItem key={c} value={c}>
                          {COST_CATEGORY_LABELS[c]}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <Input
                    type="number"
                    placeholder="Сумма"
                    value={amount}
                    onChange={(e) => setAmount(e.target.value)}
                    className="w-28 tabular"
                  />
                  <Input
                    type="date"
                    value={occurred}
                    onChange={(e) => setOccurred(e.target.value)}
                    className="w-36"
                  />
                  <Button onClick={addCost} disabled={addActual.isPending}>
                    <Plus className="h-4 w-4" />
                  </Button>
                </div>
                <div className="max-h-56 overflow-auto">
                  {(actuals ?? []).map((a) => (
                    <div
                      key={a.id}
                      className="flex items-center justify-between border-b py-2 text-sm last:border-0"
                    >
                      <div>
                        <span className="font-medium">{COST_CATEGORY_LABELS[a.category] ?? a.category}</span>
                        {a.description && (
                          <span className="ml-2 text-muted-foreground">{a.description}</span>
                        )}
                      </div>
                      <span className="tabular">{formatMoney(a.amount)}</span>
                    </div>
                  ))}
                  {(actuals ?? []).length === 0 && (
                    <p className="py-3 text-sm text-muted-foreground">Затрат пока нет</p>
                  )}
                </div>
              </CardContent>
            </Card>
          </div>
        </div>
      )}
    </div>
  );
}
