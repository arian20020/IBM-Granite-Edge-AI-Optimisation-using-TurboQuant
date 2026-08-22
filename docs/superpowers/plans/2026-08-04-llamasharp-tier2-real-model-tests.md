# LLamaSharp Tier 2 Real-Model Tests Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an opt-in trusted Windows test suite that validates the exact LLamaSharp CPU runtime against the controlled Granite 4.1 3B model, cancellation, repeatability, every committed malformed GGUF fixture, file-access failures, privacy, network observations, disposal, and original-file integrity.

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
- Missing model configuration must fail the workflow; no silent skips.
- A child-process native abort must be recorded, not allowed to terminate the MSTest host.
- Use bounded timeouts and kill the complete child process tree on timeout.
- Upload evidence only after a pre-upload scan proves no `.gguf` file is present.
- Update the nearest READMEs and coverage matrix with actual evidence rather than source-presence claims.

---

### Task 1: Create the real-model integration project and controlled-model configuration

**Files:**
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.csproj`
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/README.md`
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/ControlledModels/granite-4.1-3b-q4-k-m.json`
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/Support/RealModelEvidenceDirectory.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/Support/RealModelTestContext.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.TestSupport/ControlledModelManifest.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.TestSupport/ControlledModelConfiguration.cs`

**Interfaces:**
- Consumes: `LLAMASHARP_SPIKE_PUBLISH_DIR`, `GRANITE_TEST_MODEL_PATH`, and the committed model manifest.
- Produces: a fail-fast `RealModelTestContext` containing the published executable, model path, expected identity, and an isolated evidence root.

- [ ] **Step 1: Create the MTP-enabled test project**

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

- [ ] **Step 2: Commit the exact model manifest**

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
                   new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? throw new InvalidDataException(
                   "Controlled model manifest could not be deserialized.");
    }
}
```

- [ ] **Step 4: Implement fail-fast environment configuration**

```csharp
public sealed record ControlledModelConfiguration
{
    public const string ModelPathEnvironmentVariable =
        "GRANITE_TEST_MODEL_PATH";

    public required string ModelPath { get; init; }
    public required ControlledModelManifest Manifest { get; init; }

    public static ControlledModelConfiguration Load(string manifestPath)
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

- [ ] **Step 5: Add a local evidence-directory helper**

```csharp
internal sealed class RealModelEvidenceDirectory : IDisposable
{
    internal RealModelEvidenceDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "GraniteEdgeAI-LlamaSharpTests",
            "real-model-evidence",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    internal string Path { get; }

    internal string CreateFile(string scenario, string fileName)
    {
        string directory = System.IO.Path.Combine(
            Path,
            scenario,
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return System.IO.Path.Combine(directory, fileName);
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

- [ ] **Step 6: Add `RealModelTestContext`**

```csharp
internal sealed class RealModelTestContext : IDisposable
{
    private readonly RealModelEvidenceDirectory _evidence = new();

    internal RealModelTestContext()
    {
        string manifestPath = System.IO.Path.Combine(
            AppContext.BaseDirectory,
            "ControlledModels",
            "granite-4.1-3b-q4-k-m.json");

        Model = ControlledModelConfiguration.Load(manifestPath);
        PublishedProbeDirectory =
            PublishedProbeLocation.RequireFromEnvironment();
    }

    internal ControlledModelConfiguration Model { get; }
    internal string PublishedProbeDirectory { get; }
    internal string EvidenceRoot => _evidence.Path;

    internal string CreateEvidencePath(string scenario) =>
        _evidence.CreateFile(scenario, "result.json");

