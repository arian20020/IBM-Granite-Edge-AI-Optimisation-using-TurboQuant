"""Govern one WB-04 worker process and preserve complete raw evidence."""

from __future__ import annotations

import hashlib
import json
import math
import secrets
import shutil
import statistics
import subprocess
import time
from collections.abc import Mapping
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from .owned_process_guard import (
    CREATE_SUSPENDED,
    KillOnCloseJob,
    _append_error,
    _cleanup_job,
    _close_run_resources,
    _resume_suspended_process,
    _wait_process,
    available_ram_bytes,
    process_memory_bytes,
)
from .runtime_measurement import (
    atomic_write_json,
    parse_cpu_samples,
    parse_gpu_samples,
    parse_worker_output,
    validate_activation_telemetry,
)


MIB = 1024**2
MIN_LAUNCH_AVAILABLE_RAM_BYTES = 4096 * MIB
MIN_EMERGENCY_AVAILABLE_RAM_BYTES = 2048 * MIB
RUN_SCHEMA = "official-openvino-wb04-governed-run/v1"


def _utc_now() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="microseconds").replace(
        "+00:00", "Z"
    )


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with Path(path).open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _sha256_json(value: object) -> str:
    encoded = json.dumps(
        value,
        sort_keys=True,
        separators=(",", ":"),
        ensure_ascii=True,
        allow_nan=False,
    ).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def _write_memory_row(handle: Any, value: dict[str, Any]) -> None:
    handle.write(
        json.dumps(
            value,
            sort_keys=True,
            separators=(",", ":"),
            allow_nan=False,
        )
        + "\n"
    )
    handle.flush()


def _finite_nonnegative(value: Any, field: str) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError(f"{field} must be numeric")
    normalized = float(value)
    if not math.isfinite(normalized) or normalized < 0:
        raise ValueError(f"{field} must be finite and non-negative")
    return normalized


def _gpu_summary(
    value: Any,
    field: str,
    *,
    maximum: float | None = None,
    integers: bool = False,
) -> list[float]:
    if not isinstance(value, Mapping):
        raise ValueError(f"{field} evidence is required")
    if value.get("query_succeeded") is not True:
        raise ValueError(f"{field} query did not succeed")
    raw_values = value.get("values")
    if not isinstance(raw_values, list) or len(raw_values) < 2:
        raise ValueError(f"{field} requires at least two observations")
    values = [
        _finite_nonnegative(item, f"{field} observation") for item in raw_values
    ]
    if maximum is not None and any(item > maximum for item in values):
        raise ValueError(f"{field} observations exceed {maximum}")
    if integers and any(not item.is_integer() for item in values):
        raise ValueError(f"{field} observations must be integers")
    count = value.get("count")
    if isinstance(count, bool) or not isinstance(count, int) or count != len(values):
        raise ValueError(f"{field} count does not match observations")
    expected = {
        "mean": statistics.fmean(values),
        "median": statistics.median(values),
        "peak": max(values),
    }
    for statistic, expected_value in expected.items():
        actual = _finite_nonnegative(
            value.get(statistic),
            f"{field} {statistic}",
        )
        if not math.isclose(actual, expected_value, rel_tol=1e-9, abs_tol=1e-9):
            raise ValueError(f"{field} {statistic} does not match observations")
    return values


