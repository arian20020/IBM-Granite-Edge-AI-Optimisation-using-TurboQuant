# LLamaSharp deterministic feasibility tests

**Status:** Responsibility-based hierarchy implemented; expanded coverage is being added  
**Test runner:** Microsoft.Testing.Platform through the embedded MSTest runner  
**Tool:** [Model Inspection LLamaSharp feasibility tool](../ModelInspection.LlamaSharpSpike/README.md)

## Purpose

This MSTest project verifies deterministic behaviour around the isolated
LLamaSharp feasibility tool without treating a real native model load as a unit
test.

The project references the feasibility console project directly. It does not
reference the WinUI application and no test in this project requires the 2 GB
Granite model.

## Test-runner configuration

The repository-level `global.json` selects Microsoft.Testing.Platform. The test
project therefore remains an executable MSTest project with:

```xml
<OutputType>Exe</OutputType>
<EnableMSTestRunner>true</EnableMSTestRunner>
<TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
<TestingPlatformShowTestsFailure>true</TestingPlatformShowTestsFailure>
```

## Folder hierarchy

```text
ModelInspection.LlamaSharpSpike.Tests/
├── README.md
├── ModelInspection.LlamaSharpSpike.Tests.csproj
├── DependencyPolicy/
│   └── RuntimeDependencyPolicyTests.cs
├── CommandLine/
│   └── SpikeOptionsParserTests.cs
├── FileSafety/
│   ├── ModelProbeSafetyValidatorTests.cs
│   └── ModelFileSnapshotServiceTests.cs
├── Metadata/
│   └── VocabOnlyMetadataProjectionTests.cs
├── Progress/
│   └── NativeLoadProgressRecorderTests.cs
├── Evidence/
│   └── JsonEvidenceWriterTests.cs
├── Failures/
│   └── VocabOnlyModelProbeFailureTests.cs
└── Support/
    ├── TemporaryDirectory.cs
    └── TestFileBuilder.cs
```

Every deterministic test class is marked:

```csharp
[TestCategory("Deterministic")]
```

This category allows normal CI to run pure contract tests separately from
child-process native integration tests.

## Responsibility boundaries

### Dependency policy

Reads actual repository project files and protects exact LLamaSharp package
pins and the boundary that keeps LLamaSharp out of the WinUI project.

### Command line

Verifies native-smoke and model-probe parsing, option validation and controlled
argument errors.

### File safety

Verifies evidence/model path separation, read-only identity snapshots, SHA-256
and integrity comparison.

### Metadata

Verifies architecture-scoped metadata projection without unsafe native
hyperparameter accessors.

### Progress

Verifies genuine callback fractions, clamping, duplicate suppression and
snapshot independence.

### Evidence

Verifies parseable camel-case JSON, string enums and atomic replacement.

### Failures

Verifies deterministic failures that occur before LLamaSharp enters native
code, including missing-file classification and path redaction.

### Support

Owns small reusable test helpers only. These helpers do not call LLamaSharp and
do not contain assertions.

## What is not a deterministic unit test

The following require child-process integration tests:

- native CPU library discovery;
- missing or corrupted native DLL behaviour;
- actual `LLamaWeights.LoadFromFileAsync`;
- Granite architecture recognition;
- real metadata, vocabulary and chat-template availability;
- cancellation while native loading is active;
- disposal after a real load;
- malformed GGUF containment;
- before/after integrity of a real GGUF;
- process-owned socket observations.

A native call must never execute inside this MSTest host because llama.cpp can
terminate the process before managed exception handling runs.

## Command

```powershell
dotnet test `
    "tools\ModelInspection.LlamaSharpSpike.Tests\ModelInspection.LlamaSharpSpike.Tests.csproj" `
    --configuration Release `
    --runtime win-x64 `
    --filter "TestCategory=Deterministic" `
    --minimum-expected-tests 28
```

The previous verified baseline was 28/28. Test source added after that baseline
must receive fresh execution evidence before the documented count is raised.
