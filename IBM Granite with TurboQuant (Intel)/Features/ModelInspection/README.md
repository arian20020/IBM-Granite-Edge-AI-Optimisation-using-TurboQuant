# Model Inspection architecture

**Status:** Presentation implemented; GGUF CPU feasibility verified; production application integration pending  
**Last reviewed:** 2026-08-05  
**Current branch:** `feature/model-inspection`

[← Application feature architecture](../README.md)

## Purpose

Model Inspection is onboarding stage two. It examines a validated model package
before Hardware Fit, explains what the core runtime found and eventually
produces one controlled application outcome.

```text
Validated Model Import
        ↓
Model Inspection
        ↓
Ready / Ready with warnings /
Conversion required / Unsupported /
Invalid or incomplete
        ↓
Hardware Fit only when allowed
```

The WinUI presentation and navigation shell are implemented. The isolated GGUF
runtime feasibility boundary is now verified through LLamaSharp and its matched
CPU llama.cpp backend. The page is **not yet connected** to a production probe,
service, classifier or ViewModel.

## Architecture decisions

- [ADR-001 — matched LLamaSharp application runtime](../../../docs/architecture/decisions/ADR-001-llamasharp-application-runtime.md)
- [ADR-002 — core inspection versus backend verification](../../../docs/architecture/decisions/ADR-002-core-inspection-versus-backend-verification.md)

ADR-002 defines the meaning of this onboarding stage:

> Model Inspection establishes lightweight **core runtime compatibility**. It
> does not prove Vulkan, GPU offload, Hardware Fit, context allocation,
> TurboQuant activation or inference.

## Intended production architecture

```text
ModelInspectionPage
        ↓
ModelInspectionViewModel
        ↓
IModelInspectionService
        ↓
ModelInspectionService
        ├── ModelInspectionClassifier
        └── ILlamaModelProbe
                ↓
        protected worker process
                ↓
        LLamaSharp / llama.cpp
```

The original plan retained `ILlamaModelProbe` as a replaceable infrastructure
boundary. A real llama.cpp process abort was observed during feasibility work,
so a protected worker-process implementation is now strongly preferred over
loading the native runtime directly into the WinUI process. This remains a
formal design/ADR decision for the next application slice.

## Current capability status

| Capability | Current status |
|---|---|
| Navigate from validated Model Import | Implemented |
| Receive and retain selected model path | Implemented |
| Move onboarding indicator to Inspect Model | Implemented |
| Compose model, content, outcome and action controls | Implemented |
| Display initial selected-model summary | Implemented |
| Display approved five-stage tracker | Implemented |
| Protect stage wording and backend separation | Automated tests implemented and previously verified |
| Select Progress versus Findings templates | Implemented |
| Matched LLamaSharp CPU native smoke | Verified |
| CPU `VocabOnly` Granite probe | Verified |
| Read-only pre/post model integrity | Verified |
| Native progress, cancellation and disposal evidence | Verified through feasibility/trusted tests |
| Malformed and hostile GGUF containment | Verified through child-process trusted tests |
| Evidence privacy and model-leak scan | Verified |
| LLamaSharp package in the WinUI project | Deliberately not added |
| Production `ILlamaModelProbe` | Not implemented |
| Worker-process probe | Not implemented |
| Application service and classifier | Not implemented |
| `ModelInspectionViewModel` | Not implemented |
| Dynamic WinUI progress | Not implemented |
| Functional WinUI Cancel action | Not implemented |
| OpenVINO inspection | Not implemented |
| Vulkan or TurboQuant verification | Later engineering gates |
| Hardware Fit continuation | Not implemented |

## Source hierarchy

```text
Features/ModelInspection/
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
│   └── presentation classes and enums
└── Presentation/
    ├── README.md
    └── InitialInspectionProgressPresentationFactory.cs
```

The verified experimental/runtime source remains outside the WinUI feature:

```text
tools/
├── ModelInspection.LlamaSharpSpike/
├── ModelInspection.LlamaSharpSpike.Tests/
├── ModelInspection.LlamaSharpSpike.TestSupport/
├── ModelInspection.LlamaSharpSpike.NativeIntegrationTests/
└── ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/
```

Child documentation:

- [Inspection controls](./Controls/README.md)
- [Inspection presentation models](./Models/README.md)
- [Presentation construction](./Presentation/README.md)
- [LLamaSharp feasibility tool](../../../tools/ModelInspection.LlamaSharpSpike/README.md)
- [Trusted real-model tests](../../../tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/README.md)

## Page responsibility

`ModelInspectionPage` currently owns presentation composition only:

```text
Model inspection heading
Explanatory subtitle
InspectionOutcomeCard
InspectionModelCard
InspectionContentCard
InspectionActionCard
```

It does not:

- reference LLamaSharp;
- start a native worker;
- open or parse a GGUF;
- interpret native exceptions;
- classify model outcomes;
- choose a hardware backend;
- initialise Vulkan;
- activate TurboQuant;
- retain native handles.

### Page lifecycle

```text
constructor
    → InitializeComponent
    → subscribe to Loaded

OnNavigatedTo
    → require non-empty model path
    → retain SelectedModelPath
    → reset one-time presentation guard

Loaded
    → require SelectedModelPath
    → apply initial model, progress, outcome and action presentations
```

Receiving navigation data and manipulating loaded controls remain separate
lifecycle responsibilities.

## Initial presentation

```text
Outcome card
    → Hidden

Model card
    → Compact
    → Model selected
    → filename and basic format inferred from path
    → no invented runtime compatibility

Content card
    → Progress
    → 0 of 5 checks complete
    → stage 1 active
    → stages 2–5 waiting

Action card
    → Inspecting layout
    → Cancel visible but disabled until a real cancellable service exists
```

