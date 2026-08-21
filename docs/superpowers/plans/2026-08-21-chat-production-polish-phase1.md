# Chat Production Polish Phase 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver the approved restrained desktop shell, accessible Granite-blue chat styling, compact state-aware composer, honest preview identity, and branded Windows application assets.

**Architecture:** Keep presentation responsibilities inside the existing WinUI `ChatPage`, `ChatComposer`, `ChatMessageBubble`, theme resources, and manifest. Derive UI state from existing properties rather than changing persistence or runtime protocols, and generate Windows assets reproducibly from the approved compact SVG.

**Tech Stack:** C# 12, .NET 8, WinUI 3, Windows App SDK 2.2, XAML theme resources, MSTest 4.3.2, Visual Studio packaged `vstest.console.exe`, PowerShell 5.1, WPF vector/raster APIs.

---

## File map

- Modify `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Presentation/GgufChatTheme.xaml`: semantic shell, navigation, date, bubble, and disabled-action colors.
- Modify `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml`: restrained panel, 256px rail, aligned commands, transcript spacing, and header layout.
- Modify `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatDemoController.cs`: truthful preview header.
- Modify `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Services/DemoGgufChatSession.cs`: user-facing preview response.
- Modify `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatHistoryItem.xaml` and `.xaml.cs`: single selected-row treatment without an accent bar.
- Modify `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml` and `.xaml.cs`: compact geometry, attachment/send glyphs, tooltip, and Send-state computation.
- Modify `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatMessageBubble.xaml` and `.xaml.cs`: presentation-only assistant identity.
- Modify `IBM Granite with TurboQuant (Intel)/MainWindow.xaml.cs`: native window title and icon.
- Modify `IBM Granite with TurboQuant (Intel)/Package.appxmanifest`: Granite Edge AI package identity text.
- Create `scripts/branding/Generate-WindowsAppBranding.ps1`: deterministic compact-symbol raster and ICO generation.
- Create `IBM Granite with TurboQuant (Intel)/Assets/Branding/windows-icon-manifest.json`: source and generated-output hashes.
- Modify Windows PNG assets under `IBM Granite with TurboQuant (Intel)/Assets/` and create `Assets/Branding/granite-edge-ai.ico`.
- Modify `IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj`: package the ICO and branding manifest.
- Modify `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs`: source, palette, manifest, and asset contracts.
- Modify packaged tests under `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/`: rendered shell, history, composer, message, and accessibility behavior.

## Test command conventions

Run source contracts with:

```powershell
dotnet run --project 'tests\IntegrationTests\GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests\GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests.csproj' -c Release -- --filter 'FullyQualifiedName~GgufChatVisualContractTests' --progress off
```

For packaged WinUI tests, build once per code change and run the named filter:

```powershell
$configuration = 'Release'
$testProject = 'tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj'
dotnet build $testProject -c $configuration -p:Platform=x64 -p:RuntimeIdentifier=win-x64 --nologo
if ($LASTEXITCODE -ne 0) { throw 'Packaged test build failed.' }
$recipe = (Resolve-Path "tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\$configuration\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe").Path
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vstest = & $vswhere -latest -products * -find '**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' | Select-Object -First 1
& $vstest $recipe '/Platform:x64' '/Logger:Console;Verbosity=minimal' "/TestCaseFilter:$filter"
if ($LASTEXITCODE -ne 0) { throw 'Packaged test run failed.' }
```

### Task 1: Finish the compact composer baseline already in the worktree

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Controls/ChatComposerTests.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs`

- [ ] **Step 1: Confirm the existing compact assertions**

The worktree must assert and implement these exact values:

```csharp
Assert.AreEqual(new Thickness(12, 6, 12, 6), surface.Padding);
Assert.AreEqual(44, promptRow.MinHeight);
Assert.AreEqual(40, attachment.Height);
Assert.AreEqual(40, prompt.MinHeight);
Assert.AreEqual(44, send.Height);
Assert.AreEqual(44, stop.Height);
Assert.AreSame(Application.Current.Resources["GgufChatPrimaryGradientBrush"], stop.Background);
```

- [ ] **Step 2: Run source and packaged composer tests**

Run the source command above. Then set:

```powershell
$filter = 'FullyQualifiedName~GraniteEdgeAI.UnitTests.Features.GgufRuntime.Controls.ChatComposerTests|FullyQualifiedName~GraniteEdgeAI.UnitTests.Features.GgufRuntime.ChatAccessibilityTests'
```

and run the packaged command. Expected: source contracts and all composer/accessibility cases PASS.

- [ ] **Step 3: Commit the verified baseline**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Controls/ChatComposerTests.cs' 'tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs'
git commit -m 'style(chat): compact the message composer'
```

