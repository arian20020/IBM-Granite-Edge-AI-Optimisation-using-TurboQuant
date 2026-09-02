# Workbook 05 Route A Runtime Controlled-Resume Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resume the exact timed-out Route A Runtime workspace only after fail-closed artifact, source, path and CMake-cache requalification, and guarantee a complete decision before the outer GitHub job timeout.

**Architecture:** Add a dedicated manual controlled-resume workflow instead of complicating the ordinary fresh Runtime path. A new Python validator treats the prior timeout bundle as untrusted data, a small PowerShell boundary verifies GitHub’s artifact identity, a focused resume orchestrator requalifies the local workspace and continues the existing incremental CMake build, and the shared process adapter gains an optional elapsed-time stop that preserves evidence.

**Tech Stack:** GitHub Actions, Windows PowerShell 5.1, Python 3.12 `unittest`, CMake, Visual Studio/MSBuild/MSVC, OpenVINO Runtime source build.

## Global Constraints

- Base commit: `fb5d3349aae9d7acb6bd8132cbffa2303e40cabd`.
- Isolated branch: `fix/workbook-05-runtime-controlled-resume`.
- Prior workflow run: `31391119557`.
- Prior run attempt: `4`.
- Prior artifact: `workbook-05-build-route-a-runtime-31391119557-4`.
- Prior artifact ID: `9113026088`.
- Recorded and independently verified artifact digest: `sha256:b2b9f0ca8528f1d6135d03c77ac964f8799ffc8989cc1a03515a6d5c7c73c1a4`.
- Prior workspace: `C:\w5a\phase2-31391119557-4`.
- Prior CMake cache digest: `b5c0606efa261a9525f5562eb973919d508f5242a567f82a46d0b0b9df725c77`.
- OpenVINO Runtime source commit remains `b9a1f201c109e0bed74763934f79483cf6c4cbf4`.
- OpenVINO GenAI source commit remains `bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0`.
- Preserve `--parallel 1` and `/p:CL_MPCount=1`.
- Preserve the `1.5 GiB` available-memory and `90%` Windows-commit safety limits.
- Use an eleven-hour internal deadline and a twelve-hour self-hosted job timeout.
- Keep all later scientific authorisation flags `false`.
- Do not alter WinUI production code, model files, prompts, rubrics, TurboQuant, PolarQuant, QJL or Route B.

---

### Task 1: Add failing controlled-resume regressions

**Files:**
- Create: `tests/testing/workbook05/test_route_a_runtime_resume.py`
- Create: `tests/testing/workbook05/test_route_a_runtime_resume_workflow.py`
- Create: `tests/testing/workbook05/Invoke-BuildProcessDeadlineTests.Tests.ps1`
- Modify: `tests/testing/workbook05/test_build_powershell_contract.py`

**Interfaces:**
- Consumes: the current documented-build workflow, shared build module and existing Runtime orchestration conventions.
- Produces: focused RED tests that define the prior-bundle validator, resume workflow, process deadline and resume-orchestrator boundaries.

- [ ] **Step 1: Add the timeout-bundle validator tests**

Create `test_route_a_runtime_resume.py`. Build a synthetic bundle in `tempfile.TemporaryDirectory()` with these exact files:

```text
bundle.json
environment.json
source-provenance.json
cmake-cache-summary.json
dependencies.json
commands/route-a-runtime-configure.command.json
commands/route-a-runtime-configure.stdout.log
commands/route-a-runtime-configure.stderr.log
commands/route-a-runtime-configure.resources.json
commands/route-a-runtime-build.resources.csv
manifest.sha256
```

Use exact identities from the approved attempt-4 boundary. Generate the manifest after all other files. Add tests requiring:

```python
result = validate_runtime_resume_bundle(
    bundle_root,
    expected_run_id="31391119557",
    expected_run_attempt=4,
    expected_workspace=r"C:\w5a\phase2-31391119557-4",
    expected_cache_sha256=(
        "b5c0606efa261a9525f5562eb973919d508f5242a567f82a46d0b0b9df725c77"
    ),
)
self.assertTrue(result.valid)
```

Add independent rejection tests for:

- changed manifest-covered file;
- wrong run attempt;
- wrong workspace path;
- wrong cache digest;
- unexpected `decision.json`;
- unexpected completed build-command record;
- forbidden binary suffix;
- secret-pattern content;
- empty build resource trace.

The import should intentionally fail because `route_a_runtime_resume_bundle_validation.py` does not exist yet.