The initial tracker is created by:

```text
InitialInspectionProgressPresentationFactory.Create()
```

## Five user-visible stages

```text
1. Check model package
2. Read model configuration
3. Validate tokenizer and chat setup
4. Validate model structure
5. Confirm core runtime compatibility
```

These are user-facing workflow stages, not the CPU/Vulkan/TurboQuant engineering
gates.

| Visible row | Future real work |
|---|---|
| Check model package | Validate request, file identity and quick-scan continuity |
| Read model configuration | Start the protected core-runtime probe and collect metadata |
| Validate tokenizer and chat setup | Inspect vocabulary, special tokens and embedded chat-template evidence |
| Validate model structure | Inspect architecture, context and safe structural metadata |
| Confirm core runtime compatibility | Compare evidence and classify whether Hardware Fit may run |

Only genuine native fractions may be displayed. Where a meaningful percentage
does not exist, the UI should use stage count and an active spinner.

## Selected application runtime

```text
Managed package
    LLamaSharp 0.27.0

Native CPU backend package
    LLamaSharp.Backend.Cpu 0.27.0

Mapped llama.cpp commit
    3f7c29d318e317b63f54c558bc69803963d7d88c

Runtime identifier
    win-x64
```

The separate upstream research campaign remains:

```text
llama.cpp b9870
2d973636e292ee6f75fadcf08d29cb33511f509f
```

The two runtime identities must not be presented as the same build.

## Verified runtime gates

### Gate 1 — CPU native-library smoke

```text
Result:                  PASS
Managed package:         LLamaSharp 0.27.0
Backend package:         LLamaSharp.Backend.Cpu 0.27.0
Process architecture:    X64
Selected library:        LLama / AVX-512
CUDA selected:           false
Vulkan selected:         false
```

### Gate 2 — CPU `VocabOnly` Granite inspection

Controlled model:

```text
Filename: granite-4.1-3b-Q4_K_M.gguf
Length:   2,099,501,664 bytes
SHA-256:  662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29
```

Verified evidence:

```text
Completion:               Succeeded
Architecture:             granite
Model name:               Granite 4.1 3b
Declared context:         131,072
Embedding size:           2,560
Layers:                   40
Attention heads:          40
KV heads:                 8
Metadata entries:         31
Vocabulary entries:       100,352
Tokenizer smoke:          passed
Embedded chat template:   present
Native handle closed:     true
Original model preserved: true
```

The runtime parameter count is unavailable at this safe depth and remains
`null`; it is not guessed from the filename.

### Expanded Tier 1 verification

```text
Deterministic tests:       170 / 170 passed
Contained native tests:    4 / 4 passed
Build warnings/errors:     0 / 0
CPU smoke:                 passed
Artifact/privacy gate:     passed
```

### Trusted local Tier 2 verification

```text
Trusted tests:             20 / 20 passed
Failed / skipped:          0 / 0
Model SHA-256 unchanged:   yes
Evidence files scanned:    56
Privacy findings:          0
```

The trusted campaign covered success, repeatability, two cancellation scopes,
malformed inputs, file-access failures, privacy checks and network observation.

Evidence:

- [Tier 1 verification](../../../docs/testing/evidence/2026-08-04-llamasharp-tier1-verification.md)
- [Tier 2 verification](../../../docs/testing/evidence/2026-08-05-llamasharp-tier2-local-verification.md)
- [Coverage matrix](../../../docs/testing/LLamaSharp-Runtime-Test-Coverage-Matrix.md)

## Operational states versus model outcomes

The feasibility tool uses operation states:

```text
Succeeded
Cancelled
Failed
```

These are not final application model outcomes.

Future classifier outcomes are:

```text
Ready
ReadyWithWarnings
ConversionRequired
Unsupported
InvalidOrIncomplete
```

Operational failures remain separate. For example:

```text
Missing native DLL      ≠ Invalid model
File permission failure ≠ Unsupported model
Native worker failure   ≠ Corrupt GGUF
```

## Next production slice

The verified feasibility result supports moving to project-owned application
contracts:

```text
ModelInspectionRequest
ModelInspectionProgress
ModelInspectionEvidence
ModelInspectionFinding
ModelInspectionResult
RuntimeIdentity
```

Then implement:

```text
ILlamaModelProbe
        ↓
protected worker-process probe
        ↓
ModelInspectionClassifier
        ↓
IModelInspectionService / ModelInspectionService
        ↓
ModelInspectionViewModel
        ↓
live WinUI stages, outcomes and Cancel action
```

The worker process must speak a small versioned protocol and return ordinary
project-owned data. Native handles, LLamaSharp types and XAML objects must not
cross the boundary.

## Remaining non-claims

- no LLamaSharp reference in the WinUI application project;
- no production probe, worker protocol, service, classifier or ViewModel;
- no live page progress or working Cancel button;
- no final model-outcome classification;
- no full tensor checking or model allocation;
- no context, KV cache or token generation;
- no OpenVINO adapter;
- no Vulkan baseline;
- no TurboQuant, PolarQuant or QJL result;
- no Hardware Fit handoff.

## Related documentation

- [Application feature architecture](../README.md)
- [Model Import architecture](../ModelImport/README.md)
- [Onboarding architecture](../Onboarding/README.md)
- [Inspection controls](./Controls/README.md)
- [Inspection presentation models](./Models/README.md)
- [Presentation construction](./Presentation/README.md)
- [LLamaSharp feasibility tool](../../../tools/ModelInspection.LlamaSharpSpike/README.md)
- [Trusted execution runbook](../../../docs/testing/runbooks/LLamaSharp-Trusted-Real-Model-Runbook.md)