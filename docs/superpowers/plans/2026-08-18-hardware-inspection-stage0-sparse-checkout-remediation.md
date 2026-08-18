# Hardware Inspection Stage 0 Sparse-Checkout Remediation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the manual GitHub-hosted Stage 0 workflow complete successfully on Windows by materializing only its approved repository controls and inert evaluated design documents.

**Architecture:** Retain the pinned `actions/checkout` steps, require Git `2.28.0+` before either checkout, and add exact sparse-checkout inputs: cone mode for control and a root-anchored non-cone pattern for evaluated. The control checkout includes complete workflow, Hardware Inspection script, runbook, design, manifest, and contract-test namespaces; the evaluated checkout includes only inert design specifications at the immutable approved SHA while `source_ref` remains provenance metadata.

**Tech Stack:** GitHub Actions YAML, `actions/checkout` v7 pinned at `9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0`, Python 3.12 `unittest`, Windows PowerShell 5.1, Git sparse checkout.

**Spec:** `docs/superpowers/specs/2026-08-18-hardware-inspection-stage0-sparse-checkout-remediation-design.md`

## Verification erratum — 2026-08-18

Task 2's local sparse-checkout proof showed that Git cone mode's parent-directory semantics materialize repository-root files even when the configured evaluated cone is `docs/superpowers/specs`. The observed evaluated checkout included `Initialize-Repository-Structure.ps1` and `IBM Granite with TurboQuant (Intel).slnx`, so it was not bounded to inert specifications.

The correction is evaluated-only. Control remains the exact cone-mode boundary; evaluated uses one root-anchored non-cone directory pattern bounded to the 22 inert Markdown specifications:

```yaml
          sparse-checkout: |
            /docs/superpowers/specs/
          sparse-checkout-cone-mode: false
```

No evaluated code, project, executable, workflow step, or test runs. The corrected canonical workflow SHA-256 is `81a5893ccde84507ea8fd6d5409811ba55f0d16252b864907c29884f61c8fabf`.

## Security verification erratum — 2026-08-18

Follow-up verification found that the pinned `actions/checkout` action can fall back to a full REST archive when Git is missing or too old to support sparse checkout. The fixed first executable step now performs a privacy-safe Git capability precheck before either checkout, accepts only a single strict version line at Git `2.28.0` or newer, and fails with a fixed message without exposing command output or paths. This prevents REST fallback from materializing a broad worktree before the sparse boundary is established.

The evaluated checkout now resolves the immutable `steps.approval.outputs.approved_sha` value, not the movable `steps.approval.outputs.source_ref`. `source_ref` remains validator output and summary provenance only. The precheck and immutable SHA pin prevent both broad REST fallback materialization and movable-ref pre-materialization. The corrected canonical workflow SHA-256 is `81a5893ccde84507ea8fd6d5409811ba55f0d16252b864907c29884f61c8fabf`.

---

## Global Constraints

- Work only in `C:\hardware-inspection-stage0-fix` on branch `fix/hardware-inspection-stage0-sparse-checkout`, based on failed `main` revision `0b7da6413ba20508928c2033b88dc3d3efd10143`.
- Do not modify Hardware Inspection UI/runtime, LLM Fit candidate logic, evidence, Gate 1 records, Gate 2, Model Inspection, the Intel laptop, self-hosted runners, adapters, or network state.
- Keep the workflow manual-only, `windows-latest`, owner-only, default-branch-only, first-attempt-only, read-only, and artifact-free.
- Keep both immutable action pins and both `persist-credentials: false` settings.
- Execute code only from `control`; never execute content from `evaluated`.
- Keep exactly 12 Stage 0 contract-test identities.
- Use `apply_patch` for repository edits. Preserve unrelated and ignored files.
- Use UTF-8 without BOM and LF-only YAML with exactly one trailing LF.
- Never rerun failed run `32138539513`; after the fix merge, create a new `workflow_dispatch` run so `github.run_attempt` remains `1`.

### Task 1: Lock and implement the sparse checkout boundary

**Files:**
- Modify: `tests/testing/hardware_inspection/test_intel_runner_stage0_contract.py`
- Modify: `.github/workflows/hardware-inspection-intel-runner-stage0.yml`

**Interfaces:**
- Consumes: existing canonical workflow bytes, the pinned checkout action, the approval manifest output `steps.approval.outputs.approved_sha` for immutable evaluated materialization, `source_ref` as provenance only, and the existing 12-test contract module.
- Produces: canonical workflow SHA-256 `81a5893ccde84507ea8fd6d5409811ba55f0d16252b864907c29884f61c8fabf`, the fixed first Git precheck, and the exact control-cone and evaluated non-cone sparse-checkout blocks.

