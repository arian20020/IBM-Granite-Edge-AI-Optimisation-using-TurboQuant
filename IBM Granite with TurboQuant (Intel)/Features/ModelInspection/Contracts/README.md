# Model Inspection application contracts

**Status:** Immutable contracts are implemented and used by the protected GGUF inspection journey
**Last reviewed:** 2026-08-09

[Back to Model Inspection architecture](../README.md)

## Purpose

This folder defines the framework-neutral language used inside the WinUI
application's Model Inspection feature.

```text
Model Import
    -> ModelInspectionRequest
Onboarding shell
    -> same exact request instance
Model Inspection service/ViewModel
    -> ModelInspectionProgress
    -> ModelInspectionExecutionResult
Presentation
```

Application contracts are deliberately separate from shared worker transport
contracts. Application types describe what the product can trust, classify,
and present; worker types describe versioned process messages and factual
technical evidence.

## Owned responsibilities

- immutable selected-model identity from successful Model Import;
- authoritative fully qualified path inside `ModelInspectionRequest` only;
- path-minimised filename for display and diagnostics;
- expected file length and UTC last-write identity;
- bounded successful GGUF quick-scan snapshot;
- five approved inspection stages;
- explicit active, completed, warning, failed, and cancelled stage states;
- nullable genuine progress fractions;
- path-minimised file, configuration, tokenizer, chat-template, and runtime
  evidence;
- deterministic findings and classified outcomes;
- Hardware Fit eligibility rules without executing Hardware Fit;
- mutually exclusive completed, cooperative-cancelled, and operational-failure
  execution states.

## Request lifecycle

`ModelInspectionRequestFactory` performs the final filesystem continuity check
and creates the request. It requires a fully qualified path, matching final
filename, positive expected length, non-default UTC timestamp, successful GGUF
quick scan, and agreement between expected length and scan size.

The same request instance is forwarded by the onboarding shell and retained by
the inspection page/ViewModel. UI presentation uses only the safe filename and
quick-scan facts; it does not parse display values back out of the path.

## Progress rules

`ModelInspectionProgress` identifies one of the fixed five stages, its explicit
stage status, completed/total counts, optional fraction, and privacy-safe user
message. Fractions are either real values in the inclusive `0..1` range or
`null`; callers must not manufacture a percentage.

## Evidence design

The evidence graph is split into cohesive records:

- `ModelInspectionFileEvidence`
- `ModelInspectionConfigurationEvidence`
- `ModelInspectionTokenizerEvidence`
- `ModelInspectionChatTemplateEvidence`
- `ModelInspectionRuntimeIdentity`
- `ModelInspectionObservation`

Completed evidence does not retain the full path or complete chat-template
text. Canonical path is represented by a SHA-256 digest where required,
unavailable native metadata remains `null`, collections are defensively copied,
and completed evidence requires confirmed model integrity.

## Outcome and execution rules

```text
Ready / ReadyWithWarnings
    eligible for a later Hardware Fit navigation slice

ConversionRequired
    requires an implemented and verified conversion-route identifier

IncompletePackage / Unsupported / Invalid
    not eligible to continue
```

Unsupported is not automatically treated as convertible.

Execution results can be created only through the named terminal factories:

```text
Completed(result)
Cancelled(cooperative: true)
OperationalFailure(failure)
```

Forced termination or an unconfirmed cancellation exception is an operational
failure, not successful cancellation.

## Approved bridge

Only the application runtime adapter maps between application and worker
domains:

- `WorkerRequestMapper`
- `WorkerResultMapper`

Pages, controls, ViewModels, presentation factories, services, and classifiers
must not reference shared worker records directly.

## Forbidden responsibilities

This folder must not contain WinUI/XAML types, navigation, LLamaSharp or
llama.cpp types, worker JSON, process creation, streams, timeouts, file opening,
hashing, scanning, classifier policy, native handles, pointers, or guessed
filename-derived facts.

## Verification coverage

Tests cover request identity, quick-scan invariants, all progress stage/status
rules, nullable fractions, privacy minimisation, evidence nullability, runtime
identity, defensive collection copying, conversion/continuation rules,
mutually exclusive execution states, cooperative cancellation, and forbidden
dependency boundaries.

Relevant packaged tests include:

- `ModelInspectionContractTests`
- `ModelInspectionExecutionResultAdditionalTests`
- `ModelInspectionRequestFactoryTests`
- `ModelInspectionPageNavigationTests`
- `Gate5ApplicationBoundaryContractTests`

## Scope and non-claims

These contracts support the current local GGUF Windows x64 CPU
LLamaSharp/llama.cpp `VocabOnly` inspection route. They do not prove or execute
OpenVINO, TurboQuant, Vulkan/GPU, context creation, full inference,
performance/quality benchmarking, conversion, Hardware Fit, or chat. The
packaged build/test closure also does not replace pending extracted MSIX and
hosted exact-head release attestation.

## Related documentation

- [Model Inspection runtime adapter](../Runtime/README.md)
- [Model Inspection service](../Services/README.md)
- [Shared worker contracts](../../../../../shared/GraniteEdgeAI.ModelInspection.Contracts/README.md)
- [Protected worker ADR](../../../../../docs/architecture/decisions/ADR-003-protected-model-inspection-worker.md)
