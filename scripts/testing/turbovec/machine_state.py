"""Read-only Windows readiness sampling and formal-block admission."""

from __future__ import annotations

import argparse
import ctypes
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import statistics
import subprocess
import time
from typing import Mapping

import psutil  # type: ignore


GIB = 1024 ** 3
REQUIRED_CONDITIONS = frozenset({
    "battery_saver_disabled", "no_windows_update_install", "no_active_downloads",
    "no_cloud_sync_activity", "no_other_compilation", "no_other_benchmark",
    "no_browser_or_media_workload", "previous_candidate_processes_terminated",
    "previous_index_unloaded", "temporary_resources_cleaned", "security_protections_enabled",
})


def _committed_memory_bytes() -> int:
    if os.name == "nt":
        class PerformanceInformation(ctypes.Structure):
            _fields_ = [
                ("cb", ctypes.c_ulong),
                ("CommitTotal", ctypes.c_size_t), ("CommitLimit", ctypes.c_size_t),
                ("CommitPeak", ctypes.c_size_t), ("PhysicalTotal", ctypes.c_size_t),
                ("PhysicalAvailable", ctypes.c_size_t), ("SystemCache", ctypes.c_size_t),
                ("KernelTotal", ctypes.c_size_t), ("KernelPaged", ctypes.c_size_t),
                ("KernelNonpaged", ctypes.c_size_t), ("PageSize", ctypes.c_size_t),
                ("HandleCount", ctypes.c_ulong), ("ProcessCount", ctypes.c_ulong),
                ("ThreadCount", ctypes.c_ulong),
            ]
        information = PerformanceInformation()
        information.cb = ctypes.sizeof(information)
        if ctypes.windll.psapi.GetPerformanceInfo(ctypes.byref(information), information.cb):
            return int(information.CommitTotal * information.PageSize)
    virtual = psutil.virtual_memory()
    return int(virtual.used + psutil.swap_memory().used)


@dataclass(frozen=True)
class MachineSample:
    timestamp_utc: str
    total_physical_ram_bytes: int
    available_physical_ram_bytes: int
    committed_memory_bytes: int
    pagefile_used_bytes: int
    experiment_process_working_set_bytes: int
    system_cpu_percent: float
    experiment_process_cpu_percent: float
    gpu_utilization_percent: float | None
    shared_gpu_memory_bytes: int | None
    power_mode: str
    on_ac_power: bool | None
    uptime_seconds: float
    top_memory_processes: tuple[dict[str, object], ...]
    top_cpu_processes: tuple[dict[str, object], ...]
    thermal_celsius: float | None


def evaluate_readiness(samples: list[MachineSample], conditions: Mapping[str, bool]) -> dict[str, object]:
    reasons: list[str] = []
    if not samples:
        raise ValueError("machine samples are required")
    start = datetime.fromisoformat(samples[0].timestamp_utc)
    end = datetime.fromisoformat(samples[-1].timestamp_utc)
    coverage = (end - start).total_seconds()
    if coverage < 60:
        reasons.append("readiness sampling must cover at least 60 seconds")
    average_cpu = statistics.fmean(item.system_cpu_percent for item in samples)
    if average_cpu >= 10.0:
        reasons.append("average system CPU must be below 10 percent")
    available = [item.available_physical_ram_bytes for item in samples]
    mean_available = statistics.fmean(available)
    variation = (max(available) - min(available)) / mean_available if mean_available else 1.0
    if variation > 0.05:
        reasons.append("available RAM stability exceeded approximately 5 percent")
    minimum_available = min(available)
    safety_abort = minimum_available < 2 * GIB
    if minimum_available < 4 * GIB:
        reasons.append("available RAM is below the practical 4 GiB gate")
    if safety_abort:
        reasons.append("available RAM is below the hard 2 GiB safety floor")
    if not all(item.on_ac_power is True for item in samples):
        reasons.append("AC power was not confirmed for the full sample")
    modes = {item.power_mode for item in samples}
    if len(modes) != 1 or not next(iter(modes), ""):
        reasons.append("one stable Windows power mode was not confirmed")
    missing = REQUIRED_CONDITIONS - set(conditions)
    for key in sorted(missing):
        reasons.append(f"{key} was not recorded")
    for key in sorted(REQUIRED_CONDITIONS & set(conditions)):
        if conditions[key] is not True:
            reasons.append(f"{key} was not satisfied")
    return {
        "ready": not reasons,
        "classification": "machine_safety" if safety_abort else ("ready" if not reasons else "readiness_blocked"),
        "safety_abort": safety_abort,
        "sample_count": len(samples),
        "coverage_seconds": coverage,
        "average_system_cpu_percent": average_cpu,
        "available_ram_variation_fraction": variation,
        "minimum_available_ram_bytes": minimum_available,
        "power_modes": sorted(modes),
        "reasons": reasons,
    }


