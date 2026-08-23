# Token-limit Completion and Continuation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Report token-budget exhaustion accurately, let the user continue the same assistant response, and improve small-Granite response discipline without exposing internal control prompts.

**Architecture:** Extend GGUF protocol v2 with a typed `Stop`/`Length` completion reason. Detect length using a suppressed one-token probe inside the LLamaSharp output transform, carry the reason through the worker and application domain, persist continuation as a hidden control turn, and render a continuation action only on the eligible assistant bubble.

**Tech Stack:** C# 12, .NET 8, LLamaSharp 0.27, WinUI 3, MSTest 4, JSON-framed worker IPC, PowerShell verification scripts.

---

## File map

- `shared/GraniteEdgeAI.GgufRuntime.Contracts/Protocol/GgufProtocolVersion.cs`: advance the GGUF protocol to v2.
- `shared/GraniteEdgeAI.GgufRuntime.Contracts/Events/GgufCompletionReason.cs`: own the public terminal-reason enum.
- `shared/GraniteEdgeAI.GgufRuntime.Contracts/Events/GgufRuntimeEvent.cs`: require the reason on `ResponseCompletedEvent`.
- `runtime/GraniteEdgeAI.GgufRuntime.NativeAdapter/GraniteGenerationBoundaryObserver.cs`: hold shared completion state for cloned transforms.
- `runtime/GraniteEdgeAI.GgufRuntime.NativeAdapter/GgufAdapterGenerationEvent.cs`: represent adapter text and completion as typed events.
- `runtime/GraniteEdgeAI.GgufRuntime.NativeAdapter/GraniteTurnBoundaryTextTransform.cs`: enforce the visible token budget and suppress the probe token.
- `runtime/GraniteEdgeAI.GgufRuntime.NativeAdapter/LlamaSharpInferenceEngine.cs`: add the system role, preflight the embedded template, and emit typed completion.
- `runtime/GraniteEdgeAI.GgufRuntime.NativeAdapter/GgufAdapterHost.cs`: emit `G1DONE stop|length`.
- `workers/GraniteEdgeAI.GgufRuntime.Worker/Session/GgufCliOutputParser.cs`: parse exact terminal and startup-failure frames.
- `workers/GraniteEdgeAI.GgufRuntime.Worker/Session/GgufCliSession.cs`: preserve the adapter startup-failure code.
- `workers/GraniteEdgeAI.GgufRuntime.Worker/Session/GgufSessionCoordinator.cs`: map adapter reasons in the in-process coordinator.
- `workers/GraniteEdgeAI.GgufRuntime.Worker/GgufWorkerHost.cs`: map adapter reasons in the production worker host.
- `infrastructure/GraniteEdgeAI.GgufRuntime.WorkerClient/GgufRuntimeStartupException.cs`: expose a safe typed startup failure.
- `infrastructure/GraniteEdgeAI.GgufRuntime.WorkerClient/GgufRuntimeSession.cs`: recognize startup failure instead of reporting a handshake error.
- `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/History/*`: persist `Control` turns and `LimitReached` status.
- `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Services/*`: map completion reasons and append continuation to the same response.
- `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatMessageBubble.*`: render and raise the continuation action.
- `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml.cs`: filter control turns and route continuation by message ID.
- `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatDemoController.cs`: coordinate continuation without interrupting copy or selection.
- Existing GGUF contract, adapter, worker, integration, history, coordinator, and WinUI test projects: provide regression coverage at every boundary.

### Task 1: Introduce the protocol-v2 completion reason

**Files:**
- Create: `shared/GraniteEdgeAI.GgufRuntime.Contracts/Events/GgufCompletionReason.cs`
- Modify: `shared/GraniteEdgeAI.GgufRuntime.Contracts/Protocol/GgufProtocolVersion.cs`
- Modify: `shared/GraniteEdgeAI.GgufRuntime.Contracts/Events/GgufRuntimeEvent.cs`
- Modify: `workers/GraniteEdgeAI.GgufRuntime.Worker/GgufWorkerHost.cs`
- Modify: `workers/GraniteEdgeAI.GgufRuntime.Worker/Session/GgufSessionCoordinator.cs`
- Test: `tests/ContractTests/GraniteEdgeAI.GgufRuntime.Contracts.Tests/GgufContractTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.GgufRuntime.Transport.Tests/GgufProtocolSerializerTests.cs`

- [ ] **Step 1: Write failing contract tests for the closed reason and required payload**

Add tests that construct both valid reasons, reject an undefined enum value, round-trip both values through `GgufProtocolSerializer`, and reject a v1 completion payload:

