"""Measure two fresh production-core processes and publish strict capability evidence."""

from __future__ import annotations

import argparse
import csv
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
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

REPO_ROOT = SCRIPT_DIR.parent.parent
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from scripts.testing.campaigns.openvino import owned_process_guard  # noqa: E402
from scripts.testing.campaigns.openvino.owned_process_guard import (  # noqa: E402
    CREATE_SUSPENDED,
    KillOnCloseJob,
    _append_error,
    _cleanup_job,
    _close_run_resources,
    _resume_suspended_process,
    _set_process_affinity_mask,
    _taskkill,
    _wait_process,
    available_ram_bytes,
    process_memory_bytes,
)


MARKER_PREFIX = "TURBOQUANT_REFERENCE_CAPABILITY_JSON="
MARKER_SCHEMA = "openvino-turboquant-reference-capability/v1"
RUN_SCHEMA = "openvino-turboquant-reference-capability-run/v3"
EVIDENCE_SCHEMA = "openvino-turboquant-reference-capability-evidence/v3"
EXPECTED_TEST_NAME = (
    "TurboQuantStatefulGraph.PersistsOnlyCompressedStateForOneHundredSteps"
)
EXPECTED_GTEST_FILTER = f"--gtest_filter={EXPECTED_TEST_NAME}"
EXPECTED_HASH_ALGORITHM = "FNV-1a-64-labelled-step-result-raw-bytes"
NONCE_ENVIRONMENT_VARIABLE = "OPENVINO_TURBOQUANT_CAPABILITY_NONCE"
EXACT_INTERVAL_MS = 100
EXACT_TIMEOUT_SECONDS = 300.0
EXACT_MINIMUM_AVAILABLE_RAM_MB = 2048.0
EXACT_WORKLOAD_AFFINITY_MASK = 1
EXACT_WORKLOAD_CPU_RATE = 100
EXACT_WORKLOAD_CPU_RATE_HARD_CAP_PERCENT = 1.0
EXPECTED_RUN_ARTIFACTS = {
    "command": "command.json",
    "environment": "environment.json",
    "stdout": "stdout.txt",
    "stderr": "stderr.txt",
    "sampler_stdout": "sampler.stdout.txt",
    "sampler_stderr": "sampler.stderr.txt",
    "memory_samples": "memory.jsonl",
    "utilization": "utilization.csv",
    "gpu": "gpu.csv",
    "sampler_ready": "utilization.ready",
    "sampling_start": "utilization.start",
    "gtest": "gtest.json",
    "run": "run.json",
}

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


