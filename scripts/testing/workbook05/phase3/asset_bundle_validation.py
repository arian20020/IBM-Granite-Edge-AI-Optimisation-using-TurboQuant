"""Validate a Workbook 05 C1 asset bundle strictly as untrusted data.

The validator reads UTF-8 text, JSON, CSV, and the SHA-256 manifest. It never
imports code from the bundle, opens model payloads, extracts archives, or starts
an executable found in the artifact.
"""

from __future__ import annotations

import argparse
import csv
import json
import re
from dataclasses import dataclass
from pathlib import Path, PurePosixPath, PureWindowsPath
from typing import Any, Iterable, Mapping

from scripts.testing.workbook05.hash_manifest import verify_hash_manifest
from scripts.testing.workbook05.phase3.contracts import validate_phase3_record


FORMAL_GRANITE_REPOSITORY = "ibm-granite/granite-4.1-3b"

REQUIRED_PATHS: tuple[str, ...] = (
    "prerequisite-proof.json",
    "asset-lock.json",
    "conversion-record.json",
    "disk-preflight.json",
    "source-files.csv",
    "converted-files.csv",
    "commands/conversion.json",
    "logs/conversion.stdout.txt",
    "logs/conversion.stderr.txt",
    "manifest.sha256",
    "summary.md",
)

FORBIDDEN_SUFFIXES: frozenset[str] = frozenset(
    {
        ".7z",
        ".a",
        ".bin",
        ".ckpt",
        ".dll",
        ".dylib",
        ".exe",
        ".gguf",
        ".gz",
        ".lib",
        ".onnx",
        ".pt",
        ".pth",
        ".pyd",
        ".safetensors",
        ".so",
        ".tar",
        ".tgz",
        ".whl",
        ".xml",
        ".zip",
    }
)

SECRET_PATTERNS: tuple[str, ...] = (
    "ghp_",
    "gho_",
    "ghs_",
    "github_pat_",
    "authorization: bearer ",
    "hf_token=",
    "hugging_face_hub_token=",
    "--token ",
)

SCIENTIFIC_CLAIM_KEYS: frozenset[str] = frozenset(
    {
        "granite_model_test_authorised",
        "model_execution_authorised",
        "activation_claim_authorised",
        "codec_activation_claim_authorised",
        "packed_storage_claim_authorised",
        "performance_claim_authorised",
        "quality_claim_authorised",
    }
)

# Only these fields are portable references inside the text-only artifact.
# Machine-local observations such as `python_executable_path` and `file_path`
# are intentionally excluded and are validated by their own record contracts.
PORTABLE_PATH_KEYS: frozenset[str] = frozenset(
    {
        "adjudication_path",
        "conversion_record_path",
        "deterministic_checks_path",
        "evidence_path",
        "lock_path",
        "raw_output_path",
        "relative_path",
        "report_path",
        "stderr_path",
        "stdout_path",
        "trace_evidence_path",
    }
)

_FULL_REVISION = re.compile(r"^[0-9a-f]{40}$")


@dataclass(frozen=True, slots=True)
class BundleIssue:
    """One stable, reviewer-readable problem in an untrusted C1 bundle."""

    code: str
    path: str
    message: str


def _add(
    issues: list[BundleIssue],
    code: str,
    path: str,
    message: str,
) -> None:
    issues.append(BundleIssue(code=code, path=path, message=message))


def _portable_path(value: object) -> bool:
    """Return whether a value is one canonical traversal-free POSIX path."""

    if not isinstance(value, str) or not value or value != value.strip():
        return False
    if "\\" in value or "\x00" in value or value.startswith("/"):
        return False
    parsed = PurePosixPath(value)
    windows = PureWindowsPath(value)
    return (
        parsed.as_posix() == value
        and not parsed.is_absolute()
        and not windows.is_absolute()
        and not windows.drive
        and all(part not in {"", ".", ".."} for part in parsed.parts)
    )


def _load_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected a JSON object: {path}")
    return value


