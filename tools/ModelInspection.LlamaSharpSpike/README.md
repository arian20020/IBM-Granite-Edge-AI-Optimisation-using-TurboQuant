# Model Inspection LLamaSharp feasibility tool

**Status:** CPU native smoke passed; first VocabOnly run diagnosed; corrected rerun pending  
**Last reviewed:** 2026-08-04  
**Runtime decision:** [ADR-001](../../docs/architecture/decisions/ADR-001-llamasharp-application-runtime.md)  
**Inspection-depth decision:** [ADR-002](../../docs/architecture/decisions/ADR-002-core-inspection-versus-backend-verification.md)  
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

The tool has two gates:

```text
Mode 1 — native CPU backend smoke
    → no model
    → proves native-library discovery and runtime identity

Mode 2 — CPU VocabOnly model probe
    → one controlled local GGUF
    → probes metadata, vocabulary, tokenizer and chat-template evidence
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
- feasibility-only types cannot become production UI contracts accidentally;
- a future worker-process implementation can still replace the in-process probe
  behind `ILlamaModelProbe` without changing the page or classifier.

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
    ├── VocabOnlyMetadataProjection.cs
    ├── VocabOnlyModelProbeResult.cs
    ├── VocabOnlyEvidenceCollector.cs
    └── VocabOnlyModelProbe.cs
```

Model-specific details and the first target-laptop run are documented in the
[ModelProbe README](./ModelProbe/README.md).

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

## Current flow

### Native smoke

```text
Program
    → NativeBackendSmokeProbe
    → CpuNativeRuntimeConfiguration
    → NativeLibraryConfig.LLama.DryRun(...)
    → NativeBackendSmokeResult
    → JsonEvidenceWriter
```

### VocabOnly model probe

```text
Program
    → parse model/output/cancellation options
    → reject output/model collision
    → capture read-only model snapshot before
    → dry-run matched CPU backend
    → LLamaWeights.LoadFromFileAsync
         VocabOnly = true
         GpuLayerCount = 0
    → collect metadata/vocabulary evidence safe for VocabOnly
    → dispose native weights
    → capture model snapshot after
    → compare integrity
    → write project-owned JSON
```

## Target-laptop verification status

### Native CPU smoke

```text
Result:                     PASS
Managed package:            LLamaSharp 0.27.0
Backend package:            LLamaSharp.Backend.Cpu 0.27.0
Mapped llama.cpp commit:    3f7c29d318e317b63f54c558bc69803963d7d88c
Process architecture:       X64
Selected library:           LLama
AVX level:                  AVX-512
CUDA selected:              false
Vulkan selected:            false
```

### First deterministic test run

```text
Total:      25
Passed:     24
Failed:      1
```

The one failure was a brittle assertion against incidental message wording. The
production parser already communicated both required options. The test now
checks `--cancel-after-ms` and `--model` semantically and awaits rerun.

### First real Granite VocabOnly run

```text
Input:
granite-4.1-3b-Q4_K_M.gguf

Size:
2,099,501,664 bytes

SHA-256 before and after:
662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29

Native message:
llama-hparams.cpp:35: fatal error

Process exit code:
-1073740791

JSON evidence:
not written because native code terminated the process
```

The model file was preserved. The abort was traced to the first collector
calling native hyperparameter getters after llama.cpp had deliberately skipped
hyperparameter loading for `vocab_only`.

The branch now:

- projects structure from GGUF metadata;
- removes known unsafe hyperparameter getters from the VocabOnly collector;
- makes unavailable evidence nullable;
- records schema version `1.1`;
- includes regression tests for metadata projection.

A fresh test/build/model rerun is required before declaring the correction
successful.

## Build and deterministic tests

```powershell
$SpikeProject =
    "tools\ModelInspection.LlamaSharpSpike\ModelInspection.LlamaSharpSpike.csproj"

$SpikeTests =
    "tools\ModelInspection.LlamaSharpSpike.Tests\ModelInspection.LlamaSharpSpike.Tests.csproj"

dotnet restore $SpikeTests --runtime win-x64

dotnet test $SpikeTests `
    --configuration Release `
    --no-restore `
    --runtime win-x64 `
    --minimum-expected-tests 1

dotnet build $SpikeProject `
    --configuration Release `
    --no-restore `
    --runtime win-x64
```

## Run the native backend smoke

```powershell
dotnet run `
    --project $SpikeProject `
    --configuration Release `
    --no-build `
    --runtime win-x64 `
    -- `
    --output "artifacts\model-inspection\llamasharp\runtime-smoke.json"
```

## Run the corrected VocabOnly model probe

```powershell
$ModelPath = Join-Path `
    $env:USERPROFILE `
    "Downloads\granite-4.1-3b-Q4_K_M.gguf"

$RunId = Get-Date -Format "yyyyMMdd-HHmmss"
$ProbeOutput =
    "artifacts\model-inspection\llamasharp\runs\$RunId\vocab-only-model-probe.json"

dotnet run `
    --project $SpikeProject `
    --configuration Release `
    --no-build `
    --runtime win-x64 `
    -- `
    --model $ModelPath `
    --output $ProbeOutput
```

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Requested smoke/probe succeeded and JSON was written |
| `1` | Controlled runtime, probe, integrity, or evidence-writing failure |
| `2` | Invalid or unsafe command-line arguments |
| `3` | VocabOnly model probe was cancelled and JSON was written |

A process-level native abort may return a Windows status code outside this
managed exit-code contract because native code terminated before control
returned to `Program`.

## Evidence boundary

The corrected VocabOnly report may contain metadata-derived values for:

```text
architecture
model name
file type
quantisation version
tokenizer model
context length
embedding length
block count
attention head counts
parameter count, when present
```

The following remain nullable when VocabOnly does not expose them safely:

```text
runtime-reported model size
encoder/decoder flags
recurrent/diffusion flags
any structural field missing from GGUF metadata
```

Null means unavailable at this inspection depth; it does not mean zero or
invalid.

## Safety boundary

- CPU is the only selected backend.
- CUDA and Vulkan selection are disabled.
- The model is opened read-only.
- The evidence output may not equal the model path.
- `VocabOnly = true`.
- `GpuLayerCount = 0`.
- No context or KV cache is created.
- No inference, save, conversion or quantisation API is called.
- Cancellation is distinct from failure.
- Native/runtime failure is not automatically classified as model failure.
- The WinUI application project has no LLamaSharp reference from this work.

## Next decision

```text
Corrected VocabOnly run succeeds and evidence is sufficient
    → design production ILlamaModelProbe contracts

Corrected run succeeds but evidence is insufficient
    → investigate lower-level no-allocation evidence

Corrected run still terminates inside native loading
    → review worker-process isolation behind ILlamaModelProbe
```

Ordinary Vulkan and TurboQuant remain later backend-verification gates.
