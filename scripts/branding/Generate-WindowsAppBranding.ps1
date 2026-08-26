[CmdletBinding()]
param(
    [string]$RepositoryRoot
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
}

Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase

$sourceRelativePath = 'docs\Logo\granite-edge-ai-icon.svg'
$sourcePath = Join-Path $RepositoryRoot $sourceRelativePath
$appRelativeRoot = 'IBM Granite with TurboQuant (Intel)'
$appRoot = Join-Path $RepositoryRoot $appRelativeRoot
$brandingRoot = Join-Path $appRoot 'Assets\Branding'
$icoRelativePath = "$appRelativeRoot/Assets/Branding/granite-edge-ai.ico"
$icoPath = Join-Path $RepositoryRoot ($icoRelativePath -replace '/', '\')
$manifestPath = Join-Path $brandingRoot 'windows-icon-manifest.json'
$approvedSourceSha256 = 'b392df072100e16675c21987070b7e1063f5e891c1a4bedb920e90e679691ba2'
$actualSourceSha256 = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualSourceSha256 -ne $approvedSourceSha256) {
    throw 'The approved Granite symbol bytes have changed; review and repin branding intentionally.'
}

[xml]$source = Get-Content -LiteralPath $sourcePath -Raw
$namespace = [Xml.XmlNamespaceManager]::new($source.NameTable)
$namespace.AddNamespace('svg', 'http://www.w3.org/2000/svg')
$root = $source.SelectSingleNode('/svg:svg', $namespace)
$gradient = $source.SelectSingleNode('/svg:svg/svg:defs/svg:linearGradient[@id="graniteBlue"]', $namespace)
$group = $source.SelectSingleNode('/svg:svg/svg:g', $namespace)
$paths = @($source.SelectNodes('/svg:svg/svg:g/svg:path', $namespace))
$stops = @($source.SelectNodes('/svg:svg/svg:defs/svg:linearGradient[@id="graniteBlue"]/svg:stop', $namespace))
$rootElements = @($root.ChildNodes | Where-Object NodeType -eq Element)
$definitionElements = @($rootElements[0].ChildNodes | Where-Object NodeType -eq Element)
$groupElements = @($rootElements[1].ChildNodes | Where-Object NodeType -eq Element)

if ($null -eq $root -or $null -eq $gradient -or $null -eq $group -or
    $root.GetAttribute('width') -ne '512' -or
    $root.GetAttribute('height') -ne '512' -or
    $root.GetAttribute('viewBox') -ne '0 0 512 512' -or
    $root.GetAttribute('fill') -ne 'none' -or
    $rootElements.Count -ne 2 -or
    $rootElements[0].LocalName -ne 'defs' -or
    $rootElements[1].LocalName -ne 'g' -or
    $definitionElements.Count -ne 1 -or
    $definitionElements[0].LocalName -ne 'linearGradient' -or
    $groupElements.Count -ne 9 -or
    @($groupElements | Where-Object LocalName -ne 'path').Count -ne 0 -or
    $group.GetAttribute('transform') -ne 'translate(0 0)' -or
    $group.GetAttribute('data-brand-element') -ne 'Granite Edge AI G monogram' -or
    $paths.Count -ne 9 -or $stops.Count -ne 2) {
    throw 'The approved Granite symbol has an unexpected structure.'
}

if ($gradient.GetAttribute('x1') -ne '48' -or
    $gradient.GetAttribute('y1') -ne '256' -or
    $gradient.GetAttribute('x2') -ne '420' -or
    $gradient.GetAttribute('y2') -ne '256' -or
    $gradient.GetAttribute('gradientUnits') -ne 'userSpaceOnUse' -or
    $stops[0].GetAttribute('offset') -ne '0' -or
    $stops[0].GetAttribute('stop-color') -ne '#0F62FE' -or
    $stops[1].GetAttribute('offset') -ne '1' -or
    $stops[1].GetAttribute('stop-color') -ne '#003A9F') {
    throw 'The approved Granite symbol gradient has changed.'
}

$geometries = foreach ($path in $paths) {
    $fill = $path.GetAttribute('fill')
    if ($fill -ne 'url(#graniteBlue)') {
        throw 'Every approved Granite symbol path must use graniteBlue.'
    }

    [Windows.Media.Geometry]::Parse($path.GetAttribute('d'))
}