def _power_mode() -> str:
    completed = subprocess.run(["powercfg", "/getactivescheme"], capture_output=True, text=True, timeout=10)
    if completed.returncode != 0:
        return ""
    return " ".join(completed.stdout.split())


def _top_processes() -> tuple[tuple[dict[str, object], ...], tuple[dict[str, object], ...]]:
    rows = []
    for process in psutil.process_iter(["pid", "name", "memory_info", "cpu_percent"]):
        try:
            memory = process.info["memory_info"]
            rows.append({
                "pid": int(process.info["pid"]),
                "name": str(process.info["name"] or "unknown"),
                "working_set_bytes": int(memory.rss if memory else 0),
                "cpu_percent": float(process.info["cpu_percent"] or 0.0),
            })
        except (psutil.AccessDenied, psutil.NoSuchProcess):
            continue
    by_memory = tuple(sorted(rows, key=lambda row: int(row["working_set_bytes"]), reverse=True)[:10])
    by_cpu = tuple(sorted(rows, key=lambda row: float(row["cpu_percent"]), reverse=True)[:10])
    return by_memory, by_cpu


def capture_sample(process: psutil.Process) -> MachineSample:
    virtual = psutil.virtual_memory()
    swap = psutil.swap_memory()
    battery = psutil.sensors_battery()
    top_memory, top_cpu = _top_processes()
    return MachineSample(
        timestamp_utc=datetime.now(timezone.utc).isoformat(),
        total_physical_ram_bytes=int(virtual.total),
        available_physical_ram_bytes=int(virtual.available),
        committed_memory_bytes=_committed_memory_bytes(),
        pagefile_used_bytes=int(swap.used),
        experiment_process_working_set_bytes=int(process.memory_info().rss),
        system_cpu_percent=float(psutil.cpu_percent(interval=None)),
        experiment_process_cpu_percent=float(process.cpu_percent(interval=None)),
        gpu_utilization_percent=None,
        shared_gpu_memory_bytes=None,
        power_mode=_power_mode(),
        on_ac_power=None if battery is None else bool(battery.power_plugged),
        uptime_seconds=time.time() - psutil.boot_time(),
        top_memory_processes=top_memory,
        top_cpu_processes=top_cpu,
        thermal_celsius=None,
    )


def capture_window(duration_seconds: int, interval_seconds: float = 1.0) -> list[MachineSample]:
    if duration_seconds < 1 or interval_seconds <= 0:
        raise ValueError("sampling duration and interval must be positive")
    process = psutil.Process()
    psutil.cpu_percent(interval=None)
    process.cpu_percent(interval=None)
    deadline = time.monotonic() + duration_seconds
    samples = [capture_sample(process)]
    while time.monotonic() < deadline:
        time.sleep(min(interval_seconds, max(0.0, deadline - time.monotonic())))
        samples.append(capture_sample(process))
    coverage = (datetime.fromisoformat(samples[-1].timestamp_utc) - datetime.fromisoformat(samples[0].timestamp_utc)).total_seconds()
    if coverage < duration_seconds:
        time.sleep(duration_seconds - coverage)
        samples.append(capture_sample(process))
    return samples


def _write_json(path: Path, value: object) -> None:
    path.write_text(json.dumps(value, sort_keys=True, separators=(",", ":")) + "\n", encoding="utf-8", newline="\n")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--conditions-json", type=Path, required=True)
    parser.add_argument("--duration-seconds", type=int, default=60)
    args = parser.parse_args(argv)
    if args.output_root.exists():
        raise FileExistsError(args.output_root)
    conditions = json.loads(args.conditions_json.read_text(encoding="utf-8"))
    args.output_root.mkdir(parents=True)
    samples = capture_window(args.duration_seconds)
    with (args.output_root / "samples.jsonl").open("w", encoding="utf-8", newline="\n") as stream:
        for item in samples:
            stream.write(json.dumps(asdict(item), sort_keys=True, separators=(",", ":")) + "\n")
    decision = evaluate_readiness(samples, conditions)
    _write_json(args.output_root / "decision.json", decision)
    _write_json(args.output_root / "conditions.json", conditions)
    print(json.dumps(decision, sort_keys=True))
    return 0 if decision["ready"] else 3


if __name__ == "__main__":
    raise SystemExit(main())