```csharp
[TestMethod]
[DataRow(GgufCompletionReason.Stop)]
[DataRow(GgufCompletionReason.Length)]
public void ResponseCompletedRequiresAClosedCompletionReason(
    GgufCompletionReason reason)
{
    var value = new ResponseCompletedEvent(
        GgufProtocolVersion.Current,
        Guid.NewGuid(),
        GgufSessionId.New(),
        4,
        reason);

    Assert.AreEqual(reason, value.Reason);
}

[TestMethod]
public void ResponseCompletedRejectsUnknownCompletionReason() =>
    Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
        new ResponseCompletedEvent(
            GgufProtocolVersion.Current,
            Guid.NewGuid(),
            GgufSessionId.New(),
            4,
            (GgufCompletionReason)99));
```

- [ ] **Step 2: Run the contract and transport projects and verify RED**

Run:

```powershell
dotnet test .\tests\ContractTests\GraniteEdgeAI.GgufRuntime.Contracts.Tests\GraniteEdgeAI.GgufRuntime.Contracts.Tests.csproj -c Release
dotnet test .\tests\UnitTests\GraniteEdgeAI.GgufRuntime.Transport.Tests\GraniteEdgeAI.GgufRuntime.Transport.Tests.csproj -c Release
```

Expected: compilation fails because `GgufCompletionReason` and the required `Reason` constructor argument do not exist.

- [ ] **Step 3: Add the enum, advance v2, and require the reason**

```csharp
namespace GraniteEdgeAI.GgufRuntime.Contracts.Events;

public enum GgufCompletionReason
{
    Stop,
    Length,
}
```

Change `GgufProtocolVersion.Current` to `2`, and replace the positional completion record with validation:

```csharp
public sealed record ResponseCompletedEvent : GgufRuntimeEvent
{
    public ResponseCompletedEvent(
        int protocolVersion,
        Guid requestId,
        GgufSessionId sessionId,
        long sequence,
        GgufCompletionReason reason)
        : base(protocolVersion, requestId, sessionId, sequence)
    {
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        Reason = reason;
    }

    public GgufCompletionReason Reason { get; }
}
```

Supply `GgufCompletionReason.Stop` at the two existing worker call sites temporarily so the solution remains buildable; Task 4 replaces those defaults with parsed reasons.

- [ ] **Step 4: Run the focused tests and verify GREEN**

Expected: both projects pass, including the two reason data rows and rejection tests.

- [ ] **Step 5: Commit the protocol boundary**

```powershell
git add shared/GraniteEdgeAI.GgufRuntime.Contracts workers/GraniteEdgeAI.GgufRuntime.Worker tests/ContractTests/GraniteEdgeAI.GgufRuntime.Contracts.Tests tests/UnitTests/GraniteEdgeAI.GgufRuntime.Transport.Tests
git commit -m "feat(runtime): add typed completion reasons"
```

### Task 2: Detect length with a suppressed probe token

**Files:**
- Create: `runtime/GraniteEdgeAI.GgufRuntime.NativeAdapter/GraniteGenerationBoundaryObserver.cs`
- Modify: `runtime/GraniteEdgeAI.GgufRuntime.NativeAdapter/GraniteTurnBoundaryTextTransform.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests/GraniteTurnBoundaryTextTransformTests.cs`

- [ ] **Step 1: Add failing transform tests**

Exercise natural termination, the 513th probe, and clone sharing without a real model:

```csharp
[TestMethod]
public async Task SourceEndBeforeProbeReportsStop()
{
    var observer = new GraniteGenerationBoundaryObserver();
    var transform = new GraniteTurnBoundaryTextTransform(2, observer);

    Assert.AreEqual("one two", await CollectAsync(
        transform.TransformAsync(Chunks("one", " two"))));
    Assert.AreEqual(GgufAdapterCompletionReason.Stop, observer.Reason);
}

[TestMethod]
public async Task ProbeTokenIsSuppressedAndReportsLength()
{
    var observer = new GraniteGenerationBoundaryObserver();
    var transform = new GraniteTurnBoundaryTextTransform(2, observer);

    Assert.AreEqual("one two", await CollectAsync(
        transform.TransformAsync(Chunks("one", " two", " hidden"))));
    Assert.AreEqual(GgufAdapterCompletionReason.Length, observer.Reason);
}

[TestMethod]
public async Task CloneSharesCompletionObserver()
{
    var observer = new GraniteGenerationBoundaryObserver();
    var original = new GraniteTurnBoundaryTextTransform(1, observer);
    ITextStreamTransform clone = original.Clone();

    Assert.AreEqual("visible", await CollectAsync(
        clone.TransformAsync(Chunks("visible", " hidden"))));
    Assert.AreEqual(GgufAdapterCompletionReason.Length, observer.Reason);
}

[TestMethod]
public void ResetClearsThePreviousTurnsReason()
{
    var observer = new GraniteGenerationBoundaryObserver();
    observer.Complete(GgufAdapterCompletionReason.Length);

    observer.Reset();

    Assert.IsNull(observer.Reason);
}
```