- [ ] **Step 1: Add the focused failing contract assertions**

In `test_stage0_workflow_pins_actions_and_drops_checkout_credentials`, keep all current assertions, require the exact first Git precheck below, execute its extracted run block under PowerShell 5.1 with deterministic local stubs, and add exact expected checkout fragments:

```yaml
      - name: Require sparse-checkout-capable Git
        shell: powershell
        run: |
          $ProgressPreference = 'SilentlyContinue'
          $ErrorActionPreference = 'Stop'
          $failure = 'HI-RUNNER-STAGE0-GIT-INVALID: required Git capability is unavailable.'
          try {
            $gitCommand = Get-Command git -CommandType Application -ErrorAction SilentlyContinue
            if ($null -eq $gitCommand) {
              throw 'invalid'
            }
            $versionLines = @(& $gitCommand.Source --version 2>$null)
            if ($LASTEXITCODE -ne 0 -or $versionLines.Count -ne 1) {
              throw 'invalid'
            }
            $versionText = [string]$versionLines[0]
            if ($versionText -cnotmatch '\Agit version (?<major>0|[1-9][0-9]*)\.(?<minor>0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)(?:\.windows\.(?:0|[1-9][0-9]*))?\z') {
              throw 'invalid'
            }
            $major = [int]$Matches['major']
            $minor = [int]$Matches['minor']
            if ($major -lt 2 -or ($major -eq 2 -and $minor -lt 28)) {
              throw 'invalid'
            }
          }
          catch {
            [Console]::Error.WriteLine($failure)
            exit 1
          }
```

The semantic matrix accepts only `2.28.0`, `2.51.0`, `2.51.0.windows.2`, and `3.0.0`, and rejects `2.27.99`, two-component, uppercase/malformed, multiline, nonzero, throwing, and missing cases with empty stdout and exact `HI-RUNNER-STAGE0-GIT-INVALID: required Git capability is unavailable.\n` stderr. A malicious `C:\private\SECRET_TOKEN` source must not appear in output. The precheck contains no `${{`, absolute path, network, or candidate command.

```python
control_sparse_checkout = """          sparse-checkout: |
            .github/hardware-inspection
            .github/workflows
            docs/superpowers/specs
            docs/testing/runbooks
            scripts/hardware-inspection
            tests/testing/hardware_inspection
          sparse-checkout-cone-mode: true
"""
evaluated_sparse_checkout = """          sparse-checkout: |
            /docs/superpowers/specs/
          sparse-checkout-cone-mode: false
"""

self.assertEqual(text.count("          sparse-checkout: |\n"), 2)
self.assertEqual(text.count("          sparse-checkout-cone-mode: true\n"), 1)
self.assertEqual(text.count("          sparse-checkout-cone-mode: false\n"), 1)
self.assertIn(control_sparse_checkout, text)
self.assertIn(evaluated_sparse_checkout, text)
self.assertNotIn("          filter:", text)
self.assertNotIn("core.longpaths", text.casefold())

control_start = text.index("      - name: Check out default-branch controls\n")
control_end = text.index("      - name: Set up Python for repository contracts\n", control_start)
evaluated_start = text.index("      - name: Check out approved source for identity comparison only\n")
evaluated_end = text.index("      - name: Confirm approved source identity and publish safe summary\n", evaluated_start)
control_step = text[control_start:control_end]
evaluated_step = text[evaluated_start:evaluated_end]

self.assertIn(control_sparse_checkout, control_step)
self.assertNotIn(evaluated_sparse_checkout, control_step)
self.assertIn(evaluated_sparse_checkout, evaluated_step)
self.assertNotIn(control_sparse_checkout, evaluated_step)
self.assertIn("          sparse-checkout-cone-mode: true\n", control_step)
self.assertNotIn("          sparse-checkout-cone-mode: false\n", control_step)
self.assertIn("          sparse-checkout-cone-mode: false\n", evaluated_step)
self.assertNotIn("          sparse-checkout-cone-mode: true\n", evaluated_step)
```

- [ ] **Step 2: Run the focused test and capture RED**

Run:

```powershell
python -m unittest `
  tests.testing.hardware_inspection.test_intel_runner_stage0_contract.IntelRunnerStage0ContractTests.test_stage0_workflow_pins_actions_and_drops_checkout_credentials `
  -v
python -m unittest `
  tests.testing.hardware_inspection.test_intel_runner_stage0_contract.IntelRunnerStage0ContractTests.test_stage0_workflow_executes_validators_only_from_control_checkout `
  -v
python -m unittest `
  tests.testing.hardware_inspection.test_intel_runner_stage0_contract.IntelRunnerStage0ContractTests.test_stage0_workflow_reads_approved_source_without_free_form_sha_input `
  -v
```

