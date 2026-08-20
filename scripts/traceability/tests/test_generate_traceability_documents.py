from __future__ import annotations

import sys
import unittest
from pathlib import Path


TRACEABILITY_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TRACEABILITY_ROOT))

from generate_traceability_documents import (  # noqa: E402
    requirement_catalogue_document,
    task_catalogue_document,
    task_type_catalogue,
)


class MarkdownWhitespaceTests(unittest.TestCase):
    def test_generated_metadata_has_no_trailing_whitespace(self) -> None:
        catalogue = {
            "metadata": {
                "catalogue_version": "1.3",
                "generated_on": "2026-08-21",
                "source_sha256": "a" * 64,
                "counts": {"active_tasks": 0},
            },
            "tasks": [],
            "requirements_baseline": [],
        }
        output = Path("docs/traceability/generated/test.md")
        documents = (
            task_catalogue_document(catalogue, output),
            requirement_catalogue_document(catalogue, output, active_only=True),
            task_type_catalogue(catalogue, output, "work-package", "Work-Package Catalogue"),
        )

        for document in documents:
            with self.subTest(document=document.splitlines()[2]):
                self.assertFalse(
                    any(line != line.rstrip() for line in document.splitlines()),
                    "generated Markdown contains trailing whitespace",
                )


if __name__ == "__main__":
    unittest.main()
