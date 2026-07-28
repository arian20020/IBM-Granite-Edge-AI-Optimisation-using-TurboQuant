from __future__ import annotations

import importlib
import inspect
import json
import os
import subprocess
import sys
import threading
import time
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
SCRIPT_DIR = ROOT / "scripts" / "testing"
GUARD_TEST_FILE = Path(__file__).resolve()
GIB = 1024 * 1024 * 1024


def _guarded_build():
    return importlib.import_module(
        "scripts.testing.official_openvino.guarded_build"
    )


def _owned_guard():
    return importlib.import_module(
        "scripts.testing.official_openvino.owned_process_guard"
    )


def _limits(
    guard,
    *,
    minimum_available_ram_bytes: int = 0,
    maximum_runtime_seconds: float = 5.0,
):
    return guard.GuardLimits(
        minimum_available_ram_bytes=minimum_available_ram_bytes,
        poll_interval_seconds=0.02,
        cleanup_timeout_seconds=2.0,
        maximum_runtime_seconds=maximum_runtime_seconds,
    )


def _run(
    tmp_path: Path,
    command: list[str],
    *,
    expected_exit: str = "zero",
    limits=None,
):
    guard = _guarded_build()
    return guard.run_guarded_command(
        command,
        cwd=ROOT,
        log_path=tmp_path / "child.log",
        evidence_path=tmp_path / "evidence.json",
        expected_exit=expected_exit,
        limits=limits or _limits(guard),
    )


def _assert_zero_survivors(record: dict[str, object]) -> None:
    job = record["job_object"]
    assert job["setup_ok"] is True
    assert job["query_ok"] is True
    assert job["queried_active_process_count_after_cleanup"] == 0
    assert job["survivor_pids_after_cleanup"] == []


def _assert_memory_evidence(record: dict[str, object]) -> None:
    assert record["memory_sample_count"] >= 1
    assert record["peak_working_set_bytes"] > 0
    assert record["peak_private_bytes"] > 0
    failed_pids = record["memory_query_failed_pids"]
    assert failed_pids == sorted(set(failed_pids))
    configured = record["configured_minimum_available_ram_bytes"]
    observed = record["observed_available_ram_bytes"]
    assert observed["before"] >= configured
    assert observed["minimum"] >= configured
    assert observed["after"] >= configured


def _ps_literal(value: str) -> str:
    return "'" + value.replace("'", "''") + "'"


def test_zero_exit_atomically_replaces_log_and_evidence(tmp_path):
    guard = _guarded_build()
    log_path = tmp_path / "child.log"
    evidence_path = tmp_path / "evidence.json"
    ready_path = tmp_path / "ready"
    release_path = tmp_path / "release"
    log_path.write_text("old-log\n", encoding="utf-8")
    evidence_path.write_text("old-evidence\n", encoding="utf-8")
    child = (
        "import pathlib,sys,time;"
        "ready=pathlib.Path(sys.argv[1]);"
        "release=pathlib.Path(sys.argv[2]);"
        "ready.write_text('ready',encoding='ascii');"
        "\nwhile not release.exists(): time.sleep(0.01)\n"
        "print('replacement-stdout',flush=True);"
        "print('replacement-stderr',file=sys.stderr,flush=True)"
    )
    result: dict[str, object] = {}
    failure: list[BaseException] = []

    def invoke() -> None:
        try:
            result.update(
                guard.run_guarded_command(
                    [sys.executable, "-c", child, str(ready_path), str(release_path)],
                    cwd=ROOT,
                    log_path=log_path,
                    evidence_path=evidence_path,
                    expected_exit="zero",
                    limits=_limits(guard),
                )
            )
        except BaseException as error:
            failure.append(error)

    worker = threading.Thread(target=invoke)
    worker.start()
    deadline = time.monotonic() + 5.0
    while not ready_path.exists() and time.monotonic() < deadline:
        time.sleep(0.01)
    assert ready_path.exists()
    assert log_path.read_text(encoding="utf-8") == "old-log\n"
    assert evidence_path.read_text(encoding="utf-8") == "old-evidence\n"
    release_path.write_text("release\n", encoding="ascii")
    worker.join(timeout=10.0)
    assert not worker.is_alive()
    assert failure == []
    assert result["valid"] is True
    combined_log = log_path.read_text(encoding="utf-8")
    assert "replacement-stdout" in combined_log
    assert "replacement-stderr" in combined_log
    persisted = json.loads(evidence_path.read_text(encoding="utf-8"))
    assert persisted == result
    assert list(tmp_path.glob(".child.log.tmp-*")) == []
    assert list(tmp_path.glob(".evidence.json.tmp-*")) == []
    _assert_memory_evidence(result)
    _assert_zero_survivors(result)


