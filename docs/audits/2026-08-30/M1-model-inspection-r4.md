# M1 Model Inspection R4.1 correction

Date: 2026-08-30
Worker: M1
Branch: `audit/ucl-m1-model-inspection-remediation-r4-1`
Required R4 starting commit: `dc0d6b01f0cd9f0caf68cd1dc569f94c8a112d4d`
Required R4 starting tree: `422282085ff2dea660f0ea0dea72348d33e7a395`
Immutable R4.1 verification subject: `ca913c68eb62fdd70f1c408fd74e7b9afb66a546`
Immutable R4.1 verification tree: `f83793dbea0a6a5f3a8e8ca343526eff9a947b9b`
Original campaign base: `282a7690edd9bfbb48dbb324d09e76a7a154652e` / `812c22ea640633c5e8835266b902fb802728eea9`
Frozen campaign source: `4748fe04f19afdf6b27c4c12502b84db325e7294` / `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91`

## Outcome and correction

This is the durable M1 R4.1 correction to the completed R4 Model Inspection
work. The R4.1 verification subject is a direct child of the required R4 tip.
It changes only the cleanup source list and cleanup inventory; no production or
test file changed.

Two evidence defects were reproduced at the pinned R4 tip:

1. The published R4 report and evidence manifest had entered the dynamic Model
   Inspection cleanup scope after the earlier 705-path subject was frozen, but
   neither path was registered. A direct Microsoft.Testing.Platform cleanup run
   therefore failed one of three cleanup tests, named both missing paths, and
   required a dynamic count of 707. The full contracts suite consequently had
   two failures at the R4 tip: the cleanup omission and the separately owned C0
   fixture-composition row.
2. The R4 manifest contained command IDs and totals but no literal commands.
   That was not reproducible for Microsoft.Testing.Platform projects, where an
   ordinary `dotnet test` command is not authoritative and can discover zero
   tests.

The immutable R4.1 subject adds each missing publication path exactly once to
`docs/reviews/model-inspection-cleanup-source-files.txt`, adds one complete
matching ledger row per path to
`docs/reviews/model-inspection-cleanup-inventory.md`, and updates only
current-scope narratives and self-ledger rows to the dynamically proved
707/707 result. The earlier 705/705 value remains only where explicitly labeled
as historical evidence for the earlier R4 implementation subject. Tests were
not weakened or filtered away.

The corrected cleanup source list and ledger are ordinal-sorted, unique,
repository-relative, present on disk, and one-to-one: 707 source paths and 707
ledger paths, with zero dynamic omissions. The focused cleanup group passes
3/3 at the immutable subject and again at the final documentation tip.

## Preserved implementation boundary

- `ModelInspectionHandoffV2` remains the single public six-field schema-v2
  authority. It owns validation, canonical serialization, strict parsing, and
  the 512-byte bound. No optimization, hardware, path, filename, prompt,
  identity, provider-response, free-form metadata, or model-byte field enters
  the handoff.
- GGUF retains route-specific inspection and custody through the live
  `ModelInspectionHandoffRegistry`; claim, rollback, reissue, invalidation,
  exact-identity, stale-evidence, and no-reuse behavior remain covered.
- OpenVINO retains typed ready, warning, conversion-required, incomplete,
  unsupported, dependency-unavailable, cancelled, timed-out, invalid-evidence,
  and stale-evidence outcomes. Projection validation still precedes consumption
  of the path-bearing lease.
- The conversion pipeline retains cancellation and timeout during validation
  and reinspection. Conversion-required source intent remains a one-time offer
  and never becomes inspected-model success.
- The evaluated MSBuild closure retains deterministic explicit/current/SDK-root/
  standard-install/approved-fallback host resolution without a PATH fallback.

## Exact-subject managed verification

The ten test commands below are non-overlapping and are the only rows included
in receipt arithmetic. Here, `executed` is the receipt invariant
`passed + failed + skipped`; the OpenVINO TRX runner's separate internal
"executed" counter excludes skipped rows, so the receipt records all 480
discovered terminal outcomes as 480 executed outcomes.