- [ ] **Step 2: Run the native-adapter tests and verify RED**

Run:

```powershell
dotnet test .\tests\UnitTests\GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests\GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests.csproj -c Release
```

Expected: compilation fails because the observer, reason, and bounded constructor are absent.

- [ ] **Step 3: Implement the observer and bounded source-token counting**

```csharp
internal enum GgufAdapterCompletionReason
{
    Stop,
    Length,
}

internal sealed class GraniteGenerationBoundaryObserver
{
    internal GgufAdapterCompletionReason? Reason { get; private set; }

    internal void Complete(GgufAdapterCompletionReason reason)
    {
        if (Reason is null)
        {
            Reason = reason;
        }
    }

    internal void Reset() => Reason = null;
}
```

Give `GraniteTurnBoundaryTextTransform` a positive `visibleTokenLimit` and shared observer. At the top of its source loop, increment `sourceTokenCount`; if the count exceeds the limit, record `Length` and `yield break` before appending the token. Record `Stop` before each existing role/fence `yield break` and after a natural source end. Return a new transform with the same limit and observer from `Clone()`.

- [ ] **Step 4: Run the native-adapter tests and verify GREEN**

Re-run the exact `dotnet test` command from Step 2. Expected: all existing cleanup tests plus the four new observer/boundary tests pass; no probe text is collected.

- [ ] **Step 5: Commit the detector**

```powershell
git add runtime/GraniteEdgeAI.GgufRuntime.NativeAdapter tests/UnitTests/GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests
git commit -m "feat(runtime): detect generated token limit"
```

### Task 3: Wire typed adapter completion and the Granite system instruction

**Files:**
- Create: `runtime/GraniteEdgeAI.GgufRuntime.NativeAdapter/GgufUnsupportedChatTemplateException.cs`
- Create: `runtime/GraniteEdgeAI.GgufRuntime.NativeAdapter/GgufAdapterGenerationEvent.cs`
- Modify: `runtime/GraniteEdgeAI.GgufRuntime.NativeAdapter/IGgufInferenceEngine.cs`
- Modify: `runtime/GraniteEdgeAI.GgufRuntime.NativeAdapter/LlamaSharpInferenceEngine.cs`
- Modify: `runtime/GraniteEdgeAI.GgufRuntime.NativeAdapter/GgufAdapterHost.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests/LlamaSharpInferenceEngineTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests/GgufAdapterHostTests.cs`

- [ ] **Step 1: Write failing tests for probe configuration, system history, and terminal frames**

Update the inference assertion from 512 to 513, assert a system message is first, data-drive host completion, and make a transformer failure produce the stable startup frame `G1FAIL chat-template-unsupported` rather than `model-load-failed`:

```csharp
Assert.AreEqual(513, inference.MaxTokens);
Assert.AreEqual(AuthorRole.System, history.Messages[0].AuthorRole);
StringAssert.Contains(history.Messages[0].Content, "Check numerical claims and units");

[TestMethod]
[DataRow(GgufAdapterCompletionReason.Stop, "G1DONE stop")]
[DataRow(GgufAdapterCompletionReason.Length, "G1DONE length")]
public async Task RunEmitsExactTypedCompletionFrame(
    GgufAdapterCompletionReason reason,
    string expectedFrame)
{
    using var reader = new StringReader("G1START\nG1PROMPT aGVsbG8=\n");
    using var writer = new StringWriter(CultureInfo.InvariantCulture);
    var host = new GgufAdapterHost(
        new RecordingEngine([new GgufAdapterTextDelta("answer"),
            new GgufAdapterCompleted(reason)]),
        reader,
        writer);

    Assert.AreEqual(0, await host.RunAsync(CancellationToken.None));
    StringAssert.Contains(writer.ToString(), expectedFrame);
}
```

- [ ] **Step 2: Run the native-adapter project and verify RED**

```powershell
dotnet test .\tests\UnitTests\GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests\GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests.csproj -c Release
```

Expected: failures show the old 512 cap, absent system role, and absent typed generation events.

- [ ] **Step 3: Introduce typed adapter events and configure one probe token**

```csharp
internal abstract record GgufAdapterGenerationEvent;
internal sealed record GgufAdapterTextDelta(string Text)
    : GgufAdapterGenerationEvent;
internal sealed record GgufAdapterCompleted(GgufAdapterCompletionReason Reason)
    : GgufAdapterGenerationEvent;
```

