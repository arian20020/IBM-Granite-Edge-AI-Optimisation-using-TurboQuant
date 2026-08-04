# Application feature architecture

**Status:** Living current-state documentation  
**Last reviewed:** 2026-08-05  
**Current branch:** `feature/model-inspection`

## Purpose

Application source is organised by user-facing feature rather than by technical
file type. Each feature owns one understandable part of the journey, while the
onboarding shell owns cross-stage navigation.

Experimental/native code remains outside the application source tree until its
compatibility, containment and privacy gates pass and a production application
boundary has been designed.

## Source hierarchy

```text
Features/
├── README.md
├── ModelImport/
│   ├── README.md
│   ├── Controls/README.md
│   ├── FileImport/README.md
│   ├── FileImport/PickerRoute/README.md
│   ├── ModelDownload/README.md
│   └── QuickScan/README.md
├── Onboarding/
│   ├── README.md
│   └── Controls/README.md
└── ModelInspection/
    ├── README.md
    ├── Controls/README.md
    ├── Models/README.md
    └── Presentation/README.md
```

Runtime feasibility and test infrastructure remain separate:

```text
tools/
├── ModelInspection.LlamaSharpSpike/
├── ModelInspection.LlamaSharpSpike.Tests/
├── ModelInspection.LlamaSharpSpike.TestSupport/
├── ModelInspection.LlamaSharpSpike.NativeIntegrationTests/
└── ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/
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
| Model Import | Local GGUF selection, bounded quick scan, controlled card states and validated navigation implemented |
| Model Inspection | Navigation, page shell, four reusable controls and initial five-stage presentation implemented; runtime not connected to the page |
| Hardware Fit | Not implemented |
| Configure Model | Not implemented |
| Ready to Chat | Not implemented |

The separate LLamaSharp feasibility boundary is now verified for the selected
CPU runtime and controlled Granite model. This proves the infrastructure needed
to design production Model Inspection; it does not make the WinUI page
functional yet.

## Cross-feature ownership

```text
OnboardingShellPage
├── owns StageFrame
├── owns CurrentStage
├── synchronises OnboardingStageIndicator
├── hosts ModelImportPage
└── hosts ModelInspectionPage
```

Important separation:

```text
ModelImportPage
    → reports navigation intent
    → does not manipulate StageFrame

OnboardingShellPage
    → owns cross-stage navigation and indicator state

ModelInspectionPage
    → owns presentation composition
    → does not call LLamaSharp

Presentation factories
    → construct approved UI states
    → do not run native work

Runtime feasibility tool
    → calls LLamaSharp in isolation
    → is not referenced by the WinUI application
```

## Model Import to Model Inspection handoff

```text
ModelImportPage
    → selects and quick-scans a local GGUF
    → stores SelectedModelPath and ValidatedScanResult
    → enables Continue only after success
    → raises ModelInspectionRequested

OnboardingShellPage
    → receives the request
    → navigates StageFrame to ModelInspectionPage
    → updates CurrentStage and the stage indicator

ModelInspectionPage
    → receives the path
    → waits for Loaded
    → applies initial model, progress, outcome and action presentations
```

The current navigation parameter is only the path. The next application slice
will replace this with a project-owned `ModelInspectionRequest` carrying the
validated quick-scan handoff and file identity.

## Model Inspection progress meaning

[ADR-002](../../docs/architecture/decisions/ADR-002-core-inspection-versus-backend-verification.md)
separates lightweight model/core-runtime inspection from later
hardware/backend verification.

The five visible rows remain:

```text
1. Check model package
2. Read model configuration
3. Validate tokenizer and chat setup
4. Validate model structure
5. Confirm core runtime compatibility
```

They do not claim that Vulkan, GPU offload, context allocation, TurboQuant or
inference has passed.

## Runtime identities

[ADR-001](../../docs/architecture/decisions/ADR-001-llamasharp-application-runtime.md)
records separate research and application evidence tracks:

```text
Research runtime
    llama.cpp b9870
    2d973636e292ee6f75fadcf08d29cb33511f509f

Application feasibility runtime
    LLamaSharp 0.27.0
    LLamaSharp.Backend.Cpu 0.27.0
    llama.cpp 3f7c29d318e317b63f54c558bc69803963d7d88c
