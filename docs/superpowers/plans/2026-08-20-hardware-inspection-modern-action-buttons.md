# Hardware Inspection Modern Action Buttons Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Modernise the Hardware Inspection primary and secondary action buttons without changing any screen structure or behavior.

**Architecture:** Define two Hardware-owned local button styles inside `HardwareInspectionActionCard.xaml` and keep the existing typed-action generation and responsive layout intact. Lock the style geometry and state resources through the existing packaged component test before changing production XAML.

**Tech Stack:** C# 12, .NET 8, WinUI 3 / Windows App SDK, semantic XAML resources, MSTest packaged WinUI tests.

**Spec:** `docs/superpowers/specs/2026-08-20-hardware-inspection-modern-action-buttons-design.md`

## Global Constraints

- Minimum height is exactly 44 pixels; corner radius is exactly 10 pixels.
- Button padding is exactly `18,10` and adjacent spacing remains 12 pixels.
- Primary remains IBM-blue accent; secondary remains neutral and bordered.
- Normal, pointer-over, pressed, disabled, and keyboard-focus behavior must remain semantic in Light, Dark, and High Contrast.
- Do not alter copy, action order, action availability, navigation, page composition, footer ownership, Model Inspection, or shared App resources.

---

### Task 1: Lock and implement the Hardware-owned action styles

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionTerminalCardTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Controls/HardwareInspectionActionCard.xaml`
- Modify only if needed to bind the existing style keys: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Controls/HardwareInspectionActionCard.xaml.cs`

**Interfaces:**
- Consumes: the existing `HardwareInspectionActionCard.Apply` action list and existing typed `ActionRequested` event.
- Produces: local `HardwareInspectionPrimaryActionStyle` and `HardwareInspectionSecondaryActionStyle` resources applied by the current action-button factory.

- [ ] **Step 1: Write the failing packaged component test**

Extend the existing action-card test to load real XAML resources and assert both named styles exist. Assert their setters produce `MinHeight=44`, `CornerRadius=10`, and `Padding=18,10`; assert primary is based on the accent button style and secondary remains a distinct neutral style. Assert Completed still renders the same action order and compact mode still stacks without changing actions.

- [ ] **Step 2: Run the focused test and verify RED**

Build the packaged test project and run the exact action-card test through the repository `vstest.console.exe` app-container recipe documented in `tests/README.md`.

Expected: FAIL because `HardwareInspectionSecondaryActionStyle` and the exact geometry setters are absent.

- [ ] **Step 3: Implement the minimal styles**

In `HardwareInspectionActionCard.xaml`, keep both styles local. Base the primary style on `AccentButtonStyle`; base the secondary style on the ordinary WinUI button style. Set the approved geometry and semantic state resources. Keep the focus visual visible and use only a restrained pointer-over elevation supported by WinUI theme resources; do not add gradients, icons, glow, or pills.

If the C# factory currently applies only the primary style, select the appropriate local style by the existing `IsPrimary` action property without changing the action model or event flow.

- [ ] **Step 4: Run focused tests and verify GREEN**

Run the packaged action-card and terminal-card tests. Confirm the new assertions pass and all existing action ordering, disabled-help, typed-event, and compact-stack assertions remain green.

- [ ] **Step 5: Run full verification and capture**

Build x64 Debug with zero errors. Run the complete packaged suite and both Hardware Python contract modules. Capture Completed, Warning, one recovery state, and compact recovery locally; compare only the button treatment and confirm every surrounding layout remains unchanged. Run `git diff --check` and verify Hardware ActionCard/test-only scope.

- [ ] **Step 6: Commit**

Stage only the approved ActionCard and test files, then commit with:

```text
fix(hardware-inspection): refine action button styling
```
