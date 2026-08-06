# Model Inspection Cleanup Phase 0 Baseline and Inventory Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** repair the known unified-workflow test-discovery defect, prove the unchanged Model Inspection implementation on one exact branch head and establish a complete repeatable review inventory before any structural refactoring begins

**Architecture:** protect the workflow correction with a focused contract test, then run the complete hosted Windows campaign and retain machine-readable evidence. Build the review ledger from the deduplicated PR #44/#45/#47 file union plus all current included roots, and enforce completeness through a contract test and a PowerShell verification wrapper.

**Tech Stack:** C# 12, repository-selected .NET SDK `10.0.301` with `latestPatch` roll-forward, Microsoft Testing Platform, MSTest, PowerShell 7, Git, GitHub CLI or GitHub connector, GitHub Actions `windows-latest`, TRX and SHA-256

## Global Constraints

- work only on `refactor/model-inspection-cleanup`
- preserve the stacked base `feature/model-inspection-worker-host` at `a4138a613dd643abe12858eec5d1c3beb09e95e7`
- Phase 0 changes no production C#, XAML, protocol, process, worker or LLamaSharp behaviour
- remove only the incompatible contract-test category filter from the unified workflow
- keep `--minimum-expected-tests 75` so zero or reduced discovery fails closed
- preserve every Gate 2 build, publish, test, orphan, privacy and artifact step
- retain exact TRX files and artifact metadata from the exact verified head
- build the source inventory from PRs #44, #45 and #47 plus every current included root and connected file
- every source path must appear exactly once in the human review ledger
- new or moved in-scope files after the cleanup base must fail the inventory contract until added
- comments added by this phase use simple English, start with a lower-case letter and do not end with a full stop
- do not rewrite historical evidence to imply that it tested the cleanup branch
- do not begin Phase 1 until all Phase 0 acceptance criteria are green

---

### Task 1: Establish the Isolated Execution Workspace

**Files:**
- Read: `docs/superpowers/specs/2026-08-06-model-inspection-cleanup-design.md`
- Read: `docs/superpowers/plans/2026-08-06-model-inspection-cleanup-master.md`
- Read: `.github/workflows/build-and-test.yml`
- Read: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/BuildWorkflowContractTests.cs`

**Interfaces:**
- Consumes: remote branch `refactor/model-inspection-cleanup`
- Produces: clean isolated worktree at the exact remote head

- [ ] **Step 1: Create or select a dedicated worktree**

Use `superpowers:using-git-worktrees` before editing

```powershell
git fetch origin
git worktree add ..\model-inspection-cleanup refactor/model-inspection-cleanup
Set-Location ..\model-inspection-cleanup
```

Expected: the worktree checks out `refactor/model-inspection-cleanup` without modifying another feature workspace

- [ ] **Step 2: Verify branch identity and cleanliness**

```powershell
git branch --show-current
git rev-parse HEAD
git status --short
```

Expected:

```text
branch: refactor/model-inspection-cleanup
status: no output
```

Record the starting head in the execution notes. Do not continue if the branch or worktree is dirty

- [ ] **Step 3: Verify the repository-selected SDK and test runner**

```powershell
Get-Content global.json
dotnet --version
dotnet --info
```

Expected: SDK `10.0.301` or a later patch selected by `latestPatch`, with Microsoft Testing Platform selected in `global.json`

---

### Task 2: Add a Failing Contract Test for Complete Contract-Project Discovery

**Files:**
- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/BuildWorkflowContractTests.cs`
- Test: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/BuildWorkflowContractTests.cs`

**Interfaces:**
- Consumes: workflow step named `Run Model Inspection contract tests`
- Produces: `BuildWorkflowRunsCompleteContractProjectWithoutCategoryFilter()` and `ExtractWorkflowStep(string workflow, string stepName)`

- [ ] **Step 1: Add the focused failing test**

Add this test after `BuildWorkflowContainsCurrentContractGate`

```csharp
[TestMethod]
public void BuildWorkflowRunsCompleteContractProjectWithoutCategoryFilter()
{
    string workflow = ReadWorkflow();
    string contractStep = ExtractWorkflowStep(
        workflow,
        "Run Model Inspection contract tests");

    Assert.IsFalse(
        contractStep.Contains("--filter", StringComparison.Ordinal),
        "The complete contract project must run because the category filter discovers zero tests under the selected Microsoft Testing Platform configuration.");
    StringAssert.Contains(
        contractStep,
        "--minimum-expected-tests 75",
        "The contract floor must remain after the incompatible filter is removed.");
}
```

Add this helper before `CountOccurrences`

```csharp
private static string ExtractWorkflowStep(
    string workflow,
    string stepName)
{
    string marker = $"- name: {stepName}";
    int stepStart = workflow.IndexOf(marker, StringComparison.Ordinal);
    Assert.IsTrue(
        stepStart >= 0,
        $"Workflow step was not found: {stepName}");

    int nextStep = workflow.IndexOf(
        "\n      - name:",
        stepStart + marker.Length,
        StringComparison.Ordinal);

    return nextStep >= 0
        ? workflow[stepStart..nextStep]
        : workflow[stepStart..];
}
```

Do not change the workflow yet

- [ ] **Step 2: Restore the contract project**

```powershell
dotnet restore "tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj"
```

Expected: restore succeeds

- [ ] **Step 3: Run the new test and preserve the intended red result**

```powershell
dotnet test "tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" `
  --configuration Release `
  --no-restore `
  --filter "FullyQualifiedName~BuildWorkflowRunsCompleteContractProjectWithoutCategoryFilter" `
  --minimum-expected-tests 1
