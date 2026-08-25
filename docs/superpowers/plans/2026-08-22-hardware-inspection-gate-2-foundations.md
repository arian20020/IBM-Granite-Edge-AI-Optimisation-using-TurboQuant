# Hardware Inspection Gate 2 Foundations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build tested Windows system-snapshot, trusted-package verification, and bounded external-process foundations without activating production Hardware Inspection.

**Architecture:** Add a candidate-neutral `GraniteEdgeAI.HardwareInspection.Foundation` infrastructure library and standalone MSTest project. The app references the library only through a Windows available-memory adapter; production composition remains `UnavailableHardwareInspectionService` until later provider/coordinator gates.

**Tech Stack:** C# 12, .NET 8, Windows x64 P/Invoke, Microsoft Testing Platform, MSTest 4.3.2, existing harmless LLM Fit fake-process fixture.

**Spec:** `docs/superpowers/specs/2026-08-22-hardware-inspection-gate-2-foundations-design.md`

## Global Constraints

- LLM Fit v1.1.9 remains `FunctionalPassWithPackagingConcern`; do not embed or redistribute it.
- No model data, GGUF/OpenVINO type, compatibility calculation, candidate path, raw capture, or Gate TRX enters production code.
- No shell execution, arbitrary command strings, listening service, network dependency, or retry.
- `UnavailableHardwareInspectionService` remains the sole application composition during Gate 2.
- Every behavior change follows RED, GREEN, REFACTOR with the focused test executed at each boundary.
- Use the existing isolated worktree; do not create a nested worktree.

---

### Task 1: Create the foundation project and closed contracts

**Files:**
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/GraniteEdgeAI.HardwareInspection.Foundation.csproj`
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Properties/AssemblyInfo.cs`
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/TrustedTools/TrustedToolContracts.cs`
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Processes/ExternalProcessContracts.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/GraniteEdgeAI.HardwareInspection.Foundation.Tests.csproj`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/TrustedTools/TrustedToolContractTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel).slnx`

**Interfaces:**
- Produces: `TrustedToolCommand`, `TrustedToolPackageManifest`, `TrustedToolPackageDisposition`, `TrustedToolVerificationFailure`, `VerifiedTrustedTool`, `ExternalProcessRequest`, `ExternalProcessResult`, and `ExternalProcessTerminationReason`.

- [ ] **Step 1: Write failing contract tests**

Test that blank IDs/versions, rooted or traversal member names, invalid lowercase SHA-256, duplicate case-insensitive inventory, empty commands, duplicate command identities, non-positive caps/timeouts, and mutable input collections are rejected. Assert literal accepted values and copied collections.

```csharp
TrustedToolPackageManifest manifest = new(
    toolId: "llmfit",
    version: "1.1.9",
    executableRelativePath: "llmfit.exe",
    executableSha256: new string('a', 64),
    requiredMembers: ["llmfit.exe", "LICENSE", "README.md"],
    requiredMachine: PeMachine.Amd64,
    disposition: TrustedToolPackageDisposition.FunctionalPassWithPackagingConcern,
    commands: [new TrustedToolCommand("version", ["--version"])]);
```

- [ ] **Step 2: Run RED**

Run the new test project with `dotnet test --project ... --no-ansi`. Expected: compile failure because the contracts do not exist.

- [ ] **Step 3: Implement minimal immutable contracts**

Use sealed records/classes, copy every input collection, validate with ordinal comparisons, and expose no setters. The closed enums are:

```csharp
public enum PeMachine { Amd64 }
public enum TrustedToolPackageDisposition { FunctionalPassWithPackagingConcern, AcceptedForFunctionalEvaluation }
public enum ExternalProcessTerminationReason { Exited, TimedOut, Cancelled, OutputLimitExceeded, StartFailed, CleanupFailed }
```

- [ ] **Step 4: Run GREEN and commit**

Run the focused project, then commit as `feat(hardware-inspection): add Gate 2 foundation contracts`.

### Task 2: Capture a truthful Windows memory/OS snapshot

**Files:**
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Windows/WindowsSystemSnapshot.cs`
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Windows/WindowsSystemSnapshotProvider.cs`
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Windows/Kernel32WindowsMemoryApi.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Windows/WindowsSystemSnapshotProviderTests.cs`

