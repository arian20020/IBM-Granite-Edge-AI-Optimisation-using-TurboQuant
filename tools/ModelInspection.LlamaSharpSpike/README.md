# Model Inspection LLamaSharp feasibility tool

**Status:** Native smoke and VocabOnly probe source implemented; fresh Windows verification pending  
**Runtime decision:** [ADR-001](../../docs/architecture/decisions/ADR-001-llamasharp-application-runtime.md)  
**Inspection-depth decision:** [ADR-002](../../docs/architecture/decisions/ADR-002-core-inspection-versus-backend-verification.md)  
**Initial design:** [LLamaSharp feasibility spike](../../docs/superpowers/specs/2026-08-04-llamasharp-feasibility-spike-design.md)  
**Model-probe design:** [LLamaSharp VocabOnly model probe](../../docs/superpowers/specs/2026-08-04-llamasharp-vocab-only-model-probe-design.md)

## Purpose

This isolated console project proves the selected managed/native application
runtime before LLamaSharp is added to the WinUI application.

```text
LLamaSharp 0.27.0
        ↓
LLamaSharp.Backend.Cpu 0.27.0
        ↓
llama.cpp 3f7c29d318e317b63f54c558bc69803963d7d88c
```

The tool now has two gates:

```text
Mode 1 — native CPU backend smoke
    → no model
    → proves native-library discovery and runtime identity

Mode 2 — CPU VocabOnly model probe
    → one controlled local GGUF
    → probes lightweight metadata, vocabulary, tokenizer and chat-template evidence
    → verifies disposal and original-file integrity
```

Neither mode is referenced by the WinUI application.

## Why this is separate from the WinUI application

Native runtime selection and model loading are high-risk integration
boundaries. Keeping the experiment in a console project means:

- the WinUI package remains unchanged;
- native failures are easier to reproduce;
- logs and exit codes are not hidden by page lifecycle behavior;
- model preservation can be checked independently;
- the project can be built and run explicitly;
- feasibility-only types cannot become production UI contracts accidentally.

## Folder structure

```text
ModelInspection.LlamaSharpSpike/
├── README.md
├── ModelInspection.LlamaSharpSpike.csproj
├── Program.cs
├── PinnedApplicationRuntime.cs
├── SpikeOptionsParser.cs
├── CpuNativeRuntimeConfiguration.cs
├── NativeBackendSmokeProbe.cs
├── NativeBackendSmokeResult.cs
├── JsonEvidenceWriter.cs
└── ModelProbe/
    ├── README.md
    ├── ModelFileSnapshot.cs
    ├── ModelFileSnapshotService.cs
    ├── ModelProbeSafetyValidator.cs
    ├── NativeLoadProgressRecorder.cs
    ├── VocabOnlyModelProbeResult.cs
    ├── VocabOnlyEvidenceCollector.cs
    └── VocabOnlyModelProbe.cs
```

Model-specific details are documented in the
[ModelProbe README](./ModelProbe/README.md).

## Current flow

### Native smoke

```text
Program
    → parses --output or --help
    → NativeBackendSmokeProbe
    → CpuNativeRuntimeConfiguration
    → NativeLibraryConfig.LLama.DryRun(...)
    → NativeBackendSmokeResult
    → JsonEvidenceWriter
```

### VocabOnly model probe

```text
Program
    → parses --model, --output and optional cancellation
    → rejects output/model path collision
    → VocabOnlyModelProbe
    → read-only file snapshot before
    → matched CPU backend dry run
    → LLamaWeights.LoadFromFileAsync
         VocabOnly = true
         GpuLayerCount = 0
    → collect runtime evidence
    → dispose native weights
    → read-only file snapshot after
    → compare integrity
    → VocabOnlyModelProbeResult
    → JsonEvidenceWriter
```

## Exact dependency identity

| Item | Value |
|---|---|
| Managed package | `LLamaSharp` `0.27.0` |
| CPU backend package | `LLamaSharp.Backend.Cpu` `0.27.0` |
| LLamaSharp tag | `v0.27.0` |
| LLamaSharp release commit | `7cbbc45e421d55794d5050d126e0b96511007007` |
| Mapped llama.cpp commit | `3f7c29d318e317b63f54c558bc69803963d7d88c` |
| Intended first application RID | `win-x64` |

The separate upstream research runtime remains:

```text
b9870 / 2d973636e292ee6f75fadcf08d29cb33511f509f
```

It is not the runtime loaded by this tool.

## Build and deterministic tests

Run from the repository root in Developer PowerShell:

