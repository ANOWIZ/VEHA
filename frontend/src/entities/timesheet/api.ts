import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/shared/api/client";

export type TimeEntryStatus = "draft" | "submitted" | "approved" | "rejected";

export const TS_STATUS_LABELS: Record<TimeEntryStatus, string> = {
  draft: "Черновик",
  submitted: "На утверждении",
  approved: "Утверждено",
  rejected: "Отклонено",
};

export const TS_STATUS_VARIANT: Record<
  TimeEntryStatus,
  "default" | "secondary" | "success" | "warning" | "destructive"
> = {
  draft: "secondary",
  submitted: "warning",
  approved: "success",
  rejected: "destructive",
};

export interface TimeEntry {
  id: string;
  user_id: string;
  project_id: string;
  task_id: string | null;
  work_date: string;
  hours: string;
  comment: string;
  status: TimeEntryStatus;
  approved_by: string | null;
  approved_at: string | null;
  reject_reason: string | null;
  reversal_of: string | null;
}

export interface WeekResponse {
  week_start: string;
  week_end: string;
  status: TimeEntryStatus;
  entries: TimeEntry[];
  total_hours: string;
  daily_totals: Record<string, string>;
}

export interface PendingEntry extends TimeEntry {
  user_name: string | null;
  project_code: string | null;
}

export function useWeek(weekStart: string) {
  return useQuery({
    queryKey: ["ts-week", weekStart],
    queryFn: () => api.get<WeekResponse>("/timesheets/week", { week_start: weekStart }),
  });
}

export function useCreateEntry() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: {
      project_id: string;
      task_id?: string | null;
      work_date: string;
      hours: string;
      comment: string;
    }) => api.post<TimeEntry>("/timesheets/entries", input),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["ts-week"] }),
  });
}

export function useUpdateEntry() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({
      entryId,
      ...input
    }: {
      entryId: string;
      hours?: string;
      comment?: string;
      task_id?: string | null;
    }) => api.patch<TimeEntry>(`/timesheets/entries/${entryId}`, input),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["ts-week"] }),
  });
}

export function useDeleteEntry() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (entryId: string) => api.delete(`/timesheets/entries/${entryId}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["ts-week"] }),
  });
}

export function useSubmitWeek() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (weekStart: string) =>
      api.post<{ message: string }>("/timesheets/submit", { week_start: weekStart }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["ts-week"] }),
  });
}

export function useCopyWeek() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (vars: { source_week_start: string; target_week_start: string }) =>
      api.post<{ message: string }>("/timesheets/copy", vars),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["ts-week"] }),
  });
}

export function usePending() {
  return useQuery({
    queryKey: ["ts-pending"],
    queryFn: () => api.get<PendingEntry[]>("/timesheets/pending"),
  });
}

export function useApprove() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (entryIds: string[]) =>
      api.post<{ message: string }>("/timesheets/approve", { entry_ids: entryIds }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["ts-pending"] }),
  });
}

export function useReject() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (vars: { entry_ids: string[]; reason: string }) =>
      api.post<{ message: string }>("/timesheets/reject", vars),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["ts-pending"] }),
  });
}
