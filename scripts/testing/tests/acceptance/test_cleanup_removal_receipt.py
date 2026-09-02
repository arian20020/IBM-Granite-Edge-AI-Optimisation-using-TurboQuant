from __future__ import annotations

import csv
import hashlib
import json
import os
import subprocess
import sys
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Any

import pytest


ROOT = Path(__file__).resolve().parents[4]
INVENTORY_FIELDS = (
    "source_root_id",
    "path",
    "tracked_status",
    "size_bytes",
    "sha256",
    "route",
    "test_case_id",
    "attempt_id",
    "terminal_status",
    "evidence_ids",
    "referenced_by_final_results",
    "duplicate_group",
    "action",
    "destination",
    "reason",
)
PLAN_FIELDS = (
    "source_root_id",
    "source_root",
    "source_branch",
    "source_head",
    "source_status_sha256",
    "source_path",
    "archive_path",
    "size_bytes",
    "sha256",
    "journal",
)


def _git(repo: Path, *args: str) -> str:
    result = subprocess.run(
        ["git", *args],
        cwd=repo,
        check=True,
        capture_output=True,
        text=True,
    )
    return result.stdout.strip()


def _git_bytes(repo: Path, *args: str) -> bytes:
    return subprocess.run(
        ["git", *args], cwd=repo, check=True, capture_output=True
    ).stdout


def _status_sha256(repo: Path) -> str:
    return hashlib.sha256(
        _git_bytes(repo, "status", "--porcelain=v2", "-z", "--ignored")
    ).hexdigest()


