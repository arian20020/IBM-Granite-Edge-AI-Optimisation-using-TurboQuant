#requires -Version 5.1

<#
.SYNOPSIS
Creates controlled GGUF metadata fixtures.

.DESCRIPTION
The generated files are tiny binary test inputs.

They are not usable language models and contain no model tensors.
They exist only to exercise GGUF metadata parsing and validation.
#>

[CmdletBinding()]
param
(
    # The authoritative orchestrator already removed every generated output.
    [switch]
    $SkipOutputCleanup
)

# Use strict PowerShell behaviour so undeclared variables cause an error.
Set-StrictMode -Version Latest

# Stop the generator immediately if any operation fails.
$ErrorActionPreference = "Stop"

# Confirm that the current computer writes little-endian primitive values.
# GGUF files are normally little-endian unless stated otherwise.
if (-not [System.BitConverter]::IsLittleEndian) {
    throw "This fixture generator currently supports little-endian systems only."
}

# Find the TestFixtures folder using the location of this script.
$fixtureRoot =
Split-Path `
    -Parent `
    $MyInvocation.MyCommand.Path

# Build the valid GGUF fixture directory path.
$ggufDirectory =
Join-Path `
    -Path $fixtureRoot `
    -ChildPath "GGUF"

# Build the malformed GGUF fixture directory path.
$malformedDirectory =
Join-Path `
    -Path $fixtureRoot `
    -ChildPath "Malformed"

# Build the expected-results directory path.
$expectedMetadataDirectory =
Join-Path `
    -Path $fixtureRoot `
    -ChildPath "ExpectedMetadata"

# Ensure that all required directories exist.
New-Item `
    -ItemType Directory `
    -Force `
    -Path $ggufDirectory |
Out-Null

New-Item `
    -ItemType Directory `
    -Force `
    -Path $malformedDirectory |
Out-Null

New-Item `
    -ItemType Directory `
    -Force `
    -Path $expectedMetadataDirectory |
Out-Null

if (-not $SkipOutputCleanup) {
    # Standalone metadata generation preserves the currently known header
    # outputs. The authoritative all-fixture orchestrator performs a complete
    # pre-clean and uses -SkipOutputCleanup so newly added header outputs are
    # never removed by this compatibility path.
    $headerOwnedGgufPaths =
    @(
        Join-Path `
            -Path $ggufDirectory `
            -ChildPath "H-001-valid-v3-header.gguf"
    )

    $headerOwnedMalformedPaths =
    @(
        Join-Path -Path $malformedDirectory -ChildPath "I-000-empty-file.gguf"
        Join-Path -Path $malformedDirectory -ChildPath "I-001-invalid-magic.gguf"
        Join-Path -Path $malformedDirectory -ChildPath "I-002-unsupported-version.gguf"
        Join-Path -Path $malformedDirectory -ChildPath "I-003-truncated-header.gguf"
        Join-Path -Path $malformedDirectory -ChildPath "I-024-short-invalid-magic.gguf"
    )

    Get-ChildItem `
        -LiteralPath $ggufDirectory `
        -Filter "*.gguf" `
        -File `
        -Recurse |
    Where-Object {
        $_.FullName -notin $headerOwnedGgufPaths
    } |
    Remove-Item -Force

    Get-ChildItem `
        -LiteralPath $malformedDirectory `
        -Filter "*.gguf" `
        -File `
        -Recurse |
    Where-Object {
        $_.FullName -notin $headerOwnedMalformedPaths
    } |
    Remove-Item -Force

    Get-ChildItem `
        -LiteralPath $expectedMetadataDirectory `
        -Filter "*.json" `
        -File `
        -Recurse |
    Remove-Item -Force
}

# ---------------------------------------------------------------------
# GGUF metadata type identifiers
# ---------------------------------------------------------------------

# GGUF uint8 metadata value type.
[uint32] $GgufTypeUInt8 = 0

# GGUF int8 metadata value type.
[uint32] $GgufTypeInt8 = 1

# GGUF uint16 metadata value type.
[uint32] $GgufTypeUInt16 = 2

# GGUF int16 metadata value type.
[uint32] $GgufTypeInt16 = 3

# GGUF uint32 metadata value type.
[uint32] $GgufTypeUInt32 = 4

# GGUF int32 metadata value type.
[uint32] $GgufTypeInt32 = 5

# GGUF float32 metadata value type.
[uint32] $GgufTypeFloat32 = 6

# GGUF boolean metadata value type.
[uint32] $GgufTypeBoolean = 7

# GGUF UTF-8 string metadata value type.
[uint32] $GgufTypeString = 8

# GGUF array metadata value type.
[uint32] $GgufTypeArray = 9

# GGUF uint64 metadata value type.
[uint32] $GgufTypeUInt64 = 10

# GGUF int64 metadata value type.
[uint32] $GgufTypeInt64 = 11

# GGUF float64 metadata value type.
[uint32] $GgufTypeFloat64 = 12

# ---------------------------------------------------------------------
# General binary-writing helpers
# ---------------------------------------------------------------------

<#
.SYNOPSIS
Creates a BinaryWriter for a new fixture file.
#>
function New-FixtureBinaryWriter {
    param
    (
        # Require the output file path.
        [Parameter(Mandatory)]
        [string]
        $Path
    )

    # Create or replace the destination file.
    [System.IO.FileStream] $stream =
    [System.IO.File]::Open(
        $Path,
        [System.IO.FileMode]::Create,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::None)

    # Return a binary writer that owns the file stream.
    return [System.IO.BinaryWriter]::new($stream)
}

<#
.SYNOPSIS
Writes the standard GGUF v3 header.
#>
function Write-GgufHeader {
    param
    (
        # Receive the destination binary writer.
        [Parameter(Mandatory)]
        [System.IO.BinaryWriter]
        $Writer,

        # Allow malformed fixtures to declare another version.
        [uint32]
        $Version = 3,

        # This scanner fixture set does not include tensors.
        [uint64]
        $TensorCount = 0,

        # Declare how many metadata entries follow the header.
        [Parameter(Mandatory)]
        [uint64]
        $MetadataCount
    )

    # Write the required four-byte GGUF signature.
    $Writer.Write(
        [System.Text.Encoding]::ASCII.GetBytes("GGUF"))

    # Write the GGUF format version.
    $Writer.Write($Version)

    # Write the declared tensor count.
    $Writer.Write($TensorCount)

    # Write the declared metadata key/value count.
    $Writer.Write($MetadataCount)
}

<#
.SYNOPSIS
Writes a GGUF UTF-8 string.

.DESCRIPTION
A GGUF string is an unsigned 64-bit byte length followed by UTF-8 bytes.
It is not null terminated.
#>
function Write-GgufString {
    param
    (
        # Receive the destination writer.
        [Parameter(Mandatory)]
        [System.IO.BinaryWriter]
        $Writer,

        # Receive the text that must be encoded.
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string]
        $Value
    )

    # Convert the text into UTF-8 bytes.
    [byte[]] $utf8Bytes =
    [System.Text.Encoding]::UTF8.GetBytes($Value)

    # Write the number of UTF-8 bytes.
    $Writer.Write([uint64] $utf8Bytes.Length)

    # Write the actual UTF-8 bytes.
    $Writer.Write($utf8Bytes)
}

<#
.SYNOPSIS
Creates an in-memory description of one metadata entry.
#>
function New-GgufEntry {
    param
    (
        # Receive the metadata key.
        [Parameter(Mandatory)]
        [string]
        $Key,

        # Receive the GGUF metadata type identifier.
        [Parameter(Mandatory)]
        [uint32]
        $Type,

        # Receive the metadata value.
        [Parameter(Mandatory)]
        $Value
    )

    # Return a simple object that preserves the key, type and value.
    return [pscustomobject] @{
        Key   = $Key
        Type  = $Type
        Value = $Value
    }
}

<#
.SYNOPSIS
Creates an in-memory GGUF array value.
#>
function New-GgufArrayValue {
    param
    (
        # Receive the metadata type of every array element.
        [Parameter(Mandatory)]
        [uint32]
        $ElementType,

        # Receive the values contained by the array.
        [Parameter(Mandatory)]
        [object[]]
        $Values
    )

    # Return the element type and values together.
    return [pscustomobject] @{
        ElementType = $ElementType
        Values      = @($Values)
    }
}

<#
.SYNOPSIS
Writes one supported GGUF metadata value.
#>
function Write-GgufValue {
    param
    (
        # Receive the destination writer.
        [Parameter(Mandatory)]
        [System.IO.BinaryWriter]
        $Writer,

        # Receive the GGUF metadata type.
        [Parameter(Mandatory)]
        [uint32]
        $Type,

        # Receive the value to write.
        [Parameter(Mandatory)]
        $Value
    )

    # Select the correct binary representation for the supplied type.
    switch ($Type) {
        # Write an 8-bit unsigned integer.
        0 {
            $Writer.Write([byte] $Value)
            break
        }

        # Write an 8-bit signed integer.
        1 {
            $Writer.Write([sbyte] $Value)
            break
        }

        # Write a 16-bit unsigned integer.
        2 {
            $Writer.Write([uint16] $Value)
            break
        }

        # Write a 16-bit signed integer.
        3 {
            $Writer.Write([int16] $Value)
            break
        }

        # Write a 32-bit unsigned integer.
        4 {
            $Writer.Write([uint32] $Value)
            break
        }

        # Write a 32-bit signed integer.
        5 {
            $Writer.Write([int32] $Value)
            break
        }

        # Write a 32-bit IEEE 754 floating-point value.
        6 {
            $Writer.Write([single] $Value)
            break
        }

        # Write a GGUF Boolean as the exact byte 0 or 1.
        7 {
            [byte] $booleanByte =
            if ([bool] $Value) {
                1
            }
            else {
                0
            }

            $Writer.Write($booleanByte)
            break
        }

        # Write a GGUF string.
        8 {
            Write-GgufString `
                -Writer $Writer `
                -Value ([string] $Value)

            break
        }

        # Write a GGUF array.
        9 {
            # Read the element type stored in the array description.
            [uint32] $elementType =
            [uint32] $Value.ElementType

            # Preserve the supplied values as an array.
            [object[]] $values =
            @($Value.Values)

            # Write the type shared by all array elements.
            $Writer.Write($elementType)

            # Write the number of elements, not the byte count.
            $Writer.Write([uint64] $values.Count)

            # Write every element using its declared GGUF type.
            foreach ($item in $values) {
                Write-GgufValue `
                    -Writer $Writer `
                    -Type $elementType `
                    -Value $item
            }

            break
        }

        # Write a 64-bit unsigned integer.
        10 {
            $Writer.Write([uint64] $Value)
            break
        }

        # Write a 64-bit signed integer.
        11 {
            $Writer.Write([int64] $Value)
            break
        }

        # Write a 64-bit IEEE 754 floating-point value.
        12 {
            $Writer.Write([double] $Value)
            break
        }

        # Reject types that this controlled generator did not request.
        default {
            throw "Unsupported generator metadata type: $Type"
        }
    }
}

