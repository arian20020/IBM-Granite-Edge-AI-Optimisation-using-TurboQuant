# Compatibility Memory Estimate Presentation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show the existing memory estimate as a visible six-value breakdown and give explicit application/browser-tab recovery guidance only when no evaluated setup fits the current free-memory budget.

**Architecture:** Add a presentation-owned semantic summary derived from `CompatibilitySetupView`; the page receives bytes already owned by the compatibility result and only formats/renders them. Keep calculation ownership in the core estimator, methodology in the existing disclosure, and add no probe, policy, route, or navigation behavior.

**Tech Stack:** C# 12, .NET 8, WinUI 3 XAML, MSTest packaged WinUI tests, Visual Studio Community MSBuild/VSTest.

---

## File map

- `CompatibilityPresentation.cs`: define and carry the six-value summary.
- `CompatibilitySetupNarrative.cs`: group existing peak-phase components.
- `CompatibilityPresentationFactory.cs`: attach the summary and improve memory recovery copy.
- `CompatibilityPage.xaml`: add the light responsive breakdown card.
- `CompatibilityPage.xaml.cs`: populate and hide/show the stable controls.
- `CompatibilityRenderedStateTests.cs`: verify values, absence semantics, and state-specific copy.

### Task 1: Freeze the semantic estimate summary

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/Visual/CompatibilityRenderedStateTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Presentation/CompatibilityPresentation.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Presentation/CompatibilitySetupNarrative.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Presentation/CompatibilityPresentationFactory.cs`

- [ ] **Step 1: Write the failing semantic-summary test**

```csharp
[UITestMethod]
public void ConcludedPresentation_ExposesSemanticMemoryEstimate()
{
    CompatibilityPresentation presentation =
        Assert.IsNotNull(CompatibilityFixtureCatalogue.ById("CMP-010")).Presentation;
    CompatibilityEstimateSummary summary = Assert.IsNotNull(presentation.EstimateSummary);

    Assert.AreEqual(4_697_620_480UL, summary.ModelWeightsBytes);
    Assert.AreEqual(536_870_912UL, summary.KvCacheBytes);
    Assert.AreEqual(369_098_752UL, summary.RuntimeAndBufferBytes);
    Assert.AreEqual(560_359_014UL, summary.MarginForErrorBytes);
    Assert.AreEqual(6_163_949_158UL, summary.EstimatedPeakBytes);
    Assert.AreEqual(9_663_676_416UL, summary.SafeMemoryBytes);
    Assert.IsNull(CompatibilityPresentation.Empty.EstimateSummary);
}
```

- [ ] **Step 2: Build tests and verify RED**

Run packaged VSTest with filter `FullyQualifiedName~ConcludedPresentation_ExposesSemanticMemoryEstimate`.

Expected: FAIL because `CompatibilityEstimateSummary` and `EstimateSummary` do not exist.

- [ ] **Step 3: Add the summary type, grouping and factory assignment**

Add to `CompatibilityPresentation.cs`:

```csharp
internal sealed record CompatibilityEstimateSummary(
    ulong ModelWeightsBytes,
    ulong KvCacheBytes,
    ulong RuntimeAndBufferBytes,
    ulong MarginForErrorBytes,
    ulong EstimatedPeakBytes,
    ulong SafeMemoryBytes);
```

Add a required nullable `EstimateSummary` property and initialize it to null in `CompatibilityPresentation.Empty`.

Add to `CompatibilitySetupNarrative.cs`:

```csharp
internal static CompatibilityEstimateSummary EstimateSummary(CompatibilitySetupView setup)
{
    ulong weights = BytesFor(setup, ResourceComponentKind.Weights);
    ulong kvCache = BytesFor(setup, ResourceComponentKind.KvCache);
    ulong runtime = setup.Components
        .Where(component => component.Kind is not ResourceComponentKind.Weights
            and not ResourceComponentKind.KvCache)
        .Aggregate(0UL, (sum, component) => checked(sum + component.Bytes));

    return new CompatibilityEstimateSummary(
        weights, kvCache, runtime, setup.UncertaintyAllowanceBytes,
        setup.RequiredBytes, setup.SafeBudgetBytes);
}

