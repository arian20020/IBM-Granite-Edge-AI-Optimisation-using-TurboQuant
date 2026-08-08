# LLamaSharp VocabOnly model probe

**Status:** Corrected Granite probe, cancellation, hostile-input and privacy gates verified  
**Last reviewed:** 2026-08-05  
**CLI:** [LLamaSharp feasibility tool](../../../tools/ModelInspection.LlamaSharpSpike/README.md)
**Design:** [VocabOnly model-probe design](../../../docs/superpowers/specs/2026-08-04-llamasharp-vocab-only-model-probe-design.md)  
**Tier 2 evidence:** [Local trusted verification](../../../docs/testing/evidence/2026-08-05-llamasharp-tier2-local-verification.md)

## Purpose

This folder contains the model-specific production CPU/VocabOnly runtime used
by the isolated LLamaSharp feasibility CLI. It is not connected to the worker
or WinUI application yet.

It answers one narrow question:

> Can the exact matched LLamaSharp CPU runtime open a controlled Granite GGUF
> through `VocabOnly` and expose proportionate evidence for pre-Hardware-Fit
> Model Inspection without creating a context or running inference?

For the tested Granite 4.1 3B Q4_K_M model, the answer is **yes**. The corrected
success path, repeatability, two cancellation scopes, malformed inputs,
file-access failures, evidence privacy and model preservation have all passed
the trusted local suite.

This source is not referenced by the worker or WinUI application.

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

## Flow

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
integrity comparison and result finalisation
    ↓
VocabOnlyModelProbeResult
```

## File inventory

### `IModelFileHasher.cs` and `Sha256ModelFileHasher.cs`

Provide a testable asynchronous hashing boundary used by model snapshots.

### `ModelFileSnapshot.cs` and `ModelFileSnapshotService.cs`

Record filename, canonical-path fingerprint, length, last-write time and
SHA-256. The model is opened with:

```text
FileMode.Open
FileAccess.Read
FileShare.Read
FileOptions.Asynchronous | SequentialScan
```

The full machine-local path is not stored in the result.

### `ModelProbeSafetyValidator.cs`

Rejects an evidence output path that resolves to the model path.

### `NativeLoadProgressRecorder.cs`

Records genuine `IProgress<float>` callbacks only:

```text
NaN                     → ignored
negative infinity       → 0
positive infinity       → 1
finite value            → clamped to 0..1
consecutive duplicate   → omitted
```

### `VocabOnlyMetadataProjection.cs`

Projects structural values from GGUF metadata that remains available at
`VocabOnly` depth:

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
the filename and does not call unsafe native hyperparameter getters.

### `ChatTemplateEvidenceFactory.cs`

Stores only template presence, character length and SHA-256. It never stores
the full template text.

### `ProbeFailureMapper.cs`

Maps file, runtime and load failures to stable technical `MI-*` codes without
creating a final model outcome.

### `SensitiveTextRedactor.cs`

Removes the canonical model path from failure messages and native logs.

### `ProbeResultFinalizer.cs`

Ensures model integrity outranks success or cancellation:

```text
Succeeded/Cancelled + changed model
    → MI-OP-MODEL-INTEGRITY-CHANGED

Succeeded/Cancelled + unverifiable integrity
    → MI-OP-MODEL-INTEGRITY-VERIFICATION-FAILED

Existing runtime/model failure
    → original failure retained
```

### `VocabOnlyModelProbeResult.cs`

Defines framework-neutral evidence records. LLamaSharp handles, native
pointers, exception objects and XAML types do not cross this boundary.

### `VocabOnlyEvidenceCollector.cs`

Reads only evidence safe at the selected depth:

- metadata;
- vocabulary and special-token information;
- a fixed non-sensitive tokenizer smoke;
- chat-template metadata.

It deliberately avoids the native model-hyperparameter properties that caused
the earlier llama.cpp abort after `vocab_only` returned early.

### `VocabOnlyModelProbe.cs`

Coordinates validation, snapshots, backend selection, loading, evidence
collection, disposal, integrity, redaction and controlled completion.

## Verification history

### First real-model run — implementation defect found

The initial collector reached:

```text
llama-hparams.cpp:35: fatal error
Windows exit code: -1073740791
```

The process abort came from unsafe native hyperparameter access after
`vocab_only` skipped that native state. It was an implementation defect, not
proof that Granite was invalid or unsupported. The model SHA-256 stayed
unchanged.

### Corrected Granite success

```text
Model:                        granite-4.1-3b-Q4_K_M.gguf
Model bytes:                  2,099,501,664
Model SHA-256:                662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29
Process exit code:            0
Evidence schema:              1.1
Completion status:            Succeeded
Architecture:                 granite
Model name:                   Granite 4.1 3b
Declared context:             131,072
Embedding size:               2,560
Layer count:                  40
Attention heads:              40
KV heads:                     8
Metadata count:               31
Vocabulary count:             100,352
Tokenizer smoke:              passed / 1 token
Embedded chat template:       present
Native handle closed:         true
Original model preserved:     true
```

The parameter count was unavailable at this depth and remains `null`, not zero
or a filename-derived guess.

### Expanded verified test layers

```text
Tier 1 deterministic tests:   170 / 170 passed
Tier 1 contained native:      4 / 4 passed
Tier 2 trusted real-model:    20 / 20 passed
Tier 2 evidence files:        56 scanned
Tier 2 privacy findings:      0
Model hash after Tier 2:      unchanged
```

The Tier 2 campaign verified success, three-run repeatability, post-preflight
and native-load cancellation, malformed inputs, random bytes, file-access
failures, unsafe output paths, evidence privacy and network observation.

## Completion states

```text
Succeeded
Cancelled
Failed
```

These describe the probe operation only. They are not the application outcomes
`Ready`, `ReadyWithWarnings`, `ConversionRequired`, `Unsupported` or
`InvalidOrIncomplete`.

## Stable technical codes

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

A load or infrastructure failure remains technical evidence for the future
classifier rather than an automatic invalid/unsupported result.

## Feasibility conclusion

The current runtime can, for the controlled Granite model:

- open the GGUF through LLamaSharp;
- select the intended CPU backend only;
- obtain architecture and model metadata;
- inspect vocabulary and tokenizer behaviour;
- detect the embedded chat template;
- report genuine native progress;
- honour tested cancellation scopes;
- dispose the native model handle;
- preserve the original model;
- contain malformed/native failures through child processes;
- complete without creating a context or running inference.

This is sufficient to design the production core-inspection adapter boundary.
It is not evidence for full CPU execution, Vulkan, TurboQuant, Hardware Fit,
context creation or generation.

## Safety and non-claims

The probe:

- never opens the model for write;
- does not call save, conversion or quantisation APIs;
- does not create a context or KV cache;
- does not run generation;
- does not offload GPU layers;
- does not use Vulkan or TurboQuant;
- does not upload the model;
- does not connect to the WinUI page.

The next application slice should define project-owned contracts and implement
`ILlamaModelProbe` through a protected worker-process boundary before the
service, classifier, ViewModel and live WinUI progress are connected.
