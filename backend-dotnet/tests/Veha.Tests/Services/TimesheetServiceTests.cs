using Microsoft.EntityFrameworkCore;
using Veha.Api.Dtos;
using Veha.Api.Services;
using Veha.Domain.Common;
using Veha.Domain.Entities;
using Veha.Domain.Enums;
using Veha.Infrastructure;
using Veha.Tests.Support;

namespace Veha.Tests.Services;

/// <summary>Интеграционные тесты TimesheetService: ввод (членство/шаг/лимит), недельная
/// сетка, отправка, копирование, утверждение (снимок ставки), отклонение, сторно.</summary>
public class TimesheetServiceTests
{
    private static TimesheetService Svc(VehaDbContext ctx)
        => new(ctx, new AuditService(ctx), new NotificationService(ctx));

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
            Code = $"PRJ-2003-{Guid.NewGuid().ToString()[..3]}", Name = "P", ClientId = Guid.NewGuid(),
            Type = ProjectType.Implementation, ManagerId = managerId, Stage = Stage.Presale, Status = ProjectStatus.Active,
        };
        ctx.Projects.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    private static async Task AddMember(SqliteTestDb db, Guid projectId, Guid userId)
    {
        await using var ctx = db.NewContext();
        ctx.ProjectMembers.Add(new ProjectMember { ProjectId = projectId, UserId = userId, Role = ProjectMemberRole.Engineer });
        await ctx.SaveChangesAsync();
    }

    private static async Task<Guid> SeedEntry(
        SqliteTestDb db, Guid userId, Guid projectId, TimeEntryStatus status, DateOnly date, decimal hours = 8m)
    {
        await using var ctx = db.NewContext();
        var e = new TimeEntry
        {
            UserId = userId, ProjectId = projectId, WorkDate = date, Hours = hours,
            Comment = "работа", Status = status,
        };
        ctx.TimeEntries.Add(e);
        await ctx.SaveChangesAsync();
        return e.Id;
    }

    private static async Task<User> Usr(VehaDbContext ctx, Guid id) => (await ctx.Users.FindAsync(id))!;

    private static readonly DateOnly Mon = new(2026, 3, 2); // понедельник

    [Fact]
    public async Task Create_requires_project_membership()
    {
        using var db = new SqliteTestDb();
        var pm = await SeedUser(db, "pm", "pm");
        var eng = await SeedUser(db, "eng", "engineer");
        var pid = await SeedProject(db, pm);

        await using (var ctx = db.NewContext())
        {
            var user = await Usr(ctx, eng);
            var dto = new TimeEntryCreateDto { ProjectId = pid, WorkDate = Mon, Hours = 8m, Comment = "c" };
            await Assert.ThrowsAsync<ForbiddenException>(() => Svc(ctx).CreateEntryAsync(user, dto, default));
        }

        await AddMember(db, pid, eng);
        await using (var ctx = db.NewContext())
        {
            var user = await Usr(ctx, eng);
            var dto = new TimeEntryCreateDto { ProjectId = pid, WorkDate = Mon, Hours = 8m, Comment = "c" };
            var created = await Svc(ctx).CreateEntryAsync(user, dto, default);
            Assert.Equal(TimeEntryStatus.Draft, created.Status);
        }
    }

    [Fact]
    public async Task Create_rejects_bad_step_and_daily_limit()
    {
        using var db = new SqliteTestDb();
        var pm = await SeedUser(db, "pm", "pm");
        var eng = await SeedUser(db, "eng", "engineer");
        var pid = await SeedProject(db, pm);
        await AddMember(db, pid, eng);

        await using (var ctx = db.NewContext())
        {
            var user = await Usr(ctx, eng);
            await Assert.ThrowsAsync<TimesheetInvalidHoursException>(() => Svc(ctx).CreateEntryAsync(
                user, new TimeEntryCreateDto { ProjectId = pid, WorkDate = Mon, Hours = 1.1m, Comment = "c" }, default));
        }

        await using (var ctx = db.NewContext())
        {
            var user = await Usr(ctx, eng);
            await Svc(ctx).CreateEntryAsync(user, new TimeEntryCreateDto { ProjectId = pid, WorkDate = Mon, Hours = 20m, Comment = "c" }, default);
        }
        await using (var ctx = db.NewContext())
        {
            var user = await Usr(ctx, eng);
            await Assert.ThrowsAsync<TimesheetDailyLimitException>(() => Svc(ctx).CreateEntryAsync(
                user, new TimeEntryCreateDto { ProjectId = pid, WorkDate = Mon, Hours = 8m, Comment = "c" }, default));
        }
    }

    [Fact]
    public async Task Week_grid_aggregates_totals()
    {
        using var db = new SqliteTestDb();
        var eng = await SeedUser(db, "eng", "engineer");
        var pid = await SeedProject(db, eng);
        await SeedEntry(db, eng, pid, TimeEntryStatus.Draft, Mon, 4m);
        await SeedEntry(db, eng, pid, TimeEntryStatus.Draft, Mon.AddDays(1), 6m);

        await using var ctx = db.NewContext();
        var week = await Svc(ctx).GetWeekAsync(await Usr(ctx, eng), Mon.AddDays(3), default);
        Assert.Equal(Mon, week.WeekStart);
        Assert.Equal(10m, week.TotalHours);
        Assert.Equal(4m, week.DailyTotals["2026-03-02"]);
    }

    [Fact]
    public async Task Update_non_owner_forbidden_and_locked_when_submitted()
    {
        using var db = new SqliteTestDb();
        var eng = await SeedUser(db, "eng", "engineer");
        var other = await SeedUser(db, "other", "engineer");
        var pid = await SeedProject(db, eng);
        var draft = await SeedEntry(db, eng, pid, TimeEntryStatus.Draft, Mon);
        var submitted = await SeedEntry(db, eng, pid, TimeEntryStatus.Submitted, Mon.AddDays(1));

        await using var ctx = db.NewContext();
        var svc = Svc(ctx);
        var otherUser = await Usr(ctx, other);
        var engUser = await Usr(ctx, eng);
        await Assert.ThrowsAsync<ForbiddenException>(() => svc.UpdateEntryAsync(
            otherUser, draft, new TimeEntryUpdateDto { Comment = "hack" }, default));
        await Assert.ThrowsAsync<TimesheetLockedException>(() => svc.UpdateEntryAsync(
            engUser, submitted, new TimeEntryUpdateDto { Comment = "x" }, default));
    }

    [Fact]
    public async Task Submit_week_moves_drafts_and_sets_week_status()
    {
        using var db = new SqliteTestDb();
        var eng = await SeedUser(db, "eng", "engineer");
        var pid = await SeedProject(db, eng);
        await SeedEntry(db, eng, pid, TimeEntryStatus.Draft, Mon, 4m);
        await SeedEntry(db, eng, pid, TimeEntryStatus.Draft, Mon.AddDays(1), 4m);

        await using (var ctx = db.NewContext())
        {
            var n = await Svc(ctx).SubmitWeekAsync(await Usr(ctx, eng), Mon, default);
            Assert.Equal(2, n);
        }
        await using (var ctx = db.NewContext())
        {
            Assert.True(ctx.TimeEntries.All(e => e.Status == TimeEntryStatus.Submitted));
            Assert.Equal(TimeEntryStatus.Submitted,
                (await ctx.TimesheetWeeks.FirstAsync(w => w.UserId == eng && w.WeekStart == Mon)).Status);
        }
    }

    [Fact]
    public async Task Submit_week_without_drafts_fails()
    {
        using var db = new SqliteTestDb();
        var eng = await SeedUser(db, "eng", "engineer");
        await using var ctx = db.NewContext();
        var engUser = await Usr(ctx, eng);
        await Assert.ThrowsAsync<DomainValidationException>(
            () => Svc(ctx).SubmitWeekAsync(engUser, Mon, default));
    }

    [Fact]
    public async Task Copy_week_duplicates_entries_to_target()
    {
        using var db = new SqliteTestDb();
        var eng = await SeedUser(db, "eng", "engineer");
        var pid = await SeedProject(db, eng);
        await SeedEntry(db, eng, pid, TimeEntryStatus.Draft, Mon, 4m);
        await SeedEntry(db, eng, pid, TimeEntryStatus.Draft, Mon.AddDays(2), 5m);

        await using (var ctx = db.NewContext())
        {
            var n = await Svc(ctx).CopyWeekAsync(await Usr(ctx, eng), Mon, Mon.AddDays(7), default);
            Assert.Equal(2, n);
        }
        await using (var ctx = db.NewContext())
        {
            var week = await Svc(ctx).GetWeekAsync(await Usr(ctx, eng), Mon.AddDays(7), default);
            Assert.Equal(9m, week.TotalHours);
        }
    }

    [Fact]
    public async Task Approve_sets_status_and_cost_rate_snapshot()
    {
        using var db = new SqliteTestDb();
        var pm = await SeedUser(db, "pm", "pm");
        var eng = await SeedUser(db, "eng", "engineer");
        var pid = await SeedProject(db, pm);
        var workDate = new DateOnly(2026, 3, 10);
        var entryId = await SeedEntry(db, eng, pid, TimeEntryStatus.Submitted, workDate);

        await using (var ctx = db.NewContext())
        {
            ctx.UserCostRates.Add(new UserCostRate { UserId = eng, CostRate = 1200m, ValidFrom = workDate.AddDays(-30) });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = db.NewContext())
        {
            var n = await Svc(ctx).ApproveAsync(await Usr(ctx, pm), [entryId], default);
            Assert.Equal(1, n);
        }
        await using (var ctx = db.NewContext())
        {
            var e = await ctx.TimeEntries.FindAsync(entryId);
            Assert.Equal(TimeEntryStatus.Approved, e!.Status);
            Assert.Equal(1200m, e.CostRateSnapshot);
        }
    }

    [Fact]
    public async Task Approve_by_non_manager_pm_is_not_approver()
    {
        using var db = new SqliteTestDb();
        var pm = await SeedUser(db, "pm", "pm");
        var otherPm = await SeedUser(db, "pm2", "pm");
        var eng = await SeedUser(db, "eng", "engineer");
        var pid = await SeedProject(db, pm);
        var entryId = await SeedEntry(db, eng, pid, TimeEntryStatus.Submitted, Mon);

        await using var ctx = db.NewContext();
        var otherPmUser = await Usr(ctx, otherPm);
        await Assert.ThrowsAsync<NotApproverException>(
            () => Svc(ctx).ApproveAsync(otherPmUser, [entryId], default));
    }

    [Fact]
    public async Task Reject_requires_reason_and_notifies_user()
    {
        using var db = new SqliteTestDb();
        var pm = await SeedUser(db, "pm", "pm");
        var eng = await SeedUser(db, "eng", "engineer");
        var pid = await SeedProject(db, pm);
        var entryId = await SeedEntry(db, eng, pid, TimeEntryStatus.Submitted, Mon);

        await using (var ctx = db.NewContext())
        {
            var pmUser = await Usr(ctx, pm);
            await Assert.ThrowsAsync<DomainValidationException>(
                () => Svc(ctx).RejectAsync(pmUser, [entryId], "  ", default));
        }

        await using (var ctx = db.NewContext())
        {
            var n = await Svc(ctx).RejectAsync(await Usr(ctx, pm), [entryId], "мало деталей", default);
            Assert.Equal(1, n);
        }
        await using (var ctx = db.NewContext())
        {
            var e = await ctx.TimeEntries.FindAsync(entryId);
            Assert.Equal(TimeEntryStatus.Rejected, e!.Status);
            Assert.Equal("мало деталей", e.RejectReason);
            Assert.True(ctx.Notifications.Any(x => x.UserId == eng && x.Kind == "timesheet_rejected"));
        }
    }

    [Fact]
    public async Task Reverse_creates_negative_mirror_only_for_approved()
    {
        using var db = new SqliteTestDb();
        var pm = await SeedUser(db, "pm", "pm");
        var eng = await SeedUser(db, "eng", "engineer");
        var pid = await SeedProject(db, pm);
        var draft = await SeedEntry(db, eng, pid, TimeEntryStatus.Draft, Mon, 8m);
        var approved = await SeedEntry(db, eng, pid, TimeEntryStatus.Approved, Mon, 8m);

        await using (var ctx = db.NewContext())
        {
            var pmUser = await Usr(ctx, pm);
            await Assert.ThrowsAsync<ConflictException>(() => Svc(ctx).ReverseAsync(pmUser, draft, default));
        }

        await using (var ctx = db.NewContext())
        {
            var rev = await Svc(ctx).ReverseAsync(await Usr(ctx, pm), approved, default);
            Assert.Equal(-8m, rev.Hours);
            Assert.Equal(approved, rev.ReversalOf);
            Assert.Equal(TimeEntryStatus.Approved, rev.Status);
        }
    }

    [Fact]
    public async Task Pending_privileged_admin_sees_all_manager_sees_own()
    {
        using var db = new SqliteTestDb();
        var pm = await SeedUser(db, "pm", "pm");
        var admin = await SeedUser(db, "admin", "admin");
        var eng = await SeedUser(db, "eng", "engineer");
        var pidOwn = await SeedProject(db, pm);
        var pidOther = await SeedProject(db, admin);
        await SeedEntry(db, eng, pidOwn, TimeEntryStatus.Submitted, Mon);
        await SeedEntry(db, eng, pidOther, TimeEntryStatus.Submitted, Mon);

        await using var ctx = db.NewContext();
        var svc = Svc(ctx);
        Assert.Equal(2, (await svc.PendingForAsync(await Usr(ctx, admin), default)).Count);
        Assert.Single(await svc.PendingForAsync(await Usr(ctx, pm), default));
    }
}
