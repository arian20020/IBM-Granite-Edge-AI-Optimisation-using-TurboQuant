# Model Inspection Worker Gate 2 — Hardened Host and Process Adapter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement and prove the Windows x64 protected Model Inspection worker/process boundary defined by the approved Gate 2 specification without adding LLamaSharp, llama.cpp, application classification, packaging, or WinUI integration.

**Architecture:** Add a pure shared transport library, a short-lived production console worker with an injectable engine seam, and a separate application-side `WorkerClient` infrastructure library. The client launches the worker with `CreateProcessW` and `STARTUPINFOEX`, assigns it to a kill-on-close Job Object during process creation, inherits exactly three pipe handles, validates the versioned protocol, and fails closed on malformed output, crash, hang, timeout, cancellation failure, or incomplete process-tree cleanup. A separate test-only executable creates abnormal process behaviour; the production worker contains no hidden test switches.

**Tech Stack:** C# 12, repository-pinned .NET SDK `8.0.419`, `net8.0`, `net8.0-windows10.0.19041.0`, MSTest `4.3.2`, Microsoft.NET.Test.Sdk `18.8.1`, Windows SDK/Win32 P/Invoke, GitHub Actions `windows-latest`, and the existing `GraniteEdgeAI.ModelInspection.Contracts` protocol version 1.

## Global Constraints

- Work on `feature/model-inspection-worker-host`, stacked on Gate 1 head `0d50f27405d66be944e1352284b68928ae228e74`.
- Implement Gate 2 only: no LLamaSharp, llama.cpp, TurboQuant, OpenVINO, Hardware Fit, chat, classifier, service, ViewModel, XAML, publish closure, or MSIX integration.
- Preserve `WorkerProtocol`: version `1`; worker ID `GraniteEdgeAI.ModelInspection.Worker`; runtime profile `llamasharp-0.27.0-cpu-win-x64-vocab-only-v1`; line limit `1 MiB`; retained stderr `256 KiB`; startup `5 seconds`; overall `5 minutes`; cancellation grace `5 seconds`.
- Windows x64 is the only production target in this gate.
- Use no HTTP server, listener, TCP port, named pipe, required network access, or executable `PATH` search.
- Send the model path only in `WorkerStartInspectionCommand` over stdin, never in production process arguments or diagnostics.
- Launch with `CreateProcessW`, `STARTUPINFOEX`, `PROC_THREAD_ATTRIBUTE_JOB_LIST`, and `PROC_THREAD_ATTRIBUTE_HANDLE_LIST`; there is no uncontained fallback.
- Configure `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`; do not enable breakaway.
- Inherit exactly child stdin-read, stdout-write, and stderr-write handles.
- Use strict UTF-8 without BOM and one LF terminator. Reject CR/CRLF, invalid UTF-8, empty line, partial EOF, and over-limit data before unbounded buffering.
- Start stdout and stderr draining immediately and concurrently; continue draining stderr after retained storage reaches `256 KiB`.
- Preserve the first proven failure; cleanup failures are secondary diagnostics.
- Forced termination is never `Cancelled`; root-process exit alone is never cleanup proof.
- The Gate 2 production engine returns a controlled `OperationalFailure` with no fabricated evidence and does not open the model.
- All abnormal modes live only in `tests/ProcessFixtures/GraniteEdgeAI.ModelInspection.ProtocolTestWorker`.
- New projects use nullable reference types, analyzers, deterministic builds, warnings-as-errors, `SafeHandle` ownership, named constants, and idempotent cleanup.
- Every behaviour follows red-green-refactor and ends with focused tests, affected regressions, and a review gate.
- Hosted CI on the exact evidence head is required before Gate 2 closure or Gate 3 entry.
- Do not add `CREATE_SUSPENDED`, Job CPU/memory/active-process limits, process mitigation policies, restricted tokens, AppContainer, or Job completion ports in Gate 2; retain them as explicit evidence-driven deferrals from the approved specification.

## Locked Project Map

```text
shared/GraniteEdgeAI.ModelInspection.Transport/
workers/GraniteEdgeAI.ModelInspection.Worker/
infrastructure/GraniteEdgeAI.ModelInspection.WorkerClient/
tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests/
tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/
tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/
tests/ProcessFixtures/GraniteEdgeAI.ModelInspection.ProtocolTestWorker/
tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/
```

Stable public interfaces:

```csharp
public interface IInspectionWorkerClient
{
    Task<WorkerClientResult> ExecuteAsync(
        WorkerStartInspectionCommand command,
        IProgress<WorkerProgressMessage>? progress,
        CancellationToken cancellationToken);
}

public interface IWorkerInspectionEngine
{
    Task<WorkerEngineResult> InspectAsync(
        WorkerStartInspectionCommand command,
        IProgress<WorkerProgressMessage> progress,
        CancellationToken cancellationToken);
}
```

---

### Task 1: Establish project shells and dependency fitness

**Files:**
- Create: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/Gate2ProjectGraphTests.cs`
- Create: all eight project files in the Locked Project Map.
- Create temporary entry points: worker `Program.cs` and fixture `Program.cs`.
- Modify: `IBM Granite with TurboQuant (Intel).slnx`.

**Interfaces:**
- Consumes: `shared/GraniteEdgeAI.ModelInspection.Contracts/GraniteEdgeAI.ModelInspection.Contracts.csproj`.
- Produces: buildable Gate 2 project boundaries. Worker and WorkerClient reference only Contracts and Transport; Transport references no project.

- [ ] **Step 1: Write the failing graph test**

```csharp
[TestClass]
[TestCategory("Architecture")]
public sealed class Gate2ProjectGraphTests
{
    private static readonly string Root = RepositoryRoot.Find();

    [TestMethod]
    public void ApprovedGate2ProjectsExist()
    {
        string[] paths =
        [
            "shared/GraniteEdgeAI.ModelInspection.Transport/GraniteEdgeAI.ModelInspection.Transport.csproj",
            "workers/GraniteEdgeAI.ModelInspection.Worker/GraniteEdgeAI.ModelInspection.Worker.csproj",
            "infrastructure/GraniteEdgeAI.ModelInspection.WorkerClient/GraniteEdgeAI.ModelInspection.WorkerClient.csproj",
            "tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests/GraniteEdgeAI.ModelInspection.Transport.Tests.csproj",
            "tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/GraniteEdgeAI.ModelInspection.Worker.Tests.csproj",
            "tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests.csproj",
            "tests/ProcessFixtures/GraniteEdgeAI.ModelInspection.ProtocolTestWorker/GraniteEdgeAI.ModelInspection.ProtocolTestWorker.csproj",
            "tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.csproj"
        ];

        foreach (string path in paths)
        {
            Assert.IsTrue(File.Exists(Path.Combine(Root, path)), $"Missing {path}");
        }
    }

    [TestMethod]
    public void ProductionProjectReferencesFollowApprovedDirection()
    {
        ProjectGraphAssert.HasExactly(
            Root,
            "shared/GraniteEdgeAI.ModelInspection.Transport/GraniteEdgeAI.ModelInspection.Transport.csproj");
        ProjectGraphAssert.HasExactly(
            Root,
            "workers/GraniteEdgeAI.ModelInspection.Worker/GraniteEdgeAI.ModelInspection.Worker.csproj",
            "shared/GraniteEdgeAI.ModelInspection.Contracts/GraniteEdgeAI.ModelInspection.Contracts.csproj",
            "shared/GraniteEdgeAI.ModelInspection.Transport/GraniteEdgeAI.ModelInspection.Transport.csproj");
        ProjectGraphAssert.HasExactly(
            Root,
            "infrastructure/GraniteEdgeAI.ModelInspection.WorkerClient/GraniteEdgeAI.ModelInspection.WorkerClient.csproj",
            "shared/GraniteEdgeAI.ModelInspection.Contracts/GraniteEdgeAI.ModelInspection.Contracts.csproj",
            "shared/GraniteEdgeAI.ModelInspection.Transport/GraniteEdgeAI.ModelInspection.Transport.csproj");
    }
}
```

Add `RepositoryRoot.Find()` and `ProjectGraphAssert.HasExactly(...)` as internal helpers in the same test file; the helper loads project XML, resolves each `ProjectReference`, normalises separators, sorts ordinally, and compares exact arrays.

- [ ] **Step 2: Run red and commit the test**

```powershell
dotnet test "tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" -c Release --filter "FullyQualifiedName~Gate2ProjectGraphTests"
```

Expected: FAIL naming the first missing project.

```powershell
git add "tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/Gate2ProjectGraphTests.cs"
git commit -m "test(model-inspection): define Gate 2 project boundaries"
```

- [ ] **Step 3: Create project files**

Transport project:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <Deterministic>true</Deterministic>
    <AllowUnsafeBlocks>false</AllowUnsafeBlocks>
  </PropertyGroup>
</Project>
```

Worker and WorkerClient use:

