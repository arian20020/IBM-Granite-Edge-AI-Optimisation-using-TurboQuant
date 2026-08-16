"""Inspect reviewed Optimum source metadata without importing or executing it."""

from __future__ import annotations

import ast
import re
import tomllib
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Final

from scripts.testing.workbook05.phase3.conversion import (
    OPTIMUM_COMMIT,
    OPTIMUM_INTEL_COMMIT,
    REVIEWED_OPTIMUM_INTEL_CONSTRAINTS,
)


# Task 3 freezes the second source contract and the complete ordinary-lock
# input here first. These constants are also exported from conversion.py at
# the Task 3 closure boundary so acquisition and conversion share one source.
REVIEWED_OPTIMUM_CONSTRAINTS: Final[tuple[str, ...]] = (
    "transformers>=4.29",
    "torch>=1.11",
    "packaging",
    "numpy",
    "huggingface_hub>=0.8.0",
)

REVIEWED_NORMAL_REQUIREMENT_INPUT: Final[tuple[str, ...]] = (
    "transformers==5.5.0",
    "huggingface-hub==1.21.0",
    "nncf==3.2.0",
    "openvino==2026.2.1",
    "openvino-tokenizers==2026.2.1.0",
    "torch>=2.1",
    "safetensors<0.8.0",
    "setuptools",
    "requests>=2.33,<3.0",
    "packaging",
    "numpy",
    "wheel",
)

# Both reviewed source trees expose the same console command. Keeping this
# value explicit makes an entry-point change a reviewable contract change.
_REVIEWED_ENTRY_POINTS: Final[tuple[str, ...]] = (
    "optimum-cli=optimum.commands.optimum_cli:main",
)


@dataclass(frozen=True, slots=True)
class SourcePackageContract:
    """Data-only package metadata extracted from one immutable source tree."""

    name: str
    base_version: str
    runtime_requirements: tuple[str, ...]
    console_entry_points: tuple[str, ...]
    build_system_declared: bool


@dataclass(frozen=True, slots=True)
class _SourceLayout:
    """The exact metadata locations and declarations reviewed for one package."""

    requirement_assignment: str
    version_path: tuple[str, ...]
    expected_version: str
    expected_requirements: tuple[str, ...]
    expected_commit: str


_SOURCE_LAYOUTS: Final[dict[str, _SourceLayout]] = {
    "optimum": _SourceLayout(
        requirement_assignment="REQUIRED_PKGS",
        version_path=("optimum", "version.py"),
        expected_version="2.3.0",
        expected_requirements=REVIEWED_OPTIMUM_CONSTRAINTS,
        expected_commit=OPTIMUM_COMMIT,
    ),
    "optimum-intel": _SourceLayout(
        requirement_assignment="INSTALL_REQUIRE",
        version_path=("optimum", "intel", "version.py"),
        expected_version="2.2.0.dev0",
        expected_requirements=REVIEWED_OPTIMUM_INTEL_CONSTRAINTS,
        expected_commit=OPTIMUM_INTEL_COMMIT,
    ),
}

# Each row states how one exact reviewed source requirement is satisfied by
# the final ordinary input or by the independently pinned Optimum VCS source.
_REQUIREMENT_COVERAGE: Final[tuple[tuple[str, str, str, str], ...]] = (
    (
        "optimum",
        "transformers>=4.29",
        "transformers==5.5.0",
        "stronger-exact-pin",
    ),
    ("optimum", "torch>=1.11", "torch>=2.1", "stronger-range"),
    ("optimum", "packaging", "packaging", "direct"),
    ("optimum", "numpy", "numpy", "direct"),
    (
        "optimum",
        "huggingface_hub>=0.8.0",
        "huggingface-hub==1.21.0",
        "stronger-exact-pin",
    ),
    ("optimum-intel", "torch>=2.1", "torch>=2.1", "direct"),
    (
        "optimum-intel",
        "safetensors<0.8.0",
        "safetensors<0.8.0",
        "direct",
    ),
    (
        "optimum-intel",
        "optimum~=2.3.0",
        f"optimum@{OPTIMUM_COMMIT}",
        "reviewed-vcs-source",
    ),
    (
        "optimum-intel",
        "transformers>=4.51,<5.6",
        "transformers==5.5.0",
        "stronger-exact-pin",
    ),
    ("optimum-intel", "setuptools", "setuptools", "direct"),
    (
        "optimum-intel",
        "huggingface-hub>=0.23.2,<1.22",
        "huggingface-hub==1.21.0",
        "stronger-exact-pin",
    ),
    (
        "optimum-intel",
        "nncf>=2.19.0",
        "nncf==3.2.0",
        "stronger-exact-pin",
    ),
    (
        "optimum-intel",
        "openvino>=2026.0",
        "openvino==2026.2.1",
        "stronger-exact-pin",
    ),
    (
        "optimum-intel",
        "openvino-tokenizers>=2026.0",
        "openvino-tokenizers==2026.2.1.0",
        "stronger-exact-pin",
    ),
    (
        "optimum-intel",
        "requests>=2.33,<3.0",
        "requests>=2.33,<3.0",
        "direct",
    ),
)

