# Workbook 05 Route A Runtime Narrow-Frontend Repair Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Narrow only the Route A OpenVINO Runtime source build to the CPU + OpenVINO IR surface required by the current Runtime-to-GenAI path, preserve Windows App Control and all existing safety controls, and prove the materialized CMake cache before another Lenovo build is allowed.

**Architecture:** Keep the existing Runtime orchestration, exact upstream pins, Windows PowerShell evidence producer, one-job CMake build, resource sampler, install layout, manifest, and independent validator. First change the existing Python contract so current production fails RED for the missing CPU/IR requirements and still-enabled unused frontend/sample surface. Then minimally change the Runtime configure argument array, the generated-cache evidence list, and the fail-closed `$cacheMatches` expression so requested and materialized controls agree.

**Tech Stack:** Windows PowerShell 5.1, Python 3.12 `unittest`, CMake, Visual Studio 17 2022 generator, GitHub Actions, self-hosted Windows x64 runner, OpenVINO Runtime source build.

## Global Constraints

- Route A Runtime source remains exactly `openvinotoolkit/openvino@b9a1f201c109e0bed74763934f79483cf6c4cbf4`.
- Route A GenAI source remains exactly `openvinotoolkit/openvino.genai@05e5c7670b597746f858946974d11f38e3baf42f`.
- The approved Runtime surface must explicitly require `ENABLE_INTEL_CPU=ON` and `ENABLE_OV_IR_FRONTEND=ON`.
- The approved unused framework frontend surface must explicitly be `OFF`: `ENABLE_OV_ONNX_FRONTEND`, `ENABLE_OV_PADDLE_FRONTEND`, `ENABLE_OV_TF_FRONTEND`, `ENABLE_OV_TF_LITE_FRONTEND`, `ENABLE_OV_PYTORCH_FRONTEND`, and `ENABLE_OV_JAX_FRONTEND`.
- `ENABLE_SAMPLES=OFF` and `ENABLE_JS=OFF` are required by the approved least-functionality design.
- `ENABLE_PYTHON=ON` and `ENABLE_WHEEL=OFF` remain unchanged for this repair.
- `ENABLE_INTEL_GPU=OFF`, `ENABLE_INTEL_NPU=OFF`, `ENABLE_TESTS=OFF`, and `ENABLE_FUNCTIONAL_TESTS=OFF` remain unchanged.
- Runtime build concurrency remains `parallelism = 1` / `--parallel 1`.
- The `90%` maximum Windows commit boundary, five-consecutive-sample rule, and all other resource-safety behavior remain unchanged.
- The exact Visual Studio generator, x64 platform, Release configuration, Python 3.12.10 path/version, short `C:\w5a` workspace, source/build/install separation, source provenance checks, TBB evidence, binary hashing, decision classifications, and hosted independent validator remain unchanged.
- Device Guard / Smart App Control / App Control for Business must not be disabled, weakened, or modified by this repair.
- `ENABLE_SYSTEM_PROTOBUF` is only a secondary evidence value. If CMake materializes it, `ON` must block the build; if the key is absent, absence alone must not block once the direct frontend controls match.
- No additional AUTO/MULTI/HETERO/AUTO_BATCH/TEMPLATE plugin controls are changed.
- Route A GenAI, Route B, BR8, action pins, workflow permissions, runner labels/timeouts, and artifact trust boundaries are unchanged.
- No Granite inference, QJL/PolarQuant/TurboQuant activation, packed-storage, no-fallback, memory/context, TTFT, throughput, or quality claim is authorized by this repair.
- New or changed test/production logic must retain beginner-readable comments around each logical block.

---

### Task 1: RED contract for the reviewed narrow Runtime surface

**Files:**
- Modify: `tests/testing/workbook05/test_build_powershell_contract.py`
- Test: `tests/testing/workbook05/test_build_powershell_contract.py`

**Interfaces:**
- Consumes: source text from `scripts/testing/workbook05/Invoke-Workbook05RouteARuntimeBuild.ps1`.
- Produces: a static regression contract requiring both the requested CMake flags and the generated-cache verification policy to encode the approved CPU/IR-only Runtime surface.

- [ ] **Step 1: Replace the old broad Runtime configure expectations before changing production**

