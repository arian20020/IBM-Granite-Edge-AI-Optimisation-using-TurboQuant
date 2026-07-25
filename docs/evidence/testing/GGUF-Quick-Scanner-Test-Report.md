# GGUF quick-scanner packaged test report

## Current Task 10 final verification

This is the authoritative completion snapshot. Later sections retain the
genuine baseline, RED/GREEN, review-fix, refactoring, and Task 9 documentation
record in chronological order.

### Scope and reviewed implementation

| Item | Observed value |
|---|---|
| Date | 25 July 2026 |
| Branch | `feature/winui-shell-model-import` |
| Reviewed implementation commit | `a526a4ccf4231de54e62651b0deb3a3c580c8647` |
| Repository root | `C:\Users\Arian\source\repos\IBM-Granite-TurboQuant-Intel` |
| Selected .NET SDK | `10.0.301` from `global.json` |
| Target | `net8.0-windows10.0.19041.0`; `win-x64`; platform `x64` |
| Build/runner | Visual Studio 18 Community MSBuild `18.7.8+1ac568fee`; VSTest `18.7.0 (x64)` |
| Fixture snapshot | 52 GGUF binaries, 12 expected-result JSON files, and one integrity manifest |
| Packaged suite | 94 test methods; 108 executions |

The documentation commit is identified by subject
`docs(model-import): record final GGUF scanner verification`; a commit cannot
contain its own future hash.

Independent code/security and tests/fixtures/packaging/CI reviews both
completed clear, with no remaining Critical or Important finding. The review
follow-ups added aggregate key-work bounds, scan-wide buffers, chunked Boolean
validation, exact truncation diagnostics, genuine in-flight cancellation
tests, packaged manifest integrity, and an authoritative clean fixture
orchestrator.

### Fixture reproducibility and package integrity

Windows PowerShell 5.1 ran
`tests\TestFixtures\Generate-GgufFixtures.ps1`, which removes the complete
generated output set before invoking the header and metadata leaves.
Verification proved:

- 65 generated artifacts had identical paths, lengths, and SHA-256 hashes
  across consecutive complete runs;
- all 13 generated JSON files used literal LF and no CRLF sequence;
- deliberately injected root and nested stale GGUF/JSON outputs were removed;
- a simulated new header output survived the metadata
  `-SkipOutputCleanup` phase; and
- both Debug and Release AppX layouts contained exactly 52 GGUF binaries,
  12 expected-result JSON files, and one manifest.

`GgufFixtureIntegrityTests.PackagedGgufFixtures_MatchIntegrityManifest` then
re-enumerated the deployed `.gguf` path set and checked every byte length and
SHA-256 digest against the packaged manifest. It passed in both full runs.

The CI workflow now invokes the same clean orchestrator with Windows
PowerShell 5.1 before staging its short build tree. It rejects tracked
differences, untracked fixture output, a missing/empty/partial TRX, any
non-passing result, or a full run without passed direct-scanner,
fixture-integrity, and router definitions. This workflow change was inspected
locally; no remote run was triggered because this task performs no push.

### Final Debug and Release matrix

For each configuration, Task 10 explicitly restored and built the standalone
WinUI application with Visual Studio's x64 MSBuild, restored and built the
packaged MSTest project, and invoked direct scanner, router, and unfiltered
tests through the generated `.build.appxrecipe`. Runtime, platform, publish,
trimming, ReadyToRun, signing, and package-generation properties matched the
CI-equivalent commands documented in the beginner guide.

Every native process exited 0. Both packaged test-project builds reported:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

The application builds emitted their expected output assembly paths and no
warning or error diagnostic. TRX counters and `finish - start` durations were:

| Configuration | Scope/filter | Total/executed/passed | Failed/error/inconclusive/not executed | TRX duration | Relative TRX path |
|---|---|---:|---:|---:|---|
| Debug | `GgufQuickScannerTests` | 58/58/58 | 0/0/0/0 | 5.6096 s | `TestResults\GGUF-Quick-Scanner\Debug\task-10-final-reviewed-debug-direct.trx` |
| Debug | `ModelQuickScannerTests` | 12/12/12 | 0/0/0/0 | 2.8062 s | `TestResults\GGUF-Quick-Scanner\Debug\task-10-final-reviewed-debug-router.trx` |
| Debug | no filter | 108/108/108 | 0/0/0/0 | 2.9254 s | `TestResults\GGUF-Quick-Scanner\Debug\task-10-final-reviewed-debug-full.trx` |
| Release | `GgufQuickScannerTests` | 58/58/58 | 0/0/0/0 | 6.4571 s | `TestResults\GGUF-Quick-Scanner\Release\task-10-final-release-direct.trx` |
| Release | `ModelQuickScannerTests` | 12/12/12 | 0/0/0/0 | 2.6814 s | `TestResults\GGUF-Quick-Scanner\Release\task-10-final-release-router.trx` |
| Release | no filter | 108/108/108 | 0/0/0/0 | 2.7934 s | `TestResults\GGUF-Quick-Scanner\Release\task-10-final-release-full.trx` |

The full-run definitions/results join confirmed the required classes were not
silently omitted in either configuration:

| Required class | Debug passed | Release passed |
|---|---:|---:|
| `GgufQuickScannerTests` | 58 | 58 |
| `GgufFixtureIntegrityTests` | 1 | 1 |
| `ModelQuickScannerTests` | 12 | 12 |

No push, merge, publish, or remote pull-request mutation occurred.

## Historical Task 9 documentation snapshot

This section preserves the earlier Task 9 summary. It is historical evidence,
not the current inventory or final completion claim.

### Scope and environment

| Item | Observed value |
|---|---|
| Date | 25 July 2026 |
| Operating system | Windows 11 Pro, version `10.0.26200`, build `26200` |
| Branch | `feature/winui-shell-model-import` |
| Tested implementation commit | `c67d33f36ec323d8537245a536bb818098dd6a7e` |
| Repository root | `C:\Users\Arian\source\repos\IBM-Granite-TurboQuant-Intel` |
| Selected .NET SDK | `10.0.301` from `global.json` |
| Target framework/runtime | `net8.0-windows10.0.19041.0`; `win-x64`; packaged output uses .NET runtime pack `8.0.28` |
| Build engines | .NET SDK MSBuild `18.6.4+96856fd72` (file version `18.6.4.27133`) for `dotnet` test-project commands; Visual Studio 18 Community MSBuild `18.7.8.30822` x64 for explicit application commands |
| Packaged runner | `C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe`, VSTest `18.7.0 (x64)` |
| MSTest packages | `MSTest.TestAdapter` and `MSTest.TestFramework` `4.3.2` |
| Windows App SDK package | `Microsoft.WindowsAppSDK` `2.2.0` |
| Fixture snapshot | 42 generated binaries: 13 GGUF/header and 29 malformed; 12 expected-result JSON files |

The tested implementation commit is recorded rather than pretending that a
commit can contain its own future hash. The Task 9 documentation commit is
identified by subject `docs(model-import): explain and evidence GGUF quick
scanning` in local history. The Task 10 section above records the later
reviewed implementation and CI-equivalent verification.

