[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $BundleDirectory,

    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9A-Fa-f]{64}$')]
    [string] $ExpectedBundleSha256,

    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-f]{40}$')]
    [string] $ExpectedCommit,

    [Parameter(Mandatory)]
    [switch] $ConfirmSupportedIntelTarget,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $ReleaseTrustRecord
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$packageIdentityName = 'GraniteEdgeAI.WinUI.UnitTests'
$expectedPublisher = 'CN=GraniteEdgeAI'
$expectedPackageVersion = [Version] '1.0.0.0'
$maximumResultWait = [TimeSpan]::FromSeconds(180)
$maximumExitWait = [TimeSpan]::FromSeconds(10)
$llmFitExecutableSha256 =
    'db82bcb17f065b7ff7528ffe9904b2b0e1cce0c843fcd659b4ee1432a2e72e19'

function Assert-Gate9SupportedTargetFacts {
    param(
        [Parameter(Mandatory)] [bool] $IsAdministrator,
        [Parameter(Mandatory)] [int] $OsBuild,
        [Parameter(Mandatory)] [string] $OsArchitecture,
        [Parameter(Mandatory)] [string[]] $ProcessorManufacturers,
        [Parameter(Mandatory)] [string] $ComputerManufacturer,
        [Parameter(Mandatory)] [string] $ComputerModel,
        [Parameter(Mandatory)] [int] $ConnectedPhysicalAdapterCount
    )

    if (-not $IsAdministrator -or
        $OsBuild -lt 22000 -or
        -not [string]::Equals(
            $OsArchitecture,
            'X64',
            [StringComparison]::OrdinalIgnoreCase) -or
        $ProcessorManufacturers.Count -lt 1 -or
        $ConnectedPhysicalAdapterCount -ne 0) {
        throw 'The machine does not satisfy the supported target contract.'
    }

    foreach ($manufacturer in $ProcessorManufacturers) {
        if ($manufacturer -cnotin @('GenuineIntel', 'Genuine Intel')) {
            throw 'The machine does not satisfy the supported target contract.'
        }
    }

    $virtualIdentity = ($ComputerManufacturer + ' ' + $ComputerModel).ToLowerInvariant()
    foreach ($marker in @(
            'virtual machine',
            'vmware',
            'virtualbox',
            'qemu',
            'kvm',
            'xen',
            'amazon ec2',
            'google compute engine',
            'parallels',
            'hyper-v')) {
        if ($virtualIdentity.Contains($marker)) {
            throw 'The machine does not satisfy the supported target contract.'
        }
    }
}

