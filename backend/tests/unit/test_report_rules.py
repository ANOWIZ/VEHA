"""Юнит-тесты вспомогательных функций отчётов: рабочие дни в периоде."""

from __future__ import annotations

from datetime import date

import pytest

from app.services.report_service import working_days


@pytest.mark.parametrize(
    ("d_from", "d_to", "expected"),
    [
        (date(2024, 1, 1), date(2024, 1, 1), 1),    # Пн — 1 рабочий день
        (date(2024, 1, 6), date(2024, 1, 6), 0),    # Сб — 0
        (date(2024, 1, 7), date(2024, 1, 7), 0),    # Вс — 0
        (date(2024, 1, 1), date(2024, 1, 5), 5),    # Пн–Пт — 5
        (date(2024, 1, 1), date(2024, 1, 7), 5),    # Пн–Вс — 5 (выходные не считаем)
        (date(2024, 1, 1), date(2024, 1, 14), 10),  # две недели — 10
        (date(2024, 1, 5), date(2024, 1, 1), 0),    # перевёрнутый диапазон — 0
    ],
)
def test_working_days(d_from: date, d_to: date, expected: int) -> None:
    assert working_days(d_from, d_to) == expected
