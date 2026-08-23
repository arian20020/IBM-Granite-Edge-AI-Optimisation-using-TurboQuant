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
$packageVersion = [Version]'1.0.0.0'
$resultSchema = 'granite.hardware-inspection.process-acceptance/v1'
$maximumResultBytes = 64KB
$maximumWait = [TimeSpan]::FromSeconds(180)
$normalizedThumbprint = $CertificateThumbprint.ToUpperInvariant()
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$testOutputRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot (
    'tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\{0}\net8.0-windows10.0.19041.0\win-x64' -f $Configuration)))
$appPackagesRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot `
    'tests\UnitTests\GraniteEdgeAI.UnitTests\AppPackages'))
$ownedTempParent = [IO.Path]::GetFullPath((Join-Path $env:TEMP 'GEAI-HI-Signed'))
$ownedTempRoot = Join-Path $ownedTempParent ([Guid]::NewGuid().ToString('N').Substring(0, 8))
$stagingRoot = Join-Path $ownedTempRoot 'AppX'
$signedPackagePath = Join-Path $ownedTempRoot 'GraniteEdgeAI.UnitTests.msix'
$resultRoot = [IO.Path]::GetFullPath((Join-Path $env:TEMP 'GraniteEdgeAI.HardwareInspection.Tests\Acceptance'))
$createdResultPaths = [Collections.Generic.List[string]]::new()
$installedPackage = $null
$packageInstalledByInvocation = $false

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

    $ownedExecutablePayload = @(Get-SolutionOwnedExecutablePayload -LayoutRoot $stagingRoot)
    if ($ownedExecutablePayload.Count -eq 0 -or $ownedExecutablePayload.Count -gt 128) {
        throw 'The solution-owned executable payload count is outside its closed bound.'
    }

    foreach ($payloadFile in $ownedExecutablePayload) {
        Assert-OwnedPath -Path $payloadFile.FullName -OwnedParent $stagingRoot
        & $signTool sign /fd SHA256 /sha1 $normalizedThumbprint $payloadFile.FullName | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "signtool failed for a solution-owned executable payload with exit code $LASTEXITCODE."
        }

        $payloadSignature = Get-AuthenticodeSignature -LiteralPath $payloadFile.FullName
        if ($payloadSignature.Status -ne [Management.Automation.SignatureStatus]::Valid -or
            -not [string]::Equals(
                $payloadSignature.SignerCertificate.Thumbprint,
                $normalizedThumbprint,
                [StringComparison]::OrdinalIgnoreCase)) {
            throw 'A solution-owned executable payload did not pass exact Authenticode verification.'
        }
    }

    $stagedProbeDirectory = Join-Path $stagingRoot 'HardwareInspection\LlamaCppProbe'
    $stagedProbeManifestPath = Join-Path $stagingRoot 'HardwareInspection\llamacpp-probe-manifest.json'
    & powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
        -File (Join-Path $repositoryRoot 'scripts\hardware-inspection\New-LlamaCppProbeManifest.ps1') `
        -ProbeDirectory $stagedProbeDirectory `
        -ManifestPath $stagedProbeManifestPath
    if ($LASTEXITCODE -ne 0) {
        throw 'The detached probe manifest could not be regenerated after payload signing.'
    }
    & powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
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
