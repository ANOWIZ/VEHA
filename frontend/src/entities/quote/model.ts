export type QuoteStatus = "draft" | "sent" | "accepted" | "rejected";
export type QuoteLineKind = "license" | "work" | "subcontract" | "support";
export type LicensingModel =
  | "per_user"
  | "per_named_user"
  | "per_concurrent_user"
  | "per_core"
  | "per_cpu"
  | "per_server"
  | "per_device"
  | "subscription"
  | "perpetual";

export const QUOTE_STATUS_LABELS: Record<QuoteStatus, string> = {
  draft: "Черновик",
  sent: "Отправлен",
  accepted: "Принят",
  rejected: "Отклонён",
};

export const QUOTE_STATUS_VARIANT: Record<
  QuoteStatus,
  "default" | "secondary" | "success" | "warning" | "destructive"
> = {
  draft: "secondary",
  sent: "warning",
  accepted: "success",
  rejected: "destructive",
};

export const KIND_LABELS: Record<QuoteLineKind, string> = {
  license: "Лицензия",
  work: "Работы",
  subcontract: "Субподряд",
  support: "Техподдержка",
};

export const LICENSING_LABELS: Record<LicensingModel, string> = {
  per_user: "За пользователя",
  per_named_user: "Именованный пользователь",
  per_concurrent_user: "Конкурентный пользователь",
  per_core: "За ядро",
  per_cpu: "За CPU",
  per_server: "За сервер",
  per_device: "За устройство",
  subscription: "Подписка",
  perpetual: "Бессрочно",
};

export interface QuoteTotals {
  licenses_sell?: string;
  work_sell?: string;
  subcontract_sell?: string;
  support_sell?: string;
  total_sell?: string;
  total_cost?: string;
  margin?: string;
  margin_pct?: string;
}

export interface QuoteLine {
  id: string;
  kind: QuoteLineKind;
  name: string;
  product_id: string | null;
  licensing_model: LicensingModel | null;
  metric: string | null;
  currency: string;
  qty: string;
  unit_price: string;
  unit_cost: string | null;
  term_months: number | null;
  support_pct: string | null;
  partner_discount_pct: string;
  client_discount_pct: string;
  cost_amount: string;
  sell_amount: string;
  margin: string;
  note: string | null;
}

export interface Quote {
  id: string;
  project_id: string;
  version: number;
  title: string;
  status: QuoteStatus;
  currency_rates_snapshot: Record<string, string>;
  currency_buffer_pct: string;
  totals: QuoteTotals;
  created_at: string;
}

export interface QuoteDetail extends Quote {
  lines: QuoteLine[];
}

export interface QuoteLineInput {
  kind: QuoteLineKind;
  name: string;
  product_id?: string | null;
  licensing_model?: LicensingModel | null;
  metric?: string | null;
  currency?: string;
  qty: string;
  unit_price: string;
  unit_cost?: string | null;
  term_months?: number | null;
  support_pct?: string | null;
  partner_discount_pct?: string;
  client_discount_pct?: string;
  note?: string | null;
}
