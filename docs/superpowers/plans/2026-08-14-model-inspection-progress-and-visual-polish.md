# Model Inspection Progress and Visual Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Model Inspection respond visibly on its first active frame, report five truthful production stages with deliberate 550 ms visual pacing, and apply one professional glyph, spacing, and responsive layout system to every existing progress and terminal state.

**Architecture:** Keep worker security, protocol containment, ViewModel semantics, and classification unchanged. Move runtime stage callbacks around the work they describe, put an attempt-keyed deterministic milestone sequencer between accepted ViewModel snapshots and the existing render coordinator, and replace stock status symbols/fixed geometry with reusable vector glyphs and natural-height layouts. Safety terminals bypass pacing; normal completed outcomes wait behind the genuine five-stage sequence; reduced motion renders the newest semantic state immediately.

**Tech Stack:** C# 12; .NET 8; WinUI 3 / Windows App SDK 2.2; XAML compiled bindings; Windows Composition; `DispatcherQueueTimer`; MSTest 4.3.2; Microsoft Testing Platform for worker/contracts; packaged AppContainer tests through Visual Studio `vstest.console.exe`; Windows PowerShell 5.1-compatible gate scripts (also valid under CI PowerShell 7).

---

## Source of truth and execution boundary

- Approved design: `docs/superpowers/specs/2026-08-14-model-inspection-progress-and-visual-polish-design.md`.
- Prior visual contract: `docs/superpowers/specs/2026-08-09-model-inspection-figma-fidelity-and-motion-design.md`.
- Normal-branch requirement: execute in `C:\Users\Arian\source\repos\IBM-Granite-TurboQuant-Intel` on `refactor/model-inspection-cleanup`; do not create another worktree.
- Hardware Inspection remains a separate next feature. Do not add Hardware Fit navigation, probes, models, or UI in this plan.
- Preserve worker-manifest verification, process containment, privacy redaction, model-file integrity precedence, the five public stage names, and existing classifier outcomes.
- The 550 ms dwell is presentation-only. Worker/runtime/service code must contain no `Task.Delay`, `Thread.Sleep`, presentation timer, fabricated percentage, or fabricated completed stage.
- Do not use stopwatch timing in correctness tests. All dwell tests use a manual injected scheduler.
- Every source file created here must be added to both cleanup registers before the next full Contracts run.

## File/responsibility map

### Runtime and worker

- `runtime/GraniteEdgeAI.ModelInspection.LlamaSharp/ModelProbe/VocabOnlyProbeProgress.cs`: typed phase/status/fraction facts emitted by the real probe.
- `runtime/GraniteEdgeAI.ModelInspection.LlamaSharp/ModelProbe/VocabOnlyProbePhaseSequence.cs`: testable operation wrapper that emits Active only before delegate entry and Completed only after normal return.
- `runtime/GraniteEdgeAI.ModelInspection.LlamaSharp/ModelProbe/VocabOnlyEvidenceCollector.cs`: focused configuration, tokenizer/chat, and structure collection methods that retain the existing final evidence shape.
- `runtime/GraniteEdgeAI.ModelInspection.LlamaSharp/ModelProbe/VocabOnlyModelProbe.cs`: brackets real operations with typed progress facts.
- `workers/GraniteEdgeAI.ModelInspection.Worker/LlamaSharpInspectionEngine.cs`: validates typed facts and maps them to the existing five `WorkerStage` messages.
- `tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/LlamaSharpInspectionEngineTests.cs`: preserves the protected 67-case engine class while updating its ordered-progress oracle.
- `tools/ModelInspection.LlamaSharpSpike.Tests/Progress/VocabOnlyProbeStageBoundaryTests.cs`: new deterministic runtime-boundary tests without changing the protected worker method map.

### Presentation and motion

- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/InspectionStartupPresentation.cs`: truthful active/no-progress state above the five counted stages.
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ViewModels/IModelInspectionStartupPresentationBarrier.cs`: explicit first-frame render barrier before secure service startup.
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/DispatcherModelInspectionStartupPresentationBarrier.cs`: production dispatcher implementation of that barrier.
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/IModelInspectionMilestoneScheduler.cs`: one cancellable UI scheduling seam.
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/DispatcherQueueModelInspectionMilestoneScheduler.cs`: production `DispatcherQueueTimer` adapter.
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionMilestoneSequencer.cs`: attempt/lifetime keyed 550 ms playback state machine.
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionPresentationFactory.cs`: first-class active/no-progress startup presentation.
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/InspectionProgressRows.cs`: fraction-only updates remain non-semantic and non-animated.
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs`: owns sequencer lifetime, startup yield, motion-policy flush, and immediate safety paths.
- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/ModelInspectionMilestoneSequencerTests.cs`: pure manual-scheduler tests.

### Visual system and layouts

- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/InspectionStatusGlyphKind.cs`: success/warning/error/information/waiting/active glyph identity.
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionStatusGlyph.xaml` and `.xaml.cs`: vector geometry plus Precision Orbit.
- `InspectionContentCard`, `InspectionModelCard`, `InspectionOutcomeCard`, `InspectionActionCard`: consume the shared glyph and natural layout rules.
- `ModelInspectionPage.xaml` and `.xaml.cs`: one 24 px header gap and 16 px inter-card rhythm, without per-state magic margins.
- `Features/Onboarding/Controls/OnboardingStageIndicator.xaml` and `.xaml.cs`: reuses the same complete success geometry in the footer.
- `tests/.../Controls/InspectionStatusGlyphTests.cs`: geometry, size, motion, High Contrast, and automation contract.
- Existing rendered-state, layout, control, motion, and accessibility suites: replace obsolete exact geometry with the approved new geometry while retaining all 13 states and four disclosure pairs.

### Fixtures, governance, and docs

- `tests/TestFixtures/ModelInspectionScenarios/MI-050-progress-starting-secure-inspection.fixture.json`: active attempt before first worker progress.
- Fixture schema/policy/manifest/generator/catalogue/expected hashes: exact MI-050 registration and revised geometry.
- Workflow and contract maps: measured test counts only after final TRX parsing; retain the Release filter, 686 floor, and isolation boundary.
- Current READMEs, verification matrix, and cleanup ledger: describe truthful boundaries, 550 ms UI pacing, new glyph language, and local evidence.

## Reusable test commands

Define these helpers once per PowerShell session. A packaged run is valid only when the TRX proves `total == executed == passed` and every adverse counter is zero.

```powershell
$ErrorActionPreference = 'Stop'

function Assert-ModelInspectionTrx {
  param(
    [Parameter(Mandatory)] [string] $Path,
    [Parameter(Mandatory)] [int] $ExpectedTotal,
    [Parameter(Mandatory)] [hashtable] $ExpectedClassCounts
  )

  if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    throw "Fresh TRX is missing: $Path"
  }

  [xml] $trx = Get-Content -LiteralPath $Path -Raw
  $ns = [System.Xml.XmlNamespaceManager]::new($trx.NameTable)
  $ns.AddNamespace('t', $trx.DocumentElement.NamespaceURI)
  $counters = $trx.SelectSingleNode('//t:ResultSummary/t:Counters', $ns)
  if ($null -eq $counters) { throw 'TRX counters are missing.' }

  $total = [int] $counters.total
  $executed = [int] $counters.executed
  $passed = [int] $counters.passed
  if ($total -ne $ExpectedTotal) {
    throw "TRX total $total != exact expected $ExpectedTotal."
  }
  if ($executed -ne $total -or $passed -ne $total) {
    throw "TRX is not all-pass: total=$total executed=$executed passed=$passed."
  }

  foreach ($name in @(
      'failed','error','timeout','aborted','inconclusive','passedButRunAborted',
      'notRunnable','notExecuted','disconnected','warning','completed','inProgress','pending')) {
    if ($counters.HasAttribute($name) -and [int] $counters.GetAttribute($name) -ne 0) {
      throw "TRX adverse counter $name=$($counters.GetAttribute($name))."
    }
  }

  $definitions = @($trx.SelectNodes('//t:TestDefinitions/t:UnitTest', $ns))
  $results = @($trx.SelectNodes('//t:Results/t:UnitTestResult', $ns))
  if ($definitions.Count -ne $total -or $results.Count -ne $total) {
    throw "TRX identity cardinality mismatch: definitions=$($definitions.Count) results=$($results.Count) total=$total."
  }
  $actualClassNames = @($definitions | ForEach-Object {
    [string]$_.TestMethod.className
  } | Sort-Object -Unique)
  $unexpected = @($actualClassNames | Where-Object { -not $ExpectedClassCounts.ContainsKey($_) })
  if ($unexpected.Count -ne 0) { throw "Unexpected TRX classes: $($unexpected -join ', ')" }

  $expectedMapTotal = 0
  foreach ($count in $ExpectedClassCounts.Values) { $expectedMapTotal += [int]$count }
  if ($expectedMapTotal -ne $total) {
    throw "Expected class-map total $expectedMapTotal != TRX total $total."
  }

  foreach ($className in $ExpectedClassCounts.Keys) {
    $matchingDefinitions = @($definitions | Where-Object {
      [string]$_.TestMethod.className -ceq $className
    })
    if ($matchingDefinitions.Count -ne [int]$ExpectedClassCounts[$className]) {
      throw "TRX class count mismatch: $className=$($matchingDefinitions.Count), expected=$($ExpectedClassCounts[$className])."
    }
    foreach ($definition in $matchingDefinitions) {
      $matchingResults = @($results | Where-Object { $_.testId -eq $definition.id })
      if ($matchingResults.Count -ne 1 -or $matchingResults[0].outcome -ne 'Passed') {
        throw "TRX result identity/outcome mismatch: $className / $($definition.name)"
      }
    }
  }
}

function Invoke-PackagedModelInspectionTests {
  param(
    [Parameter(Mandatory)] [string] $Filter,
    [Parameter(Mandatory)] [string] $ResultName,
    [Parameter(Mandatory)] [int] $ExpectedTotal,
    [Parameter(Mandatory)] [hashtable] $ExpectedClassCounts,
    [ValidateSet('Debug', 'Release')] [string] $Configuration = 'Debug'
  )

  $project = '.\tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj'
  $recipe = ".\tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\$Configuration\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe"
  $resultStem = [IO.Path]::GetFileNameWithoutExtension($ResultName)
  $results = ".\TestResults\ModelInspectionPolish\$Configuration\$resultStem"
  if (Test-Path -LiteralPath $results) { throw "Results path is not fresh: $results" }

  dotnet restore $project --runtime win-x64 -p:Platform=x64
  if ($LASTEXITCODE -ne 0) { throw 'Packaged restore failed.' }
  dotnet build $project --configuration $Configuration --no-restore --runtime win-x64 -p:Platform=x64
  if ($LASTEXITCODE -ne 0) { throw 'Packaged build failed.' }

  New-Item -ItemType Directory -Force -Path $results | Out-Null
  $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
  $vstest = & $vswhere -latest -products * -find '**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' | Select-Object -First 1
  if (-not $vstest) { throw 'vstest.console.exe was not found.' }

  & $vstest (Resolve-Path $recipe).Path `
    /Platform:x64 `
    "/TestCaseFilter:$Filter" `
    "/Logger:trx;LogFileName=$ResultName" `
    "/ResultsDirectory:$((Resolve-Path $results).Path)"
  if ($LASTEXITCODE -ne 0) { throw "Packaged run failed: $ResultName" }

  Assert-ModelInspectionTrx `
    -Path (Join-Path (Resolve-Path $results).Path $ResultName) `
    -ExpectedTotal $ExpectedTotal `
    -ExpectedClassCounts $ExpectedClassCounts
}