<#
.SYNOPSIS
Writes a complete metadata entry.
#>
function Write-GgufMetadataEntry {
    param
    (
        # Receive the destination writer.
        [Parameter(Mandatory)]
        [System.IO.BinaryWriter]
        $Writer,

        # Receive the entry description.
        [Parameter(Mandatory)]
        $Entry
    )

    # Write the metadata key as a GGUF string.
    Write-GgufString `
        -Writer $Writer `
        -Value ([string] $Entry.Key)

    # Write the metadata value-type identifier.
    $Writer.Write([uint32] $Entry.Type)

    # Write the value using the declared metadata type.
    Write-GgufValue `
        -Writer $Writer `
        -Type ([uint32] $Entry.Type) `
        -Value $Entry.Value
}

<#
.SYNOPSIS
Writes a structurally complete GGUF v3 metadata fixture.
#>
function Write-GgufMetadataFile {
    param
    (
        # Receive the destination fixture path.
        [Parameter(Mandatory)]
        [string]
        $Path,

        # Receive the ordered metadata entries.
        [Parameter(Mandatory)]
        [object[]]
        $Entries
    )

    # Create a writer for the fixture.
    [System.IO.BinaryWriter] $writer =
    New-FixtureBinaryWriter `
        -Path $Path

    try {
        # Write a GGUF v3 header with no tensors.
        Write-GgufHeader `
            -Writer $writer `
            -Version 3 `
            -TensorCount 0 `
            -MetadataCount ([uint64] $Entries.Count)

        # Write the metadata entries in their supplied order.
        foreach ($entry in $Entries) {
            Write-GgufMetadataEntry `
                -Writer $writer `
                -Entry $entry
        }

        # GGUF uses a default alignment of 32 when none is supplied.
        [int64] $alignment = 32

        # Calculate how many padding bytes are needed.
        [int64] $remainder =
        $writer.BaseStream.Position % $alignment

        # Add padding only when the position is not already aligned.
        if ($remainder -ne 0) {
            [int] $paddingLength =
            [int] ($alignment - $remainder)

            # Create a zero-filled byte array for the padding.
            [byte[]] $paddingBytes =
            [System.Array]::CreateInstance(
                [byte],
                $paddingLength)

            # Write the alignment padding.
            $writer.Write($paddingBytes)
        }
    }
    finally {
        # Close the writer and its underlying file stream.
        $writer.Dispose()
    }
}

# ---------------------------------------------------------------------
# Standard Granite metadata builder
# ---------------------------------------------------------------------