```xml
<PropertyGroup>
  <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
  <PlatformTarget>x64</PlatformTarget>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <EnableNETAnalyzers>true</EnableNETAnalyzers>
  <AnalysisLevel>latest-recommended</AnalysisLevel>
  <Deterministic>true</Deterministic>
  <PublishTrimmed>false</PublishTrimmed>
  <AllowUnsafeBlocks>false</AllowUnsafeBlocks>
</PropertyGroup>
<ItemGroup>
  <ProjectReference Include="..\..\shared\GraniteEdgeAI.ModelInspection.Contracts\GraniteEdgeAI.ModelInspection.Contracts.csproj" />
  <ProjectReference Include="..\..\shared\GraniteEdgeAI.ModelInspection.Transport\GraniteEdgeAI.ModelInspection.Transport.csproj" />
</ItemGroup>
```

Worker adds `<OutputType>Exe</OutputType>`. Test projects copy the existing Contracts test project’s MSTest `18.8.1`/`4.3.2` configuration. Process-bearing tests target Windows x64; Transport tests target pure `net8.0`. Fixture is an x64 console executable referencing Contracts and Transport.

Temporary worker entry point returns exit `4`; temporary fixture entry point returns `0`. Comments must state that behaviour is replaced test-first in later tasks.

- [ ] **Step 4: Add all projects to the solution and run green**

```powershell
dotnet restore "IBM Granite with TurboQuant (Intel).slnx"
dotnet test "tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" -c Release --filter "FullyQualifiedName~Gate2ProjectGraphTests"
dotnet build "IBM Granite with TurboQuant (Intel).slnx" -c Release -p:Platform=x64 --no-restore
```

Expected: PASS; all shells compile; no Gate 2 production project restores LLamaSharp or Windows App SDK.

- [ ] **Step 5: Commit green**

```powershell
git add "IBM Granite with TurboQuant (Intel).slnx" shared workers infrastructure tests
git commit -m "build(model-inspection): add Gate 2 project boundaries"
```

**Reviewer gate:** Reject wrong dependency direction, non-x64 process projects, warning suppression, or fixture/WinUI/LLamaSharp references in production.

---

### Task 2: Implement strict byte-bounded UTF-8 framing

**Files:**
- Create: `shared/GraniteEdgeAI.ModelInspection.Transport/ProtocolStreamErrorKind.cs`
- Create: `shared/GraniteEdgeAI.ModelInspection.Transport/ProtocolStreamException.cs`
- Create: `shared/GraniteEdgeAI.ModelInspection.Transport/BoundedUtf8LineReader.cs`
- Create: `shared/GraniteEdgeAI.ModelInspection.Transport/BoundedUtf8LineWriter.cs`
- Create: reader/writer tests under `tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests/`.

**Interfaces:**
- Produces:

```csharp
public sealed class BoundedUtf8LineReader
{
    public BoundedUtf8LineReader(Stream stream, int maximumLineBytes);
    public ValueTask<byte[]?> ReadLineAsync(CancellationToken cancellationToken);
}

public sealed class BoundedUtf8LineWriter
{
    public BoundedUtf8LineWriter(Stream stream, int maximumLineBytes);
    public ValueTask WriteLineAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);
}
```

- [ ] **Step 1: Write failing tests**

Tests cover: exact-limit LF line succeeds; empty stream returns `null`; empty line rejects; BOM rejects; CR/CRLF reject; invalid UTF-8 rejects; limit+1 rejects on the first extra byte; partial EOF rejects; cancellation interrupts a blocked read; writer adds one LF and writes no BOM/CR; writer rejects embedded raw LF and oversized data before writing. Add `OneByteAtATimeReadStream`, a test-only `Stream` that returns at most one byte per read and exposes `BytesServed`, so the over-limit assertion measures processed bytes rather than operating-system read-ahead.

Representative red test:

```csharp
[TestMethod]
public async Task ReadLineAsync_StopsAtFirstByteBeyondLimit()
{
    byte[] input = [1, 2, 3, 4, 5, (byte)'\n'];
    await using OneByteAtATimeReadStream stream = new(input);
    BoundedUtf8LineReader reader = new(stream, 4);

    ProtocolStreamException error = await Assert.ThrowsExactlyAsync<ProtocolStreamException>(
        () => reader.ReadLineAsync(CancellationToken.None).AsTask());

    Assert.AreEqual(ProtocolStreamErrorKind.LineTooLong, error.ErrorKind);
    Assert.AreEqual(5, stream.BytesServed,
        "Reader must fail as soon as the first over-limit byte is observed.");
}
```

- [ ] **Step 2: Run red**

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests/GraniteEdgeAI.ModelInspection.Transport.Tests.csproj" -c Release
```

Expected: compile failure because framing types do not exist.

- [ ] **Step 3: Implement error types and bounded reader**

```csharp
public enum ProtocolStreamErrorKind
{
    InvalidConfiguration,
    EmptyLine,
    BomNotAllowed,
    CarriageReturnNotAllowed,
    InvalidUtf8,
    LineTooLong,
    UnexpectedEndOfStream
}

public sealed class ProtocolStreamException : Exception
{
    public ProtocolStreamException(ProtocolStreamErrorKind errorKind, string message, Exception? inner = null)
        : base(message, inner) => ErrorKind = errorKind;

    public ProtocolStreamErrorKind ErrorKind { get; }
}
```

Reader requirements:

```csharp
private static readonly UTF8Encoding StrictUtf8 = new(false, true);

public async ValueTask<byte[]?> ReadLineAsync(CancellationToken token)
{
    byte[] rented = ArrayPool<byte>.Shared.Rent(_maximumLineBytes);
    int length = 0;
    try
    {
        while (true)
        {
            int next = await ReadBufferedByteAsync(token).ConfigureAwait(false);
            if (next < 0)
            {
                if (length == 0) return null;
                throw Error(ProtocolStreamErrorKind.UnexpectedEndOfStream);
            }
            if (next == '\n')
            {
                if (length == 0) throw Error(ProtocolStreamErrorKind.EmptyLine);
                ValidateStrictUtf8AndNoBom(rented.AsSpan(0, length));
                return rented.AsSpan(0, length).ToArray();
            }
            if (next == '\r') throw Error(ProtocolStreamErrorKind.CarriageReturnNotAllowed);
            if (length == _maximumLineBytes) throw Error(ProtocolStreamErrorKind.LineTooLong);
            rented[length++] = (byte)next;
        }
    }
    finally
    {
        ArrayPool<byte>.Shared.Return(rented, clearArray: true);
    }
}
```

Use a fixed 4 KiB internal read buffer. Do not use `StreamReader.ReadLineAsync`, `Console.ReadLine`, or an unbounded `MemoryStream`.

- [ ] **Step 4: Implement serialized writer**

Validate non-empty, `<= maximumLineBytes`, strict UTF-8, no BOM, no CR, and no raw LF. Use a `SemaphoreSlim` to make each payload+LF+flush one atomic write operation.

```csharp
await _writeLock.WaitAsync(token).ConfigureAwait(false);
try
{
    await _stream.WriteAsync(payload, token).ConfigureAwait(false);
    await _stream.WriteAsync(new byte[] { (byte)'\n' }, token).ConfigureAwait(false);
    await _stream.FlushAsync(token).ConfigureAwait(false);
}
finally
{
    _writeLock.Release();
}
```

- [ ] **Step 5: Run green and regressions**

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests/GraniteEdgeAI.ModelInspection.Transport.Tests.csproj" -c Release
dotnet test "tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" -c Release
```

- [ ] **Step 6: Commit**

```powershell
git add shared/GraniteEdgeAI.ModelInspection.Transport tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests
git commit -m "feat(model-inspection): add bounded worker stream transport"
```

**Reviewer gate:** Reject replacement-character UTF-8, CRLF, over-limit buffering, unbounded line APIs, or writer interleaving.

---

### Task 3: Define WorkerClient execution domain and stable failure codes

**Files:**
- Create: `infrastructure/GraniteEdgeAI.ModelInspection.WorkerClient/IInspectionWorkerClient.cs`
- Create: `WorkerClientOptions.cs`, `WorkerClientFailureCodes.cs`, `WorkerClientFailure.cs`, `WorkerClientResult.cs`, `WorkerLifecycleState.cs`, `WorkerClientPolicyException.cs`.
- Create: `tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/WorkerClientDomainTests.cs`.

**Interfaces:**
- Consumes: existing command/message contracts and `WorkerProtocol` constants.
- Produces: the public client interface shown above and immutable result/failure types used by all later client tasks.

- [ ] **Step 1: Write failing tests**

