[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$SourceRoot,

    [string]$StageDirectory = ''
)

$ErrorActionPreference = 'Stop'

function Stop-Verification {
    [Console]::Out.WriteLine('turboquant_patch_closure_invalid')
    exit 1
}

function Assert-ExactProperties {
    param([object]$Value, [string[]]$Names)
    $actual = @($Value.PSObject.Properties.Name | Sort-Object)
    $expected = @($Names | Sort-Object)
    if (($actual -join "`n") -cne ($expected -join "`n")) {
        throw 'Unexpected JSON properties.'
    }
}

function Get-LowerHash {
    param([string]$LiteralPath)
    return (Get-FileHash -LiteralPath $LiteralPath -Algorithm SHA256).Hash.ToLowerInvariant()
}

try {
    $repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
    $source = [IO.Path]::GetFullPath($SourceRoot)
    if (-not (Test-Path -LiteralPath $source -PathType Container) -or
        ((Get-Item -LiteralPath $source -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        Stop-Verification
    }

    $lockPath = Join-Path $repositoryRoot 'third-party\openvino-turboquant\upstream.lock.json'
    $seriesPath = Join-Path $repositoryRoot 'third-party\openvino-turboquant\patches\series.json'
    $licensesPath = Join-Path $repositoryRoot 'third-party\openvino-turboquant\LICENSES.md'
    foreach ($path in @($lockPath, $seriesPath, $licensesPath)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { Stop-Verification }
    }

    $lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
    Assert-ExactProperties $lock @(
        'schemaVersion', 'component', 'implementationRoute', 'repository',
        'releaseTag', 'releaseCommit', 'implementationCommit', 'pullRequest',
        'pullRequestUrl', 'compatibleGenAi', 'acceptedModel',
        'acceptedHeadDimension', 'acceptedDevice', 'acceptedAttentionPath',
        'acceptedKeyCodec', 'acceptedValueCodec', 'runtimeProperties',
        'excludedCapabilities', 'sourceFiles', 'license', 'sourceDisposition',
        'discoveredAtUtc')
    if ($lock.schemaVersion -ne 1 -or
        $lock.implementationRoute -cne 'recovered-upstream' -or
        $lock.repository -cne 'https://github.com/openvinotoolkit/openvino.git' -or
        $lock.releaseTag -cne '2026.3.0' -or
        $lock.releaseCommit -cne '8a17657b995fd3b4a52f8484acfcf2bb61214623' -or
        $lock.implementationCommit -cne 'b9a1f201c109e0bed74763934f79483cf6c4cbf4' -or
        $lock.pullRequest -ne 35853 -or
        $lock.compatibleGenAi.releaseCommit -cne 'bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0' -or
        $lock.acceptedHeadDimension -ne 64 -or
        $lock.acceptedDevice -cne 'CPU' -or
        $lock.acceptedAttentionPath -cne 'SDPA' -or
        $lock.acceptedKeyCodec -cne 'TBQ4' -or
        $lock.acceptedValueCodec -cne 'TBQ4' -or
        $lock.license -cne 'Apache-2.0' -or
        $lock.sourceDisposition -cne 'exact released upstream source; no project patch applied') {
        Stop-Verification
    }

    Assert-ExactProperties $lock.compatibleGenAi @(
        'repository', 'releaseTag', 'releaseCommit')
    if ($lock.compatibleGenAi.repository -cne 'https://github.com/openvinotoolkit/openvino.genai.git' -or
        $lock.compatibleGenAi.releaseTag -cne '2026.3.0.0') {
        Stop-Verification
    }

    Assert-ExactProperties $lock.runtimeProperties @(
        'KEY_CACHE_QUANT_ALG', 'VALUE_CACHE_QUANT_ALG',
        'KEY_CACHE_PRECISION', 'VALUE_CACHE_PRECISION')
    if ($lock.runtimeProperties.KEY_CACHE_QUANT_ALG -cne 'TURBO' -or
        $lock.runtimeProperties.VALUE_CACHE_QUANT_ALG -cne 'TURBO' -or
        $lock.runtimeProperties.KEY_CACHE_PRECISION -cne 'u4' -or
        $lock.runtimeProperties.VALUE_CACHE_PRECISION -cne 'u4') {
        Stop-Verification
    }

    $git = (Get-Command git.exe -ErrorAction Stop).Source
    $head = (& $git -C $source rev-parse HEAD 2>$null)
    if ($LASTEXITCODE -ne 0 -or [string]$head -cne $lock.releaseCommit) { Stop-Verification }
    $status = @(& $git -C $source status --porcelain=v1 --untracked-files=all 2>$null)
    if ($LASTEXITCODE -ne 0 -or $status.Count -ne 0) { Stop-Verification }
    & $git -C $source merge-base --is-ancestor $lock.implementationCommit $lock.releaseCommit 2>$null
    if ($LASTEXITCODE -ne 0) { Stop-Verification }

    if (@($lock.sourceFiles).Count -lt 8) { Stop-Verification }
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($entry in $lock.sourceFiles) {
        Assert-ExactProperties $entry @('path', 'length', 'sha256')
        if ($entry.path -cnotmatch '^[a-zA-Z0-9_.\-/]+$' -or
            $entry.path.Contains('..') -or
            $entry.length -le 0 -or
            $entry.sha256 -cnotmatch '^[0-9a-f]{64}$' -or
            -not $seen.Add([string]$entry.path)) {
            Stop-Verification
        }
        $candidate = [IO.Path]::GetFullPath((Join-Path $source $entry.path))
        if (-not $candidate.StartsWith($source.TrimEnd('\', '/') + '\', [StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $candidate -PathType Leaf) -or
            ((Get-Item -LiteralPath $candidate -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -or
            (Get-Item -LiteralPath $candidate).Length -ne $entry.length -or
            (Get-LowerHash $candidate) -cne $entry.sha256) {
            Stop-Verification
        }
    }

    $series = Get-Content -LiteralPath $seriesPath -Raw | ConvertFrom-Json
    Assert-ExactProperties $series @(
        'schemaVersion', 'route', 'baseCommit', 'implementationCommit',
        'patches', 'reason', 'securityReview', 'licenseReview',
        'packageRegistrationAllowed')
    if ($series.schemaVersion -ne 1 -or
        $series.route -cne 'recovered-upstream' -or
        $series.baseCommit -cne $lock.releaseCommit -or
        $series.implementationCommit -cne $lock.implementationCommit -or
        @($series.patches).Count -ne 0 -or
        $series.securityReview -cne 'pending-external-review' -or
        $series.licenseReview -cne 'pending-external-review' -or
        $series.packageRegistrationAllowed -ne $false) {
        Stop-Verification
    }

    if (-not [string]::IsNullOrWhiteSpace($StageDirectory)) {
        $stage = [IO.Path]::GetFullPath($StageDirectory)
        if (-not (Test-Path -LiteralPath $stage -PathType Container) -or
            ((Get-Item -LiteralPath $stage -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            Stop-Verification
        }
        $manifestPath = Join-Path $stage 'turboquant-runtime.manifest.json'
        if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { Stop-Verification }
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        Assert-ExactProperties $manifest @(
            'schemaVersion', 'component', 'platform', 'configuration',
            'sourceCommit', 'implementationCommit', 'genAiCommit',
            'acceptedTuple', 'files')
        if ($manifest.schemaVersion -ne 1 -or
            $manifest.component -cne 'openvino-turboquant-runtime' -or
            $manifest.platform -cne 'windows-x86_64' -or
            $manifest.configuration -cne 'Release' -or
            $manifest.sourceCommit -cne $lock.releaseCommit -or
            $manifest.implementationCommit -cne $lock.implementationCommit -or
            $manifest.genAiCommit -cne $lock.compatibleGenAi.releaseCommit) {
            Stop-Verification
        }
        Assert-ExactProperties $manifest.acceptedTuple @(
            'codec', 'device', 'attention', 'headDimension')
        if ($manifest.acceptedTuple.codec -cne 'TBQ4/TBQ4' -or
            $manifest.acceptedTuple.device -cne 'CPU' -or
            $manifest.acceptedTuple.attention -cne 'SDPA' -or
            $manifest.acceptedTuple.headDimension -ne 64) {
            Stop-Verification
        }
        $manifestFiles = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
        $allowedKinds = [Collections.Generic.HashSet[string]]::new(
            [string[]]@('license', 'conformance-test', 'source-ledger', 'runtime-binary'),
            [StringComparer]::Ordinal)
        foreach ($entry in $manifest.files) {
            Assert-ExactProperties $entry @('path', 'length', 'sha256', 'kind')
            if ($entry.path -cnotmatch '^[a-zA-Z0-9_.\-/]+$' -or
                $entry.path.Contains('..') -or
                $entry.length -le 0 -or
                $entry.sha256 -cnotmatch '^[0-9a-f]{64}$' -or
                -not $allowedKinds.Contains([string]$entry.kind) -or
                $manifestFiles.ContainsKey([string]$entry.path)) {
                Stop-Verification
            }
            $manifestFiles.Add([string]$entry.path, $entry)
        }
        $actualFiles = @(Get-ChildItem -LiteralPath $stage -File -Recurse | Where-Object {
            $_.FullName -cne $manifestPath
        })
        if ($actualFiles.Count -ne $manifestFiles.Count) { Stop-Verification }
        foreach ($file in $actualFiles) {
            $relative = $file.FullName.Substring($stage.TrimEnd('\', '/').Length + 1).Replace('\', '/')
            if (-not $manifestFiles.ContainsKey($relative)) { Stop-Verification }
            $entry = $manifestFiles[$relative]
            if ($file.Length -ne $entry.length -or (Get-LowerHash $file.FullName) -cne $entry.sha256) {
                Stop-Verification
            }
        }
    }

    [Console]::Out.WriteLine('turboquant_patch_closure_valid')
}
catch {
    Stop-Verification
}
