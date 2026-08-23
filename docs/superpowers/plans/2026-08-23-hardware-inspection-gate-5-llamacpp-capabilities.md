# Hardware Inspection Gate 5 llama.cpp Capabilities Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a strict, trusted, child-process-only llama.cpp capability provider that records the pinned application runtime identity, CPU backend support, and runtime-visible devices without loading native code into WinUI or any test host.

**Architecture:** Extend the existing Hardware Inspection trusted-tool and Job-contained process foundation with strict UTF-8 output handling. A dedicated `win-x64` probe executable owns LLamaSharp/native loading and emits a closed JSON-v1 protocol; the Foundation project validates that protocol into immutable noncanonical evidence and remains independent of LLamaSharp types. The inactive probe ships under `HardwareInspection\LlamaCppProbe` in the signed application package, while all real/adverse process acceptance executes from the installed signed x64 AppX test package.

**Tech Stack:** C# 12, .NET 8 Windows x64, MSTest 4, Microsoft Testing Platform, `System.Text.Json`, LLamaSharp `0.27.0`, LLamaSharp.Backend.Cpu `0.27.0`, existing Win32 Job/custody boundary, Git.

**Spec:** `docs/superpowers/specs/2026-08-23-hardware-inspection-gate-5-llamacpp-capabilities-design.md`

## Global Constraints

- Use `LLamaSharp` `0.27.0` and `LLamaSharp.Backend.Cpu` `0.27.0`; mapped llama.cpp commit is exactly `3f7c29d318e317b63f54c558bc69803963d7d88c`.
- The helper is `win-x64`/AMD64 and CPU-only. A different package, backend, RID, or commit requires a new decision.
- Native llama/ggml libraries load only in `GraniteEdgeAI.HardwareInspection.LlamaCppProbe.exe`, never WinUI, Foundation, MSTest/VSTest, or the packaged parent test host.
- Reuse only `TrustedToolPackageVerifier`, live `VerifiedTrustedTool` custody, and `IExternalProcessRunner`; do not call the Model Inspection worker or copy a weaker process launcher.
- The manifest contains exactly `identity --format json-v1` and `capabilities --format json-v1`; no caller arguments, shell, PATH search, network, server, retry, or fallback.
- Identity uses 5 seconds and independent 4 KiB output limits. Capabilities uses 10 seconds and independent 64 KiB limits.
- Protocol is strict UTF-8, one JSON object, one LF, depth at most 8, no BOM/CR/CRLF/comments/trailing commas/duplicate/unknown/case-drifted fields.
- Retain at most 8 unique backends and 16 devices. Gate 5 accepts exactly backend `cpu`, contiguous ordinals from zero, and 1..16 safe buffer-type labels of at most 128 Unicode scalars.
- Cancellation propagates with the caller token. Missing/integrity/runtime failures are typed unavailability, never absent hardware.
- Retain no paths, hashes, stdout/stderr, exit codes, exceptions, native logs/pointers, host/account identity, model data, or unrestricted device/hardware names in evidence or committed artifacts.
- Package the inactive production probe only under `HardwareInspection\LlamaCppProbe`; do not locate, verify, launch, or register it in product composition.
- Real and adverse child-process acceptance must run from the installed signed x64 AppX test package. Do not execute new unsigned apphosts from loose output directories, disable Smart App Control, reuse previously trusted hashes, or substitute a system `dotnet.exe` launcher.
- The test-only adverse executable is packaged only in `GraniteEdgeAI.UnitTests`, never the production application package.
- Do not create `HardwareSnapshot`, normalize/resolve sources, infer compatibility, open a model, download/execute a candidate, or perform any operational Stage action.
- Every behavior change follows RED -> GREEN -> refactor, each task commits independently, and every Critical/Important/Minor review finding is resolved test-first.

---

### Task 1: Make the shared bounded process output reject invalid UTF-8

