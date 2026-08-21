# Chat Compact Blue Consistency Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make selected navigation, messages, sidebar alignment, responsive turn spacing, and the composer consistently compact and Granite blue.

**Architecture:** Keep the existing semantic-resource and custom-control boundaries. Palette decisions stay in `GgufChatTheme.xaml`; `ChatHistoryItem` applies selected foreground/background; `ChatPage` owns transcript item layout and sidebar composition; `ChatComposer` owns its compact single-line dimensions while retaining multiline and attachment growth.

**Tech Stack:** C# 12, .NET 8, WinUI 3 XAML, MSTest 4 AppContainer UI tests, XML source-contract tests, PowerShell verification scripts.

---

## File Map

- Modify `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Presentation/GgufChatTheme.xaml`: semantic selected/message blue surfaces and navigation padding behavior.
- Modify `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatHistoryItem.xaml.cs`: selected title foreground.
- Modify `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml`: left-align rail actions and define transcript container spacing.
- Modify `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml`: compact empty-state dimensions and blue Stop action.
- Modify `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatPageTests.cs`: role colors, rail alignment, and transcript spacing.
- Modify `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Controls/ChatHistoryItemTests.cs`: selected blue/white state.
- Modify `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Controls/ChatComposerTests.cs`: compact dimensions and Stop palette.
- Modify `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs`: source-level visual contract.

### Task 1: Selected history and blue message palette

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Presentation/GgufChatTheme.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatHistoryItem.xaml.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatPageTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Controls/ChatHistoryItemTests.cs`

- [ ] **Step 1: Write failing packaged UI assertions**

Update the role test to require primary blue/white for the user and pale-blue/dark-blue for the assistant. Extend the selected-history test:

```csharp
Assert.AreEqual(
    Assert.IsInstanceOfType<SolidColorBrush>(
        Application.Current.Resources["GgufChatPrimaryBrush"]).Color,
    Assert.IsInstanceOfType<SolidColorBrush>(historyButton.Background).Color);
Assert.AreEqual(
    Assert.IsInstanceOfType<SolidColorBrush>(
        Application.Current.Resources["GgufChatPrimaryForegroundBrush"]).Color,
    Assert.IsInstanceOfType<SolidColorBrush>(historyButton.Foreground).Color);
```

- [ ] **Step 2: Run the focused packaged tests and verify RED**

Build the packaged test project, then run `ChatPageTests|ChatHistoryItemTests` through `vstest.console.exe` using the generated `.build.appxrecipe`.

Expected: failures show the current pale selected surface and non-white selected title.

- [ ] **Step 3: Implement the semantic palette and selected foreground**

Use these Light theme values:

```xml
<SolidColorBrush x:Key="GgufChatAssistantBubbleBrush" Color="#EAF2FF" />
<SolidColorBrush x:Key="GgufChatAssistantBubbleTextBrush" Color="#102E6B" />
<SolidColorBrush x:Key="GgufChatUserBubbleBrush" Color="#2563EB" />
<SolidColorBrush x:Key="GgufChatUserBubbleTextBrush" Color="#FFFFFF" />
<SolidColorBrush x:Key="GgufChatNavigationSelectedBrush" Color="#2563EB" />
```

In `ApplySelection`, set both properties from semantic resources:

```csharp
HistoryButton.Background = (Brush)Application.Current.Resources[
    IsSelected ? "GgufChatNavigationSelectedBrush" : "GgufChatNavigationRestBrush"];
HistoryButton.Foreground = (Brush)Application.Current.Resources[
    IsSelected ? "GgufChatPrimaryForegroundBrush" : "GgufChatTextBrush"];
```

Preserve High Contrast aliases; use corresponding blue-family values for the unused Dark dictionary so theme contracts stay complete.

- [ ] **Step 4: Run focused tests and verify GREEN**

Expected: `ChatPageTests` and `ChatHistoryItemTests` all pass.

- [ ] **Step 5: Commit**

```powershell
git add "IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Presentation/GgufChatTheme.xaml" `
        "IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatHistoryItem.xaml.cs" `
        "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatPageTests.cs" `
        "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Controls/ChatHistoryItemTests.cs"
git commit -m "style(chat): unify messages and selection in granite blue"
```

### Task 2: Rail alignment and responsive turn spacing

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatPageTests.cs`
- Test: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs`

- [ ] **Step 1: Write failing rail and transcript layout tests**

Require all three rail actions to be left-aligned with compact padding, and require transcript containers to reserve 12 pixels below each turn:

```csharp
foreach (string name in new[] { "NewChatButton", "ImportModelButton", "SettingsButton" })
{
    Button action = Assert.IsInstanceOfType<Button>(page.FindName(name));
    Assert.AreEqual(HorizontalAlignment.Left, action.HorizontalContentAlignment, name);
    Assert.AreEqual(new Thickness(4, 8, 4, 8), action.Padding, name);
}

ListView transcript = Assert.IsInstanceOfType<ListView>(page.FindName("TranscriptList"));
Style containerStyle = Assert.IsNotNull(transcript.ItemContainerStyle);
Setter marginSetter = containerStyle.Setters.OfType<Setter>()
    .Single(setter => setter.Property == FrameworkElement.MarginProperty);
Assert.AreEqual(new Thickness(0, 0, 0, 12), marginSetter.Value);
```

