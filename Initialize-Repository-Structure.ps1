<#
.SYNOPSIS
Creates the recommended repository structure for the IBM Granite + TurboQuant project.

.DESCRIPTION
Run this script from anywhere inside the existing Git repository.
It finds the repository root, creates folders and starter files, updates .gitignore,
and does not move or overwrite the existing WinUI solution.

.PARAMETER CommitAndPush
Optionally commits and pushes the new structure. For safety, this option only works
when the repository was clean before the script started.
#>

param(
    [switch]$CommitAndPush
)

# Stop immediately when a command fails.
$ErrorActionPreference = "Stop"

# Catch misspelled or undefined variables.
Set-StrictMode -Version Latest

# Print a readable section heading.
function Write-Step {
    param([Parameter(Mandatory = $true)][string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

# Create a directory only when it does not already exist.
function Ensure-Directory {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
        Write-Host "Created directory: $Path" -ForegroundColor Green
    }
    else {
        Write-Host "Kept existing directory: $Path" -ForegroundColor DarkGray
    }
}

# Create a UTF-8 text file only when it does not already exist.
function Ensure-TextFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Content
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        $parent = Split-Path -Parent $Path
        if ($parent) { Ensure-Directory -Path $parent }
        Set-Content -LiteralPath $Path -Value $Content -Encoding UTF8
        Write-Host "Created file: $Path" -ForegroundColor Green
    }
    else {
        Write-Host "Kept existing file: $Path" -ForegroundColor DarkGray
    }
}

# Confirm that Git is available.
Write-Step "Checking Git"
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw "Git was not found. Install Git for Windows or use Visual Studio Developer PowerShell."
}

# Find and enter the current repository root.
Write-Step "Finding the repository root"
$repositoryRoot = (& git rev-parse --show-toplevel 2>$null).Trim()
if (-not $repositoryRoot) {
    throw "This folder is not inside a Git repository. Open PowerShell inside the project repository."
}
Set-Location -LiteralPath $repositoryRoot
Write-Host "Repository root: $repositoryRoot" -ForegroundColor Yellow

# Protect unrelated local changes when automatic commit/push is requested.
$preExistingChanges = @(& git status --porcelain)
if ($CommitAndPush -and $preExistingChanges.Count -gt 0) {
    Write-Host ""
    Write-Host "Automatic commit/push cancelled because uncommitted work already exists." -ForegroundColor Red
    Write-Host "Commit the ModelImportPage work first, then rerun this script." -ForegroundColor Yellow
    & git status
    exit 1
}

# Create the final supporting folder structure without moving the current app.
Write-Step "Creating folders"
$directories = @(
    ".github/ISSUE_TEMPLATE",
    "docs/planning",
    "docs/requirements",
    "docs/architecture/diagrams",
    "docs/architecture/decisions",
    "docs/research",
    "docs/testing/test-reports",
    "docs/ux/wireframes",
    "docs/ux/screenshots",
    "docs/risks",
    "docs/manuals",
    "docs/journal",
    "docs/evidence",
    "tests/UnitTests",
    "tests/IntegrationTests",
    "tests/TestFixtures",
    "experiments/manifests",
    "experiments/protocols",
    "experiments/prompts",
    "experiments/rubrics",
    "experiments/scripts",
    "experiments/raw-results",
    "experiments/processed-results",
    "experiments/figures",
    "report/chapters",
    "report/figures",
    "scripts",
    "release-evidence",
    "models",
    "third-party"
)
foreach ($directory in $directories) {
    Ensure-Directory -Path (Join-Path $repositoryRoot $directory)
}

# Create the controlled project-definition starter.
$projectDefinition = @'
# Project Definition and Scope Baseline

**Version:** 1.0  
**Status:** Baseline for supervisor review  
**Created:** 11 July 2026  
**Scope-freeze date:** 14 July 2026  
**Target development-completion date:** 15 August 2026  
**Project:** IBM Granite Edge AI Optimisation using TurboQuant  
**Primary platform:** Windows 11 on consumer Intel hardware  

---

## 1. Problem Statement

Insert the agreed problem statement.

## 2. Project Aim

Insert one measurable overall aim.

## 3. Research Questions

### RQ1 â€” Runtime and Hardware Feasibility

**Question:** Insert the frozen wording.  
**Evidence required:** List the tests and measurements.

### RQ2 â€” TurboQuant Effectiveness

**Question:** Insert the frozen wording.  
**Evidence required:** List activation, memory, speed, quality and stability evidence.

