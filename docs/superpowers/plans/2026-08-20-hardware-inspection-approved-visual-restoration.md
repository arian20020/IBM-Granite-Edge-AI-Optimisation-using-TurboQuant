# Hardware Inspection Approved Visual Restoration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reproduce the user-selected completed Direction B, progress Direction B measured checklist, and audited recovery family in native Hardware-owned WinUI controls.

**Architecture:** Preserve the current 15-state presentation model, exact copy, typed actions, and shell-owned footer seam. Replace the experimental visual treatment only inside Hardware-owned page/control XAML, use optically centred Segoe Fluent vector glyphs in fixed containers, and lock the selected structures through packaged UI tests and native captures.

**Tech Stack:** C# 12, .NET 8, WinUI 3 / Windows App SDK, semantic XAML theme resources, MSTest packaged WinUI tests, PowerShell capture tooling.

**Pinned sources:** completed `6C677D5E9BF9F2F58F1F404ECA3798CD6D17C68911F966CA21DEC01CA902FB4B`; progress `EDA670DDB8E6F3628D3F900B0F8A47FED19D51B763A90EF9C691132BF401E8DF`; recovery `24D34112F5BDA58A464032915C672EA825705880293BFFB88031CEB97F7B5286`.

---

### Task 1: Lock the restored family in executable tests

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionPageTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionProgressCardTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionTerminalCardTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionDetailsSummaryTests.cs`

- [ ] Add assertions for a centred `FontIcon` outcome glyph using the approved semantic glyph for each tone.
- [ ] Assert progress Direction B has one current-stage hero, a truthful count, exactly seven checklist rows, one active `ProgressRing`, and explicit status text.
- [ ] Assert completed Direction B keeps the dominant machine card, stacked support column, warning review section, details, actions, and footer seam in the approved order.
- [ ] Assert recovery cards return to compact guidance rows without the experimental accent rail or bordered step-tile treatment.
- [ ] Assert disclosures have the approved icon/title/helper/action header and retain seven stage rows plus nested IT details.
- [ ] Build and run the focused packaged tests; record failures caused only by the current XAML.

### Task 2: Restore vector glyphs and measured-checklist progress

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Controls/HardwareInspectionOutcomeCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Controls/HardwareInspectionOutcomeCard.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Controls/HardwareInspectionProgressCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Controls/HardwareInspectionProgressCard.xaml.cs`

- [ ] Replace the outcome `TextBlock` character with a centred `FontIcon` using Segoe Fluent Icons and fixed 30/44-pixel geometry.
- [ ] Map Neutral, Success, Warning, and Error to stable semantic Fluent glyphs without changing tone selection.
- [ ] Match progress Direction B: compact current-stage header, count at the trailing edge, seven separated rows, completed vector tick, exactly one active ring, numbered waiting rows, and status labels.
- [ ] Preserve reduced-motion behavior and never introduce percentage progress.
- [ ] Run the focused progress and terminal tests until green.

### Task 3: Restore Direction B completed composition

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/HardwareInspectionPage.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/HardwareInspectionPage.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Controls/HardwareInspectionSummaryCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Controls/HardwareInspectionSummaryCard.xaml.cs`

- [ ] Restore the selected scan order: outcome, warning review when present, machine facts, support cards, boundary note, details, actions, footer.
- [ ] Keep `This computer` dominant and use the selected two-column facts grid only at the approved wide breakpoint.
- [ ] Keep `Local AI tools` above `Information sources` in the narrower support column and preserve compact stacking.
- [ ] Match Direction B card padding, border hierarchy, labels, fact-tile rhythm, and whitespace with semantic resources only.
- [ ] Keep installed/usable/available memory and dedicated/shared graphics memory distinct.
- [ ] Run completed/warning page tests until green.

### Task 4: Restore the audited recovery and details family

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Controls/HardwareInspectionRecoveryCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Controls/HardwareInspectionDetailsCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Controls/HardwareInspectionDetailsCard.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Controls/HardwareInspectionActionCard.xaml`

- [ ] Remove the experimental blue header rail and individually boxed recovery steps.
- [ ] Restore the audited compact recovery row geometry, exact copy, statuses, local-processing note, and state-specific actions.
- [ ] Use centred semantic Fluent glyphs for outcome and stage status indicators.
- [ ] Restore the disclosure header anatomy: information glyph, title/helper, absolute Show/Hide action, report badge, seven records, then nested IT disclosure.
- [ ] Keep Stopping bounded with no details/report, and preserve natural page scrolling without a nested main viewport.
- [ ] Match action sizing/order and primary/secondary emphasis from the audited family.
- [ ] Run recovery, details, action, keyboard, and automation tests until green.

### Task 5: Align responsive shell and semantic styling

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/HardwareInspectionPage.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/HardwareInspectionTheme.xaml`
- Modify: `tests/testing/hardware_inspection/test_hardware_inspection_theme_contract.py`

- [ ] Keep the 840-pixel content column, 24/16-pixel gutters, 12-pixel card radii, 44-pixel targets, and 888/600 breakpoints.
- [ ] Remove experimental decorations not present in the selected sources: accent rails, ornamental shadows, gradients, glass, and novel badges.
- [ ] Keep Light as the comparison reference and maintain semantic Dark and High Contrast equivalents.
- [ ] Preserve the shell-owned footer slot after actions and never duplicate Model/Onboarding controls inside Hardware.
- [ ] Run theme, responsive, keyboard, and no-overflow contracts until green.

### Task 6: Native capture and full verification

**Files:**
- Create only ignored/local evidence under: `TestResults/HardwareInspectionVisualFidelity/ApprovedRestoration/`

- [ ] Build the packaged x64 Debug test app with zero errors.
- [ ] Run the complete packaged test suite and both Hardware Python contract modules.
- [ ] Capture representative progress, completed, warning, transient, critical, repair, Stopping, and Cancelled states.
- [ ] Capture 1440×1100, 900×1000, 480×900, and 720×900 at 200% text for the required representative matrix.
- [ ] Compare structure, spacing, glyph centring, reading order, actions, disclosure behavior, and footer seam against the pinned sources.
- [ ] Run `git diff --check`, verify intended Hardware-only scope, and commit the implementation and tests.
