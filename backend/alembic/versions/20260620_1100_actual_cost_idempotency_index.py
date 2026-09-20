"""actual cost idempotency partial unique index

Идемпотентность обмена с 1С на уровне БД: одна активная (не сторнированная)
затрата на (project_id, external_id). Partial — NULL external_id и soft-deleted
строки не участвуют, чтобы не блокировать повторную загрузку после сторно.

Revision ID: b1f2a3c4d5e6
Revises: 6c041dfac586
Create Date: 2026-06-20 11:00:00.000000
"""
from __future__ import annotations

from collections.abc import Sequence

from alembic import op

revision: str = 'b1f2a3c4d5e6'
down_revision: str | None = '6c041dfac586'
branch_labels: str | Sequence[str] | None = None
depends_on: str | Sequence[str] | None = None


def upgrade() -> None:
    op.create_index(
        "uq_actual_project_external",
        "actual_costs",
        ["project_id", "external_id"],
        unique=True,
        postgresql_where="external_id IS NOT NULL AND deleted_at IS NULL",
    )


def downgrade() -> None:
    op.drop_index("uq_actual_project_external", table_name="actual_costs")
