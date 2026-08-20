"""Validate the extra trust relationships present only in a live Workbook 05 C1 bundle.

The generic :mod:`asset_bundle_validation` module deliberately supports the deterministic,
model-free fixture used on pull requests.  A live C1 artifact contains additional records
that bind the accepted dependency preflight, the immutable IBM Granite revision, and the
approved stage order.  This module layers those live-only checks on top of the generic
validator without executing or importing anything from the artifact itself.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any, Iterable, Mapping

from scripts.testing.workbook05.phase3.asset_bundle_validation import (
    BundleIssue,
    validate_asset_bundle,
)
from scripts.testing.workbook05.phase3.dependency_acceptance import (
    ACCEPTED_DEPENDENCY_ARTIFACT_ID,
    ACCEPTED_DEPENDENCY_ARTIFACT_SHA256,
    ACCEPTED_DEPENDENCY_RUN_ATTEMPT,
    ACCEPTED_DEPENDENCY_RUN_ID,
    ACCEPTED_DEPENDENCY_WORKSPACE,
    ACCEPTED_OPTIMUM_COMMIT,
    ACCEPTED_OPTIMUM_INTEL_COMMIT,
)


# These records are produced only by the explicitly authorised live C1 route.
LIVE_REQUIRED_PATHS: tuple[str, ...] = (
    "dependency-acceptance-proof.json",
    "dependency/decision.json",
    "resolved-model.json",
    "stage-order.json",
)

# The stage trace is an auditable control, not merely descriptive logging.
APPROVED_STAGE_ORDER: tuple[str, ...] = (
    "prerequisite-verification",
    "path-root-verification",
    "disk-preflight",
    "immutable-revision-resolution",
    "source-snapshot-download",
    "source-file-hash-inventory",
    "conversion-new-output-directory",
    "converted-file-hash-inventory",
    "schema-validation",
    "manifest-generation",
)

# C1 must never be interpreted as authority for model execution or scientific claims.
SCIENTIFIC_CLAIM_KEYS: frozenset[str] = frozenset(
    {
        "model_download_authorised",
        "granite_model_test_authorised",
        "model_execution_authorised",
        "activation_claim_authorised",
        "codec_activation_claim_authorised",
        "packed_storage_claim_authorised",
        "performance_claim_authorised",
        "quality_claim_authorised",
    }
)


def _add(
    issues: list[BundleIssue],
    code: str,
    path: str,
    message: str,
) -> None:
    """Append one stable, reviewer-readable live-bundle problem."""

    issues.append(BundleIssue(code=code, path=path, message=message))


def _load_object(
    bundle: Path,
    relative: str,
    issues: list[BundleIssue],
) -> dict[str, Any] | None:
    """Read one required JSON object without trusting its shape or encoding."""

    path = bundle / relative
    if not path.is_file() or path.is_symlink():
        return None
    try:
        value = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        _add(issues, "JSON_INVALID", relative, str(error))
        return None
    if not isinstance(value, dict):
        _add(issues, "JSON_INVALID", relative, "Expected a JSON object.")
        return None
    return value


def _sha256(path: Path) -> str:
    """Return the lowercase SHA-256 of the exact retained bytes."""

    return hashlib.sha256(path.read_bytes()).hexdigest()


def _walk(value: object, path: str = "$") -> Iterable[tuple[str, str | None, object]]:
    """Yield nested values in deterministic order for non-claim enforcement."""

    if isinstance(value, Mapping):
        for key in sorted(value, key=str):
            child = value[key]
            child_path = f"{path}.{key}"
            yield child_path, str(key), child
            yield from _walk(child, child_path)
    elif isinstance(value, list):
        for index, child in enumerate(value):
            child_path = f"{path}[{index}]"
            yield child_path, None, child
            yield from _walk(child, child_path)


def _check_non_claims(
    relative: str,
    payload: Mapping[str, Any],
    issues: list[BundleIssue],
) -> None:
    """Keep every live-only scientific-authority field explicitly false."""

    for json_path, key, value in _walk(payload):
        if key in SCIENTIFIC_CLAIM_KEYS and value is not False:
            _add(
                issues,
                "SCIENTIFIC_CLAIM",
                relative,
                f"{json_path} must remain false at the C1 boundary.",
            )


def _check_dependency_binding(
    bundle: Path,
    proof: Mapping[str, Any],
    decision: Mapping[str, Any],
    conversion: Mapping[str, Any] | None,
    issues: list[BundleIssue],
) -> None:
    """Bind the copied decision bytes to the accepted proof and conversion record."""

    decision_path = bundle / "dependency" / "decision.json"
    try:
        observed_digest = _sha256(decision_path)
    except OSError as error:
        _add(issues, "DEPENDENCY_DECISION_MISMATCH", "dependency/decision.json", str(error))
        return

    expected_proof_values: dict[str, object] = {
        "workflow_run_id": ACCEPTED_DEPENDENCY_RUN_ID,
        "run_attempt": ACCEPTED_DEPENDENCY_RUN_ATTEMPT,
        "artifact_id": ACCEPTED_DEPENDENCY_ARTIFACT_ID,
        "artifact_sha256": ACCEPTED_DEPENDENCY_ARTIFACT_SHA256,
        "workspace_root": ACCEPTED_DEPENDENCY_WORKSPACE,
        "optimum_commit": ACCEPTED_OPTIMUM_COMMIT,
        "optimum_intel_commit": ACCEPTED_OPTIMUM_INTEL_COMMIT,
        "decision_status": "Passed",
    }
    drifted = [
        key
        for key, expected in expected_proof_values.items()
        if proof.get(key) != expected
    ]
    if proof.get("decision_sha256") != observed_digest:
        drifted.append("decision_sha256")
    if decision.get("status") != "Passed":
        drifted.append("dependency.status")

    dependency_reference = (
        conversion.get("dependency_preflight")
        if isinstance(conversion, Mapping)
        else None
    )
    if not isinstance(dependency_reference, Mapping):
        drifted.append("conversion-record.dependency_preflight")
    else:
        if dependency_reference.get("status") != "Passed":
            drifted.append("conversion-record.dependency_preflight.status")
        if dependency_reference.get("record_path") != "dependency/decision.json":
            drifted.append("conversion-record.dependency_preflight.record_path")
        if dependency_reference.get("record_sha256") != observed_digest:
            drifted.append("conversion-record.dependency_preflight.record_sha256")

    if drifted:
        _add(
            issues,
            "DEPENDENCY_DECISION_MISMATCH",
            "dependency-acceptance-proof.json",
            "The live C1 dependency binding drifted at: " + ", ".join(sorted(set(drifted))),
        )


def _check_resolved_model_binding(
    resolved: Mapping[str, Any],
    asset: Mapping[str, Any] | None,
    issues: list[BundleIssue],
) -> None:
    """Prove that the standalone resolution record describes the locked asset exactly."""

    if not isinstance(asset, Mapping):
        _add(
            issues,
            "RESOLVED_MODEL_RELATIONSHIP",
            "resolved-model.json",
            "asset-lock.json is unavailable for relationship validation.",
        )
        return

    source = asset.get("source")
    if not isinstance(source, Mapping):
        _add(
            issues,
            "RESOLVED_MODEL_RELATIONSHIP",
            "resolved-model.json",
            "asset-lock.json does not contain a source identity object.",
        )
        return

    expected: dict[str, object] = {
        "repository": source.get("repository"),
        "requested_revision": source.get("requested_revision"),
        "resolved_revision": source.get("resolved_revision"),
        "license": source.get("license"),
        "source_directory": asset.get("source_directory"),
        "declared_model_metadata": asset.get("declared_model_metadata"),
        "model_files": asset.get("source_files"),
        "tokenizer_files": asset.get("tokenizer_files"),
        "aggregate_model_sha256": asset.get("aggregate_model_sha256"),
        "aggregate_tokenizer_sha256": asset.get("aggregate_tokenizer_sha256"),
    }
    drifted = [key for key, value in expected.items() if resolved.get(key) != value]
    if drifted:
        _add(
            issues,
            "RESOLVED_MODEL_RELATIONSHIP",
            "resolved-model.json",
            "The resolved-model record does not match asset-lock.json at: "
            + ", ".join(sorted(drifted)),
        )


def _check_stage_order(
    stage_order: Mapping[str, Any],
    issues: list[BundleIssue],
) -> None:
    """Require the complete reviewed C1 stage sequence with a Passed outcome."""

    observed = stage_order.get("stages")
    if observed != list(APPROVED_STAGE_ORDER) or stage_order.get("status") != "Passed":
        _add(
            issues,
            "STAGE_ORDER_RELATIONSHIP",
            "stage-order.json",
            "Expected the exact approved live C1 stage order and status Passed.",
        )


def validate_live_asset_bundle(
    bundle_root: Path,
    repository_root: Path,
) -> list[BundleIssue]:
    """Return generic and live-only issues for one text-only C1 artifact."""

    issues = list(validate_asset_bundle(bundle_root, repository_root))
    try:
        bundle = bundle_root.resolve(strict=True)
    except OSError:
        return issues
    if not bundle.is_dir() or bundle_root.is_symlink():
        return issues

    for relative in LIVE_REQUIRED_PATHS:
        candidate = bundle / relative
        if not candidate.is_file() or candidate.is_symlink():
            _add(
                issues,
                "REQUIRED_PATH_MISSING",
                relative,
                "The live C1 artifact is missing a required binding or stage record.",
            )

    proof = _load_object(bundle, "dependency-acceptance-proof.json", issues)
    decision = _load_object(bundle, "dependency/decision.json", issues)
    resolved = _load_object(bundle, "resolved-model.json", issues)
    stage_order = _load_object(bundle, "stage-order.json", issues)
    asset = _load_object(bundle, "asset-lock.json", issues)
    conversion = _load_object(bundle, "conversion-record.json", issues)

    for relative, payload in (
        ("dependency-acceptance-proof.json", proof),
        ("dependency/decision.json", decision),
        ("resolved-model.json", resolved),
        ("stage-order.json", stage_order),
    ):
        if payload is not None:
            _check_non_claims(relative, payload, issues)

    if proof is not None and decision is not None:
        _check_dependency_binding(bundle, proof, decision, conversion, issues)
    if resolved is not None:
        _check_resolved_model_binding(resolved, asset, issues)
    if stage_order is not None:
        _check_stage_order(stage_order, issues)

    return sorted(
        issues,
        key=lambda issue: (issue.path.casefold(), issue.code, issue.message),
    )


def _write_report(path: Path, issues: list[BundleIssue]) -> None:
    """Write one deterministic Markdown report suitable for the job summary."""

    lines = ["# Workbook 05 live C1 asset-bundle validation", ""]
    if not issues:
        lines.append(
            "Validation passed: the generic text-only boundary and every live C1 "
            "dependency, model-identity, stage-order, and non-claim relationship are valid."
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
    """Validate one downloaded artifact without trusting its contents."""

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle-root", type=Path, required=True)
    parser.add_argument("--repository-root", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    arguments = parser.parse_args(list(argv) if argv is not None else None)

    issues = validate_live_asset_bundle(arguments.bundle_root, arguments.repository_root)
    _write_report(arguments.report, issues)
    print(arguments.report.read_text(encoding="utf-8"), end="")
    return 0 if not issues else 1


if __name__ == "__main__":
    raise SystemExit(main())
