import csv
import hashlib
import subprocess
import tempfile
import unittest
import json
from pathlib import Path

from docx import Document

from scripts.testing import audit_official_openvino_docx as audit_wrapper
from scripts.testing.official_openvino.docx_audit import audit_docx


class OfficialOpenVINODocxAuditTests(unittest.TestCase):
    def make_auditable_fixture(self, directory: str):
        root = Path(__file__).resolve().parents[3]
        matrix = root / "experiments/manifests/official-openvino/retest-matrix.json"
        docx = Path(directory) / "04_Official_OpenVINO_Controlled_Retest_Workbook_v1.docx"
        document = Document()
        document.add_paragraph("04 Official OpenVINO Controlled Retest Workbook v1.7")
        document.add_paragraph("Document revision history")
        document.add_paragraph(
            " ".join(case.test_id for case in audit_wrapper.load_matrix(matrix))
        )
        for index in range(22):
            table = document.add_table(rows=1, cols=1)
            table.cell(0, 0).text = "Version" if index == 0 else f"table-{index}"
        document.save(docx)
        manifest = Path(directory) / "Controlled-Workbook-Manifest.csv"
        with manifest.open("w", newline="", encoding="utf-8") as handle:
            writer = csv.DictWriter(
                handle,
                fieldnames=["Workbook_ID", "Revision", "Last_Validated_DOCX_SHA256"],
            )
            writer.writeheader()
            writer.writerow(
                {
                    "Workbook_ID": "WB-04",
                    "Revision": "1.7",
                    "Last_Validated_DOCX_SHA256": hashlib.sha256(docx.read_bytes()).hexdigest(),
                }
            )
        return docx, manifest, matrix

    def test_v17_fixture_passes_control_audit(self):
        with tempfile.TemporaryDirectory() as directory:
            docx, manifest, matrix = self.make_auditable_fixture(directory)
            result = audit_docx(
                docx,
                manifest,
                {case.test_id for case in audit_wrapper.load_matrix(matrix)},
            )
        self.assertTrue(result["accepted"])
        self.assertEqual(result["blank_table_cells"], 0)
        self.assertEqual(result["visible_revision"], "1.7")
        self.assertEqual(result["table_count"], 22)

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

    def test_wrapper_accepts_custom_paths_and_atomically_writes_report(self):
        with tempfile.TemporaryDirectory() as directory:
            docx, manifest, matrix = self.make_auditable_fixture(directory)
            output = Path(directory) / "release" / "audit.json"
            result = audit_wrapper.main(
                [
                    "--docx", str(docx),
                    "--manifest", str(manifest),
                    "--matrix", str(matrix),
                    "--output", str(output),
                ]
            )
            self.assertEqual(result, 0)
            report = json.loads(output.read_text(encoding="utf-8"))
            self.assertTrue(report["accepted"])
            self.assertFalse(list(output.parent.glob(".audit.json.*.tmp")))

    def test_wrapper_optional_release_checks_refuse_current_v16_shape(self):
        with tempfile.TemporaryDirectory() as directory:
            docx, manifest, matrix = self.make_auditable_fixture(directory)
            output = Path(directory) / "audit.json"
            result = audit_wrapper.main(
                [
                    "--docx", str(docx),
                    "--manifest", str(manifest),
                    "--matrix", str(matrix),
                    "--output", str(output),
                    "--expected-visible-revision", "1.8",
                    "--expected-table-count", "22",
                ]
            )
            self.assertEqual(result, 1)
            self.assertFalse(output.exists())

    def test_wrapper_optional_table_count_check_refuses_wrong_count(self):
        with tempfile.TemporaryDirectory() as directory:
            docx, manifest, matrix = self.make_auditable_fixture(directory)
            output = Path(directory) / "audit.json"
            result = audit_wrapper.main(
                [
                    "--docx", str(docx),
                    "--manifest", str(manifest),
                    "--matrix", str(matrix),
                    "--output", str(output),
                    "--expected-visible-revision", "1.7",
                    "--expected-table-count", "21",
                ]
            )
            self.assertEqual(result, 1)
            self.assertFalse(output.exists())

    def test_release_mode_refuses_incompatible_output_override(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "audit.json"
            result = audit_wrapper.main(["--release", "--output", str(output)])
            self.assertEqual(result, 1)
            self.assertFalse(output.exists())

    def test_release_mode_refuses_incompatible_expected_revision_override(self):
        result = audit_wrapper.main(
            ["--release", "--expected-visible-revision", "1.6"]
        )
        self.assertEqual(result, 1)

    def test_release_mode_selects_the_complete_controlled_profile(self):
        configuration = audit_wrapper.resolve_audit_configuration(
            audit_wrapper.parse_args(["--release"])
        )
        self.assertEqual(
            configuration,
            (
                audit_wrapper.DEFAULT_DOCX,
                audit_wrapper.DEFAULT_MANIFEST,
                audit_wrapper.DEFAULT_MATRIX,
                audit_wrapper.RELEASE_OUTPUT,
                "1.7",
                22,
            ),
        )
        self.assertEqual(
            audit_wrapper._sha256(audit_wrapper.DEFAULT_MATRIX),
            audit_wrapper.FROZEN_MATRIX_SHA256,
        )

    def test_release_mode_structural_qa_output_is_not_git_ignored(self):
        repository = Path(__file__).resolve().parents[3]
        ignored = subprocess.run(
            [
                "git",
                "check-ignore",
                "--quiet",
                "--no-index",
                str(audit_wrapper.RELEASE_OUTPUT),
            ],
            cwd=repository,
            check=False,
        )

        self.assertEqual(
            ignored.returncode,
            1,
            f"release QA output is ignored: {audit_wrapper.RELEASE_OUTPUT}",
        )


if __name__ == "__main__":
    unittest.main()
