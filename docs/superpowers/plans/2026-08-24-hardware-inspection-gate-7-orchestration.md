# Hardware Inspection Gate 7 Orchestration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Activate one production Hardware Inspection service that verifies fixed tool packages, collects the seven provider inputs with bounded overlap, resolves evidence once, emits exact progress, and returns truthful completed, warning, failed, or cancelled results.

**Architecture:** Add a closed trusted-tool acquisition boundary, a provider adapter plus bounded collection coordinator, a pure terminal policy, and a thin `HardwareInspectionService`. Gate 6 remains the only resolver, and production composition changes only after the complete Gate 7 regression is green.

**Tech Stack:** C# 13, .NET 8 Windows x64, WinUI 3 packaged MSTest, Hardware Inspection Foundation providers, `TimeProvider`, `SemaphoreSlim`, strict `System.Text.Json`, Visual Studio MSBuild/VSTest, Python contracts.

**Spec:** `docs/superpowers/specs/2026-08-24-hardware-inspection-gate-7-orchestration-design.md`

## Global Constraints

- Production supports only x64; non-x64 graphs contain no Hardware Inspection infrastructure or Foundation reference.
- LLM Fit is never bundled, downloaded, PATH-searched, registry-discovered, or accepted from a sibling manifest.
- Its production root is CommonApplicationData plus `GraniteEdgeAI/HardwareInspection/llmfit/1.1.9/win-x64`.
- Its executable SHA-256 is `db82bcb17f065b7ff7528ffe9904b2b0e1cce0c843fcd659b4ee1432a2e72e19`; inventory is exactly `llmfit.exe`, `LICENSE`, `README.md`.
- Packaged llama.cpp identity remains `granite-edge-hardware-llamacpp-probe` / `0.27.0-cpu-win-x64`.
- Native concurrency is at most four; external-process concurrency is one. Waits are cancellable and permits/custody are always released.
- One run emits the seven existing stages once, in enum order, with one non-empty ID and sequence 1..7.
- Gate 6 resolution runs at most once. No retry, partial snapshot, compatibility, fit, GPU selection, Intel inference, or NPU inference is added.
- Safe diagnostics are fixed uppercase tokens; results contain no path, hash, command, raw output, exception text, user, or host fact.
- Production composition remains unchanged until Task 7 focused tests are green.

## Packaged-test recipe

Run packaged tests from the committed physical short worktree, never a junction or substituted drive. For each step set a unique result directory and filter:

```powershell
$testProject = 'tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj'
$recipe = 'tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe'
$resultDirectory = 'TestResults\HardwareInspection\Gate7Focused'
$filter = 'FullyQualifiedName~Features.HardwareInspection.Orchestration'

dotnet restore $testProject --runtime win-x64 -p:Platform=x64
dotnet build $testProject --configuration Debug --no-restore --runtime win-x64 -p:Platform=x64
New-Item -ItemType Directory -Force -Path $resultDirectory | Out-Null
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vstest = & $vswhere -latest -products * -find '**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' | Select-Object -First 1
if (-not $vstest) { throw 'Visual Studio app-container test runner was not found.' }
& $vstest (Resolve-Path $recipe).Path '/Platform:x64' "/TestCaseFilter:$filter" '/Logger:trx;LogFileName=gate7-focused.trx' "/ResultsDirectory:$((Resolve-Path $resultDirectory).Path)"
if ($LASTEXITCODE -ne 0) { throw "Packaged VSTest failed: $LASTEXITCODE" }
```

If Smart App Control blocks the generated host, sign only solution-owned `GraniteEdgeAI.*.dll/.exe` files in the generated AppX with the already trusted development certificate; do not disable or weaken Smart App Control. Parse each TRX and require `total == executed == passed` and all non-passing counters equal zero.

---

### Task 1: Add closed orchestration contracts and terminal policy

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Orchestration/HardwareToolAcquisitionContracts.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Orchestration/HardwareEvidenceCollectionResult.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Orchestration/HardwareInspectionOutcomePolicy.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Orchestration/HardwareInspectionOutcomePolicyTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Orchestration/HardwareOrchestrationContractTests.cs`

**Interfaces:**

```csharp
internal enum HardwareToolAcquisitionDiagnosticCode
{
    ToolNotAvailable,
    ToolIntegrityFailure,
    PackagedProbeUnavailable,
}

