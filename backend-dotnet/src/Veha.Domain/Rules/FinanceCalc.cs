namespace Veha.Domain.Rules;

/// <summary>Чистые финансовые расчёты (порт finance_calc.py).
/// Деньги/часы — decimal, без double. Маржа = (выручка − затраты)/выручка.
/// Квантизация до сотых, округление half-up (away from zero) — как ROUND_HALF_UP.</summary>
public static class FinanceCalc
{
    private const decimal Hundred = 100m;

    private static decimal Q(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public readonly record struct MarginResult(
        decimal Revenue,
        decimal TotalCost,
        decimal Margin,
        decimal MarginPct);

    public static MarginResult ComputeMargin(decimal revenue, decimal totalCost)
    {
        var margin = revenue - totalCost;
        var marginPct = revenue != 0 ? margin / revenue * Hundred : 0m;
        return new MarginResult(Q(revenue), Q(totalCost), Q(margin), Q(marginPct));
    }

    public static decimal TotalCost(IEnumerable<decimal> costByCategory) => Q(costByCategory.Sum());

    public readonly record struct Forecast(decimal Etc, decimal Eac);

    public static Forecast ComputeForecast(decimal actualCostToDate, decimal remainingPlannedCost)
    {
        var etc = remainingPlannedCost > 0 ? remainingPlannedCost : 0m;
        return new Forecast(Q(etc), Q(actualCostToDate + etc));
    }

    /// <summary>Перерасход часов в %: (факт − план)/план×100. План ≤ 0 → 0.</summary>
    public static decimal HoursOverrunPct(decimal plannedHours, decimal actualHours) =>
        plannedHours <= 0 ? 0m : Q((actualHours - plannedHours) / plannedHours * Hundred);

    /// <summary>Загрузка ресурса в % от нормы. Норма ≤ 0 → 0.</summary>
    public static decimal UtilizationPct(decimal value, decimal normHours) =>
        normHours <= 0 ? 0m : Q(value / normHours * Hundred);
}
