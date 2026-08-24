# Model Import and Drag-and-Drop Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add safe one-item picker and Explorer drag/drop selection for GGUF and existing OpenVINO directories, with identical lifecycle, bounded classification, privacy-safe diagnostics, and route dispatch.

**Architecture:** All entry points normalize into a single D1-owned `SelectionOperation`. The operation creates an immutable ID before cancelling the previous operation; a bounded classifier returns a route-local immutable result, and `ModelImportPage` applies it only when current. Existing GGUF quick scanning/request creation remains intact; OpenVINO and source folders use narrow route ports and I0 owns shared navigation.

**Tech Stack:** .NET 8, C# 12, WinUI 3 / Windows App SDK 2.2, MSTest AppContainer UI tests, Windows Storage item drag/drop APIs.

---

## Fixed file map

| Path | Responsibility |
| --- | --- |
| `IBM Granite with TurboQuant (Intel)/Features/ModelImport/Selection/*.cs` | framework-neutral input, operation, policy, classifier, diagnostic and immutable result contracts |
| `IBM Granite with TurboQuant (Intel)/Features/ModelImport/DragDropRoute/ModelImportDropHandler.cs` | WinUI StorageItems extraction, Copy semantics, deferral lifetime and page callback |
| `IBM Granite with TurboQuant (Intel)/Features/ModelImport/FileImport/PickerRoute/*.cs` | current picker routes adapted to normalized input without a picker API migration |
| `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelImportPage.xaml(.cs)` | target UI, lifecycle wiring, controlled state application and D1 events |
| `IBM Granite with TurboQuant (Intel)/Features/ModelImport/Controls/ImportModelCard.xaml(.cs)` | visual states, automation/live-region contract and responsive card layout |
| `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/{Selection,DragDropRoute,Controls}/*.cs` | D1 unit and native WinUI tests |
| `tests/TestFixtures/ModelImportSelection/*` | candidate-free directory/file shape fixtures |

I0-only requests, not D1 edits: app resources/project includes, shell route registration, ModelInspection/O1 invocation contract implementation, shared fixture catalogues, and final cross-feature end-to-end tests.

### Task 1: Define operation-safe, path-private selection contracts

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/Selection/ModelSelectionRoute.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/Selection/ModelSelectionOperationId.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/Selection/ModelSelectionInput.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/Selection/ModelSelectionDiagnostic.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/Selection/ModelSelectionResult.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/Selection/ModelSelectionContractTests.cs`

- [ ] **Step 1: Write failing immutability and redaction tests.**

```csharp
[TestMethod]
public void Failure_ExposesSafeNameButNeverSelectionPath()
{
    var result = ModelSelectionResult.Failure(
        new ModelSelectionOperationId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
        "granite.gguf", "selection-access-denied",
        "This model could not be opened. Check that it is available on this computer, then try again.");

    Assert.IsFalse(result.IsAccepted);
    Assert.AreEqual("granite.gguf", result.DisplayName);
    Assert.IsFalse(typeof(ModelSelectionResult).GetProperties()
        .Any(p => p.Name.Contains("Path", StringComparison.OrdinalIgnoreCase)));
}
```

- [ ] **Step 2: Run the contract test to confirm it fails.**

Run: `dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" --filter FullyQualifiedName~ModelSelectionContractTests --no-restore`

Expected: FAIL because selection contract types do not exist.

- [ ] **Step 3: Add minimal immutable contracts.**

```csharp
internal enum ModelSelectionRoute { Gguf, OpenVinoDirectory, SourceModelDirectory }
internal readonly record struct ModelSelectionOperationId(Guid Value);
internal sealed record ModelSelectionInput(
    string LocalPath, string DisplayName, bool IsFolder);
internal sealed record ModelSelectionDiagnostic(string Code, string Message);
internal sealed record ModelSelectionResult(
    ModelSelectionOperationId OperationId, bool IsAccepted,
    ModelSelectionRoute? Route, string DisplayName,
    ModelSelectionDiagnostic? Diagnostic)
{
    internal static ModelSelectionResult Failure(
        ModelSelectionOperationId id, string displayName, string code, string message) =>
        new(id, false, null, displayName, new(code, message));
}
```

