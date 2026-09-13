[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$DllPath,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'

try {
    $dll = [IO.Path]::GetFullPath($DllPath)
    $output = [IO.Path]::GetFullPath($OutputDirectory)
    if (-not (Test-Path -LiteralPath $dll -PathType Leaf) -or
        ((Get-Item -LiteralPath $dll -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'The input DLL is not an ordinary file.'
    }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    $installation = & $vswhere -latest -products * `
        -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($installation)) {
        throw 'A verified Visual C++ toolchain was not found.'
    }
    $toolRoot = Get-ChildItem -LiteralPath (Join-Path $installation 'VC\Tools\MSVC') -Directory |
        Sort-Object Name -Descending | Select-Object -First 1
    $dumpbin = Join-Path $toolRoot.FullName 'bin\Hostx64\x64\dumpbin.exe'
    $lib = Join-Path $toolRoot.FullName 'bin\Hostx64\x64\lib.exe'
    if (-not (Test-Path -LiteralPath $dumpbin -PathType Leaf) -or
        -not (Test-Path -LiteralPath $lib -PathType Leaf)) {
        throw 'The Visual C++ import-library tools are unavailable.'
    }

    [IO.Directory]::CreateDirectory($output) | Out-Null
    $baseName = [IO.Path]::GetFileNameWithoutExtension($dll)
    $definitionPath = Join-Path $output ($baseName + '.def')
    $libraryPath = Join-Path $output ($baseName + '.lib')
    $exports = [Collections.Generic.List[string]]::new()
    foreach ($line in @(& $dumpbin /nologo /exports $dll)) {
        if ($line -match '^\s+\d+\s+[0-9A-F]+\s+[0-9A-F]+\s+(\S+)\s*$') {
            $exports.Add($Matches[1])
        }
    }
    if ($LASTEXITCODE -ne 0 -or $exports.Count -eq 0) {
        throw 'No DLL exports were recovered.'
    }
    $definition = [Collections.Generic.List[string]]::new()
    $definition.Add('LIBRARY ' + [IO.Path]::GetFileName($dll))
    $definition.Add('EXPORTS')
    foreach ($name in $exports) { $definition.Add('    ' + $name) }
    [IO.File]::WriteAllLines($definitionPath, $definition, [Text.UTF8Encoding]::new($false))

    & $lib /nologo "/def:$definitionPath" /machine:x64 "/out:$libraryPath" | Out-Null
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $libraryPath -PathType Leaf)) {
        throw 'The import library was not generated.'
    }
    [Console]::Out.WriteLine($libraryPath)
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
