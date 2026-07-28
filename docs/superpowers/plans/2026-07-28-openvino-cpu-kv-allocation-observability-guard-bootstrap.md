# OpenVINO CPU KV Allocation Observability Guard Bootstrap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Install and prove the shared Windows Job-Object, RAM-floor, timeout, and atomic-evidence guard that must govern every heavy OpenVINO process before observer implementation starts.

**Architecture:** One canonically imported module owns the existing Windows process-tree, memory-query, suspended-launch, and governed-cleanup primitives. The generic guard and both existing launchers import the exact module identity `scripts.testing.official_openvino.owned_process_guard`; the existing launchers re-export their prior public primitives so their 79-test and 11-test contracts remain unchanged. The generic runner adds a configurable wall-clock timeout, the fixed 2,048 MiB floor, complete Job-PID memory sampling, atomic combined logs, atomic JSON evidence, and zero-survivor proof without applying any CPU affinity or CPU-rate cap. A PowerShell 5.1 entry point passes a literal argv array after `--`, disables MSBuild node reuse in the child environment, and independently binds the requested command to the persisted exit, argv, paths, governance, resource, cleanup, and hash evidence.

**Tech Stack:** Python 3.11, pytest, Windows `ctypes` Job Objects, PowerShell 5.1, JSON, SHA-256, Git.

## Global Constraints

- Work only in `C:\Users\Student\IBM-Granite-Edge-AI-Optimisation-using-TurboQuant\.worktrees\openvino-turboquant-recovery`.
- Do not start an OpenVINO or OpenVINO GenAI configure, build, compiled test, or probe until this task is committed and reviewed.
- The generic guard floor is exactly `2_048 * 1024 * 1024` bytes.
- `GuardLimits.maximum_runtime_seconds` and CLI `--timeout-seconds` are mandatory parts of the public contract; their default is exactly `7_200.0` seconds.
- The generic guard must not call `KillOnCloseJob.set_cpu_rate_hard_cap`, must not set process affinity, and must report both CPU-governance fields as disabled.
- Every child environment overrides `MSBUILDDISABLENODEREUSE` to the string `"1"`.
- The CLI accepts child argv only after a literal `--` token and never joins argv into a command string.
- Every consumer imports the one canonical module name `scripts.testing.official_openvino.owned_process_guard`; loading the same file under `official_openvino.owned_process_guard` is forbidden.
- The numeric `CREATE_SUSPENDED = 0x00000004` constant is owned and exported by `owned_process_guard.py`; all consumers use that constant and never `subprocess.CREATE_SUSPENDED`.
- Every guarded run records complete Job-PID memory-sample count, peak aggregate working-set bytes, peak aggregate private bytes, every PID whose memory query failed, and separately named configured and observed available-RAM values.
- Final log and evidence files are each written through same-directory temporary files followed by `os.replace`.
- The wrapper must run on Windows PowerShell 5.1; do not use PowerShell 7-only syntax or APIs.
- Preserve all existing public import names used by `measure_llama_run.py` and `run_openvino_reference_capability.py`.
- Preserve exactly 79 tests in `tests/test_openvino_reference_capability.py` and exactly 11 tests in `scripts/testing/tests/test_parse_llama_measurement.py`.
- Author exactly 11 tests in `scripts/testing/tests/test_official_openvino_guarded_build.py`; the final three-file gate is exactly 101 tests.
- Both final 101-test runs must report zero failures, zero errors, and zero skips.
- Keep the guard test processes serial. Do not use `pytest-xdist`.
- Use `apply_patch` for hand-authored files. The explicitly listed hash-gated extraction is a permitted bulk mechanical refactor because it moves existing source byte-for-byte.

## File Structure

- Create `scripts/testing/official_openvino/owned_process_guard.py`: sole owner of Windows Job-Object bindings, process-memory queries, process-tree queries, suspended-process resume, governed cleanup, and resource closure.
- Create `scripts/testing/official_openvino/guarded_build.py`: generic command runner, timeout/RAM monitoring, atomic log/evidence publication, CLI, and evidence schema.
- Create `scripts/testing/invoke_guarded_command.ps1`: PowerShell 5.1-safe command-array adapter and independent evidence validator.
- Create `scripts/testing/tests/test_official_openvino_guarded_build.py`: exactly 11 focused guard tests.
- Modify `scripts/testing/run_openvino_reference_capability.py`: remove duplicate primitive implementations and import/re-export the shared implementations; leave capability-specific CPU affinity and 1% CPU-rate calls unchanged.
- Modify `scripts/testing/measure_llama_run.py`: remove duplicate memory/process-tree implementations and import/re-export the shared implementations; leave measurement behavior unchanged.
- Verify without modifying `tests/test_openvino_reference_capability.py`: existing 79-test behavioral boundary.
- Verify without modifying `scripts/testing/tests/test_parse_llama_measurement.py`: existing 11-test behavioral boundary.

---

### Task 1: Bootstrap the Shared Owned-Process Guard

