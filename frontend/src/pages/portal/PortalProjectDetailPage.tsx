import { useQueryClient } from "@tanstack/react-query";
import { AlertTriangle, ArrowLeft, CheckCircle2, Paperclip, Upload } from "lucide-react";
import { useState } from "react";
import { Link, useParams } from "react-router-dom";

import {
  ACTION_STATUS_LABELS,
  ACTION_STATUS_VARIANT,
  type ActionItem,
  downloadPortalArtifact,
  uploadArtifact,
  usePortalProject,
  useProvide,
} from "@/entities/customer/api";
import { STAGE_LABELS } from "@/entities/project/model";
import { ApiError } from "@/shared/api/types";
import { formatDate } from "@/shared/lib/format";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardContent } from "@/shared/ui/card";
import { Input } from "@/shared/ui/input";
import { EmptyState, PageHeader } from "@/shared/ui/page";
import { PageLoader } from "@/shared/ui/spinner";
import { toast } from "@/shared/ui/toast";
import { StageStepper } from "@/pages/projects/StageStepper";

export function PortalProjectDetailPage() {
  const { projectId } = useParams<{ projectId: string }>();
  const { data: project, isLoading } = usePortalProject(projectId);

  if (isLoading || !project) return <PageLoader />;

  const open = project.action_items.filter((i) => i.status !== "accepted");
  const done = project.action_items.filter((i) => i.status === "accepted");

  return (
    <div>
      <Link
        to="/portal"
        className="mb-3 inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
      >
        <ArrowLeft className="h-4 w-4" /> К моим проектам
      </Link>
      <PageHeader title={project.name} description={`Проект ${project.code}`} />

      <div className="mb-5 rounded-lg border bg-card p-4">
        <StageStepper current={project.stage} />
      </div>

      {project.blocked_on_customer ? (
        <div className="mb-5 flex items-start gap-3 rounded-lg border border-warning/40 bg-warning/10 p-4">
          <AlertTriangle className="mt-0.5 h-5 w-5 text-warning" />
          <div>
            <p className="font-medium text-warning">Проект ожидает действий с вашей стороны</p>
            <p className="text-sm text-muted-foreground">
              Пока задачи ниже не выполнены, работы по проекту приостановлены.
            </p>
          </div>
        </div>
      ) : (
        <div className="mb-5 flex items-center gap-2 rounded-lg border border-success/40 bg-success/10 p-4 text-success">
          <CheckCircle2 className="h-5 w-5" />
          <span className="font-medium">От вас сейчас ничего не требуется</span>
        </div>
      )}

      <h2 className="mb-3 text-lg font-semibold">Требуется от вас</h2>
      {open.length === 0 ? (
        <EmptyState title="Открытых задач нет" />
      ) : (
        <div className="space-y-3">
          {open.map((item) => (
            <ActionCard key={item.id} item={item} projectId={project.project_id} />
          ))}
        </div>
      )}

      {done.length > 0 && (
        <>
          <h2 className="mb-3 mt-8 text-lg font-semibold text-muted-foreground">Принято</h2>
          <div className="space-y-2">
            {done.map((item) => (
              <div
                key={item.id}
                className="flex items-center justify-between rounded-lg border bg-card px-4 py-3 text-sm"
              >
                <span>{item.title}</span>
                <Badge variant="success">{ACTION_STATUS_LABELS[item.status]}</Badge>
              </div>
            ))}
          </div>
        </>
      )}
    </div>
  );
}

function ActionCard({ item, projectId }: { item: ActionItem; projectId: string }) {
  const provide = useProvide(projectId);
  const qc = useQueryClient();
  const [note, setNote] = useState("");
  const [uploading, setUploading] = useState(false);

  const onProvide = async () => {
    try {
      await provide.mutateAsync({ itemId: item.id, note: note || undefined });
      toast.success("Спасибо! Мы получили вашу отметку");
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Ошибка");
    }
  };

  const onUpload = async (file: File | undefined) => {
    if (!file) return;
    setUploading(true);
    try {
      await uploadArtifact(item.id, file);
      toast.success(`Файл «${file.name}» загружен`);
      void qc.invalidateQueries({ queryKey: ["portal-project", projectId] });
    } catch {
      toast.error("Не удалось загрузить файл");
    } finally {
      setUploading(false);
    }
  };

  return (
    <Card>
      <CardContent className="p-5">
        <div className="flex flex-wrap items-start justify-between gap-2">
          <div>
            <p className="font-medium">{item.title}</p>
            {item.description && (
              <p className="mt-1 text-sm text-muted-foreground">{item.description}</p>
            )}
            <p className="mt-2 text-xs text-muted-foreground">
              Ответственный: <span className="font-medium text-foreground">{item.responsible_name}</span>
              {item.stage && ` · стадия: ${STAGE_LABELS[item.stage]}`}
              {item.due_date && ` · срок: ${formatDate(item.due_date)}`}
            </p>
          </div>
          <Badge variant={ACTION_STATUS_VARIANT[item.status]}>
            {ACTION_STATUS_LABELS[item.status]}
          </Badge>
        </div>

        {item.artifacts.length > 0 && (
          <div className="mt-3 flex flex-wrap gap-2">
            {item.artifacts.map((a) => (
              <button
                key={a.id}
                type="button"
                onClick={() =>
                  downloadPortalArtifact(a.id, a.filename).catch((e) =>
                    toast.error(e instanceof ApiError ? e.message : "Не удалось скачать файл"),
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

        {item.status === "waiting" && (
          <div className="mt-4 flex flex-wrap items-center gap-2 border-t pt-3">
            <Input
              placeholder="Комментарий (необязательно)"
              value={note}
              onChange={(e) => setNote(e.target.value)}
              className="max-w-xs"
            />
            <label className="inline-flex">
              <input
                type="file"
                className="hidden"
                onChange={(e) => onUpload(e.target.files?.[0])}
              />
              <span className="inline-flex h-9 cursor-pointer items-center gap-1.5 rounded-md border border-input bg-card px-3 text-sm hover:bg-accent">
                <Upload className="h-4 w-4" /> {uploading ? "Загрузка…" : "Прикрепить файл"}
              </span>
            </label>
            <Button onClick={onProvide} disabled={provide.isPending}>
              Готово, передать
            </Button>
          </div>
        )}
        {item.status === "provided" && (
          <p className="mt-3 border-t pt-3 text-sm text-muted-foreground">
            Данные переданы, ожидают проверки руководителем проекта.
          </p>
        )}
      </CardContent>
    </Card>
  );
}
