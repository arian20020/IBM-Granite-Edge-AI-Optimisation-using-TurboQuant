# Workbook 05 Shared CMake Cache Parser Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the duplicated Route A/Route B CMake-cache readers with one shared PowerShell parser that accepts legitimate blank cache lines, and prove the behavior with an executable Windows PowerShell regression before another Lenovo OpenVINO build is allowed.

**Architecture:** Add `Get-Wb05CMakeCacheValue` to the reviewed `Workbook05.Build.psm1` public boundary, make Route A Runtime and Route B call that one primitive, and execute a focused `*.Tests.ps1` regression through the existing Workbook 05 repository gate. Keep cache-file I/O and required-value policy at the callers so the shared parser remains small and single-purpose.

**Tech Stack:** Windows PowerShell 5.1, Python 3.12 `unittest`, GitHub Actions, CMake-generated `CMakeCache.txt`, existing Workbook 05 build/evidence tooling.

## Global Constraints

- Runtime source stays `openvinotoolkit/openvino@b9a1f201c109e0bed74763934f79483cf6c4cbf4`.
- GenAI source stays `openvinotoolkit/openvino.genai@05e5c7670b597746f858946974d11f38e3baf42f`.
- Route B source and BR8 prerequisite/owner-acceptance rules do not change.
- CMake generator, CPU-only flags, `--parallel 2`, short workspace roots, install roots, and tool pins do not change.
- GitHub runner labels/timeouts, read-only permissions, immutable action SHAs, and text/data-only artifact boundaries do not change.
- The hosted artifact validator remains fail-closed and unchanged.
- No model execution, QJL/PolarQuant/TurboQuant activation, packed-storage, no-fallback, memory/context, TTFT/tok/s, or quality claim is authorised by this repair.
- New/changed code and tests include beginner-readable comments around every logical block.

---

### Task 1: Add a real RED PowerShell regression and shared-consumer contract

**Files:**
- Create: `tests/testing/workbook05/Invoke-BuildCMakeCacheParserTests.Tests.ps1`
- Create: `tests/testing/workbook05/test_build_cmake_cache_parser_contract.py`

**Interfaces:**
- Consumes: `scripts/testing/workbook05/Workbook05.Build.psm1`, `Invoke-Workbook05RouteARuntimeBuild.ps1`, and `Invoke-Workbook05RouteBBuild.ps1`.
- Produces: an executable parser behavior test plus a static architecture test that prevents the private duplicated parsers from returning.

- [ ] **Step 1: Create the executable PowerShell regression with no production changes**

```powershell
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Resolve the repository root and import the exact real shared build module.
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$modulePath = Join-Path $repositoryRoot 'scripts\testing\workbook05\Workbook05.Build.psm1'
Import-Module $modulePath -Force -ErrorAction Stop

function Assert-Equal {
    param(
        [Parameter(Mandatory = $true)] [object]$Actual,
        [Parameter(Mandatory = $true)] [object]$Expected,
        [Parameter(Mandatory = $true)] [string]$Message
    )

    # Fail with a readable message instead of relying on an external test framework.
    if ($Actual -ne $Expected) {
        throw "$Message Expected '$Expected', found '$Actual'."
    }
}

# Reproduce the production shape: a string array that contains legitimate blank
# CMake cache lines between ordinary NAME:TYPE=value entries.
$cacheLines = @(
    'CMAKE_GENERATOR:INTERNAL=Visual Studio 17 2022',
    '',
    'ENABLE_INTEL_GPU:BOOL=OFF',
    '',
    'Python3_EXECUTABLE:FILEPATH=C:\Program Files\Python312\python.exe',
    'WB05_EQUALS_VALUE:STRING=left=right'
)

# The current branch is expected to fail here during RED because the shared
# function does not exist yet. After GREEN these calls prove real behavior.
Assert-Equal `
    -Actual (Get-Wb05CMakeCacheValue -Lines $cacheLines -Name 'CMAKE_GENERATOR') `
    -Expected 'Visual Studio 17 2022' `
    -Message 'The parser must ignore blank lines and preserve the generator value.'
Assert-Equal `
    -Actual (Get-Wb05CMakeCacheValue -Lines $cacheLines -Name 'ENABLE_INTEL_GPU') `
    -Expected 'OFF' `
    -Message 'The parser must return the requested Boolean cache value.'
Assert-Equal `
    -Actual (Get-Wb05CMakeCacheValue -Lines $cacheLines -Name 'Python3_EXECUTABLE') `
    -Expected 'C:\Program Files\Python312\python.exe' `
    -Message 'The parser must preserve Windows path punctuation.'
