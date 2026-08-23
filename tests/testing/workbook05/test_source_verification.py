from __future__ import annotations

import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path

from scripts.testing.workbook05.source_verification import verify_source_tree


GIT = shutil.which("git")


def _git(*arguments: str, cwd: Path | None = None) -> str:
    """Run one Git command for a temporary integration fixture."""

    completed = subprocess.run(
        [GIT or "git", *arguments],
        cwd=cwd,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        check=False,
    )
    if completed.returncode != 0:
        raise RuntimeError(
            f"git {' '.join(arguments)} failed: {completed.stderr}"
        )
    return completed.stdout.strip()


def _create_remote(root: Path, name: str = "remote") -> tuple[Path, str]:
    """Create a bare remote whose advertised default branch is valid."""

    remote = root / f"{name}.git"
    seed = root / f"{name}-seed"
    _git("init", "--bare", str(remote))
    _git("init", str(seed))
    _git("config", "user.email", "wb05-tests@example.invalid", cwd=seed)
    _git("config", "user.name", "Workbook 05 Tests", cwd=seed)
    (seed / "README.md").write_text("controlled source\n", encoding="utf-8")
    _git("add", "README.md", cwd=seed)
    _git("commit", "-m", "initial", cwd=seed)
    commit = _git("rev-parse", "HEAD", cwd=seed)
    _git("remote", "add", "origin", str(remote), cwd=seed)
    _git("push", "origin", "HEAD:refs/heads/main", cwd=seed)

    # A bare repository created by `git init --bare` may still advertise
    # `master`. Point HEAD to the branch we actually pushed so ordinary clones
    # materialise the controlled commit on every Windows runner.
    _git(
        "--git-dir",
        str(remote),
        "symbolic-ref",
        "HEAD",
        "refs/heads/main",
    )
    return remote, commit


def _spec(origin: Path, commit: str) -> dict[str, str]:
    return {
        "route_id": "route-a-merged-openvino",
        "source_role": "runtime",
        "repository_full_name": "openvinotoolkit/openvino",
        "origin_url": str(origin),
        "commit": commit,
    }


