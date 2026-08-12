#requires -Version 5.1

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateScript({
        if ([string]::IsNullOrWhiteSpace($_)) {
            throw 'EvidencePath cannot be blank.'
        }

        if (-not [System.IO.Path]::IsPathRooted($_)) {
            throw 'EvidencePath must be an absolute path.'
        }

        $true
    })]
    [string] $EvidencePath,

    [Parameter()]
    [switch] $WhatIfContract,

    [Parameter()]
    [switch] $EvaluateContract
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:OwnedBase = [System.IO.Path]::GetFullPath((Join-Path `
    $env:SystemDrive `
    'mi-release-isolation'))
$script:RunId = [Guid]::NewGuid().ToString('N')
$script:OwnedRoot = [System.IO.Path]::GetFullPath((Join-Path `
    $script:OwnedBase `
    $script:RunId))
$script:OwnerMarkerName = '.model-inspection-fixture-isolation-owner'
$script:OwnerMarkerPath = Join-Path $script:OwnedRoot $script:OwnerMarkerName

function Assert-CanonicalChildPath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Candidate,

        [Parameter(Mandatory = $true)]
        [string] $Parent
    )

    $canonicalCandidate = [System.IO.Path]::GetFullPath($Candidate)
    $canonicalParent = [System.IO.Path]::GetFullPath($Parent).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $requiredPrefix = $canonicalParent + [System.IO.Path]::DirectorySeparatorChar

    if (-not $canonicalCandidate.StartsWith(
            $requiredPrefix,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Owned path escaped its canonical parent: $canonicalCandidate"
    }

    return $canonicalCandidate
}

function Assert-SafeIsolationBase {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Candidate
    )

    $canonical = [System.IO.Path]::GetFullPath($Candidate)
    $expected = [System.IO.Path]::GetFullPath((Join-Path `
        $env:SystemDrive `
        'mi-release-isolation'))
    if ($canonical.IndexOf('fixture', [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
        $canonical.IndexOf('gallery', [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
        -not [string]::Equals(
            $canonical,
            $expected,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Owned base must be the exact forbidden-token-free isolation path.'
    }

    return $canonical
}

function Assert-PhysicalDirectoryAncestorChain {
    param(
        [Parameter(Mandatory = $true)]
        [string] $DirectoryPath,

        [Parameter(Mandatory = $true)]
        [string] $Description
    )

    $current = [System.IO.Path]::GetFullPath($DirectoryPath)
    while (-not [string]::IsNullOrWhiteSpace($current)) {
        $item = Get-Item -LiteralPath $current -Force
        if (-not $item.PSIsContainer -or
            ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "$Description ancestor must be a physical directory: $current"
        }

        $parent = [System.IO.Directory]::GetParent($current)
        if ($null -eq $parent) {
            break
        }

        if ([string]::Equals(
                $parent.FullName,
                $current,
                [StringComparison]::OrdinalIgnoreCase)) {
            break
        }

        $current = $parent.FullName
    }
}

function Get-RelativeChildPath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Parent,

        [Parameter(Mandatory = $true)]
        [string] $Child
    )

    $canonicalParent = [System.IO.Path]::GetFullPath($Parent).TrimEnd('\')
    $canonicalChild = Assert-CanonicalChildPath `
        -Candidate $Child `
        -Parent $canonicalParent
    return $canonicalChild.Substring($canonicalParent.Length + 1)
}

function Get-OwnedTreeEntries {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Root
    )

    $canonicalRoot = [System.IO.Path]::GetFullPath($Root)
    $pending = New-Object 'System.Collections.Generic.Stack[string]'
    $entries = New-Object 'System.Collections.Generic.List[System.IO.FileSystemInfo]'
    $pending.Push($canonicalRoot)

    while ($pending.Count -gt 0) {
        $directory = $pending.Pop()
        foreach ($entry in @(Get-ChildItem -LiteralPath $directory -Force)) {
            $null = Assert-CanonicalChildPath -Candidate $entry.FullName -Parent $canonicalRoot
            if (($entry.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Reparse points are forbidden inside the owned root: $($entry.FullName)"
            }

            $entries.Add($entry)
            if ($entry.PSIsContainer) {
                $pending.Push($entry.FullName)
            }
        }
    }

    return @($entries)
}

function Assert-OwnedRoot {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Root
    )

    $canonicalRoot = Assert-CanonicalChildPath `
        -Candidate $Root `
        -Parent $script:OwnedBase
    if (-not [string]::Equals(
            [System.IO.Path]::GetFileName($canonicalRoot),
            $script:RunId,
            [StringComparison]::Ordinal)) {
        throw 'Owned root leaf does not match the current GUID.'
    }

    if (-not (Test-Path -LiteralPath $canonicalRoot -PathType Container)) {
        throw 'Owned root does not exist.'
    }

    $rootItem = Get-Item -LiteralPath $canonicalRoot -Force
    if (($rootItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'Owned root cannot be a reparse point.'
    }

    if (-not (Test-Path -LiteralPath $script:OwnerMarkerPath -PathType Leaf)) {
        throw 'Owned root marker is missing.'
    }

    $marker = (Get-Content -LiteralPath $script:OwnerMarkerPath -Raw).Trim()
    if (-not [string]::Equals($marker, $script:RunId, [StringComparison]::Ordinal)) {
        throw 'Owned root marker does not match the current GUID.'
    }

    $null = Get-OwnedTreeEntries -Root $canonicalRoot
    return $canonicalRoot
}

function Remove-OwnedRoot {
    if (-not (Test-Path -LiteralPath $script:OwnedRoot)) {
        return
    }

    $canonicalRoot = Assert-OwnedRoot -Root $script:OwnedRoot
    Remove-Item -LiteralPath $canonicalRoot -Recurse -Force
    if (Test-Path -LiteralPath $canonicalRoot) {
        throw 'Owned root cleanup did not complete.'
    }
}

function ConvertTo-WindowsCommandLineArgument {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string] $Value
    )

    if ($Value.Length -gt 0 -and $Value -notmatch '[\s"]') {
        return $Value
    }

    $builder = New-Object System.Text.StringBuilder
    $null = $builder.Append('"')
    $backslashes = 0
    foreach ($character in $Value.ToCharArray()) {
        if ($character -eq '\') {
            $backslashes++
            continue
        }

        if ($character -eq '"') {
            $null = $builder.Append(('\' * (($backslashes * 2) + 1)))
            $null = $builder.Append('"')
            $backslashes = 0
            continue
        }

        if ($backslashes -gt 0) {
            $null = $builder.Append(('\' * $backslashes))
            $backslashes = 0
        }

        $null = $builder.Append($character)
    }

    if ($backslashes -gt 0) {
        $null = $builder.Append(('\' * ($backslashes * 2)))
    }

    $null = $builder.Append('"')
    return $builder.ToString()
}

function Stop-ProcessTreeChecked {
    param(
        [Parameter(Mandatory = $true)]
        [System.Diagnostics.Process] $Process,

        [Parameter(Mandatory = $true)]
        [string] $Operation
    )

    $taskKillPath = Join-Path $env:SystemRoot 'System32\taskkill.exe'
    $killInfo = New-Object System.Diagnostics.ProcessStartInfo
    $killInfo.FileName = $taskKillPath
    $killInfo.Arguments = "/PID $($Process.Id) /T /F"
    $killInfo.UseShellExecute = $false
    $killInfo.CreateNoWindow = $true
    $killInfo.RedirectStandardOutput = $true
    $killInfo.RedirectStandardError = $true
    $killer = New-Object System.Diagnostics.Process
    $killer.StartInfo = $killInfo
    if (-not $killer.Start()) {
        throw "$Operation timed out and taskkill did not start."
    }

    try {
        $killOutput = $killer.StandardOutput.ReadToEndAsync()
        $killError = $killer.StandardError.ReadToEndAsync()
        if (-not $killer.WaitForExit(30000)) {
            $killer.Kill()
            $null = $killer.WaitForExit(5000)
            throw "$Operation timed out and taskkill did not finish within 30 seconds."
        }

        $killer.WaitForExit()
        $output = $killOutput.GetAwaiter().GetResult()
        $errorOutput = $killError.GetAwaiter().GetResult()
        if ($killer.ExitCode -ne 0) {
            throw "$Operation timed out and taskkill failed with exit code " +
                "$($killer.ExitCode).`n$output$errorOutput"
        }
    }
    finally {
        $killer.Dispose()
    }

    if (-not $Process.WaitForExit(30000)) {
        throw "$Operation timed out and its process tree remained alive after taskkill."
    }
}

function Wait-NoOwnedProcesses {
    param(
        [Parameter(Mandatory = $true)]
        [string] $OwnedRoot,

        [Parameter()]
        [ValidateRange(1, 60)]
        [int] $TimeoutSeconds = 30
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $owners = @(Get-CimInstance -ClassName Win32_Process | Where-Object {
            -not [string]::IsNullOrWhiteSpace($_.CommandLine) -and
            $_.CommandLine.IndexOf(
                $OwnedRoot,
                [StringComparison]::OrdinalIgnoreCase) -ge 0
        })
        if ($owners.Count -eq 0) {
            return
        }

        Start-Sleep -Milliseconds 200
    }
    while ([DateTime]::UtcNow -lt $deadline)

    $ownerIds = @($owners | ForEach-Object { $_.ProcessId }) -join ','
    throw "Owned output still has process owners after $TimeoutSeconds seconds: $ownerIds"
}

function Get-OwnedProcessEnvironment {
    $paths = [ordered]@{
        NUGET_PACKAGES = Join-Path $script:OwnedRoot 'nuget-packages'
        NUGET_HTTP_CACHE_PATH = Join-Path $script:OwnedRoot 'nuget-http-cache'
        NUGET_PLUGINS_CACHE_PATH = Join-Path $script:OwnedRoot 'nuget-plugins-cache'
        NUGET_SCRATCH = Join-Path $script:OwnedRoot 'nuget-scratch'
        DOTNET_CLI_HOME = Join-Path $script:OwnedRoot 'dotnet-cli-home'
        TEMP = Join-Path $script:OwnedRoot 'temp'
        TMP = Join-Path $script:OwnedRoot 'temp'
    }
    foreach ($path in @($paths.Values | Sort-Object -Unique)) {
        $canonical = Assert-CanonicalChildPath `
            -Candidate $path `
            -Parent $script:OwnedRoot
        $null = New-Item -ItemType Directory -Path $canonical -Force
    }

    $paths['DOTNET_CLI_TELEMETRY_OPTOUT'] = '1'
    $paths['DOTNET_SKIP_FIRST_TIME_EXPERIENCE'] = '1'
    $paths['MSBUILDDISABLENODEREUSE'] = '1'
    $paths['UseSharedCompilation'] = 'false'

    return ,$paths
}

function Invoke-CheckedProcess {
    param(
        [Parameter(Mandatory = $true)]
        [string] $FilePath,

        [Parameter(Mandatory = $true)]
        [string[]] $Arguments,

        [Parameter(Mandatory = $true)]
        [string] $WorkingDirectory,

        [Parameter(Mandatory = $true)]
        [ValidateRange(1, 3600)]
        [int] $TimeoutSeconds,

        [Parameter(Mandatory = $true)]
        [string] $Operation,

        [Parameter()]
        [hashtable] $EnvironmentOverrides = @{}
    )

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $FilePath
    $startInfo.Arguments = (($Arguments | ForEach-Object {
        ConvertTo-WindowsCommandLineArgument -Value $_
    }) -join ' ')
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.StandardOutputEncoding = New-Object System.Text.UTF8Encoding($false)
    $startInfo.StandardErrorEncoding = New-Object System.Text.UTF8Encoding($false)
    $effectiveEnvironment = Get-OwnedProcessEnvironment
    foreach ($entry in $EnvironmentOverrides.GetEnumerator()) {
        $effectiveEnvironment[[string] $entry.Key] = [string] $entry.Value
    }

    foreach ($entry in $effectiveEnvironment.GetEnumerator()) {
        if ([string]::IsNullOrWhiteSpace([string] $entry.Key) -or
            $null -eq $entry.Value) {
            throw "$Operation has an invalid environment override."
        }

        $startInfo.EnvironmentVariables[[string] $entry.Key] = [string] $entry.Value
    }

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw "$Operation did not start."
    }

    try {
        $standardOutput = $process.StandardOutput.ReadToEndAsync()
        $standardError = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            Stop-ProcessTreeChecked -Process $process -Operation $Operation
            throw "$Operation timed out after $TimeoutSeconds seconds."
        }

        $process.WaitForExit()
        $output = $standardOutput.GetAwaiter().GetResult()
        $errorOutput = $standardError.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) {
            throw "$Operation failed with exit code $($process.ExitCode).`n$output$errorOutput"
        }

        return [pscustomobject]@{
            ExitCode = $process.ExitCode
            StandardOutput = $output
            StandardError = $errorOutput
        }
    }
    finally {
        $process.Dispose()
    }
}

function Get-Sha256 {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-RepositoryRoot {
    $candidate = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
    if (-not (Test-Path -LiteralPath (Join-Path $candidate 'global.json') -PathType Leaf) -or
        -not (Test-Path -LiteralPath (Join-Path `
            $candidate `
            'IBM Granite with TurboQuant (Intel).slnx') -PathType Leaf)) {
        throw 'Repository root could not be resolved from the script location.'
    }

    Assert-PhysicalDirectoryAncestorChain `
        -DirectoryPath $candidate `
        -Description 'Repository root'
    foreach ($sentinel in @(
            (Join-Path $candidate 'global.json'),
            (Join-Path $candidate 'IBM Granite with TurboQuant (Intel).slnx'))) {
        $item = Get-Item -LiteralPath $sentinel -Force
        if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'Repository root sentinel cannot be a reparse point.'
        }
    }

    return $candidate
}

function Assert-PhysicalRepositoryPath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepositoryRoot,

        [Parameter(Mandatory = $true)]
        [string] $Candidate,

        [Parameter(Mandatory = $true)]
        [string] $RelativePath
    )

    $canonicalRoot = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\')
    $canonicalCandidate = Assert-CanonicalChildPath `
        -Candidate $Candidate `
        -Parent $canonicalRoot
    Assert-PhysicalDirectoryAncestorChain `
        -DirectoryPath $canonicalRoot `
        -Description 'Repository source'

    $relative = $canonicalCandidate.Substring($canonicalRoot.Length + 1)
    $segments = @($relative -split '[\\/]' | Where-Object { $_.Length -gt 0 })
    if ($segments.Count -eq 0) {
        throw "Repository snapshot path is empty: $RelativePath"
    }

    $current = $canonicalRoot
    for ($index = 0; $index -lt $segments.Count; $index++) {
        $current = Assert-CanonicalChildPath `
            -Candidate (Join-Path $current $segments[$index]) `
            -Parent $canonicalRoot
        if (-not (Test-Path -LiteralPath $current)) {
            throw "Repository snapshot path disappeared: $RelativePath"
        }

        $item = Get-Item -LiteralPath $current -Force
        if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Repository snapshot path uses a reparse point: $RelativePath"
        }

        $isLeaf = $index -eq ($segments.Count - 1)
        if ((-not $isLeaf -and -not $item.PSIsContainer) -or
            ($isLeaf -and $item.PSIsContainer)) {
            throw "Repository snapshot path has an unexpected physical type: $RelativePath"
        }
    }

    return $canonicalCandidate
}

function Get-RepositorySnapshotState {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepositoryRoot
    )

    $git = (Get-Command git.exe -ErrorAction Stop).Source
    $headResult = Invoke-CheckedProcess `
        -FilePath $git `
        -Arguments @('-C', $RepositoryRoot, 'rev-parse', 'HEAD') `
        -WorkingDirectory $RepositoryRoot `
        -TimeoutSeconds 30 `
        -Operation 'Repository snapshot HEAD query'
    $head = $headResult.StandardOutput.Trim()
    if ($head -notmatch '^[0-9a-f]{40}$') {
        throw 'Repository snapshot HEAD query did not return a full lowercase SHA.'
    }

    $listing = Invoke-CheckedProcess `
        -FilePath $git `
        -Arguments @(
            '-C',
            $RepositoryRoot,
            'ls-files',
            '-z',
            '--cached',
            '--others',
            '--exclude-standard') `
        -WorkingDirectory $RepositoryRoot `
        -TimeoutSeconds 60 `
        -Operation 'Repository snapshot NUL listing'
    [string[]] $relativePaths = @($listing.StandardOutput.Split(
            [char[]] @([char] 0),
            [System.StringSplitOptions]::RemoveEmptyEntries))
    [Array]::Sort($relativePaths, [StringComparer]::Ordinal)
    if ($relativePaths.Count -eq 0) {
        throw 'Repository snapshot listing was empty.'
    }

    $status = Invoke-CheckedProcess `
        -FilePath $git `
        -Arguments @(
            '-C',
            $RepositoryRoot,
            'status',
            '--porcelain=v1',
            '-z',
            '--untracked-files=all') `
        -WorkingDirectory $RepositoryRoot `
        -TimeoutSeconds 60 `
        -Operation 'Repository snapshot NUL status'
    return [pscustomobject]@{
        Head = $head
        RelativePaths = $relativePaths
        Status = $status.StandardOutput
    }
}

function Assert-RepositorySnapshotStateEqual {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Before,

        [Parameter(Mandatory = $true)]
        [object] $After
    )

    if ($Before.Head -cne $After.Head -or
        $Before.Status -cne $After.Status -or
        $Before.RelativePaths.Count -ne $After.RelativePaths.Count) {
        throw 'Repository HEAD, status, or exact file set changed during the fresh snapshot.'
    }

    for ($index = 0; $index -lt $Before.RelativePaths.Count; $index++) {
        if ($Before.RelativePaths[$index] -cne $After.RelativePaths[$index]) {
            throw 'Repository exact file set changed during the fresh snapshot.'
        }
    }
}

function Copy-RepositorySnapshot {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepositoryRoot,

        [Parameter(Mandatory = $true)]
        [string] $DestinationRoot
    )

    $before = Get-RepositorySnapshotState -RepositoryRoot $RepositoryRoot
    $relativePaths = @($before.RelativePaths)

    $null = New-Item -ItemType Directory -Path $DestinationRoot
    $recordedHashes = @{}
    foreach ($relativePath in $relativePaths) {
        if ($relativePath.IndexOfAny(@([char]0, [char]10, [char]13)) -ge 0) {
            throw 'Repository snapshot contains a control character in a path.'
        }

        $sourcePath = Assert-PhysicalRepositoryPath `
            -RepositoryRoot $RepositoryRoot `
            -Candidate (Join-Path $RepositoryRoot $relativePath) `
            -RelativePath $relativePath

        $destinationPath = Assert-CanonicalChildPath `
            -Candidate (Join-Path $DestinationRoot $relativePath) `
            -Parent $DestinationRoot
        $destinationDirectory = Split-Path -Parent $destinationPath
        $null = New-Item -ItemType Directory -Path $destinationDirectory -Force
        Copy-Item -LiteralPath $sourcePath -Destination $destinationPath
        $sourceHash = Get-Sha256 -Path $sourcePath
        $destinationHash = Get-Sha256 -Path $destinationPath
        if (-not [string]::Equals(
                $sourceHash,
                $destinationHash,
                [StringComparison]::Ordinal)) {
            throw "Repository snapshot copy changed bytes: $relativePath"
        }

        $recordedHashes[$relativePath] = $sourceHash
    }

    foreach ($relativePath in @($recordedHashes.Keys)) {
        $sourcePath = Assert-PhysicalRepositoryPath `
            -RepositoryRoot $RepositoryRoot `
            -Candidate (Join-Path $RepositoryRoot $relativePath) `
            -RelativePath $relativePath
        if (-not [string]::Equals(
                $recordedHashes[$relativePath],
                (Get-Sha256 -Path $sourcePath),
                [StringComparison]::Ordinal)) {
            throw "Repository source changed during the fresh snapshot: $relativePath"
        }
    }

    $after = Get-RepositorySnapshotState -RepositoryRoot $RepositoryRoot
    Assert-RepositorySnapshotStateEqual -Before $before -After $after

    [string[]] $digestPaths = @($recordedHashes.Keys)
    [Array]::Sort($digestPaths, [StringComparer]::Ordinal)
    $digestInput = ($digestPaths | ForEach-Object {
        "$($_.Replace('\', '/'))`0$($recordedHashes[$_])"
    }) -join "`n"
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $digestBytes = [System.Text.Encoding]::UTF8.GetBytes($digestInput)
        $snapshotDigest = ([BitConverter]::ToString(
            $sha.ComputeHash($digestBytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }

    return [pscustomobject]@{
        Commit = $before.Head
        FileCount = $recordedHashes.Count
        Sha256 = $snapshotDigest
    }
}

function Write-Utf8NoBomLf {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string] $Content
    )

    $normalized = $Content.Replace("`r`n", "`n").Replace("`r", "`n")
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $normalized, $encoding)
}

