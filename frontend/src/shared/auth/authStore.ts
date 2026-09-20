import { create } from "zustand";
import { persist } from "zustand/middleware";

interface AuthState {
  token: string | null;
  setToken: (token: string | null) => void;
  logout: () => void;
}

/** Хранилище access-токена. Персистится в localStorage, читается api-клиентом. */
export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      token: null,
      setToken: (token) => set({ token }),
      logout: () => set({ token: null }),
    }),
    { name: "psa-auth" },
  ),
);

export function getToken(): string | null {
  return useAuthStore.getState().token;
}