def _validate_gpu_observability(record: Mapping[str, Any]) -> None:
    activation = record.get("activation")
    if not isinstance(activation, Mapping):
        raise ValueError("activation telemetry is required")
    actual_device = activation.get("actual_device")
    if not isinstance(actual_device, str):
        raise ValueError("actual activation device is required")
    if actual_device.split(".", 1)[0].upper() != "GPU":
        return

    gpu_values = _gpu_summary(
        record.get("gpu_percent"),
        "GPU utilization",
        maximum=100.0,
    )
    engine_values = _gpu_summary(
        record.get("gpu_engine_count"),
        "GPU engine-count",
        integers=True,
    )
    if len(gpu_values) != len(engine_values):
        raise ValueError(
            "GPU utilization and engine-count observation counts differ"
        )
    if max(engine_values) <= 0:
        raise ValueError("GPU engine-count peak must be positive")
    if max(gpu_values) <= 0:
        raise ValueError("GPU utilization peak must be positive")

    combined_bytes = record.get("gpu_memory_peak_bytes")
    if combined_bytes is not None:
        if (
            isinstance(combined_bytes, bool)
            or not isinstance(combined_bytes, int)
            or combined_bytes <= 0
        ):
            raise ValueError("GPU combined memory byte peak must be positive")
        paired_components = (
            "gpu_memory_peak_dedicated_bytes",
            "gpu_memory_peak_shared_bytes",
        )
        paired_values: list[int] = []
        for field in paired_components:
            value = record.get(field)
            if isinstance(value, bool) or not isinstance(value, int) or value < 0:
                raise ValueError(f"GPU paired peak byte receipt {field} is invalid")
            paired_values.append(value)
        if combined_bytes != sum(paired_values):
            raise ValueError("GPU paired peak bytes do not equal combined peak")
        expected_display = float(format(combined_bytes / MIB, ".6f"))
        if record.get("gpu_memory_peak_mb") != expected_display:
            raise ValueError(
                "GPU combined memory display does not match byte serialization"
            )
        return

    dedicated = _finite_nonnegative(
        record.get("gpu_dedicated_memory_peak_mb"),
        "GPU dedicated memory peak",
    )
    shared = _finite_nonnegative(
        record.get("gpu_shared_memory_peak_mb"),
        "GPU shared memory peak",
    )
    combined = _finite_nonnegative(
        record.get("gpu_memory_peak_mb"),
        "GPU combined memory peak",
    )
    if combined <= 0:
        raise ValueError("GPU combined memory peak must be positive")
    if combined < max(dedicated, shared) or combined > dedicated + shared:
        raise ValueError(
            "GPU combined memory peak is outside component bounds"
        )


