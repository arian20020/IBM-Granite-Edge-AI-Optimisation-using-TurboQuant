# GGUF Chat Template and Copy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make imported GGUF models use their embedded chat template, prevent Granite from continuing into fabricated turns, and add selectable text plus per-message and whole-conversation copying.

**Architecture:** Configure the existing LLamaSharp `ChatSession` with a model-backed `PromptTemplateTransformer`; verify the behavior through the controlled Granite model. Keep clipboard access behind a small WinUI boundary, format complete conversations with a pure formatter, and let `ChatPage` coordinate copy commands while existing message controls continue to update in place during streaming.

**Tech Stack:** C# 12, .NET 8, WinUI 3, LLamaSharp 0.27.0, Windows ApplicationModel DataTransfer clipboard APIs, MSTest 4.3.2.

---

## File structure

- Modify `runtime/GraniteEdgeAI.GgufRuntime.NativeAdapter/LlamaSharpInferenceEngine.cs` to activate the GGUF prompt template.
- Modify `tests/UnitTests/GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests/LlamaSharpRealModelSmokeTests.cs` to exercise a real formatted turn and reject transcript continuation.
- Modify `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufRealModelSmokeTests.cs` to assert the packaged process preserves the same response boundary.
- Create `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Clipboard/IChatClipboard.cs` as the test seam.
- Create `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Clipboard/WindowsChatClipboard.cs` as the only Windows clipboard adapter.
- Create `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Clipboard/ChatTranscriptFormatter.cs` for deterministic role-labelled plain text.
- Create `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Clipboard/ChatTranscriptFormatterTests.cs` for pure formatting behavior.
- Modify `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatMessageBubble.xaml` and `.xaml.cs` for text selection, hover/focus copy controls, and feedback.
- Modify `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml` and `.xaml.cs` for whole-chat copy and clipboard coordination.
- Modify `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatPageTests.cs` for WinUI structure, accessibility, clipboard success/failure, and stable streaming controls.

For every packaged WinUI test step below, set `$filter` as shown and run:

```powershell
$configuration='Release'
$testProject='tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj'
dotnet build $testProject -c $configuration -p:Platform=x64 -p:RuntimeIdentifier=win-x64 --nologo
if ($LASTEXITCODE -ne 0) { throw 'Packaged test build failed.' }
$recipe=(Resolve-Path "tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\$configuration\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe").Path
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vstest=& $vswhere -latest -products * -find '**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' | Select-Object -First 1
& $vstest $recipe '/Platform:x64' '/Logger:Console;Verbosity=minimal' "/TestCaseFilter:$filter"
if ($LASTEXITCODE -ne 0) { throw 'Packaged test run failed.' }
```

### Task 1: Activate the model-native chat template

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests/LlamaSharpRealModelSmokeTests.cs`
- Modify: `runtime/GraniteEdgeAI.GgufRuntime.NativeAdapter/LlamaSharpInferenceEngine.cs`

- [ ] **Step 1: Extend the controlled adapter test to capture one complete answer**

Add a second test that initializes the controlled model, streams a short answer, and rejects line-leading synthetic role turns and repeated empty fences:

```csharp
using System.Text.RegularExpressions;

