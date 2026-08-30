# Direct Verified Model Download Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Model Import recommended-model card download, resume, verify, store and inspect one of five slider-selected official IBM Granite GGUF artifacts.

**Architecture:** A compiled immutable catalogue maps the existing slider bands to pinned artifacts. A bounded HTTP transport and app-owned model library support resumable streaming, while a download service performs exact size/hash verification and atomic publication. A coordinator owns presentation state and exposes a path-private, one-time completion claim that `ModelImportPage` routes through `SubmitInputAsync` and guarded Model Inspection.

**Tech Stack:** C# 12, .NET 8, WinUI 3/Windows App SDK, `HttpClient`, `System.Text.Json`, `System.Security.Cryptography`, `Windows.Storage.ApplicationData`, Windows network-cost APIs, MSTest packaged app-container VSTest.

**Spec:** `docs/superpowers/specs/2026-08-28-direct-verified-model-download-design.md`

## Global Constraints

- Implement from exact source `4748fe04f19afdf6b27c4c12502b84db325e7294`, tree `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91`, plus this plan/spec.
- Preserve the existing Model Import card hierarchy, light styling, five labels and continuous slider.
- Do not bundle GGUF files, add a Granite Edge cloud service, accept arbitrary URLs, or add a non-IBM model catalogue.
- The compiled catalogue has exactly five entries at pinned revision `51ce07a9c9cfa971ca359d9625836bf8a4a1b61f`.
- Exact file byte length and SHA-256 must pass before atomic publication or Model Import handoff.
- Picker, Explorer drop and verified download all converge on `ModelImportPage.SubmitInputAsync`; no direct Model Inspection request is synthesized.
- Download completion events and navigation payloads never contain a local path.
- Explicit cancel deletes the partial; interruption preserves a durable resumable checkpoint.
- Restart auto-resumes only on a confirmed unrestricted connection; metered, roaming and unknown-cost connections require explicit confirmation.
- Add only the `internetClient` package capability.
- No new console, helper process, inbound listener, telemetry, credentials, cookies or hardware/user identity transmission.
- Use TDD for every behavioral task and commit in focused units.

---

## File Structure

### Production files to create

- `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/ModelDownloadCatalogEntry.cs` — immutable artifact identity and validation.
- `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/PinnedGraniteModelCatalog.cs` — exact five-entry catalogue and slider mapping.
- `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/ModelDownloadContracts.cs` — operation IDs, progress, results, verified-model and presentation contracts.
- `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/IModelDownloadTransport.cs` — bounded streaming transport contract and response lifetime.
- `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/HttpModelDownloadTransport.cs` — production HTTPS/range/redirect implementation.
- `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/ModelDownloadPartialState.cs` — strict persisted checkpoint schema.
- `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/AppModelLibrary.cs` — app-owned paths, leases, checkpoints, free-space checks and atomic publication.
- `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/IModelDownloadService.cs` — service boundary.
- `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/ResumableVerifiedModelDownloadService.cs` — transfer/resume/verify transaction.
- `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/IModelDownloadNetworkPolicy.cs` — offline/unrestricted/confirmation-required boundary.
- `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/WindowsModelDownloadNetworkPolicy.cs` — Windows connection-cost implementation.
- `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/ModelDownloadCoordinator.cs` — one-operation lifecycle, presentation and opaque completion claim.
- `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/ModelDownloadComposition.cs` — production-only composition.

### Production files to modify

- `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/ModelDownloadCard.xaml` — dynamic quantisation/size and bounded progress/error controls.
- `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/ModelDownloadCard.xaml.cs` — attach coordinator and render states.
- `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelImportPage.xaml.cs` — inject/compose coordinator and subscribe to path-private completion.
- `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelImportPage.Selection.cs` — claim, submit and guarded automatic inspection; retire stale automatic handoffs.
- `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/README.md` — replace prototype nonclaims with implemented boundary and remaining non-goals.
- `IBM Granite with TurboQuant (Intel)/Package.appxmanifest` — add exactly `internetClient`.

### Test files to create or modify

- Create `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelDownload/PinnedGraniteModelCatalogTests.cs`.
- Create `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelDownload/HttpModelDownloadTransportTests.cs`.
- Create `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelDownload/AppModelLibraryTests.cs`.
- Create `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelDownload/ResumableVerifiedModelDownloadServiceTests.cs`.
- Create `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelDownload/ModelDownloadCoordinatorTests.cs`.
- Create `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelDownload/ModelImportDownloadedModelIntegrationTests.cs`.
- Create `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelDownload/ModelDownloadPackageContractTests.cs`.
- Modify `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelDownload/ModelDownloadCardTests.cs`.