Inside `RouteARuntimeBuildContractTests.test_pins_source_toolchain_paths_and_configure_controls`, replace the old `ENABLE_SAMPLES=ON` expectation and add the approved configure flags:

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
            "'-DENABLE_INTEL_CPU=ON'",
            "'-DENABLE_OV_IR_FRONTEND=ON'",
            "'-DENABLE_OV_ONNX_FRONTEND=OFF'",
            "'-DENABLE_OV_PADDLE_FRONTEND=OFF'",
            "'-DENABLE_OV_TF_FRONTEND=OFF'",
            "'-DENABLE_OV_TF_LITE_FRONTEND=OFF'",
            "'-DENABLE_OV_PYTORCH_FRONTEND=OFF'",
            "'-DENABLE_OV_JAX_FRONTEND=OFF'",
            "'-DENABLE_INTEL_GPU=OFF'",
            "'-DENABLE_INTEL_NPU=OFF'",
            "'-DENABLE_TESTS=OFF'",
            "'-DENABLE_FUNCTIONAL_TESTS=OFF'",
            "'-DENABLE_SAMPLES=OFF'",
            "'-DENABLE_JS=OFF'",
            "'-DENABLE_PYTHON=ON'",
            "'-DENABLE_WHEEL=OFF'",
            "parallelism = 1",
            "'--parallel', '1'",
        )
        for token in required:
            with self.subTest(token=token):
                self.assertIn(token, self.text)

        # The narrow Runtime experiment must not accidentally restore the broad
        # sample build or the previous two-job concurrency.
        self.assertNotIn("'-DENABLE_SAMPLES=ON'", self.text)
        self.assertNotIn("parallelism = 2", self.text)
        self.assertNotIn("'--parallel', '2'", self.text)
```

- [ ] **Step 2: Add a focused generated-cache verification contract**

Add a separate test method so the repository requires the producer to verify materialized CMake state rather than only requested command-line intent:

```python
    def test_requires_materialized_narrow_runtime_cache_controls(self) -> None:
        required_cache_checks = (
            "$cacheValues.ENABLE_INTEL_CPU -eq 'ON'",
            "$cacheValues.ENABLE_OV_IR_FRONTEND -eq 'ON'",
            "$cacheValues.ENABLE_OV_ONNX_FRONTEND -eq 'OFF'",
            "$cacheValues.ENABLE_OV_PADDLE_FRONTEND -eq 'OFF'",
            "$cacheValues.ENABLE_OV_TF_FRONTEND -eq 'OFF'",
            "$cacheValues.ENABLE_OV_TF_LITE_FRONTEND -eq 'OFF'",
            "$cacheValues.ENABLE_OV_PYTORCH_FRONTEND -eq 'OFF'",
            "$cacheValues.ENABLE_OV_JAX_FRONTEND -eq 'OFF'",
            "$cacheValues.ENABLE_INTEL_GPU -eq 'OFF'",
            "$cacheValues.ENABLE_INTEL_NPU -eq 'OFF'",
            "$cacheValues.ENABLE_TESTS -eq 'OFF'",
            "$cacheValues.ENABLE_FUNCTIONAL_TESTS -eq 'OFF'",
            "$cacheValues.ENABLE_SAMPLES -eq 'OFF'",
            "$cacheValues.ENABLE_JS -eq 'OFF'",
            "$cacheValues.ENABLE_PYTHON -eq 'ON'",
            "$cacheValues.ENABLE_WHEEL -eq 'OFF'",
            "$null -eq $cacheValues.ENABLE_SYSTEM_PROTOBUF",
            "$cacheValues.ENABLE_SYSTEM_PROTOBUF -eq 'OFF'",
        )
        for token in required_cache_checks:
            with self.subTest(token=token):
                self.assertIn(token, self.text)
