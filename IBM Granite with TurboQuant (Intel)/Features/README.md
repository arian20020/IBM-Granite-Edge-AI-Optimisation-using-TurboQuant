# Application feature architecture

**Status:** Local GGUF Model Import and protected Windows x64 CPU Model Inspection journey implemented
**Last reviewed:** 2026-08-09

## Purpose

Application source is organized by user-facing feature. Each feature owns one
cohesive part of onboarding while the shell owns cross-stage navigation.
Framework-neutral worker contracts, process infrastructure, protected worker,
and native runtime remain separate repository boundaries.

## Source hierarchy

```text
Features/
|-- ModelImport/       selection, bounded quick scan, immutable request
|-- Onboarding/        stage frame, indicator, exact request/event lifecycle
`-- ModelInspection/   protected inspection application journey
    |-- Contracts/
    |-- Runtime/
    |-- Classification/
    |-- Services/
    |-- ViewModels/
    |-- Presentation/
    |-- Models/
    |-- Controls/
    `-- Infrastructure/
```

## Current onboarding status

| Stage | Current implementation |
|---|---|
| Model Import | Local GGUF picker, bounded scan, continuity check, immutable request |
| Model Inspection | Automatic protected x64 CPU/VocabOnly inspection, live stages, Cancel, retry, choose another, terminal result |
| Hardware Fit | Not implemented in this slice |
| Configure Model | Not implemented in this slice |
| Ready to Chat | Not implemented in this slice |

## Connected flow

```text
ModelImportPage
    -> choose local .gguf
    -> bounded quick scan
    -> ModelInspectionRequestFactory continuity check
    -> exact immutable request event

OnboardingShellPage
    -> forward same request instance
    -> synchronize frame and stage indicator

ModelInspectionPage
    -> fresh ViewModel for this navigation
    -> automatic service execution
    -> five live progress stages
    -> completed / cancelled / operational-failure presentation
    -> retry or choose another model as appropriate
```

The worker-to-application bridge is confined to the Model Inspection runtime
adapter. Pages, controls, presentation, ViewModels, services, and classifiers
use application-owned contracts only.

The reconciled packaged application suite passes 330/330 in Release/x64 with
zero failed, skipped, or not-executed tests. Extracted-MSIX and hosted
exact-head evidence remain separate release gates.

## Model Inspection boundary

The connected route is one short-lived protected Windows x64 worker using
LLamaSharp `0.27.0`, its matched CPU backend, and `VocabOnly` model loading. It
verifies file continuity/integrity, reads model configuration, performs
tokenizer smoke and chat-template presence checks, validates structure, and
reports exact runtime identity.

Application classification currently produces:

- `Ready` when tokenizer smoke passes and an embedded chat template is present;
- `ReadyWithWarnings` when tokenizer smoke passes and the template is absent.

Operational failures never become model outcomes. Only a cooperative worker
terminal is represented as successful cancellation.

## Scope and non-claims

The completed path is local GGUF Model Inspection on Windows x64 CPU only. It
does not perform OpenVINO, TurboQuant, Vulkan/GPU setup, context creation, full
inference, quality/performance benchmarking, conversion, Hardware Fit, or
chat. Non-x64 builds do not receive a fallback inspection backend. Extracted
MSIX and hosted exact-head release attestation remain separate release gates.

## Documentation map

- [Model Import architecture](./ModelImport/README.md)
- [Onboarding architecture](./Onboarding/README.md)
- [Model Inspection architecture](./ModelInspection/README.md)
- [Shared worker contracts](../../shared/GraniteEdgeAI.ModelInspection.Contracts/README.md)
- [Protected worker ADR](../../docs/architecture/decisions/ADR-003-protected-model-inspection-worker.md)

## Source-of-truth order

```text
1. Source code and executable tests
2. Nearest README beside the source
3. Parent feature README
4. Accepted ADRs
5. Recorded build/runtime evidence
6. Historical plans and pull-request descriptions
```
