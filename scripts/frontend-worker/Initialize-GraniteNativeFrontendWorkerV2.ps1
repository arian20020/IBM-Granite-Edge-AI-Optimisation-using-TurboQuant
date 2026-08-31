[CmdletBinding()]
param(
    [switch] $Install,
    [switch] $SkipOptionalProviders
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepositoryRoot {
    $output = & git rev-parse --show-toplevel 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($output)) {
        throw 'Run this script from inside the Granite Edge AI repository.'
    }
    return $output.Trim()
}

function Get-CommandPath([string] $Name) {
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -eq $command) { return $null }
    return $command.Source
}

function Invoke-External([string] $File, [string[]] $Arguments) {
    $output = & $File @Arguments 2>&1 | Out-String
    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output = $output.Trim()
        Command = "$File $($Arguments -join ' ')"
    }
}

function Get-VersionFromText([string] $Text) {
    $match = [regex]::Match($Text, '(?<!\d)(\d+\.\d+(?:\.\d+){0,2})(?!\d)')
    if ($match.Success) { return $match.Groups[1].Value }
    return $null
}

function Read-LockState([string] $Root) {
    $policyPath = Join-Path $Root '.frontend-worker/v2/implementation-lock.yml'
    $policy = Get-Content -LiteralPath $policyPath -Raw
    $match = [regex]::Match($policy, '(?m)^state_file:\s*(.+?)\s*$')
    if (-not $match.Success) { throw 'implementation-lock.yml does not declare state_file.' }
    $relative = $match.Groups[1].Value.Trim().Trim('"').Trim("'")
    $path = Join-Path $Root $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        return [pscustomobject]@{ State = 'closed'; Path = $path; Record = $null }
    }
    try {
        $record = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    }
    catch {
        throw "Authorization state is malformed: $path"
    }
    $open = $record.implementationAuthorized -eq $true
    return [pscustomobject]@{ State = $(if ($open) { 'open' } else { 'closed' }); Path = $path; Record = $record }
}

function Add-Result(
    [System.Collections.Generic.List[object]] $List,
    [string] $Id,
    [bool] $Required,
    [string] $State,
    [string] $Detail,
    [string] $ExpectedVersion = '',
    [string] $InstalledVersion = '',
    [string] $PinEvidence = '') {
    $List.Add([ordered]@{
        id = $Id
        required = $Required
        state = $State
        detail = $Detail
        expectedVersion = $ExpectedVersion
        installedVersion = $InstalledVersion
        pinEvidence = $PinEvidence
    })
}

function Ensure-Marketplace(
    [string] $Codex,
    [string] $ExpectedName,
    [string] $Source,
    [string] $Ref,
    [bool] $InstallNow) {
    if (-not $InstallNow) {
        return [pscustomobject]@{
            Ready = $false
            Detail = 'Exact marketplace source/ref was not reasserted. Run with -Install.'
            PinEvidence = "git:$Source@$Ref"
        }
    }

    $args = @('plugin', 'marketplace', 'add', $Source)
    if (-not [string]::IsNullOrWhiteSpace($Ref)) { $args += @('--ref', $Ref) }
    $args += '--json'
    $result = Invoke-External $Codex $args
    if ($result.ExitCode -ne 0) {
        return [pscustomobject]@{
            Ready = $false
            Detail = "Marketplace registration failed: $($result.Output)"
            PinEvidence = "git:$Source@$Ref"
        }
    }
    try { $json = $result.Output | ConvertFrom-Json }
    catch {
        return [pscustomobject]@{
            Ready = $false
            Detail = 'Marketplace command returned invalid JSON.'
            PinEvidence = "git:$Source@$Ref"
        }
    }
    if ($json.marketplaceName -ne $ExpectedName) {
        return [pscustomobject]@{
            Ready = $false
            Detail = "Expected marketplace '$ExpectedName' but Codex reported '$($json.marketplaceName)'."
            PinEvidence = "git:$Source@$Ref"
        }
    }
    return [pscustomobject]@{
        Ready = $true
        Detail = $(if ($json.alreadyAdded) { 'Marketplace already registered; exact command completed.' } else { 'Marketplace registered.' })
        PinEvidence = $(if ([string]::IsNullOrWhiteSpace($Ref)) { "local:$Source" } else { "git:$Source@$Ref" })
    }
}

