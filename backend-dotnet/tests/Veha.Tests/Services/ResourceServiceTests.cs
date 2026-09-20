using Microsoft.Extensions.Options;
using Veha.Api.Config;
using Veha.Api.Dtos;
using Veha.Api.Services;
using Veha.Domain.Entities;
using Veha.Infrastructure;
using Veha.Tests.Support;

namespace Veha.Tests.Services;

/// <summary>Тесты ResourceService: upsert ячейки плана и тепловая карта (util/load).</summary>
public class ResourceServiceTests
{
    private static ResourceService Svc(VehaDbContext ctx)
        => new(ctx, Options.Create(new VehaSettings { DefaultWeekNormHours = 40m }));

    private static readonly DateOnly Mon = new(2026, 3, 2);

    private static async Task<Guid> SeedUser(SqliteTestDb db, string name)
    {
        await using var ctx = db.NewContext();
        var u = new User { Username = name, Email = $"{name}@x.test", FullName = name, IsActive = true };
        ctx.Users.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    [Fact]
    public async Task Upsert_plan_creates_then_updates_same_cell()
    {
        using var db = new SqliteTestDb();
        var uid = Guid.NewGuid();
        var pid = Guid.NewGuid();

        Guid cellId;
        await using (var ctx = db.NewContext())
        {
            var cell = await Svc(ctx).UpsertPlanAsync(new ResourcePlanUpsertDto
            {
                UserId = uid, ProjectId = pid, WeekStart = Mon.AddDays(2), PlannedHours = 20m,
            }, default);
            cellId = cell.Id;
            Assert.Equal(Mon, cell.WeekStart);   // нормализовано к понедельнику
        }
        await using (var ctx = db.NewContext())
        {
            var cell = await Svc(ctx).UpsertPlanAsync(new ResourcePlanUpsertDto
            {
                UserId = uid, ProjectId = pid, WeekStart = Mon, PlannedHours = 32m,
            }, default);
            Assert.Equal(cellId, cell.Id);       // та же ячейка
            Assert.Equal(32m, cell.PlannedHours);
        }
    }

    [Fact]
    public async Task Heatmap_aggregates_and_classifies_load()
    {
        using var db = new SqliteTestDb();
        var uid = await SeedUser(db, "Engineer");

        await using (var ctx = db.NewContext())
        {
            // 24 + 20 = 44 ч в неделю по двум проектам → 110% → over.
            ctx.ResourcePlans.Add(new ResourcePlan { UserId = uid, ProjectId = Guid.NewGuid(), WeekStart = Mon, PlannedHours = 24m });
            ctx.ResourcePlans.Add(new ResourcePlan { UserId = uid, ProjectId = Guid.NewGuid(), WeekStart = Mon, PlannedHours = 20m });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = db.NewContext())
        {
            var hm = await Svc(ctx).HeatmapAsync(Mon, 1, default);
            Assert.Equal(40m, hm.NormHours);
            var row = Assert.Single(hm.Rows);
            Assert.Equal("Engineer", row.UserName);
            Assert.Equal(44m, row.TotalHours);
            var cell = Assert.Single(row.Cells);
            Assert.Equal(110m, cell.UtilizationPct);
            Assert.Equal("over", cell.Load);
        }
    }
}
