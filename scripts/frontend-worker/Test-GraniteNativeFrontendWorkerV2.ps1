[CmdletBinding()]
param(
    [switch] $StructureOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$requiredFiles = @(
    'AGENTS.md',
    '.agents\plugins\marketplace.json',
    'plugins\granite-native-frontend-worker\.codex-plugin\plugin.json',
    'plugins\granite-native-frontend-worker\agents\openai.yaml',
    'plugins\granite-native-frontend-worker\skills\granite-native-frontend-master\SKILL.md',
    'plugins\granite-native-frontend-worker\skills\frontend-contract-guardian\SKILL.md',
    'plugins\granite-native-frontend-worker\skills\winui-design-director\SKILL.md',
    'plugins\granite-native-frontend-worker\skills\winui-xaml-implementer\SKILL.md',
    'plugins\granite-native-frontend-worker\skills\winui-accessibility-auditor\SKILL.md',
    'plugins\granite-native-frontend-worker\skills\winui-runtime-visual-qa\SKILL.md',
    'plugins\granite-native-frontend-worker\skills\frontend-release-gate\SKILL.md',
    '.frontend-worker\v2\config.yml',
    '.frontend-worker\v2\implementation-lock.yml',
    '.frontend-worker\v2\boundary-policy.yml',
    '.frontend-worker\v2\provider-lock.json',
    '.frontend-worker\v2\tooling-lock.json',
    'docs\frontend-worker\GRANITE-NATIVE-FRONTEND-WORKER-V2-MASTER-PROMPT.md'
)

$missing = @()
foreach ($relativePath in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $RepoRoot $relativePath))) {
        $missing += $relativePath
    }
}
if ($missing.Count -gt 0) {
    throw "Missing required frontend-worker files:`n - $($missing -join "`n - ")"
}

$marketplace = Get-Content -Raw -LiteralPath `
    (Join-Path $RepoRoot '.agents\plugins\marketplace.json') | ConvertFrom-Json
if ($marketplace.name -ne 'granite-native-frontend') {
    throw 'Unexpected local marketplace name.'
}
$localEntries = @($marketplace.plugins | Where-Object name -eq 'granite-native-frontend-worker')
if ($localEntries.Count -ne 1) {
    throw 'Local marketplace must expose exactly one Granite frontend worker entry.'
}
$localEntry = $localEntries[0]
$allowedMarketplaceFields = @('name', 'source', 'policy', 'category')
$unexpectedMarketplaceFields = @($localEntry.PSObject.Properties.Name | Where-Object {
    $_ -notin $allowedMarketplaceFields
})
if ($unexpectedMarketplaceFields.Count -gt 0) {
    throw "Unsupported local marketplace entry fields: $($unexpectedMarketplaceFields -join ', ')"
}
if ($localEntry.source.source -ne 'local' -or
    $localEntry.source.path -ne './plugins/granite-native-frontend-worker') {
    throw 'Local marketplace source does not point at the repo-local Granite plugin.'
}
if ($localEntry.policy.installation -ne 'AVAILABLE' -or
    $localEntry.policy.authentication -ne 'ON_INSTALL') {
    throw 'Unexpected local marketplace policy.'
}

$plugin = Get-Content -Raw -LiteralPath `
    (Join-Path $RepoRoot 'plugins\granite-native-frontend-worker\.codex-plugin\plugin.json') |
    ConvertFrom-Json
if ($plugin.name -ne 'granite-native-frontend-worker' -or $plugin.version -ne '2.0.0') {
    throw 'Unexpected Granite frontend plugin identity or version.'
}
if ($plugin.skills -ne './skills/') {
    throw 'Granite frontend plugin must load its repo-local skill root.'
}
$requiredInterfaceFields = @(
    'displayName', 'shortDescription', 'longDescription',
    'developerName', 'category', 'capabilities', 'defaultPrompt'
)
$missingInterfaceFields = @($requiredInterfaceFields | Where-Object {
    $null -eq $plugin.interface.PSObject.Properties[$_]
})
if ($missingInterfaceFields.Count -gt 0) {
    throw "Plugin interface is missing required fields: $($missingInterfaceFields -join ', ')"
}

$providerLock = Get-Content -Raw -LiteralPath `
    (Join-Path $RepoRoot '.frontend-worker\v2\provider-lock.json') | ConvertFrom-Json
$toolingLock = Get-Content -Raw -LiteralPath `
    (Join-Path $RepoRoot '.frontend-worker\v2\tooling-lock.json') | ConvertFrom-Json
if ($providerLock.profile -ne 'native-winui' -or $toolingLock.profile -ne 'native-winui') {
    throw 'Provider/tooling lock profile must be native-winui.'
}

$unpinned = @($providerLock.providers | Where-Object {
    $_.repository -and (
        [string]::IsNullOrWhiteSpace($_.ref) -or
        $_.ref -in @('main', 'master', 'latest') -or
        $_.ref -notmatch '^[0-9a-fA-F]{40}$'
    )
})
if ($unpinned.Count -gt 0) {
    throw "Unpinned or nonimmutable provider refs: $($unpinned.id -join ', ')"
}

$writeProviders = @($providerLock.providers | Where-Object writeAuthority -eq $true)
if ($writeProviders.Count -gt 0) {
    throw "External providers cannot have production write authority: $($writeProviders.id -join ', ')"
}

$lockText = Get-Content -Raw -LiteralPath `
    (Join-Path $RepoRoot '.frontend-worker\v2\implementation-lock.yml')
if ($lockText -notmatch '(?m)^implementation_authorized:\s*false\s*$') {
    throw 'The implementation lock must remain closed after initialization.'
}

$agentText = Get-Content -Raw -LiteralPath (Join-Path $RepoRoot 'AGENTS.md')
if ($agentText -notmatch 'granite-native-frontend-master' -or
    $agentText -notmatch 'implementation_authorized') {
    throw 'Root AGENTS.md does not route frontend work through the locked Granite master.'
}

# Verify the entire bootstrap branch, staged/unstaged changes and untracked files.
$configText = Get-Content -Raw -LiteralPath `
    (Join-Path $RepoRoot '.frontend-worker\v2\config.yml')
if ($configText -notmatch '(?m)^target_base_branch:\s*(.+?)\s*$') {
    throw 'Unable to read target_base_branch from config.yml.'
}
$baseBranch = $Matches[1].Trim().Trim('"').Trim("'")
$baseCandidates = @("refs/remotes/origin/$baseBranch", "refs/heads/$baseBranch")
$baseCommit = $null
foreach ($candidate in $baseCandidates) {
    $resolved = & git -C $RepoRoot rev-parse --verify --quiet $candidate 2>$null
    if ($LASTEXITCODE -eq 0 -and $resolved) {
        $baseCommit = ($resolved | Select-Object -First 1).Trim()
        break
    }
}
if ($null -eq $baseCommit) {
    throw "Unable to resolve base branch '$baseBranch'. Fetch or create the base ref before verification."
}

$mergeBaseOutput = & git -C $RepoRoot merge-base HEAD $baseCommit 2>$null
if ($LASTEXITCODE -ne 0 -or -not $mergeBaseOutput) {
    throw "Unable to calculate merge-base against '$baseBranch'."
}
$mergeBaseCommit = ($mergeBaseOutput | Select-Object -First 1).Trim()

$changedPaths = @()
$changedPaths += & git -C $RepoRoot diff --name-only "$mergeBaseCommit...HEAD"
$changedPaths += & git -C $RepoRoot diff --name-only
$changedPaths += & git -C $RepoRoot diff --cached --name-only
$changedPaths += & git -C $RepoRoot ls-files --others --exclude-standard
$changedPaths = @($changedPaths | Where-Object {
    -not [string]::IsNullOrWhiteSpace($_)
} | Sort-Object -Unique)

$allowedBootstrapPatterns = @(
    '^AGENTS\.md$',
    '^\.agents/plugins/',
    '^\.agents/skills/',
    '^\.codex/agents/',
    '^\.codex/skills/',
    '^\.frontend-worker/',
    '^plugins/granite-native-frontend-worker/',
    '^scripts/frontend-worker/',
    '^docs/frontend-worker/',
    '^docs/superpowers/plans/'
)
$unexpectedBootstrapPaths = @($changedPaths | Where-Object {
    $path = $_
    -not ($allowedBootstrapPatterns | Where-Object { $path -match $_ })
})
if ($unexpectedBootstrapPaths.Count -gt 0) {
    throw "Files outside the initialization allowlist changed:`n - $($unexpectedBootstrapPaths -join "`n - ")"
}

