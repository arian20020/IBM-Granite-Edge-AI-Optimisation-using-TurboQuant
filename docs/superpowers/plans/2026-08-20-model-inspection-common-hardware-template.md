# Model Inspection Common Hardware Template Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reproduce the approved balanced Hardware-style visual template across Model Inspection using Model-owned XAML only, without altering backend behavior or production C#.

**Architecture:** Retain the current `ModelInspectionPage` control tree, presentation bindings, control instances, automation surface, and code-behind. Recompose existing XAML regions with semantic Model theme tokens: the page controls global order, `InspectionModelCard` owns the asymmetric overview, `InspectionContentCard`/`InspectionDisclosure` own progress and details, and `InspectionActionCard` owns the approved modern button treatment.

**Tech Stack:** C# 12 tests, .NET 8, WinUI 3 / Windows App SDK, XAML theme dictionaries, MSTest packaged WinUI tests, native fixture capture tooling.

**Spec:** `docs/superpowers/specs/2026-08-20-model-inspection-common-hardware-template-design.md`

## Global Constraints

- Production changes are limited to the eight Model-owned XAML/theme paths listed in the spec.
- No production `.cs`, ViewModel, service, scanner, classifier, contract, worker, runtime, navigation, project, App-resource, Hardware, Model Import, or Onboarding file may change.
- Existing text, values, bindings, commands, visibility decisions, action order, automation names, live regions, disclosure state, and motion behavior remain unchanged.
- Content width is 840; wide breakpoint is 888; compact breakpoint is 600; card radius is 12; major gap is 16.
- Action targets are at least 44 pixels with 10-pixel corners and `18,10` padding.
- No gradients, glass, glow, ornamental shadows, decorative rails, new badges, or invented fields.
- Tests must demonstrate RED before each XAML change and GREEN afterward.

---

### Task 1: Lock the balanced page hierarchy and theme contract

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageLayoutTests.cs`
- Modify only if needed: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionTheme.xaml`

**Interfaces:**
- Consumes: the existing named `InspectionOutcomeCardControl`, `InspectionModelCardControl`, `InspectionContentCardControl`, `OutgoingProgressContentCard`, and `InspectionActionCardControl` instances.
- Produces: the unchanged named control tree with approved 840/888/600/24/16/12/44/10/18×10 semantic layout resources.

- [ ] **Step 1: Write the failing layout test**

Extend `ModelInspectionPageLayoutTests` to instantiate the real page and assert the existing named controls remain ordered outcome → model → content → actions. Load `ModelInspectionTheme.xaml` and assert the exact visual tokens, including new action geometry resources:

```csharp
Assert.AreEqual(840d, resources["InspectionContentColumnWidth"]);
Assert.AreEqual(888d, resources["InspectionDesktopBreakpoint"]);
Assert.AreEqual(600d, resources["InspectionCompactBreakpoint"]);
Assert.AreEqual(new CornerRadius(12), resources["InspectionCardCornerRadius"]);
Assert.AreEqual(new CornerRadius(10), resources["InspectionActionCornerRadius"]);
Assert.AreEqual(new Thickness(18, 10, 18, 10), resources["InspectionActionPadding"]);
```

Assert `ModelInspectionPage.xaml` retains the single natural vertical `ScrollViewer`, `MaxWidth` token binding, light comparison theme, and no additional page-level content or nested ordinary-content scroller.

- [ ] **Step 2: Run the focused test and verify RED**

Build the packaged x64 Debug test project, then execute `ModelInspectionPageLayoutTests` through the app-container `vstest.console.exe` recipe in `tests/README.md`.

Expected: FAIL because `InspectionActionCornerRadius` and `InspectionActionPadding` are absent.

- [ ] **Step 3: Add the minimal theme tokens and page XAML alignment**

Add only the missing semantic geometry tokens to `ModelInspectionTheme.xaml`. Preserve the existing Light/Dark/HighContrast brush dictionaries. Change `ModelInspectionPage.xaml` only if a spacing/order assertion shows it differs from the approved hierarchy; do not rename controls or alter bindings.

