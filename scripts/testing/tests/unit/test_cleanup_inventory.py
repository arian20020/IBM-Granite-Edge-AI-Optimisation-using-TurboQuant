from __future__ import annotations

import csv
import hashlib
import subprocess
from pathlib import Path

import pytest

from scripts.testing.tools.cleanup_inventory import build_inventory


def _write(path: Path, value: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(value, encoding="utf-8", newline="\n")


def _git(repo: Path, *args: str) -> None:
    subprocess.run(
        ["git", *args], cwd=repo, check=True, capture_output=True, text=True
    )


@pytest.fixture
def cleanup_repo(tmp_path: Path) -> Path:
    repo = tmp_path / "source"
    repo.mkdir()
    _git(repo, "init", "-q")
    _git(repo, "config", "user.email", "cleanup@example.invalid")
    _git(repo, "config", "user.name", "Cleanup Test")

    _write(repo / "scripts/testing/runner.py", "VALUE = 1\n")
    _write(repo / "scripts/testing/__pycache__/runner.pyc", "cache\n")
    _write(repo / "experiments/raw-results/run-failed/stderr.log", "failure\n")
    _write(repo / "experiments/raw-results/run-abandoned/debug.tmp", "duplicate\n")
    _write(repo / "experiments/raw-results/run-copy/debug.tmp", "duplicate\n")
    _write(repo / "experiments/raw-results/unknown/note.txt", "ambiguous\n")
    _write(
        repo / "docs/testing/final-results/01-route/evidence/evidence-index.csv",
        "evidence_id,relative_path,sha256,size_bytes\n"
        "EV-FAIL,experiments/raw-results/run-failed/stderr.log,"
        f"{hashlib.sha256(b'failure\n').hexdigest()},8\n",
    )
    _write(
        repo / "docs/testing/final-results/01-route/failures/failure-register.csv",
        "test_case_id,attempt_id,status,evidence_ids\n"
        'CASE-1,ATTEMPT-1,failed,"[""EV-FAIL""]"\n',
    )
    _write(repo / ".gitignore", "__pycache__/\n")
    _git(
        repo,
        "add",
        ".gitignore",
        "scripts/testing/runner.py",
        "experiments/raw-results/run-failed/stderr.log",
        "docs/testing/final-results",
    )
    _git(repo, "commit", "-qm", "fixture")
    return repo


def test_inventory_preserves_referenced_failure_and_classifies_cleanup(
    cleanup_repo: Path,
):
    rows = build_inventory(cleanup_repo, cleanup_repo)
    by_path = {row.path: row for row in rows}

    failure = by_path["experiments/raw-results/run-failed/stderr.log"]
    assert failure.action == "retain_active"
    assert failure.referenced_by_final_results is True
    assert failure.evidence_ids == ("EV-FAIL",)
    assert failure.test_case_id == "CASE-1"
    assert failure.attempt_id == "ATTEMPT-1"
    assert failure.terminal_status == "failed"
    assert failure.tracked_status == "tracked"

    assert by_path["scripts/testing/runner.py"].action == "move_active"
    assert (
        by_path["scripts/testing/__pycache__/runner.pyc"].action
        == "remove_regenerable"
    )
    assert (
        by_path["experiments/raw-results/run-abandoned/debug.tmp"].action
        == "archive_external"
    )
    assert (
        by_path["experiments/raw-results/run-abandoned/debug.tmp"].duplicate_group
        == by_path["experiments/raw-results/run-copy/debug.tmp"].duplicate_group
    )
    assert (
        by_path["experiments/raw-results/unknown/note.txt"].action
        == "retain_ambiguous"
    )


def test_inventory_is_deterministic_complete_and_hash_bound(cleanup_repo: Path):
    first = build_inventory(cleanup_repo, cleanup_repo)
    second = build_inventory(cleanup_repo, cleanup_repo)
    assert first == second
    assert [row.path for row in first] == sorted(row.path for row in first)

    debug = next(
        row
        for row in first
        if row.path == "experiments/raw-results/run-abandoned/debug.tmp"
    )
    assert debug.tracked_status == "untracked"
    assert debug.size_bytes == len(b"duplicate\n")
    assert debug.sha256 == hashlib.sha256(b"duplicate\n").hexdigest()

    cache = next(row for row in first if "__pycache__" in row.path)
    assert cache.tracked_status == "ignored"


def test_inventory_rejects_a_source_outside_its_git_worktree(
    cleanup_repo: Path, tmp_path: Path
):
    with pytest.raises(ValueError, match="Git worktree"):
        build_inventory(tmp_path, cleanup_repo)
