#requires -Version 5.1

<#
.SYNOPSIS
Recreates the complete controlled GGUF fixture set.

.DESCRIPTION
This is the authoritative fixture-generation entry point. It removes every
script-owned GGUF binary, expected-result JSON file, and integrity manifest
before invoking the header and metadata generators in order. Starting from an
empty generated-output set ensures removed or renamed writer calls cannot leave
stale fixtures that appear reproducible.
#>

# Use strict PowerShell behaviour so undeclared variables cause an error.
Set-StrictMode -Version Latest

# Stop immediately when cleanup or either leaf generator fails.
$ErrorActionPreference = "Stop"

# Resolve the TestFixtures directory from this script's own location.
$fixtureRoot =
Split-Path `
    -Parent `
    $MyInvocation.MyCommand.Path

$fixtureRootFullPath =
[System.IO.Path]::GetFullPath(
    $fixtureRoot)

$fixtureRootPrefix =
$fixtureRootFullPath.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) +
[System.IO.Path]::DirectorySeparatorChar

<#
.SYNOPSIS
Confirms that a cleanup target is a child of TestFixtures.
#>
function Assert-FixtureChildPath {
    param
    (
        [Parameter(Mandatory)]
        [string]
        $Path
    )

    $fullPath =
    [System.IO.Path]::GetFullPath(
        $Path)

    if (
        -not $fullPath.StartsWith(
            $fixtureRootPrefix,
            [System.StringComparison]::OrdinalIgnoreCase)
    ) {
        throw "Refusing to clean a path outside TestFixtures: '$fullPath'."
    }

    return $fullPath
}

<#
.SYNOPSIS
Removes generated files matching one extension beneath a controlled directory.
#>
function Remove-GeneratedFixtureFiles {
    param
    (
        [Parameter(Mandatory)]
        [string]
        $Directory,

        [Parameter(Mandatory)]
        [string]
        $Filter
    )

    $safeDirectory =
    Assert-FixtureChildPath `
        -Path $Directory

    New-Item `
        -ItemType Directory `
        -Force `
        -Path $safeDirectory |
    Out-Null

    Get-ChildItem `
        -LiteralPath $safeDirectory `
        -Filter $Filter `
        -File `
        -Recurse |
    ForEach-Object {
        $safeFile =
        Assert-FixtureChildPath `
            -Path $_.FullName

        Remove-Item `
            -LiteralPath $safeFile `
            -Force
    }
}

# Clear every output owned by either leaf generator.
Remove-GeneratedFixtureFiles `
    -Directory (
        Join-Path `
            -Path $fixtureRoot `
            -ChildPath "GGUF") `
    -Filter "*.gguf"

Remove-GeneratedFixtureFiles `
    -Directory (
        Join-Path `
            -Path $fixtureRoot `
            -ChildPath "Malformed") `
    -Filter "*.gguf"

Remove-GeneratedFixtureFiles `
    -Directory (
        Join-Path `
            -Path $fixtureRoot `
            -ChildPath "ExpectedMetadata") `
    -Filter "*.json"

$manifestPath =
Assert-FixtureChildPath `
    -Path (
        Join-Path `
            -Path $fixtureRoot `
            -ChildPath "fixture-manifest.json")

if (
    Test-Path `
        -LiteralPath $manifestPath `
        -PathType Leaf
) {
    Remove-Item `
        -LiteralPath $manifestPath `
        -Force
}

# Generate header fixtures first, then metadata fixtures and the final manifest.
$headerGeneratorPath =
Assert-FixtureChildPath `
    -Path (
        Join-Path `
            -Path $fixtureRoot `
            -ChildPath "Generate-GgufHeaderFixtures.ps1")

$metadataGeneratorPath =
Assert-FixtureChildPath `
    -Path (
        Join-Path `
            -Path $fixtureRoot `
            -ChildPath "Generate-GgufMetadataFixtures.ps1")

& $headerGeneratorPath
& $metadataGeneratorPath -SkipOutputCleanup

if (
    -not (
        Test-Path `
            -LiteralPath $manifestPath `
            -PathType Leaf)
) {
    throw "Fixture generation did not create '$manifestPath'."
}