**Files:**
- Modify: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Processes/BoundedProcessOutput.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Processes/ExternalProcessRunnerTests.cs`

**Interfaces:**
- Consumes: `BoundedProcessOutput.Start(Stream, int)` and `BoundedProcessOutputResult(string Text, bool LimitExceeded, bool Failed)`.
- Produces: the same interfaces, with invalid UTF-8 returning `Failed: true` and no decoded text.

- [ ] **Step 1: Write RED strict-decoding tests**

Add tests that feed a real `MemoryStream` to `BoundedProcessOutput`:

```csharp
[TestMethod]
public async Task BoundedOutputRejectsInvalidUtf8WithoutReplacementText()
{
    using var stream = new MemoryStream([0x7b, 0xff, 0x7d]);
    BoundedProcessOutput output = BoundedProcessOutput.Start(stream, 16);

    BoundedProcessOutputResult result = await output.Completion;

    Assert.IsTrue(result.Failed);
    Assert.AreEqual(string.Empty, result.Text);
    Assert.IsFalse(result.LimitExceeded);
}
```

Retain a sibling case proving a valid multibyte scalar split across stream reads is decoded exactly. The mutation caught is restoring replacement fallback or retaining partially decoded attacker-controlled text.

- [ ] **Step 2: Run focused RED**

```powershell
dotnet test tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/GraniteEdgeAI.HardwareInspection.Foundation.Tests.csproj `
  -p:Platform=x64 --filter 'Name~BoundedOutputRejectsInvalidUtf8WithoutReplacementText' --no-ansi
```

Expected: the invalid sequence produces replacement text and `Failed` is false.

- [ ] **Step 3: Implement strict decoding**

Use one encoding instance:

```csharp
private static readonly Encoding StrictUtf8 = new UTF8Encoding(
    encoderShouldEmitUTF8Identifier: false,
    throwOnInvalidBytes: true);
```

After the bounded byte read completes, decode once inside `try`. Catch only `DecoderFallbackException`, return empty text with `Failed: true`, and always clear/return the pooled buffer. Do not catch arbitrary exceptions or change limit/stream-failure semantics.

- [ ] **Step 4: Run GREEN and process regression**

Run the focused test, all `ExternalProcessRunnerTests`, and then the full foundation project. Require zero failed/skipped.

- [ ] **Step 5: Commit**

```powershell
git add infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Processes/BoundedProcessOutput.cs `
  tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Processes/ExternalProcessRunnerTests.cs
git commit -m "fix(hardware-inspection): reject invalid process UTF-8"
```

---

### Task 2: Add immutable llama.cpp capability evidence and strict parsers

**Files:**
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/LlamaCpp/LlamaCppCapabilityEvidence.cs`
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/LlamaCpp/LlamaCppCapabilityJsonParser.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/LlamaCpp/LlamaCppCapabilityEvidenceTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/LlamaCpp/LlamaCppCapabilityJsonParserTests.cs`

**Interfaces:**
- Produces public `LlamaCppCapabilityEvidenceState`, `LlamaCppBackend`, `LlamaCppCapabilityDiagnosticCode`, `LlamaCppRuntimeIdentity`, `LlamaCppVisibleDevice`, and `LlamaCppCapabilityEvidence`.
- Produces internal `LlamaCppIdentityParseResult`, `LlamaCppCapabilitiesParseResult`, and `LlamaCppCapabilityJsonParser.ParseIdentity(string)` / `ParseCapabilities(string)`.
- Consumes existing `HardwareText.IsSafe(value, maximumScalarCount)`.

- [ ] **Step 1: Write RED evidence invariant tests**

Define the desired API through tests:

```csharp
LlamaCppCapabilityEvidence evidence = LlamaCppCapabilityEvidence.Available(
    LlamaCppRuntimeIdentity.PinnedCpu,
    new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero),
    [LlamaCppBackend.Cpu],
    [new LlamaCppVisibleDevice(0, "CPU")]);

