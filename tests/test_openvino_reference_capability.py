from __future__ import annotations

import copy
import csv
import json
import os
import sys
import textwrap
from datetime import datetime, timezone
from pathlib import Path

import pytest

from scripts.testing import run_openvino_reference_capability as capability
from scripts.testing.run_openvino_reference_capability import (
    MARKER_PREFIX,
    atomic_write_json,
    parse_lifetime_marker,
    publish_reconciled_capability,
    reconcile_runs,
    run_one,
    summarize_samples,
)


EXPECTED_EXECUTABLE_SHA256 = "a" * 64
EXPECTED_DERIVED_COMMIT = "b" * 40
EXPECTED_HASHES = ["0123456789abcdef", "0123456789abcdef"]
EXPECTED_TEST_NAME = (
    "TurboQuantStatefulGraph.PersistsOnlyCompressedStateForOneHundredSteps"
)


def expected_states(step: int) -> list[dict]:
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


def valid_marker(nonce: str) -> dict:
    allocations = {
        1: (28, 32, 32, 256, 256),
        2: (56, 64, 64, 512, 512),
        50: (1400, 1600, 1600, 12800, 12800),
        100: (2800, 3200, 3200, 25600, 25600),
    }
    snapshots = []
    for step, values in allocations.items():
        payload, norm, metadata, full_precision, decoded = values
        snapshots.append(
            {
                "step": step,
                "states": expected_states(step),
                "payload_bytes": payload,
                "norm_bytes": norm,
                "metadata_bytes": metadata,
                "full_precision_equivalent_bytes": full_precision,
                "decoded_scratch_bytes": decoded,
            }
        )
    return {
        "schema": "openvino-turboquant-reference-capability/v1",
        "device": "CPU",
        "runtime_layer_type": "Reference",
        "reference_operation_count": 4,
        "matched_state_count": 4,
        "steps": 100,
        "hash_algorithm": "FNV-1a-64-labelled-step-result-raw-bytes",
        "output_hashes": list(EXPECTED_HASHES),
        "repeat_hash_matches": True,
        "snapshots": snapshots,
        "no_full_precision_selected_state": True,
        "run_nonce": nonce,
    }


def marker_line(marker: dict) -> str:
    return MARKER_PREFIX + json.dumps(marker, separators=(",", ":"))


def summary(values: list[float]) -> dict:
    return summarize_samples(values)


