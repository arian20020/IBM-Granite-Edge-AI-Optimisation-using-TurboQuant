"""Resolve and retain immutable Workbook 05 model snapshots."""

from __future__ import annotations

import os
import re
import stat
from dataclasses import dataclass
from pathlib import Path, PurePosixPath, PureWindowsPath
from typing import Any, Protocol, Sequence


# C1 permits exactly one formal model repository. A separate diagnostic
# repository can be supplied explicitly by the caller, but it is never inferred.
FORMAL_GRANITE_REPOSITORY = "ibm-granite/granite-4.1-3b"

# A Hub commit is represented by the full lowercase forty-character Git SHA.
_FULL_COMMIT_PATTERN = re.compile(r"^[0-9a-f]{40}$")

# `snapshot_download(local_dir=...)` creates this library-owned metadata subtree.
# It is not part of the model payload catalogue returned by `model_info`.
_HUB_METADATA_PREFIX = ".cache/huggingface"


class HubApi(Protocol):
    """Narrow injected boundary used instead of importing the live client globally."""

    def model_info(self, repo_id: str, revision: str) -> Any:
        """Return Hub model metadata for one requested revision."""

    def snapshot_download(
        self,
        *,
        repo_id: str,
        revision: str,
        local_dir: str,
        allow_patterns: list[str],
    ) -> str:
        """Materialise exactly the requested files beneath `local_dir`."""


@dataclass(frozen=True, slots=True)
class HubFile:
    """One canonical repository-relative file advertised by the Hub."""

    relative_path: str


@dataclass(frozen=True, slots=True)
class ResolvedModel:
    """A moving model request bound to one immutable revision and file catalogue."""

    repository: str
    requested_revision: str
    resolved_revision: str
    siblings: tuple[HubFile, ...]


def _validate_repository(
    repository: str,
    diagnostic_repository: str | None,
) -> None:
    """Reject every repository outside the formal or explicit diagnostic scope."""

    if not isinstance(repository, str) or not repository:
        raise ValueError("The model repository must be a non-empty string.")

    approved = {FORMAL_GRANITE_REPOSITORY}
    if diagnostic_repository is not None:
        if not isinstance(diagnostic_repository, str) or not diagnostic_repository:
            raise ValueError("The diagnostic repository must be a non-empty string.")
        if diagnostic_repository == FORMAL_GRANITE_REPOSITORY:
            raise ValueError(
                "The diagnostic repository must remain separate from the formal repository."
            )
        approved.add(diagnostic_repository)

    if repository not in approved:
        raise ValueError(f"The model repository is not approved for C1: {repository}")


def _validate_requested_revision(requested_revision: str) -> None:
    """Require an explicit request so provenance never depends on a hidden default."""

    if not isinstance(requested_revision, str) or not requested_revision.strip():
        raise ValueError("The requested model revision must be a non-empty string.")
    if requested_revision != requested_revision.strip():
        raise ValueError(
            "The requested model revision must not contain surrounding whitespace."
        )
    if any(ord(character) < 32 for character in requested_revision):
        raise ValueError(
            "The requested model revision must not contain control characters."
        )


def _canonical_hub_file_path(value: Any) -> str:
    """Return one portable relative POSIX file path or fail closed."""

    if not isinstance(value, str) or not value:
        raise ValueError("Hub file path must be a non-empty string.")
    if value != value.strip():
        raise ValueError(
            f"Hub file path must not contain surrounding whitespace: {value!r}"
        )
    if "\\" in value or "\x00" in value:
        raise ValueError(
            f"Hub file path must use portable POSIX separators: {value!r}"
        )
    if any(ord(character) < 32 for character in value):
        raise ValueError(
            f"Hub file path must not contain control characters: {value!r}"
        )
    if value.startswith("/") or value.endswith("/") or "//" in value:
        raise ValueError(
            f"Hub file path must be a relative file path: {value!r}"
        )

    # PurePosixPath normalises `.` segments, so inspect the original segments
    # first and reject any spelling whose meaning would change during parsing.
    raw_parts = value.split("/")
    if any(part in {"", ".", ".."} for part in raw_parts):
        raise ValueError(f"Hub file path contains an unsafe segment: {value!r}")

    posix_path = PurePosixPath(value)
    windows_path = PureWindowsPath(value)
    if (
        posix_path.is_absolute()
        or windows_path.is_absolute()
        or windows_path.drive
    ):
        raise ValueError(
            f"Hub file path must not be absolute or drive-qualified: {value!r}"
        )
    if len(posix_path.parts) == 0:
        raise ValueError(f"Hub file path is empty after parsing: {value!r}")

    canonical = posix_path.as_posix()
    if canonical != value:
        raise ValueError(f"Hub file path is not canonical: {value!r}")
    if (
        canonical.casefold() == _HUB_METADATA_PREFIX.casefold()
        or canonical.casefold().startswith(
            _HUB_METADATA_PREFIX.casefold() + "/"
        )
    ):
        raise ValueError(
            "Hub file path collides with the library-owned local metadata subtree: "
            f"{value!r}"
        )
    return canonical


