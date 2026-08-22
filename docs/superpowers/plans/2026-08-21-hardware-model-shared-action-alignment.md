# Hardware and Model Shared Action Alignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply the approved Hardware alignment corrections to every Hardware state and make Model Inspection and Hardware Inspection journey actions use the exact established Model Import primary palette and matching secondary template.

**Architecture:** Add one locally merged XAML palette containing shared action geometry and semantic brushes. Model Inspection maps its declared buttons to the palette in XAML; Hardware Inspection maps its dynamically created presentation buttons to the same resources in its control code-behind. Correct page/details alignment only in the Hardware-owned page and shared details control, then prove exhaustive state coverage through the existing 15-state Hardware and 13-state Model test identities.

**Tech Stack:** C# 12, .NET 8, WinUI 3 / Windows App SDK 2.2, XAML resource dictionaries, MSTest AppContainer, Visual Studio VSTest.

---

## File map

**Create**

- `IBM Granite with TurboQuant (Intel)/Features/Onboarding/Presentation/GraniteJourneyActionPalette.xaml` — single source for approved action geometry and primary/secondary colours.

**Modify production**

- `IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj` — compile the shared resource dictionary.
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionActionCard.xaml` — consume shared tokens for every declared Model action without changing bindings or layout.
- `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Controls/HardwareInspectionActionCard.xaml` — consume shared action styles.
- `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Controls/HardwareInspectionActionCard.xaml.cs` — apply native interaction resources to dynamically generated buttons only.
- `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Controls/HardwareInspectionDetailsCard.xaml` — centre all row glyphs and make both disclosure headers visually consistent and full width.
- `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/HardwareInspectionPage.xaml` — centre the shared page heading/subtitle.

**Modify existing tests without adding public test identities**

- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/Controls/ImportModelCardTests.cs`
- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionActionCardTests.cs`
- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionRenderedStateTests.cs`
- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionTerminalCardTests.cs`
- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionDetailsSummaryTests.cs`
- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionPageTests.cs`

## Task 1: Lock the Model Import palette and create shared tokens

- [ ] **Step 1: Extend an existing Model Import test identity with the visual source contract**

Inside `ImportModelCardTests.ScanSucceeded_DisplaysImportedCardValues`, load `BrowseFilesButton` and add the exact assertions:

```csharp
Button browse = (Button)card.FindName("BrowseFilesButton");
Assert.AreEqual(46d, browse.Height);
Assert.AreEqual(new CornerRadius(11), browse.CornerRadius);
Assert.AreEqual(FontWeights.SemiBold.Weight, browse.FontWeight.Weight);
Assert.AreEqual(Color.FromArgb(0xFF, 0x25, 0x63, 0xEB),
    ((SolidColorBrush)browse.Resources["ButtonBackground"]).Color);
Assert.AreEqual(Color.FromArgb(0xFF, 0x1D, 0x4E, 0xD8),
    ((SolidColorBrush)browse.Resources["ButtonBackgroundPointerOver"]).Color);
Assert.AreEqual(Color.FromArgb(0xFF, 0x1E, 0x40, 0xAF),
    ((SolidColorBrush)browse.Resources["ButtonBackgroundPressed"]).Color);
Assert.AreEqual(Color.FromArgb(0xFF, 0xE5, 0xE7, 0xEB),
    ((SolidColorBrush)browse.Resources["ButtonBackgroundDisabled"]).Color);
Assert.AreEqual(Color.FromArgb(0xFF, 0x9C, 0xA3, 0xAF),
    ((SolidColorBrush)browse.Resources["ButtonForegroundDisabled"]).Color);
Assert.AreEqual(Color.FromArgb(0xFF, 0xD1, 0xD5, 0xDB),
    ((SolidColorBrush)browse.Resources["ButtonBorderBrushDisabled"]).Color);
