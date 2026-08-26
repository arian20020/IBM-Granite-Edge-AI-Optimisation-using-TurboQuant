[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Filter,
    [string]$EvidenceDirectory,
    [string]$BuildExecutable,
    [string]$BuildArguments,
    [string]$TestRunnerExecutable,
    [string]$TestRunnerPrefixArguments,
    [string]$RecipePath,
    [string]$RepositoryRoot
)

$ErrorActionPreference = 'Stop'
if($Filter -match '["\r\n\x00]'){throw 'Filter contains an unsupported command-line character.'}
$installer = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer'
$env:PATH = $installer + [IO.Path]::PathSeparator + $env:PATH
$checkpoint = [DateTime]::UtcNow

if (-not $RepositoryRoot) { $RepositoryRoot = (git rev-parse --show-toplevel) }
$RepositoryRoot = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $RepositoryRoot).Path)
if (-not $EvidenceDirectory) { $EvidenceDirectory = Join-Path ([IO.Path]::GetTempPath()) ('geai-packaged-checkpoint-' + [guid]::NewGuid().ToString('N')) }
[IO.Directory]::CreateDirectory($EvidenceDirectory) | Out-Null

$vswhere = Join-Path $installer 'vswhere.exe'
if (-not $BuildExecutable) {
    $BuildExecutable = (& $vswhere -latest -products Microsoft.VisualStudio.Product.Community -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1)
    if (-not $BuildExecutable) { throw 'MSBuild was not found.' }
    $project = Join-Path $RepositoryRoot 'tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj'
    $BuildArguments = '"{0}" /restore /target:Build /maxCpuCount:1 /verbosity:minimal /property:Configuration=Debug /property:Platform=x64 /property:RuntimeIdentifier=win-x64 /property:OpenVinoConverterPackagingRequired=false /property:OpenVinoOfficialWorkerPackagingRequired=false /property:OpenVinoTurboQuantPackagingRequired=false /property:GgufQuantizerPackagingRequired=false /property:GenerateAppxPackageOnBuild=false' -f $project
}
if (-not $TestRunnerExecutable) {
    $TestRunnerExecutable = (& $vswhere -latest -products Microsoft.VisualStudio.Product.Community -find '**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' | Select-Object -First 1)
    if (-not $TestRunnerExecutable) { throw 'VS Community VSTest was not found.' }
}
if (-not $RecipePath) { $RecipePath = Join-Path $RepositoryRoot 'tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe' }

$build = Start-Process -FilePath $BuildExecutable -ArgumentList $BuildArguments -WorkingDirectory $RepositoryRoot -Wait -PassThru -NoNewWindow
if ($build.ExitCode) { throw "Packaged UnitTests build failed with exit code $($build.ExitCode)." }
if (-not (Test-Path -LiteralPath $RecipePath)) { throw 'The packaged UnitTests recipe was not produced.' }
$recipe = Get-Item -LiteralPath $RecipePath
if ($recipe.LastWriteTimeUtc -le $checkpoint) { throw 'The packaged UnitTests recipe is stale.' }

$runStart = [DateTime]::UtcNow
$trxName = 'packaged-checkpoint-{0}.trx' -f [guid]::NewGuid().ToString('N')
$runnerArguments = @($TestRunnerPrefixArguments, ('"{0}"' -f $recipe.FullName), '/Platform:x64', ('/Logger:trx;LogFileName={0}' -f $trxName), ('/ResultsDirectory:"{0}"' -f $EvidenceDirectory), ('/TestCaseFilter:"{0}"' -f $Filter)) -join ' '
$runner = Start-Process -FilePath $TestRunnerExecutable -ArgumentList $runnerArguments -WorkingDirectory $RepositoryRoot -Wait -PassThru -NoNewWindow
$trxPath = Join-Path $EvidenceDirectory $trxName
if (-not (Test-Path -LiteralPath $trxPath)) {
    $trxPath = Get-ChildItem -LiteralPath $EvidenceDirectory -Filter '*.trx' | Where-Object LastWriteTimeUtc -ge $runStart | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $trxPath) { throw 'The packaged test runner did not produce a fresh TRX.' }
[xml]$trx = Get-Content -Raw -LiteralPath $trxPath
$counters = $trx.TestRun.ResultSummary.Counters
$discovered = [int]$counters.total; $failed = [int]$counters.failed
Write-Output "Packaged checkpoint: discovered=$discovered failed=$failed (source/component evidence only; not Release/native/package evidence)."
if ($discovered -le 0) { throw 'The packaged test filter discovered zero tests.' }
if ($failed -ne 0 -or $runner.ExitCode -ne 0) { throw "Packaged tests failed: discovered=$discovered failed=$failed runnerExit=$($runner.ExitCode)." }

$head = (git -C $RepositoryRoot rev-parse HEAD)
$evidence = [ordered]@{ label='source/component evidence only'; head=$head; filter=$Filter; discovered=$discovered; failed=$failed; recipeSha256=(Get-FileHash -LiteralPath $recipe.FullName -Algorithm SHA256).Hash.ToLowerInvariant(); checkpointUtc=$checkpoint.ToString('o') }
$evidence | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'checkpoint.json') -Encoding UTF8
