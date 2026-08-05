# Model Inspection Worker Gate 1 — Contracts and Protocol Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Establish the production Model Inspection data contracts, bounded/versioned worker protocol, immutable Model Import handoff, architecture record, CI gate, and source-adjacent documentation without introducing the worker executable or LLamaSharp production code yet.

**Architecture:** Create one pure `net8.0` shared contract library used later by both the WinUI app and worker, plus a pure Microsoft.Testing.Platform contract-test project. Keep application use-case contracts inside the Model Inspection feature. Migrate the current path-only navigation to an immutable request in one green change while preserving the existing page presentation. Define protocol serialization and sequence validation before any production process host exists.

**Tech Stack:** C# 12 / .NET 8 target, repository SDK `10.0.301`, `System.Text.Json`, MSTest `4.3.2`, Microsoft.Testing.Platform, WinUI 3 / Windows App SDK `2.2.0`, GitHub Actions on `windows-latest`.

## Global Constraints

- Work only on `feature/model-inspection-runtime-integration`, stacked on `feature/model-inspection` commit `c3276d50fe39ff0db8ae679c236a5d02cf21fe14`.
- Protocol version is exactly `1`.
- Worker identity is exactly `GraniteEdgeAI.ModelInspection.Worker`.
- First runtime profile is exactly `llamasharp-0.27.0-cpu-win-x64-vocab-only-v1`.
- Maximum command/message line is exactly `1 MiB` of UTF-8 bytes.
- Maximum retained stderr is exactly `256 KiB` of UTF-8 bytes.
- Startup timeout is exactly `5 seconds`; overall inspection timeout is `5 minutes`; cooperative-cancellation grace is `5 seconds`.
- Protocol JSON is compact UTF-8, camel-case, string-enum, one object per line, with unknown additive fields tolerated and duplicate properties rejected.
- `hello` is connection-scoped and has no request ID.
- Every request-scoped message uses one non-empty GUID request ID.
- Only cooperative worker completion may become `Cancelled`; forced termination is an operational failure in later gates.
- The shared contract project must reference no WinUI, LLamaSharp, worker, application-service, or TurboQuant package/project.
- The WinUI application project must remain free from LLamaSharp, LLamaSharp backend, TurboQuant, worker-project, and feasibility-spike references.
- No canonical model path, complete chat-template text, model bytes, prompt/health data, environment variables, native pointers, or native handles may appear in output evidence contracts.
- The first production boundary is explicitly x64-only; this gate defines that identity but does not package or launch a worker.
- Existing path-only navigation is replaced only through a complete request mapping with regression tests in the same task.
- Every production behaviour begins with a focused failing test and ends with focused, project-wide, and CI verification.
- Every new responsibility folder receives a README, and no README claims executable support before evidence exists.

---

## Locked File Structure

### New shared contracts

```text
shared/
├── README.md
└── GraniteEdgeAI.ModelInspection.Contracts/
    ├── README.md
    ├── GraniteEdgeAI.ModelInspection.Contracts.csproj
    ├── Evidence/
    │   ├── WorkerChatTemplateEvidence.cs
    │   ├── WorkerInspectionEvidence.cs
    │   ├── WorkerModelConfigurationEvidence.cs
    │   ├── WorkerModelFileEvidence.cs
    │   ├── WorkerObservation.cs
    │   ├── WorkerRuntimeIdentity.cs
    │   └── WorkerTokenizerEvidence.cs
    └── Protocol/
        ├── WorkerCancelInspectionCommand.cs
        ├── WorkerCommandKind.cs
        ├── WorkerCommandSequenceValidator.cs
        ├── WorkerCompletedMessage.cs
        ├── WorkerCompletionStatus.cs
        ├── WorkerExpectedFileIdentity.cs
        ├── WorkerHelloMessage.cs
        ├── WorkerMessageKind.cs
        ├── WorkerMessageSequenceValidator.cs
        ├── WorkerOperationalFailure.cs
        ├── WorkerProgressMessage.cs
        ├── WorkerProtocol.cs
        ├── WorkerProtocolException.cs
        ├── WorkerProtocolJson.cs
        ├── WorkerQuickScanSnapshot.cs
        ├── WorkerStage.cs
        ├── WorkerStageStatus.cs
        ├── WorkerStartInspectionCommand.cs
        └── WorkerStartedMessage.cs
```

### New pure contract tests

```text
tests/ContractTests/
└── GraniteEdgeAI.ModelInspection.Contracts.Tests/
    ├── GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj
    ├── ContractGraphTests.cs
    ├── Protocol/
    │   ├── WorkerCommandSequenceValidatorTests.cs
    │   ├── WorkerMessageSequenceValidatorTests.cs
    │   ├── WorkerProtocolJsonTests.cs
    │   └── WorkerProtocolTests.cs
    └── TestJson.cs
```

### New application contracts

```text
IBM Granite with TurboQuant (Intel)/Features/ModelInspection/
└── Contracts/
    ├── README.md
    ├── ExpectedModelFileIdentity.cs
    ├── ModelInspectionEnums.cs
    ├── ModelInspectionEvidence.cs
    ├── ModelInspectionExecutionResult.cs
    ├── ModelInspectionFinding.cs
    ├── ModelInspectionOperationalFailure.cs
    ├── ModelInspectionProgress.cs
    ├── ModelInspectionRequest.cs
    ├── ModelInspectionResult.cs
    ├── ModelInspectionRuntimeIdentity.cs
    └── ValidatedQuickScanSnapshot.cs
```

### New Model Import request mapper

```text
IBM Granite with TurboQuant (Intel)/Features/ModelImport/
└── ModelInspectionRequestFactory.cs
```

### Documentation and CI

```text
docs/architecture/decisions/ADR-003-protected-model-inspection-worker.md
docs/architecture/decisions/README.md
docs/testing/evidence/2026-08-05-model-inspection-worker-gate1-verification.md
.github/workflows/build-and-test.yml
```

---

### Task 1: Add the pure contract and contract-test project shells

