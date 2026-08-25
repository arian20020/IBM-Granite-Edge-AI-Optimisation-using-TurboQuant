# Hardware Inspection Progress Card v1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Render the exact approved active Hardware Inspection state as a reusable WinUI card.

**Architecture:** The control consumes only `HardwareInspectionPresentationState` with kind `Active`. Code-behind maps immutable stage rows to display-only glyph/visibility values; it does not own stage copy or outcome policy. The Hardware-owned resource dictionary supplies theme, sizing, spacing, and high-contrast tokens locally until the serial page owner merges it globally.

**Tech Stack:** WinUI 3 XAML, C# 12, packaged MSTest UI tests.

---

### Task 1: Lock rendering behavior

- Create `HardwareInspectionProgressCardTests.cs` with UI tests for exact title/body/count, all seven rows, exactly one active indicator, completed/waiting status, replacement state, and rejection of non-active state.
- Build and capture missing-control RED.

### Task 2: Implement the control

- Create `HardwareInspectionProgressCard.xaml` using an 840 px-bounded semantic card, 24 px padding, 12 px row gaps, theme resources, wrapped text, 44 px glyph targets, one `ProgressRing`, and no percentage.
- Create `HardwareInspectionProgressCard.xaml.cs` with `Apply(HardwareInspectionPresentationState)`, defensive validation, read-only row view data, and exact count/status updates.
- Build and run the focused packaged UI tests until green.

### Task 3: Verify and commit

- Run all 160 packaged UnitTests plus Hardware Python contracts.
- Run `git diff --check`, inspect exact scope, and commit only the control, test, and this plan.
- Record that page composition, cancellation wiring, animation pacing, reduced-motion orchestration, and navigation remain downstream.
