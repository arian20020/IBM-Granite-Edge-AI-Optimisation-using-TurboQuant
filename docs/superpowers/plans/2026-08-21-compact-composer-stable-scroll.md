# Compact Composer and Stable Transcript Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reduce the chat composer to an approximately 44-pixel empty height and remove transcript virtualization flashes during send and streaming.

**Architecture:** Preserve the existing WinUI `ListView` and stable message-bubble synchronization. Tighten only the composer geometry, then coalesce bottom-scroll work behind a one-shot natural `LayoutUpdated` handler, replacing direct per-update `ScrollIntoView` calls with `ScrollViewer.ChangeView` only after layout establishes overflow.

**Tech Stack:** C# 12, .NET 8, WinUI 3, Windows App SDK 2.2, XAML, MSTest 4.3.2, packaged Visual Studio test runner, PowerShell 5.1.

---

## File map

- Modify `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml`: compact surface, row, text box, and action-button dimensions.
- Modify `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml.cs`: coalesce deferred overflow-only bottom scrolling and cancel its pending layout handler on unload.
- Modify `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Controls/ChatComposerTests.cs`: require the new compact dimensions and measured height.
- Modify `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatPageTests.cs`: require non-empty transcript visibility through repeated synchronization.
- Modify `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs`: enforce compact XAML values and reject direct transcript `ScrollIntoView` calls.

## Packaged test command

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

### Task 1: Compact the composer

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Controls/ChatComposerTests.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml`

- [ ] **Step 1: Write failing geometry tests**

Update the packaged geometry assertions to require `PromptRow.MinHeight`, attachment/send/stop heights, and prompt minimum height of `40`, `40`, `40`, `40`, and `32` respectively. Require `ComposerSurface.Padding` to equal `6,1`, and tighten the measured empty-surface assertion to `45` pixels or less.

Update the source contract to require the same XAML values:

```csharp
Assert.AreEqual("40", promptRow.Attribute("MinHeight")?.Value);
Assert.AreEqual("40", attachmentButton.Attribute("Height")?.Value);
Assert.AreEqual("32", prompt.Attribute("MinHeight")?.Value);
Assert.AreEqual("40", sendButton.Attribute("Height")?.Value);
Assert.AreEqual("40", stopButton.Attribute("Height")?.Value);
Assert.AreEqual("6,1", composerSurface.Attribute("Padding")?.Value);
```

- [ ] **Step 2: Run tests and verify RED**

Run the source contracts and the packaged filter:

```powershell
dotnet run --project 'tests\IntegrationTests\GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests\GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests.csproj' -c Release -- --filter 'FullyQualifiedName~GgufChatVisualContractTests' --progress off
$filter = 'FullyQualifiedName~GraniteEdgeAI.UnitTests.Features.GgufRuntime.Controls.ChatComposerTests'
```

Expected: failures report the current 40/36 dimensions, `6,5` padding, and approximately 52-pixel measured height.

- [ ] **Step 3: Implement the compact geometry**

In `ChatComposer.xaml`, set:

```xml
Padding="6,1"
...
<Grid x:Name="PromptRow" MinHeight="40" ...>
...
<Button x:Name="AttachmentButton" Width="40" Height="40" ... />
<TextBox x:Name="PromptTextBox" MinHeight="32" ... />
<Button x:Name="SendButton" Width="46" Height="40" ... />
<Button x:Name="StopButton" MinWidth="84" Height="40" ... />
```

Keep the multiline `MaxHeight`, attachment presentation, keyboard behavior, and centered alignments unchanged.

- [ ] **Step 4: Run focused tests and verify GREEN**

Run the Task 1 source and packaged commands. Expected: all composer cases pass, including multiline growth, attachments, focus, Enter/Shift+Enter, and Stop.

- [ ] **Step 5: Commit**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Controls/ChatComposerTests.cs' 'tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs'
git commit -m "style(chat): reduce composer height"
```