`LocalPath` is internal-only and must never be added to `ModelSelectionResult`, diagnostics, presentation records, or public events.

- [ ] **Step 4: Run the contract test and formatting check.**

Run: `dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" --filter FullyQualifiedName~ModelSelectionContractTests --no-restore`

Expected: PASS.

- [ ] **Step 5: Commit the contract slice.**

```powershell
git add -- "IBM Granite with TurboQuant (Intel)/Features/ModelImport/Selection" "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/Selection/ModelSelectionContractTests.cs"
git commit -m "feat(model-import): define safe selection contracts"
```

### Task 2: Add current-operation and cancellation ownership

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/Selection/ModelSelectionOperation.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/Selection/ModelSelectionOperationTests.cs`

- [ ] **Step 1: Write failing replacement/cancel tests.**

```csharp
[TestMethod]
public void Replace_PublishesNewIdBeforeCancellingOldOperation()
{
    using var first = new ModelSelectionOperation();
    ModelSelectionOperation? current = first;
    using var second = new ModelSelectionOperation();
    ModelSelectionOperation? retired = Interlocked.Exchange(ref current, second);
    retired!.Retire();
    Assert.AreNotEqual(first.Id, second.Id);
    Assert.IsTrue(first.Token.IsCancellationRequested);
    Assert.IsFalse(second.Token.IsCancellationRequested);
}
```

- [ ] **Step 2: Run the focused test.**

Run: `dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" --filter FullyQualifiedName~ModelSelectionOperationTests --no-restore`

Expected: FAIL because `ModelSelectionOperation` does not exist.

- [ ] **Step 3: Implement the operation object and current-ID comparison.**

```csharp
internal sealed class ModelSelectionOperation : IDisposable
{
    private readonly CancellationTokenSource _source = new();
    internal ModelSelectionOperationId Id { get; } = new(Guid.NewGuid());
    internal CancellationToken Token => _source.Token;
    internal ModelSelectionOperation() { }
    internal void Retire() => _source.Cancel();
    public void Dispose() => _source.Dispose();
}
```

`ModelImportPage` will publish its new instance before constructing/cancelling the replacement, then accept a completion only when `ReferenceEquals(operation, _activeOperation)`.

- [ ] **Step 4: Run focused operation tests.**

Run: `dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" --filter FullyQualifiedName~ModelSelectionOperationTests --no-restore`

Expected: PASS.

- [ ] **Step 5: Commit.**

```powershell
git add -- "IBM Granite with TurboQuant (Intel)/Features/ModelImport/Selection/ModelSelectionOperation.cs" "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/Selection/ModelSelectionOperationTests.cs"
git commit -m "feat(model-import): make selection replacement attempt-safe"
```

### Task 3: Implement bounded, fail-closed folder classification policy

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/Selection/ModelSelectionLimits.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/Selection/IModelSelectionClassifier.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/Selection/BoundedModelSelectionClassifier.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/Selection/BoundedModelSelectionClassifierTests.cs`
- Create: `tests/TestFixtures/ModelImportSelection/OpenVinoComplete/model.xml`
- Create: `tests/TestFixtures/ModelImportSelection/OpenVinoComplete/model.bin`
- Create: `tests/TestFixtures/ModelImportSelection/AmbiguousIr/first.xml`
- Create: `tests/TestFixtures/ModelImportSelection/AmbiguousIr/first.bin`
- Create: `tests/TestFixtures/ModelImportSelection/AmbiguousIr/second.xml`
- Create: `tests/TestFixtures/ModelImportSelection/AmbiguousIr/second.bin`

- [ ] **Step 1: Write failing route/limit tests.**

```csharp
[DataTestMethod]
[DataRow("OpenVinoComplete", ModelSelectionRoute.OpenVinoDirectory)]
[DataRow("AmbiguousIr", null)]
public async Task ClassifyFolder_RequiresExactlyOneMatchedIrPair(
    string fixture, ModelSelectionRoute? expectedRoute)
{
    string path = Path.Combine(AppContext.BaseDirectory, "TestFixtures", "ModelImportSelection", fixture);
    var input = new ModelSelectionInput(path, fixture, true);
    ModelSelectionResult result = await classifier.ClassifyAsync(id, input, CancellationToken.None);
    Assert.AreEqual(expectedRoute, result.Route);
}