def valid_run(nonce: str, pid: int) -> dict:
    marker = valid_marker(nonce)
    return {
        "schema": "openvino-turboquant-reference-capability-run/v1",
        "run_id": f"run-{pid}",
        "command": ["test.exe", "--gtest_filter=" + EXPECTED_TEST_NAME],
        "environment_identity": {
            "identity_sha256": "c" * 64,
            "computer_name": "fixture",
            "runtime_library_dir": "C:\\fixture\\openvino\\libs",
            "runtime_openvino_dll_sha256": "e" * 64,
        },
        "run_nonce": nonce,
        "root_pid": pid,
        "observed_pids": [pid],
        "child_workload_observed": False,
        "launch_governance": {
            "workload_created_suspended": True,
            "workload_assigned_before_resume": True,
            "sampler_created_suspended": True,
            "sampler_assigned_before_resume": True,
        },
        "started_utc": "2026-07-27T00:00:00.000000Z",
        "ended_utc": "2026-07-27T00:00:01.000000Z",
        "elapsed_seconds": 1.0,
        "sampling_interval_ms": 100,
        "timeout_seconds": 300.0,
        "minimum_available_ram_mb": 2048.0,
        "memory_sample_count": 2,
        "peak_working_set_bytes": 4096,
        "peak_private_bytes": 8192,
        "available_ram_bytes": {
            "before": 8 * 1024**3,
            "minimum": 7 * 1024**3,
            "after": 8 * 1024**3,
        },
        "utilization_summary": {
            "row_count": 2,
            "cpu_percent": summary([10.0, 20.0]),
            "gpu_percent": summary([0.0, 1.0]),
            "gpu_engine_count": summary([0.0, 1.0]),
            "gpu_dedicated_mb": summary([0.0, 2.0]),
            "gpu_shared_mb": summary([0.0, 3.0]),
            "gpu_engine_query": {
                "success_count": 2,
                "failure_count": 0,
                "all_succeeded": True,
            },
            "gpu_memory_query": {
                "success_count": 2,
                "failure_count": 0,
                "all_succeeded": True,
            },
            "cpu_sample_definitions": {
                "lifetime_average_since_process_start": 1,
                "interval_delta": 1,
            },
        },
        "utilization_samples": {
            "cpu_percent": [10.0, 20.0],
            "cpu_sample_definition": [
                "lifetime_average_since_process_start",
                "interval_delta",
            ],
            "gpu_percent": [0.0, 1.0],
            "gpu_engine_count": [0.0, 1.0],
            "gpu_dedicated_mb": [0.0, 2.0],
            "gpu_shared_mb": [0.0, 3.0],
        },
        "marker": marker,
        "gtest": {
            "tests": 1,
            "passed": 1,
            "failures": 0,
            "errors": 0,
            "disabled": 0,
            "test_name": EXPECTED_TEST_NAME,
        },
        "exit_code": 0,
        "timed_out": False,
        "low_memory_stop": False,
        "emergency_stop": False,
        "cleanup_duration_seconds": 0.05,
        "sampler_exit_code": 0,
        "job_object": {
            "setup_ok": True,
            "query_ok": True,
            "queried_active_process_count_after_cleanup": 0,
            "survivor_pids_after_cleanup": [],
        },
        "sampler_job_object": {
            "setup_ok": True,
            "query_ok": True,
            "queried_active_process_count_after_cleanup": 0,
            "survivor_pids_after_cleanup": [],
        },
        "executable_sha256_before": EXPECTED_EXECUTABLE_SHA256,
        "executable_sha256_after": EXPECTED_EXECUTABLE_SHA256,
        "derived_commit": EXPECTED_DERIVED_COMMIT,
        "derived_checkout_clean_before": True,
        "derived_checkout_clean_after": True,
        "validation_errors": [],
        "valid": True,
        "artifacts": {
            "stdout": "stdout.txt",
            "stderr": "stderr.txt",
            "memory_samples": "memory.jsonl",
            "utilization": "utilization.csv",
            "gtest": "gtest.json",
            "run": "run.json",
        },
    }


def write_fixture(path: Path, marker: dict) -> None:
    marker_json = json.dumps(marker, separators=(",", ":"))
    path.write_text(
        textwrap.dedent(
            f"""
            import json
            import os
            import pathlib
            import sys
            import time

            marker = json.loads({marker_json!r})
            marker["run_nonce"] = os.environ["OPENVINO_TURBOQUANT_CAPABILITY_NONCE"]
            gtest_path = None
            ready_path = None
            for argument in sys.argv[1:]:
                if argument.startswith("--gtest_output=json:"):
                    gtest_path = pathlib.Path(argument.split("json:", 1)[1])
                elif argument.startswith("--ready-path="):
                    ready_path = pathlib.Path(argument.split("=", 1)[1])
            time.sleep(0.35)
            gtest = {{
                "tests": 1,
                "failures": 0,
                "disabled": 0,
                "errors": 0,
                "testsuites": [{{
                    "name": "TurboQuantStatefulGraph",
                    "tests": 1,
                    "failures": 0,
                    "disabled": 0,
                    "errors": 0,
                    "testsuite": [{{
                        "name": "PersistsOnlyCompressedStateForOneHundredSteps",
                        "classname": "TurboQuantStatefulGraph",
                        "status": "RUN",
                        "result": "COMPLETED"
                    }}]
                }}]
            }}
            gtest_path.write_text(json.dumps(gtest), encoding="utf-8")
            print({MARKER_PREFIX!r} + json.dumps(marker, separators=(",", ":")), flush=True)
            """
        ),
        encoding="utf-8",
    )


def write_runtime_fixture(path: Path) -> Path:
    path.mkdir(parents=True)
    (path / "openvino.dll").write_bytes(b"runtime-fixture")
    return path