[TestMethod]
[TestCategory("ControlledRuntime")]
public async Task ControlledGraniteModelUsesOneAssistantTurn()
{
    string? model = Environment.GetEnvironmentVariable(ModelVariable);
    if (string.IsNullOrWhiteSpace(model))
    {
        Assert.Inconclusive("controlled GGUF adapter model not configured");
    }

    var options = new GgufAdapterOptions(
        Path.GetFullPath(model!), 2048,
        GgufAdapterCacheType.F16, GgufAdapterCacheType.F16,
        0, 4, 256, false, 96);
    await using var engine = new LlamaSharpInferenceEngine(options);
    await engine.InitializeAsync([], CancellationToken.None);

    var chunks = new List<string>();
    await foreach (string chunk in engine.GenerateAsync(
        "Introduce yourself in one short sentence.", CancellationToken.None))
    {
        chunks.Add(chunk);
    }

    string answer = string.Concat(chunks);
    Assert.IsFalse(string.IsNullOrWhiteSpace(answer));
    Assert.IsFalse(Regex.IsMatch(
        answer,
        @"(?im)^\s*(?:me|user|assistant)\s*:"));
    Assert.IsFalse(Regex.IsMatch(
        answer,
        @"(?m)(?:^\s*```\s*$\r?\n){2,}"));
}
```

- [ ] **Step 2: Run the controlled test against the known Granite model and confirm the defect**

Run:

```powershell
$env:GRANITE_GGUF_ADAPTER_TEST_MODEL='C:\Users\Arian\Downloads\granite-4.1-3b-Q4_K_M.gguf'
dotnet run --project 'tests\UnitTests\GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests\GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests.csproj' -c Release -- --filter FullyQualifiedName~ControlledGraniteModelUsesOneAssistantTurn
```

Expected before the fix: FAIL because the untemplated session continues into a role-labelled transcript or repeated fences. If sampling happens to stop early, retain the regression and verify the missing transform directly in Step 4; do not weaken the output assertions.

- [ ] **Step 3: Apply LLamaSharp's GGUF-backed history transformer**

Add the transformer namespace and change session construction to:

```csharp
using LLama.Transformers;

var executor = new InteractiveExecutor(_context);
_session = new ChatSession(executor, CreateHistory(initialHistory))
    .WithHistoryTransform(new PromptTemplateTransformer(
        _weights,
        withAssistant: true));
```

The named argument documents that the rendered prompt ends with the assistant-generation boundary. Do not add Granite-specific raw tokens or response sanitization.

- [ ] **Step 4: Run the adapter tests and confirm the transformer and response boundary pass**

Run:

```powershell
dotnet run --project 'tests\UnitTests\GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests\GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests.csproj' -c Release -- --progress off
```

Expected: all ordinary adapter tests pass; both controlled tests pass while the model variable is set.

- [ ] **Step 5: Commit the runtime formatting fix**

```powershell
git add -- 'runtime/GraniteEdgeAI.GgufRuntime.NativeAdapter/LlamaSharpInferenceEngine.cs' 'tests/UnitTests/GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests/LlamaSharpRealModelSmokeTests.cs'
git commit -m 'fix(runtime): apply embedded gguf chat template'
```

### Task 2: Add deterministic clipboard formatting and the Windows boundary

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Clipboard/IChatClipboard.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Clipboard/WindowsChatClipboard.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Clipboard/ChatTranscriptFormatter.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Clipboard/ChatTranscriptFormatterTests.cs`

- [ ] **Step 1: Write failing formatter tests**

Create tests covering chronological role labels, blank-line separation, empty-message filtering, exact content preservation, and an in-progress partial:

```csharp
using GraniteEdgeAI.Features.GgufRuntime.Clipboard;
using GraniteEdgeAI.Features.GgufRuntime.History;

namespace GraniteEdgeAI.UnitTests.Features.GgufRuntime.Clipboard;

[TestClass]
public sealed class ChatTranscriptFormatterTests
{
    [TestMethod]
    public void FormatUsesApprovedRoleLabelledPlainText()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ChatMessage[] messages =
        [
            ChatMessage.User("Hello", now),
            ChatMessage.Assistant("Hi!", ChatCompletionStatus.Completed, now),
        ];

        Assert.AreEqual(
            $"You:{Environment.NewLine}Hello{Environment.NewLine}{Environment.NewLine}" +
            $"Granite Edge AI:{Environment.NewLine}Hi!",
            ChatTranscriptFormatter.Format(messages));
    }

    [TestMethod]
    public void FormatSkipsEmptyContentAndIncludesVisibleStreamingPartial()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ChatMessage[] messages =
        [
            ChatMessage.Assistant("", ChatCompletionStatus.Pending, now),
            ChatMessage.Assistant("Partial answer", ChatCompletionStatus.Streaming, now),
        ];

        Assert.AreEqual(
            $"Granite Edge AI:{Environment.NewLine}Partial answer",
            ChatTranscriptFormatter.Format(messages));
    }
}
```

- [ ] **Step 2: Run the formatter tests and verify they fail to compile**