Task 9 explicitly restored and built the standalone application, then built the
packaged test project in Debug and Release. The test-project reference also
compiled the production assembly. Task 10 subsequently repeated the complete
matrix after independent reviews for the authoritative result above.

### Exact Task 9 restore, build, and test commands

The result directories were created before `Resolve-Path`. The restore command
was:

```powershell
$appProject =
  'IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj'
$testProject =
  'tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj'
$resultsRoot = 'TestResults\GGUF-Quick-Scanner'

New-Item -ItemType Directory -Force -Path `
  "$resultsRoot\Debug", "$resultsRoot\Release" | Out-Null

dotnet restore $testProject `
  --runtime win-x64 `
  -p:Platform=x64
```

Exit code: 0. Output:
`TestResults\GGUF-Quick-Scanner\task-09-docs-restore.log`.

Build-tool and runner discovery used:

```powershell
$vswhere =
  "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere `
  -latest `
  -products * `
  -find '**\MSBuild\Current\Bin\amd64\MSBuild.exe' |
  Select-Object -First 1
$vstest = & $vswhere `
  -latest `
  -products * `
  -find '**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' |
  Select-Object -First 1
```

The exact Debug build and three packaged invocations were:

```powershell
& $msbuild $appProject `
  /target:Restore `
  /property:Configuration=Debug `
  /property:Platform=x64 `
  /property:RuntimeIdentifier=win-x64

& $msbuild $appProject `
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

dotnet build $testProject `
  --configuration Debug `
  --no-restore `
  --runtime win-x64 `
  -p:Platform=x64

$debugRecipe = (Resolve-Path -LiteralPath `
  'tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe').Path
$debugResults = (Resolve-Path -LiteralPath `
  "$resultsRoot\Debug").Path

& $vstest $debugRecipe `
  /Platform:x64 `
  /TestCaseFilter:'FullyQualifiedName~GraniteEdgeAI.UnitTests.GgufQuickScannerTests' `
  /Logger:'trx;LogFileName=task-09-docs-debug-direct.trx' `
  "/ResultsDirectory:$debugResults"

& $vstest $debugRecipe `
  /Platform:x64 `
  /TestCaseFilter:'FullyQualifiedName~GraniteEdgeAI.UnitTests.ModelQuickScannerTests' `
  /Logger:'trx;LogFileName=task-09-docs-debug-router.trx' `
  "/ResultsDirectory:$debugResults"

& $vstest $debugRecipe `
  /Platform:x64 `
  /Logger:'trx;LogFileName=task-09-docs-debug-full.trx' `
  "/ResultsDirectory:$debugResults"
```

The exact Release build and three packaged invocations were:

```powershell
& $msbuild $appProject `
  /target:Restore `
  /property:Configuration=Release `
  /property:Platform=x64 `
  /property:RuntimeIdentifier=win-x64

& $msbuild $appProject `
  /target:Build `
  /maxCpuCount `
  /verbosity:minimal `
  /property:Configuration=Release `
  /property:Platform=x64 `
  /property:RuntimeIdentifier=win-x64 `
  /property:PublishProfile= `
  /property:PublishTrimmed=false `
  /property:PublishReadyToRun=false `
  /property:AppxPackageSigningEnabled=false `
  /property:GenerateAppxPackageOnBuild=false

dotnet build $testProject `
  --configuration Release `
  --no-restore `
  --runtime win-x64 `
  -p:Platform=x64

$releaseRecipe = (Resolve-Path -LiteralPath `
  'tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe').Path
$releaseResults = (Resolve-Path -LiteralPath `
  "$resultsRoot\Release").Path

& $vstest $releaseRecipe `
  /Platform:x64 `
  /TestCaseFilter:'FullyQualifiedName~GraniteEdgeAI.UnitTests.GgufQuickScannerTests' `
  /Logger:'trx;LogFileName=task-09-docs-release-direct.trx' `
  "/ResultsDirectory:$releaseResults"

& $vstest $releaseRecipe `
  /Platform:x64 `
  /TestCaseFilter:'FullyQualifiedName~GraniteEdgeAI.UnitTests.ModelQuickScannerTests' `
  /Logger:'trx;LogFileName=task-09-docs-release-router.trx' `
  "/ResultsDirectory:$releaseResults"

& $vstest $releaseRecipe `
  /Platform:x64 `
  /Logger:'trx;LogFileName=task-09-docs-release-full.trx' `
  "/ResultsDirectory:$releaseResults"
```

### Task 9 Debug and Release results

The standalone application restore/build commands all exited 0:

| Configuration | Operation | Wall-clock duration | Relative log |
|---|---|---:|---|
| Debug | application restore | 5.08 s | `TestResults\GGUF-Quick-Scanner\Debug\task-09-app-debug-restore.log` |
| Debug | application build | 13.42 s | `TestResults\GGUF-Quick-Scanner\Debug\task-09-app-debug-build.log` |
| Release | application restore | 3.56 s | `TestResults\GGUF-Quick-Scanner\Release\task-09-app-release-restore.log` |
| Release | application build | 17.37 s | `TestResults\GGUF-Quick-Scanner\Release\task-09-app-release-build.log` |

The restore logs explicitly reported zero warnings and errors. The application
builds used `/verbosity:minimal`; they emitted the successful output assembly
path and no warning or error diagnostics.

The Debug packaged test-project build exited 0 in 13.46 seconds. The Release
packaged test-project build exited 0 in 68.69 seconds. Each reported:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Build logs:

- `TestResults\GGUF-Quick-Scanner\Debug\task-09-docs-debug-build.log`;
- `TestResults\GGUF-Quick-Scanner\Release\task-09-docs-release-build.log`.

Every VSTest process exited 0. Counters and durations below were read from each
TRX. Duration is the TRX `finish - start` interval.

| Configuration | Scope/filter | Total/executed/passed | Failed/error/inconclusive/not executed | TRX duration | Relative TRX path |
|---|---|---:|---:|---:|---|
| Debug | `GgufQuickScannerTests` | 47/47/47 | 0/0/0/0 | 3.4979773 s | `TestResults\GGUF-Quick-Scanner\Debug\task-09-docs-debug-direct.trx` |
| Debug | `ModelQuickScannerTests` | 11/11/11 | 0/0/0/0 | 2.7285634 s | `TestResults\GGUF-Quick-Scanner\Debug\task-09-docs-debug-router.trx` |
| Debug | no filter | 95/95/95 | 0/0/0/0 | 2.7635008 s | `TestResults\GGUF-Quick-Scanner\Debug\task-09-docs-debug-full.trx` |
| Release | `GgufQuickScannerTests` | 47/47/47 | 0/0/0/0 | 9.6642398 s | `TestResults\GGUF-Quick-Scanner\Release\task-09-docs-release-direct.trx` |
| Release | `ModelQuickScannerTests` | 11/11/11 | 0/0/0/0 | 2.3760642 s | `TestResults\GGUF-Quick-Scanner\Release\task-09-docs-release-router.trx` |
| Release | no filter | 95/95/95 | 0/0/0/0 | 2.4790299 s | `TestResults\GGUF-Quick-Scanner\Release\task-09-docs-release-full.trx` |

