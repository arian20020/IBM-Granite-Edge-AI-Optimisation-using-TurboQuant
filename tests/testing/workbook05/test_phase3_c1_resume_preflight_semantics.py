from __future__ import annotations

import json
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory
from unittest.mock import patch

from scripts.testing.workbook05.phase3.c1_resume import ValidationResult
from scripts.testing.workbook05.phase3.c1_resume_bundle_validation import (
    validate_c1_resume_bundle,
)
from tests.testing.workbook05.test_phase3_c1_resume_bundle_validation_hosted import (
    MODEL_SHA256,
    REPOSITORY_ROOT,
    REVISION,
    SOURCE_DIRECTORY,
    TOKENIZER_SHA256,
    _create_resume_bundle,
    _refresh_manifest,
)
from tests.testing.workbook05.test_phase3_live_asset_bundle_validation import (
    _write_json,
)


CONVERSION_MINIMUM_FREE_BYTES = 20 * 1024 * 1024 * 1024
RESOURCE_MINIMUM_AVAILABLE_BYTES = 4 * 1024 * 1024 * 1024
RESOURCE_MAXIMUM_COMMIT_PERCENT = 70


class Phase3C1ResumePreflightSemanticTests(unittest.TestCase):
    """Require Passed preflights to agree with their measured observations."""

    def _issues(self, mutate) -> list:
        with TemporaryDirectory() as directory:
            bundle = Path(directory) / "bundle"
            bundle.mkdir()
            decision_digest = _create_resume_bundle(bundle)

            # Materialise the exact reviewed conversion-only disk controls that
            # the production orchestrator writes before applying the mutation.
            disk_path = bundle / "disk-preflight.json"
            disk = json.loads(disk_path.read_text(encoding="utf-8"))
            disk.update(
                {
                    "free_bytes": CONVERSION_MINIMUM_FREE_BYTES,
                    "minimum_free_bytes_for_conversion_resume": (
                        CONVERSION_MINIMUM_FREE_BYTES
                    ),
                    "source_download_required": False,
                    "source_download_authorised": False,
                    "deletion_authorised": False,
                    "deletion_performed": False,
                    "prior_failed_workspace_preserved": True,
                    "prior_partial_conversion_preserved": True,
                }
            )
            _write_json(disk_path, disk)

            mutate(bundle)
            _refresh_manifest(bundle)
            with (
                patch(
                    "scripts.testing.workbook05.phase3.c1_resume_bundle_validation."
                    "EXPECTED_REVISION",
                    REVISION,
                ),
                patch(
                    "scripts.testing.workbook05.phase3.c1_resume_bundle_validation."
                    "EXPECTED_SOURCE_DIRECTORY",
                    SOURCE_DIRECTORY,
                ),
                patch(
                    "scripts.testing.workbook05.phase3.c1_resume_bundle_validation."
                    "EXPECTED_MODEL_SHA256",
                    MODEL_SHA256,
                ),
                patch(
                    "scripts.testing.workbook05.phase3.c1_resume_bundle_validation."
                    "EXPECTED_TOKENIZER_SHA256",
                    TOKENIZER_SHA256,
                ),
                patch(
                    "scripts.testing.workbook05.phase3.c1_resume_bundle_validation."
                    "EXPECTED_DEPENDENCY_DECISION_SHA256",
                    decision_digest,
                ),
                patch(
                    "scripts.testing.workbook05.phase3.c1_resume_bundle_validation."
                    "validate_prior_failure_bundle",
                    return_value=ValidationResult(valid=True, issues=()),
                ),
            ):
                return validate_c1_resume_bundle(bundle, REPOSITORY_ROOT)

    def test_resource_pass_rejects_insufficient_available_memory(self) -> None:
        def mutate(bundle: Path) -> None:
            path = bundle / "resource-preflight.json"
            record = json.loads(path.read_text(encoding="utf-8"))
            record["available_memory_bytes"] = (
                RESOURCE_MINIMUM_AVAILABLE_BYTES - 1
            )
            _write_json(path, record)

        self.assertIn(
            "RESOURCE_PREFLIGHT",
            {issue.code for issue in self._issues(mutate)},
        )

    def test_resource_pass_rejects_high_commit_and_conflicting_processes(self) -> None:
        def mutate(bundle: Path) -> None:
            path = bundle / "resource-preflight.json"
            record = json.loads(path.read_text(encoding="utf-8"))
            record["commit_percent"] = RESOURCE_MAXIMUM_COMMIT_PERCENT + 1
            record["conflicting_processes"] = ["devenv"]
            _write_json(path, record)

        self.assertIn(
            "RESOURCE_PREFLIGHT",
            {issue.code for issue in self._issues(mutate)},
        )

    def test_resource_threshold_drift_is_rejected(self) -> None:
        def mutate(bundle: Path) -> None:
            path = bundle / "resource-preflight.json"
            record = json.loads(path.read_text(encoding="utf-8"))
            record["minimum_available_memory_bytes"] = 1
            record["maximum_commit_percent"] = 99
            _write_json(path, record)

        self.assertIn(
            "RESOURCE_PREFLIGHT",
            {issue.code for issue in self._issues(mutate)},
        )

    def test_disk_pass_rejects_insufficient_free_space(self) -> None:
        def mutate(bundle: Path) -> None:
            path = bundle / "disk-preflight.json"
            record = json.loads(path.read_text(encoding="utf-8"))
            record["free_bytes"] = CONVERSION_MINIMUM_FREE_BYTES - 1
            _write_json(path, record)

        self.assertIn(
            "DISK_PREFLIGHT",
            {issue.code for issue in self._issues(mutate)},
        )

    def test_disk_threshold_and_preservation_drift_are_rejected(self) -> None:
        def mutate(bundle: Path) -> None:
            path = bundle / "disk-preflight.json"
            record = json.loads(path.read_text(encoding="utf-8"))
            record["minimum_free_bytes_for_conversion_resume"] = 1
            record["prior_partial_conversion_preserved"] = False
            _write_json(path, record)

        codes = {issue.code for issue in self._issues(mutate)}
        self.assertIn("DISK_PREFLIGHT", codes)
        self.assertIn("PRESERVATION", codes)

    def test_stage_order_requires_the_controlled_resume_operation(self) -> None:
        def mutate(bundle: Path) -> None:
            path = bundle / "stage-order.json"
            record = json.loads(path.read_text(encoding="utf-8"))
            record["operation"] = "fresh-live-asset-lock"
            _write_json(path, record)

        self.assertIn(
            "STAGE_ORDER_RELATIONSHIP",
            {issue.code for issue in self._issues(mutate)},
        )


if __name__ == "__main__":
    unittest.main()
