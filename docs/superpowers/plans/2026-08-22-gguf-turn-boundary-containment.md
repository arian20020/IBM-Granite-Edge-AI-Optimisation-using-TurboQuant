# GGUF Turn-Boundary Containment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep local Granite replies inside one clean assistant turn by removing a leading `Me:`, stopping fabricated role continuations and empty-fence loops, and using deterministic generation.

**Architecture:** Add one LLamaSharp `ITextStreamTransform` that incrementally buffers a small unsafe suffix while streaming confirmed-safe text. Attach it inside `ChatSession` so transformed text is both emitted and stored, then configure greedy sampling plus common anti-prompts for early executor termination.

**Tech Stack:** C# 12, .NET 8, LLamaSharp 0.27.0, MSTest 4.3.2, WinUI 3, PowerShell.

---

## File structure

- Create `runtime/GraniteEdgeAI.GgufRuntime.NativeAdapter/GraniteTurnBoundaryTextTransform.cs` for streaming prefix normalization and turn-boundary detection.
- Create `tests/UnitTests/GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests/GraniteTurnBoundaryTextTransformTests.cs` for deterministic fragmented-stream coverage.
- Modify `runtime/GraniteEdgeAI.GgufRuntime.NativeAdapter/LlamaSharpInferenceEngine.cs` to attach the transform and use the approved inference policy.
- Modify `tests/UnitTests/GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests/LlamaSharpInferenceEngineTests.cs` to bind greedy sampling and anti-prompts.
- Modify `tests/UnitTests/GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests/LlamaSharpRealModelSmokeTests.cs` to cover the user's `helllo` case at the production token bound.
- Modify `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufRealModelSmokeTests.cs` to apply the same response-boundary assertion to both packaged turns.

### Task 1: Streaming turn-boundary transform

**Files:**
- Create: `tests/UnitTests/GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests/GraniteTurnBoundaryTextTransformTests.cs`
- Create: `runtime/GraniteEdgeAI.GgufRuntime.NativeAdapter/GraniteTurnBoundaryTextTransform.cs`

- [ ] **Step 1: Write failing fragmented-stream tests**

Create a test helper that converts arbitrary chunks into an async stream and collects `TransformAsync`. Add these tests:

```csharp
using GraniteEdgeAI.GgufRuntime.NativeAdapter;

namespace GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests;

[TestClass]
public sealed class GraniteTurnBoundaryTextTransformTests
{
    [DataTestMethod]
    [DataRow("Me: Hello", "Hello")]
    [DataRow(" me: Hello", "Hello")]
    [DataRow("ME: Hello", "Hello")]
    public async Task LeadingMeLabelIsRemoved(string input, string expected) =>
        Assert.AreEqual(expected, await TransformAsync(input));

    [TestMethod]
    public async Task ChunkSplitLeadingLabelIsRemoved() =>
        Assert.AreEqual("Hello", await TransformAsync("M", "e", ": ", "Hello"));

    [TestMethod]
    public async Task OrdinaryMeTextIsPreserved() =>
        Assert.AreEqual("Tell me: why", await TransformAsync("Tell me: why"));

    [DataTestMethod]
    [DataRow("Answer\nUser: fabricated")]
    [DataRow("Answer\r\nassistant: fabricated")]
    [DataRow("Answer\nMe: fabricated")]
    public async Task FabricatedNextTurnIsDiscarded(string input) =>
        Assert.AreEqual("Answer", (await TransformAsync(input)).TrimEnd());

    [TestMethod]
    public async Task SavedEmptyFenceLoopStopsAfterUsefulAnswer() =>
        Assert.AreEqual(
            "Hello! How can I assist you today?",
            (await TransformAsync(
                "Me: Hello! How can I assist you today?\n```",
                "\n```\n```\n```")).TrimEnd());

    [TestMethod]
    public async Task LegitimateFencedCodeIsPreserved()
    {
        const string code = "Example:\n```csharp\nUser: value = input;\n```\nDone.";
        Assert.AreEqual(code, await TransformAsync(code));
    }

    [TestMethod]
    public async Task CloneStartsWithIndependentState()
    {
        var original = new GraniteTurnBoundaryTextTransform();
        var clone = original.Clone();
        Assert.AreNotSame(original, clone);
        Assert.AreEqual("Hello", await CollectAsync(
            clone.TransformAsync(Chunks("Me: Hello"))));
    }

    private static Task<string> TransformAsync(params string[] chunks) =>
        CollectAsync(new GraniteTurnBoundaryTextTransform().TransformAsync(Chunks(chunks)));

    private static async Task<string> CollectAsync(IAsyncEnumerable<string> chunks)
    {
        var result = new System.Text.StringBuilder();
        await foreach (string chunk in chunks)
        {
            result.Append(chunk);
        }
        return result.ToString();
    }

    private static async IAsyncEnumerable<string> Chunks(params string[] chunks)
    {
        foreach (string chunk in chunks)
        {
            yield return chunk;
            await Task.Yield();
        }
    }
}
```

- [ ] **Step 2: Run the native-adapter test project and verify RED**

Run:

```powershell
dotnet run --project tests\UnitTests\GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests\GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests.csproj -c Release -- --progress off
```

Expected: build failure because `GraniteTurnBoundaryTextTransform` does not exist.

- [ ] **Step 3: Implement the minimal streaming transform**

Create a sealed internal type implementing `LLama.Abstractions.ITextStreamTransform`. Its `TransformAsync` must:

```csharp
public async IAsyncEnumerable<string> TransformAsync(
    IAsyncEnumerable<string> tokens)
{
    var text = new StringBuilder();
    int emitted = 0;
    bool prefixResolved = false;

    await foreach (string token in tokens.ConfigureAwait(false))
    {
        text.Append(token);
        prefixResolved = ResolveLeadingMe(text, prefixResolved, isFinal: false);
        if (!prefixResolved)
        {
            continue;
        }

        int boundary = FindBoundary(text);
        int safeEnd = boundary >= 0
            ? boundary
            : FindSafeStreamingEnd(text, emitted);
        if (safeEnd > emitted)
        {
            yield return text.ToString(emitted, safeEnd - emitted);
            emitted = safeEnd;
        }
        if (boundary >= 0)
        {
            yield break;
        }
    }

    ResolveLeadingMe(text, prefixResolved, isFinal: true);
    int finalBoundary = FindBoundary(text);
    int finalEnd = finalBoundary >= 0 ? finalBoundary : text.Length;
    if (finalEnd > emitted)
    {
        yield return text.ToString(emitted, finalEnd - emitted);
    }
}

public ITextStreamTransform Clone() => new GraniteTurnBoundaryTextTransform();
```

Implement helpers with these exact policies:

- `ResolveLeadingMe` waits while the buffered prefix could still match `^\s*me\s*:`; on a full match, remove the match and at most one following ASCII space.
- `FindBoundary` scans logical lines while tracking triple-backtick fenced-code state. Outside a fenced block, return the start of a line matching `^\s*(me|user|assistant)\s*:`. Do not treat offset zero as a subsequent-role boundary after prefix resolution.
- Treat a line containing only optional horizontal whitespace plus `` ``` `` as an empty-fence candidate. If the next non-empty logical line is another empty fence, return the first candidate's offset. A fence with a language suffix or non-whitespace content between fences clears the candidate.
- `FindSafeStreamingEnd` retains from the earliest unresolved empty-fence candidate; otherwise retain the last 32 UTF-16 code units so labels split across chunks remain detectable.
- All offsets are calculated against the same `StringBuilder`; never remove content after any text has been emitted.

- [ ] **Step 4: Run the native-adapter tests and verify GREEN**

Run the Step 2 command.

Expected: all deterministic tests pass; controlled-model tests are skipped when the environment variable is absent.

- [ ] **Step 5: Commit the transform**

```powershell
git add -- runtime/GraniteEdgeAI.GgufRuntime.NativeAdapter/GraniteTurnBoundaryTextTransform.cs tests/UnitTests/GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests/GraniteTurnBoundaryTextTransformTests.cs
git commit -m "fix(runtime): contain malformed assistant turns"
```

