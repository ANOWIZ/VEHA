namespace Veha.Domain.Rules;

/// <summary>Вспомогательные расчёты отчётов (порт report_service.working_days).</summary>
public static class ReportRules
{
    /// <summary>Количество рабочих дней (Пн–Пт) в диапазоне включительно.</summary>
    public static int WorkingDays(DateOnly from, DateOnly to)
    {
        if (to < from)
            return 0;
        var total = 0;
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            if (day.DayOfWeek != DayOfWeek.Saturday && day.DayOfWeek != DayOfWeek.Sunday)
                total++;
        }
        return total;
    }
}
