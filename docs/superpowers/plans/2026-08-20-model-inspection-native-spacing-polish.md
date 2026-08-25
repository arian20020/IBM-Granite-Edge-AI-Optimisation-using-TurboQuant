# Model Inspection Native Spacing and Alignment Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the native WinUI Model Inspection screens reproduce the approved localhost spacing rhythm, alignment, repeated-row geometry, and polished action composition without changing Model Inspection behavior.

**Architecture:** Preserve the current presentation objects, control instances, bindings, commands, named elements, automation surfaces, and production C# paths. Add a small Model-owned measurement vocabulary to `ModelInspectionTheme.xaml`, consume it from the existing content and action XAML, and extend the existing packaged WinUI test identities so every visual correction is proved by a RED/GREEN cycle without changing test discovery.

**Tech Stack:** WinUI 3 / Windows App SDK 2.2; XAML resources, templates, `ItemsRepeater`, `UniformGridLayout`, `ItemsControl`, `ItemsWrapGrid`, and VisualStates; C# 12 MSTest visual-contract tests; packaged AppContainer tests through Visual Studio `vstest.console.exe`; PowerShell.

**Spec:** `docs/superpowers/specs/2026-08-20-model-inspection-native-spacing-polish-design.md`

## Global Constraints

- Execute in `C:\mi-visual-align` on `feature/model-inspection-hardware-template-v1`; do not edit or merge `main` during these tasks.
- Visual oracle: `model-inspection-balanced-full-approval-v3.html`, SHA-256 `D44CFCE9C53BCD9D0EAAA41CA8BA9DED434F68845BBF943351518366FBB20CFF`.
- Production changes are limited to Model-owned XAML/theme files named in this plan. No production `.cs`, ViewModel, service, scanner, classifier, contract, runtime, navigation, project, App resource, Hardware Inspection, Model Import, or Onboarding file may change.
- Preserve every existing `x:Name`, `x:Bind`, command, command parameter, action ID, VisualState name, automation name/help string, tab index, and disclosure interaction.
- Do not add, remove, rename, or parameterize a `UITestMethod`, `TestMethod`, or `DataRow`. The final packaged result-name multiset must equal the baseline multiset.
- Use semantic theme resources only. Do not add literal Light-only colors or force a theme.
- Normal geometry: centered 840-pixel content column; 24-pixel wide/medium gutters; 16-pixel compact gutters; 12-pixel card corners; 24-pixel repeated-row glyph column; 12-pixel glyph-to-copy gap; 68-pixel normal-scale disclosure header.
- Repeated rows are equal within their group at each tested viewport/text profile and grow together when the safe content requires more height. No fixed-height clipping at representative 200% text is accepted.
- All action targets remain at least 44 pixels high, use the existing native default/accent style inheritance, retain visible keyboard focus, and keep the current logical/tab order.
- Keep the tracked worktree clean between commits. Test output and native captures remain under ignored `TestResults/ModelInspection/CommonHardwareTemplate/NativeSpacingPolish/`.

---

## File and Responsibility Map

### Production presentation

- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionTheme.xaml`: shared row, disclosure, terminal-card, and action-spacing measurements.
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml`: progress rows, terminal result rows, expanded inspection rows, disclosure header, supporting text, and diagnostic alignment.
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionActionCard.xaml`: result-card padding, heading/helper rhythm, action-panel spacing, and future-help spacing.
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionDisclosure.xaml`: verification only; edit only if the loaded header cannot consume the approved 68-pixel minimum while retaining centered content.
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml`: verification only; no edit unless the loaded cross-card alignment test proves an accidental nested margin.

### Existing visual contracts

- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageLayoutTests.cs`
- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionContentCardTests.cs`
- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionActionCardTests.cs`
- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionModelCardTests.cs`
- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionDisclosureTests.cs`
- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionRenderedStateTests.cs`

## Reusable Packaged Test Commands

Run packaged tests serially. Before a campaign, fail if an existing runner owns the package; do not globally kill processes.

```powershell
$ErrorActionPreference = 'Stop'
$repo = 'C:\mi-visual-align'
$app = Join-Path $repo 'IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj'
$tests = Join-Path $repo 'tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj'
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -products * -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
$vstest = & $vswhere -latest -products * -find '**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' | Select-Object -First 1
if (-not $msbuild -or -not $vstest) { throw 'Visual Studio build/test tools were not found.' }

