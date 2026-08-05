"""Calculate the truthful Workbook 05 Phase 1 route and checkpoint states."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Mapping

from scripts.testing.workbook05.measurement_controls import (
    validate_measurement_controls,
)
from scripts.testing.workbook05.source_admission import evaluate_source_admission


@dataclass(frozen=True)
class PhaseDecision:
    """Evidence-backed Route A, Route B, and Phase 1 checkpoint states."""

    route_a_status: str
    route_b_status: str
    checkpoint_status: str
    reasons: tuple[str, ...]


def calculate_phase_decision(
    route_a_record: Mapping[str, Any],
    route_b_record: Mapping[str, Any],
    measurement_report: Mapping[str, Any],
) -> PhaseDecision:
    """Allow Phase 1 to pass when Route A proceeds and Route B is truthful."""

    measurement_issues = validate_measurement_controls(measurement_report)
    route_a = evaluate_source_admission(route_a_record)
    route_b = evaluate_source_admission(route_b_record)

    reasons: list[str] = []
    reasons.append(f"Route A calculated status is {route_a.calculated_status}.")
    reasons.append(f"Route B calculated status is {route_b.calculated_status}.")

    checkpoint_status = "Blocked"
    if (
        not measurement_issues
        and route_a.calculated_status == "Admitted"
        and route_b.calculated_status in {"Admitted", "Blocked"}
    ):
        checkpoint_status = "Passed"

    return PhaseDecision(
        route_a_status=route_a.calculated_status,
        route_b_status=route_b.calculated_status,
        checkpoint_status=checkpoint_status,
        reasons=tuple(reasons),
    )