### Task 2: Apply the restrained shell and unified navigation

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Presentation/GgufChatTheme.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatHistoryItem.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatHistoryItem.xaml.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatPageTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Controls/ChatHistoryItemTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatAccessibilityTests.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs`

- [ ] **Step 1: Write failing shell, selection, and contrast tests**

Require the wide history column to be 256, the conversation panel to use radius 14, padding 24, one-pixel border, and no `Translation="0,0,12"`. Require New Chat, Import Model, Settings, and history rows to use the same 20-pixel icon column, left content alignment, and four-pixel horizontal content padding.

Update the selected-history test to require one mechanism only:

```csharp
item.IsSelected = true;
Assert.AreSame(Application.Current.Resources["GgufChatNavigationSelectedBrush"], historyButton.Background);
Assert.AreSame(Application.Current.Resources["GgufChatPrimaryForegroundBrush"], historyButton.Foreground);
Assert.IsNull(item.FindName("SelectionIndicator"));
```

Add a contrast assertion for the light date-heading color against `GgufChatRailBrush` using the existing `ContrastRatio` helper and require at least `4.5`.

- [ ] **Step 2: Run the source and packaged page/history/accessibility tests; verify RED**

Use the source command. Then use:

```powershell
$filter = 'FullyQualifiedName~GraniteEdgeAI.UnitTests.Features.GgufRuntime.ChatPageTests|FullyQualifiedName~GraniteEdgeAI.UnitTests.Features.GgufRuntime.Controls.ChatHistoryItemTests|FullyQualifiedName~GraniteEdgeAI.UnitTests.Features.GgufRuntime.ChatAccessibilityTests'
```

Expected: FAIL on the 300px rail, 24px panel radius, retained selection indicator, and/or shell elevation.

- [ ] **Step 3: Implement option B exactly**

Change both the default and wide-state history widths to `256`. Set the content-grid padding to `16`, and the conversation panel to:

```xml
<Border
    x:Name="ConversationPanel"
    Background="{ThemeResource GgufChatSurfaceBrush}"
    BorderBrush="{ThemeResource GgufChatPanelBorderBrush}"
    BorderThickness="1"
    CornerRadius="14"
    Padding="24"
    Shadow="{StaticResource GgufChatSubtlePanelShadow}"
    Translation="0,0,2">
```

Add one root-level `<ThemeShadow x:Key="GgufChatSubtlePanelShadow" />`; the two-pixel Z translation makes it materially quieter than the current 12-pixel elevation. Remove the `SelectionIndicator` element and its code-behind visibility mutation. Keep selected background `#2563EB`, white foreground, and light date heading `#526174`; retain semantic dark/high-contrast counterparts.

Keep navigation buttons transparent at rest. Do not make New Chat permanently filled. Preserve existing pointer, pressed, focus, and disabled visual states.

- [ ] **Step 4: Re-run the same tests; verify GREEN**

Expected: all source shell contracts and packaged page/history/accessibility cases PASS.

- [ ] **Step 5: Commit**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Presentation/GgufChatTheme.xaml' 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml' 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatHistoryItem.xaml' 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatHistoryItem.xaml.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatPageTests.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Controls/ChatHistoryItemTests.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatAccessibilityTests.cs' 'tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs'
git commit -m 'style(chat): apply restrained desktop shell'
```

### Task 3: Make composer commands self-explanatory and state-aware

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Controls/ChatComposerTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatAccessibilityTests.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs`

- [ ] **Step 1: Write failing command-semantic and Send-state tests**

Require:

```csharp
Assert.AreEqual("Attach files", AutomationProperties.GetName(attachmentButton));
Assert.AreEqual("Attach files", ToolTipService.GetToolTip(attachmentButton));
Assert.AreEqual("\uE723", Assert.IsInstanceOfType<FontIcon>(attachmentButton.Content).Glyph);
Assert.AreEqual("\uE724", Assert.IsInstanceOfType<FontIcon>(sendButton.Content).Glyph);
Assert.IsFalse(sendButton.IsEnabled);

composer.PromptText = "   ";
Assert.IsFalse(sendButton.IsEnabled);
composer.PromptText = "Explain this model";
Assert.IsTrue(sendButton.IsEnabled);
composer.IsGenerating = true;
Assert.IsFalse(sendButton.IsEnabled);
composer.IsGenerating = false;
Assert.IsTrue(sendButton.IsEnabled);
```

Retain tests for `Add files`, file-type hint, Stop swapping, multiline growth, and picker failure.

- [ ] **Step 2: Run source and packaged composer/accessibility tests; verify RED**

Use the Task 1 source and packaged filters. Expected: FAIL because the attachment still uses plus glyph `E710`, Send uses `E72A`, there is no tooltip, and empty Send is enabled.

- [ ] **Step 3: Implement command semantics and one state function**

In XAML use:

```xml
AutomationProperties.Name="Attach files"
ToolTipService.ToolTip="Attach files"
...
<FontIcon Glyph="&#xE723;" FontSize="16" />
```

and change Send content to `<FontIcon Glyph="&#xE724;" />`.

In code-behind, make `PromptTextBox_TextChanged` call both alignment and submission-state refresh. Add:

```csharp
private void UpdateSubmissionState()
{
    if (SendButton is null || PromptTextBox is null)
    {
        return;
    }

    SendButton.IsEnabled = !IsGenerating &&
        PromptTextBox.Text.Trim().Length > 0;
}
```

Call it from the constructor after `ApplyGeneratingState`, at the end of `ApplyGeneratingState`, and after clearing the prompt in `SendButton_Click`. Keep the existing guard in `SendButton_Click` as defense in depth.

- [ ] **Step 4: Re-run the same tests; verify GREEN**

Expected: all composer, accessibility, attachment, Stop, and source-contract cases PASS.

- [ ] **Step 5: Commit**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml' 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Controls/ChatComposerTests.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatAccessibilityTests.cs' 'tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs'
git commit -m 'feat(chat): clarify composer commands and send state'
```

### Task 4: Add assistant identity and truthful preview language

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatMessageBubble.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatMessageBubble.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatDemoController.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Services/DemoGgufChatSession.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatPageTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatAccessibilityTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Services/GgufChatCoordinatorTests.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs`

- [ ] **Step 1: Write failing identity and truthfulness tests**

Construct both message roles. Require an `AssistantIdentityText` with text `Granite Edge AI`, visible for assistant and collapsed for user. Require the controller source to contain:

```csharp
page.SetModelHeader("Preview mode", "No model loaded");
```

Require demo output to contain `Preview mode is active`, the prompt, and `Import a compatible GGUF model to run local generation`; reject the phrases `deterministic demo runtime`, `production path uses`, and `protected GGUF CLI supervisor` from user-visible files.

- [ ] **Step 2: Run source and packaged message/page tests; verify RED**

Use the source command. Then use:

```powershell
$filter = 'FullyQualifiedName~GraniteEdgeAI.UnitTests.Features.GgufRuntime.ChatPageTests|FullyQualifiedName~GraniteEdgeAI.UnitTests.Features.GgufRuntime.ChatAccessibilityTests|FullyQualifiedName~GraniteEdgeAI.UnitTests.Features.GgufRuntime.Services.GgufChatCoordinatorTests'
```

Expected: FAIL because the identity label is absent and old developer-facing preview copy remains.

- [ ] **Step 3: Implement presentation-only identity and exact preview copy**

Add above `MessageText`:

```xml
<TextBlock
    x:Name="AssistantIdentityText"
    Text="Granite Edge AI"
    FontSize="12"
    FontWeight="SemiBold"
    Foreground="{ThemeResource GgufChatAssistantBubbleTextBrush}" />
```

In `ApplyRole`, set its visibility to `Collapsed` for user and `Visible` for assistant. Do not add a dependency property or persisted field.

Set the controller header exactly as tested. Replace the demo chunks with:

