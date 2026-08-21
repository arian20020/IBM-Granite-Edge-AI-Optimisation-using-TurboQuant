# Chat Modern Visual Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver the approved Calm Fluent WinUI chat polish: a single-surface composer, centered prompt text, pale readable messages, ghost navigation rows, a polished Add files flyout, and the complete Granite Edge AI lockup.

**Architecture:** Keep interaction ownership in the existing controls. `GgufChatTheme.xaml` supplies shared brushes and templates; `ChatComposer` owns prompt and flyout behavior; `ChatHistoryItem` and `ChatMessageBubble` own their local visuals; `ChatPage` composes them. Preserve all existing attachment, history, command, and GGUF behavior.

**Tech Stack:** C# 12, .NET 8, WinUI 3 / Windows App SDK 2.2, XAML resource dictionaries, MSTest 4 packaged AppContainer tests, XML contract tests, PowerShell.

---

## File Map

- Modify `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Presentation/GgufChatTheme.xaml`: Calm Fluent brushes and ghost-row, composer-editor, and flyout-action styles.
- Modify `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml`: one visible composer surface and the Add files flyout.
- Modify `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml.cs`: focus state and flyout enablement while preserving multiline behavior.
- Modify `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatHistoryItem.xaml` and `.xaml.cs`: ghost history row plus explicit selected state.
- Modify `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatMessageBubble.xaml` and `.xaml.cs`: readable role-specific surfaces and text.
- Modify `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml` and `.xaml.cs`: sidebar composition, date headings, and branding.
- Modify `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatDemoController.cs`: pass the coordinator's selected conversation into history rendering.
- Modify `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Attachments/WindowsKnowledgeFilePicker.cs`: user-facing picker title only.
- Create `IBM Granite with TurboQuant (Intel)/Assets/Branding/granite-edge-ai-lockup-outlined.svg`: WinUI-safe path-only derivative.
- Modify both app and packaged-test `.csproj` files: package the branding assets.
- Modify `ChatComposerTests.cs`, `ChatPageTests.cs`, `ChatAccessibilityTests.cs`, and `GgufChatVisualContractTests.cs`: TDD coverage.

## Commands Used Throughout

Source/XML visual contracts:

```powershell
dotnet test tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests.csproj --configuration Release --filter "FullyQualifiedName~GgufChatVisualContractTests"
```

Packaged WinUI test setup:

```powershell
$testProject = 'tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj'
dotnet build $testProject --configuration Release --runtime win-x64 -p:Platform=x64
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vstest = & $vswhere -latest -products * -find '**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' | Select-Object -First 1
$recipe = (Resolve-Path 'tests/UnitTests/GraniteEdgeAI.UnitTests/bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/GraniteEdgeAI.UnitTests.build.appxrecipe').Path
New-Item -ItemType Directory -Force 'TestResults/ChatModernPolish' | Out-Null
& $vstest $recipe /Platform:x64 /ResultsDirectory:TestResults/ChatModernPolish /Logger:"trx;LogFileName=chat-modern-polish.trx" /TestCaseFilter:"FullyQualifiedName~GraniteEdgeAI.UnitTests.Features.GgufRuntime"
```

Every GREEN packaged run must have zero failed, skipped, or not-executed tests. Use unique TRX names for RED and GREEN evidence.

### Task 1: Lock the Calm Fluent theme contract

