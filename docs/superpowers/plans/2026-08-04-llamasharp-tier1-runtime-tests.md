# LLamaSharp Tier 1 Runtime Tests Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expand the model-free LLamaSharp test boundary so every relevant push and pull request verifies deterministic contracts, exact dependency policy, safe evidence generation, and child-process CPU-native failure handling without requiring the 2 GB Granite model.

**Architecture:** Keep all pure logic in the existing feasibility project and verify it through the deterministic MSTest project. Extract failure mapping, redaction, result finalisation, chat-template projection, and native-load cancellation configuration into small project-owned components. Add a shared child-process test-support project and a hosted native-integration project so native crashes or missing DLLs cannot terminate the MSTest host.

**Tech Stack:** .NET 8, C# 12, MSTest 4.3.2 on Microsoft.Testing.Platform, LLamaSharp 0.27.0, LLamaSharp.Backend.Cpu 0.27.0, System.Text.Json, Windows x64 GitHub Actions.

## Global Constraints

- Target branch: `feature/model-inspection`.
- Preserve the verified runtime pair: `LLamaSharp` `0.27.0` plus `LLamaSharp.Backend.Cpu` `0.27.0`.
- Preserve mapped llama.cpp commit `3f7c29d318e317b63f54c558bc69803963d7d88c`.
- Keep the standalone `b9870` runtime labelled as research evidence, not the application runtime.
- Do not add LLamaSharp, CUDA, Vulkan, or TurboQuant dependencies to the WinUI application project.
- Do not require an external model in Tier 1.
- Every test that calls a native LLamaSharp entry point must launch a child process.
- Keep `VocabOnly = true`, `GpuLayerCount = 0`, CUDA disabled, and Vulkan disabled.
- Do not create a context, KV cache, or inference request.
- Treat cancellation separately from failure.
- Treat native infrastructure failure separately from model outcomes.
- Open model and fixture files read-only.
- Never serialize a full local model path, full chat template, native pointer, or native handle.
- Use behaviour-based assertions rather than complete incidental error sentences.
- Use exact, bounded timeouts; no arbitrary `Thread.Sleep` calls.
- Keep test files small and grouped by responsibility.
- Update the nearest README and `docs/testing/LLamaSharp-Runtime-Test-Coverage-Matrix.md` with actual coverage status.

---

### Task 1: Reorganise the deterministic test project by responsibility

**Files:**
- Modify: `tools/ModelInspection.LlamaSharpSpike.Tests/ModelInspection.LlamaSharpSpike.Tests.csproj`
- Move: `tools/ModelInspection.LlamaSharpSpike.Tests/PinnedApplicationRuntimeTests.cs` → `tools/ModelInspection.LlamaSharpSpike.Tests/DependencyPolicy/RuntimeDependencyPolicyTests.cs`
- Move: `tools/ModelInspection.LlamaSharpSpike.Tests/SpikeOptionsParserTests.cs` → `tools/ModelInspection.LlamaSharpSpike.Tests/CommandLine/SpikeOptionsParserTests.cs`
- Move: `tools/ModelInspection.LlamaSharpSpike.Tests/ModelProbeSafetyValidatorTests.cs` → `tools/ModelInspection.LlamaSharpSpike.Tests/FileSafety/ModelProbeSafetyValidatorTests.cs`
- Move: `tools/ModelInspection.LlamaSharpSpike.Tests/ModelFileSnapshotServiceTests.cs` → `tools/ModelInspection.LlamaSharpSpike.Tests/FileSafety/ModelFileSnapshotServiceTests.cs`
- Move: `tools/ModelInspection.LlamaSharpSpike.Tests/VocabOnlyMetadataProjectionTests.cs` → `tools/ModelInspection.LlamaSharpSpike.Tests/Metadata/VocabOnlyMetadataProjectionTests.cs`
- Move: `tools/ModelInspection.LlamaSharpSpike.Tests/NativeLoadProgressRecorderTests.cs` → `tools/ModelInspection.LlamaSharpSpike.Tests/Progress/NativeLoadProgressRecorderTests.cs`
- Move: `tools/ModelInspection.LlamaSharpSpike.Tests/SmokeEvidenceWriterTests.cs` → `tools/ModelInspection.LlamaSharpSpike.Tests/Evidence/JsonEvidenceWriterTests.cs`
- Move: `tools/ModelInspection.LlamaSharpSpike.Tests/VocabOnlyModelProbeFailureTests.cs` → `tools/ModelInspection.LlamaSharpSpike.Tests/Failures/VocabOnlyModelProbeFailureTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.Tests/Support/TemporaryDirectory.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.Tests/Support/TestFileBuilder.cs`
- Modify: `tools/ModelInspection.LlamaSharpSpike.Tests/README.md`

**Interfaces:**
- Consumes: existing deterministic tests and Microsoft.Testing.Platform project configuration.
- Produces: unchanged test behaviour under responsibility-based folders, plus reusable `TemporaryDirectory` and `TestFileBuilder` helpers.

- [ ] **Step 1: Move the existing files with Git history preserved**

```powershell
git mv `
  "tools/ModelInspection.LlamaSharpSpike.Tests/PinnedApplicationRuntimeTests.cs" `
  "tools/ModelInspection.LlamaSharpSpike.Tests/DependencyPolicy/RuntimeDependencyPolicyTests.cs"

git mv `
  "tools/ModelInspection.LlamaSharpSpike.Tests/SpikeOptionsParserTests.cs" `
  "tools/ModelInspection.LlamaSharpSpike.Tests/CommandLine/SpikeOptionsParserTests.cs"

git mv `
  "tools/ModelInspection.LlamaSharpSpike.Tests/ModelProbeSafetyValidatorTests.cs" `
  "tools/ModelInspection.LlamaSharpSpike.Tests/FileSafety/ModelProbeSafetyValidatorTests.cs"

git mv `
  "tools/ModelInspection.LlamaSharpSpike.Tests/ModelFileSnapshotServiceTests.cs" `
  "tools/ModelInspection.LlamaSharpSpike.Tests/FileSafety/ModelFileSnapshotServiceTests.cs"

git mv `
  "tools/ModelInspection.LlamaSharpSpike.Tests/VocabOnlyMetadataProjectionTests.cs" `
  "tools/ModelInspection.LlamaSharpSpike.Tests/Metadata/VocabOnlyMetadataProjectionTests.cs"

git mv `
  "tools/ModelInspection.LlamaSharpSpike.Tests/NativeLoadProgressRecorderTests.cs" `
  "tools/ModelInspection.LlamaSharpSpike.Tests/Progress/NativeLoadProgressRecorderTests.cs"

git mv `
  "tools/ModelInspection.LlamaSharpSpike.Tests/SmokeEvidenceWriterTests.cs" `
  "tools/ModelInspection.LlamaSharpSpike.Tests/Evidence/JsonEvidenceWriterTests.cs"

git mv `
  "tools/ModelInspection.LlamaSharpSpike.Tests/VocabOnlyModelProbeFailureTests.cs" `
  "tools/ModelInspection.LlamaSharpSpike.Tests/Failures/VocabOnlyModelProbeFailureTests.cs"
```

- [ ] **Step 2: Add a disposable temporary-directory helper**

```csharp
namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.Support;

internal sealed class TemporaryDirectory : IDisposable
{
    internal TemporaryDirectory(string purpose)
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "GraniteEdgeAI-LlamaSharpTests",
            purpose,
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(Path);
    }

    internal string Path { get; }

    internal string Combine(params string[] parts)
    {
        string result = Path;
        foreach (string part in parts)
        {
            result = System.IO.Path.Combine(result, part);
        }

        return result;
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
```

