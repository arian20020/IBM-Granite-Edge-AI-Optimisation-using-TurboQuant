[CmdletBinding()]
param(
    [string] $Path,
    [string] $ExpectedPackageSha256,
    [string] $ExpectedPublisher,
    [string] $ExpectedCommit
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$maximumRecordBytes = 8 * 1024
$failureMessage =
    'HI-GATE9-RELEASE-TRUST-INVALID: release trust validation failed.'

function Assert-Gate9ReleaseTrustExactProperties {
    param(
        [AllowNull()] [object] $InputObject,
        [Parameter(Mandatory)] [string[]] $Expected
    )

    if ($null -eq $InputObject -or $InputObject -is [Array]) {
        throw 'Invalid release-trust object shape.'
    }

    [string[]] $actual = @(
        $InputObject.PSObject.Properties | ForEach-Object { $_.Name })
    if ($actual.Count -ne $Expected.Count) {
        throw 'Invalid release-trust property count.'
    }

    for ($index = 0; $index -lt $Expected.Count; $index++) {
        if ($actual[$index] -cne $Expected[$index]) {
            throw 'Invalid release-trust property identity or order.'
        }
    }
}

function Assert-Gate9ReleaseTrustTrueBoolean {
    param([AllowNull()] [object] $Value)

    if ($Value -isnot [bool] -or -not $Value) {
        throw 'A required release-trust verification is absent.'
    }
}

function Read-Gate9ReleaseTrustRecord {
    param([Parameter(Mandatory)] [string] $InputPath)

    $fullPath = [IO.Path]::GetFullPath($InputPath)
    if (-not [IO.File]::Exists($fullPath)) {
        throw 'The release-trust record is absent.'
    }

    $stream = [IO.File]::Open(
        $fullPath,
        [IO.FileMode]::Open,
        [IO.FileAccess]::Read,
        [IO.FileShare]::Read)
    try {
        if ($stream.Length -lt 2 -or $stream.Length -gt $maximumRecordBytes) {
            throw 'The release-trust record length is invalid.'
        }

        $bytes = New-Object byte[] ([int] $stream.Length)
        $offset = 0
        while ($offset -lt $bytes.Length) {
            $read = $stream.Read($bytes, $offset, $bytes.Length - $offset)
            if ($read -eq 0) {
                throw 'The release-trust record ended during its bounded read.'
            }

            $offset += $read
        }
    }
    finally {
        $stream.Dispose()
    }

    if (($bytes.Length -ge 3 -and
            $bytes[0] -eq 0xef -and
            $bytes[1] -eq 0xbb -and
            $bytes[2] -eq 0xbf) -or
        $bytes[$bytes.Length - 1] -ne 0x0a) {
        throw 'The release-trust record framing is invalid.'
    }

    $lineFeedCount = 0
    foreach ($value in $bytes) {
        if ($value -eq 0x0d) {
            throw 'The release-trust record contains a carriage return.'
        }

        if ($value -eq 0x0a) {
            $lineFeedCount++
        }
    }

    if ($lineFeedCount -ne 1) {
        throw 'The release-trust record must have exactly one final line feed.'
    }

    $jsonBytes = New-Object byte[] ($bytes.Length - 1)
    [Array]::Copy($bytes, 0, $jsonBytes, 0, $jsonBytes.Length)
    $utf8 = [Text.UTF8Encoding]::new($false, $true)
    $json = $utf8.GetString($jsonBytes)
    if ([string]::IsNullOrEmpty($json)) {
        throw 'The release-trust JSON is empty.'
    }

    [pscustomobject]@{
        Bytes = [byte[]] $bytes
        Record = $json | ConvertFrom-Json -ErrorAction Stop
        Utf8 = $utf8
    }
}

function Assert-Gate9ReleaseTrustRecord {
    param(
        [Parameter(Mandatory)] [object] $Document,
        [Parameter(Mandatory)] [string] $RequiredPackageSha256,
        [Parameter(Mandatory)] [string] $RequiredPublisher,
        [Parameter(Mandatory)] [string] $RequiredCommit
    )

    if ($RequiredPackageSha256 -cnotmatch '^[0-9a-f]{64}$' -or
        $RequiredCommit -cnotmatch '^[0-9a-f]{40}$' -or
        $RequiredPublisher -cnotmatch
            '^CN=[A-Za-z0-9](?:[A-Za-z0-9._ -]{0,126}[A-Za-z0-9])?$') {
        throw 'A release-trust caller binding is invalid.'
    }

    $record = $Document.Record
    Assert-Gate9ReleaseTrustExactProperties -InputObject $record -Expected @(
        'schema',
        'classification',
        'packageSha256',
        'evaluatedCommit',
        'publisher',
        'signatureKind',
        'certificateTimeValidityVerified',
        'codeSigningEkuVerified',
        'publicChainVerified',
        'timestampVerified',
        'smartAppControlVerified')
    if ($record.schema -isnot [string] -or
        $record.schema -cne 'granite.hardware-inspection.gate9-release-trust/v1' -or
        $record.classification -isnot [string] -or
        $record.classification -cne 'local-sanitized' -or
        $record.packageSha256 -isnot [string] -or
        $record.packageSha256 -cnotmatch '^[0-9a-f]{64}$' -or
        $record.packageSha256 -cne $RequiredPackageSha256 -or
        $record.evaluatedCommit -isnot [string] -or
        $record.evaluatedCommit -cnotmatch '^[0-9a-f]{40}$' -or
        $record.evaluatedCommit -cne $RequiredCommit -or
        $record.publisher -isnot [string] -or
        $record.publisher -cnotmatch
            '^CN=[A-Za-z0-9](?:[A-Za-z0-9._ -]{0,126}[A-Za-z0-9])?$' -or
        $record.publisher -cne $RequiredPublisher -or
        $record.signatureKind -isnot [string] -or
        $record.signatureKind -cnotin @('Enterprise', 'Store')) {
        throw 'A release-trust identity binding is invalid.'
    }

    Assert-Gate9ReleaseTrustTrueBoolean `
        -Value $record.certificateTimeValidityVerified
    Assert-Gate9ReleaseTrustTrueBoolean `
        -Value $record.codeSigningEkuVerified
    Assert-Gate9ReleaseTrustTrueBoolean -Value $record.publicChainVerified
    Assert-Gate9ReleaseTrustTrueBoolean -Value $record.timestampVerified
    Assert-Gate9ReleaseTrustTrueBoolean -Value $record.smartAppControlVerified

    $canonical = [ordered]@{
        schema = 'granite.hardware-inspection.gate9-release-trust/v1'
        classification = 'local-sanitized'
        packageSha256 = [string] $record.packageSha256
        evaluatedCommit = [string] $record.evaluatedCommit
        publisher = [string] $record.publisher
        signatureKind = [string] $record.signatureKind
        certificateTimeValidityVerified = $true
        codeSigningEkuVerified = $true
        publicChainVerified = $true
        timestampVerified = $true
        smartAppControlVerified = $true
    }
    $canonicalJson = $canonical | ConvertTo-Json -Depth 4 -Compress
    [byte[]] $canonicalBytes = $Document.Utf8.GetBytes($canonicalJson + "`n")
    if (-not [Linq.Enumerable]::SequenceEqual(
            [byte[]] $Document.Bytes,
            $canonicalBytes)) {
        throw 'The release-trust record is not canonical JSON.'
    }
}

try {
    if ([string]::IsNullOrWhiteSpace($Path) -or
        [string]::IsNullOrWhiteSpace($ExpectedPackageSha256) -or
        [string]::IsNullOrWhiteSpace($ExpectedPublisher) -or
        [string]::IsNullOrWhiteSpace($ExpectedCommit)) {
        throw 'A required release-trust argument is absent.'
    }

    $document = Read-Gate9ReleaseTrustRecord -InputPath $Path
    Assert-Gate9ReleaseTrustRecord `
        -Document $document `
        -RequiredPackageSha256 $ExpectedPackageSha256 `
        -RequiredPublisher $ExpectedPublisher `
        -RequiredCommit $ExpectedCommit
}
catch {
    [Console]::Error.WriteLine($failureMessage)
    exit 1
}

exit 0
