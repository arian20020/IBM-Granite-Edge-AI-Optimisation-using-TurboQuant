# Workbook 05 Route A Runtime Single-Job Experiment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Change only Route A Runtime build concurrency from two jobs to one, keep the recorded evidence consistent with the executed command, and verify the change before another Lenovo OpenVINO build.

**Architecture:** Preserve the existing Route A Runtime orchestration, resource sampler, source pins, CMake flags, evidence bundle, and independent validator. Update the existing Runtime build contract first so it fails against the current `parallelism = 2` / `--parallel 2` implementation, then change exactly those two Runtime values to `1` and rerun the complete repository/application verification stack.

**Tech Stack:** Windows PowerShell 5.1, Python 3.12 `unittest`, CMake, Visual Studio 17 2022 generator, GitHub Actions, self-hosted Windows x64 runner.

## Global Constraints

- Route A Runtime source remains `openvinotoolkit/openvino@b9a1f201c109e0bed74763934f79483cf6c4cbf4`.
- Route A GenAI source remains `openvinotoolkit/openvino.genai@05e5c7670b597746f858946974d11f38e3baf42f`.
- Only Route A Runtime build concurrency changes: `2 -> 1`.
- `environment.json` must record `parallelism = 1` and the real build command must execute `--parallel 1`; the two values must agree.
- Route A GenAI parallelism is unchanged.
- Windows page-file configuration is unchanged.
- Resource safety controls remain unchanged, including the `90%` maximum commit boundary and five consecutive safety samples.
- CPU-only CMake controls, Python 3.12.10, Release configuration, generator/platform, short workspace roots, install roots, and source/build/install separation remain unchanged.
- Workflow permissions, immutable action SHAs, self-hosted labels/timeouts, text/data-only evidence boundary, and hosted validator remain unchanged.
- No model execution, Granite inference, TurboQuant/QJL/PolarQuant activation, packed-storage, no-fallback, memory/context, TTFT, throughput, or quality claim is authorised.
- New/changed test and production logic includes beginner-readable comments around the changed logical block.

---

### Task 1: RED contract for Runtime single-job concurrency

**Files:**
- Modify: `tests/testing/workbook05/test_build_powershell_contract.py`
- Test: `tests/testing/workbook05/test_build_powershell_contract.py`

**Interfaces:**
- Consumes: text of `scripts/testing/workbook05/Invoke-Workbook05RouteARuntimeBuild.ps1`.
- Produces: a repository contract requiring the Runtime evidence metadata and actual CMake build command to both use single-job concurrency.

- [ ] **Step 1: Update the existing Runtime contract before production changes**

Inside `RouteARuntimeBuildContractTests.test_pins_source_toolchain_paths_and_configure_controls`, replace the old Runtime parallelism token and add explicit evidence/anti-regression checks:

```python
        required = (
            "https://github.com/openvinotoolkit/openvino.git",
            "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
            "docs/dev/build_windows.md",
            "C:\\w5a",
            "Visual Studio 17 2022",
            "Python 3.12.10",
            "Join-Path $workspace.work_directory 'ov'",
            "Join-Path $workspace.work_directory 'b-ov'",
            "Join-Path $workspace.work_directory 'i-ov'",
            "'-G', $Generator",
            "'-A', 'x64'",
            "'-DENABLE_INTEL_GPU=OFF'",
            "'-DENABLE_INTEL_NPU=OFF'",
            "'-DENABLE_TESTS=OFF'",
            "'-DENABLE_FUNCTIONAL_TESTS=OFF'",
            "'-DENABLE_SAMPLES=ON'",
            "'-DENABLE_PYTHON=ON'",
            "'-DENABLE_WHEEL=OFF'",
            "parallelism = 1",
            "'--parallel', '1'",
        )
        for token in required:
            with self.subTest(token=token):
                self.assertIn(token, self.text)

        # The Runtime experiment must not silently keep the pre-experiment
        # concurrency in either evidence metadata or the executed build command.
        self.assertNotIn("parallelism = 2", self.text)
        self.assertNotIn("'--parallel', '2'", self.text)
```

Do not change the GenAI test, because GenAI is not part of this experiment.

- [ ] **Step 2: Commit the RED-only contract**

Commit message:

```text
test: require single-job Route A Runtime build
```

- [ ] **Step 3: Open a draft PR only to trigger CI**

The PR must contain the approved spec, plan, and RED-only test change. No Runtime production file may have changed yet.

- [ ] **Step 4: Verify RED on the exact test-only head**

Required failure shape:

```text
RouteARuntimeBuildContractTests.test_pins_source_toolchain_paths_and_configure_controls
```

Expected reason: the current Runtime script still contains `parallelism = 2` and `--parallel 2`, so the new required `1` values are absent and/or the explicit anti-regression checks fail.

Reject the RED checkpoint if checkout, dependency setup, syntax, import, or unrelated tests fail first.

---

### Task 2: Minimal Runtime production change and GREEN verification

**Files:**
- Modify: `scripts/testing/workbook05/Invoke-Workbook05RouteARuntimeBuild.ps1`
- Test: `tests/testing/workbook05/test_build_powershell_contract.py`

