# Chat Interaction and Streaming Smoothness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep the short-height sidebar separated, make the empty composer compact, support Enter-to-send with Shift+Enter for newlines, and render inference incrementally without flashing or forced scrolling.

**Architecture:** Preserve the existing WinUI controls and coordinator contracts, but separate history and transcript synchronization. Use stable message IDs to update bubbles in place, coalesce dispatcher renders, avoid durable writes for every transient delta, and scroll only when the viewport was already near the bottom or a new turn explicitly requested following.

**Tech Stack:** C# 12, .NET 8, WinUI 3, Windows App SDK 2.2, XAML theme resources, MSTest 4.3.2, packaged Visual Studio test runner, PowerShell 5.1.

---

## File map

- Modify `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml`: constrain history and separate the Settings footer.
- Modify `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml`: remove hidden spacing, compact controls, and connect keyboard handling.
- Modify `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml.cs`: share guarded submission and implement Enter/Shift+Enter semantics.
- Modify `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml.cs`: synchronize message bubbles by stable ID and apply bottom-aware scrolling.
- Modify `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatDemoController.cs`: separate history rendering, coalesce UI work, and request following for new turns.
- Create `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatRenderScheduler.cs`: own the one-pending-render invariant and disposal behavior.
- Modify `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Services/GgufChatCoordinator.cs`: publish every delta to memory/UI while persisting only meaningful checkpoints.
- Modify `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatPageTests.cs`: sidebar containment, keyed bubble reuse, reset, and scroll-policy tests.
- Modify `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Controls/ChatComposerTests.cs`: compact geometry and keyboard-submission tests.
- Modify `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Services/GgufChatCoordinatorTests.cs`: exact checkpoint-persistence coverage.
- Create `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatRenderSchedulerTests.cs`: coalescing, newest-state, enqueue-failure, and disposal tests.
- Modify `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs`: source-level layout and anti-full-rebuild contracts.

## Test command conventions

Run source contracts with:

```powershell
dotnet run --project 'tests\IntegrationTests\GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests\GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests.csproj' -c Release -- --filter 'FullyQualifiedName~GgufChatVisualContractTests' --progress off
```

Build and run packaged tests with:

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

### Task 1: Constrain history and separate the Settings footer

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatPageTests.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs`

- [ ] **Step 1: Write failing layout tests**

Add a packaged test that resolves `HistoryRegion` and `SettingsFooter` and requires a constrained two-row history grid plus a separated footer:

```csharp
Grid historyRegion = Assert.IsInstanceOfType<Grid>(page.FindName("HistoryRegion"));
Border settingsFooter = Assert.IsInstanceOfType<Border>(page.FindName("SettingsFooter"));
ListView history = Assert.IsInstanceOfType<ListView>(page.FindName("ChatHistoryList"));

Assert.AreEqual(2, historyRegion.RowDefinitions.Count);
Assert.AreEqual(GridUnitType.Auto, historyRegion.RowDefinitions[0].Height.GridUnitType);
Assert.AreEqual(GridUnitType.Star, historyRegion.RowDefinitions[1].Height.GridUnitType);
Assert.AreEqual(1, Grid.GetRow(history));
Assert.AreEqual(new Thickness(0, 12, 0, 0), settingsFooter.Margin);
Assert.AreEqual(new Thickness(0, 1, 0, 0), settingsFooter.BorderThickness);
Assert.AreEqual(new Thickness(0, 12, 0, 0), settingsFooter.Padding);
```

Tighten the source contract to require `HistoryRegion`, `SettingsFooter`, and the same grid-row/margin/border values. Reject a `StackPanel` as the direct owner of `ChatHistoryList`.

- [ ] **Step 2: Run the source and packaged page tests; verify RED**

Run the source command. Then set:

```powershell
$filter = 'FullyQualifiedName~GraniteEdgeAI.UnitTests.Features.GgufRuntime.ChatPageTests'
```

and run the packaged command. Expected: FAIL because the history list is currently inside an unconstrained `StackPanel` and Settings has no footer divider.

- [ ] **Step 3: Implement the constrained history/footer layout**

Replace the row-3 stack with:

```xml
<Grid x:Name="HistoryRegion" Grid.Row="3" RowSpacing="10">
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto" />
        <RowDefinition Height="*" />
    </Grid.RowDefinitions>
    <TextBlock
        Text="CHATS"
        FontSize="13"
        FontWeight="SemiBold"
        Foreground="{ThemeResource GgufChatMutedBrush}" />
    <ListView
        x:Name="ChatHistoryList"
        Grid.Row="1"
        Padding="0"
        Margin="0"
        HorizontalContentAlignment="Stretch"
        SelectionMode="None"
        AutomationProperties.Name="Dated chat history">
        <ListView.ItemContainerStyle>
            <Style TargetType="ListViewItem">
                <Setter Property="Padding" Value="0" />
                <Setter Property="Margin" Value="0" />
                <Setter Property="HorizontalContentAlignment" Value="Stretch" />
            </Style>
        </ListView.ItemContainerStyle>
    </ListView>
