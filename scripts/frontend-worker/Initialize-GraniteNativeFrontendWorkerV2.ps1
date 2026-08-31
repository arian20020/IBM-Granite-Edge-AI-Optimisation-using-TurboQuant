[CmdletBinding()]
param(
    [switch] $Install,
    [switch] $SkipOptionalProviders
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$lockPath = Join-Path $root '.frontend-worker\v2\implementation-lock.yml'
$providerLockPath = Join-Path $root '.frontend-worker\v2\provider-lock.json'
$toolingLockPath = Join-Path $root '.frontend-worker\v2\tooling-lock.json'
$statusPath = Join-Path $root '.frontend-worker\v2\provider-status.json'

foreach ($path in @($lockPath, $providerLockPath, $toolingLockPath)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing bootstrap file: $path" }
}
if ((Get-Content -Raw $lockPath) -notmatch '(?m)^implementation_authorized:\s*false\s*$') {
    throw 'Initialization requires a closed implementation lock.'
}

$providers = (Get-Content -Raw $providerLockPath | ConvertFrom-Json).providers
$tools = (Get-Content -Raw $toolingLockPath | ConvertFrom-Json).tools
$result = [ordered]@{
    schemaVersion = 1
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    repositoryRoot = $root
    implementationLock = 'closed'
    mode = if ($Install) { 'install' } else { 'verify-only' }
    providers = @()
    tools = @()
    blockers = @()
}

function Command([string] $name) {
    $item = Get-Command $name -ErrorAction SilentlyContinue
    if ($null -eq $item) { return $null }
    return $item.Source
}

function Run([string] $file, [string[]] $arguments) {
    & $file @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed ($LASTEXITCODE): $file $($arguments -join ' ')"
    }
}