- [ ] **Step 3: Add a deterministic test-file builder**

```csharp
namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.Support;

internal static class TestFileBuilder
{
    internal static async Task<string> WriteBytesAsync(
        TemporaryDirectory directory,
        string fileName,
        byte[] bytes)
    {
        string path = directory.Combine(fileName);
        await File.WriteAllBytesAsync(path, bytes);
        return path;
    }

    internal static async Task<string> WriteTextAsync(
        TemporaryDirectory directory,
        string fileName,
        string text)
    {
        string path = directory.Combine(fileName);
        await File.WriteAllTextAsync(path, text);
        return path;
    }
}
```

- [ ] **Step 4: Add `[TestCategory("Deterministic")]` to every deterministic test class**

Example:

```csharp
[TestClass]
[TestCategory("Deterministic")]
public sealed class SpikeOptionsParserTests
{
}
```

- [ ] **Step 5: Run the unchanged deterministic suite**

```powershell
dotnet test `
  "tools/ModelInspection.LlamaSharpSpike.Tests/ModelInspection.LlamaSharpSpike.Tests.csproj" `
  --configuration Release `
  --runtime win-x64 `
  --minimum-expected-tests 28
```

Expected: all 28 pre-existing tests pass after the move.

- [ ] **Step 6: Update the test README hierarchy and commit**

```powershell
git add tools/ModelInspection.LlamaSharpSpike.Tests
git commit -m "test(model-inspection): organise deterministic runtime tests"
```

---

### Task 2: Extract and test pure failure, redaction, and result-finalisation components

**Files:**
- Create: `tools/ModelInspection.LlamaSharpSpike/ModelProbe/ProbeFailure.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike/ModelProbe/ProbeFailureMapper.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike/ModelProbe/SensitiveTextRedactor.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike/ModelProbe/ProbeResultFinalizer.cs`
- Modify: `tools/ModelInspection.LlamaSharpSpike/ModelProbe/VocabOnlyModelProbe.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.Tests/Failures/ProbeFailureMapperTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.Tests/Failures/SensitiveTextRedactorTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.Tests/Failures/ProbeResultFinalizerTests.cs`

**Interfaces:**
- Produces: `ProbeFailureMapper.Map(Exception)`, `SensitiveTextRedactor.Redact(...)`, `SensitiveTextRedactor.RedactLogs(...)`, and `ProbeResultFinalizer.Resolve(...)`.
- Consumed by: `VocabOnlyModelProbe` and later process-level integration assertions.

- [ ] **Step 1: Write failing exception-mapping tests**

```csharp
[TestMethod]
[DataRow(typeof(FileNotFoundException), "MI-OP-MODEL-FILE-NOT-FOUND")]
[DataRow(typeof(UnauthorizedAccessException), "MI-OP-MODEL-FILE-ACCESS-DENIED")]
[DataRow(typeof(IOException), "MI-OP-MODEL-FILE-IO")]
[DataRow(typeof(DllNotFoundException), "MI-OP-RUNTIME-UNAVAILABLE")]
[DataRow(typeof(BadImageFormatException), "MI-OP-RUNTIME-ARCHITECTURE-MISMATCH")]
public void Map_WithKnownException_ReturnsStableCode(
    Type exceptionType,
    string expectedCode)
{
    Exception exception = (Exception)Activator.CreateInstance(
        exceptionType,
        "failure")!;

    ProbeFailure failure = ProbeFailureMapper.Map(exception);

    Assert.AreEqual(expectedCode, failure.Code);
}
```

Add focused tests for:

```csharp
new TypeInitializationException(
    "LLama.Native.NativeApi",
    new DllNotFoundException("missing"));

new TypeInitializationException(
    "LLama.Native.NativeApi",
    new BadImageFormatException("wrong architecture"));

new InvalidOperationException("unexpected");
```

Expected codes:

```text
MI-OP-RUNTIME-UNAVAILABLE
MI-OP-RUNTIME-ARCHITECTURE-MISMATCH
MI-OP-RUNTIME-INSPECTION-FAILED
```

- [ ] **Step 2: Run the focused failure-mapper tests and confirm RED**

```powershell
dotnet test `
  "tools/ModelInspection.LlamaSharpSpike.Tests/ModelInspection.LlamaSharpSpike.Tests.csproj" `
  --configuration Release `
  --runtime win-x64 `
  --filter "FullyQualifiedName~ProbeFailureMapperTests"
```

Expected: compilation fails because `ProbeFailureMapper` does not exist.

- [ ] **Step 3: Implement the project-owned failure record and mapper**

```csharp
namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.ModelProbe;

public sealed record ProbeFailure(
    string Code,
    string? Type,
    string Message);
```

```csharp
using LLama.Exceptions;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.ModelProbe;

public static class ProbeFailureMapper
{
    public static ProbeFailure Map(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        string? type = exception.GetType().FullName;

        return exception switch
        {
            FileNotFoundException =>
                new("MI-OP-MODEL-FILE-NOT-FOUND", type, exception.Message),
            UnauthorizedAccessException =>
                new("MI-OP-MODEL-FILE-ACCESS-DENIED", type, exception.Message),
            BadImageFormatException =>
                new("MI-OP-RUNTIME-ARCHITECTURE-MISMATCH", type, exception.Message),
            DllNotFoundException =>
                new("MI-OP-RUNTIME-UNAVAILABLE", type, exception.Message),
            LoadWeightsFailedException =>
                new("MI-PROBE-MODEL-LOAD-FAILED", type, exception.Message),
            IOException =>
                new("MI-OP-MODEL-FILE-IO", type, exception.Message),
            TypeInitializationException
                { InnerException: DllNotFoundException inner } =>
                new("MI-OP-RUNTIME-UNAVAILABLE", inner.GetType().FullName, inner.Message),
            TypeInitializationException
                { InnerException: BadImageFormatException inner } =>
                new("MI-OP-RUNTIME-ARCHITECTURE-MISMATCH", inner.GetType().FullName, inner.Message),
            _ =>
                new("MI-OP-RUNTIME-INSPECTION-FAILED", type, exception.Message)
        };
    }
}
```

- [ ] **Step 4: Write failing redaction tests**

```csharp
[TestMethod]
public void Redact_OnWindows_RemovesEveryCaseVariantOfCanonicalPath()
{
    const string modelPath = @"C:\Models\Granite.gguf";
    string input = @"failed C:\MODELS\GRANITE.GGUF then C:\Models\Granite.gguf";

    string? result = SensitiveTextRedactor.Redact(
        input,
        modelPath,
        windowsCaseInsensitive: true);

    Assert.AreEqual(
        "failed <model-path> then <model-path>",
        result);
}
```

Also test null text, null path, unrelated paths, filename-only text, and all entries returned by `RedactLogs`.

- [ ] **Step 5: Implement the redactor**

```csharp
namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.ModelProbe;

public static class SensitiveTextRedactor
{
    public static string? Redact(
        string? text,
        string? canonicalModelPath,
        bool? windowsCaseInsensitive = null)
    {
        if (string.IsNullOrEmpty(text) ||
            string.IsNullOrEmpty(canonicalModelPath))
        {
            return text;
        }

        bool ignoreCase = windowsCaseInsensitive ?? OperatingSystem.IsWindows();
        StringComparison comparison = ignoreCase
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return text.Replace(
            canonicalModelPath,
            "<model-path>",
            comparison);
    }

    public static IReadOnlyList<NativeBackendLogEntry> RedactLogs(
        IEnumerable<NativeBackendLogEntry> logs,
        string? canonicalModelPath)
    {
        ArgumentNullException.ThrowIfNull(logs);

        return logs.Select(
            entry => new NativeBackendLogEntry(
                entry.Level,
                Redact(entry.Message, canonicalModelPath) ?? string.Empty))
            .ToArray();
    }
}
```

