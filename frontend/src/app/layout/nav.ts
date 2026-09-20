import {
  Building2,
  Calculator,
  CheckSquare,
  FolderKanban,
  LayoutDashboard,
  FileBarChart,
  Library,
  type LucideIcon,
  ScrollText,
  Settings,
  ShieldAlert,
  Timer,
  TrendingUp,
  Users,
} from "lucide-react";

import { INTERNAL_ROLES, type Role } from "@/entities/user/model";

export interface NavItem {
  to: string;
  label: string;
  icon: LucideIcon;
  /** Роли, которым пункт виден. Пусто = всем аутентифицированным. */
  roles?: Role[];
}

export interface NavGroup {
  title: string;
  items: NavItem[];
}

export const NAV: NavGroup[] = [
  {
    title: "Портал Заказчика",
    items: [{ to: "/portal", label: "Мои проекты", icon: Building2, roles: ["client"] }],
  },
  {
    title: "Обзор",
    items: [{ to: "/", label: "Дашборд", icon: LayoutDashboard, roles: INTERNAL_ROLES }],
  },
  {
    title: "Проекты и работа",
    items: [
      { to: "/projects", label: "Проекты", icon: FolderKanban, roles: INTERNAL_ROLES },
      { to: "/timesheets", label: "Таймшиты", icon: Timer, roles: INTERNAL_ROLES },
      {
        to: "/approvals",
        label: "Утверждение",
        icon: CheckSquare,
        roles: ["pm", "admin"],
      },
    ],
  },
  {
    title: "Пресейл",
    items: [
      {
        to: "/quotes",
        label: "Калькулятор / ТКП",
        icon: Calculator,
        roles: ["presale", "pm", "admin", "director"],
      },
      {
        to: "/catalog",
        label: "Каталог продуктов",
        icon: Library,
        roles: ["presale", "pm", "admin"],
      },
    ],
  },
  {
    title: "Управление",
    items: [
      {
        to: "/finance",
        label: "Финансы",
        icon: TrendingUp,
        roles: ["finance", "director", "pm", "admin"],
      },
      {
        to: "/resources",
        label: "Ресурсы",
        icon: Users,
        roles: ["pm", "director", "admin", "finance"],
      },
      {
        to: "/risks",
        label: "Риски",
        icon: ShieldAlert,
        roles: ["pm", "director", "admin", "finance"],
      },
      {
        to: "/reports",
        label: "Отчёты",
        icon: FileBarChart,
        roles: ["pm", "director", "admin", "finance"],
      },
    ],
  },
  {
    title: "Администрирование",
    items: [
      {
        to: "/admin/users",
        label: "Пользователи и ставки",
        icon: Users,
        roles: ["admin", "finance"],
      },
      {
        to: "/admin/audit",
        label: "Аудит-лог",
        icon: ScrollText,
        roles: ["admin", "director", "finance"],
      },
      { to: "/admin/directories", label: "Справочники", icon: Settings, roles: ["admin"] },
    ],
  },
];
