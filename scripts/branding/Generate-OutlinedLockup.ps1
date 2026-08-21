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

$sourcePath = Join-Path $RepositoryRoot 'docs\Logo\granite-edge-ai-lockup.svg'
$outputPath = Join-Path $RepositoryRoot 'IBM Granite with TurboQuant (Intel)\Assets\Branding\granite-edge-ai-lockup-outlined.svg'
[xml]$source = Get-Content -LiteralPath $sourcePath -Raw
$namespace = [Xml.XmlNamespaceManager]::new($source.NameTable)
$namespace.AddNamespace('svg', 'http://www.w3.org/2000/svg')

$root = $source.SelectSingleNode('/svg:svg', $namespace)
$sourceText = $source.SelectSingleNode('/svg:svg/svg:text', $namespace)
$sourceTspan = $source.SelectSingleNode('/svg:svg/svg:text/svg:tspan', $namespace)
$sourcePaths = @($source.SelectNodes('/svg:svg/svg:g/svg:g/svg:path', $namespace))
$sourceGradient = $source.SelectSingleNode('/svg:svg/svg:defs/svg:linearGradient[@id="graniteBlue"]', $namespace)

if ($null -eq $root -or $null -eq $sourceText -or $null -eq $sourceTspan -or
    $null -eq $sourceGradient -or $sourcePaths.Count -ne 9) {
    throw 'The source Granite lockup has an unexpected structure.'
}

$wordmarkText = $sourceText.InnerText
$sourceX = [double]::Parse($sourceText.GetAttribute('x'), [Globalization.CultureInfo]::InvariantCulture)
$sourceBaseline = [double]::Parse($sourceText.GetAttribute('y'), [Globalization.CultureInfo]::InvariantCulture)
$fontSize = [double]::Parse($sourceText.GetAttribute('font-size'), [Globalization.CultureInfo]::InvariantCulture)
$letterSpacing = [double]::Parse($sourceText.GetAttribute('letter-spacing'), [Globalization.CultureInfo]::InvariantCulture)

if ($wordmarkText -ne 'Granite Edge AI' -or $fontSize -ne 138 -or
    $letterSpacing -ne -4 -or $sourceText.GetAttribute('font-weight') -ne '600') {
    throw 'The source Granite wordmark typography has changed; review the outlined conversion.'
}

$culture = [Globalization.CultureInfo]::InvariantCulture
$typeface = [Windows.Media.Typeface]::new(
    [Windows.Media.FontFamily]::new('Segoe UI'),
    [Windows.FontStyles]::Normal,
    [Windows.FontWeights]::SemiBold,
    [Windows.FontStretches]::Normal)
$formatted = [Windows.Media.FormattedText]::new(
    $wordmarkText,
    $culture,
    [Windows.FlowDirection]::LeftToRight,
    $typeface,
    $fontSize,
    [Windows.Media.Brushes]::Black,
    1.0)
$originY = $sourceBaseline - $formatted.Baseline
$geometry = $formatted.BuildGeometry([Windows.Point]::new($sourceX, $originY))
$glyphs = @($geometry.Children[0].Children)
$characterIndices = @(for ($index = 0; $index -lt $wordmarkText.Length; $index++) {
    if (-not [char]::IsWhiteSpace($wordmarkText[$index])) {
        $index
    }
})

if ($glyphs.Count -ne $characterIndices.Count) {
    throw 'The outlined wordmark glyph count does not match the source text.'
}

$sourceHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash.ToLowerInvariant()
$lines = [Collections.Generic.List[string]]::new()
$lines.Add('<?xml version="1.0" encoding="UTF-8"?>')
$lines.Add('<!-- Path-outlined WinUI derivative of docs/Logo/granite-edge-ai-lockup.svg. -->')
$lines.Add(('<svg xmlns="http://www.w3.org/2000/svg" width="{0}" height="{1}" viewBox="{2}" fill="none" data-source-sha256="{3}">' -f
    $root.GetAttribute('width'), $root.GetAttribute('height'), $root.GetAttribute('viewBox'), $sourceHash))
$lines.Add('  <defs>')
$lines.Add(('    <linearGradient id="graniteBlue" x1="{0}" y1="{1}" x2="{2}" y2="{3}" gradientUnits="{4}">' -f
    $sourceGradient.GetAttribute('x1'), $sourceGradient.GetAttribute('y1'),
    $sourceGradient.GetAttribute('x2'), $sourceGradient.GetAttribute('y2'),
    $sourceGradient.GetAttribute('gradientUnits')))
foreach ($stop in @($sourceGradient.SelectNodes('svg:stop', $namespace))) {
    $lines.Add(('      <stop offset="{0}" stop-color="{1}"/>' -f
        $stop.GetAttribute('offset'), $stop.GetAttribute('stop-color')))
}
$lines.Add('    </linearGradient>')
$lines.Add('  </defs>')
$lines.Add('  <g transform="translate(20 12) scale(0.75)" data-brand-element="Granite Edge AI G monogram">')
foreach ($path in $sourcePaths) {
    $lines.Add(('    <path d="{0}" fill="{1}"/>' -f $path.GetAttribute('d'), $path.GetAttribute('fill')))
}
$lines.Add('  </g>')
for ($glyphIndex = 0; $glyphIndex -lt $glyphs.Count; $glyphIndex++) {
    $characterIndex = $characterIndices[$glyphIndex]
    $trackingOffset = $letterSpacing * $characterIndex
    $pathData = $glyphs[$glyphIndex].GetFlattenedPathGeometry().ToString($culture) -replace '^F1', ''
    $fill = if ($characterIndex -ge $wordmarkText.IndexOf('AI')) {
        $sourceTspan.GetAttribute('fill')
    } else {
        $sourceText.GetAttribute('fill')
    }
    $lines.Add(('  <path d="{0}" fill="{1}" transform="translate({2} 0)" data-source-char-index="{3}"/>' -f
        $pathData, $fill, $trackingOffset.ToString($culture), $characterIndex))
}
$lines.Add('</svg>')

[IO.File]::WriteAllText($outputPath, ($lines -join "`n") + "`n", [Text.UTF8Encoding]::new($false))
Write-Host "Generated $outputPath from $sourcePath"
