using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Veha.Api.Auth;
using Veha.Api.Dtos;
using Veha.Api.Services;
using Veha.Domain.Common;
using Veha.Domain.Enums;

namespace Veha.Api.Controllers;

/// <summary>Реестр рисков проекта. Чтение — ProjectAccess; изменение — ProjectManage.
/// Риск проверяется на принадлежность проекту. Порт api/v1/risks.py.</summary>
[ApiController]
[Route("api/v1/projects/{projectId:guid}/risks")]
[Authorize]
public class RisksController(
    CurrentUserAccessor current, ProjectAccessService access, RiskService risks) : ControllerBase
{
    [HttpGet]
    public async Task<List<RiskOutDto>> List(Guid projectId, [FromQuery] string? status, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireAccessAsync(projectId, user, ct);
        return await risks.ListForProjectAsync(projectId, EnumQuery.Parse<RiskStatus>(status), ct);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid projectId, RiskCreateDto body, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireManageAsync(projectId, user, ct);
        return StatusCode(StatusCodes.Status201Created, await risks.CreateAsync(projectId, body, user.Id, ct));
    }

    [HttpPatch("{riskId:guid}")]
    public async Task<RiskOutDto> Update(Guid projectId, Guid riskId, RiskUpdateDto body, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireManageAsync(projectId, user, ct);
        var risk = await risks.GetAsync(riskId, ct);
        if (risk.ProjectId != projectId) throw new NotFoundException("Риск не найден в этом проекте");
        return await risks.UpdateAsync(riskId, body, user.Id, ct);
    }

    [HttpDelete("{riskId:guid}")]
    public async Task<MessageDto> Delete(Guid projectId, Guid riskId, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireManageAsync(projectId, user, ct);
        var risk = await risks.GetAsync(riskId, ct);
        if (risk.ProjectId != projectId) throw new NotFoundException("Риск не найден в этом проекте");
        await risks.DeleteAsync(riskId, user.Id, ct);
        return new MessageDto("Риск удалён");
    }
}

/// <summary>Портфель рисков (матрица 3×3) — руководству (director/admin/finance/pm).</summary>
[ApiController]
[Route("api/v1/risks")]
[Authorize(Roles = "director,admin,finance,pm")]
public class RiskPortfolioController(CurrentUserAccessor current, RiskService risks) : ControllerBase
{
    [HttpGet("portfolio")]
    public async Task<RiskPortfolioResponseDto> Portfolio(CancellationToken ct)
        => await risks.PortfolioAsync(await current.GetAsync(ct), ct);
}
