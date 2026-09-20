using System.ComponentModel.DataAnnotations;
using Veha.Domain.Enums;

namespace Veha.Api.Dtos;

// Порт schemas/quote.py. Чтение ТКП раскрывает себестоимость/маржу — закрыто от
// engineer/client (RBAC в контроллере). Правка — только для черновика.

public class QuoteCreateDto : IValidatableObject
{
    [StringLength(255)] public string Title { get; set; } = "ТКП";
    // {"USD":"90.5","EUR":"98.2"} — рублей за 1 ед.
    public Dictionary<string, string> CurrencyRates { get; set; } = new();
    public decimal CurrencyBufferPct { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext _)
    {
        if (CurrencyBufferPct is < 0 or > 100)
            yield return new ValidationResult("Буфер курса — от 0 до 100%", [nameof(CurrencyBufferPct)]);
    }
}

public class QuoteLineInputDto : IValidatableObject
{
    [Required] public QuoteLineKind Kind { get; set; }
    [Required, StringLength(255, MinimumLength = 1)] public string Name { get; set; } = "";
    public Guid? ProductId { get; set; }
    public LicensingModel? LicensingModel { get; set; }
    public string? Metric { get; set; }
    public string Currency { get; set; } = "RUB";
    public decimal Qty { get; set; } = 1m;
    public decimal UnitPrice { get; set; }
    public decimal? UnitCost { get; set; }
    public int? TermMonths { get; set; }
    public decimal? SupportPct { get; set; }
    public decimal PartnerDiscountPct { get; set; }
    public decimal ClientDiscountPct { get; set; }
    public string? Note { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext _)
    {
        if (Qty <= 0) yield return new ValidationResult("Количество должно быть положительным", [nameof(Qty)]);
        if (UnitPrice < 0) yield return new ValidationResult("Цена не может быть отрицательной", [nameof(UnitPrice)]);
        if (UnitCost is < 0) yield return new ValidationResult("Себестоимость не может быть отрицательной", [nameof(UnitCost)]);
        if (TermMonths is < 1) yield return new ValidationResult("Срок — не менее 1 месяца", [nameof(TermMonths)]);
        if (SupportPct is < 0 or > 100) yield return new ValidationResult("Поддержка — от 0 до 100%", [nameof(SupportPct)]);
        if (PartnerDiscountPct is < 0 or > 100) yield return new ValidationResult("Партнёрская скидка — от 0 до 100%", [nameof(PartnerDiscountPct)]);
        if (ClientDiscountPct is < 0 or > 100) yield return new ValidationResult("Скидка клиенту — от 0 до 100%", [nameof(ClientDiscountPct)]);
        // Подписочная лицензия без срока молча занижала бы итог — требуем term_months.
        if (Kind == QuoteLineKind.License && LicensingModel == Veha.Domain.Enums.LicensingModel.Subscription && TermMonths is null)
            yield return new ValidationResult("Для подписочной лицензии укажите срок term_months (мес.)", [nameof(TermMonths)]);
    }
}

public record QuoteLineOutDto(
    Guid Id, QuoteLineKind Kind, string Name, Guid? ProductId, LicensingModel? LicensingModel, string? Metric,
    string Currency, decimal Qty, decimal UnitPrice, decimal? UnitCost, int? TermMonths, decimal? SupportPct,
    decimal PartnerDiscountPct, decimal ClientDiscountPct, decimal CostAmount, decimal SellAmount, decimal Margin,
    string? Note);

public class QuoteOutDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public int Version { get; set; }
    public string Title { get; set; } = "";
    public QuoteStatus Status { get; set; }
    public Dictionary<string, string> CurrencyRatesSnapshot { get; set; } = new();
    public decimal CurrencyBufferPct { get; set; }
    public Dictionary<string, string> Totals { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }
}

public class QuoteDetailDto : QuoteOutDto
{
    public List<QuoteLineOutDto> Lines { get; set; } = [];
}

public class QuoteStatusUpdateDto
{
    [Required] public QuoteStatus Status { get; set; }
}