internal enum HardwareEvidenceCollectionFailureCode
{
    ProviderUnavailable,
    OrchestrationFailure,
    ProgressCallbackFailure,
}

internal interface IHardwareToolAcquisition
{
    HardwareToolAcquisitionResult Acquire();
}
```

`HardwareToolLease : IDisposable` owns non-null LLM Fit and llama.cpp `VerifiedTrustedTool` objects and disposes both once. Acquisition and collection results enforce exclusive success/failure state. Policy methods are `FromResolution`, `FromAcquisitionFailure`, and `FromCollectionFailure`.

- [ ] **Step 1: Write failing contract/policy tests**

Assert exact enum names, null/undefined/contradictory-state rejection, immutable results, idempotent lease disposal, and this resolution mapping:

```csharp
Assert.AreEqual(HardwareInspectionOutcome.Completed,
    HardwareInspectionOutcomePolicy.FromResolution(id, CleanSuccess()).Outcome);
Assert.AreEqual(HardwareInspectionOutcome.CompletedWithWarnings,
    HardwareInspectionOutcomePolicy.FromResolution(id, WarningSuccess()).Outcome);
HardwareInspectionRunResult failed =
    HardwareInspectionOutcomePolicy.FromResolution(id, FailedResolution());
Assert.AreEqual(HardwareInspectionFailureKind.CriticalEvidence, failed.FailureKind);
Assert.AreEqual("HI-EVIDENCE-UNRESOLVED", failed.SafeDiagnosticCode);
```

Use `HardwareResolutionTestData` to create real snapshots/manifests. Assert acquisition codes map to `HI-TOOL-NOT-AVAILABLE`, `HI-TOOL-INTEGRITY`, `HI-RUNTIME-PACKAGE`; collection codes map to `HI-PROVIDER-UNAVAILABLE`, `HI-ORCHESTRATION-FAILED`, `HI-PROGRESS-CALLBACK`.

- [ ] **Step 2: Run RED**

Build packaged Debug/x64 and run `HardwareInspectionOutcomePolicyTests|HardwareOrchestrationContractTests`. Expected: missing Gate 7 types.

- [ ] **Step 3: Implement minimal closed contracts and exhaustive mapping**

Use exhaustive enum switches. Copy/sort closed diagnostics. Only resolver success calls `CreateCompleted`; all failures call `CreateFailed`. Never expose resolver/provider diagnostics as arbitrary public text.

- [ ] **Step 4: Run GREEN plus Gate 6 resolution tests**

Set `$filter` to `FullyQualifiedName~Features.HardwareInspection.Orchestration|FullyQualifiedName~Features.HardwareInspection.Resolution`; require all discovered tests passed and zero skipped.

- [ ] **Step 5: Commit**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Orchestration' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Orchestration'
git commit -m "feat(hardware-inspection): add Gate 7 orchestration contracts"
```

---

### Task 2: Pin administrator-installed LLM Fit authority

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Orchestration/LlmFitToolAuthority.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Orchestration/LlmFitToolAuthorityTests.cs`

**Interfaces:** `Manifest` returns one immutable `TrustedToolPackageManifest`; `GetProductionPackageRoot()` has no parameter; internal `Verify(verifier, approvedRoot, packageRoot)` is test-only.

- [ ] **Step 1: Write RED tests for exact authority and filesystem attacks**

Assert tool/version/hash/AMD64/disposition, exact case-sensitive members, exact commands, zero-parameter production root, and no public path/URI/config/registry/environment override. Temporary packages prove missing/extra/changed file, reparse package, wrong architecture, and approved-root escape fail through the real verifier. Do not add a hash-override seam: no synthetic executable can match the pinned real SHA-256. The existing Foundation verifier suite remains the deterministic successful-package proof; Gate 9 verifies the actual administrator-installed pinned package.

- [ ] **Step 2: Run RED**

Run `FullyQualifiedName~LlmFitToolAuthorityTests`; expect missing authority.

- [ ] **Step 3: Implement the exact manifest**

```csharp
new TrustedToolPackageManifest(
    LlmFitCommandContract.ToolId,
    LlmFitCommandContract.Version,
    "llmfit.exe",
    "db82bcb17f065b7ff7528ffe9904b2b0e1cce0c843fcd659b4ee1432a2e72e19",
    ["llmfit.exe", "LICENSE", "README.md"],
    PeMachine.Amd64,
    TrustedToolPackageDisposition.FunctionalPassWithPackagingConcern,
    [LlmFitCommandContract.CreateVersionCommand(),
     LlmFitCommandContract.CreateSystemCommand()]);
