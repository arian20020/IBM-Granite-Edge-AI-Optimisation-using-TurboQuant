# LLamaSharp Tier 2 Real-Model Tests Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an opt-in trusted Windows test suite that validates the exact LLamaSharp CPU runtime against the controlled Granite 4.1 3B model, cancellation, repeatability, malformed GGUF inputs, file-access failures, privacy, networking observations, disposal, and original-file integrity.

**Architecture:** Run every native/model scenario as a child process through the TestSupport project delivered by the Tier 1 plan. Keep the 2 GB model outside Git and discover it only through trusted-runner configuration. Use a dedicated Microsoft.Testing.Platform project and a manual `workflow_dispatch` workflow restricted to the repository-owned self-hosted Windows x64 Intel target.

**Tech Stack:** .NET 8, C# 12, MSTest 4.3.2 on Microsoft.Testing.Platform, LLamaSharp 0.27.0, LLamaSharp.Backend.Cpu 0.27.0, Windows PowerShell, GitHub Actions self-hosted Windows runner.

## Global Constraints

- Tier 1 implementation and verification must be complete before this plan starts.
- Target branch: `feature/model-inspection`.
- Trusted runner labels: `self-hosted`, `Windows`, `X64`, `workbook05`, `intel-target`.
- Workflow trigger: `workflow_dispatch` only.
- Never execute this workflow from a fork pull request.
- Controlled model filename: `granite-4.1-3b-Q4_K_M.gguf`.
- Controlled model SHA-256: `662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29`.
- Controlled model byte length: `2099501664`.
- Expected architecture: `granite`.
- Expected model name: `Granite 4.1 3b`.
- Expected file type: `15`.
- Expected quantisation version: `2`.
- Expected tokenizer model: `gpt2`.
- Expected context size: `131072`.
- Expected embedding size: `2560`.
- Expected layer count: `40`.
- Expected attention head count: `40`.
- Expected KV-head count: `8`.
- Expected metadata count: `31`.
- Expected vocabulary count: `100352`.
- Do not store the full model path in committed files, JSON evidence, stdout, stderr, or uploaded artifacts.
- Do not upload or copy the GGUF into repository artifacts.
- Use `VocabOnly = true`, `GpuLayerCount = 0`, CUDA disabled, and Vulkan disabled.
- Do not create a context, KV cache, inference request, Vulkan backend, or TurboQuant route.
- Hash every model or fixture before and after each child-process scenario.
- A missing model path or expected hash must fail the workflow; no silent skips.
- A child-process native abort must be recorded, not allowed to terminate the MSTest host.
- Use exact timeouts and kill the complete child process tree on timeout.
- Upload evidence only after a pre-upload scan proves no `.gguf` file is present.
- Update the nearest READMEs and coverage matrix with actual evidence rather than source-presence claims.

---

### Task 1: Create the real-model integration project and controlled-model manifest

**Files:**
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.csproj`
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/README.md`
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/ControlledModels/granite-4.1-3b-q4-k-m.json`
- Create: `tools/ModelInspection.LlamaSharpSpike.TestSupport/ControlledModelManifest.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.TestSupport/ControlledModelConfiguration.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/Support/RealModelTestContext.cs`

**Interfaces:**
- Consumes: `LLAMASHARP_SPIKE_PUBLISH_DIR`, `GRANITE_TEST_MODEL_PATH`, and the committed model manifest.
- Produces: a fail-fast `RealModelTestContext` containing the published executable, model path, expected identity, and per-test evidence root.

- [ ] **Step 1: Create the MTP-enabled real-model test project**

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

  <ItemGroup>
    <None Include="ControlledModels\granite-4.1-3b-q4-k-m.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add the controlled model manifest**

```json
{
  "id": "granite-4.1-3b-q4-k-m",
  "fileName": "granite-4.1-3b-Q4_K_M.gguf",
  "sha256": "662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29",
  "lengthBytes": 2099501664,
  "architecture": "granite",
  "modelName": "Granite 4.1 3b",
  "fileType": "15",
  "quantizationVersion": "2",
  "tokenizerModel": "gpt2",
  "contextSize": 131072,
  "embeddingSize": 2560,
  "layerCount": 40,
  "headCount": 40,
  "kvHeadCount": 8,
  "metadataCount": 31,
  "vocabularyCount": 100352,
  "chatTemplatePresent": true,
  "tokenizerSmokeTokenCount": 1
}
```

- [ ] **Step 3: Implement manifest loading**

```csharp
namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;

