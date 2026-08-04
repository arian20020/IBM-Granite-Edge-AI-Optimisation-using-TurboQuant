# LLamaSharp Trusted Real-Model Execution Runbook

**Runbook ID:** RUNBOOK-LLAMASHARP-TRUSTED-001  
**Workflow:** `.github/workflows/llamasharp-real-model-integration.yml`  
**Trigger:** `workflow_dispatch` only  
**Branch:** `feature/model-inspection`  
**Prerequisite evidence:** [Tier 1 verified](../evidence/2026-08-04-llamasharp-tier1-verification.md)

## Purpose

This runbook stages the exact controlled Granite model in a location readable by
the self-hosted GitHub Actions service account, configures the repository
variable, dispatches the trusted workflow, waits for completion and downloads
the retained privacy-scanned evidence.

The model remains outside Git and outside the checked-out repository.

## Controlled model identity

```text
Filename:  granite-4.1-3b-Q4_K_M.gguf
Length:    2,099,501,664 bytes
SHA-256:   662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29
```

## Before running

- Open **Visual Studio Developer PowerShell**.
- Ensure GitHub CLI (`gh`) is installed and authenticated.
- Ensure the self-hosted GitHub Actions runner service is installed and running.
- Ensure the controlled source model exists in the current user's Downloads
  directory.
- Keep the pull request in draft state.

The script uses a shared staging location under:

```text
C:\Users\Public\Documents\GraniteEdgeAI\Models
```

This is intentionally outside the repository and is usually easier for a
restricted Windows service account to read than an interactive user's Downloads
folder.

## Complete PowerShell sequence

```powershell
# Stop at the first PowerShell error and reject accidental uninitialised values.
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# Define the repository, branch, workflow and exact controlled-model identity.
$Repository = `
    "arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant"

$Branch = `
    "feature/model-inspection"

$Workflow = `
    "llamasharp-real-model-integration.yml"

$KnownModelFileName = `
    "granite-4.1-3b-Q4_K_M.gguf"

$KnownModelLength = `
    [int64]2099501664

$KnownModelHash = `
    "662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29"

# Move to the local repository and confirm the expected feature branch.
Set-Location `
    "C:\Users\Arian\source\repos\IBM-Granite-TurboQuant-Intel"

git switch $Branch

if ($LASTEXITCODE -ne 0) {
    throw "Could not switch to $Branch. Git exit code: $LASTEXITCODE"
}

# Protect local work before pulling the verified branch state.
$LocalChanges = git status --porcelain

if ($LocalChanges) {
    throw @"
The working tree contains local changes.

Commit or restore them before running the trusted workflow:

$LocalChanges
"@
}

git pull `
    --ff-only `
    origin `
    $Branch

if ($LASTEXITCODE -ne 0) {
    throw "Git pull failed with exit code $LASTEXITCODE."
}

Write-Host "Current branch head:" -ForegroundColor Cyan
git log -1 --oneline

# Confirm GitHub CLI exists and is authenticated for this repository.
$GitHubCli = Get-Command `
    gh `
    -ErrorAction SilentlyContinue

if ($null -eq $GitHubCli) {
    throw "GitHub CLI (gh) is not installed or is not on PATH."
}

gh auth status

if ($LASTEXITCODE -ne 0) {
    throw "GitHub CLI is not authenticated."
}

# Discover the Windows account used by the running self-hosted runner service.
$RunnerServices = @(
    Get-CimInstance `
        Win32_Service |
    Where-Object {
        $_.Name -like "actions.runner.*"
    }
)

if ($RunnerServices.Count -eq 0) {
    throw "No actions.runner.* Windows service was found."
}

$RunnerService = `
    $RunnerServices |
    Where-Object {
        $_.State -eq "Running"
    } |
    Select-Object `
        -First 1

if ($null -eq $RunnerService) {
    throw @"
A GitHub Actions runner service exists, but none is running.

Services found:
$($RunnerServices | Format-Table Name, StartName, State -AutoSize | Out-String)
"@
}

$RunnerAccount = [string]$RunnerService.StartName