- [ ] **Step 6: Write failing result-precedence tests**

Required cases:

```text
Succeeded + preserved integrity              → Succeeded
Succeeded + changed integrity                → Failed / MI-OP-MODEL-INTEGRITY-CHANGED
Succeeded + missing integrity                → Failed / MI-OP-MODEL-INTEGRITY-VERIFICATION-FAILED
Cancelled + preserved integrity              → Cancelled / MI-PROBE-CANCELLED
Cancelled + changed integrity                → Failed / MI-OP-MODEL-INTEGRITY-CHANGED
Cancelled + missing integrity                → Failed / MI-OP-MODEL-INTEGRITY-VERIFICATION-FAILED
Failed + changed integrity                    → keep original runtime failure and record integrity separately
```

Representative test:

```csharp
[TestMethod]
public void Resolve_WhenCancelledAndIntegrityChanged_PrioritisesIntegrityFailure()
{
    ProbeCompletionResolution result = ProbeResultFinalizer.Resolve(
        VocabOnlyProbeCompletionStatus.Cancelled,
        new ProbeFailure(
            "MI-PROBE-CANCELLED",
            typeof(OperationCanceledException).FullName,
            "cancelled"),
        new ModelFileIntegrityComparison
        {
            PathUnchanged = true,
            LengthUnchanged = true,
            LastWriteTimeUnchanged = true,
            Sha256Unchanged = false
        },
        integrityErrorType: null,
        integrityErrorMessage: null);

    Assert.AreEqual(VocabOnlyProbeCompletionStatus.Failed, result.Status);
    Assert.AreEqual("MI-OP-MODEL-INTEGRITY-CHANGED", result.Failure!.Code);
}
```

- [ ] **Step 7: Implement result finalisation**

```csharp
namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.ModelProbe;

public sealed record ProbeCompletionResolution(
    VocabOnlyProbeCompletionStatus Status,
    ProbeFailure? Failure);

public static class ProbeResultFinalizer
{
    public static ProbeCompletionResolution Resolve(
        VocabOnlyProbeCompletionStatus status,
        ProbeFailure? failure,
        ModelFileIntegrityComparison? integrity,
        string? integrityErrorType,
        string? integrityErrorMessage)
    {
        bool integrityApplies = status is
            VocabOnlyProbeCompletionStatus.Succeeded or
            VocabOnlyProbeCompletionStatus.Cancelled;

        if (integrityApplies && integrity is not null && !integrity.IsPreserved)
        {
            return new(
                VocabOnlyProbeCompletionStatus.Failed,
                new ProbeFailure(
                    "MI-OP-MODEL-INTEGRITY-CHANGED",
                    typeof(IOException).FullName,
                    "The selected model changed during the VocabOnly probe."));
        }

        if (integrityApplies && integrity is null)
        {
            return new(
                VocabOnlyProbeCompletionStatus.Failed,
                new ProbeFailure(
                    "MI-OP-MODEL-INTEGRITY-VERIFICATION-FAILED",
                    integrityErrorType,
                    integrityErrorMessage ??
                        "Post-probe model integrity could not be verified."));
        }

        return new(status, failure);
    }
}
```

- [ ] **Step 8: Refactor `VocabOnlyModelProbe` to use the helpers**

Replace the private `MapFailure`, `SanitizeLogs`, `SanitizeSensitiveText`, and inline success-only integrity precedence with:

```csharp
ProbeFailure? failure = null;

catch (OperationCanceledException exception)
{
    completionStatus = VocabOnlyProbeCompletionStatus.Cancelled;
    failure = new ProbeFailure(
        "MI-PROBE-CANCELLED",
        exception.GetType().FullName,
        exception.Message);
}
catch (Exception exception)
{
    completionStatus = VocabOnlyProbeCompletionStatus.Failed;
    failure = ProbeFailureMapper.Map(exception);
}
```

After the post-probe snapshot:

```csharp
ProbeCompletionResolution resolution = ProbeResultFinalizer.Resolve(
    completionStatus,
    failure,
    integrity,
    integrityErrorType,
    integrityErrorMessage);
```

Populate the result with:

```csharp
CompletionStatus = resolution.Status,
FailureCode = resolution.Failure?.Code,
FailureType = resolution.Failure?.Type,
FailureMessage = SensitiveTextRedactor.Redact(
    resolution.Failure?.Message,
    fullModelPath),
Logs = SensitiveTextRedactor.RedactLogs(logs, fullModelPath)
```

- [ ] **Step 9: Run all deterministic tests and commit**

```powershell
dotnet test `
  "tools/ModelInspection.LlamaSharpSpike.Tests/ModelInspection.LlamaSharpSpike.Tests.csproj" `
  --configuration Release `
  --runtime win-x64 `
  --minimum-expected-tests 45

git add tools/ModelInspection.LlamaSharpSpike tools/ModelInspection.LlamaSharpSpike.Tests
git commit -m "refactor(model-inspection): isolate probe failure contracts"
```

---

### Task 3: Extract and fully test chat-template and metadata evidence

**Files:**
- Create: `tools/ModelInspection.LlamaSharpSpike/ModelProbe/ChatTemplateEvidenceFactory.cs`
- Modify: `tools/ModelInspection.LlamaSharpSpike/ModelProbe/VocabOnlyEvidenceCollector.cs`
- Modify: `tools/ModelInspection.LlamaSharpSpike/ModelProbe/VocabOnlyMetadataProjection.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.Tests/Metadata/ChatTemplateEvidenceFactoryTests.cs`
- Expand: `tools/ModelInspection.LlamaSharpSpike.Tests/Metadata/VocabOnlyMetadataProjectionTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.Tests/Metadata/VocabOnlyCollectorSourceContractTests.cs`

**Interfaces:**
- Produces: `ChatTemplateEvidenceFactory.Create(IReadOnlyDictionary<string,string>)`.
- Preserves: metadata-only structural projection with nullable unavailable values.

- [ ] **Step 1: Write failing chat-template tests**

```csharp
[TestMethod]
public void Create_WhenKeyIsAbsent_ReturnsNotPresent()
{
    ChatTemplateEvidence result = ChatTemplateEvidenceFactory.Create(
        new Dictionary<string, string>());

    Assert.IsFalse(result.Present);
    Assert.IsNull(result.LengthCharacters);
    Assert.IsNull(result.Sha256);
}

[TestMethod]
[DataRow("")]
[DataRow("   ")]
[DataRow("{% for message in messages %}{{ message.content }}{% endfor %}")]
[DataRow("{{ 'こんにちは' }}")]
public void Create_WhenKeyExists_RecordsExactLengthAndStableHash(string template)
{
    ChatTemplateEvidence result = ChatTemplateEvidenceFactory.Create(
        new Dictionary<string, string>
        {
            ["tokenizer.chat_template"] = template
        });

    Assert.IsTrue(result.Present);
    Assert.AreEqual(template.Length, result.LengthCharacters);
    Assert.AreEqual(
        Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(template)))
            .ToLowerInvariant(),
        result.Sha256);
}
```

- [ ] **Step 2: Implement `ChatTemplateEvidenceFactory`**

