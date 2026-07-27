"""Measure two fresh production-core processes and publish strict capability evidence."""

from __future__ import annotations

import argparse
import csv
import ctypes
import hashlib
import json
import math
import os
import platform
import re
import secrets
import shutil
import statistics
import subprocess
import sys
import tempfile
import time
from ctypes import wintypes
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from measure_llama_run import available_ram_bytes, process_memory_bytes  # noqa: E402


MARKER_PREFIX = "TURBOQUANT_REFERENCE_CAPABILITY_JSON="
MARKER_SCHEMA = "openvino-turboquant-reference-capability/v1"
RUN_SCHEMA = "openvino-turboquant-reference-capability-run/v1"
EVIDENCE_SCHEMA = "openvino-turboquant-reference-capability-evidence/v1"
EXPECTED_TEST_NAME = (
    "TurboQuantStatefulGraph.PersistsOnlyCompressedStateForOneHundredSteps"
)
EXPECTED_GTEST_FILTER = f"--gtest_filter={EXPECTED_TEST_NAME}"
EXPECTED_HASH_ALGORITHM = "FNV-1a-64-labelled-step-result-raw-bytes"
REQUIRED_DERIVED_COMMIT = "adb5fbe37e9f8c533461c892a19191f1709ae774"
NONCE_ENVIRONMENT_VARIABLE = "OPENVINO_TURBOQUANT_CAPABILITY_NONCE"
EXACT_INTERVAL_MS = 100
EXACT_TIMEOUT_SECONDS = 300.0
EXACT_MINIMUM_AVAILABLE_RAM_MB = 2048.0

JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000
JOB_OBJECT_BASIC_PROCESS_ID_LIST = 3
JOB_OBJECT_EXTENDED_LIMIT_INFORMATION = 9
PROCESS_TERMINATE = 0x0001
PROCESS_SET_QUOTA = 0x0100
PROCESS_QUERY_LIMITED_INFORMATION = 0x1000
ERROR_MORE_DATA = 234
CREATE_SUSPENDED = 0x00000004
TH32CS_SNAPTHREAD = 0x00000004
THREAD_SUSPEND_RESUME = 0x0002
INVALID_HANDLE_VALUE = ctypes.c_void_p(-1).value


class JOBOBJECT_BASIC_LIMIT_INFORMATION(ctypes.Structure):
    _fields_ = [
        ("PerProcessUserTimeLimit", ctypes.c_int64),
        ("PerJobUserTimeLimit", ctypes.c_int64),
        ("LimitFlags", wintypes.DWORD),
        ("MinimumWorkingSetSize", ctypes.c_size_t),
        ("MaximumWorkingSetSize", ctypes.c_size_t),
        ("ActiveProcessLimit", wintypes.DWORD),
        ("Affinity", ctypes.c_size_t),
        ("PriorityClass", wintypes.DWORD),
        ("SchedulingClass", wintypes.DWORD),
    ]


class IO_COUNTERS(ctypes.Structure):
    _fields_ = [
        ("ReadOperationCount", ctypes.c_ulonglong),
        ("WriteOperationCount", ctypes.c_ulonglong),
        ("OtherOperationCount", ctypes.c_ulonglong),
        ("ReadTransferCount", ctypes.c_ulonglong),
        ("WriteTransferCount", ctypes.c_ulonglong),
        ("OtherTransferCount", ctypes.c_ulonglong),
    ]


class JOBOBJECT_EXTENDED_LIMIT_INFORMATION(ctypes.Structure):
    _fields_ = [
        ("BasicLimitInformation", JOBOBJECT_BASIC_LIMIT_INFORMATION),
        ("IoInfo", IO_COUNTERS),
        ("ProcessMemoryLimit", ctypes.c_size_t),
        ("JobMemoryLimit", ctypes.c_size_t),
        ("PeakProcessMemoryUsed", ctypes.c_size_t),
        ("PeakJobMemoryUsed", ctypes.c_size_t),
    ]


class THREADENTRY32(ctypes.Structure):
    _fields_ = [
        ("dwSize", wintypes.DWORD),
        ("cntUsage", wintypes.DWORD),
        ("th32ThreadID", wintypes.DWORD),
        ("th32OwnerProcessID", wintypes.DWORD),
        ("tpBasePri", wintypes.LONG),
        ("tpDeltaPri", wintypes.LONG),
        ("dwFlags", wintypes.DWORD),
    ]


def _kernel32():
    if os.name != "nt":
        raise RuntimeError("Windows Job Objects are required")
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel32.CreateJobObjectW.argtypes = [ctypes.c_void_p, wintypes.LPCWSTR]
    kernel32.CreateJobObjectW.restype = wintypes.HANDLE
    kernel32.SetInformationJobObject.argtypes = [
        wintypes.HANDLE,
        ctypes.c_int,
        ctypes.c_void_p,
        wintypes.DWORD,
    ]
    kernel32.SetInformationJobObject.restype = wintypes.BOOL
    kernel32.AssignProcessToJobObject.argtypes = [wintypes.HANDLE, wintypes.HANDLE]
    kernel32.AssignProcessToJobObject.restype = wintypes.BOOL
    kernel32.QueryInformationJobObject.argtypes = [
        wintypes.HANDLE,
        ctypes.c_int,
        ctypes.c_void_p,
        wintypes.DWORD,
        ctypes.POINTER(wintypes.DWORD),
    ]
    kernel32.QueryInformationJobObject.restype = wintypes.BOOL
    kernel32.TerminateJobObject.argtypes = [wintypes.HANDLE, wintypes.UINT]
    kernel32.TerminateJobObject.restype = wintypes.BOOL
    kernel32.OpenProcess.argtypes = [
        wintypes.DWORD,
        wintypes.BOOL,
        wintypes.DWORD,
    ]
    kernel32.OpenProcess.restype = wintypes.HANDLE
    kernel32.CloseHandle.argtypes = [wintypes.HANDLE]
    kernel32.CloseHandle.restype = wintypes.BOOL
    kernel32.CreateToolhelp32Snapshot.argtypes = [
        wintypes.DWORD,
        wintypes.DWORD,
    ]
    kernel32.CreateToolhelp32Snapshot.restype = wintypes.HANDLE
    kernel32.Thread32First.argtypes = [
        wintypes.HANDLE,
        ctypes.POINTER(THREADENTRY32),
    ]
    kernel32.Thread32First.restype = wintypes.BOOL
    kernel32.Thread32Next.argtypes = [
        wintypes.HANDLE,
        ctypes.POINTER(THREADENTRY32),
    ]
    kernel32.Thread32Next.restype = wintypes.BOOL
    kernel32.OpenThread.argtypes = [
        wintypes.DWORD,
        wintypes.BOOL,
        wintypes.DWORD,
    ]
    kernel32.OpenThread.restype = wintypes.HANDLE
    kernel32.ResumeThread.argtypes = [wintypes.HANDLE]
    kernel32.ResumeThread.restype = wintypes.DWORD
    return kernel32


def _windows_error(action: str) -> OSError:
    code = ctypes.get_last_error()
    return OSError(code, f"{action} failed: {ctypes.FormatError(code).strip()}")


def _resume_suspended_process(pid: int) -> None:
    """Resume the sole primary thread after the process joins its Job Object."""
    kernel32 = _kernel32()
    snapshot = kernel32.CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0)
    if snapshot == INVALID_HANDLE_VALUE:
        raise _windows_error("CreateToolhelp32Snapshot(threads)")
    thread_ids: list[int] = []
    try:
        entry = THREADENTRY32()
        entry.dwSize = ctypes.sizeof(entry)
        more = kernel32.Thread32First(snapshot, ctypes.byref(entry))
        while more:
            if int(entry.th32OwnerProcessID) == pid:
                thread_ids.append(int(entry.th32ThreadID))
            entry.dwSize = ctypes.sizeof(entry)
            more = kernel32.Thread32Next(snapshot, ctypes.byref(entry))
    finally:
        kernel32.CloseHandle(snapshot)

    if len(thread_ids) != 1:
        raise RuntimeError(
            f"suspended process {pid} exposed {len(thread_ids)} primary threads"
        )
    thread_handle = kernel32.OpenThread(
        THREAD_SUSPEND_RESUME, False, thread_ids[0]
    )
    if not thread_handle:
        raise _windows_error(f"OpenThread({thread_ids[0]})")
    try:
        previous_suspend_count = kernel32.ResumeThread(thread_handle)
        if previous_suspend_count == 0xFFFFFFFF:
            raise _windows_error(f"ResumeThread({thread_ids[0]})")
        if previous_suspend_count != 1:
            raise RuntimeError(
                f"thread {thread_ids[0]} had unexpected suspend count "
                f"{previous_suspend_count}"
            )
    finally:
        kernel32.CloseHandle(thread_handle)