```

The two Protobuf tokens intentionally require an optional-value policy of **missing OR OFF**, not a brittle requirement that the key must exist.

- [ ] **Step 3: Commit the RED-only contract**

Commit message:

```text
test: require narrow Route A Runtime frontend surface
```

At this point the branch may contain the approved design, this implementation plan, and the RED test change only. `Invoke-Workbook05RouteARuntimeBuild.ps1` must still match the previous broad configuration.

- [ ] **Step 4: Open a draft PR only to trigger the repository CI gates**

The draft PR body must explicitly state that the production repair is not present yet and that failure is expected only in the new narrow-Runtime contract.

- [ ] **Step 5: Verify RED on the exact test-only head**

Expected failure:

```text
RouteARuntimeBuildContractTests.test_pins_source_toolchain_paths_and_configure_controls
and/or
RouteARuntimeBuildContractTests.test_requires_materialized_narrow_runtime_cache_controls
```

Expected reason: current production still has `-DENABLE_SAMPLES=ON`, does not explicitly require CPU/IR, does not explicitly disable the six framework frontends/JavaScript, and does not verify those materialized cache values.

Reject the RED checkpoint if checkout, syntax, dependency setup, test discovery, or an unrelated test fails first.

---

### Task 2: Minimal Runtime configure and cache-policy implementation

**Files:**
- Modify: `scripts/testing/workbook05/Invoke-Workbook05RouteARuntimeBuild.ps1`
- Test: `tests/testing/workbook05/test_build_powershell_contract.py`

**Interfaces:**
- Consumes: the failing narrow-Runtime contract from Task 1 and the existing shared `Get-Wb05CMakeCacheValue` parser.
- Produces: a CMake request and post-configure cache contract for the reviewed CPU/IR Runtime surface, while preserving the rest of the Runtime build/evidence pipeline.

- [ ] **Step 1: Replace only the Runtime configure argument block with the approved surface**

The final configure argument block must be:

```powershell
    # Configure only the reviewed CPU + OpenVINO IR Runtime surface. Optional
    # framework frontends that are not used by this Route A hand-off stay off so
    # the build does not create unnecessary executable/dependency surface.
    $configureArguments = @(
        '-S', $sourceRoot,
        '-B', $buildRoot,
        '-G', $Generator,
        '-A', 'x64',
        '-DCMAKE_BUILD_TYPE=Release',
        '-DENABLE_INTEL_CPU=ON',
        '-DENABLE_OV_IR_FRONTEND=ON',
        '-DENABLE_OV_ONNX_FRONTEND=OFF',
        '-DENABLE_OV_PADDLE_FRONTEND=OFF',
        '-DENABLE_OV_TF_FRONTEND=OFF',
        '-DENABLE_OV_TF_LITE_FRONTEND=OFF',
        '-DENABLE_OV_PYTORCH_FRONTEND=OFF',
        '-DENABLE_OV_JAX_FRONTEND=OFF',
        '-DENABLE_INTEL_GPU=OFF',
        '-DENABLE_INTEL_NPU=OFF',
        '-DENABLE_TESTS=OFF',
        '-DENABLE_FUNCTIONAL_TESTS=OFF',
        '-DENABLE_SAMPLES=OFF',
        '-DENABLE_JS=OFF',
        '-DENABLE_PYTHON=ON',
        '-DENABLE_WHEEL=OFF',
        "-DPython3_EXECUTABLE=$PythonPath"
    )
```

Do not change source acquisition, provenance verification, configure monitoring, or configure result classification.

- [ ] **Step 2: Extend the cache-summary key list to record the direct controls**

The cache loop must request at least:

```powershell
    foreach ($name in @(
        'CMAKE_GENERATOR',
        'CMAKE_GENERATOR_PLATFORM',
        'ENABLE_INTEL_CPU',
        'ENABLE_INTEL_GPU',
        'ENABLE_INTEL_NPU',
        'ENABLE_TESTS',
        'ENABLE_FUNCTIONAL_TESTS',
        'ENABLE_SAMPLES',
        'ENABLE_PYTHON',
        'ENABLE_WHEEL',
        'ENABLE_JS',
        'ENABLE_OV_IR_FRONTEND',
        'ENABLE_OV_ONNX_FRONTEND',
        'ENABLE_OV_PADDLE_FRONTEND',
        'ENABLE_OV_TF_FRONTEND',
        'ENABLE_OV_TF_LITE_FRONTEND',
        'ENABLE_OV_PYTORCH_FRONTEND',
        'ENABLE_OV_JAX_FRONTEND',
        'ENABLE_SYSTEM_PROTOBUF',
        'Python3_EXECUTABLE'
    )) {
        # Read each reviewed generated control through the shared parser. A
        # missing optional Protobuf key remains null; required controls are
        # rejected below if CMake did not materialize the reviewed value.
        $cacheValues[$name] = Get-Wb05CMakeCacheValue -Lines $cacheLines -Name $name
    }
