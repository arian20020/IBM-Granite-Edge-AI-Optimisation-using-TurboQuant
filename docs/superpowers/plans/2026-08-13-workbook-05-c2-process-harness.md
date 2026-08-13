# Workbook 05 C2 Process Harness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a safe, resumable Windows child-process harness that preserves complete output and resource evidence, terminates full descendant trees under reviewed safety limits, and classifies infrastructure failures separately from algorithm or model failures.

**Architecture:** C2 uses a narrow PowerShell process module for Windows-native process creation, tree discovery, sampling, and termination, with Python modules for deterministic classification, checkpoint identity, and untrusted-bundle validation. All behavior is proven first with synthetic child processes; no model or external runtime is required by this package.

**Tech Stack:** Windows PowerShell 5.1, Python 3.12.10, JSON Schema Draft 2020-12, Windows CIM process inventory, atomic JSON/checkpoint files, GitHub Actions on clean hosted Windows runners.

## Global Constraints

- C2 consumes an accepted C1 prerequisite/asset decision but does not download, convert, load, or execute a model.
- Every process is launched from an executable path plus an argument array. `Invoke-Expression`, shell interpolation, `cmd /c`, and dynamically evaluated code are forbidden.
- A run workspace is a new normal directory under `C:\w5r`; it is never a reparse point and is never silently reused.
- Available physical RAM below `1610612736` bytes for five consecutive two-second samples terminates descendants before the root process.
- Windows commit use above `90` percent for five consecutive two-second samples terminates descendants before the root process.
- A heartbeat older than `900` seconds terminates descendants before the root process.
- Process-tree working set and private bytes include every discoverable descendant.
- stdout, stderr, structured events, raw output, arguments, environment allowlist, resource samples, and termination evidence are retained independently.
- One retry is permitted only for `InfrastructureInterrupted`; `ResourceSafetyStop`, `Timeout`, `IntegrityFailure`, model/algorithm failure, and output-integrity failure are not automatically retried.
- Checkpoint updates are atomic and generation-bound. A changed prerequisite, asset, executable, argument set, environment allowlist, prompt, or configuration invalidates resume.
- C2 uploads no executable or binary fixture from self-hosted evidence. Repository Python fixture source is allowed; generated `.exe`, `.dll`, `.pyd`, archives, and model files are forbidden from evidence bundles.
- C2 authorises no model, activation, storage, performance, or quality claim.

## File Structure

```text
experiments/granite_turboquant_intel/schemas/workbook05/
  process-attempt.schema.json
  resource-summary.schema.json
  phase3-checkpoint.schema.json

experiments/granite_turboquant_intel/manifests/templates/workbook05/
  process-attempt-template.json
  resource-summary-template.json
  phase3-checkpoint-template.json

scripts/testing/workbook05/phase3/
  process_policy.py
  checkpoint.py
  process_bundle_validation.py

scripts/testing/workbook05/
  Workbook05.Run.psm1
  Invoke-Workbook05Phase3HarnessFixture.ps1
  Validate-Workbook05-Phase3.ps1

tests/testing/workbook05/
  test_phase3_process_contracts.py
  test_phase3_process_policy.py
  test_phase3_checkpoint.py
  test_phase3_process_bundle_validation.py
  test_phase3_harness_workflow_contract.py
  Invoke-Phase3RunModuleTests.Tests.ps1
  fixtures/phase3/process/
    normal_child.py
    stderr_child.py
    event_stream_child.py
    descendant_parent.py
    descendant_child.py
    heartbeat_child.py
    fail_once_child.py
    malformed_event_child.py

.github/workflows/
  workbook-05-phase3-harness-tests.yml

docs/testing/workbook05/
  phase3-process-harness-runbook.md
```

---

### Task 1: Define process-attempt, resource-summary, and Phase 3 checkpoint contracts

**Files:**
- Create: `experiments/granite_turboquant_intel/schemas/workbook05/process-attempt.schema.json`
- Create: `experiments/granite_turboquant_intel/schemas/workbook05/resource-summary.schema.json`
- Create: `experiments/granite_turboquant_intel/schemas/workbook05/phase3-checkpoint.schema.json`
- Create matching templates under: `experiments/granite_turboquant_intel/manifests/templates/workbook05/`
- Modify: `scripts/testing/workbook05/phase3/contracts.py`
- Create: `tests/testing/workbook05/test_phase3_process_contracts.py`

