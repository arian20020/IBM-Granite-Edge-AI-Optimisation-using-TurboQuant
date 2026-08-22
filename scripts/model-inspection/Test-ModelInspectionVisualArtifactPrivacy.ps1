[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $ArtifactRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Test-ForbiddenText {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Text,

        [Parameter(Mandatory = $true)]
        [string] $RelativePath
    )

    if ($Text -match '(?i)[a-z]:(?:\\|/)' -or
        $Text -match '\\\\' -or
        $Text -match '(?i)(?:^|["''\s=:(])/(?!/)') {
        throw "$RelativePath contains an absolute path."
    }

    if ($Text -match '(?i)"(?:userName|machineName|computerName)"\s*:') {
        throw "$RelativePath contains identity metadata."
    }

    $identityValues = @($env:USERNAME, $env:COMPUTERNAME) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and $_.Length -ge 3 } |
        Select-Object -Unique
    foreach ($identityValue in $identityValues) {
        if ($Text.IndexOf(
                $identityValue,
                [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "$RelativePath contains identity metadata."
        }
    }

    if ($Text -match '(?i)"(?:modelPath|modelName|publisher|architecture|quantisation|parameterCount|fileName)"\s*:' -or
        $Text -match '(?i)\.gguf(?:["\\/\s]|$)') {
        throw "$RelativePath contains real-model metadata."
    }
}

function Test-ForbiddenJsonPropertyName {
    param(
        [Parameter(Mandatory = $true)]
        [string] $PropertyName,

        [Parameter(Mandatory = $true)]
        [string] $RelativePath
    )

    if ($PropertyName -match '(?i)^(?:userName|machineName|computerName)$') {
        throw "$RelativePath contains identity metadata."
    }

    if ($PropertyName -match '(?i)^(?:modelPath|modelName|publisher|architecture|quantisation|parameterCount|fileName)$') {
        throw "$RelativePath contains real-model metadata."
    }

    Test-ForbiddenText -Text $PropertyName -RelativePath $RelativePath
}

function Test-DecodedJsonValue {
    param(
        [Parameter(Mandatory = $false)]
        [AllowNull()]
        [object] $Value,

        [Parameter(Mandatory = $true)]
        [string] $RelativePath
    )

    if ($null -eq $Value) {
        return
    }

    if ($Value -is [string]) {
        if ([System.IO.Path]::IsPathRooted($Value) -or
            $Value.StartsWith('//', [StringComparison]::Ordinal)) {
            throw "$RelativePath contains an absolute path."
        }

        Test-ForbiddenText -Text $Value -RelativePath $RelativePath
        return
    }

    if ($Value -is [System.Management.Automation.PSCustomObject]) {
        foreach ($property in $Value.PSObject.Properties) {
            Test-ForbiddenJsonPropertyName `
                -PropertyName $property.Name `
                -RelativePath $RelativePath
            Test-DecodedJsonValue `
                -Value $property.Value `
                -RelativePath $RelativePath
        }

        return
    }

    if ($Value -is [System.Collections.IDictionary]) {
        foreach ($propertyName in $Value.Keys) {
            Test-ForbiddenJsonPropertyName `
                -PropertyName ([string] $propertyName) `
                -RelativePath $RelativePath
            Test-DecodedJsonValue `
                -Value $Value[$propertyName] `
                -RelativePath $RelativePath
        }

        return
    }

    if ($Value -is [System.Collections.IEnumerable]) {
        foreach ($item in $Value) {
            Test-DecodedJsonValue -Value $item -RelativePath $RelativePath
        }
    }
}

function Test-RunManifestSchema {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Json,

        [Parameter(Mandatory = $true)]
        [string] $RelativePath
    )

    if ($Json -isnot [System.Management.Automation.PSCustomObject]) {
        throw "$RelativePath must contain one JSON run-manifest object."
    }

    [string[]] $approvedProperties = @(
        'candidateCommit',
        'osBuild',
        'rasterizer',
        'resolution',
        'dpi',
        'textScale',
        'theme',
        'animationsEnabled',
        'states'
    )
    $properties = @($Json.PSObject.Properties)
    foreach ($property in $properties) {
        if ($approvedProperties -cnotcontains $property.Name) {
            throw "$RelativePath contains an unapproved JSON property '$($property.Name)'."
        }
    }

    foreach ($propertyName in $approvedProperties) {
        if ($properties.Name -cnotcontains $propertyName) {
            throw "$RelativePath is missing required JSON property '$propertyName'."
        }
    }

    if ($properties.Count -ne $approvedProperties.Count) {
        throw "$RelativePath does not match the approved run-manifest schema."
    }

    if ($Json.candidateCommit -isnot [string] -or
        $Json.candidateCommit -cnotmatch '^[0-9a-f]{40}$') {
        throw "$RelativePath has an invalid candidateCommit."
    }

    if ($Json.osBuild -isnot [string] -or
        $Json.osBuild -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?$') {
        throw "$RelativePath has an invalid osBuild."
    }

    if ($Json.rasterizer -cne 'WinUI RenderTargetBitmap' -or
        $Json.resolution -cne '1440x1024' -or
        $Json.theme -cne 'Light') {
        throw "$RelativePath has an unapproved visual environment value."
    }

    if (($Json.dpi -isnot [int] -and $Json.dpi -isnot [long]) -or
        [int64] $Json.dpi -ne 96 -or
        ($Json.textScale -isnot [int] -and $Json.textScale -isnot [long]) -or
        [int64] $Json.textScale -ne 100 -or
        $Json.animationsEnabled -isnot [bool]) {
        throw "$RelativePath has an invalid visual environment type or value."
    }

    if ($Json.states -isnot [System.Array]) {
        throw "$RelativePath states must be the exact 13-state array."
    }

    $states = @($Json.states)
    if ($states.Count -ne 13) {
        throw "$RelativePath states must be the exact 13-state array."
    }

    for ($index = 0; $index -lt 13; $index++) {
        $expectedState = '{0:D2}' -f ($index + 1)
        if ($states[$index] -isnot [string] -or
            $states[$index] -cne $expectedState) {
            throw "$RelativePath states must be ordered exactly 01 through 13."
        }
    }
}

