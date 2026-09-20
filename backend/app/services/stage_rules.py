"""Чистые правила переходов по стадиям проекта (без БД — легко тестируется).

Переходы только последовательные вперёд на одну стадию и откат на одну назад
(с указанием причины). См. CLAUDE.md §4 «Проекты»."""

from __future__ import annotations

from app.models.enums import STAGE_ORDER, Stage


def stage_index(stage: Stage) -> int:
    return STAGE_ORDER.index(stage)


def next_stage(stage: Stage) -> Stage | None:
    i = stage_index(stage)
    return STAGE_ORDER[i + 1] if i + 1 < len(STAGE_ORDER) else None


def prev_stage(stage: Stage) -> Stage | None:
    i = stage_index(stage)
    return STAGE_ORDER[i - 1] if i > 0 else None


def is_forward(from_stage: Stage, to_stage: Stage) -> bool:
    return stage_index(to_stage) - stage_index(from_stage) == 1


def is_backward(from_stage: Stage, to_stage: Stage) -> bool:
    return stage_index(from_stage) - stage_index(to_stage) == 1


def validate_transition(from_stage: Stage, to_stage: Stage, *, reason: str | None) -> None:
    """Бросает ValueError с человекочитаемым сообщением, если переход недопустим."""
    if from_stage == to_stage:
        raise ValueError("Стадия не изменилась")
    if is_forward(from_stage, to_stage):
        return
    if is_backward(from_stage, to_stage):
        if not reason or not reason.strip():
            raise ValueError("Откат на предыдущую стадию требует указания причины")
        return
    raise ValueError(
        "Недопустимый переход: разрешено только вперёд на одну стадию "
        "или откат на одну назад с указанием причины"
    )