Change `IGgufInferenceEngine.GenerateAsync` to return `IAsyncEnumerable<GgufAdapterGenerationEvent>`. Compute the visible limit once:

```csharp
internal static int VisibleTokenLimit(GgufAdapterOptions options) =>
    Math.Min(options.MaximumGeneratedTokens,
        checked((int)options.ContextSize / 2));

MaxTokens = checked(VisibleTokenLimit(configuration) + 1);
```

Construct the observer and bounded transform during initialization. Call `observer.Reset()` immediately before every `ChatAsync` invocation; production session commands are serialized, so reset cannot race another generation. Emit `GgufAdapterTextDelta` for every nonempty transformed chunk, then exactly one `GgufAdapterCompleted(observer.Reason ?? GgufAdapterCompletionReason.Stop)` after enumeration. Add a two-generation engine test proving a first `Length` result does not leak into the next `Stop` result.

- [ ] **Step 4: Add and preflight the concise system role through the embedded template**

Define one constant system instruction and prepend it in `CreateHistory`. Build a single `PromptTemplateTransformer`, call `HistoryToText(history)` during initialization to fail before the session becomes ready if the embedded template cannot represent the system role, then pass that same transformer to `WithHistoryTransform`:

```csharp
private const string SystemInstruction =
    "You are Granite Edge AI, a concise general-purpose assistant. " +
    "Answer the user's question directly and accurately. " +
    "Distinguish facts from uncertainty and state when you are unsure. " +
    "Check numerical claims and units before stating them. " +
    "Use only as much detail as needed unless the user asks for more.";
```

Isolate the `HistoryToText(history)` call in its own helper and wrap any non-cancellation exception thrown by that call in `GgufUnsupportedChatTemplateException`; because the try block contains only template application, model loading and unrelated initialization failures cannot be relabeled. Make `GgufAdapterHost` map this exception to `G1FAIL chat-template-unsupported` and preserve the existing `G1FAIL model-load-failed` mapping for other initialization failures. Do not include exception text or paths in the frame. Do not add raw Granite control tokens.

- [ ] **Step 5: Make the host require exactly one terminal event**

Map deltas to `G1DELTA`; map completion to `G1DONE stop|length`; reject a delta after completion, duplicate completion, or end-of-stream without completion as protocol violations. This ensures the reason cannot be silently omitted.

- [ ] **Step 6: Run the native-adapter project and verify GREEN**

Re-run the exact `dotnet test` command from Step 2. Expected: all adapter tests pass, including exact frame strings, the safe unsupported-template mapping, and system-history ordering.

- [ ] **Step 7: Commit adapter wiring**

```powershell
git add runtime/GraniteEdgeAI.GgufRuntime.NativeAdapter tests/UnitTests/GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests
git commit -m "feat(runtime): emit stop and length completions"
```

### Task 4: Parse and propagate the reason through the worker

**Files:**
- Modify: `workers/GraniteEdgeAI.GgufRuntime.Worker/Session/GgufCliOutputParser.cs`
- Modify: `workers/GraniteEdgeAI.GgufRuntime.Worker/Session/GgufCliSession.cs`
- Modify: `workers/GraniteEdgeAI.GgufRuntime.Worker/Session/GgufSessionCoordinator.cs`
- Modify: `workers/GraniteEdgeAI.GgufRuntime.Worker/GgufWorkerHost.cs`
- Create: `infrastructure/GraniteEdgeAI.GgufRuntime.WorkerClient/GgufRuntimeStartupException.cs`
- Modify: `infrastructure/GraniteEdgeAI.GgufRuntime.WorkerClient/GgufRuntimeSession.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.GgufRuntime.Worker.Tests/GgufCliOutputParserTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.GgufRuntime.Worker.Tests/GgufSessionCoordinatorTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.GgufRuntime.WorkerClient.Tests/GgufRuntimeSessionTests.cs`
- Test: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufOutputBoundTests.cs`
- Test fixture: `tests/ProcessFixtures/GraniteEdgeAI.GgufRuntime.FakeCli/Program.cs`

- [ ] **Step 1: Write failing parser and coordinator tests**

```csharp
[TestMethod]
[DataRow("G1DONE stop", GgufCompletionReason.Stop)]
[DataRow("G1DONE length", GgufCompletionReason.Length)]
public void ParseCompletionRequiresExactReason(
    string frame,
    GgufCompletionReason expected)
{
    GgufCliOutput output = GgufCliOutputParser.Parse(
        frame, GgufCliOutputSource.StandardOutput);
    Assert.AreEqual(expected, output.CompletionReason);
}

[TestMethod]
[DataRow("G1DONE")]
[DataRow("G1DONE unknown")]
[DataRow("G1DONE stop extra")]
public void ParseCompletionRejectsMissingOrUnknownReason(string frame) =>
    Assert.ThrowsExactly<InvalidOperationException>(() =>
        GgufCliOutputParser.Parse(frame, GgufCliOutputSource.StandardOutput));
