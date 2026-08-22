# Hardware Inspection Gate 4 Windows and DXGI Enrichment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans for inline execution. Apply superpowers:test-driven-development to every production behavior and superpowers:systematic-debugging to every unexpected failure.

**Goal:** Add bounded provider-specific Windows processor, memory/OS, storage, DXGI graphics, and provisional NPU evidence without canonical resolution or product activation.

**Architecture:** Keep every native structure and API behind an internal injectable seam in `GraniteEdgeAI.HardwareInspection.Foundation`. Public immutable evidence exposes only bounded hardware facts, UTC timestamps, states, and closed diagnostics. Independent providers remain unresolved inputs until Gate 6.

**Tech Stack:** C# 12, .NET 8 Windows x64, Kernel32, Windows Registry, DXGI 1.1 COM interop, MSTest 4.3.2, Microsoft Testing Platform.

**Spec:** `docs/superpowers/specs/2026-08-23-hardware-inspection-gate-4-windows-dxgi-enrichment-design.md`

## Global constraints

- `UnavailableHardwareInspectionService` remains the sole product composition.
- Do not add canonical resolution, tolerance, normalization, compatibility, model data, llama.cpp, orchestration, UI, network, process launch, shell, persistence, or logging.
- Native APIs return only internal result enums/DTOs; raw HRESULTs, paths, registry locations, handles, and exception text never enter public evidence.
- Bound text before retention, topology/native buffers before parsing, adapters before retaining a 65th entry, and every public enumerable before unbounded materialization.
- Preserve installed, OS-usable, and available RAM separately. Preserve dedicated-video, dedicated-system, and shared-system graphics memory separately.
- Zero DXGI adapters is available/absent evidence. DXGI failure is unavailable. NPU `NotPresent` and `DetectionUnavailable` are never interchangeable.
- No unsafe code and no new NuGet dependency.
- Use RED, GREEN, REFACTOR and end every task in an independently reviewable commit.
- Test output remains ignored under `TestResults/HardwareInspection/Gate4/`.

---

### Task 1: Centralize bounded hardware text and harden memory/OS evidence

**Files:**
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Validation/HardwareText.cs`
- Modify: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/LlmFit/LlmFitHardwareEvidence.cs`
- Modify: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Windows/WindowsSystemSnapshot.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/LlmFit/LlmFitContractTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Windows/WindowsSystemSnapshotProviderTests.cs`

**Interfaces:**
- Produces internal `HardwareText.IsSafe(string?, int maximumScalarCount)` and `HardwareText.Validate(...)`.
- Preserves every public Gate 2/3 contract.

- [ ] **Step 1: Write RED shared-text and OS tests**

Require processor/OS/hardware names to reject empty text, boundary whitespace, NUL, C0/C1 controls, Unicode format/bidi controls, line/paragraph separators, unpaired surrogates, and more than 256 Unicode scalar values. Require valid composed Unicode and embedded ordinary spaces to remain accepted. Retain the existing LLM Fit success cases unchanged.

- [ ] **Step 2: Run focused RED**

```powershell
dotnet test tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/GraniteEdgeAI.HardwareInspection.Foundation.Tests.csproj `
  -p:Platform=x64 --filter 'FullyQualifiedName~LlmFitContractTests|FullyQualifiedName~WindowsSystemSnapshotProviderTests' --no-ansi
```

Expected: one or more format/separator/length cases are accepted.

- [ ] **Step 3: Implement one scalar-aware validator**

Use `Rune.DecodeFromUtf16` and `UnicodeCategory`. Reject invalid decoding plus `Control`, `Format`, `LineSeparator`, and `ParagraphSeparator` categories without normalizing, truncating, or changing evidence text. Count Unicode scalar values rather than UTF-16 code units; permit valid combining marks and private-use characters instead of inventing locale-specific display policy. Make the existing LLM Fit helper delegate to this validator; make `WindowsSystemSnapshot` validate all OS text through it.

