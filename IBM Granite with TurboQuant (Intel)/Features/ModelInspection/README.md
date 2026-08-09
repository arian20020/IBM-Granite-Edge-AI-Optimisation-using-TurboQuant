# Model Inspection architecture

**Status:** Presentation, application contracts, protected worker/process boundary, production CPU/VocabOnly engine, and fixed x64 application worker closure implemented; application mapping, classification, and live UI execution remain later gates
**Last reviewed:** 2026-08-09

[← Application feature architecture](../README.md)

## Purpose

Model Inspection is onboarding stage two. It receives one currently validated local model selection, will collect lightweight core-runtime evidence in a protected worker, and will classify that evidence into one controlled application outcome before Hardware Fit.

```text
Validated Model Import
        ↓ ModelInspectionRequest
Model Inspection
        ↓
Ready / ReadyWithWarnings /
ConversionRequired / IncompletePackage /
Unsupported / Invalid
        ↓
Hardware Fit only for Ready or ReadyWithWarnings
```

The page remains presentation-only. The protected production worker now completes the five lightweight GGUF stages and is carried in the x64 application/test package at one fixed path, but the page does not yet launch it or display live runtime results.

## Accepted architecture

- [ADR-001 — matched LLamaSharp application runtime](../../../docs/architecture/decisions/ADR-001-llamasharp-application-runtime.md)
- [ADR-002 — core inspection versus backend verification](../../../docs/architecture/decisions/ADR-002-core-inspection-versus-backend-verification.md)
- [ADR-003 — protected Model Inspection worker](../../../docs/architecture/decisions/ADR-003-protected-model-inspection-worker.md)

Production direction:

```text
ModelInspectionPage
        ↓
ModelInspectionViewModel
        ↓
IModelInspectionService
        ↓
ModelInspectionService
        ├── ModelInspectionClassifier
        └── ILlamaModelProbe
                ↓
        WorkerProcessLlamaModelProbe
                ↓ bounded JSON lines over redirected standard streams
        GraniteEdgeAI.ModelInspection.Worker.exe
                ↓
        LLamaSharp 0.27.0 / matched llama.cpp CPU runtime
```

The worker will return technical evidence. Application code—not the worker—will own final user-facing classification.

Chat remains a separate later route through a pinned `llama-cli.exe`; it does not share the inspection worker.

## Current capability status

| Capability | Status |
|---|---|
| Receive immutable `ModelInspectionRequest` | Implemented |
| Retain exact request as the page source of truth | Implemented |
| Preserve request object across onboarding navigation | Implemented |
| Initial selected-model card | Implemented |
| Approved five-stage tracker | Implemented |
| Reusable model/content/outcome/action controls | Implemented |
| Application request/progress/evidence/result contracts | Implemented and tested |
| Shared versioned worker protocol contracts | Implemented and tested |
| Strict bounded JSON and protocol sequence validation | Implemented and tested |
| Matched LLamaSharp CPU feasibility | Verified in isolated tools |
| Production worker executable boundary | Implemented and tested through real CPU/VocabOnly completion |
| Process adapter and bounded stream host | Implemented and tested |
| Production LLamaSharp inspection engine | Implemented and tested for lightweight GGUF CPU/VocabOnly inspection |
| Fixed x64 worker/native application closure | Implemented for build output and packaged test layout; release MSIX attestation remains pending |
| Installed/unpackaged approved-root composition | Implemented; fixed path only, no current-directory or `PATH` fallback |
| Worker-to-application mappers | Not implemented |
| Classifier and service | Not implemented |
| `ModelInspectionViewModel` | Not implemented |
| Live progress and functional Cancel | Not implemented |
| Hardware Fit continuation | Not implemented |

## Source hierarchy

```text
Features/ModelInspection/
├── README.md
├── ModelInspectionPage.xaml
├── ModelInspectionPage.xaml.cs
├── Contracts/
│   ├── README.md
│   └── application request/progress/evidence/result contracts
├── Controls/
├── Infrastructure/
│   ├── README.md
│   └── approved-root and fixed WorkerClient composition
├── Models/
└── Presentation/
```

Shared transport contracts remain framework-neutral:

```text
shared/GraniteEdgeAI.ModelInspection.Contracts/
├── Evidence/
└── Protocol/
```

The historical feasibility CLI and its specialist test campaigns remain
outside the WinUI application; production inspection is owned by the protected
worker and extracted runtime:

```text
tools/ModelInspection.LlamaSharpSpike*/
```

## Page responsibility

`ModelInspectionPage` currently owns presentation composition and navigation-data retention only.

```text
OnNavigatedTo
    → require ModelInspectionRequest
    → retain the exact request object as the page source of truth
    → reset initial-presentation guard

Loaded
    → require retained request
    → apply initial model, progress, outcome, and action presentations
```

The page does not:

- open or hash the model;
- reference LLamaSharp or worker protocol records;
- create or manage a process;
- classify findings;
- choose a hardware backend;
- initialise Vulkan or TurboQuant;
- retain native handles.

## Initial presentation

```text
Outcome card
    → Hidden

Model card
    → Compact
    → Model selected
    → request filename and validated quick-scan format only
    → no invented compatibility claim

Content card
    → Progress
    → 0 of 5 checks complete
    → stage 1 active

Action card
    → Inspecting layout
    → Cancel visible but disabled until a cancellable service exists
```