def _walk_values(
    value: object,
    *,
    path: str = "$",
) -> Iterable[tuple[str, str | None, object]]:
    """Yield JSON values with their parent key and deterministic JSON path."""

    if isinstance(value, Mapping):
        for key in sorted(value, key=str):
            child_path = f"{path}.{key}"
            child = value[key]
            yield child_path, str(key), child
            yield from _walk_values(child, path=child_path)
    elif isinstance(value, list):
        for index, child in enumerate(value):
            child_path = f"{path}[{index}]"
            yield child_path, None, child
            yield from _walk_values(child, path=child_path)


def _check_portable_paths_and_claims(
    relative: str,
    payload: Mapping[str, Any],
    issues: list[BundleIssue],
) -> None:
    for json_path, key, value in _walk_values(payload):
        if key in PORTABLE_PATH_KEYS and value is not None:
            if not _portable_path(value):
                _add(
                    issues,
                    "UNSAFE_EVIDENCE_PATH",
                    relative,
                    f"{json_path} is not a portable relative evidence path.",
                )
        if key in SCIENTIFIC_CLAIM_KEYS and value is not False:
            _add(
                issues,
                "SCIENTIFIC_CLAIM",
                relative,
                f"{json_path} must remain false at the C1 boundary.",
            )


def _check_text_surface(
    bundle_root: Path,
    issues: list[BundleIssue],
) -> None:
    """Reject linked, binary, forbidden, or credential-bearing bundle files."""

    for candidate in sorted(
        bundle_root.rglob("*"),
        key=lambda item: item.relative_to(bundle_root).as_posix().casefold(),
    ):
        relative = candidate.relative_to(bundle_root).as_posix()
        if candidate.is_symlink():
            _add(
                issues,
                "LINKED_PAYLOAD",
                relative,
                "Evidence files and directories must not be symbolic links.",
            )
            continue
        if candidate.is_dir():
            continue
        if not candidate.is_file():
            _add(
                issues,
                "NONREGULAR_PAYLOAD",
                relative,
                "The evidence member is not a regular file.",
            )
            continue
        if candidate.suffix.casefold() in FORBIDDEN_SUFFIXES:
            _add(
                issues,
                "FORBIDDEN_PAYLOAD",
                relative,
                "Model, executable, library, archive, and IR payloads are forbidden.",
            )
            continue
        try:
            text = candidate.read_text(encoding="utf-8-sig")
        except UnicodeDecodeError:
            _add(
                issues,
                "BINARY_PAYLOAD",
                relative,
                "The artifact member is not strict UTF-8 text.",
            )
            continue
        lowered = text.casefold()
        for pattern in SECRET_PATTERNS:
            if pattern in lowered:
                _add(
                    issues,
                    "SECRET_PATTERN",
                    relative,
                    f"The text contains a forbidden credential pattern: {pattern}",
                )


def _check_required_and_manifest(
    bundle_root: Path,
    issues: list[BundleIssue],
) -> None:
    for relative in REQUIRED_PATHS:
        if not (bundle_root / relative).is_file():
            _add(
                issues,
                "REQUIRED_PATH_MISSING",
                relative,
                "The required C1 evidence file is missing.",
            )

    manifest = bundle_root / "manifest.sha256"
    if manifest.is_file():
        for message in verify_hash_manifest(bundle_root, manifest):
            _add(issues, "HASH_MISMATCH", "manifest.sha256", message)


def _check_schema_record(
    record_type: str,
    relative: str,
    payload: Mapping[str, Any],
    repository_root: Path,
    issues: list[BundleIssue],
) -> None:
    try:
        schema_issues = validate_phase3_record(
            record_type,
            payload,
            repository_root,
        )
    except Exception as error:  # Fail closed while retaining a readable report.
        _add(issues, "SCHEMA_VALIDATOR_FAILURE", relative, str(error))
        return
    for issue in schema_issues:
        _add(
            issues,
            "SCHEMA_INVALID",
            relative,
            f"{issue.json_path}: {issue.message}",
        )