Assert.AreEqual(LlamaCppCapabilityEvidenceState.Available, evidence.State);
Assert.AreEqual("3f7c29d318e317b63f54c558bc69803963d7d88c", evidence.RuntimeIdentity!.MappedLlamaCppCommit);
Assert.AreEqual(1, evidence.VisibleDevices.Count);
Assert.IsNull(evidence.Diagnostic);
```

Require immutable copies; exact pinned identity; UTC; exactly `Cpu`; 1..16 devices; contiguous unique ordinals; safe 1..128-scalar labels; and no diagnostic. Require unavailable evidence to contain no identity/backend/device facts and exactly one defined diagnostic. Reject undefined enums and contradictory factories.

- [ ] **Step 2: Run evidence RED**

Expected: the `LlamaCpp` contracts do not exist.

- [ ] **Step 3: Implement minimal evidence contracts**

Use closed records/enums and `ReadOnlyCollection<T>` copies. Define `PinnedCpu` with the literal identities in Global Constraints. Factories validate every invariant; no public constructor accepts arbitrary build identity or arbitrary diagnostic text.

Define exactly these diagnostics so provider mapping remains reviewable and cannot retain arbitrary messages:

```csharp
public enum LlamaCppCapabilityDiagnosticCode
{
    ToolIdentityMismatch,
    CommandContractMismatch,
    IdentityStartFailed,
    IdentityTimedOut,
    IdentityOutputLimitExceeded,
    IdentityCleanupFailed,
    IdentityCancelledUnexpectedly,
    IdentityNonZeroExit,
    IdentityOutputInvalid,
    IdentityMismatch,
    CapabilityStartFailed,
    CapabilityTimedOut,
    CapabilityOutputLimitExceeded,
    CapabilityCleanupFailed,
    CapabilityCancelledUnexpectedly,
    CapabilityProcessFailed,
    NativeCapabilityUnavailable,
    CapabilityOutputInvalid,
}
```

- [ ] **Step 4: Write RED strict identity/parser tests**

Use hand-written literal fixtures. The valid identity fixture must match the spec byte-for-byte and end in `\n`. The valid capability fixture must map exact CPU/device facts. Add separate cases for BOM, no LF, CRLF, extra LF/data, empty, malformed, comments, trailing comma, missing/unknown/duplicate/case-drifted fields, wrong identity, undefined backend, duplicate backend, zero/17 devices, duplicate/noncontiguous ordinal, unsafe/oversized label, and depth 9.

```csharp
LlamaCppCapabilitiesParseResult parsed =
    LlamaCppCapabilityJsonParser.ParseCapabilities(
        "{\"schemaVersion\":1,\"probeIdentity\":\"granite-edge-hardware-llamacpp-capabilities/1\",\"backends\":[\"cpu\"],\"devices\":[{\"ordinal\":0,\"bufferType\":\"CPU\"}]}\n");

Assert.IsTrue(parsed.IsValid);
Assert.AreEqual(LlamaCppBackend.Cpu, parsed.Backends.Single());
Assert.AreEqual(new LlamaCppVisibleDevice(0, "CPU"), parsed.Devices.Single());
```

Add a fixed-seed malformed primitive fuzz loop with bounded inputs and assert it never throws or returns more than the closed limits.

- [ ] **Step 5: Run parser RED**

Expected: parser types are missing.

- [ ] **Step 6: Implement one strict `Utf8JsonReader` parser**

Reject framing before parsing. Encode the already strictly decoded string with UTF-8, use `JsonReaderOptions` with comments/trailing commas disabled and `MaxDepth = 8`, track every property with bit flags, and reject duplicates/unknowns immediately. Parse arrays incrementally and stop before retaining a 9th backend or 17th device. Never deserialize into permissive DTOs and never preserve input or exception text.

- [ ] **Step 7: Run GREEN and commit**

Run both new classes and every Foundation test except the existing loose-apphost `ExternalProcessRunnerTests` class:

```powershell
dotnet test tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/GraniteEdgeAI.HardwareInspection.Foundation.Tests.csproj `
  -p:Platform=x64 --filter 'FullyQualifiedName!~ExternalProcessRunnerTests' --no-ansi
```

Require zero failed/skipped. This temporary filter is removed by Task 4, which moves equivalent real-process coverage into the signed AppX boundary; it is not acceptable in final Gate 5 evidence. Then:

```powershell
git add infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/LlamaCpp `
  tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/LlamaCpp
git commit -m "feat(hardware-inspection): add llama.cpp capability evidence"
```

---

### Task 3: Add the exact command contract and provider policy

**Files:**
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/LlamaCpp/ILlamaCppCapabilityEvidenceProvider.cs`
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/LlamaCpp/LlamaCppCapabilityCommandContract.cs`
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/LlamaCpp/LlamaCppCapabilityEvidenceProvider.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/LlamaCpp/LlamaCppCapabilityCommandContractTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/LlamaCpp/LlamaCppCapabilityEvidenceProviderTests.cs`

