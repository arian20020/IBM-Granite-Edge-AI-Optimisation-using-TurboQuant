"""Collect read-only Workbook 05 Phase 3 disk and workspace evidence."""

from __future__ import annotations

import os
import re
import stat
from pathlib import Path, PureWindowsPath
from typing import Any

CAMPAIGN_ID = "GTQ-WB05-MF-v1"
MINIMUM_FREE_BYTES_BEFORE_GRANITE_3B = 53_687_091_200

# A workspace name such as ``phase2-31661571860-1`` contains the immutable
# GitHub Actions run ID and run attempt at the end of the directory name.
_RUN_IDENTITY_PATTERN = re.compile(
    r"^(?P<prefix>[A-Za-z0-9][A-Za-z0-9._-]*?)-"
    r"(?P<run_id>[0-9]{8,})-(?P<run_attempt>[1-9][0-9]*)$"
)


def _assert_safe_absolute_reference(path: Path, label: str) -> None:
    """Reject path forms that cannot represent one controlled local root."""

    text = str(path)
    lowered = text.casefold()
    if lowered.startswith("\\\\?\\") or lowered.startswith("\\\\.\\"):
        raise ValueError(f"{label} must not be a Windows device path: {text}")
    if text.startswith("\\\\"):
        raise ValueError(f"{label} must not be a UNC path: {text}")

    # Parse with both the host and Windows path rules. This keeps the function
    # testable on hosted runners while preserving the Windows-only live policy.
    if ".." in path.parts or ".." in PureWindowsPath(text).parts:
        raise ValueError(f"{label} must not contain parent traversal: {text}")
    if not path.is_absolute():
        raise ValueError(f"{label} must be absolute: {text}")


def _is_link_or_reparse(path: Path) -> bool:
    """Return true for a symbolic link, junction, or other reparse point."""

    if path.is_symlink():
        return True
    try:
        attributes = getattr(path.lstat(), "st_file_attributes", 0)
    except FileNotFoundError:
        return False
    except OSError as error:
        raise ValueError(
            f"Unable to inspect storage path safely: {path}: {error}"
        ) from error

    reparse_flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)
    return bool(attributes & reparse_flag)


def _entry_is_reparse(entry: os.DirEntry[str]) -> bool:
    """Inspect one directory entry without following it."""

    attributes = getattr(
        entry.stat(follow_symlinks=False),
        "st_file_attributes",
        0,
    )
    reparse_flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)
    return entry.is_symlink() or bool(attributes & reparse_flag)


def _new_inventory(role: str, root: Path) -> dict[str, Any]:
    """Create the explicit zero-state used for both absent and failed roots."""

    return {
        "root_role": role,
        "path": str(root),
        "exists": False,
        "file_count": 0,
        "directory_count": 0,
        "total_bytes": 0,
        "unsafe_entries": [],
    }


def _inventory_root(
    role: str,
    root: Path,
) -> tuple[dict[str, Any], list[Path], list[dict[str, str]]]:
    """Inventory one root without creating, changing, or deleting a path."""

    inventory = _new_inventory(role, root)
    direct_directories: list[Path] = []
    issues: list[dict[str, str]] = []

    # A broken symbolic link is not ``exists()``, so inspect link identity first.
    try:
        root_is_unsafe = _is_link_or_reparse(root)
    except ValueError as error:
        issues.append(
            {
                "failure_id": "ROOT_INSPECTION_FAILED",
                "root_role": role,
                "path": str(root),
                "reason": str(error),
            }
        )
        return inventory, direct_directories, issues

    if root_is_unsafe:
        inventory["exists"] = root.exists()
        inventory["unsafe_entries"].append(str(root))
        issues.append(
            {
                "failure_id": "ROOT_REPARSE_POINT",
                "root_role": role,
                "path": str(root),
                "reason": (
                    "The controlled storage root is a link or reparse point."
                ),
            }
        )
        return inventory, direct_directories, issues

    if not root.exists():
        # Missing future roots are observations. C1 never creates them here.
        return inventory, direct_directories, issues

    inventory["exists"] = True
    if not root.is_dir():
        issues.append(
            {
                "failure_id": "ROOT_NOT_DIRECTORY",
                "root_role": role,
                "path": str(root),
                "reason": "The controlled storage root is not a directory.",
            }
        )
        return inventory, direct_directories, issues

    # The depth marker lets us retain only direct child directories as possible
    # run workspaces while still recursively measuring every regular file.
    pending: list[tuple[Path, int]] = [(root, 0)]
    while pending:
        current, depth = pending.pop()
        try:
            with os.scandir(current) as iterator:
                entries = sorted(
                    iterator,
                    key=lambda item: item.name.casefold(),
                )
        except OSError as error:
            issues.append(
                {
                    "failure_id": "ROOT_INVENTORY_FAILED",
                    "root_role": role,
                    "path": str(current),
                    "reason": f"Unable to enumerate directory: {error}",
                }
            )
            continue

        for entry in entries:
            entry_path = Path(entry.path)
            try:
                if _entry_is_reparse(entry):
                    inventory["unsafe_entries"].append(str(entry_path))
                    issues.append(
                        {
                            "failure_id": "UNSAFE_STORAGE_ENTRY",
                            "root_role": role,
                            "path": str(entry_path),
                            "reason": "A link or reparse point was not followed.",
                        }
                    )
                    continue

                if entry.is_file(follow_symlinks=False):
                    inventory["file_count"] += 1
                    inventory["total_bytes"] += entry.stat(
                        follow_symlinks=False
                    ).st_size
                    continue

                if entry.is_dir(follow_symlinks=False):
                    inventory["directory_count"] += 1
                    if depth == 0:
                        direct_directories.append(entry_path)
                    pending.append((entry_path, depth + 1))
                    continue

                # Device nodes and other non-regular entries are not safe
                # evidence inputs and are therefore recorded but not consumed.
                inventory["unsafe_entries"].append(str(entry_path))
                issues.append(
                    {
                        "failure_id": "UNSAFE_STORAGE_ENTRY",
                        "root_role": role,
                        "path": str(entry_path),
                        "reason": (
                            "A non-regular storage entry was not consumed."
                        ),
                    }
                )
            except OSError as error:
                issues.append(
                    {
                        "failure_id": "ROOT_INVENTORY_FAILED",
                        "root_role": role,
                        "path": str(entry_path),
                        "reason": f"Unable to inspect entry: {error}",
                    }
                )

    inventory["unsafe_entries"].sort(key=str.casefold)
    direct_directories.sort(key=lambda item: str(item).casefold())
    return inventory, direct_directories, issues