function Ensure-Plugin(
    [string] $Codex,
    [string] $PluginId,
    [string] $ExpectedVersion,
    [bool] $InstallNow) {
    if ($InstallNow) {
        $add = Invoke-External $Codex @('plugin', 'add', $PluginId, '--json')
        if ($add.ExitCode -ne 0) {
            return [pscustomobject]@{ Ready = $false; Detail = $add.Output; Version = '' }
        }
        try { $record = $add.Output | ConvertFrom-Json }
        catch { return [pscustomobject]@{ Ready = $false; Detail = 'Plugin add returned invalid JSON.'; Version = '' } }
        $version = [string] $record.version
        if ($version -ne $ExpectedVersion) {
            return [pscustomobject]@{
                Ready = $false
                Detail = "Expected version $ExpectedVersion but installed $version."
                Version = $version
            }
        }
        return [pscustomobject]@{ Ready = $true; Detail = 'Plugin installed and enabled.'; Version = $version }
    }

    $list = Invoke-External $Codex @('plugin', 'list', '--json')
    if ($list.ExitCode -ne 0) {
        return [pscustomobject]@{ Ready = $false; Detail = $list.Output; Version = '' }
    }
    try { $document = $list.Output | ConvertFrom-Json }
    catch { return [pscustomobject]@{ Ready = $false; Detail = 'Plugin list returned invalid JSON.'; Version = '' } }
    $record = @($document.installed | Where-Object { $_.pluginId -eq $PluginId })
    if ($record.Count -ne 1) {
        return [pscustomobject]@{ Ready = $false; Detail = 'Plugin is not installed exactly once.'; Version = '' }
    }
    $version = [string] $record[0].version
    $ready = $record[0].enabled -eq $true -and $version -eq $ExpectedVersion
    return [pscustomobject]@{
        Ready = $ready
        Detail = $(if ($ready) { 'Plugin is installed and enabled.' } else { "Expected enabled version $ExpectedVersion; found version $version." })
        Version = $version
    }
}

function Find-Python3 {
    foreach ($candidate in @(
        [pscustomobject]@{ Name = 'py'; Args = @('-3', '--version') },
        [pscustomobject]@{ Name = 'python'; Args = @('--version') },
        [pscustomobject]@{ Name = 'python3'; Args = @('--version') }
    )) {
        $path = Get-CommandPath $candidate.Name
        if ($path) {
            $result = Invoke-External $path $candidate.Args
            if ($result.ExitCode -eq 0 -and $result.Output -match 'Python\s+3\.') {
                return [pscustomobject]@{ Path = $path; Version = $result.Output }
            }
        }
    }
    return $null
}

function Get-GlobalNpmVersion([string] $Npm, [string] $Package) {
    $result = Invoke-External $Npm @('list', '-g', $Package, '--depth=0', '--json')
    if ($result.ExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($result.Output)) { return $null }
    try {
        $document = $result.Output | ConvertFrom-Json
        $property = $document.dependencies.PSObject.Properties[$Package]
        if ($property) { return [string] $property.Value.version }
    }
    catch { return $null }
    return $null
}