function Invoke-WorkerTests {
  param(
    [Parameter(Mandatory)] [string] $Filter,
    [Parameter(Mandatory)] [string] $ResultName,
    [Parameter(Mandatory)] [int] $ExpectedTotal,
    [Parameter(Mandatory)] [hashtable] $ExpectedClassCounts
  )
  $resultStem = [IO.Path]::GetFileNameWithoutExtension($ResultName)
  $results = ".\TestResults\ModelInspectionPolish\Worker\$resultStem"
  if (Test-Path -LiteralPath $results) { throw "Results path is not fresh: $results" }
  New-Item -ItemType Directory -Path $results | Out-Null
  dotnet test '.\tests\UnitTests\GraniteEdgeAI.ModelInspection.Worker.Tests\GraniteEdgeAI.ModelInspection.Worker.Tests.csproj' `
    --configuration Release `
    -p:Platform=x64 `
    --filter $Filter `
    --results-directory $results `
    --report-trx `
    --report-trx-filename $ResultName
  if ($LASTEXITCODE -ne 0) { throw 'Worker tests failed.' }
  Assert-ModelInspectionTrx -Path (Join-Path $results $ResultName) `
    -ExpectedTotal $ExpectedTotal -ExpectedClassCounts $ExpectedClassCounts
}

function Invoke-RuntimeTests {
  param(
    [Parameter(Mandatory)] [string] $Filter,
    [Parameter(Mandatory)] [string] $ResultName,
    [Parameter(Mandatory)] [int] $ExpectedTotal,
    [Parameter(Mandatory)] [hashtable] $ExpectedClassCounts
  )
  $resultStem = [IO.Path]::GetFileNameWithoutExtension($ResultName)
  $results = ".\TestResults\ModelInspectionPolish\Runtime\$resultStem"
  if (Test-Path -LiteralPath $results) { throw "Results path is not fresh: $results" }
  New-Item -ItemType Directory -Path $results | Out-Null
  dotnet test '.\tools\ModelInspection.LlamaSharpSpike.Tests\ModelInspection.LlamaSharpSpike.Tests.csproj' `
    --configuration Release `
    --runtime win-x64 `
    --filter $Filter `
    --results-directory $results `
    --report-trx `
    --report-trx-filename $ResultName
  if ($LASTEXITCODE -ne 0) { throw 'Runtime tests failed.' }
  Assert-ModelInspectionTrx -Path (Join-Path $results $ResultName) `
    -ExpectedTotal $ExpectedTotal -ExpectedClassCounts $ExpectedClassCounts
}
```

## Required RED discipline

For every task below, run the named focused command immediately after adding the test and before production code. A missing-type compile failure is acceptable only for the first run; after the type exists, retain at least one behavioral RED showing the obsolete contract. Save separate `*-red.trx` and `*-green.trx` files. Never reuse a prior binary or rerun a RED without a source correction.

## Task 1: Publish an immediate, truthful startup presentation

**Files:**

- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/InspectionStartupPresentation.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/InspectionContentCardPresentation.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ViewModels/IModelInspectionStartupPresentationBarrier.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/DispatcherModelInspectionStartupPresentationBarrier.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ViewModels/ModelInspectionViewModel.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionPresentationFactory.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/ModelInspectionPage.DebugFixtures.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Runtime/ModelInspectionFixtureSession.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/ModelInspectionPresentationFactoryTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/InitialInspectionProgressPresentationTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageNavigationTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ViewModels/ModelInspectionViewModelTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Infrastructure/ModelInspectionWorkerCompositionTests.cs`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

- [ ] **Step 1: Add the startup RED without changing the five-row contract**

Extend `Create_InitialSnapshotUsesSafeMetadataAndDisablesCancelBeforeRun` so the idle snapshot still has no startup banner. Add the active/no-progress assertions inside the existing `Create_ProgressSnapshotProjectsTheLatestFiveStageUpdate` method; do not add a discovered execution:

```csharp
ModelInspectionViewSnapshot starting = new(
    new ModelInspectionRenderKey(1, 1),
    isRunActive: true,
    isCancellationRequested: false,
    progress: null,
    terminalResult: null);

ModelInspectionPagePresentation presentation =
    ModelInspectionPresentationFactory.Create(
        PresentationTestData.CreateRequest(),
        starting,
        commands,
        isDisclosureExpanded: false,
        new InspectionProgressRows());

Assert.AreEqual("Starting secure inspection…", presentation.ContentCard.Startup.Summary);
Assert.AreEqual(Visibility.Visible, presentation.ContentCard.Startup.Visibility);
Assert.AreEqual("Model inspection is starting.", presentation.ContentCard.Startup.AutomationName);
Assert.AreEqual("Model inspection is starting.", presentation.ProgressAnnouncement);
Assert.AreEqual("0 of 5 checks complete", presentation.ContentCard.ProgressSummary);
Assert.IsTrue(presentation.ContentCard.Items.All(row =>
    row.Status == InspectionContentStatus.Waiting && !row.IsActive));
```

In the existing held-service page test, assert that this presentation is rendered before the service releases Stage 1, that the heading retains focus, and that Cancel remains enabled for the active attempt. Expected RED: active/null-progress still says `Awaiting inspection` and has no dedicated startup visual.

Run:

```powershell
Invoke-PackagedModelInspectionTests `
  -Filter 'FullyQualifiedName~ModelInspectionPresentationFactoryTests|FullyQualifiedName~InitialInspectionProgressPresentationTests|FullyQualifiedName~ModelInspectionViewModelTests|FullyQualifiedName~ModelInspectionPageNavigationTests|FullyQualifiedName~ModelInspectionWorkerCompositionTests' `
  -ResultName 'task-01-startup-red.trx' `
  -ExpectedTotal 100 `
  -ExpectedClassCounts ([ordered]@{
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.ModelInspectionPresentationFactoryTests' = 18
    'GraniteEdgeAI.UnitTests.InitialInspectionProgressPresentationTests' = 6
    'GraniteEdgeAI.UnitTests.ModelInspectionViewModelTests' = 24
    'GraniteEdgeAI.UnitTests.ModelInspectionPageNavigationTests' = 39
    'GraniteEdgeAI.UnitTests.ModelInspectionWorkerCompositionTests' = 13
  })
```

- [ ] **Step 2: Add one explicit startup presentation model**

Create the focused model and expose it from `InspectionContentCardPresentation`:

```csharp
public sealed class InspectionStartupPresentation
{
    public static InspectionStartupPresentation Hidden { get; } = new();
    public Visibility Visibility { get; init; } = Visibility.Collapsed;
    public string Summary { get; init; } = string.Empty;
    public string AutomationName { get; init; } = string.Empty;
}

public InspectionStartupPresentation Startup { get; init; } =
    InspectionStartupPresentation.Hidden;
```

In `CreateProgressState`, distinguish idle from active startup:

```csharp
bool isStarting = snapshot.IsRunActive && progress is null;
string statusSummary = isStarting
    ? "Starting secure inspection…"
    : progress is null
        ? "Awaiting inspection"
        : "Inspection in progress";

InspectionStartupPresentation startup = isStarting
    ? new()
    {
        Visibility = Visibility.Visible,
        Summary = "Starting secure inspection…",
        AutomationName = "Model inspection is starting."
    }
    : InspectionStartupPresentation.Hidden;
```

Pass `startup` through a focused `InitialInspectionProgressPresentationFactory.Create(progressRows, startup)` overload; do not convert all presentation models to records. Include startup visibility/summary/automation name in the content-region key so its first render cannot be suppressed.

Set `progressAnnouncement` to `Model inspection is starting.` only for the first active/null-progress revision. Keep idle initial presentation silent and preserve the exact five waiting rows.

- [ ] **Step 3: Add a one-dispatch-turn presentation barrier before service preflight**

Publishing the snapshot is insufficient because manifest hashing can execute before the UI dispatcher drains. Add:

```csharp
internal interface IModelInspectionStartupPresentationBarrier
{
    ValueTask WaitForPresentationAsync();
}
```

The production `DispatcherModelInspectionStartupPresentationBarrier` enqueues one completion callback on the same render dispatcher. `ModelInspectionViewModel.StartAsync` must: publish active/null-progress; raise command state; await the barrier; then call `_service.InspectAsync`. Existing isolated ViewModel tests inject an immediate barrier; the page injects the dispatcher barrier. Enqueue failure is a controlled operational failure, not permission to begin invisible work.

In a manual-dispatcher page test prove the order:

```csharp
Task run = page.StartInspectionIfReadyAsync()!;
dispatcher.RunNext(); // applies startup presentation
Assert.AreEqual(0, service.CallCount);
Assert.AreEqual(
    "Starting secure inspection…",
    page.CurrentPresentation!.ContentCard.Startup.Summary);
dispatcher.RunNext(); // releases startup barrier
await WaitUntilAsync(() => service.CallCount == 1);
```

Keep manifest verification tests green; no cache/bypass is introduced.

Extend the existing protected `PackagedN001_PageJourneyCompletesAllFiveStagesAsReady` method rather than adding a new execution. Before releasing/observing the first worker stage, require one rendered `Starting secure inspection…` presentation with five Waiting rows and no counted Active stage; then retain its existing exact ten semantic stage transitions and Ready terminal assertions. This makes the final N-001 1/1 gate prove both startup feedback and semantic order.

- [ ] **Step 4: Render startup above the five counted rows**

Add a dedicated `StartupStatusRow` under the progress heading and above the five-stage repeater. Use a named 30 px `StartupActiveIndicatorHost` containing one permanently indeterminate `ProgressRing` for the Task 1 GREEN; Task 5 replaces only that host's contents with the shared Precision Orbit control. The startup presentation path is therefore independently runnable before Task 5 and never creates a sixth stage.

In `ModelInspectionPage_Loaded`, call `StartInspectionIfReadyAsync()` before deferred focus bookkeeping. The barrier—not a sleep—provides the render opportunity. Keep the focus callback and manifest verification unchanged.

- [ ] **Step 5: Verify startup GREEN and commit**

Run the same exact 100-execution map with `task-01-startup-green.trx`. Require exactly one startup announcement, five waiting rows, no focus move, and no sixth stage. Extend existing test methods only; do not change discovery counts in these protected classes.

```powershell
Invoke-PackagedModelInspectionTests `
  -Filter 'FullyQualifiedName~ModelInspectionPresentationFactoryTests|FullyQualifiedName~InitialInspectionProgressPresentationTests|FullyQualifiedName~ModelInspectionViewModelTests|FullyQualifiedName~ModelInspectionPageNavigationTests|FullyQualifiedName~ModelInspectionWorkerCompositionTests' `
  -ResultName 'task-01-startup-green.trx' `
  -ExpectedTotal 100 `
  -ExpectedClassCounts ([ordered]@{
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.ModelInspectionPresentationFactoryTests' = 18
    'GraniteEdgeAI.UnitTests.InitialInspectionProgressPresentationTests' = 6
    'GraniteEdgeAI.UnitTests.ModelInspectionViewModelTests' = 24
    'GraniteEdgeAI.UnitTests.ModelInspectionPageNavigationTests' = 39
    'GraniteEdgeAI.UnitTests.ModelInspectionWorkerCompositionTests' = 13
  })
```

```powershell
git add -- `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/InspectionStartupPresentation.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/InspectionContentCardPresentation.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ViewModels/IModelInspectionStartupPresentationBarrier.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/DispatcherModelInspectionStartupPresentationBarrier.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ViewModels/ModelInspectionViewModel.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionPresentationFactory.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/ModelInspectionPage.DebugFixtures.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Runtime/ModelInspectionFixtureSession.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/ModelInspectionPresentationFactoryTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/InitialInspectionProgressPresentationTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageNavigationTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ViewModels/ModelInspectionViewModelTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Infrastructure/ModelInspectionWorkerCompositionTests.cs' `
  'docs/reviews/model-inspection-cleanup-source-files.txt' `
  'docs/reviews/model-inspection-cleanup-inventory.md'
git commit -m 'feat(model-inspection): show secure startup immediately'
```

## Task 2: Make all five worker stages bracket their real work

**Files:**

- Modify: `runtime/GraniteEdgeAI.ModelInspection.LlamaSharp/ModelProbe/VocabOnlyProbeProgress.cs`
- Create: `runtime/GraniteEdgeAI.ModelInspection.LlamaSharp/ModelProbe/VocabOnlyProbePhaseSequence.cs`
- Modify: `runtime/GraniteEdgeAI.ModelInspection.LlamaSharp/ModelProbe/VocabOnlyEvidenceCollector.cs`
- Modify: `runtime/GraniteEdgeAI.ModelInspection.LlamaSharp/ModelProbe/VocabOnlyModelProbe.cs`
- Modify: `runtime/GraniteEdgeAI.ModelInspection.LlamaSharp/ModelProbe/NativeLoadProgressRecorder.cs`
- Modify: `workers/GraniteEdgeAI.ModelInspection.Worker/LlamaSharpInspectionEngine.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.Tests/Progress/VocabOnlyProbeStageBoundaryTests.cs`
- Modify: `tools/ModelInspection.LlamaSharpSpike.Tests/FileSafety/VocabOnlyModelProbeContinuityTests.cs`
- Modify: `tools/ModelInspection.LlamaSharpSpike.Tests/Progress/NativeLoadProgressRecorderTests.cs`
- Modify: `tools/ModelInspection.LlamaSharpSpike.Tests/Metadata/VocabOnlyCollectorSourceContractTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/LlamaSharpInspectionEngineTests.cs`
- Modify: `tools/ModelInspection.LlamaSharpSpike.Tests/DependencyPolicy/ProductionRuntimeArchitectureTests.cs`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

- [ ] **Step 1: Write the stage-boundary REDs**

Create a deterministic phase recorder that executes supplied delegates and records the callback present when each delegate ran. Require this exact sequence:

