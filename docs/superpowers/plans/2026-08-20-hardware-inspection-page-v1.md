# Hardware Inspection Page v1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Compose the approved Hardware Inspection progress and terminal controls into the production WinUI page layout.

**Architecture:** The page accepts immutable presentation, optional summary, and optional details state. It validates complete state bundles, switches active/terminal surfaces, applies the approved 840 px scroll layout and 888 px two-column breakpoint, and bubbles typed actions. It contains no provider, run, navigation, or compatibility logic.

**Tech Stack:** WinUI 3 Page/VisualStateManager, C# 12, packaged MSTest UI tests.

---

### Task 1: RED page composition tests

- Test exact page heading/subtitle and active-only surface.
- Test completed outcome with This computer left, Local AI tools then Information sources right, full-width details and actions.
- Test invalid/stopping bounded surfaces and rejection of incomplete completed/details bundles.
- Test action bubbling and replacement without stale views.

### Task 2: Summary-card filtering

- Extend the summary card with a title and exact group filter so the page can preserve the approved three-card hierarchy without duplicating facts.
- Keep the existing unfiltered overload for component fixtures.

### Task 3: Production page

- Create `HardwareInspectionPage.xaml(.cs)` with natural vertical page scrolling, max 840 content width, 24/16 gutters, active and terminal panels, wide/narrow adaptive states, theme resources, and typed action bubbling.
- Require summary for completed outcomes and details whenever `DetailsAvailable` is true; fail before mutating UI when a bundle is incomplete.

### Task 4: Verify

- Run focused and full packaged tests, Python contracts, XAML source checks for exact breakpoint/order/no nested page conflicts, and `git diff --check`.
- Commit exact page/test/plan and summary-filter changes. ViewModel/service/navigation remain separately gated.
