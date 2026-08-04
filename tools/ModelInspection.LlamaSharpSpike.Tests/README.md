# LLamaSharp feasibility tests

**Status:** Deterministic test source implemented; fresh execution pending  
**Test runner:** Microsoft.Testing.Platform through the embedded MSTest runner  
**Tool:** [Model Inspection LLamaSharp feasibility tool](../ModelInspection.LlamaSharpSpike/README.md)

## Purpose

This MSTest project verifies deterministic behavior around the isolated
LLamaSharp feasibility tool without treating a real native model load as a unit
test.

The project references the console project directly. It does not reference the
WinUI application.

## Test-runner configuration

The repository-level `global.json` selects:

```json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

The test project therefore enables the embedded MSTest runner explicitly:

```xml
<OutputType>Exe</OutputType>
<EnableMSTestRunner>true</EnableMSTestRunner>
<TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
```

Without that configuration, `.NET 10` rejects the project as VSTest-only before
any tests run. The runner configuration is infrastructure; it does not change
the test assertions or the LLamaSharp runtime under test.

## Assertion-quality correction

The first MTP build exposed `MSTEST0032` in
`PinnedApplicationRuntimeTests.cs`. The original assertions compared
compile-time constants directly with the same literal values, so the compiler
could prove that they would always pass.

The rule was not suppressed. The tests were rewritten to inspect actual
repository project files at runtime:

```text
feasibility spike .csproj
    → must reference LLamaSharp 0.27.0
    → must reference LLamaSharp.Backend.Cpu 0.27.0

WinUI application .csproj
    → must contain zero LLamaSharp package references
```

This now verifies a real architectural boundary instead of testing that a
constant equals itself.

## Current test areas

### Runtime dependency policy

`PinnedApplicationRuntimeTests.cs` verifies:

- the feasibility project contains the approved exact `LLamaSharp` version;
- the feasibility project contains the approved exact CPU-backend version;
- the WinUI application project still contains no `LLamaSharp*` dependency;
- the experimental native dependency remains isolated under `tools/` until the
  feasibility gates pass.

The mapped llama.cpp commit and separate `b9870` research identity remain
recorded by `PinnedApplicationRuntime`, ADR-001 and runtime evidence. Direct
constant-to-literal assertions are deliberately avoided.

### Command parsing

`SpikeOptionsParserTests.cs` verifies:

- native-smoke defaults;
- `--model` VocabOnly defaults;
- model/output/cancellation options in arbitrary order;
- help;
- missing values;
- duplicate options;
- invalid cancellation delays;
- rejection of cancellation without a model;
- unknown arguments.

### Evidence writing

`SmokeEvidenceWriterTests.cs` now exercises the generic
`JsonEvidenceWriter` and verifies:

- parseable camel-case JSON;
- readable string enum values;
- atomic replacement of earlier local evidence.

The historical filename remains for continuity even though the writer is now
shared by both feasibility modes.

### Model-file safety

`ModelProbeSafetyValidatorTests.cs` verifies that an evidence path cannot
resolve to the selected model path.

`ModelFileSnapshotServiceTests.cs` verifies:

- deterministic SHA-256 capture;
- filename, length and path-fingerprint evidence;
- unchanged before/after comparison;
- detection of changed file content.

### Native progress

`NativeLoadProgressRecorderTests.cs` verifies:

- clamping to `0..1`;
- suppression of consecutive duplicate fractions;
- independent progress snapshots.

### Pre-native failure handling

`VocabOnlyModelProbeFailureTests.cs` verifies that a missing model returns the
controlled `MI-OP-MODEL-FILE-NOT-FOUND` result without calling the native
runtime and without serialising the full canonical model path.

## What is not a unit test

These require integration evidence on Windows x64:

- native CPU library discovery;
- actual `LLamaWeights.LoadFromFileAsync`;
- Granite architecture recognition;
- real metadata/vocabulary/chat-template availability;
- native progress callback behavior;
- cancellation during model loading;
- native disposal after a successful load;
- before/after integrity of a real GGUF;
- process-memory observations.

Those checks use the console command and a controlled, provenance-recorded
model. They must not be replaced by mocks and then described as runtime proof.

## Commands

```powershell
dotnet restore `
    "tools\ModelInspection.LlamaSharpSpike.Tests\ModelInspection.LlamaSharpSpike.Tests.csproj" `
    --runtime win-x64

dotnet test `
    "tools\ModelInspection.LlamaSharpSpike.Tests\ModelInspection.LlamaSharpSpike.Tests.csproj" `
    --configuration Release `
    --no-restore `
    --runtime win-x64 `
    --minimum-expected-tests 1
```

No passing result is claimed until the command output is captured and reviewed.