- [ ] **Step 2: Add the dedicated workflow contract**

Create `test_route_a_runtime_resume_workflow.py` and require:

```text
.github/workflows/workbook-05-runtime-resume.yml
workflow_dispatch
contents: read
actions: read
cancel-in-progress: false
runs-on: [self-hosted, Windows, X64, workbook05, intel-target]
timeout-minutes: 720
31391119557
workbook-05-build-route-a-runtime-31391119557-4
sha256:b2b9f0ca8528f1d6135d03c77ac964f8799ffc8989cc1a03515a6d5c7c73c1a4
C:\w5a\phase2-31391119557-4
b5c0606efa261a9525f5562eb973919d508f5242a567f82a46d0b0b9df725c77
actions/download-artifact@fa0a91b85d4f404e444e00e005971372dc801d16
github-token: ${{ secrets.GITHUB_TOKEN }}
run-id: ${{ inputs.resume_run_id }}
Assert-Workbook05ArtifactIdentity.ps1
Invoke-Workbook05RouteARuntimeResume.ps1
Write-Workbook05BuildBundleMetadata.ps1
build_bundle_validation
```

Require the collection job to be manual/same-repository only and the hosted validator to consume only the new same-attempt artifact.

- [ ] **Step 3: Add static resume-orchestrator and deadline contracts**

Extend `test_build_powershell_contract.py` with constants for:

```python
RUNTIME_RESUME_SCRIPT = (
    REPOSITORY_ROOT
    / "scripts/testing/workbook05/Invoke-Workbook05RouteARuntimeResume.ps1"
)
ARTIFACT_IDENTITY_SCRIPT = (
    REPOSITORY_ROOT
    / "scripts/testing/workbook05/Assert-Workbook05ArtifactIdentity.ps1"
)
```

Add `RouteARuntimeResumeContractTests` requiring the resume script to contain:

- exact Runtime source commit and repository;
- `C:\w5a` containment;
- `ReparsePoint` checks;
- active-process names `cmake`, `MSBuild`, `cl`, `ninja`, `vctip`;
- Python prior-bundle validation call;
- local `CMakeCache.txt` SHA-256 comparison;
- all reviewed cache values;
- `--parallel 1` and `/p:CL_MPCount=1`;
- `MaximumElapsedSeconds`;
- eleven-hour deadline calculation;
- install and exact ONNX/TensorFlow header checks;
- binary/dependency evidence;
- all scientific claim flags false;
- no `git reset --hard`, `git clean`, `Invoke-Expression`, model URL or model execution token.

Add shared-module expectations for optional `MaximumElapsedSeconds` propagation and the exact controlled-deadline reason.

Add artifact-identity script expectations for:

- exact-one artifact match;
- expiration rejection;
- digest equality;
- expected workflow head SHA;
- Actions REST API path;
- no token serialization.

- [ ] **Step 4: Add an executable process-deadline PowerShell regression**

Create `Invoke-BuildProcessDeadlineTests.Tests.ps1`. Import the real module, create a temporary evidence directory, and execute:

```powershell
$result = Invoke-Wb05LoggedProcess `
    -CommandId 'deadline-test' `
    -RouteId 'route-a-merged-openvino' `
    -Component 'runtime' `
    -FilePath "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" `
    -ArgumentList @('-NoLogo', '-NoProfile', '-Command', 'Start-Sleep -Seconds 30') `
    -WorkingDirectory $temporaryRoot `
    -EvidenceDirectory $evidenceDirectory `
    -EvidenceRoot $temporaryRoot `
    -MonitorResources `
    -MaximumElapsedSeconds 3
```

Require:

```powershell
$result.resource_summary.safety_stop_triggered -eq $true
$result.resource_summary.safety_stop_reason -match 'controlled elapsed-time boundary'
$result.record.exit_code -ne 0
Test-Path $result.record_path
Test-Path $result.stdout_path
Test-Path $result.stderr_path
```

Delete only the temporary test directory in `finally` and unload the module.

- [ ] **Step 5: Commit the RED tests**

Commit message:

```text
test: define controlled Runtime resume boundary
```

- [ ] **Step 6: Verify RED in GitHub Actions**

The Workbook 05 gate should fail because the new validator, workflow, scripts and process-deadline parameter are absent. Confirm failures are limited to the new expectations before implementing production code.

---

### Task 2: Implement strict prior-artifact identity and timeout-bundle validation