```

Derive the root only from `Environment.GetFolderPath(CommonApplicationData)` and fixed leaves. Exceptions contain no path.

- [ ] **Step 4: Run GREEN plus Foundation trusted-tool tests**

Set `$filter` to `FullyQualifiedName~LlmFitToolAuthorityTests|FullyQualifiedName~TrustedTool`; run the packaged recipe and require a fully passing TRX.

- [ ] **Step 5: Commit `feat(hardware-inspection): pin administrator LLM Fit authority`**

---

### Task 3: Parse the packaged probe manifest and acquire both tools

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Orchestration/LlamaCppProbeManifestParser.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Orchestration/FixedHardwareToolAcquisition.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Orchestration/LlamaCppProbeManifestParserTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Orchestration/FixedHardwareToolAcquisitionTests.cs`

**Interfaces:** `Parse(ReadOnlySpan<byte>)` returns `TrustedToolPackageManifest` or constant-message `InvalidDataException`. `CreateProduction()` binds fixed roots. Internal construction accepts exact test roots/manifest bytes. `Acquire()` verifies LLM Fit then probe and returns both or one closed code.

- [ ] **Step 1: Write strict parser/acquisition RED tests**

Accept only schema 1, pinned identity, exact executable/hash/machine/disposition, 1..64 unique safe leaf members, exact commands, strict UTF-8 without BOM, one LF/no CR, max depth 8, and no unknown/duplicate/case-drifted properties. Mutate every field; also test malformed UTF-8, BOM, CRLF, unsafe/duplicate/65 members, command order/arguments.

Acquisition tests prove valid dual custody; missing LLM -> `ToolNotAvailable`; rejected LLM -> `ToolIntegrityFailure`; absent/rejected probe -> `PackagedProbeUnavailable`; partial custody is disposed; double disposal is safe.

- [ ] **Step 2: Run RED**

Run both new classes; expect missing parser/acquisition.

- [ ] **Step 3: Implement strict parser and all-or-nothing custody**

Use `JsonDocumentOptions` with comments/trailing commas disabled and max depth 8, explicit property lists, bounded enumeration, ordinal comparisons, and strict `UTF8Encoding(false, true)`. Verify in fixed order; use `finally` to dispose partial custody unless ownership transfers to `HardwareToolLease`.

- [ ] **Step 4: Run GREEN plus package contracts**

Set `$filter` to `FullyQualifiedName~LlamaCppProbeManifestParserTests|FullyQualifiedName~FixedHardwareToolAcquisitionTests|FullyQualifiedName~LlamaCppProbePackageContractTests`; the packaged build itself runs the manifest generator/verifier. Require build exit 0 and a fully passing TRX.

- [ ] **Step 5: Commit `feat(hardware-inspection): acquire verified hardware tools`**

---