_REQUIREMENT_NAME = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]*")


def _is_link_like(path: Path) -> bool:
    """Reject symbolic links and Windows junctions when the API is available."""

    if path.is_symlink():
        return True
    is_junction = getattr(path, "is_junction", None)
    return bool(is_junction is not None and is_junction())


def _normal_source_root(root: Path) -> Path:
    """Return a real source directory without following a link-like root."""

    if _is_link_like(root) or not root.is_dir():
        raise ValueError(f"Source root must be one normal directory: {root}")
    return root.resolve(strict=True)


def _normal_source_file(
    root: Path,
    resolved_root: Path,
    relative_parts: tuple[str, ...],
) -> Path:
    """Resolve one required regular file while keeping it inside the source root."""

    candidate = root.joinpath(*relative_parts)
    current = root
    for part in relative_parts:
        current = current / part
        if _is_link_like(current):
            raise ValueError(
                "Source metadata must not traverse a symbolic link or junction: "
                f"{candidate}"
            )
    if not candidate.is_file():
        raise ValueError(f"Required source metadata file is missing: {candidate}")
    resolved_candidate = candidate.resolve(strict=True)
    try:
        resolved_candidate.relative_to(resolved_root)
    except ValueError as error:
        raise ValueError(
            f"Source metadata escaped the reviewed source root: {candidate}"
        ) from error
    return resolved_candidate


def _parse_python(path: Path) -> ast.Module:
    """Parse Python source as syntax only; never import or execute the file."""

    try:
        return ast.parse(
            path.read_text(encoding="utf-8"),
            filename=str(path),
        )
    except (OSError, UnicodeError, SyntaxError) as error:
        raise ValueError(f"Could not parse source metadata: {path}") from error


def _single_assignment(module: ast.Module, name: str) -> ast.expr:
    """Return the value of one exact top-level assignment."""

    values: list[ast.expr] = []
    for statement in module.body:
        if isinstance(statement, ast.Assign):
            for target in statement.targets:
                if isinstance(target, ast.Name) and target.id == name:
                    values.append(statement.value)
        elif (
            isinstance(statement, ast.AnnAssign)
            and isinstance(statement.target, ast.Name)
            and statement.target.id == name
            and statement.value is not None
        ):
            values.append(statement.value)
    if len(values) != 1:
        raise ValueError(
            f"Source metadata must declare exactly one top-level {name} assignment."
        )
    return values[0]


def _literal_string_sequence(
    expression: ast.expr,
    label: str,
) -> tuple[str, ...]:
    """Require one literal, duplicate-free list or tuple of exact strings."""

    try:
        value = ast.literal_eval(expression)
    except (ValueError, TypeError) as error:
        raise ValueError(f"{label} must be one literal list or tuple.") from error
    if not isinstance(value, (list, tuple)):
        raise ValueError(f"{label} must be one literal list or tuple.")

    result: list[str] = []
    seen: set[str] = set()
    for item in value:
        if (
            not isinstance(item, str)
            or not item
            or item != item.strip()
            or "\x00" in item
            or any(ord(character) < 32 for character in item)
        ):
            raise ValueError(f"Every {label} member must be one exact string.")
        key = item.casefold()
        if key in seen:
            raise ValueError(f"{label} contains a duplicate value: {item}")
        seen.add(key)
        result.append(item)
    if not result:
        raise ValueError(f"{label} must not be empty.")
    return tuple(result)


def _literal_string(expression: ast.expr, label: str) -> str:
    """Require one non-empty string literal."""

    try:
        value = ast.literal_eval(expression)
    except (ValueError, TypeError) as error:
        raise ValueError(f"{label} must be one string literal.") from error
    if not isinstance(value, str) or not value or value != value.strip():
        raise ValueError(f"{label} must be one non-empty string literal.")
    return value


def _setup_keywords(module: ast.Module) -> dict[str, ast.expr]:
    """Return explicit keyword arguments from the single setup(...) call."""

    calls = [
        statement.value
        for statement in module.body
        if isinstance(statement, ast.Expr)
        and isinstance(statement.value, ast.Call)
        and isinstance(statement.value.func, ast.Name)
        and statement.value.func.id == "setup"
    ]
    if len(calls) != 1:
        raise ValueError("Source metadata must contain exactly one setup(...) call.")

    result: dict[str, ast.expr] = {}
    for keyword in calls[0].keywords:
        if keyword.arg is None:
            raise ValueError("setup(...) may not use dynamic **keyword metadata.")
        if keyword.arg in result:
            raise ValueError(f"setup(...) repeats keyword: {keyword.arg}")
        result[keyword.arg] = keyword.value
    return result


