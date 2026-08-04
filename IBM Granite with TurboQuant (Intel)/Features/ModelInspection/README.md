# Model Inspection architecture

**Status:** Living current-state documentation  
**Last reviewed:** 2026-08-04  
**Reviewed application baseline:** `2c51bb0551cb5556e422e63c19888c1f3874d0e5`  
**Current branch:** `feature/model-inspection`

[← Application feature architecture](../README.md)

## Purpose

Model Inspection is onboarding stage two. It is intended to inspect the selected model package before Hardware Fit is checked, present progressive evidence, and classify the final model outcome.

The current application implementation establishes the **navigation and UI architecture**. A separate feasibility tool pins and dry-runs the selected LLamaSharp CPU backend, but the WinUI feature does not yet call that runtime or inspect a model.

[ADR-002](../../../docs/architecture/decisions/ADR-002-core-inspection-versus-backend-verification.md) defines the meaning of this stage:

> Model Inspection establishes lightweight **core runtime compatibility**. It does not yet prove Vulkan, GPU offload, Hardware Fit, context allocation, TurboQuant activation, or inference.

## Current status

| Capability | Current status |
|---|---|
| Navigate from validated Model Import | Implemented |
| Receive selected model path | Implemented |
| Persistent onboarding indicator moves to Inspect Model | Implemented |
| Compose four reusable inspection controls | Implemented |
| Build initial selected-model presentation | Implemented |
| Build initial five-stage progress presentation | Implemented through a focused factory |
| Protect exact progress wording and backend separation | Test source added; Windows execution pending |
| Build initial action presentation | Implemented |
| Select Progress versus Findings XAML template | Implemented with bootstrap handling |
| Application runtime decision | Accepted in ADR-001 |
| Core inspection versus backend-verification decision | Accepted in ADR-002 |
| Isolated LLamaSharp CPU-backend smoke source | Implemented; Windows verification pending |
| LLamaSharp reference in the WinUI application | Not added |
| Vulkan or TurboQuant runtime package in the WinUI application | Not added |
| Local cold-run verification of the latest screen changes | Required before claiming the screen is fully verified |
| Real GGUF runtime inspection | Not implemented |
| OpenVINO runtime inspection | Not implemented |
| Dynamic stage progression | Not implemented |
| Final outcome classification | Not implemented |
| Functional cancellation | Not implemented |

## Folder structure

```text
ModelInspection/
├── README.md
├── ModelInspectionPage.xaml
├── ModelInspectionPage.xaml.cs
├── Controls/
│   ├── README.md
│   ├── InspectionModelCard.xaml/.cs
│   ├── InspectionContentCard.xaml/.cs
│   ├── InspectionContentTemplateSelector.cs
│   ├── InspectionOutcomeCard.xaml/.cs
│   └── InspectionActionCard.xaml/.cs
├── Models/
│   ├── README.md
│   ├── presentation classes
│   └── mode, status, tone, badge, and outcome enums
└── Presentation/
    ├── README.md
    └── InitialInspectionProgressPresentationFactory.cs
```

The isolated native feasibility code lives outside the application feature:

```text
tools/
├── ModelInspection.LlamaSharpSpike/
└── ModelInspection.LlamaSharpSpike.Tests/
```

Child documentation:

- [Inspection controls](./Controls/README.md)
- [Inspection presentation models](./Models/README.md)
- [Presentation construction](./Presentation/README.md)
- [LLamaSharp feasibility spike](../../../tools/ModelInspection.LlamaSharpSpike/README.md)

## Responsibility boundary

### This feature currently owns

- receipt of a selected model path from onboarding navigation;
- Model Inspection page lifecycle;
- layout and composition of four reusable cards;
- UI presentation contracts for progress and result states;
- construction of the approved initial five-stage core-inspection presentation;
- card visibility, visual-state, and template-selection wiring;
- tests around navigation, template selection and initial progress semantics.

### This feature does not currently own

- native llama.cpp or OpenVINO calls from the application;
- model parsing beyond the earlier quick scan;
- full tensor validation;
- tokenizer or chat-template validation;
- real core-runtime evidence;
- Vulkan device selection or layer offloading;
- TurboQuant CPU or Vulkan execution;
- result classification rules;
- cancellation-token ownership;
- Hardware Fit navigation or calculation.

The native smoke tool is supporting feasibility evidence, not a production dependency of this page.

## Page composition

