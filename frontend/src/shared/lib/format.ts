import { format, parseISO } from "date-fns";
import { ru } from "date-fns/locale";

const moneyFmt = new Intl.NumberFormat("ru-RU", {
  style: "currency",
  currency: "RUB",
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

const numberFmt = new Intl.NumberFormat("ru-RU", {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

const percentFmt = new Intl.NumberFormat("ru-RU", {
  minimumFractionDigits: 1,
  maximumFractionDigits: 1,
});

/** «1 234 567,89 ₽» */
export function formatMoney(value: number | string | null | undefined): string {
  if (value === null || value === undefined || value === "") return "—";
  return moneyFmt.format(Number(value));
}

/** Денежное значение в произвольной валюте. */
export function formatCurrency(value: number | string, currency = "RUB"): string {
  return new Intl.NumberFormat("ru-RU", {
    style: "currency",
    currency,
    minimumFractionDigits: 2,
  }).format(Number(value));
}

/** Число в формате ru-RU без единицы измерения («7,5», «1 234,5»). */
export function formatNumber(value: number | string | null | undefined): string {
  if (value === null || value === undefined || value === "") return "—";
  return numberFmt.format(Number(value));
}

/** «7,5 ч» */
export function formatHours(value: number | string | null | undefined): string {
  if (value === null || value === undefined || value === "") return "—";
  return `${numberFmt.format(Number(value))} ч`;
}

/** «24,5 %» */
export function formatPercent(value: number | string | null | undefined): string {
  if (value === null || value === undefined || value === "") return "—";
  return `${percentFmt.format(Number(value))} %`;
}

/** ISO → «12 июн. 2026» (TZ пользователя). */
export function formatDate(iso: string | null | undefined, pattern = "d MMM yyyy"): string {
  if (!iso) return "—";
  const d = iso.length <= 10 ? parseISO(iso) : new Date(iso);
  return format(d, pattern, { locale: ru });
}

export function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return "—";
  return format(new Date(iso), "d MMM yyyy, HH:mm", { locale: ru });
}