### Task 4: Adapt existing providers behind one capture port

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Orchestration/IHardwareEvidenceCapture.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Orchestration/FoundationHardwareEvidenceCapture.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Orchestration/FoundationHardwareEvidenceCaptureTests.cs`

**Interface:**

```csharp
internal interface IHardwareEvidenceCapture
{
    ValueTask<WindowsProcessorEvidence> CaptureProcessorAsync(CancellationToken token);
    ValueTask<WindowsSystemSnapshot> CaptureSystemAsync(CancellationToken token);
    ValueTask<WindowsStorageEvidence> CaptureStorageAsync(CancellationToken token);
    ValueTask<DxgiGraphicsEvidence> CaptureGraphicsAsync(CancellationToken token);
    ValueTask<NeuralProcessorEvidence> CaptureNeuralProcessorAsync(CancellationToken token);
    Task<LlmFitHardwareEvidence> CaptureLlmFitAsync(VerifiedTrustedTool tool, CancellationToken token);
    Task<LlamaCppCapabilityEvidence> CaptureLlamaCppAsync(VerifiedTrustedTool tool, CancellationToken token);
}
```

- [ ] **Step 1: Write forwarding/cancellation RED tests**

Inject seven providers through an internal constructor. Require one matching call, identical token/tool, identical evidence instance, and no swallowed cancellation/exception.

- [ ] **Step 2: Run RED**

Set `$filter` to `FullyQualifiedName~FoundationHardwareEvidenceCaptureTests`; run the packaged recipe and require the expected missing-type compile failure.

- [ ] **Step 3: Implement one-line forwarders**

Production construction creates existing Windows/DXGI/NPU providers, one shared `ExternalProcessRunner`, and existing external providers. It performs no capture.

- [ ] **Step 4: Run GREEN plus provider suites**

Set `$filter` to `FullyQualifiedName~FoundationHardwareEvidenceCaptureTests|FullyQualifiedName~EvidenceProvider|FullyQualifiedName~WindowsSystemSnapshotProvider|FullyQualifiedName~DxgiGraphicsEvidenceProvider|FullyQualifiedName~UnavailableNeuralProcessorProbe`; require a fully passing TRX.

- [ ] **Step 5: Commit `feat(hardware-inspection): adapt Gate 7 evidence providers`**

---

### Task 5: Collect evidence with bounded overlap and complete cleanup

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Orchestration/IHardwareEvidenceCollectionCoordinator.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Orchestration/HardwareEvidenceCollectionCoordinator.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Orchestration/HardwareEvidenceCollectionCoordinatorTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Orchestration/HardwareEvidenceCaptureTestDouble.cs`

**Interface:** `IHardwareEvidenceCollectionCoordinator.CollectAsync(HardwareToolLease, IProgress<HardwareInspectionRunStage>, CancellationToken)` returns `Task<HardwareEvidenceCollectionResult>`. `HardwareEvidenceCollectionCoordinator` implements it. Production lanes are 4 native/1 external; an internal constructor accepts bounds for tests.

- [ ] **Step 1: Write concurrency/lifecycle RED tests**

The controllable double records started/completed signals and active maxima. Prove category stages 2..5 once/in order; max native 4; max external 1; native/external overlap; cancellable lane waits; cancellation reaches siblings and cleanup is awaited; returned optional-unavailable evidence does not cancel; all providers run once/no retry; success builds one aggregate.

Prove exact Windows exception conversion. If Foundation string constants are inaccessible, first expose a public closed `WindowsSystemSnapshotDiagnosticCode` enum test-first; do not compare arbitrary messages. Unexpected diagnostics/provider exceptions must cancel siblings, await cleanup, return `OrchestrationFailure`, and retain no exception text.

- [ ] **Step 2: Run RED**

Set `$filter` to `FullyQualifiedName~HardwareEvidenceCollectionCoordinatorTests`; run the packaged recipe and require the expected missing-coordinator compile failure.

- [ ] **Step 3: Implement two lanes and failure cleanup**

Use one linked CTS. Start native calls via `Task.Run` and a `SemaphoreSlim(4,4)` helper; external calls use `SemaphoreSlim(1,1)`. Record all tasks before await. Release permits in `finally`. On unexpected failure cancel siblings and observe `Task.WhenAll` solely for cleanup.

- [ ] **Step 4: Run GREEN five consecutive times, then Gate 6 resolver tests**

Set `$filter` to `FullyQualifiedName~HardwareEvidenceCollectionCoordinatorTests|FullyQualifiedName~Features.HardwareInspection.Resolution`. Build once, run VSTest five consecutive times with distinct TRX names, and require stable counts, zero non-passing results, and no residual probe/fake process.