**Files:**
- Create: `shared/README.md`
- Create: `shared/GraniteEdgeAI.ModelInspection.Contracts/README.md`
- Create: `shared/GraniteEdgeAI.ModelInspection.Contracts/GraniteEdgeAI.ModelInspection.Contracts.csproj`
- Create: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj`
- Create: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/ContractGraphTests.cs`

**Interfaces:**
- Produces project `GraniteEdgeAI.ModelInspection.Contracts` targeting `net8.0`.
- Produces MTP test project `GraniteEdgeAI.ModelInspection.Contracts.Tests` referencing only the shared project.
- Later tasks rely on namespace `GraniteEdgeAI.ModelInspection.Contracts`.

- [ ] **Step 1: Add the failing dependency-boundary test**

Create `ContractGraphTests.cs`:

```csharp
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests;

[TestClass]
[TestCategory("Contract")]
public sealed class ContractGraphTests
{
    [TestMethod]
    public void ContractAssembly_ReferencesOnlyFrameworkAssemblies()
    {
        Assembly assembly = typeof(WorkerProtocol).Assembly;
        string[] references = assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.IsFalse(references.Any(name =>
            name.Contains("Microsoft.UI.Xaml", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("LLamaSharp", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("TurboQuant", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("GraniteEdgeAI.ModelInspection.Worker", StringComparison.OrdinalIgnoreCase)));
    }
}
```

The test intentionally references `WorkerProtocol`, which does not yet exist.

- [ ] **Step 2: Create project files and verify the test fails for the intended reason**

Create the contract project:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
</Project>
```

Create the test project:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
    <TestingPlatformShowTestsFailure>true</TestingPlatformShowTestsFailure>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.8.1" />
    <PackageReference Include="MSTest.TestAdapter" Version="4.3.2" />
    <PackageReference Include="MSTest.TestFramework" Version="4.3.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\shared\GraniteEdgeAI.ModelInspection.Contracts\GraniteEdgeAI.ModelInspection.Contracts.csproj" />
  </ItemGroup>
</Project>
```

Run:

```powershell
dotnet restore `
  "tests\ContractTests\GraniteEdgeAI.ModelInspection.Contracts.Tests\GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj"

dotnet test `
  "tests\ContractTests\GraniteEdgeAI.ModelInspection.Contracts.Tests\GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" `
  --configuration Release `
  --no-restore `
  --filter "TestCategory=Contract" `
  --minimum-expected-tests 1
```

Expected: compilation fails because `WorkerProtocol` does not exist. Preserve the compiler output in the task notes.

- [ ] **Step 3: Add the minimal protocol identity type**

Create `Protocol/WorkerProtocol.cs`:

```csharp
namespace GraniteEdgeAI.ModelInspection.Contracts;

public static class WorkerProtocol
{
    public const int Version = 1;
    public const string WorkerId = "GraniteEdgeAI.ModelInspection.Worker";
    public const string RuntimeProfile =
        "llamasharp-0.27.0-cpu-win-x64-vocab-only-v1";

    public const int MaximumMessageBytes = 1024 * 1024;
    public const int MaximumRetainedStandardErrorBytes = 256 * 1024;

    public static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan OverallTimeout = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan CancellationGracePeriod =
        TimeSpan.FromSeconds(5);
}
```

- [ ] **Step 4: Run the focused test and project build**

```powershell
dotnet test `
  "tests\ContractTests\GraniteEdgeAI.ModelInspection.Contracts.Tests\GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" `
  --configuration Release `
  --no-restore `
  --filter "FullyQualifiedName~ContractGraphTests" `
  --minimum-expected-tests 1

dotnet build `
  "shared\GraniteEdgeAI.ModelInspection.Contracts\GraniteEdgeAI.ModelInspection.Contracts.csproj" `
  --configuration Release `
  --no-restore
```

Expected: both pass with zero warnings and zero errors.

- [ ] **Step 5: Write source-adjacent README boundaries**

`shared/README.md` must explain that `shared/` contains framework-neutral contracts shared across process/project boundaries and may not contain application pages, native runtimes, or executable behaviour.

`shared/GraniteEdgeAI.ModelInspection.Contracts/README.md` must record:

```text
Status: project shell and protocol identity only
Depends on: .NET base class library / System.Text.Json when added
Forbidden: WinUI, LLamaSharp, TurboQuant, worker implementation, application service
Verified: focused contract graph test and Release build only
Deferred: messages, validators, worker host, process adapter, packaging
```

- [ ] **Step 6: Commit**

```powershell
git add shared tests/ContractTests
git commit -m "test(model-inspection): establish pure worker contract boundary"
```

---

### Task 2: Define protocol enums, evidence records, and message contracts

**Files:**
- Create all files under `shared/GraniteEdgeAI.ModelInspection.Contracts/Evidence/`
- Create enum/value files under `shared/GraniteEdgeAI.ModelInspection.Contracts/Protocol/`
- Create: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/Protocol/WorkerProtocolTests.cs`

**Interfaces:**
- Produces the exact worker command/message/evidence record types named in the locked file structure.
- All records use init-only properties and contain no behaviour beyond invariant validation methods.
- `WorkerCompletedMessage.Validate()` guarantees status/evidence/failure consistency.

- [ ] **Step 1: Write failing protocol-identity and invariant tests**

Create `WorkerProtocolTests.cs` with tests including:

```csharp
[TestMethod]
public void WorkerProtocol_UsesApprovedIdentityAndLimits()
{
    Assert.AreEqual(1, WorkerProtocol.Version);
    Assert.AreEqual(
        "GraniteEdgeAI.ModelInspection.Worker",
        WorkerProtocol.WorkerId);
    Assert.AreEqual(
        "llamasharp-0.27.0-cpu-win-x64-vocab-only-v1",
        WorkerProtocol.RuntimeProfile);
    Assert.AreEqual(1024 * 1024, WorkerProtocol.MaximumMessageBytes);
    Assert.AreEqual(
        256 * 1024,
        WorkerProtocol.MaximumRetainedStandardErrorBytes);
}