**Interfaces:**
- Adds record types: `process-attempt`, `resource-summary`, `phase3-checkpoint`
- Process classification enum: `IntegrityFailure`, `UnsupportedConfiguration`, `ActivationRejected`, `ActivationUnproven`, `StorageMismatch`, `OutputIntegrityFailure`, `ResourceSafetyStop`, `Timeout`, `InfrastructureInterrupted`, `ModelCompatibilityFailure`, `Passed`

- [ ] **Step 1: Write failing schema tests**

```python
class Phase3ProcessContractTests(unittest.TestCase):
    def test_templates_validate(self) -> None:
        for record_type, filename in {
            "process-attempt": "process-attempt-template.json",
            "resource-summary": "resource-summary-template.json",
            "phase3-checkpoint": "phase3-checkpoint-template.json",
        }.items():
            payload = load_template(filename)
            self.assertEqual([], validate_phase3_record(record_type, payload, REPOSITORY_ROOT))

    def test_passed_attempt_cannot_have_safety_stop(self) -> None:
        payload = load_template("process-attempt-template.json")
        payload["classification"] = "Passed"
        payload["watchdog"]["safety_stop_triggered"] = True
        self.assertNotEqual([], validate_phase3_record("process-attempt", payload, REPOSITORY_ROOT))
```

Also test that a retry relation is required when `attempt_number > 1`, a passed attempt requires exit code `0`, raw paths are repository-relative evidence paths, and a checkpoint step marked `Passed` requires a lowercase SHA-256.

- [ ] **Step 2: Verify RED**

Run:

```powershell
python -m unittest -v tests.testing.workbook05.test_phase3_process_contracts
```

Expected: unknown Phase 3 record types or missing schemas.

- [ ] **Step 3: Implement closed schemas**

The process attempt must require:

```json
{
  "attempt_id": "SMOKE-P2-PILOT-A01",
  "run_role": "Pilot",
  "attempt_number": 1,
  "retry_of_attempt_id": null,
  "identity": {
    "repository_head": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
    "prerequisite_proof_sha256": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
    "asset_lock_sha256": "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
    "executable_sha256": "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
    "request_sha256": "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee"
  },
  "execution": {
    "executable_path": "tools/phase3-driver.exe",
    "arguments": ["--request", "requests/run.json"],
    "working_directory": "work/SMOKE-P2-PILOT-A01",
    "environment_allowlist": {"PATH": "<redacted-and-hashed>"},
    "start_utc": "2026-08-13T00:00:00Z",
    "end_utc": "2026-08-13T00:00:01Z",
    "root_process_id": 100,
    "descendant_exit_codes": [],
    "exit_code": 0
  },
  "evidence": {
    "stdout_path": "logs/stdout.txt",
    "stderr_path": "logs/stderr.txt",
    "event_path": "events/events.jsonl",
    "raw_output_path": "outputs/raw.txt",
    "resource_samples_path": "metrics/resources.csv",
    "resource_summary_path": "metrics/resource-summary.json",
    "termination_path": "proof/termination.json"
  },
  "watchdog": {
    "safety_stop_triggered": false,
    "safety_stop_reason": null,
    "heartbeat_timeout_triggered": false,
    "stage_timeout_triggered": false
  },
  "health": {
    "pre_run": "Passed",
    "cooldown": "Passed",
    "post_run": "Passed"
  },
  "classification": "Passed",
  "failure_ids": [],
  "next_action": "Validate evidence bundle."
}
```

Use explicit `null` rather than absent values where a measurement does not apply.

- [ ] **Step 4: Extend `SCHEMA_NAMES` and verify GREEN**

Run:

```powershell
python -m unittest -v tests.testing.workbook05.test_phase3_process_contracts tests.testing.workbook05.test_phase3_contracts
```

Expected: pass.

- [ ] **Step 5: Commit**

