# Model Inspection classification

**Status:** Implemented for reliable GGUF CPU/VocabOnly evidence
**Last reviewed:** 2026-08-09

[Back to Model Inspection architecture](../README.md)

## Purpose

Classification converts trusted application evidence into a user-facing model
outcome. It remains separate from worker execution so transport or process
failures cannot be mistaken for properties of the model.

```text
ModelInspectionEvidence
    -> IModelInspectionClassifier
    -> ModelInspectionClassifier
    -> ModelInspectionResult
```

## Current deterministic rules

The live GGUF route currently justifies two outcomes:

| Reliable evidence | Outcome |
|---|---|
| tokenizer smoke passed and embedded chat template present | `Ready` |
| tokenizer smoke passed and embedded chat template absent | `ReadyWithWarnings` with `MI-WARN-CHAT-TEMPLATE-MISSING` |

An unknown chat-template state or an unsuccessful tokenizer smoke is rejected
rather than classified. Other application outcome enum values remain valid
presentation/contracts for future evidence policies, but this classifier does
not currently produce `ConversionRequired`, `IncompletePackage`,
`Unsupported`, or `Invalid`.

## Ownership boundary

The classifier:

- consumes only application-owned evidence;
- creates stable findings, summaries, and recommended actions;
- records the service-supplied UTC start and completion times;
- never sees a model path, worker process, protocol record, native handle, or
  raw failure payload.

It does not navigate to Hardware Fit or execute conversion. A recommendation
to continue is presentation/domain information only.

## Scope and non-claims

Classification covers lightweight GGUF metadata, tokenizer smoke, and embedded
chat-template evidence from the approved CPU/VocabOnly route. It does not
classify OpenVINO, TurboQuant, Vulkan/GPU viability, context creation, full
inference, quality, or performance.

## Tests

`ModelInspectionClassifierTests` protects both supported outcomes, the stable
warning, timestamps, and fail-closed unreliable-evidence branches.
