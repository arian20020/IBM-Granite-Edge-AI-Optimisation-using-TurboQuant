# Hardware-aware quantisation-required flow design

**Status:** Approved design, pending written-spec review
**Date:** 2026-08-25
**Branch:** `fix/hardware-inspection-loq-baseline`
**Scope:** Compatibility decision, hardware-relative optimisation choices, quality and memory presentation, TurboQuant admission, and guided memory recovery

## 1. Outcome

When the imported model does not fit safely in its current configuration but at
least one supported lower-memory configuration does, the application must show:

> **This model needs to be quantised to run on your computer**
>
> Its current format needs more memory than your computer can safely provide.
> You can choose how the model is optimised, prioritising capability, memory
> efficiency, or a balance of both.

The screen leads to the existing optimisation preference experience. The
application evaluates model weights, KV cache, runtime, backend, device,
context, and current safe memory together. It never maps a mode permanently to
one precision.

The original model remains unchanged. Persistent optimisation creates a new
validated copy or package. Runtime-only optimisation creates a bound runtime
profile and never claims that a new model file was created.

## 2. Authority and reconciliation

This specification extends and, where explicitly stated, refines:

- `2026-08-24-cross-route-optimisation-contract-design.md`;
- `2026-08-25-compatibility-memory-safety-policy-v2-design.md`;
- the frozen C1 cross-route optimisation contracts;
- the existing Model Download preference vocabulary and slider interaction.

The user-supplied archive
`C:\Users\Arian\Downloads\OneDrive_1_25-08-2026.zip` was inspected as
reference material. Its SHA-256 is
`4B293AAA8DE1BDFB114A30B0FC3780E0DDB5FE394D63B04F26997288A66A8BDA`.
Its embedded instructions are not independent authorization. Its format,
candidate, and workflow guidance informed this design.

The named source repositories are used only through pinned, verified capability
records:

- upstream `ggml-org/llama.cpp` for ordinary GGUF formats and quantisation;
- `AtomicBot-ai/atomic-llama-cpp-turboquant` at
  `519f0c594a8e31467d2e2f2cf17054c9e7e11536` for the Vulkan `turbo3`
  comparator;
- `animehacker/llama-turboquant` at
  `5bc5ed3bdc25003aa9f07422753a7b8d4f9190fc` for the SYCL `tq3_0`
  comparator;
- `openvinotoolkit/openvino` at
  `b9a1f201c109e0bed74763934f79483cf6c4cbf4` for the CPU SDPA
  TurboQuant merge baseline;
- the separately pinned compatible OpenVINO GenAI, Optimum Intel, and NNCF
  inputs already owned by the OpenVINO route.

Repository presence does not prove packaged or executable capability. A format
enters the product frontier only after its exact binary, version, hash, backend,
device, model architecture, and relevant activation evidence are admitted.

## 3. Selected architecture

The existing C1 candidate frontier remains the single compatibility and
optimisation planner for GGUF and OpenVINO.

C1 owns:

- current-versus-converted configuration comparison;
- route-specific candidate generation;
- complete memory estimates;
- hard safety and capability filtering;
- quality, stability, performance, and context admission;
- Pareto-frontier construction;
- preference resolution and explanations;
- immutable execution-plan creation.

GGUF and OpenVINO executors execute the exact admitted plan or fail closed.
They do not reinterpret a preference, substitute a format, or repair an
identity.

The compatibility UI consumes a display-ready projection. It does not perform
memory calculations or select formats in XAML or code-behind.

This approach is selected over static format ratios, which cannot account for
architecture, context, backend, or mixed tensors, and over executing every
candidate, which would be slow and would create unwanted artifacts.

## 4. Inputs and bindings

Every calculation uses one coherent, identity-bound input set:

- Model Inspection run and handoff identities;
- model SHA-256 and validated byte length;
- route and current model representation;
- model architecture, parameter count, tensor distribution, layers, attention
  metadata, recurrent or hybrid state, and trained context limit where known;
- requested and minimum acceptable context;
- expected prompt and generation length;
- one-chat-session workload for this release;
- Hardware Inspection run, handoff, snapshot identity, and snapshot hash;
- fresh available physical memory;
- dedicated and shared graphics memory without double counting;
- CPU, GPU, NPU, backend, driver, runtime, and tool capability evidence;
- available disk workspace and final-output capacity;
- capability snapshot identity and digest.

Unknown mandatory values fail closed. A stale model, hardware, or capability
binding produces `ReplanRequired`; it is not silently reused.

## 5. Complete candidate model

A candidate is a complete runnable configuration, not a precision label. It
contains:

