"""Safety and evidence primitives for completing guarded WB-03 rows on a large host."""

from __future__ import annotations

import ctypes
import hashlib
import json
import subprocess
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Iterable


ALLOWED_TEST_IDS = frozenset({"AH-06", "AH-07", "AH-10"})
MINIMUM_INSTALLED_RAM_BYTES = 32 * 1024 ** 3
BLOCKING_PROCESS_NAMES = frozenset({
    "llama-server", "llama-server.exe", "llama-cli", "llama-cli.exe",
    "run_animehacker_retest.py", "run_animehacker_quality.py",
    "run_animehacker_large_host.py",
})


@dataclass(frozen=True)
class HostInputs:
    matrix: Path
    cpu_server: Path
    sycl_server: Path
    granite8_model: Path
    selected: frozenset[str]


@dataclass(frozen=True)
class PreflightResult:
    accepted: bool
    installed_ram_bytes: int
    selected: tuple[str, ...]
    errors: tuple[str, ...]
    file_records: dict[str, dict[str, object]]
    level_zero_devices: tuple[str, ...]


@dataclass(frozen=True)
class ValidatedRow:
    test_id: str
    aggregate: dict[str, dict[str, float | int]]
    quality_scores: dict[str, float]
    quality_mean: float
    model_sha256: str


class _MemoryStatusEx(ctypes.Structure):
    _fields_ = [
        ("dwLength", ctypes.c_ulong),
        ("dwMemoryLoad", ctypes.c_ulong),
        ("ullTotalPhys", ctypes.c_ulonglong),
        ("ullAvailPhys", ctypes.c_ulonglong),
        ("ullTotalPageFile", ctypes.c_ulonglong),
        ("ullAvailPageFile", ctypes.c_ulonglong),
        ("ullTotalVirtual", ctypes.c_ulonglong),
        ("ullAvailVirtual", ctypes.c_ulonglong),
        ("ullAvailExtendedVirtual", ctypes.c_ulonglong),
    ]


def installed_physical_ram_bytes() -> int:
    status = _MemoryStatusEx()
    status.dwLength = ctypes.sizeof(status)
    if not ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(status)):
        raise ctypes.WinError()
    return int(status.ullTotalPhys)


def active_processes() -> tuple[str, ...]:
    completed = subprocess.run(
        ["powershell", "-NoProfile", "-Command", "Get-Process | Select-Object -ExpandProperty ProcessName"],
        capture_output=True, text=True, check=True,
    )
    return tuple(line.strip() for line in completed.stdout.splitlines() if line.strip())


def level_zero_inventory() -> tuple[str, ...]:
    try:
        completed = subprocess.run(["sycl-ls"], capture_output=True, text=True,
                                   timeout=30, check=True)
    except (OSError, subprocess.SubprocessError):
        return ()
    return tuple(line.strip() for line in completed.stdout.splitlines()
                 if "level_zero" in line.lower())


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def preflight(inputs: HostInputs, *, installed_ram_bytes: int | None = None,
              active_process_names: Iterable[str] | None = None,
              level_zero_devices: Iterable[str] | None = None) -> PreflightResult:
    ram = installed_physical_ram_bytes() if installed_ram_bytes is None else installed_ram_bytes
    processes = tuple(active_processes() if active_process_names is None else active_process_names)
    devices = tuple(level_zero_inventory() if level_zero_devices is None else level_zero_devices)
    errors: list[str] = []
    unknown = inputs.selected - ALLOWED_TEST_IDS
    if unknown:
        errors.append(f"unsupported test IDs: {', '.join(sorted(unknown))}")
    if ram < MINIMUM_INSTALLED_RAM_BYTES:
        errors.append("host must have at least 32 GiB installed physical RAM")
    blocking = sorted(name for name in processes if name.lower() in BLOCKING_PROCESS_NAMES)
    if blocking:
        errors.append(f"active test process detected: {', '.join(blocking)}")
    if "AH-10" in inputs.selected and not devices:
        errors.append("AH-10 requires Level Zero device evidence")

    files = {
        "matrix": inputs.matrix,
        "cpu_server": inputs.cpu_server,
        "sycl_server": inputs.sycl_server,
        "granite8_model": inputs.granite8_model,
    }
    records: dict[str, dict[str, object]] = {}
    for label, path in files.items():
        resolved = path.resolve()
        if not resolved.is_file():
            errors.append(f"missing required file {label}: {resolved}")
            continue
        records[label] = {
            "path": str(resolved),
            "bytes": resolved.stat().st_size,
            "sha256": _sha256(resolved),
        }
    return PreflightResult(
        accepted=not errors,
        installed_ram_bytes=ram,
        selected=tuple(sorted(inputs.selected)),
        errors=tuple(errors),
        file_records=records,
        level_zero_devices=devices,
    )


