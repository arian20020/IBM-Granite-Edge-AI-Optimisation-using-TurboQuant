# Application and System Test Projects

This directory contains the executable application tests, controlled fixtures,
and placeholders for later test layers.

```text
tests/
|-- UnitTests/
|   `-- GraniteEdgeAI.UnitTests/
|-- ContractTests/
|-- IntegrationTests/
|-- E2ETests/
`-- TestFixtures/
```

Creating a folder is preparation, not test evidence. A test layer counts as
implemented only when it has executable tests and preserved results.

## Current executable test project

[GraniteEdgeAI.UnitTests](UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj)
is a packaged WinUI 3 MSTest application. Its project reference compiles the
application, and its app-container runner supplies the XAML UI thread required
by `[UITestMethod]`.

The current 108-execution suite contains:

| Area | Test methods | Executions | Purpose |
|---|---:|---:|---|
| `GgufQuickScannerTests` | 55 | 58 | Real-file GGUF header, metadata, limit, cancellation, and failure behavior |
| `GgufFixtureIntegrityTests` | 1 | 1 | Packaged fixture set, byte-length, and SHA-256 enforcement |
| `ModelQuickScannerTests` | 11 | 12 | Format routing, path validation, and requested-cancellation behavior |
| `ModelQuickScanResultTests` | 17 | 27 | Immutable success/failure invariants |
| Picker and foundation tests | 10 | 10 | Nine picker seam executions and one runner smoke execution |
| **Full project** | **94** | **108** | Current packaged test total |

The scanner tests are categorized as `Unit`, but the fixture-backed cases open
real files copied into the package output. They therefore also exercise the
scanner/file-system/package boundary; the category name does not turn those
cases into pure in-memory unit tests.

## GGUF fixtures

The controlled fixture set contains 52 tiny `.gguf` binaries:

- 13 GGUF/header inputs under [GGUF](TestFixtures/GGUF/README.md).
- 39 deliberately malformed inputs under
  [Malformed](TestFixtures/Malformed/README.md).
- 12 independent expected-result JSON files under
  [ExpectedMetadata](TestFixtures/ExpectedMetadata/README.md).
- A generated [fixture manifest](TestFixtures/fixture-manifest.json) containing
  the byte length and SHA-256 digest of every binary.

These files contain no model tensors or model weights. Regenerate the complete
set from the repository root through its authoritative orchestration script:

```powershell
& ".\tests\TestFixtures\Generate-GgufFixtures.ps1"
```

Never hand-edit generated `.gguf` files. The orchestrator removes all generated
GGUF/JSON outputs first, runs both generator families, and lets the metadata
generator refresh the manifest. That clean start makes removed or renamed
writer calls visible instead of preserving stale files. Expected-result JSON
remains independent of production scanner output so a production defect cannot
silently redefine the expected answer.

The test project copies `GGUF`, `Malformed`, `ExpectedMetadata`, and the
integrity manifest into its packaged output with `PreserveNewest`. Tests
resolve them through `AppContext.BaseDirectory`, not through a
developer-specific source path.

## Build and run locally

Run packaged WinUI tests through Visual Studio's app-container VSTest runner.
`dotnet test` is not the runner for this project because it does not launch the
generated `.build.appxrecipe` in the required app-container/XAML environment.

The following PowerShell sequence works from the repository root. Change
`Debug` to `Release` consistently when collecting a Release snapshot.

```powershell
$testProject = ".\tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj"
$configuration = "Debug"
$resultDirectory = ".\TestResults\GGUF-Quick-Scanner\$configuration"
$recipe = ".\tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\$configuration\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe"

dotnet restore $testProject --runtime win-x64 -p:Platform=x64
dotnet build $testProject `
  --configuration $configuration `
  --no-restore `
  --runtime win-x64 `
  -p:Platform=x64

New-Item -ItemType Directory -Force -Path $resultDirectory | Out-Null
$recipePath = (Resolve-Path -LiteralPath $recipe).Path
$resultsPath = (Resolve-Path -LiteralPath $resultDirectory).Path

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vstest = & $vswhere `
  -latest `
  -products * `
  -find "**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe" |
  Select-Object -First 1

if (-not $vstest) {
  throw "Visual Studio app-container test runner was not found."
}

& $vstest `
  $recipePath `
  '/Platform:x64' `
  '/Logger:trx;LogFileName=full.trx' `
  "/ResultsDirectory:$resultsPath"
```

Add one of these quoted filters before the logger option for a focused run:

```powershell
/TestCaseFilter:"FullyQualifiedName~GgufQuickScannerTests"
/TestCaseFilter:"FullyQualifiedName~GgufFixtureIntegrityTests"
/TestCaseFilter:"FullyQualifiedName~ModelQuickScannerTests"
```

Treat the generated TRX file as the authoritative count ledger. Preserve the
build log beside it when the snapshot must prove warning/error counts as well as
test outcomes.

## GGUF scanner documentation and evidence

- [GGUF Quick Scanner Beginner Guide](../docs/development/GGUF-Quick-Scanner-Beginner-Guide.md)
  explains the format, implementation, limits, fixtures, tests, and extension
  procedure.
- [GGUF Quick Scanner Test Report](../docs/evidence/testing/GGUF-Quick-Scanner-Test-Report.md)
  records historical task evidence and the current packaged Debug/Release
  snapshot.

The documented scanner boundary ends at the router result. The current
`ModelImportPage` can select a file and display its scanning state, but it does
not yet invoke `ModelQuickScanner`; do not describe the fixture tests as an
end-to-end import journey.

## Continuous integration

[build-and-test.yml](../.github/workflows/build-and-test.yml) uses a sparse
checkout containing the application, packaged test project, and fixtures. It
regenerates fixtures with Windows PowerShell 5.1 and requires a clean fixture
tree, restores and builds the WinUI application, builds the packaged test
project, runs the `.build.appxrecipe` through VSTest on a Windows runner, and
requires an all-passing nonempty TRX with executed scanner, fixture-integrity,
and router tests. It uploads available test results for 30 days.

The workflow currently executes the full packaged project. The focused filters
above are for local diagnosis and evidence snapshots.

## Reserved test layers

`ContractTests`, `IntegrationTests`, and `E2ETests` currently contain
documentation/placeholders rather than executable projects. Do not count them
as implemented coverage. Future tests should move into those layers only when
their scope matches the definitions below:

- Unit tests verify deterministic logic and narrow component behavior.
- Contract tests verify adapters against pinned external-tool behavior.
- Integration tests verify component and process boundaries.
- End-to-end tests verify complete user journeys and important failure
  journeys.
- Performance and AI-quality results belong under
  `experiments/granite_turboquant_intel/` and must identify the matching
  application behavior when relevant.

All fixtures must remain small, licensed, deterministic, and safe to commit.
Never store model weights, secrets, private personal data, or unlicensed
material here.