class KillOnCloseJob:
    """One Windows Job Object with governed termination and PID-list queries."""

    def __init__(self, name: str):
        self._kernel32 = _kernel32()
        self._handle = self._kernel32.CreateJobObjectW(None, name)
        if not self._handle:
            raise _windows_error("CreateJobObjectW")
        limits = JOBOBJECT_EXTENDED_LIMIT_INFORMATION()
        limits.BasicLimitInformation.LimitFlags = (
            JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
        )
        if not self._kernel32.SetInformationJobObject(
            self._handle,
            JOB_OBJECT_EXTENDED_LIMIT_INFORMATION,
            ctypes.byref(limits),
            ctypes.sizeof(limits),
        ):
            error = _windows_error("SetInformationJobObject")
            self.close()
            raise error

    def assign_pid(self, pid: int) -> None:
        process_handle = self._kernel32.OpenProcess(
            PROCESS_TERMINATE
            | PROCESS_SET_QUOTA
            | PROCESS_QUERY_LIMITED_INFORMATION,
            False,
            pid,
        )
        if not process_handle:
            raise _windows_error(f"OpenProcess({pid})")
        try:
            if not self._kernel32.AssignProcessToJobObject(
                self._handle, process_handle
            ):
                raise _windows_error(f"AssignProcessToJobObject({pid})")
        finally:
            self._kernel32.CloseHandle(process_handle)

    def active_pids(self) -> list[int]:
        capacity = 64
        while capacity <= 4096:
            class PROCESS_ID_LIST(ctypes.Structure):
                _fields_ = [
                    ("NumberOfAssignedProcesses", wintypes.DWORD),
                    ("NumberOfProcessIdsInList", wintypes.DWORD),
                    ("ProcessIdList", ctypes.c_size_t * capacity),
                ]

            process_ids = PROCESS_ID_LIST()
            returned = wintypes.DWORD()
            ok = self._kernel32.QueryInformationJobObject(
                self._handle,
                JOB_OBJECT_BASIC_PROCESS_ID_LIST,
                ctypes.byref(process_ids),
                ctypes.sizeof(process_ids),
                ctypes.byref(returned),
            )
            if not ok:
                error_code = ctypes.get_last_error()
                if error_code == ERROR_MORE_DATA:
                    capacity *= 2
                    continue
                raise _windows_error("QueryInformationJobObject")
            assigned = int(process_ids.NumberOfAssignedProcesses)
            listed = int(process_ids.NumberOfProcessIdsInList)
            if assigned > capacity or listed > capacity or assigned != listed:
                capacity *= 2
                continue
            return [int(process_ids.ProcessIdList[index]) for index in range(listed)]
        raise RuntimeError("Job Object PID query exceeded the 4096-process limit")

    def terminate(self, exit_code: int = 1) -> None:
        if not self._kernel32.TerminateJobObject(self._handle, exit_code):
            raise _windows_error("TerminateJobObject")

    def close(self) -> None:
        if getattr(self, "_handle", None):
            if not self._kernel32.CloseHandle(self._handle):
                raise _windows_error("CloseHandle(JobObject)")
            self._handle = None


def _utc_now() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="microseconds").replace(
        "+00:00", "Z"
    )


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def validate_runtime_library_dir(path: Path) -> Path:
    """Require the explicit OpenVINO runtime directory and its core DLL."""
    candidate = Path(path)
    if not candidate.is_dir():
        raise ValueError(f"runtime library directory does not exist: {candidate}")
    resolved = candidate.resolve()
    if not (resolved / "openvino.dll").is_file():
        raise ValueError(
            f"runtime library directory does not contain openvino.dll: {resolved}"
        )
    return resolved


def absolute_invocation_path(path: Path) -> Path:
    """Make a supplied path absolute without resolving a substituted drive."""
    return Path(path).absolute()


def workload_environment(
    run_nonce: str, runtime_library_dir: Path | None = None
) -> dict[str, str]:
    """Return a copied child environment without mutating the controller."""
    environment = os.environ.copy()
    environment[NONCE_ENVIRONMENT_VARIABLE] = run_nonce
    if runtime_library_dir is not None:
        runtime_library_dir = validate_runtime_library_dir(runtime_library_dir)
        environment["PATH"] = (
            str(runtime_library_dir)
            + os.pathsep
            + environment.get("PATH", "")
        )
    return environment


def _is_finite_number(value: Any) -> bool:
    return (
        isinstance(value, (int, float))
        and not isinstance(value, bool)
        and math.isfinite(float(value))
    )


def summarize_samples(values: list[float]) -> dict:
    """Return strict descriptive statistics for non-empty finite samples."""
    if not values:
        raise ValueError("sample series is absent")
    if not all(_is_finite_number(value) for value in values):
        raise ValueError("sample series contains a non-finite value")
    samples = [float(value) for value in values]
    maximum = max(samples)
    return {
        "count": len(samples),
        "mean": statistics.fmean(samples),
        "median": statistics.median(samples),
        "minimum": min(samples),
        "maximum": maximum,
        "peak": maximum,
    }


def next_sampling_deadline(
    previous_deadline: float, interval_seconds: float, sample_finished: float
) -> float:
    """Advance on the original monotonic grid, skipping only elapsed slots."""
    if interval_seconds <= 0:
        raise ValueError("sampling interval must be positive")
    intervals = max(
        1,
        math.floor(
            (sample_finished - previous_deadline) / interval_seconds
        )
        + 1,
    )
    return previous_deadline + (intervals * interval_seconds)


def _expected_states(step: int) -> list[dict]:
    states: list[dict] = []
    for layer in range(2):
        for kind, payload_width in (("key", 3), ("value", 4)):
            prefix = f"layer_{layer}_sdpa.{kind}"
            states.extend(
                [
                    {
                        "name": f"{prefix}.payload",
                        "type": "u8",
                        "shape": [1, 2, step, payload_width],
                        "byte_count": 2 * step * payload_width,
                    },
                    {
                        "name": f"{prefix}.norm",
                        "type": "f32",
                        "shape": [1, 2, step, 1],
                        "byte_count": 2 * step * 4,
                    },
                    {
                        "name": f"{prefix}.meta",
                        "type": "i32",
                        "shape": [1, 2, step, 1],
                        "byte_count": 2 * step * 4,
                    },
                ]
            )
    return states


EXPECTED_ALLOCATIONS = {
    1: {
        "payload_bytes": 28,
        "norm_bytes": 32,
        "metadata_bytes": 32,
        "full_precision_equivalent_bytes": 256,
        "decoded_scratch_bytes": 256,
    },
    2: {
        "payload_bytes": 56,
        "norm_bytes": 64,
        "metadata_bytes": 64,
        "full_precision_equivalent_bytes": 512,
        "decoded_scratch_bytes": 512,
    },
    50: {
        "payload_bytes": 1400,
        "norm_bytes": 1600,
        "metadata_bytes": 1600,
        "full_precision_equivalent_bytes": 12800,
        "decoded_scratch_bytes": 12800,
    },
    100: {
        "payload_bytes": 2800,
        "norm_bytes": 3200,
        "metadata_bytes": 3200,
        "full_precision_equivalent_bytes": 25600,
        "decoded_scratch_bytes": 25600,
    },
}