**Files:**
- Create: `scripts/testing/official_openvino/owned_process_guard.py`
- Create: `scripts/testing/official_openvino/guarded_build.py`
- Create: `scripts/testing/invoke_guarded_command.ps1`
- Create: `scripts/testing/tests/test_official_openvino_guarded_build.py`
- Modify: `scripts/testing/run_openvino_reference_capability.py`
- Modify: `scripts/testing/measure_llama_run.py`
- Verify: `tests/test_openvino_reference_capability.py`
- Verify: `scripts/testing/tests/test_parse_llama_measurement.py`

**Interfaces:**
- Consumes: the current Git blobs `c834225196ad2bd93d42d79a3e967e203abb0061` for `run_openvino_reference_capability.py` and `905e0bcda29695756f5e1079c93d6d9cb61c906d` for `measure_llama_run.py`.
- Produces: `owned_process_guard.CREATE_SUSPENDED`, `KillOnCloseJob`, `available_ram_bytes()`, `process_memory_bytes(pid)`, `process_tree_pids(root_pid)`, `process_tree_working_set_bytes(root_pid)`, `_resume_suspended_process(pid)`, `_set_process_affinity_mask(pid, mask)`, `_cleanup_job(job, process, label, errors, emergency_actions, cleanup_timeout_seconds=10.0)`, `_taskkill(pid)`, `_wait_process(process, timeout, label, errors)`, `_close_run_resources(file_handles, jobs, errors)`, and `_append_error(errors, message)`.
- Produces: `guarded_build.GuardLimits` and `guarded_build.run_guarded_command(command, cwd, log_path, evidence_path, expected_exit, limits)`.
- Produces: a CLI with literal argv syntax `python -m scripts.testing.official_openvino.guarded_build [guard options] -- executable argument`.
- Produces: a PowerShell entry point with parameters `Label`, `WorkingDirectory`, `EvidenceRoot`, `ExpectedExit`, `TimeoutSeconds`, and `Command`.

- [ ] **Step 1: Confirm the immutable source boundary and the 90-test regression baseline**

Run from the parent worktree root:

```powershell
$expectedBlobs = [ordered]@{
  "scripts/testing/run_openvino_reference_capability.py" = "c834225196ad2bd93d42d79a3e967e203abb0061"
  "scripts/testing/measure_llama_run.py" = "905e0bcda29695756f5e1079c93d6d9cb61c906d"
  "tests/test_openvino_reference_capability.py" = "22887eb5a82012324dd3dd8ba33f8b4fc59835bb"
  "scripts/testing/tests/test_parse_llama_measurement.py" = "0f278594363519c3b5bdc613d3d6826ecc1dc10e"
}
foreach ($entry in $expectedBlobs.GetEnumerator()) {
  $actual = (git hash-object -- $entry.Key).Trim()
  if ($LASTEXITCODE -ne 0 -or $actual -ne $entry.Value) {
    throw "Guard-bootstrap source drift: $($entry.Key) expected $($entry.Value), observed $actual"
  }
}

$baseline = & python -m pytest `
  tests/test_openvino_reference_capability.py `
  scripts/testing/tests/test_parse_llama_measurement.py `
  -q 2>&1
$baseline | Write-Output
if ($LASTEXITCODE -ne 0) { throw "Guard-bootstrap baseline failed" }
$baselineText = $baseline -join "`n"
if ($baselineText -notmatch "\b90 passed\b" -or $baselineText -match "\bskipped\b") {
  throw "Expected exactly 90 passed and zero skipped before extraction"
}
```

Expected: all four blob identities match; pytest reports exactly `90 passed` and no skipped tests.

- [ ] **Step 2: Add exactly 11 RED guard tests**

Create `scripts/testing/tests/test_official_openvino_guarded_build.py` with this exact body:

```python
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
```

Confirm the authored count before implementation:

```powershell
python -c "import ast,pathlib; p=pathlib.Path(r'scripts/testing/tests/test_official_openvino_guarded_build.py'); m=ast.parse(p.read_text(encoding='utf-8')); tests=[n.name for n in m.body if isinstance(n,(ast.FunctionDef,ast.AsyncFunctionDef)) and n.name.startswith('test_')]; assert len(tests)==11, tests; print('exact guard tests:',len(tests))"
python -m pytest `
  scripts/testing/tests/test_official_openvino_guarded_build.py `
  tests/test_openvino_reference_capability.py `
  scripts/testing/tests/test_parse_llama_measurement.py `
  --collect-only -q
```

Expected collection: exactly `101 tests collected`. Run the first RED test:

```powershell
python -m pytest `
  scripts/testing/tests/test_official_openvino_guarded_build.py::test_zero_exit_atomically_replaces_log_and_evidence `
  -q
