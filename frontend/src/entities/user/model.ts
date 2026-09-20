export type Role =
  | "admin"
  | "director"
  | "pm"
  | "presale"
  | "engineer"
  | "finance"
  | "client";

/** Внутренние роли (сотрудники интегратора) — в отличие от внешнего «client». */
export const INTERNAL_ROLES: Role[] = [
  "admin",
  "director",
  "pm",
  "presale",
  "engineer",
  "finance",
];

export interface User {
  id: string;
  username: string;
  email: string;
  full_name: string;
  department: string | null;
  position: string | null;
  grade: string | null;
  is_active: boolean;
  roles: Role[];
}

export const ROLE_LABELS: Record<Role, string> = {
  admin: "Администратор",
  director: "Директор",
  pm: "Руководитель проекта",
  presale: "Пресейл",
  engineer: "Инженер",
  finance: "Финансы",
  client: "Заказчик",
};

/** Роли, которым доступны финансовые поля (маржа, себестоимость). */
export const FINANCIAL_ROLES: Role[] = ["admin", "director", "finance", "pm"];

export function hasAnyRole(user: User | undefined, roles: Role[]): boolean {
  if (!user) return false;
  return user.roles.some((r) => roles.includes(r));
}

export function canSeeFinancials(user: User | undefined): boolean {
  return hasAnyRole(user, FINANCIAL_ROLES);
}
