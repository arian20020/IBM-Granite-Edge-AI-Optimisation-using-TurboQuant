"""Canonical disposable-fixture helpers for the cleaned raw-evidence tree."""

from __future__ import annotations

import csv
import hashlib
import json
import shutil
import subprocess
from dataclasses import dataclass
from functools import lru_cache
from pathlib import Path


PARENT_COMMIT = "9678e9c175dba87e37711424004598f2e528f68a"
PARENT_TREE = "bb2cf5de5fe4d704a587c8bb60ff5567f74f3607"


@dataclass(frozen=True)
class LegacyFixtureStats:
    migrated_path_count: int
    parent_history_path_count: int
    eol_restored_path_count: int
    archive_external_path_count: int


def _rows(path: Path) -> list[dict[str, str]]:
    with path.open(encoding="utf-8-sig", newline="") as handle:
        return [dict(row) for row in csv.DictReader(handle)]


def _identity(payload: bytes) -> tuple[int, str]:
    return len(payload), hashlib.sha256(payload).hexdigest()


def _copy_bytes(payload: bytes, destination: Path) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(payload)


@lru_cache(maxsize=4)
def _parent_prefix_snapshot(
    repository_root: str, prefix: str
) -> tuple[dict[str, bytes], dict[str, str]]:
    """Read one pinned parent prefix without checking files into the live tree."""
    root = Path(repository_root)
    actual_tree = subprocess.check_output(
        ["git", "rev-parse", f"{PARENT_COMMIT}^{{tree}}"],
        cwd=root,
        text=True,
    ).strip()
    if actual_tree != PARENT_TREE:
        raise AssertionError(("unexpected parent tree", actual_tree))
    raw_tree = subprocess.check_output(
        ["git", "ls-tree", "-r", "-z", PARENT_COMMIT, "--", prefix],
        cwd=root,
    )
    object_ids: dict[str, str] = {}
    for item in raw_tree.split(b"\0"):
        if not item:
            continue
        metadata, raw_path = item.split(b"\t", 1)
        _mode, kind, object_id = metadata.decode("ascii").split()
        if kind == "blob":
            object_ids[raw_path.decode("utf-8")] = object_id
    unique_ids = tuple(dict.fromkeys(object_ids.values()))
    process = subprocess.run(
        ["git", "cat-file", "--batch"],
        cwd=root,
        input="".join(f"{object_id}\n" for object_id in unique_ids).encode("ascii"),
        capture_output=True,
        check=True,
    )
    cursor = 0
    by_object_id: dict[str, bytes] = {}
    for requested_id in unique_ids:
        line_end = process.stdout.index(b"\n", cursor)
        object_id, kind, size = process.stdout[cursor:line_end].decode("ascii").split()
        if object_id != requested_id or kind != "blob":
            raise AssertionError(("unexpected cat-file response", requested_id, object_id))
        cursor = line_end + 1
        payload_end = cursor + int(size)
        by_object_id[object_id] = process.stdout[cursor:payload_end]
        if process.stdout[payload_end : payload_end + 1] != b"\n":
            raise AssertionError(("invalid cat-file framing", object_id))
        cursor = payload_end + 1
    if cursor != len(process.stdout):
        raise AssertionError("unexpected trailing cat-file output")
    payloads = {
        path: by_object_id[object_id] for path, object_id in object_ids.items()
    }
    return payloads, object_ids


