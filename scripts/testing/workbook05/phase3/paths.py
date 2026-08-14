"""Fail-closed path primitives for Workbook 05 Phase 3 evidence."""

from __future__ import annotations

import os
import stat
from pathlib import Path, PureWindowsPath


def _raw_path_text(path: Path) -> str:
    return str(path)


def _reject_unsafe_path_text(path: Path) -> None:
    """Reject Windows device, UNC, and explicit parent-traversal paths early."""

    text = _raw_path_text(path)
    lowered = text.casefold()
    if lowered.startswith("\\\\?\\") or lowered.startswith("\\\\.\\"):
        raise ValueError(f"Windows device paths are not allowed: {text}")
    if text.startswith("\\\\"):
        raise ValueError(f"UNC paths are not allowed: {text}")

    # Check both native and Windows parsing so a Windows path is still rejected
    # correctly when the repository tests run on a non-Windows hosted runner.
    native_parts = path.parts
    windows_parts = PureWindowsPath(text).parts
    if ".." in native_parts or ".." in windows_parts:
        raise ValueError(f"Parent traversal is not allowed: {text}")


def _looks_like_windows_path(path: Path) -> bool:
    text = _raw_path_text(path)
    pure = PureWindowsPath(text)
    return bool(pure.drive) or "\\" in text


def _assert_windows_lexical_containment(
    path: Path,
    approved_root: Path,
    *,
    allow_root: bool,
) -> None:
    """Reject sibling-prefix and cross-drive Windows paths before I/O."""

    path_text = _raw_path_text(path)
    root_text = _raw_path_text(approved_root)
    if not (_looks_like_windows_path(path) or _looks_like_windows_path(approved_root)):
        return

    candidate = PureWindowsPath(path_text)
    root = PureWindowsPath(root_text)
    if not candidate.is_absolute():
        raise ValueError(f"The path must be absolute: {path_text}")
    if not root.is_absolute():
        raise ValueError(f"The approved root must be absolute: {root_text}")
    if candidate.drive.casefold() != root.drive.casefold():
        raise ValueError(
            f"The path must stay on approved root drive {root.drive}: {path_text}"
        )

    candidate_parts = candidate.parts
    root_parts = root.parts
    is_root_or_child = len(candidate_parts) >= len(root_parts) and all(
        actual.casefold() == expected.casefold()
        for actual, expected in zip(candidate_parts, root_parts)
    )
    if not is_root_or_child:
        raise ValueError(
            f"The path must be the approved root or one of its children: {path_text}"
        )
    if len(candidate_parts) == len(root_parts) and not allow_root:
        raise ValueError(f"The approved root itself is not permitted here: {path_text}")


def _is_link_or_reparse(path: Path) -> bool:
    """Return true for symbolic links, junctions, and other reparse points."""

    try:
        if path.is_symlink():
            return True
        attributes = getattr(path.lstat(), "st_file_attributes", 0)
    except OSError as error:
        raise ValueError(f"Unable to inspect path safely: {path}: {error}") from error

    reparse_flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)
    return bool(attributes & reparse_flag)


def _commonpath_is_root(root: Path, candidate: Path) -> bool:
    try:
        common = Path(os.path.commonpath([str(root), str(candidate)]))
    except ValueError:
        return False
    return os.path.normcase(str(common)) == os.path.normcase(str(root))


def _assert_no_link_or_reparse_chain(root: Path, candidate: Path) -> None:
    """Inspect the approved root and every existing child component lexically."""

    root_absolute = root.absolute()
    candidate_absolute = candidate.absolute()
    if not _commonpath_is_root(root_absolute, candidate_absolute):
        return

    if _is_link_or_reparse(root_absolute):
        raise ValueError(f"The approved root must not be a link or reparse point: {root}")

    try:
        relative_parts = candidate_absolute.relative_to(root_absolute).parts
    except ValueError:
        return

    current = root_absolute
    for part in relative_parts:
        current = current / part
        if current.exists() or current.is_symlink():
            if _is_link_or_reparse(current):
                raise ValueError(f"The path must not contain a link or reparse point: {current}")


def _resolve_approved_root(approved_root: Path) -> Path:
    _reject_unsafe_path_text(approved_root)
    if _looks_like_windows_path(approved_root) and os.name != "nt":
        # A valid Windows path cannot be resolved on this host. The caller's
        # candidate-specific lexical checks still run first, which lets hosted
        # repository tests prove rejection behavior without inventing a path.
        raise ValueError(f"The approved root does not exist on this host: {approved_root}")
    if not approved_root.is_absolute():
        raise ValueError(f"The approved root must be absolute: {approved_root}")
    if _is_link_or_reparse(approved_root):
        raise ValueError(
            f"The approved root must not be a link or reparse point: {approved_root}"
        )
    try:
        resolved = approved_root.resolve(strict=True)
    except OSError as error:
        raise ValueError(f"The approved root does not exist: {approved_root}") from error
    if not resolved.is_dir():
        raise ValueError(f"The approved root is not a directory: {approved_root}")
    return resolved


def assert_normal_local_directory(
    path: Path,
    approved_root: Path,
    allow_root: bool = False,
) -> Path:
    """Return an existing normal local directory contained by an approved root."""

    _reject_unsafe_path_text(path)
    _reject_unsafe_path_text(approved_root)
    _assert_windows_lexical_containment(path, approved_root, allow_root=allow_root)

    if _looks_like_windows_path(path) and os.name != "nt":
        # Invalid Windows candidates have already failed the lexical check.
        raise ValueError(f"The requested directory does not exist on this host: {path}")
    if not path.is_absolute():
        raise ValueError(f"The requested directory must be absolute: {path}")

    _assert_no_link_or_reparse_chain(approved_root, path)
    root = _resolve_approved_root(approved_root)
    if _is_link_or_reparse(path):
        raise ValueError(f"The path must not be a link or reparse point: {path}")
    try:
        candidate = path.resolve(strict=True)
    except OSError as error:
        raise ValueError(f"The requested directory does not exist: {path}") from error
    if not candidate.is_dir():
        raise ValueError(f"The requested path is not a directory: {path}")
    if not _commonpath_is_root(root, candidate):
        raise ValueError(
            f"The requested directory is outside the approved root {root}: {candidate}"
        )
    if os.path.normcase(str(candidate)) == os.path.normcase(str(root)) and not allow_root:
        raise ValueError(f"The approved root itself is not permitted here: {candidate}")
    return candidate


def relative_evidence_path(root: Path, path: Path) -> str:
    """Return a stable forward-slash path for existing evidence below root."""

    _reject_unsafe_path_text(root)
    _reject_unsafe_path_text(path)
    if not root.is_absolute() or not path.is_absolute():
        raise ValueError("Evidence root and path must both be absolute.")

    _assert_no_link_or_reparse_chain(root, path)
    resolved_root = _resolve_approved_root(root)
    if _is_link_or_reparse(path):
        raise ValueError(f"Evidence must not be a link or reparse point: {path}")
    try:
        resolved_path = path.resolve(strict=True)
    except OSError as error:
        raise ValueError(f"Evidence path does not exist: {path}") from error
    if not _commonpath_is_root(resolved_root, resolved_path):
        raise ValueError(
            f"Evidence path is outside the evidence root {resolved_root}: {resolved_path}"
        )
    if os.path.normcase(str(resolved_path)) == os.path.normcase(str(resolved_root)):
        raise ValueError("The evidence root itself is not an evidence file path.")
    return resolved_path.relative_to(resolved_root).as_posix()