public sealed record ControlledModelManifest
{
    public required string Id { get; init; }
    public required string FileName { get; init; }
    public required string Sha256 { get; init; }
    public required long LengthBytes { get; init; }
    public required string Architecture { get; init; }
    public required string ModelName { get; init; }
    public required string FileType { get; init; }
    public required string QuantizationVersion { get; init; }
    public required string TokenizerModel { get; init; }
    public required int ContextSize { get; init; }
    public required int EmbeddingSize { get; init; }
    public required int LayerCount { get; init; }
    public required int HeadCount { get; init; }
    public required int KvHeadCount { get; init; }
    public required int MetadataCount { get; init; }
    public required int VocabularyCount { get; init; }
    public required bool ChatTemplatePresent { get; init; }
    public required int TokenizerSmokeTokenCount { get; init; }

    public static ControlledModelManifest Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return JsonSerializer.Deserialize<ControlledModelManifest>(
                   File.ReadAllText(path),
                   new JsonSerializerOptions
                   {
                       PropertyNameCaseInsensitive = true
                   })
               ?? throw new InvalidDataException(
                   "Controlled model manifest could not be deserialized.");
    }
}
```

- [ ] **Step 4: Implement fail-fast environment configuration**

```csharp
namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;

public sealed record ControlledModelConfiguration
{
    public const string ModelPathEnvironmentVariable =
        "GRANITE_TEST_MODEL_PATH";

    public required string ModelPath { get; init; }
    public required ControlledModelManifest Manifest { get; init; }

    public static ControlledModelConfiguration Load(
        string manifestPath)
    {
        ControlledModelManifest manifest =
            ControlledModelManifest.Load(manifestPath);

        string? configuredPath = Environment.GetEnvironmentVariable(
            ModelPathEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException(
                $"{ModelPathEnvironmentVariable} is required.");
        }

        string fullPath = Path.GetFullPath(configuredPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "The controlled Granite model was not found.",
                fullPath);
        }

        return new ControlledModelConfiguration
        {
            ModelPath = fullPath,
            Manifest = manifest
        };
    }
}
```

- [ ] **Step 5: Add `RealModelTestContext`**

```csharp
namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.Support;

internal sealed class RealModelTestContext : IDisposable
{
    private readonly TemporaryDirectory _evidenceDirectory;

    internal RealModelTestContext()
    {
        string manifestPath = Path.Combine(
            AppContext.BaseDirectory,
            "ControlledModels",
            "granite-4.1-3b-q4-k-m.json");

        Model = ControlledModelConfiguration.Load(manifestPath);
        PublishedProbeDirectory =
            PublishedProbeLocation.RequireFromEnvironment();
        _evidenceDirectory = new TemporaryDirectory(
            "llamasharp-real-model-evidence");
    }

    internal ControlledModelConfiguration Model { get; }
    internal string PublishedProbeDirectory { get; }
    internal string EvidenceDirectory => _evidenceDirectory.Path;

    internal string CreateEvidencePath(string scenario)
    {
        string directory = Path.Combine(
            EvidenceDirectory,
            scenario,
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "result.json");
    }

    public void Dispose() => _evidenceDirectory.Dispose();
}
```

Use the `TemporaryDirectory` helper from TestSupport; if Tier 1 kept it inside only the deterministic project, move the implementation into TestSupport and reference it from both projects.

- [ ] **Step 6: Add precondition tests**

Write tests that deliberately clear `GRANITE_TEST_MODEL_PATH` and assert configuration throws. Add tests for missing file, mismatched filename, wrong length, and wrong hash using a temporary file. These tests do not call native code.

- [ ] **Step 7: Commit project scaffold and documentation**

```powershell
git add tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests `
        tools/ModelInspection.LlamaSharpSpike.TestSupport
git commit -m "test(model-inspection): scaffold trusted real-model tests"
```

---

### Task 2: Add controlled Granite success-contract and repeatability tests

**Files:**
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/RealModel/GraniteVocabOnlySuccessTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/RealModel/GraniteVocabOnlyRepeatabilityTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.TestSupport/ModelFileHash.cs`
- Expand: `tools/ModelInspection.LlamaSharpSpike.TestSupport/EvidenceAssertions.cs`

**Interfaces:**
- Consumes: child-process runner, model configuration, manifest, and published probe.
- Produces: exact success and three-run repeatability evidence.

- [ ] **Step 1: Add an independent file-hash helper**

```csharp
using System.Security.Cryptography;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;