```csharp
(VocabOnlyProbePhase.CheckModelPackage, VocabOnlyProbePhaseStatus.Active),
(VocabOnlyProbePhase.CheckModelPackage, VocabOnlyProbePhaseStatus.Completed),
(VocabOnlyProbePhase.ReadModelConfiguration, VocabOnlyProbePhaseStatus.Active),
(VocabOnlyProbePhase.ReadModelConfiguration, VocabOnlyProbePhaseStatus.Fraction),
(VocabOnlyProbePhase.ReadModelConfiguration, VocabOnlyProbePhaseStatus.Completed),
(VocabOnlyProbePhase.ValidateTokenizerAndChatSetup, VocabOnlyProbePhaseStatus.Active),
(VocabOnlyProbePhase.ValidateTokenizerAndChatSetup, VocabOnlyProbePhaseStatus.Completed),
(VocabOnlyProbePhase.ValidateModelStructure, VocabOnlyProbePhaseStatus.Active),
(VocabOnlyProbePhase.ValidateModelStructure, VocabOnlyProbePhaseStatus.Completed)
```

Assert that tokenizer smoke/chat-template reads occur only while tokenizer/chat is Active, and structural projection, model disposal, final snapshot, and integrity comparison occur only while structure is Active. Update `InspectAsyncForwardsExactRequestAndReportsOrderedSuccessfulProgress` to require the existing ten semantic Worker messages in the same public order, with native fractions occurring only inside Stage 2.

Update continuity coverage so Stage 1 Active publishes before the initial hash starts, no Stage 1 Completed publishes while a controlled hasher is held, and Completed publishes only after capture returns. Add a field-complete `Compose` equivalence case for every `VocabOnlyRuntimeModelEvidence` property, including sorted metadata keys, special tokens, tokenizer smoke, and chat template.

Run:

```powershell
Invoke-RuntimeTests `
  -Filter 'FullyQualifiedName~VocabOnlyProbeStageBoundaryTests|FullyQualifiedName~VocabOnlyModelProbeContinuityTests|FullyQualifiedName~NativeLoadProgressRecorderTests|FullyQualifiedName~VocabOnlyCollectorSourceContractTests' `
  -ResultName 'task-02-runtime-red.trx' `
  -ExpectedTotal 24 `
  -ExpectedClassCounts ([ordered]@{
    'GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.VocabOnlyProbeStageBoundaryTests' = 8
    'GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.VocabOnlyModelProbeContinuityTests' = 4
    'GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.NativeLoadProgressRecorderTests' = 10
    'GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.VocabOnlyCollectorSourceContractTests' = 2
  })
Invoke-WorkerTests `
  -Filter 'FullyQualifiedName~LlamaSharpInspectionEngineTests' `
  -ResultName 'task-02-worker-red.trx' `
  -ExpectedTotal 67 `
  -ExpectedClassCounts ([ordered]@{ 'GraniteEdgeAI.ModelInspection.Worker.Tests.LlamaSharpInspectionEngineTests' = 67 })
```

Expected RED: the typed phases do not exist and Stages 3/4 are synthesized after `RunAsync` has returned.

- [ ] **Step 2: Replace ambiguous booleans with typed probe facts**

Use the following public runtime-only contract; it does not cross the worker protocol boundary:

```csharp
public enum VocabOnlyProbePhase
{
    CheckModelPackage,
    ReadModelConfiguration,
    ValidateTokenizerAndChatSetup,
    ValidateModelStructure
}

public enum VocabOnlyProbePhaseStatus
{
    Active,
    Fraction,
    Completed
}

public sealed record VocabOnlyProbeProgress(
    VocabOnlyProbePhase Phase,
    VocabOnlyProbePhaseStatus Status,
    float? NativeFraction = null);
```

Validate that only `ReadModelConfiguration/Fraction` may carry a finite value in `[0,1]`, and that Active/Completed carry null. Update `NativeLoadProgressRecorder` to emit only the typed fraction fact.

- [ ] **Step 3: Split evidence collection by responsibility without changing output**

Refactor `VocabOnlyEvidenceCollector` into focused internal calls:

```csharp
internal static VocabOnlyConfigurationProjection CollectConfiguration(LLamaWeights weights);
internal static VocabOnlyTokenizerProjection CollectTokenizerAndChat(LLamaWeights weights);
internal static VocabOnlyStructureProjection CollectStructure(LLamaWeights weights);
internal static VocabOnlyRuntimeModelEvidence Compose(
    VocabOnlyConfigurationProjection configuration,
    VocabOnlyTokenizerProjection tokenizer,
    VocabOnlyStructureProjection structure);
```

The projections are internal immutable records in the same file unless another production consumer appears. `Compose` must reproduce every existing field, null policy, metadata-key ordering, token ID, smoke result, and chat-template result exactly. Do not change `VocabOnlyRuntimeModelEvidence` or the worker protocol.

- [ ] **Step 4: Add an executable phase wrapper and emit around actual operations**

Create the testable orchestration seam:

```csharp
internal sealed class VocabOnlyProbePhaseSequence(
    IProgress<VocabOnlyProbeProgress>? progress)
{
    internal T Run<T>(VocabOnlyProbePhase phase, Func<T> operation);
    internal Task<T> RunAsync<T>(
        VocabOnlyProbePhase phase,
        Func<Task<T>> operation);
    internal void ReportNativeFraction(float fraction);
}
```

`Run`/`RunAsync` report Active immediately before delegate entry and Completed only after normal return. Exception/cancellation never fabricates Completed. `ReportNativeFraction` is valid only while configuration is Active. Pure tests use fake delegates; `VocabOnlyCollectorSourceContractTests` asserts production call sites wrap tokenizer/chat collection, structure collection, disposal, final snapshot, and integrity comparison.

In `VocabOnlyModelProbe.RunCoreAsync`:

1. report Stage 1 Active before the initial snapshot and Completed immediately after it;
2. report Stage 2 Active before backend selection/load/config projection, forward genuine fractions, and complete after configuration projection;
3. report Stage 3 Active/Completed around `CollectTokenizerAndChat`;
4. report Stage 4 Active before `CollectStructure`, dispose the model, capture/compare the final snapshot, then report Completed;
5. never emit a Completed fact when that phase throws or is cancelled.

Move `weights` disposal and final integrity work into the Stage 4 bracket while retaining `finally` cleanup on every exceptional path.

- [ ] **Step 5: Map typed phases fail-closed in the worker**

Replace `CompleteSimpleStage` with a strict transition table. Stage 5 remains worker-owned and brackets only privacy-safe evidence mapping plus `evidence.Validate()`:

```csharp
state.AcceptProbeFact(value);
// ... after successful Stage 4 Completed
state.BeginRuntimeStage();
WorkerInspectionEvidence evidence =
    LlamaSharpInspectionEvidenceMapper.Map(runtimeResult, command, _workerVersion);
evidence.Validate();
state.CompleteRuntimeStage();
```

Reject duplicate, skipped, regressed, fraction-before-active, fraction-after-complete, or phase-after-finish facts as controlled generic failure. Preserve the protected 67 `LlamaSharpInspectionEngineTests` identities; modify assertions inside existing methods rather than adding/removing DataRows there.

- [ ] **Step 6: Prove equivalent evidence and commit**

Create exactly eight ordinary methods in `VocabOnlyProbeStageBoundaryTests` and no DataRows. Run the same exact runtime 24-map and worker 67-map with `task-02-runtime-green.trx` and `task-02-worker-green.trx`. Expected GREEN: stage-boundary suite passes; the focused worker class remains exactly 67/67; deterministic evidence is field-for-field unchanged.

```powershell
Invoke-RuntimeTests `
  -Filter 'FullyQualifiedName~VocabOnlyProbeStageBoundaryTests|FullyQualifiedName~VocabOnlyModelProbeContinuityTests|FullyQualifiedName~NativeLoadProgressRecorderTests|FullyQualifiedName~VocabOnlyCollectorSourceContractTests' `
  -ResultName 'task-02-runtime-green.trx' `
  -ExpectedTotal 24 `
  -ExpectedClassCounts ([ordered]@{
    'GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.VocabOnlyProbeStageBoundaryTests' = 8
    'GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.VocabOnlyModelProbeContinuityTests' = 4
    'GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.NativeLoadProgressRecorderTests' = 10
    'GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests.VocabOnlyCollectorSourceContractTests' = 2
  })
Invoke-WorkerTests `
  -Filter 'FullyQualifiedName~LlamaSharpInspectionEngineTests' `
  -ResultName 'task-02-worker-green.trx' `
  -ExpectedTotal 67 `
  -ExpectedClassCounts ([ordered]@{ 'GraniteEdgeAI.ModelInspection.Worker.Tests.LlamaSharpInspectionEngineTests' = 67 })
```

```powershell
git add -- `
  'runtime/GraniteEdgeAI.ModelInspection.LlamaSharp/ModelProbe/VocabOnlyProbeProgress.cs' `
  'runtime/GraniteEdgeAI.ModelInspection.LlamaSharp/ModelProbe/VocabOnlyProbePhaseSequence.cs' `
  'runtime/GraniteEdgeAI.ModelInspection.LlamaSharp/ModelProbe/VocabOnlyEvidenceCollector.cs' `
  'runtime/GraniteEdgeAI.ModelInspection.LlamaSharp/ModelProbe/VocabOnlyModelProbe.cs' `
  'runtime/GraniteEdgeAI.ModelInspection.LlamaSharp/ModelProbe/NativeLoadProgressRecorder.cs' `
  'workers/GraniteEdgeAI.ModelInspection.Worker/LlamaSharpInspectionEngine.cs' `
  'tools/ModelInspection.LlamaSharpSpike.Tests/Progress/VocabOnlyProbeStageBoundaryTests.cs' `
  'tools/ModelInspection.LlamaSharpSpike.Tests/FileSafety/VocabOnlyModelProbeContinuityTests.cs' `
  'tools/ModelInspection.LlamaSharpSpike.Tests/Progress/NativeLoadProgressRecorderTests.cs' `
  'tools/ModelInspection.LlamaSharpSpike.Tests/Metadata/VocabOnlyCollectorSourceContractTests.cs' `
  'tools/ModelInspection.LlamaSharpSpike.Tests/DependencyPolicy/ProductionRuntimeArchitectureTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/LlamaSharpInspectionEngineTests.cs' `
  'docs/reviews/model-inspection-cleanup-source-files.txt' `
  'docs/reviews/model-inspection-cleanup-inventory.md'
git commit -m 'fix(model-inspection): align progress with real probe work'
```

## Task 3: Make fractions silent and keep one active-indicator policy

**Files:**

- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/InspectionProgressRows.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/InspectionContentItemPresentation.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/InspectionProgressRowsTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionContentCardTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionVisualStateGuardTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionAccessibilityTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/ModelInspectionFixtureAdapterTests.cs`

- [ ] **Step 1: Extend existing protected tests with a fraction-only RED**

Within existing methods, apply `.25` then `.75` for the same attempt, stage, and Active status using successive presentation revisions. Assert:

```csharp
Assert.IsFalse(update.RowChanges.Single().StatusChanged);
Assert.IsTrue(update.RowChanges.Single().FractionChanged);
Assert.AreSame(rowBefore, rows.Items[1]);
Assert.AreEqual(0.75, rows.Items[1].StageFraction);
CollectionAssert.AreEqual(
    new[] { nameof(InspectionContentItemPresentation.StageFraction),
            nameof(InspectionContentItemPresentation.StageFractionText) },
    notifications);
```

In the loaded control test, assert no status-marker/detail animation starts, the active glyph instance is retained, and the polite announcement count does not change. Before production edits, run the exact Step 4 63-map with `ResultName 'task-03-fraction-red.trx'`. Expected RED: `StageFraction` participates in `StatusChanged` and the native fraction controls the ring mode.

- [ ] **Step 2: Separate semantic and fraction changes**

Add `FractionChanged` to `InspectionProgressRowChange`. Remove `StageFraction` from the `statusChanged` expression in `InspectionProgressRows.Apply`; update it independently. Add a bounded display property:

```csharp
public string StageFractionText => StageFraction is double value
    ? $"{Math.Round(value * 100d, MidpointRounding.AwayFromZero):0}%"
    : string.Empty;
```

Raise `StageFraction` then `StageFractionText` only when the genuine fraction changes. Keep `AutomationName` and live-region text unchanged for fraction-only updates.

- [ ] **Step 3: Remove determinate-ring behavior**

Delete `IsProgressIndeterminate` and `GetProgressPercent`. Until Task 5 replaces it, retain one permanently indeterminate `ProgressRing` and bind trailing percentage text to `StageFractionText`. The same ring instance/policy remains active for null, `.25`, `.75`, and `1.0`. Update the visual-state guard accordingly.

- [ ] **Step 4: Run GREEN and commit**

