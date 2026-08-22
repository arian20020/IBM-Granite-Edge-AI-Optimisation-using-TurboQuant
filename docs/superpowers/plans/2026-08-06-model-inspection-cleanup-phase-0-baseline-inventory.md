# Model Inspection Cleanup Phase 0 Baseline and Inventory Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** repair the unified-workflow test-discovery defect, prove the unchanged Model Inspection implementation on one exact head and establish a complete executable review inventory before structural refactoring begins

**Architecture:** protect the workflow correction with a focused contract test, run the complete hosted Windows campaign and retain its artifacts, then generate a source ledger from PRs #44, #45 and #47 plus every current included root. Contract tests compare the current filesystem scope, source list and human ledger so new or moved files fail closed until inventoried.

**Tech Stack:** C# 12, repository-selected .NET SDK `10.0.301` with `latestPatch` roll-forward, Microsoft Testing Platform, MSTest, PowerShell 7, GitHub Actions `windows-latest`, TRX and SHA-256

## Global Constraints

- work only on `refactor/model-inspection-cleanup`
- preserve the stacked base `feature/model-inspection-worker-host` at `a4138a613dd643abe12858eec5d1c3beb09e95e7`
- Phase 0 changes no production C#, XAML, protocol, process, worker or LLamaSharp behaviour
- remove only the incompatible contract-test category filter during the baseline correction
- retain a minimum expected test floor and raise it when new mandatory contract tests are added
- preserve every Gate 2 build, publish, test, privacy, orphan and artifact step
- retain exact TRX files and artifact metadata from the successful exact head
- build the inventory from the deduplicated existing-file union of PRs #44, #45 and #47 plus every current included root and connected file
- every source path appears exactly once in the review ledger
- current in-scope files missing from the source list or ledger fail an executable contract test
- comments added by this phase use simple English, start with a lower-case letter and do not end with a full stop
- historical evidence keeps its original tested commit and is not rewritten as current evidence
- Phase 1 planning is blocked until every Phase 0 acceptance condition is satisfied

---

### Task 1: Establish the Isolated Workspace

**Files:**
- Read: `docs/superpowers/specs/2026-08-06-model-inspection-cleanup-design.md`
- Read: `docs/superpowers/plans/2026-08-06-model-inspection-cleanup-master.md`
- Read: `.github/workflows/build-and-test.yml`
- Read: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/BuildWorkflowContractTests.cs`

**Interfaces:**
- Consumes: remote branch `refactor/model-inspection-cleanup`
- Produces: clean dedicated worktree on the exact remote head

- [ ] **Step 1: Create the worktree using `superpowers:using-git-worktrees`**

```powershell
git fetch origin
git worktree add ..\model-inspection-cleanup refactor/model-inspection-cleanup
Set-Location ..\model-inspection-cleanup
```

- [ ] **Step 2: Verify identity and cleanliness**

```powershell
git branch --show-current
git rev-parse HEAD
git status --short
```

Expected: branch is `refactor/model-inspection-cleanup` and status has no output

- [ ] **Step 3: Verify the selected SDK**

```powershell
Get-Content global.json
dotnet --version
dotnet --info
```

Expected: SDK `10.0.301` or a later patch selected by `latestPatch`, with Microsoft Testing Platform selected in `global.json`

---

### Task 2: Protect the Contract-Test Discovery Defect

**Files:**
- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/BuildWorkflowContractTests.cs`
- Test: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/BuildWorkflowContractTests.cs`

**Interfaces:**
- Consumes: workflow step `Run Model Inspection contract tests`
- Produces: `BuildWorkflowRunsCompleteContractProjectWithoutCategoryFilter()` and `ExtractWorkflowStep(...)`

- [ ] **Step 1: Add the failing test**

Add after `BuildWorkflowContainsCurrentContractGate`

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

Add before `CountOccurrences`

```csharp
private static string ExtractWorkflowStep(
    string workflow,
    string stepName)
{
    string marker = $"- name: {stepName}";
    int stepStart = workflow.IndexOf(marker, StringComparison.Ordinal);
    Assert.IsTrue(stepStart >= 0, $"Workflow step was not found: {stepName}");

    int nextStep = workflow.IndexOf(
        "\n      - name:",
        stepStart + marker.Length,
        StringComparison.Ordinal);

    return nextStep >= 0
        ? workflow[stepStart..nextStep]
        : workflow[stepStart..];
}
```

- [ ] **Step 2: Restore and run the red test**

```powershell
dotnet restore "tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj"