<#
.SYNOPSIS
Creates the standard metadata entries used by valid fixtures.
#>
function New-GraniteMetadataEntries {
    param
    (
        # Omit general.name for the missing-name fixture.
        [switch]
        $OmitName,

        # Omit the architecture-specific context key.
        [switch]
        $OmitContext,

        # Omit general.size_label.
        [switch]
        $OmitSizeLabel,

        # Omit general.file_type.
        [switch]
        $OmitFileType,

        # Select the numeric general.file_type value.
        [uint32]
        $FileType = 15,

        # Write context length as uint32 rather than uint64.
        [switch]
        $ContextAsUInt32,

        # Add metadata that the scanner does not use.
        [switch]
        $AddUnknownMetadata,

        # Write the known keys in a deliberately unusual order.
        [switch]
        $UseUnusualOrder
    )

    # Create each standard entry separately so its order can be controlled.
    $architectureEntry =
    New-GgufEntry `
        -Key "general.architecture" `
        -Type $GgufTypeString `
        -Value "granite"

    $nameEntry =
    New-GgufEntry `
        -Key "general.name" `
        -Type $GgufTypeString `
        -Value "IBM Granite Fixture Model"

    $sizeLabelEntry =
    New-GgufEntry `
        -Key "general.size_label" `
        -Type $GgufTypeString `
        -Value "3B"

    $fileTypeEntry =
    New-GgufEntry `
        -Key "general.file_type" `
        -Type $GgufTypeUInt32 `
        -Value $FileType

    $quantizationVersionEntry =
    New-GgufEntry `
        -Key "general.quantization_version" `
        -Type $GgufTypeUInt32 `
        -Value ([uint32] 2)

    $alignmentEntry =
    New-GgufEntry `
        -Key "general.alignment" `
        -Type $GgufTypeUInt32 `
        -Value ([uint32] 32)

    # Select uint32 or uint64 for the context compatibility fixture.
    if ($ContextAsUInt32) {
        $contextEntry =
        New-GgufEntry `
            -Key "granite.context_length" `
            -Type $GgufTypeUInt32 `
            -Value ([uint32] 131072)
    }
    else {
        $contextEntry =
        New-GgufEntry `
            -Key "granite.context_length" `
            -Type $GgufTypeUInt64 `
            -Value ([uint64] 131072)
    }

    # Use a generic list so entries can be added conditionally.
    $entries =
    [System.Collections.Generic.List[object]]::new()

    # Add entries in the normal order unless the fixture requests otherwise.
    if (-not $UseUnusualOrder) {
        $entries.Add($architectureEntry)

        if (-not $OmitName) {
            $entries.Add($nameEntry)
        }

        if (-not $OmitSizeLabel) {
            $entries.Add($sizeLabelEntry)
        }

        if (-not $OmitFileType) {
            $entries.Add($fileTypeEntry)
        }

        $entries.Add($quantizationVersionEntry)
        $entries.Add($alignmentEntry)

        if (-not $OmitContext) {
            $entries.Add($contextEntry)
        }
    }
    else {
        # Deliberately place the context field before the architecture.
        if (-not $OmitContext) {
            $entries.Add($contextEntry)
        }

        $entries.Add($alignmentEntry)

        if (-not $OmitFileType) {
            $entries.Add($fileTypeEntry)
        }

        if (-not $OmitName) {
            $entries.Add($nameEntry)
        }

        $entries.Add($quantizationVersionEntry)

        if (-not $OmitSizeLabel) {
            $entries.Add($sizeLabelEntry)
        }

        # Place the required architecture key last.
        $entries.Add($architectureEntry)
    }

    # Add safe unknown values for forward-compatibility testing.
    if ($AddUnknownMetadata) {
        $entries.Add(
            (New-GgufEntry `
                -Key "fixture.note" `
                -Type $GgufTypeString `
                -Value "Unknown metadata should be ignored"))

        $entries.Add(
            (New-GgufEntry `
                -Key "fixture.enabled" `
                -Type $GgufTypeBoolean `
                -Value $true))

        $entries.Add(
            (New-GgufEntry `
                -Key "fixture.values" `
                -Type $GgufTypeArray `
                -Value (
                New-GgufArrayValue `
                    -ElementType $GgufTypeUInt32 `
                    -Values @(
                    [uint32] 1,
                    [uint32] 2,
                    [uint32] 3
                )
            )))

        $entries.Add(
            (New-GgufEntry `
                -Key "fixture.labels" `
                -Type $GgufTypeArray `
                -Value (
                New-GgufArrayValue `
                    -ElementType $GgufTypeString `
                    -Values @(
                    "alpha",
                    "beta"
                )
            )))
    }

    # Return the completed ordered entry array.
    return $entries.ToArray()
}

# ---------------------------------------------------------------------
# Expected metadata JSON helper
# ---------------------------------------------------------------------

<#
.SYNOPSIS
Writes the expected scanner result for a valid fixture.
#>
function Write-ExpectedMetadata {
    param
    (
        # Receive the generated GGUF fixture path.
        [Parameter(Mandatory)]
        [string]
        $FixturePath,

        # Receive the stable fixture identifier.
        [Parameter(Mandatory)]
        [string]
        $FixtureId,

        # Receive the model name or its expected filename fallback.
        $ModelName,

        # Receive the expected architecture.
        $Architecture,

        # Receive the expected parameter-size label.
        $ParameterSizeLabel,

        # Receive the expected quantisation label.
        $Quantization,

        # Receive the expected context length.
        $ContextLength,

        # Explain the purpose of the fixture.
        [Parameter(Mandatory)]
        [string]
        $Notes
    )

    # Read the generated fixture information.
    $fixtureFile =
    Get-Item `
        -LiteralPath $FixturePath

    # Calculate an integrity hash for detecting accidental fixture changes.
    $fixtureHash =
    Get-FileHash `
        -LiteralPath $FixturePath `
        -Algorithm SHA256

    # Build the expected-result record.
    $expectedRecord =
    [ordered] @{
        fixtureId        = $FixtureId
        fixtureFile      = $fixtureFile.Name
        expectedOutcome  = "Success"

        expectedMetadata =
        [ordered] @{
            modelName          = $ModelName
            architecture       = $Architecture
            parameterSizeLabel = $ParameterSizeLabel
            quantization       = $Quantization
            fileSizeBytes      = [int64] $fixtureFile.Length
            contextLength      = $ContextLength
            ggufVersion        = 3
        }

        integrity        =
        [ordered] @{
            sha256 = $fixtureHash.Hash.ToLowerInvariant()
        }

        notes            = $Notes
    }

    # Derive the expected JSON filename from the fixture filename.
    $expectedFileName =
    "{0}.json" -f
    [System.IO.Path]::GetFileNameWithoutExtension(
        $fixtureFile.Name)

    # Build the full expected JSON path.
    $expectedPath =
    Join-Path `
        -Path $expectedMetadataDirectory `
        -ChildPath $expectedFileName

    # Convert the record into readable JSON.
    $expectedJson =
    $expectedRecord |
    ConvertTo-Json `
        -Depth 6

    # Match the repository's JSON line-ending policy on every host.
    $expectedJson =
    $expectedJson.Replace(
        "`r`n",
        "`n").Replace(
            "`r",
            "`n")

    # Write UTF-8 JSON without a byte-order mark.
    [System.IO.File]::WriteAllText(
        $expectedPath,
        $expectedJson,
        [System.Text.UTF8Encoding]::new($false))
}

# ---------------------------------------------------------------------
# Valid and partially valid metadata fixtures
# ---------------------------------------------------------------------

# V-001: all display metadata is present.
$v001Path =
Join-Path `
    -Path $ggufDirectory `
    -ChildPath "V-001-complete-metadata-v3.gguf"

Write-GgufMetadataFile `
    -Path $v001Path `
    -Entries (
    New-GraniteMetadataEntries
)

Write-ExpectedMetadata `
    -FixturePath $v001Path `
    -FixtureId "V-001" `
    -ModelName "IBM Granite Fixture Model" `
    -Architecture "granite" `
    -ParameterSizeLabel "3B" `
    -Quantization "Q4_K_M" `
    -ContextLength ([uint64] 131072) `
    -Notes "Complete metadata fixture for the successful scan path."

# V-002: model name is omitted and the filename should become the fallback.
$v002Path =
Join-Path `
    -Path $ggufDirectory `
    -ChildPath "V-002-missing-name.gguf"

Write-GgufMetadataFile `
    -Path $v002Path `
    -Entries (
    New-GraniteMetadataEntries `
        -OmitName
)

Write-ExpectedMetadata `
    -FixturePath $v002Path `
    -FixtureId "V-002" `
    -ModelName "V-002-missing-name.gguf" `
    -Architecture "granite" `
    -ParameterSizeLabel "3B" `
    -Quantization "Q4_K_M" `
    -ContextLength ([uint64] 131072) `
    -Notes "Missing general.name should use the filename as a display fallback."

# V-003: context is absent but the scan may still succeed.
$v003Path =
Join-Path `
    -Path $ggufDirectory `
    -ChildPath "V-003-missing-context.gguf"

Write-GgufMetadataFile `
    -Path $v003Path `
    -Entries (
    New-GraniteMetadataEntries `
        -OmitContext
)

Write-ExpectedMetadata `
    -FixturePath $v003Path `
    -FixtureId "V-003" `
    -ModelName "IBM Granite Fixture Model" `
    -Architecture "granite" `
    -ParameterSizeLabel "3B" `
    -Quantization "Q4_K_M" `
    -ContextLength $null `
    -Notes "Missing context metadata should produce an unavailable context value."

