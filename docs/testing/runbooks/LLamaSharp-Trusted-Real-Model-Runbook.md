# LLamaSharp Trusted Real-Model Execution Runbook

**Runbook ID:** RUNBOOK-LLAMASHARP-TRUSTED-001  
**Workflow:** `.github/workflows/llamasharp-real-model-integration.yml`  
**Workflow trigger:** `workflow_dispatch` only  
**Feature branch:** `feature/model-inspection`  
**Last reviewed:** 2026-08-05  
**Tier 1 evidence:** [Hosted verification](../evidence/2026-08-04-llamasharp-tier1-verification.md)  
**Tier 2 evidence:** [Local trusted verification](../evidence/2026-08-05-llamasharp-tier2-local-verification.md)

## Purpose

This runbook defines two separate verification gates:

1. **Local target-machine gate** — execute the complete trusted test project
   against the controlled Granite model before merge.
2. **Self-hosted service-account gate** — after the workflow file exists on the
   default branch, execute the same suite through the restricted GitHub Actions
   runner and verify workflow registration, service-account access and guarded
   artifact upload.

The local gate was completed successfully on 2026-08-05:

```text
Trusted tests:             20 / 20 passed
Failed / skipped:          0 / 0
Model SHA-256 unchanged:   yes
Evidence files scanned:    56
Privacy findings:          0
```

The model remains outside Git and outside every checked-out repository.

## Controlled model identity

```text
Filename:  granite-4.1-3b-Q4_K_M.gguf
Length:    2,099,501,664 bytes
SHA-256:   662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29
```

---

# Phase A — local target-machine gate

## What this phase proves

This phase executes the complete `RealModelIntegration` category on the target
Windows machine using the same published feasibility executable and
child-process harness intended for the future trusted workflow.

It covers:

- exact Granite success evidence;
- three sequential repeatability runs;
- post-preflight cancellation;
- native-load-scoped cancellation;
- the committed malformed GGUF fixture matrix;
- deterministic random bytes with a `.gguf` extension;
- missing, directory, locked-model, locked-output, invalid-parent and
  model/output-collision scenarios;
- canonical-path and chat-template privacy;
- process-owned TCP endpoint observation;
- model integrity before and after the complete suite;
- retained-evidence scanning for accidental model copies.

It does not prove that the GitHub Actions service account can read the model or
that post-default-branch workflow dispatch and artifact upload work. Those are
Phase B concerns.

## Complete Developer PowerShell command

Run from **Visual Studio Developer PowerShell**:

```powershell
# Fail on PowerShell errors and uninitialised variables.
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# Define the repository, branch and exact controlled model.
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

# Enter the repository and protect uncommitted work.
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

# Pull only a fast-forward update.
git pull `
    --ff-only `
    origin `
    $Branch

if ($LASTEXITCODE -ne 0) {
    throw "Git pull failed with exit code $LASTEXITCODE."
}

Write-Host "Current branch head:" -ForegroundColor Cyan
git log -1 --oneline

# Resolve and verify the exact controlled model.
$ModelPath = Join-Path `
    $env:USERPROFILE `
    "Downloads\$KnownModelFileName"

if (-not (Test-Path -LiteralPath $ModelPath -PathType Leaf)) {
    throw "Controlled Granite model not found: $ModelPath"
}

$Model = Get-Item -LiteralPath $ModelPath

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

# Define the feasibility and trusted-test projects.
$SpikeProject = `
    "tools\ModelInspection.LlamaSharpSpike\ModelInspection.LlamaSharpSpike.csproj"

$TrustedTestProject = `
    "tools\ModelInspection.LlamaSharpSpike.RealModelIntegrationTests\ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.csproj"

# Create fresh publish and retained-evidence directories.
$RunId = Get-Date -Format "yyyyMMdd-HHmmss"

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

# Configure only the variables consumed by the trusted tests.
$env:LLAMASHARP_SPIKE_PUBLISH_DIR = $PublishDirectory
$env:GRANITE_TEST_MODEL_PATH = $ModelPath
$env:LLAMASHARP_REAL_MODEL_EVIDENCE_DIR = $EvidenceDirectory

# Restore and publish the exact Windows x64 probe.
dotnet restore $SpikeProject --runtime win-x64

if ($LASTEXITCODE -ne 0) {
    throw "Feasibility project restore failed: $LASTEXITCODE"
}

dotnet restore $TrustedTestProject --runtime win-x64

if ($LASTEXITCODE -ne 0) {
    throw "Trusted test restore failed: $LASTEXITCODE"
}

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