function Get-Sha256([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Write-AtomicBytes([string]$Path, [byte[]]$Bytes) {
    $directory = Split-Path -Parent $Path
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    $temporaryPath = "$Path.$PID.tmp"
    $backupPath = "$Path.$PID.bak"
    try {
        [IO.File]::WriteAllBytes($temporaryPath, $Bytes)
        if ([IO.File]::Exists($Path)) {
            [IO.File]::Replace($temporaryPath, $Path, $backupPath)
            [IO.File]::Delete($backupPath)
        }
        else {
            [IO.File]::Move($temporaryPath, $Path)
        }
    }
    finally {
        if ([IO.File]::Exists($temporaryPath)) {
            [IO.File]::Delete($temporaryPath)
        }
    }
}

function New-GranitePng([int]$Width, [int]$Height) {
    $visual = [Windows.Media.DrawingVisual]::new()
    $context = $visual.RenderOpen()
    try {
        $shortEdge = [Math]::Min($Width, $Height)
        $symbolSize = $shortEdge * 0.75
        $scale = $symbolSize / 512.0
        $offsetX = ($Width - (512.0 * $scale)) / 2.0
        $offsetY = ($Height - (512.0 * $scale)) / 2.0
        $matrix = [Windows.Media.Matrix]::new(
            $scale,
            0,
            0,
            $scale,
            $offsetX,
            $offsetY)
        $context.PushTransform([Windows.Media.MatrixTransform]::new($matrix))
        try {
            $gradient = [Windows.Media.LinearGradientBrush]::new()
            $gradient.MappingMode = [Windows.Media.BrushMappingMode]::Absolute
            $gradient.StartPoint = [Windows.Point]::new(48, 256)
            $gradient.EndPoint = [Windows.Point]::new(420, 256)
            $gradient.GradientStops.Add([Windows.Media.GradientStop]::new(
                [Windows.Media.ColorConverter]::ConvertFromString('#0F62FE'),
                0))
            $gradient.GradientStops.Add([Windows.Media.GradientStop]::new(
                [Windows.Media.ColorConverter]::ConvertFromString('#003A9F'),
                1))
            $gradient.Freeze()

            foreach ($geometry in $geometries) {
                $context.DrawGeometry($gradient, $null, $geometry)
            }
        }
        finally {
            $context.Pop()
        }
    }
    finally {
        $context.Close()
    }

    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new(
        $Width,
        $Height,
        96,
        96,
        [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.MemoryStream]::new()
    try {
        $encoder.Save($stream)
        $stream.ToArray()
    }
    finally {
        $stream.Dispose()
    }
}

function New-PngBackedIco([Collections.IDictionary]$Frames) {
    $stream = [IO.MemoryStream]::new()
    $writer = [IO.BinaryWriter]::new($stream, [Text.Encoding]::UTF8, $true)
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$Frames.Count)
        $offset = 6 + (16 * $Frames.Count)
        foreach ($size in $Frames.Keys) {
            $dimension = if ([int]$size -eq 256) { [byte]0 } else { [byte][int]$size }
            $bytes = [byte[]]$Frames[$size]
            $writer.Write($dimension)
            $writer.Write($dimension)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$bytes.Length)
            $writer.Write([uint32]$offset)
            $offset += $bytes.Length
        }
        foreach ($size in $Frames.Keys) {
            $writer.Write([byte[]]$Frames[$size])
        }
        $writer.Flush()
        $stream.ToArray()
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

$pngTargets = [ordered]@{
    'Assets\LockScreenLogo.scale-200.png' = @(48, 48)
    'Assets\SplashScreen.scale-200.png' = @(1240, 600)
    'Assets\Square150x150Logo.scale-200.png' = @(300, 300)
    'Assets\Square44x44Logo.scale-200.png' = @(88, 88)
    'Assets\Square44x44Logo.targetsize-24_altform-unplated.png' = @(24, 24)
    'Assets\StoreLogo.png' = @(50, 50)
    'Assets\Wide310x150Logo.scale-200.png' = @(620, 300)
}

$pngEvidence = [Collections.Generic.List[object]]::new()
foreach ($relativePath in $pngTargets.Keys) {
    $dimensions = $pngTargets[$relativePath]
    $outputPath = Join-Path $appRoot $relativePath
    Write-AtomicBytes $outputPath (New-GranitePng $dimensions[0] $dimensions[1])
    $portablePath = "$appRelativeRoot/$($relativePath -replace '\\', '/')"
    $pngEvidence.Add([ordered]@{
        path = $portablePath
        sha256 = Get-Sha256 $outputPath
        bytes = (Get-Item -LiteralPath $outputPath).Length
        width = $dimensions[0]
        height = $dimensions[1]
    })
}

$icoSizes = @(16, 24, 32, 48, 64, 128, 256)
$icoFrames = [ordered]@{}
foreach ($size in $icoSizes) {
    $icoFrames.Add([string]$size, (New-GranitePng $size $size))
}
Write-AtomicBytes $icoPath (New-PngBackedIco $icoFrames)

$manifest = [ordered]@{
    schemaVersion = 1
    source = [ordered]@{
        path = ($sourceRelativePath -replace '\\', '/')
        sha256 = Get-Sha256 $sourcePath
    }
    pngs = $pngEvidence
    ico = [ordered]@{
        path = $icoRelativePath
        sha256 = Get-Sha256 $icoPath
        bytes = (Get-Item -LiteralPath $icoPath).Length
        sizes = $icoSizes
    }
}
$manifestBytes = [Text.UTF8Encoding]::new($false).GetBytes(
    ($manifest | ConvertTo-Json -Depth 6) + "`n")
Write-AtomicBytes $manifestPath $manifestBytes

Write-Host "Generated Windows branding from $sourceRelativePath"
