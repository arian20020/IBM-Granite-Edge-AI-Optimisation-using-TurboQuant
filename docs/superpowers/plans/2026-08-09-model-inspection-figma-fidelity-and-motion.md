# Model Inspection Figma Fidelity and Motion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the existing x64 GGUF llama.cpp Model Inspection journey reproduce the approved 13-state Figma design, expose its four details disclosures, and render truthful progress smoothly without changing the worker, classifier policy, or downstream feature scope.

**Architecture:** Keep the existing worker/service/classifier boundary. Replace separately sampled ViewModel properties with one immutable generation/revision snapshot; map that snapshot deterministically to one of 13 presentation states; retain one page/control tree and five stable progress rows; coalesce UI-thread rendering and apply changed regions only; keep disclosure interaction and animation generations page-owned; and verify semantics on ordinary CI while reserving strict pixel-golden comparison for a preflighted Windows visual environment.

**Tech Stack:** C# 12; .NET 8; WinUI 3 / Windows App SDK 2.2; XAML compiled bindings; Windows Composition and `UISettings.AnimationsEnabled`; packaged MSTest/AppContainer tests through Visual Studio `vstest.console.exe`; PowerShell 5.1/7; official Inter 4.1 under SIL OFL 1.1; Figma node exports and the supplied flattened SVG.

---

## Source of truth

- Approved design: `docs/superpowers/specs/2026-08-09-model-inspection-figma-fidelity-and-motion-design.md`.
- Approved Figma file: `gAmBX1DYh71hqxHVqiivus`, board `142:2148`.
- State nodes: `142:2151`, `142:2213`, `142:2280`, `142:2403`, `142:2476`, `142:2599`, `142:2664`, `142:2787`, `142:2851`, `142:2910`, `142:2973`, `142:3096`, `142:3154`.
- Supplied flattened SVG SHA-256: `8A171A3A1DF66D158990789A368C439752EBE5309B364A870E0C0107B519B7EB`.
- Functional implementation base: `e5e3f6cfaa0744fab32aa57eafca750faf1f1876`.
- Approved design checkpoint: `53def11`.

## Non-negotiable constraints

- Scope stays on the production x64 GGUF, CPU-only, LLamaSharp/llama.cpp VocabOnly path.
- Do not implement OpenVINO, TurboQuant, Vulkan/GPU, full inference, context creation, Hardware Fit, conversion execution, report export, or performance/quality benchmarking.
- Do not modify worker protocol, process containment, native runtime, model-integrity precedence, classifier outcome policy, or request navigation semantics for visual work.
- Never delay or fabricate worker progress. When several truthful events arrive before one compositor frame, render the newest truthful state.
- Figma sample values are layout examples. Production displays only validated request/result/evidence values or fixed `Not reported` fallbacks.
- Preserve active Cancel, Retry, Restart, Choose another model, and inline disclosure behavior. Unimplemented future actions remain visible, disabled, and expose `Coming later` help.
- One polite progress live region and one assertive terminal live region are authoritative. A terminal is announced once per `(AttemptGeneration, terminal outcome)`.
- Every new or renamed file under the Model Inspection discovery scope is added immediately to both cleanup registers before the next full Contracts run.
- No commit may claim strict pixel fidelity from an unconstrained hosted runner. The standard hosted job gates semantics, effective-pixel geometry, tokens, accessibility, and render smoke.
- Tests use deterministic dispatchers/drivers and state endpoints. No correctness assertion uses a stopwatch or `Task.Delay` for coordination.

## Reusable packaged-test command

Define the following function once in the active PowerShell session, then call
it with the exact filter and result filename named in each task.

```powershell
$ErrorActionPreference = 'Stop'
function Invoke-PackagedModelInspectionTests {
  param(
    [string] $TestFilter = '',
    [Parameter(Mandatory)] [string] $ResultName
  )

  $testProject = '.\tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj'
  $configuration = 'Release'
  $resultDirectory = '.\TestResults\ModelInspectionFigma\Release'
  $recipe = '.\tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe'

  dotnet restore $testProject --runtime win-x64 -p:Platform=x64
  if ($LASTEXITCODE -ne 0) { throw 'Packaged test restore failed.' }
  dotnet build $testProject --configuration $configuration --no-restore --runtime win-x64 -p:Platform=x64
  if ($LASTEXITCODE -ne 0) { throw 'Packaged test build failed.' }

  New-Item -ItemType Directory -Force -Path $resultDirectory | Out-Null
  $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
  $vstest = & $vswhere -latest -products * -find '**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' | Select-Object -First 1
  if (-not $vstest) { throw 'Visual Studio app-container test runner was not found.' }

  $arguments = @(
    (Resolve-Path -LiteralPath $recipe).Path
    '/Platform:x64'
    "/Logger:trx;LogFileName=$ResultName"
    "/ResultsDirectory:$((Resolve-Path -LiteralPath $resultDirectory).Path)"
  )
  if ($TestFilter.Length -gt 0) {
    $arguments += "/TestCaseFilter:$TestFilter"
  }

  & $vstest @arguments
  if ($LASTEXITCODE -ne 0) {
    throw "Packaged test run failed: $ResultName"
  }
}
```

For every run, parse the TRX rather than trusting truncated console output. Require `total == executed == passed`, with `failed == error == timeout == aborted == inconclusive == notExecuted == 0`.

---

## Required RED checkpoints

In every task, write the named test before production code and invoke the same
focused filter later shown in that task's GREEN step, changing only the result
name to `task-NN-...-red.trx`. A missing-type compile failure is valid RED and
must be retained in the task log; once the test project compiles, at least one
named behavior assertion must fail for the expected reason before GREEN. Do not
accept a test that was already green against the parent commit.

| Task | Required RED anchor | Expected parent failure |
|---|---|---|
| 1 | `ModelInspectionAssetContractTests.ThemeResources_AreSharedAndPackaged`; `ModelInspectionVisualSourceContractTests.SuppliedBoard_MatchesApprovedIdentity` | assets/dictionary/source file absent |
| 2 | `ModelInspectionDisplayTextPolicyTests.PathShapedOrControlBearingText_UsesSafeFallback` | safe projector absent |
| 3 | `ModelInspectionViewSnapshotTests.StartProgressCancelTerminal_PublishesOneAtomicSnapshotPerSemanticChange` | snapshot/key absent |
| 4 | `ModelInspectionFigmaStatePresentationTests.Create_MapsAllThirteenApprovedStates` | enum/unified state map absent |
| 5 | `InspectionProgressRowsTests.Apply_RetainsAllFiveRowsAndUpdatesSummary` | stable owner/update absent |
| 6 | `ModelInspectionPageLayoutTests.Desktop1440_UsesApprovedCenteredGeometry` | current page/card geometry differs |
| 7 | `InspectionDisclosureTests.KeyboardToggle_RaisesPageOwnedTargetRequest`; `OnboardingStageIndicatorTests.InspectionStatus_MapsAllFourNonNavigatingStates` | reusable disclosure/footer DP absent |
| 8 | `ModelInspectionRenderCoordinatorTests.BurstNotifications_ScheduleOneNewestRender` | coordinator absent |
| 9 | `ModelInspectionMotionTests.Disclosure_UsesApprovedBidirectionalEndpoints`; `ModelInspectionMotionTests.StageActiveThenCompleted_OlderCompletionCannotRestoreActive`; `ModelInspectionMotionTests.TerminalRetry_OlderCrossfadeCannotHideNewAttempt`; `ModelInspectionDisclosureTests.RapidExpandCollapse_RejectsOlderCompletion` | motion/keyed ownership absent |
| 10 | `ModelInspectionPageNavigationTests.ProgressBurst_CoalescesAndRetainsControlIdentity` | live page still replaces whole snapshots |
| 11 | `ModelInspectionRenderHarnessTests.RenderTargetBitmap_Visible1440By1024ElementReturnsExpectedDimensions` | harness/reference package absent |
| 12 | `BuildWorkflowContractTests.VisualWorkflow_SeparatesHostedAndControlledCampaigns` | workflow guards absent |

Record the focused test count and exact failed assertion after each RED and
GREEN. The checklist wording below does not replace this run.

---

## Task 1: Pin the visual sources, Inter assets, and shared design tokens

**Files:**

- Create: `IBM Granite with TurboQuant (Intel)/Assets/Fonts/Inter-Regular.ttf`
- Create: `IBM Granite with TurboQuant (Intel)/Assets/Fonts/Inter-Bold.ttf`
- Create: `IBM Granite with TurboQuant (Intel)/Assets/Fonts/OFL.txt`
- Create: `IBM Granite with TurboQuant (Intel)/Assets/Fonts/inter-manifest.json`
- Create: `IBM Granite with TurboQuant (Intel)/Assets/Fonts/README.md`
- Create: `docs/ux/screenshots/model-inspection/reference/model-inspection-complete-ordered-board-v2.svg`
- Create: `docs/ux/screenshots/model-inspection/reference/README.md`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionTheme.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/App.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/UnitTestApp.xaml`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/ModelInspectionAssetContractTests.cs`
- Create: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/ModelInspectionVisualSourceContractTests.cs`
- Modify: `docs/risks/Licence-Register.md`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

- [ ] **Step 1: Write the failing asset and token contract**

Add tests that require:

- Inter release `4.1` and the official `https://github.com/rsms/inter` source;
- exactly Regular and Bold font payloads plus the SIL OFL text;
- manifest byte lengths and lowercase SHA-256 values equal the committed bytes;
- explicit Regular and Bold font-family resources resolve
  `ms-appx:///Assets/Fonts/Inter-Regular.ttf#Inter` and
  `ms-appx:///Assets/Fonts/Inter-Bold.ttf#Inter`; 700-weight roles use the Bold
  resource rather than synthesized bold;
- the exact palette in specification section 5.2;
- `32/18/14/12/10` typography sizes and `700/400` weight roles;
- `840`, `792`, `46`, `44`, and responsive breakpoint tokens;
- both production and packaged-test application definitions merge the same
  Model Inspection dictionary;
