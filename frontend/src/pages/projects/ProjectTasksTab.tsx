import { Plus } from "lucide-react";
import { useState } from "react";

import { STAGE_LABELS, type Stage } from "@/entities/project/model";
import {
  TASK_STATUS_LABELS,
  type TaskStatus,
  useCreateTask,
  useTasks,
  useUpdateTask,
} from "@/entities/task/api";
import { useUserMap } from "@/entities/user/api";
import { ApiError } from "@/shared/api/types";
import { formatHours } from "@/shared/lib/format";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Input } from "@/shared/ui/input";
import { EmptyState } from "@/shared/ui/page";
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

const STATUS_VARIANT: Record<TaskStatus, "default" | "secondary" | "success" | "warning"> = {
  open: "secondary",
  in_progress: "warning",
  done: "success",
  cancelled: "secondary",
};

export function ProjectTasksTab({
  projectId,
  canManage,
}: {
  projectId: string;
  canManage: boolean;
}) {
  const { data: tasks } = useTasks(projectId);
  const create = useCreateTask(projectId);
  const update = useUpdateTask(projectId);
  const users = useUserMap();
  const [name, setName] = useState("");
  const [hours, setHours] = useState("");

  const add = async () => {
    if (!name.trim()) return;
    try {
      await create.mutateAsync({ name, planned_hours: hours || "0" });
      setName("");
      setHours("");
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Ошибка");
    }
  };

  return (
    <div className="space-y-4">
      {canManage && (
        <div className="flex gap-2">
          <Input
            placeholder="Новая задача…"
            value={name}
            onChange={(e) => setName(e.target.value)}
            className="max-w-md"
          />
          <Input
            placeholder="План, ч"
            type="number"
            value={hours}
            onChange={(e) => setHours(e.target.value)}
            className="w-28 tabular"
          />
          <Button onClick={add} disabled={create.isPending}>
            <Plus className="h-4 w-4" /> Добавить
          </Button>
        </div>
      )}

      {!tasks || tasks.length === 0 ? (
        <EmptyState title="Задач пока нет" />
      ) : (
        <div className="rounded-lg border bg-card">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Задача</TableHead>
                <TableHead>Стадия</TableHead>
                <TableHead>Исполнитель</TableHead>
                <TableHead className="text-right">План, ч</TableHead>
                <TableHead>Статус</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {tasks.map((t) => (
                <TableRow key={t.id}>
                  <TableCell className="font-medium">{t.name}</TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {t.stage ? STAGE_LABELS[t.stage as Stage] : "—"}
                  </TableCell>
                  <TableCell className="text-sm">
                    {t.assignee_id ? users.get(t.assignee_id)?.full_name ?? "—" : "—"}
                  </TableCell>
                  <TableCell className="text-right tabular">{formatHours(t.planned_hours)}</TableCell>
                  <TableCell>
                    {canManage ? (
                      <Select
                        value={t.status}
                        onValueChange={(v) =>
                          update.mutate({ taskId: t.id, status: v as TaskStatus })
                        }
                      >
                        <SelectTrigger className="h-7 w-36">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {Object.entries(TASK_STATUS_LABELS).map(([v, l]) => (
                            <SelectItem key={v} value={v}>
                              {l}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    ) : (
                      <Badge variant={STATUS_VARIANT[t.status]}>
                        {TASK_STATUS_LABELS[t.status]}
                      </Badge>
                    )}
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