`ModelInspectionPage.xaml` owns the complete screen arrangement:

```text
Model inspection heading
Common explanatory subtitle

InspectionOutcomeCard
InspectionModelCard
InspectionContentCard
InspectionActionCard
```

The page uses a vertically scrolling, centered `StackPanel` with an 840-effective-pixel maximum width. The page owns spacing between controls; each reusable control owns only its internal design.

Source:

- [`ModelInspectionPage.xaml`](./ModelInspectionPage.xaml)

## Page lifecycle

### Constructor

```text
InitializeComponent()
    ↓
subscribe to Loaded
```

### `OnNavigatedTo`

Receives and validates the navigation parameter:

```text
NavigationEventArgs.Parameter
    ↓
non-empty string modelPath required
    ↓
SelectedModelPath = modelPath
    ↓
mark initial presentation as not yet applied
```

It does not change child-control presentations because the visual tree and compiled bindings may not be fully loaded yet.

### `Loaded`

```text
if initial presentation already applied
    → return

require SelectedModelPath
    ↓
set one-time guard
    ↓
ShowInitialInspectionState(modelPath)
```

This separates navigation-data receipt from visual-tree manipulation.

Source:

- [`ModelInspectionPage.xaml.cs`](./ModelInspectionPage.xaml.cs)

## Initial presentation flow

```text
ShowInitialInspectionState(modelPath)
    │
    ├── OutcomeCard.Presentation = Hidden
    │
    ├── ModelCard.Presentation
    │       → Compact
    │       → ModelSelected badge
    │       → filename derived from path
    │       → format inferred from extension
    │
    ├── ContentCard.Presentation
    │       → InitialInspectionProgressPresentationFactory.Create()
    │       → Progress mode
    │       → 0 of 5 checks complete
    │       → stage 1 Active
    │       → stages 2–5 Waiting
    │
    └── ActionCard.Presentation
            → Inspecting
            → Cancel visible
            → Cancel disabled
```

The initial state is presentation-only. No inspection operation begins yet.

The progress construction was moved out of `ModelInspectionPage.xaml.cs` so the page coordinates controls while the new factory owns the exact initial progress data.

## Initial selected-model information

The current path-only navigation contract allows the page to derive:

```text
selected filename
file extension
basic GGUF versus generic MODEL label
```

It deliberately does not invent:

- quantisation;
- architecture;
- parameter count;
- context length;
- publisher;
- file size;
- runtime compatibility.

Current summary example:

```text
GGUF · Awaiting full inspection
```

A future `ModelInspectionRequest` should carry selected format and validated quick-scan metadata so the page does not lose already discovered information.

## Five user-visible progress stages

```text
1. Check model package
2. Read model configuration
3. Validate tokenizer and chat setup
4. Validate model structure
5. Confirm core runtime compatibility
```

Initial state:

```text
Stage 1 → Active / Checking
Stages 2–5 → Waiting
Progress summary → 0 of 5 checks complete
```

### Meaning of the final stage

`Confirm core runtime compatibility` means:

```text
the pinned application runtime recognises the model
and may pass its result to Hardware Fit
```

It does not mean:

```text
Vulkan has initialised
GPU offloading has succeeded
requested context memory fits
TurboQuant has activated
inference has completed
```

Those are backend- and hardware-specific checks performed later.

## Engineering gate ladder

The development gates are intentionally separate from the five progress rows:

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

This order distinguishes managed/native loading, ordinary Vulkan, fork correctness, Vulkan kernel behavior and managed/custom-backend compatibility.

### Why Vulkan is later

CPU is the diagnostic and lightweight-inspection baseline. It removes GPU-driver, shader, device-selection and offload variability while the project proves that the runtime can safely understand the model.

Ordinary Vulkan is then tested independently. Only after ordinary Vulkan and the TurboQuant CPU reference both work should they be combined in the TurboQuant Vulkan gate.

### Future backend-verification flow

After Model Inspection returns `Ready for hardware analysis`, a separate future flow may verify:

```text
Select compatible backend
Initialise Vulkan device
Load and offload model
Allocate context and KV cache
Activate selected TurboQuant format
Run a small inference check
Confirm GPU use and absence of silent fallback
```

That flow is not part of the current Model Inspection card.

## Control responsibilities

```text
InspectionModelCard
    → What model is being inspected?

InspectionContentCard
    → What is happening, or what was found?

InspectionOutcomeCard
    → What is the final high-level conclusion?

InspectionActionCard
    → What can the user do in the current state?
```