---

### Task 1: Freeze the five-entry catalogue

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/ModelDownloadCatalogEntry.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/PinnedGraniteModelCatalog.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelDownload/PinnedGraniteModelCatalogTests.cs`

**Interfaces:**
- Produces: `ModelDownloadCatalogEntry`, `PinnedGraniteModelCatalog.Entries`, `PinnedGraniteModelCatalog.ForSliderValue(double)`, `ModelDownloadCatalogEntry.DownloadSizeText`.
- Consumes: no production code beyond BCL validation and `Uri`.

- [ ] **Step 1: Write the failing exact-catalogue tests**

```csharp
[TestMethod]
public void Entries_PinExactOfficialArtifacts()
{
    (string Label, string File, long Bytes, string Sha)[] expected =
    [
        ("Maximum efficiency", "granite-4.0-h-micro-Q2_K.gguf", 1226247840, "e60b313fc0ce2a0a2c3903f4399ac61fe1048c38f219bcbd9f7a708e168b8ead"),
        ("Efficient", "granite-4.0-h-micro-Q3_K_M.gguf", 1555472032, "bb684177d546a2c6d3aec9cc591fc8fa75d9c16a27e798cbb2400fe52d9b7e29"),
        ("Balanced", "granite-4.0-h-micro-Q4_K_M.gguf", 1942564512, "c698c78e895740f0e707eb7f8e92894f83f6d5b3f2f2f0b446dfe9635fa0063e"),
        ("High capability", "granite-4.0-h-micro-Q5_K_M.gguf", 2273455776, "69857575412143ea74d66e4d54ad70ec420d42452eadfff4a7cc043fe445ed4c"),
        ("Maximum capability", "granite-4.0-h-micro-Q8_0.gguf", 3397676704, "a009111abf2865b7aad1e66326a6c772cddc29bccd22898f470292068b27bb59")
    ];

    CollectionAssert.AreEqual(expected, PinnedGraniteModelCatalog.Entries
        .Select(x => (x.PreferenceLabel, x.FileName, x.ExpectedByteLength, x.ExpectedSha256))
        .ToArray());
}

[DataTestMethod]
[DataRow(0d, "Q2_K")]
[DataRow(19.999d, "Q2_K")]
[DataRow(20d, "Q3_K_M")]
[DataRow(40d, "Q4_K_M")]
[DataRow(60d, "Q5_K_M")]
[DataRow(80d, "Q8_0")]
[DataRow(100d, "Q8_0")]
public void ForSliderValue_MapsEveryBoundary(double value, string quantisation) =>
    Assert.AreEqual(quantisation, PinnedGraniteModelCatalog.ForSliderValue(value).Quantisation);
```

- [ ] **Step 2: Build the packaged test project and prove the tests fail to compile**

Run:

```powershell
dotnet build '.\tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj' --configuration Debug --runtime win-x64 -p:Platform=x64
```

Expected: non-zero build because the catalogue types do not exist.

- [ ] **Step 3: Implement immutable validated catalogue types**

```csharp
internal sealed record ModelDownloadCatalogEntry(
    string Id,
    string PreferenceLabel,
    string Quantisation,
    double MinimumSliderValue,
    double MaximumSliderValue,
    bool IncludesMaximum,
    string RepositoryId,
    string Revision,
    string FileName,
    long ExpectedByteLength,
    string ExpectedSha256)
{
    internal Uri ResolveUri => new(
        $"https://huggingface.co/{RepositoryId}/resolve/{Revision}/{FileName}?download=true",
        UriKind.Absolute);

    internal string DownloadSizeText =>
        $"{ExpectedByteLength / 1_000_000_000d:0.00} GB";
}
```

`PinnedGraniteModelCatalog` must instantiate the exact table in the spec, validate all strings/ranges once, return a read-only list and reject slider values outside 0–100 or NaN/infinity.

- [ ] **Step 4: Run the focused packaged filter and confirm non-zero passing discovery**

Build the appxrecipe and run VSTest with:

```text
/TestCaseFilter:"FullyQualifiedName~PinnedGraniteModelCatalogTests"
```

Expected: every newly added catalogue test passes and the TRX reports a non-zero total.

- [ ] **Step 5: Commit the catalogue**

```powershell
git add 'IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/ModelDownloadCatalogEntry.cs' `
        'IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/PinnedGraniteModelCatalog.cs' `
        'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelDownload/PinnedGraniteModelCatalogTests.cs'
