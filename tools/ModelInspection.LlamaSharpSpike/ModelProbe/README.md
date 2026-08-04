# LLamaSharp VocabOnly model probe

**Status:** Source implemented; deterministic tests and real-model verification pending  
**Parent:** [LLamaSharp feasibility tool](../README.md)  
**Design:** [VocabOnly model-probe design](../../../docs/superpowers/specs/2026-08-04-llamasharp-vocab-only-model-probe-design.md)

## Purpose

This folder contains the model-specific part of the isolated LLamaSharp
feasibility tool.

It asks one narrow question:

> Can the exact matched LLamaSharp CPU runtime open a controlled Granite GGUF
> through `VocabOnly` and expose enough lightweight evidence for later Model
> Inspection work without creating a context or running inference?

The source in this folder is not referenced by the WinUI application.

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

### `VocabOnlyModelProbeResult.cs`

Defines framework-neutral evidence records for:

- completion state;
- runtime identity;
- selected CPU backend;
- before/after file identity;
- integrity comparison;
- runtime model information;
- metadata key names and selected values;
- vocabulary and special tokens;
- tokenizer smoke evidence;
- chat-template presence, length and hash;
- native progress;
- memory observations;
- disposal status;
- controlled failures and logs.

No LLamaSharp handle, native pointer or XAML type appears in these records.

### `VocabOnlyEvidenceCollector.cs`

This is one of the only spike components that directly receives
`LLamaWeights`. It reads evidence while the native handle is valid and returns
project-owned records.

It records a chat-template hash rather than copying the complete template into
JSON.

### `VocabOnlyModelProbe.cs`

Coordinates the complete read-only operation:

1. validate and fingerprint the selected file;
2. configure the CPU backend;
3. dry-run native-library selection;
4. load asynchronously using `VocabOnly`;
5. collect evidence;
6. dispose native weights;
7. verify the original file;
8. map completion and failures;
9. return one immutable evidence result.

## Current command

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

Controlled cancellation:

```powershell
--cancel-after-ms 250
```

The user may also press `Ctrl+C`. A controlled cancellation writes evidence and
returns exit code `3`.

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

`VocabOnly` is being evaluated, not yet accepted as sufficient for production
inspection. A real controlled Granite run must show which evidence is actually
available and what memory behavior occurs.

## Tests

Deterministic tests live in the adjacent test project and cover:

- command parsing;
- output/model collision rejection;
- file snapshots and integrity comparison;
- native-progress recording;
- generic JSON evidence writing.

The native model load itself requires a real GGUF and remains a Windows x64
integration gate.