```csharp
using System.Security.Cryptography;
using System.Text;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.ModelProbe;

public static class ChatTemplateEvidenceFactory
{
    public static ChatTemplateEvidence Create(
        IReadOnlyDictionary<string, string> metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        if (!metadata.TryGetValue(
                "tokenizer.chat_template",
                out string? template))
        {
            return new ChatTemplateEvidence { Present = false };
        }

        byte[] bytes = Encoding.UTF8.GetBytes(template);

        return new ChatTemplateEvidence
        {
            Present = true,
            LengthCharacters = template.Length,
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()
        };
    }
}
```

- [ ] **Step 3: Refactor the collector to call the factory**

```csharp
ChatTemplate = ChatTemplateEvidenceFactory.Create(metadata)
```

Delete the old private chat-template factory method.

- [ ] **Step 4: Expand metadata-projection tests**

Add focused tests for:

```text
missing architecture
unknown architecture
empty architecture
whitespace architecture
zero values
negative values
Int32 overflow
UInt64 overflow
culture-specific value "1,024"
wrong architecture prefix
metadata from two architectures
missing parameter count
```

Representative test:

```csharp
[TestMethod]
public void Create_WithMultipleArchitecturePrefixes_UsesOnlySelectedArchitecture()
{
    var metadata = new Dictionary<string, string>
    {
        ["general.architecture"] = "granite",
        ["granite.block_count"] = "40",
        ["llama.block_count"] = "99"
    };

    VocabOnlyMetadataProjection result =
        VocabOnlyMetadataProjection.Create(metadata);

    Assert.AreEqual(40, result.LayerCount);
}
```

- [ ] **Step 5: Add a source-contract test against unsafe VocabOnly getters**

```csharp
[TestMethod]
public void Collector_DoesNotReferenceUnsafeNativeHyperparameterGetters()
{
    string repositoryRoot = RepositoryPaths.FindRoot();
    string sourcePath = Path.Combine(
        repositoryRoot,
        "tools",
        "ModelInspection.LlamaSharpSpike",
        "ModelProbe",
        "VocabOnlyEvidenceCollector.cs");

    string source = File.ReadAllText(sourcePath);
    string[] forbiddenMembers =
    {
        ".ContextSize",
        ".EmbeddingSize",
        ".LayerCount",
        ".HeadCount",
        ".KVHeadCount",
        ".HasEncoder",
        ".HasDecoder",
        ".IsRecurrent",
        ".IsDiffusion",
        ".Description",
        ".SizeInBytes",
        ".ParameterCount"
    };

    foreach (string forbiddenMember in forbiddenMembers)
    {
        Assert.IsFalse(
            source.Contains(forbiddenMember, StringComparison.Ordinal),
            $"VocabOnly collector must not call {forbiddenMember}.");
    }
}
```

Create `Support/RepositoryPaths.cs` with:

```csharp
namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.Support;

internal static class RepositoryPaths
{
    internal static string FindRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "global.json")) &&
                Directory.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
```

- [ ] **Step 6: Run metadata tests and commit**

```powershell
dotnet test `
  "tools/ModelInspection.LlamaSharpSpike.Tests/ModelInspection.LlamaSharpSpike.Tests.csproj" `
  --configuration Release `
  --runtime win-x64 `
  --filter "FullyQualifiedName~Metadata"

git add tools/ModelInspection.LlamaSharpSpike tools/ModelInspection.LlamaSharpSpike.Tests
git commit -m "test(model-inspection): cover VocabOnly metadata evidence"
```

---

### Task 4: Harden and test native progress recording

**Files:**
- Modify: `tools/ModelInspection.LlamaSharpSpike/ModelProbe/NativeLoadProgressRecorder.cs`
- Expand: `tools/ModelInspection.LlamaSharpSpike.Tests/Progress/NativeLoadProgressRecorderTests.cs`

**Interfaces:**
- Preserves: `IProgress<float>.Report(float)` and `GetSnapshot()`.
- Adds normalization: NaN ignored, infinities normalized, finite values clamped.

- [ ] **Step 1: Add failing special-value tests**

```csharp
[TestMethod]
public void Report_WithNaN_IgnoresValue()
{
    var recorder = new NativeLoadProgressRecorder();

    recorder.Report(float.NaN);

    Assert.AreEqual(0, recorder.GetSnapshot().Count);
}

[TestMethod]
public void Report_WithInfinities_NormalisesToBounds()
{
    var recorder = new NativeLoadProgressRecorder();

    recorder.Report(float.NegativeInfinity);
    recorder.Report(float.PositiveInfinity);

    IReadOnlyList<NativeLoadProgressSample> samples = recorder.GetSnapshot();
    Assert.AreEqual(0f, samples[0].Fraction);
    Assert.AreEqual(1f, samples[1].Fraction);
}
```

- [ ] **Step 2: Add failing concurrency and elapsed-time tests**

```csharp
[TestMethod]
public void Report_FromMultipleThreads_RemainsValidAndSnapshotSafe()
{
    var recorder = new NativeLoadProgressRecorder();

    Parallel.For(0, 500, index => recorder.Report(index / 499f));

    IReadOnlyList<NativeLoadProgressSample> samples = recorder.GetSnapshot();

    Assert.IsTrue(samples.All(sample =>
        float.IsFinite(sample.Fraction) &&
        sample.Fraction >= 0f &&
        sample.Fraction <= 1f));

    for (int index = 1; index < samples.Count; index++)
    {
        Assert.IsTrue(
            samples[index].ElapsedMilliseconds >=
            samples[index - 1].ElapsedMilliseconds);
    }
}
```

- [ ] **Step 3: Implement NaN and infinity handling**

```csharp
public void Report(float value)
{
    if (float.IsNaN(value))
    {
        return;
    }

    float fraction = value switch
    {
        float.NegativeInfinity => 0f,
        float.PositiveInfinity => 1f,
        _ => Math.Clamp(value, 0f, 1f)
    };

    lock (_sync)
    {
        if (_samples.Count > 0 &&
            _samples[^1].Fraction.Equals(fraction))
        {
            return;
        }

        _samples.Add(new NativeLoadProgressSample(
            _stopwatch.ElapsedMilliseconds,
            fraction));
    }
}
```

- [ ] **Step 4: Run progress tests and commit**

```powershell
dotnet test `
  "tools/ModelInspection.LlamaSharpSpike.Tests/ModelInspection.LlamaSharpSpike.Tests.csproj" `
  --configuration Release `
  --runtime win-x64 `
  --filter "FullyQualifiedName~NativeLoadProgressRecorderTests"

git add tools/ModelInspection.LlamaSharpSpike/ModelProbe/NativeLoadProgressRecorder.cs `
        tools/ModelInspection.LlamaSharpSpike.Tests/Progress/NativeLoadProgressRecorderTests.cs
git commit -m "test(model-inspection): harden native progress evidence"
```

---

### Task 5: Expand read-only file snapshot and integrity tests

**Files:**
- Create: `tools/ModelInspection.LlamaSharpSpike/ModelProbe/IModelFileHasher.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike/ModelProbe/Sha256ModelFileHasher.cs`
- Modify: `tools/ModelInspection.LlamaSharpSpike/ModelProbe/ModelFileSnapshotService.cs`
- Expand: `tools/ModelInspection.LlamaSharpSpike.Tests/FileSafety/ModelFileSnapshotServiceTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.Tests/FileSafety/ModelFileIntegrityComparisonTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.Tests/Support/BlockingModelFileHasher.cs`

**Interfaces:**
- Produces: injectable `IModelFileHasher.ComputeHashAsync(Stream,CancellationToken)`.
- Preserves: `ModelFileSnapshotService.CaptureAsync(string,CancellationToken)`.

- [ ] **Step 1: Write failing cancellation-during-hash test**

```csharp
[TestMethod]
public async Task CaptureAsync_WhenCancelledDuringHash_ThrowsOperationCanceledException()
{
    using var directory = new TemporaryDirectory("cancel-during-hash");
    string modelPath = await TestFileBuilder.WriteTextAsync(
        directory,
        "model.gguf",
        "model-bytes");

    var hasher = new BlockingModelFileHasher();
    var service = new ModelFileSnapshotService(hasher);
    using var cancellationSource = new CancellationTokenSource();

    Task<ModelFileSnapshot> captureTask = service.CaptureAsync(
        modelPath,
        cancellationSource.Token);

    await hasher.Started;
    cancellationSource.Cancel();

    await Assert.ThrowsExactlyAsync<OperationCanceledException>(
        async () => await captureTask);
}
```

- [ ] **Step 2: Add the hasher interface and production implementation**

```csharp
namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.ModelProbe;

