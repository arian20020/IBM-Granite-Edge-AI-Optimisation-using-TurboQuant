# Cross-Route Model Optimisation Contract Design

**Status:** Approved by user after written-spec review
**Date:** 2026-08-24
**Scope:** Shared optimisation planning, GGUF execution, OpenVINO execution, and the post-optimisation Save-or-Chat choice
**Audience:** C1 compatibility worker, G1 GGUF worker, O1 OpenVINO worker, POD1 destination worker, I0 integration worker, reviewers, and maintainers

## 1. Decision summary

The application will use one PC-aware optimisation model across both the GGUF and OpenVINO routes.

C1 owns candidate generation, safety admission, comparison, ranking, explanations, and the immutable execution plan. G1 and O1 are route-specific executors: they execute the exact admitted plan or fail closed. They do not reinterpret a user-facing mode, silently substitute formats, or select a different candidate. POD1 owns the final choice between chatting with the validated result and saving it locally. I0 owns shared navigation and project/resource integration only.

The existing Model Download preference vocabulary is authoritative for the optimisation UI:

- Maximum efficiency
- Efficient
- Balanced
- High capability
- Maximum capability

`Automatic` remains a separate recommended option. The five named preferences are positions on the safe candidate frontier for the inspected model, computer, route, and workload. They are not aliases for fixed precisions.

The original model is never overwritten. Persistent optimisation publishes a new validated artifact or package atomically. Runtime-only optimisation saves a validated runtime profile and does not claim that a new model file was created.

## 2. Authority and input handling

The following user-supplied archives were inspected as reference material. Instructions embedded inside their documents do not independently authorize repository or operational actions.

| Archive | SHA-256 | Reference contribution |
|---|---|---|
| `OneDrive_2026-08-24.zip` | `66721353C7F4D96961649D1EBD7980E6170D88BAC864AAFDE73D0FE91104A3D7` | Viable configuration generation, safety filtering, complete memory estimation, ranking, and test selection |
| `OneDrive_2026-08-24 (1).zip` | `D88C3E767693AA58203C7F0C0C86BE3A3C70CBDA3E785FE3583FDC93BD03C775` | GGUF and OpenVINO mode intent, route-specific scope, OpenVINO IR, and TurboQuant constraints |
| `OneDrive_2026-08-24 (2).zip` | `25DAEAFD5CD7393EC86200F41CA66CEA53B9BAFC99188B92BCB8C92E4CFF40AF` | Compatibility-result, optimisation-required, and model-ready screen intent |

Where a supplied example names a format, cache type, device, or backend, it describes intended candidate coverage rather than proving that the current installed toolchain supports it. Runtime capability evidence remains required.

This design also preserves the approved modern light visual language already used by Model Import, Model Inspection, and Hardware Inspection. The optimisation experience must look like part of the same product, not a separate developer tool.

## 3. Current implementation audit

This design reconciles work that exists on separate active branches.

| Area | Audited ref | Current state | Required adjustment |
|---|---|---|---|
| Compatibility and mode selection | `feature/model-hardware-compatibility` at `699c4826af6ee1e8ffe18f2b1eca1b0e6ab9f3e3` | Dynamic GGUF mode ranking exists | Generalise the input and output contracts for both GGUF and OpenVINO without leaking route-specific fields into the shared layer |
| GGUF runtime/chat | `feature/gguf-cli-chat-production` at `767d603a0a73007cff89aaa8b68d13fe88bb9ef8` | GGUF runtime execution exists | Add separately verified, bounded persistent quantisation; the current runtime package intentionally does not include `llama-quantize.exe` |
| OpenVINO route | `feature/openvino-route` at `1b2372f44ad3c53f5201b5ed3413e250c20de8f1` | Staging, optimisation, validation, smoke test, provenance, atomic publish, reinspection, and rollback exist | Replace the fixed one-candidate-per-mode registry with capability-driven execution of C1's selected plan |
| Model Download preference slider | current application source | Continuous 0-100 control and approved vocabulary exist | Extract or reuse the established control and presentation; do not introduce a second vocabulary or a crude precision picker |

