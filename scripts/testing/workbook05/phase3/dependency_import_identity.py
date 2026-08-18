"""Describe imported modules without assuming every package has ``__file__``.

The reviewed Optimum and Optimum Intel distributions share a PEP 420 namespace
package. Namespace packages expose one or more search locations through their
module specification instead of a concrete ``__file__``. This helper keeps both
live dependency checks on one fail-closed, lexical Windows identity boundary.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path, PureWindowsPath
from typing import Any, Iterable


class ImportIdentityError(ValueError):
    """Raised when an imported module cannot be bound to the final environment."""


@dataclass(frozen=True, slots=True)
class ModuleImportIdentity:
    """A deterministic conventional-file or namespace-package identity."""

    kind: str
    locations: tuple[str, ...]

    @property
    def primary_location(self) -> str:
        """Return the compatibility path retained by the existing evidence format."""

        return self.locations[0]


def _windows_child(
    root: Path,
    candidate_text: str,
    *,
    module_name: str,
) -> str:
    """Validate one imported location lexically without probing the filesystem."""

    root_text = str(root)
    lowered_root = root_text.casefold()
    lowered_candidate = candidate_text.casefold()

    # UNC and Windows device paths can bypass the intended local-drive boundary.
    if root_text.startswith("\\\\") or lowered_root.startswith(("\\\\?\\", "\\\\.\\")):
        raise ImportIdentityError(
            f"The expected final environment must not be a UNC or device path: {root}"
        )
    if candidate_text.startswith("\\\\") or lowered_candidate.startswith(
        ("\\\\?\\", "\\\\.\\")
    ):
        raise ImportIdentityError(
            f"Imported module {module_name} escaped outside the supplied environment: "
            f"{candidate_text}"
        )

    root_path = PureWindowsPath(root_text)
    candidate = PureWindowsPath(candidate_text)
    if not root_path.is_absolute() or not root_path.drive:
        raise ImportIdentityError(
            f"The expected final environment must be an absolute Windows path: {root}"
        )
    if not candidate.is_absolute() or not candidate.drive:
        raise ImportIdentityError(
            f"Imported module path must be an absolute Windows path: {module_name}"
        )
    if ".." in candidate.parts or "." in candidate.parts:
        raise ImportIdentityError(
            f"Imported module {module_name} escaped outside the supplied environment: "
            f"{candidate_text}"
        )
    if root_path.drive.casefold() != candidate.drive.casefold():
        raise ImportIdentityError(
            f"Imported module {module_name} escaped outside the supplied environment: "
            f"{candidate_text}"
        )

    root_parts = tuple(part.casefold() for part in root_path.parts)
    candidate_parts = tuple(part.casefold() for part in candidate.parts)
    if (
        len(candidate_parts) <= len(root_parts)
        or candidate_parts[: len(root_parts)] != root_parts
    ):
        raise ImportIdentityError(
            f"Imported module {module_name} escaped outside the supplied environment: "
            f"{candidate_text}"
        )
    return str(candidate)


def _namespace_locations(module: Any, module_name: str) -> tuple[Any, ...]:
    """Read the namespace search catalogue without importing another module."""

    specification = getattr(module, "__spec__", None)
    raw_locations = getattr(specification, "submodule_search_locations", None)
    if raw_locations is None:
        # ``__path__`` is a standards-compatible fallback for synthetic modules
        # and older import implementations that omit the specification field.
        raw_locations = getattr(module, "__path__", None)
    if raw_locations is None:
        raise ImportIdentityError(
            f"Imported module has no file or namespace identity: {module_name}"
        )
    if isinstance(raw_locations, (str, bytes)):
        raise ImportIdentityError(
            f"Imported namespace package has an invalid location catalogue: {module_name}"
        )
    try:
        return tuple(raw_locations)
    except TypeError as error:
        raise ImportIdentityError(
            f"Imported namespace package has an invalid location catalogue: {module_name}"
        ) from error


def observe_module_identity(
    module: Any,
    environment_root: Path,
    module_name: str,
) -> ModuleImportIdentity:
    """Bind a conventional module file or every PEP 420 namespace location."""

    module_file = getattr(module, "__file__", None)
    if isinstance(module_file, str) and module_file:
        contained_file = _windows_child(
            environment_root,
            module_file,
            module_name=module_name,
        )
        return ModuleImportIdentity(kind="file", locations=(contained_file,))
    if module_file not in (None, ""):
        raise ImportIdentityError(
            f"Imported module has an invalid file identity: {module_name}"
        )

    raw_locations = _namespace_locations(module, module_name)
    if not raw_locations:
        raise ImportIdentityError(
            f"Imported namespace package has no import location: {module_name}"
        )

    # Validate and canonicalise every namespace contribution. Multiple package
    # distributions may contribute to one namespace, so all locations—not only
    # the first—must remain inside the separately qualified environment.
    observed: list[str] = []
    canonical: set[str] = set()
    for raw_location in raw_locations:
        if not isinstance(raw_location, str) or not raw_location:
            raise ImportIdentityError(
                f"Imported namespace package has an invalid location: {module_name}"
            )
        contained_location = _windows_child(
            environment_root,
            raw_location,
            module_name=module_name,
        )
        folded = contained_location.casefold()
        if folded in canonical:
            raise ImportIdentityError(
                f"Imported namespace package repeats a location: {module_name}"
            )
        canonical.add(folded)
        observed.append(contained_location)

    # Search-path order can depend on installation mechanics. Sorting the fully
    # validated paths makes retained JSON deterministic without weakening checks.
    observed.sort(key=str.casefold)
    return ModuleImportIdentity(kind="namespace", locations=tuple(observed))