[TestMethod]
public void CompletedStatus_RequiresEvidenceAndNoOperationalFailure()
{
    var message = new WorkerCompletedMessage
    {
        ProtocolVersion = WorkerProtocol.Version,
        MessageType = WorkerMessageKind.Completed,
        RequestId = Guid.NewGuid(),
        CompletionStatus = WorkerCompletionStatus.Completed,
        Evidence = null,
        OperationalFailure = null
    };

    Assert.ThrowsExactly<WorkerProtocolException>(message.Validate);
}

[TestMethod]
public void OperationalFailureStatus_RequiresFailureAndNoEvidence()
{
    var message = new WorkerCompletedMessage
    {
        ProtocolVersion = WorkerProtocol.Version,
        MessageType = WorkerMessageKind.Completed,
        RequestId = Guid.NewGuid(),
        CompletionStatus = WorkerCompletionStatus.OperationalFailure,
        Evidence = new WorkerInspectionEvidence(),
        OperationalFailure = null
    };

    Assert.ThrowsExactly<WorkerProtocolException>(message.Validate);
}
```

Add companion tests for:

- hello has non-empty worker ID/version/runtime profile and positive process ID;
- start command requires non-empty request ID, positive parent PID, parent start time, absolute path, positive expected length, and GGUF snapshot;
- cancel requires non-empty request ID;
- progress requires valid stage/count/fraction;
- completed/cancelled/operational-failure invariants;
- unavailable structural values remain nullable;
- chat-template contract has only `Present`, `LengthCharacters`, and `Sha256`.

- [ ] **Step 2: Run focused tests and verify compilation fails**

```powershell
dotnet test `
  "tests\ContractTests\GraniteEdgeAI.ModelInspection.Contracts.Tests\GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" `
  --configuration Release `
  --no-restore `
  --filter "FullyQualifiedName~WorkerProtocolTests"
```

Expected: FAIL because the contract types are missing.

- [ ] **Step 3: Implement exact enums**

Use these enum members:

```csharp
public enum WorkerCommandKind
{
    StartInspection,
    CancelInspection
}

public enum WorkerMessageKind
{
    Hello,
    Started,
    Progress,
    Completed
}

public enum WorkerCompletionStatus
{
    Completed,
    Cancelled,
    OperationalFailure
}

public enum WorkerStage
{
    CheckModelPackage = 1,
    ReadModelConfiguration = 2,
    ValidateTokenizerAndChatSetup = 3,
    ValidateModelStructure = 4,
    ConfirmCoreRuntimeCompatibility = 5
}

public enum WorkerStageStatus
{
    Active,
    Completed,
    Warning,
    Failed,
    Cancelled
}
```

- [ ] **Step 4: Implement exact request-support records**

```csharp
public sealed record WorkerExpectedFileIdentity
{
    public long LengthBytes { get; init; }
    public DateTimeOffset LastWriteTimeUtc { get; init; }
}

public sealed record WorkerQuickScanSnapshot
{
    public string Format { get; init; } = string.Empty;
    public string ModelName { get; init; } = string.Empty;
    public string Architecture { get; init; } = string.Empty;
    public string? ParameterSizeLabel { get; init; }
    public string? Quantisation { get; init; }
    public long FileSizeBytes { get; init; }
    public ulong? DeclaredContextLength { get; init; }
    public uint GgufVersion { get; init; }
}
```

- [ ] **Step 5: Implement exact message records and validation**

Use these signatures:

```csharp
public sealed record WorkerHelloMessage
{
    public int ProtocolVersion { get; init; }
    public WorkerMessageKind MessageType { get; init; }
    public string WorkerId { get; init; } = string.Empty;
    public string WorkerVersion { get; init; } = string.Empty;
    public int WorkerProcessId { get; init; }
    public string RuntimeProfile { get; init; } = string.Empty;
    public string ProcessArchitecture { get; init; } = string.Empty;
    public void Validate();
}

public sealed record WorkerStartInspectionCommand
{
    public int ProtocolVersion { get; init; }
    public WorkerCommandKind CommandType { get; init; }
    public Guid RequestId { get; init; }
    public int ParentProcessId { get; init; }
    public DateTimeOffset ParentProcessStartTimeUtc { get; init; }
    public string ModelPath { get; init; } = string.Empty;
    public WorkerExpectedFileIdentity ExpectedFileIdentity { get; init; } = new();
    public WorkerQuickScanSnapshot QuickScan { get; init; } = new();
    public void Validate();
}

public sealed record WorkerCancelInspectionCommand
{
    public int ProtocolVersion { get; init; }
    public WorkerCommandKind CommandType { get; init; }
    public Guid RequestId { get; init; }
    public void Validate();
}

public sealed record WorkerStartedMessage
{
    public int ProtocolVersion { get; init; }
    public WorkerMessageKind MessageType { get; init; }
    public Guid RequestId { get; init; }
    public void Validate();
}

public sealed record WorkerProgressMessage
{
    public int ProtocolVersion { get; init; }
    public WorkerMessageKind MessageType { get; init; }
    public Guid RequestId { get; init; }
    public WorkerStage Stage { get; init; }
    public WorkerStageStatus StageStatus { get; init; }
    public int CompletedStageCount { get; init; }
    public int TotalStageCount { get; init; }
    public double? StageFraction { get; init; }
    public void Validate();
}

public sealed record WorkerCompletedMessage
{
    public int ProtocolVersion { get; init; }
    public WorkerMessageKind MessageType { get; init; }
    public Guid RequestId { get; init; }
    public WorkerCompletionStatus CompletionStatus { get; init; }
    public WorkerInspectionEvidence? Evidence { get; init; }
    public WorkerOperationalFailure? OperationalFailure { get; init; }
    public void Validate();
}
```

`Validate()` methods must throw `WorkerProtocolException` with a stable property-oriented message and must verify the exact expected enum kind/version.

- [ ] **Step 6: Implement evidence records**

Use nullable values for unavailable runtime data. At minimum:

```csharp
public sealed record WorkerRuntimeIdentity
{
    public string WorkerVersion { get; init; } = string.Empty;
    public int ProtocolVersion { get; init; }
    public string RuntimeProfile { get; init; } = string.Empty;
    public string LLamaSharpVersion { get; init; } = string.Empty;
    public string BackendPackageVersion { get; init; } = string.Empty;
    public string MappedLlamaCppCommit { get; init; } = string.Empty;
    public string NativeLibraryName { get; init; } = string.Empty;
    public string ProcessArchitecture { get; init; } = string.Empty;
    public string InspectionMode { get; init; } = string.Empty;
    public bool UsesCuda { get; init; }
    public bool UsesVulkan { get; init; }
    public int GpuLayerCount { get; init; }
}

