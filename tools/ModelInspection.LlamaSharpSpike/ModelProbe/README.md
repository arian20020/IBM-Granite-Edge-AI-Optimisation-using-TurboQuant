# LLamaSharp VocabOnly model probe

**Status:** Corrected target-laptop probe passed; controlled cancellation verification remains  
**Last reviewed:** 2026-08-04  
**Parent:** [LLamaSharp feasibility tool](../README.md)  
**Design:** [VocabOnly model-probe design](../../../docs/superpowers/specs/2026-08-04-llamasharp-vocab-only-model-probe-design.md)

## Purpose

This folder contains the model-specific part of the isolated LLamaSharp
feasibility tool.

It answers one narrow question:

> Can the exact matched LLamaSharp CPU runtime open a controlled Granite GGUF
> through `VocabOnly` and expose proportionate evidence for pre-Hardware-Fit
> Model Inspection without creating a context or running inference?

The corrected target-laptop run shows that the answer is **yes for the tested
Granite 4.1 3B Q4_K_M model**. Cancellation still needs a dedicated controlled
run before the complete feasibility stage is closed.

The source in this folder is not referenced by the WinUI application.

## Selected runtime

```text
LLamaSharp                  0.27.0
LLamaSharp.Backend.Cpu      0.27.0
Mapped llama.cpp commit     3f7c29d318e317b63f54c558bc69803963d7d88c
Runtime identifier          win-x64
Process architecture        X64
Selected native library     LLama / AVX-512
GPU layers                  0
CUDA                        disabled
Vulkan                      disabled
```

The standalone upstream `b9870` campaign remains separate research evidence.

## Current flow

```text
selected local GGUF
    ↓
ModelProbeSafetyValidator
    ↓
ModelFileSnapshotService (before)
    ↓
matched LLamaSharp CPU backend dry run
    ↓
LLamaWeights.LoadFromFileAsync
    ├── VocabOnly = true
    ├── GpuLayerCount = 0
    ├── UseMemorymap = true
    └── cancellation + genuine native progress
    ↓
VocabOnlyEvidenceCollector
    ├── GGUF metadata projection
    ├── vocabulary and special tokens
    ├── fixed tokenizer smoke
    └── chat-template metadata
    ↓
LLamaWeights.Dispose
    ↓
ModelFileSnapshotService (after)
    ↓
integrity comparison
    ↓
VocabOnlyModelProbeResult
```

## File inventory

### `ModelFileSnapshot.cs`

Defines:

- `ModelFileSnapshot`;
- `ModelFileIntegrityComparison`.

The snapshot records filename, canonical-path fingerprint, length, last-write
time and SHA-256. It does not record the full machine-local path.

### `ModelFileSnapshotService.cs`

Opens the model with:

```text
FileMode.Open
FileAccess.Read
FileShare.Read
FileOptions.Asynchronous | SequentialScan
```

It verifies that length and last-write time do not change while SHA-256 is
being calculated.

### `ModelProbeSafetyValidator.cs`

Rejects an evidence path that resolves to the model path. This prevents a
misconfigured command from replacing a GGUF with JSON evidence.

### `NativeLoadProgressRecorder.cs`

Implements `IProgress<float>` and records only fractions genuinely reported by
LLamaSharp. Values are clamped to `0..1`; consecutive duplicates are omitted.
No synthetic percentages or estimated time remaining are created.

### `VocabOnlyMetadataProjection.cs`

Projects structural values from ordinary GGUF metadata that remains available
in `VocabOnly` mode:

```text
general.architecture
general.name
general.file_type
general.quantization_version
tokenizer.ggml.model
<architecture>.context_length
<architecture>.embedding_length
<architecture>.block_count
<architecture>.attention.head_count
<architecture>.attention.head_count_kv
general.parameter_count, when present
```

Missing or malformed values become `null`. The projection does not guess from
the filename and does not call native hyperparameter accessors.

### `VocabOnlyModelProbeResult.cs`

Defines framework-neutral evidence records for:

- completion state;
- runtime identity;
- selected CPU backend;
- before/after file identity;
- integrity comparison;
- metadata-derived model information;
- vocabulary and special tokens;
- tokenizer smoke evidence;
- chat-template presence, length and hash;
- native progress;
- memory observations;
- disposal status;
- controlled failures and logs.

Hyperparameter-dependent fields are nullable because the selected depth may not
safely expose them. No LLamaSharp handle, native pointer or XAML type appears in
these records.

### `VocabOnlyEvidenceCollector.cs`

This is one of the only spike components that receives `LLamaWeights`.

For the current depth it reads only:

- `weights.Metadata`;
- `weights.Vocab`;
- vocabulary token conversion/tokenisation operations.

It deliberately does not call native model-hyperparameter properties that are
unsafe after llama.cpp has returned early for `vocab_only`.

The chat template is read from `tokenizer.chat_template` metadata and stored as
presence, length and SHA-256 rather than complete text.

### `VocabOnlyModelProbe.cs`

Coordinates the complete read-only operation:

1. validate and fingerprint the selected file;
2. configure the CPU backend;
3. dry-run native-library selection;
4. load asynchronously using `VocabOnly`;
5. collect safe evidence;
6. dispose native weights;
7. verify the original file;
8. map completion and failures;
9. return one immutable evidence result.

