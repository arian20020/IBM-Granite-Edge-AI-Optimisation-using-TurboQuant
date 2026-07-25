# GGUF Quick Scanner Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the secure, asynchronous GGUF v3 header/metadata quick scanner, its deterministic generated fixtures, packaged WinUI MSTest coverage, and beginner/testing documentation.

**Architecture:** Keep the approved `ModelImportPage -> ModelQuickScanner -> GgufQuickScanner -> ModelQuickScanResult` flow. `GgufQuickScanner` performs one sequential metadata pass, consumes every official value type, retains only known display fields plus at most 64 pre-architecture context candidates, and converts only known structural/operational failures into stable results.

**Tech Stack:** C# 12, .NET 8, WinUI 3, Windows App SDK 2.2, `FileStream`, `ReadExactlyAsync`, `BinaryPrimitives`, strict `UTF8Encoding`, MSTest 4.3.2, PowerShell 5.1 fixture generators, single-project MSIX, Visual Studio app-container `vstest.console.exe`.

## Global Constraints

- Work only on `feature/winui-shell-model-import` in `C:\Users\Arian\source\repos\IBM-Granite-TurboQuant-Intel`.
- Do not push, merge, publish, or modify a remote pull request.
- Preserve existing user work and the current `ModelQuickScanResult` contract.
- Do not add an external GGUF package, interface-for-mocking, general object model, tensor parser, or synchronous binary reader.
- Keep `MaxMetadataEntryCount = 1_000_000` and `MaxMetadataKeyByteLength = 65_535`.
- Add `MaxMetadataStringByteLength = 16 * 1024 * 1024`, `MaxArrayElementCount = 1_000_000`, `MaxTotalArrayElementCount = 4_000_000`, `MaxArrayNestingDepth = 8`, and `MaxPendingContextCandidateCount = 64`.
- Preserve `OperationCanceledException` in `GgufQuickScanner`; only `ModelQuickScanner` converts requested cancellation to `Cancelled`.
- Generate every binary fixture with the checked-in PowerShell generators; never hand-edit `.gguf` files.
- Every direct scanner test uses `[TestMethod]`, `[TestCategory("Unit")]`, XML documentation, Arrange–Act–Assert, a fresh scanner, and an explicit `File.Exists` deployment assertion when it uses a fixture.
- Execute packaged tests through the generated `.build.appxrecipe`; `dotnet test` is not completion evidence.
- Keep production application launch profiles in the application project and the `GraniteEdgeAI.UnitTests (Package)` profile in the test project.

---

## File map

**Production**

- Modify `IBM Granite with TurboQuant (Intel)/Features/ModelImport/QuickScan/GgufQuickScanner.cs`: complete header/metadata parsing, safety policy, extraction, failures, and operational boundary.
- Modify `IBM Granite with TurboQuant (Intel)/Features/ModelImport/QuickScan/ModelQuickScanner.cs`: formatting cleanup only unless router tests expose a cancellation defect.
- Restore `IBM Granite with TurboQuant (Intel)/Properties/launchSettings.json`: packaged and unpackaged application profiles.

**Tests and fixtures**

- Modify `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/QuickScan/GgufQuickScannerTests.cs`: complete fixture-driven direct scanner suite and typed JSON assertions.
- Modify `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/QuickScan/ModelQuickScannerTests.cs`: remove the temporary `NotImplementedException` case and add real GGUF routing/cancellation cases.
- Modify `tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj`: wildcard-copy all GGUF, malformed, and expected JSON fixtures.
- Keep `tests/UnitTests/GraniteEdgeAI.UnitTests/Properties/launchSettings.json`: packaged test profile.
- Modify `tests/TestFixtures/Generate-GgufMetadataFixtures.ps1`: add all official scalar writers plus valid and safety fixtures.
- Regenerate `tests/TestFixtures/GGUF/*.gguf`, `tests/TestFixtures/Malformed/*.gguf`, and `tests/TestFixtures/ExpectedMetadata/*.json`.
- Modify `tests/TestFixtures/fixture-manifest.json`, `tests/TestFixtures/GGUF/README.md`, `tests/TestFixtures/Malformed/README.md`, and `tests/TestFixtures/ExpectedMetadata/README.md`: record the generated inventory and intent.

**Build and documentation**

- Modify `.github/workflows/build-and-test.yml`: include `tests/TestFixtures` in sparse checkout and choose the latest compatible VSTest installation.
- Create `docs/development/GGUF-Quick-Scanner-Beginner-Guide.md`.
- Create `docs/evidence/testing/GGUF-Quick-Scanner-Test-Report.md`.
- Keep `docs/superpowers/specs/2026-07-24-gguf-quick-scanner-design.md` and this plan synchronized with implemented names and limits.

## Packaged test command template

Every RED/GREEN command below uses the same checked-in project and generated recipe. Replace only `$configuration`, `$filter`, and `$trxName` with the exact values stated in the cycle.

```powershell
$testProject = 'tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj'
$configuration = 'Debug'
$recipe = "tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\$configuration\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe"
$results = "TestResults\GGUF-Quick-Scanner\$configuration"
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vstest = & $vswhere -latest -products * -find '**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' |
    Select-Object -First 1

dotnet build $testProject `
    --configuration $configuration `
    --no-restore `
    --runtime win-x64 `
    -p:Platform=x64

New-Item -ItemType Directory -Force -Path $results | Out-Null

& $vstest `
    (Resolve-Path -LiteralPath $recipe).Path `
    /Platform:x64 `
    /TestCaseFilter:"$filter" `
    /Logger:"trx;LogFileName=$trxName" `
    /ResultsDirectory:(Resolve-Path -LiteralPath $results).Path