```

Add coordinator data rows proving both parsed values appear unchanged on `ResponseCompletedEvent.Reason`.

Add startup tests proving `G1FAIL chat-template-unsupported` becomes a safe `UnsupportedConfiguration` runtime failure with code `chat-template-unsupported`, while unknown failure codes are rejected as protocol violations. Add a worker-client test proving a loading event followed by `RuntimeFailureEvent` throws `GgufRuntimeStartupException` carrying that failure rather than the generic “did not become ready” transport exception.

- [ ] **Step 2: Run worker tests and verify RED**

```powershell
dotnet test .\tests\UnitTests\GraniteEdgeAI.GgufRuntime.Worker.Tests\GraniteEdgeAI.GgufRuntime.Worker.Tests.csproj -c Release
dotnet test .\tests\UnitTests\GraniteEdgeAI.GgufRuntime.WorkerClient.Tests\GraniteEdgeAI.GgufRuntime.WorkerClient.Tests.csproj -c Release
```

Expected: parser rejects the new valid frames or lacks `CompletionReason`.

- [ ] **Step 3: Add the reason and safe failure code to `GgufCliOutput`**

Add `GgufCompletionReason? CompletionReason` and `string? FailureCode` to the output record. Accept only `G1DONE stop`, `G1DONE length`, `G1FAIL chat-template-unsupported`, and the existing approved startup failure frames; remove support for bare `G1DONE`. `GgufCliSession.StartAsync` must parse the first adapter frame and throw an internal typed startup exception carrying only the allow-listed stable code when it is a failure.

- [ ] **Step 4: Propagate parsed reason from both worker paths**

Change `GgufSessionCoordinator.CompleteResponse` to accept the parsed reason. Change production `PumpGenerationAsync` to return `Task<GgufCompletionReason>` and make its caller construct `ResponseCompletedEvent(..., await generation)`. During startup, catch the internal typed CLI exception, write `RuntimeFailureEvent(UnsupportedConfiguration, "chat-template-unsupported")` after `SessionLoadingEvent`, clean up the CLI, and end cleanly without sending `SessionReadyEvent`. In `GgufRuntimeSession.StartAsync`, recognize that loading/failure sequence and throw a new public `GgufRuntimeStartupException` carrying the safe `GgufRuntimeFailure`; preserve the generic transport exception for malformed sequences. Preserve stop/cancel behavior; a user stop remains `ResponseStoppedEvent`, never `ResponseCompletedEvent`.

- [ ] **Step 5: Run worker unit and process integration tests and verify GREEN**

```powershell
dotnet test .\tests\UnitTests\GraniteEdgeAI.GgufRuntime.Worker.Tests\GraniteEdgeAI.GgufRuntime.Worker.Tests.csproj -c Release
dotnet test .\tests\UnitTests\GraniteEdgeAI.GgufRuntime.WorkerClient.Tests\GraniteEdgeAI.GgufRuntime.WorkerClient.Tests.csproj -c Release
dotnet test .\tests\IntegrationTests\GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests\GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests.csproj -c Release
```

Expected: all non-controlled tests pass and the worker preserves both reason values.

- [ ] **Step 6: Commit worker propagation**

```powershell
git add workers/GraniteEdgeAI.GgufRuntime.Worker infrastructure/GraniteEdgeAI.GgufRuntime.WorkerClient tests/UnitTests/GraniteEdgeAI.GgufRuntime.Worker.Tests tests/UnitTests/GraniteEdgeAI.GgufRuntime.WorkerClient.Tests tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests tests/ProcessFixtures/GraniteEdgeAI.GgufRuntime.FakeCli
git commit -m "feat(runtime): propagate adapter completion reason"
```

### Task 5: Add canonical hidden control turns and visible-history filtering

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/History/ChatCompletionStatus.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/History/ChatMessage.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/History/ChatConversation.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Clipboard/ChatTranscriptFormatter.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Services/GgufChatSessionAdapter.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/History/ChatHistoryStoreTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Clipboard/ChatTranscriptFormatterTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Services/GgufChatSessionAdapterTests.cs`

- [ ] **Step 1: Write exactly three failing persistence, formatting, and replay test methods**

Add exactly one new `[TestMethod]` to each named test class. Create a conversation containing User, Assistant `LimitReached`, and Control. Assert JSON save/load preserves all three, transcript formatting emits only the first two, and runtime replay maps Control to `GgufConversationRole.User`.

