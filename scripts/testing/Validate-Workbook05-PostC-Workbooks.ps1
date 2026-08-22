[CmdletBinding()]
param(
    # Resolve the repository root from scripts/testing by default.
    [string] $RepositoryRoot = (
        Resolve-Path (Join-Path $PSScriptRoot '..\..')
    ).Path,

    # The workflow pins Python 3.12.10 and passes the application name here.
    [string] $PythonPath = 'python',

    # When supplied, stage a complete text/DOCX validation artifact here.
    [string] $ArtifactDirectory = ''
)

Set-StrictMode -Version Latest
# Windows PowerShell 5.1 lacks System.IO.Path.GetRelativePath.
if (-not ('Wb05PathCompat' -as [type])) {
    Add-Type -TypeDefinition @"
using System;
using System.IO;
public static class Wb05PathCompat
{
    public static string GetRelativePath(string relativeTo, string path)
    {
        string basePath = Path.GetFullPath(relativeTo);
        if (!basePath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            basePath += Path.DirectorySeparatorChar;
        Uri baseUri = new Uri(basePath, UriKind.Absolute);
        Uri targetUri = new Uri(Path.GetFullPath(path), UriKind.Absolute);
        return Uri.UnescapeDataString(baseUri.MakeRelativeUri(targetUri).ToString())
            .Replace('/', Path.DirectorySeparatorChar);
    }
}
"@
}
$ErrorActionPreference = 'Stop'

# Resolve only one real Python application. This avoids the WindowsApps alias
# ambiguity that previously affected Workbook 05 dependency collection.
$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$pythonCommand = Get-Command `
    -Name $PythonPath `
    -CommandType Application `
    -All `
    -ErrorAction Stop |
    Select-Object -First 1
$resolvedPython = $pythonCommand.Source

# Use only a new job-local temporary directory. The gate never deletes or
# rewrites a retained model, build, experiment, or accepted evidence directory.
$temporaryBase = if ($env:RUNNER_TEMP) {
    $env:RUNNER_TEMP
}
else {
    [IO.Path]::GetTempPath()
}
$temporaryRoot = Join-Path `
    $temporaryBase `
    ('workbook05-post-c-' + [guid]::NewGuid().ToString('N'))
$firstOutput = Join-Path $temporaryRoot 'first'
$secondOutput = Join-Path $temporaryRoot 'second'
$reportDirectory = Join-Path $temporaryRoot 'reports'
New-Item -ItemType Directory -Path $firstOutput -Force:$false | Out-Null
New-Item -ItemType Directory -Path $secondOutput -Force:$false | Out-Null
New-Item -ItemType Directory -Path $reportDirectory -Force:$false | Out-Null

Push-Location -LiteralPath $RepositoryRoot
try {
    # Run the focused acceptance suite before using the generator. The tests
    # cover the seven workbooks, 372-row execution index, safe paths, hashes,
    # 30B non-claims, deterministic generation, and hosted workflow contract.
    & $resolvedPython -m unittest -v `
        tests.testing.workbook05.test_post_c_workbook_pack
    if ($LASTEXITCODE -ne 0) {
        throw "Post-C workbook tests exited with code $LASTEXITCODE."
    }

    # Generate the complete post-C workbook pack twice from absent outputs.
    # Byte equality proves that timestamps, ZIP member order, and content are
    # deterministic rather than dependent on one workstation or execution time.
    foreach ($outputDirectory in @($firstOutput, $secondOutput)) {
        & $resolvedPython `
            '.\scripts\testing\Generate-Controlled-Workbooks.py' `
            --repository-root $RepositoryRoot `
            --output-directory $outputDirectory `
            --post-c-only
        if ($LASTEXITCODE -ne 0) {
            throw "Post-C DOCX generation exited with code $LASTEXITCODE."
        }
    }

    $firstFiles = @(
        Get-ChildItem -LiteralPath $firstOutput -File -Filter '*.docx' |
            Sort-Object Name
    )
    $secondFiles = @(
        Get-ChildItem -LiteralPath $secondOutput -File -Filter '*.docx' |
            Sort-Object Name
    )
    if ($firstFiles.Count -ne 7 -or $secondFiles.Count -ne 7) {
        throw (
            'Post-C generation must create exactly seven DOCX files in ' +
            'each clean output directory.'
        )
    }
    if (($firstFiles.Name -join "`n") -cne ($secondFiles.Name -join "`n")) {
        throw 'The two post-C generation passes produced different filenames.'
    }
    foreach ($firstFile in $firstFiles) {
        $secondPath = Join-Path $secondOutput $firstFile.Name
        $firstHash = (Get-FileHash -LiteralPath $firstFile.FullName -Algorithm SHA256).Hash
        $secondHash = (Get-FileHash -LiteralPath $secondPath -Algorithm SHA256).Hash
        if ($firstHash -cne $secondHash) {
            throw "Generated DOCX is not byte-deterministic: $($firstFile.Name)"
        }
    }

    # Validate both the committed generated working copies and a new clean
    # generation. This catches stale committed hashes as well as generator drift.
    $committedReport = Join-Path $reportDirectory 'committed-validation.json'
    & $resolvedPython -m scripts.testing.workbook05.post_c_workbook_pack `
        --repository-root $RepositoryRoot `
        --report $committedReport
    if ($LASTEXITCODE -ne 0) {
        throw "Committed post-C workbook validation exited with code $LASTEXITCODE."
    }

    $generatedReport = Join-Path $reportDirectory 'clean-generation-validation.json'
    & $resolvedPython -m scripts.testing.workbook05.post_c_workbook_pack `
        --repository-root $RepositoryRoot `
        --generated-directory $secondOutput `
        --report $generatedReport
    if ($LASTEXITCODE -ne 0) {
        throw "Clean post-C generation validation exited with code $LASTEXITCODE."
    }

    # Stage one exact artifact when requested. The artifact contains canonical
    # text, machine-readable controls, the deterministic DOCX files, and reports;
    # it contains no model, runtime binary, executable, wheel, or archive.
    if (-not [string]::IsNullOrWhiteSpace($ArtifactDirectory)) {
        if (Test-Path -LiteralPath $ArtifactDirectory) {
            throw "ArtifactDirectory already exists: $ArtifactDirectory"
        }
        $artifactRoot = New-Item `
            -ItemType Directory `
            -Path $ArtifactDirectory `
            -Force:$false
        $artifactGenerated = New-Item `
            -ItemType Directory `
            -Path (Join-Path $artifactRoot.FullName 'generated') `
            -Force:$false
        $artifactCanonical = New-Item `
            -ItemType Directory `
            -Path (Join-Path $artifactRoot.FullName 'canonical') `
            -Force:$false
        $artifactReports = New-Item `
            -ItemType Directory `
            -Path (Join-Path $artifactRoot.FullName 'reports') `
            -Force:$false

        Get-ChildItem `
            -LiteralPath $secondOutput `
            -File `
            -Filter '*.docx' |
            Copy-Item -Destination $artifactGenerated.FullName
        Get-ChildItem `
            -LiteralPath '.\docs\testing\workbooks\text-templates' `
            -File |
            Where-Object {
                $_.Name -match '^(0[7-9]|1[0-3])_.*\.md$'
            } |
            Copy-Item -Destination $artifactCanonical.FullName

        $controlledFiles = @(
            '.\docs\testing\Workbook-05-Post-C-Execution-Index-v1.csv',
            '.\docs\testing\Workbook-05-Post-C-Configuration-Matrix-v1.csv',
            '.\docs\testing\Workbook-05-Post-C-Evidence-Register-v1.csv',
            '.\docs\testing\Workbook-05-Post-C-Revision-Register-v1.csv',
            '.\docs\testing\workbooks\Workbook-05-Post-C-Pack-Manifest-v1.json',
            '.\docs\testing\workbooks\Controlled-Workbook-Manifest.csv'
        )
        foreach ($controlledFile in $controlledFiles) {
            Copy-Item `
                -LiteralPath $controlledFile `
                -Destination $artifactCanonical.FullName
        }
        Get-ChildItem `
            -LiteralPath $reportDirectory `
            -File `
            -Filter '*.json' |
            Copy-Item -Destination $artifactReports.FullName

        # Write one deterministic digest catalogue using artifact-relative paths.
        $manifestPath = Join-Path $artifactRoot.FullName 'artifact-manifest.sha256'
        $manifestLines = @(
            Get-ChildItem `
                -LiteralPath $artifactRoot.FullName `
                -File `
                -Recurse |
            Where-Object { $_.FullName -ne $manifestPath } |
            Sort-Object FullName |
            ForEach-Object {
                $relative = [Wb05PathCompat]::GetRelativePath(
                    $artifactRoot.FullName,
                    $_.FullName
                ).Replace('\', '/')
                $digest = (
                    Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
                ).Hash.ToLowerInvariant()
                "$digest  $relative"
            }
        )
        [IO.File]::WriteAllLines(
            $manifestPath,
            $manifestLines,
            [Text.UTF8Encoding]::new($false)
        )
    }

    # No later success marker may hide repository conflict markers or whitespace
    # errors introduced by this workbook package.
    & git diff --check
    if ($LASTEXITCODE -ne 0) {
        throw "git diff --check exited with code $LASTEXITCODE."
    }

    Write-Host 'WORKBOOK05_POST_C_WORKBOOK_PACK_PASS'
}
finally {
    Pop-Location
}
