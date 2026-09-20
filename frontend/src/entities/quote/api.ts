import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/shared/api/client";
import { downloadFile } from "@/shared/api/download";
import type { Quote, QuoteDetail, QuoteLineInput, QuoteStatus } from "./model";

export function useQuotes(projectId: string | undefined) {
  return useQuery({
    queryKey: ["quotes", projectId],
    queryFn: () => api.get<Quote[]>(`/projects/${projectId}/quotes`),
    enabled: !!projectId,
  });
}

export function useQuote(projectId: string | undefined, quoteId: string | undefined) {
  return useQuery({
    queryKey: ["quote", quoteId],
    queryFn: () => api.get<QuoteDetail>(`/projects/${projectId}/quotes/${quoteId}`),
    enabled: !!projectId && !!quoteId,
  });
}

export function useCreateQuote(projectId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: {
      title?: string;
      currency_rates?: Record<string, string>;
      currency_buffer_pct?: string;
    }) => api.post<QuoteDetail>(`/projects/${projectId}/quotes`, input),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["quotes", projectId] }),
  });
}

function invalidateQuote(qc: ReturnType<typeof useQueryClient>, projectId: string, quoteId: string) {
  void qc.invalidateQueries({ queryKey: ["quote", quoteId] });
  void qc.invalidateQueries({ queryKey: ["quotes", projectId] });
}

export function useAddLine(projectId: string, quoteId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: QuoteLineInput) =>
      api.post<QuoteDetail>(`/projects/${projectId}/quotes/${quoteId}/lines`, input),
    onSuccess: () => invalidateQuote(qc, projectId, quoteId),
  });
}

export function useUpdateLine(projectId: string, quoteId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ lineId, ...input }: { lineId: string } & QuoteLineInput) =>
      api.put<QuoteDetail>(`/projects/${projectId}/quotes/${quoteId}/lines/${lineId}`, input),
    onSuccess: () => invalidateQuote(qc, projectId, quoteId),
  });
}

export function useDeleteLine(projectId: string, quoteId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (lineId: string) =>
      api.delete<QuoteDetail>(`/projects/${projectId}/quotes/${quoteId}/lines/${lineId}`),
    onSuccess: () => invalidateQuote(qc, projectId, quoteId),
  });
}

export function useSetQuoteStatus(projectId: string, quoteId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (status: QuoteStatus) =>
      api.post<Quote>(`/projects/${projectId}/quotes/${quoteId}/status`, { status }),
    onSuccess: () => invalidateQuote(qc, projectId, quoteId),
  });
}

export function useCloneQuote(projectId: string, quoteId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => api.post<QuoteDetail>(`/projects/${projectId}/quotes/${quoteId}/clone`, {}),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["quotes", projectId] }),
  });
}

/** Скачать XLSX-спецификацию (Bearer-авторизация, 401→logout, ApiError). */
export function downloadQuoteXlsx(projectId: string, quoteId: string, filename: string) {
  return downloadFile(`/projects/${projectId}/quotes/${quoteId}/export.xlsx`, filename);
}
