using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Veha.Api.Auth;
using Veha.Api.Dtos;
using Veha.Api.Services;

namespace Veha.Api.Controllers;

/// <summary>Уведомления текущего пользователя. Порт api/v1/notifications.py.</summary>
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController(CurrentUserAccessor current, NotificationService notifications) : ControllerBase
{
    [HttpGet]
    public async Task<List<NotificationOutDto>> List(
        [FromQuery(Name = "unread_only")] bool unreadOnly = false, CancellationToken ct = default)
    {
        var user = await current.GetAsync(ct);
        return await notifications.ListForAsync(user.Id, unreadOnly, 50, ct);
    }

    [HttpGet("unread-count")]
    public async Task<UnreadCountDto> UnreadCount(CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        return new UnreadCountDto(await notifications.UnreadCountAsync(user.Id, ct));
    }

    [HttpPost("{notificationId:guid}/read")]
    public async Task<MessageDto> MarkRead(Guid notificationId, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        await notifications.MarkReadAsync(user.Id, notificationId, ct);
        return new MessageDto("Отмечено прочитанным");
    }

    [HttpPost("read-all")]
    public async Task<MessageDto> MarkAllRead(CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        var n = await notifications.MarkAllReadAsync(user.Id, ct);
        return new MessageDto($"Прочитано: {n}");
    }
}
