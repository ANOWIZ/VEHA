using Veha.Domain.Enums;

namespace Veha.Domain.Entities;

/// <summary>Бюджет проекта: плановая выручка и плановые затраты по категориям.</summary>
public class ProjectBudget : BaseEntity
{
    public Guid ProjectId { get; set; }
    public decimal PlannedRevenue { get; set; }
    /// <summary>Плановые затраты по категориям: {"payroll":"...","licenses":"...",...}.</summary>
    public Dictionary<string, string> PlannedCosts { get; set; } = [];
}

/// <summary>Фактическая затрата. ФОТ генерируется из approved-таймшитов
/// (source=timesheet), закупки/прочее — вручную или из 1С. Идемпотентна по
/// (project_id, external_id) для активных строк.</summary>
public class ActualCost : BaseEntity
{
    public Guid ProjectId { get; set; }
    public CostCategory Category { get; set; }
    public decimal Amount { get; set; }
    public CostSource Source { get; set; } = CostSource.Manual;
    public DateOnly OccurredOn { get; set; }
    public string? ExternalId { get; set; }
    public string? Description { get; set; }
}

/// <summary>Снимок прогноза затрат до завершения (EAC/ETC).</summary>
public class Forecast : BaseEntity
{
    public Guid ProjectId { get; set; }
    public decimal Eac { get; set; }
    public decimal Etc { get; set; }
    public DateTimeOffset CalculatedAt { get; set; }
}
