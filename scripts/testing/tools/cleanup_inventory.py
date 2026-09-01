#!/usr/bin/env python3
"""Build a deterministic, Git-aware inventory for the testing cleanup."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import subprocess
from collections import defaultdict
from dataclasses import asdict, dataclass
from pathlib import Path, PurePosixPath
from typing import Iterable, Sequence


DEFAULT_SCOPES = (
    "scripts/testing",
    "experiments/raw-results",
    "docs/testing/final-results",
)

ALLOWED_ACTIONS = frozenset(
    {
        "retain_active",
        "move_active",
        "archive_code",
        "archive_external",
        "remove_regenerable",
        "retain_ambiguous",
    }
)


@dataclass(frozen=True, slots=True)
class InventoryRecord:
    source_root_id: str
    path: str
    tracked_status: str
    size_bytes: int
    sha256: str
    route: str
    test_case_id: str
    attempt_id: str
    terminal_status: str
    evidence_ids: tuple[str, ...]
    referenced_by_final_results: bool
    duplicate_group: str
    action: str
    destination: str
    reason: str


def _run_git(root: Path, *args: str) -> bytes:
    result = subprocess.run(
        ["git", *args],
        cwd=root,
        check=True,
        capture_output=True,
    )
    return result.stdout


def _git_text(root: Path, *args: str) -> str:
    return _run_git(root, *args).decode("utf-8", errors="surrogateescape").strip()


def _require_git_root(root: Path) -> Path:
    candidate = Path(root).resolve()
    try:
        top = Path(_git_text(candidate, "rev-parse", "--show-toplevel")).resolve()
    except (OSError, subprocess.CalledProcessError) as error:
        raise ValueError("source root must be a Git worktree") from error
    if top != candidate:
        raise ValueError("source root must be the Git worktree root")
    return candidate


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _zpaths(raw: bytes) -> set[str]:
    return {
        value.decode("utf-8", errors="surrogateescape").replace("\\", "/")
        for value in raw.split(b"\0")
        if value
    }


def _status_sets(root: Path) -> tuple[set[str], set[str]]:
    tracked = _zpaths(_run_git(root, "ls-files", "-z"))
    ignored = _zpaths(
        _run_git(root, "ls-files", "--others", "--ignored", "--exclude-standard", "-z")
    )
    return tracked, ignored


def _safe_scopes(scopes: Iterable[str]) -> tuple[str, ...]:
    normalized: list[str] = []
    for value in scopes:
        path = PurePosixPath(str(value).replace("\\", "/"))
        if path.is_absolute() or ".." in path.parts or not path.parts:
            raise ValueError(f"invalid inventory scope: {value}")
        normalized.append(path.as_posix().rstrip("/"))
    return tuple(sorted(set(normalized)))


def _files(root: Path, scopes: tuple[str, ...]) -> tuple[str, ...]:
    paths: set[str] = set()
    for scope in scopes:
        scoped = root / Path(scope)
        if not scoped.exists():
            continue
        if scoped.is_symlink():
            raise ValueError(f"inventory scope cannot be a symlink: {scope}")
        for path in scoped.rglob("*"):
            if path.is_file() or path.is_symlink():
                paths.add(path.relative_to(root).as_posix())
    return tuple(sorted(paths))


def _read_csv(path: Path) -> tuple[dict[str, str], ...]:
    try:
        with path.open("r", encoding="utf-8-sig", newline="") as handle:
            return tuple(dict(row) for row in csv.DictReader(handle))
    except (OSError, UnicodeError, csv.Error):
        return ()


def _evidence_bindings(root: Path) -> tuple[dict[str, tuple[str, ...]], dict[str, tuple[str, str, str]]]:
    by_path: dict[str, set[str]] = defaultdict(set)
    outcome_by_evidence: dict[str, tuple[str, str, str]] = {}
    result_root = root / "docs/testing/final-results"
    if not result_root.is_dir():
        return {}, {}

    for index in sorted(result_root.glob("*/evidence/evidence-index.csv")):
        for row in _read_csv(index):
            evidence_id = row.get("evidence_id", "").strip()
            relative_path = row.get("relative_path", "").strip().replace("\\", "/")
            if evidence_id and relative_path:
                by_path[relative_path].add(evidence_id)

    for register in sorted(result_root.glob("*/failures/failure-register.csv")):
        for row in _read_csv(register):
            raw_ids = row.get("evidence_ids", "")
            try:
                evidence_ids = json.loads(raw_ids) if raw_ids else []
            except json.JSONDecodeError:
                evidence_ids = []
            for evidence_id in evidence_ids:
                if isinstance(evidence_id, str):
                    outcome_by_evidence[evidence_id] = (
                        row.get("test_case_id", ""),
                        row.get("attempt_id", ""),
                        row.get("status", row.get("terminal_status", "")),
                    )
    return (
        {path: tuple(sorted(ids)) for path, ids in by_path.items()},
        outcome_by_evidence,
    )


def _route(path: str) -> str:
    parts = PurePosixPath(path).parts
    route_markers = {
        "upstream-llama-cpp": "upstream-llama-cpp",
        "atomicbot": "atomicbot-turboquant",
        "atomicbot-turboquant": "atomicbot-turboquant",
        "animehacker": "animehacker-tq3-0",
        "animehacker-tq3-0": "animehacker-tq3-0",
        "openvino-experimental-fork": "openvino-experimental-fork",
        "openvino-turboquant": "openvino-official-upstream",
        "official-openvino": "openvino-official-upstream",
    }
    for part in parts:
        clean = part.removeprefix("01-").removeprefix("02-").removeprefix("03-").removeprefix("04-").removeprefix("05-")
        if clean in route_markers:
            return route_markers[clean]
    return ""


def _outcome(ids: tuple[str, ...], mapping: dict[str, tuple[str, str, str]]) -> tuple[str, str, str]:
    values = {mapping[item] for item in ids if item in mapping}
    if not values:
        return "", "", ""
    cases = ";".join(sorted({value[0] for value in values if value[0]}))
    attempts = ";".join(sorted({value[1] for value in values if value[1]}))
    statuses = ";".join(sorted({value[2] for value in values if value[2]}))
    return cases, attempts, statuses


def _classification(
    path: str,
    *,
    evidence_ids: tuple[str, ...],
    duplicate_group: str,
) -> tuple[str, str, str]:
    posix = PurePosixPath(path)
    parts = {part.casefold() for part in posix.parts}
    name = posix.name.casefold()
    if evidence_ids:
        return "retain_active", path, "cited by a final-results evidence index"
    if "reproduction" in parts:
        return "retain_active", path, "required by a published reproduction package"
    if (
        "failure" in name
        or "failures" in parts
        or ("raw-results" in parts and name in {"stderr.txt", "stderr.log"})
    ) and not duplicate_group:
        return "retain_active", path, "unique failure-supporting evidence"
    if "__pycache__" in parts or posix.suffix.casefold() in {".pyc", ".pyo"}:
        return "remove_regenerable", "", "proven interpreter cache"
    if path.startswith("scripts/testing/") and posix.suffix.casefold() in {
        ".py",
        ".ps1",
        ".json",
        ".md",
        ".txt",
    }:
        if path.startswith("scripts/testing/final_results/"):
            destination = path.replace(
                "scripts/testing/final_results/",
                "scripts/testing/reporting/",
                1,
            )
        elif path.startswith("scripts/testing/atomicbot/"):
            destination = path.replace(
                "scripts/testing/atomicbot/",
                "scripts/testing/campaigns/atomicbot/",
                1,
            )
        elif path.startswith("scripts/testing/animehacker/"):
            destination = path.replace(
                "scripts/testing/animehacker/",
                "scripts/testing/campaigns/animehacker/",
                1,
            )
        elif path == "scripts/testing/measure_llama_run.py":
            destination = "scripts/testing/campaigns/llama_cpp/measure_run.py"
        elif path == "scripts/testing/measure_llama_server.py":
            destination = "scripts/testing/campaigns/llama_cpp/measure_server.py"
        elif path == "scripts/testing/parse_llama_measurement.py":
            destination = "scripts/testing/campaigns/llama_cpp/parse_measurement.py"
        else:
            destination = (
                f"scripts/testing/tools/{posix.name}"
                if len(posix.parts) == 3
                else path
            )
        return "move_active", destination, "active testing code or command dependency"
    if duplicate_group:
        return "archive_external", path, "exact duplicate with no active evidence binding"
    if path.startswith("experiments/raw-results/"):
        if name.endswith((".tmp", ".bak", ".old")) or "abandoned" in parts:
            return "archive_external", path, "unreferenced historical raw result"
        return "retain_ambiguous", path, "raw evidence has no proven disposal classification"
    return "retain_ambiguous", path, "no higher-precedence classification was proven"


def build_inventory(
    source_root: Path,
    canonical_root: Path,
    *,
    scopes: Sequence[str] = DEFAULT_SCOPES,
) -> tuple[InventoryRecord, ...]:
    """Inventory source files without mutating either worktree."""

    source = _require_git_root(source_root)
    canonical = _require_git_root(canonical_root)
    normalized_scopes = _safe_scopes(scopes)
    tracked, ignored = _status_sets(source)
    paths = _files(source, normalized_scopes)
    evidence_by_path, outcome_by_evidence = _evidence_bindings(source)

    hashes: dict[str, str] = {}
    sizes: dict[str, int] = {}
    by_hash: dict[str, list[str]] = defaultdict(list)
    for relative in paths:
        absolute = source / Path(relative)
        if absolute.is_symlink():
            payload = f"symlink:{absolute.readlink()}".encode("utf-8")
            digest = hashlib.sha256(payload).hexdigest()
            size = len(payload)
        else:
            digest = _sha256(absolute)
            size = absolute.stat().st_size
        hashes[relative] = digest
        sizes[relative] = size
        by_hash[digest].append(relative)
    duplicate_ids = {
        relative: f"DUP-{digest[:16]}"
        for digest, members in by_hash.items()
        if len(members) > 1
        for relative in members
    }

    source_id = f"recovery-{_git_text(source, 'rev-parse', '--short=12', 'HEAD')}"
    records: list[InventoryRecord] = []
    for relative in paths:
        evidence_ids = evidence_by_path.get(relative, ())
        case_id, attempt_id, terminal_status = _outcome(evidence_ids, outcome_by_evidence)
        duplicate_group = duplicate_ids.get(relative, "")
        action, destination, reason = _classification(
            relative,
            evidence_ids=evidence_ids,
            duplicate_group=duplicate_group,
        )
        if action not in ALLOWED_ACTIONS:
            raise AssertionError(f"unsupported inventory action: {action}")
        records.append(
            InventoryRecord(
                source_root_id=source_id,
                path=relative,
                tracked_status=(
                    "tracked"
                    if relative in tracked
                    else "ignored"
                    if relative in ignored
                    else "untracked"
                ),
                size_bytes=sizes[relative],
                sha256=hashes[relative],
                route=_route(relative),
                test_case_id=case_id,
                attempt_id=attempt_id,
                terminal_status=terminal_status,
                evidence_ids=evidence_ids,
                referenced_by_final_results=bool(evidence_ids),
                duplicate_group=duplicate_group,
                action=action,
                destination=destination,
                reason=reason,
            )
        )
    return tuple(records)


def _status_hash(root: Path) -> str:
    raw = _run_git(root, "status", "--porcelain=v2", "-z", "--ignored")
    return hashlib.sha256(raw).hexdigest()


def _write_csv(path: Path, rows: Iterable[dict[str, object]], fieldnames: Sequence[str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames, lineterminator="\n")
        writer.writeheader()
        for row in rows:
            writer.writerow(row)


def write_inventory_artifacts(
    source_root: Path,
    canonical_root: Path,
    output_root: Path,
    *,
    scopes: Sequence[str] = DEFAULT_SCOPES,
) -> tuple[InventoryRecord, ...]:
    source = _require_git_root(source_root)
    canonical = _require_git_root(canonical_root)
    normalized_scopes = _safe_scopes(scopes)
    records = build_inventory(source, canonical, scopes=normalized_scopes)
    output = Path(output_root)
    output.mkdir(parents=True, exist_ok=True)

    fields = tuple(InventoryRecord.__dataclass_fields__)
    _write_csv(
        output / "file-inventory.csv",
        (
            {
                **asdict(row),
                "evidence_ids": json.dumps(row.evidence_ids, separators=(",", ":")),
            }
            for row in records
        ),
        fields,
    )
    duplicate_rows = []
    for row in records:
        if row.duplicate_group:
            duplicate_rows.append(
                {
                    "duplicate_group": row.duplicate_group,
                    "path": row.path,
                    "sha256": row.sha256,
                    "size_bytes": row.size_bytes,
                    "action": row.action,
                }
            )
    _write_csv(
        output / "duplicate-groups.csv",
        duplicate_rows,
        ("duplicate_group", "path", "sha256", "size_bytes", "action"),
    )

    canonical_paths = set(_files(canonical, normalized_scopes))
    source_by_path = {row.path: row for row in records}
    delta_rows = []
    for relative in sorted(set(source_by_path) | canonical_paths):
        source_row = source_by_path.get(relative)
        canonical_path = canonical / Path(relative)
        canonical_hash = _sha256(canonical_path) if canonical_path.is_file() else ""
        source_hash = source_row.sha256 if source_row else ""
        state = (
            "only_source"
            if source_row and not canonical_hash
            else "only_canonical"
            if canonical_hash and not source_row
            else "exact"
            if source_hash == canonical_hash
            else "different"
        )
        delta_rows.append(
            {
                "path": relative,
                "state": state,
                "source_sha256": source_hash,
                "canonical_sha256": canonical_hash,
                "classification": source_row.action if source_row else "retain_ambiguous",
            }
        )
    _write_csv(
        output / "source-canonical-deltas.csv",
        delta_rows,
        ("path", "state", "source_sha256", "canonical_sha256", "classification"),
    )

    metadata = {
        "schema": "testing-cleanup-inventory/v1",
        "source_root": str(source),
        "source_branch": _git_text(source, "branch", "--show-current"),
        "source_head": _git_text(source, "rev-parse", "HEAD"),
        "source_status_sha256": _status_hash(source),
        "canonical_root": str(canonical),
        "canonical_branch": _git_text(canonical, "branch", "--show-current"),
        "canonical_head": _git_text(canonical, "rev-parse", "HEAD"),
        "canonical_status_sha256": _status_hash(canonical),
        "scopes": list(normalized_scopes),
        "record_count": len(records),
    }
    (output / "inventory-metadata.json").write_text(
        json.dumps(metadata, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    return records


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", type=Path, required=True)
    parser.add_argument("--canonical-root", type=Path, required=True)
    parser.add_argument("--scope", action="append", dest="scopes")
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--mode", choices=("inventory",), default="inventory")
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    records = write_inventory_artifacts(
        args.source_root,
        args.canonical_root,
        args.output_root,
        scopes=args.scopes or DEFAULT_SCOPES,
    )
    print(f"Inventoried {len(records)} files without modifying either worktree.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