def allocate_run_root(parent: Path, date: str) -> Path:
    prefix = f"AH-LH-{date}-R"
    numbers = []
    if parent.exists():
        for path in parent.iterdir():
            suffix = path.name.removeprefix(prefix)
            if path.is_dir() and path.name.startswith(prefix) and suffix.isdigit():
                numbers.append(int(suffix))
    return parent / f"{prefix}{max(numbers, default=0) + 1:04d}"


def write_manifest(run_root: Path, result: PreflightResult) -> Path:
    run_root.mkdir(parents=True, exist_ok=True)
    path = run_root / "manifest.json"
    with path.open("x", encoding="utf-8") as stream:
        json.dump(asdict(result), stream, indent=2, sort_keys=True)
        stream.write("\n")
    return path


def validate_terminal_row(test_id: str, runtime_summary: object,
                          quality_records: object, manifest: object,
                          cleanup: object) -> ValidatedRow:
    from scripts.testing.run_animehacker_retest import FORMAL_FIELDS

    if test_id not in ALLOWED_TEST_IDS:
        raise ValueError(f"unsupported test ID: {test_id}")
    if not isinstance(runtime_summary, dict) or runtime_summary.get("test_id") != test_id:
        raise ValueError("runtime test ID mismatch")
    samples = runtime_summary.get("samples")
    if not isinstance(samples, list) or len(samples) != 3:
        raise ValueError("exactly three formal samples are required")
    for index, sample in enumerate(samples, 1):
        if not isinstance(sample, dict) or sample.get("valid") is not True:
            raise ValueError(f"formal sample {index} is invalid")
        if sample.get("missing") or sample.get("extended_missing"):
            raise ValueError(f"formal sample {index} reports missing metrics")
        for field in FORMAL_FIELDS:
            if sample.get(field) is None:
                raise ValueError(f"formal sample {index} missing {field}")
        utilization = sample.get("utilization")
        if not isinstance(utilization, dict):
            raise ValueError(f"formal sample {index} missing utilization")
        for device in ("cpu_percent", "gpu_percent"):
            item = utilization.get(device)
            for stat in ("mean", "median", "peak", "sample_count"):
                if not isinstance(item, dict) or item.get(stat) is None:
                    raise ValueError(f"formal sample {index} missing {device} {stat}")

    aggregate = runtime_summary.get("aggregate")
    if not isinstance(aggregate, dict):
        raise ValueError("missing runtime aggregate")
    for field in FORMAL_FIELDS:
        item = aggregate.get(field)
        for stat in ("mean", "median", "min", "max"):
            if not isinstance(item, dict) or item.get(stat) is None:
                raise ValueError(f"aggregate missing {field} {stat}")
    for device in ("cpu_percent", "gpu_percent"):
        item = aggregate.get(device)
        for stat in ("mean", "median", "peak", "sample_count"):
            if not isinstance(item, dict) or item.get(stat) is None:
                raise ValueError(f"aggregate missing {device} {stat}")
    activation = runtime_summary.get("activation")
    if (not isinstance(activation, list) or len(activation) != 3 or
            not all(isinstance(item, dict) and item.get("activated") is True
                    for item in activation)):
        raise ValueError("three proven activation records are required")

    if not isinstance(quality_records, list):
        raise ValueError("quality evidence must contain P1-P6")
    by_prompt = {item.get("prompt_id"): item for item in quality_records
                 if isinstance(item, dict)}
    required_prompts = {f"P{i}" for i in range(1, 7)}
    if set(by_prompt) != required_prompts:
        raise ValueError("quality evidence must contain P1-P6 exactly once")
    scores: dict[str, float] = {}
    for prompt_id in sorted(required_prompts):
        record = by_prompt[prompt_id]
        if record.get("status") != "complete":
            raise ValueError(f"{prompt_id} quality status is not complete")
        output_hash = record.get("output_sha256")
        if not isinstance(output_hash, str) or len(output_hash) != 64:
            raise ValueError(f"{prompt_id} output hash is missing")
        score = record.get("score")
        if not isinstance(score, (int, float)) or isinstance(score, bool) or not 0 <= score <= 10:
            raise ValueError(f"{prompt_id} quality score is invalid")
        scores[prompt_id] = float(score)

    if not isinstance(manifest, dict) or manifest.get("accepted") is not True:
        raise ValueError("host manifest was not accepted")
    if test_id not in manifest.get("selected", []):
        raise ValueError("test ID missing from host manifest")
    runtime_hash = runtime_summary.get("model", {}).get("sha256")
    manifest_hash = manifest.get("file_records", {}).get("granite8_model", {}).get("sha256")
    if not runtime_hash or runtime_hash != manifest_hash:
        raise ValueError("model hash mismatch")

    if (not isinstance(cleanup, dict) or cleanup.get("cleanup_verified") is not True or
            cleanup.get("llama_process_count") != 0 or
            cleanup.get("controller_process_count") != 0):
        raise ValueError("residual process or unverified cleanup")
    return ValidatedRow(test_id=test_id, aggregate=aggregate, quality_scores=scores,
                        quality_mean=sum(scores.values()) / len(scores),
                        model_sha256=runtime_hash)
