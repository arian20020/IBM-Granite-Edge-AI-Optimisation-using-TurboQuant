[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$FixtureRoot
)

$ErrorActionPreference = 'Stop'
$expectedSourceSpecLength = 66339L
$expectedSourceSpecSha256 = '5b35c5a754398067e56912e99847821842309945de9f23f4ab251ca43f161402'
$expectedLicenseLength = 1100L
$expectedLicenseSha256 = '3eaf75208e7fcc688aed39dce511cce3350557776ca4cba1dc251ef579b32652'

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

function Get-StaticTensorBytes {
    param([Parameter(Mandatory)][object[]]$Tensors)

    if (-not [BitConverter]::IsLittleEndian -or $Tensors.Count -ne 5) {
        throw 'The reviewed tensor set is invalid.'
    }

    $bytes = New-Object 'System.Collections.Generic.List[byte]'
    $names = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    foreach ($tensor in @($Tensors | Sort-Object { [Int64]$_.offset })) {
        if (-not $names.Add([string]$tensor.name) -or
            $bytes.Count -ne [Int64]$tensor.offset) {
            throw 'The reviewed tensor layout is invalid.'
        }

        $valueCount = 1L
        foreach ($dimension in @($tensor.shape)) {
            if ([Int64]$dimension -le 0) {
                throw 'The reviewed tensor shape is invalid.'
            }
            $valueCount *= [Int64]$dimension
        }
        $values = @($tensor.values)
        if ($values.Count -ne $valueCount) {
            throw 'The reviewed tensor value count is invalid.'
        }

        foreach ($value in $values) {
            $encoded = switch ([string]$tensor.elementType) {
                'i64' { [BitConverter]::GetBytes([Int64]$value); break }
                'f32' { [BitConverter]::GetBytes([Single]$value); break }
                default { throw 'The reviewed tensor element type is invalid.' }
            }
            foreach ($byte in $encoded) {
                $bytes.Add($byte)
            }
        }
    }

    return $bytes.ToArray()
}

function Test-ForbiddenText {
    param([AllowEmptyString()][string]$Text)

    return $Text -match '(?i)(https?://|file://|(?<![A-Z0-9])[A-Z]:[\\/]|\.\.[\\/]|api[_-]?key|access[_-]?token|password|credential|authorization)'
}

function Test-ForbiddenDecodedValue {
    param([AllowNull()][object]$Value)

    if ($null -eq $Value) {
        return $false
    }
    if ($Value -is [string]) {
        return Test-ForbiddenText $Value
    }
    if ($Value -is [System.Management.Automation.PSCustomObject]) {
        foreach ($property in $Value.PSObject.Properties) {
            if (Test-ForbiddenDecodedValue $property.Value) {
                return $true
            }
        }
        return $false
    }
    if ($Value -is [System.Collections.IEnumerable]) {
        foreach ($item in $Value) {
            if (Test-ForbiddenDecodedValue $item) {
                return $true
            }
        }
    }
    return $false
}

function Read-StrictJsonDocument {
    param([Parameter(Mandatory)][string]$Path)

    Add-Type -AssemblyName System.Runtime.Serialization
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $quotas = New-Object System.Xml.XmlDictionaryReaderQuotas
    $quotas.MaxDepth = 64
    $quotas.MaxStringContentLength = 1048576
    $quotas.MaxArrayLength = 1048576
    $quotas.MaxBytesPerRead = 4096
    $quotas.MaxNameTableCharCount = 16384
    $reader = [System.Runtime.Serialization.Json.JsonReaderWriterFactory]::CreateJsonReader(
        $bytes,
        0,
        $bytes.Length,
        [System.Text.Encoding]::UTF8,
        $quotas,
        $null)
    $document = New-Object System.Xml.XmlDocument
    $document.XmlResolver = $null
    try {
        $document.Load($reader)
    }
    finally {
        $reader.Dispose()
    }
    return $document
}

function Test-JsonObjectSchema {
    param(
        [AllowNull()][System.Xml.XmlElement]$Element,
        [Parameter(Mandatory)][string[]]$Properties
    )

    if ($null -eq $Element -or $Element.GetAttribute('type') -cne 'object') {
        return $false
    }
    $children = @($Element.ChildNodes | Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element })
    if ($children.Count -ne $Properties.Count) {
        return $false
    }
    foreach ($property in $Properties) {
        if (@($children | Where-Object { $_.LocalName -ceq $property }).Count -ne 1) {
            return $false
        }
    }
    return $true
}