- the packaged app/test resources resolve both font faces.

Put the durable SVG byte/hash/provenance assertion in
`ModelInspectionVisualSourceContractTests`, which runs unpackaged from the
repository. Do not make an AppContainer test reach into `docs/`.

Run the focused class. Expected RED: font files, manifest, resource dictionary, and merged resources do not exist.

- [ ] **Step 2: Add only the licensed font faces and immutable provenance**

Download `https://github.com/rsms/inter/releases/download/v4.1/Inter-4.1.zip`, extract only `Inter-Regular.ttf`, `Inter-Bold.ttf`, and the OFL license, then write their actual lengths and SHA-256 values into `inter-manifest.json`. Do not use a system-installed font or a mutable CDN URL at runtime. Copy the exact user-supplied bytes from `C:\Users\Arian\.codex\attachments\e2cdf09a-0127-4ff0-8d25-e03b7663a782\pasted-text.txt` to the named `.svg` path without transforming them, immediately require SHA-256 `8A171A3A1DF66D158990789A368C439752EBE5309B364A870E0C0107B519B7EB`, and record the 13 node IDs and board coordinates in the reference README. The committed SVG is the durable geometry source for every later task.

- [ ] **Step 3: Create one Model Inspection resource dictionary**

Define named `SolidColorBrush`, `FontFamily`, `Double`, `Thickness`, `CornerRadius`, and `FontWeight` resources. Add Light, Dark, and HighContrast dictionaries where a semantic brush requires a system override. Merge it through both `App.xaml` and `UnitTestApp.xaml`; do not copy the palette back into individual controls.

- [ ] **Step 4: Package the assets in the app and test package**

Add explicit `Content` items to the app project. Link the same files into the packaged test project. Assert the generated package contains the two faces, manifest, and license exactly once.

- [ ] **Step 5: Run GREEN verification and commit**

Use the reusable packaged command with:

```powershell
Invoke-PackagedModelInspectionTests `
  -TestFilter 'FullyQualifiedName~ModelInspectionAssetContractTests' `
  -ResultName 'task-01-assets-green.trx'
```

Then run the unpackaged source contract:

```powershell
dotnet test '.\tests\ContractTests\GraniteEdgeAI.ModelInspection.Contracts.Tests\GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj' `
  --configuration Release `
  --filter 'FullyQualifiedName~ModelInspectionVisualSourceContractTests' `
  --minimum-expected-tests 1
if ($LASTEXITCODE -ne 0) { throw 'Visual-source contract failed.' }
```

Expected: all asset/token tests pass; app/test package builds with zero new warnings.

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Assets/Fonts' 'docs/ux/screenshots/model-inspection/reference' 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionTheme.xaml' 'IBM Granite with TurboQuant (Intel)/App.xaml' 'IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj' 'tests/UnitTests/GraniteEdgeAI.UnitTests/UnitTestApp.xaml' 'tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/ModelInspectionAssetContractTests.cs' 'tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/ModelInspectionVisualSourceContractTests.cs' 'docs/risks/Licence-Register.md' 'docs/reviews/model-inspection-cleanup-source-files.txt' 'docs/reviews/model-inspection-cleanup-inventory.md'
git commit -m "feat(model-inspection): add figma design tokens and inter assets"
```

## Task 2: Add the bounded display-text safety boundary

**Files:**

- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionDisplayTextPolicy.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/ModelInspectionDisplayTextPolicyTests.cs`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

- [ ] **Step 1: Write the complete adversarial RED table**

Cover valid international Form-C names and reject over-cap input before normalization, malformed surrogate pairs, C0/C1 controls, newlines, bidi/format controls, private-use/unassigned scalars, `C:\...`, UNC, POSIX paths, slash/backslash, and fully qualified URLs. Prove exact caps: model name 160, metadata label 96, detail text 512 UTF-16 code units.

Also prove the fallback chain:

```text
safe completed configuration name
  -> safe quick-scan name
  -> safe final filename without .gguf
  -> "Not reported"
```

Expected RED: `ModelInspectionDisplayTextPolicy` is absent.

- [ ] **Step 2: Implement one deterministic fail-closed policy**

Expose separate methods rather than a caller-provided arbitrary cap:

```csharp
internal static class ModelInspectionDisplayTextPolicy
{
    internal static string ProjectModelName(
        string? completedName,
        string? quickScanName,
        string fileName);

    internal static string ProjectOptionalLabel(string? value);

    internal static string ProjectRequiredDetail(
        string? value,
        string genericFallback);
}
```

Scan Unicode scalar values, normalize accepted text to Form C, recheck the cap/categories, trim, and collapse repeated U+0020 only. Never include rejected input in an exception or diagnostic.

- [ ] **Step 3: Run RED mutations and GREEN**

Temporarily remove each path/category/cap guard one at a time and confirm its dedicated test fails; restore production after every mutation. Then run:

```powershell
Invoke-PackagedModelInspectionTests `
  -TestFilter 'FullyQualifiedName~ModelInspectionDisplayTextPolicyTests' `
  -ResultName 'task-02-display-text-green.trx'
```

- [ ] **Step 4: Commit**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionDisplayTextPolicy.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/ModelInspectionDisplayTextPolicyTests.cs' 'docs/reviews/model-inspection-cleanup-source-files.txt' 'docs/reviews/model-inspection-cleanup-inventory.md'
git commit -m "feat(model-inspection): bound displayed model metadata"
```

## Task 3: Publish one atomic ViewModel snapshot and render key

**Files:**

- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ViewModels/ModelInspectionViewSnapshot.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ViewModels/ModelInspectionViewModel.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ViewModels/ModelInspectionViewSnapshotTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ViewModels/ModelInspectionViewModelTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ViewModels/README.md`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

- [ ] **Step 1: Add RED invariants and exact key progression**

Add tests for:

- constructor rejection of negative key values, active+terminal, progress+terminal, and cancellation-requested without an active attempt;
- navigation initial `(0,0)`;
- first Start `(1,0)`, progress `(1,1...)`, cancellation at the next revision, terminal at the next revision with progress retired;
- Retry `(2,0)` published before the old attempt is cancelled;
- Choose another advancing generation and clearing progress/terminal before it
  raises navigation, including after a terminal result;
- one lifecycle invalidation advancing generation so queued callbacks are
  stale, while the subsequent `Dispose` for that same retirement is
  idempotent and does not advance or notify twice;
- direct active-attempt replacement remaining distinct from Retry;
- duplicate equal progress not advancing the revision;
- exactly one `PropertyChanged(nameof(Snapshot))` per accepted semantic mutation.

Expected RED: snapshot/render-key types and `Snapshot` are absent.

- [ ] **Step 2: Add the UI-independent immutable types**

```csharp
internal readonly record struct ModelInspectionRenderKey
{
    internal ModelInspectionRenderKey(
        long attemptGeneration,
        long presentationRevision);

    internal long AttemptGeneration { get; }
    internal long PresentationRevision { get; }
}

internal sealed record ModelInspectionViewSnapshot
{
    internal static ModelInspectionViewSnapshot Initial { get; }

    internal ModelInspectionViewSnapshot(
        ModelInspectionRenderKey renderKey,
        bool isRunActive,
        bool isCancellationRequested,
        ModelInspectionProgress? progress,
        ModelInspectionExecutionResult? terminalResult);

    internal ModelInspectionRenderKey RenderKey { get; }
    internal bool IsRunActive { get; }
    internal bool IsCancellationRequested { get; }
    internal ModelInspectionProgress? Progress { get; }
    internal ModelInspectionExecutionResult? TerminalResult { get; }
}
```

- [ ] **Step 3: Replace separately published state with snapshot replacement**

Under `stateLock`, replace the entire snapshot first; then raise one snapshot notification. Keep the three command instances and `CanExecute` semantics. Legacy `Progress`, `Result`, and `IsRunActive` getters may temporarily delegate to `Snapshot` for adjacent tests, but `ModelInspectionPage` must stop sampling them in Task 9.

Both constructors validate their invariants; the render-key constructor rejects
negative components. Terminal publication must set `Progress = null` before
observers run. New attempts, Choose another and lifecycle invalidation advance
generation even when cancellation callbacks run synchronously. `Deactivate`
followed by `Dispose` for one page retirement performs one invalidation only.

- [ ] **Step 4: Run deterministic race regressions**

Use existing `TaskCompletionSource`, cancellation callbacks, and synchronization-context fakes; do not add sleeps. Run:

```powershell
Invoke-PackagedModelInspectionTests `
  -TestFilter 'FullyQualifiedName~ModelInspectionViewSnapshotTests|FullyQualifiedName~ModelInspectionViewModelTests' `
  -ResultName 'task-03-view-snapshot-green.trx'
```

- [ ] **Step 5: Commit**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ViewModels' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ViewModels' 'docs/reviews/model-inspection-cleanup-source-files.txt' 'docs/reviews/model-inspection-cleanup-inventory.md'
git commit -m "refactor(model-inspection): publish atomic render snapshots"
```

## Task 4: Implement the complete truthful 13-state presentation map

**Files:**

- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionFigmaState.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/InspectionFooterStatus.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionPresentationCommands.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionRegionKeys.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionPagePresentation.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionPresentationFactory.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/InspectionActionPresentation.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/InspectionContentCardPresentation.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/InspectionModelCardPresentation.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/PresentationTestData.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/ModelInspectionFigmaStatePresentationTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/ModelInspectionPresentationFactoryTests.cs`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

- [ ] **Step 1: Lock the exact state enum in RED tests**

```csharp
internal enum ModelInspectionFigmaState
{
    InspectionProgress = 1,
    ReadyCollapsed = 2,
    ReadyExpanded = 3,
    ReadyWithWarningsCollapsed = 4,
    ReadyWithWarningsExpanded = 5,
    ConversionRequiredCollapsed = 6,
    ConversionRequiredExpanded = 7,
    IncompletePackage = 8,
    Unsupported = 9,
    InvalidCollapsed = 10,
    InvalidExpanded = 11,
    Cancelled = 12,
    OperationalFailure = 13
}

