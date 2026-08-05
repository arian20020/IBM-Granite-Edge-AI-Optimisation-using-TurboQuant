# Application feature architecture

**Status:** Model Import and Model Inspection Gate 1 boundaries implemented; production worker integration remains pending  
**Last reviewed:** 2026-08-05  
**Current branch:** `feature/model-inspection-runtime-integration`

## Purpose

Application source is organised by user-facing feature rather than technical file type. Each feature owns one understandable part of the onboarding journey, while the onboarding shell owns cross-stage navigation.

Framework-neutral worker protocol contracts live under `shared/`. Native feasibility tools remain outside the application project. This keeps WinUI, process transport, runtime code, and experimental evidence from collapsing into one tightly coupled component.

## Source hierarchy

```text
Features/
├── README.md
├── ModelImport/
│   ├── README.md
│   ├── ModelInspectionRequestFactory.cs
│   ├── Controls/
│   ├── FileImport/
│   ├── ModelDownload/
│   └── QuickScan/
├── Onboarding/
│   ├── README.md
│   └── Controls/
└── ModelInspection/
    ├── README.md
    ├── Contracts/
    ├── Controls/
    ├── Models/
    └── Presentation/
```

Separate boundaries:

```text
shared/
└── GraniteEdgeAI.ModelInspection.Contracts/
    └── versioned worker transport/evidence language

tools/
└── ModelInspection.LlamaSharpSpike*/
    └── isolated runtime feasibility and trusted tests
```

## Onboarding journey

```text
1. Choose model
       ↓
2. Inspect model
       ↓
3. Check hardware fit
       ↓
4. Configure model
       ↓
5. Ready to chat
```

## Current stage status

| Stage | Current implementation |
|---|---|
| Model Import | Local GGUF selection, bounded quick scan, changed-file rejection, and immutable inspection request implemented |
| Model Inspection | Request navigation, application contracts, reusable UI shell, and worker protocol contracts implemented; runtime execution not connected |
| Hardware Fit | Not implemented |
| Configure Model | Not implemented |
| Ready to Chat | Not implemented |

## Cross-feature ownership

```text
ModelImportPage
    → selects and quick-scans a local GGUF
    → guards current validated state
    → creates ModelInspectionRequest
    → reports navigation intent

OnboardingShellPage
    → owns StageFrame and CurrentStage
    → forwards the same immutable request
    → synchronises OnboardingStageIndicator

ModelInspectionPage
    → retains ModelInspectionRequest
    → derives SelectedModelPath from the request
    → owns initial presentation composition
    → does not call LLamaSharp or worker protocol types
```

## Model Import to Model Inspection handoff

```text
Successful bounded quick scan
    ↓
user selects Continue
    ↓
ModelInspectionRequestFactory
    → canonicalises path
    → excludes concurrent writers during identity capture
    → verifies .gguf and current length
    → captures length and UTC last-write time
    → copies quick-scan facts
    ↓
ModelInspectionRequestedEventArgs.Request
    ↓ same object
OnboardingShellPage.NavigateToModelInspection(Request)
    ↓ same object
ModelInspectionPage.Request
```

A deleted, resized, inaccessible, non-GGUF, or write-locked selection is rejected before navigation. Model Import clears validated state and shows the stable `model-selection-changed` failure.

## Model Inspection boundaries

### Application domain

`Features/ModelInspection/Contracts/` defines immutable application-owned request, progress, evidence, finding, result, and execution types.

### Worker transport domain

`shared/GraniteEdgeAI.ModelInspection.Contracts/` defines protocol version 1, bounded JSON commands/messages, evidence records, and sequence validators.

Only future `WorkerRequestMapper` and `WorkerResultMapper` may translate between these domains. Pages and controls must not depend directly on worker records.

### Runtime domain

The selected production direction is a dedicated short-lived x64 worker using LLamaSharp 0.27.0 with its matched CPU backend. The worker returns evidence; application code classifies outcomes. Chat later uses a separate pinned `llama-cli` process.

See [ADR-003](../../docs/architecture/decisions/ADR-003-protected-model-inspection-worker.md).

## Model Inspection stages

```text
1. Check model package
2. Read model configuration
3. Validate tokenizer and chat setup
4. Validate model structure
5. Confirm core runtime compatibility
```

These rows do not claim that Vulkan, GPU offload, context allocation, TurboQuant, or inference has passed.

## Outcome model

```text
Ready
ReadyWithWarnings
ConversionRequired
IncompletePackage
Unsupported
Invalid
```

Only `Ready` and `ReadyWithWarnings` can continue to Hardware Fit. `ConversionRequired` is permitted only where an actual verified conversion route exists.

Operational states remain separate:

```text
Completed
Cancelled
OperationalFailure
```

Infrastructure failures are never relabelled as model outcomes.

## Runtime identities

```text
Application feasibility runtime
    LLamaSharp 0.27.0
    LLamaSharp.Backend.Cpu 0.27.0
    llama.cpp 3f7c29d318e317b63f54c558bc69803963d7d88c

Separate upstream research runtime
    llama.cpp b9870
    2d973636e292ee6f75fadcf08d29cb33511f509f
```

The WinUI application project still has no LLamaSharp, Vulkan, or TurboQuant package reference.

## Gate status

```text
1. Matched LLamaSharp CPU feasibility                  verified in isolated tools
2. Shared protocol and application contracts           implemented and tested
3. Immutable Model Import navigation handoff           implemented and tested
4. Protected worker executable/process adapter         next gate
5. LLamaSharp worker evidence extraction               later
6. Mappers, classifier, service, and ViewModel          later
7. Dynamic WinUI results and functional Cancel          later
8. Hardware Fit / Vulkan / TurboQuant                   separate later gates
```

## Documentation map

- [Model Import architecture](./ModelImport/README.md)
- [Onboarding architecture](./Onboarding/README.md)
- [Model Inspection architecture](./ModelInspection/README.md)
- [Model Inspection application contracts](./ModelInspection/Contracts/README.md)
- [Shared worker contracts](../../shared/GraniteEdgeAI.ModelInspection.Contracts/README.md)
- [LLamaSharp feasibility tool](../../tools/ModelInspection.LlamaSharpSpike/README.md)

## Source-of-truth order

```text
1. Source code and executable tests
2. Nearest README beside the source
3. Parent feature README
4. Accepted ADRs
5. Recorded build/runtime evidence
6. Historical plans and pull-request descriptions
```

## Current non-claims

The repository does not yet prove or implement:

- production worker executable resolution, launch, or stream handling;
- LLamaSharp evidence extraction through the application;
- worker/application mappers;
- model classifier, service, ViewModel, or live page progress;
- functional cancellation from WinUI;
- full model load, context, KV cache, or generation;
- OpenVINO inspection;
- Vulkan or TurboQuant verification;
- Hardware Fit, configuration, or completed chat.