[TestMethod]
public async Task ClassifyFolder_StopsAt512DirectChildren()
{
    var result = await classifier.ClassifyAsync(inputWith513Files, CancellationToken.None);
    Assert.AreEqual("selection-enumeration-limit", result.Diagnostic!.Code);
}
```

- [ ] **Step 2: Run classifier tests to verify failure.**

Run: `dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" --filter FullyQualifiedName~BoundedModelSelectionClassifierTests --no-restore`

Expected: FAIL because classifier and policy are absent.

- [ ] **Step 3: Implement explicit policy and classifier.**

```csharp
internal static class ModelSelectionLimits
{
    internal const int MaximumDirectChildren = 512;
    internal const int MaximumRetainedNames = 32;
    internal const int MaximumMetadataBytes = 256 * 1024;
    internal static readonly TimeSpan MaximumElapsed = TimeSpan.FromSeconds(5);
}

internal interface IModelSelectionClassifier
{
    Task<ModelSelectionResult> ClassifyAsync(
        ModelSelectionOperationId operationId, ModelSelectionInput input,
        CancellationToken cancellationToken);
}
```

Reject reparse, UNC/network, device and unavailable-cloud candidates before opening metadata. Enumerate direct children only; detect exactly one case-insensitive stem-matched `.xml`/`.bin` pair; return `selection-ambiguous-folder` for more than one pair, and `selection-incomplete-openvino` for a lone member.

- [ ] **Step 4: Add source-folder shape tests and implement root-only recognition.**

```csharp
[TestMethod]
public async Task SourceFolder_RequiresRootConfigAndSafetensorsOrIndex()
{
    var result = await classifier.ClassifyAsync(id, sourceInput, CancellationToken.None);
    Assert.AreEqual(ModelSelectionRoute.SourceModelDirectory, result.Route);
}
```

Accept only readable root `config.json` plus root `.safetensors` or `model.safetensors.index.json`; reject missing index shards, custom-code requirement, unsupported task/architecture policy, malformed config/index, and nested-only artifacts. Do not deserialize arbitrary custom types or execute `auto_map`/remote code.

- [ ] **Step 5: Run all classifier tests and commit.**

Run: `dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" --filter FullyQualifiedName~BoundedModelSelectionClassifierTests --no-restore`

Expected: PASS, including limit, IR-pair, source-shape, incomplete, reparse, unavailable, changed-item and cancellation cases.

```powershell
git add -- "IBM Granite with TurboQuant (Intel)/Features/ModelImport/Selection" "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/Selection" "tests/TestFixtures/ModelImportSelection"
git commit -m "feat(model-import): classify local model selections safely"
```

### Task 4: Normalize existing picker outputs into the same input path

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/Selection/ModelSelectionInputNormalizer.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/FileImport/PickerRoute/GgufModelFilePicker.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/FileImport/PickerRoute/OpenVINOFolderPicker.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/Selection/ModelSelectionInputNormalizerTests.cs`

- [ ] **Step 1: Write failing picker normalization tests.**

```csharp
[TestMethod]
public void NormalizePickerFile_ProducesOneNonFolderInput()
{
    ModelSelectionInput input = normalizer.FromPickerPath(@"C:\Models\granite.gguf", false);
    Assert.AreEqual("granite.gguf", input.DisplayName);
    Assert.IsFalse(input.IsFolder);
}
```

- [ ] **Step 2: Run the normalizer test.**

Run: `dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" --filter FullyQualifiedName~ModelSelectionInputNormalizerTests --no-restore`