The direct and router filters exercise real generated files through the
packaged filesystem. Although the methods carry `TestCategory("Unit")`, these
fixture-backed cases are component/integration-style tests rather than pure
in-memory unit tests.

### Consolidated diagnostic record

The detailed evidence remains in the task sections below. This table connects
the important symptom, hypothesis/experiment, cause, and correction:

| Stage | Observed symptom | Hypothesis and discriminating experiment | Verified cause | Correction |
|---|---|---|---|---|
| setup baseline | 1 of 2 scanner cases failed because I-001 was absent | Inspect recipe/AppX and deploy from the current short root with both VS runners | Fixture content item copied only I-002; VS 2022 runner also lacked a suitable provider, while VS18 executed the package | Wildcard-copy all fixture groups; select latest compatible VS runner |
| empty header | `EndOfStreamException` escaped | Exact one-test packaged RED with I-000 | No structural translation around exact header read | Scanner-local `truncated-header` translation |
| short bad magic | Returned `invalid-magic` before discovering incomplete header | I-024 with `TEST` plus only half tensor count | Magic was classified before all 24 bytes were read | Complete fixed header before magic classification |
| metadata boundaries | Unsafe/missing dispatch reached later missing-architecture behavior | One fixture/test per count, key, type, truncation, Boolean, and array boundary | Metadata values were not fully bounded/consumed | Focused key/type/value/array helpers and stable failures |
| key grammar review | Empty segments, spaces, and uppercase ASCII were accepted | Four generated single-entry cases | ASCII-only validation was weaker than official key grammar | Full hierarchical lower-snake-case validation |
| known extraction | Wrong architecture type looked missing; V-001 could not succeed | Exact wrong-type and typed-JSON tests | Known fields were only being skipped | Strict known-key typing and retained state |
| context order | V-006 expected 131072 but returned null | Move exact context before architecture | Architecture-first assumption | Maximum-64 pending candidate resolution |
| current mapping/duplicates | File type 41 was unknown; later duplicates replaced values | Generated V-011 and V-012 two-test RED | Mapping stopped at 40; state was last-wins | Add 41 and bounded first-occurrence-wins state |
| operational open | `FileNotFoundException` escaped | Unique nonexistent path | `FileStream` construction was outside an operational mapping | Narrow opening-time file/directory/access/I/O catches |
| router integration | Obsolete test still expected `NotImplementedException` | Run router baseline after scanner completion | Test expectation, not production, was stale | Replace with valid, invalid, and cancellation integrations |
| refactor | Risk of changing a security-sensitive parser while extracting orchestration | Run direct/router before, after each extraction, and full suite | No behavior defect; this was a safety-net question | Three small extractions with green checkpoints |

### Task 9 limitations and evidence boundary

- `ModelImportPage` still does not call `ModelQuickScanner`; it selects a path
  and sets visual `Scanning` state, but result/UI wiring and
  `HasValidatedModel` assignment are outside this scanner-only task.
- Only GGUF v3 header/metadata is checked. Tensor descriptors/data and model
  inference are not tested by these tiny structural fixtures.
- The implementation reads multi-byte fields as little-endian and has no
  big-endian detection or byte-swapping path.
- Quantization behavior is mapped through current value 41, but fixture
  coverage is representative: 15, 40, 41, and unknown 999. It is not
  exhaustive value-by-value coverage.
- Scanner-relevant duplicates and bounded pending context candidates use
  first-occurrence-wins. Arbitrary duplicate keys are not globally retained or
  rejected.
- `general.architecture` is checked for string type, strict UTF-8, and
  nonblank content, but the scanner does not additionally enforce the GGUF
  lowercase ASCII architecture-value grammar.
- `file-access-denied` and opening-time `file-read-error` are narrow production
  mappings without a portable deterministic packaged test that forces those
  host conditions.
- Task 9 ran explicit standalone application restores/builds, but did not run
  the Task 10 whole-branch independent reviews or final repeated verification.
  No final-review claim is made here.
- No push, merge, publish, remote pull-request update, or other remote action
  occurred during Tasks 1 through 9.

## Task 1 setup repair

- Branch: `feature/winui-shell-model-import`
- Starting commit: `37a3943a38d3d6c7922eb2bdd4ed33d5f194675a`
- .NET SDK: `10.0.301`; MSBuild: `18.6.4+96856fd72`
- Packaged runner: Visual Studio 18 Community, VSTest `18.7.0 (x64)`

## Reproduced baseline

```text
Debug build: PASS, 0 warnings, 0 errors.
VS 2022 VSTest 17.14: no suitable test runtime provider.
VS 18 VSTest 18.7: package deployed and 2 scanner tests executed.
Existing result: 1 passed, 1 failed.
Failure cause: I-001 was not copied; I-002 was copied.
Repository-root parenthesis hypothesis: current root deployed successfully.
```

## Repair

- Replaced the single malformed-fixture item with project-relative wildcards for
  `GGUF`, `Malformed`, and `ExpectedMetadata`.
- Removed the duplicate unsupported-version direct scanner test from
  `ModelQuickScannerTests`; the test remains in `GgufQuickScannerTests`.
- Restored production and test-project launch-profile ownership.
- Added `tests/TestFixtures` to CI sparse checkout and made runner discovery
  select the latest Visual Studio TestWindow runner.

## Verification

```powershell
dotnet restore 'tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj' `
    --runtime win-x64 `
    -p:Platform=x64
```

```text
Determining projects to restore...
All projects are up-to-date for restore.
```

```powershell
dotnet build 'tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj' `
    --configuration Debug `
    --no-restore `
    --runtime win-x64 `
    -p:Platform=x64
```

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

```powershell
$configuration = 'Debug'
$filter = 'FullyQualifiedName~GraniteEdgeAI.UnitTests.GgufQuickScannerTests'
$trxName = 'task-01-existing-scanner.trx'
$recipe = "tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\$configuration\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe"
$results = "TestResults\GGUF-Quick-Scanner\$configuration"
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vstest = & $vswhere -latest -products * -find '**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' |
    Select-Object -First 1
New-Item -ItemType Directory -Force -Path $results | Out-Null
$resolvedRecipe = (Resolve-Path -LiteralPath $recipe).Path
$resolvedResults = (Resolve-Path -LiteralPath $results).Path
& $vstest $resolvedRecipe /Platform:x64 /TestCaseFilter:"$filter" /Logger:"trx;LogFileName=$trxName" "/ResultsDirectory:$resolvedResults"
```

```text
VSTest version 18.7.0 (x64)
Deployment succeeded.
Passed ScanAsync_UnsupportedVersion_ReturnsUnsupportedVersionFailure [21 ms]
Passed ScanAsync_InvalidMagic_ReturnsInvalidMagicFailure [24 ms]
Test Run Successful.
Total tests: 2
     Passed: 2
```

The generated appx recipe enumerated all fixture inputs. The VS18 deployment
refreshed the recipe-owned `AppX` layout, which contains 21 `.gguf` files and
eight expected-metadata `.json` files under `AppX\TestFixtures`.

## Notes

`AppX` is refreshed by the VSTest recipe deployment, not by a regular project
build. Before the runner was invoked, its previously deployed layout contained
only I-002 even though the generated recipe already contained all fixture items.

The branch baseline recorded above is the commit used for this verification.

