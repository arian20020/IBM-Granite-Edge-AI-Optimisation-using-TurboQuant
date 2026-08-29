#!/usr/bin/env python3
"""Strict, privacy-safe closure validation for the H1 R3 audit lane."""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import pathlib
import re
import subprocess
import sys
from typing import Any, Iterable


MAX_JSON_BYTES = 1024 * 1024
HEX40 = re.compile(r"^[0-9a-f]{40}$")
HEX64 = re.compile(r"^[0-9a-f]{64}$")


class ClosureFailure(Exception):
    def __init__(self, code: str):
        super().__init__(code)
        self.code = code


def fail(code: str) -> None:
    raise ClosureFailure(code)


def reject_duplicate_pairs(pairs: Iterable[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            fail("H1R3-JSON-DUPLICATE")
        result[key] = value
    return result


def load_bounded_json(path: str | pathlib.Path) -> Any:
    try:
        data = pathlib.Path(path).read_bytes()
    except OSError:
        fail("H1R3-FILE-MISSING")
    if not data or len(data) > MAX_JSON_BYTES:
        fail("H1R3-JSON-BOUNDS")
    try:
        return json.loads(
            data.decode("utf-8", "strict"), object_pairs_hook=reject_duplicate_pairs
        )
    except ClosureFailure:
        raise
    except (UnicodeDecodeError, json.JSONDecodeError):
        fail("H1R3-JSON-INVALID")


def canonical_json_bytes(value: Any) -> bytes:
    return (json.dumps(value, ensure_ascii=True, indent=2) + "\n").encode("utf-8")


def require_object(value: Any) -> dict[str, Any]:
    if not isinstance(value, dict):
        fail("H1R3-SCHEMA")
    return value


def require_string(value: Any, pattern: re.Pattern[str] | None = None) -> str:
    if not isinstance(value, str) or not value:
        fail("H1R3-SCHEMA")
    if pattern is not None and pattern.fullmatch(value) is None:
        fail("H1R3-SCHEMA")
    return value


def require_int(value: Any) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value < 0:
        fail("H1R3-ARITHMETIC")
    return value


def run_git(repository: pathlib.Path, *arguments: str, binary: bool = False):
    try:
        return subprocess.run(
            ["git", "-C", str(repository), *arguments],
            shell=False,
            capture_output=True,
            text=not binary,
            check=False,
        )
    except OSError:
        fail("H1R3-GIT")


def git_stdout(repository: pathlib.Path, *arguments: str) -> str:
    result = run_git(repository, *arguments)
    if result.returncode != 0:
        fail("H1R3-GIT")
    return result.stdout.strip()


def normalize_relative_path(repository: pathlib.Path, value: Any) -> str:
    relative = require_string(value).replace("\\", "/")
    posix = pathlib.PurePosixPath(relative)
    if posix.is_absolute() or ".." in posix.parts or relative.startswith("/"):
        fail("H1R3-FILE-BINDING")
    target = (repository / pathlib.Path(*posix.parts)).resolve()
    try:
        target.relative_to(repository.resolve())
    except ValueError:
        fail("H1R3-FILE-BINDING")
    return relative


def blob_bytes(repository: pathlib.Path, commit: str, relative: str) -> bytes:
    result = run_git(repository, "show", f"{commit}:{relative}", binary=True)
    if result.returncode != 0:
        fail("H1R3-COMMITTED-RESULT-MISSING")
    return result.stdout


def validate_counts(row: dict[str, Any]) -> tuple[int, int, int, int, int]:
    discovered = require_int(row.get("discovered"))
    executed = require_int(row.get("executed"))
    passed = require_int(row.get("passed"))
    failed = require_int(row.get("failed"))
    skipped = require_int(row.get("skipped"))
    if executed != passed + failed + skipped:
        fail("H1R3-ARITHMETIC")
    if discovered < executed:
        fail("H1R3-DISCOVERY")
    if discovered == 0:
        fail("H1R3-ZERO-DISCOVERY")
    exit_code = require_int(row.get("exitCode"))
    if (failed == 0 and exit_code != 0) or (failed > 0 and exit_code == 0):
        fail("H1R3-ARITHMETIC")
    return discovered, executed, passed, failed, skipped


def assert_tree(repository: pathlib.Path, commit: str, expected_tree: str, code: str) -> None:
    require_string(commit, HEX40)
    require_string(expected_tree, HEX40)
    actual = git_stdout(repository, "show", "-s", "--format=%T", commit)
    if actual != expected_tree:
        fail(code)


def assert_git_object(repository: pathlib.Path, commit: str) -> None:
    require_string(commit, HEX40)
    result = run_git(repository, "cat-file", "-e", f"{commit}^{{commit}}")
    if result.returncode != 0:
        fail("H1R3-BASE")


def assert_ancestor(repository: pathlib.Path, ancestor: str, descendant: str) -> None:
    result = run_git(repository, "merge-base", "--is-ancestor", ancestor, descendant)
    if result.returncode != 0:
        fail("H1R3-ANCESTRY")


def assert_remote_tip(repository: pathlib.Path, remote_ref: str, expected: str) -> None:
    actual = git_stdout(repository, "rev-parse", "--verify", remote_ref)
    if actual != expected:
        fail("H1R3-REMOTE-TIP")


def assert_clean(repository: pathlib.Path) -> None:
    result = run_git(repository, "status", "--porcelain=v1", "--untracked-files=all")
    if result.returncode != 0:
        fail("H1R3-GIT")
    if result.stdout:
        fail("H1R3-WORKTREE-DIRTY")


def assert_file_record(
    repository: pathlib.Path, commit: str, record_value: Any, missing_code: str = "H1R3-FILE-BINDING"
) -> bytes:
    record = require_object(record_value)
    relative = normalize_relative_path(repository, record.get("path"))
    expected_hash = require_string(record.get("sha256"), HEX64)
    expected_bytes = require_int(record.get("bytes"))
    try:
        data = blob_bytes(repository, commit, relative)
    except ClosureFailure as exc:
        if exc.code == "H1R3-COMMITTED-RESULT-MISSING":
            fail(missing_code)
        raise
    worktree_path = repository / pathlib.Path(*pathlib.PurePosixPath(relative).parts)
    try:
        worktree_data = worktree_path.read_bytes()
    except OSError:
        fail(missing_code)
    if data != worktree_data:
        fail(missing_code)
    if len(data) != expected_bytes or hashlib.sha256(data).hexdigest() != expected_hash:
        fail(missing_code)
    return data


def validate_ledger_document(
    repository: pathlib.Path,
    ledger: dict[str, Any],
    expected_subject_commit: str,
    expected_subject_tree: str,
) -> dict[str, int]:
    # R2 hashes had no committed result path. Check this first to preserve the
    # defect-specific diagnostic even though the older document predates R3 fields.
    commands = ledger.get("commands")
    if not isinstance(commands, list) or not commands:
        fail("H1R3-ZERO-DISCOVERY")
    if ledger.get("workerId") != "H1" or ledger.get("campaign") != "R3":
        for row_value in commands:
            row = require_object(row_value)
            if "resultSha256" in row and any(
                field not in row for field in ("resultPath", "resultBytes")
            ):
                fail("H1R3-COMMITTED-RESULT-MISSING")
        fail("H1R3-STALE-CAMPAIGN")
    if ledger.get("subjectTree") != expected_subject_tree:
        fail("H1R3-SUBJECT-TREE")
    assert_tree(repository, expected_subject_commit, expected_subject_tree, "H1R3-SUBJECT-TREE")

    totals = {key: 0 for key in ("discovered", "executed", "passed", "failed", "skipped")}
    for row_value in commands:
        row = require_object(row_value)
        require_string(row.get("id"))
        require_string(row.get("invocationId"))
        values = validate_counts(row)
        for key, value in zip(totals, values):
            totals[key] += value
    aggregate = require_object(ledger.get("totals"))
    for key, value in totals.items():
        if require_int(aggregate.get(key)) != value:
            fail("H1R3-ARITHMETIC")
    return totals


def validate_ledger(args: argparse.Namespace) -> None:
    repository = pathlib.Path(args.repository).resolve()
    ledger = require_object(load_bounded_json(args.ledger))
    validate_ledger_document(
        repository, ledger, args.expected_subject_commit, args.expected_subject_tree
    )


def summary_row(row_value: Any) -> dict[str, Any]:
    row = require_object(row_value)
    result = {
        "id": require_string(row.get("id")),
        "invocationId": require_string(row.get("invocationId")),
        "exitCode": require_int(row.get("exitCode")),
        "discovered": require_int(row.get("discovered")),
        "executed": require_int(row.get("executed")),
        "passed": require_int(row.get("passed")),
        "failed": require_int(row.get("failed")),
        "skipped": require_int(row.get("skipped")),
    }
    validate_counts(result)
    return result


def write_ledger(args: argparse.Namespace) -> None:
    summary = require_object(load_bounded_json(args.input_summary))
    if summary.get("workerId") != "H1" or summary.get("campaign") != "R3":
        fail("H1R3-STALE-CAMPAIGN")
    if summary.get("subjectTree") != args.subject_tree:
        fail("H1R3-SUBJECT-TREE")
    rows_value = summary.get("commands")
    if not isinstance(rows_value, list) or not rows_value:
        fail("H1R3-ZERO-DISCOVERY")
    rows = [summary_row(value) for value in rows_value]
    totals = {key: sum(row[key] for row in rows) for key in ("discovered", "executed", "passed", "failed", "skipped")}
    output = {
        "schemaVersion": 1,
        "workerId": "H1",
        "campaign": "R3",
        "createdAtUtc": dt.datetime.now(dt.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
        "subjectTree": args.subject_tree,
        "commands": rows,
        "totals": totals,
    }
    output_path = pathlib.Path(args.output_ledger)
    if output_path.exists():
        fail("H1R3-DESTINATION-EXISTS")
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_bytes(canonical_json_bytes(output))


def require_campaign(value: Any) -> dict[str, Any]:
    document = require_object(value)
    if document.get("campaign") != "R3" or document.get("workerId") != "H1":
        fail("H1R3-STALE-CAMPAIGN")
    return document


def assert_native_join(handoff_bytes: bytes, native_receipt: dict[str, Any]) -> None:
    if native_receipt.get("phaseClosed") is not True:
        fail("H1R3-NATIVE-CLEANUP")
    if native_receipt.get("cleanupVerified") is not True:
        fail("H1R3-NATIVE-CLEANUP")
    if native_receipt.get("nativeDisposition") != "passed":
        fail("H1R3-NATIVE-CLEANUP")
    expected = hashlib.sha256(handoff_bytes).hexdigest()
    if native_receipt.get("handoffReceiptSha256") != expected:
        fail("H1R3-NATIVE-JOIN")


def validate_manifest_commands(
    repository: pathlib.Path, final_tip: str, manifest: dict[str, Any]
) -> dict[str, int]:
    rows_value = manifest.get("commands")
    if not isinstance(rows_value, list) or not rows_value:
        fail("H1R3-ZERO-DISCOVERY")
    totals = {key: 0 for key in ("discovered", "executed", "passed", "failed", "skipped")}
    for row_value in rows_value:
        row = require_object(row_value)
        require_string(row.get("id"))
        values = validate_counts(row)
        for key, value in zip(totals, values):
            totals[key] += value
        record = {
            "path": row.get("resultPath"),
            "sha256": row.get("resultSha256"),
            "bytes": row.get("resultBytes"),
        }
        assert_file_record(
            repository, final_tip, record, "H1R3-COMMITTED-RESULT-MISSING"
        )
    aggregate = require_object(manifest.get("testTotals"))
    for key, value in totals.items():
        if require_int(aggregate.get(key)) != value:
            fail("H1R3-ARITHMETIC")
    return totals


def validate_closure(args: argparse.Namespace) -> None:
    repository = pathlib.Path(args.repository).resolve()
    manifest = require_campaign(load_bounded_json(args.manifest))
    require_string(args.expected_base, HEX40)
    require_string(args.expected_subject_commit, HEX40)
    require_string(args.expected_subject_tree, HEX40)
    require_string(args.expected_final_tip, HEX40)
    require_string(args.expected_final_tree, HEX40)

    if manifest.get("baseCommit") != args.expected_base:
        fail("H1R3-BASE")
    if manifest.get("evidenceSubjectCommit") != args.expected_subject_commit:
        fail("H1R3-SUBJECT-TREE")
    if manifest.get("evidenceSubjectTree") != args.expected_subject_tree:
        fail("H1R3-SUBJECT-TREE")

    assert_git_object(repository, args.expected_base)
    assert_ancestor(repository, args.expected_base, args.expected_subject_commit)
    assert_tree(repository, args.expected_subject_commit, args.expected_subject_tree, "H1R3-SUBJECT-TREE")
    assert_ancestor(repository, args.expected_subject_commit, args.expected_final_tip)
    assert_tree(repository, args.expected_final_tip, args.expected_final_tree, "H1R3-FINAL-TREE")
    if args.remote_ref:
        assert_remote_tip(repository, args.remote_ref, args.expected_final_tip)
    if not args.allow_dirty:
        assert_clean(repository)

    report_bytes = assert_file_record(repository, args.expected_final_tip, manifest.get("report"))
    del report_bytes
    managed_bytes = assert_file_record(repository, args.expected_final_tip, manifest.get("managedLedger"))
    native_bytes = assert_file_record(repository, args.expected_final_tip, manifest.get("nativeLedger"))
    del managed_bytes, native_bytes
    validate_manifest_commands(repository, args.expected_final_tip, manifest)

    outputs = manifest.get("outputs")
    if not isinstance(outputs, list):
        fail("H1R3-SCHEMA")
    kinds = [require_object(value).get("kind") for value in outputs]
    if sorted(kinds) != ["availableMemory", "hardwareSnapshot", "safetyBudget"]:
        fail("H1R3-SCHEMA")

    if args.managed_ledger:
        managed = require_object(load_bounded_json(args.managed_ledger))
        validate_ledger_document(repository, managed, args.expected_subject_commit, args.expected_subject_tree)
    if args.native_ledger:
        native = require_campaign(load_bounded_json(args.native_ledger))
        if native.get("cleanupVerified") is not True:
            fail("H1R3-NATIVE-CLEANUP")

    if bool(args.handoff) != bool(args.native_receipt):
        fail("H1R3-NATIVE-JOIN")
    if args.handoff:
        handoff_bytes = pathlib.Path(args.handoff).read_bytes()
        handoff = require_campaign(load_bounded_json(args.handoff))
        native_receipt = require_campaign(load_bounded_json(args.native_receipt))
        if handoff.get("phaseClosed") is not True:
            fail("H1R3-STALE-CAMPAIGN")
        assert_native_join(handoff_bytes, native_receipt)


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(add_help=False)
    subparsers = parser.add_subparsers(dest="operation", required=True)

    ledger = subparsers.add_parser("validate-ledger", add_help=False)
    ledger.add_argument("--repository", required=True)
    ledger.add_argument("--ledger", required=True)
    ledger.add_argument("--expected-subject-commit", required=True)
    ledger.add_argument("--expected-subject-tree", required=True)
    ledger.set_defaults(handler=validate_ledger)

    writer = subparsers.add_parser("write-ledger", add_help=False)
    writer.add_argument("--input-summary", required=True)
    writer.add_argument("--output-ledger", required=True)
    writer.add_argument("--subject-tree", required=True)
    writer.set_defaults(handler=write_ledger)

    closure = subparsers.add_parser("validate-closure", add_help=False)
    closure.add_argument("--repository", required=True)
    closure.add_argument("--manifest", required=True)
    closure.add_argument("--managed-ledger")
    closure.add_argument("--native-ledger")
    closure.add_argument("--report")
    closure.add_argument("--handoff")
    closure.add_argument("--native-receipt")
    closure.add_argument("--expected-base", required=True)
    closure.add_argument("--expected-subject-commit", required=True)
    closure.add_argument("--expected-subject-tree", required=True)
    closure.add_argument("--expected-final-tip", required=True)
    closure.add_argument("--expected-final-tree", required=True)
    closure.add_argument("--remote-ref")
    closure.add_argument("--allow-dirty", action="store_true")
    closure.set_defaults(handler=validate_closure)
    return parser


def main() -> int:
    try:
        args = build_parser().parse_args()
        args.handler(args)
        print("H1R3-OK")
        return 0
    except ClosureFailure as exc:
        print(exc.code)
        return 2
    except Exception:
        print("H1R3-INTERNAL")
        return 3


if __name__ == "__main__":
    sys.exit(main())