public enum InspectionFooterStatus
{
    InProgress = 0,
    Complete = 1,
    NotComplete = 2,
    Interrupted = 3
}
```

Use 13 DataRows to require exact state, outcome tone, badge, content mode, disclosure, footer, active/disabled actions, and announcement text. Add four collapsed/expanded pair tests and undefined-enum/fail-closed tests.

- [ ] **Step 2: Add one unified factory entry point**

Use one input boundary:

```csharp
internal static ModelInspectionPagePresentation Create(
    ModelInspectionRequest request,
    ModelInspectionViewSnapshot snapshot,
    ModelInspectionPresentationCommands commands,
    bool isDisclosureExpanded,
    IReadOnlyList<InspectionContentItemPresentation> progressRows);
```

Lock the command carrier instead of letting task implementations invent
different tuples:

```csharp
internal sealed class ModelInspectionPresentationCommands
{
    internal ModelInspectionPresentationCommands(
        ICommand cancel,
        ICommand retry,
        ICommand chooseAnother);

    internal ICommand Cancel { get; }
    internal ICommand Retry { get; }
    internal ICommand ChooseAnother { get; }
}
```

The constructor rejects every null command. There is one command instance per
ViewModel lifetime; region equality samples `CanExecute(null)` and never uses
command reference equality.

The output uses this exact carrier in Task 4:

```csharp
internal sealed class ModelInspectionPagePresentation
{
    internal ModelInspectionPagePresentation(
        ModelInspectionRenderKey renderKey,
        ModelInspectionFigmaState state,
        InspectionModelCardPresentation modelCard,
        InspectionContentCardPresentation contentCard,
        InspectionOutcomePresentation outcomeCard,
        InspectionActionCardPresentation actionCard,
        InspectionFooterStatus footerStatus,
        ModelInspectionRegionKeys regionKeys,
        string progressAnnouncement,
        string outcomeAnnouncement);

    internal ModelInspectionRenderKey RenderKey { get; }
    internal ModelInspectionFigmaState State { get; }
    internal InspectionModelCardPresentation ModelCard { get; }
    internal InspectionContentCardPresentation ContentCard { get; }
    internal InspectionOutcomePresentation OutcomeCard { get; }
    internal InspectionActionCardPresentation ActionCard { get; }
    internal InspectionFooterStatus FooterStatus { get; }
    internal ModelInspectionRegionKeys RegionKeys { get; }
    internal string ProgressAnnouncement { get; }
    internal string OutcomeAnnouncement { get; }
}
```

All reference/string arguments are null-checked and announcement strings pass
the display-text bound. `InspectionFooterStatus` is the one shared enum
consumed by the presentation and onboarding indicator; do not create a second
conversion enum. Reject expansion for states without an approved pair.

Retain the existing `CreateInitial`, `CreateProgress`, and `CreateTerminal`
signatures as narrow compatibility adapters until the page migrates in Task
10. Mark them for removal in that task. This keeps every intermediate commit
buildable and green.

- [ ] **Step 3: Map real overview evidence and the five completed checks**

Ready must use `InspectionModelCardMode.Detailed`, populate all eight overview
fields, and produce five ordered `InspectionCheckPresentation` rows.
ReadyWithWarnings retains the compact model card and owns its disclosure in
the content card, as frames 04/05 require. Use the display policy and `Not
reported`; never infer IBM, instruction tuning, quantization marketing names,
or tensor facts.

The Ready disclosure summary is `All 5 inspection checks passed`. Warning, conversion, and invalid disclosures use bounded classified findings/diagnostic evidence. No Figma sample value may enter a production factory constant.

- [ ] **Step 4: Lock action policy**

Add `AutomationHelpText` to action presentation and bind it later in Task 7. Active actions remain:

- progress: Cancel is always visible in state 01 and enabled iff its command
  can execute (initial and cancel-requested variants keep it disabled);
- Ready/Warnings: Choose another;
- Conversion: Choose another remains active; preparation/format-selection
  actions are visible but disabled;
- Incomplete: Locate missing file mapped to Choose another;
- Unsupported/Invalid: Choose another;
- Cancelled: Restart and Choose another;
- Failure: Retry and Choose another.

Hardware Fit, conversion execution/format selection, the Incomplete/Unsupported
technical-details actions, and every full report/export action are visible but
have no command, are disabled, and say `Coming later` in help text.

- [ ] **Step 5: Prove privacy and no-policy expansion**

Inject path, raw-template, exception-chain, request-ID, canonical-path digest, stdout/stderr, and username sentinels into all optional surfaces. Assert none enters text, tooltip, help, or automation values. Assert production classifier tests still emit only Ready/ReadyWithWarnings.

The only currently supported nonempty production finding code is
`MI-WARN-CHAT-TEMPLATE-MISSING`. A completed synthetic outcome with no finding
uses that outcome's fixed generic row and `Not reported` diagnostic value, so
Conversion/Incomplete/Unsupported/Invalid tests do not invent new codes.
Any other nonempty code selects a fixed privacy-safe OperationalFailure
presentation with diagnostic `MI-OP-PRESENTATION-UNSUPPORTED-EVIDENCE`; the
input code/text is not echoed and no guessed tone/action is applied.

The factory also emits immutable semantic keys with this exact public-to-the-
feature shape:

```csharp
internal readonly record struct ModelInspectionRegionKey
{
    internal ModelInspectionRegionKey(string value);
    internal string Value { get; }
}

internal readonly record struct ModelInspectionProgressRegionKey(
    ModelInspectionStage? Stage,
    ModelInspectionStageStatus? StageStatus,
    int CompletedStageCount,
    int StageCount,
    double? StageFraction,
    string Detail);

internal sealed record ModelInspectionRegionKeys(
    ModelInspectionRegionKey Outcome,
    ModelInspectionRegionKey Model,
    ModelInspectionRegionKey Content,
    ModelInspectionRegionKey Actions,
    ModelInspectionRegionKey Footer,
    ModelInspectionProgressRegionKey Progress,
    ModelInspectionRegionKey Announcements);
```

`ModelInspectionRegionKey` rejects blank/control-bearing/over-cap values. The
progress key is constructed from the immutable snapshot semantics, not row or
DTO references; initial/no-progress values use null stage/status, zero counts,
null fraction, and empty detail. Run/cancellation command state is deliberately
absent from this key and belongs to Actions/lifecycle. Keys contain only state/mode, disclosure,
relevant command-enabled bits, footer status and render/announcement identity.
Tests prove independently allocated but semantically equal presentations have
equal keys, Cancel changes Actions only, progress changes ProgressRows and the
progress announcement only, and a terminal transition changes exactly its
terminal regions.

- [ ] **Step 6: Run GREEN and commit**

```powershell
Invoke-PackagedModelInspectionTests `
  -TestFilter 'FullyQualifiedName~ModelInspectionFigmaStatePresentationTests|FullyQualifiedName~ModelInspectionPresentationFactoryTests|FullyQualifiedName~ModelInspectionDisplayTextPolicyTests' `
  -ResultName 'task-04-state-map-green.trx'
```

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation' 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation' 'docs/reviews/model-inspection-cleanup-source-files.txt' 'docs/reviews/model-inspection-cleanup-inventory.md'
git commit -m "feat(model-inspection): map all figma inspection states"
```

## Task 5: Retain five progress rows and mutate only changed stage state

**Files:**

- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/InspectionContentItemPresentation.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/InspectionProgressRows.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/InspectionProgressRowsUpdate.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionPagePresentation.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionPresentationFactory.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/InspectionContentCardPresentation.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/InitialInspectionProgressPresentationFactory.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/InspectionProgressPresentationFactory.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/InspectionProgressRowsTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/InspectionProgressPresentationFactoryTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/InitialInspectionProgressPresentationTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionContentCardTests.cs`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

- [ ] **Step 1: Write identity-preservation RED tests**

Require exactly five ordered objects constructed once per attempt; 0/5 initial all Waiting; only changed row properties notify; all five references survive every progress update; at most one Active; newest Completed wins when Active+Completed arrive before render; reset preserves no stale fraction/detail.

- [ ] **Step 2: Add a bounded mutable progress-row surface**

Keep one row type used by existing XAML, but make only progress-mutated properties internally settable and `INotifyPropertyChanged`. Finding/report rows remain initialized once. The pure factory creates an immutable update payload rather than mutating rows:

```csharp
internal sealed record InspectionProgressRowsUpdate(
    ModelInspectionProgressRegionKey Key,
    ModelInspectionRenderKey OwnerKey,
    string ProgressSummary);

internal readonly record struct InspectionProgressRowChange
{
    internal InspectionProgressRowChange(
        int rowIndex,
        bool statusChanged,
        bool detailChanged);
    internal int RowIndex { get; }
    internal bool StatusChanged { get; }
    internal bool DetailChanged { get; }
}