```

For a one-test cycle, `$filter` is the exact fully qualified name, for example:

```powershell
$filter = 'FullyQualifiedName=GraniteEdgeAI.UnitTests.GgufQuickScannerTests.ScanAsync_EmptyFile_ReturnsTruncatedHeaderFailure'
```

---

### Task 1: Repair fixture deployment and packaged-runner selection

**Files:**

- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/QuickScan/ModelQuickScannerTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Properties/launchSettings.json`
- Keep: `tests/UnitTests/GraniteEdgeAI.UnitTests/Properties/launchSettings.json`
- Modify: `.github/workflows/build-and-test.yml`
- Create: `docs/evidence/testing/GGUF-Quick-Scanner-Test-Report.md`

**Interfaces:**

- Consumes: existing generated fixture directories and single-project MSIX test project.
- Produces: predictable `AppX\TestFixtures\{GGUF,Malformed,ExpectedMetadata}` deployment and a latest-compatible VSTest command.

- [ ] **Step 1: Record the reproduced baseline**

Write the report header with branch/commit, .NET/Visual Studio versions, and these exact observed facts:

```text
Debug build: PASS, 0 warnings, 0 errors.
VS 2022 VSTest 17.14: no suitable test runtime provider.
VS 18 VSTest 18.7: package deployed and 2 scanner tests executed.
Existing result: 1 passed, 1 failed.
Failure cause: I-001 was not copied; I-002 was copied.
Repository-root parenthesis hypothesis: current root deployed successfully.
```

- [ ] **Step 2: Replace the single-fixture content item**

Use three repository-relative wildcard items:

```xml
<ItemGroup>
  <Content Include="..\..\TestFixtures\GGUF\**\*.gguf">
    <Link>TestFixtures\GGUF\%(RecursiveDir)%(Filename)%(Extension)</Link>
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
  <Content Include="..\..\TestFixtures\Malformed\**\*.gguf">
    <Link>TestFixtures\Malformed\%(RecursiveDir)%(Filename)%(Extension)</Link>
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
  <Content Include="..\..\TestFixtures\ExpectedMetadata\**\*.json">
    <Link>TestFixtures\ExpectedMetadata\%(RecursiveDir)%(Filename)%(Extension)</Link>
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

Remove the duplicate unsupported-version direct-scanner method from `ModelQuickScannerTests`; it belongs only in `GgufQuickScannerTests`.

- [ ] **Step 3: Restore launch-profile ownership**

Restore the production file exactly:

```json
{
  "profiles": {
    "IBM Granite with TurboQuant (Intel) (Package)": {
      "commandName": "MsixPackage"
    },
    "IBM Granite with TurboQuant (Intel) (Unpackaged)": {
      "commandName": "Project"
    }
  }
}
```

Keep the test file exactly:

```json
{
  "profiles": {
    "GraniteEdgeAI.UnitTests (Package)": {
      "commandName": "MsixPackage"
    }
  }
}
```

- [ ] **Step 4: Make CI include fixture sources and prefer the latest runner**

Add `tests/TestFixtures` to `sparse-checkout`. Change runner discovery from `-all ... | Select-Object -First 1` to:

```powershell
$vstest = & $vswhere `
    -latest `
    -products * `
    -find '**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' |
    Select-Object -First 1
```

- [ ] **Step 5: Verify deployment and the two established behaviors**

Run:

```powershell
dotnet restore 'tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj' `
    --runtime win-x64 `
    -p:Platform=x64

$configuration = 'Debug'
$filter = 'FullyQualifiedName~GraniteEdgeAI.UnitTests.GgufQuickScannerTests'
$trxName = 'task-01-existing-scanner.trx'
# Run the packaged test command template.
```

Expected: both `ScanAsync_InvalidMagic_ReturnsInvalidMagicFailure` and `ScanAsync_UnsupportedVersion_ReturnsUnsupportedVersionFailure` pass; `AppX\TestFixtures` contains all 21 existing `.gguf` files and eight expected JSON files. Record exact actual counts in the report.

- [ ] **Step 6: Commit the independently verified setup repair**

```powershell
git add -- `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/QuickScan/ModelQuickScannerTests.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Properties/launchSettings.json' `
  'IBM Granite with TurboQuant (Intel)/Properties/launchSettings.json' `
  '.github/workflows/build-and-test.yml' `
  'docs/superpowers/specs/2026-07-24-gguf-quick-scanner-design.md' `
  'docs/superpowers/plans/2026-07-24-gguf-quick-scanner-implementation.md' `
  'docs/evidence/testing/GGUF-Quick-Scanner-Test-Report.md'
git commit -m "test(model-import): repair packaged GGUF fixture deployment"
```

---

### Task 2: Lock input, cancellation, and fixed-header behavior

**Files:**

- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/QuickScan/GgufQuickScannerTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/QuickScan/GgufQuickScanner.cs`
- Modify: `docs/evidence/testing/GGUF-Quick-Scanner-Test-Report.md`

**Interfaces:**

- Consumes: `GgufQuickScanner.ScanAsync(string, CancellationToken)`.
- Produces: controlled fixed-header truncation, parsed tensor/metadata counts, and missing-architecture completion for a zero-metadata header.

- [ ] **Cycle 2.1: Existing path preconditions**

Add these separate documented tests:

```csharp
ScanAsync_NullPath_ThrowsArgumentException
ScanAsync_EmptyPath_ThrowsArgumentException
ScanAsync_WhitespacePath_ThrowsArgumentException
```

Each invokes `ScanAsync` with the named invalid value and uses `Assert.ThrowsAsync<ArgumentException>`. Run each exact fully qualified filter.

Expected actual behavior: GREEN immediately because commit `37a3943` already added `ArgumentException.ThrowIfNullOrWhiteSpace`. Record “implementation predates test”; do not mutate correct code merely to manufacture RED.

- [ ] **Cycle 2.2: Existing pre-cancellation**

Add:

```csharp
ScanAsync_PreCancelledToken_ThrowsOperationCanceledException
```

Arrange a fresh `CancellationTokenSource`, call `Cancel()`, and invoke a harmless nonblank path. Assert `OperationCanceledException`.

Expected: GREEN immediately because the established scanner checks the token before opening a file. Record the pre-existing implementation evidence.

- [ ] **Cycle 2.3: Empty fixed header**

