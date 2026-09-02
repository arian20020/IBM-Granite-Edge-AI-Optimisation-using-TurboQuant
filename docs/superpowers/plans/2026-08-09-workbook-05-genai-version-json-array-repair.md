# Workbook 05 Route A GenAI Version and JSON-Array Repair Implementation Plan

> **Required process:** execute test-first, preserve the accepted Runtime installation, and verify every completion claim from fresh CI or retained evidence.

**Goal:** Make the Route A GenAI layer build against the already accepted OpenVINO Runtime 2026.3 installation and make one-element evidence collections remain JSON arrays.

**Architecture boundary:** OpenVINO GenAI supplies the LLM pipeline and forwards an `ov::AnyMap` of Runtime/plugin properties. The accepted modified OpenVINO Runtime remains the owner of merged TurboQuant cache algorithms, bit-width selection, and CPU codec execution. The shared Workbook 05 JSON writer owns evidence serialization shape.

**Constraints:** no Runtime pin change, no Runtime rebuild in the repair workflow, no Route B change, no model download/inference, no benchmark or quality claims, no repository permission expansion, no binary artifact upload.

---

## Task 1: Freeze the exact regression failures

**Files:**
- Modify: `tests/testing/workbook05/test_build_powershell_contract.py`
- Modify: `tests/testing/workbook05/test_source_admission_settings.py`
- Modify: `tests/testing/workbook05/test_build_workflow_contract.py`
- Optionally add a focused PowerShell test under `tests/testing/workbook05/` if the existing module test host is the clearer boundary.

**Steps:**

1. Add a singleton-array regression that imports `Workbook05.Build.psm1`, calls `Write-Wb05Json` with one ordered dependency record, reads the raw JSON, and proves:
   - the first non-whitespace character is `[`;
   - `ConvertFrom-Json` yields one collection member;
   - the member retains its expected fields.
2. Add an ordinary-object companion assertion so the repair cannot turn every JSON value into an array.
3. Add exact-pin contract assertions for the source-matched GenAI commit `bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0` across active settings, manifest, script, workflow, and validator surfaces.
4. Add an assertion that the active Runtime pin stays `b9a1f201c109e0bed74763934f79483cf6c4cbf4`.
5. Commit tests without the production repair.
6. Run the Workbook 05 contract workflow and record the expected failures. The red state must identify the JSON shape and old GenAI pin rather than unrelated defects.

## Task 2: Repair the shared JSON writer

**File:**
- Modify: `scripts/testing/workbook05/Workbook05.Build.psm1`

**Steps:**

1. Replace pipeline serialization with an explicit input-object call:

   ```powershell
   $json = ConvertTo-Json -InputObject $Value -Depth 32
   ```

2. Keep UTF-8 without BOM and the existing final newline unchanged.
3. Keep the function generic; do not special-case `dependencies.json` or count collection members manually.
4. Run the focused JSON tests and confirm singleton array plus ordinary object both pass.

## Task 3: Replace the incompatible GenAI pin on active control surfaces

**Files:**
- Modify: `scripts/testing/workbook05/Invoke-Workbook05RouteAGenAIBuild.ps1`
- Modify: `.github/workflows/workbook-05-documented-build.yml`
- Modify: `experiments/granite_turboquant_intel/configurations/workbook05/source-admission-settings.json`
- Modify: `experiments/granite_turboquant_intel/configurations/workbook05/pinned-document-sources.json`
- Modify: `experiments/granite_turboquant_intel/manifests/campaigns/GTQ-WB05-MF-v1/route-a-source-admission.json`
- Modify: `scripts/testing/workbook05/source_admission_bundle_validation.py`
- Modify any active test fixture or controlled template that freezes the previous commit.

**Steps:**

1. Replace active GenAI commit `05e5c7670b597746f858946974d11f38e3baf42f` with `bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0`.
2. Preserve the Runtime commit and Route A identifiers unchanged.
3. Keep historical plans/specifications unchanged; the new design amendment records why the decision changed.
4. Update active human-readable decision text only where it describes the current candidate rather than historical evidence.
5. Search the branch for the old commit and classify every remaining occurrence as either:
   - an intentional historical record, or
   - a missed active control that must be corrected.
6. Run pin-consistency tests.

## Task 4: Run the full repository-controlled gate

**Command represented by CI:**

```powershell
& '.\scripts\testing\Validate-Workbook05-BuildStage.ps1' `
    -RepositoryRoot $env:GITHUB_WORKSPACE `
    -PythonPath 'python'
```

**Required evidence:**

1. all Workbook 05 Python tests pass;
2. PowerShell module tests pass;
3. focused workflow/security contracts pass;
4. imports and syntax checks pass;
5. forbidden execution/payload scan passes;
6. `git diff --check` passes;
7. final marker is `WORKBOOK05_BUILD_STAGE_GATE_PASS`.

Do not proceed on partial green status.

## Task 5: Run normal application regression

**Required evidence:**

1. WinUI restore/build succeeds;
2. test restore/build succeeds;
3. packaged application tests pass with zero failures;
4. retained TRX totals are independently checked rather than inferred from a green badge.

## Task 6: Review and integrate the repair

**Steps:**

1. Review the complete branch diff for scope creep.
2. Confirm no Runtime algorithm, Route B, model, benchmark, quality, security permission, or machine-policy change is present.
3. Open a detailed pull request explaining:
   - the exact run/artifact that exposed the defects;
   - the first CMake divergence;
   - why 2026.3 GenAI is the source-matched candidate;
   - why the JSON fix belongs at the shared writer boundary;
   - every modified file and test;
   - what remains unproven until the live GenAI build.
4. Wait for all required checks on the exact PR head.
5. Merge only the exact verified head.
6. Verify a fresh post-merge `main` regression before running the live external build.

## Task 7: Re-run only Route A GenAI on the Lenovo

**Inputs:**

```text
stage: route-a-genai
run_identity: phase2
runtime_install_directory: C:\w5a\phase2-31261978552-2\i-ov
runtime_decision_path: C:\w5a\accepted-route-a-runtime-31261978552-2\decision.json
```

All Route B fields remain blank/false.

**Acceptance evidence:**

1. repository gate passes on both hosted and Intel runners;
2. GenAI source resolves to `bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0`;
3. configure succeeds against OpenVINO `2026.3.0`;
4. build and install succeed;
5. `decision.json` says `Passed` for component `genai` and the exact source commit;
6. `dependencies.json` is a JSON array;
7. text-only artifact uploads successfully;
8. independently calculated artifact SHA-256 matches GitHub’s recorded digest;
9. hosted untrusted-data validation succeeds;
10. later scientific authorisation flags remain false.

## Task 8: Preserve the accepted GenAI handoff

After successful validation:

1. retain the exact artifact, digest, run ID, attempt, source commit, install directory, and decision file;
2. copy the accepted `decision.json` to a durable path under `C:\w5a` and verify its SHA-256;
3. do not rebuild GenAI for later analysis when the full experiment identity matches;
4. use the accepted Runtime + GenAI pair as the input to codec capability and lowest-memory-first experiments.

The next scientific stage remains separate: codec activation/capability proof, measured storage ordering, context frontier, formal performance metrics, P1–P6 quality scoring, and raw-output retention.