| Command ID | Discovered | Executed | Passed | Failed | Skipped | Exit | Disposition |
|---|---:|---:|---:|---:|---:|---:|---|
| `MODEL-INSPECTION-CONTRACTS` | 428 | 428 | 427 | 1 | 0 | 2 | Mixed: one C0-owned assertion evaluated 21 DebugFixtures items |
| `MODEL-INSPECTION-TRANSPORT` | 27 | 27 | 27 | 0 | 0 | 0 | Passed |
| `MODEL-INSPECTION-WORKER` | 78 | 78 | 78 | 0 | 0 | 0 | Passed |
| `MODEL-INSPECTION-WORKER-CLIENT` | 119 | 119 | 119 | 0 | 0 | 0 | Passed |
| `MODEL-INSPECTION-WORKER-PROCESS` | 8 | 8 | 8 | 0 | 0 | 0 | Passed structural/package slice |
| `MODEL-HARDWARE-COMPATIBILITY` | 1,050 | 1,050 | 1,050 | 0 | 0 | 0 | Passed |
| `HARDWARE-FOUNDATION-CONSUMERS` | 202 | 202 | 202 | 0 | 0 | 0 | Passed |
| `LLAMASHARP-DETERMINISTIC` | 191 | 191 | 191 | 0 | 0 | 0 | Passed |
| `GGUF-LIVE-HANDOFF-PROJECTION` | 2 | 2 | 2 | 0 | 0 | 0 | Passed |
| `OPENVINO-MANAGED-ROUTE` | 480 | 480 | 473 | 0 | 7 | 0 | Mixed: seven approved native stage roots absent |
| **Aggregate** | **2,585** | **2,585** | **2,577** | **1** | **7** |  | **Mixed** |

Exact arithmetic: **2,585 executed = 2,577 passed + 1 failed + 7 skipped**.
Discovery equals receipt execution for every row and in aggregate.

Additional overlapping or focused checks are excluded from that sum:

- cleanup inventory 3/3; workflow command contracts 18/18; canonical handoff
  schema 28/28; OpenVINO typed/route focus 96/96; OpenVINO contracts 202/202;
  packaged handoff claim/rollback/reissue focus 19/19; strongest production
  worker package gate 1/1;
- source-list/ledger reconciliation, privacy and forbidden-field scans,
  duplicate public type/schema/registration scans, `git diff --check`, and the
  C0 proposal apply check all passed;
- no focused result is added to the ten-command receipt arithmetic.

## Exact reproducible verification commands

All commands below were run with .NET SDK `10.0.301`. Each block is directly
copyable in PowerShell from any checkout: its first line resolves and selects
the repository root, and its build command names exactly one project using the
documented positional project argument. Microsoft.Testing.Platform test
applications are then executed directly; no ordinary `dotnet test` result is
used. The runner's `--help` output established the shown filter, TRX, results,
minimum-count, ANSI, progress, and output options. `$resultsDirectory` is a
deterministic subdirectory of the current machine's temporary directory.

### `MODEL-INSPECTION-CONTRACTS`

Working directory: repository root. Exact executable/TFM:
`tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/bin/x64/Release/net8.0/GraniteEdgeAI.ModelInspection.Contracts.Tests.exe` / `net8.0`.
Filter: none. Kind: managed contract. Environment block: none.

```powershell
Set-Location (git rev-parse --show-toplevel)
$resultsDirectory = Join-Path ([IO.Path]::GetTempPath()) 'R41-M1-results/subject'
dotnet build 'tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj' --configuration Release --property:Platform=x64 --nologo --verbosity minimal
$testExe = Join-Path (Get-Location) 'tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/bin/x64/Release/net8.0/GraniteEdgeAI.ModelInspection.Contracts.Tests.exe'
& $testExe --minimum-expected-tests 428 --report-trx --report-trx-filename 'MODEL-INSPECTION-CONTRACTS.trx' --results-directory $resultsDirectory --no-ansi --progress off --output Detailed
```

UTC: 2026-08-30T15:19:36.8381020Z to 2026-08-30T15:23:17.4951682Z.
Exit 2; 428/428/427/1/0 (discovered/executed/passed/failed/skipped);
mixed. Retained TRX SHA-256:
`952a4f62efab556bc04e7c13f65d1261cd7a34f945be48320193c3c50f183a9b`.

### `MODEL-INSPECTION-TRANSPORT`

Working directory: repository root. Exact executable/TFM:
`tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests/bin/x64/Release/net8.0/GraniteEdgeAI.ModelInspection.Transport.Tests.exe` / `net8.0`.
Filter: none. Kind: managed unit. Environment block: none.