```

Expected RED: failure because `scripts.testing.official_openvino.guarded_build` does not exist.

- [ ] **Step 3: Perform the hash-gated verbatim primitive extraction**

The source blobs checked in Step 1 make this mechanical extraction deterministic. Apply these exact moves:

1. Move the block beginning with `PROCESS_QUERY_LIMITED_INFORMATION = 0x1000` and ending immediately before `def parse_args() -> argparse.Namespace:` from `measure_llama_run.py` into the new owner.
2. Move the block beginning with `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000` and ending immediately before `def _utc_now() -> str:` from `run_openvino_reference_capability.py` into the new owner.
3. Move the complete `_append_error` function.
4. Move the block beginning with `def _taskkill(pid: int) -> dict:` and ending immediately before `def _initial_run_record(`.
5. Coalesce only the identical `PROCESS_QUERY_LIMITED_INFORMATION = 0x1000` definition. Keep numeric `CREATE_SUSPENDED = 0x00000004` in the moved Job block, export it from the owner, and import/re-export it in the capability runner.
6. Extend `_cleanup_job` with the keyword-only argument `cleanup_timeout_seconds: float = 10.0`, reject non-finite or non-positive values, and substitute it for the existing literal `10.0` deadline. Existing callers omit it and retain exact behavior.
7. Add the repository root to each directly executable legacy module's `sys.path`, then import and re-export the moved names only through canonical module name `scripts.testing.official_openvino.owned_process_guard`. Also import that module object as `owned_process_guard` in the generic guard and both legacy consumers so identity can be asserted directly.

Use the following PowerShell 5.1-compatible mechanical refactor. It aborts on source drift or a missing/duplicate anchor and writes UTF-8 without a BOM:

```powershell
$capabilityPath = Join-Path $PWD "scripts/testing/run_openvino_reference_capability.py"
$measurementPath = Join-Path $PWD "scripts/testing/measure_llama_run.py"
$ownerPath = Join-Path $PWD "scripts/testing/official_openvino/owned_process_guard.py"
$utf8 = New-Object System.Text.UTF8Encoding($false)

function Assert-One([string]$Text, [string]$Needle, [string]$Label) {
  $first = $Text.IndexOf($Needle, [StringComparison]::Ordinal)
  $last = $Text.LastIndexOf($Needle, [StringComparison]::Ordinal)
  if ($first -lt 0 -or $first -ne $last) {
    throw "$Label anchor must occur exactly once: $Needle"
  }
}

function Remove-Block(
  [ref]$Text,
  [string]$StartNeedle,
  [string]$EndNeedle,
  [string]$Label
) {
  Assert-One $Text.Value $StartNeedle "$Label start"
  Assert-One $Text.Value $EndNeedle "$Label end"
  $start = $Text.Value.IndexOf($StartNeedle, [StringComparison]::Ordinal)
  $end = $Text.Value.IndexOf(
    $EndNeedle,
    $start,
    [StringComparison]::Ordinal
  )
  if ($end -le $start) { throw "$Label anchors are out of order" }
  $block = $Text.Value.Substring($start, $end - $start)
  $Text.Value = $Text.Value.Remove($start, $end - $start)
  return $block
}

$actualCapabilityBlob = (git hash-object -- "scripts/testing/run_openvino_reference_capability.py").Trim()
$actualMeasurementBlob = (git hash-object -- "scripts/testing/measure_llama_run.py").Trim()
if ($actualCapabilityBlob -ne "c834225196ad2bd93d42d79a3e967e203abb0061") {
  throw "Capability source changed before extraction: $actualCapabilityBlob"
}
if ($actualMeasurementBlob -ne "905e0bcda29695756f5e1079c93d6d9cb61c906d") {
  throw "Measurement source changed before extraction: $actualMeasurementBlob"
}

$capability = [IO.File]::ReadAllText($capabilityPath)
$measurement = [IO.File]::ReadAllText($measurementPath)
$capabilityNewline = if ($capability.Contains("`r`n")) { "`r`n" } else { "`n" }
$measurementNewline = if ($measurement.Contains("`r`n")) { "`r`n" } else { "`n" }
$ownerNewline = $capabilityNewline

$memoryBlock = Remove-Block ([ref]$measurement) `
  "PROCESS_QUERY_LIMITED_INFORMATION = 0x1000" `
  "def parse_args() -> argparse.Namespace:" `
  "measurement primitives"
$jobBlock = Remove-Block ([ref]$capability) `
  "JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000" `
  "def _utc_now() -> str:" `
  "Job Object primitives"
$appendBlock = Remove-Block ([ref]$capability) `
  "def _append_error(errors: list[str], message: str) -> None:" `
  "def _parse_bool(value: str, field: str) -> bool:" `
  "append-error primitive"
$cleanupBlock = Remove-Block ([ref]$capability) `
  "def _taskkill(pid: int) -> dict:" `
  "def _initial_run_record(" `
  "cleanup primitives"

$memoryBlock = $memoryBlock.Replace("`r`n", "`n")
if ($ownerNewline -eq "`r`n") {
  $memoryBlock = $memoryBlock.Replace("`n", "`r`n")
}

$duplicateProcessQuery = (
  "PROCESS_QUERY_LIMITED_INFORMATION = 0x1000" + $capabilityNewline
)
Assert-One $jobBlock $duplicateProcessQuery "duplicate process-query constant"
$jobBlock = $jobBlock.Replace($duplicateProcessQuery, "")

$oldCleanupSignature = @(
  "def _cleanup_job("
  "    job: KillOnCloseJob | None,"
  "    process: subprocess.Popen | None,"
  "    label: str,"
  "    errors: list[str],"
  "    emergency_actions: list[dict],"
  ") -> tuple[dict, bool]:"
) -join $capabilityNewline
$newCleanupSignature = @(
  "def _cleanup_job("
  "    job: KillOnCloseJob | None,"
  "    process: subprocess.Popen | None,"
  "    label: str,"
  "    errors: list[str],"
  "    emergency_actions: list[dict],"
  "    *,"
  "    cleanup_timeout_seconds: float = 10.0,"
  ") -> tuple[dict, bool]:"
  "    if ("
  "        not math.isfinite(cleanup_timeout_seconds)"
  "        or cleanup_timeout_seconds <= 0"
  "    ):"
  "        raise ValueError(""cleanup_timeout_seconds must be finite and positive"")"
) -join $capabilityNewline
Assert-One $cleanupBlock $oldCleanupSignature "cleanup signature"
$cleanupBlock = $cleanupBlock.Replace($oldCleanupSignature, $newCleanupSignature)
Assert-One $cleanupBlock "deadline = time.monotonic() + 10.0" "cleanup deadline"
$cleanupBlock = $cleanupBlock.Replace(
  "deadline = time.monotonic() + 10.0",
  "deadline = time.monotonic() + cleanup_timeout_seconds"
)

$capability = $capability.Replace(
  "import ctypes" + $capabilityNewline,
  ""
)
$capability = $capability.Replace(
  "from ctypes import wintypes" + $capabilityNewline,
  ""
)
$oldCapabilityImport = "from measure_llama_run import available_ram_bytes, process_memory_bytes  # noqa: E402"
$newCapabilityImport = @(
  "REPO_ROOT = SCRIPT_DIR.parent.parent"
  "if str(REPO_ROOT) not in sys.path:"
  "    sys.path.insert(0, str(REPO_ROOT))"
  ""
  "from scripts.testing.official_openvino import owned_process_guard  # noqa: E402"
  "from scripts.testing.official_openvino.owned_process_guard import (  # noqa: E402"
  "    CREATE_SUSPENDED,"
  "    KillOnCloseJob,"
  "    _append_error,"
  "    _cleanup_job,"
  "    _close_run_resources,"
  "    _resume_suspended_process,"
  "    _set_process_affinity_mask,"
  "    _taskkill,"
  "    _wait_process,"
  "    available_ram_bytes,"
  "    process_memory_bytes,"
  ")"
) -join $capabilityNewline
Assert-One $capability $oldCapabilityImport "capability import"
$capability = $capability.Replace($oldCapabilityImport, $newCapabilityImport)

$measurement = $measurement.Replace(
  "import ctypes" + $measurementNewline,
  ""
)
$measurement = $measurement.Replace(
  "from ctypes import wintypes" + $measurementNewline,
  ""
)
$oldMeasurementImport = "from parse_llama_measurement import summarize_measurement"
$newMeasurementImport = @(
  "SCRIPT_DIR = Path(__file__).resolve().parent"
  "REPO_ROOT = SCRIPT_DIR.parent.parent"
  "if str(REPO_ROOT) not in sys.path:"
  "    sys.path.insert(0, str(REPO_ROOT))"
  ""
  "from scripts.testing.official_openvino import owned_process_guard  # noqa: E402"
  "from scripts.testing.official_openvino.owned_process_guard import (  # noqa: E402"
  "    available_ram_bytes,"
  "    process_memory_bytes,"
  "    process_tree_memory_bytes,"
  "    process_tree_pids,"
  "    process_tree_working_set_bytes,"
  "    working_set_bytes,"
  ")"
  ""
  "from parse_llama_measurement import summarize_measurement  # noqa: E402"
) -join $measurementNewline
Assert-One $measurement $oldMeasurementImport "measurement import"
$measurement = $measurement.Replace(
  $oldMeasurementImport,
  $newMeasurementImport
)

$ownerHeader = @(
  '"""Own Windows process trees and expose shared RAM/process sampling primitives."""'
  ""
  "from __future__ import annotations"
  ""
  "import ctypes"
  "import math"
  "import os"
  "import subprocess"
  "import time"
  "from ctypes import wintypes"
  "from typing import Any"
  ""
  ""
) -join $ownerNewline
$ownerExports = @(
  ""
  "__all__ = ["
  "    ""CREATE_SUSPENDED"","
  "    ""KillOnCloseJob"","
  "    ""_append_error"","
  "    ""_cleanup_job"","
  "    ""_close_run_resources"","
  "    ""_resume_suspended_process"","
  "    ""_set_process_affinity_mask"","
  "    ""_taskkill"","
  "    ""_wait_process"","
  "    ""available_ram_bytes"","
  "    ""process_memory_bytes"","
  "    ""process_tree_memory_bytes"","
  "    ""process_tree_pids"","
  "    ""process_tree_working_set_bytes"","
  "    ""working_set_bytes"","
  "]"
  ""
) -join $ownerNewline
$owner = (
  $ownerHeader +
  $memoryBlock.TrimStart("`r", "`n") +
  $jobBlock +
  $appendBlock +
  $cleanupBlock +
  $ownerExports
)

[IO.File]::WriteAllText($ownerPath, $owner, $utf8)
[IO.File]::WriteAllText($capabilityPath, $capability, $utf8)
[IO.File]::WriteAllText($measurementPath, $measurement, $utf8)
```

Run formatting-neutral structural checks:

```powershell
python -m py_compile `
  scripts/testing/official_openvino/owned_process_guard.py `
  scripts/testing/run_openvino_reference_capability.py `
  scripts/testing/measure_llama_run.py
rg -n "CreateJobObjectW\.argtypes|class KillOnCloseJob" `
  scripts/testing/official_openvino/owned_process_guard.py `
  scripts/testing/run_openvino_reference_capability.py `
  scripts/testing/measure_llama_run.py
```

Expected: compilation succeeds; both searches report only `owned_process_guard.py`.

Run the shared-owner test and the unchanged regression suites:

```powershell
python -m pytest `
  scripts/testing/tests/test_official_openvino_guarded_build.py::test_guard_primitives_have_one_owner_and_legacy_modules_reexport `
  tests/test_openvino_reference_capability.py `
  scripts/testing/tests/test_parse_llama_measurement.py `
  -q
```

Expected: exactly `91 passed`, zero skipped.

- [ ] **Step 4: Add the exact generic guard implementation**

Create `scripts/testing/official_openvino/guarded_build.py` with this exact body:

```python
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
```

Run the first nine non-wrapper guard tests:

```powershell
$guardTests = @(
  "test_zero_exit_atomically_replaces_log_and_evidence",
  "test_expected_nonzero_exit_is_valid",
  "test_unexpected_exit_is_invalid_but_persists_evidence",
  "test_ram_floor_terminates_owned_tree",
  "test_maximum_runtime_terminates_owned_tree",
  "test_normal_root_exit_with_live_descendant_is_cleaned_up",
  "test_generic_guard_disables_cpu_caps_and_msbuild_node_reuse",
  "test_cli_requires_literal_separator_and_preserves_argv",
  "test_cli_rejects_command_without_literal_separator"
)
$selectors = $guardTests | ForEach-Object {
  "scripts/testing/tests/test_official_openvino_guarded_build.py::$_"
}
python -m pytest @selectors -q
```

Expected: exactly `9 passed`, zero skipped.

- [ ] **Step 5: Add the exact PowerShell 5.1 wrapper**

Create `scripts/testing/invoke_guarded_command.ps1` with this exact body:

```powershell
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$')]
  [string]$Label,

  [Parameter(Mandatory = $true)]
  [string]$WorkingDirectory,

  [Parameter(Mandatory = $true)]
  [string]$EvidenceRoot,

  [Parameter(Mandatory = $true)]
  [ValidateSet('Zero', 'NonZero')]
  [string]$ExpectedExit,

  [Parameter(Mandatory = $false)]
  [ValidateRange(0.001, 86400.0)]
  [double]$TimeoutSeconds = 7200.0,

  [Parameter(Mandatory = $true)]
  [ValidateNotNullOrEmpty()]
  [string[]]$Command
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSVersion.Major -lt 5) {
  throw 'invoke_guarded_command.ps1 requires Windows PowerShell 5.1 or newer'
}
if ($Command.Count -eq 0) {
  throw 'Command must contain at least one argv element'
}
foreach ($argument in $Command) {
  if ([string]::IsNullOrEmpty($argument)) {
    throw 'Command argv elements must be non-empty strings'
  }
}

