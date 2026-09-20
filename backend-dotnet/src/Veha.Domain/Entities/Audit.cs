using System.Text.Json;
using Veha.Domain.Enums;

namespace Veha.Domain.Entities;

/// <summary>Аудит: кто, что, когда, diff (JSON). Project, Quote, TimeEntry,
/// ставки и финансы (CLAUDE.md §7).</summary>
public class AuditLog : BaseEntity
{
    public string Entity { get; set; } = "";
    public Guid? EntityId { get; set; }
    public AuditAction Action { get; set; }
    public Guid? ActorId { get; set; }
    public JsonDocument? Diff { get; set; }
}

/// <summary>Журнал интеграционных обменов. Идемпотентность по (system, external_id).</summary>
public class IntegrationLog : BaseEntity
{
    public IntegrationDirection Direction { get; set; }
    public string System { get; set; } = "";   // onec, cbr, keycloak
    public string? ExternalId { get; set; }
    public JsonDocument? Payload { get; set; }
    public string Status { get; set; } = "ok";
    public string? Error { get; set; }
}