private static ulong BytesFor(CompatibilitySetupView setup, ResourceComponentKind kind) =>
    setup.Components
        .Where(component => component.Kind == kind)
        .Aggregate(0UL, (sum, component) => checked(sum + component.Bytes));
```

Assign `EstimateSummary = CompatibilitySetupNarrative.EstimateSummary(setup)` in `WithSetup`.

- [ ] **Step 4: Rebuild and verify GREEN**

Run the same test filter. Expected: PASS, one discovered test.

- [ ] **Step 5: Commit**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Presentation/CompatibilityPresentation.cs' 'IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Presentation/CompatibilitySetupNarrative.cs' 'IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Presentation/CompatibilityPresentationFactory.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/Visual/CompatibilityRenderedStateTests.cs'
git commit -m "feat(compatibility): expose memory estimate summary"
```

### Task 2: Put recovery guidance in the prominent outcome

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/Visual/CompatibilityRenderedStateTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Presentation/CompatibilityPresentationFactory.cs`

- [ ] **Step 1: Write the failing copy test**

```csharp
[UITestMethod]
public void OnlyMemoryBlockedOutcome_TellsUserToCloseApplicationsAndTabs()
{
    CompatibilityPresentation blocked =
        Assert.IsNotNull(CompatibilityFixtureCatalogue.ById("CMP-030")).Presentation;
    StringAssert.Contains(blocked.OutcomeDetail, "Close unused applications and browser tabs");
    Assert.IsTrue(blocked.Recoveries.Any(recovery =>
        recovery.Detail.Contains("browser tabs", StringComparison.Ordinal)));

    foreach (CompatibilityFixture fixture in CompatibilityFixtureCatalogue.All
        .Where(fixture => fixture.Id != "CMP-030"))
    {
        Assert.IsFalse(fixture.Presentation.OutcomeDetail.Contains(
            "Close unused applications and browser tabs", StringComparison.Ordinal));
    }
}
```

- [ ] **Step 2: Run the focused test and verify RED**

Run packaged VSTest with filter `FullyQualifiedName~OnlyMemoryBlockedOutcome_TellsUserToCloseApplicationsAndTabs`.

Expected: FAIL because the prominent outcome omits the recovery instruction.

- [ ] **Step 3: Change only `NothingFits`**

```csharp
OutcomeDetail =
    "Every verified setup needs more memory than you can safely spare right now. "
    + "Close unused applications and browser tabs to free memory, then check again.",
```

Use this first recovery:

```csharp
new CompatibilityRecovery(
    "Free some memory, then check again",
    "Close unused applications and browser tabs. What matters is the memory free "
    + "right now, not how much memory your computer has in total."),
```

- [ ] **Step 4: Verify GREEN and commit**

Run the focused test; expect one PASS. Commit:

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Presentation/CompatibilityPresentationFactory.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/Visual/CompatibilityRenderedStateTests.cs'
git commit -m "fix(compatibility): explain how to free memory"
```