git commit -m 'feat(model-import): pin downloadable Granite models'
```

---

### Task 2: Define download contracts and app-owned storage

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/ModelDownloadContracts.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/ModelDownloadPartialState.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/AppModelLibrary.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelDownload/AppModelLibraryTests.cs`

**Interfaces:**
- Consumes: `ModelDownloadCatalogEntry` from Task 1.
- Produces: `ModelDownloadOperationId`, `ModelDownloadProgress`, `ModelDownloadResult`, `VerifiedDownloadedModel`, `ModelDownloadResumeInfo`, `AppModelLibrary` and `ModelDownloadLibraryLease`.

- [ ] **Step 1: Write failing storage and checkpoint tests**

Cover fixed-root containment, unsafe catalogue filename rejection, exclusive lease, state round-trip, atomic state replacement, crash-ahead truncation, short partial rejection, exact final verification, insufficient-space decision and same-volume atomic publication. Use a unique test directory and delete only that exact resolved root in cleanup.

```csharp
[TestMethod]
public async Task RecoverAsync_TruncatesBytesBeyondDurableCheckpoint()
{
    using var root = new TemporaryModelLibraryRoot();
    AppModelLibrary library = new(root.Path, _ => long.MaxValue);
    ModelDownloadCatalogEntry entry = TestCatalog.Balanced;
    await using (Stream partial = await library.OpenPartialWriteAsync(
        entry,
        truncateToLength: 0,
        CancellationToken.None))
    {
        await partial.WriteAsync(new byte[12]);
    }
    await library.WriteCheckpointAsync(
        entry,
        new ModelDownloadPartialState(1, entry.Id, entry.Revision, entry.FileName,
            entry.ExpectedByteLength, entry.ExpectedSha256, 8, "\"v1\"", DateTimeOffset.UtcNow),
        CancellationToken.None);

    ModelDownloadResumeInfo? resume = await library.RecoverAsync(entry, CancellationToken.None);

    Assert.AreEqual(8, resume!.DownloadedBytes);
    Assert.AreEqual(8, await library.GetPartialLengthAsync(entry, CancellationToken.None));
}
```

- [ ] **Step 2: Run the focused build and record RED**

Expected: compile failure because storage/contracts are absent.

- [ ] **Step 3: Implement strict contracts**

```csharp
internal readonly record struct ModelDownloadOperationId(Guid Value)
{
    internal static ModelDownloadOperationId CreateNew() => new(Guid.NewGuid());
}

internal enum ModelDownloadResultKind
{
    Completed,
    AlreadyAvailable,
    Interrupted,
    Failed
}

internal enum ModelDownloadStage
{
    Idle,
    Preparing,
    Downloading,
    Verifying,
    Completed,
    Interrupted,
    Failed
}

internal sealed record ModelDownloadProgress(
    ModelDownloadStage Stage,
    long DownloadedBytes,
    long TotalBytes);

internal sealed record VerifiedDownloadedModel(
    string LocalPath,
    string DisplayName,
    string CatalogId,
    long ByteLength,
    string Sha256);

internal sealed record ModelDownloadResult(
    ModelDownloadResultKind Kind,
    VerifiedDownloadedModel? VerifiedModel,
    string? ErrorCode);

internal sealed record ModelDownloadResumeInfo(
    string CatalogId,
    long DownloadedBytes,
    long TotalBytes,
    string? EntityTag);

internal sealed record ModelDownloadPartialState(
    int SchemaVersion,
    string CatalogId,
    string Revision,
    string FileName,
    long ExpectedByteLength,
    string ExpectedSha256,
    long DurableByteLength,
    string? EntityTag,
    DateTimeOffset UpdatedAtUtc);
```

Keep `LocalPath` internal and never include it in `ToString`, events, presentation state or diagnostic records.

- [ ] **Step 4: Implement `AppModelLibrary`**

Production `CreateDefault()` derives the root from `ApplicationData.Current.LocalFolder.Path` and fixed `GraniteEdgeAI/Models` children. The internal test constructor accepts a temporary root and disk-space delegate. Use fixed catalogue-derived names, `Path.GetFullPath`, root containment with separator boundaries, reparse checks, exclusive streams, `File.Move(..., overwrite: false)` on the same volume and atomic JSON checkpoint replacement.

