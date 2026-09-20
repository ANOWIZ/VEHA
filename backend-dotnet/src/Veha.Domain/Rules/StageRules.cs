using Veha.Domain.Common;
using Veha.Domain.Enums;

namespace Veha.Domain.Rules;

/// <summary>Правила переходов по стадиям (порт stage_rules.py). Только вперёд на
/// одну стадию или откат на одну назад с указанием причины.</summary>
public static class StageRules
{
    public static readonly Stage[] Order =
    [
        Stage.Presale,
        Stage.Survey,
        Stage.Design,
        Stage.Implementation,
        Stage.Pilot,
        Stage.Support,
        Stage.Closed,
    ];

    public static int StageIndex(Stage stage) => Array.IndexOf(Order, stage);

    public static Stage? NextStage(Stage stage)
    {
        var i = StageIndex(stage);
        return i + 1 < Order.Length ? Order[i + 1] : null;
    }

    public static Stage? PrevStage(Stage stage)
    {
        var i = StageIndex(stage);
        return i > 0 ? Order[i - 1] : null;
    }

    public static bool IsForward(Stage from, Stage to) => StageIndex(to) - StageIndex(from) == 1;

    public static bool IsBackward(Stage from, Stage to) => StageIndex(from) - StageIndex(to) == 1;

    /// <summary>Бросает DomainValidationException, если переход недопустим.</summary>
    public static void ValidateTransition(Stage from, Stage to, string? reason)
    {
        if (from == to)
            throw new DomainValidationException("Стадия не изменилась");
        if (IsForward(from, to))
            return;
        if (IsBackward(from, to))
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new DomainValidationException("Откат на предыдущую стадию требует указания причины");
            return;
        }
        throw new DomainValidationException(
            "Недопустимый переход: разрешено только вперёд на одну стадию " +
            "или откат на одну назад с указанием причины");
    }
}
