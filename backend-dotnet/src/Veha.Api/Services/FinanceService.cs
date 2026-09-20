using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Veha.Api.Dtos;
using Veha.Domain.Common;
using Veha.Domain.Entities;
using Veha.Domain.Enums;
using Veha.Domain.Rules;
using Veha.Infrastructure;

namespace Veha.Api.Services;

/// <summary>Финансы проекта: бюджет, фактические затраты, маржинальность, прогноз.
/// ФОТ считается из approved-таймшитов (часы × снимок ставки); остальное — actual_costs.
/// Порт services/finance_service.py + finance_repo.py + finance_calc.py.</summary>
public class FinanceService(VehaDbContext db, AuditService audit)
{
    private static decimal ParseDec(string s) => decimal.Parse(s, CultureInfo.InvariantCulture);
    private static string Str(decimal d) => d.ToString(CultureInfo.InvariantCulture);

    private async Task<Project> ProjectAsync(Guid projectId, CancellationToken ct)
        => await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
           ?? throw new NotFoundException("Проект не найден");

    // ---------- Бюджет ----------
    public Task<ProjectBudget?> GetBudgetAsync(Guid projectId, CancellationToken ct)
        => db.ProjectBudgets.FirstOrDefaultAsync(b => b.ProjectId == projectId, ct);

    public async Task<BudgetOutDto> UpsertBudgetAsync(Guid projectId, BudgetUpsertDto dto, Guid actorId, CancellationToken ct)
    {
        var project = await ProjectAsync(projectId, ct);
        if (project.Status is ProjectStatus.Closed or ProjectStatus.Cancelled)
            throw new ProjectClosedException("Нельзя менять бюджет закрытого или отменённого проекта");

        var budget = await GetBudgetAsync(projectId, ct);
        object? old = budget is null ? null : new
        {
            planned_revenue = Str(budget.PlannedRevenue),
            planned_costs = new Dictionary<string, string>(budget.PlannedCosts),
        };

        if (budget is null)
        {
            budget = new ProjectBudget { ProjectId = projectId, PlannedRevenue = dto.PlannedRevenue, PlannedCosts = dto.PlannedCosts };
            db.ProjectBudgets.Add(budget);
        }
        else
        {
            budget.PlannedRevenue = dto.PlannedRevenue;
            budget.PlannedCosts = dto.PlannedCosts;
        }

        await audit.RecordAsync("ProjectBudget", projectId, AuditAction.Update, actorId, new
        {
            from = old,
            to = new { planned_revenue = Str(dto.PlannedRevenue), planned_costs = dto.PlannedCosts },
        }, ct);
        return ToBudgetOut(budget);
    }

    // ---------- Фактические затраты ----------
    public async Task<List<ActualCostOutDto>> ListActualsAsync(Guid projectId, CancellationToken ct)
        => (await db.ActualCosts.Where(a => a.ProjectId == projectId)
                .OrderByDescending(a => a.OccurredOn).ToListAsync(ct))
            .Select(ToActualOut).ToList();

    public async Task<ActualCostOutDto> AddActualAsync(Guid projectId, ActualCostCreateDto dto, Guid actorId, CancellationToken ct)
    {
        await ProjectAsync(projectId, ct);
        if (dto.Category == CostCategory.Payroll)
            throw new DomainValidationException("Категория «ФОТ» считается автоматически из утверждённых таймшитов");

        if (!string.IsNullOrEmpty(dto.ExternalId)
            && await db.ActualCosts.AnyAsync(a => a.ProjectId == projectId && a.ExternalId == dto.ExternalId, ct))
            throw new ConflictException("Затрата с таким external_id уже загружена в этот проект");

        var actual = new ActualCost
        {
            ProjectId = projectId, Category = dto.Category, Amount = dto.Amount, Source = CostSource.Manual,
            OccurredOn = dto.OccurredOn, ExternalId = dto.ExternalId, Description = dto.Description,
        };
        db.ActualCosts.Add(actual);
        await audit.RecordAsync("ActualCost", projectId, AuditAction.Create, actorId,
            new { category = dto.Category, amount = Str(dto.Amount) }, ct);
        await SnapshotForecastAsync(projectId, ct);   // recalc_margin: снимок прогноза (Redis опущен)
        return ToActualOut(actual);
    }

