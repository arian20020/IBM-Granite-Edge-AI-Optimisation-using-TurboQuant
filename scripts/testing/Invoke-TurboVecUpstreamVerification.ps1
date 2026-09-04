[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$SourceRoot,
    [Parameter(Mandatory=$true)][string]$PythonExe,
    [Parameter(Mandatory=$true)][string]$OutputRoot
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ExpectedCommit = 'ccab9f325e6ce2a270a87daf01ae4e443bcf2d49'
$ExpectedRust = '1.89.0'

function Resolve-AbsoluteItem {
    param([string]$Path, [string]$Label, [switch]$Directory)
    if (-not [IO.Path]::IsPathFullyQualified($Path)) { throw "$Label must be absolute." }
    $resolved = (Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path
    $item = Get-Item -LiteralPath $resolved -Force
    if ($Directory -and -not $item.PSIsContainer) { throw "$Label must be a directory." }
    if (-not $Directory -and $item.PSIsContainer) { throw "$Label must be a file." }
    return $resolved
}

function Invoke-Logged {
    param(
        [string]$Name,
        [string]$FilePath,
        [string[]]$Arguments,
        [string]$WorkingDirectory
    )
    $stdout = Join-Path $Output "$Name.stdout.log"
    $stderr = Join-Path $Output "$Name.stderr.log"
    Push-Location -LiteralPath $WorkingDirectory
    try {
        $nativePreference = $PSNativeCommandUseErrorActionPreference
        $PSNativeCommandUseErrorActionPreference = $false
        & $FilePath @Arguments 1> $stdout 2> $stderr
        $exitCode = $LASTEXITCODE
        $PSNativeCommandUseErrorActionPreference = $nativePreference
    } finally {
        Pop-Location
    }
    return [ordered]@{
        name = $Name
        command = (@($FilePath) + $Arguments) -join ' '
        working_directory = $WorkingDirectory
        exit_code = $exitCode
        stdout = [ordered]@{path=(Split-Path -Leaf $stdout);bytes=(Get-Item -LiteralPath $stdout).Length;sha256=(Get-FileHash -LiteralPath $stdout -Algorithm SHA256).Hash.ToLowerInvariant()}
        stderr = [ordered]@{path=(Split-Path -Leaf $stderr);bytes=(Get-Item -LiteralPath $stderr).Length;sha256=(Get-FileHash -LiteralPath $stderr -Algorithm SHA256).Hash.ToLowerInvariant()}
    }
}

function New-Accounting {
    param([int]$Passed,[int]$Failed,[int]$Skipped=0,[int]$Blocked=0,[int]$Unexecuted=0)
    $executed = $Passed + $Failed
    $discovered = $executed + $Skipped + $Blocked + $Unexecuted
    $record = [ordered]@{
        discovered=$discovered
        attempted=$executed
        executed=$executed
        passed=$Passed
        failed=$Failed
        skipped=$Skipped
        blocked=$Blocked
        unexecuted=$Unexecuted
    }
    if ($record.discovered -ne ($record.executed + $record.skipped + $record.blocked + $record.unexecuted)) { throw 'Arithmetic mismatch: discovered.' }
    if ($record.executed -ne ($record.passed + $record.failed)) { throw 'Arithmetic mismatch: executed.' }
    return $record
}

function Read-CombinedLog {
    param($Command)
    return ((Get-Content -LiteralPath (Join-Path $Output $Command.stdout.path) -Raw) + "`n" +
        (Get-Content -LiteralPath (Join-Path $Output $Command.stderr.path) -Raw))
}

function Count-CargoTests {
    param([string]$Text)
    $passed=0; $failed=0; $skipped=0
    foreach ($match in [regex]::Matches($Text, 'test result: (?:ok|FAILED)\.\s+(\d+) passed;\s+(\d+) failed;\s+(\d+) ignored;')) {
        $passed += [int]$match.Groups[1].Value
        $failed += [int]$match.Groups[2].Value
        $skipped += [int]$match.Groups[3].Value
    }
    return New-Accounting -Passed $passed -Failed $failed -Skipped $skipped
}

$Source = Resolve-AbsoluteItem -Path $SourceRoot -Label 'Source root' -Directory
$Python = Resolve-AbsoluteItem -Path $PythonExe -Label 'Python executable'
if (-not [IO.Path]::IsPathFullyQualified($OutputRoot)) { throw 'Output root must be absolute.' }
$parent = Resolve-AbsoluteItem -Path (Split-Path -Parent $OutputRoot) -Label 'Output parent' -Directory
$Output = Join-Path $parent (Split-Path -Leaf $OutputRoot)
if (Test-Path -LiteralPath $Output) { throw 'Output root already exists.' }
New-Item -ItemType Directory -Path $Output | Out-Null

$commit = (& git -C $Source rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $commit -ne $ExpectedCommit) { throw "TurboVec source commit mismatch: $commit" }
$sourceStatus = @(& git -C $Source status --porcelain=v1)
if ($LASTEXITCODE -ne 0 -or $sourceStatus.Count -ne 0) { throw 'TurboVec source checkout must be clean.' }

$cargo = Resolve-AbsoluteItem -Path (Join-Path $env:USERPROFILE '.cargo\bin\cargo.exe') -Label 'Cargo executable'
$rustc = Resolve-AbsoluteItem -Path (Join-Path $env:USERPROFILE '.cargo\bin\rustc.exe') -Label 'Rust compiler'
$rustVersion = (& $rustc "+$ExpectedRust" --version)
if ($LASTEXITCODE -ne 0 -or $rustVersion -notmatch '^rustc 1\.89\.0 ') { throw "Exact Rust $ExpectedRust is unavailable." }

$modules = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'tests') -Filter 'test_*.py' -File |
    Where-Object { $_.BaseName -notlike 'test_turbovec*' } |
    Sort-Object Name |
    ForEach-Object { "scripts.testing.tests.$($_.BaseName)" })
