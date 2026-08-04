# Core Runtime Progress and Backend Gates Design

**Status:** Approved for implementation  
**Date:** 2026-08-04  
**Target branch:** `feature/model-inspection`  
**Related runtime decision:** [ADR-001](../../architecture/decisions/ADR-001-llamasharp-application-runtime.md)  
**Related feature plan:** `Development Plan for Model Inspection.docx`

## Purpose

Keep the Model Inspection progress display technically honest while the project
validates CPU, Vulkan and TurboQuant in separate engineering gates.

The user-visible Model Inspection screen answers:

> Can the pinned core application runtime safely recognise and inspect this
> model before Hardware Fit is checked?

It does not answer:

> Can this computer run the model through Vulkan, offload it to a GPU, allocate
> the requested context, or activate TurboQuant successfully?

Those backend-specific questions belong to later Hardware Fit and runtime
verification work.

## Decision summary

1. Keep the existing five visible Model Inspection rows.
2. Rename the fifth row from `Confirm runtime support` to
   `Confirm core runtime compatibility`.
3. Drive those five rows later from the lightweight CPU inspection service.
4. Do not add `CPU`, `Vulkan`, `GPU` or `TurboQuant` as rows in the Model
   Inspection tracker.
5. Validate ordinary Vulkan and TurboQuant through separate engineering gates.
6. Add a separate backend-verification user flow only after Hardware Fit has
   selected a suitable backend and configuration.

## User-visible Model Inspection stages

```text
1. Check model package
2. Read model configuration
3. Validate tokenizer and chat setup
4. Validate model structure
5. Confirm core runtime compatibility
```

### Stage meanings

| Visible stage | Internal responsibility |
|---|---|
| Check model package | Validate the request, confirm the selected file exists, preserve the quick-scan safety gate and check file identity |
| Read model configuration | Initialise the matched LLamaSharp CPU runtime and collect lightweight model metadata |
| Validate tokenizer and chat setup | Inspect vocabulary, special-token and chat-template evidence where the runtime exposes it |
| Validate model structure | Inspect architecture, context, parameters and relevant model characteristics, then compare them with quick-scan evidence |
| Confirm core runtime compatibility | Confirm that the pinned application runtime recognises the model and classify whether it may proceed to Hardware Fit |

The final stage deliberately says `core runtime compatibility`. It must not imply
that Vulkan, TurboQuant, GPU offloading, Hardware Fit or full inference has
already passed.

## Progress behavior

- The tracker reports completed stages, for example `2 of 5 checks complete`.
- Only one stage normally has an active `ProgressRing`.
- A determinate fraction is shown only when LLamaSharp supplies genuine native
  progress for the current operation.
- Stages without a meaningful numeric fraction remain indeterminate.
- The initial static presentation contains stage 1 as active and stages 2–5 as
  waiting.
- The page remains presentation-only until the ViewModel/service layer is
  implemented.

## Engineering verification ladder

The development gates remain separate from the five user-visible rows:

```text
Gate 1  Matched LLamaSharp CPU native-library smoke
        ↓
Gate 2  CPU lightweight Granite model inspection
        ↓
Gate 3  Ordinary Vulkan baseline
        ↓
Gate 4  TurboQuant fork CPU correctness baseline
        ↓
Gate 5  TurboQuant fork Vulkan acceleration
        ↓
Gate 6  LLamaSharp/custom TurboQuant backend compatibility
        ↓
Gate 7  Production inspection and inference integration
```

### Gate 1 — matched CPU native-library smoke

Proves managed/native package compatibility and backend discovery. It does not
load a model.

### Gate 2 — CPU lightweight model inspection

Proves metadata, tokenizer/chat-template evidence, progress, cancellation,
disposal and file integrity without introducing GPU-driver variability.

### Gate 3 — ordinary Vulkan baseline

Proves that a normal matched Vulkan backend can initialise, select the intended
device, load a standard model, offload layers and run a small context/inference
check.

### Gate 4 — TurboQuant CPU correctness baseline

Proves the selected fork and TurboQuant formats through the CPU reference path
before adding Vulkan-specific kernels.

### Gate 5 — TurboQuant Vulkan acceleration

Proves that the selected fork's Vulkan path genuinely activates TurboQuant,
uses the intended GPU, reduces KV memory in the expected direction and does not
silently fall back to CPU or an ordinary KV format.

### Gate 6 — LLamaSharp/custom backend compatibility

Proves that the managed LLamaSharp binding can call the exact custom native
TurboQuant build, or records the need for a controlled extension or worker
process.

## TurboQuant terminology

The design distinguishes:

```text
TurboQuant KV-cache compression
    → applied dynamically while a context is running
    → backend kernels may compress, store, read and dequantise KV data

TurboQuant weight quantisation
    → performed as a model-preparation step
    → Vulkan later executes the already-quantised weights
```

The project's primary first use remains conventional GGUF weights with
TurboQuant focused on KV-cache compression. Under that plan, the lightweight
Model Inspection page does not need a TurboQuant-aware GPU backend.

## Future backend-verification flow

After Model Inspection returns `Ready for hardware analysis`, a later feature
may show a separate sequence such as:

```text
1. Select compatible backend
2. Initialise Vulkan device
3. Load and offload model
4. Allocate context and KV cache
5. Activate selected TurboQuant format
6. Run a small inference check
7. Confirm GPU use and absence of silent fallback
```

This flow must not be squeezed into the current Model Inspection progress card.
It answers a different question and depends on Hardware Fit results.

## Source structure for this slice

```text
Features/ModelInspection/
├── ModelInspectionPage.xaml.cs
└── Presentation/
    ├── README.md
    └── InitialInspectionProgressPresentationFactory.cs

tests/UnitTests/GraniteEdgeAI.UnitTests/
└── Features/ModelInspection/
    └── InitialInspectionProgressPresentationTests.cs
```

The new factory owns only the initial progress presentation. It does not own
runtime execution, stage mutation, outcome classification or navigation.

## Testing requirements

The tests must prove:

- exactly five stages exist;
- titles and order match this design;
- the final title is `Confirm core runtime compatibility`;
- no stage title mentions Vulkan, TurboQuant, GPU or Hardware Fit;
- only the first stage is active initially;
- the final stage has no connector;
- the summary remains `0 of 5 checks complete`.

## Documentation requirements

Update:

- Model Inspection README;
- application Features README;
- ADR index;
- Model Inspection folder tree and child links;
- implementation plan status.

## Non-goals

This slice does not:

- run the existing LLamaSharp smoke command;
- load a Granite model;
- add the Vulkan backend package;
- build a TurboQuant fork;
- add a Hardware Fit page;
- add dynamic stage progression;
- add the ModelInspectionViewModel or service;
- claim CPU, Vulkan or TurboQuant verification success.
