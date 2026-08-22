# Model Inspection application runtime adapter

**Status:** Implemented for the protected Windows x64 CPU GGUF path
**Last reviewed:** 2026-08-16

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

## Truthful five-stage boundaries

The worker progress protocol now surrounds real operations rather than
post-hoc labels:

1. Check model package captures and hashes the initial file snapshot.
2. Read model configuration configures the pinned CPU backend, performs the
   `VocabOnly` load, and collects configuration; only this native load may
   carry a genuine fraction.
3. Validate tokenizer and chat setup collects tokenizer/chat evidence and runs
   tokenizer smoke.
4. Validate model structure collects structure, disposes the native handle,
   and captures and compares the final file snapshot.
5. Confirm core runtime compatibility maps and validates completed worker
   evidence before the terminal record.

No runtime, worker, service, or adapter delay is used for visual pacing. The
application publishes `Starting secure inspection…` while mandatory manifest
verification and worker launch continue, and the page owns the separate 550 ms
minimum presentation policy. Cancellation and failure remain immediate.

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

### Final progress-polish evidence

The serialized local RuntimeWorker phase passed against
`27934be2687418d7890b677cfcdabf22f059633d`: the exact 22-class runtime
project passed 189/189 with 189 matching definitions/results and TRX SHA-256
`C5916E795839D9C3DB2A2BA810C1B71E8C2B2363E4D3305A4D80C14C96CB643A`;
the worker project passed 77/77 with SHA-256
`2BE31A252D2063716053D545C29FF582DE10BE5618C349CABE414D183703398B`.
Every adverse counter was zero, the frozen source/status snapshot remained
unchanged, the scoped WER delta was zero, and no relevant process remained.

Historical evidence elsewhere remains labelled as such. Hardware Inspection
is not implemented, and these tests make no strict Figma-pixel, real Narrator,
controlled-OS, or real Granite inference claim.
