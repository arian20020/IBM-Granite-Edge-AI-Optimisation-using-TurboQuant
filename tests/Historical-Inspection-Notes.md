# Historical inspection test notes

## Current starting point

Use [CI test scope](../docs/testing/CI-Test-Scope.md) for the current workflow filter and known gaps, and [application evidence](../docs/testing/application-verification/README.md) for the recorded results. Hardware inspection is implemented. Three real-model inspection journeys passed with no skips; see [their instructions](E2ETests/GraniteEdgeAI.EndToEndTests/Journeys/InspectionRouteJourneys.md).

For setup and the distinction between compilation and a verified runtime package, read the [developer guide](../docs/manuals/Developer-Manual.md) and [build guide](../docs/manuals/Build-and-Installation.md). Do not use an old test count or excluded case as current acceptance evidence.

## Historical feature snapshots and commands

The remainder of this file preserves earlier scanner and inspection campaign notes. Statements such as “not implemented”, old count floors and “reserved” E2E layers describe those earlier snapshots, not the current app. Use the current links above and the checked-in workflow for today's commands and scope.

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

## Model Inspection progress-polish contract

The current implementation displays `Starting secure inspection…`
immediately while mandatory manifest verification and worker launch continue.
Its five public stages surround real snapshot/hash, native
configuration/`VocabOnly` load, tokenizer/chat smoke, structure/disposal/final
integrity, and runtime-evidence validation operations. Normal-motion
presentation gives each genuinely reached Active stage a 550 ms minimum from
its presented acknowledgement; safety, cancellation, recovery, navigation,
and reduced-motion paths remain immediate. No runtime or worker delay and no
fabricated fraction are permitted.

Packaged tests protect one unchanged 1,050 ms Precision Orbit across stages,
fraction-only trailing text, shared centred vector glyphs, 24/16 px page/card
rhythm, five 48 px default-scale progress rows, Balanced Centre terminal
geometry, natural warning/failure layouts, and responsive 1/2/3-action
arrangements. The Debug gallery catalogue is exactly 50 fixtures,
`MI-001` through `MI-050`, including the secure-start fixture.

Hardware Inspection is not implemented in this branch. The fixture presets and
ordinary packaged runs do not constitute strict Figma-pixel, real Narrator, or
controlled-OS evidence.

### Final progress-polish evidence

One serialized local RuntimeWorker -> Debug -> Release ladder passed on
2026-08-16 against `27934be2687418d7890b677cfcdabf22f059633d`. Its
git-ignored local run root is
`TestResults/ModelInspectionPolish/Final-20260816T131132549Z`:

| Receipt | Total/executed/passed | Definitions/results | TRX SHA-256 |
|---|---:|---:|---|
| Runtime | 189/189/189 | 189/189 | `C5916E795839D9C3DB2A2BA810C1B71E8C2B2363E4D3305A4D80C14C96CB643A` |
| Worker | 77/77/77 | 77/77 | `2BE31A252D2063716053D545C29FF582DE10BE5618C349CABE414D183703398B` |
| Debug Interaction/Lifetime | 19/19/19 | 19/19 | `2A1516B05DE81D2638AE219CFD5D7CAD660D365E689887DAD8A7752453C30695` |
| Debug fixture category | 220/220/220 | 220/220 | `17BC168A33C4C22D21F22042226FEBA827F9F7518243E09C1CF93D9013B1F1F9` |
| Debug focused polish | 322/322/322 | 322/322 | `B99360523E044578DCA7F5A420E60BBC33589409FCAE5D13D8362C5B563DF029` |
| Hosted-equivalent Release | 717/717/717 | 717/717 | `964371FA518BC6E2EF0F8061E38E95BE6582FB4890200F7507C6D2F7B88049CB` |
| Packaged N-001 | 1/1/1 | 1/1 | `F104764E57E2572E281FE32622E5E7920E2D32EE9931AD4D70D5219EA895BA8F` |

Failed, error, timeout, aborted, inconclusive, not-executed, not-runnable,
disconnected, and warning counters were zero for every TRX. Runtime used its
exact 22-class map; Worker used 67 engine plus 10 host executions; the Debug
fixture map remained 41/12/73/6/13/7/25/37/6; focused polish used its exact
22-class 322 map; and Release retained the exact 31-class, 497-execution
protected map including 39 navigation executions.

The Debug and Release packaged-recipe SHA-256 values were
`DFDE99F0D0B56200B3E67B2209816C42F634461EFCE4F2619E6CE03D284D8CA8`
and `5D08ADBF554AE4C57F8AB7FA84D806DCDA3377A33A03FC3095D71FA697E7DC62`.
All three phase source-freeze files were byte-identical before/after and shared
content SHA-256
`6BAA55091075151D3211934651D41FAA7B653A75F15BE71DB22DF5379789C095`;
each phase recorded zero WER delta and zero relevant processes before/after.
Release isolation passed at the same source commit with 112 scanned files,
zero forbidden path/token/metadata hits, and external evidence SHA-256
`2B2907FFADA8B5D13D96256FF07C1F366FD7E7EEB0D84FCC8CD6A21D20FEB1B7`.
Its source snapshot, MSIX, ReadyToRun main DLL, `resources.pri`, and exact Debug
identity SHA-256 values were respectively
`c1b5e1228d94abe82bbbd127e8e10c699de471c6d66f6fa0af900dfd2e0614b0`,
`f43e13dee79b199cbf17824971886752fa5ddeeb1d37a2c4779bc3d8356e65a6`,
`91757d2135acd343134fab8e65a1df7271ddb280dc943339ccdbd442f5fa6f24`,
`256af85337a1c327cb5c25891e946d9d2075bf7a5d8a4b8a6cac7b92483183fd`,
and `af1c2c19d5d15ee21521acdd95179cd12621efb60c818e09679b657a6fd30601`.

Raw identity-bearing TRX/logs remain local, untracked, and
do-not-stage/upload. This local gate is not strict Figma-pixel,
controlled-OS/manual Narrator, manual visual, or hosted exact-head evidence.

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

Historical pre-progress-polish evidence: that floor was observed in a local
packaged candidate based on commit
`5f90a5d9299363214f11454f548ff8571d98b1a5`: total 686, executed 686, passed
686, and zero non-passing results. It includes scanner/import coverage, Model
Inspection contracts, presentation/view/render/motion/control/accessibility
coverage, onboarding/navigation, and application-root/worker-package
composition. The composition suite includes a real package-identity-root
launch through the production client and controlled five-stage CPU/VocabOnly
completion. The package test requires the detached manifest-verified 44-file
CPU worker closure under `ModelInspection/Worker` and rejects
LLamaSharp/native files at the application root.

The historical Task 10 local hosted-equivalent Release/x64 candidate retained that
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

Historical Task 10 fixture evidence: the local Debug/x64 campaign on
2026-08-13 passed 220/220 with the exact
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
