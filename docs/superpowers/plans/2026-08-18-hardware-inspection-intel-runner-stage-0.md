# Hardware Inspection Intel Runner Stage 0 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a repository-only, GitHub-hosted Stage 0 control plane that validates one approved Hardware Inspection feature commit without configuring, contacting, or executing anything on the UCL Intel laptop.

**Architecture:** Freeze and push the reviewed `feature/hardware-inspection` tip, then implement Stage 0 in a separate integration worktree created from `origin/main`. A manual hosted workflow validates a strict default-branch approval manifest and confirms the remote feature tip by identity only; it contains no self-hosted job and executes no file from the evaluated feature checkout. Future Stages A-D require separate designs/plans and remain blocked by UCL permission and repository-writer trust.

**Tech Stack:** GitHub Actions on `windows-latest`, SHA-pinned `actions/checkout` and `actions/setup-python`, Windows PowerShell 5.1-compatible validation, Python 3 standard-library `unittest`, Git worktrees.

---

## Authority and non-deviation contract

This plan is subordinate to, and must not change:

- `docs/superpowers/specs/2026-08-15-hardware-inspection-production-design.md`;
- `docs/superpowers/plans/2026-08-15-hardware-inspection-gate-1-llmfit-spike.md`;
- `docs/testing/runbooks/Hardware-Inspection-LLM-Fit-Gate-1-Runbook.md` on the evaluated feature branch;
- `docs/superpowers/specs/2026-08-18-hardware-inspection-intel-runner-configuration-design.md`;
- the authoritative Hardware Inspection DOCX referenced by the production design.

Stage 0 must not change product behavior, WinUI, the seven progress stages, outcome semantics, evidence authority/tolerances, diagnostics, privacy rules, candidate commands, Gate 1 disposition, or any Gate 2-9 intention. It must not modify Model Inspection, the existing `.github/workflows/hardware-inspection-llmfit-spike.yml`, the 174-test `Deterministic` category, the three-test `Task8Deterministic` category, the Blocked Gate 1 report, application workflow registers, or evidence indexes.

The hosted Stage 0 workflow, and every action involving the UCL laptop, never:

- registers, labels, starts, stops, or contacts a self-hosted runner;
- downloads, acquires, verifies, or executes LLM Fit;
- invokes restore, build, MSBuild, or tests from the evaluated feature checkout;
- captures hardware or accesses raw Gate 1 evidence;
- changes or inspects network adapters;
- uploads artifacts;
- authorises Stage A.

Tasks 1 and 6 deliberately restore, build, and run the already committed candidate-free deterministic contracts on the authoring computer only. Those read/verification operations do not execute on a GitHub runner or the UCL laptop, do not set operational `GRANITE_LLMFIT_*` variables, and do not acquire or run the real candidate.

## File structure

Create only in the fresh `origin/main` integration worktree:

- `.github/workflows/hardware-inspection-intel-runner-stage0.yml` — hosted-only manual dispatcher and identity preflight.
- `.github/hardware-inspection/llmfit-gate1-approved-source.json` — strict three-field default-branch approval manifest.
- `scripts/hardware-inspection/Validate-HardwareInspectionIntelRunnerStage0.ps1` — fixed-path context, manifest, and source-identity validator.
- `tests/testing/hardware_inspection/test_intel_runner_stage0_contract.py` — twelve dependency-free Stage 0 contracts and runtime mutations.
- `docs/testing/runbooks/Hardware-Inspection-Intel-Runner-Stage-0-Runbook.md` — operator boundary and hosted-only commands.
- `docs/superpowers/specs/2026-08-18-hardware-inspection-intel-runner-configuration-design.md` — unchanged approved design copied from the frozen feature commit.

Modify only in the integration worktree:

- `scripts/README.md` — index the Stage 0 validator and runbook.

Do not create the live approval manifest on `feature/hardware-inspection`; its SHA would be self-referential or stale. The integration branch creates it only after the feature tip is frozen and pushed.

### Task 1: Freeze and verify the Hardware Inspection source

**Files:**
- Verify only: `tools/HardwareInspection.LlmFitSpike.Tests/**`
- Verify only: `tools/HardwareInspection.LlmFitSpike.IntegrationTests/**`
- Do not modify: all production, UI, evidence, runbook, and Model Inspection files

- [ ] **Step 1: Confirm the approved design and plan are the only recent documentation changes**

Run from `C:\hardware-inspection`:

```powershell
git status --short
git branch --show-current
git log -3 --oneline
```

Expected: clean status; branch exactly `feature/hardware-inspection`; the approved runner design and this Stage 0 plan are visible. Stop on any other change.

- [ ] **Step 2: Prove the Hardware Inspection implementation baseline did not move**

```powershell
$designParent = '7744bf7ba86efa1413929c08d0a22eaa60bcaf74^'
$expectedPlanningChanges = @(
    'docs/superpowers/plans/2026-08-18-hardware-inspection-intel-runner-stage-0.md',
    'docs/superpowers/specs/2026-08-18-hardware-inspection-intel-runner-configuration-design.md'
) | Sort-Object
$actualPlanningChanges = @(git diff --name-only $designParent HEAD --) | Sort-Object
if (
    [string]::Join("`n", $actualPlanningChanges) -cne
    [string]::Join("`n", $expectedPlanningChanges)
) {
    throw "Planning changed paths outside the exact two-document allowlist: $($actualPlanningChanges -join ', ')"
}
```

Expected: no output and no exception.

- [ ] **Step 3: Run and strictly validate the unchanged 174 plus three deterministic contracts**

Use one self-contained shell invocation. The TRX files are ephemeral authoring-machine verification inputs and are deleted in `finally`; they are never copied, committed, or uploaded.

```powershell
$deterministic = 'tools/HardwareInspection.LlmFitSpike.Tests/HardwareInspection.LlmFitSpike.Tests.csproj'
$integration = 'tools/HardwareInspection.LlmFitSpike.IntegrationTests/HardwareInspection.LlmFitSpike.IntegrationTests.csproj'
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$resultsRoot = [IO.Path]::GetFullPath((Join-Path $tempRoot ("GraniteEdgeAI-Stage0-Baseline-" + [Guid]::NewGuid().ToString('N'))))
if (
    -not $resultsRoot.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -or
    -not [IO.Path]::GetFileName($resultsRoot).StartsWith('GraniteEdgeAI-Stage0-Baseline-', [StringComparison]::Ordinal)
) { throw 'The owned result directory is outside the OS temporary root.' }