**Interfaces:**
- Consumes: the RED contract from Task 1.
- Produces: Route A Runtime evidence metadata that records single-job build concurrency and an actual CMake build invocation capped at one job.

- [ ] **Step 1: Change the recorded Runtime experiment metadata**

In the `environment.json` producer, change only:

```powershell
parallelism = 2
```

to:

```powershell
# Record the exact single-job control used by the follow-up memory experiment.
parallelism = 1
```

- [ ] **Step 2: Change the actual Runtime build command**

Change only the Runtime build arguments from:

```powershell
'--parallel', '2'
```

to:

```powershell
# Limit Runtime compilation to one concurrent build job so this experiment
# changes only concurrency while the existing resource-safety boundary stays fixed.
'--parallel', '1'
```

Keep `--verbose`, Release configuration, source/build/install roots, resource monitoring, and all configure/install behavior unchanged.

- [ ] **Step 3: Review the exact production diff before accepting CI**

Expected production diff:

```text
scripts/testing/workbook05/Invoke-Workbook05RouteARuntimeBuild.ps1
- parallelism = 2
+ parallelism = 1
- '--parallel', '2'
+ '--parallel', '1'
```

Comments around those lines may be adjusted for beginner clarity, but no other production behavior may change.

- [ ] **Step 4: Verify GREEN on the exact final branch head**

Require:

```text
complete Workbook 05 discovery: all pass
Route A Runtime single-job contract: pass
focused workflow/security contracts: all pass
PowerShell regression scripts: all pass
git diff --check: pass
WORKBOOK05_BUILD_STAGE_GATE_PASS
```

- [ ] **Step 5: Verify the normal WinUI/application workflow on the same exact head**

Require application restore/build success, unit-test build success, zero build errors, and the current legitimate packaged VSTest total (`134`) all passing.

- [ ] **Step 6: Independently verify the uploaded unit-test artifact**

Download the artifact, compute SHA-256 independently, compare it with GitHub's recorded digest, and independently parse the TRX counters. Required result: all executed tests passed with zero failures/errors/aborts/timeouts/not-executed.

---

### Task 3: PR integration, post-merge acceptance, and next Runtime experiment boundary

**Files:**
- No additional production files unless exact verification exposes a defect caused by this change.
- Update PR body with the complete audit trail.

**Interfaces:**
- Consumes: exact verified branch head from Task 2.
- Produces: a post-merge `main` checkpoint that authorises one new `route-a-runtime` dispatch using `--parallel 1`.

- [ ] **Step 1: Review the complete PR diff against base `e4624417f6824a31a04ab93a4a16728fe2060833`**

Reject any drift in OpenVINO/GenAI pins, CMake configure flags, page-file/safety controls, GenAI build controls, Route B/BR8 rules, workflow security, validator behavior, or application production source.

- [ ] **Step 2: Replace the draft PR body with a detailed audit record**

Include:

```text
motivating run: 31231872859
motivating artifact ID: 9014875921
motivating artifact SHA-256: 4771115658d3f326321c2792af6b53c7d16b4086a25e88cb53db240ffe383805
previous Runtime outcome: Infrastructure interrupted
previous maximum commit usage: 92%
experimental variable: Runtime parallelism 2 -> 1
page-file change: none
resource-safety change: none
RED head/run evidence
GREEN head/run evidence
WinUI 134/134 evidence
independent artifact digest/TRX evidence
exact changed files
scientific non-claims
```

- [ ] **Step 3: Mark the PR ready and verify mergeability/review threads**

Require exact verified head, mergeable status, and zero unresolved review threads.

- [ ] **Step 4: Merge only the verified head with expected-head protection**

Use a merge commit so the experiment design, RED contract, and GREEN implementation remain auditable.

- [ ] **Step 5: Verify fresh `main` after merge**

Require a new push-triggered `Build and test` on the merge SHA, then independently verify its uploaded unit-test artifact digest and TRX counters again.

- [ ] **Step 6: Authorise exactly one new manual Route A Runtime dispatch**

The new workflow must use:

```text
branch: main
stage: route-a-runtime
run_identity: phase2
```

Do not rerun workflow `31231872859`, because it is bound to the pre-experiment `--parallel 2` commit.

- [ ] **Step 7: Interpret the new live result before choosing another variable**

If Runtime reaches `Passed`, proceed to Route A GenAI only after independent evidence acceptance. If Runtime is again `Infrastructure interrupted`, compare its resource summary with run `31231872859` before considering any page-file experiment. If a normal compiler/build error occurs first, debug that new first divergence without changing the safety boundary.

## Plan Self-Review

- Spec coverage: the one-variable concurrency change, evidence/execution consistency, TDD RED, GREEN verification, merge, post-merge verification, and next live-run boundary are all mapped to tasks.
- Placeholder scan: no TODO/TBD or unspecified implementation step remains.
- Type/value consistency: Route A Runtime concurrency is consistently defined as integer/text value `1` in both the evidence producer and CMake command; Route A GenAI remains unchanged.
- Scope: one Runtime build-control experiment; no unrelated refactoring or machine configuration change is included.