def _resolve_siblings(raw_siblings: Any) -> tuple[HubFile, ...]:
    """Validate and deterministically sort the Hub file catalogue."""

    if not isinstance(raw_siblings, Sequence) or isinstance(
        raw_siblings,
        (str, bytes),
    ):
        raise ValueError("Hub model metadata must contain a sibling sequence.")
    if len(raw_siblings) == 0:
        raise ValueError(
            "Hub model metadata contains an empty sibling catalogue."
        )

    canonical_files: list[HubFile] = []
    casefolded_paths: set[str] = set()
    for sibling in raw_siblings:
        relative_path = _canonical_hub_file_path(
            getattr(sibling, "rfilename", None)
        )
        windows_identity = relative_path.casefold()
        if windows_identity in casefolded_paths:
            raise ValueError(
                "Hub sibling catalogue contains a duplicate Windows path identity: "
                f"{relative_path}"
            )
        casefolded_paths.add(windows_identity)
        canonical_files.append(HubFile(relative_path=relative_path))

    return tuple(
        sorted(
            canonical_files,
            key=lambda item: (
                item.relative_path.casefold(),
                item.relative_path,
            ),
        )
    )


def resolve_model(
    api: HubApi,
    repository: str,
    requested_revision: str,
    *,
    diagnostic_repository: str | None = None,
) -> ResolvedModel:
    """Resolve one approved model request to a full immutable Hub commit."""

    _validate_repository(repository, diagnostic_repository)
    _validate_requested_revision(requested_revision)

    # The injected adapter receives the moving request exactly once. The result
    # is accepted only when the Hub returns a full immutable commit and catalogue.
    info = api.model_info(
        repo_id=repository,
        revision=requested_revision,
    )
    resolved_revision = getattr(info, "sha", None)
    if not isinstance(resolved_revision, str) or not _FULL_COMMIT_PATTERN.fullmatch(
        resolved_revision
    ):
        raise ValueError(
            "The Hub response must contain a full lowercase forty-character "
            "resolved revision."
        )

    siblings = _resolve_siblings(getattr(info, "siblings", None))
    return ResolvedModel(
        repository=repository,
        requested_revision=requested_revision,
        resolved_revision=resolved_revision,
        siblings=siblings,
    )


def _is_link_or_reparse(path: Path) -> bool:
    """Return true for a symbolic link, junction, or other reparse point."""

    try:
        if path.is_symlink():
            return True
        attributes = getattr(path.lstat(), "st_file_attributes", 0)
    except FileNotFoundError:
        return False
    except OSError as error:
        raise ValueError(
            f"Unable to inspect local asset path safely: {path}: {error}"
        ) from error

    reparse_flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)
    return bool(attributes & reparse_flag)


def _assert_safe_new_destination(destination: Path) -> Path:
    """Validate one absent destination beneath an existing normal local parent."""

    text = str(destination)
    lowered = text.casefold()
    if lowered.startswith("\\\\?\\") or lowered.startswith("\\\\.\\"):
        raise ValueError(
            f"Snapshot destination must not be a Windows device path: {text}"
        )
    if text.startswith("\\\\"):
        raise ValueError(f"Snapshot destination must not be a UNC path: {text}")
    if not destination.is_absolute():
        raise ValueError(f"Snapshot destination must be absolute: {text}")
    if destination.exists() or destination.is_symlink():
        raise ValueError(
            f"Snapshot destination must not already exist: {text}"
        )

    parent = destination.parent
    if not parent.exists() or not parent.is_dir():
        raise ValueError(
            "Snapshot destination parent must be an existing directory: "
            f"{parent}"
        )

    # Walk from the parent to the filesystem root and reject every link/reparse
    # boundary. This prevents an approved lexical path from being redirected.
    current = parent
    while True:
        if _is_link_or_reparse(current):
            raise ValueError(
                f"Snapshot destination parent chain is unsafe: {current}"
            )
        if current.parent == current:
            break
        current = current.parent

    try:
        return parent.resolve(strict=True) / destination.name
    except OSError as error:
        raise ValueError(
            f"Unable to resolve snapshot destination parent: {parent}: {error}"
        ) from error


