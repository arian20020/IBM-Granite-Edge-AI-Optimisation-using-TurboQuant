# Hardware Inspection Gate 5 llama.cpp Capabilities Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a strict, trusted, child-process-only llama.cpp capability provider that records the pinned application runtime identity, CPU backend support, and runtime-visible devices without loading native code into WinUI or any test host.

**Architecture:** Extend the existing Hardware Inspection trusted-tool and Job-contained process foundation with strict UTF-8 output handling. A dedicated `win-x64` probe executable owns LLamaSharp/native loading and emits a closed JSON-v1 protocol; the Foundation project validates that protocol into immutable noncanonical evidence and remains independent of LLamaSharp types.

**Tech Stack:** C# 12, .NET 8 Windows x64, MSTest 4, Microsoft Testing Platform, `System.Text.Json`, LLamaSharp `0.27.0`, LLamaSharp.Backend.Cpu `0.27.0`, existing Win32 Job/custody boundary, Git.

**Spec:** `docs/superpowers/specs/2026-08-23-hardware-inspection-gate-5-llamacpp-capabilities-design.md`

## Global Constraints

- Use `LLamaSharp` `0.27.0` and `LLamaSharp.Backend.Cpu` `0.27.0`; mapped llama.cpp commit is exactly `3f7c29d318e317b63f54c558bc69803963d7d88c`.
- The helper is `win-x64`/AMD64 and CPU-only. A different package, backend, RID, or commit requires a new decision.
- Native llama/ggml libraries load only in `GraniteEdgeAI.HardwareInspection.LlamaCppProbe.exe`, never WinUI, Foundation, MSTest/VSTest, or the packaged test host.
- Reuse only `TrustedToolPackageVerifier`, live `VerifiedTrustedTool` custody, and `IExternalProcessRunner`; do not call the Model Inspection worker or copy a weaker process launcher.
- The manifest contains exactly `identity --format json-v1` and `capabilities --format json-v1`; no caller arguments, shell, PATH search, network, server, retry, or fallback.
- Identity uses 5 seconds and independent 4 KiB output limits. Capabilities uses 10 seconds and independent 64 KiB limits.
- Protocol is strict UTF-8, one JSON object, one LF, depth at most 8, no BOM/CR/CRLF/comments/trailing commas/duplicate/unknown/case-drifted fields.
- Retain at most 8 unique backends and 16 devices. Gate 5 accepts exactly backend `cpu`, contiguous ordinals from zero, and 1..16 safe buffer-type labels of at most 128 Unicode scalars.
- Cancellation propagates with the caller token. Missing/integrity/runtime failures are typed unavailability, never absent hardware.
- Retain no paths, hashes, stdout/stderr, exit codes, exceptions, native logs/pointers, host/account identity, model data, or unrestricted device/hardware names in evidence or committed artifacts.
- Do not register the provider, package the probe into AppX, create `HardwareSnapshot`, normalize/resolve sources, infer compatibility, open a model, download/execute a candidate, or perform any operational Stage action.
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

Run both new classes and full foundation tests. Then:

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

- [ ] **Step 5: Run GREEN and full foundation regression**

Require the new classes and all foundation tests to pass with zero skipped.

- [ ] **Step 6: Commit**

```powershell
git add infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/LlamaCpp `
  tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/LlamaCpp
git commit -m "feat(hardware-inspection): map llama.cpp capability provider"
```

---

### Task 4: Prove the provider through the real contained process boundary

**Files:**
- Create: `tests/ProcessFixtures/GraniteEdgeAI.HardwareInspection.LlamaCppProbeFakeTool/GraniteEdgeAI.HardwareInspection.LlamaCppProbeFakeTool.csproj`
- Create: `tests/ProcessFixtures/GraniteEdgeAI.HardwareInspection.LlamaCppProbeFakeTool/Program.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/GraniteEdgeAI.HardwareInspection.Foundation.Tests.csproj`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/LlamaCpp/LlamaCppCapabilityEvidenceProviderProcessTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Support/VerifiedLlamaCppProbeFixture.cs`

**Interfaces:**
- Produces a test-only AMD64 executable with the exact production command surface and controlled `fake-mode.txt` scenarios.
- Consumes real `TrustedToolPackageVerifier`, `ExternalProcessRunner`, and `LlamaCppCapabilityEvidenceProvider`.

- [ ] **Step 1: Write RED end-to-end process tests**

Build a verified flat fixture package using a test-only manifest generated from its exact files/hash. Exercise success, identity mismatch, invalid JSON, nonzero, output overflow, timeout, cancellation, Job membership, child spawn/hang, and normal-parent-exit-with-child. Assert evidence/diagnostics and descendant cleanup; never persist stdout/stderr.

