[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$DestinationRoot,

    [string]$FixtureSpecPath
)

$ErrorActionPreference = 'Stop'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function Stop-Generation {
    [Console]::Out.WriteLine('fixture_generation_failed')
    exit 1
}

function Get-LowerSha256 {
    param([Parameter(Mandatory)][byte[]]$Bytes)

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Write-Utf8NoBom {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Text
    )

    [System.IO.File]::WriteAllText($Path, $Text.Replace("`r`n", "`n"), $utf8NoBom)
}

function Get-StaticTensorBytes {
    param([Parameter(Mandatory)][object[]]$Tensors)

    if (-not [BitConverter]::IsLittleEndian -or $Tensors.Count -ne 5) {
        Stop-Generation
    }

    $bytes = New-Object 'System.Collections.Generic.List[byte]'
    $names = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    foreach ($tensor in @($Tensors | Sort-Object { [Int64]$_.offset })) {
        if (-not $names.Add([string]$tensor.name) -or
            $bytes.Count -ne [Int64]$tensor.offset) {
            Stop-Generation
        }

        $valueCount = 1
        foreach ($dimension in @($tensor.shape)) {
            if ([Int64]$dimension -le 0) {
                Stop-Generation
            }
            $valueCount *= [Int64]$dimension
        }
        $values = @($tensor.values)
        if ($values.Count -ne $valueCount) {
            Stop-Generation
        }

        foreach ($value in $values) {
            $encoded = switch ([string]$tensor.elementType) {
                'i64' { [BitConverter]::GetBytes([Int64]$value); break }
                'f32' { [BitConverter]::GetBytes([Single]$value); break }
                default { Stop-Generation }
            }
            foreach ($byte in $encoded) {
                $bytes.Add($byte)
            }
        }
    }

    return $bytes.ToArray()
}

try {
    if ([string]::IsNullOrWhiteSpace($FixtureSpecPath)) {
        $FixtureSpecPath = Join-Path $PSScriptRoot '..\..\tests\TestFixtures\OpenVINO\GenAI\TinySyntheticV1\source\fixture-spec.json'
    }

    $specPath = [System.IO.Path]::GetFullPath($FixtureSpecPath)
    $destination = [System.IO.Path]::GetFullPath($DestinationRoot)
    $destinationRoot = [System.IO.Path]::GetPathRoot($destination)
    if ($destination.TrimEnd('\') -ceq $destinationRoot.TrimEnd('\') -or
        -not (Test-Path -LiteralPath $specPath -PathType Leaf)) {
        Stop-Generation
    }

    $specBytes = [System.IO.File]::ReadAllBytes($specPath)
    $spec = $utf8NoBom.GetString($specBytes) | ConvertFrom-Json
    if ($spec.schemaVersion -ne 1 -or
        $spec.fixtureId -cne 'TinySyntheticV1' -or
        $spec.license -cne 'MIT' -or
        $spec.model.architecture -cne 'GraniteForCausalLM' -or
        $spec.model.modelType -cne 'granite' -or
        [int]$spec.generation.maxNewTokens -gt 32 -or
        [int]$spec.generation.maxNewTokens -le 0) {
        Stop-Generation
    }

    if (Test-Path -LiteralPath $destination) {
        $existing = @(Get-ChildItem -LiteralPath $destination -Force)
        if ($existing.Count -gt 1 -or
            ($existing.Count -eq 1 -and $existing[0].Name -cne 'source')) {
            Stop-Generation
        }
    }
    else {
        [System.IO.Directory]::CreateDirectory($destination) | Out-Null
    }

    $sourceDirectory = Join-Path $destination 'source'
    $packageDirectory = Join-Path $destination 'package'
    if ((Test-Path -LiteralPath $packageDirectory) -or
        (Test-Path -LiteralPath (Join-Path $destination 'manifest.json')) -or
        (Test-Path -LiteralPath (Join-Path $destination 'LICENSE.txt'))) {
        Stop-Generation
    }

    [System.IO.Directory]::CreateDirectory($sourceDirectory) | Out-Null
    [System.IO.Directory]::CreateDirectory($packageDirectory) | Out-Null
    $destinationSpec = Join-Path $sourceDirectory 'fixture-spec.json'
    if ($destinationSpec -cne $specPath) {
        [System.IO.File]::WriteAllBytes($destinationSpec, $specBytes)
    }

    $expectedPaths = @(
        'config.json',
        'generation_config.json',
        'openvino_detokenizer.bin',
        'openvino_detokenizer.xml',
        'openvino_model.bin',
        'openvino_model.xml',
        'openvino_tokenizer.bin',
        'openvino_tokenizer.xml',
        'tokenizer_config.json'
    )
    $artifacts = @($spec.packageArtifacts)
    $actualPaths = @($artifacts | ForEach-Object { [string]$_.path } | Sort-Object -CaseSensitive)
    if ($actualPaths.Count -ne $expectedPaths.Count) {
        Stop-Generation
    }
    for ($index = 0; $index -lt $expectedPaths.Count; $index++) {
        if ($actualPaths[$index] -cne $expectedPaths[$index]) {
            Stop-Generation
        }
    }

    [byte[]]$sourceModelBytes = Get-StaticTensorBytes @($spec.model.sourceTensors)
    foreach ($artifact in $artifacts) {
        $path = [string]$artifact.path
        if ($path -match '[\\/]' -or $artifact.encoding -cne 'base64') {
            Stop-Generation
        }

        $artifactBytes = [Convert]::FromBase64String([string]$artifact.data)
        if ($artifactBytes.LongLength -ne [Int64]$artifact.length -or
            (Get-LowerSha256 $artifactBytes) -cne [string]$artifact.sha256) {
            Stop-Generation
        }
        $bytes = if ($path -ceq 'openvino_model.bin') { $sourceModelBytes } else { $artifactBytes }
        if ($bytes.LongLength -ne [Int64]$artifact.length -or
            (Get-LowerSha256 $bytes) -cne [string]$artifact.sha256) {
            Stop-Generation
        }
        [System.IO.File]::WriteAllBytes((Join-Path $packageDirectory $path), $bytes)
    }

    $manifestLines = New-Object 'System.Collections.Generic.List[string]'
    $manifestLines.Add('{')
    $manifestLines.Add('  "schemaVersion": 1,')
    $manifestLines.Add('  "fixtureId": "TinySyntheticV1",')
    $manifestLines.Add('  "license": "MIT",')
    $manifestLines.Add('  "files": [')
    for ($index = 0; $index -lt $expectedPaths.Count; $index++) {
        $name = $expectedPaths[$index]
        $bytes = [System.IO.File]::ReadAllBytes((Join-Path $packageDirectory $name))
        $comma = if ($index -lt $expectedPaths.Count - 1) { ',' } else { '' }
        $manifestLines.Add('    {')
        $manifestLines.Add(('      "path": "package/{0}",' -f $name))
        $manifestLines.Add(('      "length": {0},' -f $bytes.LongLength))
        $manifestLines.Add(('      "sha256": "{0}"' -f (Get-LowerSha256 $bytes)))
        $manifestLines.Add(('    }}{0}' -f $comma))
    }
    $manifestLines.Add('  ]')
    $manifestLines.Add('}')
    Write-Utf8NoBom (Join-Path $destination 'manifest.json') (($manifestLines -join "`n") + "`n")
    Write-Utf8NoBom (Join-Path $destination 'LICENSE.txt') ([string]$spec.licenseText)

    [Console]::Out.WriteLine('fixture_generated')
    exit 0
}
catch {
    Write-Verbose $_.Exception.ToString()
    Stop-Generation
}
