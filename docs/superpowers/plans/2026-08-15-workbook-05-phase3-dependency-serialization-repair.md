# Workbook 05 Phase 3 Dependency Serialization Repair Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Repair the model-free Workbook 05 Phase 3 repository gate so dependency requirements remain plain strings and the complete observation serializes predictably under Windows PowerShell 5.1.

**Architecture:** Keep the existing C1 package, workflow stages, evidence schemas, fail-closed decisions, and live-operation block unchanged. Add a regression boundary around the existing serialization probe, then normalize only the requirement-file values and use `ConvertTo-Json -InputObject` at the JSON boundary so PowerShell does not traverse adapted file-content metadata or unwrap collections through the pipeline.

**Tech Stack:** Windows PowerShell 5.1, GitHub Actions `windows-latest`, Python 3.12.10, repository-controlled PowerShell and Python validation gates.

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

### Task 1: Add a fast failing regression boundary

**Files:**
- Modify: `tests/testing/workbook05/Invoke-Phase3ObservationSerializationProbe.Tests.ps1`
- Modify: `tests/testing/workbook05/Invoke-Phase3DependencyPreflightTests.Tests.ps1`

**Interfaces:**
- Consumes: `scripts/testing/workbook05/requirements.phase3-assets.in` and the existing observation probe.
- Produces: a test that rejects adapted `Get-Content` values and a 30-second process boundary around the complete serialization probe.

- [ ] **Step 1: Assert direct requirements are plain strings**

After reading the non-comment requirement rows, inspect each value through `PSObject` and require:

```powershell
if ($Requirement -isnot [string]) {
    throw "Direct requirement is not System.String: $($Requirement.GetType().FullName)"
}
foreach ($AdaptedProperty in @('PSPath', 'PSParentPath', 'PSChildName', 'PSDrive', 'PSProvider', 'ReadCount')) {
    if ($Requirement.PSObject.Properties.Match($AdaptedProperty).Count -ne 0) {
        throw "Direct requirement retained adapted file-content property: $AdaptedProperty"
    }
}
```

- [ ] **Step 2: Bound the probe in a child Windows PowerShell process**

Replace the direct in-process probe call with `System.Diagnostics.Process`. Run the exact probe file with `-NoLogo -NoProfile -ExecutionPolicy Bypass`, redirect stdout/stderr, and use:

```powershell
if (-not $Process.WaitForExit(30000)) {
    try { $Process.Kill() } catch { }
    throw 'Dependency observation serialization probe exceeded 30 seconds.'
}
```

Require exit code `0` and the final marker:

```text
Workbook 05 dependency observation serialization probe passed.
```

- [ ] **Step 3: Verify RED on the exact test-only head**

Open a draft pull request so `Workbook 05 Phase 3 assets / Verify Phase 3 repository contract` executes on GitHub-hosted Windows PowerShell 5.1.

Expected: the focused dependency test fails before production code changes, either because a direct requirement retains adapted file-content properties or because the child probe exceeds 30 seconds. It must not reach model collection.

- [ ] **Step 4: Record the failing run**

Retain the exact commit, workflow run, job, first failing marker, and classification `RepositoryGateFailure`. Do not treat this as a Runtime, GenAI, model, or codec failure.

---

### Task 2: Normalize the requirement values and JSON input boundary

**Files:**
- Modify: `scripts/testing/workbook05/Invoke-Workbook05Phase3DependencyPreflight.ps1`
- Modify: `tests/testing/workbook05/Invoke-Phase3ObservationSerializationProbe.Tests.ps1`

**Interfaces:**
- Consumes: UTF-8 requirement rows from `requirements.phase3-assets.in`.
- Produces: a true `string[]` with no adapted `Get-Content` properties and JSON generated through an explicit input object.

- [ ] **Step 1: Normalize each accepted requirement row**

Use an explicit projection after filtering:

```powershell
$DirectRequirements = @(
    Get-Content -LiteralPath $DirectRequirementsPath -Encoding UTF8 |
    Where-Object {
        -not [string]::IsNullOrWhiteSpace($_) -and
        -not $_.TrimStart().StartsWith('#')
    } |
    ForEach-Object {
        [string]::new(([string]$_).ToCharArray())
    }
)
```

Apply the same construction in the production dependency fixture and the focused probe so the probe models the real observation boundary.

- [ ] **Step 2: Use an explicit JSON input boundary**

Change JSON serialization from pipeline transmission:

```powershell
$Value | ConvertTo-Json -Depth 12
```

or:

```powershell
$SerializationProbe | ConvertTo-Json -Depth 12
```

into:

```powershell
ConvertTo-Json -InputObject $Value -Depth 12
```

and:

```powershell
ConvertTo-Json -InputObject $SerializationProbe -Depth 12
```

- [ ] **Step 3: Verify GREEN on the focused gate**

Expected output includes all field start/return pairs, including:

```text
WB05_DEP_SERIALIZE_FIELD:direct_requirements:start
WB05_DEP_SERIALIZE_FIELD:direct_requirements:return
Workbook 05 dependency observation serialization probe passed.
```

The child process must finish within 30 seconds and the complete dependency-fixture tests must pass.

- [ ] **Step 4: Verify the complete Phase 3 repository gate**

Run the pull-request-triggered `Workbook 05 Phase 3 assets / Verify Phase 3 repository contract` and require the final controlled marker:

```text
WORKBOOK05_PHASE3_GATE_PASS
```

- [ ] **Step 5: Verify normal repository regression**

Require the exact repair head to pass the normal `Build and test` workflow as well as all specialised workflows triggered by the changed paths.

---

### Task 3: Review, merge boundary, and post-merge rehearsal

**Files:**
- Review all changed files and the pull-request evidence.

**Interfaces:**
- Consumes: exact-head workflow results and the branch diff.
- Produces: a merge-ready repair whose scope is limited to the dependency observation serialization boundary.

- [ ] **Step 1: Review the exact diff**

Confirm there are no changes to models, Runtime/GenAI pins, benchmark definitions, workflow permissions, self-hosted labels, accepted assets, or scientific claim flags.

- [ ] **Step 2: Update the pull-request description**

Document the reproduced failure, first divergence, RED run, minimal implementation, exact verification runs, non-claims, and post-merge operation.

- [ ] **Step 3: Merge only after exact-head green evidence**

Do not merge while the specialised Phase 3 repository contract is red, skipped, cancelled, or still running.

- [ ] **Step 4: Verify fresh `main`**

After merge, require a fresh normal regression on the merge commit and confirm the specialised repository contract remains green where triggered.

- [ ] **Step 5: Re-dispatch the unchanged offline fixture**

Use:

```text
operation: offline-fixture
confirm_live_asset_lock: unchecked
accepted_dependency_preflight_sha256: blank
```

The expected job sequence remains repository contract, Lenovo C1 fixture collection, then clean hosted validation. No model or scientific claim is authorised by this rehearsal.
