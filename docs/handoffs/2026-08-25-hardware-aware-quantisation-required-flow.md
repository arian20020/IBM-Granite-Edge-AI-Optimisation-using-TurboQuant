# Hardware-aware quantisation-required flow handoff

**Date:** 2026-08-25

**Final verification update:** 2026-08-26

**Branch:** `fix/hardware-inspection-loq-baseline`

**Implementation base:** `9c9a665ea5397c0a1d09f9ba74543a7891a4d206`

**Task 9 review base:** `735da68de3ff882f12595561e591d39234eaa491`

**Reviewed code tip:** `478a0a77c6aad84d7f30047e092d481ad71bdc00`

**Disposition:** feature slice complete; all final core and packaged regressions
pass with non-zero discovery and zero failures or skips.

## Delivered behavior

The compatibility stage now evaluates the imported/current setup separately
from capability-admitted alternatives. It distinguishes four outcomes:

1. the current setup fits and no quantisation is required;
2. the current setup does not fit but an admitted alternative fits, producing
   the exact quantisation-required journey;
3. no admitted alternative fits; and
4. the available evidence is insufficient, which remains fail closed rather
   than being presented as a no-fit conclusion.

Both GGUF and OpenVINO alternatives are generated from complete route and
hardware authority. Each visible optimization mode uses the established Model
Download labels, describes expected quality, and reports the weight/cache
configuration, system/shared-memory peak, dedicated-memory use where
established, safe budget, headroom, working-storage obligation, and persistent
artifact behavior. Q2_K and experimental TurboQuant choices remain controlled
fallbacks rather than silent substitutions.

The visible transition is still fail closed. Compatibility can create an exact,
path-free version-three `OptimizationSelectionHandoff`, but no downstream
optimization destination is installed by this increment. Its action therefore
remains visibly disabled as `Coming later` and cannot navigate.

## Task 9 changes

### Commits

| Commit | Purpose |
|---|---|
| `c07b6583a2fc9066384ed90a6e15d6795c88279a` | isolates the OpenVINO folder picker so the full packaged run cannot block on an interactive picker |
| `322427748594c6f425f38472c65cadcdabb3776e` | removes empty compatibility cards and makes essential evidence reflow at compact width and 200% text |
| `3d727bc87b2cba7c66e6b0418e646d7151f98634` | retains the exact `CreateProcess` handle as race-free exit-code authority and adds deterministic/stress coverage |
| `45dc3648f20d439426b321eb4eee062ded1dfb67` | covers the valid injected OpenVINO picker route, shared selection lifecycle, and path-private navigation payload |
| `478a0a77c6aad84d7f30047e092d481ad71bdc00` | keeps the new lifecycle stress outside the separately frozen Gate 8 acceptance inventory |

### Changed paths after the Task 9 review base

```text
IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/CompatibilityPage.xaml
IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/CompatibilityPage.xaml.cs
IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelImportPage.xaml.cs
docs/handoffs/2026-08-25-hardware-aware-quantisation-required-flow.md
infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Processes/ExternalProcessRunner.cs
infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Processes/NativeProcessExitCodeAuthority.cs
infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Processes/WindowsSuspendedProcess.cs
tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Processes/ExternalProcessRunnerPackagedTests.cs
tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Processes/NativeProcessExitCodeAuthorityTests.cs
tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/Visual/CompatibilityRenderedStateTests.cs
tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/FileImport/ModelFilePickerTests.cs
```

The compatibility change constrains the page to the real viewport, stacks the
assessment and four fact tiles at their compact breakpoints, wraps essential
labels and values instead of ellipsizing them, and hides facts/runtime/check
surfaces which have no visible content. No decision arithmetic, route
selection, navigation, or execution behavior changed.

The process-lifecycle correction does not widen process capabilities. The
exact native process handle returned by `CreateProcess` is transferred once
into an owned `SafeHandle`, retained until result capture and cleanup finish,
and queried through `GetExitCodeProcess`. The PID-derived managed `Process`
remains only a wait/cleanup helper. A failed native query or `STILL_ACTIVE`
after an observed exit now fails closed as a bounded `CleanupFailed` result;
no exception, guessed exit code, path, or raw operating-system error escapes.
Cancellation, timeout, Job Object tree cleanup, independent output bounds, and
output-drain ordering are unchanged.

## Contract continuity

### Frozen version-two golden vectors

The complete core suite executes the frozen v2 tests. The vectors remain:

| Route/vector | SHA-256 |
|---|---|
| OpenVINO standard | `6d5902369602fb3b9b12c63f003890794488864c79323476c463610e9606270e` |
| OpenVINO legacy TurboQuant identity | `d037be18e90cba1d7d7d9dec28140cd43a91dc41d7c69686e7f0e68ee4f5dbcf` |
| GGUF Q3_K_M | `fd3699ffb644b9a8a15ec6aa4080f79779c78fb2a42461cdc7356180e007eb10` |

Version two still rejects version-three-only cache precision, Q2_K, and
TurboQuant payload shapes. No v2 canonical field order or digest was changed.

### Version-three contract

