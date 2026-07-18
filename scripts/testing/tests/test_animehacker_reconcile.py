import unittest
import json
import tempfile
from pathlib import Path


class AnimehackerReconcileTests(unittest.TestCase):
    def test_rejects_blank_and_literal_na_cells(self):
        from scripts.testing.reconcile_animehacker_workbook import validate_markdown

        with self.assertRaisesRegex(ValueError, "blank workbook"):
            validate_markdown("| Field |  |")
        with self.assertRaisesRegex(ValueError, "literal N/A"):
            validate_markdown("| Field | N/A |")

    def test_requires_every_controlled_id(self):
        from scripts.testing.reconcile_animehacker_workbook import validate_markdown

        with self.assertRaisesRegex(ValueError, "missing test id"):
            validate_markdown("| Field | Filled |")

    def test_recovered_ah09_requires_three_samples_and_six_quality_records(self):
        from scripts.testing.reconcile_animehacker_workbook import validate_recovery

        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            runtime = root / "runtime"; quality = root / "quality"
            (runtime / "AH-09").mkdir(parents=True)
            (quality / "AH-09").mkdir(parents=True)
            (runtime / "AH-09" / "summary.json").write_text(
                json.dumps({"samples": [{"valid": True}] * 2}), encoding="utf-8")
            adjudication = root / "quality-adjudications.json"
            adjudication.write_text(json.dumps({"AH-09": {"prompts": {}}}), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "AH-09 does not have three valid samples"):
                validate_recovery(runtime, quality, adjudication)

    def test_ah10_accepts_sourced_schema_v2_memory_gate_without_quality(self):
        from scripts.testing.reconcile_animehacker_workbook import validate_recovery

        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            runtime = root / "runtime"; quality = root / "quality"
            (runtime / "AH-09").mkdir(parents=True)
            (runtime / "AH-10").mkdir(parents=True)
            (quality / "AH-09").mkdir(parents=True)
            (runtime / "AH-09" / "summary.json").write_text(
                json.dumps({"samples": [{"valid": True}] * 3}), encoding="utf-8")
            prompts = {}
            for number in range(1, 7):
                prompt = f"P{number}"; text = f"response {number}"
                (quality / "AH-09" / f"{prompt}-response.txt").write_text(text, encoding="utf-8")
                import hashlib
                digest = hashlib.sha256(text.encode()).hexdigest()
                (quality / "AH-09" / f"{prompt}.json").write_text(
                    json.dumps({"output": text, "output_sha256": digest}), encoding="utf-8")
                prompts[prompt] = {}
            adjudication = root / "quality-adjudications.json"
            adjudication.write_text(json.dumps({"AH-09": {"prompts": prompts}}), encoding="utf-8")
            (runtime / "AH-10" / "wrapper-execution.json").write_text(
                json.dumps({"schema_version": 2, "evidence_kind": "wrapper-execution", "controller_exit_code": 1}), encoding="utf-8")
            (runtime / "AH-10" / "preflight.json").write_text(
                json.dumps({"schema_version": 2, "evidence_kind": "preflight", "minimum_available_ram_mib": 2048}), encoding="utf-8")
            result = validate_recovery(runtime, quality, adjudication)
            self.assertEqual(result["AH-10"]["status"], "safety-classified")


if __name__ == "__main__":
    unittest.main()
