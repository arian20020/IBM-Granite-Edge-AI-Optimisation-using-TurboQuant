import unittest
from pathlib import Path

from scripts.testing.campaigns.openvino.reconcile import validate_workbook_text

ROOT = Path(__file__).resolve().parents[3]
WORKBOOK = ROOT / "docs/testing/workbooks/text-templates/04_Official_OpenVINO_Controlled_Retest_Workbook_v1.md"


class OfficialOpenVINOReconcileTests(unittest.TestCase):
    def test_workbook_presents_only_the_three_accepted_runtime_rows(self):
        text = WORKBOOK.read_text(encoding="utf-8")
        self.assertNotIn("Overall status | Terminal-complete", text)
        lines = text.splitlines()
        header = (
            "| Test ID | Context | Load ms | TTFT ms | Prompt tok/s | "
            "TPOT ms | Decode tok/s | Generation ms |"
        )
        start = lines.index(header) + 2
        presented = set()
        for line in lines[start:]:
            if not line.startswith("|"):
                break
            cells = [cell.strip() for cell in line.strip("|").split("|")]
            presented.add((cells[0], int(cells[1])))
        self.assertEqual(
            presented,
            {
                ("OV-TQ-13", 512),
                ("OV-TQ-14", 512),
                ("OV-TQ-14", 2048),
            },
        )

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
        text = WORKBOOK.read_text(encoding="utf-8")
        required = (
            "Load ms", "TTFT ms", "Prompt tok/s", "TPOT ms", "Decode tok/s",
            "Generation ms", "Peak WS MiB", "Peak private MiB",
            "Available RAM min MiB", "KV MiB", "Cleanup",
            "CPU mean/median/peak %", "GPU mean/median/peak %", "CPU samples",
            "GPU samples", "Accepted runs", "Fallback count", "Evidence ref",
        )
        for field in required:
            self.assertIn(field, text)


if __name__ == "__main__":
    unittest.main()
