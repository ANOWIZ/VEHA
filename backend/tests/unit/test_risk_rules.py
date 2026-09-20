"""Юнит-тесты правил оценки рисков: балл матрицы и уровень критичности."""

from __future__ import annotations

import pytest
from app.core.exceptions import ValidationError
from app.services.risk_rules import compute_score, risk_level


@pytest.mark.parametrize(
    ("probability", "impact", "expected"),
    [
        (1, 1, 1),
        (1, 2, 2),
        (1, 3, 3),
        (2, 2, 4),
        (2, 3, 6),
        (3, 2, 6),
        (3, 3, 9),
    ],
)
def test_compute_score(probability: int, impact: int, expected: int) -> None:
    assert compute_score(probability, impact) == expected


@pytest.mark.parametrize("bad", [0, 4, -1, 10])
def test_compute_score_rejects_out_of_scale(bad: int) -> None:
    with pytest.raises(ValidationError):
        compute_score(bad, 2)
    with pytest.raises(ValidationError):
        compute_score(2, bad)


@pytest.mark.parametrize(
    ("score", "level"),
    [
        (1, "low"),
        (2, "low"),
        (3, "medium"),
        (4, "medium"),
        (6, "high"),
        (9, "high"),
    ],
)
def test_risk_level_banding(score: int, level: str) -> None:
    assert risk_level(score) == level


def test_level_covers_every_matrix_cell() -> None:
    # Все возможные баллы матрицы 3×3 имеют валидный уровень.
    for p in range(1, 4):
        for i in range(1, 4):
            assert risk_level(compute_score(p, i)) in {"low", "medium", "high"}
