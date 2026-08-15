# Workbook 05 Phase 3 Dependency Serialization Repair Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Repair the model-free Workbook 05 Phase 3 repository gate so dependency requirements remain plain strings, the complete observation serializes predictably under Windows PowerShell 5.1, and every Phase 3 job receives the exact pinned schema-validation dependency through an isolated job-local environment.

**Architecture:** Keep the existing C1 package, workflow stages, evidence schemas, fail-closed decisions, and live-operation block unchanged. Normalize only the requirement-file values, use `ConvertTo-Json -InputObject` at the JSON boundary, bound the diagnostic in a child Windows PowerShell process, and install the repository-pinned `jsonschema` dependency beneath `RUNNER_TEMP` in each isolated job.

**Tech Stack:** Windows PowerShell 5.1, GitHub Actions `windows-latest`, self-hosted Windows x64 collection runner, Python 3.12.10, repository-controlled PowerShell and Python validation gates.

## Global Constraints

- Start from `main@6755391decd6bd798dea48dabab4fec6a0e4df1b`.
- Preserve campaign `GTQ-WB05-MF-v1` and route `route-a-merged-openvino`.
- Preserve the ten approved dependency-preflight stages and their order.
- Preserve `offline-fixture` as the only executable dependency operation in this revision.
- Keep model download, Granite execution, activation, packed-storage, performance, and quality authorisations false.
- Do not change accepted Runtime/GenAI identities or directories.
- Do not change TurboQuant, QJL, PolarQuant, model matrices, benchmark prompts, or quality rubrics in this repair.
- Do not weaken the current forbidden-token, path, evidence, or untrusted-artifact boundaries.
- Do not increase the existing five-minute workflow timeout to hide the defect.

---

## Task 1: Add a fast failing regression boundary

**Files:**
- Modify: `tests/testing/workbook05/Invoke-Phase3ObservationSerializationProbe.Tests.ps1`
- Modify: `tests/testing/workbook05/Invoke-Phase3DependencyPreflightTests.Tests.ps1`

- [x] **Assert every direct requirement is plain `System.String` data**

The probe rejects adapted file/provider properties including `PSPath`, `PSParentPath`, `PSChildName`, `PSDrive`, `PSProvider`, and `ReadCount`.

- [x] **Bound the complete probe in a child Windows PowerShell process**

The exact probe runs with redirected stdout/stderr and a 30-second `WaitForExit` boundary. A stalled process is terminated and classified as a repository-test failure instead of consuming the whole five-minute GitHub Actions step.

- [x] **Verify RED before changing production code**

Exact test-only evidence:

```text
head: ff3c555b2872bae73f692601249397e731c9b060
workflow run: 31896957306
job: 95041627110
first causal failure:
Direct requirement retained adapted file-content property: PSPath
```

The failure occurred in about four seconds. The complete gate, Lenovo collector, artifact upload, model access, and every scientific test remained skipped.

---

## Task 2: Repair the controlled serialization boundary

**Files:**
- Modify: `scripts/testing/workbook05/Invoke-Workbook05Phase3DependencyPreflight.ps1`
- Modify: `tests/testing/workbook05/Invoke-Phase3ObservationSerializationProbe.Tests.ps1`

- [x] **Normalize each accepted requirement row**

Both the production fixture and its focused probe create a new CLR string from each retained line:

```powershell
[string]::new(([string]$_).ToCharArray())
```

This strips the adapted `Get-Content` object properties before the values enter the observation graph.

- [x] **Use an explicit JSON input boundary**

Controlled JSON writes and the cumulative probe now use:

```powershell
ConvertTo-Json -InputObject $Value -Depth 12
```

rather than sending the value through the pipeline.

- [x] **Verify that the original serialization failure is gone**

Run `31897174956`, job `95042616956`, returned from every field, including:

```text
WB05_DEP_SERIALIZE_FIELD:direct_requirements:start
WB05_DEP_SERIALIZE_FIELD:direct_requirements:return
Workbook 05 dependency observation serialization probe passed.
```

That run then exposed the next independent boundary: the clean hosted interpreter did not contain `jsonschema`.

---

## Task 3: Restore the pinned Python validation environment

**Files:**
- Modify: `.github/workflows/workbook-05-phase3-assets.yml`
- Modify: `tests/testing/workbook05/test_phase3_asset_workflow_contract.py`

- [x] **Reproduce and classify the missing dependency**

After serialization succeeded, the decision CLI failed with:

```text
ModuleNotFoundError: No module named 'jsonschema'
```

The repository already controls `jsonschema==4.25.1` in `scripts/testing/workbook05/requirements.txt`; the Phase 3 workflow had omitted the established isolated installation step.

- [x] **Add a workflow-contract regression for all three jobs**

The contract now requires `repository-contract`, `collect-assets`, and `validate-assets` to install the pinned validation dependency beneath `RUNNER_TEMP`, use `--target`, propagate only a job-local `PYTHONPATH`, and perform the setup before their first Python validation operation.

- [x] **Install and verify `jsonschema==4.25.1` in every job**

No package is installed into the checkout, machine-wide Python, accepted Runtime/GenAI folders, model roots, or evidence bundle.

- [x] **Resolve hosted Python deterministically**

Run `31897724983`, job `95043533915`, exposed that a simple `Get-Command python` returned both the tool-cache interpreter and the WindowsApps alias. Hosted jobs now use the repository-established pattern:

```powershell
$pythonCommand = Get-Command -Name python -CommandType Application -All -ErrorAction Stop |
    Select-Object -First 1
$pythonPath = $pythonCommand.Source
```

A workflow-contract test prevents regression to an ambiguous multi-path value.

---

## Task 4: Verify the complete repair and prepare integration

- [x] **Pass the specialised Phase 3 repository contract on the implementation head**

Exact green evidence before this final documentation update:

```text
head: 9df41c1923239bd8beeb7f5bbad0ced6a7c05267
workflow run: 31897867770
job: 95043874332
Python tests: 372 passed
focused workflow/security tests: 17 passed
final marker: WORKBOOK05_PHASE3_GATE_PASS
```

The deliberate tampered-manifest negative test still produced its expected hash-mismatch observation while the enclosing test suite passed.

- [x] **Pass the normal application regression on the same implementation head**

`Build and test` run `31897867779` completed successfully on `9df41c1923239bd8beeb7f5bbad0ced6a7c05267`.

- [x] **Review the repair scope**

The changed surface is limited to the Phase 3 workflow, dependency fixture/probe, their tests, and this plan. There are no application-production, model, Runtime/GenAI pin, codec, benchmark, prompt, rubric, runner-label, permission, or scientific-authorisation changes.

- [ ] **Require every workflow on the final documentation head to finish green**

Because this plan update creates the final review head, repeat exact-head verification rather than relying on the earlier implementation commit.

- [ ] **Update the pull-request description with the complete causal history and final run IDs**

- [ ] **Mark the pull request ready for review only after the final head is green**

- [ ] **Merge only after explicit project-owner approval**

- [ ] **Verify fresh `main` after merge**

Require a fresh normal regression and the specialised repository contract on the merge commit where triggered.

- [ ] **Re-dispatch the unchanged offline fixture from repaired `main`**

Use:

```text
operation: offline-fixture
confirm_live_asset_lock: unchecked
accepted_dependency_preflight_sha256: blank
```

The expected sequence remains repository contract, Lenovo C1 fixture collection, then clean hosted validation. This rehearsal still downloads no model and authorises no activation, storage, performance, or quality claim.
