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
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing bootstrap file: $path"
    }
}
if ((Get-Content -Raw $lockPath) -notmatch '(?m)^implementation_authorized:\s*false\s*$') {
    throw 'Initialization requires a closed implementation lock.'
}

$providers = (Get-Content -Raw $providerLockPath | ConvertFrom-Json).providers
$tools = (Get-Content -Raw $toolingLockPath | ConvertFrom-Json).tools
$result = [ordered]@{
    schemaVersion = 2
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
    if ($null -eq $item) {
        return $null
    }
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

function RecordProvider(
    [string] $id,
    [bool] $required,
    [string] $state,
    [string] $detail,
    [string] $expectedVersion = '',
    [string] $installedVersion = '',
    [string] $expectedRef = '',
    [string] $installedRef = '',
    [string] $pinEvidence = ''
) {
    $script:result.providers += [ordered]@{
        id = $id
        required = $required
        state = $state
        detail = $detail
        expectedVersion = $expectedVersion
        installedVersion = $installedVersion
        expectedRef = $expectedRef
        installedRef = $installedRef
        pinEvidence = $pinEvidence
    }
    if ($required -and $state -ne 'ready') {
        $script:result.blockers += "${id}: $detail"
    }
}

function RecordTool([string] $id, [bool] $required, [string] $state, [string] $detail) {
    $script:result.tools += [ordered]@{
        id = $id
        requiredForInitialization = $required
        state = $state
        detail = $detail
    }
    if ($required -and $state -ne 'ready') {
        $script:result.blockers += "${id}: $detail"
    }
}

function NewOperationResult(
    [string] $state,
    [string] $detail,
    [string] $version = '',
    [string] $pinEvidence = ''
) {
    return [pscustomobject]@{
        state = $state
        detail = $detail
        version = $version
        pinEvidence = $pinEvidence
    }
}

function EnsureMarketplace(
    [string] $codex,
    [string] $name,
    [string] $source,
    [string] $ref,
    [bool] $required
) {
    $pinEvidence = if ([string]::IsNullOrWhiteSpace($ref)) {
        "local:$source"
    }
    else {
        "git:$source@$ref"
    }

    if (-not $Install) {
        try {
            $document = Text $codex @('plugin', 'marketplace', 'list', '--json') | ConvertFrom-Json
            $matches = @($document.marketplaces | Where-Object { $_.name -eq $name })
            if ($matches.Count -eq 1) {
                return NewOperationResult 'pin-unverified' `
                    'Marketplace is present, but verify-only mode cannot reassert its exact source/ref. Rerun with -Install.' `
                    '' $pinEvidence
            }
            $state = if ($required) { 'missing' } else { 'optional-unavailable' }
            return NewOperationResult $state 'Marketplace is not registered. Rerun with -Install.' '' $pinEvidence
        }
        catch {
            $state = if ($required) { 'missing' } else { 'optional-unavailable' }
            return NewOperationResult $state $_.Exception.Message '' $pinEvidence
        }
    }

    try {
        $arguments = @('plugin', 'marketplace', 'add', $source)
        if (-not [string]::IsNullOrWhiteSpace($ref)) {
            $arguments += @('--ref', $ref)
        }
        $arguments += '--json'
        $document = Text $codex $arguments | ConvertFrom-Json
        if ($document.marketplaceName -ne $name) {
            throw "Expected marketplace '$name' but Codex registered '$($document.marketplaceName)'."
        }
        $action = if ($document.alreadyAdded) { 'already registered' } else { 'registered' }
        return NewOperationResult 'ready' "$action from the exact pinned source/ref" '' $pinEvidence
    }
    catch {
        $state = if ($required) { 'missing' } else { 'optional-unavailable' }
        return NewOperationResult $state $_.Exception.Message '' $pinEvidence
    }
}

function GetInstalledPlugin([string] $codex, [string] $id) {
    $parts = @($id -split '@', 2)
    $document = Text $codex @('plugin', 'list', '--json') | ConvertFrom-Json
    $matches = @($document.installed | Where-Object {
        $_.pluginId -eq $id -or
        ($parts.Count -eq 2 -and $_.name -eq $parts[0] -and $_.marketplaceName -eq $parts[1])
    })
    if ($matches.Count -gt 1) {
        throw "Codex reported more than one installed record for '$id'."
    }
    if ($matches.Count -eq 0) {
        return $null
    }
    return $matches[0]
}

function EnsurePlugin(
    [string] $codex,
    [string] $id,
    [string] $expectedVersion,
    [bool] $required
) {
    try {
        if ($Install) {
            # `plugin add` materializes the plugin from the currently configured,
            # already reasserted marketplace snapshot.
            $null = Text $codex @('plugin', 'add', $id, '--json')
        }

        $entry = GetInstalledPlugin $codex $id
        if ($null -eq $entry) {
            $state = if ($required) { 'missing' } else { 'optional-unavailable' }
            return NewOperationResult $state 'Plugin is not installed.'
        }
        if (-not $entry.enabled) {
            $state = if ($required) { 'missing' } else { 'optional-unavailable' }
            return NewOperationResult $state 'Plugin is installed but disabled.' ([string] $entry.version)
        }

        $installedVersion = [string] $entry.version
        if (-not [string]::IsNullOrWhiteSpace($expectedVersion) -and
            $installedVersion -ne $expectedVersion) {
            $state = if ($required) { 'version-mismatch' } else { 'optional-unavailable' }
            return NewOperationResult $state `
                "Expected version $expectedVersion but Codex reports $installedVersion." `
                $installedVersion
        }

        return NewOperationResult 'ready' 'Plugin is installed and enabled.' $installedVersion
    }
    catch {
        $state = if ($required) { 'missing' } else { 'optional-unavailable' }
        return NewOperationResult $state $_.Exception.Message
    }
}

function RecordMarketplacePluginProvider(
    [string] $id,
    [bool] $required,
    [string] $expectedVersion,
    [string] $expectedRef,
    [object] $marketplaceResult,
    [object] $pluginResult
) {
    $state = if ($marketplaceResult.state -eq 'ready' -and $pluginResult.state -eq 'ready') {
        'ready'
    }
    elseif ($required) {
        'missing'
    }
    else {
        'optional-unavailable'
    }
    RecordProvider $id $required $state `
        "$($marketplaceResult.detail); $($pluginResult.detail)" `
        $expectedVersion $pluginResult.version $expectedRef '' $marketplaceResult.pinEvidence
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

function GetGlobalNpmPackageVersion([string] $npm, [string] $packageName) {
    $output = & $npm list -g $packageName --depth=0 --json 2>$null | Out-String
    if ([string]::IsNullOrWhiteSpace($output)) {
        return $null
    }
    try {
        $document = $output | ConvertFrom-Json
        $property = $document.dependencies.PSObject.Properties[$packageName]
        if ($property) {
            return [string] $property.Value.version
        }
    }
    catch {
        return $null
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
$localPlugin = EnsurePlugin $codex 'granite-native-frontend-worker@granite-native-frontend' '2.0.0' $true
RecordMarketplacePluginProvider 'granite-native-frontend-worker' $true '2.0.0' `
    'repo-local' $localMarket $localPlugin

$winui = $providers | Where-Object id -eq 'microsoft-winui'
$winuiMarket = EnsureMarketplace $codex $winui.marketplace $winui.repository $winui.ref $true
$winuiPlugin = EnsurePlugin $codex "$($winui.plugin)@$($winui.marketplace)" $winui.reviewedVersion $true
RecordMarketplacePluginProvider $winui.id $true $winui.reviewedVersion $winui.ref `
    $winuiMarket $winuiPlugin

$stark = $providers | Where-Object id -eq 'stark'
$starkMarket = EnsureMarketplace $codex $stark.marketplace `
    $stark.distributionRepository $stark.distributionRef $true
$starkPlugin = EnsurePlugin $codex "$($stark.plugin)@$($stark.marketplace)" $stark.reviewedVersion $true
RecordMarketplacePluginProvider $stark.id $true $stark.reviewedVersion $stark.distributionRef `
    $starkMarket $starkPlugin

if ($SkipOptionalProviders) {
    RecordProvider 'product-design' $false 'skipped' 'Skipped by operator.'
    RecordProvider 'figma' $false 'skipped' 'Skipped by operator.'
}
else {
    $product = $providers | Where-Object id -eq 'product-design'
    $productMarket = EnsureMarketplace $codex $product.marketplace $product.repository $product.ref $false
    $productPlugin = EnsurePlugin $codex "$($product.plugin)@$($product.marketplace)" `
        $product.reviewedVersion $false
    RecordMarketplacePluginProvider $product.id $false $product.reviewedVersion $product.ref `
        $productMarket $productPlugin

    # `openai-curated` may be supplied by the active Codex product rather than
    # a user-configured Git marketplace. Verify the official plugin/version
    # without replacing that system marketplace.
    $figma = $providers | Where-Object id -eq 'figma'
    $figmaPlugin = EnsurePlugin $codex "$($figma.plugin)@$($figma.marketplace)" `
        $figma.reviewedVersion $false
    RecordProvider $figma.id $false $figmaPlugin.state `
        "Official marketplace plugin check: $($figmaPlugin.detail)" `
        $figma.reviewedVersion $figmaPlugin.version $figma.ref '' 'official-marketplace'
}

$uncodixfy = $providers | Where-Object id -eq 'uncodixfy'
$cache = Join-Path $root '.frontend-worker\v2\providers\uncodixfy'
$skill = Join-Path $root '.agents\skills\uncodixfy'
$metadata = Join-Path $skill '.provider.json'

if ($Install) {
    if (Test-Path $cache) {
        Remove-Item -Recurse -Force $cache
    }
    New-Item -ItemType Directory -Force (Split-Path $cache) | Out-Null
    Run $git @('clone', '--no-checkout', '--filter=blob:none',
        "https://github.com/$($uncodixfy.repository).git", $cache)
    Run $git @('-C', $cache, 'checkout', '--detach', $uncodixfy.ref)
    $head = Text $git @('-C', $cache, 'rev-parse', 'HEAD')
    if ($head -ne $uncodixfy.ref) {
        throw "Uncodixfy checkout mismatch. Expected $($uncodixfy.ref), received $head."
    }

    if (Test-Path $skill) {
        Remove-Item -Recurse -Force $skill
    }
    New-Item -ItemType Directory -Force $skill | Out-Null
    foreach ($name in @('SKILL.md', 'Uncodixfy.md', 'LICENSE')) {
        Copy-Item -Force (Join-Path $cache $name) (Join-Path $skill $name)
    }
    [ordered]@{
        repository = $uncodixfy.repository
        ref = $uncodixfy.ref
        installedHead = $head
    } | ConvertTo-Json | Set-Content -Encoding UTF8 $metadata
}

$uncodixfyReady = $false
$uncodixfyInstalledRef = ''
if (Test-Path $metadata) {
    $installed = Get-Content -Raw $metadata | ConvertFrom-Json
    $uncodixfyInstalledRef = [string] $installed.installedHead
    $uncodixfyReady = (Test-Path (Join-Path $skill 'SKILL.md')) -and
        $installed.repository -eq $uncodixfy.repository -and
        $installed.ref -eq $uncodixfy.ref -and
        $installed.installedHead -eq $uncodixfy.ref
}
RecordProvider $uncodixfy.id $true $(if ($uncodixfyReady) { 'ready' } else { 'missing' }) `
    $(if ($uncodixfyReady) { 'Pinned project skill is present.' } else { 'Rerun with -Install.' }) `
    '' '' $uncodixfy.ref $uncodixfyInstalledRef 'detached-git-checkout'

$uipro = $providers | Where-Object id -eq 'ui-ux-pro-max'
$uiproVersion = GetGlobalNpmPackageVersion $npm $uipro.npmPackage
if ($Install -and $uiproVersion -ne $uipro.npmVersion) {
    Run $npm @('install', '-g', "$($uipro.npmPackage)@$($uipro.npmVersion)", '--ignore-scripts')
}
$uiproVersion = GetGlobalNpmPackageVersion $npm $uipro.npmPackage
$uiproCommand = Command 'uipro'
if ($Install -and $uiproCommand -and $uiproVersion -eq $uipro.npmVersion) {
    Push-Location $root
    try {
        Run $uiproCommand @('init', '--ai', 'codex', '--force', '--offline')
    }
    finally {
        Pop-Location
    }
}

$skillCandidates = @(
    (Join-Path $root '.agents\skills\ui-ux-pro-max\SKILL.md'),
    (Join-Path $root '.codex\skills\ui-ux-pro-max\SKILL.md')
)
$uiproSkillPresent = @($skillCandidates | Where-Object { Test-Path $_ }).Count -gt 0
$uiproReady = $uiproCommand -and $uiproSkillPresent -and $uiproVersion -eq $uipro.npmVersion
RecordProvider $uipro.id $true $(if ($uiproReady) { 'ready' } else { 'missing' }) `
    $(if ($uiproReady) { 'Pinned CLI and project skill are present.' } else { 'Rerun with -Install.' }) `
    $uipro.npmVersion $uiproVersion $uipro.ref '' 'npm-exact-version-and-offline-template'

$result | ConvertTo-Json -Depth 10 | Set-Content -Encoding UTF8 $statusPath
& (Join-Path $PSScriptRoot 'Test-GraniteNativeFrontendWorkerV2.ps1') -StructureOnly
if ($LASTEXITCODE -ne 0) {
    throw 'Structural verification failed.'
}

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
