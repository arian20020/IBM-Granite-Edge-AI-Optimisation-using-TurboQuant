# Workbook 05 Route A Runtime evidence/validation repair design

## Status

Approved problem scope from the 2026-08-07 Route A Runtime attempt. Implementation has not started yet; this document freezes the repair boundary before test-first changes.

## Production evidence

The controlled `route-a-runtime` workflow-dispatch run `31182994474` executed on `main` commit `5e862b3eb3dd4578ffebbbcce584df689c1bf513`.

The hosted repository gate passed. The self-hosted Lenovo job also passed checkout and the repository gate, proving the process-scoped PowerShell repair from PR #52 works on the real runner.

The Route A Runtime script then failed after exact source acquisition and recursive submodule update with:

```text
Read-CommandOutput : You cannot call a method on a null-valued expression.
Invoke-Workbook05RouteARuntimeBuild.ps1:286
$sourceStatus = Read-CommandOutput $statusResult
```

The same failed attempt still uploaded the text-only artifact `workbook-05-build-route-a-runtime-31182994474-1` (artifact ID `8996859949`). GitHub recorded SHA-256:

```text
9bf984d2dd218c55bb68ec69ecebbc5ce60b07e4164f1aafe6ace55912714931
```

An independent download produced the same SHA-256.

The artifact proves `route-a-runtime-git-status-verify` exited `0` and its stdout/stderr files were both zero bytes. The source checkout was therefore clean; the failure occurred when repository code attempted `.Trim()` on the empty command output. No `route-a-runtime-configure`, `route-a-runtime-build`, or `route-a-runtime-install` record exists, so OpenVINO configure/compilation/install were not reached.

A second deterministic failure occurred in the separate hosted validator. It successfully downloaded the exact artifact and reran the full Workbook 05 gate, but the subsequent validation command failed with:

```text
ModuleNotFoundError: No module named 'jsonschema'
```

The gate intentionally installs pinned Python dependencies beneath `RUNNER_TEMP`, temporarily exposes them through process-scoped `PYTHONPATH`, and restores the previous `PYTHONPATH` before returning. The next workflow step therefore cannot import `jsonschema` unless the dependency directory is explicitly supplied again.

## Root causes

### Defect A — empty successful stdout is not represented safely

`Read-CommandOutput` currently evaluates:

```powershell
(Get-Content -LiteralPath $Result.stdout_path -Raw -ErrorAction Stop).Trim()
```

A successful command may legitimately create an empty stdout file. For `git status --porcelain=v1`, empty stdout is the clean-tree success state. The helper incorrectly assumes `Get-Content -Raw` always returns a non-null string, so a valid empty result becomes an integrity exception.

The representation contract should instead be: an existing readable zero-byte stdout file maps to the empty string `''`; a missing/unreadable stdout file must still fail closed.

### Defect B — hosted validation step loses the gate's temporary dependency path

`Validate-Workbook05-BuildStage.ps1` correctly avoids persistent/global Python-package mutation. It installs pinned dependencies beneath the runner temporary directory, temporarily adds that directory to `PYTHONPATH`, runs the gate, and restores the caller's environment in `finally`.

The hosted `Validate artifact as untrusted data` step is a new process launched after the gate. It invokes `python -m scripts.testing.workbook05.build_bundle_validation` without the temporary dependency directory on Python's module search path, so `jsonschema` is unavailable.

This is a workflow hand-off defect, not a reason to weaken the gate's environment restoration.

## Approaches considered

### Approach A — recommended: fix the two narrow contracts at their boundaries

1. Make `Read-CommandOutput` explicitly map a readable empty stdout file to `''` while preserving terminating errors for missing/unreadable files.
2. Add focused regression coverage for the empty-output case.
3. Keep the repository gate's `PYTHONPATH` restoration unchanged.
4. Give only each hosted artifact-validation step the known runner-temporary dependency directory in its `PYTHONPATH` so the separately launched Python process can import the pinned validator dependency.
5. Add workflow-contract coverage proving the hosted validators receive this dependency path.

Advantages: smallest behavioral change, preserves fail-closed semantics and least privilege, no global installs, no source/build-setting changes, and addresses both deterministic failures before another long external-source run.

### Approach B — make the gate permanently mutate the job environment

The gate could write its dependency directory to `GITHUB_ENV` or deliberately leave `PYTHONPATH` changed for later steps.