def _check_csv(relative: str, path: Path, issues: list[BundleIssue]) -> None:
    try:
        with path.open("r", encoding="utf-8-sig", newline="") as stream:
            reader = csv.DictReader(stream)
            if reader.fieldnames != ["relative_path", "size_bytes", "sha256"]:
                _add(
                    issues,
                    "CSV_HEADER",
                    relative,
                    "Expected relative_path,size_bytes,sha256 exactly.",
                )
                return
            seen: set[str] = set()
            for row_number, row in enumerate(reader, start=2):
                path_value = row.get("relative_path")
                if not _portable_path(path_value):
                    _add(
                        issues,
                        "UNSAFE_EVIDENCE_PATH",
                        relative,
                        f"Row {row_number} has an unsafe relative_path.",
                    )
                key = (path_value or "").casefold()
                if key in seen:
                    _add(
                        issues,
                        "CSV_DUPLICATE",
                        relative,
                        f"Row {row_number} repeats a canonical path.",
                    )
                seen.add(key)
                try:
                    size = int(row.get("size_bytes", ""))
                except ValueError:
                    size = -1
                if size < 0:
                    _add(
                        issues,
                        "CSV_SIZE",
                        relative,
                        f"Row {row_number} has an invalid size_bytes value.",
                    )
                if not re.fullmatch(r"[0-9a-f]{64}", row.get("sha256", "")):
                    _add(
                        issues,
                        "CSV_HASH",
                        relative,
                        f"Row {row_number} has an invalid SHA-256 value.",
                    )
    except (OSError, UnicodeError, csv.Error) as error:
        _add(issues, "CSV_READ_FAILURE", relative, str(error))


def _check_command(
    payload: Mapping[str, Any],
    issues: list[BundleIssue],
) -> None:
    arguments = payload.get("arguments")
    if (
        not isinstance(arguments, list)
        or not arguments
        or not all(isinstance(item, str) and item for item in arguments)
    ):
        _add(
            issues,
            "COMMAND_ARGUMENTS",
            "commands/conversion.json",
            "The conversion command must be a non-empty string argument array.",
        )
        return
    if "--trust-remote-code" in arguments:
        _add(
            issues,
            "REMOTE_CODE",
            "commands/conversion.json",
            "Remote model code is forbidden for the reviewed C1 candidate.",
        )
    if arguments[:2] != ["export", "openvino"]:
        _add(
            issues,
            "COMMAND_IDENTITY",
            "commands/conversion.json",
            "The command must begin with the reviewed `export openvino` arguments.",
        )
    if any("\n" in item or "\r" in item or "\x00" in item for item in arguments):
        _add(
            issues,
            "COMMAND_ARGUMENTS",
            "commands/conversion.json",
            "Command arguments must not contain control separators.",
        )


