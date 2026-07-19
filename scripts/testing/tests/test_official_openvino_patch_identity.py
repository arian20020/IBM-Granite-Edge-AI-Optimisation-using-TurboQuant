import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from scripts.testing.official_openvino import patch_identity
from scripts.testing.official_openvino.patch_identity import (
    prepare_patch_workspace,
    validate_patch_identity,
)


EXPECTED = "7dea0459b2ac7d8dfd877fd9df6737674fd8371d"


def git(*args: str, cwd: Path | None = None) -> str:
    return subprocess.check_output(
        ["git", "-c", "core.longpaths=true", *args],
        cwd=cwd,
        text=True,
        stderr=subprocess.STDOUT,
    ).strip()


class PatchIdentityTests(unittest.TestCase):
    def test_patch_identity_accepts_clean_expected_base_with_patch_commit(self):
        validate_patch_identity(
            {"base_commit": EXPECTED, "patch_commit": EXPECTED, "dirty": False},
            EXPECTED,
        )

    def test_patch_identity_rejects_wrong_or_dirty_base(self):
        with self.assertRaisesRegex(ValueError, "base commit"):
            validate_patch_identity(
                {"base_commit": "wrong", "patch_commit": EXPECTED, "dirty": False},
                EXPECTED,
            )
        with self.assertRaisesRegex(ValueError, "dirty"):
            validate_patch_identity(
                {"base_commit": EXPECTED, "patch_commit": EXPECTED, "dirty": True},
                EXPECTED,
            )

    def test_patch_identity_rejects_missing_patch_commit(self):
        with self.assertRaisesRegex(ValueError, "patch commit"):
            validate_patch_identity({"base_commit": EXPECTED, "dirty": False}, EXPECTED)


class PatchWorkspaceControllerTests(unittest.TestCase):
    def setUp(self):
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary_directory.name)
        self.control = self.root / "control"
        self.upstream = self.root / "upstream"
        self.destination = self.root / "derived"

        git("init", str(self.control))
        patch_dir = self.control / "experiments/patches/openvino-turboquant"
        patch_dir.mkdir(parents=True)
        (patch_dir / "README.md").write_text("controlled patches\n", encoding="utf-8")
        git("-C", str(self.control), "add", ".")
        git("-C", str(self.control), "-c", "user.name=Test", "-c", "user.email=test@example.invalid", "commit", "-m", "control")

        git("init", str(self.upstream))
        (self.upstream / "value.txt").write_text("base\n", encoding="utf-8")
        git("-C", str(self.upstream), "add", "value.txt")
        git("-C", str(self.upstream), "-c", "user.name=Test", "-c", "user.email=test@example.invalid", "commit", "-m", "base")
        self.expected = git("-C", str(self.upstream), "rev-parse", "HEAD")

    def tearDown(self):
        self.temporary_directory.cleanup()

    def prepare(self) -> dict:
        with patch.object(patch_identity, "REPO_ROOT", self.control):
            return prepare_patch_workspace(self.upstream, self.destination, self.expected)

    def add_tracked_patch(self):
        patch_path = self.control / "experiments/patches/openvino-turboquant/0001-value.patch"
        (self.upstream / "value.txt").write_text("patched\n", encoding="utf-8")
        patch_text = subprocess.check_output(
            ["git", "-c", "core.longpaths=true", "-C", str(self.upstream), "diff"],
            text=True,
        )
        patch_path.write_text(patch_text, encoding="utf-8")
        git("-C", str(self.upstream), "checkout", "--", "value.txt")
        git("-C", str(self.control), "add", patch_path.relative_to(self.control).as_posix())
        git("-C", str(self.control), "-c", "user.name=Test", "-c", "user.email=test@example.invalid", "commit", "-m", "patch")

    def test_callable_applies_tracked_patch_and_preserves_provenance_on_rerun(self):
        self.add_tracked_patch()
        first = self.prepare()
        second = self.prepare()

        self.assertEqual(first["patch_commit"], second["patch_commit"])
        self.assertEqual(second["applied_patches"], ["0001-value.patch"])
        self.assertEqual((self.destination / "value.txt").read_text(), "patched\n")
        self.assertEqual(git("-C", str(self.destination), "status", "--porcelain"), "")
        self.assertEqual(git("-C", str(self.destination), "config", "--bool", "core.longpaths"), "true")

    def test_rejects_dirty_upstream(self):
        (self.upstream / "dirty.txt").write_text("dirty\n")
        with self.assertRaisesRegex(ValueError, "upstream.*dirty"):
            self.prepare()

    def test_rejects_dirty_destination_without_mutating_its_config(self):
        self.prepare()
        subprocess.run(["git", "-C", str(self.destination), "config", "core.longpaths", "false"], check=True)
        (self.destination / "dirty.txt").write_text("dirty\n")
        with self.assertRaisesRegex(ValueError, "destination.*dirty"):
            self.prepare()
        self.assertEqual(
            subprocess.check_output(
                ["git", "-C", str(self.destination), "config", "--bool", "core.longpaths"], text=True
            ).strip(),
            "false",
        )

    def test_rejects_wrong_branch(self):
        self.prepare()
        git("-C", str(self.destination), "branch", "-m", "wrong")
        with self.assertRaisesRegex(ValueError, "branch"):
            self.prepare()

    def test_rejects_arbitrary_descendant_head(self):
        self.prepare()
        git("-C", str(self.destination), "-c", "user.name=Test", "-c", "user.email=test@example.invalid", "commit", "--allow-empty", "-m", "foreign")
        with self.assertRaisesRegex(ValueError, "derived HEAD"):
            self.prepare()

    def test_rejects_foreign_origin(self):
        self.prepare()
        foreign = self.root / "foreign"
        git("init", str(foreign))
        git("-C", str(self.destination), "remote", "set-url", "origin", str(foreign))
        with self.assertRaisesRegex(ValueError, "origin"):
            self.prepare()

    def test_rejects_untracked_patch_and_dirty_controlling_repo(self):
        patch_path = self.control / "experiments/patches/openvino-turboquant/untracked.patch"
        patch_path.write_text("untracked\n", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "controlling repository.*dirty"):
            self.prepare()


if __name__ == "__main__":
    unittest.main()
