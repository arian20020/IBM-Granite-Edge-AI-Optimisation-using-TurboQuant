# LLamaSharp VocabOnly Model Probe Design

**Status:** Approved for staged implementation  
**Date:** 2026-08-04  
**Target branch:** `feature/model-inspection`  
**Related decisions:**
- [ADR-001: matched LLamaSharp application runtime](../../architecture/decisions/ADR-001-llamasharp-application-runtime.md)
- [ADR-002: core inspection versus backend verification](../../architecture/decisions/ADR-002-core-inspection-versus-backend-verification.md)

## Purpose

Extend the isolated LLamaSharp feasibility tool from a native-library dry run
to a controlled, read-only model probe using LLamaSharp's supported
`VocabOnly` model-loading option.

This stage answers whether the selected application runtime can open a real
Granite GGUF and expose enough lightweight evidence for pre-Hardware-Fit Model
Inspection.

It does not connect LLamaSharp to the WinUI application yet.

## Selected runtime

```text
LLamaSharp 0.27.0
        ↓
LLamaSharp.Backend.Cpu 0.27.0
        ↓
llama.cpp 3f7c29d318e317b63f54c558bc69803963d7d88c
```

The separate upstream `b9870` campaign remains research evidence and is not
used by this probe.

## Scope

The new command accepts one local GGUF path and performs:

1. command and path validation;
2. pre-probe read-only file identity capture;
3. CPU native-backend dry run;
4. `VocabOnly = true` asynchronous model loading;
5. genuine native progress capture;
6. metadata, vocabulary, tokenizer and chat-template evidence collection;
7. deterministic disposal of `LLamaWeights` and its native handle;
8. post-probe file identity capture;
9. integrity comparison;
10. structured JSON evidence output.

## Command contract

Existing native smoke remains available:

```text
ModelInspection.LlamaSharpSpike
    [--output <json-path>]
```

The new model probe is selected by supplying `--model`:

```text
ModelInspection.LlamaSharpSpike
    --model <gguf-path>
    [--output <json-path>]
    [--cancel-after-ms <positive-integer>]
```

Help remains:

```text
ModelInspection.LlamaSharpSpike --help
```

Default output paths:

```text
Native smoke:
artifacts/model-inspection/llamasharp/runtime-smoke.json

VocabOnly model probe:
artifacts/model-inspection/llamasharp/vocab-only-model-probe.json
```

The output path must never resolve to the selected model path.

## Data flow

```text
Program
    ↓
SpikeOptionsParser
    ↓
ModelProbeSafetyValidator
    ↓
VocabOnlyModelProbe
    ├── ModelFileSnapshotService (before)
    ├── CpuNativeRuntimeConfiguration
    ├── LLamaWeights.LoadFromFileAsync
    │       ├── ModelParams.VocabOnly = true
    │       ├── ModelParams.GpuLayerCount = 0
    │       └── NativeLoadProgressRecorder
    ├── VocabOnlyEvidenceCollector
    ├── LLamaWeights.Dispose
    └── ModelFileSnapshotService (after)
            ↓
VocabOnlyModelProbeResult
            ↓
JsonEvidenceWriter
```

## Folder structure

