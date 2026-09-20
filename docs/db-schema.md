# Схема БД (обзор)

Все доменные таблицы имеют: `id UUID PK (uuid-ossp)`, `created_at`,
`updated_at`, `deleted_at` (soft delete). Деньги — `NUMERIC(15,2)`, часы —
`NUMERIC(5,2)`. Расширения Postgres: `uuid-ossp`, `pg_trgm` (поиск по каталогу).

## Справочники
- **users** — синк из Keycloak/AD: ФИО, email, подразделение, должность, грейд,
  is_active, keycloak_id. Роли — в `realm_access`, в БД кэшируется список.
- **user_cost_rates** — версионированная себестоимость часа: user_id,
  cost_rate `NUMERIC(15,2)`, valid_from, valid_to. На дату записи берётся
  действующая ставка.
- **clients** — заказчик: name, inn, industry, contacts(JSONB), is_kii.
- **vendors** — вендор ПО/оборудования.
- **products** — продукт вендора: name, edition, licensing_model, price_currency,
  is_russian_registry.
- **price_list_items** — позиция прайса: product_id, metric, price, currency,
  valid_from/valid_to, partner_discount_level.

> Курсы ЦБ не используются. Для валютных прайсов курс задаётся вручную в версии
> расчёта (`quotes.currency_rates_snapshot`) и фиксируется в ней.

## Проекты
- **projects** — code (PRJ-YYYY-NNN), name, client_id, type, manager_id,
  curator_id, stage, planned/actual dates, budget_revenue, status, contract_ref.
- **project_stage_transitions** — история переходов: project_id, from_stage,
  to_stage, reason, created_by, created_at.
- **project_members** — project_id, user_id, role, bill_rate, period_from/to.
- **tasks** — иерархия (parent_id), project_id, stage, planned_hours, assignee_id,
  status, due_date.
- **milestones** — project_id, date, name, is_payment, amount.

## Трудозатраты
- **time_entries** — user_id, project_id, task_id, work_date, hours `NUMERIC(5,2)`,
  comment, status (draft/submitted/approved/rejected), approved_by, approved_at,
  cost_rate_snapshot, reversal_of (сторно).
- **timesheet_weeks** — user_id, week_start, status — агрегат для пакетной отправки.

## Калькулятор лицензий
- **quotes** — project_id, version, status, currency_rates_snapshot(JSONB),
  currency_buffer_pct, totals(JSONB).
- **quote_lines** — quote_id, kind (license/work/subcontract/support), product_id,
  licensing_model, metric, qty, unit_price, partner_discount_pct,
  client_discount_pct, line_total, margin.

## Финансы
- **project_budgets** — project_id, planned_revenue, planned_costs(JSONB по категориям).
- **actual_costs** — project_id, category, amount, source (timesheet/manual/1c),
  external_id, occurred_on.
- **forecasts** — project_id, eac, etc, calculated_at (кэш в Redis + снапшот).

## Ресурсы
- **resource_plans** — user_id, project_id, week_start, planned_hours.

## Аудит и интеграции
- **audit_log** — entity, entity_id, action, actor_id, diff(JSONB), created_at.
- **integration_log** — direction, system, external_id, payload(JSONB), status,
  error, created_at (идемпотентность обменов по external_id).
