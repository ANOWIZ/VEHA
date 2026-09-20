"""Юнит-тест защиты XLSX от формульной инъекции."""

from __future__ import annotations

from app.services.xlsx_safe import safe_text


def test_prefixes_formula_like_strings():
    assert safe_text("=1+1") == "'=1+1"
    assert safe_text("+SUM(A1)") == "'+SUM(A1)"
    assert safe_text("-2") == "'-2"
    assert safe_text("@cmd") == "'@cmd"
    assert safe_text("\tTab") == "'\tTab"


def test_leaves_safe_values_untouched():
    assert safe_text("Обычный текст") == "Обычный текст"
    assert safe_text("Проект PRJ-2026-001") == "Проект PRJ-2026-001"
    assert safe_text("") == ""
    assert safe_text(123) == 123
    assert safe_text(None) is None