Expected: FAIL because normalizer does not exist.

- [ ] **Step 3: Implement one normalizer and adapt picker seams.**

```csharp
internal sealed class ModelSelectionInputNormalizer
{
    internal ModelSelectionInput FromPickerPath(string localPath, bool isFolder) =>
        new(localPath, Path.GetFileName(localPath.TrimEnd(Path.DirectorySeparatorChar)), isFolder);
}
```

Keep `Microsoft.Windows.Storage.Pickers` and `App.MainWindow.AppWindow.Id`. A picker cancellation returns no input; it must not create a failure card or mutate a valid selection.

- [ ] **Step 4: Run picker and current ModelImport tests.**

Run: `dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" --filter "FullyQualifiedName~ModelSelectionInputNormalizerTests|FullyQualifiedName~ModelFilePickerTests" --no-restore`

Expected: PASS.

- [ ] **Step 5: Commit.**

```powershell
git add -- "IBM Granite with TurboQuant (Intel)/Features/ModelImport/Selection/ModelSelectionInputNormalizer.cs" "IBM Granite with TurboQuant (Intel)/Features/ModelImport/FileImport/PickerRoute" "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/Selection/ModelSelectionInputNormalizerTests.cs"
git commit -m "refactor(model-import): normalize picker selections"
```

### Task 5: Add copy-only Explorer drop extraction with correct deferral lifetime

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/DragDropRoute/ModelImportDropHandler.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/DragDropRoute/ModelImportDropHandlerTests.cs`

- [ ] **Step 1: Write failing Drop contract tests.**

```csharp
[TestMethod]
public async Task Drop_OneStorageItem_ForwardsNormalizedInputAndCompletesDeferral()
{
    await handler.HandleDropAsync(sender, dragArgs);
    Assert.AreEqual(DataPackageOperation.Copy, dragArgs.AcceptedOperation);
    Assert.IsTrue(deferral.Completed);
}
```

- [ ] **Step 2: Run the Drop contract tests.**

Run: `dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" --filter FullyQualifiedName~ModelImportDropHandlerTests --no-restore`

Expected: FAIL because the handler does not exist.

- [ ] **Step 3: Implement drag precheck and asynchronous Drop handling.**

```csharp
internal async Task HandleDropAsync(DragEventArgs args, Func<ModelSelectionInput, Task> accept)
{
    var deferral = args.GetDeferral();
    try
    {
        if (!args.DataView.Contains(StandardDataFormats.StorageItems)) return;
        var items = await args.DataView.GetStorageItemsAsync();
        if (items.Count != 1) { await _reject("selection-multiple-items"); return; }
        args.AcceptedOperation = DataPackageOperation.Copy;
        await accept(_normalizer.FromStorageItem(items[0]));
    }
    finally { deferral.Complete(); }
}
```

`DragOver` never opens an item, starts classification, or permits Move. It provides valid/invalid visual state only. The Drop handler accepts `StorageFile` and `StorageFolder`; other storage item types are rejected with `selection-unsupported-file`.

- [ ] **Step 4: Add adverse tests and run.**

Cover no StorageItems, zero/multiple/mixed items, folder/file normalization, deferral completion after exception, and cancellation before StorageItems completion.

Run: `dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" --filter FullyQualifiedName~ModelImportDropHandlerTests --no-restore`

Expected: PASS.

- [ ] **Step 5: Commit.**

```powershell
git add -- "IBM Granite with TurboQuant (Intel)/Features/ModelImport/DragDropRoute" "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/DragDropRoute"
git commit -m "feat(model-import): accept one Explorer drop safely"
```