internal sealed class InspectionProgressRowsApplyResult
{
    internal static InspectionProgressRowsApplyResult Empty { get; }
    internal InspectionProgressRowsApplyResult(
        IReadOnlyList<InspectionProgressRowChange> rowChanges,
        bool progressSummaryChanged);
    internal IReadOnlyList<InspectionProgressRowChange> RowChanges { get; }
    internal bool ProgressSummaryChanged { get; }
    internal bool IsEmpty { get; }
}
```

Task 5 replaces the Task 4 page-presentation constructor with the same ordered
arguments plus final non-null
`InspectionProgressRowsUpdate progressRowsUpdate`, exposes it as
`ProgressRowsUpdate`, and updates every call site atomically.
`InspectionProgressRows.Apply(InspectionProgressRowsUpdate update)` validates
the owner/key, derives the five statuses only from the safe immutable key,
updates the observable
`ProgressSummary`, changes only unequal row/summary properties, and returns an
immutable `InspectionProgressRowsApplyResult` containing the ordered changed
row indexes plus `StatusChanged`/`DetailChanged` flags. An empty result means
no motion or row notification. Define the result and row-change records beside
`InspectionProgressRows`; `Apply` has the exact signature
`internal InspectionProgressRowsApplyResult Apply(InspectionProgressRowsUpdate update)`.
Row indexes are validated to 0 through 4, inputs are copied to a read-only
array, and `IsEmpty` is true only when no row or summary changed. No factory
call may mutate a retained row.

The factory projects `ModelInspectionProgress.UserMessage` through
`ModelInspectionDisplayTextPolicy` before placing it in the progress key. The
update never carries the raw progress contract. Tests inject path, control,
bidi and oversize progress sentinels and prove neither row detail nor
automation/live text can expose them.

`InspectionProgressRows` is a `public sealed` XAML-visible
`INotifyPropertyChanged` owner with a public event and public read-only `Items`
and `ProgressSummary` properties, but internal construction/mutation.
Task 5 replaces the unified factory's final
`IReadOnlyList<InspectionContentItemPresentation>` argument with that owner.
`InspectionContentCardPresentation` gains a read-only `ProgressRows` property
for Progress mode, and `ProgressTemplate` binds
`ProgressRows.ProgressSummary` and `ProgressRows.Items` in `Mode=OneWay`.
Finding/report modes keep their existing immutable `Items`; do not make them
observable or route them through the progress owner.

The page owns one `InspectionProgressRows` set for its initial state. The first
attempt promotes and resets that set to owner key `(1,0)` without replacing
its five row objects. A later attempt generation creates and attaches one new
set exactly once at the attempt boundary; every update inside that attempt
retains those five references. `Reset(ModelInspectionRenderKey ownerKey)`
rejects an older owner and clears status, fraction, detail, and pending row
animation state before the new attempt renders.

- [ ] **Step 3: Change progress bindings to `Mode=OneWay`**

Use observation only for the mutable stage fields and progress summary. Do not
make static finding/report text observable. Keep the ItemsSource attached for
the attempt lifetime. Tests require `0 of 5` to advance without reassigning the
content-card presentation or ItemsSource.

- [ ] **Step 4: Run GREEN and commit**

```powershell
Invoke-PackagedModelInspectionTests `
  -TestFilter 'FullyQualifiedName~InspectionProgressRowsTests|FullyQualifiedName~InspectionProgressPresentationFactoryTests|FullyQualifiedName~InitialInspectionProgressPresentationTests|FullyQualifiedName~InspectionContentCardTests' `
  -ResultName 'task-05-stable-progress-green.trx'
```

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/InspectionContentItemPresentation.cs' 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/InspectionContentCardPresentation.cs' 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/InspectionProgressRows.cs' 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/InspectionProgressRowsUpdate.cs' 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionPagePresentation.cs' 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionPresentationFactory.cs' 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/InitialInspectionProgressPresentationFactory.cs' 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/InspectionProgressPresentationFactory.cs' 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection' 'docs/reviews/model-inspection-cleanup-source-files.txt' 'docs/reviews/model-inspection-cleanup-inventory.md'
git commit -m "perf(model-inspection): retain stable progress rows"
```

## Task 6: Build the Figma page shell, outcome banner, and model overview

**Files:**

- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionOutcomeCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionOutcomeCard.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionOutcomeCardTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionModelCardTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageLayoutTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionVisualStateGuardTests.cs`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

- [ ] **Step 1: Write RED visual-tree/geometry tests**

At a 1440 x 1024 client, assert the centered 840 px host, 32 px title, 14 px subtitle, exact outcome heights/tone resources, detailed model field order, 792 px inner width, 24 px insets, 30/34 px chips, and no clipped/wrongly blank detailed fields. Assert resource references, not repeated hex values.

- [ ] **Step 2: Refactor the shell without replacing controls**

Use one stable `Grid`/`ScrollViewer` page tree. Implement exact desktop width, responsive `VisualState`s for >=888, 600-887, and <600, and natural height at 200% text scale. Keep controls individually accessible.

- [ ] **Step 3: Make Ready details reachable**

Remove the unconditional expansion reset in `InspectionModelCard.ApplyPresentation`. Expose an expansion-change event to the page; keep the disclosure button focused. Render the Ready collapsed/expanded geometry and five bounded check rows. Do not let the control own attempt/outcome identity.

- [ ] **Step 4: Apply shared theme and high-contrast semantics**

Replace local literal brushes/font sizes with semantic resources. State text and icons remain present so color is never the only signal.

- [ ] **Step 5: Run GREEN and commit**

```powershell
Invoke-PackagedModelInspectionTests `
  -TestFilter 'FullyQualifiedName~ModelInspectionPageLayoutTests|FullyQualifiedName~InspectionOutcomeCardTests|FullyQualifiedName~InspectionModelCardTests|FullyQualifiedName~InspectionVisualStateGuardTests' `
  -ResultName 'task-06-shell-model-green.trx'
```

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml' 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionOutcomeCard.xaml' 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionOutcomeCard.xaml.cs' 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml' 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageLayoutTests.cs' 'docs/reviews/model-inspection-cleanup-source-files.txt' 'docs/reviews/model-inspection-cleanup-inventory.md'
git commit -m "feat(model-inspection): match figma shell and model details"
```

## Task 7: Complete content, action, footer, and responsive states

**Files:**

- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionActionCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionActionCard.xaml.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionDisclosure.xaml`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionDisclosure.xaml.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionDisclosureToggleRequestedEventArgs.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/Controls/OnboardingStageIndicator.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/Controls/OnboardingStageIndicator.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/InspectionFooterStatus.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionContentCardTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionActionCardTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionDisclosureTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionModelCardTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Models/InspectionContentCardPresentationTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/ModelInspectionAssetContractTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/Controls/OnboardingStageIndicatorTests.cs`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

- [ ] **Step 1: Add RED tests for states 01 and 04-13**

Require exact section titles, row heights at standard scale, bounded disclosure viewports, visible scroll affordance, action slots/order, 46 px button height, 44 px interaction minimum, disabled help text, and footer states `InProgress`, `Complete`, `NotComplete`, `Interrupted`.

For every progress-row status, require both glyph and readable text. The XAML
must have explicit markers for Passed, Warning, Error/Failed, Information,
Active and Waiting; no terminal warning/failure/cancel status may produce a
blank marker.

- [ ] **Step 2: Wire all four approved disclosures**

Ready uses the model-card disclosure. Warning, Conversion, and Invalid use the
content-card disclosure. Replace both built-in `Expander` instances with one
reusable `InspectionDisclosure` whose template owns named
`DisclosureChevron` and retained `DisclosureViewport` elements. The viewport
stays realized at collapsed height/opacity, is hit-test/accessibility hidden
while collapsed, and is removed from layout only by a current keyed completion;
the platform Expander must not collapse it before the app-owned animation.

`InspectionDisclosure` raises
`DisclosureToggleRequested(InspectionDisclosureToggleRequestedEventArgs)` with
the target `IsExpanded`; it never changes domain/presentation state itself.
Its automation peer implements `IExpandCollapseProvider`, keyboard activation
raises the same request, and page calls internal
`PrepareTargetState(bool isExpanded)` then
`CompleteTargetState(bool isExpanded)`. It exposes internal read-only
`ChevronTarget` and `ViewportTarget` solely to the page motion bridge. Model
and Content cards expose one forwarded `DisclosureToggleRequested` event and
their current internal `ActiveDisclosure`; they do not own the boolean.
Remove mutable evidence ownership from
`InspectionContentCardPresentation.IsExpanded`.

Use these exact cross-control carriers:

```csharp
internal sealed class InspectionDisclosureToggleRequestedEventArgs : EventArgs
{
    internal InspectionDisclosureToggleRequestedEventArgs(bool isExpanded);
    internal bool IsExpanded { get; }
}

// InspectionDisclosure
internal event EventHandler<InspectionDisclosureToggleRequestedEventArgs>?
    ToggleRequested;
internal UIElement ChevronTarget { get; }
internal FrameworkElement ViewportTarget { get; }
internal void PrepareTargetState(bool isExpanded);
internal void CompleteTargetState(bool isExpanded);

// InspectionModelCard and InspectionContentCard
internal event EventHandler<InspectionDisclosureToggleRequestedEventArgs>?
    DisclosureToggleRequested;
internal InspectionDisclosure? ActiveDisclosure { get; }
```

`ActiveDisclosure` is null whenever that card/state has no visible disclosure;
otherwise it is the stable named control instance. The cards forward only
events from their current disclosure and detach old handlers on unload.

Update `InspectionContentCardPresentationTests` in the same RED/GREEN slice:
replace mutable/shared-expansion assertions with immutable default and
independent-presentation assertions so the intermediate commit compiles.

- [ ] **Step 3: Implement all remaining Figma layouts**

Match frames 01 and 04-13 using reusable progress/finding/report templates. Expanded lists stay in fixed-height scroll viewports with right scrollbar, bottom fade, and `Scroll for more`. Do not add a report/export implementation.

- [ ] **Step 4: Bind disabled-action accessibility**

Set `AutomationProperties.HelpText` from
`InspectionActionPresentation.AutomationHelpText` and always render adjacent
accessible `Coming later` helper text plus a tooltip for every disabled future
action. Do not branch on whether a disabled button happens to be discoverable;
tests assert the button label, tooltip, helper Text/AutomationName/HelpText and
normal tab order deterministically.

- [ ] **Step 5: Extend the existing onboarding indicator**

Keep `CurrentStage=InspectModel`; apply the inspection footer status as an
orthogonal state. Do not advance to Hardware Fit. Lock the control boundary:

```csharp
public InspectionFooterStatus InspectionStatus { get; set; }
public static readonly DependencyProperty InspectionStatusProperty;
```

Its property-changed callback updates step-2 glyph/text and the control's
non-live AutomationName for `InProgress`, `Complete`, `NotComplete`, or
`Interrupted`; it never calls `RaiseLiveRegionChanged`, because the content
card is the sole polite progress region. State 01 says step 2 active, trusted
completed outcomes say inspection complete, cancelled says not complete, and
operational failure says interrupted.

