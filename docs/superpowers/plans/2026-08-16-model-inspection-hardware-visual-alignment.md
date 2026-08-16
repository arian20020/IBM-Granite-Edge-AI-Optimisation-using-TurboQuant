# Model Inspection Hardware-Template Visual Alignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Align Model Inspection's XAML presentation and spacing with the approved Hardware Inspection template while leaving all Model Inspection content, behavior, workflow, logic, and acceptance criteria unchanged.

**Architecture:** Preserve every existing Model Inspection control instance, binding, name, VisualState identity, automation surface, and production C# path. Make the visual delta through the Model Inspection theme dictionary and existing control/page XAML, with same-identity visual-contract assertions proving the new spacing, canvas, card, responsive, theme, focus, and vector-glyph geometry. Do not edit Hardware Inspection or import its semantics.

**Tech Stack:** WinUI 3 / Windows App SDK 2.2; XAML resource dictionaries and VisualStates; C# 12 MSTest visual-contract tests; packaged AppContainer tests through Visual Studio `vstest.console.exe`; PowerShell 5.1-compatible repository gates.

---

## Source of truth and hard boundary

- Approved design: `docs/superpowers/specs/2026-08-16-model-inspection-hardware-visual-alignment-design.md`.
- Existing functional plan: `docs/superpowers/plans/2026-08-14-model-inspection-progress-and-visual-polish.md`.
- Read-only visual reference: `C:\Users\Arian\source\repos\IBM-Granite-TurboQuant-Intel\.superpowers\brainstorm\1777-1786719024\content\hardware-outcome-recovery-family-v2.html`.
- Required reference SHA-256: `24D34112F5BDA58A464032915C672EA825705880293BFFB88031CEB97F7B5286`.
- Execute on the current `refactor/model-inspection-cleanup` worktree. The user's explicit branch/worktree instruction overrides the generic isolated-worktree recommendation.
- Production edits are XAML/resource-dictionary only. Do not edit production `.cs`, models, ViewModels, services, runtime, worker, fixture JSON, content strings, commands, navigation, or Hardware Inspection files.
- Existing test methods and DataRows are extended in place. Discovery counts and behavioral assertions must not change.
- Preserve the protected untracked `.playwright-cli/` and `docs/superpowers/specs/2026-08-14-hardware-inspection-design.md` paths.

## File/responsibility map

### Production presentation

- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionTheme.xaml`: shared canvas, card, inset, radius, gap, type, Light/Dark/High Contrast resources.
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml`: theme-inheriting page canvas, 840 px centered host, 24/16 gutters, 24 px heading gap, 16 px conditional card rhythm.
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionOutcomeCard.xaml`: 24 px outcome insets and balanced 40/*/40 status geometry.
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml`: consistent compact/detailed card insets, metadata rhythm, and disclosure content inset.
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml`: progress/findings shell insets, nested report surface, technical-details spacing, and unchanged five-row geometry.
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionDisclosure.xaml`: verification-only full-width 44+ px disclosure target and retained collapsed/expanded layout contract.
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionActionCard.xaml`: 24 px result-card insets and centered existing count-driven action group.
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionStatusGlyph.xaml`: verification-only unless the same-coordinate-system center assertion identifies a real XAML geometry defect.

### Existing visual contracts

- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageLayoutTests.cs`
- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionOutcomeCardTests.cs`
- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionModelCardTests.cs`
- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionContentCardTests.cs`
- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionActionCardTests.cs`
- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionRenderedStateTests.cs`
- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionAccessibilityTests.cs`
- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/ModelInspectionFixturePresetTests.cs`

### Process registration

- `docs/reviews/model-inspection-cleanup-source-files.txt`: register this design and plan in exact Ordinal order.
- `docs/reviews/model-inspection-cleanup-inventory.md`: add the matching two reviewed rows and reconcile the current count.

## Reusable focused packaged command

Run this once after each test or XAML slice. Use a fresh result directory each time.