def _validate_lifetime_marker(
    marker: dict, expected_nonce: str | None = None
) -> dict:
    if not isinstance(marker, dict):
        raise ValueError("capability marker JSON must be an object")
    required = {
        "schema",
        "device",
        "runtime_layer_type",
        "reference_operation_count",
        "matched_state_count",
        "steps",
        "hash_algorithm",
        "output_hashes",
        "repeat_hash_matches",
        "snapshots",
        "no_full_precision_selected_state",
        "run_nonce",
    }
    missing = sorted(required - marker.keys())
    if missing:
        raise ValueError(f"capability marker missing field(s): {', '.join(missing)}")
    unexpected = sorted(marker.keys() - required)
    if unexpected:
        raise ValueError(
            f"capability marker has unexpected field(s): {', '.join(unexpected)}"
        )
    if marker["schema"] != MARKER_SCHEMA:
        raise ValueError("capability marker schema is invalid")
    if marker["device"] != "CPU":
        raise ValueError("capability marker device must be CPU")
    if marker["runtime_layer_type"] != "Reference":
        raise ValueError("capability marker runtime layer type must be Reference")
    if marker["reference_operation_count"] != 4:
        raise ValueError("capability marker reference operation count must be four")
    if marker["matched_state_count"] != 4:
        raise ValueError("capability marker matched state count must be four")
    if marker["steps"] != 100:
        raise ValueError("capability marker must report exactly 100 steps")
    if marker["hash_algorithm"] != EXPECTED_HASH_ALGORITHM:
        raise ValueError("capability marker hash algorithm is invalid")
    hashes = marker["output_hashes"]
    if (
        not isinstance(hashes, list)
        or len(hashes) != 2
        or not all(
            isinstance(value, str)
            and re.fullmatch(r"[0-9a-f]{16}", value) is not None
            for value in hashes
        )
    ):
        raise ValueError("capability marker output hash array is invalid")
    if marker["repeat_hash_matches"] is not True or hashes[0] != hashes[1]:
        raise ValueError("capability marker reconciliation flag or hashes disagree")
    if marker["no_full_precision_selected_state"] is not True:
        raise ValueError("capability marker retained a full-precision selected state")
    nonce = marker["run_nonce"]
    if (
        not isinstance(nonce, str)
        or re.fullmatch(r"[0-9a-f]{32}", nonce) is None
        or (expected_nonce is not None and nonce != expected_nonce)
    ):
        raise ValueError("capability marker run nonce is unexpected")

    snapshots = marker["snapshots"]
    if not isinstance(snapshots, list) or len(snapshots) != 4:
        raise ValueError("capability marker must contain exactly four snapshots")
    expected_steps = list(EXPECTED_ALLOCATIONS)
    actual_steps = [
        snapshot.get("step") if isinstance(snapshot, dict) else None
        for snapshot in snapshots
    ]
    if actual_steps != expected_steps:
        raise ValueError("capability marker snapshots have unexpected steps")
    snapshot_fields = {
        "step",
        "states",
        "payload_bytes",
        "norm_bytes",
        "metadata_bytes",
        "full_precision_equivalent_bytes",
        "decoded_scratch_bytes",
    }
    for snapshot in snapshots:
        if set(snapshot) != snapshot_fields:
            raise ValueError("capability marker snapshot fields are invalid")
        step = snapshot["step"]
        if snapshot["states"] != _expected_states(step):
            raise ValueError(
                f"capability marker selected states are invalid at step {step}"
            )
        for field, expected in EXPECTED_ALLOCATIONS[step].items():
            if snapshot[field] != expected:
                raise ValueError(
                    f"capability marker allocation {field} is invalid at step {step}"
                )
    return marker


def parse_lifetime_marker(
    stdout: str, expected_nonce: str | None = None
) -> dict:
    """Parse one exact Task 4 marker and validate every proof field."""
    lines = [
        line
        for line in stdout.splitlines()
        if line.startswith(MARKER_PREFIX)
    ]
    if len(lines) != 1:
        raise ValueError(
            f"expected exactly one {MARKER_PREFIX} line, found {len(lines)}"
        )
    payload = lines[0][len(MARKER_PREFIX) :]
    try:
        marker = json.loads(payload)
    except json.JSONDecodeError as error:
        raise ValueError("capability marker does not contain valid JSON") from error
    return _validate_lifetime_marker(marker, expected_nonce)


def atomic_write_json(path: Path, value: dict) -> None:
    """Flush and fsync a same-directory temporary file before atomic replace."""
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary_path: Path | None = None
    try:
        with tempfile.NamedTemporaryFile(
            mode="w",
            encoding="utf-8",
            newline="\n",
            prefix=".tmp-",
            suffix="",
            dir=path.parent,
            delete=False,
        ) as handle:
            temporary_path = Path(handle.name)
            json.dump(value, handle, indent=2, sort_keys=True, allow_nan=False)
            handle.write("\n")
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temporary_path, path)
        temporary_path = None
    finally:
        if temporary_path is not None:
            temporary_path.unlink(missing_ok=True)


def _environment_identity(runtime_library_dir: Path | None = None) -> dict:
    validated_runtime_dir = (
        validate_runtime_library_dir(runtime_library_dir)
        if runtime_library_dir is not None
        else None
    )
    identity = {
        "computer_name": os.environ.get("COMPUTERNAME", ""),
        "operating_system": platform.platform(),
        "machine": platform.machine(),
        "processor": platform.processor(),
        "processor_identifier": os.environ.get("PROCESSOR_IDENTIFIER", ""),
        "logical_processor_count": os.cpu_count(),
        "python_executable": str(Path(sys.executable).resolve()),
        "python_version": platform.python_version(),
        "powershell_executable": shutil.which("powershell.exe") or "",
        "working_directory": str(Path.cwd().resolve()),
        "capability_nonce_variable": NONCE_ENVIRONMENT_VARIABLE,
        "runtime_library_dir": (
            str(validated_runtime_dir) if validated_runtime_dir is not None else None
        ),
        "runtime_openvino_dll_sha256": (
            _sha256_file(validated_runtime_dir / "openvino.dll")
            if validated_runtime_dir is not None
            else None
        ),
    }
    encoded = json.dumps(
        identity, sort_keys=True, separators=(",", ":"), ensure_ascii=True
    ).encode("utf-8")
    identity["identity_sha256"] = hashlib.sha256(encoded).hexdigest()
    return identity


def _append_error(errors: list[str], message: str) -> None:
    if message not in errors:
        errors.append(message)


def _parse_bool(value: str, field: str) -> bool:
    normalized = value.strip().lower()
    if normalized == "true":
        return True
    if normalized == "false":
        return False
    raise ValueError(f"{field} must be true or false")


UTILIZATION_VALUE_FIELDS = (
    "cpu_percent",
    "gpu_percent",
    "gpu_engine_count",
    "gpu_dedicated_mb",
    "gpu_shared_mb",
)
FIRST_CPU_SAMPLE_DEFINITION = "lifetime_average_since_process_start"
INTERVAL_CPU_SAMPLE_DEFINITION = "interval_delta"


def _read_utilization(path: Path) -> tuple[dict | None, dict, list[str]]:
    errors: list[str] = []
    samples: dict[str, list[float]] = {
        field: [] for field in UTILIZATION_VALUE_FIELDS
    }
    samples["cpu_sample_definition"] = []
    if not path.exists():
        return None, samples, ["utilization CSV is missing"]
    try:
        with path.open(encoding="utf-8-sig", newline="") as handle:
            reader = csv.DictReader(handle)
            required = {
                "timestamp_utc",
                *UTILIZATION_VALUE_FIELDS,
                "cpu_sample_definition",
                "gpu_engine_query_ok",
                "gpu_memory_query_ok",
            }
            if reader.fieldnames is None or not required.issubset(reader.fieldnames):
                return (
                    None,
                    samples,
                    ["utilization CSV is missing required counter-status columns"],
                )
            rows = list(reader)
    except (OSError, csv.Error) as error:
        return None, samples, [f"utilization CSV could not be read: {error}"]

    engine_success_count = 0
    memory_success_count = 0
    for index, row in enumerate(rows, start=1):
        try:
            cpu = float(row["cpu_percent"])
            if not math.isfinite(cpu) or not 0.0 <= cpu <= 100.0:
                raise ValueError("CPU value is outside [0, 100]")
            samples["cpu_percent"].append(cpu)
            cpu_definition = row["cpu_sample_definition"].strip()
            expected_definition = (
                FIRST_CPU_SAMPLE_DEFINITION
                if index == 1
                else INTERVAL_CPU_SAMPLE_DEFINITION
            )
            if cpu_definition != expected_definition:
                raise ValueError(
                    f"CPU sample definition must be {expected_definition}"
                )
            samples["cpu_sample_definition"].append(cpu_definition)
            engine_ok = _parse_bool(
                row["gpu_engine_query_ok"], "gpu_engine_query_ok"
            )
            memory_ok = _parse_bool(
                row["gpu_memory_query_ok"], "gpu_memory_query_ok"
            )
            if engine_ok:
                gpu = float(row["gpu_percent"])
                engine_count = float(row["gpu_engine_count"])
                if (
                    not math.isfinite(gpu)
                    or not 0.0 <= gpu <= 100.0
                    or not math.isfinite(engine_count)
                    or engine_count < 0.0
                    or not engine_count.is_integer()
                ):
                    raise ValueError("GPU engine values are invalid")
                samples["gpu_percent"].append(gpu)
                samples["gpu_engine_count"].append(engine_count)
                engine_success_count += 1
            else:
                _append_error(errors, "GPU engine counter query failed")
                if row["gpu_percent"].strip() or row["gpu_engine_count"].strip():
                    _append_error(
                        errors,
                        "numeric GPU engine evidence was emitted for a failed query",
                    )
            if memory_ok:
                dedicated = float(row["gpu_dedicated_mb"])
                shared = float(row["gpu_shared_mb"])
                if (
                    not math.isfinite(dedicated)
                    or dedicated < 0.0
                    or not math.isfinite(shared)
                    or shared < 0.0
                ):
                    raise ValueError("GPU memory values are invalid")
                samples["gpu_dedicated_mb"].append(dedicated)
                samples["gpu_shared_mb"].append(shared)
                memory_success_count += 1
            else:
                _append_error(errors, "GPU memory counter query failed")
                if (
                    row["gpu_dedicated_mb"].strip()
                    or row["gpu_shared_mb"].strip()
                ):
                    _append_error(
                        errors,
                        "numeric GPU memory evidence was emitted for a failed query",
                    )
        except (KeyError, TypeError, ValueError) as error:
            _append_error(
                errors, f"utilization CSV row {index} is invalid: {error}"
            )

    if not rows:
        _append_error(errors, "utilization CSV has no real counter sample")
    summary: dict[str, Any] = {"row_count": len(rows)}
    for field in UTILIZATION_VALUE_FIELDS:
        try:
            summary[field] = summarize_samples(samples[field])
        except ValueError:
            summary[field] = None
            _append_error(errors, f"{field} utilization evidence is absent")
    summary["gpu_engine_query"] = {
        "success_count": engine_success_count,
        "failure_count": len(rows) - engine_success_count,
        "all_succeeded": bool(rows) and engine_success_count == len(rows),
    }
    summary["gpu_memory_query"] = {
        "success_count": memory_success_count,
        "failure_count": len(rows) - memory_success_count,
        "all_succeeded": bool(rows) and memory_success_count == len(rows),
    }
    summary["cpu_sample_definitions"] = {
        FIRST_CPU_SAMPLE_DEFINITION: samples["cpu_sample_definition"].count(
            FIRST_CPU_SAMPLE_DEFINITION
        ),
        INTERVAL_CPU_SAMPLE_DEFINITION: samples["cpu_sample_definition"].count(
            INTERVAL_CPU_SAMPLE_DEFINITION
        ),
    }
    return summary, samples, errors


