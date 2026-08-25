# Hardware Inspection Gate 6 Evidence Resolution Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deterministically resolve the existing Gate 3–5 provider evidence into a privacy-safe canonical `HardwareSnapshot` under explicit v1 authority, normalization, tolerance, freshness, consistency, and provenance rules.

**Architecture:** Add an internal x64-only resolution subsystem beneath the application Hardware Inspection infrastructure boundary. Small pure resolvers handle processor, Windows memory/OS, graphics/NPU, and storage/runtime evidence; one aggregate resolver applies global time policy, merges a fixed manifest, and constructs a snapshot only when every critical decision is truthful. Production composition remains fail-closed until Gate 7.

**Tech Stack:** C# 12, .NET 8, Windows x64, MSTest 4, Microsoft Testing Platform AppX VSTest, existing immutable Foundation evidence, `TimeProvider`, Git.

**Spec:** `docs/superpowers/specs/2026-08-24-hardware-inspection-gate-6-resolution-design.md`

## Global Constraints

- Policy ID is exactly `hardware-policy-v1`; snapshot schema is exactly `1`.
- Resolution files live under `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Resolution` and use namespace `GraniteEdgeAI.Features.HardwareInspection.Resolution` so Foundation-dependent code stays in the existing non-x64 exclusion.
- LLM Fit remains the primary overall route, but its `total_ram_gb` is never relabelled as physically installed memory. Windows supplies installed, OS-usable, available, OS, storage, physical-core, architecture, and DXGI memory semantics.
- Future skew is at most 5 seconds; Windows dynamic memory age is at most 30 seconds; other evidence age is at most 5 minutes; accepted-evidence capture span is at most 60 seconds.
- Convert GiB with checked `decimal`, exactly 1,073,741,824 bytes/GiB, and `MidpointRounding.ToEven`.
- CPU logical counts compare exactly. CPU names compare only with `StringComparison.OrdinalIgnoreCase`. Total RAM tolerance is 1 GiB. Available RAM tolerance is `max(2 GiB, 10% of Windows OS-usable bytes)`.
- Never average, clamp, fuzzy-match, infer Intel, select a primary GPU, sum graphics memory, infer an NPU, or fabricate a value required by a Domain constructor.
- A critical conflict/unavailability produces no snapshot. Optional missing evidence remains explicit in the manifest; it is not hardware absence.
- Inputs, results, entries, and diagnostics are immutable bounded copies. Diagnostics are closed enums and fixed lowercase tokens; no exception or provider text crosses the boundary.
- The resolver has no process, shell, network, filesystem, registry, native API, environment, logging, telemetry, model, compatibility, WinUI, or persistence operation.
- Production continues to compose `UnavailableHardwareInspectionService`; Gate 7 alone owns orchestration, cancellation outcomes, progress, and activation.
- Every production change follows RED -> verify expected failure -> GREEN -> refactor. Do not write production code before its failing test.
- Run packaged tests from an actual short physical worktree created with `superpowers:using-git-worktrees`; do not use a junction or `subst` path as AppX execution evidence.

## Packaged-test recipe

For every packaged test step, first build from the exact committed physical-worktree head:

```powershell
$testProject = 'tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj'
$configuration = 'Debug'
$resultDirectory = 'TestResults\HardwareInspection\Gate6Focused'
$recipe = 'tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe'

dotnet restore $testProject --runtime win-x64 -p:Platform=x64
dotnet build $testProject --configuration $configuration --no-restore --runtime win-x64 -p:Platform=x64

New-Item -ItemType Directory -Force -Path $resultDirectory | Out-Null
$recipePath = (Resolve-Path -LiteralPath $recipe).Path
$resultsPath = (Resolve-Path -LiteralPath $resultDirectory).Path
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vstest = & $vswhere -latest -products * -find '**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' |
    Select-Object -First 1
if (-not $vstest) { throw 'Visual Studio app-container test runner was not found.' }

& $vstest $recipePath '/Platform:x64' `
    '/TestCaseFilter:FullyQualifiedName~HardwareResolution' `
    '/Logger:trx;LogFileName=gate6-focused.trx' `
    "/ResultsDirectory:$resultsPath"
