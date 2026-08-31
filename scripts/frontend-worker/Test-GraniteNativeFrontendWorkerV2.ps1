[CmdletBinding()]
param(
    [switch] $StructureOnly,
    [string] $BaseRef
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-True([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
}

function Invoke-Git([string] $Root, [string[]] $Arguments) {
    $output = & git -C $Root @Arguments 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "git $($Arguments -join ' ') failed:`n$output" }
    return $output.Trim()
}

function Get-RepositoryRoot {
    $root = & git rev-parse --show-toplevel 2>$null | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($root)) { throw 'Run from inside the Granite Edge AI repository.' }
    return $root.Trim()
}

function Resolve-BaseCommit([string] $Root, [string] $Requested) {
    foreach ($candidate in @($Requested, "origin/$Requested", "refs/remotes/origin/$Requested", "refs/heads/$Requested")) {
        $value = & git -C $Root rev-parse --verify --quiet "$candidate^{commit}" 2>$null | Select-Object -First 1
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($value)) { return $value.Trim() }
    }
    throw "Unable to resolve base ref '$Requested'."
}

function Get-ChangedPaths([string] $Root, [string] $BaseCommit) {
    $mergeBase = Invoke-Git $Root @('merge-base', 'HEAD', $BaseCommit)
    $paths = @()
    $paths += & git -C $Root diff --name-only "$mergeBase...HEAD"
    $paths += & git -C $Root diff --name-only
    $paths += & git -C $Root diff --cached --name-only
    $paths += & git -C $Root ls-files --others --exclude-standard
    return @($paths | Where-Object { $_ } | ForEach-Object { $_.Replace('\', '/') } | Sort-Object -Unique)
}

function Get-AuthorizationState([string] $Root) {
    $policy = Get-Content -LiteralPath (Join-Path $Root '.frontend-worker/v2/implementation-lock.yml') -Raw
    $match = [regex]::Match($policy, '(?m)^state_file:\s*(.+?)\s*$')
    Assert-True $match.Success 'implementation-lock.yml does not declare state_file.'
    $relative = $match.Groups[1].Value.Trim().Trim('"').Trim("'")
    $path = Join-Path $Root $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        return [pscustomobject]@{ Open = $false; Path = $path }
    }
    $record = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    return [pscustomobject]@{ Open = ($record.implementationAuthorized -eq $true); Path = $path }
}

function Assert-SameSet([object[]] $Actual, [object[]] $Expected, [string] $Label) {
    $actualValues = @($Actual | ForEach-Object { [string] $_ } | Sort-Object)
    $expectedValues = @($Expected | ForEach-Object { [string] $_ } | Sort-Object)
    Assert-True (($actualValues -join '|') -eq ($expectedValues -join '|')) "$Label mismatch. Actual: $($actualValues -join ', ')"
}

$root = Get-RepositoryRoot
Push-Location $root
try {
    $requiredFiles = @(
        'AGENTS.md',
        '.agents/plugins/marketplace.json',
        '.agents/skills/.gitignore',
        '.frontend-worker/v2/README.md',
        '.frontend-worker/v2/config.yml',
        '.frontend-worker/v2/implementation-lock.yml',
        '.frontend-worker/v2/authorization.template.json',
        '.frontend-worker/v2/provider-lock.json',
        '.frontend-worker/v2/tooling-lock.json',
        '.frontend-worker/v2/boundary-policy.yml',
        '.frontend-worker/v2/IMPLEMENTATION_START_COMMAND.md',
        'docs/frontend-worker/GRANITE-NATIVE-FRONTEND-WORKER-V2-MASTER-PROMPT.md',
        'plugins/granite-native-frontend-worker/.codex-plugin/plugin.json',
        'plugins/granite-native-frontend-worker/agents/openai.yaml',
        'plugins/granite-native-frontend-worker/README.md',
        'plugins/granite-native-frontend-worker/THIRD_PARTY_NOTICES.md',
        'plugins/granite-native-frontend-worker/adapters/providers.yml',
        'plugins/granite-native-frontend-worker/rules/rule-registry.yml',
        'scripts/Authorize-GraniteNativeFrontendWorkerV2.ps1',
        'scripts/Initialize-GraniteNativeFrontendWorkerV2.ps1',
        'scripts/Test-GraniteNativeFrontendWorkerV2.ps1',
        'scripts/frontend-worker/Initialize-GraniteNativeFrontendWorkerV2.ps1',
        'scripts/frontend-worker/Test-GraniteNativeFrontendWorkerV2.ps1',
        'scripts/frontend-worker/Test-GraniteFrontendGuard.ps1',
        'tools/GraniteFrontendGuard/GraniteFrontendGuard.csproj',
        'tools/GraniteFrontendGuard/Program.cs',
        '.github/workflows/frontend-worker-bootstrap.yml'
    )
    foreach ($path in $requiredFiles) {
        Assert-True (Test-Path -LiteralPath $path -PathType Leaf) "Missing required bootstrap file: $path"
    }

    foreach ($obsolete in @('.frontend-worker/v2/MASTER_PROMPT.md', '.frontend-worker/v2/authorization.json', '.codex/skills/.gitignore')) {
        Assert-True (-not (Test-Path -LiteralPath $obsolete)) "Obsolete duplicate/state file remains: $obsolete"
    }

    $marketplace = Get-Content '.agents/plugins/marketplace.json' -Raw | ConvertFrom-Json
    $plugin = Get-Content 'plugins/granite-native-frontend-worker/.codex-plugin/plugin.json' -Raw | ConvertFrom-Json
    $providers = Get-Content '.frontend-worker/v2/provider-lock.json' -Raw | ConvertFrom-Json
    $tools = Get-Content '.frontend-worker/v2/tooling-lock.json' -Raw | ConvertFrom-Json
    $template = Get-Content '.frontend-worker/v2/authorization.template.json' -Raw | ConvertFrom-Json

    Assert-True ($marketplace.name -eq 'granite-native-frontend') 'Unexpected local marketplace name.'
    $entries = @($marketplace.plugins | Where-Object name -eq 'granite-native-frontend-worker')
    Assert-True ($entries.Count -eq 1) 'Local marketplace must contain exactly one Granite worker.'
    Assert-True ($entries[0].source.source -eq 'local') 'Granite worker must use a local source.'
    Assert-True ($entries[0].source.path -eq './plugins/granite-native-frontend-worker') 'Granite worker marketplace path is incorrect.'
    Assert-True ($entries[0].policy.products -contains 'CODEX') 'Granite worker must be scoped to CODEX.'

    Assert-True ($plugin.name -eq 'granite-native-frontend-worker') 'Unexpected plugin name.'
    Assert-True ($plugin.version -eq '2.1.0') 'Unexpected plugin version.'
    Assert-True ($plugin.skills -eq './skills/') 'Plugin skill root is incorrect.'
    foreach ($field in @('displayName', 'shortDescription', 'longDescription', 'developerName', 'category', 'capabilities', 'defaultPrompt')) {
        Assert-True ($null -ne $plugin.interface.PSObject.Properties[$field]) "Plugin interface is missing '$field'."
    }

    Assert-True ($providers.profile -eq 'native-winui') 'Provider lock profile must be native-winui.'
    Assert-True ($tools.profile -eq 'native-winui') 'Tooling lock profile must be native-winui.'
    Assert-SameSet @($providers.providers | Where-Object required -eq $true | ForEach-Object id) @('microsoft-winui', 'superpowers', 'uncodixfy-winui-v2') 'Required providers'
    Assert-SameSet @($providers.providers | Where-Object required -eq $false | ForEach-Object id) @('figma', 'product-design', 'stark', 'ui-ux-pro-max') 'Optional providers'

    $unpinned = @($providers.providers | Where-Object { $_.repository -and ([string]::IsNullOrWhiteSpace($_.ref) -or $_.ref -notmatch '^[0-9a-f]{40}$') })
    $unpinnedIds = @($unpinned | ForEach-Object { [string] $_.id })
    Assert-True ($unpinned.Count -eq 0) "Unpinned provider refs: $($unpinnedIds -join ', ')"
    $writers = @($providers.providers | Where-Object writeAuthority -eq $true)
    $writerIds = @($writers | ForEach-Object { [string] $_.id })
    Assert-True ($writers.Count -eq 0) "External providers have write authority: $($writerIds -join ', ')"

    Assert-True ($template.implementationAuthorized -eq $false) 'Authorization template must be closed.'
    Assert-True (@($template.authorizedSurfaces).Count -eq 0) 'Authorization template must not contain surfaces.'
    $state = Get-AuthorizationState $root
    Assert-True (-not $state.Open) "Bootstrap verification requires closed local authorization state: $($state.Path)"

    $requiredPhrase = 'AUTHORIZE GRANITE FRONTEND V2 IMPLEMENTATION'
    $oldPhrase = 'START GRANITE FRONTEND ' + 'IMPLEMENTATION V2'
    $phraseFiles = @(
        'AGENTS.md',
        '.frontend-worker/v2/config.yml',
        '.frontend-worker/v2/implementation-lock.yml',
        '.frontend-worker/v2/authorization.template.json',
        '.frontend-worker/v2/IMPLEMENTATION_START_COMMAND.md',
        'docs/frontend-worker/GRANITE-NATIVE-FRONTEND-WORKER-V2-MASTER-PROMPT.md',
        'plugins/granite-native-frontend-worker/README.md',
        'plugins/granite-native-frontend-worker/skills/granite-native-frontend-master/SKILL.md',
        'plugins/granite-native-frontend-worker/skills/winui-xaml-implementer/SKILL.md',
        '.codex/agents/xaml_surface_implementer.toml'
    )
    foreach ($path in $phraseFiles) {
        $text = Get-Content -LiteralPath $path -Raw
        Assert-True ($text.Contains($requiredPhrase, [System.StringComparison]::Ordinal)) "Required authorization phrase is missing from $path"
        Assert-True (-not $text.Contains($oldPhrase, [System.StringComparison]::Ordinal)) "Obsolete authorization phrase remains in $path"
    }

    foreach ($skill in Get-ChildItem 'plugins/granite-native-frontend-worker/skills' -Filter 'SKILL.md' -Recurse) {
        $text = Get-Content -LiteralPath $skill.FullName -Raw
        Assert-True ($text.StartsWith("---`n") -or $text.StartsWith("---`r`n")) "Skill frontmatter is missing: $($skill.FullName)"
        Assert-True ($text -match '(?m)^name:\s*[-a-z0-9]+\s*$') "Skill name is invalid: $($skill.FullName)"
        Assert-True ($text -match '(?m)^description:\s*Use when\s+.+$') "Skill description must be trigger-only: $($skill.FullName)"
    }

    foreach ($script in Get-ChildItem 'scripts' -Filter '*.ps1' -Recurse | Where-Object { $_.FullName -match 'GraniteNativeFrontendWorkerV2|GraniteFrontendGuard' }) {
        $tokens = $null
        $parseErrors = $null
        [void] [System.Management.Automation.Language.Parser]::ParseFile($script.FullName, [ref] $tokens, [ref] $parseErrors)
        Assert-True ($parseErrors.Count -eq 0) "PowerShell parse errors in $($script.FullName): $($parseErrors.Message -join '; ')"
    }

    if ([string]::IsNullOrWhiteSpace($BaseRef)) {
        $config = Get-Content '.frontend-worker/v2/config.yml' -Raw
        $match = [regex]::Match($config, '(?m)^target_base_branch:\s*(.+?)\s*$')
        Assert-True $match.Success 'Unable to read target_base_branch.'
        $BaseRef = $match.Groups[1].Value.Trim().Trim('"').Trim("'")
    }
    $baseCommit = Resolve-BaseCommit $root $BaseRef
    $changedPaths = Get-ChangedPaths $root $baseCommit

    $allowedPatterns = @(
        '^AGENTS\.md$', '^\.agents/plugins/', '^\.agents/skills/', '^\.codex/agents/', '^\.frontend-worker/',
        '^plugins/granite-native-frontend-worker/', '^scripts/(Authorize|Initialize|Test)-GraniteNativeFrontendWorkerV2\.ps1$',
        '^scripts/frontend-worker/', '^tools/GraniteFrontendGuard/', '^docs/frontend-worker/',
        '^docs/superpowers/specs/2026-08-31-granite-native-frontend-worker-v2-design\.md$',
        '^docs/superpowers/plans/2026-08-31-granite-native-frontend-worker-v2-bootstrap\.md$',
        '^\.github/workflows/frontend-worker-bootstrap\.yml$'
    )
    $unexpected = @($changedPaths | Where-Object { $candidate = $_; -not ($allowedPatterns | Where-Object { $candidate -match $_ }) })
    Assert-True ($unexpected.Count -eq 0) "Files outside the bootstrap allowlist changed:`n - $($unexpected -join "`n - ")"

    $forbidden = @($changedPaths | Where-Object {
        $candidate = $_
        ($candidate -match '^IBM Granite with TurboQuant \(Intel\)/') -or
        ($candidate -match '^(shared|infrastructure|runtime|workers|experiments|models|tests|research|release-evidence)/') -or
        ($candidate -match '(^|/)(Package\.appxmanifest|app\.manifest|App\.xaml\.cs)$') -or
        (($candidate -match '\.(csproj|sln|slnx|targets)$') -and ($candidate -notmatch '^tools/GraniteFrontendGuard/GraniteFrontendGuard\.csproj$'))
    })
    Assert-True ($forbidden.Count -eq 0) "Production, test, project, evidence, or backend files changed:`n - $($forbidden -join "`n - ")"

    & git diff --check "$baseCommit...HEAD"
    Assert-True ($LASTEXITCODE -eq 0) 'git diff --check failed.'

    $project = 'tools/GraniteFrontendGuard/GraniteFrontendGuard.csproj'
    & dotnet build $project --configuration Release --nologo
    Assert-True ($LASTEXITCODE -eq 0) 'GraniteFrontendGuard build failed.'

    & pwsh -NoProfile -File 'scripts/frontend-worker/Test-GraniteFrontendGuard.ps1'
    Assert-True ($LASTEXITCODE -eq 0) 'GraniteFrontendGuard regression tests failed.'

    & dotnet run --no-build --configuration Release --project $project -- verify-bootstrap --repo $root --base $baseCommit
    Assert-True ($LASTEXITCODE -eq 0) 'GraniteFrontendGuard bootstrap verification failed.'

    if (-not $StructureOnly) {
        $statusPath = '.frontend-worker/v2/provider-status.json'
        Assert-True (Test-Path -LiteralPath $statusPath -PathType Leaf) 'Provider status is missing. Run the initializer with -Install.'
        $status = Get-Content -LiteralPath $statusPath -Raw | ConvertFrom-Json
        Assert-True ($status.schemaVersion -eq 3) 'Provider status schema is stale.'
        Assert-True ($status.mode -eq 'install') 'Provider status must come from an -Install run.'
        Assert-True ($status.implementationState -eq 'closed') 'Provider status does not confirm closed authorization.'
        foreach ($id in @('granite-native-frontend-worker', 'microsoft-winui', 'superpowers', 'uncodixfy-winui-v2')) {
            $record = @($status.providers | Where-Object id -eq $id)
            Assert-True ($record.Count -eq 1 -and $record[0].state -eq 'ready') "Required provider is not ready: $id"
        }
        foreach ($toolId in @('powershell-7', 'git', 'codex-cli', 'dotnet-sdk')) {
            $record = @($status.tools | Where-Object id -eq $toolId)
            Assert-True ($record.Count -eq 1 -and $record[0].state -eq 'ready') "Required tool is not ready: $toolId"
        }
    }

    Write-Host 'Granite Native Frontend Worker v2 bootstrap: PASS'
    Write-Host 'Single authorization source and closed state: PASS'
    Write-Host 'Provider roles, pins, and write authority: PASS'
    Write-Host 'Skill discovery metadata and PowerShell syntax: PASS'
    Write-Host 'Bootstrap isolation and no production/backend changes: PASS'
    Write-Host 'GraniteFrontendGuard build and regression tests: PASS'
    if (-not $StructureOnly) { Write-Host 'Required machine providers and tools: PASS' }
    exit 0
}
finally {
    Pop-Location
}
