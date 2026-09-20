using Veha.Domain.Enums;

namespace Veha.Domain.Entities;

/// <summary>Расчёт/спецификация (ТКП). Версионируется по проекту; принятые/
/// отклонённые версии read-only. Курсы валют зафиксированы снимком на версию.</summary>
public class Quote : BaseEntity
{
    public Guid ProjectId { get; set; }
    public int Version { get; set; } = 1;
    public string Title { get; set; } = "ТКП";
    public QuoteStatus Status { get; set; } = QuoteStatus.Draft;

    /// <summary>Снимок курсов на версию: {"USD":"90.5", ...} (рублей за 1 ед.).</summary>
    public Dictionary<string, string> CurrencyRatesSnapshot { get; set; } = [];
    public decimal CurrencyBufferPct { get; set; }

    /// <summary>Кэш итогов: licenses/work/support/subcontract/total_sell/total_cost/
    /// margin/margin_pct (строками).</summary>
    public Dictionary<string, string> Totals { get; set; } = [];

    public List<QuoteLine> Lines { get; set; } = [];
}

/// <summary>Строка спецификации: лицензия / работы / субподряд / поддержка.</summary>
public class QuoteLine : BaseEntity
{
    public Guid QuoteId { get; set; }
    public Quote? Quote { get; set; }
    public QuoteLineKind Kind { get; set; }
    public string Name { get; set; } = "";

    public Guid? ProductId { get; set; }
    public LicensingModel? LicensingModel { get; set; }
    public string? Metric { get; set; }
    public string Currency { get; set; } = "RUB";

    public decimal Qty { get; set; } = 1m;
    public decimal UnitPrice { get; set; }
    public decimal? UnitCost { get; set; }
    public int? TermMonths { get; set; }       // subscription: месяцы; perpetual: % поддержки/год
    public decimal? SupportPct { get; set; }

    public decimal PartnerDiscountPct { get; set; }
    public decimal ClientDiscountPct { get; set; }

    // Рассчитанные значения (RUB), сохраняются при пересчёте.
    public decimal CostAmount { get; set; }
    public decimal SellAmount { get; set; }
    public decimal Margin { get; set; }

    public string? Note { get; set; }
}
