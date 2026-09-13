[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$EvidencePath,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$StageDirectory,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string]$ExpectedCommitSha,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$ExpectedPackageManifestSha256,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$ExpectedModelSha256,
    [Parameter(Mandatory)][long]$ExpectedModelLength
)

$ErrorActionPreference = 'Stop'

function Stop-Unverified {
    [Console]::Out.WriteLine('turboquant_activation_unverified')
    exit 1
}

function Assert-ExactProperties {
    param([object]$Value, [string[]]$Names)
    $actual = @($Value.PSObject.Properties.Name | Sort-Object)
    $expected = @($Names | Sort-Object)
    if (($actual -join "`n") -cne ($expected -join "`n")) { throw 'Unexpected evidence shape.' }
}

function Get-LowerHash {
    param([string]$LiteralPath)
    return (Get-FileHash -LiteralPath $LiteralPath -Algorithm SHA256).Hash.ToLowerInvariant()
}

try {
    $powershell = (Get-Command powershell.exe -ErrorAction Stop).Source
    $stage = [IO.Path]::GetFullPath($StageDirectory)
    $evidenceFile = [IO.Path]::GetFullPath($EvidencePath)
    if (-not (Test-Path -LiteralPath $stage -PathType Container) -or
        -not (Test-Path -LiteralPath $evidenceFile -PathType Leaf) -or
        ((Get-Item -LiteralPath $stage -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -or
        ((Get-Item -LiteralPath $evidenceFile -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        Stop-Unverified
    }
    $workerClosure = & $powershell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
        -File (Join-Path $PSScriptRoot 'Test-OpenVinoTurboQuantWorkerManifest.ps1') `
        -StageDirectory $stage
    if ($LASTEXITCODE -ne 0 -or [string]$workerClosure -cne 'turboquant_worker_manifest_valid') {
        Stop-Unverified
    }
    $manifestPath = Join-Path $stage 'worker-manifest.json'
    $runtimeManifestPath = Join-Path $stage 'turboquant-runtime.manifest.json'
    foreach ($required in @($manifestPath, $runtimeManifestPath, (Join-Path $stage 'OpenVinoTurboQuant.Worker.exe'),
        (Join-Path $stage 'OpenVinoTurboQuant.Probe.dll'))) {
        if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { Stop-Unverified }
    }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    Assert-ExactProperties $manifest @('schemaVersion', 'files')
    if ($manifest.schemaVersion -ne 1 -or @($manifest.files).Count -eq 0) { Stop-Unverified }
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($entry in @($manifest.files)) {
        Assert-ExactProperties $entry @('path', 'length', 'sha256')
        if ($entry.path -cnotmatch '^[a-zA-Z0-9_.\-/]+$' -or $entry.path.Contains('..') -or
            $entry.length -le 0 -or $entry.sha256 -cnotmatch '^[0-9a-f]{64}$' -or
            -not $seen.Add([string]$entry.path)) { Stop-Unverified }
        $file = [IO.Path]::GetFullPath((Join-Path $stage $entry.path))
        if (-not $file.StartsWith($stage.TrimEnd('\', '/') + '\', [StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $file -PathType Leaf) -or
            (Get-Item -LiteralPath $file).Length -ne [Int64]$entry.length -or
            (Get-LowerHash $file) -cne [string]$entry.sha256) { Stop-Unverified }
    }
    $actualFiles = @(Get-ChildItem -LiteralPath $stage -File -Recurse | Where-Object {
        $_.FullName -cne $manifestPath
    })
    if ($actualFiles.Count -ne $seen.Count) { Stop-Unverified }

    $evidence = Get-Content -LiteralPath $evidenceFile -Raw | ConvertFrom-Json
    Assert-ExactProperties $evidence @(
        'schemaVersion', 'commitSha', 'protocolId', 'workerManifestDigest',
        'runtimeManifestDigest', 'sourceCommit', 'implementationCommit', 'modelSha256',
        'packageManifestSha256', 'modelLength',
        'requestedDevice', 'actualExecutionDevices', 'requestedKeyCodec',
        'requestedValueCodec', 'actualKeyCodec', 'actualValueCodec', 'attentionPath',
        'headDimension', 'runtimeDispatchCount', 'encodedRecordCount',
        'modelSdpaNodeCount',
        'packedBytesPerRecord', 'fullPrecisionBytesPerRecord', 'packedCacheBytes',
        'fullPrecisionCacheBytes', 'evidenceOrigin', 'forcedScalarNegative',
        'streamedFragmentCount', 'completedTurnCount', 'cleanupVerified')
    $expectedPackedBytes = if ($evidence.requestedKeyCodec -ceq 'tbq3') { 24 } else { 32 }
    if ($evidence.schemaVersion -ne 1 -or
        $evidence.commitSha -cne $ExpectedCommitSha -or
        $evidence.protocolId -cne 'openvino.turboquant/1' -or
        $evidence.workerManifestDigest -cne (Get-LowerHash $manifestPath) -or
        $evidence.runtimeManifestDigest -cne (Get-LowerHash $runtimeManifestPath) -or
        $evidence.sourceCommit -cne 'f5f594dc0c9e5961785f0d17743486d52eac87e7' -or
        $evidence.implementationCommit -cne 'b9a1f201c109e0bed74763934f79483cf6c4cbf4' -or
        $evidence.packageManifestSha256 -cne $ExpectedPackageManifestSha256 -or
        $evidence.modelSha256 -cne $ExpectedModelSha256 -or
        $ExpectedModelLength -le 0 -or
        $evidence.modelLength -ne $ExpectedModelLength -or
        $evidence.requestedDevice -cne 'CPU' -or
        @($evidence.actualExecutionDevices).Count -ne 1 -or
        $evidence.actualExecutionDevices[0] -cne 'CPU' -or
        @('tbq4', 'tbq3') -cnotcontains $evidence.requestedKeyCodec -or
        $evidence.requestedValueCodec -cne $evidence.requestedKeyCodec -or
        $evidence.actualKeyCodec -cne $evidence.requestedKeyCodec -or
        $evidence.actualValueCodec -cne $evidence.requestedKeyCodec -or
        $evidence.attentionPath -cne 'sdpa' -or
        $evidence.headDimension -ne 64 -or
        $evidence.runtimeDispatchCount -le 0 -or
        $evidence.encodedRecordCount -le 0 -or
        $evidence.modelSdpaNodeCount -le 0 -or
        $evidence.packedBytesPerRecord -ne $expectedPackedBytes -or
        $evidence.fullPrecisionBytesPerRecord -ne 128 -or
        $evidence.packedCacheBytes -ne ($evidence.encodedRecordCount * $evidence.packedBytesPerRecord) -or
        $evidence.fullPrecisionCacheBytes -ne ($evidence.encodedRecordCount * 128) -or
        $evidence.packedCacheBytes -ge $evidence.fullPrecisionCacheBytes -or
        $evidence.evidenceOrigin -cne 'openVinoProfilingApi' -or
        $evidence.forcedScalarNegative -ne $true -or
        $evidence.streamedFragmentCount -le 0 -or
        $evidence.completedTurnCount -ne 2 -or
        $evidence.cleanupVerified -ne $true) {
        Stop-Unverified
    }
    [Console]::Out.WriteLine('turboquant_active')
}
catch {
    Stop-Unverified
}