public interface IModelFileHasher
{
    Task<byte[]> ComputeHashAsync(
        Stream stream,
        CancellationToken cancellationToken);
}
```

```csharp
using System.Security.Cryptography;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.ModelProbe;

public sealed class Sha256ModelFileHasher : IModelFileHasher
{
    public async Task<byte[]> ComputeHashAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using SHA256 sha256 = SHA256.Create();
        return await sha256.ComputeHashAsync(stream, cancellationToken);
    }
}
```

- [ ] **Step 3: Inject the hasher into `ModelFileSnapshotService`**

```csharp
private readonly IModelFileHasher _hasher;

public ModelFileSnapshotService()
    : this(new Sha256ModelFileHasher())
{
}

public ModelFileSnapshotService(IModelFileHasher hasher)
{
    _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
}
```

Replace direct `SHA256.Create()` usage with:

```csharp
hash = await _hasher.ComputeHashAsync(stream, cancellationToken);
```

- [ ] **Step 4: Add tests for the complete file matrix**

Required focused tests:

```text
empty file uses SHA-256 e3b0c442...
read-only file succeeds
missing file throws FileNotFoundException
directory path throws FileNotFoundException
pre-cancelled token throws OperationCanceledException
cancelled during hash throws OperationCanceledException
canonical path fingerprint stable for the same path
Windows case variants produce the same path fingerprint
```

- [ ] **Step 5: Add one integrity-comparison test per field**

```csharp
[TestMethod]
public void Compare_WhenOnlyTimestampChanges_ReportsOnlyTimestampDifference()
{
    ModelFileSnapshot before = Snapshot(
        length: 10,
        timestamp: DateTimeOffset.Parse("2026-08-04T10:00:00Z"),
        hash: "aa",
        pathHash: "bb");

    ModelFileSnapshot after = before with
    {
        LastWriteTimeUtc = DateTimeOffset.Parse("2026-08-04T10:00:01Z")
    };

    ModelFileIntegrityComparison result =
        ModelFileIntegrityComparison.Compare(before, after);

    Assert.IsTrue(result.PathUnchanged);
    Assert.IsTrue(result.LengthUnchanged);
    Assert.IsFalse(result.LastWriteTimeUnchanged);
    Assert.IsTrue(result.Sha256Unchanged);
    Assert.IsFalse(result.IsPreserved);
}
```

Add equivalent tests for path hash, length, and SHA-256.

- [ ] **Step 6: Run file-safety tests and commit**

```powershell
dotnet test `
  "tools/ModelInspection.LlamaSharpSpike.Tests/ModelInspection.LlamaSharpSpike.Tests.csproj" `
  --configuration Release `
  --runtime win-x64 `
  --filter "FullyQualifiedName~FileSafety"

git add tools/ModelInspection.LlamaSharpSpike tools/ModelInspection.LlamaSharpSpike.Tests
git commit -m "test(model-inspection): expand model file integrity coverage"
```

---

### Task 6: Expand path-collision and command-line contracts, including native-load cancellation

**Files:**
- Modify: `tools/ModelInspection.LlamaSharpSpike/SpikeOptionsParser.cs`
- Modify: `tools/ModelInspection.LlamaSharpSpike/Program.cs`
- Modify: `tools/ModelInspection.LlamaSharpSpike/ModelProbe/VocabOnlyModelProbe.cs`
- Expand: `tools/ModelInspection.LlamaSharpSpike.Tests/CommandLine/SpikeOptionsParserTests.cs`
- Expand: `tools/ModelInspection.LlamaSharpSpike.Tests/FileSafety/ModelProbeSafetyValidatorTests.cs`

**Interfaces:**
- Adds: `SpikeOptions.CancelNativeAfterMilliseconds`.
- Adds CLI option: `--cancel-native-after-ms <positive-integer>`.
- Adds overload: `VocabOnlyModelProbe.RunAsync(string,int?,CancellationToken)`.

- [ ] **Step 1: Write parser tests for every remaining branch**

Add tests for:

```text
-h
help mixed with another option
duplicate --output
duplicate --cancel-after-ms
duplicate --cancel-native-after-ms
missing --cancel-native-after-ms value
zero, negative, decimal, non-numeric and Int32-overflow delays
paths containing spaces
Unicode paths
both cancellation modes supplied together
--cancel-native-after-ms without --model
all valid option orders
```

Representative expected contract:

```csharp
[TestMethod]
public void Parse_WithBothCancellationModes_ReturnsControlledError()
{
    SpikeOptionsParseResult result = SpikeOptionsParser.Parse(
        new[]
        {
            "--model", "granite.gguf",
            "--cancel-after-ms", "10",
            "--cancel-native-after-ms", "10"
        });

    Assert.IsFalse(result.Succeeded);
    Assert.IsNotNull(result.ErrorMessage);
    StringAssert.Contains(result.ErrorMessage, "--cancel-after-ms");
    StringAssert.Contains(result.ErrorMessage, "--cancel-native-after-ms");
}
```

- [ ] **Step 2: Extend `SpikeOptions` and parsing**

```csharp
public sealed record SpikeOptions(
    string OutputPath,
    bool ShowHelp,
    string? ModelPath,
    int? CancelAfterMilliseconds,
    int? CancelNativeAfterMilliseconds)
```

Add parsing for:

```csharp
case "--cancel-native-after-ms":
    // same positive invariant integer validation as --cancel-after-ms
```

After parsing:

```csharp
if (cancelNativeAfterMilliseconds.HasValue &&
    string.IsNullOrWhiteSpace(modelPath))
{
    return Failure(
        "The --cancel-native-after-ms option requires --model.");
}

if (cancelAfterMilliseconds.HasValue &&
    cancelNativeAfterMilliseconds.HasValue)
{
    return Failure(
        "The --cancel-after-ms and --cancel-native-after-ms options are mutually exclusive.");
}
```

Update help text to list both modes.

- [ ] **Step 3: Add native-load-scoped cancellation to the probe**

Change the public signature to:

```csharp
public Task<VocabOnlyModelProbeResult> RunAsync(
    string modelPath,
    CancellationToken cancellationToken)
{
    return RunAsync(
        modelPath,
        cancelNativeAfterMilliseconds: null,
        cancellationToken);
}

public async Task<VocabOnlyModelProbeResult> RunAsync(
    string modelPath,
    int? cancelNativeAfterMilliseconds,
    CancellationToken cancellationToken)
```

Immediately before `LoadFromFileAsync`:

```csharp
using var nativeLoadCancellation =
    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

if (cancelNativeAfterMilliseconds.HasValue)
{
    nativeLoadCancellation.CancelAfter(
        cancelNativeAfterMilliseconds.Value);
}