```powershell
git add experiments/granite_turboquant_intel/schemas/workbook05 experiments/granite_turboquant_intel/manifests/templates/workbook05 scripts/testing/workbook05/phase3/contracts.py tests/testing/workbook05/test_phase3_process_contracts.py
git commit -m "test: define Phase 3 process evidence contracts"
```

---

### Task 2: Implement deterministic process classification and retry policy

**Files:**
- Create: `scripts/testing/workbook05/phase3/process_policy.py`
- Create: `tests/testing/workbook05/test_phase3_process_policy.py`

**Interfaces:**
- Produces: `AttemptObservation`
- Produces: `classify_attempt(observation: AttemptObservation) -> AttemptDecision`
- Produces: `retry_allowed(decision: AttemptDecision, prior_attempt_count: int) -> bool`

- [ ] **Step 1: Write the failing precedence tests**

```python
def test_integrity_failure_has_highest_precedence(self) -> None:
    observation = valid_observation(
        integrity_errors=("HASH_MISMATCH",),
        exit_code=1,
        infrastructure_interrupted=True,
    )
    self.assertEqual("IntegrityFailure", classify_attempt(observation).classification)


def test_only_one_infrastructure_retry_is_allowed(self) -> None:
    decision = AttemptDecision("InfrastructureInterrupted", ("RUNNER_DISCONNECTED",), "Retry once.")
    self.assertTrue(retry_allowed(decision, prior_attempt_count=0))
    self.assertFalse(retry_allowed(decision, prior_attempt_count=1))
```

- [ ] **Step 2: Verify RED**

Run: `python -m unittest -v tests.testing.workbook05.test_phase3_process_policy`

Expected: import failure.

- [ ] **Step 3: Implement explicit precedence**

Use this order:

```text
IntegrityFailure
ResourceSafetyStop
Timeout
InfrastructureInterrupted
OutputIntegrityFailure
ModelCompatibilityFailure
UnsupportedConfiguration
ActivationRejected
ActivationUnproven
StorageMismatch
Passed
```

C2 fixtures exercise only the first six and `Passed`; later packages supply the remaining observations without changing the precedence function.

- [ ] **Step 4: Verify GREEN**

Run: `python -m unittest -v tests.testing.workbook05.test_phase3_process_policy`

Expected: pass.

- [ ] **Step 5: Commit**

```powershell
git add scripts/testing/workbook05/phase3/process_policy.py tests/testing/workbook05/test_phase3_process_policy.py
git commit -m "feat: classify Phase 3 process attempts"
```

---

### Task 3: Create deterministic synthetic child-process fixtures

**Files:**
- Create all files under: `tests/testing/workbook05/fixtures/phase3/process/`
- Create: `tests/testing/workbook05/test_phase3_process_fixtures.py`

**Interfaces:**
- `normal_child.py`: emits one event, stdout, raw output, heartbeat; exits `0`
- `stderr_child.py`: emits deterministic stderr; exits `7`
- `event_stream_child.py`: emits `started`, `first_token`, `token`, `completed` JSONL events
- `descendant_parent.py`: starts `descendant_child.py`, records both PIDs, waits
- `heartbeat_child.py`: supports `--mode update` and `--mode stall`
- `fail_once_child.py`: fails first attempt by durable marker and succeeds second
- `malformed_event_child.py`: writes invalid JSONL but exits `0`

- [ ] **Step 1: Write the fixture behavior tests before fixture code**

Use `subprocess.run` with an argument list and temporary directories. Assert exact exit codes and exact output files.

```python
def test_event_fixture_emits_first_token_boundary(self) -> None:
    completed = run_fixture("event_stream_child.py")
    events = [json.loads(line) for line in completed.events_path.read_text().splitlines()]
    self.assertEqual(["started", "first_token", "token", "completed"], [event["event"] for event in events])
```

- [ ] **Step 2: Verify RED**

Run: `python -m unittest -v tests.testing.workbook05.test_phase3_process_fixtures`

Expected: fixture-file missing failure.

- [ ] **Step 3: Implement fixtures with comments and bounded behavior**

