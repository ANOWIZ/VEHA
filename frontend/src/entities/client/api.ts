import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/shared/api/client";
import type { Page } from "@/shared/api/types";

export interface Client {
  id: string;
  name: string;
  inn: string | null;
  industry: string | null;
  contacts: Record<string, unknown>;
  is_kii: boolean;
}

export function useClients(q?: string) {
  return useQuery({
    queryKey: ["clients", q ?? ""],
    queryFn: () => api.get<Page<Client>>("/clients", { q, limit: 200 }),
  });
}

export interface ClientInput {
  name: string;
  inn?: string | null;
  industry?: string | null;
  is_kii?: boolean;
}

export function useCreateClient() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: ClientInput) => api.post<Client>("/clients", input),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["clients"] }),
  });
}

export function useUpdateClient() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...input }: { id: string } & ClientInput) =>
      api.patch<Client>(`/clients/${id}`, input),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["clients"] }),
  });
}

export function useDeleteClient() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete(`/clients/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["clients"] }),
  });
}
