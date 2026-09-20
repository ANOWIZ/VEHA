using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Veha.Api.Auth;
using Veha.Api.Dtos;
using Veha.Api.Services;
using Veha.Domain.Enums;

namespace Veha.Api.Controllers;

/// <summary>Проекты: CRUD, стадии, участники, вехи, история. Список фильтруется по
/// доступу (admin/director/finance — все; иначе свои). Порт api/v1/projects.py.</summary>
[ApiController]
[Route("api/v1/projects")]
[Authorize]
public class ProjectsController(
    CurrentUserAccessor current,
    ProjectAccessService access,
    ProjectService projects) : ControllerBase
{
    private const string CreateRoles = "admin,pm,presale,director";

    [HttpGet]
    public async Task<Page<ProjectOutDto>> List(
        [FromQuery] string? status,
        [FromQuery] string? stage,
        [FromQuery(Name = "manager_id")] Guid? managerId,
        [FromQuery] string? q,
        [FromQuery, Range(1, 500)] int limit = 50,
        [FromQuery, Range(0, int.MaxValue)] int offset = 0,
        CancellationToken ct = default)
    {
        var user = await current.GetAsync(ct);
        return await projects.ListForUserAsync(
            user, EnumQuery.Parse<ProjectStatus>(status), EnumQuery.Parse<Stage>(stage),
            managerId, q, limit, offset, ct);
    }

    [HttpPost]
    [Authorize(Roles = CreateRoles)]
    public async Task<IActionResult> Create(ProjectCreateDto body, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        return StatusCode(StatusCodes.Status201Created, await projects.CreateAsync(body, user.Id, ct));
    }

    [HttpGet("{projectId:guid}")]
    public async Task<ProjectDetailDto> Get(Guid projectId, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        var project = await access.RequireAccessAsync(projectId, user, ct);
        var members = MaskMembers(await projects.ListMembersAsync(projectId, ct));
        var milestones = await projects.ListMilestonesAsync(projectId, ct);
        return ProjectService.ToDetail(project, members, milestones);
    }

    [HttpPatch("{projectId:guid}")]
    public async Task<ProjectOutDto> Update(Guid projectId, ProjectUpdateDto body, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireManageAsync(projectId, user, ct);
        return await projects.UpdateAsync(projectId, body, user.Id, ct);
    }

    [HttpDelete("{projectId:guid}")]
    public async Task<MessageDto> Delete(Guid projectId, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireManageAsync(projectId, user, ct);
        await projects.DeleteAsync(projectId, user.Id, ct);
        return new MessageDto("Проект удалён");
    }

    // --- Стадии ---
    [HttpPost("{projectId:guid}/stage")]
    public async Task<ProjectOutDto> ChangeStage(Guid projectId, StageChangeRequestDto body, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireManageAsync(projectId, user, ct);
        return await projects.ChangeStageAsync(projectId, body.ToStage, body.Reason, user.Id, ct);
    }

    [HttpGet("{projectId:guid}/transitions")]
    public async Task<List<StageTransitionOutDto>> Transitions(Guid projectId, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireAccessAsync(projectId, user, ct);
        return await projects.TransitionsAsync(projectId, ct);
    }

    // --- Участники ---
    [HttpGet("{projectId:guid}/members")]
    public async Task<List<MemberOutDto>> Members(Guid projectId, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireAccessAsync(projectId, user, ct);
        return MaskMembers(await projects.ListMembersAsync(projectId, ct));
    }

    [HttpPost("{projectId:guid}/members")]
    public async Task<IActionResult> AddMember(Guid projectId, MemberCreateDto body, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireManageAsync(projectId, user, ct);
        return StatusCode(StatusCodes.Status201Created, await projects.AddMemberAsync(projectId, body, ct));
    }

    [HttpDelete("{projectId:guid}/members/{memberId:guid}")]
    public async Task<MessageDto> RemoveMember(Guid projectId, Guid memberId, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireManageAsync(projectId, user, ct);
        await projects.RemoveMemberAsync(projectId, memberId, ct);
        return new MessageDto("Участник удалён");
    }

    // --- Вехи ---
    [HttpGet("{projectId:guid}/milestones")]
    public async Task<List<MilestoneOutDto>> Milestones(Guid projectId, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireAccessAsync(projectId, user, ct);
        return await projects.ListMilestonesAsync(projectId, ct);
    }

    [HttpPost("{projectId:guid}/milestones")]
    public async Task<IActionResult> AddMilestone(Guid projectId, MilestoneCreateDto body, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await access.RequireManageAsync(projectId, user, ct);
        return StatusCode(StatusCodes.Status201Created, await projects.AddMilestoneAsync(projectId, body, ct));
    }

    // bill_rate (ставка продажи) — только ролям с доступом к финансам (_members_out).
    private List<MemberOutDto> MaskMembers(List<MemberOutDto> members)
        => AuthZ.CanSeeFinancials(User)
            ? members
            : members.Select(m => m with { BillRate = null }).ToList();
}
