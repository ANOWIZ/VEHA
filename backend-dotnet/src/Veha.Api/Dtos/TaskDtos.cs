using System.ComponentModel.DataAnnotations;
using Veha.Domain.Enums;
using TaskStatus = Veha.Domain.Enums.TaskStatus;

namespace Veha.Api.Dtos;

// Порт schemas/task.py. Иерархия этап→задача через parent_id; stage задачи независим
// от стадии проекта.

public class TaskCreateDto : IValidatableObject
{
    [Required, StringLength(255, MinimumLength = 1)]
    public string Name { get; set; } = "";

    public Guid? ParentId { get; set; }
    public Stage? Stage { get; set; }
    public decimal PlannedHours { get; set; }
    public Guid? AssigneeId { get; set; }
    public DateOnly? DueDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext _)
    {
        if (PlannedHours < 0)
            yield return new ValidationResult("Плановые часы не могут быть отрицательными", [nameof(PlannedHours)]);
    }
}

public class TaskUpdateDto : IValidatableObject
{
    [StringLength(255, MinimumLength = 1)]
    public string? Name { get; set; }

    public Stage? Stage { get; set; }
    public decimal? PlannedHours { get; set; }
    public Guid? AssigneeId { get; set; }
    public TaskStatus? Status { get; set; }
    public DateOnly? DueDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext _)
    {
        if (PlannedHours is < 0)
            yield return new ValidationResult("Плановые часы не могут быть отрицательными", [nameof(PlannedHours)]);
    }
}

public record TaskOutDto(
    Guid Id,
    Guid ProjectId,
    Guid? ParentId,
    string Name,
    Stage? Stage,
    decimal PlannedHours,
    Guid? AssigneeId,
    TaskStatus Status,
    DateOnly? DueDate);