function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} `
        'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path -LiteralPath $vswhere -PathType Leaf)) {
        throw 'Visual Studio Installer discovery tool was not found.'
    }

    $result = Invoke-CheckedProcess `
        -FilePath $vswhere `
        -Arguments @(
            '-latest',
            '-products',
            '*',
            '-requires',
            'Microsoft.Component.MSBuild',
            '-find',
            'MSBuild\**\Bin\MSBuild.exe') `
        -WorkingDirectory $script:OwnedRoot `
        -TimeoutSeconds 30 `
        -Operation 'MSBuild discovery'
    $candidates = @($result.StandardOutput -split "`r?`n" | Where-Object {
        -not [string]::IsNullOrWhiteSpace($_)
    })
    if ($candidates.Count -ne 1 -or
        -not (Test-Path -LiteralPath $candidates[0] -PathType Leaf)) {
        throw 'MSBuild discovery did not return exactly one executable.'
    }

    return [System.IO.Path]::GetFullPath($candidates[0])
}

function Get-BuildPathPins {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Configuration,

        [Parameter(Mandatory = $true)]
        [string] $Scope,

        [Parameter()]
        [string] $BuildRootParent = (Join-Path $script:OwnedRoot 'build')
    )

    $buildRootParent = Assert-CanonicalChildPath `
        -Candidate $BuildRootParent `
        -Parent $script:OwnedRoot
    $buildRoot = Assert-CanonicalChildPath `
        -Candidate (Join-Path $buildRootParent $Scope) `
        -Parent $script:OwnedRoot
    $null = New-Item -ItemType Directory -Path $buildRoot -Force
    $escapedBuildRoot = [System.Security.SecurityElement]::Escape(
        $buildRoot.TrimEnd('\'))
    $propsTemplate = @'
<Project>
  <PropertyGroup>
    <_ModelInspectionIsolationBuildRoot>{0}</_ModelInspectionIsolationBuildRoot>
    <BaseIntermediateOutputPath>$(_ModelInspectionIsolationBuildRoot)\obj\$(MSBuildProjectName)\</BaseIntermediateOutputPath>
    <MSBuildProjectExtensionsPath>$(_ModelInspectionIsolationBuildRoot)\obj\$(MSBuildProjectName)\</MSBuildProjectExtensionsPath>
    <BaseOutputPath>$(_ModelInspectionIsolationBuildRoot)\bin\$(MSBuildProjectName)\</BaseOutputPath>
    <OutputPath>$(_ModelInspectionIsolationBuildRoot)\out\$(MSBuildProjectName)\</OutputPath>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>
  </PropertyGroup>
</Project>
'@
    $propsPath = Assert-CanonicalChildPath `
        -Candidate (Join-Path $buildRoot 'isolation.before-directory-build.props') `
        -Parent $script:OwnedRoot
    Write-Utf8NoBomLf `
        -Path $propsPath `
        -Content ([string]::Format(
            [Globalization.CultureInfo]::InvariantCulture,
            $propsTemplate,
            $escapedBuildRoot))
    $pins = [ordered]@{
        CustomBeforeDirectoryBuildProps = $propsPath
        RestorePackagesPath = Join-Path $script:OwnedRoot 'nuget-packages'
        UseSharedCompilation = 'false'
        _ModelInspectionWorkerPublishRoot = Join-Path `
            $buildRoot `
            "worker\$Configuration\publish"
        _ModelInspectionWorkerArtifactsRoot = Join-Path `
            $buildRoot `
            "worker\$Configuration\artifacts"
        _ModelInspectionWorkerManifestPath = Join-Path `
            $buildRoot `
            "worker\$Configuration\worker-manifest.json"
    }
    foreach ($entry in @($pins.GetEnumerator() | Where-Object {
                $_.Key -ne 'UseSharedCompilation'
            })) {
        $canonical = Assert-CanonicalChildPath `
            -Candidate ([string] $entry.Value) `
            -Parent $script:OwnedRoot
        $pins[$entry.Key] = $canonical
    }

    $arguments = @($pins.GetEnumerator() | ForEach-Object {
        "/p:$($_.Key)=$($_.Value)"
    })
    return [pscustomobject]@{
        BuildRoot = $buildRoot
        Values = $pins
        Arguments = $arguments
    }
}

function Get-AppBuildPathPins {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('Debug', 'Release')]
        [string] $Configuration,

        [Parameter()]
        [string] $PackageDirectory
    )

    $pins = Get-BuildPathPins `
        -Configuration $Configuration `
        -Scope "app-$($Configuration.ToLowerInvariant())"
    if ($Configuration -eq 'Release') {
        $packageRoot = Assert-CanonicalChildPath `
            -Candidate $PackageDirectory `
            -Parent $script:OwnedRoot
        $pins.Values['AppxPackageOutput'] = Assert-CanonicalChildPath `
            -Candidate (Join-Path $packageRoot 'application.msix') `
            -Parent $script:OwnedRoot
        $pins.Arguments = @($pins.Values.GetEnumerator() | ForEach-Object {
            "/p:$($_.Key)=$($_.Value)"
        })
    }

    return $pins
}