Full control contracts and file inventory:

- [Inspection controls README](./Controls/README.md)

## Presentation architecture

The feature currently follows this UI data flow:

```text
Models
    → define presentation data contracts

Presentation factory
    → assembles the approved initial state

ModelInspectionPage
    → assigns the state to controls

UserControl Presentation dependency property
    ↓
property-change callback
    ↓
Bindings.Update, visual-state selection, or template selection
    ↓
visible XAML
```

The `Models` folder contains UI presentation data and enums. It does not contain GGUF, OpenVINO, domain result or native-runtime objects.

- [Presentation-model README](./Models/README.md)
- [Presentation-construction README](./Presentation/README.md)

## Template-selection boundary

`InspectionContentCard` has two actual XAML structures:

```text
ProgressTemplate
    → running five-stage tracker

FindingsTemplate
    → warnings, conversion, invalid, unsupported,
      incomplete, cancelled, and operational-failure content
```

`InspectionContentTemplateSelector` handles both normal presentation input and temporary WinUI bootstrap input such as null, a `ContentControl`, or a `ContentPresenter` before the compiled binding has supplied its final value.

This prevents:

- the presentation class name being rendered as plain text;
- a normal initialization call being treated as a broken application contract.

## Navigation architecture

```text
ModelImportPage.ModelInspectionRequested
        │
        ▼
OnboardingShellPage.NavigateToModelInspection(path)
        │
        ├── StageFrame.Navigate(ModelInspectionPage, path)
        ├── CurrentStage = InspectModel
        └── StageIndicator.CurrentStage = InspectModel
                        │
                        ▼
ModelInspectionPage.OnNavigatedTo(parameter)
```

The page does not know or manipulate the onboarding shell.

## Selected application runtime

[ADR-001](../../../docs/architecture/decisions/ADR-001-llamasharp-application-runtime.md) separates the research and application runtimes:

```text
Research evidence
    llama.cpp b9870
    2d973636e292ee6f75fadcf08d29cb33511f509f

Selected application pair
    LLamaSharp 0.27.0
    LLamaSharp.Backend.Cpu 0.27.0
    mapped llama.cpp 3f7c29d318e317b63f54c558bc69803963d7d88c
```

The selected pair is intended for both Model Inspection and later in-application GGUF inference. It is currently referenced only by the isolated feasibility project.

### First feasibility slice

The first tool slice:

- pins both packages to exact versions;
- disables CUDA and Vulkan selection;
- calls `NativeLibraryConfig.LLama.DryRun`;
- records runtime and selected-backend metadata as JSON;
- maps native infrastructure failures to operational codes;
- does not accept or inspect a model;
- does not change the WinUI project file.

A dedicated Windows workflow tests, builds and dry-runs this tool. A successful hosted run is useful integration evidence, but the target-laptop run remains a separate gate.

## Tests and evidence

Initial progress semantics:

- [`InitialInspectionProgressPresentationTests.cs`](../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/InitialInspectionProgressPresentationTests.cs)

Destination navigation:

- [`ModelInspectionPageNavigationTests.cs`](../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageNavigationTests.cs)

Shell navigation and stage synchronization:

- [`OnboardingModelInspectionNavigationTests.cs`](../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingModelInspectionNavigationTests.cs)

Model Import request boundary:

- [`ModelImportNavigationRequestTests.cs`](../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelImportNavigationRequestTests.cs)

Content-template selection and bootstrap handling:

- [`InspectionContentTemplateSelectorTests.cs`](../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/Controls/InspectionContentTemplateSelectorTests.cs)

Runtime smoke unit tests:

- [`PinnedApplicationRuntimeTests.cs`](../../../tools/ModelInspection.LlamaSharpSpike.Tests/PinnedApplicationRuntimeTests.cs)
- [`SpikeOptionsParserTests.cs`](../../../tools/ModelInspection.LlamaSharpSpike.Tests/SpikeOptionsParserTests.cs)
- [`SmokeEvidenceWriterTests.cs`](../../../tools/ModelInspection.LlamaSharpSpike.Tests/SmokeEvidenceWriterTests.cs)

Runtime workflow:

- [`.github/workflows/llamasharp-feasibility-smoke.yml`](../../../.github/workflows/llamasharp-feasibility-smoke.yml)

