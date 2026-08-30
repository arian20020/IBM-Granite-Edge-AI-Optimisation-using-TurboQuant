[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{40}$')]
    [string] $SubjectTree,

    [Parameter(Mandatory = $true)]
    [string] $OutputSummaryPath,

    [string] $DotNetHostPath,
    [string] $ResultsRoot,
    [string] $ControlledCatalogPath
)

$ErrorActionPreference = 'Stop'

function Stop-H1R3Managed {
    param([string] $Code)
    Write-Output $Code
    exit 2
}

function Test-H1R3Count {
    param($Value)
    return $Value -is [int] -or $Value -is [long]
}

function Assert-H1R3Row {
    param($Row)
    foreach ($name in @('discovered', 'executed', 'passed', 'failed', 'skipped', 'exitCode')) {
        $value = $Row.$name
        if (-not (Test-H1R3Count $value) -or [long]$value -lt 0) {
            Stop-H1R3Managed 'H1R3-ARITHMETIC'
        }
    }
    if ([long]$Row.executed -ne ([long]$Row.passed + [long]$Row.failed + [long]$Row.skipped)) {
        Stop-H1R3Managed 'H1R3-ARITHMETIC'
    }
    if ([long]$Row.discovered -lt [long]$Row.executed) {
        Stop-H1R3Managed 'H1R3-DISCOVERY'
    }
    if ([long]$Row.discovered -eq 0) {
        Stop-H1R3Managed 'H1R3-ZERO-DISCOVERY'
    }
    if (([long]$Row.failed -eq 0 -and [long]$Row.exitCode -ne 0) -or
        ([long]$Row.failed -gt 0 -and [long]$Row.exitCode -eq 0)) {
        Stop-H1R3Managed 'H1R3-ARITHMETIC'
    }
}

function Write-H1R3Summary {
    param([object[]] $Rows)
    foreach ($row in $Rows) { Assert-H1R3Row $row }
    $output = [ordered]@{
        schemaVersion = 1
        workerId = 'H1'
        campaign = 'R3'
        subjectTree = $SubjectTree
        commands = @($Rows)
    }
    $path = [IO.Path]::GetFullPath($OutputSummaryPath)
    if (Test-Path -LiteralPath $path) { Stop-H1R3Managed 'H1R3-DESTINATION-EXISTS' }
    $parent = Split-Path -Parent $path
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        New-Item -ItemType Directory -Path $parent | Out-Null
    }
    [IO.File]::WriteAllText(
        $path,
        (($output | ConvertTo-Json -Depth 8) + "`n"),
        [Text.UTF8Encoding]::new($false))
}

if (-not [string]::IsNullOrWhiteSpace($ControlledCatalogPath)) {
    $catalogFile = [IO.FileInfo] $ControlledCatalogPath
    if (-not $catalogFile.Exists -or $catalogFile.Length -le 0 -or $catalogFile.Length -gt 1048576) {
        Stop-H1R3Managed 'H1R3-JSON-BOUNDS'
    }
    try { $catalog = [IO.File]::ReadAllText($catalogFile.FullName) | ConvertFrom-Json }
    catch { Stop-H1R3Managed 'H1R3-JSON-INVALID' }
    if ($null -eq $catalog.commands -or @($catalog.commands).Count -eq 0) {
        Stop-H1R3Managed 'H1R3-ZERO-DISCOVERY'
    }
    Write-H1R3Summary @($catalog.commands)
    Write-Output 'H1R3-OK'
    exit 0
}

$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
if ([string]::IsNullOrWhiteSpace($ResultsRoot)) {
    Stop-H1R3Managed 'H1R3-RESULTS-ROOT'
}
$results = [IO.Path]::GetFullPath($ResultsRoot)
if (-not (Test-Path -LiteralPath $results -PathType Container)) {
    New-Item -ItemType Directory -Path $results | Out-Null
}
if ([string]::IsNullOrWhiteSpace($DotNetHostPath) -or
    -not (Test-Path -LiteralPath $DotNetHostPath -PathType Leaf)) {
    Stop-H1R3Managed 'H1R3-DOTNET-MISSING'
}

