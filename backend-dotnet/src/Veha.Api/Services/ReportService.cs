using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Veha.Api.Config;
using Veha.Api.Dtos;
using Veha.Domain.Entities;
using Veha.Domain.Enums;
using Veha.Domain.Rules;
using Veha.Infrastructure;

namespace Veha.Api.Services;

/// <summary>Отчёты: загрузка ресурсов (utilization) и строки трудозатрат для выгрузки.
/// Порт services/report_service.py. Set-based, без N+1; себестоимость — только финролям.</summary>
public class ReportService(VehaDbContext db, ProjectAccessService access, IOptions<VehaSettings> settings)
{
    private static readonly string[] FinancialRoles = ["admin", "director", "finance", "pm"];
    private static decimal Q(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    public static bool CanSeeFinancials(User user) => FinancialRoles.Any(user.Roles.Contains);

    public Task<Dictionary<Guid, string>> AllUserNamesAsync(CancellationToken ct)
        => db.Users.ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

    private decimal DailyNorm => settings.Value.DefaultWeekNormHours / 5m;

    public async Task<UtilizationReportDto> UtilizationAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var wd = ReportRules.WorkingDays(from, to);
        var capacity = Q(DailyNorm * wd);

        var entries = await db.TimeEntries
            .Where(e => e.WorkDate >= from && e.WorkDate <= to && e.Status == TimeEntryStatus.Approved)
            .Select(e => new { e.UserId, e.Hours, e.ProjectId }).ToListAsync(ct);
        var agg = entries.GroupBy(e => e.UserId).ToDictionary(
            g => g.Key, g => (Hours: g.Sum(x => x.Hours), Projects: g.Select(x => x.ProjectId).Distinct().Count()));

        var users = (await db.Users.Where(u => u.IsActive).ToListAsync(ct))
            .Where(u => !u.Roles.Contains("client")).ToList();

        var rows = new List<UtilizationRowDto>();
        decimal totalBillable = 0;
        foreach (var u in users)
        {
            var (billable, projectsCount) = agg.GetValueOrDefault(u.Id, (0m, 0));
            if (capacity == 0 && billable == 0) continue;   // нерелевантен отчёту
            totalBillable += billable;
            rows.Add(new UtilizationRowDto(u.Id, u.FullName, u.Department,
                billable, capacity, FinanceCalc.UtilizationPct(billable, capacity), projectsCount));
        }
        rows = rows.OrderByDescending(r => r.UtilizationPct).ToList();

        var totalCapacity = Q(capacity * rows.Count);
        return new UtilizationReportDto(from, to, wd, capacity, rows.Count,
            Q(totalBillable), totalCapacity, FinanceCalc.UtilizationPct(totalBillable, totalCapacity), rows);
    }

    public async Task<List<TimesheetExportRow>> TimesheetRowsAsync(
        User user, DateOnly from, DateOnly to, Guid? projectId, TimeEntryStatus? status, bool includeCost, CancellationToken ct)
    {
        var accessible = await access.AccessibleProjectsAsync(user, null, 1000, ct);
        var accessibleIds = accessible.Select(p => p.Id).ToHashSet();
        if (projectId is not null && !accessibleIds.Contains(projectId.Value)) return [];
        var scopeIds = projectId is not null ? [projectId.Value] : accessibleIds;
        if (scopeIds.Count == 0) return [];

        var q = db.TimeEntries.Where(e => e.WorkDate >= from && e.WorkDate <= to && scopeIds.Contains(e.ProjectId));
        if (status is not null) q = q.Where(e => e.Status == status.Value);
        var entries = await q.ToListAsync(ct);

        var userIds = entries.Select(e => e.UserId).Distinct().ToList();
        var projIds = entries.Select(e => e.ProjectId).Distinct().ToList();
        var taskIds = entries.Where(e => e.TaskId != null).Select(e => e.TaskId!.Value).Distinct().ToList();
        var names = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var projs = await db.Projects.Where(p => projIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new { p.Code, p.Name }, ct);
        var tasks = await db.ProjectTasks.Where(t => taskIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name, ct);

        return entries
            .OrderBy(e => e.WorkDate).ThenBy(e => projs.TryGetValue(e.ProjectId, out var pp) ? pp.Code : "")
            .Select(e =>
            {
                var proj = projs.GetValueOrDefault(e.ProjectId);
                var task = e.TaskId is not null ? tasks.GetValueOrDefault(e.TaskId.Value) : null;
                decimal? cost = includeCost ? Q(e.Hours * (e.CostRateSnapshot ?? 0m)) : null;
                return new TimesheetExportRow(e.WorkDate, names.GetValueOrDefault(e.UserId, "—"),
                    proj?.Code ?? "—", proj?.Name ?? "—", task, e.Hours, e.Status, e.Comment, cost);
            })
            .ToList();
    }
}
