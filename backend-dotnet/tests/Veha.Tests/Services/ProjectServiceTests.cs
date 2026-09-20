using Veha.Api.Dtos;
using Veha.Api.Services;
using Veha.Domain.Common;
using Veha.Domain.Entities;
using Veha.Domain.Enums;
using Veha.Infrastructure;
using Veha.Tests.Support;

namespace Veha.Tests.Services;

/// <summary>Интеграционные тесты ProjectService: code-gen, стадии, guard'ы, аудит,
/// участники, вехи, RBAC-скоуп списка.</summary>
public class ProjectServiceTests
{
    private static ProjectService Svc(VehaDbContext ctx) => new(ctx, new AuditService(ctx));

    private static async Task<Guid> SeedUser(SqliteTestDb db, string name, params string[] roles)
    {
        await using var ctx = db.NewContext();
        var u = new User { Username = name, Email = $"{name}@x.test", FullName = name, IsActive = true, Roles = roles.ToList() };
        ctx.Users.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    private static ProjectCreateDto NewDto(Guid managerId, DateOnly? plannedStart = null) => new()
    {
        Name = "ERP Внедрение",
        ClientId = Guid.NewGuid(),
        Type = ProjectType.Implementation,
        ManagerId = managerId,
        PlannedStart = plannedStart,
    };

    private static async Task<Guid> SeedProjectAt(
        SqliteTestDb db, Guid managerId, Stage stage,
        ProjectStatus status = ProjectStatus.Active, Guid? curatorId = null)
    {
        await using var ctx = db.NewContext();
        var p = new Project
        {
            Code = $"PRJ-2000-{Guid.NewGuid().ToString()[..3]}",
            Name = "P", ClientId = Guid.NewGuid(), Type = ProjectType.Implementation,
            ManagerId = managerId, CuratorId = curatorId, Stage = stage, Status = status,
        };
        ctx.Projects.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task Create_generates_code_initial_transition_and_audit()
    {
        using var db = new SqliteTestDb();
        var mgr = await SeedUser(db, "pm", "pm");

        Guid id;
        await using (var ctx = db.NewContext())
        {
            var dto = NewDto(mgr, new DateOnly(2026, 3, 1));
            var created = await Svc(ctx).CreateAsync(dto, mgr, default);
            id = created.Id;
            Assert.Equal("PRJ-2026-001", created.Code);
            Assert.Equal(Stage.Presale, created.Stage);
            Assert.Equal(ProjectStatus.Active, created.Status);
        }

        await using (var ctx = db.NewContext())
        {
            var transitions = await Svc(ctx).TransitionsAsync(id, default);
            var initial = Assert.Single(transitions);
            Assert.Null(initial.FromStage);
            Assert.Equal(Stage.Presale, initial.ToStage);
            Assert.Equal("Создание проекта", initial.Reason);

            Assert.True(ctx.AuditLogs.Any(a => a.Entity == "Project" && a.Action == AuditAction.Create));
        }
    }

    [Fact]
    public async Task Create_second_project_increments_code()
    {
        using var db = new SqliteTestDb();
        var mgr = await SeedUser(db, "pm", "pm");
        await using var ctx = db.NewContext();
        var svc = Svc(ctx);
        var p1 = await svc.CreateAsync(NewDto(mgr, new DateOnly(2026, 1, 1)), mgr, default);
        var p2 = await svc.CreateAsync(NewDto(mgr, new DateOnly(2026, 1, 1)), mgr, default);
        Assert.Equal("PRJ-2026-001", p1.Code);
        Assert.Equal("PRJ-2026-002", p2.Code);
    }

    [Fact]
    public async Task Create_missing_manager_throws_not_found()
    {
        using var db = new SqliteTestDb();
        await using var ctx = db.NewContext();
        await Assert.ThrowsAsync<NotFoundException>(
            () => Svc(ctx).CreateAsync(NewDto(Guid.NewGuid()), Guid.NewGuid(), default));
    }

    [Fact]
    public async Task NextCode_uses_max_suffix_including_deleted()
    {
        using var db = new SqliteTestDb();
        var mgr = await SeedUser(db, "pm", "pm");

        Guid firstId;
        await using (var ctx = db.NewContext())
            firstId = (await Svc(ctx).CreateAsync(NewDto(mgr, new DateOnly(2026, 1, 1)), mgr, default)).Id;

        // Удаляем первый проект — его код PRJ-2026-001 остаётся занятым (unique).
        await using (var ctx = db.NewContext())
            await Svc(ctx).DeleteAsync(firstId, mgr, default);

        await using (var ctx = db.NewContext())
        {
            var p2 = await Svc(ctx).CreateAsync(NewDto(mgr, new DateOnly(2026, 1, 1)), mgr, default);
            Assert.Equal("PRJ-2026-002", p2.Code);   // не 001
        }
    }

    [Fact]
    public async Task Update_applies_changes_and_records_diff()
    {
        using var db = new SqliteTestDb();
        var mgr = await SeedUser(db, "pm", "pm");
        var id = await SeedProjectAt(db, mgr, Stage.Presale);

        await using (var ctx = db.NewContext())
        {
            var updated = await Svc(ctx).UpdateAsync(id,
                new ProjectUpdateDto { Name = "Новое имя", BudgetRevenue = 500000m }, mgr, default);
            Assert.Equal("Новое имя", updated.Name);
            Assert.Equal(500000m, updated.BudgetRevenue);
            Assert.True(ctx.AuditLogs.Any(a => a.Entity == "Project" && a.Action == AuditAction.Update));
        }
    }

    [Fact]
    public async Task Update_closed_project_blocks_non_status_change()
    {
        using var db = new SqliteTestDb();
        var mgr = await SeedUser(db, "pm", "pm");
        var id = await SeedProjectAt(db, mgr, Stage.Support, ProjectStatus.Closed);

        await using var ctx = db.NewContext();
        await Assert.ThrowsAsync<ProjectClosedException>(
            () => Svc(ctx).UpdateAsync(id, new ProjectUpdateDto { Name = "x" }, mgr, default));
    }

    [Fact]
    public async Task Update_closed_project_allows_status_change()
    {
        using var db = new SqliteTestDb();
        var mgr = await SeedUser(db, "pm", "pm");
        var id = await SeedProjectAt(db, mgr, Stage.Support, ProjectStatus.Closed);

        await using var ctx = db.NewContext();
        var updated = await Svc(ctx).UpdateAsync(id,
            new ProjectUpdateDto { Status = ProjectStatus.Active }, mgr, default);
        Assert.Equal(ProjectStatus.Active, updated.Status);
    }

    [Fact]
    public async Task ChangeStage_forward_advances()
    {
        using var db = new SqliteTestDb();
        var mgr = await SeedUser(db, "pm", "pm");
        var id = await SeedProjectAt(db, mgr, Stage.Presale);

        await using var ctx = db.NewContext();
        var updated = await Svc(ctx).ChangeStageAsync(id, Stage.Survey, null, mgr, default);
        Assert.Equal(Stage.Survey, updated.Stage);
        Assert.True(ctx.AuditLogs.Any(a => a.Action == AuditAction.StageChange));
    }

    [Fact]
    public async Task ChangeStage_backward_requires_reason()
    {
        using var db = new SqliteTestDb();
        var mgr = await SeedUser(db, "pm", "pm");
        var id = await SeedProjectAt(db, mgr, Stage.Design);

        await using (var ctx = db.NewContext())
            await Assert.ThrowsAsync<InvalidStageTransitionException>(
                () => Svc(ctx).ChangeStageAsync(id, Stage.Survey, null, mgr, default));

        await using (var ctx = db.NewContext())
        {
            var updated = await Svc(ctx).ChangeStageAsync(id, Stage.Survey, "откат по требованию", mgr, default);
            Assert.Equal(Stage.Survey, updated.Stage);
        }
    }

    [Fact]
    public async Task ChangeStage_skip_is_invalid()
    {
        using var db = new SqliteTestDb();
        var mgr = await SeedUser(db, "pm", "pm");
        var id = await SeedProjectAt(db, mgr, Stage.Presale);

        await using var ctx = db.NewContext();
        await Assert.ThrowsAsync<InvalidStageTransitionException>(
            () => Svc(ctx).ChangeStageAsync(id, Stage.Design, null, mgr, default));
    }

    [Fact]
    public async Task ChangeStage_to_implementation_sets_actual_start()
    {
        using var db = new SqliteTestDb();
        var mgr = await SeedUser(db, "pm", "pm");
        var id = await SeedProjectAt(db, mgr, Stage.Design);

        await using var ctx = db.NewContext();
        var updated = await Svc(ctx).ChangeStageAsync(id, Stage.Implementation, null, mgr, default);
        Assert.Equal(Stage.Implementation, updated.Stage);
        Assert.NotNull(updated.ActualStart);
    }

    [Fact]
    public async Task ChangeStage_to_closed_closes_project()
    {
        using var db = new SqliteTestDb();
        var mgr = await SeedUser(db, "pm", "pm");
        var id = await SeedProjectAt(db, mgr, Stage.Support);

        await using var ctx = db.NewContext();
        var updated = await Svc(ctx).ChangeStageAsync(id, Stage.Closed, null, mgr, default);
        Assert.Equal(Stage.Closed, updated.Stage);
        Assert.Equal(ProjectStatus.Closed, updated.Status);
        Assert.NotNull(updated.ActualEnd);
    }

    [Fact]
    public async Task ChangeStage_on_closed_project_throws()
    {
        using var db = new SqliteTestDb();
        var mgr = await SeedUser(db, "pm", "pm");
        var id = await SeedProjectAt(db, mgr, Stage.Survey, ProjectStatus.Cancelled);

        await using var ctx = db.NewContext();
        await Assert.ThrowsAsync<ProjectClosedException>(
            () => Svc(ctx).ChangeStageAsync(id, Stage.Design, null, mgr, default));
    }

    [Fact]
    public async Task Members_add_list_and_remove()
    {
        using var db = new SqliteTestDb();
        var mgr = await SeedUser(db, "pm", "pm");
        var eng = await SeedUser(db, "eng", "engineer");
        var id = await SeedProjectAt(db, mgr, Stage.Presale);

        Guid memberId;
        await using (var ctx = db.NewContext())
        {
            var m = await Svc(ctx).AddMemberAsync(id,
                new MemberCreateDto { UserId = eng, Role = ProjectMemberRole.Engineer, BillRate = 2500m }, default);
            memberId = m.Id;
            Assert.Equal(2500m, m.BillRate);
        }

        await using (var ctx = db.NewContext())
        {
            var members = await Svc(ctx).ListMembersAsync(id, default);
            Assert.Single(members);
            await Svc(ctx).RemoveMemberAsync(id, memberId, default);
        }

        await using (var ctx = db.NewContext())
            Assert.Empty(await Svc(ctx).ListMembersAsync(id, default));
    }

    [Fact]
    public async Task Members_duplicate_role_conflicts()
    {
        using var db = new SqliteTestDb();
        var mgr = await SeedUser(db, "pm", "pm");
        var eng = await SeedUser(db, "eng", "engineer");
        var id = await SeedProjectAt(db, mgr, Stage.Presale);

        await using (var ctx = db.NewContext())
            await Svc(ctx).AddMemberAsync(id,
                new MemberCreateDto { UserId = eng, Role = ProjectMemberRole.Engineer }, default);

        await using (var ctx = db.NewContext())
            await Assert.ThrowsAsync<ConflictException>(() => Svc(ctx).AddMemberAsync(id,
                new MemberCreateDto { UserId = eng, Role = ProjectMemberRole.Engineer }, default));
    }

    [Fact]
    public async Task Members_add_missing_user_throws_not_found()
    {
        using var db = new SqliteTestDb();
        var mgr = await SeedUser(db, "pm", "pm");
        var id = await SeedProjectAt(db, mgr, Stage.Presale);

        await using var ctx = db.NewContext();
        await Assert.ThrowsAsync<NotFoundException>(() => Svc(ctx).AddMemberAsync(id,
            new MemberCreateDto { UserId = Guid.NewGuid(), Role = ProjectMemberRole.Engineer }, default));
    }

    [Fact]
    public async Task Milestones_add_and_list()
    {
        using var db = new SqliteTestDb();
        var mgr = await SeedUser(db, "pm", "pm");
        var id = await SeedProjectAt(db, mgr, Stage.Presale);

        await using (var ctx = db.NewContext())
            await Svc(ctx).AddMilestoneAsync(id, new MilestoneCreateDto
            {
                Name = "Оплата 1", MilestoneDate = new DateOnly(2026, 6, 1), IsPayment = true, Amount = 100000m,
            }, default);

        await using (var ctx = db.NewContext())
        {
            var ms = await Svc(ctx).ListMilestonesAsync(id, default);
            var m = Assert.Single(ms);
            Assert.True(m.IsPayment);
            Assert.Equal(100000m, m.Amount);
        }
    }

    [Fact]
    public async Task List_privileged_sees_all_others_see_own()
    {
        using var db = new SqliteTestDb();
        var pmA = await SeedUser(db, "pmA", "pm");
        var pmB = await SeedUser(db, "pmB", "pm");
        var admin = await SeedUser(db, "admin", "admin");
        await SeedProjectAt(db, pmA, Stage.Presale);
        await SeedProjectAt(db, pmB, Stage.Presale);

        await using var ctx = db.NewContext();
        var svc = Svc(ctx);

        var adminUser = await ctx.Users.FindAsync(admin);
        var pmAUser = await ctx.Users.FindAsync(pmA);

        var all = await svc.ListForUserAsync(adminUser!, null, null, null, null, 50, 0, default);
        Assert.Equal(2, all.Total);

        var own = await svc.ListForUserAsync(pmAUser!, null, null, null, null, 50, 0, default);
        Assert.Equal(1, own.Total);
    }
}
