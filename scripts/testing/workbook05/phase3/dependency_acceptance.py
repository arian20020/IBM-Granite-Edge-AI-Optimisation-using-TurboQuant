"""Bind one independently accepted dependency preflight to live C1.

This module is intentionally narrow. It does not download a model and it does
not run conversion. Its only job is to prove that the exact accepted dependency
workspace still exists, still contains the accepted evidence, and still exposes
the executables that the later live C1 process is allowed to use.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import stat
import sys
from dataclasses import dataclass
from pathlib import Path, PureWindowsPath
from typing import Any, Mapping, Sequence

from scripts.testing.workbook05.phase3.hashing import sha256_file


# ---------------------------------------------------------------------------
# Immutable identities accepted after workflow run 32211117536 completed.
# ---------------------------------------------------------------------------

ACCEPTED_DEPENDENCY_RUN_ID = "32211117536"
ACCEPTED_DEPENDENCY_RUN_ATTEMPT = 1
ACCEPTED_DEPENDENCY_ARTIFACT_ID = "9350956534"
ACCEPTED_DEPENDENCY_ARTIFACT_NAME = (
    "workbook-05-phase3-dependency-preflight-32211117536-1"
)
ACCEPTED_DEPENDENCY_ARTIFACT_SHA256 = (
    "b68a4f8af8c57a9f5d71347d2485855796f4d0dce0291c50b596512c309c0c21"
)
ACCEPTED_DEPENDENCY_DECISION_SHA256 = (
    "429b90548ce2b4c8463941c5b2983c8ff0cf6cc3ea3865375c8cae78d7193b49"
)
ACCEPTED_DEPENDENCY_WORKSPACE = (
    r"C:\w5c\dependency-preflight-32211117536-1"
)
ACCEPTED_DEPENDENCY_EVIDENCE = ACCEPTED_DEPENDENCY_WORKSPACE + r"\evidence"
ACCEPTED_PROJECT_HEAD = "c417efd936a7fa2e871b689065b2f3b88636c1a1"
ACCEPTED_OPTIMUM_COMMIT = "982e495540364f95da1e4b6f62d2d4e5907d08fd"
ACCEPTED_OPTIMUM_INTEL_COMMIT = "a3b6012a4c02f4147260da4d4601bb6a3c0d2bb0"

_ACCEPTANCE_RELATIVE_PATH = Path(
    "experiments/granite_turboquant_intel/manifests/campaigns/"
    "GTQ-WB05-MF-v1/phase3/accepted-dependency-preflight.json"
)
_SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
_CLAIM_KEYS = (
    "model_download_authorised",
    "granite_model_test_authorised",
    "activation_claim_authorised",
    "packed_storage_claim_authorised",
    "performance_claim_authorised",
    "quality_claim_authorised",
)


@dataclass(frozen=True, slots=True)
class AcceptanceIssue:
    """One deterministic problem in the committed acceptance record."""

    code: str
    path: str
    message: str


def _issue(
    issues: list[AcceptanceIssue],
    code: str,
    path: str,
    message: str,
) -> None:
    """Append one reviewer-readable issue without raising early."""

    issues.append(AcceptanceIssue(code=code, path=path, message=message))


def _expected_record_values() -> dict[str, object]:
    """Return scalar values that may never drift without a new acceptance."""

    return {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "record_type": "dependency-preflight-acceptance",
        "route_id": "route-a-merged-openvino",
        "status": "Accepted",
        "project_repository_head": ACCEPTED_PROJECT_HEAD,
        "workflow_run_id": ACCEPTED_DEPENDENCY_RUN_ID,
        "run_attempt": ACCEPTED_DEPENDENCY_RUN_ATTEMPT,
        "artifact_id": ACCEPTED_DEPENDENCY_ARTIFACT_ID,
        "artifact_name": ACCEPTED_DEPENDENCY_ARTIFACT_NAME,
        "github_artifact_sha256": ACCEPTED_DEPENDENCY_ARTIFACT_SHA256,
        "independent_artifact_sha256": ACCEPTED_DEPENDENCY_ARTIFACT_SHA256,
        "decision_path": "decision.json",
        "decision_sha256": ACCEPTED_DEPENDENCY_DECISION_SHA256,
        "workspace_root": ACCEPTED_DEPENDENCY_WORKSPACE,
        "evidence_root": ACCEPTED_DEPENDENCY_EVIDENCE,
        "optimum_commit": ACCEPTED_OPTIMUM_COMMIT,
        "optimum_intel_commit": ACCEPTED_OPTIMUM_INTEL_COMMIT,
        "accepted_by": "arian20020",
        "owner_acceptance": True,
    }


def validate_dependency_acceptance_record(
    payload: Mapping[str, Any],
) -> list[AcceptanceIssue]:
    """Validate the committed record as one closed exact acceptance.

    The workflow may accept only this run, attempt, artifact, decision, and
    retained workspace. A syntactically valid but different SHA-256 is not a
    substitute for the independently reviewed value.
    """

    issues: list[AcceptanceIssue] = []
    expected = _expected_record_values()
    allowed_keys = set(expected) | {"accepted_at_utc", *_CLAIM_KEYS}

    # Reject both missing fields and unreviewed extension fields.
    for key in sorted(allowed_keys - set(payload)):
        _issue(
            issues,
            "REQUIRED_FIELD_MISSING",
            f"$.{key}",
            "The accepted dependency record is missing a required field.",
        )
    for key in sorted(set(payload) - allowed_keys):
        _issue(
            issues,
            "UNEXPECTED_FIELD",
            f"$.{key}",
            "The accepted dependency record contains an unreviewed field.",
        )

    # Compare every immutable scalar to the independently accepted identity.
    for key, expected_value in expected.items():
        actual = payload.get(key)
        if actual == expected_value:
            continue
        code = "FIELD_MISMATCH"
        if key == "decision_sha256":
            code = "DECISION_SHA256_MISMATCH"
        elif key in {
            "github_artifact_sha256",
            "independent_artifact_sha256",
        }:
            code = "ARTIFACT_SHA256_MISMATCH"
        elif key in {
            "workflow_run_id",
            "run_attempt",
            "workspace_root",
            "evidence_root",
        }:
            code = "WORKSPACE_IDENTITY_MISMATCH"
        _issue(
            issues,
            code,
            f"$.{key}",
            f"Expected {expected_value!r}, found {actual!r}.",
        )

    # The two artifact digests must also agree with each other.
    github_digest = payload.get("github_artifact_sha256")
    independent_digest = payload.get("independent_artifact_sha256")
    if github_digest != independent_digest:
        _issue(
            issues,
            "ARTIFACT_SHA256_MISMATCH",
            "$.independent_artifact_sha256",
            "GitHub and independently calculated artifact digests differ.",
        )

    # Preserve an explicit UTC acceptance timestamp without accepting free text.
    accepted_at = payload.get("accepted_at_utc")
    if (
        not isinstance(accepted_at, str)
        or not re.fullmatch(
            r"[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z",
            accepted_at,
        )
    ):
        _issue(
            issues,
            "ACCEPTANCE_TIMESTAMP_INVALID",
            "$.accepted_at_utc",
            "The acceptance timestamp must be an RFC 3339 whole-second UTC value.",
        )

    # Dependency acceptance authorises only the following C1 binding check.
    # It does not itself authorise model or scientific claims.
    for key in _CLAIM_KEYS:
        if payload.get(key) is not False:
            _issue(
                issues,
                "SCIENTIFIC_AUTHORITY_DRIFT",
                f"$.{key}",
                "Dependency acceptance must keep every later authority false.",
            )

    return sorted(issues, key=lambda item: (item.code, item.path, item.message))


def _load_json_object(path: Path, label: str) -> dict[str, Any]:
    """Read one strict JSON object without executing retained content."""

    try:
        raw = path.read_bytes()
        text = raw.decode("utf-8")
        if text.startswith("\ufeff"):
            raise ValueError(f"{label} must use BOM-free UTF-8.")
        value = json.loads(text)
    except (OSError, UnicodeError, json.JSONDecodeError, ValueError) as error:
        raise ValueError(
            f"{label} is not valid strict UTF-8 JSON: {path}: {error}"
        ) from error
    if not isinstance(value, dict):
        raise ValueError(f"{label} must contain one JSON object: {path}")
    return value


def _is_link_or_reparse(path: Path) -> bool:
    """Return whether a path is a symbolic link, junction, or reparse point."""

    if path.is_symlink():
        return True
    try:
        attributes = getattr(path.lstat(), "st_file_attributes", 0)
    except OSError as error:
        raise ValueError(
            f"Unable to inspect retained path safely: {path}: {error}"
        ) from error
    flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)
    return bool(attributes & flag)


def _assert_normal_directory(path: Path, label: str) -> Path:
    """Require one existing normal directory and reject redirection points."""

    if not path.exists() or not path.is_dir():
        raise ValueError(f"{label} does not exist as a directory: {path}")
    if _is_link_or_reparse(path):
        raise ValueError(f"{label} must not be a link or reparse point: {path}")
    return path.resolve(strict=True)


def _assert_regular_file(path: Path, label: str) -> Path:
    """Require one regular, non-linked file."""

    if not path.exists() or not path.is_file():
        raise ValueError(f"{label} does not exist as a regular file: {path}")
    if _is_link_or_reparse(path):
        raise ValueError(f"{label} must not be a link or reparse point: {path}")
    return path.resolve(strict=True)


def _windows_same_path(left: str, right: str) -> bool:
    """Compare Windows path components case-insensitively."""

    left_path = PureWindowsPath(left)
    right_path = PureWindowsPath(right)
    return tuple(part.casefold() for part in left_path.parts) == tuple(
        part.casefold() for part in right_path.parts
    )


def _assert_inside_workspace(path: Path, workspace: Path, label: str) -> Path:
    """Require one resolved path to remain strictly beneath the workspace."""

    resolved = path.resolve(strict=True)
    try:
        relative = resolved.relative_to(workspace)
    except ValueError as error:
        raise ValueError(f"{label} escapes the accepted workspace: {resolved}") from error
    if not relative.parts:
        raise ValueError(f"{label} must be a child of the accepted workspace.")
    return resolved


def _verify_decision_and_observation(
    evidence: Path,
    record: Mapping[str, Any],
) -> tuple[dict[str, Any], dict[str, Any]]:
    """Rehash the accepted decision and verify its live observation binding."""

    decision_path = _assert_regular_file(
        evidence / str(record["decision_path"]),
        "Accepted dependency decision",
    )
    actual_decision_sha256 = sha256_file(decision_path)
    if actual_decision_sha256 != record["decision_sha256"]:
        raise ValueError(
            "Accepted dependency decision SHA-256 mismatch. "
            f"Expected {record['decision_sha256']}, found {actual_decision_sha256}."
        )

    decision = _load_json_object(decision_path, "Accepted dependency decision")
    if decision.get("status") != "Passed":
        raise ValueError("Accepted dependency decision no longer has status Passed.")
    for key in _CLAIM_KEYS:
        if decision.get(key) is not False:
            raise ValueError(
                f"Accepted dependency decision unexpectedly authorises {key}."
            )

    observation_path = _assert_regular_file(
        evidence / "observation.json",
        "Accepted dependency observation",
    )
    observation = _load_json_object(
        observation_path,
        "Accepted dependency observation",
    )
    if observation.get("simulation_mode") is not None:
        raise ValueError("The accepted dependency observation must be a live record.")
    if not _windows_same_path(
        str(observation.get("workspace_root", "")),
        str(record["workspace_root"]),
    ):
        raise ValueError(
            "Accepted dependency observation workspace differs from the committed acceptance."
        )
    return decision, observation


def verify_retained_dependency_acceptance(
    repository_root: Path,
    supplied_decision_sha256: str,
) -> dict[str, Any]:
    """Revalidate the exact retained dependency workspace before model access."""

    # Load the repository-controlled acceptance before touching the retained
    # workspace. A different supplied digest fails before any model operation.
    acceptance_path = repository_root / _ACCEPTANCE_RELATIVE_PATH
    record = _load_json_object(
        acceptance_path,
        "Committed dependency acceptance",
    )
    issues = validate_dependency_acceptance_record(record)
    if issues:
        details = "; ".join(
            f"{issue.code} {issue.path}: {issue.message}" for issue in issues
        )
        raise ValueError(f"Committed dependency acceptance is invalid: {details}")
    if supplied_decision_sha256 != ACCEPTED_DEPENDENCY_DECISION_SHA256:
        raise ValueError(
            "The supplied dependency decision SHA-256 does not match the accepted decision."
        )

    # The live policy requires the exact C:\w5c workspace, not a copied or
    # operator-selected substitute. Each parent is inspected without following
    # a link or junction.
    workspace = _assert_normal_directory(
        Path(ACCEPTED_DEPENDENCY_WORKSPACE),
        "Accepted dependency workspace",
    )
    evidence = _assert_normal_directory(
        workspace / "evidence",
        "Accepted dependency evidence root",
    )
    if not _windows_same_path(str(workspace), str(record["workspace_root"])):
        raise ValueError("Resolved dependency workspace differs from the accepted path.")
    if not _windows_same_path(str(evidence), str(record["evidence_root"])):
        raise ValueError("Resolved dependency evidence root differs from the accepted path.")

    # Import the repository-only validator only when this verifier is executed.
    # Live model download imports the acceptance constants from this module in
    # the accepted conversion environment, which intentionally lacks jsonschema.
    try:
        from scripts.testing.workbook05.phase3.dependency_bundle_validation import (
            validate_dependency_bundle,
        )
    except ImportError as error:
        raise ValueError(
            "Retained dependency validation requires the repository validator "
            "environment with jsonschema installed."
        ) from error

    # Treat the retained Lenovo bundle as untrusted data again. This catches a
    # changed manifest, package record, source identity, command log, or claim.
    bundle_issues = validate_dependency_bundle(evidence, repository_root)
    if bundle_issues:
        details = "; ".join(
            f"{issue.code} {issue.path}: {issue.message}"
            for issue in bundle_issues
        )
        raise ValueError(f"Retained dependency bundle is no longer valid: {details}")

    decision, observation = _verify_decision_and_observation(evidence, record)

    # Rehash the exact Python executable retained by the live observation.
    python_text = observation.get("python_executable_path")
    python_digest = observation.get("python_executable_sha256")
    if not isinstance(python_text, str) or not python_text:
        raise ValueError("Accepted dependency observation has no Python executable path.")
    if not isinstance(python_digest, str) or not _SHA256_PATTERN.fullmatch(
        python_digest
    ):
        raise ValueError("Accepted dependency observation has no valid Python digest.")
    python_path = _assert_regular_file(Path(python_text), "Accepted dependency Python")
    _assert_inside_workspace(python_path, workspace, "Accepted dependency Python")
    actual_python_digest = sha256_file(python_path)
    if actual_python_digest != python_digest:
        raise ValueError(
            "Accepted dependency Python SHA-256 mismatch. "
            f"Expected {python_digest}, found {actual_python_digest}."
        )

    # Optimum CLI must come from the same accepted final environment. Its hash is
    # measured now and carried into the later conversion record.
    optimum_cli = _assert_regular_file(
        python_path.parent / "optimum-cli.exe",
        "Accepted Optimum CLI",
    )
    _assert_inside_workspace(optimum_cli, workspace, "Accepted Optimum CLI")
    optimum_cli_sha256 = sha256_file(optimum_cli)

    return {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "record_type": "dependency-acceptance-proof",
        "route_id": "route-a-merged-openvino",
        "status": "Passed",
        "workflow_run_id": ACCEPTED_DEPENDENCY_RUN_ID,
        "run_attempt": ACCEPTED_DEPENDENCY_RUN_ATTEMPT,
        "artifact_id": ACCEPTED_DEPENDENCY_ARTIFACT_ID,
        "artifact_sha256": ACCEPTED_DEPENDENCY_ARTIFACT_SHA256,
        "decision_sha256": ACCEPTED_DEPENDENCY_DECISION_SHA256,
        "workspace_root": str(workspace),
        "evidence_root": str(evidence),
        "python_executable_path": str(python_path),
        "python_executable_sha256": actual_python_digest,
        "optimum_cli_path": str(optimum_cli),
        "optimum_cli_sha256": optimum_cli_sha256,
        "optimum_commit": ACCEPTED_OPTIMUM_COMMIT,
        "optimum_intel_commit": ACCEPTED_OPTIMUM_INTEL_COMMIT,
        "decision_status": decision["status"],
        **{key: False for key in _CLAIM_KEYS},
    }


def _write_atomic_json(path: Path, value: Mapping[str, Any]) -> None:
    """Publish one create-once JSON record through a sibling temporary file."""

    if path.exists():
        raise ValueError(f"Refusing to overwrite dependency proof: {path}")
    temporary = path.with_name(path.name + ".tmp")
    if temporary.exists():
        raise ValueError(f"Temporary dependency proof already exists: {temporary}")
    payload = (json.dumps(value, indent=2, ensure_ascii=False) + "\n").encode(
        "utf-8"
    )
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


def _parse_args(argv: Sequence[str] | None) -> argparse.Namespace:
    """Parse the narrow live verifier command line."""

    parser = argparse.ArgumentParser(
        description="Revalidate the accepted Workbook 05 dependency workspace."
    )
    parser.add_argument("--repository-root", type=Path, required=True)
    parser.add_argument("--accepted-decision-sha256", required=True)
    parser.add_argument("--output", type=Path, required=True)
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    """CLI entry point used before any live C1 model operation."""

    args = _parse_args(argv)
    try:
        repository_root = _assert_normal_directory(
            args.repository_root,
            "Repository root",
        )
        output_parent = _assert_normal_directory(
            args.output.parent,
            "Dependency-proof output parent",
        )
        proof = verify_retained_dependency_acceptance(
            repository_root,
            args.accepted_decision_sha256,
        )
        _write_atomic_json(output_parent / args.output.name, proof)
    except (OSError, ValueError) as error:
        print(f"Error: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
