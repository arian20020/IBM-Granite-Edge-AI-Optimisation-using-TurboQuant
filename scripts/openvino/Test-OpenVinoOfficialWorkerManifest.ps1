[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$StageDirectory
)

$ErrorActionPreference = 'Stop'

function Stop-Invalid {
    [Console]::Out.WriteLine('worker_manifest_invalid')
    exit 1
}

function Test-NoAlternateStreams {
    param([string]$Path)
    if (-not ('GraniteEdgeAIOfficialWorkerStreams' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

public static class GraniteEdgeAIOfficialWorkerStreams
{
    private const int ErrorNoMoreFiles = 18;
    private const int ErrorHandleEof = 38;
    private static readonly IntPtr InvalidHandle = new IntPtr(-1);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FindStreamData
    {
        public long StreamSize;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 296)]
        public string StreamName;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindFirstStreamW(string fileName, int infoLevel, out FindStreamData data, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FindNextStreamW(IntPtr handle, out FindStreamData data);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FindClose(IntPtr handle);

    public static string[] GetNames(string path)
    {
        var names = new List<string>();
        FindStreamData data;
        IntPtr handle = FindFirstStreamW(path, 0, out data, 0);
        if (handle == InvalidHandle)
        {
            int error = Marshal.GetLastWin32Error();
            if (error == ErrorNoMoreFiles || error == ErrorHandleEof) return names.ToArray();
            throw new Win32Exception(error);
        }
        try
        {
            names.Add(data.StreamName);
            while (FindNextStreamW(handle, out data)) names.Add(data.StreamName);
            int error = Marshal.GetLastWin32Error();
            if (error != ErrorNoMoreFiles && error != ErrorHandleEof) throw new Win32Exception(error);
        }
        finally { FindClose(handle); }
        return names.ToArray();
    }
}
'@
    }
    $streams = @([GraniteEdgeAIOfficialWorkerStreams]::GetNames($Path))
    return @($streams | Where-Object { $_ -cne '::$DATA' }).Count -eq 0
}

function Get-PortableExecutableMachine {
    param([string]$Path)
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    $reader = [IO.BinaryReader]::new($stream)
    try {
        if ($stream.Length -lt 64 -or $reader.ReadUInt16() -ne 0x5a4d) {
            return -1
        }
        $stream.Position = 0x3c
        [Int32]$peOffset = $reader.ReadInt32()
        if ($peOffset -lt 64 -or $peOffset -gt ($stream.Length - 6)) {
            return -1
        }
        $stream.Position = $peOffset
        if ($reader.ReadUInt32() -ne 0x00004550) {
            return -1
        }
        return [Int32]($reader.ReadUInt16())
    }
    finally {
        $reader.Dispose()
    }
}

try {
    $root = [IO.Path]::GetFullPath($StageDirectory)
    $requiredRootFiles = @(
        'OpenVinoOfficial.Worker.exe',
        'openvino.dll',
        'openvino_genai.dll',
        'openvino_intel_cpu_plugin.dll',
        'openvino_intel_gpu_plugin.dll',
        'openvino_ir_frontend.dll',
        'openvino_tokenizers.dll',
        'tbb12.dll',
        'tbbbind_2_5.dll'
    )
    $requiredLicenses = @(
        'licenses/Apache_license.txt',
        'licenses/LICENSE-GENAI.txt',
        'licenses/nlohmann-json-LICENSE.MIT.txt',
        'licenses/openvino-tokenizers-LICENSE.txt',
        'licenses/openvino-tokenizers-third-party-programs.txt',
        'licenses/runtime-third-party-programs.txt',
        'licenses/TBB-LICENSE.txt',
        'licenses/third-party-programs-genai.txt'
    )
    [string[]]$allowed = @($requiredRootFiles + $requiredLicenses)
    [Array]::Sort($allowed, [StringComparer]::Ordinal)
    $manifestPath = Join-Path $root 'worker-manifest.json'
    $rootItem = Get-Item -LiteralPath $root
    if (($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
        -not (Test-NoAlternateStreams $root)) {
        Stop-Invalid
    }
    $directories = @(Get-ChildItem -LiteralPath $root -Directory -Recurse)
    if ($directories.Count -ne 1 -or
        $directories[0].FullName -cne (Join-Path $root 'licenses') -or
        ($directories[0].Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
        -not (Test-NoAlternateStreams $directories[0].FullName)) {
        Stop-Invalid
    }
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf) -or
        -not (Test-NoAlternateStreams $manifestPath)) {
        Stop-Invalid
    }
    $manifestFile = Get-Item -LiteralPath $manifestPath
    if (($manifestFile.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
        $manifestFile.Length -le 0 -or $manifestFile.Length -gt 1MB) {
        Stop-Invalid
    }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ([Int64]$manifest.schemaVersion -ne 1 -or @($manifest.files).Count -ne $allowed.Count -or
        @($manifest.PSObject.Properties.Name | Where-Object { $_ -notin @('schemaVersion', 'files') }).Count -ne 0) {
        Stop-Invalid
    }

    [string[]]$actual = @(Get-ChildItem -LiteralPath $root -File -Recurse | ForEach-Object {
        $_.FullName.Substring($root.Length).TrimStart('\').Replace('\', '/')
    } | Where-Object { $_ -ne 'worker-manifest.json' })
    [Array]::Sort($actual, [StringComparer]::Ordinal)
    if ($actual.Count -ne $allowed.Count) {
        Stop-Invalid
    }
    for ($index = 0; $index -lt $allowed.Count; $index++) {
        if ($actual[$index] -cne $allowed[$index]) {
            Stop-Invalid
        }
    }

    $previous = ''
    foreach ($entry in @($manifest.files)) {
        $properties = @($entry.PSObject.Properties.Name)
        if ($properties.Count -ne 3 -or @($properties | Where-Object { $_ -notin @('path', 'length', 'sha256') }).Count -ne 0) {
            Stop-Invalid
        }
        $relative = [string]$entry.path
        if ($relative -notin $allowed -or ($previous -and [string]::CompareOrdinal($previous, $relative) -ge 0) -or
            [Int64]$entry.length -le 0 -or [string]$entry.sha256 -cnotmatch '^[0-9a-f]{64}$') {
            Stop-Invalid
        }
        $previous = $relative
        $path = Join-Path $root $relative.Replace('/', '\')
        $file = Get-Item -LiteralPath $path
        if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
            -not (Test-NoAlternateStreams $path) -or $file.Length -ne [Int64]$entry.length -or
            (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne [string]$entry.sha256) {
            Stop-Invalid
        }
        $first = [IO.File]::ReadAllBytes($path)
        $isPe = $first.Length -ge 2 -and $first[0] -eq 0x4d -and $first[1] -eq 0x5a
        $isExecutable = $relative.EndsWith('.exe') -or $relative.EndsWith('.dll')
        if (($isExecutable -and (Get-PortableExecutableMachine $path) -ne 0x8664) -or
            (-not $isExecutable -and $isPe)) {
            Stop-Invalid
        }
    }

    [Console]::Out.WriteLine('worker_manifest_valid')
}
catch {
    Stop-Invalid
}