if ($LASTEXITCODE -ne 0) { throw "Packaged VSTest failed: $LASTEXITCODE" }
```

Parse every authoritative TRX rather than trusting console summaries:

```powershell
[xml]$trx = Get-Content -LiteralPath `
    'TestResults\HardwareInspection\Gate6Focused\gate6-focused.trx' -Raw
$counters = $trx.TestRun.ResultSummary.Counters
if ([int]$counters.total -ne [int]$counters.executed -or
    [int]$counters.total -ne [int]$counters.passed -or
    @('failed','error','timeout','aborted','inconclusive','notRunnable','notExecuted') |
        Where-Object { [int]$counters.$_ -ne 0 }) {
    throw 'The Gate 6 packaged test ledger is not completely passing.'
}
```

---

### Task 1: Lock the policy, input, and result contracts

**Files:**
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Resolution/HardwareResolutionContractTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Resolution/HardwareResolutionTestData.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Resolution/HardwareResolutionPolicy.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Resolution/HardwareResolutionDiagnostics.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Resolution/WindowsSystemEvidenceObservation.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Resolution/CollectedHardwareEvidence.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Resolution/HardwareEvidenceResolutionResult.cs`

**Interfaces:**
- Produces `HardwareResolutionPolicy.PolicyVersion`, `SchemaVersion`, `FutureClockSkew`, `DynamicMemoryMaximumAge`, `StaticEvidenceMaximumAge`, `MaximumCaptureSpan`, `InstalledMemoryToleranceBytes`, and `MinimumAvailableMemoryToleranceBytes`.
- Produces closed internal enums `WindowsSystemObservationDiagnosticCode` and `HardwareResolutionDiagnosticCode`, plus `HardwareResolutionDiagnosticTokens.Get(HardwareResolutionDiagnosticCode)`.
- Produces `WindowsSystemEvidenceObservation.Available(WindowsSystemSnapshot)` and `.Unavailable(DateTimeOffset, WindowsSystemObservationDiagnosticCode)`.
- Produces immutable `CollectedHardwareEvidence` with exactly the seven source observations in the spec.
- Produces immutable `HardwareEvidenceResolutionResult.Success(HardwareSnapshot, HardwareEvidenceManifest, IEnumerable<HardwareResolutionDiagnosticCode>)` and `.Failure(HardwareEvidenceManifest, IEnumerable<HardwareResolutionDiagnosticCode>)`.

- [ ] **Step 1: Write the RED contract tests**

Test state-machine behavior rather than echoing policy constants. Prove available/unavailable Windows-system observation invariants; all seven collected inputs reject null; result diagnostics are copied, deduplicated, sorted by enum value, bounded by the enum count, and mapped only to 1..96-character lowercase `[a-z0-9.-]` tokens. `Success` requires a usable snapshot; `Failure` exposes no snapshot. The mutations caught are accepting contradictory observation state, retaining a caller-owned diagnostic collection, accepting an undefined enum, or exposing arbitrary diagnostic text. Task 2 proves every policy limit through boundary behavior, so a wrong constant breaks a real decision instead of a change-detector assertion.

```csharp
[TestMethod]
public void Failure_CopiesSortsAndDeduplicatesClosedDiagnostics()
{
    var supplied = new List<HardwareResolutionDiagnosticCode>
    {
        HardwareResolutionDiagnosticCode.StorageUnavailable,
        HardwareResolutionDiagnosticCode.ClockFuture,
        HardwareResolutionDiagnosticCode.StorageUnavailable,
    };
    var result = HardwareEvidenceResolutionResult.Failure(
        new HardwareEvidenceManifest([]),
        supplied);

    supplied.Clear();

    CollectionAssert.AreEqual(
        new[]
        {
            HardwareResolutionDiagnosticCode.ClockFuture,
            HardwareResolutionDiagnosticCode.StorageUnavailable,
        },
        result.Diagnostics.ToArray());
    Assert.IsNull(result.Snapshot);
}
```

Create `HardwareResolutionTestData` with fixed UTC `Now = 2026-08-24T12:00:00Z` and factories using only synthetic values:

```csharp
internal const ulong GiB = 1UL << 30;
internal static WindowsSystemSnapshot WindowsSystem(DateTimeOffset? captured = null) =>
    new(32 * GiB, 31 * GiB, 20 * GiB, captured ?? Now,
        "Windows 11", "10.0.26100", "x64");

