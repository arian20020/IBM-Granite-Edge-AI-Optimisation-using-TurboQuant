# Inspected Model Production Chat Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Connect a successfully inspected local GGUF model to the real packaged CPU runtime and existing chat UI instead of the deterministic preview controller.

**Architecture:** A focused factory converts the immutable inspection request and its matching terminal evidence into a trusted `GgufChatLaunchRequest`. Model Inspection exposes an active **Open in Chat** command only for Ready outcomes; the onboarding shell forwards the resulting launch request to `MainWindow`, which uses the existing `OpenProductionChatAsync` boundary.

**Tech Stack:** C# 13, .NET 8, WinUI 3, MSTest packaged UI tests, LLamaSharp 0.27.0, existing GGUF worker/adapter protocol.

---

## File map

- Create `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/GgufInspectedModelLaunchFactory.cs`: validate completed inspection evidence, load the packaged manifest snapshot, and construct the conservative CPU launch configuration.
- Create `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionChatRequestedEventArgs.cs`: carry the exact request and completed result out of the inspection page.
- Modify `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionPresentationCommands.cs`: add the Open Chat command.
- Modify `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionPresentationFactory.cs`: render **Open in Chat** as the active primary Ready action.
- Modify `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs`: raise the request only for the current Ready terminal result.
- Modify `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs`: build and forward a production launch request.
- Modify `IBM Granite with TurboQuant (Intel)/MainWindow.xaml.cs`: receive the request and invoke the existing trusted production controller.
- Modify focused tests under `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime`, `Features/ModelInspection`, and `Features/Onboarding`.

### Task 1: Trusted inspected-model launch factory

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/GgufInspectedModelLaunchFactory.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/GgufInspectedModelLaunchFactoryTests.cs`

- [ ] **Step 1: Write the failing factory tests**

Add tests that create a real temporary package directory and manifest plus a completed Ready inspection result, then assert:

```csharp
GgufChatLaunchRequest launch = GgufInspectedModelLaunchFactory.Create(
    request,
    completed,
    packageRoot,
    processorCount: 12);

Assert.AreEqual(request.ModelPath, launch.ModelFile);
Assert.AreEqual(result.Evidence.File.ModelSha256,
    launch.Configuration.ModelSha256);
Assert.AreEqual("llamasharp-test-cpu", launch.Configuration.RuntimeBuildId);
Assert.AreEqual(GgufRuntimeBackend.Cpu, launch.Configuration.Backend);
Assert.AreEqual(0, launch.Configuration.GpuLayerCount);
Assert.AreEqual(GgufCacheType.F16, launch.Configuration.KeyCacheType);
Assert.AreEqual(8, launch.Configuration.ThreadCount);
CollectionAssert.AreEqual(manifestBytes, launch.TrustedManifest.ToArray());
```

Add rejection cases for non-completed/non-Ready results, mismatched evidence filename/length/timestamp, missing package members, and malformed manifest. Assert fixed exception codes and ensure no exception message contains `request.ModelPath`.

- [ ] **Step 2: Run the tests and verify RED**

Run:

```powershell
dotnet test tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj -c Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64 --filter FullyQualifiedName~GgufInspectedModelLaunchFactoryTests
```

Expected: compilation failure because `GgufInspectedModelLaunchFactory` does not exist.

- [ ] **Step 3: Implement the minimal launch factory**

Implement this public shape:

```csharp
internal static class GgufInspectedModelLaunchFactory
{
    internal static GgufChatLaunchRequest Create(
        ModelInspectionRequest request,
        ModelInspectionExecutionResult execution,
        string packageRoot,
        int? processorCount = null);
}
```

The method must require `Completed`, require outcome `Ready` or
`ReadyWithWarnings`, compare evidence filename/length/last-write identity with
the immutable request, read `runtime-manifest.json` once with the existing
maximum-size bound, deserialize it, and build:

```csharp
new GgufRuntimeConfiguration(
    modelId: $"inspected-{file.ModelSha256[..12].ToLowerInvariant()}",
    modelSha256: file.ModelSha256,
    runtimeBuildId: manifest.RuntimeBuildId,
    runtimeSourceCommit: manifest.RuntimeSourceCommit,
    backend: GgufRuntimeBackend.Cpu,
    deviceId: "cpu",
    contextSize: BoundedContext(request.QuickScan.DeclaredContextLength),
    keyCacheType: GgufCacheType.F16,
    valueCacheType: GgufCacheType.F16,
    gpuLayerCount: 0,
    flashAttention: false,
    threadCount: Math.Clamp(processorCount ?? Environment.ProcessorCount, 1, 8),
    batchSize: 128,
    evidenceGrade: "model-inspection-complete",
    profileId: "cpu-inspected-default",
    maximumGeneratedTokens: 512);
```

Use a 4096-token runtime ceiling, fall back to 2048 when the model declares no
context, and never include an absolute path in an exception message.

- [ ] **Step 4: Run the focused tests and verify GREEN**

Run the command from Step 2. Expected: every factory test passes.

- [ ] **Step 5: Commit the factory slice**

```powershell
git add "IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/GgufInspectedModelLaunchFactory.cs" tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/GgufInspectedModelLaunchFactoryTests.cs
git commit -m "feat(chat): build trusted launch from inspection"
```

### Task 2: Activate Open in Chat for Ready inspection

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionChatRequestedEventArgs.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionPresentationCommands.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionPresentationFactory.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/ModelInspectionPresentationFactoryTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageTests.cs`

- [ ] **Step 1: Write failing presentation and page tests**

For Ready and ReadyWithWarnings, assert the primary action is active:

