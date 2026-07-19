[CmdletBinding()]
param(
    [string]$CampaignDate = "2026-07-19",
    [string]$ExpectedCommit = "7dea0459b2ac7d8dfd877fd9df6737674fd8371d",
    [string]$UpstreamPath,
    [string]$DestinationPath,
    [string]$EvidencePath
)

$ErrorActionPreference = "Stop"
$branchName = "project/turboquant-wb04"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path

function Invoke-Git {
    param([string[]]$Arguments)
    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        $output = @(& git -c core.longpaths=true @Arguments 2>&1 | ForEach-Object { $_.ToString() })
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousPreference
    }
    if ($exitCode -ne 0) {
        throw "git $($Arguments -join ' ') failed ($exitCode): $($output -join [Environment]::NewLine)"
    }
    return $output
}

function Get-GitValue {
    param([string[]]$Arguments)
    return ((Invoke-Git $Arguments) -join "`n").Trim()
}

if (-not $UpstreamPath) {
    $commonGitDir = Get-GitValue @("-C", $repoRoot, "rev-parse", "--path-format=absolute", "--git-common-dir")
    $sharedRepoRoot = Split-Path -Parent $commonGitDir
    $UpstreamPath = Join-Path $sharedRepoRoot "external/official-openvino/$CampaignDate/openvino.genai"
}
if (-not $DestinationPath) {
    $DestinationPath = Join-Path $repoRoot "external/official-openvino/$CampaignDate/openvino.genai-turboquant"
}
if (-not $EvidencePath) {
    $EvidencePath = Join-Path (Split-Path -Parent $DestinationPath) "openvino.genai-turboquant.identity.json"
}

$upstream = [System.IO.Path]::GetFullPath($UpstreamPath)
$destination = [System.IO.Path]::GetFullPath($DestinationPath)
$evidence = [System.IO.Path]::GetFullPath($EvidencePath)
$patchRoot = Join-Path $repoRoot "experiments/patches/openvino-turboquant"
$patches = @()

if (-not (Test-Path -LiteralPath (Join-Path $upstream ".git"))) {
    throw "Pinned upstream checkout not found: $upstream"
}
$upstreamCommit = Get-GitValue @("-C", $upstream, "rev-parse", "HEAD")
if ($upstreamCommit -ne $ExpectedCommit) {
    throw "Pinned upstream base commit mismatch: expected $ExpectedCommit, found $upstreamCommit"
}
if ((Invoke-Git @("-C", $upstream, "status", "--porcelain")).Count -ne 0) {
    throw "Pinned upstream checkout is dirty: $upstream"
}

$destinationGit = Join-Path $destination ".git"
if (Test-Path -LiteralPath $destination) {
    if (-not (Test-Path -LiteralPath $destinationGit)) {
        throw "Existing destination is not a Git checkout: $destination"
    }
    Invoke-Git @("-C", $destination, "config", "core.longpaths", "true") | Out-Null
    if ((Invoke-Git @("-C", $destination, "status", "--porcelain")).Count -ne 0) {
        throw "Existing patch destination is dirty: $destination"
    }
    $currentBranch = Get-GitValue @("-C", $destination, "branch", "--show-current")
    if ($currentBranch -ne $branchName) {
        throw "Existing patch destination is on '$currentBranch', expected '$branchName'"
    }
    Invoke-Git @("-C", $destination, "merge-base", "--is-ancestor", $ExpectedCommit, "HEAD") | Out-Null
}
else {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
    Invoke-Git @("clone", "--local", "--no-hardlinks", "--no-checkout", $upstream, $destination) | Out-Null
    Invoke-Git @("-C", $destination, "config", "core.longpaths", "true") | Out-Null
    Invoke-Git @("-C", $destination, "checkout", "--detach", $ExpectedCommit) | Out-Null
    Invoke-Git @("-C", $destination, "checkout", "-b", $branchName) | Out-Null
    Invoke-Git @("-C", $destination, "submodule", "update", "--init", "--recursive") | Out-Null

    $patches = @(Get-ChildItem -LiteralPath $patchRoot -Filter "*.patch" -File | Sort-Object Name)
    foreach ($patch in $patches) {
        Invoke-Git @("-C", $destination, "apply", "--check", $patch.FullName) | Out-Null
        Invoke-Git @("-C", $destination, "apply", "--index", $patch.FullName) | Out-Null
    }
    if ((Invoke-Git @("-C", $destination, "diff", "--cached", "--name-only")).Count -ne 0) {
        Invoke-Git @("-C", $destination, "-c", "user.name=IBM Granite Project", "-c", "user.email=project@example.invalid", "commit", "-m", "Apply project TurboQuant patch set") | Out-Null
    }
}

$patchCommit = Get-GitValue @("-C", $destination, "rev-parse", "HEAD")
$dirty = (Invoke-Git @("-C", $destination, "status", "--porcelain")).Count -ne 0
if ($dirty) {
    throw "Patch workspace is dirty after preparation: $destination"
}

$record = [ordered]@{
    upstream_path = $upstream
    destination_path = $destination
    patch_directory = $patchRoot
    branch = $branchName
    base_commit = $ExpectedCommit
    upstream_commit = $upstreamCommit
    patch_commit = $patchCommit
    applied_patches = @($patches | ForEach-Object { $_.Name })
    dirty = $dirty
}
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $evidence) | Out-Null
[System.IO.File]::WriteAllText(
    $evidence,
    ($record | ConvertTo-Json -Depth 5) + [Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false)
)

Write-Host "Prepared patch workspace: $destination"
Write-Host "Identity evidence: $evidence"