internal static LlmFitHardwareEvidence LlmFit(DateTimeOffset? captured = null) =>
    LlmFitHardwareEvidence.Available(
        LlmFitCommandContract.ToolId,
        LlmFitCommandContract.Version,
        captured ?? Now,
        "Intel Core Ultra 7 155H",
        22,
        31,
        20,
        LlmFitGpuDetectionState.Reported,
        [new LlmFitReportedGpu("Intel Arc Graphics", 1)],
        new string('0', 64));
```

The helper also builds Windows processor `16/22`, one hardware DXGI adapter, available storage, `DetectionUnavailable(EnumerationMechanismNotApproved)`, and available pinned CPU llama.cpp evidence with device `(0, "CPU")`.

- [ ] **Step 2: Commit RED and verify the expected compile failure**

```powershell
git add -- tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Resolution
git commit -m "test(hardware-inspection): lock Gate 6 resolution contracts"
```

Move the physical worktree to this commit, run the packaged-test build, and require compilation failure because the resolution types do not exist. A syntax, project-reference, or unrelated error is not an acceptable RED.

- [ ] **Step 3: Implement the minimal contracts**

Define diagnostics exactly once:

```csharp
internal enum HardwareResolutionDiagnosticCode
{
    ClockFuture,
    EvidenceStale,
    CaptureSpanExceeded,
    NormalizationInvalid,
    NormalizationOverflow,
    LlmFitUnavailable,
    WindowsProcessorUnavailable,
    WindowsSystemUnavailable,
    StorageUnavailable,
    DxgiUnavailable,
    GraphicsUnresolved,
    NeuralProcessorUnavailable,
    LlamaCppUnavailable,
    ProcessorNameConflict,
    LogicalProcessorConflict,
    ProcessorTopologyConflict,
    TotalMemoryConflict,
    AvailableMemoryConflict,
    InstructionSetsUnavailable,
}
```

Map each member through a total `switch` to its lowercase dotted token, throwing for undefined enum values. Use private constructors plus static factories for the observation/result state machines. Enumerate inputs into bounded lists before copying; never call unbounded `Count()`, `Distinct()`, or `ToArray()` on caller-controlled enumerables.

- [ ] **Step 4: Verify GREEN and commit**

Run the exact packaged recipe with filter `FullyQualifiedName~HardwareResolutionContractTests`; parse the TRX and require all discovered tests passing. Then:

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Resolution' `
  tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Resolution
git commit -m "feat(hardware-inspection): add Gate 6 resolution contracts"
```

---

### Task 2: Implement deterministic normalization and freshness

**Files:**
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Resolution/HardwareEvidenceNormalizerTests.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Resolution/HardwareEvidenceNormalizer.cs`

**Interfaces:**
- Produces `EvidenceFreshness { Accepted, Stale, Future }`.
- Produces `HardwareEvidenceNormalizer.TryConvertGibToBytes(double, out ulong)`.
- Produces `GetFreshness(DateTimeOffset capturedAtUtc, DateTimeOffset resolvedAtUtc, TimeSpan maximumAge)`.
- Produces `AreWithinTolerance(ulong first, ulong second, ulong tolerance)` and `GetAvailableMemoryTolerance(ulong osUsableBytes)`.

