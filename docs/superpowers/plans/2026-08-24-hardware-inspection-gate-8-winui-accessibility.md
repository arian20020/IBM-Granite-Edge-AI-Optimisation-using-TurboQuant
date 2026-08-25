# Hardware Inspection Gate 8 WinUI and Accessibility Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to execute this plan task-by-task.

**Goal:** Close the remaining Hardware Inspection WinUI, journey, responsive, and accessibility requirements on the activated Gate 7 service without changing hardware evidence, Model Inspection semantics, or Block 3 compatibility.

**Architecture:** Preserve the existing Hardware-owned page, immutable presentation state, one-run ViewModel, typed actions, and onboarding handoff. Add only a Hardware-owned Windows motion-policy adapter beneath the existing stage-pacer seam. Expand the fixed installed-package acceptance host to a rolling Gate 8 UI inventory while retaining the 32 process-boundary tests; Gate 7 remains evidenced by immutable v12 results.

**Tech stack:** C# 12, .NET 8, WinUI 3 / Windows App SDK 2.2, MSTest 4.3.2 packaged tests, Windows `UISettings`, signed MSIX development acceptance, Python contract tests.

**Authorities:**

- `docs/superpowers/specs/2026-08-15-hardware-inspection-production-design.md`
- `docs/superpowers/specs/2026-08-19-hardware-inspection-visual-contract-v1.md`
- Gate 7 evidence commit `a1ca691d`

## Global constraints

- Work only on `integration/hardware-inspection-intel-completion-v1` in the isolated worktree.
- Preserve the exact seven public stages, four outcomes, handoff eligibility, model/hardware identity separation, and Gate 7 production composition.
- Do not modify Hardware providers, tool roots/manifests, resolution authority, tolerance/freshness policy, Model Inspection outcomes, or Block 3 compatibility.
- Hardware Presentation depends only on Application/Domain contracts and Hardware-owned adapters.
- Reduced motion removes presentation delay only; it never delays, cancels, or changes underlying collection.
- Every action/disclosure remains a real WinUI control with a minimum 44 px target, visible focus, accessible name/state, and non-colour status cue.
- Collapsed details and technical content must be removed from layout and accessibility traversal.
- Do not weaken Smart App Control, Defender, Secure Boot, vTPM, package identity, signing, or guest cleanup.
- All behavior changes are test-first. Generated packages, certificates, TRX, screenshots, raw guest results, and machine paths remain untracked.

## Task 1: Lock the current Gate 8 baseline and reduced-motion gap

**Files:**

- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionAccessibilityTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionViewModelTests.cs`

- [ ] Add RED tests requiring a Hardware-owned motion-policy boundary, normal-motion 500 ms pacing, immediate reduced-motion pacing, cancellation propagation, and fail-safe reduced motion when Windows settings cannot be read.
- [ ] Lock the existing 44 px targets, system focus visuals, accessible names/help, seven-stage live copy, collapsed-content removal, responsive narrow layout, high-contrast semantic resources, and typed page actions without duplicating visual assertions already owned by focused control tests.
- [ ] Build the x64 packaged test project and confirm the missing motion-policy types/behavior are the only intended RED failures.
- [ ] Commit `test(hardware-inspection): lock Gate 8 accessibility policy`.

## Task 2: Implement the Hardware-owned Windows motion policy

**Files:**

- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/IHardwareInspectionMotionSettings.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/UiSettingsHardwareInspectionMotionSettings.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/ViewModels/IHardwareInspectionStagePacer.cs`

- [ ] Add a minimal read-only `AnimationsEnabled` boundary and a Windows `UISettings` adapter. A settings read failure must return `false`, preferring reduced motion.
- [ ] Make `HardwareInspectionStagePacer` call its bounded delay only when animations are enabled; read the current setting for every newly visible stage so a live user preference change affects future stages.
- [ ] Keep the public ViewModel/page APIs and 500 ms normal-motion duration unchanged.
- [ ] Run focused accessibility, ViewModel, journey, page, progress, terminal, details, and presentation tests to GREEN.
- [ ] Commit `feat(hardware-inspection): honor reduced motion during stage pacing`.

