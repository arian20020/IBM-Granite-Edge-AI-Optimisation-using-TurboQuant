# Workbook 05 Route A GenAI CMake Path Comparison Repair Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow Route A GenAI to recognise equivalent absolute Windows `OpenVINO_DIR` paths without weakening any other CMake cache or Runtime-integrity check.

**Architecture:** Add a small shared Windows-path equivalence primitive to `Workbook05.Build.psm1`, prove its behaviour through an executable PowerShell regression, and use it only for the GenAI `OpenVINO_DIR` cache field. All other controls remain exact comparisons and all later scientific authorisation flags remain false.

**Tech Stack:** Windows PowerShell 5.1, .NET `System.IO.Path`, Python `unittest` contract tests, GitHub Actions, WinUI 3/.NET application regression.

## Global Constraints

- Keep Runtime commit `b9a1f201c109e0bed74763934f79483cf6c4cbf4` unchanged.
- Keep GenAI commit `bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0` unchanged.
- Do not rebuild Runtime as part of this repository repair.
- Do not change Route B, QJL, PolarQuant, TurboQuant algorithms, models, prompts, benchmark matrix, quality rubric, permissions, or machine policy.
- Do not download or run a model.
- Keep all scientific authorisation flags false.
- Preserve text-only build evidence and fail-closed cache validation.

---

### Task 1: Freeze the Windows path-equivalence regression

**Files:**
- Create: `tests/testing/workbook05/Invoke-BuildWindowsPathComparisonTests.Tests.ps1`

**Interfaces:**
- Consumes: the real exported function `Test-Wb05SameWindowsPath -Left <string> -Right <string>` from `Workbook05.Build.psm1`.
- Produces: executable regression coverage for separator, case, trailing-separator, and sibling-path behaviour.

- [ ] **Step 1: Write the failing PowerShell test**

Create a script that imports the production module and checks:

```powershell
if (-not (Test-Wb05SameWindowsPath `
    -Left 'C:\w5a\phase2\i-ov\runtime\cmake' `
    -Right 'C:/w5a/phase2/i-ov/runtime/cmake')) {
    throw 'Equivalent Windows paths with different separators must compare equal.'
}

if (-not (Test-Wb05SameWindowsPath `
    -Left 'C:\W5A\phase2\i-ov\runtime\cmake' `
    -Right 'c:\w5a\phase2\i-ov\runtime\cmake\')) {
    throw 'Equivalent Windows paths must ignore case and one trailing separator.'
}

if (Test-Wb05SameWindowsPath `
    -Left 'C:\w5a\phase2\i-ov\runtime\cmake' `
    -Right 'C:\w5a\phase2\i-ov-other\runtime\cmake') {
    throw 'Distinct sibling paths must not compare equal.'
}
```

End with `LASTEXITCODE = 0` and a controlled success marker.

- [ ] **Step 2: Commit the test without production code**

Commit message:

```text
test: reproduce GenAI Windows path comparison failure
```

- [ ] **Step 3: Run the Workbook 05 gate and verify RED**

Run through the existing PR gate:

```powershell
& '.\scripts\testing\Validate-Workbook05-BuildStage.ps1' `
    -RepositoryRoot $env:GITHUB_WORKSPACE `
    -PythonPath 'python'
```

Expected result: failure at the new PowerShell test because `Test-Wb05SameWindowsPath` is not exported or defined. No unrelated failure is acceptable as the TDD red proof.

---

### Task 2: Add the minimal shared path-equivalence primitive

**Files:**
- Modify: `scripts/testing/workbook05/Workbook05.Build.psm1`
- Modify: `scripts/testing/Validate-Workbook05-BuildStage.ps1`
- Modify: `tests/testing/workbook05/test_build_powershell_contract.py`

**Interfaces:**
- Consumes: two non-empty path strings.
- Produces: `Test-Wb05SameWindowsPath`, returning `bool` after canonical absolute Windows path comparison.

- [ ] **Step 1: Implement the helper immediately after `Assert-Wb05SafePath`**

```powershell
function Test-Wb05SameWindowsPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$Left,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$Right
    )

    # CMake and .NET may emit the same absolute Windows path with different
    # separator or case conventions. Canonicalise both before identity testing.
    $leftFull = [IO.Path]::GetFullPath($Left).TrimEnd('\', '/')
    $rightFull = [IO.Path]::GetFullPath($Right).TrimEnd('\', '/')

    return $leftFull.Equals(
        $rightFull,
        [StringComparison]::OrdinalIgnoreCase
    )
}
```

- [ ] **Step 2: Export the helper**

Add `'Test-Wb05SameWindowsPath'` to `Export-ModuleMember` and update the nearby comment from ten to eleven reviewed public primitives.

- [ ] **Step 3: Update the repository gate export list**

Add `Test-Wb05SameWindowsPath` to `$expectedFunctions` in `Validate-Workbook05-BuildStage.ps1`.

- [ ] **Step 4: Update the Python export contract**

Add `Test-Wb05SameWindowsPath` to the exact expected export set in `test_build_powershell_contract.py`.

- [ ] **Step 5: Run the focused PowerShell regression and verify GREEN**

Run:

```powershell
& '.\tests\testing\workbook05\Invoke-BuildWindowsPathComparisonTests.Tests.ps1'
```

