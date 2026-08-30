[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $ProbeDirectory,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $ManifestPath,

    [switch] $EmitVerifiedFilePaths
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Amd64PortableExecutable {
    param([Parameter(Mandatory)][string] $Path)

    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        if ($stream.Length -lt 64) { throw 'The probe executable is not a bounded PE image.' }
        $reader = [IO.BinaryReader]::new($stream, [Text.Encoding]::ASCII, $true)
        try {
            if ($reader.ReadUInt16() -ne 0x5a4d) { throw 'The probe executable lacks an MZ header.' }
            $stream.Position = 0x3c
            $peOffset = $reader.ReadInt32()
            if ($peOffset -lt 64 -or $peOffset -gt $stream.Length - 6) { throw 'The probe PE offset is invalid.' }
            $stream.Position = $peOffset
            if ($reader.ReadUInt32() -ne 0x00004550 -or $reader.ReadUInt16() -ne 0x8664) {
                throw 'The probe executable is not AMD64 PE.'
            }
        }
        finally { $reader.Dispose() }
    }
    finally { $stream.Dispose() }
}

$probeRoot = [IO.Path]::GetFullPath($ProbeDirectory)
$manifestFullPath = [IO.Path]::GetFullPath($ManifestPath)
$executableName = 'GraniteEdgeAI.HardwareInspection.LlamaCppProbe.exe'
if (-not [IO.Directory]::Exists($probeRoot) -or -not [IO.File]::Exists($manifestFullPath)) {
    throw 'The probe package or detached manifest is absent.'
}

$items = @(Get-ChildItem -LiteralPath $probeRoot -Force -ErrorAction Stop)
if ($items.Count -eq 0 -or $items.Count -gt 64 -or
    @($items | Where-Object { $_.PSIsContainer }).Count -ne 0 -or
    @($items | Where-Object { ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 }).Count -ne 0) {
    throw 'The probe package is not a closed flat file inventory.'
}

$members = [string[]]@($items.Name)
[Array]::Sort($members, [StringComparer]::Ordinal)
if (-not ($members -ccontains $executableName)) { throw 'The exact probe executable is absent.' }
$executablePath = Join-Path $probeRoot $executableName
Assert-Amd64PortableExecutable -Path $executablePath

$expected = [ordered]@{
    schemaVersion = 1
    toolId = 'granite-edge-hardware-llamacpp-probe'
    version = '0.27.0-cpu-win-x64'
    executable = $executableName
    executableSha256 = (Get-FileHash -LiteralPath $executablePath -Algorithm SHA256).Hash.ToLowerInvariant()
    members = $members
    machine = 'Amd64'
    disposition = 'AcceptedForFunctionalEvaluation'
    commands = @(
        [ordered]@{ identity = 'identity'; arguments = @('identity', '--format', 'json-v1') },
        [ordered]@{ identity = 'capabilities'; arguments = @('capabilities', '--format', 'json-v1') }
    )
}
$expectedBytes = [Text.UTF8Encoding]::new($false).GetBytes(($expected | ConvertTo-Json -Depth 6 -Compress) + "`n")
$actualBytes = [IO.File]::ReadAllBytes($manifestFullPath)
if ($actualBytes.Length -eq 0 -or $actualBytes.Length -gt 65536 -or
    -not [Linq.Enumerable]::SequenceEqual([byte[]]$actualBytes, [byte[]]$expectedBytes)) {
    throw 'The probe manifest is not the exact canonical manifest for this package.'
}

try { $null = [Text.UTF8Encoding]::new($false, $true).GetString($actualBytes) | ConvertFrom-Json }
catch { throw 'The probe manifest is not strict UTF-8 JSON.' }

if ($EmitVerifiedFilePaths) {
    foreach ($member in $members) {
        [Console]::Out.WriteLine((Join-Path $probeRoot $member))
    }
}
