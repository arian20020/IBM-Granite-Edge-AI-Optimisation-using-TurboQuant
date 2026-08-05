"""Validate a Workbook 05 source-admission bundle as untrusted data.

The validator reads text and JSON evidence only. It never imports, loads, starts,
or executes a file from the evidence bundle.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable, Mapping

from scripts.testing.workbook05.configure_probe import (
    evaluate_route_a_configure_probe,
)
from scripts.testing.workbook05.hash_manifest import verify_hash_manifest
from scripts.testing.workbook05.measurement_controls import (
    validate_measurement_controls,
)
from scripts.testing.workbook05.schema_validation import validate_json_file
from scripts.testing.workbook05.source_admission import (
    evaluate_source_admission,
    is_safe_relative_evidence_path,
)
from scripts.testing.workbook05.source_admission_phase import (
    calculate_phase_decision,
)


CAMPAIGN_ID = "GTQ-WB05-MF-v1"
PHASE_ID = "phase-1-source-admission"
ROUTE_A_ID = "route-a-merged-openvino"
ROUTE_B_ID = "route-b-experimental-qjl-polar"

STEP_ORDER = (
    "preflight-validation",
    "workspace-validation",
    "measurement-control-capture",
    "route-a-runtime-verification",
    "route-a-genai-verification",
    "route-b-verification",
    "document-capture",
    "capability-inspection",
    "route-b-cmake-audit",
    "route-a-configure-probe",
    "route-decisions",
    "hashes",
)

REQUIRED_PATHS = (
    "preflight/preflight-report.json",
    "workspace/workspace-validation.json",
    "measurement/measurement-controls.json",
    "controls/campaign-manifest.json",
    "controls/route-a-source-admission.json",
    "controls/route-b-source-admission.json",
    "routes/route-a/source-tree-runtime.json",
    "routes/route-a/source-tree-genai.json",
    "routes/route-a/source-capabilities.json",
    "routes/route-b/source-tree-runtime.json",
    "routes/route-b/source-capabilities.json",
    "routes/route-b/cmake-test-discovery.json",
    "commands/route-a-merged-openvino-documented-commands.json",
    "commands/route-b-experimental-qjl-polar-documented-commands.json",
    "summary/source-admission-summary.json",
    "summary/source-admission-summary.md",
    "checkpoint/checkpoint.json",
    "orchestration-report.json",
    "hash-manifest.sha256",
    *(f"steps/{step_id}.json" for step_id in STEP_ORDER),
)

SCHEMA_BINDINGS = (
    ("preflight/preflight-report.json", "preflight-report.schema.json"),
    (
        "measurement/measurement-controls.json",
        "measurement-controls-report.schema.json",
    ),
    ("controls/campaign-manifest.json", "campaign-manifest.schema.json"),
    (
        "controls/route-a-source-admission.json",
        "source-admission.schema.json",
    ),
    (
        "controls/route-b-source-admission.json",
        "source-admission.schema.json",
    ),
    (
        "routes/route-a/source-tree-runtime.json",
        "source-tree-report.schema.json",
    ),
    (
        "routes/route-a/source-tree-genai.json",
        "source-tree-report.schema.json",
    ),
    (
        "routes/route-b/source-tree-runtime.json",
        "source-tree-report.schema.json",
    ),
    (
        "routes/route-a/source-capabilities.json",
        "source-capability-report.schema.json",
    ),
    (
        "routes/route-b/source-capabilities.json",
        "source-capability-report.schema.json",
    ),
    (
        "routes/route-b/cmake-test-discovery.json",
        "cmake-test-discovery-report.schema.json",
    ),
    (
        "routes/route-a/configure-probe.json",
        "configure-probe-report.schema.json",
    ),
    (
        "summary/source-admission-summary.json",
        "source-admission-summary.schema.json",
    ),
    ("checkpoint/checkpoint.json", "checkpoint.schema.json"),
)

PINNED_SOURCE_REPORTS = {
    "routes/route-a/source-tree-runtime.json": {
        "route_id": ROUTE_A_ID,
        "source_role": "runtime",
        "repository_full_name": "openvinotoolkit/openvino",
        "expected_origin_url": (
            "https://github.com/openvinotoolkit/openvino.git"
        ),
        "expected_commit": (
            "b9a1f201c109e0bed74763934f79483cf6c4cbf4"
        ),
    },
    "routes/route-a/source-tree-genai.json": {
        "route_id": ROUTE_A_ID,
        "source_role": "genai",
        "repository_full_name": "openvinotoolkit/openvino.genai",
        "expected_origin_url": (
            "https://github.com/openvinotoolkit/openvino.genai.git"
        ),
        "expected_commit": (
            "05e5c7670b597746f858946974d11f38e3baf42f"
        ),
    },
    "routes/route-b/source-tree-runtime.json": {
        "route_id": ROUTE_B_ID,
        "source_role": "runtime",
        "repository_full_name": "EgorDuplensky/openvino",
        "expected_origin_url": (
            "https://github.com/EgorDuplensky/openvino.git"
        ),
        "expected_commit": (
            "1827f6458d049de11c1a8203c793af67c99935dc"
        ),
    },
}

FORBIDDEN_SUFFIXES = {
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
    ".safetensors",
    ".so",
    ".tar",
    ".tgz",
    ".whl",
    ".xml",
    ".zip",
}

SECRET_PATTERNS = (
    "ghp_",
    "gho_",
    "ghs_",
    "github_pat_",
    "--token ",
    "HF_TOKEN=",
    "HUGGING_FACE_HUB_TOKEN=",
    "Authorization: Bearer ",
)

EXPECTED_MEASURED_RUN_TOP_LEVEL = {
    "schema_version",
    "campaign_id",
    "test_id",
    "run_id",
    "route",
    "workbook_id",
    "run_purpose",
    "timestamps",
    "references",
    "execution",
    "generation",
    "performance",
    "resources",
    "procedure",
    "quality",
    "evidence",
    "classification",
}

EXPECTED_QUALITY_FIELDS = {
    "raw_output_path",
    "raw_output_sha256",
    "deterministic_checks_path",
    "deterministic_failures",
    "dimension_scores",
    "critical_caps",
    "score_0_to_10",
    "matched_baseline_run_id",
    "paired_score_delta",
    "judge_label_hidden",
    "pairwise_order",
    "adjudication_path",
    "material_degradation",
    "stability_result",
}


@dataclass(frozen=True)
class SourceAdmissionBundleIssue:
    """One deterministic problem found in an untrusted Phase 1 bundle."""

    code: str
    path: str
    message: str


def _issue(
    issues: list[SourceAdmissionBundleIssue],
    code: str,
    path: str,
    message: str,
) -> None:
    """Append one reviewer-readable issue."""

    issues.append(SourceAdmissionBundleIssue(code, path, message))


def _sha256(path: Path) -> str:
    """Hash a complete evidence file without interpreting its contents."""

    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _load_json(path: Path) -> dict[str, Any]:
    """Read one BOM-tolerant JSON object without importing any payload."""

    value = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected a JSON object: {path}")
    return value


def _safe_relative_path(value: object) -> bool:
    """Accept only portable POSIX-style paths that stay inside the bundle."""

    if not isinstance(value, str):
        return False
    return is_safe_relative_evidence_path(value)


def _schema_issues(
    issues: list[SourceAdmissionBundleIssue],
    instance_path: Path,
    schema_path: Path,
    relative_path: str,
) -> None:
    """Translate shared JSON-schema findings into bundle issues."""

    try:
        for finding in validate_json_file(instance_path, schema_path):
            _issue(
                issues,
                "SCHEMA_INVALID",
                relative_path,
                f"{finding.json_path}: {finding.message}",
            )
    except (OSError, ValueError, json.JSONDecodeError, UnicodeError) as error:
        _issue(issues, "SCHEMA_INVALID", relative_path, str(error))


def _walk_evidence_paths(
    value: Any,
    location: str = "$",
) -> list[tuple[str, str]]:
    """Collect evidence-path strings from common singular and plural fields."""

    found: list[tuple[str, str]] = []
    if isinstance(value, dict):
        for key, child in value.items():
            child_location = f"{location}.{key}"
            lowered = key.casefold()
            if (
                lowered.endswith("_path")
                and isinstance(child, str)
                and child
            ):
                found.append((child_location, child))
            elif lowered.endswith("_paths") and isinstance(child, list):
                for index, item in enumerate(child):
                    if isinstance(item, str) and item:
                        found.append((f"{child_location}[{index}]", item))
            elif key == "command_records" and isinstance(child, list):
                for index, item in enumerate(child):
                    if isinstance(item, str) and item:
                        found.append((f"{child_location}[{index}]", item))
            found.extend(_walk_evidence_paths(child, child_location))
    elif isinstance(value, list):
        for index, child in enumerate(value):
            found.extend(
                _walk_evidence_paths(child, f"{location}[{index}]")
            )
    return found


def _check_required_files(
    bundle_root: Path,
    issues: list[SourceAdmissionBundleIssue],
) -> None:
    """Require every stable R2 bundle member before trusting decisions."""

    for relative_path in REQUIRED_PATHS:
        if not (bundle_root / relative_path).is_file():
            _issue(
                issues,
                "REQUIRED_PATH_MISSING",
                relative_path,
                "Required source-admission evidence file is missing.",
            )


def _check_hash_manifest(
    bundle_root: Path,
    issues: list[SourceAdmissionBundleIssue],
) -> None:
    """Verify exact file membership and hashes before semantic checks."""

    manifest_path = bundle_root / "hash-manifest.sha256"
    if not manifest_path.is_file():
        return
    for message in verify_hash_manifest(bundle_root, manifest_path):
        _issue(
            issues,
            "HASH_MISMATCH",
            "hash-manifest.sha256",
            message,
        )


def _check_schemas(
    bundle_root: Path,
    repository_root: Path,
    issues: list[SourceAdmissionBundleIssue],
) -> None:
    """Validate every controlled JSON document with its repository schema."""

    schema_root = (
        repository_root
        / "experiments/granite_turboquant_intel/schemas/workbook05"
    )
    for relative_path, schema_name in SCHEMA_BINDINGS:
        instance_path = bundle_root / relative_path
        if instance_path.is_file():
            _schema_issues(
                issues,
                instance_path,
                schema_root / schema_name,
                relative_path,
            )

    command_schema = schema_root / "documented-command-manifest.schema.json"
    for relative_path in (
        "commands/route-a-merged-openvino-documented-commands.json",
        "commands/route-b-experimental-qjl-polar-documented-commands.json",
    ):
        instance_path = bundle_root / relative_path
        if instance_path.is_file():
            _schema_issues(
                issues,
                instance_path,
                command_schema,
                relative_path,
            )


def _check_measurement_controls(
    bundle_root: Path,
    repository_root: Path,
    issues: list[SourceAdmissionBundleIssue],
) -> dict[str, Any] | None:
    """Recompute frozen-control hashes against the reviewed repository."""

    relative_path = "measurement/measurement-controls.json"
    report_path = bundle_root / relative_path
    if not report_path.is_file():
        return None

    try:
        report = _load_json(report_path)
    except (OSError, ValueError, json.JSONDecodeError, UnicodeError) as error:
        _issue(issues, "MEASUREMENT_CONTROL_INVALID", relative_path, str(error))
        return None

    configuration_path = (
        repository_root
        / "experiments/granite_turboquant_intel/configurations/"
        "workbook05/measurement-controls.json"
    )
    for finding in validate_measurement_controls(
        report,
        repository_root=repository_root,
        configuration_path=configuration_path,
    ):
        _issue(issues, finding.code, relative_path, finding.message)

    # A captured hash can truthfully describe a weakened schema. Inspect the
    # schema's required structure as a separate semantic control.
    try:
        configuration = _load_json(configuration_path)
        schema_path = repository_root / configuration["measured_run_schema_path"]
        schema = _load_json(schema_path)
        top_required = set(schema.get("required", ()))
        top_properties = schema.get("properties", {})
        quality = (
            top_properties.get("quality", {})
            if isinstance(top_properties, dict)
            else {}
        )
        quality_required = set(
            quality.get("required", ()) if isinstance(quality, dict) else ()
        )
        if (
            schema.get("type") != "object"
            or schema.get("additionalProperties") is not False
            or not EXPECTED_MEASURED_RUN_TOP_LEVEL.issubset(top_required)
            or not EXPECTED_QUALITY_FIELDS.issubset(quality_required)
        ):
            _issue(
                issues,
                "MEASURED_RUN_SCHEMA_PERMISSIVE",
                str(configuration["measured_run_schema_path"]),
                (
                    "The measured-run schema must remain closed and require "
                    "the complete performance and quality contract."
                ),
            )
    except (
        OSError,
        KeyError,
        TypeError,
        ValueError,
        json.JSONDecodeError,
        UnicodeError,
    ) as error:
        _issue(
            issues,
            "MEASURED_RUN_SCHEMA_PERMISSIVE",
            "measured-run-manifest.schema.json",
            str(error),
        )
    return report


def _check_source_provenance(
    bundle_root: Path,
    issues: list[SourceAdmissionBundleIssue],
) -> None:
    """Verify every Passed source report against the three pinned sources."""

    for relative_path, expected in PINNED_SOURCE_REPORTS.items():
        report_path = bundle_root / relative_path
        if not report_path.is_file():
            continue
        try:
            report = _load_json(report_path)
        except (OSError, ValueError, json.JSONDecodeError, UnicodeError) as error:
            _issue(
                issues,
                "SOURCE_PROVENANCE_MISMATCH",
                relative_path,
                str(error),
            )
            continue

        for field, expected_value in expected.items():
            actual = report.get(field)
            if actual != expected_value:
                _issue(
                    issues,
                    "SOURCE_PROVENANCE_MISMATCH",
                    relative_path,
                    (
                        f"{field} must be {expected_value!r}; "
                        f"found {actual!r}."
                    ),
                )

        if report.get("actual_origin_url") != expected["expected_origin_url"]:
            _issue(
                issues,
                "SOURCE_PROVENANCE_MISMATCH",
                relative_path,
                "Actual origin does not match the pinned origin.",
            )
        if report.get("actual_commit") != expected["expected_commit"]:
            _issue(
                issues,
                "SOURCE_PROVENANCE_MISMATCH",
                relative_path,
                "Actual commit does not match the pinned commit.",
            )
        if report.get("working_tree_clean") is not True:
            _issue(
                issues,
                "SOURCE_PROVENANCE_MISMATCH",
                relative_path,
                "The verified source tree is not clean.",
            )

        submodules = report.get("submodules")
        incomplete = report.get("submodules_complete") is not True
        if not isinstance(submodules, list):
            incomplete = True
        else:
            incomplete = incomplete or any(
                not isinstance(submodule, dict)
                or submodule.get("status") != "clean"
                for submodule in submodules
            )
        if incomplete:
            _issue(
                issues,
                "SUBMODULE_INCOMPLETE",
                relative_path,
                "Recursive submodule evidence is incomplete or not clean.",
            )

        command_records = report.get("command_records")
        if isinstance(command_records, list):
            for command_record in command_records:
                if (
                    not _safe_relative_path(command_record)
                    or not (bundle_root / command_record).is_file()
                ):
                    _issue(
                        issues,
                        "SOURCE_COMMAND_RECORD_MISSING",
                        relative_path,
                        f"Missing or unsafe command record: {command_record!r}",
                    )


def _load_route_records(
    bundle_root: Path,
    issues: list[SourceAdmissionBundleIssue],
) -> tuple[dict[str, Any] | None, dict[str, Any] | None]:
    """Load and independently recalculate both route admission records."""

    records: list[dict[str, Any] | None] = []
    for relative_path in (
        "controls/route-a-source-admission.json",
        "controls/route-b-source-admission.json",
    ):
        path = bundle_root / relative_path
        if not path.is_file():
            records.append(None)
            continue
        try:
            record = _load_json(path)
            decision = evaluate_source_admission(record)
            if (
                record.get("admission_status") == "Admitted"
                and not decision.permitted
            ):
                _issue(
                    issues,
                    "FALSE_ADMISSION",
                    relative_path,
                    "; ".join(decision.reasons),
                )

            for proof in record.get("proofs", ()):
                if isinstance(proof, dict) and proof.get("status") == "Passed":
                    evidence_path = proof.get("evidence_path")
                    if (
                        not _safe_relative_path(evidence_path)
                        or not (bundle_root / str(evidence_path)).is_file()
                    ):
                        _issue(
                            issues,
                            "FALSE_ADMISSION",
                            relative_path,
                            (
                                f"Passed proof {proof.get('proof_id')} has "
                                f"missing or unsafe evidence: {evidence_path!r}"
                            ),
                        )
            records.append(record)
        except (
            KeyError,
            OSError,
            TypeError,
            ValueError,
            json.JSONDecodeError,
            UnicodeError,
        ) as error:
            _issue(issues, "FALSE_ADMISSION", relative_path, str(error))
            records.append(None)
    return records[0], records[1]


def _check_route_a_configure(
    bundle_root: Path,
    route_a_record: Mapping[str, Any] | None,
    summary: Mapping[str, Any] | None,
    issues: list[SourceAdmissionBundleIssue],
) -> None:
    """Require exact CPU-only CMake generation before Route A admission."""

    summary_status = None
    if summary is not None:
        route_decisions = summary.get("route_decisions")
        if isinstance(route_decisions, dict):
            route_a = route_decisions.get(ROUTE_A_ID)
            if isinstance(route_a, dict):
                summary_status = route_a.get("status")

    admitted = (
        route_a_record is not None
        and route_a_record.get("admission_status") == "Admitted"
    ) or summary_status == "Admitted"
    if not admitted:
        return

    relative_path = "routes/route-a/configure-probe.json"
    report_path = bundle_root / relative_path
    if not report_path.is_file():
        _issue(
            issues,
            "ROUTE_A_CONFIGURE_INVALID",
            relative_path,
            "Route A is admitted without a configure-probe report.",
        )
        return

    try:
        report = _load_json(report_path)
        cache_relative = report.get("cmake_cache_path")
        if not _safe_relative_path(cache_relative):
            raise ValueError(f"Unsafe CMake cache path: {cache_relative!r}")
        cache_path = report_path.parent / str(cache_relative)
        if not cache_path.is_file():
            raise ValueError(f"CMake cache is missing: {cache_relative}")
        if _sha256(cache_path) != report.get("cmake_cache_sha256"):
            raise ValueError("CMake cache SHA-256 does not match the report.")

        decision = evaluate_route_a_configure_probe(
            report.get("command", ()),
            int(report.get("exit_code", -1)),
            cache_path.read_text(
                encoding="utf-8-sig",
                errors="replace",
            ),
        )
        reasons: list[str] = list(decision.reasons)
        if report.get("status") != "Passed":
            reasons.append("Configure report status is not Passed.")
        for flag in (
            "build_invoked",
            "install_invoked",
            "package_invoked",
        ):
            if report.get(flag) is not False:
                reasons.append(f"{flag} must remain false.")
        if report.get("cache_values") != dict(decision.cache_values):
            reasons.append("Reported cache values do not match the CMake cache.")

        for evidence_field in ("stdout_path", "stderr_path"):
            evidence_relative = report.get(evidence_field)
            if (
                not _safe_relative_path(evidence_relative)
                or not (report_path.parent / str(evidence_relative)).is_file()
            ):
                reasons.append(
                    f"{evidence_field} is missing or unsafe: "
                    f"{evidence_relative!r}."
                )

        for reason in reasons:
            _issue(
                issues,
                "ROUTE_A_CONFIGURE_INVALID",
                relative_path,
                reason,
            )
    except (
        OSError,
        TypeError,
        ValueError,
        json.JSONDecodeError,
        UnicodeError,
    ) as error:
        _issue(
            issues,
            "ROUTE_A_CONFIGURE_INVALID",
            relative_path,
            str(error),
        )


def _check_decision_consistency(
    bundle_root: Path,
    route_a_record: Mapping[str, Any] | None,
    route_b_record: Mapping[str, Any] | None,
    measurement_report: Mapping[str, Any] | None,
    issues: list[SourceAdmissionBundleIssue],
) -> dict[str, Any] | None:
    """Recalculate the summary so stale or optimistic claims cannot pass."""

    relative_path = "summary/source-admission-summary.json"
    summary_path = bundle_root / relative_path
    if not summary_path.is_file():
        return None
    try:
        summary = _load_json(summary_path)
    except (OSError, ValueError, json.JSONDecodeError, UnicodeError) as error:
        _issue(issues, "DECISION_MISMATCH", relative_path, str(error))
        return None

    measurement_path = bundle_root / "measurement/measurement-controls.json"
    if measurement_path.is_file():
        actual_measurement_hash = _sha256(measurement_path)
        if summary.get("measurement_controls_sha256") != actual_measurement_hash:
            _issue(
                issues,
                "CONTROL_HASH_MISMATCH",
                relative_path,
                "Summary measurement-control hash does not match the file.",
            )

    if (
        route_a_record is None
        or route_b_record is None
        or measurement_report is None
    ):
        return summary

    try:
        phase = calculate_phase_decision(
            route_a_record,
            route_b_record,
            measurement_report,
        )
        expected_statuses = {
            ROUTE_A_ID: phase.route_a_status,
            ROUTE_B_ID: phase.route_b_status,
        }
        route_decisions = summary.get("route_decisions")
        if not isinstance(route_decisions, dict):
            raise ValueError("Summary route_decisions must be an object.")

        for route_id, expected_status in expected_statuses.items():
            route_summary = route_decisions.get(route_id)
            actual_status = (
                route_summary.get("status")
                if isinstance(route_summary, dict)
                else None
            )
            if actual_status != expected_status:
                _issue(
                    issues,
                    "DECISION_MISMATCH",
                    relative_path,
                    (
                        f"{route_id} must be {expected_status}; "
                        f"found {actual_status!r}."
                    ),
                )
            if isinstance(route_summary, dict):
                for evidence_path in route_summary.get("evidence_paths", ()):
                    if (
                        not _safe_relative_path(evidence_path)
                        or not (bundle_root / str(evidence_path)).is_file()
                    ):
                        _issue(
                            issues,
                            "UNSAFE_EVIDENCE_PATH",
                            relative_path,
                            f"Missing or unsafe route evidence: {evidence_path!r}",
                        )

        if summary.get("checkpoint_status") != phase.checkpoint_status:
            _issue(
                issues,
                "DECISION_MISMATCH",
                relative_path,
                (
                    "Checkpoint status does not match the recalculated "
                    f"Phase 1 status {phase.checkpoint_status}."
                ),
            )
    except (KeyError, TypeError, ValueError) as error:
        _issue(issues, "DECISION_MISMATCH", relative_path, str(error))
    return summary


def _check_route_b_blocker(
    bundle_root: Path,
    route_b_record: Mapping[str, Any] | None,
    summary: Mapping[str, Any] | None,
    issues: list[SourceAdmissionBundleIssue],
) -> None:
    """Forbid experimental admission while RB-SRC-001 is confirmed."""

    audit_path = bundle_root / "routes/route-b/cmake-test-discovery.json"
    if not audit_path.is_file():
        return
    try:
        audit = _load_json(audit_path)
    except (OSError, ValueError, json.JSONDecodeError, UnicodeError):
        return
    if audit.get("status") != "Confirmed":
        return

    record_admitted = (
        route_b_record is not None
        and route_b_record.get("admission_status") == "Admitted"
    )
    summary_admitted = False
    if summary is not None:
        route_decisions = summary.get("route_decisions")
        if isinstance(route_decisions, dict):
            route = route_decisions.get(ROUTE_B_ID)
            summary_admitted = (
                isinstance(route, dict) and route.get("status") == "Admitted"
            )
    if record_admitted or summary_admitted:
        _issue(
            issues,
            "ROUTE_B_BLOCKER_CONFLICT",
            "routes/route-b/cmake-test-discovery.json",
            "Route B cannot be admitted while RB-SRC-001 is confirmed.",
        )


def _check_json_paths_and_payloads(
    bundle_root: Path,
    issues: list[SourceAdmissionBundleIssue],
) -> None:
    """Reject dangerous suffixes, secrets, and traversal in JSON references."""

    for candidate in sorted(
        path for path in bundle_root.rglob("*") if path.is_file()
    ):
        relative_path = candidate.relative_to(bundle_root).as_posix()
        suffix = candidate.suffix.casefold()
        if suffix in FORBIDDEN_SUFFIXES:
            _issue(
                issues,
                "FORBIDDEN_PAYLOAD",
                relative_path,
                f"Forbidden source-admission payload suffix: {suffix}",
            )
            continue

        try:
            text = candidate.read_text(encoding="utf-8-sig")
        except (OSError, UnicodeDecodeError):
            text = ""

        for pattern in SECRET_PATTERNS:
            if pattern in text:
                _issue(
                    issues,
                    "SECRET_PATTERN",
                    relative_path,
                    f"Matched forbidden secret pattern: {pattern}",
                )

        if suffix != ".json" or not text:
            continue
        try:
            value = json.loads(text)
        except json.JSONDecodeError:
            continue
        for location, evidence_path in _walk_evidence_paths(value):
            if not _safe_relative_path(evidence_path):
                _issue(
                    issues,
                    "UNSAFE_EVIDENCE_PATH",
                    relative_path,
                    f"{location}: {evidence_path}",
                )


def _deduplicate_and_sort(
    issues: Iterable[SourceAdmissionBundleIssue],
) -> list[SourceAdmissionBundleIssue]:
    """Return stable unique findings for deterministic review reports."""

    unique = {
        (issue.code, issue.path, issue.message): issue
        for issue in issues
    }
    return [unique[key] for key in sorted(unique)]


def validate_source_admission_bundle(
    bundle_root: Path,
    repository_root: Path,
) -> list[SourceAdmissionBundleIssue]:
    """Return every integrity and scientific-claim issue in the bundle."""

    bundle_root = bundle_root.resolve()
    repository_root = repository_root.resolve()
    issues: list[SourceAdmissionBundleIssue] = []

    _check_required_files(bundle_root, issues)
    _check_hash_manifest(bundle_root, issues)
    _check_schemas(bundle_root, repository_root, issues)
    measurement_report = _check_measurement_controls(
        bundle_root,
        repository_root,
        issues,
    )
    _check_source_provenance(bundle_root, issues)
    route_a_record, route_b_record = _load_route_records(
        bundle_root,
        issues,
    )
    summary = _check_decision_consistency(
        bundle_root,
        route_a_record,
        route_b_record,
        measurement_report,
        issues,
    )
    _check_route_a_configure(
        bundle_root,
        route_a_record,
        summary,
        issues,
    )
    _check_route_b_blocker(
        bundle_root,
        route_b_record,
        summary,
        issues,
    )
    _check_json_paths_and_payloads(bundle_root, issues)
    return _deduplicate_and_sort(issues)


def render_validation_report(
    issues: Iterable[SourceAdmissionBundleIssue],
) -> str:
    """Render one deterministic Markdown report for every validator outcome."""

    ordered = _deduplicate_and_sort(issues)
    lines = [
        "# Workbook 05 source-admission artifact validation",
        "",
    ]
    if not ordered:
        lines.extend(
            [
                "Validation passed.",
                "",
                (
                    "Hashes, schemas, pinned provenance, recursive "
                    "submodules, route decisions, Route A configure "
                    "evidence, Route B blocker boundaries, measurement "
                    "controls, paths, secrets, and payload suffixes are "
                    "valid."
                ),
            ]
        )
    else:
        lines.extend(
            [
                f"Validation failed with {len(ordered)} issue(s).",
                "",
            ]
        )
        lines.extend(
            f"- `{issue.code}` `{issue.path}` — {issue.message}"
            for issue in ordered
        )
    return "\n".join(lines) + "\n"


def main(argv: Iterable[str] | None = None) -> int:
    """Validate one bundle, always write the Markdown report, and exit truthfully."""

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle-root", type=Path, required=True)
    parser.add_argument("--repository-root", type=Path, required=True)
    parser.add_argument(
        "--report",
        "--summary",
        dest="report",
        type=Path,
        required=True,
    )
    arguments = parser.parse_args(list(argv) if argv is not None else None)

    try:
        issues = validate_source_admission_bundle(
            arguments.bundle_root,
            arguments.repository_root,
        )
    except (
        KeyError,
        OSError,
        TypeError,
        ValueError,
        json.JSONDecodeError,
        UnicodeError,
    ) as error:
        issues = [
            SourceAdmissionBundleIssue(
                "VALIDATOR_ERROR",
                ".",
                str(error),
            )
        ]

    report = render_validation_report(issues)
    arguments.report.parent.mkdir(parents=True, exist_ok=True)
    arguments.report.write_text(
        report,
        encoding="utf-8",
        newline="\n",
    )
    print(report, end="")
    return 1 if issues else 0


if __name__ == "__main__":
    raise SystemExit(main())
