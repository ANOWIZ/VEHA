using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Veha.Api.Dtos;
using Veha.Domain.Common;
using Veha.Domain.Entities;
using Veha.Domain.Enums;
using Veha.Domain.Rules;
using Veha.Infrastructure;

namespace Veha.Api.Services;

/// <summary>Калькулятор ТКП: версии, строки, пересчёт, статусы, клонирование.
/// Порт services/quote_service.py + quote_repo.py. Правка — только черновик; принятый/
/// отклонённый — терминальный (правка через новую версию).</summary>
public class QuoteService(VehaDbContext db, AuditService audit)
{
    // Машина состояний: accepted/rejected — терминальные.
    private static readonly Dictionary<QuoteStatus, QuoteStatus[]> Allowed = new()
    {
        [QuoteStatus.Draft] = [QuoteStatus.Sent, QuoteStatus.Accepted, QuoteStatus.Rejected],
        [QuoteStatus.Sent] = [QuoteStatus.Accepted, QuoteStatus.Rejected, QuoteStatus.Draft],
        [QuoteStatus.Accepted] = [],
        [QuoteStatus.Rejected] = [],
    };

    public async Task<List<QuoteOutDto>> ListForProjectAsync(Guid projectId, CancellationToken ct)
        => (await db.Quotes.Where(q => q.ProjectId == projectId).OrderByDescending(q => q.Version).ToListAsync(ct))
            .Select(ToOut).ToList();

    public async Task<Quote> GetEntityAsync(Guid quoteId, Guid? projectId, CancellationToken ct)
    {
        var q = db.Quotes.Include(x => x.Lines).Where(x => x.Id == quoteId);
        if (projectId is not null) q = q.Where(x => x.ProjectId == projectId);
        return await q.FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Расчёт не найден");
    }

    public async Task<QuoteDetailDto> GetAsync(Guid quoteId, Guid? projectId, CancellationToken ct)
        => ToDetail(await GetEntityAsync(quoteId, projectId, ct));

    private static void AssertEditable(Quote quote)
    {
        if (quote.Status != QuoteStatus.Draft)
            throw new QuoteLockedException(
                "Расчёт не в статусе «черновик» — редактирование запрещено (создайте новую версию)");
    }

    private async Task<int> MaxVersionAsync(Guid projectId, CancellationToken ct)
        => (await db.Quotes.IgnoreQueryFilters().Where(q => q.ProjectId == projectId)
                .Select(q => q.Version).ToListAsync(ct)).DefaultIfEmpty(0).Max();

    public async Task<QuoteDetailDto> CreateAsync(Guid projectId, QuoteCreateDto dto, Guid actorId, CancellationToken ct)
    {
        var version = await MaxVersionAsync(projectId, ct) + 1;
        var quote = new Quote
        {
            ProjectId = projectId, Version = version, Title = dto.Title, Status = QuoteStatus.Draft,
            CurrencyRatesSnapshot = dto.CurrencyRates, CurrencyBufferPct = dto.CurrencyBufferPct, Totals = new(),
        };
        db.Quotes.Add(quote);
        await audit.RecordAsync("Quote", quote.Id, AuditAction.Create, actorId, new { version }, ct);
        return await GetAsync(quote.Id, null, ct);
    }

    private async Task RecomputeAsync(Quote quote, CancellationToken ct)
    {
        var rates = quote.CurrencyRatesSnapshot.ToDictionary(
            k => k.Key.ToUpperInvariant(), v => decimal.Parse(v.Value, CultureInfo.InvariantCulture));
        var computed = new List<(QuoteCalc.LineCalcInput, QuoteCalc.LineCalcResult)>();
        foreach (var line in quote.Lines)
        {
            var input = ToCalcInput(line);
            var res = QuoteCalc.ComputeLine(input, rates, quote.CurrencyBufferPct);   // может бросить RateNotFound (422)
            line.CostAmount = res.CostAmount;
            line.SellAmount = res.SellAmount;
            line.Margin = res.Margin;
            computed.Add((input, res));
        }
        quote.Totals = QuoteCalc.ComputeTotals(computed);
        await db.SaveChangesAsync(ct);
    }

    public async Task<QuoteDetailDto> AddLineAsync(Guid quoteId, QuoteLineInputDto dto, Guid? projectId, CancellationToken ct)
    {
        var quote = await GetEntityAsync(quoteId, projectId, ct);
        AssertEditable(quote);
        db.QuoteLines.Add(FromInput(quote.Id, dto));
        await db.SaveChangesAsync(ct);
        var fresh = await GetEntityAsync(quoteId, projectId, ct);
        await RecomputeAsync(fresh, ct);
        return ToDetail(fresh);
    }

    public async Task<QuoteDetailDto> UpdateLineAsync(Guid quoteId, Guid lineId, QuoteLineInputDto dto, Guid? projectId, CancellationToken ct)
    {
        var quote = await GetEntityAsync(quoteId, projectId, ct);
        AssertEditable(quote);
        var line = quote.Lines.FirstOrDefault(l => l.Id == lineId)
                   ?? throw new NotFoundException("Строка расчёта не найдена");
        ApplyInput(line, dto);
        await RecomputeAsync(quote, ct);
        return ToDetail(quote);
    }

    public async Task<QuoteDetailDto> DeleteLineAsync(Guid quoteId, Guid lineId, Guid? projectId, CancellationToken ct)
    {
        var quote = await GetEntityAsync(quoteId, projectId, ct);
        AssertEditable(quote);
        var line = quote.Lines.FirstOrDefault(l => l.Id == lineId)
                   ?? throw new NotFoundException("Строка расчёта не найдена");
        db.QuoteLines.Remove(line);
        await db.SaveChangesAsync(ct);
        var fresh = await GetEntityAsync(quoteId, projectId, ct);
        await RecomputeAsync(fresh, ct);
        return ToDetail(fresh);
    }