& $msbuild $app /target:Restore,Build /property:Configuration=Debug /property:Platform=x64
if ($LASTEXITCODE -ne 0) { throw 'Debug app build failed.' }
dotnet build $tests --configuration Debug --runtime win-x64 -p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw 'Debug test build failed.' }

$recipe = Join-Path $repo 'tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe'
if (-not (Test-Path -LiteralPath $recipe -PathType Leaf)) { throw 'Packaged test recipe is missing.' }
```

For a focused class gate, create a fresh result directory and run:

```powershell
$result = Join-Path $repo 'TestResults\ModelInspection\CommonHardwareTemplate\NativeSpacingPolish\focused-content-red'
New-Item -ItemType Directory -Path $result | Out-Null
& $vstest $recipe /Platform:x64 "/Logger:trx;LogFileName=focused.trx" "/ResultsDirectory:$result" "/TestCaseFilter:FullyQualifiedName~InspectionContentCardTests"
```

The expected class inventories remain `InspectionContentCardTests=25`, `InspectionActionCardTests=4`, `InspectionModelCardTests=9`, `InspectionDisclosureTests=12`, and `ModelInspectionRenderedStateTests=21`.

---

### Task 1: Lock and Implement Uniform Progress and Inspection Rows

**Files:**

- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionContentCardTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionRenderedStateTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionTheme.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml`

**Interfaces:**

- Consumes: existing `InspectionProgressRows`, `ProgressItemsRepeater`, `FindingsItemsRepeater`, `ExpandedReportItemsControl`, `ProgressStageTemplate`, `FindingRowTemplate`, and `ReportRowTemplate` identities.
- Produces: 24-pixel leading row column, 12-pixel copy inset, vertically centered glyph/copy/status, and equal group row heights at normal wide, compact, and representative 200% profiles.

- [ ] **Step 1: Extend the existing progress identity with loaded geometry assertions**

Inside `InitialFactoryState_RendersFiveWaitingRowsAndDisabledCancel`, retain all existing assertions and add the following checks after `rows` and `waitingLabels` are resolved:

```csharp
AssertUniformHeights(rows, "initial progress rows");
foreach (Grid row in rows)
{
    Assert.AreEqual(24d, row.ColumnDefinitions[0].ActualWidth, 0.01d,
        "the glyph/step-number column is fixed");
    FrameworkElement glyph = EnumerateDescendants(row)
        .OfType<Viewbox>()
        .Single(candidate => candidate.Name == "ProgressGlyphHost");
    StackPanel copy = EnumerateDescendants(row)
        .OfType<StackPanel>()
        .Single(candidate => candidate.Name == "ProgressCopyPanel");
    Grid status = EnumerateDescendants(row)
        .OfType<Grid>()
        .Single(candidate => candidate.Name == "ProgressStatusOwner");
    AssertVerticallyCentred(row, glyph, "progress glyph");
    AssertVerticallyCentred(row, copy, "progress copy");
    AssertVerticallyCentred(row, status, "progress status");
    Assert.AreEqual(12d, copy.Margin.Left, 0.01d,
        "copy begins at the approved inset after the glyph column");
}
```

Add these private helpers without adding a test identity:

```csharp
private static void AssertUniformHeights(
    IReadOnlyList<FrameworkElement> rows,
    string context)
{
    Assert.IsGreaterThan(0, rows.Count, context);
    double expected = rows.Max(row => row.ActualHeight);
    Assert.IsTrue(expected >= 48d, context);
    foreach (FrameworkElement row in rows)
    {
        Assert.AreEqual(expected, row.ActualHeight, 1d, context);
    }
}

private static void AssertVerticallyCentred(
    FrameworkElement row,
    FrameworkElement element,
    string context)
{
    Point origin = element.TransformToVisual(row).TransformPoint(default);
    double elementCentre = origin.Y + (element.ActualHeight / 2d);
    Assert.AreEqual(row.ActualHeight / 2d, elementCentre, 1d, context);
}
```

- [ ] **Step 2: Extend the existing disclosure identity for equal expanded rows and 200% growth**

In `DisclosurePairs_UseNaturalCollapsedAndBoundedExpandedGeometry`, obtain the visible `InspectionReportRow` borders from `ExpandedReportItemsControl`. Assert equal heights at the normal loaded size, then double the row title/detail font sizes using the test's existing representative-200% pattern, update layout, and assert equal heights again. Also assert every text block stays within its row bounds.

```csharp
ItemsControl reportItems = Assert.IsInstanceOfType<ItemsControl>(
    control.FindName("ExpandedReportItemsControl"));
Border[] reportRows = EnumerateDescendants(reportItems)
    .OfType<Border>()
    .Where(row => Equals(row.Tag, "InspectionReportRow"))
    .ToArray();
AssertUniformHeights(reportRows, "expanded inspection rows at normal text");

foreach (TextBlock text in reportRows
    .SelectMany(row => EnumerateDescendants(row).OfType<TextBlock>()))
{
    text.FontSize *= 2d;
}
control.UpdateLayout();
AssertUniformHeights(reportRows, "expanded inspection rows at 200 percent text");
foreach (Border row in reportRows)
{
    foreach (TextBlock text in EnumerateDescendants(row).OfType<TextBlock>())
    {
        Rect bounds = ElementBounds(text, row);
        Assert.IsTrue(bounds.Top >= -0.01d && bounds.Bottom <= row.ActualHeight + 0.01d,
            $"row text must not clip: {text.Text}");
    }
}
```

Use the same existing identity to assert `DisclosureHeaderLayout` has a 24-pixel first column, 12-pixel `ColumnSpacing`, a normal-scale height of at least 68 pixels, and vertically centered glyph/title/action centres.

- [ ] **Step 3: Run the exact content RED**

Build and run `InspectionContentCardTests`. Expected result: the existing inventory remains 25, with failures caused by the current 32-pixel progress column, zero copy-left margin, non-uniform repeated-item measurement, and/or off-centre loaded geometry. A compile error or changed discovery count is not an acceptable RED.

- [ ] **Step 4: Add the shared XAML measurement resources**

Add these non-theme-dependent resources beside the existing geometry resources in `ModelInspectionTheme.xaml`:

```xml
<x:Double x:Key="InspectionRepeatedRowGlyphColumnWidth">24</x:Double>
<x:Double x:Key="InspectionRepeatedRowCopyGap">12</x:Double>
<x:Double x:Key="InspectionRepeatedRowMinHeight">48</x:Double>
<x:Double x:Key="InspectionDisclosureHeaderMinHeight">68</x:Double>
<Thickness x:Key="InspectionRepeatedRowPadding">12,8,12,8</Thickness>
```

Do not replace the existing `InspectionProgressRowHeight` key if another unchanged control or test consumes it; keep it as a compatibility alias to the same value.

- [ ] **Step 5: Normalize the three repeated-row templates**

In `InspectionContentCard.xaml`:

- use `InspectionRepeatedRowGlyphColumnWidth` for the first column in all three templates;
- use `InspectionRepeatedRowCopyGap` as `ColumnSpacing` in findings/report templates;
- set glyph, copy stack, and status hosts to `VerticalAlignment="Center"`;
- retain all status-specific text bindings and brushes;
- set the progress copy panel's left margin to `12` while preserving its vertical insets;
- reduce `ProgressStatusOwner.Margin.Left` from `18` to `12`;
- retain the fraction column, motion targets, connectors, names, and automation properties.

