[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $ProbeDirectory,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $ManifestPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$probeRoot = [IO.Path]::GetFullPath($ProbeDirectory)
$manifestFullPath = [IO.Path]::GetFullPath($ManifestPath)
$executableName = 'GraniteEdgeAI.HardwareInspection.LlamaCppProbe.exe'

if (-not [IO.Directory]::Exists($probeRoot) -or
    ((Get-Item -LiteralPath $probeRoot).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw 'The probe directory is absent or is a reparse point.'
}

$items = @(Get-ChildItem -LiteralPath $probeRoot -Force -ErrorAction Stop)
if ($items.Count -eq 0 -or $items.Count -gt 64 -or
    @($items | Where-Object { $_.PSIsContainer }).Count -ne 0 -or
    @($items | Where-Object { ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 }).Count -ne 0) {
    throw 'The probe package must contain 1..64 flat, non-reparse files.'
}

$members = [string[]]@($items.Name)
[Array]::Sort($members, [StringComparer]::Ordinal)
if (-not ($members -ccontains $executableName)) {
    throw 'The probe executable is absent from the flat package.'
}

$executablePath = Join-Path $probeRoot $executableName
$manifest = [ordered]@{
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

$manifestParent = [IO.Path]::GetDirectoryName($manifestFullPath)
if ([string]::IsNullOrWhiteSpace($manifestParent) -or -not [IO.Directory]::Exists($manifestParent)) {
    throw 'The manifest parent directory does not exist.'
}

$probePrefix = $probeRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if ($manifestFullPath.StartsWith($probePrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The detached manifest must remain outside the probe directory.'
}

$json = $manifest | ConvertTo-Json -Depth 6 -Compress
$temporaryPath = $manifestFullPath + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
try {
    [IO.File]::WriteAllText($temporaryPath, $json + "`n", [Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temporaryPath -Destination $manifestFullPath -Force
}
finally {
    if ([IO.File]::Exists($temporaryPath)) {
        Remove-Item -LiteralPath $temporaryPath -Force
    }
}
