import { addDays, format, parseISO, startOfWeek } from "date-fns";

/** Понедельник недели для даты (неделя начинается с понедельника). */
export function mondayOf(d: Date): Date {
  return startOfWeek(d, { weekStartsOn: 1 });
}

export function toISODate(d: Date): string {
  return format(d, "yyyy-MM-dd");
}

export function fromISODate(s: string): Date {
  return parseISO(s);
}

/** Массив из 7 дат недели (пн…вс) от даты-понедельника. */
export function weekDays(monday: Date): Date[] {
  return Array.from({ length: 7 }, (_, i) => addDays(monday, i));
}

export const WEEKDAY_LABELS = ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"];

export { addDays };