def _enumerate_snapshot_payload(destination: Path) -> tuple[str, ...]:
    """List regular payload files without following links or metadata entries."""

    if _is_link_or_reparse(destination):
        raise ValueError(
            "Downloaded snapshot directory is a link or reparse point: "
            f"{destination}"
        )
    if not destination.is_dir():
        raise ValueError(
            f"Downloaded snapshot directory is missing: {destination}"
        )

    payload_files: list[str] = []
    pending = [destination]
    while pending:
        current = pending.pop()
        try:
            with os.scandir(current) as iterator:
                entries = sorted(
                    iterator,
                    key=lambda item: item.name.casefold(),
                )
        except OSError as error:
            raise ValueError(
                f"Unable to inventory downloaded snapshot: {current}: {error}"
            ) from error

        for entry in entries:
            entry_path = Path(entry.path)
            if _is_link_or_reparse(entry_path):
                raise ValueError(
                    "Downloaded snapshot contains a link or reparse point: "
                    f"{entry_path}"
                )

            relative = entry_path.relative_to(destination).as_posix()
            in_hub_metadata = (
                relative.casefold() == _HUB_METADATA_PREFIX.casefold()
                or relative.casefold().startswith(
                    _HUB_METADATA_PREFIX.casefold() + "/"
                )
            )

            if entry.is_dir(follow_symlinks=False):
                # Traverse metadata as well so it cannot hide a redirected
                # filesystem boundary. Its regular files are not model payload.
                pending.append(entry_path)
                continue
            if entry.is_file(follow_symlinks=False):
                if not in_hub_metadata:
                    payload_files.append(
                        _canonical_hub_file_path(relative)
                    )
                continue
            raise ValueError(
                "Downloaded snapshot contains a non-regular entry: "
                f"{entry_path}"
            )

    return tuple(
        sorted(
            payload_files,
            key=lambda value: (value.casefold(), value),
        )
    )


def download_snapshot(
    api: HubApi,
    model: ResolvedModel,
    destination: Path,
) -> tuple[Path, ...]:
    """Download and verify exactly the resolved model's advertised file set."""

    approved_destination = _assert_safe_new_destination(destination)
    allow_patterns = [item.relative_path for item in model.siblings]

    # Pass structured arguments to the injected Hub boundary. The full resolved
    # commit—not the original moving reference—controls acquisition.
    returned_directory = Path(
        api.snapshot_download(
            repo_id=model.repository,
            revision=model.resolved_revision,
            local_dir=str(approved_destination),
            allow_patterns=allow_patterns,
        )
    )

    try:
        returned_resolved = returned_directory.resolve(strict=True)
        destination_resolved = approved_destination.resolve(strict=True)
    except OSError as error:
        raise ValueError(
            f"Downloaded snapshot directory could not be resolved: {error}"
        ) from error
    if os.path.normcase(str(returned_resolved)) != os.path.normcase(
        str(destination_resolved)
    ):
        raise ValueError(
            "Hub adapter returned an unexpected directory. "
            f"Expected {destination_resolved}, found {returned_resolved}."
        )

    actual_files = _enumerate_snapshot_payload(destination_resolved)
    expected_files = tuple(item.relative_path for item in model.siblings)
    expected_identity = {
        path.casefold(): path
        for path in expected_files
    }
    actual_identity = {
        path.casefold(): path
        for path in actual_files
    }

    missing = [
        expected_identity[key]
        for key in sorted(expected_identity.keys() - actual_identity.keys())
    ]
    unexpected = [
        actual_identity[key]
        for key in sorted(actual_identity.keys() - expected_identity.keys())
    ]
    if missing:
        raise ValueError(
            "Downloaded snapshot is missing expected files: "
            + ", ".join(missing)
        )
    if unexpected:
        raise ValueError(
            "Downloaded snapshot contains unexpected files: "
            + ", ".join(unexpected)
        )

    # Return paths in the same deterministic order as the resolved catalogue.
    return tuple(
        destination_resolved.joinpath(*item.relative_path.split("/"))
        for item in model.siblings
    )