**Interfaces:**
- Produces `Task<LlamaCppCapabilityEvidence> ILlamaCppCapabilityEvidenceProvider.CaptureAsync(VerifiedTrustedTool tool, CancellationToken cancellationToken)`.
- Produces command constants and `CreateIdentityCommand()` / `CreateCapabilitiesCommand()` returning `TrustedToolCommand`.
- Consumes Task 2 parsers/evidence, `IExternalProcessRunner`, `VerifiedTrustedTool`, and `TimeProvider`.

- [ ] **Step 1: Write RED command-contract tests**

Require:

```csharp
Assert.AreEqual("granite-edge-hardware-llamacpp-probe", LlamaCppCapabilityCommandContract.ToolId);
CollectionAssert.AreEqual(
    new[] { "identity", "--format", "json-v1" },
    LlamaCppCapabilityCommandContract.CreateIdentityCommand().Arguments.ToArray());
CollectionAssert.AreEqual(
    new[] { "capabilities", "--format", "json-v1" },
    LlamaCppCapabilityCommandContract.CreateCapabilitiesCommand().Arguments.ToArray());
```

The executable name is exactly `GraniteEdgeAI.HardwareInspection.LlamaCppProbe.exe`; version is `0.27.0-cpu-win-x64`; the manifest must have exactly these two commands.
The contract also exposes `NativeUnavailableExitCode = 70`, `UsageExitCode = 64`, and `OutputFailureExitCode = 74` so the provider and probe cannot drift.

- [ ] **Step 2: Write RED provider mapping tests**

Use a recording fake `IExternalProcessRunner` that returns complete real `ExternalProcessResult` values. Assert observable evidence and sequencing rather than asserting the fake exists. Cover:

- exact success: identity then capabilities, exact limits/timeouts, UTC conversion;
- pre-cancellation: no runner access and caller token preserved;
- wrong tool ID/version and command inventory: no process starts;
- each identity runner reason, nonzero exit, invalid output, and identity mismatch: one call only;
- each capabilities runner reason, nonzero exit, invalid output: exactly two calls;
- caller cancellation after either awaited call propagates;
- unexpected runner exception is not swallowed;
- no retries and caller-owned `VerifiedTrustedTool` remains usable.

- [ ] **Step 3: Run RED**

Expected: command/provider types are missing.

- [ ] **Step 4: Implement the linear provider**

Construct only these requests:

```csharp
new ExternalProcessRequest("identity", TimeSpan.FromSeconds(5), 4 * 1024, 4 * 1024);
new ExternalProcessRequest("capabilities", TimeSpan.FromSeconds(10), 64 * 1024, 64 * 1024);
```

Map `StartFailed`, `TimedOut`, `OutputLimitExceeded`, `CleanupFailed`, unexpected `Cancelled`, nonzero `Exited`, and parser failures to closed phase-specific diagnostics. Map capability exit `70` to `NativeCapabilityUnavailable` and other nonzero capability exits to `CapabilityProcessFailed`. Check the caller token immediately before and after each await. Validate exact identity before the capability call. Do not dispose the tool or catch arbitrary exceptions.

- [ ] **Step 5: Run GREEN and native-free Foundation regression**

Require the new classes and the same explicit `FullyQualifiedName!~ExternalProcessRunnerTests` Foundation run to pass with zero skipped. Task 4 must restore an unfiltered ordinary Foundation run plus signed packaged process acceptance before any completion claim.

- [ ] **Step 6: Commit**

```powershell
git add infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/LlamaCpp `
  tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/LlamaCpp