def run_governed_process(
    *,
    command: list[str],
    output_dir: Path,
    role: str,
    environment: dict[str, str],
    sampler_script: Path,
    timeout_seconds: float = 900.0,
    launch_minimum_available_ram_bytes: int = MIN_LAUNCH_AVAILABLE_RAM_BYTES,
    emergency_minimum_available_ram_bytes: int = (
        MIN_EMERGENCY_AVAILABLE_RAM_BYTES
    ),
    sample_interval_seconds: float = 0.25,
) -> dict[str, Any]:
    """Run one fresh process behind a kill-on-close Job Object."""

    if not command or not all(isinstance(item, str) and item for item in command):
        raise ValueError("command must contain non-blank arguments")
    if role not in {"pilot", "warmup", "sample-1", "sample-2", "sample-3"}:
        raise ValueError("role is invalid")
    if timeout_seconds <= 0 or sample_interval_seconds <= 0:
        raise ValueError("timeouts and sampling intervals must be positive")
    if (
        isinstance(launch_minimum_available_ram_bytes, bool)
        or not isinstance(launch_minimum_available_ram_bytes, int)
        or launch_minimum_available_ram_bytes
        < MIN_LAUNCH_AVAILABLE_RAM_BYTES
    ):
        raise ValueError("launch available RAM must be at least 4096 MiB")
    if (
        isinstance(emergency_minimum_available_ram_bytes, bool)
        or not isinstance(emergency_minimum_available_ram_bytes, int)
        or emergency_minimum_available_ram_bytes
        < MIN_EMERGENCY_AVAILABLE_RAM_BYTES
    ):
        raise ValueError("emergency available RAM must be at least 2048 MiB")
    sampler = Path(sampler_script).resolve()
    if not sampler.is_file():
        raise ValueError(f"utilization sampler is missing: {sampler}")

    root = Path(output_dir)
    root.mkdir(parents=True, exist_ok=False)
    paths = {
        "stdout": root / "stdout.txt",
        "stderr": root / "stderr.txt",
        "sampler_stdout": root / "sampler.stdout.txt",
        "sampler_stderr": root / "sampler.stderr.txt",
        "memory": root / "memory.jsonl",
        "cpu": root / "cpu.csv",
        "gpu": root / "gpu.csv",
        "ready": root / "sampler.ready",
        "start": root / "sampler.start",
        "stop": root / "sampler.stop",
        "command": root / "command.json",
        "attempt": root / "attempt.json",
    }
    atomic_write_json(
        paths["command"],
        {
            "command": command,
            "working_directory": str(Path.cwd().resolve()),
            "role": role,
            "timeout_seconds": timeout_seconds,
            "launch_minimum_available_ram_bytes": (
                launch_minimum_available_ram_bytes
            ),
            "emergency_minimum_available_ram_bytes": (
                emergency_minimum_available_ram_bytes
            ),
            "sample_interval_seconds": sample_interval_seconds,
            "sampler_script": str(sampler),
        },
    )
    for path in (
        paths["stdout"],
        paths["stderr"],
        paths["sampler_stdout"],
        paths["sampler_stderr"],
        paths["memory"],
        paths["cpu"],
        paths["gpu"],
    ):
        path.touch()

    record: dict[str, Any] = {
        "schema": RUN_SCHEMA,
        "role": role,
        "run_nonce": secrets.token_hex(16),
        "command": command,
        "started_utc": None,
        "ended_utc": None,
        "elapsed_seconds": None,
        "root_pid": None,
        "observed_pids": [],
        "exit_code": None,
        "sampler_exit_code": None,
        "timed_out": False,
        "low_memory_stop": False,
        "launch_minimum_available_ram_bytes": launch_minimum_available_ram_bytes,
        "emergency_minimum_available_ram_bytes": (
            emergency_minimum_available_ram_bytes
        ),
        "available_ram_bytes": {"before": None, "minimum": None, "after": None},
        "peak_working_set_bytes": 0,
        "peak_private_bytes": 0,
        "memory_sample_count": 0,
        "cpu_percent": None,
        "gpu_percent": None,
        "gpu_engine_count": None,
        "gpu_dedicated_memory_peak_mb": None,
        "gpu_shared_memory_peak_mb": None,
        "gpu_memory_peak_mb": None,
        "gpu_dedicated_memory_peak_bytes": None,
        "gpu_shared_memory_peak_bytes": None,
        "gpu_memory_peak_dedicated_bytes": None,
        "gpu_memory_peak_shared_bytes": None,
        "gpu_memory_peak_bytes": None,
        "gpu_sampler_supported": False,
        "memory_unit_receipt": {
            "schema": "official-openvino-memory-unit-receipt/v2",
            "binary_mib_bytes": MIB,
            "projections": {
                "peak_working_set_mb": {
                    "collector": "owned-process memory sampler",
                    "source_field": "owned_process_memory.working_set_bytes",
                    "source_unit": "bytes",
                    "conversion": "divide-by-binary-mib",
                    "conversion_divisor_bytes": MIB,
                    "projected_unit": "MiB",
                },
                "peak_private_mb": {
                    "collector": "owned-process memory sampler",
                    "source_field": "owned_process_memory.private_bytes",
                    "source_unit": "bytes",
                    "conversion": "divide-by-binary-mib",
                    "conversion_divisor_bytes": MIB,
                    "projected_unit": "MiB",
                },
                "available_ram_min_mb": {
                    "collector": "available-RAM sampler",
                    "source_field": "available_ram_bytes.minimum",
                    "source_unit": "bytes",
                    "conversion": "divide-by-binary-mib",
                    "conversion_divisor_bytes": MIB,
                    "projected_unit": "MiB",
                },
                "kv_mb": {
                    "collector": "OpenVINO activation telemetry",
                    "source_field": "activation.actual_bytes",
                    "source_unit": "bytes",
                    "conversion": "divide-by-binary-mib",
                    "conversion_divisor_bytes": MIB,
                    "projected_unit": "MiB",
                },
                "gpu_memory_peak_mb": {
                    "collector": "Windows GPUProcessMemory sampler",
                    "source_field": "GPUProcessMemory.DedicatedUsage+SharedUsage",
                    "source_unit": "bytes",
                    "conversion": "divide-by-binary-mib",
                    "conversion_divisor_bytes": MIB,
                    "projected_unit": "MiB",
                },
            },
        },
        "fallback_count": 0,
        "residual_owned_process_count": None,
        "worker": None,
        "activation": None,
        "stdout_sha256": None,
        "stderr_sha256": None,
        "output_sha256": None,
        "telemetry_sha256": None,
        "workload_job": None,
        "sampler_job": None,
        "cleanup_process_count": None,
        "emergency_actions": [],
        "validation_errors": [],
        "valid": False,
    }
    errors: list[str] = record["validation_errors"]
    before = available_ram_bytes()
    record["available_ram_bytes"]["before"] = before
    if before is None:
        _append_error(errors, "available RAM query failed before launch")
    elif before < launch_minimum_available_ram_bytes:
        _append_error(errors, "available RAM is below the pre-launch floor")

    workload_job: KillOnCloseJob | None = None
    sampler_job: KillOnCloseJob | None = None
    workload: subprocess.Popen | None = None
    counter: subprocess.Popen | None = None
    file_handles: list[Any] = []
    memory_rows: list[dict[str, Any]] = []
    observed_pids: set[int] = set()
    started = time.monotonic()

    if not errors:
        try:
            workload_job = KillOnCloseJob(f"WB04-workload-{record['run_nonce']}")
            sampler_job = KillOnCloseJob(f"WB04-sampler-{record['run_nonce']}")
            stdout_handle = paths["stdout"].open("wb")
            stderr_handle = paths["stderr"].open("wb")
            sampler_stdout_handle = paths["sampler_stdout"].open("wb")
            sampler_stderr_handle = paths["sampler_stderr"].open("wb")
            memory_handle = paths["memory"].open(
                "w", encoding="utf-8", newline="\n"
            )
            file_handles.extend(
                [
                    stdout_handle,
                    stderr_handle,
                    sampler_stdout_handle,
                    sampler_stderr_handle,
                    memory_handle,
                ]
            )
            record["started_utc"] = _utc_now()
            workload = subprocess.Popen(
                command,
                cwd=Path.cwd(),
                env=dict(environment),
                stdin=subprocess.DEVNULL,
                stdout=stdout_handle,
                stderr=stderr_handle,
                creationflags=(
                    subprocess.CREATE_NEW_PROCESS_GROUP | CREATE_SUSPENDED
                ),
            )
            record["root_pid"] = workload.pid
            workload_job.assign_pid(workload.pid)

            powershell = shutil.which("powershell.exe") or "powershell.exe"
            sampler_command = [
                powershell,
                "-NoProfile",
                "-NonInteractive",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                str(sampler),
                "-ProcessId",
                str(workload.pid),
                "-CpuOutputPath",
                str(paths["cpu"]),
                "-GpuOutputPath",
                str(paths["gpu"]),
                "-ReadyPath",
                str(paths["ready"]),
                "-StartPath",
                str(paths["start"]),
                "-StopPath",
                str(paths["stop"]),
                "-CpuIntervalMilliseconds",
                "250",
                "-GpuIntervalMilliseconds",
                "1000",
            ]
            counter = subprocess.Popen(
                sampler_command,
                cwd=Path.cwd(),
                stdin=subprocess.DEVNULL,
                stdout=sampler_stdout_handle,
                stderr=sampler_stderr_handle,
                creationflags=(
                    subprocess.CREATE_NEW_PROCESS_GROUP | CREATE_SUSPENDED
                ),
            )
            sampler_job.assign_pid(counter.pid)
            _resume_suspended_process(counter.pid)

            ready_deadline = min(
                started + timeout_seconds, time.monotonic() + 30.0
            )
            while not paths["ready"].is_file():
                if counter.poll() is not None:
                    _append_error(
                        errors,
                        "utilization sampler exited before reporting ready",
                    )
                    break
                if time.monotonic() >= ready_deadline:
                    _append_error(errors, "utilization sampler readiness timed out")
                    break
                time.sleep(0.01)

            if not errors:
                _resume_suspended_process(workload.pid)
                paths["start"].write_text(_utc_now() + "\n", encoding="ascii")
                next_sample = time.monotonic()
                while True:
                    now = time.monotonic()
                    if workload.poll() is not None:
                        break
                    if now - started >= timeout_seconds:
                        record["timed_out"] = True
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
                            errors, f"workload Job Object query failed: {error}"
                        )
                        break
                    observed_pids.update(active_pids)
                    available = available_ram_bytes()
                    if available is None:
                        _append_error(errors, "available RAM query failed in-run")
                        break
                    process_rows: list[dict[str, int]] = []
                    for pid in active_pids:
                        memory = process_memory_bytes(pid)
                        if memory is None:
                            # A process can exit between the Job query and the
                            # memory query. Recheck before treating this as loss.
                            if pid in workload_job.active_pids():
                                _append_error(
                                    errors,
                                    f"memory query failed for live PID {pid}",
                                )
                            continue
                        process_rows.append(
                            {
                                "pid": pid,
                                "working_set_bytes": memory[0],
                                "private_bytes": memory[1],
                            }
                        )
                    working_set = sum(
                        row["working_set_bytes"] for row in process_rows
                    )
                    private = sum(row["private_bytes"] for row in process_rows)
                    row = {
                        "timestamp_utc": _utc_now(),
                        "available_ram_bytes": available,
                        "working_set_bytes": working_set,
                        "private_bytes": private,
                        "processes": process_rows,
                    }
                    memory_rows.append(row)
                    _write_memory_row(memory_handle, row)
                    record["peak_working_set_bytes"] = max(
                        record["peak_working_set_bytes"], working_set
                    )
                    record["peak_private_bytes"] = max(
                        record["peak_private_bytes"], private
                    )
                    if available < emergency_minimum_available_ram_bytes:
                        record["low_memory_stop"] = True
                        break
                    while next_sample <= now:
                        next_sample += sample_interval_seconds

            paths["stop"].write_text("stop\n", encoding="ascii")
            if (
                workload is not None
                and workload.poll() is None
                and (record["timed_out"] or record["low_memory_stop"] or errors)
            ):
                workload_job.terminate(137)
            if workload is not None:
                _wait_process(workload, 30.0, "workload", errors)
                record["exit_code"] = workload.poll()
            if counter is not None:
                if not _wait_process(counter, 20.0, "sampler", errors):
                    sampler_job.terminate(138)
                    _wait_process(counter, 10.0, "sampler after terminate", errors)
                record["sampler_exit_code"] = counter.poll()
        except Exception as error:
            _append_error(
                errors, f"governed launch failed: {type(error).__name__}: {error}"
            )
        finally:
            if paths["stop"].parent.is_dir():
                try:
                    paths["stop"].write_text("stop\n", encoding="ascii")
                except OSError as error:
                    _append_error(errors, f"sampler stop marker failed: {error}")

            workload_evidence, _ = _cleanup_job(
                workload_job,
                workload,
                "workload",
                errors,
                record["emergency_actions"],
                cleanup_timeout_seconds=15.0,
            )
            sampler_evidence, _ = _cleanup_job(
                sampler_job,
                counter,
                "sampler",
                errors,
                record["emergency_actions"],
                cleanup_timeout_seconds=15.0,
            )
            record["workload_job"] = workload_evidence
            record["sampler_job"] = sampler_evidence
            _close_run_resources(
                file_handles, [workload_job, sampler_job], errors
            )

    after = available_ram_bytes()
    record["available_ram_bytes"]["after"] = after
    if after is None:
        _append_error(errors, "available RAM query failed after run")
    elif after < emergency_minimum_available_ram_bytes:
        _append_error(errors, "available RAM is below the post-run floor")
    available_values = [
        value
        for value in (
            before,
            *(row["available_ram_bytes"] for row in memory_rows),
            after,
        )
        if value is not None
    ]
    record["available_ram_bytes"]["minimum"] = (
        min(available_values) if available_values else None
    )
    record["memory_sample_count"] = len(memory_rows)
    record["observed_pids"] = sorted(observed_pids)
    record["ended_utc"] = _utc_now()
    record["elapsed_seconds"] = time.monotonic() - started

    survivor_counts: list[int] = []
    for evidence in (record["workload_job"], record["sampler_job"]):
        if isinstance(evidence, dict):
            count = evidence.get("queried_active_process_count_after_cleanup")
            if isinstance(count, int):
                survivor_counts.append(count)
    if len(survivor_counts) == 2:
        record["cleanup_process_count"] = sum(survivor_counts)
        record["residual_owned_process_count"] = record[
            "cleanup_process_count"
        ]
    else:
        _append_error(errors, "cleanup survivor count is incomplete")

    for field in ("stdout", "stderr"):
        path = paths[field]
        if path.is_file():
            record[f"{field}_sha256"] = _sha256_file(path)
    if record["sampler_exit_code"] != 0:
        _append_error(errors, "utilization sampler did not exit successfully")
    if record["exit_code"] != 0:
        _append_error(errors, "workload did not exit successfully")
    if not record["timed_out"] and not record["low_memory_stop"]:
        try:
            cpu = parse_cpu_samples(paths["cpu"])
            gpu = parse_gpu_samples(paths["gpu"])
            parsed = parse_worker_output(
                paths["stdout"].read_text(encoding="utf-8", errors="replace"),
                paths["stderr"].read_text(encoding="utf-8", errors="replace"),
            )
            record["cpu_percent"] = cpu
            record["gpu_percent"] = gpu["gpu_percent"]
            record["gpu_sampler_supported"] = True
            record["gpu_engine_count"] = gpu["gpu_engine_count"]
            for field in (
                "gpu_dedicated_memory_peak_mb",
                "gpu_shared_memory_peak_mb",
                "gpu_memory_peak_mb",
                "gpu_dedicated_memory_peak_bytes",
                "gpu_shared_memory_peak_bytes",
                "gpu_memory_peak_dedicated_bytes",
                "gpu_memory_peak_shared_bytes",
                "gpu_memory_peak_bytes",
            ):
                record[field] = gpu.get(field)
            record["worker"] = parsed["result"]
            record["activation"] = parsed["activation"]
            _validate_gpu_observability(record)
            record["output_sha256"] = hashlib.sha256(
                parsed["result"]["output"].encode("utf-8")
            ).hexdigest()
            record["telemetry_sha256"] = _sha256_json(parsed["activation"])
        except (OSError, ValueError, KeyError) as error:
            _append_error(errors, f"measurement reconciliation failed: {error}")

    record["valid"] = (
        not errors
        and record["exit_code"] == 0
        and record["sampler_exit_code"] == 0
        and record["cleanup_process_count"] == 0
        and record["memory_sample_count"] > 0
        and record["worker"] is not None
        and record["activation"] is not None
    )
    atomic_write_json(paths["attempt"], record)
    return record


