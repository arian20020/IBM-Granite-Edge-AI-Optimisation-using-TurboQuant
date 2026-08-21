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

function ConvertTo-SvgPathData {
    param(
        [Parameter(Mandatory)]
        [string]$Text,
        [Parameter(Mandatory)]
        [double]$OriginX,
        [Parameter(Mandatory)]
        [double]$OriginY
    )

    $culture = [Globalization.CultureInfo]::InvariantCulture
    $typeface = [Windows.Media.Typeface]::new(
        [Windows.Media.FontFamily]::new('Segoe UI'),
        [Windows.FontStyles]::Normal,
        [Windows.FontWeights]::SemiBold,
        [Windows.FontStretches]::Normal)
    $formatted = [Windows.Media.FormattedText]::new(
        $Text,
        $culture,
        [Windows.FlowDirection]::LeftToRight,
        $typeface,
        138,
        [Windows.Media.Brushes]::Black,
        1.0)
    $geometry = $formatted.BuildGeometry([Windows.Point]::new($OriginX, $OriginY))
    $pathData = $geometry.GetFlattenedPathGeometry().ToString($culture)

    [pscustomobject]@{
        Data = $pathData -replace '^F1', ''
        Width = $formatted.WidthIncludingTrailingWhitespace
    }
}

$wordmark = ConvertTo-SvgPathData -Text 'Granite Edge' -OriginX 360 -OriginY 107
$ai = ConvertTo-SvgPathData -Text 'AI' -OriginX (372 + $wordmark.Width) -OriginY 107

$svg = @"
<?xml version="1.0" encoding="UTF-8"?>
<!-- Path-outlined WinUI derivative of docs/Logo/granite-edge-ai-lockup.svg. -->
<svg xmlns="http://www.w3.org/2000/svg" width="1400" height="420" viewBox="0 0 1400 420" fill="none">
  <defs>
    <linearGradient id="graniteBlue" x1="48" y1="256" x2="420" y2="256" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#0F62FE"/>
      <stop offset="1" stop-color="#003A9F"/>
    </linearGradient>
  </defs>
  <g transform="translate(20 12) scale(0.75)" data-brand-element="Granite Edge AI G monogram">
    <path d="M151 72H420L393 108H119C128 91 139 79 151 72Z" fill="url(#graniteBlue)"/>
    <path d="M100 126H386L359 162H77C83 148 91 136 100 126Z" fill="url(#graniteBlue)"/>
    <path d="M68 180H169L142 216H55C58 203 62 191 68 180Z" fill="url(#graniteBlue)"/>
    <path d="M50 234H137V270H48C47 258 48 246 50 234Z" fill="url(#graniteBlue)"/>
    <path d="M50 288H142L169 324H61C56 313 52 301 50 288Z" fill="url(#graniteBlue)"/>
    <path d="M77 342H359L386 378H100C91 368 83 356 77 342Z" fill="url(#graniteBlue)"/>
    <path d="M119 396H393L420 432H151C139 425 128 413 119 396Z" fill="url(#graniteBlue)"/>
    <path d="M228 234H420V270H196L228 234Z" fill="url(#graniteBlue)"/>
    <path d="M267 288H420V324H235L267 288Z" fill="url(#graniteBlue)"/>
  </g>
  <path d="$($wordmark.Data)" fill="#102E6B" data-brand-element="Granite Edge wordmark"/>
  <path d="$($ai.Data)" fill="#0F62FE" data-brand-element="AI wordmark"/>
</svg>
"@

$outputPath = Join-Path $RepositoryRoot 'IBM Granite with TurboQuant (Intel)\Assets\Branding\granite-edge-ai-lockup-outlined.svg'
[IO.File]::WriteAllText($outputPath, $svg, [Text.UTF8Encoding]::new($false))
Write-Host "Generated $outputPath"