```csharp
string[] chunks =
[
    "Preview mode is active. I received “",
    prompt,
    "”. ",
    "Import a compatible GGUF model to run local generation. ",
    "This preview currently demonstrates streaming, Stop, attachments, and dated history.",
];
```

- [ ] **Step 4: Re-run the same tests; verify GREEN**

Expected: assistant identity, user-role suppression, honest header, safe response, persistence, and streaming tests PASS.

- [ ] **Step 5: Commit**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatMessageBubble.xaml' 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatMessageBubble.xaml.cs' 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatDemoController.cs' 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Services/DemoGgufChatSession.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatPageTests.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatAccessibilityTests.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Services/GgufChatCoordinatorTests.cs' 'tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs'
git commit -m 'style(chat): identify truthful preview responses'
```

### Task 5: Brand the native window and package assets

**Files:**
- Create: `scripts/branding/Generate-WindowsAppBranding.ps1`
- Create: `IBM Granite with TurboQuant (Intel)/Assets/Branding/windows-icon-manifest.json`
- Create: `IBM Granite with TurboQuant (Intel)/Assets/Branding/granite-edge-ai.ico`
- Modify: `IBM Granite with TurboQuant (Intel)/Assets/LockScreenLogo.scale-200.png`
- Modify: `IBM Granite with TurboQuant (Intel)/Assets/SplashScreen.scale-200.png`
- Modify: `IBM Granite with TurboQuant (Intel)/Assets/Square150x150Logo.scale-200.png`
- Modify: `IBM Granite with TurboQuant (Intel)/Assets/Square44x44Logo.scale-200.png`
- Modify: `IBM Granite with TurboQuant (Intel)/Assets/Square44x44Logo.targetsize-24_altform-unplated.png`
- Modify: `IBM Granite with TurboQuant (Intel)/Assets/StoreLogo.png`
- Modify: `IBM Granite with TurboQuant (Intel)/Assets/Wide310x150Logo.scale-200.png`
- Modify: `IBM Granite with TurboQuant (Intel)/MainWindow.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Package.appxmanifest`
- Modify: `IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs`

- [ ] **Step 1: Write failing app-identity and asset-fidelity contracts**

Require both manifest display names and description to equal `Granite Edge AI`. Require `MainWindow` to set `Title = "Granite Edge AI"` and call `AppWindow.SetIcon` with the packaged ICO path. Parse `windows-icon-manifest.json`, recompute the SHA-256 of `docs/Logo/granite-edge-ai-icon.svg` and every listed output, and assert exact dimensions for the seven PNGs. Open the ICO and require PNG frames at 16, 24, 32, 48, 64, 128, and 256 pixels.

- [ ] **Step 2: Run source contracts; verify RED**

Use the source command. Expected: FAIL on old manifest identity, absent ICO/manifest/generator, and missing native title/icon assignment.

- [ ] **Step 3: Implement deterministic SVG-to-Windows-asset generation**

Create a PowerShell 5.1 script that:

1. parses only the approved nine SVG paths and two gradient stops;
2. validates the `0 0 512 512` view box and source structure;
3. converts each path through `[Windows.Media.Geometry]::Parse`;
4. draws into a square WPF `DrawingVisual` using the source linear gradient and transparent background;
5. scales uniformly with 12.5% clear padding and encodes PNGs through `PngBitmapEncoder`;
6. builds an ICO whose directory entries point to PNG frames for 16, 24, 32, 48, 64, 128, and 256;
7. writes `windows-icon-manifest.json` containing lowercase source/output SHA-256 values, byte counts, and PNG dimensions.

Use these fixed asset dimensions:

```powershell
$pngTargets = [ordered]@{
    'Assets\LockScreenLogo.scale-200.png' = @(48, 48)
    'Assets\SplashScreen.scale-200.png' = @(1240, 600)
    'Assets\Square150x150Logo.scale-200.png' = @(300, 300)
    'Assets\Square44x44Logo.scale-200.png' = @(88, 88)
    'Assets\Square44x44Logo.targetsize-24_altform-unplated.png' = @(24, 24)
    'Assets\StoreLogo.png' = @(50, 50)
    'Assets\Wide310x150Logo.scale-200.png' = @(620, 300)
}
```

For non-square splash/wide outputs, center the same padded square monogram without stretching it. Write files through a temporary sibling then use `[IO.File]::Replace` for existing outputs and `[IO.File]::Move` for new outputs. Never invoke a network converter.

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File '.\scripts\branding\Generate-WindowsAppBranding.ps1'
```