- [ ] **Step 5: Run `AppModelLibraryTests` through packaged VSTest**

Expected: non-zero passing count; test cleanup confirms no file remains outside each test root.

- [ ] **Step 6: Commit contracts and storage**

```powershell
git add 'IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/ModelDownloadContracts.cs' `
        'IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/ModelDownloadPartialState.cs' `
        'IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/AppModelLibrary.cs' `
        'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelDownload/AppModelLibraryTests.cs'
git commit -m 'feat(model-import): add secure model library storage'
```

---

### Task 3: Implement bounded HTTPS and range transport

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/IModelDownloadTransport.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/HttpModelDownloadTransport.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelDownload/HttpModelDownloadTransportTests.cs`

**Interfaces:**
- Consumes: `ModelDownloadCatalogEntry`.
- Produces: `IModelDownloadTransport.OpenAsync(ModelDownloadCatalogEntry, long, string?, CancellationToken)` and disposable `ModelDownloadTransportResponse`.

- [ ] **Step 1: Write failing fake-handler transport tests**

Test initial GET, exact Range and If-Range headers, response-header streaming, five-redirect ceiling, relative redirects, HTTPS downgrade rejection, label-boundary host rejection, authorization/cookie absence, cancellation and response disposal.

```csharp
internal interface IModelDownloadTransport
{
    Task<ModelDownloadTransportResponse> OpenAsync(
        ModelDownloadCatalogEntry entry,
        long offset,
        string? entityTag,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Run the focused test build and record RED**

Expected: missing transport types.

- [ ] **Step 3: Implement manual redirect and streaming response handling**

Use `HttpClientHandler.AllowAutoRedirect = false`, `HttpCompletionOption.ResponseHeadersRead`, a bounded connect timeout and a linked inactivity timeout refreshed by successful reads in the service. Accept only HTTPS and label-safe exact/subdomains of the approved Hugging Face distribution domains. Do not set credentials or cookies. Dispose every intermediate response before following its Location.

- [ ] **Step 4: Run `HttpModelDownloadTransportTests` and confirm GREEN**

Expected: non-zero passing count with no external internet dependency.

- [ ] **Step 5: Commit transport**

```powershell
git add 'IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/IModelDownloadTransport.cs' `
        'IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/HttpModelDownloadTransport.cs' `
        'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelDownload/HttpModelDownloadTransportTests.cs'
git commit -m 'feat(model-import): add bounded model download transport'
```

---

### Task 4: Implement resumable verification transaction

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/IModelDownloadService.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/ResumableVerifiedModelDownloadService.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelDownload/ResumableVerifiedModelDownloadServiceTests.cs`

**Interfaces:**
- Consumes: catalogue entry, `IModelDownloadTransport`, `AppModelLibrary`.
- Produces:

```csharp
internal interface IModelDownloadService
{
    Task<ModelDownloadResult> DownloadAsync(
        ModelDownloadCatalogEntry entry,
        IProgress<ModelDownloadProgress> progress,
        CancellationToken cancellationToken);

    Task<ModelDownloadResumeInfo?> GetResumeInfoAsync(
        ModelDownloadCatalogEntry entry,
        CancellationToken cancellationToken);

    Task DiscardPartialAsync(
        ModelDownloadCatalogEntry entry,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 1: Write failing RED tests for every transaction boundary**

Use small deterministic byte arrays whose SHA-256 is supplied through a test entry. Cover correct 200, correct 206 resume, safe 200 restart, invalid Content-Range, truncated, oversized, wrong digest, cancellation preservation, explicit discard, existing-valid reuse, existing-invalid rejection, disk-space failure, exclusive lease and privacy-safe error codes.

```csharp
[TestMethod]
public async Task DownloadAsync_WrongDigestNeverPublishes()
{
    byte[] bytes = [1, 2, 3, 4];
    TestDownloadFixture fixture = TestDownloadFixture.Create(bytes, expectedSha256: new string('0', 64));

    ModelDownloadResult result = await fixture.Service.DownloadAsync(
        fixture.Entry,
        new Progress<ModelDownloadProgress>(),
        CancellationToken.None);

    Assert.AreEqual(ModelDownloadResultKind.Failed, result.Kind);
    Assert.AreEqual("download-integrity-failed", result.ErrorCode);
    Assert.IsFalse(File.Exists(fixture.FinalPath));
}
```

- [ ] **Step 2: Run the focused filter and confirm RED**

Expected: missing service/implementation.

- [ ] **Step 3: Implement transfer, checkpoint and resume logic**

Stream in a fixed reusable buffer, reject byte count above expected immediately, throttle progress, flush/checkpoint at bounded byte/time intervals, and preserve partial state on `OperationCanceledException` or recoverable network interruption. Validate `206 Content-Range`; on safe `200` after a range request, truncate and restart. Never append an inconsistent response.

- [ ] **Step 4: Implement final verification and publication**

Require exact length, compute SHA-256 from byte zero with `SHA256.HashDataAsync`, compare using `CryptographicOperations.FixedTimeEquals`, then call the library's same-volume atomic publish. Return `VerifiedDownloadedModel` only after reopening and confirming final containment/length.

- [ ] **Step 5: Run service/storage/transport tests and confirm GREEN**

Expected: all three focused classes pass with non-zero discovery.

- [ ] **Step 6: Commit the verified service**

```powershell
git add 'IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/IModelDownloadService.cs' `
        'IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/ResumableVerifiedModelDownloadService.cs' `
        'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelDownload/ResumableVerifiedModelDownloadServiceTests.cs'