```powershell
Set-Location (git rev-parse --show-toplevel)
$resultsDirectory = Join-Path ([IO.Path]::GetTempPath()) 'R41-M1-results/subject'
dotnet build 'tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests/GraniteEdgeAI.ModelInspection.Transport.Tests.csproj' --configuration Release --property:Platform=x64 --nologo --verbosity minimal
$testExe = Join-Path (Get-Location) 'tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests/bin/x64/Release/net8.0/GraniteEdgeAI.ModelInspection.Transport.Tests.exe'
& $testExe --minimum-expected-tests 27 --report-trx --report-trx-filename 'MODEL-INSPECTION-TRANSPORT.trx' --results-directory $resultsDirectory --no-ansi --progress off --output Normal
```

UTC: 2026-08-30T15:15:13.6922803Z to 2026-08-30T15:15:22.5289084Z.
Exit 0; 27/27/27/0/0; passed. Retained TRX SHA-256:
`6414d314fe127a80e4077245f86c4673d48fda5c6e118388e391573026454258`.

### `MODEL-INSPECTION-WORKER`

Working directory: repository root. Exact executable/TFM:
`tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/GraniteEdgeAI.ModelInspection.Worker.Tests.exe` / `net8.0-windows10.0.19041.0`.
Filter: none. Kind: managed unit. Environment block: none.

```powershell
Set-Location (git rev-parse --show-toplevel)
$resultsDirectory = Join-Path ([IO.Path]::GetTempPath()) 'R41-M1-results/subject'
dotnet build 'tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/GraniteEdgeAI.ModelInspection.Worker.Tests.csproj' --configuration Release --property:Platform=x64 --nologo --verbosity minimal
$testExe = Join-Path (Get-Location) 'tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/GraniteEdgeAI.ModelInspection.Worker.Tests.exe'
& $testExe --minimum-expected-tests 78 --report-trx --report-trx-filename 'MODEL-INSPECTION-WORKER.trx' --results-directory $resultsDirectory --no-ansi --progress off --output Normal
```

UTC: 2026-08-30T15:15:59.7680110Z to 2026-08-30T15:16:10.1134145Z.
Exit 0; 78/78/78/0/0; passed. Retained TRX SHA-256:
`06efec396c9a0cb61ca058883a8f4f3a2726680b4890a5b220ba120dc20fe016`.

### `MODEL-INSPECTION-WORKER-CLIENT`

Working directory: repository root. Exact executable/TFM:
`tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/GraniteEdgeAI.ModelInspection.WorkerClient.Tests.exe` / `net8.0-windows10.0.19041.0`.
Filter: none. Kind: managed process-backed unit. Environment block: none; the
fresh subject run permitted its generated fixture without weakening policy.

```powershell
Set-Location (git rev-parse --show-toplevel)
$resultsDirectory = Join-Path ([IO.Path]::GetTempPath()) 'R41-M1-results/subject'
dotnet build 'tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests.csproj' --configuration Release --property:Platform=x64 --nologo --verbosity minimal
$testExe = Join-Path (Get-Location) 'tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/GraniteEdgeAI.ModelInspection.WorkerClient.Tests.exe'
& $testExe --minimum-expected-tests 119 --report-trx --report-trx-filename 'MODEL-INSPECTION-WORKER-CLIENT.trx' --results-directory $resultsDirectory --no-ansi --progress off --output Normal
```

UTC: 2026-08-30T15:16:22.1377749Z to 2026-08-30T15:16:43.3582950Z.
Exit 0; 119/119/119/0/0; passed. Retained TRX SHA-256:
`7f09fff8dc8f7573738a92f98d793551a11259f3ae6421bb67b000a785155da0`.

### `MODEL-INSPECTION-WORKER-PROCESS`

Working directory: repository root. Exact executable/TFM:
`tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.exe` / `net8.0-windows10.0.19041.0`.
Filter: `FullyQualifiedName~TestFixtureIsolationTests|FullyQualifiedName~TestWorkerScenarioParserTests`.
Kind: structural/package slice. Environment block: none.