**Files:**
- Modify: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Presentation/GgufChatTheme.xaml`

- [ ] **Step 1: Write the failing theme test**

Add `ChatThemeDefinesCalmFluentNavigationComposerAndBubbleContracts`. Load the Light dictionary and assert:

```csharp
var expected = new Dictionary<string, string>
{
    ["GgufChatAssistantBubbleBrush"] = "#F4F7FB",
    ["GgufChatAssistantBubbleTextBrush"] = "#172033",
    ["GgufChatUserBubbleBrush"] = "#EAF2FF",
    ["GgufChatUserBubbleTextBrush"] = "#16345F",
    ["GgufChatNavigationRestBrush"] = "#00FFFFFF",
    ["GgufChatNavigationHoverBrush"] = "#EEF4FF",
    ["GgufChatNavigationPressedBrush"] = "#E2ECFA",
    ["GgufChatNavigationSelectedBrush"] = "#E8F0FF",
    ["GgufChatComposerBrush"] = "#FFFFFFFF",
    ["GgufChatComposerFocusedBorderBrush"] = "#2563EB",
    ["GgufChatFlyoutBrush"] = "#FFFFFFFF",
    ["GgufChatDateHeadingBrush"] = "#526174",
};
```

For each entry, use `AssertThemeResource`. Require root styles named `GgufChatNavigationButtonStyle`, `GgufChatComposerTextBoxStyle`, and `GgufChatFlyoutActionStyle`. Require Normal, PointerOver, Pressed, Disabled, Focused, and Unfocused visual states for buttons, a separate `FocusVisual`, transparent editor state resources, and contrast of at least 4.5:1 for both bubble pairs.

- [ ] **Step 2: Run the source/XML command and verify RED**

Expected: FAIL because the new semantic resources and styles are absent.

- [ ] **Step 3: Add the minimal theme resources and styles**

Add the exact Light colors above. Define matching Dark keys without changing `ChatPage.RequestedTheme="Light"`. Alias High Contrast surfaces, text, and focus to appropriate system resources.

Create `GgufChatNavigationButtonStyle`: transparent normal surface, zero resting border, left alignment, 12px corner radius, 12px horizontal padding, 44px minimum height, explicit hover/pressed/disabled states, and a two-pixel focus visual.

Create `GgufChatComposerTextBoxStyle`: `BorderThickness=0`, `Padding=12,0`, `VerticalContentAlignment=Center`, and transparent stock TextBox background/border resources for normal, hover, focused, and disabled states while retaining caret and selection visibility.

Create `GgufChatFlyoutActionStyle`: 48px minimum-height left-aligned ghost button using the navigation hover/pressed/focus resources.

- [ ] **Step 4: Re-run the source/XML command and verify GREEN**

Expected: every `GgufChatVisualContractTests` case PASS.

- [ ] **Step 5: Commit**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Presentation/GgufChatTheme.xaml' 'tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs'
git commit -m "style(chat): define calm fluent theme resources"
```

### Task 2: Make the composer one smooth surface

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Controls/ChatComposerTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml.cs`

- [ ] **Step 1: Write failing surface, alignment, and focus tests**

Update the existing composer test and add rendered cases that assert:

```csharp
Border surface = Assert.IsInstanceOfType<Border>(composer.FindName("ComposerSurface"));
TextBox prompt = Assert.IsInstanceOfType<TextBox>(composer.FindName("PromptTextBox"));
Assert.AreEqual(0, prompt.BorderThickness.Left);
Assert.AreEqual(new Thickness(12, 0, 12, 0), prompt.Padding);
Assert.AreEqual(VerticalAlignment.Center, prompt.VerticalContentAlignment);
Assert.AreEqual(Visibility.Collapsed,
    Assert.IsInstanceOfType<Border>(composer.FindName("ComposerFocusVisual")).Visibility);
```

Host the control at 700×180, set a one-line prompt, transform prompt bounds into `ComposerSurface`, and require the top/bottom free-space difference to be at most 2px. Focus the prompt and require the outer focus visual to become visible; focus the attachment button and require it to collapse. Retain the existing multiline grow/top-align/shrink/center test.

- [ ] **Step 2: Run packaged `ChatComposerTests` and verify RED**

Expected: FAIL because the named outer focus visual and transparent editor contract are absent.

- [ ] **Step 3: Implement the composer surface**

Name the existing outer border `ComposerSurface`, bind it to `GgufChatComposerBrush`, and keep it as the only visible border. Add:

```xml
<Border
    x:Name="ComposerFocusVisual"
    Margin="-2"
    BorderBrush="{ThemeResource GgufChatComposerFocusedBorderBrush}"
    BorderThickness="2"
    CornerRadius="24"
    IsHitTestVisible="False"
    Visibility="Collapsed" />
