# Hardware Inspection Current-Baseline Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to execute this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port the approved Hardware Inspection domain contracts and complete fifteen-state WinUI presentation from `feature/hardware-inspection-page-v1` onto the freshly verified Model Inspection baseline without changing either feature's behaviour.

**Architecture:** Preserve the Hardware work as a Hardware-owned vertical slice under `Features/HardwareInspection/**` and preserve the original commit sequence so its review history remains inspectable. Integrate it on `feature/hardware-inspection-functional-v1`, based on Model Inspection commit `ba4fd7bad5c473208248247fcba27e6f22c356ab`; do not add navigation, services, providers, compatibility calculations, or external execution in this plan.

**Tech Stack:** C# 12, .NET 8, WinUI 3 / Windows App SDK 2.2, XAML resource dictionaries and controls, MSTest 4.3.2 packaged AppContainer tests, Python contract tests, Git.

**Spec:** `docs/superpowers/specs/2026-08-15-hardware-inspection-production-design.md`

## Global Constraints

- Execute only in `C:\hi-functional` on `feature/hardware-inspection-functional-v1`.
- The starting commit is exactly `ba4fd7bad5c473208248247fcba27e6f22c356ab`.
- The approved Hardware source is exactly `feature/hardware-inspection-page-v1` commit `ed8bc75881b2637beda6fe5689611a46fb00bf2b`.
- Preserve the Hardware commit sequence from `d80d3313` through `ed8bc758` in first-parent order; do not squash or re-author production/test commits.
- Preserve all Model Inspection files byte-for-byte relative to the starting commit.
- Hardware changes are limited to `Features/HardwareInspection/**`, `tests/UnitTests/.../HardwareInspection/**`, the two Hardware Python contracts, and Hardware-owned design/plan/contract documents already present in the approved source range.
- Do not edit Model Inspection, Model Import, Onboarding, `App.xaml`, project files, shared protocols, Block 3 compatibility code, workflow files, or external-tool code.
- Do not run hardware probes, LLM Fit, llama.cpp, a candidate, a workflow, the laptop, or any network-changing command.
- Gate 1 remains Blocked and Gate 2 remains prohibited; this is repository integration only.
- Existing test identities and data rows must remain unchanged.
- Test outputs belong under ignored `TestResults/HardwareInspection/CurrentBaselineIntegration/`.

---

## File and Responsibility Map

### Approved Hardware source range