function Assert-ExactPassingTrx {
    param(
        [Parameter(Mandatory = $true)] [string] $Path,
        [Parameter(Mandatory = $true)] [int] $ExpectedCount,
        [Parameter(Mandatory = $true)] [string] $Suite,
        [string[]] $ExpectedNames = @()
    )
    $settings = [System.Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [System.Xml.XmlReader]::Create($Path, $settings)
    $document = [System.Xml.XmlDocument]::new()
    $document.XmlResolver = $null
    try { $document.Load($reader) } finally { $reader.Dispose() }
    $manager = [System.Xml.XmlNamespaceManager]::new($document.NameTable)
    $manager.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
    $results = @($document.SelectNodes('/t:TestRun/t:Results/t:UnitTestResult', $manager))
    if ($results.Count -ne $ExpectedCount) { throw "$Suite result count differs." }
    if (@($results | Where-Object { $_.GetAttribute('outcome') -cne 'Passed' }).Count -ne 0) {
        throw "$Suite contains a non-passing result."
    }
    $names = @($results | ForEach-Object { $_.GetAttribute('testName') })
    $nameSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($name in $names) {
        if (-not $nameSet.Add([string]$name)) { throw "$Suite contains a duplicate test identity." }
    }
    if ($ExpectedNames.Count -ne 0) {
        $expectedSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        foreach ($name in $ExpectedNames) {
            if (-not $expectedSet.Add([string]$name)) { throw "$Suite expected identity is duplicated." }
        }
        if (-not $nameSet.SetEquals($expectedSet)) { throw "$Suite exact identities differ." }
    }
    $counters = $document.SelectSingleNode('/t:TestRun/t:ResultSummary/t:Counters', $manager)
    if (
        $null -eq $counters -or
        [int]$counters.GetAttribute('total') -ne $ExpectedCount -or
        [int]$counters.GetAttribute('executed') -ne $ExpectedCount -or
        [int]$counters.GetAttribute('passed') -ne $ExpectedCount
    ) { throw "$Suite counters differ." }
    foreach ($counterName in @(
        'failed', 'error', 'timeout', 'aborted', 'inconclusive',
        'passedButRunAborted', 'notRunnable', 'notExecuted', 'disconnected',
        'warning', 'completed', 'inProgress', 'pending'
    )) {
        if ([int]$counters.GetAttribute($counterName) -ne 0) {
            throw "$Suite contains a non-passing counter."
        }
    }
}

[IO.Directory]::CreateDirectory($resultsRoot) | Out-Null
try {
    dotnet restore $deterministic --runtime win-x64
    if ($LASTEXITCODE -ne 0) { throw 'Deterministic restore failed.' }
    dotnet build $deterministic --configuration Release --runtime win-x64 --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Deterministic build failed.' }
    dotnet restore $integration --runtime win-x64
    if ($LASTEXITCODE -ne 0) { throw 'Integration restore failed.' }
    dotnet build $integration --configuration Release --runtime win-x64 --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Integration build failed.' }

    dotnet test `
        --project $deterministic `
        --configuration Release `
        --runtime win-x64 `
        --no-restore `
        --no-build `
        --filter 'TestCategory=Deterministic' `
        --minimum-expected-tests 174 `
        --results-directory $resultsRoot `
        --report-trx `
        --report-trx-filename deterministic.trx `
        --no-ansi
    if ($LASTEXITCODE -ne 0) { throw 'Deterministic contracts failed.' }

    dotnet test `
        --project $integration `
        --configuration Release `
        --runtime win-x64 `
        --no-restore `
        --no-build `
        --filter 'TestCategory=Task8Deterministic' `
        --minimum-expected-tests 3 `
        --results-directory $resultsRoot `
        --report-trx `
        --report-trx-filename task8-deterministic.trx `
        --no-ansi
    if ($LASTEXITCODE -ne 0) { throw 'Task 8 deterministic guards failed.' }

    Assert-ExactPassingTrx `
        -Path (Join-Path $resultsRoot 'deterministic.trx') `
        -ExpectedCount 174 `
        -Suite 'HardwareInspection deterministic'
    Assert-ExactPassingTrx `
        -Path (Join-Path $resultsRoot 'task8-deterministic.trx') `
        -ExpectedCount 3 `
        -Suite 'HardwareInspection Task8Deterministic' `
        -ExpectedNames @(
            'ArtifactStringShape_RejectsPathsAndFreeTextWithGenericDiagnostics',
            'CaptureInterval_ThirtySecondsPlusOneTickIsOutsideBoundary',
            'StableFileIdentityAndProcessTreeCleanup_AreFailClosed'
        )
}
finally {
    if (
        [IO.Directory]::Exists($resultsRoot) -and
        $resultsRoot.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($resultsRoot).StartsWith('GraniteEdgeAI-Stage0-Baseline-', [StringComparison]::Ordinal)
    ) {
        Remove-Item -LiteralPath $resultsRoot -Recurse -Force
    }
}
```

Expected: exactly 174 deterministic results and the exact three named Task 8 guards, every result `Passed`, every non-passing counter zero, and no raw TRX left behind. No real candidate is downloaded or executed.

- [ ] **Step 4: Record and freeze the evaluated source SHA**

```powershell
$frozenFeatureSha = (git rev-parse HEAD).Trim()
if ($frozenFeatureSha -cnotmatch '\A[0-9a-f]{40}\z') {
    throw 'The feature SHA is not canonical lowercase hexadecimal.'
}
$frozenFeatureSha
```

Expected: one lowercase 40-character SHA. Record it for the integration manifest. From this point until Stage 0 delivery finishes, no commit may be added to the remote `feature/hardware-inspection` tip.

- [ ] **Step 5: Publish only the frozen feature branch and verify remote identity**

```powershell
$frozenFeatureSha = (git rev-parse HEAD).Trim()
if ($frozenFeatureSha -cnotmatch '\A[0-9a-f]{40}\z') {
    throw 'The feature SHA is not canonical lowercase hexadecimal.'
}
git push --set-upstream origin feature/hardware-inspection
if ($LASTEXITCODE -ne 0) { throw 'Feature branch push failed.' }
$remoteLine = @(git ls-remote --heads origin refs/heads/feature/hardware-inspection)
if ($LASTEXITCODE -ne 0 -or $remoteLine.Count -ne 1) {
    throw 'The remote feature ref is missing or ambiguous.'
}
$remoteSha = ($remoteLine[0] -split '\s+')[0]
if ($remoteSha -cne $frozenFeatureSha) {
    throw 'The remote feature tip differs from the reviewed local tip.'
}
```

Expected: remote and local frozen SHA match exactly. This is the only feature-branch external write in Stage 0.

### Task 2: Create the isolated default-branch integration worktree

**Files:**
- Create worktree: `C:\hardware-inspection-stage0`
- Create branch: `integration/hardware-inspection-intel-runner-stage0`

- [ ] **Step 1: Use the worktree isolation workflow**

Before executing this task, invoke `superpowers:using-git-worktrees` and follow its safety checks.

- [ ] **Step 2: Refresh and verify the default-branch base**

```powershell
git fetch origin main
if ($LASTEXITCODE -ne 0) { throw 'Fetching origin/main failed.' }
$mainSha = (git rev-parse origin/main).Trim()
if ($mainSha -cnotmatch '\A[0-9a-f]{40}\z') {
    throw 'origin/main did not resolve to one commit.'
}
```

Expected: one canonical default-branch SHA.

- [ ] **Step 3: Create the clean integration worktree**

```powershell
$integrationRoot = 'C:\hardware-inspection-stage0'
if (Test-Path -LiteralPath $integrationRoot) {
    throw 'The intended integration worktree path already exists.'
}
git worktree add `
    -b integration/hardware-inspection-intel-runner-stage0 `
    $integrationRoot `
    origin/main
if ($LASTEXITCODE -ne 0) { throw 'Creating the integration worktree failed.' }
git -C $integrationRoot status --short
git -C $integrationRoot rev-parse HEAD
```

Expected: clean worktree at the recorded `origin/main` SHA.

- [ ] **Step 4: Confirm the integration branch contains no Hardware Inspection runtime**

```powershell
$integrationRoot = 'C:\hardware-inspection-stage0'
$forbidden = @(
    'tools/HardwareInspection.LlmFitSpike',
    'third-party/bin/llmfit',
    'artifacts/hardware-inspection/llmfit'
)
foreach ($relative in $forbidden) {
    if (Test-Path -LiteralPath (Join-Path $integrationRoot $relative)) {
        throw "Unexpected runtime path exists on the integration base: $relative"
    }
}
```

Expected: no exception.

### Task 3: Lock the approval manifest and hosted validator with runtime tests

**Files:**
- Create: `.github/hardware-inspection/llmfit-gate1-approved-source.json`
- Create: `scripts/hardware-inspection/Validate-HardwareInspectionIntelRunnerStage0.ps1`
- Create: `tests/testing/hardware_inspection/test_intel_runner_stage0_contract.py`

- [ ] **Step 1: Write the first three failing contracts**

Create `tests/testing/hardware_inspection/test_intel_runner_stage0_contract.py` with the imports, paths, helpers, and first three test identities below. Use only the Python standard library.

```python
from __future__ import annotations

import hashlib
import json
import os
import re
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
MANIFEST_PATH = (
    REPOSITORY_ROOT
    / ".github/hardware-inspection/llmfit-gate1-approved-source.json"
)
VALIDATOR_PATH = (
    REPOSITORY_ROOT
    / "scripts/hardware-inspection/Validate-HardwareInspectionIntelRunnerStage0.ps1"
)
WORKFLOW_PATH = (
    REPOSITORY_ROOT
    / ".github/workflows/hardware-inspection-intel-runner-stage0.yml"
)
DESIGN_PATH = (
    REPOSITORY_ROOT
    / "docs/superpowers/specs/2026-08-18-hardware-inspection-intel-runner-configuration-design.md"
)
RUNBOOK_PATH = (
    REPOSITORY_ROOT
    / "docs/testing/runbooks/Hardware-Inspection-Intel-Runner-Stage-0-Runbook.md"
)
EXPECTED_REF = "refs/heads/feature/hardware-inspection"
EXPECTED_DESIGN_SHA256 = (
    "44916a51c7d4e1856b73564c8ce23e9b4ee60b20067c67a2edeff8c482e13cfe"
)
EXPECTED_WORKFLOW_SHA256 = (
    "9f14750368eef1a105332ccb54cd513ca92fd4af91dd8542efe5ededff304309"
)


def _strict_object(text: str) -> dict[str, object]:
    def reject_duplicates(pairs: list[tuple[str, object]]) -> dict[str, object]:
        result: dict[str, object] = {}
        for key, value in pairs:
            if key in result:
                raise ValueError("duplicate JSON member")
            result[key] = value
        return result

    value = json.loads(text, object_pairs_hook=reject_duplicates)
    if not isinstance(value, dict):
        raise ValueError("root must be an object")
    return value


class IntelRunnerStage0ContractTests(unittest.TestCase):
    maxDiff = None

    def _powershell(self) -> str:
        executable = shutil.which("powershell.exe") or shutil.which("powershell")
        self.assertIsNotNone(executable, "Windows PowerShell 5.1 is required")
        return str(executable)

    def _write_manifest(self, root: Path, sha: str, *, raw: str | None = None) -> None:
        path = root / ".github/hardware-inspection/llmfit-gate1-approved-source.json"
        path.parent.mkdir(parents=True, exist_ok=True)
        text = raw or (
            "{\n"
            '  "schemaVersion": "1.0",\n'
            f'  "remoteFeatureRef": "{EXPECTED_REF}",\n'
            f'  "approvedTipSha": "{sha}"\n'
            "}\n"
        )
        path.write_text(text, encoding="utf-8", newline="\n")

    def _make_source_repository(self, root: Path) -> tuple[Path, str]:
        source = root / "evaluated"
        source.mkdir()
        subprocess.run(["git", "init", "-q", str(source)], check=True)
        (source / "identity.txt").write_text("identity only\n", encoding="utf-8")
        subprocess.run(["git", "-C", str(source), "add", "identity.txt"], check=True)
        subprocess.run(
            [
                "git", "-C", str(source),
                "-c", "user.name=Stage0 Contract",
                "-c", "user.email=stage0.invalid@example.invalid",
                "commit", "-q", "-m", "stage0 identity fixture",
            ],
            check=True,
        )
        sha = subprocess.check_output(
            ["git", "-C", str(source), "rev-parse", "HEAD"],
            text=True,
            encoding="utf-8",
        ).strip()
        return source, sha

    def _run_validator(
        self,
        control: Path,
        phase: str,
        *,
        source: Path | None = None,
        actor: str = "arian20020",
        triggering_actor: str = "arian20020",
        workflow_ref: str = "refs/heads/main",
        default_branch: str = "main",
        attempt: str = "1",
        confirmation: str = "true",
    ) -> subprocess.CompletedProcess[str]:
        output = control / "stage0-output.txt"
        summary = control / "stage0-summary.md"
        command = [
            self._powershell(), "-NoLogo", "-NoProfile", "-NonInteractive",
            "-ExecutionPolicy", "Bypass", "-File", str(VALIDATOR_PATH),
            "-Phase", phase,
            "-ControlRoot", str(control),
            "-WorkflowRef", workflow_ref,
            "-DefaultBranch", default_branch,
            "-Actor", actor,
            "-TriggeringActor", triggering_actor,
            "-RepositoryOwner", "arian20020",
            "-RunAttempt", attempt,
            "-ConfirmRepositoryOnly", confirmation,
            "-GitHubOutputPath", str(output),
            "-SummaryPath", str(summary),
        ]
        if source is not None:
            command.extend(["-SourceCheckoutRoot", str(source)])
        return subprocess.run(
            command,
            cwd=REPOSITORY_ROOT,
            text=True,
            encoding="utf-8",
            errors="strict",
            capture_output=True,
            timeout=20,
            check=False,
        )

    def test_approval_manifest_accepts_only_exact_three_property_schema(self) -> None:
        raw = MANIFEST_PATH.read_bytes()
        self.assertLessEqual(len(raw), 4096)
        self.assertFalse(raw.startswith(b"\xef\xbb\xbf"))
        value = _strict_object(raw.decode("utf-8", errors="strict"))
        self.assertEqual(
            ["schemaVersion", "remoteFeatureRef", "approvedTipSha"],
            list(value),
        )
        self.assertEqual("1.0", value["schemaVersion"])
        self.assertEqual(EXPECTED_REF, value["remoteFeatureRef"])
        self.assertRegex(str(value["approvedTipSha"]), r"\A[0-9a-f]{40}\Z")
        self.assertNotEqual("0" * 40, value["approvedTipSha"])

    def test_approval_manifest_rejects_unsafe_ref_sha_and_dispatch_context(self) -> None:
        valid_sha = "1" * 40
        mutations = (
            '{"schemaVersion":"1.0","remoteFeatureRef":"refs/heads/main",'
            f'"approvedTipSha":"{valid_sha}"}}',
            '{"schemaVersion":"1.0","remoteFeatureRef":"refs/heads/feature/hardware-inspection",'
            '"approvedTipSha":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"}',
            '{"schemaVersion":"1.0","remoteFeatureRef":"refs/heads/feature/hardware-inspection",'
            '"approvedTipSha":"0000000000000000000000000000000000000000"}',
            '{"schemaVersion":"1.0","remoteFeatureRef":"refs/heads/feature/hardware-inspection",'
            f'"approvedTipSha":"{valid_sha}","computerName":"forbidden"}}',
            '{"schemaVersion":"1.0","schemaVersion":"1.0",'
            '"remoteFeatureRef":"refs/heads/feature/hardware-inspection",'
            f'"approvedTipSha":"{valid_sha}"}}',
        )
        with tempfile.TemporaryDirectory() as temporary:
            control = Path(temporary)
            for raw in mutations:
                with self.subTest(raw=raw):
                    self._write_manifest(control, valid_sha, raw=raw)
                    result = self._run_validator(control, "Dispatch")
                    self.assertNotEqual(0, result.returncode)
                    self.assertEqual(
                        "HI-RUNNER-STAGE0-INVALID: repository-only validation failed.\n",
                        result.stderr.replace("\r\n", "\n"),
                    )
            self._write_manifest(control, valid_sha)
            bad_contexts = (
                {"actor": "someone-else"},
                {"triggering_actor": "someone-else"},
                {"workflow_ref": "refs/heads/feature/hardware-inspection"},
                {"default_branch": "develop"},
                {"attempt": "2"},
                {"confirmation": "false"},
            )
            for mutation in bad_contexts:
                with self.subTest(mutation=mutation):
                    result = self._run_validator(control, "Dispatch", **mutation)
                    self.assertNotEqual(0, result.returncode)

    def test_stage0_validator_accepts_only_matching_clean_source_identity(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            control = Path(temporary) / "control"
            control.mkdir()
            source, source_sha = self._make_source_repository(Path(temporary))
            self._write_manifest(control, source_sha)

            dispatch = self._run_validator(control, "Dispatch")
            self.assertEqual(0, dispatch.returncode, dispatch.stderr)
            output = (control / "stage0-output.txt").read_text(encoding="utf-8")
            self.assertEqual(
                f"source_ref={EXPECTED_REF}\n"
                f"approved_sha={source_sha}\n"
                "repository_only=true\n",
                output.replace("\r\n", "\n"),
            )

            source_result = self._run_validator(control, "Source", source=source)
            self.assertEqual(0, source_result.returncode, source_result.stderr)
            summary = (control / "stage0-summary.md").read_text(encoding="utf-8")
            self.assertIn("Stage 0 only", summary)
            self.assertIn("Gate 1 remains Blocked", summary)
            self.assertIn("Gate 2 is prohibited", summary)

            (source / "untracked.txt").write_text("dirty\n", encoding="utf-8")
            dirty = self._run_validator(control, "Source", source=source)
            self.assertNotEqual(0, dirty.returncode)
            (source / "untracked.txt").unlink()

            self._write_manifest(control, "2" * 40)
            mismatch = self._run_validator(control, "Source", source=source)
            self.assertNotEqual(0, mismatch.returncode)
```

- [ ] **Step 2: Run the three tests and confirm the intended RED**

```powershell
python -m unittest `
    tests.testing.hardware_inspection.test_intel_runner_stage0_contract.IntelRunnerStage0ContractTests.test_approval_manifest_accepts_only_exact_three_property_schema `
    tests.testing.hardware_inspection.test_intel_runner_stage0_contract.IntelRunnerStage0ContractTests.test_approval_manifest_rejects_unsafe_ref_sha_and_dispatch_context `
    tests.testing.hardware_inspection.test_intel_runner_stage0_contract.IntelRunnerStage0ContractTests.test_stage0_validator_accepts_only_matching_clean_source_identity `
    -v
```

Expected RED: three errors/failures caused only by the absent manifest and validator. A syntax/import/harness error is not an acceptable RED.

- [ ] **Step 3: Create the strict manifest using the frozen feature SHA**

Re-establish the frozen identity in this shell:

```powershell
$featureRoot = 'C:\hardware-inspection'
$frozenFeatureSha = (git -C $featureRoot rev-parse HEAD).Trim()
$remoteLine = @(git -C $featureRoot ls-remote --heads origin refs/heads/feature/hardware-inspection)
if (
    $frozenFeatureSha -cnotmatch '\A[0-9a-f]{40}\z' -or
    $remoteLine.Count -ne 1 -or
    (($remoteLine[0] -split '\s+')[0] -cne $frozenFeatureSha)
) { throw 'The local and remote frozen feature identities differ.' }
$frozenFeatureSha
```

Use `apply_patch` in the integration worktree. The file must contain exactly the three ordered properties `schemaVersion`, `remoteFeatureRef`, and `approvedTipSha`. Write `"1.0"` and `"refs/heads/feature/hardware-inspection"` literally, and write the concrete 40-character value printed by the block above literally as `approvedTipSha`. No token, substitution syntax, descriptive value, or placeholder may reach the file.

Immediately verify the committed-value candidate before continuing:

```powershell
$featureRoot = 'C:\hardware-inspection'
$frozenFeatureSha = (git -C $featureRoot rev-parse HEAD).Trim()
$remoteLine = @(git -C $featureRoot ls-remote --heads origin refs/heads/feature/hardware-inspection)
if (
    $frozenFeatureSha -cnotmatch '\A[0-9a-f]{40}\z' -or
    $remoteLine.Count -ne 1 -or
    (($remoteLine[0] -split '\s+')[0] -cne $frozenFeatureSha)
) { throw 'The local and remote frozen feature identities differ.' }
$manifest = Get-Content `
    -LiteralPath '.github\hardware-inspection\llmfit-gate1-approved-source.json' `
    -Raw | ConvertFrom-Json
if ([string]$manifest.approvedTipSha -cne $frozenFeatureSha) {
    throw 'The approval manifest is not bound to the frozen feature tip.'
}
```

- [ ] **Step 4: Implement the minimal validator**

Create `scripts/hardware-inspection/Validate-HardwareInspectionIntelRunnerStage0.ps1` with these complete behaviors:

```powershell
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Dispatch', 'Source')]
    [string] $Phase,

    [Parameter(Mandatory = $true)] [string] $ControlRoot,
    [Parameter(Mandatory = $true)] [string] $WorkflowRef,
    [Parameter(Mandatory = $true)] [string] $DefaultBranch,
    [Parameter(Mandatory = $true)] [string] $Actor,
    [Parameter(Mandatory = $true)] [string] $TriggeringActor,
    [Parameter(Mandatory = $true)] [string] $RepositoryOwner,
    [Parameter(Mandatory = $true)] [string] $RunAttempt,
    [Parameter(Mandatory = $true)] [string] $ConfirmRepositoryOnly,
    [string] $SourceCheckoutRoot,
    [string] $GitHubOutputPath,
    [string] $SummaryPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Stop-Stage0Validation {
    throw 'HI-RUNNER-STAGE0-INVALID'
}

function Assert-ExactContext {
    if ($DefaultBranch -cne 'main') { Stop-Stage0Validation }
    if ($WorkflowRef -cne 'refs/heads/main') { Stop-Stage0Validation }
    if ($RepositoryOwner -cne 'arian20020') { Stop-Stage0Validation }
    if ($Actor -cne $RepositoryOwner) { Stop-Stage0Validation }
    if ($TriggeringActor -cne $RepositoryOwner) { Stop-Stage0Validation }
    if ($RunAttempt -cne '1') { Stop-Stage0Validation }
    if ($ConfirmRepositoryOnly -cne 'true') { Stop-Stage0Validation }
}

function Read-ApprovalManifest {
    $root = [IO.Path]::GetFullPath($ControlRoot)
    if (-not [IO.Directory]::Exists($root)) { Stop-Stage0Validation }
    $path = [IO.Path]::Combine(
        $root,
        '.github\hardware-inspection\llmfit-gate1-approved-source.json'
    )
    $bytes = [IO.File]::ReadAllBytes($path)
    if ($bytes.Length -eq 0 -or $bytes.Length -gt 4096) { Stop-Stage0Validation }
    if (
        $bytes.Length -ge 3 -and
        $bytes[0] -eq 0xEF -and
        $bytes[1] -eq 0xBB -and
        $bytes[2] -eq 0xBF
    ) { Stop-Stage0Validation }
    $utf8 = New-Object Text.UTF8Encoding($false, $true)
    $text = $utf8.GetString($bytes)
    $pattern = '(?s)\A\s*\{\s*"schemaVersion"\s*:\s*"1\.0"\s*,\s*' +
        '"remoteFeatureRef"\s*:\s*"refs/heads/feature/hardware-inspection"\s*,\s*' +
        '"approvedTipSha"\s*:\s*"(?<sha>[0-9a-f]{40})"\s*\}\s*\z'
    $match = [Text.RegularExpressions.Regex]::Match($text, $pattern)
    if (-not $match.Success) { Stop-Stage0Validation }
    $sha = $match.Groups['sha'].Value
    if ($sha -ceq ('0' * 40)) { Stop-Stage0Validation }
    return [pscustomobject]@{
        SourceRef = 'refs/heads/feature/hardware-inspection'
        Sha = $sha
    }
}

function Write-Utf8NoBomLines {
    param([string] $Path, [string[]] $Lines)
    if ([string]::IsNullOrWhiteSpace($Path)) { Stop-Stage0Validation }
    $encoding = New-Object Text.UTF8Encoding($false)
    [IO.File]::WriteAllText($Path, (($Lines -join "`n") + "`n"), $encoding)
}

try {
    Assert-ExactContext
    $approval = Read-ApprovalManifest

    if ($Phase -ceq 'Dispatch') {
        Write-Utf8NoBomLines -Path $GitHubOutputPath -Lines @(
            "source_ref=$($approval.SourceRef)",
            "approved_sha=$($approval.Sha)",
            'repository_only=true'
        )
        exit 0
    }

    if ([string]::IsNullOrWhiteSpace($SourceCheckoutRoot)) {
        Stop-Stage0Validation
    }
    $sourceRoot = [IO.Path]::GetFullPath($SourceCheckoutRoot)
    if (-not [IO.Directory]::Exists($sourceRoot)) { Stop-Stage0Validation }
    $head = (& git -C $sourceRoot rev-parse HEAD 2>$null | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $head -cne $approval.Sha) {
        Stop-Stage0Validation
    }
    $status = @(& git -C $sourceRoot status --porcelain --untracked-files=all 2>$null)
    if ($LASTEXITCODE -ne 0 -or $status.Count -ne 0) {
        Stop-Stage0Validation
    }
    Write-Utf8NoBomLines -Path $SummaryPath -Lines @(
        '# Hardware Inspection Intel runner preflight',
        '',
        '- Stage 0 only.',
        '- The Intel laptop was not contacted.',
        '- The LLM Fit candidate was not acquired or executed.',
        '- Gate 1 remains Blocked.',
        '- Gate 2 is prohibited.',
        "- Approved source ref: $($approval.SourceRef)",
        "- Approved source SHA: $($approval.Sha)"
    )
    exit 0
}
catch {
    [Console]::Error.WriteLine(
        'HI-RUNNER-STAGE0-INVALID: repository-only validation failed.'
    )
    exit 1
}
```

Do not emit exception messages, raw paths, actors, host data, or manifest contents. The validator's only dynamic outputs are the already constrained ref and SHA.

- [ ] **Step 5: Run the focused GREEN contracts**

Run the exact command from Step 2 again.

Expected GREEN: three tests passed, zero failed, zero skipped. Confirm each malicious mutation returns the one fixed diagnostic and the valid source summary makes only Stage 0 non-claims.

- [ ] **Step 6: Commit the contract foundation**

```powershell
git add -- `
    .github/hardware-inspection/llmfit-gate1-approved-source.json `
    scripts/hardware-inspection/Validate-HardwareInspectionIntelRunnerStage0.ps1 `
    tests/testing/hardware_inspection/test_intel_runner_stage0_contract.py
git diff --cached --check
git commit -m "test(hardware-inspection): lock runner Stage 0 approval"
```

Expected: exactly the three listed files in the commit.

### Task 4: Add the hosted-only manual Stage 0 workflow

**Files:**
- Modify: `tests/testing/hardware_inspection/test_intel_runner_stage0_contract.py`
- Create: `.github/workflows/hardware-inspection-intel-runner-stage0.yml`

- [ ] **Step 1: Add eight failing workflow contracts**

Append these exact test identities to `IntelRunnerStage0ContractTests`. Each test reads `WORKFLOW_PATH` as UTF-8 and enforces executable YAML text, not comments:

```python
    def _workflow(self) -> str:
        raw = WORKFLOW_PATH.read_bytes()
        self.assertFalse(raw.startswith(b"\xef\xbb\xbf"))
        self.assertNotIn(b"\r\n", raw)
        return raw.decode("utf-8", errors="strict")

    def _assert_canonical_workflow(self, workflow: str) -> None:
        self.assertEqual(
            EXPECTED_WORKFLOW_SHA256,
            hashlib.sha256(workflow.encode("utf-8")).hexdigest(),
        )

    def test_stage0_workflow_exposes_only_manual_trigger_and_hosted_runner(self) -> None:
        workflow = self._workflow()
        self._assert_canonical_workflow(workflow)
        self.assertRegex(workflow, r"(?m)^on:\s*$")
        self.assertEqual(1, workflow.count("workflow_dispatch:"))
        for trigger in (
            "push:", "pull_request:", "pull_request_target:", "schedule:",
            "repository_dispatch:", "workflow_call:", "workflow_run:",
        ):
            self.assertNotIn(trigger, workflow)
        self.assertEqual(1, workflow.count("runs-on: windows-latest"))
        self.assertEqual(1, workflow.count("runs-on:"))
        self.assertNotIn("self-hosted", workflow.lower())
        jobs = workflow.split("\njobs:\n", 1)[1]
        self.assertEqual(
            ["hosted-preflight"],
            re.findall(r"(?m)^  ([a-z0-9-]+):\s*$", jobs),
        )
        mutations = (
            "# runs-on: windows-latest\n" + workflow,
            workflow.replace("runs-on: windows-latest", "'runs-on': windows-latest", 1),
            workflow.replace("working-directory: control", "working-directory: evaluated", 1),
            workflow.replace(
                "    steps:\n",
                "    steps:\n      - name: Indirect evaluated execution\n"
                "        shell: pwsh\n        run: & '.\\evaluated\\script.ps1'\n",
                1,
            ),
            workflow.replace(
                "    steps:\n",
                "    steps:\n      - uses: actions/checkout@"
                "9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0\n",
                1,
            ),
            workflow + (
                "  contact-runner:\n"
                "    runs-on: ${{ inputs.runner_label }}\n"
                "    steps:\n"
                "      - shell: powershell\n"
                "        run: Get-ComputerInfo\n"
            ),
            workflow.replace(
                "    if: >-\n",
                "    # github.actor == github.repository_owner\n"
                "    # github.triggering_actor == github.repository_owner\n"
                "    # github.run_attempt == 1\n"
                "    if: true\n    ignored-guard: >-\n",
                1,
            ),
            workflow.replace(
                "          ref: ${{ github.sha }}",
                "          ref: refs/heads/feature/hardware-inspection",
                1,
            ),
            workflow.replace(
                "  workflow_dispatch:\n",
                "  workflow_dispatch:\n  pull_request_target:\n  workflow_run:\n",
                1,
            ),
        )
        for mutation in mutations:
            with self.subTest(mutation=hashlib.sha256(mutation.encode()).hexdigest()):
                with self.assertRaises(AssertionError):
                    self._assert_canonical_workflow(mutation)

    def test_stage0_workflow_uses_read_only_permissions_owner_and_default_guards(self) -> None:
        workflow = self._workflow()
        self.assertRegex(workflow, r"(?ms)^permissions:\s*\n  contents: read\s*$")
        permission_block = workflow.split("permissions:\n", 1)[1].split("\n\n", 1)[0]
        self.assertEqual("  contents: read", permission_block.strip("\n"))
        self.assertIn("github.actor == github.repository_owner", workflow)
        self.assertIn("github.triggering_actor == github.repository_owner", workflow)
        self.assertIn("github.run_attempt == 1", workflow)
        self.assertIn("github.event.repository.default_branch", workflow)
        self.assertIn("inputs.confirm_repository_only == true", workflow)
        expected_guard = (
            "    if: >-\n"
            "      github.event_name == 'workflow_dispatch' &&\n"
            "      github.ref == format('refs/heads/{0}', "
            "github.event.repository.default_branch) &&\n"
            "      github.actor == github.repository_owner &&\n"
            "      github.triggering_actor == github.repository_owner &&\n"
            "      github.run_attempt == 1 &&\n"
            "      inputs.confirm_repository_only == true\n"
        )
        self.assertIn(expected_guard, workflow)
        self.assertIn("timeout-minutes: 10", workflow)
        self.assertIn("cancel-in-progress: false", workflow)

    def test_stage0_workflow_pins_actions_and_drops_checkout_credentials(self) -> None:
        workflow = self._workflow()
        uses = [line.strip() for line in workflow.splitlines() if "uses:" in line]
        self.assertEqual(
            [
                "uses: actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0",
                "uses: actions/setup-python@a26af69be951a213d495a4c3e4e4022e16d87065",
                "uses: actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0",
            ],
            uses,
        )
        for line in uses:
            self.assertRegex(line, r"uses:\s+actions/[A-Za-z0-9_-]+@[0-9a-f]{40}\Z")
        self.assertIn("python-version: '3.12.10'", workflow)
        self.assertEqual(2, workflow.count("persist-credentials: false"))
        self.assertEqual(1, workflow.count("ref: ${{ github.sha }}"))
        self.assertNotRegex(workflow, r"uses:\s+[^\s]+@(main|master|v\d+)(?:\s|$)")

    def test_stage0_workflow_reads_approved_source_without_free_form_sha_input(self) -> None:
        workflow = self._workflow()
        self.assertIn("confirm_repository_only:", workflow)
        inputs = workflow.split("    inputs:\n", 1)[1].split("\npermissions:", 1)[0]
        self.assertEqual(
            ["confirm_repository_only"],
            re.findall(r"(?m)^      ([a-z0-9_]+):\s*$", inputs),
        )
        self.assertNotIn("evaluated_sha:", workflow)
        self.assertNotIn("source_ref:", workflow.split("steps:", 1)[0])
        self.assertIn("steps.approval.outputs.source_ref", workflow)

    def test_stage0_workflow_executes_validators_only_from_control_checkout(self) -> None:
        workflow = self._workflow()
        self.assertIn("path: control", workflow)
        self.assertIn("path: evaluated", workflow)
        self.assertGreaterEqual(
            workflow.count("control\\scripts\\hardware-inspection\\"
                           "Validate-HardwareInspectionIntelRunnerStage0.ps1"),
            2,
        )
        self.assertNotRegex(workflow, r"(?i)(?:&|python|dotnet|pwsh|powershell).*evaluated[\\/]")
        self.assertLess(workflow.index("path: control"), workflow.index("Run Stage 0 contracts"))
        self.assertLess(
            workflow.index("Run Stage 0 contracts"),
            workflow.index("Validate dispatch and approval manifest"),
        )
        self.assertLess(
            workflow.index("Validate dispatch and approval manifest"),
            workflow.index("path: evaluated"),
        )

    def test_stage0_workflow_has_no_self_hosted_registration_or_service_path(self) -> None:
        workflow = self._workflow().lower()
        for token in (
            "config.cmd", "run.cmd", "svc.sh", "svc.cmd", "--ephemeral",
            "actions/runner", "hardware-gate1-${{", "start-service",
        ):
            self.assertNotIn(token, workflow)

    def test_stage0_workflow_has_no_candidate_capture_report_or_offline_path(self) -> None:
        workflow = self._workflow().lower()
        for token in (
            "acquire-hardwareinspectionllmfitcandidate",
            "capture-hardwareinspectionwindowsreference",
            "write-hardwareinspectionllmfitgate1report",
            "trustedwindowsintel", "trustedoffline", "llmfit.exe",
            "third-party/bin/llmfit", "artifacts/hardware-inspection/llmfit",
            "dotnet restore", "dotnet build", "dotnet test",
            "netsh", "disable-netadapter", "enable-netadapter",
        ):
            self.assertNotIn(token, workflow)

    def test_stage0_workflow_has_no_raw_upload_or_operational_environment(self) -> None:
        workflow = self._workflow().lower()
        for token in (
            "upload-artifact", "granite_llmfit_", ".trx",
            "windows-reference.json", "llmfit-system.raw.json",
            "llmfit-gate1.evidence.json", "secrets.", "id-token:",
            "contents: write", "actions: write",
        ):
            self.assertNotIn(token, workflow)
```

- [ ] **Step 2: Run all eleven tests and confirm workflow RED**

```powershell
python -m unittest `
    tests.testing.hardware_inspection.test_intel_runner_stage0_contract `
    -v
```

Expected RED: the original three tests pass and the eight workflow tests fail only because the workflow is absent.

- [ ] **Step 3: Create the one-job hosted workflow**

Create `.github/workflows/hardware-inspection-intel-runner-stage0.yml` exactly in this shape, retaining the immutable action SHAs:

```yaml
name: Hardware Inspection Intel runner Stage 0

on:
  workflow_dispatch:
    inputs:
      confirm_repository_only:
        description: Confirm this run is repository-only and will not contact the Intel laptop
        required: true
        default: false
        type: boolean

permissions:
  contents: read

concurrency:
  group: hardware-inspection-intel-runner-stage0
  cancel-in-progress: false

jobs:
  hosted-preflight:
    name: Validate repository-only Intel runner controls
    if: >-
      github.event_name == 'workflow_dispatch' &&
      github.ref == format('refs/heads/{0}', github.event.repository.default_branch) &&
      github.actor == github.repository_owner &&
      github.triggering_actor == github.repository_owner &&
      github.run_attempt == 1 &&
      inputs.confirm_repository_only == true
    runs-on: windows-latest
    timeout-minutes: 10

    steps:
      - name: Check out default-branch controls
        uses: actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0
        with:
          ref: ${{ github.sha }}
          path: control
          fetch-depth: 1
          persist-credentials: false

      - name: Set up Python for repository contracts
        uses: actions/setup-python@a26af69be951a213d495a4c3e4e4022e16d87065
        with:
          python-version: '3.12.10'

      - name: Run Stage 0 contracts
        shell: pwsh
        working-directory: control
        run: >-
          python -m unittest
          tests.testing.hardware_inspection.test_intel_runner_stage0_contract
          -v

      - name: Validate dispatch and approval manifest
        id: approval
        shell: powershell
        env:
          STAGE0_WORKFLOW_REF: ${{ github.ref }}
          STAGE0_DEFAULT_BRANCH: ${{ github.event.repository.default_branch }}
          STAGE0_ACTOR: ${{ github.actor }}
          STAGE0_TRIGGERING_ACTOR: ${{ github.triggering_actor }}
          STAGE0_REPOSITORY_OWNER: ${{ github.repository_owner }}
          STAGE0_RUN_ATTEMPT: ${{ github.run_attempt }}
          STAGE0_CONFIRMATION: ${{ inputs.confirm_repository_only }}
        run: |
          & '.\control\scripts\hardware-inspection\Validate-HardwareInspectionIntelRunnerStage0.ps1' `
            -Phase Dispatch `
            -ControlRoot (Join-Path $env:GITHUB_WORKSPACE 'control') `
            -WorkflowRef $env:STAGE0_WORKFLOW_REF `
            -DefaultBranch $env:STAGE0_DEFAULT_BRANCH `
            -Actor $env:STAGE0_ACTOR `
            -TriggeringActor $env:STAGE0_TRIGGERING_ACTOR `
            -RepositoryOwner $env:STAGE0_REPOSITORY_OWNER `
            -RunAttempt $env:STAGE0_RUN_ATTEMPT `
            -ConfirmRepositoryOnly $env:STAGE0_CONFIRMATION `
            -GitHubOutputPath $env:GITHUB_OUTPUT

      - name: Check out approved source for identity comparison only
        uses: actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0
        with:
          ref: ${{ steps.approval.outputs.source_ref }}
          path: evaluated
          fetch-depth: 1
          persist-credentials: false

      - name: Confirm approved source identity and publish safe summary
        shell: powershell
        env:
          STAGE0_WORKFLOW_REF: ${{ github.ref }}
          STAGE0_DEFAULT_BRANCH: ${{ github.event.repository.default_branch }}
          STAGE0_ACTOR: ${{ github.actor }}
          STAGE0_TRIGGERING_ACTOR: ${{ github.triggering_actor }}
          STAGE0_REPOSITORY_OWNER: ${{ github.repository_owner }}
          STAGE0_RUN_ATTEMPT: ${{ github.run_attempt }}
          STAGE0_CONFIRMATION: ${{ inputs.confirm_repository_only }}
        run: |
          & '.\control\scripts\hardware-inspection\Validate-HardwareInspectionIntelRunnerStage0.ps1' `
            -Phase Source `
            -ControlRoot (Join-Path $env:GITHUB_WORKSPACE 'control') `
            -SourceCheckoutRoot (Join-Path $env:GITHUB_WORKSPACE 'evaluated') `
            -WorkflowRef $env:STAGE0_WORKFLOW_REF `
            -DefaultBranch $env:STAGE0_DEFAULT_BRANCH `
            -Actor $env:STAGE0_ACTOR `
            -TriggeringActor $env:STAGE0_TRIGGERING_ACTOR `
            -RepositoryOwner $env:STAGE0_REPOSITORY_OWNER `
            -RunAttempt $env:STAGE0_RUN_ATTEMPT `
            -ConfirmRepositoryOnly $env:STAGE0_CONFIRMATION `
            -SummaryPath $env:GITHUB_STEP_SUMMARY
```

The evaluated checkout is data for `git rev-parse` and `git status` only. Never set it as a working directory for Python, PowerShell, MSBuild, `dotnet`, or any repository script.

The canonical SHA-256 in the contract is over the exact UTF-8, no-BOM, LF-terminated YAML block above and is `9f14750368eef1a105332ccb54cd513ca92fd4af91dd8542efe5ededff304309`. Do not update that digest merely to make an edited workflow pass. Any intentional workflow-byte change requires a new security review of the full YAML, all nine canonical mutation cases, and a recomputed digest recorded in both the test and review evidence.

- [ ] **Step 4: Run the eleven-test GREEN suite**

```powershell
python -m unittest `
    tests.testing.hardware_inspection.test_intel_runner_stage0_contract `
    -v
```

Expected: 11 passed, zero failed, zero skipped.

- [ ] **Step 5: Commit the hosted-only workflow**

```powershell
git add -- `
    .github/workflows/hardware-inspection-intel-runner-stage0.yml `
    tests/testing/hardware_inspection/test_intel_runner_stage0_contract.py
git diff --cached --check
git commit -m "build(hardware-inspection): add hosted runner Stage 0 preflight"
```

Expected: exactly the workflow and its contract file; no self-hosted or runtime path.

### Task 5: Document Stage 0 and lock its inventory

**Files:**
- Modify: `tests/testing/hardware_inspection/test_intel_runner_stage0_contract.py`
- Create: `docs/testing/runbooks/Hardware-Inspection-Intel-Runner-Stage-0-Runbook.md`
- Create unchanged copy: `docs/superpowers/specs/2026-08-18-hardware-inspection-intel-runner-configuration-design.md`
- Modify: `scripts/README.md`

- [ ] **Step 1: Add the twelfth failing inventory/documentation contract**

Append this method to `IntelRunnerStage0ContractTests`:

```python
    def test_stage0_inventory_contains_only_approved_repository_controls(self) -> None:
        self.assertTrue(RUNBOOK_PATH.is_file())
        self.assertTrue(DESIGN_PATH.is_file())
        self.assertEqual(
            EXPECTED_DESIGN_SHA256,
            hashlib.sha256(DESIGN_PATH.read_bytes()).hexdigest(),
        )
        scripts_readme = (REPOSITORY_ROOT / "scripts/README.md").read_text(
            encoding="utf-8", errors="strict"
        )
        self.assertIn("Validate-HardwareInspectionIntelRunnerStage0.ps1", scripts_readme)
        self.assertIn("Hardware-Inspection-Intel-Runner-Stage-0-Runbook.md", scripts_readme)

        forbidden_paths = (
            ".github/workflows/hardware-inspection-intel-runner-stage-a.yml",
            ".github/workflows/hardware-inspection-intel-runner-stage-b.yml",
            ".github/workflows/hardware-inspection-intel-runner-stage-d.yml",
            "scripts/hardware-inspection/Invoke-HardwareInspectionIntelOffline.ps1",
            "scripts/hardware-inspection/Disable-HardwareInspectionNetwork.ps1",
            "scripts/hardware-inspection/Enable-HardwareInspectionNetwork.ps1",
        )
        for relative in forbidden_paths:
            with self.subTest(relative=relative):
                self.assertFalse((REPOSITORY_ROOT / relative).exists())

        runbook = RUNBOOK_PATH.read_text(encoding="utf-8", errors="strict")
        normalized_runbook = " ".join(runbook.split())
        for statement in (
            "Stage 0 is repository-only",
            "The UCL Intel laptop must remain disconnected from this stage",
            "Gate 1 remains Blocked",
            "Gate 2 must not start",
            "No LLM Fit candidate is acquired or executed",
            "future Stage A requires a separate approved plan",
            "The existing Workbook/TurboQuant runner must not be stopped, removed, relabelled, or contacted",
            "written UCL approval for the dedicated account, runner registration, repository and dependency execution, and evidence storage",
            "every repository writer must be UCL-authorised and trusted",
            "Actor, ref, label, environment, and approval-manifest checks are defence in depth, not substitutes",
        ):
            with self.subTest(statement=statement):
                self.assertIn(statement, normalized_runbook)
        self.assertIn(
            "docs/testing/runbooks/Hardware-Inspection-LLM-Fit-Gate-1-Runbook.md",
            runbook,
        )
```

- [ ] **Step 2: Run the suite and confirm the intended documentation RED**

```powershell
python -m unittest `
    tests.testing.hardware_inspection.test_intel_runner_stage0_contract `
    -v
```

Expected RED: 11 passed and only `test_stage0_inventory_contains_only_approved_repository_controls` fails because the new runbook/design copy/index entries do not yet exist.

- [ ] **Step 3: Copy the approved design without changing a byte**

Read the design at this exact path in the frozen feature worktree:

`C:\hardware-inspection\docs\superpowers\specs\2026-08-18-hardware-inspection-intel-runner-configuration-design.md`

Use `apply_patch` to add the identical content at the same repository-relative path in the integration worktree. Then verify:

```powershell
$sourceDesign = 'C:\hardware-inspection\docs\superpowers\specs\2026-08-18-hardware-inspection-intel-runner-configuration-design.md'
$targetDesign = 'docs\superpowers\specs\2026-08-18-hardware-inspection-intel-runner-configuration-design.md'
$sourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $sourceDesign).Hash
$targetHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $targetDesign).Hash
if ($sourceHash -cne '44916A51C7D4E1856B73564C8CE23E9B4EE60B20067C67A2EDEFF8C482E13CFE') {
    throw 'The frozen approved design has changed.'
}
if ($targetHash -cne $sourceHash) {
    throw 'The integration design copy is not byte-identical.'
}
```

Expected: both hashes match exactly.

- [ ] **Step 4: Add the self-contained Stage 0 runbook**

Create `docs/testing/runbooks/Hardware-Inspection-Intel-Runner-Stage-0-Runbook.md` with this complete operational scope:

```markdown
# Hardware Inspection Intel Runner Stage 0 Runbook

## Purpose

Stage 0 is repository-only. It validates default-branch controls and the identity
of one approved Hardware Inspection feature commit on a GitHub-hosted Windows
runner. The UCL Intel laptop must remain disconnected from this stage.

No LLM Fit candidate is acquired or executed. No hardware is inspected, no raw
evidence is read or uploaded, and no network adapter is changed. Gate 1 remains
Blocked. Gate 2 must not start.

## Authority

The approved design is
`docs/superpowers/specs/2026-08-18-hardware-inspection-intel-runner-configuration-design.md`.
The operational Gate 1 instructions remain in
`docs/testing/runbooks/Hardware-Inspection-LLM-Fit-Gate-1-Runbook.md` in the exact
evaluated feature checkout. Stage 0 does not execute or alter that runbook.

## Preconditions

1. `feature/hardware-inspection` is frozen and pushed at the SHA in
   `.github/hardware-inspection/llmfit-gate1-approved-source.json`.
2. The Stage 0 workflow and validator are present on the repository default
   branch.
3. The operator is both `github.actor` and `github.triggering_actor` and is the
   repository owner.
4. The run is attempt 1 on `main`.
5. Stage 0 neither checks nor changes any laptop or self-hosted-runner state.
   An unrelated runner remains under its existing operator-owned controls because
   the Stage 0 workflow has no job that can target it.

The existing Workbook/TurboQuant runner must not be stopped, removed, relabelled,
or contacted for Stage 0.

Stage 0 does not establish permission for a later hardware job. Before any future
Stage A, B, or D dispatch, there must be written UCL approval for the dedicated
account, runner registration, repository and dependency execution, and evidence
storage. For the entire dispatch, queue, registration, and job window, every
repository writer must be UCL-authorised and trusted, or every non-operator writer
must be reduced to read-only access. Actor, ref, label, environment, and
approval-manifest checks are defence in depth, not substitutes for those gates.

## Local contract verification

From a clean default-branch integration checkout:

```powershell
python -m unittest tests.testing.hardware_inspection.test_intel_runner_stage0_contract -v

$tokens = $null
$errors = $null
[Management.Automation.Language.Parser]::ParseFile(
    (Resolve-Path '.\scripts\hardware-inspection\Validate-HardwareInspectionIntelRunnerStage0.ps1'),
    [ref] $tokens,
    [ref] $errors
) | Out-Null
if ($errors.Count -ne 0) { throw 'The Stage 0 validator does not parse in Windows PowerShell 5.1.' }

git diff --check
```

Required result: exactly 12 contracts pass, the parser reports zero errors, and
the diff check is clean.

## Manual hosted dispatch

Stage 0 has only the explicit repository-only confirmation input. It deliberately
does not request or reserve a routing label; each future authorised Stage A, B,
or D dispatch must generate its own fresh one-time label.

```powershell
gh workflow run hardware-inspection-intel-runner-stage0.yml `
    --repo arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant `
    --ref main `
    -f confirm_repository_only=true
```

Inspect the queued run before relying on its result. It must contain exactly one
job, `hosted-preflight`, on `windows-latest`. It checks out the default-branch
controls and the approved feature ref into separate directories. Only the
default-branch validator and Python contracts execute. The evaluated feature
checkout is used only for `git rev-parse` and `git status` identity checks.

## Expected result

The fixed job summary states:

- Stage 0 only.
- The Intel laptop was not contacted.
- The LLM Fit candidate was not acquired or executed.
- Gate 1 remains Blocked.
- Gate 2 is prohibited.

No artifact is uploaded. A failed, cancelled, rerun, non-owner, non-default-
branch, moved-feature-tip, or dirty-checkout result is not an
approval and must not be worked around.

## Stop conditions

Stop immediately if the workflow contains or attempts any self-hosted job,
runner registration, candidate acquisition, candidate execution, hardware
capture, raw evidence access, adapter change, artifact upload, or execution from
the evaluated checkout. Preserve the failure; do not weaken the contract.

## Deferred stages

Stage 0 does not authorise the loan laptop. A future Stage A requires a separate
approved plan, explicit UCL permission, repository-writer trust controls, and a
fresh one-time routing label created for that authorised dispatch. Stages B, C,
and D likewise require their separately
approved implementations. Stage C remains a manual operator-owned offline
bridge and must never automate network-adapter changes.
```

Do not copy the operational Gate 1 runbook to `main`; the plain path above deliberately refers to the exact evaluated feature checkout and avoids a broken relative link.

- [ ] **Step 5: Index only the repository validator and Stage 0 runbook**

Extend `scripts/README.md` without changing its existing introduction:

```markdown
## Hardware Inspection

- `hardware-inspection/Validate-HardwareInspectionIntelRunnerStage0.ps1`
  validates the hosted, repository-only Stage 0 dispatch context, strict approval
  manifest, and approved source identity. It does not contact a self-hosted
  runner or execute Hardware Inspection code.
- The operator boundary and verification commands are in
  [`Hardware-Inspection-Intel-Runner-Stage-0-Runbook.md`](../docs/testing/runbooks/Hardware-Inspection-Intel-Runner-Stage-0-Runbook.md).
```

Do not modify `docs/testing/README.md`, `docs/testing/evidence/README.md`, either workflow change-control register, or an architecture diagram. They govern other campaigns or application workflows and Stage 0 creates no runtime evidence.

- [ ] **Step 6: Run the exact twelve-test GREEN suite**

```powershell
python -m unittest `
    tests.testing.hardware_inspection.test_intel_runner_stage0_contract `
    -v
```

Expected: exactly 12 tests passed, zero failed, zero skipped. Confirm the design-hash assertion and every forbidden Stage A-D path assertion ran.

- [ ] **Step 7: Commit the documentation boundary**

```powershell
git add -- `
    docs/testing/runbooks/Hardware-Inspection-Intel-Runner-Stage-0-Runbook.md `
    docs/superpowers/specs/2026-08-18-hardware-inspection-intel-runner-configuration-design.md `
    scripts/README.md `
    tests/testing/hardware_inspection/test_intel_runner_stage0_contract.py
git diff --cached --check
git commit -m "docs(hardware-inspection): document runner Stage 0 boundary"
```

Expected: exactly the four listed paths.

### Task 6: Verify Stage 0 and prove Hardware Inspection non-deviation

**Files:**
- Verify: all seven Stage 0 integration paths
- Verify only in frozen feature worktree: Hardware Inspection deterministic projects
- Do not modify: all other paths

- [ ] **Step 1: Invoke the verification-before-completion workflow**

Before making any pass/complete claim, invoke `superpowers:verification-before-completion` and preserve the fresh command outputs below.

- [ ] **Step 2: Run the complete Stage 0 contract suite**

From `C:\hardware-inspection-stage0`:

```powershell
python -m unittest `
    tests.testing.hardware_inspection.test_intel_runner_stage0_contract `
    -v
if ($LASTEXITCODE -ne 0) { throw 'Stage 0 contracts failed.' }
```

Expected: exactly 12/12 passed with no skips.

- [ ] **Step 3: Parse the validator with the supported Windows PowerShell floor**

```powershell
$tokens = $null
$errors = $null
[Management.Automation.Language.Parser]::ParseFile(
    (Resolve-Path '.\scripts\hardware-inspection\Validate-HardwareInspectionIntelRunnerStage0.ps1'),
    [ref] $tokens,
    [ref] $errors
) | Out-Null
if ($errors.Count -ne 0) {
    $errors | ForEach-Object { Write-Error $_.Message }
    throw 'Stage 0 validator has PowerShell 5.1 parser errors.'
}
```

Expected: zero parser errors. This is a local parser check only and emits no host evidence.

- [ ] **Step 4: Prove the workflow is hosted-only and privacy-safe**

```powershell
$workflowPath = '.github\workflows\hardware-inspection-intel-runner-stage0.yml'
$workflow = Get-Content -LiteralPath $workflowPath -Raw
if (($workflow | Select-String -AllMatches 'runs-on: windows-latest').Matches.Count -ne 1) {
    throw 'Stage 0 must have exactly one hosted job.'
}
$forbidden = @(
    'self-hosted', 'upload-artifact', 'GRANITE_LLMFIT_', 'llmfit.exe',
    'Acquire-HardwareInspectionLlmFitCandidate',
    'Capture-HardwareInspectionWindowsReference',
    'Write-HardwareInspectionLlmFitGate1Report',
    'TrustedWindowsIntel', 'TrustedOffline', 'Disable-NetAdapter',
    'Enable-NetAdapter', 'netsh', 'dotnet test', 'dotnet build', 'dotnet restore'
)
foreach ($token in $forbidden) {
    if ($workflow.IndexOf($token, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw "Forbidden Stage 0 workflow token: $token"
    }
}
```

Expected: no exception.

- [ ] **Step 5: Prove the integration diff is exactly the approved allowlist**

```powershell
$allowed = @(
    '.github/hardware-inspection/llmfit-gate1-approved-source.json',
    '.github/workflows/hardware-inspection-intel-runner-stage0.yml',
    'docs/superpowers/specs/2026-08-18-hardware-inspection-intel-runner-configuration-design.md',
    'docs/testing/runbooks/Hardware-Inspection-Intel-Runner-Stage-0-Runbook.md',
    'scripts/README.md',
    'scripts/hardware-inspection/Validate-HardwareInspectionIntelRunnerStage0.ps1',
    'tests/testing/hardware_inspection/test_intel_runner_stage0_contract.py'
)
$changed = @(git diff --name-only origin/main...HEAD) | Sort-Object
$expected = @($allowed | Sort-Object)
if ([string]::Join("`n", $changed) -cne [string]::Join("`n", $expected)) {
    throw "Stage 0 changed an unexpected path.`nActual: $($changed -join ', ')"
}
git diff --check origin/main...HEAD
if ($LASTEXITCODE -ne 0) { throw 'Stage 0 diff check failed.' }
```

Expected: exactly seven paths and a clean diff.

- [ ] **Step 6: Re-run the frozen Hardware Inspection non-deviation baselines**

From `C:\hardware-inspection`, derive the expected SHA again from the integration manifest, prove local/remote equality and a clean feature tree, and repeat Task 1 Step 3 without setting any `GRANITE_LLMFIT_*` variables:

```powershell
$featureRoot = 'C:\hardware-inspection'
$integrationRoot = 'C:\hardware-inspection-stage0'
$approval = Get-Content -LiteralPath (Join-Path $integrationRoot '.github\hardware-inspection\llmfit-gate1-approved-source.json') -Raw | ConvertFrom-Json
$frozenFeatureSha = [string]$approval.approvedTipSha
$localFeatureSha = (git -C $featureRoot rev-parse HEAD).Trim()
$remoteLine = @(git -C $featureRoot ls-remote --heads origin refs/heads/feature/hardware-inspection)
$featureStatus = @(git -C $featureRoot status --short)
if (
    $frozenFeatureSha -cnotmatch '\A[0-9a-f]{40}\z' -or
    $localFeatureSha -cne $frozenFeatureSha -or
    $remoteLine.Count -ne 1 -or
    (($remoteLine[0] -split '\s+')[0] -cne $frozenFeatureSha) -or
    $featureStatus.Count -ne 0
) { throw 'The frozen feature source changed before final verification.' }
```

Then execute the complete self-contained Task 1 Step 3 block again.

Expected:

- exactly 174 `Deterministic` tests pass, zero fail/skip;
- exactly 3 `Task8Deterministic` tests pass, zero fail/skip;
- no candidate process, candidate download, port 8787 listener, network change, or evidence artifact;
- the protected implementation paths are byte-identical to the frozen remote branch.

- [ ] **Step 7: Request an independent code review**

Invoke `superpowers:requesting-code-review`. The reviewer must answer these exact questions:

1. Is every executable Stage 0 path hosted-only and default-branch controlled?
2. Can any file from the evaluated feature checkout execute?
3. Can Stage 0 contact or route to a self-hosted runner?
4. Can it acquire/execute the candidate, capture hardware, touch network adapters, upload raw artifacts, or advance Gate 1/2?
5. Is the live manifest bound to the exact frozen remote feature SHA?
6. Are the only repository changes the seven approved integration paths?
7. Did any Hardware Inspection or Model Inspection behavior change?

Any Critical or Important finding requires a focused RED, the smallest correction, all 12 contracts again, the two frozen Hardware baselines again if relevant, and a fresh re-review.

### Task 7: Publish Stage 0 for review without enabling the Intel laptop

**Files:**
- Push branch only: `integration/hardware-inspection-intel-runner-stage0`
- Open draft PR only; do not merge the feature branch wholesale

- [ ] **Step 1: Confirm both worktrees and remote feature identity one last time**

```powershell
git -C C:\hardware-inspection status --short
git -C C:\hardware-inspection-stage0 status --short
$approval = Get-Content `
    -LiteralPath 'C:\hardware-inspection-stage0\.github\hardware-inspection\llmfit-gate1-approved-source.json' `
    -Raw | ConvertFrom-Json