```powershell
Invoke-PackagedModelInspectionTests `
  -Filter 'FullyQualifiedName~InspectionProgressRowsTests|FullyQualifiedName~InspectionContentCardTests|FullyQualifiedName~InspectionVisualStateGuardTests|FullyQualifiedName~ModelInspectionAccessibilityTests' `
  -ResultName 'task-03-fraction-green.trx' `
  -ExpectedTotal 63 `
  -ExpectedClassCounts ([ordered]@{
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.InspectionProgressRowsTests' = 16
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.InspectionContentCardTests' = 25
    'GraniteEdgeAI.UnitTests.InspectionVisualStateGuardTests' = 13
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual.ModelInspectionAccessibilityTests' = 9
  })

git add -- `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/InspectionProgressRows.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/InspectionContentItemPresentation.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/InspectionProgressRowsTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionContentCardTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionVisualStateGuardTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionAccessibilityTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/ModelInspectionFixtureAdapterTests.cs'
git commit -m 'fix(model-inspection): keep native fractions visually stable'
```

## Task 4: Add the deterministic 550 ms milestone sequencer

**Files:**

- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/IModelInspectionMilestoneScheduler.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/DispatcherQueueModelInspectionMilestoneScheduler.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionMilestoneSequencer.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionRenderCoordinator.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/ModelInspectionMilestoneSequencerTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/ModelInspectionRenderCoordinatorTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageNavigationTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionAccessibilityTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/ModelInspectionPage.DebugFixtures.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Runtime/ModelInspectionFixtureSession.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/ModelInspectionFixtureAdapterTests.cs`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

- [ ] **Step 1: Write the pure state-machine RED suite**

Create exactly ten ordinary test methods (no DataRows) around a manual scheduler with explicit `AdvanceBy(TimeSpan)` and cancellation tracking. Cover:

```csharp
sequencer.Accept(stage1Active);
sequencer.Accept(stage1Completed);
sequencer.Accept(stage2Active);

CollectionAssert.AreEqual(new[] { stage1Active }, applied);
sequencer.NotifyPresented(stage1Active.RenderKey);
scheduler.AdvanceBy(TimeSpan.FromMilliseconds(549));
CollectionAssert.AreEqual(new[] { stage1Active }, applied);
scheduler.AdvanceBy(TimeSpan.FromMilliseconds(1));
CollectionAssert.AreEqual(new[] { stage1Active, stage1Completed }, applied);
sequencer.NotifyPresented(stage1Completed.RenderKey);
CollectionAssert.AreEqual(
    new[] { stage1Active, stage1Completed, stage2Active }, applied);
```

Add separate tests for: a naturally long stage completing after 700 ms with no added wait; all five stages arriving as one burst; fraction coalescing within current Active; normal Completed terminal waiting behind Stage 5; Cancelled and OperationalFailure flushing immediately; cancellation-requested, Retry generation, navigation/disposal, and reduced motion invalidating the queue; stale callbacks being ignored; and a maximum retained queue of five stages plus one normal terminal.

Before production edits, run the exact Step 5 85-map with `ResultName 'task-04-sequencer-red.trx'`. Expected RED: sequencer and scheduler seam are absent.

- [ ] **Step 2: Implement one cancellable scheduler seam**

```csharp
internal interface IModelInspectionMilestoneScheduler : IDisposable
{
    TimeSpan Elapsed { get; }
    IDisposable Schedule(TimeSpan delay, Action callback);
}
```

Production captures one `startTimestamp = Stopwatch.GetTimestamp()` and returns monotonic `Stopwatch.GetElapsedTime(startTimestamp)`. `Schedule` rejects zero/negative delay, uses a one-shot `DispatcherQueueTimer`, and returns an idempotent handle that stops/detaches the timer. Neither a disposed handle nor a disposed scheduler can execute its callback. It does not use `Task.Delay`.

- [ ] **Step 3: Implement the keyed sequencer**

Use this public-internal shape:

```csharp
internal sealed class ModelInspectionMilestoneSequencer : IDisposable
{
    internal static readonly TimeSpan MinimumVisibleStage =
        TimeSpan.FromMilliseconds(550);

    internal ModelInspectionMilestoneSequencer(
        IModelInspectionMilestoneScheduler scheduler,
        Action<ModelInspectionViewSnapshot> applySnapshot,
        bool animationsEnabled);

    internal void Accept(ModelInspectionViewSnapshot snapshot);
    internal void NotifyPresented(ModelInspectionRenderKey renderKey);
    internal void SetAnimationsEnabled(bool enabled);
    internal void Invalidate();
    internal bool HasPendingPlayback { get; }
}
```

Store immutable snapshots, never clone/mutate semantic facts. Key work by `AttemptGeneration` plus a sequencer epoch. Releasing Active does not start the clock: `NotifyPresented` for its exact render key records the real first-presented time and schedules the remainder. Release Completed alone; wait for its presentation acknowledgement; only then release the next Active. Stage 5 Completed must be acknowledged before releasing a normal terminal. This prevents the existing coordinator from coalescing adjacent milestones.

Replace queued same-stage Active snapshots only when the change is fraction-only; a fraction update never resets the first-presented timestamp. Keep at most five typed stage slots plus one normal terminal. Cancellation-requested, Progress Failed/Cancelled, terminal Cancelled/OperationalFailure, or a newer attempt increment the epoch, clear pending slots, cancel the timer, and publish the safety/new-attempt snapshot immediately. `SetAnimationsEnabled(false)` publishes the latest semantic snapshot immediately; re-enabling paces only future stage activations. `Invalidate`/`Dispose` cancel silently and never call the apply callback.

- [ ] **Step 4: Integrate before the render coordinator**

Create the sequencer in `ActivateRequest` after the dispatcher/motion settings and before subscriptions. Add an injectable scheduler factory to the page's internal constructor; production creates `DispatcherQueueModelInspectionMilestoneScheduler`, while packaged tests and fixture sessions supply a manual/immediate deterministic scheduler. Route `ViewModel_PropertyChanged` through `sequencer.Accept(viewModel.Snapshot)`; its apply callback calls `coordinator.RequestRender`. Initial idle uses `coordinator.ApplyInitial` directly, then startup passes through immediately because it is not counted.

At the very end of `ApplyDelta`, after bindings, layout-owned state, focus work, and announcements have been applied, call `sequencer.NotifyPresented(delta.RenderKey)`. Do not acknowledge when the coordinator rejected the delta as stale.

On motion disabled, call `sequencer.SetAnimationsEnabled(false)` before flushing coordinator motion. On retirement, invalidate the coordinator, then dispose the sequencer before callback-producing animation/dispatcher cleanup. Update Debug fixture session/adapter constructor and IL guards so test composition never falls back to real 550 ms timers. Keep coordinator latest-only behavior for snapshots the sequencer has released.

- [ ] **Step 5: Run pure and packaged GREEN, then commit**

```powershell
Invoke-PackagedModelInspectionTests `
  -Filter 'FullyQualifiedName~ModelInspectionMilestoneSequencerTests|FullyQualifiedName~ModelInspectionRenderCoordinatorTests|FullyQualifiedName~ModelInspectionPageNavigationTests|FullyQualifiedName~ModelInspectionAccessibilityTests' `
  -ResultName 'task-04-sequencer-green.trx' `
  -ExpectedTotal 85 `
  -ExpectedClassCounts ([ordered]@{
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.ModelInspectionMilestoneSequencerTests' = 10
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.ModelInspectionRenderCoordinatorTests' = 27
    'GraniteEdgeAI.UnitTests.ModelInspectionPageNavigationTests' = 39
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual.ModelInspectionAccessibilityTests' = 9
  })

git add -- `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/IModelInspectionMilestoneScheduler.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/DispatcherQueueModelInspectionMilestoneScheduler.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionMilestoneSequencer.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/ModelInspectionPage.DebugFixtures.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Runtime/ModelInspectionFixtureSession.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionRenderCoordinator.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/ModelInspectionMilestoneSequencerTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/ModelInspectionRenderCoordinatorTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageNavigationTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionAccessibilityTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/ModelInspectionFixtureAdapterTests.cs' `
  'docs/reviews/model-inspection-cleanup-source-files.txt' `
  'docs/reviews/model-inspection-cleanup-inventory.md'
git commit -m 'feat(model-inspection): pace genuine progress milestones'
```

## Task 5: Build one complete vector status language and Precision Orbit

**Files:**

- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/InspectionStatusGlyphKind.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionStatusGlyph.xaml`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionStatusGlyph.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/InspectionOutcomePresentation.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionPresentationFactory.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionMotion.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionOutcomeCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionOutcomeCard.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/Controls/OnboardingStageIndicator.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/Controls/OnboardingStageIndicator.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Observation/ModelInspectionObservedScreen.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Observation/ModelInspectionFixtureScreenObserver.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Observation/ModelInspectionFixtureScreenComparer.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionStatusGlyphTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionContentCardTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionModelCardTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionOutcomeCardTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/ModelInspectionPresentationFactoryTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/ModelInspectionMotionTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionAccessibilityTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageNavigationTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/ModelInspectionFixturePresetTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/Controls/OnboardingStageIndicatorTests.cs`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

- [ ] **Step 1: Write the geometry and motion RED**

Create one loaded-control DataRow for each glyph kind and surface size (13 executions), plus exactly three ordinary methods covering dependency-property validation, Loaded/Unloaded idempotence, and bound-use migration, for exactly 16 new executions. Require:

```csharp
[DataRow(InspectionStatusGlyphKind.Success, 22d)]
[DataRow(InspectionStatusGlyphKind.Success, 30d)]
[DataRow(InspectionStatusGlyphKind.Success, 36d)]
[DataRow(InspectionStatusGlyphKind.Success, 40d)]
[DataRow(InspectionStatusGlyphKind.Warning, 22d)]
[DataRow(InspectionStatusGlyphKind.Warning, 30d)]
[DataRow(InspectionStatusGlyphKind.Error, 30d)]
[DataRow(InspectionStatusGlyphKind.Error, 36d)]
[DataRow(InspectionStatusGlyphKind.Information, 30d)]
[DataRow(InspectionStatusGlyphKind.Waiting, 30d)]
[DataRow(InspectionStatusGlyphKind.Waiting, 36d)]
[DataRow(InspectionStatusGlyphKind.NotComplete, 36d)]
[DataRow(InspectionStatusGlyphKind.Active, 30d)]
```

Assert `AutomationProperties.GetAccessibilityView(control) == AccessibilityView.Raw`, a 24 px vector viewbox is optically centred within each approved 22/30/36/40 px host, and every semantic status mark uses non-font `Path`/`Ellipse` geometry. `StageNumber` is the only allowed decorative text exception for Waiting; it is Raw and never supplies the accessible name. Add bound-use assertions for the 22 px model-check surface, 30 px progress/disclosure surface, 36 px onboarding footer, and 40 px outcome banner. For success, verify one continuous round-cap check path with both endpoints inside the viewbox. For warning, verify symmetric triangle bounds and an exclamation stem/dot sharing the same horizontal centre. For error/info/NotComplete, verify optical centre within one effective pixel. For Active, assert a neutral track, one rounded blue arc, and an exact 1,050 ms linear infinite rotation when motion is enabled; a static arc when disabled.

Before production edits, run the exact Step 5 189-map with `ResultName 'task-05-glyphs-red.trx'`. Expected RED: the control/type do not exist and stock `SymbolIcon`/Unicode marks remain.

- [ ] **Step 2: Define the shared semantic identity and vector control**

```csharp
public enum InspectionStatusGlyphKind
{
    Success,
    Warning,
    Error,
    Information,
    Waiting,
    NotComplete,
    Active
}
```

The control exposes these dependency properties so compiled XAML bindings update an existing control instance:

```csharp
public InspectionStatusGlyphKind Kind { get; set; }
public double SurfaceSize { get; set; } = 30d;
public string StageNumber { get; set; } = string.Empty;
public bool IsMotionEnabled { get; set; } = false;
```

Register all four as `DependencyProperty` values with validation and change callbacks. Reject undefined enum values, non-finite sizes, and every size outside the exact approved set `{ 22d, 30d, 36d, 40d }`; normalize `StageNumber` to the documented single-character waiting label. Use a 24x24 logical canvas. Keep geometry in WinUI XAML resources/`Path.Data` with `StrokeStartLineCap`/`StrokeEndLineCap="Round"`. Use semantic theme brushes, not literal colours. Waiting shows the stage number in the same surface. Active renders a track plus an arc and owns a compositor rotation; `Loaded`, `Unloaded`, Kind, SurfaceSize, and `IsMotionEnabled` changes must start/stop/update idempotently.

Add `PrecisionOrbitDuration = TimeSpan.FromMilliseconds(1050)` to `ModelInspectionMotionSpec`; retain 160/180/240 ms tokens.

- [ ] **Step 3: Replace every status-symbol implementation**

Replace:

- progress/finding/report marker `SymbolIcon`s;
- model check `Accept`/`Important`/`Cancel`/`Help` symbols;
- outcome banner `SymbolIcon` and `InspectionOutcomePresentation.IconSymbol`;
- disclosure summary status symbols;
- onboarding footer's literal `✓`, interrupted `✕`, and not-complete `‖` marks.

Map presentation semantics to `InspectionStatusGlyphKind`; rename outcome `IconSymbol` to `GlyphKind` and update the factory's complete outcome table. Adjacent text remains the accessible name; glyphs stay decorative. Do not expose vector path data to automation. After replacement, affected Content, Model, Outcome, Disclosure, and footer surfaces contain no status `SymbolIcon`, `FontIcon`, or Unicode success/warning/error/information/waiting/active fallback; non-status disclosure chevrons remain unchanged.

- [ ] **Step 4: Feed the motion policy to active glyphs**

Add `SetMotionEnabled(bool)` to `InspectionContentCard`. Both live and outgoing progress cards default false. In `ApplyDelta`, apply the current motion policy to `InspectionContentCardControl` and `OutgoingProgressContentCard` before assigning any Active content or changing visibility; on a reduced-motion change, stop/reset both before the sequencer/coordinator flush. Startup and the current active row bind to this value. Completed/warning/error/info/waiting/not-complete glyphs are static regardless of the setting.

- [ ] **Step 5: Run GREEN and commit**

```powershell
Invoke-PackagedModelInspectionTests `
  -Filter 'FullyQualifiedName~InspectionStatusGlyphTests|FullyQualifiedName~InspectionContentCardTests|FullyQualifiedName~InspectionModelCardTests|FullyQualifiedName~InspectionOutcomeCardTests|FullyQualifiedName~ModelInspectionPresentationFactoryTests|FullyQualifiedName~ModelInspectionMotionTests|FullyQualifiedName~ModelInspectionAccessibilityTests|FullyQualifiedName~ModelInspectionPageNavigationTests|FullyQualifiedName~ModelInspectionFixturePresetTests|FullyQualifiedName~OnboardingStageIndicatorTests' `
  -ResultName 'task-05-glyphs-green.trx' `
  -ExpectedTotal 189 `
  -ExpectedClassCounts ([ordered]@{
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.InspectionStatusGlyphTests' = 16
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.InspectionContentCardTests' = 25
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.InspectionModelCardTests' = 9
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.InspectionOutcomeCardTests' = 7
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.ModelInspectionPresentationFactoryTests' = 18
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation.ModelInspectionMotionTests' = 29
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual.ModelInspectionAccessibilityTests' = 9
    'GraniteEdgeAI.UnitTests.ModelInspectionPageNavigationTests' = 39
    'GraniteEdgeAI.UnitTests.ModelInspectionFixturePresetTests' = 25
    'GraniteEdgeAI.UnitTests.OnboardingStageIndicatorTests' = 12
  })

git add -- `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/InspectionStatusGlyphKind.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/InspectionOutcomePresentation.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionStatusGlyph.xaml' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionStatusGlyph.xaml.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionOutcomeCard.xaml' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionOutcomeCard.xaml.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionPresentationFactory.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionMotion.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Observation/ModelInspectionObservedScreen.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Observation/ModelInspectionFixtureScreenObserver.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Observation/ModelInspectionFixtureScreenComparer.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/Onboarding/Controls/OnboardingStageIndicator.xaml' `
  'IBM Granite with TurboQuant (Intel)/Features/Onboarding/Controls/OnboardingStageIndicator.xaml.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionStatusGlyphTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionContentCardTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionModelCardTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionOutcomeCardTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/ModelInspectionPresentationFactoryTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/ModelInspectionMotionTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionAccessibilityTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageNavigationTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/ModelInspectionFixturePresetTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/Controls/OnboardingStageIndicatorTests.cs' `
  'docs/reviews/model-inspection-cleanup-source-files.txt' `
  'docs/reviews/model-inspection-cleanup-inventory.md'
git commit -m 'feat(model-inspection): add precision status glyphs'
```

## Task 6: Implement the Measured Checklist progress layout

**Files:**

- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/InitialInspectionProgressPresentationFactory.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionTheme.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionContentCardTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/InitialInspectionProgressPresentationTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionRenderedStateTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageLayoutTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageNavigationTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionAccessibilityTests.cs`

- [ ] **Step 1: Replace obsolete exact-height assertions with intent REDs**

Extend existing protected methods rather than adding DataRows to protected classes. At 1440x1024 require:

```csharp
Assert.InRange(HeadingToFirstRowGap(content), 14d, 16d);
Assert.IsTrue(ProgressRows(content).All(row => row.ActualHeight >= 48d));
Assert.AreEqual(4, VisibleConnectors(content).Length);
Assert.AreEqual(0d, FinalConnectorTailHeight(content), 0.5d);
Assert.InRange(ContentBottomWhitespace(content), 20d, 24d);
Assert.IsTrue(ActiveRow(content).Background is not null);
Assert.IsTrue(CancelButton(actions).ActualHeight >= 44d);
```

At the desktop/default text scale, also require each one-line row to be 48 ±1 px. At 200% text or when detail wraps, rows may grow naturally without clipping, and connectors still run centre-to-centre. Create and name a bordered completed-count chip around the current bare summary `TextBlock`; assert it shares the heading row, uses theme brushes, remains Raw while its text is Content, and retains the exact summary accessible name. Startup feedback sits below that heading, percentage text occupies trailing space without resizing the active glyph, and row identities remain fixed across every stage. Measure both startup-visible and startup-collapsed variants: heading-to-startup/startup-to-rows or heading-to-rows must each resolve to one 14–16 px gap, never two stacked gaps. Before production edits, run the exact Step 5 104-map with `ResultName 'task-06-progress-layout-red.trx'`. Expected RED: 60 px rows, bare summary text, no heading gap, and dead tail remain.

- [ ] **Step 2: Add explicit rhythm resources**

Add semantic theme tokens:

```xml
<x:Double x:Key="InspectionCardGap">16</x:Double>
<x:Double x:Key="InspectionHeaderToCardGap">24</x:Double>
<x:Double x:Key="InspectionProgressHeadingGap">16</x:Double>
<x:Double x:Key="InspectionProgressRowHeight">48</x:Double>
```

Reuse the existing `InspectionCardPadding` resource already present in `ModelInspectionTheme.xaml`; add only the four missing double resources above. Do not duplicate literal equivalents across page/card visual states.

- [ ] **Step 3: Rebuild the progress template with stable 48 px rows**

Use a header grid with title left and the new bordered completed-count chip right. Place `StartupStatusRow` in its own auto row. Apply exactly one conditional 16 px gap between the last visible header/startup element and the repeater; do not retain an unconditional items-host margin when startup is visible. Each default-scale one-line stage row has `MinHeight=48`, not fixed `Height`; wrapped detail/text-scale may grow naturally. Connectors begin at glyph centre and end at the next glyph centre for only rows 1–4. Bind an active-row surface to `IsActive`; waiting/completed rows remain transparent. Put fraction text in the trailing column and keep detail underneath the title only when genuinely present.

Remove progress `MinHeight=60`, negative connector margins, nested 22 px status badge, and standard-card minimum-height calculations for progress. Give the card 20–24 px natural bottom padding. Keep Cancel in the action card as a full-width or deliberately centred button with an effective minimum of 44 px.

- [ ] **Step 4: Replace page gap switches with one stack rhythm**

In `ModelInspectionPage.xaml`, use a single `StackPanel`/`Grid` auto-row rhythm: 24 px header-to-first-visible-card and 16 px between visible controls. Hidden controls must be `Collapsed` and contribute zero height. Remove the state-specific gap/margin switch from the existing `ApplyPageReflow` method and its reflected rendered-state oracle; retain only expanded viewport sizing where bounded scrolling is required.

- [ ] **Step 5: Verify progress geometry and commit**

```powershell
Invoke-PackagedModelInspectionTests `
  -Filter 'FullyQualifiedName~InspectionContentCardTests|FullyQualifiedName~InitialInspectionProgressPresentationTests|FullyQualifiedName~ModelInspectionRenderedStateTests|FullyQualifiedName~ModelInspectionPageLayoutTests|FullyQualifiedName~ModelInspectionPageNavigationTests|FullyQualifiedName~ModelInspectionAccessibilityTests' `
  -ResultName 'task-06-progress-layout-green.trx' `
  -ExpectedTotal 104 `
  -ExpectedClassCounts ([ordered]@{
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.InspectionContentCardTests' = 25
    'GraniteEdgeAI.UnitTests.InitialInspectionProgressPresentationTests' = 6
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual.ModelInspectionRenderedStateTests' = 21
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.ModelInspectionPageLayoutTests' = 4
    'GraniteEdgeAI.UnitTests.ModelInspectionPageNavigationTests' = 39
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual.ModelInspectionAccessibilityTests' = 9
  })

git add -- `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/InitialInspectionProgressPresentationFactory.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionTheme.xaml' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionContentCardTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/InitialInspectionProgressPresentationTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionRenderedStateTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageLayoutTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageNavigationTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionAccessibilityTests.cs'
git commit -m 'feat(model-inspection): refine measured progress layout'
```

## Task 7: Balance completed, warning, failure, and action layouts

**Files:**

- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionOutcomeCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionOutcomeCard.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionActionCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionActionCard.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionOutcomeCardTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionModelCardTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionContentCardTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionActionCardTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionRenderedStateTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageLayoutTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionAccessibilityTests.cs`

- [ ] **Step 1: Write one all-state balance RED**

Retain the 13-state and four-disclosure DataRows but replace old magic y/min-height expectations with invariants:

- every visible-card gap is 16 ±1 effective pixel;
- outcome icon column and balancing column have equal width;
- outcome title/message bounds are centred in the middle column;
- ready model title is centred while format chip remains right-aligned;
- eight desktop metadata cells have equal width, row height, and padding, with short label/value text centred;
- collapsed disclosure height equals its realized header height and has no 25 px tail;
- hidden cards have zero realized layout height;
- warning/failure/conversion/unsupported/invalid/cancelled content uses natural collapsed height;
- long findings/diagnostics remain left-aligned;
- visible action count 1/2/3 maps to centred/equal/equal columns with no empty middle slot.

Before production edits, run the exact Step 5 79-map with `ResultName 'task-07-balanced-states-red.trx'`. Expected RED: fixed minima, left-anchored outcome focus content, and static three-column action layout violate these invariants.

- [ ] **Step 2: Centre the outcome and ready-summary geometry**

Set `OutcomeFocusTarget.HorizontalContentAlignment="Stretch"`; keep symmetric fixed glyph/balance columns around the centred copy. Use the shared glyph. In the model card, reserve equal left/right column widths by measuring the visible format chip and assigning that width to both grid columns; do not create an invisible placeholder element. Assert the title centre is within one effective pixel of the card centre at every approved width and text scale. Centre short metadata labels/values and give every desktop cell equal padding.

Remove `InspectionDetailsDisclosure.MinHeight=83`; keep `HeaderMinHeight=58` only as a 44+ accessible target baseline, not a larger outer minimum. Expanded detail viewport remains bounded.

- [ ] **Step 3: Remove terminal dead space without harming long text**

Remove outer-card minimum-height mechanisms that create dead space: Outcome 82, Action 140, Model 304/156/470, and every standard/expanded minimum returned by `InspectionContentCard.GetStandardMinimumHeight`. Retain only interactive target minima and bounded expanded viewport `Height`/`MaxHeight` values for conversion output and invalid technical reports. Findings, recommended actions, and diagnostic prose use `TextAlignment.Left`; only short summary text centres.

- [ ] **Step 4: Implement count-driven action columns**

In `InspectionActionCard`, replace the independent presentation and responsive-VisualState placement rules with one idempotent `(responsiveBand, visibleActionCount)` layout operation that runs after presentation changes and width-band changes. Collect visible buttons in semantic order and use these desktop/medium placements:

```csharp
switch (visible.Count)
{
    case 1: ConfigureColumns("*", "Auto", "*"); Place(visible[0], 1); break;
    case 2: ConfigureColumns("*", "*", "0"); Place(visible[0], 0); Place(visible[1], 1); break;
    case 3: ConfigureColumns("*", "*", "*"); Place(visible[0], 0); Place(visible[1], 1); Place(visible[2], 2); break;
}
```

Use typed `GridLength` values in production rather than parsing strings. Below 600 px, the same operation produces one semantic vertical stack for 1/2/3 actions; at 600–887 and >=888 it uses equal horizontal footprints. One action has a sensible max width and remains centred; two and three actions have equal footprints and 12–16 px spacing. Disabled future actions remain visible only when the presentation declares them, with `Coming later` help unchanged. Extend `ResultActions_ReflowAtExactClientBreakpointsWithoutReplacement` to cover visible counts 1/2/3 at widths 888/887/600/599 and prove the same button identities survive every transition.

