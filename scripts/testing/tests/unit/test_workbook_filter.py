import importlib.util
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch


ROOT = Path(__file__).resolve().parents[4]


def load_script(module_name: str, filename: str):
    spec = importlib.util.spec_from_file_location(
        module_name,
        ROOT / "scripts" / "testing" / "tools" / filename,
    )
    module = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module


class WorkbookFilterTests(unittest.TestCase):
    def test_generator_without_filter_keeps_all_templates(self):
        generator = load_script("generate_controlled_workbooks", "Generate-Controlled-Workbooks.py")
        self.assertEqual(
            generator.select_templates(None),
            generator.WORKBOOK_TEMPLATES,
        )

    def test_generator_selects_only_wb04_template(self):
        generator = load_script("generate_controlled_workbooks", "Generate-Controlled-Workbooks.py")
        self.assertEqual(
            generator.select_templates("WB-04"),
            ("04_Official_OpenVINO_Controlled_Retest_Workbook_v1.md",),
        )

    def test_generator_rejects_unknown_workbook_id(self):
        generator = load_script("generate_controlled_workbooks", "Generate-Controlled-Workbooks.py")
        with self.assertRaisesRegex(ValueError, "Unknown workbook ID"):
            generator.select_templates("WB-99")

    def test_generator_cli_filter_calls_only_wb04_conversion(self):
        generator = load_script("generate_controlled_workbooks", "Generate-Controlled-Workbooks.py")
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            templates = root / "docs/testing/workbooks/text-templates"
            templates.mkdir(parents=True)
            wb04 = templates / "04_Official_OpenVINO_Controlled_Retest_Workbook_v1.md"
            wb04.write_text("# WB-04\n", encoding="utf-8")
            with patch.object(generator, "convert_markdown_to_docx") as convert:
                result = generator.main(
                    [
                        "--repository-root", str(root),
                        "--output-directory", str(root / "out"),
                        "--workbook-id", "WB-04",
                    ]
                )
            self.assertEqual(result, 0)
            convert.assert_called_once_with(
                wb04,
                root / "out" / "04_Official_OpenVINO_Controlled_Retest_Workbook_v1.docx",
            )

    def test_history_without_filter_keeps_all_workbooks(self):
        history = load_script("apply_workbook_revision_history", "Apply-Workbook-Revision-History.py")
        self.assertEqual(
            history.select_workbooks(None),
            history.WORKBOOKS,
        )

    def test_history_selects_only_wb04_file(self):
        history = load_script("apply_workbook_revision_history", "Apply-Workbook-Revision-History.py")
        self.assertEqual(
            history.select_workbooks("WB-04"),
            {"04_Official_OpenVINO_Controlled_Retest_Workbook_v1.docx": "WB-04"},
        )

    def test_history_rejects_unknown_workbook_id(self):
        history = load_script("apply_workbook_revision_history", "Apply-Workbook-Revision-History.py")
        with self.assertRaisesRegex(ValueError, "Unknown workbook ID"):
            history.select_workbooks("WB-99")


if __name__ == "__main__":
    unittest.main()
