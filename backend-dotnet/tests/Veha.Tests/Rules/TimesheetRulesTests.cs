using Veha.Domain.Common;
using Veha.Domain.Rules;
using Xunit;

namespace Veha.Tests.Rules;

public class TimesheetRulesTests
{
    [Theory]
    [InlineData("0.25")]
    [InlineData("8")]
    [InlineData("7.5")]
    [InlineData("24")]
    public void ValidateHours_accepts_valid(string h) =>
        TimesheetRules.ValidateHours(decimal.Parse(h, System.Globalization.CultureInfo.InvariantCulture));

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("24.25")]
    [InlineData("7.1")]   // не кратно 0,25
    public void ValidateHours_rejects_invalid(string h) =>
        Assert.Throws<DomainValidationException>(() =>
            TimesheetRules.ValidateHours(decimal.Parse(h, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void ValidateDailyTotal_rejects_over_24()
    {
        TimesheetRules.ValidateDailyTotal(20m, 4m); // ровно 24 — ок
        Assert.Throws<DomainValidationException>(() => TimesheetRules.ValidateDailyTotal(20m, 5m));
    }

    [Fact]
    public void ComputeCost_rounds_half_up()
    {
        Assert.Equal(12_000.00m, TimesheetRules.ComputeCost(8m, 1500m));
        Assert.Equal(1_837.50m, TimesheetRules.ComputeCost(7.5m, 245m));
    }
}
