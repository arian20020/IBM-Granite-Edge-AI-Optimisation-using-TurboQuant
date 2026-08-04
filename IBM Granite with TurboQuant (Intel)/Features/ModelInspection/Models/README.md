# Model Inspection presentation models

**Status:** Living current-state documentation  
**Last reviewed:** 2026-08-04  
**Reviewed application baseline:** `2c51bb0551cb5556e422e63c19888c1f3874d0e5`

[← Model Inspection architecture](../README.md) · [Inspection controls](../Controls/README.md) · [Presentation construction](../Presentation/README.md)

## Important naming clarification

`Models` in this folder means **UI presentation models**.

It does not mean:

- GGUF files;
- OpenVINO model packages;
- neural-network tensor objects;
- runtime-loaded AI models;
- domain inspection results.

These classes are structured instructions telling the four reusable controls what to display.

## Purpose

The folder separates display data from control geometry and from state-construction behavior.

```text
Models/
    → defines which presentation data exists

Presentation/
    → constructs an approved presentation state

ModelInspectionPage or future ViewModel
    → chooses when that state is applied

Controls/
    → renders the supplied presentation
```

This allows one control layout to support many states without embedding state-specific decisions directly in XAML code-behind.

## Relationship to the `Presentation` folder

The new [`Presentation`](../Presentation/README.md) folder does not replace these models.

```text
InspectionContentCardPresentation
    = data contract

InitialInspectionProgressPresentationFactory
    = construction behavior that fills the contract
```

The first factory currently creates the approved five-stage initial progress state. Future runtime/domain results must still be converted into these UI-facing models only above the runtime and classifier boundaries.

## Responsibility boundary

### This folder owns

- control presentation data;
- semantic UI modes and statuses;
- action slots and commands;
- safe empty/hidden defaults;
- row/check data for repeaters;
- the one mutable Expander state currently needed by content presentation.

### This folder does not own

- presentation-factory orchestration;
- filesystem access;
- GGUF or OpenVINO parsing;
- native runtime evidence;
- classification rules;
- cancellation-token ownership;
- hardware-fit decisions;
- onboarding navigation.

## Object relationships

```text
InspectionModelCardPresentation
└── IReadOnlyList<InspectionCheckPresentation>

InspectionContentCardPresentation
├── IReadOnlyList<InspectionContentItemPresentation> Items
└── IReadOnlyList<InspectionContentItemPresentation> ExpandedItems

InspectionActionCardPresentation
├── InspectionActionPresentation CancelAction
├── InspectionActionPresentation SecondaryActionOne
├── InspectionActionPresentation SecondaryActionTwo
└── InspectionActionPresentation PrimaryAction

InspectionOutcomePresentation
└── self-contained outcome banner data
```

## Root presentation classes

### `InspectionModelCardPresentation.cs`

Supplies both compact and detailed model-card layouts.

Fields include:

```text
DisplayMode
BadgeState
ModelName
CompactSummary
FormatShortName
OverviewFormatBadgeText
Publisher
FormatName
Quantisation
ParameterCount
ModelType
DeclaredContext
FileSize
InspectionChecksSummary
InspectionChecks
```

Safe default:

```csharp
InspectionModelCardPresentation.Empty
```

The default uses empty strings, Compact mode, ModelSelected badge, and an empty checks list. It prevents null binding paths while the page has not supplied real presentation data.

[Open file](./InspectionModelCardPresentation.cs)

### `InspectionContentCardPresentation.cs`

Supplies both the progress and findings templates.

Main groups of data:

```text
Structure
    Mode
    SectionTitle
    ProgressSummary
    Items

Optional findings support
    SupportingText and Visibility
    TertiaryText, Status, and Visibility
    DiagnosticCode, Status, and Visibility

Disclosure/expanded report
    DisclosureStatus
    DisclosureSummary
    collapsed and expanded labels
    IsExpanded
    DisclosureVisibility
    ExpandedItems

Separate technical action
    OpenTechnicalDetailsCommand
    action text
    automation name
    Visibility
```

Safe default:

```csharp
InspectionContentCardPresentation.Hidden
```

This is the only root presentation currently implementing `INotifyPropertyChanged`. `IsExpanded` is mutable so a two-way XAML binding can update the disclosure state and notify bound text.

[Open file](./InspectionContentCardPresentation.cs)

### `InspectionOutcomePresentation.cs`

Supplies the high-level outcome banner:

```text
Kind
Tone
IconSymbol
Title
Message
AutomationName
```

Safe default:

