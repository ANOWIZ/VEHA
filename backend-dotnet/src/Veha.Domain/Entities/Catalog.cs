using Veha.Domain.Enums;

namespace Veha.Domain.Entities;

/// <summary>Вендор ПО/оборудования.</summary>
public class Vendor : BaseEntity
{
    public string Name { get; set; } = "";
    public string? Country { get; set; }

    public List<Product> Products { get; set; } = [];
}

/// <summary>Продукт вендора. Поиск по имени (pg_trgm в Python; обычный индекс в .NET).</summary>
public class Product : BaseEntity
{
    public Guid VendorId { get; set; }
    public Vendor? Vendor { get; set; }
    public string Name { get; set; } = "";
    public string? Edition { get; set; }
    public LicensingModel LicensingModel { get; set; }
    public string PriceCurrency { get; set; } = "RUB";
    public bool IsRussianRegistry { get; set; }

    public List<PriceListItem> PriceItems { get; set; } = [];
}

/// <summary>Позиция прайс-листа: цена за метрику с периодом действия.</summary>
public class PriceListItem : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public string Metric { get; set; } = "";
    public decimal Price { get; set; }
    public string Currency { get; set; } = "RUB";
    public string? PartnerDiscountLevel { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
}