function Test-JsonPropertyNamesAreUnique {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Text,

        [Parameter(Mandatory = $true)]
        [string] $RelativePath
    )

    $propertyPattern =
        '(?<Property>"(?:\\(?:["\\/bfnrt]|u[0-9a-fA-F]{4})|[^"\\\x00-\x1f])*")\s*:'
    $seenPropertyNames = New-Object `
        'System.Collections.Generic.HashSet[string]' `
        ([StringComparer]::Ordinal)
    foreach ($match in [regex]::Matches(
            $Text,
            $propertyPattern,
            [Text.RegularExpressions.RegexOptions]::CultureInvariant)) {
        try {
            [string] $propertyName =
                $match.Groups['Property'].Value | ConvertFrom-Json
        }
        catch {
            throw "$RelativePath is not valid JSON."
        }

        if (-not $seenPropertyNames.Add($propertyName)) {
            throw "$RelativePath contains duplicate JSON property '$propertyName'."
        }
    }
}

function Test-JsonArtifact {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo] $File,

        [Parameter(Mandatory = $true)]
        [string] $RelativePath
    )

    $text = [System.IO.File]::ReadAllText($File.FullName)
    Test-JsonPropertyNamesAreUnique -Text $text -RelativePath $RelativePath
    try {
        $json = $text | ConvertFrom-Json
    }
    catch {
        throw "$RelativePath is not valid JSON."
    }

    Test-ForbiddenText -Text $text -RelativePath $RelativePath
    Test-DecodedJsonValue -Value $json -RelativePath $RelativePath
    Test-RunManifestSchema -Json $json -RelativePath $RelativePath
}

function Read-PngUInt32 {
    param(
        [Parameter(Mandatory = $true)]
        [byte[]] $Bytes,

        [Parameter(Mandatory = $true)]
        [int64] $Offset
    )

    return [uint32] (
        ([uint64] $Bytes[$Offset] -shl 24) -bor
        ([uint64] $Bytes[$Offset + 1] -shl 16) -bor
        ([uint64] $Bytes[$Offset + 2] -shl 8) -bor
        [uint64] $Bytes[$Offset + 3])
}