def _entry_points(expression: ast.expr) -> tuple[str, ...]:
    """Read the literal console-script catalogue from setup metadata."""

    try:
        value = ast.literal_eval(expression)
    except (ValueError, TypeError) as error:
        raise ValueError("The console entry point metadata must be literal.") from error
    if not isinstance(value, dict) or set(value) != {"console_scripts"}:
        raise ValueError(
            "The console entry point metadata must contain only console_scripts."
        )
    scripts = value["console_scripts"]
    if not isinstance(scripts, (list, tuple)):
        raise ValueError("The console entry point catalogue must be a list or tuple.")
    return _literal_string_sequence(
        ast.Constant(value=list(scripts)),
        "console entry point catalogue",
    )


def _version_from_file(path: Path) -> str:
    """Read the base version from the reviewed version module as a literal."""

    module = _parse_python(path)
    return _literal_string(
        _single_assignment(module, "__version__"),
        "Source base version",
    )


def _build_system_declared(path: Path) -> bool:
    """Parse pyproject.toml as data and report whether it declares a backend."""

    try:
        with path.open("rb") as stream:
            document = tomllib.load(stream)
    except (OSError, tomllib.TOMLDecodeError) as error:
        raise ValueError(f"Could not parse source pyproject.toml: {path}") from error
    return "build-system" in document


def _canonical_requirement_name(requirement: str) -> str:
    """Return the normalized distribution name at the start of a requirement."""

    match = _REQUIREMENT_NAME.match(requirement)
    if match is None:
        raise ValueError(f"Requirement has no valid distribution name: {requirement}")
    return re.sub(r"[-_.]+", "-", match.group(0)).casefold()


def _reject_duplicate_requirement_names(requirements: tuple[str, ...]) -> None:
    """Prevent two strings from silently controlling the same distribution."""

    seen: set[str] = set()
    for requirement in requirements:
        name = _canonical_requirement_name(requirement)
        if name in seen:
            raise ValueError(
                "Reviewed normal requirement input contains a duplicate package: "
                f"{name}"
            )
        seen.add(name)


def inspect_source_contract(name: str, root: Path) -> SourcePackageContract:
    """Inspect one source tree entirely as data, without importing setup.py."""

    if name not in _SOURCE_LAYOUTS:
        raise ValueError(f"Unsupported reviewed source package: {name}")
    layout = _SOURCE_LAYOUTS[name]
    resolved_root = _normal_source_root(root)
    setup_path = _normal_source_file(root, resolved_root, ("setup.py",))
    version_path = _normal_source_file(
        root,
        resolved_root,
        layout.version_path,
    )
    pyproject_path = _normal_source_file(
        root,
        resolved_root,
        ("pyproject.toml",),
    )

    setup_module = _parse_python(setup_path)
    runtime_requirements = _literal_string_sequence(
        _single_assignment(setup_module, layout.requirement_assignment),
        "Runtime requirement assignment",
    )
    keywords = _setup_keywords(setup_module)
    required_keywords = {"name", "version", "install_requires", "entry_points"}
    missing = sorted(required_keywords - set(keywords))
    if missing:
        raise ValueError(
            "setup(...) is missing reviewed metadata keywords: "
            + ", ".join(missing)
        )

    observed_name = _literal_string(keywords["name"], "Source package name")
    if observed_name != name:
        raise ValueError(
            f"Source package name does not match {name}: {observed_name}"
        )
    version_expression = keywords["version"]
    if (
        not isinstance(version_expression, ast.Name)
        or version_expression.id != "__version__"
    ):
        raise ValueError(
            "setup(...) version must reference the reviewed __version__ value."
        )
    requirements_expression = keywords["install_requires"]
    if (
        not isinstance(requirements_expression, ast.Name)
        or requirements_expression.id != layout.requirement_assignment
    ):
        raise ValueError(
            "setup(...) install_requires must reference the reviewed runtime "
            "requirement assignment."
        )

    build_system_declared = _build_system_declared(pyproject_path)
    if build_system_declared:
        raise ValueError(
            "The reviewed source pyproject.toml must not declare [build-system]."
        )
    return SourcePackageContract(
        name=name,
        base_version=_version_from_file(version_path),
        runtime_requirements=runtime_requirements,
        console_entry_points=_entry_points(keywords["entry_points"]),
        build_system_declared=False,
    )