```

Apply `GgufChatComposerTextBoxStyle` to `PromptTextBox`; keep `MinHeight=44`, `MaxHeight=160`, `AcceptsReturn=True`, wrapping, placeholder, and existing change/size handlers. Add `GotFocus` and `LostFocus` handlers:

```csharp
private void PromptTextBox_GotFocus(object sender, RoutedEventArgs eventArguments) =>
    ComposerFocusVisual.Visibility = Visibility.Visible;

private void PromptTextBox_LostFocus(object sender, RoutedEventArgs eventArguments) =>
    ComposerFocusVisual.Visibility = Visibility.Collapsed;
```

Do not alter `UpdatePromptVerticalAlignment`: newline or measured growth means Top; otherwise Center.

- [ ] **Step 4: Re-run packaged `ChatComposerTests` and verify GREEN**

Expected: all existing and new composer tests PASS.

- [ ] **Step 5: Commit**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml' 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Controls/ChatComposerTests.cs'
git commit -m "style(chat): modernize the composer surface"
```

### Task 3: Replace the attachment menu with Add files

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Controls/ChatComposerTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatAccessibilityTests.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml` and `.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Attachments/WindowsKnowledgeFilePicker.cs`

- [ ] **Step 1: Write failing flyout and naming tests**

Replace old MenuFlyout expectations with:

```csharp
Flyout flyout = Assert.IsInstanceOfType<Flyout>(attachment.Flyout);
Button add = Assert.IsInstanceOfType<Button>(composer.FindName("AddFilesFlyoutButton"));
TextBlock hint = Assert.IsInstanceOfType<TextBlock>(composer.FindName("AddFilesHintText"));
Assert.AreEqual("Add files", AutomationProperties.GetName(attachment));
Assert.AreEqual("Add files", AutomationProperties.GetName(add));
Assert.AreEqual("Text or Markdown · Not indexed", hint.Text);
```

Add a source contract that no production file beneath `Features/GgufRuntime` contains `Add knowledge files` and the picker title is exactly `Add files`.

- [ ] **Step 2: Run source contracts and packaged composer/accessibility tests; verify RED**

Expected: FAIL on the old flyout type and labels.

- [ ] **Step 3: Implement the custom Flyout**

Use `Flyout Placement="TopEdgeAlignedLeft"` containing a 260px-minimum, 14px-corner light `Border`. Its single `AddFilesFlyoutButton` uses `GgufChatFlyoutActionStyle` and contains a file icon, semibold `Add files`, and `Text or Markdown · Not indexed` hint. Set the plus button's automation name to `Add files`.

In `ApplyGeneratingState`, enable/disable `AddFilesFlyoutButton` instead of the removed menu item. Keep the same picker method and hide the flyout before awaiting:

```csharp
private async void AddKnowledgeFiles_Click(object sender, RoutedEventArgs eventArguments)
{
    AttachmentButton.Flyout?.Hide();
    await AddKnowledgeFilesAsync();
}
```

Change only the native picker title to `Add files`; retain internal `Knowledge*` domain names and metadata-only behavior.

- [ ] **Step 4: Re-run both suites and verify GREEN**

Expected: flyout, accessibility, privacy, picker ordering, cancellation, and failure tests PASS.

- [ ] **Step 5: Commit**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml' 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml.cs' 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Attachments/WindowsKnowledgeFilePicker.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Controls/ChatComposerTests.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatAccessibilityTests.cs' 'tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs'
git commit -m "style(chat): add polished file attachment flyout"
```

