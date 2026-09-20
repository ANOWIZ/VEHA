using Microsoft.Extensions.Options;
using Veha.Api.Config;
using Veha.Api.Dtos;
using Veha.Api.Services;
using Veha.Domain.Common;
using Veha.Domain.Entities;
using Veha.Domain.Enums;
using Veha.Infrastructure;
using Veha.Tests.Support;

namespace Veha.Tests.Services;

/// <summary>Интеграционные тесты CustomerService: ожидания (create/accept/provide),
/// скоуп клиента, портал, артефакты.</summary>
public class CustomerServiceTests
{
    private static CustomerService Svc(VehaDbContext ctx, string? artifactsDir = null)
        => new(ctx, new NotificationService(ctx),
            new ArtifactStorage(Options.Create(new VehaSettings { ArtifactsDir = artifactsDir ?? "artifacts" })));

    private static async Task<Guid> SeedUser(SqliteTestDb db, string name, Guid? clientId, params string[] roles)
    {
        await using var ctx = db.NewContext();
        var u = new User { Username = name, Email = $"{name}@x.test", FullName = name, IsActive = true, Roles = roles.ToList(), ClientId = clientId };
        ctx.Users.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    private static async Task<Guid> SeedProject(SqliteTestDb db, Guid clientId, Guid managerId)
    {
        await using var ctx = db.NewContext();
        var p = new Project
        {
            Code = $"PRJ-2008-{Guid.NewGuid().ToString()[..3]}", Name = "P", ClientId = clientId,
            Type = ProjectType.Implementation, ManagerId = managerId, Stage = Stage.Survey, Status = ProjectStatus.Active,
        };
        ctx.Projects.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task Create_item_notifies_responsible_and_lists()
    {
        using var db = new SqliteTestDb();
        var pm = await SeedUser(db, "pm", null, "pm");
        var resp = await SeedUser(db, "eng", null, "engineer");
        var pid = await SeedProject(db, Guid.NewGuid(), pm);

        await using (var ctx = db.NewContext())
            await Svc(ctx).CreateActionItemAsync(pid, new ActionItemCreateDto
            {
                Title = "Дать доступ к стенду", ResponsibleName = "Иванов", ResponsibleUserId = resp,
            }, pm, default);

        await using (var ctx = db.NewContext())
        {
            var items = await Svc(ctx).ListForProjectAsync(pid, default);
            Assert.Single(items);
            Assert.Equal(CustomerActionStatus.Waiting, items[0].Status);
            Assert.True(ctx.Notifications.Any(n => n.UserId == resp && n.Kind == "customer_action_assigned"));
        }
    }

    [Fact]
    public async Task Accept_is_idempotent_conflict()
    {
        using var db = new SqliteTestDb();
        var pm = await SeedUser(db, "pm", null, "pm");
        var pid = await SeedProject(db, Guid.NewGuid(), pm);
        Guid itemId;
        await using (var ctx = db.NewContext())
            itemId = (await Svc(ctx).CreateActionItemAsync(pid, new ActionItemCreateDto { Title = "T", ResponsibleName = "R" }, pm, default)).Id;

        await using (var ctx = db.NewContext())
            await Svc(ctx).AcceptAsync(itemId, pm, default);
        await using (var ctx = db.NewContext())
            await Assert.ThrowsAsync<ConflictException>(() => Svc(ctx).AcceptAsync(itemId, pm, default));
    }

    [Fact]
    public async Task Provide_by_client_sets_provided_and_notifies_manager()
    {
        using var db = new SqliteTestDb();
        var clientOrg = Guid.NewGuid();
        var pm = await SeedUser(db, "pm", null, "pm");
        var clientUserId = await SeedUser(db, "client", clientOrg, "client");
        var pid = await SeedProject(db, clientOrg, pm);
        Guid itemId;
        await using (var ctx = db.NewContext())
            itemId = (await Svc(ctx).CreateActionItemAsync(pid, new ActionItemCreateDto { Title = "T", ResponsibleName = "R" }, pm, default)).Id;

        await using (var ctx = db.NewContext())
        {
            var client = await ctx.Users.FindAsync(clientUserId);
            var provided = await Svc(ctx).ProvideAsync(client!, itemId, "готово", default);
            Assert.Equal(CustomerActionStatus.Provided, provided.Status);
            Assert.Equal("готово", provided.ProvidedNote);
            Assert.True(ctx.Notifications.Any(n => n.UserId == pm && n.Kind == "customer_action_provided"));
        }
    }

    [Fact]
    public async Task Client_access_denied_for_other_organization()
    {
        using var db = new SqliteTestDb();
        var pm = await SeedUser(db, "pm", null, "pm");
        var clientUserId = await SeedUser(db, "client", Guid.NewGuid(), "client");   // другой клиент
        var pid = await SeedProject(db, Guid.NewGuid(), pm);

        await using var ctx = db.NewContext();
        var client = await ctx.Users.FindAsync(clientUserId);
        await Assert.ThrowsAsync<ForbiddenException>(() => Svc(ctx).AssertClientAccessAsync(client!, pid, default));
    }

    [Fact]
    public async Task Portal_projects_scoped_to_client_with_open_flag()
    {
        using var db = new SqliteTestDb();
        var clientOrg = Guid.NewGuid();
        var pm = await SeedUser(db, "pm", null, "pm");
        var clientUserId = await SeedUser(db, "client", clientOrg, "client");
        var mine = await SeedProject(db, clientOrg, pm);
        await SeedProject(db, Guid.NewGuid(), pm);   // чужой клиент — не виден
        await using (var ctx = db.NewContext())
            await Svc(ctx).CreateActionItemAsync(mine, new ActionItemCreateDto { Title = "T", ResponsibleName = "R" }, pm, default);

        await using (var ctx = db.NewContext())
        {
            var client = await ctx.Users.FindAsync(clientUserId);
            var projects = await Svc(ctx).PortalProjectsAsync(client!, default);
            var p = Assert.Single(projects);
            Assert.Equal(mine, p.ProjectId);
            Assert.True(p.BlockedOnCustomer);
            Assert.Equal(1, p.OpenActions);
        }
    }

    [Fact]
    public async Task Artifact_save_and_read_roundtrip()
    {
        using var db = new SqliteTestDb();
        var dir = Path.Combine(Path.GetTempPath(), "veha-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var pid = Guid.NewGuid();
            var data = new byte[] { 1, 2, 3, 4, 5 };
            await using (var ctx = db.NewContext())
            {
                var svc = Svc(ctx, dir);
                var art = await svc.SaveArtifactAsync(pid, null, "spec.pdf", "application/pdf", data, null, default);
                Assert.Equal(5, art.SizeBytes);
                var entity = await ctx.Artifacts.FindAsync(art.Id);
                var read = await svc.ReadArtifactAsync(entity!, default);
                Assert.Equal(data, read);
            }
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
