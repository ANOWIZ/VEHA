using Microsoft.EntityFrameworkCore;
using Veha.Api.Dtos;
using Veha.Domain.Entities;
using Veha.Domain.Enums;
using Veha.Domain.Rules;
using Veha.Infrastructure;

namespace Veha.Api.Services;

/// <summary>Дашборды: портфель проектов (маржа/часы/риски) set-based, без N+1.
/// Порт services/dashboard_service.py. Маржа/себестоимость — только финролям.</summary>
public class DashboardService(VehaDbContext db, ProjectAccessService access)
{
    private const decimal LowMarginPct = 15m;
    private const decimal HoursOverrunPct = 110m;
    private static readonly string[] FinancialRoles = ["admin", "director", "finance", "pm"];

    public static bool CanSeeFinancials(User user) => FinancialRoles.Any(user.Roles.Contains);

    public static PortfolioResponseDto Empty() => new(0, 0m, 0m, 0m, 0, []);

    public async Task<PortfolioResponseDto> PortfolioAsync(User user, CancellationToken ct)
    {
        var items = await access.AccessibleProjectsAsync(user, ProjectStatus.Active, 500, ct);
        var ids = items.Select(p => p.Id).ToList();

        // Пакетные агрегаты (материализуем и суммируем в памяти — на Postgres SUM в БД).
        var teRows = await db.TimeEntries
            .Where(e => ids.Contains(e.ProjectId) && e.Status == TimeEntryStatus.Approved)
            .Select(e => new { e.ProjectId, e.Hours, e.CostRateSnapshot }).ToListAsync(ct);
        var approved = teRows.GroupBy(r => r.ProjectId).ToDictionary(
            g => g.Key, g => (Cost: g.Sum(x => x.Hours * (x.CostRateSnapshot ?? 0m)), Hours: g.Sum(x => x.Hours)));

        var taskRows = await db.ProjectTasks.Where(t => ids.Contains(t.ProjectId))
            .Select(t => new { t.ProjectId, t.PlannedHours }).ToListAsync(ct);
        var planned = taskRows.GroupBy(r => r.ProjectId).ToDictionary(g => g.Key, g => g.Sum(x => x.PlannedHours));

        var actualRows = await db.ActualCosts.Where(a => ids.Contains(a.ProjectId))
            .Select(a => new { a.ProjectId, a.Amount }).ToListAsync(ct);
        var actuals = actualRows.GroupBy(r => r.ProjectId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var budgets = (await db.ProjectBudgets.Where(b => ids.Contains(b.ProjectId)).ToListAsync(ct))
            .ToDictionary(b => b.ProjectId);

        var rows = new List<PortfolioRowDto>();
        decimal totalRevenue = 0, totalCost = 0;
        foreach (var p in items)
        {
            var (tsCost, actualHours) = approved.GetValueOrDefault(p.Id, (0m, 0m));
            var plannedH = planned.GetValueOrDefault(p.Id);
            var revenue = budgets.TryGetValue(p.Id, out var b) ? b.PlannedRevenue : p.BudgetRevenue;
            var cost = tsCost + actuals.GetValueOrDefault(p.Id);
            var margin = FinanceCalc.ComputeMargin(revenue, cost);
            var overrun = FinanceCalc.HoursOverrunPct(plannedH, actualHours);

            var risks = new List<string>();
            if (margin.MarginPct < LowMarginPct) risks.Add("low_margin");
            if (plannedH > 0 && actualHours / plannedH * 100m > HoursOverrunPct) risks.Add("hours_overrun");

            totalRevenue += margin.Revenue;
            totalCost += margin.TotalCost;
            rows.Add(new PortfolioRowDto(p.Id, p.Code, p.Name, p.Stage, p.Status, p.ManagerId,
                margin.Revenue, margin.Margin, margin.MarginPct, plannedH, actualHours, overrun, risks));
        }
        rows = rows.OrderBy(r => r.MarginPct).ToList();  // рискованные (низкая маржа) сверху

        var marginTotal = FinanceCalc.ComputeMargin(totalRevenue, totalCost);
        return new PortfolioResponseDto(
            rows.Count, marginTotal.Revenue, marginTotal.Margin, marginTotal.MarginPct,
            rows.Count(r => r.Risks.Count > 0), rows);
    }
}
