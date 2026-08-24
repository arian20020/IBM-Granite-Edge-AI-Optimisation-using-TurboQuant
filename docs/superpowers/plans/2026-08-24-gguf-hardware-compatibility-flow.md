# GGUF Hardware-to-Compatibility Flow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Connect the validated GGUF Model and Hardware handoffs to C1 and navigate from a completed Hardware Inspection to a real Compatibility decision without weakening either inspection boundary.

**Architecture:** C1 gains one public primitive/value input boundary that adapts into its existing internal ports. The WinUI application projects current owner evidence, captures fresh memory immediately before evaluation, and injects the evaluator into the existing Compatibility ViewModel. The onboarding shell owns identity validation and exactly-once navigation.

**Tech Stack:** C# 12, .NET 8, WinUI 3, MSTest 4, Microsoft.Testing.Platform.

**Design:** `docs/superpowers/specs/2026-08-24-gguf-end-to-end-decision-flow-design.md`

---

### Task 1: Public C1 production input boundary

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Presentation/CompatibilityProductionInput.cs`
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Presentation/CompatibilityEngine.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Presentation/CompatibilityProductionInputTests.cs`

- [ ] **Step 1: Write failing boundary tests**

Add tests that construct a valid input using two UUID-v4 run identities, GGUF scalar facts,
installed capacities, present device routes, verified backends, and a fresh resource reading.
Assert `CompatibilityEngine.Run(input)` reaches a state other than `NotEstablished` caused by
missing owner ports. Add validation tests for empty identities, zero model length, stale/non-UTC
observations, and null device/backend collections. Add a reflection assertion that the public
input types contain no path, filename, model name, provider output, diagnostic, hostname, or
free-form payload member.

- [ ] **Step 2: Run the C1 suite and observe RED**

Run:

```powershell
dotnet test --project tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/GraniteEdgeAI.ModelHardwareCompatibility.Tests.csproj --configuration Release --no-ansi
```

Expected: compilation fails because `CompatibilityProductionInput` is absent.

- [ ] **Step 3: Implement immutable public inputs**

Create public sealed inputs with factory validation:

```csharp
public sealed record GgufCompatibilityModelInput
{
    public long FileLengthBytes { get; }
    public int? LayerCount { get; }
    public int? EmbeddingSize { get; }
    public int? AttentionHeadCount { get; }
    public int? KeyValueHeadCount { get; }
    public int? DeclaredContextLimit { get; }
    public int? FileType { get; }
    public int? QuantisationVersion { get; }
}

public sealed record CompatibilityHardwareInput
{
    public ulong InstalledSystemMemoryBytes { get; }
    public ulong InstalledDedicatedDeviceMemoryBytes { get; }
    public ulong FreeStorageBytes { get; }
    public IReadOnlySet<DeviceRouteId> PresentDevices { get; }
    public IReadOnlySet<CompatibilityBackend> VerifiedBackends { get; }
}

public sealed record CompatibilityFreshResourcesInput
{
    public ulong AvailableSystemMemoryBytes { get; }
    public ulong AvailableDedicatedDeviceMemoryBytes { get; }
    public ulong AvailableStorageBytes { get; }
    public DateTimeOffset ObservedAtUtc { get; }
}

public sealed record CompatibilityProductionInput
{
    public Guid ModelInspectionRunId { get; }
    public Guid ProductHardwareRunId { get; }
    public GgufCompatibilityModelInput Model { get; }
    public CompatibilityHardwareInput Hardware { get; }
    public CompatibilityFreshResourcesInput FreshResources { get; }
}
```

Factories reject default/invalid identities, zero required capacities, invalid optional positive
facts and non-UTC observations. Freshness is validated by the application projector against its
injected clock: no more than 30 seconds old and no more than 5 seconds in the future. They
defensively copy sets. They carry no owner type and no string.

- [ ] **Step 4: Adapt the input into existing C1 ports**

Add `CompatibilityEngine.Run(CompatibilityProductionInput, CancellationToken)` and private
one-run implementations of `ICompatibilityInputGateway`, `IInspectedModelFactsSource`,
`IHardwareFactsSource`, and `IFreshSystemMemoryProbe`. Convert GUIDs with `ToString("N")` only at
the internal claim boundary. Use checked byte conversion, `SupportMatrix.ProvisionalV1()`, the
existing policies, imported GGUF CPU baseline, and application-default context. Always let the
coordinator execute and roll back its claim.

