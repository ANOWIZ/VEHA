using Veha.Domain.Common;

namespace Veha.Domain.Rules;

/// <summary>Оценка рисков (порт risk_rules.py). Матрица 3×3: вероятность×влияние
/// (1–3) = балл 1–9. Уровни: низкий 1–2, средний 3–4, высокий 6–9.</summary>
public static class RiskRules
{
    public const int MinLevel = 1;
    public const int MaxLevel = 3;
    public const int HighThreshold = 6;
    public const int MediumThreshold = 3;

    public static void ValidateScale(int value, string field)
    {
        if (value < MinLevel || value > MaxLevel)
            throw new DomainValidationException($"{field}: значение должно быть от {MinLevel} до {MaxLevel}");
    }

    /// <summary>Балл риска = вероятность × влияние (после валидации шкалы 1–3).</summary>
    public static int ComputeScore(int probability, int impact)
    {
        ValidateScale(probability, "Вероятность");
        ValidateScale(impact, "Влияние");
        return probability * impact;
    }

    /// <summary>Уровень критичности по баллу: "low" | "medium" | "high".</summary>
    public static string RiskLevel(int score) =>
        score >= HighThreshold ? "high" : score >= MediumThreshold ? "medium" : "low";
}
