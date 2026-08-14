"""Validate a C1 dependency-preflight artifact strictly as untrusted data."""

from __future__ import annotations

import argparse
import json
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable, Mapping

from jsonschema import Draft202012Validator, FormatChecker

from scripts.testing.workbook05.hash_manifest import verify_hash_manifest
from scripts.testing.workbook05.phase3.conversion import (
    OPTIMUM_COMMIT,
    OPTIMUM_INTEL_COMMIT,
)
from scripts.testing.workbook05.phase3.dependency_lock import (
    build_dependency_preflight_record,
    parse_hash_locked_requirements,
    parse_normal_install_report,
)


REQUIRED_PATHS: tuple[str, ...] = (
    "decision.json",
    "observation.json",
    "locks/requirements.phase3-assets.txt",
    "reports/normal-install-report.json",
    "reports/vcs-packages.json",
    "sources/optimum.json",
    "sources/optimum-intel.json",
    "checks.json",
    "summary.md",
    "stage-order.json",
    "manifest.sha256",
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
SECRET_PATTERNS = (
    "ghp_",
    "gho_",
    "ghs_",
    "github_pat_",
    "authorization: bearer ",
    "hf_token=",
    "hugging_face_hub_token=",
    "--token ",
)
CLAIM_KEYS = (
    "model_download_authorised",
    "granite_model_test_authorised",
    "activation_claim_authorised",
    "packed_storage_claim_authorised",
    "performance_claim_authorised",
    "quality_claim_authorised",
)
EXPECTED_SOURCES = {
    "optimum-intel": {
        "repository": "huggingface/optimum-intel",
        "origin": "https://github.com/huggingface/optimum-intel.git",
        "commit": OPTIMUM_INTEL_COMMIT,
    },
    "optimum": {
        "repository": "huggingface/optimum",
        "origin": "https://github.com/huggingface/optimum.git",
        "commit": OPTIMUM_COMMIT,
    },
}
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
        raise ValueError(f"Expected a JSON object: {path}")
    return value


def _scan_text(bundle: Path, issues: list[DependencyBundleIssue]) -> None:
    for candidate in sorted(
        bundle.rglob("*"),
        key=lambda value: value.relative_to(bundle).as_posix().casefold(),
    ):
        relative = candidate.relative_to(bundle).as_posix()
        if candidate.is_symlink():
            _add(
                issues,
                "LINKED_PAYLOAD",
                relative,
                "Dependency evidence must not contain a symbolic link.",
            )
            continue
        if candidate.is_dir():
            continue
        if not candidate.is_file():
            _add(
                issues,
                "NONREGULAR_PAYLOAD",
                relative,
                "Dependency evidence must contain regular files only.",
            )
            continue
        if candidate.suffix.casefold() in FORBIDDEN_SUFFIXES:
            _add(
                issues,
                "FORBIDDEN_PAYLOAD",
                relative,
                "Wheels, executables, libraries, archives, models, and IR are forbidden.",
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
        / "experiments"
        / "granite_turboquant_intel"
        / "schemas"
        / "workbook05"
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
        path = "$." + "/".join(str(part) for part in error.absolute_path)
        _add(issues, "SCHEMA_INVALID", "decision.json", f"{path}: {error.message}")


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
                f"{key} must remain false at the dependency-preflight boundary.",
            )


def _check_lock_and_observation(
    bundle: Path,
    decision: Mapping[str, Any],
    observation: Mapping[str, Any],
    issues: list[DependencyBundleIssue],
) -> None:
    lock_path = bundle / "locks" / "requirements.phase3-assets.txt"
    report_path = bundle / "reports" / "normal-install-report.json"
    if not lock_path.is_file() or not report_path.is_file():
        return

    try:
        lock_text = lock_path.read_text(encoding="utf-8-sig")
        lock = parse_hash_locked_requirements(lock_text)
        report = _load_object(report_path)
        parse_normal_install_report(report, lock)
    except (OSError, UnicodeError, json.JSONDecodeError, ValueError) as error:
        _add(issues, "LOCK_CONTENT", "locks/requirements.phase3-assets.txt", str(error))
        return

    import hashlib

    actual_lock_sha = hashlib.sha256(lock_text.encode("utf-8")).hexdigest()
    for source_name, value in (
        ("observation", observation.get("lock_sha256")),
        ("decision", decision.get("lock_sha256")),
    ):
        if value != actual_lock_sha:
            _add(
                issues,
                "LOCK_IDENTITY",
                "locks/requirements.phase3-assets.txt",
                f"The {source_name} lock SHA-256 does not match the complete lock.",
            )
    if observation.get("lock_text") != lock_text:
        _add(
            issues,
            "LOCK_IDENTITY",
            "observation.json",
            "The observation lock_text differs from the retained lock file.",
        )


def _check_sources(
    bundle: Path,
    observation: Mapping[str, Any],
    issues: list[DependencyBundleIssue],
) -> None:
    observed_rows = observation.get("source_trees")
    if not isinstance(observed_rows, list):
        _add(issues, "VCS_IDENTITY", "observation.json", "source_trees is missing.")
        return
    observation_by_name = {
        str(row.get("name")): row
        for row in observed_rows
        if isinstance(row, Mapping)
    }

    for name, expected in EXPECTED_SOURCES.items():
        path = bundle / "sources" / f"{name}.json"
        if not path.is_file():
            continue
        try:
            source = _load_object(path)
        except (OSError, UnicodeError, json.JSONDecodeError, ValueError) as error:
            _add(issues, "VCS_IDENTITY", path.relative_to(bundle).as_posix(), str(error))
            continue
        for key, expected_value in expected.items():
            if source.get(key) != expected_value:
                _add(
                    issues,
                    "VCS_IDENTITY",
                    path.relative_to(bundle).as_posix(),
                    f"{key} does not match the reviewed source identity.",
                )
        if source.get("clean") is not True or not _SHA256.fullmatch(
            str(source.get("aggregate_sha256", ""))
        ):
            _add(
                issues,
                "VCS_IDENTITY",
                path.relative_to(bundle).as_posix(),
                "The source must be clean and have a lowercase aggregate SHA-256.",
            )
        observed = observation_by_name.get(name)
        if not isinstance(observed, Mapping) or any(
            observed.get(key) != source.get(key)
            for key in (
                "repository",
                "origin",
                "commit",
                "clean",
                "aggregate_sha256",
            )
        ):
            _add(
                issues,
                "VCS_IDENTITY",
                "observation.json",
                f"The observation differs from retained source evidence for {name}.",
            )


def _check_recomputed_record(
    decision: Mapping[str, Any],
    observation: Mapping[str, Any],
    issues: list[DependencyBundleIssue],
) -> None:
    try:
        expected = build_dependency_preflight_record(observation)
    except Exception as error:
        _add(issues, "OBSERVATION_INVALID", "observation.json", str(error))
        return

    if decision.get("status") == "Blocked":
        # Offline fixture evidence may differ only by the deliberate blocked
        # status and explanatory reasons. Every identity and non-claim must match.
        ignored = {"status", "reasons"}
        for key in sorted(set(expected) | set(decision)):
            if key in ignored:
                continue
            if expected.get(key) != decision.get(key):
                _add(
                    issues,
                    "DECISION_RECOMPUTE",
                    "decision.json",
                    f"Decision field {key} differs from recomputed observation evidence.",
                )
    elif expected != decision:
        _add(
            issues,
            "DECISION_RECOMPUTE",
            "decision.json",
            "The passed decision differs from the record recomputed from observation data.",
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
    if not bundle.is_dir() or bundle_root.is_symlink():
        return [
            DependencyBundleIssue(
                "BUNDLE_PATH",
                str(bundle_root),
                "Bundle root must be one normal existing directory.",
            )
        ]

    _scan_text(bundle, issues)
    _check_required_and_manifest(bundle, issues)

    decision: dict[str, Any] | None = None
    observation: dict[str, Any] | None = None
    for relative, target in (
        ("decision.json", "decision"),
        ("observation.json", "observation"),
    ):
        path = bundle / relative
        if not path.is_file():
            continue
        try:
            loaded = _load_object(path)
        except (OSError, UnicodeError, json.JSONDecodeError, ValueError) as error:
            _add(issues, "JSON_INVALID", relative, str(error))
            continue
        if target == "decision":
            decision = loaded
        else:
            observation = loaded

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

    if decision is not None and observation is not None:
        _check_lock_and_observation(bundle, decision, observation, issues)
        _check_sources(bundle, observation, issues)
        _check_recomputed_record(decision, observation, issues)

    return sorted(
        issues,
        key=lambda issue: (issue.path.casefold(), issue.code, issue.message),
    )


def _write_report(path: Path, issues: list[DependencyBundleIssue]) -> None:
    lines = ["# Workbook 05 dependency-preflight artifact validation", ""]
    if not issues:
        lines.append(
            "Validation passed: text payloads, hashes, lock identity, VCS identity, "
            "decision reconstruction, and C1 non-claims are valid."
        )
    else:
        lines.append(f"Validation failed with {len(issues)} issue(s).")
        lines.append("")
        for issue in issues:
            lines.append(f"- `{issue.code}` `{issue.path}` — {issue.message}")
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
