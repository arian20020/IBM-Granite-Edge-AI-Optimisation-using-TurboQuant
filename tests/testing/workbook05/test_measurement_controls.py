from __future__ import annotations

import copy
import json
import tempfile
import unittest
from pathlib import Path

from scripts.testing.workbook05.measurement_controls import (
    capture_measurement_controls,
    validate_measurement_controls,
)
from scripts.testing.workbook05.schema_validation import validate_json_file


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
CONFIGURATION = (
    REPOSITORY_ROOT
    / "experiments/granite_turboquant_intel/configurations/workbook05/measurement-controls.json"
)
SCHEMA = (
    REPOSITORY_ROOT
    / "experiments/granite_turboquant_intel/schemas/workbook05/measured-run-manifest.schema.json"
)
TEMPLATE = (
    REPOSITORY_ROOT
    / "experiments/granite_turboquant_intel/manifests/templates/workbook05/measured-run-manifest-template.json"
)


class MeasurementControlTests(unittest.TestCase):
    def test_measured_run_schema_requires_quality_and_separate_kv_metrics(self) -> None:
        schema = json.loads(SCHEMA.read_text(encoding="utf-8"))
        required_resources = schema["properties"]["resources"]["required"]
        required_quality = schema["properties"]["quality"]["required"]

        self.assertIn("k_cache_allocated_bytes", required_resources)
        self.assertIn("v_cache_allocated_bytes", required_resources)
        for field in (
            "raw_output_sha256",
            "deterministic_failures",
            "dimension_scores",
            "critical_caps",
            "matched_baseline_run_id",
            "paired_score_delta",
            "material_degradation",
        ):
            self.assertIn(field, required_quality)

    def test_controlling_assets_are_hashed_and_valid(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            destination = Path(directory) / "measurement-controls.json"
            report = capture_measurement_controls(
                REPOSITORY_ROOT,
                CONFIGURATION,
                destination,
            )
            issues = validate_measurement_controls(
                report,
                repository_root=REPOSITORY_ROOT,
                configuration_path=CONFIGURATION,
            )

        self.assertEqual([], issues)
        self.assertEqual("GTQ-PROMPTS-v1", report["prompt_set_id"])
        self.assertEqual("GTQ-QUALITY-RUBRIC-v1", report["rubric_id"])
        self.assertAlmostEqual(1.0, report["rubric_weight_total"])
        self.assertEqual(6, report["prompt_count"])
        self.assertTrue(report["raw_output_required"])
        self.assertTrue(report["activation_proof_required"])
        self.assertTrue(report["fallback_result_required"])

    def test_valid_measured_run_template_passes_its_schema(self) -> None:
        self.assertEqual([], validate_json_file(TEMPLATE, SCHEMA))

    def test_passed_run_cannot_drop_raw_output_or_quality_score(self) -> None:
        instance = json.loads(TEMPLATE.read_text(encoding="utf-8"))
        instance["quality"]["raw_output_path"] = ""
        instance["quality"]["score_0_to_10"] = None

        with tempfile.TemporaryDirectory() as directory:
            instance_path = Path(directory) / "run.json"
            instance_path.write_text(json.dumps(instance), encoding="utf-8")
            issues = validate_json_file(instance_path, SCHEMA)

        self.assertGreaterEqual(len(issues), 2)

    def test_missing_prompt_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            report = capture_measurement_controls(
                REPOSITORY_ROOT,
                CONFIGURATION,
                Path(directory) / "measurement-controls.json",
            )
        changed = copy.deepcopy(report)
        changed["prompt_ids"] = changed["prompt_ids"][:-1]
        changed["prompt_count"] = 5

        issues = validate_measurement_controls(changed)

        self.assertTrue(
            any(issue.code == "PROMPT_SET_INCOMPLETE" for issue in issues)
        )


if __name__ == "__main__":
    unittest.main()
