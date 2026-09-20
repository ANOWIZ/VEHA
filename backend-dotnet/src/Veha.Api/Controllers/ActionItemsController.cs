using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Veha.Api.Auth;
using Veha.Api.Dtos;
using Veha.Api.Services;
using Veha.Domain.Common;

namespace Veha.Api.Controllers;

/// <summary>Ожидания от Заказчика — внутренняя сторона (РП/команда проекта).
/// Порт api/v1/action_items.py.</summary>
[ApiController]
[Route("api/v1/projects/{projectId:guid}/action-items")]
[Authorize]
public class ActionItemsController(
    CurrentUserAccessor current, ProjectAccessService access, CustomerService customer) : ControllerBase
{
    [HttpGet]
    public async Task<List<ActionItemOutDto>> List(Guid projectId, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireAccessAsync(projectId, user, ct);
        return await customer.ListForProjectAsync(projectId, ct);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid projectId, ActionItemCreateDto body, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireManageAsync(projectId, user, ct);
        return StatusCode(StatusCodes.Status201Created, await customer.CreateActionItemAsync(projectId, body, user.Id, ct));
    }

    [HttpPost("{itemId:guid}/accept")]
    public async Task<ActionItemOutDto> Accept(Guid projectId, Guid itemId, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireManageAsync(projectId, user, ct);
        var item = await customer.GetItemEntityAsync(itemId, ct);
        if (item.ProjectId != projectId) throw new NotFoundException("Ожидание не найдено в этом проекте");
        return await customer.AcceptAsync(itemId, user.Id, ct);
    }

    [HttpGet("artifacts/{artifactId:guid}/download")]
    public async Task<IActionResult> Download(Guid projectId, Guid artifactId, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireAccessAsync(projectId, user, ct);
        var artifact = await customer.GetArtifactEntityAsync(artifactId, ct);
        if (artifact.ProjectId != projectId) throw new NotFoundException("Файл не найден");
        var data = await customer.ReadArtifactAsync(artifact, ct);
        return File(data, artifact.ContentType ?? "application/octet-stream", artifact.Filename);
    }
}