```csharp
LlamaCppCapabilityEvidence evidence = await provider.CaptureAsync(tool, CancellationToken.None);

Assert.AreEqual(LlamaCppCapabilityEvidenceState.Available, evidence.State);
Assert.AreEqual(LlamaCppBackend.Cpu, evidence.Backends.Single());
Assert.AreEqual(0, evidence.VisibleDevices.Single().Ordinal);
```

The fixture may emit only synthetic `Fixture CPU Buffer` text. It must contain no LLamaSharp package reference and no network mode.

- [ ] **Step 2: Run RED**

Expected: fake project/support types are missing.

- [ ] **Step 3: Implement the minimal fake executable**

Use exact argument matching and explicit LF writes through `Console.OpenStandardOutput()`. Implement modes as small functions. For hang/descendant modes reuse the established LLM Fit fake-tool pattern, but keep this fixture's command surface and files independent. Never place adverse behavior in production code.

- [ ] **Step 4: Run process GREEN three times**

Run `LlamaCppCapabilityEvidenceProviderProcessTests` three consecutive times from a physical short worktree. Require all passes, zero skipped, and no descendant/resource symptom.

- [ ] **Step 5: Commit**

```powershell
git add tests/ProcessFixtures/GraniteEdgeAI.HardwareInspection.LlamaCppProbeFakeTool `
  tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests
git commit -m "test(hardware-inspection): prove llama.cpp provider containment"
```

---

### Task 5: Add the dedicated probe application with an injectable native seam

**Files:**
- Create: `workers/GraniteEdgeAI.HardwareInspection.LlamaCppProbe/GraniteEdgeAI.HardwareInspection.LlamaCppProbe.csproj`
- Create: `workers/GraniteEdgeAI.HardwareInspection.LlamaCppProbe/Program.cs`
- Create: `workers/GraniteEdgeAI.HardwareInspection.LlamaCppProbe/LlamaCppProbeApplication.cs`
- Create: `workers/GraniteEdgeAI.HardwareInspection.LlamaCppProbe/LlamaCppProbeProtocol.cs`
- Create: `workers/GraniteEdgeAI.HardwareInspection.LlamaCppProbe/ILlamaCppNativeCapabilityApi.cs`
- Create: `workers/GraniteEdgeAI.HardwareInspection.LlamaCppProbe/Properties/AssemblyInfo.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/GraniteEdgeAI.HardwareInspection.Foundation.Tests.csproj`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/LlamaCppProbe/LlamaCppProbeApplicationTests.cs`

**Interfaces:**
- Produces `Task<int> LlamaCppProbeApplication.RunAsync(string[] args, Stream stdout, ILlamaCppNativeCapabilityApi nativeApi)`.
- Produces `LlamaCppNativeCapabilityResult Capture()` containing closed success/failure plus copied `(ordinal, bufferType)` device facts.
- The project references exact `LLamaSharp` and `LLamaSharp.Backend.Cpu` `0.27.0`; Foundation does not reference the worker.

- [ ] **Step 1: Write RED application/protocol tests**

Reference the worker only from the foundation test project. Use a fake `ILlamaCppNativeCapabilityApi`; do not call native code. Require:

- exact identity command/output and exit 0 without native API access;
- exact capabilities command writes the approved JSON-v1 with one LF;
- unknown/missing/case-drifted/additive arguments exit 64 with empty stdout;
- native unavailable/invalid results exit 70 with empty stdout;
- native API exception exits 70 with empty stdout and no exception text;
- output stream failure exits 74;
- no CR, BOM, path, environment, host, or model values enter output.

Before and after every class, enumerate parent modules and require no filename beginning with `llama` or `ggml`.

- [ ] **Step 2: Run RED**

Expected: worker project/types are missing.

- [ ] **Step 3: Implement the project and strict writer**

Project properties include `OutputType=Exe`, `TargetFramework=net8.0-windows10.0.19041.0`, `RuntimeIdentifier=win-x64`, `PlatformTarget=x64`, nullable/analyzers/warnings-as-errors, and exact package references.

`Program.cs` creates the production native API and delegates once. `LlamaCppProbeProtocol` writes precomputed identity fields and validated capability data with `Utf8JsonWriter` to a buffer, appends byte `0x0a`, then performs one bounded stream write. The application catches exceptions only at the executable trust boundary, emits no error text, and returns the closed software exit code.

- [ ] **Step 4: Run GREEN and dependency audits**

Require application tests to pass and prove:

```powershell
rg -n 'LLamaSharp|LLama\.Native' infrastructure/GraniteEdgeAI.HardwareInspection.Foundation
rg -n 'ModelInspection' workers/GraniteEdgeAI.HardwareInspection.LlamaCppProbe
```

Expected: zero production hits in both scans.

- [ ] **Step 5: Commit**

```powershell
git add workers/GraniteEdgeAI.HardwareInspection.LlamaCppProbe `
  tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests
git commit -m "feat(hardware-inspection): add isolated llama.cpp probe"
```

---

### Task 6: Implement native CPU capability enumeration and real child smoke

**Files:**
- Create: `workers/GraniteEdgeAI.HardwareInspection.LlamaCppProbe/LLamaSharpNativeCapabilityApi.cs`
- Create: `workers/GraniteEdgeAI.HardwareInspection.LlamaCppProbe/ILlamaCppNativeInterop.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/LlamaCppProbe/LLamaSharpNativeCapabilityApiContractTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/LlamaCpp/LlamaCppCapabilityIntegrationTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Support/VerifiedLlamaCppProbeFixture.cs`

**Interfaces:**
- Implements `ILlamaCppNativeCapabilityApi` through LLamaSharp native configuration, `llama_backend_init`, `ggml_backend_dev_count`, `ggml_backend_dev_get`, `ggml_backend_dev_buffer_type`, `ggml_backend_buft_name`, and `llama_backend_free`.
- Produces real provider evidence only through the child executable and existing trusted process boundary.

- [ ] **Step 1: Write RED native lifetime/bounds tests through a lower interop seam**

Keep static native calls behind internal `ILlamaCppNativeInterop`. With a fake interop require:

- exact CPU-only configuration and initialization before enumeration;
- 1..16 valid devices map ordinal/label exactly;
- zero devices, 17 devices, null device/buffer handles, null/unsafe/oversized names fail closed;
- `FreeBackend()` is called once after every successful initialization, including enumeration failure;
- initialization failure does not call free;
- no native pointer or exception text enters result.

The mutation checks are removing `finally`, allowing a 17th device, accepting a null handle, or using an inferred label.

- [ ] **Step 2: Run native RED**

Expected: production native adapter is missing.

- [ ] **Step 3: Implement the minimal native adapter**

Configure LLamaSharp for CPU with CUDA/Vulkan disabled before any native call. Load the published CPU library, call backend init, enumerate incrementally, convert runtime-owned strings immediately to managed text, and free backend state in `finally`. Catch only documented native availability exceptions at this executable seam and return a closed failed result; do not expose messages.

The executable returns code `70` only for the closed native-unavailable result. Task 3 maps that exact capability exit code to `NativeCapabilityUnavailable`; every other nonzero capability exit maps to `CapabilityProcessFailed`.

- [ ] **Step 4: Write the real child-process integration test**

Publish/build the probe to a controlled physical short path. Create an exact test-only trusted manifest from every flat output member and verified executable SHA-256. Capture through the real `TrustedToolPackageVerifier`, `ExternalProcessRunner`, and provider. Assert only:

- state is available;
- exact pinned identity and CPU backend;
- 1..16 contiguous devices with safe labels;
- UTC capture;
- parent process has no loaded llama/ggml modules before or after;
- no model is supplied/opened;
- no host-specific label is printed or written to TRX attachments.

- [ ] **Step 5: Run the real integration class three times**

From a physical short worktree, require all three repetitions to pass with zero skipped. Inspect Task Manager/process/module evidence only transiently if diagnosing; do not persist host facts.

- [ ] **Step 6: Run full foundation GREEN and commit**

```powershell
git add workers/GraniteEdgeAI.HardwareInspection.LlamaCppProbe `
  tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests
git commit -m "feat(hardware-inspection): enumerate pinned llama.cpp capabilities"
```

---

### Task 7: Gate 5 independent review, regression, audits, and evidence closure

**Files:**
- Modify: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/README.md`
- Modify: `docs/reviews/2026-08-22-hardware-inspection-integration-preservation-matrix.md`
- Create: `docs/testing/evidence/2026-08-23-hardware-inspection-gate-5-llamacpp-capabilities.md`

**Interfaces:**
- Documents exact Gate 5 behavior and evidence; changes no production composition.

- [ ] **Step 1: Run fresh physical-path authoritative evidence**

At the exact final code head:

1. build the probe Release/win-x64 with zero errors;
2. run all foundation tests with a fresh TRX and exact discovered count;
3. run fake-process and real-probe integration classes three consecutive times;
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

Additionally inspect the exact Gate 5 commit range and Debug AppX recursively. Fail for a newly tracked/packaged probe executable/native runtime, TRX, raw output, host device label, private path, candidate/trusted/offline evidence, username, URL, model data, production registration, or unexpected package/reference change. Prove the parent test/AppX processes load no llama/ggml module.

- [ ] **Step 4: Request independent code review**

Use `superpowers:requesting-code-review` against the exact Gate 5 range. Review strict UTF-8, JSON framing/schema, evidence invariants, command inventory, phase mapping, cancellation, custody, fake isolation, native load placement/lifetime, device bounds, package identity, privacy, AppX absence, and nonclaims. Resolve every Critical/Important/Minor finding test-first and repeat affected evidence.

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
