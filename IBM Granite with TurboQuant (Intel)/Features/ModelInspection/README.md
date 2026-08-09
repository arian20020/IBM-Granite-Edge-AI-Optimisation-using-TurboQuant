# Model Inspection architecture

**Status:** End-to-end local GGUF inspection journey implemented for the protected Windows x64 CPU path
**Last reviewed:** 2026-08-09

[Back to application feature architecture](../README.md)

## Purpose

Model Inspection is onboarding stage two. It receives one immutable validated
GGUF selection, runs lightweight core-runtime inspection in a protected worker,
and presents one controlled application result before any Hardware Fit work.

```text
Validated Model Import
    -> exact ModelInspectionRequest
ModelInspectionPage / ModelInspectionViewModel
    -> ModelInspectionService
    -> WorkerProcessLlamaModelProbe
    -> protected x64 worker process
    -> LLamaSharp 0.27.0 / matched llama.cpp CPU VocabOnly
    -> trusted application evidence
    -> ModelInspectionClassifier
    -> Ready or ReadyWithWarnings
    -> terminal page presentation
```

The application and worker remain separated by versioned contracts and bounded
redirected streams. LLamaSharp and native CPU libraries stay in the fixed child
worker subtree; they are not loaded into the WinUI process.

## Accepted architecture

- [ADR-001 - matched LLamaSharp application runtime](../../../docs/architecture/decisions/ADR-001-llamasharp-application-runtime.md)
- [ADR-002 - core inspection versus backend verification](../../../docs/architecture/decisions/ADR-002-core-inspection-versus-backend-verification.md)
- [ADR-003 - protected Model Inspection worker](../../../docs/architecture/decisions/ADR-003-protected-model-inspection-worker.md)

Application ownership is intentionally layered:

```text
Page
    -> ViewModel (attempt identity, commands, stale suppression)
    -> Service (orchestration and terminal semantics)
    -> Classifier (reliable evidence only)
    -> Runtime adapter (application/worker mapping)
    -> Infrastructure (fixed root and manifest-verifying client)
    -> Worker / production LLamaSharp runtime
```

## Current capability

| Capability | Status |
|---|---|
| immutable Model Import request and exact-instance handoff | Implemented |
| automatic page start once per navigation | Implemented |
| five live progress stages with explicit status/fraction | Implemented |
| functional Cancel with confirmed-cancellation semantics | Implemented |
| retry same request as a fresh attempt | Implemented |
| choose another model and fresh onboarding reset | Implemented |
| stale callback suppression across replacement/navigation | Implemented |
| request/progress/evidence/result contracts | Implemented |
| worker-to-application request/result mappers | Implemented |
| deterministic classifier and application service | Implemented |
| privacy-safe initial/progress/terminal presentation | Implemented |
| fixed manifest-verified x64 worker closure | Implemented in build/test package |
| real N-001 packaged page journey through all five stages | Implemented and locally tested |
| Hardware Fit or conversion execution | Not implemented in this feature slice |
| extracted MSIX and hosted exact-head release attestation | Pending |

## Source hierarchy