def test_parse_lifetime_marker_accepts_exactly_one_strict_marker():
    nonce = "1" * 32
    parsed = parse_lifetime_marker(
        "ordinary output\n" + marker_line(valid_marker(nonce)) + "\n",
        expected_nonce=nonce,
    )
    assert parsed["run_nonce"] == nonce
    assert parsed["snapshots"][3]["payload_bytes"] == 2800


@pytest.mark.parametrize(
    "stdout,match",
    [
        ("ordinary output\n", "exactly one"),
        (
            marker_line(valid_marker("1" * 32))
            + "\n"
            + marker_line(valid_marker("1" * 32)),
            "exactly one",
        ),
        (MARKER_PREFIX + "{not-json}", "valid JSON"),
    ],
)
def test_parse_lifetime_marker_rejects_missing_duplicate_and_malformed(
    stdout: str, match: str
):
    with pytest.raises(ValueError, match=match):
        parse_lifetime_marker(stdout, expected_nonce="1" * 32)


@pytest.mark.parametrize(
    "mutation,match",
    [
        (lambda value: value.pop("device"), "missing field"),
        (lambda value: value.__setitem__("device", "GPU"), "device"),
        (lambda value: value.__setitem__("runtime_layer_type", "jit"), "runtime"),
        (lambda value: value.__setitem__("steps", 99), "100 steps"),
        (
            lambda value: value.__setitem__("repeat_hash_matches", False),
            "reconciliation",
        ),
        (
            lambda value: value.__setitem__(
                "no_full_precision_selected_state", False
            ),
            "full-precision",
        ),
        (lambda value: value["snapshots"].pop(), "snapshots"),
        (
            lambda value: value["snapshots"][0].__setitem__("payload_bytes", 29),
            "allocation",
        ),
        (
            lambda value: value["snapshots"][0]["states"].pop(),
            "selected states",
        ),
        (lambda value: value.__setitem__("run_nonce", "2" * 32), "nonce"),
    ],
)
def test_parse_lifetime_marker_rejects_schema_state_allocation_and_nonce_errors(
    mutation, match: str
):
    nonce = "1" * 32
    marker = valid_marker(nonce)
    mutation(marker)
    with pytest.raises(ValueError, match=match):
        parse_lifetime_marker(marker_line(marker), expected_nonce=nonce)


def test_summarize_samples_reports_exact_even_and_odd_statistics():
    assert summarize_samples([1.0, 2.0, 9.0]) == {
        "count": 3,
        "mean": 4.0,
        "median": 2.0,
        "minimum": 1.0,
        "maximum": 9.0,
        "peak": 9.0,
    }
    assert summarize_samples([1.0, 2.0, 9.0, 12.0]) == {
        "count": 4,
        "mean": 6.0,
        "median": 5.5,
        "minimum": 1.0,
        "maximum": 12.0,
        "peak": 12.0,
    }


def test_next_sampling_deadline_remains_anchored_to_the_100ms_grid():
    assert capability.next_sampling_deadline(10.0, 0.1, 10.025) == pytest.approx(
        10.1
    )
    assert capability.next_sampling_deadline(10.1, 0.1, 10.35) == pytest.approx(
        10.4
    )


@pytest.mark.parametrize("values", [[], [float("nan")], [float("inf")]])
def test_summarize_samples_rejects_absent_or_nonfinite_series(values):
    with pytest.raises(ValueError):
        summarize_samples(values)


def test_collector_exposes_independent_gpu_query_success_columns():
    script = (
        Path(__file__).parents[1]
        / "scripts"
        / "testing"
        / "collect_process_utilization.ps1"
    ).read_text(encoding="utf-8-sig")
    header = next(
        line for line in script.splitlines() if line.startswith("'timestamp_utc,")
    )
    assert "gpu_engine_query_ok" in header
    assert "gpu_memory_query_ok" in header
    assert "cpu_sample_definition" in header
    assert script.count("try {") >= 2
    assert script.count("catch {") >= 2
    assert (
        "Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine "
        "-ErrorAction Stop"
    ) in script
    assert (
        "Win32_PerfFormattedData_GPUPerformanceCounters_GPUProcessMemory "
        "-ErrorAction Stop"
    ) in script