public static class ModelFileHash
{
    public static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        using SHA256 sha256 = SHA256.Create();
        byte[] hash = await sha256.ComputeHashAsync(
            stream,
            cancellationToken);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
```

- [ ] **Step 2: Add exact evidence assertions**

Implement methods:

```csharp
public static void AssertSuccessfulGraniteVocabOnly(
    JsonElement root,
    ControlledModelManifest manifest)
```

Require:

```text
schemaVersion = 1.1
completionStatus = Succeeded
succeeded = true
vocabOnlyRequested = true
gpuLayerCount = 0
managedPackageVersion = 0.27.0
backendPackageVersion = 0.27.0
expectedLlamaCppCommit = 3f7c29d...
selectedBackend.usesCuda = false
selectedBackend.usesVulkan = false
architecture/model/file type/quantisation/tokenizer match manifest
context/embedding/layers/heads/KV heads match manifest
metadata/vocabulary counts match manifest
tokenizer smoke succeeds with 1 token
chat template present
native handle closed = true
integrity preserved = true
before and after SHA-256 match manifest
progress values finite and in 0..1
failureCode/failureType/failureMessage are null
```

- [ ] **Step 3: Add the success child-process test**

```csharp
[TestClass]
[TestCategory("RealModelIntegration")]
public sealed class GraniteVocabOnlySuccessTests
{
    [TestMethod]
    public async Task Run_WithControlledGranite_ReturnsVerifiedEvidenceAndPreservesModel()
    {
        using var context = new RealModelTestContext();
        string expectedHash = await ModelFileHash.ComputeSha256Async(
            context.Model.ModelPath,
            CancellationToken.None);
        Assert.AreEqual(context.Model.Manifest.Sha256, expectedHash);

        using TemporaryProbeSandbox sandbox = TemporaryProbeSandbox.Create(
            context.PublishedProbeDirectory);
        string evidencePath = context.CreateEvidencePath("success");

        ProbeExecutionResult process = await new ProbeProcessRunner().RunAsync(
            sandbox.CreateRequest(
                new[]
                {
                    "--model", context.Model.ModelPath,
                    "--output", evidencePath
                },
                TimeSpan.FromMinutes(2)),
            CancellationToken.None);

        Assert.AreEqual(ProcessTerminationKind.Exited, process.TerminationKind);
        Assert.AreEqual(0, process.ExitCode);

        using JsonDocument evidence = EvidenceAssertions.LoadJson(evidencePath);
        EvidenceAssertions.AssertSuccessfulGraniteVocabOnly(
            evidence.RootElement,
            context.Model.Manifest);

        string actualHash = await ModelFileHash.ComputeSha256Async(
            context.Model.ModelPath,
            CancellationToken.None);
        Assert.AreEqual(expectedHash, actualHash);

        EvidenceAssertions.AssertDoesNotContainCanonicalPath(
            File.ReadAllText(evidencePath),
            context.Model.ModelPath);
        EvidenceAssertions.AssertDoesNotContainCanonicalPath(
            process.StandardOutput,
            context.Model.ModelPath);
        EvidenceAssertions.AssertDoesNotContainCanonicalPath(
            process.StandardError,
            context.Model.ModelPath);
    }
}
```

- [ ] **Step 4: Add three-run repeatability test**

Run the same child process three times sequentially with unique JSON paths. Assert every run succeeds and compare stable fields:

```text
architecture
model name
context
embedding
layers
heads
KV heads
metadata count
vocabulary count
chat-template hash
before/after model hash
```

Do not assert exact load duration, working set, peak memory, or progress sample count; only require valid nonnegative observations.

- [ ] **Step 5: Confirm cleanup after all runs**

Assert no file matching `*.tmp-*` remains under the test evidence root or sandbox. Assert every result reports `nativeHandleClosedAfterDispose = true`.

- [ ] **Step 6: Run locally on the interactive machine and commit**

```powershell
$PublishDirectory = Join-Path $env:TEMP "GraniteEdgeAI-LlamaSharp-Publish"

dotnet publish `
  "tools/ModelInspection.LlamaSharpSpike/ModelInspection.LlamaSharpSpike.csproj" `
  --configuration Release `
  --runtime win-x64 `
  --self-contained false `
  --output $PublishDirectory

$env:LLAMASHARP_SPIKE_PUBLISH_DIR = $PublishDirectory
$env:GRANITE_TEST_MODEL_PATH =
  "C:\Users\Arian\Downloads\granite-4.1-3b-Q4_K_M.gguf"

dotnet test `
  "tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.csproj" `
  --configuration Release `
  --runtime win-x64 `
  --filter "FullyQualifiedName~RealModel" `
  --minimum-expected-tests 2
```

Expected: both tests pass.

```powershell
git add tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests `
        tools/ModelInspection.LlamaSharpSpike.TestSupport
git commit -m "test(model-inspection): verify controlled Granite repeatability"
```

---

### Task 3: Verify whole-operation and native-load cancellation

**Files:**
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/Cancellation/WholeOperationCancellationTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/Cancellation/NativeLoadCancellationTests.cs`
- Expand: `tools/ModelInspection.LlamaSharpSpike.TestSupport/EvidenceAssertions.cs`

**Interfaces:**
- Consumes: `--cancel-after-ms` and Tier 1 `--cancel-native-after-ms`.
- Produces: evidence-backed cancellation at both operation scopes.

- [ ] **Step 1: Add shared cancellation assertions**

```csharp
public static void AssertCancelled(
    JsonElement root,
    string expectedHash)
{
    Assert.AreEqual(
        "Cancelled",
        root.GetProperty("completionStatus").GetString());
    Assert.AreEqual(
        "MI-PROBE-CANCELLED",
        root.GetProperty("failureCode").GetString());
    Assert.IsFalse(root.GetProperty("succeeded").GetBoolean());
    Assert.AreEqual(
        expectedHash,
        root.GetProperty("beforeSnapshot").GetProperty("sha256").GetString());
    Assert.AreEqual(
        expectedHash,
        root.GetProperty("afterSnapshot").GetProperty("sha256").GetString());
    Assert.IsTrue(
        root.GetProperty("integrity").GetProperty("isPreserved").GetBoolean());
}
```

- [ ] **Step 2: Add whole-operation cancellation test**

Launch:

```text
--model <path>
--cancel-after-ms 1
--output <unique-json>
```

Require:

```text
termination = Exited
exit code = 3
JSON exists
completion = Cancelled
failure = MI-PROBE-CANCELLED
model hash unchanged
no canonical path leak
```

Because the timer starts before hashing, no selected backend or native handle is required in this case.

- [ ] **Step 3: Add native-load-scoped cancellation test**

Launch:

```text
--model <path>
--cancel-native-after-ms 1
--output <unique-json>
```

Require:

```text
termination = Exited
exit code = 3
JSON exists
completion = Cancelled
failure = MI-PROBE-CANCELLED
selected CPU backend exists
CUDA false
Vulkan false
model hash unchanged
if nativeHandleClosedAfterDispose is non-null, it is true
```

The child process must not return `MI-PROBE-MODEL-LOAD-FAILED` for a cancellation token.

- [ ] **Step 4: Repeat native-load cancellation three times**

Use three unique JSON paths. All runs must return exit `3`. This catches timing-dependent cancellation regressions.

- [ ] **Step 5: Run and commit**

```powershell
dotnet test `
  "tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.csproj" `
  --configuration Release `
  --runtime win-x64 `
  --filter "FullyQualifiedName~Cancellation" `
  --minimum-expected-tests 3

git add tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests
git commit -m "test(model-inspection): verify LLamaSharp cancellation paths"
```

---

### Task 4: Run every committed malformed GGUF fixture in a contained child process

**Files:**
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/MalformedModels/MalformedFixtureCase.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/MalformedModels/MalformedFixtureData.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/MalformedModels/MalformedModelProcessTests.cs`
- Modify: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.csproj`

**Interfaces:**
- Consumes: all files under `tests/TestFixtures/Malformed` and `tests/TestFixtures/fixture-manifest.json`.
- Produces: one independently reported MSTest case per committed malformed fixture.

- [ ] **Step 1: Add repository fixture files as non-output inputs**

The project must locate the repository through `RepositoryPaths.FindRoot()` from TestSupport; do not copy the fixture binaries into test output or artifacts.

- [ ] **Step 2: Implement dynamic fixture discovery**

```csharp
internal sealed record MalformedFixtureCase(
    string Id,
    string Path,
    long ExpectedLength,
    string ExpectedSha256);

internal static class MalformedFixtureData
{
    public static IEnumerable<object[]> GetCases()
    {
        string root = RepositoryPaths.FindRoot();
        string manifestPath = Path.Combine(
            root,
            "tests",
            "TestFixtures",
            "fixture-manifest.json");

        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(manifestPath));

        foreach (JsonElement fixture in document.RootElement
                     .GetProperty("fixtures")
                     .EnumerateArray())
        {
            string relativePath = fixture.GetProperty("path").GetString()!;
            if (!relativePath.StartsWith(
                    "Malformed/",
                    StringComparison.Ordinal))
            {
                continue;
            }

            yield return new object[]
            {
                new MalformedFixtureCase(
                    fixture.GetProperty("id").GetString()!,
                    Path.Combine(
                        root,
                        "tests",
                        "TestFixtures",
                        relativePath.Replace('/', Path.DirectorySeparatorChar)),
                    fixture.GetProperty("bytes").GetInt64(),
                    fixture.GetProperty("sha256").GetString()!)
            };
        }
    }
}
```

If the manifest property names differ, adapt the reader to the actual manifest during implementation and add a manifest-shape unit test; do not hardcode a separate second fixture list.

- [ ] **Step 3: Add the contained malformed-fixture process test**

```csharp
[TestMethod]
[DynamicData(
    nameof(MalformedFixtureData.GetCases),
    typeof(MalformedFixtureData),
    DynamicDataSourceType.Method)]
public async Task Run_WithMalformedFixture_IsContainedAndPreservesFixture(
    MalformedFixtureCase fixture)
{
    using var context = new RealModelTestContext();
    using TemporaryProbeSandbox sandbox = TemporaryProbeSandbox.Create(
        context.PublishedProbeDirectory);

    string beforeHash = await ModelFileHash.ComputeSha256Async(
        fixture.Path,
        CancellationToken.None);
    Assert.AreEqual(fixture.ExpectedSha256, beforeHash);
    Assert.AreEqual(fixture.ExpectedLength, new FileInfo(fixture.Path).Length);

    string evidencePath = context.CreateEvidencePath(
        $"malformed-{fixture.Id}");

    ProbeExecutionResult process = await new ProbeProcessRunner().RunAsync(
        sandbox.CreateRequest(
            new[]
            {
                "--model", fixture.Path,
                "--output", evidencePath
            },
            TimeSpan.FromSeconds(30)),
        CancellationToken.None);

    Assert.AreNotEqual(ProcessTerminationKind.StartFailed, process.TerminationKind);
    Assert.AreNotEqual(0, process.ExitCode);

    string afterHash = await ModelFileHash.ComputeSha256Async(
        fixture.Path,
        CancellationToken.None);
    Assert.AreEqual(beforeHash, afterHash);

    if (File.Exists(evidencePath))
    {
        using JsonDocument evidence = EvidenceAssertions.LoadJson(evidencePath);
        JsonElement root = evidence.RootElement;
        Assert.AreEqual("Failed", root.GetProperty("completionStatus").GetString());
        Assert.IsTrue(root.GetProperty("integrity").GetProperty("isPreserved").GetBoolean());
        Assert.IsFalse(root.TryGetProperty("modelOutcome", out _));
    }

    EvidenceAssertions.AssertDoesNotContainCanonicalPath(
        process.StandardOutput,
        fixture.Path);
    EvidenceAssertions.AssertDoesNotContainCanonicalPath(
        process.StandardError,
        fixture.Path);
}
```

A native abort without JSON is acceptable only as a contained nonzero child-process result with unchanged fixture hash. Record its exit code and streams in test-result attachments or workflow evidence.

- [ ] **Step 4: Add an additional random-bytes `.gguf` case**

Create a 4096-byte file with deterministic SHA-256 input generated by:

```csharp
byte[] bytes = Enumerable.Range(0, 4096)
    .Select(index => (byte)(index % 251))
    .ToArray();
```

Run through the same containment contract.

- [ ] **Step 5: Run the complete malformed matrix and commit**

```powershell
dotnet test `
  "tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.csproj" `
  --configuration Release `
  --runtime win-x64 `
  --filter "FullyQualifiedName~MalformedModels"

git add tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests
git commit -m "test(model-inspection): contain malformed GGUF runtime failures"
```

Expected: one test case per manifest entry under `Malformed/`, plus one deterministic random-byte case.

---

### Task 5: Add file-access and evidence-write failure process tests

**Files:**
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/FileAccess/MissingModelProcessTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/FileAccess/DirectoryModelProcessTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/FileAccess/LockedModelProcessTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/FileAccess/LockedOutputProcessTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/FileAccess/InvalidOutputParentProcessTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/FileAccess/OutputEqualsModelProcessTests.cs`

**Interfaces:**
- Consumes: child-process runner and temporary test fixtures.
- Produces: controlled evidence for model-access and evidence-write failures without changing the real Granite model.

- [ ] **Step 1: Add missing-model and directory-model tests**

Missing model requires:

```text
exit 1
JSON exists
MI-OP-MODEL-FILE-NOT-FOUND
no native backend required
no canonical path leak
```

Directory supplied as model requires nonzero exit and a controlled file-related code. If current behavior maps it to `MI-OP-MODEL-FILE-NOT-FOUND`, preserve and document that exact contract.

- [ ] **Step 2: Add locked-model test using a temporary fixture**

Create a small file and hold:

```csharp
using FileStream lockStream = new(
    fixturePath,
    FileMode.Open,
    FileAccess.ReadWrite,
    FileShare.None);
```

Launch child. Require nonzero exit, parent survival, unchanged fixture hash, and `MI-OP-MODEL-FILE-IO` or `MI-OP-MODEL-FILE-ACCESS-DENIED` according to actual Windows exception mapping.

- [ ] **Step 3: Add locked-output test**

Create an existing valid JSON output and hold it with `FileShare.None`. Launch a no-model native smoke pointing to the locked output. Require:

```text
exit 1
stderr identifies MI-OP-EVIDENCE-WRITE-FAILED
existing JSON remains byte-for-byte unchanged
no temporary file remains
```

- [ ] **Step 4: Add invalid output-parent test**

Create a regular file named `parent-file` and use:

```text
<parent-file>\result.json
```

as output. Require exit `1`, `MI-OP-EVIDENCE-WRITE-FAILED`, and no modification to the model or fixture.

- [ ] **Step 5: Add output-equals-model test**

Launch with the same canonical path for `--model` and `--output`. Require exit `2`; the process must refuse before hashing/native work. Hash the model before and after.

- [ ] **Step 6: Run and commit**

```powershell
dotnet test `
  "tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.csproj" `
  --configuration Release `
  --runtime win-x64 `
  --filter "FullyQualifiedName~FileAccess" `
  --minimum-expected-tests 6

git add tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests
git commit -m "test(model-inspection): cover runtime file access failures"
```

---

### Task 6: Add evidence privacy, artifact exclusion, and network-observation tests

**Files:**
- Create: `tools/ModelInspection.LlamaSharpSpike.TestSupport/SocketObservation.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/Security/EvidencePrivacyTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/Security/NetworkObservationTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/Security/ArtifactExclusionTests.cs`

**Interfaces:**
- Consumes: child process ID, evidence directory, controlled model path, and model hash.
- Produces: strong “no listener/network use observed” and “model excluded from evidence” results.

- [ ] **Step 1: Implement process-owned TCP observation**

`SocketObservation.ObserveAsync(int processId, CancellationToken)` invokes Windows PowerShell with:

```powershell
Get-NetTCPConnection -OwningProcess <pid> -ErrorAction SilentlyContinue |
  Select-Object State, LocalAddress, LocalPort, RemoteAddress, RemotePort |
  ConvertTo-Json -Compress
```

Return a project-owned list:

```csharp
public sealed record ObservedTcpConnection(
    string State,
    string LocalAddress,
    int LocalPort,
    string RemoteAddress,
    int RemotePort);
```

Poll every 25 ms until the child exits or 2 seconds elapse. This polling delay is part of an explicit observation loop, not an arbitrary test sleep.

- [ ] **Step 2: Add evidence privacy test**

After a successful real-model run, inspect JSON, stdout, and stderr. Require absence of:

```text
full canonical model path
parent directory path
"tokenizer.chat_template" full value
"modelPath" JSON property
"chatTemplateText" JSON property
```

Require presence of filename and `canonicalPathSha256` because those are approved evidence.

- [ ] **Step 3: Add network-observation test**

Launch the real-model child process and concurrently observe its PID. Require no connection with state `Listen`, `Established`, `SynSent`, or `SynReceived` owned by the process. Record all observations in evidence even when the list is empty.

The test claim is:

```text
No TCP listener or established/outbound connection was observed for the probe process.
```

It is not a substitute for the separately deferred physically disconnected-machine test.

- [ ] **Step 4: Add artifact exclusion test**

Recursively scan the evidence root. Fail when:

```text
any filename ends with .gguf
any evidence file length equals 2099501664
any file SHA-256 equals the controlled model SHA-256
```

- [ ] **Step 5: Run and commit**

```powershell
dotnet test `
  "tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.csproj" `
  --configuration Release `
  --runtime win-x64 `
  --filter "FullyQualifiedName~Security" `
  --minimum-expected-tests 3

git add tools/ModelInspection.LlamaSharpSpike.TestSupport `
        tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests
git commit -m "test(model-inspection): verify probe privacy and network boundary"
```

---

### Task 7: Add the trusted self-hosted workflow

**Files:**
- Create: `.github/workflows/llamasharp-real-model-integration.yml`
- Modify: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/README.md`

**Interfaces:**
- Consumes repository variable: `GRANITE_TEST_MODEL_PATH`.
- Produces test/evidence artifacts without uploading the model.

- [ ] **Step 1: Create a manual-only trusted workflow**

```yaml
name: LLamaSharp real-model integration

on:
  workflow_dispatch:

permissions:
  contents: read

concurrency:
  group: llamasharp-real-model-intel-target
  cancel-in-progress: false

jobs:
  real-model-integration:
    if: github.repository == 'arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant'
    runs-on:
      - self-hosted
      - Windows
      - X64
      - workbook05
      - intel-target
    timeout-minutes: 45

