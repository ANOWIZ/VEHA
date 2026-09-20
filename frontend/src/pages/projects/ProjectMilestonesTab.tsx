import { CalendarClock, Plus } from "lucide-react";
import { useState } from "react";

import { useAddMilestone } from "@/entities/project/api";
import type { ProjectDetail } from "@/entities/project/model";
import { ApiError } from "@/shared/api/types";
import { formatDate, formatMoney } from "@/shared/lib/format";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Input } from "@/shared/ui/input";
import { Label } from "@/shared/ui/label";
import { EmptyState } from "@/shared/ui/page";
import { toast } from "@/shared/ui/toast";

export function ProjectMilestonesTab({
  project,
  canManage,
}: {
  project: ProjectDetail;
  canManage: boolean;
}) {
  const addMilestone = useAddMilestone(project.id);
  const [name, setName] = useState("");
  const [date, setDate] = useState("");
  const [amount, setAmount] = useState("0");
  const [isPayment, setIsPayment] = useState(false);

  const add = async () => {
    if (!name.trim() || !date) {
      toast.error("Укажите название и дату вехи");
      return;
    }
    try {
      await addMilestone.mutateAsync({
        name,
        milestone_date: date,
        is_payment: isPayment,
        amount: amount || "0",
      });
      setName("");
      setDate("");
      setAmount("0");
      setIsPayment(false);
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Ошибка");
    }
  };

  const sorted = [...project.milestones].sort((a, b) =>
    a.milestone_date.localeCompare(b.milestone_date),
  );

  return (
    <div className="space-y-4">
      {canManage && (
        <div className="flex flex-wrap items-end gap-2 rounded-lg border bg-card p-3">
          <div className="min-w-[200px] flex-1 space-y-1">
            <Label>Название</Label>
            <Input value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          <div className="space-y-1">
            <Label>Дата</Label>
            <Input type="date" value={date} onChange={(e) => setDate(e.target.value)} />
          </div>
          <div className="space-y-1">
            <Label>Сумма, ₽</Label>
            <Input
              type="number"
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
              className="w-36 tabular"
            />
          </div>
          <label className="flex items-center gap-2 pb-2 text-sm">
            <input
              type="checkbox"
              checked={isPayment}
              onChange={(e) => setIsPayment(e.target.checked)}
            />
            Платёжная веха
          </label>
          <Button onClick={add} disabled={addMilestone.isPending}>
            <Plus className="h-4 w-4" /> Добавить
          </Button>
        </div>
      )}

      {sorted.length === 0 ? (
        <EmptyState title="Вех нет" icon={<CalendarClock className="h-8 w-8" />} />
      ) : (
        <div className="space-y-2">
          {sorted.map((m) => (
            <div
              key={m.id}
              className="flex items-center justify-between rounded-lg border bg-card p-3"
            >
              <div className="flex items-center gap-3">
                <CalendarClock className="h-5 w-5 text-muted-foreground" />
                <div>
                  <p className="font-medium">{m.name}</p>
                  <p className="text-xs text-muted-foreground">{formatDate(m.milestone_date)}</p>
                </div>
              </div>
              <div className="flex items-center gap-3">
                {m.is_payment && <Badge variant="warning">Платёж</Badge>}
                <span className="tabular font-medium">{formatMoney(m.amount)}</span>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