</Grid>
```

Move the existing `SettingsButton` inside this complete row-4 footer:

```xml
<Border
    x:Name="SettingsFooter"
    Grid.Row="4"
    Margin="0,12,0,0"
    Padding="0,12,0,0"
    BorderBrush="{ThemeResource GgufChatBorderBrush}"
    BorderThickness="0,1,0,0">
    <Button
        x:Name="SettingsButton"
        Height="46"
        HorizontalAlignment="Stretch"
        HorizontalContentAlignment="Left"
        Padding="4,8"
        Style="{StaticResource GgufChatNavigationButtonStyle}"
        AutomationProperties.Name="Settings">
        <Grid ColumnSpacing="12">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="20" />
                <ColumnDefinition Width="*" />
            </Grid.ColumnDefinitions>
            <FontIcon FontSize="16" Glyph="&#xE713;" />
            <TextBlock
                Grid.Column="1"
                VerticalAlignment="Center"
                Text="Settings" />
        </Grid>
    </Button>
</Border>
```

- [ ] **Step 4: Re-run the same tests; verify GREEN**

Expected: all source contracts and packaged page cases pass.

- [ ] **Step 5: Commit**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatPageTests.cs' 'tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs'
git commit -m 'style(chat): separate the short-height settings footer'
```

### Task 2: Compact the empty composer without residual attachment spacing

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Controls/ChatComposerTests.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs`

- [ ] **Step 1: Write failing compact-geometry tests**

Update `ComposerUsesSingleSurfaceAndCenteredGrowingPrompt` to require:

```csharp
Grid contentGrid = Assert.IsInstanceOfType<Grid>(surface.Child);
FrameworkElement attachmentPresentation = Assert.IsInstanceOfType<FrameworkElement>(
    composer.FindName("AttachmentPresentation"));