### Task 6: Wire the page to one operation pipeline without regressing GGUF

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelImportPage.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelInspectionRequestedEventArgs.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/OpenVinoInspectionRequestedEventArgs.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/SourceModelConversionRequestedEventArgs.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelImportInputParityTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelImportOperationLifecycleTests.cs`

- [ ] **Step 1: Write the failing picker/drop parity test.**

```csharp
[TestMethod]
public async Task PickAndDropEquivalentGguf_HaveSameAcceptedStateAndRoute()
{
    await pickerPage.SubmitInputAsync(input);
    await dropPage.SubmitInputAsync(input);
    Assert.AreEqual(pickerPage.CurrentRoute, dropPage.CurrentRoute);
    Assert.AreEqual(pickerPage.HasValidatedModel, dropPage.HasValidatedModel);
}
```

- [ ] **Step 2: Run parity/lifecycle tests.**

Run: `dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" --filter "FullyQualifiedName~ModelImportInputParityTests|FullyQualifiedName~ModelImportOperationLifecycleTests" --no-restore`

Expected: FAIL because the unified submit path does not exist.

- [ ] **Step 3: Route all entries through `SubmitInputAsync`.**

```csharp
internal async Task SubmitInputAsync(ModelSelectionInput input)
{
    ModelSelectionOperation next = new();
    ModelSelectionOperation? retired = Interlocked.Exchange(ref _activeOperation, next);
    retired?.Retire();
    retired?.Dispose();
    ClearSelectionAndShowValidating(input.DisplayName);
    ModelSelectionResult result = await _classifier.ClassifyAsync(next.Id, input, next.Token);
    if (!ReferenceEquals(next, _activeOperation) || next.Token.IsCancellationRequested) return;
    ApplyCurrentResult(input, result);
}
```

For GGUF, `ApplyCurrentResult` invokes the existing `ModelQuickScanner` with the same operation token and preserves current success/failure/cancel mapping. For OpenVINO/source it raises only the new narrow D1 intent events; it does not navigate a frame or implement O1 inspection.

- [ ] **Step 4: Add lifecycle regressions and run them.**

Cover second selection before first completes, user cancel then late success, navigation-away then late success, duplicate terminal result, folder result replacing GGUF success, deleted/modified GGUF before Continue, and no path-bearing event other than existing local GGUF inspection request.

Run: `dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" --filter "FullyQualifiedName~ModelImportInputParityTests|FullyQualifiedName~ModelImportOperationLifecycleTests|FullyQualifiedName~ModelImportNavigationRequestTests" --no-restore`

Expected: PASS.

- [ ] **Step 5: Commit.**

```powershell
git add -- "IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelImportPage.xaml.cs" "IBM Granite with TurboQuant (Intel)/Features/ModelImport"/*InspectionRequestedEventArgs.cs "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelImportInputParityTests.cs" "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelImportOperationLifecycleTests.cs"
git commit -m "feat(model-import): unify picker and drop selection lifecycle"
```

### Task 7: Add the accessible drop surface and terminal states

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelImportPage.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/Controls/ImportModelCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/Controls/ImportModelCard.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/Controls/ImportModelCardState.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/Controls/ModelImportDropAccessibilityTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/Controls/ModelImportResponsiveStateTests.cs`

- [ ] **Step 1: Write failing visual/automation tests.**

```csharp
[UITestMethod]
public void DropSurface_IsHitTestableAndDescribesPickerParity()
{
    var target = (Grid)page.FindName("ModelDropTarget");
    Assert.IsTrue(target.AllowDrop);
    Assert.IsNotNull(target.Background);
    StringAssert.Contains(AutomationProperties.GetHelpText(target), "Choose model file");
}
```

- [ ] **Step 2: Run UI tests to verify failure.**

Run: `dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" --filter "FullyQualifiedName~ModelImportDropAccessibilityTests|FullyQualifiedName~ModelImportResponsiveStateTests" --no-restore`

Expected: FAIL because the named drop surface and new states are absent.

- [ ] **Step 3: Add the target, picker parity, and live region.**

