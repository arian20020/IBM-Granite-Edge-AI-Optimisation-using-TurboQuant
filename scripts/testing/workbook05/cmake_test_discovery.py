"""Audit the exact Route B target-per-test CMake source-collection pattern."""

from __future__ import annotations

import re
from dataclasses import dataclass
from pathlib import Path
from typing import Sequence


_ARCH_VARIABLE = "LIST_OF_TEST_ARCH_INSTANCES"
_COMMON_VARIABLE = "LIST_OF_TEST_COMMON_INSTANCES"
_GLOB_PATTERN = re.compile(
    r"file\s*\(\s*GLOB_RECURSE\s+"
    r"(?P<variable>LIST_OF_TEST_(?:ARCH|COMMON)_INSTANCES)\s+"
    r"\$\{TEST_DIR\}/(?P<relative>[^\s\)]+)\s*\)",
    flags=re.IGNORECASE,
)


@dataclass(frozen=True)
class CMakeDiscoveryDecision:
    """The actual versus intended test-source result for one class file."""

    blocker_confirmed: bool
    intended_sources: tuple[str, ...]
    final_sources: tuple[str, ...]
    omitted_sources: tuple[str, ...]
    reasons: tuple[str, ...]


def _normalise_relative(path: Path, root: Path) -> str:
    return path.resolve().relative_to(root.resolve()).as_posix()


def _resolve_assignment(
    test_root: Path,
    relative_expression: str,
    class_file_name: str,
) -> tuple[str, ...]:
    """Resolve the narrow ${TEST_CLASS_FILE_NAME} pattern used by the source."""

    relative = relative_expression.replace(
        "${TEST_CLASS_FILE_NAME}",
        class_file_name,
    )
    candidate = (test_root / relative).resolve()
    try:
        candidate.relative_to(test_root.resolve())
    except ValueError as error:
        raise ValueError(
            f"CMake test-source expression escapes the test root: {relative_expression}"
        ) from error
    return (
        (_normalise_relative(candidate, test_root),)
        if candidate.is_file()
        else ()
    )


def _metadata_sources(
    generated_metadata_text: str,
    intended_sources: Sequence[str],
) -> tuple[str, ...]:
    normalised = generated_metadata_text.replace("\\", "/").casefold()
    return tuple(
        source
        for source in intended_sources
        if source.casefold() in normalised
    )


def audit_target_per_test(
    cmake_text: str,
    test_root: Path,
    class_file_name: str,
    generated_metadata_text: str,
) -> CMakeDiscoveryDecision:
    """Apply exact variable assignments in source order and compare metadata.

    This is intentionally not a general CMake interpreter. It recognises only
    the two source-list variables implicated by RB-SRC-001.
    """

    assignments: dict[str, list[tuple[str, ...]]] = {
        _ARCH_VARIABLE: [],
        _COMMON_VARIABLE: [],
    }
    intended: list[str] = []

    for match in _GLOB_PATTERN.finditer(cmake_text):
        variable = match.group("variable").upper()
        resolved = _resolve_assignment(
            test_root,
            match.group("relative"),
            class_file_name,
        )
        assignments[variable].append(resolved)
        for source in resolved:
            if source not in intended:
                intended.append(source)

    calculated_final: list[str] = []
    for variable in (_COMMON_VARIABLE, _ARCH_VARIABLE):
        variable_assignments = assignments[variable]
        if variable_assignments:
            for source in variable_assignments[-1]:
                if source not in calculated_final:
                    calculated_final.append(source)

    observed_in_metadata = _metadata_sources(generated_metadata_text, intended)
    if generated_metadata_text.strip():
        final = list(observed_in_metadata)
    else:
        final = calculated_final
    omitted = [source for source in intended if source not in final]

    repeated_arch = len(assignments[_ARCH_VARIABLE]) > 1
    repeated_common = len(assignments[_COMMON_VARIABLE]) > 1
    reasons: list[str] = []
    if repeated_arch:
        reasons.append(
            "LIST_OF_TEST_ARCH_INSTANCES is assigned more than once; the later assignment replaces the earlier list."
        )
    if repeated_common:
        reasons.append(
            "LIST_OF_TEST_COMMON_INSTANCES is assigned more than once; the later assignment replaces the earlier list."
        )
    if omitted:
        reasons.append(
            "Generated target evidence omits: " + ", ".join(omitted)
        )
    elif repeated_arch or repeated_common:
        reasons.append(
            "Generated target evidence includes every intended source despite the repeated assignments."
        )
    else:
        reasons.append("No repeated source-list assignment was found.")

    blocker_confirmed = bool(omitted) and bool(generated_metadata_text.strip())
    return CMakeDiscoveryDecision(
        blocker_confirmed=blocker_confirmed,
        intended_sources=tuple(intended),
        final_sources=tuple(final),
        omitted_sources=tuple(omitted),
        reasons=tuple(reasons),
    )
