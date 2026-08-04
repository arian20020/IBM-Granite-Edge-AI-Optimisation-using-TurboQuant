# LLamaSharp Trusted Real-Model Execution Runbook

**Runbook ID:** RUNBOOK-LLAMASHARP-TRUSTED-001  
**Workflow:** `.github/workflows/llamasharp-real-model-integration.yml`  
**Workflow trigger:** `workflow_dispatch` only  
**Feature branch:** `feature/model-inspection`  
**Prerequisite evidence:** [Tier 1 verified](../evidence/2026-08-04-llamasharp-tier1-verification.md)

## Purpose

This runbook defines two distinct verification gates:

1. **Pre-merge target-machine execution** — run the complete trusted test
   project locally against the controlled Granite model now.
2. **Post-default-branch trusted workflow** — after the workflow file exists on
   the repository default branch, run the same suite through the restricted
   self-hosted GitHub Actions service account and retain privacy-scanned
   evidence.

The distinction is required by GitHub Actions. A `workflow_dispatch` event runs
only when that workflow file exists on the repository default branch. The
feature branch currently contains the new workflow, while `main` does not.
Therefore, `gh workflow run` cannot be the first pre-merge execution route
without weakening the approved trigger policy or first merging the workflow.

Official GitHub references:

- <https://docs.github.com/actions/reference/workflows-and-actions/events-that-trigger-workflows#workflow_dispatch>
- <https://cli.github.com/manual/gh_workflow_run>

The model remains outside Git and outside every checked-out repository.

## Controlled model identity

```text
Filename:  granite-4.1-3b-Q4_K_M.gguf
Length:    2,099,501,664 bytes
SHA-256:   662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29
```

---

# Phase A — pre-merge local target-machine gate

## What this phase proves

This phase executes the complete `RealModelIntegration` category on the target
Windows machine using the same published feasibility executable and
child-process test harness as the future trusted workflow.

It covers:

- exact Granite success evidence;
- three sequential repeatability runs;
- whole-operation cancellation;
- native-load-scoped cancellation;
- every committed malformed GGUF fixture;
- deterministic random bytes with a `.gguf` extension;
- missing, directory, locked-model, locked-output, invalid-parent and
  model/output-collision scenarios;
- canonical-path and chat-template privacy;
- process-owned TCP endpoint observation;
- model integrity before and after the complete suite;
- evidence-tree scanning for accidental model copies.

It does **not** prove the GitHub Actions service account can read the model or
that the post-merge workflow dispatch and artifact upload work. Those are Phase
B concerns.

## Complete Developer PowerShell command

Run this from **Visual Studio Developer PowerShell**:

```powershell
# Stop at the first PowerShell error and reject uninitialised variables.
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# Define the exact repository, branch and controlled-model identity.
$RepositoryRoot = `
    "C:\Users\Arian\source\repos\IBM-Granite-TurboQuant-Intel"

$Branch = `
    "feature/model-inspection"

$KnownModelFileName = `
    "granite-4.1-3b-Q4_K_M.gguf"

$KnownModelLength = `
    [int64]2099501664

$KnownModelHash = `
    "662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29"

# Enter the repository and protect any uncommitted work.
Set-Location $RepositoryRoot

git switch $Branch

if ($LASTEXITCODE -ne 0) {
    throw "Could not switch to $Branch. Git exit code: $LASTEXITCODE"
}

$LocalChanges = git status --porcelain

if ($LocalChanges) {
    throw @"
The working tree contains local changes.

Commit or restore them before running the trusted suite:

$LocalChanges
"@
}

# Pull only a fast-forward so the local branch cannot silently diverge.
git pull `
    --ff-only `
    origin `
    $Branch

if ($LASTEXITCODE -ne 0) {
    throw "Git pull failed with exit code $LASTEXITCODE."
}

Write-Host "Current branch head:" -ForegroundColor Cyan
git log -1 --oneline

# Resolve the exact source model used by the earlier successful baseline.
$ModelPath = Join-Path `
    $env:USERPROFILE `
    "Downloads\$KnownModelFileName"

if (-not (Test-Path -LiteralPath $ModelPath -PathType Leaf)) {
    throw "Controlled Granite model not found: $ModelPath"
}

$Model = Get-Item `
    -LiteralPath $ModelPath

if ($Model.Name -cne $KnownModelFileName) {
    throw "Unexpected controlled-model filename: $($Model.Name)"
}

if ($Model.Length -ne $KnownModelLength) {
    throw @"
Controlled-model length mismatch.

Expected: $KnownModelLength
Actual:   $($Model.Length)
"@
}

