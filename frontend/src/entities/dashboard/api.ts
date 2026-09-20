import { useQuery } from "@tanstack/react-query";

import { api } from "@/shared/api/client";
import type { ProjectStatus, Stage } from "@/entities/project/model";

export interface PortfolioRow {
  project_id: string;
  code: string;
  name: string;
  stage: Stage;
  status: ProjectStatus;
  manager_id: string;
  revenue: string;
  margin: string;
  margin_pct: string;
  planned_hours: string;
  actual_hours: string;
  hours_overrun_pct: string;
  risks: string[];
}

export interface Portfolio {
  projects_count: number;
  total_revenue: string;
  total_margin: string;
  avg_margin_pct: string;
  at_risk: number;
  rows: PortfolioRow[];
}

export const RISK_LABELS: Record<string, string> = {
  low_margin: "Низкая маржа",
  hours_overrun: "Перерасход часов",
};

export function usePortfolio(enabled = true) {
  return useQuery({
    queryKey: ["portfolio"],
    queryFn: () => api.get<Portfolio>("/dashboards/portfolio"),
    enabled,
  });
}