# Run all trusted real-model integration tests. Keep the exit code so the
# independent integrity and privacy gates still run after a test failure.
$TrustedTestExitCode = $null

try {
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
    # Re-hash the controlled model even when tests fail.
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

# Scan retained evidence before treating the run as safe. This implementation
# deliberately avoids System.IO.Path.GetRelativePath because that method is not
# available in every Developer PowerShell host used by this project.
$ArtifactFindings = @()
$MaximumEvidenceBytes = 100MB

$EvidenceRootFullPath = (
    [IO.Path]::GetFullPath($EvidenceDirectory)
).TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar)

$EvidenceRootPrefix = `
    $EvidenceRootFullPath +
    [IO.Path]::DirectorySeparatorChar

$EvidenceFiles = @(
    Get-ChildItem `
        -LiteralPath $EvidenceDirectory `
        -File `
        -Recurse `
        -ErrorAction Stop)

foreach ($File in $EvidenceFiles) {
    $FileFullPath = [IO.Path]::GetFullPath($File.FullName)

    if (-not $FileFullPath.StartsWith(
        $EvidenceRootPrefix,
        [StringComparison]::OrdinalIgnoreCase)) {

        $ArtifactFindings += [PSCustomObject]@{
            RelativePath = $FileFullPath
            Reason = "File resolved outside the retained evidence root."
        }

        continue
    }

    $RelativePath = $FileFullPath.Substring(
        $EvidenceRootPrefix.Length)

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
            Reason = "Evidence file exceeds the 100 MB safety limit."
        }

        continue
    }

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

if ($ArtifactFindings.Count -gt 0) {
    $FindingText = (
        $ArtifactFindings |
        Format-Table RelativePath, Reason -AutoSize |
        Out-String)

    throw @"
The retained-evidence privacy scan failed.

$FindingText
"@
}

if ($TrustedTestExitCode -ne 0) {
    throw "Trusted real-model tests failed with exit code $TrustedTestExitCode."
}

Write-Host `
    "Trusted local real-model gate fully passed." `
    -ForegroundColor Green

Write-Host `
    "Evidence files scanned: $($EvidenceFiles.Count)" `
    -ForegroundColor Green

Write-Host `
    "Retained evidence: $EvidenceDirectory" `
    -ForegroundColor Green
```

## Phase A failure handling

Do not immediately rerun without reading the first failing test and its retained
child-process evidence.

- A restore or publish failure means no real-model test ran.
- A controlled-model precondition failure means the selected file identity is
  wrong.
- A cancellation failure must be examined separately from ordinary load
  failure.
- A native abort must remain contained in the child-process result.
- A model-integrity mismatch blocks all further work.
- An evidence privacy finding blocks retention and upload.

## Verified Phase A result

The command boundary above completed on 2026-08-05:

```text
Tests:                    20 / 20 passed
Model hash unchanged:     yes
Evidence files scanned:   56
Privacy findings:         0
```

See the [formal Tier 2 evidence record](../evidence/2026-08-05-llamasharp-tier2-local-verification.md).

---

# Phase B — self-hosted service-account workflow

## Why this phase waits

GitHub documents that `workflow_dispatch` triggers only when the workflow file
exists on the repository default branch. The trusted workflow is new on the
feature branch and cannot be the first pre-merge execution route without
weakening its approved trigger policy.

Do not add an automatic pull-request trigger to the self-hosted workflow.
Unreviewed pull-request code must not execute implicitly on the
repository-owned target machine.

After the workflow exists on `main`, `--ref` may select the branch or tag whose
workflow version should run.

## Runner-visible model staging

The runner service account should read a shared, read-only model copy outside
the repository, for example:

```text
C:\Users\Public\Documents\GraniteEdgeAI\Models\granite-4.1-3b-Q4_K_M.gguf
```

The staging operation must:

1. identify the running `actions.runner.*` Windows service account;
2. verify the source model filename, byte length and SHA-256;
3. copy the model outside the repository;
4. set the staged file read-only;
5. grant the runner account read/traverse access only;
6. verify the staged identity again;
7. configure the repository variable `GRANITE_TEST_MODEL_PATH`;
8. dispatch `llamasharp-real-model-integration.yml` manually;
9. watch the run with `--exit-status`;
10. download only privacy-scanned retained evidence.

Expected workflow order:

```text
Validate model identity from the runner service account
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

The local target-machine Tier 2 runtime gate is closed by the verified 20/20
result. Phase B remains a deployment/workflow-registration and service-account
access check after the workflow exists on the default branch. It does not
replace the local runtime evidence.