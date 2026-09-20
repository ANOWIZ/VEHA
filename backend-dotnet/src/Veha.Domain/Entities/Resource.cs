namespace Veha.Domain.Entities;

/// <summary>Плановые часы по пользователю/проекту/неделе (понедельник).
/// Уникальность по (user, project, week) — upsert ячейки тепловой карты.</summary>
public class ResourcePlan : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid ProjectId { get; set; }
    public DateOnly WeekStart { get; set; }
    public decimal PlannedHours { get; set; }
}
