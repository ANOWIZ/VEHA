"""Publication guard: the seed must use explicitly synthetic identities."""

import ast
import re
from pathlib import Path

SEED = Path(__file__).parents[2] / "app" / "seed.py"


def test_demo_organizations_are_explicitly_synthetic():
    tree = ast.parse(SEED.read_text(encoding="utf-8"))
    counts = {"ClientCreate": 0, "VendorCreate": 0}
    for node in ast.walk(tree):
        if not isinstance(node, ast.Call) or not isinstance(node.func, ast.Name):
            continue
        if node.func.id not in counts:
            continue
        counts[node.func.id] += 1
        fields = {kw.arg: kw.value for kw in node.keywords}
        assert isinstance(fields["name"], ast.Constant)
        assert fields["name"].value.startswith("Демо-")
        if "inn" in fields:
            assert re.fullmatch(r"0{9}[1-4]", fields["inn"].value)
    assert counts == {"ClientCreate": 4, "VendorCreate": 3}


def test_demo_emails_and_contracts_are_synthetic():
    tree = ast.parse(SEED.read_text(encoding="utf-8"))
    for node in ast.walk(tree):
        if isinstance(node, ast.Constant) and isinstance(node.value, str) and "@" in node.value:
            assert re.fullmatch(r"[^@]*@[a-z0-9]+\.example\.com", node.value)
        if isinstance(node, ast.keyword) and node.arg == "contract_ref":
            assert isinstance(node.value, ast.Constant)
            assert node.value.value.startswith("DEMO-")


def test_demo_people_are_labeled_as_test_users():
    tree = ast.parse(SEED.read_text(encoding="utf-8"))
    for node in ast.walk(tree):
        if isinstance(node, ast.Assign) and any(
            isinstance(target, ast.Name) and target.id == "USERS" for target in node.targets
        ):
            users = ast.literal_eval(node.value)
            assert len(users) == 10
            assert all(user[1].startswith("Демо Пользователь ") for user in users)
        if isinstance(node, ast.keyword) and node.arg == "full_name" and isinstance(node.value, ast.Constant):
            assert node.value.value.startswith("Демо Пользователь ")