def _parse_gtest_json(path: Path) -> dict:
    if not path.exists():
        raise ValueError("GTest JSON is missing")
    try:
        value = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as error:
        raise ValueError("GTest JSON is invalid") from error
    required_counts = ("tests", "failures", "errors", "disabled")
    if not isinstance(value, dict) or any(
        not isinstance(value.get(field), int) or isinstance(value.get(field), bool)
        for field in required_counts
    ):
        raise ValueError("GTest JSON count fields are invalid")
    suites = value.get("testsuites")
    if not isinstance(suites, list):
        raise ValueError("GTest JSON testsuites are missing")
    cases: list[dict] = []
    for suite in suites:
        if not isinstance(suite, dict) or not isinstance(
            suite.get("testsuite"), list
        ):
            raise ValueError("GTest JSON suite structure is invalid")
        for case in suite["testsuite"]:
            if not isinstance(case, dict):
                raise ValueError("GTest JSON test case is invalid")
            cases.append(case)
    passed = value["tests"] - value["failures"] - value["errors"] - value["disabled"]
    if (
        value["tests"] != 1
        or passed != 1
        or value["failures"] != 0
        or value["errors"] != 0
        or value["disabled"] != 0
        or len(cases) != 1
    ):
        raise ValueError("GTest JSON must contain exactly one passed GTest")
    case = cases[0]
    test_name = f"{case.get('classname', '')}.{case.get('name', '')}"
    if (
        test_name != EXPECTED_TEST_NAME
        or case.get("status") != "RUN"
        or case.get("result") != "COMPLETED"
    ):
        raise ValueError("GTest JSON does not contain the required passed GTest")
    return {
        "tests": 1,
        "passed": 1,
        "failures": 0,
        "errors": 0,
        "disabled": 0,
        "test_name": test_name,
    }


def _taskkill(pid: int) -> dict:
    command = ["taskkill.exe", "/PID", str(pid), "/T", "/F"]
    completed = subprocess.run(
        command,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        check=False,
        timeout=30,
    )
    return {
        "command": command,
        "exit_code": completed.returncode,
        "stdout": completed.stdout,
        "stderr": completed.stderr,
    }


def _cleanup_job(
    job: KillOnCloseJob | None,
    process: subprocess.Popen | None,
    label: str,
    errors: list[str],
    emergency_actions: list[dict],
) -> tuple[dict, bool]:
    evidence = {
        "setup_ok": job is not None and process is not None,
        "query_ok": False,
        "terminate_job_called": False,
        "queried_active_process_count_after_cleanup": None,
        "survivor_pids_after_cleanup": None,
    }
    emergency = False
    if job is None:
        _append_error(errors, f"{label} Job Object setup failed")
        return evidence, emergency
    try:
        active = job.active_pids()
        evidence["query_ok"] = True
        if active:
            job.terminate(1)
            evidence["terminate_job_called"] = True
        deadline = time.monotonic() + 10.0
        survivors = active
        while survivors and time.monotonic() < deadline:
            time.sleep(0.05)
            survivors = job.active_pids()
        evidence["queried_active_process_count_after_cleanup"] = len(survivors)
        evidence["survivor_pids_after_cleanup"] = sorted(survivors)
        if survivors:
            _append_error(errors, f"{label} Job Object cleanup left survivors")
            if process is not None:
                action = _taskkill(process.pid)
                action["reason"] = f"{label} Job Object cleanup fallback"
                emergency_actions.append(action)
                emergency = True
                try:
                    survivors = job.active_pids()
                    evidence["queried_active_process_count_after_cleanup"] = len(
                        survivors
                    )
                    evidence["survivor_pids_after_cleanup"] = sorted(survivors)
                except OSError as error:
                    evidence["query_ok"] = False
                    _append_error(
                        errors,
                        f"{label} Job Object query failed after taskkill: {error}",
                    )
    except (OSError, RuntimeError) as error:
        evidence["query_ok"] = False
        _append_error(errors, f"{label} Job Object query/cleanup failed: {error}")
        if process is not None and process.poll() is None:
            action = _taskkill(process.pid)
            action["reason"] = f"{label} Job Object query failure fallback"
            emergency_actions.append(action)
            emergency = True
    return evidence, emergency


def _initial_run_record(
    command: list[str],
    output_dir: Path,
    timeout_seconds: float,
    interval_ms: int,
    minimum_available_ram_mb: float,
    run_nonce: str,
    environment_identity: dict,
) -> dict:
    return {
        "schema": RUN_SCHEMA,
        "run_id": output_dir.name,
        "output_directory": str(output_dir.resolve()),
        "command": list(command),
        "environment_identity": environment_identity,
        "run_nonce": run_nonce,
        "root_pid": None,
        "observed_pids": [],
        "child_workload_observed": False,
        "launch_governance": {
            "workload_created_suspended": False,
            "workload_assigned_before_resume": False,
            "sampler_created_suspended": False,
            "sampler_assigned_before_resume": False,
        },
        "started_utc": None,
        "ended_utc": None,
        "elapsed_seconds": None,
        "sampling_interval_ms": interval_ms,
        "timeout_seconds": timeout_seconds,
        "minimum_available_ram_mb": minimum_available_ram_mb,
        "memory_sample_count": 0,
        "peak_working_set_bytes": None,
        "peak_private_bytes": None,
        "available_ram_bytes": {
            "before": None,
            "minimum": None,
            "after": None,
        },
        "utilization_summary": None,
        "utilization_samples": {
            **{field: [] for field in UTILIZATION_VALUE_FIELDS},
            "cpu_sample_definition": [],
        },
        "marker": None,
        "gtest": None,
        "exit_code": None,
        "timed_out": False,
        "low_memory_stop": False,
        "emergency_stop": False,
        "cleanup_duration_seconds": None,
        "sampler_exit_code": None,
        "job_object": {
            "setup_ok": False,
            "query_ok": False,
            "queried_active_process_count_after_cleanup": None,
            "survivor_pids_after_cleanup": None,
        },
        "sampler_job_object": {
            "setup_ok": False,
            "query_ok": False,
            "queried_active_process_count_after_cleanup": None,
            "survivor_pids_after_cleanup": None,
        },
        "emergency_actions": [],
        "validation_errors": [],
        "valid": False,
        "artifacts": {
            "command": "command.json",
            "environment": "environment.json",
            "stdout": "stdout.txt",
            "stderr": "stderr.txt",
            "sampler_stdout": "sampler.stdout.txt",
            "sampler_stderr": "sampler.stderr.txt",
            "memory_samples": "memory.jsonl",
            "utilization": "utilization.csv",
            "gtest": "gtest.json",
            "run": "run.json",
        },
    }