## Five user-visible stages

```text
1. Check model package
2. Read model configuration
3. Validate tokenizer and chat setup
4. Validate model structure
5. Confirm core runtime compatibility
```

These are Model Inspection workflow stages. They do not prove Vulkan, GPU offload, context allocation, TurboQuant, or inference.

## Application contracts

The application domain owns:

- `ModelInspectionRequest` and expected file identity;
- validated quick-scan snapshot;
- progress and five-stage state;
- path-minimised technical evidence;
- findings and final outcomes;
- operational failure separation;
- Hardware Fit continuation rules.

Only `Ready` and `ReadyWithWarnings` can continue. `ConversionRequired` requires an implemented and verified conversion-route identifier; unsupported models are not automatically described as convertible.

See [application contracts](./Contracts/README.md).

## Shared worker protocol

Protocol Gate 1 defines:

```text
Protocol version:               1
Worker ID:                      GraniteEdgeAI.ModelInspection.Worker
Runtime profile:                llamasharp-0.27.0-cpu-win-x64-vocab-only-v1
Maximum message:                1 MiB UTF-8
Maximum retained stderr:        256 KiB UTF-8
Startup timeout contract:       5 seconds
Overall timeout contract:       5 minutes
Cancellation grace contract:    5 seconds
```

The protocol has bounded commands, messages, evidence records, strict JSON parsing, duplicate-property rejection, and deterministic command/message sequence validators. It does not itself launch a process or inspect a model.

## Runtime identities

```text
Protected application worker runtime
    LLamaSharp 0.27.0
    LLamaSharp.Backend.Cpu 0.27.0
    llama.cpp 3f7c29d318e317b63f54c558bc69803963d7d88c
    win-x64

Separate upstream research runtime
    llama.cpp b9870
    2d973636e292ee6f75fadcf08d29cb33511f509f
```

These are separate evidence tracks and must not be represented as the same native build.

## Verified feasibility evidence

Isolated LLamaSharp work verified:

- CPU native-library selection;
- controlled Granite `VocabOnly` metadata inspection;
- tokenizer smoke and embedded chat-template presence;
- cancellation and disposal evidence;
- malformed/hostile GGUF containment in child processes;
- model integrity and evidence privacy checks.

Evidence:

- [Tier 1 verification](../../../docs/testing/evidence/2026-08-04-llamasharp-tier1-verification.md)
- [Tier 2 trusted verification](../../../docs/testing/evidence/2026-08-05-llamasharp-tier2-local-verification.md)
- [Coverage matrix](../../../docs/testing/LLamaSharp-Runtime-Test-Coverage-Matrix.md)

Production evidence now also proves the published worker reaches `Completed` through all five stages against the controlled zero-tensor N-001 tokenizer fixture, and that the x64 application/test layout contains the exact 44-file CPU closure plus a detached SHA-256 manifest. The same manifest is embedded in the application, and every execution verifies the detached bytes and all staged path/length/hash entries before the process client can launch. The installed-package route anchors that snapshot with Windows package immutability; the unpackaged AppContext route is development/test-only and does not claim protection from a concurrent writer. This does not claim trusted-Granite inference, quality, performance, or final release approval.

## Operational states versus model outcomes

Execution state and model classification remain distinct:

```text
Completed
    → reliable evidence exists
    → application classifier produces one model outcome

Cancelled
    → only cooperative worker completion may produce cancellation

OperationalFailure
    → reliable classification evidence does not exist
    → no model outcome is invented
```

Examples:

```text
Missing native DLL      ≠ Invalid model
File permission failure ≠ Unsupported model
Forced worker kill      ≠ Successful cancellation
Worker crash            ≠ Corrupt GGUF
```

## Next production gate

The protected worker, real lightweight LLamaSharp engine, fixed x64 child path, prelaunch-verified deterministic CPU dependency manifest, and application-root selection are implemented. The remaining release-packaging work is an extracted unsigned MSIX closure/notice attestation; it does not block beginning the application adapter because the packaged test layout already proves the fixed files are present, isolated, and checked before execution.

The next user-journey gate is worker-to-application mapping, deterministic classification, and service orchestration. The following gate then connects that service to the page/ViewModel for automatic start, live progress, Cancel, retry, and outcome actions without loading LLamaSharp in the WinUI process.

## Non-claims

- no LLamaSharp, worker-host, or native-library reference in the WinUI process; the app references only WorkerClient on x64;
- no worker evidence extraction from a real model through the application;
- no classifier, service, ViewModel, live progress, or working Cancel action;
- no full tensor load, context, KV cache, or generation;
- no OpenVINO inspection;
- no Vulkan or TurboQuant result;
- no Hardware Fit handoff.

## Related documentation

- [Application feature architecture](../README.md)
- [Model Import architecture](../ModelImport/README.md)
- [Onboarding architecture](../Onboarding/README.md)
- [Application contracts](./Contracts/README.md)
- [Inspection controls](./Controls/README.md)
- [Infrastructure composition](./Infrastructure/README.md)
- [Presentation models](./Models/README.md)
- [Presentation construction](./Presentation/README.md)
- [Shared worker contracts](../../../shared/GraniteEdgeAI.ModelInspection.Contracts/README.md)