    public async Task<QuoteOutDto> SetStatusAsync(Guid quoteId, QuoteStatus status, Guid actorId, Guid? projectId, CancellationToken ct)
    {
        var quote = await GetEntityAsync(quoteId, projectId, ct);
        if (status != quote.Status && !Allowed[quote.Status].Contains(status))
            throw new QuoteLockedException(
                $"Недопустимый переход статуса расчёта: «{quote.Status}» → «{status}». " +
                "Принятый/отклонённый расчёт неизменяем — создайте новую версию.");
        quote.Status = status;
        await audit.RecordAsync("Quote", quote.Id, AuditAction.Update, actorId, new { status }, ct);
        return ToOut(quote);
    }

    public async Task<QuoteDetailDto> CloneAsync(Guid quoteId, Guid actorId, Guid? projectId, CancellationToken ct)
    {
        var src = await GetEntityAsync(quoteId, projectId, ct);
        var version = await MaxVersionAsync(src.ProjectId, ct) + 1;
        var newQuote = new Quote
        {
            ProjectId = src.ProjectId, Version = version, Title = src.Title, Status = QuoteStatus.Draft,
            CurrencyRatesSnapshot = new(src.CurrencyRatesSnapshot), CurrencyBufferPct = src.CurrencyBufferPct, Totals = new(),
        };
        db.Quotes.Add(newQuote);
        foreach (var line in src.Lines)
            db.QuoteLines.Add(new QuoteLine
            {
                QuoteId = newQuote.Id, Kind = line.Kind, Name = line.Name, ProductId = line.ProductId,
                LicensingModel = line.LicensingModel, Metric = line.Metric, Currency = line.Currency,
                Qty = line.Qty, UnitPrice = line.UnitPrice, UnitCost = line.UnitCost, TermMonths = line.TermMonths,
                SupportPct = line.SupportPct, PartnerDiscountPct = line.PartnerDiscountPct,
                ClientDiscountPct = line.ClientDiscountPct, Note = line.Note,
            });
        await db.SaveChangesAsync(ct);
        var fresh = await GetEntityAsync(newQuote.Id, null, ct);
        await audit.RecordAsync("Quote", newQuote.Id, AuditAction.Create, actorId,
            new { cloned_from = src.Id.ToString(), version }, ct);
        await RecomputeAsync(fresh, ct);
        return ToDetail(fresh);
    }

    // --- Мапперы ---
    private static QuoteCalc.LineCalcInput ToCalcInput(QuoteLine l) => new()
    {
        Kind = l.Kind, Qty = l.Qty, UnitPrice = l.UnitPrice, UnitCost = l.UnitCost, Currency = l.Currency,
        LicensingModel = l.LicensingModel, TermMonths = l.TermMonths, SupportPct = l.SupportPct,
        PartnerDiscountPct = l.PartnerDiscountPct, ClientDiscountPct = l.ClientDiscountPct,
    };

    private static QuoteLine FromInput(Guid quoteId, QuoteLineInputDto d) => new()
    {
        QuoteId = quoteId, Kind = d.Kind, Name = d.Name, ProductId = d.ProductId, LicensingModel = d.LicensingModel,
        Metric = d.Metric, Currency = d.Currency, Qty = d.Qty, UnitPrice = d.UnitPrice, UnitCost = d.UnitCost,
        TermMonths = d.TermMonths, SupportPct = d.SupportPct, PartnerDiscountPct = d.PartnerDiscountPct,
        ClientDiscountPct = d.ClientDiscountPct, Note = d.Note,
    };

    private static void ApplyInput(QuoteLine l, QuoteLineInputDto d)
    {
        l.Kind = d.Kind; l.Name = d.Name; l.ProductId = d.ProductId; l.LicensingModel = d.LicensingModel;
        l.Metric = d.Metric; l.Currency = d.Currency; l.Qty = d.Qty; l.UnitPrice = d.UnitPrice; l.UnitCost = d.UnitCost;
        l.TermMonths = d.TermMonths; l.SupportPct = d.SupportPct; l.PartnerDiscountPct = d.PartnerDiscountPct;
        l.ClientDiscountPct = d.ClientDiscountPct; l.Note = d.Note;
    }

    private static void FillOut(QuoteOutDto o, Quote q)
    {
        o.Id = q.Id; o.ProjectId = q.ProjectId; o.Version = q.Version; o.Title = q.Title; o.Status = q.Status;
        o.CurrencyRatesSnapshot = new(q.CurrencyRatesSnapshot); o.CurrencyBufferPct = q.CurrencyBufferPct;
        o.Totals = new(q.Totals); o.CreatedAt = q.CreatedAt;
    }

    public static QuoteOutDto ToOut(Quote q) { var o = new QuoteOutDto(); FillOut(o, q); return o; }

    public static QuoteDetailDto ToDetail(Quote q)
    {
        var d = new QuoteDetailDto { Lines = q.Lines.Select(ToLineOut).ToList() };
        FillOut(d, q);
        return d;
    }

    private static QuoteLineOutDto ToLineOut(QuoteLine l) => new(
        l.Id, l.Kind, l.Name, l.ProductId, l.LicensingModel, l.Metric, l.Currency, l.Qty, l.UnitPrice, l.UnitCost,
        l.TermMonths, l.SupportPct, l.PartnerDiscountPct, l.ClientDiscountPct, l.CostAmount, l.SellAmount, l.Margin, l.Note);
}
