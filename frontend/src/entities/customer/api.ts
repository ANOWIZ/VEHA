import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/shared/api/client";
import { downloadFile } from "@/shared/api/download";
import { env } from "@/shared/config/env";
import { getToken } from "@/shared/auth/authStore";
import type { ProjectStatus, Stage } from "@/entities/project/model";

export type ActionStatus = "waiting" | "provided" | "accepted";

export const ACTION_STATUS_LABELS: Record<ActionStatus, string> = {
  waiting: "Ждём вас",
  provided: "Предоставлено",
  accepted: "Принято",
};

export const ACTION_STATUS_VARIANT: Record<
  ActionStatus,
  "default" | "secondary" | "success" | "warning" | "destructive"
> = {
  waiting: "warning",
  provided: "default",
  accepted: "success",
};

export interface Artifact {
  id: string;
  filename: string;
  content_type: string | null;
  size_bytes: number;
  created_at: string;
  uploaded_by: string | null;
}

export interface ActionItem {
  id: string;
  project_id: string;
  stage: Stage | null;
  title: string;
  description: string | null;
  responsible_name: string;
  responsible_email: string | null;
  responsible_user_id: string | null;
  due_date: string | null;
  status: ActionStatus;
  provided_at: string | null;
  accepted_at: string | null;
  provided_note: string | null;
  created_at: string;
  artifacts: Artifact[];
}

export interface PortalProject {
  project_id: string;
  code: string;
  name: string;
  stage: Stage;
  status: ProjectStatus;
  planned_end: string | null;
  open_actions: number;
  blocked_on_customer: boolean;
}

export interface PortalProjectDetail {
  project_id: string;
  code: string;
  name: string;
  stage: Stage;
  status: ProjectStatus;
  blocked_on_customer: boolean;
  action_items: ActionItem[];
}

// ---------- Портал Заказчика ----------
export function usePortalProjects() {
  return useQuery({
    queryKey: ["portal-projects"],
    queryFn: () => api.get<PortalProject[]>("/portal/projects"),
  });
}

export function usePortalProject(projectId: string | undefined) {
  return useQuery({
    queryKey: ["portal-project", projectId],
    queryFn: () => api.get<PortalProjectDetail>(`/portal/projects/${projectId}`),
    enabled: !!projectId,
  });
}

export function useProvide(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (vars: { itemId: string; note?: string }) =>
      api.post<ActionItem>(`/portal/action-items/${vars.itemId}/provide`, { note: vars.note }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ["portal-project", projectId] });
      void qc.invalidateQueries({ queryKey: ["portal-projects"] });
    },
  });
}

export async function uploadArtifact(itemId: string, file: File): Promise<void> {
  const fd = new FormData();
  fd.append("file", file);
  const resp = await fetch(`${env.apiBaseUrl}/portal/action-items/${itemId}/artifacts`, {
    method: "POST",
    headers: { Authorization: `Bearer ${getToken() ?? ""}` },
    body: fd,
  });
  if (!resp.ok) throw new Error("Не удалось загрузить файл");
}

/** Скачать артефакт со стороны портала Заказчика (роль client). */
export function downloadPortalArtifact(artifactId: string, filename: string): Promise<void> {
  return downloadFile(`/portal/artifacts/${artifactId}/download`, filename);
}

/** Скачать артефакт со внутренней стороны (РП, вкладка «Заказчик»). */
export function downloadProjectArtifact(
  projectId: string,
  artifactId: string,
  filename: string,
): Promise<void> {
  return downloadFile(
    `/projects/${projectId}/action-items/artifacts/${artifactId}/download`,
    filename,
  );
}

// ---------- Внутренняя сторона (РП) ----------
export function useActionItems(projectId: string | undefined) {
  return useQuery({
    queryKey: ["action-items", projectId],
    queryFn: () => api.get<ActionItem[]>(`/projects/${projectId}/action-items`),
    enabled: !!projectId,
  });
}

export function useCreateActionItem(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: {
      title: string;
      description?: string;
      stage?: Stage | null;
      responsible_name: string;
      responsible_email?: string;
      due_date?: string | null;
    }) => api.post<ActionItem>(`/projects/${projectId}/action-items`, input),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["action-items", projectId] }),
  });
}

export function useAcceptActionItem(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (itemId: string) =>
      api.post<ActionItem>(`/projects/${projectId}/action-items/${itemId}/accept`, {}),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["action-items", projectId] }),
  });
}
