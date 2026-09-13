[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$StageDirectory,

    [ValidateNotNullOrEmpty()]
    [string]$AllowedDirectoryList = 'licenses'
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

try {
    $root = [IO.Path]::GetFullPath($StageDirectory)
    if (-not (Test-Path -LiteralPath $root -PathType Container)) {
        Stop-Invalid
    }
    $rootItem = Get-Item -LiteralPath $root
    if (($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
        -not (Test-NoAlternateStreams $root)) {
        Stop-Invalid
    }
    [string[]]$expectedDirectories = @($AllowedDirectoryList.Split(',') | Sort-Object)
    [string[]]$actualDirectories = @(Get-ChildItem -LiteralPath $root -Directory -Recurse | ForEach-Object {
        $_.FullName.Substring($root.Length).TrimStart('\').Replace('\', '/')
    } | Sort-Object)
    if (($actualDirectories -join "`n") -cne ($expectedDirectories -join "`n")) {
        Stop-Invalid
    }
    foreach ($relativeDirectory in $actualDirectories) {
        $directory = Get-Item -LiteralPath (Join-Path $root $relativeDirectory.Replace('/', '\'))
        if (($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
            -not (Test-NoAlternateStreams $directory.FullName)) {
            Stop-Invalid
        }
    }

    $manifestPath = Join-Path $root 'worker-manifest.json'
    if (Test-Path -LiteralPath $manifestPath) {
        Stop-Invalid
    }

    $fileMap = [Collections.Generic.Dictionary[string, IO.FileInfo]]::new([StringComparer]::Ordinal)
    foreach ($file in @(Get-ChildItem -LiteralPath $root -File -Recurse)) {
        if (-not (Test-NoAlternateStreams $file.FullName)) {
            Stop-Invalid
        }
        $relative = $file.FullName.Substring($root.Length).TrimStart('\').Replace('\', '/')
        if ($fileMap.ContainsKey($relative)) {
            Stop-Invalid
        }
        $fileMap.Add($relative, $file)
    }
    [string[]]$paths = @($fileMap.Keys)
    [Array]::Sort($paths, [StringComparer]::Ordinal)
    $files = @($paths | ForEach-Object { $fileMap[$_] })
    if ($files.Count -eq 0) {
        Stop-Invalid
    }

    $entries = @()
    foreach ($file in $files) {
        if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or $file.Length -le 0) {
            Stop-Invalid
        }
        $relative = $file.FullName.Substring($root.Length).TrimStart('\').Replace('\', '/')
        if ([string]::IsNullOrWhiteSpace($relative) -or $relative.Contains('..') -or $relative.Contains(':')) {
            Stop-Invalid
        }
        $entries += [ordered]@{
            path = $relative
            length = [Int64]$file.Length
            sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }

    $document = [ordered]@{ schemaVersion = 1; files = $entries }
    $json = $document | ConvertTo-Json -Depth 5
    [IO.File]::WriteAllText($manifestPath, $json + "`n", [Text.UTF8Encoding]::new($false))
    [Console]::Out.WriteLine('worker_manifest_created')
}
catch {
    Stop-Invalid
}
