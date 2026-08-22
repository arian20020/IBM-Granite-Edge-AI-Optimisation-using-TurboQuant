# ADR-002: Separate core model inspection from backend verification

**Status:** Accepted  
**Date:** 2026-08-04  
**Decision owner:** Project team  
**Affected features:** Model Inspection, Hardware Fit, runtime configuration and later GGUF inference  
**Related decision:** [ADR-001](ADR-001-llamasharp-application-runtime.md)  
**Related design:** [Core Runtime Progress and Backend Gates](../../superpowers/specs/2026-08-04-core-runtime-progress-and-backend-gates-design.md)

## Context

The project must validate several different things:

1. whether the managed application can load its matched native runtime;
2. whether the core runtime can recognise and inspect a selected GGUF model;
3. whether a Vulkan backend can initialise and use the intended GPU;
4. whether the selected TurboQuant fork works correctly on CPU;
5. whether TurboQuant works correctly and beneficially through Vulkan;
6. whether LLamaSharp can call the exact custom TurboQuant native build.

Those are related, but they are not one check.

The Model Inspection page appears before Hardware Fit in the user journey. At
that point the application has not yet selected a suitable device/backend,
calculated memory fit, allocated the requested context, or proved GPU offload.
A generic label such as `Confirm runtime support` could therefore be read as a
claim that Vulkan, GPU offload and TurboQuant are already supported.

The current screen already uses five visible progress rows. The project also
needs a development test ladder that isolates ordinary CPU, ordinary Vulkan and
TurboQuant failures so one problem is not misdiagnosed as another.

## Decision

1. Model Inspection validates **lightweight core runtime compatibility** before
   Hardware Fit.
2. The current five visible rows remain:

   ```text
   1. Check model package
   2. Read model configuration
   3. Validate tokenizer and chat setup
   4. Validate model structure
   5. Confirm core runtime compatibility
   ```

3. The fifth row is renamed from `Confirm runtime support` to
   `Confirm core runtime compatibility`.
4. CPU, Vulkan, GPU, Hardware Fit and TurboQuant are not added as rows in the
   Model Inspection progress card.
5. The lightweight CPU probe will eventually drive the five current rows.
6. Backend-specific verification occurs after core inspection and Hardware Fit
   have selected a candidate backend and configuration.
7. Engineering verification follows this sequence:

   ```text
   matched LLamaSharp CPU smoke
       ↓
   CPU lightweight Granite inspection
       ↓
   ordinary Vulkan baseline
       ↓
   TurboQuant fork CPU correctness
       ↓
   TurboQuant fork Vulkan acceleration
       ↓
   LLamaSharp/custom TurboQuant backend compatibility
       ↓
   production integration
   ```

8. A later backend-verification screen or workflow may display Vulkan,
   offloading, context allocation and TurboQuant activation progress. That work
   must not be silently folded into the current Model Inspection tracker.

## Why this decision was selected

### Honest user-facing claims

`Ready for hardware analysis` means the model passed package, metadata,
tokenizer, structure and core-runtime checks. It does not mean the current
computer can run the model efficiently or that a particular GPU backend works.

### Failure isolation

The gate order distinguishes:

```text
managed/native loading failure
ordinary Vulkan or driver failure
TurboQuant fork correctness failure
TurboQuant Vulkan kernel failure
managed/custom-backend compatibility failure
```

Combining those in one early test would make diagnosis and evidence weak.

### Stable presentation architecture

The existing page and card structure can stay stable while future service and
ViewModel layers supply real progress. Backend selection can evolve without
changing the meaning of the five Model Inspection rows.

### Proportionate pre-Hardware-Fit work

The development plan requires lightweight inspection before full model
allocation. A CPU/core-runtime probe is the simplest baseline because it avoids
introducing GPU-driver, shader and offload variability before the model itself
has been understood.

## Alternatives considered

### Add CPU, Vulkan and TurboQuant rows to Model Inspection

Rejected because they are engineering/runtime-configuration gates rather than
model-package checks. It would also imply that every user must run every
backend and would make the screen dependent on hardware that has not yet been
analysed.

### Replace the CPU gate with Vulkan immediately

Rejected because a failure could come from LLamaSharp/native versioning, the
Vulkan loader, drivers, device selection, model support, offloading or
TurboQuant. The CPU gate intentionally proves the managed/native and core-model
boundaries first.

### Treat a successful ordinary Vulkan run as TurboQuant proof

Rejected because the official Vulkan backend does not automatically contain the
selected TurboQuant fork's formats and kernels. Ordinary Vulkan and TurboQuant
Vulkan require separate evidence.

## Consequences

### Positive

- the Model Inspection wording remains accurate;
- existing Figma/card geometry is preserved;
- backend implementation can change behind later adapters;
- Vulkan and TurboQuant evidence is collected at the correct depth;
- failures can be classified at the first divergent gate;
- the same core result can feed multiple future backend choices.

### Costs and limitations

- a second backend-verification flow must be designed later;
- the initial Model Inspection result cannot claim GPU readiness;
- the project needs separate evidence campaigns for ordinary Vulkan and
  TurboQuant Vulkan;
- if TurboQuant-specific weight formats are imported in the future, the core
  inspection runtime boundary may require review because an ordinary runtime
  may not recognise those weights.

## Progress semantics

The page reports stage completion, not invented timing:

```text
0 of 5 checks complete
1 of 5 checks complete
...
5 of 5 checks complete
```

Only one stage normally animates. A numeric fraction may be displayed only when
the runtime reports a genuine fraction for the active operation.

The initial stage presentation remains:

```text
Stage 1  Active / Checking
Stages 2–5  Waiting
```

## Review triggers

Review or supersede this ADR when:

- the user journey moves Hardware Fit before Model Inspection;
- Model Inspection begins creating a full inference context;
- a backend-specific model format must be recognised before Hardware Fit;
- TurboQuant-specific weights become a primary import path;
- Vulkan or another GPU backend becomes mandatory for all supported models;
- the application no longer separates core inspection from backend selection;
- the visible progress stages or outcome wording changes.

## Non-claims

This ADR does not claim that:

- the LLamaSharp CPU smoke has passed;
- a Granite GGUF has been inspected through LLamaSharp;
- Vulkan has been initialised on the target hardware;
- TurboQuant works on CPU or Vulkan;
- GPU offloading, context allocation or inference has passed;
- a backend-verification page already exists.
