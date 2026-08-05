"""Validate a Workbook 05 source-admission bundle as untrusted data.

The validator only reads text and JSON evidence. It never imports, loads,
starts, or executes a file from the evidence bundle.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable, Mapping

from scripts.testing.workbook05.configure_probe import evaluate_route_a_configure_probe
from scripts.testing.workbook05.hash_manifest import verify_hash_manifest
from scripts.testing.workbook05.measurement_controls import validate_measurement_controls
from scripts.testing.workbook05.schema_validation import validate_json_file
from scripts.testing.workbook05.source_admission import (
    evaluate_source_admission,
    is_safe_relative_evidence_path,
)
from scripts.testing.workbook05.source_admission_phase import calculate_phase_decision

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
    ("controls/route-a-source-admission.json", "source-admission.schema.json"),
    ("controls/route-b-source-admission.json", "source-admission.schema.json"),
    ("routes/route-a/source-tree-runtime.json", "source-tree-report.schema.json"),
    ("routes/route-a/source-tree-genai.json", "source-tree-report.schema.json"),
    ("routes/route-b/source-tree-runtime.json", "source-tree-report.schema.json"),
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
    ("routes/route-a/configure-probe.json", "configure-probe-report.schema.json"),
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
        "expected_origin_url": "https://github.com/openvinotoolkit/openvino.git",
        "expected_commit": "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
    },
    "routes/route-a/source-tree-genai.json": {
        "route_id": ROUTE_A_ID,
        "source_role": "genai-compatibility-candidate",
        "repository_full_name": "openvinotoolkit/openvino.genai",
        "expected_origin_url": (
            "https://github.com/openvinotoolkit/openvino.genai.git"
        ),
        "expected_commit": "05e5c7670b597746f858946974d11f38e3baf42f",
    },
    "routes/route-b/source-tree-runtime.json": {
        "route_id": ROUTE_B_ID,
        "source_role": "experimental-runtime",
        "repository_full_name": "EgorDuplensky/openvino",
        "expected_origin_url": "https://github.com/EgorDuplensky/openvino.git",
        "expected_commit": "1827f6458d049de11c1a8203c793af67c99935dc",
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
    """One deterministic issue found in an untrusted Phase 1 bundle."""

    code: str
    path: str
    message: str


def _add(
    issues: list[SourceAdmissionBundleIssue],
    code: str,
    path: str,
    message: str,
) -> None:
    """Append one reviewer-readable issue."""

    issues.append(SourceAdmissionBundleIssue(code, path, message))


def _load_json(path: Path) -> dict[str, Any]:
    """Read a BOM-tolerant JSON object without importing bundle code."""

    value = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected a JSON object: {path}")
    return value


def _sha256(path: Path) -> str:
    """Hash a complete evidence file."""

    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _safe(value: object) -> bool:
    """Accept only traversal-free portable relative evidence paths."""

    return isinstance(value, str) and is_safe_relative_evidence_path(value)


def _schema_root(repository_root: Path) -> Path:
    """Return the controlled Workbook 05 schema directory."""

    return (
        repository_root
        / "experiments/granite_turboquant_intel/schemas/workbook05"
    )


def _check_required_and_hashes(
    bundle: Path,
    issues: list[SourceAdmissionBundleIssue],
) -> None:
    """Check stable bundle membership and the exact hash manifest first."""

    for relative in REQUIRED_PATHS:
        if not (bundle / relative).is_file():
            _add(
                issues,
                "REQUIRED_PATH_MISSING",
                relative,
                "Required source-admission evidence file is missing.",
            )
    manifest = bundle / "hash-manifest.sha256"
    if manifest.is_file():
        for message in verify_hash_manifest(bundle, manifest):
            _add(issues, "HASH_MISMATCH", "hash-manifest.sha256", message)


def _check_schemas(
    bundle: Path,
    repository: Path,
    issues: list[SourceAdmissionBundleIssue],
) -> None:
    """Validate every controlled JSON object against repository schemas."""

    root = _schema_root(repository)
    bindings = list(SCHEMA_BINDINGS) + [
        (
            "commands/route-a-merged-openvino-documented-commands.json",
            "documented-command-manifest.schema.json",
        ),
        (
            "commands/route-b-experimental-qjl-polar-documented-commands.json",
            "documented-command-manifest.schema.json",
        ),
    ]
    for relative, schema_name in bindings:
        instance = bundle / relative
        if not instance.is_file():
            continue
        try:
            for finding in validate_json_file(instance, root / schema_name):
                _add(
                    issues,
                    "SCHEMA_INVALID",
                    relative,
                    f"{finding.json_path}: {finding.message}",
                )
        except (
            OSError,
            ValueError,
            json.JSONDecodeError,
            UnicodeError,
        ) as error:
            _add(issues, "SCHEMA_INVALID", relative, str(error))


def _check_measurement_controls(
    bundle: Path,
    repository: Path,
    issues: list[SourceAdmissionBundleIssue],
) -> dict[str, Any] | None:
    """Recompute frozen hashes and enforce the full measured-run schema."""

    relative = "measurement/measurement-controls.json"
    path = bundle / relative
    if not path.is_file():
        return None
    try:
        report = _load_json(path)
        configuration_path = repository / (
            "experiments/granite_turboquant_intel/configurations/"
            "workbook05/measurement-controls.json"
        )
        for finding in validate_measurement_controls(
            report,
            repository_root=repository,
            configuration_path=configuration_path,
        ):
            _add(issues, finding.code, relative, finding.message)

        configuration = _load_json(configuration_path)
        schema_relative = configuration["measured_run_schema_path"]
        schema = _load_json(repository / schema_relative)
        properties = schema.get("properties", {})
        quality = (
            properties.get("quality", {})
            if isinstance(properties, dict)
            else {}
        )
        top_required = set(schema.get("required", ()))
        quality_required = (
            set(quality.get("required", ()))
            if isinstance(quality, dict)
            else set()
        )
        if (
            schema.get("type") != "object"
            or schema.get("additionalProperties") is not False
            or not EXPECTED_MEASURED_RUN_TOP_LEVEL.issubset(top_required)
            or not EXPECTED_QUALITY_FIELDS.issubset(quality_required)
        ):
            _add(
                issues,
                "MEASURED_RUN_SCHEMA_PERMISSIVE",
                str(schema_relative),
                (
                    "The measured-run schema must remain closed and require "
                    "the complete performance and quality contract."
                ),
            )
        return report
    except (
        OSError,
        KeyError,
        TypeError,
        ValueError,
        json.JSONDecodeError,
        UnicodeError,
    ) as error:
        _add(issues, "MEASUREMENT_CONTROL_INVALID", relative, str(error))
        return None


def _check_provenance(
    bundle: Path,
    issues: list[SourceAdmissionBundleIssue],
) -> None:
    """Verify pins, clean trees, submodules, and recorded commands."""

    for relative, expected in PINNED_SOURCE_REPORTS.items():
        report_path = bundle / relative
        if not report_path.is_file():
            continue
        try:
            report = _load_json(report_path)
        except (
            OSError,
            ValueError,
            json.JSONDecodeError,
            UnicodeError,
        ) as error:
            _add(issues, "SOURCE_PROVENANCE_MISMATCH", relative, str(error))
            continue

        for field, expected_value in expected.items():
            if report.get(field) != expected_value:
                _add(
                    issues,
                    "SOURCE_PROVENANCE_MISMATCH",
                    relative,
                    (
                        f"{field} must be {expected_value!r}; "
                        f"found {report.get(field)!r}."
                    ),
                )
        if report.get("actual_origin_url") != expected["expected_origin_url"]:
            _add(
                issues,
                "SOURCE_PROVENANCE_MISMATCH",
                relative,
                "Actual origin does not match the pinned origin.",
            )
        if report.get("actual_commit") != expected["expected_commit"]:
            _add(
                issues,
                "SOURCE_PROVENANCE_MISMATCH",
                relative,
                "Actual commit does not match the pinned commit.",
            )
        if report.get("working_tree_clean") is not True:
            _add(
                issues,
                "SOURCE_PROVENANCE_MISMATCH",
                relative,
                "The verified source tree is not clean.",
            )

        submodules = report.get("submodules")
        incomplete = (
            report.get("submodules_complete") is not True
            or not isinstance(submodules, list)
        )
        if isinstance(submodules, list):
            incomplete = incomplete or any(
                not isinstance(item, dict) or item.get("status") != "clean"
                for item in submodules
            )
        if incomplete:
            _add(
                issues,
                "SUBMODULE_INCOMPLETE",
                relative,
                "Recursive submodule evidence is incomplete or not clean.",
            )

        command_records = report.get("command_records", ())
        if isinstance(command_records, list):
            for command_record in command_records:
                if not _safe(command_record):
                    candidates: tuple[Path, ...] = ()
                else:
                    # Accept the current route-local R2 convention and the
                    # earlier explicit bundle-root-qualified fixture form.
                    candidates = (
                        report_path.parent / str(command_record),
                        bundle / str(command_record),
                    )
                if not any(candidate.is_file() for candidate in candidates):
                    _add(
                        issues,
                        "SOURCE_COMMAND_RECORD_MISSING",
                        relative,
                        f"Missing or unsafe command record: {command_record!r}",
                    )


def _load_route_records(
    bundle: Path,
    issues: list[SourceAdmissionBundleIssue],
) -> tuple[dict[str, Any] | None, dict[str, Any] | None]:
    """Load both route records and independently reject false admission."""

    records: list[dict[str, Any] | None] = []
    for relative in (
        "controls/route-a-source-admission.json",
        "controls/route-b-source-admission.json",
    ):
        path = bundle / relative
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
                _add(
                    issues,
                    "FALSE_ADMISSION",
                    relative,
                    "; ".join(decision.reasons),
                )
            for proof in record.get("proofs", ()):
                if (
                    not isinstance(proof, dict)
                    or proof.get("status") != "Passed"
                ):
                    continue
                evidence = proof.get("evidence_path")
                if (
                    not _safe(evidence)
                    or not (bundle / str(evidence)).is_file()
                ):
                    _add(
                        issues,
                        "FALSE_ADMISSION",
                        relative,
                        (
                            f"Passed proof {proof.get('proof_id')} has missing "
                            f"or unsafe evidence: {evidence!r}"
                        ),
                    )
            records.append(record)
        except (
            OSError,
            KeyError,
            TypeError,
            ValueError,
            json.JSONDecodeError,
            UnicodeError,
        ) as error:
            _add(issues, "FALSE_ADMISSION", relative, str(error))
            records.append(None)
    return records[0], records[1]


def _check_summary(
    bundle: Path,
    route_a: Mapping[str, Any] | None,
    route_b: Mapping[str, Any] | None,
    measurement: Mapping[str, Any] | None,
    issues: list[SourceAdmissionBundleIssue],
) -> dict[str, Any] | None:
    """Recalculate route and checkpoint decisions instead of trusting them."""

    relative = "summary/source-admission-summary.json"
    path = bundle / relative
    if not path.is_file():
        return None
    try:
        summary = _load_json(path)
        measurement_path = bundle / "measurement/measurement-controls.json"
        if (
            measurement_path.is_file()
            and summary.get("measurement_controls_sha256")
            != _sha256(measurement_path)
        ):
            _add(
                issues,
                "CONTROL_HASH_MISMATCH",
                relative,
                "Summary measurement-control hash does not match the file.",
            )
        if route_a is None or route_b is None or measurement is None:
            return summary

        phase = calculate_phase_decision(route_a, route_b, measurement)
        expected = {
            ROUTE_A_ID: phase.route_a_status,
            ROUTE_B_ID: phase.route_b_status,
        }
        decisions = summary.get("route_decisions")
        if not isinstance(decisions, dict):
            raise ValueError("Summary route_decisions must be an object.")
        for route_id, expected_status in expected.items():
            route_summary = decisions.get(route_id)
            actual = (
                route_summary.get("status")
                if isinstance(route_summary, dict)
                else None
            )
            if actual != expected_status:
                _add(
                    issues,
                    "DECISION_MISMATCH",
                    relative,
                    (
                        f"{route_id} must be {expected_status}; "
                        f"found {actual!r}."
                    ),
                )
            if isinstance(route_summary, dict):
                for evidence in route_summary.get("evidence_paths", ()):
                    if (
                        not _safe(evidence)
                        or not (bundle / str(evidence)).is_file()
                    ):
                        _add(
                            issues,
                            "UNSAFE_EVIDENCE_PATH",
                            relative,
                            f"Missing or unsafe route evidence: {evidence!r}",
                        )
        if summary.get("checkpoint_status") != phase.checkpoint_status:
            _add(
                issues,
                "DECISION_MISMATCH",
                relative,
                (
                    "Checkpoint status does not match recalculated Phase 1 "
                    f"status {phase.checkpoint_status}."
                ),
            )
        return summary
    except (
        OSError,
        KeyError,
        TypeError,
        ValueError,
        json.JSONDecodeError,
        UnicodeError,
    ) as error:
        _add(issues, "DECISION_MISMATCH", relative, str(error))
        return None


def _check_route_a_configure(
    bundle: Path,
    route_a: Mapping[str, Any] | None,
    summary: Mapping[str, Any] | None,
    issues: list[SourceAdmissionBundleIssue],
) -> None:
    """Require truthful CPU-only generation evidence for Route A."""

    summary_status = None
    if isinstance(summary, Mapping):
        decisions = summary.get("route_decisions")
        route = decisions.get(ROUTE_A_ID) if isinstance(decisions, dict) else None
        summary_status = route.get("status") if isinstance(route, dict) else None
    admitted = (
        route_a is not None
        and route_a.get("admission_status") == "Admitted"
    ) or summary_status == "Admitted"
    if not admitted:
        return

    relative = "routes/route-a/configure-probe.json"
    report_path = bundle / relative
    if not report_path.is_file():
        _add(
            issues,
            "ROUTE_A_CONFIGURE_INVALID",
            relative,
            "Route A is admitted without a configure-probe report.",
        )
        return
    try:
        report = _load_json(report_path)
        cache_relative = report.get("cmake_cache_path")
        if not _safe(cache_relative):
            raise ValueError(f"Unsafe CMake cache path: {cache_relative!r}")
        cache = report_path.parent / str(cache_relative)
        if not cache.is_file():
            raise ValueError(f"CMake cache is missing: {cache_relative}")
        if _sha256(cache) != report.get("cmake_cache_sha256"):
            raise ValueError("CMake cache SHA-256 does not match the report.")

        decision = evaluate_route_a_configure_probe(
            report.get("command", ()),
            int(report.get("exit_code", -1)),
            cache.read_text(encoding="utf-8-sig", errors="replace"),
        )
        reasons = list(decision.reasons)
        if report.get("status") != "Passed":
            reasons.append("Configure report status is not Passed.")
        for flag in ("build_invoked", "install_invoked", "package_invoked"):
            if report.get(flag) is not False:
                reasons.append(f"{flag} must remain false.")
        if report.get("cache_values") != dict(decision.cache_values):
            reasons.append("Reported cache values do not match the CMake cache.")
        for field in ("stdout_path", "stderr_path"):
            evidence = report.get(field)
            if (
                not _safe(evidence)
                or not (report_path.parent / str(evidence)).is_file()
            ):
                reasons.append(f"{field} is missing or unsafe: {evidence!r}.")
        for reason in reasons:
            _add(issues, "ROUTE_A_CONFIGURE_INVALID", relative, reason)
    except (
        OSError,
        TypeError,
        ValueError,
        json.JSONDecodeError,
        UnicodeError,
    ) as error:
        _add(issues, "ROUTE_A_CONFIGURE_INVALID", relative, str(error))


def _check_route_b_blocker(
    bundle: Path,
    route_b: Mapping[str, Any] | None,
    summary: Mapping[str, Any] | None,
    issues: list[SourceAdmissionBundleIssue],
) -> None:
    """Forbid Route B admission while RB-SRC-001 is confirmed."""

    relative = "routes/route-b/cmake-test-discovery.json"
    path = bundle / relative
    if not path.is_file():
        return
    try:
        audit = _load_json(path)
    except (
        OSError,
        ValueError,
        json.JSONDecodeError,
        UnicodeError,
    ):
        return
    if audit.get("status") != "Confirmed":
        return
    record_admitted = (
        route_b is not None
        and route_b.get("admission_status") == "Admitted"
    )
    summary_admitted = False
    if isinstance(summary, Mapping):
        decisions = summary.get("route_decisions")
        route = decisions.get(ROUTE_B_ID) if isinstance(decisions, dict) else None
        summary_admitted = (
            isinstance(route, dict) and route.get("status") == "Admitted"
        )
    if record_admitted or summary_admitted:
        _add(
            issues,
            "ROUTE_B_BLOCKER_CONFLICT",
            relative,
            "Route B cannot be admitted while RB-SRC-001 is confirmed.",
        )


def _walk_paths(
    value: Any,
    location: str = "$",
) -> list[tuple[str, str]]:
    """Find evidence paths without treating machine paths as bundle files."""

    found: list[tuple[str, str]] = []
    if isinstance(value, dict):
        for key, child in value.items():
            child_location = f"{location}.{key}"
            lowered = key.casefold()
            if (
                lowered not in {"canonical_path", "file_path"}
                and lowered.endswith("_path")
                and isinstance(child, str)
                and child
            ):
                found.append((child_location, child))
            elif lowered.endswith("_paths") and isinstance(child, list):
                found.extend(
                    (f"{child_location}[{index}]", item)
                    for index, item in enumerate(child)
                    if isinstance(item, str) and item
                )
            found.extend(_walk_paths(child, child_location))
    elif isinstance(value, list):
        for index, child in enumerate(value):
            found.extend(_walk_paths(child, f"{location}[{index}]"))
    return found


def _check_payloads_and_paths(
    bundle: Path,
    issues: list[SourceAdmissionBundleIssue],
) -> None:
    """Reject forbidden payload types, secrets, and traversal references."""

    for candidate in sorted(
        path for path in bundle.rglob("*") if path.is_file()
    ):
        relative = candidate.relative_to(bundle).as_posix()
        suffix = candidate.suffix.casefold()
        if suffix in FORBIDDEN_SUFFIXES:
            _add(
                issues,
                "FORBIDDEN_PAYLOAD",
                relative,
                f"Forbidden source-admission payload suffix: {suffix}",
            )
            continue
        try:
            text = candidate.read_text(encoding="utf-8-sig")
        except (OSError, UnicodeDecodeError):
            text = ""
        for pattern in SECRET_PATTERNS:
            if pattern in text:
                _add(
                    issues,
                    "SECRET_PATTERN",
                    relative,
                    f"Matched forbidden secret pattern: {pattern}",
                )
        if suffix != ".json" or not text:
            continue
        try:
            value = json.loads(text)
        except json.JSONDecodeError:
            continue
        for location, evidence in _walk_paths(value):
            if not _safe(evidence):
                _add(
                    issues,
                    "UNSAFE_EVIDENCE_PATH",
                    relative,
                    f"{location}: {evidence}",
                )


def _ordered(
    issues: Iterable[SourceAdmissionBundleIssue],
) -> list[SourceAdmissionBundleIssue]:
    """Deduplicate and sort findings for deterministic reports."""

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

    bundle = bundle_root.resolve()
    repository = repository_root.resolve()
    issues: list[SourceAdmissionBundleIssue] = []
    _check_required_and_hashes(bundle, issues)
    _check_schemas(bundle, repository, issues)
    measurement = _check_measurement_controls(bundle, repository, issues)
    _check_provenance(bundle, issues)
    route_a, route_b = _load_route_records(bundle, issues)
    summary = _check_summary(bundle, route_a, route_b, measurement, issues)
    _check_route_a_configure(bundle, route_a, summary, issues)
    _check_route_b_blocker(bundle, route_b, summary, issues)
    _check_payloads_and_paths(bundle, issues)
    return _ordered(issues)


def render_validation_report(
    issues: Iterable[SourceAdmissionBundleIssue],
) -> str:
    """Always render a deterministic Markdown report."""

    ordered = _ordered(issues)
    lines = ["# Workbook 05 source-admission artifact validation", ""]
    if not ordered:
        lines.extend(
            [
                "Validation passed.",
                "",
                (
                    "Hashes, schemas, pinned provenance, recursive submodules, "
                    "route decisions, Route A configure evidence, Route B "
                    "blocker boundaries, measurement controls, paths, secrets, "
                    "and payload suffixes are valid."
                ),
            ]
        )
    else:
        lines.extend(
            [f"Validation failed with {len(ordered)} issue(s).", ""]
        )
        lines.extend(
            f"- `{issue.code}` `{issue.path}` — {issue.message}"
            for issue in ordered
        )
    return "\n".join(lines) + "\n"


def main(argv: Iterable[str] | None = None) -> int:
    """Validate one bundle, write the report, and return a truthful code."""

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
    arguments = parser.parse_args(
        list(argv) if argv is not None else None
    )
    try:
        issues = validate_source_admission_bundle(
            arguments.bundle_root,
            arguments.repository_root,
        )
    except (
        OSError,
        KeyError,
        TypeError,
        ValueError,
        json.JSONDecodeError,
        UnicodeError,
    ) as error:
        issues = [SourceAdmissionBundleIssue("VALIDATOR_ERROR", ".", str(error))]
    report = render_validation_report(issues)
    arguments.report.parent.mkdir(parents=True, exist_ok=True)
    arguments.report.write_text(report, encoding="utf-8", newline="\n")
    print(report, end="")
    return 1 if issues else 0


if __name__ == "__main__":
    raise SystemExit(main())