`OptimizationExecutionPlan.CurrentContractVersion` is `3`; the minimum
executable version remains `2`. Version three binds:

- immutable journey identity: Model inspection run/handoff, model SHA-256 and
  byte length, product Hardware run, and Hardware snapshot SHA-256;
- exact capability snapshot, workload, admitted candidate, user preference,
  route-specific execution payload, freshness and disk authority;
- full GGUF/OpenVINO runtime, device, cache, conversion, package, optimizer,
  evidence, and experimental identity needed to execute without defaults;
- a canonical `ConfigurationSha256`, recomputed before execution; and
- a new plan ID on reissue rather than amendment or substitution.

The frozen OpenVINO TBQ4 v3 vector remains
`77d980c983a9fc08d4ba1c8318c57a1a473f0a7983dd580c1294f6a77a3ae770`.

The UI boundary is the six-property immutable
`OptimizationSelectionHandoff`: `ModelInspectionRunId`,
`ModelInspectionHandoffId`, `ProductHardwareRunId`, `OptimizationPlanId`,
`ConfigurationSha256`, and the exact `Plan`. Creation rechecks the complete
current binding, snapshot identity/digest, route, source, preference, plan ID,
and canonical digest. It carries no path or filename.

### Q2_K and TurboQuant gates

- GGUF Q2_K is a persistent-weight fallback. It is admitted only with exact
  quantizer/runtime/capability evidence, requires explicit quality warning and
  consent, and is bound into the selected plan.
- GGUF TurboQuant3Bit is cache compression, not weight quantisation. It requires
  an experimental capability plus the exact pinned backend implementation and
  source identity; ordinary capabilities cannot advertise it.
- OpenVINO TBQ4/TBQ3 are cache algorithm/precision choices, not model-weight
  formats. They require experimental opt-in evidence and an exact
  `TurboQuantBuildIdentity` (source commit, implementation commit, patch-series
  digest, and runtime-manifest digest).
- TBQ3 and the lowest-memory fallbacks are visibly lower-quality/experimental;
  the acceptable-quality floor may exclude them. No candidate is inferred from
  a name or installed-tool assumption.

## Verification

### Core Release suite

```powershell
dotnet test --project '.\tests\UnitTests\GraniteEdgeAI.ModelHardwareCompatibility.Tests\GraniteEdgeAI.ModelHardwareCompatibility.Tests.csproj' --configuration Release
```

Result: **1,038 total, 1,038 passed, 0 failed, 0 skipped**.

An earlier invocation which added unsupported VSTest-style logger arguments to
this Microsoft.Testing.Platform project ran zero tests and exited 5. It is not
counted as verification; the exact supported plan command above is the result
of record.

### Packaged Debug/x64 build

```powershell
$env:PATH = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer;' + $env:PATH
$testProject = '.\tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj'
dotnet restore $testProject --runtime win-x64 -p:Platform=x64 --disable-build-servers -m:1
dotnet build $testProject --configuration Debug --no-restore --runtime win-x64 `
  -p:Platform=x64 -p:BuildInParallel=false --disable-build-servers -m:1
```

Result: **succeeded, 0 errors, 28 existing warnings**. The warnings are the
existing missing publish-profile, nullable-test, and obsolete MSTest data-row
warnings; Task 9 introduced none.

### Packaged VS Community VSTest

Runner:
`C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe`

Recipe:
`C:\GEAI-LOQ\tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe`

The complete suite was run with x64 and this one-worker MSTest setting:

```xml
<RunSettings>
  <MSTest>
    <Parallelize>
      <Workers>1</Workers>
      <Scope>MethodLevel</Scope>
    </Parallelize>
  </MSTest>
</RunSettings>
```

Command of record:

```powershell
& $vstest $recipe '/Platform:x64' "/Settings:$runsettings" `
  '/Logger:trx;LogFileName=full-serial-native-handle-final-green.trx' `
  "/ResultsDirectory:$resultsPath"