$resolvedWorkingDirectory = (Resolve-Path -LiteralPath $WorkingDirectory).Path
$resolvedEvidenceRoot = [IO.Path]::GetFullPath($EvidenceRoot)
[IO.Directory]::CreateDirectory($resolvedEvidenceRoot) | Out-Null
$logPath = Join-Path $resolvedEvidenceRoot ($Label + '.log')
$evidencePath = Join-Path $resolvedEvidenceRoot ($Label + '.json')
if (
  [IO.File]::Exists($logPath) -or
  [IO.File]::Exists($evidencePath)
) {
  throw "Guard evidence label already exists: $Label"
}

$expectedExitValue = if ($ExpectedExit -eq 'Zero') { 'zero' } else { 'nonzero' }
$timeoutValue = $TimeoutSeconds.ToString(
  'R',
  [Globalization.CultureInfo]::InvariantCulture
)
$python = (Get-Command python -ErrorAction Stop).Source
$guardArguments = @(
  '-m',
  'scripts.testing.official_openvino.guarded_build',
  '--cwd',
  $resolvedWorkingDirectory,
  '--log',
  $logPath,
  '--evidence',
  $evidencePath,
  '--expected-exit',
  $expectedExitValue,
  '--minimum-available-ram-mib',
  '2048',
  '--timeout-seconds',
  $timeoutValue,
  '--'
) + @($Command)