Assert.IsTrue(browse.UseSystemFocusVisuals);
```

Add `using Microsoft.UI;`, `using Microsoft.UI.Text;`, and `using Microsoft.UI.Xaml.Media;` if absent.

- [ ] **Step 2: Run the unchanged-source contract as a baseline**

Run:

```powershell
dotnet build "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" -c Debug -p:Platform=x64 --no-restore
& "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe" `
  "tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe" `
  /TestCaseFilter:"FullyQualifiedName~ImportModelCardTests.ScanSucceeded_DisplaysImportedCardValues" `
  /Logger:"trx;LogFileName=model-import-palette-baseline.trx" `
  /ResultsDirectory:"TestResults\InspectionAlignment\Task1-Baseline"
```

Expected: the existing Model Import visual source assertion passes. This is a protected baseline, not the feature RED.

- [ ] **Step 3: Add failing shared-palette assertions to the existing Model and Hardware style tests**

In `InspectionActionCardTests.ResultActions_KeepApprovedSlotOrderAndTargetSizes` and `HardwareInspectionTerminalCardTests.ActionCard_UsesApprovedModernPrimaryAndSecondaryStyles`, change the expected geometry from 44/10 to 46/11 and assert that primary/secondary resources named below exist. Use this helper in each test class:

```csharp
private static Color BrushColor(FrameworkElement owner, string key) =>
    ((SolidColorBrush)owner.Resources[key]).Color;
```

Expected shared keys:

```csharp
Assert.AreEqual(Color.FromArgb(0xFF, 0x25, 0x63, 0xEB),
    BrushColor(control, "GraniteJourneyPrimaryBackgroundBrush"));
Assert.AreEqual(Color.FromArgb(0xFF, 0x1D, 0x4E, 0xD8),
    BrushColor(control, "GraniteJourneyPrimaryPointerOverBrush"));
Assert.AreEqual(Color.FromArgb(0xFF, 0x1E, 0x40, 0xAF),
    BrushColor(control, "GraniteJourneyPrimaryPressedBrush"));
Assert.AreEqual(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF),
    BrushColor(control, "GraniteJourneySecondaryBackgroundBrush"));
Assert.AreEqual(Color.FromArgb(0xFF, 0xC9, 0xD7, 0xE8),
    BrushColor(control, "GraniteJourneySecondaryBorderBrush"));
```

- [ ] **Step 4: Run the two action-card classes and capture the valid RED**

Run the build and VSTest recipe from Step 2 with:

```text
/TestCaseFilter:"FullyQualifiedName~InspectionActionCardTests|FullyQualifiedName~HardwareInspectionTerminalCardTests"
```

Expected: existing test identities fail only because the shared resource keys are absent and Hardware still resolves 44 px / 10 px.

- [ ] **Step 5: Create the shared resource dictionary**

Create `Features/Onboarding/Presentation/GraniteJourneyActionPalette.xaml` with exactly:

```xml
<?xml version="1.0" encoding="utf-8"?>
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <x:Double x:Key="GraniteJourneyActionHeight">46</x:Double>
    <Thickness x:Key="GraniteJourneyActionPadding">18,10,18,10</Thickness>
    <CornerRadius x:Key="GraniteJourneyActionCornerRadius">11</CornerRadius>

    <SolidColorBrush x:Key="GraniteJourneyPrimaryBackgroundBrush" Color="#2563EB" />
    <SolidColorBrush x:Key="GraniteJourneyPrimaryPointerOverBrush" Color="#1D4ED8" />
    <SolidColorBrush x:Key="GraniteJourneyPrimaryPressedBrush" Color="#1E40AF" />
    <SolidColorBrush x:Key="GraniteJourneyPrimaryForegroundBrush" Color="#FFFFFF" />

    <SolidColorBrush x:Key="GraniteJourneySecondaryBackgroundBrush" Color="#FFFFFF" />
    <SolidColorBrush x:Key="GraniteJourneySecondaryPointerOverBrush" Color="#F8FAFC" />
    <SolidColorBrush x:Key="GraniteJourneySecondaryPressedBrush" Color="#EEF2F7" />
    <SolidColorBrush x:Key="GraniteJourneySecondaryForegroundBrush" Color="#111827" />
    <SolidColorBrush x:Key="GraniteJourneySecondaryBorderBrush" Color="#C9D7E8" />
    <SolidColorBrush x:Key="GraniteJourneySecondaryPointerOverBorderBrush" Color="#94A3B8" />
    <SolidColorBrush x:Key="GraniteJourneySecondaryPressedBorderBrush" Color="#64748B" />

    <SolidColorBrush x:Key="GraniteJourneyDisabledBackgroundBrush" Color="#E5E7EB" />
    <SolidColorBrush x:Key="GraniteJourneyDisabledForegroundBrush" Color="#9CA3AF" />
    <SolidColorBrush x:Key="GraniteJourneyDisabledBorderBrush" Color="#D1D5DB" />