- [ ] **Step 5: Commit `feat(hardware-inspection): collect bounded hardware evidence`**

---

### Task 6: Implement service progress, cancellation, resolution, and outcomes

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Orchestration/IHardwareEvidenceResolver.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Orchestration/HardwareInspectionService.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Resolution/HardwareEvidenceResolver.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Orchestration/HardwareInspectionServiceTests.cs`

**Interfaces:** Resolver interface exposes the existing `Resolve(Guid, CollectedHardwareEvidence)` signature. Service consumes `IHardwareToolAcquisition`, `IHardwareEvidenceCollectionCoordinator`, and `IHardwareEvidenceResolver`; the public API stays `IHardwareInspectionService.RunAsync` with no path/manifest/command/model overload.

- [ ] **Step 1: Write complete service RED tests**

Prove invalid ID/null progress rejection; pre-cancel has no calls/progress; exact stages and sequence 1..7; acquisition after stage 1; resolver once after stage 6; stage 7 before mapping; four outcomes; exact acquisition/collection codes; handoff only on completed results; callback failure cancels and suppresses later progress; cancellation before resolver skips it; cancellation after resolution wins before reporting; custody outlives cleanup; arbitrary exceptions become `HI-ORCHESTRATION-FAILED` without text.

- [ ] **Step 2: Run RED**

Set `$filter` to `FullyQualifiedName~HardwareInspectionServiceTests`; run the packaged recipe and require the expected missing service/resolver compile failure.

- [ ] **Step 3: Implement the thin top-to-bottom lifecycle**

```csharp
progress.Report(new HardwareInspectionRunProgress(
    inspectionId,
    checked(++sequence),
    stage));
```

Order is validate -> cancellation -> stage 1 -> acquire -> collection/stages 2..5 -> cancellation -> stage 6 -> resolve once -> cancellation -> stage 7 -> policy -> dispose. Catch cooperative cancellation separately. Do not catch process-corruption exceptions.

- [ ] **Step 4: Run GREEN plus lifecycle and Gate 6 tests**

Set `$filter` to `FullyQualifiedName~HardwareInspectionServiceTests|FullyQualifiedName~HardwareInspectionViewModelTests|FullyQualifiedName~HardwareInspectionPageTests|FullyQualifiedName~HardwareInspectionJourneyTests|FullyQualifiedName~HardwareInspectionRunContractTests|FullyQualifiedName~Features.HardwareInspection.Resolution`; require a fully passing TRX.

- [ ] **Step 5: Commit `feat(hardware-inspection): orchestrate production inspection runs`**

---

### Task 7: Activate production composition after focused GREEN

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Orchestration/HardwareInspectionComposition.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionJourneyTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Orchestration/HardwareInspectionCompositionTests.cs`

**Interface:** `CreateProduction()` returns `IHardwareInspectionService` and performs no verification, capture, or process launch. Internal shell injection remains unchanged.

- [ ] **Step 1: Write composition RED tests**

Require production type `HardwareInspectionService`, zero construction-time activity, no public-shell reference to unavailable service, retained internal injection, and zero orchestration/Foundation items in evaluated x86 graph.

- [ ] **Step 2: Run RED**

Set `$filter` to `FullyQualifiedName~HardwareInspectionCompositionTests|FullyQualifiedName~HardwareInspectionJourneyTests`; run the packaged recipe. The source/behavior assertion must fail because the public shell still uses the unavailable service.

- [ ] **Step 3: Create the graph and switch only the public constructor**

```csharp
IHardwareToolAcquisition tools = FixedHardwareToolAcquisition.CreateProduction();
IHardwareEvidenceCapture capture = new FoundationHardwareEvidenceCapture();
IHardwareEvidenceCollectionCoordinator collector =
    new HardwareEvidenceCollectionCoordinator(capture, TimeProvider.System);
IHardwareEvidenceResolver resolver = new HardwareEvidenceResolver(TimeProvider.System);
return new HardwareInspectionService(tools, collector, resolver);
```

