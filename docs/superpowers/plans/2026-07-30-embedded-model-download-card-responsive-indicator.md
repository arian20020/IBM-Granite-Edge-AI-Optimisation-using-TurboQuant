# Embedded Model Download Card and Responsive Indicator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Embed the complete existing recommended-model card in the model-import page and make both the card and persistent onboarding indicator responsive without adding model-download behaviour.

**Architecture:** `ModelImportPage` will directly compose the reusable `ModelDownloadCard` inside its existing `ScrollViewer`; the shell-owned indicator remains outside page navigation and pinned below the stage frame. The card uses WinUI adaptive visual states for narrow layouts, while the indicator uses percentage-like ten-column geometry, auto-height labels, and whole-word wrapping.

**Tech Stack:** C# 12, .NET 8, WinUI 3, Windows App SDK 2.2, XAML adaptive visual states, MSTest 4.3.2, packaged app-container VSTest.

## Global Constraints

- Work only on `feature/onboarding-stage-indicator-local` and preserve every pre-existing staged, unstaged, and untracked user change.
- Do not stage, commit, push, update the pull request, switch branches, reset, or discard files during implementation; the user will review and correct the completed UI first.
- Do not add model catalogue, network, download, progress, cancellation, integrity-checking, or downloaded-model validation behaviour.
- `DownloadModelButton` remains visual-only and must not mutate model-import or onboarding state.
- `ContinueToModelInspectionButton` remains enabled exclusively by a successful local quick scan.
- Retain the indicator's dependency property, live-region notification, progress-state behaviour, colours, 40-pixel boxes, and connector fill transforms.
- Use the exact stage labels `Choose model`, `Inspect model`, `Check hardware fit`, `Configure model`, and `Ready to chat`.
- The indicator inner layout uses `MaxWidth="1200"`, 32-pixel side margins, `MinHeight="128"`, an auto-height label row with a 28-pixel minimum, whole-word wrapping, and no trimming.
- The card switches to its wide state at 720 window pixels and uses its narrow state below 720 pixels.
- The reviewed minimum window width is 500 pixels.
- Use `[UITestMethod]` and `[TestCategory("WinUI")]` for tests that construct WinUI objects.
- Execute packaged tests through the generated `.build.appxrecipe`; `dotnet test` is not completion evidence.
- Test semantic properties and relative layout behaviour, not screenshots or exact rendered pixels.

---

### Task 1: Characterize embedded-card composition and card presentation state

**Files:**
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelImportPageCompositionTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelDownload/ModelDownloadCardTests.cs`

**Interfaces:**
- Consumes: `ModelImportPage`, `ModelDownloadCard`, `ModelPreferenceSlider`, `ContinueToModelInspectionButton`, and the card's existing named slider and label.
- Produces: Failing UI tests that require direct page composition while preserving the card's existing slider-label presentation and local-readiness boundary.

- [ ] **Step 1: Add the failing page-composition tests**

Create `ModelImportPageCompositionTests`:

```csharp
using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelImport.ModelDownload;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ModelImportPageCompositionTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public void Constructor_EmbedsRecommendedModelCardWithoutChangingReadiness()
    {
        var page = new ModelImportPage();

        var downloadCard = page.FindName("RecommendedModelDownloadCard")
            as ModelDownloadCard;
        var continueButton = (Button)page.FindName(
            "ContinueToModelInspectionButton");

        Assert.IsNotNull(downloadCard);
        Assert.AreEqual(
            "Recommended model download option",
            AutomationProperties.GetName(downloadCard));
        Assert.IsNull(page.FindName("RecommendedModelDownloadButton"));
        Assert.IsFalse(page.HasValidatedModel);
        Assert.IsFalse(continueButton.IsEnabled);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void EmbeddedCardInteraction_DoesNotChangeLocalModelReadiness()
    {
        var page = new ModelImportPage();
        var downloadCard = (ModelDownloadCard)page.FindName(
            "RecommendedModelDownloadCard");
        var slider = (Slider)downloadCard.FindName("ModelScaleSlider");

        slider.Value = 85;

        Assert.IsNull(page.SelectedModelPath);
        Assert.IsNull(page.ValidatedScanResult);
        Assert.IsFalse(page.HasValidatedModel);
        Assert.IsFalse(
            ((Button)page.FindName(
                "ContinueToModelInspectionButton")).IsEnabled);
    }
}
```

- [ ] **Step 2: Add focused card presentation tests**

Create `ModelDownloadCardTests`:

```csharp
using GraniteEdgeAI.Features.ModelImport.ModelDownload;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ModelDownloadCardTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public void Constructor_DisplaysBalancedPreference()
    {
        var card = new ModelDownloadCard();

        Assert.AreEqual(
            50d,
            ((Slider)card.FindName("ModelScaleSlider")).Value);
        Assert.AreEqual(
            "Balanced",
            ((TextBlock)card.FindName("ModelScaleValueText")).Text);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void PreferenceSlider_MapsEveryBoundaryToExpectedLabel()
    {
        var card = new ModelDownloadCard();
        var slider = (Slider)card.FindName("ModelScaleSlider");
        var label = (TextBlock)card.FindName("ModelScaleValueText");
        (double Value, string Label)[] expectations =
        [
            (0, "Maximum efficiency"),
            (20, "Efficient"),
            (40, "Balanced"),
            (60, "High capability"),
            (80, "Maximum capability"),
            (100, "Maximum capability")
        ];

        foreach ((double value, string expectedLabel) in expectations)
        {
            slider.Value = value;
            Assert.AreEqual(expectedLabel, label.Text);
        }
    }
}
```

- [ ] **Step 3: Build and run the focused tests to verify RED**

Run:

```powershell
dotnet build `
  '.\tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj' `
  --configuration Debug `
  --runtime win-x64 `
  -p:Platform=x64