```csharp
[TestMethod]
public void DefaultOptionsUseExactWorkerProtocolValues()
{
    WorkerClientOptions options = WorkerClientOptions.CreateDefault(@"C:\Program Files\GraniteEdgeAI");
    Assert.AreEqual(WorkerProtocol.StartupTimeout, options.StartupTimeout);
    Assert.AreEqual(WorkerProtocol.OverallTimeout, options.OverallTimeout);
    Assert.AreEqual(WorkerProtocol.CancellationGracePeriod, options.CancellationGracePeriod);
    Assert.AreEqual(WorkerProtocol.MaximumRetainedStandardErrorBytes, options.MaximumRetainedStandardErrorBytes);
}

[TestMethod]
public void ResultRejectsTerminalAndInfrastructureFailureTogether()
{
    WorkerClientResult result = new(
        TerminalMessage: WorkerTestData.CancelledTerminal(),
        Failure: new(WorkerClientFailureCodes.WorkerCrashed, "Worker ended unexpectedly."),
        ExitCode: 3,
        ForcedTermination: false,
        StandardErrorTruncated: false,
        RetainedStandardError: string.Empty,
        SecondaryDiagnostics: []);

    Assert.ThrowsExactly<InvalidOperationException>(result.Validate);
}
```

Also test failure-code uniqueness/lower-snake-case and positive option values.

- [ ] **Step 2: Run red**

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests.csproj" -c Release --filter "FullyQualifiedName~WorkerClientDomainTests"
```

Expected: missing domain types.

- [ ] **Step 3: Implement exact failure taxonomy**

```csharp
public static class WorkerClientFailureCodes
{
    public const string WorkerExecutableUntrusted = "worker_executable_untrusted";
    public const string WorkerArchitectureUnsupported = "worker_architecture_unsupported";
    public const string WorkerLaunchFailed = "worker_launch_failed";
    public const string WorkerContainmentFailed = "worker_containment_failed";
    public const string WorkerHandlePolicyFailed = "worker_handle_policy_failed";
    public const string WorkerEnvironmentPolicyFailed = "worker_environment_policy_failed";
    public const string WorkerHandshakeTimeout = "worker_handshake_timeout";
    public const string WorkerHandshakeInvalid = "worker_handshake_invalid";
    public const string WorkerProtocolInvalid = "worker_protocol_invalid";
    public const string WorkerOutputLimitExceeded = "worker_output_limit_exceeded";
    public const string WorkerCrashed = "worker_crashed";
    public const string WorkerOverallTimeout = "worker_overall_timeout";
    public const string WorkerCancellationForced = "worker_cancellation_forced";
    public const string WorkerExitMismatch = "worker_exit_mismatch";
    public const string WorkerProcessTreeIntegrityFailed = "worker_process_tree_integrity_failed";
    public const string WorkerCleanupFailed = "worker_cleanup_failed";

    public static string[] All =>
    [
        WorkerExecutableUntrusted, WorkerArchitectureUnsupported, WorkerLaunchFailed,
        WorkerContainmentFailed, WorkerHandlePolicyFailed, WorkerEnvironmentPolicyFailed,
        WorkerHandshakeTimeout, WorkerHandshakeInvalid, WorkerProtocolInvalid,
        WorkerOutputLimitExceeded, WorkerCrashed, WorkerOverallTimeout,
        WorkerCancellationForced, WorkerExitMismatch,
        WorkerProcessTreeIntegrityFailed, WorkerCleanupFailed
    ];
}
```

- [ ] **Step 4: Implement options, lifecycle, failure, and result**

```csharp
public enum WorkerLifecycleState
{
    NotStarted, ResolvingExecutable, CreatingContainment, Starting,
    AwaitingHello, Ready, StartSent, Running, CancellationRequested,
    TerminalReceived, Exited, CleaningUp, Completed, Failed, Disposed
}

public sealed record WorkerClientOptions(
    string ApprovedWorkerRoot,
    TimeSpan StartupTimeout,
    TimeSpan OverallTimeout,
    TimeSpan CancellationGracePeriod,
    int MaximumRetainedStandardErrorBytes,
    TimeSpan ProcessTreeCleanupTimeout)
{
    public static WorkerClientOptions CreateDefault(string approvedWorkerRoot) =>
        new(approvedWorkerRoot, WorkerProtocol.StartupTimeout,
            WorkerProtocol.OverallTimeout, WorkerProtocol.CancellationGracePeriod,
            WorkerProtocol.MaximumRetainedStandardErrorBytes, TimeSpan.FromSeconds(5));
}

public sealed record WorkerClientFailure(string Code, string Message);

public sealed record WorkerClientResult(
    WorkerCompletedMessage? TerminalMessage,
    WorkerClientFailure? Failure,
    int? ExitCode,
    bool ForcedTermination,
    bool StandardErrorTruncated,
    string RetainedStandardError,
    IReadOnlyList<string> SecondaryDiagnostics)
{
    public void Validate()
    {
        bool hasTerminal = TerminalMessage is not null;
        bool hasFailure = Failure is not null;
        if (hasTerminal == hasFailure)
            throw new InvalidOperationException("Exactly one terminal or infrastructure failure is required.");
        if (ForcedTermination && hasTerminal)
            throw new InvalidOperationException("Forced termination cannot produce a trusted terminal result.");
        TerminalMessage?.Validate();
    }
}
```

`WorkerClientPolicyException` contains one validated `WorkerClientFailure` but never a raw path, environment value, model name, request ID, or protocol line.

- [ ] **Step 5: Run green and commit**

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests.csproj" -c Release --filter "FullyQualifiedName~WorkerClientDomainTests"
git add infrastructure/GraniteEdgeAI.ModelInspection.WorkerClient tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests
git commit -m "feat(model-inspection): define worker client execution domain"
```

**Reviewer gate:** Reject ambiguous result states, copied timeout values that can drift from `WorkerProtocol`, arbitrary public error strings, or public exposure of `Process`/native handles/raw lines.

---

### Task 4: Implement trusted executable and child-environment policies

**Files:**
- Create: `WorkerExecutableResolver.cs`, `VerifiedWorkerExecutable.cs`, `WorkerEnvironmentPolicy.cs`.
- Create: `Windows/WindowsEnvironmentBlock.cs`.
- Create tests: `WorkerExecutableResolverTests.cs`, `WorkerEnvironmentPolicyTests.cs`, `WindowsEnvironmentBlockTests.cs`.

**Interfaces:**
- Produces:

```csharp
internal VerifiedWorkerExecutable Resolve(string approvedRoot);
internal static IReadOnlyDictionary<string, string> Create(
    IReadOnlyDictionary<string, string?> parentEnvironment);
```

- [ ] **Step 1: Write path-policy tests**

Cover: relative/empty root; candidate outside root; candidate equals directory; missing file; x86/ARM64 PE; root/candidate reparse point; separator-aware containment; final path outside final root; successful AMD64 file; no raw path in failure message.

```csharp
[TestMethod]
public void ResolveRejectsRelativeApprovedRoot()
{
    WorkerExecutableResolver resolver = new("GraniteEdgeAI.ModelInspection.Worker.exe");
    WorkerClientPolicyException error = Assert.ThrowsExactly<WorkerClientPolicyException>(
        () => resolver.Resolve("relative"));
    Assert.AreEqual(WorkerClientFailureCodes.WorkerExecutableUntrusted, error.Failure.Code);
}
```

Use a test helper that writes a minimal PE header with machine `0x8664` or a deliberately unsupported machine; do not rely on a developer-machine executable.

- [ ] **Step 2: Run red**

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests.csproj" -c Release --filter "FullyQualifiedName~WorkerExecutableResolverTests"
```

- [ ] **Step 3: Implement maintained verification handle and final-path validation**

```csharp
internal sealed class VerifiedWorkerExecutable : IDisposable
{
    public VerifiedWorkerExecutable(string rootFinalPath, string executableFinalPath, SafeFileHandle handle)
    {
        ApprovedRootFinalPath = rootFinalPath;
        ExecutableFinalPath = executableFinalPath;
        VerificationHandle = handle;
    }

    public string ApprovedRootFinalPath { get; }
    public string ExecutableFinalPath { get; }
    public SafeFileHandle VerificationHandle { get; }
    public void Dispose() => VerificationHandle.Dispose();
}
```

Resolver order is mandatory:

```csharp
string root = RequireAbsoluteCanonicalRoot(approvedRoot);
string candidate = Path.GetFullPath(Path.Combine(root, _fixedRelativePath));
RequireSeparatorQualifiedContainment(root, candidate);
RequireNoUnexpectedReparsePoint(root, candidate);
RequireExistingRegularFile(candidate);
RequireAmd64Pe(candidate);
SafeFileHandle handle = File.OpenHandle(candidate, FileMode.Open, FileAccess.Read,
    FileShare.Read, FileOptions.RandomAccess);