dotnet test "tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" `
  --configuration Release `
  --no-restore `
  --filter "FullyQualifiedName~BuildWorkflowRunsCompleteContractProjectWithoutCategoryFilter" `
  --minimum-expected-tests 1
```

Expected: one test fails because the step contains `--filter "TestCategory=Contract"`

- [ ] **Step 3: Commit the red test**

```powershell
git add tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/BuildWorkflowContractTests.cs
git commit -m "test(model-inspection): expose contract discovery defect"
```

---

### Task 3: Correct the Unified Workflow

**Files:**
- Modify: `.github/workflows/build-and-test.yml`
- Test: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/BuildWorkflowContractTests.cs`

**Interfaces:**
- Consumes: complete contract test project
- Produces: unfiltered contract execution with a fail-closed floor

- [ ] **Step 1: Remove only the incompatible filter line**

Change

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

Do not alter the logger, results directory, test order, privacy scan, orphan check or artifact uploads

- [ ] **Step 2: Run focused green and full local contracts**

```powershell
dotnet test "tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" `
  --configuration Release `
  --no-restore `
  --filter "FullyQualifiedName~BuildWorkflowRunsCompleteContractProjectWithoutCategoryFilter" `
  --minimum-expected-tests 1

dotnet test "tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" `
  --configuration Release `
  --no-restore `
  --minimum-expected-tests 75
```

Expected: focused test passes and at least 76 contract tests pass with none failed or skipped

- [ ] **Step 3: Review and commit**

```powershell
git diff --check
git diff -- .github/workflows/build-and-test.yml tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/BuildWorkflowContractTests.cs

git add .github/workflows/build-and-test.yml tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/BuildWorkflowContractTests.cs
git commit -m "fix(ci): run complete model inspection contract suite"
```

---

### Task 4: Obtain the Behaviour Baseline on the Exact Head

**Files:**
- Read: `.github/workflows/build-and-test.yml`
- Temporary: `artifacts/model-inspection-cleanup-baseline/`

**Interfaces:**
- Consumes: Task 3 commit
- Produces: successful hosted run, job, TRX counters and artifact metadata for the unchanged implementation

- [ ] **Step 1: Push and capture the exact head**

```powershell
git push origin refactor/model-inspection-cleanup
$baselineHead = git rev-parse HEAD
```

- [ ] **Step 2: Select and watch the exact-head run**

```powershell
$baselineRun = gh run list `
  --workflow build-and-test.yml `
  --branch refactor/model-inspection-cleanup `
  --limit 10 `
  --json databaseId,headSha,status,conclusion,attempt,createdAt `
  | ConvertFrom-Json `
  | Where-Object headSha -eq $baselineHead `
  | Select-Object -First 1

if ($null -eq $baselineRun) {
  throw "No Build and test run was found for $baselineHead"
}

$baselineRunId = [long]$baselineRun.databaseId
gh run watch $baselineRunId --exit-status
```

When the GitHub connector is used, select the run whose `head_sha` exactly equals `$baselineHead` and poll its jobs to completion

- [ ] **Step 3: Apply systematic debugging to any failure**

```powershell
gh run view $baselineRunId --log-failed
```

Use `superpowers:systematic-debugging`, preserve the first failing command, add a focused regression where practical and fix only the proven cause. Never weaken a test floor, privacy gate, orphan check or artifact condition

- [ ] **Step 4: Capture run, job and required-step metadata**

```powershell
$baselineRunJson = gh run view $baselineRunId --json databaseId,headSha,attempt,status,conclusion,createdAt,updatedAt,jobs,url | ConvertFrom-Json
$baselineJob = $baselineRunJson.jobs | Where-Object name -eq "Build WinUI and run unit tests" | Select-Object -First 1

if ($baselineRunJson.headSha -ne $baselineHead -or $baselineRunJson.conclusion -ne "success") {
  throw "The selected run is not a successful exact-head baseline"
}

$baselineJobId = [long]$baselineJob.databaseId

foreach ($stepName in @(
  "Check for orphaned Gate 2 processes"
  "Scan retained evidence for sensitive data"
  "Upload Gate 2 verification results"
  "Upload unit test results")) {
  $step = $baselineJob.steps | Where-Object name -eq $stepName | Select-Object -First 1
  if ($null -eq $step -or $step.conclusion -ne "success") {
    throw "Required baseline step did not succeed: $stepName"
  }
}
```

