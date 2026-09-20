using Veha.Api.Dtos;
using Veha.Api.Services;
using Veha.Domain.Common;
using Veha.Domain.Enums;
using Veha.Tests.Support;

namespace Veha.Tests.Services;

/// <summary>Интеграционные тесты CatalogService: вендоры, продукты (поиск/фильтр),
/// прайс-листы, валидация вендора/продукта.</summary>
public class CatalogServiceTests
{
    private static async Task<Guid> SeedVendor(SqliteTestDb db, string name)
    {
        await using var ctx = db.NewContext();
        var v = await new CatalogService(ctx).CreateVendorAsync(new VendorCreateDto { Name = name }, default);
        return v.Id;
    }

    private static async Task<Guid> SeedProduct(SqliteTestDb db, Guid vendorId, string name, string? edition = null)
    {
        await using var ctx = db.NewContext();
        var p = await new CatalogService(ctx).CreateProductAsync(new ProductCreateDto
        {
            VendorId = vendorId, Name = name, Edition = edition, LicensingModel = LicensingModel.PerUser,
        }, default);
        return p.Id;
    }

    [Fact]
    public async Task Vendors_create_and_list_sorted()
    {
        using var db = new SqliteTestDb();
        await SeedVendor(db, "Zeta");
        await SeedVendor(db, "Alpha");

        await using var ctx = db.NewContext();
        var vendors = await new CatalogService(ctx).ListVendorsAsync(default);
        Assert.Equal(new[] { "Alpha", "Zeta" }, vendors.Select(v => v.Name).ToArray());
    }

    [Fact]
    public async Task Create_product_requires_existing_vendor()
    {
        using var db = new SqliteTestDb();
        await using var ctx = db.NewContext();
        await Assert.ThrowsAsync<NotFoundException>(() => new CatalogService(ctx).CreateProductAsync(
            new ProductCreateDto { VendorId = Guid.NewGuid(), Name = "X", LicensingModel = LicensingModel.PerCore }, default));
    }

    [Fact]
    public async Task Search_products_by_name_edition_and_vendor()
    {
        using var db = new SqliteTestDb();
        var v1 = await SeedVendor(db, "V1");
        var v2 = await SeedVendor(db, "V2");
        await SeedProduct(db, v1, "Database Server", edition: "Enterprise");
        await SeedProduct(db, v1, "Office Suite", edition: "Standard");
        await SeedProduct(db, v2, "Antivirus");

        await using var ctx = db.NewContext();
        var svc = new CatalogService(ctx);

        Assert.Equal(1, (await svc.SearchProductsAsync("database", null, 50, 0, default)).Total);
        Assert.Equal(1, (await svc.SearchProductsAsync("enterprise", null, 50, 0, default)).Total); // по редакции
        Assert.Equal(2, (await svc.SearchProductsAsync(null, v1, 50, 0, default)).Total);           // по вендору
        Assert.Equal(3, (await svc.SearchProductsAsync(null, null, 50, 0, default)).Total);
    }

    [Fact]
    public async Task Get_missing_product_throws()
    {
        using var db = new SqliteTestDb();
        await using var ctx = db.NewContext();
        await Assert.ThrowsAsync<NotFoundException>(() => new CatalogService(ctx).GetProductAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Update_product_partial()
    {
        using var db = new SqliteTestDb();
        var v = await SeedVendor(db, "V");
        var pid = await SeedProduct(db, v, "Old", edition: "Ed");

        await using (var ctx = db.NewContext())
        {
            var updated = await new CatalogService(ctx).UpdateProductAsync(pid,
                new ProductUpdateDto { Name = "New", IsRussianRegistry = true }, default);
            Assert.Equal("New", updated.Name);
            Assert.True(updated.IsRussianRegistry);
            Assert.Equal("Ed", updated.Edition);  // не менялось
        }
    }

    [Fact]
    public async Task Prices_add_and_list_sorted_desc()
    {
        using var db = new SqliteTestDb();
        var v = await SeedVendor(db, "V");
        var pid = await SeedProduct(db, v, "P");

        await using (var ctx = db.NewContext())
        {
            var svc = new CatalogService(ctx);
            await svc.AddPriceAsync(pid, new PriceItemCreateDto { Metric = "user", Price = 100m, ValidFrom = new DateOnly(2025, 1, 1) }, default);
            await svc.AddPriceAsync(pid, new PriceItemCreateDto { Metric = "user", Price = 120m, ValidFrom = new DateOnly(2026, 1, 1) }, default);
        }

        await using (var ctx = db.NewContext())
        {
            var prices = await new CatalogService(ctx).ListPricesAsync(pid, default);
            Assert.Equal(2, prices.Count);
            Assert.Equal(new DateOnly(2026, 1, 1), prices[0].ValidFrom);  // desc
            Assert.Equal(120m, prices[0].Price);
        }
    }

    [Fact]
    public async Task Add_price_to_missing_product_throws()
    {
        using var db = new SqliteTestDb();
        await using var ctx = db.NewContext();
        await Assert.ThrowsAsync<NotFoundException>(() => new CatalogService(ctx).AddPriceAsync(
            Guid.NewGuid(), new PriceItemCreateDto { Metric = "user", Price = 1m, ValidFrom = new DateOnly(2026, 1, 1) }, default));
    }
}
