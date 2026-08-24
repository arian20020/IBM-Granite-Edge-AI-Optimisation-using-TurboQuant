[CmdletBinding(DefaultParameterSetName = 'Development')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Development')]
    [ValidatePattern('^[0-9A-Fa-f]{40}$')]
    [string] $CertificateThumbprint,

    [Parameter(Mandatory, ParameterSetName = 'Trusted')]
    [ValidateNotNullOrEmpty()]
    [string] $TrustedSignerPath,

    [Parameter(Mandatory, ParameterSetName = 'Trusted')]
    [ValidatePattern('^[0-9A-Fa-f]{64}$')]
    [string] $ExpectedTrustedSignerSha256,

    [Parameter(Mandatory, ParameterSetName = 'Trusted')]
    [ValidateCount(1, 128)]
    [string[]] $TrustedSignerArgumentTemplate,

    [Parameter(Mandatory, ParameterSetName = 'Trusted')]
    [ValidateNotNullOrEmpty()]
    [string] $TrustedBundleDirectory,

    [ValidateRange(1, 10)]
    [int] $Repetitions = 1,

    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [Parameter(ParameterSetName = 'Development')]
    [ValidateNotNullOrEmpty()]
    [string] $DevelopmentBundleDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$packageIdentityName = 'GraniteEdgeAI.WinUI.UnitTests'
$packagePublisher = 'CN=GraniteEdgeAI'
$packageVersion = [Version]'1.0.0.0'
$resultSchema = 'granite.hardware-inspection.process-acceptance/v1'
$maximumResultBytes = 64KB
$maximumWait = [TimeSpan]::FromSeconds(180)
$trustedSigning = $PSCmdlet.ParameterSetName -ceq 'Trusted'
$normalizedThumbprint = if ($trustedSigning) {
    $null
}
else {
    $CertificateThumbprint.ToUpperInvariant()
}
$currentUserSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$windowsPowerShellPath = [IO.Path]::Combine(
    [Environment]::SystemDirectory,
    'WindowsPowerShell\v1.0\powershell.exe')
$windowsPowerShellItem = Get-Item -LiteralPath $windowsPowerShellPath -ErrorAction Stop
$windowsPowerShellSignature = Get-AuthenticodeSignature -LiteralPath $windowsPowerShellPath
if (($windowsPowerShellItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
    $windowsPowerShellSignature.Status -ne [Management.Automation.SignatureStatus]::Valid) {
    throw 'The fixed Windows PowerShell host does not satisfy the signed launcher contract.'
}
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$testOutputRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot (
    'tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\{0}\net8.0-windows10.0.19041.0\win-x64' -f $Configuration)))
$appPackagesRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot `
    'tests\UnitTests\GraniteEdgeAI.UnitTests\AppPackages'))
$ownedTempParent = [IO.Path]::GetFullPath((Join-Path $env:TEMP 'GEAI-HI-Signed'))
$bundlePublicationRoot = $null
$requestedBundleDirectory = if ($trustedSigning) {
    $TrustedBundleDirectory
}
elseif ($PSBoundParameters.ContainsKey('DevelopmentBundleDirectory')) {
    $DevelopmentBundleDirectory
}
else {
    $null
}
if ($null -ne $requestedBundleDirectory) {
    if ($requestedBundleDirectory -notmatch '^[A-Za-z]:[\\/]') {
        throw 'The bundle publication directory must be an absolute path.'
    }

    $bundlePublicationRoot = [IO.Path]::GetFullPath($requestedBundleDirectory)
    if ([string]::Equals(
            $bundlePublicationRoot,
            [IO.Path]::GetPathRoot($bundlePublicationRoot),
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The bundle publication directory must not be a filesystem root.'
    }
    $guardedRoots = @($repositoryRoot, $testOutputRoot, $appPackagesRoot, $ownedTempParent)
    foreach ($guardedRoot in $guardedRoots) {
        $guardedPrefix = $guardedRoot.TrimEnd(
            [IO.Path]::DirectorySeparatorChar,
            [IO.Path]::AltDirectorySeparatorChar)
        if ([string]::Equals(
                $bundlePublicationRoot,
                $guardedPrefix,
                [StringComparison]::OrdinalIgnoreCase) -or
            $bundlePublicationRoot.StartsWith(
                $guardedPrefix + [IO.Path]::DirectorySeparatorChar,
                [StringComparison]::OrdinalIgnoreCase)) {
            throw 'The bundle publication directory must be outside repository and build roots.'
        }
    }

    if (-not (Test-Path -LiteralPath $bundlePublicationRoot -PathType Container)) {
        throw 'The bundle publication directory must already exist and be empty.'
    }
    $bundlePublicationItem = Get-Item -LiteralPath $bundlePublicationRoot -Force -ErrorAction Stop
    if (($bundlePublicationItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'The bundle publication directory must not be a reparse point.'
    }
    if (@(Get-ChildItem -LiteralPath $bundlePublicationRoot -Force -ErrorAction Stop).Count -ne 0) {
        throw 'The bundle publication directory must already exist and be empty.'
    }
}
$ownedTempRoot = Join-Path $ownedTempParent ([Guid]::NewGuid().ToString('N').Substring(0, 8))
$stagingRoot = Join-Path $ownedTempRoot 'AppX'
$signedPackagePath = Join-Path $ownedTempRoot 'GraniteEdgeAI.UnitTests.msix'
$resultRoot = [IO.Path]::GetFullPath((Join-Path $env:TEMP 'GraniteEdgeAI.HardwareInspection.Tests\Acceptance'))
$createdResultPaths = [Collections.Generic.List[string]]::new()
$installedPackage = $null
$packageInstalledByInvocation = $false
$bundlePublished = $false
$trustedSignerThumbprint = $null
$trustModulePath = Join-Path $PSScriptRoot 'HardwareInspectionTrust.psm1'
if ($trustedSigning) {
    if (-not [IO.File]::Exists($trustModulePath)) {
        throw 'The Hardware Inspection trust module is unavailable.'
    }
    Import-Module $trustModulePath -Force -ErrorAction Stop
}

function Get-NonRootPathWithoutTrailingSeparator {
    param([Parameter(Mandatory)][string] $Path)

    $fullPath = [IO.Path]::GetFullPath($Path)
    if ([string]::Equals(
            $fullPath,
            [IO.Path]::GetPathRoot($fullPath),
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'A guarded path must not be a filesystem root.'
    }

    return $fullPath.TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
}

function Assert-OwnedPath {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [string] $OwnedParent
    )

    $resolvedPath = [IO.Path]::GetFullPath($Path)
    $resolvedParent = Get-NonRootPathWithoutTrailingSeparator -Path $OwnedParent
    if (-not $resolvedPath.StartsWith(
            $resolvedParent + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'An owned temporary path escaped its fixed parent.'
    }
}

function Get-SolutionOwnedExecutablePayload {
    param([Parameter(Mandatory)][string] $LayoutRoot)

    Get-ChildItem -LiteralPath $LayoutRoot -File -Recurse -ErrorAction Stop | Where-Object {
        ($_.Extension -in @('.dll', '.exe')) -and
        ($_.Name.StartsWith('GraniteEdgeAI.', [StringComparison]::Ordinal) -or
            [string]::Equals(
                $_.Name,
                'IBM Granite with TurboQuant (Intel).dll',
                [StringComparison]::Ordinal) -or
            [string]::Equals(
                $_.Name,
                'IBM Granite with TurboQuant (Intel).exe',
                [StringComparison]::Ordinal))
    }
}

function Resolve-WindowsSdkTool {
    param([Parameter(Mandatory)][string] $Name)

    $windowsKitsRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    $tool = Get-ChildItem -LiteralPath $windowsKitsRoot -Filter $Name -File -Recurse |
        Where-Object { $_.DirectoryName.EndsWith('\x64', [StringComparison]::OrdinalIgnoreCase) } |
        Sort-Object { [version]$_.Directory.Parent.Name } -Descending |
        Select-Object -First 1
    if ($null -eq $tool) {
        throw "The x64 Windows SDK tool '$Name' was not found."
    }

    return $tool.FullName
}

function Invoke-AcceptanceSigning {
    param([Parameter(Mandatory)] [string] $Path)

    if ($trustedSigning) {
        $parameters = @{
            FilePath = $Path
            ProviderPath = $TrustedSignerPath
            ExpectedProviderSha256 = $ExpectedTrustedSignerSha256
            ArgumentTemplate = $TrustedSignerArgumentTemplate
            ExpectedPublisher = $packagePublisher
        }
        if (-not [string]::IsNullOrWhiteSpace($script:trustedSignerThumbprint)) {
            $parameters.ExpectedSignerThumbprint = $script:trustedSignerThumbprint
        }

        $evidence = Invoke-HardwareInspectionTrustedSigner @parameters
        if ([string]::IsNullOrWhiteSpace($script:trustedSignerThumbprint)) {
            $script:trustedSignerThumbprint = [string] $evidence.Thumbprint
        }

        return $evidence
    }

    & $signTool sign /fd SHA256 /sha1 $normalizedThumbprint $Path | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed with exit code $LASTEXITCODE."
    }

    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne [Management.Automation.SignatureStatus]::Valid -or
        $null -eq $signature.SignerCertificate -or
        -not [string]::Equals(
            $signature.SignerCertificate.Thumbprint,
            $normalizedThumbprint,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'A development signing result did not pass exact Authenticode verification.'
    }

    [pscustomobject]@{
        Path = [IO.Path]::GetFullPath($Path)
        Sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
        Publisher = $signature.SignerCertificate.Subject
        Thumbprint = $signature.SignerCertificate.Thumbprint
        TimestampThumbprint = if ($null -eq $signature.TimeStamperCertificate) {
            $null
        }
        else {
            $signature.TimeStamperCertificate.Thumbprint
        }
        ChainTrustVerified = $false
    }
}

function Read-ClosedPackageManifest {
    param([Parameter(Mandatory)][string] $Path)

    $settings = [Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [Xml.XmlReader]::Create([IO.Path]::GetFullPath($Path), $settings)
    try {
        $document = [Xml.XmlDocument]::new()
        $document.XmlResolver = $null
        $document.Load($reader)
    }
    finally {
        $reader.Dispose()
    }

    $namespaces = [Xml.XmlNamespaceManager]::new($document.NameTable)
    $namespaces.AddNamespace('f', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
    $identities = @($document.SelectNodes('/f:Package/f:Identity', $namespaces))
    $applications = @($document.SelectNodes('/f:Package/f:Applications/f:Application', $namespaces))
    $manifestVersion = $null
    if ($identities.Count -ne 1 -or
        $applications.Count -ne 1 -or
        -not [Version]::TryParse($identities[0].GetAttribute('Version'), [ref]$manifestVersion) -or
        -not [string]::Equals($identities[0].GetAttribute('Name'), $packageIdentityName, [StringComparison]::Ordinal) -or
        -not [string]::Equals($identities[0].GetAttribute('Publisher'), $packagePublisher, [StringComparison]::Ordinal) -or
        $manifestVersion -ne $packageVersion -or
        -not [string]::Equals($identities[0].GetAttribute('ProcessorArchitecture'), 'x64', [StringComparison]::Ordinal) -or
        -not [string]::Equals($applications[0].GetAttribute('Id'), 'App', [StringComparison]::Ordinal) -or
        -not [string]::Equals($applications[0].GetAttribute('Executable'), 'GraniteEdgeAI.UnitTests.exe', [StringComparison]::Ordinal) -or
        -not [string]::Equals($applications[0].GetAttribute('EntryPoint'), 'Windows.FullTrustApplication', [StringComparison]::Ordinal)) {
        throw 'The staged test package manifest does not match the closed signed acceptance identity.'
    }

    [pscustomobject]@{
        Version = $manifestVersion
        ApplicationId = $applications[0].GetAttribute('Id')
    }
}

function Assert-ExactDirectoryMirror {
    param(
        [Parameter(Mandatory)][string] $ExpectedRoot,
        [Parameter(Mandatory)][string] $ActualRoot
    )

    $expectedPrefix = (Get-NonRootPathWithoutTrailingSeparator -Path $ExpectedRoot) +
        [IO.Path]::DirectorySeparatorChar
    $actualPrefix = (Get-NonRootPathWithoutTrailingSeparator -Path $ActualRoot) +
        [IO.Path]::DirectorySeparatorChar
    $expectedFiles = @(Get-ChildItem -LiteralPath $ExpectedRoot -File -Recurse -ErrorAction Stop)
    $actualFiles = @(Get-ChildItem -LiteralPath $ActualRoot -File -Recurse -ErrorAction Stop)
    if ($expectedFiles.Count -eq 0 -or
        $expectedFiles.Count -gt 256 -or
        $actualFiles.Count -ne $expectedFiles.Count) {
        throw 'The staged Hardware Inspection payload does not match the bounded build output.'
    }

    foreach ($expectedFile in $expectedFiles) {
        $relativePath = $expectedFile.FullName.Substring($expectedPrefix.Length)
        $actualPath = Join-Path $actualPrefix $relativePath
        if (-not (Test-Path -LiteralPath $actualPath -PathType Leaf) -or
            -not [string]::Equals(
                (Get-FileHash -Algorithm SHA256 -LiteralPath $expectedFile.FullName).Hash,
                (Get-FileHash -Algorithm SHA256 -LiteralPath $actualPath).Hash,
                [StringComparison]::OrdinalIgnoreCase)) {
            throw 'The staged Hardware Inspection payload differs from the exact build output.'
        }
    }
}

function Remove-TestPackage {
    $packages = @(Get-AppxPackage -Name $packageIdentityName)
    foreach ($package in $packages) {
        if (-not [string]::Equals($package.Name, $packageIdentityName, [StringComparison]::Ordinal)) {
            throw 'Package cleanup resolved an unexpected identity.'
        }

        Remove-AppxPackage -Package $package.PackageFullName -User $currentUserSid
    }
}

function Read-AcceptanceResult {
    param([Parameter(Mandatory)][string] $Path)

    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -eq 0 -or $bytes.Length -gt $maximumResultBytes -or $bytes[-1] -ne 0x0a) {
        throw 'The signed acceptance result violates its byte bound or LF framing.'
    }

    if (@($bytes | Where-Object { $_ -eq 0x0a }).Count -ne 1 -or
        @($bytes | Where-Object { $_ -eq 0x0d }).Count -ne 0 -or
        ($bytes.Length -ge 3 -and $bytes[0] -eq 0xef -and $bytes[1] -eq 0xbb -and $bytes[2] -eq 0xbf)) {
        throw 'The signed acceptance result has forbidden framing.'
    }

    $strictUtf8 = [Text.UTF8Encoding]::new($false, $true)
    $text = $strictUtf8.GetString($bytes)
    $pattern = '^\{"schema":"granite\.hardware-inspection\.process-acceptance/v1","packageIdentityPresent":true,"total":(?<total>[1-9][0-9]{0,2}),"passed":(?<passed>[0-9]{1,3}),"failed":\[(?<failed>(?:"[A-Za-z0-9_.]{1,512}"(?:,"[A-Za-z0-9_.]{1,512}")*)?)\]\}\n$'
    $match = [Text.RegularExpressions.Regex]::Match(
        $text,
        $pattern,
        [Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if (-not $match.Success) {
        throw 'The signed acceptance result does not match the closed JSON-v1 grammar.'
    }

    $total = [int]$match.Groups['total'].Value
    $passed = [int]$match.Groups['passed'].Value
    $failedNames = @([Text.RegularExpressions.Regex]::Matches(
        $match.Groups['failed'].Value,
        '"(?<name>[A-Za-z0-9_.]{1,512})"') | ForEach-Object { $_.Groups['name'].Value })
    if ($total -gt 128 -or $passed -gt $total -or $total -ne $passed + $failedNames.Count) {
        throw 'The signed acceptance result counts are inconsistent.'
    }

    [pscustomobject]@{
        Schema = $resultSchema
        PackageIdentityPresent = $true
        Total = $total
        Passed = $passed
        Failed = $failedNames
    }
}

function Assert-DevelopmentAcceptanceBundlePublication {
    param(
        [Parameter(Mandatory)][string] $DestinationRoot,
        [Parameter(Mandatory)]
        [ValidatePattern('^[0-9A-Fa-f]{40}$')]
        [string] $CertificateThumbprint
    )

    $resolvedDestination = [IO.Path]::GetFullPath($DestinationRoot)
    if ([string]::Equals(
            $resolvedDestination,
            [IO.Path]::GetPathRoot($resolvedDestination),
            [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $resolvedDestination -PathType Container)) {
        throw 'The published development bundle boundary is invalid.'
    }
    $destinationItem = Get-Item -LiteralPath $resolvedDestination -Force -ErrorAction Stop
    if (($destinationItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'The published development bundle boundary is invalid.'
    }

    $expectedInventory = [string[]]@(
        'GraniteEdgeAI.UnitTests.msix',
        'GraniteEdgeAI.cer',
        'Invoke-HardwareInspectionDevelopmentAcceptanceGuest.ps1',
        'bundle-manifest.json'
    )
    [Array]::Sort($expectedInventory, [StringComparer]::Ordinal)
    $actualItems = @(Get-ChildItem -LiteralPath $resolvedDestination -Force -ErrorAction Stop)
    $actualInventory = [string[]]@($actualItems | ForEach-Object { $_.Name })
    [Array]::Sort($actualInventory, [StringComparer]::Ordinal)
    if ($actualInventory.Count -ne $expectedInventory.Count -or
        @($actualItems | Where-Object {
                $_.PSIsContainer -or
                ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0
            }).Count -ne 0) {
        throw 'The published development bundle inventory is not exact.'
    }
    for ($index = 0; $index -lt $expectedInventory.Count; $index++) {
        if (-not [string]::Equals(
                $actualInventory[$index],
                $expectedInventory[$index],
                [StringComparison]::Ordinal)) {
            throw 'The published development bundle inventory is not exact.'
        }
    }

    $manifestPath = Join-Path $resolvedDestination 'bundle-manifest.json'
    $manifestBytes = [IO.File]::ReadAllBytes($manifestPath)
    if ($manifestBytes.Length -eq 0 -or
        $manifestBytes.Length -gt 16KB -or
        $manifestBytes[-1] -ne 0x0a -or
        @($manifestBytes | Where-Object { $_ -eq 0x0a }).Count -ne 1 -or
        @($manifestBytes | Where-Object { $_ -eq 0x0d }).Count -ne 0 -or
        ($manifestBytes.Length -ge 3 -and
            $manifestBytes[0] -eq 0xef -and
            $manifestBytes[1] -eq 0xbb -and
            $manifestBytes[2] -eq 0xbf)) {
        throw 'The published development bundle manifest has forbidden framing.'
    }
    try {
        $manifestText = [Text.UTF8Encoding]::new($false, $true).GetString($manifestBytes)
    }
    catch {
        throw 'The published development bundle manifest is not strict UTF-8.'
    }

    $normalizedThumbprint = $CertificateThumbprint.ToUpperInvariant()
    $manifestPattern = '^\{"schema":"granite\.hardware-inspection\.development-acceptance-bundle/v1",' +
        '"packageName":"GraniteEdgeAI\.WinUI\.UnitTests",' +
        '"publisher":"CN=GraniteEdgeAI",' +
        '"version":"1\.0\.0\.0",' +
        '"architecture":"x64",' +
        '"certificateThumbprint":"' +
        [Text.RegularExpressions.Regex]::Escape($normalizedThumbprint) + '",' +
        '"files":\[' +
        '\{"name":"GraniteEdgeAI\.UnitTests\.msix","length":(?<msixLength>[1-9][0-9]{0,10}),"sha256":"(?<msixHash>[0-9a-f]{64})"\},' +
        '\{"name":"GraniteEdgeAI\.cer","length":(?<cerLength>[1-9][0-9]{0,10}),"sha256":"(?<cerHash>[0-9a-f]{64})"\},' +
        '\{"name":"Invoke-HardwareInspectionDevelopmentAcceptanceGuest\.ps1","length":(?<scriptLength>[1-9][0-9]{0,10}),"sha256":"(?<scriptHash>[0-9a-f]{64})"\}' +
        '\]\}\n$'
    $manifestMatch = [Text.RegularExpressions.Regex]::Match(
        $manifestText,
        $manifestPattern,
        [Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if (-not $manifestMatch.Success) {
        throw 'The published development bundle manifest is not canonical.'
    }

    $payloadContracts = @(
        [pscustomobject]@{
            Name = 'GraniteEdgeAI.UnitTests.msix'
            Length = [int64]$manifestMatch.Groups['msixLength'].Value
            Hash = $manifestMatch.Groups['msixHash'].Value
        },
        [pscustomobject]@{
            Name = 'GraniteEdgeAI.cer'
            Length = [int64]$manifestMatch.Groups['cerLength'].Value
            Hash = $manifestMatch.Groups['cerHash'].Value
        },
        [pscustomobject]@{
            Name = 'Invoke-HardwareInspectionDevelopmentAcceptanceGuest.ps1'
            Length = [int64]$manifestMatch.Groups['scriptLength'].Value
            Hash = $manifestMatch.Groups['scriptHash'].Value
        }
    )
    foreach ($payloadContract in $payloadContracts) {
        $payloadPath = Join-Path $resolvedDestination $payloadContract.Name
        $payloadItem = Get-Item -LiteralPath $payloadPath -Force -ErrorAction Stop
        $payloadHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $payloadPath).Hash
        if ($payloadItem.Length -ne $payloadContract.Length -or
            -not [string]::Equals(
                $payloadHash,
                $payloadContract.Hash,
                [StringComparison]::OrdinalIgnoreCase)) {
            throw 'A published development bundle payload differs from its manifest.'
        }
    }
}

function Write-DevelopmentAcceptanceBundle {
    param(
        [Parameter(Mandatory)][string] $SignedPackagePath,
        [Parameter(Mandatory)][string] $PublicCertificatePath,
        [Parameter(Mandatory)][string] $GuestRunnerPath,
        [Parameter(Mandatory)][string] $DestinationRoot,
        [Parameter(Mandatory)]
        [ValidatePattern('^[0-9A-Fa-f]{40}$')]
        [string] $CertificateThumbprint
    )

    $resolvedDestination = [IO.Path]::GetFullPath($DestinationRoot)
    if ([string]::Equals(
            $resolvedDestination,
            [IO.Path]::GetPathRoot($resolvedDestination),
            [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $resolvedDestination -PathType Container) -or
        @(Get-ChildItem -LiteralPath $resolvedDestination -Force -ErrorAction Stop).Count -ne 0) {
        throw 'The development bundle destination must be an existing empty directory.'
    }
    $destinationItem = Get-Item -LiteralPath $resolvedDestination -Force -ErrorAction Stop
    if (($destinationItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'The development bundle destination must not be a reparse point.'
    }

    $sourceContracts = @(
        [pscustomobject]@{
            Source = [IO.Path]::GetFullPath($SignedPackagePath)
            Name = 'GraniteEdgeAI.UnitTests.msix'
        },
        [pscustomobject]@{
            Source = [IO.Path]::GetFullPath($PublicCertificatePath)
            Name = 'GraniteEdgeAI.cer'
        },
        [pscustomobject]@{
            Source = [IO.Path]::GetFullPath($GuestRunnerPath)
            Name = 'Invoke-HardwareInspectionDevelopmentAcceptanceGuest.ps1'
        }
    )
    foreach ($sourceContract in $sourceContracts) {
        $destinationItem = Get-Item -LiteralPath $resolvedDestination -Force -ErrorAction Stop
        if (($destinationItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'The development bundle destination changed during publication.'
        }
        if (-not (Test-Path -LiteralPath $sourceContract.Source -PathType Leaf)) {
            throw 'A required development bundle source file is absent.'
        }
        $destinationPath = Join-Path $resolvedDestination $sourceContract.Name
        if (Test-Path -LiteralPath $destinationPath) {
            throw 'The development bundle destination changed during publication.'
        }
        Copy-Item -LiteralPath $sourceContract.Source -Destination $destinationPath
        $publishedItem = Get-Item -LiteralPath $destinationPath -Force -ErrorAction Stop
        if ($publishedItem.PSIsContainer -or
            ($publishedItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'The development bundle destination changed during publication.'
        }
    }

    $payloads = @($sourceContracts | ForEach-Object {
            $path = Join-Path $resolvedDestination $_.Name
            $item = Get-Item -LiteralPath $path -Force -ErrorAction Stop
            [pscustomobject]@{
                Name = $_.Name
                Length = [int64]$item.Length
                Hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant()
            }
        })
    $normalizedBundleThumbprint = $CertificateThumbprint.ToUpperInvariant()
    $manifestText = '{"schema":"granite.hardware-inspection.development-acceptance-bundle/v1",' +
        '"packageName":"GraniteEdgeAI.WinUI.UnitTests",' +
        '"publisher":"CN=GraniteEdgeAI",' +
        '"version":"1.0.0.0",' +
        '"architecture":"x64",' +
        '"certificateThumbprint":"' + $normalizedBundleThumbprint + '",' +
        '"files":[' +
        (($payloads | ForEach-Object {
                    '{"name":"' + $_.Name + '","length":' +
                        $_.Length.ToString([Globalization.CultureInfo]::InvariantCulture) +
                        ',"sha256":"' + $_.Hash + '"}'
                }) -join ',') + ']}'
    $destinationItem = Get-Item -LiteralPath $resolvedDestination -Force -ErrorAction Stop
    if (($destinationItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
        @(Get-ChildItem -LiteralPath $resolvedDestination -Force -ErrorAction Stop).Count -ne 3) {
        throw 'The development bundle destination changed during publication.'
    }
    $manifestPath = Join-Path $resolvedDestination 'bundle-manifest.json'
    if (Test-Path -LiteralPath $manifestPath) {
        throw 'The development bundle destination changed during publication.'
    }
    [IO.File]::WriteAllText(
        $manifestPath,
        $manifestText + "`n",
        [Text.UTF8Encoding]::new($false))

    $finalInventory = [string[]]@(Get-ChildItem -LiteralPath $resolvedDestination -Force |
        ForEach-Object { $_.Name })
    [Array]::Sort($finalInventory, [StringComparer]::Ordinal)
    $expectedInventory = [string[]]@(
        'GraniteEdgeAI.UnitTests.msix',
        'GraniteEdgeAI.cer',
        'Invoke-HardwareInspectionDevelopmentAcceptanceGuest.ps1',
        'bundle-manifest.json'
    )
    [Array]::Sort($expectedInventory, [StringComparer]::Ordinal)
    if ($finalInventory.Count -ne $expectedInventory.Count) {
        throw 'The development bundle publication inventory is not exact.'
    }
    for ($index = 0; $index -lt $expectedInventory.Count; $index++) {
        if (-not [string]::Equals(
                $finalInventory[$index],
                $expectedInventory[$index],
                [StringComparison]::Ordinal)) {
            throw 'The development bundle publication inventory is not exact.'
        }
    }

    Assert-DevelopmentAcceptanceBundlePublication `
        -DestinationRoot $resolvedDestination `
        -CertificateThumbprint $normalizedBundleThumbprint
}

