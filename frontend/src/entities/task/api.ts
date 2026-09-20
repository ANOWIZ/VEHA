import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/shared/api/client";
import type { Stage } from "@/entities/project/model";

export type TaskStatus = "open" | "in_progress" | "done" | "cancelled";

export const TASK_STATUS_LABELS: Record<TaskStatus, string> = {
  open: "Открыта",
  in_progress: "В работе",
  done: "Выполнена",
  cancelled: "Отменена",
};

export interface Task {
  id: string;
  project_id: string;
  parent_id: string | null;
  name: string;
  stage: Stage | null;
  planned_hours: string;
  assignee_id: string | null;
  status: TaskStatus;
  due_date: string | null;
}

export function useTasks(projectId: string | undefined) {
  return useQuery({
    queryKey: ["tasks", projectId],
    queryFn: () => api.get<Task[]>(`/projects/${projectId}/tasks`),
    enabled: !!projectId,
  });
}

export function useCreateTask(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: {
      name: string;
      stage?: Stage | null;
      planned_hours?: string;
      assignee_id?: string | null;
      due_date?: string | null;
    }) => api.post<Task>(`/projects/${projectId}/tasks`, input),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["tasks", projectId] }),
  });
}

export function useUpdateTask(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ taskId, ...input }: { taskId: string } & Partial<Task>) =>
      api.patch<Task>(`/projects/${projectId}/tasks/${taskId}`, input),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["tasks", projectId] }),
  });
}
