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
intentionally not decoded merely to skip them. The scanner's broad support for
official scalar types and nested arrays is established here by code inspection
plus the Task 3 malformed subset. Task 6 remains responsible for generated
full-type, nested-array, and remaining boundary coverage.

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
also asserts all failure fields are null. Task 4 introduces only that required
file-type mapping plus `Unknown (file type N)` fallback. Task 5 expands the
mapping through the current official value 41. Quantization fixture coverage is
representative rather than exhaustive: V-001 checks 15, review fixture V-011
checks 41, and Task 6 retains planned checks for 40 and unknown value 999.

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
diagnostics; Task 6 fixture I-017 remains the planned public-scan validation of
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
still reaches `invalid-context-type`; Task 6 I-016 remains its planned
generated public-scan validation.

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