### Task 2: Deterministic inference policy and session integration

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests/LlamaSharpInferenceEngineTests.cs`
- Modify: `runtime/GraniteEdgeAI.GgufRuntime.NativeAdapter/LlamaSharpInferenceEngine.cs`

- [ ] **Step 1: Write the failing inference-policy test**

Add:

```csharp
[TestMethod]
public void CreateInferenceParametersUsesGreedyTurnBoundaries()
{
    InferenceParams inference = LlamaSharpInferenceEngine.CreateInferenceParameters(
        new GgufAdapterOptions(
            "C:\\Models\\granite.gguf", 2048,
            GgufAdapterCacheType.F16, GgufAdapterCacheType.F16,
            0, 4, 256, false, 512));

    Assert.AreEqual(512, inference.MaxTokens);
    Assert.IsInstanceOfType<GreedySamplingPipeline>(inference.SamplingPipeline);
    CollectionAssert.Contains(inference.AntiPrompts.ToList(), "\nUser:");
    CollectionAssert.Contains(inference.AntiPrompts.ToList(), "\nuser:");
    CollectionAssert.Contains(inference.AntiPrompts.ToList(), "\nAssistant:");
    CollectionAssert.Contains(inference.AntiPrompts.ToList(), "\nassistant:");
    CollectionAssert.Contains(inference.AntiPrompts.ToList(), "\nMe:");
    CollectionAssert.Contains(inference.AntiPrompts.ToList(), "\nme:");
}
```

Add `using LLama.Sampling;` if absent.

- [ ] **Step 2: Run the native-adapter tests and verify RED**

Run the Task 1 Step 2 command.

Expected: build failure because `CreateInferenceParameters` is not defined.

- [ ] **Step 3: Attach the transform and inference policy**

Change session creation to:

```csharp
_session = new ChatSession(executor, CreateHistory(initialHistory))
    .WithHistoryTransform(new PromptTemplateTransformer(
        _weights,
        withAssistant: true))
    .WithOutputTransform(new GraniteTurnBoundaryTextTransform());
```

Replace inline inference construction with:

```csharp
InferenceParams inference = CreateInferenceParameters(options);
```

Add:

```csharp
internal static InferenceParams CreateInferenceParameters(GgufAdapterOptions configuration) =>
    new()
    {
        MaxTokens = Math.Min(
            configuration.MaximumGeneratedTokens,
            checked((int)configuration.ContextSize / 2)),
        SamplingPipeline = new GreedySamplingPipeline(),
        AntiPrompts =
        [
            "\nUser:", "\nuser:",
            "\nAssistant:", "\nassistant:",
            "\nMe:", "\nme:",
            "\n```\n```", "\r\n```\r\n```",
            "\n```\n\n```", "\r\n```\r\n\r\n```",
        ],
    };
```

- [ ] **Step 4: Run the native-adapter tests and verify GREEN**

Run the Task 1 Step 2 command.

Expected: all deterministic tests pass.

- [ ] **Step 5: Commit the policy integration**

```powershell
git add -- runtime/GraniteEdgeAI.GgufRuntime.NativeAdapter/LlamaSharpInferenceEngine.cs tests/UnitTests/GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests/LlamaSharpInferenceEngineTests.cs
git commit -m "fix(runtime): enforce deterministic chat boundaries"
```

### Task 3: Controlled model and packaged-process regression

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests/LlamaSharpRealModelSmokeTests.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufRealModelSmokeTests.cs`

- [ ] **Step 1: Add the production-shape controlled model test**

Add `ControlledGraniteHelloOmitsSpeakerLabelAndFenceLoop`, using the controlled model, the production 512-token limit, and prompt `helllo`. Collect all chunks and assert:

```csharp
Assert.IsFalse(string.IsNullOrWhiteSpace(answer));
Assert.IsFalse(Regex.IsMatch(answer, @"(?i)^\s*me\s*:"), answer);
Assert.IsFalse(Regex.IsMatch(
    answer,
    @"(?im)^\s*(?:user|assistant|me)\s*:",
    RegexOptions.None,
    TimeSpan.FromSeconds(1)), answer);
Assert.IsFalse(Regex.IsMatch(
    answer,
    @"(?m)^\s*```\s*$\r?\n(?:\s*\r?\n)*^\s*```\s*$",
    RegexOptions.None,
    TimeSpan.FromSeconds(1)), answer);
```