```xml
<Grid x:Name="ModelDropTarget" Background="Transparent" AllowDrop="True"
      DragOver="ModelDropTarget_DragOver" DragLeave="ModelDropTarget_DragLeave"
      Drop="ModelDropTarget_Drop"
      AutomationProperties.Name="Model drop area"
      AutomationProperties.HelpText="Optional. Choose model file or Choose model folder provides the same selection options.">
  <!-- existing ImportModelCard visual content -->
</Grid>
<TextBlock x:Name="SelectionAnnouncement" Visibility="Collapsed"
           AutomationProperties.LiveSetting="Polite" />
```

Expose buttons named exactly `Choose model file` and `Choose model folder`. Retain existing page heading, GGUF scan card, current success card, current `model-selection-changed` copy, and Continue button. Add controlled visual states for valid/invalid drag, access denied, unsupported, ambiguous folder, cancellation, and replacement.

- [ ] **Step 4: Add layout/theme/motion tests and run.**

Assert 44 px targets, status text plus icon, no horizontal scrollbar/clip at compact width and 200% text fixture, High Contrast system brush usage, and a static reduced-motion validation indication.

Run: `dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" --filter "FullyQualifiedName~ModelImportDropAccessibilityTests|FullyQualifiedName~ModelImportResponsiveStateTests|FullyQualifiedName~ModelImportPageCompositionTests" --no-restore`

Expected: PASS.

- [ ] **Step 5: Commit.**

```powershell
git add -- "IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelImportPage.xaml" "IBM Granite with TurboQuant (Intel)/Features/ModelImport/Controls" "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/Controls"
git commit -m "feat(model-import): add accessible model drop states"
```

### Task 8: Lock D1 privacy, ownership, and MVP verification

**Files:**
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/Selection/ModelImportPrivacyBoundaryTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/Selection/ModelImportOwnershipContractTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/README.md`

- [ ] **Step 1: Write failing boundary tests.**

```csharp
[TestMethod]
public void PresentationAndDiagnostics_ContainNoAbsolutePathBearingProperty()
{
    foreach (Type type in D1PresentationAndDiagnosticTypes)
        Assert.IsFalse(type.GetProperties().Any(p => p.Name.Contains("Path")));
}
```

- [ ] **Step 2: Run privacy/ownership tests.**

Run: `dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" --filter "FullyQualifiedName~ModelImportPrivacyBoundaryTests|FullyQualifiedName~ModelImportOwnershipContractTests" --no-restore`

Expected: FAIL until the tests enumerate the new contracts and source boundaries.

- [ ] **Step 3: Add architecture guards and update feature documentation.**

The ownership test fails if D1 production changes include `App.xaml`, any `.csproj`, `Features/Onboarding`, `Features/ModelInspection`, or non-D1 feature paths. The privacy test injects a temporary absolute path and verifies that all visible error/cancel/replacement text contains only the safe filename. Document the limits, route matrix, non-goals, Copy-only drop policy, and I0 requests in the ModelImport README.

- [ ] **Step 4: Run the complete D1 suite.**

Run: `dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" --filter FullyQualifiedName~Features.ModelImport --no-restore`

Expected: PASS with zero failed, skipped, or not-executed D1 tests.

- [ ] **Step 5: Build the D1 host and commit.**

Run: `dotnet build "IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj" -c Debug -p:Platform=x64 --no-restore`

Expected: build succeeds with no new warnings.

```powershell
git add -- "IBM Granite with TurboQuant (Intel)/Features/ModelImport/README.md" "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/Selection"
git commit -m "test(model-import): enforce import privacy and ownership"
```

### Task 9: Dispatch supported source-model folders without conversion

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/Selection/SourceModelSelectionPreflight.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/SourceModelConversionRequestedEventArgs.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelImportPage.xaml.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/Selection/SourceModelSelectionPreflightTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelImportSourceModelDispatchTests.cs`

- [ ] **Step 1: Write failing source-folder and no-execution tests.**

```csharp
[TestMethod]
public async Task ValidSourceFolder_RaisesConversionRequiredIntentExactlyOnce()
{
    await page.SubmitInputAsync(sourceFolderInput);
    page.RequestSourceModelConversion();
    Assert.AreEqual(1, conversionRequestCount);
    Assert.AreEqual(ModelSelectionRoute.SourceModelDirectory, captured.Route);
}

