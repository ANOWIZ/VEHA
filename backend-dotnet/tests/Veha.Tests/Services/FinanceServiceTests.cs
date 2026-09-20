using Veha.Api.Dtos;
using Veha.Api.Services;
using Veha.Domain.Common;
using Veha.Domain.Entities;
using Veha.Domain.Enums;
using Veha.Infrastructure;
using Veha.Tests.Support;

namespace Veha.Tests.Services;

/// <summary>Интеграционные тесты FinanceService: бюджет (guard закрытого), факт
/// (ФОТ-запрет, дедуп external_id), маржа (ФОТ из таймшитов), сводка.</summary>
public class FinanceServiceTests
{
    private static FinanceService Svc(VehaDbContext ctx) => new(ctx, new AuditService(ctx));

    private static async Task<Guid> SeedProject(SqliteTestDb db, decimal budgetRevenue = 0m, ProjectStatus status = ProjectStatus.Active)
    {
        await using var ctx = db.NewContext();
        var p = new Project
        {
            Code = $"PRJ-2004-{Guid.NewGuid().ToString()[..3]}", Name = "P", ClientId = Guid.NewGuid(),
            Type = ProjectType.Implementation, ManagerId = Guid.NewGuid(), Stage = Stage.Presale,
            Status = status, BudgetRevenue = budgetRevenue,
        };
        ctx.Projects.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task Upsert_budget_creates_then_updates()
    {
        using var db = new SqliteTestDb();
        var pid = await SeedProject(db);

        await using (var ctx = db.NewContext())
        {
            var b = await Svc(ctx).UpsertBudgetAsync(pid, new BudgetUpsertDto
            {
                PlannedRevenue = 1_000_000m,
                PlannedCosts = new() { ["licenses"] = "200000" },
            }, Guid.NewGuid(), default);
            Assert.Equal(1_000_000m, b.PlannedRevenue);
        }
        await using (var ctx = db.NewContext())
        {
            var b = await Svc(ctx).UpsertBudgetAsync(pid, new BudgetUpsertDto { PlannedRevenue = 1_200_000m }, Guid.NewGuid(), default);
            Assert.Equal(1_200_000m, b.PlannedRevenue);
        }
    }

    [Fact]
    public async Task Upsert_budget_on_closed_project_throws()
    {
        using var db = new SqliteTestDb();
        var pid = await SeedProject(db, status: ProjectStatus.Closed);
        await using var ctx = db.NewContext();
        await Assert.ThrowsAsync<ProjectClosedException>(
            () => Svc(ctx).UpsertBudgetAsync(pid, new BudgetUpsertDto { PlannedRevenue = 1m }, Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Add_actual_rejects_payroll_and_dedups_external()
    {
        using var db = new SqliteTestDb();
        var pid = await SeedProject(db);
        var actor = Guid.NewGuid();

        await using (var ctx = db.NewContext())
            await Assert.ThrowsAsync<DomainValidationException>(() => Svc(ctx).AddActualAsync(pid,
                new ActualCostCreateDto { Category = CostCategory.Payroll, Amount = 100m, OccurredOn = new DateOnly(2026, 1, 1) }, actor, default));

        await using (var ctx = db.NewContext())
        {
            var a = await Svc(ctx).AddActualAsync(pid, new ActualCostCreateDto
            {
                Category = CostCategory.Licenses, Amount = 5000m, OccurredOn = new DateOnly(2026, 1, 1), ExternalId = "1C-1",
            }, actor, default);
            Assert.Equal(CostSource.Manual, a.Source);   // источник форсирован
        }

        await using (var ctx = db.NewContext())
            await Assert.ThrowsAsync<ConflictException>(() => Svc(ctx).AddActualAsync(pid, new ActualCostCreateDto
            {
                Category = CostCategory.Licenses, Amount = 5000m, OccurredOn = new DateOnly(2026, 1, 1), ExternalId = "1C-1",
            }, actor, default));
    }

    [Fact]
    public async Task Margin_takes_payroll_from_timesheets_and_revenue_from_budget()
    {
        using var db = new SqliteTestDb();
        var pid = await SeedProject(db, budgetRevenue: 0m);

        await using (var ctx = db.NewContext())
        {
            ctx.ProjectBudgets.Add(new ProjectBudget { ProjectId = pid, PlannedRevenue = 1_000_000m });
            ctx.TimeEntries.Add(new TimeEntry
            {
                UserId = Guid.NewGuid(), ProjectId = pid, WorkDate = new DateOnly(2026, 1, 5), Hours = 10m,
                Comment = "c", Status = TimeEntryStatus.Approved, CostRateSnapshot = 1000m,
            });
            ctx.ActualCosts.Add(new ActualCost
            {
                ProjectId = pid, Category = CostCategory.Licenses, Amount = 200_000m,
                Source = CostSource.Manual, OccurredOn = new DateOnly(2026, 1, 5),
            });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = db.NewContext())
        {
            var (result, breakdown) = await Svc(ctx).ComputeMarginAsync(pid, default);
            Assert.Equal(1_000_000m, result.Revenue);
            Assert.Equal(210_000m, result.TotalCost);   // 10000 ФОТ + 200000 лицензии
            Assert.Equal(790_000m, result.Margin);
            Assert.Equal("10000", breakdown["payroll"]);
            Assert.Equal("200000", breakdown["licenses"]);
        }
    }

    [Fact]
    public async Task Project_finance_summary_computes_hours_overrun()
    {
        using var db = new SqliteTestDb();
        var pid = await SeedProject(db, budgetRevenue: 500_000m);

        await using (var ctx = db.NewContext())
        {
            ctx.ProjectTasks.Add(new ProjectTask { ProjectId = pid, Name = "task", PlannedHours = 100m });
            ctx.TimeEntries.Add(new TimeEntry
            {
                UserId = Guid.NewGuid(), ProjectId = pid, WorkDate = new DateOnly(2026, 1, 5), Hours = 110m,
                Comment = "c", Status = TimeEntryStatus.Approved, CostRateSnapshot = 0m,
            });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = db.NewContext())
        {
            var fin = await Svc(ctx).ProjectFinanceAsync(pid, default);
            Assert.Equal(100m, fin.PlannedHours);
            Assert.Equal(110m, fin.ActualHours);
            Assert.Equal(10m, fin.HoursOverrunPct);
            Assert.Equal(500_000m, fin.Margin.Revenue);   // выручка из project.budget_revenue (бюджета нет)
        }
    }
}
