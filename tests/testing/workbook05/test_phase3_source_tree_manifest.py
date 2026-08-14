from __future__ import annotations

import csv
import json
import os
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory

from scripts.testing.workbook05.phase3.source_tree_manifest import (
    create_source_manifest,
)


class Phase3SourceTreeManifestTests(unittest.TestCase):
    """Bind every tracked VCS file to its relative name, size, and hash."""

    def test_complete_manifest_is_deterministic_and_text_only(self) -> None:
        with TemporaryDirectory() as directory:
            root = Path(directory) / "source"
            root.mkdir()
            (root / "a.txt").write_text("A", encoding="utf-8")
            (root / "nested").mkdir()
            (root / "nested" / "b.txt").write_text("B", encoding="utf-8")
            file_list = Path(directory) / "tracked.txt"
            file_list.write_text("nested/b.txt\na.txt\n", encoding="utf-8")
            csv_output = Path(directory) / "source-files.csv"
            json_output = Path(directory) / "source-tree.json"

            record = create_source_manifest(
                name="optimum",
                repository="huggingface/optimum",
                origin="https://github.com/huggingface/optimum.git",
                commit="1" * 40,
                root=root,
                file_list=file_list,
                csv_output=csv_output,
                json_output=json_output,
            )

            self.assertEqual(2, record["file_count"])
            self.assertRegex(record["aggregate_sha256"], r"^[0-9a-f]{64}$")
            self.assertEqual("source-files.csv", record["file_manifest_path"])
            parsed = json.loads(json_output.read_text(encoding="utf-8"))
            self.assertEqual(record, parsed)
            with csv_output.open("r", encoding="utf-8", newline="") as stream:
                rows = list(csv.DictReader(stream))
            self.assertEqual(
                ["a.txt", "nested/b.txt"],
                [row["relative_path"] for row in rows],
            )

    def test_parent_traversal_is_rejected(self) -> None:
        with TemporaryDirectory() as directory:
            root = Path(directory) / "source"
            root.mkdir()
            outside = Path(directory) / "outside.txt"
            outside.write_text("outside", encoding="utf-8")
            file_list = Path(directory) / "tracked.txt"
            file_list.write_text("../outside.txt\n", encoding="utf-8")

            with self.assertRaisesRegex(ValueError, "Unsafe tracked path"):
                create_source_manifest(
                    name="optimum",
                    repository="huggingface/optimum",
                    origin="https://github.com/huggingface/optimum.git",
                    commit="1" * 40,
                    root=root,
                    file_list=file_list,
                    csv_output=Path(directory) / "source-files.csv",
                    json_output=Path(directory) / "source-tree.json",
                )

    def test_case_colliding_tracked_paths_are_rejected(self) -> None:
        with TemporaryDirectory() as directory:
            root = Path(directory) / "source"
            root.mkdir()
            (root / "a.txt").write_text("A", encoding="utf-8")
            file_list = Path(directory) / "tracked.txt"
            file_list.write_text("a.txt\nA.TXT\n", encoding="utf-8")

            with self.assertRaises(ValueError):
                create_source_manifest(
                    name="optimum",
                    repository="huggingface/optimum",
                    origin="https://github.com/huggingface/optimum.git",
                    commit="1" * 40,
                    root=root,
                    file_list=file_list,
                    csv_output=Path(directory) / "source-files.csv",
                    json_output=Path(directory) / "source-tree.json",
                )

    @unittest.skipUnless(hasattr(os, "symlink"), "symlink support unavailable")
    def test_symlinked_tracked_file_is_rejected(self) -> None:
        with TemporaryDirectory() as directory:
            root = Path(directory) / "source"
            root.mkdir()
            target = root / "target.txt"
            target.write_text("target", encoding="utf-8")
            link = root / "link.txt"
            try:
                link.symlink_to(target)
            except OSError as error:
                self.skipTest(f"symlink creation unavailable: {error}")
            file_list = Path(directory) / "tracked.txt"
            file_list.write_text("link.txt\n", encoding="utf-8")

            with self.assertRaisesRegex(ValueError, "normal file"):
                create_source_manifest(
                    name="optimum",
                    repository="huggingface/optimum",
                    origin="https://github.com/huggingface/optimum.git",
                    commit="1" * 40,
                    root=root,
                    file_list=file_list,
                    csv_output=Path(directory) / "source-files.csv",
                    json_output=Path(directory) / "source-tree.json",
                )


if __name__ == "__main__":
    unittest.main()
