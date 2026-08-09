# Model Inspection infrastructure composition

**Status:** Fixed x64 worker closure and default application service composition implemented
**Last reviewed:** 2026-08-09

[Back to Model Inspection architecture](../README.md)

This folder is the only WinUI application source boundary allowed to depend on
`GraniteEdgeAI.ModelInspection.WorkerClient`. It never references the worker
host, LLamaSharp runtime implementation, or native-library APIs.

## Approved root selection

- A packaged process uses `Package.Current.InstalledLocation.Path` after the
  native package-identity probe confirms package identity.
- A controlled unpackaged development/test process uses
  `AppContext.BaseDirectory`. This route verifies the static snapshot but does
  not claim protection from a concurrent writer after verification.
- An invalid packaged root fails closed and never falls back to the unpackaged
  root.
- Current directory and `PATH` search never participate.

The client resolves exactly:

```text
ModelInspection\Worker\GraniteEdgeAI.ModelInspection.Worker.exe
```

The detached closure manifest is stored at
`ModelInspection\worker-manifest.json`. The x64 application build and packaged
test layout contain the fixed worker subtree, including LLamaSharp and CPU
native dependencies that remain outside the WinUI process.

Immediately before execution, composition compares the detached manifest with
the trusted embedded copy, rejects reparse points or missing/extra files, and
verifies every declared path, length, and SHA-256 digest. A mismatch returns a
fixed integrity failure before process launch. The installed-package route
combines that verification with Windows package immutability.

## Application service composition

`ModelInspectionWorkerComposition.CreateDefaultService()` creates the complete
x64 application route:

```text
ModelInspectionService
    -> ModelInspectionClassifier
    -> WorkerProcessLlamaModelProbe
    -> manifest-verifying protected worker client
```

The page reaches this route only through
`ModelInspectionServiceComposition.CreateDefault()`; page and ViewModel code do
not own worker/protocol composition.

## Scope and non-claims

This is the fixed Windows x64 CPU LLamaSharp/llama.cpp `VocabOnly` route. It
does not add OpenVINO, TurboQuant, Vulkan/GPU, context creation, full inference,
benchmarking, conversion, Hardware Fit, or chat execution. Extracted MSIX
closure/notice review and hosted exact-head release attestation remain pending.

## Tests

`ModelInspectionWorkerCompositionTests` covers approved roots, fixed layout,
manifest and payload integrity, failure containment, and the real N-001
packaged worker through the application service.
