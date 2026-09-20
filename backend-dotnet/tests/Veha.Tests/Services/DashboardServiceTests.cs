using Veha.Api.Services;
using Veha.Domain.Entities;
using Veha.Domain.Enums;
using Veha.Infrastructure;
using Veha.Tests.Support;

namespace Veha.Tests.Services;

/// <summary>Тесты DashboardService: портфель проектов (маржа, риски low_margin/
/// hours_overrun, агрегаты).</summary>
public class DashboardServiceTests
{
    private static DashboardService Svc(VehaDbContext ctx) => new(ctx, new ProjectAccessService(ctx));

    [Fact]
    public async Task Portfolio_flags_low_margin_and_hours_overrun()
    {
        using var db = new SqliteTestDb();
        Guid adminId, pid;
        await using (var ctx = db.NewContext())
        {
            var admin = new User { Username = "admin", Email = "a@x.test", FullName = "Admin", IsActive = true, Roles = ["admin"] };
            ctx.Users.Add(admin);
            var p = new Project
            {
                Code = "PRJ-2006-001", Name = "P", ClientId = Guid.NewGuid(), Type = ProjectType.Implementation,
                ManagerId = admin.Id, Stage = Stage.Implementation, Status = ProjectStatus.Active, BudgetRevenue = 100_000m,
            };
            ctx.Projects.Add(p);
            // approved: 10 ч × 1000 = 10000 ФОТ; план 5 ч → перерасход (10/5*100=200% > 110%)
            ctx.TimeEntries.Add(new TimeEntry
            {
                UserId = admin.Id, ProjectId = p.Id, WorkDate = new DateOnly(2026, 1, 5), Hours = 10m,
                Comment = "c", Status = TimeEntryStatus.Approved, CostRateSnapshot = 1000m,
            });
            ctx.ProjectTasks.Add(new ProjectTask { ProjectId = p.Id, Name = "t", PlannedHours = 5m });
            ctx.ActualCosts.Add(new ActualCost
            {
                ProjectId = p.Id, Category = CostCategory.Licenses, Amount = 100_000m,
                Source = CostSource.Manual, OccurredOn = new DateOnly(2026, 1, 5),
            });
            await ctx.SaveChangesAsync();
            adminId = admin.Id; pid = p.Id;
        }

        await using (var ctx = db.NewContext())
        {
            var user = await ctx.Users.FindAsync(adminId);
            var pf = await Svc(ctx).PortfolioAsync(user!, default);

            Assert.Equal(1, pf.ProjectsCount);
            Assert.Equal(1, pf.AtRisk);
            var row = Assert.Single(pf.Rows);
            // revenue 100000, cost 110000 → margin -10000, margin_pct -10 (<15) → low_margin
            Assert.Equal(-10_000m, row.Margin);
            Assert.Contains("low_margin", row.Risks);
            Assert.Contains("hours_overrun", row.Risks);
        }
    }
}
