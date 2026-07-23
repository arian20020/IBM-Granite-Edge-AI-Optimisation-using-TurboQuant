#requires -Version 5.1

<#
.SYNOPSIS
Creates controlled GGUF metadata fixtures.

.DESCRIPTION
The generated files are tiny binary test inputs.

They are not usable language models and contain no model tensors.
They exist only to exercise GGUF metadata parsing and validation.
#>

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

# ---------------------------------------------------------------------
# GGUF metadata type identifiers
# ---------------------------------------------------------------------

# GGUF uint32 metadata value type.
[uint32] $GgufTypeUInt32 = 4

# GGUF boolean metadata value type.
[uint32] $GgufTypeBoolean = 7

# GGUF UTF-8 string metadata value type.
[uint32] $GgufTypeString = 8

# GGUF array metadata value type.
[uint32] $GgufTypeArray = 9

# GGUF uint64 metadata value type.
[uint32] $GgufTypeUInt64 = 10

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
        # Write a 32-bit unsigned integer.
        4 {
            $Writer.Write([uint32] $Value)
            break
        }

        # Write a one-byte boolean.
        7 {
            $Writer.Write([bool] $Value)
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
        -Value ([uint32] 15)

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