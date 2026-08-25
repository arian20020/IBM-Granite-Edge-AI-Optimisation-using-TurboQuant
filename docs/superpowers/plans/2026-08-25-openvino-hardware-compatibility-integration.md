# OpenVINO Hardware Compatibility Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver one route-aware onboarding flow in which GGUF and OpenVINO share Hardware Inspection and then use their own compatibility/optimisation projections.

**Architecture:** Merge the committed hardware branch into the committed OpenVINO branch, preserve one onboarding shell, and add the smallest route-discriminated handoff/projection glue required for OpenVINO. Hardware collection remains model-independent; compatibility is the first layer allowed to combine model and fresh hardware evidence.

**Tech Stack:** C# 12, .NET 8, WinUI 3, Windows App SDK, MSTest packaged AppContainer tests, Microsoft Testing Platform, PowerShell, Git merge commits.

**Spec:** `docs/superpowers/specs/2026-08-25-openvino-hardware-compatibility-integration-design.md`

## Global Constraints

- OpenVINO source SHA: `f0189ed187ba900f27bade5fde282ae4e99e8d7b`.
- Hardware source SHA: `6bf1b7281fd3e9080716e528bad8959f42297440`.
- Preserve C1 V2.1 contract names and semantics without duplication.
- Preserve all OpenVINO security, build-identity, provenance, rollback and cleanup gates.
- Hardware Inspection receives no model-specific data.
- GGUF and OpenVINO evidence never cross route boundaries.
- Use current available physical memory at compatibility admission.
- Keep the committed provisional memory policy unchanged.
- Keep one light onboarding shell and one bottom stage indicator.
- Do not expose local paths or raw model evidence.

---

### Task 1: Merge committed hardware history

**Files:**
- Merge: `origin/fix/hardware-inspection-loq-baseline`
- Resolve: `IBM Granite with TurboQuant (Intel).slnx`
- Resolve: `IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj`
- Resolve: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelImportPage.Selection.cs`
- Resolve: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelImportPage.xaml.cs`
- Resolve: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs`
- Resolve: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/DebugFixtures/OnboardingShellPage.FixtureGallery.cs`
- Resolve: the three predicted packaged-test conflicts under `tests/UnitTests/GraniteEdgeAI.UnitTests`

**Interfaces:**
- Consumes: both verified Git commits.
- Produces: a real merge commit with both histories, one build graph and no unmerged paths.

- [ ] Start `git merge --no-ff origin/fix/hardware-inspection-loq-baseline`.
- [ ] Resolve each conflict by preserving both route behaviors and the union of required projects/resources.
- [ ] Keep `.github/workflows/llamasharp-feasibility-smoke.yml` because the OpenVINO branch still modifies and uses it.
- [ ] Run `git diff --check` and inspect `git diff --cc` for every resolution.
- [ ] Build Debug/x64 with official-worker packaging disabled only for this merge compile check.
- [ ] Commit the merge and record both parent SHAs.

### Task 2: Route-aware model-to-hardware handoff

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.OpenVino.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingOpenVinoHardwareNavigationTests.cs`

**Interfaces:**
- Consumes: the existing GGUF `ModelInspectionHandoff`, OpenVINO schema-v2 handoff lease, and shared `IHardwareInspectionService`.
- Produces: one path-free route-discriminated model handoff accepted by the shared hardware navigation transaction.

- [ ] Add a test proving a completed OpenVINO inspection enables exactly one hardware navigation request without starting GGUF inspection or exposing its directory.
- [ ] Run the test and verify RED because OpenVINO has no hardware handoff/navigation.
- [ ] Add the minimal OpenVINO handoff retention/reissue and shell dispatch needed to enter the existing Hardware Inspection page.
- [ ] Add rejection tests for stale, missing, duplicate and mixed-route handoffs; verify RED before each production change.
- [ ] Run the focused navigation/lifecycle suite until GREEN.
- [ ] Commit the route-aware handoff integration.

### Task 3: OpenVINO compatibility projection

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Infrastructure/OpenVinoCompatibilityInputProjector.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/CompatibilityPage.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/ViewModels/CompatibilityViewModel.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/OpenVinoCompatibilityInputProjectorTests.cs`

