# Model Inspection architecture

**Status:** Living current-state documentation  
**Last reviewed:** 2026-08-04  
**Reviewed application baseline:** `2c51bb0551cb5556e422e63c19888c1f3874d0e5`  
**Current branch:** `feature/model-inspection`

[← Application feature architecture](../README.md)

## Purpose

Model Inspection is onboarding stage two. It is intended to inspect the selected model package before Hardware Fit is checked, present progressive evidence, and classify the final model outcome.

The current application implementation establishes the **navigation and UI architecture**. A separate feasibility tool now pins and dry-runs the selected LLamaSharp CPU backend, but the WinUI feature does not yet call that runtime or inspect a model.

## Current status

| Capability | Current status |
|---|---|
| Navigate from validated Model Import | Implemented |
| Receive selected model path | Implemented |
| Persistent onboarding indicator moves to Inspect Model | Implemented |
| Compose four reusable inspection controls | Implemented |
| Build initial selected-model presentation | Implemented |
| Build initial five-stage progress presentation | Implemented |
| Build initial action presentation | Implemented |
| Select Progress versus Findings XAML template | Implemented with bootstrap handling |
| Application runtime decision | Accepted in ADR-001 |
| Isolated LLamaSharp CPU-backend smoke source | Implemented; Windows verification pending |
| LLamaSharp reference in the WinUI application | Not added |
| Local cold-run verification of the latest selector fix | Required before claiming the screen is fully verified |
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
└── Models/
    ├── README.md
    ├── presentation classes
    └── mode, status, tone, badge, and outcome enums
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
- [LLamaSharp feasibility spike](../../../tools/ModelInspection.LlamaSharpSpike/README.md)

## Responsibility boundary

### This feature currently owns

- receipt of a selected model path from onboarding navigation;
- Model Inspection page lifecycle;
- layout and composition of four reusable cards;
- UI presentation contracts for progress and result states;
- initial static progress state;
- card visibility, visual-state, and template-selection wiring;
- tests around navigation and template selection.

### This feature does not currently own

- native llama.cpp or OpenVINO calls;
- model parsing beyond the earlier quick scan;
- full tensor validation;
- tokenizer or chat-template validation;
- runtime support evidence;
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
    │       → Progress
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

## Five current progress stages

```text
1. Check model package
2. Read model configuration
3. Validate tokenizer and chat setup
4. Validate model structure
5. Confirm runtime support
```

Initial state:

```text
Stage 1 → Active / Checking
Stages 2–5 → Waiting
Progress summary → 0 of 5 checks complete
```

These labels describe the intended future runtime workflow. They are not currently backed by executed checks.

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
Presentation object
    ↓
UserControl Presentation dependency property
    ↓
property-change callback
    ↓
Bindings.Update, visual-state selection, or template selection
    ↓
visible XAML
```

The `Models` folder contains these presentation contracts and enums. It does not contain GGUF, OpenVINO, or AI model objects.

- [Presentation-model README](./Models/README.md)

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

The selector fix still requires a local cold build, Test Explorer run, and manual navigation check before final UI verification is claimed. The new runtime tool similarly requires fresh workflow and target-machine output before backend-loading success is claimed.

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
- template selector bootstrap handling;
- focused navigation and selector tests;
- accepted application-runtime ADR;
- isolated exact-version LLamaSharp CPU-backend smoke source and tests;
- dedicated Windows smoke workflow.

## Not implemented and non-claims

- no LLamaSharp reference in the WinUI application project;
- no production `ILlamaModelProbe` or `LlamaSharpModelProbe`;
- no model is loaded by the first native smoke slice;
- no OpenVINO Runtime or GenAI inspection adapter;
- no real package, tokenizer, structure, or runtime check executes in the page;
- no dynamic stage updates;
- no inspection evidence or diagnostic report is produced for a model;
- no deterministic outcome classifier exists;
- no enabled cancellation command or active inspection token exists;
- no continuation to Hardware Fit exists;
- no successful hosted or target-machine backend run is claimed until fresh evidence exists.

## Known technical debt

- `ModelInspectionPage.xaml.cs` currently constructs presentation objects directly and will grow if runtime states are added there;
- navigation carries only a string path rather than a project-owned request containing validated metadata;
- `Models` is an ambiguous folder name in an AI project because it contains UI presentation models;
- some status brushes are constructed in C# rather than resolved through shared theme resources;
- presentation contracts use WinUI types such as `Visibility`, `Symbol`, and `ICommand`, which is acceptable for the presentation layer but unsuitable for future runtime/domain results;
- there is no central inspection state machine, ViewModel, service interface, production runtime probe, or classifier yet;
- the spike does not yet prove whether `VocabOnly` is sufficient for lightweight inspection.

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

The real service layer must keep lightweight pre-Hardware-Fit inspection proportionate and avoid blindly allocating a model before machine suitability is known.

## Related documentation

- [Application feature architecture](../README.md)
- [Model Import architecture](../ModelImport/README.md)
- [Onboarding architecture](../Onboarding/README.md)
- [Inspection controls](./Controls/README.md)
- [Inspection presentation models](./Models/README.md)
- [ADR-001: selected LLamaSharp application runtime](../../../docs/architecture/decisions/ADR-001-llamasharp-application-runtime.md)
- [LLamaSharp spike design](../../../docs/superpowers/specs/2026-08-04-llamasharp-feasibility-spike-design.md)
- [LLamaSharp spike implementation plan](../../../docs/superpowers/plans/2026-08-04-llamasharp-feasibility-spike.md)
- [LLamaSharp feasibility tool](../../../tools/ModelInspection.LlamaSharpSpike/README.md)