Run:

Set `$filter = 'FullyQualifiedName~ChatTranscriptFormatterTests'` and run the
packaged WinUI command above.

Expected: FAIL because `ChatTranscriptFormatter` does not exist.

- [ ] **Step 3: Implement the formatter and clipboard seam**

Implement these focused types:

```csharp
namespace GraniteEdgeAI.Features.GgufRuntime.Clipboard;

internal interface IChatClipboard
{
    bool TrySetText(string text);
}
```

```csharp
using System;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;

namespace GraniteEdgeAI.Features.GgufRuntime.Clipboard;

internal sealed class WindowsChatClipboard : IChatClipboard
{
    public bool TrySetText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        try
        {
            var package = new DataPackage();
            package.SetText(text);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
            Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
            return true;
        }
        catch (Exception exception) when (
            exception is COMException or UnauthorizedAccessException or InvalidOperationException)
        {
            return false;
        }
    }
}
```

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using GraniteEdgeAI.Features.GgufRuntime.History;

namespace GraniteEdgeAI.Features.GgufRuntime.Clipboard;

internal static class ChatTranscriptFormatter
{
    internal static string Format(IReadOnlyList<ChatMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        return string.Join(
            Environment.NewLine + Environment.NewLine,
            messages
                .Where(message => !string.IsNullOrWhiteSpace(message.Content))
                .Select(message =>
                    $"{RoleLabel(message.Role)}:{Environment.NewLine}{message.Content}"));
    }

    private static string RoleLabel(ChatMessageRole role) => role switch
    {
        ChatMessageRole.User => "You",
        ChatMessageRole.Assistant => "Granite Edge AI",
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };
}
```

- [ ] **Step 4: Run formatter tests and the Release application build**

Run the filtered test command from Step 2, then:

```powershell
dotnet build 'IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj' -c Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64 --nologo
```

Expected: formatter tests pass and the application build has zero errors.

- [ ] **Step 5: Commit the clipboard foundation**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Clipboard' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Clipboard'
git commit -m 'feat(chat): add clipboard formatting boundary'
```

### Task 3: Make every message selectable and individually copyable

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatMessageBubble.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatMessageBubble.xaml.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatPageTests.cs`

- [ ] **Step 1: Write failing WinUI tests for message selection and copy affordance**

Add tests that instantiate both roles and verify selectable text, accessible copy controls, exact event content, and feedback:

```csharp
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;

[UITestMethod]
[TestCategory("WinUI")]
public void MessageTextIsSelectableAndCopyRaisesExactVisibleContent()
{
    var bubble = new ChatMessageBubble
    {
        MessageContent = "Line one\r\nLine two",
        IsUser = false,
    };
    TextBlock text = Assert.IsInstanceOfType<TextBlock>(bubble.FindName("MessageText"));
    Button copy = Assert.IsInstanceOfType<Button>(bubble.FindName("CopyMessageButton"));
    string? requested = null;
    bubble.CopyRequested += (_, content) => requested = content;

    Assert.IsTrue(text.IsTextSelectionEnabled);
    Assert.AreEqual("Copy message", AutomationProperties.GetName(copy));
    Invoke(copy);
    Assert.AreEqual("Line one\r\nLine two", requested);

    bubble.ShowCopyResult(succeeded: true);
    Assert.AreEqual("Copied", AutomationProperties.GetName(copy));
}

private static void Invoke(Button button)
{
    var peer = new ButtonAutomationPeer(button);
    var provider = Assert.IsInstanceOfType<IInvokeProvider>(
        peer.GetPattern(PatternInterface.Invoke));
    provider.Invoke();
}
```

- [ ] **Step 2: Run the filtered test and verify it fails**

Run:

Set `$filter = 'FullyQualifiedName~MessageTextIsSelectableAndCopyRaisesExactVisibleContent'`
and run the packaged WinUI command above.

Expected: FAIL because the copy control/event and selection flag do not exist.

- [ ] **Step 3: Restructure the bubble without changing its role colors**

In XAML, retain the current bubble contents but place them in a role-aligned
container, set `MessageText.IsTextSelectionEnabled="True"`, and add this action
below the bubble:

```xml
<Button
    x:Name="CopyMessageButton"
    Margin="4,4,4,0"
    Padding="6"
    HorizontalAlignment="Left"
    Background="Transparent"
    BorderThickness="0"
    Opacity="0"
    AutomationProperties.Name="Copy message"
    ToolTipService.ToolTip="Copy message"
    Click="CopyMessageButton_Click"
    GotFocus="CopyMessageButton_GotFocus"
    LostFocus="CopyMessageButton_LostFocus">
    <FontIcon x:Name="CopyMessageGlyph" FontSize="14" Glyph="&#xE8C8;" />
