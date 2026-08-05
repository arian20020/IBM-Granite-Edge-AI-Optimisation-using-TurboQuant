# Model Inspection application contracts

**Status:** Immutable application-owned request, progress, evidence, finding, result, and execution contracts are implemented and verified. Navigation wiring, worker mapping, classification, service orchestration, ViewModel behavior, and UI integration remain later tasks.

## Purpose

This folder defines the language used inside the WinUI application's Model Inspection feature.

```text
Model Import
    ↓ ModelInspectionRequest
Model Inspection application layer
    ↓ ModelInspectionProgress / ModelInspectionExecutionResult
ViewModel and presentation
```

These contracts are deliberately different from the shared worker protocol.

```text
Application request/result language
    describes what the application needs and may present

Worker transport/evidence language
    describes versioned process messages and raw technical evidence
```

Keeping those domains separate prevents JSON, process, LLamaSharp, and native-runtime details from spreading into navigation, classification, ViewModels, and controls.

## Owned responsibilities

The contracts own:

- immutable model-selection identity from the successful Model Import quick scan;
- the authoritative fully qualified model path used only by `ModelInspectionRequest`;
- a path-minimised final filename for display and diagnostics;
- the five approved lightweight inspection stages;
- nullable progress fractions when the runtime cannot measure genuine progress;
- immutable file, configuration, tokenizer, chat-template, runtime, and observation evidence;
- deterministic classified outcomes and findings;
- the rule that only `Ready` and `ReadyWithWarnings` may continue to Hardware Fit;
- mutually exclusive `Completed`, cooperative `Cancelled`, and `OperationalFailure` execution states.

## Request invariants

`ModelInspectionRequest` requires:

- a fully qualified model path;
- a final filename that matches the path using Windows case-insensitive comparison;
- a positive expected file length;
- a non-default UTC last-write timestamp;
- a successful bounded GGUF quick-scan snapshot;
- agreement between the expected file length and quick-scan file size.

The request constructor does not open, hash, or modify the model. The later request factory owns the immediate file-system continuity check before navigation.

## Evidence design

The evidence graph is split into cohesive records:

- `ModelInspectionFileEvidence`
- `ModelInspectionConfigurationEvidence`
- `ModelInspectionTokenizerEvidence`
- `ModelInspectionChatTemplateEvidence`
- `ModelInspectionRuntimeIdentity`
- `ModelInspectionObservation`

The design preserves enough information for deterministic classification and audit while minimising sensitive data:

- the full model path is not retained in completed evidence;
- the canonical path is represented by a SHA-256 digest;
- complete chat-template text is never retained;
- unavailable native metadata remains `null` rather than being guessed;
- collections are defensively copied before exposure;
- completed evidence requires confirmed model integrity.

## Result and execution rules

`ModelInspectionResult` separates reliable technical evidence from application classification.

```text
Ready / ReadyWithWarnings
    may continue to Hardware Fit

ConversionRequired
    requires an implemented and verified conversion-route identifier

IncompletePackage / Unsupported / Invalid
    cannot continue
```

`ModelInspectionExecutionResult` can be created only through named factories:

```text
Completed(result)
Cancelled(cooperative: true)
OperationalFailure(failure)
```

Forced process termination is not successful cancellation. It must be mapped to an operational failure by the later service layer.

## Approved bridges

Only the following future mappers may translate between this application domain and the shared worker transport domain:

- `WorkerRequestMapper`
- `WorkerResultMapper`

Pages, controls, ViewModels, and classifiers must not reference shared worker records directly.

## Forbidden responsibilities

This folder must not contain:

- WinUI or XAML types;
- pages, controls, navigation, or presentation models;
- LLamaSharp or llama.cpp types;
- native handles, pointers, or runtime objects;
- JSON serialization or protocol discriminators;
- process creation, stream framing, timeout, cancellation, or kill logic;
- file opening, hashing, quick scanning, or model modification;
- model classification policies;
- guessed values derived from model filenames.

## Verification

Verified implementation checkpoint:

```text
Commit:  50eaf40950240558a5274778a03acd5a7f57b10c
Run:     31019835159
Result:  63 worker-contract tests passed
         WinUI application built
         packaged test project built
         196 unit and WinUI UI-thread tests passed
```

The tests cover:

- path, filename, file-size, and UTC identity invariants;
- successful GGUF quick-scan snapshots;
- progress-stage and fraction rules;
- path and chat-template data minimisation;
- nullable unavailable evidence;
- complete approved runtime identity;
- defensive collection copying;
- conversion-route and Hardware Fit continuation rules;
- mutually exclusive execution states;
- cooperative cancellation only;
- prohibition of worker, LLamaSharp, native-handle, and XAML types in the application contract graph.

## Non-claims

This implementation does not prove that:

- navigation carries `ModelInspectionRequest` yet;
- a worker process exists or is packaged;
- LLamaSharp is invoked from the application;
- worker evidence is mapped or classified;
- inspection progress, cancellation, results, or failures appear in the UI;
- a real GGUF can yet be inspected through the production application.

Those claims require the later navigation, mapper, worker, service, ViewModel, packaging, and real-model gates.

## Related documentation

- `../../../../../shared/GraniteEdgeAI.ModelInspection.Contracts/README.md`
- `../../../../../docs/superpowers/specs/2026-08-05-model-inspection-worker-integration-design.md`
- `../../../../../docs/superpowers/plans/2026-08-05-model-inspection-worker-gate-1-contracts-protocol.md`
