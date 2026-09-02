from __future__ import annotations

import csv
import hashlib
import io
import json
import subprocess
import tarfile
from collections import Counter
from pathlib import Path


ROOT = Path(__file__).resolve().parents[4]
INVENTORY = ROOT / "docs/testing/cleanup/file-inventory.csv"
BASELINE = json.loads(
    (ROOT / "docs/testing/cleanup/baseline-semantic-snapshot.json").read_text(
        encoding="utf-8"
    )
)
RAW_ROOT = ROOT / "experiments/raw-results"
RETAINED_ROOT = RAW_ROOT / "retained"
MANIFEST = RAW_ROOT / "evidence-manifest.csv"
FAILURE_INDEX = RAW_ROOT / "failure-records/failure-evidence.csv"
PATH_MIGRATION = ROOT / "docs/testing/cleanup/PATH-MIGRATION.csv"
ACTIVE_ACTIONS = {"retain_active", "move_active"}
ROUTES = {
    "upstream-llama-cpp",
    "atomicbot-turboquant",
    "animehacker-tq3-0",
    "openvino-experimental-fork",
    "openvino-official-upstream",
}


def _rows(path: Path) -> list[dict[str, str]]:
    with path.open(encoding="utf-8-sig", newline="") as handle:
        return [dict(row) for row in csv.DictReader(handle)]