git commit -m "feat(hardware-inspection): map llama.cpp capability provider"
```

---

### Task 4: Move real process-runner acceptance into the signed AppX test boundary

**Files:**
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Processes/BoundedProcessOutputTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Processes/ExternalProcessRunnerPackagedTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Support/VerifiedPackagedToolFixture.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/HardwareInspection.ProcessFixturePackaging.targets`
- Modify: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Processes/ExternalProcessRunnerTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/GraniteEdgeAI.HardwareInspection.Foundation.Tests.csproj`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj`
- Modify: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Properties/AssemblyInfo.cs`

**Interfaces:**
- Retains native-free byte-stream tests in the Foundation suite.
- Produces signed-package acceptance for the real `ExternalProcessRunner` using the existing LLM Fit fake tool under `HardwareInspection\TestTools\LlmFitFake`.
- Grants internals only to the established `GraniteEdgeAI.UnitTests` test assembly.

- [ ] **Step 1: Write the RED package/inventory contract**

Add a packaged test that resolves `AppContext.BaseDirectory`, requires the fixture directory to remain beneath it, inventories only top-level files, verifies the executable is AMD64, and constructs a `TrustedToolPackageManifest` from the exact SHA-256 and inventory. Require failure if the package directory is absent or contains a subdirectory.

```csharp
Assert.IsTrue(packageRoot.StartsWith(
    Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory) + Path.DirectorySeparatorChar,
    StringComparison.OrdinalIgnoreCase));
Assert.IsFalse(Directory.EnumerateDirectories(packageRoot).Any());
```

- [ ] **Step 2: Run packaged RED**

Build and install the x64 Debug AppX test package, then run only `ExternalProcessRunnerPackagedTests`. Expected: `HardwareInspection\TestTools\LlmFitFake` is missing.

- [ ] **Step 3: Add test-only fixture packaging**

Publish `GraniteEdgeAI.HardwareInspection.LlmFitFakeTool` as Release/framework-dependent/win-x64 with `UseAppHost=true`, no symbols, no trimming, and no ReadyToRun. Include every flat published member as AppX `Content` under `HardwareInspection\TestTools\LlmFitFake`. Import this target only from `GraniteEdgeAI.UnitTests.csproj`; no application project edit is allowed in this step.

- [ ] **Step 4: Split native-free and real-process tests**

Move `MemoryStream`/bounded-decoding cases into `BoundedProcessOutputTests`. Move every test that starts the fixture executable, checks Job membership, timeout, cancellation, overflow, crash, descendant cleanup, or custody into `ExternalProcessRunnerPackagedTests`. Remove the process-fixture project reference from the Foundation test project and add the narrowly scoped `InternalsVisibleTo("GraniteEdgeAI.UnitTests")` needed by packaged acceptance.

- [ ] **Step 5: Run GREEN at both boundaries**

Require the ordinary Foundation suite to pass without starting an unsigned apphost. Require the installed AppX process class to pass three consecutive times with zero skipped and no surviving descendants.

- [ ] **Step 6: Commit**

```powershell
git add infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Properties/AssemblyInfo.cs `
  tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests `
  tests/UnitTests/GraniteEdgeAI.UnitTests
git commit -m "test(hardware-inspection): run process acceptance from signed AppX"
```

---

### Task 5: Add and unit-test the isolated llama.cpp probe

**Files:**
- Create: `workers/GraniteEdgeAI.HardwareInspection.LlamaCppProbe/GraniteEdgeAI.HardwareInspection.LlamaCppProbe.csproj`
- Create: `workers/GraniteEdgeAI.HardwareInspection.LlamaCppProbe/Program.cs`
- Create: `workers/GraniteEdgeAI.HardwareInspection.LlamaCppProbe/LlamaCppProbeApplication.cs`
- Create: `workers/GraniteEdgeAI.HardwareInspection.LlamaCppProbe/LlamaCppProbeProtocol.cs`
- Create: `workers/GraniteEdgeAI.HardwareInspection.LlamaCppProbe/ILlamaCppNativeCapabilityApi.cs`
- Create: `workers/GraniteEdgeAI.HardwareInspection.LlamaCppProbe/ILlamaCppNativeInterop.cs`
- Create: `workers/GraniteEdgeAI.HardwareInspection.LlamaCppProbe/LLamaSharpNativeCapabilityApi.cs`
- Create: `workers/GraniteEdgeAI.HardwareInspection.LlamaCppProbe/Properties/AssemblyInfo.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.LlamaCppProbe.Tests/GraniteEdgeAI.HardwareInspection.LlamaCppProbe.Tests.csproj`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.LlamaCppProbe.Tests/LlamaCppProbeApplicationTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.LlamaCppProbe.Tests/LLamaSharpNativeCapabilityApiTests.cs`

