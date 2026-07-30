"""Run one fail-closed, fully instrumented OpenVINO WB-04 measurement."""

from __future__ import annotations

import argparse
import json
import os
import sys
from collections.abc import Callable, Mapping
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.testing.official_openvino.runtime_process import (
    run_governed_process,
)


MIB = 1024**2
SPEC_SCHEMA = "official-openvino-wb04-worker-spec/v1"
ROLES = frozenset({"pilot", "warmup", "sample-1", "sample-2", "sample-3"})


def _directory(path: Path, field: str) -> Path:
    resolved = Path(path).resolve()
    if not resolved.is_dir():
        raise ValueError(f"{field} directory is missing: {resolved}")
    return resolved


def _file(path: Path, field: str) -> Path:
    resolved = Path(path).resolve()
    if not resolved.is_file():
        raise ValueError(f"{field} file is missing: {resolved}")
    return resolved


def build_worker_environment(
    *,
    build_root: Path,
    repo_root: Path,
    python_site_packages: Path,
    openvino_libraries: Path,
    base_environment: Mapping[str, str] | None = None,
) -> dict[str, str]:
    """Bind imports and DLL lookup to one verified build."""

    build = _directory(build_root, "build root")
    repository = _directory(repo_root, "repository root")
    site_packages = _directory(python_site_packages, "Python site-packages")
    libraries = _directory(openvino_libraries, "OpenVINO libraries")
    package = _directory(build / "openvino_genai", "OpenVINO GenAI package")
    _file(package / "__init__.py", "OpenVINO GenAI package initializer")
    modules = sorted(package.glob("py_openvino_genai*.pyd"))
    if len(modules) != 1 or modules[0].stat().st_size <= 0:
        raise ValueError(
            "build must contain exactly one non-empty py_openvino_genai module"
        )
    _file(package / "openvino_genai.dll", "OpenVINO GenAI runtime")

    environment = dict(
        os.environ if base_environment is None else base_environment
    )
    existing_path = environment.get("PATH", "")
    path_entries = [str(package), str(libraries)]
    if existing_path:
        path_entries.append(existing_path)
    environment.update(
        {
            "PATH": os.pathsep.join(path_entries),
            "PYTHONPATH": os.pathsep.join(
                (str(build), str(repository), str(site_packages))
            ),
            "PYTHONNOUSERSITE": "1",
            "PYTHONUTF8": "1",
            "PYTHONHASHSEED": "0",
        }
    )
    return environment


def _load_spec(path: Path, role: str) -> Path:
    if role not in ROLES:
        raise ValueError(f"unsupported measurement role: {role}")
    spec_path = _file(path, "worker spec")
    try:
        value = json.loads(spec_path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as error:
        raise ValueError(f"worker spec is unreadable: {spec_path}") from error
    if not isinstance(value, dict) or value.get("schema") != SPEC_SCHEMA:
        raise ValueError("worker spec schema is invalid")
    if value.get("role") != role:
        raise ValueError("worker spec role does not match requested role")
    return spec_path


def run_single_measurement(
    *,
    spec_path: Path,
    output_dir: Path,
    role: str,
    build_root: Path,
    repo_root: Path,
    python_executable: Path,
    python_site_packages: Path,
    openvino_libraries: Path,
    sampler_script: Path,
    timeout_seconds: float = 900.0,
    minimum_available_ram_mib: int = 2048,
    run_process: Callable[..., dict[str, Any]] = run_governed_process,
) -> dict[str, Any]:
    """Execute one role in a fresh process and return its persisted record."""

    spec = _load_spec(spec_path, role)
    python = _file(python_executable, "Python executable")
    sampler = _file(sampler_script, "utilization sampler")
    output = Path(output_dir).resolve()
    if output.exists():
        raise ValueError(f"output directory already exists: {output}")
    if timeout_seconds <= 0:
        raise ValueError("timeout seconds must be positive")
    if (
        isinstance(minimum_available_ram_mib, bool)
        or not isinstance(minimum_available_ram_mib, int)
        or minimum_available_ram_mib < 2048
    ):
        raise ValueError("minimum available RAM must be at least 2048 MiB")

    environment = build_worker_environment(
        build_root=build_root,
        repo_root=repo_root,
        python_site_packages=python_site_packages,
        openvino_libraries=openvino_libraries,
    )
    command = [
        str(python),
        "-m",
        "scripts.testing.official_openvino.measurement_worker",
        "--spec",
        str(spec),
    ]
    return run_process(
        command=command,
        output_dir=output,
        role=role,
        environment=environment,
        sampler_script=sampler,
        timeout_seconds=float(timeout_seconds),
        minimum_available_ram_bytes=minimum_available_ram_mib * MIB,
        sample_interval_seconds=0.25,
    )


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser()
    parser.add_argument("--spec", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--role", choices=sorted(ROLES), required=True)
    parser.add_argument("--build-root", type=Path, required=True)
    parser.add_argument("--repo-root", type=Path, required=True)
    parser.add_argument("--python-executable", type=Path, required=True)
    parser.add_argument("--python-site-packages", type=Path, required=True)
    parser.add_argument("--openvino-libraries", type=Path, required=True)
    parser.add_argument("--sampler-script", type=Path, required=True)
    parser.add_argument("--timeout-seconds", type=float, default=900.0)
    parser.add_argument("--minimum-available-ram-mib", type=int, default=2048)
    return parser


def main(argv: list[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    record = run_single_measurement(
        spec_path=args.spec,
        output_dir=args.output_dir,
        role=args.role,
        build_root=args.build_root,
        repo_root=args.repo_root,
        python_executable=args.python_executable,
        python_site_packages=args.python_site_packages,
        openvino_libraries=args.openvino_libraries,
        sampler_script=args.sampler_script,
        timeout_seconds=args.timeout_seconds,
        minimum_available_ram_mib=args.minimum_available_ram_mib,
    )
    print(json.dumps(record, sort_keys=True, allow_nan=False))
    return 0 if record.get("valid") is True else 1


if __name__ == "__main__":
    raise SystemExit(main())
