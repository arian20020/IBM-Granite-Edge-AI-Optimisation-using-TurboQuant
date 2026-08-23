[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9A-Fa-f]{40}$')]
    [string] $CertificateThumbprint,

    [ValidateRange(1, 10)]
    [int] $Repetitions = 1,

    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$packageIdentityName = 'GraniteEdgeAI.WinUI.UnitTests'
$packagePublisher = 'CN=GraniteEdgeAI'
$resultSchema = 'granite.hardware-inspection.process-acceptance/v1'
$maximumResultBytes = 64KB
$maximumWait = [TimeSpan]::FromSeconds(180)
$normalizedThumbprint = $CertificateThumbprint.ToUpperInvariant()
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$testOutputRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot (
    'tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\{0}\net8.0-windows10.0.19041.0\win-x64' -f $Configuration)))
$appxLayoutRoot = Join-Path $testOutputRoot 'AppX'
$ownedTempParent = [IO.Path]::GetFullPath((Join-Path $env:TEMP 'GraniteEdgeAI.HardwareInspection.Tests\SignedPackage'))
$ownedTempRoot = Join-Path $ownedTempParent ([Guid]::NewGuid().ToString('N'))
$stagingRoot = Join-Path $ownedTempRoot 'AppX'
$signedPackagePath = Join-Path $ownedTempRoot 'GraniteEdgeAI.UnitTests.msix'
$resultRoot = [IO.Path]::GetFullPath((Join-Path $env:TEMP 'GraniteEdgeAI.HardwareInspection.Tests\Acceptance'))
$createdResultPaths = [Collections.Generic.List[string]]::new()
$installedPackage = $null
$packageInstalledByInvocation = $false

function Assert-OwnedPath {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [string] $OwnedParent
    )

    $resolvedPath = [IO.Path]::GetFullPath($Path)
    $resolvedParentWithSeparator = [IO.Path]::GetFullPath($OwnedParent)
    $parentRoot = [IO.Path]::GetPathRoot($resolvedParentWithSeparator)
    if ([string]::Equals(
            $resolvedParentWithSeparator,
            $parentRoot,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'An owned temporary parent must not be a filesystem root.'
    }

    $resolvedParent = $resolvedParentWithSeparator.TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
    if (-not $resolvedPath.StartsWith(
            $resolvedParent + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'An owned temporary path escaped its fixed parent.'
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

function Remove-TestPackage {
    $packages = @(Get-AppxPackage -Name $packageIdentityName)
    foreach ($package in $packages) {
        if (-not [string]::Equals($package.Name, $packageIdentityName, [StringComparison]::Ordinal)) {
            throw 'Package cleanup resolved an unexpected identity.'
        }

        Remove-AppxPackage -Package $package.PackageFullName
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

Assert-OwnedPath -Path $ownedTempRoot -OwnedParent $ownedTempParent
if (-not (Test-Path -LiteralPath $appxLayoutRoot -PathType Container)) {
    throw 'Build the x64 packaged test project before invoking signed acceptance.'
}

$repositoryItem = Get-Item -LiteralPath $repositoryRoot
if (($repositoryItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw 'Signed acceptance requires a real physical worktree, not a reparse-point root.'
}

$userCertificate = Get-Item -LiteralPath "Cert:\CurrentUser\My\$normalizedThumbprint" -ErrorAction Stop
$trustedCertificate = Get-Item -LiteralPath "Cert:\LocalMachine\TrustedPeople\$normalizedThumbprint" -ErrorAction Stop
if (-not $userCertificate.HasPrivateKey -or
    $trustedCertificate.HasPrivateKey -or
    -not [string]::Equals($userCertificate.Subject, $packagePublisher, [StringComparison]::Ordinal) -or
    -not [string]::Equals($trustedCertificate.Subject, $packagePublisher, [StringComparison]::Ordinal) -or
    $userCertificate.NotBefore -gt [DateTime]::Now -or
    $userCertificate.NotAfter -le [DateTime]::Now -or
    -not ($userCertificate.EnhancedKeyUsageList.ObjectId -contains '1.3.6.1.5.5.7.3.3')) {
    throw 'The purpose-specific package-signing certificate does not satisfy the closed trust contract.'
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
            int result = manager.ActivateApplication(appUserModelId, arguments, 0, out uint processId);
            Marshal.ThrowExceptionForHR(result);
            return processId;
        }
    }
}
'@

try {
    New-Item -ItemType Directory -Path $stagingRoot | Out-Null
    Get-ChildItem -LiteralPath $appxLayoutRoot -Force |
        Copy-Item -Destination $stagingRoot -Recurse -Force
    foreach ($developmentFile in @('vs.appxrecipe', 'AppxBlockMap.xml', 'AppxSignature.p7x', '[Content_Types].xml')) {
        $candidate = Join-Path $stagingRoot $developmentFile
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            Remove-Item -LiteralPath $candidate -Force
        }
    }

    & $makeAppx pack /d $stagingRoot /p $signedPackagePath /o | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "makeappx failed with exit code $LASTEXITCODE."
    }

    & $signTool sign /fd SHA256 /sha1 $normalizedThumbprint $signedPackagePath | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed with exit code $LASTEXITCODE."
    }

    $signature = Get-AuthenticodeSignature -LiteralPath $signedPackagePath
    if ($signature.Status -ne [Management.Automation.SignatureStatus]::Valid -or
        -not [string]::Equals($signature.SignerCertificate.Thumbprint, $normalizedThumbprint, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The signed MSIX did not pass exact Authenticode verification.'
    }

    Remove-TestPackage
    Add-AppxPackage -Path $signedPackagePath
    $packageInstalledByInvocation = $true
    $installedPackage = Get-AppxPackage -Name $packageIdentityName
    if ($null -eq $installedPackage -or
        -not [string]::Equals($installedPackage.SignatureKind.ToString(), 'Developer', [StringComparison]::Ordinal) -or
        -not [string]::Equals($installedPackage.Architecture.ToString(), 'X64', [StringComparison]::Ordinal) -or
        -not [string]::Equals($installedPackage.Publisher, $packagePublisher, [StringComparison]::Ordinal) -or
        [IO.Path]::GetFullPath($installedPackage.InstallLocation).StartsWith(
            [IO.Path]::TrimEndingDirectorySeparator($repositoryRoot) + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The test package was not normally installed with the exact signed x64 identity.'
    }

    $appUserModelId = "$($installedPackage.PackageFamilyName)!App"
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
    if ($packageInstalledByInvocation) {
        Remove-TestPackage
    }

    foreach ($resultPath in $createdResultPaths) {
        if (Test-Path -LiteralPath $resultPath -PathType Leaf) {
            Remove-Item -LiteralPath $resultPath -Force
        }
    }

    Assert-OwnedPath -Path $ownedTempRoot -OwnedParent $ownedTempParent
    if (Test-Path -LiteralPath $ownedTempRoot -PathType Container) {
        Remove-Item -LiteralPath $ownedTempRoot -Recurse -Force
    }
}
