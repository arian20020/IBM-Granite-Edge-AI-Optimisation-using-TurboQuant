[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$EvaluatedMembershipFile,
    [Parameter(Mandatory = $true)][string]$OutputFile,
    [Parameter(Mandatory = $true)][string]$ImplementationSubjectCommit,
    [Parameter(Mandatory = $true)][string]$ImplementationSubjectTree,
    [Parameter(Mandatory = $true)][string]$BaseCommit,
    [Parameter(Mandatory = $true)][string]$BaseTree,
    [Parameter(Mandatory = $true)][string]$BuildCommandIdentity,
    [string]$Configuration = 'Release',
    [string]$Platform = 'x64',
    [string]$RuntimeIdentifier = 'win-x64',
    [string]$PreviousClosureFile,
    [string]$ActualPackageFile,
    [string[]]$Blocker = @()
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-CanonicalGitIdentity([string]$Name, [string]$Value) {
    if ($Value -cnotmatch '^[0-9a-f]{40}$') {
        throw "$Name is not a canonical Git identity."
    }
}

function Get-Sha256([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-AllowlistReason([string]$TargetPath) {
    $extension = [IO.Path]::GetExtension($TargetPath).ToLowerInvariant()
    switch ($extension) {
        '.exe' { return 'first-party-or-approved-runtime-executable' }
        '.dll' { return 'first-party-or-approved-runtime-library' }
        '.winmd' { return 'approved-runtime-metadata' }
        '.json' { return 'runtime-or-closure-configuration' }
        '.runtimeconfig.json' { return 'managed-runtime-configuration' }
        '.deps.json' { return 'managed-dependency-closure' }
        '.xbf' { return 'compiled-production-ui' }
        '.pri' { return 'compiled-production-resources' }
        '.xml' { return 'package-or-runtime-metadata' }
        '.manifest' { return 'package-or-runtime-manifest' }
        '.png' { return 'approved-application-asset' }
        '.ico' { return 'approved-application-asset' }
        '.svg' { return 'approved-application-asset' }
        '.dat' { return 'approved-runtime-data' }
        '.bin' { return 'approved-runtime-data' }
        default { throw "Evaluated package member has no approved classification." }
    }
}

Assert-CanonicalGitIdentity 'ImplementationSubjectCommit' $ImplementationSubjectCommit
Assert-CanonicalGitIdentity 'ImplementationSubjectTree' $ImplementationSubjectTree
Assert-CanonicalGitIdentity 'BaseCommit' $BaseCommit
Assert-CanonicalGitIdentity 'BaseTree' $BaseTree
if ($BuildCommandIdentity -cnotmatch '^[a-z0-9][a-z0-9._:-]{0,127}$') {
    throw 'BuildCommandIdentity is not sanitized.'
}
if (-not (Test-Path -LiteralPath $EvaluatedMembershipFile -PathType Leaf)) {
    throw 'The evaluated AppX membership input is unavailable.'
}

$forbidden = '(?i)(^|/)(debugfixtures?|tests?|evidence|models?|credentials?|secrets?|tokens?|proxies?)(/|$)|\.pdb$|\.appxrecipe$|\.build\.appxrecipe$|\.trx$|\.dmp$|\.dump$'
$entries = [Collections.Generic.List[object]]::new()
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($line in [IO.File]::ReadAllLines((Resolve-Path -LiteralPath $EvaluatedMembershipFile))) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $parts = $line.Split('|', 2)
    if ($parts.Count -ne 2) { throw 'Evaluated membership line is malformed.' }
    $target = $parts[0].Replace('\', '/').TrimStart('/')
    $source = [IO.Path]::GetFullPath($parts[1])
    if ([string]::IsNullOrWhiteSpace($target) -or
        [IO.Path]::IsPathRooted($target) -or
        $target.Contains('$(') -or
        $target.Split('/') -contains '..' -or
        $target -match $forbidden -or
        -not $seen.Add($target)) {
        throw 'Evaluated package membership failed closed policy.'
    }
    if (-not (Test-Path -LiteralPath $source -PathType Leaf) -or
        ((Get-Item -LiteralPath $source -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'Evaluated package source is unavailable or redirected.'
    }
    $file = Get-Item -LiteralPath $source
    $entries.Add([ordered]@{
        path = $target
        bytes = [long]$file.Length
        sha256 = Get-Sha256 $source
        allowlistReason = Get-AllowlistReason $target
    })
}
if ($entries.Count -eq 0) { throw 'Evaluated AppX membership was empty.' }
$entries = @($entries | Sort-Object -Property path)

$previousPaths = @()
if ($PreviousClosureFile -and (Test-Path -LiteralPath $PreviousClosureFile -PathType Leaf)) {
    $previous = Get-Content -LiteralPath $PreviousClosureFile -Raw | ConvertFrom-Json
    $previousPaths = @($previous.entries | ForEach-Object { [string]$_.path })
}
$currentPaths = @($entries | ForEach-Object { [string]$_.path })
$actualPackage = $null
if ($ActualPackageFile) {
    if (-not (Test-Path -LiteralPath $ActualPackageFile -PathType Leaf)) {
        throw 'The declared actual package is unavailable.'
    }
    $package = Get-Item -LiteralPath $ActualPackageFile
    $actualPackage = [ordered]@{
        status = 'available'
        filename = $package.Name
        bytes = [long]$package.Length
        sha256 = Get-Sha256 $package.FullName
    }
}

$closure = [ordered]@{
    schemaVersion = 3
    workerId = 'S1'
    implementationSubjectCommit = $ImplementationSubjectCommit
    implementationSubjectTree = $ImplementationSubjectTree
    baseCommit = $BaseCommit
    baseTree = $BaseTree
    configuration = $Configuration
    platform = $Platform
    runtimeIdentifier = $RuntimeIdentifier
    buildCommandIdentity = $BuildCommandIdentity
    rawBuildOutput = [ordered]@{
        disposition = 'not-package-membership'
        enumerationUsed = $false
    }
    evaluatedMembership = [ordered]@{
        source = 'MSBuild @(AppxPackagePayload) after _ComputeAppxPackagePayload'
        memberCount = [long]$entries.Count
    }
    actualPackage = $actualPackage
    entries = $entries
    baseToCandidateDifferences = [ordered]@{
        added = @($currentPaths | Where-Object { $_ -notin $previousPaths } | Sort-Object)
        removed = @($previousPaths | Where-Object { $_ -notin $currentPaths } | Sort-Object)
    }
    stableScans = [ordered]@{
        forbiddenMemberMatches = 0
        unresolvedExpressions = 0
        duplicateTargetPaths = 0
        privateSourcePathsEmitted = 0
    }
    blockers = @($Blocker)
    nonclaims = @(
        'Raw build output is not package membership.',
        'Evaluated membership is not an actual staged or signed package.',
        'Intel-native and performance acceptance are not claimed.'
    )
}

$parent = Split-Path -Parent ([IO.Path]::GetFullPath($OutputFile))
[IO.Directory]::CreateDirectory($parent) | Out-Null
$json = $closure | ConvertTo-Json -Depth 12
[IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputFile), $json + "`n", [Text.UTF8Encoding]::new($false))