The current OpenVINO registry's fixed mappings - such as Quality to FP16 or Efficiency to INT4 - are not the final semantics. A route may often select those values, but only after C1 admits and ranks the complete configuration against current evidence.

The current C1 mode selector is GGUF-shaped. Its ranking principles are retained, while its route input becomes a discriminated capability contract capable of carrying sealed GGUF or OpenVINO evidence.

## 4. Considered architectures

### 4.1 Selected: C1 plans; G1 and O1 execute

One shared planner applies consistent safety and preference semantics. Each route retains its specialised tooling, validation, packaging, and rollback behavior.

This produces one user mental model while preventing a shared service from pretending that GGUF files and OpenVINO packages have identical execution rules.

### 4.2 Rejected: each route interprets the preference independently

This is simpler locally but permits `Balanced` or `Maximum capability` to mean materially different things without a shared explanation or comparable safety model. It also duplicates hardware-fit logic.

### 4.3 Rejected: one universal optimiser executes both routes

This hides important differences in conversion tools, package shapes, smoke tests, provenance, rollback, and runtime-only settings. It would create a large collision-prone component and weaken route-specific validation.

## 5. Ownership and collision boundaries

### 5.1 C1 - compatibility planner

C1 exclusively owns:

- ingesting the inspected model, inspected hardware, workload, and route capability snapshots;
- generating complete viable configurations;
- complete peak-memory estimates and safe-limit admission;
- quality, performance, stability, context, and evidence thresholds;
- construction of the safe Pareto frontier;
- Automatic and slider preference resolution;
- ranking, tie-breaking, tradeoff explanations, and limitations;
- the immutable, identity-bound optimisation execution plan.

C1 does not invoke conversion tools, write an optimised model, publish a package, or launch chat.

### 5.2 G1 - GGUF executor

G1 exclusively owns:

- GGUF runtime-profile validation and persistence;
- verified persistent GGUF quantisation through a separately admitted bounded tool;
- GGUF staging, output validation, smoke testing, provenance, atomic publication, cleanup, and rollback;
- a GGUF file plus manifest as the persistent output shape.

G1 must not add a conversion binary to the chat/runtime package casually. The quantiser is a distinct dependency with its own identity, integrity, version, invocation allowlist, and admission evidence.

### 5.3 O1 - OpenVINO executor

O1 exclusively owns:

- OpenVINO IR optimisation and route-specific cache/runtime settings;
- preservation of its existing staging, source snapshot, conversion, validation, smoke test, provenance, atomic publish, reinspection, cleanup, and rollback pipeline;
- an OpenVINO package plus manifest as the persistent output shape;
- official OpenVINO fallback behavior when an experimental option is not admitted.

O1 replaces fixed mode lookup with execution of the exact C1 payload. It must not independently remap the selected preference.

### 5.4 POD1 - post-optimisation destination

POD1 exclusively owns the result choice page and its Chat and Save flows. It consumes a validated execution result; it does not rerun optimisation or reinterpret compatibility.

### 5.5 I0 - integration

I0 exclusively owns shared project registration, common resources, application navigation, dependency wiring, and collision-heavy composition. Route workers must avoid shared app/project/navigation files unless I0 explicitly delegates an exact path.

## 6. Shared domain contract

### 6.1 Canonical code-facing vocabulary

The shared contract uses the following names so C1, G1, O1, Model Inspection, and Hardware Inspection do not create parallel terms for the same value:

