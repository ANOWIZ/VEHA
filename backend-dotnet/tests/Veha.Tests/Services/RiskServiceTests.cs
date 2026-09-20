using Microsoft.EntityFrameworkCore;
using Veha.Api.Dtos;
using Veha.Api.Services;
using Veha.Domain.Entities;
using Veha.Domain.Enums;
using Veha.Infrastructure;
using Veha.Tests.Support;

namespace Veha.Tests.Services;

/// <summary>Интеграционные тесты RiskService: CRUD (score/level/аудит/closed_at,
/// уведомление владельца) и портфель (матрица 3×3, активные статусы).</summary>
public class RiskServiceTests
{
    private static RiskService Svc(VehaDbContext ctx)
        => new(ctx, new AuditService(ctx), new NotificationService(ctx), new ProjectAccessService(ctx));

    private static async Task<Guid> SeedUser(SqliteTestDb db, string name, params string[] roles)
    {
        await using var ctx = db.NewContext();
        var u = new User { Username = name, Email = $"{name}@x.test", FullName = name, IsActive = true, Roles = roles.ToList() };
        ctx.Users.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    private static async Task<Guid> SeedProject(SqliteTestDb db, Guid managerId)
    {
        await using var ctx = db.NewContext();
        var p = new Project
        {
            Code = $"PRJ-2005-{Guid.NewGuid().ToString()[..3]}", Name = "P", ClientId = Guid.NewGuid(),
            Type = ProjectType.Implementation, ManagerId = managerId, Stage = Stage.Presale, Status = ProjectStatus.Active,
        };
        ctx.Projects.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    private static async Task SeedRisk(SqliteTestDb db, Guid projectId, int prob, int impact, RiskStatus status, RiskCategory cat)
    {
        await using var ctx = db.NewContext();
        ctx.Risks.Add(new Risk
        {
            ProjectId = projectId, Title = "R", Probability = prob, Impact = impact, Score = prob * impact,
            Status = status, Category = cat,
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Create_computes_score_and_audits()
    {
        using var db = new SqliteTestDb();
        var actor = await SeedUser(db, "pm", "pm");
        var pid = await SeedProject(db, actor);

        await using var ctx = db.NewContext();
        var risk = await Svc(ctx).CreateAsync(pid, new RiskCreateDto { Title = "Срыв сроков", Probability = 3, Impact = 3 }, actor, default);
        Assert.Equal(9, risk.Score);
        Assert.Equal("high", risk.Level);
        Assert.True(ctx.AuditLogs.Any(a => a.Entity == "Risk" && a.Action == AuditAction.Create));
    }

    [Fact]
    public async Task Update_recomputes_score_and_sets_closed_at()
    {
        using var db = new SqliteTestDb();
        var actor = await SeedUser(db, "pm", "pm");
        var pid = await SeedProject(db, actor);

        Guid riskId;
        await using (var ctx = db.NewContext())
            riskId = (await Svc(ctx).CreateAsync(pid, new RiskCreateDto { Title = "R", Probability = 1, Impact = 1 }, actor, default)).Id;

        await using (var ctx = db.NewContext())
        {
            var updated = await Svc(ctx).UpdateAsync(riskId, new RiskUpdateDto { Probability = 3, Impact = 3 }, actor, default);
            Assert.Equal(9, updated.Score);
        }
        await using (var ctx = db.NewContext())
        {
            var closed = await Svc(ctx).UpdateAsync(riskId, new RiskUpdateDto { Status = RiskStatus.Closed }, actor, default);
            Assert.NotNull(closed.ClosedAt);
        }
    }

    [Fact]
    public async Task Create_notifies_internal_owner()
    {
        using var db = new SqliteTestDb();
        var actor = await SeedUser(db, "pm", "pm");
        var owner = await SeedUser(db, "eng", "engineer");
        var pid = await SeedProject(db, actor);

        await using (var ctx = db.NewContext())
            await Svc(ctx).CreateAsync(pid, new RiskCreateDto { Title = "R", Probability = 2, Impact = 2, OwnerId = owner }, actor, default);

        await using (var ctx = db.NewContext())
            Assert.True(ctx.Notifications.Any(n => n.UserId == owner && n.Kind == "risk_assigned"));
    }

    [Fact]
    public async Task List_ordered_by_score_desc()
    {
        using var db = new SqliteTestDb();
        var pid = await SeedProject(db, Guid.NewGuid());
        await SeedRisk(db, pid, 1, 1, RiskStatus.Open, RiskCategory.Technical); // score 1
        await SeedRisk(db, pid, 3, 3, RiskStatus.Open, RiskCategory.Budget);    // score 9

        await using var ctx = db.NewContext();
        var list = await Svc(ctx).ListForProjectAsync(pid, null, default);
        Assert.Equal(9, list[0].Score);
        Assert.Equal(1, list[1].Score);
    }

    [Fact]
    public async Task Portfolio_aggregates_active_risks_matrix_and_high()
    {
        using var db = new SqliteTestDb();
        var admin = await SeedUser(db, "admin", "admin");
        var pid = await SeedProject(db, admin);
        await SeedRisk(db, pid, 3, 3, RiskStatus.Open, RiskCategory.Technical);      // score 9, high, active
        await SeedRisk(db, pid, 1, 2, RiskStatus.Mitigating, RiskCategory.Budget);   // score 2, active
        await SeedRisk(db, pid, 2, 3, RiskStatus.Closed, RiskCategory.Schedule);     // score 6, NOT active

        await using var ctx = db.NewContext();
        var user = await ctx.Users.FindAsync(admin);
        var pf = await Svc(ctx).PortfolioAsync(user!, default);

        Assert.Equal(2, pf.TotalActive);
        Assert.Equal(1, pf.TotalHigh);
        Assert.Equal(1, pf.ProjectsCount);
        Assert.Equal(1, pf.Matrix.First(c => c is { Probability: 3, Impact: 3 }).Count);
        var row = Assert.Single(pf.Rows);
        Assert.Equal(2, row.ActiveCount);
        Assert.Equal(1, row.HighCount);
        Assert.Equal(9, row.TopScore);
    }
}