```powershell
Set-Location (git rev-parse --show-toplevel)
$resultsDirectory = Join-Path ([IO.Path]::GetTempPath()) 'R41-M1-results/subject'
dotnet build 'tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.csproj' --configuration Release --property:Platform=x64 --nologo --verbosity minimal
$testExe = Join-Path (Get-Location) 'tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.exe'
& $testExe --filter 'FullyQualifiedName~TestFixtureIsolationTests|FullyQualifiedName~TestWorkerScenarioParserTests' --minimum-expected-tests 8 --report-trx --report-trx-filename 'MODEL-INSPECTION-WORKER-PROCESS.trx' --results-directory $resultsDirectory --no-ansi --progress off --output Normal
```

UTC: 2026-08-30T15:17:24.9611943Z to 2026-08-30T15:17:35.6275572Z.
Exit 0; 8/8/8/0/0; passed. Retained TRX SHA-256:
`ee1c1b6003d4bb94af0adf31e2e857ef0d85ae34a2b33418cac641d7bcf52fed`.

### `MODEL-HARDWARE-COMPATIBILITY`

Working directory: repository root. Exact executable/TFM:
`tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/bin/x64/Release/net8.0/GraniteEdgeAI.ModelHardwareCompatibility.Tests.exe` / `net8.0`.
Filter: none. Kind: managed unit. Environment block: none.

```powershell
Set-Location (git rev-parse --show-toplevel)
$resultsDirectory = Join-Path ([IO.Path]::GetTempPath()) 'R41-M1-results/subject'
dotnet build 'tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/GraniteEdgeAI.ModelHardwareCompatibility.Tests.csproj' --configuration Release --property:Platform=x64 --nologo --verbosity minimal
$testExe = Join-Path (Get-Location) 'tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/bin/x64/Release/net8.0/GraniteEdgeAI.ModelHardwareCompatibility.Tests.exe'
& $testExe --minimum-expected-tests 1050 --report-trx --report-trx-filename 'MODEL-HARDWARE-COMPATIBILITY.trx' --results-directory $resultsDirectory --no-ansi --progress off --output Normal
```

UTC: 2026-08-30T15:17:47.5493797Z to 2026-08-30T15:17:57.5008086Z.
Exit 0; 1,050/1,050/1,050/0/0; passed. Retained TRX SHA-256:
`23a77224dcaaf65586999b0ecf8594693708bacf15022ecb3f542ff6340e5e0d`.

### `HARDWARE-FOUNDATION-CONSUMERS`

Working directory: repository root. Exact executable/TFM:
`tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/GraniteEdgeAI.HardwareInspection.Foundation.Tests.exe` / `net8.0-windows10.0.19041.0`.
Filter: none. Kind: managed consumer unit. Environment block: none.

```powershell
Set-Location (git rev-parse --show-toplevel)
$resultsDirectory = Join-Path ([IO.Path]::GetTempPath()) 'R41-M1-results/subject'
dotnet build 'tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/GraniteEdgeAI.HardwareInspection.Foundation.Tests.csproj' --configuration Release --property:Platform=x64 --nologo --verbosity minimal
$testExe = Join-Path (Get-Location) 'tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/GraniteEdgeAI.HardwareInspection.Foundation.Tests.exe'
& $testExe --minimum-expected-tests 202 --report-trx --report-trx-filename 'HARDWARE-FOUNDATION-CONSUMERS.trx' --results-directory $resultsDirectory --no-ansi --progress off --output Normal
```

UTC: 2026-08-30T15:18:10.2671699Z to 2026-08-30T15:18:20.5833046Z.
Exit 0; 202/202/202/0/0; passed. Retained TRX SHA-256:
`24091fcc895dea852ec017cabc8663c9841cef41197a97e5364fcc8721d9b2ba`.

### `LLAMASHARP-DETERMINISTIC`

Working directory: repository root. Exact executable/TFM:
`tools/ModelInspection.LlamaSharpSpike.Tests/bin/x64/Release/net8.0/ModelInspection.LlamaSharpSpike.Tests.exe` / `net8.0`.
Filter: none. Kind: managed deterministic unit. Environment block: none.

```powershell
Set-Location (git rev-parse --show-toplevel)
$resultsDirectory = Join-Path ([IO.Path]::GetTempPath()) 'R41-M1-results/subject'
dotnet build 'tools/ModelInspection.LlamaSharpSpike.Tests/ModelInspection.LlamaSharpSpike.Tests.csproj' --configuration Release --property:Platform=x64 --nologo --verbosity minimal
$testExe = Join-Path (Get-Location) 'tools/ModelInspection.LlamaSharpSpike.Tests/bin/x64/Release/net8.0/ModelInspection.LlamaSharpSpike.Tests.exe'
& $testExe --minimum-expected-tests 191 --report-trx --report-trx-filename 'LLAMASHARP-DETERMINISTIC.trx' --results-directory $resultsDirectory --no-ansi --progress off --output Normal
```