```csharp
ChatMessage control = ChatMessage.Control(
    GgufChatCoordinator.ContinuationInstruction,
    DateTimeOffset.UtcNow);
Assert.IsFalse(control.IsVisible);
StringAssert.DoesNotContain(ChatTranscriptFormatter.Format(messages),
    GgufChatCoordinator.ContinuationInstruction);
Assert.AreEqual(GgufConversationRole.User, replayed[^1].Role);
```

- [ ] **Step 2: Run the packaged app tests and verify RED**

Run:

```powershell
dotnet build .\tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj -c Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64 -p:PublishReadyToRun=false -p:AppxPackageSigningEnabled=false -p:GenerateAppxPackageOnBuild=false
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vstest = & $vswhere -latest -products * -find '**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' | Select-Object -First 1
& $vstest '.\tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe' /Platform:x64 '/TestCaseFilter:FullyQualifiedName~ChatHistoryStoreTests|FullyQualifiedName~ChatTranscriptFormatterTests|FullyQualifiedName~GgufChatSessionAdapterTests' /Logger:'console;verbosity=minimal'
```

Expected: compilation fails for `Control`, `LimitReached`, and `IsVisible`.

- [ ] **Step 3: Add the closed history states**

Add `LimitReached` to `ChatCompletionStatus` and `Control` to `ChatMessageRole`. Add:

```csharp
public bool IsVisible => Role is ChatMessageRole.User or ChatMessageRole.Assistant;

internal static ChatMessage Control(string content, DateTimeOffset createdUtc) =>
    new(Guid.NewGuid(), ChatMessageRole.Control, content,
        ChatCompletionStatus.Completed, createdUtc);
```

Keep conversation schema version 1 because enum values serialize numerically today and the new value is additive within the same application release. Validate that Control messages are nonempty and always Completed; reject invalid role/status combinations in the constructor.

- [ ] **Step 4: Exclude control turns from visible behavior and preserve runtime replay**

Filter `ChatTranscriptFormatter` with `message.IsVisible`. Ensure `ChatConversation.Append` derives titles only from `ChatMessageRole.User`. Update `GgufChatSessionAdapter.CreateInitialTurns` to map User and Control to runtime User, Assistant to runtime Assistant, while still excluding empty messages.

- [ ] **Step 5: Re-run the focused packaged tests and verify GREEN**

Re-run the exact build and filtered `vstest.console.exe` commands from Step 2. Expected: hidden controls round-trip but never appear in copied text.

- [ ] **Step 6: Commit canonical hidden history**

```powershell
git add 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime' tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime
git commit -m "feat(chat): persist hidden continuation turns"
```

### Task 6: Append continuation to the same assistant message

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Services/IGgufChatSession.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Services/GgufChatSessionAdapter.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Services/GgufChatCoordinator.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Services/DemoGgufChatSession.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Services/GgufChatSessionAdapterTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Services/GgufChatCoordinatorTests.cs`

- [ ] **Step 1: Write exactly seven failing domain-mapping and continuation test methods**

Map protocol completion into an internal closed kind:

```csharp
internal enum GgufChatCompletionKind { Stop, Length }
internal sealed record GgufChatCompleted(GgufChatCompletionKind Kind)
    : GgufChatEvent;
```

Use two separate adapter test methods for Stop and Length. Add five coordinator test methods that prove:

1. `Length` stores `LimitReached`.
2. `ContinueAsync` adds exactly one hidden control and appends deltas to the same assistant ID.
3. A second `Length` permits another continuation.
4. Failure before a delta retains the original text and failure after a delta persists the appended partial; exercise both subcases in this one method.
5. Calling Continue on a nonlatest, nonassistant, or nonlimited message throws before invoking the runtime; exercise all invalid targets in this one method.

- [ ] **Step 2: Run the service tests and verify RED**

Run:

```powershell
dotnet build .\tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj -c Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64 -p:PublishReadyToRun=false -p:AppxPackageSigningEnabled=false -p:GenerateAppxPackageOnBuild=false
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vstest = & $vswhere -latest -products * -find '**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' | Select-Object -First 1
& $vstest '.\tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe' /Platform:x64 '/TestCaseFilter:FullyQualifiedName~GgufChatSessionAdapterTests|FullyQualifiedName~GgufChatCoordinatorTests' /Logger:'console;verbosity=minimal'
```

Expected: old parameterless `GgufChatCompleted`, no `ContinueAsync`, and no limit status cause failures.

- [ ] **Step 3: Map public protocol reasons into the chat domain**

In `GgufChatSessionAdapter`, map `GgufCompletionReason.Stop` to `GgufChatCompletionKind.Stop` and `Length` to `Length`; reject unknown values. Update demo generation to emit `Stop`.

- [ ] **Step 4: Refactor coordinator generation into one shared append loop**

Extract a private method that accepts the current conversation, target assistant, prompt sent to the runtime, and whether to append a visible user message. Keep `SendAsync` behavior unchanged. Add:

```csharp
internal const string ContinuationInstruction =
    "Continue from exactly where the preceding response ended. " +
    "Do not repeat text already given. Complete the answer concisely.";

