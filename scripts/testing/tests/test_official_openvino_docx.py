import tempfile
import unittest
from pathlib import Path

from docx import Document

from scripts.testing.official_openvino.docx_audit import audit_docx


class OfficialOpenVINODocxAuditTests(unittest.TestCase):
    def test_generated_wb04_passes_control_audit(self):
        root = Path(__file__).resolve().parents[3]
        result = audit_docx(
            root / "docs/testing/workbooks/generated/04_Official_OpenVINO_Controlled_Retest_Workbook_v1.docx",
            root / "docs/testing/workbooks/Controlled-Workbook-Manifest.csv",
            {"OV-B01", "OV-C01", "OV-01", "OV-TQS-01", "OV-TQ-01", "P1"},
        )
        self.assertTrue(result["accepted"])
        self.assertEqual(result["blank_table_cells"], 0)
        self.assertEqual(result["visible_revision"], "1.6")

    def test_blank_table_cell_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "bad.docx"
            document = Document()
            document.add_paragraph("v1.4 Revision history OV-01")
            table = document.add_table(rows=1, cols=1)
            table.cell(0, 0).text = ""
            document.save(path)
            with self.assertRaisesRegex(ValueError, "blank DOCX table cells"):
                audit_docx(path, Path("unused.csv"), {"OV-01"})


if __name__ == "__main__":
    unittest.main()