| Concept | Canonical shared name | Compatibility rule |
|---|---|---|
| User selection | `OptimizationPreferenceSelection` | Discriminated as `Automatic` or `Manual`; `Manual` carries `PreferenceValue` 0-100 and derives its visible label |
| Route capability input | `OptimizationCapabilitySnapshot` | Discriminated by `Route`; contains exactly one sealed `GgufCapabilityPayload` or `OpenVinoCapabilityPayload` |
| Complete comparable option | `OptimizationCandidate` | Carries shared normalised metrics plus exactly one sealed route configuration |
| Confirmed immutable work | `OptimizationExecutionPlan` | The only input G1 or O1 may execute |
| Terminal executor output | `OptimizationExecutionResult` | The only optimisation output POD1 may consume |
| Route discriminator | `OptimizationRoute` | Closed values `Gguf` and `OpenVino`; never inferred from a path or filename |
| Planner identity | `OptimizationPlanId` | Non-zero UUID, unique for every newly issued immutable plan |
| Selected configuration digest | `ConfigurationSha256` | Digest of the canonical complete route configuration, not merely its weight format |
| Capability evidence digest | `CapabilitySnapshotSha256` | Digest of the exact route capability snapshot used by C1 |

The established six-field Model Inspection handoff remains byte- and name-compatible: `schemaVersion`, `modelInspectionHandoffId`, `modelInspectionRunId`, `outcome`, `modelSha256`, and `modelLengthBytes`. Optimisation does not rename, widen, or reconstruct that handoff. The separately created Hardware identity remains `productHardwareRunId`. C# properties may use normal PascalCase equivalents, but serialized field names remain exact.

Existing route-specific public names such as `GgufRouteConfiguration`, `OpenVinoOptimizationCandidate`, `OpenVinoPersistentArtifact`, and `OpenVinoRuntimeOptimization` remain inside their owning adapters. Shared code does not add nullable GGUF fields to OpenVINO records or nullable OpenVINO fields to GGUF records.

The current C1 enum members `Quality`, `Balanced`, and `Efficiency` are migration inputs, not the final user contract. The shared selector must support `Automatic` plus the five exact manual bands. Legacy values may be read only through a versioned adapter during migration; new plans never serialize the old four-mode vocabulary.

### 6.2 Identity and binding

All durable planning and execution records use canonical UTF-8 serialization and bind at minimum:

- contract version;
- optimisation plan ID;
- `modelInspectionRunId` and `modelInspectionHandoffId` from the current validated handoff;
- model identity and source SHA-256;
- validated `modelLengthBytes`;
- `productHardwareRunId` and the current Hardware handoff identity;
- hardware snapshot identity and hash;
- route capability snapshot identity and hash;
- selected workload profile identity;
- selected preference kind and slider value when applicable;
- route kind;
- complete selected configuration hash;
- creation timestamp in UTC.

An executor recomputes the source identity and validates all applicable bindings immediately before execution. A mismatch or unknown value fails closed and returns control to C1 for replanning. No worker repairs an identity silently.

### 6.3 Preference selection

The preference input is one of:

- `Automatic`; or
- `Manual`, with an integer slider value from 0 through 100 inclusive.

The visible label is derived exactly as follows:

| Slider value | Exact label |
|---:|---|
| 0-19 | Maximum efficiency |
| 20-39 | Efficient |
| 40-59 | Balanced |
| 60-79 | High capability |
| 80-100 | Maximum capability |

The endpoint labels are `Maximum efficiency` and `Maximum capability`. Internal enum names may be code-styled equivalents, but user-facing copy must remain exact.

### 6.4 Complete candidate

A candidate is not just a weight precision. It contains:

- route kind and backend identity;
- weight representation and whether it is original, runtime-only, or persistent;
- KV-cache representation and its capability evidence;
- target device and device priority;
- GPU offload or partitioning policy where the route supports it;
- context length and workload assumptions;
- Flash Attention state where applicable;
- thread, stream, batch, or performance controls that materially affect fit;
- source and output package expectations;
- estimated model, cache, runtime, working, and transient conversion memory;
- safe usable-memory budget, required reserve, predicted peak, and remaining headroom;
- estimated disk workspace and final output size;
- quality, performance, stability, and context assessments;
- evidence grade and capability references;
- experimental flags and required fallback;
- persistent-change indicator;
- limitations and user-readable tradeoff explanation.