function Test-JsonPropertyType {
    param(
        [Parameter(Mandatory)][System.Xml.XmlElement]$Element,
        [Parameter(Mandatory)][string]$Property,
        [Parameter(Mandatory)][string]$Type
    )

    $child = $Element.SelectSingleNode($Property)
    return $null -ne $child -and $child.GetAttribute('type') -ceq $Type
}

function Test-JsonArray {
    param(
        [AllowNull()][System.Xml.XmlElement]$Element,
        [Parameter(Mandatory)][string]$ItemType
    )

    if ($null -eq $Element -or $Element.GetAttribute('type') -cne 'array') {
        return $false
    }
    foreach ($item in @($Element.ChildNodes)) {
        if ($item.NodeType -ne [System.Xml.XmlNodeType]::Element -or
            $item.LocalName -cne 'item' -or
            $item.GetAttribute('type') -cne $ItemType) {
            return $false
        }
    }
    return $true
}

function Test-SourceJsonSchema {
    param([Parameter(Mandatory)][System.Xml.XmlDocument]$Document)

    $root = $Document.DocumentElement
    if (-not (Test-JsonObjectSchema $root @('schemaVersion', 'fixtureId', 'purpose', 'license', 'generation', 'model', 'tokenizer', 'toolchain', 'packageArtifacts', 'licenseText'))) {
        return $false
    }
    foreach ($property in @('schemaVersion')) {
        if (-not (Test-JsonPropertyType $root $property 'number')) { return $false }
    }
    foreach ($property in @('fixtureId', 'purpose', 'license', 'licenseText')) {
        if (-not (Test-JsonPropertyType $root $property 'string')) { return $false }
    }

    $generation = $root.SelectSingleNode('generation')
    if (-not (Test-JsonObjectSchema $generation @('offline', 'deterministic', 'maxNewTokens', 'expectedText', 'productConverterClosureUsed', 'productConverterGate'))) {
        return $false
    }
    foreach ($property in @('offline', 'deterministic', 'productConverterClosureUsed')) {
        if (-not (Test-JsonPropertyType $generation $property 'boolean')) { return $false }
    }
    if (-not (Test-JsonPropertyType $generation 'maxNewTokens' 'number')) { return $false }
    foreach ($property in @('expectedText', 'productConverterGate')) {
        if (-not (Test-JsonPropertyType $generation $property 'string')) { return $false }
    }

    $model = $root.SelectSingleNode('model')
    if (-not (Test-JsonObjectSchema $model @('architecture', 'modelType', 'task', 'dense', 'vocabSize', 'maxPositionEmbeddings', 'inputs', 'output', 'sourceTensors'))) {
        return $false
    }
    foreach ($property in @('architecture', 'modelType', 'task')) {
        if (-not (Test-JsonPropertyType $model $property 'string')) { return $false }
    }
    if (-not (Test-JsonPropertyType $model 'dense' 'boolean')) { return $false }
    foreach ($property in @('vocabSize', 'maxPositionEmbeddings')) {
        if (-not (Test-JsonPropertyType $model $property 'number')) { return $false }
    }
    $inputs = $model.SelectSingleNode('inputs')
    if (-not (Test-JsonArray $inputs 'object')) { return $false }
    foreach ($item in @($inputs.ChildNodes)) {
        if (-not (Test-JsonObjectSchema $item @('name', 'type', 'shape'))) { return $false }
        foreach ($property in @('name', 'type', 'shape')) {
            if (-not (Test-JsonPropertyType $item $property 'string')) { return $false }
        }
    }
    $output = $model.SelectSingleNode('output')
    if (-not (Test-JsonObjectSchema $output @('name', 'type', 'shape', 'policy'))) { return $false }
    foreach ($property in @('name', 'type', 'shape', 'policy')) {
        if (-not (Test-JsonPropertyType $output $property 'string')) { return $false }
    }
    $sourceTensors = $model.SelectSingleNode('sourceTensors')
    if (-not (Test-JsonArray $sourceTensors 'object')) { return $false }
    foreach ($item in @($sourceTensors.ChildNodes)) {
        if (-not (Test-JsonObjectSchema $item @('name', 'elementType', 'shape', 'values', 'offset')) -or
            -not (Test-JsonPropertyType $item 'name' 'string') -or
            -not (Test-JsonPropertyType $item 'elementType' 'string') -or
            -not (Test-JsonArray $item.SelectSingleNode('shape') 'number') -or
            -not (Test-JsonArray $item.SelectSingleNode('values') 'number') -or
            -not (Test-JsonPropertyType $item 'offset' 'number')) {
            return $false
        }
    }

    $tokenizer = $root.SelectSingleNode('tokenizer')
    if (-not (Test-JsonObjectSchema $tokenizer @('implementation', 'preTokenizer', 'decoder', 'vocabulary', 'padTokenId', 'bosTokenId', 'eosTokenId', 'fixtureTokenId', 'unknownTokenId'))) {
        return $false
    }
    foreach ($property in @('implementation', 'preTokenizer', 'decoder')) {
        if (-not (Test-JsonPropertyType $tokenizer $property 'string')) { return $false }
    }
    if (-not (Test-JsonArray $tokenizer.SelectSingleNode('vocabulary') 'string')) { return $false }
    foreach ($property in @('padTokenId', 'bosTokenId', 'eosTokenId', 'fixtureTokenId', 'unknownTokenId')) {
        if (-not (Test-JsonPropertyType $tokenizer $property 'number')) { return $false }
    }

    $toolchain = $root.SelectSingleNode('toolchain')
    if (-not (Test-JsonArray $toolchain 'object')) { return $false }
    foreach ($item in @($toolchain.ChildNodes)) {
        if (-not (Test-JsonObjectSchema $item @('component', 'version', 'length', 'sha256')) -or
            -not (Test-JsonPropertyType $item 'component' 'string') -or
            -not (Test-JsonPropertyType $item 'version' 'string') -or
            -not (Test-JsonPropertyType $item 'length' 'number') -or
            -not (Test-JsonPropertyType $item 'sha256' 'string')) {
            return $false
        }
    }
    $artifacts = $root.SelectSingleNode('packageArtifacts')
    if (-not (Test-JsonArray $artifacts 'object')) { return $false }
    foreach ($item in @($artifacts.ChildNodes)) {
        if (-not (Test-JsonObjectSchema $item @('path', 'length', 'sha256', 'encoding', 'data')) -or
            -not (Test-JsonPropertyType $item 'path' 'string') -or
            -not (Test-JsonPropertyType $item 'length' 'number') -or
            -not (Test-JsonPropertyType $item 'sha256' 'string') -or
            -not (Test-JsonPropertyType $item 'encoding' 'string') -or
            -not (Test-JsonPropertyType $item 'data' 'string')) {
            return $false
        }
    }
    return $true
}

