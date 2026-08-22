# Hardware Inspection Gate 3 LLM Fit Provider Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a production-shaped, infrastructure-only LLM Fit v1.1.9 command, parser, validation, and evidence provider on the verified Gate 2 process boundary without bundling the candidate or activating product collection.

**Architecture:** Add immutable provider-specific contracts and a bounded tolerant JSON parser to `GraniteEdgeAI.HardwareInspection.Foundation`. `LlmFitHardwareEvidenceProvider` validates exact verified identity/commands, executes version and system commands through `IExternalProcessRunner`, and maps only closed results; raw output and app-domain policy never cross the boundary.

**Tech Stack:** C# 12, .NET 8 Windows x64, `System.Text.Json`, SHA-256, Microsoft Testing Platform, MSTest 4.3.2, existing verified harmless process fixture.

**Spec:** `docs/superpowers/specs/2026-08-22-hardware-inspection-gate-3-llmfit-provider-design.md`

## Global Constraints

- LLM Fit is pinned to tool ID `llmfit`, version `1.1.9`, version output `llmfit 1.1.9`, version arguments `--version`, and system arguments `--no-dashboard --json system`.
- LLM Fit v1.1.9 remains `FunctionalPassWithPackagingConcern`; do not track, bundle, acquire, download, sign, redistribute, or production-register the candidate.
- `UnavailableHardwareInspectionService` remains the sole product composition.
- Gate 3 contains no model data, GGUF/OpenVINO type, compatibility calculation, canonical resolver, Windows/DXGI enrichment, orchestrator, UI, network, dashboard, listener, shell, retry, or raw-output persistence.
- Every process execution consumes `VerifiedTrustedTool` through `IExternalProcessRunner`; no second launcher or command-string API is permitted.
- Parser depth is 16. System stdout/stderr limits are independently 256 KiB; version stdout/stderr limits are independently 4 KiB. Version timeout is 5 seconds; system timeout is 15 seconds.
- Unknown JSON properties are tolerated. Root, `system`, and each parsed GPU object reject ordinal duplicate property names. Required names are case-sensitive.
- All new behavior follows RED, GREEN, REFACTOR and ends in an independently reviewable commit.
- Do not modify Model Inspection, onboarding, app composition, or Hardware UI code during Gate 3.

---

### Task 1: Add exact command and immutable evidence contracts

**Files:**
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/LlmFit/LlmFitCommandContract.cs`
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/LlmFit/LlmFitHardwareEvidence.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/LlmFit/LlmFitContractTests.cs`

**Interfaces:**
- Produces: `LlmFitCommandContract.VersionCommand`, `LlmFitCommandContract.SystemCommand`, `LlmFitEvidenceState`, `LlmFitGpuDetectionState`, `LlmFitDiagnosticCode`, `LlmFitReportedGpu`, and `LlmFitHardwareEvidence`.
- Consumes: Gate 2 `TrustedToolCommand`.

- [ ] **Step 1: Write failing command-contract tests**

Add tests requiring exact ordinal values and fresh immutable argument collections:

```csharp
[TestMethod]
public void CommandContractUsesOnlyPinnedReadOnlyInvocations()
{
    TrustedToolCommand version = LlmFitCommandContract.CreateVersionCommand();
    TrustedToolCommand system = LlmFitCommandContract.CreateSystemCommand();

    Assert.AreEqual("llmfit", LlmFitCommandContract.ToolId);
    Assert.AreEqual("1.1.9", LlmFitCommandContract.Version);
    Assert.AreEqual("llmfit 1.1.9", LlmFitCommandContract.ExpectedVersionOutput);
    CollectionAssert.AreEqual(new[] { "--version" }, version.Arguments.ToArray());
    CollectionAssert.AreEqual(
        new[] { "--no-dashboard", "--json", "system" },
        system.Arguments.ToArray());
}
```

- [ ] **Step 2: Write failing evidence-invariant tests**

Cover:

- enum values are exactly those in the spec;
- available evidence requires all CPU/RAM facts, a non-invalid GPU state, an output hash, and no diagnostics;
- invalid evidence requires at least one parser diagnostic and may retain only valid nullable facts;
- unavailable evidence has no hardware facts/hash and exactly one provider/process diagnostic;
- UTC is enforced;
- RAM is finite and within `0 <= available <= total <= 16384`;
- processor count is `1..4096`;
- GPU count is `0..64`, list counts sum exactly, and names are safe/unique;
- SHA-256 is lowercase canonical;
- every input list is copied.

Use this successful construction as the positive contract:

```csharp
LlmFitHardwareEvidence evidence = LlmFitHardwareEvidence.Available(
    "llmfit",
    "1.1.9",
    new DateTimeOffset(2026, 8, 22, 21, 0, 0, TimeSpan.Zero),
    "Fixture Intel CPU",
    16,
    31.72,
    18.40,
    LlmFitGpuDetectionState.Reported,
    [new LlmFitReportedGpu("Fixture Intel Arc Graphics", 1)],
    new string('a', 64));
```

- [ ] **Step 3: Run RED**

Run:

```powershell
dotnet test tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/GraniteEdgeAI.HardwareInspection.Foundation.Tests.csproj `
  -p:Platform=x64 --filter 'FullyQualifiedName~LlmFitContractTests' --no-ansi
```

Expected: compile failure because the Gate 3 contracts do not exist.

- [ ] **Step 4: Implement exact command factories**

Implement a static contract with no mutable public array:

```csharp
public static class LlmFitCommandContract
{
    public const string ToolId = "llmfit";
    public const string Version = "1.1.9";
    public const string ExpectedVersionOutput = "llmfit 1.1.9";
    public const string VersionCommandIdentity = "version";
    public const string SystemCommandIdentity = "system";

    public static TrustedToolCommand CreateVersionCommand() =>
        new(VersionCommandIdentity, ["--version"]);

    public static TrustedToolCommand CreateSystemCommand() =>
        new(SystemCommandIdentity, ["--no-dashboard", "--json", "system"]);
}
```

- [ ] **Step 5: Implement evidence contracts and closed factories**

Use private construction plus three public static factories:

```csharp
public sealed class LlmFitHardwareEvidence
{
    public static LlmFitHardwareEvidence Available(
        string toolId,
        string version,
        DateTimeOffset capturedAtUtc,
        string cpuName,
        int cpuLogicalProcessorCount,
        double totalRamGiB,
        double availableRamGiB,
        LlmFitGpuDetectionState gpuState,
        IEnumerable<LlmFitReportedGpu> gpus,
        string rawOutputSha256);

    public static LlmFitHardwareEvidence Invalid(
        string toolId,
        string version,
        DateTimeOffset capturedAtUtc,
        string? cpuName,
        int? cpuLogicalProcessorCount,
        double? totalRamGiB,
        double? availableRamGiB,
        LlmFitGpuDetectionState gpuState,
        IEnumerable<LlmFitReportedGpu> gpus,
        string rawOutputSha256,
        IEnumerable<LlmFitDiagnosticCode> diagnostics);

    public static LlmFitHardwareEvidence Unavailable(
        string toolId,
        string version,
        DateTimeOffset capturedAtUtc,
        LlmFitDiagnosticCode diagnostic);
}
```

Use `Array.AsReadOnly(copy)` for GPUs/diagnostics. Centralize validation so none of the three factories can construct a contradictory state. Safe names allow printable Unicode, reject NUL/control/unpaired-surrogate input, reject leading/trailing whitespace, and cap at 256 Unicode scalar values.

- [ ] **Step 6: Run GREEN and full foundation regression**

Run the focused filter, then the full project with a minimum of 50 existing tests plus the new discovered count. Require zero failed/skipped.

- [ ] **Step 7: Commit**

```powershell
git add infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/LlmFit `
  tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/LlmFit/LlmFitContractTests.cs
git commit -m "feat(hardware-inspection): add LLM Fit evidence contracts"
```

---

### Task 2: Parse valid and additive LLM Fit system JSON

