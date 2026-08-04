# Application feature architecture

**Status:** Living current-state documentation  
**Last reviewed:** 2026-08-04  
**Reviewed application baseline:** `2c51bb0551cb5556e422e63c19888c1f3874d0e5`  
**Current branch:** `feature/model-inspection`

## Purpose

This folder is organised by **user-facing feature** rather than by technical file type. Each feature owns one understandable part of the application journey, while shared orchestration remains in the onboarding shell.

This README is the entry point for understanding how the implemented features communicate. Documentation continues down to each meaningful nested source folder so a contributor can understand the local architecture without first reading every implementation file.

## Documented source hierarchy

```text
Features/
├── README.md
│
├── ModelImport/
│   ├── README.md
│   ├── Controls/
│   │   └── README.md
│   ├── FileImport/
│   │   ├── README.md
│   │   └── PickerRoute/
│   │       └── README.md
│   ├── ModelDownload/
│   │   └── README.md
│   └── QuickScan/
│       └── README.md
│
├── Onboarding/
│   ├── README.md
│   └── Controls/
│       └── README.md
│
└── ModelInspection/
    ├── README.md
    ├── Controls/
    │   └── README.md
    ├── Models/
    │   └── README.md
    └── Presentation/
        └── README.md
```

The nearest README explains the folder's local responsibility, file inventory, inputs, outputs, tests, limitations, and change hazards. Parent READMEs explain how those folders compose into a larger feature.

Experimental or feasibility-only code is kept outside the application source tree:

```text
tools/
├── README.md
├── ModelInspection.LlamaSharpSpike/
└── ModelInspection.LlamaSharpSpike.Tests/
```

That separation prevents a native dependency from silently becoming part of the WinUI application before its gates pass.

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

### Current implementation status

| Stage | Feature | Current status |
|---|---|---|
| 1 | Model Import | Implemented for local GGUF selection and bounded quick scan |
| 2 | Model Inspection | Navigation, page composition, reusable controls and initial core-runtime progress presentation implemented; real model inspection is not implemented |
| 3 | Hardware Fit | Not implemented |
| 4 | Configure Model | Not implemented |
| 5 | Ready to Chat | Not implemented |

The Model Inspection initial-screen changes still require a fresh Windows cold build, packaged test run and manual screen verification.

A separate LLamaSharp feasibility project contains the selected CPU-native-backend dry-run source. That is an integration preparation step, not evidence that the page performs runtime inspection.

## Cross-feature ownership

```text
OnboardingShellPage
│
├── owns StageFrame
├── owns CurrentStage
├── owns OnboardingStageIndicator synchronization
│
├── hosts ModelImportPage
└── hosts ModelInspectionPage
```

The shell owns the journey. Individual stage pages own their internal workflow and report user intent through explicit contracts.

### Important separation

```text
ModelImportPage
    does not navigate StageFrame directly

ModelInspectionPage
    does not know how Model Import selected the file

Reusable cards
    do not know about the onboarding shell

Presentation factories
    construct UI state but do not run native code

Picker and future runtime adapters
    do not own visible page state

Feasibility console tools
    are not referenced by the WinUI application
```

This prevents a page or control from reaching upward through the visual tree to manipulate unrelated application state, and prevents experimental native code from leaking into application contracts.

## Implemented Model Import to Model Inspection handoff

```text
ModelImportPage
    → selects and quick-scans a local GGUF file
    → stores SelectedModelPath and ValidatedScanResult
    → enables Continue only after quick-scan success
    → revalidates the state in TryRequestModelInspection()
    → raises ModelInspectionRequested

OnboardingShellPage
    → receives ModelInspectionRequested
    → navigates StageFrame to ModelInspectionPage
    → updates CurrentStage to InspectModel
    → updates the persistent stage indicator

ModelInspectionPage
    → receives the selected path in OnNavigatedTo
    → waits for Loaded before changing child controls
    → applies the initial model, progress, outcome and action presentations
```

The current navigation contract carries the selected model path. Passing the selected format and complete validated quick-scan result is a planned improvement, not current behavior.

## Model Inspection progress meaning

[ADR-002](../../docs/architecture/decisions/ADR-002-core-inspection-versus-backend-verification.md) establishes that Model Inspection checks the model against the **core application runtime** before Hardware Fit.

The five visible rows are:

```text
1. Check model package
2. Read model configuration
3. Validate tokenizer and chat setup
4. Validate model structure
5. Confirm core runtime compatibility
```

The last row does not mean that Vulkan, GPU offloading, Hardware Fit, context allocation, TurboQuant or inference has passed.

The initial progress data is now constructed by:

```text
InitialInspectionProgressPresentationFactory
```

rather than being embedded directly inside the page's code-behind.

## Research runtime versus application runtime

[ADR-001](../../docs/architecture/decisions/ADR-001-llamasharp-application-runtime.md) establishes two separate evidence tracks:

```text
Standalone research runtime
    llama.cpp tag b9870
    commit 2d973636e292ee6f75fadcf08d29cb33511f509f
    → retained research and benchmark evidence

Selected application runtime pair
    LLamaSharp 0.27.0
    LLamaSharp.Backend.Cpu 0.27.0
    mapped llama.cpp commit
    3f7c29d318e317b63f54c558bc69803963d7d88c
    → application feasibility and later production evidence
```

The two tracks must not be described as the same runtime. The selected application pair is intended for both Model Inspection and later in-application GGUF inference so that inspection and execution do not silently disagree.

### Current feasibility boundary

The first isolated tool slice:

- pins the exact managed and native package versions;
- disables CUDA and Vulkan selection;
- uses `NativeLibraryConfig.LLama.DryRun` to test CPU backend discovery;
- writes project-owned JSON evidence;
- does not accept a model path;
- does not load a GGUF model;
- does not add LLamaSharp to the WinUI project.