- route;
- weight representation and source provenance;
- persistent or runtime-only disposition;
- key and value cache representation;
- backend and target device;
- GPU offload or partitioning policy;
- context and workload;
- Flash Attention state when relevant;
- thread, stream, batch, and token-limit controls that affect fit;
- model-weight, KV-cache, model-state, compute-buffer, runtime-overhead,
  staging, and uncertainty estimates;
- safe memory budget, predicted peak, shortage or headroom;
- disk workspace and predicted output size;
- quality, performance, stability, context, and evidence assessments;
- experimental status, limitations, and fallback;
- canonical complete-configuration digest.

Weight compression and KV-cache compression are distinct dimensions.
TurboQuant is not presented as an ordinary GGUF weight precision.

## 6. Route format matrix

### 6.1 GGUF weights

The admitted candidate ladder is:

1. current imported representation;
2. BF16 or F16 only from a genuine source of that precision;
3. Q8_0;
4. Q6_K;
5. Q5_K_M;
6. Q4_K_M;
7. Q3_K_M;
8. Q2_K as a controlled low-memory extension.

The planner never describes conversion from a lower precision to a higher one
as quality restoration. A candidate requiring a higher-quality source is
excluded when that source is unavailable.

Q2_K is the current product floor. It is considered only when its exact
architecture/tool combination is admitted and a less destructive candidate
does not provide the mode's meaningful benefit. It is most likely on extremely
constrained systems and carries the strongest quality warning.

### 6.2 GGUF runtime cache

The initial cache candidates are:

- F16;
- Q8_0;
- TurboQuant 3-bit.

TurboQuant 3-bit has backend-specific implementations:

- Intel SYCL `tq3_0` from the pinned animehacker comparator;
- Intel Vulkan `turbo3` from the pinned AtomicBot comparator;
- CPU only where a separate exact capability record admits it.

They share one user-facing name but remain different internal configurations.
The product must not imply that a CPU-resident cache or one-layer partial
offload is a fully GPU-resident TurboQuant path.

### 6.3 OpenVINO weights

The admitted candidate ladder is:

1. current imported representation;
2. FP16 from a genuine source;
3. INT8;
4. INT4.

An already-compressed OpenVINO package cannot be converted back to genuine
original-quality FP16 without the genuine higher-precision source.

### 6.4 OpenVINO runtime cache

The initial standard candidates are:

- F16;
- BF16;
- U8;
- U4 where the exact selected device supports it.

The initial experimental candidates are:

- symmetric CPU SDPA TurboQuant 4-bit (`TURBO/u4` for key and value);
- symmetric CPU SDPA TurboQuant 3-bit (`TURBO/u3` for key and value).

TBQ4 is considered before TBQ3 when both provide the required benefit. TBQ3 is
selected only when TBQ4 does not provide sufficient headroom, the added saving
is meaningful, and quality, stability, performance, and context thresholds
still pass.

TurboQuant activation requires agreement between requested algorithm and
precision, runtime property evidence, actual dispatch evidence, packed-storage
evidence, absence of silent fallback, and the pinned build identity. `u3` or
`u4` alone does not prove TurboQuant.

The explicit fallback order is:

`TBQ3 -> TBQ4 -> standard U4 -> U8 -> F16/BF16 where safe`

If the custom TurboQuant worker or build is unavailable, planning continues
through the official OpenVINO route without claiming TurboQuant.

### 6.5 Required C1 OpenVINO contract correction

The current C1 planning types contain a semantic defect: `TurboQuantTbq4` and
`TurboQuantTbq3` are members of `OpenVinoWeightFormat`, while
`OpenVinoKvCacheFormat` has no TurboQuant members. The current execution payload
also exposes only `ReleasedDefault` and `U8` cache precision. That shape cannot
represent the approved format matrix truthfully.

The implementation must correct the contract before adding these candidates:

- OpenVINO weights remain `Original`, `Fp16`, `Int8`, and `Int4` only;
- OpenVINO cache selection gains standard `U4` plus distinct TurboQuant TBQ4
  and TBQ3 cache values;
- the execution payload records the cache algorithm and precision required to
  distinguish standard U4 from `TURBO/u4`;
- symmetric TurboQuant records bind both key and value selections explicitly;
- TurboQuant build identity is required for TurboQuant cache selections and
  forbidden for standard selections;
- canonical configuration hashing includes the corrected weight, key-cache,
  value-cache, algorithm, backend, device, and build fields.

This is a versioned contract correction, not an in-place reinterpretation.
Existing version-2 plans and digests remain reproducible. Corrected plans use a
new schema/version identity and are reissued; no executor may accept an old
digest under the new semantics.

