using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Veha.Api.Auth;
using Veha.Api.Dtos;
using Veha.Api.Services;

namespace Veha.Api.Controllers;

/// <summary>Каталог: вендоры, продукты, прайс-листы. Чтение — авторизованным;
/// изменение — admin/pm/presale; цены — только ролям с доступом к ценам. Порт catalog.py.</summary>
[ApiController]
[Route("api/v1/catalog")]
[Authorize]
public class CatalogController(CatalogService catalog) : ControllerBase
{
    private const string ManageRoles = "admin,pm,presale";
    private const string PriceRoles = "admin,presale,pm,finance,director";

    [HttpGet("vendors")]
    public Task<List<VendorOutDto>> ListVendors(CancellationToken ct) => catalog.ListVendorsAsync(ct);

    [HttpPost("vendors")]
    [Authorize(Roles = ManageRoles)]
    public async Task<IActionResult> CreateVendor(VendorCreateDto body, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await catalog.CreateVendorAsync(body, ct));

    [HttpGet("products")]
    public Task<Page<ProductOutDto>> SearchProducts(
        [FromQuery] string? q,
        [FromQuery(Name = "vendor_id")] Guid? vendorId,
        [FromQuery, Range(1, 200)] int limit = 50,
        [FromQuery, Range(0, int.MaxValue)] int offset = 0,
        CancellationToken ct = default)
        => catalog.SearchProductsAsync(q, vendorId, limit, offset, ct);

    [HttpGet("products/{productId:guid}")]
    public async Task<ProductDetailDto> GetProduct(Guid productId, CancellationToken ct)
    {
        var product = await catalog.GetProductAsync(productId, ct);
        var vendor = await catalog.GetVendorAsync(product.VendorId, ct);
        var prices = AuthZ.CanSeePrices(User)
            ? await catalog.ListPricesAsync(productId, ct)
            : [];
        return CatalogService.ToDetail(product, vendor, prices);
    }

    [HttpPost("products")]
    [Authorize(Roles = ManageRoles)]
    public async Task<IActionResult> CreateProduct(ProductCreateDto body, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await catalog.CreateProductAsync(body, ct));

    [HttpPatch("products/{productId:guid}")]
    [Authorize(Roles = ManageRoles)]
    public Task<ProductOutDto> UpdateProduct(Guid productId, ProductUpdateDto body, CancellationToken ct)
        => catalog.UpdateProductAsync(productId, body, ct);

    [HttpGet("products/{productId:guid}/prices")]
    [Authorize(Roles = PriceRoles)]
    public Task<List<PriceItemOutDto>> ListPrices(Guid productId, CancellationToken ct)
        => catalog.ListPricesAsync(productId, ct);

    [HttpPost("products/{productId:guid}/prices")]
    [Authorize(Roles = ManageRoles)]
    public async Task<IActionResult> AddPrice(Guid productId, PriceItemCreateDto body, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await catalog.AddPriceAsync(productId, body, ct));
}