function Read-Gate9ProductionResult {
    param([Parameter(Mandatory)] [string] $Path)

    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 2 -or
        $bytes.Length -gt 4KB -or
        $bytes[$bytes.Length - 1] -ne 0x0a -or
        @($bytes | Where-Object { $_ -eq 0x0a }).Count -ne 1 -or
        @($bytes | Where-Object { $_ -eq 0x0d }).Count -ne 0 -or
        ($bytes.Length -ge 3 -and
            $bytes[0] -eq 0xef -and
            $bytes[1] -eq 0xbb -and
            $bytes[2] -eq 0xbf)) {
        throw 'The Gate 9 production result has invalid framing.'
    }

    $text = [Text.UTF8Encoding]::new($false, $true).GetString($bytes)
    $pattern = '^(?:' +
        '\{"schema":"granite\.hardware-inspection\.gate9-production-run/v1",' +
        '"packageIdentityPresent":true,"outcome":"(?<outcome>Completed|CompletedWithWarnings)",' +
        '"stageCount":7,"handoffPresent":true,"manifestFieldCount":19,' +
        '"diagnostics":\[\]\}\n)$'
    $match = [Text.RegularExpressions.Regex]::Match(
        $text,
        $pattern,
        [Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if (-not $match.Success) {
        throw 'The Gate 9 production result does not match its closed grammar.'
    }

    [pscustomobject]@{
        PackageIdentityPresent = $true
        Outcome = $match.Groups['outcome'].Value
        StageCount = 7
        HandoffPresent = $true
        ManifestFieldCount = 19
        Diagnostics = [string[]] @()
    }
}

function Get-Gate9OwnedProcessIds {
    param(
        [Parameter(Mandatory)] [int] $RootProcessId,
        [Parameter(Mandatory)] [object[]] $ProcessRecords
    )

    if ($RootProcessId -lt 1 -or $ProcessRecords.Count -gt 65536) {
        throw 'The process inventory is invalid.'
    }

    $owned = [Collections.Generic.HashSet[int]]::new()
    [void] $owned.Add($RootProcessId)
    $changed = $true
    while ($changed) {
        $changed = $false
        foreach ($record in $ProcessRecords) {
            $processId = [int] $record.ProcessId
            $parentProcessId = [int] $record.ParentProcessId
            if ($processId -gt 0 -and
                $owned.Contains($parentProcessId) -and
                $owned.Add($processId)) {
                $changed = $true
                if ($owned.Count -gt 256) {
                    throw 'The invocation-owned process tree exceeds its bound.'
                }
            }
        }
    }

    [int[]] $result = @($owned)
    [Array]::Sort($result)
    $result
}

function Test-Gate9OwnedEndpointRecords {
    param(
        [Parameter(Mandatory)] [int[]] $OwnedProcessIds,
        [Parameter(Mandatory)] [AllowEmptyCollection()] [object[]] $TcpRecords,
        [Parameter(Mandatory)] [AllowEmptyCollection()] [object[]] $UdpRecords
    )

    $owned = [Collections.Generic.HashSet[int]]::new()
    foreach ($processId in $OwnedProcessIds) {
        [void] $owned.Add([int] $processId)
    }

    foreach ($record in $TcpRecords) {
        if ($owned.Contains([int] $record.OwningProcess) -and
            [string] $record.State -cin @('Listen', 'Bound')) {
            return $true
        }
    }

    foreach ($record in $UdpRecords) {
        if ($owned.Contains([int] $record.OwningProcess)) {
            return $true
        }
    }

    return $false
}

function Write-Gate9BytesAtomically {
    param(
        [Parameter(Mandatory)] [string] $DestinationPath,
        [Parameter(Mandatory)] [byte[]] $Bytes
    )

    $destination = [IO.Path]::GetFullPath($DestinationPath)
    $parent = [IO.Path]::GetDirectoryName($destination)
    if ([string]::IsNullOrWhiteSpace($parent) -or
        -not [IO.Directory]::Exists($parent) -or
        [IO.File]::Exists($destination) -or
        [IO.Directory]::Exists($destination)) {
        throw 'The Gate 9 publication destination is invalid.'
    }

    $temporary = Join-Path $parent (
        '.gate9-publication.{0}.tmp' -f [Guid]::NewGuid().ToString('N'))
    try {
        $stream = [IO.FileStream]::new(
            $temporary,
            [IO.FileMode]::CreateNew,
            [IO.FileAccess]::Write,
            [IO.FileShare]::None)
        try {
            $stream.Write($Bytes, 0, $Bytes.Length)
            $stream.Flush($true)
        }
        finally {
            $stream.Dispose()
        }

        [IO.File]::Move($temporary, $destination)
    }
    finally {
        if ([IO.File]::Exists($temporary)) {
            Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue
        }
    }
}

function Write-Gate9EngineeringSummary {
    param(
        [Parameter(Mandatory)] [string] $DestinationPath,
        [Parameter(Mandatory)] [string] $ExpectedCommit,
        [Parameter(Mandatory)] [object[]] $Repetitions,
        [Parameter(Mandatory)] [string] $SignatureKind,
        [Parameter(Mandatory)] [bool] $PublicTrustVerified,
        [Parameter(Mandatory)] [bool] $SmartAppControlVerified,
        [Parameter(Mandatory)] [string] $SummaryValidatorPath
    )

    if ($ExpectedCommit -cnotmatch '^[0-9a-f]{40}$' -or
        $Repetitions.Count -ne 3 -or
        $SignatureKind -cnotin @('Developer', 'Enterprise', 'Store') -or
        ($SignatureKind -ceq 'Developer' -and
            ($PublicTrustVerified -or $SmartAppControlVerified)) -or
        ($SmartAppControlVerified -and -not $PublicTrustVerified)) {
        throw 'The Gate 9 summary inputs are invalid.'
    }

    $canonicalRepetitions = [Collections.Generic.List[object]]::new()
    for ($index = 0; $index -lt 3; $index++) {
        $repetition = $Repetitions[$index]
        if ([int] $repetition.Run -ne $index + 1 -or
            [string] $repetition.Outcome -cnotin @('Completed', 'CompletedWithWarnings') -or
            @($repetition.Diagnostics).Count -ne 0) {
            throw 'A Gate 9 summary repetition is invalid.'
        }

        $canonicalRepetitions.Add([ordered]@{
            run = $index + 1
            packageIdentityPresent = $true
            outcome = [string] $repetition.Outcome
            stageCount = 7
            handoffPresent = $true
            manifestFieldCount = 19
            diagnostics = [string[]] @()
        })
    }

    if (@($canonicalRepetitions | ForEach-Object { $_.outcome } |
            Select-Object -Unique).Count -ne 1) {
        throw 'Gate 9 repetition outcomes are inconsistent.'
    }

    $releasePassed =
        $SignatureKind -cin @('Enterprise', 'Store') -and
        $PublicTrustVerified -and
        $SmartAppControlVerified
    $disposition = if ($releasePassed) {
        'Passed'
    }
    else {
        'EngineeringPassedReleaseBlocked'
    }
    $summary = [ordered]@{
        schema = 'granite.hardware-inspection.gate9-engineering-acceptance/v1'
        classification = 'local-sanitized'
        evaluatedCommit = $ExpectedCommit
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
            signatureKind = $SignatureKind
            publicTrustVerified = $PublicTrustVerified
            smartAppControlVerified = $SmartAppControlVerified
        }
        disposition = $disposition
    }
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes(
        (($summary | ConvertTo-Json -Depth 8 -Compress) + "`n"))
    if ($bytes.Length -gt 16KB) {
        throw 'The Gate 9 summary exceeds its byte bound.'
    }

    $destination = [IO.Path]::GetFullPath($DestinationPath)
    $validationPath = Join-Path ([IO.Path]::GetDirectoryName($destination)) (
        '.gate9-validation.{0}.json' -f [Guid]::NewGuid().ToString('N'))
    try {
        Write-Gate9BytesAtomically -DestinationPath $validationPath -Bytes $bytes
        & $SummaryValidatorPath -Path $validationPath -ExpectedCommit $ExpectedCommit
        if ($LASTEXITCODE -ne 0) {
            throw 'The Gate 9 summary failed independent validation.'
        }

        if ([IO.File]::Exists($destination) -or [IO.Directory]::Exists($destination)) {
            throw 'The Gate 9 summary destination already exists.'
        }

        [IO.File]::Move($validationPath, $destination)
    }
    finally {
        if ([IO.File]::Exists($validationPath)) {
            Remove-Item -LiteralPath $validationPath -Force -ErrorAction SilentlyContinue
        }
    }
}