### RQ3 â€” Memory-Fit Prediction and Configuration Selection

**Question:** Insert the frozen wording.  
**Evidence required:** List predicted-versus-measured evidence.

### RQ4 â€” End-to-End Desktop Application

**Question:** Insert the frozen wording.  
**Evidence required:** List application, failure-handling and UX evidence.

## 4. Project Objectives

1. Insert measurable objectives.
2. Link every objective to at least one requirement or RQ.

## 5. First-Release Scope

### 5.1 Must Have

- Insert committed first-release work.

### 5.2 Should Have

- Insert work attempted after the Must Haves are stable.

### 5.3 Experimental

- Insert work that requires a technical evidence gate.

### 5.4 Deferred

- Insert work deliberately excluded from the first release.

## 6. Claims Not Made by This Project

- No universal Granite or Intel compatibility claim.
- No guaranteed TurboQuant or OpenVINO success claim.
- TurboQuant does not create a smaller GGUF weight file.
- Measured results, estimates and paper claims remain separate.

## 7. Definition of a Satisfactory Project Outcome

Insert measurable completion conditions.

## 8. Constraints and Assumptions

### 8.1 Constraints

Insert time, hardware, runtime, licensing and access constraints.

### 8.2 Assumptions

Insert assumptions and explain how each will be checked.

## 9. Scope Change Rule

Version 1.0 becomes the working scope baseline on 14 July 2026.

Any major change must record the date, reason, evidence, schedule effect, risk effect,
work removed to create capacity, final decision and significant supervisor feedback.

No new major feature will be added after 10 August 2026.
'@
Ensure-TextFile -Path (Join-Path $repositoryRoot "docs/planning/Project-Definition-v1.md") -Content $projectDefinition

# Create the scope-change log.
$scopeChangeLog = @'
# Scope Change Log

| ID | Date | Proposed change | Reason | Schedule effect | Risk effect | Decision | Evidence or approval |
|---|---|---|---|---|---|---|---|
| SC-001 | 11-Jul-2026 | Establish Project Definition v1 | Prevent scope drift | Creates a controlled baseline | Reduces uncontrolled growth | Accepted | Project Definition v1 |
'@
Ensure-TextFile -Path (Join-Path $repositoryRoot "docs/planning/Scope-Change-Log.md") -Content $scopeChangeLog

# Create the research-question evidence map.
$rqMap = @'
# Research Question Evidence Map

| RQ | Question summary | Existing evidence | Remaining evidence | Report location | Status |
|---|---|---|---|---|---|
| RQ1 | Runtime and hardware feasibility | Existing llama.cpp tests | OpenVINO and app-integrated confirmation | Chapters 4, 8 and 9 | Planned |
| RQ2 | TurboQuant effectiveness | AtomicBot and animehacker tests | App-integrated comparison where feasible | Chapters 4, 8 and 9 | Planned |
| RQ3 | Memory-fit prediction | Existing runtime measurements | Estimator implementation and calibration | Chapters 4, 6, 8 and 9 | Planned |
| RQ4 | End-to-end application | Initial UI work | Application tests and UX evaluation | Chapters 6, 7, 8 and 9 | Planned |
'@
Ensure-TextFile -Path (Join-Path $repositoryRoot "docs/planning/Research-Question-Evidence-Map.md") -Content $rqMap

# Create a reusable Architecture Decision Record template.
$adrTemplate = @'
# ADR-XXX: Decision Title

**Status:** Proposed  
**Date:** YYYY-MM-DD  
**Decision owner:** Project developer  

## Context
Explain the engineering problem and constraints.

## Options Considered
1. Option A
2. Option B
3. Option C

## Decision
State the chosen option.

## Reasons
Explain why it was chosen.

## Trade-offs and Consequences
### Benefits
- List benefits.

### Costs and risks
- List disadvantages and risks.

## Evidence
Link tests, research, logs, diagrams or supervisor feedback.

## Review Trigger
State what new evidence would cause this decision to be reviewed.
'@
Ensure-TextFile -Path (Join-Path $repositoryRoot "docs/architecture/decisions/ADR-Template.md") -Content $adrTemplate

# Create a simple combined risk/assumption/constraint/licence register.
$riskRegister = @'
# Risk, Assumption, Constraint and Licence Register