def test_runtime_library_dir_rejects_missing_and_incomplete_directories(
    tmp_path: Path,
):
    with pytest.raises(ValueError, match="does not exist"):
        capability.validate_runtime_library_dir(tmp_path / "missing")

    incomplete = tmp_path / "incomplete"
    incomplete.mkdir()
    with pytest.raises(ValueError, match="openvino.dll"):
        capability.validate_runtime_library_dir(incomplete)


def test_valid_runtime_library_dir_is_child_only_path_prepend_and_identity(
    tmp_path: Path,
):
    runtime_dir = tmp_path / "openvino" / "libs"
    runtime_dir.mkdir(parents=True)
    (runtime_dir / "openvino.dll").write_bytes(b"runtime-fixture")
    original_parent_path = os.environ.get("PATH", "")

    validated = capability.validate_runtime_library_dir(runtime_dir)
    child_environment = capability.workload_environment("4" * 32, validated)
    identity = capability._environment_identity(validated)

    assert validated == runtime_dir.resolve()
    assert child_environment["PATH"].split(os.pathsep)[0] == str(validated)
    assert child_environment[capability.NONCE_ENVIRONMENT_VARIABLE] == "4" * 32
    assert os.environ.get("PATH", "") == original_parent_path
    assert identity["runtime_library_dir"] == str(validated)
    assert identity["runtime_openvino_dll_sha256"] == capability._sha256_file(
        validated / "openvino.dll"
    )


def test_absolute_invocation_path_preserves_supplied_drive():
    supplied = Path(
        "R:/external/official-openvino/2026-07-19/build-genai-turboquant/"
        "tests/cpp/Release/tests_continuous_batching.exe"
    )
    invocation = capability.absolute_invocation_path(supplied)
    assert invocation.drive.upper() == "R:"
    assert str(invocation).upper().startswith("R:\\")


def test_two_350ms_fresh_fixture_processes_produce_memory_and_utilization(
    tmp_path: Path,
):
    fixture = tmp_path / "fixture.py"
    write_fixture(fixture, valid_marker("0" * 32))
    runtime_dir = write_runtime_fixture(tmp_path / "runtime")

    records = []
    for index, nonce in enumerate(("1" * 32, "2" * 32), start=1):
        output_dir = tmp_path / f"run-{index}"
        command = [
            sys.executable,
            str(fixture),
            f"--gtest_output=json:{output_dir / 'gtest.json'}",
            f"--ready-path={output_dir / 'utilization.ready'}",
        ]
        record = run_one(
            command,
            output_dir,
            timeout_seconds=10.0,
            interval_ms=100,
            minimum_available_ram_mb=1.0,
            run_nonce=nonce,
            runtime_library_dir=runtime_dir,
        )
        records.append(record)
        assert record["memory_sample_count"] >= 1
        assert record["utilization_summary"]["row_count"] >= 1
        assert record["gtest"]["tests"] == 1
        assert record["gtest"]["passed"] == 1
        assert record["utilization_samples"]["cpu_sample_definition"][0] == (
            "lifetime_average_since_process_start"
        )
        assert record["job_object"][
            "queried_active_process_count_after_cleanup"
        ] == 0
        assert record["launch_governance"] == {
            "workload_created_suspended": True,
            "workload_assigned_before_resume": True,
            "sampler_created_suspended": True,
            "sampler_assigned_before_resume": True,
        }
        memory_rows = [
            json.loads(line)
            for line in (output_dir / "memory.jsonl")
            .read_text(encoding="utf-8")
            .splitlines()
        ]
        assert len(memory_rows) >= 2
        memory_deltas = [
            later["elapsed_seconds"] - earlier["elapsed_seconds"]
            for earlier, later in zip(memory_rows, memory_rows[1:])
        ]
        assert all(0.075 <= delta <= 0.2 for delta in memory_deltas)
        with (output_dir / "utilization.csv").open(
            encoding="utf-8-sig", newline=""
        ) as handle:
            rows = list(csv.DictReader(handle))
        assert rows
        assert all(row["gpu_engine_query_ok"] in {"true", "false"} for row in rows)
        assert all(row["gpu_memory_query_ok"] in {"true", "false"} for row in rows)

    assert records[0]["root_pid"] != records[1]["root_pid"]
    assert records[0]["run_nonce"] != records[1]["run_nonce"]