### Task 3: Render the estimate breakdown

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/Visual/CompatibilityRenderedStateTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/CompatibilityPage.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/CompatibilityPage.xaml.cs`

- [ ] **Step 1: Write failing rendered-state tests**

Apply fixture `CMP-010`; assert `EstimateBreakdownCard` is visible and the six named values are:

```csharp
Assert.AreEqual("4.4 GB", Element<TextBlock>(page, "EstimateWeightsValue").Text);
Assert.AreEqual("512 MB", Element<TextBlock>(page, "EstimateKvCacheValue").Text);
Assert.AreEqual("352 MB", Element<TextBlock>(page, "EstimateRuntimeValue").Text);
Assert.AreEqual("534 MB", Element<TextBlock>(page, "EstimateMarginValue").Text);
Assert.AreEqual("5.7 GB", Element<TextBlock>(page, "EstimatePeakValue").Text);
Assert.AreEqual("9 GB", Element<TextBlock>(page, "EstimateSafeValue").Text);
```

Apply `CompatibilityPresentation.Empty`; assert `EstimateBreakdownCard` is collapsed.

- [ ] **Step 2: Run both tests and verify RED**

Expected: FAIL because the named controls do not exist.

- [ ] **Step 3: Add the stable breakdown card**

Immediately after `BudgetDiagram`, add collapsed `EstimateBreakdownCard` with heading `Estimated memory breakdown`, supporting line `Calculated for the setup shown above — not measured`, and a two-column/three-row grid. Use existing compatibility surface, border, typography and radius resources. Use these fixed label/value pairs:

```text
Model weights       EstimateWeightsValue
KV cache            EstimateKvCacheValue
Runtime and buffers EstimateRuntimeValue
Margin for error    EstimateMarginValue
Estimated peak      EstimatePeakValue
Safe memory         EstimateSafeValue
```

Use equal star columns, Auto rows and no fixed width or horizontal scrolling.

- [ ] **Step 4: Populate without recalculation**

Call `ApplyEstimateSummary(presentation.EstimateSummary)` immediately after `ApplyBudget`. Implement:

```csharp
private void ApplyEstimateSummary(CompatibilityEstimateSummary? summary)
{
    EstimateBreakdownCard.Visibility = summary is null
        ? Visibility.Collapsed
        : Visibility.Visible;

    if (summary is null)
    {
        EstimateWeightsValue.Text = EstimateKvCacheValue.Text = string.Empty;
        EstimateRuntimeValue.Text = EstimateMarginValue.Text = string.Empty;
        EstimatePeakValue.Text = EstimateSafeValue.Text = string.Empty;
        return;
    }

    EstimateWeightsValue.Text = CompatibilityBudget.Describe(summary.ModelWeightsBytes);
    EstimateKvCacheValue.Text = CompatibilityBudget.Describe(summary.KvCacheBytes);
    EstimateRuntimeValue.Text = CompatibilityBudget.Describe(summary.RuntimeAndBufferBytes);
    EstimateMarginValue.Text = CompatibilityBudget.Describe(summary.MarginForErrorBytes);
    EstimatePeakValue.Text = CompatibilityBudget.Describe(summary.EstimatedPeakBytes);
    EstimateSafeValue.Text = CompatibilityBudget.Describe(summary.SafeMemoryBytes);
}
```

- [ ] **Step 5: Run all rendered-state tests and commit**

Run packaged VSTest with filter `FullyQualifiedName~CompatibilityRenderedStateTests`; expect every discovered test to pass. Commit:

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/CompatibilityPage.xaml' 'IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/CompatibilityPage.xaml.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/Visual/CompatibilityRenderedStateTests.cs'
git commit -m "feat(compatibility): show memory estimate breakdown"
```

### Task 4: Verify and stage the production package

**Files:**
- Verify only: all modified paths above.

- [ ] **Step 1: Run the complete core compatibility test project**

```powershell
dotnet test 'tests\UnitTests\GraniteEdgeAI.ModelHardwareCompatibility.Tests\GraniteEdgeAI.ModelHardwareCompatibility.Tests.csproj' --configuration Release
```

Expected: non-zero test count and zero failures.

- [ ] **Step 2: Rebuild packaged Debug/x64 production output**

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe' 'IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj' -restore -t:Rebuild -p:Configuration=Debug -p:Platform=x64 -m:1 -v:minimal
```

Expected: exit code 0 and no compile/XAML errors.

- [ ] **Step 3: Verify repository hygiene**

```powershell
git diff --check
git status --short
```

Expected: `git diff --check` emits nothing and no unrelated path is modified.

- [ ] **Step 4: Stage matching package files and relaunch**

Stop only the registered Granite Edge AI process. Copy the rebuilt main DLL and `resources.pri` into the registered Debug/x64 `AppX`. Copy every file named by the generated Model Inspection worker manifest from `obj/model-inspection-worker/Debug/win-x64` into `AppX/ModelInspection/Worker`, then copy the manifest last. Recompute every manifest SHA-256 and length; require zero mismatches. Launch:

```powershell
Start-Process explorer.exe -ArgumentList 'shell:AppsFolder\488d3892-c214-40c5-9a6a-1154c1e69fff_gqahnnh6hk88w!App'
```

Expected: app opens, Model Inspection remains functional, evaluated compatibility results show the breakdown, and memory-blocked results show the new guidance.

- [ ] **Step 5: Report verification**

No staging output belongs in Git. Confirm a clean worktree and report the three feature commits plus exact test totals.
