"""Prepare and validate a reproducible project-patched OpenVINO checkout."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import stat
import subprocess
import tempfile
from collections.abc import Mapping
from dataclasses import dataclass
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[3]


@dataclass(frozen=True)
class PatchWorkspaceSpec:
    branch: str
    patch_directory: Path
    commit_message: str


@dataclass(frozen=True)
class ControlledPatch:
    path: Path
    relative_path: str
    name: str
    blob_id: str
    sha256: str
    raw_bytes: bytes
    apply_bytes: bytes


GENAI_TURBOQUANT_SPEC = PatchWorkspaceSpec(
    branch="project/turboquant-wb04",
    patch_directory=Path("experiments/patches/openvino-turboquant"),
    commit_message="Apply controlled OpenVINO TurboQuant patch set",
)

CORE_OBSERVER_SPEC = PatchWorkspaceSpec(
    branch="project/cpu-state-allocation-observer",
    patch_directory=Path("experiments/patches/openvino-cpu-state-observer"),
    commit_message="Apply controlled OpenVINO CPU state observer patch set",
)

FAMILY_SPECS: Mapping[str, PatchWorkspaceSpec] = {
    "openvino-genai-turboquant": GENAI_TURBOQUANT_SPEC,
    "openvino-cpu-observer": CORE_OBSERVER_SPEC,
}


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


def _is_link_or_reparse(path: Path) -> bool:
    try:
        metadata = path.lstat()
    except FileNotFoundError:
        return False
    attributes = getattr(metadata, "st_file_attributes", 0)
    reparse_flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0)
    return path.is_symlink() or bool(reparse_flag and attributes & reparse_flag)


def _path_at_or_below(path: Path, parent: Path) -> bool:
    try:
        common = Path(os.path.commonpath([str(path.resolve()), str(parent.resolve())]))
    except ValueError:
        return False
    return _same_path(common, parent)


def _patch_root(spec: PatchWorkspaceSpec) -> Path:
    repository_root = REPO_ROOT.resolve()
    relative = Path(spec.patch_directory)
    if relative.is_absolute() or ".." in relative.parts:
        raise ValueError("patch directory is outside the controlling repository")
    current = repository_root
    for component in relative.parts:
        if component in ("", "."):
            continue
        current /= component
        if os.path.lexists(current) and _is_link_or_reparse(current):
            raise ValueError(f"patch directory contains a symlink or reparse point: {current}")
    candidate = (repository_root / relative).resolve()
    try:
        common = Path(os.path.commonpath([str(repository_root), str(candidate)]))
    except ValueError as exc:
        raise ValueError("patch directory is outside the controlling repository") from exc
    if _same_path(candidate, repository_root) or not _same_path(common, repository_root):
        raise ValueError("patch directory is outside the controlling repository")
    if not candidate.is_dir():
        raise ValueError(f"patch directory not found: {candidate}")
    return candidate


def _controlled_patches(spec: PatchWorkspaceSpec) -> list[ControlledPatch]:
    if not _clean(REPO_ROOT):
        raise ValueError("controlling repository is dirty; patch provenance is not reproducible")
    patch_root = _patch_root(spec)
    paths = sorted(
        (path for path in patch_root.iterdir() if path.suffix == ".patch"),
        key=lambda path: path.name,
    )
    patches: list[ControlledPatch] = []
    for patch in paths:
        if _is_link_or_reparse(patch):
            raise ValueError(f"patch is a symlink or reparse point: {patch}")
        if not patch.is_file():
            raise ValueError(f"patch is not a regular file: {patch}")
        relative = patch.relative_to(REPO_ROOT.resolve()).as_posix()
        _git(REPO_ROOT, "ls-files", "--error-unmatch", "--", relative)
        head_blob = _git(REPO_ROOT, "rev-parse", f"HEAD:{relative}")
        raw_bytes = patch.read_bytes()
        working_blob = _git(
            REPO_ROOT,
            "hash-object",
            f"--path={relative}",
            "--stdin",
            input_bytes=raw_bytes,
        )
        if working_blob != head_blob:
            raise ValueError(f"patch is not identical to controlling repository HEAD: {relative}")
        patches.append(
            ControlledPatch(
                path=patch,
                relative_path=relative,
                name=patch.name,
                blob_id=head_blob,
                sha256=hashlib.sha256(raw_bytes).hexdigest(),
                raw_bytes=raw_bytes,
                apply_bytes=raw_bytes.replace(b"\r\n", b"\n"),
            )
        )
    return patches


def _patch_evidence(patches: list[ControlledPatch]) -> list[dict[str, str]]:
    return [
        {
            "name": patch.name,
            "blob_id": patch.blob_id,
            "sha256": patch.sha256,
        }
        for patch in patches
    ]


def _revalidate_controlled_patches(
    patches: list[ControlledPatch],
    spec: PatchWorkspaceSpec,
) -> None:
    try:
        if not _clean(REPO_ROOT):
            raise ValueError("controlling repository is dirty")
        patch_root = _patch_root(spec)
        current_names = sorted(
            path.name for path in patch_root.iterdir() if path.suffix == ".patch"
        )
        if current_names != [patch.name for patch in patches]:
            raise ValueError("controlled patch set changed")
        for patch in patches:
            if _is_link_or_reparse(patch.path) or not patch.path.is_file():
                raise ValueError(f"controlled patch path changed: {patch.relative_path}")
            raw_bytes = patch.path.read_bytes()
            if raw_bytes != patch.raw_bytes:
                raise ValueError(f"controlled patch bytes changed: {patch.relative_path}")
            if hashlib.sha256(raw_bytes).hexdigest() != patch.sha256:
                raise ValueError(f"controlled patch SHA-256 changed: {patch.relative_path}")
            head_blob = _git(REPO_ROOT, "rev-parse", f"HEAD:{patch.relative_path}")
            working_blob = _git(
                REPO_ROOT,
                "hash-object",
                f"--path={patch.relative_path}",
                "--stdin",
                input_bytes=raw_bytes,
            )
            if head_blob != patch.blob_id or working_blob != patch.blob_id:
                raise ValueError(f"controlled patch blob changed: {patch.relative_path}")
    except (OSError, ValueError) as exc:
        raise ValueError(f"controlling repository changed during patch preparation: {exc}") from exc


def _create_exact_checkout(
    upstream: Path,
    destination: Path,
    expected_commit: str,
    patches: list[ControlledPatch],
    spec: PatchWorkspaceSpec,
) -> str:
    destination.parent.mkdir(parents=True, exist_ok=True)
    _git(None, "clone", "--local", "--no-hardlinks", "--no-checkout", str(upstream), str(destination))
    _git(destination, "checkout", "--detach", expected_commit)
    _git(destination, "checkout", "-b", spec.branch)
    for patch in patches:
        _git(destination, "apply", "--cached", "--check", "-", input_bytes=patch.apply_bytes)
        _git(destination, "apply", "--cached", "-", input_bytes=patch.apply_bytes)

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
            input_text=f"{spec.commit_message}\n",
        )
        _git(destination, "update-ref", f"refs/heads/{spec.branch}", patch_commit)
        _git(destination, "reset", "--hard", patch_commit)
    else:
        patch_commit = expected_commit

    _git(destination, "submodule", "update", "--init", "--recursive")
    if not _clean(destination):
        raise ValueError("derived destination is dirty after preparation")
    _git(destination, "config", "core.longpaths", "true")
    return patch_commit


def _create_and_publish_checkout(
    upstream: Path,
    destination: Path,
    expected_commit: str,
    patches: list[ControlledPatch],
    spec: PatchWorkspaceSpec,
) -> str:
    destination.parent.mkdir(parents=True, exist_ok=True)
    staging_root = Path(
        tempfile.mkdtemp(
            prefix=f".{destination.name}.preparing-",
            dir=destination.parent,
        )
    )
    staging = staging_root / "checkout"
    try:
        patch_commit = _create_exact_checkout(
            upstream,
            staging,
            expected_commit,
            patches,
            spec,
        )
        if destination.exists():
            raise ValueError("destination appeared while patch workspace was being prepared")
        os.replace(staging, destination)
        return patch_commit
    finally:
        shutil.rmtree(staging_root, ignore_errors=True)


def _verify_existing(
    upstream: Path,
    destination: Path,
    expected_head: str,
    spec: PatchWorkspaceSpec,
) -> None:
    if not (destination / ".git").exists():
        raise ValueError("existing destination is not a Git checkout")
    if not _clean(destination):
        raise ValueError("existing destination is dirty")
    branch = _git(destination, "branch", "--show-current")
    if branch != spec.branch:
        raise ValueError(
            f"existing destination branch mismatch: expected {spec.branch}, found {branch}"
        )
    origin = _git(destination, "remote", "get-url", "origin")
    if not _same_path(origin, upstream):
        raise ValueError(f"existing destination origin mismatch: expected {upstream}, found {origin}")
    actual_head = _git(destination, "rev-parse", "HEAD")
    if actual_head != expected_head:
        raise ValueError(
            f"existing destination derived HEAD mismatch: expected {expected_head}, found {actual_head}"
        )
    _git(destination, "config", "core.longpaths", "true")


def spec_for_family(name: str) -> PatchWorkspaceSpec:
    try:
        return FAMILY_SPECS[name]
    except KeyError as exc:
        raise ValueError(f"unknown patch family: {name}") from exc


def prepare_patch_workspace(
    upstream: Path,
    destination: Path,
    expected_commit: str,
    spec: PatchWorkspaceSpec | None = None,
) -> dict[str, object]:
    """Create or strictly verify a checkout derived from base plus controlled patches."""
    spec = GENAI_TURBOQUANT_SPEC if spec is None else spec
    upstream = Path(upstream).resolve()
    destination = Path(destination).resolve()
    if _path_at_or_below(destination, upstream):
        raise ValueError("destination must not equal or reside inside the pinned upstream")
    if not (upstream / ".git").exists():
        raise ValueError(f"pinned upstream checkout not found: {upstream}")
    upstream_commit = _git(upstream, "rev-parse", "HEAD")
    if upstream_commit != expected_commit:
        raise ValueError(f"upstream base commit mismatch: expected {expected_commit}, found {upstream_commit}")
    if not _clean(upstream):
        raise ValueError("upstream checkout is dirty")

    try:
        patches = _controlled_patches(spec)
        if destination.exists():
            with tempfile.TemporaryDirectory(prefix="openvino-patch-identity-") as temporary_directory:
                reference = Path(temporary_directory) / "reference"
                expected_head = _create_exact_checkout(
                    upstream,
                    reference,
                    expected_commit,
                    patches,
                    spec,
                )
            _verify_existing(upstream, destination, expected_head, spec)
            patch_commit = expected_head
        else:
            patch_commit = _create_and_publish_checkout(
                upstream,
                destination,
                expected_commit,
                patches,
                spec,
            )
    finally:
        final_upstream_commit = _git(upstream, "rev-parse", "HEAD")
        if final_upstream_commit != expected_commit:
            raise ValueError(
                "pinned upstream changed during patch preparation: "
                f"expected {expected_commit}, found {final_upstream_commit}"
            )
        if not _clean(upstream):
            raise ValueError("pinned upstream became dirty during patch preparation")

    record: dict[str, object] = {
        "upstream_path": str(upstream),
        "destination_path": str(destination),
        "patch_directory": str(_patch_root(spec)),
        "branch": spec.branch,
        "base_commit": expected_commit,
        "upstream_commit": upstream_commit,
        "patch_commit": patch_commit,
        "upstream_tree": _git(upstream, "rev-parse", f"{expected_commit}^{{tree}}"),
        "derived_tree": _git(destination, "rev-parse", f"{patch_commit}^{{tree}}"),
        "applied_patches": [patch.name for patch in patches],
        "patches": _patch_evidence(patches),
        "dirty": False,
    }
    validate_patch_identity(record, expected_commit)
    _revalidate_controlled_patches(patches, spec)
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
    parser.add_argument(
        "--family",
        choices=tuple(sorted(FAMILY_SPECS)),
        default="openvino-genai-turboquant",
    )
    parser.add_argument("--upstream", type=Path, required=True)
    parser.add_argument("--destination", type=Path, required=True)
    parser.add_argument("--expected-commit", required=True)
    parser.add_argument("--evidence", type=Path, required=True)
    args = parser.parse_args()
    record = prepare_patch_workspace(
        args.upstream,
        args.destination,
        args.expected_commit,
        spec_for_family(args.family),
    )
    args.evidence.parent.mkdir(parents=True, exist_ok=True)
    args.evidence.write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    print(f"Prepared patch workspace: {args.destination.resolve()}")
    print(f"Identity evidence: {args.evidence.resolve()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