- `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Domain/**`: immutable canonical Hardware facts, evidence, and snapshot contracts.
- `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Application/**`: outcome, handoff, and current-memory provider seams.
- `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/**`: immutable presentation state, copy, theme, factories, and Hardware-owned controls.
- `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/HardwareInspectionPage.xaml(.cs)`: static page composition only; no run or navigation ownership.
- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/**`: packaged WinUI/domain tests.
- `tests/testing/hardware_inspection/test_hardware_inspection_contract_v1.py`: source/contract boundary checks.
- `tests/testing/hardware_inspection/test_hardware_inspection_theme_contract.py`: theme and visual-authority boundary checks.

### Explicitly protected current-baseline surfaces

- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/**`
- `IBM Granite with TurboQuant (Intel)/Features/ModelImport/**`
- `IBM Granite with TurboQuant (Intel)/Features/Onboarding/**`
- `IBM Granite with TurboQuant (Intel)/App.xaml*`
- both application and packaged-test project files

---

### Task 1: Record the Approved Production Authority

**Files:**

- Create: `docs/superpowers/specs/2026-08-15-hardware-inspection-production-design.md`
- Create: `docs/superpowers/plans/2026-08-21-hardware-inspection-current-baseline-integration.md`

**Interfaces:**

- Consumes: exact approved production-design blob from commit `19c53b214c1d8232ac664ed3a667cfadcb09f305`.
- Produces: a local immutable authority path for every later Hardware implementation plan.

- [ ] **Step 1: Verify the imported production design is byte-identical.**

Run:

```powershell
$expected = git show '19c53b214c1d8232ac664ed3a667cfadcb09f305:docs/superpowers/specs/2026-08-15-hardware-inspection-production-design.md'
$actual = Get-Content -LiteralPath 'docs/superpowers/specs/2026-08-15-hardware-inspection-production-design.md' -Raw
if (($expected -join "`n").TrimEnd("`r", "`n") -cne $actual.TrimEnd("`r", "`n")) {
    throw 'Imported production design differs from the approved blob.'
}
```

Expected: no output and exit code 0.

- [ ] **Step 2: Verify strict document encoding and scope.**

Run:

```powershell
$paths = @(
  'docs/superpowers/specs/2026-08-15-hardware-inspection-production-design.md',
  'docs/superpowers/plans/2026-08-21-hardware-inspection-current-baseline-integration.md'
)
foreach ($path in $paths) {
    $bytes = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $path))
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        throw "$path has a UTF-8 BOM."
    }
    $text = [Text.UTF8Encoding]::new($false, $true).GetString($bytes)
    if ($text.Contains("`r") -or -not $text.EndsWith("`n") -or $text.EndsWith("`n`n")) {
        throw "$path does not use LF-only with one final newline."
    }
}
```

Expected: no output and exit code 0.

- [ ] **Step 3: Commit the two authority documents.**

```powershell
git add -- `
  'docs/superpowers/specs/2026-08-15-hardware-inspection-production-design.md' `
  'docs/superpowers/plans/2026-08-21-hardware-inspection-current-baseline-integration.md'
git diff --cached --check
git commit -m 'docs(hardware-inspection): plan current baseline integration'
```

Expected: exactly the two documentation paths are committed.

---

### Task 2: Port the Reviewed Hardware Contract and Visual History

**Files:**

- Create: the exact Hardware-owned files introduced by first-parent commits `d80d3313..ed8bc758`.
- Protect: every non-Hardware path named in Global Constraints.

**Interfaces:**

- Consumes: reviewed source range after parent `1dc0a048` through `ed8bc758`.
- Produces: the same Hardware domain/application/presentation public surface on the current Model baseline.

- [ ] **Step 1: Generate and verify the exact first-parent commit list.**

Run:

```powershell
$commits = @(git rev-list --reverse --first-parent '1dc0a048..ed8bc758')
if ($commits.Count -ne 34) { throw "Expected 34 Hardware commits, got $($commits.Count)." }
if ($commits[0] -notlike 'd80d3313*' -or $commits[-1] -notlike 'ed8bc758*') {
    throw 'Hardware commit boundary mismatch.'
}
```

Expected: no output and exactly 34 commits.

- [ ] **Step 2: Cherry-pick the reviewed sequence in order.**

Run:

```powershell
$commits = @(git rev-list --reverse --first-parent '1dc0a048..ed8bc758')
foreach ($commit in $commits) {
    git cherry-pick $commit
    if ($LASTEXITCODE -ne 0) { throw "Cherry-pick failed at $commit." }
}
```

Expected: all 34 commits apply without editing Model, Model Import, Onboarding, shared, project, or App paths.

- [ ] **Step 3: Verify the ported Hardware blobs exactly match the approved source head.**

Run:

```powershell
$paths = @(git ls-tree -r --name-only ed8bc758 -- `
  'IBM Granite with TurboQuant (Intel)/Features/HardwareInspection' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection' `
  'tests/testing/hardware_inspection/test_hardware_inspection_contract_v1.py' `
  'tests/testing/hardware_inspection/test_hardware_inspection_theme_contract.py')
foreach ($path in $paths) {
    $expected = git rev-parse "ed8bc758:$path"
    $actual = git rev-parse "HEAD:$path"
    if ($expected -ne $actual) { throw "Ported blob mismatch: $path" }
}
```

Expected: no output; every source blob is identical.

- [ ] **Step 4: Verify protected Model baseline bytes.**

Run:

```powershell
$protected = @(git ls-tree -r --name-only ba4fd7ba -- `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelImport' `
  'IBM Granite with TurboQuant (Intel)/Features/Onboarding' `
  'IBM Granite with TurboQuant (Intel)/App.xaml' `
  'IBM Granite with TurboQuant (Intel)/App.xaml.cs' `
  'IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj')
foreach ($path in $protected) {
    if ((git rev-parse "ba4fd7ba:$path") -ne (git rev-parse "HEAD:$path")) {
        throw "Protected baseline changed: $path"
    }
}
```

Expected: no output.

---

### Task 3: Run Hardware Contract and Packaged UI Gates

**Files:**

- Verify only; no production edits are expected.
- Test output: `TestResults/HardwareInspection/CurrentBaselineIntegration/`.

**Interfaces:**

- Consumes: the ported Hardware files compiled inside the current app/test projects by SDK default item discovery.
- Produces: executable proof that Hardware contracts and all approved WinUI controls load on the current Model baseline.

- [ ] **Step 1: Run both Python Hardware contract modules.**

```powershell
python -B -m unittest -v `
  tests.testing.hardware_inspection.test_hardware_inspection_contract_v1 `
  tests.testing.hardware_inspection.test_hardware_inspection_theme_contract
```

Expected: all discovered tests pass with zero errors/failures.

- [ ] **Step 2: Build the current app and packaged test project.**

```powershell
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -products * -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'MSBuild was not found.' }
$app = 'IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj'
$tests = 'tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj'
& $msbuild $app /target:Restore,Build /maxCpuCount /verbosity:minimal /property:Configuration=Debug /property:Platform=x64
if ($LASTEXITCODE -ne 0) { throw 'App build failed.' }
dotnet build $tests --configuration Debug --runtime win-x64 -p:Platform=x64 --no-restore --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
```

Expected: both builds succeed with zero errors.

- [ ] **Step 3: Run the exact packaged Hardware class filter.**

```powershell
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vstest = & $vswhere -latest -products * -find '**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' | Select-Object -First 1
$recipe = 'tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe'
$results = 'TestResults\HardwareInspection\CurrentBaselineIntegration\hardware-focused'
if (Test-Path -LiteralPath $results) { throw 'Focused result directory must be fresh.' }
New-Item -ItemType Directory -Path $results | Out-Null
& $vstest $recipe /Platform:x64 "/ResultsDirectory:$results" '/Logger:trx;LogFileName=hardware-focused.trx' '/TestCaseFilter:FullyQualifiedName~HardwareInspection'
if ($LASTEXITCODE -ne 0) { throw 'Packaged Hardware suite failed.' }
```

Expected: every Hardware Inspection packaged test passes; no Model test is modified to make it pass.

---

### Task 4: Prove the Combined Model and Hardware Baseline

**Files:**

- Verify only; no production edits are expected.
- Test output: `TestResults/HardwareInspection/CurrentBaselineIntegration/full/`.

**Interfaces:**

- Consumes: the combined accepted Model baseline and ported Hardware slice.
- Produces: regression evidence suitable for starting the service/ViewModel plan.

- [ ] **Step 1: Run the complete unfiltered packaged suite.**

```powershell
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vstest = & $vswhere -latest -products * -find '**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' | Select-Object -First 1
$recipe = 'tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe'
$results = 'TestResults\HardwareInspection\CurrentBaselineIntegration\full'
if (Test-Path -LiteralPath $results) { throw 'Full result directory must be fresh.' }
New-Item -ItemType Directory -Path $results | Out-Null
& $vstest $recipe /Platform:x64 "/ResultsDirectory:$results" '/Logger:trx;LogFileName=combined-full.trx'
if ($LASTEXITCODE -ne 0) { throw 'Combined packaged suite failed.' }
```

Expected: all discovered tests pass with zero failed/error/timeout/aborted/not-executed results.

- [ ] **Step 2: Verify scope, whitespace, and cleanliness.**

```powershell
git diff --check ba4fd7ba..HEAD
if ($LASTEXITCODE -ne 0) { throw 'Integrated range has whitespace errors.' }
if (git status --porcelain) { throw 'Worktree is not clean.' }
```

Expected: no output and clean worktree.

- [ ] **Step 3: Record the integration handoff.**

Report:

- branch and immutable HEAD;
- exact source and Model baseline SHAs;
- imported Hardware commit count and blob-identity result;
- Python contract counts;
- app/test build result;
- focused Hardware and full packaged TRX counters and hashes;
- protected Model byte-identity result;
- explicit non-claims: no functional service/provider/navigation, hardware probe, external tool, laptop, candidate, workflow, network change, Gate 1 closure, or Gate 2 entry.

---

## Next Plan Boundary

After this plan is green, write and execute `Hardware Inspection Run Lifecycle v1` against this exact combined HEAD. That plan owns `IHardwareInspectionService`, immutable run/progress/result contracts, one-run ViewModel lifecycle, cancellation, retry, stale-event rejection, presentation mapping, and a candidate-free test service. Real Windows/DXGI/LLM Fit/llama.cpp providers and external-process security remain separate gate plans and must not be fabricated inside the lifecycle slice.