Fixtures must never allocate dangerous memory or alter machine settings. Safety-stop tests inject sampler observations rather than exhausting real RAM or commit.

- [ ] **Step 4: Verify GREEN**

Run: `python -m unittest -v tests.testing.workbook05.test_phase3_process_fixtures`

Expected: pass on Windows and hosted CI.

- [ ] **Step 5: Commit**

```powershell
git add tests/testing/workbook05/fixtures/phase3/process tests/testing/workbook05/test_phase3_process_fixtures.py
git commit -m "test: add Phase 3 process fixtures"
```

---

### Task 4: Implement the Windows process supervisor module

**Files:**
- Create: `scripts/testing/workbook05/Workbook05.Run.psm1`
- Create: `tests/testing/workbook05/Invoke-Phase3RunModuleTests.Tests.ps1`

**Interfaces:**
- Produces: `New-Wb05RunWorkspace`
- Produces: `Invoke-Wb05SupervisedProcess`
- Produces: `Get-Wb05ProcessTreeIds`
- Produces: `Stop-Wb05ProcessTree`
- Produces: `Write-Wb05AtomicJson`

- [ ] **Step 1: Write failing module export and argument-boundary tests**

```powershell
$ExpectedFunctions = @(
    'New-Wb05RunWorkspace',
    'Invoke-Wb05SupervisedProcess',
    'Get-Wb05ProcessTreeIds',
    'Stop-Wb05ProcessTree',
    'Write-Wb05AtomicJson'
)

foreach ($Name in $ExpectedFunctions) {
    if (-not (Get-Command -Name $Name -CommandType Function -ErrorAction SilentlyContinue)) {
        throw "Missing Phase 3 run function: $Name"
    }
}
```

Also launch `normal_child.py` with arguments containing spaces, quotes, empty strings, and trailing backslashes; assert the child receives the original boundaries exactly.

- [ ] **Step 2: Verify RED**

Run: `& '.\tests\testing\workbook05\Invoke-Phase3RunModuleTests.Tests.ps1'`

Expected: module missing.

- [ ] **Step 3: Implement workspace creation**

`New-Wb05RunWorkspace -Root 'C:\w5r' -RunIdentity 'fixture-123-1'` must:

- validate the exact root;
- create it only if absent;
- reject reparse points;
- reject an existing child directory;
- create `logs`, `events`, `outputs`, `metrics`, `proof`, `requests`, and `records` subdirectories;
- return their absolute paths without writing outside the new child.

- [ ] **Step 4: Implement supervised process launch**

Use `System.Diagnostics.ProcessStartInfo` with:

```powershell
$StartInfo.UseShellExecute = $false
$StartInfo.RedirectStandardOutput = $true
$StartInfo.RedirectStandardError = $true
$StartInfo.CreateNoWindow = $true
$StartInfo.WorkingDirectory = $WorkingDirectory
$StartInfo.FileName = $ExecutablePath
```

Preserve arguments with the reviewed Windows C-runtime quoting routine. Drain stdout and stderr asynchronously into separate files. Do not route native stderr through PowerShell's error stream.

- [ ] **Step 5: Implement full-tree discovery and descendant-first termination**

Take one `Win32_Process` snapshot, build parent-to-child edges, then stop IDs in descending depth order. Record PID, parent PID, depth, executable name, termination request UTC, and observed exit UTC in `proof/termination.json`.

- [ ] **Step 6: Implement atomic JSON writes**

Write BOM-free UTF-8 to a sibling temporary file, flush, close, validate caller-supplied JSON when requested, then move into place. Never leave a partially written final record.

- [ ] **Step 7: Verify GREEN**

Run:

```powershell
& '.\tests\testing\workbook05\Invoke-Phase3RunModuleTests.Tests.ps1'
```

Expected: module exports, quoting, stdout/stderr separation, workspace rejection, and descendant termination tests pass.

- [ ] **Step 8: Commit**

```powershell
git add scripts/testing/workbook05/Workbook05.Run.psm1 tests/testing/workbook05/Invoke-Phase3RunModuleTests.Tests.ps1
git commit -m "feat: supervise Phase 3 child processes"
```