def run_one(
    command: list[str],
    output_dir: Path,
    timeout_seconds: float,
    interval_ms: int,
    minimum_available_ram_mb: float,
    run_nonce: str,
    runtime_library_dir: Path,
) -> dict:
    """Run one fresh governed process and preserve all raw measurement artifacts."""
    if not command or not all(isinstance(value, str) and value for value in command):
        raise ValueError("command must contain non-empty strings")
    if interval_ms != EXACT_INTERVAL_MS:
        raise ValueError("the capability sampling interval must be exactly 100 ms")
    if timeout_seconds <= 0:
        raise ValueError("timeout_seconds must be positive")
    if minimum_available_ram_mb < 0:
        raise ValueError("minimum_available_ram_mb must be non-negative")
    if re.fullmatch(r"[0-9a-f]{32}", run_nonce) is None:
        raise ValueError("run_nonce must be exactly 32 lowercase hexadecimal digits")
    validated_runtime_dir = validate_runtime_library_dir(runtime_library_dir)

    output_dir = Path(output_dir)
    output_dir.mkdir(parents=True, exist_ok=False)
    paths = {
        "command": output_dir / "command.json",
        "environment": output_dir / "environment.json",
        "stdout": output_dir / "stdout.txt",
        "stderr": output_dir / "stderr.txt",
        "sampler_stdout": output_dir / "sampler.stdout.txt",
        "sampler_stderr": output_dir / "sampler.stderr.txt",
        "memory": output_dir / "memory.jsonl",
        "utilization": output_dir / "utilization.csv",
        "ready": output_dir / "utilization.ready",
        "stop": output_dir / "utilization.stop",
        "gtest": output_dir / "gtest.json",
        "run": output_dir / "run.json",
    }
    identity = _environment_identity(validated_runtime_dir)
    record = _initial_run_record(
        command,
        output_dir,
        timeout_seconds,
        interval_ms,
        minimum_available_ram_mb,
        run_nonce,
        identity,
    )
    errors: list[str] = record["validation_errors"]
    atomic_write_json(
        paths["command"],
        {
            "command": command,
            "working_directory": str(Path.cwd().resolve()),
            "run_nonce_environment_variable": NONCE_ENVIRONMENT_VARIABLE,
            "runtime_library_dir": (
                str(validated_runtime_dir)
                if validated_runtime_dir is not None
                else None
            ),
            "path_prepend_applied_to": "workload-child-only",
        },
    )
    atomic_write_json(paths["environment"], identity)
    for path in (
        paths["stdout"],
        paths["stderr"],
        paths["sampler_stdout"],
        paths["sampler_stderr"],
        paths["memory"],
        paths["utilization"],
    ):
        path.touch()
    paths["ready"].unlink(missing_ok=True)
    paths["stop"].unlink(missing_ok=True)

    available_before = available_ram_bytes()
    record["available_ram_bytes"]["before"] = available_before
    floor_bytes = int(minimum_available_ram_mb * 1024 * 1024)
    if available_before is None:
        _append_error(errors, "available RAM query failed before launch")
    elif available_before < floor_bytes:
        _append_error(
            errors,
            "available RAM before launch is below the configured floor",
        )

    workload_job: KillOnCloseJob | None = None
    sampler_job: KillOnCloseJob | None = None
    workload: subprocess.Popen | None = None
    sampler: subprocess.Popen | None = None
    workload_assigned = False
    sampler_assigned = False
    file_handles: list[Any] = []
    memory_handle = None
    memory_samples: list[dict] = []
    observed_pids: set[int] = set()
    started_monotonic: float | None = None
    cleanup_started: float | None = None

    if not errors:
        try:
            workload_job = KillOnCloseJob(
                f"OpenVINOTurboQuantWorkload-{run_nonce}"
            )
            sampler_job = KillOnCloseJob(
                f"OpenVINOTurboQuantSampler-{run_nonce}"
            )
            stdout_handle = paths["stdout"].open("wb")
            stderr_handle = paths["stderr"].open("wb")
            sampler_stdout_handle = paths["sampler_stdout"].open("wb")
            sampler_stderr_handle = paths["sampler_stderr"].open("wb")
            memory_handle = paths["memory"].open("w", encoding="utf-8", newline="\n")
            file_handles.extend(
                [
                    stdout_handle,
                    stderr_handle,
                    sampler_stdout_handle,
                    sampler_stderr_handle,
                    memory_handle,
                ]
            )
            environment = workload_environment(run_nonce, validated_runtime_dir)
            record["started_utc"] = _utc_now()
            started_monotonic = time.monotonic()
            workload = subprocess.Popen(
                command,
                cwd=Path.cwd(),
                env=environment,
                stdin=subprocess.DEVNULL,
                stdout=stdout_handle,
                stderr=stderr_handle,
                creationflags=(
                    subprocess.CREATE_NEW_PROCESS_GROUP | CREATE_SUSPENDED
                ),
            )
            record["launch_governance"]["workload_created_suspended"] = True
            record["root_pid"] = workload.pid
            workload_job.assign_pid(workload.pid)
            workload_assigned = True
            _resume_suspended_process(workload.pid)
            record["launch_governance"][
                "workload_assigned_before_resume"
            ] = True

            sampler_command = [
                shutil.which("powershell.exe") or "powershell.exe",
                "-NoProfile",
                "-NonInteractive",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                str(SCRIPT_DIR / "collect_process_utilization.ps1"),
                "-ProcessId",
                str(workload.pid),
                "-OutputPath",
                str(paths["utilization"]),
                "-ReadyPath",
                str(paths["ready"]),
                "-StopPath",
                str(paths["stop"]),
                "-IntervalMilliseconds",
                str(interval_ms),
            ]
            sampler = subprocess.Popen(
                sampler_command,
                cwd=Path.cwd(),
                stdin=subprocess.DEVNULL,
                stdout=sampler_stdout_handle,
                stderr=sampler_stderr_handle,
                creationflags=(
                    subprocess.CREATE_NEW_PROCESS_GROUP | CREATE_SUSPENDED
                ),
            )
            record["launch_governance"]["sampler_created_suspended"] = True
            sampler_job.assign_pid(sampler.pid)
            sampler_assigned = True
            _resume_suspended_process(sampler.pid)
            record["launch_governance"][
                "sampler_assigned_before_resume"
            ] = True

            interval_seconds = interval_ms / 1000.0
            next_sample = time.monotonic()
            while True:
                now = time.monotonic()
                if now - started_monotonic >= timeout_seconds:
                    record["timed_out"] = True
                    break
                if workload.poll() is not None:
                    break
                if now < next_sample:
                    try:
                        workload.wait(timeout=min(next_sample - now, 0.05))
                    except subprocess.TimeoutExpired:
                        pass
                    continue

                try:
                    active_pids = workload_job.active_pids()
                except (OSError, RuntimeError) as error:
                    _append_error(
                        errors, f"workload Job Object sampling query failed: {error}"
                    )
                    break
                observed_pids.update(active_pids)
                if any(pid != workload.pid for pid in active_pids):
                    record["child_workload_observed"] = True

                available = available_ram_bytes()
                if available is None:
                    _append_error(errors, "available RAM query failed in-run")
                    break
                if available < floor_bytes:
                    record["low_memory_stop"] = True
                    break

                process_rows = []
                for pid in active_pids:
                    memory = process_memory_bytes(pid)
                    if memory is None:
                        _append_error(
                            errors,
                            f"memory query failed for active workload PID {pid}",
                        )
                        continue
                    working_set, private_bytes = memory
                    process_rows.append(
                        {
                            "pid": pid,
                            "working_set_bytes": working_set,
                            "private_bytes": private_bytes,
                        }
                    )
                if active_pids and len(process_rows) == len(active_pids):
                    row = {
                        "timestamp_utc": _utc_now(),
                        "elapsed_seconds": time.monotonic() - started_monotonic,
                        "active_pids": sorted(active_pids),
                        "processes": process_rows,
                        "working_set_bytes": sum(
                            item["working_set_bytes"] for item in process_rows
                        ),
                        "private_bytes": sum(
                            item["private_bytes"] for item in process_rows
                        ),
                        "available_ram_bytes": available,
                    }
                    memory_samples.append(row)
                    memory_handle.write(
                        json.dumps(row, sort_keys=True, allow_nan=False) + "\n"
                    )
                    memory_handle.flush()
                sample_finished = time.monotonic()
                next_sample = next_sampling_deadline(
                    next_sample,
                    interval_seconds,
                    sample_finished,
                )
        except Exception as error:
            _append_error(
                errors,
                f"fresh-process execution failed: {type(error).__name__}: {error}",
            )
        finally:
            cleanup_started = time.monotonic()
            try:
                paths["stop"].write_text("stop\n", encoding="ascii")
            except OSError as error:
                _append_error(errors, f"sampler stop signal failed: {error}")

            if sampler is not None and sampler_assigned:
                try:
                    sampler.wait(timeout=10.0)
                except subprocess.TimeoutExpired:
                    pass
            if sampler is not None and not sampler_assigned and sampler.poll() is None:
                action = _taskkill(sampler.pid)
                action["reason"] = "sampler assignment failure fallback"
                record["emergency_actions"].append(action)
                record["emergency_stop"] = True
            sampler_evidence, sampler_emergency = _cleanup_job(
                sampler_job,
                sampler if sampler_assigned else None,
                "sampler",
                errors,
                record["emergency_actions"],
            )
            sampler_evidence["setup_ok"] = sampler_assigned
            record["sampler_job_object"] = sampler_evidence
            record["emergency_stop"] = (
                record["emergency_stop"] or sampler_emergency
            )

            if workload is not None and not workload_assigned and workload.poll() is None:
                action = _taskkill(workload.pid)
                action["reason"] = "workload assignment failure fallback"
                record["emergency_actions"].append(action)
                record["emergency_stop"] = True
            workload_evidence, workload_emergency = _cleanup_job(
                workload_job,
                workload if workload_assigned else None,
                "workload",
                errors,
                record["emergency_actions"],
            )
            workload_evidence["setup_ok"] = workload_assigned
            record["job_object"] = workload_evidence
            record["emergency_stop"] = (
                record["emergency_stop"] or workload_emergency
            )

            if sampler is not None:
                try:
                    sampler.wait(timeout=5.0)
                except subprocess.TimeoutExpired:
                    action = _taskkill(sampler.pid)
                    action["reason"] = "sampler wait fallback"
                    record["emergency_actions"].append(action)
                    record["emergency_stop"] = True
                    sampler.wait(timeout=5.0)
                record["sampler_exit_code"] = sampler.returncode
            if workload is not None:
                try:
                    workload.wait(timeout=5.0)
                except subprocess.TimeoutExpired:
                    action = _taskkill(workload.pid)
                    action["reason"] = "workload wait fallback"
                    record["emergency_actions"].append(action)
                    record["emergency_stop"] = True
                    workload.wait(timeout=5.0)
                record["exit_code"] = workload.returncode

            for handle in file_handles:
                try:
                    handle.close()
                except OSError as error:
                    _append_error(errors, f"artifact handle close failed: {error}")
            if sampler_job is not None:
                try:
                    sampler_job.close()
                except OSError as error:
                    _append_error(errors, f"sampler Job Object close failed: {error}")
            if workload_job is not None:
                try:
                    workload_job.close()
                except OSError as error:
                    _append_error(errors, f"workload Job Object close failed: {error}")
            record["cleanup_duration_seconds"] = time.monotonic() - cleanup_started
            if started_monotonic is not None:
                record["elapsed_seconds"] = time.monotonic() - started_monotonic
                record["ended_utc"] = _utc_now()

    available_after = available_ram_bytes()
    record["available_ram_bytes"]["after"] = available_after
    available_values = [
        value
        for value in [
            available_before,
            *(row["available_ram_bytes"] for row in memory_samples),
            available_after,
        ]
        if value is not None
    ]
    record["available_ram_bytes"]["minimum"] = (
        min(available_values) if available_values else None
    )
    record["observed_pids"] = sorted(observed_pids)
    record["memory_sample_count"] = len(memory_samples)
    if memory_samples:
        record["peak_working_set_bytes"] = max(
            row["working_set_bytes"] for row in memory_samples
        )
        record["peak_private_bytes"] = max(
            row["private_bytes"] for row in memory_samples
        )
    else:
        _append_error(errors, "memory evidence is missing")

    if available_after is None:
        _append_error(errors, "available RAM query failed after run")
    if record["available_ram_bytes"]["minimum"] is None:
        _append_error(errors, "available RAM evidence is missing")
    elif record["available_ram_bytes"]["minimum"] < floor_bytes:
        _append_error(errors, "available RAM fell below the configured floor")

    stdout = paths["stdout"].read_text(encoding="utf-8", errors="replace")
    try:
        record["marker"] = parse_lifetime_marker(stdout, expected_nonce=run_nonce)
    except ValueError as error:
        _append_error(errors, str(error))
    try:
        record["gtest"] = _parse_gtest_json(paths["gtest"])
    except ValueError as error:
        _append_error(errors, str(error))
    utilization, utilization_samples, utilization_errors = _read_utilization(
        paths["utilization"]
    )
    record["utilization_summary"] = utilization
    record["utilization_samples"] = utilization_samples
    for error in utilization_errors:
        _append_error(errors, error)

    if not paths["ready"].exists():
        _append_error(errors, "utilization sampler never reported ready")
    if record["exit_code"] != 0:
        _append_error(errors, f"workload exit code is {record['exit_code']}")
    if record["sampler_exit_code"] != 0:
        _append_error(errors, f"utilization sampler exit code is {record['sampler_exit_code']}")
    if record["timed_out"]:
        _append_error(errors, "workload timeout occurred")
    if record["low_memory_stop"]:
        _append_error(errors, "workload stopped at the available RAM floor")
    if record["emergency_stop"]:
        _append_error(errors, "emergency process-tree stop was required")
    if record["child_workload_observed"]:
        _append_error(
            errors,
            "child workload process invalidates root-PID utilization sampling",
        )
    for label, evidence in (
        ("workload", record["job_object"]),
        ("sampler", record["sampler_job_object"]),
    ):
        if not evidence["setup_ok"]:
            _append_error(errors, f"{label} Job Object setup failed")
        if not evidence["query_ok"]:
            _append_error(errors, f"{label} Job Object cleanup query failed")
        if evidence["queried_active_process_count_after_cleanup"] != 0:
            _append_error(errors, f"{label} Job Object cleanup did not prove zero")

    record["valid"] = not errors
    atomic_write_json(paths["run"], record)
    return record