- [ ] **Step 4: Run GREEN and full foundation regression**

Require the focused classes and the full project to pass with zero skipped.

- [ ] **Step 5: Commit**

```powershell
git add infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Validation `
  infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/LlmFit/LlmFitHardwareEvidence.cs `
  infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Windows/WindowsSystemSnapshot.cs `
  tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/LlmFit/LlmFitContractTests.cs `
  tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Windows/WindowsSystemSnapshotProviderTests.cs
git commit -m "refactor(hardware-inspection): centralize bounded hardware text"
```

---

### Task 2: Add immutable Windows processor evidence and provider policy

**Files:**
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Windows/WindowsProcessorEvidence.cs`
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Windows/WindowsProcessorEvidenceProvider.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Windows/WindowsProcessorEvidenceProviderTests.cs`

**Interfaces:**
- Produces `WindowsProcessorEvidenceState`, `WindowsProcessorArchitecture`, `WindowsProcessorDiagnosticCode`, `WindowsProcessorEvidence`, and `WindowsProcessorEvidenceProvider`.
- Consumes internal `IWindowsProcessorApi` returning a native-neutral `WindowsProcessorApiResult`.

- [ ] **Step 1: Write RED evidence invariant tests**

Require:

- available evidence has safe name, UTC time, a defined architecture, `1..4096` physical cores, `1..4096` logical processors, `physical <= logical`, and no diagnostics;
- unavailable evidence has no facts and exactly one defined processor diagnostic;
- inputs and enum values are validated without arbitrary strings.

- [ ] **Step 2: Write RED provider mapping tests**

Use a fake `IWindowsProcessorApi` to cover successful x64 mapping, cancellation before API access, name unavailable, topology unavailable, invalid record result, unsupported architecture, and impossible physical/logical relationships. Require one API call, closed diagnostics, UTC conversion, and no exception/error text.

- [ ] **Step 3: Run RED**

Expected: processor types are missing.

- [ ] **Step 4: Implement contracts and linear provider mapping**

The provider checks cancellation, invokes the API once, maps only closed API outcomes, validates all facts through factories, and never catches arbitrary exceptions. No registry/native class is added in this task.

- [ ] **Step 5: Run GREEN and commit**

```powershell
git add infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Windows/WindowsProcessorEvidence.cs `
  infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Windows/WindowsProcessorEvidenceProvider.cs `
  tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Windows/WindowsProcessorEvidenceProviderTests.cs
git commit -m "feat(hardware-inspection): add Windows processor evidence"
```

---

### Task 3: Implement and prove the bounded Windows processor native adapter

**Files:**
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Windows/Kernel32WindowsProcessorApi.cs`
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Windows/RegistryProcessorNameSource.cs`
- Modify: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Windows/WindowsProcessorEvidenceProvider.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Windows/Kernel32WindowsProcessorApiTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Windows/WindowsProcessorIntegrationTests.cs`

**Interfaces:**
- Implements `IWindowsProcessorApi` using `GetLogicalProcessorInformationEx`, `GetActiveProcessorCount`, native architecture mapping, and a bounded registry-name source.

- [ ] **Step 1: Write RED topology-buffer tests**

Separate buffer parsing from P/Invoke. Use synthetic byte buffers to cover one/multiple physical cores, multi-group logical counts, zero size, record smaller than its header, record beyond buffer, no forward progress, unsupported relationship, more than 4096 cores, and trailing bytes. No test uses unsafe code.

- [ ] **Step 2: Write RED registry/architecture tests**

Use seams for missing/wrong-type/unsafe/oversized processor names and supported/unsupported native architecture values. Registry paths and exceptions must collapse to an internal unavailable result.

- [ ] **Step 3: Run RED**

Expected: native adapter/parser types are missing.

- [ ] **Step 4: Implement bounded two-call native query**

Call `GetLogicalProcessorInformationEx` first for required size, reject zero or a size above 1 MiB, allocate one unmanaged buffer, call again, parse only validated records, and release in `finally`. Use `GetActiveProcessorCount(0xffff)` and reject zero/overflow. Read only `ProcessorNameString` from the approved registry key through `RegistryProcessorNameSource`. Map architecture without using process architecture.

- [ ] **Step 5: Add privacy-safe Windows smoke test**

The real provider must return available evidence on the supported Windows test host. Assert ranges, safe text, and UTC only; do not print or persist the host processor name.

- [ ] **Step 6: Run GREEN and commit**

```powershell
git add infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Windows `
  tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Windows/Kernel32WindowsProcessorApiTests.cs `
  tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Windows/WindowsProcessorIntegrationTests.cs
