using Veha.Domain.Common;

namespace Veha.Domain.Rules;

/// <summary>Правила трудозатрат (порт timesheet_rules.py). Часы — шаг 0,25,
/// суммарно ≤ 24 в день. Себестоимость = часы × ставка на дату записи.</summary>
public static class TimesheetRules
{
    public const decimal HourStep = 0.25m;
    public const decimal MaxDailyHours = 24m;

    public static void ValidateHours(decimal hours)
    {
        if (hours <= 0)
            throw new DomainValidationException("Количество часов должно быть положительным");
        if (hours > MaxDailyHours)
            throw new DomainValidationException("За один день нельзя списать более 24 часов");
        if (hours / HourStep % 1 != 0)
            throw new DomainValidationException("Часы указываются с шагом 0,25");
    }

    public static void ValidateDailyTotal(decimal existingHours, decimal newHours)
    {
        if (existingHours + newHours > MaxDailyHours)
            throw new DomainValidationException(
                $"Превышен суточный лимит: уже {existingHours} ч + {newHours} ч > 24 ч");
    }

    /// <summary>Себестоимость записи, округление до копеек (half-up).</summary>
    public static decimal ComputeCost(decimal hours, decimal costRate) =>
        Math.Round(hours * costRate, 2, MidpointRounding.AwayFromZero);
}
