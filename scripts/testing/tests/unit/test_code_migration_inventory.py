from __future__ import annotations

import ast
import csv
from pathlib import Path


ROOT = Path(__file__).resolve().parents[4]
INVENTORY_PATH = ROOT / "docs/testing/cleanup/file-inventory.csv"
MIGRATION_PATH = ROOT / "archive/testing-code/MIGRATION.csv"
ARCHIVE_ROOT = ROOT / "archive/testing-code/2026-09-01"
TESTING_ROOT = ROOT / "scripts/testing"
ALLOWED_ROOT_FILES = {"README.md", "requirements.txt"}
ALLOWED_ROOT_DIRS = {"campaigns", "cli", "examples", "reporting", "tests", "tools"}
IGNORED_ROOT_DIRS = {".pytest_cache", "__pycache__"}
EXPECTED_ARCHIVE_BASELINE = "d0eb34f2"


def _inventory_rows() -> list[dict[str, str]]:
    with INVENTORY_PATH.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def _migration_rows() -> list[dict[str, str]]:
    with MIGRATION_PATH.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def _task1_root_scripts() -> tuple[str, ...]:
    rows = _inventory_rows()
    return tuple(
        sorted(
            row["path"]
            for row in rows
            if row["tracked_status"] == "tracked"
            and row["path"].startswith("scripts/testing/")
            and row["path"].count("/") == 2
            and Path(row["path"]).suffix in {".py", ".ps1"}
        )
    )


def _assert_category_path(category: str, final_path: str, archive_path: str) -> None:
    if category == "canonical_cli":
        assert final_path.startswith("scripts/testing/cli/")
        assert (ROOT / final_path).is_file()
        assert archive_path == ""
    elif category == "active_tool":
        assert final_path.startswith("scripts/testing/tools/")
        assert (ROOT / final_path).is_file()
        assert archive_path == ""
    elif category == "campaign_module":
        assert final_path.startswith("scripts/testing/campaigns/")
        assert (ROOT / final_path).is_file()
        assert archive_path == ""
    elif category == "archived_code":
        assert final_path == ""
        assert archive_path.startswith("archive/testing-code/2026-09-01/")
        assert (ROOT / archive_path).is_file()
    else:
        raise AssertionError(f"unsupported migration category: {category}")


def _legacy_imports_and_paths(module_path: Path, legacy_module_names: set[str], legacy_paths: set[str]) -> list[str]:
    text = module_path.read_text(encoding="utf-8")
    tree = ast.parse(text, filename=str(module_path))
    findings: list[str] = []
    for node in ast.walk(tree):
        if isinstance(node, ast.Import):
            for alias in node.names:
                if alias.name in legacy_module_names:
                    findings.append(f"{module_path}: import {alias.name}")
        elif isinstance(node, ast.ImportFrom) and node.module in legacy_module_names:
            findings.append(f"{module_path}: from {node.module}")
    for legacy_path in legacy_paths:
        if legacy_path in text:
            findings.append(f"{module_path}: path {legacy_path}")
    return findings


def test_tracked_task1_root_scripts_have_a_complete_migration_inventory() -> None:
    assert MIGRATION_PATH.is_file()
    assert ARCHIVE_ROOT.is_dir()

    task1_root_scripts = set(_task1_root_scripts())
    migration_rows = _migration_rows()
    by_old_path = {row["old_path"]: row for row in migration_rows}

    assert set(by_old_path) == task1_root_scripts

    inventory_rows = {
        row["path"]: row
        for row in _inventory_rows()
        if row["path"] in task1_root_scripts
    }

    for old_path in sorted(task1_root_scripts):
        row = by_old_path[old_path]
        category = row["category"]
        final_path = row["final_path"]
        replacement = row["replacement"]
        archive_path = row["archive_path"]
        reason = row["reason"]
        baseline = row["last_scientific_baseline"]

        assert category in {
            "canonical_cli",
            "active_tool",
            "campaign_module",
            "archived_code",
        }
        assert reason
        _assert_category_path(category, final_path, archive_path)

        inventory_row = inventory_rows[old_path]
        if category == "archived_code":
            assert replacement in {"none"} or replacement
            assert baseline == EXPECTED_ARCHIVE_BASELINE
            assert inventory_row["action"] == "archive_code"
            assert inventory_row["destination"] == archive_path
        else:
            assert baseline == ""
            assert inventory_row["action"] == "move_active"
            assert inventory_row["destination"] == final_path


def test_scripts_testing_root_is_limited_to_documented_files_and_directories() -> None:
    root_files = {path.name for path in TESTING_ROOT.iterdir() if path.is_file()}
    root_dirs = {
        path.name
        for path in TESTING_ROOT.iterdir()
        if path.is_dir() and path.name not in IGNORED_ROOT_DIRS
    }

    assert root_files == ALLOWED_ROOT_FILES
    assert root_dirs == ALLOWED_ROOT_DIRS


def test_active_code_no_longer_imports_or_points_at_legacy_root_scripts() -> None:
    migration_rows = _migration_rows()
    legacy_module_names = {
        old_path[:-3].replace("/", ".")
        for old_path in _task1_root_scripts()
        if old_path.endswith(".py")
    }
    allowed_paths = {
        row["old_path"]
        for row in migration_rows
        if row["category"] == "archived_code"
    }
    legacy_paths = {
        old_path
        for old_path in _task1_root_scripts()
        if old_path not in allowed_paths
    }

    findings: list[str] = []
    for path in sorted((ROOT / "scripts/testing").rglob("*.py")):
        relative = path.relative_to(ROOT).as_posix()
        if relative.startswith("scripts/testing/tests/"):
            continue
        if relative == "scripts/testing/tools/cleanup_inventory.py":
            continue
        findings.extend(_legacy_imports_and_paths(path, legacy_module_names, legacy_paths))

    assert findings == []