- [ ] **Step 4: Run the focused test and verify GREEN**

Rebuild and rerun `ModelInspectionPageLayoutTests`. Confirm all existing page layout, theme, 200%-text, and retained-tree tests remain green.

### Task 2: Recompose the existing model card into the balanced overview

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionModelCardTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml`

**Interfaces:**
- Consumes: every existing `InspectionModelCardPresentation` binding and existing visual-state selection from unchanged code-behind.
- Produces: the same named elements and bound values arranged as a dominant overview card plus stacked support cards at wide width, and one reading-order column below 888 pixels.

- [ ] **Step 1: Write failing balanced-layout tests**

In `InspectionModelCardTests`, apply the existing Ready and Warning presentations and assert:

```csharp
Grid overviewGrid = (Grid)control.FindName("BalancedOverviewGrid");
Assert.AreEqual(2, overviewGrid.ColumnDefinitions.Count);
Assert.IsTrue(overviewGrid.ColumnDefinitions[0].Width.Value >
              overviewGrid.ColumnDefinitions[1].Width.Value);
Assert.AreEqual(2, ((Grid)control.FindName("ModelFactGrid")).ColumnDefinitions.Count);
```

Drive the existing responsive test seam to medium and compact widths and assert the support stack moves below the dominant card, facts become one column when required, every bound value remains present, and the accessible reading order is unchanged.

- [ ] **Step 2: Run focused model-card tests and verify RED**

Run only `InspectionModelCardTests` using the packaged runner.

Expected: FAIL because `BalancedOverviewGrid` and `ModelFactGrid` do not yet express the selected asymmetric composition.

- [ ] **Step 3: Implement the XAML-only balanced overview**

Recompose `InspectionModelCard.xaml` using the existing bound elements. At wide width use approximately 1.6fr/1fr columns: the left card contains the existing primary model facts in a two-column tile grid; the right column stacks the existing configuration/package and result/limit groups. Use current theme brushes and 12-pixel card corners. Add XAML visual-state setters for medium/compact stacking; do not add or change handlers.

- [ ] **Step 4: Run focused tests and verify GREEN**

Confirm all `InspectionModelCardTests` pass, including missing-value honesty, visual-state behavior, automation names, and compact insets.

### Task 3: Align outcome, progress, findings, glyphs, and disclosures

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionContentCardTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionDisclosureTests.cs`
- Modify only when an existing assertion requires it: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionOutcomeCardTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionOutcomeCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionDisclosure.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionStatusGlyph.xaml`

**Interfaces:**
- Consumes: unchanged outcome/content/disclosure presentations, five-check item collections, status-glyph kinds, and existing disclosure events.
- Produces: the same data and behaviors in the approved semantic cards, measured checklist, compact findings, and two-level disclosure structure.

- [ ] **Step 1: Write failing visual-structure tests**

Extend existing packaged tests to assert:

```csharp
Assert.AreEqual(5, ProgressRows(content).Length);
Assert.AreEqual(1, visibleGlyphs.Count(g => g.Kind == InspectionStatusGlyphKind.Active));
Assert.IsNotNull(disclosure.FindName("DisclosureInformationGlyph"));
Assert.IsNotNull(disclosure.FindName("DisclosureActionText"));
Assert.AreEqual(new CornerRadius(12), outcomeSurface.CornerRadius);
```

Assert details remain native disclosures, collapsed content remains outside hit testing/tab/automation, the nested technical disclosure remains after the five rows, and no new text/value is introduced.

- [ ] **Step 2: Run focused tests and verify RED**

Run `InspectionOutcomeCardTests`, `InspectionContentCardTests`, and `InspectionDisclosureTests` through the packaged runner.

Expected: FAIL only on the missing approved header anatomy or layout geometry.

- [ ] **Step 3: Implement the minimal XAML alignment**

Use semantic solid surfaces and vector glyph geometry. Retain exactly five progress rows and one active indicator. Recompose findings as compact rows and details as full-width native disclosures with information glyph, title/helper/action header, existing rows, then existing nested IT disclosure. Preserve all `x:Name`, `x:Bind`, commands, events, visibility bindings, and automation properties used by code/tests.

- [ ] **Step 4: Run focused tests and verify GREEN**

Confirm all current state variants, disclosure behaviors, keyboard paths, status semantics, and motion tests pass without production C# changes.

### Task 4: Apply the approved modern Model action treatment

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionActionCardTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionActionCard.xaml`

