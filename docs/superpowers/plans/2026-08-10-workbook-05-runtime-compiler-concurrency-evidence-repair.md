# Workbook 05 Runtime Compiler-Concurrency and Evidence Repair Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Route A Runtime evidence schema-valid and enforce a genuine one-process MSVC compilation boundary on the 16 GB Lenovo.

**Architecture:** Keep the current Runtime orchestration and safety controller intact. Add one native MSBuild property to the existing CMake build command, and restore `required_components` to the empty component-dependency array required for a standalone Runtime decision. Preserve frontend header verification as a separate post-install hand-off contract.

**Tech Stack:** Windows PowerShell 5.1, Python 3.12 `unittest`, CMake Visual Studio generator, MSBuild/MSVC, GitHub Actions.

## Global Constraints

- Base commit is `fc4476817ffa68579b2e1caf757846b130029ccf` on `main`.
- OpenVINO Runtime source remains `b9a1f201c109e0bed74763934f79483cf6c4cbf4`.
- OpenVINO GenAI source remains `bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0`.
- Keep `--parallel 1` and add `/p:CL_MPCount=1`; do not increase either value.
- Keep the 90 percent Windows commit threshold and all existing resource-safety controls unchanged.
- Keep ONNX, TensorFlow, IR, Intel CPU, and Python enabled.
- Keep Paddle, TensorFlow Lite, PyTorch, JAX, Intel GPU, Intel NPU, tests, functional tests, samples, JavaScript, and wheel creation disabled.
- Keep the exact two GenAI tokenizer frontend header checks after Runtime installation.
- Keep all model, activation, packed-storage, performance, and quality authorisation flags `false`.
- Do not change application production code, benchmark definitions, prompts, rubrics, TurboQuant, PolarQuant, QJL, or Route B.

---

### Task 1: Add failing concurrency and decision-shape regressions

**Files:**
- Modify: `tests/testing/workbook05/test_build_powershell_contract.py`
- Test: `tests/testing/workbook05/test_build_powershell_contract.py`

**Interfaces:**
- Consumes: the text of `scripts/testing/workbook05/Invoke-Workbook05RouteARuntimeBuild.ps1` loaded as `RouteARuntimeBuildContractTests.text`.
- Produces: two contract tests that fail on the current production script and define the repair boundary.

- [ ] **Step 1: Add the compiler-concurrency regression**

Add this method inside `RouteARuntimeBuildContractTests`:

```python
    def test_runtime_build_caps_native_and_compiler_parallelism(self) -> None:
        self.assertIn("'--parallel', '1'", self.text)
        self.assertIn("'--', '/p:CL_MPCount=1'", self.text)
        self.assertNotIn("'/p:CL_MPCount=2'", self.text)
        self.assertNotIn("'/p:CL_MPCount=4'", self.text)
```

This test preserves the existing one-job native build boundary and requires the separate MSVC `/MP` process cap.

- [ ] **Step 2: Add the schema-compatible Runtime decision regression**

Add this method inside `RouteARuntimeBuildContractTests`:

```python
    def test_runtime_decision_has_no_lower_build_component_dependencies(self) -> None:
        self.assertIn("required_components = @()", self.text)
        self.assertNotIn(
            "required_components = @($RequiredGenAIFrontendHeaders)",
            self.text,
        )
```

The existing `test_runtime_install_requires_genai_tokenizer_frontend_headers` remains unchanged and proves the two header checks were not removed.

- [ ] **Step 3: Run the focused test class and prove RED**

Run:

```powershell
python -m unittest `
  tests.testing.workbook05.test_build_powershell_contract.RouteARuntimeBuildContractTests `
  -v
```

Expected result:

- `test_runtime_build_caps_native_and_compiler_parallelism` fails because `/p:CL_MPCount=1` is absent.
- `test_runtime_decision_has_no_lower_build_component_dependencies` fails because `required_components` contains `$RequiredGenAIFrontendHeaders`.
- Existing Runtime contract tests continue to pass.

- [ ] **Step 4: Commit the RED regression**

```bash
git add tests/testing/workbook05/test_build_powershell_contract.py
git commit -m "test: reproduce Runtime concurrency and decision defects"
```

---

### Task 2: Apply the minimal Runtime orchestration repair

**Files:**
- Modify: `scripts/testing/workbook05/Invoke-Workbook05RouteARuntimeBuild.ps1`
- Test: `tests/testing/workbook05/test_build_powershell_contract.py`

**Interfaces:**
- Consumes: `Invoke-RouteACommand`, the existing CMake build root, resource monitor, decision writer, and post-install frontend header checks.
- Produces: a schema-valid Runtime decision and a CMake build command that passes `/p:CL_MPCount=1` to MSBuild.

- [ ] **Step 1: Restore the correct Runtime decision shape**

In `Write-RouteARuntimeDecision`, replace:

```powershell
        required_components = @($RequiredGenAIFrontendHeaders)
```

with:

```powershell
        # Runtime is the lower Route A build component, so it has no lower
        # component dependency. File-level GenAI hand-off requirements are
        # verified separately after installation and do not belong here.
        required_components = @()
```

Do not change `$RequiredGenAIFrontendHeaders` or the post-install checks.

- [ ] **Step 2: Cap compiler-level MSVC parallelism**

Replace the current Runtime build block:

```powershell
    # Limit Runtime compilation to one concurrent build job for this experiment.
    # The existing resource-safety boundary remains fixed and monitored.
    $build = Invoke-RouteACommand -CommandId 'route-a-runtime-build' -FilePath $CMakePath -Arguments @('--build', $buildRoot, '--config', 'Release', '--parallel', '1', '--verbose') -WorkingDirectory $workspace.work_directory -MonitorResources
```

