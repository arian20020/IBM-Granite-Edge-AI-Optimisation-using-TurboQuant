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
    [Parameter(Mandatory = $true)][string]$PreviousClosureFile,
    [string]$ActualPackageFile,
    [string[]]$Blocker = @()
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$approvedMembershipPolicyFile = [IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot 'S1PackageApprovedPaths.txt'))

function Assert-CanonicalGitIdentity([string]$Name, [string]$Value) {
    if ($Value -cnotmatch '^[0-9a-f]{40}$') {
        throw "$Name is not a canonical Git identity."
    }
}

function Get-Sha256([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

# ASCII and UTF-16LE privacy scan.  Scan bounded chunks so large native payloads
# never need to be retained as decoded strings, and overlap chunks so a sentinel
# cannot evade the check by crossing a read boundary.
function Assert-NoPrivateContent([string]$Path) {
    $pattern = '(?i)(?:[a-z]:\\(?:users|r4-[^\\\x00\r\n]*)(?:\\|/)|\\\\(?![?.]\\)[a-z0-9][a-z0-9._-]{1,63}\\[a-z0-9$._-]+\\|\.nuget(?:\\|/)|(?:all_proxy|http_proxy|https_proxy|no_proxy|access_token|api_key|password|secret|credential)\s*[:=]|cc7aee17|da49c3d1)'
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        $buffer = [byte[]]::new(1048576)
        $tail = [byte[]]::new(0)
        while (($read = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
            $chunk = [byte[]]::new($tail.Length + $read)
            if ($tail.Length -gt 0) { [Array]::Copy($tail, 0, $chunk, 0, $tail.Length) }
            [Array]::Copy($buffer, 0, $chunk, $tail.Length, $read)
            $ascii = [Text.Encoding]::ASCII.GetString($chunk)
            $unicode = [Text.Encoding]::Unicode.GetString($chunk)
            if ($ascii -match $pattern -or $unicode -match $pattern) {
                throw 'package member contains private content.'
            }
            $tailLength = [Math]::Min(512, $chunk.Length)
            $tail = [byte[]]::new($tailLength)
            [Array]::Copy($chunk, $chunk.Length - $tailLength, $tail, 0, $tailLength)
        }
    }
    finally {
        $stream.Dispose()
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
if (-not (Test-Path -LiteralPath $PreviousClosureFile -PathType Leaf)) {
    throw 'The prior package closure used for difference reporting is unavailable.'
}
$previous = Get-Content -LiteralPath $PreviousClosureFile -Raw | ConvertFrom-Json
$previousPaths = @($previous.entries | ForEach-Object { [string]$_.path })
if (-not (Test-Path -LiteralPath $ApprovedMembershipPolicyFile -PathType Leaf)) {
    throw 'The reviewed package membership policy is unavailable.'
}
$approvedPaths = [Collections.Generic.Dictionary[string,string]]::new(
    [StringComparer]::Ordinal)
foreach ($policyLine in [IO.File]::ReadAllLines(
    (Resolve-Path -LiteralPath $ApprovedMembershipPolicyFile))) {
    if ([string]::IsNullOrWhiteSpace($policyLine) -or $policyLine.StartsWith('#')) {
        continue
    }
    $policyParts = $policyLine.Split('|', 2)
    if ($policyParts.Count -ne 2 -or
        [string]::IsNullOrWhiteSpace($policyParts[0]) -or
        $policyParts[0] -cne $policyParts[0].Replace('\', '/').TrimStart('/') -or
        [string]::IsNullOrWhiteSpace($policyParts[1]) -or
        $approvedPaths.ContainsKey($policyParts[0])) {
        throw 'The reviewed package membership policy is malformed or duplicated.'
    }
    $approvedPaths.Add($policyParts[0], $policyParts[1])
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
        -not $approvedPaths.ContainsKey($target) -or
        -not $seen.Add($target)) {
        throw 'Evaluated package membership failed closed policy.'
    }
    if (-not (Test-Path -LiteralPath $source -PathType Leaf) -or
        ((Get-Item -LiteralPath $source -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'Evaluated package source is unavailable or redirected.'
    }
    $file = Get-Item -LiteralPath $source
    Assert-NoPrivateContent $source
    $entries.Add([ordered]@{
        path = $target
        bytes = [long]$file.Length
        sha256 = Get-Sha256 $source
        allowlistReason = $approvedPaths[$target]
    })
}
if ($entries.Count -eq 0) { throw 'Evaluated AppX membership was empty.' }
$entries = @($entries | Sort-Object -Property path)

$validatorProject = Join-Path $repositoryRoot 'tools\GraniteEdgeAI.R4Handoff.Validator\GraniteEdgeAI.R4Handoff.Validator.csproj'
& dotnet run --project $validatorProject --configuration Release --no-restore -- `
    assembly-membership ([IO.Path]::GetFullPath($EvaluatedMembershipFile)) $ImplementationSubjectCommit
if ($LASTEXITCODE -ne 0) {
    throw 'First-party package member does not bind to the implementation subject.'
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
    approvedMembershipPolicy = [ordered]@{
        path = 'scripts/verification/S1PackageApprovedPaths.txt'
        bytes = [long](Get-Item -LiteralPath $approvedMembershipPolicyFile).Length
        sha256 = Get-Sha256 $approvedMembershipPolicyFile
    }
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
        privateContentMatches = 0
        staleFirstPartyIdentityMatches = 0
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
