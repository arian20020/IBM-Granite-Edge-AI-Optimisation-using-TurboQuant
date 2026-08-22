[CmdletBinding()]
param([Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$StageDirectory)

$ErrorActionPreference = 'Stop'

if (-not ('GraniteEdgeAI.OpenVino.ConverterManifestHasher' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Security.Cryptography;

namespace GraniteEdgeAI.OpenVino
{
    public static class ConverterManifestHasher
    {
        public static string Sha256(string path)
        {
            using (FileStream stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read, 131072,
                FileOptions.SequentialScan))
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream))
                    .Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
'@
}

function Stop-Verification {
    [Console]::Out.WriteLine('converter_manifest_invalid')
    exit 1
}

try {
    $root = [IO.Path]::GetFullPath($StageDirectory).TrimEnd('\', '/')
    $manifestPath = Join-Path $root 'converter-manifest.json'
    if (-not (Test-Path -LiteralPath $root -PathType Container) -or
        -not (Test-Path -LiteralPath $manifestPath -PathType Leaf) -or
        ((Get-Item -LiteralPath $root -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'converter-root-invalid'
    }
    Import-Module (Join-Path $PSScriptRoot 'OpenVinoClosedJson.psm1') -Force
    $raw = Get-OpenVinoClosedJsonText -Path $manifestPath `
        -MaximumBytes 8388608 -MaximumDepth 8
    $manifest = $raw | ConvertFrom-Json -ErrorAction Stop
    $rootFields = @('schemaVersion','protocol','protocolVersion','pythonSha256',
        'wheelManifestSha256','requirementsLockSha256','wheelLockStatus',
        'maximumOperationMinutes','launchArguments','files')
    $actualFields = @($manifest.PSObject.Properties.Name)
    if ($actualFields.Count -ne $rootFields.Count -or
        (Compare-Object $rootFields $actualFields -SyncWindow 0)) {
        throw 'converter-manifest-shape-invalid'
    }
    if ($manifest.schemaVersion -ne 1 -or
        $manifest.protocol -cne 'granite.openvino.converter' -or
        $manifest.protocolVersion -ne 1 -or
        $manifest.wheelLockStatus -cne 'resolved' -or
        $manifest.maximumOperationMinutes -ne 120 -or
        $manifest.pythonSha256 -cnotmatch '^[0-9a-f]{64}$' -or
        $manifest.wheelManifestSha256 -cnotmatch '^[0-9a-f]{64}$' -or
        $manifest.requirementsLockSha256 -cnotmatch '^[0-9a-f]{64}$' -or
        (@($manifest.launchArguments) -join '\0') -cne '-I\0-s\0-E\0-S\0-B\0-m\0converter') {
        throw 'converter-manifest-value-invalid'
    }
    $expected = New-Object 'System.Collections.Generic.Dictionary[string,object]' `
        ([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in @($manifest.files)) {
        $fields = @($entry.PSObject.Properties.Name)
        if ($fields.Count -ne 3 -or
            (Compare-Object @('path','length','sha256') $fields -SyncWindow 0) -or
            $entry.path -isnot [string] -or $entry.path -cnotmatch '^[^\\/:]+(?:/[^\\/:]+)*$' -or
            $entry.path -ceq 'converter-manifest.json' -or
            ($entry.length -isnot [int] -and $entry.length -isnot [long]) -or
            [long]$entry.length -lt 0 -or $entry.sha256 -cnotmatch '^[0-9a-f]{64}$' -or
            $expected.ContainsKey([string]$entry.path)) {
            throw 'converter-file-entry-invalid'
        }
        $expected.Add([string]$entry.path, $entry)
    }
    if ($expected.Count -eq 0) { throw 'converter-inventory-empty' }
    $actual = @(Get-ChildItem -LiteralPath $root -File -Recurse -Force |
        Where-Object FullName -cne $manifestPath)
    if ($actual.Count -ne $expected.Count) { throw 'converter-inventory-mismatch' }
    foreach ($file in $actual) {
        if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw 'converter-reparse-rejected'
        }
        $relative = $file.FullName.Substring($root.Length + 1).Replace('\', '/')
        $entry = $null
        if (-not $expected.TryGetValue($relative, [ref]$entry) -or
            $file.Length -ne [long]$entry.length -or
            [GraniteEdgeAI.OpenVino.ConverterManifestHasher]::Sha256($file.FullName) -cne
                [string]$entry.sha256) {
            throw 'converter-file-mismatch'
        }
    }
    [Console]::Out.WriteLine('converter_manifest_valid')
    exit 0
}
catch {
    if ($env:GRANITE_OPENVINO_VERIFIER_DEBUG -ceq '1') {
        [Console]::Error.WriteLine($_.Exception.Message)
    }
    Stop-Verification
}