Route-specific payloads are sealed discriminated records. Shared code may compare normalised metrics but must not invent, strip, or reinterpret route-only fields.

### 6.5 Immutable execution plan

The selected plan contains the identity block, the complete candidate, a canonical candidate hash, selected preference explanation, preflight requirements, disk requirements, expected output shape, allowed executor/tool identity, validation obligations, smoke-test obligations, publication rule, and cleanup rule.

The plan is immutable after user confirmation. If any input or capability changes, the executor rejects it and C1 issues a new plan ID. There is no in-place amendment and no executor substitution.

### 6.6 Execution result

The result is one of `SucceededPersistent`, `SucceededRuntimeProfile`, `Cancelled`, `Failed`, or `ReplanRequired` and binds to the exact plan. A successful result records:

- route and plan identity;
- source hash and unchanged-source attestation;
- executor and admitted tool identities;
- selected configuration hash;
- output artifact or runtime-profile identity;
- output manifest/provenance identity;
- validation, smoke-test, and reinspection summaries;
- output size and final storage location controlled by the app;
- bounded privacy-safe support codes;
- completion timestamp.

Only the successful states may reach POD1.

## 7. Candidate generation and admission

### 7.1 Capability evidence

C1 uses capability snapshots produced from the currently admitted route, backend, device, and tool versions. A claimed combination is not viable merely because a planning document lists it.

Released, tested combinations receive normal evidence grades. Experimental combinations are excluded unless an explicit capability record proves the exact backend, device, model representation, cache representation, and relevant version. Unknown evidence fails closed.

### 7.2 Safe memory budget

The planner starts with physically and operationally usable memory, then removes:

- operating-system and application reserve;
- device/runtime reserve;
- workload reserve;
- uncertainty margin;
- transient conversion workspace when persistent conversion is proposed.

Integrated-GPU shared memory must not be counted once as system RAM and again as independent VRAM. Dedicated and shared memory are reported separately, with the binding/partition assumption made explicit.

The peak estimate includes weights, KV cache, runtime allocations, scratch/graph buffers, offload duplication, conversion transient use, and other evidence-backed components. A candidate is unsafe if any required component is unknown or if predicted peak exceeds the safe budget.

### 7.3 Route candidate coverage

GGUF candidate generation may consider, when proved available and semantically valid:

- original representation;
- BF16 or F16 only when a true source of that precision exists;
- Q8_0, Q6_K, Q5_K_M, Q4_K_M, and Q3_K_M;
- standard F16 and Q8_0 KV caches;
- separately admitted backend-specific experimental cache options;
- supported CPU/GPU placement, offload, context, and Flash Attention combinations.

GGUF must never increase precision, describe requantisation as restoring quality, or perform an unsafe requantisation chain.

OpenVINO candidate generation may consider, when proved available:

- original, FP16, INT8, and INT4 weight paths;
- released default, F16, BF16, U8, and U4 cache options where supported;
- supported CPU/GPU/NPU placement and route controls;
- TurboQuant CPU TBQ4 or TBQ3 only after exact experimental admission;
- official OpenVINO behavior as the fallback when an experimental option is absent or rejected.

### 7.4 Filtering and frontier

C1 first rejects candidates that fail hard constraints: capability, source validity, memory safety, disk safety, minimum context, minimum stability, required quality, required performance, or evidence policy.

It then removes candidates dominated across the normalised objectives. The remaining safe Pareto frontier is ordered from memory efficiency toward model capability. Equal candidates are de-duplicated by complete configuration hash.

### 7.5 Preference semantics

- **Maximum capability:** highest-quality complete configuration under this PC's safe limit.
- **High capability:** next meaningful frontier point with a modest memory or context benefit and minimal additional quality loss.
- **Balanced:** the best supported knee between quality, memory headroom, context, performance, and stability.
- **Efficient:** a lower-memory frontier point that remains above preferred - not merely hard-minimum - quality and performance targets.
- **Maximum efficiency:** the minimum-memory complete configuration that still meets hard acceptable quality, stability, performance, and context thresholds.
- **Automatic:** strongest overall result from the same safe frontier, including a penalty for unnecessary persistent conversion.