$previousErrorActionPreference = $ErrorActionPreference
try {
  # Windows PowerShell 5.1 can surface native stderr as non-terminating error
  # records. The native exit code, not ErrorAction, is authoritative here.
  $ErrorActionPreference = 'Continue'
  $guardNativeOutput = @(& $python @guardArguments)
  $guardExitCode = $LASTEXITCODE
}
finally {
  $ErrorActionPreference = $previousErrorActionPreference
}
if (-not [IO.File]::Exists($evidencePath)) {
  throw "Guard did not publish evidence: $evidencePath"
}
if (-not [IO.File]::Exists($logPath)) {
  throw "Guard did not publish a log: $logPath"
}

$record = Get-Content -LiteralPath $evidencePath -Raw | ConvertFrom-Json
if ($record.schema -ne 'official-openvino-owned-process-guard/v1') {
  throw "Unexpected guard schema: $($record.schema)"
}
if ($record.expected_exit -ne $expectedExitValue) {
  throw "Guard expected-exit mismatch: $($record.expected_exit)"
}
if ($null -eq $record.exit_code) {
  throw 'Guard did not record the actual child exit code'
}
$actualExitCode = [int]$record.exit_code
$actualExitMatches = if ($ExpectedExit -eq 'Zero') {
  $actualExitCode -eq 0
}
else {
  $actualExitCode -ne 0
}
if (-not $actualExitMatches) {
  throw (
    "Actual child exit $actualExitCode does not match ExpectedExit " +
    $ExpectedExit
  )
}

