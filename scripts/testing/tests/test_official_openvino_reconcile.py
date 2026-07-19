import unittest
from pathlib import Path

from scripts.testing.official_openvino.reconcile import validate_workbook_text


class OfficialOpenVINOReconcileTests(unittest.TestCase):
    def test_rejects_blank_and_bare_na_table_cells(self):
        with self.assertRaisesRegex(ValueError, "blank table cell"):
            validate_workbook_text("| Field | Value |\n| --- | --- |\n| x |  |", {"x"})
        with self.assertRaisesRegex(ValueError, "bare N/A"):
            validate_workbook_text("| Field | Value |\n| --- | --- |\n| x | N/A |", {"x"})

    def test_requires_every_controlled_id(self):
        with self.assertRaisesRegex(ValueError, "missing controlled ID"):
            validate_workbook_text("| ID | Status |\n| --- | --- |\n| OV-01 | terminal |", {"OV-01", "OV-02"})

    def test_complete_terminal_fixture_is_accepted(self):
        text = "| ID | Status | Evidence |\n| --- | --- | --- |\n| OV-01 | not-measured: gate | evidence.json |"
        self.assertTrue(validate_workbook_text(text, {"OV-01"})["accepted"])

    def test_workbook_exposes_every_required_performance_field(self):
        root = Path(__file__).resolve().parents[3]
        text = (root / "docs/testing/workbooks/text-templates/04_Official_OpenVINO_Controlled_Retest_Workbook_v1.md").read_text(encoding="utf-8")
        required = (
            "Load ms", "TTFT ms", "Prompt tok/s", "TPOT ms", "Decode tok/s",
            "Generation duration ms", "Peak working set MB", "Peak private MB",
            "Available RAM min MB", "KV MB", "GPU memory peak MB", "CPU mean %",
            "CPU median %", "CPU peak %", "CPU sample count", "GPU mean %",
            "GPU median %", "GPU peak %", "GPU sample count",
        )
        for field in required:
            self.assertIn(field, text)


if __name__ == "__main__":
    unittest.main()