def measurement_sample(record: dict[str, Any], source_path: Path) -> dict[str, Any]:
    """Convert a valid governed record into the strict metrics sample schema."""

    if record.get("valid") is not True:
        raise ValueError("governed record is not valid")
    worker = record["worker"]
    activation = record["activation"]
    validate_activation_telemetry(activation)
    _validate_gpu_observability(record)
    source = Path(source_path)
    if not source.is_file():
        raise ValueError("governed source record is missing")
    expected_standard_bytes = int(
        activation["expected_persistent_standard_bytes"]
    )
    standard_bytes = int(activation["actual_persistent_standard_bytes"])
    expected_payload_bytes = int(
        activation["expected_persistent_payload_bytes"]
    )
    payload_bytes = int(activation["actual_persistent_payload_bytes"])
    expected_norm_bytes = int(activation["expected_persistent_norm_bytes"])
    norm_bytes = int(activation["actual_persistent_norm_bytes"])
    expected_metadata_bytes = int(
        activation["expected_persistent_metadata_bytes"]
    )
    metadata_bytes = int(activation["actual_persistent_metadata_bytes"])
    actual_bytes = int(activation["actual_bytes"])
    expected_bytes = int(activation["expected_bytes"])
    if actual_bytes != standard_bytes + payload_bytes + norm_bytes + metadata_bytes:
        raise ValueError("activation byte components do not equal persistent total")
    return {
        "sample_id": record["role"],
        "load_ms": worker["load_ms"],
        "ttft_ms": worker["ttft_ms"],
        "prompt_tps": worker["prompt_tps"],
        "tpot_ms": worker["tpot_ms"],
        "decode_tps": worker["decode_tps"],
        "generation_duration_ms": worker["generation_duration_ms"],
        "peak_working_set_mb": record["peak_working_set_bytes"] / MIB,
        "peak_private_mb": record["peak_private_bytes"] / MIB,
        "available_ram_before_mb": (
            record["available_ram_bytes"]["before"] / MIB
        ),
        "available_ram_min_mb": (
            record["available_ram_bytes"]["minimum"] / MIB
        ),
        "available_ram_after_mb": (
            record["available_ram_bytes"]["after"] / MIB
        ),
        "gpu_dedicated_memory_peak_mb": (
            record["gpu_dedicated_memory_peak_mb"]
        ),
        "gpu_shared_memory_peak_mb": record["gpu_shared_memory_peak_mb"],
        "gpu_memory_peak_mb": record["gpu_memory_peak_mb"],
        "expected_persistent_kv_bytes": expected_bytes,
        "actual_persistent_kv_bytes": actual_bytes,
        "standard_kv_bytes": standard_bytes,
        "payload_kv_bytes": payload_bytes,
        "norm_kv_bytes": norm_bytes,
        "metadata_kv_bytes": metadata_bytes,
        "scratch_peak_bytes": activation["decoded_scratch_bytes"],
        "kv_mb": actual_bytes / MIB,
        "cpu_percent": record["cpu_percent"],
        "gpu_percent": record.get("gpu_percent"),
        "activation": {
            "status": activation["status"],
            "requested_key_algorithm": activation[
                "requested_key_algorithm"
            ],
            "requested_value_algorithm": activation[
                "requested_value_algorithm"
            ],
            "activated_key_algorithm": activation[
                "activated_key_algorithm"
            ],
            "activated_value_algorithm": activation[
                "activated_value_algorithm"
            ],
            "requested_key_cache_precision": activation[
                "requested_key_cache_precision"
            ],
            "requested_value_cache_precision": activation[
                "requested_value_cache_precision"
            ],
            "activated_key_cache_precision": activation[
                "activated_key_cache_precision"
            ],
            "activated_value_cache_precision": activation[
                "activated_value_cache_precision"
            ],
            "observed_key_state_precision": activation[
                "observed_key_state_precision"
            ],
            "observed_value_state_precision": activation[
                "observed_value_state_precision"
            ],
            "norm_correction": activation["norm_correction"],
            "attention_path": activation["attention_path"],
            "requested_device": activation["device"],
            "actual_device": activation["actual_device"],
            "fallback": activation["fallback"],
            "expected_persistent_bytes": expected_bytes,
            "actual_persistent_bytes": actual_bytes,
            "expected_persistent_standard_bytes": activation[
                "expected_persistent_standard_bytes"
            ],
            "actual_persistent_standard_bytes": standard_bytes,
            "expected_persistent_payload_bytes": expected_payload_bytes,
            "actual_persistent_payload_bytes": payload_bytes,
            "expected_persistent_norm_bytes": expected_norm_bytes,
            "actual_persistent_norm_bytes": norm_bytes,
            "expected_persistent_metadata_bytes": expected_metadata_bytes,
            "actual_persistent_metadata_bytes": metadata_bytes,
            "operation_type": activation["operation_type"],
            "operation_count": activation["operation_count"],
            "matched_state_count": activation["matched_state_count"],
            "transformed_model_hash": activation[
                "transformed_model_hash"
            ],
            "runtime_layer_type": activation["runtime_layer_type"],
            "build_commit": activation["build_commit"],
            "model_hash": activation["model_hash"],
            "output_valid": worker["output_valid"],
        },
        "output_sha256": record["output_sha256"],
        "telemetry_sha256": record["telemetry_sha256"],
        "source": source.as_posix(),
        "source_sha256": _sha256_file(source),
        "cleanup_process_count": record["cleanup_process_count"],
    }


__all__ = [
    "MIB",
    "MIN_EMERGENCY_AVAILABLE_RAM_BYTES",
    "MIN_LAUNCH_AVAILABLE_RAM_BYTES",
    "measurement_sample",
    "run_governed_process",
]