def _candidate_workspace(
    role: str,
    directory: Path,
) -> dict[str, Any] | None:
    """Return review-only metadata when a directory ends in a run identity."""

    match = _RUN_IDENTITY_PATTERN.fullmatch(directory.name)
    if match is None:
        return None

    return {
        "root_role": role,
        "workspace_name": directory.name,
        "path": str(directory),
        "run_id": match.group("run_id"),
        "run_attempt": int(match.group("run_attempt")),
        "staleness_confirmed": False,
        "owner_review_required": True,
        "automatic_deletion_authorised": False,
    }


def collect_disk_preflight(
    model_root: Path,
    probe_root: Path,
    run_root: Path,
    free_bytes: int,
) -> dict[str, Any]:
    """Return a read-only disk decision for Granite 4.1 3B acquisition.

    The function inventories four controlled roots and identifies directories
    that look like prior workflow workspaces. It never creates, repairs, reuses,
    or deletes any filesystem object.
    """

    if isinstance(free_bytes, bool) or not isinstance(free_bytes, int):
        raise TypeError("free_bytes must be an integer byte count.")
    if free_bytes < 0:
        raise ValueError("free_bytes must not be negative.")

    # C:\w5a is the accepted-build sibling of the model root C:\w5m. Deriving
    # it keeps the public interface fixed to the approved Task 4 contract.
    accepted_root = model_root.parent / "w5a"
    roots = {
        "accepted_root": accepted_root,
        "model_root": model_root,
        "probe_root": probe_root,
        "run_root": run_root,
    }
    for role, root in roots.items():
        _assert_safe_absolute_reference(root, role)

    inventories: dict[str, dict[str, Any]] = {}
    candidates: list[dict[str, Any]] = []
    inventory_issues: list[dict[str, str]] = []

    # Scan each root independently. An absent root remains an explicit zero
    # observation; an unsafe or unreadable root blocks the preflight.
    for role, root in roots.items():
        inventory, direct_directories, issues = _inventory_root(role, root)
        inventories[role] = inventory
        inventory_issues.extend(issues)
        for directory in direct_directories:
            candidate = _candidate_workspace(role, directory)
            if candidate is not None:
                candidates.append(candidate)

    candidates.sort(
        key=lambda item: (
            str(item["root_role"]).casefold(),
            str(item["path"]).casefold(),
        )
    )
    inventory_issues.sort(
        key=lambda item: (
            item["failure_id"],
            item["root_role"],
            item["path"].casefold(),
        )
    )

    failure_ids = sorted(
        {issue["failure_id"] for issue in inventory_issues}
    )
    reasons = [issue["reason"] for issue in inventory_issues]

    # The threshold is inclusive: exactly 50 GiB is sufficient.
    if free_bytes < MINIMUM_FREE_BYTES_BEFORE_GRANITE_3B:
        failure_ids.append("DISK_BELOW_50_GIB")
        reasons.append(
            "Drive C free space is below the required 50 GiB "
            f"({free_bytes} < {MINIMUM_FREE_BYTES_BEFORE_GRANITE_3B})."
        )

    failure_ids = sorted(set(failure_ids))
    status = "Passed" if not failure_ids else "Blocked"

    return {
        "schema_version": "1.0",
        "campaign_id": CAMPAIGN_ID,
        "record_type": "disk-preflight",
        "status": status,
        "failure_ids": failure_ids,
        "reasons": reasons,
        "free_bytes": free_bytes,
        "minimum_free_bytes_before_granite_3b": (
            MINIMUM_FREE_BYTES_BEFORE_GRANITE_3B
        ),
        "inventories": inventories,
        "candidate_stale_workspaces": candidates,
        "deletion_authorised": False,
        "deletion_performed": False,
        "granite_3b_download_authorised": status == "Passed",
        "granite_8b_download_authorised": False,
    }