```

Expected: one test is discovered and fails because the workflow step still contains `--filter "TestCategory=Contract"`

- [ ] **Step 4: Commit the red contract test**

```powershell
git add "tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/BuildWorkflowContractTests.cs"
git commit -m "test(model-inspection): expose contract discovery defect"
```

---

### Task 3: Correct the Unified Workflow Without Weakening the Test Floor

**Files:**
- Modify: `.github/workflows/build-and-test.yml`
- Test: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/BuildWorkflowContractTests.cs`

**Interfaces:**
- Consumes: complete contract test project
- Produces: a contract step with no category filter and a minimum expected count of `75`

- [ ] **Step 1: Remove only the incompatible filter line**

Change this command

```yaml
          dotnet test "$env:CONTRACT_TEST_PROJECT" `
            --configuration Release `
            --no-restore `
            --filter "TestCategory=Contract" `
            --minimum-expected-tests 75 `
```

to

```yaml
          dotnet test "$env:CONTRACT_TEST_PROJECT" `
            --configuration Release `
            --no-restore `
            --minimum-expected-tests 75 `
```

Do not alter the logger, results directory, step order, orphan check, privacy scan or artifact upload

- [ ] **Step 2: Run the focused workflow contract test**

```powershell
dotnet test "tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" `
  --configuration Release `
  --no-restore `
  --filter "FullyQualifiedName~BuildWorkflowRunsCompleteContractProjectWithoutCategoryFilter" `
  --minimum-expected-tests 1
```

Expected: one test passes

- [ ] **Step 3: Run the complete contract project without a filter**

```powershell
dotnet test "tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" `
  --configuration Release `
  --no-restore `
  --minimum-expected-tests 75
```

Expected: at least 75 tests run, with zero failed and zero skipped

- [ ] **Step 4: Review the exact diff**

```powershell
git diff --check
git diff -- .github/workflows/build-and-test.yml `
  tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/BuildWorkflowContractTests.cs
```

Expected: one workflow command correction plus the focused contract test and helper

- [ ] **Step 5: Commit the green workflow correction**

```powershell
git add .github/workflows/build-and-test.yml `
  tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/BuildWorkflowContractTests.cs

git commit -m "fix(ci): run complete model inspection contract suite"
```

---

### Task 4: Obtain the Exact-Head Hosted Baseline

**Files:**
- Read: `.github/workflows/build-and-test.yml`
- Create later in Task 8: `docs/testing/evidence/2026-08-06-model-inspection-cleanup-baseline.md`

**Interfaces:**
- Consumes: workflow-fix commit from Task 3
- Produces: successful `Build and test` run, Gate 2 artifact and packaged-test artifact on the same head

- [ ] **Step 1: Push the branch**

```powershell
git push origin refactor/model-inspection-cleanup
$baselineHead = git rev-parse HEAD
Write-Host "baseline head: $baselineHead"
```

Expected: the push starts the `Build and test` workflow for `$baselineHead`

- [ ] **Step 2: Locate the exact-head workflow run**

With GitHub CLI

```powershell
$run = gh run list `
  --workflow build-and-test.yml `
  --branch refactor/model-inspection-cleanup `
  --limit 10 `
  --json databaseId,headSha,status,conclusion,attempt,createdAt `
  | ConvertFrom-Json `
  | Where-Object headSha -eq $baselineHead `
  | Select-Object -First 1

if ($null -eq $run) {
  throw "No Build and test run was found for $baselineHead"
}

$runId = [long]$run.databaseId
gh run watch $runId --exit-status
```

When using the GitHub connector, fetch runs for the branch, select the run whose `head_sha` equals `$baselineHead`, then poll its jobs until completion

Expected: conclusion `success`

- [ ] **Step 3: Diagnose any failure from the first failing step only**

If the run fails, use `superpowers:systematic-debugging`

```powershell
gh run view $runId --log-failed
```

Record the first failing command and its exact error. Add a focused regression test where practical, correct only the proven cause, commit, push and repeat Steps 1 and 2 on the new exact head. Do not weaken test floors, privacy checks, orphan checks or artifact conditions to make the workflow green

- [ ] **Step 4: Capture run and job metadata**

```powershell
$runJson = gh run view $runId --json databaseId,headSha,attempt,status,conclusion,createdAt,updatedAt,jobs,url | ConvertFrom-Json
$job = $runJson.jobs | Where-Object name -eq "Build WinUI and run unit tests" | Select-Object -First 1

if ($runJson.headSha -ne $baselineHead -or $runJson.conclusion -ne "success") {
  throw "The selected run is not a successful exact-head baseline"
}

$jobId = [long]$job.databaseId
```

Expected: exact head matches and the job succeeded

- [ ] **Step 5: Confirm orphan and privacy steps succeeded**

```powershell
$requiredSteps = @(
  "Check for orphaned Gate 2 processes"
  "Scan retained evidence for sensitive data"
  "Upload Gate 2 verification results"
  "Upload unit test results"
)

foreach ($stepName in $requiredSteps) {
  $step = $job.steps | Where-Object name -eq $stepName | Select-Object -First 1
  if ($null -eq $step -or $step.conclusion -ne "success") {
    throw "Required baseline step did not succeed: $stepName"
  }
}
```

Expected: all required steps succeeded

---

### Task 5: Download and Validate Baseline Artifacts

**Files:**
- Temporary local directory: `artifacts/model-inspection-cleanup-baseline/`
- Create later: `docs/testing/evidence/2026-08-06-model-inspection-cleanup-baseline.md`

**Interfaces:**
- Consumes: successful `$runId`
- Produces: exact test counters and SHA-256 digests for both artifacts

- [ ] **Step 1: List artifacts for the successful run**

```powershell
$artifactRoot = Join-Path $PWD "artifacts/model-inspection-cleanup-baseline"
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null

