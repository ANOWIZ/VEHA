using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Veha.Api.Auth;
using Veha.Api.Dtos;
using Veha.Api.Services;

namespace Veha.Api.Controllers;

/// <summary>Финансы проекта: бюджет, факт, маржа, сводка «здоровье». Доступ — только
/// финансовым ролям с доступом к проекту; запись — admin/finance/pm. Порт finance.py.</summary>
[ApiController]
[Route("api/v1/projects/{projectId:guid}/finance")]
[Authorize]
public class FinanceController(
    CurrentUserAccessor current,
    ProjectAccessService access,
    FinanceService finance) : ControllerBase
{
    private const string WriteRoles = "admin,finance,pm";

    [HttpGet]
    public async Task<ProjectFinanceDto> Get(Guid projectId, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireFinancialsAsync(projectId, user, ct);
        return await finance.ProjectFinanceAsync(projectId, ct);
    }

    [HttpGet("budget")]
    public async Task<BudgetOutDto?> GetBudget(Guid projectId, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireFinancialsAsync(projectId, user, ct);
        var budget = await finance.GetBudgetAsync(projectId, ct);
        return budget is null ? null : FinanceService.ToBudgetOut(budget);
    }

    [HttpPut("budget")]
    [Authorize(Roles = WriteRoles)]
    public async Task<BudgetOutDto> UpsertBudget(Guid projectId, BudgetUpsertDto body, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireFinancialsAsync(projectId, user, ct);
        return await finance.UpsertBudgetAsync(projectId, body, user.Id, ct);
    }

    [HttpGet("actuals")]
    public async Task<List<ActualCostOutDto>> ListActuals(Guid projectId, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireFinancialsAsync(projectId, user, ct);
        return await finance.ListActualsAsync(projectId, ct);
    }

    [HttpPost("actuals")]
    [Authorize(Roles = WriteRoles)]
    public async Task<IActionResult> AddActual(Guid projectId, ActualCostCreateDto body, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireFinancialsAsync(projectId, user, ct);
        return StatusCode(StatusCodes.Status201Created, await finance.AddActualAsync(projectId, body, user.Id, ct));
    }

    [HttpGet("margin")]
    public async Task<MarginOutDto> GetMargin(Guid projectId, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireFinancialsAsync(projectId, user, ct);
        var (result, breakdown) = await finance.ComputeMarginAsync(projectId, ct);
        return new MarginOutDto(result.Revenue, result.TotalCost, result.Margin, result.MarginPct, breakdown);
    }
}
