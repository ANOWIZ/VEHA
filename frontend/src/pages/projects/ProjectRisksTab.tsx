import { Pencil, Plus, ShieldAlert, Trash2 } from "lucide-react";
import { useMemo, useState } from "react";

import {
  IMPACT_LABELS,
  PROBABILITY_LABELS,
  RISK_CATEGORY_LABELS,
  RISK_LEVEL_LABELS,
  RISK_LEVEL_VARIANT,
  RISK_RESPONSE_LABELS,
  RISK_STATUS_LABELS,
  RISK_STATUS_VARIANT,
  type Risk,
  type RiskCategory,
  type RiskLevel,
  type RiskMatrixCell,
  type RiskResponse,
  type RiskStatus,
  useCreateRisk,
  useDeleteRisk,
  useRisks,
  useUpdateRisk,
} from "@/entities/risk/api";
import { RiskMatrix } from "@/entities/risk/RiskMatrix";
import { useUserMap, useUsers } from "@/entities/user/api";
import { ApiError } from "@/shared/api/types";
import { formatDate } from "@/shared/lib/format";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardContent } from "@/shared/ui/card";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/shared/ui/dialog";
import { Input } from "@/shared/ui/input";
import { Label } from "@/shared/ui/label";
import { EmptyState } from "@/shared/ui/page";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/shared/ui/select";
import { toast } from "@/shared/ui/toast";

const ACTIVE: RiskStatus[] = ["open", "mitigating"];
const NONE = "__none__";

function levelOf(score: number): RiskLevel {
  if (score >= 6) return "high";
  if (score >= 3) return "medium";
  return "low";
}

interface FormState {
  title: string;
  category: RiskCategory;
  probability: string;
  impact: string;
  response_strategy: RiskResponse;
  owner_id: string;
  due_date: string;
  description: string;
  mitigation_plan: string;
}

const EMPTY_FORM: FormState = {
  title: "",
  category: "technical",
  probability: "2",
  impact: "2",
  response_strategy: "mitigate",
  owner_id: NONE,
  due_date: "",
  description: "",
  mitigation_plan: "",
};