    defaults:
      run:
        shell: powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -Command ". '{0}'"

    env:
      SPIKE_PROJECT: tools/ModelInspection.LlamaSharpSpike/ModelInspection.LlamaSharpSpike.csproj
      REAL_MODEL_TEST_PROJECT: tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.csproj
      GRANITE_TEST_MODEL_PATH: ${{ vars.GRANITE_TEST_MODEL_PATH }}
      EXPECTED_GRANITE_SHA256: 662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29
```

- [ ] **Step 2: Add checkout and fail-fast model preflight**

Preflight verifies:

```powershell
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($env:GRANITE_TEST_MODEL_PATH)) {
    throw 'Repository variable GRANITE_TEST_MODEL_PATH is required.'
}

if (-not (Test-Path -LiteralPath $env:GRANITE_TEST_MODEL_PATH -PathType Leaf)) {
    throw 'Configured Granite test model is not readable by the runner service.'
}

$actualHash = (
    Get-FileHash -LiteralPath $env:GRANITE_TEST_MODEL_PATH -Algorithm SHA256
).Hash.ToLowerInvariant()

if ($actualHash -ne $env:EXPECTED_GRANITE_SHA256) {
    throw "Controlled Granite hash mismatch: $actualHash"
}

if ((Get-Item -LiteralPath $env:GRANITE_TEST_MODEL_PATH).Length -ne 2099501664) {
    throw 'Controlled Granite byte length mismatch.'
}
```

- [ ] **Step 3: Restore, publish, and export the publish directory**

```yaml
- name: Restore trusted integration tests
  run: |
    dotnet restore "$env:REAL_MODEL_TEST_PROJECT" --runtime win-x64