**Interfaces:**
- Produces `Task<int> LlamaCppProbeApplication.RunAsync(string[] args, Stream stdout, ILlamaCppNativeCapabilityApi nativeApi)`.
- Produces `LlamaCppNativeCapabilityResult Capture()` with only closed success/failure and copied `(ordinal, bufferType)` facts.
- Implements the native seam through matched LLamaSharp/llama.cpp calls; no Foundation reference points back to the worker.

- [ ] **Step 1: Write RED application/protocol tests**

With a fake native API, require exact identity output without native access, exact capability output with one LF, strict argument matching, exit `64` for usage, exit `70` for closed native failure, exit `74` for output failure, empty stdout on failure, and no exception/path/environment/host/model text.

```csharp
int exitCode = await application.RunAsync(
    ["identity", "--format", "json-v1"],
    output,
    nativeApi);
Assert.AreEqual(0, exitCode);
Assert.AreEqual(0, nativeApi.CaptureCalls);
```

- [ ] **Step 2: Write RED native lifetime/bounds tests**

With fake `ILlamaCppNativeInterop`, require initialization before enumeration; 1..16 valid devices; rejection of zero/17 devices, null handles, null/unsafe/oversized labels; and exactly one `FreeBackend()` in `finally` after every successful initialization. Initialization failure must not free. Enumerate parent modules before/after and reject any loaded filename beginning `llama` or `ggml`.

- [ ] **Step 3: Run RED**

Expected: the worker and test project types are missing.

- [ ] **Step 4: Implement strict executable and native seams**

Use `OutputType=Exe`, .NET 8 Windows, `win-x64`, AMD64, nullable/analyzers/warnings-as-errors, and exact `LLamaSharp`/`LLamaSharp.Backend.Cpu` `0.27.0`. Write protocol bytes with `Utf8JsonWriter`, append `0x0a`, and perform one bounded write. Configure CPU-only native loading, copy runtime-owned labels immediately, validate them before retention, and always free initialized backend state in `finally`. Catch only the executable/native availability boundary categories and emit no diagnostic text.

- [ ] **Step 5: Run GREEN and dependency audits**

Require all probe tests to pass with zero skipped, then require zero hits for LLamaSharp/native references in Foundation and zero Model Inspection references in the probe.

- [ ] **Step 6: Commit**

```powershell
git add workers/GraniteEdgeAI.HardwareInspection.LlamaCppProbe `
  tests/UnitTests/GraniteEdgeAI.HardwareInspection.LlamaCppProbe.Tests
git commit -m "feat(hardware-inspection): add isolated llama.cpp capability probe"
```

---

### Task 6: Package the inactive production probe with exact identity

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/HardwareInspection.LlamaCppProbePackaging.targets`
- Create: `scripts/hardware-inspection/New-LlamaCppProbeManifest.ps1`
- Create: `scripts/hardware-inspection/Test-LlamaCppProbeManifest.ps1`
- Modify: `IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/LlamaCpp/LlamaCppProbePackageContractTests.cs`

**Interfaces:**
- Publishes the flat worker directory to `HardwareInspection\LlamaCppProbe` in both x64 application and x64 AppX test packages.
- Produces `HardwareInspection\llamacpp-probe-manifest.json` with schema 1, exact tool/version/executable/hash/member inventory/machine/disposition, and exactly the two fixed commands.
- Does not add a product locator, verifier call, provider registration, or launch path.

- [ ] **Step 1: Write RED package-source and AppX inventory tests**

Require the target import in both csproj files, fixed paths, Release/win-x64 publishing flags, manifest generation followed by verification, exact flat package membership, AMD64 executable, lowercase SHA-256, and absence of the probe below any other AppX directory. Require source inspection to find `UnavailableHardwareInspectionService.Instance` and no `LlamaCppCapabilityEvidenceProvider` construction in application code.

- [ ] **Step 2: Run RED**

Expected: packaging target/scripts and AppX members are absent.

- [ ] **Step 3: Implement deterministic publish and manifest scripts**

