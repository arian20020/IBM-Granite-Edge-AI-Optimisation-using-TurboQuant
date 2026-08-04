# Application feature architecture

**Status:** Living current-state documentation  
**Last reviewed:** 2026-08-04  
**Current branch:** `feature/model-inspection`  
**Reviewed application baseline:** `2c51bb0551cb5556e422e63c19888c1f3874d0e5`

## Purpose

Application source is organised by user-facing feature rather than by technical
file type. Each feature owns one understandable part of the journey, while the
onboarding shell owns cross-stage navigation.

Experimental native work remains outside the application source tree until its
compatibility and safety gates pass.

## Documented source hierarchy

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

Feasibility-only source is separate:

```text
tools/
├── README.md
├── ModelInspection.LlamaSharpSpike/
│   ├── README.md
│   └── ModelProbe/README.md
└── ModelInspection.LlamaSharpSpike.Tests/
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
| Model Import | Local GGUF selection, bounded quick scan, controlled card states and validated navigation request implemented |
| Model Inspection | Navigation, page shell, four reusable controls and initial five-stage core-runtime presentation implemented |
| Hardware Fit | Not implemented |
| Configure Model | Not implemented |
| Ready to Chat | Not implemented |

A separate LLamaSharp feasibility tool now includes the matched CPU backend
smoke and the source for a controlled `VocabOnly` model probe. This is not yet
production Model Inspection.

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
    → owns page composition
    → does not call LLamaSharp

Presentation factories
    → construct UI state
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
    → revalidates through TryRequestModelInspection()
    → raises ModelInspectionRequested

OnboardingShellPage
    → receives the request
    → StageFrame.Navigate(ModelInspectionPage, modelPath)
    → CurrentStage = InspectModel
    → StageIndicator.CurrentStage = InspectModel

ModelInspectionPage
    → receives the path in OnNavigatedTo
    → waits for Loaded
    → applies initial model, progress, outcome and action presentations
```

The current navigation parameter is only the path. A project-owned
`ModelInspectionRequest` carrying format and validated quick-scan information
remains planned.

## Model Inspection progress meaning

[ADR-002](../../docs/architecture/decisions/ADR-002-core-inspection-versus-backend-verification.md)
separates model-package/core-runtime inspection from later hardware/backend
verification.

The five visible rows remain:

```text
1. Check model package
2. Read model configuration
3. Validate tokenizer and chat setup
4. Validate model structure
5. Confirm core runtime compatibility
```

They do not claim that Vulkan, GPU offloading, context allocation, TurboQuant
or inference has passed.

## Research runtime and application runtime

[ADR-001](../../docs/architecture/decisions/ADR-001-llamasharp-application-runtime.md)
records two separate evidence tracks:

```text
Research runtime
    llama.cpp b9870
    2d973636e292ee6f75fadcf08d29cb33511f509f

Application feasibility runtime
    LLamaSharp 0.27.0
    LLamaSharp.Backend.Cpu 0.27.0
    llama.cpp 3f7c29d318e317b63f54c558bc69803963d7d88c
```

The upstream campaign remains valid research, but it is not the embedded
LLamaSharp runtime.

## Current LLamaSharp feasibility gates

### Gate 1 — CPU native-library smoke

```text
LLamaSharp configuration
    → CUDA disabled
    → Vulkan disabled
    → CPU fallback enabled
    → NativeLibraryConfig.LLama.DryRun
    → runtime identity and logs written to JSON
```

No model is involved.

### Gate 2 — CPU lightweight `VocabOnly` model probe

```text
controlled local GGUF
    ↓
output/model collision validation
    ↓
read-only SHA-256 snapshot before
    ↓
matched CPU backend dry run
    ↓
LLamaWeights.LoadFromFileAsync
    VocabOnly = true
    GpuLayerCount = 0
    ↓
metadata, vocabulary, tokenizer and chat-template evidence
    ↓
deterministic native disposal
    ↓
read-only SHA-256 snapshot after
    ↓
file-integrity comparison
    ↓
project-owned JSON evidence
```

The probe also records genuine native progress, cancellation, runtime identity,
memory observations and controlled operational failures. Full local model paths
and full chat-template text are not serialized.

This gate does not create a context, allocate a KV cache, run inference, use
Vulkan or activate TurboQuant.

## Engineering validation ladder

The development gates are separate from the five user-visible inspection rows:

```text
1. Matched LLamaSharp CPU native-library smoke
        ↓
2. CPU lightweight Granite VocabOnly probe
        ↓
3. Ordinary Vulkan baseline
        ↓
4. TurboQuant fork CPU correctness
        ↓
5. TurboQuant fork Vulkan acceleration
        ↓
6. LLamaSharp/custom TurboQuant backend compatibility
        ↓
7. Production inspection and inference integration
```

This order allows the first divergent boundary to be identified accurately:
managed/native loading, model recognition, ordinary Vulkan/driver behavior,
TurboQuant fork correctness, TurboQuant Vulkan kernels, or managed/custom-DLL
compatibility.

## Current dependency direction

```text
OnboardingShellPage
    ├── ModelImportPage
    └── ModelInspectionPage

ModelImportPage
    ├── import controls
    ├── picker routes
    └── quick-scan contracts

ModelInspectionPage
    ├── inspection controls
    ├── presentation data models
    └── presentation factories

Isolated LLamaSharp tool
    ├── LLamaSharp 0.27.0
    └── LLamaSharp.Backend.Cpu 0.27.0
```

The WinUI application project still has no LLamaSharp, Vulkan or TurboQuant
package reference.

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
- [VocabOnly probe design](../../docs/superpowers/specs/2026-08-04-llamasharp-vocab-only-model-probe-design.md)
- [VocabOnly probe implementation plan](../../docs/superpowers/plans/2026-08-04-llamasharp-vocab-only-model-probe.md)

## Source-of-truth order

```text
1. Source code and executable tests
2. Nearest README beside the source
3. Parent feature README
4. Accepted ADRs
5. Detailed development evidence
6. Historical plans and pull-request descriptions
```

## Current non-claims

The repository does not yet prove:

- that the LLamaSharp CPU smoke passes on the target laptop;
- that a controlled Granite GGUF passes the VocabOnly probe;
- that `VocabOnly` exposes all evidence needed by production Model Inspection;
- that the WinUI page runs native inspection;
- dynamic stage movement or functional page cancellation;
- final model outcome classification;
- OpenVINO inspection;
- Vulkan initialisation or GPU layer offload;
- TurboQuant CPU or Vulkan correctness;
- Hardware Fit, configuration or completed chat.

Those claims require fresh tests and runtime evidence, not source presence alone.

## Documentation update triggers

Review the nearest README, parent README and relevant ADR when any of these
change:

- file/folder responsibility;
- navigation contract;
- visible inspection stages;
- runtime package/version/commit;
- model-probe depth;
- cancellation or integrity behavior;
- failure classification;
- backend selection;
- Vulkan or TurboQuant verification;
- implemented versus deferred boundary;
- tests or evidence supporting a claim.