function Text([string] $file, [string[]] $arguments) {
    $output = & $file @arguments 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed ($LASTEXITCODE): $file $($arguments -join ' ')`n$output"
    }
    return $output.Trim()
}

function RecordProvider([string] $id, [bool] $required, [string] $state, [string] $detail) {
    $script:result.providers += [ordered]@{
        id = $id; required = $required; state = $state; detail = $detail
    }
    if ($required -and $state -ne 'ready') { $script:result.blockers += "$id: $detail" }
}

function RecordTool([string] $id, [bool] $required, [string] $state, [string] $detail) {
    $script:result.tools += [ordered]@{
        id = $id; requiredForInitialization = $required; state = $state; detail = $detail
    }
    if ($required -and $state -ne 'ready') { $script:result.blockers += "$id: $detail" }
}

function EnsureMarketplace(
    [string] $codex, [string] $name, [string] $source, [string] $ref, [bool] $required
) {
    $list = Text $codex @('plugin', 'marketplace', 'list')
    if ($list -match [Regex]::Escape($name)) { return 'ready|already registered' }
    if (-not $Install) {
        return $(if ($required) { 'missing|rerun with -Install' } else { 'optional-unavailable|not installed' })
    }
    try {
        $args = @('plugin', 'marketplace', 'add', $source)
        if ($ref) { $args += @('--ref', $ref) }
        Run $codex $args
        return "ready|registered at $ref"
    }
    catch {
        if ($required) { throw }
        return "optional-unavailable|$($_.Exception.Message)"
    }
}

function EnsurePlugin([string] $codex, [string] $id, [bool] $required) {
    $name = $id.Split('@')[0]
    if ((Text $codex @('plugin', 'list')) -match [Regex]::Escape($name)) {
        return 'ready|already installed'
    }
    if (-not $Install) {
        return $(if ($required) { 'missing|rerun with -Install' } else { 'optional-unavailable|not installed' })
    }
    try {
        Run $codex @('plugin', 'add', $id)
        return 'ready|installed'
    }
    catch {
        if ($required) { throw }
        return "optional-unavailable|$($_.Exception.Message)"
    }
}

function ProviderPair(
    [string] $id, [bool] $required, [string] $marketplaceResult, [string] $pluginResult
) {
    $market = $marketplaceResult.Split('|', 2)
    $plugin = $pluginResult.Split('|', 2)
    $state = if ($market[0] -eq 'ready' -and $plugin[0] -eq 'ready') {
        'ready'
    } elseif ($required) {
        'missing'
    } else {
        'optional-unavailable'
    }
    RecordProvider $id $required $state "$($market[1]); $($plugin[1])"
}

function FindPython3 {
    foreach ($candidate in @(
        @{ name = 'py'; args = @('-3', '--version') },
        @{ name = 'python'; args = @('--version') },
        @{ name = 'python3'; args = @('--version') }
    )) {
        $source = Command $candidate.name
        if ($source) {
            $version = & $source @($candidate.args) 2>&1 | Out-String
            if ($LASTEXITCODE -eq 0 -and $version -match 'Python\s+3\.') {
                return "$($version.Trim()) at $source"
            }
        }
    }
    return $null
}

$codex = Command 'codex'
$git = Command 'git'
$npm = Command 'npm'
$python = FindPython3

RecordTool 'codex-cli' $true $(if ($codex) { 'ready' } else { 'missing' }) `
    $(if ($codex) { $codex } else { 'Install and authenticate Codex.' })
RecordTool 'git' $true $(if ($git) { 'ready' } else { 'missing' }) `
    $(if ($git) { $git } else { 'Install Git.' })
RecordTool 'node-npm' $true $(if ($npm) { 'ready' } else { 'missing' }) `
    $(if ($npm) { $npm } else { 'Install Node.js/npm.' })
RecordTool 'python' $true $(if ($python) { 'ready' } else { 'missing' }) `
    $(if ($python) { $python } else { 'Install Python 3.' })

if (-not $codex -or -not $git -or -not $npm -or -not $python) {
    $result | ConvertTo-Json -Depth 10 | Set-Content -Encoding UTF8 $statusPath
    Write-Host 'INITIALIZATION BLOCKED'
    $result.blockers | ForEach-Object { Write-Host " - $_" }
    exit 2
}

$dotnet = Command 'dotnet'
RecordTool 'dotnet-sdk' $false $(if ($dotnet) { 'ready' } else { 'required-before-implementation' }) `
    $(if ($dotnet) { "$(Text $dotnet @('--version')) at $dotnet" } else { 'Install .NET SDK 8 before implementation.' })
$winapp = Command 'winapp'
RecordTool 'winapp-cli' $false $(if ($winapp) { 'ready' } else { 'required-before-implementation' }) `
    $(if ($winapp) { "$(Text $winapp @('--version')) at $winapp" } else { 'Human-approved command: winget install --id Microsoft.WinAppCli --exact --source winget' })
RecordTool 'accessibility-insights-windows' $false 'manual-verification-before-release-gate' `
    'Install or verify Accessibility Insights before accessibility acceptance.'
RecordTool 'windows-developer-mode' $false 'human-approval-if-needed' `
    'Never enable unattended; request approval before elevation or registry changes.'

$localMarket = EnsureMarketplace $codex 'granite-native-frontend' $root '' $true
$localPlugin = EnsurePlugin $codex 'granite-native-frontend-worker@granite-native-frontend' $true
ProviderPair 'granite-native-frontend-worker' $true $localMarket $localPlugin

$winui = $providers | Where-Object id -eq 'microsoft-winui'
ProviderPair $winui.id $true `
    (EnsureMarketplace $codex $winui.marketplace $winui.repository $winui.ref $true) `
    (EnsurePlugin $codex "$($winui.plugin)@$($winui.marketplace)" $true)

$stark = $providers | Where-Object id -eq 'stark'
ProviderPair $stark.id $true `
    (EnsureMarketplace $codex $stark.marketplace $stark.distributionRepository $stark.distributionRef $true) `
    (EnsurePlugin $codex "$($stark.plugin)@$($stark.marketplace)" $true)

if ($SkipOptionalProviders) {
    RecordProvider 'product-design' $false 'skipped' 'Skipped by operator.'
    RecordProvider 'figma' $false 'skipped' 'Skipped by operator.'
}
else {
    $product = $providers | Where-Object id -eq 'product-design'
    ProviderPair $product.id $false `
        (EnsureMarketplace $codex $product.marketplace $product.repository $product.ref $false) `
        (EnsurePlugin $codex "$($product.plugin)@$($product.marketplace)" $false)

    $figma = $providers | Where-Object id -eq 'figma'
    ProviderPair $figma.id $false `
        (EnsureMarketplace $codex $figma.marketplace $figma.repository $figma.ref $false) `
        (EnsurePlugin $codex "$($figma.plugin)@$($figma.marketplace)" $false)
}

$uncodixfy = $providers | Where-Object id -eq 'uncodixfy'
$cache = Join-Path $root '.frontend-worker\v2\providers\uncodixfy'
$skill = Join-Path $root '.agents\skills\uncodixfy'
$metadata = Join-Path $skill '.provider.json'

if ($Install) {
    if (Test-Path $cache) { Remove-Item -Recurse -Force $cache }
    New-Item -ItemType Directory -Force (Split-Path $cache) | Out-Null
    Run $git @('clone', '--no-checkout', '--filter=blob:none',
        "https://github.com/$($uncodixfy.repository).git", $cache)
    Run $git @('-C', $cache, 'checkout', '--detach', $uncodixfy.ref)
    New-Item -ItemType Directory -Force $skill | Out-Null
    foreach ($name in @('SKILL.md', 'Uncodixfy.md', 'LICENSE')) {
        Copy-Item -Force (Join-Path $cache $name) (Join-Path $skill $name)
    }
    @{ repository = $uncodixfy.repository; ref = $uncodixfy.ref } |
        ConvertTo-Json | Set-Content -Encoding UTF8 $metadata
}

$uncodixfyReady = $false
if (Test-Path $metadata) {
    $installed = Get-Content -Raw $metadata | ConvertFrom-Json
    $uncodixfyReady = (Test-Path (Join-Path $skill 'SKILL.md')) -and
        $installed.repository -eq $uncodixfy.repository -and $installed.ref -eq $uncodixfy.ref
}
RecordProvider $uncodixfy.id $true $(if ($uncodixfyReady) { 'ready' } else { 'missing' }) `
    $(if ($uncodixfyReady) { "pinned at $($uncodixfy.ref)" } else { 'Rerun with -Install.' })

$uipro = $providers | Where-Object id -eq 'ui-ux-pro-max'
$npmJson = & $npm list -g $uipro.npmPackage --depth=0 --json 2>$null | Out-String
$uiproVersion = $null
try {
    $doc = $npmJson | ConvertFrom-Json
    $property = $doc.dependencies.PSObject.Properties[$uipro.npmPackage]
    if ($property) { $uiproVersion = [string] $property.Value.version }
}
catch { $uiproVersion = $null }

if ($Install -and $uiproVersion -ne $uipro.npmVersion) {
    Run $npm @('install', '-g', "$($uipro.npmPackage)@$($uipro.npmVersion)", '--ignore-scripts')
}
$uiproCommand = Command 'uipro'
if ($Install -and $uiproCommand) { Run $uiproCommand @('init', '--ai', 'codex') }

$skillCandidates = @(
    (Join-Path $root '.agents\skills\ui-ux-pro-max\SKILL.md'),
    (Join-Path $root '.codex\skills\ui-ux-pro-max\SKILL.md')
)
$uiproReady = $uiproCommand -and (@($skillCandidates | Where-Object { Test-Path $_ }).Count -gt 0)
RecordProvider $uipro.id $true $(if ($uiproReady) { 'ready' } else { 'missing' }) `
    $(if ($uiproReady) { 'CLI and project skill present' } else { 'Rerun with -Install.' })

$result | ConvertTo-Json -Depth 10 | Set-Content -Encoding UTF8 $statusPath
& (Join-Path $PSScriptRoot 'Test-GraniteNativeFrontendWorkerV2.ps1') -StructureOnly
if ($LASTEXITCODE -ne 0) { throw 'Structural verification failed.' }

if ($result.blockers.Count -gt 0) {
    Write-Host 'INITIALIZATION BLOCKED'
    $result.blockers | ForEach-Object { Write-Host " - $_" }
    Write-Host 'No production UI or backend implementation was started.'
    exit 2
}

Write-Host 'INITIALIZATION READY'
Write-Host "Provider and tooling status: $statusPath"
Write-Host 'The implementation lock remains CLOSED.'
Write-Host 'Start a new Codex thread so plugin and skill discovery is current.'
exit 0