**Interfaces:**
- Produces: `ValueTask<WindowsSystemSnapshot> CaptureAsync(CancellationToken)`.
- Internal seam: `IWindowsMemoryApi.TryGetPhysicallyInstalledKilobytes(out ulong)` and `TryGetMemoryStatus(out ulong totalBytes, out ulong availableBytes)`.

- [ ] **Step 1: Write RED tests**

Cover valid conversion, zero available memory, UTC timestamps, caller cancellation before native calls, native-call failure codes, installed-KiB multiplication overflow, and `installed >= usable >= available` enforcement.

```csharp
WindowsSystemSnapshot snapshot = await provider.CaptureAsync(CancellationToken.None);
Assert.AreEqual(16UL * 1024 * 1024 * 1024, snapshot.PhysicallyInstalledBytes);
Assert.AreEqual(15UL * 1024 * 1024 * 1024, snapshot.OsUsablePhysicalBytes);
Assert.AreEqual(8UL * 1024 * 1024 * 1024, snapshot.AvailablePhysicalBytes);
Assert.AreEqual(TimeSpan.Zero, snapshot.CapturedAtUtc.Offset);
```

- [ ] **Step 2: Run RED**

Expected: missing provider types.

- [ ] **Step 3: Implement provider and native adapter**

Use `GetPhysicallyInstalledSystemMemory`, `GlobalMemoryStatusEx`, `RuntimeInformation.OSDescription`, `Environment.OSVersion.Version`, and `RuntimeInformation.OSArchitecture`. Convert KiB with `checked(value * 1024UL)`. Throw `WindowsSystemSnapshotException` with only `HI-WINDOWS-MEMORY-UNAVAILABLE`, `HI-WINDOWS-MEMORY-OVERFLOW`, or `HI-WINDOWS-MEMORY-INCONSISTENT`.

- [ ] **Step 4: Run GREEN, run a real Windows smoke test, and commit**

The smoke test asserts only structural relationships and UTC time; it must not print or retain host/user identity. Commit as `feat(hardware-inspection): capture Windows system snapshot`.

### Task 3: Verify a trusted flat tool package

**Files:**
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/TrustedTools/TrustedToolPackageVerifier.cs`
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/TrustedTools/PeImageInspector.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/TrustedTools/TrustedToolPackageVerifierTests.cs`

**Interfaces:**
- Consumes: `TrustedToolPackageManifest`.
- Produces: `TrustedToolVerificationResult Verify(string approvedRoot, string packageRoot, TrustedToolPackageManifest manifest)` with either `VerifiedTrustedTool` or one closed failure.

- [ ] **Step 1: Write RED verification tests**

Create fresh real directories/files and hand-built PE fixtures. Cover exact success, root escape, rooted/traversal member, missing/extra/nested/case-colliding member, reparse package/member/ancestor, hash mismatch, non-AMD64 PE, and mutation between initial inventory and stable-open validation.

```csharp
TrustedToolVerificationResult result = verifier.Verify(approvedRoot, packageRoot, manifest);
Assert.IsTrue(result.IsVerified);
Assert.AreEqual(Path.Combine(packageRoot, "llmfit.exe"), result.Tool!.ExecutablePath);
```

- [ ] **Step 2: Run RED**

Expected: verifier is missing.

- [ ] **Step 3: Implement stable verification**

Canonicalize with `Path.GetFullPath`, enforce containment using `Path.GetRelativePath`, reject `..`, rooted paths and reparse attributes, compare exact flat inventory ordinal-ignore-case while retaining canonical spellings, open files read-only with no write/delete sharing, hash the stable stream, parse MZ/PE/AMD64 from that same stream, and re-enumerate before success.

- [ ] **Step 4: Run GREEN and commit**

Also execute the existing 174 Gate deterministic tests. Commit as `feat(hardware-inspection): verify trusted tool packages`.