$artifacts = gh api "repos/arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant/actions/runs/$runId/artifacts" | ConvertFrom-Json
$gate2Artifact = $artifacts.artifacts | Where-Object name -eq "gate2-verification-$runId-$($runJson.attempt)" | Select-Object -First 1
$unitArtifact = $artifacts.artifacts | Where-Object name -eq "unit-test-results-$runId-$($runJson.attempt)" | Select-Object -First 1

if ($null -eq $gate2Artifact -or $null -eq $unitArtifact) {
  throw "Both exact baseline artifacts are required"
}
```

Expected: one Gate 2 artifact and one packaged unit-test artifact

- [ ] **Step 2: Download both artifacts**

```powershell
gh run download $runId `
  --name $gate2Artifact.name `
  --dir (Join-Path $artifactRoot "gate2")

gh run download $runId `
  --name $unitArtifact.name `
  --dir (Join-Path $artifactRoot "unit")
```

Expected: TRX files and the Gate 2 publish manifest are present

- [ ] **Step 3: Parse every TRX counter**

```powershell
$trxResults = foreach ($trxFile in Get-ChildItem $artifactRoot -Recurse -Filter *.trx) {
  [xml]$trx = Get-Content -LiteralPath $trxFile.FullName -Raw
  $counters = $trx.TestRun.ResultSummary.Counters

  [pscustomobject]@{
    File = $trxFile.Name
    Total = [int]$counters.total
    Executed = [int]$counters.executed
    Passed = [int]$counters.passed
    Failed = [int]$counters.failed
    Error = [int]$counters.error
    Timeout = [int]$counters.timeout
    Aborted = [int]$counters.aborted
    Inconclusive = [int]$counters.inconclusive
    NotExecuted = [int]$counters.notExecuted
  }
}

$trxResults | Sort-Object File | Format-Table -AutoSize

$badResults = $trxResults | Where-Object {
  $_.Total -le 0 -or
  $_.Failed -ne 0 -or
  $_.Error -ne 0 -or
  $_.Timeout -ne 0 -or
  $_.Aborted -ne 0 -or
  $_.Inconclusive -ne 0 -or
  $_.NotExecuted -ne 0 -or
  $_.Passed -ne $_.Total
}

if ($badResults) {
  throw "One or more baseline TRX files contain a failed, skipped or zero-test result"
}
```

Expected: every retained suite has a positive count and all tests passed

- [ ] **Step 4: Verify all required TRX files exist**

```powershell
$requiredTrxFiles = @(
  "GraniteEdgeAI.ModelInspection.Contracts.Tests.trx"
  "GraniteEdgeAI.ModelInspection.Transport.Tests.trx"
  "GraniteEdgeAI.ModelInspection.Worker.Tests.trx"
  "GraniteEdgeAI.ModelInspection.WorkerClient.Tests.trx"
  "GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.trx"
  "GraniteEdgeAI.UnitTests.trx"
)