| ID | Type | Description | Probability | Impact | Validation or trigger | Mitigation | Contingency | Owner | Status | Evidence |
|---|---|---|---|---|---|---|---|---|---|---|
| R-001 | Schedule | Development may exceed 15 August | Medium | High | Weekly review | Protect Must Haves | Defer Experimental and Should work | Project developer | Open | Timetable |
| R-002 | Technical | TurboQuant may fail or silently fall back | High | High | Activation/backend checks | Keep upstream primary | Remove Experimental option | Project developer | Open | Test workbooks |
| A-001 | Assumption | Raw experiment evidence can be recovered | Medium | High | Evidence audit | Recover and checksum now | Repeat minimum critical runs | Project developer | Open | Evidence manifest |
'@
Ensure-TextFile -Path (Join-Path $repositoryRoot "docs/risks/Risk-Register.md") -Content $riskRegister

# Create the daily engineering-journal template.
$journalTemplate = @'
# YYYY-MM-DD â€” Daily Engineering Journal

## Planned work
- State the intended work.

## Work completed
- State what was actually completed.

## Tests and evidence
- List tests, screenshots, logs, measurements and commit hashes.

## Decisions
- Record architecture, scope and implementation decisions.

## Problems and blockers
- State the problem, effect and next diagnostic action.

## Time spent
- Focused engineering hours:

## Next action
- State the next smallest demonstrable task.
'@
Ensure-TextFile -Path (Join-Path $repositoryRoot "docs/journal/Journal-Template.md") -Content $journalTemplate

# Create a reproducible experiment-manifest template.
$experimentManifest = @'
{
  "experiment_id": "EXP-YYYY-MM-DD-001",
  "research_question": "RQ1",
  "purpose": "Describe the experiment purpose.",
  "timestamp_utc": "YYYY-MM-DDTHH:MM:SSZ",
  "hardware": {
    "cpu": "",
    "gpu": "",
    "npu": "",
    "installed_ram_bytes": 0,
    "available_ram_before_bytes": 0,
    "operating_system": "",
    "power_mode": ""
  },
  "model": {
    "name": "",
    "source": "",
    "revision": "",
    "filename": "",
    "sha256": "",
    "format": "",
    "weight_quantisation": "",
    "licence": ""
  },
  "runtime": {
    "repository": "",
    "commit": "",
    "executable_sha256": "",
    "compiler": "",
    "build_options": [],
    "requested_backend": "",
    "actual_backend": "",
    "requested_device": "",
    "actual_device": ""
  },
  "configuration": {
    "context_length": 0,
    "kv_cache_type": "",
    "threads": 0,
    "gpu_layers": 0,
    "seed": 0,
    "sampling_settings": {}
  },
  "command": "",
  "raw_result_directory": "",
  "notes": ""
}
'@
Ensure-TextFile -Path (Join-Path $repositoryRoot "experiments/manifests/experiment-manifest-template.json") -Content $experimentManifest

# Create short README files that explain the major folders.
$readmes = @{
    "docs/README.md" = "# Project Documentation`n`nControlled planning, requirements, architecture, research, testing, UX, risk, manuals, journals and evidence live here.`n"
    "docs/requirements/README.md" = "# Requirements`n`nStore the versioned MoSCoW baseline, stable requirement IDs, acceptance criteria, use cases and traceability matrix here.`n"
    "docs/architecture/README.md" = "# Architecture`n`nStore context, component, process and deployment diagrams here. Record important choices in short ADRs.`n"
    "docs/research/README.md" = "# Research Notes`n`nStore project-specific notes and comparison tables. Do not upload copyrighted textbooks or papers.`n"
    "docs/testing/Test-Strategy.md" = "# Application Testing Strategy`n`nUse the existing full testing standard as the controlling strategy. Store app-specific unit, contract, integration, E2E, failure, regression, compatibility, security, offline, UX and acceptance evidence here.`n"
    "docs/ux/README.md" = "# UX Evidence`n`nStore personas, journeys, wireframes, screenshots, accessibility checks and task-based evaluation evidence here.`n"
    "docs/manuals/README.md" = "# Manuals`n`nCreate User-Manual.md, Developer-Manual.md, Build-and-Installation.md and Known-Limitations.md before release.`n"
    "docs/evidence/README.md" = "# Evidence Index`n`nStore small report-ready evidence and indexes here. Large raw results belong in experiments/raw-results/.`n"
    "tests/README.md" = "# Tests`n`nCreate real Visual Studio unit and integration test projects here when implementation begins.`n"
    "experiments/README.md" = "# Experiments`n`nEvery formal experiment needs an ID, protocol, manifest, exact hashes, raw stdout/stderr, raw measurements, failed runs and reproducible processing scripts.`n"
    "report/README.md" = "# Project Report`n`nStore LaTeX source, chapters, bibliography and report figures here. Explain evidence in the report; preserve raw evidence under experiments/.`n"
    "scripts/README.md" = "# Automation Scripts`n`nStore repeatable build, test, benchmark, evidence-validation and release scripts here.`n"
    "release-evidence/README.md" = "# Release Evidence`n`nRecord the release tag, package checksum, clean build/install log, manuals, demo and final requirement status.`n"
    "models/README.md" = "# Local Model Files`n`nDo not commit large model files. Record model source, revision, licence, filename and SHA-256 in an experiment manifest.`n"
    "third-party/README.md" = "# Third-Party Runtime Register`n`nRecord repository, commit/release, licence, compiler flags, executable hash, local path and project patches.`n"
}
foreach ($relativePath in $readmes.Keys) {
    Ensure-TextFile -Path (Join-Path $repositoryRoot $relativePath) -Content $readmes[$relativePath]
}