```powershell
$ErrorActionPreference = 'Stop'
$repo = 'C:\Users\Arian\source\repos\IBM-Granite-TurboQuant-Intel'
$app = Join-Path $repo 'IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj'
$tests = Join-Path $repo 'tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj'
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -products * -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
$vstest = & $vswhere -latest -products * -find '**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' | Select-Object -First 1

& $msbuild $app /target:Restore,Build /property:Configuration=Debug /property:Platform=x64
if ($LASTEXITCODE -ne 0) { throw 'Debug app build failed.' }
dotnet build $tests --configuration Debug --runtime win-x64 -p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw 'Debug test build failed.' }

$recipe = Join-Path $repo 'tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe'
$visualFilter = 'FullyQualifiedName~InspectionActionCardTests|FullyQualifiedName~InspectionContentCardTests|FullyQualifiedName~InspectionModelCardTests|FullyQualifiedName~InspectionOutcomeCardTests|FullyQualifiedName~InspectionStatusGlyphTests|FullyQualifiedName~InspectionDisclosureTests|FullyQualifiedName~ModelInspectionDisclosureTests|FullyQualifiedName~ModelInspectionPageLayoutTests|FullyQualifiedName~ModelInspectionPageNavigationTests|FullyQualifiedName~ModelInspectionRenderedStateTests|FullyQualifiedName~ModelInspectionAccessibilityTests|FullyQualifiedName~ModelInspectionFixturePresetTests|FullyQualifiedName~OnboardingStageIndicatorTests'
$resultRoot = Join-Path $repo 'TestResults\ModelInspectionPolish\HardwareVisualAlignment\focused-001'
New-Item -ItemType Directory -Path $resultRoot | Out-Null
& $vstest $recipe /Platform:x64 "/Logger:trx;LogFileName=visual.trx" "/ResultsDirectory:$resultRoot" "/TestCaseFilter:$visualFilter"
if ($LASTEXITCODE -ne 0) { throw 'Focused packaged visual gate failed.' }
```

The all-pass focused map remains exactly 188:

```text
Action 4; Content 25; Model 9; Outcome 7; StatusGlyph 16;
InspectionDisclosure 12; ModelInspectionDisclosure 5; PageLayout 4;
Navigation 39; RenderedState 21; Accessibility 9; FixturePreset 25;
Onboarding 12.
```

## Task 1: Capture the spacing/theme RED in existing test identities

**Files:**

- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageLayoutTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionOutcomeCardTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionModelCardTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionContentCardTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionActionCardTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionRenderedStateTests.cs`

- [ ] **Step 1: Extend the page/theme assertion without adding a test**

In `ResponsiveStates_DeclareExactClientBreakpointsAndInsets`, add these assertions after loading `resources`:

```csharp
Assert.AreEqual(840d, resources["InspectionContentColumnWidth"]);
Assert.AreEqual(12d,
    Assert.IsInstanceOfType<CornerRadius>(resources["InspectionCardCornerRadius"]).TopLeft);
Assert.AreEqual(new Thickness(24),
    Assert.IsInstanceOfType<Thickness>(resources["InspectionCardPadding"]));
foreach (string themeName in new[] { "Light", "Dark", "HighContrast" })
{
    Assert.IsTrue(
        Assert.IsInstanceOfType<ResourceDictionary>(
            resources.ThemeDictionaries[themeName])
        .ContainsKey("InspectionCanvasBrush"),
        themeName);
}

var themedPage = new ModelInspectionPage();
Assert.AreEqual(ElementTheme.Default, themedPage.RequestedTheme,
    "Model Inspection must inherit the application theme.");
Assert.AreSame(
    Assert.IsInstanceOfType<ResourceDictionary>(
        resources.ThemeDictionaries["Light"])["InspectionCanvasBrush"],
    Assert.IsInstanceOfType<Grid>(themedPage.FindName("LayoutRoot")).Background);
```

- [ ] **Step 2: Extend existing card geometry assertions**

Add these exact assertions inside the named existing methods; do not create TestMethods or DataRows:

```csharp
// InspectionOutcomeCardTests.Ready_UsesNaturalBalancedGeometryTypographyAndSuccessResources
Assert.AreEqual(new Thickness(24), card.Padding, "outcome card inset");
Assert.AreEqual(16d, layout.ColumnSpacing, 0.01, "outcome icon/copy gap");

// InspectionModelCardTests.ReadyCollapsed_UsesBalancedNaturalGeometryAndFieldOrder
Assert.AreEqual(new Thickness(24),
    Find<Border>(control, "CompactView").Padding,
    "compact card inset");
Assert.AreEqual(24d,
    Find<Grid>(control, "DetailedHeader").Margin.Left,
    0.01,
    "detailed header inset");
