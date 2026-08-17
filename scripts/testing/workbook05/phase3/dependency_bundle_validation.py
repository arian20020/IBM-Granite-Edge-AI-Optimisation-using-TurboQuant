"""Validate a complete dependency-preflight artifact strictly as untrusted data."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import os
import re
import stat
from dataclasses import dataclass
from pathlib import Path, PurePosixPath, PureWindowsPath
from typing import Any, Iterable, Mapping, Sequence

from jsonschema import Draft202012Validator, FormatChecker

from scripts.testing.workbook05.hash_manifest import verify_hash_manifest
from scripts.testing.workbook05.phase3.conversion import (
    OPTIMUM_COMMIT,
    OPTIMUM_INTEL_COMMIT,
)
from scripts.testing.workbook05.phase3.dependency_decision import (
    BOOTSTRAP_DIRECT_VERSIONS,
    build_live_dependency_preflight_record,
)
from scripts.testing.workbook05.phase3.dependency_lock import (
    DIRECT_NORMAL_VERSIONS,
    VCS_PACKAGE_NAMES,
    LockedDistribution,
    parse_hash_locked_requirements,
    parse_install_report_against_lock,
    parse_normal_install_report,
)


STAGE_ORDER: tuple[str, ...] = (
    "workspace-validation",
    "source-verification",
    "lock-generation",
    "normal-install",
    "vcs-install",
    "imports",
    "cli-help",
    "no-model-compatibility",
    "record-generation",
    "manifest-generation",
)
CHECK_ORDER: tuple[str, ...] = (
    "resolver",
    "install",
    "imports",
    "cli_help",
    "no_model_compatibility",
    "remote_code_disabled",
)
REQUIRED_PATHS: tuple[str, ...] = (
    "decision.json",
    "observation.json",
    "locks/requirements.phase3-bootstrap.txt",
    "locks/requirements.phase3-assets.txt",
    "reports/bootstrap-install-report.json",
    "reports/normal-install-report.json",
    "reports/source-contracts.json",
    "reports/normal-packages.json",
    "reports/vcs-packages.json",
    "reports/final-environment-packages.json",
    "reports/no-model-compatibility.json",
    "sources/optimum.json",
    "sources/optimum.csv",
    "sources/optimum-intel.json",
    "sources/optimum-intel.csv",
    "checks.json",
    "command-index.json",
    "stage-order.json",
    "summary.md",
    "manifest.sha256",
)
EXPECTED_SOURCES: dict[str, dict[str, str]] = {
    "optimum": {
        "repository": "huggingface/optimum",
        "origin": "https://github.com/huggingface/optimum.git",
        "commit": OPTIMUM_COMMIT,
        "base_version": "2.3.0",
    },
    "optimum-intel": {
        "repository": "huggingface/optimum-intel",
        "origin": "https://github.com/huggingface/optimum-intel.git",
        "commit": OPTIMUM_INTEL_COMMIT,
        "base_version": "2.2.0.dev0",
    },
}
CLAIM_KEYS: tuple[str, ...] = (
    "model_download_authorised",
    "granite_model_test_authorised",
    "activation_claim_authorised",
    "packed_storage_claim_authorised",
    "performance_claim_authorised",
    "quality_claim_authorised",
)
FORBIDDEN_SUFFIXES = frozenset(
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
    "begin rsa private key",
    "begin openssh private key",
    "begin ec private key",
)
_SHA256 = re.compile(r"^[0-9a-f]{64}$")


@dataclass(frozen=True, slots=True)
class DependencyBundleIssue:
    code: str
    path: str
    message: str


def _add(
    issues: list[DependencyBundleIssue],
    code: str,
    path: str,
    message: str,
) -> None:
    issues.append(DependencyBundleIssue(code, path, message))


def _load_object(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected one JSON object: {path}")
    return value


def _canonical_name(value: str) -> str:
    return re.sub(r"[-_.]+", "-", value).casefold()


def _safe_relative(value: object) -> str | None:
    if not isinstance(value, str) or not value or "\\" in value:
        return None
    parsed = PurePosixPath(value)
    if (
        parsed.is_absolute()
        or PureWindowsPath(value).drive
        or value.startswith("/")
        or value.endswith("/")
        or "//" in value
        or any(part in {"", ".", ".."} for part in parsed.parts)
    ):
        return None
    return value


def _is_reparse(path: Path) -> bool:
    try:
        details = path.lstat()
    except OSError:
        return False
    attributes = getattr(details, "st_file_attributes", 0)
    return bool(attributes & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0))


def _scan_payload(bundle: Path, issues: list[DependencyBundleIssue]) -> None:
    seen: dict[str, str] = {}
    for candidate in sorted(
        bundle.rglob("*"),
        key=lambda value: value.relative_to(bundle).as_posix().casefold(),
    ):
        relative = candidate.relative_to(bundle).as_posix()
        folded = relative.casefold()
        prior = seen.get(folded)
        if prior is not None and prior != relative:
            _add(
                issues,
                "CASE_COLLIDING_PATH",
                relative,
                f"Artifact paths collide case-insensitively: {prior}",
            )
        else:
            seen[folded] = relative

        if candidate.is_symlink() or _is_reparse(candidate):
            _add(
                issues,
                "LINKED_PAYLOAD",
                relative,
                "Artifact members must not be links, junctions, or reparse points.",
            )
            continue
        if candidate.is_dir():
            continue
        if not candidate.is_file():
            _add(
                issues,
                "NONREGULAR_PAYLOAD",
                relative,
                "Artifact members must be regular files.",
            )
            continue
        if candidate.suffix.casefold() in FORBIDDEN_SUFFIXES:
            _add(
                issues,
                "FORBIDDEN_PAYLOAD",
                relative,
                "Executables, libraries, wheels, archives, models, and IR are forbidden.",
            )
            continue
        try:
            text = candidate.read_text(encoding="utf-8-sig")
        except UnicodeDecodeError:
            _add(
                issues,
                "BINARY_PAYLOAD",
                relative,
                "Artifact members must contain strict UTF-8 text.",
            )
            continue
        lowered = text.casefold()
        for pattern in SECRET_PATTERNS:
            if pattern in lowered:
                _add(
                    issues,
                    "SECRET_PATTERN",
                    relative,
                    f"Forbidden credential pattern found: {pattern}",
                )


def _check_required_and_manifest(
    bundle: Path,
    issues: list[DependencyBundleIssue],
) -> None:
    for relative in REQUIRED_PATHS:
        if not (bundle / relative).is_file():
            _add(
                issues,
                "REQUIRED_PATH_MISSING",
                relative,
                "Required dependency-preflight evidence is missing.",
            )
    manifest = bundle / "manifest.sha256"
    if manifest.is_file():
        for message in verify_hash_manifest(bundle, manifest):
            _add(issues, "HASH_MISMATCH", "manifest.sha256", message)


def _check_decision_schema(
    decision: Mapping[str, Any],
    repository: Path,
    issues: list[DependencyBundleIssue],
) -> None:
    schema_path = (
        repository
        / "experiments/granite_turboquant_intel/schemas/workbook05"
        / "conversion-dependency-preflight.schema.json"
    )
    try:
        schema = _load_object(schema_path)
        errors = sorted(
            Draft202012Validator(
                schema,
                format_checker=FormatChecker(),
            ).iter_errors(decision),
            key=lambda error: (
                tuple(str(part) for part in error.absolute_path),
                error.message,
            ),
        )
    except Exception as error:
        _add(issues, "SCHEMA_VALIDATOR_FAILURE", "decision.json", str(error))
        return
    for error in errors:
        json_path = "$"
        for part in error.absolute_path:
            json_path += f"[{part}]" if isinstance(part, int) else f".{part}"
        _add(
            issues,
            "SCHEMA_INVALID",
            "decision.json",
            f"{json_path}: {error.message}",
        )


def _check_claims(
    decision: Mapping[str, Any],
    issues: list[DependencyBundleIssue],
) -> None:
    for key in CLAIM_KEYS:
        if decision.get(key) is not False:
            _add(
                issues,
                "SCIENTIFIC_CLAIM",
                "decision.json",
                f"{key} must remain false at this boundary.",
            )


def _load_lock_and_report(
    bundle: Path,
    lock_relative: str,
    report_relative: str,
    *,
    required: Mapping[str, str],
    bootstrap: bool,
    issues: list[DependencyBundleIssue],
) -> tuple[tuple[LockedDistribution, ...], dict[str, Any], str, str] | None:
    lock_path = bundle / lock_relative
    report_path = bundle / report_relative
    if not lock_path.is_file() or not report_path.is_file():
        return None
    code = (
        "BOOTSTRAP_LOCK_REPORT_MISMATCH"
        if bootstrap
        else "NORMAL_LOCK_REPORT_MISMATCH"
    )
    try:
        lock_text = lock_path.read_text(encoding="utf-8")
        report_text = report_path.read_text(encoding="utf-8")
        report = json.loads(report_text)
        if not isinstance(report, Mapping):
            raise ValueError("pip report must contain one JSON object.")
        lock = parse_hash_locked_requirements(
            lock_text,
            required_direct_versions=required,
            forbidden_names=VCS_PACKAGE_NAMES,
        )
        if bootstrap:
            parse_install_report_against_lock(
                report,
                lock,
                forbidden_names=VCS_PACKAGE_NAMES,
            )
        else:
            parse_normal_install_report(report, lock)
    except (OSError, UnicodeError, json.JSONDecodeError, ValueError) as error:
        _add(issues, code, report_relative, str(error))
        return None
    return lock, dict(report), lock_text, report_text


def _check_lock_observation_relationships(
    observation: Mapping[str, Any],
    bootstrap_data: tuple[Sequence[LockedDistribution], Mapping[str, Any], str, str] | None,
    normal_data: tuple[Sequence[LockedDistribution], Mapping[str, Any], str, str] | None,
    issues: list[DependencyBundleIssue],
) -> None:
    bindings = (
        (
            bootstrap_data,
            "bootstrap_lock_text",
            "bootstrap_lock_sha256",
            "bootstrap_install_report_text",
            "bootstrap_install_report_sha256",
            "BOOTSTRAP_LOCK_REPORT_MISMATCH",
        ),
        (
            normal_data,
            "lock_text",
            "lock_sha256",
            None,
            None,
            "NORMAL_LOCK_REPORT_MISMATCH",
        ),
    )
    for data, text_key, sha_key, report_key, report_sha_key, code in bindings:
        if data is None:
            continue
        _, _, lock_text, report_text = data
        actual_lock_sha = hashlib.sha256(lock_text.encode("utf-8")).hexdigest()
        if observation.get(text_key) != lock_text or observation.get(sha_key) != actual_lock_sha:
            _add(
                issues,
                code,
                "observation.json",
                f"Observation {text_key}/{sha_key} differs from retained bytes.",
            )
        if report_key is not None:
            actual_report_sha = hashlib.sha256(
                report_text.encode("utf-8")
            ).hexdigest()
            if (
                observation.get(report_key) != report_text
                or observation.get(report_sha_key) != actual_report_sha
            ):
                _add(
                    issues,
                    code,
                    "observation.json",
                    "Observation bootstrap report identity differs from retained bytes.",
                )


def _read_source_csv(
    path: Path,
    relative: str,
    issues: list[DependencyBundleIssue],
) -> tuple[int, str] | None:
    try:
        with path.open("r", encoding="utf-8-sig", newline="") as handle:
            reader = csv.DictReader(handle)
            if reader.fieldnames != ["relative_path", "size_bytes", "sha256"]:
                raise ValueError("Source CSV columns changed.")
            rows = list(reader)
    except (OSError, UnicodeError, csv.Error, ValueError) as error:
        _add(issues, "SOURCE_MANIFEST_DRIFT", relative, str(error))
        return None
    if not rows:
        _add(issues, "SOURCE_MANIFEST_DRIFT", relative, "Source CSV is empty.")
        return None

    seen: set[str] = set()
    digest = hashlib.sha256()
    for row in rows:
        source_path = row.get("relative_path")
        safe = _safe_relative(source_path)
        folded = str(source_path).casefold()
        if safe is None or folded in seen:
            _add(
                issues,
                "SOURCE_MANIFEST_DRIFT",
                relative,
                f"Unsafe or duplicate source path: {source_path}",
            )
            continue
        seen.add(folded)
        try:
            size = int(str(row.get("size_bytes", "")))
        except ValueError:
            size = -1
        sha = str(row.get("sha256", ""))
        if size < 0 or not _SHA256.fullmatch(sha):
            _add(
                issues,
                "SOURCE_MANIFEST_DRIFT",
                relative,
                f"Invalid size or digest for source path: {source_path}",
            )
            continue
        digest.update(f"{safe}\0{size}\0{sha}\n".encode("utf-8"))
    return len(rows), digest.hexdigest()


def _check_sources(
    bundle: Path,
    observation: Mapping[str, Any],
    issues: list[DependencyBundleIssue],
) -> None:
    source_rows = observation.get("source_trees")
    observed = {
        str(row.get("name")): row
        for row in source_rows
        if isinstance(row, Mapping)
    } if isinstance(source_rows, list) else {}

    try:
        contracts = _load_object(bundle / "reports/source-contracts.json")
    except Exception as error:
        _add(issues, "SOURCE_CONTRACT_DRIFT", "reports/source-contracts.json", str(error))
        contracts = {}
    contract_sources = contracts.get("sources")
    if contracts.get("source_metadata_execution") is not False or not isinstance(
        contract_sources, Mapping
    ):
        _add(
            issues,
            "SOURCE_CONTRACT_DRIFT",
            "reports/source-contracts.json",
            "Source contracts must remain data-only and include both sources.",
        )
        contract_sources = {}

    for name, expected in EXPECTED_SOURCES.items():
        json_relative = f"sources/{name}.json"
        csv_relative = f"sources/{name}.csv"
        try:
            source = _load_object(bundle / json_relative)
        except Exception as error:
            _add(issues, "VCS_IDENTITY", json_relative, str(error))
            continue
        for key in ("repository", "origin", "commit"):
            if source.get(key) != expected[key]:
                _add(
                    issues,
                    "VCS_IDENTITY",
                    json_relative,
                    f"{key} differs from the reviewed source identity.",
                )
        if source.get("clean") is not True:
            _add(issues, "VCS_IDENTITY", json_relative, "Source checkout is not clean.")

        csv_result = _read_source_csv(bundle / csv_relative, csv_relative, issues)
        if csv_result is not None:
            count, aggregate = csv_result
            if (
                source.get("file_count") != count
                or source.get("aggregate_sha256") != aggregate
                or source.get("manifest_path") != csv_relative
            ):
                _add(
                    issues,
                    "SOURCE_MANIFEST_DRIFT",
                    json_relative,
                    "Source summary does not recompute from its CSV rows.",
                )

        observed_row = observed.get(name)
        if not isinstance(observed_row, Mapping) or any(
            observed_row.get(key) != source.get(key)
            for key in (
                "repository",
                "origin",
                "commit",
                "clean",
                "file_count",
                "aggregate_sha256",
                "manifest_path",
            )
        ):
            _add(
                issues,
                "VCS_IDENTITY",
                "observation.json",
                f"Observation source identity differs for {name}.",
            )

        contract = contract_sources.get(name)
        if not isinstance(contract, Mapping) or (
            contract.get("name") != name
            or contract.get("base_version") != expected["base_version"]
            or contract.get("reviewed_commit") != expected["commit"]
        ):
            _add(
                issues,
                "SOURCE_CONTRACT_DRIFT",
                "reports/source-contracts.json",
                f"Reviewed source contract drifted for {name}.",
            )


def _package_rows(path: Path) -> list[dict[str, str]]:
    value = _load_object(path).get("packages")
    if not isinstance(value, list):
        raise ValueError(f"Package report has no packages array: {path}")
    rows: list[dict[str, str]] = []
    seen: set[str] = set()
    for raw in value:
        if not isinstance(raw, Mapping):
            raise ValueError(f"Package row is not an object: {path}")
        name = _canonical_name(str(raw.get("name", "")))
        version = str(raw.get("version", ""))
        if not name or not version or name in seen:
            raise ValueError(f"Package report contains an invalid duplicate: {name}")
        seen.add(name)
        rows.append({"name": name, "version": version})
    return rows


def _check_final_packages(
    bundle: Path,
    normal_lock: Sequence[LockedDistribution] | None,
    issues: list[DependencyBundleIssue],
) -> None:
    if normal_lock is None:
        return
    try:
        normal_rows = _package_rows(bundle / "reports/normal-packages.json")
        vcs_document = _load_object(bundle / "reports/vcs-packages.json")
        vcs_values = vcs_document.get("packages")
        if not isinstance(vcs_values, list):
            raise ValueError("VCS package report has no packages array.")
        vcs_rows = []
        for row in vcs_values:
            if not isinstance(row, Mapping):
                raise ValueError("VCS package row is not an object.")
            name = _canonical_name(str(row.get("name", "")))
            version = str(row.get("version", ""))
            commit = str(row.get("commit", ""))
            expected = EXPECTED_SOURCES.get(name)
            if (
                expected is None
                or commit != expected["commit"]
                or not version
            ):
                raise ValueError(f"VCS package identity drifted: {name}")
            vcs_rows.append({"name": name, "version": version})
        final_rows = _package_rows(
            bundle / "reports/final-environment-packages.json"
        )
    except Exception as error:
        _add(
            issues,
            "FINAL_PACKAGE_SET_DRIFT",
            "reports/final-environment-packages.json",
            str(error),
        )
        return

    locked = {row.name: row.version for row in normal_lock}
    if {row["name"]: row["version"] for row in normal_rows} != locked:
        _add(
            issues,
            "FINAL_PACKAGE_SET_DRIFT",
            "reports/normal-packages.json",
            "Normal package inventory differs from the retained lock.",
        )

    expected_final = dict(locked)
    expected_final.update({row["name"]: row["version"] for row in vcs_rows})
    final_map = {row["name"]: row["version"] for row in final_rows}
    pip_version = final_map.pop("pip", None)
    if not pip_version or final_map != expected_final:
        _add(
            issues,
            "FINAL_PACKAGE_SET_DRIFT",
            "reports/final-environment-packages.json",
            "Final environment must equal pip plus the lock and two VCS packages.",
        )


def _check_checks_and_stages(
    bundle: Path,
    observation: Mapping[str, Any],
    issues: list[DependencyBundleIssue],
) -> None:
    try:
        checks = _load_object(bundle / "checks.json").get("checks")
    except Exception as error:
        _add(issues, "CHECK_ORDER_DRIFT", "checks.json", str(error))
        checks = None
    if not isinstance(checks, list) or [
        row.get("name") for row in checks if isinstance(row, Mapping)
    ] != list(CHECK_ORDER):
        _add(
            issues,
            "CHECK_ORDER_DRIFT",
            "checks.json",
            "Dependency checks are not in the exact reviewed order.",
        )
    elif any(
        not isinstance(row, Mapping) or row.get("status") != "Passed"
        for row in checks
    ):
        _add(
            issues,
            "CHECK_STATUS_DRIFT",
            "checks.json",
            "Every live dependency check must be Passed.",
        )
    if observation.get("checks") != checks:
        _add(
            issues,
            "CHECK_ORDER_DRIFT",
            "observation.json",
            "Observation checks differ from checks.json.",
        )

    try:
        stages = _load_object(bundle / "stage-order.json")
    except Exception as error:
        _add(issues, "STAGE_ORDER_DRIFT", "stage-order.json", str(error))
        return
    if (
        stages.get("stage_order") != list(STAGE_ORDER)
        or stages.get("completed_stages") != list(STAGE_ORDER)
        or stages.get("current_stage") is not None
    ):
        _add(
            issues,
            "STAGE_ORDER_DRIFT",
            "stage-order.json",
            "Passed evidence must retain the exact complete stage sequence.",
        )


def _check_command_index(
    bundle: Path,
    issues: list[DependencyBundleIssue],
) -> None:
    try:
        commands = _load_object(bundle / "command-index.json").get("commands")
    except Exception as error:
        _add(issues, "COMMAND_INDEX_INVALID", "command-index.json", str(error))
        return
    if not isinstance(commands, list) or not commands:
        _add(
            issues,
            "COMMAND_INDEX_INVALID",
            "command-index.json",
            "Command index must contain at least one command.",
        )
        return

    seen: set[str] = set()
    for index, row in enumerate(commands):
        index_path = f"command-index.json#commands[{index}]"
        if not isinstance(row, Mapping):
            _add(issues, "COMMAND_INDEX_INVALID", index_path, "Command row is not an object.")
            continue
        command_id = str(row.get("command_id", ""))
        if not command_id or command_id in seen:
            _add(issues, "COMMAND_INDEX_INVALID", index_path, "Command ID is missing or duplicated.")
        seen.add(command_id)
        for key in (
            "record_path",
            "stdout_path",
            "stderr_path",
            "resource_summary_path",
            "resource_csv_path",
        ):
            relative = _safe_relative(row.get(key))
            if relative is None:
                _add(
                    issues,
                    "COMMAND_INDEX_PATH_UNSAFE",
                    index_path,
                    f"Unsafe command evidence path in {key}.",
                )
                continue
            candidate = bundle / relative
            if not candidate.is_file():
                _add(
                    issues,
                    "COMMAND_EVIDENCE_MISSING",
                    relative,
                    "Indexed command evidence is missing.",
                )
        record_relative = _safe_relative(row.get("record_path"))
        if record_relative and (bundle / record_relative).is_file():
            try:
                record = _load_object(bundle / record_relative)
                if record.get("command_id") != command_id or record.get("exit_code") != 0:
                    raise ValueError("Command record identity or exit code drifted.")
            except Exception as error:
                _add(issues, "COMMAND_RECORD_DRIFT", record_relative, str(error))


def _check_no_model(
    bundle: Path,
    issues: list[DependencyBundleIssue],
) -> None:
    try:
        record = _load_object(bundle / "reports/no-model-compatibility.json")
    except Exception as error:
        _add(issues, "NO_MODEL_BOUNDARY_DRIFT", "reports/no-model-compatibility.json", str(error))
        return
    for key in (
        "trust_remote_code",
        "model_opened",
        "network_contacted",
        "process_executed",
        "output_directory_created",
    ):
        if record.get(key) is not False:
            _add(
                issues,
                "NO_MODEL_BOUNDARY_DRIFT",
                "reports/no-model-compatibility.json",
                f"{key} must remain false.",
            )
    authorisations = record.get("scientific_authorisations")
    if not isinstance(authorisations, Mapping) or any(
        value is not False for value in authorisations.values()
    ):
        _add(
            issues,
            "NO_MODEL_BOUNDARY_DRIFT",
            "reports/no-model-compatibility.json",
            "No-model scientific authorisations must all remain false.",
        )


def _check_recomputed_decision(
    decision: Mapping[str, Any],
    observation: Mapping[str, Any],
    issues: list[DependencyBundleIssue],
) -> None:
    try:
        expected = build_live_dependency_preflight_record(observation)
    except Exception as error:
        _add(issues, "OBSERVATION_INVALID", "observation.json", str(error))
        return
    if expected != decision:
        _add(
            issues,
            "DECISION_RECOMPUTE",
            "decision.json",
            "Decision differs from the record recomputed from observation data.",
        )


def validate_dependency_bundle(
    bundle_root: Path,
    repository_root: Path,
    *,
    require_passed: bool = False,
) -> list[DependencyBundleIssue]:
    issues: list[DependencyBundleIssue] = []
    try:
        bundle = bundle_root.resolve(strict=True)
        repository = repository_root.resolve(strict=True)
    except OSError as error:
        return [DependencyBundleIssue("BUNDLE_PATH", str(bundle_root), str(error))]
    if not bundle.is_dir() or bundle_root.is_symlink() or _is_reparse(bundle):
        return [
            DependencyBundleIssue(
                "BUNDLE_PATH",
                str(bundle_root),
                "Bundle root must be one normal existing directory.",
            )
        ]

    _scan_payload(bundle, issues)
    _check_required_and_manifest(bundle, issues)

    loaded: dict[str, dict[str, Any]] = {}
    for relative in ("decision.json", "observation.json"):
        path = bundle / relative
        if not path.is_file():
            continue
        try:
            loaded[relative] = _load_object(path)
        except Exception as error:
            _add(issues, "JSON_INVALID", relative, str(error))
    decision = loaded.get("decision.json")
    observation = loaded.get("observation.json")

    if decision is not None:
        _check_decision_schema(decision, repository, issues)
        _check_claims(decision, issues)
        if require_passed and decision.get("status") != "Passed":
            _add(
                issues,
                "DECISION_NOT_PASSED",
                "decision.json",
                "Live acceptance requires a Passed dependency decision.",
            )
    if observation is not None and require_passed and observation.get("simulation_mode") is not False:
        _add(
            issues,
            "SIMULATION_NOT_LIVE",
            "observation.json",
            "A repository simulation cannot be accepted as live dependency evidence.",
        )

    bootstrap_data = _load_lock_and_report(
        bundle,
        "locks/requirements.phase3-bootstrap.txt",
        "reports/bootstrap-install-report.json",
        required=BOOTSTRAP_DIRECT_VERSIONS,
        bootstrap=True,
        issues=issues,
    )
    normal_data = _load_lock_and_report(
        bundle,
        "locks/requirements.phase3-assets.txt",
        "reports/normal-install-report.json",
        required=DIRECT_NORMAL_VERSIONS,
        bootstrap=False,
        issues=issues,
    )

    if observation is not None:
        _check_lock_observation_relationships(
            observation,
            bootstrap_data,
            normal_data,
            issues,
        )
        _check_sources(bundle, observation, issues)
        _check_checks_and_stages(bundle, observation, issues)
    _check_final_packages(
        bundle,
        normal_data[0] if normal_data is not None else None,
        issues,
    )
    _check_command_index(bundle, issues)
    _check_no_model(bundle, issues)
    if decision is not None and observation is not None:
        _check_recomputed_decision(decision, observation, issues)

    return sorted(
        issues,
        key=lambda issue: (issue.path.casefold(), issue.code, issue.message),
    )


def _write_report(path: Path, issues: Sequence[DependencyBundleIssue]) -> None:
    lines = ["# Workbook 05 dependency-preflight artifact validation", ""]
    if not issues:
        lines.append(
            "Validation passed: manifest, source, lock, package, command, stage, "
            "decision, and non-claim relationships are valid."
        )
    else:
        lines.append(f"Validation failed with {len(issues)} issue(s).")
        lines.append("")
        lines.extend(
            f"- `{issue.code}` `{issue.path}` — {issue.message}"
            for issue in issues
        )
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")


def main(argv: Iterable[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle-root", type=Path, required=True)
    parser.add_argument("--repository-root", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--require-passed", action="store_true")
    arguments = parser.parse_args(list(argv) if argv is not None else None)

    issues = validate_dependency_bundle(
        arguments.bundle_root,
        arguments.repository_root,
        require_passed=arguments.require_passed,
    )
    _write_report(arguments.report, issues)
    print(arguments.report.read_text(encoding="utf-8"), end="")
    return 0 if not issues else 1


if __name__ == "__main__":
    raise SystemExit(main())