Finish migrating Content/Action to the Task 1 dictionary and extend
`ModelInspectionAssetContractTests` to reject remaining color literals in all
four Model Inspection control XAML files. This assertion becomes GREEN here,
after Tasks 6 and 7 have both performed their owned migrations.

- [ ] **Step 6: Run GREEN and commit**

```powershell
Invoke-PackagedModelInspectionTests `
  -TestFilter 'FullyQualifiedName~InspectionContentCardTests|FullyQualifiedName~InspectionActionCardTests|FullyQualifiedName~InspectionDisclosureTests|FullyQualifiedName~InspectionModelCardTests|FullyQualifiedName~InspectionContentCardPresentationTests|FullyQualifiedName~ModelInspectionAssetContractTests|FullyQualifiedName~OnboardingStageIndicatorTests|FullyQualifiedName~ModelInspectionFigmaStatePresentationTests' `
  -ResultName 'task-07-complete-states-green.trx'
```

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls' 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/InspectionFooterStatus.cs' 'IBM Granite with TurboQuant (Intel)/Features/Onboarding/Controls' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Models/InspectionContentCardPresentationTests.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/ModelInspectionAssetContractTests.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/Controls/OnboardingStageIndicatorTests.cs' 'docs/reviews/model-inspection-cleanup-source-files.txt' 'docs/reviews/model-inspection-cleanup-inventory.md'
git commit -m "feat(model-inspection): complete figma outcome layouts"
```

## Task 8: Coalesce renders and apply changed regions only

**Files:**

- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/IModelInspectionRenderDispatcher.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/DispatcherQueueModelInspectionRenderDispatcher.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionPresentationDelta.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionVisualOperationKey.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionRenderCoordinator.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/ModelInspectionRenderCoordinatorTests.cs`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

- [ ] **Step 1: Write pure deterministic coordinator RED tests**

Cover one queued callback for a notification burst, newest snapshot wins, old generation/revision rejected, dispatcher rejection clears pending state, unchanged regions not reassigned, Cancel updates Actions only, progress row identities retained, terminal retires progress first, Dispose rejects queued work, and a reentrant newer request schedules one final drain.

- [ ] **Step 2: Define explicit region deltas**

```csharp
[Flags]
internal enum ModelInspectionPresentationRegions
{
    None = 0,
    Outcome = 1,
    Model = 2,
    Content = 4,
    Actions = 8,
    Footer = 16,
    ProgressRows = 32,
    LiveRegions = 64
}
```

`ModelInspectionPresentationDelta` carries the render key, latest presentation,
changed flags, and the immutable `InspectionProgressRowsUpdate` payload. Compare
semantic values, not object references. During state 01, progress changes use
`ProgressRows`; they call `rows.Apply(delta.ProgressRowsUpdate)` only after the
coordinator's final key check and do not reassign the content card.

Set `ProgressRows` when the semantic Progress key changes or when
`AttemptGeneration` advances and the page-owned row set must be promoted/reset.
A cancellation request inside the same generation changes Actions only. The
first-attempt reset may return no row changes because initial rows are already
Waiting; Retry still clears old detail, fraction, and statuses before state 01
becomes visible.

Use the Task 4 typed region keys; never compare DTO, nested-list, or command
references. Lock the coordinator surface before implementation:

```csharp
internal interface IModelInspectionRenderDispatcher
{
    bool TryEnqueue(Action callback);
}

internal readonly record struct ModelInspectionVisualOperationKey
{
    internal ModelInspectionVisualOperationKey(
        ModelInspectionRenderKey renderKey,
        long interactionRevision);

    internal ModelInspectionRenderKey RenderKey { get; }
    internal long InteractionRevision { get; }
}

internal sealed class ModelInspectionPresentationDelta
{
    internal ModelInspectionPresentationDelta(
        ModelInspectionRenderKey renderKey,
        ModelInspectionPagePresentation presentation,
        ModelInspectionPresentationRegions changedRegions,
        InspectionProgressRowsUpdate? progressRowsUpdate,
        ModelInspectionVisualOperationKey visualOperationKey);

    internal ModelInspectionRenderKey RenderKey { get; }
    internal ModelInspectionPagePresentation Presentation { get; }
    internal ModelInspectionPresentationRegions ChangedRegions { get; }
    internal InspectionProgressRowsUpdate? ProgressRowsUpdate { get; }
    internal ModelInspectionVisualOperationKey VisualOperationKey { get; }
}

internal sealed class ModelInspectionRenderCoordinator : IDisposable
{
    internal ModelInspectionRenderCoordinator(
        IModelInspectionRenderDispatcher dispatcher,
        Func<ModelInspectionViewSnapshot, bool,
            ModelInspectionPagePresentation> createPresentation,
        Action<ModelInspectionPresentationDelta> applyDelta);

    internal ModelInspectionRenderKey? LatestAcceptedKey { get; }
    internal ModelInspectionPagePresentation? CurrentPresentation { get; }
    internal long InteractionRevision { get; }
    internal bool HasPendingRender { get; }
    internal void ApplyInitial(ModelInspectionViewSnapshot snapshot);
    internal void RequestRender(ModelInspectionViewSnapshot snapshot);
    internal bool TryRequestDisclosure(
        ModelInspectionRenderKey renderKey,
        bool isExpanded,
        out ModelInspectionVisualOperationKey operationKey);
    internal bool IsCurrent(ModelInspectionRenderKey renderKey);
    internal bool IsCurrent(ModelInspectionVisualOperationKey operationKey);
    internal void InvalidateInteractions();
    public void Dispose();
}
```

The operation-key constructor rejects a negative interaction revision. The
delta constructor rejects null presentation, `None` for a newly applied
presentation, a `ProgressRows` flag without an update, and an update without
that flag. Every delta has a visual-operation key: ordinary semantic progress
or terminal render uses `(delta.RenderKey, current InteractionRevision)`, while
a disclosure render uses the incremented revision written by
`TryRequestDisclosure`. `TryEnqueue` returning false is the only dispatcher
rejection path.

- [ ] **Step 3: Implement the coordinator state machine**

`RequestRender` stores only the newest valid snapshot and schedules at most one
dispatcher callback. `Drain` rechecks page lifetime and key before factory
invocation, before applying, and before rescheduling. `TryRequestDisclosure`
returns false with `operationKey = default` for a stale key, a state without an
approved disclosure pair, or a no-op target; those paths do not increment,
schedule, or mutate current presentation. Otherwise it increments the separate
`InteractionRevision`, stores the target expansion, schedules the current
snapshot, writes
`ModelInspectionVisualOperationKey(RenderKey, InteractionRevision)`, and
returns true. That
drain calls the factory with the new expansion, changes the actual Figma state
identity (02/03, 04/05, 06/07, or 10/11), and includes the operation key in the
delta. A newer toggle replaces any pending target/key; its older completion
cannot change `CurrentPresentation` or final visibility.

Every newer accepted render key invalidates progress/terminal operations from
an older semantic revision; every interaction increment invalidates an older
same-render disclosure operation. `InvalidateInteractions` increments the
interaction revision even when no disclosure is open. Tests manually complete
old stage, terminal, and disclosure driver calls after a newer render and prove
that none changes visibility, transforms, announcement identity, or the
coordinator's current presentation.

For terminal deltas, the apply contract is ordered: invalidate/cancel row
motion and retire/hide the progress content first; update model, actions and
footer second; reveal/crossfade the outcome third; schedule keyed live-region
announcements last. Tests observe those exact phases, not merely the final
flag set.

- [ ] **Step 4: Run mutation checks and GREEN**

Mutate away the pre-apply key check, pending reset, and Actions-only delta; confirm dedicated tests fail. Run:

```powershell
Invoke-PackagedModelInspectionTests `
  -TestFilter 'FullyQualifiedName~ModelInspectionRenderCoordinatorTests' `
  -ResultName 'task-08-render-coordinator-green.trx'
```

- [ ] **Step 5: Commit**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/ModelInspectionRenderCoordinatorTests.cs' 'docs/reviews/model-inspection-cleanup-source-files.txt' 'docs/reviews/model-inspection-cleanup-inventory.md'
git commit -m "perf(model-inspection): coalesce presentation renders"
```

## Task 9: Add retargetable motion, disclosure ownership, and keyed announcements

**Files:**

- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionMotion.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/IModelInspectionAnimationDriver.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/WinUiModelInspectionAnimationDriver.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/IModelInspectionMotionSettings.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/UiSettingsModelInspectionMotionSettings.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/InspectionFooterStatusChangedEventArgs.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionRenderCoordinator.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionContentCard.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionModelCard.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionOutcomeCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/InspectionOutcomeCard.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/ModelInspectionMotionTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/ModelInspectionDisclosureTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionVisualStateGuardTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageNavigationTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingModelInspectionNavigationTests.cs`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

- [ ] **Step 1: Lock exact endpoint/timing RED tests**

Require:

- Fast 160 ms: status icon/ring opacity;
- Standard 180 ms: active detail opacity plus Y `8 -> 0`, terminal crossfade;
- Disclosure 240 ms: chevron, clipped viewport opacity/reveal, and following-element reposition;
- cubic ease-out;
- animations disabled means zero driver starts and identical final semantic state.

Use a fake driver that records endpoints and completions; do not measure elapsed wall time.

`ModelInspectionMotion.cs` contains the one immutable spec consumed by both
production and fake drivers:

```csharp
internal sealed record ModelInspectionMotionSpec
{
    internal static ModelInspectionMotionSpec Approved { get; }
    internal TimeSpan FastDuration { get; }             // 160 ms
    internal TimeSpan StandardDuration { get; }         // 180 ms
    internal TimeSpan DisclosureDuration { get; }       // 240 ms
    internal Vector2 EaseOutControlPoint1 { get; }       // (0, 0)
    internal Vector2 EaseOutControlPoint2 { get; }       // (0.2, 1)
    internal double StatusOpacityFrom { get; }           // 0
    internal double StatusOpacityTo { get; }             // 1
    internal double ActiveDetailOpacityFrom { get; }     // 0
    internal double ActiveDetailOpacityTo { get; }       // 1
    internal double ActiveDetailOffsetYFrom { get; }     // 8
    internal double ActiveDetailOffsetYTo { get; }       // 0
    internal double TerminalOutgoingOpacityFrom { get; } // 1
    internal double TerminalOutgoingOpacityTo { get; }   // 0
    internal double TerminalIncomingOpacityFrom { get; } // 0
    internal double TerminalIncomingOpacityTo { get; }   // 1
    internal double CollapsedChevronDegrees { get; }     // 0
    internal double ExpandedChevronDegrees { get; }      // 180
    internal double CollapsedRevealProgress { get; }     // 0
    internal double ExpandedRevealProgress { get; }      // 1
}
```

The constructor validates every finite endpoint and positive duration and is
internal for mutation tests. `WinUiModelInspectionAnimationDriver` and the fake
both receive a non-null spec in their constructors; no duplicate literal is
allowed in either implementation. Tests assert the exact spec and manually
retarget in-flight stage and terminal calls as well as disclosure calls.

- [ ] **Step 2: Implement Windows animation policy and driver**

Read `UISettings.AnimationsEnabled` through an injectable policy. Use Windows
Composition for opacity/translation/rotation. For disclosure reflow, capture
following-element bounds before layout, apply the new layout, set a compositor
translation equal to the old-minus-new Y offset, and animate that translation
to zero in exactly 240 ms. Do not use `RepositionThemeTransition`, whose
duration/easing are platform-owned, and do not add a third-party dependency.

Lock the seams as:

```csharp
internal interface IModelInspectionMotionSettings : IDisposable
{
    bool AnimationsEnabled { get; }
    event EventHandler? AnimationsEnabledChanged;
}

internal interface IModelInspectionAnimationDriver : IDisposable
{
    void StartStageStatus(
        UIElement target,
        ModelInspectionVisualOperationKey key,
        Action<ModelInspectionVisualOperationKey> completed);
    void StartActiveDetail(
        UIElement target,
        ModelInspectionVisualOperationKey key,
        Action<ModelInspectionVisualOperationKey> completed);
    void StartDisclosure(
        UIElement chevron,
        FrameworkElement viewport,
        IReadOnlyList<UIElement> followingElements,
        bool isExpanded,
        IReadOnlyList<double> previousTopOffsets,
        ModelInspectionVisualOperationKey key,
        Action<ModelInspectionVisualOperationKey> completed);
    void StartTerminal(
        UIElement outgoing,
        UIElement incoming,
        ModelInspectionVisualOperationKey key,
        Action<ModelInspectionVisualOperationKey> completed);
    void CancelAll();
}

internal sealed class InspectionFooterStatusChangedEventArgs : EventArgs
{
    internal InspectionFooterStatusChangedEventArgs(
        InspectionFooterStatus status);

    internal InspectionFooterStatus Status { get; }
}
```

The disclosure caller captures each following element's pre-layout top before
applying the target expanded/collapsed layout, passes those offsets and the
target `isExpanded`, then the driver measures post-layout tops. Expansion owns
chevron `0 -> 180`, viewport opacity `0 -> 1`, and old-minus-new translation;
collapse owns the exact reverse endpoints. Both directions finish at the new
layout in 240 ms. Offset count must equal element count. The four methods own
the exact endpoints/tokens from Step 1; callers cannot pass arbitrary
durations. The fake records calls and completes them manually.

Bridge progress motion inside `InspectionContentCard`, where realized repeater
containers actually exist. Name the status and detail elements in
`ProgressStageTemplate`. The control tracks `ItemsRepeater.ElementPrepared` and
`ElementClearing`, and exposes:

```csharp
internal void AnimateProgressChanges(
    InspectionProgressRowsApplyResult changes,
    IModelInspectionAnimationDriver driver,
    ModelInspectionVisualOperationKey operationKey,
    Func<ModelInspectionVisualOperationKey, bool> isCurrent);
internal void CancelProgressMotion();
```

Only realized current containers animate; an unrealized row already has its
truthful final bound state and starts no animation. A recycled container has
all composition animation stopped and its old row association removed before
reuse. Attempt/outcome/navigation invalidation calls `CancelProgressMotion`
and the page/driver `CancelAll`, so no template callback can outlive its row or
attempt.
`UiSettingsModelInspectionMotionSettings` raises the change event when the
system animation preference changes and releases its watcher in `Dispose`.

- [ ] **Step 3: Move disclosure state to page/coordinator ownership**

Every toggle increments interaction revision and captures a combined visual-operation key. New attempt, outcome change, navigation away, or motion-setting change invalidates it. Rapid Expand -> Collapse and Collapse -> Expand must reject the older completion.

When `AnimationsEnabledChanged` fires, invalidate interaction revision,
cancel the driver, apply the already-selected final disclosure state
instantly, and start no replacement animation.

- [ ] **Step 4: Integrate the coordinator before removing assignment announcements**

Construct the Task 8 coordinator per navigation lifetime and route the atomic
snapshot through it. Apply card presentations only for changed regions, update
stable rows in place, and forward footer state without advancing onboarding.
Only after this path is active may the control setters stop announcing.

Pin construction for deterministic tests and production ownership. Keep the
existing public/default and service-only constructors as adapters; add this
internal constructor:

```csharp
internal ModelInspectionPage(
    IModelInspectionService service,
    Func<IModelInspectionRenderDispatcher> dispatcherFactory,
    Func<IModelInspectionAnimationDriver> animationDriverFactory,
    Func<IModelInspectionMotionSettings> motionSettingsFactory);
```

Each factory is invoked once per `OnNavigatedTo` lifetime. The page owns and
disposes that lifetime's coordinator, animation driver, and motion settings
before deactivating the ViewModel on `OnNavigatedFrom`; it never disposes the
service. Production adapters create the DispatcherQueue/Composition/UISettings
implementations. Tests inject fresh fakes and assert disposal/order.

Terminal retirement is semantic before it is visual: stop accepting/updating
progress and remove it from accessibility/live semantics first, but retain one
hit-test-disabled outgoing content layer in the page Grid until the 180 ms
crossfade completion. The incoming outcome layer becomes authoritative during
the transition; completion collapses/clears the outgoing layer only if its
visual-operation key is still current. `ModelInspectionPage.xaml` owns those
two stable layers, so the driver always receives realized outgoing/incoming
targets and flow-layout removal cannot pre-empt the crossfade.

- [ ] **Step 5: Make announcements explicit and keyed**

Presentation assignment must not automatically announce. Content exposes
`internal void AnnounceProgress(string automationName)` and Outcome exposes
`internal void AnnounceOutcome(string automationName)`; each rejects blank or
text over 512 code units, controls, bidi/format characters, or path-shaped
input with `ArgumentException`, and raises only its one authoritative peer.
Only already-projected factory text reaches these hooks. The page's
`applyDelta` callback invokes them only after the coordinator has delivered a
current delta and rechecks `coordinator.IsCurrent(delta.RenderKey)` immediately
before each call. The coordinator never holds a control reference. Dedupe
terminal identity for the whole attempt. Same-attempt Hide -> show does not
reannounce; Retry reaching the same outcome announces once for the new
generation.

The page exposes `internal InspectionFooterStatus CurrentFooterStatus` and
`internal event EventHandler<InspectionFooterStatusChangedEventArgs>
FooterStatusChanged`. `AttachModelInspectionPage` subscribes and immediately
samples current status because `Frame.Navigate` has already run
`OnNavigatedTo`; it validates sender identity and detaches on replacement.
Footer-only changes do not raise the indicator's polite live event, leaving
the page progress region as the sole polite inspection announcer.

- [ ] **Step 6: Prove focus and reduced-motion equivalence**

Expand/collapse keeps focus on the disclosure and progress never steals focus.
Ready, ReadyWithWarnings, ConversionRequired, Incomplete, Unsupported, and
Cancelled do not move a still-visible focused element. Invalid and
OperationalFailure move focus to the outcome heading only when the previously
focused element belongs to the retired/collapsed progress layer; otherwise
they retain focus and rely on the assertive announcement. OutcomeCard exposes
that heading as internal `FrameworkElement FocusTarget`. RED tests cover both
branches, announcement rejection/dedupe, immediate footer sampling, stale
sender rejection, and footer-handler detach. Reduced motion changes no state,
tab order, announcement, or final geometry.

On navigation retirement, perform this literal order: detach motion-settings
and footer handlers; invalidate coordinator keys/interactions; call
`animationDriver.CancelAll()`; dispose driver, settings, and coordinator;
only then deactivate and dispose the ViewModel. Tests inject a settings change
and animation completion during retirement and prove neither reaches controls
or the onboarding shell.

- [ ] **Step 7: Run GREEN and commit**

```powershell
Invoke-PackagedModelInspectionTests `
  -TestFilter 'FullyQualifiedName~ModelInspectionMotionTests|FullyQualifiedName~ModelInspectionDisclosureTests|FullyQualifiedName~InspectionVisualStateGuardTests|FullyQualifiedName~ModelInspectionPageNavigationTests|FullyQualifiedName~OnboardingModelInspectionNavigationTests' `
  -ResultName 'task-09-motion-green.trx'
```

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation' 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls' 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml' 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs' 'IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/ModelInspectionMotionTests.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageNavigationTests.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingModelInspectionNavigationTests.cs' 'docs/reviews/model-inspection-cleanup-source-files.txt' 'docs/reviews/model-inspection-cleanup-inventory.md'
git commit -m "feat(model-inspection): add accessible composited motion"
```

## Task 10: Integrate the coordinator with page and onboarding lifecycle

**Files:**

- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionPresentationFactory.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageNavigationTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingModelInspectionNavigationTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/ModelInspectionPresentationFactoryTests.cs`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

- [ ] **Step 1: Replace the old churn test with RED lifecycle evidence**

