from __future__ import annotations

import hashlib
import os
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory

from scripts.testing.workbook05.phase3.hashing import sha256_file, sha256_tree
from scripts.testing.workbook05.phase3.paths import (
    assert_normal_local_directory,
    relative_evidence_path,
)


class Phase3PathTests(unittest.TestCase):
    def test_sibling_prefix_is_not_a_child(self) -> None:
        with self.assertRaisesRegex(ValueError, "approved root"):
            assert_normal_local_directory(
                Path(r"C:\w5m-other\run"),
                Path(r"C:\w5m"),
            )

    def test_tree_hash_binds_relative_name_size_and_file_digest(self) -> None:
        with TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "a.txt").write_text("A", encoding="utf-8")
            (root / "b.txt").write_text("B", encoding="utf-8")

            first = sha256_tree(root, [root / "a.txt", root / "b.txt"])
            second = sha256_tree(root, [root / "b.txt", root / "a.txt"])
            self.assertEqual(first, second)

            (root / "b.txt").write_text("changed", encoding="utf-8")
            self.assertNotEqual(
                first,
                sha256_tree(root, [root / "a.txt", root / "b.txt"]),
            )

    def test_tree_hash_changes_when_only_the_relative_name_changes(self) -> None:
        with TemporaryDirectory() as directory:
            root = Path(directory)
            original = root / "a.txt"
            renamed = root / "renamed.txt"
            original.write_text("same bytes", encoding="utf-8")
            first = sha256_tree(root, [original])
            original.rename(renamed)
            self.assertNotEqual(first, sha256_tree(root, [renamed]))

    def test_duplicate_canonical_relative_path_is_rejected(self) -> None:
        with TemporaryDirectory() as directory:
            root = Path(directory)
            file_path = root / "one.txt"
            file_path.write_text("one", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "Duplicate canonical"):
                sha256_tree(root, [file_path, file_path])

    def test_tree_hash_rejects_file_outside_root(self) -> None:
        with TemporaryDirectory() as directory:
            base = Path(directory)
            root = base / "root"
            root.mkdir()
            outside = base / "outside.txt"
            outside.write_text("outside", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "outside the tree root"):
                sha256_tree(root, [outside])

    def test_symlink_file_is_rejected(self) -> None:
        with TemporaryDirectory() as directory:
            root = Path(directory)
            target = root / "target.txt"
            link = root / "link.txt"
            target.write_text("target", encoding="utf-8")
            try:
                link.symlink_to(target)
            except (OSError, NotImplementedError):
                self.skipTest("Symlink creation is unavailable on this runner.")

            with self.assertRaisesRegex(ValueError, "link or reparse point"):
                sha256_file(link)
            with self.assertRaisesRegex(ValueError, "link or reparse point"):
                sha256_tree(root, [link])

    def test_symlink_directory_is_rejected(self) -> None:
        with TemporaryDirectory() as directory:
            base = Path(directory)
            root = base / "approved"
            target = root / "target"
            link = root / "link"
            target.mkdir(parents=True)
            try:
                link.symlink_to(target, target_is_directory=True)
            except (OSError, NotImplementedError):
                self.skipTest("Directory symlink creation is unavailable on this runner.")

            with self.assertRaisesRegex(ValueError, "link or reparse point"):
                assert_normal_local_directory(link, root)

    def test_sha256_file_matches_standard_library(self) -> None:
        with TemporaryDirectory() as directory:
            file_path = Path(directory) / "payload.bin"
            file_path.write_bytes(b"phase-three")
            self.assertEqual(
                hashlib.sha256(b"phase-three").hexdigest(),
                sha256_file(file_path),
            )

    def test_relative_evidence_path_uses_forward_slashes(self) -> None:
        with TemporaryDirectory() as directory:
            root = Path(directory)
            evidence = root / "proof" / "record.json"
            evidence.parent.mkdir()
            evidence.write_text("{}", encoding="utf-8")
            self.assertEqual(
                "proof/record.json",
                relative_evidence_path(root, evidence),
            )

    def test_relative_evidence_path_rejects_outside_file(self) -> None:
        with TemporaryDirectory() as directory:
            base = Path(directory)
            root = base / "root"
            root.mkdir()
            outside = base / "record.json"
            outside.write_text("{}", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "outside the evidence root"):
                relative_evidence_path(root, outside)

    def test_root_requires_explicit_allow_root(self) -> None:
        with TemporaryDirectory() as directory:
            root = Path(directory)
            with self.assertRaisesRegex(ValueError, "root itself"):
                assert_normal_local_directory(root, root)
            self.assertEqual(
                root.resolve(),
                assert_normal_local_directory(root, root, allow_root=True),
            )

    def test_unc_and_device_paths_are_rejected_before_filesystem_access(self) -> None:
        root = Path(r"C:\w5m")
        for unsafe in (
            Path(r"\\server\share\asset"),
            Path(r"\\?\C:\w5m\asset"),
            Path(r"\\.\C:\w5m\asset"),
        ):
            with self.subTest(path=str(unsafe)):
                with self.assertRaises(ValueError):
                    assert_normal_local_directory(unsafe, root)

    @unittest.skipUnless(os.name == "nt", "Windows-specific case-insensitive containment")
    def test_windows_containment_is_case_insensitive(self) -> None:
        with TemporaryDirectory() as directory:
            root = Path(directory)
            child = root / "Child"
            child.mkdir()
            self.assertEqual(
                child.resolve(),
                assert_normal_local_directory(child, Path(str(root).upper())),
            )


if __name__ == "__main__":
    unittest.main()