```

The upstream campaign remains valid research, but it is not the LLamaSharp
runtime used by Model Inspection feasibility work.

## Verified LLamaSharp gates

### Gate 1 — CPU native-library smoke

```text
Result:                    PASS
Process architecture:      X64
Selected library:          LLama / AVX-512
CUDA:                      false
Vulkan:                    false
```

### Gate 2 — lightweight Granite `VocabOnly` probe

```text
Result:                    PASS
Model:                     granite-4.1-3b-Q4_K_M.gguf
Model SHA-256 preserved:   yes
Architecture:              granite
Declared context:          131,072
Layers / heads / KV heads: 40 / 40 / 8
Vocabulary:                100,352
Tokenizer smoke:           passed
Chat template:             present
Native handle closed:      true
```

### Expanded verification

```text
Tier 1 deterministic:      170 / 170 passed
Tier 1 contained native:   4 / 4 passed
Tier 2 trusted local:      20 / 20 passed
Tier 2 evidence files:     56 scanned
Tier 2 privacy findings:   0
```

Evidence:

- [Tier 1 verification](../../docs/testing/evidence/2026-08-04-llamasharp-tier1-verification.md)
- [Tier 2 verification](../../docs/testing/evidence/2026-08-05-llamasharp-tier2-local-verification.md)
- [Coverage matrix](../../docs/testing/LLamaSharp-Runtime-Test-Coverage-Matrix.md)

## Engineering validation ladder

The development gates remain separate from the five user-visible rows:

```text
1. Matched LLamaSharp CPU native-library smoke          verified
2. CPU lightweight Granite VocabOnly probe              verified
3. Production inspection adapter and WinUI integration  next
4. Full CPU runtime/load/inference verification          later
5. Ordinary Vulkan baseline                              later
6. TurboQuant fork CPU correctness                       later
7. TurboQuant fork Vulkan acceleration                   later
8. LLamaSharp/custom backend compatibility               later
```

This order keeps failures attributable to the first divergent boundary rather
than mixing managed/native loading, model recognition, UI state, Vulkan,
TurboQuant and inference into one opaque failure.

## Next production dependency direction

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
    protected local worker process
            ↓
    LLamaSharp / llama.cpp
```

The protected worker-process route is strongly preferred because feasibility
work observed a real native abort that bypassed managed exception handling. A
formal ADR/design approval is still required before implementation.

The WinUI project continues to have no LLamaSharp, Vulkan or TurboQuant package
reference.

## Documentation map

### Model Import

- [Model Import architecture](./ModelImport/README.md)
- [Imported-model controls](./ModelImport/Controls/README.md)
- [File-import boundary](./ModelImport/FileImport/README.md)
- [Native picker routes](./ModelImport/FileImport/PickerRoute/README.md)
- [Recommended-model prototype](./ModelImport/ModelDownload/README.md)
- [Quick-scan architecture](./ModelImport/QuickScan/README.md)

### Onboarding

- [Onboarding architecture](./Onboarding/README.md)
- [Stage-indicator control](./Onboarding/Controls/README.md)

### Model Inspection

- [Model Inspection architecture](./ModelInspection/README.md)
- [Inspection controls](./ModelInspection/Controls/README.md)
- [Inspection presentation models](./ModelInspection/Models/README.md)
- [Inspection presentation construction](./ModelInspection/Presentation/README.md)

### Runtime feasibility

- [Engineering tools](../../tools/README.md)
- [LLamaSharp feasibility tool](../../tools/ModelInspection.LlamaSharpSpike/README.md)
- [VocabOnly model probe](../../tools/ModelInspection.LlamaSharpSpike/ModelProbe/README.md)
- [Trusted tests](../../tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/README.md)
- [Trusted execution runbook](../../docs/testing/runbooks/LLamaSharp-Trusted-Real-Model-Runbook.md)

## Source-of-truth order

```text
1. Source code and executable tests
2. Nearest README beside the source
3. Parent feature README
4. Accepted ADRs
5. Recorded runtime evidence and coverage matrix
6. Historical plans and pull-request descriptions
```

## Current non-claims

The repository still does not prove or implement:

- a production runtime probe or worker protocol;
- the WinUI page running native inspection;
- dynamic stage movement or functional page cancellation;
- final model-outcome classification;
- full tensor load, context, KV cache or generation;
- OpenVINO inspection;
- Vulkan initialisation or GPU layer offload;
- TurboQuant, PolarQuant or QJL correctness;
- Hardware Fit, configuration or completed chat.

## Documentation update triggers

Review the nearest README, parent README and relevant ADR whenever these
change:

- file/folder responsibility;
- navigation or handoff contract;
- visible inspection stages;
- runtime package/version/commit;
- model-probe depth;
- cancellation, integrity or process-containment behaviour;
- failure classification;
- backend selection;
- Vulkan or TurboQuant verification;
- implemented versus deferred boundary;
- tests or evidence supporting a claim.