$frozenFeatureSha = [string]$approval.approvedTipSha
if ($frozenFeatureSha -cnotmatch '\A[0-9a-f]{40}\z') {
    throw 'The integration manifest SHA is invalid.'
}
$localFeature = (git -C C:\hardware-inspection rev-parse HEAD).Trim()
$remoteFeatureLine = @(git -C C:\hardware-inspection ls-remote --heads origin refs/heads/feature/hardware-inspection)
if ($remoteFeatureLine.Count -ne 1) { throw 'Frozen feature ref is unavailable.' }
$remoteFeature = ($remoteFeatureLine[0] -split '\s+')[0]
if ($localFeature -cne $frozenFeatureSha -or $remoteFeature -cne $frozenFeatureSha) {
    throw 'The frozen feature identity changed before Stage 0 publication.'
}
```

Expected: both worktrees clean and all three SHA values identical.

- [ ] **Step 2: Push only the integration branch**

```powershell
git -C C:\hardware-inspection-stage0 push `
    --set-upstream origin `
    integration/hardware-inspection-intel-runner-stage0
if ($LASTEXITCODE -ne 0) { throw 'Stage 0 integration push failed.' }
```

Expected: no force push and no new feature-branch commit.

- [ ] **Step 3: Open a draft PR into `main`**

Use the repository's GitHub publishing workflow or `gh` to open a draft PR whose body states:

- repository-only Stage 0;
- exactly seven changed paths;
- frozen evaluated feature ref and SHA;
- 12/12 Stage 0 contracts;
- unchanged 174/174 and 3/3 Hardware Inspection baselines;
- no self-hosted runner, candidate, hardware capture, raw upload, network action, Gate 1 change, or Gate 2 start;
- Stage A-D remain separately permission-gated.

Do not include absolute paths, host/user/device data, TRX, raw evidence, or candidate metadata.

- [ ] **Step 4: Verify the default-branch workflow after review and merge**

Do not dispatch before the workflow exists on the repository default branch. After an authorised merge into `main`:

```powershell
gh workflow view hardware-inspection-intel-runner-stage0.yml `
    --repo arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant `
    --ref main `
    --yaml
```