The next runtime slice remains blocked until fresh Windows smoke evidence is reviewed.

## Backend verification ladder

Development validation proceeds in separate gates:

```text
1. Matched LLamaSharp CPU native-library smoke
        ↓
2. CPU lightweight Granite model inspection
        ↓
3. Ordinary Vulkan baseline
        ↓
4. TurboQuant fork CPU correctness baseline
        ↓
5. TurboQuant fork Vulkan acceleration
        ↓
6. LLamaSharp/custom TurboQuant backend compatibility
        ↓
7. Production inspection and inference integration
```

This ladder is not the user-visible five-stage tracker. It exists to isolate managed/native, driver, ordinary Vulkan, fork, kernel and custom-backend failures.

### Why Vulkan and TurboQuant are later

Model Inspection occurs before Hardware Fit. The application must first prove that the core runtime can understand the model without mixing in device drivers, shaders, GPU selection or offload behavior.

After Hardware Fit selects a candidate backend, a later backend-verification flow may prove:

```text
Vulkan initialisation
intended GPU selection
layer offload
context and KV-cache allocation
TurboQuant activation
small inference success
absence of silent CPU fallback
```

No such backend-verification feature is implemented yet.

## Current high-level dependency direction

```text
OnboardingShellPage
    ├── depends on ModelImportPage
    └── depends on ModelInspectionPage

ModelImportPage
    ├── depends on import controls
    ├── depends on picker routes
    └── depends on quick-scan contracts

ModelInspectionPage
    ├── depends on inspection controls
    ├── depends on inspection presentation models
    └── depends on focused presentation factories

Reusable controls
    └── depend on their presentation contracts

Isolated feasibility tool
    └── depends on LLamaSharp and its published CPU backend
```

Dependencies do not flow back from cards into pages, from the inspection page into Model Import, or from Model Import into the shell implementation. The application does not reference the feasibility console project.

## Feature and nested documentation

### Model Import

- [Model Import architecture](./ModelImport/README.md)
- [Imported-model controls](./ModelImport/Controls/README.md)
- [File-import boundary](./ModelImport/FileImport/README.md)
- [Native picker routes](./ModelImport/FileImport/PickerRoute/README.md)
- [Recommended-model download prototype](./ModelImport/ModelDownload/README.md)
- [Quick-scan architecture](./ModelImport/QuickScan/README.md)

### Onboarding

- [Onboarding architecture](./Onboarding/README.md)
- [Onboarding stage-indicator control](./Onboarding/Controls/README.md)

### Model Inspection

- [Model Inspection architecture](./ModelInspection/README.md)
- [Inspection controls](./ModelInspection/Controls/README.md)
- [Inspection presentation models](./ModelInspection/Models/README.md)
- [Inspection presentation construction](./ModelInspection/Presentation/README.md)

### Runtime feasibility and decisions

- [Engineering tools overview](../../tools/README.md)
- [LLamaSharp feasibility spike](../../tools/ModelInspection.LlamaSharpSpike/README.md)
- [ADR-001: matched application runtime](../../docs/architecture/decisions/ADR-001-llamasharp-application-runtime.md)
- [ADR-002: core inspection versus backend verification](../../docs/architecture/decisions/ADR-002-core-inspection-versus-backend-verification.md)
- [LLamaSharp spike design](../../docs/superpowers/specs/2026-08-04-llamasharp-feasibility-spike-design.md)
- [Core progress/backend-gates design](../../docs/superpowers/specs/2026-08-04-core-runtime-progress-and-backend-gates-design.md)

Detailed implementation evidence remains under `docs/`, including:

- [Model Import and GGUF quick scan current state](../../docs/development/Model-Import-Quick-Scan-Current-State.md)
- [GGUF quick-scanner beginner guide](../../docs/development/GGUF-Quick-Scanner-Beginner-Guide.md)

## Source-of-truth hierarchy

When documents and implementation disagree, use this order:

```text
1. Source code and executable tests
2. Nearest README beside the affected code
3. Parent feature README
4. Accepted ADR for significant runtime choices
5. Detailed development evidence under docs/development
6. Historical design documents and pull-request descriptions
```

READMEs document responsibilities and contracts. They should not contain complete copied XAML or C# files because duplicated source can become stale.

## Documentation update rules

Review the nearest README and its parent whenever any of these changes:

- a file is added, moved, or removed;
- folder or feature responsibility changes;
- navigation event or navigation parameter changes;
- visible state or stage wording changes;
- page/control composition changes;
- cancellation or stale-result behavior changes;
- picker, parser, error or outcome classification changes;
- runtime package, native backend or adapter boundary changes;
- tests proving the architecture change;
- implemented, deferred or non-claim boundary changes.

A runtime-version change requires an ADR review because the managed wrapper, native backend package and mapped llama.cpp revision form one compatibility decision.

Moving a backend-specific check into Model Inspection also requires ADR-002 review.

## Current non-claims

The feature architecture currently does **not** provide:

- a LLamaSharp reference inside the WinUI application;
- a Vulkan or TurboQuant backend package inside the WinUI application;
- real GGUF model inspection through LLamaSharp;
- OpenVINO quick scanning or runtime inspection;
- dynamic Model Inspection progress execution;
- runtime outcome classification;
- functional Model Inspection cancellation;
- a working recommended-model catalog or download service;
- Hardware Fit analysis;
- backend-specific Vulkan/TurboQuant verification;
- model configuration;
- a completed chat experience;
- a verified Windows or target-laptop runtime result until fresh evidence is available.

Those capabilities must be documented as implemented only after source, tests and runtime evidence support the claim.