Assert.AreEqual(new Thickness(6), surface.Padding);
Assert.AreEqual(0, contentGrid.RowSpacing);
Assert.AreEqual(new Thickness(0, 0, 0, 8), attachmentPresentation.Margin);
Assert.AreEqual(40, promptRow.MinHeight);
Assert.AreEqual(36, prompt.MinHeight);
Assert.AreEqual(40, send.Height);
Assert.AreEqual(40, stop.Height);
Assert.AreEqual(new CornerRadius(20), surface.CornerRadius);
```

Add this rendered regression test using the existing `SequenceKnowledgeFilePicker`, `Candidate`, and `WaitForLayoutAsync` helpers. It asserts the hidden-attachment baseline is at most 52 pixels plus one pixel of layout tolerance and that visible rejection feedback grows the surface:

```csharp
[UITestMethod]
[TestCategory("WinUI")]
public async Task HiddenAttachmentRowLeavesNoResidualComposerGap()
{
    var picker = new SequenceKnowledgeFilePicker(new[]
    {
        Candidate(@"C:\Knowledge\unsupported.pdf")
    });
    var composer = new ChatComposer(picker);
    Border surface = Assert.IsInstanceOfType<Border>(
        composer.FindName("ComposerSurface"));
    await using WinUiRenderHost host =
        await WinUiRenderHost.ShowAsync(composer, 700, 180);
    await WaitForLayoutAsync(surface);
    double compactHeight = surface.ActualHeight;

    Assert.IsLessThanOrEqualTo(53, compactHeight);

    await composer.AddKnowledgeFilesAsync();
    await WaitForLayoutAsync(surface);

    Assert.IsGreaterThan(compactHeight, surface.ActualHeight);
}
```

Extend the source contract with the same static values.

- [ ] **Step 2: Run source and packaged composer tests; verify RED**

Run the source command. Then set:

```powershell
$filter = 'FullyQualifiedName~GraniteEdgeAI.UnitTests.Features.GgufRuntime.Controls.ChatComposerTests'
```

and run the packaged command. Expected: FAIL on the current row spacing, 44-pixel row/buttons, 40-pixel textbox, and 12-by-6 surface padding.

- [ ] **Step 3: Implement exact compact geometry**

Change `ComposerSurface` from `CornerRadius="22"` and `Padding="12,6"` to these exact attributes, and change its child grid from `RowSpacing="8"` to `RowSpacing="0"`:

```xml
CornerRadius="20"
Padding="6"
RowSpacing="0"
```

Set `AttachmentPresentation` to `Margin="0,0,0,8"`; collapsed elements contribute no margin. Set `PromptRow.MinHeight="40"`, `PromptTextBox.MinHeight="36"`, `PromptTextBox.Padding="10,0"`, `SendButton.Height="40"`, and `StopButton.Height="40"`. Keep both attachment-button dimensions at 40 for touch accessibility and retain the 160-pixel multiline maximum. Change `ComposerFocusVisual.CornerRadius` to `22` so it follows the outer contour.

- [ ] **Step 4: Re-run the same tests; verify GREEN**

Expected: all source and packaged composer cases pass, including multiline growth, focus, attachments, and Stop.

- [ ] **Step 5: Commit**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Controls/ChatComposerTests.cs' 'tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs'
git commit -m 'style(chat): tighten the empty composer'
```

### Task 3: Add Enter-to-send and Shift+Enter newline behavior

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Controls/ChatComposerTests.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs`

- [ ] **Step 1: Write failing key-policy and shared-submission tests**

Add tests for the desired pure key decision:

```csharp
Assert.IsTrue(ChatComposer.IsSendKey(Windows.System.VirtualKey.Enter, isShiftPressed: false));
Assert.IsFalse(ChatComposer.IsSendKey(Windows.System.VirtualKey.Enter, isShiftPressed: true));
Assert.IsFalse(ChatComposer.IsSendKey(Windows.System.VirtualKey.Space, isShiftPressed: false));
```

Invoke the non-public `TrySubmitPrompt` method by reflection and require that it trims and raises once for non-empty text, returns false for whitespace, and returns false while `IsGenerating` is true. Retain the existing click test so keyboard and pointer paths are proven to share behavior. Update the source contract to require `KeyDown="PromptTextBox_KeyDown"`.

- [ ] **Step 2: Run source and packaged composer tests; verify RED**

Use the Task 2 commands. Expected: FAIL because `IsSendKey`, `TrySubmitPrompt`, and the XAML key handler do not exist.

- [ ] **Step 3: Implement the key handler and one guarded submission path**

Add `KeyDown="PromptTextBox_KeyDown"` to `PromptTextBox`. Add these namespaces:

```csharp
using Microsoft.UI.Input;
using Windows.System;
using Windows.UI.Core;
```

Implement:

```csharp
internal static bool IsSendKey(VirtualKey key, bool isShiftPressed) =>
    key == VirtualKey.Enter && !isShiftPressed;