# Convert service-account aliases into ACL-friendly Windows account names.
if ($RunnerAccount.StartsWith(".\", [StringComparison]::Ordinal)) {
    $RunnerAccount = `
        "$env:COMPUTERNAME\$($RunnerAccount.Substring(2))"
}
elseif ($RunnerAccount -eq "LocalSystem") {
    $RunnerAccount = `
        "NT AUTHORITY\SYSTEM"
}

Write-Host "Runner service:" -ForegroundColor Cyan

[PSCustomObject]@{
    Name = $RunnerService.Name
    State = $RunnerService.State
    Account = $RunnerAccount
} |
Format-List

# Locate and independently validate the source model before copying anything.
$SourceModelPath = Join-Path `
    $env:USERPROFILE `
    "Downloads\$KnownModelFileName"

if (-not (Test-Path -LiteralPath $SourceModelPath -PathType Leaf)) {
    throw "Controlled source model not found: $SourceModelPath"
}

$SourceModel = Get-Item `
    -LiteralPath $SourceModelPath

if ($SourceModel.Name -cne $KnownModelFileName) {
    throw "Unexpected source filename: $($SourceModel.Name)"
}

if ($SourceModel.Length -ne $KnownModelLength) {
    throw @"
Controlled source-model length mismatch.

Expected: $KnownModelLength
Actual:   $($SourceModel.Length)
"@
}

$SourceHash = (
    Get-FileHash `
        -LiteralPath $SourceModelPath `
        -Algorithm SHA256
).Hash.ToLowerInvariant()

if ($SourceHash -ne $KnownModelHash) {
    throw @"
Controlled source-model SHA-256 mismatch.

Expected: $KnownModelHash
Actual:   $SourceHash
"@
}

# Create a shared model directory outside the repository.
$StagedModelDirectory = Join-Path `
    $env:PUBLIC `
    "Documents\GraniteEdgeAI\Models"

$StagedModelPath = Join-Path `
    $StagedModelDirectory `
    $KnownModelFileName

New-Item `
    -ItemType Directory `
    -Path $StagedModelDirectory `
    -Force |
Out-Null

# Reuse an exact staged copy; otherwise replace it from the validated source.
$CopyRequired = $true

if (Test-Path -LiteralPath $StagedModelPath -PathType Leaf) {
    $ExistingStagedModel = Get-Item `
        -LiteralPath $StagedModelPath

    if ($ExistingStagedModel.Length -eq $KnownModelLength) {
        $ExistingHash = (
            Get-FileHash `
                -LiteralPath $StagedModelPath `
                -Algorithm SHA256
        ).Hash.ToLowerInvariant()

        if ($ExistingHash -eq $KnownModelHash) {
            $CopyRequired = $false
        }
    }

    if ($CopyRequired) {
        # Clear read-only before replacing a stale or incorrect staged file.
        $ExistingStagedModel.IsReadOnly = $false

        Remove-Item `
            -LiteralPath $StagedModelPath `
            -Force
    }
}

if ($CopyRequired) {
    Copy-Item `
        -LiteralPath $SourceModelPath `
        -Destination $StagedModelPath `
        -Force
}

# Mark the staged model read-only at the ordinary file-attribute level.
$StagedModel = Get-Item `
    -LiteralPath $StagedModelPath

$StagedModel.IsReadOnly = $true

# Grant only read/traverse access to the runner service account.
& icacls.exe `
    $StagedModelDirectory `
    /grant `
    "${RunnerAccount}:(OI)(CI)(RX)"

if ($LASTEXITCODE -ne 0) {
    throw "Failed to grant runner access to the staged model directory."
}

& icacls.exe `
    $StagedModelPath `
    /grant `
    "${RunnerAccount}:(R)"

if ($LASTEXITCODE -ne 0) {
    throw "Failed to grant runner read access to the staged model file."
}

# Re-verify the exact staged identity after copying and ACL changes.
$StagedModel = Get-Item `
    -LiteralPath $StagedModelPath

$StagedHash = (
    Get-FileHash `
        -LiteralPath $StagedModelPath `
        -Algorithm SHA256
).Hash.ToLowerInvariant()

if ($StagedModel.Length -ne $KnownModelLength) {
    throw "Staged model length changed unexpectedly."
}

if ($StagedHash -ne $KnownModelHash) {
    throw "Staged model SHA-256 changed unexpectedly."
}

if (-not $StagedModel.IsReadOnly) {
    throw "The staged model is not marked read-only."
}

Write-Host "Controlled model staged and verified:" -ForegroundColor Green

[PSCustomObject]@{
    Path = $StagedModelPath
    LengthBytes = $StagedModel.Length
    Sha256 = $StagedHash
    ReadOnly = $StagedModel.IsReadOnly
    RunnerAccount = $RunnerAccount
} |
Format-List

# Store the runner-visible path as a repository Actions variable.
gh variable set `
    GRANITE_TEST_MODEL_PATH `
    --body $StagedModelPath `
    --repo $Repository

if ($LASTEXITCODE -ne 0) {
    throw "Could not set GRANITE_TEST_MODEL_PATH."
}

# Capture existing run IDs so the newly dispatched run is identified exactly.
$KnownRunIds = @(
    gh run list `
        --repo $Repository `
        --workflow $Workflow `
        --branch $Branch `
        --event workflow_dispatch `
        --limit 20 `
        --json databaseId |
    ConvertFrom-Json |
    ForEach-Object {
        [int64]$_.databaseId
    }
)

# Dispatch the manual trusted workflow against the feature branch.
gh workflow run `
    $Workflow `
    --ref $Branch `
    --repo $Repository

if ($LASTEXITCODE -ne 0) {
    throw "Trusted workflow dispatch failed."
}

# Wait briefly for GitHub to register the new workflow run.
$RunDiscoveryDeadline = `
    (Get-Date).AddMinutes(2)

$NewRun = $null

do {
    Start-Sleep `
        -Seconds 3

    $CandidateRuns = @(
        gh run list `
            --repo $Repository `
            --workflow $Workflow `
            --branch $Branch `
            --event workflow_dispatch `
            --limit 20 `
            --json databaseId,status,conclusion,createdAt,url,headSha |
        ConvertFrom-Json
    )

    $NewRun = `
        $CandidateRuns |
        Where-Object {
            $KnownRunIds -notcontains [int64]$_.databaseId
        } |
        Sort-Object {
            [DateTimeOffset]$_.createdAt
        } `
            -Descending |
        Select-Object `
            -First 1
}
until (
    $null -ne $NewRun -or
    (Get-Date) -ge $RunDiscoveryDeadline
)

if ($null -eq $NewRun) {
    throw "The trusted workflow was dispatched, but its new run ID was not found."
}

$RunId = [int64]$NewRun.databaseId

Write-Host "Trusted workflow run:" -ForegroundColor Cyan
$NewRun | Format-List

# Stream the run and return a non-zero exit code when any required gate fails.
gh run watch `
    $RunId `
    --repo $Repository `
    --exit-status

$TrustedRunExitCode = $LASTEXITCODE

if ($TrustedRunExitCode -ne 0) {
    Write-Host `
        "Trusted workflow failed. Showing failed-step logs." `
        -ForegroundColor Red

    gh run view `
        $RunId `
        --repo $Repository `
        --log-failed

    throw "Trusted workflow failed with exit code $TrustedRunExitCode."
}

# Display the final run summary.
gh run view `
    $RunId `
    --repo $Repository

# Download the privacy-scanned evidence artifact into the local repository's
# ignored artifact area for review. The controlled GGUF is never downloaded.
$RepositoryRoot = (
    git rev-parse --show-toplevel
).Trim()

$DownloadedEvidenceDirectory = Join-Path `
    $RepositoryRoot `
    "artifacts\downloaded\llamasharp-real-model-run-$RunId"

New-Item `
    -ItemType Directory `
    -Path $DownloadedEvidenceDirectory `
    -Force |
Out-Null

gh run download `
    $RunId `
    --repo $Repository `
    --dir $DownloadedEvidenceDirectory

if ($LASTEXITCODE -ne 0) {
    throw "Trusted evidence download failed."
}

Write-Host `
    "Trusted evidence downloaded to: $DownloadedEvidenceDirectory" `
    -ForegroundColor Green
```

## Expected workflow order

```text
Validate exact model identity
        ↓
Restore projects
        ↓
Publish exact CPU feasibility executable
        ↓
Run RealModelIntegration tests
        ↓
Re-hash controlled model
        ↓
Scan retained evidence for model leakage
        ↓
Upload evidence only when integrity and privacy scans pass
```

## Failure handling

Do not rerun immediately without reading the first failing step.

- A precondition failure means the runner path, filename, length, hash or ACL is
  wrong; the native runtime was not tested.
- A compile failure is a source/analyzer defect; the model was not tested.
- A test failure must be diagnosed from the exact child-process result and
  retained JSON/log evidence.
- A model-integrity failure is load-bearing and blocks artifact upload.
- An artifact-privacy failure is load-bearing and blocks artifact upload.
- A queued run that never starts usually means the required self-hosted labels
  or runner service are unavailable.

Keep the pull request in draft until the trusted evidence is reviewed and the
coverage register is reconciled with actual results.