**Files:**
- Create: `scripts/testing/workbook05/Assert-Workbook05ArtifactIdentity.ps1`
- Create: `scripts/testing/workbook05/route_a_runtime_resume_bundle_validation.py`
- Test: `tests/testing/workbook05/test_route_a_runtime_resume.py`
- Test: `tests/testing/workbook05/test_build_powershell_contract.py`

**Interfaces:**
- Consumes: GitHub Actions REST metadata and an extracted attempt-4 artifact directory.
- Produces: a verified artifact metadata boundary and `ValidationResult` for a specific incomplete Runtime timeout bundle.

- [ ] **Step 1: Implement the artifact metadata boundary**

Create `Assert-Workbook05ArtifactIdentity.ps1` with mandatory parameters:

```powershell
[string]$Repository
[string]$RunId
[int]$RunAttempt
[string]$ArtifactName
[ValidatePattern('^sha256:[0-9a-f]{64}$')][string]$ExpectedArtifactDigest
[ValidatePattern('^sha256:[0-9a-f]{64}$')][string]$ActualArtifactDigest
[ValidatePattern('^[0-9a-f]{40}$')][string]$ExpectedHeadSha
[string]$GitHubToken
```

Validate the repository as `owner/name`, run ID as digits, positive attempt and non-empty artifact name. Reject unequal expected/actual digests before making a request. Query:

```text
https://api.github.com/repos/<repository>/actions/runs/<run-id>/artifacts?per_page=100
```

Use `Authorization: Bearer <token>`, `Accept: application/vnd.github+json` and `X-GitHub-Api-Version: 2022-11-28`. Require one exact artifact name, `expired = false`, matching digest and matching `workflow_run.head_sha`. Print only a stable success marker and non-secret artifact metadata.

- [ ] **Step 2: Implement the untrusted timeout-bundle validator**

Create `route_a_runtime_resume_bundle_validation.py` with:

```python
@dataclass(frozen=True)
class ValidationResult:
    valid: bool
    file_count: int
    manifest_entry_count: int
    reasons: tuple[str, ...]
```

Expose:

```python
def validate_runtime_resume_bundle(
    bundle_root: Path,
    *,
    expected_run_id: str,
    expected_run_attempt: int,
    expected_workspace: str,
    expected_cache_sha256: str,
) -> ValidationResult:
```

Implement strict safe-path, manifest, SHA-256, text-only, size and secret checks. Require the exact Runtime timeout shape described in the design. Reject `decision.json`, `integrity-failure.json` and `commands/route-a-runtime-build.command.json`. Require a non-empty build-resource CSV.

Add a CLI accepting:

```text
--bundle-root
--expected-run-id
--expected-run-attempt
--expected-workspace
--expected-cache-sha256
--report
```

Write a deterministic Markdown report and return exit `0` only for a valid prerequisite.

- [ ] **Step 3: Run focused tests**

Run:

```powershell
python -m unittest -v `
  tests.testing.workbook05.test_route_a_runtime_resume