- name: Publish feasibility executable
  run: |
    $publishDirectory = Join-Path $env:RUNNER_TEMP 'llamasharp-spike-publish'
    Remove-Item $publishDirectory -Recurse -Force -ErrorAction SilentlyContinue

    dotnet publish "$env:SPIKE_PROJECT" `
      --configuration Release `
      --runtime win-x64 `
      --self-contained false `
      --output $publishDirectory

    "LLAMASHARP_SPIKE_PUBLISH_DIR=$publishDirectory" |
      Out-File -FilePath $env:GITHUB_ENV -Encoding utf8 -Append
```

- [ ] **Step 4: Run the complete trusted suite**

```yaml
- name: Run real-model and hostile-input integration tests
  run: |
    dotnet test "$env:REAL_MODEL_TEST_PROJECT" `
      --configuration Release `
      --runtime win-x64 `
      --minimum-expected-tests 10
```

The final minimum count must be raised to the actual discovered count after implementation; it must not remain `10` if more tests are created.

- [ ] **Step 5: Copy only test evidence into a clean upload directory**

The integration test project writes under:

```text
$env:RUNNER_TEMP\llamasharp-real-model-evidence
```

Before upload:

```powershell
$evidenceRoot = Join-Path $env:RUNNER_TEMP 'llamasharp-real-model-evidence'

$forbiddenModels = Get-ChildItem `
    -LiteralPath $evidenceRoot `
    -Recurse `
    -File `
    -ErrorAction SilentlyContinue |
  Where-Object {
      $_.Extension -ieq '.gguf' -or
      $_.Length -eq 2099501664
  }

if ($forbiddenModels) {
    throw 'A model-like file was found in the upload evidence directory.'
}
```