```

Keep the existing `cmake-cache-summary.json` SHA-256 and `values` serialization unchanged.

- [ ] **Step 3: Extend the fail-closed `$cacheMatches` expression**

The final policy must be:

```powershell
    $cacheMatches = (
        $cacheValues.CMAKE_GENERATOR -eq $Generator -and
        $cacheValues.CMAKE_GENERATOR_PLATFORM -eq 'x64' -and
        $cacheValues.ENABLE_INTEL_CPU -eq 'ON' -and
        $cacheValues.ENABLE_INTEL_GPU -eq 'OFF' -and
        $cacheValues.ENABLE_INTEL_NPU -eq 'OFF' -and
        $cacheValues.ENABLE_TESTS -eq 'OFF' -and
        $cacheValues.ENABLE_FUNCTIONAL_TESTS -eq 'OFF' -and
        $cacheValues.ENABLE_SAMPLES -eq 'OFF' -and
        $cacheValues.ENABLE_PYTHON -eq 'ON' -and
        $cacheValues.ENABLE_WHEEL -eq 'OFF' -and
        $cacheValues.ENABLE_JS -eq 'OFF' -and
        $cacheValues.ENABLE_OV_IR_FRONTEND -eq 'ON' -and
        $cacheValues.ENABLE_OV_ONNX_FRONTEND -eq 'OFF' -and
        $cacheValues.ENABLE_OV_PADDLE_FRONTEND -eq 'OFF' -and
        $cacheValues.ENABLE_OV_TF_FRONTEND -eq 'OFF' -and
        $cacheValues.ENABLE_OV_TF_LITE_FRONTEND -eq 'OFF' -and
        $cacheValues.ENABLE_OV_PYTORCH_FRONTEND -eq 'OFF' -and
        $cacheValues.ENABLE_OV_JAX_FRONTEND -eq 'OFF' -and
        (
            $null -eq $cacheValues.ENABLE_SYSTEM_PROTOBUF -or
            $cacheValues.ENABLE_SYSTEM_PROTOBUF -eq 'OFF'
        )
    )
```

This deliberately fails closed for every direct reviewed frontend/CPU control while allowing the dependent `ENABLE_SYSTEM_PROTOBUF` key to be absent. If CMake emits the key as `ON`, the stage becomes `Blocked` before compilation.

- [ ] **Step 4: Preserve the existing blocked decision path**

Keep the existing decision behavior:

```powershell
    if (-not $cacheMatches) {
        Complete-RouteARuntimeEvidence `
            -Status 'Blocked' `
            -Reasons @('Generated Route A Runtime CMake cache does not match the reviewed CPU-only build controls.')
        return
    }
```

Do not reclassify cache mismatch as a compiler failure and do not weaken the validator.

- [ ] **Step 5: Review the exact production diff before accepting GREEN**

Expected production behavior changes are limited to:

```text
Runtime configure flags:
  + CPU ON
  + IR frontend ON
  + six unused framework frontends OFF
  + samples OFF
  + JavaScript OFF

Runtime cache evidence/policy:
  + corresponding direct cache keys and exact matches
  + optional ENABLE_SYSTEM_PROTOBUF key with missing-or-OFF policy