git commit -m 'feat(model-import): verify resumable model downloads'
```

---

### Task 5: Add coordinator, network-cost policy and production composition

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/IModelDownloadNetworkPolicy.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/WindowsModelDownloadNetworkPolicy.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/ModelDownloadCoordinator.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/ModelDownloadComposition.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelDownload/ModelDownloadCoordinatorTests.cs`

**Interfaces:**
- Consumes: `PinnedGraniteModelCatalog`, `IModelDownloadService`, network policy.
- Produces: `ModelDownloadCoordinator.State`, `StateChanged`, path-free `VerifiedModelAvailable`, `StartAsync`, `CancelAsync`, `ResumeAsync`, `RecoverAsync`, `RetireAutomaticHandoff`, `TryClaimVerifiedModel`.

- [ ] **Step 1: Write failing coordinator lifecycle tests**

Test one active operation, slider snapshot, monotonic progress, explicit-cancel discard, interruption preservation, unrestricted auto-resume, metered confirmation, offline state, stale progress/completion rejection, one-time claim and no local path in event/state/`ToString`.

```csharp
internal sealed class VerifiedModelAvailableEventArgs(
    ModelDownloadOperationId operationId,
    string displayName) : EventArgs
{
    internal ModelDownloadOperationId OperationId { get; } = operationId;
    internal string DisplayName { get; } = displayName;
}

internal enum ModelDownloadConnectionKind
{
    Offline,
    Unrestricted,
    ConfirmationRequired
}

internal interface IModelDownloadNetworkPolicy
{
    ModelDownloadConnectionKind GetCurrentConnectionKind();
}
```

- [ ] **Step 2: Run focused tests and confirm RED**

- [ ] **Step 3: Implement state machine and atomic one-time claim**

Use a lock for state/claim transitions and never invoke events while holding it. Store the verified model privately by current operation ID. `TryClaimVerifiedModel` removes the value atomically after one successful claim. `RetireAutomaticHandoff` prevents navigation but does not delete a valid completed model.

- [ ] **Step 4: Implement Windows connection policy and default composition**

Map no profile to Offline; `Unrestricted` cost to Unrestricted; fixed/variable, roaming, over-limit or unknown cost to ConfirmationRequired. `ModelDownloadComposition.CreateDefault()` creates the default library, bounded `HttpClient`, transport, service, policy and coordinator exactly once per page lifetime.

- [ ] **Step 5: Run coordinator tests and confirm GREEN**

- [ ] **Step 6: Commit orchestration**

```powershell
git add 'IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/IModelDownloadNetworkPolicy.cs' `
        'IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/WindowsModelDownloadNetworkPolicy.cs' `
        'IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/ModelDownloadCoordinator.cs' `
        'IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/ModelDownloadComposition.cs' `
        'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelDownload/ModelDownloadCoordinatorTests.cs'
git commit -m 'feat(model-import): coordinate model download lifecycle'
```

---

