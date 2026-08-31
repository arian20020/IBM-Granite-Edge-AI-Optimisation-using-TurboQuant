Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-E1RegularFile {
    param(
        [Parameter(Mandatory = $true)] [string] $Path,
        [long] $MaximumBytes = 16777216
    )
    $item = Get-Item -LiteralPath $Path -Force -ErrorAction Stop
    if ($item.PSIsContainer -or $item.Length -le 0 -or $item.Length -gt $MaximumBytes -or
        ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Evidence input is not a bounded regular file: $($item.Name)"
    }
    return $item
}

function Assert-E1NoReparseChain {
    param(
        [Parameter(Mandatory = $true)] [string] $Path,
        [Parameter(Mandatory = $true)] [string] $Boundary
    )
    $boundaryFull = [IO.Path]::GetFullPath($Boundary).TrimEnd('\')
    $cursor = [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($Path))
    while ($cursor -and $cursor.Length -ge $boundaryFull.Length) {
        $item = Get-Item -LiteralPath $cursor -Force -ErrorAction Stop
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Evidence input has a reparse-point ancestor: $($item.Name)"
        }
        if ($cursor.Equals($boundaryFull, [StringComparison]::OrdinalIgnoreCase)) { return }
        $parent = [IO.Directory]::GetParent($cursor)
        if (-not $parent) { break }
        $cursor = $parent.FullName
    }
    throw 'Evidence input is outside its validated boundary.'
}

function Assert-E1TrustedVSTestPath {
    param(
        [Parameter(Mandatory = $true)] [string] $Path,
        [Parameter(Mandatory = $true)] [string] $InstallationRoot
    )
    $root = [IO.Path]::GetFullPath($InstallationRoot).TrimEnd('\')
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($full) -ne 'vstest.console.exe' -or
        [IO.Path]::GetExtension($full) -ne '.exe') {
        throw 'VSTest is not the approved Visual Studio executable.'
    }
    [void](Assert-E1RegularFile -Path $full)
    Assert-E1NoReparseChain -Path $full -Boundary $root
    return $full
}