export function ProjectRisksTab({
  projectId,
  canManage,
}: {
  projectId: string;
  canManage: boolean;
}) {
  const { data: risks } = useRisks(projectId);
  const { data: users } = useUsers();
  const userMap = useUserMap();
  const create = useCreateRisk(projectId);
  const update = useUpdateRisk(projectId);
  const remove = useDeleteRisk(projectId);

  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<Risk | null>(null);
  const [form, setForm] = useState<FormState>(EMPTY_FORM);

  const activeRisks = useMemo(
    () => (risks ?? []).filter((r) => ACTIVE.includes(r.status)),
    [risks],
  );
  const highCount = activeRisks.filter((r) => r.score >= 6).length;

  // Матрица из активных рисков проекта (клиентская агрегация).
  const cells: RiskMatrixCell[] = useMemo(() => {
    const counts = new Map<string, number>();
    for (const r of activeRisks) {
      const key = `${r.probability}-${r.impact}`;
      counts.set(key, (counts.get(key) ?? 0) + 1);
    }
    const out: RiskMatrixCell[] = [];
    for (let p = 1; p <= 3; p++) {
      for (let i = 1; i <= 3; i++) {
        const score = p * i;
        out.push({
          probability: p,
          impact: i,
          score,
          level: levelOf(score),
          count: counts.get(`${p}-${i}`) ?? 0,
        });
      }
    }
    return out;
  }, [activeRisks]);

  const openCreate = () => {
    setEditing(null);
    setForm(EMPTY_FORM);
    setOpen(true);
  };

  const openEdit = (r: Risk) => {
    setEditing(r);
    setForm({
      title: r.title,
      category: r.category,
      probability: String(r.probability),
      impact: String(r.impact),
      response_strategy: r.response_strategy,
      owner_id: r.owner_id ?? NONE,
      due_date: r.due_date ? r.due_date.slice(0, 10) : "",
      description: r.description ?? "",
      mitigation_plan: r.mitigation_plan ?? "",
    });
    setOpen(true);
  };

  const submit = async () => {
    if (!form.title.trim()) {
      toast.error("Укажите название риска");
      return;
    }
    const payload = {
      title: form.title.trim(),
      category: form.category,
      probability: Number(form.probability),
      impact: Number(form.impact),
      response_strategy: form.response_strategy,
      owner_id: form.owner_id === NONE ? null : form.owner_id,
      due_date: form.due_date ? form.due_date : null,
      description: form.description.trim() || null,
      mitigation_plan: form.mitigation_plan.trim() || null,
    };
    try {
      if (editing) {
        await update.mutateAsync({ riskId: editing.id, ...payload });
        toast.success("Риск обновлён");
      } else {
        await create.mutateAsync(payload);
        toast.success("Риск добавлен");
      }
      setOpen(false);
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Ошибка");
    }
  };

  const changeStatus = async (r: Risk, status: RiskStatus) => {
    try {
      await update.mutateAsync({ riskId: r.id, status });
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Ошибка");
    }
  };

  const del = async (r: Risk) => {
    if (!window.confirm(`Удалить риск «${r.title}»?`)) return;
    try {
      await remove.mutateAsync(r.id);
      toast.success("Риск удалён");
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Ошибка удаления");
    }
  };

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="flex flex-wrap items-center gap-3">
          <div className="rounded-md border bg-card px-3 py-2 text-sm">
            Активных рисков: <span className="font-semibold">{activeRisks.length}</span>
          </div>
          <div className="rounded-md border bg-card px-3 py-2 text-sm">
            Высокой критичности:{" "}
            <span className="font-semibold text-destructive">{highCount}</span>
          </div>
        </div>
        {canManage && (
          <Button onClick={openCreate}>
            <Plus className="h-4 w-4" /> Добавить риск
          </Button>
        )}
      </div>

      {activeRisks.length > 0 && (
        <div className="rounded-lg border bg-card p-4">
          <p className="mb-3 text-sm font-medium">Матрица активных рисков</p>
          <RiskMatrix cells={cells} />
        </div>
      )}

      {!risks || risks.length === 0 ? (
        <EmptyState
          title="Рисков пока нет"
          description="Заведите риски проекта, чтобы отслеживать вероятность и влияние."
          icon={<ShieldAlert className="h-8 w-8" />}
        />
      ) : (
        <div className="space-y-2">
          {risks.map((r) => (
            <Card key={r.id}>
              <CardContent className="flex flex-wrap items-start justify-between gap-3 p-4">
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge variant={RISK_LEVEL_VARIANT[r.level]}>
                      {RISK_LEVEL_LABELS[r.level]} · балл {r.score}
                    </Badge>
                    <Badge variant={RISK_STATUS_VARIANT[r.status]}>
                      {RISK_STATUS_LABELS[r.status]}
                    </Badge>
                    <Badge variant="secondary">{RISK_CATEGORY_LABELS[r.category]}</Badge>
                    <span className="font-medium">{r.title}</span>
                  </div>
                  <p className="mt-1.5 text-xs text-muted-foreground">
                    Вероятность: {PROBABILITY_LABELS[r.probability]} · влияние:{" "}
                    {IMPACT_LABELS[r.impact]} · реагирование:{" "}
                    {RISK_RESPONSE_LABELS[r.response_strategy]}
                    {r.owner_id && ` · ответственный: ${userMap.get(r.owner_id)?.full_name ?? "—"}`}
                    {r.due_date && ` · срок: ${formatDate(r.due_date)}`}
                  </p>
                  {r.description && <p className="mt-2 text-sm">{r.description}</p>}
                  {r.mitigation_plan && (
                    <p className="mt-1 text-sm text-muted-foreground">
                      План реагирования: {r.mitigation_plan}
                    </p>
                  )}
                </div>
                {canManage && (
                  <div className="flex items-center gap-2">
                    <Select
                      value={r.status}
                      onValueChange={(v) => changeStatus(r, v as RiskStatus)}
                    >
                      <SelectTrigger className="w-36">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {Object.entries(RISK_STATUS_LABELS).map(([v, l]) => (
                          <SelectItem key={v} value={v}>
                            {l}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <Button variant="ghost" size="icon" onClick={() => openEdit(r)}>
                      <Pencil className="h-4 w-4" />
                    </Button>
                    <Button variant="ghost" size="icon" onClick={() => del(r)}>
                      <Trash2 className="h-4 w-4 text-destructive" />
                    </Button>
                  </div>
                )}
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>{editing ? "Изменить риск" : "Новый риск"}</DialogTitle>
          </DialogHeader>
          <div className="space-y-3">
            <div className="space-y-1.5">
              <Label>Название</Label>
              <Input
                value={form.title}
                onChange={(e) => setForm({ ...form, title: e.target.value })}
                placeholder="Например: срыв сроков миграции"
              />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label>Категория</Label>
                <Select
                  value={form.category}
                  onValueChange={(v) => setForm({ ...form, category: v as RiskCategory })}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {Object.entries(RISK_CATEGORY_LABELS).map(([v, l]) => (
                      <SelectItem key={v} value={v}>
                        {l}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1.5">
                <Label>Реагирование</Label>
                <Select
                  value={form.response_strategy}
                  onValueChange={(v) =>
                    setForm({ ...form, response_strategy: v as RiskResponse })
                  }
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {Object.entries(RISK_RESPONSE_LABELS).map(([v, l]) => (
                      <SelectItem key={v} value={v}>
                        {l}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1.5">
                <Label>Вероятность</Label>
                <Select
                  value={form.probability}
                  onValueChange={(v) => setForm({ ...form, probability: v })}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {[1, 2, 3].map((n) => (
                      <SelectItem key={n} value={String(n)}>
                        {PROBABILITY_LABELS[n]} ({n})
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1.5">
                <Label>Влияние</Label>
                <Select
                  value={form.impact}
                  onValueChange={(v) => setForm({ ...form, impact: v })}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {[1, 2, 3].map((n) => (
                      <SelectItem key={n} value={String(n)}>
                        {IMPACT_LABELS[n]} ({n})
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1.5">
                <Label>Ответственный</Label>
                <Select
                  value={form.owner_id}
                  onValueChange={(v) => setForm({ ...form, owner_id: v })}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="—" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={NONE}>— не назначен —</SelectItem>
                    {users?.items.map((u) => (
                      <SelectItem key={u.id} value={u.id}>
                        {u.full_name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1.5">
                <Label>Срок реагирования</Label>
                <Input
                  type="date"
                  value={form.due_date}
                  onChange={(e) => setForm({ ...form, due_date: e.target.value })}
                />
              </div>
            </div>
            <div className="space-y-1.5">
              <Label>Описание</Label>
              <Input
                value={form.description}
                onChange={(e) => setForm({ ...form, description: e.target.value })}
              />
            </div>
            <div className="space-y-1.5">
              <Label>План реагирования</Label>
              <Input
                value={form.mitigation_plan}
                onChange={(e) => setForm({ ...form, mitigation_plan: e.target.value })}
              />
            </div>
            <div className="rounded-md bg-muted/40 px-3 py-2 text-sm">
              Расчётный балл:{" "}
              <span className="font-semibold">
                {Number(form.probability) * Number(form.impact)}
              </span>{" "}
              ·{" "}
              <Badge
                variant={
                  RISK_LEVEL_VARIANT[levelOf(Number(form.probability) * Number(form.impact))]
                }
              >
                {RISK_LEVEL_LABELS[levelOf(Number(form.probability) * Number(form.impact))]}
              </Badge>
            </div>
          </div>
          <DialogFooter className="gap-2">
            <Button variant="outline" onClick={() => setOpen(false)}>
              Отмена
            </Button>
            <Button onClick={submit} disabled={create.isPending || update.isPending}>
              {editing ? "Сохранить" : "Добавить"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
