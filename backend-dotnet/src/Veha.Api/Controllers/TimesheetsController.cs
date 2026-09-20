using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Veha.Api.Auth;
using Veha.Api.Dtos;
using Veha.Api.Services;

namespace Veha.Api.Controllers;

/// <summary>Трудозатраты: недельная сетка, ввод, отправка, утверждение, сторно.
/// Ввод — участник проекта; утверждение/сторно — pm/admin. Порт api/v1/timesheets.py.</summary>
[ApiController]
[Route("api/v1/timesheets")]
[Authorize]
public class TimesheetsController(CurrentUserAccessor current, TimesheetService timesheets) : ControllerBase
{
    [HttpGet("week")]
    public async Task<WeekResponseDto> Week(
        [FromQuery(Name = "week_start")] DateOnly? weekStart, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        var start = weekStart ?? DateOnly.FromDateTime(DateTime.UtcNow);
        return await timesheets.GetWeekAsync(user, start, ct);
    }

    [HttpPost("entries")]
    public async Task<IActionResult> CreateEntry(TimeEntryCreateDto body, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        return StatusCode(StatusCodes.Status201Created, await timesheets.CreateEntryAsync(user, body, ct));
    }

    [HttpPatch("entries/{entryId:guid}")]
    public async Task<TimeEntryOutDto> UpdateEntry(Guid entryId, TimeEntryUpdateDto body, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        return await timesheets.UpdateEntryAsync(user, entryId, body, ct);
    }

    [HttpDelete("entries/{entryId:guid}")]
    public async Task<MessageDto> DeleteEntry(Guid entryId, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await timesheets.DeleteEntryAsync(user, entryId, ct);
        return new MessageDto("Запись удалена");
    }

    [HttpPost("submit")]
    public async Task<MessageDto> Submit(SubmitWeekRequestDto body, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        var n = await timesheets.SubmitWeekAsync(user, body.WeekStart, ct);
        return new MessageDto($"Отправлено на утверждение записей: {n}");
    }

    [HttpPost("copy")]
    public async Task<MessageDto> Copy(CopyWeekRequestDto body, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        var n = await timesheets.CopyWeekAsync(user, body.SourceWeekStart, body.TargetWeekStart, ct);
        return new MessageDto($"Скопировано записей: {n}");
    }

    // ---------- Утверждение (РП / admin) ----------
    [HttpGet("pending")]
    [Authorize(Roles = "pm,admin")]
    public async Task<List<PendingEntryDto>> Pending(CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        return await timesheets.PendingForAsync(user, ct);
    }

    [HttpPost("approve")]
    [Authorize(Roles = "pm,admin")]
    public async Task<MessageDto> Approve(ApprovalDecisionDto body, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        var n = await timesheets.ApproveAsync(user, body.EntryIds, ct);
        return new MessageDto($"Утверждено записей: {n}");
    }

    [HttpPost("reject")]
    [Authorize(Roles = "pm,admin")]
    public async Task<MessageDto> Reject(ApprovalDecisionDto body, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        var n = await timesheets.RejectAsync(user, body.EntryIds, body.Reason, ct);
        return new MessageDto($"Отклонено записей: {n}");
    }

    [HttpPost("entries/{entryId:guid}/reverse")]
    [Authorize(Roles = "pm,admin")]
    public async Task<TimeEntryOutDto> Reverse(Guid entryId, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        return await timesheets.ReverseAsync(user, entryId, ct);
    }
}