```

Expected: all timeout-bundle validator tests pass.

- [ ] **Step 4: Commit**

Commit message:

```text
feat: validate Runtime resume prerequisite evidence
```

---

### Task 3: Add the controlled native-process deadline

**Files:**
- Modify: `scripts/testing/workbook05/Workbook05.Build.psm1`
- Test: `tests/testing/workbook05/Invoke-BuildProcessDeadlineTests.Tests.ps1`
- Test: `tests/testing/workbook05/test_build_powershell_contract.py`

**Interfaces:**
- Consumes: existing monitored native process execution.
- Produces: optional `MaximumElapsedSeconds` propagation into the resource sampler and a normal resource-summary stop rather than an external job cancellation.

- [ ] **Step 1: Extend `Start-Wb05ResourceSampler`**

Add:

```powershell
[int]$MaximumElapsedSeconds = 0
```

Pass it into the background job and record the sampler start time. During each sample, after memory/commit checks and before sleeping, enforce:

```powershell
elseif (
    $MaximumElapsedSeconds -gt 0 -and
    (([DateTime]::UtcNow - $startedUtc).TotalSeconds -ge $MaximumElapsedSeconds)
) {
    $safetyStopTriggered = $true
    $safetyStopReason = (
        "The native command reached the controlled elapsed-time boundary of " +
        "$MaximumElapsedSeconds seconds."
    )
}
```

Keep the existing descendant-first termination order.

- [ ] **Step 2: Extend `Invoke-Wb05LoggedProcess`**

Add optional:

```powershell
[int]$MaximumElapsedSeconds = 0
```

Reject negative values and require `MonitorResources` when a positive value is supplied. Forward it to `Start-Wb05ResourceSampler`.

- [ ] **Step 3: Run executable PowerShell regression**

Run:

```powershell
& '.\tests\testing\workbook05\Invoke-BuildProcessDeadlineTests.Tests.ps1'
```

Expected: the child is stopped by the module, complete command/log/resource evidence exists and the test prints a pass marker.

- [ ] **Step 4: Run existing PowerShell tests**

Run all `*.Tests.ps1` files under `tests/testing/workbook05`. Expected: no regression in cache parsing, JSON writing or Windows path comparison.

- [ ] **Step 5: Commit**

Commit message:

```text
feat: add controlled native-process deadline
```

---

### Task 4: Implement the fail-closed Runtime resume orchestrator

**Files:**
- Create: `scripts/testing/workbook05/Invoke-Workbook05RouteARuntimeResume.ps1`
- Test: `tests/testing/workbook05/test_build_powershell_contract.py`

**Interfaces:**
- Consumes: validated prior bundle, exact local workspace, shared process adapter, CMake, Git and the existing build schemas.
- Produces: new Runtime build evidence and one normal decision or one integrity-failure record.

- [ ] **Step 1: Define exact parameters and constants**

Mandatory parameters:

```powershell
[string]$RepositoryRoot
[string]$OutputDirectory
[string]$ResumeBundleDirectory
[string]$ResumeWorkspaceDirectory
[string]$ResumeRunId
[int]$ResumeRunAttempt
[string]$ResumeArtifactName
[ValidatePattern('^sha256:[0-9a-f]{64}$')][string]$ResumeExpectedArtifactDigest
[ValidatePattern('^sha256:[0-9a-f]{64}$')][string]$ResumeActualArtifactDigest
[ValidatePattern('^[0-9a-f]{64}$')][string]$ExpectedCacheSha256
[int]$InternalDeadlineSeconds = 39600
[string]$PythonPath = 'C:\Program Files\Python312\python.exe'
```

Pin the same Runtime source, build document, generator, tool paths and required frontend headers as the fresh script.

- [ ] **Step 2: Add normal-directory and process-state checks**

Implement a local helper that resolves every existing directory under `C:\w5a`, walks each path component and rejects `ReparsePoint`. Require the workspace, `ov` and `b-ov` directories. Permit `i-ov` only when absent or empty.

Before validation/build, reject any active process named:

```text
cmake
MSBuild
cl
ninja
vctip
```

Do not terminate unrelated processes automatically.

- [ ] **Step 3: Validate the prior artifact and record the resume prerequisite**

Run the Python validator before any Git or CMake command. Parse its exact bundle/environment/provenance/cache records. Require supplied digest equality. Write `resume-prerequisite.json` and the validator Markdown report into the new evidence directory.

- [ ] **Step 4: Requalify source and cache**

Use current logged Git commands to verify remote, HEAD, clean status and recursive submodules. Recalculate the build-document hash. Recalculate local `CMakeCache.txt` SHA-256 and parse all reviewed values with `Get-Wb05CMakeCacheValue`. Require exact agreement with both the previous artifact and explicit expected cache digest.

Write current `environment.json`, `source-provenance.json`, `cmake-cache-summary.json` and regenerated TBB `dependencies.json`.

- [ ] **Step 5: Resume build within the internal deadline**

Set:

```powershell
$deadlineUtc = [DateTime]::UtcNow.AddSeconds($InternalDeadlineSeconds)
```

Before each monitored native command, calculate positive remaining whole seconds. Execute the existing incremental build command with `MaximumElapsedSeconds` equal to the remaining budget.

Classify a resource or elapsed-time stop as `Infrastructure interrupted`, a non-zero native exit as `Failed`, and continue only after zero exit.

- [ ] **Step 6: Install and verify the hand-off**

Install to the same workspace `i-ov` directory using the remaining internal budget. Require both exact frontend headers, collect current binary records in place and require at least one binary.

Write `decision.json` through the same schema shape as the fresh Runtime path and keep every later authorisation `false`.

- [ ] **Step 7: Preserve integrity failures**

Wrap the orchestrator in `try/catch`. On an integrity exception, write `integrity-failure.json`, all authorisation flags false and a manifest, then rethrow so the workflow is red while retaining evidence.

- [ ] **Step 8: Run focused static tests**

Run:

```powershell
python -m unittest -v `
  tests.testing.workbook05.test_build_powershell_contract.RouteARuntimeResumeContractTests
```