- [ ] **Step 5: Run GREEN and the privacy canary**

Run the whole C1 suite. Expected: all prior 771 tests plus the new tests pass, with non-zero
discovery and zero failures.

- [ ] **Step 6: Commit**

```powershell
git add shared/GraniteEdgeAI.ModelHardwareCompatibility.Core tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests
git commit -m "feat(compatibility): accept validated production inputs"
```

### Task 2: Application evidence projector

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Infrastructure/GgufCompatibilityInputProjector.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Infrastructure/ICompatibilityFreshMemorySource.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Infrastructure/WindowsCompatibilityFreshMemorySource.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/GgufCompatibilityInputProjectorTests.cs`

- [ ] **Step 1: Write failing projection tests**

Build real eligible `ModelInspectionHandoff`, terminal `ModelInspectionExecutionResult`, and usable
`HardwareInspectionHandoff` fixtures. Assert exact projection of model architecture scalars,
installed RAM, checked aggregate dedicated VRAM, storage, present device routes, and verified
CPU/SYCL/Vulkan backends. Assert absent optional facts stay null. Assert mismatched Model run,
Hardware run, ineligible terminal result, unusable Hardware input, overflow, non-UTC/stale fresh
memory, and unknown backends fail without producing an input.

- [ ] **Step 2: Run the focused test and observe RED**

Run the UnitTests project filtered to `GgufCompatibilityInputProjectorTests`. Expected:
compilation failure because the projector is absent.

- [ ] **Step 3: Implement the projector**

Expose one method:

```csharp
internal static bool TryProject(
    ModelInspectionHandoff modelHandoff,
    ModelInspectionExecutionResult terminalModelResult,
    Guid productHardwareRunId,
    HardwareInspectionHandoff hardwareHandoff,
    AvailableMemorySnapshot freshMemory,
    out CompatibilityProductionInput? input)
```

Require the terminal result to be completed and eligible, require exact Model run and model-length
agreement, and require `hardwareHandoff.InspectionId == productHardwareRunId`. Read only validated
configuration evidence. Derive device categories structurally from memory facts, not adapter names.
Map runtime backends through an exhaustive switch. Set fresh dedicated availability to zero until
a dedicated-memory availability collector exists; this deliberately prevents an unproved GPU-safe
result while allowing the verified CPU route.

- [ ] **Step 4: Implement fresh memory capture**

The Windows source wraps the existing `IAvailableMemoryProvider`/`WindowsAvailableMemoryProvider`
and returns one UTC `AvailableMemorySnapshot` per compatibility attempt. It does not reuse the
Hardware handoff's earlier availability value.

- [ ] **Step 5: Run GREEN and commit**

Run focused projector tests, Hardware contract tests, Model handoff tests, and the full UnitTests
project. Commit:

```powershell
git add "IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Infrastructure" tests/UnitTests/GraniteEdgeAI.UnitTests
git commit -m "feat(compatibility): project gguf and hardware evidence"
```

### Task 3: Inject a real evaluation into the C1 page

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/ViewModels/CompatibilityViewModel.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/CompatibilityPage.xaml.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/CompatibilityViewModelTests.cs`

- [ ] **Step 1: Write failing ViewModel tests**

Assert an injected `Func<CancellationToken, Task<CompatibilityScreenModel>>` runs exactly once per
`StartAsync`, publishes C1's decision, cancels correctly, and rejects a late result from a superseded
attempt. Assert the parameterless constructor retains the safe unavailable-adapter behavior used by
fixtures and direct construction.

- [ ] **Step 2: Observe RED**

Run the focused ViewModel tests. Expected: constructor/evaluator overload absent.

- [ ] **Step 3: Implement minimal injection**

Store the evaluator delegate in the ViewModel. The default delegate wraps
`CompatibilityEngine.RunWithAvailableAdapters`; the injected delegate is used by the shell. Add an
internal CompatibilityPage constructor accepting the evaluator and expose typed Back/Continue page
events that are raised from the existing commands. Do not put reasoning in the page.

- [ ] **Step 4: Run GREEN and commit**

Run ViewModel and packaged Compatibility UI tests. Commit:

```powershell
git add "IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility" tests/UnitTests/GraniteEdgeAI.UnitTests
git commit -m "feat(compatibility): inject production evaluation"
```

### Task 4: Exactly-once Hardware completion and shell navigation

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/HardwareInspectionPage.xaml.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/HardwareInspectionCompletedEventArgs.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionCompletionNavigationTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingCompatibilityNavigationTests.cs`

- [ ] **Step 1: Write failing completion tests**

Assert the Hardware page raises one completion event per attempt only when its snapshot carries a
usable handoff, and never for active, cancelled, blocked, failed, or duplicate revisions. Assert a
retry can raise one new completion for the new generation.

- [ ] **Step 2: Write failing shell tests**

Using injected navigators and fresh-memory source, assert the shell navigates exactly once from the
attached Hardware page to Compatibility with matching active identities. Assert mismatched IDs,
stale page, invalidated handoff, projection failure, memory failure, navigation failure, retry, and
journey abandonment do not navigate. Assert Back restores the completed Hardware page without
rerunning it. Assert Continue remains disabled until a later optimisation route is registered.

- [ ] **Step 3: Observe RED**

Run both focused test classes. Expected: completion event and compatibility navigator are absent.

- [ ] **Step 4: Implement Hardware completion event**

Track the last published attempt generation in `HardwareInspectionPage`. After applying a newer
snapshot, raise `InspectionCompleted` only when `snapshot.Handoff` is non-null and the presentation
is Completed/CompletedWithWarnings. Reset eligibility naturally when generation changes.

- [ ] **Step 5: Expose validated Model evidence without widening the public handoff**

Add an internal ModelInspectionPage accessor that returns its current terminal execution result only
when the requested handoff is the page's current issued handoff and the run IDs match. The exact
six-field `ModelInspectionHandoff` remains unchanged.

- [ ] **Step 6: Implement shell transaction and navigation**

Subscribe/unsubscribe to Hardware completion alongside existing actions. Capture fresh memory,
project inputs, construct a Compatibility page with an evaluator calling `CompatibilityEngine.Run`,
and replace StageFrame content only after all validation succeeds. Retain the completed Hardware page
for Back. Set the onboarding stage to CheckHardwareFit; the page's own stepper displays the compatibility
substep. Invalidate all retained compatibility state on new model, retry, or abandonment.

- [ ] **Step 7: Run GREEN and commit**

Run Hardware page, onboarding navigation, Model handoff, Compatibility ViewModel, and packaged UI
tests. Commit:

```powershell
git add "IBM Granite with TurboQuant (Intel)/Features" tests/UnitTests/GraniteEdgeAI.UnitTests
git commit -m "feat(onboarding): navigate hardware results to compatibility"
```

### Task 5: Full verification and drag-and-drop checkpoint

**Files:**
- Merge from the exact worker-provided ref: its existing Model Import drag-and-drop paths
- Test: existing Model Import, Model Inspection, Hardware Inspection, Compatibility, onboarding,
  privacy, and packaged suites

- [ ] **Step 1: Resolve the authoritative drag-and-drop ref**

Verify the worker-provided branch and commit locally or on `origin`. If it remains unavailable, record
that exact external dependency and do not fabricate an implementation. Continue verification of the
picker-based GGUF flow.

- [ ] **Step 2: Merge and reconcile when available**

Merge the authoritative ref with `--no-ff --no-commit`. Resolve only shared Model Import project/XAML
integration by preserving picker and drag-and-drop routes. Both must call the same quick-scan and
immutable request factory. Abort rather than choose between conflicting scanner or request contracts.

- [ ] **Step 3: Run complete verification**

Run:

- C1 Core suite;
- full packaged UnitTests project;
- Model Inspection Foundation and worker process tests;
- Hardware Foundation, packaged Hardware, deterministic Stage A, and Task 8 tests;
- Model Import quick-scan and navigation tests;
- Compatibility privacy and packaged UI tests;
- Debug x64 solution build;
- Release x64 application build;
- opt-in Compatibility fixture-gallery build;
- `git diff --check` and clean-worktree verification.

- [ ] **Step 4: Record final handoff**

Report exact branch, commits, changed paths, test counts, build results, and whether drag-and-drop was
merged or remains blocked on its missing authoritative ref. Do not claim OpenVINO, optimisation,
export, or Chat completion.