---

### Task 5: Add the resource sampler and reviewed watchdog

**Files:**
- Modify: `scripts/testing/workbook05/Workbook05.Run.psm1`
- Modify: `tests/testing/workbook05/Invoke-Phase3RunModuleTests.Tests.ps1`

**Interfaces:**
- Produces: `Start-Wb05RunSampler`
- Produces: `Stop-Wb05RunSampler`
- Produces: `Test-Wb05RunWatchdog`
- Produces CSV columns: UTC, root PID, process IDs, working set, private bytes, available RAM, commit percent, CPU percent, heartbeat age

- [ ] **Step 1: Add failing injected-sample tests**

Expose an internal `Test-Wb05RunWatchdog` function accepting a sequence of sample objects so tests do not consume real resources.

```powershell
$Samples = 1..5 | ForEach-Object {
    [pscustomobject]@{
        available_memory_bytes = 1610612735
        commit_percent = 50.0
        heartbeat_age_seconds = 1
    }
}
$Decision = Test-Wb05RunWatchdog -Samples $Samples
if ($Decision.reason -ne 'LOW_AVAILABLE_MEMORY') { throw 'Expected low-memory stop.' }
```

Test reset behavior: four low-memory samples followed by one healthy sample must not trigger; five new low samples must trigger.

- [ ] **Step 2: Verify RED**

Run the PowerShell suite and expect missing sampler/watchdog functions.

- [ ] **Step 3: Implement fixed safety values**

Defaults:

```powershell
[int]$SampleIntervalSeconds = 2
[int64]$MinimumAvailableMemoryBytes = 1610612736
[double]$MaximumCommitPercent = 90
[int]$ConsecutiveSafetySamples = 5
[int]$HeartbeatTimeoutSeconds = 900
```

These may be lowered only in injected tests, never in live workflow inputs.

- [ ] **Step 4: Record complete summary**

`resource-summary.json` includes sample count, peak process-tree working set/private bytes, minimum available memory, maximum commit percent, mean/peak CPU, first/last sample UTC, stop class, and missing-data codes. Empty sampling for a process that exits before the first sample is explicitly `Unavailable`, not zero.

- [ ] **Step 5: Verify GREEN**

Run the full PowerShell suite. Expected: all sampler, reset, heartbeat, empty-sample, and stop-reason tests pass.

- [ ] **Step 6: Commit**

```powershell
git add scripts/testing/workbook05/Workbook05.Run.psm1 tests/testing/workbook05/Invoke-Phase3RunModuleTests.Tests.ps1
git commit -m "feat: add Phase 3 resource watchdog"
```

---

### Task 6: Add cooldown health checks and one controlled retry

**Files:**
- Modify: `scripts/testing/workbook05/Workbook05.Run.psm1`
- Create: `scripts/testing/workbook05/Invoke-Workbook05Phase3HarnessFixture.ps1`
- Modify: `tests/testing/workbook05/Invoke-Phase3RunModuleTests.Tests.ps1`
- Modify: `tests/testing/workbook05/test_phase3_process_policy.py`

**Interfaces:**
- Produces: `Test-Wb05CooldownHealth`
- Produces: `Invoke-Wb05AttemptSequence`
- Cooldown result: `Passed`, `Failed`, or `Not required`

- [ ] **Step 1: Write failing cooldown tests**

Cooldown passes only when:

- no descendant from the prior attempt remains;
- available RAM is at or above 1.5 GiB;
- commit use is at or below 90 percent;
- heartbeat/output files are closed;
- the configured cooldown interval has elapsed.

Inject health observations in tests; do not wait minutes in CI.

- [ ] **Step 2: Write the failing retry-sequence test**

Use `fail_once_child.py`. The first attempt is explicitly classified `InfrastructureInterrupted`; the second succeeds. Assert exactly two attempt records, `retry_of_attempt_id` points to attempt one, and a third attempt is impossible.

- [ ] **Step 3: Verify RED**

Run both PowerShell and Python policy suites; expect missing orchestration behavior.

- [ ] **Step 4: Implement sequence orchestration**