def resolve_expected_derived_commit(
    explicit_commit: str | None,
    source_identity: Path | None,
) -> str:
    """Resolve the required derived commit without embedding campaign state."""

    if explicit_commit is not None and re.fullmatch(
        r"[0-9a-f]{40}", explicit_commit
    ) is None:
        raise ValueError(
            "expected derived commit must be a lowercase 40-character Git ID"
        )

    identity_commit: str | None = None
    if source_identity is not None:
        identity_path = Path(source_identity).resolve()
        try:
            identity = json.loads(identity_path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as exc:
            raise ValueError(
                f"source identity is unreadable: {identity_path}"
            ) from exc
        if not isinstance(identity, dict):
            raise ValueError("source identity must contain a JSON object")
        identity_commit = identity.get("patch_commit")
        if not isinstance(identity_commit, str) or re.fullmatch(
            r"[0-9a-f]{40}", identity_commit
        ) is None:
            raise ValueError(
                "source identity patch_commit must be a lowercase "
                "40-character Git ID"
            )

    if explicit_commit is None and identity_commit is None:
        raise ValueError(
            "an expected derived commit or source identity is required"
        )
    if (
        explicit_commit is not None
        and identity_commit is not None
        and explicit_commit != identity_commit
    ):
        raise ValueError(
            "explicit expected derived commit conflicts with source identity"
        )
    resolved_commit = (
        explicit_commit if explicit_commit is not None else identity_commit
    )
    if resolved_commit is None:
        raise AssertionError("derived commit resolution invariant failed")
    return resolved_commit


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


def _same_path(left: Path | str, right: Path | str) -> bool:
    return os.path.normcase(str(Path(left).resolve())) == os.path.normcase(
        str(Path(right).resolve())
    )


def _path_is_within(child: Path | str, parent: Path | str) -> bool:
    child_text = os.path.normcase(str(Path(child).resolve()))
    parent_text = os.path.normcase(str(Path(parent).resolve()))
    try:
        return os.path.commonpath([child_text, parent_text]) == parent_text
    except ValueError:
        return False


def validate_build_provenance(
    derived_repo: Path, build_dir: Path, executable: Path
) -> dict:
    """Bind the executable's CMake configuration to the selected source tree."""
    source = Path(derived_repo).resolve()
    build = Path(build_dir).resolve()
    binary = Path(executable).absolute()
    if not source.is_dir():
        raise ValueError(f"derived source directory does not exist: {source}")
    if not build.is_dir():
        raise ValueError(f"build directory does not exist: {build}")
    cache = build / "CMakeCache.txt"
    if not cache.is_file():
        raise ValueError(f"CMakeCache.txt is missing from build directory: {build}")
    if not binary.is_file():
        raise ValueError(f"capability executable does not exist: {binary}")
    if not _path_is_within(binary, build):
        raise ValueError(
            "capability executable is outside the declared build directory"
        )

    values: dict[str, str] = {}
    for line in cache.read_text(encoding="utf-8-sig", errors="strict").splitlines():
        match = re.fullmatch(r"([^:#=]+):[^=]+=(.*)", line)
        if match:
            values[match.group(1)] = match.group(2)
    cmake_home = values.get("CMAKE_HOME_DIRECTORY")
    if not cmake_home:
        raise ValueError("CMakeCache.txt has no CMAKE_HOME_DIRECTORY")
    configured_source = Path(cmake_home).resolve()
    if not _same_path(configured_source, source):
        raise ValueError(
            "CMAKE_HOME_DIRECTORY does not match the selected derived checkout"
        )
    generator = values.get("CMAKE_GENERATOR")
    if not generator:
        raise ValueError("CMakeCache.txt has no CMAKE_GENERATOR")
    return {
        "build_directory": str(build),
        "cmake_cache": str(cache.resolve()),
        "cmake_cache_sha256": _sha256_file(cache),
        "cmake_generator": generator,
        "cmake_home_directory": str(configured_source),
        "executable_within_build_directory": True,
    }


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


def environment_identity_sha256(identity: dict) -> str:
    """Hash every environment-identity field except the digest itself."""
    content = {
        key: value for key, value in identity.items() if key != "identity_sha256"
    }
    encoded = json.dumps(
        content, sort_keys=True, separators=(",", ":"), ensure_ascii=True
    ).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


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
    identity["identity_sha256"] = environment_identity_sha256(identity)
    return identity


def _parse_bool(value: str, field: str) -> bool:
    normalized = value.strip().lower()
    if normalized == "true":
        return True
    if normalized == "false":
        return False
    raise ValueError(f"{field} must be true or false")


UTILIZATION_VALUE_FIELDS = ("cpu_percent",)
GPU_VALUE_FIELDS = (
    "gpu_percent",
    "gpu_engine_count",
    "gpu_dedicated_mb",
    "gpu_shared_mb",
)
UTILIZATION_TIMING_FIELDS = (
    "sample_deadline_elapsed_ms",
    "sample_lateness_ms",
)
FIRST_CPU_SAMPLE_DEFINITION = "lifetime_average_since_workload_resume"
INTERVAL_CPU_SAMPLE_DEFINITION = "interval_delta"


def _read_utilization(path: Path) -> tuple[dict | None, dict, list[str]]:
    errors: list[str] = []
    samples: dict[str, list[float]] = {
        field: []
        for field in (*UTILIZATION_VALUE_FIELDS, *UTILIZATION_TIMING_FIELDS)
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
                *UTILIZATION_TIMING_FIELDS,
                "cpu_sample_definition",
            }
            if reader.fieldnames is None or not required.issubset(reader.fieldnames):
                return (
                    None,
                    samples,
                    ["utilization CSV is missing required CPU cadence columns"],
                )
            rows = list(reader)
    except (OSError, csv.Error) as error:
        return None, samples, [f"utilization CSV could not be read: {error}"]

    for index, row in enumerate(rows, start=1):
        try:
            cpu = float(row["cpu_percent"])
            if not math.isfinite(cpu) or not 0.0 <= cpu <= 100.0:
                raise ValueError("CPU value is outside [0, 100]")
            samples["cpu_percent"].append(cpu)
            sample_deadline = float(row["sample_deadline_elapsed_ms"])
            sample_lateness = float(row["sample_lateness_ms"])
            if (
                not math.isfinite(sample_deadline)
                or sample_deadline < EXACT_INTERVAL_MS
                or not math.isclose(
                    sample_deadline % EXACT_INTERVAL_MS,
                    0.0,
                    abs_tol=1e-6,
                )
                or (
                    samples["sample_deadline_elapsed_ms"]
                    and sample_deadline
                    <= samples["sample_deadline_elapsed_ms"][-1]
                )
                or not math.isfinite(sample_lateness)
                or sample_lateness < 0.0
            ):
                raise ValueError("sampling deadline/lateness values are invalid")
            samples["sample_deadline_elapsed_ms"].append(sample_deadline)
            samples["sample_lateness_ms"].append(sample_lateness)
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
        except (KeyError, TypeError, ValueError) as error:
            _append_error(
                errors, f"utilization CSV row {index} is invalid: {error}"
            )

    if not rows:
        _append_error(errors, "utilization CSV has no real counter sample")
    summary: dict[str, Any] = {"row_count": len(rows)}
    for field in (*UTILIZATION_VALUE_FIELDS, *UTILIZATION_TIMING_FIELDS):
        try:
            summary[field] = summarize_samples(samples[field])
        except ValueError:
            summary[field] = None
            _append_error(errors, f"{field} utilization evidence is absent")
    summary["cpu_sample_definitions"] = {
        FIRST_CPU_SAMPLE_DEFINITION: samples["cpu_sample_definition"].count(
            FIRST_CPU_SAMPLE_DEFINITION
        ),
        INTERVAL_CPU_SAMPLE_DEFINITION: samples["cpu_sample_definition"].count(
            INTERVAL_CPU_SAMPLE_DEFINITION
        ),
    }
    return summary, samples, errors


GPU_QUERY_TIME_FIELDS = (
    "gpu_engine_query_started_utc",
    "gpu_engine_query_completed_utc",
    "gpu_memory_query_started_utc",
    "gpu_memory_query_completed_utc",
)


def _read_gpu(path: Path) -> tuple[dict | None, dict, list[str]]:
    errors: list[str] = []
    samples: dict[str, list[Any]] = {
        **{field: [] for field in GPU_VALUE_FIELDS},
        "gpu_engine_query_ok": [],
        "gpu_memory_query_ok": [],
        **{field: [] for field in GPU_QUERY_TIME_FIELDS},
    }
    if not path.exists():
        return None, samples, ["GPU CSV is missing"]
    try:
        with path.open(encoding="utf-8-sig", newline="") as handle:
            reader = csv.DictReader(handle)
            required = {
                "observation_utc",
                *GPU_VALUE_FIELDS,
                "gpu_engine_query_ok",
                "gpu_memory_query_ok",
                *GPU_QUERY_TIME_FIELDS,
            }
            if reader.fieldnames is None or not required.issubset(reader.fieldnames):
                return None, samples, ["GPU CSV is missing required query columns"]
            rows = list(reader)
    except (OSError, csv.Error) as error:
        return None, samples, [f"GPU CSV could not be read: {error}"]

    if len(rows) != 1:
        _append_error(errors, "GPU CSV must contain exactly one observation row")
    engine_success_count = 0
    memory_success_count = 0
    for index, row in enumerate(rows, start=1):
        try:
            observation = _parse_utc_timestamp(row["observation_utc"])
            if observation is None:
                raise ValueError("GPU observation timestamp is invalid")
            engine_ok = _parse_bool(
                row["gpu_engine_query_ok"], "gpu_engine_query_ok"
            )
            memory_ok = _parse_bool(
                row["gpu_memory_query_ok"], "gpu_memory_query_ok"
            )
            samples["gpu_engine_query_ok"].append(engine_ok)
            samples["gpu_memory_query_ok"].append(memory_ok)
            for prefix, query_ok in (
                ("gpu_engine", engine_ok),
                ("gpu_memory", memory_ok),
            ):
                started_field = f"{prefix}_query_started_utc"
                completed_field = f"{prefix}_query_completed_utc"
                started = _parse_utc_timestamp(row[started_field])
                completed = _parse_utc_timestamp(row[completed_field])
                if query_ok and (
                    started is None
                    or completed is None
                    or completed < started
                    or completed > observation
                ):
                    raise ValueError(f"{prefix} query timing is invalid")
                samples[started_field].append(row[started_field])
                samples[completed_field].append(row[completed_field])

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
            _append_error(errors, f"GPU CSV row {index} is invalid: {error}")

    summary: dict[str, Any] = {"row_count": len(rows)}
    for field in GPU_VALUE_FIELDS:
        try:
            summary[field] = summarize_samples(samples[field])
        except ValueError:
            summary[field] = None
            _append_error(errors, f"{field} GPU evidence is absent")
    summary["gpu_engine_query"] = {
        "success_count": engine_success_count,
        "failure_count": len(rows) - engine_success_count,
        "all_succeeded": len(rows) == 1 and engine_success_count == 1,
    }
    summary["gpu_memory_query"] = {
        "success_count": memory_success_count,
        "failure_count": len(rows) - memory_success_count,
        "all_succeeded": len(rows) == 1 and memory_success_count == 1,
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
    source_file = case.get("file")
    source_line = case.get("line")
    if (
        test_name != EXPECTED_TEST_NAME
        or case.get("status") != "RUN"
        or case.get("result") != "COMPLETED"
        or not isinstance(source_file, str)
        or not Path(source_file).is_absolute()
        or not isinstance(source_line, int)
        or isinstance(source_line, bool)
        or source_line <= 0
    ):
        raise ValueError("GTest JSON does not contain the required passed GTest")
    return {
        "tests": 1,
        "passed": 1,
        "failures": 0,
        "errors": 0,
        "disabled": 0,
        "test_name": test_name,
        "source_file": source_file,
        "source_line": source_line,
    }


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
            "workload_affinity_mask": None,
            "workload_affinity_set_before_resume": False,
            "workload_cpu_rate_hard_cap_percent": None,
            "workload_cpu_rate_control_set_before_resume": False,
            "sampler_created_suspended": False,
            "sampler_assigned_before_resume": False,
        },
        "sampler_start_gate": {
            "ready_before_workload_resume": False,
            "released_after_workload_resume": False,
            "sampler_ready_observed_utc": None,
            "workload_resumed_utc": None,
            "sampling_start_released_utc": None,
            "sampler_ready_observed_elapsed_seconds": None,
            "workload_resumed_elapsed_seconds": None,
            "sampling_start_released_elapsed_seconds": None,
        },
        "started_utc": None,
        "ended_utc": None,
        "elapsed_seconds": None,
        "workload_sampling_window_seconds": None,
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
            **{
                field: []
                for field in (
                    *UTILIZATION_VALUE_FIELDS,
                    *UTILIZATION_TIMING_FIELDS,
                )
            },
            "cpu_sample_definition": [],
        },
        "gpu_summary": None,
        "gpu_samples": {
            **{field: [] for field in GPU_VALUE_FIELDS},
            "gpu_engine_query_ok": [],
            "gpu_memory_query_ok": [],
            **{field: [] for field in GPU_QUERY_TIME_FIELDS},
        },
        "marker": None,
        "gtest": None,
        "exit_code": None,
        "timed_out": False,
        "low_memory_stop": False,
        "emergency_stop": False,
        "cleanup_duration_seconds": None,
        "sampler_exit_code": None,
        "runtime_openvino_dll_sha256_before": None,
        "runtime_openvino_dll_sha256_after": None,
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
        "artifacts": dict(EXPECTED_RUN_ARTIFACTS),
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
        "gpu": output_dir / "gpu.csv",
        "ready": output_dir / "utilization.ready",
        "start": output_dir / "utilization.start",
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
        paths["gpu"],
    ):
        path.touch()
    paths["ready"].unlink(missing_ok=True)
    paths["start"].unlink(missing_ok=True)
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
    started_counter_ns: int | None = None
    workload_resumed_counter_ns: int | None = None
    cleanup_started: float | None = None

    if not errors:
        try:
            workload_job = KillOnCloseJob(
                f"OpenVINOTurboQuantWorkload-{run_nonce}"
            )
            workload_job.set_cpu_rate_hard_cap(EXACT_WORKLOAD_CPU_RATE)
            record["launch_governance"][
                "workload_cpu_rate_hard_cap_percent"
            ] = EXACT_WORKLOAD_CPU_RATE_HARD_CAP_PERCENT
            record["launch_governance"][
                "workload_cpu_rate_control_set_before_resume"
            ] = True
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
            started_counter_ns = time.perf_counter_ns()
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
            _set_process_affinity_mask(
                workload.pid, EXACT_WORKLOAD_AFFINITY_MASK
            )
            record["launch_governance"][
                "workload_affinity_mask"
            ] = EXACT_WORKLOAD_AFFINITY_MASK
            record["launch_governance"][
                "workload_affinity_set_before_resume"
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
                "-GpuOutputPath",
                str(paths["gpu"]),
                "-ReadyPath",
                str(paths["ready"]),
                "-StartPath",
                str(paths["start"]),
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

            sampler_startup_deadline = min(
                started_monotonic + timeout_seconds,
                time.monotonic() + 10.0,
            )
            while not paths["ready"].is_file():
                now = time.monotonic()
                if now - started_monotonic >= timeout_seconds:
                    record["timed_out"] = True
                    break
                if now >= sampler_startup_deadline:
                    _append_error(
                        errors,
                        "utilization sampler readiness timed out",
                    )
                    break
                sampler_exit_code = sampler.poll()
                if sampler_exit_code is not None:
                    _append_error(
                        errors,
                        "utilization sampler exited before reporting ready "
                        f"with code {sampler_exit_code}",
                    )
                    break
                time.sleep(0.005)

            if paths["ready"].is_file() and not record["timed_out"] and not errors:
                gate = record["sampler_start_gate"]
                gate["sampler_ready_observed_elapsed_seconds"] = (
                    (time.perf_counter_ns() - started_counter_ns) / 1_000_000_000
                )
                gate["sampler_ready_observed_utc"] = _utc_now()
                gate["ready_before_workload_resume"] = True

                _resume_suspended_process(workload.pid)
                record["launch_governance"][
                    "workload_assigned_before_resume"
                ] = True
                gate["workload_resumed_elapsed_seconds"] = (
                    (time.perf_counter_ns() - started_counter_ns) / 1_000_000_000
                )
                gate["workload_resumed_utc"] = _utc_now()
                workload_resumed_counter_ns = time.perf_counter_ns()

                paths["start"].write_text(
                    gate["workload_resumed_utc"] + "\n",
                    encoding="ascii",
                )
                gate["sampling_start_released_elapsed_seconds"] = (
                    (time.perf_counter_ns() - started_counter_ns) / 1_000_000_000
                )
                gate["sampling_start_released_utc"] = _utc_now()
                gate["released_after_workload_resume"] = True

                interval_seconds = interval_ms / 1000.0
                next_sample = time.monotonic()
                while True:
                    now = time.monotonic()
                    if now - started_monotonic >= timeout_seconds:
                        record["timed_out"] = True
                        break
                    if workload.poll() is not None:
                        if workload_resumed_counter_ns is not None:
                            record["workload_sampling_window_seconds"] = (
                                time.perf_counter_ns()
                                - workload_resumed_counter_ns
                            ) / 1_000_000_000
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
                            errors,
                            f"workload Job Object sampling query failed: {error}",
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
                try:
                    paths["stop"].write_text("stop\n", encoding="ascii")
                except Exception as error:
                    _append_error(
                        errors,
                        f"sampler stop signal failed: {type(error).__name__}: {error}",
                    )

                if sampler is not None and sampler_assigned:
                    try:
                        sampler.wait(timeout=10.0)
                    except subprocess.TimeoutExpired:
                        pass
                    except Exception as error:
                        _append_error(
                            errors,
                            f"sampler initial wait failed: "
                            f"{type(error).__name__}: {error}",
                        )
                try:
                    if (
                        sampler is not None
                        and not sampler_assigned
                        and sampler.poll() is None
                    ):
                        action = _taskkill(sampler.pid)
                        action["reason"] = "sampler assignment failure fallback"
                        record["emergency_actions"].append(action)
                        record["emergency_stop"] = True
                except Exception as error:
                    _append_error(
                        errors,
                        f"sampler assignment cleanup failed: "
                        f"{type(error).__name__}: {error}",
                    )
                    record["emergency_stop"] = True
                try:
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
                except Exception as error:
                    _append_error(
                        errors,
                        f"sampler governed cleanup failed: "
                        f"{type(error).__name__}: {error}",
                    )
                    record["emergency_stop"] = True

                try:
                    if (
                        workload is not None
                        and not workload_assigned
                        and workload.poll() is None
                    ):
                        action = _taskkill(workload.pid)
                        action["reason"] = "workload assignment failure fallback"
                        record["emergency_actions"].append(action)
                        record["emergency_stop"] = True
                except Exception as error:
                    _append_error(
                        errors,
                        f"workload assignment cleanup failed: "
                        f"{type(error).__name__}: {error}",
                    )
                    record["emergency_stop"] = True
                try:
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
                except Exception as error:
                    _append_error(
                        errors,
                        f"workload governed cleanup failed: "
                        f"{type(error).__name__}: {error}",
                    )
                    record["emergency_stop"] = True

                for process, label, exit_key in (
                    (sampler, "sampler", "sampler_exit_code"),
                    (workload, "workload", "exit_code"),
                ):
                    if process is None:
                        continue
                    try:
                        process.wait(timeout=5.0)
                    except Exception as error:
                        action = _taskkill(process.pid)
                        action["reason"] = f"{label} wait fallback"
                        record["emergency_actions"].append(action)
                        record["emergency_stop"] = True
                        _append_error(
                            errors,
                            f"{label} wait required fallback: "
                            f"{type(error).__name__}: {error}",
                        )
                        _wait_process(
                            process,
                            timeout=5.0,
                            label=f"{label} post-taskkill",
                            errors=errors,
                        )
                    record[exit_key] = process.returncode
            finally:
                _close_run_resources(
                    file_handles,
                    [sampler_job, workload_job],
                    errors,
                )
                record["cleanup_duration_seconds"] = (
                    time.monotonic() - cleanup_started
                )
                if started_monotonic is not None:
                    record["elapsed_seconds"] = (
                        time.monotonic() - started_monotonic
                    )
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
    gpu_summary, gpu_samples, gpu_errors = _read_gpu(paths["gpu"])
    record["gpu_summary"] = gpu_summary
    record["gpu_samples"] = gpu_samples
    for error in gpu_errors:
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


def _parse_utc_timestamp(value: Any) -> datetime | None:
    if not isinstance(value, str) or not value:
        return None
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError:
        return None
    if parsed.tzinfo is None or parsed.utcoffset() is None:
        return None
    if parsed.utcoffset().total_seconds() != 0:
        return None
    return parsed


def expected_cpu_sample_count(
    workload_sampling_window_seconds: float, interval_ms: int
) -> int:
    """Return the minimum publishable CPU rows for a measured workload window."""
    if (
        not _is_finite_number(workload_sampling_window_seconds)
        or workload_sampling_window_seconds <= 0
        or interval_ms <= 0
    ):
        raise ValueError("workload sampling window is invalid")
    scheduled = math.floor(
        (workload_sampling_window_seconds * 1000.0) / interval_ms + 1e-9
    )
    # Allow the terminal scheduled point to race with the stop signal, while
    # still requiring a real interval delta for every accepted run.
    return max(2, scheduled - 1)


def _validate_summary_and_samples(run: dict) -> None:
    summary = run.get("utilization_summary")
    samples = run.get("utilization_samples")
    _require(isinstance(summary, dict), "CPU utilization summary is missing")
    _require(isinstance(samples, dict), "CPU utilization samples are missing")
    row_count = summary.get("row_count")
    _require(
        isinstance(row_count, int)
        and not isinstance(row_count, bool)
        and row_count >= 2,
        "CPU interval-delta evidence requires at least two rows",
    )
    for field in UTILIZATION_VALUE_FIELDS:
        values = samples.get(field)
        _require(
            isinstance(values, list) and len(values) == row_count,
            f"CPU {field} samples are missing",
        )
        expected = summarize_samples(values)
        _require(
            summary.get(field) == expected,
            f"CPU {field} summary does not match raw samples",
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
        "CPU interval-delta sample definitions are invalid",
    )
    expected_definition_counts = {
        FIRST_CPU_SAMPLE_DEFINITION: 1,
        INTERVAL_CPU_SAMPLE_DEFINITION: row_count - 1,
    }
    _require(
        summary.get("cpu_sample_definitions") == expected_definition_counts,
        "CPU interval-delta sample definition counts are invalid",
    )
    deadlines = samples.get("sample_deadline_elapsed_ms")
    lateness = samples.get("sample_lateness_ms")
    _require(
        isinstance(deadlines, list)
        and isinstance(lateness, list)
        and len(deadlines) == row_count
        and len(lateness) == row_count
        and deadlines[0] == float(EXACT_INTERVAL_MS)
        and all(
            _is_finite_number(value)
            and value >= EXACT_INTERVAL_MS
            and math.isclose(
                value % EXACT_INTERVAL_MS,
                0.0,
                abs_tol=1e-6,
            )
            for value in deadlines
        )
        and all(
            math.isclose(
                later - earlier,
                float(EXACT_INTERVAL_MS),
                abs_tol=1e-6,
            )
            for earlier, later in zip(deadlines, deadlines[1:])
        )
        and all(
            _is_finite_number(value)
            and 0 <= value < EXACT_INTERVAL_MS
            for value in lateness
        )
        and summary.get("sample_deadline_elapsed_ms")
        == summarize_samples(deadlines)
        and summary.get("sample_lateness_ms") == summarize_samples(lateness),
        "sampling deadlines are not contiguous 100 ms cadence evidence",
    )
    workload_window = run.get("workload_sampling_window_seconds")
    expected_count = expected_cpu_sample_count(
        workload_window, run.get("sampling_interval_ms")
    )
    _require(
        row_count >= expected_count,
        "CPU sample coverage is below the measured workload window requirement",
    )


def _validate_gpu_summary_and_samples(run: dict) -> None:
    summary = run.get("gpu_summary")
    samples = run.get("gpu_samples")
    _require(isinstance(summary, dict), "GPU utilization summary is missing")
    _require(isinstance(samples, dict), "GPU utilization samples are missing")
    row_count = summary.get("row_count")
    _require(row_count == 1, "GPU evidence must contain one observation row")
    for field in GPU_VALUE_FIELDS:
        values = samples.get(field)
        _require(
            isinstance(values, list) and len(values) == 1,
            f"GPU {field} samples are missing",
        )
        _require(
            summary.get(field) == summarize_samples(values),
            f"GPU {field} summary does not match raw samples",
        )
    for field in ("gpu_engine_query", "gpu_memory_query"):
        status = summary.get(field)
        _require(
            isinstance(status, dict)
            and status.get("all_succeeded") is True
            and status.get("success_count") == 1
            and status.get("failure_count") == 0,
            "GPU counter query evidence is missing or failed",
        )
    _require(
        samples.get("gpu_engine_query_ok") == [True]
        and samples.get("gpu_memory_query_ok") == [True],
        "GPU counter query status samples are invalid",
    )
    gate = run.get("sampler_start_gate")
    workload_resumed = (
        _parse_utc_timestamp(gate.get("workload_resumed_utc"))
        if isinstance(gate, dict)
        else None
    )
    run_ended = _parse_utc_timestamp(run.get("ended_utc"))
    for prefix in ("gpu_engine", "gpu_memory"):
        started_values = samples.get(f"{prefix}_query_started_utc")
        completed_values = samples.get(f"{prefix}_query_completed_utc")
        started = (
            _parse_utc_timestamp(started_values[0])
            if isinstance(started_values, list) and len(started_values) == 1
            else None
        )
        completed = (
            _parse_utc_timestamp(completed_values[0])
            if isinstance(completed_values, list) and len(completed_values) == 1
            else None
        )
        _require(
            workload_resumed is not None
            and run_ended is not None
            and started is not None
            and completed is not None
            and workload_resumed <= started <= completed <= run_ended,
            "GPU counter query timing evidence is invalid",
        )


def _validate_run_for_reconciliation(
    run: dict, derived_commit: str, executable_sha256: str
) -> None:
    _require(isinstance(run, dict), "run evidence is missing")
    _require(run.get("valid") is True, "nested run is not marked valid")
    _require(run.get("schema") == RUN_SCHEMA, "run schema is invalid")
    output_directory = run.get("output_directory")
    _require(
        isinstance(output_directory, str)
        and bool(output_directory)
        and Path(output_directory).is_absolute(),
        "run output directory is invalid",
    )
    _require(
        run.get("run_id") == Path(output_directory).name,
        "run identifier does not match its output directory",
    )
    _require(
        run.get("artifacts") == EXPECTED_RUN_ARTIFACTS,
        "run artifact manifest is not exact",
    )
    command = run.get("command")
    expected_gtest_output = (
        f"--gtest_output=json:{Path(output_directory) / 'gtest.json'}"
        if isinstance(output_directory, str) and output_directory
        else None
    )
    _require(
        isinstance(command, list)
        and len(command) == 3
        and isinstance(command[0], str)
        and bool(command[0])
        and Path(command[0]).is_absolute()
        and command[1] == EXPECTED_GTEST_FILTER
        and command[2] == expected_gtest_output,
        "run did not use the exact production GTest command",
    )
    derived_repo = run.get("derived_repo")
    provenance = run.get("build_provenance")
    build_directory = (
        provenance.get("build_directory")
        if isinstance(provenance, dict)
        else None
    )
    cmake_home = (
        provenance.get("cmake_home_directory")
        if isinstance(provenance, dict)
        else None
    )
    cmake_cache = (
        provenance.get("cmake_cache") if isinstance(provenance, dict) else None
    )
    gtest = run.get("gtest")
    source_file = gtest.get("source_file") if isinstance(gtest, dict) else None
    _require(
        isinstance(derived_repo, str)
        and bool(derived_repo)
        and isinstance(provenance, dict)
        and isinstance(build_directory, str)
        and isinstance(cmake_home, str)
        and isinstance(cmake_cache, str)
        and re.fullmatch(
            r"[0-9a-f]{64}", provenance.get("cmake_cache_sha256", "")
        )
        is not None
        and isinstance(provenance.get("cmake_generator"), str)
        and bool(provenance["cmake_generator"])
        and provenance.get("executable_within_build_directory") is True
        and _same_path(cmake_home, derived_repo)
        and _same_path(cmake_cache, Path(build_directory) / "CMakeCache.txt")
        and _path_is_within(command[0], build_directory)
        and isinstance(source_file, str)
        and _path_is_within(source_file, derived_repo)
        and Path(source_file).name == "turboquant_stateful_graph.cpp",
        "executable provenance is not bound to the derived checkout",
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
    _require(
        identity["identity_sha256"] == environment_identity_sha256(identity),
        "run environment identity digest does not match its content",
    )
    runtime_dll_sha256 = identity["runtime_openvino_dll_sha256"]
    _require(
        run.get("runtime_openvino_dll_sha256_before") == runtime_dll_sha256
        and run.get("runtime_openvino_dll_sha256_after") == runtime_dll_sha256,
        "runtime DLL hash drift",
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
        and governance.get("workload_affinity_mask")
        == EXACT_WORKLOAD_AFFINITY_MASK
        and governance.get("workload_affinity_set_before_resume") is True
        and governance.get("sampler_created_suspended") is True
        and governance.get("sampler_assigned_before_resume") is True,
        "launch governance/affinity did not prove controls before resume",
    )
    _require(
        governance.get("workload_cpu_rate_hard_cap_percent")
        == EXACT_WORKLOAD_CPU_RATE_HARD_CAP_PERCENT
        and governance.get("workload_cpu_rate_control_set_before_resume")
        is True,
        "controlled CPU rate hard cap was not set before resume",
    )
    gate = run.get("sampler_start_gate")
    ready_observed = (
        _parse_utc_timestamp(gate.get("sampler_ready_observed_utc"))
        if isinstance(gate, dict)
        else None
    )
    workload_resumed = (
        _parse_utc_timestamp(gate.get("workload_resumed_utc"))
        if isinstance(gate, dict)
        else None
    )
    sampling_start_released = (
        _parse_utc_timestamp(gate.get("sampling_start_released_utc"))
        if isinstance(gate, dict)
        else None
    )
    ready_elapsed = (
        gate.get("sampler_ready_observed_elapsed_seconds")
        if isinstance(gate, dict)
        else None
    )
    resumed_elapsed = (
        gate.get("workload_resumed_elapsed_seconds")
        if isinstance(gate, dict)
        else None
    )
    released_elapsed = (
        gate.get("sampling_start_released_elapsed_seconds")
        if isinstance(gate, dict)
        else None
    )
    _require(
        isinstance(gate, dict)
        and gate.get("ready_before_workload_resume") is True
        and gate.get("released_after_workload_resume") is True
        and ready_observed is not None
        and workload_resumed is not None
        and sampling_start_released is not None
        and ready_observed <= workload_resumed <= sampling_start_released,
        "sampling start gate did not prove readable UTC ordering",
    )
    _require(
        _is_finite_number(ready_elapsed)
        and _is_finite_number(resumed_elapsed)
        and _is_finite_number(released_elapsed)
        and ready_elapsed >= 0
        and ready_elapsed <= resumed_elapsed < released_elapsed,
        "sampling start gate did not prove ready, resume, and release ordering",
    )
    _require(
        run.get("sampling_interval_ms") == EXACT_INTERVAL_MS,
        "sampling interval is not exactly 100 ms",
    )
    _require(
        run.get("timeout_seconds") == EXACT_TIMEOUT_SECONDS,
        "run timeout is not exactly 300 seconds",
    )
    _require(
        run.get("minimum_available_ram_mb")
        == EXACT_MINIMUM_AVAILABLE_RAM_MB,
        "run RAM floor is not exactly 2,048 MiB",
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
    _require(
        ram["minimum"]
        >= int(EXACT_MINIMUM_AVAILABLE_RAM_MB * 1024 * 1024),
        "available RAM minimum fell below the 2,048 MiB floor",
    )
    _validate_summary_and_samples(run)
    _validate_gpu_summary_and_samples(run)
    marker = run.get("marker")
    _validate_lifetime_marker(marker, expected_nonce=nonce)
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
    _require(
        runs[0]["command"][0] == runs[1]["command"][0],
        "fresh processes used different production executables",
    )
    _require(
        runs[0]["derived_repo"] == runs[1]["derived_repo"]
        and runs[0]["build_provenance"] == runs[1]["build_provenance"],
        "build provenance differs across fresh processes",
    )
    runtime_dll_sha256 = runs[0]["environment_identity"][
        "runtime_openvino_dll_sha256"
    ]
    _require(
        runs[1]["environment_identity"]["runtime_openvino_dll_sha256"]
        == runtime_dll_sha256,
        "runtime DLL identity differs across fresh processes",
    )
    _require(
        runs[0]["environment_identity"] == runs[1]["environment_identity"],
        "environment identities differ across fresh processes",
    )
    runtime_dll_observations = {
        "before_run_1": runs[0]["runtime_openvino_dll_sha256_before"],
        "before_run_2": runs[0]["runtime_openvino_dll_sha256_after"],
        "after_run_2": runs[1]["runtime_openvino_dll_sha256_after"],
    }
    _require(
        set(runtime_dll_observations.values()) == {runtime_dll_sha256}
        and runs[1]["runtime_openvino_dll_sha256_before"]
        == runtime_dll_observations["before_run_2"],
        "runtime DLL hash drift across observation points",
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
    for field in (*UTILIZATION_VALUE_FIELDS, *UTILIZATION_TIMING_FIELDS):
        values = [
            value
            for run in runs
            for value in run["utilization_samples"][field]
        ]
        combined[field] = summarize_samples(values)
    for field in GPU_VALUE_FIELDS:
        values = [
            value
            for run in runs
            for value in run["gpu_samples"][field]
        ]
        combined[field] = summarize_samples(values)
    combined["gpu_engine_query"] = {
        "success_count": sum(
            run["gpu_summary"]["gpu_engine_query"]["success_count"]
            for run in runs
        ),
        "failure_count": 0,
        "all_succeeded": True,
    }
    combined["gpu_memory_query"] = {
        "success_count": sum(
            run["gpu_summary"]["gpu_memory_query"]["success_count"]
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
        "derived_repo": runs[0]["derived_repo"],
        "build_provenance": runs[0]["build_provenance"],
        "executable_sha256": executable_sha256,
        "runtime_openvino_dll_sha256": runtime_dll_sha256,
        "runtime_openvino_dll_sha256_observations": runtime_dll_observations,
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
            "runtime_dll_hashes_match": True,
            "environment_identities_match": True,
            "exact_production_commands": True,
            "build_bound_to_derived_checkout": True,
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


def preserve_superseded_canonical(
    output: Path, raw_root: Path
) -> dict[str, str] | None:
    """Preserve an existing canonical artifact before a later publication."""
    output = Path(output)
    if not output.is_file():
        return None
    raw_root = Path(raw_root)
    raw_root.mkdir(parents=True, exist_ok=True)
    content = output.read_bytes()
    digest = hashlib.sha256(content).hexdigest()
    target = raw_root / f"superseded-{output.stem}-{digest[:16]}.json"
    if target.exists():
        if _sha256_file(target) != digest:
            raise ValueError("superseded canonical path contains different bytes")
    else:
        temporary: Path | None = None
        try:
            with tempfile.NamedTemporaryFile(
                mode="wb",
                dir=raw_root,
                prefix=".sup-",
                suffix=".tmp",
                delete=False,
            ) as handle:
                temporary = Path(handle.name)
                handle.write(content)
                handle.flush()
                os.fsync(handle.fileno())
            os.replace(temporary, target)
            temporary = None
        finally:
            if temporary is not None:
                temporary.unlink(missing_ok=True)
    return {"path": str(target), "sha256": digest}


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


def _parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--derived-repo", type=Path, required=True)
    parser.add_argument("--build-dir", type=Path, required=True)
    parser.add_argument("--executable", type=Path, required=True)
    parser.add_argument("--runtime-library-dir", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    commit_source = parser.add_mutually_exclusive_group(required=True)
    commit_source.add_argument("--expected-derived-commit")
    commit_source.add_argument("--source-identity", type=Path)
    parser.add_argument(
        "--timeout-seconds", type=float, default=EXACT_TIMEOUT_SECONDS
    )
    parser.add_argument("--interval-ms", type=int, default=EXACT_INTERVAL_MS)
    parser.add_argument(
        "--minimum-available-ram-mb",
        type=float,
        default=EXACT_MINIMUM_AVAILABLE_RAM_MB,
    )
    return parser.parse_args(argv)


def main() -> int:
    args = _parse_args()
    expected_derived_commit = resolve_expected_derived_commit(
        args.expected_derived_commit,
        args.source_identity,
    )
    output = args.output.resolve()
    raw_root = output.parent / output.stem
    raw_root.mkdir(parents=True, exist_ok=True)
    superseded_canonical = preserve_superseded_canonical(output, raw_root)
    attempt_nonce = secrets.token_hex(4)
    attempt_dir = attempt_directory_for(
        output, datetime.now(timezone.utc), attempt_nonce
    )
    attempt_dir.mkdir(parents=True, exist_ok=False)
    summary_path = attempt_dir / "attempt-summary.json"
    attempt_summary: dict[str, Any] = {
        "schema": "openvino-turboquant-reference-capability-attempt/v2",
        "attempt_directory": str(attempt_dir),
        "canonical_output": str(output),
        "started_utc": _utc_now(),
        "status": "failed",
        "error": None,
        "runs": [],
        "derived_repo": str(args.derived_repo.resolve()),
        "build_directory": str(args.build_dir.resolve()),
        "executable": str(absolute_invocation_path(args.executable)),
        "runtime_library_dir": str(args.runtime_library_dir),
        "expected_derived_commit": expected_derived_commit,
        "source_identity": (
            None
            if args.source_identity is None
            else {
                "path": str(args.source_identity.resolve()),
                "sha256": _sha256_file(args.source_identity.resolve()),
            }
        ),
        "controller_configuration": {
            "timeout_seconds": args.timeout_seconds,
            "sampling_interval_ms": args.interval_ms,
            "minimum_available_ram_mb": args.minimum_available_ram_mb,
            "workload_affinity_mask": EXACT_WORKLOAD_AFFINITY_MASK,
            "workload_cpu_rate_hard_cap_percent": (
                EXACT_WORKLOAD_CPU_RATE_HARD_CAP_PERCENT
            ),
        },
        "superseded_canonical": superseded_canonical,
        "derived_observations": {},
        "build_provenance_observations": {},
        "build_provenance_snapshot": None,
        "executable_sha256_observations": {},
        "runtime_openvino_dll_sha256_observations": {},
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
        build_dir = args.build_dir.resolve()
        executable = absolute_invocation_path(args.executable)
        if not derived_repo.is_dir():
            raise ValueError("derived checkout does not exist")
        if not executable.is_file():
            raise ValueError("production-linked executable does not exist")
        runtime_library_dir = validate_runtime_library_dir(
            args.runtime_library_dir
        )
        runtime_openvino_dll = runtime_library_dir / "openvino.dll"
        build_provenance_before_run_1 = validate_build_provenance(
            derived_repo, build_dir, executable
        )
        attempt_summary["build_provenance_observations"][
            "before_run_1"
        ] = build_provenance_before_run_1
        cache_snapshot = attempt_dir / "CMakeCache.txt"
        cache_snapshot.write_bytes(
            Path(build_provenance_before_run_1["cmake_cache"]).read_bytes()
        )
        cache_snapshot_sha256 = _sha256_file(cache_snapshot)
        if (
            cache_snapshot_sha256
            != build_provenance_before_run_1["cmake_cache_sha256"]
        ):
            raise ValueError("CMakeCache snapshot hash does not match provenance")
        attempt_summary["build_provenance_snapshot"] = {
            "path": str(cache_snapshot),
            "sha256": cache_snapshot_sha256,
        }

        commit_before_run_1, status_before_run_1 = _git_identity(derived_repo)
        attempt_summary["derived_observations"]["before_run_1"] = {
            "commit": commit_before_run_1,
            "status_porcelain": status_before_run_1,
        }
        if commit_before_run_1 != expected_derived_commit:
            raise ValueError(
                f"derived commit is {commit_before_run_1}, expected "
                f"{expected_derived_commit}"
            )
        if status_before_run_1:
            raise ValueError("derived checkout is not clean before run 1")

        sha_before_run_1 = _sha256_file(executable)
        runtime_sha_before_run_1 = _sha256_file(runtime_openvino_dll)
        attempt_summary["executable_sha256_observations"][
            "before_run_1"
        ] = sha_before_run_1
        attempt_summary["runtime_openvino_dll_sha256_observations"][
            "before_run_1"
        ] = runtime_sha_before_run_1

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
        run_1["derived_repo"] = str(derived_repo)
        run_1["build_provenance"] = build_provenance_before_run_1
        runs.append(run_1)

        commit_before_run_2, status_before_run_2 = _git_identity(derived_repo)
        build_provenance_before_run_2 = validate_build_provenance(
            derived_repo, build_dir, executable
        )
        sha_before_run_2 = _sha256_file(executable)
        runtime_sha_before_run_2 = _sha256_file(runtime_openvino_dll)
        attempt_summary["derived_observations"]["before_run_2"] = {
            "commit": commit_before_run_2,
            "status_porcelain": status_before_run_2,
        }
        attempt_summary["build_provenance_observations"][
            "before_run_2"
        ] = build_provenance_before_run_2
        attempt_summary["executable_sha256_observations"][
            "before_run_2"
        ] = sha_before_run_2
        attempt_summary["runtime_openvino_dll_sha256_observations"][
            "before_run_2"
        ] = runtime_sha_before_run_2
        run_1.update(
            {
                "derived_commit": commit_before_run_1,
                "derived_checkout_clean_before": status_before_run_1 == "",
                "derived_checkout_clean_after": status_before_run_2 == "",
                "executable_sha256_before": sha_before_run_1,
                "executable_sha256_after": sha_before_run_2,
                "runtime_openvino_dll_sha256_before": runtime_sha_before_run_1,
                "runtime_openvino_dll_sha256_after": runtime_sha_before_run_2,
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
        run_2["derived_repo"] = str(derived_repo)
        run_2["build_provenance"] = build_provenance_before_run_2
        runs.append(run_2)

        commit_after_run_2, status_after_run_2 = _git_identity(derived_repo)
        build_provenance_after_run_2 = validate_build_provenance(
            derived_repo, build_dir, executable
        )
        sha_after_run_2 = _sha256_file(executable)
        runtime_sha_after_run_2 = _sha256_file(runtime_openvino_dll)
        attempt_summary["derived_observations"]["after_run_2"] = {
            "commit": commit_after_run_2,
            "status_porcelain": status_after_run_2,
        }
        attempt_summary["build_provenance_observations"][
            "after_run_2"
        ] = build_provenance_after_run_2
        attempt_summary["executable_sha256_observations"][
            "after_run_2"
        ] = sha_after_run_2
        attempt_summary["runtime_openvino_dll_sha256_observations"][
            "after_run_2"
        ] = runtime_sha_after_run_2
        run_2.update(
            {
                "derived_commit": commit_after_run_2,
                "derived_checkout_clean_before": status_before_run_2 == "",
                "derived_checkout_clean_after": status_after_run_2 == "",
                "executable_sha256_before": sha_before_run_2,
                "executable_sha256_after": sha_after_run_2,
                "runtime_openvino_dll_sha256_before": runtime_sha_before_run_2,
                "runtime_openvino_dll_sha256_after": runtime_sha_after_run_2,
            }
        )
        atomic_write_json(run_2_dir / "run.json", run_2)

        if not (
            commit_before_run_1
            == commit_before_run_2
            == commit_after_run_2
            == expected_derived_commit
        ):
            raise ValueError("derived commit drifted across the two runs")
        if status_before_run_2 or status_after_run_2:
            raise ValueError("derived checkout did not remain exactly clean")
        if not (
            sha_before_run_1 == sha_before_run_2 == sha_after_run_2
        ):
            raise ValueError("executable SHA-256 drifted across the two runs")
        if not (
            build_provenance_before_run_1
            == build_provenance_before_run_2
            == build_provenance_after_run_2
        ):
            raise ValueError("build provenance drifted across the two runs")
        if not (
            runtime_sha_before_run_1
            == runtime_sha_before_run_2
            == runtime_sha_after_run_2
        ):
            raise ValueError("OpenVINO runtime DLL SHA-256 drifted across the two runs")

        capability = reconcile_runs(
            runs, commit_before_run_1, sha_before_run_1
        )
        capability["attempt_directory"] = str(attempt_dir)
        capability["supersedes"] = superseded_canonical
        capability["runtime_library_dir"] = str(runtime_library_dir)
        if (
            capability["runtime_openvino_dll_sha256_observations"]
            != attempt_summary["runtime_openvino_dll_sha256_observations"]
        ):
            raise ValueError("runtime DLL observations disagree after reconciliation")
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
        capability["build_provenance_observations"] = attempt_summary[
            "build_provenance_observations"
        ]
        capability["build_provenance_snapshot"] = attempt_summary[
            "build_provenance_snapshot"
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