Use a one-column `UniformGridLayout` for `ProgressItemsRepeater` so its realized rows share the largest measured item size while retaining `ElementPrepared` and `ElementClearing`:

```xml
<ItemsRepeater.Layout>
    <UniformGridLayout
        Orientation="Vertical"
        MaximumRowsOrColumns="1"
        MinColumnSpacing="0"
        MinRowSpacing="0"
        ItemsStretch="Fill" />
</ItemsRepeater.Layout>
```

Use a one-column uniform `ItemsWrapGrid` panel for the existing findings and report `ItemsControl` instances. Leave `ItemHeight` unset so native measure grows with text scale, and preserve the existing `ItemsControl` identities and item templates:

```xml
<ItemsControl.ItemsPanel>
    <ItemsPanelTemplate>
        <ItemsWrapGrid
            Orientation="Horizontal"
            MaximumRowsOrColumns="1" />
    </ItemsPanelTemplate>
</ItemsControl.ItemsPanel>
```

If the loaded RED/GREEN probe proves that `Horizontal` creates a single horizontal row on this WinUI version, switch only the panel orientation to `Vertical`; do not replace `ItemsControl` or alter observers. The acceptance condition is one stretched item per visual row with equal measured heights.

- [ ] **Step 6: Run the exact content GREEN and commit**

Run the 25-case content class. Require 25/25, unchanged discovery, equal normal/200% row geometry, centered status/glyph/copy, no clipping, and preserved disclosure behavior.

```powershell
git diff --check
git add -- `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionTheme.xaml' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionContentCardTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionRenderedStateTests.cs'
git diff --cached --check
git commit -m 'style(model-inspection): align repeated inspection rows'
```

---

### Task 2: Equalize Terminal Content-Card Spacing

**Files:**

- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionContentCardTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionRenderedStateTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionTheme.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml`

**Interfaces:**

- Consumes: existing terminal presentations for warning, unsupported, invalid, cancelled, and operational failure.
- Produces: one terminal-card rhythm: 18-pixel outer vertical inset, 12 pixels title-to-result, 8 pixels result-to-helper, 8 pixels helper-to-diagnostic, and vertically centered semantic result rows.

- [ ] **Step 1: Add terminal geometry assertions to the existing state DataRows**

Extend `FactoryTerminalStates_UseNaturalGeometryAndLeftAlignedLongCopy` without adding DataRows. Resolve `FindingsSectionTitle`, `FindingsRowsSurface`, `SupportingText`, `TertiaryText`, `DiagnosticCodeBorder`, and the final visible child. For each visible pair, assert the approved gaps with a 1-pixel layout tolerance:

```csharp
TextBlock sectionTitle = Assert.IsInstanceOfType<TextBlock>(
    control.FindName("FindingsSectionTitle"));
Border rowsSurface = Assert.IsInstanceOfType<Border>(
    control.FindName("FindingsRowsSurface"));
TextBlock supporting = Assert.IsInstanceOfType<TextBlock>(
    control.FindName("SupportingText"));
TextBlock tertiary = Assert.IsInstanceOfType<TextBlock>(
    control.FindName("TertiaryText"));
Border diagnostic = Assert.IsInstanceOfType<Border>(
    control.FindName("DiagnosticCodeBorder"));

Assert.AreEqual(18d, ElementBounds(sectionTitle, shell).Top, 1d,
    "terminal title top inset");
Assert.AreEqual(12d, VerticalGap(sectionTitle, rowsSurface, shell), 1d,
    "title to semantic row");
if (supporting.Visibility == Visibility.Visible)
{
    Assert.AreEqual(8d, VerticalGap(rowsSurface, supporting, shell), 1d,
        "semantic row to helper copy");
}
if (diagnostic.Visibility == Visibility.Visible)
{
    FrameworkElement previous = tertiary.Visibility == Visibility.Visible
        ? tertiary
        : supporting;
    Assert.AreEqual(8d, VerticalGap(previous, diagnostic, shell), 1d,
        "helper copy to diagnostic code");
}
```

