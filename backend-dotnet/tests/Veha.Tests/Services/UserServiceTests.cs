using Veha.Api.Dtos;
using Veha.Api.Services;
using Veha.Domain.Common;
using Veha.Domain.Entities;
using Veha.Tests.Support;

namespace Veha.Tests.Services;

/// <summary>Интеграционные тесты UserService: список/фильтр active_only, маскирование
/// current_cost_rate, версионирование ставок (авто-закрытие открытой + конфликт), 404.</summary>
public class UserServiceTests
{
    private static async Task<Guid> SeedUser(SqliteTestDb db, string fullName, bool active = true)
    {
        await using var ctx = db.NewContext();
        var handle = fullName.Replace(" ", "").ToLower();
        var u = new User
        {
            Username = handle,
            Email = $"{handle}@x.test",
            FullName = fullName,
            IsActive = active,
        };
        ctx.Users.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    [Fact]
    public async Task List_orders_by_full_name_and_filters_inactive()
    {
        using var db = new SqliteTestDb();
        await SeedUser(db, "Bob");
        await SeedUser(db, "Alice");
        await SeedUser(db, "Carol", active: false);

        await using var ctx = db.NewContext();
        var svc = new UserService(ctx);

        var activeOnly = await svc.ListAsync(50, 0, true, default);
        Assert.Equal(2, activeOnly.Total);
        Assert.Equal(new[] { "Alice", "Bob" }, activeOnly.Items.Select(u => u.FullName).ToArray());

        var all = await svc.ListAsync(50, 0, false, default);
        Assert.Equal(3, all.Total);
        Assert.Equal(new[] { "Alice", "Bob", "Carol" }, all.Items.Select(u => u.FullName).ToArray());
    }

    [Fact]
    public async Task List_paginates()
    {
        using var db = new SqliteTestDb();
        foreach (var n in new[] { "A", "B", "C", "D" })
            await SeedUser(db, n);

        await using var ctx = db.NewContext();
        var page = await new UserService(ctx).ListAsync(2, 1, true, default);

        Assert.Equal(4, page.Total);
        Assert.Equal(new[] { "B", "C" }, page.Items.Select(u => u.FullName).ToArray());
    }

    [Fact]
    public async Task Get_masks_current_cost_rate_for_non_financial_roles()
    {
        using var db = new SqliteTestDb();
        var id = await SeedUser(db, "Engineer One");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await using (var ctx = db.NewContext())
            await new UserService(ctx).SetCostRateAsync(id,
                new CostRateCreateDto { CostRate = 1500m, ValidFrom = today.AddDays(-10) }, default);

        await using (var ctx = db.NewContext())
        {
            var svc = new UserService(ctx);
            var masked = await svc.GetAsync(id, includeFinancials: false, default);
            Assert.Null(masked.CurrentCostRate);

            var visible = await svc.GetAsync(id, includeFinancials: true, default);
            Assert.Equal(1500m, visible.CurrentCostRate);
        }
    }

    [Fact]
    public async Task Get_missing_throws_not_found()
    {
        using var db = new SqliteTestDb();
        await using var ctx = db.NewContext();
        await Assert.ThrowsAsync<NotFoundException>(
            () => new UserService(ctx).GetAsync(Guid.NewGuid(), true, default));
    }

    [Fact]
    public async Task SetCostRate_auto_closes_prior_open_rate()
    {
        using var db = new SqliteTestDb();
        var id = await SeedUser(db, "Rate User");

        await using (var ctx = db.NewContext())
            await new UserService(ctx).SetCostRateAsync(id,
                new CostRateCreateDto { CostRate = 100m, ValidFrom = new DateOnly(2026, 1, 1) }, default);

        await using (var ctx = db.NewContext())
            await new UserService(ctx).SetCostRateAsync(id,
                new CostRateCreateDto { CostRate = 120m, ValidFrom = new DateOnly(2026, 6, 1) }, default);

        await using (var ctx = db.NewContext())
        {
            var svc = new UserService(ctx);
            // Прежняя открытая ставка закрыта днём до начала новой.
            Assert.Equal(100m, await svc.EffectiveCostRateAsync(id, new DateOnly(2026, 5, 31), default));
            Assert.Equal(120m, await svc.EffectiveCostRateAsync(id, new DateOnly(2026, 6, 1), default));
            Assert.Equal(100m, await svc.EffectiveCostRateAsync(id, new DateOnly(2026, 3, 1), default));
        }
    }

    [Fact]
    public async Task SetCostRate_conflicts_on_overlap_with_closed_interval()
    {
        using var db = new SqliteTestDb();
        var id = await SeedUser(db, "Conflict User");

        await using (var ctx = db.NewContext())
            await new UserService(ctx).SetCostRateAsync(id, new CostRateCreateDto
            {
                CostRate = 100m,
                ValidFrom = new DateOnly(2026, 1, 1),
                ValidTo = new DateOnly(2026, 12, 31),
            }, default);

        await using (var ctx = db.NewContext())
        {
            await Assert.ThrowsAsync<ConflictException>(() =>
                new UserService(ctx).SetCostRateAsync(id, new CostRateCreateDto
                {
                    CostRate = 120m,
                    ValidFrom = new DateOnly(2026, 6, 1),
                    ValidTo = new DateOnly(2026, 8, 1),
                }, default));
        }
    }

    [Fact]
    public async Task SetCostRate_missing_user_throws_not_found()
    {
        using var db = new SqliteTestDb();
        await using var ctx = db.NewContext();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UserService(ctx).SetCostRateAsync(Guid.NewGuid(),
                new CostRateCreateDto { CostRate = 100m, ValidFrom = new DateOnly(2026, 1, 1) }, default));
    }

    [Fact]
    public async Task EffectiveCostRate_returns_null_outside_any_interval()
    {
        using var db = new SqliteTestDb();
        var id = await SeedUser(db, "Gap User");
        await using (var ctx = db.NewContext())
            await new UserService(ctx).SetCostRateAsync(id, new CostRateCreateDto
            {
                CostRate = 100m,
                ValidFrom = new DateOnly(2026, 1, 1),
                ValidTo = new DateOnly(2026, 6, 30),
            }, default);

        await using (var ctx = db.NewContext())
        {
            var rate = await new UserService(ctx)
                .EffectiveCostRateAsync(id, new DateOnly(2025, 12, 31), default);
            Assert.Null(rate);
        }
    }
}
