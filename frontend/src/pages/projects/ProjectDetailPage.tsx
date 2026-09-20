import { ArrowLeft, ChevronRight, RotateCcw } from "lucide-react";
import { useState } from "react";
import { Link, useParams } from "react-router-dom";

import {
  PROJECT_TYPE_LABELS,
  STAGE_LABELS,
  STATUS_LABELS,
  STATUS_VARIANT,
  nextStage,
  prevStage,
} from "@/entities/project/model";
import {
  useChangeStage,
  useProject,
  useStageHistory,
} from "@/entities/project/api";
import { useUserMap } from "@/entities/user/api";
import { hasAnyRole } from "@/entities/user/model";
import { ApiError } from "@/shared/api/types";
import { useMe } from "@/shared/auth/useAuth";
import { formatDate, formatMoney } from "@/shared/lib/format";
import { Badge } from "@/shared/ui/badge";
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
import { EmptyState, StatCard } from "@/shared/ui/page";
import { PageLoader } from "@/shared/ui/spinner";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/shared/ui/tabs";
import { toast } from "@/shared/ui/toast";
import { ProjectTasksTab } from "./ProjectTasksTab";
import { ProjectMembersTab } from "./ProjectMembersTab";
import { ProjectMilestonesTab } from "./ProjectMilestonesTab";
import { ProjectCustomerTab } from "./ProjectCustomerTab";
import { ProjectRisksTab } from "./ProjectRisksTab";
import { StageStepper } from "./StageStepper";

export function ProjectDetailPage() {
  const { projectId } = useParams<{ projectId: string }>();
  const { data: project, isLoading, isError } = useProject(projectId);
  const { data: history } = useStageHistory(projectId);
  const { data: me } = useMe();
  const users = useUserMap();
  const changeStage = useChangeStage(projectId!);
  const [rollbackOpen, setRollbackOpen] = useState(false);
  const [reason, setReason] = useState("");

  if (isLoading) return <PageLoader />;
  if (isError || !project)
    return <EmptyState title="Не удалось загрузить проект" description="Попробуйте обновить страницу." />;

  const canManage =
    hasAnyRole(me, ["admin"]) || project.manager_id === me?.id || project.curator_id === me?.id;
  const fwd = nextStage(project.stage);
  const back = prevStage(project.stage);

  const advance = async () => {
    if (!fwd) return;
    try {
      await changeStage.mutateAsync({ to_stage: fwd });
      toast.success(`Стадия: ${STAGE_LABELS[fwd]}`);
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Ошибка перехода");
    }
  };

  const rollback = async () => {
    if (!back || !reason.trim()) {
      toast.error("Укажите причину отката");
      return;
    }
    try {
      await changeStage.mutateAsync({ to_stage: back, reason });
      toast.success(`Откат на стадию: ${STAGE_LABELS[back]}`);
      setRollbackOpen(false);
      setReason("");
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Ошибка отката");
    }
  };

  return (
    <div>
      <Link
        to="/projects"
        className="mb-3 inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
      >
        <ArrowLeft className="h-4 w-4" /> К списку проектов
      </Link>

      <div className="mb-5 flex flex-wrap items-start justify-between gap-3">
        <div>
          <div className="flex items-center gap-2">
            <span className="font-mono text-sm text-muted-foreground">{project.code}</span>
            <Badge variant={STATUS_VARIANT[project.status]}>
              {STATUS_LABELS[project.status]}
            </Badge>
          </div>
          <h1 className="mt-1 text-2xl font-semibold">{project.name}</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            {PROJECT_TYPE_LABELS[project.type]} · РП: {users.get(project.manager_id)?.full_name ?? "—"}
          </p>
        </div>
        {canManage && (
          <div className="flex gap-2">
            {back && (
              <Button variant="outline" onClick={() => setRollbackOpen(true)}>
                <RotateCcw className="h-4 w-4" /> Откат
              </Button>
            )}
            {fwd && (
              <Button onClick={advance} disabled={changeStage.isPending}>
                {STAGE_LABELS[fwd]} <ChevronRight className="h-4 w-4" />
              </Button>
            )}
          </div>
        )}
      </div>

      <div className="mb-6 rounded-lg border bg-card p-4">
        <StageStepper current={project.stage} />
      </div>

      <div className="mb-6 grid grid-cols-2 gap-4 lg:grid-cols-4">
        <StatCard label="Бюджет (выручка)" value={formatMoney(project.budget_revenue)} />
        <StatCard
          label="План старта"
          value={formatDate(project.planned_start)}
          hint={`Факт: ${formatDate(project.actual_start)}`}
        />
        <StatCard
          label="План завершения"
          value={formatDate(project.planned_end)}
          hint={`Факт: ${formatDate(project.actual_end)}`}
        />
        <StatCard label="Участников" value={project.members.length} />
      </div>

      <Tabs defaultValue="tasks">
        <TabsList>
          <TabsTrigger value="tasks">Задачи</TabsTrigger>
          <TabsTrigger value="members">Участники</TabsTrigger>
          <TabsTrigger value="milestones">Вехи</TabsTrigger>
          <TabsTrigger value="risks">Риски</TabsTrigger>
          <TabsTrigger value="customer">Заказчик</TabsTrigger>
          <TabsTrigger value="history">История стадий</TabsTrigger>
        </TabsList>

        <TabsContent value="tasks">
          <ProjectTasksTab projectId={project.id} canManage={canManage} />
        </TabsContent>
        <TabsContent value="members">
          <ProjectMembersTab project={project} canManage={canManage} />
        </TabsContent>
        <TabsContent value="milestones">
          <ProjectMilestonesTab project={project} canManage={canManage} />
        </TabsContent>
        <TabsContent value="risks">
          <ProjectRisksTab projectId={project.id} canManage={canManage} />
        </TabsContent>
        <TabsContent value="customer">
          <ProjectCustomerTab projectId={project.id} canManage={canManage} />
        </TabsContent>
        <TabsContent value="history">
          <div className="rounded-lg border bg-card divide-y">
            {(history ?? []).map((t) => (
              <div key={t.id} className="flex items-center justify-between p-3 text-sm">
                <div>
                  <span className="font-medium">
                    {t.from_stage ? STAGE_LABELS[t.from_stage] : "—"} →{" "}
                    {STAGE_LABELS[t.to_stage]}
                  </span>
                  {t.reason && (
                    <span className="ml-2 text-muted-foreground">· {t.reason}</span>
                  )}
                </div>
                <span className="text-muted-foreground">{formatDate(t.created_at)}</span>
              </div>
            ))}
            {(history ?? []).length === 0 && (
              <p className="p-4 text-sm text-muted-foreground">Нет переходов</p>
            )}
          </div>
        </TabsContent>
      </Tabs>

      <Dialog open={rollbackOpen} onOpenChange={setRollbackOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Откат на предыдущую стадию</DialogTitle>
          </DialogHeader>
          <p className="text-sm text-muted-foreground">
            {STAGE_LABELS[project.stage]} → {back ? STAGE_LABELS[back] : ""}. Укажите причину —
            она сохранится в истории.
          </p>
          <div className="space-y-1.5">
            <Label>Причина</Label>
            <Input value={reason} onChange={(e) => setReason(e.target.value)} />
          </div>
          <DialogFooter className="gap-2">
            <Button variant="outline" onClick={() => setRollbackOpen(false)}>
              Отмена
            </Button>
            <Button variant="destructive" onClick={rollback}>
              Откатить
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
