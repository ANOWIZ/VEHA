using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Veha.Api.Auth;
using Veha.Api.Dtos;
using Veha.Api.Services;

namespace Veha.Api.Controllers;

/// <summary>Задачи проекта — вложены в проект для единообразной проверки доступа.
/// Чтение — ProjectAccess; изменение — ProjectManage. Порт api/v1/tasks.py.</summary>
[ApiController]
[Route("api/v1/projects/{projectId:guid}/tasks")]
[Authorize]
public class ProjectTasksController(
    CurrentUserAccessor current,
    ProjectAccessService access,
    TaskService tasks) : ControllerBase
{
    [HttpGet]
    public async Task<List<TaskOutDto>> List(Guid projectId, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireAccessAsync(projectId, user, ct);
        return await tasks.ListForProjectAsync(projectId, ct);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid projectId, TaskCreateDto body, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireManageAsync(projectId, user, ct);
        return StatusCode(StatusCodes.Status201Created, await tasks.CreateAsync(projectId, body, ct));
    }

    [HttpPatch("{taskId:guid}")]
    public async Task<TaskOutDto> Update(Guid projectId, Guid taskId, TaskUpdateDto body, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireManageAsync(projectId, user, ct);
        return await tasks.UpdateAsync(projectId, taskId, body, ct);
    }

    [HttpDelete("{taskId:guid}")]
    public async Task<MessageDto> Delete(Guid projectId, Guid taskId, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireManageAsync(projectId, user, ct);
        await tasks.DeleteAsync(projectId, taskId, ct);
        return new MessageDto("Задача удалена");
    }
}
