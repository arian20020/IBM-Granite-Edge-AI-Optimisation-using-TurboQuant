from __future__ import annotations

import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path

from scripts.testing.workbook05.source_verification import verify_source_tree


GIT = shutil.which("git")


def _git(*arguments: str, cwd: Path | None = None) -> str:
    """Run one Git command for the temporary recursive-submodule fixture."""

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


def _create_remote(root: Path, name: str) -> tuple[Path, Path, str]:
    """Create one bare remote and a writable seed checkout at a pinned commit."""

    remote = root / f"{name}.git"
    seed = root / f"{name}-seed"
    _git("init", "--bare", str(remote))
    _git("init", str(seed))
    _git("config", "user.email", "wb05-tests@example.invalid", cwd=seed)
    _git("config", "user.name", "Workbook 05 Tests", cwd=seed)
    (seed / "README.md").write_text(
        f"controlled {name} source\n",
        encoding="utf-8",
    )
    _git("add", "README.md", cwd=seed)
    _git("commit", "-m", "initial", cwd=seed)
    commit = _git("rev-parse", "HEAD", cwd=seed)
    _git("remote", "add", "origin", str(remote), cwd=seed)
    _git("push", "origin", "HEAD:refs/heads/main", cwd=seed)
    _git(
        "--git-dir",
        str(remote),
        "symbolic-ref",
        "HEAD",
        "refs/heads/main",
    )
    return remote, seed, commit


def _add_submodule(
    parent_seed: Path,
    child_remote: Path,
    relative_path: str,
    commit_message: str,
) -> str:
    """Add one local child remote and return the parent's new pinned commit."""

    _git(
        "-c",
        "protocol.file.allow=always",
        "submodule",
        "add",
        str(child_remote),
        relative_path,
        cwd=parent_seed,
    )
    _git("commit", "-am", commit_message, cwd=parent_seed)
    commit = _git("rev-parse", "HEAD", cwd=parent_seed)
    _git("push", "origin", "HEAD:refs/heads/main", cwd=parent_seed)
    return commit


def _spec(origin: Path, commit: str) -> dict[str, str]:
    """Build the allowlisted local-origin specification used by the verifier."""

    return {
        "route_id": "route-a-merged-openvino",
        "source_role": "runtime",
        "repository_full_name": "openvinotoolkit/openvino",
        "origin_url": str(origin),
        "commit": commit,
    }


@unittest.skipUnless(GIT, "Git is required for source-verification tests.")
class NestedSubmoduleSourceVerificationTests(unittest.TestCase):
    def test_recursive_manifest_records_the_nested_submodule_url(self) -> None:
        """Every recursive row must retain its own non-empty provenance URL."""

        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)

            # Create leaf <- middle <- parent so `git submodule status
            # --recursive` returns one top-level row and one nested row.
            leaf_remote, _, _ = _create_remote(root, "leaf")
            middle_remote, middle_seed, _ = _create_remote(root, "middle")
            _add_submodule(
                middle_seed,
                leaf_remote,
                "deps/leaf",
                "add nested leaf submodule",
            )
            parent_remote, parent_seed, _ = _create_remote(root, "parent")
            parent_commit = _add_submodule(
                parent_seed,
                middle_remote,
                "deps/middle",
                "add middle submodule",
            )

            # Materialise both levels exactly as the Intel source-admission run
            # does before asking the verifier to build its recursive manifest.
            source = root / "source"
            _git("clone", str(parent_remote), str(source))
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
                _spec(parent_remote, parent_commit),
                source,
                root / "evidence",
                60,
                allow_local_origins_for_tests=True,
            )

        rows = {
            row["path"]: row
            for row in result.report["submodules"]
        }
        self.assertEqual("Passed", result.report["decision"])
        self.assertTrue(result.report["submodules_complete"])
        self.assertEqual(str(middle_remote), rows["deps/middle"]["url"])
        self.assertEqual(
            str(leaf_remote),
            rows["deps/middle/deps/leaf"]["url"],
        )
        self.assertTrue(
            all(row["url"].strip() for row in result.report["submodules"]),
            "Recursive source provenance must never contain an empty URL.",
        )


if __name__ == "__main__":
    unittest.main()