Within every visible `InspectionFindingRow`, assert a 24-pixel leading column, 12-pixel column spacing, and vertically centered visible glyph/copy/status. At 480 pixels, assert the trailing status does not intersect the copy column and every element remains within the shell.

- [ ] **Step 2: Run the terminal-content RED**

Run `InspectionContentCardTests` and the 13-row `AllThirteenStates_RenderExactSemanticsTokensAndGeometry` identity. Require failures only on the current 20/24/4-pixel inconsistent terminal gaps or loaded centering. Preserve 25 and 21 class totals.

- [ ] **Step 3: Add terminal spacing resources and consume them**

Add these resources in `ModelInspectionTheme.xaml`:

```xml
<x:Double x:Key="InspectionTerminalOuterInset">18</x:Double>
<x:Double x:Key="InspectionTerminalTitleGap">12</x:Double>
<x:Double x:Key="InspectionTerminalElementGap">8</x:Double>
```

Apply the values in `InspectionContentCard.xaml`:

- `FindingsView.Padding="0,0,0,18"`;
- `FindingsSectionTitle.Margin="24,18,24,0"`;
- `FindingsRowsSurface.Margin="24,12,24,0"`;
- `SupportingText.Margin="24,8,24,0"` when visible;
- `TertiaryText.Margin="24,8,24,0"` when visible;
- `DiagnosticCodeBorder.Margin="72,8,24,0"`, aligning the code with the semantic-row copy start;
- the narrow VisualState repeats the same semantic measurements rather than restoring the old `20/10/4` values;
- terminal finding row uses 24-pixel leading column, 12-pixel copy gap, and centered status.

Do not change terminal text, status, diagnostic value, visibility, or technical-details behavior.

- [ ] **Step 4: Run terminal GREEN and commit**

Require `InspectionContentCardTests=25/25` and `ModelInspectionRenderedStateTests=21/21`. Verify every terminal state at 840 and 480 pixels, including operational failure and unsupported, and commit only the four listed files:

```powershell
git diff --check
git add -- `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionTheme.xaml' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionContentCardTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionRenderedStateTests.cs'
git diff --cached --check
git commit -m 'style(model-inspection): balance terminal card spacing'
```

---

### Task 3: Polish Result and Recovery Action Cards

**Files:**

- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionActionCardTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionRenderedStateTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionTheme.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionActionCard.xaml`

**Interfaces:**

- Consumes: existing `InspectionActionCardPresentation`, four button identities, native primary/secondary styles, responsive XAML states, and unchanged layout code-behind.
- Produces: consistent 18-pixel vertical padding, 6-pixel heading/helper gap, 12-pixel helper/action gap, centered helper copy, and stable disabled-action help spacing for every terminal state.

- [ ] **Step 1: Extend the existing action identities with exact loaded spacing**

In `ResultActions_KeepApprovedSlotOrderAndTargetSizes`, assert:

```csharp
Border result = Assert.IsInstanceOfType<Border>(control.FindName("ResultView"));
TextBlock heading = FindVisibleText(control, presentation.Title);
TextBlock helper = FindVisibleText(control, presentation.Message);
Grid panel = Assert.IsInstanceOfType<Grid>(control.FindName("ResultButtonPanel"));

Assert.AreEqual(new Thickness(20d, 18d, 20d, 18d), result.Padding);
Assert.AreEqual(6d, VerticalGap(heading, helper, result), 1d);
Assert.AreEqual(12d, VerticalGap(helper, panel, result), 1d);
Assert.AreEqual(result.ActualWidth / 2d, HorizontalCentre(heading, result), 1d);
Assert.AreEqual(result.ActualWidth / 2d, HorizontalCentre(helper, result), 1d);
```

