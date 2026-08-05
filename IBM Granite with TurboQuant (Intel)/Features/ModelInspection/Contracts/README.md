# Model Inspection application contracts

**Status:** Immutable application-owned contracts and navigation handoff implemented and tested; worker mapping and runtime orchestration remain later gates  
**Last reviewed:** 2026-08-05

## Purpose

This folder defines the language used inside the WinUI application's Model Inspection feature.

```text
Model Import
    ↓ ModelInspectionRequest
Onboarding shell
    ↓ same request
Model Inspection application layer
    ↓ ModelInspectionProgress / ModelInspectionExecutionResult
Future ViewModel and presentation
```

These contracts are deliberately separate from the shared worker protocol.

```text
Application request/result language
    describes what the application needs, classifies, and may present

Worker transport/evidence language
    describes versioned process commands, messages, and raw technical evidence
```

## Owned responsibilities

- immutable model-selection identity from successful Model Import;
- authoritative fully qualified model path inside `ModelInspectionRequest` only;
- path-minimised final filename for display and diagnostics;
- expected file length and UTC last-write identity;
- bounded successful GGUF quick-scan snapshot;
- five approved inspection stages;
- nullable genuine progress fractions;
- path-minimised file/configuration/tokenizer/chat/runtime evidence;
- deterministic findings and classified outcomes;
- Hardware Fit continuation rules;
- mutually exclusive completed, cooperative-cancelled, and operational-failure states.

## Request lifecycle

`ModelInspectionRequestFactory` in Model Import performs the final file-system continuity check and then constructs `ModelInspectionRequest`.

The request requires:

- a fully qualified model path;
- a final filename matching the path;
- positive expected file length;
- non-default UTC last-write time;
- successful GGUF quick-scan snapshot;
- agreement between expected length and quick-scan size.

The same request instance is then forwarded by `OnboardingShellPage` and retained by `ModelInspectionPage`. The destination derives `SelectedModelPath` from `Request?.ModelPath`; it does not recreate or weaken the contract.

## Evidence design

The evidence graph is split into cohesive records:

- `ModelInspectionFileEvidence`
- `ModelInspectionConfigurationEvidence`
- `ModelInspectionTokenizerEvidence`
- `ModelInspectionChatTemplateEvidence`
- `ModelInspectionRuntimeIdentity`
- `ModelInspectionObservation`

Data minimisation rules:

- completed evidence does not retain the full model path;
- canonical path is represented by a SHA-256 digest where required;
- complete chat-template text is never retained;
- unavailable native metadata remains `null` rather than being guessed;
- collections are defensively copied;
- completed evidence requires confirmed model integrity.

## Result rules

```text
Ready / ReadyWithWarnings
    may continue to Hardware Fit

ConversionRequired
    requires an implemented and verified conversion-route identifier

IncompletePackage / Unsupported / Invalid
    cannot continue
```

Unsupported models are not automatically labelled convertible.

## Execution rules

`ModelInspectionExecutionResult` can be created only through named factories:

```text
Completed(result)
Cancelled(cooperative: true)
OperationalFailure(failure)
```

Forced process termination is an operational failure, not successful cancellation.

## Approved bridges

Only future application infrastructure mappers may translate between the application and worker domains:

- `WorkerRequestMapper`
- `WorkerResultMapper`

Pages, controls, ViewModels, and classifiers must not reference shared worker records directly.

## Forbidden responsibilities

This folder must not contain:

- WinUI/XAML types, pages, controls, navigation, or presentation models;
- LLamaSharp or llama.cpp types;
- worker JSON discriminators or serialization;
- process creation, streams, timeouts, cancellation, or kill logic;
- file opening, hashing, scanning, or model modification;
- classifier policy implementation;
- native handles, pointers, or guessed filename-derived facts.

## Verification coverage

Tests cover:

- path, filename, length, and UTC identity invariants;
- successful GGUF snapshots;
- progress-stage and fraction rules;
- path and chat-template minimisation;
- nullable unavailable evidence;
- runtime identity completeness;
- defensive collection copying;
- conversion-route and continuation rules;
- mutually exclusive execution states;
- cooperative cancellation only;
- prohibition of worker, LLamaSharp, native-handle, and XAML types;
- request creation and same-object navigation through Model Inspection.

Relevant tests:

- [`ModelInspectionContractTests.cs`](../../../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionContractTests.cs)
- [`ModelInspectionRequestFactoryTests.cs`](../../../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelInspectionRequestFactoryTests.cs)
- [`ModelInspectionPageNavigationTests.cs`](../../../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageNavigationTests.cs)

## Non-claims

This implementation does not prove that:

- the production worker executable exists or is packaged;
- LLamaSharp is invoked by the application;
- worker evidence is mapped or classified;
- progress, cancellation, results, or failures are live in the UI;
- a real GGUF is inspected through the production application route.

## Related documentation

- [Model Inspection architecture](../README.md)
- [Model Import architecture](../../ModelImport/README.md)
- [Shared worker contracts](../../../../../shared/GraniteEdgeAI.ModelInspection.Contracts/README.md)
- [Protected worker ADR](../../../../../docs/architecture/decisions/ADR-003-protected-model-inspection-worker.md)
