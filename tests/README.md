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

## Packaged application test project

[GraniteEdgeAI.UnitTests](UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj)
is a packaged WinUI 3 MSTest application. Its project reference compiles the
application, and its app-container runner supplies the XAML UI thread required
by `[UITestMethod]`.

The ordinary hosted-equivalent Release/x64 floor is 686 tests with this exact
filter:

```text
TestCategory!=ModelInspectionVisualRegression&TestCategory!=ModelInspectionControlledOs
```

That floor was observed in a local packaged candidate based on commit
`5f90a5d9299363214f11454f548ff8571d98b1a5`: total 686, executed 686, passed
686, and zero non-passing results. It includes scanner/import coverage, Model
Inspection contracts, presentation/view/render/motion/control/accessibility
coverage, onboarding/navigation, and application-root/worker-package
composition. The composition suite includes a real package-identity-root
launch through the production client and controlled five-stage CPU/VocabOnly
completion. The package test requires the detached manifest-verified 44-file
CPU worker closure under `ModelInspection/Worker` and rejects
LLamaSharp/native files at the application root.

The final Task 10 local hosted-equivalent Release/x64 candidate retained that
exact filter and 686 minimum while passing 691/691 with zero non-passing
results. Its protected map contains 31 classes and 497 executions, including
39 `ModelInspectionPageNavigationTests`. The ephemeral Release TRX SHA-256 was
`E6C1EF3B00A238ED14BE33AE513A8D7E3AC06382F07AAF62A0404EC73B5F255F`.
This newer count does not replace the permanent 686 floor or claim that the
hosted GitHub workflow ran at the candidate head.

The generated local TRX files are the authoritative source for execution
counts, but they contain machine names and absolute paths. Both packaged-unit
and Gate 2 TRX remain runner-ephemeral and are not uploaded by ordinary CI. The
permanent workflow validates their counters and exact protected class counts
before the runner discards them.

The scanner tests are categorized as `Unit`, but the fixture-backed cases open
real files copied into the package output. They therefore also exercise the
scanner/file-system/package boundary; the category name does not turn those
cases into pure in-memory unit tests.

## Model Inspection Debug fixture gallery

To inspect a deterministic Model Inspection state manually, select `Debug`
and `x64`, build and launch the packaged application, then click
`Fixture gallery` on the onboarding shell. Search or filter the catalogue,
select an `MI-NNN` row, and use only the controls shown for that fixture.
Supported safe routes include the descriptor-declared Cancel, Retry, Restart,
Choose another, disclosure expand/collapse, Reset fixture, fixture switching,
and Close actions. Future report, conversion, and Hardware Fit controls remain
disabled with `Coming later`; the gallery does not dispatch them.

The authoritative catalogue is the exact `MI-001` through `MI-050` descriptor
set under
[ModelInspectionScenarios](TestFixtures/ModelInspectionScenarios/README.md).
Every filename is
`MI-NNN-<target-condition>[-<variant>].fixture.json`, using lowercase
hyphenated slugs and the matching stable ID. The checked-in
[generated catalogue](../docs/evidence/testing/Model-Inspection-Fixture-Catalog.md)
lists every exact filename, target, interaction, preset, provenance, and the
separate N-001 evidence joins.

The gallery is synthetic and deterministic. It reads only the allowlisted
schema, policy, and descriptors from the fixed packaged
`ms-appx:///Fixtures/` root, and it does not start
the worker, open a model, use a current-directory fallback, or perform network
or arbitrary filesystem I/O. High Contrast, 200% text scale, width, and motion
presets are in-app previews; they are not evidence of actual OS High Contrast,
actual OS 200% text scale, Narrator, or strict comparison with approved Figma
PNGs. Synthetic outcome screens likewise do not prove real-worker outcomes.
Only the catalogue's external N-001 links identify separately verified
production-worker/page evidence.

The fresh local Debug/x64 campaign on 2026-08-13 passed 220/220 with the exact
nine-class map: service 41, adapter 12, gallery 73, interaction 6, lifetime 13,
page lifecycle 7, preset 25, screen contract 37, and ViewModel integration 6.
The interaction/lifetime prerequisite pair passed 19/19 (6 plus 13). The
category TRX SHA-256 was
`847D8EEC40AE653FF61BC3D45C557D599E14CDEE09E805C569022FB036BD51E3`; the
pair TRX SHA-256 was
`741DAD14EA1C5935DF07BC1128FEAE3AF3627C3EA67A4E23B27FABD3228E69A0`.
The Debug build had zero errors and 14 known warnings, and both runs left zero
crash-report or test-process residue. Final local closure also passed Contracts
357/357 (TRX SHA-256
`184106CA8DAC97CC206D6D9C292E2AF83CA812610566F3D92633034B37489D21`),
the focused packaged N-001 journey 1/1 (TRX SHA-256
`19257734B69EFDA98AC84FCAFB5C18E76C907ECB0B25B9800C24FFFA10D20422`),
and Release isolation. Hosted exact-head evidence remains pending.

## GGUF fixtures

The controlled fixture set contains 53 tiny `.gguf` binaries:

- 14 GGUF/header/native-probe inputs under
  [GGUF](TestFixtures/GGUF/README.md).
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

[build-and-test.yml](../.github/workflows/build-and-test.yml) uses a full
checkout so Release isolation can enumerate every tracked candidate file. It
regenerates fixtures with Windows PowerShell 5.1 and requires a clean fixture
tree, restores and builds the WinUI application, and runs two separate packaged
campaigns through the app-container VSTest runner. The serialized Debug/x64
campaign uses exactly `TestCategory=ModelInspectionFixtureGallery` and requires
the 220-test, nine-class map recorded above. The ordinary Release/x64 campaign
then uses exactly the two-category exclusion shown above and preserves its
686-test floor plus protected scanner, fixture-integrity, router,
worker-package, and Model Inspection class counts. Release isolation follows
the Release campaign. The current protected map contains 31 classes and 497
executions, including 39 navigation executions; the permanent minimum remains
686. Raw Debug, packaged-unit, and Gate 2 TRX files remain runner-ephemeral and
are not uploaded.

[model-inspection-visual-regression.yml](../.github/workflows/model-inspection-visual-regression.yml)
is a manual, fail-closed preflight only. It describes separate Light 96 DPI,
actual High Contrast, and actual 200% text-scale environments, but it cannot
produce or upload controlled evidence until the exact 13 Figma node exports,
approved runner pins, strict test classes, exact 13-row execution, and a
privacy-safe result schema/scanner/upload closure exist. Missing self-hosted
runner labels can prevent scheduling before the preflight is reached.

See the [17-step Visual Studio Debug guide](../docs/development/Model-Inspection-Visual-Studio-Debug-Guide.md)
and the [Task 12 visual verification record](../docs/evidence/testing/Model-Inspection-Figma-Visual-Verification.md)
for the reproducible local route and precise open evidence boundaries.

## Reserved test layers

The repository now has executable Contract and Integration projects in
addition to the packaged application tests. `E2ETests` remains reserved for
the complete user journey. Place future tests according to these boundaries:

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
