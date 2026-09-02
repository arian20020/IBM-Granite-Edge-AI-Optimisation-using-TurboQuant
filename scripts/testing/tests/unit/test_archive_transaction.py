from __future__ import annotations

import hashlib
import os
import subprocess
from pathlib import Path

import pytest

from scripts.testing.tools.archive_transaction import (
    ArchiveError,
    JournalState,
    copy_archive,
    plan_archive,
    verify_archive,
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


def _status_hash(repo: Path) -> str:
    result = subprocess.run(
        ["git", "status", "--porcelain=v2", "-z", "--ignored"],
        cwd=repo,
        check=True,
        capture_output=True,
    )
    return hashlib.sha256(result.stdout).hexdigest()


def _sha(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def _record(repo: Path, relative: str, archive_path: str | None = None) -> dict[str, str]:
    source = repo / Path(relative)
    payload = source.read_bytes()
    head = _git(repo, "rev-parse", "HEAD")
    return {
        "source_root_id": f"recovery-{head[:12]}",
        "source_root": str(repo.resolve()),
        "source_branch": _git(repo, "branch", "--show-current"),
        "source_head": head,
        "source_status_sha256": _status_hash(repo),
        "path": relative.replace("\\", "/"),
        "destination": (archive_path or relative).replace("\\", "/"),
        "size_bytes": str(len(payload)),
        "sha256": _sha(payload),
        "action": "archive_external",
    }


@pytest.fixture
def source_repo(tmp_path: Path) -> Path:
    repo = tmp_path / "recovery"
    repo.mkdir()
    _git(repo, "init", "-q")
    _git(repo, "config", "user.email", "archive@example.invalid")
    _git(repo, "config", "user.name", "Archive Test")
    source = repo / "experiments/raw-results/history/result.txt"
    source.parent.mkdir(parents=True)
    source.write_bytes(b"historical evidence\n")
    _git(repo, "add", ".")
    _git(repo, "commit", "-qm", "fixture")
    return repo


def test_source_is_never_removed_by_copy_or_verify(source_repo: Path, tmp_path: Path):
    source = source_repo / "experiments/raw-results/history/result.txt"
    plan = plan_archive([_record(source_repo, source.relative_to(source_repo).as_posix())])
    destination = tmp_path / "archive"

    copy_archive(plan, destination)

    assert source.exists()
    result = verify_archive(plan, destination)
    assert result.valid
    assert source.exists()
    assert plan.entries[0].journal == (
        JournalState.PLANNED,
        JournalState.COPIED,
        JournalState.VERIFIED,
    )


def test_copy_refuses_insufficient_space_before_writing(
    source_repo: Path, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    plan = plan_archive([_record(source_repo, "experiments/raw-results/history/result.txt")])
    destination = tmp_path / "archive"
    usage_type = type(__import__("shutil").disk_usage(tmp_path))
    monkeypatch.setattr(
        "scripts.testing.tools.archive_transaction.shutil.disk_usage",
        lambda _path: usage_type(total=100, used=99, free=1),
    )

    with pytest.raises(ArchiveError, match="insufficient free space"):
        copy_archive(plan, destination)

    assert not destination.exists()


def test_plan_rejects_case_insensitive_destination_collision(
    source_repo: Path,
):
    second = source_repo / "experiments/raw-results/history/second.txt"
    second.write_bytes(b"second\n")
    first = _record(source_repo, "experiments/raw-results/history/result.txt", "same/File.txt")
    second_record = _record(source_repo, "experiments/raw-results/history/second.txt", "SAME/file.TXT")

    with pytest.raises(ArchiveError, match="duplicate archive destination"):
        plan_archive([first, second_record])


def test_copy_refuses_existing_different_bytes_without_overwrite(
    source_repo: Path, tmp_path: Path
):
    relative = "experiments/raw-results/history/result.txt"
    plan = plan_archive([_record(source_repo, relative)])
    destination = tmp_path / "archive"
    target = destination / Path(relative)
    target.parent.mkdir(parents=True)
    target.write_bytes(b"do not overwrite\n")

    with pytest.raises(ArchiveError, match="destination collision"):
        copy_archive(plan, destination)

    assert target.read_bytes() == b"do not overwrite\n"


def test_copy_resumes_interrupted_transaction_from_identical_file(
    source_repo: Path, tmp_path: Path
):
    second = source_repo / "experiments/raw-results/history/second.txt"
    second.write_bytes(b"second historical result\n")
    records = [
        _record(source_repo, "experiments/raw-results/history/result.txt"),
        _record(source_repo, "experiments/raw-results/history/second.txt"),
    ]
    plan = plan_archive(records)
    destination = tmp_path / "archive"
    first_target = destination / Path(records[0]["destination"])
    first_target.parent.mkdir(parents=True)
    first_target.write_bytes(
        (source_repo / Path(records[0]["path"])).read_bytes()
    )

    copy_archive(plan, destination)

    assert (destination / Path(records[1]["destination"])).read_bytes() == b"second historical result\n"
    assert all(entry.journal[-1] is JournalState.COPIED for entry in plan.entries)


def test_copy_and_verify_support_long_archive_paths(source_repo: Path, tmp_path: Path):
    long_relative = "/".join(["segment-" + ("x" * 35)] * 7) + "/result.txt"
    record = _record(
        source_repo,
        "experiments/raw-results/history/result.txt",
        long_relative,
    )
    plan = plan_archive([record])
    destination = tmp_path / "archive"

    copy_archive(plan, destination)

    assert verify_archive(plan, destination).valid
    assert len(str(destination / Path(long_relative))) > 260


def test_copy_rejects_source_hash_mismatch(source_repo: Path, tmp_path: Path):
    relative = "experiments/raw-results/history/result.txt"
    plan = plan_archive([_record(source_repo, relative)])
    (source_repo / Path(relative)).write_bytes(b"changed after planning\n")

    with pytest.raises(ArchiveError, match="source identity|source hash mismatch"):
        copy_archive(plan, tmp_path / "archive")


def test_verify_rejects_destination_hash_mismatch(source_repo: Path, tmp_path: Path):
    relative = "experiments/raw-results/history/result.txt"
    plan = plan_archive([_record(source_repo, relative)])
    destination = tmp_path / "archive"
    copy_archive(plan, destination)
    (destination / Path(relative)).write_bytes(b"corrupted archive\n")

    result = verify_archive(plan, destination)

    assert not result.valid
    assert result.unverified == (relative,)
    assert plan.entries[0].journal[-1] is JournalState.COPIED


def test_plan_rejects_source_symlink_or_reparse_point(source_repo: Path, tmp_path: Path):
    outside = tmp_path / "outside.txt"
    outside.write_bytes(b"historical evidence\n")
    link = source_repo / "experiments/raw-results/history/link.txt"
    try:
        os.symlink(outside, link)
    except OSError as error:
        pytest.fail(f"test environment cannot create required symlink fixture: {error}")
    record = _record(source_repo, "experiments/raw-results/history/result.txt")
    record["path"] = "experiments/raw-results/history/link.txt"

    with pytest.raises(ArchiveError, match="reparse point"):
        plan_archive([record])


def test_copy_rejects_destination_reparse_point(source_repo: Path, tmp_path: Path):
    plan = plan_archive([_record(source_repo, "experiments/raw-results/history/result.txt")])
    destination = tmp_path / "archive"
    destination.mkdir()
    outside = tmp_path / "outside"
    outside.mkdir()
    linked_component = destination / "experiments"
    try:
        os.symlink(outside, linked_component, target_is_directory=True)
    except OSError as error:
        pytest.fail(f"test environment cannot create required symlink fixture: {error}")

    with pytest.raises(ArchiveError, match="reparse point"):
        copy_archive(plan, destination)

    assert not any(outside.iterdir())


@pytest.mark.parametrize(
    ("field", "value"),
    [
        ("path", "../outside.txt"),
        ("destination", "../escaped.txt"),
        ("destination", "C:/absolute.txt"),
    ],
)
def test_plan_rejects_path_escape(
    source_repo: Path, field: str, value: str
):
    record = _record(source_repo, "experiments/raw-results/history/result.txt")
    record[field] = value

    with pytest.raises(ArchiveError, match="unsafe relative path"):
        plan_archive([record])


def test_each_operation_revalidates_recovery_worktree_identity(
    source_repo: Path, tmp_path: Path
):
    plan = plan_archive([_record(source_repo, "experiments/raw-results/history/result.txt")])
    destination = tmp_path / "archive"
    copy_archive(plan, destination)
    _git(source_repo, "checkout", "-qb", "wrong-branch")

    with pytest.raises(ArchiveError, match="source identity"):
        verify_archive(plan, destination)


def test_plan_rejects_source_root_id_not_bound_to_metadata(source_repo: Path):
    record = _record(source_repo, "experiments/raw-results/history/result.txt")
    record["source_root_id"] = "recovery-deadbeefdead"

    with pytest.raises(ArchiveError, match="source_root_id"):
        plan_archive([record])


def test_supported_journal_states_include_future_cleanup_transitions():
    assert {state.value for state in JournalState} == {
        "planned",
        "copied",
        "verified",
        "migrated",
        "removed",
    }