function Test-ManifestJsonSchema {
    param([Parameter(Mandatory)][System.Xml.XmlDocument]$Document)

    $root = $Document.DocumentElement
    if (-not (Test-JsonObjectSchema $root @('schemaVersion', 'fixtureId', 'license', 'files')) -or
        -not (Test-JsonPropertyType $root 'schemaVersion' 'number') -or
        -not (Test-JsonPropertyType $root 'fixtureId' 'string') -or
        -not (Test-JsonPropertyType $root 'license' 'string')) {
        return $false
    }
    $files = $root.SelectSingleNode('files')
    if (-not (Test-JsonArray $files 'object')) { return $false }
    foreach ($item in @($files.ChildNodes)) {
        if (-not (Test-JsonObjectSchema $item @('path', 'length', 'sha256')) -or
            -not (Test-JsonPropertyType $item 'path' 'string') -or
            -not (Test-JsonPropertyType $item 'length' 'number') -or
            -not (Test-JsonPropertyType $item 'sha256' 'string')) {
            return $false
        }
    }
    return $true
}

function Get-WindowsStreamNames {
    param([Parameter(Mandatory)][string]$Path)

    if (-not ('GraniteEdgeAIFixtureStreams' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

public static class GraniteEdgeAIFixtureStreams
{
    private const int ErrorNoMoreFiles = 18;
    private const int ErrorHandleEof = 38;
    private static readonly IntPtr InvalidHandle = new IntPtr(-1);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FindStreamData
    {
        public long StreamSize;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 296)]
        public string StreamName;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindFirstStreamW(
        string fileName,
        int infoLevel,
        out FindStreamData data,
        uint flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FindNextStreamW(IntPtr handle, out FindStreamData data);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FindClose(IntPtr handle);

    public static string[] GetNames(string path)
    {
        var names = new List<string>();
        FindStreamData data;
        IntPtr handle = FindFirstStreamW(path, 0, out data, 0);
        if (handle == InvalidHandle)
        {
            int firstError = Marshal.GetLastWin32Error();
            if (firstError == ErrorNoMoreFiles || firstError == ErrorHandleEof)
            {
                return names.ToArray();
            }

            throw new Win32Exception(firstError);
        }

        try
        {
            names.Add(data.StreamName);
            while (FindNextStreamW(handle, out data))
            {
                names.Add(data.StreamName);
            }

            int nextError = Marshal.GetLastWin32Error();
            if (nextError != ErrorNoMoreFiles && nextError != ErrorHandleEof)
            {
                throw new Win32Exception(nextError);
            }
        }
        finally
        {
            FindClose(handle);
        }

        return names.ToArray();
    }
}
'@
    }

    return @([GraniteEdgeAIFixtureStreams]::GetNames($Path))
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
    foreach ($item in $items) {
        if (-not $item.PSIsContainer -and $item.Length -le 0) {
            Stop-Invalid
        }
        if ($isWindowsPlatform) {
            $streams = @(Get-WindowsStreamNames $item.FullName)
            if (@($streams | Where-Object { $_ -cne '::$DATA' }).Count -ne 0) {
                Stop-Invalid
            }
        }
    }

    $sourceSpecPath = Join-Path $root 'source\fixture-spec.json'
    $sourceSpecFile = Get-Item -LiteralPath $sourceSpecPath
    if ($sourceSpecFile.Length -ne $expectedSourceSpecLength -or
        (Get-LowerSha256 $sourceSpecPath) -cne $expectedSourceSpecSha256) {
        Stop-Invalid
    }
    $sourceDocument = Read-StrictJsonDocument $sourceSpecPath
    if (-not (Test-SourceJsonSchema $sourceDocument)) {
        Stop-Invalid
    }
    $sourceText = [System.IO.File]::ReadAllText($sourceSpecPath)
    $sourceSpec = $sourceText | ConvertFrom-Json
    if ((Test-ForbiddenText $sourceText) -or
        (Test-ForbiddenDecodedValue $sourceSpec)) {
        Stop-Invalid
    }
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

    $manifestPath = Join-Path $root 'manifest.json'
    $manifestDocument = Read-StrictJsonDocument $manifestPath
    if (-not (Test-ManifestJsonSchema $manifestDocument)) {
        Stop-Invalid
    }
    $manifestText = [System.IO.File]::ReadAllText($manifestPath)
    $manifest = $manifestText | ConvertFrom-Json
    if ((Test-ForbiddenText $manifestText) -or
        (Test-ForbiddenDecodedValue $manifest)) {
        Stop-Invalid
    }
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

    [byte[]]$rebuiltModelBytes = Get-StaticTensorBytes @($sourceSpec.model.sourceTensors)
    $modelBinPath = Join-Path $root 'package\openvino_model.bin'
    if ($rebuiltModelBytes.LongLength -ne (Get-Item -LiteralPath $modelBinPath).Length -or
        (Get-LowerSha256Bytes $rebuiltModelBytes) -cne (Get-LowerSha256 $modelBinPath)) {
        Stop-Invalid
    }

    $licensePath = Join-Path $root 'LICENSE.txt'
    $licenseFile = Get-Item -LiteralPath $licensePath
    $licenseText = [System.IO.File]::ReadAllText($licensePath)
    if ($licenseFile.Length -ne $expectedLicenseLength -or
        (Get-LowerSha256 $licensePath) -cne $expectedLicenseSha256 -or
        (Test-ForbiddenText $licenseText)) {
        Stop-Invalid
    }
    $licenseLines = @(Get-Content -LiteralPath $licensePath)
    if ($licenseLines.Count -lt 5 -or
        $licenseLines[0] -cne 'SPDX-License-Identifier: MIT' -or
        @($licenseLines | Where-Object { $_ -match '(?i)\b(TBD|N/A|unverified|unknown|pending)\b' }).Count -ne 0) {
        Stop-Invalid
    }
    if ($licenseText -cne [string]$sourceSpec.licenseText) {
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
        if (Test-ForbiddenText $text) {
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
