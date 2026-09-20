namespace Veha.Domain.Enums;

// Доменные перечисления. В БД хранятся строками (EF HasConversion<string>),
// значения соответствуют исходной системе на Python.

public enum Role
{
    Admin,
    Director,
    Pm,
    Presale,
    Engineer,
    Finance,
    Client,
}

public enum ProjectType
{
    Implementation,
    Pilot,
    Support,
    Presale,
}

public enum ProjectStatus
{
    Active,
    OnHold,
    Closed,
    Cancelled,
}

// Жизненный цикл проекта. Порядок важен — переходы только ±1 (см. StageRules).
public enum Stage
{
    Presale,
    Survey,
    Design,
    Implementation,
    Pilot,
    Support,
    Closed,
}

public enum ProjectMemberRole
{
    Pm,
    Architect,
    Engineer,
    Analyst,
    Presale,
}

public enum TaskStatus
{
    Open,
    InProgress,
    Done,
    Cancelled,
}

public enum TimeEntryStatus
{
    Draft,
    Submitted,
    Approved,
    Rejected,
}

public enum RiskCategory
{
    Technical,
    Schedule,
    Budget,
    Resource,
    Scope,
    External,
    Organizational,
}

public enum RiskStatus
{
    Open,
    Mitigating,
    Accepted,
    Closed,
    Realized,
}

public enum RiskResponse
{
    Avoid,
    Mitigate,
    Transfer,
    Accept,
}

public enum AuditAction
{
    Create,
    Update,
    Delete,
    StageChange,
    Approve,
    Reject,
    Submit,
}

// --- Калькулятор лицензий / ТКП ---
public enum LicensingModel
{
    PerUser,
    PerNamedUser,
    PerConcurrentUser,
    PerCore,
    PerCpu,
    PerServer,
    PerDevice,
    Subscription,  // срок (мес) × ставка
    Perpetual,     // бессрочно (+опц. техподдержка %/год)
}

public enum QuoteStatus
{
    Draft,
    Sent,
    Accepted,
    Rejected,
}

public enum QuoteLineKind
{
    License,
    Work,         // работы: часы × bill_rate
    Subcontract,
    Support,      // техподдержка
}

// --- Финансы ---
public enum CostCategory
{
    Payroll,      // ФОТ
    Licenses,     // закупка лицензий
    Subcontract,
    Travel,       // командировки
    Other,
}

public enum CostSource
{
    Timesheet,    // авторасчёт из approved-таймшитов
    Manual,
    Onec,         // из 1С
}

// --- Портал Заказчика ---
public enum CustomerActionStatus
{
    Waiting,      // ждём действий Заказчика — проект «стоит» на их стороне
    Provided,     // Заказчик предоставил данные/артефакты
    Accepted,     // мы приняли — блокер снят
}

public enum IntegrationDirection
{
    Inbound,
    Outbound,
}
