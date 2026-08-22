# Hardware Inspection Terminal Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refine the transient recovery surface and warning review-count alignment while preserving the frozen Hardware Inspection semantics and copy.

**Architecture:** Keep the existing presentation factory and typed states unchanged. Make the polish entirely inside the two Hardware-owned WinUI controls, lock the required geometry in the existing packaged UI tests, and verify the result with native captures.

**Tech Stack:** C# 12, .NET 8, WinUI 3 / Windows App SDK, MSTest packaged WinUI tests, PowerShell capture tooling.

---

### Task 1: Lock the approved polish in tests

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionPageTests.cs`

- [x] Add assertions that the warning count and its label are centred within the trailing count stack.
- [x] Add assertions that recovery instructions render from a named items control into separate bordered, padded, rounded surfaces with a 32-pixel marker.
- [x] Run the focused packaged tests and confirm they fail because the current alignment and surfaces are absent.

### Task 2: Refine the two controls

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Controls/HardwareInspectionOutcomeCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Controls/HardwareInspectionRecoveryCard.xaml`

- [x] Centre the review count and label without changing their trailing-column placement, text, or visibility contract.
- [x] Give the recovery card a clearer header boundary and each instruction its own quiet bordered surface.
- [x] Increase the marker to 32 pixels, preserve text and reading order, and retain semantic theme brushes.
- [x] Run the focused packaged tests and confirm they pass.

### Task 3: Verify and capture

**Files:**
- Create only ignored/local evidence under: `TestResults/HardwareInspectionVisualFidelity/Polish/`

- [x] Build the packaged x64 Debug test application.
- [x] Run the complete packaged suite and both Hardware Python contract modules.
- [x] Capture native transient-medium-light and warning-medium-light screens and inspect their hierarchy, centring, wrapping, and action order.
- [x] Run `git diff --check`, verify intended scope, and commit the polished controls, tests, design, and plan.