### Task 6: Make the existing card truthful and interactive

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/ModelDownloadCard.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/ModelDownloadCard.xaml.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelDownload/ModelDownloadCardTests.cs`

**Interfaces:**
- Consumes: coordinator state and catalogue entries.
- Produces: card button/slider interactions only; no network or navigation code.

- [ ] **Step 1: Add failing UI tests for dynamic selection and states**

Verify initial Balanced/Q4_K_M/1.94 GB, all band labels/quantisations/sizes, start click, slider disabled during active transfer, progress values, verification indicator, interrupted Resume/Discard, bounded error copy, cancel, accessibility names/live region and preserved narrow/wide layout.

- [ ] **Step 2: Run the `ModelDownloadCardTests` packaged filter and confirm RED**

- [ ] **Step 3: Extend XAML without restructuring the card**

Add `x:Name="QuantizationValueText"` to the existing value. Add a collapsed status region immediately above the existing button containing `DownloadStatusText`, `DownloadProgressBar`, `DownloadProgressText` and a secondary `DiscardDownloadButton`. Keep existing spacing, colors, corner radii and action-button resources.

- [ ] **Step 4: Bind card interactions through code-behind to the coordinator**

Implement `Attach(ModelDownloadCoordinator)`, detach old event handlers, render state on UI thread, snapshot the slider through coordinator start, and map primary actions to Start/Cancel/Resume. Do not place `HttpClient`, paths or file APIs in the control.

- [ ] **Step 5: Run card and catalogue filters and confirm GREEN**

- [ ] **Step 6: Commit the card**

```powershell
git add 'IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/ModelDownloadCard.xaml' `
        'IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/ModelDownloadCard.xaml.cs' `
        'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelDownload/ModelDownloadCardTests.cs'
git commit -m 'feat(model-import): activate recommended model card'
```

---

### Task 7: Route verified downloads through Model Import and Model Inspection

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelImportPage.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelImportPage.Selection.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelDownload/ModelImportDownloadedModelIntegrationTests.cs`

**Interfaces:**
- Consumes: path-free completion event and one-time coordinator claim.
- Produces: existing `SubmitInputAsync` and `TryRequestModelInspection` behavior only.

- [ ] **Step 1: Write failing page integration tests**

Inject a fake coordinator/service and classifier. Prove successful current completion claims once, calls the classifier/quick scan through `SubmitInputAsync`, raises `ModelInspectionRequested` exactly once, and never includes the path in event arguments. Prove failed scan, stale operation, picker selection, drop, second download and navigation retirement cannot navigate.

- [ ] **Step 2: Run the focused filter and confirm RED**

- [ ] **Step 3: Add coordinator injection and path-private completion handling**

Append an optional coordinator parameter to the existing internal constructor; production defaults to `ModelDownloadComposition.CreateDefault()`. Attach the existing card after `InitializeComponent`. The async completion handler calls `TryClaimVerifiedModel`, then `SubmitInputAsync`, checks current accepted GGUF state, and finally calls `TryRequestModelInspection` only when the coordinator still authorizes that operation.

- [ ] **Step 4: Retire automatic handoff at competing entry points**

Before picker submission, Explorer-drop submission, a different download and `OnNavigatedFrom`, call the coordinator's retirement method. Keep picker and drop delegates converging on `SubmitInputAsync`; do not duplicate classification or quick scan.

- [ ] **Step 5: Run ModelDownload, ModelImport and Onboarding packaged filters**

```text
/TestCaseFilter:"FullyQualifiedName~ModelDownload|FullyQualifiedName~ModelImport|FullyQualifiedName~Onboarding"
```

Expected: non-zero discovery and zero failures.

- [ ] **Step 6: Commit the integrated handoff**

```powershell
git add 'IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelImportPage.xaml.cs' `
        'IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelImportPage.Selection.cs' `
        'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelDownload/ModelImportDownloadedModelIntegrationTests.cs'
git commit -m 'feat(model-import): inspect verified downloads'
```

---

### Task 8: Package capability, contracts and documentation

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Package.appxmanifest`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelDownload/ModelDownloadPackageContractTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/README.md`

**Interfaces:**
- Consumes: completed feature.
- Produces: minimal outbound network package declaration and accurate current-state documentation.

- [ ] **Step 1: Write failing package/source contract tests**

Read the manifest and project items. Assert exactly one `internetClient`, no `privateNetworkClientServer`, no server capability, no GGUF content item, no arbitrary URL setting, and no model path in public event types. Assert all five filenames/digests remain present once in the catalogue.