## Task 2 fixed-header TDD evidence

Task 2 was run on top of the setup-repair commit with the packaged Visual Studio
18 VSTest runner. Every invocation used the generated `.build.appxrecipe`, a
Debug `win-x64` build with `-p:Platform=x64`, and the same results directory:

```powershell
$testProject = 'tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj'
$configuration = 'Debug'
$recipe = "tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\$configuration\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe"
$results = "TestResults\GGUF-Quick-Scanner\$configuration"
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vstest = & $vswhere -latest -products * -find '**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' |
    Select-Object -First 1

dotnet build $testProject --configuration $configuration --no-restore --runtime win-x64 -p:Platform=x64
New-Item -ItemType Directory -Force -Path $results | Out-Null
$resolvedRecipe = (Resolve-Path -LiteralPath $recipe).Path
$resolvedResults = (Resolve-Path -LiteralPath $results).Path
& $vstest $resolvedRecipe /Platform:x64 /TestCaseFilter:"$filter" /Logger:"trx;LogFileName=$trxName" "/ResultsDirectory:$resolvedResults"
```

All listed builds succeeded with zero warnings and zero errors. The precondition
and pre-cancellation tests were intentionally added after their implementation:
commit `37a3943` already used `ArgumentException.ThrowIfNullOrWhiteSpace` and
checked a pre-cancelled token before opening the path. They therefore passed on
their first run rather than being artificially made RED.

| Cycle | Filter result | Evidence |
|---|---|---|
| 2.1 | `ScanAsync_NullPath_ThrowsArgumentException`, `ScanAsync_EmptyPath_ThrowsArgumentException`, and `ScanAsync_WhitespacePath_ThrowsArgumentException`: each passed, 1/1 | `ScanAsync_NullPath_ThrowsArgumentException.trx`, `ScanAsync_EmptyPath_ThrowsArgumentException.trx`, `ScanAsync_WhitespacePath_ThrowsArgumentException.trx` |
| 2.2 | `ScanAsync_PreCancelledToken_ThrowsOperationCanceledException`: passed, 1/1 | `ScanAsync_PreCancelledToken_ThrowsOperationCanceledException.trx` |
| 2.3 RED | Empty fixture failed, 0/1, with an unhandled `System.IO.EndOfStreamException` from `ReadExactlyAsync` | `cycle-2-3-red.trx` |
| 2.3 GREEN | Empty fixture returned `truncated-header`, passed 1/1 | `cycle-2-3-green.trx` |
| 2.4 RED | Partial-header fixture failed, 0/1, with the temporary `NotImplementedException` before tensor-count handling | `cycle-2-4-red.trx` |
| 2.4 GREEN | Partial-header fixture returned `truncated-header` identifying tensor count at offset 8, passed 1/1 | `cycle-2-4-green.trx` |
| 2.5 RED | Valid zero-metadata header failed, 0/1, with the temporary `NotImplementedException` | `cycle-2-5-red.trx` |
| 2.5 GREEN | Valid zero-metadata header returned `missing-required-architecture`, passed 1/1 | `cycle-2-5-green.trx` |
| 2.6 | `FullyQualifiedName~GraniteEdgeAI.UnitTests.GgufQuickScannerTests`: passed 9/9 | `task-02-post-refactor.trx` |

The red and green TRX files are under
`TestResults\GGUF-Quick-Scanner\Debug`. The final packaged regression ran the
two established magic/version tests plus the seven new input, cancellation, and
fixed-header tests.

## Task 2 review follow-up

The fixed-header reader now completes all 24 header bytes before classifying a
complete signature. This ensures that a file shorter than 24 bytes returns
`truncated-header` even if its first four bytes are not `GGUF`. The generated
`I-024-short-invalid-magic.gguf` fixture is 12 bytes: `TEST`, version 3, and
half of the tensor-count field.

- RED: `task-02-review-short-invalid-magic-red.trx` failed 0/1 on the prior
  implementation because it returned `invalid-magic`.
- GREEN: `task-02-review-short-invalid-magic-green.trx` passed 1/1 after the
  complete-header ordering fix.
- Affected header tests: `task-02-review-affected-header-tests.trx` passed
  4/4, including empty, partial, short-invalid-magic, and complete
  invalid-magic inputs.
- Final direct scanner regression:
  `task-02-review-post-fixes-final.trx` passed 10/10.

The deployed `AppX\TestFixtures` layout now contains 22 `.gguf` files. Header
diagnostics now include actual signature bytes for invalid magic and actual
bytes read as well as the expected width for truncation. Historical attribution
for the path-validation and pre-cancellation behavior was corrected to
`37a3943`.

## Task 3 bounded metadata TDD evidence

Task 3 ran from `a441ab1` with the Visual Studio 18 VSTest 18.7.0 x64 runner.
Each RED and GREEN invocation built the Debug `win-x64` packaged test project,
deployed its generated `.build.appxrecipe`, selected one exact fully qualified
test name, and wrote a TRX file beneath
`TestResults\GGUF-Quick-Scanner\Debug`.

The corrected packaged command form resolved the recipe and results paths
before passing them to VSTest:

```powershell
$testProject = 'tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj'
$configuration = 'Debug'
$recipe = "tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\$configuration\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe"
$results = "TestResults\GGUF-Quick-Scanner\$configuration"
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vstest = & $vswhere -latest -products * -find '**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' |
    Select-Object -First 1

dotnet build $testProject --configuration $configuration --no-restore --runtime win-x64 -p:Platform=x64
New-Item -ItemType Directory -Force -Path $results | Out-Null
$resolvedRecipe = (Resolve-Path -LiteralPath $recipe).Path
$resolvedResults = (Resolve-Path -LiteralPath $results).Path
& $vstest $resolvedRecipe /Platform:x64 /TestCaseFilter:"$filter" /Logger:"trx;LogFileName=$trxName" "/ResultsDirectory:$resolvedResults"
```

| Cycle | RED evidence | GREEN evidence |
|---|---|---|
| 3.1 metadata count | `cycle-3-1-red.trx`: failed 0/1 because the old implementation returned `missing-required-architecture` for 1,000,001 entries | `cycle-3-1-green.trx`: passed 1/1 with `excessive-metadata-count` and the actual/limit diagnostics |
| 3.2 key length | `cycle-3-2-red.trx`: failed 0/1 because the old implementation returned `missing-required-architecture` for a 65,536-byte declared key | `cycle-3-2-green.trx`: passed 1/1 with `metadata-key-too-long` before narrowing or allocation |
| 3.3 value type | `cycle-3-3-red.trx`: failed 0/1 because type 99 was not dispatched | `cycle-3-3-green.trx`: passed 1/1 with `unsupported-metadata-type` for `fixture.unknown` |
| 3.4 truncated string | `cycle-3-4-red.trx`: failed 0/1 because the string payload was not consumed | `cycle-3-4-green.trx`: passed 1/1 with `truncated-metadata`, key/stage/offset, three actual bytes, and 20 expected bytes |
| 3.5 strict boolean | `cycle-3-5-red.trx`: failed 0/1 because byte 2 was skipped and the scan reached missing architecture | `cycle-3-5-green.trx`: passed 1/1 with `invalid-boolean-value` and the required 0-or-1 domain |
| 3.6 truncated array | `cycle-3-6-red.trx`: failed 0/1 because array structure was not consumed | `cycle-3-6-green.trx`: passed 1/1 after the fixed-width payload check found four available bytes versus 12 declared bytes |