Expected: pass.

- [ ] **Step 9: Commit**

Commit message:

```text
feat: resume the exact Route A Runtime workspace
```

---

### Task 5: Add the dedicated controlled-resume workflow

**Files:**
- Create: `.github/workflows/workbook-05-runtime-resume.yml`
- Test: `tests/testing/workbook05/test_route_a_runtime_resume_workflow.py`

**Interfaces:**
- Consumes: exact previous artifact inputs and local workspace.
- Produces: a new same-attempt Runtime evidence artifact followed by independent hosted validation.

- [ ] **Step 1: Define triggers, inputs and permissions**

Add pull-request path triggers for the new workflow, build scripts, tests, schemas and plan/spec documents. Add `workflow_dispatch` defaults for every exact attempt-4 identity listed in Global Constraints. Keep:

```yaml
permissions:
  contents: read
  actions: read
```

Use a unique concurrency group and `cancel-in-progress: false`.

- [ ] **Step 2: Add hosted repository verification**

The first job runs on `windows-latest`, checks out the exact PR head or dispatch SHA with the existing sparse-checkout family, sets up Python 3.12.10 and runs `Validate-Workbook05-BuildStage.ps1`.

- [ ] **Step 3: Add the self-hosted resume collector**

Guard it with manual dispatch and exact repository identity. Use exact Lenovo labels and `timeout-minutes: 720`.

Steps:

1. checkout exact controls with `persist-credentials: false`;
2. rerun the repository gate on the Lenovo;
3. verify prior artifact metadata with `Assert-Workbook05ArtifactIdentity.ps1` using `${{ secrets.GITHUB_TOKEN }}` only in the step environment;
4. download the exact prior artifact with pinned `actions/download-artifact`, `github-token`, repository and prior run ID;
5. execute `Invoke-Workbook05RouteARuntimeResume.ps1`;
6. bind the new bundle metadata to the new run/attempt with `Write-Workbook05BuildBundleMetadata.ps1` under `if: always()`;
7. upload only the new text evidence under `if: always()` with 30-day retention.

- [ ] **Step 4: Add independent hosted validation**

A final `windows-latest` job runs under `if: always()` after the collector, downloads the new same-attempt artifact, reruns the repository gate, and validates it with the ordinary `build_bundle_validation` module as Route A Runtime.

- [ ] **Step 5: Run workflow contract test**

Run:

```powershell
python -m unittest -v `
  tests.testing.workbook05.test_route_a_runtime_resume_workflow
```

Expected: pass.

- [ ] **Step 6: Commit**

Commit message:

```text
ci: add controlled Route A Runtime resume workflow
```

---

### Task 6: Complete verification and prepare the pull request

**Files:**
- Verify: every file changed by Tasks 1–5.
- Update: pull-request description only after exact evidence is available.

**Interfaces:**
- Consumes: final branch head.
- Produces: exact repository and application regression evidence plus a reviewable PR.

- [ ] **Step 1: Run the complete Workbook 05 gate**

Run:

```powershell
& '.\scripts\testing\Validate-Workbook05-BuildStage.ps1' `
    -RepositoryRoot (Get-Location).Path `
    -PythonPath 'python'
```

Require every Python test, every PowerShell executable test, module import/export checks, focused workflow contracts and `git diff --check` to pass, ending with:

```text
WORKBOOK05_BUILD_STAGE_GATE_PASS
```

- [ ] **Step 2: Run the normal application build and tests**

Use the existing `Build and test` GitHub workflow. Require all `134/134` packaged application tests to pass and independently verify the retained ZIP digest and TRX totals.

- [ ] **Step 3: Review scope and security**

Confirm no changes to WinUI production code, source pins, model files, prompts, metrics, quality rubrics, TurboQuant, PolarQuant, QJL, Route B, machine policy or resource thresholds.

Confirm the workflow token is never passed to artifact evidence or native command arguments.

- [ ] **Step 4: Open a detailed draft PR**

The PR body must include:

- the attempt-4 timeout evidence;
- exact prior artifact and cache digests;
- why timeout is the first causal failure;
- controlled-resume architecture;
- RED and GREEN run IDs;
- complete Workbook 05 and application test results;
- explicit non-claims;
- exact live post-merge dispatch inputs.

- [ ] **Step 5: Request user approval before merge**

Do not merge automatically. Present the exact verified head and wait for the user’s explicit merge instruction.
