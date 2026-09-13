#requires -Version 5.1

<#
.SYNOPSIS
creates the first controlled GGUF header fixtures

.DESCRIPTION
these fixtures are tiny binary test inputs

they are not usable language models and contain no model tensors
they exist only to test GGUF header validation deterministically
#>

# enable stricter PowerShell behaviour so accidental variable mistakes fail
Set-StrictMode -Version Latest

# stop immediately when any fixture-writing operation fails
$ErrorActionPreference = "Stop"

# resolve the TestFixtures directory from this script's own location
$fixtureRoot =
Split-Path `
    -Parent `
    $MyInvocation.MyCommand.Path

# build the physical path to the supported GGUF fixture directory
$ggufDirectory =
Join-Path `
    -Path $fixtureRoot `
    -ChildPath "GGUF"

# build the physical path to the deliberately malformed fixture directory
$malformedDirectory =
Join-Path `
    -Path $fixtureRoot `
    -ChildPath "Malformed"

# make sure the supported GGUF fixture directory exists
New-Item `
    -ItemType Directory `
    -Force `
    -Path $ggufDirectory |
Out-Null

# make sure the malformed GGUF fixture directory exists
New-Item `
    -ItemType Directory `
    -Force `
    -Path $malformedDirectory |
Out-Null

<#
.SYNOPSIS
writes a complete 24-byte GGUF header

.PARAMETER Path
the output fixture path

.PARAMETER Magic
exactly four ASCII characters used as the file signature

.PARAMETER Version
the GGUF format version written as a 32-bit unsigned integer

.PARAMETER TensorCount
the number of tensor descriptions declared by the fixture

.PARAMETER MetadataCount
the number of metadata entries declared by the fixture
#>
function Write-CompleteGgufHeader {
    param
    (
        # require a destination path for every generated fixture
        [Parameter(Mandatory)]
        [string]
        $Path,

        # require exactly four characters for the file signature
        [Parameter(Mandatory)]
        [ValidateLength(4, 4)]
        [string]
        $Magic,

        # require the GGUF version value
        [Parameter(Mandatory)]
        [uint32]
        $Version,

        # default to no tensors because this first fixture slice tests headers
        [uint64]
        $TensorCount = 0,

        # default to no metadata because metadata parsing comes later
        [uint64]
        $MetadataCount = 0
    )

    # convert the four-character signature into its ASCII byte representation
    [byte[]] $magicBytes =
    [System.Text.Encoding]::ASCII.GetBytes($Magic)

    # create or replace the destination fixture file
    [System.IO.FileStream] $stream =
    [System.IO.File]::Open(
        $Path,
        [System.IO.FileMode]::Create,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::None)

    # create a binary writer that writes primitive values in little-endian order
    [System.IO.BinaryWriter] $writer =
    [System.IO.BinaryWriter]::new($stream)

    try {
        # write the four magic bytes, normally G G U F
        $writer.Write($magicBytes)

        # write the 32-bit GGUF version
        $writer.Write($Version)

        # write the 64-bit declared tensor count
        $writer.Write($TensorCount)

        # write the 64-bit declared metadata key/value count
        $writer.Write($MetadataCount)
    }
    finally {
        # close the binary writer and its underlying file stream
        $writer.Dispose()
    }
}

# build the path for the supported GGUF v3 header fixture
$validHeaderPath =
Join-Path `
    -Path $ggufDirectory `
    -ChildPath "H-001-valid-v3-header.gguf"

# create a structurally complete GGUF v3 header
Write-CompleteGgufHeader `
    -Path $validHeaderPath `
    -Magic "GGUF" `
    -Version 3 `
    -TensorCount 0 `
    -MetadataCount 0

# build the path for the intentionally empty fixture
$emptyFilePath =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-000-empty-file.gguf"

# create an intentionally empty zero-byte fixture
[System.IO.File]::WriteAllBytes(
    $emptyFilePath,
    [byte[]] @())

# build the path for the invalid-magic fixture
$invalidMagicPath =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-001-invalid-magic.gguf"

# write a complete header that deliberately starts with TEST instead of GGUF
Write-CompleteGgufHeader `
    -Path $invalidMagicPath `
    -Magic "TEST" `
    -Version 3 `
    -TensorCount 0 `
    -MetadataCount 0

# build the path for the unsupported-version fixture
$unsupportedVersionPath =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-002-unsupported-version.gguf"

# write a correct GGUF signature but an intentionally unsupported version
Write-CompleteGgufHeader `
    -Path $unsupportedVersionPath `
    -Magic "GGUF" `
    -Version 99 `
    -TensorCount 0 `
    -MetadataCount 0

# build the path for the truncated-header fixture
$truncatedHeaderPath =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-003-truncated-header.gguf"

# create or replace the truncated-header file
[System.IO.FileStream] $truncatedStream =
[System.IO.File]::Open(
    $truncatedHeaderPath,
    [System.IO.FileMode]::Create,
    [System.IO.FileAccess]::Write,
    [System.IO.FileShare]::None)

# create a writer for the deliberately incomplete header
[System.IO.BinaryWriter] $truncatedWriter =
[System.IO.BinaryWriter]::new($truncatedStream)

try {
    # write the correct four-byte GGUF signature
    $truncatedWriter.Write(
        [System.Text.Encoding]::ASCII.GetBytes("GGUF"))

    # write a supported version number
    $truncatedWriter.Write([uint32] 3)

    # write only half of the required 64-bit tensor count
    # the file deliberately ends here to simulate truncation
    $truncatedWriter.Write([uint32] 0)
}
finally {
    # close the deliberately truncated fixture
    $truncatedWriter.Dispose()
}

# build the path for a short header with an invalid signature
$shortInvalidMagicPath =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-024-short-invalid-magic.gguf"

# create or replace the deliberately short invalid-signature fixture
[System.IO.FileStream] $shortInvalidMagicStream =
[System.IO.File]::Open(
    $shortInvalidMagicPath,
    [System.IO.FileMode]::Create,
    [System.IO.FileAccess]::Write,
    [System.IO.FileShare]::None)

# create a writer for the incomplete fixed header
[System.IO.BinaryWriter] $shortInvalidMagicWriter =
[System.IO.BinaryWriter]::new($shortInvalidMagicStream)

try {
    # write an invalid four-byte signature before the header ends early
    $shortInvalidMagicWriter.Write(
        [System.Text.Encoding]::ASCII.GetBytes("TEST"))

    # write a supported version so the remaining fixed-header bytes are reached
    $shortInvalidMagicWriter.Write([uint32] 3)

    # write only half of tensor count so the file ends at offset 12
    $shortInvalidMagicWriter.Write([uint32] 0)
}
finally {
    # close the generated short invalid-signature fixture
    $shortInvalidMagicWriter.Dispose()
}

# display the generated fixture names and their exact sizes
Get-ChildItem `
    -Path $ggufDirectory, $malformedDirectory `
    -Filter "*.gguf" |
Sort-Object FullName |
Select-Object Name, Length, FullName
