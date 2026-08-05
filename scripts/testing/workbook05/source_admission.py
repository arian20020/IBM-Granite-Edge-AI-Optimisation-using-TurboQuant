"""Apply the non-negotiable source-admission gate for Workbook 05."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Mapping


@dataclass(frozen=True)
class AdmissionDecision:
    """Calculated source-admission state and its evidence-backed reasons."""

    permitted: bool
    calculated_status: str
    reasons: tuple[str, ...]


def is_safe_relative_evidence_path(value: object) -> bool:
    """Return True only for a portable, traversal-free relative evidence path."""

    # Evidence paths are stored in repository-style POSIX form even on Windows.
    # Reject absolute, drive-qualified, UNC-like, backslash, and empty segments.
    if not isinstance(value, str) or not value:
        return False
    if value.startswith("/") or value.startswith("//") or "\\" in value or ":" in value:
        return False

    segments = value.split("/")
    return all(segment not in {"", ".", ".."} for segment in segments)


def source_admission_integrity_issues(
    record: Mapping[str, Any],
) -> tuple[str, ...]:
    """Return record-integrity failures separately from scientific blockers."""

    issues: list[str] = []

    # Every outcome needs a reviewable explanation, including a blocked outcome.
    decision_reason = record.get("decision_reason")
    if not isinstance(decision_reason, str) or not decision_reason.strip():
        issues.append("decision_reason must be a non-empty string")

    # Only Passed proofs claim that evidence exists. Their references must be
    # present and safe before any phase can rely on the route's calculated state.
    for proof in record["proofs"]:
        if proof["status"] != "Passed":
            continue

        proof_id = proof["proof_id"]
        evidence_path = proof["evidence_path"]
        if not evidence_path:
            issues.append(f"passed proof {proof_id} has no evidence_path")
        elif not is_safe_relative_evidence_path(evidence_path):
            issues.append(
                f"passed proof {proof_id} has unsafe evidence_path: {evidence_path}"
            )

    return tuple(issues)


def evaluate_source_admission(record: Mapping[str, Any]) -> AdmissionDecision:
    """Permit admission only when every required source proof is trustworthy."""

    # Keep evidence-integrity failures explicit so the phase decision can
    # distinguish them from an honest experimental-route source blocker.
    integrity_issues = source_admission_integrity_issues(record)
    reasons: list[str] = list(integrity_issues)
    blocking_failure = bool(integrity_issues)

    # Required proofs may be pending while a route remains a Candidate. A failed
    # or blocked required proof is a route-level blocking outcome.
    for proof in record["proofs"]:
        proof_id = proof["proof_id"]
        proof_status = proof["status"]
        if proof["required"] and proof_status != "Passed":
            reasons.append(f"required proof {proof_id} is {proof_status}")
            if proof_status in {"Failed", "Blocked"}:
                blocking_failure = True

    # Open and accepted-risk blockers both prevent source admission. A risk can
    # be accepted for planning, but it cannot be re-labelled as passed evidence.
    for blocker in record["known_blockers"]:
        blocker_status = blocker["status"]
        if blocker_status != "Resolved":
            reasons.append(
                f"{blocker_status} blocker {blocker['blocker_id']}: "
                f"{blocker['description']}"
            )
            blocking_failure = True

    requested_status = record["admission_status"]
    if not reasons and requested_status == "Admitted":
        return AdmissionDecision(True, "Admitted", ())
    if requested_status == "Blocked" or blocking_failure:
        return AdmissionDecision(False, "Blocked", tuple(reasons))
    return AdmissionDecision(False, "Candidate", tuple(reasons))