[TestMethod]
public async Task SourceFolderWithAutoMap_IsUnsupportedAndNeverStartsAProcess()
{
    ModelSelectionResult result = await classifier.ClassifyAsync(id, autoMapInput, CancellationToken.None);
    Assert.AreEqual("selection-custom-code-required", result.Diagnostic!.Code);
    Assert.AreEqual(0, processStarter.CallCount);
}
```

- [ ] **Step 2: Run the source-folder tests.**

Run: `dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" --filter "FullyQualifiedName~SourceModelSelectionPreflightTests|FullyQualifiedName~ModelImportSourceModelDispatchTests" --no-restore`

Expected: FAIL because the source preflight and conversion-required event do not exist.

- [ ] **Step 3: Implement the bounded source preflight and immutable intent.**

```csharp
internal sealed record SourceModelSelectionPreflight(
    bool HasRootConfig, bool HasWeightFileOrIndex,
    bool HasCompleteIndexedShards, bool RequiresCustomCode);

internal sealed class SourceModelConversionRequestedEventArgs : EventArgs
{
    internal SourceModelConversionRequestedEventArgs(ModelSelectionResult selection) => Selection = selection;
    internal ModelSelectionResult Selection { get; }
}
```

Read only root `config.json` and the bounded root index needed to establish the source shape. Reject `auto_map`, `trust_remote_code` requirements, malformed JSON, missing named shards, unsupported declared task/architecture, and files exceeding the existing metadata cap. Raise the intent only for the current immutable accepted operation; it contains no UI type, runtime type, diagnostic raw path, or executable action.

- [ ] **Step 4: Run source tests and current-operation regressions.**

Run: `dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" --filter "FullyQualifiedName~SourceModelSelectionPreflightTests|FullyQualifiedName~ModelImportSourceModelDispatchTests|FullyQualifiedName~ModelImportOperationLifecycleTests" --no-restore`

Expected: PASS, including cancellation/replacement before dispatch and no converter/process invocation.

- [ ] **Step 5: Commit the source-folder increment.**

```powershell
git add -- "IBM Granite with TurboQuant (Intel)/Features/ModelImport/Selection/SourceModelSelectionPreflight.cs" "IBM Granite with TurboQuant (Intel)/Features/ModelImport/SourceModelConversionRequestedEventArgs.cs" "IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelImportPage.xaml.cs" "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/Selection/SourceModelSelectionPreflightTests.cs" "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelImportSourceModelDispatchTests.cs"
git commit -m "feat(model-import): dispatch supported source model folders"
```

### Task 10: Add consent-bound Find downloaded models only after approval

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/DownloadedModels/DownloadedModelSearchPolicy.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/DownloadedModels/IDownloadedModelFinder.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/DownloadedModels/BoundedDownloadedModelFinder.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/DownloadedModels/DownloadedModelConsentDialog.xaml`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/DownloadedModels/DownloadedModelConsentDialog.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelImportPage.xaml(.cs)`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/DownloadedModels/DownloadedModelFinderTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/DownloadedModels/DownloadedModelConsentTests.cs`

- [ ] **Step 1: Freeze policy values and write consent-first failures.**

```csharp
[TestMethod]
public async Task DeclinedConsent_NeverEnumeratesAnyLocation()
{
    await page.FindDownloadedModelsAsync(consent: false);
    Assert.AreEqual(0, finder.EnumerateCallCount);
}