string finalRoot = ResolveDirectoryFinalPath(root);
string finalExecutable = ResolveFileFinalPath(handle);
RequireSeparatorQualifiedContainment(finalRoot, finalExecutable);
return new VerifiedWorkerExecutable(finalRoot, finalExecutable, handle);
```

Use `GetFinalPathNameByHandleW`; normalise `\\?\` and volume forms before ordinal-ignore-case, separator-qualified comparison. Keep the executable handle alive through `CreateProcessW` where the API permits. Gate 4 owns hash/signature/package immutability; Gate 2 must not claim complete TOCTOU elimination.

- [ ] **Step 4: Write environment-policy tests**

```csharp
[TestMethod]
public void EnvironmentDropsPathSecretsAndDiagnosticPorts()
{
    Dictionary<string, string?> parent = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SystemRoot"] = @"C:\Windows", ["WINDIR"] = @"C:\Windows",
        ["ComSpec"] = @"C:\Windows\System32\cmd.exe",
        ["TEMP"] = @"C:\Temp", ["TMP"] = @"C:\Temp",
        ["PATH"] = @"C:\Untrusted", ["AZURE_CLIENT_SECRET"] = "secret",
        ["DOTNET_DiagnosticPorts"] = "listen", ["REQUEST_ID"] = "sensitive"
    };

    IReadOnlyDictionary<string, string> child = WorkerEnvironmentPolicy.Create(parent);
    Assert.IsFalse(child.ContainsKey("PATH"));
    Assert.IsFalse(child.ContainsKey("AZURE_CLIENT_SECRET"));
    Assert.IsFalse(child.ContainsKey("DOTNET_DiagnosticPorts"));
    Assert.AreEqual("0", child["DOTNET_EnableDiagnostics"]);
    Assert.AreEqual("0", child["COMPlus_EnableDiagnostics"]);
}
```

- [ ] **Step 5: Implement explicit allowlist**

Allow only `SystemRoot`, `WINDIR`, `ComSpec`, `TEMP`, `TMP`, and processor architecture keys when present. Require Windows/temp keys. Add `DOTNET_EnableDiagnostics=0` and `COMPlus_EnableDiagnostics=0`. Reject NUL/blank values. Do not add `PATH` unless a focused publish test proves a specific dependency requires it; never copy the full parent environment.

`WindowsEnvironmentBlock.Create` sorts keys ordinal-ignore-case, writes UTF-16 `key=value\0` entries and a final extra NUL, owns `Marshal.AllocHGlobal` memory idempotently, and tests the exact double-NUL ending.

- [ ] **Step 6: Run green and commit**

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests.csproj" -c Release --filter "FullyQualifiedName~WorkerExecutableResolverTests|FullyQualifiedName~WorkerEnvironmentPolicyTests|FullyQualifiedName~WindowsEnvironmentBlockTests"
git add infrastructure/GraniteEdgeAI.ModelInspection.WorkerClient tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests
git commit -m "feat(model-inspection): secure worker path and environment policies"
```

**Reviewer gate:** Reject `PATH` search, user-selected executable, prefix-only containment, unverified machine type, complete environment inheritance, enabled diagnostics, double-free, or sensitive exception text.

---

### Task 5: Build safe Win32 process primitives

**Files:**
- Create under `infrastructure/...WorkerClient/Windows/`: `NativeConstants.cs`, `NativeStructures.cs`, `NativeMethods.cs`, `SafeJobHandle.cs`, `SafeProcessHandle.cs`, `SafeThreadHandle.cs`, `SafeAttributeListBuffer.cs`, `WindowsPipeSet.cs`, `WindowsJobObject.cs`, `WindowsProcessWaiter.cs`.
- Create tests: `NativeLayoutTests.cs`, `WindowsPipeSetTests.cs`, `WindowsJobObjectTests.cs`.

**Interfaces:**
- Internal only. Produces safe Job Object, pipe, attribute-list, process/thread handle, and async wait primitives for Task 6.

- [ ] **Step 1: Write native-layout tests**

```csharp
[TestMethod]
public void RequiredCreationFlagsHaveNoBreakaway()
{
    uint flags = NativeConstants.RequiredCreationFlags;
    Assert.AreNotEqual(0u, flags & NativeConstants.ExtendedStartupInfoPresent);
    Assert.AreNotEqual(0u, flags & NativeConstants.CreateNoWindow);
    Assert.AreNotEqual(0u, flags & NativeConstants.CreateUnicodeEnvironment);
    Assert.AreEqual(0u, flags & NativeConstants.CreateBreakawayFromJob);
}
```

Also assert `STARTUPINFOEX` layout, two distinct process-thread attributes, `STARTF_USESTDHANDLES`, and x64 pointer-size assumptions.

- [ ] **Step 2: Run red**

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests.csproj" -c Release --filter "FullyQualifiedName~NativeLayoutTests"
```

- [ ] **Step 3: Define reviewed constants, structures, and P/Invokes**

Required constants include creation flags, `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`, wait results, handle inheritance flag, `STILL_ACTIVE`, AMD64 machine, `PROC_THREAD_ATTRIBUTE_HANDLE_LIST`, and `PROC_THREAD_ATTRIBUTE_JOB_LIST`.

Required declarations include:

```csharp
CreateJobObjectW
SetInformationJobObject
QueryInformationJobObject
TerminateJobObject
CreatePipe
SetHandleInformation
InitializeProcThreadAttributeList
UpdateProcThreadAttribute
DeleteProcThreadAttributeList
CreateProcessW
WaitForSingleObject
GetExitCodeProcess
CloseHandle
```

Use `LibraryImport` where its marshalling is supported, otherwise one isolated `DllImport` file. Every error-returning call uses `SetLastError=true`; structures use official field order and pointer widths.

- [ ] **Step 4: Implement deterministic owners**

`SafeJobHandle`, `SafeProcessHandle`, and `SafeThreadHandle` derive from `SafeHandleZeroOrMinusOneIsInvalid`. `SafeAttributeListBuffer.Create(2)` follows the two-call sizing protocol, calls `DeleteProcThreadAttributeList`, then frees unmanaged memory exactly once.

`WindowsPipeSet.Create()` creates three anonymous pipes with inheritable child ends and clears inheritance on parent ends. Tests prove directions, EOF, and idempotent disposal.

`WindowsJobObject.CreateKillOnClose()` creates an unnamed job and sets only kill-on-close. `GetActiveProcessCount()` uses `QueryInformationJobObject`; `Terminate(uint)` calls `TerminateJobObject`.

`WindowsProcessWaiter.WaitAsync` uses a registered wait or wait-handle wrapper, not a blocked UI thread or endless polling.

- [ ] **Step 5: Prove kill-on-close with a harmless fixture process**

The wrapper-level test may assign a fixture after start solely to test `WindowsJobObject`; the production launcher must use creation-time assignment. Close the final job handle and assert the process exits before a named deadline.

- [ ] **Step 6: Run green and commit**

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests.csproj" -c Release --filter "FullyQualifiedName~NativeLayoutTests|FullyQualifiedName~WindowsPipeSetTests|FullyQualifiedName~WindowsJobObjectTests"
git add infrastructure/GraniteEdgeAI.ModelInspection.WorkerClient/Windows tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests
git commit -m "feat(model-inspection): add safe Windows process primitives"
```

**Reviewer gate:** Reject unmanaged ownership by raw `IntPtr`, missing last-error handling, unsafe code spread across files, breakaway flags, inheritable parent ends, or blocking waits.

---

### Task 6: Launch atomically with Job Object and exact handle allowlist

**Files:**
- Create: `Windows/WindowsProcessLaunchRequest.cs`, `Windows/WindowsWorkerProcessLauncher.cs`.
- Create: `WorkerProcessSession.cs`.
- Modify fixture `Program.cs` to add a test-only `launch-probe` mode.
- Create integration helpers/tests: `PublishedFixture.cs`, `WorkerLaunchContainmentTests.cs`.

**Interfaces:**
- Consumes: `VerifiedWorkerExecutable`, environment block, pipe set, Job Object, safe handles.
- Produces:

```csharp
internal static WorkerProcessSession Launch(WindowsProcessLaunchRequest request);
```

- [ ] **Step 1: Add a deterministic fixture probe**

```csharp
internal static async Task<int> Main(string[] args)
{
    if (args.Length != 1 || args[0] != "launch-probe") return 64;
    await Console.Out.WriteLineAsync("fixture-ready");
    await Console.Out.FlushAsync();
    _ = await Console.In.ReadLineAsync();
    return 0;
}
```

This parser remains fixture-only. Production gets no scenario argument path.

- [ ] **Step 2: Write failing creation-time containment test**

```csharp
[TestMethod]
public async Task LaunchCreatesPrivateJobAndThreePipes()
{
    string root = await PublishedFixture.CreateAsync();
    using VerifiedWorkerExecutable executable =
        new WorkerExecutableResolver("GraniteEdgeAI.ModelInspection.ProtocolTestWorker.exe").Resolve(root);

    WindowsProcessLaunchRequest request = new(
        executable, root, WorkerEnvironmentPolicy.Create(EnvironmentSnapshot.Capture()),
        TestOnlyArguments: ["launch-probe"]);

    await using WorkerProcessSession session = WindowsWorkerProcessLauncher.Launch(request);
    Assert.AreEqual("fixture-ready", await ProcessTestDeadline.ReadLineAsync(session.StandardOutput));
    Assert.AreEqual(1u, session.Job.GetActiveProcessCount());
}
```

