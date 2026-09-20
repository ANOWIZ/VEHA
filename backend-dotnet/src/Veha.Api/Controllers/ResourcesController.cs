using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Veha.Api.Dtos;
using Veha.Api.Services;

namespace Veha.Api.Controllers;

/// <summary>Ресурсное планирование: тепловая карта загрузки и план. Доступ —
/// pm/director/admin/finance. Порт api/v1/resources.py.</summary>
[ApiController]
[Route("api/v1/resources")]
[Authorize(Roles = "pm,director,admin,finance")]
public class ResourcesController(ResourceService resources) : ControllerBase
{
    [HttpGet("heatmap")]
    public Task<HeatmapResponseDto> Heatmap(
        [FromQuery(Name = "week_from")] DateOnly? weekFrom,
        [FromQuery, Range(1, 26)] int weeks = 8,
        CancellationToken ct = default)
        => resources.HeatmapAsync(weekFrom ?? DateOnly.FromDateTime(DateTime.UtcNow), weeks, ct);

    [HttpPost("plan")]
    public async Task<IActionResult> UpsertPlan(ResourcePlanUpsertDto body, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await resources.UpsertPlanAsync(body, ct));
}