**Files:**
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/LlmFit/LlmFitSystemDto.cs`
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/LlmFit/LlmFitSystemJsonParser.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/LlmFit/LlmFitSystemJsonParserTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Fixtures/LlmFit/valid-windows-intel.json`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Fixtures/LlmFit/valid-cpu-only.json`
- Modify: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/GraniteEdgeAI.HardwareInspection.Foundation.Tests.csproj`

**Interfaces:**
- Produces internal `LlmFitSystemParseResult LlmFitSystemJsonParser.Parse(string json)`.
- `LlmFitSystemParseResult` carries state, nullable validated facts, immutable GPU entries, SHA-256, and ordered parser diagnostics; it never carries raw JSON.

- [ ] **Step 1: Add accepted synthetic fixtures**

Copy the exact existing synthetic Gate 1 fixture content from:

- `tools/HardwareInspection.LlmFitSpike.Tests/Fixtures/valid-windows-intel.json`
- `tools/HardwareInspection.LlmFitSpike.Tests/Fixtures/valid-cpu-only.json`

Do not copy trusted raw capture evidence. Add test-project content entries with `CopyToOutputDirectory="PreserveNewest"`; app/test packaging references must remain absent.

- [ ] **Step 2: Write RED success and additive-schema tests**

Require exact mapping for `31.72`, `18.40`, 16 logical processors, Intel fixture names, one reported GPU, and lowercase SHA-256. Require CPU-only mapping to `NotReported`, count zero, and an empty list. Add unknown root/system/GPU properties and require identical facts/hash except for the intentionally different raw hash.

```csharp
LlmFitSystemParseResult result = LlmFitSystemJsonParser.Parse(json);
Assert.AreEqual(LlmFitEvidenceState.Available, result.State);
Assert.AreEqual(31.72, result.TotalRamGiB);
Assert.AreEqual(18.40, result.AvailableRamGiB);
Assert.AreEqual(16, result.CpuLogicalProcessorCount);
Assert.AreEqual(LlmFitGpuDetectionState.Reported, result.GpuState);
Assert.AreEqual(1, result.Gpus.Single().Count);
Assert.AreEqual(64, result.RawOutputSha256.Length);
Assert.AreEqual(0, result.Diagnostics.Count);
```

- [ ] **Step 3: Run RED**

Expected: parser/DTO types are missing.

- [ ] **Step 4: Implement bounded parser helpers**

Use:

```csharp
using JsonDocument document = JsonDocument.Parse(
    json,
    new JsonDocumentOptions
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 16,
    });
```

Implement focused helpers:

- `HasUniquePropertyNames(JsonElement)`;
- `ReadFiniteDouble(JsonElement, string, out double)`;
- `ReadBoundedInteger(JsonElement, string, int min, int max, out int)`;
- `ReadSafeName(JsonElement, string, bool allowNull, out string?)`;
- `ParseGpuEntries(JsonElement, int expectedCount, out IReadOnlyList<LlmFitReportedGpu>)`;
- `ComputeSha256(string)` using `SHA256.HashData(Encoding.UTF8.GetBytes(json))`.

Catch only `JsonException` and `InvalidOperationException` caused by content. Null input remains `ArgumentNullException`.

- [ ] **Step 5: Map success to an internal immutable result**

Do not deserialize into a public serializer model. Read only approved fields from `JsonElement`; ignore additive properties; copy GPU lists. Preserve GiB doubles without converting to bytes.

- [ ] **Step 6: Run GREEN and commit**

```powershell
git add infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/LlmFit `
  tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/LlmFit `
  tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Fixtures/LlmFit `
  tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/GraniteEdgeAI.HardwareInspection.Foundation.Tests.csproj
git commit -m "feat(hardware-inspection): parse LLM Fit system evidence"
```

---

### Task 3: Fail closed for malformed, missing, and contradictory JSON

**Files:**
- Modify: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/LlmFit/LlmFitSystemJsonParser.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/LlmFit/LlmFitSystemJsonParserTests.cs`

