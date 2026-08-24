# Model Import Drop Card Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore the dashed import card and communicate valid/invalid drag-over state without changing selection behavior.

**Architecture:** Extend the existing `ImportModelCardState` and card XAML visual states. The existing Task 5 drag handler remains the only drop extraction path; visual events only select a presentation state.

**Tech Stack:** WinUI 3 XAML, C#, MSTest AppContainer UI tests.

---

### Task 1: Restore card visuals and drag-over feedback

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/Controls/ImportModelCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/Controls/ImportModelCard.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/Controls/ImportModelCardState.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelImportPage.xaml`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/Controls/ModelImportDropAccessibilityTests.cs`

- [ ] **Step 1: Write failing visual-state tests.** Assert the default card exposes a rounded dashed border; a valid drag displays `Drop model now`; an invalid drag displays corrective text; drag leave restores the default text and border state.
- [ ] **Step 2: Run the packaged UI filter and confirm RED.**
- [ ] **Step 3: Implement the minimal state mapping.** Default uses the restored dashed border; valid uses blue fill/outline and target icon; invalid uses red fill/outline; all states retain dimensions and theme brushes.
- [ ] **Step 4: Run the packaged UI filter and confirm GREEN.**
- [ ] **Step 5: Commit.**
