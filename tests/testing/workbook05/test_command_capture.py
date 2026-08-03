from __future__ import annotations

import hashlib
import unittest
from pathlib import Path

from scripts.testing.workbook05.capture_documented_commands import (
    build_raw_github_url,
    extract_fenced_commands,
    validate_document_spec,
)


ROOT = Path(__file__).resolve().parents[3]


class CommandCaptureTests(unittest.TestCase):
    def test_fenced_commands_preserve_text_heading_and_line_numbers(self) -> None:
        fixture = (
            ROOT / "tests/testing/workbook05/fixtures/build-guide.md"
        ).read_text(encoding="utf-8")
        commands = extract_fenced_commands("DOC-1", fixture)

        self.assertEqual(2, len(commands))
        self.assertEqual("Windows build", commands[0].heading)
        self.assertEqual("powershell", commands[0].language)
        self.assertEqual(
            (
                "git clone --recursive "
                "https://example.invalid/repository.git\n"
                "cmake -S . -B build"
            ),
            commands[0].verbatim_text,
        )
        self.assertEqual(
            hashlib.sha256(
                commands[0].verbatim_text.encode("utf-8")
            ).hexdigest(),
            commands[0].sha256,
        )
        self.assertLess(commands[0].start_line, commands[0].end_line)

    def test_raw_url_contains_the_exact_commit(self) -> None:
        url = build_raw_github_url(
            "openvinotoolkit/openvino",
            "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
            "docs/dev/build_windows.md",
        )
        self.assertEqual(
            (
                "https://raw.githubusercontent.com/openvinotoolkit/openvino/"
                "b9a1f201c109e0bed74763934f79483cf6c4cbf4/"
                "docs/dev/build_windows.md"
            ),
            url,
        )

    def test_document_identifier_cannot_escape_snapshot_directory(self) -> None:
        specification = {
            "document_id": "../escape",
            "repository_full_name": "openvinotoolkit/openvino",
            "commit": "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
            "path": "README.md",
        }

        with self.assertRaisesRegex(ValueError, "document_id"):
            validate_document_spec(specification)

    def test_path_traversal_and_unpinned_refs_are_rejected(self) -> None:
        with self.assertRaisesRegex(ValueError, "40-character"):
            validate_document_spec(
                {
                    "document_id": "DOC-1",
                    "repository_full_name": "openvinotoolkit/openvino",
                    "commit": "master",
                    "path": "README.md",
                }
            )
        with self.assertRaisesRegex(ValueError, "repository-relative"):
            validate_document_spec(
                {
                    "document_id": "DOC-1",
                    "repository_full_name": "openvinotoolkit/openvino",
                    "commit": (
                        "b9a1f201c109e0bed74763934f79483cf6c4cbf4"
                    ),
                    "path": "../secret.txt",
                }
            )


if __name__ == "__main__":
    unittest.main()