The planner never fabricates a worse or unsupported configuration to force five distinct results. Adjacent bands may resolve to the same candidate, with a truthful explanation that no meaningful safer distinction exists.

Quality must be nondecreasing as preference moves toward Maximum capability. Memory efficiency must be nondecreasing as preference moves toward Maximum efficiency. Equality is allowed for adjacent positions.

Tie-breaking is deterministic: higher evidence grade, larger safety headroom, no persistent conversion, higher stability, better workload fit, then canonical configuration hash.

## 8. User experience

### 8.1 Entry and selection

The optimisation page appears only after model and hardware inspection have produced a compatible, bound planning input. The default recommended choice is `Automatic`.

Manual mode uses the same continuous 0-100 preference interaction and exact labels as Model Download. The implementation should extract or share the established slider control and tokens rather than clone divergent behavior.

The page presents preference, not a raw precision picker. Advanced technical values may be shown in the live result, but users are never required to understand quantisation names to make the primary choice.

### 8.2 Live configuration card

The live result beneath the selector shows:

- weights and KV cache;
- backend and device;
- context length;
- offload and Flash Attention when relevant;
- estimated memory components and safe headroom;
- evidence grade;
- expected quality/performance tradeoff;
- limitations;
- `New model copy: Yes` or `New model copy: No`.

When two slider bands select the same candidate, the card explains why rather than changing invisible settings.

### 8.3 Confirmation

`Review this configuration` opens a confirmation surface showing:

- whether a persistent new model/package will be created;
- that the original remains unchanged;
- exact output format or runtime-profile type;
- runtime settings, context, and device behavior;
- peak estimate, safe budget, and expected headroom;
- evidence and limitations;
- required working and final disk space;
- route-specific validation and smoke-test summary to be performed.

No optimisation begins until the user confirms this exact immutable plan.

### 8.4 Progress and terminal states

The persistent progress sequence is:

1. Preflight
2. Prepare staging
3. Optimise
4. Validate
5. Smoke test
6. Reinspect
7. Publish

Runtime-only work uses the applicable subset and states clearly that no new model file is being produced. Progress, cancellation, failure, replan-required, and success states must never overclaim completion.

### 8.5 Visual contract

All optimisation screens use the approved modern light application family:

- the same content width, card radii, borders, spacing rhythm, typography hierarchy, status chips, and primary/secondary action palette as the polished Model Import, Model Inspection, and Hardware Inspection screens;
- clean single-column hierarchy at compact widths and deliberate use of space at standard/wide widths;
- vertically centred icons, labels, and statuses within repeated rows;
- equal row sizing for comparable repeated content;
- full-width disclosure hit targets;
- no dark-theme surprise, developer-console styling, raw form grids, or low-level precision-first presentation;
- complete keyboard access, screen-reader names, visible focus, High Contrast behavior, and usable 200% text scaling.

The supplied and brainstormed screens establish direction, but production XAML must use shared product tokens and controls so the layout stays consistent in every state.

## 9. Route execution lifecycle

### 9.1 Common preflight

Immediately before execution, G1 or O1 must:

- recompute and match source identity;
- match model and hardware run bindings;
- match route capability snapshot and admitted tool identity;
- validate free disk, destination policy, and working directory;
- validate the complete selected configuration without modification;
- acquire an exclusive operation claim;
- establish a clean operation-owned staging directory.

If any check fails because planning inputs changed, return `ReplanRequired`. Environmental failures that do not change the plan return a bounded `Failed` result with recovery guidance.

### 9.2 Persistent execution

Persistent execution follows:

`Preflight -> Prepare staging -> Optimise -> Validate -> Smoke test -> Reinspect -> Publish atomically`

