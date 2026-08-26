[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Filter,
    [string]$EvidenceDirectory
)

$ErrorActionPreference = 'Stop'

function Test-WrapperContainedPath {
    param([Parameter(Mandatory)][string]$Root, [Parameter(Mandatory)][string]$Path)
    $canonicalRoot = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $canonicalPath = [IO.Path]::GetFullPath($Path)
    return $canonicalPath.StartsWith($canonicalRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
}

function Assert-WrapperNonReparseAncestry {
    param([Parameter(Mandatory)][string]$Path, [switch]$IncludeLeaf)
    $cursor = if ($IncludeLeaf) { [IO.Path]::GetFullPath($Path) } else { Split-Path -Parent ([IO.Path]::GetFullPath($Path)) }
    while (-not [string]::IsNullOrWhiteSpace($cursor)) {
        if (Test-Path -LiteralPath $cursor) {
            $item = Get-Item -Force -LiteralPath $cursor -ErrorAction Stop
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Reparse points are not permitted in checkpoint wrapper paths.' }
        }
        $parent = Split-Path -Parent $cursor
        if ([string]::IsNullOrWhiteSpace($parent) -or $parent -eq $cursor) { break }
        $cursor = $parent
    }
}

function ConvertTo-PrivateCheckpointFailure {
    param([Parameter(Mandatory)][string]$Message)
    $safe = $Message
    foreach ($name in @('repository', 'evidenceRoot', 'installer', 'communityRoot')) {
        $variable = Get-Variable -Name $name -Scope Script -ErrorAction SilentlyContinue
        if ($null -ne $variable -and -not [string]::IsNullOrWhiteSpace([string]$variable.Value)) {
            $safe = [regex]::Replace($safe, [regex]::Escape([string]$variable.Value), '[private-path]', [Text.RegularExpressions.RegexOptions]::IgnoreCase)
        }
    }
    $safe = [regex]::Replace($safe, '(?i)(?:[A-Z]:\\|\\\\)[^\r\n;]+', '[private-path]')
    $safe = [regex]::Replace($safe, '[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]', '?')
    if ($safe.Length -gt 512) { $safe = $safe.Substring(0, 512) }
    return $safe
}

trap {
    [Console]::Error.WriteLine('Packaged checkpoint failed: ' + (ConvertTo-PrivateCheckpointFailure -Message ([string]$_.Exception.Message)))
    exit 1
}

if ([string]::IsNullOrWhiteSpace($Filter) -or $Filter.Length -gt 4096 -or $Filter -cnotmatch '^FullyQualifiedName~[A-Za-z0-9_.+]+(?:\|FullyQualifiedName~[A-Za-z0-9_.+]+)*$') {
    throw 'Filter must use OR-separated FullyQualifiedName~value clauses and be at most 4096 characters.'
}

$installer = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer'
$env:PATH = $installer + [IO.Path]::PathSeparator + $env:PATH
$installerPathHead = @($env:PATH -split [regex]::Escape([string][IO.Path]::PathSeparator))[0]
if (-not [StringComparer]::OrdinalIgnoreCase.Equals([IO.Path]::GetFullPath($installerPathHead).TrimEnd('\', '/'), [IO.Path]::GetFullPath($installer).TrimEnd('\', '/'))) {
    throw 'Visual Studio Installer PATH precedence assertion failed.'
}
$env:MSBUILDDISABLENODEREUSE = '1'
$env:UseSharedCompilation = 'false'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
$repository = [IO.Path]::GetFullPath((Join-Path -Path $PSScriptRoot -ChildPath '..\..'))
$modulePath = Join-Path -Path $PSScriptRoot -ChildPath 'PackagedCheckpoint.Core.psm1'
Import-Module -Force -Name $modulePath

$vswhere = Join-Path -Path $installer -ChildPath 'vswhere.exe'
$vswhereBefore = Get-PackagedRegularFileIdentity -Path $vswhere -Label 'VS Community discovery tool'
$communityResult = Invoke-PackagedProcess `
    -FilePath $vswhereBefore.Path `
    -WorkingDirectory $repository `
    -Arguments @('-latest', '-products', 'Microsoft.VisualStudio.Product.Community', '-property', 'installationPath') `
    -TimeoutSeconds 30
$communityLines = @($communityResult.Output -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
if ($communityResult.ExitCode -ne 0 -or $communityLines.Count -ne 1) { throw 'VS Community discovery must bind exactly one installation root.' }
$communityRoot = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $communityLines[0] -ErrorAction Stop).Path).TrimEnd('\', '/')
Assert-WrapperNonReparseAncestry -Path $communityRoot -IncludeLeaf
$msbuildResult = Invoke-PackagedProcess `
    -FilePath $vswhereBefore.Path `
    -WorkingDirectory $repository `
    -Arguments @('-latest', '-products', 'Microsoft.VisualStudio.Product.Community', '-requires', 'Microsoft.Component.MSBuild', '-find', 'MSBuild\**\Bin\MSBuild.exe') `
    -TimeoutSeconds 30
if ($msbuildResult.ExitCode -ne 0) {
    throw 'VS Community MSBuild discovery failed.'
}
$msbuildLines = @($msbuildResult.Output -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
if ($msbuildLines.Count -ne 1) {
    throw 'VS Community discovery must bind exactly one MSBuild executable.'
}
$msbuildBefore = Get-PackagedRegularFileIdentity -Path $msbuildLines[0] -Label 'VS Community MSBuild'
if (-not (Test-WrapperContainedPath -Root $communityRoot -Path $msbuildBefore.Path)) { throw 'MSBuild executable is outside the exact VS Community installation root.' }

$runnerResult = Invoke-PackagedProcess `
    -FilePath $vswhereBefore.Path `
    -WorkingDirectory $repository `
    -Arguments @('-latest', '-products', 'Microsoft.VisualStudio.Product.Community', '-find', '**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe') `
    -TimeoutSeconds 30
if ($runnerResult.ExitCode -ne 0) {
    throw 'VS Community VSTest discovery failed.'
}
$runnerLines = @($runnerResult.Output -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
if ($runnerLines.Count -ne 1) {
    throw 'VS Community discovery must bind exactly one VSTest executable.'
}
$runnerBefore = Get-PackagedRegularFileIdentity -Path $runnerLines[0] -Label 'VS Community VSTest'
if (-not (Test-WrapperContainedPath -Root $communityRoot -Path $runnerBefore.Path)) { throw 'VSTest executable is outside the exact VS Community installation root.' }
$vswhereAfterDiscovery = Get-PackagedRegularFileIdentity -Path $vswhereBefore.Path -Label 'VS Community discovery tool'
Assert-PackagedFileIdentityUnchanged -Before $vswhereBefore -After $vswhereAfterDiscovery -Label 'VS Community discovery tool'

$project = Join-Path -Path $repository -ChildPath 'tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj'
$recipe = Join-Path -Path $repository -ChildPath 'tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe'
if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) {
    $EvidenceDirectory = Join-Path -Path ([IO.Path]::GetTempPath()) -ChildPath ('geai-packaged-checkpoint-evidence-' + [guid]::NewGuid().ToString('N'))
}
$evidenceRoot = [IO.Path]::GetFullPath($EvidenceDirectory)
$createdEvidenceRoot = $false
if ((Test-WrapperContainedPath -Root $repository -Path $evidenceRoot) -or [StringComparer]::OrdinalIgnoreCase.Equals($repository.TrimEnd('\', '/'), $evidenceRoot.TrimEnd('\', '/'))) {
    throw 'EvidenceDirectory must be outside the source repository.'
}
Assert-WrapperNonReparseAncestry -Path $evidenceRoot -IncludeLeaf
if (-not (Test-Path -LiteralPath $evidenceRoot)) {
    $evidenceParent = Split-Path -Parent $evidenceRoot
    $resolvedParent = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $evidenceParent -ErrorAction Stop).Path)
    Assert-WrapperNonReparseAncestry -Path $resolvedParent -IncludeLeaf
    [IO.Directory]::CreateDirectory($evidenceRoot) | Out-Null
    $createdEvidenceRoot = $true
}
Assert-WrapperNonReparseAncestry -Path $evidenceRoot -IncludeLeaf
$evidenceRootItem = Get-Item -Force -LiteralPath $evidenceRoot -ErrorAction Stop
if (-not $evidenceRootItem.PSIsContainer -or ($evidenceRootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'EvidenceDirectory must be a regular non-reparse directory.' }

$buildAction = {
    param([Parameter(Mandatory)][object]$Context)

    $processResult = Invoke-PackagedProcess `
        -FilePath $msbuildBefore.Path `
        -WorkingDirectory $repository `
        -Arguments @(
            $project
            '/restore'
            '/target:Rebuild'
            '/maxCpuCount:1'
            '/nodeReuse:false'
            '/property:UseSharedCompilation=false'
            '/verbosity:minimal'
            '/property:Configuration=Debug'
            '/property:Platform=x64'
            '/property:RuntimeIdentifier=win-x64'
            '/property:BuildInParallel=false'
            '/property:OpenVinoConverterPackagingRequired=false'
            '/property:OpenVinoOfficialWorkerPackagingRequired=false'
            '/property:OpenVinoTurboQuantPackagingRequired=false'
            '/property:GgufQuantizerPackagingRequired=false'
            '/property:GenerateAppxPackageOnBuild=false'
        ) `
        -TimeoutSeconds 1200
    $msbuildAfter = Get-PackagedRegularFileIdentity -Path $msbuildBefore.Path -Label 'VS Community MSBuild'
    Assert-PackagedFileIdentityUnchanged -Before $msbuildBefore -After $msbuildAfter -Label 'VS Community MSBuild'
    return [pscustomobject]@{
        ExitCode = [int]$processResult.ExitCode
    }
}

$testAction = {
    param([Parameter(Mandatory)][object]$Context)

    $processResult = Invoke-PackagedProcess `
        -FilePath $runnerBefore.Path `
        -WorkingDirectory $repository `
        -Arguments @(
            $Context.RecipePath
            '/Platform:x64'
            ('/Logger:trx;LogFileName=' + $Context.TrxName)
            ('/ResultsDirectory:' + $Context.ResultsDirectory)
            ('/TestCaseFilter:' + $Context.Filter)
        ) `
        -TimeoutSeconds 1800
    $runnerAfter = Get-PackagedRegularFileIdentity -Path $runnerBefore.Path -Label 'VS Community VSTest'
    Assert-PackagedFileIdentityUnchanged -Before $runnerBefore -After $runnerAfter -Label 'VS Community VSTest'
    return [pscustomobject]@{
        ExitCode = [int]$processResult.ExitCode
        Output = [string]$processResult.Output
        Error = [string]$processResult.Error
        RecipePathUsed = [string]$Context.RecipePath
        TrxPathUsed = [string]$Context.TrxPath
        FilterUsed = [string]$Context.Filter
    }
}

$result = $null
try {
    $coreItems = @(
        Invoke-PackagedCheckpointCore `
            -RepositoryRoot $repository `
            -RecipePath $recipe `
            -EvidenceDirectory $evidenceRoot `
            -Filter $Filter `
            -BuildAction $buildAction `
            -TestAction $testAction
    )
    if ($coreItems.Count -ne 1 -or $coreItems[0] -isnot [pscustomobject]) {
        throw 'Checkpoint core must return exactly one PSCustomObject.'
    }
    $result = $coreItems[0]
    if ($result.EvidenceClass -cne 'nonpublishable-core' -or $result.Publishable -ne $false) {
        throw 'Core evidence classification is not safe for production publication.'
    }
    $vswhereAfter = Get-PackagedRegularFileIdentity -Path $vswhereBefore.Path -Label 'VS Community discovery tool'
    $msbuildAfterCheckpoint = Get-PackagedRegularFileIdentity -Path $msbuildBefore.Path -Label 'VS Community MSBuild'
    $runnerAfterCheckpoint = Get-PackagedRegularFileIdentity -Path $runnerBefore.Path -Label 'VS Community VSTest'
    Assert-PackagedFileIdentityUnchanged -Before $vswhereBefore -After $vswhereAfter -Label 'VS Community discovery tool'
    Assert-PackagedFileIdentityUnchanged -Before $msbuildBefore -After $msbuildAfterCheckpoint -Label 'VS Community MSBuild'
    Assert-PackagedFileIdentityUnchanged -Before $runnerBefore -After $runnerAfterCheckpoint -Label 'VS Community VSTest'

    $checkpointPath = Join-Path -Path $result.OperationDirectory -ChildPath 'checkpoint.json'
    $evidence = [ordered]@{
        label = 'source/component evidence only; non-Release, non-native, non-package'
        evidenceClass = 'source-component-only'
        publishableAsReleaseNativeOrPackage = $false
        discovered = $result.Discovered
        executed = $result.Executed
        passed = $result.Passed
        failed = $result.Failed
        filter = $result.Filter
        filterSha256 = $result.FilterSha256
        testRunId = $result.TestRunId
        testIdentities = $result.TestIdentities
        preHead = $result.PreHead
        postHead = $result.PostHead
        preTree = $result.PreTree
        postTree = $result.PostTree
        preIndexTree = $result.PreIndexTree
        postIndexTree = $result.PostIndexTree
        recipeName = $result.RecipeName
        recipeSha256 = $result.RecipeSha256
        recipeLength = $result.RecipeLength
        recipeLastWriteUtc = $result.RecipeLastWriteUtc
        payloadCount = $result.PayloadCount
        payloadSetSha256 = $result.PayloadSetSha256
        unitTestAssemblySha256 = $result.UnitTestAssemblySha256
        unitTestAssemblyLength = $result.UnitTestAssemblyLength
        unitTestAssemblyLastWriteUtc = $result.UnitTestAssemblyLastWriteUtc
        trxName = $result.TrxName
        trxSha256 = $result.TrxSha256
        trxLength = $result.TrxLength
        trxLastWriteUtc = $result.TrxLastWriteUtc
        vswhere = [ordered]@{
            productName = $vswhereBefore.ProductName
            productVersion = $vswhereBefore.ProductVersion
            fileVersion = $vswhereBefore.FileVersion
            sha256 = $vswhereBefore.Sha256
            length = $vswhereBefore.Length
        }
        msbuild = [ordered]@{
            productName = $msbuildBefore.ProductName
            productVersion = $msbuildBefore.ProductVersion
            fileVersion = $msbuildBefore.FileVersion
            sha256 = $msbuildBefore.Sha256
            length = $msbuildBefore.Length
        }
        vstest = [ordered]@{
            productName = $runnerBefore.ProductName
            productVersion = $runnerBefore.ProductVersion
            fileVersion = $runnerBefore.FileVersion
            sha256 = $runnerBefore.Sha256
            length = $runnerBefore.Length
        }
        startedUtc = $result.StartedUtc
        completedUtc = $result.CompletedUtc
    }
    $utf8 = New-Object -TypeName Text.UTF8Encoding -ArgumentList $false
    $json = ConvertTo-Json -InputObject $evidence -Depth 5
    $jsonBytes = $utf8.GetBytes($json)
    $checkpointStream = New-Object -TypeName IO.FileStream -ArgumentList @(
        $checkpointPath,
        [IO.FileMode]::CreateNew,
        [IO.FileAccess]::Write,
        [IO.FileShare]::None,
        4096,
        [IO.FileOptions]::WriteThrough
    )
    try {
        $checkpointStream.Write($jsonBytes, 0, $jsonBytes.Length)
        $checkpointStream.Flush($true)
    }
    finally {
        $checkpointStream.Dispose()
    }
    Remove-PackagedPublishedTrx `
        -OperationDirectory $result.OperationDirectory `
        -TrxName $result.TrxName `
        -ExpectedSha256 $result.TrxSha256 `
        -ExpectedLength $result.TrxLength
    $publishedEntries = @([IO.Directory]::EnumerateFileSystemEntries($result.OperationDirectory))
    $ownerMarker = Join-Path -Path $result.OperationDirectory -ChildPath '.operation-owner'
    $expectedPublishedEntries = @([IO.Path]::GetFullPath($checkpointPath), [IO.Path]::GetFullPath($ownerMarker)) | Sort-Object
    $actualPublishedEntries = @($publishedEntries | ForEach-Object { [IO.Path]::GetFullPath($_) } | Sort-Object)
    if ($publishedEntries.Count -ne 2 -or ($actualPublishedEntries -join [char]0) -cne ($expectedPublishedEntries -join [char]0)) {
        throw 'Locked evidence directory must contain the sanitized checkpoint and owner marker only.'
    }
    Complete-PackagedOperationOwnership -OperationDirectory $result.OperationDirectory
}
catch {
    $failure = $_.Exception
    if ($null -ne $result -and $null -ne $result.OperationDirectory) {
        try {
            Remove-PackagedOwnedDirectory -EvidenceRoot $evidenceRoot -OperationDirectory $result.OperationDirectory
        }
        catch {
            throw 'Packaged checkpoint failed and exact operation-owned cleanup also failed.'
        }
    }
    if ($createdEvidenceRoot -and (Test-Path -LiteralPath $evidenceRoot)) {
        Assert-WrapperNonReparseAncestry -Path $evidenceRoot -IncludeLeaf
        if (@([IO.Directory]::EnumerateFileSystemEntries($evidenceRoot)).Count -ne 0) { throw 'Created evidence root was not empty after exact failure cleanup.' }
        [IO.Directory]::Delete($evidenceRoot, $false)
    }
    throw $failure
}

$summary = "Packaged checkpoint: discovered=$($result.Discovered) failed=$($result.Failed) (source/component evidence only; not Release/native/package evidence)."
$summary = [regex]::Replace($summary, '[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]', '?')
if ($summary.Length -gt 512) {
    $summary = $summary.Substring(0, 512)
}
Write-Output $summary
