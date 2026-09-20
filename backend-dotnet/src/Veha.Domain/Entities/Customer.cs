using Veha.Domain.Enums;

namespace Veha.Domain.Entities;

/// <summary>Ожидание работ от Заказчика (блокер). Status=Waiting → проект «стоит»
/// на стороне Заказчика; указан ответственный с его стороны.</summary>
public class CustomerActionItem : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Stage? Stage { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }

    public string ResponsibleName { get; set; } = "";
    public string? ResponsibleEmail { get; set; }
    public Guid? ResponsibleUserId { get; set; }

    public DateTimeOffset? DueDate { get; set; }
    public CustomerActionStatus Status { get; set; } = CustomerActionStatus.Waiting;
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? ProvidedAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public string? ProvidedNote { get; set; }

    public List<Artifact> Artifacts { get; set; } = [];
}

/// <summary>Файл-артефакт (свидетельства, документы), загруженный через портал.</summary>
public class Artifact : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid? ActionItemId { get; set; }
    public CustomerActionItem? ActionItem { get; set; }
    public string Filename { get; set; } = "";
    public string? ContentType { get; set; }
    public int SizeBytes { get; set; }
    public string StoragePath { get; set; } = "";
    public Guid? UploadedBy { get; set; }
}
