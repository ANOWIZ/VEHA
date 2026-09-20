using System.ComponentModel.DataAnnotations;
using Veha.Domain.Enums;

namespace Veha.Api.Dtos;

// Порт schemas/risk.py. Матрица 3×3: probability×impact (1–3), score 1–9,
// level low/medium/high. Риски не содержат финансовых полей.

public class RiskCreateDto : IValidatableObject
{
    [Required, StringLength(255, MinimumLength = 1)]
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public RiskCategory Category { get; set; } = RiskCategory.Technical;
    public int Probability { get; set; } = 1;
    public int Impact { get; set; } = 1;
    public RiskResponse ResponseStrategy { get; set; } = RiskResponse.Mitigate;
    public string? MitigationPlan { get; set; }
    public Guid? OwnerId { get; set; }
    public DateOnly? DueDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext _)
    {
        if (Probability is < 1 or > 3) yield return new ValidationResult("Вероятность — от 1 до 3", [nameof(Probability)]);
        if (Impact is < 1 or > 3) yield return new ValidationResult("Влияние — от 1 до 3", [nameof(Impact)]);
    }
}

public class RiskUpdateDto : IValidatableObject
{
    [StringLength(255, MinimumLength = 1)]
    public string? Title { get; set; }
    public string? Description { get; set; }
    public RiskCategory? Category { get; set; }
    public int? Probability { get; set; }
    public int? Impact { get; set; }
    public RiskStatus? Status { get; set; }
    public RiskResponse? ResponseStrategy { get; set; }
    public string? MitigationPlan { get; set; }
    public Guid? OwnerId { get; set; }
    public DateOnly? DueDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext _)
    {
        if (Probability is < 1 or > 3) yield return new ValidationResult("Вероятность — от 1 до 3", [nameof(Probability)]);
        if (Impact is < 1 or > 3) yield return new ValidationResult("Влияние — от 1 до 3", [nameof(Impact)]);
    }
}

public record RiskOutDto(
    Guid Id, Guid ProjectId, string Title, string? Description, RiskCategory Category,
    int Probability, int Impact, int Score, RiskStatus Status, RiskResponse ResponseStrategy,
    string? MitigationPlan, Guid? OwnerId, DateOnly? DueDate, DateTimeOffset? ClosedAt,
    Guid? CreatedBy, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string Level);

// --- Портфель рисков ---
public record RiskMatrixCellDto(int Probability, int Impact, int Score, string Level, int Count);

public record RiskCategoryCountDto(RiskCategory Category, int Count);

public record ProjectRiskRowDto(
    Guid ProjectId, string Code, string Name, Stage Stage, ProjectStatus Status, Guid ManagerId,
    int ActiveCount, int HighCount, int TopScore);

public record RiskPortfolioResponseDto(
    int ProjectsCount, int TotalActive, int TotalHigh,
    List<RiskMatrixCellDto> Matrix, List<RiskCategoryCountDto> ByCategory, List<ProjectRiskRowDto> Rows);