public sealed record WorkerModelFileEvidence
{
    public string FileName { get; init; } = string.Empty;
    public string CanonicalPathSha256 { get; init; } = string.Empty;
    public long LengthBefore { get; init; }
    public long LengthAfter { get; init; }
    public DateTimeOffset LastWriteTimeBeforeUtc { get; init; }
    public DateTimeOffset LastWriteTimeAfterUtc { get; init; }
    public string Sha256Before { get; init; } = string.Empty;
    public string Sha256After { get; init; } = string.Empty;
    public bool IntegrityPreserved { get; init; }
}

public sealed record WorkerChatTemplateEvidence
{
    public bool Present { get; init; }
    public int? LengthCharacters { get; init; }
    public string? Sha256 { get; init; }
}
```

`WorkerInspectionEvidence` composes runtime, file, configuration, tokenizer, chat-template, and observation collections. It must have no `ModelPath`, `CanonicalPath`, `Text`, `Template`, `Content`, pointer, handle, or XAML property.

- [ ] **Step 7: Run focused and full contract tests**

```powershell
dotnet test `
  "tests\ContractTests\GraniteEdgeAI.ModelInspection.Contracts.Tests\GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" `
  --configuration Release `
  --no-restore `
  --filter "FullyQualifiedName~WorkerProtocolTests" `
  --minimum-expected-tests 12

dotnet test `
  "tests\ContractTests\GraniteEdgeAI.ModelInspection.Contracts.Tests\GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" `
  --configuration Release `
  --no-restore `
  --filter "TestCategory=Contract" `
  --minimum-expected-tests 13
```

Expected: PASS, zero warnings/errors.

- [ ] **Step 8: Commit**

```powershell
git add shared/GraniteEdgeAI.ModelInspection.Contracts tests/ContractTests
git commit -m "feat(model-inspection): define worker protocol contracts"
```

---

### Task 3: Add strict bounded JSON serialization

**Files:**
- Create: `shared/GraniteEdgeAI.ModelInspection.Contracts/Protocol/WorkerProtocolException.cs`
- Create: `shared/GraniteEdgeAI.ModelInspection.Contracts/Protocol/WorkerProtocolJson.cs`
- Create: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/Protocol/WorkerProtocolJsonTests.cs`
- Create: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/TestJson.cs`

**Interfaces:**
- Produces `WorkerProtocolJson.Serialize<T>(T value) : byte[]`.
- Produces `WorkerProtocolJson.DeserializeCommand(ReadOnlySpan<byte>) : object` returning only `WorkerStartInspectionCommand` or `WorkerCancelInspectionCommand`.
- Produces `WorkerProtocolJson.DeserializeMessage(ReadOnlySpan<byte>) : object` returning only approved message records.
- Rejects invalid UTF-8, oversized payloads, non-object roots, duplicate JSON properties at any object depth, missing discriminators, unknown kinds, invalid enum values, and failed record validation.

- [ ] **Step 1: Write failing serializer tests**

Tests must include:

```csharp
[TestMethod]
public void DeserializeMessage_AllowsUnknownAdditiveProperty()
{
    string json = """
        {
          "protocolVersion": 1,
          "messageType": "hello",
          "workerId": "GraniteEdgeAI.ModelInspection.Worker",
          "workerVersion": "1.0.0",
          "workerProcessId": 123,
          "runtimeProfile": "llamasharp-0.27.0-cpu-win-x64-vocab-only-v1",
          "processArchitecture": "X64",
          "futureField": "ignored"
        }
        """;

    object message = WorkerProtocolJson.DeserializeMessage(
        Encoding.UTF8.GetBytes(json));

    Assert.IsInstanceOfType<WorkerHelloMessage>(message);
}

[TestMethod]
public void DeserializeMessage_RejectsDuplicatePropertyAtNestedDepth()
{
    string json = """
        {
          "protocolVersion": 1,
          "messageType": "completed",
          "requestId": "11111111-1111-1111-1111-111111111111",
          "completionStatus": "operationalFailure",
          "operationalFailure": {
            "code": "MI-OP-TEST",
            "code": "MI-OP-OVERRIDE",
            "message": "failed"
          }
        }
        """;

    Assert.ThrowsExactly<WorkerProtocolException>(() =>
        WorkerProtocolJson.DeserializeMessage(Encoding.UTF8.GetBytes(json)));
}

[TestMethod]
public void DeserializeMessage_RejectsPayloadAboveOneMiB()
{
    byte[] bytes = new byte[WorkerProtocol.MaximumMessageBytes + 1];
    Assert.ThrowsExactly<WorkerProtocolException>(() =>
        WorkerProtocolJson.DeserializeMessage(bytes));
}
```

Also cover invalid UTF-8, comments, trailing commas, root arrays, missing discriminator, unknown kind, wrong version, invalid enum, compact camel-case round trip, serialization size enforcement, and no embedded newline in serialized JSON.

- [ ] **Step 2: Run tests and verify failure**

```powershell
dotnet test `
  "tests\ContractTests\GraniteEdgeAI.ModelInspection.Contracts.Tests\GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" `
  --configuration Release `
  --no-restore `
  --filter "FullyQualifiedName~WorkerProtocolJsonTests"
