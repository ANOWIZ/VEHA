import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";

import { ApiError } from "@/shared/api/types";
import { Toaster } from "@/shared/ui/toast";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: (count, error) => {
        // Не повторяем при ошибках клиента (4xx).
        if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
          return false;
        }
        return count < 2;
      },
      staleTime: 30_000,
      refetchOnWindowFocus: false,
    },
  },
});

export function AppProviders({ children }: { children: ReactNode }) {
  return (
    <QueryClientProvider client={queryClient}>
      {children}
      <Toaster />
    </QueryClientProvider>
  );
}