git commit -m "feat(hardware-inspection): collect bounded Windows processor facts"
```

---

### Task 4: Add system-volume storage evidence and native adapter

**Files:**
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Windows/WindowsStorageEvidence.cs`
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Windows/WindowsStorageEvidenceProvider.cs`
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Windows/Kernel32WindowsStorageApi.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Windows/WindowsStorageEvidenceProviderTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Windows/WindowsStorageIntegrationTests.cs`

**Interfaces:**
- Produces `WindowsStorageEvidenceState`, `WindowsStorageDiagnosticCode`, `WindowsStorageEvidence`, and `WindowsStorageEvidenceProvider`.
- Consumes internal `IWindowsStorageApi` returning capacity and bytes available to the caller only.

- [ ] **Step 1: Write RED contract/provider tests**

Cover capacity positive, `available <= capacity`, UTC, unavailable-no-facts, undefined diagnostics, cancellation before API access, system-directory failure, invalid volume root, disk API failure, inconsistent values, and successful exact `ulong` mapping.

- [ ] **Step 2: Run RED**

Expected: storage types are missing.

- [ ] **Step 3: Implement evidence and provider**

Keep the system-volume path inside the native adapter. Public evidence exposes only capacity, caller-available bytes, timestamp, state, and one closed diagnostic.

- [ ] **Step 4: Implement native adapter and smoke test**

Use bounded `GetSystemWindowsDirectoryW` retrieval, `Path.GetPathRoot` internally, and `GetDiskFreeSpaceExW`. Reject truncation and never expose the root. The real smoke test asserts only safe numeric relationships.

- [ ] **Step 5: Run GREEN and commit**

```powershell
git add infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Windows `
  tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Windows/WindowsStorageEvidenceProviderTests.cs `
  tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Windows/WindowsStorageIntegrationTests.cs
git commit -m "feat(hardware-inspection): collect system-volume evidence"
```

---

### Task 5: Add bounded DXGI graphics contracts and provider mapping

**Files:**
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Dxgi/DxgiGraphicsEvidence.cs`
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Dxgi/DxgiGraphicsEvidenceProvider.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Dxgi/DxgiGraphicsEvidenceProviderTests.cs`

**Interfaces:**
- Produces `DxgiGraphicsEvidenceState`, `DxgiAdapterKind`, `DxgiGraphicsDiagnosticCode`, `DxgiAdapterEvidence`, `DxgiGraphicsEvidence`, and `DxgiGraphicsEvidenceProvider`.
- Consumes internal `IDxgiAdapterApi` returning a bounded native-neutral result.

- [ ] **Step 1: Write RED evidence tests**

Require immutable copied adapters, safe names, defined kind, vendor/device IDs, three independent memory fields, UTC, zero-adapter available evidence, unavailable-no-adapters, closed diagnostics, and public enumeration stopping before a 65th retained adapter. Duplicate display names are allowed because ordinal occurrence is part of provider identity.

- [ ] **Step 2: Write RED provider tests**

Use a fake API for:

- integrated-style adapter (`0` dedicated video plus shared memory);
- discrete adapter with dedicated video memory;
- software and remote adapters;
- multiple adapters with equal display names;
- successful zero adapters;
- factory unavailable, enumeration failed, invalid description, and adapter-limit outcomes;
- cancellation before API access and UTC conversion.

