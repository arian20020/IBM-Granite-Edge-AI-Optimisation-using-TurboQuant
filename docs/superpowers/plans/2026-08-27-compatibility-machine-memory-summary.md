# Compatibility Machine Memory Summary Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show the exact computer-memory values behind every established GGUF and OpenVINO compatibility decision.

**Architecture:** The core evaluation returns an immutable `CompatibilityMachineMemory` derived from the production input and the same `SafetyPolicy.ProportionalV2()` used by fit assessment. Application presentation formats that data once, and the WinUI page renders a responsive card without recalculating it.

**Tech Stack:** C# 12, .NET 8, WinUI 3, Windows App SDK 2.2, MSTest, packaged AppContainer VSTest.

**Spec:** `docs/superpowers/specs/2026-08-27-compatibility-machine-memory-summary-design.md`

## Global Constraints

- Do not change estimators, candidate selection, safety policy, verdicts, optimisation planning, or navigation.
- Use the exact production hardware and fresh-resource snapshot consumed by the decision.
- Keep the contract route-neutral and identical for GGUF and OpenVINO.
- Never render fabricated figures for an absent, stale, or failed evaluation.
- Show capacities only; never expose processes, paths, host identity, diagnostics, or provider payloads.
- Preserve the established light visual baseline and responsive content width.

---

### Task 1: Carry exact machine-memory evidence

**Files:**
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Presentation/CompatibilityEvaluation.cs`
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Presentation/CompatibilityEngine.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Presentation/CompatibilityProductionInputTests.cs`

**Interfaces:**
- Consumes: `CompatibilityHardwareInput.InstalledSystemMemoryBytes`, `CompatibilityFreshResourcesInput.AvailableSystemMemoryBytes`, and `SafetyPolicy.ProportionalV2()`.
- Produces: nullable `CompatibilityEvaluation.MachineMemory` of type `CompatibilityMachineMemory`, created only through `CompatibilityMachineMemory.Create(ulong installedSystemMemoryBytes, ulong availableSystemMemoryBytes, ulong safetyReserveBytes, ulong safeModelBudgetBytes)`.

- [ ] **Step 1: Write the failing core test**

Add `ProductionEvaluation_CarriesExactMachineMemorySummary`:

```csharp
CompatibilityEvaluation evaluation =
    CompatibilityEngine.EvaluateProduction(ValidInput());
Assert.IsNotNull(evaluation.MachineMemory);
Assert.AreEqual(64 * GiB, evaluation.MachineMemory.InstalledSystemMemoryBytes);
Assert.AreEqual(48 * GiB, evaluation.MachineMemory.AvailableSystemMemoryBytes);
ulong reserve = (ulong)Math.Ceiling(48 * GiB * 0.10m);
Assert.AreEqual(reserve, evaluation.MachineMemory.SafetyReserveBytes);
Assert.AreEqual(48 * GiB - reserve, evaluation.MachineMemory.SafeModelBudgetBytes);
```

- [ ] **Step 2: Run it and require RED**

```powershell
dotnet test --project '.\tests\UnitTests\GraniteEdgeAI.ModelHardwareCompatibility.Tests\GraniteEdgeAI.ModelHardwareCompatibility.Tests.csproj' --configuration Release --filter 'FullyQualifiedName~ProductionEvaluation_CarriesExactMachineMemorySummary'
```

Expected: compilation fails because `MachineMemory` does not exist.

- [ ] **Step 3: Implement the minimal core contract**

Define `CompatibilityMachineMemory` with four read-only `ulong` properties and
a `Create` factory that rejects zero installed memory, available memory above
installed memory, reserve above available memory, and any safe budget unequal
to `available - reserve`. Add nullable `MachineMemory` to
`CompatibilityEvaluation`, and attach it only after a current production
evaluation. Derive reserve using
`SafetyPolicy.ProportionalV2().AvailableMemoryReserveFor(available)` and safe
budget using `TrySubtract`; do not copy the formula.

- [ ] **Step 4: Run focused and complete core suites**

Run the focused command, followed by the same command without `--filter`.
Require non-zero discovered tests and zero failures.

- [ ] **Step 5: Commit**

```powershell
git add -- 'shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Presentation/CompatibilityEvaluation.cs' 'shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Presentation/CompatibilityEngine.cs' 'tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Presentation/CompatibilityProductionInputTests.cs'
git commit -m 'feat(compatibility): carry machine memory summary'
```