def _sha256_bytes(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def _sha256(path: Path) -> str:
    return _sha256_bytes(path.read_bytes())


def _json_bytes(payload: dict[str, Any]) -> bytes:
    return (json.dumps(payload, indent=2, sort_keys=True) + "\n").encode("utf-8")


def _write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(_json_bytes(payload))


def _write_csv(
    path: Path, rows: list[dict[str, str]], fieldnames: tuple[str, ...]
) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


@dataclass
class RemovalFixture:
    control_repo: Path
    source_repo: Path
    archive_root: Path
    inventory: Path
    archive_receipt: Path
    output: Path
    target: Path
    retained: Path
    rows: list[dict[str, str]]
    receipt: dict[str, Any]

    def commit_control_files(self) -> None:
        _git(self.control_repo, "add", "docs/testing/cleanup")
        _git(self.control_repo, "commit", "-qm", "bind cleanup controls")

    def write_controls(self, *, commit: bool = True) -> None:
        cleanup = self.inventory.parent
        source_head = _git(self.source_repo, "rev-parse", "HEAD")
        source_root_id = f"recovery-{source_head[:12]}"
        source_branch = _git(self.source_repo, "branch", "--show-current")
        source_status_sha256 = _status_sha256(self.source_repo)
        for row in self.rows:
            row["source_root_id"] = source_root_id

        _write_csv(self.inventory, self.rows, INVENTORY_FIELDS)
        plan_rows = []
        for row in self.rows:
            if row["action"] != "archive_external":
                continue
            receipt_entry = next(
                entry
                for entry in self.receipt["entries"]
                if entry["source_path"] == row["path"]
            )
            plan_rows.append(
                {
                    "source_root_id": source_root_id,
                    "source_root": str(self.source_repo.resolve()),
                    "source_branch": source_branch,
                    "source_head": source_head,
                    "source_status_sha256": source_status_sha256,
                    "source_path": row["path"],
                    "archive_path": row["destination"],
                    "size_bytes": row["size_bytes"],
                    "sha256": row["sha256"],
                    "journal": "planned|copied|verified",
                }
            )
            receipt_entry["source_root_id"] = source_root_id

        archive_plan = cleanup / "archive-plan.csv"
        _write_csv(archive_plan, plan_rows, PLAN_FIELDS)
        self.receipt["source_roots"] = [
            {
                "source_root_id": source_root_id,
                "source_root": str(self.source_repo.resolve()),
                "source_branch": source_branch,
                "source_head": source_head,
                "source_status_sha256": source_status_sha256,
            }
        ]
        self.receipt["archive_manifest_sha256"] = _archive_manifest(
            self.receipt["entries"]
        )
        _write_json(self.archive_receipt, self.receipt)

        bootstrap_head = _git(self.control_repo, "rev-list", "--max-parents=0", "HEAD")
        metadata = {
            "canonical_branch": _git(
                self.control_repo, "branch", "--show-current"
            ),
            "canonical_head": bootstrap_head,
            "canonical_root": str(self.control_repo.resolve()),
            "canonical_status_sha256": "0" * 64,
            "record_count": len(self.rows),
            "schema": "testing-cleanup-inventory/v1",
            "scopes": [
                "docs/testing/final-results",
                "experiments/raw-results",
                "scripts/testing",
            ],
            "source_branch": source_branch,
            "source_head": source_head,
            "source_root": str(self.source_repo.resolve()),
            "source_status_sha256": source_status_sha256,
        }
        _write_json(cleanup / "inventory-metadata.json", metadata)
        summary = {
            "archive_manifest_sha256": self.receipt[
                "archive_manifest_sha256"
            ],
            "archive_root": str(self.archive_root.resolve()),
            "entry_count": self.receipt["entry_count"],
            "plan_sha256": _sha256(archive_plan),
            "receipt_path": str(self.archive_receipt.resolve()),
            "receipt_sha256": _sha256(self.archive_receipt),
            "schema": "testing-history-archive-summary/v1",
            "total_bytes": self.receipt["total_bytes"],
            "unverified_count": self.receipt["unverified_count"],
        }
        _write_json(cleanup / "archive-summary.json", summary)
        if commit:
            self.commit_control_files()

    def run(self, *, dry_run: bool) -> subprocess.CompletedProcess[str]:
        command = [
            sys.executable,
            "-B",
            "-m",
            "scripts.testing.tools.archive_transaction",
            "remove-verified",
            "--inventory",
            str(self.inventory),
            "--archive-receipt",
            str(self.archive_receipt),
            "--output",
            str(self.output),
        ]
        if dry_run:
            command.append("--dry-run")
        environment = {**os.environ, "PYTHONDONTWRITEBYTECODE": "1"}
        return subprocess.run(
            command,
            cwd=ROOT,
            env=environment,
            capture_output=True,
            text=True,
            check=False,
        )


def _archive_manifest(entries: list[dict[str, Any]]) -> str:
    digest = hashlib.sha256()
    for entry in sorted(entries, key=lambda item: item["archive_path"]):
        digest.update(
            (
                f"{entry['sha256']}  {entry['size_bytes']}  "
                f"{entry['archive_path']}\n"
            ).encode("utf-8")
        )
    return digest.hexdigest()


@pytest.fixture
def removal_fixture(tmp_path: Path) -> RemovalFixture:
    source_repo = tmp_path / "recovery"
    source_repo.mkdir()
    _git(source_repo, "init", "-q", "-b", "recovery")
    _git(source_repo, "config", "user.email", "cleanup@example.invalid")
    _git(source_repo, "config", "user.name", "Cleanup Test")
    target = source_repo / "experiments/raw-results/history/result.txt"
    target.parent.mkdir(parents=True)
    target.write_bytes(b"archived historical result\n")
    retained = source_repo / "experiments/raw-results/history/keep.txt"
    retained.write_bytes(b"retained evidence\n")
    script = source_repo / "scripts/testing/runner.py"
    script.parent.mkdir(parents=True)
    script.write_text("VALUE = 1\n", encoding="utf-8")
    (source_repo / ".gitignore").write_text("__pycache__/\n", encoding="utf-8")
    _git(source_repo, "add", ".")
    _git(source_repo, "commit", "-qm", "source fixture")

    control_repo = tmp_path / "control"
    control_repo.mkdir()
    _git(control_repo, "init", "-q", "-b", "cleanup")
    _git(control_repo, "config", "user.email", "cleanup@example.invalid")
    _git(control_repo, "config", "user.name", "Cleanup Test")
    (control_repo / "README.md").write_text("control\n", encoding="utf-8")
    _git(control_repo, "add", "README.md")
    _git(control_repo, "commit", "-qm", "bootstrap")

    relative = target.relative_to(source_repo).as_posix()
    payload = target.read_bytes()
    digest = _sha256_bytes(payload)
    archive_root = tmp_path / "archive"
    archived = archive_root / relative
    archived.parent.mkdir(parents=True)
    archived.write_bytes(payload)
    entry = {
        "archive_path": relative,
        "journal": ["planned", "copied", "verified"],
        "sha256": digest,
        "size_bytes": len(payload),
        "source_path": relative,
        "source_root_id": "pending",
    }
    receipt = {
        "archive_manifest_sha256": _archive_manifest([entry]),
        "archive_root": str(archive_root.resolve()),
        "entries": [entry],
        "entry_count": 1,
        "journal_state_counts": {"copied": 1, "planned": 1, "verified": 1},
        "schema": "testing-history-archive-receipt/v1",
        "source_roots": [],
        "total_bytes": len(payload),
        "unverified_count": 0,
    }
    row = {
        "source_root_id": "pending",
        "path": relative,
        "tracked_status": "tracked",
        "size_bytes": str(len(payload)),
        "sha256": digest,
        "route": "",
        "test_case_id": "",
        "attempt_id": "",
        "terminal_status": "",
        "evidence_ids": "[]",
        "referenced_by_final_results": "False",
        "duplicate_group": f"DUP-{digest[:16]}",
        "action": "archive_external",
        "destination": relative,
        "reason": "exact duplicate with no active evidence binding",
    }
    fixture = RemovalFixture(
        control_repo=control_repo,
        source_repo=source_repo,
        archive_root=archive_root,
        inventory=control_repo / "docs/testing/cleanup/file-inventory.csv",
        archive_receipt=archive_root / "archive-receipt.json",
        output=control_repo / "docs/testing/cleanup/removal-receipt.json",
        target=target,
        retained=retained,
        rows=[row],
        receipt=receipt,
    )
    fixture.write_controls()
    return fixture


def _assert_refused(
    fixture: RemovalFixture, expected_error: str
) -> subprocess.CompletedProcess[str]:
    result = fixture.run(dry_run=True)
    assert result.returncode == 2, result.stdout + result.stderr
    assert expected_error in result.stderr.lower()
    assert fixture.target.exists()
    assert fixture.retained.read_bytes() == b"retained evidence\n"
    assert not fixture.output.exists()
    return result


def test_remove_verified_refuses_unverified_archive_row(
    removal_fixture: RemovalFixture,
) -> None:
    removal_fixture.receipt["entries"][0]["journal"] = ["planned", "copied"]
    removal_fixture.receipt["unverified_count"] = 1
    removal_fixture.write_controls()

    _assert_refused(removal_fixture, "unverified")


def test_remove_verified_refuses_mismatched_archive_destination(
    removal_fixture: RemovalFixture,
) -> None:
    removal_fixture.receipt["entries"][0]["archive_path"] = (
        "experiments/raw-results/history/different.txt"
    )
    removal_fixture.write_controls()

    _assert_refused(removal_fixture, "destination")


def test_remove_verified_refuses_cited_inventory_row(
    removal_fixture: RemovalFixture,
) -> None:
    removal_fixture.rows[0]["referenced_by_final_results"] = "True"
    removal_fixture.rows[0]["evidence_ids"] = '["EV-001"]'
    removal_fixture.write_controls()

    _assert_refused(removal_fixture, "cited")


def test_remove_verified_refuses_imported_script_misclassified_as_regenerable(
    removal_fixture: RemovalFixture,
) -> None:
    script = removal_fixture.source_repo / "scripts/testing/runner.py"
    removal_fixture.target = script
    removal_fixture.rows[0].update(
        {
            "path": "scripts/testing/runner.py",
            "tracked_status": "tracked",
            "size_bytes": str(script.stat().st_size),
            "sha256": _sha256(script),
            "duplicate_group": "",
            "action": "remove_regenerable",
            "destination": "scripts/testing/runner.py",
            "reason": "proven interpreter cache",
        }
    )
    removal_fixture.receipt["entries"] = []
    removal_fixture.receipt["entry_count"] = 0
    removal_fixture.receipt["total_bytes"] = 0
    removal_fixture.receipt["archive_manifest_sha256"] = _archive_manifest([])
    removal_fixture.write_controls()

    _assert_refused(removal_fixture, "regenerable")


def test_remove_verified_refuses_ambiguous_archive_record(
    removal_fixture: RemovalFixture,
) -> None:
    removal_fixture.rows[0]["duplicate_group"] = ""
    removal_fixture.rows[0]["reason"] = "no higher-precedence classification was proven"
    removal_fixture.write_controls()

    _assert_refused(removal_fixture, "ambiguous")


@pytest.mark.parametrize(
    ("unsafe_path", "expected_error"),
    [
        ("../outside.txt", "unsafe relative path"),
        (".", "unsafe relative path"),
        ("experiments/raw-results/history/*.txt", "wildcard"),
    ],
)
def test_remove_verified_refuses_escape_root_and_wildcard_paths(
    removal_fixture: RemovalFixture,
    unsafe_path: str,
    expected_error: str,
) -> None:
    removal_fixture.rows[0]["path"] = unsafe_path
    removal_fixture.rows[0]["destination"] = unsafe_path
    removal_fixture.receipt["entries"][0]["source_path"] = unsafe_path
    removal_fixture.receipt["entries"][0]["archive_path"] = unsafe_path
    removal_fixture.write_controls()

    _assert_refused(removal_fixture, expected_error)


def test_remove_verified_refuses_source_reparse_point(
    removal_fixture: RemovalFixture, tmp_path: Path
) -> None:
    outside = tmp_path / "outside.txt"
    outside.write_bytes(removal_fixture.target.read_bytes())
    link = removal_fixture.target.with_name("linked.txt")
    try:
        os.symlink(outside, link)
    except OSError as error:
        pytest.fail(f"test environment cannot create required symlink fixture: {error}")
    removal_fixture.target = link
    relative = link.relative_to(removal_fixture.source_repo).as_posix()
    removal_fixture.rows[0]["path"] = relative
    removal_fixture.rows[0]["destination"] = relative
    removal_fixture.receipt["entries"][0]["source_path"] = relative
    removal_fixture.receipt["entries"][0]["archive_path"] = relative
    archived = removal_fixture.archive_root / relative
    archived.write_bytes(outside.read_bytes())
    removal_fixture.write_controls()

    _assert_refused(removal_fixture, "reparse point")


def test_remove_verified_refuses_archive_receipt_not_bound_by_summary(
    removal_fixture: RemovalFixture,
) -> None:
    payload = json.loads(removal_fixture.archive_receipt.read_text(encoding="utf-8"))
    payload["extra"] = "tampered after verification"
    _write_json(removal_fixture.archive_receipt, payload)

    _assert_refused(removal_fixture, "receipt sha-256")


def test_remove_verified_refuses_inventory_bytes_changed_after_commit(
    removal_fixture: RemovalFixture,
) -> None:
    with removal_fixture.inventory.open("a", encoding="utf-8", newline="") as handle:
        handle.write("\n")

    _assert_refused(removal_fixture, "inventory")


def test_remove_verified_refuses_source_root_identity_change(
    removal_fixture: RemovalFixture,
) -> None:
    _git(removal_fixture.source_repo, "checkout", "-qb", "wrong-source")

    _assert_refused(removal_fixture, "source identity")


def test_remove_verified_refuses_source_size_changed_after_inventory(
    removal_fixture: RemovalFixture,
) -> None:
    removal_fixture.target.write_bytes(b"changed source bytes after inventory\n")
    removal_fixture.write_controls()

    _assert_refused(removal_fixture, "source size mismatch")


def test_remove_verified_refuses_directory_even_with_cache_shaped_name(
    removal_fixture: RemovalFixture,
) -> None:
    directory = (
        removal_fixture.source_repo
        / "scripts/testing/__pycache__/directory.cpython-313.pyc"
    )
    directory.mkdir(parents=True)
    removal_fixture.target = directory
    removal_fixture.rows[0].update(
        {
            "path": directory.relative_to(removal_fixture.source_repo).as_posix(),
            "tracked_status": "ignored",
            "size_bytes": "0",
            "sha256": _sha256_bytes(b""),
            "duplicate_group": "",
            "action": "remove_regenerable",
            "destination": "",
            "reason": "proven interpreter cache",
        }
    )
    removal_fixture.receipt["entries"] = []
    removal_fixture.receipt["entry_count"] = 0
    removal_fixture.receipt["total_bytes"] = 0
    removal_fixture.write_controls()

    _assert_refused(removal_fixture, "regular file")


def test_remove_verified_accepts_blank_destination_for_proven_bytecode_cache(
    removal_fixture: RemovalFixture,
) -> None:
    cache = (
        removal_fixture.source_repo
        / "scripts/testing/__pycache__/runner.cpython-313.pyc"
    )
    cache.parent.mkdir(parents=True, exist_ok=True)
    cache.write_bytes(b"regenerable cache\n")
    removal_fixture.target = cache
    removal_fixture.rows[0].update(
        {
            "path": cache.relative_to(removal_fixture.source_repo).as_posix(),
            "tracked_status": "ignored",
            "size_bytes": str(cache.stat().st_size),
            "sha256": _sha256(cache),
            "duplicate_group": "",
            "action": "remove_regenerable",
            "destination": "",
            "reason": "proven interpreter cache",
        }
    )
    removal_fixture.receipt["entries"] = []
    removal_fixture.receipt["entry_count"] = 0
    removal_fixture.receipt["total_bytes"] = 0
    removal_fixture.write_controls()

    dry_run = removal_fixture.run(dry_run=True)
    assert dry_run.returncode == 0, dry_run.stdout + dry_run.stderr
    assert cache.is_file()

    removed = removal_fixture.run(dry_run=False)
    assert removed.returncode == 0, removed.stdout + removed.stderr
    assert not cache.exists()
    receipt = json.loads(removal_fixture.output.read_text(encoding="utf-8"))
    assert receipt["entries"][0]["archive_destination"] is None
    assert receipt["entries"][0]["regenerable_rule"] == (
        "python bytecode cache: scripts/testing/**/__pycache__/*.pyc"
    )


def test_remove_verified_dry_run_then_removes_only_the_exact_listed_source(
    removal_fixture: RemovalFixture,
) -> None:
    inventory_before = removal_fixture.inventory.read_bytes()
    dry_run = removal_fixture.run(dry_run=True)

    assert dry_run.returncode == 0, dry_run.stdout + dry_run.stderr
    assert "planned 1 exact source file" in dry_run.stdout.lower()
    assert removal_fixture.target.is_file()
    assert removal_fixture.retained.read_bytes() == b"retained evidence\n"
    planned = json.loads(removal_fixture.output.read_text(encoding="utf-8"))
    assert planned["status"] == "planned"
    assert planned["planned_count"] == 1
    assert planned["removed_count"] == 0
    assert planned["root_counts"] == [
        {
            "action_counts": {"archive_external": 1, "remove_regenerable": 0},
            "planned_count": 1,
            "removed_count": 0,
            "role": "recovery",
            "root": str(removal_fixture.source_repo.resolve()),
            "source_root_id": planned["root_counts"][0]["source_root_id"],
        },
        {
            "action_counts": {"archive_external": 0, "remove_regenerable": 0},
            "planned_count": 0,
            "removed_count": 0,
            "role": "implementation",
            "root": str(removal_fixture.control_repo.resolve()),
            "source_root_id": None,
        },
    ]

    removed = removal_fixture.run(dry_run=False)

    assert removed.returncode == 0, removed.stdout + removed.stderr
    assert "removed 1 exact source file" in removed.stdout.lower()
    assert not removal_fixture.target.exists()
    assert removal_fixture.retained.read_bytes() == b"retained evidence\n"
    assert removal_fixture.inventory.read_bytes() == inventory_before
    receipt = json.loads(removal_fixture.output.read_text(encoding="utf-8"))
    assert receipt["status"] == "removed"
    assert receipt["planned_count"] == 1
    assert receipt["removed_count"] == 1
    assert receipt["entries"] == [
        {
            "action": "archive_external",
            "archive_destination": str(
                (removal_fixture.archive_root / receipt["entries"][0]["original_path"]).resolve()
            ),
            "archive_path": receipt["entries"][0]["original_path"],
            "original_path": receipt["entries"][0]["original_path"],
            "regenerable_rule": None,
            "removed_at_utc": receipt["entries"][0]["removed_at_utc"],
            "sha256": _sha256_bytes(b"archived historical result\n"),
            "size_bytes": len(b"archived historical result\n"),
            "source_root": str(removal_fixture.source_repo.resolve()),
            "source_root_id": receipt["entries"][0]["source_root_id"],
            "state": "removed",
        }
    ]
    assert datetime.fromisoformat(
        receipt["entries"][0]["removed_at_utc"].replace("Z", "+00:00")
    ).tzinfo is not None