UTC: 2026-08-30T15:18:31.7891074Z to 2026-08-30T15:19:03.2430935Z.
Exit 0; 191/191/191/0/0; passed. Retained TRX SHA-256:
`c2a066715f9f03fd85cd3e1804395f91a2b5ba26155d68967f10705210c35dff`.

### `GGUF-LIVE-HANDOFF-PROJECTION`

Working directory: repository root. Exact executable/TFM:
`tests/UnitTests/GraniteEdgeAI.ModelInspection.Handoff.Tests/bin/x64/Release/net8.0/GraniteEdgeAI.ModelInspection.Handoff.Tests.exe` / `net8.0`.
Filter: `FullyQualifiedName~ModelInspectionHandoffProjectionTests`. Kind:
managed focused unit. Environment block: none.

```powershell
Set-Location (git rev-parse --show-toplevel)
$resultsDirectory = Join-Path ([IO.Path]::GetTempPath()) 'R41-M1-results/subject'
dotnet build 'tests/UnitTests/GraniteEdgeAI.ModelInspection.Handoff.Tests/GraniteEdgeAI.ModelInspection.Handoff.Tests.csproj' --configuration Release --property:Platform=x64 --nologo --verbosity minimal
$testExe = Join-Path (Get-Location) 'tests/UnitTests/GraniteEdgeAI.ModelInspection.Handoff.Tests/bin/x64/Release/net8.0/GraniteEdgeAI.ModelInspection.Handoff.Tests.exe'
& $testExe --filter 'FullyQualifiedName~ModelInspectionHandoffProjectionTests' --minimum-expected-tests 2 --report-trx --report-trx-filename 'GGUF-LIVE-HANDOFF-PROJECTION.trx' --results-directory $resultsDirectory --no-ansi --progress off --output Normal
```

UTC: 2026-08-30T15:19:18.1860716Z to 2026-08-30T15:19:24.7393137Z.
Exit 0; 2/2/2/0/0; passed. Retained TRX SHA-256:
`f86050a96cf96c90ac06bc6446f0612e3cbb9e272a61f7791c0f555c3840700b`.

### `OPENVINO-MANAGED-ROUTE`

Working directory: repository root. Exact executable/TFM:
`tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/GraniteEdgeAI.OpenVino.Tests.exe` / `net8.0-windows10.0.19041.0`.
Filter: none. Kind: managed route unit with native-stage skips. Environment
block: approved converter/official-worker stage roots were not supplied.

```powershell
Set-Location (git rev-parse --show-toplevel)
$resultsDirectory = Join-Path ([IO.Path]::GetTempPath()) 'R41-M1-results/subject'
dotnet build 'tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/GraniteEdgeAI.OpenVino.Tests.csproj' --configuration Release --property:Platform=x64 --nologo --verbosity minimal
$testExe = Join-Path (Get-Location) 'tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/GraniteEdgeAI.OpenVino.Tests.exe'
& $testExe --minimum-expected-tests 480 --report-trx --report-trx-filename 'OPENVINO-MANAGED-ROUTE-UNIT.trx' --results-directory $resultsDirectory --no-ansi --progress off --output Normal
```

UTC: 2026-08-30T15:27:34.5678163Z to 2026-08-30T15:29:31.5551125Z.
Exit 0; 480/480/473/0/7; mixed. Retained TRX SHA-256:
`f9fb03801953617bdce65822f4ba118bbdc0f68cf8c78ca82225b8c9eafd8867`.

### `DEBUG-X64-APPLICATION-BUILD`

Working directory: repository root. Build-only command; executable/TFM and
test filter/logger are not applicable because packaging stopped before an
application executable was produced. Kind: packaged application build.
Environment prerequisite: Visual Studio MSBuild located with the installed
Visual Studio Installer query tool.

```powershell
Set-Location (git rev-parse --show-toplevel)
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$msbuild = & $vswhere -latest -products '*' -find 'MSBuild/**/Bin/MSBuild.exe' | Select-Object -First 1
& $msbuild 'IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj' /target:Restore,Build /property:Configuration=Debug /property:Platform=x64 /verbosity:minimal /nologo
```

