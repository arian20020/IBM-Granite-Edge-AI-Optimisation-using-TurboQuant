# Workbook 05 Runtime Evidence/Validation Repair Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Repair the two deterministic orchestration defects exposed by Route A Runtime run `31182994474` so a clean Git status is represented safely and every hosted build-bundle validator can import the pinned `jsonschema` dependency.

**Architecture:** Preserve the existing Workbook 05 boundaries. The Runtime script continues reading only the command adapter's captured stdout file, but explicitly maps a readable zero-byte file to `''`; missing/unreadable evidence remains a terminating error. The repository gate continues to install dependencies in runner-temporary storage and restore `PYTHONPATH`; only the four hosted artifact-validation steps receive that known temporary directory through step-scoped `env`.

**Tech Stack:** Windows PowerShell 5.1, GitHub Actions YAML, Python 3.12.10 `unittest`, pinned `jsonschema==4.25.1`.

## Global Constraints

- Runtime source stays `openvinotoolkit/openvino@b9a1f201c109e0bed74763934f79483cf6c4cbf4`.
- GenAI source stays `openvinotoolkit/openvino.genai@05e5c7670b597746f858946974d11f38e3baf42f`.
- No CMake option, parallelism, workspace root, install path, runner label, action SHA, workflow permission, Route B BR8 rule, or model/scientific-claim boundary changes.
- Do not install Python packages globally or persistently alter machine/user `PYTHONPATH`.
- Keep the gate's environment-restoration behavior unchanged.
- Every production behavior change must have a failing regression test first.

---

### Task 1: Protect legitimate empty command stdout

**Files:**
- Modify: `tests/testing/workbook05/test_route_a_runtime_integrity.py`
- Modify: `scripts/testing/workbook05/Invoke-Workbook05RouteARuntimeBuild.ps1`

**Interfaces:**
- Consumes: `Read-CommandOutput($Result)` where `$Result.stdout_path` identifies the command adapter's captured stdout file.
- Produces: trimmed text for non-empty stdout; exactly `''` for a readable zero-byte stdout file; a terminating error for missing/unreadable files.

- [ ] **Step 1: Write the failing regression contract**

Add a focused integrity test that requires the Runtime helper to materialise the raw read into a variable, explicitly handle `$null`, return `''`, and trim only the non-null value. The test must also reject the unsafe one-expression form that caused run `31182994474` to fail.

```python
def test_command_output_reader_handles_empty_stdout_without_masking_missing_files(self) -> None:
    text = RUNTIME_SCRIPT.read_bytes().decode("utf-8", errors="strict")

    self.assertIn(
        "$capturedOutput = Get-Content -LiteralPath $Result.stdout_path -Raw -ErrorAction Stop",
        text,
    )
    self.assertIn("if ($null -eq $capturedOutput)", text)
    self.assertIn("return ''", text)
    self.assertIn("return $capturedOutput.Trim()", text)
    self.assertNotIn(
        "return (Get-Content -LiteralPath $Result.stdout_path -Raw -ErrorAction Stop).Trim()",
        text,
    )
```

- [ ] **Step 2: Prove RED on the test-only branch head**

Run the focused repository check through CI/PR against the test-only head. Expected result: this test fails because the current Runtime script still calls `.Trim()` directly on the `Get-Content -Raw` result.

- [ ] **Step 3: Implement the minimal null-safe reader**

Replace only `Read-CommandOutput` with:

```powershell
function Read-CommandOutput {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Result
    )

    # Read only the captured stdout file associated with the reviewed command.
    # Windows PowerShell can yield no object for a readable zero-byte file, which
    # is a legitimate success value for commands such as `git status --porcelain`.
    $capturedOutput = Get-Content `
        -LiteralPath $Result.stdout_path `
        -Raw `
        -ErrorAction Stop
    if ($null -eq $capturedOutput) {
        return ''
    }
    return $capturedOutput.Trim()
}
```

The unchanged `-ErrorAction Stop` is the fail-closed boundary for missing/unreadable stdout evidence.

- [ ] **Step 4: Prove GREEN for the focused Runtime integrity test**

Run the focused test again. Expected: PASS.

- [ ] **Step 5: Commit the Runtime helper repair**

Commit only the Runtime regression test and minimal helper change with a message describing the clean-status empty-output defect.

---

### Task 2: Preserve pinned validator dependencies across the GitHub step boundary

**Files:**
- Modify: `tests/testing/workbook05/test_build_workflow_contract.py`
- Modify: `.github/workflows/workbook-05-documented-build.yml`

**Interfaces:**
- Consumes: the gate-created dependency directory `${{ runner.temp }}\workbook05-build-stage-python`.
- Produces: step-scoped `PYTHONPATH` for only each `Validate artifact as untrusted data` Python process in `validate-route-a-runtime`, `validate-route-a-genai`, `validate-route-b-runtime`, and `validate-route-b-genai`.

- [ ] **Step 1: Write the failing workflow regression contract**

Add a focused test that scopes to each hosted validator job and requires the validation step to carry the temporary dependency directory:

```python
def test_hosted_bundle_validators_receive_temporary_python_dependencies(self) -> None:
    expected = (
        "- name: Validate artifact as untrusted data\n"
        "        env:\n"
        "          PYTHONPATH: ${{ runner.temp }}\\workbook05-build-stage-python\n"
    )
    for job_id in (
        "validate-route-a-runtime",
        "validate-route-a-genai",
        "validate-route-b-runtime",
        "validate-route-b-genai",
    ):
        with self.subTest(job_id=job_id):
            block = self._job_block(job_id)
            self.assertIn(expected, block)
```

- [ ] **Step 2: Prove RED before workflow modification**

Run the focused workflow contract on the test-only head. Expected: four subtest failures because none of the hosted validator steps currently define `PYTHONPATH`.

- [ ] **Step 3: Apply the minimal step-scoped workflow change**

For each hosted `Validate artifact as untrusted data` step, add exactly:

```yaml
env:
  PYTHONPATH: ${{ runner.temp }}\workbook05-build-stage-python
```

Keep the gate unchanged. Do not write `GITHUB_ENV`; do not add global/job-wide dependency state.

- [ ] **Step 4: Prove GREEN for the focused workflow contract**

Run the focused workflow/security contract again. Expected: PASS.

- [ ] **Step 5: Commit the hosted-validator dependency repair**

Commit only the workflow regression test and the four step-scoped `PYTHONPATH` additions.

---

### Task 3: Full verification and integration

**Files:**
- Review all files changed by Tasks 1-2 plus this spec/plan.

**Interfaces:**
- Consumes: exact branch head after Tasks 1-2.
- Produces: one mergeable PR whose evidence proves both defects are covered and no scientific/build boundary drift occurred.

- [ ] **Step 1: Run the complete Workbook 05 gate**

Require the full Python discovery suite, focused workflow/security contracts, PowerShell module import, `git diff --check`, and final `WORKBOOK05_BUILD_STAGE_GATE_PASS` on the exact branch head.

- [ ] **Step 2: Run the normal WinUI build/test regression**

Require application restore/build, unit-test restore/build, and packaged VSTest with all tests passing.

- [ ] **Step 3: Review the exact diff**

Confirm only the intended Runtime helper, two regression-test files, documented-build workflow, and design/plan records changed. Confirm source pins, CMake flags, `--parallel 2`, self-hosted labels/timeouts, action SHAs, permissions, artifact boundaries, Route B BR8 logic, and claim authorisations are unchanged.

- [ ] **Step 4: Open a detailed PR**

Document run `31182994474`, collector job `92881007514`, validator job `92891689050`, artifact `8996859949`, digest `9bf984d2dd218c55bb68ec69ecebbc5ce60b07e4164f1aafe6ace55912714931`, first-divergence evidence, RED/GREEN run IDs, exact verified head, security rationale, and explicit non-claims.

- [ ] **Step 5: Merge only the exact verified head**

Use expected-head protection when merging. Do not merge if checks, review, or exact-head verification changed.

- [ ] **Step 6: Verify fresh `main` after merge**

Require the post-merge WinUI regression on the new merge commit and independently verify its TRX artifact/digest before advancing.

- [ ] **Step 7: Start a fresh `route-a-runtime` dispatch**

Use `main`, `stage=route-a-runtime`, `run_identity=phase2`, and leave Runtime/BR8 optional inputs blank/default/false. If the connected GitHub tool exposes no workflow-dispatch write action, stop only at this final UI boundary and provide the exact dispatch values rather than claiming the workflow was started.

- [ ] **Step 8: Monitor the new Runtime attempt**

Confirm the repository gate, self-hosted runner, source acquisition, provenance checks, configure, build, install, evidence upload, and independent hosted validation in order. Treat any new failure as a fresh first-divergence investigation; do not infer OpenVINO success or failure from upstream orchestration state.
