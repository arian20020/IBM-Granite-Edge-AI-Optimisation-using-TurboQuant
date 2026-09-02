from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from scripts.testing.workbook05.schema_validation import (
    assert_valid_json_file,
    validate_json_file,
)


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
SCHEMA_ROOT = REPOSITORY_ROOT / "experiments/granite_turboquant_intel/schemas/workbook05"


class SchemaValidationTests(unittest.TestCase):
    def test_valid_source_admission_template_passes(self) -> None:
        instance = REPOSITORY_ROOT / "experiments/granite_turboquant_intel/manifests/templates/workbook05/source-admission-template.json"
        issues = validate_json_file(instance, SCHEMA_ROOT / "source-admission.schema.json")
        self.assertEqual([], issues)

    def test_unknown_route_is_reported_with_a_json_path(self) -> None:
        template_path = REPOSITORY_ROOT / "experiments/granite_turboquant_intel/manifests/templates/workbook05/source-admission-template.json"
        record = json.loads(template_path.read_text(encoding="utf-8"))
        record["route_id"] = "route-c-unknown"

        with tempfile.TemporaryDirectory() as temporary_directory:
            instance = Path(temporary_directory) / "invalid.json"
            instance.write_text(json.dumps(record), encoding="utf-8")
            issues = validate_json_file(instance, SCHEMA_ROOT / "source-admission.schema.json")

        self.assertEqual(1, len(issues))
        self.assertEqual("$.route_id", issues[0].json_path)
        self.assertIn("is not one of", issues[0].message)

    def test_assert_valid_raises_one_readable_exception(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            instance = Path(temporary_directory) / "invalid.json"
            instance.write_text("{}", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "campaign_id"):
                assert_valid_json_file(instance, SCHEMA_ROOT / "campaign-manifest.schema.json")


if __name__ == "__main__":
    unittest.main()