function Assert-BuildPathEvaluation {
    param(
        [Parameter(Mandatory = $true)]
        [string] $MSBuild,

        [Parameter(Mandatory = $true)]
        [string] $ProjectPath,

        [Parameter(Mandatory = $true)]
        [string] $Configuration,

        [Parameter(Mandatory = $true)]
        [object] $Pins
    )

    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($ProjectPath)
    $expectedProperties = [ordered]@{
        BaseIntermediateOutputPath = Join-Path $Pins.BuildRoot "obj\$projectName\"
        _InitialBaseIntermediateOutputPath = Join-Path $Pins.BuildRoot "obj\$projectName\"
        MSBuildProjectExtensionsPath = Join-Path $Pins.BuildRoot "obj\$projectName\"
        _InitialMSBuildProjectExtensionsPath = Join-Path $Pins.BuildRoot "obj\$projectName\"
        BaseOutputPath = Join-Path $Pins.BuildRoot "bin\$projectName\"
        OutputPath = Join-Path $Pins.BuildRoot "out\$projectName\"
        CustomBeforeDirectoryBuildProps = $Pins.Values.CustomBeforeDirectoryBuildProps
        RestorePackagesPath = $Pins.Values.RestorePackagesPath
        UseSharedCompilation = 'false'
        _ModelInspectionWorkerPublishRoot = $Pins.Values._ModelInspectionWorkerPublishRoot
        _ModelInspectionWorkerArtifactsRoot = $Pins.Values._ModelInspectionWorkerArtifactsRoot
        _ModelInspectionWorkerManifestPath = $Pins.Values._ModelInspectionWorkerManifestPath
    }
    if ($Pins.Values.Contains('AppxPackageOutput')) {
        $expectedProperties['AppxPackageOutput'] = $Pins.Values.AppxPackageOutput
    }

    $propertyNames = @($expectedProperties.Keys)
    $result = Invoke-CheckedProcess `
        -FilePath $MSBuild `
        -Arguments (@(
            $ProjectPath,
            '/nologo',
            "/p:Configuration=$Configuration",
            '/p:Platform=x64',
            '/p:RuntimeIdentifier=win-x64') +
            @($Pins.Arguments) +
            @("-getProperty:$($propertyNames -join ',')")) `
        -WorkingDirectory (Split-Path -Parent $ProjectPath) `
        -TimeoutSeconds 120 `
        -Operation "$Configuration build-path pre-target evaluation"
    $start = $result.StandardOutput.IndexOf('{')
    $end = $result.StandardOutput.LastIndexOf('}')
    if ($start -lt 0 -or $end -lt $start) {
        throw 'Build-path pre-target evaluation returned no JSON.'
    }

    $evaluation = $result.StandardOutput.Substring(
        $start,
        ($end - $start) + 1) | ConvertFrom-Json
    foreach ($name in $propertyNames) {
        $actualProperty = $evaluation.Properties.PSObject.Properties[$name]
        if ($null -eq $actualProperty) {
            throw "Build-path pre-target evaluation omitted $name."
        }

        $expected = [string] $expectedProperties[$name]
        $actual = [string] $actualProperty.Value
        if ($name -ne 'UseSharedCompilation') {
            $expected = [System.IO.Path]::GetFullPath($expected)
            $actual = [System.IO.Path]::GetFullPath($actual)
            $null = Assert-CanonicalChildPath `
                -Candidate $actual `
                -Parent $script:OwnedRoot
            $expected = $expected.TrimEnd('\')
            $actual = $actual.TrimEnd('\')
        }

        if (-not [string]::Equals(
                $actual,
                $expected,
                [StringComparison]::OrdinalIgnoreCase)) {
            throw "Build-path pre-target evaluation changed $name."
        }
    }
}

function Assert-SnapshotBuildPathIsolation {
    param(
        [Parameter(Mandatory = $true)]
        [string] $MSBuild,

        [Parameter(Mandatory = $true)]
        [string] $SourceRoot,

        [Parameter(Mandatory = $true)]
        [ValidateSet('Debug', 'Release')]
        [string] $Configuration,

        [Parameter(Mandatory = $true)]
        [object] $Pins
    )

    $projects = @(Get-ChildItem `
        -LiteralPath $SourceRoot `
        -Recurse `
        -File `
        -Filter '*.csproj')
    if ($projects.Count -ne 19) {
        throw "Fresh source snapshot contains $($projects.Count) projects; expected 19."
    }

    $names = New-Object 'System.Collections.Generic.HashSet[string]' (
        [StringComparer]::OrdinalIgnoreCase)
    foreach ($project in $projects) {
        $null = Assert-CanonicalChildPath `
            -Candidate $project.FullName `
            -Parent $SourceRoot
        if (($project.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Project path cannot be a reparse point: $($project.FullName)"
        }

        if (-not $names.Add($project.BaseName)) {
            throw "Project basename is not unique for isolated output derivation: $($project.BaseName)"
        }
    }

    foreach ($project in @($projects | Sort-Object FullName)) {
        Assert-BuildPathEvaluation `
            -MSBuild $MSBuild `
            -ProjectPath $project.FullName `
            -Configuration $Configuration `
            -Pins $Pins
    }
}

function Assert-WorkerDestructiveTargetContract {
    param(
        [Parameter(Mandatory = $true)]
        [string] $SourceRoot
    )

    $targetPath = Join-Path `
        $SourceRoot `
        'IBM Granite with TurboQuant (Intel)\ModelInspection.WorkerPackaging.targets'
    if (-not (Test-Path -LiteralPath $targetPath -PathType Leaf)) {
        throw 'Worker packaging target is missing from the fresh source snapshot.'
    }

    [xml] $document = Get-Content -LiteralPath $targetPath -Raw
    [string[]] $removeTargets = @($document.SelectNodes(
            '//*[local-name()="RemoveDir"]') | ForEach-Object {
            [string] $_.GetAttribute('Directories')
        })
    [Array]::Sort($removeTargets, [StringComparer]::Ordinal)
    [string[]] $expectedRemoveTargets = @(
        '$(_ModelInspectionWorkerArtifactsRoot)',
        '$(_ModelInspectionWorkerPublishRoot)')
    [Array]::Sort($expectedRemoveTargets, [StringComparer]::Ordinal)
    if ($removeTargets.Count -ne $expectedRemoveTargets.Count) {
        throw 'Worker packaging target has an unexpected RemoveDir count.'
    }

    for ($index = 0; $index -lt $expectedRemoveTargets.Count; $index++) {
        if ($removeTargets[$index] -cne $expectedRemoveTargets[$index]) {
            throw 'Worker packaging target has an unpinned RemoveDir operand.'
        }
    }

    [string[]] $deleteTargets = @($document.SelectNodes(
            '//*[local-name()="Delete"]') | ForEach-Object {
            [string] $_.GetAttribute('Files')
        })
    if ($deleteTargets.Count -ne 1 -or
        $deleteTargets[0] -cne '$(_ModelInspectionWorkerManifestPath)') {
        throw 'Worker packaging target has an unpinned Delete operand.'
    }
}

function Invoke-AppBuild {
    param(
        [Parameter(Mandatory = $true)]
        [string] $MSBuild,

        [Parameter(Mandatory = $true)]
        [string] $ProjectPath,

        [Parameter(Mandatory = $true)]
        [ValidateSet('Debug', 'Release')]
        [string] $Configuration,

        [Parameter(Mandatory = $true)]
        [object] $Pins,

        [Parameter()]
        [string] $PackageDirectory
    )

    $projectDirectory = Split-Path -Parent $ProjectPath
    if ($Configuration -eq 'Release') {
        $packageRoot = Assert-CanonicalChildPath `
            -Candidate $PackageDirectory `
            -Parent $script:OwnedRoot
    }

    $common = @(
        $ProjectPath,
        '/m:1',
        '/nr:false',
        '/v:minimal',
        "/p:Configuration=$Configuration",
        '/p:Platform=x64',
        '/p:RuntimeIdentifier=win-x64',
        "/p:PublishReadyToRun=$($Configuration -eq 'Release')") +
        @($Pins.Arguments)
    $null = Invoke-CheckedProcess `
        -FilePath $MSBuild `
        -Arguments ($common + @('/t:Restore')) `
        -WorkingDirectory $projectDirectory `
        -TimeoutSeconds 900 `
        -Operation "$Configuration x64 application restore"

    $buildArguments = $common + @(
        '/t:Build',
        '/p:PublishProfile=',
        '/p:PublishTrimmed=false',
        '/p:AppxPackageSigningEnabled=false',
        '/p:AppxBundle=Never')
    if ($Configuration -eq 'Release') {
        $null = New-Item -ItemType Directory -Path $packageRoot
        $buildArguments += @(
            '/p:GenerateAppxPackageOnBuild=true',
            '/p:UapAppxPackageBuildMode=SideloadOnly',
            "/p:AppxPackageDir=$($packageRoot.TrimEnd('\'))\")
    }
    else {
        $buildArguments += '/p:GenerateAppxPackageOnBuild=false'
    }

    $result = Invoke-CheckedProcess `
        -FilePath $MSBuild `
        -Arguments $buildArguments `
        -WorkingDirectory $projectDirectory `
        -TimeoutSeconds 1200 `
        -Operation "$Configuration x64 application build"
    return [pscustomobject]@{
        configuration = $Configuration
        platform = 'x64'
        runtime = 'win-x64'
        command = if ($Configuration -eq 'Release') {
            'msbuild <app-project> /t:Restore,Build /p:Configuration=Release /p:Platform=x64 /p:RuntimeIdentifier=win-x64 /p:GenerateAppxPackageOnBuild=true /p:AppxPackageDir=<owned-root>/release/package'
        }
        else {
            'msbuild <app-project> /t:Restore,Build /p:Configuration=Debug /p:Platform=x64 /p:RuntimeIdentifier=win-x64 /p:GenerateAppxPackageOnBuild=false'
        }
        exitCode = $result.ExitCode
    }
}

function Invoke-ProjectEvaluation {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ProjectPath,

        [Parameter(Mandatory = $true)]
        [ValidateSet('Debug', 'Release')]
        [string] $Configuration
    )

    $dotnet = (Get-Command dotnet.exe -ErrorAction Stop).Source
    $result = Invoke-CheckedProcess `
        -FilePath $dotnet `
        -Arguments @(
            'msbuild',
            $ProjectPath,
            '-nologo',
            "-p:Configuration=$Configuration",
            '-p:Platform=x64',
            '-p:RuntimeIdentifier=win-x64',
            '-getProperty:DefineConstants',
            '-getItem:Compile,Page,None,Content,EmbeddedResource,PRIResource,ProjectReference') `
        -WorkingDirectory (Split-Path -Parent $ProjectPath) `
        -TimeoutSeconds 120 `
        -Operation "$Configuration x64 project evaluation"
    $start = $result.StandardOutput.IndexOf('{')
    $end = $result.StandardOutput.LastIndexOf('}')
    if ($start -lt 0 -or $end -lt $start) {
        throw "$Configuration project evaluation returned no JSON object."
    }

    return ($result.StandardOutput.Substring($start, ($end - $start) + 1) |
        ConvertFrom-Json)
}

function Get-ItemArray {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Items,

        [Parameter(Mandatory = $true)]
        [string] $Name
    )

    $property = $Items.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) {
        return @()
    }

    return @($property.Value)
}