Assert-OwnedPath -Path $ownedTempRoot -OwnedParent $ownedTempParent
$configurationToken = if ($Configuration -eq 'Debug') { '_Debug' } else { '' }
$directoryPattern = '^GraniteEdgeAI\.UnitTests_[0-9]+(?:\.[0-9]+){3}_x64' +
    [Text.RegularExpressions.Regex]::Escape($configurationToken) + '_Test$'
$filePattern = '^GraniteEdgeAI\.UnitTests_[0-9]+(?:\.[0-9]+){3}_x64' +
    [Text.RegularExpressions.Regex]::Escape($configurationToken) + '\.msix$'
$builtPackages = @(Get-ChildItem -LiteralPath $appPackagesRoot -Filter '*.msix' -File -Recurse -ErrorAction Stop |
    Where-Object {
        $_.Directory.Name -match $directoryPattern -and
        $_.Name -match $filePattern
    })
if ($builtPackages.Count -ne 1) {
    throw 'Expected exactly one generated x64 test MSIX for signed acceptance.'
}

$builtPackagePath = $builtPackages[0].FullName
$testAssemblyPath = Join-Path $testOutputRoot 'GraniteEdgeAI.UnitTests.dll'
$hardwareInspectionOutputRoot = Join-Path $testOutputRoot 'HardwareInspection'
if (-not (Test-Path -LiteralPath $testAssemblyPath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $hardwareInspectionOutputRoot -PathType Container)) {
    throw 'The generated test package inputs are absent.'
}
$latestPackageInput = @(
    Get-Item -LiteralPath $testAssemblyPath
    Get-ChildItem -LiteralPath $hardwareInspectionOutputRoot -File -Recurse -ErrorAction Stop
) | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
if ($builtPackages[0].LastWriteTimeUtc -lt $latestPackageInput.LastWriteTimeUtc) {
    throw 'The generated test MSIX is older than a packaged Hardware Inspection input.'
}

