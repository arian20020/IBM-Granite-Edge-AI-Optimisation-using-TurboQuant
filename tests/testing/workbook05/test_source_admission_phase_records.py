from __future__ import annotations

import json
import unittest
from pathlib import Path

from scripts.testing.workbook05.source_admission_phase import assemble_phase_outputs


ROOT = Path(__file__).resolve().parents[3]
CAMPAIGN_ROOT = (
    ROOT
    / "experiments/granite_turboquant_intel/manifests/campaigns/GTQ-WB05-MF-v1"
)
VALID_SHA256 = "0" * 64

ROUTE_A_PHASE_1_PROOFS = {
    "RA-P01-document-capture",
    "RA-P02-runtime-source-tree",
    "RA-P03-genai-source-tree",
    "RA-P04-runtime-submodules",
    "RA-P05-genai-submodules",
    "RA-P06-source-capabilities",
    "RA-P07-toolchain",
    "RA-P08-configure-probe",
    "RA-P09-generated-metadata",
}
ROUTE_B_PHASE_1_PROOFS = {
    "RB-P01-document-capture",
    "RB-P02-runtime-source-tree",
    "RB-P03-runtime-submodules",
    "RB-P04-qjl-source-paths",
    "RB-P05-polar-source-paths",
    "RB-P06-encode-decode-source-paths",
    "RB-P07-independent-kv-source-paths",
    "RB-P08-test-discovery-audit",
    "RB-P09-generated-test-metadata",
}
DEFERRED_PROOF_TERMS = {
    "documented-build",
    "standard-cache-baseline",
    "activation",
    "packed-allocation",
    "packed-size-conformance",
    "no-silent-fallback",
}


def _load_record(filename: str) -> dict[str, object]:
    """Load one controlled route record exactly as the future orchestrator will."""

    return json.loads((CAMPAIGN_ROOT / filename).read_text(encoding="utf-8"))


def _measurement_report() -> dict[str, object]:
    """Return valid measurement controls for summary-reason testing."""

    return {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "prompt_set_id": "GTQ-PROMPTS-v1",
        "rubric_id": "GTQ-QUALITY-RUBRIC-v1",
        "prompt_count": 6,
        "prompt_ids": ["P1", "P2", "P3", "P4", "P5", "P6"],
        "rubric_weight_total": 1.0,
        "required_metric_names": ["TTFT"],
        "controls": [
            {"path": f"controls/control-{index}.json", "sha256": VALID_SHA256}
            for index in range(6)
        ],
        "raw_output_required": True,
        "activation_proof_required": True,
        "fallback_result_required": True,
        "separate_k_v_allocation_required": True,
        "weight_and_kv_axes_separate": True,
    }


def _checkpoint_template() -> dict[str, object]:
    """Return the minimum checkpoint structure required by the pure assembler."""

    return {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "generation": 0,
        "phase_order": ["phase-1-source-admission"],
        "steps": [
            {
                "step_id": "phase-1-source-admission",
                "status": "Not started",
                "evidence_sha256": "",
            }
        ],
        "updated_at_utc": "2026-08-05T00:00:00Z",
    }


def _completed_route(
    route_id: str,
    status: str,
    decision_reason: str,
    *,
    proof_status: str = "Passed",
) -> dict[str, object]:
    """Create one completed route input for summary-reason testing."""

    return {
        "route_id": route_id,
        "admission_status": status,
        "decision_reason": decision_reason,
        "proofs": [
            {
                "proof_id": f"{route_id}-proof",
                "required": True,
                "status": proof_status,
                "evidence_path": f"routes/{route_id}/proof.json",
            }
        ],
        "known_blockers": [],
    }


class SourceAdmissionPhaseRecordTests(unittest.TestCase):
    def test_controlled_route_records_contain_only_phase_1_proofs(self) -> None:
        route_a = _load_record("route-a-source-admission.json")
        route_b = _load_record("route-b-source-admission.json")

        route_a_ids = {proof["proof_id"] for proof in route_a["proofs"]}
        route_b_ids = {proof["proof_id"] for proof in route_b["proofs"]}

        self.assertEqual(ROUTE_A_PHASE_1_PROOFS, route_a_ids)
        self.assertEqual(ROUTE_B_PHASE_1_PROOFS, route_b_ids)
        for proof_id in route_a_ids | route_b_ids:
            self.assertFalse(
                any(term in proof_id for term in DEFERRED_PROOF_TERMS),
                f"Deferred runtime/build proof leaked into Phase 1: {proof_id}",
            )

    def test_summary_explains_a_calculated_block_instead_of_repeating_stale_text(self) -> None:
        route_a = _completed_route(
            "route-a-merged-openvino",
            "Admitted",
            "The route was expected to pass.",
            proof_status="Failed",
        )
        route_b = _completed_route(
            "route-b-experimental-qjl-polar",
            "Blocked",
            "The experimental route is blocked for a recorded source reason.",
        )

        outputs = assemble_phase_outputs(
            route_a,
            route_b,
            _measurement_report(),
            checkpoint_template=_checkpoint_template(),
            measurement_controls_path="measurement/measurement-controls.json",
            measurement_controls_sha256="2" * 64,
            summary_json_path="summary/source-admission-summary.json",
            summary_markdown_path="summary/source-admission-summary.md",
            checkpoint_path="checkpoint/checkpoint.json",
            hash_manifest_path="hash-manifest.sha256",
        )

        route_a_summary = outputs.summary["route_decisions"][
            "route-a-merged-openvino"
        ]
        self.assertEqual("Blocked", route_a_summary["status"])
        self.assertIn("required proof", route_a_summary["decision_reason"])
        self.assertNotEqual("The route was expected to pass.", route_a_summary["decision_reason"])


if __name__ == "__main__":
    unittest.main()