### 6.6 Deferred formats

The following remain outside this increment unless separately admitted and
planned: Q1/IQ1 GGUF weights, TurboQuant 2-bit cache, asymmetric key/value
formats, QJL, PolarQuant, mixed experimental cache algorithms, unverified
Vulkan TBQ4, TurboQuant GPU/NPU OpenVINO execution, cache export/restoration,
and unsupported Granite architectures.

## 7. Controlled requantisation

The preferred persistent GGUF conversion source is F16 or BF16. If only an
already-quantised GGUF exists and the current model cannot fit, the product may
offer further quantisation under these conditions:

- the exact locally verified quantiser supports the source and target;
- the target is strictly lower in the admitted ladder;
- `--allow-requantize` or its pinned equivalent is used only through the closed
  executor contract;
- the user explicitly acknowledges the quality warning;
- the candidate's quality evidence is downgraded appropriately;
- the output is a new file and the source is opened read-only;
- no text claims that quality is restored or guaranteed.

The warning states that further quantising an already-quantised model may
noticeably reduce response accuracy, detail, and consistency. Q2_K carries the
strongest version of this warning.

## 8. Memory estimation and safety

Predicted peak includes:

- encoded model weights and backend alignment or duplication;
- KV cache using real encoded block sizes;
- recurrent, Mamba, hybrid, or other model state not represented by the normal
  KV-cache equation;
- compute and activation buffers;
- runtime and backend allocations;
- CPU/GPU staging and offload duplication;
- application-owned inference overhead;
- conversion transient memory where applicable;
- estimator uncertainty.

The corrected production safety policy v2 is authoritative:

`safe budget = max(0, fresh available physical memory - max(10%, 512 MiB))`

The estimator separately adds its uncertainty margin to predicted peak.
Current available memory already excludes memory used by Windows and running
applications; fixed OS and application allowances are not subtracted again.

Integrated-GPU shared memory remains part of the system-memory pool and is not
added as independent VRAM.

All modes use fresh available memory. Planning rechecks it before confirmation,
and the executor rechecks it immediately before execution.

## 9. Candidate admission and frontier

The planner generates all complete combinations supported by the exact route
capability snapshot. It rejects any candidate that:

- exceeds safe system or device memory;
- lacks a mandatory estimate;
- cannot preserve minimum context;
- is unsupported by the model, backend, device, runtime, or tool;
- requires unavailable source precision;
- falls below the accepted quality, stability, or usable-performance floor;
- lacks required evidence;
- exceeds disk workspace or publication limits.

It removes candidates dominated on the user-relevant quality and memory axes,
using context, performance, stability, evidence, persistence, and device fit as
deterministic tie-breakers.

The planner does not invent artificial distinctions to force every preference
to resolve differently. Adjacent positions may select the same candidate when
no meaningful alternative exists.

## 10. Hardware-relative preference semantics

The existing Model Download vocabulary remains exact:

| Slider | Mode | Quality statement |
|---:|---|---|
| 0-19 | Maximum efficiency | Lowest acceptable estimated quality |
| 20-39 | Efficient | Reduced estimated quality |
| 40-59 | Balanced | Good estimated quality |
| 60-79 | High capability | Very high estimated quality |
| 80-100 | Maximum capability | Highest available estimated quality |

`Automatic` remains separate and recommended by default.

Modes define objectives, not fixed memory percentages or fixed formats:

- **Maximum capability:** select the highest-quality complete configuration
  that fits safely.
- **High capability:** retain near-maximum quality while requiring a meaningful
  memory, context, performance, or device benefit over the maximum-capability
  point.
- **Balanced:** select the strongest supported knee across quality, memory
  headroom, context, performance, stability, and persistence cost.
- **Efficient:** require substantial practical memory or context benefit while
  retaining good estimated quality and all hard floors.
- **Maximum efficiency:** select the lowest-memory practical candidate that
  passes the minimum quality, stability, performance, and context floors, but
  do not apply additional degradation with no meaningful benefit.
- **Automatic:** select the least-destructive strongest overall result from the
  same frontier, penalising unnecessary persistent conversion.

Therefore Maximum efficiency may resolve to Q8, Q4, Q3, Q2, a standard
compressed cache, or TurboQuant depending on model size, current format,
available RAM, requested context, backend, device, and admitted evidence.

Quality and memory efficiency must remain monotonic across the ordered slider.
Equality is permitted between adjacent bands.

## 11. Quality evidence

Every mode shows an explicit quality statement alongside its actual selected
configuration.

