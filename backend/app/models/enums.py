"""Доменные перечисления. Хранятся в БД как строки (native_enum=False)."""

from __future__ import annotations

from enum import StrEnum


class Role(StrEnum):
    ADMIN = "admin"
    DIRECTOR = "director"
    PM = "pm"
    PRESALE = "presale"
    ENGINEER = "engineer"
    FINANCE = "finance"
    CLIENT = "client"  # контакт Заказчика (внешний портал)


class CustomerActionStatus(StrEnum):
    """Статус ожидания работ от Заказчика (портал Заказчика)."""

    WAITING = "waiting"    # ждём действий Заказчика — проект «стоит» на их стороне
    PROVIDED = "provided"  # Заказчик предоставил данные/артефакты
    ACCEPTED = "accepted"  # мы приняли — блокер снят


class RiskCategory(StrEnum):
    """Категория риска проекта."""

    TECHNICAL = "technical"            # технический
    SCHEDULE = "schedule"             # сроки
    BUDGET = "budget"                 # бюджет / финансы
    RESOURCE = "resource"             # ресурсы / команда
    SCOPE = "scope"                   # содержание / требования
    EXTERNAL = "external"             # внешний (Заказчик / вендор / регуляторы)
    ORGANIZATIONAL = "organizational"  # организационный


class RiskStatus(StrEnum):
    """Статус риска в реестре."""

    OPEN = "open"              # идентифицирован
    MITIGATING = "mitigating"  # в работе (снижается)
    ACCEPTED = "accepted"      # принят (осознанно живём с риском)
    CLOSED = "closed"          # закрыт (не актуален / устранён)
    REALIZED = "realized"      # реализовался → стал проблемой


# Активные риски — те, что требуют внимания (попадают в матрицу/портфель).
ACTIVE_RISK_STATUSES: tuple[RiskStatus, ...] = (RiskStatus.OPEN, RiskStatus.MITIGATING)


class RiskResponse(StrEnum):
    """Стратегия реагирования на риск (PMBOK)."""

    AVOID = "avoid"        # уклонение
    MITIGATE = "mitigate"  # снижение
    TRANSFER = "transfer"  # передача
    ACCEPT = "accept"      # принятие


class ProjectType(StrEnum):
    IMPLEMENTATION = "implementation"  # внедрение
    PILOT = "pilot"                    # пилот
    SUPPORT = "support"                # поддержка
    PRESALE = "presale"               # пресейл


class ProjectStatus(StrEnum):
    ACTIVE = "active"
    ON_HOLD = "on_hold"
    CLOSED = "closed"
    CANCELLED = "cancelled"


class Stage(StrEnum):
    """Жизненный цикл проекта. Порядок важен — переходы только ±1 (см. сервис)."""

    PRESALE = "presale"
    SURVEY = "survey"                  # обследование
    DESIGN = "design"                  # проектирование
    IMPLEMENTATION = "implementation"  # внедрение
    PILOT = "pilot"                    # опытная эксплуатация
    SUPPORT = "support"               # поддержка
    CLOSED = "closed"


# Упорядоченная последовательность стадий для проверки переходов.
STAGE_ORDER: tuple[Stage, ...] = (
    Stage.PRESALE,
    Stage.SURVEY,
    Stage.DESIGN,
    Stage.IMPLEMENTATION,
    Stage.PILOT,
    Stage.SUPPORT,
    Stage.CLOSED,
)


class ProjectMemberRole(StrEnum):
    PM = "pm"                # руководитель проекта
    ARCHITECT = "architect"  # архитектор
    ENGINEER = "engineer"    # инженер
    ANALYST = "analyst"      # аналитик
    PRESALE = "presale"      # пресейл


class TaskStatus(StrEnum):
    OPEN = "open"
    IN_PROGRESS = "in_progress"
    DONE = "done"
    CANCELLED = "cancelled"


class TimeEntryStatus(StrEnum):
    DRAFT = "draft"
    SUBMITTED = "submitted"
    APPROVED = "approved"
    REJECTED = "rejected"


class LicensingModel(StrEnum):
    PER_USER = "per_user"
    PER_NAMED_USER = "per_named_user"
    PER_CONCURRENT_USER = "per_concurrent_user"
    PER_CORE = "per_core"
    PER_CPU = "per_cpu"
    PER_SERVER = "per_server"
    PER_DEVICE = "per_device"
    SUBSCRIPTION = "subscription"  # срок (мес) × ставка
    PERPETUAL = "perpetual"        # бессрочно (+опц. техподдержка %/год)


class QuoteStatus(StrEnum):
    DRAFT = "draft"
    SENT = "sent"
    ACCEPTED = "accepted"
    REJECTED = "rejected"


class QuoteLineKind(StrEnum):
    LICENSE = "license"
    WORK = "work"              # работы: часы × bill_rate
    SUBCONTRACT = "subcontract"
    SUPPORT = "support"        # техподдержка


class CostCategory(StrEnum):
    PAYROLL = "payroll"        # ФОТ
    LICENSES = "licenses"      # закупка лицензий
    SUBCONTRACT = "subcontract"
    TRAVEL = "travel"          # командировки
    OTHER = "other"


class CostSource(StrEnum):
    TIMESHEET = "timesheet"    # авторасчёт из approved-таймшитов
    MANUAL = "manual"
    ONEC = "onec"              # из 1С


class AuditAction(StrEnum):
    CREATE = "create"
    UPDATE = "update"
    DELETE = "delete"
    STAGE_CHANGE = "stage_change"
    APPROVE = "approve"
    REJECT = "reject"
    SUBMIT = "submit"


class IntegrationDirection(StrEnum):
    INBOUND = "inbound"
    OUTBOUND = "outbound"