Mirror `ModelInspection.WorkerPackaging.targets` with unique target/property names. Clean dedicated intermediate roots; restore/publish Release/win-x64/framework-dependent with apphost, no trim/ReadyToRun/symbols; reject directories and more than 64 files; sort member names ordinally; hash the executable with SHA-256; write UTF-8 without BOM and one LF; immediately verify every field, hash, member, command, and AMD64 PE before adding `Content` items.

- [ ] **Step 4: Import packaging without activation**

Import the target for x64 in both the application and AppX test csproj. The production project gets only the real probe and manifest. The test project gets the same real probe through the same target plus test-only fixtures through Task 4/Task 7 targets.

- [ ] **Step 5: Build and run GREEN**

Build application Debug/x64 and Release/win-x64, build/install the Debug/x64 AppX tests, and run `LlamaCppProbePackageContractTests`. Require zero errors, exact single-location inventory, valid manifest, and inactive composition.

- [ ] **Step 6: Commit**

```powershell
git add 'IBM Granite with TurboQuant (Intel)' `
  scripts/hardware-inspection `
  tests/UnitTests/GraniteEdgeAI.UnitTests
git commit -m "build(hardware-inspection): package inactive llama.cpp probe"
```

---

### Task 7: Prove provider and native behavior from the signed AppX package

**Files:**
- Create: `tests/ProcessFixtures/GraniteEdgeAI.HardwareInspection.LlamaCppProbeFakeTool/GraniteEdgeAI.HardwareInspection.LlamaCppProbeFakeTool.csproj`
- Create: `tests/ProcessFixtures/GraniteEdgeAI.HardwareInspection.LlamaCppProbeFakeTool/Program.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/HardwareInspection.ProcessFixturePackaging.targets`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/LlamaCpp/LlamaCppCapabilityEvidenceProviderPackagedTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/LlamaCpp/LlamaCppCapabilityRealProbePackagedTests.cs`

**Interfaces:**
- Packages the native-free adverse fixture only under `HardwareInspection\TestTools\LlamaCppProbeFake` in the AppX test package.
- Consumes real `TrustedToolPackageVerifier`, `ExternalProcessRunner`, and `LlamaCppCapabilityEvidenceProvider` for both fake and real packaged children.

- [ ] **Step 1: Write RED adverse acceptance tests**

Create exact trusted manifests from catalog-covered package members and exercise success, identity mismatch, malformed JSON, nonzero exit, stdout/stderr overflow, timeout, cancellation, Job membership, child spawn/hang, and normal-parent-exit-with-child. Assert only evidence/closed diagnostics and descendant cleanup; never log process output.

```csharp
LlamaCppCapabilityEvidence evidence = await provider.CaptureAsync(tool, CancellationToken.None);
Assert.AreEqual(LlamaCppCapabilityEvidenceState.Available, evidence.State);
Assert.AreEqual(LlamaCppBackend.Cpu, evidence.Backends.Single());
```

- [ ] **Step 2: Implement and package the minimal adverse fixture**

Match only the two production commands and controlled test modes. Write explicit UTF-8/LF, use only synthetic `Fixture CPU Buffer`, include no LLamaSharp package and no network mode, and publish it with the Task 4 signed-test-only settings.

- [ ] **Step 3: Write the RED real-probe smoke**

Resolve only `HardwareInspection\LlamaCppProbe` under the installed package. Parse/construct its exact trusted manifest, capture through the real provider, and assert exact pinned identity, CPU backend, 1..16 contiguous safe devices, UTC capture, no supplied model, zero parent llama/ggml modules before/after, and zero persisted labels/output.

- [ ] **Step 4: Run signed packaged GREEN three times**

Build/install the AppX test package and run both packaged classes three consecutive times. Require total=executed=passed, zero failed/skipped/not-executed, and no surviving descendant/resource symptom. Do not attempt a loose-output fallback if policy blocks execution.

- [ ] **Step 5: Run full regressions and commit**

Require the full ordinary Foundation suite, probe unit suite, and authoritative packaged Hardware Inspection filter to pass. Then:

```powershell
git add tests/ProcessFixtures/GraniteEdgeAI.HardwareInspection.LlamaCppProbeFakeTool `
  tests/UnitTests/GraniteEdgeAI.UnitTests
git commit -m "test(hardware-inspection): verify llama.cpp capabilities in signed AppX"
```

