# Hardware Inspection Visual Fidelity Correction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the structurally correct but visually incomplete Hardware Inspection XAML with a native WinUI rendering that follows the approved Direction B, measured-progress, expanded-details, and recovery-family references recorded in the approved visual contract.

**Architecture:** Preserve the existing immutable data and 15-state presentation contracts. Correct only Hardware-owned presentation controls and the page composer, default the page to the current production shell's light presentation while retaining semantic theme dictionaries for explicit Dark and High Contrast validation, and keep shared Onboarding navigation ownership out of Hardware controls. Use executable XAML/control tests plus native capture comparison at the four approved viewport conditions.

**Tech Stack:** C# 12, .NET 8, WinUI 3 / Windows App SDK, MSTest packaged WinUI tests, PowerShell capture tooling.

**Frozen authority:** `docs/hardware-inspection-visual-contract-v1` commit `bb50093688a1a73f898c5eee3bef4432e30381ef`; `docs/superpowers/specs/2026-08-19-hardware-inspection-visual-contract-v1.md`; Direction B HTML SHA-256 `6C677D5E9BF9F2F58F1F404ECA3798CD6D17C68911F966CA21DEC01CA902FB4B`; measured-progress HTML SHA-256 `EDA670DDB8E6F3628D3F900B0F8A47FED19D51B763A90EF9C691132BF401E8DF`; recovery-family HTML SHA-256 `24D34112F5BDA58A464032915C672EA825705880293BFFB88031CEB97F7B5286`; expanded-details design SHA-256 `EAFD6F7EEFFF92F6DA792F134EFBAD609D513FD4B9E8A8659BAE06265F216458`.

---

### Task 1: Lock the visual contract in executable tests

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionPageTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionProgressCardTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionDetailsSummaryTests.cs`
- Modify: `tests/testing/hardware_inspection/test_hardware_inspection_theme_contract.py`

- [ ] Add failing tests requiring the page's initial light presentation, active Cancel action, the approved count/header geometry, visible footer slot, completed fact-tile hierarchy, warning/recovery section, and compact single-column reflow.
- [ ] Run the focused packaged and Python tests and record failures caused by the current missing surfaces/layout.
- [ ] Commit only the RED tests with `test(hardware-inspection): lock approved visual fidelity`.

### Task 2: Correct the progress screen

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Controls/HardwareInspectionProgressCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Controls/HardwareInspectionProgressCard.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/HardwareInspectionPage.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/HardwareInspectionPage.xaml.cs`

- [ ] Move `N of 7` and `checks complete` into the approved right-aligned progress header while retaining truthful completed-stage counting.
- [ ] Add row separators, compact 48-pixel row rhythm, one active orbit, completed tick, waiting number, and non-colour status text.
- [ ] Render the active `Cancel inspection` action centred below the progress card through the existing typed action surface.
- [ ] Keep the active screen free of hardware facts, report details, percentages, or terminal conclusions.
- [ ] Run focused tests, then commit with `fix(hardware-inspection): match approved progress screen`.

### Task 3: Correct completed and warning composition

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Controls/HardwareInspectionSummaryCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Controls/HardwareInspectionSummaryCard.xaml.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Controls/HardwareInspectionReviewCard.xaml`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Controls/HardwareInspectionReviewCard.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/HardwareInspectionPage.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/HardwareInspectionPage.xaml.cs`

- [ ] Replace the long label/value stream with bordered fact tiles using two columns only when each tile remains readable and one column below the approved breakpoint.
- [ ] Preserve the exact Direction B order: outcome, `This computer`, `Local AI tools`, `Information sources`, details, actions.
- [ ] Add the warning-only `What needs review` section with exactly one unresolved review item and one resolved informational note; clean Completed contains no warning language.
- [ ] Preserve dedicated/shared GPU memory and installed/usable/available memory as distinct facts.
- [ ] Run focused tests and commit with `fix(hardware-inspection): match approved completed composition`.

### Task 4: Correct recovery and details surfaces

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Controls/HardwareInspectionOutcomeCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Controls/HardwareInspectionDetailsCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Controls/HardwareInspectionDetailsCard.xaml.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Controls/HardwareInspectionRecoveryCard.xaml`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Controls/HardwareInspectionRecoveryCard.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/HardwareInspectionPage.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/HardwareInspectionPage.xaml.cs`

- [ ] Implement the exact critical/transient/repair/stopping/cancelled content hierarchy from recovery v2 without changing service outcomes.
- [ ] Render `Inspection details` as a compact native disclosure with report/no-report badge, seven bordered stage rows, and a separately collapsed `Technical information for IT` disclosure.
- [ ] Keep ordinary records in the page's natural scroll; add no nested main viewport or fixed card height.
- [ ] Run focused tests and commit with `fix(hardware-inspection): match approved recovery details`.

### Task 5: Align page theme, responsiveness, and footer seam

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/HardwareInspectionPage.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/HardwareInspectionPage.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/HardwareInspectionTheme.xaml`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionPageTests.cs`

- [ ] Default the page to the current production shell's approved Light presentation while allowing explicit Dark and High Contrast overrides for the required matrix.
- [ ] Apply 24-pixel desktop/medium gutters, 16-pixel compact gutters, 840-pixel maximum content width, 12-pixel radii, and approved 888/600 breakpoints.
- [ ] Provide a dedicated footer content slot owned by the shell integration; test its placement after actions and natural content, without duplicating or importing Model semantics into Hardware.
- [ ] Ensure focus, keyboard targets, disclosure states, wrapping, and no-horizontal-overflow behavior remain intact.
- [ ] Run focused tests and commit with `fix(hardware-inspection): align approved responsive shell`.

### Task 6: Native capture and final verification

**Files:**
- Create only ignored/local evidence under: `TestResults/HardwareInspectionVisualFidelity/`

- [ ] Build the packaged x64 Debug test app with zero errors.
- [ ] Run the complete packaged suite; expect at least the current 169 tests plus all new fidelity tests to pass.
- [ ] Run both Hardware Python contract modules.
- [ ] Capture native Light screens at 1440x1100, 900x1000, 480x900, and 720x900 with 200-percent text; compare reading order, card hierarchy, actions, footer placement, wrapping, and scroll reachability against the pinned artifacts.
- [ ] Capture representative Dark and High Contrast states separately; never substitute them for the Light reference comparison.
- [ ] Run `git diff --check`, confirm a clean worktree, and record exact commit and capture hashes for the merge coordinator.

