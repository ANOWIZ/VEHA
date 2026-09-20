import { useEffect, useState } from "react";

import { useTasks } from "@/entities/task/api";
import {
  type TimeEntry,
  useCreateEntry,
  useDeleteEntry,
  useUpdateEntry,
} from "@/entities/timesheet/api";
import { ApiError } from "@/shared/api/types";
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/shared/ui/select";
import { Spinner } from "@/shared/ui/spinner";
import { formatDate } from "@/shared/lib/format";
import { toast } from "@/shared/ui/toast";

export interface CellTarget {
  projectId: string;
  projectName: string;
  date: string;
  entry: TimeEntry | null;
}

export function CellEditor({
  target,
  onClose,
}: {
  target: CellTarget | null;
  onClose: () => void;
}) {
  const create = useCreateEntry();
  const update = useUpdateEntry();
  const del = useDeleteEntry();
  const { data: tasks } = useTasks(target?.projectId);

  const [hours, setHours] = useState("");
  const [comment, setComment] = useState("");
  const [taskId, setTaskId] = useState<string>("");

  useEffect(() => {
    if (target) {
      setHours(target.entry?.hours ?? "");
      setComment(target.entry?.comment ?? "");
      setTaskId(target.entry?.task_id ?? "");
    }
  }, [target]);

  if (!target) return null;

  const save = async () => {
    const h = Number(hours);
    if (!h || h <= 0) {
      toast.error("Укажите часы (> 0, шаг 0,25)");
      return;
    }
    if (!comment.trim()) {
      toast.error("Комментарий обязателен");
      return;
    }
    try {
      if (target.entry) {
        await update.mutateAsync({
          entryId: target.entry.id,
          hours,
          comment,
          task_id: taskId || null,
        });
      } else {
        await create.mutateAsync({
          project_id: target.projectId,
          task_id: taskId || null,
          work_date: target.date,
          hours,
          comment,
        });
      }
      toast.success("Сохранено");
      onClose();
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Ошибка сохранения");
    }
  };

  const remove = async () => {
    if (!target.entry) return;
    try {
      await del.mutateAsync(target.entry.id);
      toast.success("Запись удалена");
      onClose();
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Ошибка");
    }
  };

  return (
    <Dialog open={!!target} onOpenChange={(v) => !v && onClose()}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>
            {target.projectName} · {formatDate(target.date)}
          </DialogTitle>
        </DialogHeader>
        <div className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label>Часы</Label>
              <Input
                type="number"
                step="0.25"
                min="0"
                value={hours}
                onChange={(e) => setHours(e.target.value)}
                className="tabular"
                autoFocus
              />
            </div>
            <div className="space-y-1.5">
              <Label>Задача</Label>
              <Select value={taskId} onValueChange={setTaskId}>
                <SelectTrigger>
                  <SelectValue placeholder="—" />
                </SelectTrigger>
                <SelectContent>
                  {tasks?.map((t) => (
                    <SelectItem key={t.id} value={t.id}>
                      {t.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
          <div className="space-y-1.5">
            <Label>Комментарий *</Label>
            <Input
              value={comment}
              onChange={(e) => setComment(e.target.value)}
              placeholder="Что делали"
            />
          </div>
        </div>
        <DialogFooter className="gap-2">
          {target.entry && (
            <Button variant="ghost" className="mr-auto text-destructive" onClick={remove}>
              Удалить
            </Button>
          )}
          <Button variant="outline" onClick={onClose}>
            Отмена
          </Button>
          <Button onClick={save} disabled={create.isPending || update.isPending}>
            {(create.isPending || update.isPending) && <Spinner />} Сохранить
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