function Get-PngCrc32 {
    param(
        [Parameter(Mandatory = $true)]
        [byte[]] $Bytes,

        [Parameter(Mandatory = $true)]
        [int64] $Offset,

        [Parameter(Mandatory = $true)]
        [int64] $Length
    )

    [uint32] $crc = [uint32]::MaxValue
    for ($index = $Offset; $index -lt $Offset + $Length; $index++) {
        $crc = [uint32] ($crc -bxor [uint32] $Bytes[$index])
        for ($bit = 0; $bit -lt 8; $bit++) {
            if (($crc -band 1) -ne 0) {
                $crc = [uint32] (($crc -shr 1) -bxor [uint32] 3988292384)
            }
            else {
                $crc = [uint32] ($crc -shr 1)
            }
        }
    }

    return [uint32] ($crc -bxor [uint32]::MaxValue)
}

function Test-PngPayload {
    param(
        [Parameter(Mandatory = $true)]
        [byte[]] $Bytes,

        [Parameter(Mandatory = $true)]
        [int64] $Offset,

        [Parameter(Mandatory = $true)]
        [byte[]] $Expected,

        [Parameter(Mandatory = $true)]
        [string] $RelativePath
    )

    for ($index = 0; $index -lt $Expected.Length; $index++) {
        if ($Bytes[$Offset + $index] -ne $Expected[$index]) {
            throw "$RelativePath contains an unapproved PNG metadata payload."
        }
    }
}