Add XML assertions for `HorizontalContentAlignment="Left"`, `Padding="4,8"`, and transcript `Margin="0,0,0,12"`.

- [ ] **Step 2: Run source and packaged tests and verify RED**

Expected: New Chat and Import Model still report centered alignment and the transcript has no item-container spacing.

- [ ] **Step 3: Implement alignment and item spacing**

On New Chat, Import Model, and Settings:

```xml
HorizontalContentAlignment="Left"
Padding="4,8"
```

Add to `TranscriptList`:

```xml
<ListView.ItemContainerStyle>
    <Style TargetType="ListViewItem">
        <Setter Property="Padding" Value="0" />
        <Setter Property="Margin" Value="0,0,0,12" />
        <Setter Property="HorizontalContentAlignment" Value="Stretch" />
    </Style>
</ListView.ItemContainerStyle>
```

- [ ] **Step 4: Run focused source and packaged tests and verify GREEN**

Expected: visual contracts and `ChatPageTests` pass at wide and narrow test widths.

- [ ] **Step 5: Commit**

```powershell
git add "IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml" `
        "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatPageTests.cs" `
        "tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs"
git commit -m "style(chat): align rail actions and separate transcript turns"
```

### Task 3: Compact composer and blue Stop action

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Controls/ChatComposerTests.cs`
- Test: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs`

- [ ] **Step 1: Write failing compact-dimension and palette tests**

Extend `ComposerUsesSingleSurfaceAndCenteredGrowingPrompt`:

```csharp
Border surface = Assert.IsInstanceOfType<Border>(composer.FindName("ComposerSurface"));
Assert.AreEqual(new Thickness(12, 6, 12, 6), surface.Padding);
Grid promptRow = Assert.IsInstanceOfType<Grid>(composer.FindName("PromptRow"));
Assert.AreEqual(44, promptRow.MinHeight);
Assert.AreEqual(40, prompt.MinHeight);
Assert.AreEqual(40, attachment.Height);
Assert.AreEqual(44, send.Height);

Button stop = Assert.IsInstanceOfType<Button>(composer.FindName("StopButton"));
Assert.AreEqual(
    Assert.IsInstanceOfType<SolidColorBrush>(
        Application.Current.Resources["GgufChatPrimaryBrush"]).Color,
    Assert.IsInstanceOfType<SolidColorBrush>(stop.Background).Color);
```

Add matching XML source assertions so the compact dimensions cannot regress without running the packaged UI suite.

- [ ] **Step 2: Run composer tests and verify RED**

Expected: failures show vertical padding `10`, row minimum `52`, prompt/attachment height `44/42`, and explicit black Stop background.

- [ ] **Step 3: Implement the compact composer**

Apply:

```xml
<Border x:Name="ComposerSurface" Padding="12,6" ...>
<Grid x:Name="PromptRow" MinHeight="44" ...>
<Button x:Name="AttachmentButton" Width="40" Height="40" ... />
<TextBox x:Name="PromptTextBox" MinHeight="40" ... />
<Button x:Name="StopButton"
        Height="44"
        Style="{StaticResource GgufChatPrimaryButtonStyle}" ... />
```

Keep Send at 44 pixels. Preserve the current `MaxHeight="160"`, multiline top-alignment logic, attachment row, focus ring, and Stop square layout.

- [ ] **Step 4: Run composer, focus, multiline, attachment, and accessibility tests**

Expected: all focused packaged tests pass, including single-line centering and multiline growth.

- [ ] **Step 5: Commit**

```powershell
git add "IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml" `
        "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Controls/ChatComposerTests.cs" `
        "tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs"
git commit -m "style(chat): compact the message composer"
```

### Task 4: Full verification and review

**Files:**
- Verify: all files changed in Tasks 1-3

- [ ] **Step 1: Build the packaged WinUI test project**

```powershell
dotnet build tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj `
  --configuration Release --runtime win-x64 -p:Platform=x64 --no-restore
```

Expected: build succeeds with 0 warnings and 0 errors.

- [ ] **Step 2: Run all packaged GGUF UI tests**

Run `vstest.console.exe` against `GraniteEdgeAI.UnitTests.build.appxrecipe` with:

```text
/TestCaseFilter:"FullyQualifiedName~GgufRuntime"
```

Expected: every discovered packaged GGUF UI test passes.

- [ ] **Step 3: Run full GGUF verification**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\gguf-runtime\Invoke-GgufChatVerification.ps1
```

Expected: all deterministic suites and Release build pass; the controlled real-model integration test may skip only when no controlled model is configured.

- [ ] **Step 4: Inspect repository state**

```powershell
git diff --check
git status --short
```

Expected: no whitespace errors and no uncommitted changes.

- [ ] **Step 5: Request independent code review**

Review the implementation against `docs/superpowers/specs/2026-08-21-chat-compact-blue-consistency-design.md`. Fix every Critical or Important finding test-first and rerun Steps 1-4.