```csharp
Assert.AreEqual("open-chat", presentation.Actions.PrimaryAction.ActionId);
Assert.AreEqual("Open in Chat", presentation.Actions.PrimaryAction.Text);
Assert.IsTrue(presentation.Actions.PrimaryAction.IsEnabled);
Assert.AreSame(commands.OpenChat,
    presentation.Actions.PrimaryAction.Command);
```

For all blocking outcomes, assert no Open Chat action is exposed. Add a page
test that executes the rendered command after a Ready result and asserts one
event containing the same `ModelInspectionRequest` and `ModelInspectionResult`.
Execute the command again after replacement navigation and assert no stale
event.

- [ ] **Step 2: Run the focused tests and verify RED**

Run the packaged test recipe with:

```powershell
dotnet build tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj -c Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64 -p:PublishReadyToRun=false -p:AppxPackageSigningEnabled=false -p:GenerateAppxPackageOnBuild=false
```

Then run VSTest against the generated `.build.appxrecipe` filtered to
`ModelInspectionPresentationFactoryTests|ModelInspectionPageTests`. Expected:
the Ready primary-action assertions fail because the action is still the future
hardware action.

- [ ] **Step 3: Implement the Open Chat command and event**

Extend `ModelInspectionPresentationCommands` with a non-null `OpenChat` command.
Replace the two Ready primary future actions with:

```csharp
CreateActiveAction(
    "open-chat",
    "Open in Chat",
    "Open inspected model in chat",
    commands.OpenChat)
```

Create event args with immutable `Request` and `Result` properties. In
`ModelInspectionPage`, bind `OpenChat` to a method that revalidates the current
snapshot is completed and Ready, verifies it belongs to `Request`, and raises
`ProductionChatRequested` exactly once per activation. Clear the event during
page lifetime retirement.

- [ ] **Step 4: Run focused packaged tests and verify GREEN**

Rebuild and rerun the same filter. Expected: all focused presentation/page tests
pass.

- [ ] **Step 5: Commit the inspection action slice**

```powershell
git add "IBM Granite with TurboQuant (Intel)/Features/ModelInspection" tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection
git commit -m "feat(inspection): open ready model in chat"
```

### Task 3: Forward production launch through onboarding

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/MainWindow.xaml.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingEntryPointTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingShellPageTests.cs`

- [ ] **Step 1: Write failing route tests**

Inject a launch factory delegate into `OnboardingShellPage` tests, attach a
Ready inspection page, invoke Open Chat, and assert the shell raises exactly one
`ProductionChatRequested` with the factory's exact `GgufChatLaunchRequest`.
Add a MainWindow seam test that asserts a forwarded request enters the
production creation path and does not create the preview controller.

- [ ] **Step 2: Run the route tests and verify RED**

Build and run the packaged recipe filtered to
`OnboardingShellPageTests|OnboardingEntryPointTests`. Expected: the event and
factory injection members do not exist.

- [ ] **Step 3: Implement forwarding and fixed failure behavior**

Subscribe to `ModelInspectionPage.ProductionChatRequested` in
`AttachModelInspectionPage` and unsubscribe in `DetachModelInspectionPage`.
Use the injected/default factory to construct the launch request from:

```csharp
Path.Combine(AppContext.BaseDirectory, "GgufRuntime")
```

Raise a shell `ProductionChatRequested` event carrying the exact launch request.
In `MainWindow.ShowOnboarding`, subscribe to it. The handler must unsubscribe
the source shell, call `OpenProductionChatAsync`, and catch only the expected
trust/file/config/runtime launch exceptions after `OpenProductionChatAsync` has
already restored onboarding. It must never call the preview handler.

- [ ] **Step 4: Run the route tests and verify GREEN**

Rebuild and rerun the route filter. Expected: all route tests pass and the
preview-constructor assertion remains false.

- [ ] **Step 5: Commit the route slice**

```powershell
git add "IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs" "IBM Granite with TurboQuant (Intel)/MainWindow.xaml.cs" tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding
git commit -m "feat(onboarding): launch inspected model chat"
```

### Task 4: End-to-end verification and documentation

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/README.md`
- Modify: `docs/handoffs/2026-08-20-g1-gguf-chat-production.md`

- [ ] **Step 1: Run the complete packaged WinUI chat/onboarding/inspection slice**

Build the Release/x64 packaged test project and run the generated recipe with:

```text
FullyQualifiedName~Features.GgufRuntime|
FullyQualifiedName~Features.Onboarding|
FullyQualifiedName~Features.ModelInspection
```

Expected: zero failures, with the new route tests included.

- [ ] **Step 2: Run the complete GGUF runtime verification**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\gguf-runtime\Invoke-GgufChatVerification.ps1
```

Expected: all non-controlled tests pass, controlled tests skip without external
configuration, and the application build has zero errors.

- [ ] **Step 3: Run the controlled Granite model through the production route**

Use the existing controlled configuration with the freshly built package and
`C:\Users\Arian\Downloads\granite-4.1-3b-Q4_K_M.gguf`. Run the real-model smoke
and a packaged WinUI route probe. Expected: a non-empty streamed model response,
Stop/reload, Close, and unchanged model SHA-256.

- [ ] **Step 4: Update truthful user documentation**

Document that users import and inspect a compatible GGUF, select **Open in
Chat**, then prompt the real local model. Keep Preview Chat labelled as a
deterministic UI demonstration. Document the CPU/F16 baseline and the explicit
TurboQuant/TurboVec boundaries.

- [ ] **Step 5: Review, verify, and commit**

Run `git diff --check`, inspect every changed file, confirm no controlled local
configuration remains, then commit:

```powershell
git add --all
git commit -m "fix(chat): connect inspected model to local inference"
```