def _require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def _validate_summary_and_samples(run: dict) -> None:
    summary = run.get("utilization_summary")
    samples = run.get("utilization_samples")
    _require(isinstance(summary, dict), "CPU/GPU utilization summary is missing")
    _require(isinstance(samples, dict), "CPU/GPU utilization samples are missing")
    row_count = summary.get("row_count")
    _require(
        isinstance(row_count, int) and not isinstance(row_count, bool) and row_count > 0,
        "CPU/GPU utilization row count is invalid",
    )
    for field in UTILIZATION_VALUE_FIELDS:
        values = samples.get(field)
        label = "CPU" if field == "cpu_percent" else "GPU"
        _require(
            isinstance(values, list) and len(values) == row_count,
            f"{label} {field} samples are missing",
        )
        expected = summarize_samples(values)
        _require(
            summary.get(field) == expected,
            f"{label} {field} summary does not match raw samples",
        )
    definitions = samples.get("cpu_sample_definition")
    _require(
        isinstance(definitions, list)
        and len(definitions) == row_count
        and definitions[0] == FIRST_CPU_SAMPLE_DEFINITION
        and all(
            definition == INTERVAL_CPU_SAMPLE_DEFINITION
            for definition in definitions[1:]
        ),
        "CPU sample interval definitions are invalid",
    )
    expected_definition_counts = {
        FIRST_CPU_SAMPLE_DEFINITION: 1,
        INTERVAL_CPU_SAMPLE_DEFINITION: row_count - 1,
    }
    _require(
        summary.get("cpu_sample_definitions") == expected_definition_counts,
        "CPU sample interval definition counts are invalid",
    )
    for field in ("gpu_engine_query", "gpu_memory_query"):
        status = summary.get(field)
        _require(
            isinstance(status, dict)
            and status.get("all_succeeded") is True
            and status.get("success_count") == row_count
            and status.get("failure_count") == 0,
            "GPU counter query evidence is missing or failed",
        )


