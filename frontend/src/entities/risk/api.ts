import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/shared/api/client";
import type { ProjectStatus, Stage } from "@/entities/project/model";

export type RiskCategory =
  | "technical"
  | "schedule"
  | "budget"
  | "resource"
  | "scope"
  | "external"
  | "organizational";

export type RiskStatus = "open" | "mitigating" | "accepted" | "closed" | "realized";
export type RiskResponse = "avoid" | "mitigate" | "transfer" | "accept";
export type RiskLevel = "low" | "medium" | "high";

export const RISK_CATEGORY_LABELS: Record<RiskCategory, string> = {
  technical: "Технический",
  schedule: "Сроки",
  budget: "Бюджет",
  resource: "Ресурсы",
  scope: "Содержание",
  external: "Внешний",
  organizational: "Организационный",
};

export const RISK_STATUS_LABELS: Record<RiskStatus, string> = {
  open: "Открыт",
  mitigating: "В работе",
  accepted: "Принят",
  closed: "Закрыт",
  realized: "Реализовался",
};

export const RISK_STATUS_VARIANT: Record<
  RiskStatus,
  "default" | "secondary" | "success" | "warning" | "destructive"
> = {
  open: "warning",
  mitigating: "default",
  accepted: "secondary",
  closed: "success",
  realized: "destructive",
};

export const RISK_RESPONSE_LABELS: Record<RiskResponse, string> = {
  avoid: "Уклонение",
  mitigate: "Снижение",
  transfer: "Передача",
  accept: "Принятие",
};

export const RISK_LEVEL_LABELS: Record<RiskLevel, string> = {
  low: "Низкий",
  medium: "Средний",
  high: "Высокий",
};

export const RISK_LEVEL_VARIANT: Record<RiskLevel, "success" | "warning" | "destructive"> = {
  low: "success",
  medium: "warning",
  high: "destructive",
};

/** Цвета зон матрицы (Tailwind-классы фона/текста) по уровню. */
export const RISK_LEVEL_CELL: Record<RiskLevel, string> = {
  low: "bg-success/15 text-success",
  medium: "bg-warning/15 text-warning",
  high: "bg-destructive/15 text-destructive",
};

export const PROBABILITY_LABELS: Record<number, string> = {
  1: "Низкая",
  2: "Средняя",
  3: "Высокая",
};

export const IMPACT_LABELS: Record<number, string> = {
  1: "Низкое",
  2: "Среднее",
  3: "Высокое",
};

export interface Risk {
  id: string;
  project_id: string;
  title: string;
  description: string | null;
  category: RiskCategory;
  probability: number;
  impact: number;
  score: number;
  level: RiskLevel;
  status: RiskStatus;
  response_strategy: RiskResponse;
  mitigation_plan: string | null;
  owner_id: string | null;
  due_date: string | null;
  closed_at: string | null;
  created_by: string | null;
  created_at: string;
  updated_at: string;
}

export interface RiskInput {
  title: string;
  description?: string | null;
  category: RiskCategory;
  probability: number;
  impact: number;
  response_strategy?: RiskResponse;
  mitigation_plan?: string | null;
  owner_id?: string | null;
  due_date?: string | null;
}

export interface RiskMatrixCell {
  probability: number;
  impact: number;
  score: number;
  level: RiskLevel;
  count: number;
}

export interface RiskCategoryCount {
  category: RiskCategory;
  count: number;
}

export interface ProjectRiskRow {
  project_id: string;
  code: string;
  name: string;
  stage: Stage;
  status: ProjectStatus;
  manager_id: string;
  active_count: number;
  high_count: number;
  top_score: number;
}

export interface RiskPortfolio {
  projects_count: number;
  total_active: number;
  total_high: number;
  matrix: RiskMatrixCell[];
  by_category: RiskCategoryCount[];
  rows: ProjectRiskRow[];
}

// ---------- Риски проекта ----------
export function useRisks(projectId: string | undefined) {
  return useQuery({
    queryKey: ["risks", projectId],
    queryFn: () => api.get<Risk[]>(`/projects/${projectId}/risks`),
    enabled: !!projectId,
  });
}

export function useCreateRisk(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: RiskInput) => api.post<Risk>(`/projects/${projectId}/risks`, input),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ["risks", projectId] });
      void qc.invalidateQueries({ queryKey: ["risk-portfolio"] });
    },
  });
}

export function useUpdateRisk(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ riskId, ...input }: { riskId: string } & Partial<RiskInput> & { status?: RiskStatus }) =>
      api.patch<Risk>(`/projects/${projectId}/risks/${riskId}`, input),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ["risks", projectId] });
      void qc.invalidateQueries({ queryKey: ["risk-portfolio"] });
    },
  });
}

export function useDeleteRisk(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (riskId: string) => api.delete(`/projects/${projectId}/risks/${riskId}`),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ["risks", projectId] });
      void qc.invalidateQueries({ queryKey: ["risk-portfolio"] });
    },
  });
}

// ---------- Портфель рисков ----------
export function useRiskPortfolio() {
  return useQuery({
    queryKey: ["risk-portfolio"],
    queryFn: () => api.get<RiskPortfolio>("/risks/portfolio"),
  });
}
