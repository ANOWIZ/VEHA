using System.ComponentModel.DataAnnotations;
using Veha.Domain.Enums;

namespace Veha.Api.Dtos;

// Порт schemas/customer.py. Портал Заказчика: ожидания-блокеры + артефакты. Без финансов.

public record ArtifactOutDto(
    Guid Id, string Filename, string? ContentType, int SizeBytes, DateTimeOffset CreatedAt, Guid? UploadedBy);

public class ActionItemCreateDto
{
    [Required, StringLength(255, MinimumLength = 1)] public string Title { get; set; } = "";
    public string? Description { get; set; }
    public Stage? Stage { get; set; }
    [Required, StringLength(255, MinimumLength = 1)] public string ResponsibleName { get; set; } = "";
    [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Некорректный email")]
    public string? ResponsibleEmail { get; set; }
    public Guid? ResponsibleUserId { get; set; }
    public DateTimeOffset? DueDate { get; set; }
}

public class ActionItemProvideDto
{
    public string? Note { get; set; }
}

public record ActionItemOutDto(
    Guid Id, Guid ProjectId, Stage? Stage, string Title, string? Description,
    string ResponsibleName, string? ResponsibleEmail, Guid? ResponsibleUserId, DateTimeOffset? DueDate,
    CustomerActionStatus Status, DateTimeOffset? ProvidedAt, DateTimeOffset? AcceptedAt, string? ProvidedNote,
    DateTimeOffset CreatedAt, List<ArtifactOutDto> Artifacts);

public record PortalProjectDto(
    Guid ProjectId, string Code, string Name, Stage Stage, ProjectStatus Status,
    DateTimeOffset? PlannedEnd, int OpenActions, bool BlockedOnCustomer);

public record PortalProjectDetailDto(
    Guid ProjectId, string Code, string Name, Stage Stage, ProjectStatus Status,
    bool BlockedOnCustomer, List<ActionItemOutDto> ActionItems);
