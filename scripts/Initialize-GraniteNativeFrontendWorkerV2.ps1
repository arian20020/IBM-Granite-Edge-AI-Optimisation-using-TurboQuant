[CmdletBinding()]
param(
    [switch]$InstallUiUxProMax,
    [switch]$PrintProviderCommands,
    [switch]$SkipMachineChecks
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepositoryRoot {
    $root = (& git rev-parse --show-toplevel 2>$null)
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($root)) {
        throw 'Run this script from inside the Granite Edge AI repository.'
    }
    return $root.Trim()
}

function Test-CommandAvailable([string]$Name) {
    return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

function Get-FirstVersion([string]$Text) {
    $match = [regex]::Match($Text, '(?<!\d)(\d+\.\d+(?:\.\d+){0,2})(?!\d)')
    if ($match.Success) { return $match.Groups[1].Value }
    return $null
}

$root = Get-RepositoryRoot
Push-Location $root
try {
    $requiredFiles = @(
        'AGENTS.md',
        '.agents/plugins/marketplace.json',
        '.frontend-worker/v2/MASTER_PROMPT.md',
        '.frontend-worker/v2/authorization.json',
        '.frontend-worker/v2/boundary-policy.yml',
        '.frontend-worker/v2/provider-lock.json',
        'plugins/granite-native-frontend-worker/.codex-plugin/plugin.json',
        'plugins/granite-native-frontend-worker/skills/granite-native-frontend-master/SKILL.md'
    )

    $missing = @($requiredFiles | Where-Object { -not (Test-Path $_ -PathType Leaf) })
    if ($missing.Count -gt 0) {
        throw "Frontend worker bootstrap is incomplete. Missing: $($missing -join ', ')"
    }

    $authorization = Get-Content '.frontend-worker/v2/authorization.json' -Raw | ConvertFrom-Json
    if ($authorization.implementation_authorized -ne $false) {
        throw 'Initialization must start with the implementation lock closed.'
    }

    $providerLock = Get-Content '.frontend-worker/v2/provider-lock.json' -Raw | ConvertFrom-Json
    if ($providerLock.active_profile -ne 'native-winui') {
        throw 'The provider lock is not using the native-winui profile.'
    }

    Write-Host 'Granite Native Frontend Worker v2 repository bootstrap: OK'
    Write-Host 'Implementation lock: CLOSED'
    Write-Host 'Production application edits: NOT AUTHORIZED'

    if (-not $SkipMachineChecks) {
        $checks = [ordered]@{}
        foreach ($command in @('git', 'dotnet', 'codex', 'winapp', 'node', 'npm')) {
            $checks[$command] = Test-CommandAvailable $command
        }
        $checks['python'] = (Test-CommandAvailable 'python') -or (Test-CommandAvailable 'py') -or (Test-CommandAvailable 'python3')

        if ($checks.dotnet) {
            $sdkLines = @(& dotnet --list-sdks 2>$null)
            $sdkVersions = @($sdkLines | ForEach-Object { Get-FirstVersion $_ } | Where-Object { $_ })
            $checks['dotnet_8_or_newer'] = @($sdkVersions | Where-Object { [version]$_ -ge [version]'8.0.100' }).Count -gt 0
        }

        if ($checks.winapp) {
            $winappText = (& winapp --version 2>&1 | Out-String)
            $winappVersion = Get-FirstVersion $winappText
            $checks['winapp_0_6_or_newer'] = $null -ne $winappVersion -and [version]$winappVersion -ge [version]'0.6.0'
        }

        Write-Host ''
        Write-Host 'Machine readiness:'
        foreach ($entry in $checks.GetEnumerator()) {
            $mark = if ($entry.Value) { 'OK' } else { 'MISSING' }
            Write-Host ("  {0,-24} {1}" -f $entry.Key, $mark)
        }
    }

    if ($InstallUiUxProMax) {
        if (-not (Test-CommandAvailable 'npm')) {
            throw 'npm is required for the pinned UI/UX Pro Max installation.'
        }
        Write-Host 'Installing pinned UI/UX Pro Max CLI 2.5.0...'
        & npm install --global 'ui-ux-pro-max-cli@2.5.0'
        if ($LASTEXITCODE -ne 0) { throw 'UI/UX Pro Max CLI installation failed.' }
        if (-not (Test-CommandAvailable 'uipro')) {
            throw 'The uipro command was not found after installation.'
        }
        & uipro init --ai codex
        if ($LASTEXITCODE -ne 0) { throw 'Project-local UI/UX Pro Max initialisation failed.' }
        Write-Host 'UI/UX Pro Max project skill initialised. Review the generated diff before committing.'
    }

    if ($PrintProviderCommands) {
        Write-Host ''
        Write-Host 'Review these commands before running them in the current Codex CLI:'
        Write-Host '  codex plugin marketplace add .'
        Write-Host '  codex plugin install granite-native-frontend-worker --source granite-native-frontend'
        Write-Host '  codex plugin marketplace add microsoft/win-dev-skills --ref 68ae65d5c65ee87c3265a7f5abe3aaf97c7e6932'
        Write-Host '  # Then enable the winui plugin from the Codex plugin directory.'
        Write-Host '  codex plugin marketplace add https://github.com/hashgraph-online/awesome-codex-plugins.git --ref 0ff99e11ba9c2ef21446dc9960033c5c18183a97 --sparse .agents/plugins --sparse plugins'
        Write-Host '  codex plugin install stark --source awesome-codex-plugins'
        Write-Host '  npm install --global ui-ux-pro-max-cli@2.5.0'
        Write-Host '  uipro init --ai codex'
        Write-Host ''
        Write-Host 'Enable Figma and Product Design through the supported Codex plugin directory when needed.'
        Write-Host 'Do not install the original Uncodixfy skill for native execution; the reviewed WinUI adapter is already local.'
        Write-Host 'If Codex CLI syntax has changed, use `codex plugin --help` or the plugin directory rather than guessing.'
    }

    Write-Host ''
    Write-Host 'Next verification command:'
    Write-Host '  ./scripts/Test-GraniteNativeFrontendWorkerV2.ps1'
}
finally {
    Pop-Location
}