**Interfaces:**
- Consumes: exact OpenVINO model inspection identity/evidence, OpenVINO capability facts, current hardware handoff, product hardware run ID and `AvailableMemorySnapshot`.
- Produces: the existing C1 V2.1 `CompatibilityProductionInput` with `OptimizationRoute.OpenVino`, or `false` for any absent/stale/mismatched input.

- [ ] Add literal, behavior-based tests for accepted OpenVINO CPU evidence and for missing, GGUF, stale-memory, stale-model, hardware-run and identity mismatches.
- [ ] Run the tests and verify RED because the OpenVINO projector does not exist.
- [ ] Implement the projector without GGUF defaults or model data in hardware providers.
- [ ] Add a shell test proving hardware completion dispatches OpenVINO to the OpenVINO projector while GGUF still dispatches to `GgufCompatibilityInputProjector`.
- [ ] Run focused projector, compatibility and shell suites until GREEN.
- [ ] Commit the OpenVINO compatibility projection.

### Task 4: Compatibility-to-optimisation continuity and privacy

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/CompatibilityPage.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Optimization/OpenVinoOptimizationPlanAdapter.cs`
- Test: existing OpenVINO optimisation, compatibility and onboarding suites plus focused new integration tests.

**Interfaces:**
- Consumes: C1 V2.1 compatibility candidates and exact OpenVINO execution payloads.
- Produces: unchanged O1 execution/provenance behavior after OpenVINO compatibility, and unchanged GGUF behavior.

- [ ] Add a test proving an admitted OpenVINO candidate maps unchanged into O1 and a GGUF/mixed payload fails closed.
- [ ] Run it and verify RED only if integration glue is missing; do not rewrite already-correct O1 behavior.
- [ ] Implement the minimal route dispatch needed for the compatibility result to reach the existing optimiser.
- [ ] Add a privacy/lifecycle test proving paths and retired handoffs do not enter compatibility presentation, diagnostics or provenance.
- [ ] Run OpenVINO optimisation and cross-route contract suites until GREEN.
- [ ] Commit continuity/privacy integration changes.

### Task 5: Complete automated verification

**Files:**
- No source changes unless a failing behavior is reproduced by a new RED test.

**Interfaces:**
- Consumes: the integrated source tree.
- Produces: exact discovered/passed/failed/skipped counts and build evidence.

- [ ] Build the full solution Debug/x64.
- [ ] Build packaged `GraniteEdgeAI.UnitTests` Debug/x64.
- [ ] Run non-zero packaged filters for ModelImport, ModelInspection, HardwareInspection, ModelHardwareCompatibility, Compatibility, Onboarding and OpenVino.
- [ ] Run the complete compatibility core suite.
- [ ] Run every complete OpenVINO test project with official native stage variables where available.
- [ ] Run O1 native/integration tests and explicitly record environmental failures or skips.
- [ ] Run `git diff --check` and confirm no conflict markers or unmerged paths.

### Task 6: Packaged Intel walkthrough and handoff

**Files:**
- No source changes unless a walkthrough defect is first captured by a failing test.

**Interfaces:**
- Consumes: the officially staged Debug/x64 packaged application.
- Produces: manual route results, final commits and pushed integration branch.

- [ ] Build with the verified official OpenVINO worker stage and pinned manifest digest.
- [ ] Launch through the trusted packaged registration path without weakening Windows Application Control.
- [ ] Exercise GGUF picker/drop, GGUF inspection, hardware, compatibility, retry/back/cancel, and single-shell/responsive behavior.
- [ ] Exercise the available trusted OpenVINO package through inspection, shared hardware and OpenVINO compatibility.
- [ ] Record blockers rather than fabricating unavailable native results.
- [ ] Commit any final test-first fixes, push `integration/openvino-hardware-compatibility-flow-v1`, and report SHAs, conflicts, changed paths, counts, walkthrough, privacy and clean status.