### Task 2: Format the machine-memory presentation

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Presentation/CompatibilityPresentation.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Presentation/CompatibilityPresentationFactory.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/ViewModels/CompatibilityViewModel.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/CompatibilityPresentationFactoryTests.cs`

**Interfaces:**
- Consumes: `CompatibilityEvaluation.MachineMemory`.
- Produces: nullable `CompatibilityMachineMemoryPresentation` containing four `CompatibilityFact` values.

- [ ] **Step 1: Write the failing presentation test**

Create an established evaluation with `16 GiB installed`, `6 GiB available`,
`0.6 GiB reserve`, and `5.4 GiB safe`. Pass it to
`CompatibilityPresentationFactory.From(CompatibilityEvaluation)` and assert
the labels are exactly `Installed RAM`, `Available now`, `Safety reserve`, and
`Safe for this model`; assert values use `CompatibilityBudget.Describe`.

- [ ] **Step 2: Build the packaged tests and require RED**

```powershell
dotnet build '.\tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj' --configuration Debug -p:Platform=x64 -p:RuntimeIdentifier=win-x64
$vs = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe'
$recipe = '.\tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe'
& $vs $recipe /Tests:MachineMemorySummary_FormatsExactEvaluationValues
```

Expected: failure because the overload and presentation do not exist.

- [ ] **Step 3: Implement formatting and preserve it through ViewModel states**

Add nullable `MachineMemory` to `CompatibilityPresentation.Empty`. Add factory
overloads accepting `CompatibilityEvaluation`; format every byte value with
`CompatibilityBudget.Describe`. Update initial completion, preference changes,
and optional optimisation paths to retain the same summary.

- [ ] **Step 4: Repeat focused VSTest and require PASS**

Require one discovered test and zero failures.

- [ ] **Step 5: Commit**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Presentation/CompatibilityPresentation.cs' 'IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Presentation/CompatibilityPresentationFactory.cs' 'IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/ViewModels/CompatibilityViewModel.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/CompatibilityPresentationFactoryTests.cs'
git commit -m 'feat(compatibility): present machine memory statistics'
```

### Task 3: Render the responsive This computer card

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/CompatibilityPage.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/CompatibilityPage.xaml.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/Visual/CompatibilityRenderedStateTests.cs`

**Interfaces:**
- Consumes: `CompatibilityPresentation.MachineMemory`.
- Produces: `MachineMemoryCard` and `MachineMemoryFactsGrid`, visible only when evidence exists.

- [ ] **Step 1: Write failing rendered-state tests**

Apply a presentation containing four facts and assert the card is visible with
the exact labels and values. Apply `CompatibilityPresentation.Empty` and assert
it is collapsed. At a 560-pixel layout, require two columns, two rows, and
wrapping text.

- [ ] **Step 2: Run the focused packaged test and require RED**

Rebuild the package using Task 2's command, then run:

```powershell
& $vs $recipe /Tests:MachineMemoryCard_RendersExactValuesAndCollapsesWithoutEvidence
```

Expected: failure because `MachineMemoryCard` does not exist.

- [ ] **Step 3: Implement the stable responsive card**

Insert the full-width light card between `ModelSummaryCard` and `OutcomeCard`.
Reuse existing compatibility surfaces, borders, radii, typography, fact tiles,
and spacing. Populate a stable four-child grid in `Apply`; wide mode uses four
columns and compact mode uses two columns and two rows. Perform no arithmetic
in code-behind.

- [ ] **Step 4: Run regressions**

```powershell
dotnet test --project '.\tests\UnitTests\GraniteEdgeAI.ModelHardwareCompatibility.Tests\GraniteEdgeAI.ModelHardwareCompatibility.Tests.csproj' --configuration Release
dotnet build '.\tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj' --configuration Debug -p:Platform=x64 -p:RuntimeIdentifier=win-x64
& $vs $recipe /TestCaseFilter:'FullyQualifiedName~ModelHardwareCompatibility'
git diff --check
```

Expected: non-zero discovered tests, zero failures, and no diff-check output.

- [ ] **Step 5: Build and launch exactly one self-contained instance**

```powershell
dotnet build '.\IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj' --configuration Debug -p:Platform=x64 -p:RuntimeIdentifier=win-x64 -p:WindowsAppSDKSelfContained=true -p:OpenVinoOfficialWorkerPackagingRequired=false -p:GenerateAppxPackageOnBuild=false
$app = '.\IBM Granite with TurboQuant (Intel)\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\IBM Granite with TurboQuant (Intel).exe'
$process = Start-Process -FilePath $app -WorkingDirectory (Split-Path $app) -PassThru
Start-Sleep -Seconds 6
$process.Refresh()
if ($process.HasExited -or -not $process.Responding) { throw 'The app did not remain responsive.' }
```

Confirm the established compatibility page shows all four values and **Check
again** refreshes **Available now**.

- [ ] **Step 6: Commit**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/CompatibilityPage.xaml' 'IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/CompatibilityPage.xaml.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/Visual/CompatibilityRenderedStateTests.cs'
git commit -m 'feat(compatibility): show computer memory statistics'
```
