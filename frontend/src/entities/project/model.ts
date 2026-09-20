export type ProjectType = "implementation" | "pilot" | "support" | "presale";
export type ProjectStatus = "active" | "on_hold" | "closed" | "cancelled";
export type Stage =
  | "presale"
  | "survey"
  | "design"
  | "implementation"
  | "pilot"
  | "support"
  | "closed";

export const STAGE_ORDER: Stage[] = [
  "presale",
  "survey",
  "design",
  "implementation",
  "pilot",
  "support",
  "closed",
];

export const STAGE_LABELS: Record<Stage, string> = {
  presale: "Пресейл",
  survey: "Обследование",
  design: "Проектирование",
  implementation: "Внедрение",
  pilot: "Опытная эксплуатация",
  support: "Поддержка",
  closed: "Закрыт",
};

export const PROJECT_TYPE_LABELS: Record<ProjectType, string> = {
  implementation: "Внедрение",
  pilot: "Пилот",
  support: "Поддержка",
  presale: "Пресейл",
};

export const STATUS_LABELS: Record<ProjectStatus, string> = {
  active: "Активен",
  on_hold: "Приостановлен",
  closed: "Закрыт",
  cancelled: "Отменён",
};

export const STATUS_VARIANT: Record<
  ProjectStatus,
  "default" | "secondary" | "success" | "warning" | "destructive"
> = {
  active: "success",
  on_hold: "warning",
  closed: "secondary",
  cancelled: "destructive",
};

export const MEMBER_ROLE_LABELS: Record<string, string> = {
  pm: "Руководитель проекта",
  architect: "Архитектор",
  engineer: "Инженер",
  analyst: "Аналитик",
  presale: "Пресейл",
};

export interface Project {
  id: string;
  code: string;
  name: string;
  client_id: string;
  type: ProjectType;
  manager_id: string;
  curator_id: string | null;
  stage: Stage;
  status: ProjectStatus;
  planned_start: string | null;
  planned_end: string | null;
  actual_start: string | null;
  actual_end: string | null;
  budget_revenue: string;
  contract_ref: string | null;
  created_at: string;
}

export interface ProjectMember {
  id: string;
  user_id: string;
  role: string;
  bill_rate: string;
  period_from: string | null;
  period_to: string | null;
}

export interface Milestone {
  id: string;
  name: string;
  milestone_date: string;
  is_payment: boolean;
  amount: string;
}

export interface ProjectDetail extends Project {
  members: ProjectMember[];
  milestones: Milestone[];
}

export interface StageTransition {
  id: string;
  from_stage: Stage | null;
  to_stage: Stage;
  reason: string | null;
  created_by: string | null;
  created_at: string;
}

export function nextStage(stage: Stage): Stage | null {
  const i = STAGE_ORDER.indexOf(stage);
  return i + 1 < STAGE_ORDER.length ? STAGE_ORDER[i + 1] : null;
}

export function prevStage(stage: Stage): Stage | null {
  const i = STAGE_ORDER.indexOf(stage);
  return i > 0 ? STAGE_ORDER[i - 1] : null;
}