- [ ] **Step 5: Download and validate both artifacts**

```powershell
$artifactRoot = Join-Path $PWD "artifacts/model-inspection-cleanup-baseline"
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null

$artifactResponse = gh api "repos/arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant/actions/runs/$baselineRunId/artifacts" | ConvertFrom-Json
$gate2Artifact = $artifactResponse.artifacts | Where-Object name -eq "gate2-verification-$baselineRunId-$($baselineRunJson.attempt)" | Select-Object -First 1
$unitArtifact = $artifactResponse.artifacts | Where-Object name -eq "unit-test-results-$baselineRunId-$($baselineRunJson.attempt)" | Select-Object -First 1

if ($null -eq $gate2Artifact -or $null -eq $unitArtifact) {
  throw "Both exact baseline artifacts are required"
}

gh run download $baselineRunId --name $gate2Artifact.name --dir (Join-Path $artifactRoot "gate2")
gh run download $baselineRunId --name $unitArtifact.name --dir (Join-Path $artifactRoot "unit")
```

- [ ] **Step 6: Parse every TRX and reject zero, failed or skipped results**

```powershell
$trxResults = foreach ($trxFile in Get-ChildItem $artifactRoot -Recurse -Filter *.trx) {
  [xml]$trx = Get-Content -LiteralPath $trxFile.FullName -Raw
  $counters = $trx.TestRun.ResultSummary.Counters

  [pscustomobject]@{
    File = $trxFile.Name
    Total = [int]$counters.total
    Passed = [int]$counters.passed
    Failed = [int]$counters.failed
    Error = [int]$counters.error
    Timeout = [int]$counters.timeout
    Aborted = [int]$counters.aborted
    Inconclusive = [int]$counters.inconclusive
    NotExecuted = [int]$counters.notExecuted
  }
}

$badResults = $trxResults | Where-Object {
  $_.Total -le 0 -or
  $_.Passed -ne $_.Total -or
  $_.Failed -ne 0 -or
  $_.Error -ne 0 -or
  $_.Timeout -ne 0 -or
  $_.Aborted -ne 0 -or
  $_.Inconclusive -ne 0 -or
  $_.NotExecuted -ne 0
}

if ($badResults) {
  $badResults | Format-Table -AutoSize
  throw "One or more baseline TRX files contain a failed, skipped or zero-test result"
}
```

Required files

```powershell
$requiredTrx = @(
  "GraniteEdgeAI.ModelInspection.Contracts.Tests.trx"
  "GraniteEdgeAI.ModelInspection.Transport.Tests.trx"
  "GraniteEdgeAI.ModelInspection.Worker.Tests.trx"
  "GraniteEdgeAI.ModelInspection.WorkerClient.Tests.trx"
  "GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.trx"
  "GraniteEdgeAI.UnitTests.trx"
)

foreach ($name in $requiredTrx) {
  if (-not (Get-ChildItem $artifactRoot -Recurse -Filter $name)) {
    throw "Required baseline TRX file is missing: $name"
  }
}
```

- [ ] **Step 7: Compute deterministic local artifact-content digests**

```powershell
function Get-DirectoryDigest([string]$Path) {
  $records = Get-ChildItem -LiteralPath $Path -Recurse -File |
    Sort-Object FullName |
    ForEach-Object {
      $relative = [System.IO.Path]::GetRelativePath($Path, $_.FullName).Replace('\', '/')
      $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
      "$relative`t$hash"
    }

  $bytes = [System.Text.Encoding]::UTF8.GetBytes([string]::Join("`n", $records))
  return [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}