def test_timeout_child_fixture_has_queried_zero_job_survivors(tmp_path: Path):
    fixture = tmp_path / "timeout_fixture.py"
    fixture.write_text(
        textwrap.dedent(
            """
            import subprocess
            import sys
            import time

            time.sleep(0.1)
            subprocess.Popen([sys.executable, "-c", "import time; time.sleep(30)"])
            time.sleep(30)
            """
        ),
        encoding="utf-8",
    )
    runtime_dir = write_runtime_fixture(tmp_path / "runtime")
    record = run_one(
        [sys.executable, str(fixture)],
        tmp_path / "timeout-run",
        timeout_seconds=0.5,
        interval_ms=100,
        minimum_available_ram_mb=1.0,
        run_nonce="3" * 32,
        runtime_library_dir=runtime_dir,
    )
    assert record["timed_out"] is True
    assert record["child_workload_observed"] is True
    assert record["job_object"]["query_ok"] is True
    assert record["job_object"]["queried_active_process_count_after_cleanup"] == 0
    assert record["job_object"]["survivor_pids_after_cleanup"] == []


@pytest.mark.parametrize(
    "mutation,match",
    [
        (lambda run: run.pop("peak_working_set_bytes"), "memory"),
        (
            lambda run: run["utilization_summary"].pop("cpu_percent"),
            "CPU",
        ),
        (
            lambda run: run["utilization_summary"]["gpu_engine_query"].__setitem__(
                "all_succeeded", False
            ),
            "GPU",
        ),
        (
            lambda run: run["available_ram_bytes"].__setitem__("minimum", None),
            "available RAM",
        ),
        (
            lambda run: run["marker"]["snapshots"][0]["states"].pop(),
            "selected states",
        ),
        (
            lambda run: run["marker"]["snapshots"][0].__setitem__(
                "payload_bytes", 29
            ),
            "allocation",
        ),
        (
            lambda run: run["marker"].__setitem__(
                "output_hashes", ["not-a-hash", "not-a-hash"]
            ),
            "hash",
        ),
        (lambda run: run.__setitem__("timed_out", True), "timeout"),
        (lambda run: run.__setitem__("exit_code", 1), "exit"),
        (
            lambda run: run["job_object"].__setitem__(
                "queried_active_process_count_after_cleanup", 1
            ),
            "cleanup",
        ),
        (
            lambda run: run["launch_governance"].__setitem__(
                "workload_assigned_before_resume", False
            ),
            "launch",
        ),
    ],
)
def test_reconcile_rejects_missing_or_invalid_measurement_evidence(
    mutation, match: str
):
    runs = [valid_run("1" * 32, 101), valid_run("2" * 32, 202)]
    mutation(runs[0])
    with pytest.raises(ValueError, match=match):
        reconcile_runs(runs, EXPECTED_DERIVED_COMMIT, EXPECTED_EXECUTABLE_SHA256)


def test_reconcile_rejects_cross_process_hash_mismatch():
    runs = [valid_run("1" * 32, 101), valid_run("2" * 32, 202)]
    runs[1]["marker"]["output_hashes"] = [
        "fedcba9876543210",
        "fedcba9876543210",
    ]
    with pytest.raises(ValueError, match="fresh-process output hashes"):
        reconcile_runs(runs, EXPECTED_DERIVED_COMMIT, EXPECTED_EXECUTABLE_SHA256)