# V-004: size label is absent but the scan may still succeed.
$v004Path =
Join-Path `
    -Path $ggufDirectory `
    -ChildPath "V-004-missing-size-label.gguf"

Write-GgufMetadataFile `
    -Path $v004Path `
    -Entries (
    New-GraniteMetadataEntries `
        -OmitSizeLabel
)

Write-ExpectedMetadata `
    -FixturePath $v004Path `
    -FixtureId "V-004" `
    -ModelName "IBM Granite Fixture Model" `
    -Architecture "granite" `
    -ParameterSizeLabel $null `
    -Quantization "Q4_K_M" `
    -ContextLength ([uint64] 131072) `
    -Notes "Missing general.size_label should produce an unavailable size label."

# V-005: extra scalar and array metadata should be ignored safely.
$v005Path =
Join-Path `
    -Path $ggufDirectory `
    -ChildPath "V-005-unknown-metadata.gguf"

Write-GgufMetadataFile `
    -Path $v005Path `
    -Entries (
    New-GraniteMetadataEntries `
        -AddUnknownMetadata
)

Write-ExpectedMetadata `
    -FixturePath $v005Path `
    -FixtureId "V-005" `
    -ModelName "IBM Granite Fixture Model" `
    -Architecture "granite" `
    -ParameterSizeLabel "3B" `
    -Quantization "Q4_K_M" `
    -ContextLength ([uint64] 131072) `
    -Notes "Unknown scalar and array metadata must not disrupt known-field extraction."

# V-006: metadata ordering must not affect the scanner result.
$v006Path =
Join-Path `
    -Path $ggufDirectory `
    -ChildPath "V-006-unusual-metadata-order.gguf"

Write-GgufMetadataFile `
    -Path $v006Path `
    -Entries (
    New-GraniteMetadataEntries `
        -UseUnusualOrder
)

Write-ExpectedMetadata `
    -FixturePath $v006Path `
    -FixtureId "V-006" `
    -ModelName "IBM Granite Fixture Model" `
    -Architecture "granite" `
    -ParameterSizeLabel "3B" `
    -Quantization "Q4_K_M" `
    -ContextLength ([uint64] 131072) `
    -Notes "Known metadata must be extracted independently of entry order."

# V-007: readers should accept context lengths represented as uint32.
$v007Path =
Join-Path `
    -Path $ggufDirectory `
    -ChildPath "V-007-context-uint32.gguf"

Write-GgufMetadataFile `
    -Path $v007Path `
    -Entries (
    New-GraniteMetadataEntries `
        -ContextAsUInt32
)

Write-ExpectedMetadata `
    -FixturePath $v007Path `
    -FixtureId "V-007" `
    -ModelName "IBM Granite Fixture Model" `
    -Architecture "granite" `
    -ParameterSizeLabel "3B" `
    -Quantization "Q4_K_M" `
    -ContextLength ([uint64] 131072) `
    -Notes "Context length encoded as uint32 should be normalised into the result."

# V-008: general.file_type is optional, so quantisation may be unavailable.
$v008Path =
Join-Path `
    -Path $ggufDirectory `
    -ChildPath "V-008-missing-file-type.gguf"

Write-GgufMetadataFile `
    -Path $v008Path `
    -Entries (
    New-GraniteMetadataEntries `
        -OmitFileType
)

Write-ExpectedMetadata `
    -FixturePath $v008Path `
    -FixtureId "V-008" `
    -ModelName "IBM Granite Fixture Model" `
    -Architecture "granite" `
    -ParameterSizeLabel "3B" `
    -Quantization $null `
    -ContextLength ([uint64] 131072) `
    -Notes "Missing general.file_type should produce an unavailable quantisation value."

# V-009: every official metadata type, including a nested array, is consumed.
$v009Path =
Join-Path `
    -Path $ggufDirectory `
    -ChildPath "V-009-all-official-metadata-types.gguf"

$v009Entries =
[System.Collections.Generic.List[object]]::new()

foreach ($entry in (
    New-GraniteMetadataEntries `
        -FileType ([uint32] 40)
)) {
    $v009Entries.Add($entry)
}

$v009Entries.Add(
    (New-GgufEntry `
        -Key "fixture.uint8" `
        -Type $GgufTypeUInt8 `
        -Value ([byte] 255)))

$v009Entries.Add(
    (New-GgufEntry `
        -Key "fixture.int8" `
        -Type $GgufTypeInt8 `
        -Value ([sbyte] -7)))

$v009Entries.Add(
    (New-GgufEntry `
        -Key "fixture.uint16" `
        -Type $GgufTypeUInt16 `
        -Value ([uint16] 65535)))

$v009Entries.Add(
    (New-GgufEntry `
        -Key "fixture.int16" `
        -Type $GgufTypeInt16 `
        -Value ([int16] -1234)))

$v009Entries.Add(
    (New-GgufEntry `
        -Key "fixture.uint32" `
        -Type $GgufTypeUInt32 `
        -Value ([uint32] 4000000000)))

$v009Entries.Add(
    (New-GgufEntry `
        -Key "fixture.int32" `
        -Type $GgufTypeInt32 `
        -Value ([int32] -2000000000)))

$v009Entries.Add(
    (New-GgufEntry `
        -Key "fixture.float32" `
        -Type $GgufTypeFloat32 `
        -Value ([single] 1.25)))

$v009Entries.Add(
    (New-GgufEntry `
        -Key "fixture.boolean" `
        -Type $GgufTypeBoolean `
        -Value $true))

$v009Entries.Add(
    (New-GgufEntry `
        -Key "fixture.string" `
        -Type $GgufTypeString `
        -Value "all official metadata types"))

$v009NestedArray =
New-GgufArrayValue `
    -ElementType $GgufTypeArray `
    -Values @(
    (New-GgufArrayValue `
        -ElementType $GgufTypeInt32 `
        -Values @(
        [int32] -7,
        [int32] 0,
        [int32] 7
    ))
)

$v009Entries.Add(
    (New-GgufEntry `
        -Key "fixture.nested_array" `
        -Type $GgufTypeArray `
        -Value $v009NestedArray))

$v009Entries.Add(
    (New-GgufEntry `
        -Key "fixture.uint64" `
        -Type $GgufTypeUInt64 `
        -Value ([uint64]::MaxValue)))

$v009Entries.Add(
    (New-GgufEntry `
        -Key "fixture.int64" `
        -Type $GgufTypeInt64 `
        -Value ([int64] -9000000000000)))