</ResourceDictionary>
```

- [ ] **Step 6: Register only the shared dictionary in the application project**

Add to the existing XAML `None Remove` item group:

```xml
<None Remove="Features\Onboarding\Presentation\GraniteJourneyActionPalette.xaml" />
```

Add an adjacent page item group:

```xml
<ItemGroup>
  <Page Update="Features\Onboarding\Presentation\GraniteJourneyActionPalette.xaml">
    <Generator>MSBuild:Compile</Generator>
  </Page>
</ItemGroup>
```

- [ ] **Step 7: Commit the shared contract**

```powershell
git add -- `
  "IBM Granite with TurboQuant (Intel)/Features/Onboarding/Presentation/GraniteJourneyActionPalette.xaml" `
  "IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj" `
  "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/Controls/ImportModelCardTests.cs" `
  "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionActionCardTests.cs" `
  "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionTerminalCardTests.cs"
git commit -m "style(inspection): define shared journey action palette"
```

The Model/Hardware tests remain RED until Tasks 2 and 3 consume the palette.

## Task 2: Apply the palette to every Model Inspection action

- [ ] **Step 1: Merge the shared palette into `InspectionActionCard.xaml`**

Replace the simple `UserControl.Resources` body with a `ResourceDictionary` that merges both the existing Model theme inherited through app resources and:

```xml
<ResourceDictionary.MergedDictionaries>
    <ResourceDictionary Source="ms-appx:///Features/Onboarding/Presentation/GraniteJourneyActionPalette.xaml" />
</ResourceDictionary.MergedDictionaries>
```

Keep every existing text style and action style key.

- [ ] **Step 2: Update both existing action styles without changing their native bases**

Set both styles to:

```xml
<Setter Property="MinHeight" Value="{StaticResource GraniteJourneyActionHeight}" />
<Setter Property="Padding" Value="{StaticResource GraniteJourneyActionPadding}" />
<Setter Property="CornerRadius" Value="{StaticResource GraniteJourneyActionCornerRadius}" />
<Setter Property="FontWeight" Value="SemiBold" />
<Setter Property="HorizontalContentAlignment" Value="Center" />
<Setter Property="VerticalContentAlignment" Value="Center" />
<Setter Property="UseSystemFocusVisuals" Value="True" />
```

Primary normal colours use the shared primary background/border/foreground. Secondary normal colours use the shared secondary background/border/foreground. Preserve `BasedOn="{StaticResource AccentButtonStyle}"` for primary and `BasedOn="{StaticResource DefaultButtonStyle}"` for secondary.

- [ ] **Step 3: Map native interaction resource slots on every declared Model action**

For `PrimaryActionButton`, add:

```xml
<Button.Resources>
    <StaticResource x:Key="ButtonBackgroundPointerOver" ResourceKey="GraniteJourneyPrimaryPointerOverBrush" />
    <StaticResource x:Key="ButtonBorderBrushPointerOver" ResourceKey="GraniteJourneyPrimaryPointerOverBrush" />
    <StaticResource x:Key="ButtonBackgroundPressed" ResourceKey="GraniteJourneyPrimaryPressedBrush" />
    <StaticResource x:Key="ButtonBorderBrushPressed" ResourceKey="GraniteJourneyPrimaryPressedBrush" />
    <StaticResource x:Key="ButtonBackgroundDisabled" ResourceKey="GraniteJourneyDisabledBackgroundBrush" />
    <StaticResource x:Key="ButtonForegroundDisabled" ResourceKey="GraniteJourneyDisabledForegroundBrush" />
    <StaticResource x:Key="ButtonBorderBrushDisabled" ResourceKey="GraniteJourneyDisabledBorderBrush" />
</Button.Resources>
```