$commands = [ordered]@{}
$commands.repository_baseline = Invoke-Logged -Name 'repository-baseline' -FilePath $Python -Arguments (@('-m','unittest') + $modules + @('-v')) -WorkingDirectory (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$baselineText = Read-CombinedLog $commands.repository_baseline
$baselineMatch = [regex]::Match($baselineText, 'Ran\s+(\d+)\s+tests?')
if (-not $baselineMatch.Success) { throw 'Could not parse repository baseline count.' }
$baselineCount = [int]$baselineMatch.Groups[1].Value
$baselinePassed = if ($commands.repository_baseline.exit_code -eq 0) { $baselineCount } else { 0 }
$commands.repository_baseline['accounting'] = New-Accounting -Passed $baselinePassed -Failed ($baselineCount-$baselinePassed)

$commands.upstream_python = Invoke-Logged -Name 'upstream-python' -FilePath $Python -Arguments @('-m','pytest','turbovec-python/tests','-ra','--tb=short') -WorkingDirectory $Source
$pythonText = Read-CombinedLog $commands.upstream_python
$summary = [regex]::Match($pythonText, '(\d+) failed,\s+(\d+) passed,\s+(\d+) skipped')
if (-not $summary.Success) { throw 'Could not parse upstream Python test arithmetic.' }
$commands.upstream_python['accounting'] = New-Accounting -Passed ([int]$summary.Groups[2].Value) -Failed ([int]$summary.Groups[1].Value) -Skipped ([int]$summary.Groups[3].Value)
$knownLongPathOnly = $commands.upstream_python.accounting.failed -eq 1 -and $pythonText.Contains('test_atomic_save_round_trips_a_long_sidecar_name')
$commands.upstream_python['known_long_path_only'] = $knownLongPathOnly

$commands.rust_core = Invoke-Logged -Name 'rust-core' -FilePath $cargo -Arguments @("+$ExpectedRust",'test','--release','--locked','-p','turbovec') -WorkingDirectory $Source
$commands.rust_core['accounting'] = Count-CargoTests (Read-CombinedLog $commands.rust_core)
$commands.rust_python = Invoke-Logged -Name 'rust-python' -FilePath $cargo -Arguments @("+$ExpectedRust",'test','--release','--locked','-p','turbovec-python','--no-default-features') -WorkingDirectory $Source
$commands.rust_python['accounting'] = Count-CargoTests (Read-CombinedLog $commands.rust_python)
$commands.clippy = Invoke-Logged -Name 'clippy' -FilePath $cargo -Arguments @("+$ExpectedRust",'clippy','--release','--locked','--workspace','--all-targets','--','-D','warnings') -WorkingDirectory $Source
$commands.clippy['accounting'] = New-Accounting -Passed $(if($commands.clippy.exit_code -eq 0){1}else{0}) -Failed $(if($commands.clippy.exit_code -eq 0){0}else{1})

$status = if (
    $commands.repository_baseline.exit_code -eq 0 -and $baselineCount -eq 111 -and
    $knownLongPathOnly -and
    $commands.rust_core.exit_code -eq 0 -and
    $commands.rust_python.exit_code -eq 0 -and
    $commands.clippy.exit_code -eq 0
) { 'passed_with_known_long_path_limitation' } else { 'failed' }

$manifest = [ordered]@{
    schema_version='2.0'
    campaign_id='turbovec-production-scale-final-evaluation-v2'
    experiment_id='EXP-TV-COMP-001'
    status=$status
    source=[ordered]@{commit=$commit;working_tree='clean'}
    runtime=[ordered]@{python=(& $Python --version 2>&1).ToString();rust=$rustVersion.ToString();toolchain=$ExpectedRust;installer_sha256='6f4bef66261261fcb43131be8720bab817d403a09edec7455c371974b90bdb7e'}
    commands=$commands
}
$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $Output 'manifest.json') -Encoding utf8
if ($status -ne 'passed_with_known_long_path_limitation') { exit 1 }