Assert-Equal `
    -Actual (Get-Wb05CMakeCacheValue -Lines $cacheLines -Name 'WB05_EQUALS_VALUE') `
    -Expected 'left=right' `
    -Message 'The parser must split only on the first equals sign.'

# A missing key is not invented; the caller decides later whether it blocks.
$missing = Get-Wb05CMakeCacheValue -Lines $cacheLines -Name 'DOES_NOT_EXIST'
if ($null -ne $missing) {
    throw 'The parser must return null for a missing cache key.'
}

# Leave a controlled native-style success code for the repository gate.
$global:LASTEXITCODE = 0
Write-Host 'Workbook 05 CMake cache parser PowerShell tests passed.'
```

- [ ] **Step 2: Create the shared-consumer contract**

```python
from __future__ import annotations

import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
MODULE = REPOSITORY_ROOT / "scripts/testing/workbook05/Workbook05.Build.psm1"
RUNTIME = REPOSITORY_ROOT / "scripts/testing/workbook05/Invoke-Workbook05RouteARuntimeBuild.ps1"
ROUTE_B = REPOSITORY_ROOT / "scripts/testing/workbook05/Invoke-Workbook05RouteBBuild.ps1"


class BuildCMakeCacheParserContractTests(unittest.TestCase):
    def test_shared_module_exports_cache_parser_and_both_routes_use_it(self) -> None:
        module_text = MODULE.read_text(encoding="utf-8", errors="strict")
        runtime_text = RUNTIME.read_text(encoding="utf-8", errors="strict")
        route_b_text = ROUTE_B.read_text(encoding="utf-8", errors="strict")

        # Require one reviewed shared primitive, not route-local parser copies.
        self.assertIn("function Get-Wb05CMakeCacheValue", module_text)
        self.assertIn("'Get-Wb05CMakeCacheValue'", module_text)
        self.assertIn("Get-Wb05CMakeCacheValue -Lines $cacheLines -Name $name", runtime_text)
        self.assertIn("Get-Wb05CMakeCacheValue -Lines $cacheLines -Name $name", route_b_text)
        self.assertNotIn("function Get-CMakeCacheValue", runtime_text)
        self.assertNotIn("function Get-CMakeCacheValue", route_b_text)


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 3: Commit the RED-only tests**

Commit message:

```text
test: reproduce Workbook 05 CMake cache parser failure
```

- [ ] **Step 4: Open a draft PR only to trigger CI**

Expected PR state: design + plan + tests only; no production repair.

- [ ] **Step 5: Verify RED on the exact test-only head**

Expected failure: the executable PowerShell test cannot resolve `Get-Wb05CMakeCacheValue`, and the architecture test reports that the shared function/export/callers are absent. Reject the RED checkpoint if syntax, checkout, Python dependency setup, or the test harness itself fails first.

---

### Task 2: Add the shared parser and export it

**Files:**
- Modify: `scripts/testing/workbook05/Workbook05.Build.psm1`
- Modify: `scripts/testing/Validate-Workbook05-BuildStage.ps1`
- Test: `tests/testing/workbook05/Invoke-BuildCMakeCacheParserTests.Tests.ps1`

**Interfaces:**
- Produces: `Get-Wb05CMakeCacheValue -Lines <string[]> -Name <string> -> string|null`.
- Consumers: Route A Runtime and Route B build orchestrators.

- [ ] **Step 1: Add the minimal shared parser**

```powershell
function Get-Wb05CMakeCacheValue {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string[]]$Lines,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$Name
    )

    # CMake cache entries use NAME:TYPE=value. Blank/comment/unrelated lines are
    # legitimate cache structure and simply do not match the requested key.
    $escapedName = [Regex]::Escape($Name)
    $line = $Lines |
        Where-Object { $_ -match "^${escapedName}:[^=]+=" } |
        Select-Object -First 1

    # Missing values are represented explicitly as null so callers can fail
    # closed according to their own reviewed cache-control policy.
    if ($null -eq $line) {
        return $null
    }

    # Split only once so an equals sign inside a cache value is preserved.
    return ($line -split '=', 2)[1]
}
```

- [ ] **Step 2: Add the function to `Export-ModuleMember`**

The reviewed export list must contain:

```powershell
'Get-Wb05CMakeCacheValue'
```

- [ ] **Step 3: Add the function to the gate's expected export list**

`Validate-Workbook05-BuildStage.ps1` must require:

```powershell
'Get-Wb05CMakeCacheValue'
```

- [ ] **Step 4: Commit the shared primitive**

Commit message:

```text
fix: add shared Workbook 05 CMake cache parser
```

---

### Task 3: Migrate Runtime and Route B to the shared parser

**Files:**
- Modify: `scripts/testing/workbook05/Invoke-Workbook05RouteARuntimeBuild.ps1`
- Modify: `scripts/testing/workbook05/Invoke-Workbook05RouteBBuild.ps1`
- Test: `tests/testing/workbook05/test_build_cmake_cache_parser_contract.py`

**Interfaces:**
- Consumes: exported `Get-Wb05CMakeCacheValue` from `Workbook05.Build.psm1`.
- Produces: unchanged Runtime/Route B cache-summary and fail-closed decision semantics with no duplicate parser implementation.

- [ ] **Step 1: Remove Runtime's private `Get-CMakeCacheValue` function**

Delete only that helper; do not alter source pins, configure arguments, cache-key list, or decision rules.

- [ ] **Step 2: Change Runtime's cache loop to the shared function**

```powershell
foreach ($name in @(
    'CMAKE_GENERATOR',
    'CMAKE_GENERATOR_PLATFORM',
    'ENABLE_INTEL_GPU',
    'ENABLE_INTEL_NPU',
    'ENABLE_TESTS',
    'ENABLE_FUNCTIONAL_TESTS',
    'ENABLE_SAMPLES',
    'ENABLE_PYTHON',
    'ENABLE_WHEEL',
    'Python3_EXECUTABLE'
)) {
    # Read each reviewed control through the one shared parser contract.
    $cacheValues[$name] = Get-Wb05CMakeCacheValue -Lines $cacheLines -Name $name
}
```

- [ ] **Step 3: Remove Route B's duplicate private helper and update its cache loop**

Use the exact same shared call:

```powershell
$cacheValues[$name] = Get-Wb05CMakeCacheValue -Lines $cacheLines -Name $name
```

- [ ] **Step 4: Commit caller migration**

Commit message:

```text
refactor: share Workbook 05 CMake cache parsing
```

---

### Task 4: GREEN verification, review, merge and post-merge acceptance

**Files:**
- No new production files unless verification exposes a defect directly caused by this repair.
- Update PR body with complete evidence before merge.

**Interfaces:**
- Consumes: final repair branch.
- Produces: merge-ready evidence and a post-merge `main` checkpoint.

- [ ] **Step 1: Verify the Workbook 05 gate on the exact final head**

Required evidence:

```text
complete Workbook 05 discovery: all pass
Invoke-BuildCMakeCacheParserTests.Tests.ps1: pass
focused workflow/security contracts: all pass
module import/export gate: pass
git diff --check: pass
WORKBOOK05_BUILD_STAGE_GATE_PASS
```

- [ ] **Step 2: Verify the normal WinUI/application workflow on the same exact head**

Required evidence: application restore/build succeeds, unit-test build succeeds, packaged VSTest has zero failures/errors and retains the legitimate current total (134 at plan creation).

- [ ] **Step 3: Independently verify the uploaded unit-test artifact**

Download the artifact, compare local SHA-256 with GitHub's recorded digest, and inspect the TRX counters independently.

- [ ] **Step 4: Review the exact diff against base `dd48d5fa4c814211238b8748a9a63b25af76d8e5`**

Reject scope drift in source pins, CMake flags, parallelism, workflow permissions/action SHAs, validator policy, Route B BR8 rules, or application production code.

- [ ] **Step 5: Replace the draft PR body with a detailed audit record and mark ready**

Include: live failure/run/artifact IDs, successful configure evidence, root cause, RED head/run, GREEN head/runs, test counts, artifact digests, exact changed files, non-claims, and post-merge boundary.

- [ ] **Step 6: Merge only the verified exact head with expected-head protection**

Use a merge commit so the repair history and RED/GREEN commits remain auditable.

- [ ] **Step 7: Verify fresh `main` after merge**

Require a new push-triggered `Build and test` run on the merge SHA, then independently verify its artifact digest and TRX counters.

- [ ] **Step 8: Only then allow another new manual `route-a-runtime` dispatch**

Do not rerun workflow `31204911650`, because it is bound to the pre-fix commit. The new run must start from the new `main` merge commit.

## Plan Self-Review

- Spec coverage: shared parser, export boundary, Runtime/Route B migration, executable PowerShell regression, fail-closed semantics, RED/GREEN, PR/merge/post-merge verification are all mapped to tasks.
- Placeholder scan: no TODO/TBD or unspecified implementation steps remain.
- Type consistency: `Get-Wb05CMakeCacheValue` is consistently defined and consumed as `-Lines <string[]> -Name <string>`, returning `string` or `$null`.
- Scope: one parser/evidence-control subsystem; no unrelated refactoring is included.