def _sha256_bytes(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _active_raw_rows() -> list[dict[str, str]]:
    return [
        row
        for row in _rows(INVENTORY)
        if row["path"].startswith("experiments/raw-results/")
        and row["action"] in ACTIVE_ACTIONS
    ]


def _destination(row: dict[str, str]) -> str:
    source = Path(row["path"])
    parts = source.parts
    if parts[2] == "failures":
        assert source.as_posix() == "experiments/raw-results/failures/README.md"
        return "experiments/raw-results/failure-records/README.md"
    route = row["route"] or parts[2]
    assert route in ROUTES
    tail = Path(*parts[3:]).as_posix()
    return f"experiments/raw-results/retained/{route}/{tail}"


def _mapping() -> dict[str, str]:
    return {row["path"]: _destination(row) for row in _active_raw_rows()}


def _relationship_key(row: dict[str, str]) -> tuple[str, str, str, str]:
    return (
        row["evidence_id"],
        row["relative_path"],
        row["sha256"],
        row["size_bytes"],
    )


def test_every_active_raw_inventory_row_has_one_exact_retained_destination() -> None:
    rows = _active_raw_rows()
    mapping = _mapping()

    assert len(rows) == 1400
    assert len(mapping) == len(rows)
    assert len(set(mapping.values())) == len(rows)
    assert len({path.casefold() for path in mapping.values()}) == len(rows)
    assert RETAINED_ROOT.is_dir()

    for row in rows:
        destination = ROOT / mapping[row["path"]]
        assert destination.is_file(), mapping[row["path"]]
        assert destination.stat().st_size == int(row["size_bytes"])
        assert _sha256(destination) == row["sha256"]


def test_manifest_and_path_ledger_are_complete_and_exclude_archive_rows() -> None:
    inventory = _rows(INVENTORY)
    active = _active_raw_rows()
    expected = {
        (row["path"], _destination(row), row["sha256"], row["size_bytes"])
        for row in active
    }
    manifest = _rows(MANIFEST)
    actual = {
        (
            row["source_path"],
            row["retained_path"],
            row["sha256"],
            row["size_bytes"],
        )
        for row in manifest
    }

    assert len(manifest) == 1400
    assert actual == expected
    assert {row["inventory_action"] for row in manifest} <= ACTIVE_ACTIONS
    archive_sources = {
        row["path"] for row in inventory if row["action"] == "archive_external"
    }
    assert archive_sources.isdisjoint(row["source_path"] for row in manifest)

    ledger = _rows(PATH_MIGRATION)
    ledger_pairs = {(row["old_path"], row["new_path"]) for row in ledger}
    assert {(old, new) for old, new in _mapping().items()} <= ledger_pairs
    assert len({row["old_path"] for row in ledger}) == len(ledger)


def test_all_evidence_relationships_resolve_with_exact_task_one_semantics() -> None:
    mapping = _mapping()
    reverse = {new: old for old, new in mapping.items()}
    indexes = sorted(
        (ROOT / "docs/testing/final-results").glob("*/evidence/evidence-index.csv")
    )
    actual = [row for index in indexes for row in _rows(index)]

    assert len(actual) == 1857
    assert len({row["relative_path"] for row in actual}) == 1846
    expected = sorted(_relationship_key(row) for row in BASELINE["evidence"]["relationships"])
    normalized = sorted(
        (
            row["evidence_id"],
            reverse.get(row["relative_path"], row["relative_path"]),
            row["sha256"],
            row["size_bytes"],
        )
        for row in actual
    )
    assert normalized == expected

    for row in actual:
        path = ROOT / row["relative_path"]
        assert path.is_file(), row["relative_path"]
        assert path.stat().st_size == int(row["size_bytes"])
        assert _sha256(path) == row["sha256"]


def test_scientific_tables_outcomes_and_comparisons_equal_task_one_snapshot() -> None:
    table_paths = {
        "attempts": "data/attempts.csv",
        "measurements": "data/measurements.csv",
        "summaries": "data/summaries.csv",
        "quality": "data/quality.csv",
        "failures": "data/failures.csv",
        "deviations": "data/deviations.csv",
    }
    routes = sorted((ROOT / "docs/testing/final-results").glob("0[1-5]-*"))

    for table, relative in table_paths.items():
        actual = sorted(
            (
                row
                for route in routes
                if (route / relative).is_file()
                for row in _rows(route / relative)
            ),
            key=lambda row: json.dumps(row, sort_keys=True, separators=(",", ":")),
        )
        assert actual == BASELINE["scientific_tables"][table]

    attempts = [row for route in routes for row in _rows(route / "data/attempts.csv")]
    assert len(attempts) == 169
    assert Counter(row["status"] for row in attempts) == Counter(
        {"passed": 81, "failed": 6, "blocked": 28, "artifact_unavailable": 54}
    )
    assert {row["route_id"] for row in attempts} == set(
        BASELINE["outcomes"]["stable_route_ids"]
    )
    assert {row["campaign_id"] for row in attempts} == set(
        BASELINE["outcomes"]["stable_campaign_ids"]
    )
    assert {row["test_case_id"] for row in attempts} == set(
        BASELINE["outcomes"]["stable_test_case_ids"]
    )
    assert {row["attempt_id"] for row in attempts} == set(
        BASELINE["outcomes"]["stable_attempt_ids"]
    )

    comparisons = sorted(
        _rows(
            ROOT
            / "docs/testing/final-results/06-cross-route-comparison/data/comparability-matrix.csv"
        ),
        key=lambda row: json.dumps(row, sort_keys=True, separators=(",", ":")),
    )
    assert comparisons == BASELINE["comparability"]["rows"]


def test_terminal_support_artifacts_have_complete_failure_records() -> None:
    expected_rows = [row for row in _active_raw_rows() if row["terminal_status"]]
    actual_rows = _rows(FAILURE_INDEX)

    assert len(expected_rows) == 15
    assert {row["terminal_status"] for row in expected_rows} == {
        "failed",
        "blocked",
        "artifact_unavailable",
    }
    expected = {
        (
            row["route"],
            row["test_case_id"],
            row["attempt_id"],
            row["terminal_status"],
            row["evidence_ids"],
            _destination(row),
            row["sha256"],
            row["size_bytes"],
        )
        for row in expected_rows
    }
    actual = {
        (
            row["route"],
            row["test_case_id"],
            row["attempt_id"],
            row["terminal_status"],
            row["evidence_ids"],
            row["retained_path"],
            row["sha256"],
            row["size_bytes"],
        )
        for row in actual_rows
    }
    assert actual == expected


def test_path_only_report_updates_preserve_markdown_docx_semantic_parity() -> None:
    from scripts.testing.reporting.parity import compare_markdown_docx

    for route in sorted((ROOT / "docs/testing/final-results").glob("0[1-5]-*")):
        markdown = next((route / "reports").glob("*.md"))
        docx = next((route / "reports").glob("*.docx"))
        assert compare_markdown_docx(markdown, docx)["matches"] is True, route.name


def test_every_destination_is_present_with_exact_bytes_in_the_git_index() -> None:
    rows = _active_raw_rows()
    tree = subprocess.run(
        ["git", "write-tree"],
        cwd=ROOT,
        check=True,
        capture_output=True,
        text=True,
    ).stdout.strip()
    archive = subprocess.run(
        ["git", "archive", "--format=tar", tree],
        cwd=ROOT,
        check=True,
        capture_output=True,
    ).stdout

    with tarfile.open(fileobj=io.BytesIO(archive), mode="r:") as staged:
        members = {member.name: member for member in staged.getmembers() if member.isfile()}
        for row in rows:
            retained_path = _destination(row)
            member = members[retained_path]
            assert member.size == int(row["size_bytes"])
            extracted = staged.extractfile(member)
            assert extracted is not None
            assert _sha256_bytes(extracted.read()) == row["sha256"]

        staged_relationships: list[dict[str, str]] = []
        for index_path in sorted(
            path
            for path in members
            if path.startswith("docs/testing/final-results/0")
            and path.endswith("/evidence/evidence-index.csv")
        ):
            extracted = staged.extractfile(members[index_path])
            assert extracted is not None
            text = io.TextIOWrapper(extracted, encoding="utf-8-sig", newline="")
            staged_relationships.extend(dict(row) for row in csv.DictReader(text))

        assert len(staged_relationships) == 1857
        assert len({row["relative_path"] for row in staged_relationships}) == 1846
        reverse = {new: old for old, new in _mapping().items()}
        normalized = sorted(
            (
                row["evidence_id"],
                reverse.get(row["relative_path"], row["relative_path"]),
                row["sha256"],
                row["size_bytes"],
            )
            for row in staged_relationships
        )
        assert normalized == sorted(
            _relationship_key(row) for row in BASELINE["evidence"]["relationships"]
        )
        for row in staged_relationships:
            member = members[row["relative_path"]]
            assert member.size == int(row["size_bytes"])
            extracted = staged.extractfile(member)
            assert extracted is not None
            assert _sha256_bytes(extracted.read()) == row["sha256"]

        for manifest_path in sorted(
            path
            for path in members
            if path.startswith("docs/testing/final-results/0")
            and path.endswith("/evidence/manifest-sha256.txt")
        ):
            extracted = staged.extractfile(members[manifest_path])
            assert extracted is not None
            for line in extracted.read().decode("utf-8").splitlines():
                expected_hash, relative_path = line.split("  ", 1)
                target = staged.extractfile(members[relative_path])
                assert target is not None
                assert _sha256_bytes(target.read()) == expected_hash