Expected: the reviewed hosted-only workflow, not a feature-branch copy.

- [ ] **Step 5: Dispatch the repository-only preflight once**

```powershell
gh workflow run hardware-inspection-intel-runner-stage0.yml `
    --repo arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant `
    --ref main `
    -f confirm_repository_only=true
```

Expected: one `windows-latest` job, no artifact, and the fixed Stage 0 summary. Do not install, register, start, or connect a runner on the UCL laptop for this dispatch.

- [ ] **Step 6: Record the Stage 0 handoff without claiming Gate 1 progress**

Report the hosted run URL and fixed non-sensitive outcome only. State explicitly:

- Hardware Inspection implementation and UI are unchanged;
- the feature plan and intentions are unchanged;
- Gate 1 remains Blocked until authorised Intel target and true offline runs succeed;
- Gate 2 must not start;
- Stage A is the next possible planning task, not an authorised implementation task.

## Completion criteria

Stage 0 is complete only when all of the following are true:

- the frozen remote feature SHA equals the strict manifest SHA;
- exactly 12 Stage 0 contracts pass with no skip;
- the validator parses in Windows PowerShell 5.1;
- the integration diff contains exactly the seven approved paths;
- the workflow has one hosted job and no self-hosted/runtime/evidence path;
- the unchanged Hardware Inspection suites remain 174/174 and 3/3;
- an independent review has no Critical or Important finding;
- the merged default-branch workflow completes its hosted preflight;
- no UCL laptop, candidate, network adapter, raw evidence, Gate 1 disposition, Gate 2 work, product UI, or Model Inspection file was touched.

## Explicitly deferred work

This plan does not implement Stages A, B, C, or D. It does not configure the Intel laptop or runner, create a persistent session, acquire LLM Fit, run trusted or offline tests, collect evidence, or generate a new Gate 1 report. Those steps remain governed by the approved design and require separate plans after the relevant UCL and repository-writer approvals are recorded.
