using Microsoft.EntityFrameworkCore;
using Veha.Api.Dtos;
using Veha.Domain.Common;
using Veha.Domain.Entities;
using Veha.Infrastructure;

namespace Veha.Api.Services;

/// <summary>Каталог: вендоры, продукты (поиск по имени/редакции), прайс-листы.
/// Порт services/catalog_service.py + repositories/catalog_repo.py.</summary>
public class CatalogService(VehaDbContext db)
{
    // --- Вендоры ---
    public async Task<List<VendorOutDto>> ListVendorsAsync(CancellationToken ct)
        => (await db.Vendors.OrderBy(v => v.Name).Take(500).ToListAsync(ct))
            .Select(ToVendorOut).ToList();

    public async Task<VendorOutDto> CreateVendorAsync(VendorCreateDto dto, CancellationToken ct)
    {
        var v = new Vendor { Name = dto.Name, Country = dto.Country };
        db.Vendors.Add(v);
        await db.SaveChangesAsync(ct);
        return ToVendorOut(v);
    }

    // --- Продукты ---
    public async Task<Page<ProductOutDto>> SearchProductsAsync(
        string? q, Guid? vendorId, int limit, int offset, CancellationToken ct)
    {
        var query = db.Products.AsQueryable();
        if (vendorId is not null) query = query.Where(p => p.VendorId == vendorId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var pat = $"%{q.ToLower()}%";
            query = query.Where(p =>
                EF.Functions.Like(p.Name.ToLower(), pat) ||
                (p.Edition != null && EF.Functions.Like(p.Edition.ToLower(), pat)));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(p => p.Name).Skip(offset).Take(limit).ToListAsync(ct);
        return new Page<ProductOutDto>(items.Select(ToProductOut).ToList(), total, limit, offset);
    }

    public async Task<Product> GetProductAsync(Guid productId, CancellationToken ct)
        => await db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct)
           ?? throw new NotFoundException("Продукт не найден");

    public Task<Vendor?> GetVendorAsync(Guid vendorId, CancellationToken ct)
        => db.Vendors.FirstOrDefaultAsync(v => v.Id == vendorId, ct);

    public async Task<ProductOutDto> CreateProductAsync(ProductCreateDto dto, CancellationToken ct)
    {
        if (!await db.Vendors.AnyAsync(v => v.Id == dto.VendorId, ct))
            throw new NotFoundException("Вендор не найден");
        var p = new Product
        {
            VendorId = dto.VendorId, Name = dto.Name, Edition = dto.Edition,
            LicensingModel = dto.LicensingModel, PriceCurrency = dto.PriceCurrency,
            IsRussianRegistry = dto.IsRussianRegistry,
        };
        db.Products.Add(p);
        await db.SaveChangesAsync(ct);
        return ToProductOut(p);
    }

    public async Task<ProductOutDto> UpdateProductAsync(Guid productId, ProductUpdateDto dto, CancellationToken ct)
    {
        var p = await GetProductAsync(productId, ct);
        if (dto.Name is not null) p.Name = dto.Name;
        if (dto.Edition is not null) p.Edition = dto.Edition;
        if (dto.LicensingModel is not null) p.LicensingModel = dto.LicensingModel.Value;
        if (dto.PriceCurrency is not null) p.PriceCurrency = dto.PriceCurrency;
        if (dto.IsRussianRegistry is not null) p.IsRussianRegistry = dto.IsRussianRegistry.Value;
        await db.SaveChangesAsync(ct);
        return ToProductOut(p);
    }

    // --- Прайс ---
    public async Task<List<PriceItemOutDto>> ListPricesAsync(Guid productId, CancellationToken ct)
    {
        await GetProductAsync(productId, ct);
        return (await db.PriceListItems
                .Where(pi => pi.ProductId == productId)
                .OrderByDescending(pi => pi.ValidFrom)
                .ToListAsync(ct))
            .Select(ToPriceOut).ToList();
    }

    public async Task<PriceItemOutDto> AddPriceAsync(Guid productId, PriceItemCreateDto dto, CancellationToken ct)
    {
        await GetProductAsync(productId, ct);
        var pi = new PriceListItem
        {
            ProductId = productId, Metric = dto.Metric, Price = dto.Price, Currency = dto.Currency,
            PartnerDiscountLevel = dto.PartnerDiscountLevel, ValidFrom = dto.ValidFrom, ValidTo = dto.ValidTo,
        };
        db.PriceListItems.Add(pi);
        await db.SaveChangesAsync(ct);
        return ToPriceOut(pi);
    }

    // --- Мапперы ---
    public static VendorOutDto ToVendorOut(Vendor v) => new(v.Id, v.Name, v.Country);

    public static ProductOutDto ToProductOut(Product p) => new()
    {
        Id = p.Id, VendorId = p.VendorId, Name = p.Name, Edition = p.Edition,
        LicensingModel = p.LicensingModel, PriceCurrency = p.PriceCurrency, IsRussianRegistry = p.IsRussianRegistry,
    };

    public static PriceItemOutDto ToPriceOut(PriceListItem p) => new(
        p.Id, p.ProductId, p.Metric, p.Price, p.Currency, p.PartnerDiscountLevel, p.ValidFrom, p.ValidTo);

    public static ProductDetailDto ToDetail(Product p, Vendor? vendor, List<PriceItemOutDto> prices)
    {
        var d = new ProductDetailDto
        {
            Id = p.Id, VendorId = p.VendorId, Name = p.Name, Edition = p.Edition,
            LicensingModel = p.LicensingModel, PriceCurrency = p.PriceCurrency, IsRussianRegistry = p.IsRussianRegistry,
            Vendor = vendor is null ? null : ToVendorOut(vendor),
            PriceItems = prices,
        };
        return d;
    }
}