For `CancelActionButton`, `SecondaryActionOneButton`, and `SecondaryActionTwoButton`, use the secondary pointer-over/pressed background and border resources plus the same disabled resources. Do not change any button name, binding, command, command parameter, Tag, TabIndex, visibility, help, host, or grid placement.

- [ ] **Step 4: Verify Model GREEN**

Run the focused `InspectionActionCardTests` class. Expected: exact existing identity count and all passes. Then run `ModelInspectionRenderedStateTests`; expected: all 13 state rows pass with unchanged action labels/order and the new palette.

- [ ] **Step 5: Commit Model consumption**

```powershell
git add -- `
  "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionActionCard.xaml" `
  "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionActionCardTests.cs" `
  "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionRenderedStateTests.cs"
git commit -m "style(model-inspection): use shared journey actions"
```

## Task 3: Apply the palette to every Hardware Inspection action

- [ ] **Step 1: Merge the shared palette and update both Hardware styles**

In `HardwareInspectionActionCard.xaml`, merge the existing Hardware theme and the shared palette. Keep the exact style keys and native bases, but replace 44/10/local colours with shared 46/11/padding and primary/secondary normal brushes.

- [ ] **Step 2: Add a visual-only native state mapper for dynamic buttons**

In `HardwareInspectionActionCard.xaml.cs`, add:

```csharp
private void ApplyNativeStatePalette(Button button, bool primary)
{
    ArgumentNullException.ThrowIfNull(button);
    button.Resources["ButtonBackgroundPointerOver"] = Resources[
        primary
            ? "GraniteJourneyPrimaryPointerOverBrush"
            : "GraniteJourneySecondaryPointerOverBrush"];
    button.Resources["ButtonBorderBrushPointerOver"] = Resources[
        primary
            ? "GraniteJourneyPrimaryPointerOverBrush"
            : "GraniteJourneySecondaryPointerOverBorderBrush"];
    button.Resources["ButtonBackgroundPressed"] = Resources[
        primary
            ? "GraniteJourneyPrimaryPressedBrush"
            : "GraniteJourneySecondaryPressedBrush"];
    button.Resources["ButtonBorderBrushPressed"] = Resources[
        primary
            ? "GraniteJourneyPrimaryPressedBrush"
            : "GraniteJourneySecondaryPressedBorderBrush"];
    button.Resources["ButtonBackgroundDisabled"] = Resources[
        "GraniteJourneyDisabledBackgroundBrush"];
    button.Resources["ButtonForegroundDisabled"] = Resources[
        "GraniteJourneyDisabledForegroundBrush"];
    button.Resources["ButtonBorderBrushDisabled"] = Resources[
        "GraniteJourneyDisabledBorderBrush"];
}
```

Call it with `primary: false` immediately after each button is created. After the current code identifies the terminal primary button, assign the primary style and call it again with `primary: true`. This method must not read or mutate action labels, kinds, enabled state, visibility, help, order, or commands.

- [ ] **Step 3: Extend the existing Hardware action test across every presentation family**

Within `ActionCard_UsesApprovedModernPrimaryAndSecondaryStyles`, iterate the factory's invalid, seven active stages, Stopping, Completed, CompletedWithWarnings, three failure classes, and Cancelled states. For every generated button assert:

```csharp
Assert.AreEqual(46d, button.MinHeight);
Assert.AreEqual(new CornerRadius(11), button.CornerRadius);
Assert.AreEqual(FontWeights.SemiBold.Weight, button.FontWeight.Weight);
Assert.IsTrue(button.UseSystemFocusVisuals);
Assert.IsNotNull(button.Resources["ButtonBackgroundPointerOver"]);
Assert.IsNotNull(button.Resources["ButtonBackgroundPressed"]);
Assert.IsNotNull(button.Resources["ButtonBackgroundDisabled"]);
```

Also preserve the existing label/order/style assertions and compact-width test.

- [ ] **Step 4: Verify Hardware action GREEN and commit**

Run `HardwareInspectionTerminalCardTests`; expected all existing identities pass. Commit:

```powershell
git add -- `
  "IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Controls/HardwareInspectionActionCard.xaml" `
  "IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Controls/HardwareInspectionActionCard.xaml.cs" `
  "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionTerminalCardTests.cs"
git commit -m "style(hardware-inspection): use shared journey actions"
```