def test_expected_nonzero_exit_is_valid(tmp_path):
    record = _run(
        tmp_path,
        [sys.executable, "-c", "raise SystemExit(7)"],
        expected_exit="nonzero",
    )
    assert record["exit_code"] == 7
    assert record["expected_exit"] == "nonzero"
    assert record["valid"] is True
    _assert_zero_survivors(record)


def test_unexpected_exit_is_invalid_but_persists_evidence(tmp_path):
    record = _run(tmp_path, [sys.executable, "-c", "raise SystemExit(9)"])
    assert record["exit_code"] == 9
    assert record["valid"] is False
    assert any(
        "did not match expected_exit=zero" in value
        for value in record["validation_errors"]
    )
    assert json.loads(
        (tmp_path / "evidence.json").read_text(encoding="utf-8")
    ) == record
    _assert_zero_survivors(record)


def test_ram_floor_terminates_owned_tree(monkeypatch, tmp_path):
    guard = _guarded_build()
    samples = iter([4 * GIB, 1 * GIB, 4 * GIB])

    def available_ram() -> int:
        return next(samples, 4 * GIB)

    monkeypatch.setattr(guard, "available_ram_bytes", available_ram)
    record = _run(
        tmp_path,
        [sys.executable, "-c", "import time; time.sleep(60)"],
        limits=_limits(
            guard,
            minimum_available_ram_bytes=2 * GIB,
            maximum_runtime_seconds=5.0,
        ),
    )
    assert record["low_memory_stop"] is True
    assert record["termination_reason"] == "minimum_available_ram"
    assert record["configured_minimum_available_ram_bytes"] == 2 * GIB
    assert record["observed_available_ram_bytes"]["minimum"] == 1 * GIB
    assert record["valid"] is False
    _assert_zero_survivors(record)


def test_maximum_runtime_terminates_owned_tree(tmp_path):
    guard = _guarded_build()
    record = _run(
        tmp_path,
        [sys.executable, "-c", "import time; time.sleep(60)"],
        limits=_limits(guard, maximum_runtime_seconds=0.2),
    )
    assert record["timed_out"] is True
    assert record["termination_reason"] == "maximum_runtime"
    assert record["maximum_runtime_seconds"] == 0.2
    assert record["elapsed_seconds"] < 5.0
    assert record["valid"] is False
    _assert_zero_survivors(record)


def test_normal_root_exit_with_live_descendant_is_cleaned_up(tmp_path):
    guard = _guarded_build()
    owned = _owned_guard()
    child_pid_path = tmp_path / "child.pid"
    root_program = (
        "import pathlib,subprocess,sys,time;"
        "child=subprocess.Popen([sys.executable,'-c','import time;time.sleep(60)']);"
        "pathlib.Path(sys.argv[1]).write_text(str(child.pid),encoding='ascii');"
        "time.sleep(0.25);"
        "raise SystemExit(0)"
    )
    record = _run(
        tmp_path,
        [sys.executable, "-c", root_program, str(child_pid_path)],
        limits=_limits(guard, maximum_runtime_seconds=5.0),
    )
    child_pid = int(child_pid_path.read_text(encoding="ascii"))
    assert record["exit_code"] == 0
    assert record["timed_out"] is False
    assert record["low_memory_stop"] is False
    assert record["termination_reason"] is None
    assert child_pid in record["observed_pids"]
    assert record["child_process_observed"] is True
    assert record["job_object"]["terminate_job_called"] is True
    assert record["valid"] is True
    _assert_memory_evidence(record)
    _assert_zero_survivors(record)
    deadline = time.monotonic() + 2.0
    while (
        owned.process_memory_bytes(child_pid) is not None
        and time.monotonic() < deadline
    ):
        time.sleep(0.02)
    assert owned.process_memory_bytes(child_pid) is None


