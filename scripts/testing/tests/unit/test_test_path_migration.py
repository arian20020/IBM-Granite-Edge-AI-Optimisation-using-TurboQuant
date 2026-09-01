from __future__ import annotations

import csv
import subprocess
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[4]
TEST_ROOT = ROOT / "scripts/testing/tests"
MIGRATION_CSV = ROOT / "docs/testing/cleanup/test-path-migration.csv"
PRE_MOVE_MANIFEST = ROOT / "docs/testing/cleanup/pre-move-pytest-collection.txt"
POST_MOVE_MANIFEST = ROOT / "docs/testing/cleanup/post-move-pytest-collection.txt"
DESTINATION_BUCKETS = ("unit", "integration", "acceptance")


def _tracked_tests() -> tuple[str, ...]:
    result = subprocess.run(
        ["git", "ls-files", "scripts/testing/tests/**/test_*.py", "scripts/testing/tests/test_*.py"],
        cwd=ROOT,
        check=True,
        capture_output=True,
        text=True,
    )
    return tuple(sorted(path for path in result.stdout.splitlines() if path))


def _migration_rows() -> list[dict[str, str]]:
    with MIGRATION_CSV.open("r", encoding="utf-8-sig", newline="") as handle:
        rows = list(csv.DictReader(handle))
    assert rows
    required = {"source_path", "destination_path", "bucket"}
    assert required <= set(rows[0]), rows[0].keys()
    return rows


def _collection_node_ids(text: str) -> tuple[str, ...]:
    return tuple(
        line.strip()
        for line in text.splitlines()
        if "::" in line
    )


def _manifest_node_ids(path: Path) -> tuple[str, ...]:
    return _collection_node_ids(path.read_text(encoding="utf-8"))


def _live_collection_node_ids() -> tuple[str, ...]:
    result = subprocess.run(
        [
            sys.executable,
            "-m",
            "pytest",
            TEST_ROOT.relative_to(ROOT).as_posix(),
            "--collect-only",
            "-q",
        ],
        cwd=ROOT,
        check=True,
        capture_output=True,
        text=True,
    )
    return _collection_node_ids(result.stdout)


def _normalize_post_move_node_id(
    node_id: str, destination_to_source: dict[str, str]
) -> str:
    for destination, source in destination_to_source.items():
        if node_id.startswith(destination):
            return source + node_id[len(destination) :]
    return node_id


def test_tracked_tests_have_complete_responsibility_migration_map_and_collection_identity() -> None:
    tracked_tests = _tracked_tests()
    rows = _migration_rows()
    by_source = {row["source_path"]: row for row in rows}
    by_destination = {row["destination_path"]: row for row in rows}

    assert len(by_source) == len(rows)
    assert len(by_destination) == len(rows)
    assert tuple(sorted(by_destination)) == tracked_tests

    destination_to_source: dict[str, str] = {}
    for destination_path in tracked_tests:
        row = by_destination[destination_path]
        source_path = row["source_path"]
        bucket = row["bucket"]

        assert bucket in DESTINATION_BUCKETS
        assert destination_path.startswith(f"scripts/testing/tests/{bucket}/")
        destination_to_source[destination_path] = source_path
        assert destination_path.endswith(Path(source_path).name)

    assert len(destination_to_source) == len(tracked_tests)

    assert PRE_MOVE_MANIFEST.is_file()
    assert POST_MOVE_MANIFEST.is_file()

    pre_move_node_ids = _manifest_node_ids(PRE_MOVE_MANIFEST)
    for post_move_node_ids in (
        _manifest_node_ids(POST_MOVE_MANIFEST),
        _live_collection_node_ids(),
    ):
        normalized_post_move = tuple(
            sorted(
                _normalize_post_move_node_id(node_id, destination_to_source)
                for node_id in post_move_node_ids
            )
        )
        assert tuple(sorted(pre_move_node_ids)) == normalized_post_move