Assert.AreEqual(24d,
    Find<Grid>(control, "MetadataGrid").Margin.Left,
    0.01,
    "metadata inset");

// InspectionContentCardTests.InitialFactoryState_RendersFiveWaitingRowsAndDisabledCancel
Assert.AreEqual(new Thickness(24),
    Assert.IsInstanceOfType<Grid>(content.FindName("ProgressView")).Padding);

// InspectionActionCardTests.ResultActions_KeepApprovedSlotOrderAndTargetSizes
Assert.AreEqual(new Thickness(24), result.Padding, "action card inset");
```

In the existing findings/rendered-state identities, assert every visible findings header/list/body/report viewport has at least 24 px from the card's inner left/right edges and retains natural height at 200%.

In `AllThirteenStates_RenderExactSemanticsTokensAndGeometry`, replace the page-background expectation only:

```csharp
AssertBrushColor(
    "InspectionCanvasBrush",
    Element<Grid>(page, "LayoutRoot").Background);
```

Keep all card-surface and outcome-tone expectations on their existing semantic resources.

- [ ] **Step 3: Add the requested 900 px and 480 px endpoints without changing discovery**

Change the existing matrix constant only:

```csharp
private static readonly double[] ResponsiveWidths =
    [1440d, 900d, 888d, 887d, 600d, 599d, 480d, 360d];
```

Keep the same 13 DataRows and existing pre-initialization 200% loop.

- [ ] **Step 4: Run the focused command and prove a behavioral RED**

Run the reusable focused packaged command with result root `focused-red`. Expected: exact discovery remains 188; only new presentation assertions fail, including forced Light theme, missing canvas resource, and inconsistent card insets. Any semantic, navigation, timing, command, or fixture-content failure is an invalid RED and must be corrected test-side before production edits.

- [ ] **Step 5: Commit the test-only RED**

```powershell
git add -- `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageLayoutTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionOutcomeCardTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionModelCardTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionContentCardTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionActionCardTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionRenderedStateTests.cs'
git commit -m 'test(model-inspection): lock hardware template spacing'
```

## Task 2: Align the page frame and semantic theme resources

**Files:**

- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionTheme.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml`

- [ ] **Step 1: Add one canvas brush to each theme dictionary**

Add the same key alongside the existing surface resources:

```xml
<!-- Light -->
<SolidColorBrush x:Key="InspectionCanvasBrush" Color="#F6F8FB" />

<!-- Dark -->
<SolidColorBrush x:Key="InspectionCanvasBrush" Color="#0C111D" />

<!-- HighContrast -->
<SolidColorBrush x:Key="InspectionCanvasBrush"
                 Color="{ThemeResource SystemColorWindowColor}" />
```

Retain the existing `InspectionSurfaceBrush` for white/dark card surfaces. Do not replace semantic success, warning, error, information, or focus brushes.

- [ ] **Step 2: Centralize the existing approved spacing values**

Keep or add these exact resources at the bottom of `ModelInspectionTheme.xaml`:

```xml
<x:Double x:Key="InspectionContentColumnWidth">840</x:Double>
<x:Double x:Key="InspectionCardGap">16</x:Double>
<x:Double x:Key="InspectionHeaderToCardGap">24</x:Double>
<x:Double x:Key="InspectionProgressHeadingGap">16</x:Double>
<x:Double x:Key="InspectionProgressRowHeight">48</x:Double>
<Thickness x:Key="InspectionCardPadding">24</Thickness>
<Thickness x:Key="InspectionDesktopPageMargin">24,28,24,32</Thickness>
<Thickness x:Key="InspectionCompactPageMargin">16,24,16,24</Thickness>
<CornerRadius x:Key="InspectionCardCornerRadius">12</CornerRadius>
```

Do not add a second competing spacing system.

- [ ] **Step 3: Make the page inherit theme and consume the canvas/margin tokens**

Remove `RequestedTheme="Light"` from the root Page, set `LayoutRoot.Background` to `InspectionCanvasBrush`, and use the centralized margins:

```xml
<Setter Target="InspectionContentHost.Margin"
        Value="{StaticResource InspectionDesktopPageMargin}" />