- [ ] **Step 1: Write RED decision-boundary tests**

Use data rows for `0`, half-byte midpoint, exact byte, `16384 GiB`, negative, NaN, infinities, and overflow. Prove midpoint-to-even rather than away-from-zero. Add freshness rows at future `+5s` accepted, `+5s+1 tick` future, dynamic age exactly `30s` accepted, `30s+1 tick` stale, static age exactly `5m` accepted, and `5m+1 tick` stale.

```csharp
[TestMethod]
public void Freshness_RejectsOnlyBeyondTheClosedBoundary()
{
    Assert.AreEqual(EvidenceFreshness.Accepted,
        HardwareEvidenceNormalizer.GetFreshness(
            HardwareResolutionTestData.Now.AddSeconds(-30),
            HardwareResolutionTestData.Now,
            HardwareResolutionPolicy.DynamicMemoryMaximumAge));
    Assert.AreEqual(EvidenceFreshness.Stale,
        HardwareEvidenceNormalizer.GetFreshness(
            HardwareResolutionTestData.Now.AddSeconds(-30).AddTicks(-1),
            HardwareResolutionTestData.Now,
            HardwareResolutionPolicy.DynamicMemoryMaximumAge));
}
```

Prove tolerance without unsigned subtraction overflow, and prove available tolerance returns 2 GiB below 20 GiB OS-usable and exactly 10% above it using integer division.

- [ ] **Step 2: Commit RED and observe the missing normalizer failure**

Commit only the test, build from the physical worktree, and require failure because `HardwareEvidenceNormalizer` is absent.

- [ ] **Step 3: Implement checked pure helpers**

Reject non-finite/negative values before decimal conversion. Multiply checked decimal by `1_073_741_824m`, round with `decimal.Round(value, 0, MidpointRounding.ToEven)`, and checked-cast to `ulong`; return false on `OverflowException`. Validate UTC offsets and nonnegative maximum age. Compare unsigned values as `larger - smaller <= tolerance` after ordering them.

- [ ] **Step 4: Run focused GREEN, contract regression, and commit**

Run the packaged filter `FullyQualifiedName~HardwareEvidenceNormalizerTests|FullyQualifiedName~HardwareResolutionContractTests`, parse the TRX, then commit test and implementation as `feat(hardware-inspection): add Gate 6 normalization policy`.

---

### Task 3: Resolve processor authority and conflicts

**Files:**
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Resolution/ProcessorEvidenceResolverTests.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Resolution/ProcessorEvidenceResolver.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Resolution/ComponentResolution.cs`

**Interfaces:**
- Produces bounded immutable `ComponentResolution<T>` with `T? Value`, `IReadOnlyList<HardwareEvidenceEntry> Entries`, `IReadOnlyList<HardwareResolutionDiagnosticCode> Diagnostics`, and `bool HasCriticalFailure`.
- Produces `ProcessorEvidenceResolver.Resolve(LlmFitHardwareEvidence, WindowsProcessorEvidence, DateTimeOffset resolvedAtUtc)` returning `ComponentResolution<ProcessorFacts>`.

- [ ] **Step 1: Write RED processor decision-table tests**

Cover these independent rows: both sources agree; name differs only by case; name differs materially; logical count differs; LLM Fit unavailable with valid Windows fallback; LLM Fit invalid with valid Windows fallback; Windows unavailable; stale/future LLM Fit; stale/future Windows; Windows physical cores greater than the accepted LLM Fit logical count.

For agreement require LLM Fit spelling/count, Windows physical cores/architecture, empty instruction sets, `ResolvedCorroborated` entries for name/logical, Windows `ResolvedPrimary` for physical/architecture, and `Unavailable` plus `instruction-sets-unavailable` for instruction sets. For fallback require Windows facts and `ResolvedFallback` for name/logical. Every conflict row must have `Value == null`, `HasCriticalFailure == true`, and the exact closed diagnostic.

- [ ] **Step 2: Commit RED and verify the intended missing-resolver failure**

Build the committed RED test in the physical worktree. Require missing `ProcessorEvidenceResolver`/`ComponentResolution<T>` symbols, not an unrelated error.

- [ ] **Step 3: Implement the processor resolver**

Evaluate each source timestamp before reading its facts. Treat `LlmFitEvidenceState.Invalid` and `Unavailable` as unusable primary observations; never recover the partial fields retained by invalid evidence. Use the exact architecture mapping `X86 -> "x86"`, `X64 -> "x64"`, `Arm64 -> "arm64"`. Preserve the selected name spelling without normalization. Emit entries in the exact field-key order from the spec.

- [ ] **Step 4: Run GREEN and commit**

Run packaged filter `FullyQualifiedName~ProcessorEvidenceResolverTests|FullyQualifiedName~HardwareEvidenceNormalizerTests|FullyQualifiedName~HardwareResolutionContractTests`, parse all counters, and commit as `feat(hardware-inspection): resolve processor evidence`.

---

### Task 4: Resolve Windows memory and operating-system evidence

**Files:**
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Resolution/MemorySystemEvidenceResolverTests.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Resolution/MemorySystemEvidenceResolver.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Resolution/MemorySystemResolution.cs`