internal Task ContinueAsync(Guid assistantMessageId,
    CancellationToken cancellationToken)
```

Under `stateSync`, require the target to be the last visible message, Assistant, and `LimitReached`; set `isGenerating`; append one Control message; reuse the target assistant with `Pending`; append each delta to its existing content. Map Stop to Completed and Length to LimitReached. Preserve the established partial-save logic in `finally`.

- [ ] **Step 5: Run coordinator and adapter tests and verify GREEN**

Re-run the exact build and filtered `vstest.console.exe` commands from Step 2. Expected: continuation keeps one visible assistant bubble and canonical hidden history.

- [ ] **Step 6: Commit continuation orchestration**

```powershell
git add 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Services' tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Services
git commit -m "feat(chat): continue length-limited responses"
```

### Task 7: Render an accessible Continue generating action

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatMessageBubble.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatMessageBubble.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatDemoController.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatPageTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatAccessibilityTests.cs`

- [ ] **Step 1: Write exactly four failing bubble and page test methods**

Add four test methods: (1) collapsed for Completed, (2) visible and keyboard-focusable only for the latest `LimitReached` assistant, (3) raises exactly one message ID, and (4) the streaming-copy regression below:

```csharp
[TestMethod]
public void CopyingStreamingPartialDoesNotRaiseStopOrContinuation()
{
    var clipboard = new RecordingClipboard();
    var page = new ChatPage(clipboard);
    int stops = 0;
    int continuations = 0;
    page.StopRequested += (_, _) => stops++;
    page.ContinuationRequested += (_, _) => continuations++;
    page.SynchronizeTranscript(id, streamingMessages, forceFollowLatest: true);

    FindCopyButton(page).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    Assert.AreEqual(0, stops);
    Assert.AreEqual(0, continuations);
    Assert.AreEqual(partialText, clipboard.Text);
}
```

- [ ] **Step 2: Run the focused packaged UI tests and verify RED**

Run:

```powershell
dotnet build .\tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj -c Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64 -p:PublishReadyToRun=false -p:AppxPackageSigningEnabled=false -p:GenerateAppxPackageOnBuild=false
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vstest = & $vswhere -latest -products * -find '**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' | Select-Object -First 1
& $vstest '.\tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe' /Platform:x64 '/TestCaseFilter:FullyQualifiedName~ChatPageTests|FullyQualifiedName~ChatAccessibilityTests' /Logger:'console;verbosity=minimal'
```

Expected: no continuation property, button, event, or routing exists.

- [ ] **Step 3: Add bubble state and action**

Add `MessageId` and `CanContinue` properties, plus `event EventHandler? ContinueRequested`. In XAML place a subtle text-style button alongside status/copy actions:

```xml
<Button
    x:Name="ContinueButton"
    Content="Continue generating"
    Visibility="Collapsed"
    AutomationProperties.Name="Continue generating"
    Click="ContinueButton_Click" />
```

The code-behind sets visibility from `CanContinue`, keeps it available to keyboard focus, and does not share a click handler with Copy or Stop.

- [ ] **Step 4: Filter controls from transcript rendering and route the eligible ID**

In `ChatPage.SynchronizeTranscript`, create a visible-message array before reset/update logic. Set each bubble's `MessageId`; set `CanContinue` only when the message is the last visible assistant and its status is `LimitReached`. Map status to **Response limit reached**. Add `event EventHandler<Guid>? ContinuationRequested` and raise it from the bubble event.

- [ ] **Step 5: Connect the controller**

Subscribe/unsubscribe `page.ContinuationRequested`. The async handler gates disposed/generating state, sets `followLatest`, calls `page.SetGenerating(true)`, awaits `coordinator.ContinueAsync(id, CancellationToken.None)`, and always restores generating state and requests a final render. Copy handlers remain synchronous UI-only operations and never reach the coordinator.

- [ ] **Step 6: Run the focused packaged UI tests and verify GREEN**

Re-run the exact build and filtered `vstest.console.exe` commands from Step 2. Expected: visibility, keyboard focus, one-shot routing, same-bubble streaming, and copy independence all pass.

- [ ] **Step 7: Commit the continuation UI**

```powershell
git add 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime' tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime
git commit -m "feat(chat): add continue generating action"
```

### Task 8: Prove the controlled runtime and update the exact hosted inventory