</Button>
```

Give the root a transparent background and pointer-enter/exit handlers so the
button fades in on hover. In `ApplyRole`, align both the container and action to
the message role. Implement the event and feedback surface:

```csharp
public event EventHandler<string>? CopyRequested;

internal void ShowCopyResult(bool succeeded)
{
    CopyMessageGlyph.Glyph = succeeded ? "\uE73E" : "\uEA39";
    string label = succeeded ? "Copied" : "Couldn't copy";
    AutomationProperties.SetName(CopyMessageButton, label);
    ToolTipService.SetToolTip(CopyMessageButton, label);
    CopyMessageButton.Opacity = 1;
    RestartCopyFeedbackTimer();
}

private void CopyMessageButton_Click(object sender, RoutedEventArgs eventArguments)
{
    if (!string.IsNullOrEmpty(MessageContent))
    {
        CopyRequested?.Invoke(this, MessageContent);
    }
}
```

Use a `DispatcherQueueTimer` with a 1.5-second interval to restore the copy glyph,
accessible name, tooltip, and hover/focus opacity. Stop that timer on `Unloaded`.

- [ ] **Step 4: Run message tests and existing role/streaming tests**

Run:

Set `$filter = 'FullyQualifiedName~ChatPageTests.Message|FullyQualifiedName~ChatPageTests.Streaming'`
and run the packaged WinUI command above.

Expected: selectable/copy tests and existing role-surface/in-place streaming tests pass.

- [ ] **Step 5: Commit the per-message interaction**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatMessageBubble.xaml' 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatMessageBubble.xaml.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatPageTests.cs'
git commit -m 'feat(chat): add selectable per-message copy'
```

### Task 4: Wire per-message clipboard use and whole-chat copying

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatPageTests.cs`

- [ ] **Step 1: Write failing page-level clipboard tests**

Add a recording fake and tests for per-message success/failure, whole-chat text,
empty-chat disabling, and stable streaming controls:

```csharp
private sealed class RecordingClipboard(bool succeeds = true) : IChatClipboard
{
    internal string? Text { get; private set; }
    public bool TrySetText(string text)
    {
        Text = text;
        return succeeds;
    }
}

[UITestMethod]
[TestCategory("WinUI")]
public void CopyChatWritesOnlyTheOpenConversationInApprovedFormat()
{
    var clipboard = new RecordingClipboard();
    var page = new ChatPage(clipboard);
    DateTimeOffset now = DateTimeOffset.UtcNow;
    page.SynchronizeTranscript(Guid.NewGuid(),
    [
        ChatMessage.User("Question", now),
        ChatMessage.Assistant("Partial", ChatCompletionStatus.Streaming, now),
    ], forceFollowLatest: false);
    Button copy = Assert.IsInstanceOfType<Button>(page.FindName("CopyChatButton"));

    Invoke(copy);

    Assert.AreEqual(
        $"You:{Environment.NewLine}Question{Environment.NewLine}{Environment.NewLine}" +
        $"Granite Edge AI:{Environment.NewLine}Partial",
        clipboard.Text);
    Assert.AreEqual("Copied", AutomationProperties.GetName(copy));
}
```

Also add a test that obtains a rendered `ChatMessageBubble`, raises its copy
button, and asserts the fake receives only that bubble's content. Construct a
page with `new RecordingClipboard(false)` and assert both controls expose
`Couldn't copy` without throwing.

- [ ] **Step 2: Run the new page tests and verify they fail**

Run:

Set `$filter = 'FullyQualifiedName~CopyChat|FullyQualifiedName~CopyMessage'` and
run the packaged WinUI command above.