**Interfaces:**
- Produces `MemorySystemResolution` with optional `MemoryFacts`, optional `OperatingSystemFacts`, fixed entries, diagnostics, and `HasCriticalFailure`.
- Produces `MemorySystemEvidenceResolver.Resolve(LlmFitHardwareEvidence, WindowsSystemEvidenceObservation, DateTimeOffset resolvedAtUtc)`.

- [ ] **Step 1: Write RED memory/OS decision tables**

Cover valid corroboration; LLM Fit unavailable with Windows canonical fallback; total RAM delta exactly 1 GiB and one byte beyond; available delta exactly `max(2 GiB, 10%)` and one byte beyond; GiB midpoint conversion; Windows unavailable; dynamic age/future boundaries; LLM Fit static age/future boundaries; and preservation of installed/OS-usable/available ordering and OS strings.

The valid row must prove:

```csharp
Assert.AreEqual(32 * HardwareResolutionTestData.GiB,
    result.Memory!.PhysicallyInstalledBytes);
Assert.AreEqual(31 * HardwareResolutionTestData.GiB,
    result.Memory.OsUsablePhysicalBytes);
Assert.AreEqual(20 * HardwareResolutionTestData.GiB,
    result.Memory.AvailablePhysicalBytes);
Assert.AreEqual(HardwareResolutionTestData.Now,
    result.Memory.AvailableCapturedAtUtc);
```

Installed bytes are always sourced from Windows. LLM Fit total bytes corroborate OS-usable bytes. Out-of-tolerance total or available memory is critical and returns no memory/OS value.

- [ ] **Step 2: Commit RED and verify it fails for missing production types**

Use the physical build and require missing memory resolver types.

- [ ] **Step 3: Implement memory/OS resolution**

Require the Windows observation to be available and dynamically fresh. If LLM Fit is available and statically fresh, normalize both RAM fields and apply the exact tolerances. If LLM Fit is invalid/unavailable/stale, keep truthful Windows canonical facts with a closed primary-unavailable diagnostic; do not claim corroboration. Emit Windows source entries for installed, OS-usable, available, and all OS fields.

- [ ] **Step 4: Run GREEN and commit**

Run the memory class plus all prior Gate 6 filters, parse the TRX, and commit as `feat(hardware-inspection): resolve memory and OS evidence`.

---

### Task 5: Resolve graphics and neural-processor evidence