**Interfaces:**
- Preserves `Parse(string)` from Task 2.
- Produces deterministic `Invalid` results using only `JsonInvalid`, `RequiredCpuRamMissing`, `RequiredCpuRamInvalid`, `GpuShapeMissing`, and `GpuInconsistent`.

- [ ] **Step 1: Add RED malformed-structure cases**

Use data rows/dynamic data for:

- empty input, truncated JSON, comments, trailing comma, root array, missing/non-object `system`;
- duplicate `system`, duplicate required system property, duplicate GPU-entry property;
- nesting deeper than 16;
- case-changed required property names.

Require `Invalid`, no exception, a canonical SHA-256, no contradictory fact, and ordered unique diagnostics.

- [ ] **Step 2: Add RED CPU/RAM boundary cases**

Cover missing each required field, strings/bools instead of numbers, non-integer cores, cores 0/4097, total 0/16385, negative available, available greater than total, exponent overflow, and unsafe CPU names. Assert missing vs invalid diagnostics are distinct.

- [ ] **Step 3: Add RED GPU consistency cases**

Cover missing shape, wrong types, negative/65 count, flag/count disagreement, empty list when true, nonempty list when false, invalid entry, sum mismatch/overflow, duplicate case-insensitive names, unsafe names, and contradictory top-level name representation.

- [ ] **Step 4: Run RED**

Expected: one or more adverse cases are incorrectly accepted or mapped.

- [ ] **Step 5: Implement deterministic diagnostic precedence**

Use this order:

1. `JsonInvalid` for unparseable/root/duplicate structural failure;
2. `RequiredCpuRamMissing` before `RequiredCpuRamInvalid` when both occur;
3. `GpuShapeMissing` before `GpuInconsistent` when both occur.

Invalid results retain a field only when its own type/range/name validation passed. Never retain GPU entries unless the entire GPU shape is consistent.

- [ ] **Step 6: Run GREEN, fuzz bounded primitives, and commit**

Add a deterministic loop over 256 short malformed strings built from a fixed seed/alphabet and require no exception or oversized diagnostic/fact collection. Then run the full parser class.

```powershell
git add infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/LlmFit/LlmFitSystemJsonParser.cs `
  tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/LlmFit/LlmFitSystemJsonParserTests.cs
git commit -m "test(hardware-inspection): close LLM Fit parser failures"
```

---

### Task 4: Map verified execution into provider evidence

**Files:**
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/LlmFit/ILlmFitHardwareEvidenceProvider.cs`
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/LlmFit/LlmFitHardwareEvidenceProvider.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/LlmFit/LlmFitHardwareEvidenceProviderTests.cs`

**Interfaces:**
- Produces `Task<LlmFitHardwareEvidence> CaptureAsync(VerifiedTrustedTool tool, CancellationToken cancellationToken)`.
- Consumes `IExternalProcessRunner`, `TimeProvider`, `LlmFitSystemJsonParser`, and Gate 2 verified command custody.

- [ ] **Step 1: Create a scripted runner test double**

Implement in the test file:

```csharp
private sealed class ScriptedRunner(params ExternalProcessResult[] results)
    : IExternalProcessRunner
{
    private readonly Queue<ExternalProcessResult> _results = new(results);
    internal List<ExternalProcessRequest> Requests { get; } = [];

    public Task<ExternalProcessResult> RunAsync(
        VerifiedTrustedTool tool,
        ExternalProcessRequest request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(_results.Dequeue());
    }
}
```

Create verified test tools only through the real `TrustedToolPackageVerifier`; do not construct `VerifiedTrustedTool` through reflection.

- [ ] **Step 2: Write RED identity/command/version tests**

Require:

- wrong tool ID/version returns `Unavailable(ToolIdentityMismatch)` without invoking the runner;
- missing/drifted version or system arguments returns `CommandContractMismatch` without invoking;
- version request uses 5 seconds and 4096/4096 limits;
- system request uses 15 seconds and 262144/262144 limits;
- version output accepts only the exact one-line identity plus a final CRLF/LF;
- system is never invoked after version failure/mismatch.

- [ ] **Step 3: Write RED closed process mapping tests**

For each command, map `StartFailed`, `TimedOut`, `OutputLimitExceeded`, `CleanupFailed`, unexpected `Cancelled`, nonzero `Exited`, and successful zero exit. Assert stderr and exit code never appear in evidence. Assert cancellation with a cancelled caller token throws `OperationCanceledException` carrying that token.

- [ ] **Step 4: Write RED parser/evidence mapping tests**

Successful system output becomes `Available`; malformed/missing/contradictory output becomes `Invalid` with copied parser diagnostics and only valid partial fields. Capture time is the injected time converted to UTC. The provider never disposes the caller-owned verified tool.

- [ ] **Step 5: Run RED**

Expected: provider interfaces/types are missing.

- [ ] **Step 6: Implement provider with one linear attempt**

Constructor:

```csharp
public LlmFitHardwareEvidenceProvider(
    IExternalProcessRunner processRunner,
    TimeProvider? timeProvider = null)