    // ---------- Маржинальность / прогноз ----------
    private async Task<Dictionary<string, decimal>> CostBreakdownAsync(Guid projectId, CancellationToken ct)
    {
        var actuals = await db.ActualCosts.Where(a => a.ProjectId == projectId).ToListAsync(ct);
        var breakdown = actuals
            .GroupBy(a => a.Category.ToString().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.Sum(a => a.Amount));
        // ФОТ — только из approved-таймшитов (перекрываем, чтобы не задвоить).
        breakdown["payroll"] = await ApprovedCostAsync(projectId, ct);
        return breakdown;
    }

    private async Task<decimal> ApprovedCostAsync(Guid projectId, CancellationToken ct)
    {
        var rows = await db.TimeEntries
            .Where(e => e.ProjectId == projectId && e.Status == TimeEntryStatus.Approved)
            .Select(e => new { e.Hours, e.CostRateSnapshot })
            .ToListAsync(ct);
        return rows.Sum(r => r.Hours * (r.CostRateSnapshot ?? 0m));
    }

    private async Task<decimal> ActualHoursAsync(Guid projectId, CancellationToken ct)
        => (await db.TimeEntries
                .Where(e => e.ProjectId == projectId && e.Status == TimeEntryStatus.Approved)
                .Select(e => e.Hours).ToListAsync(ct)).Sum();

    private async Task<decimal> PlannedHoursAsync(Guid projectId, CancellationToken ct)
        => (await db.ProjectTasks.Where(t => t.ProjectId == projectId).Select(t => t.PlannedHours).ToListAsync(ct)).Sum();

    public async Task<(FinanceCalc.MarginResult Result, Dictionary<string, string> Breakdown)> ComputeMarginAsync(
        Guid projectId, CancellationToken ct)
    {
        var project = await ProjectAsync(projectId, ct);
        var budget = await GetBudgetAsync(projectId, ct);
        var revenue = budget?.PlannedRevenue ?? project.BudgetRevenue;
        var breakdown = await CostBreakdownAsync(projectId, ct);
        var total = FinanceCalc.TotalCost(breakdown.Values);
        var result = FinanceCalc.ComputeMargin(revenue, total);
        return (result, breakdown.ToDictionary(k => k.Key, v => Str(v.Value)));
    }

    private async Task SnapshotForecastAsync(Guid projectId, CancellationToken ct)
    {
        var budget = await GetBudgetAsync(projectId, ct);
        var plannedTotal = budget is null ? 0m : FinanceCalc.TotalCost(budget.PlannedCosts.Values.Select(ParseDec));
        var breakdown = await CostBreakdownAsync(projectId, ct);
        var actualToDate = FinanceCalc.TotalCost(breakdown.Values);
        var f = FinanceCalc.ComputeForecast(actualToDate, plannedTotal - actualToDate);
        db.Forecasts.Add(new Forecast { ProjectId = projectId, Eac = f.Eac, Etc = f.Etc, CalculatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(ct);
    }

    public async Task<ProjectFinanceDto> ProjectFinanceAsync(Guid projectId, CancellationToken ct)
    {
        var project = await ProjectAsync(projectId, ct);
        var budget = await GetBudgetAsync(projectId, ct);
        var (result, breakdown) = await ComputeMarginAsync(projectId, ct);
        var plannedHours = await PlannedHoursAsync(projectId, ct);
        var actualHours = await ActualHoursAsync(projectId, ct);
        var plannedTotal = budget is null ? 0m : FinanceCalc.TotalCost(budget.PlannedCosts.Values.Select(ParseDec));
        var forecast = FinanceCalc.ComputeForecast(result.TotalCost, plannedTotal - result.TotalCost);

        return new ProjectFinanceDto(
            project.Id,
            budget is null ? null : ToBudgetOut(budget),
            new MarginOutDto(result.Revenue, result.TotalCost, result.Margin, result.MarginPct, breakdown),
            new ForecastOutDto(forecast.Eac, forecast.Etc, DateTimeOffset.UtcNow),
            plannedHours, actualHours,
            FinanceCalc.HoursOverrunPct(plannedHours, actualHours));
    }

    // ---------- Мапперы ----------
    public static BudgetOutDto ToBudgetOut(ProjectBudget b)
        => new(b.Id, b.ProjectId, b.PlannedRevenue, new Dictionary<string, string>(b.PlannedCosts));

    private static ActualCostOutDto ToActualOut(ActualCost a)
        => new(a.Id, a.ProjectId, a.Category, a.Amount, a.Source, a.OccurredOn, a.ExternalId, a.Description);
}