private void PromptTextBox_KeyDown(object sender, KeyRoutedEventArgs eventArguments)
{
    bool isShiftPressed = InputKeyboardSource
        .GetKeyStateForCurrentThread(VirtualKey.Shift)
        .HasFlag(CoreVirtualKeyStates.Down);
    if (!IsSendKey(eventArguments.Key, isShiftPressed))
    {
        return;
    }

    eventArguments.Handled = true;
    TrySubmitPrompt();
}

private bool TrySubmitPrompt()
{
    string prompt = PromptTextBox.Text.Trim();
    if (IsGenerating || prompt.Length == 0)
    {
        return false;
    }

    PromptTextBox.Text = string.Empty;
    UpdateSubmissionState();
    SendRequested?.Invoke(this, prompt);
    return true;
}
```

Make `SendButton_Click` call only `TrySubmitPrompt()`. Plain Enter is handled even when submission is rejected, so whitespace cannot create stray blank lines. Shift+Enter is left unhandled and retains native multiline behavior.

- [ ] **Step 4: Re-run the same tests; verify GREEN**

Expected: key policy, guarded submission, click submission, multiline growth, and accessibility tests pass.

- [ ] **Step 5: Commit**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml' 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Controls/ChatComposerTests.cs' 'tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs'
git commit -m 'feat(chat): send on enter and preserve shift enter'
```

### Task 4: Stop durable writes and visual-tree rebuilds for every delta

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Services/GgufChatCoordinator.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Services/GgufChatCoordinatorTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatPageTests.cs`

- [ ] **Step 1: Write failing persistence and keyed-reuse tests**

Change the coordinator persistence assertion to an exact checkpoint count. With one new-chat save and one turn containing multiple deltas plus completion, require four total saves: new chat, user append, pending assistant, and terminal assistant.

```csharp
Assert.AreEqual(4, store.SaveCount);
```

Add page tests that create a conversation ID and stable message IDs, synchronize twice with longer assistant content, and require the same bubble instance:

```csharp
page.SynchronizeTranscript(conversationId, initialMessages, forceFollowLatest: false);
var transcript = Assert.IsInstanceOfType<ListView>(page.FindName("TranscriptList"));
ChatMessageBubble firstAssistant = Assert.IsInstanceOfType<ChatMessageBubble>(transcript.Items[1]);

page.SynchronizeTranscript(conversationId, updatedMessages, forceFollowLatest: false);

Assert.AreSame(firstAssistant, transcript.Items[1]);
Assert.AreEqual("Hello there", firstAssistant.MessageContent);
Assert.AreEqual(2, transcript.Items.Count);
```

Add a second test proving that a different conversation ID resets the item sequence and that an empty message list shows `EmptyConversationState`.

- [ ] **Step 2: Run the coordinator and page tests; verify RED**

Set:

```powershell
$filter = 'FullyQualifiedName~GraniteEdgeAI.UnitTests.Features.GgufRuntime.Services.GgufChatCoordinatorTests|FullyQualifiedName~GraniteEdgeAI.UnitTests.Features.GgufRuntime.ChatPageTests'
```

Run the packaged command. Expected: FAIL because every delta is persisted and `SynchronizeTranscript` does not exist.

- [ ] **Step 3: Publish deltas in memory and persist checkpoints**

Change `PublishAsync` to accept `bool persist`:

```csharp
private async Task PublishAsync(
    ChatConversation conversation,
    bool persist,
    CancellationToken cancellationToken)
{
    int index = conversations.FindIndex(item => item.Id == conversation.Id);
    if (index < 0)
    {
        throw new InvalidOperationException("The conversation is not active.");
    }

    conversations[index] = conversation;
    SelectedConversation = conversation;
    if (persist)
    {
        await store.SaveAsync(conversation, cancellationToken).ConfigureAwait(false);
    }

    ConversationChanged?.Invoke(this, EventArgs.Empty);
}
```

Call it with `persist: true` for the user append, pending assistant, completed, stopped, incomplete, and failed states. Call it with `persist: false` for `GgufChatDelta`. Track the latest unpersisted conversation and save it once in `finally` only when generation exits without a terminal event, preserving partial output after cancellation or an unexpected session failure.

- [ ] **Step 4: Implement keyed transcript synchronization**

In `ChatPage`, retain:

```csharp
private readonly Dictionary<Guid, ChatMessageBubble> transcriptBubbles = [];
private readonly List<Guid> renderedMessageIds = [];
private Guid? renderedConversationId;
```

Add `SynchronizeTranscript(Guid conversationId, IReadOnlyList<ChatMessage> messages, bool forceFollowLatest)`. Before mutation, compute whether output should be followed. If the conversation changed, the rendered sequence is longer than the model sequence, or any existing index has a different message ID, clear once and reset the dictionaries. Reuse existing bubbles by ID; otherwise create and append one. For every message, update `MessageContent`, `IsUser`, and formatted `StatusText`. Show the empty state for zero messages and call `ScrollIntoView` only when following is permitted.

Move status formatting from `ChatDemoController` into an internal static `ChatPage.FormatStatus(ChatCompletionStatus)` method so synchronization owns bubble presentation. Keep `ClearTranscript` and `AddMessage` removed from the controller path; delete them if no remaining caller exists.

- [ ] **Step 5: Re-run the same tests; verify GREEN**

Expected: exact save checkpoints, partial-output fallback persistence, same-bubble reuse, conversation reset, and empty-state behavior pass.

- [ ] **Step 6: Commit**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Services/GgufChatCoordinator.cs' 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Services/GgufChatCoordinatorTests.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatPageTests.cs'
git commit -m 'perf(chat): update streamed messages in place'
```