```csharp
InspectionOutcomePresentation.Hidden
```

The semantic `Kind` determines whether an outcome exists. `Tone` determines the visual color family. Keeping these separate allows multiple outcomes to share one tone without losing semantic identity.

[Open file](./InspectionOutcomePresentation.cs)

### `InspectionActionCardPresentation.cs`

Supplies either the inspecting action layout or the completed result action layout:

```text
Mode
Title
Message
AutomationName
CancelAction
SecondaryActionOne
SecondaryActionTwo
PrimaryAction
```

Safe default:

```csharp
InspectionActionCardPresentation.Hidden
```

Fixed action slots keep XAML layout predictable. Optional actions use a hidden action presentation instead of null.

[Open file](./InspectionActionCardPresentation.cs)

## Child presentation classes

### `InspectionContentItemPresentation.cs`

Represents one progress stage, finding, warning, diagnostic row, or expanded report row.

```text
StageNumber
Title
Detail
DetailVisibility
Status
StatusText
IsActive
ShowConnector
AutomationName
```

The same type is deliberately reused across multiple DataTemplates because the rows share a common semantic shape.

The initial factory currently supplies these five progress titles:

```text
Check model package
Read model configuration
Validate tokenizer and chat setup
Validate model structure
Confirm core runtime compatibility
```

The titles deliberately exclude Vulkan, TurboQuant, GPU and Hardware Fit. Those belong to later backend verification rather than pre-Hardware-Fit model inspection.

[Open file](./InspectionContentItemPresentation.cs)

### `InspectionCheckPresentation.cs`

Represents one completed model-inspection check shown in the detailed model card.

```text
Title
Detail
Status
StatusText
AutomationName
```

It is separate from `InspectionContentItemPresentation` because detailed completed checks do not need stage numbers, active-ring state, or connector rules.

[Open file](./InspectionCheckPresentation.cs)

### `InspectionActionPresentation.cs`

Represents one button slot:

```text
Text
Command
CommandParameter
IsEnabled
Visibility
AutomationName
MinimumWidth
```

Safe default:

```csharp
InspectionActionPresentation.Hidden
```

The hidden default provides a non-null object for every compiled binding, even when a result state does not use all secondary action slots.

[Open file](./InspectionActionPresentation.cs)

## Mode and status enums

### Model card

#### `InspectionModelCardMode.cs`

```text
Compact
Detailed
```

[Open file](./InspectionModelCardMode.cs)

#### `InspectionModelBadgeState.cs`

Represents the compact status badge, including states such as model selected, inspected, source model, incomplete, unsupported, invalid, not inspected, and result unknown.

[Open file](./InspectionModelBadgeState.cs)

#### `InspectionCheckStatus.cs`

```text
Passed
Warning
Error
Information
```

Used by detailed inspection-check rows.

[Open file](./InspectionCheckStatus.cs)

### Content card

#### `InspectionContentCardMode.cs`

```text
Hidden
Progress
Warnings
ConversionRequired
IncompletePackage
Unsupported
Invalid
Cancelled
OperationalFailure
```

`Progress` uses the progress layout. All completed non-ready modes use the shared findings layout.

[Open file](./InspectionContentCardMode.cs)

#### `InspectionContentStatus.cs`

```text
Neutral
Waiting
Active
Passed
Warning
Error
Information
```

Used by stage markers, finding icons, status labels, diagnostic surfaces, and disclosure headers.

[Open file](./InspectionContentStatus.cs)

### Outcome card

#### `InspectionOutcomePresentationKind.cs`

```text
Hidden
Ready
ReadyWithWarnings
ConversionRequired
IncompletePackage
Unsupported
Invalid
Cancelled
OperationalFailure
```

This is the semantic result identity exposed by the UI layer.

[Open file](./InspectionOutcomePresentationKind.cs)

#### `InspectionOutcomeTone.cs`

```text
Success
Warning
Information
Error
Neutral
```

Multiple semantic outcomes can share one visual tone.

[Open file](./InspectionOutcomeTone.cs)

### Action card

#### `InspectionActionCardMode.cs`

```text
Hidden
Inspecting
Result
```

[Open file](./InspectionActionCardMode.cs)

## Default and hidden object strategy

The controls use non-null defaults:

```text
InspectionModelCardPresentation.Empty
InspectionContentCardPresentation.Hidden
InspectionOutcomePresentation.Hidden
InspectionActionCardPresentation.Hidden
InspectionActionPresentation.Hidden
```

Benefits:

- compiled binding paths remain safe during construction;
- controls can initialize before real page state exists;
- optional action slots do not need null checks in XAML;
- hidden state is explicit.

### Shared-instance caution

These defaults are static shared instances. Most properties are init-only, but `InspectionContentCardPresentation.IsExpanded` is mutable.

Do not mutate `InspectionContentCardPresentation.Hidden.IsExpanded`. A future cleanup should consider either:

- making hidden/default factories return new instances; or
- separating mutable view state from otherwise immutable presentation data.

## WinUI dependencies

These presentation types intentionally belong to the UI layer and currently use:

```text
Microsoft.UI.Xaml.Visibility
Microsoft.UI.Xaml.Controls.Symbol
System.Windows.Input.ICommand
INotifyPropertyChanged
```

That is acceptable for control presentation, but future runtime/domain types must remain framework-neutral.

Bad future boundary:

```text
llama.cpp adapter returns InspectionOutcomePresentation
```

Recommended boundary:

```text
llama.cpp/OpenVINO adapter
    → framework-neutral runtime evidence

ModelInspectionClassifier
    → framework-neutral ModelInspectionResult

presentation factory or ViewModel
    → WinUI presentation objects
```

## Mutation model

Most presentation properties are `init`-only. The page replaces the complete root presentation when a significant state changes.

Current exception:

```text
InspectionContentCardPresentation.IsExpanded
```

This property changes in place and raises `PropertyChanged` because the Expander uses two-way binding.

Future dynamic progress could use either:

1. replace complete immutable presentation snapshots; or
2. introduce a dedicated observable ViewModel/state object.

Mixing both patterns without a clear rule would make binding updates difficult to reason about.

## Tests and evidence

Presentation objects are exercised through:

- [`InitialInspectionProgressPresentationTests.cs`](../../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/InitialInspectionProgressPresentationTests.cs)
- [`InspectionContentTemplateSelectorTests.cs`](../../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/Controls/InspectionContentTemplateSelectorTests.cs)
- [`ModelInspectionPageNavigationTests.cs`](../../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageNavigationTests.cs)
- [`OnboardingModelInspectionNavigationTests.cs`](../../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingModelInspectionNavigationTests.cs)

The new initial-progress test protects:

- exact five-stage wording and order;
- initial active/waiting status;
- connector count;
- completed-stage summary;
- separation from backend-specific terminology.

Dedicated presentation-contract tests are still needed for:

- default/hidden invariants;
- every enum-to-layout mapping;
- every outcome kind/tone combination;
- action-slot visibility and command data;
- required accessibility text;
- future presentation factories.

## Implemented now

- four root presentation classes;
- three child presentation classes;
- explicit mode, status, tone, badge, and outcome enums;
- non-null hidden/default objects;
- empty list defaults;
- one observable expanded-state property;
- action-command slots;
- accessibility fields throughout presentation data;
- a separate initial-progress factory using these contracts.

## Not implemented and non-claims

- these classes do not contain runtime evidence;
- no classifier creates final presentations from real inspection results;
- no ViewModel owns the complete state transition graph;
- no domain result or diagnostic-code contract exists yet;
- enum values alone do not mean the corresponding runtime route is implemented;
- the presence of a core-runtime stage does not mean CPU, Vulkan or TurboQuant has passed.

## Known limitations and change hazards

- the folder name `Models` is ambiguous in an AI application;
- presentation types are coupled to WinUI and should not cross into runtime adapters;
- shared mutable hidden state is a risk around `IsExpanded`;
- adding an enum value requires selector, visual-state, presentation-factory, test, and documentation review;
- fixed action slots are simple now but may need a different layout strategy if result actions become highly variable;
- required versus optional fields are currently enforced mainly by page/control/factory construction rather than domain-level contracts.

## Current and future organization

```text
ModelInspection/
├── Models/              ← current WinUI presentation data contracts
├── Presentation/        ← current presentation construction behavior
├── Domain/              ← future framework-neutral results and findings
├── Services/            ← future workflow orchestration
├── Runtime/Gguf/        ← future llama.cpp adapter
├── Runtime/OpenVino/    ← future OpenVINO adapter
└── Controls/
```

A future rename of `Models` should be performed only with coordinated namespace, XAML `x:DataType`, project, test, and documentation updates.

## Related documentation

- [Model Inspection architecture](../README.md)
- [Inspection controls](../Controls/README.md)
- [Presentation construction](../Presentation/README.md)
- [ADR-002: core inspection versus backend verification](../../../docs/architecture/decisions/ADR-002-core-inspection-versus-backend-verification.md)