## Task 4: Correct Hardware headers, stage glyphs, and disclosure width

- [ ] **Step 1: Write the failing page-header assertions**

Inside `HardwareInspectionPageTests.Page_DefaultsToApprovedLightPresentationAndProvidesShellFooterSlot`, add:

```csharp
TextBlock title = Text(page, "PageTitleTextBlock");
TextBlock subtitle = Text(page, "PageSubtitleTextBlock");
Assert.AreEqual(TextAlignment.Center, title.TextAlignment);
Assert.AreEqual(TextAlignment.Center, subtitle.TextAlignment);
Assert.AreEqual(HorizontalAlignment.Stretch, title.HorizontalAlignment);
Assert.AreEqual(HorizontalAlignment.Stretch, subtitle.HorizontalAlignment);
```

- [ ] **Step 2: Write the failing details alignment and full-header assertions**

Inside `DetailsCard_RendersSevenRowsAndTwoCollapsedLevels`, assert:

```csharp
Border rowSurface = (Border)rows.ItemTemplate.LoadContent();
Border glyphSurface = (Border)((Grid)rowSurface.Child).Children[0];
Assert.AreEqual(VerticalAlignment.Center, glyphSurface.VerticalAlignment);
Assert.IsInstanceOfType<FontIcon>(card.FindName("TechnicalInformationGlyph"));
Grid technicalHeader = (Grid)card.FindName("TechnicalHeaderGrid");
Assert.AreEqual(HorizontalAlignment.Stretch, technicalHeader.HorizontalAlignment);
Assert.AreEqual(3, technicalHeader.ColumnDefinitions.Count);
Assert.AreEqual(new GridLength(28), technicalHeader.ColumnDefinitions[0].Width);
```

Add loaded geometry inside the same existing test identity: place the card in a `Window` at 840 px and 480 px, expand `DetailsExpander`, wait for layout, then assert the technical header's actual width is at least the Technical Expander width minus the native chevron reservation and that the glyph centre is within one physical pixel of its row's vertical centre.

- [ ] **Step 3: Run Page and Details classes and capture RED**

Expected failures: title/subtitle alignment are unset/left, row glyph surface is `Top`, the nested information glyph/header name is absent, and Technical Expander lacks explicit stretch ownership.

- [ ] **Step 4: Implement the minimal shared alignment corrections**

In `HardwareInspectionPage.xaml`, set both title and subtitle:

```xml
HorizontalAlignment="Stretch"
TextAlignment="Center"
```

In the details row template, change only:

```xml
VerticalAlignment="Center"
```

on the 20x20 status-glyph surface.

On `TechnicalExpander`, add `HorizontalAlignment="Stretch"`. Replace its header grid with a named three-column full-width grid:

```xml
<Grid
    x:Name="TechnicalHeaderGrid"
    MinHeight="52"
    HorizontalAlignment="Stretch"
    ColumnSpacing="12">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="28" />
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="Auto" />
    </Grid.ColumnDefinitions>
    <Border
        Width="28"
        Height="28"
        VerticalAlignment="Center"
        Background="{ThemeResource HardwareInspectionAccentSurfaceBrush}"
        CornerRadius="14">
        <FontIcon
            x:Name="TechnicalInformationGlyph"
            HorizontalAlignment="Center"
            VerticalAlignment="Center"
            FontFamily="Segoe Fluent Icons"
            FontSize="13"
            Foreground="{ThemeResource HardwareInspectionAccentBrush}"
            Glyph="&#xE946;" />
    </Border>
    <StackPanel Grid.Column="1" VerticalAlignment="Center" Spacing="2">
        <!-- retain exact existing title and helper TextBlocks -->
    </StackPanel>
    <TextBlock
        x:Name="TechnicalActionTextBlock"
        Grid.Column="2"
        Margin="12,0,4,0"
        VerticalAlignment="Center"
        FontSize="{ThemeResource HardwareInspectionHelperFontSize}"
        FontWeight="Bold"
        Foreground="{ThemeResource HardwareInspectionAccentBrush}"
        Text="Show IT details" />
</Grid>
```