```

Expected: FAIL because `WorkerProtocolJson` does not exist.

- [ ] **Step 3: Implement serializer options and bounded parsing**

`WorkerProtocolJson` must:

1. reject `payload.Length == 0` and `payload.Length > MaximumMessageBytes`;
2. decode using `new UTF8Encoding(false, true)` so invalid UTF-8 throws;
3. parse `JsonDocument` with comments/trailing commas disabled and `MaxDepth = 32`;
4. recursively enumerate every JSON object with `HashSet<string>(StringComparer.Ordinal)` and reject duplicate property names;
5. require root `JsonValueKind.Object`;
6. read the discriminator with exact camel-case property name;
7. deserialize using compact camel-case `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)` and case-sensitive property matching;
8. call the record’s `Validate()` method;
9. wrap `JsonException`, `DecoderFallbackException`, and validation exceptions in `WorkerProtocolException` without including model-path values.

Use:

```csharp
private static readonly JsonSerializerOptions SerializerOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    WriteIndented = false,
    MaxDepth = 32,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
};
```

Add `JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)`.

- [ ] **Step 4: Run focused/full tests and build**

```powershell
dotnet test `
  "tests\ContractTests\GraniteEdgeAI.ModelInspection.Contracts.Tests\GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" `
  --configuration Release `
  --no-restore `
  --filter "FullyQualifiedName~WorkerProtocolJsonTests" `
  --minimum-expected-tests 12

dotnet test `
  "tests\ContractTests\GraniteEdgeAI.ModelInspection.Contracts.Tests\GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" `
  --configuration Release `
  --no-restore `
  --filter "TestCategory=Contract" `
  --minimum-expected-tests 25
```

Expected: PASS with no warnings/errors.

- [ ] **Step 5: Update contract README and commit**

Document exact JSON rules, additive-field compatibility, duplicate-property rejection, and the fact that stream reading is implemented later but must enforce the same byte limit before deserialization.

```powershell
git add shared/GraniteEdgeAI.ModelInspection.Contracts tests/ContractTests
git commit -m "feat(model-inspection): enforce bounded worker JSON protocol"
```

---

### Task 4: Define command and message sequence validators

**Files:**
- Create: `shared/GraniteEdgeAI.ModelInspection.Contracts/Protocol/WorkerCommandSequenceValidator.cs`
- Create: `shared/GraniteEdgeAI.ModelInspection.Contracts/Protocol/WorkerMessageSequenceValidator.cs`
- Create: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/Protocol/WorkerCommandSequenceValidatorTests.cs`
- Create: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/Protocol/WorkerMessageSequenceValidatorTests.cs`

**Interfaces:**
- `WorkerCommandSequenceValidator.AcceptStart(WorkerStartInspectionCommand)`
- `WorkerCommandSequenceValidator.AcceptCancel(WorkerCancelInspectionCommand)`
- `WorkerCommandSequenceValidator.MarkTerminal()`
- `WorkerMessageSequenceValidator.AcceptHello(WorkerHelloMessage)`
- `WorkerMessageSequenceValidator.SetExpectedRequest(Guid)`
- `WorkerMessageSequenceValidator.AcceptStarted(WorkerStartedMessage)`
- `WorkerMessageSequenceValidator.AcceptProgress(WorkerProgressMessage)`
- `WorkerMessageSequenceValidator.AcceptCompleted(WorkerCompletedMessage)`
- Both validators throw `WorkerProtocolException` on invalid transitions and expose read-only current state.

- [ ] **Step 1: Write failing state-machine tests**

Cover:

- cancel before start rejected;
- second start rejected;
- repeated matching cancel accepted/idempotent;
- wrong request ID rejected;
- command after terminal rejected;
- started/progress/completed before hello rejected;
- two hello messages rejected;
- request-scoped output before `SetExpectedRequest` rejected;
- wrong request ID rejected;
- backward stage rejected;
- decreasing completed count rejected;
- duplicate terminal rejected;
- any output after terminal rejected;
- valid hello → request → started → monotonic progress → completed accepted.

Example:

```csharp
[TestMethod]
public void MessageSequence_BackwardStage_Throws()
{
    Guid requestId = Guid.NewGuid();
    var validator = CreateRunningValidator(requestId);

    validator.AcceptProgress(CreateProgress(
        requestId,
        WorkerStage.ValidateModelStructure,
        completed: 3));

    Assert.ThrowsExactly<WorkerProtocolException>(() =>
        validator.AcceptProgress(CreateProgress(
            requestId,
            WorkerStage.ReadModelConfiguration,
            completed: 1)));
}
```

- [ ] **Step 2: Run tests and verify intended failure**

```powershell
dotnet test `
  "tests\ContractTests\GraniteEdgeAI.ModelInspection.Contracts.Tests\GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" `
  --configuration Release `
  --no-restore `
  --filter "FullyQualifiedName~SequenceValidatorTests"
```

Expected: FAIL because validators do not exist.

- [ ] **Step 3: Implement minimal deterministic validators**

Use small internal state enums inside each validator. Do not add process, stream, timer, file, or LLamaSharp responsibilities.

Message validator monotonicity rules:

```text
Current stage numeric value may stay the same or increase.
CompletedStageCount may stay the same or increase.
Started must occur exactly once before progress or completed.
Completed may occur after Started even when no progress event was emitted.
```

- [ ] **Step 4: Run focused and full contract suites**

```powershell
dotnet test `
  "tests\ContractTests\GraniteEdgeAI.ModelInspection.Contracts.Tests\GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" `
  --configuration Release `
  --no-restore `
  --filter "FullyQualifiedName~SequenceValidatorTests" `
  --minimum-expected-tests 16

dotnet test `
  "tests\ContractTests\GraniteEdgeAI.ModelInspection.Contracts.Tests\GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" `
  --configuration Release `
  --no-restore `
  --filter "TestCategory=Contract" `
  --minimum-expected-tests 41
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add shared/GraniteEdgeAI.ModelInspection.Contracts tests/ContractTests
git commit -m "feat(model-inspection): define worker protocol state rules"
```

---

### Task 5: Add application-owned inspection contracts

**Files:**
- Create all files under `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Contracts/`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionContractTests.cs`

**Interfaces:**
- Produces immutable application types in namespace `GraniteEdgeAI.Features.ModelInspection.Contracts`.
- No application contract references shared worker protocol types; later mappers bridge the two domains.
- `ModelInspectionExecutionResult` enforces execution/result invariants through named factory methods.

