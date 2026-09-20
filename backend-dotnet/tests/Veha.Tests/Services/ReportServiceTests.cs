using Microsoft.Extensions.Options;
using Veha.Api.Config;
using Veha.Api.Services;
using Veha.Domain.Entities;
using Veha.Domain.Enums;
using Veha.Infrastructure;
using Veha.Tests.Support;

namespace Veha.Tests.Services;

/// <summary>Тесты ReportService: загрузка ресурсов (utilization), строки выгрузки,
/// генерация XLSX (smoke).</summary>
public class ReportServiceTests
{
    private static ReportService Svc(VehaDbContext ctx)
        => new(ctx, new ProjectAccessService(ctx), Options.Create(new VehaSettings { DefaultWeekNormHours = 40m }));

    private static readonly DateOnly Mon = new(2026, 3, 2);   // Пн
    private static readonly DateOnly Fri = new(2026, 3, 6);   // Пт (5 рабочих дней)

    private static async Task<Guid> SeedUser(SqliteTestDb db, string name, params string[] roles)
    {
        await using var ctx = db.NewContext();
        var u = new User { Username = name, Email = $"{name}@x.test", FullName = name, IsActive = true, Roles = roles.ToList() };
        ctx.Users.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    private static async Task SeedApproved(SqliteTestDb db, Guid userId, Guid projectId, decimal hours)
    {
        await using var ctx = db.NewContext();
        ctx.TimeEntries.Add(new TimeEntry
        {
            UserId = userId, ProjectId = projectId, WorkDate = Mon, Hours = hours,
            Comment = "c", Status = TimeEntryStatus.Approved, CostRateSnapshot = 1000m,
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Utilization_computes_capacity_and_sorts_by_load()
    {
        using var db = new SqliteTestDb();
        var alice = await SeedUser(db, "Alice", "engineer");
        var bob = await SeedUser(db, "Bob", "engineer");
        await SeedUser(db, "Client", "client");   // исключается
        var pid = Guid.NewGuid();
        await SeedApproved(db, alice, pid, 40m);
        await SeedApproved(db, bob, pid, 20m);

        await using var ctx = db.NewContext();
        var rep = await Svc(ctx).UtilizationAsync(Mon, Fri, default);

        Assert.Equal(5, rep.WorkingDays);
        Assert.Equal(40m, rep.CapacityPerUser);       // 8 ч × 5 дней
        Assert.Equal(2, rep.UsersCount);              // client исключён
        Assert.Equal(60m, rep.TotalBillableHours);
        Assert.Equal(75m, rep.AvgUtilizationPct);     // 60/80*100
        Assert.Equal("Alice", rep.Rows[0].FullName);  // 100% сверху
        Assert.Equal(100m, rep.Rows[0].UtilizationPct);
    }

    [Fact]
    public async Task Timesheet_rows_include_cost_for_financial_roles()
    {
        using var db = new SqliteTestDb();
        var pmId = await SeedUser(db, "Pm", "pm");
        await using var ctx = db.NewContext();
        // проект, где pm — руководитель (доступен)
        var project = new Project
        {
            Code = "PRJ-2007-001", Name = "P", ClientId = Guid.NewGuid(), Type = ProjectType.Implementation,
            ManagerId = pmId, Stage = Stage.Presale, Status = ProjectStatus.Active,
        };
        ctx.Projects.Add(project);
        ctx.TimeEntries.Add(new TimeEntry
        {
            UserId = pmId, ProjectId = project.Id, WorkDate = Mon, Hours = 8m,
            Comment = "работа", Status = TimeEntryStatus.Approved, CostRateSnapshot = 1000m,
        });
        await ctx.SaveChangesAsync();

        var pm = await ctx.Users.FindAsync(pmId);
        var rows = await Svc(ctx).TimesheetRowsAsync(pm!, Mon, Fri, null, null, includeCost: true, default);
        var r = Assert.Single(rows);
        Assert.Equal(8m, r.Hours);
        Assert.Equal(8000m, r.Cost);   // 8 ч × 1000

        // XLSX smoke — файл не пустой.
        var bytes = ReportExport.Timesheets(rows, includeCost: true, Mon, Fri);
        Assert.True(bytes.Length > 0);
    }
}