### Task 4: Lighten bubbles and modernize sidebar rows

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatPageTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatAccessibilityTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Controls/ChatHistoryItemTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatMessageBubble.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatMessageBubble.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatHistoryItem.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatHistoryItem.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatDemoController.cs`

- [ ] **Step 1: Write failing role and navigation tests**

Construct assistant and user `ChatMessageBubble` instances. Assert `BubbleBorder.Background` uses the matching assistant/user brush and `MessageText.Foreground` uses the matching text brush. Add `SidebarActionsAndHistoryUseGhostNavigationRows`: require all three page actions and `HistoryButton` to use `GgufChatNavigationButtonStyle`, left alignment, no resting border, and existing keyboard focus. In the new `ChatHistoryItemTests`, set `IsSelected=true` and assert the pale selected surface and three-pixel indicator are visible; set it false and assert both return to the resting state. Add a source contract to `GgufChatVisualContractTests` requiring `ChatDemoController.Render` to compare each history ID with `coordinator.SelectedConversation?.Id` and pass that Boolean to `AddHistoryConversation`.

- [ ] **Step 2: Run packaged page/accessibility tests and verify RED**

Expected: FAIL because page actions use primary/secondary styles and assistant messages reuse the plain surface.

- [ ] **Step 3: Apply readable role resources**

In `ApplyRole`, select both keys:

```csharp
string surfaceKey = IsUser ? "GgufChatUserBubbleBrush" : "GgufChatAssistantBubbleBrush";
string textKey = IsUser ? "GgufChatUserBubbleTextBrush" : "GgufChatAssistantBubbleTextBrush";
BubbleBorder.Background = (Brush)Application.Current.Resources[surfaceKey];
MessageText.Foreground = (Brush)Application.Current.Resources[textKey];
```

Retain current 16px corners, padding, maximum width, and left/right alignment. Add no heavy outline.

- [ ] **Step 4: Apply the navigation-row language**

Use `GgufChatNavigationButtonStyle` for New Chat, Import Model, Settings, and `HistoryButton`. Build page action content from a leading `FontIcon` plus label; New Chat text is semibold. Remove filled gradient and persistent borders. Keep unique automation names and event handlers.

Add an `IsSelected` dependency property to `ChatHistoryItem`. Its property callback calls `VisualStateManager.GoToState(this, IsSelected ? "Selected" : "Unselected", false)`. The Selected state sets `HistoryButton.Background` to `GgufChatNavigationSelectedBrush` and shows a left-aligned, three-pixel blue `SelectionIndicator`; Unselected restores the transparent surface and collapses the indicator.

Change `ChatPage.AddHistoryConversation` to `AddHistoryConversation(Guid id, string title, bool isSelected)` and assign the property on the created item. In `ChatDemoController.Render`, cache `Guid? selectedId = coordinator.SelectedConversation?.Id` before building groups and pass `conversation.Id == selectedId`. This changes visual selection only; the existing click event and coordinator selection remain authoritative.

Keep `CHATS` and date headings at zero rail-content inset. Retain `Margin = new Thickness(0, 14, 0, 6)` for date groups but bind their foreground to a dedicated readable heading brush, not a disabled-looking neutral.

- [ ] **Step 5: Re-run packaged tests and verify GREEN**

Expected: pale readable messages, quiet resting rows, hover/pressed/focus resources, and all history/action behavior PASS.

- [ ] **Step 6: Commit**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatMessageBubble.xaml' 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatMessageBubble.xaml.cs' 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatHistoryItem.xaml' 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatHistoryItem.xaml.cs' 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml' 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml.cs' 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatDemoController.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatPageTests.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatAccessibilityTests.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Controls/ChatHistoryItemTests.cs' 'tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs'
git commit -m "style(chat): lighten messages and navigation rows"
```

### Task 5: Package the complete WinUI-safe lockup

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Assets/Branding/granite-edge-ai-lockup-outlined.svg`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs`

- [ ] **Step 1: Write failing asset contracts**

Require `BrandLockup.Source` to equal `ms-appx:///Assets/Branding/granite-edge-ai-lockup-outlined.svg`. Load the new asset and assert:

```csharp
Assert.IsFalse(outlined.Descendants().Any(e => e.Name.LocalName is "text" or "tspan"));
Assert.IsTrue(outlined.Descendants().Count(e => e.Name.LocalName == "path") > 9);
```

Require app and test projects to package the outlined asset. Keep `EmptyStateBrandMark.Source` equal to `ms-appx:///Assets/Branding/granite-edge-ai-icon.svg`.

- [ ] **Step 2: Run source contracts and verify RED**

Expected: FAIL because the outlined asset and package entries are absent.

- [ ] **Step 3: Create and validate the path-only derivative**

Start from `docs/Logo/granite-edge-ai-lockup.svg`. Preserve its 1400×420 viewBox, monogram paths, spacing, and brand colors. Convert only the `Granite Edge AI` text/tspan glyphs to closed SVG paths using Segoe UI Semibold, the installed fallback declared by the source. Commit no `<text>`, `<tspan>`, external font, bitmap, script, or external reference.

Validate:

```powershell
[xml]$svg = Get-Content -Raw 'IBM Granite with TurboQuant (Intel)/Assets/Branding/granite-edge-ai-lockup-outlined.svg'
if ($svg.SelectNodes('//*[local-name()="text" or local-name()="tspan"]').Count -ne 0) { throw 'Outlined lockup still contains SVG text.' }
if ($svg.SelectNodes('//*[local-name()="path"]').Count -le 9) { throw 'Outlined wordmark paths are missing.' }
```

- [ ] **Step 4: Package and reference the asset**

Point `BrandLockup` to the outlined asset. Add it as app content with output/publish `PreserveNewest`; link it and the icon into the packaged UI test project. Remove the old linked runtime lockup entry, but leave `docs/Logo/granite-edge-ai-lockup.svg` as source-of-truth documentation.

- [ ] **Step 5: Run source and packaged branding tests and verify GREEN**

Expected: asset structure, package references, complete sidebar lockup, monogram-only empty state, and decorative-image accessibility PASS.

- [ ] **Step 6: Preview the logo**

Close any running preview process to avoid the known apphost lock, then run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Run-ChatPreview.ps1
```

Expected: top-left shows monogram plus complete `Granite Edge AI` wordmark; center shows only the monogram.

- [ ] **Step 7: Commit**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Assets/Branding/granite-edge-ai-lockup-outlined.svg' 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml' 'IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj' 'tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj' 'tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs'
git commit -m "fix(chat): render the complete granite lockup"
```

### Task 6: Final accessibility and regression gate

**Files:**
- Modify only files from Tasks 1–5 if a test exposes a defect.

- [ ] **Step 1: Run all focused packaged chat tests**

Use the packaged command with `FullyQualifiedName~GraniteEdgeAI.UnitTests.Features.GgufRuntime`.

Expected: all selected tests PASS with zero failed, skipped, or not-executed.

- [ ] **Step 2: Run full GGUF verification**

Close the preview, then run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\gguf-runtime\Invoke-GgufChatVerification.ps1
```

Expected: all GGUF tests pass; only the documented real-model case may skip when no controlled model is configured; Release preview build succeeds.

- [ ] **Step 3: Perform manual visual acceptance**

Run the preview and verify: only the outer composer is visible; one-line text is centered; multiline text grows/top-aligns/shrinks; focus appears on the outer capsule; Add files opens above with its exact hint; both bubble roles are pale and readable; every sidebar/history row is quiet at rest with hover/press/focus feedback; date headings align with `CHATS`; the full lockup is visible top-left; the monogram remains centered.

- [ ] **Step 4: Check repository hygiene**

```powershell
git diff --check
git status --short
git log --oneline -6
```

Expected: diff check emits no output and status contains no unintended artifact.

- [ ] **Step 5: Request final review and verify completion**

Use `requesting-code-review` against the merge base and branch HEAD. Resolve blockers through a new RED/GREEN cycle. Rerun Steps 1–4, then use `verification-before-completion` before reporting success. Do not create an empty commit.
