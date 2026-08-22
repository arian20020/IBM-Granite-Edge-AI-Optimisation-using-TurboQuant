# Onboarding Stage Indicator Cleanup and Tests Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the current onboarding stage-indicator changes compile, display the indicator in the onboarding shell, and protect the complete feature with structured packaged WinUI tests.

**Architecture:** Keep `OnboardingStage.ImportModel` as the canonical first-stage identifier while retaining “Choose model” as user-facing copy. Exercise the real `OnboardingStageIndicator`, shell, and application entry point through the existing packaged WinUI MSTest host; do not introduce a presenter or test-only production seam.

**Tech Stack:** C# 12, .NET 8, WinUI 3, Windows App SDK 2.2, MSTest 4.3.2, packaged app-container VSTest.

## Global Constraints

- Work only on the current branch and preserve every pre-existing staged, unstaged, and untracked user change.
- Do not commit, stage, reset, switch branches, discard files, push, or modify the PR.
- Keep `OnboardingStage.ImportModel = 1` as the first-stage API and keep “Choose model” as the visible and accessible label.
- Use `[UITestMethod]` and `[TestCategory("WinUI")]` for every test that constructs WinUI objects.
- Put tests under `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/`, mirroring the production feature structure.
- Do not add packages, a second test project, test-only production APIs, source-text assertions, or pixel/layout assertions.
- Build x64 with runtime `win-x64`; execute tests from the generated `.build.appxrecipe` with Visual Studio’s app-container VSTest runner. `dotnet test` is not valid completion evidence for this project.
- Successful application and test-project builds cover the `.csproj` XAML `Page` entries; do not add tests that parse project XML.

---

### Task 1: Repair and cover the stage indicator

**Files:**
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/Controls/OnboardingStageIndicatorTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/Controls/OnboardingStageIndicator.xaml.cs`
- Modify only for whitespace/comment cleanup: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/Controls/OnboardingStageIndicator.xaml`

**Interfaces:**
- Consumes: `OnboardingStage` values `ImportModel`, `InspectModel`, `CheckHardwareFit`, `ConfigureModel`, and `ReadyToChat`.
- Produces: A compiling `OnboardingStageIndicator.CurrentStage` dependency property whose default is `ImportModel`, plus real-control UI coverage for all stage transitions.

- [ ] **Step 1: Add the indicator tests before changing production code**

Create a `[TestClass]` named `OnboardingStageIndicatorTests` with these `[UITestMethod]` cases:

```csharp
Constructor_DisplaysImportModelAsInitialStage()
CurrentStage_ForEveryDefinedStage_UpdatesProgress()
MovingFromReadyToChatBackToInspectModel_RestoresFutureStates()
UndefinedCurrentStage_ThrowsArgumentOutOfRangeException()
```

Use `FindName` to inspect the five named `Border` step boxes, five value `TextBlock`s, five label `TextBlock`s, and four named connector `ScaleTransform`s. Compare brushes with the matching objects in `indicator.Resources`, not duplicated hexadecimal values.

The all-stage test must use literal expectations for:

```text
ImportModel      values 1,2,3,4,5  connectors 0,0,0,0  display "Choose model"
InspectModel     values ✓,2,3,4,5  connectors 1,0,0,0  display "Inspect model"
CheckHardwareFit values ✓,✓,3,4,5  connectors 1,1,0,0  display "Check hardware fit"
ConfigureModel   values ✓,✓,✓,4,5  connectors 1,1,1,0  display "Configure model"
ReadyToChat      values ✓,✓,✓,✓,5  connectors 1,1,1,1  display "Ready to chat"
```

For each case, verify completed/current/future box, value, and label tokens; `MODEL SETUP · STEP n OF 5`; and `AutomationProperties.Name`. The constructor case must also verify the fixed labels, help text, polite live setting, `IsHitTestVisible == false`, and `IsTabStop == false`. Use fresh controls when checking undefined values `0` and `6`.

- [ ] **Step 2: Run the test-project build and verify the existing RED state**

Run:

```powershell
dotnet build `
  '.\tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj' `
  --configuration Debug `
  --runtime win-x64 `
  -p:Platform=x64
```

Expected: build fails with `CS0117` because `OnboardingStage.ChooseModel` does not exist. Confirm the failure points to the dependency-property default and stage-display switch, not to test syntax.

- [ ] **Step 3: Apply the minimal compile fix and cleanup**

In `OnboardingStageIndicator.xaml.cs`:

```csharp
new PropertyMetadata(
    OnboardingStage.ImportModel,
    OnCurrentStageChanged)
```

and:

```csharp
OnboardingStage.ImportModel => "Choose model",
```

Replace the unnecessary type alias with a normal `using GraniteEdgeAI.Features.Onboarding;`. Keep useful intent comments while removing comments that only narrate getters, setters, assignments, or obvious control flow. Remove trailing whitespace from the XAML without changing its layout or design tokens.

- [ ] **Step 4: Build and run the focused indicator tests**

