import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/shared/api/client";
import type { Page } from "@/shared/api/types";
import type {
  Milestone,
  Project,
  ProjectDetail,
  ProjectMember,
  ProjectStatus,
  Stage,
  StageTransition,
} from "./model";

export interface ProjectFilters {
  status?: ProjectStatus;
  stage?: Stage;
  manager_id?: string;
  q?: string;
  limit?: number;
  offset?: number;
}

export function useProjects(filters: ProjectFilters = {}) {
  return useQuery({
    queryKey: ["projects", filters],
    queryFn: () => api.get<Page<Project>>("/projects", filters),
  });
}

export function useProject(projectId: string | undefined) {
  return useQuery({
    queryKey: ["project", projectId],
    queryFn: () => api.get<ProjectDetail>(`/projects/${projectId}`),
    enabled: !!projectId,
  });
}

export interface ProjectCreateInput {
  name: string;
  client_id: string;
  type: string;
  manager_id: string;
  curator_id?: string | null;
  planned_start?: string | null;
  planned_end?: string | null;
  budget_revenue?: string;
  contract_ref?: string | null;
}

export function useCreateProject() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: ProjectCreateInput) => api.post<Project>("/projects", input),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["projects"] }),
  });
}

export function useUpdateProject(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: Partial<ProjectCreateInput> & { status?: ProjectStatus }) =>
      api.patch<Project>(`/projects/${projectId}`, input),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ["project", projectId] });
      void qc.invalidateQueries({ queryKey: ["projects"] });
    },
  });
}

export function useChangeStage(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (vars: { to_stage: Stage; reason?: string }) =>
      api.post<Project>(`/projects/${projectId}/stage`, vars),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ["project", projectId] });
      void qc.invalidateQueries({ queryKey: ["project-transitions", projectId] });
    },
  });
}

export function useStageHistory(projectId: string | undefined) {
  return useQuery({
    queryKey: ["project-transitions", projectId],
    queryFn: () => api.get<StageTransition[]>(`/projects/${projectId}/transitions`),
    enabled: !!projectId,
  });
}

export function useAddMember(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: {
      user_id: string;
      role: string;
      bill_rate?: string;
      period_from?: string | null;
      period_to?: string | null;
    }) => api.post<ProjectMember>(`/projects/${projectId}/members`, input),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["project", projectId] }),
  });
}

export function useRemoveMember(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (memberId: string) =>
      api.delete(`/projects/${projectId}/members/${memberId}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["project", projectId] }),
  });
}

export function useAddMilestone(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: {
      name: string;
      milestone_date: string;
      is_payment?: boolean;
      amount?: string;
    }) => api.post<Milestone>(`/projects/${projectId}/milestones`, input),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["project", projectId] }),
  });
}
