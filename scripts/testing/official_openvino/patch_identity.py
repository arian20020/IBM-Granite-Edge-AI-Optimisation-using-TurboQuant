"""Prepare and validate a reproducible project-patched OpenVINO GenAI checkout."""

from __future__ import annotations

import argparse
import json
import os
import subprocess
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[3]
BRANCH = "project/turboquant-wb04"
PATCH_DIRECTORY = Path("experiments/patches/openvino-turboquant")


def _git(
    repository: Path | None,
    *arguments: str,
    env: dict | None = None,
    input_text: str | None = None,
    input_bytes: bytes | None = None,
) -> str:
    command = ["git", "-c", "core.longpaths=true"]
    if repository is not None:
        command.extend(["-C", str(repository)])
    command.extend(arguments)
    if input_text is not None and input_bytes is not None:
        raise ValueError("only one Git input form may be supplied")
    binary = input_bytes is not None
    result = subprocess.run(
        command,
        check=False,
        capture_output=True,
        text=not binary,
        env=env,
        input=input_bytes if binary else input_text,
    )
    stdout = result.stdout.decode("utf-8", errors="replace") if binary else result.stdout
    stderr = result.stderr.decode("utf-8", errors="replace") if binary else result.stderr
    if result.returncode:
        detail = (stderr or stdout).strip()
        raise ValueError(f"git {' '.join(arguments)} failed ({result.returncode}): {detail}")
    return stdout.strip()


def _clean(repository: Path) -> bool:
    return _git(repository, "status", "--porcelain", "--untracked-files=all") == ""


def _same_path(left: str | Path, right: str | Path) -> bool:
    return os.path.normcase(str(Path(left).resolve())) == os.path.normcase(str(Path(right).resolve()))


def _controlled_patches() -> list[Path]:
    if not _clean(REPO_ROOT):
        raise ValueError("controlling repository is dirty; patch provenance is not reproducible")
    patch_root = REPO_ROOT / PATCH_DIRECTORY
    patches = sorted(patch_root.glob("*.patch"), key=lambda path: path.name)
    for patch in patches:
        relative = patch.relative_to(REPO_ROOT).as_posix()
        _git(REPO_ROOT, "ls-files", "--error-unmatch", "--", relative)
        head_blob = _git(REPO_ROOT, "rev-parse", f"HEAD:{relative}")
        working_blob = _git(REPO_ROOT, "hash-object", "--", relative)
        if working_blob != head_blob:
            raise ValueError(f"patch is not identical to controlling repository HEAD: {relative}")
    return patches


def _create_exact_checkout(upstream: Path, destination: Path, expected_commit: str, patches: list[Path]) -> str:
    destination.parent.mkdir(parents=True, exist_ok=True)
    _git(None, "clone", "--local", "--no-hardlinks", "--no-checkout", str(upstream), str(destination))
    _git(destination, "checkout", "--detach", expected_commit)
    _git(destination, "checkout", "-b", BRANCH)
    for patch in patches:
        patch_bytes = patch.read_bytes().replace(b"\r\n", b"\n")
        _git(destination, "apply", "--cached", "--check", "-", input_bytes=patch_bytes)
        _git(destination, "apply", "--cached", "-", input_bytes=patch_bytes)

    if _git(destination, "diff", "--cached", "--name-only"):
        tree = _git(destination, "write-tree")
        timestamp = _git(upstream, "show", "-s", "--format=%cI", expected_commit)
        commit_env = os.environ.copy()
        commit_env.update(
            {
                "GIT_AUTHOR_NAME": "IBM Granite Project",
                "GIT_AUTHOR_EMAIL": "project@example.invalid",
                "GIT_AUTHOR_DATE": timestamp,
                "GIT_COMMITTER_NAME": "IBM Granite Project",
                "GIT_COMMITTER_EMAIL": "project@example.invalid",
                "GIT_COMMITTER_DATE": timestamp,
            }
        )
        patch_commit = _git(
            destination,
            "commit-tree",
            tree,
            "-p",
            expected_commit,
            env=commit_env,
            input_text="Apply controlled OpenVINO TurboQuant patch set\n",
        )
        _git(destination, "update-ref", f"refs/heads/{BRANCH}", patch_commit)
        _git(destination, "reset", "--hard", patch_commit)
    else:
        patch_commit = expected_commit

    _git(destination, "submodule", "update", "--init", "--recursive")
    if not _clean(destination):
        raise ValueError("derived destination is dirty after preparation")
    _git(destination, "config", "core.longpaths", "true")
    return patch_commit