Build the test project with the command from Step 2, then run the generated recipe with:

```powershell
$recipe = (Resolve-Path `
  '.\tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe').Path
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vstest = & $vswhere -latest -products * `
  -find '**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' |
  Select-Object -First 1
& $vstest $recipe '/Platform:x64' `
  '/TestCaseFilter:FullyQualifiedName~OnboardingStageIndicatorTests'
```

Expected: the build and all four indicator test methods pass.

---

### Task 2: Wire and cover the onboarding shell and entry point

**Files:**
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingShellPageTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingEntryPointTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: The repaired `OnboardingStageIndicator` from Task 1 and the existing `ModelImportPage`.
- Produces: A visible named `StageIndicator` synchronized to `OnboardingShellPage.CurrentStage`, with regression coverage for initial shell content and the application’s root navigation.

- [ ] **Step 1: Add failing shell coverage and entry-point characterization**

Create `OnboardingShellPageTests` with:

```csharp
Constructor_PresentsModelImportAsInitialStage()
Constructor_ShowsPersistentIndicatorSynchronizedWithCurrentStage()
```

The first test must assert that `CurrentStage` is `ImportModel`, `StageFrame.SourcePageType` is `typeof(ModelImportPage)`, and `StageFrame.Content` is a `ModelImportPage`. The second must require a named `OnboardingStageIndicator` called `StageIndicator`, verify its stage matches the shell, and verify it is separate from `StageFrame.Content`.

Create `OnboardingEntryPointTests` with:

```csharp
MainWindow_Constructor_NavigatesRootFrameToOnboardingShell()
```

Construct `MainWindow` on the UI test thread, find the `rootFrame` through the window content’s XAML namescope, assert its content is `OnboardingShellPage`, and close the window in `finally`.

- [ ] **Step 2: Run the focused shell test and verify RED**

Build the test project, then run the generated recipe with:

```powershell
'/TestCaseFilter:FullyQualifiedName~OnboardingShellPageTests'
```

Expected: `Constructor_ShowsPersistentIndicatorSynchronizedWithCurrentStage` fails because the shell still contains only the empty `StageIndicatorHost`.

- [ ] **Step 3: Put the real indicator in the shell**

Add:

```xml
xmlns:controls="using:GraniteEdgeAI.Features.Onboarding.Controls"
```

Replace the empty `ContentControl` with:

```xml
<controls:OnboardingStageIndicator
    x:Name="StageIndicator"
    Grid.Row="1"
    HorizontalAlignment="Stretch" />
```

After assigning `CurrentStage = OnboardingStage.ImportModel` in the constructor, assign:

```csharp
StageIndicator.CurrentStage = CurrentStage;
```

Keep the nested stage page in `StageFrame` so the indicator remains outside frame navigation.

- [ ] **Step 4: Clean stale comments without changing behavior**

Update shell comments so they describe the now-present persistent indicator. Update the `MainWindow.xaml.cs` comment so it says the root frame loads `OnboardingShellPage`, and remove redundant blank lines. Keep the existing initial navigation and explicit navigation-failure exception.

- [ ] **Step 5: Build and run all onboarding tests**

Build the test project, then execute the generated recipe with:

```powershell
'/TestCaseFilter:FullyQualifiedName~Onboarding'
```

Expected: the indicator, shell, and entry-point test classes all pass with no failed tests.

---

### Task 3: Final verification and worktree audit

**Files:**
- Verify all files changed by Tasks 1 and 2.

**Interfaces:**
- Consumes: Completed production cleanup and onboarding tests.
- Produces: Evidence that the current uncommitted feature compiles, its packaged tests pass, and no unrelated files were changed.

- [ ] **Step 1: Run whitespace and XML checks**

```powershell
git diff --check
[xml](Get-Content -Raw `
  '.\IBM Granite with TurboQuant (Intel)\Features\Onboarding\Controls\OnboardingStageIndicator.xaml') |
  Out-Null
[xml](Get-Content -Raw `
  '.\IBM Granite with TurboQuant (Intel)\Features\Onboarding\OnboardingShellPage.xaml') |
  Out-Null
```

Expected: no output from `git diff --check`; both XML reads succeed.

- [ ] **Step 2: Build the application and test package**

```powershell
dotnet build `
  '.\IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj' `
  --configuration Debug `
  --runtime win-x64 `
  -p:Platform=x64

dotnet build `
  '.\tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj' `
  --configuration Debug `
  --runtime win-x64 `
  -p:Platform=x64
```

Expected: both builds succeed with zero errors.

- [ ] **Step 3: Run the full packaged test suite**

Run the complete generated `.build.appxrecipe` without a test-case filter and write a TRX file under `TestResults/Onboarding/Debug`.

Expected: all discovered tests pass. Read the TRX counters rather than inferring results from console truncation.

- [ ] **Step 4: Audit scope**

```powershell
git status --short
git diff --check
git diff --stat
```

Confirm that all original user changes remain present, the structured onboarding tests are included, and no files were staged or committed.