### Task 4: Run verified tools through a bounded process boundary

**Files:**
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Processes/IExternalProcessRunner.cs`
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Processes/ExternalProcessRunner.cs`
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Processes/BoundedProcessOutput.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Processes/ExternalProcessRunnerTests.cs`
- Modify: `tests/ProcessFixtures/GraniteEdgeAI.HardwareInspection.LlmFitFakeTool/Program.cs` only if a missing deterministic child-process mode is proved by RED.

**Interfaces:**
- Consumes: `VerifiedTrustedTool`, manifest-declared command identity, timeout, stdout cap, stderr cap, and cancellation token.
- Produces: `Task<ExternalProcessResult> RunAsync(...)`.

- [ ] **Step 1: Write RED real-process tests**

Publish the existing fake fixture to a fresh short path, verify it through the real package verifier, and test exact arguments, successful bounded output, non-zero exit, stdout overflow, stderr overflow, timeout, caller cancellation, and child-process-tree cleanup. Assert returned behavior, never mock calls.

- [ ] **Step 2: Run RED**

Expected: runner is missing.

- [ ] **Step 3: Implement the minimal runner**

Construct `ProcessStartInfo.ArgumentList` only from the verified command, set `UseShellExecute=false`, `CreateNoWindow=true`, redirect all standard streams, read bytes through independent bounded readers, race exit against timeout/cancellation, call `Kill(entireProcessTree: true)` on every non-exit terminal path, await bounded cleanup, and return only the closed termination reason and bounded UTF-8 strings.

- [ ] **Step 4: Run GREEN, repeat cancellation/timeout tests, and commit**

Run the focused test class three times to expose cleanup races, confirm no fixture process remains, then commit as `feat(hardware-inspection): add bounded external process runner`.

### Task 5: Add the app adapter without activating collection

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Infrastructure/WindowsAvailableMemoryProvider.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/WindowsAvailableMemoryProviderTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj`
- Modify: `IBM Granite with TurboQuant (Intel).slnx`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionJourneyTests.cs`

**Interfaces:**
- Consumes: `WindowsSystemSnapshotProvider`.
- Produces: existing `IAvailableMemoryProvider.CaptureAsync` value.

- [ ] **Step 1: Write RED adapter and composition tests**

Assert exact available bytes/timestamp mapping and cancellation. Strengthen the composition test so `OnboardingShellPage` still receives `UnavailableHardwareInspectionService`, and assert the app package contains no `llmfit.exe`, Gate evidence JSON, or trusted/offline TRX.

- [ ] **Step 2: Run RED**

Expected: adapter type missing; existing composition assertion remains green.

- [ ] **Step 3: Implement adapter and project references**

Add project references only. Do not instantiate or register the adapter in onboarding or app startup.

- [ ] **Step 4: Run packaged GREEN tests and commit**

Run the focused packaged filter for Hardware Inspection and onboarding. Commit as `feat(hardware-inspection): add Windows memory adapter boundary`.

### Task 6: Gate 2 verification and evidence

**Files:**
- Create: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/README.md`
- Update: `docs/reviews/2026-08-22-hardware-inspection-integration-preservation-matrix.md`

- [ ] **Step 1: Run all fresh verification**

Run foundation tests, 174 Gate deterministic tests, packaged Hardware Inspection/Model contract/onboarding tests, Stage 0/A Python tests, `git diff --check`, conflict-marker scan, and Debug/Release x64 app builds. Parse every TRX and require zero failed/skipped tests where the suite contract requires no skips.

- [ ] **Step 2: Audit security and packaging**

Confirm no `Process.Start` exists outside the bounded runner in the new project, no shell command string exists, no candidate/evidence/TRX is tracked or packaged, no model type enters the foundation project, and production composition remains unavailable.

- [ ] **Step 3: Update evidence and commit**

Record exact commands/counts/warnings and Gate 3 as the next step. Use `superpowers:verification-before-completion`, request code review, and commit as `docs(hardware-inspection): record Gate 2 verification`.
