[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$PackageRoot,
    [Parameter(Mandatory)] [string]$ManifestPath,
    [Parameter(Mandatory)] [ValidatePattern('^[0-9a-fA-F]{40}$')] [string]$RuntimeSourceCommit,
    [Parameter(Mandatory)] [string]$RuntimeBuildId,
    [Parameter(Mandatory)] [string[]]$BuildFlags
)

$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath($PackageRoot)
if (-not (Test-Path -LiteralPath $root -PathType Container)) {
    throw 'The GGUF runtime package root does not exist.'
}

$files = @(Get-ChildItem -LiteralPath $root -File -Recurse | Sort-Object FullName)
if ($files.Count -eq 0) {
    throw 'The GGUF runtime package is empty.'
}

$entries = foreach ($file in $files) {
    $relative = [System.IO.Path]::GetRelativePath($root, $file.FullName).Replace('\', '/')
    $role = if ($relative -ceq 'Worker/GraniteEdgeAI.GgufRuntime.Worker.exe') {
        'Supervisor'
    } elseif ($relative -ceq 'Cli/llama-cli.exe') {
        'Cli'
    } elseif ($file.Name -match '^(LICENSE|COPYING|NOTICE)') {
        'License'
    } else {
        'Dependency'
    }

    [ordered]@{
        relativePath = $relative
        length = $file.Length
        sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        architecture = 'X64'
        role = $role
        licenseReference = 'Cli/LICENSE.txt'
    }
}

if (@($entries | Where-Object role -eq 'Supervisor').Count -ne 1) {
    throw 'The package must contain exactly one protected supervisor.'
}
if (@($entries | Where-Object role -eq 'Cli').Count -ne 1) {
    throw 'The package must contain exactly one llama-cli.exe.'
}
if (@($entries | Where-Object role -eq 'License').Count -lt 1) {
    throw 'The package must contain CLI license material.'
}

$manifest = [ordered]@{
    schemaVersion = 1
    runtimeBuildId = $RuntimeBuildId
    runtimeSourceCommit = $RuntimeSourceCommit.ToLowerInvariant()
    buildFlags = @($BuildFlags)
    files = @($entries)
}
$manifestDirectory = Split-Path -Parent ([System.IO.Path]::GetFullPath($ManifestPath))
New-Item -ItemType Directory -Path $manifestDirectory -Force | Out-Null
$json = $manifest | ConvertTo-Json -Depth 8
[System.IO.File]::WriteAllText(
    [System.IO.Path]::GetFullPath($ManifestPath),
    $json + [Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false))