# Add .gitkeep files so empty evidence folders appear in Git.
$gitKeeps = @(
    "docs/architecture/diagrams/.gitkeep",
    "docs/testing/test-reports/.gitkeep",
    "docs/ux/wireframes/.gitkeep",
    "docs/ux/screenshots/.gitkeep",
    "tests/UnitTests/.gitkeep",
    "tests/IntegrationTests/.gitkeep",
    "tests/TestFixtures/.gitkeep",
    "experiments/protocols/.gitkeep",
    "experiments/prompts/.gitkeep",
    "experiments/rubrics/.gitkeep",
    "experiments/scripts/.gitkeep",
    "experiments/raw-results/.gitkeep",
    "experiments/processed-results/.gitkeep",
    "experiments/figures/.gitkeep",
    "report/chapters/.gitkeep",
    "report/figures/.gitkeep"
)
foreach ($relativePath in $gitKeeps) {
    Ensure-TextFile -Path (Join-Path $repositoryRoot $relativePath) -Content ""
}

# Append safe project-specific ignore rules without deleting current rules.
Write-Step "Updating .gitignore safely"
$gitIgnorePath = Join-Path $repositoryRoot ".gitignore"
$marker = "# === IBM Granite project support ignores ==="
$ignoreBlock = @'

# === IBM Granite project support ignores ===

# Visual Studio and build output
.vs/
**/bin/
**/obj/
TestResults/
*.user
*.suo

# Secrets and machine-local configuration
.env
.env.*
secrets.json
appsettings.Local.json

# Large local models and downloads
models/*
!models/README.md
downloads/
*.gguf
*.safetensors
*.onnx
*.pt
*.pth
*.ckpt

# Third-party local binaries/builds
third-party/bin/
third-party/build/
third-party/cache/

# Temporary files
temp/
cache/
*.tmp
'@

if (-not (Test-Path -LiteralPath $gitIgnorePath)) {
    Set-Content -LiteralPath $gitIgnorePath -Value $ignoreBlock.TrimStart() -Encoding UTF8
    Write-Host "Created .gitignore" -ForegroundColor Green
}
else {
    $currentIgnore = Get-Content -LiteralPath $gitIgnorePath -Raw
    if ($currentIgnore -notmatch [regex]::Escape($marker)) {
        Add-Content -LiteralPath $gitIgnorePath -Value $ignoreBlock -Encoding UTF8
        Write-Host "Added project rules to .gitignore" -ForegroundColor Green
    }
    else {
        Write-Host "Kept existing project .gitignore block" -ForegroundColor DarkGray
    }
}

# Show exactly what changed.
Write-Step "Structure created"
& git status --short

# Optionally commit and push the generated structure.
if ($CommitAndPush) {
    Write-Step "Committing the generated structure"
    & git add .gitignore docs tests experiments report scripts release models third-party
    & git commit -m "chore: add project documentation and evidence structure"

    Write-Step "Pushing to the configured upstream branch"
    & git push
    Write-Host "Structure committed and pushed successfully." -ForegroundColor Green
}
else {
    Write-Host ""
    Write-Host "Review the files, then run:" -ForegroundColor Yellow
    Write-Host "git add .gitignore docs tests experiments report scripts release models third-party"
    Write-Host 'git commit -m "chore: add project documentation and evidence structure"'
    Write-Host "git push"
}

Write-Host ""
Write-Host "The existing WinUI solution was not moved or overwritten." -ForegroundColor Cyan

