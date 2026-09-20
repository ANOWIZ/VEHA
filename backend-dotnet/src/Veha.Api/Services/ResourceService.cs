using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Veha.Api.Config;
using Veha.Api.Dtos;
using Veha.Domain.Entities;
using Veha.Domain.Rules;
using Veha.Infrastructure;

namespace Veha.Api.Services;

/// <summary>Ресурсное планирование: план загрузки (upsert ячейки) и тепловая карта.
/// Порт services/resource_service.py. Норма недели — из настроек.</summary>
public class ResourceService(VehaDbContext db, IOptions<VehaSettings> settings)
{
    private static DateOnly Monday(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    public async Task<ResourcePlanOutDto> UpsertPlanAsync(ResourcePlanUpsertDto dto, CancellationToken ct)
    {
        var week = Monday(dto.WeekStart);
        var cell = await db.ResourcePlans.FirstOrDefaultAsync(
            c => c.UserId == dto.UserId && c.ProjectId == dto.ProjectId && c.WeekStart == week, ct);
        if (cell is null)
        {
            cell = new ResourcePlan { UserId = dto.UserId, ProjectId = dto.ProjectId, WeekStart = week, PlannedHours = dto.PlannedHours };
            db.ResourcePlans.Add(cell);
        }
        else
        {
            cell.PlannedHours = dto.PlannedHours;
        }
        await db.SaveChangesAsync(ct);
        return new ResourcePlanOutDto(cell.Id, cell.UserId, cell.ProjectId, cell.WeekStart, cell.PlannedHours);
    }

    public async Task<HeatmapResponseDto> HeatmapAsync(DateOnly weekFrom, int weeks, CancellationToken ct)
    {
        var start = Monday(weekFrom);
        var weekList = Enumerable.Range(0, weeks).Select(i => start.AddDays(7 * i)).ToList();
        var weekTo = weekList[^1];
        var norm = settings.Value.DefaultWeekNormHours;

        var plans = await db.ResourcePlans.Where(p => p.WeekStart >= start && p.WeekStart <= weekTo).ToListAsync(ct);

        // Агрегируем часы по (user, week) суммарно по всем проектам.
        var agg = new Dictionary<Guid, Dictionary<DateOnly, decimal>>();
        foreach (var p in plans)
        {
            if (!agg.TryGetValue(p.UserId, out var byWeek)) { byWeek = new(); agg[p.UserId] = byWeek; }
            byWeek[p.WeekStart] = byWeek.GetValueOrDefault(p.WeekStart) + p.PlannedHours;
        }

        var userIds = agg.Keys.ToList();
        var names = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var rows = new List<HeatmapRowDto>();
        foreach (var (userId, byWeek) in agg)
        {
            var cells = new List<HeatmapCellDto>();
            decimal total = 0;
            foreach (var wk in weekList)
            {
                var hours = byWeek.GetValueOrDefault(wk);
                total += hours;
                var util = FinanceCalc.UtilizationPct(hours, norm);
                var load = util > 100 ? "over" : util < 70 ? "under" : "ok";
                cells.Add(new HeatmapCellDto(wk, hours, util, load));
            }
            rows.Add(new HeatmapRowDto(userId, names.GetValueOrDefault(userId, "—"), cells, total));
        }
        rows = rows.OrderBy(r => r.UserName).ToList();

        return new HeatmapResponseDto(weekList, norm, rows);
    }
}
