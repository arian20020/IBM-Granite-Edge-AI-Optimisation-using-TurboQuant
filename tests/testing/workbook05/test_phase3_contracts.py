from __future__ import annotations

import copy
import json
import unittest
from pathlib import Path

from scripts.testing.workbook05.phase3.contracts import (
    assert_phase3_record,
    validate_phase3_record,
)

REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
TEMPLATE_ROOT = (
    REPOSITORY_ROOT
    / "experiments"
    / "granite_turboquant_intel"
    / "manifests"
    / "templates"
    / "workbook05"
)
SCHEMA_ROOT = (
    REPOSITORY_ROOT
    / "experiments"
    / "granite_turboquant_intel"
    / "schemas"
    / "workbook05"
)


def load_template(filename: str) -> dict[str, object]:
    return json.loads((TEMPLATE_ROOT / filename).read_text(encoding="utf-8"))


class Phase3ContractTests(unittest.TestCase):
    def test_c1_templates_validate(self) -> None:
        bindings = {
            "prerequisite-proof": "phase3-prerequisite-proof-template.json",
            "model-asset-lock": "model-asset-lock-template.json",
            "model-conversion-record": "model-conversion-record-template.json",
        }

        for record_type, filename in bindings.items():
            with self.subTest(record_type=record_type):
                self.assertEqual(
                    [],
                    validate_phase3_record(
                        record_type,
                        load_template(filename),
                        REPOSITORY_ROOT,
                    ),
                )

    def test_asset_template_keeps_unobserved_file_sets_empty(self) -> None:
        payload = load_template("model-asset-lock-template.json")

        self.assertEqual([], payload["source_files"])
        self.assertEqual([], payload["tokenizer_files"])
        self.assertEqual([], payload["converted_files"])
        self.assertFalse(
            payload["declared_model_metadata"]["observed_runtime_capability"]  # type: ignore[index]
        )

    def test_asset_lock_rejects_moving_revision(self) -> None:
        payload = load_template("model-asset-lock-template.json")
        payload["source"]["resolved_revision"] = "main"  # type: ignore[index]

        issues = validate_phase3_record(
            "model-asset-lock",
            payload,
            REPOSITORY_ROOT,
        )

        self.assertTrue(
            any(issue.json_path == "$.source.resolved_revision" for issue in issues)
        )

    def test_asset_lock_rejects_uppercase_sha256(self) -> None:
        payload = load_template("model-asset-lock-template.json")
        payload["aggregate_model_sha256"] = "A" * 64

        issues = validate_phase3_record(
            "model-asset-lock",
            payload,
            REPOSITORY_ROOT,
        )

        self.assertTrue(
            any(issue.json_path == "$.aggregate_model_sha256" for issue in issues)
        )

    def test_prerequisite_proof_cannot_authorise_later_claims(self) -> None:
        payload = load_template("phase3-prerequisite-proof-template.json")
        payload["performance_claim_authorised"] = True

        issues = validate_phase3_record(
            "prerequisite-proof",
            payload,
            REPOSITORY_ROOT,
        )

        self.assertTrue(
            any(issue.json_path == "$.performance_claim_authorised" for issue in issues)
        )

    def test_conversion_record_requires_remote_code_to_remain_disabled(self) -> None:
        payload = load_template("model-conversion-record-template.json")
        payload["conversion"]["trust_remote_code"] = True  # type: ignore[index]

        issues = validate_phase3_record(
            "model-conversion-record",
            payload,
            REPOSITORY_ROOT,
        )

        self.assertTrue(
            any(issue.json_path == "$.conversion.trust_remote_code" for issue in issues)
        )

    def test_unknown_record_type_is_rejected(self) -> None:
        with self.assertRaisesRegex(
            ValueError,
            "Unsupported Phase 3 record type: unknown",
        ):
            validate_phase3_record("unknown", {}, REPOSITORY_ROOT)

    def test_closed_schema_rejects_an_unknown_top_level_field(self) -> None:
        payload = load_template("model-asset-lock-template.json")
        payload["unexpected"] = "not allowed"

        issues = validate_phase3_record(
            "model-asset-lock",
            payload,
            REPOSITORY_ROOT,
        )

        self.assertTrue(any(issue.json_path == "$" for issue in issues))

    def test_assert_phase3_record_reports_all_problems(self) -> None:
        payload = load_template("model-asset-lock-template.json")
        payload["aggregate_model_sha256"] = "bad"
        payload["aggregate_tokenizer_sha256"] = "also-bad"

        with self.assertRaises(ValueError) as raised:
            assert_phase3_record("model-asset-lock", payload, REPOSITORY_ROOT)

        message = str(raised.exception)
        self.assertIn("$.aggregate_model_sha256", message)
        self.assertIn("$.aggregate_tokenizer_sha256", message)

    def test_validation_issue_order_is_stable(self) -> None:
        payload = load_template("model-asset-lock-template.json")
        payload["aggregate_model_sha256"] = "bad"
        payload["aggregate_tokenizer_sha256"] = "also-bad"

        first = validate_phase3_record("model-asset-lock", payload, REPOSITORY_ROOT)
        second = validate_phase3_record(
            "model-asset-lock",
            copy.deepcopy(payload),
            REPOSITORY_ROOT,
        )

        self.assertEqual(first, second)
        self.assertEqual(
            sorted(issue.json_path for issue in first),
            [issue.json_path for issue in first],
        )

    def test_every_c1_schema_is_closed_at_the_top_level(self) -> None:
        for filename in (
            "phase3-prerequisite-proof.schema.json",
            "model-asset-lock.schema.json",
            "model-conversion-record.schema.json",
        ):
            with self.subTest(filename=filename):
                schema = json.loads((SCHEMA_ROOT / filename).read_text(encoding="utf-8"))
                self.assertFalse(schema["additionalProperties"])
                self.assertEqual("object", schema["type"])


if __name__ == "__main__":
    unittest.main()
