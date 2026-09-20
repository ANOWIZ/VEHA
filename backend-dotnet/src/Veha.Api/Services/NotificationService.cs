using Microsoft.EntityFrameworkCore;
using Veha.Api.Dtos;
using Veha.Domain.Entities;
using Veha.Infrastructure;

namespace Veha.Api.Services;

/// <summary>In-app уведомления. Порт services/notification_service.py.</summary>
public class NotificationService(VehaDbContext db)
{
    public async Task<Notification> NotifyAsync(
        Guid userId, string kind, string title, string? body, string? link, CancellationToken ct = default)
    {
        var n = new Notification { UserId = userId, Kind = kind, Title = title, Body = body, Link = link };
        db.Notifications.Add(n);
        await db.SaveChangesAsync(ct);
        return n;
    }

    public async Task<List<NotificationOutDto>> ListForAsync(
        Guid userId, bool unreadOnly, int limit, CancellationToken ct)
    {
        var q = db.Notifications.Where(n => n.UserId == userId);
        if (unreadOnly) q = q.Where(n => !n.IsRead);
        return (await q.OrderByDescending(n => n.CreatedAt).Take(limit).ToListAsync(ct))
            .Select(n => new NotificationOutDto(n.Id, n.Kind, n.Title, n.Body, n.Link, n.IsRead, n.CreatedAt))
            .ToList();
    }

    public Task<int> UnreadCountAsync(Guid userId, CancellationToken ct)
        => db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);

    public Task MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct)
        => db.Notifications
            .Where(n => n.Id == notificationId && n.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);

    public Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct)
        => db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
}
