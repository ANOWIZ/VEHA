using Veha.Domain.Rules;
using Xunit;

namespace Veha.Tests.Rules;

public class ReportRulesTests
{
    [Theory]
    [InlineData("2024-01-01", "2024-01-01", 1)]  // Пн
    [InlineData("2024-01-06", "2024-01-06", 0)]  // Сб
    [InlineData("2024-01-07", "2024-01-07", 0)]  // Вс
    [InlineData("2024-01-01", "2024-01-05", 5)]  // Пн–Пт
    [InlineData("2024-01-01", "2024-01-07", 5)]  // Пн–Вс
    [InlineData("2024-01-01", "2024-01-14", 10)] // две недели
    [InlineData("2024-01-05", "2024-01-01", 0)]  // перевёрнутый диапазон
    public void WorkingDays(string from, string to, int expected) =>
        Assert.Equal(expected, ReportRules.WorkingDays(DateOnly.Parse(from), DateOnly.Parse(to)));
}
