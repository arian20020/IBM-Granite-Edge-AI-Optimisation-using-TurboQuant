[CmdletBinding(SupportsShouldProcess = $true)]
param(
    # Repository in owner/name format.
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[^/]+/[^/]+$')]
    [string]$Repository,

    # Repository root containing the generated task catalogue.
    [Parameter(Mandatory = $false)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })]
    [string]$RepositoryPath = (Get-Location).Path,

    # Actually create issues. Without this switch the script only previews them.
    [Parameter(Mandatory = $false)]
    [switch]$Apply,

    # Optional label applied to each created issue.
    [Parameter(Mandatory = $false)]
    [string]$Label = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Resolve the catalogue before checking GitHub authentication.
$ResolvedRepositoryPath = (Resolve-Path -LiteralPath $RepositoryPath).Path
$CataloguePath = Join-Path $ResolvedRepositoryPath 'docs\traceability\data\task-catalogue.json'

if (-not (Test-Path -LiteralPath $CataloguePath -PathType Leaf)) {
    throw "Task catalogue not found: $CataloguePath"
}

# GitHub CLI is required only for issue discovery and creation.
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw 'GitHub CLI was not found. Install gh and authenticate with gh auth login.'
}

# Confirm the user is authenticated before any write operation is attempted.
& gh auth status | Out-Host

if ($LASTEXITCODE -ne 0) {
    throw 'GitHub CLI authentication failed. Run gh auth login before continuing.'
}

# Read the generated catalogue and select only executable work-package tasks.
$Catalogue = Get-Content -LiteralPath $CataloguePath -Raw | ConvertFrom-Json
$WorkPackages = @(
    $Catalogue.tasks |
    Where-Object { $_.task_type -eq 'work-package' } |
    Sort-Object task_number
)

# Read existing issue titles so repeated runs do not create duplicates.
$ExistingIssueJson = & gh issue list `
    --repo $Repository `
    --state all `
    --limit 1000 `
    --json title,url

if ($LASTEXITCODE -ne 0) {
    throw 'Could not read existing GitHub issues.'
}

$ExistingIssues = @($ExistingIssueJson | ConvertFrom-Json)
$ExistingTitles = @{}

foreach ($Issue in $ExistingIssues) {
    $ExistingTitles[$Issue.title] = $Issue.url
}

$CreatedCount = 0
$SkippedCount = 0
$PreviewCount = 0

foreach ($Task in $WorkPackages) {
    # Use the stable WP ID at the beginning of every issue title.
    $Title = "[$($Task.source_id)] $($Task.title)"

    if ($ExistingTitles.ContainsKey($Title)) {
        Write-Host "SKIP existing: $Title" -ForegroundColor DarkYellow
        Write-Host "  $($ExistingTitles[$Title])"
        $SkippedCount++
        continue
    }

    # Build readable traceability lists for the issue body.
    $Requirements = if ($Task.relationships.requirements.Count -gt 0) {
        ($Task.relationships.requirements | ForEach-Object { "- REQ:$_" }) -join "`n"
    }
    else {
        '- No active requirement is directly allocated; see the task catalogue for the engineering rationale.'
    }

    $Practices = if ($Task.relationships.engineering_practices.Count -gt 0) {
        ($Task.relationships.engineering_practices | ForEach-Object { "- EP:$_" }) -join "`n"
    }
    else {
        '- No engineering-practice record is directly allocated.'
    }

    # Create one consistent operational issue body from the controlled catalogue.
    $Body = @"
## Work package

WP:$($Task.source_id)

## Task

$($Task.title)

## Definition of Done

- [ ] $($Task.definition_of_done)

## Requirements

$Requirements

## Engineering practices

$Practices

## Evidence location

``````text
$($Task.evidence_path)
``````

## Planning metadata

- Phase: $($Task.release_role_or_phase)
- Priority: $($Task.priority)
- RTM deadline: $($Task.deadline)
- RTM effective status at issue creation: $($Task.effective_status)

## Completion boundary

Closing this issue means the operational work package is complete. The task becomes Verified only after its evidence is checked and Validation is set to `Validated` in the controlled RTM.

## Pull request

Link the implementing pull request here and include `WP:$($Task.source_id)` plus the related requirement and engineering-practice IDs in the PR description.
"@

    if (-not $Apply) {
        Write-Host "PREVIEW: $Title" -ForegroundColor Cyan
        Write-Host "  Evidence: $($Task.evidence_path)"
        $PreviewCount++
        continue
    }

    # Create the issue only when -Apply was supplied and ShouldProcess approves it.
    if ($PSCmdlet.ShouldProcess($Repository, "Create issue $Title")) {
        $Arguments = @(
            'issue', 'create',
            '--repo', $Repository,
            '--title', $Title,
            '--body', $Body
        )

        if ($Label) {
            $Arguments += @('--label', $Label)
        }

        & gh @Arguments | Out-Host

        if ($LASTEXITCODE -ne 0) {
            throw "Failed to create issue: $Title"
        }

        $CreatedCount++
    }
}

Write-Host ''
Write-Host 'Work-package issue summary'
Write-Host "Catalogue work packages: $($WorkPackages.Count)"
Write-Host "Previewed: $PreviewCount"
Write-Host "Created: $CreatedCount"
Write-Host "Skipped existing: $SkippedCount"

if (-not $Apply) {
    Write-Host ''
    Write-Host 'No GitHub issues were created. Review the preview, then rerun with -Apply.' -ForegroundColor Green
}