$gate2ContentDigest = Get-DirectoryDigest (Join-Path $artifactRoot "gate2")
$unitContentDigest = Get-DirectoryDigest (Join-Path $artifactRoot "unit")
```

---

### Task 5: Create the Inventory Files and Completeness Tests

**Files:**
- Create: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/CleanupInventoryContractTests.cs`
- Create: `scripts/model-inspection/Verify-ModelInspectionCleanupInventory.ps1`
- Create: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Create: `docs/reviews/model-inspection-cleanup-inventory.md`
- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/BuildWorkflowContractTests.cs`
- Modify: `.github/workflows/build-and-test.yml`

**Interfaces:**
- Consumes: PR file lists and current repository files
- Produces: source list, review ledger, three inventory tests and workflow inputs required to execute them

- [ ] **Step 1: Add the inventory contract test file**

Create `CleanupInventoryContractTests.cs`

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests;

[TestClass]
[TestCategory("Architecture")]
[TestCategory("Contract")]
public sealed class CleanupInventoryContractTests
{
    private static readonly string Root = FindRepositoryRoot();

    private static readonly string[] CompleteRoots =
    [
        "IBM Granite with TurboQuant (Intel)/Features/ModelInspection",
        "shared/GraniteEdgeAI.ModelInspection.Contracts",
        "shared/GraniteEdgeAI.ModelInspection.Transport",
        "infrastructure/GraniteEdgeAI.ModelInspection.WorkerClient",
        "workers/GraniteEdgeAI.ModelInspection.Worker",
        "tools/ModelInspection.LlamaSharpSpike",
        "tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests",
        "tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests",
        "tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests",
        "tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests",
        "tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests",
        "tests/ProcessFixtures/GraniteEdgeAI.ModelInspection.ProtocolTestWorker",
        "scripts/model-inspection"
    ];

    [TestMethod]
    public void CleanupSourceListIsSortedUniqueAndContainsOnlyExistingFiles()
    {
        string[] sourceFiles = ReadSourceFiles();
        string[] sortedUnique = sourceFiles
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(sortedUnique, sourceFiles);

        string[] missing = sourceFiles
            .Where(path => !File.Exists(Path.Combine(Root, path)))
            .ToArray();

        Assert.AreEqual(
            0,
            missing.Length,
            $"The cleanup source list contains missing files:{Environment.NewLine}{string.Join(Environment.NewLine, missing)}");
    }

    [TestMethod]
    public void CleanupInventoryContainsEverySourceFileExactlyOnce()
    {
        CollectionAssert.AreEqual(ReadSourceFiles(), ReadInventoryFiles());
    }

    [TestMethod]
    public void CurrentCleanupScopeIsFullyInventoried()
    {
        string[] sourceFiles = ReadSourceFiles();
        string[] currentFiles = DiscoverCurrentScopeFiles();
        string[] missing = currentFiles
            .Except(sourceFiles, StringComparer.Ordinal)
            .ToArray();

        Assert.AreEqual(
            0,
            missing.Length,
            $"Current Model Inspection files need inventory rows:{Environment.NewLine}{string.Join(Environment.NewLine, missing)}");
    }

    private static string[] ReadSourceFiles() =>
        File.ReadAllLines(Path.Combine(
                Root,
                "docs",
                "reviews",
                "model-inspection-cleanup-source-files.txt"))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(Normalize)
            .ToArray();

    private static string[] ReadInventoryFiles() =>
        File.ReadLines(Path.Combine(
                Root,
                "docs",
                "reviews",
                "model-inspection-cleanup-inventory.md"))
            .Where(line => line.StartsWith("| `", StringComparison.Ordinal))
            .Select(line => line.Split('`')[1])
            .Select(Normalize)
            .ToArray();

    private static string[] DiscoverCurrentScopeFiles()
    {
        HashSet<string> files = new(StringComparer.Ordinal);

        foreach (string root in CompleteRoots)
        {
            string fullRoot = Path.Combine(Root, root);
            if (!Directory.Exists(fullRoot))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(
                         fullRoot,
                         "*",
                         SearchOption.AllDirectories))
            {
                files.Add(ToRelative(file));
            }
        }

        AddMatchingFiles(
            files,
            "IBM Granite with TurboQuant (Intel)/Features/ModelImport",
            contentMustContainModelInspection: true);
        AddMatchingFiles(
            files,
            "IBM Granite with TurboQuant (Intel)/Features/Onboarding",
            contentMustContainModelInspection: true);
        AddMatchingFiles(
            files,
            "tests/UnitTests/GraniteEdgeAI.UnitTests",
            contentMustContainModelInspection: true);

        foreach (string workflow in Directory.EnumerateFiles(
                     Path.Combine(Root, ".github", "workflows"),
                     "*.yml"))
        {
            string name = Path.GetFileName(workflow);
            if (name.Equals("build-and-test.yml", StringComparison.Ordinal) ||
                name.StartsWith("model-inspection-", StringComparison.Ordinal) ||
                name.StartsWith("llamasharp-", StringComparison.Ordinal))
            {
                files.Add(ToRelative(workflow));
            }
        }

        foreach (string document in Directory.EnumerateFiles(
                     Path.Combine(Root, "docs"),
                     "*",
                     SearchOption.AllDirectories))
        {
            string relative = ToRelative(document);
            if (relative.Contains("model-inspection", StringComparison.OrdinalIgnoreCase) ||
                relative.Contains("llamasharp", StringComparison.OrdinalIgnoreCase))
            {
                files.Add(relative);
            }
        }

        foreach (string path in new[]
        {
            "IBM Granite with TurboQuant (Intel).slnx",
            "IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj",
            "IBM Granite with TurboQuant (Intel)/MainWindow.xaml",
            "IBM Granite with TurboQuant (Intel)/MainWindow.xaml.cs",
            "shared/README.md",
            "infrastructure/README.md",
            "workers/README.md",
            "tools/README.md"
        })
        {
            if (File.Exists(Path.Combine(Root, path)))
            {
                files.Add(path);
            }
        }

        return files.OrderBy(path => path, StringComparer.Ordinal).ToArray();
    }

    private static void AddMatchingFiles(
        ISet<string> files,
        string relativeRoot,
        bool contentMustContainModelInspection)
    {
        string fullRoot = Path.Combine(Root, relativeRoot);
        if (!Directory.Exists(fullRoot))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(
                     fullRoot,
                     "*",
                     SearchOption.AllDirectories))
        {
            if (!contentMustContainModelInspection ||
                File.ReadAllText(file).Contains(
                    "ModelInspection",
                    StringComparison.OrdinalIgnoreCase))
            {
                files.Add(ToRelative(file));
            }
        }
    }

    private static string ToRelative(string path) =>
        Normalize(Path.GetRelativePath(Root, path));

    private static string Normalize(string path) =>
        path.Replace('\\', '/');

    private static string FindRepositoryRoot()
    {
        string[] startingPaths =
        [
            AppContext.BaseDirectory,
            Environment.CurrentDirectory,
            Path.GetDirectoryName(
                typeof(CleanupInventoryContractTests).Assembly.Location)
                ?? AppContext.BaseDirectory
        ];

        foreach (string startingPath in startingPaths)
        {
            DirectoryInfo? directory = new(startingPath);
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
        }

        throw new DirectoryNotFoundException(
            "Could not locate the repository root for cleanup inventory tests.");
    }
}
```

- [ ] **Step 2: Add the developer verification wrapper**

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

- [ ] **Step 3: Generate the source list and initial ledger after the new files exist**

Use tracked and untracked files so the new test and script are included before their first commit

```powershell
$repository = "arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant"
$sourcePath = "docs/reviews/model-inspection-cleanup-source-files.txt"
$inventoryPath = "docs/reviews/model-inspection-cleanup-inventory.md"

