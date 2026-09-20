using System.ComponentModel.DataAnnotations;
using Veha.Domain.Enums;

namespace Veha.Api.Dtos;

// Порт schemas/timesheet.py. Часы > 0 и <= 24 (шаг 0.25 и суточный лимит — в сервисе).

public class TimeEntryCreateDto : IValidatableObject
{
    [Required] public Guid ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    [Required] public DateOnly WorkDate { get; set; }
    public decimal Hours { get; set; }

    [Required, StringLength(2000, MinimumLength = 1)]
    public string Comment { get; set; } = "";

    public IEnumerable<ValidationResult> Validate(ValidationContext _)
    {
        if (Hours is <= 0 or > 24)
            yield return new ValidationResult("Часы должны быть в диапазоне (0; 24]", [nameof(Hours)]);
    }
}

public class TimeEntryUpdateDto : IValidatableObject
{
    public Guid? TaskId { get; set; }
    public decimal? Hours { get; set; }

    [StringLength(2000, MinimumLength = 1)]
    public string? Comment { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext _)
    {
        if (Hours is <= 0 or > 24)
            yield return new ValidationResult("Часы должны быть в диапазоне (0; 24]", [nameof(Hours)]);
    }
}

public record TimeEntryOutDto(
    Guid Id,
    Guid UserId,
    Guid ProjectId,
    Guid? TaskId,
    DateOnly WorkDate,
    decimal Hours,
    string Comment,
    TimeEntryStatus Status,
    Guid? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    string? RejectReason,
    Guid? ReversalOf);

public record WeekResponseDto(
    DateOnly WeekStart,
    DateOnly WeekEnd,
    TimeEntryStatus Status,
    List<TimeEntryOutDto> Entries,
    decimal TotalHours,
    Dictionary<string, decimal> DailyTotals);

public class SubmitWeekRequestDto
{
    [Required] public DateOnly WeekStart { get; set; }
}

public class CopyWeekRequestDto
{
    [Required] public DateOnly TargetWeekStart { get; set; }
    [Required] public DateOnly SourceWeekStart { get; set; }
}

public class ApprovalDecisionDto
{
    [Required, MinLength(1)] public List<Guid> EntryIds { get; set; } = [];
    public string? Reason { get; set; }
}

/// <summary>Запись на утверждение + контекст (имя пользователя, код проекта).</summary>
public record PendingEntryDto(
    Guid Id,
    Guid UserId,
    Guid ProjectId,
    Guid? TaskId,
    DateOnly WorkDate,
    decimal Hours,
    string Comment,
    TimeEntryStatus Status,
    Guid? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    string? RejectReason,
    Guid? ReversalOf,
    string? UserName,
    string? ProjectCode);