def materialize_inventory_legacy_view(
    repository_root: Path,
    fixture_root: Path,
    legacy_prefix: str,
) -> LegacyFixtureStats:
    """Build an authoritative legacy view only inside a disposable fixture.

    Migrated inputs come from their retained destinations. Unmapped tracked
    historical rows come from the pinned parent tree; where Git normalized a
    CRLF source blob, the frozen inventory identity is used to restore CRLF and
    fail closed on any non-EOL difference.
    """
    source = Path(repository_root).resolve(strict=True)
    fixture = Path(fixture_root).resolve(strict=True)
    if not legacy_prefix.endswith("/"):
        raise ValueError("legacy prefix must end with a slash")
    inventory_path = source / "docs/testing/cleanup/file-inventory.csv"
    ledger_path = source / "docs/testing/cleanup/PATH-MIGRATION.csv"
    receipt_path = (
        source
        / "docs/testing/cleanup/implementation-root-removal-receipt.json"
    )
    inventory = {
        row["path"]: row
        for row in _rows(inventory_path)
        if row["tracked_status"] == "tracked"
        and row["path"].startswith(legacy_prefix)
    }
    migrations = {
        row["old_path"]: row["new_path"]
        for row in _rows(ledger_path)
        if row["old_path"].startswith(legacy_prefix)
    }
    unknown_migrations = set(migrations) - set(inventory)
    if unknown_migrations:
        raise AssertionError(("migration outside frozen inventory", unknown_migrations))

    ledger_target = fixture / "docs/testing/cleanup/PATH-MIGRATION.csv"
    ledger_target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(ledger_path, ledger_target)
    for old_path, new_path in sorted(migrations.items()):
        canonical = source / new_path
        payload = canonical.read_bytes()
        row = inventory[old_path]
        if _identity(payload) != (int(row["size_bytes"]), row["sha256"]):
            raise AssertionError(("retained identity mismatch", old_path, new_path))
        _copy_bytes(payload, fixture / new_path)
        _copy_bytes(payload, fixture / old_path)

    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    if receipt["input_commit"] != PARENT_COMMIT:
        raise AssertionError("implementation-removal receipt parent mismatch")
    removal_entries = {entry["path"]: entry for entry in receipt["entries"]}
    parent_payloads, parent_object_ids = _parent_prefix_snapshot(
        str(source), legacy_prefix
    )
    parent_count = eol_count = archive_count = 0
    for path, row in sorted(inventory.items()):
        if path in migrations:
            continue
        parent_payload = parent_payloads.get(path)
        if parent_payload is None:
            raise AssertionError(("path absent from pinned parent", path))
        object_id = parent_object_ids[path]
        expected = (int(row["size_bytes"]), row["sha256"])
        payload = parent_payload
        if _identity(payload) != expected:
            expanded = payload.replace(b"\n", b"\r\n")
            if _identity(expanded) != expected:
                raise AssertionError(("non-EOL parent/inventory mismatch", path))
            payload = expanded
            eol_count += 1
        if row["action"] == "archive_external":
            entry = removal_entries.get(path)
            if entry is None:
                raise AssertionError(("archived path absent from receipt", path))
            if (
                entry["parent_git_blob_oid"] != object_id
                or entry["parent_git_blob_size_bytes"] != len(parent_payload)
                or entry["parent_git_blob_sha256"]
                != hashlib.sha256(parent_payload).hexdigest()
                or (entry["size_bytes"], entry["sha256"]) != expected
            ):
                raise AssertionError(("receipt/parent identity mismatch", path))
            archive_count += 1
        _copy_bytes(payload, fixture / path)
        parent_count += 1
    return LegacyFixtureStats(
        migrated_path_count=len(migrations),
        parent_history_path_count=parent_count,
        eol_restored_path_count=eol_count,
        archive_external_path_count=archive_count,
    )


def materialize_exact_retained_alias(
    repository_root: Path,
    fixture_root: Path,
    legacy_path: str,
) -> str:
    """Copy the sole exact retained hash peer to a disposable legacy alias."""
    source = Path(repository_root).resolve(strict=True)
    fixture = Path(fixture_root).resolve(strict=True)
    inventory = {
        row["path"]: row
        for row in _rows(source / "docs/testing/cleanup/file-inventory.csv")
    }
    row = inventory[legacy_path]
    peers = [
        item["retained_path"]
        for item in _rows(source / "experiments/raw-results/evidence-manifest.csv")
        if (item["size_bytes"], item["sha256"])
        == (row["size_bytes"], row["sha256"])
    ]
    if len(peers) != 1:
        raise AssertionError(("expected one retained hash peer", legacy_path, peers))
    payload = (source / peers[0]).read_bytes()
    if _identity(payload) != (int(row["size_bytes"]), row["sha256"]):
        raise AssertionError(("retained alias identity mismatch", legacy_path))
    _copy_bytes(payload, fixture / legacy_path)
    return peers[0]


def materialize_openvino_canonical_repository(
    repository_root: Path,
    fixture_root: Path,
) -> Path:
    """Materialize the OpenVINO inputs needed by integration tests.

    Canonical migrated directories are copied under ``retained/`` and resolved
    through an exact copy of PATH-MIGRATION.csv. The two unmapped historical
    source-model aliases are reconstructed only in this disposable fixture from
    their sole retained hash peer.
    """
    source = Path(repository_root).resolve(strict=True)
    fixture = Path(fixture_root).resolve()
    fixture.mkdir(parents=True, exist_ok=True)
    relative_trees = (
        Path(
            "experiments/raw-results/retained/openvino-experimental-fork/"
            "2026-08-30/fv6"
        ),
        Path(
            "experiments/raw-results/retained/openvino-official-upstream/"
            "2026-08-30/fv1"
        ),
        Path(
            "experiments/raw-results/retained/openvino-official-upstream/"
            "2026-08-30/fv2-missing-model-attempts"
        ),
        Path("outputs/openvino-experimental-fork-results"),
        Path("outputs/openvino-official-upstream-results"),
        Path("docs/testing/final-results/04-openvino-experimental-fork"),
        Path("docs/testing/final-results/05-openvino-official-upstream"),
    )
    for relative in relative_trees:
        shutil.copytree(source / relative, fixture / relative)
    ledger = Path("docs/testing/cleanup/PATH-MIGRATION.csv")
    (fixture / ledger).parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source / ledger, fixture / ledger)
    aliases = (
        "experiments/raw-results/openvino-official-upstream/2026-08-30/"
        "fv2-missing-model-attempts/guarded-retry-002/source-models.json",
        "experiments/raw-results/openvino-official-upstream/2026-08-30/"
        "fv2-missing-model-attempts/source-models.json",
    )
    for legacy_path in aliases:
        materialize_exact_retained_alias(source, fixture, legacy_path)
    return fixture
