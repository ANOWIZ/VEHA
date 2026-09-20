using System.Text.Json;
using Veha.Domain.Enums;

namespace Veha.Api.Dtos;

// Порт schemas/audit.py. diff — произвольный JSON (jsonb).

public record AuditEntryOutDto(
    Guid Id, string Entity, Guid? EntityId, AuditAction Action, Guid? ActorId,
    JsonElement? Diff, DateTimeOffset CreatedAt, string? ActorName);
