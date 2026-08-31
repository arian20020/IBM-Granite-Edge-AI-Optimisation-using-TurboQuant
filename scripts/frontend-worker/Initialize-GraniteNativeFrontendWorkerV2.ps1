[CmdletBinding()]
param(
    [switch] $Install,
    [switch] $SkipOptionalProviders
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Native([string] $File, [string[]] $Arguments) {
    $raw = & $File @Arguments 2>&1
    $code = $LASTEXITCODE
    [pscustomobject]@{
        ExitCode = $code
        Output = ($raw | Out-String).Trim()
        Command = "$File $($Arguments -join ' ')"
    }
}

function Get-CommandPath([string] $Name) {
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -eq $command) { return $null }
    $command.Source
}

function Get-RepositoryRoot {
    $git = Get-CommandPath 'git'
    if (-not $git) { throw 'Git is required.' }
    $result = Invoke-Native $git @('rev-parse', '--show-toplevel')
    if ($result.ExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($result.Output)) {
        throw 'Run this script from inside the Granite Edge AI repository.'
    }
    $result.Output
}

function Get-LockState([string] $Root) {
    $policyPath = Join-Path $Root '.frontend-worker/v2/implementation-lock.yml'
    $policy = Get-Content -LiteralPath $policyPath -Raw
    $match = [regex]::Match($policy, '(?m)^state_file:\s*(.+?)\s*$')
    if (-not $match.Success) { throw 'implementation-lock.yml does not declare state_file.' }
    $relative = $match.Groups[1].Value.Trim().Trim('"').Trim("'")
    $path = Join-Path $Root $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        return [pscustomobject]@{ Open = $false; Path = $path }
    }
    try { $record = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json }
    catch { throw "Authorization state is malformed: $path" }
    [pscustomobject]@{ Open = ($record.implementationAuthorized -eq $true); Path = $path }
}

function Add-Status(
    [System.Collections.Generic.List[object]] $List,
    [string] $Id,
    [bool] $Required,
    [string] $State,
    [string] $Detail,
    [string] $Expected = '',
    [string] $Installed = '',
    [string] $Pin = '') {
    $List.Add([ordered]@{
        id = $Id
        required = $Required
        state = $State
        detail = $Detail
        expectedVersion = $Expected
        installedVersion = $Installed
        pinEvidence = $Pin
    })
}

function Ensure-Marketplace(
    [string] $Codex,
    [string] $Git,
    [string] $Name,
    [string] $Source,
    [string] $Ref,
    [bool] $InstallNow) {
    if (-not $InstallNow) {
        return [pscustomobject]@{ Ready = $false; Detail = 'Run with -Install to assert the reviewed marketplace source.'; Pin = '' }
    }

    $arguments = @('plugin', 'marketplace', 'add', $Source)
    if ($Ref) { $arguments += @('--ref', $Ref) }
    $arguments += '--json'
    $result = Invoke-Native $Codex $arguments
    if ($result.ExitCode -ne 0) {
        return [pscustomobject]@{ Ready = $false; Detail = $result.Output; Pin = '' }
    }
    try { $record = $result.Output | ConvertFrom-Json }
    catch { return [pscustomobject]@{ Ready = $false; Detail = 'Marketplace add returned invalid JSON.'; Pin = '' } }
    if ($record.marketplaceName -ne $Name) {
        return [pscustomobject]@{ Ready = $false; Detail = "Expected marketplace '$Name'; Codex reported '$($record.marketplaceName)'."; Pin = '' }
    }

    $installedRoot = [string] $record.installedRoot
    if ($Ref) {
        if ([string]::IsNullOrWhiteSpace($installedRoot) -or -not (Test-Path -LiteralPath $installedRoot -PathType Container)) {
            return [pscustomobject]@{ Ready = $false; Detail = 'Codex did not return a readable installed marketplace root.'; Pin = '' }
        }
        $head = Invoke-Native $Git @('-C', $installedRoot, 'rev-parse', 'HEAD')
        if ($head.ExitCode -ne 0 -or $head.Output -cne $Ref) {
            return [pscustomobject]@{
                Ready = $false
                Detail = "Marketplace '$Name' is already registered at a different revision. Remove it explicitly, then rerun initialization."
                Pin = $(if ($head.ExitCode -eq 0) { $head.Output } else { '' })
            }
        }
    }

    [pscustomobject]@{
        Ready = $true
        Detail = $(if ($record.alreadyAdded) { 'Marketplace already registered and installed revision verified.' } else { 'Marketplace registered and installed revision verified.' })
        Pin = $(if ($Ref) { "git:$Source@$Ref" } else { "local:$Source" })
    }
}