$recordCommand = @($record.command)
if ($recordCommand.Count -ne $Command.Count) {
  throw 'Guard command argv count does not match the requested argv'
}
for ($index = 0; $index -lt $Command.Count; $index++) {
  if (
    -not [StringComparer]::Ordinal.Equals(
      [string]$recordCommand[$index],
      [string]$Command[$index]
    )
  ) {
    throw "Guard command argv mismatch at index $index"
  }
}
$resolvedLogPath = (Resolve-Path -LiteralPath $logPath).Path
$resolvedEvidencePath = (Resolve-Path -LiteralPath $evidencePath).Path
if (
  -not [StringComparer]::OrdinalIgnoreCase.Equals(
    [string]$record.working_directory,
    $resolvedWorkingDirectory
  )
) {
  throw 'Guard working-directory evidence does not match the request'
}
if (
  -not [StringComparer]::OrdinalIgnoreCase.Equals(
    [string]$record.log_path,
    $resolvedLogPath
  )
) {
  throw 'Guard log-path evidence does not match the request'
}
if (
  -not [StringComparer]::OrdinalIgnoreCase.Equals(
    [string]$record.evidence_path,
    $resolvedEvidencePath
  )
) {
  throw 'Guard evidence-path evidence does not match the request'
}
if ([double]$record.maximum_runtime_seconds -ne $TimeoutSeconds) {
  throw "Guard timeout mismatch: $($record.maximum_runtime_seconds)"
}
if ([int64]$record.configured_minimum_available_ram_bytes -ne 2147483648) {
  throw 'Guard configured RAM floor is not exactly 2,048 MiB'
}
$configuredRamFloor = [int64]$record.configured_minimum_available_ram_bytes
if (
  $null -eq $record.observed_available_ram_bytes.before -or
  $null -eq $record.observed_available_ram_bytes.minimum -or
  $null -eq $record.observed_available_ram_bytes.after
) {
  throw 'Guard did not record complete observed available-RAM evidence'
}
$observedRamMinimum = [int64]$record.observed_available_ram_bytes.minimum
if ($observedRamMinimum -lt $configuredRamFloor) {
  throw 'Guard observed RAM fell below the configured 2,048 MiB floor'
}
if (
  $observedRamMinimum -gt
    [int64]$record.observed_available_ram_bytes.before -or
  $observedRamMinimum -gt
    [int64]$record.observed_available_ram_bytes.after
) {
  throw 'Guard observed minimum RAM is inconsistent with endpoint samples'
}
if (
  [int]$record.memory_sample_count -lt 1 -or
  [int64]$record.peak_working_set_bytes -le 0 -or
  [int64]$record.peak_private_bytes -le 0
) {
  throw 'Guard did not record complete Job-PID memory samples and peaks'
}
if (
  $record.launch_governance.created_suspended -ne $true -or
  $record.launch_governance.assigned_before_resume -ne $true
) {
  throw 'Guard did not prove suspended launch and assignment before resume'
}
if ($record.job_object.setup_ok -ne $true) {
  throw 'Guard did not prove Job Object setup'
}
if ($record.job_object.query_ok -ne $true) {
  throw 'Guard did not query the Job Object after cleanup'
}
if (
  [int]$record.job_object.queried_active_process_count_after_cleanup -ne 0
) {
  throw 'Guard reported active processes after cleanup'
}
if (@($record.job_object.survivor_pids_after_cleanup).Count -ne 0) {
  throw 'Guard reported survivor PIDs after cleanup'
}
if (
  $record.timed_out -ne $false -or
  $record.low_memory_stop -ne $false -or
  $null -ne $record.termination_reason
) {
  throw 'Guarded command reported timeout, low memory, or forced termination'
}
if (@($record.emergency_actions).Count -ne 0) {
  throw 'Guarded command required emergency cleanup'
}
if (@($record.validation_errors).Count -ne 0) {
  throw 'Guarded command persisted validation errors'
}
if ($null -ne $record.launch_governance.cpu_affinity_mask) {
  throw 'Generic guard unexpectedly applied CPU affinity'
}
if ($null -ne $record.launch_governance.cpu_rate_hard_cap_percent) {
  throw 'Generic guard unexpectedly applied a CPU-rate cap'
}
if ($record.msbuild_disable_node_reuse -ne '1') {
  throw 'MSBUILDDISABLENODEREUSE was not forced to 1'
}
$observedLogHash = (
  Get-FileHash -LiteralPath $logPath -Algorithm SHA256
).Hash.ToLowerInvariant()
if ($record.log_sha256 -ne $observedLogHash) {
  throw 'Guard log SHA-256 does not match the persisted log'
}
if ($record.valid -ne $true -or $guardExitCode -ne 0) {
  $failures = @($record.validation_errors) -join '; '
  $nativeText = @($guardNativeOutput) -join '; '
  throw "Guarded command failed validation: $failures; native: $nativeText"
}