$v009Entries.Add(
    (New-GgufEntry `
        -Key "fixture.float64" `
        -Type $GgufTypeFloat64 `
        -Value ([double] 123.456)))

Write-GgufMetadataFile `
    -Path $v009Path `
    -Entries $v009Entries.ToArray()

Write-ExpectedMetadata `
    -FixturePath $v009Path `
    -FixtureId "V-009" `
    -ModelName "IBM Granite Fixture Model" `
    -Architecture "granite" `
    -ParameterSizeLabel "3B" `
    -Quantization "Q1_0" `
    -ContextLength ([uint64] 131072) `
    -Notes "All metadata value types 0 through 12 and a nested array are consumed safely."

# V-010: an unassigned numeric file type receives a stable fallback label.
$v010Path =
Join-Path `
    -Path $ggufDirectory `
    -ChildPath "V-010-unknown-file-type.gguf"

Write-GgufMetadataFile `
    -Path $v010Path `
    -Entries (
    New-GraniteMetadataEntries `
        -FileType ([uint32] 999)
)

Write-ExpectedMetadata `
    -FixturePath $v010Path `
    -FixtureId "V-010" `
    -ModelName "IBM Granite Fixture Model" `
    -Architecture "granite" `
    -ParameterSizeLabel "3B" `
    -Quantization "Unknown (file type 999)" `
    -ContextLength ([uint64] 131072) `
    -Notes "An unassigned general.file_type value should retain its numeric identity."

# V-011: current llama.cpp file type 41 identifies Q2_0.
$v011Path =
Join-Path `
    -Path $ggufDirectory `
    -ChildPath "V-011-current-q2_0-file-type.gguf"

Write-GgufMetadataFile `
    -Path $v011Path `
    -Entries (
    New-GraniteMetadataEntries `
        -FileType ([uint32] 41)
)

Write-ExpectedMetadata `
    -FixturePath $v011Path `
    -FixtureId "V-011" `
    -ModelName "IBM Granite Fixture Model" `
    -Architecture "granite" `
    -ParameterSizeLabel "3B" `
    -Quantization "Q2_0" `
    -ContextLength ([uint64] 131072) `
    -Notes "Current llama.cpp file type 41 should display as Q2_0."

# V-012: scanner-relevant duplicate keys use deterministic first-occurrence wins.
$v012Path =
Join-Path `
    -Path $ggufDirectory `
    -ChildPath "V-012-duplicate-relevant-metadata.gguf"

$v012Entries =
@(
    # Retain the first pre-architecture context candidate.
    New-GgufEntry `
        -Key "granite.context_length" `
        -Type $GgufTypeUInt64 `
        -Value ([uint64] 131072)

    New-GgufEntry `
        -Key "granite.context_length" `
        -Type $GgufTypeUInt64 `
        -Value ([uint64] 262144)

    New-GgufEntry `
        -Key "general.name" `
        -Type $GgufTypeString `
        -Value "First Duplicate Fixture Name"

    New-GgufEntry `
        -Key "general.name" `
        -Type $GgufTypeString `
        -Value "Ignored Duplicate Name"

    New-GgufEntry `
        -Key "general.size_label" `
        -Type $GgufTypeString `
        -Value "3B"

    New-GgufEntry `
        -Key "general.size_label" `
        -Type $GgufTypeString `
        -Value "8B"

    New-GgufEntry `
        -Key "general.file_type" `
        -Type $GgufTypeUInt32 `
        -Value ([uint32] 15)

    New-GgufEntry `
        -Key "general.file_type" `
        -Type $GgufTypeUInt32 `
        -Value ([uint32] 41)

    # The first architecture resolves the first retained Granite context.
    New-GgufEntry `
        -Key "general.architecture" `
        -Type $GgufTypeString `
        -Value "granite"

    # This duplicate must not redirect subsequent exact-context matching.
    New-GgufEntry `
        -Key "general.architecture" `
        -Type $GgufTypeString `
        -Value "llama"

    # The matching context was already established, so this is also a duplicate.
    New-GgufEntry `
        -Key "granite.context_length" `
        -Type $GgufTypeUInt64 `
        -Value ([uint64] 524288)

    # This remains unrelated because the first architecture is Granite.
    New-GgufEntry `
        -Key "llama.context_length" `
        -Type $GgufTypeUInt64 `
        -Value ([uint64] 4096)
)

Write-GgufMetadataFile `
    -Path $v012Path `
    -Entries $v012Entries

Write-ExpectedMetadata `
    -FixturePath $v012Path `
    -FixtureId "V-012" `
    -ModelName "First Duplicate Fixture Name" `
    -Architecture "granite" `
    -ParameterSizeLabel "3B" `
    -Quantization "Q4_K_M" `
    -ContextLength ([uint64] 131072) `
    -Notes "Scanner-relevant duplicate keys retain their first occurrence."

# N-001: a zero-tensor SentencePiece vocabulary accepted by the production
# CPU/VocabOnly runtime. The N-series is native-probe evidence, not another
# quick-scanner expectation row.
$n001Path =
Join-Path `
    -Path $ggufDirectory `
    -ChildPath "N-001-vocab-only-spm.gguf"

$n001Tokens =
New-GgufArrayValue `
    -ElementType $GgufTypeString `
    -Values @(
    "<unk>",
    "<s>",
    "</s>",
    "<0x0A>",
    "He",
    "Hel",
    "Hell",
    "Hello"
)

$n001Scores =
New-GgufArrayValue `
    -ElementType $GgufTypeFloat32 `
    -Values @(
    [single] 0,
    [single] 0,
    [single] 0,
    [single] 0,
    [single] 0,
    [single] 0,
    [single] 0,
    [single] 0
)

$n001TokenTypes =
New-GgufArrayValue `
    -ElementType $GgufTypeInt32 `
    -Values @(
    [int32] 2,
    [int32] 3,
    [int32] 3,
    [int32] 6,
    [int32] 1,
    [int32] 1,
    [int32] 1,
    [int32] 1
)

$n001Entries =
@(
    New-GgufEntry `
        -Key "general.architecture" `
        -Type $GgufTypeString `
        -Value "granite"

    New-GgufEntry `
        -Key "tokenizer.ggml.model" `
        -Type $GgufTypeString `
        -Value "llama"

    New-GgufEntry `
        -Key "tokenizer.ggml.tokens" `
        -Type $GgufTypeArray `
        -Value $n001Tokens

    New-GgufEntry `
        -Key "tokenizer.ggml.scores" `
        -Type $GgufTypeArray `
        -Value $n001Scores

    New-GgufEntry `
        -Key "tokenizer.ggml.token_type" `
        -Type $GgufTypeArray `
        -Value $n001TokenTypes

    New-GgufEntry `
        -Key "tokenizer.ggml.bos_token_id" `
        -Type $GgufTypeUInt32 `
        -Value ([uint32] 1)

    New-GgufEntry `
        -Key "tokenizer.ggml.eos_token_id" `
        -Type $GgufTypeUInt32 `
        -Value ([uint32] 2)

    New-GgufEntry `
        -Key "tokenizer.ggml.unknown_token_id" `
        -Type $GgufTypeUInt32 `
        -Value ([uint32] 0)

    New-GgufEntry `
        -Key "tokenizer.ggml.add_bos_token" `
        -Type $GgufTypeBoolean `
        -Value $false

    New-GgufEntry `
        -Key "tokenizer.ggml.add_eos_token" `
        -Type $GgufTypeBoolean `
        -Value $false

    New-GgufEntry `
        -Key "tokenizer.ggml.add_space_prefix" `
        -Type $GgufTypeBoolean `
        -Value $false

    New-GgufEntry `
        -Key "tokenizer.chat_template" `
        -Type $GgufTypeString `
        -Value "{% for message in messages %}{{ message['content'] }}{% endfor %}"
)

Write-GgufMetadataFile `
    -Path $n001Path `
    -Entries $n001Entries

# ---------------------------------------------------------------------
# Malformed metadata fixtures
# ---------------------------------------------------------------------

# I-004: declare a string value that ends before its declared length.
$i004Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-004-truncated-metadata-value.gguf"

$i004Writer =
New-FixtureBinaryWriter `
    -Path $i004Path

try {
    Write-GgufHeader `
        -Writer $i004Writer `
        -MetadataCount 1

    Write-GgufString `
        -Writer $i004Writer `
        -Value "general.name"

    $i004Writer.Write($GgufTypeString)

    # Declare 20 bytes but supply only three.
    $i004Writer.Write([uint64] 20)

    $i004Writer.Write(
        [System.Text.Encoding]::UTF8.GetBytes("abc"))
}
finally {
    $i004Writer.Dispose()
}

# I-005: declare a key longer than the specification permits.
$i005Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-005-oversized-key-length.gguf"

$i005Writer =
New-FixtureBinaryWriter `
    -Path $i005Path

try {
    Write-GgufHeader `
        -Writer $i005Writer `
        -MetadataCount 1

    # Keys are limited to 65,535 bytes; declare 65,536.
    $i005Writer.Write([uint64] 65536)
}
finally {
    $i005Writer.Dispose()
}

# I-006: use a metadata type that GGUF does not define.
$i006Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-006-unknown-value-type.gguf"

$i006Writer =
New-FixtureBinaryWriter `
    -Path $i006Path

try {
    Write-GgufHeader `
        -Writer $i006Writer `
        -MetadataCount 1

    Write-GgufString `
        -Writer $i006Writer `
        -Value "fixture.unknown"

    # Valid GGUF metadata types currently range from 0 through 12.
    $i006Writer.Write([uint32] 99)
}
finally {
    $i006Writer.Dispose()
}

# I-007: declare an unreasonable number of metadata entries.
$i007Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-007-excessive-metadata-count.gguf"

$i007Writer =
New-FixtureBinaryWriter `
    -Path $i007Path

try {
    Write-GgufHeader `
        -Writer $i007Writer `
        -MetadataCount ([uint64] 1000001)
}
finally {
    $i007Writer.Dispose()
}

# I-008: omit the required general.architecture metadata key.
$i008Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-008-missing-required-architecture.gguf"

$i008Entries =
@(
    New-GgufEntry `
        -Key "general.name" `
        -Type $GgufTypeString `
        -Value "Missing Architecture Fixture"

    New-GgufEntry `
        -Key "general.size_label" `
        -Type $GgufTypeString `
        -Value "3B"

    New-GgufEntry `
        -Key "general.file_type" `
        -Type $GgufTypeUInt32 `
        -Value ([uint32] 15)

    New-GgufEntry `
        -Key "granite.context_length" `
        -Type $GgufTypeUInt64 `
        -Value ([uint64] 131072)
)

Write-GgufMetadataFile `
    -Path $i008Path `
    -Entries $i008Entries

# I-009: provide general.architecture using the wrong value type.
$i009Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-009-wrong-architecture-type.gguf"

$i009Entries =
@(
    New-GgufEntry `
        -Key "general.architecture" `
        -Type $GgufTypeUInt32 `
        -Value ([uint32] 123)
)

Write-GgufMetadataFile `
    -Path $i009Path `
    -Entries $i009Entries

# I-010: GGUF booleans may contain only zero or one.
$i010Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-010-invalid-boolean-value.gguf"

$i010Writer =
New-FixtureBinaryWriter `
    -Path $i010Path

try {
    Write-GgufHeader `
        -Writer $i010Writer `
        -MetadataCount 1

    Write-GgufString `
        -Writer $i010Writer `
        -Value "fixture.enabled"

    $i010Writer.Write($GgufTypeBoolean)

    # Deliberately write an invalid boolean byte.
    $i010Writer.Write([byte] 2)
}
finally {
    $i010Writer.Dispose()
}

# I-011: declare three array elements but provide only one.
$i011Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-011-truncated-array.gguf"

$i011Writer =
New-FixtureBinaryWriter `
    -Path $i011Path

try {
    Write-GgufHeader `
        -Writer $i011Writer `
        -MetadataCount 1

    Write-GgufString `
        -Writer $i011Writer `
        -Value "fixture.values"

    # Declare that the metadata value is an array.
    $i011Writer.Write($GgufTypeArray)

    # Declare uint32 as the array element type.
    $i011Writer.Write($GgufTypeUInt32)

    # Declare three elements.
    $i011Writer.Write([uint64] 3)

    # Supply only one element before ending the file.
    $i011Writer.Write([uint32] 1)
}
finally {
    $i011Writer.Dispose()
}

# I-012: declare one metadata string beyond the 16 MiB scanner limit.
$i012Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-012-oversized-metadata-string.gguf"

$i012Writer =
New-FixtureBinaryWriter `
    -Path $i012Path

try {
    Write-GgufHeader `
        -Writer $i012Writer `
        -MetadataCount 1

    Write-GgufString `
        -Writer $i012Writer `
        -Value "fixture.oversized_string"

    $i012Writer.Write($GgufTypeString)

    # The scanner rejects this declaration before reading or allocating payload.
    $i012Writer.Write([uint64] ((16 * 1024 * 1024) + 1))
}
finally {
    $i012Writer.Dispose()
}

# I-013: declare one array beyond the per-array element limit.
$i013Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-013-excessive-array-count.gguf"

$i013Writer =
New-FixtureBinaryWriter `
    -Path $i013Path

try {
    Write-GgufHeader `
        -Writer $i013Writer `
        -MetadataCount 1

    Write-GgufString `
        -Writer $i013Writer `
        -Value "fixture.excessive_array"

    $i013Writer.Write($GgufTypeArray)
    $i013Writer.Write($GgufTypeUInt8)

    # The scanner rejects this declaration before iterating or reading payload.
    $i013Writer.Write([uint64] 1000001)
}
finally {
    $i013Writer.Dispose()
}

# I-014: nested declarations exceed the aggregate four-million-element limit.
$i014Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-014-excessive-total-array-count.gguf"

$i014Writer =
New-FixtureBinaryWriter `
    -Path $i014Path

try {
    Write-GgufHeader `
        -Writer $i014Writer `
        -MetadataCount 1

    Write-GgufString `
        -Writer $i014Writer `
        -Value "fixture.excessive_total"

    $i014Writer.Write($GgufTypeArray)

    # Each level is individually permitted. The fifth declaration raises the
    # aggregate total to five million before any large payload is required.
    foreach ($level in 1..5) {
        $i014Writer.Write($GgufTypeArray)
        $i014Writer.Write([uint64] 1000000)
    }
}
finally {
    $i014Writer.Dispose()
}

# I-015: declare nine nested array levels where the scanner permits eight.
$i015Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-015-excessive-array-depth.gguf"

$i015Writer =
New-FixtureBinaryWriter `
    -Path $i015Path

try {
    Write-GgufHeader `
        -Writer $i015Writer `
        -MetadataCount 1

    Write-GgufString `
        -Writer $i015Writer `
        -Value "fixture.too_deep"

    $i015Writer.Write($GgufTypeArray)

    foreach ($level in 1..9) {
        $i015Writer.Write($GgufTypeArray)
        $i015Writer.Write([uint64] 1)
    }
}
finally {
    $i015Writer.Dispose()
}

# I-016: the exact architecture context candidate has the wrong value type.
$i016Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-016-invalid-context-type.gguf"

$i016Entries =
@(
    New-GgufEntry `
        -Key "granite.context_length" `
        -Type $GgufTypeString `
        -Value "not an integer"

    New-GgufEntry `
        -Key "general.architecture" `
        -Type $GgufTypeString `
        -Value "granite"
)

Write-GgufMetadataFile `
    -Path $i016Path `
    -Entries $i016Entries

# I-017: a 65th distinct pre-architecture context candidate exceeds the cap.
$i017Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-017-excessive-context-candidates.gguf"

$i017Entries =
[System.Collections.Generic.List[object]]::new()

foreach ($candidateIndex in 0..64) {
    $i017Entries.Add(
        (New-GgufEntry `
            -Key ("candidate{0:D2}.context_length" -f $candidateIndex) `
            -Type $GgufTypeUInt32 `
            -Value ([uint32] (1024 + $candidateIndex))))
}

$i017Entries.Add(
    (New-GgufEntry `
        -Key "general.architecture" `
        -Type $GgufTypeString `
        -Value "granite"))

Write-GgufMetadataFile `
    -Path $i017Path `
    -Entries $i017Entries.ToArray()

# I-018: general.architecture contains a malformed UTF-8 byte sequence.
$i018Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-018-invalid-utf8-architecture.gguf"

$i018Writer =
New-FixtureBinaryWriter `
    -Path $i018Path

try {
    Write-GgufHeader `
        -Writer $i018Writer `
        -MetadataCount 1

    Write-GgufString `
        -Writer $i018Writer `
        -Value "general.architecture"

    $i018Writer.Write($GgufTypeString)
    $i018Writer.Write([uint64] 2)
    $i018Writer.Write([byte[]] @(
        [byte] 0xC3,
        [byte] 0x28
    ))
}
finally {
    $i018Writer.Dispose()
}

# I-019: metadata keys are restricted to ASCII lower_snake_case segments.
$i019Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-019-non-ascii-key.gguf"

$i019Entries =
@(
    # Keep the script source ASCII-only so Windows PowerShell 5.1 and
    # UTF-8-aware hosts generate the same non-ASCII key bytes.
    New-GgufEntry `
        -Key ('fixture.na' + [char] 0x00EF + 've') `
        -Type $GgufTypeUInt8 `
        -Value ([byte] 1)
)

Write-GgufMetadataFile `
    -Path $i019Path `
    -Entries $i019Entries

# I-020: general.name must be a string when present.
$i020Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-020-wrong-name-type.gguf"

$i020Entries =
@(
    New-GgufEntry `
        -Key "general.architecture" `
        -Type $GgufTypeString `
        -Value "granite"

    New-GgufEntry `
        -Key "general.name" `
        -Type $GgufTypeUInt32 `
        -Value ([uint32] 20)
)

Write-GgufMetadataFile `
    -Path $i020Path `
    -Entries $i020Entries

# I-021: general.size_label must be a string when present.
$i021Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-021-wrong-size-label-type.gguf"

$i021Entries =
@(
    New-GgufEntry `
        -Key "general.architecture" `
        -Type $GgufTypeString `
        -Value "granite"

    New-GgufEntry `
        -Key "general.size_label" `
        -Type $GgufTypeUInt32 `
        -Value ([uint32] 3)
)

Write-GgufMetadataFile `
    -Path $i021Path `
    -Entries $i021Entries

# I-022: general.file_type must be a uint32 when present.
$i022Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-022-wrong-file-type.gguf"

$i022Entries =
@(
    New-GgufEntry `
        -Key "general.architecture" `
        -Type $GgufTypeString `
        -Value "granite"

    New-GgufEntry `
        -Key "general.file_type" `
        -Type $GgufTypeString `
        -Value "Q4_K_M"
)

Write-GgufMetadataFile `
    -Path $i022Path `
    -Entries $i022Entries

# I-023: a whitespace-only architecture is not usable metadata.
$i023Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-023-blank-architecture.gguf"

$i023Entries =
@(
    New-GgufEntry `
        -Key "general.architecture" `
        -Type $GgufTypeString `
        -Value "   "
)

Write-GgufMetadataFile `
    -Path $i023Path `
    -Entries $i023Entries

# I-025: metadata keys must not be empty.
$i025Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-025-empty-metadata-key.gguf"

$i025Writer =
New-FixtureBinaryWriter `
    -Path $i025Path

try {
    Write-GgufHeader `
        -Writer $i025Writer `
        -MetadataCount 1

    Write-GgufString `
        -Writer $i025Writer `
        -Value ""

    # Supply a complete uint8 value so only the key grammar is invalid.
    $i025Writer.Write([uint32] 0)
    $i025Writer.Write([byte] 0)
}
finally {
    $i025Writer.Dispose()
}

# I-026: hierarchical metadata keys must not contain an empty segment.
$i026Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-026-empty-key-segment.gguf"

$i026Writer =
New-FixtureBinaryWriter `
    -Path $i026Path

try {
    Write-GgufHeader `
        -Writer $i026Writer `
        -MetadataCount 1

    Write-GgufString `
        -Writer $i026Writer `
        -Value "general..name"

    # Supply a complete uint8 value so only the key grammar is invalid.
    $i026Writer.Write([uint32] 0)
    $i026Writer.Write([byte] 0)
}
finally {
    $i026Writer.Dispose()
}

# I-027: spaces are ASCII but not valid lower_snake_case key characters.
$i027Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-027-key-with-space.gguf"

$i027Writer =
New-FixtureBinaryWriter `
    -Path $i027Path

try {
    Write-GgufHeader `
        -Writer $i027Writer `
        -MetadataCount 1

    Write-GgufString `
        -Writer $i027Writer `
        -Value "general name"

    # Supply a complete uint8 value so only the key grammar is invalid.
    $i027Writer.Write([uint32] 0)
    $i027Writer.Write([byte] 0)
}
finally {
    $i027Writer.Dispose()
}

# I-028: uppercase letters are outside lower_snake_case key segments.
$i028Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-028-uppercase-key.gguf"

$i028Writer =
New-FixtureBinaryWriter `
    -Path $i028Path

try {
    Write-GgufHeader `
        -Writer $i028Writer `
        -MetadataCount 1

    Write-GgufString `
        -Writer $i028Writer `
        -Value "General.name"

    # Supply a complete uint8 value so only the key grammar is invalid.
    $i028Writer.Write([uint32] 0)
    $i028Writer.Write([byte] 0)
}
finally {
    $i028Writer.Dispose()
}

# I-029: individually valid key lengths exceed the aggregate scanner budget.
$i029Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-029-excessive-total-key-bytes.gguf"

$i029Writer =
New-FixtureBinaryWriter `
    -Path $i029Path

try {
    Write-GgufHeader `
        -Writer $i029Writer `
        -MetadataCount 2

    # Consume one valid key byte so the next maximum-length key exceeds the total.
    Write-GgufString `
        -Writer $i029Writer `
        -Value "a"
    $i029Writer.Write($GgufTypeUInt8)
    $i029Writer.Write([byte] 1)

    # Declare an individually legal key length without supplying its payload.
    # The aggregate guard must win before any allocation or remaining-byte check.
    $i029Writer.Write([uint64] 65535)
}
finally {
    $i029Writer.Dispose()
}

# I-030: an invalid boolean value appears just beyond one validation chunk.
$i030Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-030-invalid-boolean-array-value.gguf"

$i030Writer =
New-FixtureBinaryWriter `
    -Path $i030Path

try {
    Write-GgufHeader `
        -Writer $i030Writer `
        -MetadataCount 1

    Write-GgufString `
        -Writer $i030Writer `
        -Value "fixture.boolean_array"
    $i030Writer.Write($GgufTypeArray)
    $i030Writer.Write($GgufTypeBoolean)
    $i030Writer.Write([uint64] 4097)

    # Fill one complete 4 KiB validation chunk with valid false values.
    [byte[]] $validBooleanChunk = [byte[]]::new(4096)
    $i030Writer.Write($validBooleanChunk)

    # The next element must fail with its array index and absolute file offset.
    $i030Writer.Write([byte] 2)
}
finally {
    $i030Writer.Dispose()
}

# I-031: an oversized string appears after many empty array elements.
$i031Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-031-oversized-string-array-value.gguf"

$i031Writer =
New-FixtureBinaryWriter `
    -Path $i031Path

try {
    Write-GgufHeader `
        -Writer $i031Writer `
        -MetadataCount 1

    Write-GgufString `
        -Writer $i031Writer `
        -Value "fixture.string_array"
    $i031Writer.Write($GgufTypeArray)
    $i031Writer.Write($GgufTypeString)
    $i031Writer.Write([uint64] 4097)

    # Each all-zero UInt64 declares one valid empty string.
    [byte[]] $emptyStringLengths = [byte[]]::new(8 * 4096)
    $i031Writer.Write($emptyStringLengths)

    # The next length exceeds the 16 MiB per-string safety limit.
    $i031Writer.Write([uint64] (16 * 1024 * 1024 + 1))
}
finally {
    $i031Writer.Dispose()
}

# I-032: a long nested-array run ends in a truncated child header.
$i032Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-032-truncated-nested-array.gguf"

$i032Writer =
New-FixtureBinaryWriter `
    -Path $i032Path

try {
    Write-GgufHeader `
        -Writer $i032Writer `
        -MetadataCount 1

    Write-GgufString `
        -Writer $i032Writer `
        -Value "fixture.nested_arrays"
    $i032Writer.Write($GgufTypeArray)
    $i032Writer.Write($GgufTypeArray)
    $i032Writer.Write([uint64] 4097)

    # Exercise thousands of empty child headers before the malformed child.
    for ($childIndex = 0; $childIndex -lt 4096; $childIndex++) {
        $i032Writer.Write($GgufTypeUInt8)
        $i032Writer.Write([uint64] 0)
    }

    # Supply a complete child element type but only half its UInt64 count.
    $i032Writer.Write($GgufTypeUInt8)
    $i032Writer.Write([uint32] 0)
}
finally {
    $i032Writer.Dispose()
}

# I-033: retained architecture text exceeds the per-string scanner limit.
$i033Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-033-oversized-retained-string.gguf"

$i033Writer =
New-FixtureBinaryWriter `
    -Path $i033Path

try {
    Write-GgufHeader `
        -Writer $i033Writer `
        -MetadataCount 1

    Write-GgufString `
        -Writer $i033Writer `
        -Value "general.architecture"
    $i033Writer.Write($GgufTypeString)
    $i033Writer.Write([uint64] (16 * 1024 * 1024 + 1))
}
finally {
    $i033Writer.Dispose()
}

# I-034: separate top-level arrays exceed the aggregate element budget.
$i034Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-034-cross-entry-array-total.gguf"

$i034Writer =
New-FixtureBinaryWriter `
    -Path $i034Path

try {
    Write-GgufHeader `
        -Writer $i034Writer `
        -MetadataCount 5

    [byte[]] $millionZeroBytes = [byte[]]::new(1000000)
    for ($arrayIndex = 0; $arrayIndex -lt 4; $arrayIndex++) {
        Write-GgufString `
            -Writer $i034Writer `
            -Value "fixture.array_$arrayIndex"
        $i034Writer.Write($GgufTypeArray)
        $i034Writer.Write($GgufTypeUInt8)
        $i034Writer.Write([uint64] 1000000)
        $i034Writer.Write($millionZeroBytes)
    }

    Write-GgufString `
        -Writer $i034Writer `
        -Value "fixture.array_4"
    $i034Writer.Write($GgufTypeArray)
    $i034Writer.Write($GgufTypeUInt8)
    $i034Writer.Write([uint64] 1)
}
finally {
    $i034Writer.Dispose()
}

# I-035: the first metadata key length stops halfway through its UInt64.
$i035Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-035-truncated-key-length.gguf"

$i035Writer =
New-FixtureBinaryWriter `
    -Path $i035Path

try {
    Write-GgufHeader `
        -Writer $i035Writer `
        -MetadataCount 1
    $i035Writer.Write([uint32] 1)
}
finally {
    $i035Writer.Dispose()
}

# I-036: a key declares five bytes but provides only two.
$i036Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-036-truncated-key-bytes.gguf"

$i036Writer =
New-FixtureBinaryWriter `
    -Path $i036Path

try {
    Write-GgufHeader `
        -Writer $i036Writer `
        -MetadataCount 1
    $i036Writer.Write([uint64] 5)
    $i036Writer.Write(
        [System.Text.Encoding]::ASCII.GetBytes("ab"))
}
finally {
    $i036Writer.Dispose()
}

# I-037: a complete key is followed by only half a value-type UInt32.
$i037Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-037-truncated-value-type.gguf"

$i037Writer =
New-FixtureBinaryWriter `
    -Path $i037Path

try {
    Write-GgufHeader `
        -Writer $i037Writer `
        -MetadataCount 1
    Write-GgufString `
        -Writer $i037Writer `
        -Value "a"
    $i037Writer.Write([uint16] 0)
}
finally {
    $i037Writer.Dispose()
}

# I-038: metadata key bytes contain an invalid UTF-8 continuation sequence.
$i038Path =
Join-Path `
    -Path $malformedDirectory `
    -ChildPath "I-038-invalid-utf8-key.gguf"

$i038Writer =
New-FixtureBinaryWriter `
    -Path $i038Path

try {
    Write-GgufHeader `
        -Writer $i038Writer `
        -MetadataCount 1
    $i038Writer.Write([uint64] 2)
    $i038Writer.Write([byte[]] @(0xC3, 0x28))
}
finally {
    $i038Writer.Dispose()
}

# ---------------------------------------------------------------------
# Generated fixture integrity manifest
# ---------------------------------------------------------------------

# Build one integrity record without deriving any scanner expectation.
function New-FixtureManifestRecord {
    param
    (
        # Receive the generated fixture file.
        [Parameter(Mandatory)]
        [System.IO.FileInfo]
        $FixtureFile,

        # Receive its repository-relative fixture category.
        [Parameter(Mandatory)]
        [string]
        $Category
    )

    $fixtureHash =
    Get-FileHash `
        -LiteralPath $FixtureFile.FullName `
        -Algorithm SHA256

    $fixtureIdMatch =
    [System.Text.RegularExpressions.Regex]::Match(
        $FixtureFile.BaseName,
        "^[A-Z]-[0-9]{3}")

    return [ordered] @{
        fixtureId  = $fixtureIdMatch.Value
        fixtureFile = "$Category/$($FixtureFile.Name)"
        byteLength = [int64] $FixtureFile.Length
        sha256     = $fixtureHash.Hash.ToLowerInvariant()
    }
}

$generatedFixtureRecords =
[System.Collections.Generic.List[object]]::new()

foreach ($fixtureFile in (
    Get-ChildItem `
        -LiteralPath $ggufDirectory `
        -Filter "*.gguf" |
    Sort-Object Name)) {
    $generatedFixtureRecords.Add(
        (New-FixtureManifestRecord `
            -FixtureFile $fixtureFile `
            -Category "GGUF"))
}

foreach ($fixtureFile in (
    Get-ChildItem `
        -LiteralPath $malformedDirectory `
        -Filter "*.gguf" |
    Sort-Object Name)) {
    $generatedFixtureRecords.Add(
        (New-FixtureManifestRecord `
            -FixtureFile $fixtureFile `
            -Category "Malformed"))
}

$fixtureManifest =
[ordered] @{
    fixtures =
    [ordered] @{
        Malformed        = "Malformed test assets"
        GGUF             = "GGUF model fixture assets"
        OpenVINO         = "OpenVINO model fixture assets"
        ExpectedMetadata = "Expected metadata fixtures"
    }

    generatedGgufFixtures = $generatedFixtureRecords.ToArray()
}

$fixtureManifestJson =
$fixtureManifest |
ConvertTo-Json `
    -Depth 6

# Match the repository's JSON line-ending policy on every host.
$fixtureManifestJson =
$fixtureManifestJson.Replace(
    "`r`n",
    "`n").Replace(
        "`r",
        "`n")

$fixtureManifestPath =
Join-Path `
    -Path $fixtureRoot `
    -ChildPath "fixture-manifest.json"

[System.IO.File]::WriteAllText(
    $fixtureManifestPath,
    $fixtureManifestJson + "`n",
    [System.Text.UTF8Encoding]::new($false))

# ---------------------------------------------------------------------
# Final verification output
# ---------------------------------------------------------------------

# Display every generated metadata fixture and its exact size.
Get-ChildItem `
    -Path $ggufDirectory, $malformedDirectory `
    -Filter "*.gguf" |
Sort-Object FullName |
Select-Object Name, Length, DirectoryName

# Display every generated expected-result file.
Get-ChildItem `
    -Path $expectedMetadataDirectory `
    -Filter "*.json" |
Sort-Object Name |
Select-Object Name, Length