weights = await LLamaWeights.LoadFromFileAsync(
    modelParameters,
    nativeLoadCancellation.Token,
    progressRecorder);
```

- [ ] **Step 4: Pass the new option from `Program`**

```csharp
result = await new VocabOnlyModelProbe().RunAsync(
    modelPath,
    options.CancelNativeAfterMilliseconds,
    cancellationSource.Token);
```

- [ ] **Step 5: Expand path-collision tests**

Add tests for:

```text
relative versus absolute equivalent path
. segment
.. segment
Windows case-only difference
spaces
Unicode
blank model path
blank output path
invalid path syntax
```

Where the operating system does not reject a nominally invalid character, assert canonical equality behaviour rather than assuming cross-platform invalidity.

- [ ] **Step 6: Run parser and safety tests, then commit**

```powershell
dotnet test `
  "tools/ModelInspection.LlamaSharpSpike.Tests/ModelInspection.LlamaSharpSpike.Tests.csproj" `
  --configuration Release `
  --runtime win-x64 `
  --filter "FullyQualifiedName~CommandLine|FullyQualifiedName~ModelProbeSafetyValidatorTests"

git add tools/ModelInspection.LlamaSharpSpike tools/ModelInspection.LlamaSharpSpike.Tests
git commit -m "feat(model-inspection): add native-load cancellation test hook"
```

---

### Task 7: Harden the atomic JSON writer and evidence contracts

**Files:**
- Modify: `tools/ModelInspection.LlamaSharpSpike/JsonEvidenceWriter.cs`
- Expand: `tools/ModelInspection.LlamaSharpSpike.Tests/Evidence/JsonEvidenceWriterTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.Tests/Evidence/EvidenceContractTests.cs`
- Expand: `tools/ModelInspection.LlamaSharpSpike.Tests/DependencyPolicy/RuntimeDependencyPolicyTests.cs`

**Interfaces:**
- Preserves: `JsonEvidenceWriter.WriteAsync<T>(T,string,CancellationToken)`.
- Adds tests ensuring evidence contains only project-owned, privacy-safe types.

- [ ] **Step 1: Add failing JSON writer tests**

Cover:

```text
parent directory creation
camel-case properties
string enums
atomic replacement
serialization failure preserves previous file
pre-cancelled token preserves previous file
temporary file removed after success
temporary file removed after serialization failure
destination locked
destination path is a directory
blank path
null evidence object
```

Use a deliberately failing type:

```csharp
private sealed class UnserializableEvidence
{
    public string Value => throw new InvalidOperationException("serialization failed");
}
```

Representative atomicity test:

```csharp
[TestMethod]
public async Task WriteAsync_WhenSerializationFails_PreservesExistingEvidence()
{
    using var directory = new TemporaryDirectory("json-serialization-failure");
    string output = directory.Combine("evidence.json");
    await File.WriteAllTextAsync(output, "{\"valid\":true}");

    var writer = new JsonEvidenceWriter();

    await Assert.ThrowsExactlyAsync<InvalidOperationException>(
        async () => await writer.WriteAsync(
            new UnserializableEvidence(),
            output,
            CancellationToken.None));

    Assert.AreEqual(
        "{\"valid\":true}",
        await File.ReadAllTextAsync(output));
    Assert.AreEqual(0, Directory.GetFiles(directory.Path, "*.tmp-*" ).Length);
}
```

- [ ] **Step 2: Make serialization happen before creating the temporary file**

In `JsonEvidenceWriter.WriteAsync`:

```csharp
string json = JsonSerializer.Serialize(result, SerializerOptions);
string temporaryPath =
    fullOutputPath + ".tmp-" + Guid.NewGuid().ToString("N");
```

This guarantees a serialization failure cannot leave a temporary file.

- [ ] **Step 3: Add evidence-contract tests**

Use reflection and serialization to assert:

```text
VocabOnlyModelProbeResult schema is 1.1
parameter count and unsafe fields are nullable
no property type belongs to LLamaSharp assemblies
no property type is SafeHandle, IntPtr, UIntPtr, Stream, Exception, or XAML
no property is named ModelPath or ChatTemplateText
serialized success evidence does not contain tokenizer.chat_template content
```

Representative reflection helper:

```csharp
private static IEnumerable<Type> WalkTypes(Type root)
{
    var pending = new Stack<Type>();
    var visited = new HashSet<Type>();
    pending.Push(root);

    while (pending.Count > 0)
    {
        Type current = pending.Pop();
        if (!visited.Add(current))
        {
            continue;
        }

        yield return current;

        foreach (PropertyInfo property in current.GetProperties())
        {
            Type propertyType = Nullable.GetUnderlyingType(property.PropertyType)
                ?? property.PropertyType;

            if (propertyType.IsGenericType)
            {
                foreach (Type argument in propertyType.GetGenericArguments())
                {
                    pending.Push(argument);
                }
            }
            else if (propertyType.Namespace?.StartsWith(
                         "GraniteEdgeAI.",
                         StringComparison.Ordinal) == true)
            {
                pending.Push(propertyType);
            }
        }
    }
}
```

- [ ] **Step 4: Expand dependency-policy tests**

Read actual project files and assert:

```text
LLamaSharp exactly 0.27.0
LLamaSharp.Backend.Cpu exactly 0.27.0
no floating/range versions
no LLamaSharp.Backend.Cuda*
no LLamaSharp.Backend.Vulkan
no TurboQuant package/reference in the spike project
no LLamaSharp* reference in the WinUI application project
MTP runner properties remain enabled
PinnedApplicationRuntime records mapped commit and separate b9870 research commit
```

- [ ] **Step 5: Run evidence and policy tests, then commit**

```powershell
dotnet test `
  "tools/ModelInspection.LlamaSharpSpike.Tests/ModelInspection.LlamaSharpSpike.Tests.csproj" `
  --configuration Release `
  --runtime win-x64 `
  --filter "FullyQualifiedName~Evidence|FullyQualifiedName~RuntimeDependencyPolicyTests"

git add tools/ModelInspection.LlamaSharpSpike tools/ModelInspection.LlamaSharpSpike.Tests
git commit -m "test(model-inspection): harden runtime evidence contracts"
```

---

### Task 8: Add shared child-process test support

**Files:**
- Create: `tools/ModelInspection.LlamaSharpSpike.TestSupport/ModelInspection.LlamaSharpSpike.TestSupport.csproj`
- Create: `tools/ModelInspection.LlamaSharpSpike.TestSupport/ProcessTerminationKind.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.TestSupport/ProbeProcessRequest.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.TestSupport/ProbeExecutionResult.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.TestSupport/ProbeProcessRunner.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.TestSupport/PublishedProbeLocation.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.TestSupport/TemporaryProbeSandbox.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.TestSupport/EvidenceAssertions.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.TestSupport/README.md`
- Create: `tools/ModelInspection.LlamaSharpSpike.Tests/Support/ProbeProcessRunnerTests.cs`
- Modify: `tools/ModelInspection.LlamaSharpSpike.Tests/ModelInspection.LlamaSharpSpike.Tests.csproj`

**Interfaces:**
- Produces: bounded child-process execution and a disposable copy of the published spike.
- Consumed by: hosted native integration and trusted real-model integration projects.

- [ ] **Step 1: Create the non-shipping support project**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Define process request/result contracts**

```csharp
namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;

public enum ProcessTerminationKind
{
    Exited,
    TimedOut,
    StartFailed
}