Rejected because the gate is also used outside this one validator path and currently has a clear ownership rule: install temporary dependencies, use them for the gate, then restore the caller environment. Turning the gate into a cross-step environment mutator increases coupling and weakens isolation.

### Approach C — install `jsonschema` globally/on the hosted image and leave the Runtime helper unchanged except for a one-off guard

Rejected because it introduces persistent/unnecessary environment dependence and would treat the two symptoms independently rather than preserving a single explicit temporary-dependency boundary. It would also make local/hosted reproducibility less controlled.

## Selected design

Use Approach A.

### Runtime command-output behavior

`Read-CommandOutput` remains responsible only for reading the command adapter's captured stdout file. It must:

- require the recorded stdout path to exist and be readable;
- return `''` for a legitimate zero-byte file;
- return trimmed text for non-empty stdout;
- never convert a missing/unreadable evidence file into apparent success.

No Git command, source pin, source repository, CMake option, build parallelism, workspace root, resource-safety rule, or install path changes.

### Hosted validator dependency behavior

Keep `Validate-Workbook05-BuildStage.ps1` unchanged in its environment-restoration policy.

For the hosted build-bundle validation steps, explicitly set process-scoped `PYTHONPATH` to the exact dependency directory the gate creates beneath `RUNNER_TEMP`:

```text
<runner temp>/workbook05-build-stage-python
```

The setting belongs only to the Python validation step that imports `build_bundle_validation`; it is not written to machine/user environment state and is not propagated to unrelated jobs.

The same contract must apply consistently to Route A Runtime, Route A GenAI, Route B Runtime, and Route B GenAI hosted validators so a later stage cannot rediscover the same deterministic defect.

## Test-first plan boundary

Implementation must proceed RED -> GREEN.

1. Add a failing regression test that exercises or contract-checks empty stdout handling and proves the current `.Trim()` assumption is unsafe.
2. Add a failing workflow contract proving every hosted build-bundle validator receives the temporary pinned dependency directory when launching Python validation.
3. Confirm the tests fail for the intended reasons on the test-only head.
4. Apply only the minimal Runtime helper/workflow changes required for those tests.
5. Rerun the focused tests, then the complete Workbook 05 suite and `git diff --check`.
6. Run the normal WinUI build/test regression before integration.
7. Open a detailed PR containing production run IDs, artifact digest, RED/GREEN evidence, security rationale, unchanged scientific boundaries, and exact next dispatch instructions.
8. Merge only the exact verified head, run post-merge `main` verification, then dispatch a fresh `route-a-runtime` attempt.

## Security and scientific boundaries

Unchanged:

- repository/workflow permissions remain read-only;
- actions remain pinned to immutable SHAs;
- self-hosted execution remains same-repository/manual and exact-label constrained;
- no machine-wide or user-wide PowerShell/Python setting is changed;
- no global Python dependency installation is added;
- Runtime source remains `openvinotoolkit/openvino@b9a1f201c109e0bed74763934f79483cf6c4cbf4`;
- GenAI source remains `openvinotoolkit/openvino.genai@05e5c7670b597746f858946974d11f38e3baf42f`;
- Route B remains fail-closed behind BR8;
- no model is executed;
- no QJL/PolarQuant/TurboQuant activation, packed-storage, performance, memory, context-length, or quality claim is authorised.

A successful repaired Runtime run is still only a build candidate until the exact text-only artifact passes independent hosted validation.

## Professional references

- Microsoft PowerShell `Get-Content -Raw`: https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.management/get-content
- GitHub Actions variables and passing values between steps: https://docs.github.com/en/actions/how-tos/write-workflows/choose-what-workflows-do/use-variables
- GitHub Actions workflow environment files: https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-commands
- Python 3.12 module search path and `PYTHONPATH`: https://docs.python.org/3.12/library/sys_path_init.html

## Textbook basis

- *Why Programs Fail: A Guide to Systematic Debugging*, 2nd ed. — reproduce the failure, identify the first divergence, trace the bad value backward, and change one cause at a time.
- *The Art of Unit Testing* — preserve discovered defects as automated regression tests before implementation.
- *Code Complete*, Chapters 22-23 — developer testing and evidence-driven debugging rather than speculative repair.
- *Designing Secure Software*, Chapter 4 — least privilege, secure defaults, and narrowly scoped environment changes.
- *Systems Engineering: Principles and Practice*, Chapter 17 — verify each stage before advancing to the next controlled integration boundary.