```

Capture flow:

1. throw for null tool and pre-cancelled caller;
2. validate exact tool/version and both internal verified commands;
3. execute version once and map result;
4. validate exact version stdout without broad `Trim()`;
5. execute system once and map result;
6. parse successful stdout;
7. use `_timeProvider.GetUtcNow().ToUniversalTime()`;
8. construct available/invalid/unavailable evidence through the Task 1 factories.

No catch-all exception handling, logging, retry, or fallback is allowed.

- [ ] **Step 7: Run GREEN and commit**

```powershell
git add infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/LlmFit `
  tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/LlmFit/LlmFitHardwareEvidenceProviderTests.cs
git commit -m "feat(hardware-inspection): add verified LLM Fit provider"
```

---

### Task 5: Prove the provider through the real protected process boundary

**Files:**
- Modify: `tests/ProcessFixtures/GraniteEdgeAI.HardwareInspection.LlmFitFakeTool/Program.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/LlmFit/LlmFitHardwareEvidenceProviderProcessTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Support/VerifiedLlmFitFixture.cs`

**Interfaces:**
- Consumes the real `TrustedToolPackageVerifier`, `ExternalProcessRunner`, fake PE/apphost package, and Gate 3 provider.
- Produces no production interface.

- [ ] **Step 1: Write RED real-process success test**

Build/copy the harmless fake package to a fresh short approved root with tool ID `llmfit`, version `1.1.9`, and commands created by `LlmFitCommandContract`. Verify it with the real verifier, capture with the real runner/provider, and require available CPU-only evidence.

- [ ] **Step 2: Write RED real-process failure tests**

Add only proved missing fake modes:

- `version-mismatch` returns `llmfit 9.9.9` for `--version`;
- `invalid-json` emits one bounded malformed system line;
- reuse `nonzero`, `large-output`, and `sleep` for system failure, overflow, timeout, and cancellation.

Require exact closed diagnostics, caller cancellation propagation, no residual root/child process, and fixture cleanup retry for application-control scan latency.

- [ ] **Step 3: Run RED**

Expected: the new fake modes/provider process tests fail before fixture changes or expose a real-boundary mapping gap.

- [ ] **Step 4: Implement minimal fake modes and shared fixture**

Keep the fixture harmless, offline, no-dashboard, and bounded. Do not add arbitrary argument handling. `VerifiedLlmFitFixture` owns/disposes `VerifiedTrustedTool`, copies only the flat published fixture members, writes `fake-mode.txt`, computes the actual executable SHA-256, and retries only temporary-directory deletion.

- [ ] **Step 5: Run real-process class three times**

Use a physical short worktree and no controlled old fixture root so the newly built fake modes execute:

```powershell
1..3 | ForEach-Object {
  dotnet test tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/GraniteEdgeAI.HardwareInspection.Foundation.Tests.csproj `
    -p:Platform=x64 --no-restore `
    --filter 'FullyQualifiedName~LlmFitHardwareEvidenceProviderProcessTests' --no-ansi
  if ($LASTEXITCODE -ne 0) { throw "Gate 3 process repeat $_ failed." }
}
```