function Test-ScopePath([string]$Path) {
  $normalized = $Path.Replace('\', '/')
  $prefixes = @(
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
    "scripts/model-inspection/"
  )

  if ($prefixes | Where-Object { $normalized.StartsWith($_, [StringComparison]::Ordinal) }) {
    return $true
  }

  if ($normalized.StartsWith("IBM Granite with TurboQuant (Intel)/Features/ModelImport/", [StringComparison]::Ordinal) -or
      $normalized.StartsWith("IBM Granite with TurboQuant (Intel)/Features/Onboarding/", [StringComparison]::Ordinal) -or
      $normalized.StartsWith("tests/UnitTests/GraniteEdgeAI.UnitTests/", [StringComparison]::Ordinal)) {
    return (Test-Path -LiteralPath $normalized -PathType Leaf) -and
      (Get-Content -LiteralPath $normalized -Raw).Contains("ModelInspection", [StringComparison]::OrdinalIgnoreCase)
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

$currentCandidates = git ls-files --cached --others --exclude-standard
$currentScope = $currentCandidates | Where-Object { Test-ScopePath $_ }

$allFiles = @(
  $prFiles
  $currentScope
  $sourcePath
  $inventoryPath
) |
  ForEach-Object { $_.Replace('\', '/') } |
  Where-Object { $_ -in @($sourcePath, $inventoryPath) -or (Test-Path -LiteralPath $_ -PathType Leaf) } |
  Sort-Object -Unique

New-Item -ItemType Directory -Force -Path (Split-Path $sourcePath) | Out-Null
[System.IO.File]::WriteAllLines(
  (Join-Path $PWD $sourcePath),
  $allFiles,
  [System.Text.UTF8Encoding]::new($false))

function Get-Subsystem([string]$Path) {
  switch -Regex ($Path) {
    '^IBM Granite with TurboQuant \(Intel\)/Features/ModelInspection/' { 'WinUI Model Inspection'; break }
    '^IBM Granite with TurboQuant \(Intel\)/Features/ModelImport/' { 'Model Import handoff'; break }
    '^IBM Granite with TurboQuant \(Intel\)/Features/Onboarding/' { 'Onboarding navigation'; break }
    '^shared/GraniteEdgeAI\.ModelInspection\.Contracts/' { 'worker contracts'; break }
    '^shared/GraniteEdgeAI\.ModelInspection\.Transport/' { 'transport'; break }
    '^infrastructure/GraniteEdgeAI\.ModelInspection\.WorkerClient/' { 'WorkerClient infrastructure'; break }
    '^workers/GraniteEdgeAI\.ModelInspection\.Worker/' { 'production worker'; break }
    '^tools/ModelInspection\.LlamaSharpSpike' { 'LLamaSharp feasibility'; break }
    '^tests/ProcessFixtures/' { 'abnormal process fixture'; break }
    '^tests/IntegrationTests/' { 'process integration tests'; break }
    '^tests/ContractTests/' { 'contract and architecture tests'; break }
    '^tests/UnitTests/' { 'unit tests'; break }
    '^\.github/workflows/' { 'GitHub Actions'; break }
    '^scripts/' { 'verification scripts'; break }
    '^docs/' { 'documentation and evidence'; break }
    default { 'connected project infrastructure' }
  }
}

function Get-Risk([string]$Path) {
  if ($Path -match '/Windows/' -or
      $Path -match 'Native(Methods|Structures|Handle|Backend)' -or
      $Path -match 'Worker(Process|Conversation|Cancellation|Executable|Environment|Handshake|Exit|Failure)' -or
      $Path -match 'Protocol/' -or
      $Path -match '(Privacy|Redactor|Integrity|Hash)') { return 'critical' }

  if ($Path -match '^workers/' -or
      $Path -match '^tools/ModelInspection\.LlamaSharpSpike' -or
      $Path -match '^\.github/workflows/' -or
      $Path -match 'IntegrationTests|ProcessFixtures') { return 'high' }

  if ($Path -match 'Contracts|ModelInspection|ModelImport|Onboarding|UnitTests|ContractTests') { return 'medium' }

  return 'low'
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
  $risk = Get-Risk $file
  "| ``$file`` | $subsystem | serve the recorded subsystem boundary | $risk | pending review | not reviewed | none | baseline behaviour | to be mapped during subsystem audit | baseline pending | none |"
}

[System.IO.File]::WriteAllLines(
  (Join-Path $PWD $inventoryPath),
  @($header + $rows),
  [System.Text.UTF8Encoding]::new($false))
```

- [ ] **Step 4: Add workflow checkout coverage for the inventory inputs**

Add this test to `BuildWorkflowContractTests`

```csharp
[TestMethod]
public void BuildWorkflowChecksOutCleanupInventoryInputs()
{
    string workflow = ReadWorkflow();

    StringAssert.Contains(workflow, "            docs");
    StringAssert.Contains(workflow, "            scripts");
    StringAssert.Contains(workflow, "            tools");
}
```

Add these sparse-checkout roots after `.github`

```yaml
            docs
            scripts
            tools
```

Raise both the workflow and `BuildWorkflowContainsCurrentContractGate` expectation from `--minimum-expected-tests 75` to `--minimum-expected-tests 80`

- [ ] **Step 5: Run focused and complete contract verification**

```powershell
dotnet test "tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" `
  --configuration Release `
  --filter "FullyQualifiedName~CleanupInventoryContractTests|FullyQualifiedName~BuildWorkflowChecksOutCleanupInventoryInputs" `
  --minimum-expected-tests 4

dotnet test "tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" `
  --configuration Release `
  --no-restore `
  --minimum-expected-tests 80
```

Expected: four focused tests and at least 80 complete contract tests pass

- [ ] **Step 6: Add and run the developer wrapper**

```powershell
& .\scripts\model-inspection\Verify-ModelInspectionCleanupInventory.ps1
```

Expected: three inventory tests pass

- [ ] **Step 7: Commit the inventory gate**

```powershell
git add .github/workflows/build-and-test.yml `
  tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests `
  scripts/model-inspection `
  docs/reviews

git commit -m "test(model-inspection): establish complete cleanup inventory"
```

---

### Task 6: Record Baseline Evidence

**Files:**
- Create: `docs/testing/evidence/2026-08-06-model-inspection-cleanup-baseline.md`
- Modify: `docs/testing/evidence/README.md`
- Regenerate: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Regenerate: `docs/reviews/model-inspection-cleanup-inventory.md`

**Interfaces:**
- Consumes: Task 4 run/artifact data and Task 5 inventory counts
- Produces: immutable pre-refactoring baseline record

- [ ] **Step 1: Create the evidence document with captured values**

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
**Behaviour baseline commit:** ``$baselineHead``  
**Workflow:** ``Build and test``  
**Run ID:** ``$baselineRunId``  
**Job ID:** ``$baselineJobId``  
**Run attempt:** ``$($baselineRunJson.attempt)``  
**Conclusion:** ``$($baselineRunJson.conclusion)``  
**Selected SDK:** ``$sdkVersion``  

## Purpose

This run proves the unchanged executable Model Inspection behaviour before structural cleanup begins

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

## Cleanup inventory prepared after the behaviour baseline

- source files: ``$sourceCount``
- inventory rows: ``$inventoryCount``
- duplicate paths: ``0``
- missing paths: ``0``
- initial review state: ``pending review``

## Conclusion

The recorded behaviour-baseline commit passed the complete existing Model Inspection and packaged WinUI verification boundary before structural cleanup

The later Phase 0 documentation head requires its own successful hosted run and is recorded separately in the draft PR
"@

[System.IO.File]::WriteAllText(
  (Join-Path $PWD "docs/testing/evidence/2026-08-06-model-inspection-cleanup-baseline.md"),
  $evidence,
  [System.Text.UTF8Encoding]::new($false))
```

- [ ] **Step 2: Add the evidence index entry**

```markdown
- `2026-08-06-model-inspection-cleanup-baseline.md` — exact pre-refactoring behaviour baseline, retained artifacts, privacy/orphan gates and cleanup inventory counts
```

- [ ] **Step 3: Regenerate the source list and ledger**

Rerun Task 5 Step 3 in full. No subsystem review data exists yet, so full regeneration is correct. The new evidence file and updated evidence index must appear in the source list and ledger

- [ ] **Step 4: Verify and commit evidence**

```powershell
& .\scripts\model-inspection\Verify-ModelInspectionCleanupInventory.ps1

dotnet test "tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" `
  --configuration Release `
  --no-restore `
  --minimum-expected-tests 80

git add docs/testing/evidence docs/reviews
git commit -m "docs(model-inspection): record cleanup baseline evidence"
```

---

### Task 7: Prove the Final Phase 0 Head and Open the Draft PR

**Files:**
- Read: final Phase 0 branch head
- Create: draft PR from `refactor/model-inspection-cleanup` to `feature/model-inspection-worker-host`

**Interfaces:**
- Consumes: Task 6 commit
- Produces: successful final Phase 0 hosted run and correctly stacked draft PR

- [ ] **Step 1: Push and select the exact final Phase 0 run**

```powershell
git push origin refactor/model-inspection-cleanup
$phase0Head = git rev-parse HEAD

$phase0Run = gh run list `
  --workflow build-and-test.yml `
  --branch refactor/model-inspection-cleanup `
  --limit 10 `
  --json databaseId,headSha,status,conclusion,attempt,createdAt `
  | ConvertFrom-Json `
  | Where-Object headSha -eq $phase0Head `
  | Select-Object -First 1

if ($null -eq $phase0Run) {
  throw "No Build and test run was found for $phase0Head"
}

$phase0RunId = [long]$phase0Run.databaseId
gh run watch $phase0RunId --exit-status
$phase0RunJson = gh run view $phase0RunId --json headSha,conclusion,jobs,url | ConvertFrom-Json
$phase0Job = $phase0RunJson.jobs | Where-Object name -eq "Build WinUI and run unit tests" | Select-Object -First 1
$phase0JobId = [long]$phase0Job.databaseId

if ($phase0RunJson.headSha -ne $phase0Head -or $phase0RunJson.conclusion -ne "success") {
  throw "The final Phase 0 head is not fully verified"
}
```

Do not edit the evidence document again merely to insert this run and create a documentation/run loop. Record this final exact-head run in the PR body and later final closure evidence

- [ ] **Step 2: Open the draft PR using the captured IDs**

```powershell
$sourceCount = (Get-Content "docs/reviews/model-inspection-cleanup-source-files.txt").Count
$inventoryCount = (Get-Content "docs/reviews/model-inspection-cleanup-inventory.md" | Where-Object { $_ -match '^\| `[^`]+` \|' }).Count

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

- behaviour baseline head: ``$baselineHead``
- behaviour baseline run/job: ``$baselineRunId`` / ``$baselineJobId``
- final Phase 0 head: ``$phase0Head``
- final Phase 0 run/job: ``$phase0RunId`` / ``$phase0JobId``
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

- [ ] **Step 3: Verify the PR boundary**

```powershell
gh pr view --json number,title,isDraft,baseRefName,headRefName,url
```

Expected: draft is true, base is `feature/model-inspection-worker-host` and head is `refactor/model-inspection-cleanup`

---

### Task 8: Complete the Phase 0 Review Gate

**Files:**
- Review: complete diff from `a4138a613dd643abe12858eec5d1c3beb09e95e7`
- Review: `docs/reviews/model-inspection-cleanup-inventory.md`
- Review: `docs/testing/evidence/2026-08-06-model-inspection-cleanup-baseline.md`
- Review: draft PR body

**Interfaces:**
- Consumes: all Phase 0 outputs
- Produces: explicit permit or block for Phase 1 planning

- [ ] **Step 1: Review the exact Phase 0 diff**

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

Confirm no production Model Inspection file changed

- [ ] **Step 2: Review inventory completeness manually**

Confirm the PR #44/#45/#47 existing-file union, complete roots, connected workflows, scripts and documentation are represented exactly once. Confirm risk values are plausible and no file is marked reviewed before its subsystem audit

- [ ] **Step 3: Run final local gates**

```powershell
& .\scripts\model-inspection\Verify-ModelInspectionCleanupInventory.ps1

dotnet test "tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" `
  --configuration Release `
  --minimum-expected-tests 80

git status --short
```

Expected: all checks pass and the worktree is clean

- [ ] **Step 4: Confirm every acceptance condition**

```text
[ ] discovery defect has an authentic red test and minimal green fix
[ ] complete contract project runs without a category filter
[ ] final workflow floor is at least 80
[ ] behaviour baseline exact head succeeds
[ ] final Phase 0 exact head succeeds
[ ] every required TRX exists and contains only passed tests
[ ] Gate 2 and packaged-test artifacts are retained and hashed
[ ] privacy scan passes
[ ] no worker or fixture process remains
[ ] source list is sorted, unique and contains only existing files
[ ] inventory contains exactly one row per source file
[ ] current in-scope files missing from the ledger fail closed
[ ] evidence distinguishes behaviour baseline from later inventory preparation
[ ] draft PR is correctly stacked and remains draft
[ ] no production behaviour changed
```

Do not write the Phase 1 implementation plan until every item is satisfied

---

## Engineering Basis

- **Why Programs Fail** — establish a reproducible baseline and preserve the first failing observation
- **The Art of Unit Testing** — protect discovery and inventory rules with focused executable tests
- **Refactoring** — do not restructure code before behaviour is characterized and green
- **Designing Secure Software** — preserve fail-closed floors, privacy checks and orphan cleanup
- **Fundamentals of Software Architecture** — use the ledger and architecture tests as fitness functions
- **Systems Engineering Principles and Practice** — preserve traceability from branch head through run, artifact and acceptance evidence