- [ ] **Step 6: Upload evidence on success or failure**

```yaml
- name: Upload trusted integration evidence
  if: always()
  uses: actions/upload-artifact@bbbca2ddaa5d8feaa63e36b76fdaad77386f024f
  with:
    name: llamasharp-real-model-${{ github.run_id }}-${{ github.run_attempt }}
    path: ${{ runner.temp }}/llamasharp-real-model-evidence/**
    if-no-files-found: warn
    retention-days: 30
```

- [ ] **Step 7: Document runner setup**

The README must instruct the repository owner to configure:

```text
Repository variable:
GRANITE_TEST_MODEL_PATH

Recommended service-readable location:
C:\ProgramData\GraniteEdgeAI\TestModels\granite-4.1-3b-Q4_K_M.gguf
```

The file must be readable by the runner service account and must not be inside the repository checkout.

- [ ] **Step 8: Commit workflow and README**

```powershell
git add .github/workflows/llamasharp-real-model-integration.yml `
        tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/README.md
git commit -m "ci(model-inspection): add trusted LLamaSharp real-model suite"
```

---

### Task 8: Reconcile the coverage matrix and source-adjacent documentation

**Files:**
- Modify: `docs/testing/LLamaSharp-Runtime-Test-Coverage-Matrix.md`
- Modify: `tools/README.md`
- Modify: `tools/ModelInspection.LlamaSharpSpike/README.md`
- Modify: `tools/ModelInspection.LlamaSharpSpike/ModelProbe/README.md`
- Modify: `tools/ModelInspection.LlamaSharpSpike.TestSupport/README.md`
- Modify: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/README.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/README.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/README.md`
- Modify: `docs/superpowers/plans/2026-08-04-llamasharp-vocab-only-model-probe.md`

