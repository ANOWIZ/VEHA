"""Юнит-тесты безопасного Content-Disposition (защита от инъекции через имя файла)."""

from __future__ import annotations

from app.services.artifact_storage import content_disposition_attachment


def test_plain_ascii_name() -> None:
    cd = content_disposition_attachment("report.pdf")
    assert cd.startswith('attachment; filename="report.pdf"')
    assert "filename*=UTF-8''report.pdf" in cd


def test_quote_injection_is_neutralised() -> None:
    # Кавычка в имени не должна ломать quoted-string заголовка.
    cd = content_disposition_attachment('evil".exe')
    # В ASCII-фолбэке кавычка вычищена (нет «сырых» кавычек внутри значения).
    fallback = cd.split('filename="', 1)[1].split('"', 1)[0]
    assert '"' not in fallback
    assert ";" not in fallback


def test_no_crlf_or_header_breaking_chars() -> None:
    cd = content_disposition_attachment("a\r\nSet-Cookie: x=1.txt")
    assert "\r" not in cd
    assert "\n" not in cd


def test_unicode_name_percent_encoded() -> None:
    cd = content_disposition_attachment("Отчёт по проекту.xlsx")
    # Кириллица уходит в RFC 5987 filename* в процентном кодировании UTF-8.
    assert "filename*=UTF-8''" in cd
    assert "%D0" in cd  # начало UTF-8 кириллицы
