# OpenVINO Hardware Compatibility Integration Design

## Objective

Integrate the committed Hardware Inspection and shared compatibility flow from
`origin/fix/hardware-inspection-loq-baseline` at
`6bf1b7281fd3e9080716e528bad8959f42297440` into the committed OpenVINO
optimisation implementation at
`f0189ed187ba900f27bade5fde282ae4e99e8d7b`.

The resulting onboarding journey is:

`Model Import -> route-aware Model Inspection -> shared Hardware Inspection -> route-aware Model/Hardware Compatibility -> route-aware optimisation`

## Source and provenance

- Work only in `C:\OV-HI` on
  `integration/openvino-hardware-compatibility-flow-v1`.
- Merge the hardware source with a real Git merge; never copy files from a
  dirty worktree.
- Preserve the complete histories of both source branches.
- Preserve C1 V2.1 names, identities, validation and configuration digests.
- Preserve OpenVINO official-worker, converter, manifest, executable-hash,
  runtime-build, provenance, rollback and shutdown gates.

## Architecture

One `OnboardingShellPage` remains the sole owner of stage navigation and the
sole bottom stage indicator. Model Import selects the route before inspection.
GGUF continues through `ModelInspectionRequest`; OpenVINO continues through
`OpenVinoInspectionRequestedEventArgs`. Each route publishes a path-free,
identity-bound terminal model handoff that the shell retains only for the
current journey.

Both routes then invoke the same model-independent Hardware Inspection page and
service. Hardware Inspection receives no model path, name, content or
route-specific configuration. On completion it returns only the hardware run
identity and hardware evidence. The shell combines the still-current model
handoff with the fresh hardware handoff only at the compatibility boundary.

Compatibility dispatches by the authoritative model route. GGUF uses the
existing GGUF projector, estimator and candidate generator. OpenVINO uses an
OpenVINO projector which consumes only OpenVINO inspection/capability evidence,
fresh hardware evidence and current available physical memory. Missing, stale,
mixed-route or identity-mismatched evidence fails closed. OpenVINO presentation
must not expose llama.cpp, GGUF quantisation or GGUF-only controls.

## Merge conflict policy

- Preserve the OpenVINO version of the removed feasibility workflow because it
  is an active OpenVINO source gate.
- Union solution and application project references without duplicate entries.
- Preserve route-aware OpenVINO import and the hardware branch's picker/drop
  behavior in Model Import.
- Preserve one light onboarding shell, adding OpenVINO inspection dispatch to
  the hardware/compatibility state machine rather than creating a second shell.
- Preserve fixture-gallery support for every integrated stage.
- Reconcile conflicting tests by retaining assertions for both routes.
- Preserve `MainWindow` conditional fixture navigation and unconditional
  OpenVINO shutdown registration if that file changes during integration.

## Evidence and privacy boundaries

- Hardware providers receive no model-derived value.
- Navigation, UI, diagnostics, evidence, provenance and history expose no local
  paths.
- Model, hardware, capability, configuration and build identities are checked
  at each existing boundary.
- Compatibility uses a fresh Windows available-memory sample, not installed
  memory, and retains the committed provisional memory policy unchanged.
- No route substitutes another route's defaults or evidence.

## Failure and lifecycle behavior

Cancellation, retry, back navigation and stale callbacks retire the current
journey and cannot publish into a replacement journey. A missing hardware
provider, stale memory sample, invalid handoff, route mismatch or unavailable
OpenVINO evidence produces an honest non-continuable state. OpenVINO worker and
conversion cleanup remains part of application shutdown and page retirement.

## Verification

Every new integration behavior starts with a failing observable test. Required
verification includes the full Debug/x64 solution and packaged test builds,
the specified packaged WinUI filters, the complete compatibility core suite,
all OpenVINO suites, supported native/integration tests, `git diff --check`, and
a packaged Intel-machine walkthrough of both routes. Zero-test discovery and
unavailable native stages are never reported as passes.