### Task 2: Remove transcript flashing from per-update scrolling

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatPageTests.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml.cs`

- [ ] **Step 1: Write failing stable-scroll contracts**

Add a packaged test that synchronizes a non-empty conversation twice, checks that `TranscriptList.Visibility` remains `Visible`, `EmptyConversationState.Visibility` remains `Collapsed`, and the existing bubble instance is retained.

Extend `StreamingRenderingIsIncrementalAndDispatcherCoalesced` with:

```csharp
Assert.IsFalse(page.Contains("TranscriptList.ScrollIntoView", StringComparison.Ordinal));
StringAssert.Contains(page, "TranscriptList.LayoutUpdated += TranscriptList_LayoutUpdated;");
StringAssert.Contains(page, "scrollViewer.ChangeView(");
Assert.IsFalse(page.Contains("UpdateLayout()", StringComparison.Ordinal));
```

- [ ] **Step 2: Run tests and verify RED**

Run the source contracts and packaged filter:

```powershell
dotnet run --project 'tests\IntegrationTests\GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests\GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests.csproj' -c Release -- --filter 'FullyQualifiedName~GgufChatVisualContractTests' --progress off
$filter = 'FullyQualifiedName~GraniteEdgeAI.UnitTests.Features.GgufRuntime.ChatPageTests|FullyQualifiedName~GraniteEdgeAI.UnitTests.Features.GgufRuntime.ChatRenderSchedulerTests'
```

Expected: the source contract fails because `ChatPage` still calls `TranscriptList.ScrollIntoView` directly and has no natural-layout follow path.

- [ ] **Step 3: Implement deferred overflow-only scrolling**

Track one pending follow request and subscribe to natural layout only once:

```csharp
transcriptFollowRequested = true;
transcriptFollowOriginOffset = requiresTranscriptReset
    ? 0
    : scrollViewer?.VerticalOffset ?? 0;
if (!transcriptFollowAwaitingLayout)
{
    transcriptFollowAwaitingLayout = true;
    TranscriptList.LayoutUpdated += TranscriptList_LayoutUpdated;
}
```

Replace both direct `TranscriptList.ScrollIntoView` sites with this coalesced request. In the one-shot layout callback, unsubscribe before applying the follow and require the current offset not to be below the recorded origin:

```csharp
private void ScrollTranscriptToEnd()
{
    ScrollViewer? scrollViewer = FindDescendant<ScrollViewer>(TranscriptList);
    bool shouldApplyFollow = transcriptFollowRequested &&
        scrollViewer is not null &&
        scrollViewer.VerticalOffset + 0.5 >= transcriptFollowOriginOffset;
    transcriptFollowRequested = false;
    if (shouldApplyFollow && scrollViewer!.ScrollableHeight > 0)
    {
        scrollViewer.ChangeView(null, scrollViewer.ScrollableHeight, null, true);
    }
}
```

The existing `ShouldFollowOutput` check remains the gate for requesting a follow operation. Conversation resets use an origin of zero; later non-following synchronizations and page unload remove the pending layout handler.

- [ ] **Step 4: Run focused tests and verify GREEN**

Run the Task 2 source and packaged commands. Expected: page tests pass for fresh overflow, manual scroll-away, and switching overflowing conversations, with no direct `ScrollIntoView` or forced `UpdateLayout` contract violation.

- [ ] **Step 5: Commit**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatPageTests.cs' 'tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs'
git commit -m "fix(chat): stabilize streamed transcript scrolling"
```

### Task 3: Full verification and review

**Files:**
- Verify all files changed by Tasks 1 and 2.

- [ ] **Step 1: Run focused packaged tests**

Set `$filter = 'FullyQualifiedName~GraniteEdgeAI.UnitTests.Features.GgufRuntime'` and run the packaged test command. Expected: all GGUF packaged UI tests pass.

- [ ] **Step 2: Run source contracts**

```powershell
dotnet run --project 'tests\IntegrationTests\GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests\GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests.csproj' -c Release -- --filter 'FullyQualifiedName~GgufChatVisualContractTests' --progress off
```

Expected: all chat visual contracts pass.

- [ ] **Step 3: Run the complete runtime gate**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File '.\scripts\gguf-runtime\Invoke-GgufChatVerification.ps1'
```

Expected: all configured tests pass, the controlled local-model test may skip when no model is configured, and the Release app build completes with zero warnings and errors.

- [ ] **Step 4: Check hygiene and request independent review**

```powershell
git diff --check
git status --short
```

Review against `docs/superpowers/specs/2026-08-21-compact-composer-stable-scroll-design.md`, focusing on composer height, transcript visibility, natural-layout scroll coalescing, manual-scroll cancellation, conversation switching, unload safety, and regressions.