@unittest.skipUnless(GIT, "Git is required for source-verification tests.")
class SourceVerificationTests(unittest.TestCase):
    def test_absent_source_tree_is_cloned_at_the_exact_commit(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            remote, commit = _create_remote(root)
            source = root / "source"

            result = verify_source_tree(
                _spec(remote, commit),
                source,
                root / "evidence",
                60,
                allow_local_origins_for_tests=True,
            )

            self.assertTrue((source / ".git").exists())
            self.assertEqual(commit, _git("rev-parse", "HEAD", cwd=source))
        self.assertEqual("Passed", result.report["decision"])
        self.assertGreaterEqual(len(result.command_records), 10)

    def test_existing_exact_source_tree_is_accepted(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            remote, commit = _create_remote(root)
            source = root / "source"
            _git("clone", str(remote), str(source))

            result = verify_source_tree(
                _spec(remote, commit),
                source,
                root / "evidence",
                60,
                allow_local_origins_for_tests=True,
            )

        self.assertEqual("Passed", result.report["decision"])
        self.assertEqual(commit, result.report["actual_commit"])
        self.assertTrue(result.report["working_tree_clean"])
        self.assertTrue(result.report["submodules_complete"])
        self.assertGreaterEqual(len(result.command_records), 5)

    def test_dirty_tree_is_blocked_without_cleanup(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            remote, commit = _create_remote(root)
            source = root / "source"
            _git("clone", str(remote), str(source))
            marker = source / "untracked.txt"
            marker.write_text("keep me\n", encoding="utf-8")

            result = verify_source_tree(
                _spec(remote, commit),
                source,
                root / "evidence",
                60,
                allow_local_origins_for_tests=True,
            )

            self.assertTrue(marker.exists())
        self.assertEqual("Blocked", result.report["decision"])
        self.assertIn("not clean", result.report["decision_reason"])

    def test_mismatched_origin_is_blocked(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            expected_remote, commit = _create_remote(root, "expected")
            actual_remote, _ = _create_remote(root, "actual")
            source = root / "source"
            _git("clone", str(actual_remote), str(source))

            result = verify_source_tree(
                _spec(expected_remote, commit),
                source,
                root / "evidence",
                60,
                allow_local_origins_for_tests=True,
            )

        self.assertEqual("Blocked", result.report["decision"])
        self.assertIn("Origin mismatch", result.report["decision_reason"])

    def test_unexpected_existing_directory_is_preserved_and_blocked(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            remote, commit = _create_remote(root)
            source = root / "source"
            source.mkdir()
            sentinel = source / "do-not-delete.txt"
            sentinel.write_text("preserve\n", encoding="utf-8")

            result = verify_source_tree(
                _spec(remote, commit),
                source,
                root / "evidence",
                60,
                allow_local_origins_for_tests=True,
            )

            self.assertTrue(sentinel.exists())
        self.assertEqual("Blocked", result.report["decision"])
        self.assertEqual((), result.command_records)

    def test_unexpected_head_is_blocked_without_reset(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            remote, expected_commit = _create_remote(root)
            source = root / "source"
            _git("clone", str(remote), str(source))
            _git("config", "user.email", "wb05-tests@example.invalid", cwd=source)
            _git("config", "user.name", "Workbook 05 Tests", cwd=source)
            (source / "second.txt").write_text("second\n", encoding="utf-8")
            _git("add", "second.txt", cwd=source)
            _git("commit", "-m", "second", cwd=source)
            actual_commit = _git("rev-parse", "HEAD", cwd=source)

            result = verify_source_tree(
                _spec(remote, expected_commit),
                source,
                root / "evidence",
                60,
                allow_local_origins_for_tests=True,
            )

            self.assertEqual(actual_commit, _git("rev-parse", "HEAD", cwd=source))
        self.assertEqual("Blocked", result.report["decision"])
        self.assertIn("Commit mismatch", result.report["decision_reason"])

    def test_recursive_submodule_manifest_is_recorded(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            sub_remote, _ = _create_remote(root, "submodule")
            remote, _ = _create_remote(root, "parent")
            seed = root / "parent-working-copy"
            _git("clone", str(remote), str(seed))
            _git("config", "user.email", "wb05-tests@example.invalid", cwd=seed)
            _git("config", "user.name", "Workbook 05 Tests", cwd=seed)
            _git(
                "-c",
                "protocol.file.allow=always",
                "submodule",
                "add",
                str(sub_remote),
                "deps/sub",
                cwd=seed,
            )
            _git("commit", "-am", "add submodule", cwd=seed)
            commit = _git("rev-parse", "HEAD", cwd=seed)
            _git("push", "origin", "HEAD:refs/heads/main", cwd=seed)

            source = root / "source"
            _git("clone", str(remote), str(source))
            _git(
                "-c",
                "protocol.file.allow=always",
                "submodule",
                "update",
                "--init",
                "--recursive",
                cwd=source,
            )

            result = verify_source_tree(
                _spec(remote, commit),
                source,
                root / "evidence",
                60,
                allow_local_origins_for_tests=True,
            )

        self.assertEqual("Passed", result.report["decision"])
        self.assertEqual(1, len(result.report["submodules"]))
        self.assertEqual("deps/sub", result.report["submodules"][0]["path"])
        self.assertEqual("clean", result.report["submodules"][0]["status"])

    def test_malformed_or_moving_commit_is_rejected_before_git_runs(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            remote, _ = _create_remote(root)
            specification = _spec(remote, "main")

            with self.assertRaisesRegex(ValueError, "40-character SHA"):
                verify_source_tree(
                    specification,
                    root / "source",
                    root / "evidence",
                    60,
                    allow_local_origins_for_tests=True,
                )


if __name__ == "__main__":
    unittest.main()
