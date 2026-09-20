using Microsoft.EntityFrameworkCore;
using Veha.Domain.Common;
using Veha.Domain.Entities;
using Veha.Infrastructure;

namespace Veha.Api.Services;

/// <summary>Проверки доступа к проекту (порт require_project_access / require_project_manage
/// из core/deps.py). Возвращают проект или бросают 404/403.</summary>
public class ProjectAccessService(VehaDbContext db)
{
    // admin/director/finance видят все проекты.
    private static readonly string[] Privileged = ["admin", "director", "finance"];

    /// <summary>Доступ на чтение: admin/director/finance — все; иначе РП/куратор/участник.</summary>
    public async Task<Project> RequireAccessAsync(Guid projectId, User user, CancellationToken ct = default)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
                      ?? throw new NotFoundException("Проект не найден");

        if (Privileged.Any(user.Roles.Contains))
            return project;
        if (project.ManagerId == user.Id || project.CuratorId == user.Id)
            return project;

        var isMember = await db.ProjectMembers
            .AnyAsync(m => m.ProjectId == projectId && m.UserId == user.Id, ct);
        if (isMember)
            return project;

        throw new ForbiddenException("Нет доступа к этому проекту");
    }

    /// <summary>Управление (изменение/утверждение): admin или РП/куратор проекта.</summary>
    public async Task<Project> RequireManageAsync(Guid projectId, User user, CancellationToken ct = default)
    {
        var project = await RequireAccessAsync(projectId, user, ct);
        if (user.Roles.Contains("admin"))
            return project;
        if (project.ManagerId == user.Id || project.CuratorId == user.Id)
            return project;
        throw new ForbiddenException("Управлять проектом может администратор, РП или куратор");
    }

    // Финансовые роли (как FINANCIAL_ROLES / can_see_financials в Python).
    private static readonly string[] FinancialRoles = ["admin", "director", "finance", "pm"];

    /// <summary>Доступ к финансам проекта: доступ к проекту И финансовая роль.</summary>
    public async Task<Project> RequireFinancialsAsync(Guid projectId, User user, CancellationToken ct = default)
    {
        var project = await RequireAccessAsync(projectId, user, ct);
        if (!FinancialRoles.Any(user.Roles.Contains))
            throw new ForbiddenException("Финансовые данные доступны только ролям с доступом к финансам");
        return project;
    }

    /// <summary>Доступные пользователю проекты (порт project_repo.list_for_user без
    /// текстовых фильтров): admin/director/finance — все; иначе свои (РП/куратор/участник).</summary>
    public async Task<List<Project>> AccessibleProjectsAsync(
        User user, Domain.Enums.ProjectStatus? status, int limit, CancellationToken ct = default)
    {
        var q = db.Projects.AsQueryable();
        if (!Privileged.Any(user.Roles.Contains))
        {
            var uid = user.Id;
            q = q.Where(p => p.ManagerId == uid || p.CuratorId == uid
                || db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == uid));
        }
        if (status is not null) q = q.Where(p => p.Status == status.Value);
        return await q.Take(limit).ToListAsync(ct);
    }
}
