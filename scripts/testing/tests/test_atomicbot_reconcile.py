import tempfile
import unittest
from pathlib import Path


class AtomicBotReconcileTests(unittest.TestCase):
    def test_every_required_field_needs_value_status_and_source(self):
        from scripts.testing.atomicbot.reconcile import reconcile_workbook

        with tempfile.TemporaryDirectory() as directory:
            template = Path(directory) / "workbook.md"
            template.write_text("# Workbook\n", encoding="utf-8")
            report = reconcile_workbook(template, {"required_fields": ["AB-01.ttft_ms"], "field_map": {
                "AB-01.ttft_ms": {"value": 123.4, "status": "Measured",
                                  "source_path": "raw/measurement.json", "source_key": "ttft_ms"}
            }})
            self.assertTrue(report.valid)
            self.assertEqual(report.checked_fields, 1)

    def test_blank_placeholder_and_sourceless_measurement_fail(self):
        from scripts.testing.atomicbot.reconcile import reconcile_workbook

        with tempfile.TemporaryDirectory() as directory:
            template = Path(directory) / "workbook.md"
            template.write_text("# Workbook\n", encoding="utf-8")
            for value in ("", "TBD", "TODO"):
                report = reconcile_workbook(template, {"required_fields": ["AB-01.ttft_ms"], "field_map": {
                    "AB-01.ttft_ms": {"value": value, "status": "Measured",
                                      "source_path": "", "source_key": "ttft_ms"}
                }})
                self.assertFalse(report.valid)

    def test_allowed_nonmeasurement_status_requires_reason_and_evidence(self):
        from scripts.testing.atomicbot.reconcile import reconcile_workbook

        with tempfile.TemporaryDirectory() as directory:
            template = Path(directory) / "workbook.md"
            template.write_text("# Workbook\n", encoding="utf-8")
            result = {"required_fields": ["AB-15M.gpu_memory"], "field_map": {
                "AB-15M.gpu_memory": {"value": "Blocked", "status": "Blocked",
                                      "reason": "memory gate", "source_path": "raw/gate.json",
                                      "source_key": "allowed"}
            }}
            self.assertTrue(reconcile_workbook(template, result).valid)


if __name__ == "__main__":
    unittest.main()
