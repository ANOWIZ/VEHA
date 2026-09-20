using Veha.Api.Dtos;
using Veha.Api.Services;
using Veha.Domain.Common;
using Veha.Tests.Support;

namespace Veha.Tests.Services;

/// <summary>Интеграционные тесты ClientService (порт паритета с client_service.py):
/// CRUD, поиск по имени/ИНН, пагинация, soft-delete, 404.</summary>
public class ClientServiceTests
{
    private static async Task<Guid> Seed(SqliteTestDb db, string name, string? inn = null)
    {
        await using var ctx = db.NewContext();
        var svc = new ClientService(ctx);
        var created = await svc.CreateAsync(
            new ClientCreateDto { Name = name, Inn = inn }, default);
        return created.Id;
    }

    [Fact]
    public async Task Create_then_get_roundtrips_fields()
    {
        using var db = new SqliteTestDb();
        Guid id;
        await using (var ctx = db.NewContext())
        {
            var created = await new ClientService(ctx).CreateAsync(new ClientCreateDto
            {
                Name = "Acme LLC",
                Inn = "0000000001",
                Industry = "IT",
                Contacts = new() { ["email"] = "a@acme.test" },
                IsKii = true,
            }, default);
            id = created.Id;
            Assert.NotEqual(Guid.Empty, id);
        }

        await using (var ctx = db.NewContext())
        {
            var got = await new ClientService(ctx).GetAsync(id, default);
            Assert.Equal("Acme LLC", got.Name);
            Assert.Equal("0000000001", got.Inn);
            Assert.Equal("IT", got.Industry);
            Assert.Equal("a@acme.test", got.Contacts["email"]);
            Assert.True(got.IsKii);
        }
    }

    [Fact]
    public async Task Get_missing_throws_not_found()
    {
        using var db = new SqliteTestDb();
        await using var ctx = db.NewContext();
        await Assert.ThrowsAsync<NotFoundException>(
            () => new ClientService(ctx).GetAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Search_by_name_is_case_insensitive_and_ordered()
    {
        using var db = new SqliteTestDb();
        await Seed(db, "Beta Corp");
        await Seed(db, "Acme LLC");
        await Seed(db, "Gamma Inc");

        await using var ctx = db.NewContext();
        var page = await new ClientService(ctx).SearchAsync("acme", 50, 0, default);

        Assert.Equal(1, page.Total);
        Assert.Equal("Acme LLC", Assert.Single(page.Items).Name);
    }

    [Fact]
    public async Task Search_matches_by_inn()
    {
        using var db = new SqliteTestDb();
        await Seed(db, "Beta Corp", inn: "0000000002");
        await Seed(db, "Acme LLC", inn: "0000000001");

        await using var ctx = db.NewContext();
        var page = await new ClientService(ctx).SearchAsync("0000000001", 50, 0, default);

        Assert.Equal(1, page.Total);
        Assert.Equal("Acme LLC", Assert.Single(page.Items).Name);
    }

    [Fact]
    public async Task Search_empty_query_returns_all_ordered_by_name()
    {
        using var db = new SqliteTestDb();
        await Seed(db, "Gamma");
        await Seed(db, "Alpha");
        await Seed(db, "Beta");

        await using var ctx = db.NewContext();
        var page = await new ClientService(ctx).SearchAsync(null, 50, 0, default);

        Assert.Equal(3, page.Total);
        Assert.Equal(new[] { "Alpha", "Beta", "Gamma" }, page.Items.Select(c => c.Name).ToArray());
    }

    [Fact]
    public async Task Search_paginates_with_limit_and_offset()
    {
        using var db = new SqliteTestDb();
        foreach (var n in new[] { "A", "B", "C", "D", "E" })
            await Seed(db, n);

        await using var ctx = db.NewContext();
        var page = await new ClientService(ctx).SearchAsync(null, 2, 2, default);

        Assert.Equal(5, page.Total);          // total игнорирует пагинацию
        Assert.Equal(2, page.Limit);
        Assert.Equal(2, page.Offset);
        Assert.Equal(new[] { "C", "D" }, page.Items.Select(c => c.Name).ToArray());
    }

    [Fact]
    public async Task Update_is_partial_and_keeps_untouched_fields()
    {
        using var db = new SqliteTestDb();
        var id = await Seed(db, "Old Name", inn: "0000000001");

        await using (var ctx = db.NewContext())
            await new ClientService(ctx).UpdateAsync(
                id, new ClientUpdateDto { Name = "New Name" }, default);

        await using (var ctx = db.NewContext())
        {
            var got = await new ClientService(ctx).GetAsync(id, default);
            Assert.Equal("New Name", got.Name);
            Assert.Equal("0000000001", got.Inn);   // inn не передан → не изменён
        }
    }

    [Fact]
    public async Task Delete_soft_deletes_and_hides_from_get_and_search()
    {
        using var db = new SqliteTestDb();
        var id = await Seed(db, "Acme LLC");

        await using (var ctx = db.NewContext())
            await new ClientService(ctx).DeleteAsync(id, default);

        await using (var ctx = db.NewContext())
        {
            var svc = new ClientService(ctx);
            await Assert.ThrowsAsync<NotFoundException>(() => svc.GetAsync(id, default));
            var page = await svc.SearchAsync(null, 50, 0, default);
            Assert.Equal(0, page.Total);
            Assert.Empty(page.Items);
        }
    }

    [Fact]
    public async Task Delete_missing_throws_not_found()
    {
        using var db = new SqliteTestDb();
        await using var ctx = db.NewContext();
        await Assert.ThrowsAsync<NotFoundException>(
            () => new ClientService(ctx).DeleteAsync(Guid.NewGuid(), default));
    }
}
