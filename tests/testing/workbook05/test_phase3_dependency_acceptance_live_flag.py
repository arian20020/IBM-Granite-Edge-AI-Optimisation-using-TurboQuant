"""Regression tests for the accepted dependency observation live-mode flag.

The accepted dependency artifact records ``simulation_mode`` explicitly as
``false``.  Live C1 must accept that exact value and must reject both a simulated
record and an observation that omits the field entirely.
"""

from __future__ import annotations

import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from typing import Final

from scripts.testing.workbook05.phase3 import dependency_acceptance as acceptance


_CLAIM_FLAGS: Final[tuple[str, ...]] = (
    "model_download_authorised",
    "granite_model_test_authorised",
    "activation_claim_authorised",
    "packed_storage_claim_authorised",
    "performance_claim_authorised",
    "quality_claim_authorised",
)
_MISSING = object()


class Phase3DependencyAcceptanceLiveFlagTests(unittest.TestCase):
    """Require one explicit Boolean distinction between live and simulated evidence."""

    def _create_evidence(
        self,
        root: Path,
        *,
        simulation_mode: object = _MISSING,
    ) -> tuple[Path, dict[str, object]]:
        """Create the smallest retained decision/observation pair for the check."""

        evidence = root / "evidence"
        evidence.mkdir()

        decision: dict[str, object] = {
            "schema_version": "1.0",
            "campaign_id": "GTQ-WB05-MF-v1",
            "record_type": "dependency-preflight-decision",
            "route_id": "route-a-merged-openvino",
            "status": "Passed",
        }
        decision.update({key: False for key in _CLAIM_FLAGS})
        decision_bytes = (
            json.dumps(decision, indent=2, sort_keys=True) + "\n"
        ).encode("utf-8")
        (evidence / "decision.json").write_bytes(decision_bytes)

        observation: dict[str, object] = {
            "workspace_root": str(root),
        }
        if simulation_mode is not _MISSING:
            observation["simulation_mode"] = simulation_mode
        (evidence / "observation.json").write_text(
            json.dumps(observation, indent=2) + "\n",
            encoding="utf-8",
        )

        record: dict[str, object] = {
            "decision_path": "decision.json",
            "decision_sha256": hashlib.sha256(decision_bytes).hexdigest(),
            "workspace_root": str(root),
        }
        return evidence, record

    def test_explicit_false_is_accepted_as_live_evidence(self) -> None:
        """The exact Boolean used by the accepted Lenovo artifact must pass."""

        with tempfile.TemporaryDirectory() as directory:
            evidence, record = self._create_evidence(
                Path(directory),
                simulation_mode=False,
            )

            _, observation = acceptance._verify_decision_and_observation(
                evidence,
                record,
            )

        self.assertIs(observation["simulation_mode"], False)

    def test_true_is_rejected_as_simulated_evidence(self) -> None:
        """A repository simulation must never inherit the accepted live identity."""

        with tempfile.TemporaryDirectory() as directory:
            evidence, record = self._create_evidence(
                Path(directory),
                simulation_mode=True,
            )

            with self.assertRaisesRegex(ValueError, "must be a live record"):
                acceptance._verify_decision_and_observation(evidence, record)

    def test_missing_live_flag_is_rejected_fail_closed(self) -> None:
        """Omitting the field must not be interpreted as proof of live execution."""

        with tempfile.TemporaryDirectory() as directory:
            evidence, record = self._create_evidence(Path(directory))

            with self.assertRaisesRegex(ValueError, "must be a live record"):
                acceptance._verify_decision_and_observation(evidence, record)


if __name__ == "__main__":
    unittest.main()