Expected: FAIL because injection, header action, and page-level routing do not exist.

- [ ] **Step 3: Add the header action and dependency injection seam**

Replace the row-zero header stack with a two-column grid. Keep the existing title
and model text on the left and add:

```xml
<Button
    x:Name="CopyChatButton"
    Grid.Column="1"
    HorizontalAlignment="Right"
    VerticalAlignment="Top"
    Padding="10,7"
    Background="Transparent"
    BorderThickness="0"
    IsEnabled="False"
    AutomationProperties.Name="Copy chat"
    ToolTipService.ToolTip="Copy chat"
    Click="CopyChatButton_Click">
    <StackPanel Orientation="Horizontal" Spacing="7">
        <FontIcon x:Name="CopyChatGlyph" FontSize="14" Glyph="&#xE8C8;" />
        <TextBlock x:Name="CopyChatLabel" Text="Copy chat" />
    </StackPanel>
</Button>
```

Add page state and constructors:

```csharp
private readonly IChatClipboard clipboard;
private IReadOnlyList<ChatMessage> currentMessages = [];

public ChatPage() : this(new WindowsChatClipboard())
{
}

internal ChatPage(IChatClipboard clipboard)
{
    this.clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
    InitializeComponent();
    Unloaded += ChatPage_Unloaded;
}
```

- [ ] **Step 4: Route both commands without rebuilding the transcript**

In `SynchronizeTranscript`, snapshot the supplied messages and enable the header
command only when formatting yields content. When creating a bubble, subscribe
once to its `CopyRequested` event. Implement:

```csharp
private void MessageBubble_CopyRequested(object? sender, string content)
{
    if (sender is ChatMessageBubble bubble)
    {
        bubble.ShowCopyResult(clipboard.TrySetText(content));
    }
}

private void CopyChatButton_Click(object sender, RoutedEventArgs eventArguments)
{
    string text = ChatTranscriptFormatter.Format(currentMessages);
    if (string.IsNullOrEmpty(text))
    {
        return;
    }

    ShowCopyChatResult(clipboard.TrySetText(text));
}
```

`ShowCopyChatResult` must change the icon, label, accessible name, and tooltip to
`Copied` or `Couldn't copy`, then restore `Copy chat` after 1.5 seconds. Reset
`currentMessages` and disable the action in `ResetTranscript`. Unsubscribe bubble
events before clearing controls. Do not replace existing bubble instances during
ordinary streaming updates.

- [ ] **Step 5: Run page clipboard, accessibility, and streaming tests**

Run:

Set `$filter = 'FullyQualifiedName~GraniteEdgeAI.UnitTests.Features.GgufRuntime.ChatPageTests'`
and run the packaged WinUI command above.

Expected: all `ChatPageTests` pass, including exact clipboard payloads and
`Assert.AreSame` checks for streamed bubble instances.

- [ ] **Step 6: Commit whole-chat copy and page routing**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml' 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatPageTests.cs'
git commit -m 'feat(chat): copy messages and current conversation'
```

### Task 5: Prove the packaged runtime response boundary

**Files:**
- Modify: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufRealModelSmokeTests.cs`

- [ ] **Step 1: Add packaged-process response-boundary assertions**

After collecting the first turn, concatenate its deltas and assert that the
worker does not expose fabricated turns:

```csharp
using System.Text.RegularExpressions;

string firstText = string.Concat(
    first.OfType<TextDeltaEvent>().Select(delta => delta.Text));
Assert.IsFalse(string.IsNullOrWhiteSpace(firstText));
Assert.IsFalse(Regex.IsMatch(
    firstText,
    @"(?im)^\s*(?:me|user|assistant)\s*:"));
Assert.IsFalse(Regex.IsMatch(
    firstText,
    @"(?m)(?:^\s*```\s*$\r?\n){2,}"));