    public void Dispose() => _evidence.Dispose();
}
```

- [ ] **Step 7: Add deterministic configuration tests**

Test missing environment variable, missing file, wrong filename, wrong length, and wrong SHA-256 using temporary small files. These tests must fail before any native call.

- [ ] **Step 8: Commit the scaffold**

```powershell
git add tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests `
        tools/ModelInspection.LlamaSharpSpike.TestSupport
git commit -m "test(model-inspection): scaffold trusted real-model tests"
```

---

### Task 2: Add controlled Granite success and repeatability contracts

**Files:**
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/RealModel/GraniteVocabOnlySuccessTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/RealModel/GraniteVocabOnlyRepeatabilityTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.TestSupport/ModelFileHash.cs`
- Expand: `tools/ModelInspection.LlamaSharpSpike.TestSupport/EvidenceAssertions.cs`

**Interfaces:**
- Produces: exact success assertions and three-run repeatability evidence.

- [ ] **Step 1: Add independent SHA-256 calculation**

```csharp
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

- [ ] **Step 2: Implement exact evidence assertions**

`EvidenceAssertions.AssertSuccessfulGraniteVocabOnly` must require:

```text
schemaVersion = 1.1
completionStatus = Succeeded
succeeded = true
vocabOnlyRequested = true
gpuLayerCount = 0
managedPackageVersion = 0.27.0
backendPackageVersion = 0.27.0
expectedLlamaCppCommit = 3f7c29d318e317b63f54c558bc69803963d7d88c
selectedBackend.usesCuda = false
selectedBackend.usesVulkan = false
all manifest metadata values match
parameterCount is null
progress fractions are finite and within 0..1
nativeHandleClosedAfterDispose = true
integrity.isPreserved = true
before/after SHA-256 match manifest
failure fields are null
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

- [ ] **Step 4: Add three-run repeatability**

Run three child processes sequentially with unique JSON paths. Require all three to succeed. Compare stable fields only:

```text
architecture, name, context, embedding, layers, heads, KV heads,
metadata count, vocabulary count, chat-template hash, model hashes
```

Do not compare exact load duration, working set, peak memory, or progress sample count; require only valid nonnegative observations.

- [ ] **Step 5: Assert cleanup**

No `*.tmp-*` file may remain in the evidence root or sandbox. Every result must report a closed native handle.

- [ ] **Step 6: Run locally and commit**

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
- Produces: evidence-backed cancellation at both scopes.

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

Launch with:

```text
--model <path> --cancel-after-ms 1 --output <json>
```

Require child exit `3`, JSON, `Cancelled`, `MI-PROBE-CANCELLED`, unchanged model hash, and no path leak. Selected backend may be absent because cancellation can happen during the pre-hash stage.

- [ ] **Step 3: Add native-load cancellation test**

Launch with:

```text
--model <path> --cancel-native-after-ms 1 --output <json>
```

Require child exit `3`, JSON, selected CPU backend, CUDA/Vulkan false, `Cancelled`, `MI-PROBE-CANCELLED`, unchanged model hash, and any non-null native-handle disposal flag equal to true.

- [ ] **Step 4: Repeat native-load cancellation three times**

All three unique runs must return exit `3`; none may become `MI-PROBE-MODEL-LOAD-FAILED`.

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

### Task 4: Run all committed malformed GGUF fixtures in child processes

**Files:**
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/MalformedModels/MalformedFixtureCase.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/MalformedModels/MalformedFixtureData.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/MalformedModels/MalformedModelProcessTests.cs`

**Interfaces:**
- Consumes: `tests/TestFixtures/fixture-manifest.json` property `generatedGgufFixtures`, using exact fields `fixtureId`, `fixtureFile`, `byteLength`, and `sha256`.
- Produces: one MSTest case per entry whose `fixtureFile` begins with `Malformed/`.

- [ ] **Step 1: Implement exact manifest discovery**

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
                     .GetProperty("generatedGgufFixtures")
                     .EnumerateArray())
        {
            string relativePath = fixture
                .GetProperty("fixtureFile")
                .GetString()!;

            if (!relativePath.StartsWith(
                    "Malformed/",
                    StringComparison.Ordinal))
            {
                continue;
            }

            yield return new object[]
            {
                new MalformedFixtureCase(
                    fixture.GetProperty("fixtureId").GetString()!,
                    Path.Combine(
                        root,
                        "tests",
                        "TestFixtures",
                        relativePath.Replace('/', Path.DirectorySeparatorChar)),
                    fixture.GetProperty("byteLength").GetInt64(),
                    fixture.GetProperty("sha256").GetString()!)
            };
        }
    }
}
```

- [ ] **Step 2: Add one contained process test per fixture**

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
        Assert.AreEqual(
            "Failed",
            evidence.RootElement.GetProperty("completionStatus").GetString());
        Assert.IsTrue(
            evidence.RootElement
                .GetProperty("integrity")
                .GetProperty("isPreserved")
                .GetBoolean());
        Assert.IsFalse(evidence.RootElement.TryGetProperty("modelOutcome", out _));
    }

    EvidenceAssertions.AssertDoesNotContainCanonicalPath(
        process.StandardOutput,
        fixture.Path);
    EvidenceAssertions.AssertDoesNotContainCanonicalPath(
        process.StandardError,
        fixture.Path);
}
```