Delete/rename `ProgressAndTerminalEvents_ReplaceTheCompleteFourCardSnapshot`. Add tests proving:

- OnNavigatedTo synchronously applies state 01 initial `(0,0)`, 0/5, Cancel disabled;
- Loaded still starts exactly once;
- notification bursts schedule one render;
- all four controls and five progress rows retain identity;
- Cancel changes only the action region;
- terminal retires progress before the banner becomes visible;
- same-outcome updates preserve disclosure/focus;
- Retry resets disclosure and rejects old progress/result/animation/announcement callbacks;
- navigation-away disposes coordinator before synchronous cancellation callbacks;
- footer status reaches the shell without advancing `CurrentStage`;
- old pages cannot update the shell and Frame back stack remains empty.

- [ ] **Step 2: Construct one coordinator per navigation lifetime**

Finish the migration started in Task 9: subscribe only to
`PropertyChanged(nameof(ModelInspectionViewModel.Snapshot))` and the existing
choose-another event. Command state is sampled from the snapshot/commands
during the coalesced render, not handled by three whole-page refresh callbacks.
Remove the three Task 4 compatibility factory adapters after no production or
test caller uses them. Update `ModelInspectionPresentationFactoryTests` to call
the unified snapshot/commands/progress-row API before removal.

- [ ] **Step 3: Apply only delta flags**

Assign a card `Presentation` only when its flag is present; update stable progress rows in place; forward footer state only when changed; schedule keyed announcements after the visual state applies.

- [ ] **Step 4: Preserve production Loaded and real worker journey**

The existing secondary-window Loaded test must still prove one service call. Extend the N-001 test to traverse the default `Frame` composition, observe every ordered Active/Completed stage, reach Ready, open/close Ready details, retain privacy-safe text, and find zero production-worker/fixture process after completion.

- [ ] **Step 5: Run GREEN and commit**

```powershell
Invoke-PackagedModelInspectionTests `
  -TestFilter 'FullyQualifiedName~ModelInspectionPageNavigationTests|FullyQualifiedName~OnboardingModelInspectionNavigationTests|FullyQualifiedName~ModelInspectionViewModelTests|FullyQualifiedName~ModelInspectionRenderCoordinatorTests|FullyQualifiedName~ModelInspectionPresentationFactoryTests' `
  -ResultName 'task-10-page-integration-green.trx'
```

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs' 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionPresentationFactory.cs' 'IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageNavigationTests.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Presentation/ModelInspectionPresentationFactoryTests.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingModelInspectionNavigationTests.cs' 'docs/reviews/model-inspection-cleanup-inventory.md'
git commit -m "feat(model-inspection): integrate stable figma rendering"
```

## Task 11: Add deterministic render, geometry, accessibility, and reference evidence

**Files:**

- Modify: `docs/ux/screenshots/model-inspection/reference/README.md`
- Create: `tests/TestFixtures/ModelInspectionVisual/References/visual-reference-manifest.json`
- Create: `tests/TestFixtures/ModelInspectionVisual/References/01-inspection-progress.png`
- Create: `tests/TestFixtures/ModelInspectionVisual/References/02-ready.png`
- Create: `tests/TestFixtures/ModelInspectionVisual/References/03-ready-expanded.png`
- Create: `tests/TestFixtures/ModelInspectionVisual/References/04-ready-with-warnings.png`
- Create: `tests/TestFixtures/ModelInspectionVisual/References/05-ready-with-warnings-expanded.png`
- Create: `tests/TestFixtures/ModelInspectionVisual/References/06-conversion-required.png`
- Create: `tests/TestFixtures/ModelInspectionVisual/References/07-conversion-required-expanded.png`
- Create: `tests/TestFixtures/ModelInspectionVisual/References/08-incomplete-package.png`
- Create: `tests/TestFixtures/ModelInspectionVisual/References/09-unsupported.png`
- Create: `tests/TestFixtures/ModelInspectionVisual/References/10-invalid.png`
- Create: `tests/TestFixtures/ModelInspectionVisual/References/11-invalid-expanded.png`
- Create: `tests/TestFixtures/ModelInspectionVisual/References/12-cancelled.png`
- Create: `tests/TestFixtures/ModelInspectionVisual/References/13-operational-failure.png`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/WinUiRenderHost.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/RenderedFrame.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/PixelDiff.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionVisualReferenceIntegrityTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionRenderHarnessTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionRenderedStateTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionAccessibilityTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionControlledAccessibilityTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual/ModelInspectionVisualRegressionTests.cs`
- Create: `scripts/model-inspection/Test-ModelInspectionVisualArtifactPrivacy.ps1`
- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/ModelInspectionVisualSourceContractTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

- [ ] **Step 1: Preserve immutable provenance before producing references**

Use the Task 1 durable SVG only as an independent board/geometry source.
Strict PNG goldens require exact 1440 x 1024 exports from all 13 Figma node
IDs; a crop or rerasterization of the flattened board can never be approved as
the strict golden oracle. If exact node export is unavailable, continue the
semantic/geometry/accessibility implementation but leave the strict pixel DoD
and controlled test explicitly blocked rather than manufacturing references.

The manifest records node ID, state number/name, dimensions, exact Figma-node
export provenance, and lowercase SHA-256. Each row also embeds its canonical
per-pixel tolerance mask as `base64-bitset-v1` with width, height, decoded byte
length, and lowercase SHA-256 of the decoded bitset. The integrity test decodes
and hashes every mask before comparison; no sidecar mask is implicit or
generated at test time. Package only the manifest and 13 PNGs into tests; do
not package the 8.2 MB board into the application.

- [ ] **Step 2: Prove `RenderTargetBitmap` feasibility first**

Create a visible 1440 x 1024 test root, wait for two dispatcher/composition frames without stopwatch assertions, capture it, encode PNG, and attach it to TRX. Tests require correct dimensions, attachment, identical-frame pass, and a one-pixel geometry mutation failure. Mark visual classes `[DoNotParallelize]`.

If AppContainer attachment/capture fails, stop and record the blocker. A
brokered output capability is a separately designed security boundary and is
not authorized as an automatic fallback in this plan.

- [ ] **Step 3: Add strict semantic geometry tests for all 13 states**

Use 13 DataRows plus four disclosure pairs. Assert effective-pixel bounds (one-pixel tolerance), exact palette resources, Inter family/size/weight, text wrapping bounds, bounded scroll viewports, control presence, and responsive hierarchy at 1440, 888, 600, and narrow widths.

- [ ] **Step 4: Add accessibility tests**

On the ordinary packaged runner, cover keyboard disclosure, focus retention,
tab order, disabled-action help, icon+text status, bounded list row names, one
polite meaningful-progress announcement, terminal once/attempt, same-attempt
hide/show suppression, retry reannouncement, complete HighContrast resource
dictionaries, wrapping/minimum-size behavior, and reduced-motion policy parity.
Do not pretend that setting `RequestedTheme` reproduces the Windows
High-Contrast service or that doubling a font size reproduces the OS text
scale. Actual High Contrast and 200% text-scale no-clipping checks belong to
the controlled/manual environment in Step 6.

- [ ] **Step 5: Add controlled pixel comparison**

Mark `ModelInspectionVisualRegressionTests` with both `[DoNotParallelize]` and
`[TestCategory("ModelInspectionVisualRegression")]`; use canonical safe
fixtures and 13 DataRows. Programmatic element bounds and solid interior color
samples are exact within the one-effective-pixel geometry tolerance. Semantic
text, FontFamily, face, weight, size, value, wrapping and bounds are asserted
separately. Pixel comparison may ignore only manifest-declared per-pixel glyph
and one-pixel vector-edge antialias masks whose embedded payloads/hashes are pinned;
it may not ignore whole text rectangles or use a global mismatch percentage,
and a greater-than-one-pixel edge displacement must fail.

Every passing controlled row writes and attaches its actual PNG, not only a
failure diff. Failures attach reference, actual and diff. The run writes one
privacy-safe manifest containing candidate commit, OS build, rasterizer,
resolution, DPI, text scale, theme, animation setting, 13 state results and
all artifact hashes. `Test-ModelInspectionVisualArtifactPrivacy.ps1` scans the
manifest and PNG metadata before retention and rejects user/machine names,
absolute paths and real-model metadata.

- [ ] **Step 6: Run focused evidence and commit**

On the normal development machine run:

```powershell
Invoke-PackagedModelInspectionTests `
  -TestFilter 'FullyQualifiedName~ModelInspectionVisualReferenceIntegrityTests|FullyQualifiedName~ModelInspectionRenderHarnessTests|FullyQualifiedName~ModelInspectionRenderedStateTests|FullyQualifiedName~ModelInspectionAccessibilityTests' `
  -ResultName 'task-11-render-accessibility-green.trx'
```

Only after preflight confirms 1440 x 1024, DPI 96, text scale 100%, Light theme, fixed OS build/rasterizer, and recorded animation setting, run:

```powershell
Invoke-PackagedModelInspectionTests `
  -TestFilter 'FullyQualifiedName~ModelInspectionVisualRegressionTests' `
  -ResultName 'task-11-visual-golden-green.trx'
```

In separate controlled passes, enable Windows High Contrast and set Windows
text scale to 200%, then run the accessibility/rendered-state classes and
retain the exact setting and result record. Controlled-only tests carry the
general `[TestCategory("ModelInspectionControlledOs")]` plus exactly one of
`ModelInspectionControlledHighContrast` or
`ModelInspectionControlledTextScale200`; each fails unless the real Windows
service reports its required state and inspects the actual rendered tree for
clipping/focus. Every Window/composition/disclosure
or OS-state class is `[DoNotParallelize]`; cleanup closes secondary windows and
restores changed settings in `finally`. Restore the operating-system settings
after each pass and record one manual Narrator/keyboard result beside the run
manifest.

