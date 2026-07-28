"""Run one owned Windows process tree with strict resource and evidence gates."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import secrets
import subprocess
import sys
import tempfile
import time
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Literal, Sequence

from scripts.testing.official_openvino import owned_process_guard
from scripts.testing.official_openvino.owned_process_guard import (
    CREATE_SUSPENDED,
    KillOnCloseJob,
    _append_error,
    _cleanup_job,
    _close_run_resources,
    _resume_suspended_process,
    _taskkill,
    _wait_process,
    available_ram_bytes,
    process_memory_bytes,
)


GUARD_SCHEMA = "official-openvino-owned-process-guard/v1"
MIB = 1024 * 1024


@dataclass(frozen=True)
class GuardLimits:
    minimum_available_ram_bytes: int = 2_048 * MIB
    poll_interval_seconds: float = 0.25
    cleanup_timeout_seconds: float = 15.0
    maximum_runtime_seconds: float = 7_200.0


def _utc_now() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="microseconds").replace(
        "+00:00", "Z"
    )


def _validate_limits(limits: GuardLimits) -> None:
    if (
        isinstance(limits.minimum_available_ram_bytes, bool)
        or not isinstance(limits.minimum_available_ram_bytes, int)
        or limits.minimum_available_ram_bytes < 0
    ):
        raise ValueError(
            "minimum_available_ram_bytes must be a non-negative integer"
        )
    for field_name in (
        "poll_interval_seconds",
        "cleanup_timeout_seconds",
        "maximum_runtime_seconds",
    ):
        value = getattr(limits, field_name)
        if (
            isinstance(value, bool)
            or not isinstance(value, (int, float))
            or not math.isfinite(float(value))
            or float(value) <= 0
        ):
            raise ValueError(f"{field_name} must be finite and positive")


def _validate_inputs(
    command: Sequence[str],
    cwd: Path,
    log_path: Path,
    evidence_path: Path,
    expected_exit: str,
    limits: GuardLimits,
) -> list[str]:
    if os.name != "nt":
        raise RuntimeError("the owned-process guard requires Windows")
    values = list(command)
    if not values or not all(isinstance(value, str) and value for value in values):
        raise ValueError("command must contain non-empty strings")
    if expected_exit not in {"zero", "nonzero"}:
        raise ValueError("expected_exit must be 'zero' or 'nonzero'")
    if not cwd.is_dir():
        raise ValueError(f"working directory does not exist: {cwd}")
    if log_path == evidence_path:
        raise ValueError("log_path and evidence_path must be different")
    _validate_limits(limits)
    return values


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _atomic_write_json(path: Path, value: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary_path: Path | None = None
    try:
        with tempfile.NamedTemporaryFile(
            mode="w",
            encoding="utf-8",
            newline="\n",
            prefix=f".{path.name}.tmp-",
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


def _exit_matches(exit_code: int | None, expected_exit: str) -> bool:
    if not isinstance(exit_code, int):
        return False
    return exit_code == 0 if expected_exit == "zero" else exit_code != 0


def _sample_job_processes(
    job: KillOnCloseJob,
) -> tuple[list[int], int | None, int | None, list[int]]:
    """Sample every PID returned by the Job Object without tree heuristics."""
    active_pids = job.active_pids()
    if not active_pids:
        return active_pids, None, None, []
    working_set_bytes = 0
    private_bytes = 0
    failed_pids: list[int] = []
    for pid in active_pids:
        memory = process_memory_bytes(pid)
        if memory is None:
            failed_pids.append(pid)
            continue
        working_set_bytes += memory[0]
        private_bytes += memory[1]
    if failed_pids:
        return active_pids, None, None, sorted(failed_pids)
    return active_pids, working_set_bytes, private_bytes, []


def run_guarded_command(
    command: Sequence[str],
    *,
    cwd: Path,
    log_path: Path,
    evidence_path: Path,
    expected_exit: Literal["zero", "nonzero"],
    limits: GuardLimits = GuardLimits(),
) -> dict[str, object]:
    """Run one owned process tree and atomically persist exit/RAM/cleanup evidence."""
    cwd = Path(cwd).resolve()
    log_path = Path(log_path).resolve()
    evidence_path = Path(evidence_path).resolve()
    command_values = _validate_inputs(
        command,
        cwd,
        log_path,
        evidence_path,
        expected_exit,
        limits,
    )
    log_path.parent.mkdir(parents=True, exist_ok=True)
    evidence_path.parent.mkdir(parents=True, exist_ok=True)

    started_monotonic = time.monotonic()
    record: dict[str, object] = {
        "schema": GUARD_SCHEMA,
        "command": command_values,
        "working_directory": str(cwd),
        "log_path": str(log_path),
        "evidence_path": str(evidence_path),
        "expected_exit": expected_exit,
        "started_utc": _utc_now(),
        "ended_utc": None,
        "elapsed_seconds": None,
        "configured_minimum_available_ram_bytes": (
            limits.minimum_available_ram_bytes
        ),
        "poll_interval_seconds": float(limits.poll_interval_seconds),
        "cleanup_timeout_seconds": float(limits.cleanup_timeout_seconds),
        "maximum_runtime_seconds": float(limits.maximum_runtime_seconds),
        "observed_available_ram_bytes": {
            "before": None,
            "minimum": None,
            "after": None,
        },
        "memory_sample_count": 0,
        "peak_working_set_bytes": None,
        "peak_private_bytes": None,
        "memory_query_failed_pids": [],
        "root_pid": None,
        "observed_pids": [],
        "child_process_observed": False,
        "exit_code": None,
        "timed_out": False,
        "low_memory_stop": False,
        "termination_reason": None,
        "msbuild_disable_node_reuse": "1",
        "launch_governance": {
            "created_suspended": False,
            "assigned_before_resume": False,
            "cpu_affinity_mask": None,
            "cpu_rate_hard_cap_percent": None,
        },
        "job_object": {
            "setup_ok": False,
            "query_ok": False,
            "terminate_job_called": False,
            "queried_active_process_count_after_cleanup": None,
            "survivor_pids_after_cleanup": None,
        },
        "emergency_actions": [],
        "validation_errors": [],
        "log_sha256": None,
        "valid": False,
    }
    errors: list[str] = record["validation_errors"]
    emergency_actions: list[dict] = record["emergency_actions"]
    observed_pids: set[int] = set()
    memory_query_failed_pids: set[int] = set()
    memory_sample_count = 0
    peak_working_set_bytes: int | None = None
    peak_private_bytes: int | None = None
    available_samples: list[int] = []
    job: KillOnCloseJob | None = None
    process: subprocess.Popen[bytes] | None = None
    assigned = False
    log_handle = None
    log_temporary_path: Path | None = None

    file_descriptor, temporary_name = tempfile.mkstemp(
        prefix=f".{log_path.name}.tmp-",
        dir=log_path.parent,
    )
    os.close(file_descriptor)
    log_temporary_path = Path(temporary_name)

    try:
        log_handle = log_temporary_path.open("wb", buffering=0)
        available_before = available_ram_bytes()
        record["observed_available_ram_bytes"]["before"] = available_before
        if available_before is None:
            record["termination_reason"] = "available_ram_query_failure"
            _append_error(errors, "available RAM query failed before launch")
        else:
            available_samples.append(available_before)
            if available_before < limits.minimum_available_ram_bytes:
                record["low_memory_stop"] = True
                record["termination_reason"] = "minimum_available_ram_before_launch"
                _append_error(
                    errors,
                    "available RAM before launch is below the configured floor",
                )

        if not errors:
            job = KillOnCloseJob(
                f"OfficialOpenVINOGuard-{secrets.token_hex(16)}"
            )
            environment = os.environ.copy()
            environment["MSBUILDDISABLENODEREUSE"] = "1"
            creation_flags = (
                subprocess.CREATE_NEW_PROCESS_GROUP | CREATE_SUSPENDED
            )
            process = subprocess.Popen(
                command_values,
                cwd=cwd,
                env=environment,
                stdin=subprocess.DEVNULL,
                stdout=log_handle,
                stderr=subprocess.STDOUT,
                creationflags=creation_flags,
            )
            record["launch_governance"]["created_suspended"] = True
            record["root_pid"] = process.pid
            observed_pids.add(process.pid)
            job.assign_pid(process.pid)
            assigned = True
            (
                active_pids,
                sample_working_set_bytes,
                sample_private_bytes,
                failed_pids,
            ) = _sample_job_processes(job)
            observed_pids.update(active_pids)
            memory_query_failed_pids.update(failed_pids)
            if sample_working_set_bytes is not None:
                memory_sample_count += 1
                peak_working_set_bytes = sample_working_set_bytes
                peak_private_bytes = sample_private_bytes
            _resume_suspended_process(process.pid)
            record["launch_governance"]["assigned_before_resume"] = True

            while process.poll() is None:
                elapsed = time.monotonic() - started_monotonic
                try:
                    (
                        active_pids,
                        sample_working_set_bytes,
                        sample_private_bytes,
                        failed_pids,
                    ) = _sample_job_processes(job)
                    observed_pids.update(active_pids)
                    memory_query_failed_pids.update(failed_pids)
                    if sample_working_set_bytes is not None:
                        memory_sample_count += 1
                        peak_working_set_bytes = max(
                            peak_working_set_bytes or 0,
                            sample_working_set_bytes,
                        )
                        peak_private_bytes = max(
                            peak_private_bytes or 0,
                            sample_private_bytes,
                        )
                except (OSError, RuntimeError) as error:
                    record["termination_reason"] = "job_query_failure"
                    _append_error(errors, f"Job Object sampling query failed: {error}")
                    break

                available = available_ram_bytes()
                if available is None:
                    record["termination_reason"] = "available_ram_query_failure"
                    _append_error(errors, "available RAM query failed during run")
                    break
                available_samples.append(available)
                if available < limits.minimum_available_ram_bytes:
                    record["low_memory_stop"] = True
                    record["termination_reason"] = "minimum_available_ram"
                    _append_error(
                        errors,
                        "available RAM fell below the configured floor",
                    )
                    break
                if elapsed >= limits.maximum_runtime_seconds:
                    record["timed_out"] = True
                    record["termination_reason"] = "maximum_runtime"
                    _append_error(
                        errors,
                        "child exceeded maximum_runtime_seconds",
                    )
                    break
                try:
                    process.wait(timeout=limits.poll_interval_seconds)
                except subprocess.TimeoutExpired:
                    pass
    except Exception as error:
        if record["termination_reason"] is None:
            record["termination_reason"] = "launch_or_monitor_failure"
        _append_error(
            errors,
            f"guarded execution failed: {type(error).__name__}: {error}",
        )
    finally:
        try:
            if process is not None and not assigned and process.poll() is None:
                action = _taskkill(process.pid)
                action["reason"] = "Job Object assignment failure fallback"
                emergency_actions.append(action)
            if job is not None:
                try:
                    job_evidence, emergency = _cleanup_job(
                        job,
                        process if assigned else None,
                        "guarded child",
                        errors,
                        emergency_actions,
                        cleanup_timeout_seconds=limits.cleanup_timeout_seconds,
                    )
                    job_evidence["setup_ok"] = assigned
                    record["job_object"] = job_evidence
                    if emergency:
                        _append_error(errors, "guarded cleanup required fallback")
                except Exception as error:
                    _append_error(
                        errors,
                        "guarded Job Object cleanup failed: "
                        f"{type(error).__name__}: {error}",
                    )
            if process is not None:
                if not _wait_process(
                    process,
                    limits.cleanup_timeout_seconds,
                    "guarded child",
                    errors,
                ):
                    action = _taskkill(process.pid)
                    action["reason"] = "post-Job wait fallback"
                    emergency_actions.append(action)
                    _wait_process(
                        process,
                        limits.cleanup_timeout_seconds,
                        "guarded child post-taskkill",
                        errors,
                    )
                record["exit_code"] = process.returncode
            if log_handle is not None and not log_handle.closed:
                try:
                    log_handle.flush()
                    os.fsync(log_handle.fileno())
                except Exception as error:
                    _append_error(
                        errors,
                        f"log flush failed: {type(error).__name__}: {error}",
                    )
        finally:
            _close_run_resources(
                [log_handle] if log_handle is not None else [],
                [job] if job is not None else [],
                errors,
            )

    if log_temporary_path is None or not log_temporary_path.is_file():
        _append_error(errors, "temporary log is missing")
    else:
        try:
            os.replace(log_temporary_path, log_path)
            log_temporary_path = None
        finally:
            if log_temporary_path is not None:
                log_temporary_path.unlink(missing_ok=True)

    available_after = available_ram_bytes()
    record["observed_available_ram_bytes"]["after"] = available_after
    if available_after is None:
        _append_error(errors, "available RAM query failed after cleanup")
    else:
        available_samples.append(available_after)
    record["observed_available_ram_bytes"]["minimum"] = (
        min(available_samples) if available_samples else None
    )
    record["memory_sample_count"] = memory_sample_count
    record["peak_working_set_bytes"] = peak_working_set_bytes
    record["peak_private_bytes"] = peak_private_bytes
    record["memory_query_failed_pids"] = sorted(memory_query_failed_pids)
    record["observed_pids"] = sorted(observed_pids)
    root_pid = record["root_pid"]
    record["child_process_observed"] = (
        isinstance(root_pid, int)
        and any(pid != root_pid for pid in observed_pids)
    )

    job_evidence = record["job_object"]
    if not (
        job_evidence["setup_ok"] is True
        and job_evidence["query_ok"] is True
        and job_evidence["queried_active_process_count_after_cleanup"] == 0
        and job_evidence["survivor_pids_after_cleanup"] == []
    ):
        _append_error(errors, "zero-survivor Job Object proof is missing")
    if emergency_actions:
        _append_error(errors, "emergency process cleanup was required")
    if (
        memory_sample_count < 1
        or not isinstance(peak_working_set_bytes, int)
        or peak_working_set_bytes <= 0
        or not isinstance(peak_private_bytes, int)
        or peak_private_bytes <= 0
    ):
        _append_error(errors, "complete Job-PID process-memory evidence is missing")
    if not _exit_matches(record["exit_code"], expected_exit):
        _append_error(
            errors,
            f"child exit code did not match expected_exit={expected_exit}",
        )
    minimum_observed = record["observed_available_ram_bytes"]["minimum"]
    if (
        not isinstance(minimum_observed, int)
        or minimum_observed < limits.minimum_available_ram_bytes
    ):
        _append_error(errors, "measured available RAM did not satisfy the floor")
    if log_path.is_file():
        record["log_sha256"] = _sha256_file(log_path)
    else:
        _append_error(errors, "final log is missing")

    record["ended_utc"] = _utc_now()
    record["elapsed_seconds"] = time.monotonic() - started_monotonic
    record["valid"] = not errors
    _atomic_write_json(evidence_path, record)
    return record


def _parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--cwd", type=Path, required=True)
    parser.add_argument("--log", type=Path, required=True)
    parser.add_argument("--evidence", type=Path, required=True)
    parser.add_argument(
        "--expected-exit",
        choices=("zero", "nonzero"),
        required=True,
    )
    parser.add_argument(
        "--minimum-available-ram-mib",
        type=int,
        default=2048,
    )
    parser.add_argument(
        "--poll-interval-seconds",
        type=float,
        default=0.25,
    )
    parser.add_argument(
        "--cleanup-timeout-seconds",
        type=float,
        default=15.0,
    )
    parser.add_argument(
        "--timeout-seconds",
        type=float,
        default=7_200.0,
    )
    parser.add_argument("command", nargs=argparse.REMAINDER)
    args = parser.parse_args(argv)
    if not args.command or args.command[0] != "--":
        parser.error("child command requires a literal -- separator")
    args.command = args.command[1:]
    if not args.command:
        parser.error("a child command is required after the literal -- separator")
    return args


def main(argv: Sequence[str] | None = None) -> int:
    args = _parse_args(argv)
    record = run_guarded_command(
        args.command,
        cwd=args.cwd,
        log_path=args.log,
        evidence_path=args.evidence,
        expected_exit=args.expected_exit,
        limits=GuardLimits(
            minimum_available_ram_bytes=args.minimum_available_ram_mib * MIB,
            poll_interval_seconds=args.poll_interval_seconds,
            cleanup_timeout_seconds=args.cleanup_timeout_seconds,
            maximum_runtime_seconds=args.timeout_seconds,
        ),
    )
    print(
        json.dumps(
            {
                "schema": record["schema"],
                "evidence_path": record["evidence_path"],
                "exit_code": record["exit_code"],
                "valid": record["valid"],
            },
            sort_keys=True,
        )
    )
    return 0 if record["valid"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
