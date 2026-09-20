using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Veha.Domain.Enums;

namespace Veha.Api.Dtos;

// Порт schemas/finance.py. Финансовые эндпоинты доступны только ролям с финансами
// и доступом к проекту (ProjectFinancials).

public class BudgetUpsertDto : IValidatableObject
{
    public decimal PlannedRevenue { get; set; }
    // {"payroll":"...","licenses":"...","subcontract":"...","travel":"...","other":"..."}
    public Dictionary<string, string> PlannedCosts { get; set; } = new();

    private static readonly HashSet<string> AllowedCategories =
        ["payroll", "licenses", "subcontract", "travel", "other"];

    public IEnumerable<ValidationResult> Validate(ValidationContext _)
    {
        if (PlannedRevenue < 0)
            yield return new ValidationResult("Выручка не может быть отрицательной", [nameof(PlannedRevenue)]);
        foreach (var (key, raw) in PlannedCosts)
        {
            if (!AllowedCategories.Contains(key))
                yield return new ValidationResult($"Неизвестная категория затрат «{key}»", [nameof(PlannedCosts)]);
            else if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount < 0)
                yield return new ValidationResult($"Сумма категории «{key}» должна быть неотрицательным числом", [nameof(PlannedCosts)]);
        }
    }
}

public record BudgetOutDto(Guid Id, Guid ProjectId, decimal PlannedRevenue, Dictionary<string, string> PlannedCosts);

public class ActualCostCreateDto : IValidatableObject
{
    [Required] public CostCategory Category { get; set; }
    public decimal Amount { get; set; }
    public CostSource Source { get; set; } = CostSource.Manual;   // игнорируется — всегда MANUAL
    [Required] public DateOnly OccurredOn { get; set; }
    public string? ExternalId { get; set; }
    public string? Description { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext _)
    {
        if (Amount < 0)
            yield return new ValidationResult("Сумма не может быть отрицательной", [nameof(Amount)]);
    }
}

public record ActualCostOutDto(
    Guid Id, Guid ProjectId, CostCategory Category, decimal Amount, CostSource Source,
    DateOnly OccurredOn, string? ExternalId, string? Description);

public record MarginOutDto(
    decimal Revenue, decimal TotalCost, decimal Margin, decimal MarginPct, Dictionary<string, string> CostBreakdown);

public record ForecastOutDto(decimal Eac, decimal Etc, DateTimeOffset? CalculatedAt);

public record ProjectFinanceDto(
    Guid ProjectId,
    BudgetOutDto? Budget,
    MarginOutDto Margin,
    ForecastOutDto Forecast,
    decimal PlannedHours,
    decimal ActualHours,
    decimal HoursOverrunPct);
