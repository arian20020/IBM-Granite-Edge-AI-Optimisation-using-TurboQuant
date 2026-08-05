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


def evaluate_source_admission(record: Mapping[str, Any]) -> AdmissionDecision:
    """Permit admission only when every required source proof is trustworthy."""

    reasons: list[str] = []
    blocking_failure = False

    # A decision without an explanation is not independently reviewable.
    decision_reason = record.get("decision_reason")
    if not isinstance(decision_reason, str) or not decision_reason.strip():
        reasons.append("decision_reason must be a non-empty string")
        blocking_failure = True

    # Required proofs may be pending while a route remains a Candidate. A failed
    # proof, or a Passed proof without safe evidence, blocks the route.
    for proof in record["proofs"]:
        proof_id = proof["proof_id"]
        proof_status = proof["status"]
        if proof["required"] and proof_status != "Passed":
            reasons.append(f"required proof {proof_id} is {proof_status}")
            if proof_status in {"Failed", "Blocked"}:
                blocking_failure = True

        if proof_status == "Passed":
            evidence_path = proof["evidence_path"]
            if not evidence_path:
                reasons.append(f"passed proof {proof_id} has no evidence_path")
                blocking_failure = True
            elif not is_safe_relative_evidence_path(evidence_path):
                reasons.append(
                    f"passed proof {proof_id} has unsafe evidence_path: {evidence_path}"
                )
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