```

Final result: **1,430 total, 1,430 passed, 0 failed, 0 skipped** in
**9.1783 minutes**.

TRX of record:
`C:\GEAI-LOQ\TestResults\HardwareAwareOptimisation\ProcessLifecycle\full-serial-native-handle-final-green.trx`.

The final run includes the complete ModelHardwareCompatibility and Onboarding
inventories with no unexpected skips. It also executes the 64-iteration
short-lived-process stress and both LlmFit tests which originally exposed the
race, late in the same long-lived one-worker host; all passed.

#### Process-lifecycle correction evidence

The earlier two **1,424-test** runs remain useful RED evidence. They failed in
different short-lived LlmFit tests at the same stack:
`System.Diagnostics.Process.ExitCode` reported `Process was not started by
this object`. Source tracing found that `WindowsSuspendedProcess` closed the
authoritative `CreateProcess` handle during launch, then
`ExternalProcessRunner` later depended on a PID-derived managed `Process` to
reacquire exit status after the child had left the process table. This was a
real ownership race, not an environment or long-host limitation.

Before implementation, the deterministic authority tests failed to compile
because `NativeProcessExitCodeAuthority` did not exist. After the minimal
ownership correction:

- `process-lifecycle-green-2.trx`: **5/5 passed**, including native-query
  success, query-failure and `STILL_ACTIVE` fail-closed cases, source custody,
  and 64 consecutive short-lived runs;
- `process-lifecycle-broad-green.trx`: **24/24 passed**, covering every
  `ExternalProcessRunner` terminal/cleanup path and all seven LlmFit real
  boundaries; and
- `process-and-picker-green.trx`: **6/6 passed**, combining lifecycle evidence
  with the valid path-private injected OpenVINO picker route.

The first post-correction full run executed all 1,430 tests and found one test
metadata regression: the stress method had accidentally joined the frozen
Gate 8 acceptance category, producing an inventory count of 88 instead of 87.
No runtime test failed. Removing only that category restored the exact
inventory; `inventory-and-stress-green.trx` then passed **2/2**, and the final
complete run above passed **1,430/1,430**.

The focused final packaged ModelHardwareCompatibility suite remains
**105/105** at
`C:\GEAI-LOQ\TestResults\HardwareAwareOptimisation\Debug\Task9-Compatibility-Final\compatibility.trx`.

## Native fixture-gallery evidence

The gallery build used:

```powershell
dotnet build '.\IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj' --configuration Debug -p:Platform=x64 -p:CompatibilityFixtureGallery=true --runtime win-x64 --no-restore
```

Result: succeeded, 0 errors, one existing `NETSDK1198` warning.

Evidence root:
`C:\GEAI-LOQ\TestResults\HardwareAwareOptimisation\Debug\Task9-Gallery-Corrected`

| Scale | Fixtures | Maximized | Minimum | Manifest SHA-256 |
|---|---:|---|---|---|
| normal | 26 | 26 at 1936x1048 | 26 at 600x900 | `8F2F6F7652C7EB6BDC23E6DBEC189A5E9A7D2530D9E188E90159E3CC7C70409D` |
| 200% text | 26 | 26 at 1936x1048 | 26 at 600x900 | `D6BC4A9D600B3CC9B8D24069888AE3B8C86575E6FF5F4D86C863F5B222AE9380` |

All **104 native screenshots** were reviewed through the individual frames and
four generated contact sheets. Capture fails unless the exact WinUI window is
visible and owns the foreground for every frame; every maximized/minimum pair
has different dimensions and a different SHA-256. An earlier evidence set was
discarded when this review caught background-window contamination.

Representative corrected frames:

- `normal\CMP-022-min-600x900.png` — SHA-256
  `8BF7FAC5E762EFBD69DE39E7ADE82A53FA452B242AD870593D7ACDDE0C755692`
- `text-200\CMP-022-min-600x900.png` — SHA-256
  `874000E46659E52AE6BEE25197F88D3B3E81982D6EE2E8F6A2D7D7B0C597BF59`
- `normal\CMP-050-min-600x900.png` — SHA-256
  `0A2C244FD128E8CDA90B619830E75EE98F89215A9D31C560FB95C85F464DD1E8`
- `text-200\CMP-010-min-600x900.png` — SHA-256
  `8AE9CAFDCE2B39237D77EB7A126F05E41672DFD0577B67E95C5ACEFB73455942`

Visual disposition: the product surface is a modern light, centered,
single-shell page; compact and 200% layouts wrap/stack instead of clipping;
the footer remains outside this page rather than being duplicated; Q2_K and
TBQ3 warnings are prominent; icons, labels, state text, focus order,
screen-reader names, and 44px targets retain their tested contracts; empty
assessment cards are suppressed. The current/recommended memory arithmetic and
segment labels reconcile in the rendered-state tests, with safe budget shown
separately. Long screens use expected vertical scrolling, not horizontal
clipping.

## Privacy and process-control scans

```powershell
rg -n "Process\.GetProcesses|\.Kill\(|TerminateProcess|ManagementObjectSearcher" 'IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility'
git diff -U0 80a67bc78a343c6589d1c1f6b3be98092af81ecd..478a0a77c6aad84d7f30047e092d481ad71bdc00 -- 'infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Processes' | rg -n '^\+.*(Process\.GetProcesses|\.Kill\(|TerminateProcess|ManagementObjectSearcher)'
rg -n "[A-Za-z]:\\|Users\\|Downloads\\" 'IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility' 'shared/GraniteEdgeAI.ModelHardwareCompatibility.Core'
git diff --check
```

All three scans returned no matches. `git diff --check` passed. Compatibility
does not enumerate or terminate user processes and emits no local path-bearing
UI; the lifecycle correction added no process enumeration or termination
capability.

## Explicit downstream non-scope

This increment does **not** implement or authorize:

- GGUF or OpenVINO optimizer/quantizer execution;
- an optimization progress screen;
- persistent model/package export or download;
- Chat integration;
- execution of Q2_K, TBQ3/TBQ4, or any other selected candidate; or
- silent process termination to free memory.

The next worker must consume the exact v3 handoff, revalidate all authority at
the execution boundary, and either execute the confirmed plan exactly or fail
closed. It must not infer defaults, widen TurboQuant gates, or reinterpret
compatibility estimates as measured execution evidence.