def test_generic_guard_disables_cpu_caps_and_msbuild_node_reuse(tmp_path):
    guard = _guarded_build()
    program = (
        "import os;"
        "print(os.environ.get('MSBUILDDISABLENODEREUSE','missing'),flush=True)"
    )
    record = _run(tmp_path, [sys.executable, "-c", program])
    source = inspect.getsource(guard.run_guarded_command)
    assert "set_cpu_rate_hard_cap" not in source
    assert "_set_process_affinity_mask" not in source
    assert record["launch_governance"] == {
        "created_suspended": True,
        "assigned_before_resume": True,
        "cpu_affinity_mask": None,
        "cpu_rate_hard_cap_percent": None,
    }
    assert record["msbuild_disable_node_reuse"] == "1"
    assert (tmp_path / "child.log").read_text(encoding="utf-8").strip() == "1"
    assert record["valid"] is True
    _assert_memory_evidence(record)


def test_cli_requires_literal_separator_and_preserves_argv(tmp_path):
    log_path = tmp_path / "cli.log"
    evidence_path = tmp_path / "cli.json"
    program = "import json,sys;print(json.dumps(sys.argv[1:]),flush=True)"
    child_argv = ["space value", "--switch=value", "semi;colon", "dollar$(literal)"]
    command = [
        sys.executable,
        "-m",
        "scripts.testing.official_openvino.guarded_build",
        "--cwd",
        str(ROOT),
        "--log",
        str(log_path),
        "--evidence",
        str(evidence_path),
        "--expected-exit",
        "zero",
        "--minimum-available-ram-mib",
        "0",
        "--timeout-seconds",
        "5",
        "--",
        sys.executable,
        "-c",
        program,
        *child_argv,
    ]
    completed = subprocess.run(
        command,
        cwd=ROOT,
        text=True,
        capture_output=True,
        timeout=15,
    )
    assert completed.returncode == 0, completed.stderr
    assert json.loads(log_path.read_text(encoding="utf-8")) == child_argv
    record = json.loads(evidence_path.read_text(encoding="utf-8"))
    assert record["command"][-len(child_argv) :] == child_argv
    assert record["valid"] is True


def test_cli_rejects_command_without_literal_separator(tmp_path):
    completed = subprocess.run(
        [
            sys.executable,
            "-m",
            "scripts.testing.official_openvino.guarded_build",
            "--cwd",
            str(ROOT),
            "--log",
            str(tmp_path / "missing-separator.log"),
            "--evidence",
            str(tmp_path / "missing-separator.json"),
            "--expected-exit",
            "zero",
            "--minimum-available-ram-mib",
            "0",
            "--timeout-seconds",
            "5",
            sys.executable,
            "-c",
            "print('must not run')",
        ],
        cwd=ROOT,
        text=True,
        capture_output=True,
        timeout=10,
    )
    assert completed.returncode == 2
    assert "literal -- separator" in completed.stderr
    assert not (tmp_path / "missing-separator.log").exists()
    assert not (tmp_path / "missing-separator.json").exists()