$ModelHashBefore = (
    Get-FileHash `
        -LiteralPath $ModelPath `
        -Algorithm SHA256
).Hash.ToLowerInvariant()

if ($ModelHashBefore -ne $KnownModelHash) {
    throw @"
Controlled-model SHA-256 mismatch.

Expected: $KnownModelHash
Actual:   $ModelHashBefore
"@
}

# Define the production feasibility project and trusted test project.
$SpikeProject = `
    "tools\ModelInspection.LlamaSharpSpike\ModelInspection.LlamaSharpSpike.csproj"

$TrustedTestProject = `
    "tools\ModelInspection.LlamaSharpSpike.RealModelIntegrationTests\ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.csproj"

# Use fresh, bounded directories for the published executable and retained
# evidence. Both remain outside source-controlled project files.
$RunId = Get-Date `
    -Format "yyyyMMdd-HHmmss"

$PublishDirectory = Join-Path `
    $env:TEMP `
    "GraniteEdgeAI-LlamaSharp-Publish-$RunId"

$EvidenceDirectory = Join-Path `
    $RepositoryRoot `
    "artifacts\model-inspection\llamasharp\real-model-local\$RunId"

foreach ($Directory in @($PublishDirectory, $EvidenceDirectory)) {
    if (Test-Path -LiteralPath $Directory) {
        Remove-Item `
            -LiteralPath $Directory `
            -Recurse `
            -Force
    }

    New-Item `
        -ItemType Directory `
        -Path $Directory `
        -Force |
    Out-Null
}

# Configure only the environment variables consumed by the trusted tests.
$env:LLAMASHARP_SPIKE_PUBLISH_DIR = `
    $PublishDirectory

$env:GRANITE_TEST_MODEL_PATH = `
    $ModelPath

$env:LLAMASHARP_REAL_MODEL_EVIDENCE_DIR = `
    $EvidenceDirectory

# Track the test process exit code without skipping the independent post-suite
# model-integrity and artifact-privacy checks.
$TrustedTestExitCode = $null

try {
    # Restore the exact runtime and trusted test dependency graphs.
    dotnet restore `
        $SpikeProject `
        --runtime win-x64

    if ($LASTEXITCODE -ne 0) {
        throw "Feasibility project restore failed: $LASTEXITCODE"
    }

    dotnet restore `
        $TrustedTestProject `
        --runtime win-x64

    if ($LASTEXITCODE -ne 0) {
        throw "Trusted test restore failed: $LASTEXITCODE"
    }

    # Publish the exact framework-dependent Windows x64 feasibility tool.
    dotnet publish `
        $SpikeProject `
        --configuration Release `
        --no-restore `
        --runtime win-x64 `
        --self-contained false `
        --output $PublishDirectory

    if ($LASTEXITCODE -ne 0) {
        throw "Feasibility tool publish failed: $LASTEXITCODE"
    }

    # Execute every trusted real-model integration test. The minimum count
    # prevents a false green result where filtering accidentally runs no tests.
    dotnet test `
        $TrustedTestProject `
        --configuration Release `
        --no-restore `
        --runtime win-x64 `
        --filter "TestCategory=RealModelIntegration" `
        --minimum-expected-tests 20

    $TrustedTestExitCode = $LASTEXITCODE
}
finally {
    # Re-hash the controlled model even when restore, publish or tests fail.
    $ModelHashAfter = (
        Get-FileHash `
            -LiteralPath $ModelPath `
            -Algorithm SHA256
    ).Hash.ToLowerInvariant()

    Write-Host "Model SHA-256 before: $ModelHashBefore" -ForegroundColor Cyan
    Write-Host "Model SHA-256 after:  $ModelHashAfter" -ForegroundColor Cyan

    if ($ModelHashAfter -ne $ModelHashBefore) {
        throw @"
The controlled Granite model changed during trusted testing.

Before: $ModelHashBefore
After:  $ModelHashAfter
"@
    }
}

# Independently scan the retained evidence tree before treating the run as safe.
$ArtifactFindings = @()
$MaximumEvidenceBytes = 100MB

