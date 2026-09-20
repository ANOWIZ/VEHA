using System.Globalization;
using Veha.Domain.Common;
using Veha.Domain.Enums;

namespace Veha.Domain.Rules;

/// <summary>Калькулятор стоимости лицензий и ТКП (порт quote_calc.py, без БД/float).
/// Лицензия: цена×курс×(1+буфер%) → закупка×(1−партн.скидка), продажа×(1−скидка клиенту),
/// ×срок(подписка)×кол-во, +техподдержка для perpetual. Работы/субподряд/поддержка — в рублях.</summary>
public static class QuoteCalc
{
    private const decimal Hundred = 100m;
    private static decimal Q(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    public sealed class LineCalcInput
    {
        public QuoteLineKind Kind { get; init; }
        public decimal Qty { get; init; } = 1m;
        public decimal UnitPrice { get; init; }
        public decimal? UnitCost { get; init; }
        public string Currency { get; init; } = "RUB";
        public LicensingModel? LicensingModel { get; init; }
        public int? TermMonths { get; init; }
        public decimal? SupportPct { get; init; }
        public decimal PartnerDiscountPct { get; init; }
        public decimal ClientDiscountPct { get; init; }
    }

    public readonly record struct LineCalcResult(decimal CostAmount, decimal SellAmount, decimal Margin);

    public static decimal ResolveRate(string? currency, IReadOnlyDictionary<string, decimal> rates)
    {
        var cur = (currency ?? "RUB").ToUpperInvariant();
        if (cur == "RUB") return 1m;
        if (!rates.TryGetValue(cur, out var rate)) throw new RateNotFoundException(cur);
        return rate;
    }

    private static decimal TermMultiplier(LineCalcInput line)
        => line.LicensingModel == Enums.LicensingModel.Subscription && line.TermMonths is > 0
            ? line.TermMonths.Value
            : 1m;

    public static LineCalcResult ComputeLine(LineCalcInput line, IReadOnlyDictionary<string, decimal> rates, decimal bufferPct)
    {
        if (line.Kind == QuoteLineKind.License)
        {
            var rate = ResolveRate(line.Currency, rates);
            var baseRub = line.UnitPrice * rate * (1m + bufferPct / Hundred);
            var mult = TermMultiplier(line) * line.Qty;
            var costUnit = baseRub * (1m - line.PartnerDiscountPct / Hundred);
            var sellUnit = baseRub * (1m - line.ClientDiscountPct / Hundred);
            var cost = costUnit * mult;
            var sell = sellUnit * mult;
            if (line.LicensingModel == Enums.LicensingModel.Perpetual && line.SupportPct is > 0)
            {
                cost += cost * line.SupportPct.Value / Hundred;
                sell += sell * line.SupportPct.Value / Hundred;
            }
            return new LineCalcResult(Q(cost), Q(sell), Q(sell - cost));
        }

        // work / subcontract / support — в рублях.
        var sellR = line.UnitPrice * line.Qty;
        var costR = (line.UnitCost ?? 0m) * line.Qty;
        return new LineCalcResult(Q(costR), Q(sellR), Q(sellR - costR));
    }

    private static readonly Dictionary<QuoteLineKind, string> KindBucket = new()
    {
        [QuoteLineKind.License] = "licenses_sell",
        [QuoteLineKind.Work] = "work_sell",
        [QuoteLineKind.Subcontract] = "subcontract_sell",
        [QuoteLineKind.Support] = "support_sell",
    };

    /// <summary>Итоги по расчёту: суммы продаж по типам, total_sell/cost, margin, margin_pct
    /// (строками в snake_case — как totals в Python).</summary>
    public static Dictionary<string, string> ComputeTotals(IEnumerable<(LineCalcInput Input, LineCalcResult Result)> lines)
    {
        var buckets = new Dictionary<string, decimal>
        {
            ["licenses_sell"] = 0m, ["work_sell"] = 0m, ["subcontract_sell"] = 0m, ["support_sell"] = 0m,
        };
        decimal totalSell = 0, totalCost = 0;
        foreach (var (input, res) in lines)
        {
            buckets[KindBucket[input.Kind]] += res.SellAmount;
            totalSell += res.SellAmount;
            totalCost += res.CostAmount;
        }
        var margin = totalSell - totalCost;
        var marginPct = totalSell != 0 ? margin / totalSell * Hundred : 0m;

        // Как Python .quantize(0.01) → всегда 2 знака ("9900.00").
        static string S(decimal d) => d.ToString("0.00", CultureInfo.InvariantCulture);
        return new Dictionary<string, string>
        {
            ["licenses_sell"] = S(Q(buckets["licenses_sell"])),
            ["work_sell"] = S(Q(buckets["work_sell"])),
            ["subcontract_sell"] = S(Q(buckets["subcontract_sell"])),
            ["support_sell"] = S(Q(buckets["support_sell"])),
            ["total_sell"] = S(Q(totalSell)),
            ["total_cost"] = S(Q(totalCost)),
            ["margin"] = S(Q(margin)),
            ["margin_pct"] = S(Q(marginPct)),
        };
    }
}