def _validate_run_for_reconciliation(
    run: dict, derived_commit: str, executable_sha256: str
) -> None:
    _require(isinstance(run, dict), "run evidence is missing")
    _require(run.get("schema") == RUN_SCHEMA, "run schema is invalid")
    _require(
        isinstance(run.get("command"), list) and bool(run["command"]),
        "run command evidence is missing",
    )
    identity = run.get("environment_identity")
    _require(
        isinstance(identity, dict)
        and re.fullmatch(r"[0-9a-f]{64}", identity.get("identity_sha256", ""))
        is not None
        and isinstance(identity.get("runtime_library_dir"), str)
        and bool(identity["runtime_library_dir"])
        and re.fullmatch(
            r"[0-9a-f]{64}", identity.get("runtime_openvino_dll_sha256", "")
        )
        is not None,
        "run environment identity is missing",
    )
    nonce = run.get("run_nonce")
    _require(
        isinstance(nonce, str) and re.fullmatch(r"[0-9a-f]{32}", nonce) is not None,
        "run nonce evidence is invalid",
    )
    root_pid = run.get("root_pid")
    _require(
        isinstance(root_pid, int) and not isinstance(root_pid, bool) and root_pid > 0,
        "observed root PID is invalid",
    )
    observed = run.get("observed_pids")
    _require(
        isinstance(observed, list)
        and observed
        and observed == [root_pid]
        and run.get("child_workload_observed") is False,
        "observed PID list contains a child workload process",
    )
    governance = run.get("launch_governance")
    _require(
        isinstance(governance, dict)
        and governance.get("workload_created_suspended") is True
        and governance.get("workload_assigned_before_resume") is True
        and governance.get("sampler_created_suspended") is True
        and governance.get("sampler_assigned_before_resume") is True,
        "launch governance did not prove Job assignment before resume",
    )
    _require(
        run.get("sampling_interval_ms") == EXACT_INTERVAL_MS,
        "sampling interval is not exactly 100 ms",
    )
    memory_count = run.get("memory_sample_count")
    _require(
        isinstance(memory_count, int)
        and not isinstance(memory_count, bool)
        and memory_count > 0,
        "memory sample count is missing",
    )
    _require(
        _is_finite_number(run.get("peak_working_set_bytes"))
        and run["peak_working_set_bytes"] > 0,
        "peak working-set memory evidence is missing",
    )
    _require(
        _is_finite_number(run.get("peak_private_bytes"))
        and run["peak_private_bytes"] > 0,
        "peak private-byte memory evidence is missing",
    )
    ram = run.get("available_ram_bytes")
    _require(isinstance(ram, dict), "available RAM evidence is missing")
    for field in ("before", "minimum", "after"):
        _require(
            _is_finite_number(ram.get(field)) and ram[field] > 0,
            f"available RAM {field} evidence is missing",
        )
    _require(
        ram["minimum"] <= ram["before"] and ram["minimum"] <= ram["after"],
        "available RAM minimum is inconsistent",
    )
    _validate_summary_and_samples(run)
    marker = run.get("marker")
    _validate_lifetime_marker(marker, expected_nonce=nonce)
    gtest = run.get("gtest")
    _require(
        isinstance(gtest, dict)
        and gtest.get("tests") == 1
        and gtest.get("passed") == 1
        and gtest.get("failures") == 0
        and gtest.get("errors") == 0
        and gtest.get("disabled") == 0
        and gtest.get("test_name") == EXPECTED_TEST_NAME,
        "GTest evidence is not exactly one passed GTest",
    )
    _require(run.get("exit_code") == 0, "run exit status is nonzero")
    _require(run.get("timed_out") is False, "run timeout status is invalid")
    _require(
        run.get("low_memory_stop") is False,
        "run available RAM floor status is invalid",
    )
    _require(
        run.get("emergency_stop") is False, "run emergency stop status is invalid"
    )
    _require(
        _is_finite_number(run.get("cleanup_duration_seconds"))
        and run["cleanup_duration_seconds"] >= 0,
        "cleanup duration evidence is missing",
    )
    _require(
        run.get("sampler_exit_code") == 0,
        "utilization sampler exit status is invalid",
    )
    for label, key in (
        ("cleanup", "job_object"),
        ("sampler cleanup", "sampler_job_object"),
    ):
        evidence = run.get(key)
        _require(
            isinstance(evidence, dict)
            and evidence.get("setup_ok") is True
            and evidence.get("query_ok") is True
            and evidence.get("queried_active_process_count_after_cleanup") == 0
            and evidence.get("survivor_pids_after_cleanup") == [],
            f"{label} did not prove a queried zero survivor count",
        )
    _require(
        not run.get("validation_errors"), "run contains validation errors"
    )
    _require(run.get("derived_commit") == derived_commit, "derived commit drift")
    _require(
        run.get("derived_checkout_clean_before") is True
        and run.get("derived_checkout_clean_after") is True,
        "derived checkout is not exactly clean",
    )
    _require(
        run.get("executable_sha256_before") == executable_sha256
        and run.get("executable_sha256_after") == executable_sha256,
        "executable hash drift",
    )


def reconcile_runs(
    runs: list[dict], derived_commit: str, executable_sha256: str
) -> dict:
    """Strictly reconcile two independently governed fresh-process records."""
    _require(
        isinstance(derived_commit, str)
        and re.fullmatch(r"[0-9a-f]{40}", derived_commit) is not None,
        "derived commit identity is invalid",
    )
    _require(
        isinstance(executable_sha256, str)
        and re.fullmatch(r"[0-9a-f]{64}", executable_sha256) is not None,
        "executable SHA-256 identity is invalid",
    )
    _require(len(runs) == 2, "exactly two fresh-process runs are required")
    for run in runs:
        _validate_run_for_reconciliation(run, derived_commit, executable_sha256)
    _require(
        runs[0]["root_pid"] != runs[1]["root_pid"],
        "fresh processes reused the same PID",
    )
    _require(
        runs[0]["run_nonce"] != runs[1]["run_nonce"],
        "fresh processes reused the same nonce",
    )
    output_hash_arrays = [run["marker"]["output_hashes"] for run in runs]
    _require(
        output_hash_arrays[0] == output_hash_arrays[1],
        "fresh-process output hashes differ",
    )
    common_marker = copy_without_nonce = {
        key: value
        for key, value in runs[0]["marker"].items()
        if key != "run_nonce"
    }
    second_marker = {
        key: value
        for key, value in runs[1]["marker"].items()
        if key != "run_nonce"
    }
    _require(
        copy_without_nonce == second_marker,
        "fresh-process state proof fields differ",
    )

    combined: dict[str, Any] = {}
    for field in UTILIZATION_VALUE_FIELDS:
        values = [
            value
            for run in runs
            for value in run["utilization_samples"][field]
        ]
        combined[field] = summarize_samples(values)
    combined["gpu_engine_query"] = {
        "success_count": sum(
            run["utilization_summary"]["gpu_engine_query"]["success_count"]
            for run in runs
        ),
        "failure_count": 0,
        "all_succeeded": True,
    }
    combined["gpu_memory_query"] = {
        "success_count": sum(
            run["utilization_summary"]["gpu_memory_query"]["success_count"]
            for run in runs
        ),
        "failure_count": 0,
        "all_succeeded": True,
    }
    combined["cpu_sample_definitions"] = {
        FIRST_CPU_SAMPLE_DEFINITION: sum(
            run["utilization_summary"]["cpu_sample_definitions"][
                FIRST_CPU_SAMPLE_DEFINITION
            ]
            for run in runs
        ),
        INTERVAL_CPU_SAMPLE_DEFINITION: sum(
            run["utilization_summary"]["cpu_sample_definitions"][
                INTERVAL_CPU_SAMPLE_DEFINITION
            ]
            for run in runs
        ),
    }

    final_snapshot = common_marker["snapshots"][-1]
    return {
        "schema": EVIDENCE_SCHEMA,
        "generated_utc": _utc_now(),
        "derived_commit": derived_commit,
        "executable_sha256": executable_sha256,
        "device": common_marker["device"],
        "runtime_layer_type": common_marker["runtime_layer_type"],
        "reference_operation_count": common_marker["reference_operation_count"],
        "matched_state_count": common_marker["matched_state_count"],
        "steps": common_marker["steps"],
        "hash_algorithm": common_marker["hash_algorithm"],
        "output_hashes_per_process": output_hash_arrays,
        "state_snapshots": common_marker["snapshots"],
        "step_100_allocation": {
            field: final_snapshot[field]
            for field in (
                "payload_bytes",
                "norm_bytes",
                "metadata_bytes",
                "full_precision_equivalent_bytes",
                "decoded_scratch_bytes",
            )
        },
        "runs": runs,
        "combined_utilization": combined,
        "reconciliation": {
            "fresh_processes": True,
            "distinct_nonces": True,
            "output_hash_arrays_match": True,
            "state_proofs_match": True,
            "executable_hashes_match": True,
            "derived_checkout_clean": True,
            "exactly_one_passed_gtest_per_run": True,
            "queried_zero_survivors": True,
        },
    }


def publish_reconciled_capability(
    path: Path,
    runs: list[dict],
    derived_commit: str,
    executable_sha256: str,
) -> dict:
    """Reconcile first; an invalid attempt never reaches atomic replacement."""
    value = reconcile_runs(runs, derived_commit, executable_sha256)
    atomic_write_json(path, value)
    return value