function Get-E1TrustedVSTest {
    $programFiles = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFiles)
    $programFilesX86 = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFilesX86)
    if ([string]::IsNullOrWhiteSpace($programFiles) -or [string]::IsNullOrWhiteSpace($programFilesX86)) {
        throw 'Blocked: canonical Windows program roots are unavailable.'
    }
    $candidates = @(
        (Join-Path $programFilesX86 'Microsoft Visual Studio\Installer\vswhere.exe'),
        (Join-Path $programFiles 'Microsoft Visual Studio\Installer\vswhere.exe')
    )
    $vswhere = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    if (-not $vswhere) { throw 'Blocked: approved vswhere.exe is unavailable.' }
    $vswhereItem = Assert-E1RegularFile -Path $vswhere
    if ($vswhereItem.Name -ne 'vswhere.exe') { throw 'Blocked: Visual Studio discovery executable identity is invalid.' }
    $vswhereBoundary = if ($vswhereItem.FullName.StartsWith([IO.Path]::GetFullPath($programFilesX86) + '\', [StringComparison]::OrdinalIgnoreCase)) { $programFilesX86 } else { $programFiles }
    Assert-E1NoReparseChain -Path $vswhereItem.FullName -Boundary $vswhereBoundary
    $installation = @(& $vswhereItem.FullName -latest -products * -requires Microsoft.VisualStudio.Component.TestTools.BuildTools -property installationPath 2>&1 | ForEach-Object { "$_" })
    if ($LASTEXITCODE -ne 0 -or $installation.Count -ne 1 -or [string]::IsNullOrWhiteSpace($installation[0])) {
        throw 'Blocked: Visual Studio discovery failed.'
    }
    $rootItem = Get-Item -LiteralPath $installation[0].Trim() -Force -ErrorAction Stop
    if (-not $rootItem.PSIsContainer -or ($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'Blocked: Visual Studio installation root is invalid.'
    }
    $canonicalRoots = @([IO.Path]::GetFullPath($programFiles).TrimEnd('\'), [IO.Path]::GetFullPath($programFilesX86).TrimEnd('\'))
    if (-not @($canonicalRoots | Where-Object { $rootItem.FullName.StartsWith($_ + '\', [StringComparison]::OrdinalIgnoreCase) }).Count) {
        throw 'Blocked: Visual Studio installation is outside canonical Windows program roots.'
    }
    $expected = Join-Path $rootItem.FullName 'Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe'
    return Assert-E1TrustedVSTestPath -Path $expected -InstallationRoot $rootItem.FullName
}

function Get-E1TrustedDotNet {
    $programFiles = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFiles)
    if ([string]::IsNullOrWhiteSpace($programFiles)) { throw 'Blocked: canonical Windows Program Files is unavailable.' }
    $path = Join-Path $programFiles 'dotnet\dotnet.exe'
    $item = Assert-E1RegularFile -Path $path
    if ($item.Name -ne 'dotnet.exe' -or $item.Extension -ne '.exe') { throw 'Blocked: approved dotnet executable identity is invalid.' }
    Assert-E1NoReparseChain -Path $item.FullName -Boundary $programFiles
    return $item.FullName
}

function Get-E1TrustedMSBuild {
    $programFiles = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFiles)
    if ([string]::IsNullOrWhiteSpace($programFiles)) { throw 'Blocked: canonical Windows Program Files is unavailable.' }
    $dotnetRoot = Join-Path $programFiles 'dotnet'
    $sdkRoot = Join-Path $dotnetRoot 'sdk'
    $sdkCandidates = @(Get-ChildItem -LiteralPath $sdkRoot -Directory -Force -ErrorAction Stop | ForEach-Object {
        $parsed = [Version]::new()
        if ([Version]::TryParse($_.Name, [ref]$parsed)) {
            [pscustomobject]@{ Version = $parsed; Directory = $_ }
        }
    } | Sort-Object Version -Descending)
    if ($sdkCandidates.Count -eq 0) { throw 'Blocked: no stable canonical .NET SDK is installed.' }
    $sdkDirectory = $sdkCandidates[0].Directory
    if (($sdkDirectory.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Blocked: canonical .NET SDK directory is a reparse point.' }
    $path = Join-Path $sdkDirectory.FullName 'MSBuild.dll'
    $item = Assert-E1RegularFile -Path $path
    if ($item.Name -ne 'MSBuild.dll' -or $item.Extension -ne '.dll') { throw 'Blocked: approved MSBuild identity is invalid.' }
    Assert-E1NoReparseChain -Path $item.FullName -Boundary $dotnetRoot
    return $item.FullName
}

function Get-E1TrustedMSBuildSdkRoot {
    param([Parameter(Mandatory = $true)] [string] $MSBuildPath)
    $programFiles = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFiles)
    $dotnetRoot = Join-Path $programFiles 'dotnet'
    $msbuild = [IO.Path]::GetFullPath($MSBuildPath)
    if (-not $msbuild.StartsWith(([IO.Path]::GetFullPath($dotnetRoot).TrimEnd('\') + '\sdk\'), [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($msbuild) -ne 'MSBuild.dll') {
        throw 'Blocked: MSBuild SDK root is not canonical.'
    }
    $sdksPath = Join-Path ([IO.Path]::GetDirectoryName($msbuild)) 'Sdks'
    $item = Get-Item -LiteralPath $sdksPath -Force -ErrorAction Stop
    if (-not $item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'Blocked: canonical MSBuild SDK root is invalid.'
    }
    Assert-E1NoReparseChain -Path (Join-Path $item.FullName 'boundary.probe') -Boundary $dotnetRoot
    return $item.FullName
}

function Get-E1UnsafeManagedEnvironmentNames {
    return @([Environment]::GetEnvironmentVariables('Process').Keys | ForEach-Object { [string]$_ } | Where-Object {
        $_ -match '^(CORECLR_|COR_|COMPlus_|DOTNET_|MSBUILD|NUGET_)' -or $_ -eq 'RestoreSources'
    } | Sort-Object -Unique)
}

function Assert-E1SafeDirectoryChain {
    param(
        [Parameter(Mandatory = $true)] [string] $Path,
        [Parameter(Mandatory = $true)] [string] $RepositoryRoot
    )
    $repository = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\')
    $target = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    if (-not $target.StartsWith($repository + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Build directory is outside the repository.'
    }
    $repositoryItem = Get-Item -LiteralPath $repository -Force -ErrorAction Stop
    if (-not $repositoryItem.PSIsContainer -or ($repositoryItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'Repository root is not a regular directory.'
    }
    $cursor = $repository
    foreach ($segment in $target.Substring($repository.Length + 1).Split('\')) {
        if ([string]::IsNullOrWhiteSpace($segment) -or $segment -in @('.', '..')) { throw 'Build directory syntax is not canonical.' }
        $cursor = Join-Path $cursor $segment
        $item = Get-Item -LiteralPath $cursor -Force -ErrorAction Stop
        if (-not $item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Build directory ancestry is not regular: $segment"
        }
    }
    return $target
}

function New-E1SafeDirectoryChain {
    param(
        [Parameter(Mandatory = $true)] [string] $Path,
        [Parameter(Mandatory = $true)] [string] $RepositoryRoot
    )
    $repository = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\')
    $target = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    if (-not $target.StartsWith($repository + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Build directory is outside the repository.' }
    $cursor = $repository
    foreach ($segment in $target.Substring($repository.Length + 1).Split('\')) {
        if ([string]::IsNullOrWhiteSpace($segment) -or $segment -in @('.', '..')) { throw 'Build directory syntax is not canonical.' }
        $cursor = Join-Path $cursor $segment
        if (-not (Test-Path -LiteralPath $cursor)) { [void][IO.Directory]::CreateDirectory($cursor) }
        $item = Get-Item -LiteralPath $cursor -Force -ErrorAction Stop
        if (-not $item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Build directory ancestry is not regular: $segment"
        }
    }
    return Assert-E1SafeDirectoryChain -Path $target -RepositoryRoot $repository
}

function Remove-E1SafeDirectoryTree {
    param(
        [Parameter(Mandatory = $true)] [string] $Path,
        [Parameter(Mandatory = $true)] [string] $RepositoryRoot
    )
    $target = Assert-E1SafeDirectoryChain -Path $Path -RepositoryRoot $RepositoryRoot
    foreach ($item in @(Get-ChildItem -LiteralPath $target -Force -Recurse -ErrorAction Stop)) {
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Build cleanup refuses a reparse-point descendant.' }
    }
    Remove-Item -LiteralPath $target -Recurse -Force -ErrorAction Stop
    if (Test-Path -LiteralPath $target) { throw 'Build cleanup did not remove the validated directory.' }
}

function Assert-E1EvidenceAssembly {
    param(
        [Parameter(Mandatory = $true)] [string] $Path,
        [Parameter(Mandatory = $true)] [string] $ExpectedOutputRoot,
        [Parameter(Mandatory = $true)] [ValidatePattern('^[0-9a-f]{40}$')] [string] $ImplementationCommit,
        [Parameter(Mandatory = $true)] [DateTime] $FreshSinceUtc
    )
    $root = [IO.Path]::GetFullPath($ExpectedOutputRoot).TrimEnd('\')
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($full) -ne 'GraniteEdgeAI.EndToEndTests.dll' -or
        [IO.Path]::GetExtension($full) -ne '.dll') {
        throw 'Evidence assembly is outside the expected repository build output.'
    }
    $item = Assert-E1RegularFile -Path $full
    Assert-E1NoReparseChain -Path $full -Boundary $root
    if ($item.LastWriteTimeUtc -lt $FreshSinceUtc.AddSeconds(-2)) { throw 'Evidence assembly is stale.' }
    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($item.FullName).ProductVersion
    if (-not $version -or $version.IndexOf($ImplementationCommit, [StringComparison]::Ordinal) -lt 0) {
        throw 'Evidence assembly is not bound to the implementation subject.'
    }
    return $item.FullName
}

function Invoke-E1Git {
    param([string] $RepositoryRoot, [string[]] $Arguments)
    $output = @(& git.exe -C $RepositoryRoot @Arguments 2>&1 | ForEach-Object { "$_" })
    if ($LASTEXITCODE -ne 0) { throw "Git subject-boundary validation failed: git $($Arguments -join ' ')" }
    return @($output)
}

function Assert-E1ImplementationBoundary {
    param(
        [Parameter(Mandatory = $true)] [string] $RepositoryRoot,
        [Parameter(Mandatory = $true)] [ValidatePattern('^[0-9a-f]{40}$')] [string] $ImplementationCommit
    )
    $allowed = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    @(
        'docs/audits/2026-08-30/E1-r4-independent-acceptance-v2.md',
        'docs/audits/2026-08-30/evidence/E1-r4-independent-acceptance-v2.json',
        'docs/audits/2026-08-30/evidence/E1-r4-post-acceptance-v2.json',
        'docs/audits/2026-08-30/handoffs/R4-E1.json',
        'docs/audits/2026-08-30/evidence/E1-r4-external-block-observation-v2.json',
        'docs/audits/2026-08-30/evidence/E1-r4-independent-final-review-v4.json'
    ) | ForEach-Object { [void]$allowed.Add($_) }
    [void](Invoke-E1Git -RepositoryRoot $RepositoryRoot -Arguments @('merge-base', '--is-ancestor', $ImplementationCommit, 'HEAD'))
    $committed = Invoke-E1Git -RepositoryRoot $RepositoryRoot -Arguments @('diff', '--name-only', "$ImplementationCommit..HEAD", '--')
    foreach ($path in @($committed | Where-Object { $_ })) {
        if (-not $allowed.Contains($path)) { throw "Code or configuration changed after the implementation subject: $path" }
    }
    $status = Invoke-E1Git -RepositoryRoot $RepositoryRoot -Arguments @('status', '--porcelain=v1', '--untracked-files=all')
    foreach ($line in @($status | Where-Object { $_ })) {
        if ($line.Length -lt 4) { throw 'Git worktree status is malformed.' }
        $path = $line.Substring(3)
        if ($path.Contains(' -> ')) { throw 'Renamed worktree content is not allowed after the implementation subject.' }
        if (-not $allowed.Contains($path)) { throw "Uncommitted code or configuration differs from the implementation subject: $path" }
    }
}

function New-E1AuthoritativeVSTestArguments {
    param(
        [Parameter(Mandatory = $true)] [string] $AssemblyPath,
        [Parameter(Mandatory = $true)] [string] $ExpectedClass,
        [Parameter(Mandatory = $true)] [string] $ExpectedMethod,
        [Parameter(Mandatory = $true)] [string] $ResultsRoot,
        [Parameter(Mandatory = $true)] [ValidatePattern('^[A-Za-z0-9._-]+\.trx$')] [string] $TrxFileName
    )
    $fullyQualified = "$ExpectedClass.$ExpectedMethod"
    return @(
        $AssemblyPath,
        '/Platform:x64',
        "/TestCaseFilter:FullyQualifiedName=$fullyQualified",
        "/Logger:trx;LogFileName=$TrxFileName",
        "/ResultsDirectory:$ResultsRoot"
    )
}

function Assert-E1AuthoritativeTrx {
    param(
        [Parameter(Mandatory = $true)] [string] $Path,
        [Parameter(Mandatory = $true)] [string] $ExpectedResultsRoot,
        [Parameter(Mandatory = $true)] [string] $RepositoryRoot,
        [Parameter(Mandatory = $true)] [string] $ExpectedClass,
        [Parameter(Mandatory = $true)] [string] $ExpectedMethod,
        [Parameter(Mandatory = $true)] [DateTime] $InvocationStartedUtc
    )
    $repository = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\')
    $root = [IO.Path]::GetFullPath($ExpectedResultsRoot).TrimEnd('\')
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $root.Equals($repository, [StringComparison]::OrdinalIgnoreCase) -and
        -not $root.StartsWith($repository + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Authoritative TRX results root is outside the repository.' }
    if (-not $full.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Authoritative TRX is outside its validated results root.' }
    $item = Assert-E1RegularFile -Path $Path
    Assert-E1NoReparseChain -Path $item.FullName -Boundary $repository
    if ($item.LastWriteTimeUtc -lt $InvocationStartedUtc.AddSeconds(-2)) { throw 'Authoritative TRX is stale.' }
    try { [xml]$document = Get-Content -Raw -LiteralPath $item.FullName -ErrorAction Stop }
    catch { throw 'Authoritative TRX is unreadable or malformed.' }
    $namespace = [Xml.XmlNamespaceManager]::new($document.NameTable)
    $namespace.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
    $summary = $document.SelectSingleNode('/t:TestRun/t:ResultSummary', $namespace)
    $counters = $document.SelectSingleNode('/t:TestRun/t:ResultSummary/t:Counters', $namespace)
    $results = @($document.SelectNodes('/t:TestRun/t:Results/t:UnitTestResult', $namespace))
    $definitions = @($document.SelectNodes('/t:TestRun/t:TestDefinitions/t:UnitTest', $namespace))
    if (-not $summary -or -not $counters -or $summary.outcome -ne 'Completed' -or
        [int]$counters.total -ne 1 -or [int]$counters.executed -ne 1 -or [int]$counters.passed -ne 1 -or
        [int]$counters.failed -ne 0 -or [int]$counters.notExecuted -ne 0 -or
        $results.Count -ne 1 -or $definitions.Count -ne 1) {
        throw 'Authoritative TRX arithmetic or outcome is not exactly one passing test.'
    }
    $result = $results[0]
    $definition = $definitions[0]
    $method = $definition.SelectSingleNode('t:TestMethod', $namespace)
    if (-not $method -or $result.outcome -ne 'Passed' -or $result.testName -ne $ExpectedMethod -or
        $definition.name -ne $ExpectedMethod -or $method.name -ne $ExpectedMethod -or
        $method.className -ne $ExpectedClass -or $result.testId -ne $definition.id) {
        throw 'Authoritative TRX test identity differs from the exact intended evaluator.'
    }
}

Export-ModuleMember -Function Get-E1TrustedVSTest, Get-E1TrustedDotNet, Get-E1TrustedMSBuild, Get-E1TrustedMSBuildSdkRoot, Get-E1UnsafeManagedEnvironmentNames, Assert-E1SafeDirectoryChain, New-E1SafeDirectoryChain, Remove-E1SafeDirectoryTree, Assert-E1TrustedVSTestPath, Assert-E1ImplementationBoundary, Assert-E1EvidenceAssembly, New-E1AuthoritativeVSTestArguments, Assert-E1AuthoritativeTrx
