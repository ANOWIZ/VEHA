using System.ComponentModel.DataAnnotations;

namespace Veha.Api.Dtos;

// Порт schemas/resource.py. Загрузка: over >100% нормы, under <70%, ok иначе.

public class ResourcePlanUpsertDto : IValidatableObject
{
    [Required] public Guid UserId { get; set; }
    [Required] public Guid ProjectId { get; set; }
    [Required] public DateOnly WeekStart { get; set; }
    public decimal PlannedHours { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext _)
    {
        if (PlannedHours is < 0 or > 168)
            yield return new ValidationResult("Плановые часы недели — от 0 до 168", [nameof(PlannedHours)]);
    }
}

public record ResourcePlanOutDto(Guid Id, Guid UserId, Guid ProjectId, DateOnly WeekStart, decimal PlannedHours);

public record HeatmapCellDto(DateOnly WeekStart, decimal PlannedHours, decimal UtilizationPct, string Load);

public record HeatmapRowDto(Guid UserId, string UserName, List<HeatmapCellDto> Cells, decimal TotalHours);

public record HeatmapResponseDto(List<DateOnly> Weeks, decimal NormHours, List<HeatmapRowDto> Rows);