- [ ] **Step 1: Write failing application contract tests**

Tests cover:

```csharp
[TestMethod]
public void Request_RejectsMismatchedFileName()
{
    Assert.ThrowsExactly<ArgumentException>(() =>
        new ModelInspectionRequest(
            @"C:\Models\granite.gguf",
            "different.gguf",
            new ExpectedModelFileIdentity(100, DateTimeOffset.UtcNow),
            ValidatedQuickScanSnapshot.CreateGguf(
                "Granite", "granite", "3B", "Q4_K_M", 100, 4096, 3)));
}

[TestMethod]
public void CompletedExecution_RequiresClassifiedResult()
{
    Assert.ThrowsExactly<ArgumentNullException>(() =>
        ModelInspectionExecutionResult.Completed(null!));
}

[TestMethod]
public void OnlyReadyOutcomesCanContinueToHardwareFit()
{
    Assert.IsTrue(ModelInspectionResult.CanContinue(
        ModelInspectionOutcome.Ready));
    Assert.IsTrue(ModelInspectionResult.CanContinue(
        ModelInspectionOutcome.ReadyWithWarnings));
    Assert.IsFalse(ModelInspectionResult.CanContinue(
        ModelInspectionOutcome.Unsupported));
}
```

Also verify:

- absolute model path and final filename invariants;
- positive file identity length;
- successful GGUF snapshot invariants;
- `Cancelled` has no model result/failure;
- `OperationalFailure` requires operational failure and no model result;
- `Completed` requires result and no operational failure;
- unavailable evidence remains nullable;
- no worker protocol, LLamaSharp, XAML, page, pointer, handle, path-output, or full-template type/property enters the application evidence graph.

- [ ] **Step 2: Run focused test and verify failure**

Build/run through the existing packaged test project:

```powershell
dotnet build `
  "tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj" `
  --configuration Release `
  --runtime win-x64 `
  -p:Platform=x64
```

Expected: FAIL because application contracts are missing.

- [ ] **Step 3: Implement immutable request/progress/result types**

Use constructor-validated sealed records/classes. Required enums:

```csharp
internal enum ModelInspectionExecutionStatus
{
    Completed,
    Cancelled,
    OperationalFailure
}

internal enum ModelInspectionOutcome
{
    Ready,
    ReadyWithWarnings,
    ConversionRequired,
    IncompletePackage,
    Unsupported,
    Invalid
}

internal enum ModelInspectionStage
{
    CheckModelPackage = 1,
    ReadModelConfiguration = 2,
    ValidateTokenizerAndChatSetup = 3,
    ValidateModelStructure = 4,
    ConfirmCoreRuntimeCompatibility = 5
}

internal enum ModelInspectionFindingSeverity
{
    Information,
    Warning,
    Blocking
}
```

`ModelInspectionExecutionResult` exposes only:

```csharp
internal static ModelInspectionExecutionResult Completed(
    ModelInspectionResult result);
internal static ModelInspectionExecutionResult Cancelled(
    bool cooperative);
internal static ModelInspectionExecutionResult OperationalFailure(
    ModelInspectionOperationalFailure failure);
```

`ModelInspectionResult.CanContinueToHardwareFit` is derived from outcome and cannot be set independently.

- [ ] **Step 4: Run build and packaged focused tests**

Use the same local recipe as CI to build the app/test package, then execute the recipe with `vstest.console.exe`. Filter to `ModelInspectionContractTests` when supported by the runner; otherwise run the complete packaged suite and inspect TRX.

Expected: all existing tests and new contract tests pass.

- [ ] **Step 5: Write `Contracts/README.md` and commit**

README must distinguish:

```text
Application request/result language
versus
worker transport/evidence language
```

It must record that later `WorkerRequestMapper` and `WorkerResultMapper` are the only approved bridges.

```powershell
git add `
  "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Contracts" `
  tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection
git commit -m "feat(model-inspection): add application inspection contracts"
```

---

### Task 6: Replace path-only navigation with the immutable request

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelInspectionRequestFactory.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelInspectionRequestedEventArgs.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelImportPage.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelImportNavigationRequestTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingModelInspectionNavigationTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageNavigationTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelInspectionRequestFactoryTests.cs`

**Interfaces:**
- `ModelInspectionRequestFactory.TryCreate(string modelPath, ModelQuickScanResult scanResult, out ModelInspectionRequest? request) : bool`
- `ModelInspectionRequestedEventArgs.Request : ModelInspectionRequest`
- `OnboardingShellPage.NavigateToModelInspection(ModelInspectionRequest request) : bool`
- `ModelInspectionPage.Request : ModelInspectionRequest?`
- Preserve `ModelInspectionPage.SelectedModelPath` as a derived compatibility property returning `Request?.ModelPath` during this gate.

- [ ] **Step 1: Rewrite navigation tests first**

Change assertions from `capturedRequest.ModelPath` to:

```csharp
Assert.AreEqual(selectedPath, capturedRequest.Request.ModelPath);
Assert.AreEqual("granite", capturedRequest.Request.QuickScan.Architecture);
Assert.AreEqual("Q4_K_M", capturedRequest.Request.QuickScan.Quantisation);
Assert.AreEqual(3U, capturedRequest.Request.QuickScan.GgufVersion);
Assert.AreEqual(
    new FileInfo(selectedPath).Length,
    capturedRequest.Request.ExpectedFileIdentity.LengthBytes);