Quality is labelled `Estimated` unless the exact model, source, output format,
cache, backend, context, workload, tool/build, and evaluation protocol have
accepted measured evidence. The product does not fabricate percentages.

When measured evidence exists, the completed optimisation result may show a
measured score and methodology reference in technical details. It does not
silently compare scores produced by different rubrics.

Candidate quality ranking accounts separately for weight conversion and cache
compression. The planner does not assume that preserving weights with a very
aggressive cache is always better than moderately compressed weights with a
higher-quality cache; it compares the complete configuration using available
evidence.

## 12. Compatibility decision

The projector evaluates the current baseline separately from converted or
runtime-optimised candidates:

- current configuration safely fits: `EstimatedCompatible` or the appropriate
  ready/recommendation state;
- current configuration does not fit but at least one admitted alternative
  does: `OptimisationRequired`;
- no admitted complete configuration fits: `NoEstimatedSafeConfiguration`;
- mandatory evidence is missing, stale, or contradictory: fail-closed
  unavailable/reinspection state.

This replaces any implementation that reports `EstimatedCompatible` merely
because a converted candidate fits.

The optimisation-required projection includes:

- current model format and route;
- current predicted peak;
- current safe budget and shortage;
- primary limiting factor: weights, KV cache/model state, both, backend, or
  evidence;
- number of safe configurations found;
- highest-quality safe complete option;
- whether requested context is preserved;
- maximum estimated safe context where known;
- estimate/evidence labels and limitations.

## 13. User experience

### 13.1 Optimisation-required screen

The approved visual hierarchy is:

1. centred product heading and concise explanation;
2. amber optimisation-required banner;
3. current-versus-recommended comparison;
4. feature introduction explaining that the user controls the quality and
   efficiency balance;
5. the established optimisation slider and exact labels;
6. live selected-format, quality, peak-memory, and expected-headroom summary;
7. quality/requantisation warning when applicable;
8. `Choose another model` and `Continue to optimisation` actions.

Primary copy:

> **This model needs to be quantised to run on your computer**
>
> Its current format needs more memory than your computer can safely provide.
> You can choose how the model is optimised, prioritising capability, memory
> efficiency, or a balance of both. We only offer configurations estimated to
> run safely on this computer.

The page may truthfully recommend a runtime-only cache change instead of a new
weight file when that is the highest-quality solution. In that case, the copy
uses `optimised` rather than falsely claiming that model weights will be
rewritten.

### 13.2 Live mode information

Every mode shows:

- exact selected weight representation;
- exact cache representation in user-readable form;
- backend and device;
- context;
- estimated peak memory;
- current safe budget and predicted remaining headroom;
- quality statement and `Estimated` or `Measured` grade;
- whether a new model copy will be created;
- recommendation and experimental status;
- limitations and relevant quality warning.

### 13.3 Existing visual system

The production XAML uses the established modern light Model Import, Model
Inspection, Hardware Inspection, and onboarding visual family. It reuses the
existing slider interaction and tokens. Cards, spacing, typography, symbols,
statuses, disclosures, buttons, responsive widths, 200% text, High Contrast,
keyboard, and screen-reader behaviour remain consistent in every state.

No dark compatibility island, raw developer grid, or precision-first screen is
introduced.

## 14. Guided memory recovery

The compatibility page provides `Free up memory` beside `Check again` when
memory is the limiting factor.

For this release it:

- releases only Granite Edge AI's own disposable previews, caches, and
  reconstructible resources;
- states how much additional safe memory is estimated to be needed;
- explains that unsaved work should be preserved before closing applications;
- offers `Open Task Manager` with guidance to close unused applications and
  browser tabs;
- provides `Check memory again` after the user returns;
- recomputes Hardware Inspection evidence and replans candidates.

It never automatically kills, suspends, or closes another process. It never
claims Windows standby/cache memory is unusable merely because it is not shown
as completely free.

User-selected graceful closing of third-party applications is deferred.

## 15. Revalidation and execution handoff

Before optimisation begins, the application revalidates:

- source model identity and length;
- current Model Inspection and Hardware Inspection bindings;
- fresh memory and disk capacity;
- selected route and capability snapshot;
- exact quantiser/converter/runtime binary identity;
- selected complete configuration and canonical digest;
- warning acknowledgement where requantisation is involved.

Any mismatch produces `ReplanRequired`. The executor receives the immutable C1
plan and cannot select another candidate.

Persistent output uses operation-owned staging, validation, smoke testing,
reinspection, and atomic publication. Cancellation or failure publishes
nothing, removes incomplete output under the owned-cleanup policy, and leaves
the original unchanged.

## 16. Privacy and security