**Interfaces:**
- Produces: evidence-backed coverage state and explicit deferrals.

- [ ] **Step 1: Mark every implemented Tier 2 scenario in the matrix**

Update status only after actual test output exists. Required entries:

```text
controlled Granite success
three-run repeatability
whole-operation cancellation
native-load cancellation
all committed malformed fixtures
random-byte input
missing model
directory model
locked model
locked output
invalid output parent
output equals model
path privacy
artifact exclusion
no TCP listener/established socket observed
before/after integrity for every model/fixture scenario
```

- [ ] **Step 2: Record explicit deferrals**

Keep these as deferred with reasons and future evidence routes:

```text
physical network disconnection
true x86 native-backend mismatch
completely full disk
valid real GGUF without chat template
Windows Job Object hard memory limit
power loss or operating-system termination
full CPU allocation/context/generation
Vulkan
TurboQuant
WinUI Cancel button
```

- [ ] **Step 3: Update current-state READMEs**

Document the difference between:

```text
verified runtime feasibility
production ILlamaModelProbe not yet implemented
WinUI cancellation not yet connected
full CPU/Vulkan/TurboQuant gates not yet run
```

- [ ] **Step 4: Run the complete Tier 2 suite on the trusted runner**

Record:

```text
workflow run ID
target runner identity
application commit
number of tests discovered/passed/failed/skipped
controlled model hash
artifact name
any contained native-abort exit codes
```

