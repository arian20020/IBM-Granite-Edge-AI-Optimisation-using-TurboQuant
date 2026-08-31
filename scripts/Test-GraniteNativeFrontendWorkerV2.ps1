[CmdletBinding()]
param(
    [string]$BaseRef = 'integration/ucl-cross-route-native-validation-v1',
    [switch]$SkipGitDiff
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

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$root = Get-RepositoryRoot
Push-Location $root
try {
    $requiredFiles = @(
        'AGENTS.md',
        '.agents/plugins/marketplace.json',
        '.frontend-worker/v2/README.md',
        '.frontend-worker/v2/config.yml',
        '.frontend-worker/v2/authorization.json',
        '.frontend-worker/v2/provider-lock.json',
        '.frontend-worker/v2/boundary-policy.yml',
        '.frontend-worker/v2/MASTER_PROMPT.md',
        '.frontend-worker/v2/IMPLEMENTATION_START_COMMAND.md',
        'plugins/granite-native-frontend-worker/.codex-plugin/plugin.json',
        'plugins/granite-native-frontend-worker/agents/openai.yaml',
        'plugins/granite-native-frontend-worker/README.md',
        'plugins/granite-native-frontend-worker/THIRD_PARTY_NOTICES.md',
        'plugins/granite-native-frontend-worker/skills/granite-native-frontend-initializer/SKILL.md',
        'plugins/granite-native-frontend-worker/skills/granite-native-frontend-master/SKILL.md',
        'plugins/granite-native-frontend-worker/skills/frontend-contract-guardian/SKILL.md',
        'plugins/granite-native-frontend-worker/skills/winui-design-director/SKILL.md',
        'plugins/granite-native-frontend-worker/skills/winui-xaml-implementer/SKILL.md',
        'plugins/granite-native-frontend-worker/skills/winui-accessibility-auditor/SKILL.md',
        'plugins/granite-native-frontend-worker/skills/winui-runtime-visual-qa/SKILL.md',
        'plugins/granite-native-frontend-worker/skills/frontend-release-gate/SKILL.md'
    )

    foreach ($path in $requiredFiles) {
        Assert-True (Test-Path $path -PathType Leaf) "Missing required bootstrap file: $path"
    }

    $marketplace = Get-Content '.agents/plugins/marketplace.json' -Raw | ConvertFrom-Json
    $plugin = Get-Content 'plugins/granite-native-frontend-worker/.codex-plugin/plugin.json' -Raw | ConvertFrom-Json
    $providers = Get-Content '.frontend-worker/v2/provider-lock.json' -Raw | ConvertFrom-Json
    $authorization = Get-Content '.frontend-worker/v2/authorization.json' -Raw | ConvertFrom-Json

    Assert-True ($marketplace.name -eq 'granite-native-frontend') 'Unexpected local marketplace name.'
    Assert-True ($marketplace.plugins.name -contains 'granite-native-frontend-worker') 'Worker is not registered in the local marketplace.'
    Assert-True ($plugin.name -eq 'granite-native-frontend-worker') 'Unexpected plugin manifest name.'
    Assert-True ($plugin.version -eq '2.0.0') 'Unexpected plugin version.'
    Assert-True ($providers.active_profile -eq 'native-winui') 'Provider lock is not using native-winui.'
    Assert-True ($authorization.implementation_authorized -eq $false) 'Bootstrap verification requires the implementation lock to remain closed.'
    Assert-True ($authorization.authorized_surfaces.Count -eq 0) 'Bootstrap verification found an authorized production surface.'

    foreach ($skill in Get-ChildItem 'plugins/granite-native-frontend-worker/skills' -Filter 'SKILL.md' -Recurse) {
        $text = Get-Content $skill.FullName -Raw
        Assert-True ($text.StartsWith("---`n") -or $text.StartsWith("---`r`n")) "Skill frontmatter is missing: $($skill.FullName)"
        Assert-True ($text -match '(?m)^name:\s*[-a-z0-9]+\s*$') "Skill name is missing or invalid: $($skill.FullName)"
        Assert-True ($text -match '(?m)^description:\s*.+$') "Skill description is missing: $($skill.FullName)"
    }

    if (-not $SkipGitDiff) {
        & git rev-parse --verify $BaseRef *> $null
        if ($LASTEXITCODE -ne 0) { throw "Base ref does not exist locally: $BaseRef" }

        $changed = @(& git diff --name-only "$BaseRef...HEAD")
        $allowedExact = @('AGENTS.md', '.agents/plugins/marketplace.json')
        $allowedPrefixes = @(
            '.frontend-worker/',
            'plugins/granite-native-frontend-worker/',
            'docs/superpowers/specs/',
            'docs/superpowers/plans/'
        )

        foreach ($path in $changed) {
            $allowed = $allowedExact -contains $path
            if (-not $allowed) {
                $allowed = @($allowedPrefixes | Where-Object { $path.StartsWith($_, [System.StringComparison]::Ordinal) }).Count -gt 0
            }
            if (-not $allowed) {
                $allowed = $path -match '^scripts/(Initialize|Test|Authorize)-GraniteNativeFrontendWorkerV2\.ps1$'
            }
            Assert-True $allowed "Bootstrap branch changed a forbidden path: $path"
        }

        $productionChanges = @($changed | Where-Object {
            $_ -like 'IBM Granite with TurboQuant (Intel)/*' -or
            $_ -like 'shared/*' -or
            $_ -like 'infrastructure/*' -or
            $_ -like 'runtime/*' -or
            $_ -like 'workers/*' -or
            $_ -like 'tests/*' -or
            $_ -like 'experiments/*'
        })
        Assert-True ($productionChanges.Count -eq 0) "Production or backend files changed during bootstrap: $($productionChanges -join ', ')"
    }

    & git diff --check
    Assert-True ($LASTEXITCODE -eq 0) 'git diff --check failed.'

    Write-Host 'PASS: Granite Native Frontend Worker v2 bootstrap is structurally valid.'
    Write-Host 'PASS: Implementation lock is closed.'
    if (-not $SkipGitDiff) {
        Write-Host 'PASS: No production application, backend, test, or experiment path changed.'
    }
}
finally {
    Pop-Location
}