`Invoke-Wb05AttemptSequence` calls the classifier after every attempt. It may retry only when `retry_allowed` returns true and cooldown passes. It does not rewrite attempt one to `Passed` after attempt two succeeds.

- [ ] **Step 5: Verify GREEN**

Run:

```powershell
& '.\tests\testing\workbook05\Invoke-Phase3RunModuleTests.Tests.ps1'
python -m unittest -v tests.testing.workbook05.test_phase3_process_policy
```

Expected: pass.

- [ ] **Step 6: Commit**

```powershell
git add scripts/testing/workbook05/Workbook05.Run.psm1 scripts/testing/workbook05/Invoke-Workbook05Phase3HarnessFixture.ps1 tests/testing/workbook05/Invoke-Phase3RunModuleTests.Tests.ps1 tests/testing/workbook05/test_phase3_process_policy.py
git commit -m "feat: add Phase 3 cooldown and retry"
```

---

### Task 7: Implement identity-bound atomic checkpoint and resume

**Files:**
- Create: `scripts/testing/workbook05/phase3/checkpoint.py`
- Create: `tests/testing/workbook05/test_phase3_checkpoint.py`

**Interfaces:**
- Produces: `CheckpointIdentity`
- Produces: `load_and_verify_checkpoint(path: Path, expected: CheckpointIdentity) -> dict[str, Any]`
- Produces: `record_checkpoint_step(path: Path, expected_generation: int, identity: CheckpointIdentity, step_id: str, status: str, evidence_sha256: str) -> dict[str, Any]`
- Produces: `first_incomplete_step(checkpoint: Mapping[str, Any]) -> str | None`

- [ ] **Step 1: Write failing stale-identity tests**

```python
def test_changed_executable_rejects_resume(self) -> None:
    checkpoint = write_valid_checkpoint(self.path)
    expected = checkpoint_identity(executable_sha256="f" * 64)
    with self.assertRaisesRegex(CheckpointIdentityError, "executable_sha256"):
        load_and_verify_checkpoint(self.path, expected)
```

Cover prerequisite proof, asset lock, executable, request, configuration, prompt, and rubric hashes; generation conflict; duplicate step; unsafe evidence hash; and lock contention.

- [ ] **Step 2: Verify RED**

Run: `python -m unittest -v tests.testing.workbook05.test_phase3_checkpoint`

Expected: import failure.

- [ ] **Step 3: Implement without weakening the existing checkpoint module**

Do not change `scripts/testing/workbook05/checkpoint.py`. The Phase 3 checkpoint has richer identity and a separate schema, while retaining its proven exclusive-lock, generation, fsync, temporary-file, and atomic-replace pattern.

- [ ] **Step 4: Implement resume rules**

A successful completed step may be skipped only when its evidence file still exists and its SHA-256 matches the checkpoint. Failed, blocked, incomplete, or mismatched steps are not skipped. Resume never rewrites expected identities to match current files.

- [ ] **Step 5: Verify GREEN**

Run:

```powershell
python -m unittest -v tests.testing.workbook05.test_phase3_checkpoint tests.testing.workbook05.test_checkpoint
```

Expected: both old and new checkpoint suites pass.

- [ ] **Step 6: Commit**

```powershell
git add scripts/testing/workbook05/phase3/checkpoint.py tests/testing/workbook05/test_phase3_checkpoint.py
git commit -m "feat: add identity-bound Phase 3 checkpoints"
```

---

### Task 8: Collect and validate process evidence as untrusted data

**Files:**
- Create: `scripts/testing/workbook05/phase3/process_bundle_validation.py`
- Create: `tests/testing/workbook05/test_phase3_process_bundle_validation.py`
- Create fixtures under: `tests/testing/workbook05/fixtures/phase3/process-bundles/`

**Interfaces:**
- Produces: `validate_process_bundle(bundle_root: Path, repository_root: Path) -> list[BundleIssue]`
- Required paths include attempt, resource, checkpoint, raw output, events, logs, command, environment allowlist, and manifest

- [ ] **Step 1: Write adversarial tests**

Reject:

- process attempt says `Passed` but root exit code is nonzero;
- resource summary says no safety stop but termination proof says low memory;
- malformed JSONL event stream;
- first-token event after completed event;
- missing raw output or mismatched raw-output hash;
- executable, DLL, archive, model, or secret payload;
- shell-string command instead of argument array;
- absolute or parent-traversing evidence path;
- checkpoint evidence hash drift;
- retry without a valid prior attempt.

- [ ] **Step 2: Verify RED**

Run: `python -m unittest -v tests.testing.workbook05.test_phase3_process_bundle_validation`

Expected: import failure.

- [ ] **Step 3: Implement deterministic cross-record checks**

Validate all JSON schemas first, then manifest, then paths/payloads, then cross-record semantics. Return stable issue codes such as `PROCESS_EXIT_CONTRADICTION`, `SAFETY_STOP_CONTRADICTION`, `EVENT_ORDER_INVALID`, `RAW_OUTPUT_HASH_MISMATCH`, and `RETRY_RELATION_INVALID`.

- [ ] **Step 4: Verify GREEN**

Run the focused suite. Expected: valid bundle passes and every adversarial mutation fails for the intended reason.

- [ ] **Step 5: Commit**

```powershell
git add scripts/testing/workbook05/phase3/process_bundle_validation.py tests/testing/workbook05/test_phase3_process_bundle_validation.py tests/testing/workbook05/fixtures/phase3/process-bundles
git commit -m "test: validate Phase 3 process bundles"
```

---

### Task 9: Add hosted C2 system tests without model or laptop execution

**Files:**
- Create: `.github/workflows/workbook-05-phase3-harness-tests.yml`
- Create: `tests/testing/workbook05/test_phase3_harness_workflow_contract.py`
- Modify: `scripts/testing/Validate-Workbook05-Phase3.ps1`

**Interfaces:**
- Workflow job: `phase3-harness-system-tests`
- Artifact: `workbook-05-phase3-harness-fixtures-${{ github.run_id }}-${{ github.run_attempt }}`

- [ ] **Step 1: Write failing workflow contracts**

Require Windows-hosted execution, exact Python 3.12.10, immutable action SHAs, read-only permissions, exact-head checkout, no self-hosted labels, no external model source, no network-dependent fixture, and same-attempt artifact validation.

- [ ] **Step 2: Verify RED**

Run: `python -m unittest -v tests.testing.workbook05.test_phase3_harness_workflow_contract`

Expected: missing workflow.

- [ ] **Step 3: Implement two hosted jobs**

```text
produce-fixture-evidence   executes synthetic fixtures and writes a text-only bundle
validate-fixture-evidence  downloads the exact artifact and validates as untrusted data
```

The producer runs `Validate-Workbook05-Phase3.ps1` first. It may execute only repository fixture scripts. It must not reference `C:\w5a`, `C:\w5m`, accepted binaries, or Hugging Face.

- [ ] **Step 4: Extend the repository gate**

Add C2 tests and module import/export checks. Keep the final gate marker unreachable until every C1 and C2 test passes.

- [ ] **Step 5: Verify GREEN**

Run:

```powershell
python -m unittest -v tests.testing.workbook05.test_phase3_harness_workflow_contract
& '.\scripts\testing\Validate-Workbook05-Phase3.ps1' -PythonPath 'python'
```

Expected: pass and final marker.

- [ ] **Step 6: Commit**

```powershell
git add .github/workflows/workbook-05-phase3-harness-tests.yml tests/testing/workbook05/test_phase3_harness_workflow_contract.py scripts/testing/Validate-Workbook05-Phase3.ps1
git commit -m "ci: exercise Phase 3 process harness"
```

---

### Task 10: Document C2 operation, debugging, and non-claims

**Files:**
- Create: `docs/testing/workbook05/phase3-process-harness-runbook.md`

- [ ] **Step 1: Document every output and failure class**

Explain stdout versus stderr, event JSONL, raw output, resource CSV, summary JSON, termination proof, attempt record, checkpoint, and hash manifest in beginner-friendly terms.

- [ ] **Step 2: Document recovery**

