# Model Inspection application runtime adapter

**Status:** Implemented for the protected Windows x64 CPU GGUF path
**Last reviewed:** 2026-08-09

[Back to Model Inspection architecture](../README.md)

## Purpose

This folder is the application-owned adapter between the Model Inspection use
case and the protected worker client. It translates the immutable application
request into one worker command and translates validated worker progress and
terminal evidence back into application contracts.

```text
ModelInspectionRequest
    -> WorkerRequestMapper
    -> IInspectionWorkerClient
    -> protected worker process
    -> WorkerResultMapper
    -> ModelInspectionProbeResult
```

Only the files in this folder know both the application contracts and the
shared worker protocol. The rest of the application depends on
`ILlamaModelProbe` and application-owned types.

## Files and responsibilities

- `ILlamaModelProbe.cs` defines the single lightweight GGUF inspection seam.
- `ModelInspectionProbeResult.cs` separates completed evidence, confirmed
  cooperative cancellation, and operational failure.
- `WorkerRequestMapper.cs` creates a fresh request ID, captures the current
  parent process identity, and maps the exact expected file identity and quick
  scan snapshot.
- `WorkerProcessLlamaModelProbe.cs` executes one short-lived worker through
  `IInspectionWorkerClient`, maps progress, and contains recoverable boundary
  failures as privacy-safe operational failures.
- `WorkerResultMapper.cs` maps all five stages and fail-closes completed
  evidence before application classification.

## Completed-evidence trust checks

The result mapper accepts completed evidence only when it agrees with the
active request and the approved runtime profile. Checks include:

- request identity and terminal record validation;
- selected filename, length, UTC timestamp, before/after hash, and integrity
  preservation;
- recomputation of the canonical-path SHA-256 from the selected request path;
- application quick-scan agreement for architecture, context length, and any
  native model name that is present;
- LLamaSharp `0.27.0`, CPU backend `0.27.0`, mapped llama.cpp commit
  `3f7c29d318e317b63f54c558bc69803963d7d88c`, x64, `VocabOnly`, and native
  backend identity;
- no CUDA, Vulkan, GPU layers, or unknown worker observations;
- successful tokenizer smoke evidence with a positive token count.

Contradictory, incomplete, unexpected, or unapproved evidence becomes an
operational failure. It is never converted into a model outcome.

## Cancellation and privacy

Pre-cancellation propagates as `OperationCanceledException`. A worker terminal
record is the only source of trusted cooperative cancellation. Client launch,
protocol, I/O, and evidence-mapping failures are reduced to stable application
failure records; raw stderr, paths, exception messages, and protocol records
do not enter the presentation layer.

## Scope and non-claims

This adapter supports only the fixed Windows x64 LLamaSharp/llama.cpp CPU
`VocabOnly` route. It does not perform OpenVINO, TurboQuant, Vulkan/GPU setup,
context creation, full inference, quality or performance benchmarking,
conversion execution, Hardware Fit execution, or chat execution.

## Tests

Focused packaged tests cover request mapping, progress and terminal mapping,
evidence rejection, privacy, cancellation, and the worker-client adapter:

- `WorkerRequestMapperTests`
- `WorkerResultMapperTests`
- `WorkerProcessLlamaModelProbeTests`
- `ModelInspectionProbeResultTests`
