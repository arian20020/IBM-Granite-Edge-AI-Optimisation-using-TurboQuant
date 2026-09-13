[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$SourceRoot,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$GenAiSourceRoot,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$VerifiedRuntimeDirectory,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$OfficialDistributionDirectory,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$BuildDirectory,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$StageDirectory
)

$ErrorActionPreference = 'Stop'

function Stop-Build {
    [Console]::Out.WriteLine('turboquant_runtime_build_failed')
    exit 1
}

function Test-PathOverlap {
    param([string]$Left, [string]$Right)
    $leftRoot = $Left.TrimEnd('\', '/')
    $rightRoot = $Right.TrimEnd('\', '/')
    return $leftRoot -ieq $rightRoot -or
        $leftRoot.StartsWith($rightRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
        $rightRoot.StartsWith($leftRoot + '\', [StringComparison]::OrdinalIgnoreCase)
}

try {
    $repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
    $source = [IO.Path]::GetFullPath($SourceRoot)
    $genAiSource = [IO.Path]::GetFullPath($GenAiSourceRoot)
    $verifiedRuntime = [IO.Path]::GetFullPath($VerifiedRuntimeDirectory)
    $distribution = [IO.Path]::GetFullPath($OfficialDistributionDirectory)
    $buildRoot = [IO.Path]::GetFullPath($BuildDirectory)
    $stageRoot = [IO.Path]::GetFullPath($StageDirectory)
    $inputs = @($source, $genAiSource, $verifiedRuntime, $distribution)
    if (Test-PathOverlap $buildRoot $stageRoot) { Stop-Build }
    foreach ($owned in @($buildRoot, $stageRoot)) {
        if ($owned -ieq $repositoryRoot -or
            $owned.StartsWith($repositoryRoot.TrimEnd('\', '/') + '\', [StringComparison]::OrdinalIgnoreCase) -or
            (Test-Path -LiteralPath $owned)) { Stop-Build }
        foreach ($inputRoot in $inputs) {
            if (Test-PathOverlap $owned $inputRoot) { Stop-Build }
        }
    }

    $powershell = (Get-Command powershell.exe -ErrorAction Stop).Source
    $sourceCheck = & $powershell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
        -File (Join-Path $PSScriptRoot 'Test-OpenVinoTurboQuantPatchClosure.ps1') `
        -SourceRoot $source
    if ($LASTEXITCODE -ne 0 -or [string]$sourceCheck -cne 'turboquant_patch_closure_valid') { Stop-Build }
    if (-not (Test-Path -LiteralPath $distribution -PathType Container)) { Stop-Build }

    $git = (Get-Command git.exe -ErrorAction Stop).Source
    $genAiHead = & $git -C $genAiSource rev-parse HEAD
    $genAiStatus = @(& $git -C $genAiSource status --porcelain=v1 --untracked-files=all)
    if ($LASTEXITCODE -ne 0 -or
        [string]$genAiHead -cne '6fbc103538d30d42da4b0b5130a4792a20f728ba' -or
        $genAiStatus.Count -ne 0) { Stop-Build }

    $expectedRuntime = [ordered]@{
        'openvino.dll' = 'e7e5df583fea9cb16ccf190e54c7682622adbe000c0a3b42475230aba2755f22'
        'openvino_genai.dll' = '781f7f7dd908122e920f6814b409b2e21ce3f23db29a032985fa543799db4a3d'
        'openvino_tokenizers.dll' = 'b331316cadc56a584c431f1a5f699435b282bd1eefcadd5640208ec9e6f7ca5b'
        'openvino_intel_cpu_plugin.dll' = '6e0109de895b98407811a01ea3f8eca7424745d32eda3f23580c04c42445a18a'
        'openvino_ir_frontend.dll' = 'e38d897763f496cd11c5025b5151d000ade4c5a535b39aba7186e1e29d4e587f'
        'tbb12.dll' = 'a7a236bb095ac33f463a996d16ad7f5ee6bb9046bc151c667c2340061e5fc348'
        'tbbbind_2_5.dll' = '70225c2c43de3962bcc0dee8590a81317106ec1e4890e970fb107bfe1a6815cf'
    }
    foreach ($entry in $expectedRuntime.GetEnumerator()) {
        $path = Join-Path $verifiedRuntime $entry.Key
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or
            (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $entry.Value) { Stop-Build }
    }
    if ([Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $verifiedRuntime 'openvino.dll')).ProductVersion -cne
            '2026.5.0-22950-f5f594dc0c9' -or
        [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $verifiedRuntime 'openvino_genai.dll')).ProductVersion -cne
            '2026.5.0.0-3409-6fbc103538d' -or
        [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $verifiedRuntime 'openvino_tokenizers.dll')).ProductVersion -cne
            '2026.5.0.0-737-824033c3061') { Stop-Build }

    New-Item -ItemType Directory -Path $buildRoot | Out-Null
    New-Item -ItemType Directory -Path $stageRoot | Out-Null
    foreach ($name in $expectedRuntime.Keys) {
        Copy-Item -LiteralPath (Join-Path $verifiedRuntime $name) -Destination (Join-Path $stageRoot $name)
    }

    $licenseMap = [ordered]@{
        (Join-Path $source 'LICENSE') = 'licenses/openvino-Apache-2.0.txt'
        (Join-Path $source 'licensing/third-party-programs.txt') = 'licenses/openvino-third-party-programs.txt'
        (Join-Path $genAiSource 'LICENSE') = 'licenses/openvino-genai-Apache-2.0.txt'
        (Join-Path $genAiSource 'third-party-programs.txt') = 'licenses/openvino-genai-third-party-programs.txt'
        (Join-Path $distribution 'docs/openvino_tokenizers/LICENSE') = 'licenses/openvino-tokenizers-LICENSE.txt'
        (Join-Path $distribution 'docs/openvino_tokenizers/third-party-programs.txt') = 'licenses/openvino-tokenizers-third-party-programs.txt'
        (Join-Path $distribution 'runtime/3rdparty/tbb/TBB-LICENSE') = 'licenses/TBB-LICENSE.txt'
    }
    foreach ($entry in $licenseMap.GetEnumerator()) {
        $destination = Join-Path $stageRoot $entry.Value
        New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
        Copy-Item -LiteralPath $entry.Key -Destination $destination
    }
    Copy-Item (Join-Path $repositoryRoot 'third-party/openvino-turboquant/upstream.lock.json') `
        (Join-Path $stageRoot 'source-manifest.json')
    Copy-Item (Join-Path $repositoryRoot 'third-party/openvino-turboquant/patches/series.json') `
        (Join-Path $stageRoot 'patch-manifest.json')
    Copy-Item (Join-Path $repositoryRoot 'third-party/openvino-turboquant/LICENSES.md') `
        (Join-Path $stageRoot 'licenses/TurboQuant-LICENSES.md')

    $files = @(Get-ChildItem $stageRoot -File -Recurse | Sort-Object FullName | ForEach-Object {
        $relative = $_.FullName.Substring($stageRoot.TrimEnd('\', '/').Length + 1).Replace('\', '/')
        $kind = if ($relative.StartsWith('licenses/', [StringComparison]::Ordinal)) { 'license' }
            elseif ($relative.EndsWith('manifest.json', [StringComparison]::Ordinal)) { 'source-ledger' }
            else { 'runtime-binary' }
        [ordered]@{
            path = $relative
            length = $_.Length
            sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            kind = $kind
        }
    })
    $manifest = [ordered]@{
        schemaVersion = 1
        component = 'openvino-turboquant-runtime'
        platform = 'windows-x86_64'
        configuration = 'Release'
        sourceCommit = 'f5f594dc0c9e5961785f0d17743486d52eac87e7'
        implementationCommit = 'b9a1f201c109e0bed74763934f79483cf6c4cbf4'
        genAiCommit = '6fbc103538d30d42da4b0b5130a4792a20f728ba'
        acceptedTuple = [ordered]@{ codec='TBQ4/TBQ3'; device='CPU'; attention='SDPA'; headDimension=64 }
        files = $files
    }
    [IO.File]::WriteAllText((Join-Path $stageRoot 'turboquant-runtime.manifest.json'),
        ($manifest | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    $stageCheck = & $powershell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
        -File (Join-Path $PSScriptRoot 'Test-OpenVinoTurboQuantPatchClosure.ps1') `
        -SourceRoot $source -StageDirectory $stageRoot
    if ($LASTEXITCODE -ne 0 -or [string]$stageCheck -cne 'turboquant_patch_closure_valid') { Stop-Build }
    [Console]::Out.WriteLine('turboquant_runtime_built')
}
catch { Stop-Build }
