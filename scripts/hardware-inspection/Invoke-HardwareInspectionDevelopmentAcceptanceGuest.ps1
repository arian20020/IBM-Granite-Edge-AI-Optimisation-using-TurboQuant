[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $BundleDirectory,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $ResultDirectory,

    [Parameter(Mandatory)]
    [switch] $ConfirmDisposableGuest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-DevelopmentBundleCertificate {
    param(
        [Parameter(Mandatory)] $Certificate,
        [Parameter(Mandatory)]
        [ValidatePattern('^[0-9A-Fa-f]{40}$')]
        [string] $ExpectedThumbprint,
        [Parameter(Mandatory)][DateTime] $Now
    )

    $normalizedExpectedThumbprint = $ExpectedThumbprint.ToUpperInvariant()
    $normalizedActualThumbprint = ([string]$Certificate.Thumbprint).ToUpperInvariant()
    $codeSigningEkuPresent = @(
        $Certificate.EnhancedKeyUsageList | Where-Object {
            [string]::Equals(
                [string]$_.ObjectId,
                '1.3.6.1.5.5.7.3.3',
                [StringComparison]::Ordinal)
        }).Count -eq 1
    if (-not [string]::Equals(
            $normalizedActualThumbprint,
            $normalizedExpectedThumbprint,
            [StringComparison]::Ordinal) -or
        -not [string]::Equals(
            [string]$Certificate.Subject,
            'CN=GraniteEdgeAI',
            [StringComparison]::Ordinal) -or
        [bool]$Certificate.HasPrivateKey -or
        $Now -lt [DateTime]$Certificate.NotBefore -or
        $Now -gt [DateTime]$Certificate.NotAfter -or
        -not $codeSigningEkuPresent) {
        throw 'The development bundle public certificate does not satisfy the closed contract.'
    }
}

function Read-DevelopmentAcceptanceResult {
    param([Parameter(Mandatory)][string] $Path)

    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -eq 0 -or $bytes.Length -gt 64KB -or $bytes[-1] -ne 0x0a -or
        @($bytes | Where-Object { $_ -eq 0x0a }).Count -ne 1 -or
        @($bytes | Where-Object { $_ -eq 0x0d }).Count -ne 0 -or
        ($bytes.Length -ge 3 -and
            $bytes[0] -eq 0xef -and
            $bytes[1] -eq 0xbb -and
            $bytes[2] -eq 0xbf)) {
        throw 'The development acceptance result has forbidden framing.'
    }

    try {
        $text = [Text.UTF8Encoding]::new($false, $true).GetString($bytes)
    }
    catch {
        throw 'The development acceptance result is not strict UTF-8.'
    }
    $pattern = '^\{"schema":"granite\.hardware-inspection\.process-acceptance/v1",' +
        '"packageIdentityPresent":true,"total":(?<total>[1-9][0-9]{0,2}),' +
        '"passed":(?<passed>[0-9]{1,3}),"failed":\[(?<failed>' +
        '(?:"[A-Za-z0-9_.]{1,512}"(?:,"[A-Za-z0-9_.]{1,512}")*)?)\]\}\n$'
    $match = [Text.RegularExpressions.Regex]::Match(
        $text,
        $pattern,
        [Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if (-not $match.Success) {
        throw 'The development acceptance result does not match the closed JSON-v1 grammar.'
    }

    $total = [int]$match.Groups['total'].Value
    $passed = [int]$match.Groups['passed'].Value
    $failedNames = @([Text.RegularExpressions.Regex]::Matches(
        $match.Groups['failed'].Value,
        '"(?<name>[A-Za-z0-9_.]{1,512})"') | ForEach-Object {
            $_.Groups['name'].Value
        })
    if ($total -gt 128 -or
        $passed -gt $total -or
        $total -ne $passed + $failedNames.Count) {
        throw 'The development acceptance result counts are inconsistent.'
    }
    if ($failedNames.Count -ne 0 -or $total -ne $passed) {
        throw ('A development acceptance repetition failed: {0}' -f
            ($failedNames -join ', '))
    }

    [pscustomobject]@{
        PackageIdentityPresent = $true
        Total = $total
        Passed = $passed
        Failed = $failedNames
    }
}

function Write-DevelopmentAcceptanceSummary {
    param(
        [Parameter(Mandatory)][string] $ResultRoot,
        [Parameter(Mandatory)][object[]] $Repetitions
    )

    if ($Repetitions.Count -ne 3) {
        throw 'Development acceptance requires exactly three repetitions.'
    }

    for ($index = 0; $index -lt 3; $index++) {
        $repetition = $Repetitions[$index]
        if ([int]$repetition.Run -ne $index + 1 -or
            -not [bool]$repetition.PackageIdentityPresent -or
            [int]$repetition.Total -lt 1 -or
            [int]$repetition.Total -gt 128 -or
            [int]$repetition.Total -ne [int]$repetition.Passed -or
            -not [string]::Equals(
                [string]$repetition.SignatureKind,
                'Developer',
                [StringComparison]::Ordinal)) {
            throw 'A development acceptance summary repetition is invalid.'
        }

    }

    $summaryText = '{"schema":"granite.hardware-inspection.development-acceptance/v1",' +
        '"classification":"development-only","publicTrustVerified":false,' +
        '"smartAppControlVerified":false,"repetitions":[' +
        ('{{"run":1,"packageIdentityPresent":true,"total":{0},"passed":{1},"signatureKind":"Developer"}},' -f
            ([int]$Repetitions[0].Total), ([int]$Repetitions[0].Passed)) +
        ('{{"run":2,"packageIdentityPresent":true,"total":{0},"passed":{1},"signatureKind":"Developer"}},' -f
            ([int]$Repetitions[1].Total), ([int]$Repetitions[1].Passed)) +
        ('{{"run":3,"packageIdentityPresent":true,"total":{0},"passed":{1},"signatureKind":"Developer"}}' -f
            ([int]$Repetitions[2].Total), ([int]$Repetitions[2].Passed)) +
        '],"failures":[]}' + "`n"
    $summaryBytes = [Text.UTF8Encoding]::new($false).GetBytes($summaryText)
    if ($summaryBytes.Length -gt 64KB) {
        throw 'The development acceptance summary exceeds its byte bound.'
    }

    $destinationPath = Join-Path $ResultRoot 'development-acceptance.json'
    $temporaryPath = Join-Path $ResultRoot (
        '.development-acceptance.{0}.tmp' -f [Guid]::NewGuid().ToString('N'))
    try {
        [IO.File]::WriteAllBytes($temporaryPath, $summaryBytes)
        Move-Item -LiteralPath $temporaryPath -Destination $destinationPath
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath -PathType Leaf) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
    }
}

function Get-DevelopmentAcceptanceAumid {
    param(
        [Parameter(Mandatory)] $Package,
        [Parameter(Mandatory)][string] $BundleRoot,
        [Parameter(Mandatory)][string] $ResultRoot
    )

    $installRoot = [IO.Path]::GetFullPath([string]$Package.InstallLocation).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
    $normalizedBundleRoot = [IO.Path]::GetFullPath($BundleRoot).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
    $normalizedResultRoot = [IO.Path]::GetFullPath($ResultRoot).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
    $insideBundle = [string]::Equals(
            $installRoot,
            $normalizedBundleRoot,
            [StringComparison]::OrdinalIgnoreCase) -or
        $installRoot.StartsWith(
            $normalizedBundleRoot + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)
    $insideResult = [string]::Equals(
            $installRoot,
            $normalizedResultRoot,
            [StringComparison]::OrdinalIgnoreCase) -or
        $installRoot.StartsWith(
            $normalizedResultRoot + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)
    $packageFamilyName = [string]$Package.PackageFamilyName
    if (-not [string]::Equals(
            [string]$Package.Name,
            'GraniteEdgeAI.WinUI.UnitTests',
            [StringComparison]::Ordinal) -or
        -not [string]::Equals(
            [string]$Package.Publisher,
            'CN=GraniteEdgeAI',
            [StringComparison]::Ordinal) -or
        [Version]$Package.Version -ne [Version]'1.0.0.0' -or
        -not [string]::Equals(
            [string]$Package.Architecture,
            'X64',
            [StringComparison]::Ordinal) -or
        -not [string]::Equals(
            [string]$Package.SignatureKind,
            'Developer',
            [StringComparison]::Ordinal) -or
        $packageFamilyName -notmatch '^[A-Za-z0-9_.-]{1,255}$' -or
        $insideBundle -or
        $insideResult) {
        throw 'The installed test package does not match the closed identity contract.'
    }

    "$packageFamilyName!App"
}

if (-not $ConfirmDisposableGuest) {
    throw 'Disposable guest confirmation is required.'
}

if ($BundleDirectory -notmatch '^[A-Za-z]:[\\/]' -or
    $ResultDirectory -notmatch '^[A-Za-z]:[\\/]') {
    throw 'The bundle and result directories must be absolute paths.'
}

$bundleRoot = [IO.Path]::GetFullPath($BundleDirectory).TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar)
$resultRoot = [IO.Path]::GetFullPath($ResultDirectory).TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar)
if ([string]::Equals($bundleRoot, $resultRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The bundle and result directories must be distinct.'
}

if (-not (Test-Path -LiteralPath $bundleRoot -PathType Container)) {
    throw 'The bundle directory does not exist.'
}
if (-not (Test-Path -LiteralPath $resultRoot -PathType Container) -or
    @(Get-ChildItem -LiteralPath $resultRoot -Force -ErrorAction Stop).Count -ne 0) {
    throw 'The result directory must already exist and be empty.'
}

$bundleItem = Get-Item -LiteralPath $bundleRoot -Force
$resultItem = Get-Item -LiteralPath $resultRoot -Force
if (($bundleItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
    ($resultItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw 'The bundle and result directories must not be reparse points.'
}

$expectedInventory = [string[]]@(
    'GraniteEdgeAI.UnitTests.msix',
    'GraniteEdgeAI.cer',
    'Invoke-HardwareInspectionDevelopmentAcceptanceGuest.ps1',
    'bundle-manifest.json'
)
[Array]::Sort($expectedInventory, [StringComparer]::Ordinal)
$actualItems = @(Get-ChildItem -LiteralPath $bundleRoot -Force -ErrorAction Stop)
$actualInventory = [string[]]@($actualItems | ForEach-Object { $_.Name })
[Array]::Sort($actualInventory, [StringComparer]::Ordinal)
$inventoryMatches = $actualInventory.Count -eq $expectedInventory.Count
if ($inventoryMatches) {
    for ($index = 0; $index -lt $expectedInventory.Count; $index++) {
        if (-not [string]::Equals(
                $expectedInventory[$index],
                $actualInventory[$index],
                [StringComparison]::Ordinal)) {
            $inventoryMatches = $false
            break
        }
    }
}
if (-not $inventoryMatches -or
    @($actualItems | Where-Object { $_.PSIsContainer }).Count -ne 0 -or
    @($actualItems | Where-Object {
            ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0
        }).Count -ne 0) {
    throw 'The development bundle file inventory is not exact.'
}

$manifestPath = Join-Path $bundleRoot 'bundle-manifest.json'
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
    throw 'The development bundle manifest has forbidden framing.'
}

try {
    $manifestText = [Text.UTF8Encoding]::new($false, $true).GetString($manifestBytes)
}
catch {
    throw 'The development bundle manifest is not strict UTF-8.'
}

$manifestPattern = '^\{"schema":"granite\.hardware-inspection\.development-acceptance-bundle/v1",' +
    '"packageName":"GraniteEdgeAI\.WinUI\.UnitTests",' +
    '"publisher":"CN=GraniteEdgeAI",' +
    '"version":"1\.0\.0\.0",' +
    '"architecture":"x64",' +
    '"certificateThumbprint":"(?<thumbprint>[0-9A-F]{40})",' +
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
    throw 'The development bundle manifest does not match the closed JSON-v1 grammar.'
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
    $payloadPath = Join-Path $bundleRoot $payloadContract.Name
    $payloadItem = Get-Item -LiteralPath $payloadPath -Force -ErrorAction Stop
    $payloadHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $payloadPath).Hash
    if ($payloadItem.Length -ne $payloadContract.Length -or
        -not [string]::Equals(
            $payloadHash,
            $payloadContract.Hash,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'A development bundle payload differs from its manifest.'
    }
}

$certificatePath = Join-Path $bundleRoot 'GraniteEdgeAI.cer'
try {
    $bundleCertificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new(
        $certificatePath)
}
catch {
    throw 'The development bundle public certificate is invalid.'
}
if ($null -eq $bundleCertificate) {
    throw 'The development bundle public certificate is invalid.'
}

Assert-DevelopmentBundleCertificate `
    -Certificate $bundleCertificate `
    -ExpectedThumbprint $manifestMatch.Groups['thumbprint'].Value `
    -Now ([DateTime]::Now)

$currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
$currentPrincipal = [Security.Principal.WindowsPrincipal]::new($currentIdentity)
if (-not $currentPrincipal.IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'The development acceptance guest runner must be elevated.'
}

$packageIdentityName = 'GraniteEdgeAI.WinUI.UnitTests'
$existingPackages = @(Get-AppxPackage -Name $packageIdentityName -ErrorAction Stop)
if ($existingPackages.Count -ne 0) {
    throw 'The disposable guest must not contain the test package before acceptance.'
}

try {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

namespace GraniteEdgeAI.HardwareInspection.DevelopmentAcceptanceGuest
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
}
catch {
    throw 'The development acceptance activation boundary could not be initialized.'
}

$acceptanceRoot = [IO.Path]::GetFullPath((Join-Path $env:TEMP `
    'GraniteEdgeAI.HardwareInspection.Tests\Acceptance'))
