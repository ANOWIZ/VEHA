"""Тесты правил переходов по стадиям (чистая логика, без БД)."""

from __future__ import annotations

import pytest

from app.models.enums import Stage
from app.services import stage_rules


def test_forward_single_step_ok():
    stage_rules.validate_transition(Stage.PRESALE, Stage.SURVEY, reason=None)
    stage_rules.validate_transition(Stage.DESIGN, Stage.IMPLEMENTATION, reason=None)


def test_forward_skipping_is_rejected():
    with pytest.raises(ValueError):
        stage_rules.validate_transition(Stage.PRESALE, Stage.DESIGN, reason=None)


def test_backward_requires_reason():
    with pytest.raises(ValueError, match="причин"):
        stage_rules.validate_transition(Stage.DESIGN, Stage.SURVEY, reason=None)
    # с причиной — откат на одну назад допустим
    stage_rules.validate_transition(Stage.DESIGN, Stage.SURVEY, reason="недостаточно данных")


def test_backward_more_than_one_is_rejected():
    with pytest.raises(ValueError):
        stage_rules.validate_transition(
            Stage.IMPLEMENTATION, Stage.SURVEY, reason="любая причина"
        )


def test_same_stage_rejected():
    with pytest.raises(ValueError, match="не изменилась"):
        stage_rules.validate_transition(Stage.DESIGN, Stage.DESIGN, reason=None)


def test_next_prev_helpers():
    assert stage_rules.next_stage(Stage.PRESALE) == Stage.SURVEY
    assert stage_rules.next_stage(Stage.CLOSED) is None
    assert stage_rules.prev_stage(Stage.SURVEY) == Stage.PRESALE
    assert stage_rules.prev_stage(Stage.PRESALE) is None


def test_full_forward_chain_is_valid():
    from app.models.enums import STAGE_ORDER

    for a, b in zip(STAGE_ORDER, STAGE_ORDER[1:], strict=False):
        stage_rules.validate_transition(a, b, reason=None)