```

for Desktop and Medium; use `InspectionCompactPageMargin` for Narrow and as the initial `InspectionContentHost.Margin`. Keep all nine rows, conditional spacer elements, named cards, and the outgoing-progress overlay unchanged.

- [ ] **Step 4: Run focused page/render tests**

Use the reusable command with filter reduced to `ModelInspectionPageLayoutTests|ModelInspectionRenderedStateTests|ModelInspectionAccessibilityTests`; expect 34/34 pass with class counts 4/21/9.

- [ ] **Step 5: Commit the page/theme slice**

```powershell
git add -- `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionTheme.xaml' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml'
git commit -m 'style(model-inspection): align page canvas and rhythm'
```

## Task 3: Normalize card, disclosure, and action spacing in XAML

**Files:**

- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionOutcomeCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml`
- Verify unchanged: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionDisclosure.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionActionCard.xaml`
- Verify unchanged: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionStatusGlyph.xaml`

- [ ] **Step 1: Normalize primary card insets**

Apply the shared padding directly:

```xml
<!-- InspectionOutcomeCard.xaml -->
<Border x:Name="OutcomeCardBorder"
        Padding="{StaticResource InspectionCardPadding}"
        BorderThickness="1"
        CornerRadius="{StaticResource InspectionCardCornerRadius}">

<!-- InspectionModelCard.xaml -->
<Border x:Name="CompactView"
        Padding="{StaticResource InspectionCardPadding}"
        BorderThickness="1"
        CornerRadius="{StaticResource InspectionCardCornerRadius}">

<!-- InspectionActionCard.xaml -->
<Border x:Name="ResultView"
        Padding="{StaticResource InspectionCardPadding}"
        BorderThickness="1"
        CornerRadius="{StaticResource InspectionCardCornerRadius}">
```

Keep every existing binding, command, automation property, and tone VisualState.

- [ ] **Step 2: Normalize detailed-model and findings insets**

Use 24 px horizontal card insets consistently:

```xml
<Grid x:Name="DetailedHeader" Margin="24,0" ColumnSpacing="20">
<Grid x:Name="MetadataGrid" Margin="24,0">

<TextBlock x:Name="FindingsSectionTitle" Margin="24,20,24,0" />
<ItemsRepeater x:Name="FindingsItemsRepeater" Margin="24,12,24,0" />
<TextBlock x:Name="SupportingText" Margin="24,0,24,0" />
<TextBlock x:Name="TertiaryText" Margin="24,10,24,0" />
<Border x:Name="DiagnosticCodeBorder" Margin="24,4,24,0" />
<Border x:Name="ExpandedReportViewport" Margin="24,0,24,24" />
```

Preserve metadata field order, responsive 4/2/1 columns, report bounds, and all Model Inspection text.

- [ ] **Step 3: Preserve disclosure/action accessibility while matching spacing**

Keep `DisclosureToggleButton` full width with `MinHeight` bound to the existing 58 px header baseline; keep its chevron target 44x44 and collapsed viewport `Visibility="Collapsed"`. In the owning card XAML, set `InspectionDetailsHeader.Padding="24,0"`, `FindingsDisclosure` header padding to `24,0,0,0`, `TechnicalDetailsFutureHelpText.Margin="24,0,12,0"`, and `TechnicalDetailsButton.Margin="12,7,24,7"`. Keep nested report content padding between 16 and 24 px. Keep action buttons at the existing 46 px minimum, centered content, current semantic order, and the existing count-driven 1/2/3 placement.

The following must remain verbatim in structure:

```xml
<Button x:Name="DisclosureToggleButton"
        MinHeight="{x:Bind HeaderMinHeight, Mode=OneWay}"
        HorizontalAlignment="Stretch"
        IsTabStop="False"
        UseSystemFocusVisuals="True" />

<Button x:Name="PrimaryActionButton"
        MinWidth="{StaticResource InspectionMinimumTargetSize}"
        MinHeight="{StaticResource InspectionStandardButtonHeight}"
        HorizontalContentAlignment="Center"
        UseSystemFocusVisuals="True" />
```

- [ ] **Step 4: Verify complete vector geometry without changing semantics**

Confirm `InspectionStatusGlyph.xaml` still owns each circle/ring and its tick, X, warning, or information mark within the same 32x32 coordinate system. If all existing 16 glyph tests pass and centered bounds remain within one effective pixel, leave the file untouched. Do not replace vectors with `FontIcon`, `SymbolIcon`, or a separately positioned mark.

- [ ] **Step 5: Run the exact 188 GREEN and commit the XAML slice**

Run the reusable packaged command with a fresh `focused-green` root. Require 188/188, exact class map, zero failed/error/timeout/aborted/notExecuted results, and unchanged test identities.

```powershell
git add -- `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionOutcomeCard.xaml' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionActionCard.xaml'
git commit -m 'style(model-inspection): normalize card spacing'
```