**Files:**
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Resolution/GraphicsEvidenceResolverTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Resolution/NeuralProcessorEvidenceResolverTests.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Resolution/GraphicsEvidenceResolver.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Resolution/NeuralProcessorEvidenceResolver.cs`

**Interfaces:**
- Produces `GraphicsEvidenceResolver.Resolve(DxgiGraphicsEvidence, LlmFitHardwareEvidence, DateTimeOffset)` returning `ComponentResolution<IReadOnlyList<GraphicsAdapterFacts>>`.
- Produces `NeuralProcessorEvidenceResolver.Resolve(NeuralProcessorEvidence, DateTimeOffset)` returning `ComponentResolution<NeuralProcessorFacts>`.

- [ ] **Step 1: Write RED graphics tests**

Require DXGI ordinal ordering and exact preservation of dedicated-video, dedicated-system, and shared-system bytes. Cover zero adapters; software/remote adapters; DXGI unavailable with LLM Fit reported-GPU fallback and all memory properties null; both sources unavailable yielding an empty collection plus `Conflict`/`Unavailable` provenance; DXGI zero versus LLM Fit reported; stale/future evidence; and LLM Fit count expansion without invented suffixes or fuzzy name matching.

Assert that no test or production API sums memory categories, inspects vendor IDs to infer Intel, or selects a primary adapter.

- [ ] **Step 2: Write RED NPU tests**

Prove exact one-to-one mapping for `Present`, `NotPresent`, and `DetectionUnavailable`; preserve the name only for Present. A stale/future NPU observation maps to `DetectionUnavailable` with unavailable provenance. No CPU/GPU/runtime input is accepted by this resolver.

- [ ] **Step 3: Commit RED and verify both missing-resolver failures**

Run the physical build and require only the intended missing symbols.

- [ ] **Step 4: Implement the two isolated resolvers**

Iterate bounded existing evidence collections directly. DXGI available is canonical even when empty. Use LLM Fit fallback only when its state is Available and GPU state is Reported. For unavailable graphics return a non-null empty read-only list and noncritical diagnostics. For NPU, never manufacture `NotPresent` from an unavailable/stale observation.

- [ ] **Step 5: Run GREEN and commit**

Run both classes plus all earlier Gate 6 tests, parse the TRX, and commit as `feat(hardware-inspection): resolve graphics and NPU evidence`.

---

### Task 6: Resolve required storage and local-runtime evidence

**Files:**
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Resolution/StorageEvidenceResolverTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Resolution/RuntimeEvidenceResolverTests.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Resolution/StorageEvidenceResolver.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Resolution/RuntimeEvidenceResolver.cs`

**Interfaces:**
- Produces `StorageEvidenceResolver.Resolve(WindowsStorageEvidence, DateTimeOffset)` returning `ComponentResolution<StorageFacts>`.
- Produces `RuntimeEvidenceResolver.Resolve(LlamaCppCapabilityEvidence, DateTimeOffset)` returning `ComponentResolution<LocalRuntimeCapabilities>`.

- [ ] **Step 1: Write RED storage tests**

Prove exact capacity/caller-available mapping, fresh boundary acceptance, stale/future rejection, and unavailable-state critical failure. Assert the resolved object and diagnostics contain no path or volume identity.

- [ ] **Step 2: Write RED runtime tests**

Require the exact composite build identity:

```text
LLamaSharp/0.27.0;LLamaSharp.Backend.Cpu/0.27.0;llama.cpp/3f7c29d318e317b63f54c558bc69803963d7d88c;win-x64
```

Require exactly `LocalRuntimeBackend.Cpu`, preserve visible-device buffer labels in ordinal order, and reject stale/future/unavailable evidence with no runtime value. Prove that no model-execution field or claim exists.

- [ ] **Step 3: Commit RED and verify the missing production types**

Use the physical build and accept only the intended missing resolver errors.

- [ ] **Step 4: Implement the required resolvers**

Map only available, fresh, already invariant-checked provider evidence. Storage and runtime failures set `HasCriticalFailure`; do not invent zero capacity, an unknown runtime, or an empty backend.

- [ ] **Step 5: Run GREEN and commit**

Run both new classes and all prior Gate 6 tests, parse the TRX, and commit as `feat(hardware-inspection): resolve storage and runtime evidence`.