```

The following must remain byte-for-byte or semantically unchanged apart from adjacent comments: source pins, source/provenance commands, TBB handling, `parallelism = 1`, `--parallel 1`, resource thresholds, build/install commands, binary evidence, decision classifications, manifest generation, environment restore, and scientific authorization flags.

- [ ] **Step 6: Commit the minimal implementation**

Commit message:

```text
fix: narrow Route A Runtime frontend build surface
```

---

### Task 3: GREEN repository/application verification

**Files:**
- No additional production files unless verification exposes a defect directly caused by Task 2.
- Test: `tests/testing/workbook05/test_build_powershell_contract.py`
- Test: existing Workbook 05 PowerShell `*.Tests.ps1` regressions through the repository gate.

**Interfaces:**
- Consumes: exact final implementation head from Task 2.
- Produces: repository-level evidence that the narrow Runtime change is regression-safe before any long Lenovo OpenVINO experiment is allowed.

- [ ] **Step 1: Require the focused narrow-Runtime contract to pass**

Run the repository's Python test command for `tests/testing/workbook05/test_build_powershell_contract.py` on the exact implementation head. Required result: every discovered test in that file passes.

- [ ] **Step 2: Require the complete Workbook 05 gate**

Run the existing `Validate-Workbook05-BuildStage.ps1` gate through the same supported Windows PowerShell path used by CI. Require all Python contracts, executable PowerShell regressions, module/export checks, security checks, and the final marker:

```text
WORKBOOK05_BUILD_STAGE_GATE_PASS
```

No test may be skipped to obtain GREEN.

- [ ] **Step 3: Require focused workflow/security contracts and whitespace integrity**

Require the existing focused workflow/security test set to pass and require:

```text
git diff --check
```

with no output/error.

- [ ] **Step 4: Verify the normal WinUI/application workflow on the same exact head**

Require application restore/build success, unit-test restore/build success, zero build errors, and the current legitimate packaged VSTest total (`134`) all passing.

- [ ] **Step 5: Independently verify the uploaded unit-test artifact**

Download the exact artifact from the successful WinUI run, compute SHA-256 independently, compare it with GitHub's recorded digest, and parse the TRX counters independently. Required result: all 134 tests executed and passed, with zero failed/errors/aborted/timeouts/not-executed.

- [ ] **Step 6: Stop if GREEN requires any unrelated repair**

If verification exposes a new failure not caused by the narrow frontend contract, return to systematic debugging. Do not bundle an unrelated fix into this PR merely to make the branch green.

---

### Task 4: PR review, merge, post-merge verification, and live Runtime boundary

**Files:**
- No additional production files unless exact verification identifies a defect caused by this repair.
- Update the PR body with the complete evidence trail before merge.

**Interfaces:**
- Consumes: exact verified branch head from Task 3.
- Produces: a verified `main` checkpoint authorizing one brand-new narrow `route-a-runtime` experiment.

- [ ] **Step 1: Review the complete branch diff against base `45c2f44b2e5bdb200a70702851be3340c4a082c6`**

Expected changed files are limited to the approved design/plan, `tests/testing/workbook05/test_build_powershell_contract.py`, and `scripts/testing/workbook05/Invoke-Workbook05RouteARuntimeBuild.ps1`.

Reject drift in OpenVINO/GenAI pins, Runtime parallelism, resource thresholds, page-file/machine security state, GenAI, Route B/BR8, GitHub Actions permissions/action pins, application source, or evidence-validator behavior.

- [ ] **Step 2: Replace the draft PR body with a detailed audit record**

Include at least:

```text
motivating Runtime run: 31236734532
base/main commit: 45c2f44b2e5bdb200a70702851be3340c4a082c6
first divergence: generated OpenVINO protoc.exe blocked by Device Guard/App Control
previous Runtime decision: Failed
previous build parallelism: 1
previous safety stop: false
approved repair: CPU + IR frontend only; six unused framework frontends OFF; samples/JS OFF
Device Guard/App Control change: none
resource-safety change: none
source-pin change: none
RED exact head/run and expected failing contract
GREEN exact head/run and Workbook gate evidence
WinUI 134/134 evidence
independent unit-test artifact SHA-256/TRX evidence
exact changed files
explicit scientific non-claims
```

- [ ] **Step 3: Mark the PR ready and verify review state**

Require the exact verified head, mergeable status, and zero unresolved review threads. Do not merge a head that moved after final verification.

- [ ] **Step 4: Merge only the verified head using expected-head protection**

Use a merge commit so the design, TDD history, and implementation remain auditable.

- [ ] **Step 5: Verify fresh `main` after merge**

Require a new push-triggered `Build and test` run on the literal merge SHA. Independently verify its uploaded unit-test artifact SHA-256 and TRX counters again before authorizing the long Runtime build.

- [ ] **Step 6: Authorize exactly one brand-new manual Route A Runtime dispatch**

The manual workflow inputs remain:

```text
branch: main
stage: route-a-runtime
run_identity: phase2
runtime_install_directory: blank
runtime_decision_path: blank
br8_run_id: blank
br8_artifact_name: blank
br8_expected_artifact_digest: blank
br8_actual_artifact_digest: blank
br8_accepted_by_project_owner: false
```

Do not rerun `31236734532`; it belongs to the previous broad-frontend commit.

- [ ] **Step 7: Interpret the new live result before changing another variable**

Accept Route A Runtime as `Passed` only when configure, materialized-cache verification, build, install, binary evidence, `decision.json`, manifest, and independent hosted validation all pass in the same attempt. If a different compiler/build failure appears, treat it as the new first divergence. Do not weaken Device Guard or the resource controller without a new evidence-based design.

- [ ] **Step 8: Advance to Route A GenAI only after accepted Runtime `Passed` evidence**

GenAI remains blocked until the narrow Runtime attempt has one accepted install directory and independently validated `Passed` decision. A green workflow that merely records a controlled Runtime failure is not sufficient.

## Plan Self-Review

- **Spec coverage:** CPU/IR requirements, six frontend disables, samples/JS disables, Python/wheel retention, optional Protobuf evidence semantics, fail-closed cache checking, unchanged security/resource/source boundaries, TDD, full verification, merge, post-merge verification, and the live experiment hand-off are all mapped to tasks.
- **Placeholder scan:** no TODO/TBD, "similar to", or unspecified production/test step remains.
- **Type/value consistency:** all reviewed CMake controls use exact `ON`/`OFF` strings in configure, cache evidence, and cache-match assertions; Runtime concurrency stays `1`; optional `ENABLE_SYSTEM_PROTOBUF` is consistently defined as `null OR OFF`.
- **Scope:** one Route A Runtime build-surface repair only; no Device Guard change, no machine-policy change, no GenAI/Route B/application implementation change, and no model/performance claim is included.