- [ ] **Step 2: Run the focused filter and confirm RED on missing capability**

- [ ] **Step 3: Add the minimal manifest capability and update README**

Add `<Capability Name="internetClient" />` beside existing capabilities. Document implemented catalogue, network/storage/verification/resume flow, quick-scan handoff and remaining non-goals. Remove obsolete claims that download functionality does not exist.

- [ ] **Step 4: Run package contract and all download filters**

Expected: non-zero passing discovery.

- [ ] **Step 5: Commit packaging/documentation**

```powershell
git add 'IBM Granite with TurboQuant (Intel)/Package.appxmanifest' `
        'IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload/README.md' `
        'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelDownload/ModelDownloadPackageContractTests.cs'
git commit -m 'docs(model-import): document verified downloads'
```

---

### Task 9: Full verification and controlled real-source acceptance

**Files:**
- Modify only if a newly reproduced defect requires a focused correction and RED test.
- Evidence remains uncommitted under `TestResults/ModelDownload/`.

**Interfaces:**
- Consumes: Tasks 1–8.
- Produces: verified branch and handoff evidence.

- [ ] **Step 1: Run complete Debug x64 restore and build**

```powershell
$testProject='.\tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj'
dotnet restore $testProject --runtime win-x64 -p:Platform=x64
dotnet build $testProject --configuration Debug --no-restore --runtime win-x64 -p:Platform=x64
dotnet build '.\IBM Granite with TurboQuant (Intel).sln' --configuration Debug -p:Platform=x64 -m:1
```

Expected: zero errors.

- [ ] **Step 2: Run the packaged focused campaign**

Resolve the generated `.build.appxrecipe` and Visual Studio `vstest.console.exe` as documented in `tests/README.md`. Run serially with `tests/runsettings/OneWorker.runsettings` and:

```text
/TestCaseFilter:"FullyQualifiedName~ModelDownload|FullyQualifiedName~ModelImport|FullyQualifiedName~Onboarding"
```

Parse the TRX and require non-zero executed count, zero failed and explicit skipped count.

- [ ] **Step 3: Run the full packaged UnitTests campaign**

Run the same recipe without a filter. Require non-zero discovery and record exact passed/failed/skipped totals.

- [ ] **Step 4: Build the production package and scan package contents**

Build Release x64 using the repository's documented packaging properties. Inspect the package file list and prove no `.gguf`, `.partial`, download-state JSON or model library content is packaged. Confirm the manifest adds only `internetClient`.

- [ ] **Step 5: Run privacy, duplication and diff checks**

```powershell
rg -n "LocalPath|PartialPath|https://|Authorization|Cookie" `
  'IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelDownload' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelImportPage.Selection.cs'
git diff --check
git status --short
```

Review every match and prove paths/URLs remain internal, never UI/event/navigation text. Scan packaged content and production registrations for duplicates.

- [ ] **Step 6: Run one controlled real Q2_K acceptance download when network/policy permits**

Use the application UI, select Maximum efficiency, confirm 1.23 GB, download from the pinned official source, interrupt once, restart, resume, complete, and independently compute exact byte count and SHA-256. Confirm the file enters quick scan and Model Inspection automatically. Do not commit the model, path or raw network logs.

Expected identity:

```text
Bytes: 1226247840
SHA-256: e60b313fc0ce2a0a2c3903f4399ac61fe1048c38f219bcbd9f7a708e168b8ead
```

If external network or UCL policy blocks this single acceptance case, record the exact external boundary while retaining all deterministic/packaged results; do not weaken transport or integrity validation.

- [ ] **Step 7: Request code review, correct accepted findings test-first, and rerun affected/full verification**

Use `superpowers:requesting-code-review`. For each accepted defect, add or identify the RED test, apply one minimal correction and rerun the focused plus full campaigns.

- [ ] **Step 8: Require a clean, fully committed worktree**

Every accepted review correction must be committed with its owning production/test paths before this check. Run `git status --short`; expected output is empty. If it is not empty, stop and return to the task that owns those exact paths rather than creating a catch-all commit.

- [ ] **Step 9: Report final identity and integration options**

Report branch, base, tip, tree, changed paths, exact test counts, package result, real-source acceptance disposition, remaining blockers and no-main-merge status. Use `superpowers:finishing-a-development-branch` to select merge/PR/keep-worktree handling.
