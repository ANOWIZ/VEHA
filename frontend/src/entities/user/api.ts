import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/shared/api/client";
import type { Page } from "@/shared/api/types";
import type { User } from "./model";

export interface UserWithRate extends User {
  current_cost_rate: string | null;
}

export function useUsers() {
  return useQuery({
    queryKey: ["users"],
    queryFn: () => api.get<Page<User>>("/users", { limit: 500 }),
  });
}

export function useUser(userId: string | undefined) {
  return useQuery({
    queryKey: ["user", userId],
    queryFn: () => api.get<UserWithRate>(`/users/${userId}`),
    enabled: !!userId,
  });
}

export function useSetCostRate(userId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: { cost_rate: string; valid_from: string; valid_to?: string | null }) =>
      api.post(`/users/${userId}/cost-rates`, input),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["user", userId] }),
  });
}

/** Удобный словарь id → пользователь для подстановки имён. */
export function useUserMap() {
  const { data } = useUsers();
  const map = new Map<string, User>();
  data?.items.forEach((u) => map.set(u.id, u));
  return map;
}