$acceptanceRootCreated = $false
if (-not (Test-Path -LiteralPath $acceptanceRoot -PathType Container)) {
    New-Item -ItemType Directory -Path $acceptanceRoot -Force | Out-Null
    $acceptanceRootCreated = $true
}
$acceptanceRootItem = Get-Item -LiteralPath $acceptanceRoot -Force -ErrorAction Stop
if (($acceptanceRootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw 'The development acceptance result boundary is not a physical directory.'
}

$normalizedThumbprint = $manifestMatch.Groups['thumbprint'].Value.ToUpperInvariant()
$certificateAddedByInvocation = $false
$installationAttempted = $false
$createdResultPaths = [Collections.Generic.List[string]]::new()
$repetitions = [Collections.Generic.List[object]]::new()
$maximumWait = [TimeSpan]::FromSeconds(180)

try {
    $trustedStore = [Security.Cryptography.X509Certificates.X509Store]::new(
        'TrustedPeople',
        [Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)
    try {
        $trustedStore.Open(
            [Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        $trustedMatches = @($trustedStore.Certificates | Where-Object {
                [string]::Equals(
                    $_.Thumbprint,
                    $normalizedThumbprint,
                    [StringComparison]::OrdinalIgnoreCase)
            })
        if ($trustedMatches.Count -gt 1) {
            throw 'The disposable guest certificate store is not in a closed state.'
        }
        if ($trustedMatches.Count -eq 1) {
            Assert-DevelopmentBundleCertificate `
                -Certificate $trustedMatches[0] `
                -ExpectedThumbprint $normalizedThumbprint `
                -Now ([DateTime]::Now)
        }
        else {
            $trustedStore.Add($bundleCertificate)
            $certificateAddedByInvocation = $true
            $trustedMatches = @($trustedStore.Certificates | Where-Object {
                    [string]::Equals(
                        $_.Thumbprint,
                        $normalizedThumbprint,
                        [StringComparison]::OrdinalIgnoreCase)
                })
            if ($trustedMatches.Count -ne 1) {
                throw 'The public development certificate was not imported exactly once.'
            }
        }
    }
    finally {
        $trustedStore.Close()
    }

    $installationAttempted = $true
    try {
        Add-AppxPackage -Path (Join-Path $bundleRoot 'GraniteEdgeAI.UnitTests.msix')
    }
    catch {
        throw 'The signed development test package could not be installed normally.'
    }
    $installedPackages = @(Get-AppxPackage -Name $packageIdentityName -ErrorAction Stop)
    if ($installedPackages.Count -ne 1) {
        throw 'The signed development test package was not installed exactly once.'
    }
    $installedPackage = $installedPackages[0]
    $appUserModelId = Get-DevelopmentAcceptanceAumid `
        -Package $installedPackage `
        -BundleRoot $bundleRoot `
        -ResultRoot $resultRoot

    for ($run = 1; $run -le 3; $run++) {
        $resultToken = [Guid]::NewGuid().ToString('N')
        $resultPath = Join-Path $acceptanceRoot "$resultToken.json"
        $createdResultPaths.Add($resultPath)
        $arguments = "--hardware-inspection-process-acceptance --result-token $resultToken"
        try {
            $processId = [GraniteEdgeAI.HardwareInspection.DevelopmentAcceptanceGuest.Activation]::Activate(
                $appUserModelId,
                $arguments)
        }
        catch {
            throw 'A development acceptance repetition could not be activated.'
        }

        $stopwatch = [Diagnostics.Stopwatch]::StartNew()
        while (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
            if ($stopwatch.Elapsed -ge $maximumWait) {
                throw 'A development acceptance repetition exceeded its fixed result timeout.'
            }
            if ($null -eq (Get-Process -Id $processId -ErrorAction SilentlyContinue)) {
                throw 'A development acceptance repetition exited without publishing a result.'
            }
            Start-Sleep -Milliseconds 20
        }

        $result = Read-DevelopmentAcceptanceResult -Path $resultPath
        $repetitions.Add([pscustomobject]@{
                Run = $run
                PackageIdentityPresent = $result.PackageIdentityPresent
                Total = $result.Total
                Passed = $result.Passed
                SignatureKind = $installedPackage.SignatureKind.ToString()
            })
    }
}
finally {
    $cleanupFailed = $false
    foreach ($resultPath in $createdResultPaths) {
        try {
            if (Test-Path -LiteralPath $resultPath -PathType Leaf) {
                Remove-Item -LiteralPath $resultPath -Force
            }
            if (Test-Path -LiteralPath $resultPath) {
                $cleanupFailed = $true
            }
        }
        catch {
            $cleanupFailed = $true
        }
    }

    if ($installationAttempted) {
        try {
            $packagesToRemove = @(Get-AppxPackage -Name $packageIdentityName -ErrorAction Stop)
            foreach ($packageToRemove in $packagesToRemove) {
                if (-not [string]::Equals(
                        $packageToRemove.Name,
                        $packageIdentityName,
                        [StringComparison]::Ordinal)) {
                    $cleanupFailed = $true
                    continue
                }
                Remove-AppxPackage -Package $packageToRemove.PackageFullName -ErrorAction Stop
            }
            if (@(Get-AppxPackage -Name $packageIdentityName -ErrorAction Stop).Count -ne 0) {
                $cleanupFailed = $true
            }
        }
        catch {
            $cleanupFailed = $true
        }
    }

    if ($certificateAddedByInvocation) {
        try {
            $cleanupStore = [Security.Cryptography.X509Certificates.X509Store]::new(
                'TrustedPeople',
                [Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)
            try {
                $cleanupStore.Open(
                    [Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
                $certificatesToRemove = @($cleanupStore.Certificates | Where-Object {
                        [string]::Equals(
                            $_.Thumbprint,
                            $normalizedThumbprint,
                            [StringComparison]::OrdinalIgnoreCase)
                    })
                foreach ($certificateToRemove in $certificatesToRemove) {
                    $cleanupStore.Remove($certificateToRemove)
                }
                if (@($cleanupStore.Certificates | Where-Object {
                            [string]::Equals(
                                $_.Thumbprint,
                                $normalizedThumbprint,
                                [StringComparison]::OrdinalIgnoreCase)
                        }).Count -ne 0) {
                    $cleanupFailed = $true
                }
            }
            finally {
                $cleanupStore.Close()
            }
        }
        catch {
            $cleanupFailed = $true
        }
    }

    if ($acceptanceRootCreated) {
        try {
            if (@(Get-ChildItem -LiteralPath $acceptanceRoot -Force -ErrorAction Stop).Count -eq 0) {
                Remove-Item -LiteralPath $acceptanceRoot -Force
            }
        }
        catch {
            $cleanupFailed = $true
        }
    }

    if ($cleanupFailed) {
        $summaryPath = Join-Path $resultRoot 'development-acceptance.json'
        if (Test-Path -LiteralPath $summaryPath -PathType Leaf) {
            Remove-Item -LiteralPath $summaryPath -Force -ErrorAction SilentlyContinue
        }
        throw 'Development acceptance cleanup could not prove a clean guest state.'
    }
}

Write-DevelopmentAcceptanceSummary `
    -ResultRoot $resultRoot `
    -Repetitions $repetitions.ToArray()
[pscustomobject]@{
    Classification = 'development-only'
    Repetitions = 3
    PublicTrustVerified = $false
    SmartAppControlVerified = $false
    CleanupVerified = $true
}