A native abort without JSON is acceptable only as a contained nonzero child-process result with unchanged fixture hash.

- [ ] **Step 3: Add a deterministic random-byte `.gguf` case**

```csharp
byte[] bytes = Enumerable.Range(0, 4096)
    .Select(index => (byte)(index % 251))
    .ToArray();
```

Run through the same containment, privacy, and integrity contract.

- [ ] **Step 4: Run and commit**

```powershell
dotnet test `
  "tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.csproj" `
  --configuration Release `
  --runtime win-x64 `
  --filter "FullyQualifiedName~MalformedModels"

git add tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests
git commit -m "test(model-inspection): contain malformed GGUF runtime failures"
```

Expected: all manifest entries under `Malformed/` are discovered, plus one random-byte case.

---

### Task 5: Add file-access and evidence-write failure tests

**Files:**
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/FileAccess/MissingModelProcessTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/FileAccess/DirectoryModelProcessTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/FileAccess/LockedModelProcessTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/FileAccess/LockedOutputProcessTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/FileAccess/InvalidOutputParentProcessTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/FileAccess/OutputEqualsModelProcessTests.cs`

**Interfaces:**
- Produces: controlled child-process evidence for model-access and evidence-write failures.

- [ ] **Step 1: Add missing-model and directory-model cases**

Missing model requires exit `1`, JSON, `MI-OP-MODEL-FILE-NOT-FOUND`, and no path leak. Directory supplied as model requires a controlled nonzero file-related result; record the actual stable code in the coverage matrix.

- [ ] **Step 2: Add locked-model case with a temporary fixture**

Hold the file with:

```csharp
using FileStream lockStream = new(
    fixturePath,
    FileMode.Open,
    FileAccess.ReadWrite,
    FileShare.None);
```

Require parent survival, nonzero exit, unchanged fixture hash, and `MI-OP-MODEL-FILE-IO` or `MI-OP-MODEL-FILE-ACCESS-DENIED` according to the observed Windows exception.

- [ ] **Step 3: Add locked-output case**

Hold an existing valid JSON file with `FileShare.None`, run native smoke to that output, and require exit `1`, stderr code `MI-OP-EVIDENCE-WRITE-FAILED`, unchanged existing JSON, and no `*.tmp-*` file.

- [ ] **Step 4: Add invalid-output-parent case**

Create a regular file named `parent-file` and use `parent-file\result.json` as output. Require exit `1`, `MI-OP-EVIDENCE-WRITE-FAILED`, and no model/fixture change.

- [ ] **Step 5: Add output-equals-model case**

Use identical canonical paths for `--model` and `--output`. Require exit `2` before native work and unchanged model hash.

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

### Task 6: Add privacy, artifact-exclusion, and network-observation tests

**Files:**
- Create: `tools/ModelInspection.LlamaSharpSpike.TestSupport/SocketObservation.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/Security/EvidencePrivacyTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/Security/NetworkObservationTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/Security/ArtifactExclusionTests.cs`

**Interfaces:**
- Produces: strong “no TCP use observed” and “model excluded from evidence” results.

- [ ] **Step 1: Implement process-owned TCP observation**

Invoke Windows PowerShell with:

```powershell
Get-NetTCPConnection -OwningProcess <pid> -ErrorAction SilentlyContinue |
  Select-Object State, LocalAddress, LocalPort, RemoteAddress, RemotePort |
  ConvertTo-Json -Compress