---

### Task 8: Gate 5 independent review, regression, audits, and evidence closure

**Files:**
- Modify: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/README.md`
- Modify: `docs/reviews/2026-08-22-hardware-inspection-integration-preservation-matrix.md`
- Create: `docs/testing/evidence/2026-08-23-hardware-inspection-gate-5-llamacpp-capabilities.md`

**Interfaces:**
- Documents exact Gate 5 behavior and evidence; changes no production composition.

- [ ] **Step 1: Run fresh physical-path authoritative evidence**

At the exact final code head:

1. build the probe and application Release/win-x64 with zero errors;
2. run all Foundation and probe unit tests with fresh TRX files and exact discovered counts;
3. install the signed x64 AppX test package and run fake-process and real-probe classes three consecutive times;
4. parse every TRX and require total=executed=passed with zero failed/error/timeout/aborted/inconclusive/not-executed.

- [ ] **Step 2: Run repository regressions**

Require:

- deterministic Gate-1 category at minimum 174 with fresh TRX;
- authoritative packaged Hardware Inspection/handoff/onboarding filter at minimum 114 with fresh TRX;
- Stage A 12/12 and Stage 0/acquisition/public-contract/theme 18/18;
- packaged Debug/x64 test build and app Debug/x64 plus Release/win-x64 builds with zero errors.

Record but do not misattribute pre-existing warnings.

- [ ] **Step 3: Audit dependency and security boundaries**

Run and inspect every hit:

```powershell
rg -n 'LLamaSharp|LLama\.Native|ggml_backend|llama_backend' infrastructure/GraniteEdgeAI.HardwareInspection.Foundation
rg -n 'Process\.Start|UseShellExecute|cmd\.exe|powershell|TcpListener|HttpListener' `
  infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/LlamaCpp `
  workers/GraniteEdgeAI.HardwareInspection.LlamaCppProbe
rg -n 'ModelInspection|GGUF|OpenVINO|HardwareSnapshot' `
  infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/LlamaCpp `
  workers/GraniteEdgeAI.HardwareInspection.LlamaCppProbe
rg -n 'UnavailableHardwareInspectionService' `
  'IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs'
git diff --check
rg -n '^(<<<<<<< |=======$|>>>>>>> )' --glob '!docs/superpowers/plans/*'
```

Additionally inspect the exact Gate 5 commit range and Debug/Release AppX inventories recursively. Require the real probe/native runtime exactly once under `HardwareInspection\LlamaCppProbe`; require adverse fixtures only in the test AppX; fail for TRX, raw output, host device labels, private paths, candidate/trusted/offline evidence, usernames, URLs, model data, production registration, or unexpected package/reference changes. Prove the parent test/AppX processes load no llama/ggml module.

- [ ] **Step 4: Request independent code review**

Use `superpowers:requesting-code-review` against the exact Gate 5 range. Review strict UTF-8, JSON framing/schema, evidence invariants, command inventory, phase mapping, cancellation, custody, fake isolation, native load placement/lifetime, device bounds, signed package identity/inventory, production inactivity, privacy, and nonclaims. Resolve every Critical/Important/Minor finding test-first and repeat affected evidence.

- [ ] **Step 5: Update evidence and boundary documentation**

Record exact commits, commands, counts, warnings, runtime identity, fake/real repetitions, review result, module/package audits, production inactivity, and nonclaims. Do not record device labels, native logs, paths outside repository-relative test-result paths, or raw output.

- [ ] **Step 6: Use verification-before-completion and commit**

Require a clean tracked worktree after:

```powershell
git add infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/README.md `
  docs/reviews/2026-08-22-hardware-inspection-integration-preservation-matrix.md `
  docs/testing/evidence/2026-08-23-hardware-inspection-gate-5-llamacpp-capabilities.md
git commit -m "docs(hardware-inspection): record Gate 5 verification"
```

Gate 5 is complete only after this evidence commit. The full feature remains incomplete: Gate 6 evidence authority, normalization, consistency, freshness, resolution, provenance, and canonical snapshot is next.