Expected:

```text
Workbook 05 Windows path comparison PowerShell tests passed.
```

---

### Task 3: Use canonical path identity in the Route A GenAI cache check

**Files:**
- Modify: `scripts/testing/workbook05/Invoke-Workbook05RouteAGenAIBuild.ps1`
- Modify: `tests/testing/workbook05/test_build_powershell_contract.py`

**Interfaces:**
- Consumes: `$cacheValues.OpenVINO_DIR` and `$openvinoConfigDirectory`.
- Produces: Boolean `$openvinoConfigPathMatches`, false when the cache field is missing/empty and otherwise based on `Test-Wb05SameWindowsPath`.

- [ ] **Step 1: Compute the fail-closed path match before the cache `if`**

```powershell
$openvinoConfigPathMatches = (
    -not [string]::IsNullOrWhiteSpace([string]$cacheValues.OpenVINO_DIR) -and
    (Test-Wb05SameWindowsPath `
        -Left $cacheValues.OpenVINO_DIR `
        -Right $openvinoConfigDirectory)
)
```

- [ ] **Step 2: Replace only the raw path comparison**

Replace:

```powershell
$cacheValues.OpenVINO_DIR -ne $openvinoConfigDirectory.Replace('\', '/') -or
```

with:

```powershell
-not $openvinoConfigPathMatches -or
```

Do not change the generator, platform, Python, or JavaScript checks.

- [ ] **Step 3: Freeze the script-use contract**

In `RouteAGenAIBuildContractTests`, require the tokens:

```text
Test-Wb05SameWindowsPath
$openvinoConfigPathMatches
-not $openvinoConfigPathMatches
```

and assert that the previous separator-replacement comparison is absent.

- [ ] **Step 4: Commit the production repair**

Commit message:

```text
fix: canonicalize GenAI CMake package path comparison
```

---

### Task 4: Run complete repository and application verification

**Files:**
- Verify all changed files.

**Interfaces:**
- Consumes: exact branch head after Tasks 1–3.
- Produces: retained CI evidence proving the repair is regression-safe.

- [ ] **Step 1: Run the full Workbook 05 gate**

Expected evidence:

- every Workbook 05 Python test passes;
- all executable PowerShell tests pass;
- focused workflow/security contracts pass;
- module import/export checks pass;
- forbidden execution/payload checks pass;
- `git diff --check` passes;
- final marker: `WORKBOOK05_BUILD_STAGE_GATE_PASS`.

- [ ] **Step 2: Run normal application regression**

Expected evidence:

- WinUI restore/build passes;
- test restore/build passes;
- packaged test TRX has zero failures;
- artifact digest is independently matched;
- the actual TRX totals are parsed rather than inferred from the green badge.

- [ ] **Step 3: Review the complete diff**

Confirm no change outside:

- the repair design and plan;
- the shared Windows path helper;
- the Route A GenAI cache comparison;
- module-export/gate lists;
- focused tests.

---

### Task 5: Open, review, and integrate the repair

**Files:**
- Create a detailed pull request from `fix/workbook-05-genai-cmake-path-comparison` to `main`.

**Interfaces:**
- Consumes: exact verified branch head.
- Produces: auditable PR and, after explicit approval, a merge commit on `main`.

- [ ] **Step 1: Open a detailed draft PR**

Explain:

- run `31322416220`, attempt `1`, artifact identity and digest;
- configure exit `0` and exact cache values;
- why separator-sensitive raw text caused the false block;
- test-first red and green evidence;
- every modified file;
- unchanged scientific and security boundaries;
- what remains unproven until the live GenAI build.

- [ ] **Step 2: Wait for all checks on the exact PR head**

Do not treat stale checks from an earlier SHA as evidence.

- [ ] **Step 3: Review PR diff and threads**

Resolve only evidence-backed issues. Keep the PR draft until all mandatory checks are green.

- [ ] **Step 4: Merge only with explicit project-owner approval**

Use the exact verified head SHA.

- [ ] **Step 5: Verify a fresh post-merge `main` application run and retained TRX**

---

### Task 6: Re-run only Route A GenAI

**Files:**
- No repository changes unless a new first divergence is proven.

**Interfaces:**
- Consumes: accepted Runtime installation and decision file.
- Produces: a new Route A GenAI build evidence artifact.

- [ ] **Step 1: Dispatch the existing documented-build workflow**

```text
stage: route-a-genai
run_identity: phase2
runtime_install_directory: C:\w5a\phase2-31261978552-2\i-ov
runtime_decision_path: C:\w5a\accepted-route-a-runtime-31261978552-2\decision.json
```

Leave all Route B fields blank/false.

- [ ] **Step 2: Accept only complete evidence**

Require:

- configure advances past the CMake cache check;
- build and install exit `0`;
- `decision.json` says `Passed` for `genai` and exact commit `bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0`;
- text-only artifact uploads;
- independent artifact digest equals GitHub’s recorded digest;
- hosted untrusted-data validation succeeds;
- all model/performance/quality authorisation flags remain false.

- [ ] **Step 3: Preserve the accepted GenAI handoff**

Retain the artifact, digest, run/attempt, install directory, source commit, decision file, and decision SHA-256 under a durable `C:\w5a` path. Do not rebuild it later when the full experiment identity is unchanged.