using Microsoft.EntityFrameworkCore;
using Veha.Api.Dtos;
using Veha.Domain.Common;
using Veha.Domain.Entities;
using Veha.Domain.Enums;
using Veha.Domain.Rules;
using Veha.Infrastructure;

namespace Veha.Api.Services;

/// <summary>Сервис проектов: жизненный цикл по стадиям, участники, вехи, аудит.
/// Порт services/project_service.py + repositories/project_repo.py.</summary>
public class ProjectService(VehaDbContext db, AuditService audit)
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    // --- Проект ---
    public async Task<ProjectOutDto> CreateAsync(ProjectCreateDto dto, Guid actorId, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == dto.ManagerId, ct))
            throw new NotFoundException("Руководитель проекта не найден");

        var year = (dto.PlannedStart ?? Today).Year;
        var code = await NextCodeAsync(year, ct);

        var project = new Project
        {
            Code = code,
            Name = dto.Name,
            ClientId = dto.ClientId,
            Type = dto.Type,
            ManagerId = dto.ManagerId,
            CuratorId = dto.CuratorId,
            Stage = Stage.Presale,
            Status = ProjectStatus.Active,
            PlannedStart = dto.PlannedStart,
            PlannedEnd = dto.PlannedEnd,
            BudgetRevenue = dto.BudgetRevenue,
            ContractRef = dto.ContractRef,
        };
        db.Projects.Add(project);
        db.ProjectStageTransitions.Add(new ProjectStageTransition
        {
            ProjectId = project.Id,
            FromStage = null,
            ToStage = Stage.Presale,
            Reason = "Создание проекта",
            CreatedBy = actorId,
        });
        // Один SaveChanges (внутри audit) — проект + переход + аудит атомарно.
        await audit.RecordAsync("Project", project.Id, AuditAction.Create, actorId,
            new { code, name = project.Name }, ct);
        return ToOut(project);
    }

    /// <summary>Следующий код PRJ-YYYY-NNN: от максимального суффикса (включая
    /// soft-deleted — их коды заняты unique-ограничением).</summary>
    private async Task<string> NextCodeAsync(int year, CancellationToken ct)
    {
        var prefix = $"PRJ-{year}-";
        var maxCode = await db.Projects
            .IgnoreQueryFilters()
            .Where(p => EF.Functions.Like(p.Code, prefix + "%"))
            .MaxAsync(p => (string?)p.Code, ct);

        var nextNum = 1;
        if (!string.IsNullOrEmpty(maxCode))
        {
            var suffix = maxCode[(maxCode.LastIndexOf('-') + 1)..];
            if (int.TryParse(suffix, out var n)) nextNum = n + 1;
        }
        return $"{prefix}{nextNum:D3}";
    }

    public async Task<Page<ProjectOutDto>> ListForUserAsync(
        User user, ProjectStatus? status, Stage? stage, Guid? managerId,
        string? query, int limit, int offset, CancellationToken ct)
    {
        var q = db.Projects.AsQueryable();

        var privileged = new[] { "admin", "director", "finance" }.Any(user.Roles.Contains);
        if (!privileged)
        {
            var uid = user.Id;
            q = q.Where(p => p.ManagerId == uid || p.CuratorId == uid
                || db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == uid));
        }

        if (status is not null) q = q.Where(p => p.Status == status.Value);
        if (stage is not null) q = q.Where(p => p.Stage == stage.Value);
        if (managerId is not null) q = q.Where(p => p.ManagerId == managerId.Value);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var pat = $"%{query.ToLower()}%";
            q = q.Where(p => EF.Functions.Like(p.Name.ToLower(), pat)
                             || EF.Functions.Like(p.Code.ToLower(), pat));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(p => p.CreatedAt).Skip(offset).Take(limit).ToListAsync(ct);
        return new Page<ProjectOutDto>(items.Select(ToOut).ToList(), total, limit, offset);
    }

    public async Task<Project> GetAsync(Guid projectId, CancellationToken ct)
        => await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
           ?? throw new NotFoundException("Проект не найден");

    public async Task<ProjectOutDto> UpdateAsync(Guid projectId, ProjectUpdateDto dto, Guid actorId, CancellationToken ct)
    {
        var project = await GetAsync(projectId, ct);

        var closed = project.Status is ProjectStatus.Closed or ProjectStatus.Cancelled;
        if (closed)
        {
            var onlyStatus = dto.Name is null && dto.Type is null && dto.ManagerId is null
                && dto.CuratorId is null && dto.PlannedStart is null && dto.PlannedEnd is null
                && dto.ActualStart is null && dto.ActualEnd is null && dto.BudgetRevenue is null
                && dto.ContractRef is null;
            if (!onlyStatus)
                throw new ProjectClosedException("Проект закрыт/отменён: доступно только изменение статуса");
        }

        var diff = new Dictionary<string, object>();
        void Track(string key, object? oldV, object? newV) => diff[key] = new { from = oldV, to = newV };

        if (dto.Name is not null && dto.Name != project.Name)
        { Track("name", project.Name, dto.Name); project.Name = dto.Name; }
        if (dto.Type is not null && dto.Type.Value != project.Type)
        { Track("type", project.Type, dto.Type.Value); project.Type = dto.Type.Value; }
        if (dto.ManagerId is not null && dto.ManagerId.Value != project.ManagerId)
        { Track("manager_id", project.ManagerId, dto.ManagerId.Value); project.ManagerId = dto.ManagerId.Value; }
        if (dto.CuratorId is not null && dto.CuratorId != project.CuratorId)
        { Track("curator_id", project.CuratorId, dto.CuratorId); project.CuratorId = dto.CuratorId; }
        if (dto.Status is not null && dto.Status.Value != project.Status)
        { Track("status", project.Status, dto.Status.Value); project.Status = dto.Status.Value; }
        if (dto.PlannedStart is not null && dto.PlannedStart != project.PlannedStart)
        { Track("planned_start", project.PlannedStart, dto.PlannedStart); project.PlannedStart = dto.PlannedStart; }
        if (dto.PlannedEnd is not null && dto.PlannedEnd != project.PlannedEnd)
        { Track("planned_end", project.PlannedEnd, dto.PlannedEnd); project.PlannedEnd = dto.PlannedEnd; }
        if (dto.ActualStart is not null && dto.ActualStart != project.ActualStart)
        { Track("actual_start", project.ActualStart, dto.ActualStart); project.ActualStart = dto.ActualStart; }
        if (dto.ActualEnd is not null && dto.ActualEnd != project.ActualEnd)
        { Track("actual_end", project.ActualEnd, dto.ActualEnd); project.ActualEnd = dto.ActualEnd; }
        if (dto.BudgetRevenue is not null && dto.BudgetRevenue.Value != project.BudgetRevenue)
        { Track("budget_revenue", project.BudgetRevenue, dto.BudgetRevenue.Value); project.BudgetRevenue = dto.BudgetRevenue.Value; }
        if (dto.ContractRef is not null && dto.ContractRef != project.ContractRef)
        { Track("contract_ref", project.ContractRef, dto.ContractRef); project.ContractRef = dto.ContractRef; }

        if (diff.Count > 0)
            await audit.RecordAsync("Project", project.Id, AuditAction.Update, actorId, diff, ct);
        else
            await db.SaveChangesAsync(ct);
        return ToOut(project);
    }

    public async Task<ProjectOutDto> ChangeStageAsync(Guid projectId, Stage toStage, string? reason, Guid actorId, CancellationToken ct)
    {
        var project = await GetAsync(projectId, ct);
        if (project.Status is ProjectStatus.Closed or ProjectStatus.Cancelled)
            throw new ProjectClosedException("Нельзя менять стадию закрытого/отменённого проекта");

        try { StageRules.ValidateTransition(project.Stage, toStage, reason); }
        catch (DomainValidationException ex) { throw new InvalidStageTransitionException(ex.Message); }

        var fromStage = project.Stage;
        project.Stage = toStage;
        if (toStage == Stage.Implementation && project.ActualStart is null)
            project.ActualStart = Today;
        if (toStage == Stage.Closed)
        {
            project.ActualEnd = Today;
            project.Status = ProjectStatus.Closed;
        }
        db.ProjectStageTransitions.Add(new ProjectStageTransition
        {
            ProjectId = project.Id, FromStage = fromStage, ToStage = toStage,
            Reason = reason, CreatedBy = actorId,
        });
        await audit.RecordAsync("Project", project.Id, AuditAction.StageChange, actorId,
            new { from = fromStage, to = toStage, reason }, ct);
        return ToOut(project);
    }

    public async Task DeleteAsync(Guid projectId, Guid actorId, CancellationToken ct)
    {
        var project = await GetAsync(projectId, ct);
        project.DeletedAt = DateTimeOffset.UtcNow;
        await audit.RecordAsync("Project", project.Id, AuditAction.Delete, actorId, null, ct);
    }

    // --- Участники ---
    public async Task<List<MemberOutDto>> ListMembersAsync(Guid projectId, CancellationToken ct)
        => (await db.ProjectMembers.Where(m => m.ProjectId == projectId).ToListAsync(ct))
            .Select(ToMemberOut).ToList();

    public async Task<MemberOutDto> AddMemberAsync(Guid projectId, MemberCreateDto dto, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == dto.UserId, ct))
            throw new NotFoundException("Пользователь не найден");
        if (await db.ProjectMembers.AnyAsync(
                m => m.ProjectId == projectId && m.UserId == dto.UserId && m.Role == dto.Role, ct))
            throw new ConflictException("Пользователь уже участвует в этой роли");

        var member = new ProjectMember
        {
            ProjectId = projectId, UserId = dto.UserId, Role = dto.Role,
            BillRate = dto.BillRate, PeriodFrom = dto.PeriodFrom, PeriodTo = dto.PeriodTo,
        };
        db.ProjectMembers.Add(member);
        await db.SaveChangesAsync(ct);
        return ToMemberOut(member);
    }

    public async Task RemoveMemberAsync(Guid projectId, Guid memberId, CancellationToken ct)
    {
        var member = await db.ProjectMembers
            .FirstOrDefaultAsync(m => m.Id == memberId && m.ProjectId == projectId, ct)
            ?? throw new NotFoundException("Участник не найден");
        member.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    // --- Вехи ---
    public async Task<List<MilestoneOutDto>> ListMilestonesAsync(Guid projectId, CancellationToken ct)
        => (await db.Milestones.Where(m => m.ProjectId == projectId).ToListAsync(ct))
            .Select(ToMilestoneOut).ToList();

    public async Task<MilestoneOutDto> AddMilestoneAsync(Guid projectId, MilestoneCreateDto dto, CancellationToken ct)
    {
        var milestone = new Milestone
        {
            ProjectId = projectId, Name = dto.Name, MilestoneDate = dto.MilestoneDate,
            IsPayment = dto.IsPayment, Amount = dto.Amount,
        };
        db.Milestones.Add(milestone);
        await db.SaveChangesAsync(ct);
        return ToMilestoneOut(milestone);
    }

    // --- История стадий ---
    public async Task<List<StageTransitionOutDto>> TransitionsAsync(Guid projectId, CancellationToken ct)
        => (await db.ProjectStageTransitions
                .Where(t => t.ProjectId == projectId)
                .OrderBy(t => t.CreatedAt)
                .ToListAsync(ct))
            .Select(ToTransitionOut).ToList();

    // --- Мапперы ---
    private static void FillBase(ProjectOutDto d, Project p)
    {
        d.Id = p.Id; d.Code = p.Code; d.Name = p.Name; d.ClientId = p.ClientId; d.Type = p.Type;
        d.ManagerId = p.ManagerId; d.CuratorId = p.CuratorId; d.Stage = p.Stage; d.Status = p.Status;
        d.PlannedStart = p.PlannedStart; d.PlannedEnd = p.PlannedEnd;
        d.ActualStart = p.ActualStart; d.ActualEnd = p.ActualEnd;
        d.BudgetRevenue = p.BudgetRevenue; d.ContractRef = p.ContractRef; d.CreatedAt = p.CreatedAt;
    }

    public static ProjectOutDto ToOut(Project p)
    {
        var d = new ProjectOutDto();
        FillBase(d, p);
        return d;
    }

    public static ProjectDetailDto ToDetail(Project p, List<MemberOutDto> members, List<MilestoneOutDto> milestones)
    {
        var d = new ProjectDetailDto { Members = members, Milestones = milestones };
        FillBase(d, p);
        return d;
    }

    private static MemberOutDto ToMemberOut(ProjectMember m)
        => new(m.Id, m.UserId, m.Role, m.BillRate, m.PeriodFrom, m.PeriodTo);

    private static MilestoneOutDto ToMilestoneOut(Milestone m)
        => new(m.Id, m.Name, m.MilestoneDate, m.IsPayment, m.Amount);

    private static StageTransitionOutDto ToTransitionOut(ProjectStageTransition t)
        => new(t.Id, t.FromStage, t.ToStage, t.Reason, t.CreatedBy, t.CreatedAt);
}
