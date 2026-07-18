"""Portable serial launcher for guarded WB-03 completion on a 32 GiB+ host."""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
from dataclasses import dataclass
from datetime import date
from pathlib import Path
from typing import Callable, Iterable

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.testing.animehacker.large_host import (
    ALLOWED_TEST_IDS,
    BLOCKING_PROCESS_NAMES,
    HostInputs,
    active_processes,
    allocate_run_root,
    preflight,
    write_manifest,
)


@dataclass(frozen=True)
class LargeHostConfig:
    matrix: Path
    prompt_set: Path
    cpu_build: Path
    sycl_build: Path
    diagnostic_model: Path
    granite3_model: Path
    granite8_model: Path
    output_parent: Path
    selected: tuple[str, ...]

    @classmethod
    def fixture(cls, root: Path, *, selected: Iterable[str] = ("AH-06", "AH-07", "AH-10")):
        paths = {
            "matrix": root / "matrix.json",
            "prompt_set": root / "prompts.json",
            "diagnostic_model": root / "models" / "diagnostic.gguf",
            "granite3_model": root / "models" / "granite3.gguf",
            "granite8_model": root / "models" / "granite8.gguf",
        }
        for path in paths.values():
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text("{}", encoding="utf-8")
        cpu_build = root / "cpu"
        sycl_build = root / "sycl"
        for build in (cpu_build, sycl_build):
            server = build / "bin" / "llama-server.exe"
            server.parent.mkdir(parents=True, exist_ok=True)
            server.write_bytes(b"server")
        return cls(cpu_build=cpu_build, sycl_build=sycl_build,
                   output_parent=root / "large-host-completion",
                   selected=tuple(selected), **paths)


def validate_selection(selected: Iterable[str]) -> tuple[str, ...]:
    result = tuple(sorted(set(selected)))
    unknown = set(result) - ALLOWED_TEST_IDS
    if unknown:
        raise ValueError(f"unsupported test ID: {sorted(unknown)[0]}")
    if not result:
        raise ValueError("at least one test ID is required")
    return result


def _common_paths(config: LargeHostConfig) -> list[str]:
    return [
        "--matrix", str(config.matrix),
        "--cpu-build", str(config.cpu_build),
        "--sycl-build", str(config.sycl_build),
        "--diagnostic-model", str(config.diagnostic_model),
        "--granite3-model", str(config.granite3_model),
        "--granite8-model", str(config.granite8_model),
    ]


def build_runtime_command(config: LargeHostConfig, test_id: str, runtime_root: Path) -> list[str]:
    validate_selection((test_id,))
    return [sys.executable, str(ROOT / "scripts/testing/run_animehacker_retest.py"),
            *_common_paths(config), "--output-root", str(runtime_root), "--only", test_id,
            "--include-guarded", "--resume", "--minimum-available-ram-mb", "2048"]


def build_quality_command(config: LargeHostConfig, test_id: str, runtime_root: Path,
                          quality_root: Path) -> list[str]:
    validate_selection((test_id,))
    return [sys.executable, str(ROOT / "scripts/testing/run_animehacker_quality.py"),
            *_common_paths(config), "--prompt-set", str(config.prompt_set),
            "--runtime-root", str(runtime_root), "--output-root", str(quality_root),
            "--only", test_id]


def build_adjudication_command(quality_root: Path, output: Path) -> list[str]:
    return [sys.executable, str(ROOT / "scripts/testing/adjudicate_animehacker_quality.py"),
            "--raw-root", str(quality_root), "--output", str(output)]


def _run_phase(command: list[str], log_root: Path, label: str,
               runner: Callable[..., object]) -> None:
    completed = runner(command, capture_output=True, text=True)
    log_root.mkdir(parents=True, exist_ok=True)
    (log_root / f"{label}-stdout.txt").write_text(completed.stdout or "", encoding="utf-8")
    (log_root / f"{label}-stderr.txt").write_text(completed.stderr or "", encoding="utf-8")
    if completed.returncode:
        display = label.replace("-runtime", " runtime").replace("-quality", " quality")
        raise RuntimeError(f"{display} failed with exit code {completed.returncode}")


def execute(config: LargeHostConfig, *, runner: Callable[..., object] = subprocess.run,
            installed_ram_bytes: int | None = None,
            active_process_names: Iterable[str] | None = None,
            level_zero_devices: Iterable[str] | None = None) -> Path:
    selected = validate_selection(config.selected)
    inputs = HostInputs(
        matrix=config.matrix,
        cpu_server=config.cpu_build / "bin" / "llama-server.exe",
        sycl_server=config.sycl_build / "bin" / "llama-server.exe",
        granite8_model=config.granite8_model,
        selected=frozenset(selected),
    )
    result = preflight(inputs, installed_ram_bytes=installed_ram_bytes,
                       active_process_names=active_process_names,
                       level_zero_devices=level_zero_devices)
    run_root = allocate_run_root(config.output_parent, date.today().isoformat())
    write_manifest(run_root, result)
    if not result.accepted:
        raise RuntimeError("large-host preflight failed: " + "; ".join(result.errors))
    runtime_root = run_root / "runtime"
    quality_root = run_root / "quality"
    state_path = run_root / "state.json"
    state = {"selected": list(selected), "completed": []}
    for test_id in selected:
        _run_phase(build_runtime_command(config, test_id, runtime_root), run_root / "logs",
                   f"{test_id}-runtime", runner)
        _run_phase(build_quality_command(config, test_id, runtime_root, quality_root),
                   run_root / "logs", f"{test_id}-quality", runner)
        _run_phase(build_adjudication_command(quality_root, run_root / "quality-adjudications.json"),
                   run_root / "logs", f"{test_id}-adjudication", runner)
        remaining = [name for name in active_processes()
                     if name.lower() in BLOCKING_PROCESS_NAMES]
        cleanup_root = run_root / "cleanup"
        cleanup_root.mkdir(parents=True, exist_ok=True)
        cleanup = {"test_id": test_id, "cleanup_verified": not remaining,
                   "llama_process_count": sum("llama" in name.lower() for name in remaining),
                   "controller_process_count": sum("animehacker" in name.lower() for name in remaining),
                   "remaining_process_names": remaining}
        (cleanup_root / f"{test_id}.json").write_text(
            json.dumps(cleanup, indent=2), encoding="utf-8")
        if remaining:
            raise RuntimeError(f"{test_id} cleanup failed: {remaining}")
        state["completed"].append(test_id)
        state_path.write_text(json.dumps(state, indent=2), encoding="utf-8")
    return run_root


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--matrix", type=Path, required=True)
    parser.add_argument("--prompt-set", type=Path, required=True)
    parser.add_argument("--cpu-build", type=Path, required=True)
    parser.add_argument("--sycl-build", type=Path, required=True)
    parser.add_argument("--diagnostic-model", type=Path, required=True)
    parser.add_argument("--granite3-model", type=Path, required=True)
    parser.add_argument("--granite8-model", type=Path, required=True)
    parser.add_argument("--output-parent", type=Path, required=True)
    parser.add_argument("--only", action="append", required=True, choices=sorted(ALLOWED_TEST_IDS))
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    config = LargeHostConfig(selected=tuple(args.only), **{
        key: getattr(args, key) for key in (
            "matrix", "prompt_set", "cpu_build", "sycl_build", "diagnostic_model",
            "granite3_model", "granite8_model", "output_parent")
    })
    print(execute(config))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
