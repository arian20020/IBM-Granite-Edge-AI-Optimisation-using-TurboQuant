[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$RepositoryRoot,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$OfficialStageDirectory,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$TurboQuantStageDirectory,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$ConverterStageDirectory,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$baselineImplementationCommit =
    'c1e0fe2f0bc3717dbec168ac5d3abbe4ebfc6a1d'
$expectedOfficialManifestSha256 =
    'db46a1c79a6bd2199eb4d9434ba406a1de51a11b060a4108a100542bdf9e39d3'
$staging = $null
$outerPath = $null
$checksumPath = $null
$succeeded = $false
$phase = 'startup'

function Get-NormalizedDirectoryPrefix {
    param([Parameter(Mandatory)][string]$Path)
    return [IO.Path]::GetFullPath($Path).TrimEnd('\', '/') +
        [IO.Path]::DirectorySeparatorChar
}

function Test-PathDescendsFrom {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string]$Path
    )
    return [IO.Path]::GetFullPath($Path).StartsWith(
        (Get-NormalizedDirectoryPrefix $Root),
        [StringComparison]::OrdinalIgnoreCase)
}

function Get-SafeRelativePath {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string]$Path
    )
    $rootPrefix = Get-NormalizedDirectoryPrefix $Root
    $fullPath = [IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith(
            $rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'relative-path-outside-root'
    }
    return $fullPath.Substring($rootPrefix.Length).Replace('\', '/')
}

function Get-LowerSha256 {
    param([Parameter(Mandatory)][string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Write-Utf8NoBom {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Text
    )
    $encoding = [Text.UTF8Encoding]::new($false, $true)
    [IO.File]::WriteAllText($Path, $Text, $encoding)
}

function Invoke-ManifestVerifier {
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$ScriptName,
        [Parameter(Mandatory)][string]$StageRoot
    )
    $scriptPath = Join-Path $Repository "scripts\openvino\$ScriptName"
    $result = @(& powershell.exe -NoLogo -NoProfile -NonInteractive `
        -ExecutionPolicy Bypass -File $scriptPath -StageDirectory $StageRoot 2>&1)
    if ($LASTEXITCODE -ne 0) { throw 'manifest-verification-failed' }
}

function Test-InputTree {
    param([Parameter(Mandatory)][string]$Root)
    $rootItem = Get-Item -LiteralPath $Root -Force
    if (-not $rootItem.PSIsContainer -or
        ($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'input-root-invalid'
    }
    $entries = @(Get-ChildItem -LiteralPath $Root -Recurse -Force)
    if ($entries | Where-Object {
            $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) {
        throw 'input-reparse-invalid'
    }
    $files = @($entries | Where-Object { -not $_.PSIsContainer })
    if ($files.Count -lt 1 -or $files.Count -gt 30000) {
        throw 'input-file-count-invalid'
    }
    [long]$bytes = 0
    foreach ($file in $files) {
        if ($file.Length -lt 0 -or $file.Length -gt 2147483648 -or
            [long]$file.Length -gt (4294967296 - $bytes)) {
            throw 'input-expanded-size-invalid'
        }
        $bytes += [long]$file.Length
    }
    return [pscustomobject]@{
        Files = @($files | Sort-Object FullName)
        FileCount = $files.Count
        Bytes = $bytes
    }
}

function New-DirectoryArchive {
    param(
        [Parameter(Mandatory)][string]$SourceRoot,
        [Parameter(Mandatory)][string]$ArchivePath
    )
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $tree = Test-InputTree $SourceRoot
    $stream = [IO.File]::Open($ArchivePath, [IO.FileMode]::CreateNew,
        [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $archive = [IO.Compression.ZipArchive]::new(
        $stream, [IO.Compression.ZipArchiveMode]::Create, $false)
    try {
        foreach ($file in $tree.Files) {
            $relative = Get-SafeRelativePath $SourceRoot $file.FullName
            if ([IO.Path]::IsPathRooted($relative) -or
                $relative -match '(^|[\/])\.\.([\/]|$)' -or
                $relative.IndexOf(':') -ge 0 -or
                $relative.Length -gt 512 -or
                $relative -match '[\x00-\x1f]') {
                throw 'archive-entry-traversal'
            }
            $entry = $archive.CreateEntry(
                $relative,
                [IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = [DateTimeOffset]::new(
                2000, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
            $input = [IO.File]::Open($file.FullName, [IO.FileMode]::Open,
                [IO.FileAccess]::Read, [IO.FileShare]::Read)
            $output = $entry.Open()
            try {
                $buffer = [byte[]]::new(81920)
                [long]$copied = 0
                while (($read = $input.Read($buffer, 0, $buffer.Length)) -gt 0) {
                    if ([long]$read -gt ([long]$file.Length - $copied)) {
                        throw 'archive-source-size-changed'
                    }
                    $output.Write($buffer, 0, $read)
                    $copied += [long]$read
                }
                if ($copied -ne [long]$file.Length) {
                    throw 'archive-source-size-changed'
                }
            }
            finally {
                $output.Dispose()
                $input.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
        $stream.Dispose()
    }

    $readArchive = [IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        $names = [Collections.Generic.HashSet[string]]::new(
            [StringComparer]::OrdinalIgnoreCase)
        [long]$expanded = 0
        foreach ($entry in $readArchive.Entries) {
            if ([string]::IsNullOrWhiteSpace($entry.Name) -or
                [IO.Path]::IsPathRooted($entry.FullName) -or
                $entry.FullName -match '(^|[\/])\.\.([\/]|$)' -or
                $entry.FullName.IndexOf(':') -ge 0 -or
                -not $names.Add($entry.FullName)) {
                throw 'duplicate-entry'
            }
            if ([long]$entry.Length -gt (4294967296 - $expanded)) {
                throw 'expanded-size-invalid'
            }
            $expanded += [long]$entry.Length
        }
        if ($readArchive.Entries.Count -ne $tree.FileCount -or
            $expanded -ne $tree.Bytes) {
            throw 'archive-inventory-invalid'
        }
    }
    finally { $readArchive.Dispose() }
    return [pscustomobject]@{
        FileCount = $tree.FileCount
        Bytes = $tree.Bytes
    }
}

function Copy-ContextFile {
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$StagingRoot,
        [Parameter(Mandatory)][string]$RelativeSource,
        [Parameter(Mandatory)][string]$RelativeDestination
    )
    $source = [IO.Path]::GetFullPath((Join-Path $Repository $RelativeSource))
    if (-not (Test-PathDescendsFrom $Repository $source) -or
        -not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw 'context-source-invalid'
    }
    $target = [IO.Path]::GetFullPath((Join-Path $StagingRoot $RelativeDestination))
    if (-not (Test-PathDescendsFrom $StagingRoot $target)) {
        throw 'context-destination-invalid'
    }
    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force
    Copy-Item -LiteralPath $source -Destination $target
    return $target
}

function New-PayloadRow {
    param(
        [Parameter(Mandatory)][string]$Role,
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)][string]$AbsolutePath,
        [int]$ExpandedFileCount = 0,
        [long]$ExpandedBytes = 0
    )
    $item = Get-Item -LiteralPath $AbsolutePath -Force
    return [ordered]@{
        role = $Role
        relativePath = $RelativePath.Replace('\', '/')
        length = [long]$item.Length
        sha256 = Get-LowerSha256 $item.FullName
        expandedFileCount = $ExpandedFileCount
        expandedBytes = $ExpandedBytes
        evidenceDisposition = 'transfer_input_only'
    }
}

try {
    $phase = 'root-validation'
    $repository = [IO.Path]::GetFullPath($RepositoryRoot)
    $official = [IO.Path]::GetFullPath($OfficialStageDirectory)
    $turboQuant = [IO.Path]::GetFullPath($TurboQuantStageDirectory)
    $converter = [IO.Path]::GetFullPath($ConverterStageDirectory)
    $output = [IO.Path]::GetFullPath($OutputDirectory)
    foreach ($root in @($repository, $official, $turboQuant, $converter)) {
        if (-not (Test-Path -LiteralPath $root -PathType Container)) {
            throw 'required-root-missing'
        }
        $item = Get-Item -LiteralPath $root -Force
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw 'required-root-reparse-invalid'
        }
    }
    $inputRoots = @($repository, $official, $turboQuant, $converter)
    $normalizedInputs = @($inputRoots |
        ForEach-Object { Get-NormalizedDirectoryPrefix $_ })
    if (@($normalizedInputs | Select-Object -Unique).Count -ne 4) {
        throw 'roots-aliased'
    }
    for ($left = 0; $left -lt $inputRoots.Count; $left++) {
        for ($right = $left + 1; $right -lt $inputRoots.Count; $right++) {
            if ((Test-PathDescendsFrom $inputRoots[$left] $inputRoots[$right]) -or
                (Test-PathDescendsFrom $inputRoots[$right] $inputRoots[$left])) {
                throw 'roots-aliased'
            }
        }
    }
    foreach ($root in @($repository, $official, $turboQuant, $converter)) {
        if ($output -ieq $root -or
            (Test-PathDescendsFrom $root $output) -or
            (Test-PathDescendsFrom $output $root)) {
            throw 'roots-aliased'
        }
    }
    if (Test-Path -LiteralPath $output) {
        $outputItem = Get-Item -LiteralPath $output -Force
        if (-not $outputItem.PSIsContainer -or
            ($outputItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
            @(Get-ChildItem -LiteralPath $output -Force).Count -ne 0) {
            throw 'output-not-empty'
        }
    }
    else {
        $null = New-Item -ItemType Directory -Path $output
    }

    $phase = 'candidate-validation'
    $branch = ([string](& git -C $repository branch --show-current)).Trim()
    $handoffCommit = ([string](& git -C $repository rev-parse HEAD)).Trim().ToLowerInvariant()
    $status = @(& git -C $repository status --porcelain)
    if ($branch -cne 'feature/openvino-route' -or
        $handoffCommit -cnotmatch '^[0-9a-f]{40}$' -or
        $status.Count -ne 0) {
        throw 'candidate-dirty-or-invalid'
    }
    @(& git -C $repository merge-base --is-ancestor `
        $baselineImplementationCommit $handoffCommit 2>&1) | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'baseline-not-ancestor' }

    $phase = 'official-manifest-verification'
    Invoke-ManifestVerifier $repository `
        'Test-OpenVinoOfficialWorkerManifest.ps1' $official
    $phase = 'turboquant-manifest-verification'
    Invoke-ManifestVerifier $repository `
        'Test-OpenVinoTurboQuantWorkerManifest.ps1' $turboQuant
    $phase = 'converter-manifest-verification'
    Invoke-ManifestVerifier $repository `
        'Test-OpenVinoConverterWorkerManifest.ps1' $converter
    $phase = 'official-manifest-identity'
    if ((Get-LowerSha256 (Join-Path $official 'worker-manifest.json')) -cne
        $expectedOfficialManifestSha256) {
        throw 'official-manifest-identity-invalid'
    }

    $phase = 'staging-creation'
    $staging = Join-Path $output ('.build-' + [Guid]::NewGuid().ToString('N'))
    $null = New-Item -ItemType Directory -Path $staging
    foreach ($leaf in @('repository','closures','inventory','tools\lib','context')) {
        $null = New-Item -ItemType Directory -Path (Join-Path $staging $leaf) -Force
    }

    $gitBundlePath = Join-Path $staging 'repository\openvino-route.bundle'
    $sourceSnapshotPath = Join-Path $staging 'repository\source-c1e0fe2f.zip'
    $phase = 'git-bundle-creation'
    @(& git -C $repository bundle create $gitBundlePath feature/openvino-route 2>&1) |
        Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'git-bundle-create-failed' }
    $phase = 'git-bundle-verification'
    $savedErrorAction = $ErrorActionPreference
    $gitBundleVerifyExitCode = -1
    try {
        $ErrorActionPreference = 'Continue'
        @(& git bundle verify $gitBundlePath 2>&1) | Out-Null
        $gitBundleVerifyExitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $savedErrorAction
    }
    if ($gitBundleVerifyExitCode -ne 0) { throw 'git-bundle-verify-failed' }
    $phase = 'source-snapshot-creation'
    @(& git -C $repository archive --format=zip `
        --output $sourceSnapshotPath $baselineImplementationCommit 2>&1) | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'git-archive-failed' }

    $phase = 'context-copy'
    $contextSources = [ordered]@{
        'docs\handoffs\openvino-ucl\READ_FIRST.md' = 'READ_FIRST.md'
        'docs\handoffs\openvino-ucl\CONTINUATION_PROMPT.md' = 'CONTINUATION_PROMPT.md'
        'docs\superpowers\plans\2026-08-20-openvino-route.md' = 'context\MASTER_IMPLEMENTATION_PLAN.md'
        'docs\superpowers\specs\2026-08-23-openvino-ucl-handoff-bundle-design.md' = 'context\HANDOFF_DESIGN.md'
        'docs\superpowers\plans\2026-08-23-openvino-ucl-handoff-bundle.md' = 'context\HANDOFF_IMPLEMENTATION_PLAN.md'
        '.superpowers\sdd\2026-08-20-openvino-route\progress.md' = 'context\PROGRESS.md'
        'docs\evidence\openvino\README.md' = 'context\RELEASE_EVIDENCE_CATALOGUE.md'
    }
    foreach ($number in 1..18) {
        $contextSources[".superpowers\sdd\2026-08-20-openvino-route\task-$number-report.md"] =
            "context\task-$number-report.md"
    }
    $contextTargets = @()
    foreach ($entry in $contextSources.GetEnumerator()) {
        $contextTargets += Copy-ContextFile $repository $staging `
            $entry.Key $entry.Value
    }
    $verificationSummaryPath = Join-Path $staging 'context\LOCAL_VERIFICATION_SUMMARY.md'
    @'
# Local verification summary

This file summarizes transferred local regression results. It is not hosted or
trusted UCL evidence.

- OpenVINO contracts: 187/187 passed at the Task 18 boundary.
- Model Inspection contracts: 357/357 passed.
- OpenVINO application tests: 272/272 passed.
- Packaged affected WinUI tests: 580/580 passed.
- OpenVINO process integration: 61 passed, zero failed, one physical-GPU skip.
- Release x64 application build: passed with official manifest validation.
- No-evidence release gate: `openvino_release_blocked`, exit 1.

Re-run all hardware-relevant checks on the exact laptop candidate.
'@ | Set-Content -LiteralPath $verificationSummaryPath -Encoding UTF8
    $contextTargets += $verificationSummaryPath

    $initializerTarget = Copy-ContextFile $repository $staging `
        'scripts\openvino\handoff\Initialize-UclHandoff.ps1' `
        'tools\Initialize-UclHandoff.ps1'
    $moduleTarget = Copy-ContextFile $repository $staging `
        'scripts\openvino\OpenVinoClosedJson.psm1' `
        'tools\lib\OpenVinoClosedJson.psm1'
    $contextTargets += @($initializerTarget, $moduleTarget)

    $officialZip = Join-Path $staging 'closures\official-worker.zip'
    $turboZip = Join-Path $staging 'closures\turboquant-worker.zip'
    $converterZip = Join-Path $staging 'closures\converter-stage-p.zip'
    $phase = 'official-archive-creation'
    $officialMetrics = New-DirectoryArchive $official $officialZip
    $phase = 'turboquant-archive-creation'
    $turboMetrics = New-DirectoryArchive $turboQuant $turboZip
    $phase = 'converter-archive-creation'
    $converterMetrics = New-DirectoryArchive $converter $converterZip

    $phase = 'inventory-creation'
    $payloadRows = @(
        (New-PayloadRow 'gitBundle' 'repository/openvino-route.bundle' $gitBundlePath),
        (New-PayloadRow 'sourceSnapshot' 'repository/source-c1e0fe2f.zip' $sourceSnapshotPath),
        (New-PayloadRow 'officialWorker' 'closures/official-worker.zip' $officialZip `
            $officialMetrics.FileCount $officialMetrics.Bytes),
        (New-PayloadRow 'turboQuantWorker' 'closures/turboquant-worker.zip' $turboZip `
            $turboMetrics.FileCount $turboMetrics.Bytes),
        (New-PayloadRow 'converter' 'closures/converter-stage-p.zip' $converterZip `
            $converterMetrics.FileCount $converterMetrics.Bytes)
    )
    foreach ($target in @($contextTargets | Sort-Object)) {
        $relative = Get-SafeRelativePath $staging $target
        $payloadRows += New-PayloadRow 'context' $relative $target
    }
    $payloadRows = @($payloadRows | Sort-Object { $_.relativePath })

    $inventory = [ordered]@{
        schemaVersion = 1
        handoffCommit = $handoffCommit
        baselineImplementationCommit = $baselineImplementationCommit
        branch = 'feature/openvino-route'
        payloads = $payloadRows
    }
    $inventoryPath = Join-Path $staging 'inventory\payloads.json'
    Write-Utf8NoBom $inventoryPath ($inventory | ConvertTo-Json -Depth 5)
    $checksumLines = @($payloadRows | ForEach-Object {
        "$($_.sha256)  $($_.relativePath)"
    })
    $checksumLines | Set-Content `
        -LiteralPath (Join-Path $staging 'CHECKSUMS.sha256') -Encoding ASCII

    $phase = 'outer-archive-creation'
    $shortCommit = $handoffCommit.Substring(0, 12)
    $outerPath = Join-Path $output "OpenVino-UCL-Handoff-$shortCommit.zip"
    $checksumPath = "$outerPath.sha256"
    $null = New-DirectoryArchive $staging $outerPath
    $phase = 'outer-checksum-creation'
    $outerHash = Get-LowerSha256 $outerPath
    "$outerHash  $([IO.Path]::GetFileName($outerPath))" | Set-Content `
        -LiteralPath $checksumPath -Encoding ASCII
    $succeeded = $true
}
catch {
    Write-Verbose "failure_phase=$phase"
    $succeeded = $false
}
finally {
    if ($null -ne $staging -and (Test-Path -LiteralPath $staging)) {
        $resolvedStaging = [IO.Path]::GetFullPath($staging)
        if (-not (Test-PathDescendsFrom $output $resolvedStaging) -or
            [IO.Path]::GetFileName($resolvedStaging) -cnotmatch '^\.build-[0-9a-f]{32}$') {
            $succeeded = $false
        }
        else {
            Remove-Item -LiteralPath $resolvedStaging -Recurse -Force
        }
    }
    if (-not $succeeded) {
        foreach ($ownedOutput in @($outerPath, $checksumPath)) {
            if ($null -ne $ownedOutput -and
                (Test-Path -LiteralPath $ownedOutput) -and
                (Test-PathDescendsFrom $output $ownedOutput)) {
                Remove-Item -LiteralPath $ownedOutput -Force
            }
        }
    }
}

if ($succeeded) {
    [Console]::Out.WriteLine('openvino_ucl_handoff_created')
    exit 0
}
[Console]::Out.WriteLine('openvino_ucl_handoff_invalid')
exit 1