For each visible future-help label, assert a 4-pixel button-to-help gap and centered bounds. In `ResultActions_ReflowAtExactClientBreakpointsWithoutReplacement`, retain 888/887/600/599 and the existing 260-pixel doubled-font coverage, and assert every visible host remains inside `ResultView` with equal button widths for the active band.

Add `using Microsoft.UI.Xaml.Media;` and these helpers to the existing class; they introduce no test identity:

```csharp
private static TextBlock FindVisibleText(DependencyObject root, string value) =>
    EnumerateDescendants(root)
        .OfType<TextBlock>()
        .Single(text => text.Visibility == Visibility.Visible && text.Text == value);

private static IEnumerable<DependencyObject> EnumerateDescendants(
    DependencyObject root)
{
    int count = VisualTreeHelper.GetChildrenCount(root);
    for (int index = 0; index < count; index++)
    {
        DependencyObject child = VisualTreeHelper.GetChild(root, index);
        yield return child;
        foreach (DependencyObject descendant in EnumerateDescendants(child))
        {
            yield return descendant;
        }
    }
}

private static double VerticalGap(
    FrameworkElement upper,
    FrameworkElement lower,
    UIElement root)
{
    Windows.Foundation.Point upperOrigin = upper.TransformToVisual(root)
        .TransformPoint(default);
    Windows.Foundation.Point lowerOrigin = lower.TransformToVisual(root)
        .TransformPoint(default);
    return lowerOrigin.Y - (upperOrigin.Y + upper.ActualHeight);
}

private static double HorizontalCentre(
    FrameworkElement element,
    UIElement root)
{
    Windows.Foundation.Point origin = element.TransformToVisual(root)
        .TransformPoint(default);
    return origin.X + (element.ActualWidth / 2d);
}
```

Extend the existing 13-state rendered identity to assert the same action-card outer padding and heading/helper/panel rhythm for cancelled, operational failure, unsupported, warning, and ready states. Do not add a test identity.

- [ ] **Step 2: Run the action RED**

Run `InspectionActionCardTests`. Expected result: exactly 4 tests discovered and geometry failures on the current `20,17` result padding and 14-pixel helper-to-panel margin. Existing binding, style, focus, visibility, and responsive assertions must continue to pass.

- [ ] **Step 3: Add and consume action rhythm resources**

Add to `ModelInspectionTheme.xaml`:

```xml
<Thickness x:Key="InspectionResultActionPadding">20,18,20,18</Thickness>
<x:Double x:Key="InspectionActionHeadingMessageGap">6</x:Double>
<x:Double x:Key="InspectionActionMessagePanelGap">12</x:Double>
<x:Double x:Key="InspectionActionFutureHelpGap">4</x:Double>
```

In `InspectionActionCard.xaml`:

- bind `ResultView.Padding` to `InspectionResultActionPadding`;
- retain centered heading/helper styles;
- keep helper top margin at 6;
- change `ResultButtonPanel.Margin` to `0,12,0,0`;
- retain 4-pixel future-help margin;
- preserve `InspectionSecondaryActionButtonStyle` based on `DefaultButtonStyle` and `InspectionPrimaryActionButtonStyle` based on `AccentButtonStyle`;
- preserve 46-pixel minimum height, `18,10` padding, 10-pixel corners, semantic brushes, semibold labels, and system focus visuals;
- do not edit `InspectionActionCard.xaml.cs`.

- [ ] **Step 4: Run action GREEN and commit**

Require `InspectionActionCardTests=4/4` and the action portions of `ModelInspectionRenderedStateTests=21/21`. Confirm the method/DataRow inventory is unchanged and commit only the four listed files:

```powershell
git diff --check
git add -- `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionTheme.xaml' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionActionCard.xaml' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionActionCardTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionRenderedStateTests.cs'
git diff --cached --check
git commit -m 'style(model-inspection): polish recovery action rhythm'
```

---

### Task 4: Verify Cross-Card Alignment and Preserve Accessibility

**Files:**

- Modify only if a RED proves a defect: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionDisclosure.xaml`
- Modify only if a RED proves a defect: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionDisclosureTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionModelCardTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageLayoutTests.cs`

**Interfaces:**

- Consumes: final Task 1-3 XAML and the existing 840/888/600/599 responsive contracts.
- Produces: evidence that adjacent cards share bounds, disclosure content is vertically centered, balanced overview remains 1.6:1, and normal/compact/200% layouts do not clip.

- [ ] **Step 1: Add assertions inside existing identities**

In the existing disclosure header identity, assert the loaded header is at least 68 pixels at normal text, grows naturally after doubling header text, keeps the 24-pixel glyph column, and keeps glyph/title/action centers within one pixel.

In `InspectionModelCardTests.ReadyCollapsed_UsesBalancedNaturalGeometryAndFieldOrder`, retain the 1.6:1 overview contract and assert the outer `LayoutRoot` width matches the content/action roots at 840 pixels. In `ModelInspectionPageLayoutTests.ResponsiveStates_DeclareExactClientBreakpointsAndInsets`, assert every visible top-level Model card has the same left/right bounds at 840, 600, and 480 pixels.

- [ ] **Step 2: Run the cross-card RED-or-GREEN gate**

Run `InspectionDisclosureTests`, `InspectionModelCardTests`, and `ModelInspectionPageLayoutTests`. If all assertions pass, do not edit the two verification-only production files. If a loaded assertion fails, make the smallest XAML-only margin/alignment correction and rerun the same gate.

- [ ] **Step 3: Verify accessibility mechanics**

Within the existing tests, prove keyboard focus on enabled disclosures/actions, collapsed content absent from hit testing and the accessible tree, all glyphs within their 20-by-20 hosts, and no text/status overlap after representative 200% font multiplication. Do not change strings, tab order, or automation names to make geometry pass.

For each of `Light`, `Dark`, and `HighContrast`, load the existing theme dictionary and assert that every brush used by the changed controls resolves through an existing `Inspection*Brush` key. Do not compare High Contrast to a literal RGB value. Retain the existing reduced-motion tests unchanged because this plan adds no animation or transition.

- [ ] **Step 4: Commit only actual changes**

If only tests changed, commit only those existing test files. If a loaded RED required one of the two verification-only XAML files, include only the proven file.

```powershell
git diff --check
$allowed = @(
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionDisclosure.xaml',
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml',
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionDisclosureTests.cs',
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionModelCardTests.cs',
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageLayoutTests.cs'
)
$changed = @(git diff --name-only)
$unexpected = @($changed | Where-Object { $_ -notin $allowed })
if ($unexpected.Count -ne 0) { throw "Unexpected Task 4 path: $($unexpected -join ', ')" }
git add -- $changed
git diff --cached --check
git commit -m 'test(model-inspection): lock native spacing alignment'
```

---

### Task 5: Full Regression, Identity, Scope, and Native Capture Gate

**Files:**

- No tracked production/test edit unless a regression is first reproduced in its focused owning test.
- Create ignored evidence only under `TestResults/ModelInspection/CommonHardwareTemplate/NativeSpacingPolish/`.

**Interfaces:**

- Consumes: committed Task 1-4 changes.
- Produces: a clean branch, a zero-regression packaged suite, exact identity preservation, and native WinUI captures for final user approval.

- [ ] **Step 1: Capture the baseline result-name multiset**

Use the last known all-green baseline TRX:

`TestResults/ModelInspection/CommonHardwareTemplate/Task5/RegressionFix/FullSuiteStrictFinal/task5-full-suite-strict-final.trx`

Require its SHA-256 to be `C117C8712BFA17A88C42B5F30DB792D71A09D1A82CA040161F170001D714F4A7` before using it. Extract every `UnitTestResult.testName`, sort with Ordinal semantics, and store the ignored baseline list in the new evidence directory.

- [ ] **Step 2: Run the focused visual gate**

Run the reusable build, then this focused filter:

```powershell
$filter = 'FullyQualifiedName~InspectionActionCardTests|FullyQualifiedName~InspectionContentCardTests|FullyQualifiedName~InspectionModelCardTests|FullyQualifiedName~InspectionOutcomeCardTests|FullyQualifiedName~InspectionStatusGlyphTests|FullyQualifiedName~InspectionDisclosureTests|FullyQualifiedName~ModelInspectionDisclosureTests|FullyQualifiedName~ModelInspectionPageLayoutTests|FullyQualifiedName~ModelInspectionPageNavigationTests|FullyQualifiedName~ModelInspectionRenderedStateTests|FullyQualifiedName~ModelInspectionAccessibilityTests|FullyQualifiedName~ModelInspectionFixturePresetTests|FullyQualifiedName~OnboardingStageIndicatorTests'
$result = Join-Path $repo 'TestResults\ModelInspection\CommonHardwareTemplate\NativeSpacingPolish\focused-final'
New-Item -ItemType Directory -Path $result | Out-Null
& $vstest $recipe /Platform:x64 "/Logger:trx;LogFileName=focused-final.trx" "/ResultsDirectory:$result" "/TestCaseFilter:$filter"
if ($LASTEXITCODE -ne 0) { throw 'Focused visual gate failed.' }
```

Require the pinned 188-case map from the existing visual-alignment plan and zero non-passing results.

- [ ] **Step 3: Run the full unfiltered packaged suite**

```powershell
$result = Join-Path $repo 'TestResults\ModelInspection\CommonHardwareTemplate\NativeSpacingPolish\full-final'
New-Item -ItemType Directory -Path $result | Out-Null
& $vstest $recipe /Platform:x64 "/Logger:trx;LogFileName=full-final.trx" "/ResultsDirectory:$result"
if ($LASTEXITCODE -ne 0) { throw 'Full packaged suite failed.' }
```

Require all discovered tests to pass. Compare the sorted final result-name multiset byte-for-byte with the verified baseline list; any missing, added, renamed, duplicated, or parameter-drifted result blocks completion.

- [ ] **Step 4: Verify production scope and protected identities**

```powershell
git diff --check 85084459e9544984b0815f40ddad1ed6150cee00..HEAD
git status --short
git diff --name-only 85084459e9544984b0815f40ddad1ed6150cee00..HEAD
```

Require a clean worktree. Require no production `.cs` path and no backend/shared/Hardware/ModelImport/Onboarding/project/App path in the implementation range. Compare the pre-implementation and final XAML `x:Name`, binding, command, VisualState, and automation-name multisets; they must be identical.

- [ ] **Step 5: Produce fresh native WinUI captures**

Use the already validated ignored native `RenderTargetBitmap` capture harness. Instantiate the exact committed `ModelInspectionPage`; do not render HTML. Capture progress, ready with expanded details, warning, operational failure/incomplete, unsupported, and cancelled at:

- 1440 by 1100 wide;
- 900 by 1000 medium;
- 480 by 900 compact;
- 720 by 900 representative 200% text.

Also capture one enabled action or disclosure with a visible keyboard-focus ring. Verify every PNG decodes at its filename dimensions, contains nontrivial color/content, and has no black edge-clipping sample. Record the exact HEAD and app-DLL SHA-256 with the capture set.

- [ ] **Step 6: Perform the visual oracle comparison and stop for user approval**

Compare the native captures against the approved localhost oracle for:

- uniform progress and expanded-detail rows;
- centered step names, helper copy, `Waiting`, and terminal statuses;
- 24-pixel glyph columns and 12-pixel copy gaps;
- 68-pixel centered summary/disclosure headers;
- equal terminal-card spacing;
- centered, modern native actions and help copy;
- aligned card edges and consistent 10/12/16/18/24 spacing rhythm;
- no compact or 200% overlap, truncation, clipping, or horizontal scrolling.

Do not merge. Present the fresh capture directory to the user and require explicit visual approval before any branch integration decision.