The executor writes only to operation-owned staging until all validation succeeds. Publication uses an atomic move or an equivalent collision-safe commit within the app-controlled model store. Existing artifacts are not overwritten silently.

### 9.3 Runtime-only execution

Runtime-only execution validates and stores the exact runtime profile. It does not run a persistent converter and does not create or advertise a model copy. It still binds the profile to model, hardware, route capabilities, and the plan.

### 9.4 Cancellation and failure

- Cancellation terminates operation-owned child processes and verifies cleanup before reporting `Cancelled`.
- Conversion, validation, smoke-test, or reinspection failure publishes nothing.
- Capability drift never triggers substitution; it returns `ReplanRequired`.
- Staging is retained only under a bounded, privacy-reviewed diagnostic policy; the default is cleanup.
- A failed POD1 Chat or Save action does not rerun optimisation. The validated app-controlled result remains available.
- Logs and support details exclude raw model content, private paths, credentials, and unbounded tool output.

## 10. Save-or-Chat destination

POD1 receives only a validated successful result.

For a persistent artifact, it presents:

- `Chat with this model`
- `Save model to this computer`

For a runtime-only result, it presents:

- `Chat with this model`
- `Save this setup`

`Save model to this computer` means the user chooses a local destination and the application copies the validated result to that computer. It does not launch or export directly into another application.

GGUF Save copies the validated GGUF file and its manifest. OpenVINO Save copies the complete validated package and its manifest. The destination operation is collision-safe and must not leave a partial destination after failure or cancellation.

`Save this setup` exports a bounded validated runtime-profile document and manifest; it must not imply that a model was downloaded or created.

Chat consumes the validated route-specific artifact or profile through the existing route runtime. It must verify the result identity before launch. Neither action alters the source model.

## 11. Security, privacy, and integrity

- All tool executables are versioned, hash-verified, and admitted before use.
- Argument construction uses closed allowlists; no free-form shell composition is permitted.
- Working, staging, application-store, and user destination paths are treated as distinct trust boundaries.
- Source and result hashes are recomputed locally at the boundaries where they are relied upon.
- Manifests bind the source, plan, exact configuration, executor/tool identities, output, and validation evidence.
- The original source is opened read-only for conversion inputs and is verified unchanged afterward.
- Raw tool output, model data, private paths, host identity, credentials, and provider payloads are not placed in product-visible support records.
- Experimental TurboQuant options remain unavailable unless their exact route combination is admitted by evidence; failure falls back to released capability planning, not silent runtime fallback after confirmation.

## 12. Testing and acceptance

### 12.1 Shared planner tests

- Both GGUF and OpenVINO capability snapshots can generate complete candidates without route-field leakage.
- Every unsafe, unsupported, incomplete, or insufficient-evidence candidate is rejected.
- Integrated-GPU shared memory is not double-counted.
- Peak estimates include all required components and fail closed on unknown mandatory values.
- Dominated candidates are removed and deterministic ordering is stable.
- No slider band is hardcoded to a precision.
- Exact labels and numeric ranges match Model Download.
- Quality and memory-efficiency monotonic properties hold, allowing equal adjacent selections.
- Automatic uses the same frontier and avoids unnecessary persistent conversion.
- Identity or capability drift produces a new plan requirement.

### 12.2 G1 tests

- Runtime-only GGUF profile does not create a new model.
- Persistent GGUF quantisation uses only the admitted separate tool and exact plan arguments.
- Unsupported requantisation and precision-increase paths fail closed.
- Source hash remains unchanged.
- Validation/smoke failure and cancellation publish nothing and clean owned processes/staging.
- Successful output and manifest are bound and atomically published.

### 12.3 O1 tests

- Existing staging, snapshot, validation, smoke, provenance, rollback, reinspection, and publication invariants remain intact.
- Fixed mode mapping is removed from execution behavior.
- Exact C1 OpenVINO payload is executed without substitution.
- Official supported behavior remains available when experimental TurboQuant is not admitted.
- Source package remains unchanged and failed work publishes nothing.

