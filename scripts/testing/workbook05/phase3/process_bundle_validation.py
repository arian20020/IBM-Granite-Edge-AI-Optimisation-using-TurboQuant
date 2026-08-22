"""Validate Workbook 05 C2 process bundles as untrusted data."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from dataclasses import asdict, dataclass
from pathlib import Path, PurePosixPath
from typing import Any, Iterable, Mapping


_MANIFEST_LINE = re.compile(r"^([0-9a-f]{64})  ([^\r\n]+)$")
_FORBIDDEN_SUFFIXES = {
    ".7z", ".a", ".bin", ".ckpt", ".dll", ".dylib", ".exe", ".gguf",
    ".gz", ".lib", ".onnx", ".pt", ".pth", ".pyd", ".safetensors",
    ".so", ".tar", ".tgz", ".whl", ".xml", ".zip",
}


@dataclass(frozen=True, slots=True)
class BundleIssue:
    """One deterministic validation issue."""

    code: str
    message: str
    path: str | None = None


def _issue(code: str, message: str, path: str | None = None) -> BundleIssue:
    return BundleIssue(code=code, message=message, path=path)


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _safe_relative(value: str) -> bool:
    if not value or "\\" in value:
        return False
    path = PurePosixPath(value)
    return not path.is_absolute() and all(part not in {"", ".", ".."} for part in path.parts)


def _json(path: Path, issues: list[BundleIssue], code: str) -> Any | None:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        issues.append(_issue(code, f"Unreadable JSON: {error}", path.as_posix()))
        return None


def _manifest_entries(bundle: Path, issues: list[BundleIssue]) -> dict[str, str]:
    manifest_path = bundle / "manifest.sha256"
    if not manifest_path.is_file():
        issues.append(_issue("MANIFEST_MISSING", "manifest.sha256 is required"))
        return {}
    entries: dict[str, str] = {}
    try:
        lines = manifest_path.read_text(encoding="utf-8").splitlines()
    except (OSError, UnicodeError) as error:
        issues.append(_issue("MANIFEST_UNREADABLE", str(error), "manifest.sha256"))
        return {}
    for line_number, line in enumerate(lines, start=1):
        match = _MANIFEST_LINE.fullmatch(line)
        if match is None:
            issues.append(_issue("MANIFEST_ROW_INVALID", f"Invalid row {line_number}", "manifest.sha256"))
            continue
        digest, relative = match.groups()
        if not _safe_relative(relative):
            issues.append(_issue("UNSAFE_PATH", f"Unsafe manifest path: {relative}", relative))
            continue
        if relative in entries:
            issues.append(_issue("MANIFEST_DUPLICATE", f"Duplicate manifest path: {relative}", relative))
            continue
        entries[relative] = digest
    return entries


def _validate_manifest(bundle: Path, entries: Mapping[str, str], issues: list[BundleIssue]) -> None:
    actual_files = {
        path.relative_to(bundle).as_posix()
        for path in bundle.rglob("*")
        if path.is_file() and path.name != "manifest.sha256"
    }
    expected_files = set(entries)
    for missing in sorted(expected_files - actual_files):
        issues.append(_issue("MANIFEST_FILE_MISSING", f"Manifested file is missing: {missing}", missing))
    for extra in sorted(actual_files - expected_files):
        issues.append(_issue("MANIFEST_FILE_UNLISTED", f"File is not in manifest: {extra}", extra))
    for relative in sorted(actual_files & expected_files):
        path = bundle / PurePosixPath(relative)
        if path.is_symlink():
            issues.append(_issue("LINK_REJECTED", "Links are not permitted", relative))
            continue
        if _sha256(path) != entries[relative]:
            issues.append(_issue("MANIFEST_HASH_MISMATCH", f"SHA-256 mismatch: {relative}", relative))


def _validate_forbidden_payloads(bundle: Path, issues: list[BundleIssue]) -> None:
    for path in sorted(bundle.rglob("*")):
        if not path.is_file():
            continue
        relative = path.relative_to(bundle).as_posix()
        if path.suffix.casefold() in _FORBIDDEN_SUFFIXES:
            issues.append(_issue("FORBIDDEN_PAYLOAD", f"Forbidden payload suffix: {path.suffix}", relative))


def _validate_events(bundle: Path, issues: list[BundleIssue]) -> None:
    path = bundle / "events/events.jsonl"
    if not path.is_file():
        issues.append(_issue("EVENTS_MISSING", "events/events.jsonl is required", "events/events.jsonl"))
        return
    names: list[str] = []
    for line_number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), start=1):
        try:
            event = json.loads(line)
        except json.JSONDecodeError:
            issues.append(_issue("EVENT_JSON_INVALID", f"Invalid event JSON on line {line_number}", "events/events.jsonl"))
            return
        if not isinstance(event, dict) or not isinstance(event.get("event"), str):
            issues.append(_issue("EVENT_RECORD_INVALID", f"Invalid event record on line {line_number}", "events/events.jsonl"))
            return
        names.append(event["event"])
    try:
        started = names.index("started")
        first = names.index("first_token")
        completed = names.index("completed")
    except ValueError:
        issues.append(_issue("EVENT_ORDER_INVALID", "started, first_token and completed are required", "events/events.jsonl"))
        return
    if not (started < first < completed) or any(
        name == "token" and not (first < index < completed)
        for index, name in enumerate(names)
    ):
        issues.append(_issue("EVENT_ORDER_INVALID", "Event order is not started -> first_token -> token* -> completed", "events/events.jsonl"))


def _validate_command(bundle: Path, issues: list[BundleIssue]) -> None:
    path = bundle / "records/command.json"
    payload = _json(path, issues, "COMMAND_JSON_INVALID")
    if not isinstance(payload, dict):
        return
    if "command" in payload or not isinstance(payload.get("arguments"), list):
        issues.append(_issue("COMMAND_BOUNDARY_INVALID", "Command must be executable plus an argument array", "records/command.json"))
    if not isinstance(payload.get("executable"), str) or not payload.get("executable"):
        issues.append(_issue("COMMAND_BOUNDARY_INVALID", "Command executable is required", "records/command.json"))


def _validate_attempt(bundle: Path, issues: list[BundleIssue]) -> None:
    path = bundle / "records/process-attempt.json"
    attempt = _json(path, issues, "PROCESS_ATTEMPT_JSON_INVALID")
    if not isinstance(attempt, dict):
        return

    execution = attempt.get("execution")
    watchdog = attempt.get("watchdog")
    evidence = attempt.get("evidence")
    classification = attempt.get("classification")
    if not isinstance(execution, dict) or not isinstance(watchdog, dict) or not isinstance(evidence, dict):
        issues.append(_issue("PROCESS_ATTEMPT_INVALID", "Attempt subrecords are incomplete", "records/process-attempt.json"))
        return

    if classification == "Passed" and execution.get("exit_code") != 0:
        issues.append(_issue("PROCESS_EXIT_CONTRADICTION", "Passed attempt has a non-zero exit code", "records/process-attempt.json"))
    if classification == "Passed" and watchdog.get("safety_stop_triggered"):
        issues.append(_issue("SAFETY_STOP_CONTRADICTION", "Passed attempt reports a safety stop", "records/process-attempt.json"))

    termination = _json(bundle / "proof/termination.json", issues, "TERMINATION_JSON_INVALID")
    if classification == "Passed" and isinstance(termination, dict):
        if termination.get("reason") not in {"NORMAL_EXIT", "PROCESS_EXIT"}:
            issues.append(_issue("SAFETY_STOP_CONTRADICTION", "Passed attempt has a non-normal termination reason", "proof/termination.json"))

    raw_relative = evidence.get("raw_output_path")
    expected_raw_hash = evidence.get("raw_output_sha256")
    if not isinstance(raw_relative, str) or not _safe_relative(raw_relative):
        issues.append(_issue("UNSAFE_PATH", "Raw output path is unsafe", "records/process-attempt.json"))
    else:
        raw_path = bundle / PurePosixPath(raw_relative)
        if not raw_path.is_file() or _sha256(raw_path) != expected_raw_hash:
            issues.append(_issue("RAW_OUTPUT_HASH_MISMATCH", "Raw output does not match the attempt record", raw_relative))

    attempt_number = attempt.get("attempt_number")
    retry_of = attempt.get("retry_of_attempt_id")
    if isinstance(attempt_number, int) and attempt_number > 1:
        prior_ids: set[str] = set()
        for prior_path in bundle.glob("records/process-attempt-*.json"):
            prior = _json(prior_path, issues, "PRIOR_ATTEMPT_JSON_INVALID")
            if isinstance(prior, dict) and isinstance(prior.get("attempt_id"), str):
                prior_ids.add(prior["attempt_id"])
        if not isinstance(retry_of, str) or retry_of not in prior_ids:
            issues.append(_issue("RETRY_RELATION_INVALID", "Retry does not identify a retained prior attempt", "records/process-attempt.json"))

    # Every evidence path in the attempt must be safe and present.
    for field, value in evidence.items():
        if field.endswith("_sha256"):
            continue
        if not isinstance(value, str) or not _safe_relative(value):
            issues.append(_issue("UNSAFE_PATH", f"Unsafe evidence path in {field}", "records/process-attempt.json"))
            continue
        if not (bundle / PurePosixPath(value)).is_file():
            issues.append(_issue("EVIDENCE_FILE_MISSING", f"Evidence file is missing: {value}", value))


def validate_process_bundle(bundle: Path, repository_root: Path) -> list[BundleIssue]:
    """Return all stable issues without executing any captured command."""

    del repository_root  # The validator deliberately needs no executable repo input.
    bundle = bundle.resolve()
    issues: list[BundleIssue] = []
    if not bundle.is_dir():
        return [_issue("BUNDLE_MISSING", f"Bundle directory is missing: {bundle}")]
    entries = _manifest_entries(bundle, issues)
    if entries:
        _validate_manifest(bundle, entries, issues)
    _validate_forbidden_payloads(bundle, issues)
    _validate_events(bundle, issues)
    _validate_command(bundle, issues)
    _validate_attempt(bundle, issues)
    return sorted(issues, key=lambda item: (item.code, item.path or "", item.message))


def _report(issues: Iterable[BundleIssue]) -> dict[str, Any]:
    rows = [asdict(issue) for issue in issues]
    return {"valid": not rows, "issue_count": len(rows), "issues": rows}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle", type=Path, required=True)
    parser.add_argument("--repository-root", type=Path, required=True)
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()
    issues = validate_process_bundle(args.bundle, args.repository_root)
    payload = _report(issues)
    text = json.dumps(payload, indent=2) + "\n"
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(text, encoding="utf-8")
    print(text, end="")
    return 0 if not issues else 1


if __name__ == "__main__":
    raise SystemExit(main())
