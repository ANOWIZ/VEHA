using Veha.Domain.Common;
using Veha.Domain.Enums;
using Veha.Domain.Rules;

namespace Veha.Tests.Rules;

/// <summary>Паритет-тесты калькулятора ТКП (quote_calc.py).</summary>
public class QuoteCalcTests
{
    private static readonly Dictionary<string, decimal> Rates = new() { ["USD"] = 90m };

    [Fact]
    public void License_applies_rate_buffer_and_discounts()
    {
        var line = new QuoteCalc.LineCalcInput
        {
            Kind = QuoteLineKind.License, Currency = "USD", LicensingModel = LicensingModel.PerUser,
            Qty = 1m, UnitPrice = 100m, PartnerDiscountPct = 20m,
        };
        var r = QuoteCalc.ComputeLine(line, Rates, 10m);   // база 100*90*1.1 = 9900
        Assert.Equal(9900m, r.SellAmount);
        Assert.Equal(7920m, r.CostAmount);                 // 9900 * 0.8
        Assert.Equal(1980m, r.Margin);
    }

    [Fact]
    public void Subscription_multiplies_by_term_months()
    {
        var line = new QuoteCalc.LineCalcInput
        {
            Kind = QuoteLineKind.License, Currency = "RUB", LicensingModel = LicensingModel.Subscription,
            TermMonths = 12, Qty = 1m, UnitPrice = 100m,
        };
        var r = QuoteCalc.ComputeLine(line, Rates, 0m);
        Assert.Equal(1200m, r.SellAmount);   // 100 * 1 (rate RUB) * 12
    }

    [Fact]
    public void Perpetual_with_support_adds_support_to_cost_and_sell()
    {
        var line = new QuoteCalc.LineCalcInput
        {
            Kind = QuoteLineKind.License, Currency = "RUB", LicensingModel = LicensingModel.Perpetual,
            Qty = 1m, UnitPrice = 1000m, SupportPct = 20m,
        };
        var r = QuoteCalc.ComputeLine(line, Rates, 0m);
        Assert.Equal(1200m, r.SellAmount);   // 1000 + 20%
        Assert.Equal(1200m, r.CostAmount);
    }

    [Fact]
    public void Missing_rate_throws()
    {
        var line = new QuoteCalc.LineCalcInput { Kind = QuoteLineKind.License, Currency = "EUR", UnitPrice = 10m };
        Assert.Throws<RateNotFoundException>(() => QuoteCalc.ComputeLine(line, Rates, 0m));
    }

    [Fact]
    public void Work_line_uses_ruble_price_and_cost()
    {
        var line = new QuoteCalc.LineCalcInput { Kind = QuoteLineKind.Work, Qty = 10m, UnitPrice = 5000m, UnitCost = 3000m };
        var r = QuoteCalc.ComputeLine(line, Rates, 0m);
        Assert.Equal(50000m, r.SellAmount);
        Assert.Equal(30000m, r.CostAmount);
        Assert.Equal(20000m, r.Margin);
    }

    [Fact]
    public void Totals_bucket_by_kind_and_compute_margin_pct()
    {
        var lic = new QuoteCalc.LineCalcInput { Kind = QuoteLineKind.License, Currency = "RUB", UnitPrice = 100m };
        var work = new QuoteCalc.LineCalcInput { Kind = QuoteLineKind.Work, Qty = 1m, UnitPrice = 100m, UnitCost = 50m };
        var rLic = QuoteCalc.ComputeLine(lic, Rates, 0m);
        var rWork = QuoteCalc.ComputeLine(work, Rates, 0m);
        var totals = QuoteCalc.ComputeTotals([(lic, rLic), (work, rWork)]);

        Assert.Equal("100.00", totals["licenses_sell"]);
        Assert.Equal("100.00", totals["work_sell"]);
        Assert.Equal("200.00", totals["total_sell"]);
        Assert.Equal("150.00", totals["total_cost"]);  // лицензия 100 + работы 50
        Assert.Equal("50.00", totals["margin"]);
        Assert.Equal("25.00", totals["margin_pct"]);
    }
}