```powershell
git add -- 'docs/ux/screenshots/model-inspection/reference' 'tests/TestFixtures/ModelInspectionVisual' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Visual' 'tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj' 'scripts/model-inspection/Test-ModelInspectionVisualArtifactPrivacy.ps1' 'tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/ModelInspectionVisualSourceContractTests.cs' 'docs/reviews/model-inspection-cleanup-source-files.txt' 'docs/reviews/model-inspection-cleanup-inventory.md'
git commit -m "test(model-inspection): add figma visual evidence"
```

## Task 12: Reconcile CI, documentation, and exact completion evidence

**Files:**

- Modify: `.github/workflows/build-and-test.yml`
- Create: `.github/workflows/model-inspection-visual-regression.yml`
- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/BuildWorkflowContractTests.cs`
- Modify: `tests/README.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/README.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/README.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Controls/README.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/README.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ViewModels/README.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/README.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/Controls/README.md`
- Modify: `docs/ux/screenshots/README.md`
- Modify: `docs/ux/accessibility/README.md`
- Modify: `docs/testing/Model-Inspection-Test-Completeness-Matrix.md`
- Create: `docs/development/Model-Inspection-Visual-Studio-Debug-Guide.md`
- Create: `docs/evidence/testing/Model-Inspection-Figma-Visual-Verification.md`
- Modify: `docs/superpowers/specs/2026-08-09-model-inspection-figma-fidelity-and-motion-design.md`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

- [ ] **Step 1: Parse the final packaged TRX before editing floors**

Run the hosted-equivalent packaged project with the controlled categories
excluded into `task-12-hosted-equivalent-packaged.trx`. Record actual total,
executed, passed, failed, skipped/not-executed, and exact method counts for
every hosted class. Never estimate the floor from source attributes and never
derive a filtered floor from an unfiltered TRX.

```powershell
Invoke-PackagedModelInspectionTests `
  -TestFilter 'TestCategory!=ModelInspectionVisualRegression&TestCategory!=ModelInspectionControlledOs' `
  -ResultName 'task-12-hosted-equivalent-packaged.trx'
```

- [ ] **Step 2: Protect the new tests in permanent CI**

Raise the packaged floor only to the observed count. Add exact class/count guards for:

- `ModelInspectionAssetContractTests`;
- `ModelInspectionDisplayTextPolicyTests`;
- `ModelInspectionViewSnapshotTests`;
- `ModelInspectionFigmaStatePresentationTests`;
- `InspectionProgressRowsTests`;
- all four card control classes;
- `ModelInspectionRenderCoordinatorTests`;
- `ModelInspectionMotionTests`;
- `ModelInspectionDisclosureTests`;
- visual reference integrity/rendered-state/accessibility/render-smoke classes.

The permanent `build-and-test.yml` uses the same two-category exclusion and
derives its floor from that exact hosted-equivalent TRX. Create a separate
manual `model-inspection-visual-regression.yml` for the controlled Windows x64
visual runner. It runs three separately preflighted campaigns: Light/96-DPI/
100%-text visual goldens, actual High Contrast accessibility, and actual 200%
text-scale clipping. It records OS build, 1440 x 1024, DPI, text scale, theme
and animation state for each rather than trying to satisfy incompatible OS
preconditions in one test invocation.

The controlled workflow verifies all 13 result rows and writes successful
actual PNGs, failure diffs, TRX and environment/result manifest beneath one
known test-results directory. It runs the visual privacy scanner before an
always-gated artifact upload and retains the artifact. The documentation
evidence file records run/head, settings, 13 reference/actual hashes and the
manual Narrator/keyboard result without embedding raw paths.

- [ ] **Step 3: Make workflow contracts fail before production CI can drift**

Add RED mutations for deleted class entries, lowered counts/floor, missing
`[DoNotParallelize]` on any Window/composition/disclosure/OS-state class,
accidental inclusion of either controlled category on an unconstrained runner,
missing exact 13-row controlled execution, missing successful-actual retention,
missing visual-reference integrity, and missing artifact privacy scan/upload
gating. Then restore and run the full Contracts project.

Immediately before that full Contracts run, regenerate/reconcile both cleanup registers so `CleanupInventoryContractTests` sees every new file.

```powershell
$contractProject = '.\tests\ContractTests\GraniteEdgeAI.ModelInspection.Contracts.Tests\GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj'
$contractResults = '.\TestResults\ModelInspectionFigma\Contracts'
dotnet test $contractProject `
  --configuration Release `
  --minimum-expected-tests 149 `
  --results-directory $contractResults `
  --report-trx `
  --report-trx-filename 'contracts-discovery.trx'
if ($LASTEXITCODE -ne 0) { throw 'Contracts discovery failed.' }

[xml]$contractTrx = Get-Content -LiteralPath (Join-Path $contractResults 'contracts-discovery.trx') -Raw
$contractFloor = [int]$contractTrx.TestRun.ResultSummary.Counters.total
if ($contractFloor -lt 150) { throw 'The new visual-source contract was not discovered.' }

dotnet test $contractProject `
  --configuration Release `
  --minimum-expected-tests $contractFloor
if ($LASTEXITCODE -ne 0) { throw 'Exact current Contracts verification failed.' }
```

Write `$contractFloor` into the workflow/contract guard in the same commit;
do not retain literal 149 as the final floor.

- [ ] **Step 4: Document the exact Visual Studio Debug journey**

The guide must show beginner-level clicks and checks:

1. open the `.slnx`, not Folder View;
2. choose `Debug` and `x64`;
3. set the WinUI project as startup;
4. select the packaged Local Machine profile;
5. stop stale `MSBuild.exe`/app/test processes if DLLs are locked;
6. restore/rebuild;
7. start with F5;
8. select a valid `.gguf`;
9. observe five smooth factual stages;
10. expand/collapse details;
11. exercise Cancel, Retry, and Choose another;
12. repeat with Windows animation effects disabled;
13. use keyboard-only disclosure/action navigation;
14. repeat at Windows text scale 200% and inspect for clipping;
15. enable Windows High Contrast and verify focus/status meaning;
16. run one recorded Narrator pass for progress and terminal announcements;
17. confirm Hardware Fit/conversion/report buttons say Coming later and do not claim execution.

- [ ] **Step 5: Update claims only from current evidence**

Mark all 13 presentations, four disclosures, stable-tree rendering, reduced motion, accessibility, and visual evidence complete only where their exact tests ran. Changes to the approved specification are limited to metadata/status and links to the plan/evidence; do not rewrite its normative behavior during implementation. Retain explicit nonclaims for OpenVINO, TurboQuant, GPU, full inference/context, Hardware Fit, conversion, report export, benchmarks, extracted MSIX, and hosted exact-head work that did not run.

- [ ] **Step 6: Run final local ladder**

Run, fail-fast:

1. asset/reference integrity focused tests;
2. all new presentation/view/render/motion/control/accessibility focused classes;
3. hosted-equivalent packaged WinUI suite with TRX and
   `TestCategory!=ModelInspectionVisualRegression&TestCategory!=ModelInspectionControlledOs`;
4. real N-001 page journey;
5. zero production-worker/fixture orphan check;
6. full Contracts suite;
7. cleanup verifier, expected 3/3;
8. Release x64 app build;
9. `git diff --check` and conflict-marker scan.

The controlled evidence ladder is separate: run the Light/100% visual-golden
TRX, the High Contrast category TRX, and the 200% text-scale category TRX under
their respective recorded preflights. A standard developer-machine run cannot
replace or combine those three campaigns; if a controlled environment is not
available, implementation may be green but the corresponding visual/
accessibility completion claims remain open.

Do not run a real external Granite model, GPU/native expansion, inference, or benchmark campaign for this UI completion.

- [ ] **Step 7: Independent review and final commit**

Request one spec-compliance review and one code-quality/accessibility review. Fix every Critical/Important finding, rerun affected RED/GREEN and the full ladder, then commit:

```powershell
git add -- '.github/workflows/build-and-test.yml' '.github/workflows/model-inspection-visual-regression.yml' 'tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/BuildWorkflowContractTests.cs' 'tests/README.md' 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection' 'IBM Granite with TurboQuant (Intel)/Features/Onboarding' 'docs/ux' 'docs/evidence/testing/Model-Inspection-Figma-Visual-Verification.md' 'docs/testing/Model-Inspection-Test-Completeness-Matrix.md' 'docs/development/Model-Inspection-Visual-Studio-Debug-Guide.md' 'docs/superpowers/specs/2026-08-09-model-inspection-figma-fidelity-and-motion-design.md' 'docs/reviews/model-inspection-cleanup-source-files.txt' 'docs/reviews/model-inspection-cleanup-inventory.md'
git commit -m "feat(model-inspection): complete figma fidelity and motion"
```

---

## Completion checklist

- [ ] All 13 Figma state identities render from truthful evidence or trusted terminals.
- [ ] Ready, Warning, Conversion, and Invalid collapsed/expanded pairs work.
- [ ] Initial state is 0/5 waiting and does not falsely claim execution.
- [ ] Four card instances and five progress-row instances stay stable during a run.
- [ ] One dispatcher render drains a notification burst and the newest truthful state wins.
- [ ] 160/180/240 ms motion endpoints are correct; reduced motion starts no animation.
- [ ] Stale attempt, revision, interaction, navigation, animation, and announcement callbacks are rejected.
- [ ] Terminal announcements occur once per attempt/outcome; same-attempt visibility changes do not repeat them.
- [ ] Desktop geometry, palette, Inter typography, responsive layout, keyboard, Narrator, high contrast, and 200% text scale have direct evidence.
- [ ] The real packaged N-001 journey still reaches all five stages and Ready with no orphan worker.
- [ ] Active recovery/navigation actions work; future actions are visibly disabled with `Coming later` help.
- [ ] No private path/template/exception/process detail is visible or retained in test artifacts.
- [ ] Permanent workflow floors and exact protected class counts equal final TRX evidence.
- [ ] Source list and cleanup inventory are sorted, unique, existing, and one-to-one.
- [ ] No OpenVINO, TurboQuant, Vulkan/GPU, full inference/context, Hardware Fit, conversion execution, report export, or benchmark implementation entered the delta.