Use the same `GgufAdapterOptions` construction as the existing real-model tests, with `MaximumGeneratedTokens: 512`.

- [ ] **Step 2: Strengthen the packaged helper**

In `AssertSingleAssistantTurn`, reject a leading `Me:` and the generalized empty-fence pair using the same bounded regexes. Keep the existing non-empty assertion. Both first and second packaged responses already call this helper.

- [ ] **Step 3: Run the controlled native adapter suite**

```powershell
$env:GRANITE_GGUF_ADAPTER_TEST_MODEL='C:\Users\Arian\Downloads\granite-4.1-3b-Q4_K_M.gguf'
dotnet run --project tests\UnitTests\GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests\GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests.csproj -c Release -- --progress off
```

Expected: all native-adapter tests pass with zero skips and no malformed response assertion.

- [ ] **Step 4: Refresh the ignored controlled manifest checksum after building**

Calculate the SHA-256 of:

```text
IBM Granite with TurboQuant (Intel)\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\GgufRuntime\runtime-manifest.json
```

Update only `manifestSha256` in the ignored file:

```text
.controlled\gguf-runtime\granite-4.1-3b-Q4_K_M.runtime.json
```

Do not stage the controlled configuration.

- [ ] **Step 5: Run the controlled packaged-process suite**

```powershell
$env:GRANITE_GGUF_RUNTIME_TEST_CONFIG='C:\g1-chat\.controlled\gguf-runtime\granite-4.1-3b-Q4_K_M.runtime.json'
dotnet run --project tests\IntegrationTests\GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests\GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests.csproj -c Release -- --progress off
```

Expected: 21 tests pass, zero fail, zero skip.

- [ ] **Step 6: Commit the controlled regressions**

```powershell
git add -- tests/UnitTests/GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests/LlamaSharpRealModelSmokeTests.cs tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufRealModelSmokeTests.cs
git commit -m "test(runtime): cover malformed Granite turn loops"
```

### Task 4: Full verification and PR update

**Files:**
- Verify only; no source changes expected.

- [ ] **Step 1: Close the running preview app**

Ask the user to close it when present, or stop only the exact process whose path resolves beneath `C:\g1-chat\IBM Granite with TurboQuant (Intel)\bin`. This prevents the known apphost copy lock.

- [ ] **Step 2: Run the complete controlled GGUF verification**

```powershell
$env:GRANITE_GGUF_ADAPTER_TEST_MODEL='C:\Users\Arian\Downloads\granite-4.1-3b-Q4_K_M.gguf'
$env:GRANITE_GGUF_RUNTIME_TEST_CONFIG='C:\g1-chat\.controlled\gguf-runtime\granite-4.1-3b-Q4_K_M.runtime.json'
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\gguf-runtime\Invoke-GgufChatVerification.ps1
```

Expected: every suite passes, the real-model tests have zero skips, and the self-contained Release/x64 build has zero errors.

- [ ] **Step 3: Run all packaged GGUF chat tests**

```powershell
$configuration='Release'
$testProject='tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj'
dotnet build $testProject -c $configuration -p:Platform=x64 -p:RuntimeIdentifier=win-x64 --nologo
if ($LASTEXITCODE -ne 0) { throw 'Packaged test build failed.' }
$recipe=(Resolve-Path "tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\$configuration\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe").Path
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vstest=& $vswhere -latest -products * -find '**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' | Select-Object -First 1
& $vstest $recipe '/Platform:x64' '/Logger:Console;Verbosity=minimal' '/TestCaseFilter:FullyQualifiedName~GraniteEdgeAI.UnitTests.Features.GgufRuntime'
if ($LASTEXITCODE -ne 0) { throw 'Packaged test run failed.' }
```

Expected: all packaged `Features.GgufRuntime` tests pass.

- [ ] **Step 4: Check repository integrity**

```powershell
git diff --check
git status --short
```

Expected: no unstaged or untracked files after all commits.

- [ ] **Step 5: Push the branch and update PR 103**

```powershell
git push origin feature/gguf-cli-chat-production
gh pr view 103 --json url,state,mergeable,statusCheckRollup
```

Expected: push succeeds and PR 103 remains open against `feature/gguf-cli-chat-runtime`.