def validate_asset_bundle(
    bundle_root: Path,
    repository_root: Path,
) -> list[BundleIssue]:
    """Return every deterministic issue in one text-only C1 evidence bundle."""

    issues: list[BundleIssue] = []
    try:
        bundle = bundle_root.resolve(strict=True)
        repository = repository_root.resolve(strict=True)
    except OSError as error:
        return [BundleIssue("BUNDLE_PATH", str(bundle_root), str(error))]
    if not bundle.is_dir() or bundle_root.is_symlink():
        return [
            BundleIssue(
                "BUNDLE_PATH",
                str(bundle_root),
                "The bundle root must be a normal existing directory.",
            )
        ]

    _check_text_surface(bundle, issues)
    _check_required_and_manifest(bundle, issues)

    records: dict[str, dict[str, Any]] = {}
    bindings = {
        "prerequisite-proof.json": "prerequisite-proof",
        "asset-lock.json": "model-asset-lock",
        "conversion-record.json": "model-conversion-record",
    }
    for relative, record_type in bindings.items():
        path = bundle / relative
        if not path.is_file():
            continue
        try:
            payload = _load_json(path)
        except (OSError, UnicodeError, json.JSONDecodeError, ValueError) as error:
            _add(issues, "JSON_INVALID", relative, str(error))
            continue
        records[relative] = payload
        _check_schema_record(
            record_type,
            relative,
            payload,
            repository,
            issues,
        )
        _check_portable_paths_and_claims(relative, payload, issues)

    disk_path = bundle / "disk-preflight.json"
    if disk_path.is_file():
        try:
            disk_payload = _load_json(disk_path)
            _check_portable_paths_and_claims(
                "disk-preflight.json",
                disk_payload,
                issues,
            )
            if disk_payload.get("deletion_authorised") is not False:
                _add(
                    issues,
                    "DELETION_AUTHORITY",
                    "disk-preflight.json",
                    "C1 disk evidence must never authorise deletion.",
                )
            if disk_payload.get("model_download_authorised") is not False:
                _add(
                    issues,
                    "DOWNLOAD_AUTHORITY",
                    "disk-preflight.json",
                    "Disk preflight alone must not authorise model download.",
                )
        except (OSError, UnicodeError, json.JSONDecodeError, ValueError) as error:
            _add(issues, "JSON_INVALID", "disk-preflight.json", str(error))

    asset = records.get("asset-lock.json")
    conversion = records.get("conversion-record.json")
    if asset is not None:
        source = asset.get("source")
        if not isinstance(source, Mapping):
            _add(
                issues,
                "MODEL_IDENTITY",
                "asset-lock.json",
                "The asset source identity is missing.",
            )
        else:
            repository_name = source.get("repository")
            revision = source.get("resolved_revision")
            if repository_name != FORMAL_GRANITE_REPOSITORY:
                _add(
                    issues,
                    "MODEL_IDENTITY",
                    "asset-lock.json",
                    "The formal C1 asset must use the official Granite 4.1 3B repository.",
                )
            if not isinstance(revision, str) or not _FULL_REVISION.fullmatch(
                revision
            ):
                _add(
                    issues,
                    "MODEL_IDENTITY",
                    "asset-lock.json",
                    "The resolved model revision must be a full lowercase commit.",
                )

    if asset is not None and conversion is not None:
        asset_status = asset.get("status")
        conversion_status = conversion.get("status")
        if asset_status in {"Accepted", "Candidate", "Passed"} and (
            conversion_status not in {"Candidate", "Passed"}
        ):
            _add(
                issues,
                "CONVERSION_RELATIONSHIP",
                "conversion-record.json",
                "A positive asset record cannot reference a failed conversion.",
            )

    command_path = bundle / "commands" / "conversion.json"
    if command_path.is_file():
        try:
            command_payload = _load_json(command_path)
            _check_portable_paths_and_claims(
                "commands/conversion.json",
                command_payload,
                issues,
            )
            _check_command(command_payload, issues)
        except (OSError, UnicodeError, json.JSONDecodeError, ValueError) as error:
            _add(issues, "JSON_INVALID", "commands/conversion.json", str(error))

    for relative in ("source-files.csv", "converted-files.csv"):
        path = bundle / relative
        if path.is_file():
            _check_csv(relative, path, issues)

    return sorted(
        issues,
        key=lambda issue: (issue.path.casefold(), issue.code, issue.message),
    )


def _write_report(path: Path, issues: list[BundleIssue]) -> None:
    lines = ["# Workbook 05 C1 asset-bundle validation", ""]
    if not issues:
        lines.append(
            "Validation passed: required text evidence, hashes, identities, "
            "paths, payload types, command boundaries, and non-claims are valid."
        )
    else:
        lines.append(f"Validation failed with {len(issues)} issue(s).")
        lines.append("")
        for issue in issues:
            lines.append(
                f"- `{issue.code}` `{issue.path}` — {issue.message}"
            )
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")


def main(argv: Iterable[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle-root", type=Path, required=True)
    parser.add_argument("--repository-root", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    arguments = parser.parse_args(list(argv) if argv is not None else None)

    issues = validate_asset_bundle(
        arguments.bundle_root,
        arguments.repository_root,
    )
    _write_report(arguments.report, issues)
    print(arguments.report.read_text(encoding="utf-8"), end="")
    return 0 if not issues else 1


if __name__ == "__main__":
    raise SystemExit(main())