function Get-ItemPropertyValue {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Item,

        [Parameter(Mandatory = $true)]
        [string] $Name
    )

    $property = $Item.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Get-OrdinalSortedUniqueStrings {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [object[]] $Values,

        [Parameter(Mandatory = $true)]
        [string] $Description
    )

    [string[]] $sorted = @($Values | ForEach-Object { [string] $_ })
    [Array]::Sort($sorted, [StringComparer]::Ordinal)
    for ($index = 1; $index -lt $sorted.Count; $index++) {
        if ([string]::Equals(
                $sorted[$index - 1],
                $sorted[$index],
                [StringComparison]::Ordinal)) {
            throw "$Description contains an ordinal duplicate: $($sorted[$index])"
        }
    }

    return $sorted
}

function Test-TextContainsFixtureBoundary {
    param(
        [Parameter()]
        [AllowNull()]
        [object] $Value
    )

    if ($null -eq $Value) {
        return $false
    }

    $text = [string] $Value
    return $text.IndexOf('DebugFixtures', [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
        $text.IndexOf('ModelInspectionScenarios', [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
        $text.IndexOf(
            'GraniteEdgeAI.ModelInspection.Fixtures.csproj',
            [StringComparison]::OrdinalIgnoreCase) -ge 0
}

function Get-FixtureEvaluation {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Evaluation,

        [Parameter(Mandatory = $true)]
        [string] $SourceRoot,

        [Parameter(Mandatory = $true)]
        [string] $ProjectDirectory
    )

    $counts = [ordered]@{
        projectReference = 0
        compile = 0
        page = 0
        none = 0
        content = 0
        embeddedResource = 0
        priResource = 0
        jsonPackageContent = 0
    }
    $identities = New-Object 'System.Collections.Generic.List[string]'
    foreach ($itemType in @(
            'ProjectReference',
            'Compile',
            'Page',
            'None',
            'Content',
            'EmbeddedResource',
            'PRIResource')) {
        foreach ($item in @(Get-ItemArray -Items $Evaluation.Items -Name $itemType)) {
            $values = @(
                Get-ItemPropertyValue -Item $item -Name 'Identity'
                Get-ItemPropertyValue -Item $item -Name 'FullPath'
                Get-ItemPropertyValue -Item $item -Name 'Link'
                Get-ItemPropertyValue -Item $item -Name 'TargetPath')
            if (-not ($values | Where-Object {
                    Test-TextContainsFixtureBoundary -Value $_
                })) {
                continue
            }

            $countKey = $itemType.Substring(0, 1).ToLowerInvariant() +
                $itemType.Substring(1)
            $counts[$countKey]++
            $link = Get-ItemPropertyValue -Item $item -Name 'Link'
            $targetPath = Get-ItemPropertyValue -Item $item -Name 'TargetPath'
            $itemIdentity = Get-ItemPropertyValue -Item $item -Name 'Identity'
            $fullPath = Get-ItemPropertyValue -Item $item -Name 'FullPath'
            $identity = if ($itemType -eq 'ProjectReference') {
                if ([string]::IsNullOrWhiteSpace([string] $fullPath)) {
                    throw 'Evaluated fixture ProjectReference has no FullPath.'
                }

                (Get-RelativeChildPath `
                    -Parent $SourceRoot `
                    -Child ([string] $fullPath)).Replace('\', '/')
            }
            elseif ($itemType -eq 'Compile' -or $itemType -eq 'Page') {
                if ([string]::IsNullOrWhiteSpace([string] $fullPath)) {
                    throw "Evaluated fixture $itemType has no FullPath."
                }

                (Get-RelativeChildPath `
                    -Parent $ProjectDirectory `
                    -Child ([string] $fullPath)).Replace('\', '/')
            }
            elseif (-not [string]::IsNullOrWhiteSpace([string] $link)) {
                [string] $link
            }
            elseif (-not [string]::IsNullOrWhiteSpace([string] $targetPath)) {
                [string] $targetPath
            }
            else {
                [string] $itemIdentity
            }
            $identity = $identity.Replace('\', '/')
            $identities.Add("$itemType|$identity")
            if ($itemType -eq 'Content' -and
                $identity.EndsWith('.json', [StringComparison]::OrdinalIgnoreCase) -and
                $identity.IndexOf(
                    'ModelInspectionScenarios',
                    [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                $counts.jsonPackageContent++
            }
        }
    }

    [string[]] $sortedIdentities = @(Get-OrdinalSortedUniqueStrings `
            -Values @($identities) `
            -Description 'Evaluated fixture identities')

    $identityText = $sortedIdentities -join "`n"
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $identityDigest = ([BitConverter]::ToString($sha.ComputeHash(
            [System.Text.Encoding]::UTF8.GetBytes($identityText)))).Replace(
            '-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }

    return [pscustomobject]@{
        Counts = [pscustomobject] $counts
        Identities = $sortedIdentities
        IdentitySha256 = $identityDigest
        DefineConstants = [string] $Evaluation.Properties.DefineConstants
    }
}

function Assert-EvaluatedClosure {
    param(
        [Parameter(Mandatory = $true)]
        [string] $SourceRoot,

        [Parameter(Mandatory = $true)]
        [object] $Release,

        [Parameter(Mandatory = $true)]
        [object] $DebugEvaluation
    )

    foreach ($property in $Release.Counts.PSObject.Properties) {
        if ([int] $property.Value -ne 0) {
            throw "Release evaluation leaked fixture item $($property.Name)."
        }
    }

    $galleryConstant = 'MODEL_INSPECTION_FIXTURE_GALLERY'
    $releaseConstants = @($Release.DefineConstants.Split(';') | Where-Object {
        $_ -eq $galleryConstant
    })
    $debugConstants = @($DebugEvaluation.DefineConstants.Split(';') | Where-Object {
        $_ -eq $galleryConstant
    })
    if ($releaseConstants.Count -ne 0 -or $debugConstants.Count -ne 1) {
        throw 'Gallery build constant must occur exactly once only in Debug x64 evaluation.'
    }

    $appRoot = Join-Path $SourceRoot 'IBM Granite with TurboQuant (Intel)'
    $fixtureSourceRoots = @(
        'Features\ModelInspection\DebugFixtures',
        'Features\Onboarding\DebugFixtures')
    $expectedCompile = @($fixtureSourceRoots | ForEach-Object {
            $fixtureRoot = Join-Path $appRoot $_
            if (Test-Path -LiteralPath $fixtureRoot -PathType Container) {
                Get-OwnedTreeEntries -Root $fixtureRoot | Where-Object {
                    -not $_.PSIsContainer -and $_.Extension -eq '.cs'
                }
            }
        })
    $expectedPage = @($fixtureSourceRoots | ForEach-Object {
            $fixtureRoot = Join-Path $appRoot $_
            if (Test-Path -LiteralPath $fixtureRoot -PathType Container) {
                Get-OwnedTreeEntries -Root $fixtureRoot | Where-Object {
                    -not $_.PSIsContainer -and $_.Extension -eq '.xaml'
                }
            }
        })
    $scenarioRoot = Join-Path $SourceRoot `
        'tests\TestFixtures\ModelInspectionScenarios'
    $policyPath = Join-Path `
        $scenarioRoot `
        'model-inspection-fixture-coverage-policy.json'
    $schemaFileName = 'model-inspection-fixture.schema.json'
    $policyFileName = 'model-inspection-fixture-coverage-policy.json'
    $policy = Get-Content -LiteralPath $policyPath -Raw | ConvertFrom-Json
    [string[]] $descriptorNames = @(Get-OrdinalSortedUniqueStrings `
            -Values @($policy.fixtures | ForEach-Object { $_.fileName }) `
            -Description 'Coverage-policy descriptor filenames')
    if ($descriptorNames.Count -ne 49) {
        throw 'The coverage policy must declare exactly 49 unique descriptor filenames.'
    }

    [string[]] $expectedJsonNames = @(Get-OrdinalSortedUniqueStrings `
            -Values @($descriptorNames + @($schemaFileName, $policyFileName)) `
            -Description 'Policy-derived JSON filenames')
    [string[]] $physicalJsonNames = @(Get-OrdinalSortedUniqueStrings `
            -Values @(Get-ChildItem `
                -LiteralPath $scenarioRoot `
                -File `
                -Filter '*.json' |
                    ForEach-Object { $_.Name }) `
            -Description 'Physical scenario JSON filenames')
    $jsonSetsMatch = $expectedJsonNames.Count -eq $physicalJsonNames.Count
    for ($index = 0; $jsonSetsMatch -and $index -lt $expectedJsonNames.Count; $index++) {
        $jsonSetsMatch = $expectedJsonNames[$index] -ceq $physicalJsonNames[$index]
    }

    if (-not $jsonSetsMatch) {
        throw 'Physical scenario JSON closure differs from the policy-derived 49+schema+policy set.'
    }

    $expected = [ordered]@{
        projectReference = 1
        compile = $expectedCompile.Count
        page = $expectedPage.Count
        none = 0
        content = $expectedJsonNames.Count
        embeddedResource = 0
        priResource = 0
        jsonPackageContent = $expectedJsonNames.Count
    }
    foreach ($property in $expected.GetEnumerator()) {
        if ([int] $DebugEvaluation.Counts.($property.Key) -ne [int] $property.Value) {
            throw "Debug evaluation count $($property.Key) was " +
                "$($DebugEvaluation.Counts.($property.Key)); expected $($property.Value)."
        }
    }

    if ($expectedJsonNames.Count -ne 51) {
        throw "The policy-derived scenario closure contains $($expectedJsonNames.Count) JSON files; expected 51."
    }

    $expectedIdentities = New-Object 'System.Collections.Generic.List[string]'
    $expectedIdentities.Add(
        'ProjectReference|shared/GraniteEdgeAI.ModelInspection.Fixtures/GraniteEdgeAI.ModelInspection.Fixtures.csproj')
    foreach ($file in $expectedCompile) {
        $relative = (Get-RelativeChildPath `
            -Parent $appRoot `
            -Child $file.FullName).Replace('\', '/')
        $expectedIdentities.Add("Compile|$relative")
    }

    foreach ($file in $expectedPage) {
        $relative = (Get-RelativeChildPath `
            -Parent $appRoot `
            -Child $file.FullName).Replace('\', '/')
        $expectedIdentities.Add("Page|$relative")
    }

    foreach ($fileName in $expectedJsonNames) {
        $expectedIdentities.Add(
            "Content|TestFixtures/ModelInspectionScenarios/$fileName")
    }

    [string[]] $expectedIdentityArray = @(Get-OrdinalSortedUniqueStrings `
            -Values @($expectedIdentities) `
            -Description 'Expected Debug fixture identities')
    $identitiesMatch = $expectedIdentityArray.Count -eq $DebugEvaluation.Identities.Count
    for ($index = 0; $identitiesMatch -and $index -lt $expectedIdentityArray.Count; $index++) {
        $identitiesMatch = $expectedIdentityArray[$index] -ceq $DebugEvaluation.Identities[$index]
    }

    if (-not $identitiesMatch) {
        throw 'Debug evaluated identities differ from exact physical source and policy closure.'
    }

    if ($DebugEvaluation.Identities.Count -le 0 -or
        $DebugEvaluation.IdentitySha256 -notmatch '^[0-9a-f]{64}$') {
        throw 'Debug evaluated identity closure is empty or unhashed.'
    }
}

function Find-ByteSequence {
    param(
        [Parameter(Mandatory = $true)]
        [byte[]] $Haystack,

        [Parameter(Mandatory = $true)]
        [byte[]] $Needle
    )

    if ($Needle.Length -eq 0 -or $Needle.Length -gt $Haystack.Length) {
        return $false
    }

    for ($index = 0; $index -le $Haystack.Length - $Needle.Length; $index++) {
        $matched = $true
        for ($offset = 0; $offset -lt $Needle.Length; $offset++) {
            if ($Haystack[$index + $offset] -ne $Needle[$offset]) {
                $matched = $false
                break
            }
        }

        if ($matched) {
            return $true
        }
    }

    return $false
}

function Get-ResourceTokenHits {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ResourcePath
    )

    $bytes = [System.IO.File]::ReadAllBytes($ResourcePath)
    $hits = New-Object 'System.Collections.Generic.List[string]'
    $tokens = @(
        'fixture',
        'gallery',
        'ModelInspectionScenarios',
        'DebugFixtures',
        'GraniteEdgeAI.ModelInspection.Fixtures')
    $encodings = [ordered]@{
        utf8 = New-Object System.Text.UTF8Encoding($false)
        utf16le = [System.Text.Encoding]::Unicode
        utf16be = [System.Text.Encoding]::BigEndianUnicode
    }
    foreach ($encoding in $encodings.GetEnumerator()) {
        $offsets = if ($encoding.Key -eq 'utf8') { @(0) } else { @(0, 1) }
        foreach ($offset in $offsets) {
            if ($bytes.Length -le $offset) {
                continue
            }

            $text = $encoding.Value.GetString(
                $bytes,
                $offset,
                $bytes.Length - $offset)
            foreach ($token in $tokens) {
                if ($text.IndexOf($token, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                    $hits.Add("resources.pri:$($encoding.Key)-offset${offset}:$token")
                }
            }
        }
    }

    return @($hits | Sort-Object -Unique)
}

function Get-PayloadTokenHits {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo[]] $Files,

        [Parameter(Mandatory = $true)]
        [string] $LayoutRoot
    )

    $tokens = @(
        'fixture',
        'gallery',
        'Fixture gallery',
        'MODEL_INSPECTION_FIXTURE_GALLERY',
        'ModelInspectionFixture',
        'ModelInspectionScenarios',
        'DebugFixtures',
        'GraniteEdgeAI.ModelInspection.Fixtures',
        'FixtureGallery')
    $encodings = [ordered]@{
        utf8 = New-Object System.Text.UTF8Encoding($false)
        utf16le = [System.Text.Encoding]::Unicode
        utf16be = [System.Text.Encoding]::BigEndianUnicode
    }
    $hits = New-Object 'System.Collections.Generic.List[string]'
    foreach ($file in $Files) {
        $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
        $relative = (Get-RelativeChildPath `
            -Parent $LayoutRoot `
            -Child $file.FullName).Replace('\', '/')
        foreach ($encoding in $encodings.GetEnumerator()) {
            $offsets = if ($encoding.Key -eq 'utf8') { @(0) } else { @(0, 1) }
            foreach ($offset in $offsets) {
                if ($bytes.Length -le $offset) {
                    continue
                }

                $text = $encoding.Value.GetString(
                    $bytes,
                    $offset,
                    $bytes.Length - $offset)
                foreach ($token in $tokens) {
                    if ($text.IndexOf($token, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                        $hits.Add("${relative}:$($encoding.Key)-offset${offset}:$token")
                    }
                }
            }
        }
    }

    return @($hits | Sort-Object -Unique)
}

function Invoke-CommittedMetadataInspection {
    param(
        [Parameter(Mandatory = $true)]
        [string] $AssemblyPath,

        [Parameter(Mandatory = $true)]
        [string] $SourceRoot,

        [Parameter(Mandatory = $true)]
        [string] $MSBuild
    )

    $contractProject = Join-Path $SourceRoot `
        'tests\ContractTests\GraniteEdgeAI.ModelInspection.Contracts.Tests\GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj'
    $dotnet = (Get-Command dotnet.exe -ErrorAction Stop).Source
    $pins = Get-BuildPathPins `
        -Configuration Release `
        -Scope 'contract-inspector' `
        -BuildRootParent (Join-Path $SourceRoot '.model-inspection-isolation-build')
    Assert-BuildPathEvaluation `
        -MSBuild $MSBuild `
        -ProjectPath $contractProject `
        -Configuration Release `
        -Pins $pins
    $build = Invoke-CheckedProcess `
        -FilePath $dotnet `
        -Arguments (@(
                'build',
                $contractProject,
                '--configuration',
                'Release',
                '--nologo',
                '--no-incremental',
                '-p:Platform=x64',
                '-p:RuntimeIdentifier=win-x64',
                '-p:PublishReadyToRun=false') +
            @($pins.Arguments)) `
        -WorkingDirectory (Split-Path -Parent $contractProject) `
        -TimeoutSeconds 300 `
        -Operation 'Committed Release metadata inspector build'

    $contractExecutable = Join-Path `
        $pins.BuildRoot `
        'out\GraniteEdgeAI.ModelInspection.Contracts.Tests\GraniteEdgeAI.ModelInspection.Contracts.Tests.exe'
    if (-not (Test-Path -LiteralPath $contractExecutable -PathType Leaf)) {
        throw 'Committed Release metadata inspector executable is missing.'
    }

    $inspectionRoot = Join-Path $script:OwnedRoot 'release\inspection'
    $null = New-Item -ItemType Directory -Path $inspectionRoot
    $resultPath = Join-Path $inspectionRoot 'metadata-result.json'
    if (Test-Path -LiteralPath $resultPath) {
        throw 'Committed Release metadata result must not pre-exist.'
    }

    $fullyQualifiedName =
        'GraniteEdgeAI.ModelInspection.Contracts.Tests.' +
        'ModelInspectionFixtureBuildBoundaryContractTests.' +
        'ReleaseMainAssemblyMetadata_IsFixtureFreeWhenInvokedByIsolationScript'
    $run = Invoke-CheckedProcess `
        -FilePath $contractExecutable `
        -Arguments @(
            '--filter',
            "FullyQualifiedName=$fullyQualifiedName",
            '--minimum-expected-tests',
            '1',
            '--progress',
            'off') `
        -WorkingDirectory (Split-Path -Parent $contractExecutable) `
        -TimeoutSeconds 180 `
        -Operation 'Committed Release metadata inspection' `
        -EnvironmentOverrides @{
            MODEL_INSPECTION_FIXTURE_RELEASE_MAIN_DLL = $AssemblyPath
            MODEL_INSPECTION_FIXTURE_RELEASE_INSPECTION_RESULT = $resultPath
            MODEL_INSPECTION_FIXTURE_ISOLATION_OWNED_ROOT = $script:OwnedRoot
        }
    if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
        throw 'Committed Release metadata inspector did not produce its result.'
    }

    $resultItem = Get-Item -LiteralPath $resultPath -Force
    if (($resultItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'Committed Release metadata result cannot be a reparse point.'
    }

    $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    $expectedRootProperties = @(
        'schemaVersion',
        'status',
        'mainDllSha256',
        'readyToRun',
        'metadataTableCounts',
        'forbiddenMetadataHits')
    if (@(Compare-Object `
            -ReferenceObject $expectedRootProperties `
            -DifferenceObject @($result.PSObject.Properties.Name) `
            -SyncWindow 0).Count -ne 0) {
        throw 'Committed Release metadata result has an unexpected root schema.'
    }

    $expectedCountProperties = @(
        'assemblyReference',
        'typeDefinition',
        'nestedClass',
        'typeReference',
        'exportedType',
        'manifestResource')
    if (@(Compare-Object `
            -ReferenceObject $expectedCountProperties `
            -DifferenceObject @($result.metadataTableCounts.PSObject.Properties.Name) `
            -SyncWindow 0).Count -ne 0) {
        throw 'Committed Release metadata result has an unexpected count schema.'
    }

    if ([int] $result.schemaVersion -ne 1 -or
        $result.status -ne 'passed' -or
        -not [bool] $result.readyToRun -or
        $result.mainDllSha256 -cne (Get-Sha256 -Path $AssemblyPath) -or
        @($result.forbiddenMetadataHits).Count -ne 0) {
        throw 'Committed Release metadata result did not prove fixture-free ReadyToRun metadata.'
    }

    return [pscustomobject]@{
        Result = $result
        Provenance = [pscustomobject]@{
            configuration = 'Release'
            platform = 'x64'
            runtime = 'win-x64'
            command = 'contract-test <release-main-assembly-metadata-filter> <owned-root>'
            exitCode = $run.ExitCode
        }
        BuildExitCode = $build.ExitCode
        ContractExecutable = $contractExecutable
    }
}

function ConvertFrom-SafePackageSegment {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Segment,

        [Parameter(Mandatory = $true)]
        [string] $EntryName
    )

    for ($index = 0; $index -lt $Segment.Length; $index++) {
        if ($Segment[$index] -ne '%') {
            continue
        }

        if ($index + 2 -ge $Segment.Length -or
            $Segment[$index + 1] -notmatch '^[0-9A-Fa-f]$' -or
            $Segment[$index + 2] -notmatch '^[0-9A-Fa-f]$') {
            throw "Package contains a malformed URI escape: $EntryName"
        }

        $index += 2
    }

    try {
        $decoded = [System.Uri]::UnescapeDataString($Segment)
    }
    catch {
        throw "Package contains an invalid URI-escaped segment: $EntryName"
    }

    if ([string]::IsNullOrEmpty($decoded) -or
        $decoded -eq '.' -or
        $decoded -eq '..' -or
        $decoded.EndsWith(' ', [StringComparison]::Ordinal) -or
        $decoded.EndsWith('.', [StringComparison]::Ordinal) -or
        [System.IO.Path]::IsPathRooted($decoded) -or
        $decoded.IndexOfAny([char[]] @('/', '\', ':')) -ge 0) {
        throw "Package URI decoding produced an unsafe segment: $EntryName"
    }

    foreach ($character in $decoded.ToCharArray()) {
        if ([char]::IsControl($character) -or
            $character -eq [char] 0xFFFD -or
            [Array]::IndexOf(
                [System.IO.Path]::GetInvalidFileNameChars(),
                $character) -ge 0) {
            throw "Package URI decoding produced an invalid filename character: $EntryName"
        }
    }

    $deviceBaseName = $decoded.Split('.')[0]
    if ($deviceBaseName -match `
        '^(?i:CON|PRN|AUX|NUL|COM(?:[1-9]|\u00B9|\u00B2|\u00B3)|LPT(?:[1-9]|\u00B9|\u00B2|\u00B3))$') {
        throw "Package URI decoding produced a reserved device basename: $EntryName"
    }

    return $decoded
}

function Expand-SafePackage {
    param(
        [Parameter(Mandatory = $true)]
        [string] $PackagePath,

        [Parameter(Mandatory = $true)]
        [string] $LayoutRoot
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $planned = New-Object 'System.Collections.Generic.List[object]'
        $destinations = New-Object `
            'System.Collections.Generic.HashSet[string]' `
            ([StringComparer]::OrdinalIgnoreCase)
        foreach ($entry in $archive.Entries) {
            $entryName = $entry.FullName
            if ([string]::IsNullOrWhiteSpace($entryName) -or
                $entryName.Contains('\') -or
                $entryName.Contains(':') -or
                $entryName.StartsWith('/', [StringComparison]::Ordinal) -or
                [System.IO.Path]::IsPathRooted($entryName)) {
                throw "Package contains an unsafe entry path: $entryName"
            }

            $rawSegments = @($entryName.Split('/') | Where-Object {
                -not [string]::IsNullOrEmpty($_)
            })
            if ($rawSegments.Count -eq 0 -or
                @($rawSegments | Where-Object { $_ -eq '.' -or $_ -eq '..' }).Count -ne 0) {
                throw "Package contains a traversal entry path: $entryName"
            }

            $segments = @($rawSegments | ForEach-Object {
                ConvertFrom-SafePackageSegment `
                    -Segment $_ `
                    -EntryName $entryName
            })

            $relativePath = [string]::Join(
                [System.IO.Path]::DirectorySeparatorChar,
                $segments)
            $destination = Assert-CanonicalChildPath `
                -Candidate (Join-Path $LayoutRoot $relativePath) `
                -Parent $LayoutRoot
            if (-not $destinations.Add($destination)) {
                throw "Package contains a duplicate destination: $entryName"
            }

            $unixType = ($entry.ExternalAttributes -shr 16) -band 0xF000
            $windowsAttributes = $entry.ExternalAttributes -band 0xFFFF
            if ($unixType -eq 0xA000 -or
                ($windowsAttributes -band [int][System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Package contains a link/reparse entry: $entryName"
            }

            $planned.Add([pscustomobject]@{
                Entry = $entry
                Destination = $destination
                IsDirectory = $entryName.EndsWith(
                    '/',
                    [StringComparison]::Ordinal)
            })
        }

        if ($planned.Count -eq 0) {
            throw 'The current-run package contains no entries.'
        }

        foreach ($item in $planned) {
            if ($item.IsDirectory) {
                $null = New-Item -ItemType Directory -Path $item.Destination -Force
                continue
            }

            $parent = Split-Path -Parent $item.Destination
            $null = New-Item -ItemType Directory -Path $parent -Force
            $inputStream = $item.Entry.Open()
            $outputStream = New-Object System.IO.FileStream(
                $item.Destination,
                [System.IO.FileMode]::CreateNew,
                [System.IO.FileAccess]::Write,
                [System.IO.FileShare]::None)
            try {
                $inputStream.CopyTo($outputStream)
            }
            finally {
                $outputStream.Dispose()
                $inputStream.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}

function New-SyntheticPackageArchive {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string[]] $EntryNames
    )

    $path = Assert-CanonicalChildPath -Candidate $Path -Parent $script:OwnedRoot
    if (Test-Path -LiteralPath $path) {
        throw 'Synthetic package archive must be fresh.'
    }

    Add-Type -AssemblyName System.IO.Compression
    $stream = New-Object System.IO.FileStream(
        $path,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::None)
    $archive = New-Object System.IO.Compression.ZipArchive(
        $stream,
        [System.IO.Compression.ZipArchiveMode]::Create,
        $false)
    try {
        foreach ($entryName in $EntryNames) {
            $entry = $archive.CreateEntry($entryName)
            $entryStream = $entry.Open()
            try {
                $bytes = [System.Text.Encoding]::UTF8.GetBytes('synthetic-package-entry')
                $entryStream.Write($bytes, 0, $bytes.Length)
            }
            finally {
                $entryStream.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
        $stream.Dispose()
    }
}

function Assert-SafePackageExtractionContract {
    $contractRoot = Assert-CanonicalChildPath `
        -Candidate (Join-Path $script:OwnedRoot 'package-extraction-contract') `
        -Parent $script:OwnedRoot
    $null = New-Item -ItemType Directory -Path $contractRoot

    $positivePackage = Join-Path $contractRoot 'positive.msix'
    $positiveLayout = Join-Path $contractRoot 'positive-layout'
    New-SyntheticPackageArchive `
        -Path $positivePackage `
        -EntryNames @('IBM Granite with TurboQuant %28Intel%29.dll')
    $null = New-Item -ItemType Directory -Path $positiveLayout
    Expand-SafePackage -PackagePath $positivePackage -LayoutRoot $positiveLayout
    $decodedPath = Join-Path `
        $positiveLayout `
        'IBM Granite with TurboQuant (Intel).dll'
    $literalPath = Join-Path `
        $positiveLayout `
        'IBM Granite with TurboQuant %28Intel%29.dll'
    if (-not (Test-Path -LiteralPath $decodedPath -PathType Leaf) -or
        (Test-Path -LiteralPath $literalPath)) {
        throw 'Safe package extraction did not decode one strict URI-escaped segment.'
    }

    $unsafeEntries = @(
        '%2e%2e/escape.txt',
        'safe/%2Fescape.txt',
        'safe/%5cescape.txt',
        'safe/%3Aescape.txt',
        'safe/%00escape.txt',
        'safe/%0Aescape.txt',
        'safe/%ZZescape.txt',
        'safe/CON.txt',
        'safe/trailing.%20',
        'safe/COM%C2%B9',
        'safe/com%C2%B9.txt',
        'safe/COM%C2%B2',
        'safe/com%C2%B2.txt',
        'safe/COM%C2%B3',
        'safe/com%C2%B3.txt',
        'safe/LPT%C2%B9',
        'safe/lpt%C2%B9.txt',
        'safe/LPT%C2%B2',
        'safe/lpt%C2%B2.txt',
        'safe/LPT%C2%B3',
        'safe/lpt%C2%B3.txt')
    for ($index = 0; $index -lt $unsafeEntries.Count; $index++) {
        $packagePath = Join-Path $contractRoot "unsafe-$index.msix"
        $layoutPath = Join-Path $contractRoot "unsafe-$index-layout"
        New-SyntheticPackageArchive `
            -Path $packagePath `
            -EntryNames @($unsafeEntries[$index])
        $null = New-Item -ItemType Directory -Path $layoutPath
        $failedClosed = $false
        try {
            Expand-SafePackage -PackagePath $packagePath -LayoutRoot $layoutPath
        }
        catch {
            $failedClosed = $true
        }

        if (-not $failedClosed) {
            throw "Unsafe URI-escaped package entry was accepted: $($unsafeEntries[$index])"
        }
    }
}

function Format-SafeIsolationHitDiagnostics {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [string[]] $Hits
    )

    $canonicalTokens = New-Object `
        'System.Collections.Generic.HashSet[string]' `
        ([StringComparer]::Ordinal)
    foreach ($token in @(
            'fixture',
            'gallery',
            'Fixture gallery',
            'MODEL_INSPECTION_FIXTURE_GALLERY',
            'ModelInspectionFixture',
            'ModelInspectionScenarios',
            'DebugFixtures',
            'GraniteEdgeAI.ModelInspection.Fixtures',
            'FixtureGallery')) {
        $null = $canonicalTokens.Add($token)
    }

    $distinct = New-Object `
        'System.Collections.Generic.HashSet[string]' `
        ([StringComparer]::Ordinal)
    foreach ($hit in $Hits) {
        if ([string]::IsNullOrEmpty($hit) -or $hit.Length -gt 256) {
            throw 'Isolation-hit diagnostic identifier is empty or overlong.'
        }

        foreach ($character in $hit.ToCharArray()) {
            if ([char]::IsControl($character)) {
                throw 'Isolation-hit diagnostic identifier contains a control character.'
            }
        }

        $parts = @($hit.Split([char] ':'))
        if ($parts.Count -ne 3) {
            throw 'Isolation-hit diagnostic identifier has noncanonical separators.'
        }

        $relativePath = $parts[0]
        $encodingAndOffset = $parts[1]
        $token = $parts[2]
        if ([string]::IsNullOrEmpty($relativePath) -or
            [System.IO.Path]::IsPathRooted($relativePath) -or
            $relativePath.IndexOf('\', [StringComparison]::Ordinal) -ge 0 -or
            $relativePath.StartsWith('/', [StringComparison]::Ordinal) -or
            $relativePath.EndsWith('/', [StringComparison]::Ordinal)) {
            throw 'Isolation-hit diagnostic identifier has an unsafe relative path.'
        }

        $segments = @($relativePath.Split([char] '/'))
        if ($segments.Count -eq 0) {
            throw 'Isolation-hit diagnostic identifier has an empty relative path.'
        }

        foreach ($segment in $segments) {
            if ([string]::IsNullOrEmpty($segment) -or
                $segment -eq '.' -or
                $segment -eq '..' -or
                $segment.EndsWith(' ', [StringComparison]::Ordinal) -or
                $segment.EndsWith('.', [StringComparison]::Ordinal)) {
                throw 'Isolation-hit diagnostic identifier has an unsafe path segment.'
            }
        }

        if ($encodingAndOffset -notmatch `
            '^(?:utf8-offset0|utf16le-offset[01]|utf16be-offset[01])$' -or
            -not $canonicalTokens.Contains($token)) {
            throw 'Isolation-hit diagnostic identifier has a noncanonical encoding or token.'
        }

        $null = $distinct.Add($hit)
    }

    [string[]] $ordered = @($distinct)
    [Array]::Sort($ordered, [StringComparer]::Ordinal)
    $display = New-Object 'System.Collections.Generic.List[string]'
    $displayCount = [Math]::Min(16, $ordered.Length)
    for ($index = 0; $index -lt $displayCount; $index++) {
        $display.Add($ordered[$index])
    }

    return [string]::Join('; ', $display.ToArray())
}

function Assert-SafeIsolationHitDiagnosticContract {
    $formatted = Format-SafeIsolationHitDiagnostics -Hits @(
        'z.dll:utf8-offset0:fixture',
        'a/resources.pri:utf16le-offset1:gallery',
        'z.dll:utf8-offset0:fixture')
    if ($formatted -cne (
            'a/resources.pri:utf16le-offset1:gallery; ' +
            'z.dll:utf8-offset0:fixture')) {
        throw 'Isolation-hit diagnostics are not ordinal, distinct, and canonical.'
    }

    $overflow = @(0..16 | ForEach-Object {
        'p{0:d2}.dll:utf8-offset0:fixture' -f $_
    })
    $bounded = Format-SafeIsolationHitDiagnostics -Hits $overflow
    if (@($bounded -split '; ').Count -ne 16 -or
        $bounded.IndexOf('p15.dll', [StringComparison]::Ordinal) -lt 0 -or
        $bounded.IndexOf('p16.dll', [StringComparison]::Ordinal) -ge 0) {
        throw 'Isolation-hit diagnostics did not enforce the 16-identifier bound.'
    }

    $unsafeSets = @(
        @('C:/escape.dll:utf8-offset0:fixture'),
        @('safe\escape.dll:utf8-offset0:fixture'),
        @('../escape.dll:utf8-offset0:fixture'),
        @("safe`nname.dll:utf8-offset0:fixture"),
        @('safe.dll:utf32-offset0:fixture'),
        @('safe.dll:utf8-offset1:fixture'),
        @('safe.dll:utf8-offset0:not-canonical'),
        @(('a' * 240) + '.dll:utf8-offset0:fixture'),
        @(@(0..15 | ForEach-Object {
                    'q{0:d2}.dll:utf8-offset0:fixture' -f $_
                }) + 'unsafe\hidden.dll:utf8-offset0:fixture'))
    foreach ($unsafeSet in $unsafeSets) {
        $failedClosed = $false
        try {
            $null = Format-SafeIsolationHitDiagnostics -Hits $unsafeSet
        }
        catch {
            $failedClosed = $true
        }

        if (-not $failedClosed) {
            throw 'Unsafe or overlong isolation-hit diagnostics were accepted.'
        }
    }
}

function Assert-OwnedBaseContract {
    $expected = [System.IO.Path]::GetFullPath((Join-Path `
        $env:SystemDrive `
        'mi-release-isolation'))
    if (-not [string]::Equals(
            $script:OwnedBase,
            $expected,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Owned base is not the exact forbidden-token-free isolation base.'
    }

    $accepted = Assert-SafeIsolationBase -Candidate $expected
    if (-not [string]::Equals(
            $accepted,
            $expected,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Owned-base validation changed the canonical expected path.'
    }

    foreach ($unsafeLeaf in @(
            'mi-fixture-isolation',
            'mi-gallery-isolation')) {
        $failedClosed = $false
        try {
            $null = Assert-SafeIsolationBase -Candidate (Join-Path `
                $env:SystemDrive `
                $unsafeLeaf)
        }
        catch {
            $failedClosed = $true
        }

        if (-not $failedClosed) {
            throw 'Owned-base validation accepted a forbidden-token mutation.'
        }
    }
}

function Inspect-ReleasePackage {
    param(
        [Parameter(Mandatory = $true)]
        [string] $PackagePath,

        [Parameter(Mandatory = $true)]
        [string] $SourceRoot,

        [Parameter(Mandatory = $true)]
        [string] $MSBuild
    )

    $packagePath = Assert-CanonicalChildPath `
        -Candidate $PackagePath `
        -Parent $script:OwnedRoot
    $expectedPackagePath = Join-Path `
        $script:OwnedRoot `
        'release\package\application.msix'
    if (-not [string]::Equals(
            $packagePath,
            [System.IO.Path]::GetFullPath($expectedPackagePath),
            [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
        throw 'The exact current-run AppxPackageOutput does not exist.'
    }

    $packageItem = Get-Item -LiteralPath $packagePath -Force
    if (($packageItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'The exact current-run AppxPackageOutput cannot be a reparse point.'
    }

    $layoutRoot = Join-Path $script:OwnedRoot 'release\layout'
    if (Test-Path -LiteralPath $layoutRoot) {
        throw 'Fresh package layout already exists.'
    }

    $null = New-Item -ItemType Directory -Path $layoutRoot
    Expand-SafePackage -PackagePath $packagePath -LayoutRoot $layoutRoot
    $layoutEntries = @(Get-OwnedTreeEntries -Root $layoutRoot)
    $layoutFiles = @($layoutEntries | Where-Object { -not $_.PSIsContainer })
    if ($layoutFiles.Count -eq 0) {
        throw 'The current-run package layout is empty.'
    }

    $relativePaths = @($layoutFiles | ForEach-Object {
        (Get-RelativeChildPath -Parent $layoutRoot -Child $_.FullName).Replace('\', '/')
    } | Sort-Object)
    $pathHits = @($relativePaths | Where-Object {
        $_ -match '(?i)(fixture|gallery|ModelInspectionScenarios|DebugFixtures|GraniteEdgeAI\.ModelInspection\.Fixtures)'
    })

    $expectedMainPath = 'IBM Granite with TurboQuant (Intel).dll'
    $mainFiles = @($layoutFiles | Where-Object {
        [string]::Equals(
            (Get-RelativeChildPath -Parent $layoutRoot -Child $_.FullName).Replace('\', '/'),
            $expectedMainPath,
            [StringComparison]::Ordinal)
    })
    if ($mainFiles.Count -ne 1) {
        throw 'The exact packaged application DLL PackagePath was not present once.'
    }

    $resourceFiles = @($layoutFiles | Where-Object {
        [string]::Equals(
            (Get-RelativeChildPath -Parent $layoutRoot -Child $_.FullName).Replace('\', '/'),
            'resources.pri',
            [StringComparison]::Ordinal)
    })
    if ($resourceFiles.Count -ne 1) {
        throw 'The exact packaged resources.pri PackagePath was not present once.'
    }

    $metadataInspection = Invoke-CommittedMetadataInspection `
        -AssemblyPath $mainFiles[0].FullName `
        -SourceRoot $SourceRoot `
        -MSBuild $MSBuild
    $metadata = $metadataInspection.Result

    $tokenHits = @(
        @(Get-ResourceTokenHits -ResourcePath $resourceFiles[0].FullName) +
        @(Get-PayloadTokenHits -Files $layoutFiles -LayoutRoot $layoutRoot) |
            Sort-Object -Unique)
    $metadataHits = @($metadata.forbiddenMetadataHits)
    if ($pathHits.Count -ne 0 -or
        $tokenHits.Count -ne 0 -or
        $metadataHits.Count -ne 0) {
        $tokenDiagnostics = Format-SafeIsolationHitDiagnostics -Hits $tokenHits
        throw "Release package isolation failed. Path hits=$($pathHits.Count), " +
            "token hits=$($tokenHits.Count), metadata hits=$($metadataHits.Count). " +
            "Token diagnostics=[$tokenDiagnostics]."
    }

    return [pscustomobject]@{
        PackagePath = (Get-RelativeChildPath `
            -Parent $script:OwnedRoot `
            -Child $packagePath).Replace('\', '/')
        PackageSha256 = Get-Sha256 -Path $packagePath
        LayoutPath = (Get-RelativeChildPath `
            -Parent $script:OwnedRoot `
            -Child $layoutRoot).Replace('\', '/')
        MainDll = [pscustomobject]@{
            packagePath = $expectedMainPath
            sha256 = Get-Sha256 -Path $mainFiles[0].FullName
            readyToRun = [bool] $metadata.readyToRun
            metadataTableCounts = $metadata.metadataTableCounts
        }
        ResourcesPri = [pscustomobject]@{
            packagePath = 'resources.pri'
            sha256 = Get-Sha256 -Path $resourceFiles[0].FullName
        }
        ScannedFileCount = $layoutFiles.Count
        ForbiddenPathHits = @($pathHits)
        ForbiddenTokenHits = @($tokenHits)
        ForbiddenMetadataHits = @($metadataHits)
        InspectorProvenance = $metadataInspection.Provenance
        ContractExecutable = $metadataInspection.ContractExecutable
    }
}

function Get-AllowedEvidenceDirectory {
    $repositoryRoot = Get-RepositoryRoot
    $repositoryItem = Get-Item -LiteralPath $repositoryRoot -Force
    if (($repositoryItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'Repository root cannot be a reparse point for evidence output.'
    }

    $current = $repositoryRoot
    foreach ($segment in @(
            'TestResults',
            'ModelInspectionFixtures',
            'ReleaseIsolation')) {
        $next = Assert-CanonicalChildPath `
            -Candidate (Join-Path $current $segment) `
            -Parent $current
        if (-not (Test-Path -LiteralPath $next)) {
            $null = New-Item -ItemType Directory -Path $next
        }

        $item = Get-Item -LiteralPath $next -Force
        if (-not $item.PSIsContainer -or
            ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Evidence directory must be a physical directory: $segment"
        }

        $current = $next
    }

    return $current
}

function Resolve-EvidencePathContract {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Candidate
    )

    $repositoryRoot = Get-RepositoryRoot
    $current = $repositoryRoot
    $repositoryItem = Get-Item -LiteralPath $repositoryRoot -Force
    if (($repositoryItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'Repository root cannot be a reparse point for evidence output.'
    }

    foreach ($segment in @(
            'TestResults',
            'ModelInspectionFixtures',
            'ReleaseIsolation')) {
        $current = Assert-CanonicalChildPath `
            -Candidate (Join-Path $current $segment) `
            -Parent $current
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -LiteralPath $current -Force
            if (-not $item.PSIsContainer -or
                ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Evidence path chain contains a non-physical directory: $segment"
            }
        }
    }

    $absolute = Assert-CanonicalChildPath -Candidate $Candidate -Parent $current
    if (-not [string]::Equals(
            (Split-Path -Parent $absolute),
            $current,
            [StringComparison]::OrdinalIgnoreCase) -or
        -not [string]::Equals(
            [System.IO.Path]::GetExtension($absolute),
            '.json',
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'EvidencePath must be a direct .json child of the approved evidence directory.'
    }

    if (Test-Path -LiteralPath $absolute) {
        $item = Get-Item -LiteralPath $absolute -Force
        if ($item.PSIsContainer -or
            ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'EvidencePath cannot be a directory or reparse point.'
        }
    }

    return $absolute
}

function Clear-PreviousEvidence {
    param(
        [Parameter(Mandatory = $true)]
        [string] $AbsoluteEvidencePath
    )

    $validated = Resolve-EvidencePathContract -Candidate $AbsoluteEvidencePath
    if (-not (Test-Path -LiteralPath $validated)) {
        return
    }

    $item = Get-Item -LiteralPath $validated -Force
    if ($item.PSIsContainer -or
        ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'A previous EvidencePath is not a physical file.'
    }

    [System.IO.File]::Delete($validated)
    if (Test-Path -LiteralPath $validated) {
        throw 'Previous EvidencePath invalidation did not complete.'
    }
}

function ConvertTo-EvidenceBytes {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Evidence
    )

    $json = (($Evidence | ConvertTo-Json -Depth 12) + "`n").Replace(
        "`r`n",
        "`n").Replace("`r", "`n")
    $encoding = New-Object System.Text.UTF8Encoding($false)
    return ,$encoding.GetBytes($json)
}

function Get-BytesSha256 {
    param(
        [Parameter(Mandatory = $true)]
        [byte[]] $Bytes
    )

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace(
            '-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Write-BytesCreateNew {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [byte[]] $Bytes
    )

    $stream = New-Object System.IO.FileStream(
        $Path,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::None)
    try {
        $stream.Write($Bytes, 0, $Bytes.Length)
        $stream.Flush($true)
    }
    finally {
        $stream.Dispose()
    }
}

function Invoke-CommittedEvidenceValidation {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ContractExecutable,

        [Parameter(Mandatory = $true)]
        [byte[]] $EvidenceBytes
    )

    $contractExecutable = Assert-CanonicalChildPath `
        -Candidate $ContractExecutable `
        -Parent $script:OwnedRoot
    if (-not (Test-Path -LiteralPath $contractExecutable -PathType Leaf)) {
        throw 'Committed evidence validator executable is missing.'
    }

    if ($EvidenceBytes.Length -le 0 -or $EvidenceBytes.Length -gt (4 * 1024 * 1024)) {
        throw 'Canonical release-isolation evidence bytes are empty or unbounded.'
    }

    $ownedEvidencePath = Assert-CanonicalChildPath `
        -Candidate (Join-Path $script:OwnedRoot 'release-isolation-evidence.json') `
        -Parent $script:OwnedRoot
    if (-not [string]::Equals(
            (Split-Path -Parent $ownedEvidencePath),
            $script:OwnedRoot,
            [StringComparison]::OrdinalIgnoreCase) -or
        (Test-Path -LiteralPath $ownedEvidencePath)) {
        throw 'Owned evidence-validation input must be a fresh direct child.'
    }

    Write-BytesCreateNew -Path $ownedEvidencePath -Bytes $EvidenceBytes
    $receiptPath = Assert-CanonicalChildPath `
        -Candidate (Join-Path `
            $script:OwnedRoot `
            'release-isolation-evidence-validation.json') `
        -Parent $script:OwnedRoot
    if (-not [string]::Equals(
            (Split-Path -Parent $receiptPath),
            $script:OwnedRoot,
            [StringComparison]::OrdinalIgnoreCase) -or
        (Test-Path -LiteralPath $receiptPath)) {
        throw 'Evidence-validation receipt must be a fresh direct owned child.'
    }

    $fullyQualifiedName =
        'GraniteEdgeAI.ModelInspection.Contracts.Tests.' +
        'ModelInspectionFixtureBuildBoundaryContractTests.' +
        'ReleaseIsolationEvidence_IsCanonicalWhenInvokedByIsolationScript'
    $run = Invoke-CheckedProcess `
        -FilePath $contractExecutable `
        -Arguments @(
            '--filter',
            "FullyQualifiedName=$fullyQualifiedName",
            '--minimum-expected-tests',
            '1',
            '--progress',
            'off') `
        -WorkingDirectory (Split-Path -Parent $contractExecutable) `
        -TimeoutSeconds 180 `
        -Operation 'Committed release-isolation evidence validation' `
        -EnvironmentOverrides @{
            MODEL_INSPECTION_FIXTURE_RELEASE_EVIDENCE = $ownedEvidencePath
            MODEL_INSPECTION_FIXTURE_RELEASE_EVIDENCE_RECEIPT = $receiptPath
            MODEL_INSPECTION_FIXTURE_ISOLATION_OWNED_ROOT = $script:OwnedRoot
        }

    if (-not (Test-Path -LiteralPath $receiptPath -PathType Leaf)) {
        throw 'Committed evidence validator did not create its receipt.'
    }

    $receiptItem = Get-Item -LiteralPath $receiptPath -Force
    if (($receiptItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 -or
        $receiptItem.Length -le 0 -or
        $receiptItem.Length -gt 4096) {
        throw 'Committed evidence-validation receipt is not a bounded physical file.'
    }

    [byte[]] $receiptBytes = [System.IO.File]::ReadAllBytes($receiptPath)
    if (($receiptBytes.Length -ge 3 -and
            $receiptBytes[0] -eq 0xEF -and
            $receiptBytes[1] -eq 0xBB -and
            $receiptBytes[2] -eq 0xBF) -or
        $receiptBytes[$receiptBytes.Length - 1] -ne 0x0A) {
        throw 'Committed evidence-validation receipt is not BOMless UTF-8 with a final LF.'
    }

    $receiptEncoding = New-Object System.Text.UTF8Encoding($false, $true)
    $receiptText = $receiptEncoding.GetString($receiptBytes)
    if ($receiptText.IndexOf("`r", [StringComparison]::Ordinal) -ge 0 -or
        $receiptText.EndsWith("`n`n", [StringComparison]::Ordinal)) {
        throw 'Committed evidence-validation receipt must use LF with exactly one final LF.'
    }

    $receipt = $receiptText | ConvertFrom-Json
    $expectedReceiptProperties = @(
        'schemaVersion',
        'status',
        'evidenceSha256')
    if (@(Compare-Object `
            -ReferenceObject $expectedReceiptProperties `
            -DifferenceObject @($receipt.PSObject.Properties.Name) `
            -SyncWindow 0).Count -ne 0 -or
        [int] $receipt.schemaVersion -ne 1 -or
        $receipt.status -cne 'passed' -or
        $receipt.evidenceSha256 -cne (Get-BytesSha256 -Bytes $EvidenceBytes)) {
        throw 'Committed evidence-validation receipt did not bind the exact evidence bytes.'
    }

    [byte[]] $validatedBytes = [System.IO.File]::ReadAllBytes($ownedEvidencePath)
    if ($validatedBytes.Length -ne $EvidenceBytes.Length) {
        throw 'Committed evidence validation changed the canonical evidence bytes.'
    }

    for ($index = 0; $index -lt $EvidenceBytes.Length; $index++) {
        if ($validatedBytes[$index] -ne $EvidenceBytes[$index]) {
            throw 'Committed evidence validation changed the canonical evidence bytes.'
        }
    }

    return [pscustomobject]@{
        Bytes = $validatedBytes
        ExitCode = $run.ExitCode
    }
}

function Publish-EvidenceBytes {
    param(
        [Parameter(Mandatory = $true)]
        [byte[]] $EvidenceBytes,

        [Parameter()]
        [AllowNull()]
        [string] $AbsoluteEvidencePath
    )

    if ([string]::IsNullOrWhiteSpace($AbsoluteEvidencePath)) {
        $encoding = New-Object System.Text.UTF8Encoding($false, $true)
        [Console]::Out.Write($encoding.GetString($EvidenceBytes))
        return
    }

    $absoluteEvidencePath = Resolve-EvidencePathContract `
        -Candidate $AbsoluteEvidencePath
    $allowedDirectory = Get-AllowedEvidenceDirectory
    if (Test-Path -LiteralPath $absoluteEvidencePath) {
        throw 'EvidencePath was recreated before successful publication.'
    }

    $temporaryPath = Assert-CanonicalChildPath `
        -Candidate (Join-Path `
            $allowedDirectory `
            ".$([System.IO.Path]::GetFileName($absoluteEvidencePath)).$($script:RunId).tmp") `
        -Parent $allowedDirectory
    try {
        Write-BytesCreateNew -Path $temporaryPath -Bytes $EvidenceBytes
        [System.IO.File]::Move($temporaryPath, $absoluteEvidencePath)
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath -PathType Leaf) {
            $null = Assert-CanonicalChildPath `
                -Candidate $temporaryPath `
                -Parent $allowedDirectory
            [System.IO.File]::Delete($temporaryPath)
        }
    }

    Write-Output $absoluteEvidencePath
}

[byte[]] $pendingEvidenceBytes = $null
$absoluteEvidencePath = $null
try {
    if ($WhatIfContract -and $EvaluateContract) {
        throw 'WhatIfContract and EvaluateContract are mutually exclusive.'
    }

    if (-not [string]::IsNullOrWhiteSpace($EvidencePath)) {
        $absoluteEvidencePath = Resolve-EvidencePathContract -Candidate $EvidencePath
        if (-not $WhatIfContract -and -not $EvaluateContract) {
            Clear-PreviousEvidence -AbsoluteEvidencePath $absoluteEvidencePath
        }
    }

    $script:OwnedBase = Assert-SafeIsolationBase -Candidate $script:OwnedBase
    $null = New-Item -ItemType Directory -Path $script:OwnedBase -Force
    $baseItem = Get-Item -LiteralPath $script:OwnedBase -Force
    if (($baseItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'Owned base cannot be a reparse point.'
    }

    $null = Assert-CanonicalChildPath `
        -Candidate $script:OwnedRoot `
        -Parent $script:OwnedBase
    $null = New-Item -ItemType Directory -Path $script:OwnedRoot
    Set-Content `
        -LiteralPath $script:OwnerMarkerPath `
        -Value $script:RunId `
        -Encoding Ascii `
        -NoNewline

    if ($WhatIfContract) {
        Assert-OwnedBaseContract
        $probeDirectory = Join-Path $script:OwnedRoot 'what-if-contract'
        $null = New-Item -ItemType Directory -Path $probeDirectory
        Set-Content `
            -LiteralPath (Join-Path $probeDirectory 'probe.txt') `
            -Value 'owned-root-lifecycle' `
            -Encoding Ascii `
            -NoNewline
        $canonicalRoot = Assert-OwnedRoot -Root $script:OwnedRoot
        $probeProcess = Invoke-CheckedProcess `
            -FilePath $env:ComSpec `
            -Arguments @('/d', '/c', 'echo owned-root-process') `
            -WorkingDirectory $script:OwnedRoot `
            -TimeoutSeconds 10 `
            -Operation 'What-if process contract'
        if ($probeProcess.StandardOutput.Trim() -ne 'owned-root-process') {
            throw 'What-if process contract returned unexpected output.'
        }

        Assert-SafePackageExtractionContract
        Assert-SafeIsolationHitDiagnosticContract

        [pscustomobject][ordered]@{
            schemaVersion = 1
            status = 'what-if-contract'
            generatedUtc = '1970-01-01T00:00:00.0000000Z'
            sourceCommit = ('0' * 40)
            sourceSnapshotSha256 = ('0' * 64)
            sourceSnapshotFileCount = 1
            ownedRoot = '<owned-root>'
            configuration = 'Release'
            platform = 'x64'
            runtime = 'win-x64'
            packagePath = 'release/package/application.msix'
            packageSha256 = ('0' * 64)
            layoutPath = 'release/layout'
            mainDll = [pscustomobject][ordered]@{
                packagePath = 'IBM Granite with TurboQuant (Intel).dll'
                sha256 = ('0' * 64)
                readyToRun = $false
                metadataTableCounts = [pscustomobject][ordered]@{
                    assemblyReference = 0
                    typeDefinition = 0
                    nestedClass = 0
                    typeReference = 0
                    exportedType = 0
                    manifestResource = 0
                }
            }
            resourcesPri = [pscustomobject][ordered]@{
                packagePath = 'resources.pri'
                sha256 = ('0' * 64)
            }
            scannedFileCount = 1
            releaseForbiddenPathHits = @()
            releaseForbiddenTokenHits = @()
            releaseForbiddenMetadataHits = @()
            debugEvaluatedCounts = [pscustomobject][ordered]@{
                projectReference = 1
                compile = 1
                page = 0
                none = 0
                content = 51
                embeddedResource = 0
                priResource = 0
                jsonPackageContent = 51
            }
            debugEvaluatedIdentityCount = 53
            debugExactIdentitiesSha256 = ('0' * 64)
            releaseEvaluatedCounts = [pscustomobject][ordered]@{
                projectReference = 0
                compile = 0
                page = 0
                none = 0
                content = 0
                embeddedResource = 0
                priResource = 0
                jsonPackageContent = 0
            }
            buildProvenance = @(
                [pscustomobject][ordered]@{
                    configuration = 'Release'
                    platform = 'x64'
                    runtime = 'win-x64'
                    command = 'msbuild <app-project> /t:Restore,Build /p:Configuration=Release /p:Platform=x64 /p:RuntimeIdentifier=win-x64 /p:GenerateAppxPackageOnBuild=true /p:AppxPackageDir=<owned-root>/release/package'
                    exitCode = 0
                },
                [pscustomobject][ordered]@{
                    configuration = 'Debug'
                    platform = 'x64'
                    runtime = 'win-x64'
                    command = 'msbuild <app-project> /t:Restore,Build /p:Configuration=Debug /p:Platform=x64 /p:RuntimeIdentifier=win-x64 /p:GenerateAppxPackageOnBuild=false'
                    exitCode = 0
                },
                [pscustomobject][ordered]@{
                    configuration = 'Release'
                    platform = 'x64'
                    runtime = 'win-x64'
                    command = 'contract-test <release-main-assembly-metadata-filter> <owned-root>'
                    exitCode = 0
                },
                [pscustomobject][ordered]@{
                    configuration = 'Release'
                    platform = 'x64'
                    runtime = 'win-x64'
                    command = 'contract-test <release-isolation-evidence-filter> <owned-root>'
                    exitCode = 0
                })
        } | ConvertTo-Json -Depth 12
        return
    }

    $repositoryRoot = Get-RepositoryRoot
    $sourceRoot = Join-Path $script:OwnedRoot 'source'
    $snapshot = Copy-RepositorySnapshot `
        -RepositoryRoot $repositoryRoot `
        -DestinationRoot $sourceRoot
    $sourceCommit = $snapshot.Commit
    $appProject = Join-Path `
        $sourceRoot `
        'IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj'
    if (-not (Test-Path -LiteralPath $appProject -PathType Leaf)) {
        throw 'The fresh source snapshot does not contain the application project.'
    }

    $releaseEvaluation = Get-FixtureEvaluation `
        -Evaluation (Invoke-ProjectEvaluation `
            -ProjectPath $appProject `
            -Configuration Release) `
        -SourceRoot $sourceRoot `
        -ProjectDirectory (Split-Path -Parent $appProject)
    $debugEvaluation = Get-FixtureEvaluation `
        -Evaluation (Invoke-ProjectEvaluation `
            -ProjectPath $appProject `
            -Configuration Debug) `
        -SourceRoot $sourceRoot `
        -ProjectDirectory (Split-Path -Parent $appProject)
    Assert-EvaluatedClosure `
        -SourceRoot $sourceRoot `
        -Release $releaseEvaluation `
        -DebugEvaluation $debugEvaluation
    Assert-WorkerDestructiveTargetContract -SourceRoot $sourceRoot

    $msbuild = Find-MSBuild
    $packageDirectory = Join-Path $script:OwnedRoot 'release\package'
    $packageOutput = Join-Path $packageDirectory 'application.msix'
    $releasePins = Get-AppBuildPathPins `
        -Configuration Release `
        -PackageDirectory $packageDirectory
    $debugPins = Get-AppBuildPathPins -Configuration Debug
    Assert-SnapshotBuildPathIsolation `
        -MSBuild $msbuild `
        -SourceRoot $sourceRoot `
        -Configuration Release `
        -Pins $releasePins
    Assert-SnapshotBuildPathIsolation `
        -MSBuild $msbuild `
        -SourceRoot $sourceRoot `
        -Configuration Debug `
        -Pins $debugPins

    if ($EvaluateContract) {
        [pscustomobject][ordered]@{
            schemaVersion = 1
            status = 'evaluation-contract-passed'
            ownedRoot = '<owned-root>'
            sourceSnapshotSha256 = $snapshot.Sha256
            debugEvaluatedCounts = $debugEvaluation.Counts
            debugEvaluatedIdentityCount = $debugEvaluation.Identities.Count
            debugExactIdentitiesSha256 = $debugEvaluation.IdentitySha256
            releaseEvaluatedCounts = $releaseEvaluation.Counts
        } | ConvertTo-Json -Depth 6
        return
    }

    $releaseBuild = Invoke-AppBuild `
        -MSBuild $msbuild `
        -ProjectPath $appProject `
        -Configuration Release `
        -Pins $releasePins `
        -PackageDirectory $packageDirectory
    $debugBuild = Invoke-AppBuild `
        -MSBuild $msbuild `
        -ProjectPath $appProject `
        -Configuration Debug `
        -Pins $debugPins
    $package = Inspect-ReleasePackage `
        -PackagePath $packageOutput `
        -SourceRoot $sourceRoot `
        -MSBuild $msbuild

    $evidenceValidationProvenance = [pscustomobject][ordered]@{
        configuration = 'Release'
        platform = 'x64'
        runtime = 'win-x64'
        command = 'contract-test <release-isolation-evidence-filter> <owned-root>'
        exitCode = 0
    }

    $evidence = [ordered]@{
        schemaVersion = 1
        status = 'passed'
        generatedUtc = [DateTime]::UtcNow.ToString(
            'o',
            [Globalization.CultureInfo]::InvariantCulture)
        sourceCommit = $sourceCommit
        sourceSnapshotSha256 = $snapshot.Sha256
        sourceSnapshotFileCount = $snapshot.FileCount
        ownedRoot = '<owned-root>'
        configuration = 'Release'
        platform = 'x64'
        runtime = 'win-x64'
        packagePath = $package.PackagePath
        packageSha256 = $package.PackageSha256
        layoutPath = $package.LayoutPath
        mainDll = $package.MainDll
        resourcesPri = $package.ResourcesPri
        scannedFileCount = $package.ScannedFileCount
        releaseForbiddenPathHits = @($package.ForbiddenPathHits)
        releaseForbiddenTokenHits = @($package.ForbiddenTokenHits)
        releaseForbiddenMetadataHits = @($package.ForbiddenMetadataHits)
        debugEvaluatedCounts = $debugEvaluation.Counts
        debugEvaluatedIdentityCount = $debugEvaluation.Identities.Count
        debugExactIdentitiesSha256 = $debugEvaluation.IdentitySha256
        releaseEvaluatedCounts = $releaseEvaluation.Counts
        buildProvenance = @(
            $releaseBuild,
            $debugBuild,
            $package.InspectorProvenance,
            $evidenceValidationProvenance)
    }
    $candidateEvidenceBytes = ConvertTo-EvidenceBytes -Evidence $evidence
    $validation = Invoke-CommittedEvidenceValidation `
        -ContractExecutable $package.ContractExecutable `
        -EvidenceBytes $candidateEvidenceBytes
    if ($validation.ExitCode -ne 0) {
        throw 'Committed release-isolation evidence validation did not exit zero.'
    }

    $pendingEvidenceBytes = $validation.Bytes
}
finally {
    Wait-NoOwnedProcesses -OwnedRoot $script:OwnedRoot
    Remove-OwnedRoot
}

if ($null -ne $pendingEvidenceBytes) {
    Publish-EvidenceBytes `
        -EvidenceBytes $pendingEvidenceBytes `
        -AbsoluteEvidencePath $absoluteEvidencePath
}