Add `ScanAsync_EmptyFile_ReturnsTruncatedHeaderFailure` using `I-000-empty-file.gguf`.

RED command:

```powershell
$filter = 'FullyQualifiedName=GraniteEdgeAI.UnitTests.GgufQuickScannerTests.ScanAsync_EmptyFile_ReturnsTruncatedHeaderFailure'
$trxName = 'cycle-2-3-red.trx'
# Run the packaged test command template.
```

Expected RED: unhandled `EndOfStreamException`, not a result.

Implement a scanner-local `GgufFormatException`, a header exact-read helper, and the catch that converts only that exception to `ModelQuickScanResult.CreateFailure`. The failure must be:

```csharp
failureCode: "truncated-header"
```

GREEN: rerun the same filter and expect one passed test.

- [ ] **Cycle 2.4: Partially truncated fixed header**

Add `ScanAsync_TruncatedHeader_ReturnsTruncatedHeaderFailure` using `I-003-truncated-header.gguf`.

RED expectation: the current stage-specific read still escapes or reports the wrong stage. Make the minimal change so every short read before offset 24 is `truncated-header` and the technical message includes the field and starting offset. Rerun and expect one pass.

- [ ] **Cycle 2.5: Complete header with no architecture**

Add `ScanAsync_ValidHeaderWithoutArchitecture_ReturnsMissingArchitectureFailure` using `H-001-valid-v3-header.gguf`.

RED expectation: `NotImplementedException`.

Add:

```csharp
private readonly record struct GgufHeader(
    uint Version,
    ulong TensorCount,
    ulong MetadataEntryCount);
```

Read the tensor and metadata counts with `ReadUInt64LittleEndian`, remove the terminal `NotImplementedException`, and return:

```csharp
failureCode: "missing-required-architecture"
```

when zero parsed entries leave architecture absent. GREEN: one pass.

- [ ] **Cycle 2.6: Refactor and regression**

Extract only intent-revealing header helpers (`ReadHeaderAsync`, `ReadUInt32Async`, `ReadUInt64Async`) and keep the existing explicit magic/version behavior. Run:

```powershell
$filter = 'FullyQualifiedName~GraniteEdgeAI.UnitTests.GgufQuickScannerTests'
$trxName = 'task-02-post-refactor.trx'
```

Expected: all direct scanner tests created so far pass.

- [ ] **Step 7: Commit**

```powershell
git add -- `
  'IBM Granite with TurboQuant (Intel)/Features/ModelImport/QuickScan/GgufQuickScanner.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/QuickScan/GgufQuickScannerTests.cs' `
  'docs/evidence/testing/GGUF-Quick-Scanner-Test-Report.md'
git commit -m "feat(model-import): validate complete GGUF headers"
```

---

### Task 3: Bound metadata keys and dispatch every value type

**Files:**

- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/QuickScan/GgufQuickScannerTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/QuickScan/GgufQuickScanner.cs`
- Modify: `docs/evidence/testing/GGUF-Quick-Scanner-Test-Report.md`
- Modify: `tests/TestFixtures/Generate-GgufMetadataFixtures.ps1`
- Create: `tests/TestFixtures/Malformed/I-025-empty-metadata-key.gguf`
- Create: `tests/TestFixtures/Malformed/I-026-empty-key-segment.gguf`
- Create: `tests/TestFixtures/Malformed/I-027-key-with-space.gguf`
- Create: `tests/TestFixtures/Malformed/I-028-uppercase-key.gguf`

**Interfaces:**

- Consumes: parsed `GgufHeader.MetadataEntryCount`.
- Produces: bounded metadata loop, strict hierarchical `lower_snake_case` key validation, official type enum, safe exact reads/skips, and structural failure translation.

- [ ] **Cycle 3.1: Metadata count limit**

Add `ScanAsync_ExcessiveMetadataCount_ReturnsExcessiveMetadataCountFailure` using `I-007-excessive-metadata-count.gguf`.

RED: the scanner either iterates/reads and reports truncation or has no limit.

Add before the loop:

```csharp
private const ulong MaxMetadataEntryCount = 1_000_000;
```

Return `excessive-metadata-count` with actual `1_000_001` and the `1_000_000` limit in technical diagnostics. GREEN: one pass.

- [ ] **Cycle 3.2: GGUF key length**

Add `ScanAsync_OversizedKeyLength_ReturnsMetadataKeyTooLongFailure` using `I-005-oversized-key-length.gguf`.

RED: the scanner lacks key parsing or reports truncation.

Add `MaxMetadataKeyByteLength = 65_535`, read the `uint64` length, compare before narrowing/allocation, validate remaining bytes, and decode with strict UTF-8. Validate the complete GGUF hierarchical-key grammar, equivalently `[a-z0-9_]+(\.[a-z0-9_]+)*`, without echoing unsafe invalid key text. Return `metadata-key-too-long` before allocation and `invalid-metadata-key` for empty keys, empty segments, or characters outside lowercase ASCII letters, digits, and underscores. GREEN: one pass.

- [ ] **Cycle 3.3: Unknown metadata type**

Add `ScanAsync_UnknownMetadataType_ReturnsUnsupportedMetadataTypeFailure` using `I-006-unknown-value-type.gguf`.

RED: type 99 is not dispatched.

Add:

```csharp
private enum GgufMetadataValueType : uint
{
    UInt8 = 0,
    Int8 = 1,
    UInt16 = 2,
    Int16 = 3,
    UInt32 = 4,
    Int32 = 5,
    Float32 = 6,
    Boolean = 7,
    String = 8,
    Array = 9,
    UInt64 = 10,
    Int64 = 11,
    Float64 = 12
}
```

Validate every raw type before dispatch and return `unsupported-metadata-type` for 99. GREEN: one pass.

- [ ] **Cycle 3.4: Truncated scalar/string metadata**

Add `ScanAsync_TruncatedMetadataValue_ReturnsTruncatedMetadataFailure` using `I-004-truncated-metadata-value.gguf`.

RED: `EndOfStreamException` or an inaccurate failure.

Make metadata exact reads and validated skips translate premature EOF into `truncated-metadata`, including key/stage and offset. Compare length to `stream.Length - stream.Position`; never use unchecked `position + length`. GREEN: one pass.

- [ ] **Cycle 3.5: Strict Boolean**

Add `ScanAsync_InvalidBooleanValue_ReturnsInvalidBooleanFailure` using `I-010-invalid-boolean-value.gguf`.

RED: unknown boolean is skipped or accepted.

Consume boolean bytes explicitly and accept only 0 or 1. Return `invalid-boolean-value` with actual byte 2. GREEN: one pass.

- [ ] **Cycle 3.6: Truncated array**

Add `ScanAsync_TruncatedArray_ReturnsTruncatedMetadataFailure` using `I-011-truncated-array.gguf`.

RED: array consumption is absent or short read escapes.

Implement `ConsumeArrayAsync` with element type, count, per-array limit, total budget, recursive depth, and cancellation checks. Primitive non-boolean arrays may use checked payload-size calculation plus validated seek; booleans, strings, and arrays must be consumed element by element. GREEN: one pass.

- [ ] **Cycle 3.7: Refactor and regression**

Extract only these focused helpers:

```csharp
ReadMetadataKeyAsync
ReadMetadataTypeAsync
SkipMetadataValueAsync
ConsumeArrayAsync
SkipValidatedBytes
```

Run the full direct scanner class and expect all tests to pass. Record counts and commit:

```powershell
git add -- `
  'IBM Granite with TurboQuant (Intel)/Features/ModelImport/QuickScan/GgufQuickScanner.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/QuickScan/GgufQuickScannerTests.cs' `
  'docs/evidence/testing/GGUF-Quick-Scanner-Test-Report.md'