function Ensure-Plugin(
    [string] $Codex,
    [string] $PluginId,
    [string] $ExpectedVersion,
    [bool] $InstallNow) {
    if ($InstallNow) {
        $result = Invoke-Native $Codex @('plugin', 'add', $PluginId, '--json')
        if ($result.ExitCode -ne 0) {
            return [pscustomobject]@{ Ready = $false; Detail = $result.Output; Version = '' }
        }
        try { $record = $result.Output | ConvertFrom-Json }
        catch { return [pscustomobject]@{ Ready = $false; Detail = 'Plugin add returned invalid JSON.'; Version = '' } }
        $version = [string] $record.version
        $ready = $record.enabled -eq $true -and $version -eq $ExpectedVersion
        return [pscustomobject]@{
            Ready = $ready
            Detail = $(if ($ready) { 'Plugin installed and enabled.' } else { "Expected enabled version $ExpectedVersion; found $version." })
            Version = $version
        }
    }

    $result = Invoke-Native $Codex @('plugin', 'list', '--json')
    if ($result.ExitCode -ne 0) {
        return [pscustomobject]@{ Ready = $false; Detail = $result.Output; Version = '' }
    }
    try { $document = $result.Output | ConvertFrom-Json }
    catch { return [pscustomobject]@{ Ready = $false; Detail = 'Plugin list returned invalid JSON.'; Version = '' } }
    $matches = @($document.installed | Where-Object { $_.pluginId -eq $PluginId })
    if ($matches.Count -ne 1) {
        return [pscustomobject]@{ Ready = $false; Detail = 'Plugin is not installed exactly once.'; Version = '' }
    }
    $version = [string] $matches[0].version
    $ready = $matches[0].enabled -eq $true -and $version -eq $ExpectedVersion
    [pscustomobject]@{
        Ready = $ready
        Detail = $(if ($ready) { 'Plugin is installed and enabled.' } else { "Expected enabled version $ExpectedVersion; found $version." })
        Version = $version
    }
}

function Get-Python3 {
    foreach ($candidate in @(
        [pscustomobject]@{ Name = 'py'; Args = @('-3', '--version') },
        [pscustomobject]@{ Name = 'python'; Args = @('--version') },
        [pscustomobject]@{ Name = 'python3'; Args = @('--version') }
    )) {
        $path = Get-CommandPath $candidate.Name
        if ($path) {
            $result = Invoke-Native $path $candidate.Args
            if ($result.ExitCode -eq 0 -and $result.Output -match '^Python\s+3\.') { return $path }
        }
    }
    $null
}

function Get-NpmPackageVersion([string] $Npm, [string] $Package) {
    $result = Invoke-Native $Npm @('list', '-g', $Package, '--depth=0', '--json')
    if ($result.ExitCode -ne 0) { return $null }
    try {
        $document = $result.Output | ConvertFrom-Json
        $property = $document.dependencies.PSObject.Properties[$Package]
        if ($property) { return [string] $property.Value.version }
    }
    catch { }
    $null
}