Run the focused existing identities for the precheck, checkout ordering, and approved-ref assertions while retaining the current pre-security digest during RED. Expected failures are the absent precheck and old movable `source_ref` ref, not import, syntax, harness, or canonical-digest errors.

- [ ] **Step 3: Add the exact Git precheck and control sparse checkout**

Add the exact Git precheck as the first executable step directly under `steps:` and before any checkout. It must have no preceding `uses:` step. Then extend `Check out default-branch controls` without changing its ref, path, depth, credential, action-pin, or step order:

```yaml
        with:
          ref: ${{ github.sha }}
          path: control
          fetch-depth: 1
          persist-credentials: false
          sparse-checkout: |
            .github/hardware-inspection
            .github/workflows
            docs/superpowers/specs
            docs/testing/runbooks
            scripts/hardware-inspection
            tests/testing/hardware_inspection
          sparse-checkout-cone-mode: true
```

- [ ] **Step 4: Add the exact evaluated sparse checkout at the approved SHA**

Extend `Check out approved source for identity comparison only` without changing its approved SHA ref, path, depth, credential, action-pin, or step order. Keep `source_ref` only in validator provenance output and summary metadata:

```yaml
        with:
          ref: ${{ steps.approval.outputs.approved_sha }}
          path: evaluated
          fetch-depth: 1
          persist-credentials: false
          sparse-checkout: |
            /docs/superpowers/specs/
          sparse-checkout-cone-mode: false
```

- [ ] **Step 5: Update the canonical workflow digest**

Change only:

```python
EXPECTED_WORKFLOW_SHA256 = "81a5893ccde84507ea8fd6d5409811ba55f0d16252b864907c29884f61c8fabf"
```

The new digest is computed from the exact LF-only workflow above with one trailing LF. Do not weaken `_assert_canonical_workflow` or remove any existing mutation.

- [ ] **Step 6: Run GREEN verification**

Run:

```powershell
python -m unittest tests.testing.hardware_inspection.test_intel_runner_stage0_contract -v
python -m py_compile tests/testing/hardware_inspection/test_intel_runner_stage0_contract.py

$tokens = $null
$errors = $null
[System.Management.Automation.Language.Parser]::ParseFile(
    (Resolve-Path 'scripts/hardware-inspection/Validate-HardwareInspectionIntelRunnerStage0.ps1').Path,
    [ref]$tokens,
    [ref]$errors
) | Out-Null
if ($errors.Count -ne 0) { throw 'Stage 0 validator parser check failed.' }

git diff --check
git diff --check origin/main...HEAD
```

Expected: the PowerShell 5.1 precheck semantic matrix and exactly 12 tests pass with zero skips/failures/errors, Python compilation exits 0, PowerShell reports zero parser errors, and both working-tree and committed-range whitespace checks exit 0.

- [ ] **Step 7: Verify exact scope and bytes**

Verify:

```powershell
$workflow = '.github/workflows/hardware-inspection-intel-runner-stage0.yml'
$workflowBytes = [System.IO.File]::ReadAllBytes($workflow)
if ($workflowBytes -contains 13) { throw 'Workflow contains CR bytes.' }
if (-not ([System.Text.Encoding]::UTF8.GetString($workflowBytes).EndsWith("`n"))) {
    throw 'Workflow lacks its final LF.'
}
if ((Get-FileHash -Algorithm SHA256 $workflow).Hash.ToLowerInvariant() -ne
    '81a5893ccde84507ea8fd6d5409811ba55f0d16252b864907c29884f61c8fabf') {
    throw 'Workflow digest mismatch.'
}
if ((Get-Item $workflow).Length -ne 5940) { throw 'Workflow byte length mismatch.' }

$actualPaths = @(
    git diff --name-only origin/main...HEAD
    git diff --name-only
) | Sort-Object -Unique
$expectedPaths = @(
    '.github/workflows/hardware-inspection-intel-runner-stage0.yml'
    'docs/superpowers/plans/2026-08-18-hardware-inspection-stage0-sparse-checkout-remediation.md'
    'docs/superpowers/specs/2026-08-18-hardware-inspection-stage0-sparse-checkout-remediation-design.md'
    'tests/testing/hardware_inspection/test_intel_runner_stage0_contract.py'
) | Sort-Object
if (Compare-Object -ReferenceObject $expectedPaths -DifferenceObject $actualPaths) {
    throw 'Stage 0 remediation scope differs from the exact four-path allowlist.'
}
```

Expected implementation scope beyond the already committed design and plan: only the workflow and contract test. No candidate, operational, UI, evidence, or runbook file changes.

- [ ] **Step 8: Commit the tested implementation**

```powershell
git add -- `
  .github/workflows/hardware-inspection-intel-runner-stage0.yml `
  tests/testing/hardware_inspection/test_intel_runner_stage0_contract.py