function New-H1R3Row {
    param(
        [string] $Id, [string] $InvocationId, [int] $ExitCode,
        [long] $Discovered, [long] $Executed, [long] $Passed,
        [long] $Failed, [long] $Skipped
    )
    return [pscustomobject][ordered]@{
        id = $Id; invocationId = $InvocationId; exitCode = $ExitCode
        discovered = $Discovered; executed = $Executed; passed = $Passed
        failed = $Failed; skipped = $Skipped
    }
}

function Invoke-H1R3Process {
    param([string] $FilePath, [string[]] $Arguments, [string] $OutputName)
    $outputPath = Join-Path $results $OutputName
    & $FilePath @Arguments *> $outputPath
    return $LASTEXITCODE
}

function Invoke-H1R3TestExecutable {
    param([string] $Id, [string] $InvocationId, [string] $RelativeExecutable, [string] $TrxName)
    $executable = Join-Path $root $RelativeExecutable
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
        return New-H1R3Row $Id $InvocationId 1 1 1 0 1 0
    }
    $trx = Join-Path $results $TrxName
    if (Test-Path -LiteralPath $trx) { Remove-Item -LiteralPath $trx -Force }
    $exitCode = Invoke-H1R3Process $executable @(
        '--report-trx', '--report-trx-filename', $trx,
        '--no-ansi', '--progress', 'off', '--zero-tests-policy', 'strict', '--timeout', '10m'
    ) ($TrxName + '.output.txt')
    if (-not (Test-Path -LiteralPath $trx -PathType Leaf)) {
        return New-H1R3Row $Id $InvocationId ([Math]::Max(1, $exitCode)) 1 1 0 1 0
    }
    try {
        [xml]$xml = [IO.File]::ReadAllText($trx)
        $counters = $xml.TestRun.ResultSummary.Counters
        $discovered = [long]$counters.total
        $executed = [long]$counters.executed
        $passed = [long]$counters.passed
        $failed = [long]$counters.failed + [long]$counters.error + [long]$counters.timeout + [long]$counters.aborted
        $skipped = $executed - $passed - $failed
        if ($skipped -lt 0) { Stop-H1R3Managed 'H1R3-ARITHMETIC' }
        $normalizedExit = if ($failed -gt 0) { [Math]::Max(1, $exitCode) } else { $exitCode }
        return New-H1R3Row $Id $InvocationId $normalizedExit $discovered $executed $passed $failed $skipped
    }
    catch { Stop-H1R3Managed 'H1R3-TRX' }
}

function Invoke-H1R3ScalarGate {
    param([string] $Id, [string] $InvocationId, [scriptblock] $Body)
    try { & $Body; $ok = $? }
    catch { $ok = $false }
    if ($ok) { return New-H1R3Row $Id $InvocationId 0 1 1 1 0 0 }
    return New-H1R3Row $Id $InvocationId 1 1 1 0 1 0
}

$rows = [Collections.Generic.List[object]]::new()
$rows.Add((Invoke-H1R3TestExecutable 'HARDWARE-FOUNDATION' 'foundation-tests-v1' `
            'tests\UnitTests\GraniteEdgeAI.HardwareInspection.Foundation.Tests\bin\Debug\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.HardwareInspection.Foundation.Tests.exe' 'foundation.trx'))
$rows.Add((Invoke-H1R3TestExecutable 'LLAMACPP-PROBE-TOOL' 'llamacpp-probe-tests-v1' `
            'tests\UnitTests\GraniteEdgeAI.HardwareInspection.LlamaCppProbe.Tests\bin\Debug\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.HardwareInspection.LlamaCppProbe.Tests.exe' 'probe.trx'))