foreach ($File in Get-ChildItem `
    -LiteralPath $EvidenceDirectory `
    -File `
    -Recurse `
    -ErrorAction Stop) {

    $RelativePath = [IO.Path]::GetRelativePath(
        $EvidenceDirectory,
        $File.FullName)

    if ($File.Extension -ieq ".gguf") {
        $ArtifactFindings += [PSCustomObject]@{
            RelativePath = $RelativePath
            Reason = "GGUF file found under retained evidence."
        }
    }

    if ($File.Length -eq $KnownModelLength) {
        $ArtifactFindings += [PSCustomObject]@{
            RelativePath = $RelativePath
            Reason = "File length matches the controlled model."
        }
    }

    if ($File.Length -gt $MaximumEvidenceBytes) {
        $ArtifactFindings += [PSCustomObject]@{
            RelativePath = $RelativePath
            Reason = "Evidence file exceeds $MaximumEvidenceBytes bytes."
        }
    }

    if ($File.Length -le $MaximumEvidenceBytes) {
        $FileHash = (
            Get-FileHash `
                -LiteralPath $File.FullName `
                -Algorithm SHA256
        ).Hash.ToLowerInvariant()

        if ($FileHash -eq $KnownModelHash) {
            $ArtifactFindings += [PSCustomObject]@{
                RelativePath = $RelativePath
                Reason = "File SHA-256 matches the controlled model."
            }
        }
    }
}

if ($ArtifactFindings.Count -gt 0) {
    throw @"
The retained-evidence privacy scan failed.

$($ArtifactFindings | Format-Table -AutoSize | Out-String)
"@
}

if ($TrustedTestExitCode -ne 0) {
    throw "Trusted real-model tests failed with exit code $TrustedTestExitCode."
}

Write-Host `
    "Trusted local real-model gate passed." `
    -ForegroundColor Green

Write-Host `
    "Retained evidence: $EvidenceDirectory" `
    -ForegroundColor Green

Get-ChildItem `
    -LiteralPath $EvidenceDirectory `
    -File `
    -Recurse |
Select-Object `
    FullName,
    Length,
    LastWriteTimeUtc |
Format-Table `
    -AutoSize
```

## Phase A failure handling

Do not rerun immediately without reading the first failing test and its child
process evidence.

- A restore or publish failure means no real-model test ran.
- A controlled-model precondition failure means the selected file identity is
  wrong.
- A cancellation failure should be examined separately from ordinary model-load
  failure.
- A native abort is expected to remain contained in the child-process result;
  the complete MSTest host must survive.
- A model-integrity mismatch is load-bearing and blocks all further work.
- An evidence-tree privacy finding is load-bearing and blocks retention or
  upload.

Keep the pull request in draft while any required trusted scenario is failing.

---

# Phase B — post-default-branch self-hosted workflow

## Why this phase waits

GitHub documents that `workflow_dispatch` only triggers when the workflow file
exists on the repository default branch. The approved trusted workflow is new
on `feature/model-inspection`, so it is not dispatchable through the Actions UI,
CLI or REST API until the workflow path also exists on `main`.

Do not work around this by adding an automatic pull-request trigger to the
self-hosted workflow. Pull-request code must not be allowed to execute
implicitly on the repository-owned target machine.

After the workflow exists on `main`, `--ref` may select the branch or tag whose
workflow version should run.

## Runner-visible model staging

The runner service account should read a shared, read-only model copy outside
the repository, for example:

```text
C:\Users\Public\Documents\GraniteEdgeAI\Models\granite-4.1-3b-Q4_K_M.gguf
```

Use the following block after the workflow file is present on `main`:

```powershell
# Stop on errors and define the exact model identity.
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$Repository = `
    "arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant"

$Branch = `
    "feature/model-inspection"

$Workflow = `
    "llamasharp-real-model-integration.yml"

$ModelFileName = `
    "granite-4.1-3b-Q4_K_M.gguf"

$ExpectedLength = `
    [int64]2099501664

$ExpectedHash = `
    "662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29"

$SourceModelPath = Join-Path `
    $env:USERPROFILE `
    "Downloads\$ModelFileName"

$StagedDirectory = Join-Path `
    $env:PUBLIC `
    "Documents\GraniteEdgeAI\Models"

$StagedModelPath = Join-Path `
    $StagedDirectory `
    $ModelFileName

# Verify the workflow is now registered from the default branch.
gh workflow view `
    $Workflow `
    --repo $Repository

if ($LASTEXITCODE -ne 0) {
    throw @"
The trusted workflow is not registered from the default branch.
Do not attempt workflow_dispatch until its file exists on main.
"@
}

# Discover the running self-hosted runner's Windows service account.
$RunnerServices = @(
    Get-CimInstance `
        Win32_Service |
    Where-Object {
        $_.Name -like "actions.runner.*"
    }
)

$RunnerService = `
    $RunnerServices |
    Where-Object {
        $_.State -eq "Running"
    } |
    Select-Object `
        -First 1

if ($null -eq $RunnerService) {
    throw "No running actions.runner.* Windows service was found."
}

$RunnerAccount = [string]$RunnerService.StartName