The first Cycle 3.2 GREEN build exposed an `int`-to-`ulong` argument conversion
mistake and the XAML compiler emitted its follow-on error. No test ran and no
TRX was produced for that attempt. The explicit conversion was corrected; the
recorded `cycle-3-2-green.trx` rerun built with zero warnings and zero errors
and passed 1/1. All other recorded cycle builds also completed with zero
warnings and zero errors.

The implementation now performs one bounded metadata pass, validates strict
UTF-8/ASCII keys, dispatches official types 0 through 12, skips unknown strings
without allocation, validates boolean bytes, and recursively consumes nested
arrays. The scanner applies the GGUF key specification limit separately from
the application limits for strings, per-array elements, total array elements,
and nesting depth. Its aggregate array counter is local to one scan.

After the helper refactor, the full direct-scanner packaged filter
`FullyQualifiedName~GraniteEdgeAI.UnitTests.GgufQuickScannerTests` passed
16/16 with zero failures and no skips:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)

Test Run Successful.
Total tests: 16
     Passed: 16
```

The final evidence is
`TestResults\GGUF-Quick-Scanner\Debug\task-03-post-refactor.trx`.

## Task 3 security-review fixes

The Task 3 security review reported zero Critical, one Important, and two
Minor findings. The Important finding was verified against the official
[GGUF metadata-key caveats](https://github.com/ggml-org/ggml/blob/master/docs/gguf.md):
ASCII alone is insufficient. A key must be hierarchical, with nonempty
`lower_snake_case` segments separated by dots.

`Generate-GgufMetadataFixtures.ps1` generated four complete single-entry
regressions. Each entry uses a uint8 payload so the key grammar is its only
malformed structure:

| Fixture | Bytes | SHA-256 | Rule exercised |
|---|---:|---|---|
| `I-025-empty-metadata-key.gguf` | 37 | `2cef2535ce942753bc61a01c22b2f50bad84fc8b5945f487a2542d42bbb81ced` | Empty key |
| `I-026-empty-key-segment.gguf` | 50 | `7f433c3e0c61dbab04ebb8f0f955f7948cb0a4f1ea90b75d240aca6c88dfc601` | Adjacent dots |
| `I-027-key-with-space.gguf` | 49 | `f6312a5b66b8c5c27be636259d7ac7fb506a156f1a205ef82fdcd42b836f4920` | U+0020 at index 7 |
| `I-028-uppercase-key.gguf` | 49 | `bdb9ec1266004d214f61fbb5605d925ac7ee17645cee9a6912c26c4bcaa9dbfe` | U+0047 at index 0 |

Repository inspection after generation showed only the generator and these four
new binaries changed under `tests\TestFixtures`; no existing generated binary
or expectation JSON changed. The source fixture inventory and the deployed
`AppX\TestFixtures` layout each contain 26 `.gguf` files.

The exact-name OR filter selected only the four new regression methods:

- RED: `task-03-review-key-grammar-red.trx` failed 0/4. The ASCII-only
  implementation accepted all four keys and reached
  `missing-required-architecture`.
- GREEN: `task-03-review-key-grammar-green.trx` passed 4/4. Each result used
  `invalid-metadata-key` and reported the entry, key-byte offset, and a safe
  character index/code or empty-segment rule without echoing the invalid key.

The final packaged direct-scanner regression built with zero warnings and zero
errors and passed 20/20 with no failures or skips:

```text
Test Run Successful.
Total tests: 20
     Passed: 20
```

Final evidence:
`TestResults\GGUF-Quick-Scanner\Debug\task-03-review-post-fixes-final.trx`.

For clarity, unretained string values have their declared length, application
limit, and remaining-byte range validated, but their payload UTF-8 is
intentionally not decoded merely to skip them. At this Task 3 checkpoint,
broad scalar/nested-array support was established by code inspection plus the
malformed subset. Task 6 later added generated all-type, nested-array, and
remaining boundary coverage, recorded in its section below.

## Task 4 required metadata extraction TDD evidence

Task 4 implementation started from base commit `ea8b4b5` and became commit
`a9973ca`. It used the Visual Studio 18 VSTest 18.7.0 x64 runner, the generated
Debug `win-x64` appxrecipe, and deployed fixture assertions for every
fixture-backed test. Every listed build completed with zero warnings and zero
errors.

| Cycle | Result | Evidence |
|---|---|---|
| 4.1 missing architecture | Passed 1/1 immediately. Task 3 already consumed all entries and returned `missing-required-architecture`; the test documents that existing observable behavior without manufacturing a RED. | `cycle-4-1-existing-green.trx` |
| 4.2 architecture type RED | Failed 0/1: a UInt32 `general.architecture` reached `missing-required-architecture` instead of reporting its wrong type. | `cycle-4-2-4-3-red.trx` |
| 4.2 architecture type GREEN | Passed after the scanner required a String value before retained decoding and returned `invalid-architecture-type` with actual/expected types. | `cycle-4-2-4-3-green.trx` |
| 4.3 complete metadata RED | Failed 0/1: V-001 returned `Failure` because known display metadata was only skipped. | `cycle-4-2-4-3-red.trx` |
| 4.3 complete metadata GREEN | Passed after bounded strict-UTF-8 retained-string reads, UInt32/UInt64 normalization, and `ModelQuickScanResult.CreateSuccess`. | `cycle-4-2-4-3-green.trx` |

The successful V-001 test deserializes its checked-in expectation as typed
`ExpectedFixture`/`ExpectedMetadata` records and asserts every success field,
including file length, GGUF version, context length, and `15 => Q4_K_M`; it
also asserts all failure fields are null. Task 4 introduced only that required
file-type mapping plus `Unknown (file type N)` fallback. Task 5 later expanded
the mapping through current official value 41. Quantization fixture coverage is
representative rather than exhaustive: V-001 checks 15, review fixture V-011
checks 41, and Task 6 added checks for 40 and unknown value 999.

The post-refactor packaged direct-scanner regression was:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)

Test Run Successful.
Total tests: 23
     Passed: 23
```

Its result is
`TestResults\GGUF-Quick-Scanner\Debug\task-04-post-refactor.trx`.

## Task 4 review fixes

The Task 4 review reported zero Critical, two Important, and two Minor
findings. The scanner now compares an architecture-specific context key by
exact length plus ordinal suffix/prefix checks. It no longer constructs
`architecture + ".context_length"` for every later entry, avoiding repeated
large temporary allocations when a file supplies a retained architecture near
the 16 MiB string limit.

The V-001 expectation test now treats a JSON `null` result as a failed setup
assertion rather than an inconclusive test. JSON parsing exceptions continue to
surface as test errors. The test also asserts the JSON `expectedOutcome`
against `result.Outcome.ToString()`.

Fresh packaged Debug evidence:

| Verification | Result | TRX |
|---|---|---|
| Exact V-001 plus affected missing/wrong architecture tests | 4 total, 4 executed, 4 passed, 0 failed, 0 error, 0 inconclusive, 0 not executed | `task-04-review-affected-metadata.trx` |
| Full direct scanner class | 23 total, 23 executed, 23 passed, 0 failed, 0 error, 0 inconclusive, 0 not executed | `task-04-review-post-fixes-final.trx` |

Both packaged invocations built with zero warnings and zero errors. The TRX
files are under `TestResults\GGUF-Quick-Scanner\Debug`.

## Task 5 optional metadata and ordering TDD evidence

Task 5 started from `db1ee28` and added V-002 through V-008 as direct,
fixture-deployment-asserting tests before scanner changes. The existing Task 4
implementation already supplied filename fallback, null optional fields, safe
unknown-value consumption, and direct architecture-first UInt32 context
normalization. Those tests therefore document established behavior; no
artificial RED was manufactured for them.

The V-006 test exposed the genuine missing behavior:

| Cycle | Result | TRX |
|---|---|---|
| 5.5 RED | Context preceding `general.architecture` was discarded; expected `131072`, actual `null`; 1 total/executed, 0 passed, 1 failed, 0 error, inconclusive, or not executed | `cycle-5-5-red.trx` |
| 5.5 GREEN | The bounded pending-candidate resolution returned the expected context; 1 total/executed/passed, 0 failed, error, inconclusive, or not executed | `cycle-5-5-green.trx` |
| 5.8 regression | Direct scanner class: 30 total/executed/passed, 0 failed, error, inconclusive, or not executed | `task-05-final.trx` |

Each listed Debug `win-x64` packaged build completed with zero warnings and
zero errors using VSTest 18.7.0 and the generated `.build.appxrecipe`.

The scanner retains at most 64 distinct pre-architecture keys that end in
`.context_length`. Each candidate is consumed once: UInt32 and UInt64 values
are normalized to `ulong`; every other declared type is safely skipped and its
type retained. Once architecture is available, an allocation-free exact
length/suffix/prefix comparison selects only its matching candidate. Code
inspection shows a 65th distinct candidate reaches
`excessive-context-candidate-count` with actual count 65 and limit 64
diagnostics. Task 6 fixture I-017 later supplied the public-scan validation of
that boundary. Unrelated wrong-type candidates remain harmless.

`MapFileTypeToQuantization` now covers labels 0 through 41 from the current
[llama.cpp `llama_ftype` enum](https://github.com/ggml-org/llama.cpp/blob/master/include/llama.h),
including `15 => Q4_K_M`, `40 => Q1_0`, and `41 => Q2_0`. Historical labels
4-6 and 33-35 remain mapped for compatibility; values outside this set return
`Unknown (file type N)`.

## Task 5 review fixes

The review follow-up generated two additional fixtures solely through
`Generate-GgufMetadataFixtures.ps1`:

| Fixture | Bytes | SHA-256 | Public behavior |
|---|---:|---|---|
| `V-011-current-q2_0-file-type.gguf` | 320 | `ff4540498e5fea00b3c321c19a3ab0b7919451ae5fe5f0e5880e867c764d9258` | Current file type 41 displays as `Q2_0` |
| `V-012-duplicate-relevant-metadata.gguf` | 544 | `a7ba69c11ee794f13105ebee23614094b86b66422e5df44bf3c46fbad91dae58` | Scanner-relevant duplicate fields and exact context keys retain their first occurrence |

The shared typed expectation helper now requires each test to supply the
expected fixture ID and asserts it independently from the expected fixture
filename. The generator also refreshes `fixture-manifest.json` with exact
length and SHA-256 records for the complete current GGUF fixture inventory.

The packaged two-test RED used the unchanged scanner:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)

V-011: expected Q2_0; actual Unknown (file type 41).
V-012: expected First Duplicate Fixture Name; actual Ignored Duplicate Name.
Total tests: 2
     Failed: 2
```

Evidence: `task-05-review-fixes-red.trx`.

The minimal implementation adds `41 => Q2_0` and bounded first-occurrence-wins
state only for scanner-relevant keys. Fixed Boolean flags cover the four
recognized `general.*` keys and resolved exact context. The existing
maximum-64 dictionary retains the first occurrence of each distinct
pre-architecture context candidate; it does not grow into a general metadata
key set. Later duplicates are consumed through the normal structural skip path
without replacing retained state. This keeps a duplicate architecture from
redirecting exact context matching. A first matching wrong-type candidate
still reaches `invalid-context-type`; Task 6 I-016 later supplied its generated
public-scan validation.

The focused packaged GREEN was:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)

Test Run Successful.
Total tests: 2
     Passed: 2
```

Evidence: `task-05-review-fixes-green.trx`. Both review TRX files are under
`TestResults\GGUF-Quick-Scanner\Debug`.

The final review-fix direct-scanner regression rebuilt the packaged Debug
`win-x64` project with zero warnings and zero errors, then ran the complete
class:

```text
Test Run Successful.
Total tests: 32
     Passed: 32
```

There were zero failed, error, inconclusive, and not-executed tests. Evidence:
`TestResults\GGUF-Quick-Scanner\Debug\task-05-review-fixes-final.trx`.

## Task 6 generated boundaries and packaged TDD evidence

Task 6 extended `Generate-GgufMetadataFixtures.ps1` to write every official
metadata value type 0 through 12 with the corresponding `BinaryWriter`
primitive overload. Boolean values are emitted as an explicit byte `0` or
`1`, and arrays remain recursively generator-owned. The two fixture scripts
were run successfully; no binary or expected-result file was edited by hand.

At the Task 6 snapshot, the generator produced 42 GGUF binaries: 13 under
`GGUF` and 29 under `Malformed`. It also produced 12 typed expected-result JSON
files. A
regenerate-and-compare check covered all 55 generated artifacts (42 binaries,
12 expectations, and the manifest) and found byte-for-byte deterministic
content. Independent validation parsed every JSON
file, matched all 42 manifest records to the actual files, and recomputed every
recorded byte length and SHA-256 hash.
I-019 constructs its non-ASCII `ï` as `[char] 0x00EF` from otherwise ASCII
script source so Windows PowerShell 5.1 and UTF-8-aware hosts do not interpret
the generator source differently.