## Task 3: Lock production onboarding and UI acceptance inventory

**Files:**

- Modify: relevant Hardware UI/lifecycle test classes under `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingHardwareInspectionNavigationTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingShellPageTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Acceptance/HardwareInspectionProcessAcceptanceHostTests.cs`

- [ ] Add class-level `HardwareInspectionGate8Acceptance` only to the approved presentation, lifecycle, page, and onboarding classes.
- [ ] Replace the rolling guest selection of Gate 7 with Gate 8 while retaining the 32 process tests. Keep Gate 7 attributes/history in source but do not exceed the fixed maximum-128 inventory.
- [ ] Require an exact deterministic inventory count and explicit presence of page, ViewModel, journey, onboarding, responsive, and accessibility tests.
- [ ] Commit `test(hardware-inspection): lock Gate 8 packaged inventory`.

## Task 4: Enable fixed UI-thread guest execution

**Files:**

- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/UnitTestApp.xaml.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Acceptance/HardwareInspectionProcessAcceptanceHost.cs`

- [ ] In acceptance mode create one test window, activate it, and set `UITestMethodAttribute.DispatcherQueue` before discovery; retain normal package identity validation and atomic bounded results.
- [ ] Execute the fixed Gate 8 inventory sequentially on the UI thread. Preserve parameterless `void`/`Task`/`ValueTask` signatures, unique safe names, maximum 128 tests, exception suppression in published output, and process exit semantics.
- [ ] Dispose/close the window before process exit without weakening the guest runner's process and package cleanup.
- [ ] Build Release/x64 and packaged Debug/x64 with zero errors.
- [ ] Commit `test(hardware-inspection): execute Gate 8 UI acceptance`.

## Task 5: Run regressions and audits

- [ ] Run Foundation 201-floor, probe 22-floor, and Hardware/runner Python 63-floor suites with zero failed/skipped.
- [ ] Run focused Hardware presentation/lifecycle/onboarding tests and the broader Hardware/model-handoff/onboarding filter on an eligible packaged host.
- [ ] Build app Release/x64 MSIX and test Debug/x64 MSIX with zero errors; record known warning families separately.
- [ ] Evaluate x86 and require zero Hardware Infrastructure compile items and zero Foundation references.
- [ ] Audit XAML and code for minimum targets, focus, automation names/help, collapsed accessibility, high contrast, responsive layout, reduced motion, raw/private output, model coupling, and navigation ownership.
- [ ] Run `git diff --check`, conflict-marker, dependency, binary/artifact, private-path, and raw-result scans.
- [ ] Perform an inline exact-range review and record the independent-review exception.

## Task 6: Run disposable-guest UI acceptance and record Gate 8

- [ ] Build a fresh physical-worktree signed bundle from the exact reviewed head and verify its four-file canonical inventory and ZIP SHA-256.
- [ ] Run exactly three normally installed registered-AUMID repetitions on the disposable Azure Windows 11 x64 guest with security controls unchanged.
- [ ] Require the exact Gate 8 total to pass in every repetition, package identity present, `Developer` signature, empty failures, and successful cleanup.
- [ ] Validate only the canonical development summary, deallocate/delete the guest resources, and keep raw results/packages/certificates untracked.
- [ ] Update the Foundation README, preservation matrix, evidence index, and a new Gate 8 evidence record with exact counts, hashes, warnings, review status, and non-claims.
- [ ] Commit `docs(hardware-inspection): record Gate 8 verification`.

Gate 8 is complete only after the exact-head guest result and evidence commit. Gate 9 supported Intel-machine, offline/no-port, public signing/trust, and final Block 2 traceability remain incomplete.
