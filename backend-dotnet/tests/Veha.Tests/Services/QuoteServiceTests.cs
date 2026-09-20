using Veha.Api.Dtos;
using Veha.Api.Services;
using Veha.Domain.Common;
using Veha.Domain.Enums;
using Veha.Infrastructure;
using Veha.Tests.Support;

namespace Veha.Tests.Services;

/// <summary>Интеграционные тесты QuoteService: версии, строки+пересчёт, блокировка
/// правки, машина статусов, клонирование, IDOR-скоуп к проекту.</summary>
public class QuoteServiceTests
{
    private static QuoteService Svc(VehaDbContext ctx) => new(ctx, new AuditService(ctx));

    private static QuoteCreateDto CreateDto() => new()
    {
        Title = "ТКП", CurrencyRates = new() { ["USD"] = "90" }, CurrencyBufferPct = 10m,
    };

    private static QuoteLineInputDto LicenseLine() => new()
    {
        Kind = QuoteLineKind.License, Name = "СУБД", LicensingModel = LicensingModel.PerUser,
        Currency = "USD", Qty = 1m, UnitPrice = 100m, PartnerDiscountPct = 20m,
    };

    [Fact]
    public async Task Create_starts_version_1_draft()
    {
        using var db = new SqliteTestDb();
        var pid = Guid.NewGuid();
        await using var ctx = db.NewContext();
        var q = await Svc(ctx).CreateAsync(pid, CreateDto(), Guid.NewGuid(), default);
        Assert.Equal(1, q.Version);
        Assert.Equal(QuoteStatus.Draft, q.Status);
    }

    [Fact]
    public async Task Add_line_recomputes_line_and_totals()
    {
        using var db = new SqliteTestDb();
        var pid = Guid.NewGuid();
        Guid qid;
        await using (var ctx = db.NewContext())
            qid = (await Svc(ctx).CreateAsync(pid, CreateDto(), Guid.NewGuid(), default)).Id;

        await using (var ctx = db.NewContext())
        {
            var detail = await Svc(ctx).AddLineAsync(qid, LicenseLine(), pid, default);
            var line = Assert.Single(detail.Lines);
            Assert.Equal(9900m, line.SellAmount);
            Assert.Equal(7920m, line.CostAmount);
            Assert.Equal("9900.00", detail.Totals["total_sell"]);
            Assert.Equal("9900.00", detail.Totals["licenses_sell"]);
        }
    }

    [Fact]
    public async Task Editing_non_draft_is_locked()
    {
        using var db = new SqliteTestDb();
        var pid = Guid.NewGuid();
        var actor = Guid.NewGuid();
        Guid qid;
        await using (var ctx = db.NewContext())
            qid = (await Svc(ctx).CreateAsync(pid, CreateDto(), actor, default)).Id;

        await using (var ctx = db.NewContext())
            await Svc(ctx).SetStatusAsync(qid, QuoteStatus.Accepted, actor, pid, default);   // draft → accepted

        await using (var ctx = db.NewContext())
            await Assert.ThrowsAsync<QuoteLockedException>(() => Svc(ctx).AddLineAsync(qid, LicenseLine(), pid, default));
    }

    [Fact]
    public async Task Accepted_is_terminal()
    {
        using var db = new SqliteTestDb();
        var pid = Guid.NewGuid();
        var actor = Guid.NewGuid();
        Guid qid;
        await using (var ctx = db.NewContext())
            qid = (await Svc(ctx).CreateAsync(pid, CreateDto(), actor, default)).Id;

        await using (var ctx = db.NewContext())
            await Svc(ctx).SetStatusAsync(qid, QuoteStatus.Accepted, actor, pid, default);

        await using (var ctx = db.NewContext())
            await Assert.ThrowsAsync<QuoteLockedException>(() => Svc(ctx).SetStatusAsync(qid, QuoteStatus.Sent, actor, pid, default));
    }

    [Fact]
    public async Task Clone_creates_next_version_draft_with_lines()
    {
        using var db = new SqliteTestDb();
        var pid = Guid.NewGuid();
        var actor = Guid.NewGuid();
        Guid qid;
        await using (var ctx = db.NewContext())
            qid = (await Svc(ctx).CreateAsync(pid, CreateDto(), actor, default)).Id;
        await using (var ctx = db.NewContext())
            await Svc(ctx).AddLineAsync(qid, LicenseLine(), pid, default);
        await using (var ctx = db.NewContext())
            await Svc(ctx).SetStatusAsync(qid, QuoteStatus.Accepted, actor, pid, default);

        await using (var ctx = db.NewContext())
        {
            var clone = await Svc(ctx).CloneAsync(qid, actor, pid, default);
            Assert.Equal(2, clone.Version);
            Assert.Equal(QuoteStatus.Draft, clone.Status);
            Assert.Single(clone.Lines);
            Assert.Equal(9900m, clone.Lines[0].SellAmount);
        }
    }

    [Fact]
    public async Task Get_scoped_to_project_hides_other_project_quote()
    {
        using var db = new SqliteTestDb();
        var pidA = Guid.NewGuid();
        Guid qid;
        await using (var ctx = db.NewContext())
            qid = (await Svc(ctx).CreateAsync(pidA, CreateDto(), Guid.NewGuid(), default)).Id;

        await using (var ctx = db.NewContext())
            await Assert.ThrowsAsync<NotFoundException>(() => Svc(ctx).GetAsync(qid, Guid.NewGuid(), default));
    }
}