```

Return:

```csharp
public sealed record ObservedTcpConnection(
    string State,
    string LocalAddress,
    int LocalPort,
    string RemoteAddress,
    int RemotePort);
```

Poll every 25 ms until the child exits or two seconds elapse. The delay belongs to the bounded observation loop and must use cancellation.

- [ ] **Step 2: Add evidence privacy test**

After a successful model run, require JSON/stdout/stderr to exclude:

```text
full canonical model path
parent directory path
modelPath property
chatTemplateText property
full tokenizer.chat_template value
```

Require filename and `canonicalPathSha256` because those are approved evidence.

- [ ] **Step 3: Add network-observation test**

Launch a real-model child and observe its PID. Require no `Listen`, `Established`, `SynSent`, or `SynReceived` connection owned by the process. State the claim narrowly: no TCP listener or active connection was observed.

- [ ] **Step 4: Add artifact exclusion test**

Recursively scan the evidence root. Fail on:

```text
any .gguf file
any file of length 2099501664
any file whose SHA-256 equals the controlled model hash
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
- Consumes repository variable `GRANITE_TEST_MODEL_PATH`.
- Produces test and diagnostic evidence without uploading the model.

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

- [ ] **Step 2: Add fail-fast model preflight**

Verify configured path, file readability, exact SHA-256, and exact byte length. Fail before restore/test if any value differs.

- [ ] **Step 3: Restore and publish**

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
      --minimum-expected-tests 1
```

After implementation, replace `1` with the exact discovered test-case count from a fresh run; do not guess the final count.

- [ ] **Step 5: Scan evidence before upload**

Fail if the evidence root contains a `.gguf`, a file of length `2099501664`, or a file whose SHA-256 equals the controlled model hash.

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

Repository variable:

```text
GRANITE_TEST_MODEL_PATH
```

Recommended service-readable location:

```text
C:\ProgramData\GraniteEdgeAI\TestModels\granite-4.1-3b-Q4_K_M.gguf
```

The model must be outside the checkout and readable by the runner service account.

- [ ] **Step 8: Commit**

```powershell
git add .github/workflows/llamasharp-real-model-integration.yml `
        tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/README.md
git commit -m "ci(model-inspection): add trusted LLamaSharp real-model suite"
```

---

### Task 8: Reconcile coverage and documentation after fresh evidence

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

- [ ] **Step 1: Mark every implemented scenario in the matrix**

Required rows:

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
no TCP listener/connection observed
before/after integrity for every model/fixture scenario
```

- [ ] **Step 2: Record explicit deferrals**

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

Distinguish verified feasibility from unimplemented production `ILlamaModelProbe`, WinUI cancellation, full CPU execution, Vulkan, and TurboQuant.

- [ ] **Step 4: Run the trusted workflow and record evidence identifiers**

Record workflow run ID, runner identity, application commit, discovered/passed/failed/skipped counts, model hash, artifact name, and contained native-abort exit codes.

- [ ] **Step 5: Download and inspect the artifact**

Confirm no `.gguf`, model-length file, model-hash file, or canonical local model path appears.

- [ ] **Step 6: Commit evidence-backed documentation**

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
- [ ] Whole-operation cancellation returns `Cancelled`, `MI-PROBE-CANCELLED`, exit `3`, and preserved integrity.
- [ ] Native-load cancellation returns the same controlled result without becoming a load failure.
- [ ] Every committed malformed fixture is contained in a child process and remains unchanged.
- [ ] Missing, directory, locked-model, locked-output, invalid-output-parent, and output-equals-model cases are controlled.
- [ ] No canonical model path or full chat template appears in JSON, stdout, stderr, or artifacts.
- [ ] No TCP listener or active connection is observed for the probe process.
- [ ] No `.gguf`, model-sized file, or model-hash file appears in uploaded evidence.
- [ ] Coverage matrix links every automated scenario to its test and evidence tier.
- [ ] Remaining non-automated conditions have explicit reasons and future routes.
- [ ] READMEs state exactly what is verified and what remains outside this slice.
