# Model Inspection LLamaSharp feasibility tool

**Status:** Successful CPU/VocabOnly baseline verified; expanded Tier 1 tests implemented; fresh CI evidence pending  
**Last reviewed:** 2026-08-04  
**Runtime decision:** [ADR-001](../../docs/architecture/decisions/ADR-001-llamasharp-application-runtime.md)  
**Inspection-depth decision:** [ADR-002](../../docs/architecture/decisions/ADR-002-core-inspection-versus-backend-verification.md)  
**Runtime-test design:** [LLamaSharp runtime test architecture](../../docs/superpowers/specs/2026-08-04-llamasharp-runtime-test-architecture-design.md)

## Purpose

This isolated console project proves and diagnoses the selected managed/native
application runtime before LLamaSharp enters the WinUI application.

```text
LLamaSharp 0.27.0
        ↓
LLamaSharp.Backend.Cpu 0.27.0
        ↓
llama.cpp 3f7c29d318e317b63f54c558bc69803963d7d88c
```

It supports two runtime depths:

```text
Mode 1 — native CPU backend smoke
    → no model
    → proves native-library discovery and runtime identity

Mode 2 — CPU VocabOnly model probe
    → one controlled local GGUF
    → reads proportionate metadata/vocabulary/tokenizer/chat-template evidence
    → verifies disposal and original-file integrity
```

Neither mode is referenced by the WinUI application.

## Why this remains isolated

Native loading can terminate a process before managed exception handling runs.
The first Granite experiment exposed that exact risk when an unsafe VocabOnly
hyperparameter getter reached `GGML_ABORT`. The collector was corrected to use
safe GGUF metadata, but all automated native/model tests now execute the
published tool as a child process.

```text
MSTest host
    ↓
ProbeProcessRunner
    ↓
feasibility executable
    ↓
LLamaSharp / llama.cpp
```

This preserves a stable future production boundary:

```text
ModelInspectionPage
    ↓
ModelInspectionViewModel
    ↓
IModelInspectionService
    ↓
ILlamaModelProbe
```

## Source structure

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
    ├── IModelFileHasher.cs
    ├── Sha256ModelFileHasher.cs
    ├── ModelFileSnapshot.cs
    ├── ModelFileSnapshotService.cs
    ├── ModelProbeSafetyValidator.cs
    ├── NativeLoadProgressRecorder.cs
    ├── ProbeFailure.cs
    ├── ProbeFailureMapper.cs
    ├── SensitiveTextRedactor.cs
    ├── ProbeResultFinalizer.cs
    ├── ChatTemplateEvidenceFactory.cs
    ├── VocabOnlyMetadataProjection.cs
    ├── VocabOnlyModelProbeResult.cs
    ├── VocabOnlyEvidenceCollector.cs
    └── VocabOnlyModelProbe.cs
```

## Exact runtime identity

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

It is research evidence, not the runtime loaded by this tool.

## Native-smoke flow

```text
Program
    → NativeBackendSmokeProbe
    → CpuNativeRuntimeConfiguration
    → NativeLibraryConfig.LLama.DryRun(...)
    → NativeBackendSmokeResult
    → JsonEvidenceWriter
```

The report records package/native identity, operating system, architecture,
selected library, AVX level, CUDA/Vulkan flags, logs, and controlled operational
failure information.

## VocabOnly model-probe flow

```text
Program
    → parse model/output/cancellation options
    → reject output/model collision
    → capture read-only model snapshot before
    → dry-run matched CPU backend
    → LLamaWeights.LoadFromFileAsync
         VocabOnly = true
         GpuLayerCount = 0
    → collect only evidence safe at VocabOnly depth
    → dispose native weights
    → capture model snapshot after
    → apply integrity precedence
    → redact canonical local path
    → write project-owned JSON
```

## Cancellation scopes

The command supports two deliberately different diagnostic scopes.

### Whole operation

```text
--cancel-after-ms <positive integer>
```

The timer starts before file hashing. It verifies the complete outer operation
and may cancel before native backend selection.

### Native load

```text
--cancel-native-after-ms <positive integer>
```

The timer starts immediately before `LLamaWeights.LoadFromFileAsync`. It tests
the managed/native cancellation boundary without spending the delay during
preflight hashing.

The two options are mutually exclusive. `Ctrl+C` continues to cancel the whole
operation.

## Pure project-owned components

### `ProbeFailureMapper`

Maps managed file/runtime exceptions to stable `MI-*` diagnostics. It never
produces a final model outcome.

### `SensitiveTextRedactor`

Removes the canonical model path from failure messages and every native log
entry. The filename and canonical-path SHA-256 may remain useful evidence.

### `ProbeResultFinalizer`

Applies final precedence:

```text
Succeeded + changed model
    → integrity failure

