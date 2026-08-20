[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$FixtureRoot
)

$ErrorActionPreference = 'Stop'

function Stop-Invalid {
    [Console]::Out.WriteLine('fixture_invalid')
    exit 1
}

function Get-LowerSha256 {
    param([Parameter(Mandatory)][string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-LowerSha256Bytes {
    param([Parameter(Mandatory)][byte[]]$Bytes)

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Get-ControlledRelativePath {
    param(
        [Parameter(Mandatory)][string]$FullPath,
        [Parameter(Mandatory)][string]$DirectoryPath
    )

    $directoryPrefix = $DirectoryPath.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar
    $canonicalPath = [System.IO.Path]::GetFullPath($FullPath)
    if (-not $canonicalPath.StartsWith(
            $directoryPrefix,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'A fixture path escaped the controlled fixture root.'
    }

    return $canonicalPath.Substring($directoryPrefix.Length).Replace(
        [System.IO.Path]::DirectorySeparatorChar,
        '/')
}

function Test-XmlModel {
    param(
        [Parameter(Mandatory)][string]$XmlPath,
        [Parameter(Mandatory)][string]$BinPath,
        [Parameter(Mandatory)][string[]]$RequiredLayerTypes
    )

    $settings = New-Object System.Xml.XmlReaderSettings
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [System.Xml.XmlReader]::Create($XmlPath, $settings)
    $document = New-Object System.Xml.XmlDocument
    $document.XmlResolver = $null
    try {
        $document.Load($reader)
    }
    finally {
        $reader.Dispose()
    }

    if ($document.DocumentElement.LocalName -cne 'net' -or
        [string]::IsNullOrWhiteSpace($document.DocumentElement.GetAttribute('name'))) {
        return $false
    }

    $types = @($document.SelectNodes('/net/layers/layer') |
        ForEach-Object { $_.GetAttribute('type') })
    foreach ($required in $RequiredLayerTypes) {
        if ($types -cnotcontains $required) {
            return $false
        }
    }

    $binLength = (Get-Item -LiteralPath $BinPath).Length
    if ($binLength -le 0) {
        return $false
    }
    foreach ($constant in @($document.SelectNodes('/net/layers/layer[@type="Const"]/data'))) {
        $offset = [Int64]$constant.GetAttribute('offset')
        $size = [Int64]$constant.GetAttribute('size')
        if ($offset -lt 0 -or $size -lt 0 -or $offset + $size -gt $binLength) {
            return $false
        }
    }

    return $true
}

try {
    $root = [System.IO.Path]::GetFullPath($FixtureRoot)
    if (-not (Test-Path -LiteralPath $root -PathType Container)) {
        Stop-Invalid
    }

    $expectedFiles = @(
        'LICENSE.txt',
        'manifest.json',
        'package/config.json',
        'package/generation_config.json',
        'package/openvino_detokenizer.bin',
        'package/openvino_detokenizer.xml',
        'package/openvino_model.bin',
        'package/openvino_model.xml',
        'package/openvino_tokenizer.bin',
        'package/openvino_tokenizer.xml',
        'package/tokenizer_config.json',
        'source/fixture-spec.json'
    )
    $expectedDirectories = @('package', 'source')

    $items = @(Get-Item -LiteralPath $root) + @(Get-ChildItem -LiteralPath $root -Force -Recurse)
    if (@($items | Where-Object { ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 }).Count -ne 0) {
        Stop-Invalid
    }

    $actualFiles = @(Get-ChildItem -LiteralPath $root -Force -File -Recurse |
        ForEach-Object { Get-ControlledRelativePath $_.FullName $root } |
        Sort-Object -CaseSensitive)
    $actualDirectories = @(Get-ChildItem -LiteralPath $root -Force -Directory -Recurse |
        ForEach-Object { Get-ControlledRelativePath $_.FullName $root } |
        Sort-Object -CaseSensitive)
    if ($actualFiles.Count -ne $expectedFiles.Count -or
        $actualDirectories.Count -ne $expectedDirectories.Count) {
        Stop-Invalid
    }
    for ($index = 0; $index -lt $expectedFiles.Count; $index++) {
        if ($actualFiles[$index] -cne $expectedFiles[$index]) {
            Stop-Invalid
        }
    }
    for ($index = 0; $index -lt $expectedDirectories.Count; $index++) {
        if ($actualDirectories[$index] -cne $expectedDirectories[$index]) {
            Stop-Invalid
        }
    }

    $isWindowsPlatform = [System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT
    foreach ($file in Get-ChildItem -LiteralPath $root -Force -File -Recurse) {
        if ($file.Length -le 0) {
            Stop-Invalid
        }
        if ($isWindowsPlatform) {
            $streams = @(Get-Item -LiteralPath $file.FullName -Stream * -ErrorAction Stop)
            if (@($streams | Where-Object { $_.Stream -cne ':$DATA' }).Count -ne 0) {
                Stop-Invalid
            }
        }
    }

    $sourceSpecPath = Join-Path $root 'source\fixture-spec.json'
    $sourceSpec = Get-Content -LiteralPath $sourceSpecPath -Raw | ConvertFrom-Json
    if ($sourceSpec.schemaVersion -ne 1 -or
        $sourceSpec.fixtureId -cne 'TinySyntheticV1' -or
        $sourceSpec.license -cne 'MIT' -or
        $sourceSpec.generation.offline -ne $true -or
        $sourceSpec.generation.deterministic -ne $true -or
        $sourceSpec.generation.productConverterClosureUsed -ne $false -or
        $sourceSpec.generation.productConverterGate -cne 'DEP-02 remains unresolved' -or
        [int]$sourceSpec.generation.maxNewTokens -le 0 -or
        [int]$sourceSpec.generation.maxNewTokens -gt 32 -or
        $sourceSpec.model.architecture -cne 'GraniteForCausalLM' -or
        $sourceSpec.model.modelType -cne 'granite' -or
        $sourceSpec.model.dense -ne $true -or
        [int]$sourceSpec.model.vocabSize -ne 8 -or
        @($sourceSpec.model.sourceTensors).Count -ne 5 -or
        @($sourceSpec.tokenizer.vocabulary).Count -ne 8 -or
        [int]$sourceSpec.tokenizer.eosTokenId -ne 2 -or
        [int]$sourceSpec.tokenizer.fixtureTokenId -ne 3) {
        Stop-Invalid
    }

    $manifest = Get-Content -LiteralPath (Join-Path $root 'manifest.json') -Raw | ConvertFrom-Json
    if ($manifest.schemaVersion -ne 1 -or
        $manifest.fixtureId -cne 'TinySyntheticV1' -or
        $manifest.license -cne 'MIT') {
        Stop-Invalid
    }

    $expectedPackagePaths = @($expectedFiles | Where-Object { $_.StartsWith('package/', [StringComparison]::Ordinal) })
    $entries = @($manifest.files)
    if ($entries.Count -ne $expectedPackagePaths.Count) {
        Stop-Invalid
    }
    for ($index = 0; $index -lt $entries.Count; $index++) {
        $entry = $entries[$index]
        $path = [string]$entry.path
        if ($path -cne $expectedPackagePaths[$index] -or
            [string]$entry.sha256 -cnotmatch '^[0-9a-f]{64}$' -or
            [Int64]$entry.length -le 0) {
            Stop-Invalid
        }
        $fullPath = Join-Path $root $path.Replace('/', '\')
        $file = Get-Item -LiteralPath $fullPath
        if ($file.Length -ne [Int64]$entry.length -or
            (Get-LowerSha256 $fullPath) -cne [string]$entry.sha256) {
            Stop-Invalid
        }
    }

    $sourceArtifacts = @($sourceSpec.packageArtifacts)
    if ($sourceArtifacts.Count -ne $expectedPackagePaths.Count) {
        Stop-Invalid
    }
    for ($index = 0; $index -lt $sourceArtifacts.Count; $index++) {
        $sourceArtifact = $sourceArtifacts[$index]
        $expectedArtifactPath = $expectedPackagePaths[$index].Substring('package/'.Length)
        if ([string]$sourceArtifact.path -cne $expectedArtifactPath -or
            $sourceArtifact.encoding -cne 'base64' -or
            [string]$sourceArtifact.sha256 -cnotmatch '^[0-9a-f]{64}$' -or
            [Int64]$sourceArtifact.length -le 0) {
            Stop-Invalid
        }

        $sourceBytes = [Convert]::FromBase64String([string]$sourceArtifact.data)
        if ($sourceBytes.LongLength -ne [Int64]$sourceArtifact.length -or
            (Get-LowerSha256Bytes $sourceBytes) -cne [string]$sourceArtifact.sha256 -or
            [Int64]$sourceArtifact.length -ne [Int64]$entries[$index].length -or
            [string]$sourceArtifact.sha256 -cne [string]$entries[$index].sha256) {
            Stop-Invalid
        }
    }

    $licenseLines = @(Get-Content -LiteralPath (Join-Path $root 'LICENSE.txt'))
    if ($licenseLines.Count -lt 5 -or
        $licenseLines[0] -cne 'SPDX-License-Identifier: MIT' -or
        @($licenseLines | Where-Object { $_ -match '(?i)\b(TBD|N/A|unverified|unknown|pending)\b' }).Count -ne 0) {
        Stop-Invalid
    }
    if ([System.IO.File]::ReadAllText((Join-Path $root 'LICENSE.txt')) -cne [string]$sourceSpec.licenseText) {
        Stop-Invalid
    }

    $package = Join-Path $root 'package'
    $config = Get-Content -LiteralPath (Join-Path $package 'config.json') -Raw | ConvertFrom-Json
    $generation = Get-Content -LiteralPath (Join-Path $package 'generation_config.json') -Raw | ConvertFrom-Json
    $tokenizer = Get-Content -LiteralPath (Join-Path $package 'tokenizer_config.json') -Raw | ConvertFrom-Json
    if ($config.model_type -cne 'granite' -or
        @($config.architectures).Count -ne 1 -or
        $config.architectures[0] -cne 'GraniteForCausalLM' -or
        [int]$config.vocab_size -ne 8 -or
        [int]$config.max_position_embeddings -ne 64 -or
        [int]$generation.max_new_tokens -le 0 -or
        [int]$generation.max_new_tokens -gt 32 -or
        $generation.do_sample -ne $false -or
        [int]$generation.eos_token_id -ne 2 -or
        [int]$tokenizer.model_max_length -ne 64) {
        Stop-Invalid
    }

    $textFiles = @('config.json', 'generation_config.json', 'tokenizer_config.json', 'openvino_model.xml', 'openvino_tokenizer.xml', 'openvino_detokenizer.xml')
    foreach ($name in $textFiles) {
        $text = Get-Content -LiteralPath (Join-Path $package $name) -Raw
        if ($text -match '(?i)(https?://|file://|[A-Z]:[\\/]|\.\.[\\/]|api[_-]?key|access[_-]?token|password|credential|authorization)') {
            Stop-Invalid
        }
    }

    if (-not (Test-XmlModel (Join-Path $package 'openvino_model.xml') (Join-Path $package 'openvino_model.bin') @('Parameter', 'Select', 'Result')) -or
        -not (Test-XmlModel (Join-Path $package 'openvino_tokenizer.xml') (Join-Path $package 'openvino_tokenizer.bin') @('Parameter', 'StringTensorUnpack', 'Result')) -or
        -not (Test-XmlModel (Join-Path $package 'openvino_detokenizer.xml') (Join-Path $package 'openvino_detokenizer.bin') @('Parameter', 'VocabDecoder', 'Result'))) {
        Stop-Invalid
    }

    [Console]::Out.WriteLine('fixture_valid')
    exit 0
}
catch {
    Write-Verbose $_.Exception.ToString()
    Stop-Invalid
}
