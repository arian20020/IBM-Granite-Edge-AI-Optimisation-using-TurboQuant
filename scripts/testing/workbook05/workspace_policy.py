"""Pure policy checks for the Workbook 05 external Windows workspace."""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import PureWindowsPath


@dataclass(frozen=True)
class WorkspaceDecision:
    """The deterministic result of checking one requested Windows path."""

    permitted: bool
    canonical_path: str
    reasons: tuple[str, ...]


def evaluate_workspace_path(
    requested: PureWindowsPath,
    allowed_root: PureWindowsPath = PureWindowsPath("C:/wb05"),
) -> WorkspaceDecision:
    """Allow only the controlled root or one of its descendants.

    The function is deliberately pure. Filesystem checks such as reparse-point
    detection are performed by the PowerShell module on the Windows runner.
    """

    requested_text = str(requested)
    allowed_text = str(allowed_root)
    reasons: list[str] = []

    # Device and UNC paths have different trust semantics from a local drive.
    lowered = requested_text.lower()
    if lowered.startswith("\\\\?\\") or lowered.startswith("\\\\.\\"):
        reasons.append("Windows device paths are not allowed.")
    if requested.anchor.startswith("\\\\"):
        reasons.append("UNC paths are not allowed.")

    # Parent traversal is rejected before any lexical normalisation can hide it.
    if any(part == ".." for part in requested.parts):
        reasons.append("Parent traversal is not allowed.")

    if not requested.is_absolute():
        reasons.append("The workspace path must be absolute.")

    if requested.drive.casefold() != allowed_root.drive.casefold():
        reasons.append(
            f"The workspace must stay on drive {allowed_root.drive}; "
            f"found {requested.drive or 'no drive'}."
        )

    requested_parts = requested.parts
    allowed_parts = allowed_root.parts
    if len(requested_parts) < len(allowed_parts) or any(
        actual.casefold() != expected.casefold()
        for actual, expected in zip(requested_parts, allowed_parts)
    ):
        reasons.append(f"The workspace must be {allowed_text} or its child.")

    if reasons:
        return WorkspaceDecision(False, requested_text, tuple(reasons))

    # Rebuild the path from the frozen root so casing and separators are stable.
    remaining_parts = requested_parts[len(allowed_parts) :]
    canonical = PureWindowsPath(allowed_root, *remaining_parts)
    return WorkspaceDecision(True, str(canonical), ())
