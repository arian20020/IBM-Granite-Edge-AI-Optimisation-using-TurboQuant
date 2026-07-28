#requires -Version 5.1

<#
.SYNOPSIS
Creates the first controlled GGUF header fixtures.

.DESCRIPTION
These fixtures are tiny binary test inputs.

They are not usable language models and contain no model tensors.
They exist only to test GGUF header validation deterministically.
#>

# Enable stricter PowerShell behaviour so accidental variable mistakes fail.
Set-StrictMode -Version Latest

# Stop immediately when any fixture-writing operation fails.
$ErrorActionPreference = "Stop"

# Resolve the TestFixtures directory from this script's own location.
$fixtureRoot =
Split-Path `
    -Parent `
    $MyInvocation.MyCommand.Path

# Build the physical path to the supported GGUF fixture directory.
$ggufDirectory =
Join-Path `
    -Path $fixtureRoot `
    -ChildPath "GGUF"

# Build the physical path to the deliberately malformed fixture directory.
$malformedDirectory =
Join-Path `
    -Path $fixtureRoot `
    -ChildPath "Malformed"

# Make sure the supported GGUF fixture directory exists.
New-Item `
    -ItemType Directory `
    -Force `
    -Path $ggufDirectory |
Out-Null

# Make sure the malformed GGUF fixture directory exists.
New-Item `
    -ItemType Directory `
    -Force `
    -Path $malformedDirectory |
Out-Null

<#
.SYNOPSIS
Writes a complete 24-byte GGUF header.

.PARAMETER Path
The output fixture path.

.PARAMETER Magic
Exactly four ASCII characters used as the file signature.

.PARAMETER Version
The GGUF format version written as a 32-bit unsigned integer.

.PARAMETER TensorCount
The number of tensor descriptions declared by the fixture.

.PARAMETER MetadataCount
The number of metadata entries declared by the fixture.
#>
function Write-CompleteGgufHeader {
    param
    (
        # Require a destination path for every generated fixture.
        [Parameter(Mandatory)]
        [string]
        $Path,

        # Require exactly four characters for the file signature.
        [Parameter(Mandatory)]
        [ValidateLength(4, 4)]
        [string]
        $Magic,

        # Require the GGUF version value.
        [Parameter(Mandatory)]
        [uint32]
        $Version,

        # Default to no tensors because this first fixture slice tests headers.
        [uint64]
        $TensorCount = 0,

        # Default to no metadata because metadata parsing comes later.
        [uint64]
        $MetadataCount = 0
    )

    # Convert the four-character signature into its ASCII byte representation.
    [byte[]] $magicBytes =
    [System.Text.Encoding]::ASCII.GetBytes($Magic)

    # Create or replace the destination fixture file.
    [System.IO.FileStream] $stream =
    [System.IO.File]::Open(
        $Path,
        [System.IO.FileMode]::Create,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::None)

    # Create a binary writer that writes primitive values in little-endian order.
    [System.IO.BinaryWriter] $writer =
    [System.IO.BinaryWriter]::new($stream)

    try {
        # Write the four magic bytes, normally G G U F.
        $writer.Write($magicBytes)

        # Write the 32-bit GGUF version.
        $writer.Write($Version)

        # Write the 64-bit declared tensor count.
        $writer.Write($TensorCount)

        # Write the 64-bit declared metadata key/value count.
        $writer.Write($MetadataCount)
    }
    finally {
        # Close the binary writer and its underlying file stream.
        $writer.Dispose()
    }
}

# Build the path for the supported GGUF v3 header fixture.
$validHeaderPath =
Join-Path `
    -Path $ggufDirectory `
    -ChildPath "H-001-valid-v3-header.gguf"

# Create a structurally complete GGUF v3 header.
Write-CompleteGgufHeader `
    -Path $validHeaderPath `
    -Magic "GGUF" `
    -Version 3 `
    -TensorCount 0 `
    -MetadataCount 0

# Build the path for the intentionally empty fixture.
$emptyFilePath =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-000-empty-file.gguf"

# Create an intentionally empty zero-byte fixture.
[System.IO.File]::WriteAllBytes(
    $emptyFilePath,
    [byte[]] @())

# Build the path for the invalid-magic fixture.
$invalidMagicPath =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-001-invalid-magic.gguf"

# Write a complete header that deliberately starts with TEST instead of GGUF.
Write-CompleteGgufHeader `
    -Path $invalidMagicPath `
    -Magic "TEST" `
    -Version 3 `
    -TensorCount 0 `
    -MetadataCount 0

# Build the path for the unsupported-version fixture.
$unsupportedVersionPath =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-002-unsupported-version.gguf"

# Write a correct GGUF signature but an intentionally unsupported version.
Write-CompleteGgufHeader `
    -Path $unsupportedVersionPath `
    -Magic "GGUF" `
    -Version 99 `
    -TensorCount 0 `
    -MetadataCount 0

# Build the path for the truncated-header fixture.
$truncatedHeaderPath =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-003-truncated-header.gguf"

# Create or replace the truncated-header file.
[System.IO.FileStream] $truncatedStream =
[System.IO.File]::Open(
    $truncatedHeaderPath,
    [System.IO.FileMode]::Create,
    [System.IO.FileAccess]::Write,
    [System.IO.FileShare]::None)

# Create a writer for the deliberately incomplete header.
[System.IO.BinaryWriter] $truncatedWriter =
[System.IO.BinaryWriter]::new($truncatedStream)

try {
    # Write the correct four-byte GGUF signature.
    $truncatedWriter.Write(
        [System.Text.Encoding]::ASCII.GetBytes("GGUF"))

    # Write a supported version number.
    $truncatedWriter.Write([uint32] 3)

    # Write only half of the required 64-bit tensor count.
    # The file deliberately ends here to simulate truncation.
    $truncatedWriter.Write([uint32] 0)
}
finally {
    # Close the deliberately truncated fixture.
    $truncatedWriter.Dispose()
}

# Build the path for a short header with an invalid signature.
$shortInvalidMagicPath =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-024-short-invalid-magic.gguf"

# Create or replace the deliberately short invalid-signature fixture.
[System.IO.FileStream] $shortInvalidMagicStream =
[System.IO.File]::Open(
    $shortInvalidMagicPath,
    [System.IO.FileMode]::Create,
    [System.IO.FileAccess]::Write,
    [System.IO.FileShare]::None)

# Create a writer for the incomplete fixed header.
[System.IO.BinaryWriter] $shortInvalidMagicWriter =
[System.IO.BinaryWriter]::new($shortInvalidMagicStream)

try {
    # Write an invalid four-byte signature before the header ends early.
    $shortInvalidMagicWriter.Write(
        [System.Text.Encoding]::ASCII.GetBytes("TEST"))

    # Write a supported version so the remaining fixed-header bytes are reached.
    $shortInvalidMagicWriter.Write([uint32] 3)

    # Write only half of tensor count so the file ends at offset 12.
    $shortInvalidMagicWriter.Write([uint32] 0)
}
finally {
    # Close the generated short invalid-signature fixture.
    $shortInvalidMagicWriter.Dispose()
}

# Display the generated fixture names and their exact sizes.
Get-ChildItem `
    -Path $ggufDirectory, $malformedDirectory `
    -Filter "*.gguf" |
Sort-Object FullName |
Select-Object Name, Length, FullName
