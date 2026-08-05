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


def evaluate_source_admission(record: Mapping[str, Any]) -> AdmissionDecision:
    """Permit admission only when every required proof passes and blockers close."""

    reasons: list[str] = []
    for proof in record["proofs"]:
        if proof["required"] and proof["status"] != "Passed":
            reasons.append(f"required proof {proof['proof_id']} is {proof['status']}")
        if proof["status"] == "Passed" and not proof["evidence_path"]:
            reasons.append(f"passed proof {proof['proof_id']} has no evidence_path")

    for blocker in record["known_blockers"]:
        if blocker["status"] != "Resolved":
            reasons.append(f"open blocker {blocker['blocker_id']}: {blocker['description']}")

    requested_status = record["admission_status"]
    if not reasons and requested_status == "Admitted":
        return AdmissionDecision(True, "Admitted", ())
    if requested_status == "Blocked":
        return AdmissionDecision(False, "Blocked", tuple(reasons))
    return AdmissionDecision(False, "Candidate", tuple(reasons))