- [ ] **Step 5: Verify all terminal states and commit**

```powershell
Invoke-PackagedModelInspectionTests `
  -Filter 'FullyQualifiedName~InspectionOutcomeCardTests|FullyQualifiedName~InspectionModelCardTests|FullyQualifiedName~InspectionContentCardTests|FullyQualifiedName~InspectionActionCardTests|FullyQualifiedName~ModelInspectionRenderedStateTests|FullyQualifiedName~ModelInspectionPageLayoutTests|FullyQualifiedName~ModelInspectionAccessibilityTests' `
  -ResultName 'task-07-balanced-states-green.trx' `
  -ExpectedTotal 79 `
  -ExpectedClassCounts ([ordered]@{
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.InspectionOutcomeCardTests' = 7
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.InspectionModelCardTests' = 9
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.InspectionContentCardTests' = 25
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.InspectionActionCardTests' = 4
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual.ModelInspectionRenderedStateTests' = 21
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.ModelInspectionPageLayoutTests' = 4
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual.ModelInspectionAccessibilityTests' = 9
  })

git add -- `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionOutcomeCard.xaml' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionOutcomeCard.xaml.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionActionCard.xaml' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionActionCard.xaml.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionOutcomeCardTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionModelCardTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionContentCardTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionActionCardTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionRenderedStateTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageLayoutTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionAccessibilityTests.cs'
git commit -m 'feat(model-inspection): balance all result states'
```

## Task 8: Prove responsive, text-scale, High Contrast, focus, and reduced-motion behavior

**Files:**

- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionActionCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionActionCard.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionOutcomeCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionOutcomeCard.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionTheme.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageLayoutTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionRenderedStateTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionAccessibilityTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/ModelInspectionFixturePresetTests.cs`

- [ ] **Step 1: Add a loop-based responsive RED without changing protected execution counts**

Inside one existing rendered-state method, loop over all 13 states and widths `1440, 888, 887, 600, 599, 360`. Do not add 78 DataRows to the protected class. For each pair assert:

- host width/inset follows `840 centred`, `24 px`, then `16 px` rules;
- no visible text/control bounds overlap or clip;
- metadata is 4/2/1 columns at desktop/medium/narrow;
- actions are equal horizontal columns when they fit and a stable semantic stack under 600;
- every interactive target is at least 44x44;
- disclosure remains reachable and bounded;
- heading, reading, tab, and action order do not change;
- hidden cards measure zero.

Run each state group with the text surface that can actually dominate it: the 160-character model name and longest stage detail for Progress/Ready; the longest metadata value for both Ready disclosures; the longest finding, recommended action, and conversion path for warning/conversion/invalid collapsed and expanded pairs; and the longest outcome message, diagnostic, and action label for IncompletePackage, Unsupported, Cancelled, and OperationalFailure. Repeat the complete 13-state/four-disclosure matrix with the existing 200% preview preset. Before production edits, run the exact Step 4 188-map with `ResultName 'task-08-responsive-accessibility-red.trx'`. Expected RED: fixed minima and old action grid cause dead space or unequal/overflowing layouts.

- [ ] **Step 2: Make responsive states content-driven**

Keep breakpoints exactly `>=888`, `600–887`, `<600`. Set metadata column counts and action orientation in existing VisualStates/code-behind without replacing controls. Use natural height and wrapping; do not scale font size down to force a fit. Preserve the 840 px maximum desktop column.

- [ ] **Step 3: Complete accessibility and High Contrast contracts**

Require system-brush mappings for glyph foreground, outline, surfaces, and focus visuals in High Contrast. Verify decorative glyphs are Raw while adjacent rows/banner text are Content. Startup announces once; fractions stay silent; genuine stages announce once in semantic order; terminal announcements remain deduplicated per attempt. Startup never steals the initial heading focus, and later progress never moves whichever in-page control currently owns keyboard focus: heading if untouched, Cancel after the user tabs/clicks it, and disclosure after a toggle. Normal/reduced-motion endpoints are semantically identical, and reduced motion starts no orbit or custom transition.

- [ ] **Step 4: Run GREEN and commit**

```powershell
Invoke-PackagedModelInspectionTests `
  -Filter 'FullyQualifiedName~InspectionActionCardTests|FullyQualifiedName~InspectionContentCardTests|FullyQualifiedName~InspectionModelCardTests|FullyQualifiedName~InspectionOutcomeCardTests|FullyQualifiedName~InspectionStatusGlyphTests|FullyQualifiedName~InspectionDisclosureTests|FullyQualifiedName~ModelInspectionDisclosureTests|FullyQualifiedName~ModelInspectionPageLayoutTests|FullyQualifiedName~ModelInspectionPageNavigationTests|FullyQualifiedName~ModelInspectionRenderedStateTests|FullyQualifiedName~ModelInspectionAccessibilityTests|FullyQualifiedName~ModelInspectionFixturePresetTests|FullyQualifiedName~OnboardingStageIndicatorTests' `
  -ResultName 'task-08-responsive-accessibility-green.trx' `
  -ExpectedTotal 188 `
  -ExpectedClassCounts ([ordered]@{
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.InspectionActionCardTests' = 4
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.InspectionContentCardTests' = 25
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.InspectionModelCardTests' = 9
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.InspectionOutcomeCardTests' = 7
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.InspectionStatusGlyphTests' = 16
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.InspectionDisclosureTests' = 12
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls.ModelInspectionDisclosureTests' = 5
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.ModelInspectionPageLayoutTests' = 4
    'GraniteEdgeAI.UnitTests.ModelInspectionPageNavigationTests' = 39
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual.ModelInspectionRenderedStateTests' = 21
    'GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual.ModelInspectionAccessibilityTests' = 9
    'GraniteEdgeAI.UnitTests.ModelInspectionFixturePresetTests' = 25
    'GraniteEdgeAI.UnitTests.OnboardingStageIndicatorTests' = 12
  }) `
  -Configuration Debug

git add -- `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionActionCard.xaml' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionActionCard.xaml.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionOutcomeCard.xaml' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionOutcomeCard.xaml.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionTheme.xaml' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageLayoutTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionRenderedStateTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionAccessibilityTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/ModelInspectionFixturePresetTests.cs'
git commit -m 'test(model-inspection): harden responsive visual polish'
```

## Task 9: Add the startup fixture and regenerate the exact fixture catalogue

**Files:**

- Create: `tests/TestFixtures/ModelInspectionScenarios/MI-050-progress-starting-secure-inspection.fixture.json`
- Modify: `tests/TestFixtures/ModelInspectionScenarios/model-inspection-fixture.schema.json`
- Modify: `tests/TestFixtures/ModelInspectionScenarios/model-inspection-fixture-coverage-policy.json`
- Modify: `shared/GraniteEdgeAI.ModelInspection.Fixtures/ModelInspectionFixtureCoverageValidator.cs`
- Modify: `shared/GraniteEdgeAI.ModelInspection.Fixtures/ModelInspectionFixtureReportGenerator.cs`
- Modify: `shared/GraniteEdgeAI.ModelInspection.Fixtures/ModelInspectionFixtureDescriptor.cs`
- Modify: `shared/GraniteEdgeAI.ModelInspection.Fixtures/ModelInspectionFixtureValidator.cs`
- Modify: `shared/GraniteEdgeAI.ModelInspection.Fixtures/StrictModelInspectionFixtureJson.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Gallery/ModelInspectionFixturePackageLoader.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Gallery/ModelInspectionFixtureScenarioRunner.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Runtime/DebugModelInspectionService.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Runtime/ModelInspectionFixtureSession.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Runtime/ModelInspectionFixtureSessionEvidence.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Observation/ModelInspectionObservedScreen.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Observation/ModelInspectionFixtureScreenObserver.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Observation/ModelInspectionFixtureScreenComparer.cs`
- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/ModelInspectionFixtureBuildBoundaryContractTests.cs`
- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/ModelInspectionFixtureCatalogueContractTests.cs`
- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/ModelInspectionFixtureReportContractTests.cs`
- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/ModelInspectionFixtureStressBatchContractTests.cs`
- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/ModelInspectionFixtureWorkflowContractTests.cs`
- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/ModelInspectionFixtureValidationContractTests.cs`
- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/ModelInspectionFixtureJsonContractTests.cs`
- Modify: `scripts/model-inspection/Test-ModelInspectionFixtureReleaseIsolation.ps1`
- Create: `scripts/model-inspection/Generate-ModelInspectionFixtureReport.ps1`
- Modify: `docs/evidence/testing/Model-Inspection-Fixture-Catalog.md`
- Modify: `tests/TestFixtures/README.md`
- Modify: `tests/TestFixtures/ModelInspectionScenarios/README.md`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/DebugModelInspectionServiceTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/ModelInspectionFixtureAdapterTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/ModelInspectionFixtureGalleryTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/ModelInspectionFixtureScreenContractTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/ModelInspectionFixtureInteractionTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/ModelInspectionFixtureLifetimeTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/ModelInspectionFixtureViewModelIntegrationTests.cs`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

- [ ] **Step 1: Write the MI-050 RED in existing catalogue/fixture methods**

Require IDs exactly `MI-001` through `MI-050`, 50 unique policy rows, 50 semantic hashes, and 52 physical JSON files (schema + policy + 50 descriptors). Extend only existing loop/mutation methods and DataRows throughout Task 9, preserving the full Contracts total at 357, report class at 11, non-report fixture-contract map at 179, and Debug category at 220. If a new discovered execution is genuinely unavoidable, stop and revise this plan before running any pinned command. Build MI-050 from the complete schema-valid MI-014 descriptor rather than inventing a short-form document. Change `targetCondition` to `progress-starting-secure-inspection`; keep `figmaStates: ["inspectionProgress"]` but use empty `stages` and `stageStatuses`; give attempt 1 one unreleased `checkpoint: "terminal"` service step whose effect is the normal Ready completion; make `setupSteps` contain only `observe`; and keep `observationCheckpoint: "observed"`. This held checkpoint leaves exactly one service call and cancellation registration active without a worker-progress fact.

Declare `category: "progress"`, preset `P01`, empty semantic stage/status coverage, and exact interactions `cancel` plus `reset`; add an explicit ordinal-50 Progress special case to category/interaction coverage so it cannot fall through to Stress. The full `expected` record must retain exact Figma, model, actions, footer, focus, automation, announcements, rows/scroll, retained identities, presets, and copy keys. Its startup region is visible with `Starting secure inspection…`; all five counted rows are Waiting; fraction is absent; footer is InProgress; Cancel is enabled; focus remains on the page heading; startup announces exactly once; Reset retains its authored behavior; and no sixth stage exists. Before implementation, run the Step 5 non-report 179-map with `task-09-fixture-contracts-without-report-red.trx`; the report class is intentionally excluded while its checked-in bytes still describe 49 fixtures. Expected RED: MI-050 is absent and every exact-49 closure rejects it.

- [ ] **Step 2: Extend schema/model only for startup facts**

Add bounded optional fields for startup status/visibility/active state to the expected progress screen. Validate that startup may occur only with `inspectionProgress`, zero completed stages, zero active counted stages, five waiting stages, exactly one active service attempt, no released progress checkpoint, and no terminal. It is not a sixth stage and cannot carry a fraction. Mutation tests must independently reject the wrong Figma state, no active attempt, any active/completed stage, and any terminal.

- [ ] **Step 3: Register exact packaging, policy, and semantic hashes**

Add MI-050 to both project `Content` item groups using the existing logical Link and short `Fixtures\...` TargetPath convention. Add its filename to the package loader, coverage validator manifest, policy tags, and freshly canonicalized uppercase semantic SHA-256. Update every exact `49`, `MI-001..MI-049`, stress-batch partition, catalogue/report loop, and package-closure assertion to 50 only where it describes the full catalogue; preserve historical counts in evidence prose.

- [ ] **Step 4: Update expected screens and prepare the checked-in generator path**

Update expected geometry for all 13 states and disclosure pairs to the new rhythm/glyph observations. Add checked-in `Generate-ModelInspectionFixtureReport.ps1`: it rejects output paths inside the repository, sets a required temporary output-path environment variable, and invokes the existing exact report-contract method once through the Contracts executable. In explicit generator mode, that method writes `ModelInspectionFixtureReportGenerator.GenerateUtf8` bytes to the temporary path, validates their structure/hash, and returns without comparing the still-old checked-in report; normal test mode retains strict byte equality. Do not copy or stamp `Verified` yet.

- [ ] **Step 5: Prove the non-report fixture contracts and serialized Debug fixture campaign**