## Target-laptop verification history — 2026-08-04

### Controlled input

```text
File:
granite-4.1-3b-Q4_K_M.gguf

Size:
2,099,501,664 bytes

SHA-256:
662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29
```

### First run — diagnosed implementation failure

The first run terminated at:

```text
llama-hparams.cpp:35: fatal error
Windows exit code: -1073740791
```

The original collector called native layer/head hyperparameter getters after
llama.cpp had deliberately skipped hyperparameter loading for `vocab_only`.
The process abort was therefore a probe implementation error, not evidence that
the Granite model was invalid or unsupported. The model SHA-256 remained
unchanged.

### Corrected deterministic verification

```text
Test project:        ModelInspection.LlamaSharpSpike.Tests
Configuration:       Release / win-x64
Total:               28
Passed:              28
Failed:              0
Skipped:             0
Exit code:           0
```

The corrected Release `win-x64` spike project also built successfully.

### Corrected real-model run

```text
Run ID:                      20260804-154719
Process exit code:           0
Evidence schema:             1.1
Completion status:           Succeeded
Failure code:                none
VocabOnly requested:         true
GPU layer count:             0
Native library:              LLama
AVX level:                   AVX-512
CUDA selected:               false
Vulkan selected:             false
Architecture:                granite
Model name:                  Granite 4.1 3b
GGUF file type:              15
Quantisation version:        2
Tokenizer model:             gpt2
Declared context:            131,072
Embedding size:              2,560
Layer count:                 40
Attention head count:        40
KV-head count:               8
Metadata count:              31
Vocabulary count:            100,352
Tokenizer smoke:             passed
Tokenizer smoke token count: 1
Embedded chat template:      present
Native progress samples:     1
Load duration:               398 ms
Native handle closed:        true
File preserved:              true
```

The parameter count was `null` because the tested GGUF did not expose a safely
usable `general.parameter_count` value at this depth. That means unavailable,
not zero and not invalid. The earlier validated quick scan can continue to
provide the user-facing parameter-size label until a deeper verified runtime
source is available.

### Independent preservation check

PowerShell calculated the same SHA-256 immediately before and immediately after
the corrected runtime probe:

```text
662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29
```

The probe's own integrity result also reported `FilePreserved = true`.

## Feasibility conclusion

The corrected result proves that the matched CPU runtime can, for the tested
Granite model:

- open the GGUF through LLamaSharp;
- use the intended CPU backend only;
- obtain architecture and model metadata;
- inspect vocabulary and tokenizer behaviour;
- detect an embedded chat template;
- report genuine native loading progress;
- dispose the native model handle;
- preserve the original model file;
- complete without creating a context or running inference.

This is sufficient to proceed toward the first production **core inspection
adapter**, provided the dedicated cancellation run also succeeds. It is not
evidence for Vulkan, TurboQuant, GPU offload, Hardware Fit, context creation or
generation.

## Current command

```powershell
dotnet run `
    --project "tools\ModelInspection.LlamaSharpSpike\ModelInspection.LlamaSharpSpike.csproj" `
    --configuration Release `
    --no-build `
    --runtime win-x64 `
    -- `
    --model "C:\Users\Arian\Downloads\granite-4.1-3b-Q4_K_M.gguf" `
    --output "artifacts\model-inspection\llamasharp\vocab-only-model-probe.json"
```

Controlled cancellation:

```powershell
--cancel-after-ms 1
```

A controlled cancellation must write evidence, report `Cancelled`, preserve the
model, close any native handle that was created, and return exit code `3`.

## Completion states

```text
Succeeded
Cancelled
Failed
```

These describe the feasibility operation only. They are not the final
application outcomes `Ready`, `Unsupported`, `Invalid`, or similar.

## Failure codes

```text
MI-OP-MODEL-FILE-NOT-FOUND
MI-OP-MODEL-FILE-ACCESS-DENIED
MI-OP-MODEL-FILE-IO
MI-OP-RUNTIME-UNAVAILABLE
MI-OP-RUNTIME-ARCHITECTURE-MISMATCH
MI-PROBE-MODEL-LOAD-FAILED
MI-PROBE-CANCELLED
MI-OP-MODEL-INTEGRITY-CHANGED
MI-OP-MODEL-INTEGRITY-VERIFICATION-FAILED
MI-OP-RUNTIME-INSPECTION-FAILED
```

A model-load failure is retained as technical evidence. This spike does not
make the final classifier decision.

## Safety and non-claims

The probe:

- does not open the model for write;
- does not call save, conversion or quantisation APIs;
- does not create a context;
- does not allocate a KV cache;
- does not run generation;
- does not offload GPU layers;
- does not use Vulkan;
- does not use TurboQuant;
- does not upload the model or evidence;
- does not connect to the Model Inspection page.

## Tests

Deterministic tests in the adjacent test project now pass `28/28` and cover:

- command parsing;
- semantic cancellation-error behaviour;
- output/model collision rejection;
- file snapshots and integrity comparison;
- native-progress recording;
- metadata-only structural projection;
- generic JSON evidence writing;
- runtime package-boundary policy.

The next integration verification is controlled cancellation during the real
model probe. After that, the next implementation stage is the project-owned
runtime contracts and `ILlamaModelProbe` adapter boundary.