In `MainWindow` after `InitializeComponent()`:

```csharp
Title = "Granite Edge AI";
string iconPath = Path.Combine(
    AppContext.BaseDirectory,
    "Assets",
    "Branding",
    "granite-edge-ai.ico");
AppWindow.SetIcon(iconPath);
```

Add `using System.IO;`. Package the ICO and JSON with `CopyToOutputDirectory` and `CopyToPublishDirectory` set to `PreserveNewest`. Change manifest property and visual-element identity text to `Granite Edge AI`.

- [ ] **Step 4: Regenerate twice and verify deterministic GREEN**

Run the generator twice, save hashes after each run, and compare them:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File '.\scripts\branding\Generate-WindowsAppBranding.ps1'
$first = Get-FileHash 'IBM Granite with TurboQuant (Intel)\Assets\Branding\granite-edge-ai.ico','IBM Granite with TurboQuant (Intel)\Assets\*.png' -Algorithm SHA256
powershell -NoProfile -ExecutionPolicy Bypass -File '.\scripts\branding\Generate-WindowsAppBranding.ps1'
$second = Get-FileHash 'IBM Granite with TurboQuant (Intel)\Assets\Branding\granite-edge-ai.ico','IBM Granite with TurboQuant (Intel)\Assets\*.png' -Algorithm SHA256
if (Compare-Object $first.Hash $second.Hash) { throw 'Branding generation is not deterministic.' }
```

Run the source contract command. Expected: all branding and visual contracts PASS.

- [ ] **Step 5: Build the application and commit**

```powershell
dotnet build 'IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj' -c Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64 -p:AppxPackageSigningEnabled=false -p:GenerateAppxPackageOnBuild=false --nologo
```

Expected: build succeeds with zero errors. Then:

```powershell
git add -- 'scripts/branding/Generate-WindowsAppBranding.ps1' 'IBM Granite with TurboQuant (Intel)/Assets' 'IBM Granite with TurboQuant (Intel)/MainWindow.xaml.cs' 'IBM Granite with TurboQuant (Intel)/Package.appxmanifest' 'IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj' 'tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs'
git commit -m 'feat(branding): apply granite windows identity'
```

### Task 6: Run release verification and review the completed branch

**Files:**
- Modify only if a verification failure proves a production or test defect in a Phase 1 file.

- [ ] **Step 1: Run focused source and packaged UI suites**

Run the source command. Run packaged tests with:

```powershell
$filter = 'FullyQualifiedName~GraniteEdgeAI.UnitTests.Features.GgufRuntime'
```

Expected: all focused source and packaged GGUF UI cases PASS.

- [ ] **Step 2: Run the full GGUF verification gate**

Close any running Granite Edge AI preview first so the app executable is not locked, then run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File '.\scripts\gguf-runtime\Invoke-GgufChatVerification.ps1'
```

Expected: all contract/unit/integration suites pass; only the documented controlled real-model test may skip when no local model is configured; the Release application build succeeds.

- [ ] **Step 3: Run repository hygiene checks**

```powershell
git diff --check
git status --short
git log -8 --oneline
```

Expected: `git diff --check` has no output and the worktree is clean.

- [ ] **Step 4: Perform code review**

Use `superpowers:requesting-code-review`. Review against `docs/superpowers/specs/2026-08-21-chat-production-polish-phase1-design.md`, with particular attention to truthful status, narrow layout, selected/date contrast, disabled Send, asset reproducibility, and no deferred-feature simulation.

- [ ] **Step 5: Fix findings test-first and re-run affected gates**

For each valid finding, add or tighten a failing regression test, reproduce RED, make the smallest production change, and rerun the focused test plus Steps 1–3. Commit each coherent correction separately.

- [ ] **Step 6: Prepare branch handoff**

Use `superpowers:verification-before-completion`, then `superpowers:finishing-a-development-branch`. Report the branch name, commits, exact passing test counts, expected skips, build result, and the existing preview command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File '.\scripts\Run-ChatPreview.ps1'
```
