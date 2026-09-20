using Veha.Domain.Enums;

namespace Veha.Domain.Entities;

/// <summary>Агрегат недели пользователя для пакетной отправки/утверждения таймшита.
/// Уникальность по (user, week).</summary>
public class TimesheetWeek : BaseEntity
{
    public Guid UserId { get; set; }
    public DateOnly WeekStart { get; set; }   // понедельник
    public TimeEntryStatus Status { get; set; } = TimeEntryStatus.Draft;
}