---

### Task 7: Aggregate the fixed manifest and construct snapshots fail-closed

**Files:**
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Resolution/HardwareEvidenceResolverTests.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Resolution/HardwareEvidenceResolver.cs`

**Interfaces:**
- Produces `HardwareEvidenceResolver(TimeProvider)` and `HardwareEvidenceResolutionResult Resolve(Guid snapshotId, CollectedHardwareEvidence evidence)`.
- Preserves the existing public `HardwareSnapshot` constructor, properties, and four v1 minimum evidence keys.
- Requires the resolver's manifest to contain the exact 19-field Gate 6 inventory and requires all 15 construction-critical fields to resolve before it constructs a snapshot; graphics, NPU, and instruction-set provenance may remain explicitly unresolved.

- [ ] **Step 1: Write RED aggregate success and provenance tests**

With the default synthetic evidence, require a usable schema-v1 snapshot, the caller UUID, resolver UTC time, policy ID, exact canonical facts, and exactly 19 manifest entries in the spec's ordinal order. Verify source, state, confidence, capture time, and diagnostic for every entry. Require these 15 fields to resolve: four processor fields except instruction sets, all three memory fields, both storage fields, all three OS fields, and all three runtime fields. Mutate every source collection after input creation and prove the result cannot change.

- [ ] **Step 2: Write RED aggregate failure decision tables**

Independently replace each critical source with conflict, stale, future, or unavailable evidence and require `Snapshot == null`. Cover capture span exactly 60 seconds and one tick beyond; `Guid.Empty`; duplicate, missing, or out-of-order aggregate manifest attempts; optional graphics/NPU/instruction-set unavailability remaining resolvable with explicit unresolved entries; and deterministic diagnostics under permuted input diagnostics.

- [ ] **Step 3: Commit RED and verify the aggregate tests fail for missing behavior**

The aggregate resolver is absent, so the new aggregate tests must fail to compile for that symbol alone. Reject unrelated failures.

- [ ] **Step 4: Implement the aggregate resolver**

Read `TimeProvider.GetUtcNow()` once. Validate UTC, snapshot UUID, and global capture span using only accepted available observations. Invoke every component resolver once, concatenate entries in fixed policy order, require the exact 19-key inventory, and build `HardwareEvidenceManifest` before deciding success. If any component is critical or any of the 15 construction-critical entries is unresolved, return `Failure` with no snapshot. Otherwise build `HardwareSnapshot` with schema/version constants and `HardwareSnapshotUsability.Usable`. Do not change public Domain members, constructor signatures, enum values, handoff semantics, or existing privacy guards.

- [ ] **Step 5: Run full Gate 6 GREEN and relevant packaged regression**

Run filter:

```text
FullyQualifiedName~Features.HardwareInspection.Resolution|FullyQualifiedName~HardwareInspectionContractTests|FullyQualifiedName~HardwareInspectionRunContractTests|FullyQualifiedName~HardwareInspectionPresentationContractTests
```

Parse the TRX and require total=executed=passed, zero non-passing. Then run the authoritative packaged filter:

```text
FullyQualifiedName~Features.HardwareInspection|FullyQualifiedName~ModelInspectionHandoff|FullyQualifiedName~OnboardingHardwareInspectionNavigationTests
```

Require at least the Gate 5 discovered 118 and no non-passing result.

- [ ] **Step 6: Commit the aggregate slice**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Resolution' `
  tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection
git commit -m "feat(hardware-inspection): resolve canonical hardware evidence"
```

---

### Task 8: Review, regress, audit, and record Gate 6 evidence

**Files:**
- Modify: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/README.md`
- Modify: `docs/reviews/2026-08-22-hardware-inspection-integration-preservation-matrix.md`
- Modify: `docs/testing/evidence/README.md`
- Create: `docs/testing/evidence/2026-08-24-hardware-inspection-gate-6-resolution.md`

