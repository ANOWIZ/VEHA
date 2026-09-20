using System.ComponentModel.DataAnnotations;
using Veha.Domain.Enums;

namespace Veha.Api.Dtos;

// Порт schemas/project.py. Поле типа проекта — "type" (не project_type). bill_rate
// участника маскируется для ролей без финансов (в контроллере).

public class ProjectCreateDto : IValidatableObject
{
    [Required, StringLength(255, MinimumLength = 1)]
    public string Name { get; set; } = "";

    [Required] public Guid ClientId { get; set; }
    [Required] public ProjectType Type { get; set; }
    [Required] public Guid ManagerId { get; set; }
    public Guid? CuratorId { get; set; }
    public DateOnly? PlannedStart { get; set; }
    public DateOnly? PlannedEnd { get; set; }
    public decimal BudgetRevenue { get; set; }
    public string? ContractRef { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext _)
    {
        if (BudgetRevenue < 0)
            yield return new ValidationResult("Бюджет не может быть отрицательным", [nameof(BudgetRevenue)]);
    }
}

public class ProjectUpdateDto : IValidatableObject
{
    [StringLength(255, MinimumLength = 1)]
    public string? Name { get; set; }

    public ProjectType? Type { get; set; }
    public Guid? ManagerId { get; set; }
    public Guid? CuratorId { get; set; }
    public ProjectStatus? Status { get; set; }
    public DateOnly? PlannedStart { get; set; }
    public DateOnly? PlannedEnd { get; set; }
    public DateOnly? ActualStart { get; set; }
    public DateOnly? ActualEnd { get; set; }
    public decimal? BudgetRevenue { get; set; }
    public string? ContractRef { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext _)
    {
        if (BudgetRevenue is < 0)
            yield return new ValidationResult("Бюджет не может быть отрицательным", [nameof(BudgetRevenue)]);
    }
}

/// <summary>Проект в ответе (ProjectOut). Class (не record) — чтобы ProjectDetailDto
/// наследовал поля без дублирования позиционных параметров.</summary>
public class ProjectOutDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public Guid ClientId { get; set; }
    public ProjectType Type { get; set; }
    public Guid ManagerId { get; set; }
    public Guid? CuratorId { get; set; }
    public Stage Stage { get; set; }
    public ProjectStatus Status { get; set; }
    public DateOnly? PlannedStart { get; set; }
    public DateOnly? PlannedEnd { get; set; }
    public DateOnly? ActualStart { get; set; }
    public DateOnly? ActualEnd { get; set; }
    public decimal BudgetRevenue { get; set; }
    public string? ContractRef { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Деталь проекта (ProjectDetail = ProjectOut + участники + вехи).</summary>
public class ProjectDetailDto : ProjectOutDto
{
    public List<MemberOutDto> Members { get; set; } = [];
    public List<MilestoneOutDto> Milestones { get; set; } = [];
}

public class MemberCreateDto : IValidatableObject
{
    [Required] public Guid UserId { get; set; }
    [Required] public ProjectMemberRole Role { get; set; }
    public decimal BillRate { get; set; }
    public DateOnly? PeriodFrom { get; set; }
    public DateOnly? PeriodTo { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext _)
    {
        if (BillRate < 0)
            yield return new ValidationResult("Ставка не может быть отрицательной", [nameof(BillRate)]);
    }
}

/// <summary>Участник в ответе (MemberOut). bill_rate = null для ролей без финансов.</summary>
public record MemberOutDto(
    Guid Id, Guid UserId, ProjectMemberRole Role, decimal? BillRate, DateOnly? PeriodFrom, DateOnly? PeriodTo);

public class MilestoneCreateDto : IValidatableObject
{
    [Required, StringLength(255, MinimumLength = 1)]
    public string Name { get; set; } = "";

    [Required] public DateOnly MilestoneDate { get; set; }
    public bool IsPayment { get; set; }
    public decimal Amount { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext _)
    {
        if (Amount < 0)
            yield return new ValidationResult("Сумма не может быть отрицательной", [nameof(Amount)]);
    }
}

public record MilestoneOutDto(Guid Id, string Name, DateOnly MilestoneDate, bool IsPayment, decimal Amount);

public class StageChangeRequestDto
{
    [Required] public Stage ToStage { get; set; }
    public string? Reason { get; set; }
}

public record StageTransitionOutDto(
    Guid Id, Stage? FromStage, Stage ToStage, string? Reason, Guid? CreatedBy, DateTimeOffset CreatedAt);
