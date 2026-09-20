namespace Veha.Domain.Entities;

/// <summary>In-app уведомление пользователю (email/Telegram — поверх той же модели).</summary>
public class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    public string Kind { get; set; } = "";   // timesheet_rejected / action_assigned / ...
    public string Title { get; set; } = "";
    public string? Body { get; set; }
    public string? Link { get; set; }
    public bool IsRead { get; set; }
}