Do not add a second button or click handler; the native Expander remains the only target.

- [ ] **Step 5: Verify alignment GREEN and disclosure semantics**

Run `HardwareInspectionDetailsSummaryTests` and `HardwareInspectionPageTests`. Expand/collapse both levels through automation peers and assert existing labels, state preservation, focus, and zero nested `ScrollViewer` behavior still pass.

- [ ] **Step 6: Commit alignment**

```powershell
git add -- `
  "IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/HardwareInspectionPage.xaml" `
  "IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Presentation/Controls/HardwareInspectionDetailsCard.xaml" `
  "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionDetailsSummaryTests.cs" `
  "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionPageTests.cs"
git commit -m "style(hardware-inspection): centre shared details geometry"
```

## Task 5: Exhaustive verification and native evidence

- [ ] **Step 1: Preserve public test identity inventories**

Compare test method/DataRow inventories before the design commit and current HEAD. Expected: no added, removed, or renamed public test identities; all new assertions live inside existing identities.

- [ ] **Step 2: Run the focused packaged gates**

Run the VSTest recipe with this filter:

```text
FullyQualifiedName~ImportModelCardTests|
FullyQualifiedName~InspectionActionCardTests|
FullyQualifiedName~ModelInspectionRenderedStateTests|
FullyQualifiedName~HardwareInspectionTerminalCardTests|
FullyQualifiedName~HardwareInspectionDetailsSummaryTests|
FullyQualifiedName~HardwareInspectionPageTests
```

Expected: all selected cases pass with zero failed/error/timeout/aborted/not-executed counters.

- [ ] **Step 3: Build and run the complete packaged suite**

```powershell
dotnet restore "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" -p:Platform=x64
dotnet build "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" -c Debug -p:Platform=x64 --no-restore
& "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe" `
  "tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe" `
  /Logger:"trx;LogFileName=inspection-shared-actions-full.trx" `
  /ResultsDirectory:"TestResults\InspectionAlignment\Full"
```

Expected: the same complete test-name multiset as the baseline and every test passes.

- [ ] **Step 4: Capture representative native WinUI evidence**

Using only ignored test output, instantiate the committed real Model and Hardware pages in a packaged WinUI `Window` and capture:

- Model active and one terminal with primary/secondary actions at 1440x1100 and 480x900;
- Hardware active, Completed, warning, each failure family, Stopping, and Cancelled at 1440x1100 and 480x900;
- one Hardware expanded-details screen at 480x900 showing centred row glyphs and the full-width nested disclosure;
- one keyboard-focus capture for primary and secondary actions.

Verify decoded PNG dimensions, no edge clipping, centred headings, centred compact glyphs, shared button palette, full-width disclosure layout, and visible focus. Do not use HTML captures as acceptance evidence.

- [ ] **Step 5: Perform final scope and cleanliness checks**

```powershell
git diff --check
git status --short
git diff --name-only fa218c9acb1087e258dd36414067c53e18e44829..HEAD
```

Expected production scope: the shared palette/project registration, Model action XAML, Hardware action XAML/view-only code, Hardware details/page XAML, and the six existing tests only. No Model/Hardware ViewModel, service, domain, navigation, provider, compatibility, workflow, or generated evidence file changes.

- [ ] **Step 6: Request independent review before integration**

The review must check exact Model Import palette equivalence, all 15 Hardware and 13 Model state paths, native state/focus preservation, full-width disclosure behavior, compact 200% layout, zero backend changes, exact test inventory, and complete-suite evidence.

---

## Completion non-claims

This plan changes presentation only. It does not make the Hardware collector operational, close Gate 1, enter Gate 2, enable Block 3, run a candidate, contact hardware or a laptop, change networking, dispatch a workflow, publish evidence, push, open a PR, or merge a branch.