def test_reconcile_rejects_nonce_mismatch_and_executable_hash_drift():
    nonce_runs = [valid_run("1" * 32, 101), valid_run("2" * 32, 202)]
    nonce_runs[0]["marker"]["run_nonce"] = "9" * 32
    with pytest.raises(ValueError, match="nonce"):
        reconcile_runs(
            nonce_runs, EXPECTED_DERIVED_COMMIT, EXPECTED_EXECUTABLE_SHA256
        )

    hash_runs = [valid_run("1" * 32, 101), valid_run("2" * 32, 202)]
    hash_runs[1]["executable_sha256_before"] = "d" * 64
    with pytest.raises(ValueError, match="executable hash drift"):
        reconcile_runs(
            hash_runs, EXPECTED_DERIVED_COMMIT, EXPECTED_EXECUTABLE_SHA256
        )


def test_reconcile_retains_per_run_and_combined_cpu_gpu_statistics():
    runs = [valid_run("1" * 32, 101), valid_run("2" * 32, 202)]
    result = reconcile_runs(
        runs, EXPECTED_DERIVED_COMMIT, EXPECTED_EXECUTABLE_SHA256
    )
    assert result["runs"][0]["utilization_summary"]["cpu_percent"]["count"] == 2
    assert result["runs"][1]["utilization_summary"]["gpu_percent"]["peak"] == 1.0
    assert result["combined_utilization"]["cpu_percent"] == summary(
        [10.0, 20.0, 10.0, 20.0]
    )
    assert result["combined_utilization"]["gpu_percent"] == summary(
        [0.0, 1.0, 0.0, 1.0]
    )
    assert result["reconciliation"]["queried_zero_survivors"] is True


def test_atomic_write_uses_same_directory_flush_fsync_and_replace(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    output = tmp_path / "capability.json"
    calls: list[tuple] = []
    real_fsync = os.fsync
    real_replace = os.replace

    def fsync_spy(fd: int) -> None:
        calls.append(("fsync", fd))
        real_fsync(fd)

    def replace_spy(source, destination) -> None:
        calls.append(("replace", Path(source), Path(destination)))
        assert Path(source).parent == output.parent
        assert Path(destination) == output
        real_replace(source, destination)

    monkeypatch.setattr(capability.os, "fsync", fsync_spy)
    monkeypatch.setattr(capability.os, "replace", replace_spy)
    atomic_write_json(output, {"status": "published"})

    assert json.loads(output.read_text(encoding="utf-8")) == {
        "status": "published"
    }
    assert [call[0] for call in calls] == ["fsync", "replace"]


def test_attempt_path_stays_under_legacy_windows_limits():
    root = Path(__file__).parents[1]
    output = (
        root
        / "experiments"
        / "raw-results"
        / "openvino-turboquant"
        / "2026-07-27"
        / "conformance"
        / "reference-capability.json"
    )
    attempt = capability.attempt_directory_for(
        output,
        datetime(2026, 7, 27, 21, 47, 45, 137784, tzinfo=timezone.utc),
        "0fbd692a",
    )
    assert attempt.name == "attempt-20260727T214745Z-0fbd692a"
    assert len(str(attempt / "run-1")) < 248
    assert len(str(attempt / "run-1" / "sampler.stderr.txt")) < 260


def test_atomic_write_uses_short_temp_name_near_legacy_limit(tmp_path: Path):
    directory = tmp_path
    while len(str(directory)) < 238:
        remaining = 238 - len(str(directory)) - 1
        component = "d" * min(50, remaining)
        directory = directory / component
        directory.mkdir()
    output = directory / "evidence.json"
    assert len(str(output)) < 260
    atomic_write_json(output, {"status": "path-safe"})
    assert json.loads(output.read_text(encoding="utf-8")) == {
        "status": "path-safe"
    }


def test_failed_reconciliation_preserves_existing_canonical_json(tmp_path: Path):
    output = tmp_path / "reference-capability.json"
    output.write_text('{"status":"previous"}\n', encoding="utf-8")
    runs = [valid_run("1" * 32, 101), valid_run("2" * 32, 202)]
    runs[1]["timed_out"] = True

    with pytest.raises(ValueError, match="timeout"):
        publish_reconciled_capability(
            output,
            runs,
            EXPECTED_DERIVED_COMMIT,
            EXPECTED_EXECUTABLE_SHA256,
        )

    assert output.read_text(encoding="utf-8") == '{"status":"previous"}\n'
