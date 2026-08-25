Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Find-HardwareInspectionCodeIntegrityBlock {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [object[]] $Events,

        [Parameter(Mandatory)]
        [ValidatePattern('^[A-Za-z0-9_.-]{1,255}$')]
        [string] $PackageFullName,

        [Parameter(Mandatory)]
        [DateTime] $NotBeforeUtc
    )

    if ($Events.Count -gt 1024) {
        throw 'The Code Integrity event inventory exceeds its closed bound.'
    }

    $packageMarker = '\Program Files\WindowsApps\' + $PackageFullName + '\'
    $matches = [Collections.Generic.List[object]]::new()
    foreach ($eventRecord in $Events) {
        if ($null -eq $eventRecord -or
            [int] $eventRecord.Id -notin @(3033, 3077) -or
            $null -eq $eventRecord.TimeCreated -or
            ([DateTime] $eventRecord.TimeCreated).ToUniversalTime() -lt
                $NotBeforeUtc.ToUniversalTime()) {
            continue
        }

        $message = [string] $eventRecord.Message
        if ([string]::IsNullOrWhiteSpace($message) -or
            $message.IndexOf(
                $packageMarker,
                [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            continue
        }

        $moduleMatch = [Text.RegularExpressions.Regex]::Match(
            $message,
            'attempted to load [^\r\n]*\\(?<module>[A-Za-z0-9 _.()\-]{1,240}\.(?:dll|exe)) that did not meet',
            [Text.RegularExpressions.RegexOptions]::CultureInvariant -bor
                [Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if (-not $moduleMatch.Success) {
            continue
        }

        $policyId = $null
        $policyMatch = [Text.RegularExpressions.Regex]::Match(
            $message,
            'Policy ID:\{(?<policy>[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12})\}',
            [Text.RegularExpressions.RegexOptions]::CultureInvariant)
        if ($policyMatch.Success) {
            $policyId = $policyMatch.Groups['policy'].Value.ToLowerInvariant()
        }

        $matches.Add([pscustomobject]@{
                EventId = [int] $eventRecord.Id
                TimeCreatedUtc = ([DateTime] $eventRecord.TimeCreated).ToUniversalTime()
                ModuleName = $moduleMatch.Groups['module'].Value
                PolicyId = $policyId
            })
    }

    $matches |
        Sort-Object `
            @{ Expression = { if ($_.EventId -eq 3077) { 0 } else { 1 } } },
            @{ Expression = { $_.TimeCreatedUtc }; Descending = $true } |
        Select-Object -First 1
}

function Resolve-HardwareInspectionSigningArguments {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateCount(1, 128)]
        [string[]] $ArgumentTemplate,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string] $FilePath
    )

    $resolvedPath = [IO.Path]::GetFullPath($FilePath)
    $tokenCount = 0
    $resolved = [Collections.Generic.List[string]]::new()
    foreach ($argument in $ArgumentTemplate) {
        if ($null -eq $argument -or $argument.Length -gt 4096) {
            throw 'A trusted-signing argument is outside its closed bound.'
        }

        if ([string]::Equals(
                $argument,
                '{FilePath}',
                [StringComparison]::Ordinal)) {
            $tokenCount++
            $resolved.Add($resolvedPath)
            continue
        }

        if ($argument.Contains('{FilePath}') -or
            $argument.Contains([char]0) -or
            $argument.Contains("`r") -or
            $argument.Contains("`n")) {
            throw 'A trusted-signing argument violates the structured argument contract.'
        }

        $resolved.Add($argument)
    }

    if ($tokenCount -ne 1) {
        throw 'The trusted-signing arguments must contain exactly one whole file-path token.'
    }

    return [string[]] $resolved.ToArray()
}

function Assert-HardwareInspectionNormalFile {
    param(
        [Parameter(Mandatory)] [string] $Path,
        [Parameter(Mandatory)] [string] $Description
    )

    $fullPath = [IO.Path]::GetFullPath($Path)
    if (-not [IO.File]::Exists($fullPath)) {
        throw "$Description is absent."
    }

    $file = Get-Item -LiteralPath $fullPath -Force -ErrorAction Stop
    if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "$Description has a reparse-point ancestor."
    }

    $currentDirectory = $file.Directory
    while ($null -ne $currentDirectory) {
        if (($currentDirectory.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "$Description has a reparse-point ancestor."
        }

        $currentDirectory = $currentDirectory.Parent
    }

    return $fullPath
}

function Invoke-HardwareInspectionTrustedSigner {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string] $FilePath,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string] $ProviderPath,

        [Parameter(Mandatory)]
        [ValidatePattern('^[0-9A-Fa-f]{64}$')]
        [string] $ExpectedProviderSha256,

        [Parameter(Mandatory)]
        [ValidateCount(1, 128)]
        [string[]] $ArgumentTemplate,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string] $ExpectedPublisher,

        [Parameter()]
        [ValidatePattern('^[0-9A-Fa-f]{40}$')]
        [string] $ExpectedSignerThumbprint
    )

    $target = Assert-HardwareInspectionNormalFile `
        -Path $FilePath `
        -Description 'The trusted-signing target'
    $provider = Assert-HardwareInspectionNormalFile `
        -Path $ProviderPath `
        -Description 'The trusted-signing provider'
    $providerHash = (Get-FileHash `
            -LiteralPath $provider `
            -Algorithm SHA256).Hash
    if (-not [string]::Equals(
            $providerHash,
            $ExpectedProviderSha256,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The trusted-signing provider hash is invalid.'
    }

    $arguments = Resolve-HardwareInspectionSigningArguments `
        -ArgumentTemplate $ArgumentTemplate `
        -FilePath $target
    $global:LASTEXITCODE = 0
    & $provider @arguments
    if (-not $? -or $LASTEXITCODE -ne 0) {
        throw "The trusted-signing provider failed with exit code $LASTEXITCODE."
    }

    $signature = Get-AuthenticodeSignature -LiteralPath $target
    $certificate = $signature.SignerCertificate
    if ($signature.Status -ne [Management.Automation.SignatureStatus]::Valid -or
        $null -eq $certificate -or
        -not [string]::Equals(
            $certificate.Subject,
            $ExpectedPublisher,
            [StringComparison]::Ordinal) -or
        $certificate.PublicKey.Oid.Value -cne '1.2.840.113549.1.1.1' -or
        @($certificate.EnhancedKeyUsageList | Where-Object {
                [string] $_.ObjectId -ceq '1.3.6.1.5.5.7.3.3'
            }).Count -ne 1 -or
        $null -eq $signature.TimeStamperCertificate) {
        throw 'The trusted-signing result does not satisfy the public signing contract.'
    }

    if ($PSBoundParameters.ContainsKey('ExpectedSignerThumbprint') -and
        -not [string]::Equals(
            $certificate.Thumbprint,
            $ExpectedSignerThumbprint,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The trusted-signing result has an unexpected signer thumbprint.'
    }

    $chain = [Security.Cryptography.X509Certificates.X509Chain]::new()
    try {
        $chain.ChainPolicy.RevocationMode =
            [Security.Cryptography.X509Certificates.X509RevocationMode]::Online
        $chain.ChainPolicy.RevocationFlag =
            [Security.Cryptography.X509Certificates.X509RevocationFlag]::ExcludeRoot
        $chain.ChainPolicy.VerificationFlags =
            [Security.Cryptography.X509Certificates.X509VerificationFlags]::NoFlag
        $chain.ChainPolicy.UrlRetrievalTimeout = [TimeSpan]::FromSeconds(30)
        $chain.ChainPolicy.ApplicationPolicy.Add(
            [Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.3'))
        if (-not $chain.Build($certificate)) {
            throw 'The trusted-signing certificate did not build to a trusted public root.'
        }
    }
    finally {
        $chain.Dispose()
    }

    [pscustomobject]@{
        Path = $target
        Sha256 = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
        Publisher = $certificate.Subject
        Thumbprint = $certificate.Thumbprint
        TimestampThumbprint = $signature.TimeStamperCertificate.Thumbprint
        ChainTrustVerified = $true
    }
}

Export-ModuleMember -Function @(
    'Find-HardwareInspectionCodeIntegrityBlock'
    'Resolve-HardwareInspectionSigningArguments'
    'Invoke-HardwareInspectionTrustedSigner'
)