$forbidden = @($changedPaths | Where-Object {
    $_ -match '^IBM Granite with TurboQuant \(Intel\)/' -or
    $_ -match '^(shared|infrastructure|runtime|workers|experiments)/' -or
    $_ -match '(^|/)(Package\.appxmanifest|app\.manifest|App\.xaml\.cs)$' -or
    $_ -match '\.(csproj|sln|slnx|targets)$'
})
if ($forbidden.Count -gt 0) {
    throw "Production, project, packaging or backend files changed during initialization:`n - $($forbidden -join "`n - ")"
}

if (-not $StructureOnly) {
    $statusPath = Join-Path $RepoRoot '.frontend-worker\v2\provider-status.json'
    if (-not (Test-Path -LiteralPath $statusPath)) {
        throw 'Provider status is missing. Run the initializer with -Install first.'
    }

    $status = Get-Content -Raw -LiteralPath $statusPath | ConvertFrom-Json
    if ($status.schemaVersion -ne 2 -or $status.mode -ne 'install') {
        throw 'Provider status must come from an authoritative -Install run using schema version 2.'
    }
    if ($status.implementationLock -ne 'closed') {
        throw 'Provider status does not confirm a closed implementation lock.'
    }

    $localRecord = @($status.providers | Where-Object id -eq 'granite-native-frontend-worker')
    if ($localRecord.Count -ne 1 -or
        $localRecord[0].state -ne 'ready' -or
        $localRecord[0].installedVersion -ne '2.0.0') {
        throw 'The repo-local Granite frontend plugin is not ready at version 2.0.0.'
    }

    $providerFailures = @()
    foreach ($provider in @($providerLock.providers | Where-Object required -eq $true)) {
        $record = @($status.providers | Where-Object id -eq $provider.id)
        if ($record.Count -ne 1 -or $record[0].state -ne 'ready') {
            $providerFailures += "$($provider.id): not ready"
            continue
        }

        $reviewedVersionProperty = $provider.PSObject.Properties['reviewedVersion']
        $npmVersionProperty = $provider.PSObject.Properties['npmVersion']
        $expectedVersion = if ($reviewedVersionProperty) {
            [string] $reviewedVersionProperty.Value
        }
        elseif ($npmVersionProperty) {
            [string] $npmVersionProperty.Value
        }
        else {
            ''
        }
        if ($expectedVersion -and $record[0].installedVersion -ne $expectedVersion) {
            $providerFailures += "$($provider.id): expected version $expectedVersion, got $($record[0].installedVersion)"
        }

        if ($provider.id -eq 'uncodixfy' -and $record[0].installedRef -ne $provider.ref) {
            $providerFailures += "$($provider.id): expected ref $($provider.ref), got $($record[0].installedRef)"
        }
        if ($provider.id -eq 'microsoft-winui' -and
            $record[0].pinEvidence -notmatch [Regex]::Escape($provider.ref)) {
            $providerFailures += "$($provider.id): pinned marketplace ref evidence is missing"
        }
        if ($provider.id -eq 'stark' -and
            $record[0].pinEvidence -notmatch [Regex]::Escape($provider.distributionRef)) {
            $providerFailures += "$($provider.id): pinned distribution ref evidence is missing"
        }
    }
    if ($providerFailures.Count -gt 0) {
        throw "Required provider verification failed:`n - $($providerFailures -join "`n - ")"
    }

    $toolFailures = @()
    foreach ($tool in @($toolingLock.tools | Where-Object requiredForInitialization -eq $true)) {
        $record = @($status.tools | Where-Object id -eq $tool.id)
        if ($record.Count -ne 1 -or $record[0].state -ne 'ready') {
            $toolFailures += $tool.id
        }
    }
    if ($toolFailures.Count -gt 0) {
        throw "Required initialization tools are not ready: $($toolFailures -join ', ')"
    }
}

Write-Host 'Granite Native Frontend Worker v2 structure: PASS'
Write-Host 'Marketplace and plugin manifest shape: PASS'
Write-Host 'Provider revisions pinned: PASS'
Write-Host 'External provider write authority disabled: PASS'
Write-Host 'Implementation lock closed: PASS'
Write-Host 'Bootstrap allowlist and production/backend diff: PASS'
if (-not $StructureOnly) {
    Write-Host 'Authoritative provider and initialization tooling readiness: PASS'
}
exit 0