Cancelled + changed model
    → integrity failure

Succeeded/Cancelled + unverifiable integrity
    → integrity-verification failure

Existing runtime/model failure
    → original failure retained
```

Cancellation cannot conceal a changed or unverifiable model.

### `ChatTemplateEvidenceFactory`

Stores only presence, character length, and SHA-256. It never serializes the
full chat template.

### `NativeLoadProgressRecorder`

```text
NaN                     → ignored
negative infinity       → 0
positive infinity       → 1
finite value            → clamped to 0..1
consecutive duplicate   → omitted
```

It records genuine callbacks only; no synthetic percentages are created.

## Verified target-laptop baseline

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

### Corrected deterministic baseline

```text
Configuration:              Release / win-x64
Total tests:                28
Passed:                     28
Failed:                     0
Skipped:                    0
Exit code:                  0
Release build:              PASS
```

### Controlled Granite VocabOnly probe

```text
Run ID:                     20260804-154719
Model:                      granite-4.1-3b-Q4_K_M.gguf
Model size:                 2,099,501,664 bytes
Model SHA-256:              662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29
Result:                     PASS
Process exit code:          0
Evidence schema:            1.1
Completion status:          Succeeded
Architecture:               granite
Declared context:           131,072
Embedding size:             2,560
Layer count:                40
Attention heads:            40
KV heads:                   8
Vocabulary count:           100,352
Tokenizer smoke:            PASS / 1 token
Embedded chat template:     present
Native progress samples:    1
Load duration:              398 ms
Native handle closed:       true
Original GGUF preserved:    true
```

The parameter count was unavailable at this safe depth and remains `null`, not
zero or a filename-derived guess.

## Expanded test architecture

### Tier 1 — model-free normal CI

```text
Deterministic contracts
    dependency/version policy
    command-line matrix
    path and hashing safety
    metadata and template projection
    progress normalization
    failure mapping and redaction
    result precedence
    evidence/type/privacy contracts

Contained native integration
    published CPU smoke
    missing native DLLs
    corrupted native image
    model-free runtime evidence contract
```

Tier 1 source and workflow are implemented. Fresh hosted execution remains
required before the expanded suite is marked verified.

### Tier 2 — trusted real-model runner

Planned separately:

```text
controlled Granite success and repeatability
whole-operation and native-load cancellation
all committed malformed GGUF fixtures
locked model/output scenarios
path/privacy checks
network-listener observation
before/after integrity after every scenario
```

See:

- [Tier 1 implementation plan](../../docs/superpowers/plans/2026-08-04-llamasharp-tier1-runtime-tests.md)
- [Tier 2 implementation plan](../../docs/superpowers/plans/2026-08-04-llamasharp-tier2-real-model-tests.md)
- [Coverage matrix](../../docs/testing/LLamaSharp-Runtime-Test-Coverage-Matrix.md)

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
    --filter "TestCategory=Deterministic" `
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

## Run the VocabOnly model probe

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

## Run native-load cancellation

```powershell
$RunId = Get-Date -Format "yyyyMMdd-HHmmss"
$CancellationOutput =
    "artifacts\model-inspection\llamasharp\runs\$RunId\vocab-only-native-cancellation.json"

dotnet run `
    --project $SpikeProject `
    --configuration Release `
    --no-build `
    --runtime win-x64 `
    -- `
    --model $ModelPath `
    --cancel-native-after-ms 1 `
    --output $CancellationOutput
```

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Requested smoke/probe succeeded and JSON was written |
| `1` | Controlled runtime, probe, integrity, or evidence-writing failure |
| `2` | Invalid or unsafe command-line arguments |
| `3` | VocabOnly model probe was cancelled and JSON was written |

## Safety and non-claims

- CPU is the only selected backend.
- CUDA and Vulkan are disabled.
- Models are opened read-only.
- Evidence output may not equal the model path.
- No context, KV cache, inference, save, conversion, or quantisation occurs.
- Native/runtime failure is not classified as an invalid model.
- Full local paths, full chat templates, native pointers, and handles are not
  serialized.
- The WinUI application has no LLamaSharp package reference from this work.
- Expanded Tier 1 source is not yet a pass claim.
- Tier 2 cancellation, malformed-input, file-access, privacy, and network
  evidence remains pending.
- Full CPU execution, Vulkan, and TurboQuant remain later gates.
