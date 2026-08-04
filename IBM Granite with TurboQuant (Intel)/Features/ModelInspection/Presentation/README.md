# Model Inspection presentation construction

**Status:** Living current-state documentation  
**Last reviewed:** 2026-08-04  
**Current branch:** `feature/model-inspection`

[← Model Inspection architecture](../README.md)

## Purpose

This folder contains focused code that **constructs presentation objects** for
the Model Inspection controls.

The adjacent [`Models`](../Models/README.md) folder defines the presentation
data shapes and enums. This folder contains the behavior that assembles those
shapes into a specific screen state.

```text
Models/
    → What data can a control receive?

Presentation/
    → How is one approved screen state assembled from that data?
```

## Current file inventory

### `InitialInspectionProgressPresentationFactory.cs`

Creates the initial five-row progress state shown when Model Inspection first
appears:

```text
1. Check model package                         Checking
2. Read model configuration                    Waiting
3. Validate tokenizer and chat setup           Waiting
4. Validate model structure                    Waiting
5. Confirm core runtime compatibility          Waiting
```

It also establishes:

- `InspectionContentCardMode.Progress`;
- the `Inspection progress` heading;
- `0 of 5 checks complete`;
- stage 1 as the only active row;
- connectors under stages 1–4;
- meaningful detail text for every stage;
- accessible row names.

## Responsibility boundary

The factory owns only the **initial progress presentation**.

It does not:

- call LLamaSharp or llama.cpp;
- run a CPU, Vulkan or TurboQuant backend;
- advance stages dynamically;
- own cancellation;
- classify model outcomes;
- decide whether Hardware Fit may continue;
- manipulate XAML controls directly;
- retain native runtime objects.

`ModelInspectionPage` requests the presentation and assigns it to
`InspectionContentCard`. A future ViewModel/service will replace the static
initial state with real progress updates.

## Core inspection versus backend verification

The five rows describe lightweight model inspection before Hardware Fit. The
final row is deliberately:

```text
Confirm core runtime compatibility
```

It does not claim that:

- Vulkan initialised;
- a GPU was selected;
- layers were offloaded;
- the requested context fits;
- TurboQuant activated;
- inference succeeded.

Those checks belong to later backend-verification gates recorded in
[ADR-002](../../../../docs/architecture/decisions/ADR-002-core-inspection-versus-backend-verification.md).

## Tests

The presentation contract is protected by:

- [`InitialInspectionProgressPresentationTests.cs`](../../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/InitialInspectionProgressPresentationTests.cs)

The tests verify:

- exact row count, wording and order;
- initial active/waiting states;
- connector geometry;
- completed-stage summary;
- absence of Vulkan, TurboQuant, GPU and Hardware Fit wording from row titles.

## Change hazards

Review this README, the parent README, ADR-002 and the tests when:

- a visible stage is renamed, inserted or removed;
- stage order changes;
- backend-specific checks move into Model Inspection;
- a real progress state machine replaces the initial static factory;
- the progress card's accessibility contract changes.
