import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/shared/api/client";

export interface Budget {
  id: string;
  project_id: string;
  planned_revenue: string;
  planned_costs: Record<string, string>;
}

export interface Margin {
  revenue: string;
  total_cost: string;
  margin: string;
  margin_pct: string;
  cost_breakdown: Record<string, string>;
}

export interface ActualCost {
  id: string;
  project_id: string;
  category: string;
  amount: string;
  source: string;
  occurred_on: string;
  description: string | null;
}

export interface ProjectFinance {
  project_id: string;
  budget: Budget | null;
  margin: Margin;
  forecast: { eac: string; etc: string; calculated_at: string | null };
  planned_hours: string;
  actual_hours: string;
  hours_overrun_pct: string;
}

export const COST_CATEGORY_LABELS: Record<string, string> = {
  payroll: "ФОТ",
  licenses: "Лицензии",
  subcontract: "Субподряд",
  travel: "Командировки",
  other: "Прочее",
};

export function useProjectFinance(projectId: string | undefined) {
  return useQuery({
    queryKey: ["finance", projectId],
    queryFn: () => api.get<ProjectFinance>(`/projects/${projectId}/finance`),
    enabled: !!projectId,
  });
}

export function useActuals(projectId: string | undefined) {
  return useQuery({
    queryKey: ["actuals", projectId],
    queryFn: () => api.get<ActualCost[]>(`/projects/${projectId}/finance/actuals`),
    enabled: !!projectId,
  });
}

export function useAddActual(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: {
      category: string;
      amount: string;
      occurred_on: string;
      description?: string;
    }) => api.post<ActualCost>(`/projects/${projectId}/finance/actuals`, input),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ["actuals", projectId] });
      void qc.invalidateQueries({ queryKey: ["finance", projectId] });
    },
  });
}

export function useUpsertBudget(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: { planned_revenue: string; planned_costs: Record<string, string> }) =>
      api.put<Budget>(`/projects/${projectId}/finance/budget`, input),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["finance", projectId] }),
  });
}