- [ ] **Step 6: Run the accepted Gate 1 fixture mapping test**

The synthetic accepted fixture must map to available evidence with 31.72/18.40 GiB, 16 logical processors, one Intel fixture GPU, no diagnostics, and the recomputed hash. Assert no real host data or Gate raw capture path is referenced.

- [ ] **Step 7: Commit**

```powershell
git add tests/ProcessFixtures/GraniteEdgeAI.HardwareInspection.LlmFitFakeTool/Program.cs `
  tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/LlmFit `
  tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Support/VerifiedLlmFitFixture.cs
git commit -m "test(hardware-inspection): prove LLM Fit provider boundary"
```

---

### Task 6: Gate 3 verification, review, and evidence

**Files:**
- Modify: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/README.md`
- Modify: `docs/reviews/2026-08-22-hardware-inspection-integration-preservation-matrix.md`

**Interfaces:**
- Documents the completed Gate 3 boundary and Gate 4 entry; changes no runtime contract.

- [ ] **Step 1: Run fresh foundation and Gate regression tests**

From a physical short worktree at the exact candidate head:

1. restore/build the fake fixture and foundation tests;
2. run the complete foundation project with the exact freshly discovered minimum and TRX;
3. run Gate deterministic `TestCategory=Deterministic` with minimum 174 and TRX, using the approved harmless controlled fixture only if application control blocks test publishing;
4. parse both TRX files and require zero failed/skipped.

- [ ] **Step 2: Run packaged and repository regressions**

Run:

- packaged Debug/x64 VSTest filter `FullyQualifiedName~Features.HardwareInspection|FullyQualifiedName~ModelInspectionHandoffTests|FullyQualifiedName~ModelInspectionHandoffRegistryTests|FullyQualifiedName~Onboarding`, requiring at least the Gate 2 authoritative 114 tests and zero failed/skipped;
- Stage A 12/12 with process-scoped `PSExecutionPolicyPreference=Bypass`;
- Stage 0/acquisition/public-contract/theme 18/18;
- Debug/x64 and Release/x64 app builds, zero errors.

- [ ] **Step 3: Audit boundaries and packages**

Require:

```powershell
rg -n 'Process\.Start|UseShellExecute|cmd\.exe|powershell|GGUF|OpenVINO|ModelInspection' `
  infrastructure/GraniteEdgeAI.HardwareInspection.Foundation
rg -n 'UnavailableHardwareInspectionService' `
  'IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs'
git diff --check
rg -n '^(<<<<<<< |=======$|>>>>>>> )' --glob '!docs/superpowers/plans/*'
```

Inspect the Debug AppX recursively and the Gate 3 commit range. Fail for any newly tracked/packaged `llmfit.exe`, archive, TRX, raw capture, trusted/offline report, candidate URL, username, or absolute machine path.

- [ ] **Step 4: Request independent code review**

Use `superpowers:requesting-code-review`. Review command/evidence invariants, parser allocation/bounds/duplicate handling, process-result precedence, cancellation, raw-output privacy, candidate non-packaging, and production inactivity. Resolve every Critical/Important finding and rerun affected verification.

- [ ] **Step 5: Update documentation**

README must state exact command/time/output limits, parsing tolerance, diagnostics, custody ownership, provider-specific/noncanonical semantics, and production inactivity. Preservation matrix must record exact head, commands, counts, warnings, review result, non-claims, and Gate 4 as next.

- [ ] **Step 6: Final verification and commit**

Use `superpowers:verification-before-completion`, confirm a clean tracked worktree, then commit:

```powershell
git add infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/README.md `
  docs/reviews/2026-08-22-hardware-inspection-integration-preservation-matrix.md
git commit -m "docs(hardware-inspection): record Gate 3 verification"
```

Gate 3 is complete only after this evidence commit. The full Hardware Inspection feature remains incomplete; Gate 4 Windows/DXGI enrichment is next.