[TestMethod]
public async Task AcceptedConsent_StopsAtDeclaredItemAndTimeBounds()
{
    await finder.FindAsync(DownloadedModelSearchPolicy.Default, CancellationToken.None);
    Assert.IsTrue(finder.EnumerateCallCount <= 3);
    Assert.IsTrue(finder.VisitedItemCount <= 250);
}
```

- [ ] **Step 2: Run downloaded-model tests.**

Run: `dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" --filter "FullyQualifiedName~DownloadedModelFinderTests|FullyQualifiedName~DownloadedModelConsentTests" --no-restore`

Expected: FAIL because consent dialog and finder policy do not exist.

- [ ] **Step 3: Implement a bounded explicit-consent finder.**

```csharp
internal sealed record DownloadedModelSearchPolicy(
    IReadOnlyList<KnownFolderId> AllowedFolders, int MaximumLocations,
    int MaximumVisitedItems, TimeSpan MaximumElapsed)
{
    internal static DownloadedModelSearchPolicy Default { get; } =
        new([KnownFolderId.Downloads, KnownFolderId.Documents, KnownFolderId.Desktop], 3, 250, TimeSpan.FromSeconds(10));
}
```

The consent dialog names the three locations, single-run purpose, 250-item/10-second limit, no background scheduling, and Cancel choice. The finder runs only after affirmative dialog completion, does not recurse beyond one child level, checks cancellation between every item, returns safe display names only, and forgets all discovered paths on cancel/navigation/end of selection.

- [ ] **Step 4: Add cancellation, privacy, and no-background tests.**

```csharp
[TestMethod]
public async Task NavigationAway_CancelsSearchAndExposesNoDiscoveredPath()
{
    using var cancellation = new CancellationTokenSource();
    Task search = finder.FindAsync(DownloadedModelSearchPolicy.Default, cancellation.Token);
    cancellation.Cancel();
    await search;
    Assert.IsEmpty(finder.LastSafeResults);
}
```

Run: `dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" --filter "FullyQualifiedName~DownloadedModelFinderTests|FullyQualifiedName~DownloadedModelConsentTests|FullyQualifiedName~ModelImportPrivacyBoundaryTests" --no-restore`

Expected: PASS, including decline/no-scan, cancellation, timeout, limit, reparse/network rejection, no raw-path display, and no scheduled/background invocation.

- [ ] **Step 5: Request I0 project/resource integration and commit only D1 files.**

```powershell
git add -- "IBM Granite with TurboQuant (Intel)/Features/ModelImport/DownloadedModels" "IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelImportPage.xaml" "IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelImportPage.xaml.cs" "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/DownloadedModels"
git commit -m "feat(model-import): add consent-bound downloaded model search"
```

Do not modify a project file or `App.xaml`; supply I0 the exact XAML/page include and shared-resource request if the existing build item rules do not include the new dialog.

## I0 integration request checklist

- [ ] Freeze and expose the O1 OpenVINO/source invocation port; D1 must receive a test double first.
- [ ] Register route-specific navigation and failed-navigation recovery in `OnboardingShellPage` only after both destination contracts are accepted.
- [ ] Decide whether semantic Model Inspection resources become shared resources; register them centrally if approved.
- [ ] Add D1 fixture registration/project inclusions and run the cross-feature route suite.
- [ ] Implement the C0-approved path-minimized ModelInspectionHandoff separately; D1 must not substitute its local GGUF request for that handoff.

## Final implementation verification

Run, in order:

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" --filter FullyQualifiedName~Features.ModelImport --no-restore
dotnet build "IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj" -c Debug -p:Platform=x64 --no-restore
git diff --check
git status --short
```

Expected: every D1 test passes; Debug/x64 build succeeds without new warnings; whitespace check is clean; only intended D1 files and separately approved I0 integration files are changed.

Manual packaged acceptance, after I0 integration: drop one `.gguf`, one complete IR directory, multiple items, an inaccessible/local-only placeholder, and a changed file; verify Copy cursor, plain-language recovery, picker parity, cancellation/replacement/navigate-away safety, 200% text, Light/Dark/High Contrast, keyboard/Narrator flow, no model modification, and no network/listener/process launch.
