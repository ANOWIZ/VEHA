using Veha.Domain.Enums;

namespace Veha.Domain.Entities;

/// <summary>Риск проекта. Оценка по матрице вероятность×влияние (1–3 каждый),
/// score = probability × impact (1–9) денормализован и пересчитывается сервисом.</summary>
public class Risk : BaseEntity
{
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public RiskCategory Category { get; set; } = RiskCategory.Technical;

    public int Probability { get; set; } = 1;
    public int Impact { get; set; } = 1;
    public int Score { get; set; } = 1;

    public RiskStatus Status { get; set; } = RiskStatus.Open;
    public RiskResponse ResponseStrategy { get; set; } = RiskResponse.Mitigate;
    public string? MitigationPlan { get; set; }

    public Guid? OwnerId { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public Guid? CreatedBy { get; set; }
}
