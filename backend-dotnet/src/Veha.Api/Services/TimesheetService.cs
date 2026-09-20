using Microsoft.EntityFrameworkCore;
using Veha.Api.Dtos;
using Veha.Domain.Common;
using Veha.Domain.Entities;
using Veha.Domain.Enums;
using Veha.Domain.Rules;
using Veha.Infrastructure;

namespace Veha.Api.Services;

/// <summary>Трудозатраты: недельная сетка, ввод, отправка, утверждение, сторно.
/// Порт services/timesheet_service.py. Себестоимость фиксируется снимком ставки на
/// дату записи при утверждении; корректировка утверждённого — сторно, не правка.</summary>
public class TimesheetService(VehaDbContext db, AuditService audit, NotificationService notifications)
{
    private static readonly TimeEntryStatus[] Editable = [TimeEntryStatus.Draft, TimeEntryStatus.Rejected];

    private static DateOnly MondayOf(DateOnly d)
        => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    // ---------- Недельная сетка ----------
    public async Task<WeekResponseDto> GetWeekAsync(User user, DateOnly weekStart, CancellationToken ct)
    {
        var start = MondayOf(weekStart);
        var end = start.AddDays(6);
        var entries = await db.TimeEntries
            .Where(e => e.UserId == user.Id && e.WorkDate >= start && e.WorkDate <= end)
            .OrderBy(e => e.WorkDate)
            .ToListAsync(ct);
        var week = await db.TimesheetWeeks.FirstOrDefaultAsync(w => w.UserId == user.Id && w.WeekStart == start, ct);

        decimal total = 0;
        var daily = new Dictionary<string, decimal>();
        foreach (var e in entries)
        {
            total += e.Hours;
            var key = e.WorkDate.ToString("yyyy-MM-dd");
            daily[key] = daily.GetValueOrDefault(key) + e.Hours;
        }

        return new WeekResponseDto(start, end, week?.Status ?? TimeEntryStatus.Draft,
            entries.Select(ToOut).ToList(), total, daily);
    }