Assert memory fields are never summed.

- [ ] **Step 3: Run RED**

Expected: DXGI contracts/provider are missing.

- [ ] **Step 4: Implement contracts and provider**

The provider maps one API result into immutable evidence. It does not identify Intel, rank adapters, select a primary adapter, calculate usable graphics memory, or infer GPU absence from failures.

- [ ] **Step 5: Run GREEN and commit**

```powershell
git add infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Dxgi `
  tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Dxgi/DxgiGraphicsEvidenceProviderTests.cs
git commit -m "feat(hardware-inspection): add DXGI graphics evidence"
```

---

### Task 6: Implement and prove minimal DXGI 1.1 COM enumeration

**Files:**
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Dxgi/DxgiAdapterApi.cs`
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Dxgi/DxgiInterop.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Dxgi/DxgiAdapterApiTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Dxgi/DxgiGraphicsIntegrationTests.cs`

**Interfaces:**
- Implements `IDxgiAdapterApi` through `CreateDXGIFactory1`, `IDXGIFactory1.EnumAdapters1`, and `IDXGIAdapter1.GetDesc1` only.

- [ ] **Step 1: Write RED COM-loop tests through an injectable interop seam**

Require:

- `DXGI_ERROR_NOT_FOUND` terminates as success;
- any other failed HRESULT maps to enumeration failure;
- factory failure maps separately;
- every acquired adapter and factory is released once on success and all failures;
- a 65th adapter maps to limit exceeded without retaining it;
- native fixed-buffer descriptions are trimmed only at their terminator/boundary and then validated;
- DXGI flag combinations map deterministically to hardware/software/remote;
- `nuint` memory converts checked to `ulong`.

- [ ] **Step 2: Run RED**

Expected: DXGI adapter/interop types are missing.

- [ ] **Step 3: Implement minimal COM definitions**

Use `[ComImport]`, `[Guid]`, `InterfaceIsIUnknown`, exact vtable method ordering through the used interfaces, `PreserveSig` HRESULTs, and `CreateDXGIFactory1` from system `dxgi.dll`. Do not create D3D devices or enable unsafe code. Release COM objects in `finally` and map raw HRESULTs immediately to internal outcomes.

- [ ] **Step 4: Add privacy-safe Windows smoke test**

Real enumeration must return available bounded evidence on the supported test host, with at most 64 adapters and valid independent memory values. Assert structure only; do not print or persist adapter names or IDs.

- [ ] **Step 5: Run the real DXGI class three times**

From a physical short worktree, require all repetitions to pass with zero skipped and no leaked process/native resource symptom.

- [ ] **Step 6: Run GREEN and commit**

```powershell
git add infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Dxgi `
  tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Dxgi
git commit -m "feat(hardware-inspection): enumerate DXGI adapters safely"
```

---

### Task 7: Add the provisional neural-processor boundary

**Files:**
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/NeuralProcessors/INeuralProcessorProbe.cs`
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/NeuralProcessors/NeuralProcessorEvidence.cs`
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/NeuralProcessors/UnavailableNeuralProcessorProbe.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/NeuralProcessors/NeuralProcessorEvidenceTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/NeuralProcessors/UnavailableNeuralProcessorProbeTests.cs`

**Interfaces:**
- Produces `INeuralProcessorProbe.CaptureAsync`, `NeuralProcessorEvidenceState`, `NeuralProcessorDiagnosticCode`, and immutable evidence.

- [ ] **Step 1: Write RED state-invariant tests**

Require `Present` to have one safe name and no diagnostic, `NotPresent` to have neither, and `DetectionUnavailable` to have exactly one defined diagnostic and no name. Reject contradictory factories and undefined enum values.

- [ ] **Step 2: Write RED default-probe tests**

Require pre-cancellation propagation with the caller token and otherwise exact `DetectionUnavailable(EnumerationMechanismNotApproved)`. Assert the implementation has no registry, DXGI, process, OpenVINO, DirectML, network, or heuristic dependency.

- [ ] **Step 3: Run RED**

Expected: NPU contracts are missing.

- [ ] **Step 4: Implement the minimal truthful boundary**

Add no enumeration mechanism. The default probe checks cancellation and returns the fixed unavailable evidence with injected `TimeProvider` UTC time.

- [ ] **Step 5: Run GREEN and commit**

```powershell
git add infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/NeuralProcessors `
  tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/NeuralProcessors
git commit -m "feat(hardware-inspection): add provisional NPU evidence boundary"
```

