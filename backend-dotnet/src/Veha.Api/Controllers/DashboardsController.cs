using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Veha.Api.Auth;
using Veha.Api.Dtos;
using Veha.Api.Services;

namespace Veha.Api.Controllers;

/// <summary>Дашборды: портфель проектов. Порт api/v1/dashboards.py.</summary>
[ApiController]
[Route("api/v1/dashboards")]
[Authorize]
public class DashboardsController(CurrentUserAccessor current, DashboardService dashboards) : ControllerBase
{
    [HttpGet("portfolio")]
    [Authorize(Roles = "director,admin,finance,pm")]
    public async Task<PortfolioResponseDto> Portfolio(CancellationToken ct)
        => await dashboards.PortfolioAsync(await current.GetAsync(ct), ct);

    /// <summary>Здоровье моих проектов. Не-финролям — пустой портфель.</summary>
    [HttpGet("me")]
    public async Task<PortfolioResponseDto> My(CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        return DashboardService.CanSeeFinancials(user)
            ? await dashboards.PortfolioAsync(user, ct)
            : DashboardService.Empty();
    }
}