UTC: 2026-08-30T15:30:21.9304087Z to 2026-08-30T15:31:47.4749581Z.
Exit 1; 0/0/0/0/0; blocked at
`OpenVino.WorkerPackaging.targets(17,5)` because
`OpenVinoOfficialWorkerStageDirectory` is required for x64 builds. No result
artifact was retained.

### `DEBUG-X64-PACKAGED-TEST-BUILD`

Working directory: repository root. Build-only command; test executable/TFM,
filter, and logger are not applicable. Kind: packaged test build. Environment
prerequisite: the Visual Studio Installer directory is prepended to the
process-only PATH so the existing NativeAOT target can resolve `vswhere.exe`.

```powershell
Set-Location (git rev-parse --show-toplevel)
$visualStudioInstaller = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer'
$env:PATH = $visualStudioInstaller + [IO.Path]::PathSeparator + $env:PATH
dotnet build 'tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj' --configuration Debug --runtime win-x64 --property:Platform=x64 --nologo --verbosity minimal
```

UTC: 2026-08-30T15:36:50.4688156Z to 2026-08-30T15:40:28.1095421Z.
Exit 0; 0/0/0/0/0; passed build with 27 pre-existing warnings and zero errors.
No result artifact was retained.

### `SOURCE-PRIVACY-INVENTORY-GATES`

Working directory: repository root. Build, executable/TFM, test filter, and
logger are not applicable. Kind: source/privacy/inventory scan. Environment
block: none.

