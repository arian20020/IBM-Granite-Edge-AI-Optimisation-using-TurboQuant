[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$OperationRoot,
    [string[]]$OwnedRoots = @()
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Stop-Invalid {
    [Console]::Out.WriteLine('openvino_cleanup_invalid')
    exit 1
}

try {
    $root = [IO.Path]::GetFullPath($OperationRoot)
    if (-not (Test-Path -LiteralPath $root -PathType Container)) { Stop-Invalid }
    $rootItem = Get-Item -LiteralPath $root -Force
    if ($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) { Stop-Invalid }

    $ownedNames = @(
        'OpenVinoOfficial.Worker.exe',
        'OpenVinoTurboQuant.Worker.exe',
        'OpenVinoConverter.Worker.exe',
        'GraniteEdgeAI.OpenVino.ProtocolTestWorker.exe',
        'GraniteEdgeAI.OpenVino.ParentExitFixture.exe'
    )
    $processes = @(Get-CimInstance Win32_Process -ErrorAction Stop)
    $normalizedOwnedRoots = @($OwnedRoots | Where-Object {
            -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object {
            [IO.Path]::GetFullPath($_).TrimEnd('\', '/') +
                [IO.Path]::DirectorySeparatorChar })
    $owned = @($processes | Where-Object {
        if ($_.Name -in $ownedNames) { return $true }
        if ([string]::IsNullOrWhiteSpace($_.ExecutablePath)) { return $false }
        $path = [IO.Path]::GetFullPath($_.ExecutablePath)
        return @($normalizedOwnedRoots | Where-Object {
            $path.StartsWith($_, [StringComparison]::OrdinalIgnoreCase) }).Count -ne 0
    })
    $ownedIds = [Collections.Generic.HashSet[uint32]]::new()
    foreach ($process in $owned) { $null = $ownedIds.Add([uint32]$process.ProcessId) }
    do {
        $added = $false
        foreach ($process in $processes) {
            if ($ownedIds.Contains([uint32]$process.ParentProcessId) -and
                $ownedIds.Add([uint32]$process.ProcessId)) { $added = $true }
        }
    } while ($added)
    if ($ownedIds.Count -ne 0) { Stop-Invalid }

    $residue = @(Get-ChildItem -LiteralPath $root -Recurse -Force |
        Where-Object {
            ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
            $_.Name -like '.granite-openvino-*.staging' -or
            $_.Name -like '*.partial' -or
            $_.Name -like '*.lock'
        })
    if ($residue.Count -ne 0) { Stop-Invalid }

    foreach ($file in @(Get-ChildItem -LiteralPath $root -File -Recurse -Force)) {
        $stream = $null
        try {
            $stream = [IO.File]::Open($file.FullName, [IO.FileMode]::Open,
                [IO.FileAccess]::Read, [IO.FileShare]::None)
        }
        finally { if ($null -ne $stream) { $stream.Dispose() } }
    }

    $pipeResidue = @(Get-ChildItem -LiteralPath '\\.\pipe\' -ErrorAction Stop |
        Where-Object { $_.Name -match '(?i)granite.*openvino|openvino.*granite' })
    if ($pipeResidue.Count -ne 0) { Stop-Invalid }

    [Console]::Out.WriteLine('openvino_cleanup_valid')
    exit 0
}
catch {
    Stop-Invalid
}