$root = Get-RepositoryRoot
Push-Location $root
try {
    $lock = Get-LockState $root
    if ($lock.Open) { throw "Initialization requires closed authorization. Close $($lock.Path)." }

    $providerLock = Get-Content '.frontend-worker/v2/provider-lock.json' -Raw | ConvertFrom-Json
    if ($providerLock.profile -ne 'native-winui') { throw 'Provider lock profile must be native-winui.' }

    $providerStatus = [System.Collections.Generic.List[object]]::new()
    $toolStatus = [System.Collections.Generic.List[object]]::new()
    $blockers = [System.Collections.Generic.List[string]]::new()

    $git = Get-CommandPath 'git'
    $codex = Get-CommandPath 'codex'
    $dotnet = Get-CommandPath 'dotnet'
    $pwsh = Get-CommandPath 'pwsh'

    $pwshReady = $pwsh -and $PSVersionTable.PSVersion -ge [version] '7.4.0'
    Add-Status $toolStatus 'powershell-7' $true $(if ($pwshReady) { 'ready' } else { 'missing' }) $(if ($pwshReady) { $PSVersionTable.PSVersion.ToString() } else { 'PowerShell 7.4 or newer is required.' })
    if (-not $pwshReady) { $blockers.Add('powershell-7: PowerShell 7.4 or newer is required') }

    foreach ($tool in @(
        [pscustomobject]@{ Id = 'git'; Path = $git },
        [pscustomobject]@{ Id = 'codex-cli'; Path = $codex },
        [pscustomobject]@{ Id = 'dotnet-sdk'; Path = $dotnet }
    )) {
        $ready = -not [string]::IsNullOrWhiteSpace($tool.Path)
        Add-Status $toolStatus $tool.Id $true $(if ($ready) { 'ready' } else { 'missing' }) $(if ($ready) { $tool.Path } else { "Required command '$($tool.Id)' is missing." })
        if (-not $ready) { $blockers.Add("$($tool.Id): missing") }
    }

    if ($dotnet) {
        $sdkResult = Invoke-Native $dotnet @('--list-sdks')
        $versions = @([regex]::Matches($sdkResult.Output, '(?m)^(\d+\.\d+\.\d+)') | ForEach-Object { [version] $_.Groups[1].Value })
        if (@($versions | Where-Object { $_ -ge [version] '8.0.100' }).Count -eq 0) {
            $blockers.Add('dotnet-sdk: .NET SDK 8.0.100 or newer is required')
        }
    }

    if ($blockers.Count -eq 0) {
        & (Join-Path $PSScriptRoot 'Test-GraniteNativeFrontendWorkerV2.ps1') -StructureOnly
        if (-not $?) { $blockers.Add('bootstrap-structure: verification failed') }
    }

    if ($blockers.Count -eq 0) {
        $localMarket = Ensure-Marketplace $codex $git 'granite-native-frontend' $root '' $Install
        $localPlugin = Ensure-Plugin $codex 'granite-native-frontend-worker@granite-native-frontend' '2.1.0' $Install
        $ready = $localMarket.Ready -and $localPlugin.Ready
        Add-Status $providerStatus 'granite-native-frontend-worker' $true $(if ($ready) { 'ready' } else { 'missing' }) "$($localMarket.Detail); $($localPlugin.Detail)" '2.1.0' $localPlugin.Version $localMarket.Pin
        if (-not $ready) { $blockers.Add('granite-native-frontend-worker: not ready') }

        $winui = @($providerLock.providers | Where-Object id -eq 'microsoft-winui')[0]
        $market = Ensure-Marketplace $codex $git $winui.marketplace $winui.repository $winui.ref $Install
        $plugin = Ensure-Plugin $codex "$($winui.plugin)@$($winui.marketplace)" $winui.reviewedVersion $Install
        $ready = $market.Ready -and $plugin.Ready
        Add-Status $providerStatus $winui.id $true $(if ($ready) { 'ready' } else { 'missing' }) "$($market.Detail); $($plugin.Detail)" $winui.reviewedVersion $plugin.Version $market.Pin
        if (-not $ready) { $blockers.Add('microsoft-winui: not ready') }

        $superpowers = @($providerLock.providers | Where-Object id -eq 'superpowers')[0]
        $plugin = Ensure-Plugin $codex "$($superpowers.plugin)@$($superpowers.marketplace)" $superpowers.reviewedVersion $Install
        Add-Status $providerStatus $superpowers.id $true $(if ($plugin.Ready) { 'ready' } else { 'missing' }) $plugin.Detail $superpowers.reviewedVersion $plugin.Version "official:$($superpowers.repository)@$($superpowers.ref)"
        if (-not $plugin.Ready) { $blockers.Add('superpowers: not ready') }

        $uncodixfy = @($providerLock.providers | Where-Object id -eq 'uncodixfy-winui-v2')[0]
        $adapterReady = Test-Path -LiteralPath $uncodixfy.adapterPath -PathType Leaf
        Add-Status $providerStatus $uncodixfy.id $true $(if ($adapterReady) { 'ready' } else { 'missing' }) $(if ($adapterReady) { 'Bundled WinUI adapter is present.' } else { 'Bundled WinUI adapter is missing.' }) '' '' "source:$($uncodixfy.repository)@$($uncodixfy.ref)"
        if (-not $adapterReady) { $blockers.Add('uncodixfy-winui-v2: adapter missing') }

        if ($SkipOptionalProviders) {
            foreach ($id in @('stark', 'ui-ux-pro-max', 'figma', 'product-design')) { Add-Status $providerStatus $id $false 'skipped' 'Skipped by operator.' }
        }
        else {
            foreach ($id in @('stark', 'product-design')) {
                $definition = @($providerLock.providers | Where-Object id -eq $id)[0]
                $market = Ensure-Marketplace $codex $git $definition.marketplace $definition.repository $definition.ref $Install
                $plugin = Ensure-Plugin $codex "$($definition.plugin)@$($definition.marketplace)" $definition.reviewedVersion $Install
                $ready = $market.Ready -and $plugin.Ready
                Add-Status $providerStatus $definition.id $false $(if ($ready) { 'ready' } else { 'optional-unavailable' }) "$($market.Detail); $($plugin.Detail)" $definition.reviewedVersion $plugin.Version $market.Pin
            }

            $figma = @($providerLock.providers | Where-Object id -eq 'figma')[0]
            $plugin = Ensure-Plugin $codex "$($figma.plugin)@$($figma.marketplace)" $figma.reviewedVersion $Install
            Add-Status $providerStatus $figma.id $false $(if ($plugin.Ready) { 'ready' } else { 'optional-unavailable' }) $plugin.Detail $figma.reviewedVersion $plugin.Version "official:$($figma.repository)@$($figma.ref)"

            $uipro = @($providerLock.providers | Where-Object id -eq 'ui-ux-pro-max')[0]
            $npm = Get-CommandPath 'npm'
            $python = Get-Python3
            $version = $(if ($npm) { Get-NpmPackageVersion $npm $uipro.npmPackage } else { $null })
            if ($Install -and $npm -and $python -and $version -ne $uipro.npmVersion) {
                $installResult = Invoke-Native $npm @('install', '-g', "$($uipro.npmPackage)@$($uipro.npmVersion)", '--ignore-scripts')
                if ($installResult.ExitCode -eq 0) { $version = Get-NpmPackageVersion $npm $uipro.npmPackage }
            }
            $uiproCommand = Get-CommandPath 'uipro'
            if ($Install -and $uiproCommand -and $version -eq $uipro.npmVersion) {
                $initResult = Invoke-Native $uiproCommand @('init', '--ai', 'codex', '--force', '--offline')
                if ($initResult.ExitCode -ne 0) { $uiproCommand = $null }
            }
            $ready = $uiproCommand -and $version -eq $uipro.npmVersion -and (Test-Path -LiteralPath $uipro.skillPath -PathType Leaf)
            Add-Status $providerStatus $uipro.id $false $(if ($ready) { 'ready' } else { 'optional-unavailable' }) $(if ($ready) { 'Pinned CLI and project skill are ready.' } else { 'npm, Python 3, pinned CLI, or project skill was unavailable.' }) $uipro.npmVersion $version "npm:$($uipro.npmPackage)@$($uipro.npmVersion);source:$($uipro.repository)@$($uipro.ref)"
        }
    }

    $status = [ordered]@{
        schemaVersion = 3
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        repositoryRoot = $root
        mode = $(if ($Install) { 'install' } else { 'verify-only' })
        implementationState = 'closed'
        providers = $providerStatus
        tools = $toolStatus
        blockers = $blockers
        restartRequired = $Install
    }
    $status | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath '.frontend-worker/v2/provider-status.json' -Encoding utf8NoBOM

    if ($blockers.Count -gt 0) {
        Write-Host 'INITIALIZATION BLOCKED'
        $blockers | ForEach-Object { Write-Host " - $_" }
        Write-Host 'No production UI or backend implementation was started.'
        exit 2
    }

    if ($Install) {
        Write-Host 'INITIALIZATION RESTART REQUIRED'
        Write-Host 'Start a fresh Codex session from the repository root, then run scripts/Test-GraniteNativeFrontendWorkerV2.ps1.'
    }
    else {
        Write-Host 'INITIALIZATION READY'
    }
    Write-Host 'Implementation authorization remains CLOSED.'
    exit 0
}
finally {
    Pop-Location
}