function Test-PngArtifact {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo] $File,

        [Parameter(Mandatory = $true)]
        [string] $RelativePath
    )

    [byte[]] $bytes = [System.IO.File]::ReadAllBytes($File.FullName)
    [byte[]] $signature = @(137, 80, 78, 71, 13, 10, 26, 10)
    if ($bytes.Length -lt $signature.Length) {
        throw "$RelativePath is not a complete PNG."
    }

    for ($index = 0; $index -lt $signature.Length; $index++) {
        if ($bytes[$index] -ne $signature[$index]) {
            throw "$RelativePath has an invalid PNG signature."
        }
    }

    [int64] $offset = 8
    $sawHeader = $false
    $sawImageData = $false
    $sawEnd = $false
    $chunkTypes = New-Object 'System.Collections.Generic.List[string]'
    while ($offset -lt $bytes.Length) {
        if ($offset + 12 -gt $bytes.Length) {
            throw "$RelativePath has a truncated PNG chunk."
        }

        [int64] $length = Read-PngUInt32 -Bytes $bytes -Offset $offset
        [string] $chunkType = [System.Text.Encoding]::ASCII.GetString(
            $bytes,
            [int] ($offset + 4),
            4)
        if ($chunkType -notmatch '^[A-Za-z]{4}$') {
            throw "$RelativePath has an invalid PNG chunk type."
        }

        [int64] $nextOffset = $offset + 12 + $length
        if ($length -lt 0 -or $nextOffset -gt $bytes.Length) {
            throw "$RelativePath has an invalid PNG chunk length."
        }

        if (-not $sawHeader) {
            if ($chunkType -ne 'IHDR' -or $length -ne 13) {
                throw "$RelativePath must begin with one PNG IHDR chunk."
            }

            $sawHeader = $true
        }

        if ($chunkType -in @('tEXt', 'zTXt', 'iTXt', 'eXIf')) {
            throw "$RelativePath contains PNG textual metadata."
        }

        $approvedChunkTypes = @('IHDR', 'sRGB', 'gAMA', 'pHYs', 'IDAT', 'IEND')
        if ($approvedChunkTypes -cnotcontains $chunkType) {
            throw "$RelativePath contains an unapproved PNG chunk."
        }

        if (($chunkType -eq 'sRGB' -and $length -ne 1) -or
            ($chunkType -eq 'gAMA' -and $length -ne 4) -or
            ($chunkType -eq 'pHYs' -and $length -ne 9)) {
            throw "$RelativePath has an invalid PNG metadata chunk length."
        }

        [uint32] $storedCrc = Read-PngUInt32 `
            -Bytes $bytes `
            -Offset ($offset + 8 + $length)
        [uint32] $computedCrc = Get-PngCrc32 `
            -Bytes $bytes `
            -Offset ($offset + 4) `
            -Length ($length + 4)
        if ($storedCrc -ne $computedCrc) {
            throw "$RelativePath has an invalid PNG chunk CRC."
        }

        if ($chunkType -eq 'sRGB') {
            Test-PngPayload `
                -Bytes $bytes `
                -Offset ($offset + 8) `
                -Expected ([byte[]] @(0)) `
                -RelativePath $RelativePath
        }
        elseif ($chunkType -eq 'gAMA') {
            Test-PngPayload `
                -Bytes $bytes `
                -Offset ($offset + 8) `
                -Expected ([byte[]] @(0, 0, 177, 143)) `
                -RelativePath $RelativePath
        }
        elseif ($chunkType -eq 'pHYs') {
            Test-PngPayload `
                -Bytes $bytes `
                -Offset ($offset + 8) `
                -Expected ([byte[]] @(0, 0, 14, 195, 0, 0, 14, 195, 1)) `
                -RelativePath $RelativePath
        }

        if ($chunkType -eq 'IHDR' -and $offset -ne 8) {
            throw "$RelativePath contains more than one PNG IHDR chunk."
        }

        if ($chunkType -eq 'IDAT') {
            $sawImageData = $true
        }

        if ($chunkType -eq 'IEND') {
            if ($length -ne 0 -or $nextOffset -ne $bytes.Length) {
                throw "$RelativePath has an invalid PNG IEND chunk."
            }

            $sawEnd = $true
        }

        $chunkTypes.Add($chunkType)
        $offset = $nextOffset
    }

    if (-not $sawHeader -or -not $sawImageData -or -not $sawEnd) {
        throw "$RelativePath is missing required PNG structure."
    }

    [string[]] $types = $chunkTypes.ToArray()
    $firstImageData = [Array]::IndexOf($types, 'IDAT')
    $lastImageData = [Array]::LastIndexOf($types, 'IDAT')
    if ($firstImageData -lt 1 -or
        $lastImageData -ne $types.Length - 2 -or
        $types[$types.Length - 1] -cne 'IEND') {
        throw "$RelativePath has an invalid PNG chunk order."
    }

    for ($index = $firstImageData; $index -le $lastImageData; $index++) {
        if ($types[$index] -cne 'IDAT') {
            throw "$RelativePath has non-contiguous PNG image data."
        }
    }

    [string[]] $metadata = @()
    if ($firstImageData -gt 1) {
        $metadata = @($types[1..($firstImageData - 1)])
    }

    [string[]] $approvedMetadata = @('sRGB', 'gAMA', 'pHYs')
    if ($metadata.Count -ne 0) {
        if ($metadata.Count -ne $approvedMetadata.Count) {
            throw "$RelativePath does not contain the approved PNG metadata sequence."
        }

        for ($index = 0; $index -lt $approvedMetadata.Count; $index++) {
            if ($metadata[$index] -cne $approvedMetadata[$index]) {
                throw "$RelativePath does not contain the approved PNG metadata sequence."
            }
        }
    }
}

$resolvedRoot = (Resolve-Path -LiteralPath $ArtifactRoot).Path
if (-not (Test-Path -LiteralPath $resolvedRoot -PathType Container)) {
    throw 'ArtifactRoot must resolve to a directory.'
}

$files = @(Get-ChildItem -LiteralPath $resolvedRoot -File -Recurse -Force |
    Sort-Object FullName)
if ($files.Count -eq 0) {
    throw 'ArtifactRoot contains no artifacts to scan.'
}

$jsonCount = 0
$pngCount = 0
foreach ($file in $files) {
    $relativePath = $file.FullName.Substring($resolvedRoot.Length).TrimStart('\', '/')
    Test-ForbiddenText -Text $relativePath -RelativePath $relativePath

    switch ($file.Extension.ToLowerInvariant()) {
        '.json' {
            Test-JsonArtifact -File $file -RelativePath $relativePath
            $jsonCount++
        }
        '.png' {
            Test-PngArtifact -File $file -RelativePath $relativePath
            $pngCount++
        }
        '.trx' {
            throw 'Raw TRX is not approved by this scanner; Task 12 must sanitize retained test evidence.'
        }
        default {
            throw "$relativePath has an unsupported artifact extension."
        }
    }
}

$successMessage =
    'Privacy scan passed for {0} JSON and {1} PNG artifact(s). ' +
    'Raw TRX is not approved by this scanner.'
Write-Output ($successMessage -f $jsonCount, $pngCount)
