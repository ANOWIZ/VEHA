import { Check, Paperclip, Plus } from "lucide-react";
import { useState } from "react";

import {
  ACTION_STATUS_LABELS,
  ACTION_STATUS_VARIANT,
  downloadProjectArtifact,
  useAcceptActionItem,
  useActionItems,
  useCreateActionItem,
} from "@/entities/customer/api";
import { STAGE_LABELS, type Stage } from "@/entities/project/model";
import { ApiError } from "@/shared/api/types";
import { formatDate } from "@/shared/lib/format";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Input } from "@/shared/ui/input";
import { EmptyState } from "@/shared/ui/page";
import { Card, CardContent } from "@/shared/ui/card";
import { toast } from "@/shared/ui/toast";

export function ProjectCustomerTab({
  projectId,
  canManage,
}: {
  projectId: string;
  canManage: boolean;
}) {
  const { data: items } = useActionItems(projectId);
  const create = useCreateActionItem(projectId);
  const accept = useAcceptActionItem(projectId);
  const [title, setTitle] = useState("");
  const [responsible, setResponsible] = useState("");
  const [due, setDue] = useState("");

  const openCount = items?.filter((i) => i.status === "waiting").length ?? 0;

  const add = async () => {
    if (!title.trim() || !responsible.trim()) {
      toast.error("Укажите задачу и ответственного со стороны Заказчика");
      return;
    }
    try {
      await create.mutateAsync({
        title,
        responsible_name: responsible,
        due_date: due ? new Date(due).toISOString() : null,
      });
      setTitle("");
      setResponsible("");
      setDue("");
      toast.success("Ожидание добавлено");
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Ошибка");
    }
  };

  return (
    <div className="space-y-4">
      {openCount > 0 && (
        <div className="rounded-md border border-warning/40 bg-warning/10 px-3 py-2 text-sm font-medium text-warning">
          Проект ожидает Заказчика: открытых задач — {openCount}
        </div>
      )}

      {canManage && (
        <div className="flex flex-wrap items-end gap-2 rounded-lg border bg-card p-3">
          <Input
            placeholder="Что требуется от Заказчика"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            className="min-w-[260px] flex-1"
          />
          <Input
            placeholder="Ответственный (Заказчик)"
            value={responsible}
            onChange={(e) => setResponsible(e.target.value)}
            className="w-56"
          />
          <Input type="date" value={due} onChange={(e) => setDue(e.target.value)} className="w-40" />
          <Button onClick={add} disabled={create.isPending}>
            <Plus className="h-4 w-4" /> Добавить
          </Button>
        </div>
      )}

      {!items || items.length === 0 ? (
        <EmptyState title="Ожиданий от Заказчика нет" />
      ) : (
        <div className="space-y-2">
          {items.map((item) => (
            <Card key={item.id}>
              <CardContent className="flex flex-wrap items-start justify-between gap-3 p-4">
                <div>
                  <p className="font-medium">{item.title}</p>
                  <p className="mt-1 text-xs text-muted-foreground">
                    Ответственный: {item.responsible_name}
                    {item.stage && ` · стадия: ${STAGE_LABELS[item.stage as Stage]}`}
                    {item.due_date && ` · срок: ${formatDate(item.due_date)}`}
                  </p>
                  {item.provided_note && (
                    <p className="mt-1 text-sm">Комментарий Заказчика: {item.provided_note}</p>
                  )}
                  {item.artifacts.length > 0 && (
                    <div className="mt-2 flex flex-wrap gap-2">
                      {item.artifacts.map((a) => (
                        <button
                          key={a.id}
                          type="button"
                          onClick={() =>
                            downloadProjectArtifact(projectId, a.id, a.filename).catch((e) =>
                              toast.error(
                                e instanceof ApiError ? e.message : "Не удалось скачать файл",
                              ),
                            )
                          }
                          className="inline-flex items-center gap-1 rounded-md border bg-muted/40 px-2 py-1 text-xs hover:bg-accent"
                        >
                          <Paperclip className="h-3 w-3" />
                          {a.filename}
                        </button>
                      ))}
                    </div>
                  )}
                </div>
                <div className="flex items-center gap-2">
                  <Badge variant={ACTION_STATUS_VARIANT[item.status]}>
                    {ACTION_STATUS_LABELS[item.status]}
                  </Badge>
                  {canManage && item.status === "provided" && (
                    <Button size="sm" variant="success" onClick={() => accept.mutate(item.id)}>
                      <Check className="h-4 w-4" /> Принять
                    </Button>
                  )}
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