with:

```powershell
    # Limit both native build scheduling and the MSVC `/MP` compiler process
    # count to one. CMake forwards arguments after `--` to MSBuild, preventing
    # a one-job build from silently spawning many concurrent cl.exe processes.
    # The existing resource-safety boundary remains fixed and monitored.
    $build = Invoke-RouteACommand `
        -CommandId 'route-a-runtime-build' `
        -FilePath $CMakePath `
        -Arguments @(
            '--build', $buildRoot,
            '--config', 'Release',
            '--parallel', '1',
            '--verbose',
            '--', '/p:CL_MPCount=1'
        ) `
        -WorkingDirectory $workspace.work_directory `
        -MonitorResources
```

The resulting command record must contain both `--parallel 1` and `/p:CL_MPCount=1`.

- [ ] **Step 3: Run the focused test class and prove GREEN**

Run:

```powershell
python -m unittest `
  tests.testing.workbook05.test_build_powershell_contract.RouteARuntimeBuildContractTests `
  -v
```

Expected: all `RouteARuntimeBuildContractTests` pass.

- [ ] **Step 4: Commit the production repair**

```bash
git add scripts/testing/workbook05/Invoke-Workbook05RouteARuntimeBuild.ps1
git commit -m "fix: cap Runtime compiler concurrency and repair decision evidence"
```

---

### Task 3: Run complete repository verification

**Files:**
- Verify: `scripts/testing/Validate-Workbook05-BuildStage.ps1`
- Verify: `.github/workflows/workbook-05-documented-build.yml`
- Verify: `.github/workflows/build-and-test.yml`

**Interfaces:**
- Consumes: the final branch head from Tasks 1 and 2.
- Produces: complete Workbook 05 and normal application regression evidence bound to the exact commit.

- [ ] **Step 1: Run the complete Workbook 05 gate**

Run from the repository root:

```powershell
& '.\scripts\testing\Validate-Workbook05-BuildStage.ps1' `
    -RepositoryRoot (Get-Location).Path `
    -PythonPath 'python'
```

Expected:

- all Workbook 05 Python tests pass;
- CMake cache-parser PowerShell tests pass;
- JSON writer PowerShell tests pass;
- Windows path-comparison PowerShell tests pass;
- focused workflow/security tests pass;
- final marker is `WORKBOOK05_BUILD_STAGE_GATE_PASS`.

- [ ] **Step 2: Check text and diff integrity**

Run:

```bash
git diff --check main...HEAD
git status --short
```

Expected: `git diff --check` exits `0`; only intended committed files appear in the branch diff; working tree is clean.

- [ ] **Step 3: Open a draft pull request and let GitHub run both pipelines**

The pull request must target `main` and explain:

- the observed resource-safety interruption from run `31341784206`;
- the `required_components` schema violation;
- why `--parallel 1` did not constrain bare `/MP`;
- the two-part repair;
- the RED and GREEN evidence;
- the post-merge live Runtime boundary.

Expected required workflow results on the exact final head:

- `Workbook 05 documented build`: success;
- `Build and test`: success with all normal application tests passing.

- [ ] **Step 4: Independently inspect retained test evidence**

Download the `unit-test-results-<run-id>-1` artifact from the final `Build and test` run.

Verify:

```powershell
Get-FileHash '.\unit-test-results-<run-id>-1.zip' -Algorithm SHA256
```

Expected: calculated SHA-256 equals GitHub's recorded artifact digest. Parse the TRX and require zero failed, error, timeout, aborted, or not-executed results.

- [ ] **Step 5: Update the pull-request record and mark ready for review**

The final PR description must record:

- exact final head SHA;
- focused RED failure evidence;
- complete Workbook 05 GREEN run ID and results;
- normal application GREEN run ID, artifact ID, digest, and parsed TRX totals;
- exact changed-file scope;
- explicit statement that no live Runtime success is claimed by this PR alone.

Do not merge without project-owner approval.

---

### Task 4: Post-merge live validation handoff

**Files:**
- No repository changes.
- Execute: `.github/workflows/workbook-05-documented-build.yml` on merged `main`.

**Interfaces:**
- Consumes: the exact merged `main` commit and the Lenovo self-hosted runner.
- Produces: a new Runtime installation and independently validated text-only evidence, or a new precisely classified failure.

- [ ] **Step 1: Verify the automatic post-merge application workflow**

Require the fresh `Build and test` run on the exact merge commit to pass before any manual Workbook 05 dispatch.

- [ ] **Step 2: Dispatch only Route A Runtime**

Use these inputs:

```text
Use workflow from: main
stage: route-a-runtime
run_identity: phase2
runtime_install_directory: blank
runtime_decision_path: blank
all BR8 fields: blank
ExecutableCandidate acceptance: unchecked
```

- [ ] **Step 3: Inspect the live Runtime artifact**

Require:

```text
configure exit code = 0
build exit code = 0
install exit code = 0
safety stop = false
required frontend headers = present
decision.json status = Passed
hosted validation = success
independent ZIP SHA-256 = GitHub digest
```

Also inspect the verbose build log and resource trace to determine whether the compiler-process cap prevented the previous commit-pressure pattern.

- [ ] **Step 4: Preserve the accepted Runtime handoff**

Save the exact accepted `decision.json` to a durable `C:\w5a\accepted-route-a-runtime-<run-id>-<attempt>\decision.json` directory and record its SHA-256 before running Route A GenAI.