```

Add tests that:

- delete the model after successful quick scan, then request is rejected and event count stays zero;
- modify the model length after quick scan, then request is rejected;
- navigation passes the same immutable request instance to `ModelInspectionPage`;
- navigation rejects null/invalid request;
- Model Inspection initial card still shows only the filename and existing presentation.

Existing test helpers must create a real small temporary `.gguf` file and set quick-scan `FileSizeBytes` to its actual length; no fake nonexistent `C:\Models` path remains in request-factory tests.

- [ ] **Step 2: Run packaged tests and confirm failures**

Expected failures: event args still exposes path, shell accepts string, page expects string, and factory does not exist.

- [ ] **Step 3: Implement request factory**

`TryCreate` must:

1. reject non-success scan results by caller invariant;
2. canonicalise path;
3. require existing ordinary file;
4. read `FileInfo.Length` and `LastWriteTimeUtc`;
5. require current length equals `scanResult.FileSizeBytes`;
6. copy quick-scan values into `ValidatedQuickScanSnapshot`;
7. return `false` for missing, directory, inaccessible, invalid, or mismatched file without exposing path in diagnostic text;
8. not hash the full model in the UI process.

- [ ] **Step 4: Migrate event/shell/page contracts**

`ModelInspectionRequestedEventArgs` constructor accepts only `ModelInspectionRequest`.

`TryRequestModelInspection()` creates the request. On failure it:

```text
returns false
raises no event
sets HasValidatedModel=false
clears ValidatedScanResult
keeps SelectedModelPath only long enough to show the existing filename
turns Continue off
shows the existing failure card with stable code model-selection-changed
```

The user message is: `The selected model changed after validation. Choose the model again.`

Technical diagnostics contain the stable code and exception type only, never the canonical path.

`ModelInspectionPage.OnNavigatedTo` requires `ModelInspectionRequest`; `SelectedModelPath` becomes:

```csharp
internal string? SelectedModelPath => Request?.ModelPath;
```

- [ ] **Step 5: Run focused and full packaged tests**

Expected: navigation/request tests pass; complete existing packaged test suite passes; no regressions in quick-scan state/cancellation tests.

- [ ] **Step 6: Update feature READMEs and commit**

Update:

- `Features/ModelImport/README.md` — successful selection now creates immutable inspection request.
- `Features/Onboarding/README.md` — shell forwards request, not path.
- `Features/ModelInspection/README.md` — page retains request but remains presentation-only.
- `Features/README.md` — current cross-feature handoff.

```powershell
git add `
  "IBM Granite with TurboQuant (Intel)/Features" `
  tests/UnitTests/GraniteEdgeAI.UnitTests/Features
git commit -m "refactor(model-inspection): pass validated inspection request"
```

---

### Task 7: Record ADR-003 and documentation hierarchy

**Files:**
- Create: `docs/architecture/decisions/ADR-003-protected-model-inspection-worker.md`
- Modify: `docs/architecture/decisions/README.md`
- Modify: `shared/README.md`
- Modify: `shared/GraniteEdgeAI.ModelInspection.Contracts/README.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/README.md`

**Interfaces:**
- ADR records accepted architecture decision, not implementation proof.
- README statuses must match current Gate 1 evidence only.

- [ ] **Step 1: Write ADR-003**

Required sections:

```text
Status / date / owner / affected features / related ADRs/spec
Context
Decision
Why selected
Alternatives considered
Consequences
Protocol and dependency boundaries
x64-only first boundary
Chat llama-cli separation
Review triggers
Non-claims
```

Decision text must say:

```text
Model Inspection native work runs in a dedicated short-lived worker process.
The WinUI application communicates over bounded JSON lines on redirected standard streams.
The worker returns technical evidence; application code classifies outcomes.
Chat later uses a separate pinned llama-cli process.
```

- [ ] **Step 2: Update ADR index**

Add:

```markdown
| [ADR-003](ADR-003-protected-model-inspection-worker.md) | Accepted | Run LLamaSharp/native Model Inspection in a dedicated x64 worker using bounded port-free standard-stream JSON; keep model classification in application code and chat on a separate pinned `llama-cli` route |
```

- [ ] **Step 3: Documentation contradiction review**

Search for and correct current statements that still claim:

- worker isolation is deferred;
- direct in-process LLamaSharp is the production plan;
- navigation carries only a path;
- Invalid and Incomplete are one outcome.

Do not rewrite historical plans; add a supersession note where history must remain intact.

- [ ] **Step 4: Commit**

```powershell
git add docs/architecture shared "IBM Granite with TurboQuant (Intel)/Features"
git commit -m "docs(model-inspection): accept protected worker architecture"
```

---

### Task 8: Add Gate 1 to hosted CI

**Files:**
- Modify: `.github/workflows/build-and-test.yml`

**Interfaces:**
- Existing packaged WinUI build/test remains unchanged in meaning.
- Adds a pure contract test gate before WinUI restore/build.
- Sparse checkout includes `shared` and `tests/ContractTests`.

- [ ] **Step 1: Add a source-contract regression test before workflow modification**

Add a deterministic test in the contract project that reads `.github/workflows/build-and-test.yml` from repository root and requires:

```text
shared

tests/ContractTests

CONTRACT_TEST_PROJECT

Run Model Inspection contract tests

--minimum-expected-tests 41
```

The test locates repository root by walking upward until both `global.json` and `.github/workflows/build-and-test.yml` exist.

- [ ] **Step 2: Run the focused test and verify red state**

Expected: FAIL because CI does not yet include the project.

- [ ] **Step 3: Update workflow**

Add environment variable:

```yaml
CONTRACT_TEST_PROJECT: 'tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj'
```

Add sparse paths:

```yaml
shared
tests/ContractTests
```

Add steps after tool versions and before WinUI restore:

```yaml
- name: Restore Model Inspection contract tests
  run: |
    dotnet restore "$env:CONTRACT_TEST_PROJECT"
    if ($LASTEXITCODE -ne 0) {
      throw "Model Inspection contract restore failed: $LASTEXITCODE"
    }

- name: Run Model Inspection contract tests
  run: |
    dotnet test "$env:CONTRACT_TEST_PROJECT" `
      --configuration Release `
      --no-restore `
      --filter "TestCategory=Contract" `
      --minimum-expected-tests 41
    if ($LASTEXITCODE -ne 0) {
      throw "Model Inspection contract tests failed: $LASTEXITCODE"
    }
```

- [ ] **Step 4: Run contract suite and YAML/static checks**

```powershell
dotnet test `
  "tests\ContractTests\GraniteEdgeAI.ModelInspection.Contracts.Tests\GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" `
  --configuration Release `
  --no-restore `
  --filter "TestCategory=Contract" `
  --minimum-expected-tests 41

git diff --check
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add .github/workflows/build-and-test.yml tests/ContractTests
git commit -m "ci(model-inspection): run worker contract gate"
```

