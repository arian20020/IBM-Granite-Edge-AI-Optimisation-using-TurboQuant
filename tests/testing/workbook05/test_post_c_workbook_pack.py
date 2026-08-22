"""Acceptance tests for the Workbook 05 post-C controlled workbook pack.

These tests describe the complete workbook package before implementation. The
package is repository-only: it must be fully generatable and independently
valid without contacting the Lenovo runner or executing a model.
"""

from __future__ import annotations

import csv
import hashlib
import json
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from scripts.testing.workbook05.post_c_workbook_pack import (
    POST_C_WORKBOOKS,
    validate_post_c_workbook_pack,
)


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]


class PostCWorkbookPackTests(unittest.TestCase):
    """Require complete, deterministic and fail-closed post-C workbooks."""

    def test_complete_pack_validates(self) -> None:
        self.assertEqual([], validate_post_c_workbook_pack(REPOSITORY_ROOT))

    def test_all_seven_canonical_and_generated_workbooks_exist(self) -> None:
        for workbook in POST_C_WORKBOOKS:
            with self.subTest(workbook=workbook.workbook_id):
                self.assertTrue((REPOSITORY_ROOT / workbook.template_path).is_file())
                self.assertTrue((REPOSITORY_ROOT / workbook.generated_path).is_file())

    def test_controlled_manifest_registers_all_thirteen_workbooks(self) -> None:
        path = REPOSITORY_ROOT / "docs/testing/workbooks/Controlled-Workbook-Manifest.csv"
        with path.open(newline="", encoding="utf-8-sig") as handle:
            rows = list(csv.DictReader(handle))
        self.assertEqual(
            {f"WB-{number:02d}" for number in range(1, 14)},
            {row["Workbook_ID"] for row in rows},
        )
        for workbook in POST_C_WORKBOOKS:
            row = next(row for row in rows if row["Workbook_ID"] == workbook.workbook_id)
            self.assertEqual(workbook.template_path, row["Canonical_Text_Template"])
            self.assertEqual(Path(workbook.generated_path).name, row["Controlled_File"])

    def test_execution_index_covers_every_post_c_stage(self) -> None:
        path = REPOSITORY_ROOT / "docs/testing/Workbook-05-Post-C-Execution-Index-v1.csv"
        with path.open(newline="", encoding="utf-8-sig") as handle:
            rows = list(csv.DictReader(handle))
        self.assertEqual(
            {"D1", "D2", "E1", "E2", "E3", "E4", "F"},
            {row["Stage_ID"] for row in rows},
        )
        self.assertEqual(48, sum(row["Stage_ID"] == "D2" for row in rows))
        self.assertTrue(any(row["Model_Scale"] == "3B" for row in rows))
        self.assertTrue(any(row["Model_Scale"] == "8B" for row in rows))
        self.assertTrue(any(row["Model_Scale"] == "30B" for row in rows))

    def test_30b_rows_cannot_authorise_formal_statistics(self) -> None:
        path = REPOSITORY_ROOT / "docs/testing/Workbook-05-Post-C-Execution-Index-v1.csv"
        with path.open(newline="", encoding="utf-8-sig") as handle:
            rows = list(csv.DictReader(handle))
        e3_rows = [row for row in rows if row["Stage_ID"] == "E3"]
        self.assertGreater(len(e3_rows), 0)
        self.assertEqual({"false"}, {row["Formal_Statistics_Allowed"] for row in e3_rows})
        self.assertEqual({"true"}, {row["Discovery_Only"] for row in e3_rows})

    def test_closure_is_initialised_open_not_falsely_closed(self) -> None:
        path = REPOSITORY_ROOT / "docs/testing/workbooks/Workbook-05-Post-C-Pack-Manifest-v1.json"
        payload = json.loads(path.read_text(encoding="utf-8"))
        self.assertEqual("Initialised", payload["pack_status"])
        self.assertFalse(payload["scientific_results_authorised"])
        self.assertFalse(payload["workbook_closure_authorised"])
        self.assertEqual(7, len(payload["workbooks"]))

    def test_post_c_generation_is_byte_deterministic(self) -> None:
        generator = REPOSITORY_ROOT / "scripts/testing/Generate-Controlled-Workbooks.py"
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            first = root / "first"
            second = root / "second"
            for output in (first, second):
                completed = subprocess.run(
                    [
                        sys.executable,
                        str(generator),
                        "--repository-root",
                        str(REPOSITORY_ROOT),
                        "--output-directory",
                        str(output),
                        "--post-c-only",
                    ],
                    cwd=REPOSITORY_ROOT,
                    text=True,
                    capture_output=True,
                    timeout=180,
                    check=False,
                )
                self.assertEqual(0, completed.returncode, completed.stdout + completed.stderr)
            for workbook in POST_C_WORKBOOKS:
                filename = Path(workbook.generated_path).name
                self.assertEqual(
                    hashlib.sha256((first / filename).read_bytes()).hexdigest(),
                    hashlib.sha256((second / filename).read_bytes()).hexdigest(),
                    filename,
                )

    def test_hosted_workflow_has_producer_and_independent_validator(self) -> None:
        path = REPOSITORY_ROOT / ".github/workflows/workbook-05-post-c-workbooks.yml"
        text = path.read_text(encoding="utf-8")
        self.assertIn("produce-post-c-workbook-pack:", text)
        self.assertIn("validate-post-c-workbook-pack:", text)
        self.assertIn("needs: produce-post-c-workbook-pack", text)
        self.assertIn("runs-on: windows-latest", text)
        self.assertNotIn("self-hosted", text)
        self.assertIn("contents: read", text)
        self.assertIn("actions: read", text)
        self.assertIn("WORKBOOK05_POST_C_WORKBOOK_PACK_PASS", text)
        self.assertIn("workbook-05-post-c-pack-${{ github.run_id }}-${{ github.run_attempt }}", text)

    def test_unsafe_evidence_path_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            copy_root = Path(temporary_directory) / "repo"
            shutil.copytree(REPOSITORY_ROOT, copy_root)
            index_path = copy_root / "docs/testing/Workbook-05-Post-C-Execution-Index-v1.csv"
            with index_path.open(newline="", encoding="utf-8-sig") as handle:
                rows = list(csv.DictReader(handle))
            rows[0]["Evidence_Path"] = "../outside/evidence.json"
            with index_path.open("w", newline="", encoding="utf-8") as handle:
                writer = csv.DictWriter(handle, fieldnames=rows[0].keys(), lineterminator="\n")
                writer.writeheader()
                writer.writerows(rows)
            issues = validate_post_c_workbook_pack(copy_root)
            self.assertTrue(any(issue.code == "UNSAFE_EVIDENCE_PATH" for issue in issues))

    def test_manifest_hash_drift_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            copy_root = Path(temporary_directory) / "repo"
            shutil.copytree(REPOSITORY_ROOT, copy_root)
            manifest_path = copy_root / "docs/testing/workbooks/Controlled-Workbook-Manifest.csv"
            with manifest_path.open(newline="", encoding="utf-8-sig") as handle:
                rows = list(csv.DictReader(handle))
            target = next(row for row in rows if row["Workbook_ID"] == "WB-07")
            target["Canonical_Template_SHA256"] = "0" * 64
            with manifest_path.open("w", newline="", encoding="utf-8") as handle:
                writer = csv.DictWriter(handle, fieldnames=rows[0].keys(), lineterminator="\n")
                writer.writeheader()
                writer.writerows(rows)
            issues = validate_post_c_workbook_pack(copy_root)
            self.assertTrue(any(issue.code == "TEMPLATE_HASH_MISMATCH" for issue in issues))

    def test_duplicate_execution_identity_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            copy_root = Path(temporary_directory) / "repo"
            shutil.copytree(REPOSITORY_ROOT, copy_root)
            index_path = copy_root / "docs/testing/Workbook-05-Post-C-Execution-Index-v1.csv"
            with index_path.open(newline="", encoding="utf-8-sig") as handle:
                rows = list(csv.DictReader(handle))
            rows[1]["Execution_Record_ID"] = rows[0]["Execution_Record_ID"]
            with index_path.open("w", newline="", encoding="utf-8") as handle:
                writer = csv.DictWriter(handle, fieldnames=rows[0].keys(), lineterminator="\n")
                writer.writeheader()
                writer.writerows(rows)
            issues = validate_post_c_workbook_pack(copy_root)
            self.assertTrue(any(issue.code == "DUPLICATE_EXECUTION_RECORD" for issue in issues))


if __name__ == "__main__":
    unittest.main()