- [ ] **Step 3: Run red**

```powershell
dotnet test "tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.csproj" -c Release --filter "FullyQualifiedName~WorkerLaunchContainmentTests"
```

Expected: missing launcher/session types.

- [ ] **Step 4: Implement request and session ownership**

```csharp
internal sealed record WindowsProcessLaunchRequest(
    VerifiedWorkerExecutable Executable,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string> Environment,
    IReadOnlyList<string> TestOnlyArguments);
```

Production composition passes an empty argument list; launcher always places the quoted executable path in writable `argv[0]`. `WorkerProcessSession : IAsyncDisposable` owns process handle/ID, Job Object, parent stdin-write/stdout-read/stderr-read streams, lifecycle state, stream tasks, and secondary diagnostics. The Job Object closes last.

- [ ] **Step 5: Implement two-entry `STARTUPINFOEX` launch**

The visible production sequence is:

```csharp
using WindowsEnvironmentBlock environment = WindowsEnvironmentBlock.Create(request.Environment);
WindowsJobObject job = WindowsJobObject.CreateKillOnClose();
WindowsPipeSet pipes = WindowsPipeSet.Create();
using SafeAttributeListBuffer attributes = SafeAttributeListBuffer.Create(2);

attributes.SetHandleList([
    pipes.ChildStandardInputRead,
    pipes.ChildStandardOutputWrite,
    pipes.ChildStandardErrorWrite]);
attributes.SetJobList([job.SafeHandle]);

NativeStructures.StartupInfoEx startup = NativeStructures.StartupInfoEx.Create(
    attributes.Pointer,
    pipes.ChildStandardInputRead,
    pipes.ChildStandardOutputWrite,
    pipes.ChildStandardErrorWrite);

bool created = NativeMethods.CreateProcess(
    request.Executable.ExecutableFinalPath,
    BuildWritableCommandLine(request.Executable.ExecutableFinalPath, request.TestOnlyArguments),
    IntPtr.Zero, IntPtr.Zero,
    inheritHandles: true,
    NativeConstants.RequiredCreationFlags,
    environment.Pointer,
    request.WorkingDirectory,
    ref startup,
    out NativeStructures.ProcessInformation information);
```

Keep backing arrays for both attributes alive until `CreateProcessW` returns. After success: wrap process/thread handles immediately, close primary thread handle, close the parent copies of child pipe ends, and transfer only parent streams/job/process to the session. On any failure: close every partial resource and return a stable failure. Do not retry with `Process.Start`, post-start assignment, or relaxed inheritance.

- [ ] **Step 6: Test failure paths and nested-job policy**

Use narrow internal native-call seams for deterministic failures. Assert:

- Job setup failure → `worker_containment_failed`, no process;
- handle-list failure → `worker_handle_policy_failed`, no process;
- environment failure → `worker_environment_policy_failed`, no process;
- `CreateProcessW` failure → `worker_launch_failed`, no leaked handles;
- incompatible parent-job policy → containment failure, no fallback.

- [ ] **Step 7: Prove unrelated inheritable handle is absent**

Create an unrelated inheritable event in the test process; send its numeric value only to the fixture. Fixture calls `GetHandleInformation`. Expected: unavailable because the explicit list contains only three pipes.

- [ ] **Step 8: Run green and commit**

```powershell
dotnet test "tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.csproj" -c Release --filter "FullyQualifiedName~WorkerLaunchContainmentTests" --logger "trx;LogFileName=gate2-launch.trx"
git add infrastructure/GraniteEdgeAI.ModelInspection.WorkerClient tests/ProcessFixtures tests/IntegrationTests
git commit -m "feat(model-inspection): launch workers in atomic job containment"
```

**Reviewer gate:** Reject `Process.Start` in production, `AssignProcessToJobObject` as the primary path, fallback launch, inherited unrelated handles, `CREATE_BREAKAWAY_FROM_JOB`, leaked thread handle, or job disposal before zero-active-process proof.

---

### Task 7: Implement the production worker host and engine seam

**Files:**
- Replace worker `Program.cs`.
- Create: `WorkerHost.cs`, `WorkerExitCodes.cs`, `ParentProcessMonitor.cs`, `WorkerTerminalCoordinator.cs`.
- Create under `Inspection/`: `IWorkerInspectionEngine.cs`, `WorkerEngineResult.cs`, `UnavailableWorkerInspectionEngine.cs`.
- Create worker tests and `ScriptedWorkerInspectionEngine.cs`.

**Interfaces:**
- Consumes: Contracts and Transport.
- Produces: the production executable protocol lifecycle and the exact engine seam Gate 3 will implement.

- [ ] **Step 1: Write hello-first test**

```csharp
[TestMethod]
public async Task HostWritesHelloBeforeWaitingForStart()
{
    BlockingReadStream stdin = new();
    await using MemoryStream stdout = new();
    WorkerHost host = WorkerHost.CreateForTests(
        stdin, stdout, Stream.Null,
        new ScriptedWorkerInspectionEngine(WorkerEngineResult.ControlledFailure(
            "worker_engine_unavailable_in_gate2", "Runtime is unavailable in Gate 2.")),
        new StubParentProcessMonitor(true), 1234, "0.2.0-gate2");

    Task<int> run = host.RunAsync(CancellationToken.None);
    WorkerHelloMessage hello = Assert.IsInstanceOfType<WorkerHelloMessage>(
        WorkerProtocolJson.DeserializeMessage(await ReadOneBoundedLineAsync(stdout)));
    Assert.AreEqual(WorkerProtocol.WorkerId, hello.WorkerId);
    Assert.AreEqual(1234, hello.WorkerProcessId);
    stdin.Complete();
    Assert.AreEqual(WorkerExitCodes.ProtocolMisuse, await run);
}
```

- [ ] **Step 2: Run red**

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/GraniteEdgeAI.ModelInspection.Worker.Tests.csproj" -c Release --filter "FullyQualifiedName~WorkerHostTests"
```

- [ ] **Step 3: Define exit and engine types**

```csharp
public static class WorkerExitCodes
{
    public const int Completed = 0;
    public const int ProtocolMisuse = 2;
    public const int CooperativeCancellation = 3;
    public const int ControlledOperationalFailure = 4;
}

