from __future__ import annotations

import unittest

from scripts.testing.workbook05.source_admission_bundle_validation import (
    _walk_paths,
)


class SourceAdmissionCommandPathClassificationTests(unittest.TestCase):
    """Keep executable observations separate from bundle evidence references."""

    def test_absolute_command_file_path_is_not_a_bundle_evidence_reference(
        self,
    ) -> None:
        """A pinned executable may be absolute without weakening evidence paths."""

        command_record = {
            "command_id": "source-admission-route-a-runtime-verification",
            "file_path": r"C:\Program Files\Python312\python.exe",
            "argv": ["-m", "scripts.testing.workbook05.example"],
            "working_directory": r"C:\actions-runner\_work\repository",
            "stdout_path": (
                "source-admission-route-a-runtime-verification.stdout.txt"
            ),
            "stderr_path": (
                "source-admission-route-a-runtime-verification.stderr.txt"
            ),
        }

        found = dict(_walk_paths(command_record))

        self.assertNotIn("$.file_path", found)
        self.assertEqual(
            "source-admission-route-a-runtime-verification.stdout.txt",
            found["$.stdout_path"],
        )
        self.assertEqual(
            "source-admission-route-a-runtime-verification.stderr.txt",
            found["$.stderr_path"],
        )

    def test_other_path_fields_remain_subject_to_evidence_path_validation(
        self,
    ) -> None:
        """Only `file_path` is an observation; evidence paths remain discoverable."""

        command_record = {
            "file_path": r"C:\Program Files\Python312\python.exe",
            "stdout_path": r"..\escaped.stdout.txt",
            "stderr_path": r"C:\escaped.stderr.txt",
            "report_path": r"..\escaped-report.json",
        }

        found = dict(_walk_paths(command_record))

        self.assertNotIn("$.file_path", found)
        self.assertEqual(r"..\escaped.stdout.txt", found["$.stdout_path"])
        self.assertEqual(r"C:\escaped.stderr.txt", found["$.stderr_path"])
        self.assertEqual(r"..\escaped-report.json", found["$.report_path"])


if __name__ == "__main__":
    unittest.main()
