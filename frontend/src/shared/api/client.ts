import axios, { AxiosError, type AxiosInstance } from "axios";

import { env } from "@/shared/config/env";
import { useAuthStore, getToken } from "@/shared/auth/authStore";
import { ApiError, type ApiErrorBody } from "./types";

/** Axios-инстанс с инъекцией Bearer-токена и нормализацией ошибок. */
export const http: AxiosInstance = axios.create({
  baseURL: env.apiBaseUrl,
  headers: { "Content-Type": "application/json" },
});

http.interceptors.request.use((config) => {
  const token = getToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

http.interceptors.response.use(
  (resp) => resp,
  (error: AxiosError<{ error?: ApiErrorBody }>) => {
    const status = error.response?.status ?? 0;
    if (status === 401) {
      // Токен истёк/невалиден — сбрасываем, чтобы увести на логин.
      useAuthStore.getState().logout();
    }
    const body = error.response?.data?.error;
    if (body) {
      return Promise.reject(new ApiError(status, body));
    }
    return Promise.reject(
      new ApiError(status, {
        code: "network_error",
        message: error.message || "Ошибка сети",
        details: [],
      }),
    );
  },
);

/** Типизированные хелперы. */
export const api = {
  // params типизируем как object, чтобы принимать и интерфейсы фильтров (без
  // индексной сигнатуры), и литералы. Axios сериализует их в query.
  get: async <T>(url: string, params?: object): Promise<T> =>
    (await http.get<T>(url, { params })).data,
  post: async <T>(url: string, data?: unknown): Promise<T> =>
    (await http.post<T>(url, data)).data,
  put: async <T>(url: string, data?: unknown): Promise<T> =>
    (await http.put<T>(url, data)).data,
  patch: async <T>(url: string, data?: unknown): Promise<T> =>
    (await http.patch<T>(url, data)).data,
  delete: async <T>(url: string): Promise<T> => (await http.delete<T>(url)).data,
};