```text
Features/ModelInspection/
|-- ModelInspectionPage.xaml(.cs)  page lifecycle and four-card assignment
|-- Contracts/                     framework-neutral application language
|-- Runtime/                       worker request/result adapter
|-- Classification/                reliable-evidence outcome policy
|-- Services/                      use-case orchestration and x64 entry point
|-- ViewModels/                    async attempt and command lifecycle
|-- Presentation/                  privacy-safe snapshot construction
|-- Models/                        WinUI presentation shapes
|-- Controls/                      reusable four-card rendering
`-- Infrastructure/                fixed worker root/client composition
```

Shared protocol/transport, worker host, client, and production LLamaSharp
runtime remain separate repository projects.

## Page lifecycle

`ModelInspectionPage.OnNavigatedTo` requires the exact
`ModelInspectionRequest`, retires any prior ViewModel, creates a fresh
navigation-owned ViewModel, subscribes to property/command/event changes, and
applies the initial four-card snapshot.

The first `Loaded` event starts at most one automatic attempt for that
navigation. Progress, command state, and terminal results replace the complete
four-card snapshot. `OnNavigatedFrom` invalidates page ownership before
cancellation, unsubscribes, deactivates/disposes the ViewModel, and prevents
retired callbacks from repainting the page.

Completed results offer Choose another model. Cancellation or operational
failure offers Retry and Choose another. The page reports choose-another intent
to the onboarding shell; it does not manipulate the shell frame itself.

## Five user-visible stages

```text
1. Check model package
2. Read model configuration
3. Validate tokenizer and chat setup
4. Validate model structure
5. Confirm core runtime compatibility
```

The worker executes those stages with CPU `VocabOnly` loading, metadata and
tokenizer smoke evidence, file integrity verification, and deterministic
runtime identity. A genuine worker fraction is shown when available; otherwise
the current ring remains indeterminate.

## Outcome policy

Execution state and model outcome remain distinct:

```text
Completed
    -> reliable evidence
    -> Ready when tokenizer smoke passed and chat template is present
    -> ReadyWithWarnings when tokenizer smoke passed and template is absent

Cancelled
    -> only a cooperative worker terminal proves cancellation

OperationalFailure
    -> no reliable model classification is invented
```

The presentation layer supports all six domain model outcomes for future
classifier policies, but the current protected GGUF route produces only
`Ready` or `ReadyWithWarnings` from completed evidence.

## Runtime and package identity

```text
Protocol version:          1
Worker ID:                 GraniteEdgeAI.ModelInspection.Worker
Runtime profile:           llamasharp-0.27.0-cpu-win-x64-vocab-only-v1
LLamaSharp:                0.27.0
CPU backend package:       0.27.0
Mapped llama.cpp commit:   3f7c29d318e317b63f54c558bc69803963d7d88c
Process architecture:      x64
Inspection mode:           VocabOnly
```

The fixed worker subtree is resolved only from the installed package root or
controlled unpackaged `AppContext.BaseDirectory`. A detached manifest is
checked against the embedded trusted manifest, then every staged path, length,
and SHA-256 digest is verified before launch. No current-directory or `PATH`
fallback exists.

## Tests and evidence

Focused packaged tests cover contracts, application boundary fitness, mapper,
classifier, service, ViewModel, presentation, page lifecycle, onboarding shell
lifecycle, and the manifest-verifying composition. A real packaged N-001 page
journey reaches all five stage snapshots and final `Ready` through the actual
protected worker/service path.

The reconciled full packaged Release/x64 run passes 330/330 with zero failed,
skipped, or not-executed tests. This is local application evidence, not the
still-pending extracted-MSIX or hosted exact-head release attestation.

N-001 is a controlled zero-tensor tokenizer fixture. It proves the lightweight
VocabOnly journey and native closure, not trusted Granite inference, quality,
or performance.

## Strict scope and non-claims

This completed feature slice is only local GGUF Model Inspection through the
Windows x64 CPU LLamaSharp/llama.cpp `VocabOnly` path.

It does not perform or prove:

- OpenVINO;
- TurboQuant;
- Vulkan or GPU initialization/offload;
- full inference or context creation;
- performance or quality benchmarking;
- model conversion execution;
- Hardware Fit execution/navigation;
- chat execution;
- non-x64 inspection;
- extracted MSIX closure/notice approval or hosted exact-head release evidence.

Those are downstream product/release gates and must not be folded into the
meaning of Model Inspection completion.

## Related documentation

- [Application contracts](./Contracts/README.md)
- [Runtime adapter](./Runtime/README.md)
- [Classification](./Classification/README.md)
- [Application service](./Services/README.md)
- [ViewModel](./ViewModels/README.md)
- [Presentation](./Presentation/README.md)
- [Controls](./Controls/README.md)
- [Infrastructure](./Infrastructure/README.md)
- [Completeness matrix](../../../docs/testing/Model-Inspection-Test-Completeness-Matrix.md)