foreach ($requiredFile in $requiredTrxFiles) {
  if (-not (Get-ChildItem $artifactRoot -Recurse -Filter $requiredFile)) {
    throw "Required baseline TRX file is missing: $requiredFile"
  }
}
```

- [ ] **Step 5: Compute artifact-content digests**

GitHub may provide an artifact digest. Also create a deterministic local digest over sorted retained files

```powershell
function Get-DirectoryDigest([string]$Path) {
  $records = Get-ChildItem -LiteralPath $Path -Recurse -File |
    Sort-Object FullName |
    ForEach-Object {
      $relative = [System.IO.Path]::GetRelativePath($Path, $_.FullName).Replace('\', '/')
      $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
      "$relative`t$hash"
    }

  $manifest = [string]::Join("`n", $records)
  $bytes = [System.Text.Encoding]::UTF8.GetBytes($manifest)
  $sha = [System.Security.Cryptography.SHA256]::HashData($bytes)
  return [Convert]::ToHexString($sha).ToLowerInvariant()
}

$gate2ContentDigest = Get-DirectoryDigest (Join-Path $artifactRoot "gate2")
$unitContentDigest = Get-DirectoryDigest (Join-Path $artifactRoot "unit")
```

Record GitHub artifact IDs, names, sizes, server digests when present and deterministic local content digests

---

### Task 6: Generate the Complete Cleanup Source Set

**Files:**
- Create: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Create: `docs/reviews/model-inspection-cleanup-inventory.md`

**Interfaces:**
- Consumes: PR #44, #45 and #47 file lists plus current tracked files
- Produces: sorted unique existing path list and one ledger row per path

- [ ] **Step 1: Generate the PR file union and current-scope union**

Run this PowerShell from the repository root

```powershell
$repository = "arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant"
$sourcePath = "docs/reviews/model-inspection-cleanup-source-files.txt"
$inventoryPath = "docs/reviews/model-inspection-cleanup-inventory.md"

function Test-ModelInspectionScopePath([string]$Path) {
  $normalized = $Path.Replace('\', '/')

  $includedPrefixes = @(
    "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/"
    "shared/GraniteEdgeAI.ModelInspection.Contracts/"
    "shared/GraniteEdgeAI.ModelInspection.Transport/"
    "infrastructure/GraniteEdgeAI.ModelInspection.WorkerClient/"
    "workers/GraniteEdgeAI.ModelInspection.Worker/"
    "tools/ModelInspection.LlamaSharpSpike"
    "tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/"
    "tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests/"
    "tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/"
    "tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/"
    "tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/"
    "tests/ProcessFixtures/GraniteEdgeAI.ModelInspection.ProtocolTestWorker/"
  )

  if ($includedPrefixes | Where-Object { $normalized.StartsWith($_, [StringComparison]::Ordinal) }) {
    return $true
  }

  if ($normalized.StartsWith("IBM Granite with TurboQuant (Intel)/Features/ModelImport/", [StringComparison]::Ordinal) -or
      $normalized.StartsWith("IBM Granite with TurboQuant (Intel)/Features/Onboarding/", [StringComparison]::Ordinal) -or
      $normalized.StartsWith("tests/UnitTests/GraniteEdgeAI.UnitTests/", [StringComparison]::Ordinal)) {
    if (Test-Path -LiteralPath $normalized -PathType Leaf) {
      return (Get-Content -LiteralPath $normalized -Raw).Contains("ModelInspection", [StringComparison]::OrdinalIgnoreCase)
    }
  }

  if ($normalized -eq ".github/workflows/build-and-test.yml" -or
      $normalized -match "^\.github/workflows/(llamasharp|model-inspection)-.*\.yml$") {
    return $true
  }

  if ($normalized.StartsWith("docs/", [StringComparison]::Ordinal) -and
      ($normalized.Contains("model-inspection", [StringComparison]::OrdinalIgnoreCase) -or
       $normalized.Contains("llamasharp", [StringComparison]::OrdinalIgnoreCase))) {
    return $true
  }

  return $normalized -in @(
    "IBM Granite with TurboQuant (Intel).slnx"
    "IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj"
    "IBM Granite with TurboQuant (Intel)/MainWindow.xaml"
    "IBM Granite with TurboQuant (Intel)/MainWindow.xaml.cs"
    "shared/README.md"
    "infrastructure/README.md"
    "workers/README.md"
    "tools/README.md"
  )
}

$prFiles = foreach ($prNumber in 44, 45, 47) {
  gh api --paginate "repos/$repository/pulls/$prNumber/files?per_page=100" --jq '.[].filename'
}

$trackedScopeFiles = git ls-files | Where-Object { Test-ModelInspectionScopePath $_ }

$allFiles = @($prFiles + $trackedScopeFiles) |
  ForEach-Object { $_.Replace('\', '/') } |
  Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
  Sort-Object -Unique

New-Item -ItemType Directory -Force -Path (Split-Path $sourcePath) | Out-Null
[System.IO.File]::WriteAllLines(
  (Join-Path $PWD $sourcePath),
  $allFiles,
  [System.Text.UTF8Encoding]::new($false))
```

Expected: sorted unique UTF-8 path list with no missing file

- [ ] **Step 2: Generate the initial human ledger**

Continue in the same PowerShell session

```powershell
function Get-Subsystem([string]$Path) {
  switch -Regex ($Path) {
    '^IBM Granite with TurboQuant \(Intel\)/Features/ModelInspection/' { return 'WinUI Model Inspection' }
    '^IBM Granite with TurboQuant \(Intel\)/Features/ModelImport/' { return 'Model Import handoff' }
    '^IBM Granite with TurboQuant \(Intel\)/Features/Onboarding/' { return 'Onboarding navigation' }
    '^shared/GraniteEdgeAI\.ModelInspection\.Contracts/' { return 'worker contracts' }
    '^shared/GraniteEdgeAI\.ModelInspection\.Transport/' { return 'transport' }
    '^infrastructure/GraniteEdgeAI\.ModelInspection\.WorkerClient/' { return 'WorkerClient infrastructure' }
    '^workers/GraniteEdgeAI\.ModelInspection\.Worker/' { return 'production worker' }
    '^tools/ModelInspection\.LlamaSharpSpike' { return 'LLamaSharp feasibility' }
    '^tests/ProcessFixtures/' { return 'abnormal process fixture' }
    '^tests/IntegrationTests/' { return 'process integration tests' }
    '^tests/ContractTests/' { return 'contract and architecture tests' }
    '^tests/UnitTests/' { return 'unit tests' }
    '^\.github/workflows/' { return 'GitHub Actions' }
    '^docs/' { return 'documentation and evidence' }
    default { return 'connected project infrastructure' }
  }
}

function Get-Risk([string]$Path) {
  if ($Path -match '/Windows/' -or
      $Path -match 'Native(Methods|Structures|Handle|Backend)' -or
      $Path -match 'Worker(Process|Conversation|Cancellation|Executable|Environment|Handshake|Exit|Failure)' -or
      $Path -match 'Protocol/' -or
      $Path -match '(Privacy|Redactor|Integrity|Hash)') {
    return 'critical'
  }

  if ($Path -match '^workers/' -or
      $Path -match '^tools/ModelInspection\.LlamaSharpSpike' -or
      $Path -match '^\.github/workflows/' -or
      $Path -match 'IntegrationTests|ProcessFixtures') {
    return 'high'
  }

  if ($Path -match 'Contracts|ModelInspection|ModelImport|Onboarding|UnitTests|ContractTests') {
    return 'medium'
  }

  return 'low'
}

function Get-Responsibility([string]$Subsystem) {
  switch ($Subsystem) {
    'WinUI Model Inspection' { return 'present Model Inspection state and actions' }
    'Model Import handoff' { return 'create or forward the validated inspection request' }
    'Onboarding navigation' { return 'navigate to Model Inspection and preserve onboarding stage state' }
    'worker contracts' { return 'define stable worker protocol and evidence data' }
    'transport' { return 'read and write bounded strict UTF-8 protocol frames' }
    'WorkerClient infrastructure' { return 'verify, launch, communicate with and clean up the protected worker process' }
    'production worker' { return 'host one bounded inspection conversation in the isolated worker process' }
    'LLamaSharp feasibility' { return 'prove and test the selected LLamaSharp runtime boundary' }
    'abnormal process fixture' { return 'simulate process failures without adding test switches to production' }
    'process integration tests' { return 'prove real process lifecycle, containment and protocol behaviour' }
    'contract and architecture tests' { return 'protect protocol, project and workflow contracts' }
    'unit tests' { return 'prove isolated behaviour and failure rules' }
    'GitHub Actions' { return 'build, execute and retain verification evidence' }
    'documentation and evidence' { return 'record design, operation, verification and limitations' }
    default { return 'connect the reviewed Model Inspection build and application boundary' }
  }
}

$header = @(
  '# Model Inspection Cleanup Review Inventory'
  ''
  '**Branch:** `refactor/model-inspection-cleanup`  '
  '**Base:** `a4138a613dd643abe12858eec5d1c3beb09e95e7`  '
  '**Source list:** `docs/reviews/model-inspection-cleanup-source-files.txt`  '
  ''
  'This ledger records one review disposition for every file in the complete cleanup scope'
  ''
  '| File | Subsystem | Primary responsibility | Risk | Review status | Findings | Changes made | Behaviour preserved | Tests | Verification evidence | Deferred work and reason |'
  '|---|---|---|---|---|---|---|---|---|---|---|'
)

$rows = foreach ($file in $allFiles) {
  $subsystem = Get-Subsystem $file
  $responsibility = Get-Responsibility $subsystem
  $risk = Get-Risk $file
  "| ``$file`` | $subsystem | $responsibility | $risk | pending review | not reviewed | none | baseline behaviour | to be mapped during subsystem audit | baseline pending | none |"
}

[System.IO.File]::WriteAllLines(
  (Join-Path $PWD $inventoryPath),
  @($header + $rows),
  [System.Text.UTF8Encoding]::new($false))
```

Expected: every source file has exactly one table row with subsystem, responsibility, risk and initial review state

- [ ] **Step 3: Inspect counts and duplicates**

```powershell
$sourceFiles = Get-Content $sourcePath
$inventoryRows = Get-Content $inventoryPath | Where-Object { $_ -match '^\| `[^`]+` \|' }

if ($sourceFiles.Count -eq 0) {
  throw "The cleanup source list is empty"
}
if (($sourceFiles | Sort-Object -Unique).Count -ne $sourceFiles.Count) {
  throw "The cleanup source list contains duplicate paths"
}
if ($inventoryRows.Count -ne $sourceFiles.Count) {
  throw "The inventory row count does not match the source file count"
}
```

---

### Task 7: Add an Executable Inventory Completeness Contract

**Files:**
- Create: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/CleanupInventoryContractTests.cs`
- Create: `scripts/model-inspection/Verify-ModelInspectionCleanupInventory.ps1`
- Test: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/CleanupInventoryContractTests.cs`

**Interfaces:**
- Consumes: source list, inventory ledger, cleanup base SHA and current Git diff
- Produces: three contract tests and one developer/CI verification command

- [ ] **Step 1: Add the inventory contract test file**

Create `CleanupInventoryContractTests.cs` with this content

```csharp
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests;

[TestClass]
[TestCategory("Architecture")]
[TestCategory("Contract")]
public sealed class CleanupInventoryContractTests
{
    private const string CleanupBase =
        "a4138a613dd643abe12858eec5d1c3beb09e95e7";

    private static readonly string Root = FindRepositoryRoot();

    private static readonly string SourcePath = Path.Combine(
        Root,
        "docs",
        "reviews",
        "model-inspection-cleanup-source-files.txt");

    private static readonly string InventoryPath = Path.Combine(
        Root,
        "docs",
        "reviews",
        "model-inspection-cleanup-inventory.md");

    [TestMethod]
    public void CleanupSourceListIsSortedUniqueAndContainsOnlyExistingFiles()
    {
        string[] sourceFiles = ReadSourceFiles();
        string[] sortedUnique = sourceFiles
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            sortedUnique,
            sourceFiles,
            "The cleanup source list must remain sorted and duplicate-free.");

        string[] missingFiles = sourceFiles
            .Where(path => !File.Exists(Path.Combine(Root, path)))
            .ToArray();

        Assert.AreEqual(
            0,
            missingFiles.Length,
            $"The cleanup source list contains missing files:{Environment.NewLine}{string.Join(Environment.NewLine, missingFiles)}");
    }

    [TestMethod]
    public void CleanupInventoryContainsEverySourceFileExactlyOnce()
    {
        string[] sourceFiles = ReadSourceFiles();
        string[] inventoryFiles = ReadInventoryFiles();

        CollectionAssert.AreEqual(
            sourceFiles,
            inventoryFiles,
            "The review inventory must contain one ordered row for every source file.");
    }

    [TestMethod]
    public void NewOrMovedModelInspectionFilesAreAddedToTheInventory()
    {
        string[] inventoryFiles = ReadInventoryFiles();
        string[] changedFiles = RunGit(
                "diff",
                "--name-only",
                "--diff-filter=ACMR",
                $"{CleanupBase}...HEAD")
            .Where(IsInCleanupScope)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        string[] missingRows = changedFiles
            .Except(inventoryFiles, StringComparer.Ordinal)
            .ToArray();

        Assert.AreEqual(
            0,
            missingRows.Length,
            $"New or moved Model Inspection files need inventory rows:{Environment.NewLine}{string.Join(Environment.NewLine, missingRows)}");
    }

    private static string[] ReadSourceFiles() =>
        File.ReadAllLines(SourcePath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(Normalize)
            .ToArray();

    private static string[] ReadInventoryFiles() =>
        File.ReadLines(InventoryPath)
            .Where(line => line.StartsWith("| `", StringComparison.Ordinal))
            .Select(line => line.Split('`')[1])
            .Select(Normalize)
            .ToArray();

    private static bool IsInCleanupScope(string path)
    {
        string normalized = Normalize(path);

        string[] prefixes =
        [
            "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/",
            "shared/GraniteEdgeAI.ModelInspection.Contracts/",
            "shared/GraniteEdgeAI.ModelInspection.Transport/",
            "infrastructure/GraniteEdgeAI.ModelInspection.WorkerClient/",
            "workers/GraniteEdgeAI.ModelInspection.Worker/",
            "tools/ModelInspection.LlamaSharpSpike",
            "tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/",
            "tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests/",
            "tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/",
            "tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/",
            "tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/",
            "tests/ProcessFixtures/GraniteEdgeAI.ModelInspection.ProtocolTestWorker/"
        ];

        if (prefixes.Any(prefix =>
            normalized.StartsWith(prefix, StringComparison.Ordinal)))
        {
            return true;
        }

        if (normalized.Equals(
                ".github/workflows/build-and-test.yml",
                StringComparison.Ordinal) ||
            normalized.StartsWith(
                ".github/workflows/model-inspection-",
                StringComparison.Ordinal) ||
            normalized.StartsWith(
                ".github/workflows/llamasharp-",
                StringComparison.Ordinal))
        {
            return true;
        }

        if (normalized.StartsWith("docs/", StringComparison.Ordinal) &&
            (normalized.Contains(
                "model-inspection",
                StringComparison.OrdinalIgnoreCase) ||
             normalized.Contains(
                "llamasharp",
                StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (normalized.StartsWith(
                "IBM Granite with TurboQuant (Intel)/Features/ModelImport/",
                StringComparison.Ordinal) ||
            normalized.StartsWith(
                "IBM Granite with TurboQuant (Intel)/Features/Onboarding/",
                StringComparison.Ordinal) ||
            normalized.StartsWith(
                "tests/UnitTests/GraniteEdgeAI.UnitTests/",
                StringComparison.Ordinal))
        {
            string fullPath = Path.Combine(Root, normalized);
            return File.Exists(fullPath) &&
                   File.ReadAllText(fullPath).Contains(
                       "ModelInspection",
                       StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static string[] RunGit(params string[] arguments)
    {
        ProcessStartInfo startInfo = new("git")
        {
            WorkingDirectory = Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Git did not start.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.AreEqual(
            0,
            process.ExitCode,
            $"Git failed while checking the cleanup inventory: {error}");

        return output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(Normalize)
            .ToArray();
    }

    private static string Normalize(string path) =>
        path.Replace('\\', '/');

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "global.json")) &&
                File.Exists(Path.Combine(
                    directory.FullName,
                    "IBM Granite with TurboQuant (Intel).slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the repository root for cleanup inventory tests.");
    }
}
```

- [ ] **Step 2: Run the new tests**

```powershell
dotnet test "tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" `
  --configuration Release `
  --no-restore `
  --filter "FullyQualifiedName~CleanupInventoryContractTests" `
  --minimum-expected-tests 3
```

Expected: three tests pass

If the repository-root search fails because the test output path does not have the checkout as an ancestor, use the existing multi-start-path search from `BuildWorkflowContractTests` while preserving the same two root markers

- [ ] **Step 3: Add the developer verification wrapper**

Create `scripts/model-inspection/Verify-ModelInspectionCleanupInventory.ps1`

```powershell
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$project = Join-Path $repositoryRoot 'tests\ContractTests\GraniteEdgeAI.ModelInspection.Contracts.Tests\GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj'

Push-Location $repositoryRoot
try {
    dotnet test $project `
        --configuration Release `
        --filter 'FullyQualifiedName~CleanupInventoryContractTests' `
        --minimum-expected-tests 3

    if ($LASTEXITCODE -ne 0) {
        throw "Model Inspection cleanup inventory verification failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}
```

Use the approved simple comment style if a comment becomes necessary. The script should not contain explanatory comments for obvious commands

- [ ] **Step 4: Run the wrapper from the repository root**

```powershell
& .\scripts\model-inspection\Verify-ModelInspectionCleanupInventory.ps1
```

Expected: three tests pass

- [ ] **Step 5: Run the complete contract project again**

The new tests raise the minimum discovered count. Keep the workflow floor at `75` because it remains a lower bound, then record the exact new count from this run

```powershell
dotnet test "tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" `
  --configuration Release `
  --no-restore `
  --minimum-expected-tests 78
```

Expected: at least 78 tests run and all pass

- [ ] **Step 6: Commit the inventory system**

```powershell
git add docs/reviews `
  scripts/model-inspection `
  tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/CleanupInventoryContractTests.cs

git commit -m "test(model-inspection): establish complete cleanup inventory"
```

---

### Task 8: Record Exact Baseline Evidence and Open the Draft Cleanup PR

**Files:**
- Create: `docs/testing/evidence/2026-08-06-model-inspection-cleanup-baseline.md`
- Update: `docs/testing/evidence/README.md`
- Create: draft PR from `refactor/model-inspection-cleanup` to `feature/model-inspection-worker-host`

**Interfaces:**
- Consumes: baseline run metadata, artifact metadata, TRX counters, source count and inventory count
- Produces: immutable baseline record and reviewable stacked draft PR

- [ ] **Step 1: Create the evidence document from captured variables**

Use the captured values from Tasks 4 and 5

```powershell
$sourceCount = (Get-Content "docs/reviews/model-inspection-cleanup-source-files.txt").Count
$inventoryCount = (Get-Content "docs/reviews/model-inspection-cleanup-inventory.md" | Where-Object { $_ -match '^\| `[^`]+` \|' }).Count
$sdkVersion = dotnet --version
$testTable = ($trxResults | Sort-Object File | ForEach-Object {
  "| $($_.File) | $($_.Total) | $($_.Passed) | $($_.Failed) | $($_.NotExecuted) |"
}) -join "`n"

$evidence = @"
# Model Inspection Cleanup Baseline Evidence

**Date:** 2026-08-06  
**Branch:** ``refactor/model-inspection-cleanup``  
**Commit:** ``$baselineHead``  
**Workflow:** ``Build and test``  
**Run ID:** ``$runId``  
**Job ID:** ``$jobId``  
**Run attempt:** ``$($runJson.attempt)``  
**Conclusion:** ``$($runJson.conclusion)``  
**Selected SDK:** ``$sdkVersion``  

## Purpose

This run establishes the unchanged executable baseline before structural Model Inspection cleanup begins

## Test results

| TRX | Total | Passed | Failed | Not executed |
|---|---:|---:|---:|---:|
$testTable

## Process and privacy gates

- orphan-process check: passed
- retained-evidence privacy scan: passed
- production worker and abnormal fixture publish roots: separate
- zero-test protection: passed

## Artifacts

### Gate 2 verification

- name: ``$($gate2Artifact.name)``
- ID: ``$($gate2Artifact.id)``
- size: ``$($gate2Artifact.size_in_bytes)`` bytes
- server digest: ``$($gate2Artifact.digest)``
- deterministic content digest: ``sha256:$gate2ContentDigest``

### Packaged unit tests

- name: ``$($unitArtifact.name)``
- ID: ``$($unitArtifact.id)``
- size: ``$($unitArtifact.size_in_bytes)`` bytes
- server digest: ``$($unitArtifact.digest)``
- deterministic content digest: ``sha256:$unitContentDigest``

## Cleanup inventory baseline

- source files: ``$sourceCount``
- inventory rows: ``$inventoryCount``
- duplicate paths: ``0``
- missing files: ``0``
- initial review state: ``pending review``

## Baseline conclusion

The exact recorded commit passed the complete existing Model Inspection and packaged WinUI verification boundary before structural cleanup

This evidence does not claim that any later cleanup commit has passed until that later commit receives its own verification
"@

[System.IO.File]::WriteAllText(
  (Join-Path $PWD "docs/testing/evidence/2026-08-06-model-inspection-cleanup-baseline.md"),
  $evidence,
  [System.Text.UTF8Encoding]::new($false))
```

- [ ] **Step 2: Add the evidence link to the evidence index**

Add one bullet under the Model Inspection evidence section in `docs/testing/evidence/README.md`

```markdown
- `2026-08-06-model-inspection-cleanup-baseline.md` — exact-head pre-refactoring baseline, retained artifacts, privacy/orphan gates and complete cleanup inventory count
```

- [ ] **Step 3: Run inventory and contract verification after documentation changes**

The new evidence file is in scope and must be added to both the source list and ledger before the tests can pass

Regenerate the source and inventory rows using Task 6 while preserving existing row data for unchanged paths, then run

```powershell
& .\scripts\model-inspection\Verify-ModelInspectionCleanupInventory.ps1

dotnet test "tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" `
  --configuration Release `
  --no-restore `
  --minimum-expected-tests 78
```

Expected: inventory verification and complete contracts pass

- [ ] **Step 4: Commit baseline evidence**

```powershell
git add docs/testing/evidence docs/reviews
git commit -m "docs(model-inspection): record cleanup baseline evidence"
```

- [ ] **Step 5: Push the documentation head and obtain one more full exact-head run**

```powershell
git push origin refactor/model-inspection-cleanup
$phase0Head = git rev-parse HEAD
```

Select and watch the `Build and test` run whose head SHA equals `$phase0Head`. It must succeed. This run proves the checked-in evidence and inventory files do not break the executable baseline

Do not edit the evidence document again merely to embed the second run and create an endless documentation/run loop. Record the final Phase 0 exact-head run in the draft PR body and later final evidence

- [ ] **Step 6: Open the stacked draft PR**

```powershell
$prBody = @"
## Purpose

This draft pull request performs the approved complete professional cleanup of all Model Inspection work delivered through PRs #44, #45 and #47

The cleanup is behaviour-preserving and inventory-driven. It does not implement Gate 3 functionality

## Stacked boundary

- base: ``feature/model-inspection-worker-host``
- head: ``refactor/model-inspection-cleanup``
- cleanup specification: ``docs/superpowers/specs/2026-08-06-model-inspection-cleanup-design.md``
- master plan: ``docs/superpowers/plans/2026-08-06-model-inspection-cleanup-master.md``

## Phase 0 baseline

- exact final Phase 0 head: ``$phase0Head``
- hosted run: record the exact successful run and job IDs here before submitting the command
- complete contract, transport, worker, WorkerClient, process and packaged WinUI suites: passed
- privacy scan: passed
- orphan-process check: passed
- cleanup source files: ``$sourceCount``
- inventory rows: ``$inventoryCount``

## Locked guarantees

- protocol and JSON compatibility remain unchanged
- process containment and handle allowlist remain unchanged
- cancellation and timeout meanings remain unchanged
- path, chat-template, environment and artifact privacy remain unchanged
- production and abnormal test code remain separate

## Planned review phases

1. WinUI, navigation and application contracts
2. shared contracts, protocol and transport
3. WorkerClient and Windows process infrastructure
4. production worker host and abnormal fixture
5. LLamaSharp feasibility implementation and support
6. test architecture
7. workflows, scripts and documentation
8. independent final review and exact-head closure

## Explicit non-claims

This PR does not yet implement the real production LLamaSharp engine, classifier, service, ViewModel, packaging, Hardware Fit, OpenVINO, chat or TurboQuant

## Review focus

Review each phase for behaviour preservation, readability, explicit ownership, security, privacy, deterministic tests and agreement between code and documentation
"@

gh pr create `
  --draft `
  --base feature/model-inspection-worker-host `
  --head refactor/model-inspection-cleanup `
  --title "refactor(model-inspection): complete professional cleanup" `
  --body $prBody
```

Replace the Phase 0 run sentence in `$prBody` with the actual run and job IDs before invoking `gh pr create`

- [ ] **Step 7: Verify the PR boundary**

```powershell
gh pr view --json number,title,isDraft,baseRefName,headRefName,url
```

Expected:

```text
isDraft: true
baseRefName: feature/model-inspection-worker-host
headRefName: refactor/model-inspection-cleanup
```

---

### Task 9: Perform the Phase 0 Review Gate

**Files:**
- Review: complete Phase 0 diff
- Review: `docs/reviews/model-inspection-cleanup-inventory.md`
- Review: `docs/testing/evidence/2026-08-06-model-inspection-cleanup-baseline.md`
- Review: draft cleanup PR body

**Interfaces:**
- Consumes: all Phase 0 outputs
- Produces: decision to begin or block Phase 1 planning

- [ ] **Step 1: Review the complete branch diff against the stacked base**

```powershell
git diff --check
git diff --stat a4138a613dd643abe12858eec5d1c3beb09e95e7...HEAD
git diff a4138a613dd643abe12858eec5d1c3beb09e95e7...HEAD -- `
  .github/workflows/build-and-test.yml `
  tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests `
  scripts/model-inspection `
  docs/reviews `
  docs/testing/evidence
```

Confirm that no production Model Inspection file changed in Phase 0

- [ ] **Step 2: Review inventory completeness manually**

Check:

```text
all PR #44, #45 and #47 current files are represented
all current files beneath included roots are represented
all connected workflows and documentation are represented
no duplicate path exists
all paths exist
risk levels are plausible
no file has been marked reviewed before its subsystem audit
```

- [ ] **Step 3: Run final Phase 0 local checks**

```powershell
& .\scripts\model-inspection\Verify-ModelInspectionCleanupInventory.ps1

dotnet test "tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" `
  --configuration Release `
  --minimum-expected-tests 78

git status --short
```

Expected: tests pass and worktree is clean

- [ ] **Step 4: Confirm Phase 0 acceptance criteria**

```text
[ ] known workflow discovery defect has a red regression and minimal green fix
[ ] complete contract project runs with no category filter
[ ] minimum test floor remains present
[ ] exact-head hosted baseline succeeds
[ ] every retained TRX has positive all-pass counters
[ ] Gate 2 artifact is retained and hashed
[ ] packaged unit-test artifact is retained and hashed
[ ] privacy scan passes
[ ] no worker or fixture process remains
[ ] source list is sorted, unique and contains only existing files
[ ] inventory contains exactly one row per source file
[ ] new or moved in-scope files fail closed when not inventoried
[ ] baseline evidence identifies the exact tested commit
[ ] draft PR is correctly stacked and remains draft
[ ] no production behaviour changed
```

Do not write the Phase 1 implementation plan until every item is satisfied

---

## Phase 0 Acceptance Evidence

The phase is complete only when the following immutable facts are available

```text
workflow-fix red test run
workflow-fix green focused run
complete local contract count
successful exact-head hosted run and job
exact TRX counters for all retained suites
artifact names, IDs, sizes and digests
privacy step success
orphan-process step success
source file count
inventory row count
inventory contract test count
successful documentation-head hosted run
stacked draft PR URL
```

## Engineering Basis

- **Why Programs Fail** — establish a reproducible baseline and preserve the first failing observation before refactoring
- **The Art of Unit Testing** — protect the CI discovery defect with a focused test and retain separate test layers
- **Refactoring** — do not restructure code until behaviour is characterized and green
- **Designing Secure Software** — do not weaken fail-closed test floors, privacy gates or orphan cleanup to obtain a green build
- **Fundamentals of Software Architecture** — use the inventory and architecture tests as executable fitness functions
- **Systems Engineering Principles and Practice** — preserve exact traceability from branch head through run, artifact and acceptance evidence