```powershell
Set-Location (git rev-parse --show-toplevel)
$source = @(Get-Content 'docs/reviews/model-inspection-cleanup-source-files.txt' | Where-Object { $_ -and -not $_.StartsWith('#') })
$ledger = @(Select-String -Path 'docs/reviews/model-inspection-cleanup-inventory.md' -Pattern '^\| `([^`]+)` \|' | ForEach-Object { $_.Matches[0].Groups[1].Value })
if ($source.Count -ne 707 -or $ledger.Count -ne 707) { throw 'Cleanup cardinality mismatch.' }
if ((Compare-Object $source ($source | Sort-Object -CaseSensitive)) -or (Compare-Object $ledger ($ledger | Sort-Object -CaseSensitive))) { throw 'Cleanup ordering mismatch.' }
if (($source | Select-Object -Unique).Count -ne 707 -or ($ledger | Select-Object -Unique).Count -ne 707) { throw 'Cleanup uniqueness mismatch.' }
if (Compare-Object $source $ledger) { throw 'Cleanup one-to-one mismatch.' }
if (@($source | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) }).Count) { throw 'Cleanup path missing from disk.' }
$evidencePaths = @('docs/audits/2026-08-30/M1-model-inspection-r4.md','docs/audits/2026-08-30/evidence/M1-model-inspection-r4.json','docs/audits/2026-08-30/handoffs/R4-M1.json')
$evidenceText = ($evidencePaths | ForEach-Object { Get-Content -Raw -LiteralPath $_ }) -join "`n"
$separator = [IO.Path]::DirectorySeparatorChar
$drivePrefixPattern = '[A-Za-z]' + [regex]::Escape(([char]58).ToString() + $separator)
$userRootPattern = [regex]::Escape($separator + 'Users' + $separator)
$uncPattern = [regex]::Escape($separator + $separator)
if ($evidenceText -match $drivePrefixPattern -or $evidenceText -match $userRootPattern -or $evidenceText -match $uncPattern) { throw 'Private path detected.' }
foreach ($identity in @($env:USERNAME,$env:COMPUTERNAME)) { if ($identity -and $evidenceText -match ('(?i)(?<![\p{L}\p{N}])' + [regex]::Escape($identity) + '(?![\p{L}\p{N}])')) { throw 'Private identity detected.' } }
$handoffSource = Get-Content -Raw 'shared/GraniteEdgeAI.ModelInspection.Contracts/Evidence/ModelInspectionProjectionV2.cs'
foreach ($forbidden in @('modelPath','fileName','prompt','providerResponse','hardware','optimization')) { if ($handoffSource -cmatch $forbidden) { throw "Forbidden handoff field: $forbidden" } }
if (@(rg -l 'public (sealed )?(record|class) ModelInspectionHandoffV2' --glob '*.cs').Count -ne 1) { throw 'Duplicate public handoff authority.' }
git diff --check dc0d6b01f0cd9f0caf68cd1dc569f94c8a112d4d HEAD
if ($LASTEXITCODE -ne 0) { throw 'git diff --check failed.' }
```

UTC: 2026-08-30T16:02:47.7807531Z to 2026-08-30T16:02:50.8596840Z.
Exit 0; 0/0/0/0/0; passed. No result artifact is retained.

### `C0-FIXTURE-PROPOSAL-APPLY-CHECK`

Working directory: repository root. Build, executable/TFM, test filter, and
logger are not applicable. Kind: structural source check. Environment block:
none.

```powershell
Set-Location (git rev-parse --show-toplevel)
git apply --check 'docs/audits/2026-08-28/proposals/M1-R3-shared-project-fixture-boundary.diff'
```

UTC: 2026-08-30T16:02:55.4449019Z to 2026-08-30T16:02:55.5126245Z.
Exit 0; 0/0/0/0/0; passed. No result artifact is retained.

## Build, visual, and native disposition

The Debug x64 packaged-test build passes after process-local PATH preparation.
The strongest production worker package gate also passes 1/1. The Debug x64
application build nevertheless stops in its existing packaging target because
`OpenVinoOfficialWorkerStageDirectory` was not provided. It produces no exact
application executable. Therefore application launch, both-route file-13
smoke, 100%/200% scaling checks, screenshots, and visual defect acceptance are
blocked before activation. No fixture screenshot or executable from another
commit substitutes for that evidence.

`nativeDisposition` is `blocked`. This non-authoritative development machine
does not establish Intel-native GGUF/OpenVINO execution, signed-package or App
Control acceptance, installed-layout acceptance, performance, quality, or
end-to-end visual acceptance.

## Independent review disposition

An independent read-only reviewer inspected the exact R4-to-R4.1 subject diff,
the two reproduced findings, dynamic scope computation, source/ledger parity,
test integrity, privacy boundary, arithmetic, and supplied verification
outputs. It found no Critical, Important, or Minor defect in the immutable
R4.1 subject. Its Phase C publication gate required the literal commands and
fresh results now recorded here: 707/707 cleanup, WorkerClient 119/119, the
passing PATH-prepared packaged build, the application-stage blocker, one C0
failure involving 21 evaluated fixture items, and exact 2,585-result arithmetic.

## C0 integration note

1. Integrate immutable R4.1 subject
   `ca913c68eb62fdd70f1c408fd74e7b9afb66a546` after the required R4 tip, and
   retain both cleanup registrations and their 707/707 current-scope claims.
2. Preserve
   `shared/GraniteEdgeAI.ModelInspection.Contracts/Evidence/ModelInspectionProjectionV2.cs`
   as the sole schema-v2 authority. Do not restore the removed OpenVINO codec.
3. Preserve the live GGUF registry caller and the OpenVINO route service plus
   exact handoff-lease validation-before-consumption. Preserve typed OpenVINO
   inspection, presentation, activation, cancellation, timeout, and stale-
   evidence outcomes during conflict resolution.
4. Q1 must consume the six canonical fields at the later optimization seam; do
   not add optimization, hardware, export, or product-result fields to M1's
   handoff. Preserve both run/handoff IDs, SHA-256, and model length exactly.
5. The C0-owned full contracts failure evaluates 21 prohibited DebugFixtures
   items across production contexts. Apply or supersede the still-apply-
   checkable shared project fixture-boundary proposal, then rerun the evaluated
   closure and require zero prohibited fixture items.
6. Supply independently verified OpenVINO converter/official-worker stage roots
   and `OpenVinoOfficialWorkerStageDirectory`, rebuild the exact Debug x64 app,
   and perform file-13 visual/native gates on an authoritative Intel machine.

## Scope and nonclaims

No production, test, XAML, shell, project, solution, package-composition, Q1
optimization, hardware-fact, or F1 frontend file was changed by R4.1. No
machine policy, trust, signing, registry, firewall, or installation setting was
weakened. No raw TRX, model, prompt, local path, filename, user/machine identity,
provider output, credential, binary, or generated build output is committed as
evidence. No failed, skipped, blocked, build-only, structural, fixture, or
mock-only row is represented as native or user-journey success.
