using Veha.Domain.Enums;

namespace Veha.Domain.Entities;

/// <summary>Списание часов. После утверждения запись блокируется; корректировка —
/// сторнирующей записью (ReversalOf), не редактированием. Себестоимость фиксируется
/// снимком на дату записи при утверждении.</summary>
public class TimeEntry : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public DateOnly WorkDate { get; set; }
    public decimal Hours { get; set; }
    public string Comment { get; set; } = "";  // комментарий обязателен
    public TimeEntryStatus Status { get; set; } = TimeEntryStatus.Draft;

    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? RejectReason { get; set; }

    /// <summary>Снимок себестоимости часа на дату записи (фиксируется при утверждении).</summary>
    public decimal? CostRateSnapshot { get; set; }

    /// <summary>Сторно: ссылка на исходную утверждённую запись.</summary>
    public Guid? ReversalOf { get; set; }
}
