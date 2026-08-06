"""Validate Workbook 05 build evidence as untrusted text-only data."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import Final, Iterable

from scripts.testing.workbook05.build_contracts import validate_build_record


REQUIRED_PATHS: Final[tuple[str, ...]] = (
    "bundle.json",
    "decision.json",
    "manifest.sha256",
)
FORBIDDEN_SUFFIXES: Final[frozenset[str]] = frozenset(
    {
        ".exe",
        ".dll",
        ".lib",
        ".pdb",
        ".whl",
        ".zip",
        ".7z",
        ".tar",
        ".gz",
        ".onnx",
        ".xml",
        ".bin",
        ".gguf",
        ".safetensors",
    }
)
SECRET_PATTERNS: Final[tuple[str, ...]] = (
    "ghp_",
    "github_pat_",
    "HF_TOKEN=",
    "HUGGING_FACE_HUB_TOKEN=",
    "Authorization: Bearer ",
    "-----BEGIN PRIVATE KEY-----",
)
_RECORD_TYPES: Final[dict[str, str]] = {
    "build-command": "command",
    "build-deviation": "deviation",
    "build-dependency": "dependency",
    "build-resource-summary": "resource",
    "build-binary": "binary",
    "build-compatibility-attempt": "compatibility",
    "build-decision": "decision",
}
# Runtime and GenAI deliberately emit these high-cardinality record classes as
# JSON arrays. Naming the containers makes the parser fail closed without
# guessing that every unrelated JSON list is a build-record collection.
_RECORD_COLLECTIONS: Final[dict[str, str]] = {
    "binaries.json": "build-binary",
    "dependencies.json": "build-dependency",
}
_MANIFEST_LINE = re.compile(r"^([0-9a-f]{64})  (.+)$")


@dataclass(frozen=True)
class ExpectedBuildBundle:
    """Identity that the hosted job expects from one exact workflow artifact."""

    route_id: str
    component: str
    source_commit: str
    run_id: str
    run_attempt: int


@dataclass(frozen=True)
class ValidationIssue:
    """One deterministic issue found while reading an untrusted bundle."""

    code: str
    message: str
    path: str | None = None


def _safe_relative_path(value: object) -> bool:
    """Accept only slash-normalised relative paths contained by the bundle."""

    if not isinstance(value, str) or not value or "\x00" in value:
        return False
    if "\\" in value or value.startswith("/") or value.startswith("//"):
        return False
    if re.match(r"^[A-Za-z]:", value):
        return False
    return all(part not in {"", ".", ".."} for part in PurePosixPath(value).parts)


def _load_json(path: Path, relative: str, issues: list[ValidationIssue]) -> object | None:
    """Read one JSON file without importing or executing any of its contents."""

    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        issues.append(ValidationIssue("JSON_INVALID", str(error), relative))
        return None


def _walk_evidence_paths(value: object, location: str = "$") -> list[tuple[str, str]]:
    """Collect only fields that semantically point inside the evidence bundle."""

    found: list[tuple[str, str]] = []
    if isinstance(value, dict):
        for key, child in value.items():
            child_location = f"{location}.{key}"
            lowered = key.lower()

            # Tool and dependency metadata legitimately contains absolute machine
            # paths such as python_path and cmake_path. Only fields whose contract
            # explicitly means "path inside this artifact" receive containment
            # checks here; typed record validators provide the second line of defence.
            is_single_evidence_path = (
                lowered in {"stdout_path", "stderr_path", "relative_path"}
                or lowered.endswith("evidence_path")
            )
            if is_single_evidence_path and isinstance(child, str):
                found.append((child_location, child))
            elif lowered.endswith("evidence_paths") and isinstance(child, list):
                for index, item in enumerate(child):
                    if isinstance(item, str):
                        found.append((f"{child_location}[{index}]", item))
            found.extend(_walk_evidence_paths(child, child_location))
    elif isinstance(value, list):
        for index, child in enumerate(value):
            found.extend(_walk_evidence_paths(child, f"{location}[{index}]"))
    return found


def _record_payloads(
    relative: str,
    payload: object,
    issues: list[ValidationIssue],
) -> list[tuple[str, dict[str, object]]]:
    """Expose one record object or every record in a known collection file."""

    collection_record_name = _RECORD_COLLECTIONS.get(PurePosixPath(relative).name)
    if collection_record_name is not None:
        if not isinstance(payload, list):
            issues.append(
                ValidationIssue(
                    "RECORD_INVALID",
                    f"{PurePosixPath(relative).name} must contain a JSON array.",
                    relative,
                )
            )
            return []

        records: list[tuple[str, dict[str, object]]] = []
        for index, item in enumerate(payload):
            item_path = f"{relative}[{index}]"
            if not isinstance(item, dict):
                issues.append(
                    ValidationIssue(
                        "RECORD_INVALID",
                        "Build-record collection item must be a JSON object.",
                        item_path,
                    )
                )
                continue
            if item.get("record_type") != collection_record_name:
                issues.append(
                    ValidationIssue(
                        "RECORD_INVALID",
                        "Build-record collection item has the wrong or missing record_type.",
                        item_path,
                    )
                )
                continue
            records.append((item_path, item))
        return records

    if isinstance(payload, dict) and "record_type" in payload:
        return [(relative, payload)]
    return []


def _validate_manifest(root: Path, issues: list[ValidationIssue]) -> tuple[int, int]:
    """Verify hashes and require the manifest to cover every other bundle file."""

    manifest = root / "manifest.sha256"
    if not manifest.is_file():
        return (0, 0)

    try:
        lines = manifest.read_text(encoding="utf-8-sig").splitlines()
    except (OSError, UnicodeError) as error:
        issues.append(ValidationIssue("MANIFEST_INVALID", str(error), "manifest.sha256"))
        return (0, 0)

    entries: dict[str, str] = {}
    for line_number, line in enumerate(lines, start=1):
        match = _MANIFEST_LINE.fullmatch(line)
        if not match:
            issues.append(
                ValidationIssue(
                    "MANIFEST_INVALID",
                    f"Line {line_number} is not '<sha256><two spaces><path>'.",
                    "manifest.sha256",
                )
            )
            continue
        digest, relative = match.groups()
        if not _safe_relative_path(relative):
            issues.append(
                ValidationIssue(
                    "UNSAFE_EVIDENCE_PATH",
                    f"Manifest line {line_number}: {relative}",
                    "manifest.sha256",
                )
            )
            continue
        if relative == "manifest.sha256" or relative in entries:
            issues.append(
                ValidationIssue(
                    "MANIFEST_INVALID",
                    f"Duplicate or self-referential manifest path: {relative}",
                    "manifest.sha256",
                )
            )
            continue
        entries[relative] = digest

    actual_paths = {
        candidate.relative_to(root).as_posix()
        for candidate in root.rglob("*")
        if candidate.is_file() and candidate != manifest
    }
    listed_paths = set(entries)
    for relative in sorted(actual_paths - listed_paths):
        issues.append(
            ValidationIssue(
                "MANIFEST_INCOMPLETE",
                "Bundle file is not covered by the manifest.",
                relative,
            )
        )
    for relative in sorted(listed_paths - actual_paths):
        issues.append(
            ValidationIssue(
                "HASH_MISMATCH",
                "Manifest references a missing file.",
                relative,
            )
        )

    verified = 0
    for relative in sorted(actual_paths & listed_paths):
        candidate = root / PurePosixPath(relative)
        digest = hashlib.sha256(candidate.read_bytes()).hexdigest()
        if digest != entries[relative]:
            issues.append(
                ValidationIssue(
                    "HASH_MISMATCH",
                    f"Expected {entries[relative]}, calculated {digest}.",
                    relative,
                )
            )
        else:
            verified += 1
    return (len(actual_paths), verified)


def _validate_metadata(
    payload: object,
    expected: ExpectedBuildBundle,
    issues: list[ValidationIssue],
) -> None:
    """Validate the exact route/component/source/run identity and Route B gate."""

    if not isinstance(payload, dict):
        issues.append(ValidationIssue("IDENTITY_MISMATCH", "bundle.json must be an object.", "bundle.json"))
        return

    allowed = {
        "schema_version",
        "campaign_id",
        "route_id",
        "component",
        "source_commit",
        "run_id",
        "run_attempt",
        "br8_prerequisite",
    }
    for key in sorted(set(payload) - allowed):
        issues.append(ValidationIssue("IDENTITY_MISMATCH", f"Unexpected property: {key}", "bundle.json"))

    expected_values = {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "route_id": expected.route_id,
        "component": expected.component,
        "source_commit": expected.source_commit,
        "run_id": expected.run_id,
        "run_attempt": expected.run_attempt,
    }
    for key, expected_value in expected_values.items():
        if payload.get(key) != expected_value:
            issues.append(
                ValidationIssue(
                    "IDENTITY_MISMATCH",
                    f"{key} expected {expected_value!r}, found {payload.get(key)!r}.",
                    "bundle.json",
                )
            )

    if expected.route_id != "route-b-experimental-qjl-polar":
        return

    prerequisite = payload.get("br8_prerequisite")
    valid = (
        isinstance(prerequisite, dict)
        and prerequisite.get("accepted") is True
        and prerequisite.get("status") == "ExecutableCandidate"
        and prerequisite.get("source_commit") == expected.source_commit
        and isinstance(prerequisite.get("artifact_digest"), str)
        and re.fullmatch(r"sha256:[0-9a-f]{64}", prerequisite["artifact_digest"])
    )
    if not valid:
        issues.append(
            ValidationIssue(
                "BR8_PREREQUISITE_INVALID",
                "Route B requires an accepted, digest-bound BR8 ExecutableCandidate record.",
                "bundle.json",
            )
        )


def validate_build_bundle(
    bundle_root: Path,
    expected: ExpectedBuildBundle,
    repository_root: Path,
) -> list[ValidationIssue]:
    """Return every integrity, identity, schema, path, payload and secret issue."""

    root = bundle_root.resolve()
    repository_root = repository_root.resolve()
    issues: list[ValidationIssue] = []

    if not root.is_dir():
        return [ValidationIssue("BUNDLE_MISSING", "Bundle directory does not exist.", str(bundle_root))]

    for relative in REQUIRED_PATHS:
        if not (root / relative).is_file():
            issues.append(ValidationIssue("REQUIRED_PATH_MISSING", "Required bundle file is missing.", relative))

    _validate_manifest(root, issues)

    json_payloads: dict[str, object] = {}
    candidates = sorted(candidate for candidate in root.rglob("*") if candidate.is_file())
    for candidate in candidates:
        relative = candidate.relative_to(root).as_posix()
        if not _safe_relative_path(relative):
            issues.append(ValidationIssue("UNSAFE_EVIDENCE_PATH", "File path is not safely contained.", relative))
            continue
        if candidate.suffix.lower() in FORBIDDEN_SUFFIXES:
            issues.append(
                ValidationIssue(
                    "FORBIDDEN_PAYLOAD",
                    f"Forbidden artifact suffix: {candidate.suffix}",
                    relative,
                )
            )
            continue

        try:
            text = candidate.read_text(encoding="utf-8-sig")
        except (OSError, UnicodeError) as error:
            issues.append(ValidationIssue("NON_TEXT_PAYLOAD", str(error), relative))
            continue

        for pattern in SECRET_PATTERNS:
            if pattern in text:
                issues.append(
                    ValidationIssue(
                        "SECRET_PATTERN",
                        f"Matched forbidden secret pattern: {pattern}",
                        relative,
                    )
                )

        if candidate.suffix.lower() != ".json":
            continue
        payload = _load_json(candidate, relative, issues)
        if payload is None:
            continue
        json_payloads[relative] = payload
        for location, evidence_path in _walk_evidence_paths(payload):
            if not _safe_relative_path(evidence_path):
                issues.append(
                    ValidationIssue(
                        "UNSAFE_EVIDENCE_PATH",
                        f"{location}: {evidence_path}",
                        relative,
                    )
                )

    metadata = json_payloads.get("bundle.json")
    if metadata is not None:
        _validate_metadata(metadata, expected, issues)

    decision_count = 0
    for relative, payload in sorted(json_payloads.items()):
        for record_path, record in _record_payloads(relative, payload, issues):
            record_name = record.get("record_type")
            record_type = _RECORD_TYPES.get(record_name) if isinstance(record_name, str) else None
            if record_type is None:
                issues.append(
                    ValidationIssue(
                        "RECORD_INVALID",
                        f"Unknown record_type: {record_name!r}",
                        record_path,
                    )
                )
                continue

            errors = validate_build_record(record_type, record, repository_root)
            for message in errors:
                code = "UNSAFE_EVIDENCE_PATH" if "unsafe evidence path" in message else "RECORD_INVALID"
                issues.append(ValidationIssue(code, message, record_path))

            if record.get("route_id") != expected.route_id:
                issues.append(
                    ValidationIssue(
                        "IDENTITY_MISMATCH",
                        "Record route_id does not match the expected artifact route.",
                        record_path,
                    )
                )
            record_component = record.get("component")
            if record_component in {"runtime", "genai"} and record_component != expected.component:
                issues.append(
                    ValidationIssue(
                        "IDENTITY_MISMATCH",
                        "Record component does not match the expected artifact component.",
                        record_path,
                    )
                )

            if record_type == "decision":
                decision_count += 1
                if record.get("source_commit") != expected.source_commit:
                    issues.append(
                        ValidationIssue(
                            "IDENTITY_MISMATCH",
                            "Decision source_commit does not match the expected artifact source.",
                            record_path,
                        )
                    )

            if record_type == "command":
                for key in ("stdout_path", "stderr_path"):
                    log_path = record.get(key)
                    if not _safe_relative_path(log_path) or not (root / PurePosixPath(log_path)).is_file():
                        issues.append(
                            ValidationIssue(
                                "COMMAND_LOG_MISSING",
                                f"Command record does not resolve to an existing {key} file.",
                                record_path,
                            )
                        )

    if decision_count != 1:
        issues.append(ValidationIssue("DECISION_COUNT_INVALID", f"Expected exactly one build decision record, found {decision_count}.", "decision.json"))

    return sorted(issues, key=lambda issue: (issue.code, issue.path or "", issue.message))


def _render_report(
    expected: ExpectedBuildBundle,
    issues: list[ValidationIssue],
    files_checked: int,
) -> str:
    """Render one deterministic hosted-validation report."""

    lines = [
        "# Workbook 05 documented-build artifact validation",
        "",
        f"- Route: `{expected.route_id}`",
        f"- Component: `{expected.component}`",
        f"- Source commit: `{expected.source_commit}`",
        f"- Workflow run: `{expected.run_id}`",
        f"- Run attempt: `{expected.run_attempt}`",
        f"- Files checked: `{files_checked}`",
        "",
    ]
    if issues:
        lines.append(f"Validation failed with {len(issues)} issue(s).")
        lines.append("")
        lines.extend(
            f"- `{issue.code}` `{issue.path or '-'}` — {issue.message}"
            for issue in issues
        )
    else:
        lines.append("Validation passed: hashes, identity, records, paths, payload types and secret checks are valid.")
    return "\n".join(lines) + "\n"


def main(argv: Iterable[str] | None = None) -> int:
    """CLI entry point used only by the independent GitHub-hosted validator."""

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle", type=Path, required=True)
    parser.add_argument("--repository-root", type=Path, required=True)
    parser.add_argument("--route-id", required=True)
    parser.add_argument("--component", choices=("runtime", "genai"), required=True)
    parser.add_argument("--source-commit", required=True)
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--run-attempt", type=int, required=True)
    parser.add_argument("--report", type=Path, required=True)
    arguments = parser.parse_args(list(argv) if argv is not None else None)

    expected = ExpectedBuildBundle(
        route_id=arguments.route_id,
        component=arguments.component,
        source_commit=arguments.source_commit,
        run_id=arguments.run_id,
        run_attempt=arguments.run_attempt,
    )
    issues = validate_build_bundle(arguments.bundle, expected, arguments.repository_root)
    files_checked = len([candidate for candidate in arguments.bundle.rglob("*") if candidate.is_file()]) if arguments.bundle.is_dir() else 0
    report = _render_report(expected, issues, files_checked)
    arguments.report.parent.mkdir(parents=True, exist_ok=True)
    arguments.report.write_text(report, encoding="utf-8")
    print(report, end="")
    return 1 if issues else 0


if __name__ == "__main__":
    raise SystemExit(main())
