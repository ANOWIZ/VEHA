import { Check, X } from "lucide-react";
import { useState } from "react";

import {
  useApprove,
  usePending,
  useReject,
} from "@/entities/timesheet/api";
import { ApiError } from "@/shared/api/types";
import { formatDate, formatHours } from "@/shared/lib/format";
import { Button } from "@/shared/ui/button";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/shared/ui/dialog";
import { Input } from "@/shared/ui/input";
import { Label } from "@/shared/ui/label";
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
import { toast } from "@/shared/ui/toast";

export function ApprovalsPage() {
  const { data: pending, isLoading } = usePending();
  const approve = useApprove();
  const reject = useReject();
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [rejectOpen, setRejectOpen] = useState(false);
  const [reason, setReason] = useState("");

  const toggle = (id: string) =>
    setSelected((s) => {
      const next = new Set(s);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });

  const allIds = pending?.map((e) => e.id) ?? [];
  const allSelected = allIds.length > 0 && allIds.every((id) => selected.has(id));

  const toggleAll = () =>
    setSelected(allSelected ? new Set() : new Set(allIds));

  const doApprove = async () => {
    if (selected.size === 0) return;
    try {
      const r = await approve.mutateAsync([...selected]);
      toast.success(r.message);
      setSelected(new Set());
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Ошибка");
    }
  };

  const doReject = async () => {
    if (!reason.trim()) {
      toast.error("Укажите причину отклонения");
      return;
    }
    try {
      const r = await reject.mutateAsync({ entry_ids: [...selected], reason });
      toast.success(r.message);
      setSelected(new Set());
      setRejectOpen(false);
      setReason("");
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Ошибка");
    }
  };

  return (
    <div>
      <PageHeader
        title="Утверждение трудозатрат"
        description="Записи, ожидающие вашего утверждения по проектам, где вы — РП."
        actions={
          <div className="flex gap-2">
            <Button
              variant="outline"
              disabled={selected.size === 0}
              onClick={() => setRejectOpen(true)}
            >
              <X className="h-4 w-4" /> Отклонить ({selected.size})
            </Button>
            <Button variant="success" disabled={selected.size === 0} onClick={doApprove}>
              <Check className="h-4 w-4" /> Утвердить ({selected.size})
            </Button>
          </div>
        }
      />

      {isLoading ? (
        <PageLoader />
      ) : !pending || pending.length === 0 ? (
        <EmptyState
          title="Нет записей на утверждение"
          description="Все трудозатраты по вашим проектам обработаны."
        />
      ) : (
        <div className="rounded-lg border bg-card">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-10">
                  <input type="checkbox" checked={allSelected} onChange={toggleAll} />
                </TableHead>
                <TableHead>Сотрудник</TableHead>
                <TableHead>Проект</TableHead>
                <TableHead>Дата</TableHead>
                <TableHead className="text-right">Часы</TableHead>
                <TableHead>Комментарий</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {pending.map((e) => (
                <TableRow key={e.id} data-state={selected.has(e.id) ? "selected" : undefined}>
                  <TableCell>
                    <input
                      type="checkbox"
                      checked={selected.has(e.id)}
                      onChange={() => toggle(e.id)}
                    />
                  </TableCell>
                  <TableCell className="font-medium">{e.user_name}</TableCell>
                  <TableCell className="font-mono text-xs">{e.project_code}</TableCell>
                  <TableCell>{formatDate(e.work_date)}</TableCell>
                  <TableCell className="text-right tabular">{formatHours(e.hours)}</TableCell>
                  <TableCell className="max-w-xs truncate text-muted-foreground">
                    {e.comment}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      <Dialog open={rejectOpen} onOpenChange={setRejectOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Отклонить {selected.size} записей</DialogTitle>
          </DialogHeader>
          <div className="space-y-1.5">
            <Label>Причина отклонения</Label>
            <Input value={reason} onChange={(e) => setReason(e.target.value)} />
          </div>
          <DialogFooter className="gap-2">
            <Button variant="outline" onClick={() => setRejectOpen(false)}>
              Отмена
            </Button>
            <Button variant="destructive" onClick={doReject}>
              Отклонить
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