public sealed record WorkerEngineResult(
    WorkerCompletionStatus CompletionStatus,
    WorkerInspectionEvidence? Evidence,
    WorkerOperationalFailure? OperationalFailure)
{
    public static WorkerEngineResult ControlledFailure(string code, string message) =>
        new(WorkerCompletionStatus.OperationalFailure, null,
            new WorkerOperationalFailure { Code = code, Message = message });
}
```

`UnavailableWorkerInspectionEngine.InspectAsync` validates cancellation then returns `worker_engine_unavailable_in_gate2`; it opens no file and supplies no evidence.

- [ ] **Step 4: Implement host lifecycle**

`WorkerHost.RunAsync`:

1. rejects non-x64;
2. writes/flushed exact `WorkerHelloMessage` first;
3. reads one bounded start command and validates `WorkerProtocolJson` plus `WorkerCommandSequenceValidator`;
4. writes `WorkerStartedMessage`;
5. runs engine and concurrent command reader;
6. accepts matching cancel once and treats EOF/parent loss as a cancellation request;
7. validates progress through `WorkerMessageSequenceValidator`;
8. writes exactly one terminal via `WorkerTerminalCoordinator` using `Interlocked.CompareExchange`;
9. maps terminal to exit 0/3/4 and protocol misuse to 2;
10. writes only fixed redacted stderr messages.

Terminal conversion:

```csharp
new WorkerCompletedMessage
{
    ProtocolVersion = WorkerProtocol.Version,
    MessageType = WorkerMessageKind.Completed,
    RequestId = requestId,
    CompletionStatus = result.CompletionStatus,
    Evidence = result.Evidence,
    OperationalFailure = result.OperationalFailure
};
```

- [ ] **Step 5: Implement parent-loss monitoring without PID-only polling**

Open parent with `SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION`, verify creation time equals `ParentProcessStartTimeUtc`, retain the handle, and monitor handle signal plus stdin EOF. A mismatched creation time is protocol/parent identity failure. Parent disappearance requests cooperative cancellation; it does not directly fabricate a successful `Cancelled` result.

- [ ] **Step 6: Keep `Program` as composition only**

```csharp
internal static async Task<int> Main()
{
    await using Stream input = Console.OpenStandardInput();
    await using Stream output = Console.OpenStandardOutput();
    await using Stream error = Console.OpenStandardError();
    WorkerHost host = new(
        new BoundedUtf8LineReader(input, WorkerProtocol.MaximumMessageBytes),
        new BoundedUtf8LineWriter(output, WorkerProtocol.MaximumMessageBytes),
        error, new UnavailableWorkerInspectionEngine(), new ParentProcessMonitor(),
        Environment.ProcessId,
        typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0");
    return await host.RunAsync(CancellationToken.None).ConfigureAwait(false);
}
```

No arguments, scenarios, model parser, LLamaSharp, or UI.

- [ ] **Step 7: Complete unit matrix**

Test hello once/first; malformed/oversized/duplicate start; second start; cancel before start; wrong request cancel; idempotent matching cancel; monotonic progress; all terminal statuses; one terminal under races; EOF/parent loss; exact exit codes; JSON-only stdout; redacted stderr; truthful unavailable engine.

- [ ] **Step 8: Run green and commit**

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/GraniteEdgeAI.ModelInspection.Worker.Tests.csproj" -c Release
dotnet test "tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" -c Release
git add workers/GraniteEdgeAI.ModelInspection.Worker tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests
git commit -m "feat(model-inspection): add protected worker host lifecycle"
```

**Reviewer gate:** Reject file access/fake evidence, production scenarios, multiple terminal writers, PID-only parent checks, sensitive stderr, or forced termination presented as cancellation.

---

### Task 8: Build the isolated abnormal-process fixture

**Files:**
- Replace fixture `Program.cs`.
- Create: `TestWorkerScenario.cs`, `TestWorkerScenarioParser.cs`, `ProtocolScenarioRunner.cs`, `ChildProcessScenario.cs`, `FixtureMilestoneWriter.cs`.
- Create: `TestFixtureIsolationTests.cs` in WorkerClient unit tests.

**Interfaces:**
- Test-only executable. Valid-protocol scenarios reuse Contracts/Transport; abnormal byte/process scenarios write explicit bytes or terminate/hang.

- [ ] **Step 1: Write production-isolation test**

Search production `.cs`/`.csproj` files under `shared`, `workers`, `infrastructure`, and WinUI. Reject tokens `ProtocolTestWorker`, `TestWorkerScenario`, `CrashAfterHello`, `HangAfterStart`, and `IgnoreCancellation`.

- [ ] **Step 2: Define exact scenario enum**

```csharp
internal enum TestWorkerScenario
{
    LaunchProbe, HealthyControlledFailure, NoHello, MalformedHello,
    WrongProtocolVersion, WrongWorkerId, WrongWorkerProcessId,
    WrongRuntimeProfile, WrongArchitecture, TextBeforeHello,
    InvalidUtf8, Utf8Bom, OversizedStdoutLine, MalformedJson,
    DuplicateJsonProperty, WrongRequestId, ProgressBeforeStarted,
    NonMonotonicProgress, DuplicateTerminal, ExitWithoutTerminal,
    CrashBeforeHello, CrashAfterHello, CrashAfterStart,
    HangBeforeHello, HangAfterHello, HangAfterStart,
    CooperativeCancellation, IgnoreCancellation, SpawnChildAndWait,
    ExitRootWithLiveChild, FloodStdout, FloodStderr,
    TerminalExitMismatch, EchoEnvironmentKeys, ProbeUnrelatedHandle
}
```

Parser accepts one exact kebab-case mode and only scenario-specific safe values. Unknown/extra arguments return exit 64. No model-path argument exists.

- [ ] **Step 3: Add observable milestones**

Use fixture-safe stderr milestones such as `FIXTURE:HELLO_WRITTEN`, `FIXTURE:START_RECEIVED`, `FIXTURE:CANCEL_RECEIVED`, `FIXTURE:CHILD_STARTED:<pid>`, and `FIXTURE:TERMINAL_WRITTEN`. Milestones coordinate tests only; production client correctness depends on protocol and process state.

- [ ] **Step 4: Implement explicit abnormal behaviours**

```csharp
await stdout.WriteAsync(new byte[] { 0xC3, 0x28, (byte)'\n' }); // invalid UTF-8
Environment.FailFast("Protocol test fixture crash.");
await Task.Delay(Timeout.InfiniteTimeSpan); // hang until Job Object cleanup
```

`ExitRootWithLiveChild` starts the same fixture in child-wait mode, emits child PID, then exits root. Child uses no breakaway. Stderr flood exceeds four times retained limit while a valid conversation completes; stdout flood exceeds 1 MiB before LF.

- [ ] **Step 5: Test parser, scenario reachability, and isolation**

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests.csproj" -c Release --filter "FullyQualifiedName~TestFixtureIsolationTests"
dotnet build "tests/ProcessFixtures/GraniteEdgeAI.ModelInspection.ProtocolTestWorker/GraniteEdgeAI.ModelInspection.ProtocolTestWorker.csproj" -c Release -r win-x64
```

Tests use hard deadlines and milestones, not arbitrary sleeps.

- [ ] **Step 6: Commit**

```powershell
git add tests/ProcessFixtures/GraniteEdgeAI.ModelInspection.ProtocolTestWorker tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/TestFixtureIsolationTests.cs
git commit -m "test(model-inspection): add isolated abnormal worker fixture"
```

**Reviewer gate:** Reject production failure switches, random-delay synchronization, path/secret output, network listeners, or breakaway children.

---

### Task 9: Implement WorkerClient handshake, conversation, and exit integrity

**Files:**
- Create: `InspectionWorkerClient.cs`, `WorkerHandshakeValidator.cs`, `WorkerConversation.cs`, `WorkerExitConsistencyValidator.cs`, `WorkerFailureAccumulator.cs`, `BoundedStandardErrorCollector.cs`.
- Create unit tests for each policy.
- Create integration helpers: `ProcessTestDeadline.cs`, `ProcessCleanupGuard.cs`.
- Create integration tests: `WorkerHandshakeTests.cs`, `WorkerProtocolIntegrityTests.cs`.

**Interfaces:**
- Consumes: trusted resolver/environment, atomic launcher/session, bounded transport, existing Gate 1 sequence validators.
- Produces: `IInspectionWorkerClient.ExecuteAsync` through a trusted terminal result or stable infrastructure failure.

- [ ] **Step 1: Write exact handshake tests**

```csharp
[DataTestMethod]
[DataRow(0, WorkerProtocol.WorkerId, WorkerProtocol.RuntimeProfile, "X64")]
[DataRow(1234, "wrong", WorkerProtocol.RuntimeProfile, "X64")]
[DataRow(1234, WorkerProtocol.WorkerId, "wrong-profile", "X64")]
[DataRow(1234, WorkerProtocol.WorkerId, WorkerProtocol.RuntimeProfile, "ARM64")]
public void HandshakeRejectsAnyIdentityMismatch(
    int pid, string workerId, string runtimeProfile, string architecture)
{
    WorkerHelloMessage hello = WorkerTestData.Hello(pid, workerId, runtimeProfile, architecture);
    WorkerClientPolicyException error = Assert.ThrowsExactly<WorkerClientPolicyException>(
        () => WorkerHandshakeValidator.Validate(hello, expectedProcessId: 1234));
    Assert.AreEqual(WorkerClientFailureCodes.WorkerHandshakeInvalid, error.Failure.Code);
}
```

Test exact version through `WorkerProtocolJson`, non-empty version, correct real PID, hello first/once, no preceding stdout, and 5-second production default.

- [ ] **Step 2: Write stderr collector tests**

Collector drains to EOF even after retention is full; retains at most the configured byte limit; records truncation; decodes strict UTF-8 or a fixed safe invalid-diagnostic marker; redacts canonical paths, token assignments, GUID/request IDs, and model filenames. Test a >1 MiB stream with 256 KiB retention and no deadlock.

- [ ] **Step 3: Start all observation tasks immediately after launch**

```csharp
Task<StandardErrorSnapshot> stderrTask =
    _stderrCollector.DrainAsync(session.StandardError, CancellationToken.None);
Task processExitTask = session.WaitForExitAsync(CancellationToken.None);
BoundedUtf8LineReader stdout =
    new(session.StandardOutput, WorkerProtocol.MaximumMessageBytes);
Task<byte[]?> helloReadTask = stdout.ReadLineAsync(startupToken).AsTask();
```

User cancellation must not stop stderr drainage; session cleanup closes the pipe and awaits the task.

- [ ] **Step 4: Implement startup race and exact hello validation**

Use a dedicated startup CTS. If process exits first → `worker_crashed`; timeout first → `worker_handshake_timeout`; malformed/BOM/invalid/over-limit → `worker_handshake_invalid` or `worker_output_limit_exceeded`. Then:

```csharp
object parsed = WorkerProtocolJson.DeserializeMessage(helloBytes);
WorkerHelloMessage hello = parsed as WorkerHelloMessage
    ?? throw WorkerClientPolicyException.For(
        WorkerClientFailureCodes.WorkerHandshakeInvalid,
        "Worker did not send hello first.");
WorkerHandshakeValidator.Validate(hello, session.ProcessId);
```

No downgrade or alternative profile is accepted.

- [ ] **Step 5: Write conversation and exit table tests**

`WorkerConversation` accepts exactly `Started -> zero+ monotonic Progress -> Completed`, all with expected request ID, and rejects progress-before-started, duplicate started/terminal, backward progress, message after terminal, and EOF before terminal.

```csharp
[DataTestMethod]
[DataRow(WorkerCompletionStatus.Completed, 0, false, true)]
[DataRow(WorkerCompletionStatus.Cancelled, 3, false, true)]
[DataRow(WorkerCompletionStatus.OperationalFailure, 4, false, true)]
[DataRow(WorkerCompletionStatus.Completed, 4, false, false)]
[DataRow(WorkerCompletionStatus.Cancelled, 3, true, false)]
public void ExitConsistencyMatchesProtocol(
    WorkerCompletionStatus status, int exit, bool forced, bool expected) =>
    Assert.AreEqual(expected,
        WorkerExitConsistencyValidator.IsConsistent(status, exit, forced));
```

Exit 2 is protocol misuse, never a successful request. Any other exit is abnormal.

- [ ] **Step 6: Send start after hello and process messages**

```csharp
await stdinWriter.WriteLineAsync(
    WorkerProtocolJson.Serialize(command), operationToken).ConfigureAwait(false);
conversation.MarkStartSent(command.RequestId);

while (conversation.TerminalMessage is null)
{
    byte[]? line = await stdout.ReadLineAsync(operationToken).ConfigureAwait(false);
    if (line is null)
        throw WorkerClientPolicyException.For(
            WorkerClientFailureCodes.WorkerProtocolInvalid,
            "Worker output ended before a terminal message.");

    object message = WorkerProtocolJson.DeserializeMessage(line);
    conversation.Accept(message);
    if (message is WorkerProgressMessage item) progress?.Report(item);
}
```

Do not retain raw lines after successful parsing. The model path appears only inside serialized start stdin bytes and never in errors/logs.

- [ ] **Step 7: Verify terminal, process exit, stream EOF, and empty job**

After terminal: close stdin; await root exit; read exit code; await stdout EOF and stderr drain; reject post-terminal output; cross-check terminal/exit/forced flag; wait for Job Object active count zero. Controlled `OperationalFailure` + exit 4 is a trusted worker result, not a client infrastructure failure.

- [ ] **Step 8: Preserve first failure**

`WorkerFailureAccumulator.TrySetPrimary` succeeds once. Cleanup adds safe type-name diagnostics. A handshake timeout followed by cleanup trouble remains `worker_handshake_timeout`, not `worker_cleanup_failed`.

- [ ] **Step 9: Run real fixture and production worker matrix**

Scenarios: healthy exact hello; no/malformed/wrong hello; pre-hello text; invalid UTF-8/BOM/oversize; crash/hang before/after hello; wrong request; bad progress; duplicate/no terminal; malformed/duplicate JSON; terminal/exit mismatch; crash/flood after start. Also run production Gate 2 worker and assert:

```text
hello -> started -> completed(OperationalFailure, no evidence) -> exit 4
```

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests.csproj" -c Release
dotnet test "tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.csproj" -c Release --filter "FullyQualifiedName~WorkerHandshakeTests|FullyQualifiedName~WorkerProtocolIntegrityTests" --logger "trx;LogFileName=gate2-protocol.trx"
```

- [ ] **Step 10: Commit**

```powershell
git add infrastructure/GraniteEdgeAI.ModelInspection.WorkerClient tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests tests/IntegrationTests
git commit -m "feat(model-inspection): enforce worker handshake and conversation integrity"
```

**Reviewer gate:** Reject sequential pipe reads, tolerant identity, raw line retention, missing exit cross-check, partial evidence acceptance, path logging, or cleanup that overwrites the first failure.

---

### Task 10: Prove cancellation, timeout, process-tree cleanup, environment, stderr, and concurrency

**Files:**
- Create: `WorkerCancellationCoordinator.cs`; modify client/session/stderr collector.
- Create integration tests: `WorkerCancellationAndTimeoutTests.cs`, `WorkerProcessTreeContainmentTests.cs`, `WorkerEnvironmentAndHandleTests.cs`, `WorkerStandardErrorTests.cs`, `WorkerConcurrencyTests.cs`.

**Interfaces:**
- Produces final Gate 2 runtime behaviour and real abnormal-process evidence.

- [ ] **Step 1: Write cancellation semantics tests**

Required outcomes:

```text
before StartSent -> cleanup + OperationCanceledException, no terminal claim
after StartSent + valid Cancelled + exit 3 -> trusted Cancelled, ForcedTermination=false
ignored cancel -> wait grace, TerminateJobObject, worker_cancellation_forced, ForcedTermination=true
overall timeout -> primary worker_overall_timeout, request cancel, force if needed without replacing primary
```

Tests inject short values; production defaults remain exact protocol values.

- [ ] **Step 2: Implement one cancel and three independent clocks**

`WorkerCancellationCoordinator` uses `Interlocked.Exchange` and writes at most one matching `WorkerCancelInspectionCommand`, never before start. Use separate startup, overall, and grace CTS instances; do not reuse an expired token for cleanup.

- [ ] **Step 3: Implement complete-job termination and zero proof**

```csharp
public async Task TerminateAndVerifyEmptyAsync(
    uint forcedExitCode, TimeSpan timeout, CancellationToken token)
{
    if (Job.GetActiveProcessCount() > 0) Job.Terminate(forcedExitCode);
    await WaitForRootExitAsync(timeout, token).ConfigureAwait(false);
    if (!await Job.WaitUntilEmptyAsync(timeout, token).ConfigureAwait(false))
        throw WorkerClientPolicyException.For(
            WorkerClientFailureCodes.WorkerProcessTreeIntegrityFailed,
            "Worker process tree did not become empty.");
}
```

`WaitUntilEmptyAsync` may use named 25 ms bounded polling of `QueryInformationJobObject`; this queried state is the correctness signal. Close Job Object last as kill-on-close safeguard.

- [ ] **Step 4: Prove root exit with live child**

Fixture emits child PID then root exits. Client observes non-zero active count, performs/report integrity cleanup, terminates job, and verifies child PID gone plus active count zero. Root exit alone cannot pass.

- [ ] **Step 5: Prove environment and handle minimisation**

Fixture reports key names only and booleans for diagnostic-disabled values. Assert required Windows keys present; `PATH`, secrets, request/model keys, and diagnostic ports absent; `DOTNET_EnableDiagnostics`/`COMPlus_EnableDiagnostics` are 0. Probe unrelated inheritable handle absent while stdin/out/err remain valid.

- [ ] **Step 6: Prove stderr drainage/redaction**

Flood at least four times retained limit while valid protocol completes. Assert no deadlock; result returned; retained UTF-8 <= 256 KiB; truncation true; EOF observed; injected path/GUID/token/secret absent; fixed redaction marker present; raw stderr absent from normal CI console.

- [ ] **Step 7: Prove session isolation**

Run at least two sessions concurrently with different request IDs. Assert distinct PIDs/jobs; progress and stderr never mix; cancelling one does not affect the other; cleaning one job does not kill the other; both jobs end at zero.

- [ ] **Step 8: Repeat process suite five times**

```powershell
dotnet test "tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.csproj" -c Release --logger "trx;LogFileName=gate2-process.trx"
1..5 | ForEach-Object {
  dotnet test "tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.csproj" -c Release --no-build --logger "trx;LogFileName=gate2-process-repeat-$_.trx"
  if ($LASTEXITCODE -ne 0) { throw "Gate 2 repetition $_ failed." }
}
```

After each run, assert no worker/fixture PID remains.

- [ ] **Step 9: Run all focused/regression suites**

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests/GraniteEdgeAI.ModelInspection.Transport.Tests.csproj" -c Release
dotnet test "tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/GraniteEdgeAI.ModelInspection.Worker.Tests.csproj" -c Release
dotnet test "tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests.csproj" -c Release
dotnet test "tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" -c Release
```

- [ ] **Step 10: Commit**

```powershell
git add infrastructure/GraniteEdgeAI.ModelInspection.WorkerClient tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests
git commit -m "feat(model-inspection): contain cancellation timeout and process trees"
```

**Reviewer gate:** Reject one shared timeout, forced kill as Cancelled, root-only cleanup, abandoned stderr tasks, arbitrary unbounded sleeps, mixed sessions, or surviving processes.

---

### Task 11: Add architecture fitness, hosted CI, documentation, evidence, and review closure

**Files:**
- Create: `Gate2ArchitectureFitnessTests.cs`; modify `BuildWorkflowContractTests.cs`.
- Modify: `.github/workflows/build-and-test.yml`.
- Create READMEs for Transport, workers root/worker, infrastructure root/client, and fixture.
- Modify after executable proof: Contracts README, ADR-003, umbrella worker integration spec.
- Create after exact hosted CI: `docs/testing/evidence/2026-08-05-model-inspection-worker-gate2-verification.md`; update evidence index.

**Interfaces:**
- Produces executable governance and immutable verification evidence; adds no runtime API.

- [ ] **Step 1: Write red architecture/workflow tests**

Tests inspect projects and production sources. Reject LLamaSharp, Microsoft.WindowsAppSDK, TurboQuant, fixture references, `HttpListener`, `TcpListener`, `Socket.Listen`, `NamedPipeServerStream`, production `Process.Start(`, scenario tokens, non-x64 worker/client, fixture in WinUI output, missing `JOB_LIST`/`HANDLE_LIST`, and workflow omission of any Gate 2 project/TRX.

```csharp
[TestMethod]
public void ProductionHasNoForbiddenDependencyOrListener()
{
    string text = RepositorySource.ReadProductionGate2();
    foreach (string token in new[] {
        "LLamaSharp", "Microsoft.WindowsAppSDK", "ProtocolTestWorker",
        "HttpListener", "TcpListener", "Socket.Listen", "NamedPipeServerStream" })
        Assert.IsFalse(text.Contains(token, StringComparison.OrdinalIgnoreCase), token);
}
```

- [ ] **Step 2: Run red and commit tests**

```powershell
dotnet test "tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" -c Release --filter "FullyQualifiedName~Gate2ArchitectureFitnessTests|FullyQualifiedName~BuildWorkflowContractTests"
git add tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests
git commit -m "test(model-inspection): define Gate 2 architecture fitness gates"
```

Expected: workflow checks fail until CI is updated.

- [ ] **Step 3: Extend sparse checkout**

```yaml
sparse-checkout: |
  .github
  global.json
  IBM Granite with TurboQuant (Intel)
  IBM Granite with TurboQuant (Intel).slnx
  shared
  workers
  infrastructure
  tests
```

Retain every Gate 1 build/test step.

- [ ] **Step 4: Restore/build/publish x64 test inputs**

Add explicit restore/build for Transport, Worker, WorkerClient, fixture, and process tests. Publish worker and fixture framework-dependent to separate `artifacts/gate2/worker` and `artifacts/gate2/fixture` roots. This is deterministic process-test input, not Gate 4 MSIX closure.

- [ ] **Step 5: Add separate TRX test steps**

```yaml
- run: dotnet test "tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests/GraniteEdgeAI.ModelInspection.Transport.Tests.csproj" -c Release --logger "trx;LogFileName=gate2-transport.trx" --results-directory "TestResults/Gate2Transport"
- run: dotnet test "tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/GraniteEdgeAI.ModelInspection.Worker.Tests.csproj" -c Release --logger "trx;LogFileName=gate2-worker.trx" --results-directory "TestResults/Gate2Worker"
- run: dotnet test "tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests.csproj" -c Release --logger "trx;LogFileName=gate2-client.trx" --results-directory "TestResults/Gate2Client"
- run: dotnet test "tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.csproj" -c Release -r win-x64 --logger "trx;LogFileName=gate2-process.trx" --results-directory "TestResults/Gate2Process"
```

Pass controlled publish roots through `GRANITE_GATE2_WORKER_ROOT` and `GRANITE_GATE2_FIXTURE_ROOT`; no model path/secret.

- [ ] **Step 6: Add always-run orphan check**

```powershell
$names = @('GraniteEdgeAI.ModelInspection.Worker','GraniteEdgeAI.ModelInspection.ProtocolTestWorker')
$remaining = Get-Process -ErrorAction SilentlyContinue | Where-Object { $names -contains $_.ProcessName }
if ($remaining) {
  $remaining | Format-Table Id, ProcessName, StartTime
  $remaining | Stop-Process -Force -ErrorAction SilentlyContinue
  throw 'Gate 2 left a process running.'
}
```

Cleanup prevents runner pollution but the job remains failed.

- [ ] **Step 7: Upload safe evidence**

Upload TRX/XML/safe logs and production worker test publish closure. Do not present fixture as product output. Scan retained evidence for paths, secrets, environment values, prompts, health data, and raw protocol lines.

- [ ] **Step 8: Run full local closure**

```powershell
dotnet --version # expected 8.0.419
dotnet restore "IBM Granite with TurboQuant (Intel).slnx"
dotnet build "IBM Granite with TurboQuant (Intel).slnx" -c Release -p:Platform=x64 --no-restore
dotnet test "tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" -c Release
dotnet test "tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests/GraniteEdgeAI.ModelInspection.Transport.Tests.csproj" -c Release
dotnet test "tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/GraniteEdgeAI.ModelInspection.Worker.Tests.csproj" -c Release
dotnet test "tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests.csproj" -c Release
dotnet test "tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.csproj" -c Release -r win-x64
```

Also execute every pre-existing app/package test command from the workflow. Exact counts are recorded only from hosted closure CI.

- [ ] **Step 9: Write proven READMEs and reconcile decisions**

Document purpose, dependency boundaries, lifecycle/security invariants, test commands, evidence link, and non-claims. After tests prove it, update ADR-003/umbrella spec from “Job Object deferred” to creation-time containment while preserving decision history. Do not claim LLamaSharp, real model, UI, package, x86/ARM64, GPU, Hardware Fit, chat, or TurboQuant.

- [ ] **Step 10: Commit workflow/documentation and push**

```powershell
git add .github/workflows/build-and-test.yml tests/ContractTests shared workers infrastructure docs/architecture docs/superpowers/specs
git commit -m "ci(model-inspection): verify Gate 2 worker containment"
git push origin feature/model-inspection-worker-host
```

- [ ] **Step 11: Record exact hosted evidence**

Capture commit SHA, run/job IDs and URLs, pass/fail/skip counts by project, warnings/errors, artifact ID/name/size/SHA-256, orphan result, and privacy scan. Create evidence sections:

```text
Scope/non-claims; source identity; runner/SDK; build/publish;
contracts/fitness; transport; worker; client; real process;
crash/hang/cancel/timeout/orphan; environment/handle/path/privacy;
artifact identity; warnings; Definition of Done; Gate 3 decision.
```

Commit evidence/index, push, then require a final full CI run on that documentation head. Repeat evidence metadata only until document and successful exact head agree.

- [ ] **Step 12: Invoke final Superpowers gates**

Use `superpowers:verification-before-completion`, then `superpowers:requesting-code-review`. Review atomic Job assignment, handle allowlist, resource failure paths, stream bounds/deadlock resistance, cancellation semantics, zero-active-process proof, fixture isolation, first-failure integrity, privacy, and code/spec/ADR/README/evidence agreement.

- [ ] **Step 13: Update draft PR and keep it draft**

Title: `feat(model-inspection): build hardened Gate 2 worker boundary`.

Body explains exact stacked base, purpose, data flow, Job/handle hardening, production-vs-fixture separation, all tests/results/links, warnings, non-claims, and Gate 3 block. Do not mark ready or merge without explicit user instruction.

**Reviewer gate:** Any unchecked Definition of Done item, surviving process, uncontained path, fixture in app output, or evidence/head mismatch blocks Gate 3.

---

## Verification Closure Properties

```text
All commands exit 0.
All TRX files report failed = 0.
No worker/fixture/child process remains.
No Gate 2 production project references LLamaSharp, Windows App SDK, or fixture.
No listener implementation exists.
Production worker returns controlled OperationalFailure with no evidence.
Hosted CI succeeds on the exact evidence head.
```

## Textbook and Project Basis

- **Fundamentals of Software Architecture, 2nd edition:** component boundaries, dependency direction, architecture characteristics, and fitness functions.
- **Designing Secure Software:** trust boundaries, least authority, fail-closed launch, untrusted input, security testing, and residual risks.
- **Code Complete, 2nd edition:** defensive programming, explicit state, focused classes, resource ownership, incremental construction, and evidence-backed debugging.
- **The Art of Unit Testing, 3rd edition:** engine seam, deterministic substitutes, isolated tests, and separation of unit/process tests.
- **Why Programs Fail, 2nd edition:** reproducible abnormal scenarios, first-failure preservation, and diagnostic evidence.
- **Refactoring, 2nd edition:** small test-backed changes and focused extraction without unrelated restructuring.
- **Engineering Software Products:** reliability/security requirements, automated regression, CI evidence, and reviewable changes.
- **`windows-apps.pdf`:** WinUI remains presentation-only; Windows process infrastructure stays outside pages/ViewModels and asynchronous work must not block the UI/STA thread.
- **Official Microsoft sources named in the approved specification:** `CreateProcessW`, `STARTUPINFOEX`, process-thread attributes, Job Objects, handle inheritance, UTF-8, redirected streams, and .NET diagnostics.