$observedEvidenceHash = (
  Get-FileHash -LiteralPath $evidencePath -Algorithm SHA256
).Hash.ToLowerInvariant()
$verification = [ordered]@{
  schema = 'official-openvino-wrapper-verification/v1'
  command = @($Command)
  working_directory = $resolvedWorkingDirectory
  log_path = $resolvedLogPath
  evidence_path = $resolvedEvidencePath
  expected_exit = $expectedExitValue
  actual_exit_code = $actualExitCode
  log_sha256 = $observedLogHash
  evidence_sha256 = $observedEvidenceHash
  valid = $true
}
Write-Output ($verification | ConvertTo-Json -Depth 8 -Compress)
```

Run both wrapper-focused tests:

```powershell
python -m pytest `
  scripts/testing/tests/test_official_openvino_guarded_build.py::test_powershell_51_wrapper_preserves_command_array `
  scripts/testing/tests/test_official_openvino_guarded_build.py::test_guard_primitives_have_one_owner_and_legacy_modules_reexport `
  -q
```

Expected: exactly `2 passed`, zero skipped.

- [ ] **Step 6: Run static integrity checks**

Run:

```powershell
python -m py_compile `
  scripts/testing/official_openvino/owned_process_guard.py `
  scripts/testing/official_openvino/guarded_build.py `
  scripts/testing/run_openvino_reference_capability.py `
  scripts/testing/measure_llama_run.py

$createJobOwners = rg -l "CreateJobObjectW\.argtypes" `
  scripts/testing/official_openvino/owned_process_guard.py `
  scripts/testing/run_openvino_reference_capability.py `
  scripts/testing/measure_llama_run.py
if ($LASTEXITCODE -ne 0) { throw "CreateJobObjectW owner scan failed" }
if (
  @($createJobOwners).Count -ne 1 -or
  $createJobOwners[0].Replace('\', '/') -ne
    'scripts/testing/official_openvino/owned_process_guard.py'
) {
  throw "Expected exactly one CreateJobObjectW binding owner"
}

$suspendedConstantOwners = rg -l "^CREATE_SUSPENDED = 0x00000004$" `
  scripts/testing/official_openvino/owned_process_guard.py `
  scripts/testing/official_openvino/guarded_build.py `
  scripts/testing/run_openvino_reference_capability.py `
  scripts/testing/measure_llama_run.py
if ($LASTEXITCODE -ne 0) { throw "CREATE_SUSPENDED owner scan failed" }
if (
  @($suspendedConstantOwners).Count -ne 1 -or
  $suspendedConstantOwners[0].Replace('\', '/') -ne
    'scripts/testing/official_openvino/owned_process_guard.py'
) {
  throw "Expected exactly one numeric CREATE_SUSPENDED owner"
}

$forbiddenSuspended = rg -n "subprocess\.CREATE_SUSPENDED" `
  scripts/testing/official_openvino/owned_process_guard.py `
  scripts/testing/official_openvino/guarded_build.py `
  scripts/testing/run_openvino_reference_capability.py `
  scripts/testing/measure_llama_run.py
if ($LASTEXITCODE -eq 0) {
  throw "A consumer uses subprocess.CREATE_SUSPENDED instead of the shared constant"
}
if ($LASTEXITCODE -ne 1) { throw "subprocess.CREATE_SUSPENDED scan failed" }

$nonCanonicalImports = rg -n `
  "(from|import) official_openvino\.owned_process_guard" `
  scripts/testing/official_openvino/guarded_build.py `
  scripts/testing/run_openvino_reference_capability.py `
  scripts/testing/measure_llama_run.py
if ($LASTEXITCODE -eq 0) {
  throw "Found a non-canonical owned_process_guard import"
}
if ($LASTEXITCODE -ne 1) { throw "canonical owner import scan failed" }

$genericGuardSource = Get-Content `
  scripts/testing/official_openvino/guarded_build.py -Raw
if (
  $genericGuardSource.Contains('set_cpu_rate_hard_cap') -or
  $genericGuardSource.Contains('_set_process_affinity_mask')
) {
  throw "Generic guard contains forbidden CPU governance"
}
if (-not $genericGuardSource.Contains('MSBUILDDISABLENODEREUSE')) {
  throw "Generic guard does not disable MSBuild node reuse"
}
if (-not $genericGuardSource.Contains('maximum_runtime_seconds')) {
  throw "Generic guard has no maximum runtime"
}
if (-not $genericGuardSource.Contains('"--"')) {
  throw "Generic guard does not require the literal argv separator"
}
foreach ($requiredGuardToken in @(
  'process_memory_bytes',
  'memory_sample_count',
  'peak_working_set_bytes',
  'peak_private_bytes',
  'memory_query_failed_pids',
  'configured_minimum_available_ram_bytes',
  'observed_available_ram_bytes'
)) {
  if (-not $genericGuardSource.Contains($requiredGuardToken)) {
    throw "Generic guard is missing evidence token: $requiredGuardToken"
  }
}

$wrapperSource = Get-Content scripts/testing/invoke_guarded_command.ps1 -Raw
foreach ($requiredWrapperToken in @(
  "`$ErrorActionPreference = 'Continue'",
  '$guardExitCode = $LASTEXITCODE',
  'created_suspended',
  'assigned_before_resume',
  'configured_minimum_available_ram_bytes',
  'observed_available_ram_bytes',
  'evidence_sha256',
  'log_sha256'
)) {
  if (-not $wrapperSource.Contains($requiredWrapperToken)) {
    throw "PowerShell wrapper is missing validation token: $requiredWrapperToken"
  }
}