git commit -m "feat(model-import): parse bounded GGUF metadata values"
```

- [ ] **Security-review follow-up: Complete key grammar**

Generate `I-025` through `I-028` for an empty key, `general..name`, a key
containing a space, and a key containing uppercase ASCII. Add separate direct
tests for each rule. Capture one packaged four-test RED against the ASCII-only
implementation, then a four-test GREEN after validating the complete grammar.
Run the full direct scanner class and expect 20/20 tests.

---

### Task 4: Validate required architecture and extract complete metadata

**Files:**

- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/QuickScan/GgufQuickScannerTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/QuickScan/GgufQuickScanner.cs`
- Modify: `docs/evidence/testing/GGUF-Quick-Scanner-Test-Report.md`

**Interfaces:**

- Consumes: bounded key/type/value helpers.
- Produces: strict known-key typing, `GgufScanState`, expected-JSON DTO, and complete `ModelQuickScanResult.CreateSuccess`.

**Sequencing note:** V-001 requires `general.file_type = 15` to display as
`Q4_K_M`. Task 4 therefore introduces the isolated mapping for 15 and the
stable unknown-file-type fallback used by this fixture. Task 5 extends and
tests that same mapping across the current official 0 through 40 values.

- [ ] **Cycle 4.1: Missing required architecture**

Add `ScanAsync_MissingArchitecture_ReturnsMissingArchitectureFailure` using `I-008-missing-required-architecture.gguf`.

RED: the parser has no complete known-key state.

Add a scanner-local state with nullable `ModelName`, `Architecture`, `ParameterSizeLabel`, `FileType`, and `ContextLength`. After consuming all declared entries, reject a missing/blank architecture with `missing-required-architecture`. GREEN: one pass.

- [ ] **Cycle 4.2: Architecture type**

Add `ScanAsync_ArchitectureWithWrongType_ReturnsInvalidArchitectureTypeFailure` using `I-009-wrong-architecture-type.gguf`.

RED: the `uint32` is skipped and later reported as missing.

When the key is `general.architecture`, require `GgufMetadataValueType.String` immediately and return `invalid-architecture-type` with actual and expected types. GREEN: one pass.

- [ ] **Cycle 4.3: Complete metadata**

Add `ScanAsync_CompleteMetadata_ReturnsExpectedSuccessResult` using V-001 and its expected JSON.

Add exact typed expectation records using `[JsonPropertyName]`:

```csharp
private sealed record ExpectedFixture
{
    [JsonPropertyName("fixtureId")]
    public required string FixtureId { get; init; }

    [JsonPropertyName("fixtureFile")]
    public required string FixtureFile { get; init; }

    [JsonPropertyName("expectedOutcome")]
    public required string ExpectedOutcome { get; init; }

    [JsonPropertyName("expectedMetadata")]
    public required ExpectedMetadata ExpectedMetadata { get; init; }
}

private sealed record ExpectedMetadata
{
    [JsonPropertyName("modelName")]
    public required string ModelName { get; init; }

    [JsonPropertyName("architecture")]
    public required string Architecture { get; init; }

    [JsonPropertyName("parameterSizeLabel")]
    public string? ParameterSizeLabel { get; init; }

    [JsonPropertyName("quantization")]
    public string? Quantization { get; init; }

    [JsonPropertyName("fileSizeBytes")]
    public long FileSizeBytes { get; init; }

    [JsonPropertyName("contextLength")]
    public ulong? ContextLength { get; init; }

    [JsonPropertyName("ggufVersion")]
    public uint GgufVersion { get; init; }
}
```

RED: missing extraction/success result.

Read known strings only after type validation, read file type as `uint32`, normalize context, and call:

```csharp
ModelQuickScanResult.CreateSuccess(
    modelName,
    architecture,
    parameterSizeLabel,
    quantization,
    stream.Length,
    contextLength,
    header.Version);
```

Assert every result field and that all failure properties are null. GREEN: one pass.

- [ ] **Cycle 4.4: Refactor and regression**

Extract `ReadRetainedStringAsync`, `ReadKnownMetadataValueAsync`, and `CreateSuccessResult` only if each name removes detail from the main metadata loop. Run all direct scanner tests and expect green.

- [ ] **Step 5: Commit**

