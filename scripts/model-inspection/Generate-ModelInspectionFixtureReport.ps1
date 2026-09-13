#requires -Version 5.1

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$reportFileName = 'Model-Inspection-Fixture-Catalog.md'
$outputEnvironmentName = 'MODEL_INSPECTION_FIXTURE_REPORT_OUTPUT_PATH'
$nodeReuseEnvironmentName = 'MSBUILDDISABLENODEREUSE'
$contractTestName =
    'GraniteEdgeAI.ModelInspection.Contracts.Tests.' +
    'ModelInspectionFixtureReportContractTests.' +
    'GeneratedCatalogueReportByteMatchesCheckedInUtf8LfArtifact'

function Assert-PathHasNoReparsePoint {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    $current = New-Object System.IO.DirectoryInfo(
        [System.IO.Path]::GetFullPath($Path))
    while ($null -ne $current) {
        if (($current.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Path cannot traverse a reparse point: $($current.FullName)"
        }

        $current = $current.Parent
    }
}

function Get-RepositorySnapshot {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepositoryRoot,

        [Parameter(Mandatory = $true)]
        [string] $Git
    )

    [string[]] $status = @(& $Git `
            -C $RepositoryRoot `
            status `
            '--porcelain=v1' `
            '--untracked-files=all')
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not capture repository status for report generation.'
    }

    [string[]] $paths = @(& $Git `
            -C $RepositoryRoot `
            -c 'core.quotepath=false' `
            ls-files `
            -c `
            -o `
            '--exclude-standard')
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not enumerate repository files for report generation.'
    }

    [Array]::Sort($status, [StringComparer]::Ordinal)
    [Array]::Sort($paths, [StringComparer]::Ordinal)
    $snapshot = New-Object 'System.Collections.Generic.List[string]'
    foreach ($line in $status) {
        $snapshot.Add("status|$line")
    }

    $repositoryPrefix = $RepositoryRoot.TrimEnd('\') + '\'
    foreach ($relativePath in $paths) {
        $fullPath = [System.IO.Path]::GetFullPath(
            (Join-Path $RepositoryRoot $relativePath))
        if (-not $fullPath.StartsWith(
                $repositoryPrefix,
                [StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            throw "Repository snapshot path is not a physical child: $relativePath"
        }

        $hash = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash
        $snapshot.Add("file|$hash|$relativePath")
    }

    return $snapshot.ToArray()
}

function Assert-RepositorySnapshotsEqual {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Before,

        [Parameter(Mandatory = $true)]
        [string[]] $After
    )

    if ($Before.Count -ne $After.Count) {
        throw 'Report generation changed the repository file/status closure.'
    }

    for ($index = 0; $index -lt $Before.Count; $index++) {
        if ($Before[$index] -cne $After[$index]) {
            throw 'Report generation changed the repository file/status closure.'
        }
    }
}

function Remove-OwnedBuildRoot {
    param(
        [Parameter(Mandatory = $true)]
        [string] $BuildRoot,

        [Parameter(Mandatory = $true)]
        [string] $RepositoryRoot
    )

    $canonicalBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
    $repositoryPrefix = $RepositoryRoot.TrimEnd('\') + '\'
    $leaf = [System.IO.Path]::GetFileName($canonicalBuildRoot)
    if (-not $canonicalBuildRoot.StartsWith(
            $repositoryPrefix,
            [StringComparison]::OrdinalIgnoreCase) -or
        $leaf -cnotmatch '^\.model-inspection-report-build-[0-9a-f]{32}$') {
        throw 'Refusing to remove a non-owned report build root.'
    }

    if (-not (Test-Path -LiteralPath $canonicalBuildRoot -PathType Container)) {
        return
    }

    $item = Get-Item -LiteralPath $canonicalBuildRoot -Force
    if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'Refusing to remove a reparse-point report build root.'
    }

    [System.IO.Directory]::Delete($canonicalBuildRoot, $true)
    if (Test-Path -LiteralPath $canonicalBuildRoot) {
        throw 'The owned report build root was not removed.'
    }
}

function Assert-GeneratedReport {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw 'The report contract did not create its output file.'
    }

    $item = Get-Item -LiteralPath $Path -Force
    if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 -or
        $item.Length -le 0 -or
        $item.Length -gt (1024 * 1024)) {
        throw 'The generated report is not a bounded physical file.'
    }

    $parent = Split-Path -Parent $Path
    [System.IO.FileSystemInfo[]] $entries = @(Get-ChildItem `
            -LiteralPath $parent `
            -Force)
    if ($entries.Count -ne 1 -or
        -not [string]::Equals(
            $entries[0].FullName,
            $Path,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The external report directory must contain exactly the generated report.'
    }

    [byte[]] $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -ge 3 -and
        $bytes[0] -eq 0xEF -and
        $bytes[1] -eq 0xBB -and
        $bytes[2] -eq 0xBF) {
        throw 'The generated report cannot contain a UTF-8 BOM.'
    }

    if ([Array]::IndexOf($bytes, [byte] 13) -ge 0 -or
        $bytes[$bytes.Length - 1] -ne 10 -or
        ($bytes.Length -gt 1 -and $bytes[$bytes.Length - 2] -eq 10)) {
        throw 'The generated report must use LF and exactly one final newline.'
    }

    $strictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)
    $text = $strictUtf8.GetString($bytes)
    [string[]] $lines = $text.Substring(0, $text.Length - 1).Split([char] 10)
    if ($lines.Count -ne 60 -or
        $lines[0] -cne '# Model Inspection Fixture Catalog' -or
        $lines[8] -cne ('| ID | Filename | Target condition | Category | ' +
            'Coverage tags | Initial screen | Expected screen | Interactions | ' +
            'Presets | Semantic/render | Lifetime | Provenance | ' +
            'Real-worker evidence |') -or
        $lines[9] -cne '|---|---|---|---|---|---|---|---|---|---|---|---|---|') {
        throw 'The generated report header or exact line count is invalid.'
    }

    [string[]] $rows = @($lines[10..59])
    if ($rows.Count -ne 50) {
        throw 'The generated report must contain exactly 50 fixture rows.'
    }

    for ($index = 0; $index -lt $rows.Count; $index++) {
        $expectedId = 'MI-{0:D3}' -f ($index + 1)
        if (-not $rows[$index].StartsWith(
                "| $expectedId |",
                [StringComparison]::Ordinal)) {
            throw "Generated report row order differs at $expectedId."
        }

        [string[]] $cells = @($rows[$index].Substring(
                2,
                $rows[$index].Length - 4) -split ' \| ')
        if ($cells.Count -ne 13) {
            throw "Generated report row $expectedId does not contain 13 cells."
        }
    }
}

$repositoryRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot '..\..')).TrimEnd('\')
if (-not (Test-Path `
        -LiteralPath (Join-Path $repositoryRoot 'IBM Granite with TurboQuant (Intel).slnx') `
        -PathType Leaf)) {
    throw 'Could not locate the Model Inspection repository root.'
}

if ([string]::IsNullOrWhiteSpace($OutputPath) -or
    $OutputPath -notmatch '^(?:[A-Za-z]:[\\/]|\\\\[^\\/]+[\\/][^\\/]+[\\/])') {
    throw 'OutputPath must be a fully qualified Windows path.'
}

$canonicalOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
if ([System.IO.Path]::GetFileName($canonicalOutputPath) -cne $reportFileName) {
    throw "OutputPath must end with the exact filename $reportFileName."
}

$repositoryPrefix = $repositoryRoot + '\'
if ($canonicalOutputPath.Equals(
        $repositoryRoot,
        [StringComparison]::OrdinalIgnoreCase) -or
    $canonicalOutputPath.StartsWith(
        $repositoryPrefix,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputPath must be outside the repository.'
}

$outputParent = Split-Path -Parent $canonicalOutputPath
if (-not (Test-Path -LiteralPath $outputParent -PathType Container)) {
    throw 'OutputPath parent must already exist.'
}

Assert-PathHasNoReparsePoint -Path $outputParent
if (@(Get-ChildItem -LiteralPath $outputParent -Force).Count -ne 0) {
    throw 'OutputPath parent must be a fresh empty directory.'
}

$git = (Get-Command git.exe -ErrorAction Stop).Source
$dotnet = (Get-Command dotnet.exe -ErrorAction Stop).Source
[string[]] $repositoryBefore = @(Get-RepositorySnapshot `
        -RepositoryRoot $repositoryRoot `
        -Git $git)

$buildRoot = Join-Path `
    $repositoryRoot `
    ('.model-inspection-report-build-' + [guid]::NewGuid().ToString('N'))
$buildRootCreated = $false
$environmentChanged = $false
$nodeReuseEnvironmentChanged = $false
$priorOutputEnvironment = [Environment]::GetEnvironmentVariable(
    $outputEnvironmentName,
    'Process')
$priorNodeReuseEnvironment = [Environment]::GetEnvironmentVariable(
    $nodeReuseEnvironmentName,
    'Process')

try {
    if (Test-Path -LiteralPath $buildRoot) {
        throw 'The owned report build root is not fresh.'
    }

    $null = [System.IO.Directory]::CreateDirectory($buildRoot)
    $buildRootCreated = $true
    Assert-PathHasNoReparsePoint -Path $buildRoot

    $escapedBuildRoot = [System.Security.SecurityElement]::Escape(
        $buildRoot.TrimEnd('\'))
    $propsTemplate = @'
<Project>
  <PropertyGroup>
    <_ModelInspectionReportBuildRoot>{0}</_ModelInspectionReportBuildRoot>
    <BaseIntermediateOutputPath>$(_ModelInspectionReportBuildRoot)\obj\$(MSBuildProjectName)\</BaseIntermediateOutputPath>
    <MSBuildProjectExtensionsPath>$(_ModelInspectionReportBuildRoot)\obj\$(MSBuildProjectName)\</MSBuildProjectExtensionsPath>
    <BaseOutputPath>$(_ModelInspectionReportBuildRoot)\bin\$(MSBuildProjectName)\</BaseOutputPath>
    <OutputPath>$(_ModelInspectionReportBuildRoot)\out\$(MSBuildProjectName)\</OutputPath>
    <DefaultItemExcludes>$(DefaultItemExcludes);$(MSBuildProjectDirectory)\bin\**;$(MSBuildProjectDirectory)\obj\**</DefaultItemExcludes>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>
  </PropertyGroup>
</Project>
'@
    $props = [string]::Format(
        [Globalization.CultureInfo]::InvariantCulture,
        $propsTemplate,
        $escapedBuildRoot)
    $propsPath = Join-Path $buildRoot 'report.before-directory-build.props'
    $utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText(
        $propsPath,
        $props.Replace("`r`n", "`n"),
        $utf8WithoutBom)

    $contractProject = Join-Path `
        $repositoryRoot `
        'tests\ContractTests\GraniteEdgeAI.ModelInspection.Contracts.Tests\GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj'
    $restorePackagesPath = Join-Path $buildRoot 'nuget-packages'
    try {
        [Environment]::SetEnvironmentVariable(
            $nodeReuseEnvironmentName,
            '1',
            'Process')
        $nodeReuseEnvironmentChanged = $true
        & $dotnet @(
            'build',
            $contractProject,
            '--configuration',
            'Release',
            '--nologo',
            '--no-incremental',
            '-p:Platform=x64',
            '-p:RuntimeIdentifier=win-x64',
            '-p:PublishReadyToRun=false',
            "/p:CustomBeforeDirectoryBuildProps=$propsPath",
            "/p:RestorePackagesPath=$restorePackagesPath",
            '/p:UseSharedCompilation=false')
        $buildExitCode = $LASTEXITCODE
    }
    finally {
        [Environment]::SetEnvironmentVariable(
            $nodeReuseEnvironmentName,
            $priorNodeReuseEnvironment,
            'Process')
        $nodeReuseEnvironmentChanged = $false
    }

    if ($buildExitCode -ne 0) {
        throw "Release Contracts build failed with exit code $buildExitCode."
    }

    $contractExecutable = Join-Path `
        $buildRoot `
        'out\GraniteEdgeAI.ModelInspection.Contracts.Tests\GraniteEdgeAI.ModelInspection.Contracts.Tests.exe'
    if (-not (Test-Path -LiteralPath $contractExecutable -PathType Leaf)) {
        throw 'The isolated Release Contracts executable is missing.'
    }

    try {
        [Environment]::SetEnvironmentVariable(
            $outputEnvironmentName,
            $canonicalOutputPath,
            'Process')
        $environmentChanged = $true
        Push-Location (Split-Path -Parent $contractExecutable)
        try {
            & $contractExecutable @(
                '--filter',
                "FullyQualifiedName=$contractTestName",
                '--minimum-expected-tests',
                '1',
                '--progress',
                'off')
            $contractExitCode = $LASTEXITCODE
        }
        finally {
            Pop-Location
        }
    }
    finally {
        [Environment]::SetEnvironmentVariable(
            $outputEnvironmentName,
            $priorOutputEnvironment,
            'Process')
        $environmentChanged = $false
    }

    if ($contractExitCode -ne 0) {
        throw "Report contract invocation failed with exit code $contractExitCode."
    }

    Assert-GeneratedReport -Path $canonicalOutputPath
    $reportSha256 = (Get-FileHash `
        -LiteralPath $canonicalOutputPath `
        -Algorithm SHA256).Hash
    Write-Output "Generated $canonicalOutputPath"
    Write-Output "SHA-256 $reportSha256"
}
finally {
    if ($environmentChanged) {
        [Environment]::SetEnvironmentVariable(
            $outputEnvironmentName,
            $priorOutputEnvironment,
            'Process')
    }

    if ($nodeReuseEnvironmentChanged) {
        [Environment]::SetEnvironmentVariable(
            $nodeReuseEnvironmentName,
            $priorNodeReuseEnvironment,
            'Process')
    }

    if ($buildRootCreated) {
        Remove-OwnedBuildRoot `
            -BuildRoot $buildRoot `
            -RepositoryRoot $repositoryRoot
    }

    [string[]] $repositoryAfter = @(Get-RepositorySnapshot `
            -RepositoryRoot $repositoryRoot `
            -Git $git)
    Assert-RepositorySnapshotsEqual `
        -Before $repositoryBefore `
        -After $repositoryAfter
}