| ID | Generated path | Bytes | SHA-256 |
|---|---|---:|---|
| V-009 | `GGUF/V-009-all-official-metadata-types.gguf` | 800 | `60a2bf3eb87e175780f0a497e01141a761226fbdcf5a67d73c769462341367ae` |
| V-010 | `GGUF/V-010-unknown-file-type.gguf` | 320 | `76b6811fca0100ec3fcb359845248ed16510c36cb259e7fb76810de14d443baf` |
| I-012 | `Malformed/I-012-oversized-metadata-string.gguf` | 68 | `3abd4bed4d42098d9b1bfa48ebc89c526d5a4ea9ab6638775a41f68c3e1c2747` |
| I-013 | `Malformed/I-013-excessive-array-count.gguf` | 71 | `ced815ded71fbb52a89260648443ba11bf69b6017f35beadd9d7f4f1f6f79e7d` |
| I-014 | `Malformed/I-014-excessive-total-array-count.gguf` | 119 | `ba920a378635b094a4c194a75307fdce9d0b4a4da7eca53496a707aa74784dbd` |
| I-015 | `Malformed/I-015-excessive-array-depth.gguf` | 160 | `96e4ff650a25ff47d83b93e05cbf825aedc847953e33c5d260ee5d55ac5769ff` |
| I-016 | `Malformed/I-016-invalid-context-type.gguf` | 128 | `f8536d245a710e6a8417d1efc841056163ed1116cfe0a2d61d894cedaf8dd2af` |
| I-017 | `Malformed/I-017-excessive-context-candidates.gguf` | 2,816 | `156cff109ff91006f562a5f074487904cd7cc635fded78d1b98965024a2b8001` |
| I-018 | `Malformed/I-018-invalid-utf8-architecture.gguf` | 66 | `737dc2f422eff9716edc95311bcada3ea735632046c006854e6423610050b7ca` |
| I-019 | `Malformed/I-019-non-ascii-key.gguf` | 64 | `73a3cef1886351357edde4223729ca4d3d176e22c448749f7a0cc22024da682a` |
| I-020 | `Malformed/I-020-wrong-name-type.gguf` | 128 | `afce52e673fc3fb12f0f97cb0a76a895669f418d2bcf03d2676ea740c7b2e773` |
| I-021 | `Malformed/I-021-wrong-size-label-type.gguf` | 128 | `6b8ac86095fc48a40a4b21af50eff0cf31843d1fef6b2e1c62541f35c49b542d` |
| I-022 | `Malformed/I-022-wrong-file-type.gguf` | 128 | `0dbe0bc77ae89cdd7d5af2d403f651a59ded37b06f71708f1b435fbd2d689a2e` |
| I-023 | `Malformed/I-023-blank-architecture.gguf` | 96 | `82c179f364552beb3b176d0b054dc9bc51d4cf2e6dd5ce69e663a767ca9902d4` |

Each new public test was selected through an exact fully qualified name
against the packaged Debug x64 appxrecipe before any missing production
behavior was changed. Cycles 6.1 through 6.14 passed on their first run because
Tasks 3 through 5 had already implemented the parser guards, all official
value consumption, nested arrays, and numeric file-type fallback. No
artificial RED was manufactured.

| Cycle | Exact test | First-run result | TRX |
|---|---|---|---|
| 6.1 | `ScanAsync_OversizedMetadataString_ReturnsMetadataStringTooLongFailure` | PASS 1/1 | `cycle-6-01-first-run.trx` |
| 6.2 | `ScanAsync_ExcessiveArrayCount_ReturnsExcessiveArrayCountFailure` | PASS 1/1 | `cycle-6-02-first-run.trx` |
| 6.3 | `ScanAsync_ExcessiveTotalArrayCount_ReturnsExcessiveArrayCountFailure` | PASS 1/1 | `cycle-6-03-first-run.trx` |
| 6.4 | `ScanAsync_ExcessiveArrayDepth_ReturnsExcessiveArrayDepthFailure` | PASS 1/1 | `cycle-6-04-first-run.trx` |
| 6.5 | `ScanAsync_ContextWithWrongType_ReturnsInvalidContextTypeFailure` | PASS 1/1 | `cycle-6-05-first-run.trx` |
| 6.6 | `ScanAsync_ExcessiveContextCandidates_ReturnsControlledFailure` | PASS 1/1 | `cycle-6-06-first-run.trx` |
| 6.7 | `ScanAsync_InvalidUtf8Architecture_ReturnsInvalidEncodingFailure` | PASS 1/1 | `cycle-6-07-first-run.trx` |
| 6.8 | `ScanAsync_NonAsciiKey_ReturnsInvalidMetadataKeyFailure` | PASS 1/1 | `cycle-6-08-first-run.trx` |
| 6.9 | `ScanAsync_NameWithWrongType_ReturnsInvalidNameTypeFailure` | PASS 1/1 | `cycle-6-09-first-run.trx` |
| 6.10 | `ScanAsync_SizeLabelWithWrongType_ReturnsInvalidSizeLabelTypeFailure` | PASS 1/1 | `cycle-6-10-first-run.trx` |
| 6.11 | `ScanAsync_FileTypeWithWrongType_ReturnsInvalidFileTypeFailure` | PASS 1/1 | `cycle-6-11-first-run.trx` |
| 6.12 | `ScanAsync_BlankArchitecture_ReturnsMissingArchitectureFailure` | PASS 1/1 | `cycle-6-12-first-run.trx` |
| 6.13 | `ScanAsync_AllOfficialMetadataTypes_ReturnsExpectedSuccessResult` | PASS 1/1 | `cycle-6-13-first-run.trx` |
| 6.14 | `ScanAsync_UnknownFileType_ReturnsDocumentedLabel` | PASS 1/1 | `cycle-6-14-first-run.trx` |

Cycle 6.15 supplied the genuine RED. The packaged test threw an unhandled
`FileNotFoundException`, so `cycle-6-15-red.trx` recorded 1 total/executed,
0 passed, and 1 failed. The minimal production change catches exceptions only
around `FileStream` construction:

- `FileNotFoundException` and `DirectoryNotFoundException` become
  `file-not-found`;
- `UnauthorizedAccessException` becomes `file-access-denied`;
- other opening-time `IOException` instances become `file-read-error`.

The parse block remains outside those operational catches, and
`OperationCanceledException` is not caught. The exact missing-file test then
passed 1/1 in `cycle-6-15-green.trx`.

The final packaged Debug `win-x64` build completed with zero warnings and zero
errors. VSTest 18.7.0 deployed the generated appxrecipe and ran the direct
scanner class:

```text
Test Run Successful.
Total tests: 47
     Passed: 47
```

There were zero failed, error, inconclusive, and not-executed tests. Evidence:
`TestResults\GGUF-Quick-Scanner\Debug\task-06-direct-scanner.trx`.

The host-determinism review fix regenerated I-019 with the intended UTF-8 key
bytes. Its focused packaged regression passed 1/1 in
`task-06-review-nonascii-green.trx`, followed by 47/47 direct-scanner tests in
`task-06-review-post-fix.trx`; both runs recorded zero failed, error,
inconclusive, or not-executed tests.

## Task 7 GGUF router integration evidence

Task 7 used Visual Studio 18 VSTest 18.7.0 with the generated Debug x64
`.build.appxrecipe`. Before replacing the temporary router test, the packaged
`FullyQualifiedName~GraniteEdgeAI.UnitTests.ModelQuickScannerTests` baseline
ran nine cases. Eight passed; the only failure was
`ScanAsync_GgufWithNonBlankPath_ReachesUnimplementedScanner`, because it still
expected an exact `NotImplementedException` after the scanner had become
functional. Evidence:
`TestResults\GGUF-Quick-Scanner\Debug\task-07-stale-router-baseline.trx`.

The obsolete assertion was replaced with three real router integrations. Each
new exact fully qualified filter passed on its first packaged run, before any
production behavior changed:

| Cycle | Exact test | First-run result | TRX |
|---|---|---|---|
| 7.1 | `ScanAsync_GgufWithValidFixture_ReturnsSuccessResult` | PASS 1/1; V-001 returned `Success`, the complete Granite metadata, file size 320, context 131072, and GGUF version 3 | `cycle-7-1-first-run.trx` |
| 7.2 | `ScanAsync_GgufWithInvalidFixture_ReturnsScannerFailure` | PASS 1/1; I-001 returned `Failure` with `invalid-magic` | `cycle-7-2-first-run.trx` |
| 7.3 | `ScanAsync_GgufWithPreCancelledToken_ReturnsCancelledResult` | PASS 1/1; the existing narrow router catch converted requested cancellation to `Cancelled` | `cycle-7-3-first-run.trx` |

Both fixture-backed tests explicitly verified their deployed fixture existed.
The initial working tree contained an uncommitted second blank line between
the `using` block and namespace in `ModelQuickScanner.cs`. Removing that
workspace-only line returned the file to its already-committed blob, so the
Task 7 commit correctly contains no production-file change; the intentional
blank separators between switch cases remain. No router behavior changed. A
repository search found no remaining `NotImplementedException`, `currently
unfinished`, or `unimplemented scanner` marker in production or unit-test
sources.

The final packaged command used:

```powershell
$testProject = 'tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj'
$configuration = 'Debug'
$recipe = "tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\$configuration\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe"
$results = "TestResults\GGUF-Quick-Scanner\$configuration"
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vstest = & $vswhere -latest -products * -find '**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' |
    Select-Object -First 1
$filter = 'FullyQualifiedName~GraniteEdgeAI.UnitTests.ModelQuickScannerTests'
$trxName = 'task-07-router.trx'

dotnet build $testProject `
    --configuration $configuration `
    --no-restore `
    --runtime win-x64 `
    -p:Platform=x64

New-Item -ItemType Directory -Force -Path $results | Out-Null
$recipePath = (Resolve-Path -LiteralPath $recipe).Path
$resultsPath = (Resolve-Path -LiteralPath $results).Path

& $vstest `
    $recipePath `
    '/Platform:x64' `
    "/TestCaseFilter:$filter" `
    "/Logger:trx;LogFileName=$trxName" `
    "/ResultsDirectory:$resultsPath"
```

The final build completed with zero warnings and zero errors; its console
output is recorded in
`TestResults\GGUF-Quick-Scanner\Debug\task-07-build.log`. All prior router cases
plus the three real GGUF integrations passed:

```text
Test Run Successful.
Total tests: 11
     Passed: 11
```

The final TRX records 11 total/executed/passed, with zero failed, error,
inconclusive, and not-executed tests:
`TestResults\GGUF-Quick-Scanner\Debug\task-07-router.trx`.

## Task 8 behavior-preserving parser refactor evidence

Task 8 changed no scanner behavior and did not modify the test sources. The
refactor extracted only three responsibilities already covered by the packaged
suite:

1. `CreateFormatFailure` centralizes construction of the existing controlled
   result from `GgufFormatException`.
2. `ScanOpenedFileAsync` owns fixed-header validation, the bounded metadata
   orchestration, required-architecture validation, filename fallback, and
   success creation.
3. `ReadMetadataAsync` owns the local retained state, local aggregate-array
   budget, and bounded metadata-entry loop.

`ScanAsync` still validates its public boundary, constructs `FileStream` inside
the same narrow open-time catches, owns `await using (stream)`, and catches only
`GgufFormatException` around the awaited open-file scan. Parse-time I/O and
`OperationCanceledException` remain uncaught. The metadata-entry count guard
remains in `ScanOpenedFileAsync`, before `ReadMetadataAsync` is called.

The pre-refactor Debug x64 safety net used a fresh build and these packaged
commands:

```powershell
dotnet build `
  'tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj' `
  --configuration Debug `
  --no-restore `
  --runtime win-x64 `
  -p:Platform=x64

& $vstest `
  $recipePath `
  '/Platform:x64' `
  '/TestCaseFilter:FullyQualifiedName~GraniteEdgeAI.UnitTests.GgufQuickScannerTests' `
  '/Logger:trx;LogFileName=task-08-pre-refactor-direct.trx' `
  "/ResultsDirectory:$resultsPath"

& $vstest `
  $recipePath `
  '/Platform:x64' `
  '/TestCaseFilter:FullyQualifiedName~GraniteEdgeAI.UnitTests.ModelQuickScannerTests' `
  '/Logger:trx;LogFileName=task-08-pre-refactor-router.trx' `
  "/ResultsDirectory:$resultsPath"
```

The build completed with zero warnings and zero errors. The direct class passed
47/47 and the router class passed 11/11, with zero failed, error, inconclusive,
or not-executed tests.

After each individual extraction, the same fresh Debug x64 build completed with
zero warnings and zero errors and the packaged direct class passed 47/47:

| Extraction | TRX | Total/executed/passed | Failed/error/inconclusive/not executed |
|---|---|---:|---:|
| `CreateFormatFailure` | `task-08-step-1-format-failure.trx` | 47/47/47 | 0/0/0/0 |
| `ScanOpenedFileAsync` | `task-08-step-2-opened-file.trx` | 47/47/47 | 0/0/0/0 |
| `ReadMetadataAsync` | `task-08-step-3-metadata-loop.trx` | 47/47/47 | 0/0/0/0 |

The required combined static scan was:

```powershell
rg -n 'catch\s*\(Exception|BinaryReader|Read\(|ReadByte\(|NotImplementedException|Task\.Run|new byte\[[^\]]*(ulong|length)' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelImport/QuickScan/GgufQuickScanner.cs'
```

It returned exactly three allocation matches, at final source lines 348, 404,
and 1378. Each is a fixed eight-byte `new byte[sizeof(ulong)]` buffer used to
decode one GGUF unsigned 64-bit field; none is sized from attacker-controlled
input. A separate scan of the forbidden alternatives returned no match (ripgrep
exit code 1):

```powershell
rg -n 'catch\s*\(Exception|BinaryReader|\.Read\(|ReadByte\(|NotImplementedException|Task\.Run' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelImport/QuickScan/GgufQuickScanner.cs'
```

Final verification rebuilt the packaged Debug x64 project with zero warnings
and zero errors, then ran:

```powershell
& $vstest `
  $recipePath `
  '/Platform:x64' `
  '/TestCaseFilter:FullyQualifiedName~GraniteEdgeAI.UnitTests.GgufQuickScannerTests' `
  '/Logger:trx;LogFileName=task-08-final-direct.trx' `
  "/ResultsDirectory:$resultsPath"

& $vstest `
  $recipePath `
  '/Platform:x64' `
  '/TestCaseFilter:FullyQualifiedName~GraniteEdgeAI.UnitTests.ModelQuickScannerTests' `
  '/Logger:trx;LogFileName=task-08-final-router.trx' `
  "/ResultsDirectory:$resultsPath"

& $vstest `
  $recipePath `
  '/Platform:x64' `
  '/Logger:trx;LogFileName=task-08-final-full.trx' `
  "/ResultsDirectory:$resultsPath"
```

The direct scanner class passed 47/47, the router class passed 11/11, and the
full packaged suite passed 95/95. Every final TRX records zero failed, error,
inconclusive, and not-executed tests. All Task 8 TRX files are under
`TestResults\GGUF-Quick-Scanner\Debug`.
