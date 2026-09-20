/** Единый формат ошибки бэкенда: {"error": {code, message, details}}. */
export interface ApiErrorBody {
  code: string;
  message: string;
  details: unknown[];
}

export class ApiError extends Error {
  code: string;
  status: number;
  details: unknown[];

  constructor(status: number, body: ApiErrorBody) {
    super(body.message);
    this.name = "ApiError";
    this.code = body.code;
    this.status = status;
    this.details = body.details ?? [];
  }
}

/** Страница серверной пагинации. */
export interface Page<T> {
  items: T[];
  total: number;
  limit: number;
  offset: number;
}
