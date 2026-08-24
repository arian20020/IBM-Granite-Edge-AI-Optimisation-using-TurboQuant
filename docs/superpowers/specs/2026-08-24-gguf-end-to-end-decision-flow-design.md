# GGUF End-to-End Decision Flow Design

**Status:** Approved for implementation on 24 August 2026.

## Goal

Deliver one coherent GGUF application journey:

`Model Import -> Model Inspection -> Hardware Inspection -> Model/Hardware Compatibility decision`

This increment ends at the compatibility decision. Optimisation-mode selection,
optimisation execution, and the final Export/Chat choice are the next increment.

## Existing foundations retained

- Model Import remains the owner of native selection and bounded GGUF quick scan.
- Model Inspection remains the owner of protected inspection and its exact six-field
  `ModelInspectionHandoff`.
- Hardware Inspection remains the owner of local hardware collection and its usable
  `HardwareInspectionHandoff`.
- C1's merged Model/Hardware Compatibility Core remains the sole owner of compatibility
  reasoning, candidate generation, fit policy, decisions, and compatibility UI.
- No inspection backend is changed to make integration easier.
- Existing approved visual resources and page grammar remain authoritative.

## Architecture

The onboarding shell owns the journey transaction because it already owns the active
Model handoff registry, the Hardware run identity, retry invalidation, and navigation.
It never performs compatibility calculations.

A narrow application adapter projects owner evidence into a route-neutral C1 input:

- Model facts come from the terminal validated Model Inspection result associated with
  the current Model handoff. Paths, filenames, model display names, raw worker output,
  and diagnostics do not cross the boundary.
- Hardware facts come from the usable Hardware snapshot associated with the current
  Hardware handoff. Hardware providers receive no model data.
- Available physical memory is read once, immediately before C1's safety gate. The
  historical availability value in the Hardware snapshot is never substituted for this
  fresh observation.
- The C1 engine retains all estimation, support-matrix, candidate, fit, mode, and decision
  logic. The application adapter cannot override or reproduce a C1 result.

C1 exposes one public route-neutral execution boundary while its ports and calculation
types remain internal. The boundary accepts validated scalar/value inputs and builds the
existing internal ports. It does not reference Model Inspection, Hardware Inspection,
WinUI, paths, or provider types.

## Identity and transaction rules

Compatibility navigation is permitted exactly once when all of the following are true:

1. The Model handoff is the shell's active, non-invalidated handoff.
2. Its `modelInspectionRunId` matches the terminal Model evidence being projected.
3. The Hardware handoff is usable and belongs to the active `productHardwareRunId`.
4. Hardware Inspection ended as Completed or CompletedWithWarnings.
5. The Hardware page is still the shell's attached active page.
6. No retry, rollback, abandonment, replacement, or later run has superseded either input.

Any false, missing, conflicting, or unknown condition fails closed and leaves the user on
the Hardware page with its existing recovery behavior. Navigation failure must not consume
or promote either input.

The shell stores the paired compatibility session only for the lifetime of the active
journey. Returning to Model Inspection, selecting a new model, retrying Hardware Inspection,
or abandoning the page invalidates it.

## GGUF model projection

The first production adapter accepts only a terminal eligible GGUF Model Inspection result.
It projects:

- validated model byte length from the exact handoff;
- layer count, embedding size, attention-head count, KV-head count, declared context limit,
  GGUF file type, and quantisation version from validated configuration evidence;
- the current GGUF route, imported weight format, CPU baseline, and conservative defaults
  already owned by C1 where the current inspection contract does not establish a value.

Missing optional facts remain absent. They are never inferred as zero. If mandatory facts
for a safe conclusion are absent, C1 produces Not Established rather than a false-safe result.

The route discriminator and public boundary are format-neutral. A later OpenVINO adapter can
provide OpenVINO facts without renaming the Model/Hardware identity fields or changing shell
navigation.

## Hardware projection

The adapter projects only calculation inputs:

- installed physical memory;
- aggregate dedicated device memory using checked arithmetic;
- free system-volume storage;
- present CPU/integrated/discrete device routes derived from established snapshot facts;
- verified backends derived only from the inspected local runtime capability contract.

Names, host identity, device identifiers, raw evidence, native output, and safe diagnostic
text do not enter C1.

## Navigation and presentation

Hardware completion raises one typed completion event carrying the immutable Hardware
handoff. The shell validates and projects the paired session, constructs the existing C1
Compatibility page with injected input, and makes Compatibility the active onboarding stage.

The Compatibility page automatically evaluates once per navigation. Its existing analysing,
compatible, optimisation-required, not-established, failure, and cancellation presentations
remain unchanged. Back returns to the completed Hardware page without rerunning inspection.

Continue remains visible-disabled in this increment because the optimisation-mode destination
is not yet registered. This preserves the approved fail-closed Continue predicate. The next
increment registers that destination and commits the compatibility transfer only after the user
selects Continue.

## Drag-and-drop integration

The already-implemented drag-and-drop feature must be imported as Git history, not recreated.
Before merging it, the coordinator resolves its authoritative branch and tip, verifies its
handoff, and compares it with the current Model Import contracts.

The merge must preserve both picker and drag-and-drop entry methods. Both routes converge on
the same `ModelQuickScanResult -> ModelInspectionRequest` boundary, so downstream Model,
Hardware, and Compatibility code cannot distinguish how the model was selected. Drag-and-drop
must not bypass format choice, bounded quick scan, scan cancellation, file identity validation,
or immutable Model Inspection request creation.

The authoritative drag-and-drop branch is not currently present locally, on `origin`, in local
reflogs, or as a discovered bundle. Its import is therefore a separately verifiable merge step;
no substitute implementation is authorised by this design.

## Error handling

- Adapter validation returns stable typed unavailability, never a native exception or raw text.
- Checked conversion/arithmetic failure produces Not Established.
- Fresh-memory probe failure produces Not Established.
- Stale or mismatched identities prevent navigation.
- A Compatibility evaluation exception is converted by the existing C1 engine into its safe
  failure presentation.
- Cancellation retires the active attempt and cannot publish a late result.

## Verification

Implementation uses red-green test cycles and must demonstrate:

- exact GGUF model-evidence projection, including absent optional facts;
- Hardware projection and checked dedicated-memory aggregation;
- one fresh-memory observation per evaluation;
- rejection of stale, mismatched, retried, abandoned, or unusable handoffs;
- exactly-once Hardware-completion navigation;
- Back behavior without rerunning Hardware Inspection;
- Compatibility Continue visible-disabled until the optimisation route exists;
- picker and drag-and-drop convergence on the same immutable request boundary;
- C1 Core, Model Import, Model Inspection, Hardware Inspection, onboarding navigation,
  packaged UI, and privacy suites;
- Debug x64, Release x64, and opt-in fixture-gallery builds.

## Non-goals

This increment does not implement OpenVINO import/inspection adaptation, runtime optimisation,
mode selection UI, quantisation/conversion execution, model export, Chat, candidate execution,
or Stage C/Gate 1 operational activity.
