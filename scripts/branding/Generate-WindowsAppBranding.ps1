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

[xml]$source = Get-Content -LiteralPath $sourcePath -Raw
$namespace = [Xml.XmlNamespaceManager]::new($source.NameTable)
$namespace.AddNamespace('svg', 'http://www.w3.org/2000/svg')
$root = $source.SelectSingleNode('/svg:svg', $namespace)
$paths = @($source.SelectNodes('/svg:svg/svg:g/svg:path', $namespace))
$stops = @($source.SelectNodes('/svg:svg/svg:defs/svg:linearGradient[@id="graniteBlue"]/svg:stop', $namespace))

if ($null -eq $root -or $root.GetAttribute('viewBox') -ne '0 0 512 512' -or
    $paths.Count -ne 9 -or $stops.Count -ne 2) {
    throw 'The approved Granite symbol has an unexpected structure.'
}

if ($stops[0].GetAttribute('offset') -ne '0' -or
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