---

### Task 9: Execute full Gate 1 verification and record evidence

**Files:**
- Create: `docs/testing/evidence/2026-08-05-model-inspection-worker-gate1-verification.md`
- Modify: `docs/testing/evidence/README.md`
- Modify: PR #45 body/checklist

**Interfaces:**
- Evidence must identify exact tested head, commands, test counts, build results, CI run IDs, and explicit non-claims.

- [ ] **Step 1: Run local verification from a clean branch**

```powershell
git status --short

dotnet restore `
  "tests\ContractTests\GraniteEdgeAI.ModelInspection.Contracts.Tests\GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj"

dotnet test `
  "tests\ContractTests\GraniteEdgeAI.ModelInspection.Contracts.Tests\GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" `
  --configuration Release `
  --no-restore `
  --filter "TestCategory=Contract" `
  --minimum-expected-tests 41

msbuild `
  "IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj" `
  /target:Restore,Build `
  /property:Configuration=Release `
  /property:Platform=x64 `
  /property:RuntimeIdentifier=win-x64 `
  /property:PublishProfile= `
  /property:PublishTrimmed=false `
  /property:PublishReadyToRun=false `
  /property:AppxPackageSigningEnabled=false `
  /property:GenerateAppxPackageOnBuild=false

dotnet build `
  "tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj" `
  --configuration Release `
  --runtime win-x64 `
  -p:Platform=x64

git diff --check
```

Run the complete packaged WinUI test recipe through `vstest.console.exe` using the same commands as `.github/workflows/build-and-test.yml`.

- [ ] **Step 2: Review current-head GitHub Actions**

Required:

```text
Build and test → success
Model Inspection contract step → success
WinUI application build → success
Packaged unit/UI-thread tests → success
```

Do not record a pass while any exact-head run is queued, in progress, cancelled, skipped, or failed.

- [ ] **Step 3: Write evidence record**

Required evidence sections:

```text
Exact branch/head
Scope
Contract test count and result
Application build result
Packaged test count/result
Navigation request verification
Dependency graph result
Protocol JSON/state-machine result
README/ADR status
Security/privacy checks
CI run IDs
Explicit non-claims
Next gate
```

Non-claims include worker executable, process launch, LLamaSharp extraction, real-model production-worker execution, packaging, classifier/service, ViewModel, functional Cancel, Hardware Fit, and chat.

- [ ] **Step 4: Whole-gate review**

Review:

- all new public/internal types for single responsibility;
- duplicate domain concepts;
- nullable/invariant consistency;
- JSON ambiguity and leaks;
- app/worker dependency direction;
- navigation stale-file behaviour;
- README claims versus evidence;
- test gaps;
- `git diff --check`.

Fix every blocker test-first before closure.

- [ ] **Step 5: Commit evidence and update draft PR**

```powershell
git add docs/testing/evidence
git commit -m "docs(model-inspection): record worker contract gate evidence"
```

Update PR #45 with:

- exact gate scope;
- commits/tasks completed;
- test counts and run IDs;
- architecture/security boundaries;
- blockers found and fixed;
- non-claims;
- next Gate 2 plan boundary.

Keep the PR draft.

---

## Plan Self-Review

### Spec coverage

This plan covers the approved specification’s Gate 1 requirements:

- pure shared contract project;
- protocol version/identity/limits;
- commands/messages/evidence;
- duplicate-property and bounded JSON handling;
- command/message state-machine rules;
- application request/result contracts;
- complete immutable Model Import handoff;
- ADR-003;
- README hierarchy;
- hosted CI;
- fresh evidence and whole-gate review.

Worker process launch, LLamaSharp extraction, packaging, classifier/service, ViewModel/UI, and chat are intentionally excluded and receive later plans.

### Placeholder scan

No `TBD`, `TODO`, “similar to,” unspecified error handling, or unowned test step remains. Every task names files, interfaces, commands, expected red/green outcomes, and commit boundaries.

### Type consistency

- Shared transport namespace: `GraniteEdgeAI.ModelInspection.Contracts`.
- Application namespace: `GraniteEdgeAI.Features.ModelInspection.Contracts`.
- `WorkerCompletionStatus.Completed` is distinct from `ModelInspectionOutcome.Ready`.
- `ModelInspectionExecutionResult` is application-owned and does not expose worker records.
- Navigation carries `ModelInspectionRequest`; `SelectedModelPath` remains a temporary derived compatibility property.
- Request IDs use `Guid` consistently.
- Stage values are 1–5 and preserve the approved UI wording/order.

## Primary Verification Sources

- Microsoft `ProcessStartInfo.RedirectStandardInput`: redirected stdin requires `UseShellExecute=false`.
- Microsoft `ProcessStartInfo.RedirectStandardOutput`: redirected streams require asynchronous/bounded consumption to avoid blocking.
- Microsoft `Process.Start(ProcessStartInfo)`: launch only a trusted controlled executable path.
- Microsoft `Process.Kill(Boolean)`: forced tree termination is abnormal and reserved for later fallback cleanup.
- Microsoft Windows app packaging/deployment guidance: packaged apps use MSIX and architecture-specific outputs.
- LLamaSharp official repository/NuGet: `LLamaSharp` and `LLamaSharp.Backend.Cpu` `0.27.0` map to llama.cpp commit `3f7c29d318e317b63f54c558bc69803963d7d88c`.

## Textbook Basis

- *Fundamentals of Software Architecture*: component responsibility, cohesion/coupling, stable boundaries, ADRs.
- *Code Complete*: information hiding, defensive interfaces, small cohesive types, incremental integration.
- *Designing Secure Software*: bounded untrusted input, fail-closed parsing, least exposure, explicit trust boundaries.
- *The Art of Unit Testing*: pure contracts, fakes/seams, separated unit and integration tiers.
- *Why Programs Fail*: preserve first failure and protocol/process evidence; do not misattribute infrastructure faults.
- *Refactoring*: migrate path handoff and runtime boundaries in small behaviour-preserving steps.