```

Then execute:

```powershell
$recipe = (Resolve-Path `
  '.\tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe').Path
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vstest = & $vswhere -latest -products * `
  -find '**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' |
  Select-Object -First 1
& $vstest $recipe '/Platform:x64' `
  '/TestCaseFilter:FullyQualifiedName~ModelImportPageCompositionTests|FullyQualifiedName~ModelDownloadCardTests'
```

Expected: both card-only tests pass, while the two page-composition tests fail because `RecommendedModelDownloadCard` is not yet present.

---

### Task 2: Embed the full card and retire the wrapper page

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelImportPage.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelImportPage.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj`
- Delete: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/RecommendedModelDownloadPage.xaml`
- Delete: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/RecommendedModelDownloadPage.xaml.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelImportPageCompositionTests.cs`

**Interfaces:**
- Consumes: The existing public `ModelDownloadCard` constructor and its internal presentation behaviour.
- Produces: A named `RecommendedModelDownloadCard` directly in `ModelImportPage`; no remaining navigation route to `RecommendedModelDownloadPage`.

- [ ] **Step 1: Register the model-download namespace on the import page**

Add this namespace to the `Page` element:

```xml
xmlns:modelDownload="using:GraniteEdgeAI.Features.ModelImport.ModelDownload"
```

- [ ] **Step 2: Replace the old navigation button with the complete card**

Replace the `RecommendedModelDownloadButton` element and all of its button
resources with:

```xml
<modelDownload:ModelDownloadCard
    x:Name="RecommendedModelDownloadCard"
    Margin="0,4,0,0"
    HorizontalAlignment="Stretch"
    AutomationProperties.Name="Recommended model download option" />
```

Keep the existing **or** separator immediately before the card and the Continue
button immediately after it.

- [ ] **Step 3: Set the reviewed bottom spacing**

Change the page's main `Border` from symmetric spacing to:

```xml
Margin="32,32,32,16"
Padding="48,48,48,24"
```

This leaves 24 pixels of internal padding plus 16 pixels of external margin
after Continue when the page is scrolled to the end.

- [ ] **Step 4: Remove obsolete navigation code**

Delete:

```csharp
using GraniteEdgeAI.Features.ModelImport.ModelDownload;
```

and:

```csharp
private void RecommendedModelDownloadButton_Click(
    object sender,
    RoutedEventArgs e)
{
    Frame.Navigate(typeof(RecommendedModelDownloadPage));
}
```

Keep the `Microsoft.UI.Xaml` import because the import-card event handlers still
consume `RoutedEventArgs`.

- [ ] **Step 5: Retire the unreachable wrapper page**

Delete `RecommendedModelDownloadPage.xaml` and
`RecommendedModelDownloadPage.xaml.cs`. Remove these exact project entries:

```xml
<None Remove="Features\ModelImport\ModelDownload\RecommendedModelDownloadPage.xaml" />
```

and:

```xml
<ItemGroup>
  <Page Update="Features\ModelImport\ModelDownload\RecommendedModelDownloadPage.xaml">
    <Generator>MSBuild:Compile</Generator>
  </Page>
</ItemGroup>
```

Do not remove the `ModelDownloadCard.xaml` page entry.

- [ ] **Step 6: Rebuild and run the focused composition tests**

Use the Task 1 build and VSTest commands.

Expected: all four focused methods pass, and the application project compiles
without a reference to `RecommendedModelDownloadPage`.

---

### Task 3: Make the full model download card responsive

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/ModelDownloadCard.xaml`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelDownload/ModelDownloadCardTests.cs`

**Interfaces:**
- Consumes: The card's existing static profile content and
  `ModelScaleSlider_ValueChanged`.
- Produces: `NarrowLayout` and `WideLayout` visual states with a 720-pixel
  window threshold, wrapping text, stacked narrow header, and two-column narrow
  facts.

- [ ] **Step 1: Extend the card test with semantic responsive requirements**

Add:

```csharp
[UITestMethod]
[TestCategory("WinUI")]
public void Constructor_ConfiguresTextForNarrowLayouts()
{
    var card = new ModelDownloadCard();
    string[] wrappingTextNames =
    [
        "ModelNameText",
        "ModelPackageSummaryText",
        "ModelDescriptionText",
        "EstimatedMemoryValueText",
        "ContextLengthValueText",
        "EfficiencyEndpointText",
        "CapabilityEndpointText"
    ];

    foreach (string elementName in wrappingTextNames)
    {
        var text = (TextBlock)card.FindName(elementName);
        Assert.AreEqual(TextWrapping.WrapWholeWords, text.TextWrapping);
        Assert.AreEqual(TextTrimming.None, text.TextTrimming);
    }

    Assert.IsNotNull(card.FindName("ModelHeaderGrid"));
    Assert.IsNotNull(card.FindName("ModelFactsGrid"));
}
```

Add `using Microsoft.UI.Xaml;` for `TextWrapping` and `TextTrimming`.

- [ ] **Step 2: Run the card tests to verify RED**

Build and run with:

```powershell
'/TestCaseFilter:FullyQualifiedName~ModelDownloadCardTests'
```

Expected: the new method fails because the required named elements and wrapping
settings are not present.

- [ ] **Step 3: Add adaptive layout states**

Name the root `Grid` `CardLayoutRoot` and add:

```xml
<VisualStateManager.VisualStateGroups>
    <VisualStateGroup x:Name="CardLayoutStates">
        <VisualState x:Name="NarrowLayout">
            <VisualState.StateTriggers>
                <AdaptiveTrigger MinWindowWidth="0" />
            </VisualState.StateTriggers>
            <VisualState.Setters>
                <Setter Target="ModelPackageSummaryText.(Grid.Row)" Value="1" />
                <Setter Target="ModelPackageSummaryText.(Grid.Column)" Value="0" />
                <Setter Target="ModelPackageSummaryText.HorizontalAlignment" Value="Left" />
                <Setter Target="ModelPackageSummaryText.TextAlignment" Value="Left" />
                <Setter Target="ModelPackageSummaryText.Margin" Value="0,4,0,0" />
                <Setter Target="FactsColumn2.Width" Value="0" />
                <Setter Target="FactsColumn3.Width" Value="0" />
                <Setter Target="EstimatedMemoryLabelText.(Grid.Row)" Value="2" />
                <Setter Target="EstimatedMemoryLabelText.(Grid.Column)" Value="0" />
                <Setter Target="EstimatedMemoryValueText.(Grid.Row)" Value="3" />
                <Setter Target="EstimatedMemoryValueText.(Grid.Column)" Value="0" />
                <Setter Target="ContextLengthLabelText.(Grid.Row)" Value="2" />
                <Setter Target="ContextLengthLabelText.(Grid.Column)" Value="1" />
                <Setter Target="ContextLengthValueText.(Grid.Row)" Value="3" />
                <Setter Target="ContextLengthValueText.(Grid.Column)" Value="1" />
            </VisualState.Setters>
        </VisualState>
        <VisualState x:Name="WideLayout">
            <VisualState.StateTriggers>
                <AdaptiveTrigger MinWindowWidth="720" />
            </VisualState.StateTriggers>
            <VisualState.Setters>
                <Setter Target="ModelPackageSummaryText.(Grid.Row)" Value="0" />
                <Setter Target="ModelPackageSummaryText.(Grid.Column)" Value="1" />
                <Setter Target="ModelPackageSummaryText.HorizontalAlignment" Value="Right" />
                <Setter Target="ModelPackageSummaryText.TextAlignment" Value="Right" />
                <Setter Target="ModelPackageSummaryText.Margin" Value="0" />
                <Setter Target="FactsColumn2.Width" Value="*" />
                <Setter Target="FactsColumn3.Width" Value="*" />
                <Setter Target="EstimatedMemoryLabelText.(Grid.Row)" Value="0" />
                <Setter Target="EstimatedMemoryLabelText.(Grid.Column)" Value="2" />
                <Setter Target="EstimatedMemoryValueText.(Grid.Row)" Value="1" />
                <Setter Target="EstimatedMemoryValueText.(Grid.Column)" Value="2" />
                <Setter Target="ContextLengthLabelText.(Grid.Row)" Value="0" />
                <Setter Target="ContextLengthLabelText.(Grid.Column)" Value="3" />
                <Setter Target="ContextLengthValueText.(Grid.Row)" Value="1" />
                <Setter Target="ContextLengthValueText.(Grid.Column)" Value="3" />
            </VisualState.Setters>
        </VisualState>
    </VisualStateGroup>
</VisualStateManager.VisualStateGroups>
```

- [ ] **Step 4: Prepare the header for both states**

Name the header grid `ModelHeaderGrid`, give it two auto rows, and name its
existing text blocks:

```xml
<Grid.RowDefinitions>
    <RowDefinition Height="Auto" />
    <RowDefinition Height="Auto" />
</Grid.RowDefinitions>
```

```xml
x:Name="ModelNameText"
TextWrapping="WrapWholeWords"
TextTrimming="None"
```

```xml
x:Name="ModelPackageSummaryText"
TextWrapping="WrapWholeWords"
TextTrimming="None"
```

- [ ] **Step 5: Prepare the facts grid for two or four columns**

Name the facts grid `ModelFactsGrid`. Name columns three and four:

```xml
<ColumnDefinition x:Name="FactsColumn2" Width="*" />
<ColumnDefinition x:Name="FactsColumn3" Width="*" />
```

Add two auto rows after the existing rows:

```xml
<RowDefinition Height="Auto" />
<RowDefinition Height="Auto" />
```

Name the third and fourth labels and values exactly:

```text
EstimatedMemoryLabelText
EstimatedMemoryValueText
ContextLengthLabelText
ContextLengthValueText
```

Set `TextWrapping="WrapWholeWords"` and `TextTrimming="None"` on the two named
values.

- [ ] **Step 6: Make remaining long text wrap**

Name and configure:

```text
ModelDescriptionText
EfficiencyEndpointText
CapabilityEndpointText
```

Each receives:

```xml
TextWrapping="WrapWholeWords"
TextTrimming="None"
```

- [ ] **Step 7: Build and run the focused card and composition tests**

Build the test project, then run:

```powershell
'/TestCaseFilter:FullyQualifiedName~ModelDownloadCardTests|FullyQualifiedName~ModelImportPageCompositionTests'
```

Expected: all focused methods pass. Treat any XAML compiler error as a failure
of the adaptive-state target syntax; correct the XAML rather than adding
code-behind layout state.

---

### Task 4: Specify the responsive indicator with failing tests

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/Controls/OnboardingStageIndicatorTests.cs`

**Interfaces:**
- Consumes: The existing five named labels and `OnboardingStageIndicator`.
- Produces: Tests requiring full label copy, whole-word wrapping, no trimming,
  a 1200-pixel layout cap, minimum rather than fixed height, and increased
  desired height when narrow.

- [ ] **Step 1: Update the fixed label expectations**

Change only the abbreviated labels in `Steps`:

```csharp
new("InspectModelStepBox", "InspectModelStepValue", "InspectModelStepLabel", "Inspect model"),
new("CheckFitStepBox", "CheckFitStepValue", "CheckFitStepLabel", "Check hardware fit"),
new("ConfigureModelStepBox", "ConfigureModelStepValue", "ConfigureModelStepLabel", "Configure model"),
```

- [ ] **Step 2: Add semantic layout coverage**

Add:

```csharp
[UITestMethod]
[TestCategory("WinUI")]
public void Constructor_ConfiguresResponsiveLabelsAndHeight()
{
    var indicator = new OnboardingStageIndicator();
    var layoutGrid = (Grid)indicator.FindName("IndicatorLayoutGrid");

    Assert.IsTrue(double.IsNaN(indicator.Height));
    Assert.AreEqual(128d, indicator.MinHeight);
    Assert.AreEqual(1200d, layoutGrid.MaxWidth);

    foreach (StepElementNames step in Steps)
    {
        TextBlock label = GetTextBlock(indicator, step.LabelName);
        Assert.AreEqual(TextWrapping.WrapWholeWords, label.TextWrapping);
        Assert.AreEqual(TextTrimming.None, label.TextTrimming);
        Assert.IsTrue(double.IsNaN(label.Width));
    }
}
```

- [ ] **Step 3: Add relative narrow-height coverage**

Add:

```csharp
[UITestMethod]
[TestCategory("WinUI")]
public void Measure_NarrowWidthRequestsMoreHeightWithoutLosingStages()
{
    var wideIndicator = new OnboardingStageIndicator();
    var narrowIndicator = new OnboardingStageIndicator();

    wideIndicator.Measure(new Windows.Foundation.Size(1200, double.PositiveInfinity));
    narrowIndicator.Measure(new Windows.Foundation.Size(500, double.PositiveInfinity));

    Assert.AreEqual(128d, wideIndicator.DesiredSize.Height);
    Assert.IsGreaterThan(
        wideIndicator.DesiredSize.Height,
        narrowIndicator.DesiredSize.Height);

    foreach (StepElementNames step in Steps)
    {
        Assert.AreEqual(
            step.Label,
            GetTextBlock(narrowIndicator, step.LabelName).Text);
    }
}
```

The MSTest assertion is ordered as
`Assert.IsGreaterThan(comparand, value)`; the code therefore asserts that the
narrow desired height is greater than 128.

- [ ] **Step 4: Build and run the indicator tests to verify RED**

Build the test project, then run:

```powershell
'/TestCaseFilter:FullyQualifiedName~OnboardingStageIndicatorTests'
```

Expected: fixed label expectations and responsive layout tests fail against the
current fixed-height, 934-pixel, no-wrap implementation.

---

### Task 5: Implement wider auto-height indicator geometry

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/Controls/OnboardingStageIndicator.xaml`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/Controls/OnboardingStageIndicatorTests.cs`

**Interfaces:**
- Consumes: Existing named boxes, values, labels, and connector
  `ScaleTransform`s used by `OnboardingStageIndicator.xaml.cs`.
- Produces: The same public indicator API and named elements with responsive
  layout-only changes.

- [ ] **Step 1: Allow the control and label row to grow**

Replace:

```xml
Height="128"
```

with:

```xml
MinHeight="128"
```

Name the inner grid and widen its cap:

```xml
<Grid
    x:Name="IndicatorLayoutGrid"
    MaxWidth="1200"
    Margin="32,0"
    HorizontalAlignment="Stretch">
```

Change the label row from fixed height to:

```xml
<RowDefinition
    Height="Auto"
    MinHeight="28" />
```

- [ ] **Step 2: Make label styling fluid**

Remove the label style's fixed `Width`. Set:

```xml
<Setter Property="HorizontalAlignment" Value="Stretch" />
<Setter Property="TextTrimming" Value="None" />
<Setter Property="TextWrapping" Value="WrapWholeWords" />
```

Retain centred horizontal text and vertical alignment.

- [ ] **Step 3: Replace the eleven-column track with ten equal columns**

Use:

```xml
<Grid.ColumnDefinitions>
    <ColumnDefinition Width="*" />
    <ColumnDefinition Width="*" />
    <ColumnDefinition Width="*" />
    <ColumnDefinition Width="*" />
    <ColumnDefinition Width="*" />
    <ColumnDefinition Width="*" />
    <ColumnDefinition Width="*" />
    <ColumnDefinition Width="*" />
    <ColumnDefinition Width="*" />
    <ColumnDefinition Width="*" />
</Grid.ColumnDefinitions>
```

Set the divider and eyebrow to `Grid.Column="0"` and
`Grid.ColumnSpan="10"`.

- [ ] **Step 4: Align boxes and labels in paired columns**

Use these column assignments for both each box and its matching label:

```text
Choose model       Grid.Column="0" Grid.ColumnSpan="2"
Inspect model      Grid.Column="2" Grid.ColumnSpan="2"
Check hardware fit Grid.Column="4" Grid.ColumnSpan="2"
Configure model    Grid.Column="6" Grid.ColumnSpan="2"
Ready to chat      Grid.Column="8" Grid.ColumnSpan="2"
```

Update the three abbreviated `Text` values to the exact full copy from the
global constraints.

- [ ] **Step 5: Connect adjacent step centres**

Use:

```text
ChooseToInspect connector Grid.Column="1" Grid.ColumnSpan="2"
InspectToFit connector    Grid.Column="3" Grid.ColumnSpan="2"
FitToConfigure connector  Grid.Column="5" Grid.ColumnSpan="2"
ConfigureToReady connector Grid.Column="7" Grid.ColumnSpan="2"
```

Apply `Margin="20,0"` to each connector container grid. Keep both base and blue
overlay rectangles and all existing named `ScaleTransform`s unchanged.

- [ ] **Step 6: Build and run all onboarding tests**

Build the test package, then run:

```powershell
'/TestCaseFilter:FullyQualifiedName~Onboarding'
```

Expected: all indicator, shell, and entry-point tests pass.

---

### Task 6: Full regression and worktree verification

**Files:**
- Verify every file changed by Tasks 1 through 5.

**Interfaces:**
- Consumes: Completed embedded-card and responsive-indicator implementation.
- Produces: Fresh build and packaged-test evidence plus a cleanly scoped,
  uncommitted diff for user correction.

- [ ] **Step 1: Validate XAML and whitespace**

Run:

```powershell
$xamlFiles = @(
  '.\IBM Granite with TurboQuant (Intel)\Features\ModelImport\ModelImportPage.xaml',
  '.\IBM Granite with TurboQuant (Intel)\Features\ModelImport\ModelDownload\ModelDownloadCard.xaml',
  '.\IBM Granite with TurboQuant (Intel)\Features\Onboarding\Controls\OnboardingStageIndicator.xaml'
)
foreach ($xamlFile in $xamlFiles)
{
    [xml](Get-Content -Raw -LiteralPath $xamlFile) | Out-Null
}
git diff --check
```

Expected: all XML reads succeed. `git diff --check` has no errors; the known
`MainWindow.xaml.cs` LF-to-CRLF warning remains informational if Git emits it.

- [ ] **Step 2: Build the application and packaged test project**

Run:

```powershell
dotnet build `
  '.\IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj' `
  --configuration Debug `
  --runtime win-x64 `
  -p:Platform=x64

dotnet build `
  '.\tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj' `
  --configuration Debug `
  --runtime win-x64 `
  -p:Platform=x64
```

Expected: both builds complete with zero errors.

- [ ] **Step 3: Run focused feature tests**

Run the packaged recipe with:

```powershell
'/TestCaseFilter:FullyQualifiedName~ModelImportPageCompositionTests|FullyQualifiedName~ModelDownloadCardTests|FullyQualifiedName~Onboarding'
```

Write a TRX file under `TestResults/Onboarding/Debug` and verify every focused
method passes.

- [ ] **Step 4: Run the complete packaged suite**

Run the same `.build.appxrecipe` without a test-case filter and write a
timestamped TRX under `TestResults/Onboarding/Debug`.

Expected: all discovered tests pass. Read the TRX `Counters` element and report
`total`, `executed`, `passed`, `failed`, and `error`.

- [ ] **Step 5: Inspect responsive layout at representative sizes**

Launch the application and inspect widths 500, 800, 1024, and 1440 pixels and
heights 720, 900, and 1080 pixels. Confirm:

```text
full card content remains reachable by vertical scrolling
no horizontal scrollbar appears
header and facts stack below 720 pixels
all five indicator labels are complete
labels wrap only when needed
indicator height grows without clipping
boxes and connectors remain centred
indicator remains pinned while the page scrolls
keyboard focus follows visible order
```

- [ ] **Step 6: Audit final scope**

Run:

```powershell
git status --short
git diff --check
git diff --stat
git diff --name-status
git diff --cached --name-only
```

Confirm the implementation is unstaged, the pre-existing onboarding and
application changes remain present, the design commit is untouched, and no
unrelated file was modified.