function Get-Gate9NonRootFullPath {
    param(
        [Parameter(Mandatory)] [string] $Path,
        [Parameter(Mandatory)] [string] $Description
    )

    if ($Path -notmatch '^[A-Za-z]:[\\/]') {
        throw "$Description must be an absolute Windows path."
    }

    $fullPath = [IO.Path]::GetFullPath($Path)
    if ([string]::Equals(
            $fullPath,
            [IO.Path]::GetPathRoot($fullPath),
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description must not be a filesystem root."
    }

    $fullPath.TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
}

function Assert-Gate9NoReparseAncestors {
    param([Parameter(Mandatory)] [string] $Path)

    $fullPath = [IO.Path]::GetFullPath($Path)
    $current = if ([IO.File]::Exists($fullPath)) {
        Get-Item -LiteralPath $fullPath -Force -ErrorAction Stop
    }
    elseif ([IO.Directory]::Exists($fullPath)) {
        Get-Item -LiteralPath $fullPath -Force -ErrorAction Stop
    }
    else {
        throw 'A required Gate 9 path is absent.'
    }

    while ($null -ne $current) {
        if (($current.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'A required Gate 9 path traverses a reparse point.'
        }

        $current = if ($current -is [IO.FileInfo]) {
            $current.Directory
        }
        else {
            $current.Parent
        }
    }
}

function Assert-Gate9ExactFlatInventory {
    param(
        [Parameter(Mandatory)] [string] $Directory,
        [Parameter(Mandatory)] [string[]] $ExpectedNames
    )

    $items = @(Get-ChildItem -LiteralPath $Directory -Force -ErrorAction Stop)
    if (@($items | Where-Object { $_.PSIsContainer }).Count -ne 0 -or
        @($items | Where-Object {
                ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0
            }).Count -ne 0) {
        throw 'A Gate 9 package is not a physical flat file inventory.'
    }

    [string[]] $actual = @($items | ForEach-Object { $_.Name })
    [string[]] $expected = @($ExpectedNames)
    [Array]::Sort($actual, [StringComparer]::Ordinal)
    [Array]::Sort($expected, [StringComparer]::Ordinal)
    if ($actual.Count -ne $expected.Count) {
        throw 'A Gate 9 package inventory is not exact.'
    }

    for ($index = 0; $index -lt $expected.Count; $index++) {
        if ($actual[$index] -cne $expected[$index]) {
            throw 'A Gate 9 package inventory is not exact.'
        }
    }
}

function Read-Gate9BundleManifest {
    param([Parameter(Mandatory)] [string] $BundleRoot)

    $manifestPath = Join-Path $BundleRoot 'bundle-manifest.json'
    $bytes = [IO.File]::ReadAllBytes($manifestPath)
    if ($bytes.Length -lt 2 -or
        $bytes.Length -gt 16KB -or
        $bytes[$bytes.Length - 1] -ne 0x0a -or
        @($bytes | Where-Object { $_ -eq 0x0a }).Count -ne 1 -or
        @($bytes | Where-Object { $_ -eq 0x0d }).Count -ne 0 -or
        ($bytes.Length -ge 3 -and
            $bytes[0] -eq 0xef -and
            $bytes[1] -eq 0xbb -and
            $bytes[2] -eq 0xbf)) {
        throw 'The Gate 9 bundle manifest framing is invalid.'
    }

    $text = [Text.UTF8Encoding]::new($false, $true).GetString($bytes)
    $pattern = '^\{"schema":"granite\.hardware-inspection\.development-acceptance-bundle/v1",' +
        '"packageName":"GraniteEdgeAI\.WinUI\.UnitTests",' +
        '"publisher":"CN=GraniteEdgeAI","version":"1\.0\.0\.0",' +
        '"architecture":"x64","certificateThumbprint":"(?<thumbprint>[0-9A-F]{40})",' +
        '"files":\[' +
        '\{"name":"GraniteEdgeAI\.UnitTests\.msix","length":(?<msixLength>[1-9][0-9]{0,10}),"sha256":"(?<msixHash>[0-9a-f]{64})"\},' +
        '\{"name":"GraniteEdgeAI\.cer","length":(?<cerLength>[1-9][0-9]{0,10}),"sha256":"(?<cerHash>[0-9a-f]{64})"\},' +
        '\{"name":"Invoke-HardwareInspectionDevelopmentAcceptanceGuest\.ps1","length":(?<runnerLength>[1-9][0-9]{0,10}),"sha256":"(?<runnerHash>[0-9a-f]{64})"\}' +
        '\]\}\n$'
    $match = [Text.RegularExpressions.Regex]::Match(
        $text,
        $pattern,
        [Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if (-not $match.Success) {
        throw 'The Gate 9 bundle manifest does not match its closed grammar.'
    }

    [pscustomobject]@{
        CertificateThumbprint = $match.Groups['thumbprint'].Value
        PackageSha256 = $match.Groups['msixHash'].Value
        Files = @(
            [pscustomobject]@{
                Name = 'GraniteEdgeAI.UnitTests.msix'
                Length = [long] $match.Groups['msixLength'].Value
                Sha256 = $match.Groups['msixHash'].Value
            },
            [pscustomobject]@{
                Name = 'GraniteEdgeAI.cer'
                Length = [long] $match.Groups['cerLength'].Value
                Sha256 = $match.Groups['cerHash'].Value
            },
            [pscustomobject]@{
                Name = 'Invoke-HardwareInspectionDevelopmentAcceptanceGuest.ps1'
                Length = [long] $match.Groups['runnerLength'].Value
                Sha256 = $match.Groups['runnerHash'].Value
            })
    }
}

function Assert-Gate9Bundle {
    param(
        [Parameter(Mandatory)] [string] $BundleRoot,
        [Parameter(Mandatory)] [string] $ExpectedArchiveSha256
    )

    Assert-Gate9NoReparseAncestors -Path $BundleRoot
    $archivePath = $BundleRoot + '.zip'
    Assert-Gate9NoReparseAncestors -Path $archivePath
    $archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
    if (-not [string]::Equals(
            $archiveHash,
            $ExpectedArchiveSha256,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The Gate 9 bundle archive hash is invalid.'
    }

    Assert-Gate9ExactFlatInventory -Directory $BundleRoot -ExpectedNames @(
        'GraniteEdgeAI.UnitTests.msix',
        'GraniteEdgeAI.cer',
        'Invoke-HardwareInspectionDevelopmentAcceptanceGuest.ps1',
        'bundle-manifest.json')
    $manifest = Read-Gate9BundleManifest -BundleRoot $BundleRoot
    foreach ($contract in $manifest.Files) {
        $path = Join-Path $BundleRoot $contract.Name
        $item = Get-Item -LiteralPath $path -Force -ErrorAction Stop
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        if ($item.Length -ne $contract.Length -or
            -not [string]::Equals(
                $hash,
                $contract.Sha256,
                [StringComparison]::OrdinalIgnoreCase)) {
            throw 'A Gate 9 bundle payload differs from its manifest.'
        }
    }

    $certificatePath = Join-Path $BundleRoot 'GraniteEdgeAI.cer'
    $certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new(
        $certificatePath)
    try {
        $codeSigningEku = @($certificate.EnhancedKeyUsageList | Where-Object {
                [string]::Equals(
                    [string] $_.ObjectId,
                    '1.3.6.1.5.5.7.3.3',
                    [StringComparison]::Ordinal)
            }).Count -eq 1
        if (-not [string]::Equals(
                $certificate.Thumbprint,
                $manifest.CertificateThumbprint,
                [StringComparison]::OrdinalIgnoreCase) -or
            -not [string]::Equals(
                $certificate.Subject,
                $expectedPublisher,
                [StringComparison]::Ordinal) -or
            $certificate.HasPrivateKey -or
            -not $codeSigningEku -or
            [DateTime]::Now -lt $certificate.NotBefore -or
            [DateTime]::Now -gt $certificate.NotAfter) {
            throw 'The Gate 9 bundle certificate is invalid.'
        }

        $packagePath = Join-Path $BundleRoot 'GraniteEdgeAI.UnitTests.msix'
        $signature = Get-AuthenticodeSignature -FilePath $packagePath
        if ($signature.Status -ne [Management.Automation.SignatureStatus]::Valid -or
            $null -eq $signature.SignerCertificate -or
            -not [string]::Equals(
                $signature.SignerCertificate.Thumbprint,
                $certificate.Thumbprint,
                [StringComparison]::OrdinalIgnoreCase)) {
            throw 'The Gate 9 package signature is invalid.'
        }
    }
    finally {
        $certificate.Dispose()
    }

    $manifest
}

function Assert-Gate9LlmFitPackage {
    $llmFitRoot = Join-Path $env:ProgramData (
        'GraniteEdgeAI\HardwareInspection\llmfit\1.1.9\win-x64')
    Assert-Gate9NoReparseAncestors -Path $llmFitRoot
    Assert-Gate9ExactFlatInventory -Directory $llmFitRoot -ExpectedNames @(
        'llmfit.exe',
        'LICENSE',
        'README.md')
    $executable = Join-Path $llmFitRoot 'llmfit.exe'
    $actualHash = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash
    if (-not [string]::Equals(
            $actualHash,
            $llmFitExecutableSha256,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The fixed LLM Fit executable hash is invalid.'
    }

    foreach ($documentName in @('LICENSE', 'README.md')) {
        $document = Get-Item -LiteralPath (Join-Path $llmFitRoot $documentName) -Force
        if ($document.Length -lt 1 -or $document.Length -gt 1MB) {
            throw 'The fixed LLM Fit documentation inventory is invalid.'
        }
    }
}

function Get-Gate9Aumid {
    param(
        [Parameter(Mandatory)] $Package,
        [Parameter(Mandatory)] [string] $BundleRoot
    )

    $installRoot = [IO.Path]::GetFullPath([string] $Package.InstallLocation)
    if (-not [string]::Equals(
            [string] $Package.Name,
            $packageIdentityName,
            [StringComparison]::Ordinal) -or
        -not [string]::Equals(
            [string] $Package.Publisher,
            $expectedPublisher,
            [StringComparison]::Ordinal) -or
        [Version] $Package.Version -ne $expectedPackageVersion -or
        -not [string]::Equals(
            [string] $Package.Architecture,
            'X64',
            [StringComparison]::Ordinal) -or
        [string] $Package.SignatureKind -cnotin @('Developer', 'Enterprise', 'Store') -or
        $installRoot.StartsWith(
            $BundleRoot + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase) -or
        [string] $Package.PackageFamilyName -notmatch '^[A-Za-z0-9_.-]{1,255}$') {
        throw 'The installed Gate 9 package identity is invalid.'
    }

    "$($Package.PackageFamilyName)!App"
}

function Remove-Gate9TokenArtifacts {
    param(
        [Parameter(Mandatory)] [string] $AcceptanceRoot,
        [Parameter(Mandatory)] [string] $ResultToken
    )

    if ($ResultToken -cnotmatch '^[0-9a-f]{32}$') {
        throw 'The Gate 9 result token is invalid.'
    }

    $escaped = [Text.RegularExpressions.Regex]::Escape($ResultToken)
    $pattern = '^(?:' + $escaped + '\.json|\.' + $escaped +
        '\.[0-9a-f]{32}\.tmp)$'
    $items = @(Get-ChildItem -LiteralPath $AcceptanceRoot -Force -ErrorAction Stop |
        Where-Object { $_.Name -match $pattern })
    foreach ($item in $items) {
        if ($item.PSIsContainer -or
            ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'A Gate 9 result artifact is not a physical file.'
        }

        Remove-Item -LiteralPath $item.FullName -Force -ErrorAction Stop
    }

    if (@(Get-ChildItem -LiteralPath $AcceptanceRoot -Force -ErrorAction Stop |
            Where-Object { $_.Name -match $pattern }).Count -ne 0) {
        throw 'Gate 9 result artifacts remain after cleanup.'
    }
}

function Get-Gate9ProcessRecords {
    @(
        Get-CimInstance -ClassName Win32_Process -ErrorAction Stop |
            Select-Object ProcessId, ParentProcessId, CreationDate, Name
    )
}

function Update-Gate9OwnedProcessIdentities {
    param(
        [Parameter(Mandatory)] [int] $RootProcessId,
        [Parameter(Mandatory)] [hashtable] $OwnedIdentities,
        [Parameter(Mandatory)] [object[]] $ProcessRecords
    )

    $seeds = [Collections.Generic.HashSet[int]]::new()
    [void] $seeds.Add($RootProcessId)
    foreach ($key in $OwnedIdentities.Keys) {
        [void] $seeds.Add([int] $key)
    }

    foreach ($seed in $seeds) {
        foreach ($processId in @(Get-Gate9OwnedProcessIds `
                -RootProcessId $seed `
                -ProcessRecords $ProcessRecords)) {
            $record = @($ProcessRecords | Where-Object {
                    [int] $_.ProcessId -eq [int] $processId
                } | Select-Object -First 1)
            if ($record.Count -eq 1) {
                $identity = [string] $record[0].CreationDate
                if ($OwnedIdentities.ContainsKey([int] $processId) -and
                    $OwnedIdentities[[int] $processId] -cne $identity) {
                    throw 'A Gate 9 process identity was reused during observation.'
                }

                $OwnedIdentities[[int] $processId] = $identity
            }
        }
    }

    if ($OwnedIdentities.Count -gt 256) {
        throw 'The invocation-owned process tree exceeds its bound.'
    }
}

function Test-Gate9OwnedProcessesActive {
    param(
        [Parameter(Mandatory)] [hashtable] $OwnedIdentities,
        [Parameter(Mandatory)] [object[]] $ProcessRecords
    )

    foreach ($record in $ProcessRecords) {
        $processId = [int] $record.ProcessId
        if ($OwnedIdentities.ContainsKey($processId) -and
            $OwnedIdentities[$processId] -ceq [string] $record.CreationDate) {
            return $true
        }
    }

    return $false
}

function Stop-Gate9OwnedProcesses {
    param([Parameter(Mandatory)] [hashtable] $OwnedIdentities)

    $records = Get-Gate9ProcessRecords
    [int[]] $processIds = @($OwnedIdentities.Keys | ForEach-Object { [int] $_ })
    [Array]::Sort($processIds)
    [Array]::Reverse($processIds)
    foreach ($processId in $processIds) {
        $matching = @($records | Where-Object {
                [int] $_.ProcessId -eq $processId -and
                [string] $_.CreationDate -ceq $OwnedIdentities[$processId]
            })
        if ($matching.Count -eq 1) {
            Stop-Process -Id $processId -Force -ErrorAction Stop
        }
    }

    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        $remaining = Get-Gate9ProcessRecords
        if (-not (Test-Gate9OwnedProcessesActive `
                -OwnedIdentities $OwnedIdentities `
                -ProcessRecords $remaining)) {
            return
        }

        Start-Sleep -Milliseconds 20
    } while ([DateTime]::UtcNow -lt $deadline)

    throw 'Invocation-owned Gate 9 processes remain after cleanup.'
}

function Initialize-Gate9ActivationBoundary {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

namespace GraniteEdgeAI.HardwareInspection.Gate9
{
    [ComImport]
    [Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
    internal class ApplicationActivationManager { }

    [ComImport]
    [Guid("2e941141-7f97-4756-ba1d-9decde894a3d")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IApplicationActivationManager
    {
        int ActivateApplication(
            [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            [MarshalAs(UnmanagedType.LPWStr)] string arguments,
            uint options,
            out uint processId);
        int ActivateForFile(IntPtr appUserModelId, IntPtr itemArray, IntPtr verb, out uint processId);
        int ActivateForProtocol(IntPtr appUserModelId, IntPtr itemArray, out uint processId);
    }

    public static class Activation
    {
        public static uint Activate(string appUserModelId, string arguments)
        {
            var manager = (IApplicationActivationManager)new ApplicationActivationManager();
            uint processId;
            int result = manager.ActivateApplication(appUserModelId, arguments, 0, out processId);
            Marshal.ThrowExceptionForHR(result);
            return processId;
        }
    }
}
'@
}

function Invoke-Gate9Repetition {
    param(
        [Parameter(Mandatory)] [int] $Run,
        [Parameter(Mandatory)] [string] $AppUserModelId,
        [Parameter(Mandatory)] [string] $AcceptanceRoot
    )

    $resultToken = [Guid]::NewGuid().ToString('N')
    $resultPath = Join-Path $AcceptanceRoot "$resultToken.json"
    $ownedIdentities = @{}
    $endpointObserved = $false
    try {
        $arguments =
            "--hardware-inspection-gate9-acceptance --result-token $resultToken"
        $processId =
            [GraniteEdgeAI.HardwareInspection.Gate9.Activation]::Activate(
                $AppUserModelId,
                $arguments)
        if ($processId -lt 1 -or $processId -gt [int]::MaxValue) {
            throw 'Gate 9 activation returned an invalid process identity.'
        }

        $stopwatch = [Diagnostics.Stopwatch]::StartNew()
        $result = $null
        while ($null -eq $result) {
            if ($stopwatch.Elapsed -ge $maximumResultWait) {
                throw 'A Gate 9 repetition exceeded its fixed result timeout.'
            }

            $records = Get-Gate9ProcessRecords
            Update-Gate9OwnedProcessIdentities `
                -RootProcessId ([int] $processId) `
                -OwnedIdentities $ownedIdentities `
                -ProcessRecords $records
            if (-not $ownedIdentities.ContainsKey([int] $processId)) {
                throw 'Gate 9 monitoring did not observe the activated root process.'
            }

            [int[]] $ownedIds = @($ownedIdentities.Keys | ForEach-Object { [int] $_ })
            $tcp = @(Get-NetTCPConnection -ErrorAction Stop)
            $udp = @(Get-NetUDPEndpoint -ErrorAction Stop)
            if (Test-Gate9OwnedEndpointRecords `
                    -OwnedProcessIds $ownedIds `
                    -TcpRecords $tcp `
                    -UdpRecords $udp) {
                $endpointObserved = $true
            }

            if ([IO.File]::Exists($resultPath)) {
                $result = Read-Gate9ProductionResult -Path $resultPath
                break
            }

            if (-not (Test-Gate9OwnedProcessesActive `
                    -OwnedIdentities $ownedIdentities `
                    -ProcessRecords $records)) {
                throw 'A Gate 9 repetition exited without publishing a result.'
            }

            Start-Sleep -Milliseconds 20
        }

        $exitDeadline = [Diagnostics.Stopwatch]::StartNew()
        do {
            $records = Get-Gate9ProcessRecords
            Update-Gate9OwnedProcessIdentities `
                -RootProcessId ([int] $processId) `
                -OwnedIdentities $ownedIdentities `
                -ProcessRecords $records
            [int[]] $ownedIds = @($ownedIdentities.Keys | ForEach-Object { [int] $_ })
            if (Test-Gate9OwnedEndpointRecords `
                    -OwnedProcessIds $ownedIds `
                    -TcpRecords @(Get-NetTCPConnection -ErrorAction Stop) `
                    -UdpRecords @(Get-NetUDPEndpoint -ErrorAction Stop)) {
                $endpointObserved = $true
            }

            if (-not (Test-Gate9OwnedProcessesActive `
                    -OwnedIdentities $ownedIdentities `
                    -ProcessRecords $records)) {
                break
            }

            if ($exitDeadline.Elapsed -ge $maximumExitWait) {
                throw 'A Gate 9 repetition did not exit within its cleanup bound.'
            }

            Start-Sleep -Milliseconds 20
        } while ($true)

        if ($endpointObserved) {
            throw 'A Gate 9 process exposed a relevant network endpoint.'
        }

        [pscustomobject]@{
            Run = $Run
            Outcome = $result.Outcome
            Diagnostics = [string[]] @($result.Diagnostics)
        }
    }
    finally {
        $cleanupFailed = $false
        try {
            Stop-Gate9OwnedProcesses -OwnedIdentities $ownedIdentities
        }
        catch {
            $cleanupFailed = $true
        }
        try {
            Remove-Gate9TokenArtifacts `
                -AcceptanceRoot $AcceptanceRoot `
                -ResultToken $resultToken
        }
        catch {
            $cleanupFailed = $true
        }
        if ($cleanupFailed) {
            throw 'A Gate 9 repetition could not prove process and result cleanup.'
        }
    }
}

if (-not $ConfirmSupportedIntelTarget) {
    throw 'Explicit supported Intel target confirmation is required.'
}

$bundleRoot = Get-Gate9NonRootFullPath `
    -Path $BundleDirectory `
    -Description 'The Gate 9 bundle directory'
if (-not [IO.Directory]::Exists($bundleRoot)) {
    throw 'The Gate 9 bundle directory is absent.'
}

$summaryPath = Join-Path ([IO.Path]::GetDirectoryName($bundleRoot)) (
    'gate9-engineering-acceptance.json')
if ([IO.File]::Exists($summaryPath) -or [IO.Directory]::Exists($summaryPath)) {
    throw 'A prior Gate 9 engineering summary already exists.'
}

$manifest = Assert-Gate9Bundle `
    -BundleRoot $bundleRoot `
    -ExpectedArchiveSha256 $ExpectedBundleSha256
Assert-Gate9LlmFitPackage

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
$isAdministrator = $principal.IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
$operatingSystems = @(Get-CimInstance -ClassName Win32_OperatingSystem -ErrorAction Stop)
$processors = @(Get-CimInstance -ClassName Win32_Processor -ErrorAction Stop)
$computerSystems = @(Get-CimInstance -ClassName Win32_ComputerSystem -ErrorAction Stop)
$physicalAdapters = @(Get-NetAdapter -Physical -ErrorAction Stop)
if ($operatingSystems.Count -ne 1 -or
    $processors.Count -lt 1 -or
    $computerSystems.Count -ne 1) {
    throw 'Windows did not return a closed supported-target inventory.'
}

$osBuild = [int] $operatingSystems[0].BuildNumber
$osArchitecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
[string[]] $processorManufacturers = @($processors | ForEach-Object {
        [string] $_.Manufacturer
    })
$computerManufacturer = [string] $computerSystems[0].Manufacturer
$computerModel = [string] $computerSystems[0].Model
$connectedPhysicalAdapterCount = @($physicalAdapters | Where-Object {
        [string] $_.Status -ceq 'Up'
    }).Count
$operatingSystems = $null
$processors = $null
$computerSystems = $null
$physicalAdapters = $null
Assert-Gate9SupportedTargetFacts `
    -IsAdministrator $isAdministrator `
    -OsBuild $osBuild `
    -OsArchitecture $osArchitecture `
    -ProcessorManufacturers $processorManufacturers `
    -ComputerManufacturer $computerManufacturer `
    -ComputerModel $computerModel `
    -ConnectedPhysicalAdapterCount $connectedPhysicalAdapterCount
$processorManufacturers = $null
$computerManufacturer = $null
$computerModel = $null

$relevantProcessNames = @(
    'GraniteEdgeAI.UnitTests',
    'GraniteEdgeAI.HardwareInspection.LlamaCppProbe',
    'llmfit')
foreach ($processName in $relevantProcessNames) {
    if (@(Get-Process -Name $processName -ErrorAction SilentlyContinue).Count -ne 0) {
        throw 'A relevant process already exists before Gate 9 acceptance.'
    }
}

if (@(Get-AppxPackage -Name $packageIdentityName -ErrorAction Stop).Count -ne 0) {
    throw 'The Gate 9 test package must be absent before acceptance.'
}

Initialize-Gate9ActivationBoundary
$acceptanceRoot = [IO.Path]::GetFullPath((Join-Path $env:TEMP (
    'GraniteEdgeAI.HardwareInspection.Tests\Acceptance')))
$acceptanceRootCreated = $false
if (-not [IO.Directory]::Exists($acceptanceRoot)) {
    $null = New-Item -ItemType Directory -Path $acceptanceRoot
    $acceptanceRootCreated = $true
}
Assert-Gate9NoReparseAncestors -Path $acceptanceRoot
if (@(Get-ChildItem -LiteralPath $acceptanceRoot -Force -ErrorAction Stop).Count -ne 0) {
    throw 'The Gate 9 acceptance result root must be empty.'
}

$installedPackageFullName = $null
$installedPackage = $null
$repetitions = [Collections.Generic.List[object]]::new()
$cleanupVerified = $false
try {
    Add-AppxPackage `
        -Path (Join-Path $bundleRoot 'GraniteEdgeAI.UnitTests.msix') `
        -ErrorAction Stop
    $packages = @(Get-AppxPackage -Name $packageIdentityName -ErrorAction Stop)
    if ($packages.Count -ne 1) {
        throw 'The Gate 9 package was not installed exactly once.'
    }

    $installedPackage = $packages[0]
    $installedPackageFullName = [string] $installedPackage.PackageFullName
    if ([string]::IsNullOrWhiteSpace($installedPackageFullName)) {
        throw 'The Gate 9 package full name is invalid.'
    }

    $appUserModelId = Get-Gate9Aumid `
        -Package $installedPackage `
        -BundleRoot $bundleRoot
    for ($run = 1; $run -le 3; $run++) {
        $repetitions.Add((Invoke-Gate9Repetition `
                -Run $run `
                -AppUserModelId $appUserModelId `
                -AcceptanceRoot $acceptanceRoot))
    }
}
finally {
    $cleanupFailed = $false
    try {
        $cleanupPackages = @(
            Get-AppxPackage -Name $packageIdentityName -ErrorAction Stop)
        if ([string]::IsNullOrWhiteSpace($installedPackageFullName)) {
            if ($cleanupPackages.Count -eq 1) {
                $installedPackageFullName = [string] $cleanupPackages[0].PackageFullName
                if ([string]::IsNullOrWhiteSpace($installedPackageFullName)) {
                    $cleanupFailed = $true
                }
            }
            elseif ($cleanupPackages.Count -gt 1) {
                $cleanupFailed = $true
            }
        }

        if (-not [string]::IsNullOrWhiteSpace($installedPackageFullName)) {
            $exact = @($cleanupPackages |
                Where-Object {
                    [string]::Equals(
                        [string] $_.PackageFullName,
                        $installedPackageFullName,
                        [StringComparison]::Ordinal)
                })
            if ($exact.Count -ne 1) {
                $cleanupFailed = $true
            }
            else {
                Remove-AppxPackage `
                    -Package $installedPackageFullName `
                    -ErrorAction Stop
            }
        }

        if (@(Get-AppxPackage -Name $packageIdentityName -ErrorAction Stop).Count -ne 0) {
            $cleanupFailed = $true
        }
    }
    catch {
        $cleanupFailed = $true
    }

    foreach ($processName in $relevantProcessNames) {
        if (@(Get-Process -Name $processName -ErrorAction SilentlyContinue).Count -ne 0) {
            $cleanupFailed = $true
        }
    }

    if ($acceptanceRootCreated) {
        try {
            if (@(Get-ChildItem -LiteralPath $acceptanceRoot -Force -ErrorAction Stop).Count -eq 0) {
                Remove-Item -LiteralPath $acceptanceRoot -Force -ErrorAction Stop
            }
            else {
                $cleanupFailed = $true
            }
        }
        catch {
            $cleanupFailed = $true
        }
    }

    if ($cleanupFailed) {
        throw 'Gate 9 cleanup could not prove a clean target state.'
    }

    $cleanupVerified = $true
}

if (-not $cleanupVerified -or $repetitions.Count -ne 3) {
    throw 'Gate 9 did not complete its exact engineering campaign.'
}

$signatureKind = [string] $installedPackage.SignatureKind
$publicTrustVerified = $false
$smartAppControlVerified = $false
if ($PSBoundParameters.ContainsKey('ReleaseTrustRecord')) {
    $releaseTrustValidator = Join-Path $PSScriptRoot (
        'Test-HardwareInspectionGate9ReleaseTrust.ps1')
    if (-not [IO.File]::Exists($releaseTrustValidator)) {
        throw 'The Gate 9 release-trust validator is unavailable.'
    }

    $trustRecordPath = [IO.Path]::GetFullPath($ReleaseTrustRecord)
    Assert-Gate9NoReparseAncestors -Path $trustRecordPath
    $trustStream = [IO.File]::Open(
        $trustRecordPath,
        [IO.FileMode]::Open,
        [IO.FileAccess]::Read,
        [IO.FileShare]::Read)
    try {
        & $releaseTrustValidator `
            -Path $trustRecordPath `
            -ExpectedPackageSha256 $manifest.PackageSha256 `
            -ExpectedPublisher $expectedPublisher `
            -ExpectedCommit $ExpectedCommit
        if ($LASTEXITCODE -ne 0) {
            throw 'The Gate 9 release-trust record is invalid.'
        }

        if ($trustStream.Length -lt 2 -or $trustStream.Length -gt 8KB) {
            throw 'The Gate 9 release-trust record length is invalid.'
        }

        [byte[]] $trustBytes = New-Object byte[] ([int] $trustStream.Length)
        $trustOffset = 0
        while ($trustOffset -lt $trustBytes.Length) {
            $trustRead = $trustStream.Read(
                $trustBytes,
                $trustOffset,
                $trustBytes.Length - $trustOffset)
            if ($trustRead -eq 0) {
                throw 'The Gate 9 release-trust record ended during its locked read.'
            }

            $trustOffset += $trustRead
        }

        [byte[]] $trustJsonBytes = New-Object byte[] ($trustBytes.Length - 1)
        [Array]::Copy(
            $trustBytes,
            0,
            $trustJsonBytes,
            0,
            $trustJsonBytes.Length)
        $trustJson = [Text.UTF8Encoding]::new($false, $true).GetString(
            $trustJsonBytes)
        $trust = $trustJson | ConvertFrom-Json -ErrorAction Stop
    }
    finally {
        $trustStream.Dispose()
    }

    $signatureKind = [string] $trust.signatureKind
    $publicTrustVerified = [bool] $trust.publicChainVerified
    $smartAppControlVerified = [bool] $trust.smartAppControlVerified
}

$summaryValidator = Join-Path $PSScriptRoot 'Test-HardwareInspectionGate9Summary.ps1'
Write-Gate9EngineeringSummary `
    -DestinationPath $summaryPath `
    -ExpectedCommit $ExpectedCommit `
    -Repetitions $repetitions.ToArray() `
    -SignatureKind $signatureKind `
    -PublicTrustVerified $publicTrustVerified `
    -SmartAppControlVerified $smartAppControlVerified `
    -SummaryValidatorPath $summaryValidator

[pscustomobject]@{
    Classification = 'local-sanitized'
    Repetitions = 3
    Offline = $true
    NoRelevantNetworkEndpointObserved = $true
    CleanupVerified = $true
    Disposition = if ($publicTrustVerified -and $smartAppControlVerified) {
        'Passed'
    }
    else {
        'EngineeringPassedReleaseBlocked'
    }
}
