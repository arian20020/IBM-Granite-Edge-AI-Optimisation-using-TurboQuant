"""Validate and requalify one failed Workbook 05 live C1 attempt for resume.

The module never downloads or executes a model. It treats the prior GitHub artifact
as untrusted text-only data, binds it to one independently verified failure, and
rehashes the already retained Granite source before a separate PowerShell
orchestrator may start a new conversion in a new output directory.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import os
import re
import shutil
import stat
from dataclasses import dataclass
from pathlib import Path, PurePosixPath, PureWindowsPath
from typing import Any, Iterable, Mapping, Sequence

from scripts.testing.workbook05.phase3.live_asset_lock import inventory_tree


CAMPAIGN_ID = "GTQ-WB05-MF-v1"
ROUTE_ID = "route-a-merged-openvino"
PRIOR_RUN_ID = "32410714130"
PRIOR_RUN_ATTEMPT = 1
PRIOR_ARTIFACT_NAME = "workbook-05-phase3-assets-32410714130-1"
PRIOR_ARTIFACT_DIGEST = (
    "sha256:33d3ca32236d6da973c21c5d95ba36d088c5ed7384d93eb3f353dbd081671257"
)
PRIOR_HEAD_SHA = "80946fc2e06767a8aeff878deab7d31f10fd6676"
FORMAL_REPOSITORY = "ibm-granite/granite-4.1-3b"
EXPECTED_REVISION = "c0650403e44e78ec0262dab1c90914c65b196c4e"
EXPECTED_SOURCE_DIRECTORY = r"C:\w5m\sources\granite41-3b-c0650403"
EXPECTED_PARTIAL_CONVERSION_DIRECTORY = (
    r"C:\w5m\converted\granite41-3b-int4a-g128-r100-c0650403"
)
EXPECTED_MODEL_SHA256 = (
    "58e7e6635ac57fbf201e4258cc17c01f9ac04a997bbd9cc5315ec52caba79956"
)
EXPECTED_TOKENIZER_SHA256 = (
    "21b6eb2dd3b049017077d62aaab88d38bbc639c6765e4bcb33c2bfc44e463b4e"
)
EXPECTED_DEPENDENCY_DECISION_SHA256 = (
    "429b90548ce2b4c8463941c5b2983c8ff0cf6cc3ea3865375c8cae78d7193b49"
)
EXPECTED_FAILURE_REASON = (
    "Available memory remained below 1.5 GiB for 10 seconds."
)
EXPECTED_COMPLETED_STAGES = (
    "prerequisite-verification",
    "path-root-verification",
    "disk-preflight",
    "immutable-revision-resolution",
    "source-snapshot-download",
    "source-file-hash-inventory",
    "conversion-new-output-directory",
)
CONVERSION_MINIMUM_FREE_BYTES = 20 * 1024 * 1024 * 1024

_SHA256 = re.compile(r"^[0-9a-f]{64}$")
_FULL_REVISION = re.compile(r"^[0-9a-f]{40}$")
_FORBIDDEN_SUFFIXES = {
    ".7z",
    ".bin",
    ".ckpt",
    ".dll",
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
_SECRET_PATTERNS = (
    "ghp_",
    "gho_",
    "ghs_",
    "github_pat_",
    "authorization: bearer ",
    "hf_token=",
    "hugging_face_hub_token=",
    "--token ",
)
_REQUIRED_PRIOR_PATHS = (
    "dependency-acceptance-proof.json",
    "prerequisite-proof.json",
    "resolved-model.json",
    "source-files.csv",
    "failure.json",
    "commands/conversion.json",
    "commands/conversion-native.resources.json",
)
_SUCCESS_ONLY_PATHS = (
    "asset-lock.json",
    "conversion-record.json",
    "converted-files.csv",
    "stage-order.json",
    "summary.md",
    "manifest.sha256",
)
_CLAIM_KEYS = {
    "model_download_authorised",
    "granite_model_test_authorised",
    "model_execution_authorised",
    "activation_claim_authorised",
    "codec_activation_claim_authorised",
    "packed_storage_claim_authorised",
    "performance_claim_authorised",
    "quality_claim_authorised",
}


@dataclass(frozen=True, slots=True)
class ValidationResult:
    """One deterministic validation result used by tests and CLI boundaries."""

    valid: bool
    issues: tuple[str, ...]


def _result(issues: Sequence[str]) -> ValidationResult:
    ordered = tuple(sorted(set(issues), key=lambda value: (value.casefold(), value)))
    return ValidationResult(valid=not ordered, issues=ordered)


def _is_link_or_reparse(path: Path) -> bool:
    """Return whether a path is a symbolic link, junction, or reparse point."""

    try:
        if path.is_symlink():
            return True
        attributes = getattr(path.lstat(), "st_file_attributes", 0)
    except OSError:
        return False
    return bool(attributes & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400))


def _portable_path(value: object) -> bool:
    """Require one canonical, traversal-free, relative POSIX path."""

    if not isinstance(value, str) or not value or value != value.strip():
        return False
    if "\\" in value or "\x00" in value or value.startswith("/"):
        return False
    raw_parts = value.split("/")
    if any(part in {"", ".", ".."} for part in raw_parts):
        return False
    posix = PurePosixPath(value)
    windows = PureWindowsPath(value)
    return (
        posix.as_posix() == value
        and not posix.is_absolute()
        and not windows.is_absolute()
        and not windows.drive
    )


def _load_object(path: Path, label: str, issues: list[str]) -> dict[str, Any] | None:
    """Read one strict UTF-8 JSON object without trusting its shape."""

    if not path.is_file() or _is_link_or_reparse(path):
        issues.append(f"{label} is missing or linked: {path.name}")
        return None
    try:
        value = json.loads(path.read_bytes().decode("utf-8-sig"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        issues.append(f"{label} is invalid JSON: {error}")
        return None
    if not isinstance(value, dict):
        issues.append(f"{label} must contain one JSON object.")
        return None
    return value


def _walk(value: object) -> Iterable[tuple[str, str | None, object]]:
    """Yield every nested value with its parent key for non-claim checks."""

    if isinstance(value, Mapping):
        for key in sorted(value, key=str):
            child = value[key]
            yield str(key), str(key), child
            yield from _walk(child)
    elif isinstance(value, list):
        for child in value:
            yield "[]", None, child
            yield from _walk(child)


def _check_non_claims(label: str, value: Mapping[str, Any], issues: list[str]) -> None:
    for _, key, child in _walk(value):
        if key in _CLAIM_KEYS and child is not False:
            issues.append(f"{label} scientific claim {key} must remain false.")


def _check_text_surface(root: Path, issues: list[str]) -> None:
    """Reject linked, binary, executable, archive, or credential-bearing members."""

    for candidate in sorted(
        root.rglob("*"),
        key=lambda item: item.relative_to(root).as_posix().casefold(),
    ):
        relative = candidate.relative_to(root).as_posix()
        if _is_link_or_reparse(candidate):
            issues.append(f"Prior artifact contains a linked entry: {relative}")
            continue
        if candidate.is_dir():
            continue
        if not candidate.is_file():
            issues.append(f"Prior artifact contains a non-regular entry: {relative}")
            continue
        if candidate.suffix.casefold() in _FORBIDDEN_SUFFIXES:
            issues.append(f"Prior artifact contains a forbidden payload: {relative}")
            continue
        try:
            text = candidate.read_text(encoding="utf-8-sig")
        except (OSError, UnicodeError):
            issues.append(f"Prior artifact contains non-UTF-8 data: {relative}")
            continue
        lowered = text.casefold()
        for pattern in _SECRET_PATTERNS:
            if pattern in lowered:
                issues.append(
                    f"Prior artifact contains forbidden credential text in {relative}: {pattern}"
                )


def _read_inventory(path: Path, issues: list[str]) -> list[dict[str, object]]:
    """Read one source identity CSV with exact columns and stable values."""

    rows: list[dict[str, object]] = []
    try:
        with path.open("r", encoding="utf-8-sig", newline="") as stream:
            reader = csv.DictReader(stream)
            if reader.fieldnames != ["relative_path", "size_bytes", "sha256"]:
                issues.append("source-files.csv has the wrong header.")
                return rows
            seen: set[str] = set()
            for number, row in enumerate(reader, start=2):
                relative = row.get("relative_path")
                if not _portable_path(relative):
                    issues.append(f"source-files.csv row {number} has an unsafe path.")
                    continue
                key = str(relative).casefold()
                if key in seen:
                    issues.append(f"source-files.csv repeats path {relative}.")
                    continue
                seen.add(key)
                try:
                    size = int(row.get("size_bytes", ""))
                except (TypeError, ValueError):
                    size = -1
                digest = row.get("sha256", "")
                if size < 0:
                    issues.append(f"source-files.csv row {number} has an invalid size.")
                if not isinstance(digest, str) or not _SHA256.fullmatch(digest):
                    issues.append(f"source-files.csv row {number} has an invalid digest.")
                rows.append(
                    {
                        "relative_path": str(relative),
                        "size_bytes": size,
                        "sha256": str(digest),
                    }
                )
    except (OSError, UnicodeError, csv.Error) as error:
        issues.append(f"source-files.csv cannot be read: {error}")
    return rows


def _identity_rows(value: object, label: str, issues: list[str]) -> list[dict[str, object]]:
    """Validate one JSON array of path, size, and SHA-256 file identities."""

    if not isinstance(value, list):
        issues.append(f"resolved-model.json {label} must be an array.")
        return []
    rows: list[dict[str, object]] = []
    seen: set[str] = set()
    for index, row in enumerate(value):
        if not isinstance(row, Mapping):
            issues.append(f"resolved-model.json {label}[{index}] must be an object.")
            continue
        relative = row.get("relative_path")
        size = row.get("size_bytes")
        digest = row.get("sha256")
        if not _portable_path(relative):
            issues.append(f"resolved-model.json {label}[{index}] has an unsafe path.")
            continue
        key = str(relative).casefold()
        if key in seen:
            issues.append(f"resolved-model.json {label} repeats path {relative}.")
            continue
        seen.add(key)
        if isinstance(size, bool) or not isinstance(size, int) or size < 0:
            issues.append(f"resolved-model.json {label}[{index}] has an invalid size.")
        if not isinstance(digest, str) or not _SHA256.fullmatch(digest):
            issues.append(f"resolved-model.json {label}[{index}] has an invalid digest.")
        rows.append(
            {
                "relative_path": str(relative),
                "size_bytes": int(size) if isinstance(size, int) and not isinstance(size, bool) else -1,
                "sha256": str(digest),
            }
        )
    return rows


def _aggregate_identities(rows: Sequence[Mapping[str, object]]) -> str:
    """Recompute the canonical tree identity directly from retained records."""

    digest = hashlib.sha256()
    for row in sorted(
        rows,
        key=lambda item: (
            str(item["relative_path"]).casefold(),
            str(item["relative_path"]),
        ),
    ):
        line = (
            f"{row['relative_path']}\0{row['size_bytes']}\0{row['sha256']}\n"
        ).encode("utf-8")
        digest.update(line)
    return digest.hexdigest()


def validate_prior_failure_bundle(
    bundle_root: Path,
    *,
    expected_model_sha256: str | None = None,
    expected_tokenizer_sha256: str | None = None,
) -> ValidationResult:
    """Validate the exact conversion-watchdog attempt as untrusted data."""

    issues: list[str] = []
    try:
        bundle = bundle_root.resolve(strict=True)
    except OSError as error:
        return _result([f"Prior artifact root does not exist: {error}"])
    if not bundle.is_dir() or _is_link_or_reparse(bundle):
        return _result(["Prior artifact root must be one normal directory."])

    _check_text_surface(bundle, issues)
    for relative in _REQUIRED_PRIOR_PATHS:
        if not (bundle / relative).is_file():
            issues.append(f"Prior artifact required path is missing: {relative}")
    for relative in _SUCCESS_ONLY_PATHS:
        if (bundle / relative).exists():
            issues.append(f"Prior artifact contains success-only evidence: {relative}")

    failure = _load_object(bundle / "failure.json", "failure.json", issues)
    resolved = _load_object(bundle / "resolved-model.json", "resolved-model.json", issues)
    dependency = _load_object(
        bundle / "dependency-acceptance-proof.json",
        "dependency-acceptance-proof.json",
        issues,
    )
    prerequisite = _load_object(
        bundle / "prerequisite-proof.json",
        "prerequisite-proof.json",
        issues,
    )
    command = _load_object(
        bundle / "commands" / "conversion.json",
        "commands/conversion.json",
        issues,
    )
    resources = _load_object(
        bundle / "commands" / "conversion-native.resources.json",
        "commands/conversion-native.resources.json",
        issues,
    )

    for label, payload in (
        ("failure.json", failure),
        ("resolved-model.json", resolved),
        ("dependency-acceptance-proof.json", dependency),
        ("prerequisite-proof.json", prerequisite),
        ("commands/conversion.json", command),
        ("commands/conversion-native.resources.json", resources),
    ):
        if payload is not None:
            _check_non_claims(label, payload, issues)

    if failure is not None:
        if failure.get("run_id") != PRIOR_RUN_ID or failure.get("run_attempt") != PRIOR_RUN_ATTEMPT:
            issues.append("failure.json does not match the accepted prior run identity.")
        if failure.get("status") != "Failed":
            issues.append("failure.json status must be Failed.")
        if failure.get("completed_stages") != list(EXPECTED_COMPLETED_STAGES):
            issues.append("failure.json completed stages do not match the accepted boundary.")
        if EXPECTED_FAILURE_REASON not in str(failure.get("message", "")):
            issues.append("failure.json does not record the accepted watchdog reason.")

    model_rows: list[dict[str, object]] = []
    tokenizer_rows: list[dict[str, object]] = []
    if resolved is not None:
        expected_identity = {
            "repository": FORMAL_REPOSITORY,
            "requested_revision": "main",
            "resolved_revision": EXPECTED_REVISION,
            "source_directory": EXPECTED_SOURCE_DIRECTORY,
        }
        for key, expected in expected_identity.items():
            if resolved.get(key) != expected:
                issues.append(f"resolved-model.json {key} does not match the accepted source.")
        if not _FULL_REVISION.fullmatch(str(resolved.get("resolved_revision", ""))):
            issues.append("resolved-model.json revision is not a full lowercase commit.")
        model_rows = _identity_rows(resolved.get("model_files"), "model_files", issues)
        tokenizer_rows = _identity_rows(
            resolved.get("tokenizer_files"),
            "tokenizer_files",
            issues,
        )
        if not model_rows:
            issues.append("resolved-model.json has no model file identities.")
        if not tokenizer_rows:
            issues.append("resolved-model.json has no tokenizer file identities.")
        observed_model = _aggregate_identities(model_rows)
        observed_tokenizer = _aggregate_identities(tokenizer_rows)
        if resolved.get("aggregate_model_sha256") != observed_model:
            issues.append("resolved-model.json aggregate model digest is inconsistent.")
        if resolved.get("aggregate_tokenizer_sha256") != observed_tokenizer:
            issues.append("resolved-model.json aggregate tokenizer digest is inconsistent.")
        if expected_model_sha256 is not None and observed_model != expected_model_sha256:
            issues.append("resolved-model.json model digest differs from the accepted live attempt.")
        if expected_tokenizer_sha256 is not None and observed_tokenizer != expected_tokenizer_sha256:
            issues.append("resolved-model.json tokenizer digest differs from the accepted live attempt.")

    csv_rows = _read_inventory(bundle / "source-files.csv", issues)
    json_rows = sorted(
        model_rows + tokenizer_rows,
        key=lambda row: (str(row["relative_path"]).casefold(), str(row["relative_path"])),
    )
    if csv_rows != json_rows:
        issues.append("source-files.csv does not match resolved-model.json identities.")

    if dependency is not None:
        if dependency.get("status") != "Passed":
            issues.append("dependency-acceptance-proof.json is not Passed.")
        if dependency.get("decision_sha256") != EXPECTED_DEPENDENCY_DECISION_SHA256:
            issues.append("dependency-acceptance-proof.json has the wrong decision digest.")

    if prerequisite is not None and prerequisite.get("status") != "Passed":
        issues.append("prerequisite-proof.json is not Passed.")

    if command is not None:
        expected_arguments = [
            "export",
            "openvino",
            "--model",
            EXPECTED_SOURCE_DIRECTORY,
            "--task",
            "text-generation-with-past",
            "--weight-format",
            "int4",
            "--group-size",
            "128",
            "--ratio",
            "1.0",
            EXPECTED_PARTIAL_CONVERSION_DIRECTORY,
        ]
        if command.get("arguments") != expected_arguments:
            issues.append("commands/conversion.json does not match the reviewed conversion.")
        if command.get("exit_code") != -1 or command.get("safety_stop_triggered") is not True:
            issues.append("commands/conversion.json is not the accepted interrupted conversion.")

    if resources is not None:
        if resources.get("safety_stop_triggered") is not True:
            issues.append("conversion resource record does not show a safety stop.")
        if resources.get("safety_stop_reason") != EXPECTED_FAILURE_REASON:
            issues.append("conversion resource record has the wrong safety-stop reason.")
        sample_count = resources.get("sample_count")
        minimum_available = resources.get("minimum_available_memory_bytes")
        maximum_commit = resources.get("maximum_commit_percent")
        if isinstance(sample_count, bool) or not isinstance(sample_count, int) or sample_count <= 0:
            issues.append("conversion resource record has no real samples.")
        if (
            isinstance(minimum_available, bool)
            or not isinstance(minimum_available, int)
            or minimum_available >= 1610612736
        ):
            issues.append("conversion resource record does not show the accepted low-memory observation.")
        if (
            isinstance(maximum_commit, bool)
            or not isinstance(maximum_commit, (int, float))
            or float(maximum_commit) > 90
        ):
            issues.append("conversion resource record does not show safe commit pressure.")

    return _result(issues)


def validate_retained_source(
    bundle_root: Path,
    source_directory: Path,
    *,
    expected_source_directory: str,
    expected_model_sha256: str | None = None,
    expected_tokenizer_sha256: str | None = None,
) -> ValidationResult:
    """Rehash every retained source file and compare all recorded identities."""

    issues: list[str] = []
    prior = validate_prior_failure_bundle(
        bundle_root,
        expected_model_sha256=expected_model_sha256,
        expected_tokenizer_sha256=expected_tokenizer_sha256,
    )
    issues.extend(prior.issues)
    try:
        source = source_directory.resolve(strict=True)
    except OSError as error:
        return _result([*issues, f"Retained source directory does not exist: {error}"])
    if not source.is_dir() or _is_link_or_reparse(source):
        return _result([*issues, "Retained source must be one normal directory."])
    if os.path.normcase(str(source)) != os.path.normcase(str(Path(expected_source_directory).resolve())):
        issues.append("Retained source path does not match the caller's expected directory.")

    resolved = _load_object(
        bundle_root.resolve() / "resolved-model.json",
        "resolved-model.json",
        issues,
    )
    expected_csv = _read_inventory(bundle_root.resolve() / "source-files.csv", issues)
    try:
        observed_files = inventory_tree(source)
    except ValueError as error:
        return _result([*issues, f"Retained source inventory failed: {error}"])
    observed = [item.as_record() for item in observed_files]

    expected_by_path = {str(row["relative_path"]): row for row in expected_csv}
    observed_by_path = {str(row["relative_path"]): row for row in observed}
    for relative in sorted(set(expected_by_path) - set(observed_by_path), key=str.casefold):
        issues.append(f"Retained source is missing recorded file: {relative}")
    for relative in sorted(set(observed_by_path) - set(expected_by_path), key=str.casefold):
        issues.append(f"Retained source contains unrecorded file: {relative}")
    for relative in sorted(set(expected_by_path) & set(observed_by_path), key=str.casefold):
        expected = expected_by_path[relative]
        actual = observed_by_path[relative]
        if expected.get("size_bytes") != actual.get("size_bytes"):
            issues.append(f"Retained source size drifted for {relative}.")
        if expected.get("sha256") != actual.get("sha256"):
            issues.append(f"Retained source digest drifted for {relative}.")

    if resolved is not None:
        model_rows = _identity_rows(resolved.get("model_files"), "model_files", issues)
        tokenizer_rows = _identity_rows(
            resolved.get("tokenizer_files"),
            "tokenizer_files",
            issues,
        )
        observed_map = {str(row["relative_path"]): row for row in observed}
        observed_model = [observed_map[str(row["relative_path"])] for row in model_rows if str(row["relative_path"]) in observed_map]
        observed_tokenizer = [observed_map[str(row["relative_path"])] for row in tokenizer_rows if str(row["relative_path"]) in observed_map]
        if _aggregate_identities(observed_model) != resolved.get("aggregate_model_sha256"):
            issues.append("Retained source aggregate model digest drifted.")
        if _aggregate_identities(observed_tokenizer) != resolved.get("aggregate_tokenizer_sha256"):
            issues.append("Retained source aggregate tokenizer digest drifted.")

    return _result(issues)


def _write_atomic_json(path: Path, value: Mapping[str, Any]) -> None:
    """Publish one create-once JSON record through a sibling temporary file."""

    if path.exists():
        raise ValueError(f"Refusing to overwrite C1 resume evidence: {path}")
    temporary = path.with_name(path.name + ".tmp")
    if temporary.exists():
        raise ValueError(f"C1 resume temporary evidence already exists: {temporary}")
    path.parent.mkdir(parents=True, exist_ok=True)
    payload = (json.dumps(value, indent=2, ensure_ascii=False) + "\n").encode("utf-8")
    try:
        with temporary.open("xb") as stream:
            stream.write(payload)
            stream.flush()
            os.fsync(stream.fileno())
        temporary.replace(path)
    except Exception:
        if temporary.exists():
            temporary.unlink()
        raise


def _proof_base(record_type: str, status: str) -> dict[str, Any]:
    return {
        "schema_version": "1.0",
        "campaign_id": CAMPAIGN_ID,
        "record_type": record_type,
        "route_id": ROUTE_ID,
        "status": status,
        "model_download_authorised": False,
        "granite_model_test_authorised": False,
        "model_execution_authorised": False,
        "activation_claim_authorised": False,
        "packed_storage_claim_authorised": False,
        "performance_claim_authorised": False,
        "quality_claim_authorised": False,
    }


def _validate_prior_cli(args: argparse.Namespace) -> None:
    result = validate_prior_failure_bundle(
        args.bundle_root,
        expected_model_sha256=args.expected_model_sha256,
        expected_tokenizer_sha256=args.expected_tokenizer_sha256,
    )
    if not result.valid:
        raise ValueError("Prior C1 artifact validation failed: " + "; ".join(result.issues))
    record = _proof_base("c1-prior-artifact-validation", "Passed")
    record.update(
        {
            "prior_run_id": PRIOR_RUN_ID,
            "prior_run_attempt": PRIOR_RUN_ATTEMPT,
            "prior_artifact_name": PRIOR_ARTIFACT_NAME,
            "prior_artifact_digest": PRIOR_ARTIFACT_DIGEST,
            "prior_head_sha": PRIOR_HEAD_SHA,
            "repository": FORMAL_REPOSITORY,
            "resolved_revision": EXPECTED_REVISION,
            "source_directory": EXPECTED_SOURCE_DIRECTORY,
            "aggregate_model_sha256": args.expected_model_sha256,
            "aggregate_tokenizer_sha256": args.expected_tokenizer_sha256,
            "issues": [],
        }
    )
    _write_atomic_json(args.output, record)


def _rehash_source_cli(args: argparse.Namespace) -> None:
    result = validate_retained_source(
        args.bundle_root,
        args.source_directory,
        expected_source_directory=args.expected_source_directory,
        expected_model_sha256=EXPECTED_MODEL_SHA256,
        expected_tokenizer_sha256=EXPECTED_TOKENIZER_SHA256,
    )
    if not result.valid:
        raise ValueError("Retained Granite source requalification failed: " + "; ".join(result.issues))
    resolved = json.loads(
        (args.bundle_root / "resolved-model.json").read_text(encoding="utf-8-sig")
    )
    inventory = inventory_tree(args.source_directory)
    record = _proof_base("c1-retained-source-proof", "Passed")
    record.update(
        {
            "prior_run_id": PRIOR_RUN_ID,
            "repository": FORMAL_REPOSITORY,
            "resolved_revision": EXPECTED_REVISION,
            "source_directory": str(args.source_directory.resolve(strict=True)),
            "file_count": len(inventory),
            "aggregate_model_sha256": resolved["aggregate_model_sha256"],
            "aggregate_tokenizer_sha256": resolved["aggregate_tokenizer_sha256"],
            "source_reused_read_only": True,
            "prior_partial_conversion_reused": False,
            "issues": [],
        }
    )
    _write_atomic_json(args.output, record)


def _conversion_disk_preflight_cli(args: argparse.Namespace) -> None:
    """Record a conversion-only reserve; this command never authorises deletion."""

    try:
        free_bytes = shutil.disk_usage(str(args.drive_root)).free
    except OSError as error:
        raise ValueError(f"Unable to inspect conversion drive: {error}") from error
    minimum = int(args.minimum_free_bytes)
    status = "Passed" if free_bytes >= minimum else "Blocked"
    record = _proof_base("c1-resume-disk-preflight", status)
    record.update(
        {
            "free_bytes": free_bytes,
            "minimum_free_bytes_for_conversion_resume": minimum,
            "source_download_required": False,
            "source_download_authorised": False,
            "deletion_authorised": False,
            "deletion_performed": False,
            "prior_failed_workspace_preserved": True,
            "prior_partial_conversion_preserved": True,
            "reasons": []
            if status == "Passed"
            else [
                f"Conversion resume requires {minimum} free bytes; observed {free_bytes}."
            ],
        }
    )
    _write_atomic_json(args.output, record)
    if status != "Passed":
        raise ValueError(record["reasons"][0])


def _parse_args(argv: Sequence[str] | None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)

    prior = commands.add_parser("validate-prior-bundle")
    prior.add_argument("--bundle-root", type=Path, required=True)
    prior.add_argument("--expected-model-sha256", required=True)
    prior.add_argument("--expected-tokenizer-sha256", required=True)
    prior.add_argument("--output", type=Path, required=True)

    source = commands.add_parser("rehash-retained-source")
    source.add_argument("--bundle-root", type=Path, required=True)
    source.add_argument("--source-directory", type=Path, required=True)
    source.add_argument("--expected-source-directory", required=True)
    source.add_argument("--output", type=Path, required=True)

    disk = commands.add_parser("conversion-disk-preflight")
    disk.add_argument("--drive-root", type=Path, required=True)
    disk.add_argument(
        "--minimum-free-bytes",
        type=int,
        default=CONVERSION_MINIMUM_FREE_BYTES,
    )
    disk.add_argument("--output", type=Path, required=True)
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = _parse_args(argv)
    if args.command == "validate-prior-bundle":
        _validate_prior_cli(args)
    elif args.command == "rehash-retained-source":
        _rehash_source_cli(args)
    elif args.command == "conversion-disk-preflight":
        _conversion_disk_preflight_cli(args)
    else:  # pragma: no cover - argparse enforces the closed command set.
        raise AssertionError(f"Unknown C1 resume command: {args.command}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