### Task 5: Coalesce renders, stabilize history, and respect manual scrolling

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatRenderScheduler.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatDemoController.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatRenderSchedulerTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatPageTests.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs`

- [ ] **Step 1: Write failing scheduler and scroll-policy tests**

Test a scheduler with a fake enqueue function that captures callbacks. Call `Request()` three times before executing the callback and require one enqueue and one render. Execute it, request again, and require a second enqueue. Test a rejected enqueue can be retried and disposal makes a captured callback harmless.

Add pure scroll-policy cases:

```csharp
Assert.IsTrue(ChatPage.ShouldFollowOutput(verticalOffset: 500, scrollableHeight: 520));
Assert.IsTrue(ChatPage.ShouldFollowOutput(verticalOffset: 520, scrollableHeight: 520));
Assert.IsFalse(ChatPage.ShouldFollowOutput(verticalOffset: 300, scrollableHeight: 520));
Assert.IsTrue(ChatPage.ShouldFollowOutput(verticalOffset: 0, scrollableHeight: 0));
```

Extend the source contract to reject `page.ClearHistory();` and `page.ClearTranscript();` inside `ChatDemoController.Render`, require `ChatRenderScheduler`, and require `SynchronizeTranscript`.

- [ ] **Step 2: Run source, scheduler, and page tests; verify RED**

Run the source command. Then set:

```powershell
$filter = 'FullyQualifiedName~GraniteEdgeAI.UnitTests.Features.GgufRuntime.ChatRenderSchedulerTests|FullyQualifiedName~GraniteEdgeAI.UnitTests.Features.GgufRuntime.ChatPageTests'
```

and run the packaged command. Expected: FAIL because the scheduler and scroll policy do not exist and the controller still performs full rebuilds.

- [ ] **Step 3: Implement the one-pending-render scheduler**

Create an internal sealed `ChatRenderScheduler` with constructor `ChatRenderScheduler(Func<Action, bool> enqueue, Action render)`. Under a private lock, `Request()` does nothing when disposed or already pending; otherwise it marks pending and submits one `Drain` callback. If enqueue returns false, clear pending so a later request can retry. `Drain` clears pending, checks disposal, then invokes `render`. `Dispose` marks disposed and prevents captured callbacks from rendering.

- [ ] **Step 4: Split controller history/transcript rendering**

Construct the scheduler with:

```csharp
renderScheduler = new ChatRenderScheduler(
    callback => page.DispatcherQueue.TryEnqueue(callback),
    Render);
