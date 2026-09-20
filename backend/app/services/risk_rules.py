"""Правила оценки рисков: балл по матрице вероятность×влияние и уровень.

Матрица 3×3: вероятность (1–3) × влияние (1–3) = балл 1–9. Возможные баллы —
1, 2, 3, 4, 6, 9. Разбиение на уровни критичности:
  низкий  (low)    : 1–2   — зелёная зона
  средний (medium) : 3–4   — жёлтая зона
  высокий (high)   : 6–9   — красная зона
Чистые функции без БД — покрыты юнит-тестами.
"""

from __future__ import annotations

from app.core.exceptions import ValidationError

MIN_LEVEL = 1
MAX_LEVEL = 3

# Нижние границы балла для уровней критичности.
HIGH_THRESHOLD = 6
MEDIUM_THRESHOLD = 3


def validate_scale(value: int, field: str) -> None:
    if not (MIN_LEVEL <= value <= MAX_LEVEL):
        raise ValidationError(f"{field}: значение должно быть от {MIN_LEVEL} до {MAX_LEVEL}")


def compute_score(probability: int, impact: int) -> int:
    """Балл риска = вероятность × влияние (после валидации шкалы 1–3)."""
    validate_scale(probability, "Вероятность")
    validate_scale(impact, "Влияние")
    return probability * impact


def risk_level(score: int) -> str:
    """Уровень критичности по баллу: 'low' | 'medium' | 'high'."""
    if score >= HIGH_THRESHOLD:
        return "high"
    if score >= MEDIUM_THRESHOLD:
        return "medium"
    return "low"