Describe how to inspect an `InfrastructureInterrupted` attempt, verify no orphan remains, run cooldown, and allow one retry. Explicitly state that `ResourceSafetyStop` and `Timeout` are not algorithm failures and are not auto-retried.

- [ ] **Step 3: Document debugging order**

```text
reproduce exact attempt
-> verify identity hashes
-> inspect process/descendant exit evidence
-> inspect watchdog samples
-> inspect event ordering
-> inspect raw output integrity
-> classify
-> fix only after the failure cause is isolated
```

- [ ] **Step 4: State non-claims**

C2 proves harness behavior only. It does not prove Granite compatibility, TurboQuant activation, cache storage, speed, memory benefit, context length, or output quality.

- [ ] **Step 5: Commit**

```powershell
git add docs/testing/workbook05/phase3-process-harness-runbook.md
git commit -m "docs: add Phase 3 process-harness runbook"
```

---

### Task 11: Run complete C2 verification and prepare the package PR

- [ ] **Step 1: Run focused Python suites**

```powershell
python -m unittest -v `
  tests.testing.workbook05.test_phase3_process_contracts `
  tests.testing.workbook05.test_phase3_process_policy `
  tests.testing.workbook05.test_phase3_process_fixtures `
  tests.testing.workbook05.test_phase3_checkpoint `
  tests.testing.workbook05.test_phase3_process_bundle_validation `
  tests.testing.workbook05.test_phase3_harness_workflow_contract
```

Expected: pass.

- [ ] **Step 2: Run PowerShell component tests**

```powershell
& '.\tests\testing\workbook05\Invoke-Phase3RunModuleTests.Tests.ps1'
```

Expected: pass with no orphan fixture process.

- [ ] **Step 3: Run complete Phase 3 gate**

```powershell
& '.\scripts\testing\Validate-Workbook05-Phase3.ps1' -PythonPath 'python'
```

Expected final line: `WORKBOOK05_PHASE3_GATE_PASS`.

- [ ] **Step 4: Check for orphan processes and forbidden payloads**

```powershell
Get-CimInstance Win32_Process |
  Where-Object { $_.CommandLine -like '*fixtures\phase3\process*' } |
  Format-Table ProcessId, ParentProcessId, CommandLine

git diff --check
git status --short
```

Expected: no fixture process, no whitespace error, no uncommitted output.

- [ ] **Step 5: Open the C2 PR with RED/GREEN and trust-boundary evidence**

Explain why the harness uses synthetic tests first, how descendant-first termination works, why failure classes remain separate, how retry is bounded, and why no model claim is possible.

- [ ] **Step 6: Verify exact final head in GitHub Actions**

Require both normal build/test and `Workbook 05 Phase 3 harness tests` success on the exact final SHA before merge.

## C2 Acceptance Gate

C2 is accepted only when:

- every synthetic normal and failure path is deterministic and reviewable;
- stdout/stderr/event/raw-output evidence is complete and separately retained;
- full descendant trees are discovered and terminated descendant-first;
- all three safety limits are tested without deliberately exhausting the machine;
- only one infrastructure retry is possible and cooldown is required;
- stale or mismatched checkpoints fail closed;
- a clean hosted runner independently validates the text-only fixture bundle;
- no model or scientific optimisation claim is authorised.

## Textbook Basis

- *Why Programs Fail*, Chapters 3–6, 8, 9, and 13–15: controlled failure fixtures, reproduction, observation, origin tracking, cause isolation, and correction verification.
- *Code Complete*, Chapters 8, 22, 23, 28, and 29: defensive programming, developer testing, systematic debugging, configuration control, and incremental integration.
- *The Art of Unit Testing*, Chapters 6–10: asynchronous testing, trustworthy fixtures, maintainable test code, and a deliberate test-level strategy.
- *Designing Secure Software*, Chapters 4, 10, 12, and 13: fail-secure behavior, untrusted input, security tests, and secure development controls.
- *Systems Engineering: Principles and Practice*, Chapters 12, 16, and 17: risk controls, integration boundaries, and test-traceability evidence.
- *Fundamentals of Software Architecture*, Chapters 3 and 6: focused modules and executable fitness functions for process-safety characteristics.