- [ ] **Step 5: Verify uploaded artifact contents**

Download the artifact and confirm:

```text
no .gguf file
no file with controlled model length
no file with controlled model SHA-256
no canonical local model path in any text or JSON
```

- [ ] **Step 6: Commit final documentation after evidence review**

```powershell
git add docs/testing/LLamaSharp-Runtime-Test-Coverage-Matrix.md `
        docs/superpowers/plans/2026-08-04-llamasharp-vocab-only-model-probe.md `
        tools `
        "IBM Granite with TurboQuant (Intel)/Features/README.md" `
        "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/README.md"

git commit -m "docs(model-inspection): record comprehensive runtime test evidence"
```

## Tier 2 Definition of Done

- [ ] Trusted runner fails loudly when model configuration is absent or wrong.
- [ ] Controlled Granite success contract passes with exact expected evidence.
- [ ] Three sequential probes pass and close every native handle.
- [ ] Whole-operation cancellation returns `Cancelled`, code `MI-PROBE-CANCELLED`, exit `3`, and preserved integrity.
- [ ] Native-load cancellation returns the same controlled result without becoming a load failure.
- [ ] Every committed malformed fixture is contained in a child process and remains unchanged.
- [ ] Missing, directory, locked-model, locked-output, invalid-output-parent, and output-equals-model cases are controlled.
- [ ] No canonical model path or full chat template appears in JSON, stdout, stderr, or artifacts.
- [ ] No TCP listener or established/outbound socket is observed for the probe process.
- [ ] No `.gguf`, model-sized file, or model-hash file appears in uploaded evidence.
- [ ] Coverage matrix links every automated scenario to its test and evidence tier.
- [ ] Remaining non-automated conditions have explicit reasons and future routes.
- [ ] READMEs state exactly what is verified and what remains outside this slice.
