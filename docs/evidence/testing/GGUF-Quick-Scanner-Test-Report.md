# GGUF quick-scanner packaged test report

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
commit `b85f8fd` already used `ArgumentException.ThrowIfNullOrWhiteSpace` and
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