def attempt_directory_for(
    output: Path, timestamp: datetime, attempt_nonce: str
) -> Path:
    """Return a required raw-attempt path within legacy Windows path budgets."""
    if timestamp.tzinfo is None:
        raise ValueError("attempt timestamp must be timezone-aware")
    if re.fullmatch(r"[0-9a-f]{8}", attempt_nonce) is None:
        raise ValueError("attempt nonce must be eight lowercase hexadecimal digits")
    utc_stamp = timestamp.astimezone(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    output = Path(output)
    return (
        output.parent
        / output.stem
        / f"attempt-{utc_stamp}-{attempt_nonce}"
    )


def _git_identity(repo: Path) -> tuple[str, str]:
    commit = subprocess.run(
        ["git", "-C", str(repo), "rev-parse", "HEAD"],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        check=True,
    ).stdout.strip()
    status = subprocess.run(
        [
            "git",
            "-C",
            str(repo),
            "status",
            "--porcelain=v1",
            "--untracked-files=all",
        ],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        check=True,
    ).stdout
    return commit, status


def _parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--derived-repo", type=Path, required=True)
    parser.add_argument("--executable", type=Path, required=True)
    parser.add_argument("--runtime-library-dir", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument(
        "--timeout-seconds", type=float, default=EXACT_TIMEOUT_SECONDS
    )
    parser.add_argument("--interval-ms", type=int, default=EXACT_INTERVAL_MS)
    parser.add_argument(
        "--minimum-available-ram-mb",
        type=float,
        default=EXACT_MINIMUM_AVAILABLE_RAM_MB,
    )
    return parser.parse_args()


def main() -> int:
    args = _parse_args()
    output = args.output.resolve()
    raw_root = output.parent / output.stem
    raw_root.mkdir(parents=True, exist_ok=True)
    attempt_nonce = secrets.token_hex(4)
    attempt_dir = attempt_directory_for(
        output, datetime.now(timezone.utc), attempt_nonce
    )
    attempt_dir.mkdir(parents=True, exist_ok=False)
    summary_path = attempt_dir / "attempt-summary.json"
    attempt_summary: dict[str, Any] = {
        "schema": "openvino-turboquant-reference-capability-attempt/v1",
        "attempt_directory": str(attempt_dir),
        "canonical_output": str(output),
        "started_utc": _utc_now(),
        "status": "failed",
        "error": None,
        "runs": [],
        "derived_repo": str(args.derived_repo.resolve()),
        "executable": str(absolute_invocation_path(args.executable)),
        "runtime_library_dir": str(args.runtime_library_dir),
        "derived_observations": {},
        "executable_sha256_observations": {},
    }
    runs: list[dict] = []
    try:
        if args.interval_ms != EXACT_INTERVAL_MS:
            raise ValueError("publication requires exactly 100 ms sampling")
        if args.timeout_seconds != EXACT_TIMEOUT_SECONDS:
            raise ValueError("publication requires a 300-second absolute timeout")
        if args.minimum_available_ram_mb != EXACT_MINIMUM_AVAILABLE_RAM_MB:
            raise ValueError("publication requires the 2,048 MiB RAM floor")
        derived_repo = args.derived_repo.resolve()
        executable = absolute_invocation_path(args.executable)
        if not derived_repo.is_dir():
            raise ValueError("derived checkout does not exist")
        if not executable.is_file():
            raise ValueError("production-linked executable does not exist")
        runtime_library_dir = validate_runtime_library_dir(
            args.runtime_library_dir
        )

        commit_before_run_1, status_before_run_1 = _git_identity(derived_repo)
        attempt_summary["derived_observations"]["before_run_1"] = {
            "commit": commit_before_run_1,
            "status_porcelain": status_before_run_1,
        }
        if commit_before_run_1 != REQUIRED_DERIVED_COMMIT:
            raise ValueError(
                f"derived commit is {commit_before_run_1}, expected "
                f"{REQUIRED_DERIVED_COMMIT}"
            )
        if status_before_run_1:
            raise ValueError("derived checkout is not clean before run 1")

        sha_before_run_1 = _sha256_file(executable)
        attempt_summary["executable_sha256_observations"][
            "before_run_1"
        ] = sha_before_run_1

        run_1_dir = attempt_dir / "run-1"
        run_1_nonce = secrets.token_hex(16)
        run_1_command = [
            str(executable),
            EXPECTED_GTEST_FILTER,
            f"--gtest_output=json:{run_1_dir / 'gtest.json'}",
        ]
        run_1 = run_one(
            run_1_command,
            run_1_dir,
            args.timeout_seconds,
            args.interval_ms,
            args.minimum_available_ram_mb,
            run_1_nonce,
            runtime_library_dir,
        )
        runs.append(run_1)

        commit_before_run_2, status_before_run_2 = _git_identity(derived_repo)
        sha_before_run_2 = _sha256_file(executable)
        attempt_summary["derived_observations"]["before_run_2"] = {
            "commit": commit_before_run_2,
            "status_porcelain": status_before_run_2,
        }
        attempt_summary["executable_sha256_observations"][
            "before_run_2"
        ] = sha_before_run_2
        run_1.update(
            {
                "derived_commit": commit_before_run_1,
                "derived_checkout_clean_before": status_before_run_1 == "",
                "derived_checkout_clean_after": status_before_run_2 == "",
                "executable_sha256_before": sha_before_run_1,
                "executable_sha256_after": sha_before_run_2,
            }
        )
        atomic_write_json(run_1_dir / "run.json", run_1)

        run_2_dir = attempt_dir / "run-2"
        run_2_nonce = secrets.token_hex(16)
        run_2_command = [
            str(executable),
            EXPECTED_GTEST_FILTER,
            f"--gtest_output=json:{run_2_dir / 'gtest.json'}",
        ]
        run_2 = run_one(
            run_2_command,
            run_2_dir,
            args.timeout_seconds,
            args.interval_ms,
            args.minimum_available_ram_mb,
            run_2_nonce,
            runtime_library_dir,
        )
        runs.append(run_2)

        commit_after_run_2, status_after_run_2 = _git_identity(derived_repo)
        sha_after_run_2 = _sha256_file(executable)
        attempt_summary["derived_observations"]["after_run_2"] = {
            "commit": commit_after_run_2,
            "status_porcelain": status_after_run_2,
        }
        attempt_summary["executable_sha256_observations"][
            "after_run_2"
        ] = sha_after_run_2
        run_2.update(
            {
                "derived_commit": commit_after_run_2,
                "derived_checkout_clean_before": status_before_run_2 == "",
                "derived_checkout_clean_after": status_after_run_2 == "",
                "executable_sha256_before": sha_before_run_2,
                "executable_sha256_after": sha_after_run_2,
            }
        )
        atomic_write_json(run_2_dir / "run.json", run_2)

        if not (
            commit_before_run_1
            == commit_before_run_2
            == commit_after_run_2
            == REQUIRED_DERIVED_COMMIT
        ):
            raise ValueError("derived commit drifted across the two runs")
        if status_before_run_2 or status_after_run_2:
            raise ValueError("derived checkout did not remain exactly clean")
        if not (
            sha_before_run_1 == sha_before_run_2 == sha_after_run_2
        ):
            raise ValueError("executable SHA-256 drifted across the two runs")

        capability = reconcile_runs(
            runs, commit_before_run_1, sha_before_run_1
        )
        capability["attempt_directory"] = str(attempt_dir)
        capability["derived_repo"] = str(derived_repo)
        capability["runtime_library_dir"] = str(runtime_library_dir)
        capability["executable"] = {
            "path": str(executable),
            "sha256": sha_before_run_1,
            "sha256_observations": attempt_summary[
                "executable_sha256_observations"
            ],
        }
        capability["derived_checkout_observations"] = attempt_summary[
            "derived_observations"
        ]
        atomic_write_json(output, capability)
        attempt_summary["status"] = "published"
        attempt_summary["canonical_sha256"] = _sha256_file(output)
        print(f"Published capability evidence: {output}")
        print(f"Raw attempt evidence: {attempt_dir}")
        return 0
    except Exception as error:
        attempt_summary["error"] = f"{type(error).__name__}: {error}"
        print(attempt_summary["error"], file=sys.stderr)
        print(f"Failed attempt evidence: {attempt_dir}", file=sys.stderr)
        return 1
    finally:
        attempt_summary["ended_utc"] = _utc_now()
        attempt_summary["runs"] = [
            {
                "run_id": run.get("run_id"),
                "directory": run.get("output_directory"),
                "valid": run.get("valid"),
                "validation_errors": run.get("validation_errors"),
            }
            for run in runs
        ]
        atomic_write_json(summary_path, attempt_summary)


if __name__ == "__main__":
    raise SystemExit(main())