```text
tools/ModelInspection.LlamaSharpSpike/
├── Program.cs
├── SpikeOptionsParser.cs
├── CpuNativeRuntimeConfiguration.cs
├── JsonEvidenceWriter.cs
├── NativeBackendSmokeProbe.cs
├── NativeBackendSmokeResult.cs
├── README.md
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

The new `ModelProbe` folder contains only model-specific feasibility code. The
existing native smoke remains a separate gate.

## Loading configuration

The model probe uses:

```text
VocabOnly              true
GpuLayerCount           0
UseMemorymap            true
UseMemoryLock           false
CUDA selection          disabled
Vulkan selection        disabled
Automatic CPU fallback  enabled
```

This stage must not create a context, allocate a KV cache, offload layers, run
inference or activate TurboQuant.

## Evidence contract

### Runtime identity

Record:

- LLamaSharp package and version;
- CPU backend package and version;
- LLamaSharp source tag and release commit;
- mapped llama.cpp commit;
- process architecture;
- operating system;
- .NET runtime;
- selected native library name;
- AVX level;
- CUDA and Vulkan flags;
- native loader logs.

### Model file identity

Record before and after:

- filename;
- byte length;
- last-write time in UTC;
- SHA-256.

Do not record the full local model path in JSON. Record a SHA-256 fingerprint
of the canonical path instead, so evidence can correlate repeated runs without
publishing a username or machine-specific directory.

### Runtime model evidence

Record when available:

- runtime description;
- metadata count and sorted key names;
- `general.architecture`;
- `general.name`;
- `general.file_type`;
- `general.quantization_version`;
- `tokenizer.ggml.model`;
- context size;
- runtime-reported model size;
- parameter count;
- embedding size;
- layer count;
- attention-head count;
- KV-head count;
- encoder, decoder, recurrent and diffusion flags.

Values must be recorded as observed. A zero or missing value must not be
replaced by a guessed value from the filename.

### Vocabulary and tokenizer evidence

Record:

- vocabulary count;
- vocabulary type;
- known special-token identifiers and optional decoded text;
- a fixed, non-sensitive tokenizer smoke using `Hello`;
- tokenizer smoke token count or controlled error.

### Chat-template evidence

Record:

- whether the default embedded template exists;
- template length;
- SHA-256 of the template text.

Do not write the full chat template into the evidence file.

### Progress and memory observations

Record:

- genuine `IProgress<float>` samples from `LoadFromFileAsync`;
- elapsed milliseconds for each sample;
- total load duration;
- process working set before load, after load and after disposal;
- process peak working set observed by the operating system.

Memory values are feasibility observations, not a final production memory
estimate.

### Disposal

Record whether the native model handle reports closed after deterministic
`LLamaWeights.Dispose()`.

No `LLamaWeights`, `SafeLlamaModelHandle`, native pointer or arbitrary
exception object may appear in the project-owned result.

## Completion states

```text
Succeeded
Cancelled
Failed
```

These are spike-operation states, not final user-facing model outcomes.

A successful load does not automatically mean `Ready`. Classification remains
future application-service work.

## Failure mapping

Use stable feasibility codes:

| Condition | Code |
|---|---|
| Model path missing | `MI-OP-MODEL-FILE-NOT-FOUND` |
| Model file access denied | `MI-OP-MODEL-FILE-ACCESS-DENIED` |
| Model/file I/O failure | `MI-OP-MODEL-FILE-IO` |
| Native library unavailable | `MI-OP-RUNTIME-UNAVAILABLE` |
| Native architecture mismatch | `MI-OP-RUNTIME-ARCHITECTURE-MISMATCH` |
| LLamaSharp cannot load the model | `MI-PROBE-MODEL-LOAD-FAILED` |
| Probe cancelled | `MI-PROBE-CANCELLED` |
| Original file changed | `MI-OP-MODEL-INTEGRITY-CHANGED` |
| Other runtime/probe failure | `MI-OP-RUNTIME-INSPECTION-FAILED` |

A load failure is evidence for later analysis. This spike must not directly
label the model unsupported, invalid or corrupt.

## Cancellation

Cancellation may come from:

- `Ctrl+C`;
- `--cancel-after-ms` for controlled testing.

Cancellation must:

- reach `LoadFromFileAsync` through a `CancellationToken`;
- be recorded as `Cancelled`;
- retain any genuine progress already observed;
- execute disposal when a handle exists;
- run the post-probe integrity check;
- return process exit code `3` after evidence is written.

## File-integrity rules

- Open the model only with `FileAccess.Read`.
- Never call LLamaSharp save or conversion APIs.
- Reject an evidence output path equal to the model path.
- Capture SHA-256 and last-write data before and after.
- A changed hash, length or timestamp changes an otherwise successful result to
  `Failed` with `MI-OP-MODEL-INTEGRITY-CHANGED`.
- If post-probe integrity capture itself fails, record the verification error
  separately rather than claiming preservation.

## Tests

Deterministic unit tests cover:

- model-probe command parsing;
- cancel-delay validation;
- default output selection;
- protection against writing evidence over the model;
- file snapshot SHA-256;
- unchanged and changed integrity comparisons;
- progress clamping and duplicate suppression;
- enum-as-string JSON serialization;
- atomic evidence replacement.

Native model loading is an integration test and requires a real controlled
Granite GGUF on Windows x64.

## README updates

Update:

- `tools/ModelInspection.LlamaSharpSpike/README.md`;
- new `tools/ModelInspection.LlamaSharpSpike/ModelProbe/README.md`;
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/README.md`;
- `IBM Granite with TurboQuant (Intel)/Features/README.md`;
- the existing LLamaSharp feasibility implementation plan.

Documentation must say that source exists while runtime verification remains
pending until a real model run is reviewed.

## Acceptance criteria

1. Existing no-model native smoke behavior remains available.
2. `--model` selects the VocabOnly probe.
3. The exact matched CPU runtime remains pinned.
4. No Vulkan or TurboQuant package is added.
5. No LLamaSharp package is added to the WinUI project.
6. The probe uses `VocabOnly = true` and `GpuLayerCount = 0`.
7. Evidence contains model, tokenizer, chat-template, progress, disposal and
   integrity fields.
8. Cancellation is separate from failure.
9. The output path cannot overwrite the model.
10. Unit tests and Release build pass.
11. A real controlled Granite run remains a separate target-machine gate.
12. No final model outcome or Hardware Fit claim is produced.

## Non-goals

This stage does not implement:

- the production `ILlamaModelProbe`;
- `IModelInspectionService`;
- `ModelInspectionViewModel`;
- dynamic WinUI progress updates;
- final classifier outcomes;
- full tensor checking;
- full weight allocation;
- context or KV-cache creation;
- inference;
- Vulkan;
- TurboQuant;
- OpenVINO;
- automatic model download.