---

### Task 8: Gate 4 verification, review, and evidence

**Files:**
- Modify: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/README.md`
- Modify: `docs/reviews/2026-08-22-hardware-inspection-integration-preservation-matrix.md`

**Interfaces:**
- Documents Gate 4 closure and Gate 5 entry; changes no runtime composition.

- [ ] **Step 1: Run fresh final-head foundation evidence**

From a physical short worktree:

1. run all foundation tests with the exact new discovered minimum and authoritative TRX;
2. run Windows processor/storage/DXGI smoke tests three times;
3. parse every TRX and require zero failed/skipped/not-executed.

- [ ] **Step 2: Run Gate and repository regressions**

Require:

- deterministic Gate tests at the Gate 3 floor 174 with TRX;
- packaged authoritative filter at floor 114 with TRX;
- Stage A 12/12 with process-scoped `PSExecutionPolicyPreference=Bypass`;
- Stage 0/acquisition/public-contract/theme 18/18;
- packaged Debug/x64 test build and app Debug/x64 plus Release/win-x64 builds with zero errors.

- [ ] **Step 3: Audit boundaries, resources, and packages**

Require:

```powershell
rg -n 'Process\.Start|UseShellExecute|cmd\.exe|powershell|GGUF|OpenVINO|ModelInspection|HardwareSnapshot' `
  infrastructure/GraniteEdgeAI.HardwareInspection.Foundation
rg -n 'UnavailableHardwareInspectionService' `
  'IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs'
git diff --check
rg -n '^(<<<<<<< |=======$|>>>>>>> )' --glob '!docs/superpowers/plans/*'
```

Treat search hits as an audit set, not an automatic zero-result assertion: inspect every hit and fail for a newly introduced prohibited dependency or boundary crossing. Inspect the exact Gate 4 range and Debug AppX recursively. Fail for newly tracked/packaged native capture, TRX, host processor/adapter/storage value, path, HRESULT, candidate, trusted/offline evidence, username, absolute machine path, or production registration. Confirm no leaked COM/process handles through repeated execution and no new package dependency.

- [ ] **Step 4: Request independent code review**

Use `superpowers:requesting-code-review`. Review topology buffer bounds/forward progress, registry/native exception mapping, storage path privacy, COM vtable/signatures/ownership, adapter bounds and memory semantics, NPU absence/unavailability, cancellation, evidence enumeration bounds, package isolation, and production inactivity. Resolve every Critical/Important finding test-first and rerun affected verification.

- [ ] **Step 5: Update documentation**

README records exact providers, native APIs, bounds, semantics, diagnostics, and noncanonical status. The preservation matrix records exact head, commands, counts, warnings, smoke-test behavior without host facts, review result, non-claims, and Gate 5 as next.

- [ ] **Step 6: Final verification and commit**

Use `superpowers:verification-before-completion`, require a clean tracked worktree after committing:

```powershell
git add infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/README.md `
  docs/reviews/2026-08-22-hardware-inspection-integration-preservation-matrix.md
git commit -m "docs(hardware-inspection): record Gate 4 verification"
```

Gate 4 is complete only after the evidence commit. The full feature remains incomplete; Gate 5 llama.cpp capability evidence is next.