git diff --cached --check
git commit -m "fix(hardware-inspection): pin Stage 0 sparse prerequisites"
```

The worktree must be clean after the commit.

---

### Task 2: Prove sparse behavior and complete independent reviews

**Files:**
- Read: `.github/workflows/hardware-inspection-intel-runner-stage0.yml`
- Read: `tests/testing/hardware_inspection/test_intel_runner_stage0_contract.py`
- Read: `scripts/hardware-inspection/Validate-HardwareInspectionIntelRunnerStage0.ps1`
- Create temporarily outside the repository: owned sparse control and evaluated clones; delete them after verification.

**Interfaces:**
- Consumes: Task 1 workflow digest, the fixed Git `2.28.0+` precheck result, and exact control cone/evaluated non-cone sparse boundaries.
- Produces: local proof that the control cone runs all 12 contracts and the evaluated non-cone pattern materializes only inert specs at immutable `approved_sha` without materializing executable feature content.

- [ ] **Step 1: Run the exact owned sparse-checkout proof**

Before creating either temporary clone, execute the exact first-step Git precheck under PowerShell 5.1 and stop if it does not accept the single strict version line. Resolve `approvedSha` from the exact approval manifest before materialization; use `source_ref` only when checking the validator's provenance output, never as an evaluated checkout ref. These prerequisites ensure a missing/old Git cannot trigger REST archive fallback and that evaluated materialization cannot follow a movable ref.

Run this as one PowerShell command so the randomly generated ownership path never has to be inferred by a later shell. It clones only into a new GUID-named direct child of the OS temp directory and deletes only that validated child in `finally`:

```powershell
$ErrorActionPreference = 'Stop'
$tempParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\')
$proofLeaf = 'GraniteEdgeAI-Stage0-Sparse-Proof-' + [Guid]::NewGuid().ToString('N')
$proofRoot = Join-Path $tempParent $proofLeaf
$controlRoot = Join-Path $proofRoot 'control'
$evaluatedRoot = Join-Path $proofRoot 'evaluated'
$approvedSha = 'cc2e57ceb94e73e49f34fc383d5440a9047fba21'

if (Test-Path -LiteralPath $proofRoot) { throw 'Owned sparse-proof root already exists.' }