**Interfaces:**
- Documents the exact evaluated source head, policy, commands, discovered counts, review disposition, privacy/security audits, and nonclaims.
- Changes no production composition.

- [ ] **Step 1: Run fresh exact-head ordinary suites**

From the committed physical worktree, run:

```powershell
dotnet test tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/GraniteEdgeAI.HardwareInspection.Foundation.Tests.csproj -p:Platform=x64 --no-ansi
dotnet test tests/UnitTests/GraniteEdgeAI.HardwareInspection.LlamaCppProbe.Tests/GraniteEdgeAI.HardwareInspection.LlamaCppProbe.Tests.csproj -p:Platform=x64 --no-ansi
python -B -m unittest discover -s tests/testing/hardware_inspection -v
```

Require total=passed with zero failed/skipped. Record actual counts.

- [ ] **Step 2: Run packaged and build regressions**

Run the authoritative packaged 118-floor filter from Task 7 with a fresh TRX. Build the packaged Debug/x64 test project and application Release/x64 MSIX with zero errors. Record known warning families separately; do not call pre-existing warnings new Gate 6 defects.

- [ ] **Step 3: Audit architecture, privacy, and production inactivity**

Inspect every hit:

```powershell
rg -n 'Process\.Start|UseShellExecute|cmd\.exe|powershell|TcpListener|HttpListener|HttpClient|File\.|Directory\.|Registry|Environment\.' `
  'IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Resolution'
rg -n 'ModelInspection|GGUF|OpenVINO|Compatibility|HardwareInspectionHandoff|HardwareInspectionOutcome' `
  'IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Resolution'
rg -n 'UnavailableHardwareInspectionService' `
  'IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs'
rg -n 'Path|FileName|HostName|UserName|Stdout|Stderr|RawOutput|Exception\.Message' `
  'IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Resolution'
git diff --check
rg -n '^(<<<<<<< |=======$|>>>>>>> )' --glob '!docs/superpowers/plans/*'
```

Audit the exact Gate 6 range for added packages, certificates, keys, binaries, TRX, raw evidence, URLs, private paths, host facts, or project references. Require none. Build a non-x64 project graph or inspect evaluated Compile items to prove `Infrastructure/Resolution` remains excluded whenever the Foundation project reference is absent.

- [ ] **Step 4: Request independent code review**

Review the exact Gate 6 implementation range for authority drift, semantic relabelling, timestamp boundary errors, numeric overflow, unbounded enumeration, manifest completeness, fallback safety, false absence, mutable aliases, arbitrary diagnostics, provider/model leakage, and production activation. Resolve every Critical/Important/Minor finding test-first and repeat affected evidence.

- [ ] **Step 5: Write evidence and boundary docs**

Record exact commits, actual test counts, build warnings/errors, policy values, decision-table coverage, audit results, review result, and these exact nonclaims:

- Gate 6 implements deterministic evidence resolution only.
- Production still uses `UnavailableHardwareInspectionService`.
- Gate 7 orchestration, outcomes, cancellation, progress, and activation remain incomplete.
- Gates 8–9 UI integration and supported-machine end-to-end evidence remain incomplete.
- No model compatibility or fit conclusion is produced.
- Gate 5 development acceptance does not establish public-trust signing or Smart App Control acceptance.

Do not commit TRX, packages, certificates, raw evidence, host labels, or machine paths.

- [ ] **Step 6: Verify documentation and commit Gate 6 evidence**

Run `git diff --check`, link-check the new evidence index entry, scan the Markdown for private paths/host labels, and commit:

```powershell
git add -- infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/README.md `
  docs/reviews/2026-08-22-hardware-inspection-integration-preservation-matrix.md `
  docs/testing/evidence/README.md `
  docs/testing/evidence/2026-08-24-hardware-inspection-gate-6-resolution.md
git commit -m "docs(hardware-inspection): record Gate 6 verification"
```

Gate 6 is complete only after this evidence commit. The full Hardware Inspection feature remains incomplete; Gate 7 is next.