def test_powershell_51_wrapper_preserves_command_array(tmp_path):
    wrapper = ROOT / "scripts" / "testing" / "invoke_guarded_command.ps1"
    evidence_root = tmp_path / "powershell evidence"
    driver = tmp_path / "driver.ps1"
    program = "import json,sys;print(json.dumps(sys.argv[1:]),flush=True)"
    child_argv = ["space value", "--equals=x", "dollar$(literal)"]
    driver.write_text(
        "\n".join(
            [
                "$ErrorActionPreference = 'Stop'",
                "$childCommand = @(",
                f"  {_ps_literal(sys.executable)},",
                "  '-c',",
                f"  {_ps_literal(program)},",
                f"  {_ps_literal(child_argv[0])},",
                f"  {_ps_literal(child_argv[1])},",
                f"  {_ps_literal(child_argv[2])}",
                ")",
                (
                    f"& {_ps_literal(str(wrapper))} "
                    f"-Label 'ps51-literal-argv' "
                    f"-WorkingDirectory {_ps_literal(str(ROOT))} "
                    f"-EvidenceRoot {_ps_literal(str(evidence_root))} "
                    "-ExpectedExit Zero -TimeoutSeconds 5 "
                    "-Command $childCommand"
                ),
            ]
        )
        + "\n",
        encoding="utf-8",
    )
    completed = subprocess.run(
        [
            "powershell.exe",
            "-NoLogo",
            "-NoProfile",
            "-NonInteractive",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            str(driver),
        ],
        cwd=ROOT,
        text=True,
        capture_output=True,
        timeout=20,
    )
    assert completed.returncode == 0, completed.stderr
    assert json.loads(
        (evidence_root / "ps51-literal-argv.log").read_text(encoding="utf-8")
    ) == child_argv
    record = json.loads(
        (evidence_root / "ps51-literal-argv.json").read_text(encoding="utf-8")
    )
    assert record["valid"] is True
    assert record["maximum_runtime_seconds"] == 5.0
    receipt = json.loads(completed.stdout)
    assert receipt["schema"] == "official-openvino-wrapper-verification/v1"
    assert receipt["actual_exit_code"] == 0
    assert receipt["expected_exit"] == "zero"
    assert receipt["command"] == [
        sys.executable,
        "-c",
        program,
        *child_argv,
    ]
    assert receipt["log_sha256"] == record["log_sha256"]
    assert len(receipt["evidence_sha256"]) == 64
    _assert_memory_evidence(record)
    _assert_zero_survivors(record)


def test_guard_primitives_have_one_owner_and_legacy_modules_reexport():
    guard_path = (
        ROOT / "scripts" / "testing" / "official_openvino" / "owned_process_guard.py"
    )
    capability_path = (
        ROOT / "scripts" / "testing" / "run_openvino_reference_capability.py"
    )
    measurement_path = ROOT / "scripts" / "testing" / "measure_llama_run.py"
    sources = {
        path: path.read_text(encoding="utf-8")
        for path in (guard_path, capability_path, measurement_path)
    }
    binding_owners = [
        path
        for path, source in sources.items()
        if "CreateJobObjectW.argtypes" in source
    ]
    class_owners = [
        path for path, source in sources.items() if "class KillOnCloseJob" in source
    ]
    assert binding_owners == [guard_path]
    assert class_owners == [guard_path]

    sys.path.insert(0, str(SCRIPT_DIR))
    try:
        owned = importlib.import_module(
            "scripts.testing.official_openvino.owned_process_guard"
        )
        guard = importlib.import_module(
            "scripts.testing.official_openvino.guarded_build"
        )
        capability = importlib.import_module("run_openvino_reference_capability")
        measurement = importlib.import_module("measure_llama_run")
    finally:
        sys.path.remove(str(SCRIPT_DIR))
    assert guard.owned_process_guard is owned
    assert capability.owned_process_guard is owned
    assert measurement.owned_process_guard is owned
    assert guard.CREATE_SUSPENDED == 0x00000004
    assert guard.CREATE_SUSPENDED is owned.CREATE_SUSPENDED
    assert capability.CREATE_SUSPENDED is owned.CREATE_SUSPENDED
    assert capability.KillOnCloseJob is owned.KillOnCloseJob
    assert capability.available_ram_bytes is owned.available_ram_bytes
    assert capability.process_memory_bytes is owned.process_memory_bytes
    assert measurement.available_ram_bytes is owned.available_ram_bytes
    assert measurement.process_memory_bytes is owned.process_memory_bytes
    assert measurement.process_tree_pids is owned.process_tree_pids
    assert measurement.process_tree_working_set_bytes is (
        owned.process_tree_working_set_bytes
    )
    owners = [
        name
        for name, module in sys.modules.items()
        if getattr(module, "__file__", None)
        and Path(module.__file__).resolve() == guard_path.resolve()
    ]
    assert owners == [
        "scripts.testing.official_openvino.owned_process_guard"
    ]
