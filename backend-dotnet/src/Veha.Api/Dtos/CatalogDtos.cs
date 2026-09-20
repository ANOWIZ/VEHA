using System.ComponentModel.DataAnnotations;
using Veha.Domain.Enums;

namespace Veha.Api.Dtos;

// Порт schemas/catalog.py. Цены (PriceItemOut) — финансово-чувствительны, отдаются
// только ролям с доступом к ценам (в контроллере/RBAC).

// --- Вендор ---
public class VendorCreateDto
{
    [Required, StringLength(255, MinimumLength = 1)]
    public string Name { get; set; } = "";
    public string? Country { get; set; }
}

public record VendorOutDto(Guid Id, string Name, string? Country);

// --- Продукт ---
public class ProductCreateDto
{
    [Required] public Guid VendorId { get; set; }
    [Required, StringLength(255, MinimumLength = 1)]
    public string Name { get; set; } = "";
    public string? Edition { get; set; }
    [Required] public LicensingModel LicensingModel { get; set; }
    public string PriceCurrency { get; set; } = "RUB";
    public bool IsRussianRegistry { get; set; }
}

public class ProductUpdateDto
{
    [StringLength(255, MinimumLength = 1)]
    public string? Name { get; set; }
    public string? Edition { get; set; }
    public LicensingModel? LicensingModel { get; set; }
    public string? PriceCurrency { get; set; }
    public bool? IsRussianRegistry { get; set; }
}

public class ProductOutDto
{
    public Guid Id { get; set; }
    public Guid VendorId { get; set; }
    public string Name { get; set; } = "";
    public string? Edition { get; set; }
    public LicensingModel LicensingModel { get; set; }
    public string PriceCurrency { get; set; } = "RUB";
    public bool IsRussianRegistry { get; set; }
}

public class ProductDetailDto : ProductOutDto
{
    public VendorOutDto? Vendor { get; set; }
    public List<PriceItemOutDto> PriceItems { get; set; } = [];
}

// --- Прайс ---
public class PriceItemCreateDto : IValidatableObject
{
    [Required, StringLength(64, MinimumLength = 1)]
    public string Metric { get; set; } = "";
    public decimal Price { get; set; }
    public string Currency { get; set; } = "RUB";
    public string? PartnerDiscountLevel { get; set; }
    [Required] public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext _)
    {
        if (Price < 0)
            yield return new ValidationResult("Цена не может быть отрицательной", [nameof(Price)]);
    }
}

public record PriceItemOutDto(
    Guid Id, Guid ProductId, string Metric, decimal Price, string Currency,
    string? PartnerDiscountLevel, DateOnly ValidFrom, DateOnly? ValidTo);