Do not change navigation, handoff claim, ViewModel pacing, or Model Inspection.

- [ ] **Step 4: Run GREEN and authoritative packaged regression**

Run all orchestration tests, then `Features.HardwareInspection|ModelInspectionHandoff|OnboardingHardwareInspectionNavigationTests`. Parse TRX; require total=passed, zero skipped/not-executed.

- [ ] **Step 5: Commit `feat(hardware-inspection): activate Gate 7 production service`**

---

### Task 8: Regress, audit, review, and record evidence

**Files:**
- Modify: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/README.md`
- Modify: `docs/reviews/2026-08-22-hardware-inspection-integration-preservation-matrix.md`
- Modify: `docs/testing/evidence/README.md`
- Create: `docs/testing/evidence/2026-08-24-hardware-inspection-gate-7-orchestration.md`

- [ ] **Step 1: Run fresh ordinary suites**

```powershell
dotnet test tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/GraniteEdgeAI.HardwareInspection.Foundation.Tests.csproj -p:Platform=x64 --no-ansi
dotnet test tests/UnitTests/GraniteEdgeAI.HardwareInspection.LlamaCppProbe.Tests/GraniteEdgeAI.HardwareInspection.LlamaCppProbe.Tests.csproj -p:Platform=x64 --no-ansi
Set-ExecutionPolicy -Scope Process Bypass -Force
python -B -m unittest discover -s tests/testing/hardware_inspection -v
```

Require total=passed and zero skipped. Keep Smart App Control enabled; sign only generated test binaries with the existing trusted development identity if required.

- [ ] **Step 2: Run packaged/build regressions**

Run focused Gate 6-7 and authoritative packaged filters with fresh TRX files. Build packaged Debug/x64 tests and application Release/x64 MSIX with zero errors. Record actual counts and known warnings.

- [ ] **Step 3: Audit architecture, security, privacy, and x86 exclusion**

```powershell
rg -n 'Process\.Start|UseShellExecute|cmd\.exe|powershell|TcpListener|HttpListener|HttpClient' 'IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Orchestration'
rg -n 'ModelInspection|GGUF|OpenVINO|Compatibility' 'IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Orchestration'
rg -n 'HostName|UserName|Stdout|Stderr|RawOutput|Exception\.Message' 'IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/Orchestration'
rg -n 'UnavailableHardwareInspectionService' 'IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs'
git diff --check
rg -n '^(<<<<<<< |=======$|>>>>>>> )' --glob '!docs/superpowers/plans/*'
```

Inspect every hit. Audit the exact Gate 7 range for new dependencies, downloads/URLs, override paths, certificates/keys/binaries/packages/TRX/raw evidence/private paths/host facts/model data. Evaluate x86 Compile/ProjectReference items and require zero orchestration/Foundation entries.

- [ ] **Step 4: Review the exact range**

Review authority, roots, JSON, custody, disposal, concurrency, cancellation races, sibling cleanup, progress, single resolution, terminal exclusivity, safe codes, handoff eligibility, activation, and x86 exclusion. Fix findings test-first. If review is inline, label it inline and preserve the independent-review exception.

- [ ] **Step 5: Write evidence with exact non-claims**

- Gate 7 activates verified orchestration, not LLM Fit redistribution or approval.
- Missing/unverifiable administrator LLM Fit fails closed.
- The current successful real route is `CompletedWithWarnings` because instruction sets remain unavailable.
- Gate 8 WinUI integration and visual/accessibility acceptance remain incomplete.
- Gate 9 supported Intel-machine, offline/no-port, signing, and final evidence remain incomplete.
- No model compatibility or fit conclusion is produced.

Commit no test artifact, package, certificate, tool file, raw result, host label, or machine path.

- [ ] **Step 6: Verify and commit evidence**

Run links/private-data/whitespace checks and a final focused packaged test, then commit `docs(hardware-inspection): record Gate 7 verification`.

Gate 7 is complete only after this evidence commit. Gate 8 is next; the complete feature remains gated through Gate 9.