```powershell
$fixtureContractResults = '.\TestResults\ModelInspectionPolish\Contracts\task-09-fixture-contracts-without-report'
if (Test-Path -LiteralPath $fixtureContractResults) { throw 'Fixture contract results path is not fresh.' }
dotnet test '.\tests\ContractTests\GraniteEdgeAI.ModelInspection.Contracts.Tests\GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj' `
  --configuration Release `
  --filter 'FullyQualifiedName~ModelInspectionFixture&FullyQualifiedName!~ModelInspectionFixtureReportContractTests' `
  --minimum-expected-tests 179 `
  --results-directory $fixtureContractResults `
  --report-trx `
  --report-trx-filename 'task-09-fixture-contracts-without-report-green.trx'
if ($LASTEXITCODE -ne 0) { throw 'Fixture contracts failed.' }
Assert-ModelInspectionTrx `
  -Path (Join-Path $fixtureContractResults 'task-09-fixture-contracts-without-report-green.trx') `
  -ExpectedTotal 179 `
  -ExpectedClassCounts ([ordered]@{
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureBuildBoundaryContractTests' = 53
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureCatalogueContractTests' = 32
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureJsonContractTests' = 16
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureLifecycleBatchContractTests' = 3
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureOperationalFailureBatchContractTests' = 3
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureRetryRestartContractTests' = 27
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureStressBatchContractTests' = 6
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureValidationContractTests' = 37
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureWorkflowContractTests' = 2
  })

Invoke-PackagedModelInspectionTests `
  -Filter 'TestCategory=ModelInspectionFixtureGallery' `
  -ResultName 'task-09-fixtures-green.trx' `
  -ExpectedTotal 220 `
  -ExpectedClassCounts ([ordered]@{
    'GraniteEdgeAI.UnitTests.DebugModelInspectionServiceTests' = 41
    'GraniteEdgeAI.UnitTests.ModelInspectionFixtureAdapterTests' = 12
    'GraniteEdgeAI.UnitTests.ModelInspectionFixtureGalleryTests' = 73
    'GraniteEdgeAI.UnitTests.ModelInspectionFixtureInteractionTests' = 6
    'GraniteEdgeAI.UnitTests.ModelInspectionFixtureLifetimeTests' = 13
    'GraniteEdgeAI.UnitTests.ModelInspectionFixturePageLifecycleTests' = 7
    'GraniteEdgeAI.UnitTests.ModelInspectionFixturePresetTests' = 25
    'GraniteEdgeAI.UnitTests.ModelInspectionFixtureScreenContractTests' = 37
    'GraniteEdgeAI.UnitTests.ModelInspectionFixtureViewModelIntegrationTests' = 6
  }) `
  -Configuration Debug
```

Require exact all-pass TRX counts for every class, zero WER events, and zero worker/test/app orphans. The existing screen-contract loop must compare MI-050's complete loaded surface, while the existing service, Interaction, and Lifetime loops prove its one pending call/registration, Cancel/Reset controls, exact focus/footer/announcement behavior, and idempotent retirement. Only after those assertions pass may the report call MI-050 Verified.

- [ ] **Step 6: Generate the report, prove normal byte equality, then run full Contracts**

After Step 5 is GREEN, run the checked-in generator to a unique external temp path, inspect the exact diff, intentionally replace the catalogue, run the normal report-contract mode, and only then run the full Contracts suite. The generator script must require `-OutputPath`, resolve it outside the repository root, build/run the Release Contracts executable in explicit generator mode, require exactly one generated UTF-8 file, and leave the repository untouched. Only this post-proof generation may emit MI-050 as Verified.

```powershell
$repoRoot = (Resolve-Path '.').Path
$reportTempRoot = Join-Path ([IO.Path]::GetTempPath()) ("model-inspection-report-$([guid]::NewGuid().ToString('N'))")
$generatedReport = Join-Path $reportTempRoot 'Model-Inspection-Fixture-Catalog.md'
New-Item -ItemType Directory -Path $reportTempRoot | Out-Null
& powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File '.\scripts\model-inspection\Generate-ModelInspectionFixtureReport.ps1' `
  -OutputPath $generatedReport
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $generatedReport -PathType Leaf)) {
  throw 'External fixture-report generation failed.'
}

git diff --no-index -- '.\docs\evidence\testing\Model-Inspection-Fixture-Catalog.md' $generatedReport
if ($LASTEXITCODE -notin @(0, 1)) { throw 'Generated report diff failed.' }
# Review the one intentional 49 -> 50 catalogue diff before this copy.
Copy-Item -LiteralPath $generatedReport `
  -Destination '.\docs\evidence\testing\Model-Inspection-Fixture-Catalog.md'

$reportResults = '.\TestResults\ModelInspectionPolish\Contracts\task-09-report-contracts'
if (Test-Path -LiteralPath $reportResults) { throw 'Report results path is not fresh.' }
dotnet test '.\tests\ContractTests\GraniteEdgeAI.ModelInspection.Contracts.Tests\GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj' `
  --configuration Release `
  --filter 'FullyQualifiedName~ModelInspectionFixtureReportContractTests' `
  --minimum-expected-tests 11 `
  --results-directory $reportResults `
  --report-trx `
  --report-trx-filename 'task-09-report-contracts-green.trx'
if ($LASTEXITCODE -ne 0) { throw 'Normal report contracts failed.' }
Assert-ModelInspectionTrx `
  -Path (Join-Path $reportResults 'task-09-report-contracts-green.trx') `
  -ExpectedTotal 11 `
  -ExpectedClassCounts ([ordered]@{ 'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureReportContractTests' = 11 })

$fullContractResults = '.\TestResults\ModelInspectionPolish\Contracts\task-09-full-contracts'
if (Test-Path -LiteralPath $fullContractResults) { throw 'Full contract results path is not fresh.' }
dotnet test '.\tests\ContractTests\GraniteEdgeAI.ModelInspection.Contracts.Tests\GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj' `
  --configuration Release `
  --minimum-expected-tests 357 `
  --results-directory $fullContractResults `
  --report-trx `
  --report-trx-filename 'task-09-full-contracts-green.trx'
if ($LASTEXITCODE -ne 0) { throw 'Full Contracts gate failed.' }
Assert-ModelInspectionTrx `
  -Path (Join-Path $fullContractResults 'task-09-full-contracts-green.trx') `
  -ExpectedTotal 357 `
  -ExpectedClassCounts ([ordered]@{
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.BuildWorkflowContractTests' = 16
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.CleanupInventoryContractTests' = 3
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ContractGraphTests' = 1
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.Gate2ArchitectureFitnessTests' = 5
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.Gate2ProjectGraphTests' = 5
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.Gate4PackagingContractTests' = 4
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.Gate5ApplicationBoundaryContractTests' = 6
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureBuildBoundaryContractTests' = 53
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureCatalogueContractTests' = 32
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureJsonContractTests' = 16
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureLifecycleBatchContractTests' = 3
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureOperationalFailureBatchContractTests' = 3
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureReportContractTests' = 11
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureRetryRestartContractTests' = 27
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureStressBatchContractTests' = 6
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureValidationContractTests' = 37
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureWorkflowContractTests' = 2
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionVisualSourceContractTests' = 8
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.Protocol.WorkerCommandSequenceValidatorTests' = 9
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.Protocol.WorkerEvidenceValidationTests' = 41
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.Protocol.WorkerMessageSequenceValidatorTests' = 17
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.Protocol.WorkerProtocolJsonTests' = 19
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.Protocol.WorkerProtocolTests' = 33
  })

git add -- `
  'tests/TestFixtures/ModelInspectionScenarios/MI-050-progress-starting-secure-inspection.fixture.json' `
  'tests/TestFixtures/ModelInspectionScenarios/model-inspection-fixture.schema.json' `
  'tests/TestFixtures/ModelInspectionScenarios/model-inspection-fixture-coverage-policy.json' `
  'shared/GraniteEdgeAI.ModelInspection.Fixtures/ModelInspectionFixtureCoverageValidator.cs' `
  'shared/GraniteEdgeAI.ModelInspection.Fixtures/ModelInspectionFixtureReportGenerator.cs' `
  'shared/GraniteEdgeAI.ModelInspection.Fixtures/ModelInspectionFixtureDescriptor.cs' `
  'shared/GraniteEdgeAI.ModelInspection.Fixtures/ModelInspectionFixtureValidator.cs' `
  'shared/GraniteEdgeAI.ModelInspection.Fixtures/StrictModelInspectionFixtureJson.cs' `
  'IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Gallery/ModelInspectionFixturePackageLoader.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Gallery/ModelInspectionFixtureScenarioRunner.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Runtime/DebugModelInspectionService.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Runtime/ModelInspectionFixtureSession.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Runtime/ModelInspectionFixtureSessionEvidence.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Observation/ModelInspectionObservedScreen.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Observation/ModelInspectionFixtureScreenObserver.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/DebugFixtures/Observation/ModelInspectionFixtureScreenComparer.cs' `
  'tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/ModelInspectionFixtureBuildBoundaryContractTests.cs' `
  'tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/ModelInspectionFixtureCatalogueContractTests.cs' `
  'tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/ModelInspectionFixtureReportContractTests.cs' `
  'tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/ModelInspectionFixtureStressBatchContractTests.cs' `
  'tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/ModelInspectionFixtureWorkflowContractTests.cs' `
  'tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/ModelInspectionFixtureValidationContractTests.cs' `
  'tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/ModelInspectionFixtureJsonContractTests.cs' `
  'scripts/model-inspection/Test-ModelInspectionFixtureReleaseIsolation.ps1' `
  'scripts/model-inspection/Generate-ModelInspectionFixtureReport.ps1' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/DebugModelInspectionServiceTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/ModelInspectionFixtureAdapterTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/ModelInspectionFixtureGalleryTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/ModelInspectionFixtureScreenContractTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/ModelInspectionFixtureInteractionTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/ModelInspectionFixtureLifetimeTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/DebugFixtures/ModelInspectionFixtureViewModelIntegrationTests.cs' `
  'docs/evidence/testing/Model-Inspection-Fixture-Catalog.md' `
  'tests/TestFixtures/README.md' `
  'tests/TestFixtures/ModelInspectionScenarios/README.md' `
  'docs/reviews/model-inspection-cleanup-source-files.txt' `
  'docs/reviews/model-inspection-cleanup-inventory.md'
git commit -m 'test(model-inspection): add secure-start fixture coverage'
```

## Task 10: Add fail-closed policy guards and reconcile measured workflow counts

**Files:**

- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/ModelInspectionVisualSourceContractTests.cs`
- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/BuildWorkflowContractTests.cs`
- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/ModelInspectionFixtureWorkflowContractTests.cs`
- Modify: `.github/workflows/build-and-test.yml`
- Create: `scripts/model-inspection/Invoke-ModelInspectionProgressPolishGate.ps1`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

- [ ] **Step 1: Write mutation REDs for the new architecture**

Extend existing contract methods without adding discovered methods or DataRows, preserving the exact 357-class map. Before production/workflow edits, run the exact Step 4 command with `task-10-contracts-red.trx`; require a behavioral mutation failure, not a stale compile. The source validator must reject:

- `Task.Delay`, `Thread.Sleep`, `System.Threading.Timer`, or `DispatcherQueueTimer` under runtime/worker/service progress paths; the scan is syntax/path aware, ignores comments and strings, and allows `DispatcherQueueTimer` only in the exact app file `Presentation/DispatcherQueueModelInspectionMilestoneScheduler.cs`;
- a sequencer duration other than exactly 550 ms;
- a scheduler outside the app Presentation directory;
- terminal safety paths routed through dwell scheduling;
- a determinate active `ProgressRing` or a fraction bound to active-glyph motion;
- stock `SymbolIcon`, `FontIcon`, or Unicode success/warning/error/information/waiting/not-complete/active glyphs in affected Content, Model, Outcome, Disclosure, and footer surfaces, while explicitly allowing the non-status disclosure chevron;
- reintroduction of state-specific card-gap literals or `InspectionDetailsDisclosure.MinHeight=83`.

The workflow contract must also parse the new gate script and require all four phase names, fresh/ignored RunRoot rejection, first-error termination, exact runtime/worker/Debug/Release/N-001/isolation commands, TRX identity/adverse parsing, and final process/WER/source-hash checks. Mutation-test removal or weakening of each phase boundary.

Allow `DispatcherQueueTimer` only in `DispatcherQueueModelInspectionMilestoneScheduler.cs`. Mutation-test each rule against an in-memory source copy so a comment cannot satisfy the contract.

- [ ] **Step 2: Register every new source immediately and verify cleanup shape**

Confirm the already-registered design/plan remain present, then insert every newly created startup/barrier, scheduler, glyph/test, runtime-test, generator/gate-script, and MI-050 path in Ordinal order in the same task that creates it. Update the inventory header and exact self-rows to the freshly measured count. Run:

```powershell
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File '.\scripts\model-inspection\Verify-ModelInspectionCleanupInventory.ps1'
```

Require 3/3, exact path equality, unique/sorted lists, no missing files, and 12 pipes in every inventory row.

- [ ] **Step 3: Measure instead of guessing all test totals**

Before measuring, create `Invoke-ModelInspectionProgressPolishGate.ps1` with this fail-closed interface:

```powershell
param(
  [Parameter(Mandatory)]
  [ValidateSet('RuntimeWorker','Debug','Release','FinalSource')]
  [string] $Phase,

  [Parameter(Mandatory)]
  [string] $RunRoot
)
```

The script rejects a pre-existing/non-ignored RunRoot, freezes HEAD/status/source hashes, runs each command at most once, stops at the first nonzero exit or TRX/map mismatch, and never edits source. Every phase writes command/log/TRX/hash/process/WER metadata beneath RunRoot and requires zero relevant orphan processes at both boundaries. `RuntimeWorker` runs the full runtime-spike and worker projects and enforces the protected 67/67 engine class. `Debug` performs one Debug/x64 build, then exact Interaction+Lifetime 19/19, exact nine-class fixture category 220/220, and the focused polish-class campaign with freshly measured exact totals. `Release` executes the permanent workflow restore/build/filter, exact protected map, exact N-001 FQN, and one unique-path release-isolation invocation. `FinalSource` runs exact Contracts, cleanup 3/3, diff/conflict checks, and release isolation against the final commit. Keep all measured totals in one ordered map shared with the workflow contracts; no console-summary-only validation is accepted.

Run one full Debug fixture category TRX, one full Release hosted-equivalent TRX, one full Contracts TRX, one full worker-project TRX, and one full `tools/ModelInspection.LlamaSharpSpike.Tests` runtime TRX. Parse exact class definitions/results and allow only deltas caused by this plan:

- new unprotected `ModelInspectionMilestoneSequencerTests` and `InspectionStatusGlyphTests` classes;
- unchanged protected execution counts unless an existing DataRow was deliberately added;
- runtime-spike total increase from `VocabOnlyProbeStageBoundaryTests` with every pre-existing runtime class still present/all-pass;
- worker total changes only for edits in the worker test project, while protected `LlamaSharpInspectionEngineTests` remains exactly 67/67;
- fixture catalogue grows to 50 descriptors, but fixture test execution counts change only if the existing tests are data-driven per descriptor.

Update workflow total/class literals and both workflow contracts to those parsed values. Recompute the hosted packaged-step SHA after the count edits. Retain the exact Release category filter, floor `686`, full checkout, raw-TRX deletion, and Release-isolation ordering.

- [ ] **Step 4: Run the complete Contracts gate and commit governance**

```powershell
$contractResults = '.\TestResults\ModelInspectionPolish\Contracts\task-10-contracts'
if (Test-Path -LiteralPath $contractResults) { throw 'Contracts results path is not fresh.' }
dotnet test '.\tests\ContractTests\GraniteEdgeAI.ModelInspection.Contracts.Tests\GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj' `
  --configuration Release `
  --minimum-expected-tests 357 `
  --results-directory $contractResults `
  --report-trx `
  --report-trx-filename 'task-10-contracts.trx'
if ($LASTEXITCODE -ne 0) { throw 'Full Contracts gate failed.' }
Assert-ModelInspectionTrx `
  -Path (Join-Path $contractResults 'task-10-contracts.trx') `
  -ExpectedTotal 357 `
  -ExpectedClassCounts ([ordered]@{
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.BuildWorkflowContractTests' = 16
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.CleanupInventoryContractTests' = 3
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ContractGraphTests' = 1
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.Gate2ArchitectureFitnessTests' = 5
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.Gate2ProjectGraphTests' = 5
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.Gate4PackagingContractTests' = 4
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.Gate5ApplicationBoundaryContractTests' = 6
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureBuildBoundaryContractTests' = 53
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureCatalogueContractTests' = 32
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureJsonContractTests' = 16
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureLifecycleBatchContractTests' = 3
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureOperationalFailureBatchContractTests' = 3
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureReportContractTests' = 11
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureRetryRestartContractTests' = 27
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureStressBatchContractTests' = 6
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureValidationContractTests' = 37
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionFixtureWorkflowContractTests' = 2
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.ModelInspectionVisualSourceContractTests' = 8
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.Protocol.WorkerCommandSequenceValidatorTests' = 9
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.Protocol.WorkerEvidenceValidationTests' = 41
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.Protocol.WorkerMessageSequenceValidatorTests' = 17
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.Protocol.WorkerProtocolJsonTests' = 19
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.Protocol.WorkerProtocolTests' = 33
  })

git add -- `
  '.github/workflows/build-and-test.yml' `
  'tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/ModelInspectionVisualSourceContractTests.cs' `
  'tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/BuildWorkflowContractTests.cs' `
  'tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/ModelInspectionFixtureWorkflowContractTests.cs' `
  'scripts/model-inspection/Invoke-ModelInspectionProgressPolishGate.ps1' `
  'docs/reviews/model-inspection-cleanup-source-files.txt' `
  'docs/reviews/model-inspection-cleanup-inventory.md'
git commit -m 'test(model-inspection): gate truthful visual pacing'
```

Task 10 changes existing contract methods rather than adding discovered methods, so 357 is expected to remain exact. If a new method is genuinely unavoidable, stop before this command, measure the complete exact per-class map, update this command/workflow/contracts together, and obtain review; never lower a count or retain a stale floor.

## Task 11: Update current documentation and perform the final verification ladder

**Files:**

- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/README.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/README.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/README.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Runtime/README.md`
- Modify: `workers/GraniteEdgeAI.ModelInspection.Worker/README.md`
- Modify: `tests/README.md`
- Modify: `docs/testing/Model-Inspection-Test-Completeness-Matrix.md`
- Modify: `docs/evidence/testing/Model-Inspection-Figma-Visual-Verification.md`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

- [ ] **Step 1: Document current behavior before the evidence ladder**

Record:

- immediate `Starting secure inspection…` feedback while secure verification/launch continues;
- the exact five real operation boundaries;
- UI-only 550 ms minimum and immediate safety/reduced-motion paths;
- Precision Orbit and shared vector glyph semantics;
- 24/16 px rhythm, 48 px progress rows, Balanced Centre terminal layout, and responsive action counts;
- MI-050 and the 50-fixture catalogue;
- no Hardware Inspection implementation in this branch;
- no strict Figma pixel, real Narrator, or controlled-OS claim without fresh controlled evidence.

Preserve historical evidence paragraphs and label them historical rather than rewriting their old counts. Do not add fresh totals or hashes yet.

- [ ] **Step 2: Commit the behavior documentation before testing it**

```powershell
git add -- `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/README.md' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/README.md' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/README.md' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Runtime/README.md' `
  'workers/GraniteEdgeAI.ModelInspection.Worker/README.md' `
  'tests/README.md' `
  'docs/testing/Model-Inspection-Test-Completeness-Matrix.md' `
  'docs/evidence/testing/Model-Inspection-Figma-Visual-Verification.md' `
  'docs/reviews/model-inspection-cleanup-inventory.md'
git commit -m 'docs(model-inspection): document polished progress behavior'
```

- [ ] **Step 3: Run the serialized runtime, worker, Debug, and Release phases**

```powershell
$stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
$gateBase = ".\TestResults\ModelInspectionPolish\Final-$stamp"

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File '.\scripts\model-inspection\Invoke-ModelInspectionProgressPolishGate.ps1' `
  -Phase RuntimeWorker -RunRoot "$gateBase\RuntimeWorker"
if ($LASTEXITCODE -ne 0) { throw 'Runtime/worker phase RED.' }

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File '.\scripts\model-inspection\Invoke-ModelInspectionProgressPolishGate.ps1' `
  -Phase Debug -RunRoot "$gateBase\Debug"
if ($LASTEXITCODE -ne 0) { throw 'Debug phase RED.' }

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File '.\scripts\model-inspection\Invoke-ModelInspectionProgressPolishGate.ps1' `
  -Phase Release -RunRoot "$gateBase\Release"
if ($LASTEXITCODE -ne 0) { throw 'Release phase RED.' }
```

`RuntimeWorker` must include the full runtime-spike project and full worker project. `Debug` must include the exact Interaction+Lifetime pair, all nine fixture category classes, and every startup/sequencer/glyph/control/layout/disclosure/navigation/motion/accessibility class. `Release` must use the workflow's exact app/test restore/build flags, two-category exclusion filter, measured protected 31-class map, exact FQN `GraniteEdgeAI.UnitTests.ModelInspectionPageNavigationTests.PackagedN001_PageJourneyCompletesAllFiveStagesAsReady`, and one unique evidence path for `Test-ModelInspectionFixtureReleaseIsolation.ps1`. Stop at the first RED with no edit/rerun in that run root. Record TRX SHA-256, totals, definitions/results, adverse counters, recipe/binary hashes, WER delta, source freeze, and final process zero.

- [ ] **Step 4: Reconcile measured evidence, then commit it**

Update only the evidence/count/hash paragraphs in the files listed above from the freshly parsed run-root artifacts. Preserve the raw TRX/logs outside tracked source and mark them do-not-stage/upload. Re-run `git diff --check`, review the exact docs-only diff, and commit those exact files with:

```powershell
git add -- `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/README.md' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/README.md' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/README.md' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Runtime/README.md' `
  'workers/GraniteEdgeAI.ModelInspection.Worker/README.md' `
  'tests/README.md' `
  'docs/testing/Model-Inspection-Test-Completeness-Matrix.md' `
  'docs/evidence/testing/Model-Inspection-Figma-Visual-Verification.md' `
  'docs/reviews/model-inspection-cleanup-inventory.md'
git commit -m 'docs(model-inspection): seal polished progress evidence'
```

- [ ] **Step 5: Validate the final committed source snapshot**

Because Step 4 changes tracked files after the main Release run, validate that exact final commit:

```powershell
$finalStamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File '.\scripts\model-inspection\Invoke-ModelInspectionProgressPolishGate.ps1' `
  -Phase FinalSource `
  -RunRoot ".\TestResults\ModelInspectionPolish\FinalSource-$finalStamp"
if ($LASTEXITCODE -ne 0) { throw 'Final-source phase RED.' }
```

Require exact Contracts all-pass, cleanup 3/3, no conflict/diff-check errors, source/hash stability, one final release-isolation pass whose `sourceCommit` equals the final commit, zero WER target crashes, and zero relevant processes. Do not edit tracked source afterward.

- [ ] **Step 6: Perform the manual visual acceptance without changing source**

First open the root `.slnx` in Visual Studio, select Debug/x64 and the Package profile, run Rebuild Solution, Deploy, and F5; require no unknown mapping/missing-content/deploy error and confirm the visible Fixture gallery entry. Then use a real multi-gigabyte GGUF only for immediate startup feedback, unchanged Precision Orbit cadence, perceivable real Stage 1–5 pacing, no Stage 2 mode switch, and its genuine terminal. Separately use the Debug gallery to inspect Ready, ReadyWithWarnings, ConversionRequired, IncompletePackage, Unsupported, Invalid, Cancelled, and OperationalFailure states, including all four disclosure pairs and narrow/200% presets. Manual perception supplements but never replaces the automated semantic gates.

## Completion checklist

- [ ] First active frame shows `Starting secure inspection…`; five rows still wait; no sixth stage exists.
- [ ] Probe Active/Completed facts surround real work and public worker progress stays five ordered stages.
- [ ] Every genuine stage is visible for at least 550 ms from its presented acknowledgement in normal motion, with no added dwell once genuine Active time already reached 550 ms; safety/reduced-motion paths are immediate.
- [ ] Fractions update only restrained text and never restart or reshape Precision Orbit.
- [ ] Shared vector glyphs replace every affected font/Unicode status mark and remain centred in High Contrast.
- [ ] Progress has one 14–16 px heading/startup rhythm, five 48 px default-scale rows that grow naturally under wrapping/text scale, four centre-to-centre connectors, and no dead tail.
- [ ] All 13 terminal/progress states and four disclosure pairs use natural height, 16 px visible-card gaps, and balanced 1/2/3-action layouts.
- [ ] Responsive, 200% preview, focus, keyboard, announcement, reduced-motion, and 44x44 target contracts pass.
- [ ] MI-050 and all 50 fixture descriptors pass schema, policy, screen, interaction/lifetime, report, and packaging gates.
- [ ] Worker, Debug fixture, packaged Release, N-001, Contracts, cleanup, and Release isolation gates are freshly green.
- [ ] Normal branch opens, builds, deploys, and debugs through the root `.slnx` using Debug/x64 Package.
- [ ] Hardware Inspection remains untouched and ready for its own approved design/plan next.