public sealed record ProbeProcessRequest
{
    public required string ExecutablePath { get; init; }
    public required IReadOnlyList<string> Arguments { get; init; }
    public required string WorkingDirectory { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    public IReadOnlyDictionary<string, string?> EnvironmentVariables { get; init; } =
        new Dictionary<string, string?>();
}

public sealed record ProbeExecutionResult
{
    public required ProcessTerminationKind TerminationKind { get; init; }
    public int? ExitCode { get; init; }
    public required string StandardOutput { get; init; }
    public required string StandardError { get; init; }
    public required TimeSpan Duration { get; init; }
    public int ProcessId { get; init; }
}
```

- [ ] **Step 3: Write failing process-runner tests**

Use the current .NET host as a deterministic child process:

```csharp
[TestMethod]
public async Task RunAsync_WhenProcessExits_CapturesBothStreamsAndExitCode()
{
    ProbeExecutionResult result = await new ProbeProcessRunner().RunAsync(
        new ProbeProcessRequest
        {
            ExecutablePath = "powershell.exe",
            Arguments = new[]
            {
                "-NoProfile",
                "-Command",
                "[Console]::Out.Write('out'); [Console]::Error.Write('err'); exit 7"
            },
            WorkingDirectory = Environment.CurrentDirectory,
            Timeout = TimeSpan.FromSeconds(10)
        },
        CancellationToken.None);

    Assert.AreEqual(ProcessTerminationKind.Exited, result.TerminationKind);
    Assert.AreEqual(7, result.ExitCode);
    Assert.AreEqual("out", result.StandardOutput);
    Assert.AreEqual("err", result.StandardError);
}
```

Add a timeout test with `Start-Sleep -Seconds 30` and a 200 ms timeout; assert `TimedOut` and that the process tree was killed.

- [ ] **Step 4: Implement bounded child-process execution**

Key requirements in `ProbeProcessRunner.RunAsync`:

```csharp
var startInfo = new ProcessStartInfo
{
    FileName = request.ExecutablePath,
    WorkingDirectory = request.WorkingDirectory,
    UseShellExecute = false,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    CreateNoWindow = true
};

foreach (string argument in request.Arguments)
{
    startInfo.ArgumentList.Add(argument);
}

foreach ((string key, string? value) in request.EnvironmentVariables)
{
    startInfo.Environment[key] = value;
}
```

Use `ReadToEndAsync`, `WaitForExitAsync`, a linked timeout token, and:

```csharp
process.Kill(entireProcessTree: true);
```

on timeout. Return `TimedOut` rather than throwing.

- [ ] **Step 5: Add published-probe location and sandbox helpers**

```csharp
public static class PublishedProbeLocation
{
    public const string EnvironmentVariable =
        "LLAMASHARP_SPIKE_PUBLISH_DIR";

    public static string RequireFromEnvironment()
    {
        string? path = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            throw new InvalidOperationException(
                $"{EnvironmentVariable} must point to a published spike directory.");
        }

        return Path.GetFullPath(path);
    }
}
```

`TemporaryProbeSandbox.Create(string publishedDirectory)` copies every file recursively into a unique temp directory and returns the path to:

```text
GraniteEdgeAI.ModelInspection.LlamaSharpSpike.exe
```

It also exposes:

```csharp
public IReadOnlyList<string> FindNativeDlls()
```

matching `llama*.dll` and `ggml*.dll`.

- [ ] **Step 6: Add shared evidence assertions**

```csharp
public static JsonDocument LoadJson(string path)
{
    AssertFileExists(path);
    return JsonDocument.Parse(File.ReadAllText(path));
}

public static void AssertDoesNotContainCanonicalPath(
    string value,
    string canonicalPath)
{
    if (value.Contains(
            canonicalPath,
            StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "Output exposed the canonical model path.");
    }
}
```

The support project must not reference MSTest; throw ordinary exceptions so both integration projects can use it.

- [ ] **Step 7: Reference TestSupport from the deterministic tests and run its focused tests**

```xml
<ProjectReference Include="..\ModelInspection.LlamaSharpSpike.TestSupport\ModelInspection.LlamaSharpSpike.TestSupport.csproj" />
```

```powershell
dotnet test `
  "tools/ModelInspection.LlamaSharpSpike.Tests/ModelInspection.LlamaSharpSpike.Tests.csproj" `
  --configuration Release `
  --runtime win-x64 `
  --filter "FullyQualifiedName~ProbeProcessRunnerTests"
```

- [ ] **Step 8: Commit**

```powershell
git add tools/ModelInspection.LlamaSharpSpike.TestSupport `
        tools/ModelInspection.LlamaSharpSpike.Tests
git commit -m "test(model-inspection): add isolated probe process support"
```

---

### Task 9: Add hosted native-library process integration tests

**Files:**
- Create: `tools/ModelInspection.LlamaSharpSpike.NativeIntegrationTests/ModelInspection.LlamaSharpSpike.NativeIntegrationTests.csproj`
- Create: `tools/ModelInspection.LlamaSharpSpike.NativeIntegrationTests/README.md`
- Create: `tools/ModelInspection.LlamaSharpSpike.NativeIntegrationTests/NativeBackendSmokeProcessTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.NativeIntegrationTests/MissingNativeBackendProcessTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.NativeIntegrationTests/InvalidNativeBackendProcessTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.NativeIntegrationTests/RuntimeEvidenceProcessTests.cs`

**Interfaces:**
- Consumes: published spike directory from `LLAMASHARP_SPIKE_PUBLISH_DIR` and TestSupport child-process helpers.
- Produces: model-free native success and negative-backend verification on GitHub-hosted Windows x64.

- [ ] **Step 1: Create the MTP-enabled integration project**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
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
    <ProjectReference Include="..\ModelInspection.LlamaSharpSpike.TestSupport\ModelInspection.LlamaSharpSpike.TestSupport.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add successful native-smoke child-process test**

```csharp
[TestClass]
[TestCategory("NativeIntegration")]
public sealed class NativeBackendSmokeProcessTests
{
    [TestMethod]
    public async Task Run_WithPublishedCpuBackend_SucceedsAndWritesEvidence()
    {
        using TemporaryProbeSandbox sandbox = TemporaryProbeSandbox.Create(
            PublishedProbeLocation.RequireFromEnvironment());

        string evidencePath = Path.Combine(
            sandbox.DirectoryPath,
            "evidence",
            "runtime-smoke.json");

        ProbeExecutionResult result = await new ProbeProcessRunner().RunAsync(
            sandbox.CreateRequest(
                new[] { "--output", evidencePath },
                TimeSpan.FromSeconds(30)),
            CancellationToken.None);

        Assert.AreEqual(ProcessTerminationKind.Exited, result.TerminationKind);
        Assert.AreEqual(0, result.ExitCode);

        using JsonDocument document = EvidenceAssertions.LoadJson(evidencePath);
        JsonElement root = document.RootElement;
        Assert.IsTrue(root.GetProperty("succeeded").GetBoolean());
        Assert.AreEqual("0.27.0", root.GetProperty("managedPackageVersion").GetString());
        Assert.AreEqual("0.27.0", root.GetProperty("backendPackageVersion").GetString());
        Assert.IsFalse(root.GetProperty("selectedBackend").GetProperty("usesCuda").GetBoolean());
        Assert.IsFalse(root.GetProperty("selectedBackend").GetProperty("usesVulkan").GetBoolean());
    }
}
```

- [ ] **Step 3: Add missing-backend sandbox test**

Delete all DLLs returned by `sandbox.FindNativeDlls()` before launching the process.

Required assertions:

```text
parent test host survives
termination kind is Exited
exit code is 1
JSON exists
failure code is MI-OP-RUNTIME-UNAVAILABLE or MI-OP-RUNTIME-INITIALISATION-FAILED
no model outcome field exists
```

