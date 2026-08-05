"""Calculate and assemble truthful Workbook 05 Phase 1 decisions."""

from __future__ import annotations

from copy import deepcopy
from dataclasses import dataclass
from typing import Any, Mapping

from scripts.testing.workbook05.measurement_controls import (
    validate_measurement_controls,
)
from scripts.testing.workbook05.source_admission import (
    AdmissionDecision,
    evaluate_source_admission,
    is_safe_relative_evidence_path,
    source_admission_integrity_issues,
)


CAMPAIGN_ID = "GTQ-WB05-MF-v1"
PHASE_ID = "phase-1-source-admission"
ROUTE_A_ID = "route-a-merged-openvino"
ROUTE_B_ID = "route-b-experimental-qjl-polar"


@dataclass(frozen=True)
class PhaseDecision:
    """Evidence-backed Route A, Route B, and Phase 1 checkpoint states."""

    route_a_status: str
    route_b_status: str
    checkpoint_status: str
    reasons: tuple[str, ...]


@dataclass(frozen=True)
class PhaseOutputs:
    """Deterministic data products consumed by the later orchestrator."""

    decision: PhaseDecision
    summary: dict[str, Any]
    summary_markdown: str
    checkpoint_candidate: dict[str, Any]
    hash_manifest_inputs: tuple[str, ...]


def _append_route_reasons(
    reasons: list[str],
    route_name: str,
    decision: AdmissionDecision,
) -> None:
    """Add one route's state and evidence problems to the phase explanation."""

    reasons.append(f"{route_name} calculated status is {decision.calculated_status}.")
    reasons.extend(f"{route_name}: {reason}." for reason in decision.reasons)


def calculate_phase_decision(
    route_a_record: Mapping[str, Any],
    route_b_record: Mapping[str, Any],
    measurement_report: Mapping[str, Any],
) -> PhaseDecision:
    """Calculate route states without hiding evidence-integrity failures."""

    # Measurement controls govern every later benchmark. Phase 1 cannot pass
    # when those controls are incomplete or weakened.
    measurement_issues = validate_measurement_controls(measurement_report)

    # Record-integrity failures are evaluated separately from scientific source
    # blockers. A truthful Route B blocker is allowed; corrupt Route B evidence
    # is not an acceptable basis for passing the phase.
    route_a_integrity = source_admission_integrity_issues(route_a_record)
    route_b_integrity = source_admission_integrity_issues(route_b_record)

    # Each route is evaluated independently so experimental Route B evidence
    # can remain blocked while the merged Route A proceeds to documented build.
    route_a = evaluate_source_admission(route_a_record)
    route_b = evaluate_source_admission(route_b_record)

    reasons: list[str] = []
    _append_route_reasons(reasons, "Route A", route_a)
    _append_route_reasons(reasons, "Route B", route_b)
    reasons.extend(
        f"Measurement controls {issue.code} at {issue.path}: {issue.message}"
        for issue in measurement_issues
    )

    # A truthful Route B blocker is an allowed scientific outcome. Evidence
    # integrity failures, invalid controls, or a blocked Route A stop the phase.
    if measurement_issues or route_a_integrity or route_b_integrity:
        checkpoint_status = "Blocked"
    elif (
        route_a.calculated_status == "Admitted"
        and route_b.calculated_status in {"Admitted", "Blocked"}
    ):
        checkpoint_status = "Passed"
    elif route_a.calculated_status == "Blocked":
        checkpoint_status = "Blocked"
    else:
        checkpoint_status = "Candidate"

    return PhaseDecision(
        route_a_status=route_a.calculated_status,
        route_b_status=route_b.calculated_status,
        checkpoint_status=checkpoint_status,
        reasons=tuple(reasons),
    )


def _require_safe_path(path: str, field_name: str) -> str:
    """Return a safe relative path or raise one precise assembly error."""

    if not is_safe_relative_evidence_path(path):
        raise ValueError(f"{field_name} must be a safe relative evidence path: {path!r}")
    return path


def _require_sha256(value: str, field_name: str) -> str:
    """Return a lowercase SHA-256 value or raise a precise assembly error."""

    if len(value) != 64 or any(character not in "0123456789abcdef" for character in value):
        raise ValueError(f"{field_name} must be a 64-character lowercase SHA-256")
    return value


def _route_evidence_paths(record: Mapping[str, Any]) -> tuple[str, ...]:
    """Collect stable, unique proof references from one controlled route record."""

    paths = {
        _require_safe_path(proof["evidence_path"], f"{proof['proof_id']}.evidence_path")
        for proof in record["proofs"]
        if proof.get("evidence_path")
    }
    if not paths:
        raise ValueError(f"Route {record.get('route_id')!r} has no evidence paths")
    return tuple(sorted(paths))


