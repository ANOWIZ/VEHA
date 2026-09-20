using Veha.Domain.Rules;
using Xunit;

namespace Veha.Tests.Rules;

public class FinanceCalcTests
{
    [Fact]
    public void ComputeMargin_basic()
    {
        var r = FinanceCalc.ComputeMargin(1_000_000m, 850_000m);
        Assert.Equal(1_000_000.00m, r.Revenue);
        Assert.Equal(850_000.00m, r.TotalCost);
        Assert.Equal(150_000.00m, r.Margin);
        Assert.Equal(15.00m, r.MarginPct);
    }

    [Fact]
    public void ComputeMargin_zero_revenue_gives_zero_pct()
    {
        var r = FinanceCalc.ComputeMargin(0m, 500m);
        Assert.Equal(0m, r.MarginPct);
        Assert.Equal(-500.00m, r.Margin);
    }

    [Theory]
    [InlineData("20", "40", "50.00")]
    [InlineData("50", "520", "9.62")]   // как в utilization (130/2088 на сотрудника)
    [InlineData("0", "40", "0.00")]
    [InlineData("10", "0", "0.00")]     // норма 0 → 0
    public void UtilizationPct(string value, string norm, string expected)
    {
        Assert.Equal(
            decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            FinanceCalc.UtilizationPct(
                decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture),
                decimal.Parse(norm, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Theory]
    [InlineData("100", "110", "10.00")]
    [InlineData("100", "90", "-10.00")]
    [InlineData("0", "50", "0.00")]     // план 0 → 0
    public void HoursOverrunPct(string planned, string actual, string expected)
    {
        Assert.Equal(
            decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            FinanceCalc.HoursOverrunPct(
                decimal.Parse(planned, System.Globalization.CultureInfo.InvariantCulture),
                decimal.Parse(actual, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void ComputeForecast_etc_floored_at_zero()
    {
        var f = FinanceCalc.ComputeForecast(actualCostToDate: 300_000m, remainingPlannedCost: 200_000m);
        Assert.Equal(200_000.00m, f.Etc);
        Assert.Equal(500_000.00m, f.Eac);

        var f2 = FinanceCalc.ComputeForecast(300_000m, -50m);
        Assert.Equal(0m, f2.Etc);
        Assert.Equal(300_000.00m, f2.Eac);
    }
}