    // ---------- Ввод ----------
    private async Task EnsureCanLogAsync(User user, Guid projectId, CancellationToken ct)
    {
        var member = await db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == user.Id, ct);
        if (member) return;
        var lead = await db.Projects.AnyAsync(
            p => p.Id == projectId && (p.ManagerId == user.Id || p.CuratorId == user.Id), ct);
        if (!lead)
            throw new ForbiddenException("Вы не участник проекта — списание часов недоступно");
    }

    public async Task<TimeEntryOutDto> CreateEntryAsync(User user, TimeEntryCreateDto dto, CancellationToken ct)
    {
        await EnsureCanLogAsync(user, dto.ProjectId, ct);
        try { TimesheetRules.ValidateHours(dto.Hours); }
        catch (DomainValidationException ex) { throw new TimesheetInvalidHoursException(ex.Message); }

        var existing = await DailyTotalAsync(user.Id, dto.WorkDate, null, ct);
        try { TimesheetRules.ValidateDailyTotal(existing, dto.Hours); }
        catch (DomainValidationException ex) { throw new TimesheetDailyLimitException(ex.Message); }

        var entry = new TimeEntry
        {
            UserId = user.Id, ProjectId = dto.ProjectId, TaskId = dto.TaskId,
            WorkDate = dto.WorkDate, Hours = dto.Hours, Comment = dto.Comment,
            Status = TimeEntryStatus.Draft,
        };
        db.TimeEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return ToOut(entry);
    }

    private async Task<TimeEntry> OwnEditableAsync(User user, Guid entryId, CancellationToken ct)
    {
        var entry = await db.TimeEntries.FirstOrDefaultAsync(e => e.Id == entryId, ct)
                    ?? throw new NotFoundException("Запись не найдена");
        if (entry.UserId != user.Id)
            throw new ForbiddenException("Можно редактировать только свои записи");
        if (!Editable.Contains(entry.Status))
            throw new TimesheetLockedException(
                "Запись отправлена или утверждена — редактирование запрещено (используйте сторно для утверждённых)");
        return entry;
    }

    public async Task<TimeEntryOutDto> UpdateEntryAsync(User user, Guid entryId, TimeEntryUpdateDto dto, CancellationToken ct)
    {
        var entry = await OwnEditableAsync(user, entryId, ct);
        if (dto.Hours is not null)
        {
            var newHours = dto.Hours.Value;
            try { TimesheetRules.ValidateHours(newHours); }
            catch (DomainValidationException ex) { throw new TimesheetInvalidHoursException(ex.Message); }
            var existing = await DailyTotalAsync(user.Id, entry.WorkDate, entry.Id, ct);
            try { TimesheetRules.ValidateDailyTotal(existing, newHours); }
            catch (DomainValidationException ex) { throw new TimesheetDailyLimitException(ex.Message); }
            entry.Hours = newHours;
        }
        if (dto.TaskId is not null) entry.TaskId = dto.TaskId;
        if (dto.Comment is not null) entry.Comment = dto.Comment;
        // Повторная правка после отклонения возвращает в черновик.
        if (entry.Status == TimeEntryStatus.Rejected)
        {
            entry.Status = TimeEntryStatus.Draft;
            entry.RejectReason = null;
        }
        await db.SaveChangesAsync(ct);
        return ToOut(entry);
    }

    public async Task DeleteEntryAsync(User user, Guid entryId, CancellationToken ct)
    {
        var entry = await OwnEditableAsync(user, entryId, ct);
        entry.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    // ---------- Отправка ----------
    public async Task<int> SubmitWeekAsync(User user, DateOnly weekStart, CancellationToken ct)
    {
        var start = MondayOf(weekStart);
        var end = start.AddDays(6);
        var drafts = await db.TimeEntries
            .Where(e => e.UserId == user.Id && e.WorkDate >= start && e.WorkDate <= end
                        && e.Status == TimeEntryStatus.Draft)
            .ToListAsync(ct);
        if (drafts.Count == 0)
            throw new DomainValidationException("Нет черновиков для отправки на этой неделе");

        foreach (var e in drafts) e.Status = TimeEntryStatus.Submitted;

        var week = await db.TimesheetWeeks.FirstOrDefaultAsync(w => w.UserId == user.Id && w.WeekStart == start, ct);
        if (week is null)
            db.TimesheetWeeks.Add(new TimesheetWeek { UserId = user.Id, WeekStart = start, Status = TimeEntryStatus.Submitted });
        else
            week.Status = TimeEntryStatus.Submitted;

        await db.SaveChangesAsync(ct);
        return drafts.Count;
    }

    public async Task<int> CopyWeekAsync(User user, DateOnly sourceWeekStart, DateOnly targetWeekStart, CancellationToken ct)
    {
        var src = MondayOf(sourceWeekStart);
        var tgt = MondayOf(targetWeekStart);
        if (src == tgt)
            throw new DomainValidationException("Неделя-источник совпадает с целевой");

        var srcEntries = await db.TimeEntries
            .Where(e => e.UserId == user.Id && e.WorkDate >= src && e.WorkDate <= src.AddDays(6))
            .ToListAsync(ct);
        if (srcEntries.Count == 0)
            throw new DomainValidationException("В неделе-источнике нет записей для копирования");

        var offset = tgt.DayNumber - src.DayNumber;
        var created = 0;
        foreach (var e in srcEntries)
        {
            var newDate = e.WorkDate.AddDays(offset);
            var existing = await DailyTotalAsync(user.Id, newDate, null, ct);
            if (existing + e.Hours > TimesheetRules.MaxDailyHours)
                continue;  // не превышаем суточный лимит — пропускаем
            db.TimeEntries.Add(new TimeEntry
            {
                UserId = user.Id, ProjectId = e.ProjectId, TaskId = e.TaskId,
                WorkDate = newDate, Hours = e.Hours, Comment = e.Comment, Status = TimeEntryStatus.Draft,
            });
            await db.SaveChangesAsync(ct);  // как per-create flush в Python: следующий daily_total видит копии
            created++;
        }
        return created;
    }

    // ---------- Утверждение ----------
    public async Task<List<PendingEntryDto>> PendingForAsync(User approver, CancellationToken ct)
    {
        var privileged = approver.Roles.Contains("admin");
        var q = db.TimeEntries.Where(e => e.Status == TimeEntryStatus.Submitted);
        if (!privileged)
            q = q.Where(e => db.Projects.Any(
                p => p.Id == e.ProjectId && (p.ManagerId == approver.Id || p.CuratorId == approver.Id)));
        var entries = await q.OrderBy(e => e.WorkDate).ToListAsync(ct);

        var userIds = entries.Select(e => e.UserId).Distinct().ToList();
        var projIds = entries.Select(e => e.ProjectId).Distinct().ToList();
        var names = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var codes = await db.Projects.Where(p => projIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Code, ct);

        return entries.Select(e => new PendingEntryDto(
            e.Id, e.UserId, e.ProjectId, e.TaskId, e.WorkDate, e.Hours, e.Comment, e.Status,
            e.ApprovedBy, e.ApprovedAt, e.RejectReason, e.ReversalOf,
            names.GetValueOrDefault(e.UserId, "—"), codes.GetValueOrDefault(e.ProjectId, "—"))).ToList();
    }

    private async Task AssertCanApproveAsync(User approver, TimeEntry entry, CancellationToken ct)
    {
        if (approver.Roles.Contains("admin")) return;
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == entry.ProjectId, ct)
                      ?? throw new NotFoundException("Проект записи не найден");
        if (project.ManagerId != approver.Id && project.CuratorId != approver.Id)
            throw new NotApproverException("Утверждать может только руководитель/куратор проекта");
    }

    public async Task<int> ApproveAsync(User approver, List<Guid> entryIds, CancellationToken ct)
    {
        var approved = 0;
        foreach (var eid in entryIds)
        {
            var entry = await db.TimeEntries.FirstOrDefaultAsync(e => e.Id == eid, ct);
            if (entry is null || entry.Status != TimeEntryStatus.Submitted) continue;
            await AssertCanApproveAsync(approver, entry, ct);
            var rate = await EffectiveRateAsync(entry.UserId, entry.WorkDate, ct);
            entry.Status = TimeEntryStatus.Approved;
            entry.ApprovedBy = approver.Id;
            entry.ApprovedAt = DateTimeOffset.UtcNow;
            entry.CostRateSnapshot = rate ?? 0m;
            approved++;
        }
        if (approved > 0)
            await audit.RecordAsync("TimeEntry", null, AuditAction.Approve, approver.Id,
                new { count = approved, ids = entryIds.Select(i => i.ToString()).ToArray() }, ct);
        return approved;
    }

    public async Task<int> RejectAsync(User approver, List<Guid> entryIds, string? reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainValidationException("Отклонение требует указания причины");

        var rejected = 0;
        foreach (var eid in entryIds)
        {
            var entry = await db.TimeEntries.FirstOrDefaultAsync(e => e.Id == eid, ct);
            if (entry is null || entry.Status != TimeEntryStatus.Submitted) continue;
            await AssertCanApproveAsync(approver, entry, ct);
            entry.Status = TimeEntryStatus.Rejected;
            entry.RejectReason = reason;
            await notifications.NotifyAsync(entry.UserId, "timesheet_rejected", "Трудозатраты отклонены",
                $"Запись от {entry.WorkDate:yyyy-MM-dd} отклонена: {reason}", "/timesheets", ct);
            rejected++;
        }
        if (rejected > 0)
            await audit.RecordAsync("TimeEntry", null, AuditAction.Reject, approver.Id,
                new { count = rejected, reason }, ct);
        return rejected;
    }

    public async Task<TimeEntryOutDto> ReverseAsync(User approver, Guid entryId, CancellationToken ct)
    {
        var entry = await db.TimeEntries.FirstOrDefaultAsync(e => e.Id == entryId, ct)
                    ?? throw new NotFoundException("Запись не найдена");
        if (entry.Status != TimeEntryStatus.Approved)
            throw new ConflictException("Сторнировать можно только утверждённую запись");
        await AssertCanApproveAsync(approver, entry, ct);

        var reversal = new TimeEntry
        {
            UserId = entry.UserId, ProjectId = entry.ProjectId, TaskId = entry.TaskId, WorkDate = entry.WorkDate,
            Hours = -entry.Hours,
            Comment = $"Сторно записи от {entry.WorkDate:yyyy-MM-dd}: {entry.Comment}",
            Status = TimeEntryStatus.Approved, ApprovedBy = approver.Id, ApprovedAt = DateTimeOffset.UtcNow,
            CostRateSnapshot = entry.CostRateSnapshot, ReversalOf = entry.Id,
        };
        db.TimeEntries.Add(reversal);
        await audit.RecordAsync("TimeEntry", entry.Id, AuditAction.Update, approver.Id,
            new { reversal = reversal.Id.ToString() }, ct);
        return ToOut(reversal);
    }

    // ---------- Вспомогательное ----------
    private async Task<decimal> DailyTotalAsync(Guid userId, DateOnly workDate, Guid? excludeId, CancellationToken ct)
    {
        var q = db.TimeEntries.Where(e => e.UserId == userId && e.WorkDate == workDate);
        if (excludeId is not null) q = q.Where(e => e.Id != excludeId);
        return await q.SumAsync(e => (decimal?)e.Hours, ct) ?? 0m;
    }

    private async Task<decimal?> EffectiveRateAsync(Guid userId, DateOnly onDate, CancellationToken ct)
    {
        var rate = await db.UserCostRates
            .Where(r => r.UserId == userId && r.ValidFrom <= onDate && (r.ValidTo == null || r.ValidTo >= onDate))
            .OrderByDescending(r => r.ValidFrom)
            .FirstOrDefaultAsync(ct);
        return rate?.CostRate;
    }

    private static TimeEntryOutDto ToOut(TimeEntry e) => new(
        e.Id, e.UserId, e.ProjectId, e.TaskId, e.WorkDate, e.Hours, e.Comment, e.Status,
        e.ApprovedBy, e.ApprovedAt, e.RejectReason, e.ReversalOf);
}