$rows.Add((Invoke-H1R3TestExecutable 'MODEL-HARDWARE-COMPATIBILITY' 'compatibility-tests-v1' `
            'tests\UnitTests\GraniteEdgeAI.ModelHardwareCompatibility.Tests\bin\Debug\net8.0\GraniteEdgeAI.ModelHardwareCompatibility.Tests.exe' 'compatibility.trx'))

$pesterOutput = Join-Path $results 'h1-pester.txt'
$pesterCommand = @"
`$r = Invoke-Pester -Path @(
  'tests/PowerShell/HardwareInspectionPackaging.Tests.ps1',
  'tests/PowerShell/HardwareInspectionTrust.Tests.ps1',
  'tests/PowerShell/H1R3Closure.Tests.ps1'
) -PassThru
[pscustomobject]@{ TotalCount=`$r.TotalCount; PassedCount=`$r.PassedCount; FailedCount=`$r.FailedCount; SkippedCount=`$r.SkippedCount } | ConvertTo-Json -Compress
if (`$r.TotalCount -eq 0 -or `$r.FailedCount -ne 0) { exit 1 }
"@
$pesterRaw = & powershell.exe -NoProfile -ExecutionPolicy Bypass -Command $pesterCommand 2>&1
$pesterExit = $LASTEXITCODE
$pesterRaw | Set-Content -LiteralPath $pesterOutput -Encoding UTF8
$pesterJsonLine = @($pesterRaw | Where-Object { $_ -is [string] -and $_ -match '^\{"TotalCount"' }) | Select-Object -Last 1
if ($null -eq $pesterJsonLine) {
    $rows.Add((New-H1R3Row 'HARDWARE-POWERSHELL' 'h1-pester-v1' 1 1 1 0 1 0))
}
else {
    $pc = $pesterJsonLine | ConvertFrom-Json
    $rows.Add((New-H1R3Row 'HARDWARE-POWERSHELL' 'h1-pester-v1' $pesterExit `
                ([long]$pc.TotalCount) ([long]$pc.TotalCount) ([long]$pc.PassedCount) `
                ([long]$pc.FailedCount) ([long]$pc.SkippedCount)))
}

$appProject = Join-Path $root 'IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj'
$unitProject = Join-Path $root 'tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj'
$buildExit = Invoke-H1R3Process $DotNetHostPath @('build', $appProject, '--no-restore', '-c', 'Debug', '-m:1', '-p:Platform=x64', '-p:RuntimeIdentifier=win-x64', "-p:DotNetHostPath=$DotNetHostPath") 'app-build.txt'
$appBuildRow = if ($buildExit -eq 0) {
    New-H1R3Row 'DEBUG-X64-APP-BUILD' 'app-build-v1' 0 1 1 1 0 0
} else {
    New-H1R3Row 'DEBUG-X64-APP-BUILD' 'app-build-v1' $buildExit 1 1 0 1 0
}
$rows.Add($appBuildRow)
$unitBuildExit = Invoke-H1R3Process $DotNetHostPath @('build', $unitProject, '--no-restore', '-c', 'Debug', '-m:1', '-p:Platform=x64', '-p:RuntimeIdentifier=win-x64', "-p:DotNetHostPath=$DotNetHostPath") 'unit-build.txt'
$unitBuildRow = if ($unitBuildExit -eq 0) {
    New-H1R3Row 'DEBUG-X64-UNIT-BUILD' 'unit-build-v1' 0 1 1 1 0 0
} else {
    New-H1R3Row 'DEBUG-X64-UNIT-BUILD' 'unit-build-v1' $unitBuildExit 1 1 0 1 0
}
$rows.Add($unitBuildRow)

$staticPassed = 0
$staticConfigurations = @(
    @('Debug', 'x64', 'win-x64'), @('Release', 'x64', 'win-x64'),
    @('Debug', 'x86', 'win-x86'), @('Debug', 'AnyCPU', '')
)
$index = 0
foreach ($configuration in $staticConfigurations) {
    $arguments = @('msbuild', $appProject, '-nologo', '-getItem:Content', "-p:Configuration=$($configuration[0])", "-p:Platform=$($configuration[1])", '-p:DesignTimeBuild=true', '-p:HardwareInspectionLlamaCppProbeSkipPackaging=true')
    if ($configuration[2]) { $arguments += "-p:RuntimeIdentifier=$($configuration[2])" }
    $exitCode = Invoke-H1R3Process $DotNetHostPath $arguments ("static-{0}.json" -f $index)
    if ($exitCode -eq 0) { $staticPassed++ }
    $index++
}
$staticExit = if ($staticPassed -eq 4) { 0 } else { 1 }
$rows.Add((New-H1R3Row 'STATIC-PACKAGING-EVAL' 'static-package-matrix-v1' `
            $staticExit 4 4 $staticPassed (4 - $staticPassed) 0))

$manifestVerifier = Join-Path $root 'scripts\hardware-inspection\Test-LlamaCppProbeManifest.ps1'
$probeDirectory = Join-Path $root 'obj\hi-lcp\package\Debug\win-x64'
$probeManifest = Join-Path $root 'obj\hi-lcp\package\Debug\llamacpp-probe-manifest.json'
$rows.Add((Invoke-H1R3ScalarGate 'PACKAGE-MANIFEST-VERIFY' 'probe-manifest-v1' {
            & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $manifestVerifier `
                -ProbeDirectory $probeDirectory -ManifestPath $probeManifest -EmitVerifiedFilePaths `
                *> (Join-Path $results 'manifest-verify.txt')
            if ($LASTEXITCODE -ne 0) { throw 'manifest' }
        }))

$rows.Add((Invoke-H1R3ScalarGate 'PACKAGE-PRIVACY-DUPLICATION' 'package-privacy-v1' {
            $files = Get-ChildItem -LiteralPath $probeDirectory -File
            if ($files.Count -eq 0) { throw 'empty' }
            if (($files.Name | Group-Object | Where-Object Count -gt 1).Count -ne 0) { throw 'duplicate' }
            if (($files.FullName | Select-String -Pattern '(?i)(docs[\\/]+audits|debugfixtures|gallery|evidence|reference|\.pdb$)' -Quiet)) { throw 'private' }
        }))

$rows.Add((Invoke-H1R3ScalarGate 'PRODUCTION-REACHABILITY' 'production-reachability-v1' {
            $sourceRoots = @((Join-Path $root 'IBM Granite with TurboQuant (Intel)'), (Join-Path $root 'shared'))
            $files = Get-ChildItem -LiteralPath $sourceRoots -Recurse -File -Include '*.cs', '*.csproj'
            $text = $files | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }
            if (@($text | Select-String -Pattern 'HardwareSnapshotIdentityCanonicalizer\.Compute\(').Count -ne 1) { throw 'snapshot' }
            if (@($text | Select-String -Pattern 'SystemMemoryBudgetCalculator\.Calculate\(').Count -ne 1) { throw 'budget' }
            if (@($text | Select-String -Pattern 'HardwareInspectionComposition\.CreateProduction\(').Count -ne 1) { throw 'composition' }
            if (@($text | Select-String -Pattern 'Project="HardwareInspection\.LlamaCppProbePackaging\.targets"').Count -ne 1) { throw 'import' }
        }))

$ggufTarget = Join-Path $root 'IBM Granite with TurboQuant (Intel)\GgufRuntime.WorkerPackaging.targets'
$ggufText = [IO.File]::ReadAllText($ggufTarget)
if ($ggufText -match '@\(_GgufRuntimePublishedFiles\)') {
    $rows.Add((New-H1R3Row 'MODEL-INSPECTION-PACKAGE-BOUNDARY' 'model-package-boundary-v1' 1 1 1 0 1 0))
}
else {
    $rows.Add((New-H1R3Row 'MODEL-INSPECTION-PACKAGE-BOUNDARY' 'model-package-boundary-v1' 0 1 1 1 0 0))
}

$rows.Add((Invoke-H1R3ScalarGate 'PRIVACY-DIFF-SCANS' 'privacy-diff-v1' {
            & git -C $root diff --check
            if ($LASTEXITCODE -ne 0) { throw 'diff' }
            $changed = & git -C $root diff --name-only e000ee4f7b1ecc68cac79d774d47e659c96b661c...HEAD
            foreach ($relative in $changed) {
                $path = Join-Path $root $relative
                if (Test-Path -LiteralPath $path -PathType Leaf) {
                    $value = [IO.File]::ReadAllText($path)
                    if ($value -match '(?i)C:\\Users\\Student|\\Users\\Student|hostname\s*[:=]|username\s*[:=]') { throw 'privacy' }
                }
            }
        }))

Write-H1R3Summary $rows.ToArray()
if (($rows | Where-Object failed -gt 0).Count -gt 0) {
    Write-Output 'H1R3-MANAGED-FAILURES'
    exit 1
}
Write-Output 'H1R3-OK'
exit 0