$repositoryItem = Get-Item -LiteralPath $repositoryRoot
if (($repositoryItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw 'Signed acceptance requires a real physical worktree, not a reparse-point root.'
}

$userCertificate = $null
if (-not $trustedSigning) {
    $userCertificate = Get-Item `
        -LiteralPath "Cert:\CurrentUser\My\$normalizedThumbprint" `
        -ErrorAction Stop
    if (-not $userCertificate.HasPrivateKey -or
        -not [string]::Equals($userCertificate.Subject, $packagePublisher, [StringComparison]::Ordinal) -or
        $userCertificate.NotBefore -gt [DateTime]::Now -or
        $userCertificate.NotAfter -le [DateTime]::Now -or
        -not ($userCertificate.EnhancedKeyUsageList.ObjectId -contains '1.3.6.1.5.5.7.3.3')) {
        throw 'The purpose-specific package-signing certificate does not satisfy the closed trust contract.'
    }
    if ($null -eq $bundlePublicationRoot) {
        $trustedCertificate = Get-Item `
            -LiteralPath "Cert:\LocalMachine\TrustedPeople\$normalizedThumbprint" `
            -ErrorAction Stop
        if ($trustedCertificate.HasPrivateKey -or
            -not [string]::Equals(
                $trustedCertificate.Subject,
                $packagePublisher,
                [StringComparison]::Ordinal)) {
            throw 'The purpose-specific package-signing certificate does not satisfy the closed trust contract.'
        }
    }
}

$makeAppx = Resolve-WindowsSdkTool -Name 'makeappx.exe'
$signTool = Resolve-WindowsSdkTool -Name 'signtool.exe'

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

namespace GraniteEdgeAI.HardwareInspection.AcceptanceLauncher
{
    [ComImport]
    [Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
    internal class ApplicationActivationManager
    {
    }

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

try {
    New-Item -ItemType Directory -Path $ownedTempRoot | Out-Null
    & $makeAppx unpack /p $builtPackagePath /d $stagingRoot /o | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'The generated x64 test MSIX could not be unpacked for signing.'
    }

    foreach ($requiredPath in @(
            'AppxManifest.xml',
            'GraniteEdgeAI.UnitTests.dll',
            'HardwareInspection\LlamaCppProbe\GraniteEdgeAI.HardwareInspection.LlamaCppProbe.exe',
            'HardwareInspection\llamacpp-probe-manifest.json',
            'HardwareInspection\TestTools\LlamaCppProbeFake\success\fake-mode.txt')) {
        if (-not (Test-Path -LiteralPath (Join-Path $stagingRoot $requiredPath) -PathType Leaf)) {
            throw 'The generated x64 test MSIX lacks a required Hardware Inspection payload.'
        }
    }
    $stagedManifest = Read-ClosedPackageManifest -Path (Join-Path $stagingRoot 'AppxManifest.xml')
    if (-not [string]::Equals(
            (Get-FileHash -Algorithm SHA256 -LiteralPath $testAssemblyPath).Hash,
            (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $stagingRoot 'GraniteEdgeAI.UnitTests.dll')).Hash,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The staged test assembly differs from the exact build output.'
    }
    Assert-ExactDirectoryMirror `
        -ExpectedRoot $hardwareInspectionOutputRoot `
        -ActualRoot (Join-Path $stagingRoot 'HardwareInspection')
    foreach ($developmentFile in @('vs.appxrecipe', 'AppxBlockMap.xml', 'AppxSignature.p7x', '[Content_Types].xml')) {
        $candidate = Join-Path $stagingRoot $developmentFile
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            Remove-Item -LiteralPath $candidate -Force
        }
    }

    $reparsePoint = Get-ChildItem -LiteralPath $stagingRoot -Force -Recurse |
        Where-Object { ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 } |
        Select-Object -First 1
    if ($null -ne $reparsePoint) {
        throw 'The staged package must not contain a reparse point.'
    }
    $stagedFileInventory = [string[]]@(Get-ChildItem -LiteralPath $stagingRoot -File -Recurse |
        ForEach-Object { $_.FullName.Substring($stagingRoot.Length + 1) })
    [Array]::Sort($stagedFileInventory, [StringComparer]::Ordinal)

    $ownedExecutablePayload = @(Get-SolutionOwnedExecutablePayload -LayoutRoot $stagingRoot)
    if ($ownedExecutablePayload.Count -eq 0 -or $ownedExecutablePayload.Count -gt 128) {
        throw 'The solution-owned executable payload count is outside its closed bound.'
    }

    foreach ($payloadFile in $ownedExecutablePayload) {
        Assert-OwnedPath -Path $payloadFile.FullName -OwnedParent $stagingRoot
        $null = Invoke-AcceptanceSigning -Path $payloadFile.FullName
    }

    $stagedProbeDirectory = Join-Path $stagingRoot 'HardwareInspection\LlamaCppProbe'
    $stagedProbeManifestPath = Join-Path $stagingRoot 'HardwareInspection\llamacpp-probe-manifest.json'
    & $windowsPowerShellPath -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
        -File (Join-Path $repositoryRoot 'scripts\hardware-inspection\New-LlamaCppProbeManifest.ps1') `
        -ProbeDirectory $stagedProbeDirectory `
        -ManifestPath $stagedProbeManifestPath
    if ($LASTEXITCODE -ne 0) {
        throw 'The detached probe manifest could not be regenerated after payload signing.'
    }
    & $windowsPowerShellPath -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
        -File (Join-Path $repositoryRoot 'scripts\hardware-inspection\Test-LlamaCppProbeManifest.ps1') `
        -ProbeDirectory $stagedProbeDirectory `
        -ManifestPath $stagedProbeManifestPath
    if ($LASTEXITCODE -ne 0) {
        throw 'The signed probe payload does not match its regenerated detached manifest.'
    }
    $regeneratedManifest = Get-Content -LiteralPath $stagedProbeManifestPath -Raw | ConvertFrom-Json
    $signedProbeHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (
        Join-Path $stagedProbeDirectory 'GraniteEdgeAI.HardwareInspection.LlamaCppProbe.exe')).Hash
    if (-not [string]::Equals(
            [string]$regeneratedManifest.executableSha256,
            $signedProbeHash,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The regenerated detached manifest does not bind the exact signed probe executable.'
    }

    $finalReparsePoint = Get-ChildItem -LiteralPath $stagingRoot -Force -Recurse |
        Where-Object { ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 } |
        Select-Object -First 1
    $finalStagedFileInventory = [string[]]@(Get-ChildItem -LiteralPath $stagingRoot -File -Recurse |
        ForEach-Object { $_.FullName.Substring($stagingRoot.Length + 1) })
    [Array]::Sort($finalStagedFileInventory, [StringComparer]::Ordinal)
    $finalOwnedExecutablePayload = @(Get-SolutionOwnedExecutablePayload -LayoutRoot $stagingRoot)
    $inventoryMatches = $stagedFileInventory.Count -eq $finalStagedFileInventory.Count
    if ($inventoryMatches) {
        for ($index = 0; $index -lt $stagedFileInventory.Count; $index++) {
            if (-not [string]::Equals(
                    $stagedFileInventory[$index],
                    $finalStagedFileInventory[$index],
                    [StringComparison]::Ordinal)) {
                $inventoryMatches = $false
                break
            }
        }
    }
    if ($null -ne $finalReparsePoint -or
        -not $inventoryMatches -or
        $finalOwnedExecutablePayload.Count -ne $ownedExecutablePayload.Count) {
        throw 'The signed staging inventory changed outside the closed signing contract.'
    }
    $expectedSigningThumbprint = if ($trustedSigning) {
        $trustedSignerThumbprint
    }
    else {
        $normalizedThumbprint
    }
    foreach ($payloadFile in $finalOwnedExecutablePayload) {
        $payloadSignature = Get-AuthenticodeSignature -LiteralPath $payloadFile.FullName
        if ($payloadSignature.Status -ne [Management.Automation.SignatureStatus]::Valid -or
            $null -eq $payloadSignature.SignerCertificate -or
            -not [string]::Equals(
                $payloadSignature.SignerCertificate.Thumbprint,
                $expectedSigningThumbprint,
                [StringComparison]::OrdinalIgnoreCase)) {
            throw 'A final solution-owned payload signature check failed before package creation.'
        }
    }

    & $makeAppx pack /d $stagingRoot /p $signedPackagePath /o | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "makeappx failed with exit code $LASTEXITCODE."
    }

    $packageSigningEvidence = Invoke-AcceptanceSigning -Path $signedPackagePath
    $signature = Get-AuthenticodeSignature -LiteralPath $signedPackagePath

    if ($null -ne $bundlePublicationRoot) {
        $publicCertificatePath = Join-Path $ownedTempRoot 'GraniteEdgeAI.cer'
        $bundleCertificate = if ($trustedSigning) {
            $signature.SignerCertificate
        }
        else {
            $userCertificate
        }
        [IO.File]::WriteAllBytes(
            $publicCertificatePath,
            $bundleCertificate.Export(
                [Security.Cryptography.X509Certificates.X509ContentType]::Cert))
        $publicCertificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new(
            $publicCertificatePath)
        $bundleCertificateThumbprint = [string] $packageSigningEvidence.Thumbprint
        if ($publicCertificate.HasPrivateKey -or
            -not [string]::Equals(
                $publicCertificate.Thumbprint,
                $bundleCertificateThumbprint,
                [StringComparison]::OrdinalIgnoreCase) -or
            -not [string]::Equals(
                $publicCertificate.Subject,
                $packagePublisher,
                [StringComparison]::Ordinal)) {
            throw 'The exported development bundle certificate is not the exact public signing identity.'
        }

        Write-DevelopmentAcceptanceBundle `
            -SignedPackagePath $signedPackagePath `
            -PublicCertificatePath $publicCertificatePath `
            -GuestRunnerPath (Join-Path $repositoryRoot `
                'scripts\hardware-inspection\Invoke-HardwareInspectionDevelopmentAcceptanceGuest.ps1') `
            -DestinationRoot $bundlePublicationRoot `
            -CertificateThumbprint $bundleCertificateThumbprint
        $bundlePublished = $true
        [pscustomobject]@{
            Classification = if ($trustedSigning) {
                'trusted-signing-candidate'
            }
            else {
                'development-only'
            }
            BundleReady = $true
            SigningChainVerified = [bool] $packageSigningEvidence.ChainTrustVerified
            PublicTrustVerified = $false
            SmartAppControlVerified = $false
            FileCount = 4
        }
        return
    }

    Remove-TestPackage
    Add-AppxPackage -Path $signedPackagePath
    $packageInstalledByInvocation = $true
    $installedPackage = Get-AppxPackage -Name $packageIdentityName
    if ($null -eq $installedPackage -or
        -not [string]::Equals($installedPackage.SignatureKind.ToString(), 'Developer', [StringComparison]::Ordinal) -or
        -not [string]::Equals($installedPackage.Architecture.ToString(), 'X64', [StringComparison]::Ordinal) -or
        -not [string]::Equals($installedPackage.Publisher, $packagePublisher, [StringComparison]::Ordinal) -or
        [Version]$installedPackage.Version -ne $stagedManifest.Version -or
        [IO.Path]::GetFullPath($installedPackage.InstallLocation).StartsWith(
            (Get-NonRootPathWithoutTrailingSeparator -Path $repositoryRoot) + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The test package was not normally installed with the exact signed x64 identity.'
    }

    $appUserModelId = "$($installedPackage.PackageFamilyName)!$($stagedManifest.ApplicationId)"
    for ($run = 1; $run -le $Repetitions; $run++) {
        $resultToken = [Guid]::NewGuid().ToString('N')
        $resultPath = Join-Path $resultRoot "$resultToken.json"
        $createdResultPaths.Add($resultPath)
        $arguments = "--hardware-inspection-process-acceptance --result-token $resultToken"
        $processId = [GraniteEdgeAI.HardwareInspection.AcceptanceLauncher.Activation]::Activate(
            $appUserModelId,
            $arguments)
        $stopwatch = [Diagnostics.Stopwatch]::StartNew()
        while (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
            if ($stopwatch.Elapsed -ge $maximumWait) {
                throw 'The signed acceptance host exceeded its fixed result timeout.'
            }

            $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
            if ($null -eq $process) {
                throw 'The signed acceptance host exited without publishing a result.'
            }

            Start-Sleep -Milliseconds 20
        }

        $result = Read-AcceptanceResult -Path $resultPath
        [pscustomobject]@{
            Run = $run
            SignatureKind = $installedPackage.SignatureKind.ToString()
            PackageIdentityPresent = $result.PackageIdentityPresent
            Total = $result.Total
            Passed = $result.Passed
            Failed = $result.Failed.Count
        }
        if ($result.Failed.Count -ne 0) {
            throw ("Signed acceptance failed {0} test(s): {1}" -f
                $result.Failed.Count,
                ($result.Failed -join ', '))
        }
    }
}
finally {
    try {
        if ($packageInstalledByInvocation) {
            Remove-TestPackage
        }
    }
    finally {
        try {
            $resultCleanupFailure = $null
            foreach ($resultPath in $createdResultPaths) {
                try {
                    if (Test-Path -LiteralPath $resultPath -PathType Leaf) {
                        Remove-Item -LiteralPath $resultPath -Force
                    }
                }
                catch {
                    if ($null -eq $resultCleanupFailure) {
                        $resultCleanupFailure = $_
                    }
                }
            }
            if ($null -ne $resultCleanupFailure) {
                throw $resultCleanupFailure
            }
        }
        finally {
            try {
                if ($null -ne $bundlePublicationRoot -and -not $bundlePublished) {
                    foreach ($bundleName in @(
                            'GraniteEdgeAI.UnitTests.msix',
                            'GraniteEdgeAI.cer',
                            'Invoke-HardwareInspectionDevelopmentAcceptanceGuest.ps1',
                            'bundle-manifest.json')) {
                        $bundlePath = Join-Path $bundlePublicationRoot $bundleName
                        if (Test-Path -LiteralPath $bundlePath -PathType Leaf) {
                            Remove-Item -LiteralPath $bundlePath -Force
                        }
                    }
                }
            }
            finally {
                Assert-OwnedPath -Path $ownedTempRoot -OwnedParent $ownedTempParent
                if (Test-Path -LiteralPath $ownedTempRoot -PathType Container) {
                    Remove-Item -LiteralPath $ownedTempRoot -Recurse -Force
                }
            }
        }
    }
}