```powershell
dotnet restore `
    "tools\ModelInspection.LlamaSharpSpike.Tests\ModelInspection.LlamaSharpSpike.Tests.csproj" `
    --runtime win-x64

dotnet test `
    "tools\ModelInspection.LlamaSharpSpike.Tests\ModelInspection.LlamaSharpSpike.Tests.csproj" `
    --configuration Release `
    --no-restore `
    --runtime win-x64

dotnet build `
    "tools\ModelInspection.LlamaSharpSpike\ModelInspection.LlamaSharpSpike.csproj" `
    --configuration Release `
    --no-restore `
    --runtime win-x64
```

## Run the native backend smoke

```powershell
dotnet run `
    --project "tools\ModelInspection.LlamaSharpSpike\ModelInspection.LlamaSharpSpike.csproj" `
    --configuration Release `
    --no-build `
    --runtime win-x64 `
    -- `
    --output "artifacts\model-inspection\llamasharp\runtime-smoke.json"
```

Default output:

```text
artifacts/model-inspection/llamasharp/runtime-smoke.json
```

## Run the VocabOnly model probe

```powershell
dotnet run `
    --project "tools\ModelInspection.LlamaSharpSpike\ModelInspection.LlamaSharpSpike.csproj" `
    --configuration Release `
    --no-build `
    --runtime win-x64 `
    -- `
    --model "C:\Models\granite-model.gguf" `
    --output "artifacts\model-inspection\llamasharp\vocab-only-model-probe.json"
```

Default output when `--model` is supplied:

```text
artifacts/model-inspection/llamasharp/vocab-only-model-probe.json
```

Controlled cancellation:

```powershell
--cancel-after-ms 250
```

`Ctrl+C` also requests cancellation.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Requested smoke/probe succeeded and JSON was written |
| `1` | Controlled runtime, probe, integrity, or evidence-writing failure |
| `2` | Invalid or unsafe command-line arguments |
| `3` | VocabOnly model probe was cancelled and JSON was written |

## Evidence

Local exploratory output remains under ignored `artifacts/`. Do not move it
into formal evidence until the command, environment, package identity, model
provenance and output have been reviewed.

### Native smoke evidence

Records:

- exact managed/backend package pins;
- mapped llama.cpp commit;
- process architecture, OS and .NET runtime;
- selected native library and AVX metadata;
- CUDA/Vulkan flags;
- native-loader logs;
- controlled operational failure details.

### VocabOnly model evidence

Records:

- the same runtime identity;
- model filename and path fingerprint;
- before/after length, timestamp and SHA-256;
- file-preservation comparison;
- actual native progress samples;
- load duration and process-memory observations;
- runtime description and selected metadata;
- metadata key list;
- context, parameter, layer/head and architecture characteristics;
- vocabulary and special tokens;
- a fixed `Hello` tokenizer smoke;
- chat-template presence, length and SHA-256;
- native-handle disposal status;
- controlled completion/failure information.

The full machine-local path, model bytes, full chat template and native handles
are not serialized.

## Safety boundary

- CPU is the only selected backend.
- CUDA selection is disabled.
- Vulkan selection is disabled.
- Runtime auto-download is not used.
- The model is opened read-only.
- The evidence output may not equal the model path.
- `VocabOnly = true`.
- `GpuLayerCount = 0`.
- No context or KV cache is created.
- No inference is run.
- No save, conversion or quantisation API is called.
- Cancellation is distinct from failure.
- Load failure is not automatically classified as invalid or unsupported.
- The WinUI application project has no LLamaSharp reference from this work.

## Current verification status

Implemented in source:

- command parsing;
- native smoke;
- VocabOnly model-probe orchestration;
- read-only identity capture;
- genuine progress collection;
- evidence extraction;
- deterministic disposal;
- integrity comparison;
- JSON writing;
- deterministic unit-test contracts.

Still required before advancing:

- fresh Windows x64 restore;
- deterministic tests;
- Release build;
- native CPU smoke result;
- one controlled real Granite GGUF run;
- review of which fields are actually available in `VocabOnly` mode;
- review of memory behavior, progress, cancellation and disposal evidence.

No successful model-probe claim is made by source presence alone.

## Next gate

After the controlled Granite result is reviewed:

```text
VocabOnly evidence sufficient
    → design the production ILlamaModelProbe contracts

VocabOnly evidence insufficient
    → investigate a lower-level no-allocation route behind the same boundary
```

Ordinary Vulkan and TurboQuant remain later backend-verification gates. They are
not part of this tool stage.