## Task 4: Close fixtures, governance, and unchanged-behavior evidence

**Files:**

- Modify only if an existing same-identity visual assertion needs the approved geometry: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionAccessibilityTests.cs`
- Modify only if the existing 25-test loaded gallery oracle needs the approved geometry: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/ModelInspectionFixturePresetTests.cs`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

- [ ] **Step 1: Run the repository Debug gate on final XAML**

```powershell
$runRoot = 'TestResults/ModelInspectionPolish/HardwareVisualAlignment/debug-final'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File `
  'scripts/model-inspection/Invoke-ModelInspectionProgressPolishGate.ps1' `
  -Phase Debug `
  -RunRoot $runRoot
```

Require exact all-pass totals: Interaction/Lifetime 19, FixtureCategory 220, FocusedPolish 322. This covers all 50 Model Inspection fixture surfaces, existing behavior, disclosure/focus order, theme resources, reduced motion, and the full polish map.

- [ ] **Step 2: Register the two planning documents, then run Contracts and cleanup verification**

Insert these two paths in exact Ordinal order in `model-inspection-cleanup-source-files.txt` and add matching reviewed rows to `model-inspection-cleanup-inventory.md`:

```text
docs/superpowers/plans/2026-08-16-model-inspection-hardware-visual-alignment.md
docs/superpowers/specs/2026-08-16-model-inspection-hardware-visual-alignment-design.md
```

Update only the current source/ledger cardinality from 625/625 to 627/627; retain every historical reconciliation statement verbatim.

```powershell
dotnet test `
  'tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj' `
  --configuration Release `
  --minimum-expected-tests 357 `
  --results-directory 'TestResults/ModelInspectionPolish/HardwareVisualAlignment/contracts-final' `
  --report-trx `
  --report-trx-filename 'contracts.trx'

powershell.exe -NoProfile -ExecutionPolicy Bypass -File `
  'scripts/model-inspection/Verify-ModelInspectionCleanupInventory.ps1'
```

Require Contracts 357/357 and cleanup 3/3. Register the new design and plan paths in the source list and inventory without rewriting historical evidence.

- [ ] **Step 3: Recheck the immutable Hardware reference and XAML-only production scope**

```powershell
$reference = 'C:\Users\Arian\source\repos\IBM-Granite-TurboQuant-Intel\.superpowers\brainstorm\1777-1786719024\content\hardware-outcome-recovery-family-v2.html'
if ((Get-FileHash -LiteralPath $reference -Algorithm SHA256).Hash -cne
    '24D34112F5BDA58A464032915C672EA825705880293BFFB88031CEB97F7B5286') {
    throw 'Hardware visual reference changed.'
}

$productionDiff = @(git diff --name-only af43a45f..HEAD -- 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection')
$nonXaml = @($productionDiff | Where-Object { $_ -notmatch '\.xaml$' })
if ($nonXaml.Count -ne 0) { throw "Non-XAML production changes: $($nonXaml -join ', ')" }

git diff --check
git status --short
```

Require no Hardware path in the diff, no production `.cs` path, no unmerged files, and only the protected pre-existing untracked paths outside the planned scope.

- [ ] **Step 4: Obtain sequential spec and quality review**

The spec review must confirm Hardware-template visual parity without semantics drift. The quality review must inspect XAML resource lookup, responsive/natural-height behavior, 200% clipping, complete vector geometry, Light/Dark/High Contrast, minimum targets, focus, and reduced motion. Resolve any finding test-first inside this same XAML-only boundary.

- [ ] **Step 5: Commit final verification/registration changes**

```powershell
git add -- `
  'docs/reviews/model-inspection-cleanup-source-files.txt' `
  'docs/reviews/model-inspection-cleanup-inventory.md'
git commit -m 'docs(model-inspection): register hardware visual alignment'
```

Final handoff must report separately:

1. Presentation/template files changed.
2. Behaviors and feature intentions explicitly preserved.
3. Hardware Inspection files unchanged and reference hash unchanged.
4. Model Inspection plan, logic, content, and acceptance criteria unchanged.
