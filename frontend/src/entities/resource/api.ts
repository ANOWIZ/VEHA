import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/shared/api/client";

export interface HeatmapCell {
  week_start: string;
  planned_hours: string;
  utilization_pct: string;
  load: "over" | "under" | "ok";
}

export interface HeatmapRow {
  user_id: string;
  user_name: string;
  cells: HeatmapCell[];
  total_hours: string;
}

export interface Heatmap {
  weeks: string[];
  norm_hours: string;
  rows: HeatmapRow[];
}

export function useHeatmap(weekFrom: string, weeks = 8) {
  return useQuery({
    queryKey: ["heatmap", weekFrom, weeks],
    queryFn: () => api.get<Heatmap>("/resources/heatmap", { week_from: weekFrom, weeks }),
  });
}

export function useUpsertPlan() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: {
      user_id: string;
      project_id: string;
      week_start: string;
      planned_hours: string;
    }) => api.post("/resources/plan", input),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["heatmap"] }),
  });
}
