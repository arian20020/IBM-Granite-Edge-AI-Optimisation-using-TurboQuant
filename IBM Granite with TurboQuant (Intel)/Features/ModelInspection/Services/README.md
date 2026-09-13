# Model Inspection application service

**Status:** Implemented and composed for Windows x64
**Last reviewed:** 2026-08-09

[Back to Model Inspection architecture](../README.md)

## Purpose

The service runs one complete application-owned Model Inspection use case. It
coordinates the isolated runtime probe, preserves terminal-state meaning, and
classifies only trusted completed evidence.

```text
IModelInspectionService
    -> ModelInspectionService
       -> ILlamaModelProbe
       -> IModelInspectionClassifier (completed evidence only)
    -> ModelInspectionExecutionResult
```

## Terminal semantics

- `Completed` contains one classified `ModelInspectionResult`.
- `Cancelled` is returned only from the probe's confirmed cooperative worker
  terminal.
- `OperationalFailure` contains no invented model outcome.
- Caller pre-cancellation is observed before worker execution.

The service records start time immediately before the probe and completion time
immediately before classification through an injectable `TimeProvider`.

## Composition

`ModelInspectionServiceComposition.CreateDefault()` is the page-facing entry
point. On the supported x64 build it delegates to the fixed, manifest-verifying
worker composition. Other architectures fail explicitly with
`PlatformNotSupportedException`; they do not silently use another backend.

The default x64 chain is:

```text
ModelInspectionService
    -> WorkerProcessLlamaModelProbe
    -> ManifestVerifyingInspectionWorkerClient
    -> fixed packaged worker root
    -> GraniteEdgeAI.ModelInspection.Worker.exe
```

UI code does not compose the worker client or reference protocol records.

## Scope and non-claims

The composed service is the local GGUF Windows x64 CPU LLamaSharp/llama.cpp
`VocabOnly` route only. It does not select OpenVINO, TurboQuant, Vulkan/GPU,
create an inference context, benchmark quality/performance, run conversion,
execute Hardware Fit, or start chat.

The build/test package verifies the fixed worker closure. Extracted MSIX and
hosted exact-head release attestation remain pending.

## Tests

- `ModelInspectionServiceTests` covers orchestration, timing, classification,
  cancellation, failure preservation, and caller pre-cancellation.
- `ModelInspectionWorkerCompositionTests` exercises the real packaged N-001
  worker through this application service and verifies completed `Ready`
  evidence after all five stages.