- [ ] **Step 4: Add invalid-native-image sandbox test**

Locate `llama.dll` case-insensitively, replace its bytes with UTF-8 text `not a PE file`, launch the child, and assert:

```text
parent survives
process exits or terminates; result is captured
exit is nonzero
no external model is involved
stdout/stderr and JSON, when present, are retained
```

If the process returns managed evidence, require `MI-OP-RUNTIME-ARCHITECTURE-MISMATCH` or `MI-OP-RUNTIME-INITIALISATION-FAILED`. If native startup terminates before JSON, record the nonzero process result as the expected containment behaviour.

- [ ] **Step 5: Add runtime-evidence privacy and schema tests**

Require:

```text
schema 1.0
exact mapped commit
X64 process
non-empty native library name
finite log count
no property named modelPath
no CUDA or Vulkan selection
```

- [ ] **Step 6: Publish locally and run the integration project**

```powershell
$PublishDirectory = Join-Path $env:TEMP "GraniteEdgeAI-LlamaSharp-Publish"

Remove-Item $PublishDirectory -Recurse -Force -ErrorAction SilentlyContinue

dotnet publish `
  "tools/ModelInspection.LlamaSharpSpike/ModelInspection.LlamaSharpSpike.csproj" `
  --configuration Release `
  --runtime win-x64 `
  --self-contained false `
  --output $PublishDirectory

$env:LLAMASHARP_SPIKE_PUBLISH_DIR = $PublishDirectory

dotnet test `
  "tools/ModelInspection.LlamaSharpSpike.NativeIntegrationTests/ModelInspection.LlamaSharpSpike.NativeIntegrationTests.csproj" `
  --configuration Release `
  --runtime win-x64 `
  --minimum-expected-tests 4
```

- [ ] **Step 7: Commit**

```powershell
git add tools/ModelInspection.LlamaSharpSpike.NativeIntegrationTests
git commit -m "test(model-inspection): add hosted native backend integration tests"
```

---

### Task 10: Update hosted CI, documentation, and the Tier 1 coverage register

**Files:**
- Modify: `.github/workflows/llamasharp-feasibility-smoke.yml`
- Create: `docs/testing/LLamaSharp-Runtime-Test-Coverage-Matrix.md`
- Modify: `tools/README.md`
- Modify: `tools/ModelInspection.LlamaSharpSpike.Tests/README.md`
- Modify: `tools/ModelInspection.LlamaSharpSpike.TestSupport/README.md`
- Modify: `tools/ModelInspection.LlamaSharpSpike.NativeIntegrationTests/README.md`
- Modify: `tools/ModelInspection.LlamaSharpSpike/README.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/README.md`

**Interfaces:**
- Produces: model-free Tier 1 automation and an explicit risk-to-test coverage matrix.
- Blocks: Tier 2 until all Tier 1 commands pass.

- [ ] **Step 1: Extend workflow path filters and sparse checkout**

Add:

```yaml
- 'tools/ModelInspection.LlamaSharpSpike.TestSupport/**'
- 'tools/ModelInspection.LlamaSharpSpike.NativeIntegrationTests/**'
- 'docs/testing/LLamaSharp-Runtime-Test-Coverage-Matrix.md'
```

Sparse checkout must include the same directories.

- [ ] **Step 2: Publish the spike before process tests**

```yaml
- name: Publish the Windows x64 feasibility tool
  run: |
    $publishDirectory = Join-Path $env:RUNNER_TEMP 'llamasharp-spike-publish'
    dotnet publish "$env:SPIKE_PROJECT" `
      --configuration Release `
      --runtime win-x64 `
      --self-contained false `
      --output $publishDirectory

    "LLAMASHARP_SPIKE_PUBLISH_DIR=$publishDirectory" |
      Out-File `
        -FilePath $env:GITHUB_ENV `
        -Encoding utf8 `
        -Append
```

- [ ] **Step 3: Run deterministic and native integration suites separately**

```yaml
- name: Run deterministic LLamaSharp tests
  run: |
    dotnet test "$env:SPIKE_TEST_PROJECT" `
      --configuration Release `
      --runtime win-x64 `
      --minimum-expected-tests 1

- name: Run hosted native integration tests
  run: |
    dotnet test `
      "tools/ModelInspection.LlamaSharpSpike.NativeIntegrationTests/ModelInspection.LlamaSharpSpike.NativeIntegrationTests.csproj" `
      --configuration Release `
      --runtime win-x64 `
      --minimum-expected-tests 4
```

- [ ] **Step 4: Upload only model-free evidence**

Retain:

```yaml
path: artifacts/model-inspection/llamasharp/**
```

Add a pre-upload PowerShell gate that fails if any file ends in `.gguf`.

- [ ] **Step 5: Create the coverage matrix**

Use columns:

```markdown
| Risk ID | Failure scenario | Layer | Automated test | Expected result/code | CI tier | Evidence | Status / deferral |
```

Populate every Tier 1 case from Tasks 1–9 and add explicit Tier 2 rows marked `Planned — Tier 2 implementation plan`.

- [ ] **Step 6: Run the complete Tier 1 verification locally**

```powershell
$DeterministicProject =
  "tools/ModelInspection.LlamaSharpSpike.Tests/ModelInspection.LlamaSharpSpike.Tests.csproj"

$NativeIntegrationProject =
  "tools/ModelInspection.LlamaSharpSpike.NativeIntegrationTests/ModelInspection.LlamaSharpSpike.NativeIntegrationTests.csproj"

$PublishDirectory = Join-Path $env:TEMP "GraniteEdgeAI-LlamaSharp-Publish"

dotnet restore $DeterministicProject --runtime win-x64

dotnet test $DeterministicProject `
  --configuration Release `
  --runtime win-x64 `
  --minimum-expected-tests 1

dotnet publish `
  "tools/ModelInspection.LlamaSharpSpike/ModelInspection.LlamaSharpSpike.csproj" `
  --configuration Release `
  --runtime win-x64 `
  --self-contained false `
  --output $PublishDirectory

$env:LLAMASHARP_SPIKE_PUBLISH_DIR = $PublishDirectory

dotnet test $NativeIntegrationProject `
  --configuration Release `
  --runtime win-x64 `
  --minimum-expected-tests 4
```

Expected: every command exits `0`.

- [ ] **Step 7: Commit Tier 1 automation and documentation**

```powershell
git add .github/workflows/llamasharp-feasibility-smoke.yml `
        docs/testing/LLamaSharp-Runtime-Test-Coverage-Matrix.md `
        tools `
        "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/README.md"

git commit -m "ci(model-inspection): complete Tier 1 LLamaSharp runtime coverage"
```

## Tier 1 Definition of Done

- [ ] Deterministic suite covers every pure branch listed in the approved design.
- [ ] Runtime dependency policy reads actual project files and rejects drift.
- [ ] Native progress cannot serialize NaN or out-of-range values.
- [ ] Cancellation cannot conceal changed or unverifiable model integrity.
- [ ] VocabOnly collector cannot reintroduce known unsafe native getters.
- [ ] JSON failures cannot replace previously valid evidence.
- [ ] All native entry points execute in child processes during tests.
- [ ] Published CPU smoke succeeds on GitHub-hosted Windows x64.
- [ ] Missing and corrupted native backend cases are contained and recorded.
- [ ] Normal CI requires no real model and uploads no `.gguf` file.
- [ ] Coverage matrix links every Tier 1 risk to a test and every deferred item to Tier 2.
- [ ] READMEs describe actual test architecture and evidence boundaries.