def _verify_existing(upstream: Path, destination: Path, expected_head: str) -> None:
    if not (destination / ".git").exists():
        raise ValueError("existing destination is not a Git checkout")
    if not _clean(destination):
        raise ValueError("existing destination is dirty")
    branch = _git(destination, "branch", "--show-current")
    if branch != BRANCH:
        raise ValueError(f"existing destination branch mismatch: expected {BRANCH}, found {branch}")
    origin = _git(destination, "remote", "get-url", "origin")
    if not _same_path(origin, upstream):
        raise ValueError(f"existing destination origin mismatch: expected {upstream}, found {origin}")
    actual_head = _git(destination, "rev-parse", "HEAD")
    if actual_head != expected_head:
        raise ValueError(f"existing destination derived HEAD mismatch: expected {expected_head}, found {actual_head}")
    _git(destination, "config", "core.longpaths", "true")


def prepare_patch_workspace(upstream: Path, destination: Path, expected_commit: str) -> dict:
    """Create or strictly verify the checkout derived from base plus controlled patches."""
    upstream = Path(upstream).resolve()
    destination = Path(destination).resolve()
    if not (upstream / ".git").exists():
        raise ValueError(f"pinned upstream checkout not found: {upstream}")
    upstream_commit = _git(upstream, "rev-parse", "HEAD")
    if upstream_commit != expected_commit:
        raise ValueError(f"upstream base commit mismatch: expected {expected_commit}, found {upstream_commit}")
    if not _clean(upstream):
        raise ValueError("upstream checkout is dirty")

    patches = _controlled_patches()
    if destination.exists():
        # Build the expected identity independently; do not mutate the destination
        # until every provenance and cleanliness check has passed.
        import tempfile

        with tempfile.TemporaryDirectory(prefix="openvino-turboquant-identity-") as temporary_directory:
            reference = Path(temporary_directory) / "reference"
            expected_head = _create_exact_checkout(upstream, reference, expected_commit, patches)
        _verify_existing(upstream, destination, expected_head)
        patch_commit = expected_head
    else:
        patch_commit = _create_exact_checkout(upstream, destination, expected_commit, patches)

    record = {
        "upstream_path": str(upstream),
        "destination_path": str(destination),
        "patch_directory": str((REPO_ROOT / PATCH_DIRECTORY).resolve()),
        "branch": BRANCH,
        "base_commit": expected_commit,
        "upstream_commit": upstream_commit,
        "patch_commit": patch_commit,
        "applied_patches": [patch.name for patch in patches],
        "dirty": False,
    }
    validate_patch_identity(record, expected_commit)
    return record


def validate_patch_identity(record: dict, expected_base: str) -> None:
    """Reject evidence that does not identify a clean, pinned patch workspace."""
    if record.get("base_commit") != expected_base:
        raise ValueError("base commit mismatch")
    if record.get("dirty") is not False:
        raise ValueError("dirty patch workspace")
    if not record.get("patch_commit"):
        raise ValueError("patch commit missing")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--upstream", type=Path, required=True)
    parser.add_argument("--destination", type=Path, required=True)
    parser.add_argument("--expected-commit", required=True)
    parser.add_argument("--evidence", type=Path, required=True)
    args = parser.parse_args()
    record = prepare_patch_workspace(args.upstream, args.destination, args.expected_commit)
    args.evidence.parent.mkdir(parents=True, exist_ok=True)
    args.evidence.write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    print(f"Prepared patch workspace: {args.destination.resolve()}")
    print(f"Identity evidence: {args.evidence.resolve()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