def build_reviewed_normal_requirement_input() -> tuple[str, ...]:
    """Return the exact ordinary-distribution input approved for resolution."""

    requirements = tuple(REVIEWED_NORMAL_REQUIREMENT_INPUT)
    _reject_duplicate_requirement_names(requirements)
    forbidden = {"optimum", "optimum-intel"}
    observed = {
        _canonical_requirement_name(requirement)
        for requirement in requirements
    }
    if forbidden & observed:
        raise ValueError(
            "VCS packages must remain outside the ordinary requirement input."
        )
    return requirements


def _requirement_coverage() -> list[dict[str, str]]:
    """Build and self-check the complete reviewed source-requirement mapping."""

    expected_pairs = {
        (source_name, requirement)
        for source_name, layout in _SOURCE_LAYOUTS.items()
        for requirement in layout.expected_requirements
    }
    observed_pairs = {
        (source_name, requirement)
        for source_name, requirement, _, _ in _REQUIREMENT_COVERAGE
    }
    if observed_pairs != expected_pairs or len(observed_pairs) != len(
        _REQUIREMENT_COVERAGE
    ):
        raise ValueError(
            "The reviewed requirement coverage map is incomplete or duplicated."
        )

    ordinary = set(build_reviewed_normal_requirement_input())
    records: list[dict[str, str]] = []
    for source_name, requirement, covered_by, mode in _REQUIREMENT_COVERAGE:
        if mode == "reviewed-vcs-source":
            expected_vcs = f"optimum@{OPTIMUM_COMMIT}"
            if covered_by != expected_vcs or requirement != "optimum~=2.3.0":
                raise ValueError("The reviewed VCS requirement binding drifted.")
        elif covered_by not in ordinary:
            raise ValueError(
                "A source requirement is not covered by the ordinary input: "
                f"{source_name} {requirement}"
            )
        records.append(
            {
                "source_package": source_name,
                "source_requirement": requirement,
                "covered_by": covered_by,
                "mode": mode,
            }
        )
    return records


def validate_reviewed_source_contracts(
    optimum_root: Path,
    optimum_intel_root: Path,
) -> dict[str, object]:
    """Require both pinned source trees to match the complete reviewed contract."""

    observed = {
        "optimum": inspect_source_contract("optimum", optimum_root),
        "optimum-intel": inspect_source_contract(
            "optimum-intel",
            optimum_intel_root,
        ),
    }
    for name, contract in observed.items():
        layout = _SOURCE_LAYOUTS[name]
        if contract.base_version != layout.expected_version:
            raise ValueError(
                f"{name} source version drifted from {layout.expected_version}: "
                f"{contract.base_version}"
            )
        if contract.runtime_requirements != layout.expected_requirements:
            raise ValueError(
                f"{name} runtime requirements do not match the reviewed source "
                "contract."
            )
        if contract.console_entry_points != _REVIEWED_ENTRY_POINTS:
            raise ValueError(
                f"{name} console entry point does not match the reviewed source "
                "contract."
            )
        if contract.build_system_declared:
            raise ValueError(f"{name} unexpectedly declares a build-system.")

    source_records: dict[str, dict[str, object]] = {}
    for name in ("optimum", "optimum-intel"):
        contract_record = asdict(observed[name])
        contract_record["runtime_requirements"] = list(
            observed[name].runtime_requirements
        )
        contract_record["console_entry_points"] = list(
            observed[name].console_entry_points
        )
        contract_record["reviewed_commit"] = _SOURCE_LAYOUTS[
            name
        ].expected_commit
        source_records[name] = contract_record

    return {
        "schema_version": "1.0",
        "record_type": "dependency-source-contract",
        "status": "Passed",
        "source_metadata_execution": False,
        "build_system_declared": False,
        "installer_build_mode": "setuptools-no-build-isolation",
        "reviewed_build_tools": ["setuptools", "wheel"],
        "sources": source_records,
        "normal_requirement_input": list(
            build_reviewed_normal_requirement_input()
        ),
        "requirement_coverage": _requirement_coverage(),
    }


def write_reviewed_normal_requirement_input(
    output: Path,
) -> tuple[str, ...]:
    """Create the exact LF-terminated input once without overwriting any file."""

    requirements = build_reviewed_normal_requirement_input()
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = output.with_name(output.name + ".tmp")
    if temporary.exists():
        raise FileExistsError(
            f"Temporary requirements output already exists: {temporary}"
        )
    payload = ("\n".join(requirements) + "\n").encode("utf-8")
    # Exclusive creation is the fail-closed boundary: a pre-existing file is
    # never replaced, repaired, or silently reused.
    with output.open("xb") as stream:
        stream.write(payload)
    return requirements