if ($RunnerAccount.StartsWith(".\", [StringComparison]::Ordinal)) {
    $RunnerAccount = `
        "$env:COMPUTERNAME\$($RunnerAccount.Substring(2))"
}
elseif ($RunnerAccount -eq "LocalSystem") {
    $RunnerAccount = `
        "NT AUTHORITY\SYSTEM"
}

# Validate and stage the exact model outside the repository.
$SourceModel = Get-Item `
    -LiteralPath $SourceModelPath

$SourceHash = (
    Get-FileHash `
        -LiteralPath $SourceModelPath `
        -Algorithm SHA256
).Hash.ToLowerInvariant()

if ($SourceModel.Length -ne $ExpectedLength -or
    $SourceHash -ne $ExpectedHash) {
    throw "The source model does not match the controlled identity."
}

New-Item `
    -ItemType Directory `
    -Path $StagedDirectory `
    -Force |
Out-Null

if (Test-Path -LiteralPath $StagedModelPath -PathType Leaf) {
    (Get-Item -LiteralPath $StagedModelPath).IsReadOnly = $false
}

Copy-Item `
    -LiteralPath $SourceModelPath `
    -Destination $StagedModelPath `
    -Force

(Get-Item -LiteralPath $StagedModelPath).IsReadOnly = $true

# Grant the runner account read/traverse access only.
& icacls.exe `
    $StagedDirectory `
    /grant `
    "${RunnerAccount}:(OI)(CI)(RX)"

if ($LASTEXITCODE -ne 0) {
    throw "Could not grant runner directory access."
}

& icacls.exe `
    $StagedModelPath `
    /grant `
    "${RunnerAccount}:(R)"

if ($LASTEXITCODE -ne 0) {
    throw "Could not grant runner model-file access."
}

$StagedModel = Get-Item `
    -LiteralPath $StagedModelPath

$StagedHash = (
    Get-FileHash `
        -LiteralPath $StagedModelPath `
        -Algorithm SHA256
).Hash.ToLowerInvariant()

if ($StagedModel.Length -ne $ExpectedLength -or
    $StagedHash -ne $ExpectedHash -or
    -not $StagedModel.IsReadOnly) {
    throw "The staged model failed identity or read-only verification."
}

# Configure the repository variable consumed by the workflow.
gh variable set `
    GRANITE_TEST_MODEL_PATH `
    --body $StagedModelPath `
    --repo $Repository

if ($LASTEXITCODE -ne 0) {
    throw "Could not set GRANITE_TEST_MODEL_PATH."
}

# Dispatch the manual workflow and select the branch to test.
gh workflow run `
    $Workflow `
    --ref $Branch `
    --repo $Repository

if ($LASTEXITCODE -ne 0) {
    throw "Trusted workflow dispatch failed."
}

# Find and watch the newest trusted run for the selected branch.
Start-Sleep -Seconds 5

$Run = `
    gh run list `
        --repo $Repository `
        --workflow $Workflow `
        --branch $Branch `
        --event workflow_dispatch `
        --limit 1 `
        --json databaseId,status,conclusion,createdAt,url,headSha |
    ConvertFrom-Json |
    Select-Object `
        -First 1

if ($null -eq $Run) {
    throw "The dispatched trusted run was not found."
}

$RunId = [int64]$Run.databaseId

$Run | Format-List

gh run watch `
    $RunId `
    --repo $Repository `
    --exit-status

if ($LASTEXITCODE -ne 0) {
    gh run view `
        $RunId `
        --repo $Repository `
        --log-failed

    throw "Trusted workflow failed."
}

# Download only the workflow's privacy-scanned retained evidence.
$DownloadDirectory = Join-Path `
    (git rev-parse --show-toplevel).Trim() `
    "artifacts\downloaded\llamasharp-real-model-run-$RunId"

New-Item `
    -ItemType Directory `
    -Path $DownloadDirectory `
    -Force |
Out-Null

gh run download `
    $RunId `
    --repo $Repository `
    --dir $DownloadDirectory

if ($LASTEXITCODE -ne 0) {
    throw "Trusted evidence download failed."
}

Write-Host `
    "Trusted evidence downloaded to: $DownloadDirectory" `
    -ForegroundColor Green
```

## Expected Phase B workflow order

```text
Validate exact model identity from the runner service account
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
Upload only when integrity and privacy scans both pass
```

## Closure

Phase A can be executed before merge and supplies the next immediate technical
evidence. Phase B supplies the final self-hosted workflow and service-account
evidence after the workflow exists on the default branch.

The pull request remains draft until the required real-model behaviours have
passed, the coverage matrix reflects the actual evidence, and final whole-branch
review is complete.