```

Make `Coordinator_ConversationChanged` call only `renderScheduler.Request()`. Store a `List<HistoryRenderKey>` where each key contains group label, conversation ID, title, and selected state. In `RenderHistoryIfChanged`, build the newest key sequence; call the existing clear/add history methods only when `SequenceEqual` reports a structural change.

In `Render`, call `RenderHistoryIfChanged`, then:

```csharp
if (coordinator.SelectedConversation is ChatConversation selected)
{
    page.SynchronizeTranscript(
        selected.Id,
        selected.Messages,
        forceFollowLatest: consumeFollowLatest);
}
```

Set `consumeFollowLatest` for a newly submitted turn before calling `SendAsync`. Dispose the scheduler before unsubscribing/disposing coordinator resources.

- [ ] **Step 5: Implement bottom-aware scrolling**

Add:

```csharp
internal static bool ShouldFollowOutput(double verticalOffset, double scrollableHeight) =>
    scrollableHeight - verticalOffset <= 48;
```

Find the transcript's descendant `ScrollViewer` after load using a small recursive visual-tree helper. Before synchronizing, capture `forceFollowLatest || viewer is null || ShouldFollowOutput(viewer.VerticalOffset, viewer.ScrollableHeight)`. Scroll the last item only when that captured value is true. Because the decision is made before content increases `ScrollableHeight`, a user already at the bottom continues following while a user who moved upward is left undisturbed.

- [ ] **Step 6: Re-run source and packaged focused tests; verify GREEN**

Run the source command and the Task 5 packaged filter. Expected: scheduler coalescing, retry/disposal, scroll policy, keyed updates, and source anti-rebuild contracts pass.

- [ ] **Step 7: Commit**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatRenderScheduler.cs' 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatDemoController.cs' 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatRenderSchedulerTests.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatPageTests.cs' 'tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs'
git commit -m 'perf(chat): coalesce streamed ui rendering'
```

### Task 6: Run release verification and independent review

**Files:**
- Modify only if a failing test or review finding proves a defect in the files above.

- [ ] **Step 1: Run focused source and packaged GGUF tests**

Run the source command. Set:

```powershell
$filter = 'FullyQualifiedName~GraniteEdgeAI.UnitTests.Features.GgufRuntime'
```

Run the packaged command. Expected: every focused source and packaged GGUF UI test passes with zero build warnings/errors.

- [ ] **Step 2: Run the complete GGUF verification gate**

Confirm no preview executable from `C:\g1-chat` is running, then run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File '.\scripts\gguf-runtime\Invoke-GgufChatVerification.ps1'
```

Expected: all contract/unit/integration suites pass; only `VerifiedLocalRuntimeLoadsStreamsTwoTurnsStopsAndCloses` may skip when no controlled local model is configured; the Release application build succeeds with zero warnings/errors.

- [ ] **Step 3: Run repository hygiene checks**

```powershell
git diff --check
git status --short
git log -10 --oneline
```

Expected: no diff-check output and a clean worktree.

- [ ] **Step 4: Request code review**

Use `superpowers:requesting-code-review` against `docs/superpowers/specs/2026-08-21-chat-interaction-smoothness-design.md`. Require review of short-height containment, exact composer height, keyboard modifiers, delta persistence, keyed-control reuse, scheduler races/disposal, scroll behavior, accessibility, and unchanged persistence schema/runtime protocol.

- [ ] **Step 5: Fix valid findings test-first**

For each valid finding, add or tighten a regression test, verify the expected RED failure, implement the smallest correction, rerun the focused gate, and commit one coherent fix. Then repeat Steps 1 through 3.

- [ ] **Step 6: Prepare the user handoff**

Use `superpowers:verification-before-completion`, then `superpowers:finishing-a-development-branch`. Report the branch, commits, exact test totals, expected skip, build result, and preview command:

```powershell
cd C:\g1-chat
powershell -NoProfile -ExecutionPolicy Bypass -File '.\scripts\Run-ChatPreview.ps1'
```