```

Retain the existing second-turn, Stop, Close, and model-digest assertions.

- [ ] **Step 2: Run the controlled packaged smoke test**

Build the package, generate a local controlled configuration from the resulting
manifest and the verified model, then run the smoke test:

```powershell
dotnet build 'IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj' -c Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64 --nologo
$packageRoot=(Resolve-Path 'IBM Granite with TurboQuant (Intel)\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\GgufRuntime').Path
$manifestPath=Join-Path $packageRoot 'runtime-manifest.json'
$manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$modelPath=(Resolve-Path 'C:\Users\Arian\Downloads\granite-4.1-3b-Q4_K_M.gguf').Path
$controlledRoot='C:\g1-chat\.controlled\gguf-runtime'
New-Item -ItemType Directory -Force -Path $controlledRoot | Out-Null
$configurationPath=Join-Path $controlledRoot 'granite-4.1-3b-Q4_K_M.runtime.json'
$payload=[ordered]@{
    schemaVersion=1
    packageRoot=$packageRoot
    manifestSha256=(Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
    modelFile=$modelPath
    modelSha256=(Get-FileHash -LiteralPath $modelPath -Algorithm SHA256).Hash
    modelId='granite-4.1-3b-q4-k-m'
    runtimeBuildId=$manifest.runtimeBuildId
    runtimeSourceCommit=$manifest.runtimeSourceCommit
    backend='Cpu'
    deviceId='cpu'
    contextSize=2048
    keyCacheType='F16'
    valueCacheType='F16'
    gpuLayerCount=0
    flashAttention=$false
    threadCount=4
    batchSize=128
    evidenceGrade='controlled-smoke'
    profileId='cpu-controlled-smoke'
    maximumGeneratedTokens=96
}
[System.IO.File]::WriteAllText(
    $configurationPath,
    ($payload | ConvertTo-Json -Depth 4) + [Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false))
$env:GRANITE_GGUF_RUNTIME_TEST_CONFIG=$configurationPath
dotnet run --project 'tests\IntegrationTests\GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests\GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests.csproj' -c Release -- --filter FullyQualifiedName~VerifiedLocalRuntimeLoadsStreamsTwoTurnsStopsAndCloses
```

Expected: PASS with two real turns, one stopped turn, a clean close, unchanged
model digest, and no transcript continuation in the first answer.

- [ ] **Step 3: Commit the packaged regression**

```powershell
git add -- 'tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufRealModelSmokeTests.cs'
git commit -m 'test(runtime): reject fabricated chat turns'
```

### Task 6: Run complete verification and prepare the user test path

**Files:**
- Modify only if the commands reveal a genuine defect in an in-scope file.

- [ ] **Step 1: Run the full GGUF suite and self-contained application build**

Ensure no preview application process is holding the Debug or Release apphost,
then run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File '.\scripts\gguf-runtime\Invoke-GgufChatVerification.ps1'
```

Expected: all GGUF contract/unit/integration tests pass, only controlled tests
without configured inputs are skipped, and the Release/x64 app build succeeds.

- [ ] **Step 2: Run the packaged WinUI chat tests**

Set `$filter = 'FullyQualifiedName~Features.GgufRuntime'` and run the packaged
WinUI command above.

Expected: all GGUF chat UI/history/controller tests pass with zero failures.

- [ ] **Step 3: Run both controlled real-model gates**

Set `GRANITE_GGUF_ADAPTER_TEST_MODEL` to the verified Granite GGUF and
`GRANITE_GGUF_RUNTIME_TEST_CONFIG` to the existing trusted package configuration,
then rerun the two filtered controlled tests from Tasks 1 and 5.

Expected: both pass and produce one assistant turn per prompt.

- [ ] **Step 4: Launch the app for manual acceptance**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File '.\scripts\Run-ChatPreview.ps1'
```

In the app, import the verified Granite model, choose **Open in Chat**, start a
new chat, and verify: normal single-turn output; smooth streaming without a white
blank; selectable substrings; per-message copy; header **Copy chat** role-labelled
text; keyboard access; and success feedback. Close the app before rebuilding to
avoid the known apphost file lock.

- [ ] **Step 5: Inspect the final diff and commit any verification-only correction**

```powershell
git diff --check
git status --short
git log --oneline -6
```

Expected: no whitespace errors, no unintended `.superpowers/` artifacts staged,
and only the planned feature commits after the approved design/plan commits.
