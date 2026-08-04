# LLamaSharp VocabOnly model probe

**Status:** First target-laptop run diagnosed; metadata-only correction implemented; verification rerun pending  
**Last reviewed:** 2026-08-04  
**Parent:** [LLamaSharp feasibility tool](../README.md)  
**Design:** [VocabOnly model-probe design](../../../docs/superpowers/specs/2026-08-04-llamasharp-vocab-only-model-probe-design.md)

## Purpose

This folder contains the model-specific part of the isolated LLamaSharp
feasibility tool.

It asks one narrow question:

> Can the exact matched LLamaSharp CPU runtime open a controlled Granite GGUF
> through `VocabOnly` and expose enough lightweight evidence for later Model
> Inspection without creating a context or running inference?

The source in this folder is not referenced by the WinUI application.

## Selected runtime

```text
LLamaSharp                  0.27.0
LLamaSharp.Backend.Cpu      0.27.0
Mapped llama.cpp commit     3f7c29d318e317b63f54c558bc69803963d7d88c
Initial runtime identifier  win-x64
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

It deliberately does not call native model-hyperparameter properties such as:

```text
ContextSize
EmbeddingSize
LayerCount
HeadCount
KVHeadCount
HasEncoder
HasDecoder
IsRecurrent
IsDiffusion
Description
```

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

## First target-laptop run — 2026-08-04

### Controlled input

```text
File:
granite-4.1-3b-Q4_K_M.gguf

Size:
2,099,501,664 bytes

SHA-256 before:
662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29
```

### Results

```text
Matched LLamaSharp CPU native-library smoke:
PASS

Deterministic tests at that revision:
24 passed, 1 failed because one assertion matched incidental error wording

Real VocabOnly model run:
PROCESS TERMINATED

Native message:
llama-hparams.cpp:35: fatal error

Windows process exit code:
-1073740791

Evidence JSON:
not written because the native process terminated before managed cleanup

SHA-256 after:
662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29

Original GGUF preserved:
YES
```

### Root cause

The mapped llama.cpp implementation returns from model-hyperparameter loading
when `vocab_only` is enabled. Therefore values such as layer/head arrays are not
initialised at this inspection depth.

The first collector nevertheless called LLamaSharp's native `HeadCount`
property. That property asks llama.cpp for head count at layer zero. Because the
VocabOnly model has no populated native layers, llama.cpp calls
`GGML_ABORT("fatal error")`.

The process-level abort bypassed C# exception handling and prevented the JSON
writer and managed `finally` blocks from completing.

This result means:

```text
unsafe evidence accessor used at the selected inspection depth
```

It does **not** prove:

```text
Granite 4.1 is invalid
Granite 4.1 is unsupported
The GGUF is corrupt
The CPU backend failed to load
```

### Corrective changes

The branch now:

- makes the cancellation-parser test assert the required option names rather
  than incidental filler wording;
- adds metadata-projection tests;
- projects structural values from architecture-scoped GGUF metadata;
- removes every known hyperparameter-dependent getter from the VocabOnly
  collector;
- makes unavailable evidence fields nullable;
- records evidence schema version `1.1`.

The correction still requires a fresh test/build/real-model rerun before it can
be described as passing.

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
--cancel-after-ms 250
```

The user may also press `Ctrl+C`. A controlled cancellation writes evidence and
returns exit code `3` when native code returns control normally.

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

A process-level native abort cannot be converted into an in-process managed
result. If the rerun still terminates inside native loading after the unsafe
collector calls have been removed, the feasibility result will trigger review
of the worker-process hardening option already preserved behind
`ILlamaModelProbe`.

## Tests

Deterministic tests in the adjacent test project cover:

- command parsing;
- semantic cancellation-error behavior;
- output/model collision rejection;
- file snapshots and integrity comparison;
- native-progress recording;
- metadata-only structural projection;
- generic JSON evidence writing.

The native model load itself remains a Windows x64 integration gate.