$root = Get-RepositoryRoot
Push-Location $root
try {
    $requiredFiles = @(
        'AGENTS.md',
        '.agents/plugins/marketplace.json',
        '.frontend-worker/v2/config.yml',
        '.frontend-worker/v2/implementation-lock.yml',
        '.frontend-worker/v2/authorization.template.json',
        '.frontend-worker/v2/provider-lock.json',
        '.frontend-worker/v2/tooling-lock.json',
        'docs/frontend-worker/GRANITE-NATIVE-FRONTEND-WORKER-V2-MASTER-PROMPT.md',
        'plugins/granite-native-frontend-worker/.codex-plugin/plugin.json'
    )
    $missing = @($requiredFiles | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) })
    if ($missing.Count -gt 0) { throw "Bootstrap is incomplete. Missing: $($missing -join ', ')" }

    $lock = Read-LockState $root
    if ($lock.State -ne 'closed') {
        throw "Initialization requires closed authorization. Close local state at $($lock.Path)."
    }

    $providerLock = Get-Content '.frontend-worker/v2/provider-lock.json' -Raw | ConvertFrom-Json
    if ($providerLock.profile -ne 'native-winui') { throw 'Provider lock profile must be native-winui.' }

    $providers = [System.Collections.Generic.List[object]]::new()
    $tools = [System.Collections.Generic.List[object]]::new()
    $blockers = [System.Collections.Generic.List[string]]::new()

    $git = Get-CommandPath 'git'
    $codex = Get-CommandPath 'codex'
    $dotnet = Get-CommandPath 'dotnet'
    $pwsh = Get-CommandPath 'pwsh'

    foreach ($tool in @(
        [pscustomobject]@{ Id = 'powershell-7'; Path = $pwsh },
        [pscustomobject]@{ Id = 'git'; Path = $git },
        [pscustomobject]@{ Id = 'codex-cli'; Path = $codex },
        [pscustomobject]@{ Id = 'dotnet-sdk'; Path = $dotnet }
    )) {
        $ready = -not [string]::IsNullOrWhiteSpace($tool.Path)
        Add-Result $tools $tool.Id $true $(if ($ready) { 'ready' } else { 'missing' }) $(if ($ready) { $tool.Path } else { "Required command '$($tool.Id)' is missing." })
        if (-not $ready) { $blockers.Add("$($tool.Id): missing") }
    }

    if ($dotnet) {
        $sdks = Invoke-External $dotnet @('--list-sdks')
        $versions = @($sdks.Output -split "`r?`n" | ForEach-Object { Get-VersionFromText $_ } | Where-Object { $_ })
        $supported = @($versions | Where-Object { [version] $_ -ge [version] '8.0.100' }).Count -gt 0
        if (-not $supported) { $blockers.Add('dotnet-sdk: .NET SDK 8.0.100 or newer is required') }
    }

    if ($blockers.Count -eq 0) {
        & (Join-Path $PSScriptRoot 'Test-GraniteNativeFrontendWorkerV2.ps1') -StructureOnly
        if ($LASTEXITCODE -ne 0) { $blockers.Add('bootstrap-structure: verification failed') }
    }

    if ($blockers.Count -eq 0) {
        $localMarket = Ensure-Marketplace $codex 'granite-native-frontend' $root '' $Install
        $localPlugin = Ensure-Plugin $codex 'granite-native-frontend-worker@granite-native-frontend' '2.1.0' $Install
        $ready = $localMarket.Ready -and $localPlugin.Ready
        Add-Result $providers 'granite-native-frontend-worker' $true $(if ($ready) { 'ready' } else { 'missing' }) "$($localMarket.Detail); $($localPlugin.Detail)" '2.1.0' $localPlugin.Version $localMarket.PinEvidence
        if (-not $ready) { $blockers.Add('granite-native-frontend-worker: not ready') }

        $winui = @($providerLock.providers | Where-Object id -eq 'microsoft-winui')[0]
        $winuiMarket = Ensure-Marketplace $codex $winui.marketplace $winui.repository $winui.ref $Install
        $winuiPlugin = Ensure-Plugin $codex "$($winui.plugin)@$($winui.marketplace)" $winui.reviewedVersion $Install
        $ready = $winuiMarket.Ready -and $winuiPlugin.Ready
        Add-Result $providers $winui.id $true $(if ($ready) { 'ready' } else { 'missing' }) "$($winuiMarket.Detail); $($winuiPlugin.Detail)" $winui.reviewedVersion $winuiPlugin.Version $winuiMarket.PinEvidence
        if (-not $ready) { $blockers.Add('microsoft-winui: not ready') }

        $superpowers = @($providerLock.providers | Where-Object id -eq 'superpowers')[0]
        $superPlugin = Ensure-Plugin $codex "$($superpowers.plugin)@$($superpowers.marketplace)" $superpowers.reviewedVersion $Install
        Add-Result $providers $superpowers.id $true $(if ($superPlugin.Ready) { 'ready' } else { 'missing' }) $superPlugin.Detail $superpowers.reviewedVersion $superPlugin.Version "official:$($superpowers.repository)@$($superpowers.ref)"
        if (-not $superPlugin.Ready) { $blockers.Add('superpowers: not ready') }

        $uncodixfy = @($providerLock.providers | Where-Object id -eq 'uncodixfy-winui-v2')[0]
        $adapterReady = Test-Path -LiteralPath $uncodixfy.adapterPath -PathType Leaf
        Add-Result $providers $uncodixfy.id $true $(if ($adapterReady) { 'ready' } else { 'missing' }) $(if ($adapterReady) { 'Bundled WinUI adapter is present; no upstream runtime install required.' } else { 'Bundled WinUI adapter is missing.' }) '' '' "source:$($uncodixfy.repository)@$($uncodixfy.ref)"
        if (-not $adapterReady) { $blockers.Add('uncodixfy-winui-v2: adapter missing') }

        if ($SkipOptionalProviders) {
            foreach ($id in @('stark', 'ui-ux-pro-max', 'figma', 'product-design')) {
                Add-Result $providers $id $false 'skipped' 'Skipped by operator.'
            }
        }
        else {
            $stark = @($providerLock.providers | Where-Object id -eq 'stark')[0]
            $starkMarket = Ensure-Marketplace $codex $stark.marketplace $stark.repository $stark.ref $Install
            $starkPlugin = Ensure-Plugin $codex "$($stark.plugin)@$($stark.marketplace)" $stark.reviewedVersion $Install
            $starkReady = $starkMarket.Ready -and $starkPlugin.Ready
            Add-Result $providers $stark.id $false $(if ($starkReady) { 'ready' } else { 'optional-unavailable' }) "$($starkMarket.Detail); $($starkPlugin.Detail)" $stark.reviewedVersion $starkPlugin.Version $starkMarket.PinEvidence

            $figma = @($providerLock.providers | Where-Object id -eq 'figma')[0]
            $figmaPlugin = Ensure-Plugin $codex "$($figma.plugin)@$($figma.marketplace)" $figma.reviewedVersion $Install
            Add-Result $providers $figma.id $false $(if ($figmaPlugin.Ready) { 'ready' } else { 'optional-unavailable' }) $figmaPlugin.Detail $figma.reviewedVersion $figmaPlugin.Version "official:$($figma.repository)@$($figma.ref)"

            $product = @($providerLock.providers | Where-Object id -eq 'product-design')[0]
            $productMarket = Ensure-Marketplace $codex $product.marketplace $product.repository $product.ref $Install
            $productPlugin = Ensure-Plugin $codex "$($product.plugin)@$($product.marketplace)" $product.reviewedVersion $Install
            $productReady = $productMarket.Ready -and $productPlugin.Ready
            Add-Result $providers $product.id $false $(if ($productReady) { 'ready' } else { 'optional-unavailable' }) "$($productMarket.Detail); $($productPlugin.Detail)" $product.reviewedVersion $productPlugin.Version $productMarket.PinEvidence

            $uipro = @($providerLock.providers | Where-Object id -eq 'ui-ux-pro-max')[0]
            $npm = Get-CommandPath 'npm'
            $python = Find-Python3
            if (-not $npm -or -not $python) {
                Add-Result $providers $uipro.id $false 'optional-unavailable' 'npm and Python 3 are required only when enabling UI/UX Pro Max.' $uipro.npmVersion
            }
            else {
                $version = Get-GlobalNpmVersion $npm $uipro.npmPackage
                if ($Install -and $version -ne $uipro.npmVersion) {
                    $npmInstall = Invoke-External $npm @('install', '-g', "$($uipro.npmPackage)@$($uipro.npmVersion)", '--ignore-scripts')
                    if ($npmInstall.ExitCode -eq 0) { $version = Get-GlobalNpmVersion $npm $uipro.npmPackage }
                }
                $uiproCommand = Get-CommandPath 'uipro'
                if ($Install -and $uiproCommand -and $version -eq $uipro.npmVersion) {
                    $init = Invoke-External $uiproCommand @('init', '--ai', 'codex', '--force', '--offline')
                    if ($init.ExitCode -ne 0) { $uiproCommand = $null }
                }
                $skillReady = Test-Path -LiteralPath $uipro.skillPath -PathType Leaf
                $ready = $uiproCommand -and $version -eq $uipro.npmVersion -and $skillReady
                Add-Result $providers $uipro.id $false $(if ($ready) { 'ready' } else { 'optional-unavailable' }) $(if ($ready) { 'Pinned CLI and project skill are ready.' } else { 'Pinned CLI/project skill could not be verified.' }) $uipro.npmVersion $version "npm:$($uipro.npmPackage)@$($uipro.npmVersion);source:$($uipro.repository)@$($uipro.ref)"
            }
        }
    }

    $status = [ordered]@{
        schemaVersion = 3
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        repositoryRoot = $root
        mode = $(if ($Install) { 'install' } else { 'verify-only' })
        implementationState = 'closed'
        providers = $providers
        tools = $tools
        blockers = $blockers
        restartRequired = $Install
    }
    $statusPath = '.frontend-worker/v2/provider-status.json'
    $status | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $statusPath -Encoding utf8NoBOM

    if ($blockers.Count -gt 0) {
        Write-Host 'INITIALIZATION BLOCKED'
        $blockers | ForEach-Object { Write-Host " - $_" }
        Write-Host 'No production UI or backend implementation was started.'
        exit 2
    }

    if ($Install) {
        Write-Host 'INITIALIZATION RESTART REQUIRED'
        Write-Host 'Providers were installed/reasserted. Start a fresh Codex session from the repository root, then run the verification script.'
    }
    else {
        Write-Host 'INITIALIZATION READY'
    }
    Write-Host "Provider status: $statusPath"
    Write-Host 'Implementation authorization remains CLOSED.'
    exit 0
}
finally {
    Pop-Location
}
