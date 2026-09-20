using System.ComponentModel.DataAnnotations;

namespace Veha.Api.Dtos;

// Порт schemas/user.py. Финансовое поле current_cost_rate — только в UserWithRate
// и только для ролей с доступом к финансам (заполняется в контроллере).

/// <summary>Безопасное представление пользователя без финансов (UserPublic).</summary>
public record UserPublicDto(
    Guid Id,
    string Username,
    string Email,
    string FullName,
    string? Department,
    string? Position,
    string? Grade,
    bool IsActive,
    List<string> Roles);

/// <summary>Пользователь + действующая ставка (UserWithRate). current_cost_rate =
/// null для ролей без доступа к финансам.</summary>
public record UserWithRateDto(
    Guid Id,
    string Username,
    string Email,
    string FullName,
    string? Department,
    string? Position,
    string? Grade,
    bool IsActive,
    List<string> Roles,
    decimal? CurrentCostRate);

/// <summary>Версия ставки себестоимости в ответе (CostRateOut).</summary>
public record CostRateOutDto(Guid Id, Guid UserId, decimal CostRate, DateOnly ValidFrom, DateOnly? ValidTo);

/// <summary>Тело назначения ставки (CostRateCreate). cost_rate &gt; 0; valid_to &gt;= valid_from.</summary>
public class CostRateCreateDto : IValidatableObject
{
    public decimal CostRate { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CostRate <= 0)
            yield return new ValidationResult("Ставка должна быть больше 0", [nameof(CostRate)]);
        // Перевёрнутый интервал сделал бы ставку невыбираемой (см. _check_dates).
        if (ValidTo is not null && ValidTo < ValidFrom)
            yield return new ValidationResult(
                "Дата окончания ставки не может быть раньше даты начала", [nameof(ValidTo)]);
    }
}