```powershell
git add -- `
  'IBM Granite with TurboQuant (Intel)/Features/ModelImport/QuickScan/GgufQuickScanner.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/QuickScan/GgufQuickScannerTests.cs' `
  'docs/evidence/testing/GGUF-Quick-Scanner-Test-Report.md'
git commit -m "feat(model-import): extract required GGUF display metadata"
```

---

### Task 5: Implement optional fields, safe skipping, and order independence

**Files:**

- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/QuickScan/GgufQuickScannerTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/QuickScan/GgufQuickScanner.cs`
- Modify: `docs/evidence/testing/GGUF-Quick-Scanner-Test-Report.md`

**Interfaces:**

- Consumes: `GgufScanState` and expected-fixture assertion helper.
- Produces: filename fallback, nullable optional fields, unknown metadata skipping, bounded pending context candidates, uint32 normalization, and the expanded official quantization mapping.

- [ ] **Cycle 5.1: Missing name**

Add `ScanAsync_MissingName_UsesFileNameFallback` using V-002.

RED: success creation has no name.

Use `Path.GetFileName(modelFilePath)` when `general.name` is absent/blank. GREEN: expected JSON comparison passes.

- [ ] **Cycle 5.2: Missing context**

Add `ScanAsync_MissingContext_ReturnsSuccessWithNullContext` using V-003.

RED: context is treated as required or result creation is incomplete.

Leave `ContextLength` null when no matching key exists. GREEN: expected JSON comparison passes.

- [ ] **Cycle 5.3: Missing size label**

Add `ScanAsync_MissingSizeLabel_ReturnsSuccessWithNullSizeLabel` using V-004.

RED: size label is treated as required.

Leave absent/blank size label null. GREEN: expected JSON comparison passes.

- [ ] **Cycle 5.4: Unknown values**

Add `ScanAsync_UnknownMetadata_SkipsValuesAndReturnsExpectedResult` using V-005.

RED: unknown string/bool/array values interrupt parsing or allocate unnecessarily.

Use `SkipMetadataValueAsync`: validate and seek unknown strings; strictly consume booleans; consume/skip arrays by element type. Do not decode unknown strings. GREEN: expected JSON comparison passes.

- [ ] **Cycle 5.5: Unusual ordering**

Add `ScanAsync_UnusualMetadataOrder_ReturnsExpectedResult` using V-006.

RED: context preceding architecture is lost.

Retain only context-suffix candidates before architecture in:

```csharp
Dictionary<string, PendingContextValue>
```

Limit it to 64, resolve the exact `Architecture + ".context_length"` key after architecture is known, and do not retain unrelated values. GREEN: expected JSON comparison passes.

- [ ] **Cycle 5.6: `uint32` context**

Add `ScanAsync_ContextEncodedAsUInt32_NormalisesToUInt64` using V-007.

RED: only `uint64` is accepted.

Normalize `uint32` to `ulong` and retain `uint64` unchanged. GREEN: expected JSON comparison passes.

- [ ] **Cycle 5.7: Missing file type**

Add `ScanAsync_MissingFileType_ReturnsSuccessWithNullQuantization` using V-008.

RED: quantization is required or defaults incorrectly.

Keep missing `general.file_type` as null. Extend the Task 4
`MapFileTypeToQuantization(uint)` mapping across the official values, including:

```csharp
15 => "Q4_K_M",
40 => "Q1_0",
_ => $"Unknown (file type {fileType})"
```

GREEN: expected JSON comparison passes.

- [ ] **Cycle 5.8: Refactor and regression**

Run the complete direct scanner class. Expected: every required V-001 through V-008 behavior and malformed I-000 through I-011 behavior passes.

- [ ] **Step 9: Commit**

```powershell
git add -- `
  'IBM Granite with TurboQuant (Intel)/Features/ModelImport/QuickScan/GgufQuickScanner.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/QuickScan/GgufQuickScannerTests.cs' `
  'docs/evidence/testing/GGUF-Quick-Scanner-Test-Report.md'