def _render_summary_markdown(summary: Mapping[str, Any]) -> str:
    """Render the machine summary into a deterministic reviewer-facing report."""

    lines = [
        "# Workbook 05 Source-Admission Summary",
        "",
        f"- Campaign: `{summary['campaign_id']}`",
        f"- Phase: `{summary['phase_id']}`",
        f"- Checkpoint: **{summary['checkpoint_status']}**",
        "",
    ]

    for route_name, route_id in (("Route A", ROUTE_A_ID), ("Route B", ROUTE_B_ID)):
        route = summary["route_decisions"][route_id]
        lines.extend(
            [
                f"## {route_name}",
                "",
                f"- Status: **{route['status']}**",
                f"- Decision: {route['decision_reason']}",
                "- Evidence:",
            ]
        )
        lines.extend(f"  - `{path}`" for path in route["evidence_paths"])
        lines.append("")

    lines.extend(["## Phase reasons", ""])
    lines.extend(f"- {reason}" for reason in summary["phase_reasons"])
    lines.append("")
    return "\n".join(lines)


def _build_checkpoint_candidate(
    checkpoint_template: Mapping[str, Any],
    checkpoint_status: str,
) -> dict[str, Any]:
    """Prepare a non-persisted checkpoint copy for later evidence hashing."""

    checkpoint = deepcopy(dict(checkpoint_template))
    matches = [
        step for step in checkpoint["steps"] if step["step_id"] == PHASE_ID
    ]
    if len(matches) != 1:
        raise ValueError(f"Checkpoint must contain exactly one {PHASE_ID!r} step")

    # The candidate deliberately leaves its evidence hash empty. The later
    # orchestrator fills it only after the completed evidence bundle is hashed.
    matches[0]["status"] = {
        "Passed": "Passed",
        "Blocked": "Blocked",
        "Candidate": "In progress",
    }[checkpoint_status]
    matches[0]["evidence_sha256"] = ""
    return checkpoint


def assemble_phase_outputs(
    route_a_record: Mapping[str, Any],
    route_b_record: Mapping[str, Any],
    measurement_report: Mapping[str, Any],
    *,
    checkpoint_template: Mapping[str, Any],
    measurement_controls_path: str,
    measurement_controls_sha256: str,
    summary_json_path: str,
    summary_markdown_path: str,
    checkpoint_path: str,
    hash_manifest_path: str,
) -> PhaseOutputs:
    """Assemble deterministic R1 outputs without writing files or changing Git."""

    decision = calculate_phase_decision(
        route_a_record,
        route_b_record,
        measurement_report,
    )

    # Validate every path before it becomes a trusted reference in the summary
    # or the future hash manifest.
    measurement_controls_path = _require_safe_path(
        measurement_controls_path,
        "measurement_controls_path",
    )
    summary_json_path = _require_safe_path(summary_json_path, "summary_json_path")
    summary_markdown_path = _require_safe_path(
        summary_markdown_path,
        "summary_markdown_path",
    )
    checkpoint_path = _require_safe_path(checkpoint_path, "checkpoint_path")
    hash_manifest_path = _require_safe_path(hash_manifest_path, "hash_manifest_path")
    measurement_controls_sha256 = _require_sha256(
        measurement_controls_sha256,
        "measurement_controls_sha256",
    )

    route_a_paths = _route_evidence_paths(route_a_record)
    route_b_paths = _route_evidence_paths(route_b_record)

    # The controlled record supplies the human explanation. Admission logic has
    # already blocked blank explanations before this summary is assembled.
    route_a_reason = str(route_a_record.get("decision_reason", "")).strip()
    route_b_reason = str(route_b_record.get("decision_reason", "")).strip()
    if not route_a_reason or not route_b_reason:
        raise ValueError("Each route requires a non-empty decision_reason")

    summary = {
        "schema_version": "1.0",
        "campaign_id": CAMPAIGN_ID,
        "phase_id": PHASE_ID,
        "measurement_controls_path": measurement_controls_path,
        "measurement_controls_sha256": measurement_controls_sha256,
        "route_decisions": {
            ROUTE_A_ID: {
                "status": decision.route_a_status,
                "decision_reason": route_a_reason,
                "evidence_paths": list(route_a_paths),
            },
            ROUTE_B_ID: {
                "status": decision.route_b_status,
                "decision_reason": route_b_reason,
                "evidence_paths": list(route_b_paths),
            },
        },
        "checkpoint_status": decision.checkpoint_status,
        "checkpoint_path": checkpoint_path,
        "hash_manifest_path": hash_manifest_path,
        "phase_reasons": list(decision.reasons),
    }

    checkpoint_candidate = _build_checkpoint_candidate(
        checkpoint_template,
        decision.checkpoint_status,
    )
    summary_markdown = _render_summary_markdown(summary)

    # The manifest itself is intentionally excluded because hash manifests must
    # not recursively hash their own changing contents.
    hash_manifest_inputs = tuple(
        sorted(
            {
                measurement_controls_path,
                *route_a_paths,
                *route_b_paths,
                summary_json_path,
                summary_markdown_path,
                checkpoint_path,
            }
        )
    )

    return PhaseOutputs(
        decision=decision,
        summary=summary,
        summary_markdown=summary_markdown,
        checkpoint_candidate=checkpoint_candidate,
        hash_manifest_inputs=hash_manifest_inputs,
    )
