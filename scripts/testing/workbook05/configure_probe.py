"""Construct and evaluate the controlled Route A CMake configure-only probe."""

from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass
from pathlib import Path, PureWindowsPath
from typing import Mapping, Sequence


DEFAULT_CMAKE_PATH = PureWindowsPath(
    r"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
)
DEFAULT_SOURCE_ROOT = PureWindowsPath(r"C:\wb05\source\route-a\openvino")
DEFAULT_BUILD_ROOT = PureWindowsPath(r"C:\wb05\build\route-a-runtime")
EXPECTED_GENERATOR = "Visual Studio 17 2022"
EXPECTED_PLATFORM = "x64"
EXPECTED_GPU_SETTING = "OFF"
FORBIDDEN_ARGUMENTS = {"--build", "--install", "--target", "--package"}


@dataclass(frozen=True)
class ConfigureProbeDecision:
    """The controlled interpretation of one CMake generation attempt."""

    passed: bool
    command: tuple[str, ...]
    cache_values: Mapping[str, str]
    reasons: tuple[str, ...]


def build_route_a_configure_command(
    cmake_path: PureWindowsPath = DEFAULT_CMAKE_PATH,
    source_root: PureWindowsPath = DEFAULT_SOURCE_ROOT,
    build_root: PureWindowsPath = DEFAULT_BUILD_ROOT,
) -> tuple[str, ...]:
    """Return the exact CPU-only generation command; no build action is present."""

    return (
        str(cmake_path),
        "-S",
        str(source_root),
        "-B",
        str(build_root),
        "-G",
        EXPECTED_GENERATOR,
        "-A",
        EXPECTED_PLATFORM,
        "-DENABLE_INTEL_GPU=OFF",
    )


def parse_cmake_cache(cmake_cache_text: str) -> dict[str, str]:
    """Parse NAME:TYPE=value records while ignoring comments and blank lines."""

    values: dict[str, str] = {}
    for raw_line in cmake_cache_text.splitlines():
        line = raw_line.strip()
        if not line or line.startswith(("#", "//")) or "=" not in line:
            continue
        name_and_type, value = line.split("=", 1)
        name = name_and_type.split(":", 1)[0]
        if name:
            values[name] = value
    return values


def _windows_cache_path(path: PureWindowsPath) -> str:
    return path.as_posix()


def evaluate_route_a_configure_probe(
    command: Sequence[str],
    exit_code: int,
    cmake_cache_text: str,
    expected_source_root: PureWindowsPath = DEFAULT_SOURCE_ROOT,
    expected_build_root: PureWindowsPath = DEFAULT_BUILD_ROOT,
    expected_cmake_path: PureWindowsPath = DEFAULT_CMAKE_PATH,
) -> ConfigureProbeDecision:
    """Reject wrong commands or a misleading zero-exit CMake cache."""

    command_tuple = tuple(str(item) for item in command)
    expected_command = build_route_a_configure_command(
        expected_cmake_path,
        expected_source_root,
        expected_build_root,
    )
    cache_values = parse_cmake_cache(cmake_cache_text)
    reasons: list[str] = []

    lowered_arguments = {argument.casefold() for argument in command_tuple}
    forbidden = sorted(FORBIDDEN_ARGUMENTS.intersection(lowered_arguments))
    if forbidden:
        reasons.append("Forbidden CMake action argument(s): " + ", ".join(forbidden))
    if command_tuple != expected_command:
        reasons.append("The command does not match the frozen Route A configure command.")
    if exit_code != 0:
        reasons.append(f"CMake configure exited with code {exit_code}.")

    expected_cache = {
        "CMAKE_HOME_DIRECTORY": _windows_cache_path(expected_source_root),
        "CMAKE_GENERATOR": EXPECTED_GENERATOR,
        "CMAKE_GENERATOR_PLATFORM": EXPECTED_PLATFORM,
        "ENABLE_INTEL_GPU": EXPECTED_GPU_SETTING,
    }
    for name, expected in expected_cache.items():
        actual = cache_values.get(name)
        if actual != expected:
            reasons.append(
                f"CMake cache {name} must be {expected!r}; found {actual!r}."
            )

    return ConfigureProbeDecision(
        passed=not reasons,
        command=command_tuple,
        cache_values=cache_values,
        reasons=tuple(reasons),
    )


def ensure_fresh_build_directory(build_root: Path) -> None:
    """Allow an absent or empty directory and refuse unexpected existing state."""

    if build_root.is_symlink():
        raise ValueError(f"Build directory must not be a symbolic link: {build_root}")
    if build_root.exists():
        if not build_root.is_dir():
            raise ValueError(f"Build path exists but is not a directory: {build_root}")
        if any(build_root.iterdir()):
            raise ValueError(
                f"Build directory is not empty and will not be reused: {build_root}"
            )
    else:
        build_root.mkdir(parents=True, exist_ok=False)


def write_configure_probe_report(
    destination: Path,
    *,
    decision: ConfigureProbeDecision,
    started_utc: str,
    ended_utc: str,
    exit_code: int,
    stdout_path: str,
    stderr_path: str,
    cmake_cache_path: Path,
) -> dict[str, object]:
    """Write the schema-ready report without embedding generated build data."""

    cache_hash = hashlib.sha256(cmake_cache_path.read_bytes()).hexdigest()
    report: dict[str, object] = {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "phase_id": "phase-1-source-admission",
        "route_id": "route-a-merged-openvino",
        "command": list(decision.command),
        "working_directory": r"C:\wb05",
        "started_utc": started_utc,
        "ended_utc": ended_utc,
        "exit_code": exit_code,
        "stdout_path": stdout_path,
        "stderr_path": stderr_path,
        "cmake_cache_path": "configure/CMakeCache.txt",
        "cmake_cache_sha256": cache_hash,
        "cache_values": {
            key: decision.cache_values.get(key, "")
            for key in (
                "CMAKE_HOME_DIRECTORY",
                "CMAKE_GENERATOR",
                "CMAKE_GENERATOR_PLATFORM",
                "ENABLE_INTEL_GPU",
            )
        },
        "build_invoked": False,
        "install_invoked": False,
        "package_invoked": False,
        "status": "Passed" if decision.passed else "Failed",
        "reasons": list(decision.reasons),
    }
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(
        json.dumps(report, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    return report
