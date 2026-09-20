using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Veha.Domain.Entities;
using Veha.Domain.Enums;
using Veha.Infrastructure;

namespace Veha.Api.Services;

/// <summary>Обмен с 1С: выгрузка approved-таймшитов за период (идемпотентно по external_id).
/// Порт services/onec_service.py с симулированным шлюзом (accept all) — как дефолт Python.</summary>
public class OneCService(VehaDbContext db)
{
    private const string SystemName = "onec";

    public async Task<object> ExportTimesheetsAsync(DateOnly periodFrom, DateOnly periodTo, CancellationToken ct)
    {
        var rows = await (
            from e in db.TimeEntries
            join p in db.Projects on e.ProjectId equals p.Id
            join u in db.Users on e.UserId equals u.Id
            where e.Status == TimeEntryStatus.Approved && e.WorkDate >= periodFrom && e.WorkDate <= periodTo
            select new { e.Id, e.Hours, e.CostRateSnapshot, ProjectCode = p.Code, u.Username, e.WorkDate }
        ).ToListAsync(ct);

        var allIds = rows.Select(r => r.Id.ToString()).ToList();
        var done = allIds.Count == 0
            ? new HashSet<string>()
            : (await db.IntegrationLogs
                .Where(l => l.System == SystemName && l.Direction == IntegrationDirection.Outbound
                            && l.Status == "ok" && l.ExternalId != null && allIds.Contains(l.ExternalId))
                .Select(l => l.ExternalId!).ToListAsync(ct)).ToHashSet();

        var newRows = rows.Where(r => !done.Contains(r.Id.ToString())).ToList();

        // Симулированный шлюз принимает все строки без ошибок.
        var accepted = newRows.Count;
        foreach (var r in newRows)
        {
            var cost = r.Hours * (r.CostRateSnapshot ?? 0m);
            var payload = JsonSerializer.Serialize(new
            {
                project_code = r.ProjectCode,
                user = r.Username,
                date = r.WorkDate.ToString("yyyy-MM-dd"),
                hours = r.Hours.ToString(CultureInfo.InvariantCulture),
                cost = cost.ToString(CultureInfo.InvariantCulture),
                simulated = true,
            });
            db.IntegrationLogs.Add(new IntegrationLog
            {
                Direction = IntegrationDirection.Outbound, System = SystemName, ExternalId = r.Id.ToString(),
                Payload = JsonDocument.Parse(payload), Status = "ok", Error = null,
            });
        }
        await db.SaveChangesAsync(ct);

        return new
        {
            period = new[] { periodFrom.ToString("yyyy-MM-dd"), periodTo.ToString("yyyy-MM-dd") },
            total_approved = rows.Count,
            already_exported = rows.Count - newRows.Count,
            exported_now = newRows.Count,
            accepted,
            errors = Array.Empty<string>(),
            mode = "simulated",
        };
    }
}