**Interfaces:**
- Consumes: unchanged action presentations, commands, help text, visibility, enabled state, order, and responsive code-behind state selection.
- Produces: the same buttons styled with Model-owned primary/secondary resources and approved 44/10/18×10 geometry.

- [ ] **Step 1: Write the failing button-style test**

Extend `InspectionActionCardTests` to load the real control and assert each existing button receives the appropriate local style while preserving the exact command, action ID, label, help text, enabled state, and tab order:

```csharp
Assert.AreEqual(44d, button.MinHeight);
Assert.AreEqual(new CornerRadius(10), button.CornerRadius);
Assert.AreEqual(new Thickness(18, 10, 18, 10), button.Padding);
Assert.IsTrue(button.UseSystemFocusVisuals);
```

Assert wider states remain centred and compact states remain full-width vertical without changing action order.

- [ ] **Step 2: Run focused action tests and verify RED**

Expected: FAIL because current action buttons use 12-pixel corners and `20,10` padding.

- [ ] **Step 3: Implement the XAML-only action styles**

Replace repeated visual setters with local XAML styles based on native accent/default button styles. Use the Task 1 theme geometry tokens, semantic primary/secondary brushes, and native pointer/focus/disabled states. Keep every existing binding and `TabIndex` unchanged.

- [ ] **Step 4: Run focused tests and verify GREEN**

Confirm all action, disabled-future-help, command, accessibility, responsive, and focus tests remain green.

### Task 5: Full regression, native visual approval, and integration readiness

**Files:**
- Create only ignored/local captures under: `TestResults/ModelInspection/CommonHardwareTemplate/`
- Modify no production file during this task unless a failing acceptance check first produces a focused test.

**Interfaces:**
- Consumes: the completed XAML-only visual implementation.
- Produces: verified committed Model visual branch ready for explicit merge approval.

- [ ] **Step 1: Verify production scope mechanically**

Run `git diff --name-only 960bb4d0..HEAD` and the working-tree equivalent. Fail if any production `.cs`, backend, project, shared-resource, Hardware, Model Import, Onboarding, or navigation path appears. Documentation and Model visual tests are allowed.

- [ ] **Step 2: Build and run the full packaged suite**

Build x64 Debug with zero errors using the repository recipe. Run the complete `GraniteEdgeAI.UnitTests` app-container suite and inspect the TRX counters for zero failed/error/timeout/aborted/non-passing outcomes.

- [ ] **Step 3: Capture representative native states**

Use the existing Model fixture gallery/capture runner without changing fixture meaning. Capture progress, ready, warning, unsupported/failure, and cancelled at 1440×1100, 900×1000, 480×900, and representative 720×900 at 200% text.

- [ ] **Step 4: Compare against the selected visual authority**

Confirm region order, asymmetric overview, solid semantic surfaces, five-row progress/details, glyph centring, action styling, compact stacking, focus visibility, and absence of horizontal clipping. Record any divergence rather than altering semantics to imitate the HTML.

- [ ] **Step 5: Commit and verify committed bytes**

Run `git diff --check`, stage only approved XAML/theme and visual-test paths, then commit with:

```text
style(model-inspection): adopt common hardware template
```

Rerun the full packaged suite and scope check from committed bytes. Preserve the branch and worktree for user screenshot approval; do not merge until the user explicitly approves the native captures.