**Files:**
- Modify: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufRealModelSmokeTests.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufOutputBoundTests.cs`
- Modify: `scripts/model-inspection/Invoke-ModelInspectionProgressPolishGate.ps1`
- Modify generated package evidence only through existing build targets: `IBM Granite with TurboQuant (Intel)/obj/gguf-runtime-manifest/*` (never commit `obj` output)

- [ ] **Step 1: Add a controlled tiny-budget continuation assertion**

In the real-model smoke test, clone the controlled configuration with `MaximumGeneratedTokens = 8`, generate a prompt that cannot finish in eight tokens, and assert:

```csharp
ResponseCompletedEvent firstCompletion =
    first.OfType<ResponseCompletedEvent>().Single();
Assert.AreEqual(GgufCompletionReason.Length, firstCompletion.Reason);
Assert.IsTrue(first.OfType<TextDeltaEvent>().Any());
```

Then submit the fixed continuation instruction and assert another nonempty delta stream with a typed terminal event. Do not attempt to infer token counts by retokenizing concatenated text in this process test; the transform unit tests are the authoritative proof that the private probe is suppressed. Keep this test opt-in under `GRANITE_GGUF_RUNTIME_TEST_CONFIG`.

- [ ] **Step 2: Run the integration test without configuration and verify the expected skip**

Run the GGUF verification script. Expected: all deterministic tests pass and the controlled real-model case is the sole environment-dependent skip when the variable is absent.

- [ ] **Step 3: Run with the approved controlled model configuration**

```powershell
if ([string]::IsNullOrWhiteSpace($env:GRANITE_GGUF_RUNTIME_TEST_CONFIG)) {
    throw 'Set GRANITE_GGUF_RUNTIME_TEST_CONFIG to the approved controlled configuration JSON before this step.'
}
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\gguf-runtime\Invoke-GgufChatVerification.ps1
Remove-Item Env:\GRANITE_GGUF_RUNTIME_TEST_CONFIG
```

Expected: the real model emits `Length` with the tiny visible budget and produces continuation deltas. The transform unit test proves that the private probe is not emitted. The model and manifest SHA-256 checks remain unchanged before and after execution.

- [ ] **Step 4: Update the exact hosted test inventory**

The plan adds exactly 14 packaged application test cases: three in Task 5, seven in Task 6, and four in Task 7. Change only `$ExpectedTotals.HostedRelease` from `844` to `858`. `FocusedPolish` remains `326` because its filter contains only model-inspection classes. Do not change class maps or weaken exact-map assertions.

- [ ] **Step 5: Run complete verification**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\gguf-runtime\Invoke-GgufChatVerification.ps1
dotnet test .\tests\ContractTests\GraniteEdgeAI.ModelInspection.Contracts.Tests\GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj -c Release --minimum-expected-tests 357
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\model-inspection\Invoke-ModelInspectionProgressPolishGate.ps1
```

Build the Release WinUI package and run the full hosted filter:

```powershell
dotnet build .\tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj -c Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64 -p:PublishReadyToRun=false -p:AppxPackageSigningEnabled=false -p:GenerateAppxPackageOnBuild=false
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vstest = & $vswhere -latest -products * -find '**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' | Select-Object -First 1
& $vstest '.\tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe' /Platform:x64 '/TestCaseFilter:TestCategory!=ModelInspectionVisualRegression&TestCategory!=ModelInspectionControlledOs' /Logger:'console;verbosity=minimal'
```

Expected: zero failures, zero unexpected skips, a warning-free Release application build, and no committed build output.

- [ ] **Step 6: Inspect the final diff and commit verification updates**

```powershell
git diff --check
git status --short
git add tests scripts .github 'IBM Granite with TurboQuant (Intel)'
git commit -m "test(chat): verify length continuation end to end"
```

### Task 9: Final review, push, and hosted checks

**Files:**
- Review only: all files changed by Tasks 1-8

- [ ] **Step 1: Review against every acceptance criterion**

Confirm explicitly that token 513 is hidden, `Length` is typed end-to-end, Continue preserves one visible assistant ID, Control survives reload but is not displayed/copied, Copy emits no coordinator action, and the embedded chat template handles the system role.

- [ ] **Step 2: Run repository hygiene checks**

```powershell
git diff --check
git status --short
git log --oneline -10
```

Expected: clean worktree, no whitespace errors, no `bin`, `obj`, model, or private history files staged.

- [ ] **Step 3: Push the existing feature branch**

```powershell
git push origin feature/gguf-cli-chat-production
```

- [ ] **Step 4: Monitor PR #103**

```powershell
gh pr checks 103 --watch --interval 10
```

Expected: build/test, worker-process, traceability, and Release-isolation checks pass. If GitHub again reports that an Actions budget prevented the jobs from starting, record that as an external account blocker; do not describe it as a code/test failure.