### 12.4 POD1 tests

- Persistent and runtime-only success produce the correct adaptive action labels.
- GGUF Save produces one validated file plus manifest.
- OpenVINO Save produces one complete validated package plus manifest.
- Runtime-profile Save never claims to export a model.
- Collision, cancellation, disk exhaustion, and copy failure leave no partial destination.
- Chat/Save failure does not rerun optimisation.

### 12.5 Visual and accessibility acceptance

Every selection, confirmation, progress, cancellation, replan, failure, success, Chat, and Save state is reviewed at compact, standard, wide, and 200% text sizes. Repeated cards, rows, icons, labels, statuses, disclosures, and action areas remain aligned. Keyboard, screen-reader, focus, and High Contrast behavior pass. Copy and action colors match the shared product template.

## 13. Migration and worker sequence

Implementation proceeds without overlapping ownership:

1. C1 introduces the route-neutral capability, candidate, plan, result, and mode semantics while preserving existing GGUF behavior through adapters.
2. G1 and O1 may work in parallel against the frozen shared contract.
3. POD1 may work in parallel once the successful-result contract is frozen.
4. I0 integrates common controls, resources, navigation, and dependency wiring after component branches stabilise.
5. Cross-route contract tests and visual QA run on the integrated branch.

During parallel work, C1 alone edits shared compatibility contracts; G1 and O1 edit only their route directories and component-local tests; POD1 edits only its destination feature and component-local tests; I0 alone edits shared app/project/navigation/resource files.

The mode registry migration is additive first: adapters map current route evidence into the new capability snapshot, parity tests hold existing valid outcomes, then fixed executor-side mappings are removed. Persistent conversion remains feature-gated until its tool identity, validation, cancellation, and atomic-publication tests pass.

## 14. Explicit non-goals

This design does not:

- make five preference bands produce five artificial or distinct configurations;
- promise a named precision on every computer;
- treat a document example as tool capability evidence;
- overwrite or delete the original model;
- merge GGUF and OpenVINO tooling into one executor;
- allow Hardware Inspection providers to interpret model data;
- implement Block 3 compatibility calculations beyond C1's approved fit inputs;
- enable unverified TurboQuant formats;
- export directly into an arbitrary third-party application;
- redesign the established product visual system.

## 15. Resolved defaults and residual risks

All design-level choices required for planning are resolved. Safe defaults are:

- Automatic is initially selected.
- Unknown capability or estimate fails closed.
- Original artifacts remain read-only and unchanged.
- Persistent outputs publish only after full validation.
- Experimental options are excluded without exact evidence.
- Adjacent slider bands may share a candidate.
- The app-controlled validated result remains available if a later Chat or Save action fails.

Residual implementation risks are controlled rather than left undecided:

- **Cross-branch contract drift:** freeze canonical records under C1 ownership and require executor contract tests.
- **GGUF quantiser supply-chain or cancellation risk:** isolate the tool, pin identity, close arguments, own the process tree, and keep persistent conversion gated until proven.
- **OpenVINO regression from registry replacement:** preserve the existing executor pipeline and introduce capability adapters with parity tests before removing fixed lookup.
- **Misleading mode copy:** derive the visible configuration and explanation from the actual selected candidate and retain exact shared labels.
- **Memory overcommit:** require complete component estimates, reserve margins, no shared-memory double count, and fail-closed unknowns.
- **Partial user exports:** copy through an operation-owned temporary destination and commit only after verification.

## 16. Acceptance statement

This design is accepted when C1, G1, O1, POD1, and I0 use these ownership boundaries and contracts; the same preference labels and PC-aware semantics apply to GGUF and OpenVINO; the original model remains unchanged; persistent and runtime-only outcomes are represented truthfully; execution is exact, validated, and atomic; and all optimisation screens match the established modern product template across supported layouts and accessibility modes.