python -c "import ast,pathlib; p=pathlib.Path(r'scripts/testing/tests/test_official_openvino_guarded_build.py'); m=ast.parse(p.read_text(encoding='utf-8')); tests=[n.name for n in m.body if isinstance(n,(ast.FunctionDef,ast.AsyncFunctionDef)) and n.name.startswith('test_')]; assert len(tests)==11, tests; print('exact guard tests:',len(tests))"
git diff --check
```

Expected: all commands succeed; the only Job Object binding and numeric `CREATE_SUSPENDED` owner is `owned_process_guard.py`; no consumer uses `subprocess.CREATE_SUSPENDED` or a non-canonical owner import; all memory-evidence and independent-wrapper validation tokens are present; the new file has exactly 11 test functions; `git diff --check` is silent.

- [ ] **Step 7: Run the exact 101-test acceptance gate twice with zero skips**

Run:

```powershell
$acceptanceTests = @(
  "scripts/testing/tests/test_official_openvino_guarded_build.py",
  "tests/test_openvino_reference_capability.py",
  "scripts/testing/tests/test_parse_llama_measurement.py"
)
foreach ($run in 1..2) {
  $output = & python -m pytest @acceptanceTests -q 2>&1
  $exitCode = $LASTEXITCODE
  $output | Write-Output
  if ($exitCode -ne 0) {
    throw "Guard bootstrap acceptance run $run failed"
  }
  $text = $output -join "`n"
  if ($text -notmatch "\b101 passed\b") {
    throw "Guard bootstrap run $run did not report exactly 101 passed"
  }
  if ($text -match "\bskipped\b") {
    throw "Guard bootstrap run $run reported a skipped test"
  }
}
```

Expected twice: exactly `101 passed`, with no failed, error, xfailed, xpassed, or skipped result.

- [ ] **Step 8: Review the exact task diff and commit**

Inspect only the intended files:

```powershell
git status --short
git diff -- `
  scripts/testing/official_openvino/owned_process_guard.py `
  scripts/testing/official_openvino/guarded_build.py `
  scripts/testing/invoke_guarded_command.ps1 `
  scripts/testing/tests/test_official_openvino_guarded_build.py `
  scripts/testing/run_openvino_reference_capability.py `
  scripts/testing/measure_llama_run.py
```

Required review facts:

- `run_openvino_reference_capability.py` still invokes its capability-specific `set_cpu_rate_hard_cap(EXACT_WORKLOAD_CPU_RATE)` and `_set_process_affinity_mask(workload.pid, EXACT_WORKLOAD_AFFINITY_MASK)`.
- `guarded_build.py` invokes neither CPU-governance method.
- Both legacy modules re-export their prior helper names.
- `guarded_build.py` and both legacy modules hold the same canonical `owned_process_guard` module object; no second module identity is loaded from the same file.
- `owned_process_guard.py` contains the only `CreateJobObjectW` binding and only `KillOnCloseJob` class.
- `owned_process_guard.py` contains and exports the only numeric `CREATE_SUSPENDED`; both the generic guard and capability runner use the imported numeric constant.
- The sampling loop queries active Job PIDs, samples each through `process_memory_bytes`, and persists complete-sample count, aggregate working/private peaks, failed PIDs, configured floor, and observed available RAM.
- Both final artifacts use same-directory temporary files and `os.replace`.
- The CLI rejects a child command without literal `--`.
- The wrapper splats a string array and never calls `Invoke-Expression`, `cmd.exe`, or `Start-Process`.
- The wrapper temporarily uses `ErrorActionPreference = 'Continue'` only around native Python, captures `$LASTEXITCODE` immediately, then independently validates actual-vs-expected exit, exact argv/cwd/log/evidence paths, suspended Job assignment, cleanup, false stop/emergency flags, RAM floor, memory evidence, and both returned file hashes.
- One guard test proves a root exits normally with code zero while its descendant remains alive, then verifies Job cleanup kills that descendant; separate timeout and low-RAM tests remain present.
- The final test arithmetic is `11 + 79 + 11 = 101`.

Commit only after both acceptance runs and review succeed:

```powershell
git add `
  scripts/testing/official_openvino/owned_process_guard.py `
  scripts/testing/official_openvino/guarded_build.py `
  scripts/testing/invoke_guarded_command.ps1 `
  scripts/testing/tests/test_official_openvino_guarded_build.py `
  scripts/testing/run_openvino_reference_capability.py `
  scripts/testing/measure_llama_run.py
git commit -m "build(testing): guard heavy OpenVINO processes"
```

Expected: one focused commit containing only the six implementation/test files listed above. The two pre-existing regression test files remain byte-identical.