The new progress test source is present, but a fresh Windows packaged test run is still required. No passing test result is claimed by this documentation update.

## Implemented now

- event-driven navigation into stage two;
- path validation and storage;
- Loaded-based initial UI application;
- one-time initialization guard;
- four reusable controls;
- presentation dependency properties;
- model-card compact and detailed layouts;
- content-card progress and findings layouts;
- outcome tones and result kinds;
- action-card inspecting and result layouts;
- initial selected-model, progress, outcome, and action presentations;
- focused initial-progress presentation factory;
- exact five-stage core-runtime wording;
- template selector bootstrap handling;
- focused navigation, selector and progress-contract tests;
- accepted application-runtime ADR;
- accepted core-inspection/backend-verification ADR;
- isolated exact-version LLamaSharp CPU-backend smoke source and tests;
- dedicated Windows smoke workflow.

## Not implemented and non-claims

- no LLamaSharp reference in the WinUI application project;
- no Vulkan or TurboQuant backend package in the WinUI application project;
- no production `ILlamaModelProbe` or `LlamaSharpModelProbe`;
- no model is loaded by the first native smoke slice;
- no OpenVINO Runtime or GenAI inspection adapter;
- no real package, tokenizer, structure, or runtime check executes in the page;
- no dynamic stage updates;
- no inspection evidence or diagnostic report is produced for a model;
- no deterministic outcome classifier exists;
- no enabled cancellation command or active inspection token exists;
- no continuation to Hardware Fit exists;
- no successful CPU, Vulkan, TurboQuant or target-machine runtime result is claimed until fresh evidence exists.

## Known technical debt

- `ModelInspectionPage.xaml.cs` still constructs the initial model and action presentations directly; only progress construction has moved into a focused factory;
- navigation carries only a string path rather than a project-owned request containing validated metadata;
- `Models` is an ambiguous folder name in an AI project because it contains UI presentation models;
- some status brushes are constructed in C# rather than resolved through shared theme resources;
- presentation contracts use WinUI types such as `Visibility`, `Symbol`, and `ICommand`, which is acceptable for the presentation layer but unsuitable for future runtime/domain results;
- there is no central inspection state machine, ViewModel, service interface, production runtime probe, or classifier yet;
- the spike does not yet prove whether `VocabOnly` is sufficient for lightweight inspection;
- the future backend-verification UI and service boundary are not yet designed in implementation detail.

## Recommended next architecture layer

```text
ModelInspectionPage
        ↓
ModelInspectionViewModel
        ↓
IModelInspectionService
        ↓
ModelInspectionService
        ├── ILlamaModelProbe
        │       └── LLamaSharp / matched llama.cpp CPU backend
        └── IOpenVinoModelProbe
                └── OpenVINO Runtime / GenAI

Runtime evidence
        ↓
ModelInspectionClassifier
        ↓
project-owned inspection result
        ↓
presentation factory
        ↓
existing four controls
```

After that core flow is proven, Hardware Fit and backend verification may select and validate ordinary Vulkan or a custom TurboQuant backend through separate adapters.

The real service layer must keep lightweight pre-Hardware-Fit inspection proportionate and avoid blindly allocating a model before machine suitability is known.

## Related documentation

- [Application feature architecture](../README.md)
- [Model Import architecture](../ModelImport/README.md)
- [Onboarding architecture](../Onboarding/README.md)
- [Inspection controls](./Controls/README.md)
- [Inspection presentation models](./Models/README.md)
- [Presentation construction](./Presentation/README.md)
- [ADR-001: selected LLamaSharp application runtime](../../../docs/architecture/decisions/ADR-001-llamasharp-application-runtime.md)
- [ADR-002: core inspection versus backend verification](../../../docs/architecture/decisions/ADR-002-core-inspection-versus-backend-verification.md)
- [Core runtime progress design](../../../docs/superpowers/specs/2026-08-04-core-runtime-progress-and-backend-gates-design.md)
- [Core runtime progress implementation plan](../../../docs/superpowers/plans/2026-08-04-core-runtime-progress-and-backend-gates.md)
- [LLamaSharp spike design](../../../docs/superpowers/specs/2026-08-04-llamasharp-feasibility-spike-design.md)
- [LLamaSharp spike implementation plan](../../../docs/superpowers/plans/2026-08-04-llamasharp-feasibility-spike.md)
- [LLamaSharp feasibility tool](../../../tools/ModelInspection.LlamaSharpSpike/README.md)