git commit -m "feat(model-import): complete optional GGUF metadata extraction"
```

---

### Task 6: Generate and test the remaining security boundaries

**Files:**

- Modify: `tests/TestFixtures/Generate-GgufMetadataFixtures.ps1`
- Modify/regenerate: `tests/TestFixtures/GGUF/*.gguf`
- Modify/regenerate: `tests/TestFixtures/Malformed/*.gguf`
- Modify/regenerate: `tests/TestFixtures/ExpectedMetadata/*.json`
- Modify: `tests/TestFixtures/fixture-manifest.json`
- Modify: `tests/TestFixtures/GGUF/README.md`
- Modify: `tests/TestFixtures/Malformed/README.md`
- Modify: `tests/TestFixtures/ExpectedMetadata/README.md`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/QuickScan/GgufQuickScannerTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/QuickScan/GgufQuickScanner.cs`
- Modify: `docs/evidence/testing/GGUF-Quick-Scanner-Test-Report.md`

**Interfaces:**

- Consumes: PowerShell writer helpers and all parser safety constants.
- Produces: generator-owned V-009/V-010 and I-012 through I-023 fixtures plus boundary coverage.

- [ ] **Step 1: Extend the generator’s type support**

Define all IDs 0–12 and extend `Write-GgufValue` with exact `BinaryWriter.Write` overloads for:

```powershell
[byte], [sbyte], [uint16], [int16], [uint32], [int32],
[single], [byte]0-or-1, [string], array, [uint64], [int64], [double]
```

Keep arrays recursive through `Write-GgufValue`.

- [ ] **Step 2: Add valid generated fixtures**

Generate:

```text
V-009-all-official-metadata-types.gguf
V-010-unknown-file-type.gguf
```

V-009 includes required Granite fields plus unknown values of every type 0–12, a nested array within depth 8, and `general.file_type = 40` expecting `Q1_0`. V-010 uses an unassigned `general.file_type = 999` expecting `Unknown (file type 999)`. Generate matching expected JSON with hashes.

- [ ] **Step 3: Add malformed generated fixtures**

Generate:

```text
I-012-oversized-metadata-string.gguf
I-013-excessive-array-count.gguf
I-014-excessive-total-array-count.gguf
I-015-excessive-array-depth.gguf
I-016-invalid-context-type.gguf
I-017-excessive-context-candidates.gguf
I-018-invalid-utf8-architecture.gguf
I-019-non-ascii-key.gguf
I-020-wrong-name-type.gguf
I-021-wrong-size-label-type.gguf
I-022-wrong-file-type.gguf
I-023-blank-architecture.gguf
```

Construct limit fixtures with declared lengths/counts so rejection occurs before large allocation. I-014 uses nested arrays with allowed per-array counts whose cumulative declared total exceeds four million before payload iteration. I-015 uses nine nested array levels with count one. I-017 supplies 65 distinct `*.context_length` keys before architecture.

- [ ] **Step 4: Regenerate and inspect**

Run:

```powershell
& 'tests\TestFixtures\Generate-GgufHeaderFixtures.ps1'
& 'tests\TestFixtures\Generate-GgufMetadataFixtures.ps1'
git diff --check
Get-ChildItem 'tests\TestFixtures\GGUF','tests\TestFixtures\Malformed' -Filter '*.gguf' |
    Sort-Object FullName |
    Select-Object Name, Length
```

Expected: generator exits 0, every fixture is small, all JSON parses, and hashes/sizes in expected metadata/manifest match generated bytes.

- [ ] **Cycles 6.1–6.12: Add one boundary test at a time**

For each row, add the exact documented test, run its exact FQN for RED, implement only the named guard/mapping, rerun for GREEN, and append actual output to the report:

| Test name | Fixture | Expected RED | Minimal GREEN | Failure/success expectation |
|---|---|---|---|---|
| `ScanAsync_OversizedMetadataString_ReturnsMetadataStringTooLongFailure` | I-012 | length is skipped/read or reports truncation | compare to 16 MiB before narrowing | `metadata-string-too-long` |
| `ScanAsync_ExcessiveArrayCount_ReturnsExcessiveArrayCountFailure` | I-013 | loop/truncation | compare one count to 1,000,000 | `excessive-array-count` |
| `ScanAsync_ExcessiveTotalArrayCount_ReturnsExcessiveArrayCountFailure` | I-014 | nested multiplication proceeds | checked cumulative count, max 4,000,000 | `excessive-array-count` |
| `ScanAsync_ExcessiveArrayDepth_ReturnsExcessiveArrayDepthFailure` | I-015 | recursion proceeds | reject depth 9 before recursive descent | `excessive-array-depth` |
| `ScanAsync_ContextWithWrongType_ReturnsInvalidContextTypeFailure` | I-016 | matching context is ignored | retain/resolve declared type | `invalid-context-type` |
| `ScanAsync_ExcessiveContextCandidates_ReturnsControlledFailure` | I-017 | dictionary grows to 65 | enforce 64 before add | `excessive-context-candidate-count` |
| `ScanAsync_InvalidUtf8Architecture_ReturnsInvalidEncodingFailure` | I-018 | replacement characters or decoder exception escapes | strict UTF-8 translation | `invalid-metadata-encoding` |
| `ScanAsync_NonAsciiKey_ReturnsInvalidMetadataKeyFailure` | I-019 | invalid key accepted | ASCII key guard | `invalid-metadata-key` |
| `ScanAsync_NameWithWrongType_ReturnsInvalidNameTypeFailure` | I-020 | wrong value skipped | known-key type guard | `invalid-name-type` |
| `ScanAsync_SizeLabelWithWrongType_ReturnsInvalidSizeLabelTypeFailure` | I-021 | wrong value skipped | known-key type guard | `invalid-size-label-type` |
| `ScanAsync_FileTypeWithWrongType_ReturnsInvalidFileTypeFailure` | I-022 | wrong value skipped | known-key type guard | `invalid-file-type` |
| `ScanAsync_BlankArchitecture_ReturnsMissingArchitectureFailure` | I-023 | success factory throws or blank succeeds | validate nonblank before factory | `missing-required-architecture` |

- [ ] **Cycle 6.13: All official types and nested arrays**

Add `ScanAsync_AllOfficialMetadataTypes_ReturnsExpectedSuccessResult` using V-009.

RED expectation: any unimplemented scalar or nested-array path fails.

GREEN requires safe consumption of IDs 0–12, nested arrays, strict booleans, and `40 -> Q1_0`. Compare all success fields with expected JSON.

- [ ] **Cycle 6.14: Unknown numeric file type**

Add `ScanAsync_UnknownFileType_ReturnsDocumentedLabel` using V-010.

RED expectation: unknown mapping becomes null or throws.

GREEN expectation: success with `Quantization == "Unknown (file type 999)"`.

- [ ] **Cycle 6.15: Operational missing file**

Add `ScanAsync_MissingFile_ReturnsFileNotFoundFailure` using a unique nonexistent path beneath `AppContext.BaseDirectory`.

RED expectation: `FileNotFoundException`.

Catch only `FileNotFoundException` and `DirectoryNotFoundException` at the file-opening boundary and return `file-not-found`. Add the adjacent narrow `UnauthorizedAccessException -> file-access-denied` and unrelated `IOException -> file-read-error` mappings without catching cancellation or `Exception`.

- [ ] **Step 20: Full direct scanner regression and commit**

Run:

```powershell
$filter = 'FullyQualifiedName~GraniteEdgeAI.UnitTests.GgufQuickScannerTests'
$trxName = 'task-06-direct-scanner.trx'
# Run the packaged test command template.
```

Expected: all direct scanner tests pass. Then:

```powershell
git add -- `
  'tests/TestFixtures' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelImport/QuickScan/GgufQuickScanner.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/QuickScan/GgufQuickScannerTests.cs' `
  'docs/evidence/testing/GGUF-Quick-Scanner-Test-Report.md'
git commit -m "test(model-import): cover GGUF scanner safety boundaries"
```

---

### Task 7: Replace the temporary router test with real integration

**Files:**

- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/QuickScan/ModelQuickScannerTests.cs`
- Inspect/format: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/QuickScan/ModelQuickScanner.cs`
- Modify: `docs/evidence/testing/GGUF-Quick-Scanner-Test-Report.md`

**Interfaces:**

- Consumes: completed real `GgufQuickScanner`.
- Produces: observable valid/invalid GGUF routing and cancellation conversion.

- [ ] **Cycle 7.1: Valid GGUF routing**

Delete `ScanAsync_GgufWithNonBlankPath_ReachesUnimplementedScanner`.

Add `ScanAsync_GgufWithValidFixture_ReturnsSuccessResult` using V-001. RED before scanner completion is `NotImplementedException`; GREEN is `Success` with Granite metadata.

- [ ] **Cycle 7.2: Invalid GGUF routing**

Add `ScanAsync_GgufWithInvalidFixture_ReturnsScannerFailure` using I-001. Assert `Failure` and `invalid-magic`. GREEN should require no router parsing or special case.

- [ ] **Cycle 7.3: Requested cancellation**

Add `ScanAsync_GgufWithPreCancelledToken_ReturnsCancelledResult` with a cancelled token and harmless nonblank path. Assert `Cancelled`, not an exception/failure.

Expected: the existing narrow catch filter passes. If it does, record that router cancellation behavior predates the new test.

- [ ] **Step 4: Preserve all prior router cases**

Run:

```powershell
$filter = 'FullyQualifiedName~GraniteEdgeAI.UnitTests.ModelQuickScannerTests'
$trxName = 'task-07-router.trx'
# Run the packaged test command template.
```

Expected: `None`, `None` with empty path, OpenVINO, unknown enum, GGUF null/blank paths, constructor null, valid/invalid GGUF, and requested cancellation all pass.

- [ ] **Step 5: Cleanup and commit**

Remove the extra blank line in `ModelQuickScanner.cs`; do not change behavior. Confirm:

```powershell
rg -n 'NotImplementedException|currently unfinished|unimplemented scanner' `
  'IBM Granite with TurboQuant (Intel)' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests'
```

Expected: no GGUF scanner temporary markers.

```powershell
git add -- `
  'IBM Granite with TurboQuant (Intel)/Features/ModelImport/QuickScan/ModelQuickScanner.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/QuickScan/ModelQuickScannerTests.cs' `
  'docs/evidence/testing/GGUF-Quick-Scanner-Test-Report.md'
git commit -m "test(model-import): route GGUF fixtures through quick scanner"
```

---

### Task 8: Refactor with the complete behavioral safety net

**Files:**

- Modify only when justified: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/QuickScan/GgufQuickScanner.cs`
- Modify only when readability improves: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/QuickScan/GgufQuickScannerTests.cs`
- Modify: `docs/evidence/testing/GGUF-Quick-Scanner-Test-Report.md`

**Interfaces:**

- Consumes: green direct and router suites.
- Produces: final cohesive parser without behavior changes.

- [ ] **Step 1: Run pre-refactor safety net**

Run direct scanner and router class filters. Expected: all pass.

- [ ] **Step 2: Apply one small refactoring at a time**

Keep these intended responsibilities:

```text
ScanAsync                    validates API boundary and maps known failures
ScanOpenedFileAsync          orchestrates header, metadata, and success
ReadHeaderAsync              owns offsets 0–23
ReadMetadataAsync            loops bounded entries and builds small state
ReadMetadataKeyAsync         bounds, reads, and validates key text
ReadRetainedStringAsync      bounds, reads, and strictly decodes known text
SkipMetadataValueAsync       consumes one official value
ConsumeArrayAsync            enforces count/total/depth and consumes elements
MapFileTypeToQuantization    contains only official label mapping
CreateFormatFailure          creates consistent scanner-local diagnostics
```

After each extraction/rename, build and run the direct scanner class. Revert any transformation that makes control flow or signatures harder to follow.

- [ ] **Step 3: Static safety scan**

Run:

```powershell
rg -n 'catch\s*\(Exception|BinaryReader|Read\(|ReadByte\(|NotImplementedException|Task\.Run|new byte\[[^\]]*(ulong|length)' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelImport/QuickScan/GgufQuickScanner.cs'
```

Expected: no broad catch, synchronous read, `BinaryReader`, `Task.Run`, unfinished code, or unchecked attacker-length allocation.

- [ ] **Step 4: Post-refactor regression and commit**

Run the direct class, router class, and full packaged suite. Expected: all pass.

```powershell
git add -- `
  'IBM Granite with TurboQuant (Intel)/Features/ModelImport/QuickScan/GgufQuickScanner.cs' `
  'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/QuickScan/GgufQuickScannerTests.cs' `
  'docs/evidence/testing/GGUF-Quick-Scanner-Test-Report.md'
git commit -m "refactor(model-import): clarify bounded GGUF parsing"
```

---

### Task 9: Write the beginner guide and complete the evidence report

**Files:**

- Create: `docs/development/GGUF-Quick-Scanner-Beginner-Guide.md`
- Complete: `docs/evidence/testing/GGUF-Quick-Scanner-Test-Report.md`
- Modify: `tests/README.md`

**Interfaces:**

- Consumes: final code, generated fixture inventory, TRX results, source/book evidence.
- Produces: a standalone teaching guide and reproducible test record.

- [ ] **Step 1: Write the beginner guide**

Include distinct sections covering all of:

1. feature purpose and non-goals;
2. workflow diagram;
3. each class responsibility;
4. 24-byte header table;
5. little-endian decoding;
6. all metadata type IDs 0–12;
7. `FileStream` options;
8. `ReadExactlyAsync`;
9. cancellation propagation;
10. GGUF strings;
11. arrays and nested arrays;
12. unknown-value skipping;
13. order-independent context resolution;
14. quantization mapping and unknown values;
15. every safety limit and its rationale;
16. every failure code;
17. fixture generation rules;
18. a complete fixture table;
19. a complete test table with test name, Arrange, Act, Assert, and failure caught;
20. line-by-line explanation of `ScanAsync`;
21. line-by-line explanation of header reading;
22. line-by-line explanation of bounded string reading;
23. line-by-line explanation of metadata value consumption;
24. line-by-line explanation of arrays;
25. line-by-line explanation of quantization mapping;
26. representative malformed test walkthrough;
27. representative successful JSON test walkthrough;
28. fixture-copy MSBuild explanation;
29. packaged WinUI runner explanation;
30. exact Debug and Release commands with actual final totals;
31. limitations and safe extension guidance;
32. sources/textbook principles and glossary.

- [ ] **Step 2: Complete the evidence report**

Record:

- date, OS, SDK/runtime, Visual Studio/VSTest, branch, and final local commit;
- every restore/build/test command;
- baseline failures, hypotheses, discriminating experiments, causes, and corrections;
- Debug and Release application/test builds;
- targeted direct scanner and router counts;
- full suite count;
- exact exit codes, durations, and TRX relative paths;
- any warnings, skips, failures, and limitations;
- confirmation that no remote action occurred.

- [ ] **Step 3: Update test documentation**

Replace stale “smoke-only” wording in `tests/README.md` with the packaged MSTest project path, fixture-generator paths, appxrecipe command shape, and links to the beginner guide/report.

- [ ] **Step 4: Documentation validation and commit**

Run:

```powershell
rg -n 'T[B]D|T[O]DO|PLACEHOLDER|xx tests|not run yet' `
  'docs/development/GGUF-Quick-Scanner-Beginner-Guide.md' `
  'docs/evidence/testing/GGUF-Quick-Scanner-Test-Report.md'
git diff --check
```

Expected: no placeholders and no whitespace errors.

```powershell
git add -- `
  'docs/development/GGUF-Quick-Scanner-Beginner-Guide.md' `
  'docs/evidence/testing/GGUF-Quick-Scanner-Test-Report.md' `
  'tests/README.md'
git commit -m "docs(model-import): explain and evidence GGUF quick scanning"
```

---

### Task 10: Independent reviews and final Debug/Release verification

**Files:**

- Review: all changes from `origin/feature/winui-shell-model-import..HEAD` plus remaining working-tree changes.
- Modify: any file implicated by a verified Critical or Important finding.
- Complete: `docs/evidence/testing/GGUF-Quick-Scanner-Test-Report.md`

**Interfaces:**

- Consumes: implemented feature and documentation.
- Produces: reviewed, reproducibly verified local branch with no required work remaining.

- [ ] **Step 1: Run independent review passes**

Request separate specification, code/security, tests, documentation, and whole-branch reviews. Each review must inspect:

```text
unchecked conversions, allocation, products, count loops, seeks
endianness and exact-read offsets
metadata ordering and duplicate/ambiguous state
strict Boolean and all value types
nested-array count/depth/total handling
overbroad exception catches
cancellation preservation
duplicate parsing in tests
brittle paths/assertions
NotImplementedException remnants
launchSettings ownership
fixture generation/copy and CI sparse checkout
unrelated changes
```

Fix every Critical/Important finding with a focused regression test, then rerun its class and the full suite.

- [ ] **Step 2: Inspect repository state**

Run:

```powershell
git status --short
git diff --check
git diff --stat origin/feature/winui-shell-model-import...HEAD
git diff origin/feature/winui-shell-model-import...HEAD
git diff
git diff --cached
```

Expected: only intended feature/test/docs/CI changes and no whitespace errors.

- [ ] **Step 3: Final Debug restore/build/tests**

Run restore, application build, and test build:

```powershell
$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" `
    -latest -products * `
    -find '**\MSBuild\Current\Bin\amd64\MSBuild.exe' |
    Select-Object -First 1

& $msbuild `
  'IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj' `
  /target:Restore `
  /property:Configuration=Debug `
  /property:Platform=x64 `
  /property:RuntimeIdentifier=win-x64

dotnet restore `
  'tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj' `
  --runtime win-x64 `
  -p:Platform=x64

& $msbuild `
  'IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj' `
  /target:Build `
  /maxCpuCount `
  /verbosity:minimal `
  /property:Configuration=Debug `
  /property:Platform=x64 `
  /property:RuntimeIdentifier=win-x64 `
  /property:PublishProfile= `
  /property:PublishTrimmed=false `
  /property:PublishReadyToRun=false `
  /property:AppxPackageSigningEnabled=false `
  /property:GenerateAppxPackageOnBuild=false

dotnet build `
  'tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj' `
  --configuration Debug `
  --no-restore `
  --runtime win-x64 `
  -p:Platform=x64
```

Run three Debug appxrecipe invocations with TRX:

```text
FullyQualifiedName~GgufQuickScannerTests
FullyQualifiedName~ModelQuickScannerTests
no filter (full suite)
```

Expected: exit 0 for all; record exact counts/durations.

- [ ] **Step 4: Final Release/CI-equivalent build/tests**

Repeat the exact commands with `Configuration=Release`, then run the Release recipe:

```text
tests/UnitTests/GraniteEdgeAI.UnitTests/bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/GraniteEdgeAI.UnitTests.build.appxrecipe
```

Produce:

```text
final-release-gguf.trx
final-release-router.trx
final-release-full.trx
```

Expected: exit 0, zero failed tests, and exact totals recorded in the guide/report.

- [ ] **Step 5: Final evidence amendment and local commit**

Update the report and guide with actual final commands/totals, then:

```powershell
git add -- `
  'docs/development/GGUF-Quick-Scanner-Beginner-Guide.md' `
  'docs/evidence/testing/GGUF-Quick-Scanner-Test-Report.md'
git commit -m "docs(testing): record final GGUF scanner verification"
```

- [ ] **Step 6: Finishing options only**

Use `superpowers:finishing-a-development-branch` to report the available integration options and current local commit range. Do not execute a merge, push, remote PR update, branch deletion, or worktree cleanup.
