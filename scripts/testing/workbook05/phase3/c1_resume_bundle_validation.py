"""Validate a controlled Workbook 05 C1 source-resume artifact as untrusted data."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any, Iterable, Mapping, Sequence

from scripts.testing.workbook05.phase3.asset_bundle_validation import (
    BundleIssue,
    validate_asset_bundle,
)
from scripts.testing.workbook05.phase3.c1_resume import (
    CONVERSION_MINIMUM_FREE_BYTES,
    EXPECTED_DEPENDENCY_DECISION_SHA256,
    EXPECTED_MODEL_SHA256,
    EXPECTED_PARTIAL_CONVERSION_DIRECTORY,
    EXPECTED_REVISION,
    EXPECTED_SOURCE_DIRECTORY,
    EXPECTED_TOKENIZER_SHA256,
    FORMAL_REPOSITORY,
    PRIOR_ARTIFACT_DIGEST,
    PRIOR_ARTIFACT_NAME,
    PRIOR_HEAD_SHA,
    PRIOR_RUN_ATTEMPT,
    PRIOR_RUN_ID,
    validate_prior_failure_bundle,
)


RESUME_REQUIRED_PATHS = (
    "dependency-acceptance-proof.json",
    "dependency/decision.json",
    "resolved-model.json",
    "stage-order.json",
    "prior-artifact-identity.json",
    "prior-attempt-validation.json",
    "retained-source-proof.json",
    "resource-preflight.json",
    "prior-attempt/failure.json",
    "prior-attempt/resolved-model.json",
    "prior-attempt/source-files.csv",
)
RESUME_STAGE_ORDER = (
    "prerequisite-verification",
    "prior-artifact-validation",
    "retained-source-requalification",
    "conversion-disk-preflight",
    "resource-preflight",
    "conversion-new-output-directory",
    "converted-file-hash-inventory",
    "schema-validation",
    "manifest-generation",
)
RESUME_OPERATION = "controlled-source-resume"
RESOURCE_MINIMUM_AVAILABLE_BYTES = 4 * 1024 * 1024 * 1024
RESOURCE_MAXIMUM_COMMIT_PERCENT = 70
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


def _add(issues: list[BundleIssue], code: str, path: str, message: str) -> None:
    issues.append(BundleIssue(code=code, path=path, message=message))


def _load(bundle: Path, relative: str, issues: list[BundleIssue]) -> dict[str, Any] | None:
    path = bundle / relative
    if not path.is_file() or path.is_symlink():
        return None
    try:
        value = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        _add(issues, "JSON_INVALID", relative, str(error))
        return None
    if not isinstance(value, dict):
        _add(issues, "JSON_INVALID", relative, "Expected one JSON object.")
        return None
    return value


def _walk(value: object, path: str = "$") -> Iterable[tuple[str, str | None, object]]:
    if isinstance(value, Mapping):
        for key in sorted(value, key=str):
            child_path = f"{path}.{key}"
            child = value[key]
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
    for json_path, key, value in _walk(payload):
        if key in _CLAIM_KEYS and value is not False:
            _add(
                issues,
                "SCIENTIFIC_CLAIM",
                relative,
                f"{json_path} must remain false at the C1 boundary.",
            )


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _is_integer(value: object) -> bool:
    """Reject booleans even though Python represents them as integers."""

    return isinstance(value, int) and not isinstance(value, bool)


def _is_number(value: object) -> bool:
    """Accept a real numeric observation but reject Boolean lookalikes."""

    return isinstance(value, (int, float)) and not isinstance(value, bool)


def _check_identity_record(
    identity: Mapping[str, Any],
    issues: list[BundleIssue],
) -> None:
    expected = {
        "prior_run_id": PRIOR_RUN_ID,
        "prior_run_attempt": PRIOR_RUN_ATTEMPT,
        "prior_artifact_name": PRIOR_ARTIFACT_NAME,
        "prior_artifact_digest": PRIOR_ARTIFACT_DIGEST,
        "prior_head_sha": PRIOR_HEAD_SHA,
        "status": "Passed",
    }
    drifted = [key for key, value in expected.items() if identity.get(key) != value]
    if drifted:
        _add(
            issues,
            "PRIOR_ARTIFACT_IDENTITY",
            "prior-artifact-identity.json",
            "The resumed artifact is not bound to the accepted failed attempt at: "
            + ", ".join(sorted(drifted)),
        )


def _check_prior_proof(
    proof: Mapping[str, Any],
    issues: list[BundleIssue],
) -> None:
    expected = {
        "status": "Passed",
        "prior_run_id": PRIOR_RUN_ID,
        "prior_run_attempt": PRIOR_RUN_ATTEMPT,
        "prior_artifact_name": PRIOR_ARTIFACT_NAME,
        "prior_artifact_digest": PRIOR_ARTIFACT_DIGEST,
        "prior_head_sha": PRIOR_HEAD_SHA,
        "repository": FORMAL_REPOSITORY,
        "resolved_revision": EXPECTED_REVISION,
        "source_directory": EXPECTED_SOURCE_DIRECTORY,
        "aggregate_model_sha256": EXPECTED_MODEL_SHA256,
        "aggregate_tokenizer_sha256": EXPECTED_TOKENIZER_SHA256,
    }
    drifted = [key for key, value in expected.items() if proof.get(key) != value]
    if drifted:
        _add(
            issues,
            "PRIOR_ARTIFACT_VALIDATION",
            "prior-attempt-validation.json",
            "The prior-attempt validation proof drifted at: "
            + ", ".join(sorted(drifted)),
        )


def _check_source_proof(
    proof: Mapping[str, Any],
    issues: list[BundleIssue],
) -> None:
    expected = {
        "status": "Passed",
        "prior_run_id": PRIOR_RUN_ID,
        "repository": FORMAL_REPOSITORY,
        "resolved_revision": EXPECTED_REVISION,
        "source_directory": EXPECTED_SOURCE_DIRECTORY,
        "aggregate_model_sha256": EXPECTED_MODEL_SHA256,
        "aggregate_tokenizer_sha256": EXPECTED_TOKENIZER_SHA256,
        "source_reused_read_only": True,
        "prior_partial_conversion_reused": False,
    }
    drifted = [key for key, value in expected.items() if proof.get(key) != value]
    if drifted:
        _add(
            issues,
            "RETAINED_SOURCE_PROOF",
            "retained-source-proof.json",
            "The retained-source proof drifted at: " + ", ".join(sorted(drifted)),
        )


def _check_stage_order(
    record: Mapping[str, Any],
    issues: list[BundleIssue],
) -> None:
    drifted: list[str] = []
    if record.get("status") != "Passed":
        drifted.append("status")
    if record.get("operation") != RESUME_OPERATION:
        drifted.append("operation")
    if record.get("stages") != list(RESUME_STAGE_ORDER):
        drifted.append("stages")
    if drifted:
        _add(
            issues,
            "STAGE_ORDER_RELATIONSHIP",
            "stage-order.json",
            "Expected the exact controlled C1 source-resume operation, stage order, "
            "and status Passed; drifted at: " + ", ".join(sorted(drifted)),
        )


def _check_resource_preflight(
    record: Mapping[str, Any],
    issues: list[BundleIssue],
) -> None:
    """Verify that a Passed resource preflight is supported by its observations."""

    drifted: list[str] = []
    minimum = record.get("minimum_available_memory_bytes")
    maximum_commit = record.get("maximum_commit_percent")
    available = record.get("available_memory_bytes")
    commit = record.get("commit_percent")
    conflicts = record.get("conflicting_processes")

    if record.get("status") != "Passed":
        drifted.append("status")
    if minimum != RESOURCE_MINIMUM_AVAILABLE_BYTES:
        drifted.append("minimum_available_memory_bytes")
    if maximum_commit != RESOURCE_MAXIMUM_COMMIT_PERCENT:
        drifted.append("maximum_commit_percent")
    if not _is_integer(available) or available < RESOURCE_MINIMUM_AVAILABLE_BYTES:
        drifted.append("available_memory_bytes")
    if not _is_number(commit) or float(commit) > RESOURCE_MAXIMUM_COMMIT_PERCENT:
        drifted.append("commit_percent")
    if not isinstance(conflicts, list) or conflicts:
        drifted.append("conflicting_processes")

    if drifted:
        _add(
            issues,
            "RESOURCE_PREFLIGHT",
            "resource-preflight.json",
            "A Passed resource preflight must prove at least 4 GiB available "
            "physical memory, commit at or below 70 percent, no conflicting "
            "processes, and the exact reviewed thresholds; drifted at: "
            + ", ".join(sorted(set(drifted))),
        )


def _check_disk_preflight(
    record: Mapping[str, Any],
    issues: list[BundleIssue],
) -> None:
    """Verify conversion-only capacity and the non-destructive recovery boundary."""

    drifted: list[str] = []
    minimum = record.get("minimum_free_bytes_for_conversion_resume")
    free_bytes = record.get("free_bytes")
    if record.get("status") != "Passed":
        drifted.append("status")
    if minimum != CONVERSION_MINIMUM_FREE_BYTES:
        drifted.append("minimum_free_bytes_for_conversion_resume")
    if not _is_integer(free_bytes) or free_bytes < CONVERSION_MINIMUM_FREE_BYTES:
        drifted.append("free_bytes")
    if drifted:
        _add(
            issues,
            "DISK_PREFLIGHT",
            "disk-preflight.json",
            "A Passed conversion-only disk preflight must prove the exact 20 GiB "
            "reserve and sufficient observed free space; drifted at: "
            + ", ".join(sorted(set(drifted))),
        )

    download_drift = [
        key
        for key in ("source_download_required", "source_download_authorised")
        if record.get(key) is not False
    ]
    if download_drift:
        _add(
            issues,
            "DOWNLOAD_AUTHORITY",
            "disk-preflight.json",
            "Controlled source resume must neither require nor authorise another "
            "model download; drifted at: " + ", ".join(sorted(download_drift)),
        )

    deletion_drift = [
        key
        for key in ("deletion_authorised", "deletion_performed")
        if record.get(key) is not False
    ]
    if deletion_drift:
        _add(
            issues,
            "DELETION_AUTHORITY",
            "disk-preflight.json",
            "Controlled source resume must neither authorise nor perform deletion; "
            "drifted at: " + ", ".join(sorted(deletion_drift)),
        )

    preservation_drift = [
        key
        for key in (
            "prior_failed_workspace_preserved",
            "prior_partial_conversion_preserved",
        )
        if record.get(key) is not True
    ]
    if preservation_drift:
        _add(
            issues,
            "PRESERVATION",
            "disk-preflight.json",
            "The failed evidence workspace and partial conversion must remain "
            "preserved; drifted at: " + ", ".join(sorted(preservation_drift)),
        )


def _check_dependency_binding(
    bundle: Path,
    proof: Mapping[str, Any],
    conversion: Mapping[str, Any] | None,
    issues: list[BundleIssue],
) -> None:
    decision_path = bundle / "dependency" / "decision.json"
    try:
        decision_digest = _sha256(decision_path)
        decision = json.loads(decision_path.read_text(encoding="utf-8-sig"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        _add(issues, "DEPENDENCY_DECISION_MISMATCH", "dependency/decision.json", str(error))
        return
    drifted: list[str] = []
    if decision_digest != EXPECTED_DEPENDENCY_DECISION_SHA256:
        drifted.append("decision-bytes")
    if proof.get("decision_sha256") != decision_digest:
        drifted.append("proof.decision_sha256")
    if not isinstance(decision, Mapping) or decision.get("status") != "Passed":
        drifted.append("decision.status")
    reference = conversion.get("dependency_preflight") if isinstance(conversion, Mapping) else None
    if not isinstance(reference, Mapping):
        drifted.append("conversion-record.dependency_preflight")
    else:
        if reference.get("status") != "Passed":
            drifted.append("conversion-record.dependency_preflight.status")
        if reference.get("record_path") != "dependency/decision.json":
            drifted.append("conversion-record.dependency_preflight.record_path")
        if reference.get("record_sha256") != decision_digest:
            drifted.append("conversion-record.dependency_preflight.record_sha256")
    if drifted:
        _add(
            issues,
            "DEPENDENCY_DECISION_MISMATCH",
            "dependency-acceptance-proof.json",
            "The dependency binding drifted at: " + ", ".join(sorted(set(drifted))),
        )


def _check_model_relationship(
    resolved: Mapping[str, Any],
    asset: Mapping[str, Any] | None,
    issues: list[BundleIssue],
) -> None:
    expected_resolved = {
        "repository": FORMAL_REPOSITORY,
        "requested_revision": "main",
        "resolved_revision": EXPECTED_REVISION,
        "source_directory": EXPECTED_SOURCE_DIRECTORY,
        "aggregate_model_sha256": EXPECTED_MODEL_SHA256,
        "aggregate_tokenizer_sha256": EXPECTED_TOKENIZER_SHA256,
    }
    drifted = [key for key, value in expected_resolved.items() if resolved.get(key) != value]
    if drifted:
        _add(
            issues,
            "RESOLVED_MODEL_RELATIONSHIP",
            "resolved-model.json",
            "The resumed source identity drifted at: " + ", ".join(sorted(drifted)),
        )
    if not isinstance(asset, Mapping):
        return
    source = asset.get("source")
    relationship = {
        "repository": source.get("repository") if isinstance(source, Mapping) else None,
        "resolved_revision": source.get("resolved_revision") if isinstance(source, Mapping) else None,
        "source_directory": asset.get("source_directory"),
        "aggregate_model_sha256": asset.get("aggregate_model_sha256"),
        "aggregate_tokenizer_sha256": asset.get("aggregate_tokenizer_sha256"),
    }
    expected_asset = {
        "repository": FORMAL_REPOSITORY,
        "resolved_revision": EXPECTED_REVISION,
        "source_directory": EXPECTED_SOURCE_DIRECTORY,
        "aggregate_model_sha256": EXPECTED_MODEL_SHA256,
        "aggregate_tokenizer_sha256": EXPECTED_TOKENIZER_SHA256,
    }
    asset_drift = [key for key, value in expected_asset.items() if relationship.get(key) != value]
    converted = str(asset.get("converted_directory", ""))
    if converted.casefold() == EXPECTED_PARTIAL_CONVERSION_DIRECTORY.casefold():
        asset_drift.append("converted_directory-reuses-prior-partial-output")
    if "-resume-" not in converted.casefold():
        asset_drift.append("converted_directory-resume-identity")
    if asset_drift:
        _add(
            issues,
            "RESOLVED_MODEL_RELATIONSHIP",
            "asset-lock.json",
            "The final asset relationship drifted at: " + ", ".join(sorted(set(asset_drift))),
        )


def _check_conversion_command(
    command: Mapping[str, Any],
    issues: list[BundleIssue],
) -> None:
    arguments = command.get("arguments")
    expected_prefix = [
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
    ]
    if not isinstance(arguments, list) or arguments[:12] != expected_prefix:
        _add(
            issues,
            "COMMAND_IDENTITY",
            "commands/conversion.json",
            "The resumed conversion command does not match the reviewed candidate.",
        )
        return
    output = str(arguments[-1])
    if output.casefold() == EXPECTED_PARTIAL_CONVERSION_DIRECTORY.casefold() or "-resume-" not in output.casefold():
        _add(
            issues,
            "COMMAND_IDENTITY",
            "commands/conversion.json",
            "The resumed conversion must use one new resume-scoped output directory.",
        )
    if command.get("exit_code") != 0 or command.get("safety_stop_triggered") is not False:
        _add(
            issues,
            "COMMAND_RESULT",
            "commands/conversion.json",
            "The resumed conversion did not complete cleanly without a safety stop.",
        )


def validate_c1_resume_bundle(
    bundle_root: Path,
    repository_root: Path,
) -> list[BundleIssue]:
    """Return generic and resume-specific issues for one same-attempt artifact."""

    issues = list(validate_asset_bundle(bundle_root, repository_root))
    try:
        bundle = bundle_root.resolve(strict=True)
    except OSError:
        return issues
    if not bundle.is_dir() or bundle_root.is_symlink():
        return issues

    for relative in RESUME_REQUIRED_PATHS:
        candidate = bundle / relative
        if not candidate.is_file() or candidate.is_symlink():
            _add(
                issues,
                "REQUIRED_PATH_MISSING",
                relative,
                "The controlled source-resume artifact is missing required evidence.",
            )

    identity = _load(bundle, "prior-artifact-identity.json", issues)
    prior_proof = _load(bundle, "prior-attempt-validation.json", issues)
    source_proof = _load(bundle, "retained-source-proof.json", issues)
    resource_preflight = _load(bundle, "resource-preflight.json", issues)
    disk_preflight = _load(bundle, "disk-preflight.json", issues)
    stage_order = _load(bundle, "stage-order.json", issues)
    dependency_proof = _load(bundle, "dependency-acceptance-proof.json", issues)
    resolved = _load(bundle, "resolved-model.json", issues)
    asset = _load(bundle, "asset-lock.json", issues)
    conversion = _load(bundle, "conversion-record.json", issues)
    command = _load(bundle, "commands/conversion.json", issues)

    for relative, payload in (
        ("prior-artifact-identity.json", identity),
        ("prior-attempt-validation.json", prior_proof),
        ("retained-source-proof.json", source_proof),
        ("resource-preflight.json", resource_preflight),
        ("disk-preflight.json", disk_preflight),
        ("stage-order.json", stage_order),
        ("dependency-acceptance-proof.json", dependency_proof),
        ("resolved-model.json", resolved),
        ("asset-lock.json", asset),
        ("conversion-record.json", conversion),
        ("commands/conversion.json", command),
    ):
        if payload is not None:
            _check_non_claims(relative, payload, issues)

    if identity is not None:
        _check_identity_record(identity, issues)
    if prior_proof is not None:
        _check_prior_proof(prior_proof, issues)
    if source_proof is not None:
        _check_source_proof(source_proof, issues)
    if resource_preflight is not None:
        _check_resource_preflight(resource_preflight, issues)
    if disk_preflight is not None:
        _check_disk_preflight(disk_preflight, issues)
    if stage_order is not None:
        _check_stage_order(stage_order, issues)
    if dependency_proof is not None:
        _check_dependency_binding(bundle, dependency_proof, conversion, issues)
    if resolved is not None:
        _check_model_relationship(resolved, asset, issues)
    if command is not None:
        _check_conversion_command(command, issues)

    prior_root = bundle / "prior-attempt"
    if prior_root.is_dir() and not prior_root.is_symlink():
        prior_result = validate_prior_failure_bundle(
            prior_root,
            expected_model_sha256=EXPECTED_MODEL_SHA256,
            expected_tokenizer_sha256=EXPECTED_TOKENIZER_SHA256,
        )
        for message in prior_result.issues:
            _add(issues, "PRIOR_ARTIFACT_INVALID", "prior-attempt", message)

    return sorted(
        issues,
        key=lambda issue: (issue.path.casefold(), issue.code, issue.message),
    )


def _write_report(path: Path, issues: Sequence[BundleIssue]) -> None:
    lines = ["# Workbook 05 controlled C1 source-resume validation", ""]
    if not issues:
        lines.append(
            "Validation passed: the resumed asset bundle, prior-attempt binding, "
            "retained-source identity, conversion output, hashes, and all C1 "
            "non-claims are valid."
        )
    else:
        lines.append(f"Validation failed with {len(issues)} issue(s).")
        lines.append("")
        lines.extend(
            f"- `{issue.code}` `{issue.path}` — {issue.message}" for issue in issues
        )
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle-root", type=Path, required=True)
    parser.add_argument("--repository-root", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    args = parser.parse_args(argv)
    issues = validate_c1_resume_bundle(args.bundle_root, args.repository_root)
    _write_report(args.report, issues)
    print(args.report.read_text(encoding="utf-8"), end="")
    return 0 if not issues else 1


if __name__ == "__main__":
    raise SystemExit(main())
