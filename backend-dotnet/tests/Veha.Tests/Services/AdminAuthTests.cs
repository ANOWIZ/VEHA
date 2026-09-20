using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Veha.Api.Auth;
using Veha.Api.Services;
using Veha.Domain.Entities;
using Veha.Domain.Enums;
using Veha.Tests.Support;

namespace Veha.Tests.Services;

/// <summary>Тесты admin (аудит-лог, 1С-выгрузка) и dev-токена.</summary>
public class AdminAuthTests
{
    [Fact]
    public async Task Audit_list_enriches_actor_name_and_paginates()
    {
        using var db = new SqliteTestDb();
        Guid actorId;
        await using (var ctx = db.NewContext())
        {
            var actor = new User { Username = "pm", Email = "pm@x.test", FullName = "Пётр Менеджеров", IsActive = true, Roles = ["pm"] };
            ctx.Users.Add(actor);
            await ctx.SaveChangesAsync();
            actorId = actor.Id;
            await new AuditService(ctx).RecordAsync("Project", Guid.NewGuid(), AuditAction.Create, actorId, new { version = 1 }, default);
        }

        await using (var ctx = db.NewContext())
        {
            var page = await new AuditService(ctx).ListAsync(null, null, 100, 0, default);
            Assert.Equal(1, page.Total);
            var entry = Assert.Single(page.Items);
            Assert.Equal("Project", entry.Entity);
            Assert.Equal(AuditAction.Create, entry.Action);
            Assert.Equal("Пётр Менеджеров", entry.ActorName);
            Assert.NotNull(entry.Diff);
        }
    }

    [Fact]
    public async Task OneC_export_is_idempotent()
    {
        using var db = new SqliteTestDb();
        await using (var ctx = db.NewContext())
        {
            var u = new User { Username = "eng", Email = "e@x.test", FullName = "Eng", IsActive = true };
            var p = new Project { Code = "PRJ-2009-001", Name = "P", ClientId = Guid.NewGuid(), Type = ProjectType.Implementation, ManagerId = Guid.NewGuid(), Stage = Stage.Implementation, Status = ProjectStatus.Active };
            ctx.Users.Add(u);
            ctx.Projects.Add(p);
            ctx.TimeEntries.Add(new TimeEntry { UserId = u.Id, ProjectId = p.Id, WorkDate = new DateOnly(2026, 1, 15), Hours = 8m, Comment = "c", Status = TimeEntryStatus.Approved, CostRateSnapshot = 1000m });
            await ctx.SaveChangesAsync();
        }

        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 31);
        await using (var ctx = db.NewContext())
            await new OneCService(ctx).ExportTimesheetsAsync(from, to, default);
        await using (var ctx = db.NewContext())
            await new OneCService(ctx).ExportTimesheetsAsync(from, to, default);   // повторно

        await using (var ctx = db.NewContext())
            Assert.Equal(1, await ctx.IntegrationLogs.CountAsync(l => l.System == "onec" && l.Status == "ok"));
    }

    [Fact]
    public void Dev_token_is_issued_and_recognized()
    {
        var token = DevToken.Issue("pm.demo", ["pm"], null, null, "test-secret-32-bytes-minimum-length!!");
        Assert.Equal("psa-dev", new JwtSecurityTokenHandler().ReadJwtToken(token).Issuer);

        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Authorization = $"Bearer {token}";
        Assert.True(DevToken.IsDevBearer(ctx.Request));

        var plain = new DefaultHttpContext();
        Assert.False(DevToken.IsDevBearer(plain.Request));
    }
}
