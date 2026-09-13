[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $Path,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $ExpectedCommit
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$maximumSummaryBytes = 16 * 1024
$failureMessage = 'HI-GATE9-SUMMARY-INVALID: canonical summary validation failed.'

function Assert-ExactProperties {
    param(
        [Parameter(Mandatory)] [object] $InputObject,
        [Parameter(Mandatory)] [string[]] $Expected
    )

    if ($null -eq $InputObject -or $InputObject -is [Array]) {
        throw 'Invalid object shape.'
    }

    [string[]] $actual = @(
        $InputObject.PSObject.Properties | ForEach-Object { $_.Name }
    )
    if ($actual.Count -ne $Expected.Count) {
        throw 'Invalid property count.'
    }

    for ($index = 0; $index -lt $Expected.Count; $index++) {
        if ($actual[$index] -cne $Expected[$index]) {
            throw 'Invalid property identity or order.'
        }
    }
}

function Assert-ExactString {
    param(
        [AllowNull()] [object] $Value,
        [Parameter(Mandatory)] [string] $Expected
    )

    if ($Value -isnot [string] -or $Value -cne $Expected) {
        throw 'Invalid string value.'
    }
}

function Assert-TrueBoolean {
    param([AllowNull()] [object] $Value)

    if ($Value -isnot [bool] -or -not $Value) {
        throw 'Invalid Boolean value.'
    }
}

function Assert-ExactInteger {
    param(
        [AllowNull()] [object] $Value,
        [Parameter(Mandatory)] [long] $Expected
    )

    if ($null -eq $Value -or
        ($Value.GetType() -ne [int] -and $Value.GetType() -ne [long]) -or
        [long] $Value -ne $Expected) {
        throw 'Invalid integer value.'
    }
}

function Assert-CanonicalDiagnostics {
    param([AllowNull()] [object] $Value)

    if ($Value -isnot [Array]) {
        throw 'Diagnostics must be an array.'
    }

    [object[]] $diagnostics = @($Value)
    if ($diagnostics.Count -gt 8) {
        throw 'Diagnostics exceed their bound.'
    }

    [string] $previous = $null
    foreach ($diagnostic in $diagnostics) {
        if ($diagnostic -isnot [string] -or
            $diagnostic -cnotmatch '^[A-Z0-9-]{1,64}$') {
            throw 'Diagnostic token is invalid.'
        }

        if ($null -ne $previous -and
            [StringComparer]::Ordinal.Compare($previous, $diagnostic) -ge 0) {
            throw 'Diagnostics are not unique and sorted.'
        }

        $previous = $diagnostic
    }
}

function Read-BoundedCanonicalSummary {
    param([Parameter(Mandatory)] [string] $InputPath)

    $fullPath = [IO.Path]::GetFullPath($InputPath)
    if (-not [IO.File]::Exists($fullPath)) {
        throw 'Summary is absent.'
    }

    $stream = [IO.File]::Open(
        $fullPath,
        [IO.FileMode]::Open,
        [IO.FileAccess]::Read,
        [IO.FileShare]::Read)
    try {
        if ($stream.Length -lt 2 -or $stream.Length -gt $maximumSummaryBytes) {
            throw 'Summary length is invalid.'
        }

        $bytes = New-Object byte[] ([int] $stream.Length)
        $offset = 0
        while ($offset -lt $bytes.Length) {
            $read = $stream.Read($bytes, $offset, $bytes.Length - $offset)
            if ($read -eq 0) {
                throw 'Summary ended during its bounded read.'
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
        throw 'Summary framing is invalid.'
    }

    $lineFeedCount = 0
    foreach ($value in $bytes) {
        if ($value -eq 0x0d) {
            throw 'Summary contains a carriage return.'
        }

        if ($value -eq 0x0a) {
            $lineFeedCount++
        }
    }

    if ($lineFeedCount -ne 1) {
        throw 'Summary must have exactly one final line feed.'
    }

    $jsonBytes = New-Object byte[] ($bytes.Length - 1)
    [Array]::Copy($bytes, 0, $jsonBytes, 0, $jsonBytes.Length)
    $utf8 = [Text.UTF8Encoding]::new($false, $true)
    $json = $utf8.GetString($jsonBytes)
    if ([string]::IsNullOrEmpty($json)) {
        throw 'Summary JSON is empty.'
    }

    $summary = $json | ConvertFrom-Json -ErrorAction Stop
    return [pscustomobject]@{
        Bytes = $bytes
        Json = $json
        Summary = $summary
        Utf8 = $utf8
    }
}

function Assert-Gate9Summary {
    param(
        [Parameter(Mandatory)] [object] $Document,
        [AllowNull()] [string] $RequiredCommit,
        [Parameter(Mandatory)] [bool] $CommitWasSpecified
    )

    $summary = $Document.Summary
    Assert-ExactProperties -InputObject $summary -Expected @(
        'schema',
        'classification',
        'evaluatedCommit',
        'target',
        'offline',
        'noRelevantNetworkEndpointObserved',
        'repetitions',
        'cleanupVerified',
        'failures',
        'releaseTrust',
        'disposition')
    Assert-ExactString `
        -Value $summary.schema `
        -Expected 'granite.hardware-inspection.gate9-engineering-acceptance/v1'
    Assert-ExactString -Value $summary.classification -Expected 'local-sanitized'
    if ($summary.evaluatedCommit -isnot [string] -or
        $summary.evaluatedCommit -cnotmatch '^[0-9a-f]{40}$') {
        throw 'Evaluated commit is invalid.'
    }

    if ($CommitWasSpecified) {
        if ($RequiredCommit -cnotmatch '^[0-9a-f]{40}$' -or
            $summary.evaluatedCommit -cne $RequiredCommit) {
            throw 'Evaluated commit does not match.'
        }
    }

    Assert-ExactProperties -InputObject $summary.target -Expected @(
        'windows11', 'x64', 'intel', 'physical')
    Assert-TrueBoolean -Value $summary.target.windows11
    Assert-TrueBoolean -Value $summary.target.x64
    Assert-TrueBoolean -Value $summary.target.intel
    Assert-TrueBoolean -Value $summary.target.physical
    Assert-TrueBoolean -Value $summary.offline
    Assert-TrueBoolean -Value $summary.noRelevantNetworkEndpointObserved

    if ($summary.repetitions -isnot [Array]) {
        throw 'Repetitions must be an array.'
    }

    [object[]] $repetitions = @($summary.repetitions)
    if ($repetitions.Count -ne 3) {
        throw 'Exactly three repetitions are required.'
    }

    $canonicalRepetitions = [Collections.Generic.List[object]]::new()
    $campaignOutcome = $null
    for ($index = 0; $index -lt $repetitions.Count; $index++) {
        $repetition = $repetitions[$index]
        Assert-ExactProperties -InputObject $repetition -Expected @(
            'run',
            'packageIdentityPresent',
            'outcome',
            'stageCount',
            'handoffPresent',
            'manifestFieldCount',
            'diagnostics')
        Assert-ExactInteger -Value $repetition.run -Expected ($index + 1)
        Assert-TrueBoolean -Value $repetition.packageIdentityPresent
        if ($repetition.outcome -isnot [string] -or
            $repetition.outcome -cnotin @('Completed', 'CompletedWithWarnings')) {
            throw 'Repetition outcome is invalid.'
        }

        if ($null -eq $campaignOutcome) {
            $campaignOutcome = $repetition.outcome
        }
        elseif ($campaignOutcome -cne $repetition.outcome) {
            throw 'Repetition outcomes are inconsistent.'
        }

        Assert-ExactInteger -Value $repetition.stageCount -Expected 7
        Assert-TrueBoolean -Value $repetition.handoffPresent
        Assert-ExactInteger -Value $repetition.manifestFieldCount -Expected 19
        Assert-CanonicalDiagnostics -Value $repetition.diagnostics

        $canonicalRepetitions.Add([ordered]@{
            run = [long] $repetition.run
            packageIdentityPresent = [bool] $repetition.packageIdentityPresent
            outcome = [string] $repetition.outcome
            stageCount = [long] $repetition.stageCount
            handoffPresent = [bool] $repetition.handoffPresent
            manifestFieldCount = [long] $repetition.manifestFieldCount
            diagnostics = [string[]] @($repetition.diagnostics)
        })
    }

    Assert-TrueBoolean -Value $summary.cleanupVerified
    if ($summary.failures -isnot [Array] -or @($summary.failures).Count -ne 0) {
        throw 'Failures must be an empty array.'
    }

    Assert-ExactProperties -InputObject $summary.releaseTrust -Expected @(
        'signatureKind', 'publicTrustVerified', 'smartAppControlVerified')
    if ($summary.releaseTrust.signatureKind -isnot [string] -or
        $summary.releaseTrust.signatureKind -cnotin @('Developer', 'Enterprise', 'Store') -or
        $summary.releaseTrust.publicTrustVerified -isnot [bool] -or
        $summary.releaseTrust.smartAppControlVerified -isnot [bool]) {
        throw 'Release trust values are invalid.'
    }

    $signatureKind = [string] $summary.releaseTrust.signatureKind
    $publicTrustVerified = [bool] $summary.releaseTrust.publicTrustVerified
    $smartAppControlVerified = [bool] $summary.releaseTrust.smartAppControlVerified
    if ($signatureKind -ceq 'Developer' -and
        ($publicTrustVerified -or $smartAppControlVerified)) {
        throw 'Developer trust cannot be promoted.'
    }

    if ($smartAppControlVerified -and -not $publicTrustVerified) {
        throw 'Smart App Control trust requires public trust.'
    }

    $fullyReleaseTrusted =
        $signatureKind -cin @('Enterprise', 'Store') -and
        $publicTrustVerified -and
        $smartAppControlVerified
    $requiredDisposition = if ($fullyReleaseTrusted) {
        'Passed'
    }
    else {
        'EngineeringPassedReleaseBlocked'
    }
    Assert-ExactString -Value $summary.disposition -Expected $requiredDisposition

    $canonical = [ordered]@{
        schema = 'granite.hardware-inspection.gate9-engineering-acceptance/v1'
        classification = 'local-sanitized'
        evaluatedCommit = [string] $summary.evaluatedCommit
        target = [ordered]@{
            windows11 = $true
            x64 = $true
            intel = $true
            physical = $true
        }
        offline = $true
        noRelevantNetworkEndpointObserved = $true
        repetitions = [object[]] $canonicalRepetitions.ToArray()
        cleanupVerified = $true
        failures = [object[]] @()
        releaseTrust = [ordered]@{
            signatureKind = $signatureKind
            publicTrustVerified = $publicTrustVerified
            smartAppControlVerified = $smartAppControlVerified
        }
        disposition = $requiredDisposition
    }
    $canonicalJson = $canonical | ConvertTo-Json -Depth 8 -Compress
    [byte[]] $canonicalBytes = $Document.Utf8.GetBytes($canonicalJson + "`n")
    if (-not [Linq.Enumerable]::SequenceEqual(
            [byte[]] $Document.Bytes,
            $canonicalBytes)) {
        throw 'Summary is not canonical JSON.'
    }
}

try {
    $document = Read-BoundedCanonicalSummary -InputPath $Path
    Assert-Gate9Summary `
        -Document $document `
        -RequiredCommit $ExpectedCommit `
        -CommitWasSpecified $PSBoundParameters.ContainsKey('ExpectedCommit')
}
catch {
    [Console]::Error.WriteLine($failureMessage)
    exit 1
}

exit 0
