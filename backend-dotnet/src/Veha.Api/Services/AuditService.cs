using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Veha.Api.Dtos;
using Veha.Domain.Entities;
using Veha.Domain.Enums;
using Veha.Infrastructure;

namespace Veha.Api.Services;

/// <summary>Аудит изменений (Project, Quote, TimeEntry, ставки, финансы) — CLAUDE.md §7.
/// Порт services/audit_service.py. diff сериализуется в jsonb (snake_case ключи/enum).</summary>
public class AuditService(VehaDbContext db)
{
    private static readonly JsonSerializerOptions DiffOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public async Task RecordAsync(
        string entity,
        Guid? entityId,
        AuditAction action,
        Guid? actorId,
        object? diff = null,
        CancellationToken ct = default)
    {
        var json = diff is null ? "{}" : JsonSerializer.Serialize(diff, DiffOptions);
        db.AuditLogs.Add(new AuditLog
        {
            Entity = entity,
            EntityId = entityId,
            Action = action,
            ActorId = actorId,
            Diff = JsonDocument.Parse(json),
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Аудит-лог с пагинацией и обогащением именами акторов (порт admin.list_audit).</summary>
    public async Task<Page<AuditEntryOutDto>> ListAsync(
        string? entity, Guid? entityId, int limit, int offset, CancellationToken ct)
    {
        var q = db.AuditLogs.AsQueryable();
        if (!string.IsNullOrEmpty(entity)) q = q.Where(a => a.Entity == entity);
        if (entityId is not null) q = q.Where(a => a.EntityId == entityId);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(a => a.CreatedAt).Skip(offset).Take(limit).ToListAsync(ct);

        var actorIds = items.Where(a => a.ActorId != null).Select(a => a.ActorId!.Value).Distinct().ToList();
        var names = await db.Users.Where(u => actorIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var dtos = items.Select(a => new AuditEntryOutDto(
            a.Id, a.Entity, a.EntityId, a.Action, a.ActorId,
            a.Diff?.RootElement.Clone(), a.CreatedAt,
            a.ActorId is not null ? names.GetValueOrDefault(a.ActorId.Value, "—") : null)).ToList();

        return new Page<AuditEntryOutDto>(dtos, total, limit, offset);
    }
}