try {
    New-Item -ItemType Directory -Path $proofRoot -ErrorAction Stop | Out-Null
    $resolvedProofRoot = (Resolve-Path -LiteralPath $proofRoot).Path.TrimEnd('\')
    if ((Split-Path -Parent $resolvedProofRoot) -cne $tempParent -or
        (Split-Path -Leaf $resolvedProofRoot) -cne $proofLeaf) {
        throw 'Owned sparse-proof root escaped the OS temp directory.'
    }

    & git clone --no-checkout --no-local 'C:\hardware-inspection-stage0-fix' $controlRoot
    if ($LASTEXITCODE -ne 0) { throw 'Control clone failed.' }
    & git -C $controlRoot sparse-checkout init --cone
    if ($LASTEXITCODE -ne 0) { throw 'Control sparse initialization failed.' }
    & git -C $controlRoot sparse-checkout set `
        '.github/hardware-inspection' `
        '.github/workflows' `
        'docs/superpowers/specs' `
        'docs/testing/runbooks' `
        'scripts/hardware-inspection' `
        'tests/testing/hardware_inspection'
    if ($LASTEXITCODE -ne 0) { throw 'Control cone selection failed.' }
    $implementationSha = (& git -C 'C:\hardware-inspection-stage0-fix' rev-parse HEAD).Trim()
    & git -C $controlRoot checkout --detach $implementationSha
    if ($LASTEXITCODE -ne 0) { throw 'Control sparse checkout failed.' }

    foreach ($forbiddenPath in @(
        'research',
        'experiments',
        'third-party',
        'IBM Granite with TurboQuant (Intel)',
        'tools/HardwareInspection.LlmFitSpike'
    )) {
        if (Test-Path -LiteralPath (Join-Path $controlRoot $forbiddenPath)) {
            throw 'Control sparse checkout materialized a forbidden tree.'
        }
    }

    Push-Location $controlRoot
    try {
        $contractOutput = (& python -m unittest `
            tests.testing.hardware_inspection.test_intel_runner_stage0_contract `
            -v 2>&1 | Out-String)
        if ($LASTEXITCODE -ne 0 -or
            $contractOutput -notmatch '(?m)^Ran 12 tests' -or
            $contractOutput -notmatch '(?m)^OK$') {
            throw 'Sparse control contracts were not exactly 12 passing tests.'
        }
    }
    finally {
        Pop-Location
    }

    & git clone --no-checkout --no-local 'C:\hardware-inspection' $evaluatedRoot
    if ($LASTEXITCODE -ne 0) { throw 'Evaluated clone failed.' }
    & git -C $evaluatedRoot sparse-checkout init --no-cone
    if ($LASTEXITCODE -ne 0) { throw 'Evaluated sparse initialization failed.' }
    & git -C $evaluatedRoot sparse-checkout set --no-cone '/docs/superpowers/specs/'
    if ($LASTEXITCODE -ne 0) { throw 'Evaluated non-cone selection failed.' }
    & git -C $evaluatedRoot checkout --detach $approvedSha
    if ($LASTEXITCODE -ne 0) { throw 'Evaluated sparse checkout failed.' }

    $actualSha = (& git -C $evaluatedRoot rev-parse HEAD).Trim()
    $evaluatedStatus = @(& git -C $evaluatedRoot status --porcelain --untracked-files=all)
    if ($actualSha -cne $approvedSha -or $evaluatedStatus.Count -ne 0) {
        throw 'Evaluated sparse identity or cleanliness check failed.'
    }
    foreach ($forbiddenPath in @('.github', 'scripts', 'tests', 'tools', 'third-party')) {
        if (Test-Path -LiteralPath (Join-Path $evaluatedRoot $forbiddenPath)) {
            throw 'Evaluated sparse checkout materialized executable repository content.'
        }
    }
    $forbiddenEvaluatedFile = Get-ChildItem -LiteralPath $evaluatedRoot -File -Recurse -Force |
        Where-Object {
            $_.FullName -notlike ((Join-Path $evaluatedRoot '.git') + '\\*') -and
            $_.Extension.ToLowerInvariant() -in @(
                '.ps1', '.py', '.cs', '.xaml', '.exe', '.dll', '.csproj', '.slnx', '.yml', '.yaml'
            )
        } |
        Select-Object -First 1
    if ($null -ne $forbiddenEvaluatedFile) {
        throw 'Evaluated sparse checkout contains a forbidden executable or project file.'
    }

    $validator = Join-Path $controlRoot 'scripts\hardware-inspection\Validate-HardwareInspectionIntelRunnerStage0.ps1'
    $dispatchOutputPath = Join-Path $proofRoot 'dispatch-output.txt'
    $summaryPath = Join-Path $proofRoot 'summary.md'
    $commonArguments = @(
        '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', $validator,
        '-ControlRoot', $controlRoot,
        '-WorkflowRef', 'refs/heads/main',
        '-DefaultBranch', 'main',
        '-Actor', 'arian20020',
        '-TriggeringActor', 'arian20020',
        '-RepositoryOwner', 'arian20020',
        '-RunAttempt', '1',
        '-ConfirmRepositoryOnly', 'true'
    )
    & powershell.exe @commonArguments -Phase Dispatch -GitHubOutputPath $dispatchOutputPath | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Dispatch validator failed in the sparse control checkout.' }
    $expectedDispatch = "source_ref=refs/heads/feature/hardware-inspection`n" +
        "approved_sha=$approvedSha`nrepository_only=true`n"
    if ([System.IO.File]::ReadAllText($dispatchOutputPath) -cne $expectedDispatch) {
        throw 'Dispatch validator emitted unexpected output.'
    }

    & powershell.exe @commonArguments `
        -Phase Source `
        -SourceCheckoutRoot $evaluatedRoot `
        -SummaryPath $summaryPath | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Source validator failed in the sparse evaluated checkout.' }
    $expectedSummary = "# Hardware Inspection Intel runner preflight`n`n" +
        "- Stage 0 only.`n" +
        "- The Intel laptop was not contacted.`n" +
        "- The LLM Fit candidate was not acquired or executed.`n" +
        "- Gate 1 remains Blocked.`n" +
        "- Gate 2 is prohibited.`n" +
        "- Approved source ref: refs/heads/feature/hardware-inspection`n" +
        "- Approved source SHA: $approvedSha`n"
    if ([System.IO.File]::ReadAllText($summaryPath) -cne $expectedSummary) {
        throw 'Source validator emitted an unexpected summary.'
    }
}
finally {
    if (Test-Path -LiteralPath $proofRoot) {
        $resolvedProofRoot = (Resolve-Path -LiteralPath $proofRoot).Path.TrimEnd('\')
        if ((Split-Path -Parent $resolvedProofRoot) -cne $tempParent -or
            (Split-Path -Leaf $resolvedProofRoot) -cne $proofLeaf) {
            throw 'Refusing to remove an unverified sparse-proof path.'
        }
        $reparsePoint = Get-ChildItem -LiteralPath $resolvedProofRoot -Force -Recurse |
            Where-Object { ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 } |
            Select-Object -First 1
        if ($null -ne $reparsePoint) {
            throw 'Refusing to remove a sparse-proof tree containing a reparse point.'
        }
        Remove-Item -LiteralPath $resolvedProofRoot -Recurse -Force
    }
}
if (Test-Path -LiteralPath $proofRoot) { throw 'Owned sparse-proof cleanup failed.' }
```

Expected: the exact 12 contracts pass, the control checkout excludes unrelated long-path/product/candidate trees, the evaluated checkout contains only inert specs at the approved SHA, both validator phases pass with exact safe output, and the owned temporary root is absent afterward.

- [ ] **Step 2: Request two independent reviews**

First request a spec review against the confirmed remediation design and this plan. After spec approval, request a quality/security review focused on sparse-inventory completeness, control/evaluated isolation, Windows-path risk, action pins, credentials, and test robustness. Fix every Critical or Important issue test-first and repeat the corresponding review. Record Minor findings or fix them when narrowly safe.

- [ ] **Step 3: Run final pre-publication verification**

Freshly rerun all Task 1 checks, verify the branch differs from `origin/main` only by the design, plan, workflow, and contract test, and verify both this fix worktree and the frozen feature worktree have no tracked changes. The feature local and remote SHA must still equal `cc2e57ceb94e73e49f34fc383d5440a9047fba21`.

---

### Task 3: Publish, merge, and create a new Stage 0 run

**Files:**
- Publish: branch `fix/hardware-inspection-stage0-sparse-checkout`
- Create: one draft pull request targeting `main`
- Execute after review and authorized merge: `.github/workflows/hardware-inspection-intel-runner-stage0.yml`

**Interfaces:**
- Consumes: reviewed Task 1 commit, Task 2 verification evidence, and exact workflow digest.
- Produces: merged sparse-checkout remediation and one new first-attempt repository-only Stage 0 run.

- [ ] **Step 1: Push without force and open a draft PR**

Run from the clean fix worktree:

```powershell
$fixSha = (& git rev-parse HEAD).Trim()
if (@(& git status --porcelain --untracked-files=all).Count -ne 0) {
    throw 'Fix worktree must be clean before publication.'
}
& git push --set-upstream origin fix/hardware-inspection-stage0-sparse-checkout
if ($LASTEXITCODE -ne 0) { throw 'Fix-branch push failed.' }
$remoteFixSha = ((& git ls-remote --heads origin refs/heads/fix/hardware-inspection-stage0-sparse-checkout) -split '\s+')[0]
if ($remoteFixSha -cne $fixSha) { throw 'Remote fix ref does not match the reviewed head.' }

$prBody = @"
## Summary
- fixes failed repository-only Stage 0 run 32138539513
- requires a fixed Git 2.28+ precheck before checkout and uses the immutable approved SHA for evaluated materialization
- replaces both broad Windows checkouts with reviewed control-cone and evaluated non-cone sparse boundaries
- preserves all Hardware Inspection, Gate 1, Gate 2, laptop, candidate, and network behavior

## Verification
- Stage 0 contracts: 12 passed, 0 failed, 0 skipped
- canonical workflow SHA-256: 81a5893ccde84507ea8fd6d5409811ba55f0d16252b864907c29884f61c8fabf
- local control/evaluated sparse-checkout proof: passed
- PowerShell parser, Python compile, whitespace, scope, and independent reviews: passed

This PR does not contact a self-hosted runner or Intel laptop, acquire or execute the candidate, change network state, or start Gate 1/Gate 2 evaluation.
"@
& gh pr create `
    --repo arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant `
    --base main `
    --head fix/hardware-inspection-stage0-sparse-checkout `
    --draft `
    --title 'fix(hardware-inspection): pin Stage 0 sparse prerequisites' `
    --body $prBody
if ($LASTEXITCODE -ne 0) { throw 'Draft PR creation failed.' }
```

Do not upload TRX, raw evidence, paths, or machine data.

- [ ] **Step 2: Wait for existing PR checks**

Resolve the PR number from its exact head branch, then watch every applicable check:

```powershell
$pr = gh pr view fix/hardware-inspection-stage0-sparse-checkout `
    --repo arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant `
    --json number,headRefOid,isDraft,mergeable,mergeStateStatus | ConvertFrom-Json
if ($pr.headRefOid -cne $fixSha) { throw 'PR head moved after review.' }
gh pr checks $pr.number `
    --repo arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant `
    --watch `
    --fail-fast `
    --interval 10
if ($LASTEXITCODE -ne 0) { throw 'A pull-request check failed.' }
```

Require every applicable check to finish with success or an expected skipped conclusion. Diagnose failures from Actions logs; never bypass, cancel, or mark a failing check successful.

- [ ] **Step 3: Merge only the reviewed immutable head**

Mark the PR ready after reviews, re-read its head and mergeability, and supply the reviewed SHA to GitHub's head-match guard:

```powershell
gh pr ready $pr.number --repo arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant
if ($LASTEXITCODE -ne 0) { throw 'Could not mark the reviewed PR ready.' }
$readyPr = gh pr view $pr.number `
    --repo arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant `
    --json headRefOid,mergeable,mergeStateStatus,isDraft | ConvertFrom-Json
if ($readyPr.headRefOid -cne $fixSha -or
    $readyPr.isDraft -or
    $readyPr.mergeable -cne 'MERGEABLE' -or
    $readyPr.mergeStateStatus -cne 'CLEAN') {
    throw 'Reviewed PR is not in an acceptable immutable merge state.'
}
gh pr merge $pr.number `
    --repo arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant `
    --merge `
    --match-head-commit $fixSha
if ($LASTEXITCODE -ne 0) { throw 'PR merge failed.' }
```

Do not force-push or delete the worktree.

- [ ] **Step 4: Verify the merged `main` tree**

Fetch and verify the merge rather than trusting the PR UI:

```powershell
git fetch origin `
    refs/heads/main:refs/remotes/origin/main `
    refs/heads/feature/hardware-inspection:refs/remotes/origin/feature/hardware-inspection
$mergeSha = (& git rev-parse origin/main).Trim()
$parents = @((& git show -s --format=%P $mergeSha).Trim() -split ' ')
if ($parents.Count -ne 2 -or $parents[1] -cne $fixSha) {
    throw 'Main is not the expected merge of the reviewed fix head.'
}
$fixTree = (& git rev-parse "$fixSha^{tree}").Trim()
$mergeTree = (& git rev-parse "$mergeSha^{tree}").Trim()
if ($fixTree -cne $mergeTree) { throw 'Merged main tree differs from the reviewed fix tree.' }
$featureSha = (& git rev-parse origin/feature/hardware-inspection).Trim()
if ($featureSha -cne 'cc2e57ceb94e73e49f34fc383d5440a9047fba21') {
    throw 'Approved Hardware Inspection feature ref moved.'
}
```

Re-read the merged workflow from `origin/main` and confirm it remains manual-only with the canonical SHA-256 `81a5893ccde84507ea8fd6d5409811ba55f0d16252b864907c29884f61c8fabf`.

- [ ] **Step 5: Create a new manual dispatch**

Record the existing run IDs, then run exactly:

```powershell
$workflowName = 'hardware-inspection-intel-runner-stage0.yml'
$repository = 'arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant'
$beforeRunIds = @(
    gh run list --repo $repository --workflow $workflowName --limit 100 --json databaseId |
        ConvertFrom-Json |
        ForEach-Object { [string]$_.databaseId }
)
gh workflow run hardware-inspection-intel-runner-stage0.yml `
    --repo $repository `
    --ref main `
    -f confirm_repository_only=true
if ($LASTEXITCODE -ne 0) { throw 'New Stage 0 dispatch failed.' }

$deadline = [DateTime]::UtcNow.AddMinutes(2)
do {
    Start-Sleep -Seconds 3
    $newRuns = @(
        gh run list --repo $repository --workflow $workflowName --limit 20 `
            --json databaseId,event,headBranch,headSha,attempt,createdAt,status,conclusion |
            ConvertFrom-Json |
            Where-Object { [string]$_.databaseId -notin $beforeRunIds }
    )
} while ($newRuns.Count -eq 0 -and [DateTime]::UtcNow -lt $deadline)
if ($newRuns.Count -ne 1) { throw 'Could not identify exactly one new Stage 0 run.' }
$newRun = $newRuns[0]
if ($newRun.event -cne 'workflow_dispatch' -or
    $newRun.headBranch -cne 'main' -or
    $newRun.headSha -cne $mergeSha -or
    [int]$newRun.attempt -ne 1 -or
    [string]$newRun.databaseId -eq '32138539513') {
    throw 'New Stage 0 run identity is invalid.'
}
```

Do not use GitHub's rerun operation on `32138539513`.

- [ ] **Step 6: Verify the hosted result**

Wait for the exact new run, fail on a non-success conclusion, and capture its immutable metadata:

```powershell
gh run watch $newRun.databaseId --repo $repository --exit-status --interval 5
if ($LASTEXITCODE -ne 0) { throw 'New Stage 0 run failed.' }
$run = gh api "repos/$repository/actions/runs/$($newRun.databaseId)" | ConvertFrom-Json
$jobsResponse = gh api "repos/$repository/actions/runs/$($newRun.databaseId)/jobs?filter=all&per_page=100" |
    ConvertFrom-Json
$artifactResponse = gh api "repos/$repository/actions/runs/$($newRun.databaseId)/artifacts" |
    ConvertFrom-Json
$jobs = @($jobsResponse.jobs)
if ($run.status -cne 'completed' -or
    $run.conclusion -cne 'success' -or
    [int]$run.run_attempt -ne 1 -or
    $run.event -cne 'workflow_dispatch' -or
    $run.head_branch -cne 'main' -or
    $run.head_sha -cne $mergeSha -or
    $run.actor.login -cne 'arian20020' -or
    $run.triggering_actor.login -cne 'arian20020') {
    throw 'Completed Stage 0 run metadata is invalid.'
}
if ($jobs.Count -ne 1 -or
    $jobs[0].name -cne 'Validate repository-only Intel runner controls' -or
    $jobs[0].conclusion -cne 'success' -or
    @($jobs[0].labels) -contains 'self-hosted') {
    throw 'Stage 0 job identity, outcome, or hosted-runner boundary is invalid.'
}
$expectedSteps = @(
    'Require sparse-checkout-capable Git',
    'Check out default-branch controls',
    'Set up Python for repository contracts',
    'Run Stage 0 contracts',
    'Validate dispatch and approval manifest',
    'Check out approved source for identity comparison only',
    'Confirm approved source identity and publish safe summary'
)
foreach ($stepName in $expectedSteps) {
    $step = @($jobs[0].steps | Where-Object name -CEQ $stepName)
    if ($step.Count -ne 1 -or $step[0].conclusion -cne 'success') {
        throw "Required Stage 0 step did not succeed: $stepName"
    }
}
if ([int]$artifactResponse.total_count -ne 0) {
    throw 'Stage 0 unexpectedly uploaded an artifact.'
}
$workflowText = (Get-Content -Raw '.github/workflows/hardware-inspection-intel-runner-stage0.yml')
if ($workflowText -notmatch "ref: \$\{\{ steps\.approval\.outputs\.approved_sha \}\}") {
    throw 'Evaluated checkout is not pinned to approved_sha.'
}
if ($workflowText -match "ref: \$\{\{ steps\.approval\.outputs\.source_ref \}\}") {
    throw 'Evaluated checkout uses movable source_ref.'
}
$runLog = (& gh run view $newRun.databaseId --repo $repository --log | Out-String)
foreach ($requiredLogFragment in @(
    'Ran 12 tests in',
    'OK',
    '- Stage 0 only.',
    '- The Intel laptop was not contacted.',
    '- The LLM Fit candidate was not acquired or executed.',
    '- Gate 1 remains Blocked.',
    '- Gate 2 is prohibited.'
)) {
    if (-not $runLog.Contains($requiredLogFragment)) {
        throw "Stage 0 log lacks required proof: $requiredLogFragment"
    }
}
```

Require:

- status `completed`, conclusion `success`, attempt `1`;
- actor and triggering actor `arian20020`;
- exactly one `hosted-preflight` job on `windows-latest`;
- fixed Git `2.28.0+` precheck passes before any checkout;
- both sparse checkout steps pass;
- exactly 12 Stage 0 contracts pass;
- Dispatch and Source validator phases pass;
- summary says Stage 0 only, laptop not contacted, candidate not acquired/executed, Gate 1 Blocked, and Gate 2 prohibited;
- artifact count `0` and no self-hosted job.

If the new run fails, stop, retain its exact logs, and return to root-cause analysis. Do not expand into Stage A, B, C, or D.

- [ ] **Step 7: Final handoff**

Report the remediation PR, merge SHA, successful new run URL, exact 12-test result, Git precheck matrix, immutable approved-SHA checkout proof, hashes, zero-artifact/self-hosted proof, and the unchanged Gate 1/Gate 2 boundary. Preserve the fix worktree and branches for audit; do not remove candidate or user artifacts.