- Paths, raw tool output, host identity, credentials, model content, and
  unbounded diagnostics do not enter product UI or navigation.
- Tool arguments use closed allowlists; no free-form shell is constructed.
- Tool and runtime binaries are versioned and hash-verified.
- Model, hardware, capability, plan, and output identities are recomputed at
  the boundaries where they are relied upon.
- Experimental TurboQuant never silently falls back while retaining a
  TurboQuant label.
- The UI exposes safe summaries and bounded support codes only.

## 17. Failure states

- Missing or stale model/hardware evidence: request reinspection.
- Unsupported architecture or source/target pair: remove the candidate.
- Required tool absent or unverified: show the option as unavailable or omit
  it; do not claim capability.
- No safe candidate: explain that the model cannot run safely and offer a
  smaller model or shorter context.
- Memory drops before execution: pause and offer memory recovery, replanning,
  or a more efficient mode.
- Quantisation/conversion fails: publish nothing and return a privacy-safe
  recovery result.
- Quality/stability/performance validation fails: reject the candidate and
  replan; never continue because its memory estimate was attractive.
- TurboQuant activation cannot be proved: reject the TurboQuant candidate and
  use the explicit standard fallback frontier.

## 18. Verification

### 18.1 Decision and candidate tests

- Current format fits produces the ready state.
- Current format fails while an alternative fits produces
  `OptimisationRequired`.
- No alternative fits produces `NoEstimatedSafeConfiguration`.
- Unknown or stale evidence fails closed.
- GGUF and OpenVINO candidate generation remain route-separated.
- Complete configurations include weights, cache, backend, device, offload,
  context, and material runtime settings.
- Integrated-GPU shared memory is not double counted.
- Every predicted peak includes required components and uncertainty.

### 18.2 Format and mode tests

- The GGUF ladder includes Q2_K only as the controlled floor.
- Requantisation requires lower target, verified tool, acknowledgement, and
  separate output.
- OpenVINO weight and cache ladders match admitted device capability.
- TurboQuant cache candidates require exact build and activation evidence.
- TBQ3 falls back through TBQ4 and standard formats without silent relabelling.
- Every mode may resolve to different formats across hardware and context
  fixtures.
- No mode is hard-coded to one precision.
- Unnecessary degradation with no meaningful benefit is rejected.
- Quality and efficiency monotonicity holds, allowing adjacent equality.
- Exact Model Download labels and ranges remain unchanged.

### 18.3 Quality tests

- Every mode has a visible quality statement.
- Unevaluated results are labelled `Estimated`.
- `Measured` requires exact matched evaluation evidence.
- Q2 and requantisation warnings are prominent and specific.
- No inferred quality restoration is claimed.

### 18.4 Memory-recovery tests

- Only application-owned disposable resources are released.
- No API path terminates or closes another process.
- Task Manager guidance and memory-shortage copy are present.
- `Check memory again` obtains fresh evidence and issues a new plan when inputs
  change.

### 18.5 Native flow

A packaged x64 walkthrough must cover:

`Model Import -> Model Inspection -> Hardware Inspection -> Compatibility ->`
`Optimisation required -> Mode selection`

It uses an actual GGUF model and representative OpenVINO package. Low-, medium-,
and high-memory fixtures cover weight-limited, cache-limited, combined, and
no-fit outcomes. Native review covers compact, standard, wide, 200% text, High
Contrast, keyboard, focus, and screen-reader states.

## 19. Scope boundaries

This increment ends when the compatibility decision and mode-selection handoff
are correct, integrated, visually approved, and verified. It does not itself
complete quantiser/converter execution, the optimisation progress lifecycle,
or the later Save-or-Chat destination screen, although it emits the exact
immutable plan those stages require.

No implementation may claim Q1/IQ1, TurboQuant 2-bit, QJL, PolarQuant,
asymmetric cache, or unverified GPU/NPU TurboQuant support.

## 20. Acceptance

The design is accepted when:

- a current model that fails but has a safe alternative is described as
  requiring optimisation rather than as already compatible;
- format selection uses one dynamic complete-configuration frontier across
  GGUF and OpenVINO;
- Q2_K is available only as the controlled low-memory GGUF floor;
- GGUF TurboQuant 3-bit and OpenVINO TBQ4/TBQ3 are included only with exact
  capability and activation evidence;
- every established mode shows its actual hardware-relative format, memory,
  headroom, and quality statement;
- the original model remains unchanged;
- guided memory recovery never terminates another application;
- all estimates and measurements are labelled truthfully;
- the screen matches the approved modern light product template;
- stale evidence, unsupported formats, and failed validation all fail closed.
