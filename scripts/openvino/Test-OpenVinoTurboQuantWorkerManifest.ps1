[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$StageDirectory
)

$ErrorActionPreference = 'Stop'

function Stop-Invalid {
    [Console]::Out.WriteLine('turboquant_worker_manifest_invalid')
    exit 1
}

function Get-Sha256Hex {
    param([Parameter(Mandatory)][string]$Path)
    $stream = [IO.File]::OpenRead($Path)
    try {
        $sha256 = [Security.Cryptography.SHA256]::Create()
        try {
            return ([BitConverter]::ToString($sha256.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
        }
        finally {
            $sha256.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Assert-ExactProperties {
    param([object]$Value, [string[]]$Names)
    $actual = @($Value.PSObject.Properties.Name | Sort-Object)
    $expected = @($Names | Sort-Object)
    if (($actual -join "`n") -cne ($expected -join "`n")) { throw 'Unexpected manifest shape.' }
}

try {
    $stage = [IO.Path]::GetFullPath($StageDirectory)
    if (-not (Test-Path -LiteralPath $stage -PathType Container) -or
        ((Get-Item -LiteralPath $stage -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        Stop-Invalid
    }
    $manifestPath = Join-Path $stage 'worker-manifest.json'
    $runtimeManifestPath = Join-Path $stage 'turboquant-runtime.manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf) -or
        -not (Test-Path -LiteralPath $runtimeManifestPath -PathType Leaf)) { Stop-Invalid }
    $runtimeManifest = Get-Content -LiteralPath $runtimeManifestPath -Raw | ConvertFrom-Json
    Assert-ExactProperties $runtimeManifest @(
        'schemaVersion', 'component', 'platform', 'configuration', 'sourceCommit',
        'implementationCommit', 'genAiCommit', 'acceptedTuple', 'files')
    Assert-ExactProperties $runtimeManifest.acceptedTuple @(
        'codec', 'device', 'attention', 'headDimension')
    if ($runtimeManifest.schemaVersion -ne 1 -or
        $runtimeManifest.component -cne 'openvino-turboquant-runtime' -or
        $runtimeManifest.platform -cne 'windows-x86_64' -or
        $runtimeManifest.configuration -cne 'Release' -or
        $runtimeManifest.sourceCommit -cne 'f5f594dc0c9e5961785f0d17743486d52eac87e7' -or
        $runtimeManifest.implementationCommit -cne 'b9a1f201c109e0bed74763934f79483cf6c4cbf4' -or
        $runtimeManifest.genAiCommit -cne '6fbc103538d30d42da4b0b5130a4792a20f728ba' -or
        $runtimeManifest.acceptedTuple.codec -cne 'TBQ4/TBQ3' -or
        $runtimeManifest.acceptedTuple.device -cne 'CPU' -or
        $runtimeManifest.acceptedTuple.attention -cne 'SDPA' -or
        $runtimeManifest.acceptedTuple.headDimension -ne 64 -or
        @($runtimeManifest.files).Count -eq 0) {
        Stop-Invalid
    }
    $allowedKinds = [Collections.Generic.HashSet[string]]::new(
        [string[]]@('license', 'conformance-test', 'source-ledger', 'runtime-binary'),
        [StringComparer]::Ordinal)
    $allowed = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($entry in @($runtimeManifest.files)) {
        Assert-ExactProperties $entry @('path', 'length', 'sha256', 'kind')
        $relative = [string]$entry.path
        $added = $allowed.Add($relative)
        if ($relative -cnotmatch '^[a-zA-Z0-9_.\-/]+$' -or $relative.Contains('..') -or
            $entry.length -le 0 -or $entry.sha256 -cnotmatch '^[0-9a-f]{64}$' -or
            -not $allowedKinds.Contains([string]$entry.kind) -or
            -not $added) { Stop-Invalid }
        $runtimeFile = [IO.Path]::GetFullPath((Join-Path $stage $relative))
        if (-not $runtimeFile.StartsWith(
                $stage.TrimEnd('\', '/') + '\',
                [StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $runtimeFile -PathType Leaf) -or
            ((Get-Item -LiteralPath $runtimeFile -Force).Attributes -band
                [IO.FileAttributes]::ReparsePoint) -or
            (Get-Item -LiteralPath $runtimeFile).Length -ne [Int64]$entry.length -or
            (Get-Sha256Hex -Path $runtimeFile) -cne
                [string]$entry.sha256) {
            Stop-Invalid
        }
    }
    foreach ($extra in @(
        'turboquant-runtime.manifest.json',
        'OpenVinoTurboQuant.Worker.exe',
        'OpenVinoTurboQuant.Probe.dll',
        'licenses/nlohmann-json-LICENSE.MIT.txt')) {
        if (-not $allowed.Add($extra)) { Stop-Invalid }
    }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    Assert-ExactProperties $manifest @('schemaVersion', 'files')
    if ($manifest.schemaVersion -ne 1 -or @($manifest.files).Count -ne $allowed.Count) { Stop-Invalid }
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($entry in @($manifest.files)) {
        Assert-ExactProperties $entry @('path', 'length', 'sha256')
        $relative = [string]$entry.path
        $added = $seen.Add($relative)
        if (-not $allowed.Contains($relative) -or -not $added -or
            $entry.length -le 0 -or $entry.sha256 -cnotmatch '^[0-9a-f]{64}$') { Stop-Invalid }
        $file = [IO.Path]::GetFullPath((Join-Path $stage $relative))
        if (-not $file.StartsWith($stage.TrimEnd('\', '/') + '\', [StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $file -PathType Leaf) -or
            ((Get-Item -LiteralPath $file -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -or
            (Get-Item -LiteralPath $file).Length -ne [Int64]$entry.length -or
            (Get-Sha256Hex -Path $file) -cne [string]$entry.sha256) {
            Stop-Invalid
        }
    }
    $actual = @(Get-ChildItem -LiteralPath $stage -File -Recurse | Where-Object {
        $_.FullName -cne $manifestPath
    })
    if ($actual.Count -ne $allowed.Count) { Stop-Invalid }
    [Console]::Out.WriteLine('turboquant_worker_manifest_valid')
}
catch {
    Stop-Invalid
}
