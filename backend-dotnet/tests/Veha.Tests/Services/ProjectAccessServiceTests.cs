using Veha.Api.Services;
using Veha.Domain.Common;
using Veha.Domain.Entities;
using Veha.Domain.Enums;
using Veha.Infrastructure;
using Veha.Tests.Support;

namespace Veha.Tests.Services;

/// <summary>Тесты RBAC доступа к проекту (require_project_access / manage).</summary>
public class ProjectAccessServiceTests
{
    private static async Task<Guid> SeedUser(SqliteTestDb db, string name, params string[] roles)
    {
        await using var ctx = db.NewContext();
        var u = new User { Username = name, Email = $"{name}@x.test", FullName = name, IsActive = true, Roles = roles.ToList() };
        ctx.Users.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    private static async Task<Guid> SeedProject(SqliteTestDb db, Guid managerId, Guid? curatorId = null)
    {
        await using var ctx = db.NewContext();
        var p = new Project
        {
            Code = $"PRJ-2001-{Guid.NewGuid().ToString()[..3]}", Name = "P", ClientId = Guid.NewGuid(),
            Type = ProjectType.Implementation, ManagerId = managerId, CuratorId = curatorId,
            Stage = Stage.Presale, Status = ProjectStatus.Active,
        };
        ctx.Projects.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    private static async Task<User> User(VehaDbContext ctx, Guid id) => (await ctx.Users.FindAsync(id))!;

    [Fact]
    public async Task Access_granted_to_privileged_manager_curator_member()
    {
        using var db = new SqliteTestDb();
        var pm = await SeedUser(db, "pm", "pm");
        var curator = await SeedUser(db, "cur", "pm");
        var director = await SeedUser(db, "dir", "director");
        var member = await SeedUser(db, "eng", "engineer");
        var pid = await SeedProject(db, pm, curator);

        await using (var ctx = db.NewContext())
        {
            ctx.ProjectMembers.Add(new ProjectMember { ProjectId = pid, UserId = member, Role = ProjectMemberRole.Engineer });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = db.NewContext())
        {
            var access = new ProjectAccessService(ctx);
            Assert.NotNull(await access.RequireAccessAsync(pid, await User(ctx, director), default)); // privileged
            Assert.NotNull(await access.RequireAccessAsync(pid, await User(ctx, pm), default));       // manager
            Assert.NotNull(await access.RequireAccessAsync(pid, await User(ctx, curator), default));  // curator
            Assert.NotNull(await access.RequireAccessAsync(pid, await User(ctx, member), default));   // member
        }
    }

    [Fact]
    public async Task Access_denied_to_outsider_and_404_for_missing()
    {
        using var db = new SqliteTestDb();
        var pm = await SeedUser(db, "pm", "pm");
        var outsider = await SeedUser(db, "out", "engineer");
        var pid = await SeedProject(db, pm);

        await using var ctx = db.NewContext();
        var access = new ProjectAccessService(ctx);
        var outsiderUser = await User(ctx, outsider);
        var pmUser = await User(ctx, pm);
        await Assert.ThrowsAsync<ForbiddenException>(
            () => access.RequireAccessAsync(pid, outsiderUser, default));
        await Assert.ThrowsAsync<NotFoundException>(
            () => access.RequireAccessAsync(Guid.NewGuid(), pmUser, default));
    }

    [Fact]
    public async Task Manage_allows_admin_manager_curator_but_not_finance_or_member()
    {
        using var db = new SqliteTestDb();
        var pm = await SeedUser(db, "pm", "pm");
        var curator = await SeedUser(db, "cur", "pm");
        var admin = await SeedUser(db, "admin", "admin");
        var finance = await SeedUser(db, "fin", "finance");
        var pid = await SeedProject(db, pm, curator);

        await using var ctx = db.NewContext();
        var access = new ProjectAccessService(ctx);

        Assert.NotNull(await access.RequireManageAsync(pid, await User(ctx, admin), default));
        Assert.NotNull(await access.RequireManageAsync(pid, await User(ctx, pm), default));
        Assert.NotNull(await access.RequireManageAsync(pid, await User(ctx, curator), default));

        // finance имеет доступ (privileged), но не управление.
        var financeUser = await User(ctx, finance);
        await Assert.ThrowsAsync<ForbiddenException>(
            () => access.RequireManageAsync(pid, financeUser, default));
    }
}
