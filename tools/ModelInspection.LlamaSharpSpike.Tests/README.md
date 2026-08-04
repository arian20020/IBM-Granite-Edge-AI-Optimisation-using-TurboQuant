# LLamaSharp feasibility tests

**Status:** Deterministic test source implemented; fresh execution pending  
**Tool:** [Model Inspection LLamaSharp feasibility tool](../ModelInspection.LlamaSharpSpike/README.md)

## Purpose

This MSTest project verifies deterministic behavior around the isolated
LLamaSharp feasibility tool without treating a real native model load as a unit
test.

The project references the console project directly. It does not reference the
WinUI application.

## Current test areas

### Runtime identity

`PinnedApplicationRuntimeTests.cs` protects:

- `LLamaSharp` `0.27.0`;
- `LLamaSharp.Backend.Cpu` `0.27.0`;
- mapped llama.cpp commit
  `3f7c29d318e317b63f54c558bc69803963d7d88c`;
- intended `win-x64` application runtime;
- explicit separation from the standalone `b9870` research runtime.

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
    --runtime win-x64
```

No passing result is claimed until the command output is captured and reviewed.
