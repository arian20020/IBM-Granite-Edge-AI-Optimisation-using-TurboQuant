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

No evaluated code, project, executable, workflow step, or test runs. The corrected canonical workflow SHA-256 is `db075979900b0e5ca5aef3588f64225221f91bba744018f20180470177061ab3`.

## Security verification erratum — 2026-08-18

Follow-up verification found that the pinned `actions/checkout` action can fall back to a full REST archive when Git is missing or too old to support sparse checkout. The fixed first executable step now performs a privacy-safe Git capability precheck before either checkout, accepts only a single strict version line at Git `2.28.0` or newer, and fails with a fixed message without exposing command output or paths. This prevents REST fallback from materializing a broad worktree before the sparse boundary is established.

The evaluated checkout now resolves the immutable `steps.approval.outputs.approved_sha` value, not the movable `steps.approval.outputs.source_ref`. `source_ref` remains validator output and summary provenance only. The precheck and immutable SHA pin prevent both broad REST fallback materialization and movable-ref pre-materialization. The corrected canonical workflow SHA-256 is `db075979900b0e5ca5aef3588f64225221f91bba744018f20180470177061ab3`.

## Git command-resolution follow-up — 2026-08-18

Hosted attempt-1 run `32155463873` failed in the first capability gate with the fixed privacy-safe Git error even though the official Windows image Git version `2.55.0.windows.3` satisfies the strict version expression. Reproduction showed that `Get-Command git -CommandType Application` can return multiple PATH-visible Git applications, including `bin` and `cmd`, and that invoking the array's `.Source` value treats all returned paths as one command name.

The narrow correction selects the first PATH-ordered application with `Select-Object -First 1`, matching the pinned checkout bundle's `which('git', true)` then `matches[0]` resolution. Existing test identities now cover valid-first/invalid-second and invalid-first/valid-second results, retain the fixed error and no-output privacy boundary, and bind removal of first-result selection through the canonical mutation set. The suite remains exactly 12 identities and the canonical workflow SHA-256 is `db075979900b0e5ca5aef3588f64225221f91bba744018f20180470177061ab3`.

This follow-up is isolated in `C:\hardware-inspection-stage0-git-fix` on `fix/hardware-inspection-stage0-git-resolution`, based on failed revision `b985ec7d9fe11aedd83afa9ba657699ed115a19f`. Every executable instruction below is bound to that worktree, branch, and base; preceding delivery history remains available in Git rather than in stale executable commands.

## Source cleanliness validation follow-up — 2026-08-18

Hosted attempt-1 run `32162980959` at `12c0ff1ef322d2ed455fcab9c7cfa76185432884` passed the repository contracts and reached Source validation, then stopped through the validator's fixed generic error before summary publication. The confirmed reproduction is a blobless non-cone sparse checkout with a missing root `.gitignore` promisor object and an unavailable origin. The old `git status --porcelain --untracked-files=all` attempts to lazy-fetch the missing ignore blob and therefore rejects an otherwise clean evaluated checkout.

The narrow correction uses `git status --porcelain --untracked-files=no` for staged and tracked worktree dirt, followed by `git ls-files --others --` without exclude flags for every untracked path, including normally ignored paths. The explicit `--` closes option parsing. Both probes are network-free in the exact reproduction and work at the existing Git `2.28.0+` floor. Do not use `GIT_NO_LAZY_FETCH`: that environment control was introduced in Git 2.45 and would silently weaken the declared compatibility floor.

This follow-up is isolated in `C:\hardware-inspection-stage0-source-fix` on `fix/hardware-inspection-stage0-source-validation`, based exactly on failed revision `12c0ff1ef322d2ed455fcab9c7cfa76185432884`. Task 4 is the only current executable task; Tasks 1 through 3 below remain the completed Git-resolution delivery record and must not be rerun. The current task stops after a verified local commit: no push, pull request, workflow dispatch, or failed-run rerun is authorized.

---

## Global Constraints

- For Task 4, work only in `C:\hardware-inspection-stage0-source-fix` on branch `fix/hardware-inspection-stage0-source-validation`, based exactly on failed `main` revision `12c0ff1ef322d2ed455fcab9c7cfa76185432884`. Earlier worktree bindings are historical only.
- Do not modify Hardware Inspection UI/runtime, LLM Fit candidate logic, evidence, Gate 1 records, Gate 2, Model Inspection, the Intel laptop, self-hosted runners, adapters, or network state.
- Keep the workflow manual-only, `windows-latest`, owner-only, default-branch-only, first-attempt-only, read-only, and artifact-free.
- Keep both immutable action pins and both `persist-credentials: false` settings.
- Execute code only from `control`; never execute content from `evaluated`.
- Keep exactly 12 Stage 0 contract-test identities.
- Keep `.github/workflows/hardware-inspection-intel-runner-stage0.yml` byte-for-byte unchanged, including both immutable action pins and both `persist-credentials: false` settings.
- Limit the aggregate Task 4 range to the validator, the existing Stage 0 contract module, this plan, and its design.
- Use `apply_patch` for repository edits. Preserve unrelated and ignored files.
- Use UTF-8 without BOM and LF-only YAML with exactly one trailing LF.
- Never rerun failed runs `32138539513`, `32155463873`, or `32162980959`; Task 4 performs no workflow dispatch.

### Task 1: Lock and implement deterministic Stage 0 Git resolution

**Files:**
- Modify: `tests/testing/hardware_inspection/test_intel_runner_stage0_contract.py`
- Modify: `.github/workflows/hardware-inspection-intel-runner-stage0.yml`

**Interfaces:**
- Consumes: existing canonical workflow bytes, the pinned checkout action, the approval manifest output `steps.approval.outputs.approved_sha` for immutable evaluated materialization, `source_ref` as provenance only, and the existing 12-test contract module.
- Produces: canonical workflow SHA-256 `db075979900b0e5ca5aef3588f64225221f91bba744018f20180470177061ab3`, deterministic first-PATH-application resolution in the existing Git precheck, and unchanged control-cone/evaluated non-cone sparse-checkout blocks.

- [ ] **Step 1: Add the focused failing contract assertions**

In `test_stage0_workflow_pins_actions_and_drops_checkout_credentials`, keep all current assertions, require the exact first Git precheck below, execute its extracted run block under PowerShell 5.1 with deterministic local stubs, and add exact expected checkout fragments. The same identity must prove that the first PATH-ordered application is used when two applications are returned, including valid-first/invalid-second and invalid-first/valid-second cases, with the fixed privacy-safe failure retained:

```yaml
      - name: Require sparse-checkout-capable Git
        shell: powershell
        run: |
          $ProgressPreference = 'SilentlyContinue'
          $ErrorActionPreference = 'Stop'
          $failure = 'HI-RUNNER-STAGE0-GIT-INVALID: required Git capability is unavailable.'
          try {
            $gitCommand = Get-Command git -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
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

The semantic matrix accepts `2.28.0`, `2.51.0`, `2.51.0.windows.2`, the hosted-image form `2.55.0.windows.3`, and `3.0.0`; it rejects `2.27.99`, two-component, uppercase/malformed, multiline, nonzero, throwing, and missing cases with empty stdout and exact `HI-RUNNER-STAGE0-GIT-INVALID: required Git capability is unavailable.\n` stderr. A valid first application plus invalid second application must pass, while an invalid first application plus valid second application must fail with the same fixed output. A malicious `C:\private\SECRET_TOKEN` source must not appear in output. The precheck contains no `${{`, absolute path, network, or candidate command.

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
  tests.testing.hardware_inspection.test_intel_runner_stage0_contract.IntelRunnerStage0ContractTests.test_stage0_workflow_exposes_only_manual_trigger_and_hosted_runner `
  -v
```

The base revision already contains the first Git gate, both sparse boundaries, and immutable `approved_sha` materialization. Keep `EXPECTED_WORKFLOW_SHA256` at the pre-follow-up value `81a5893ccde84507ea8fd6d5409811ba55f0d16252b864907c29884f61c8fabf` during RED. Add the ordered two-application stub cases and the mutation that removes ` | Select-Object -First 1` before editing the workflow. The action-pin identity must fail because valid-first/invalid-second returns exit `1`, while the canonical-mutation identity must fail because removing the still-missing selection is a no-op and leaves the pre-follow-up digest unchanged. Import, syntax, harness, precheck-presence, sparse-boundary, and approved-SHA assertions must not be the RED cause.

- [ ] **Step 3: Select the first PATH-ordered Git application**

Change only the existing precheck's command-resolution assignment to append `| Select-Object -First 1`. Keep the precheck as the first executable step with no preceding `uses:` step. Do not change the control checkout's ref, path, depth, credentials, action pin, sparse cones, or step order:

```powershell
$gitCommand = Get-Command git -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
```

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

- [ ] **Step 4: Preserve the evaluated sparse checkout at the approved SHA**

Verify `Check out approved source for identity comparison only` remains byte-for-byte unchanged. Do not change its approved SHA ref, path, depth, credential, action pin, non-cone pattern, or step order. Keep `source_ref` only in validator provenance output and summary metadata:

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
EXPECTED_WORKFLOW_SHA256 = "db075979900b0e5ca5aef3588f64225221f91bba744018f20180470177061ab3"
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
git diff --check b985ec7d9fe11aedd83afa9ba657699ed115a19f...HEAD
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
    'db075979900b0e5ca5aef3588f64225221f91bba744018f20180470177061ab3') {
    throw 'Workflow digest mismatch.'
}
if ((Get-Item $workflow).Length -ne 5965) { throw 'Workflow byte length mismatch.' }

$actualPaths = @(
    git diff --name-only b985ec7d9fe11aedd83afa9ba657699ed115a19f...HEAD
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

Expected aggregate follow-up scope from `b985ec7d9fe11aedd83afa9ba657699ed115a19f`: exactly the workflow, contract test, remediation design, and remediation plan. No candidate, operational, UI, evidence, or runbook file changes.

- [ ] **Step 8: Commit the tested implementation**

```powershell
git add -- `
  .github/workflows/hardware-inspection-intel-runner-stage0.yml `
  docs/superpowers/plans/2026-08-18-hardware-inspection-stage0-sparse-checkout-remediation.md `
  docs/superpowers/specs/2026-08-18-hardware-inspection-stage0-sparse-checkout-remediation-design.md `
  tests/testing/hardware_inspection/test_intel_runner_stage0_contract.py
git diff --cached --check
git commit -m "fix(hardware-inspection): select first Stage 0 Git application"
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

Before the first temporary clone, execute the exact first-step Git precheck under PowerShell 5.1 and stop if it does not accept the single strict version line. After the control sparse clone and 12-test proof, run the Dispatch validator, parse its exact three-line LF output, capture and validate `approved_sha`, and compare it to the pinned expected feature SHA before creating the evaluated clone. Use `source_ref` only when checking validator provenance output, never as an evaluated checkout ref. These prerequisites ensure a missing/old Git cannot trigger REST archive fallback and that evaluated materialization cannot follow a movable ref.

Run this as one PowerShell command so the randomly generated ownership path never has to be inferred by a later shell. It clones only into a new GUID-named direct child of the OS temp directory and deletes only that validated child in `finally`:

```powershell
$ErrorActionPreference = 'Stop'
$followUpRoot = 'C:\hardware-inspection-stage0-git-fix'
$followUpBranch = 'fix/hardware-inspection-stage0-git-resolution'
$followUpBaseSha = 'b985ec7d9fe11aedd83afa9ba657699ed115a19f'
$actualBranch = (& git -C $followUpRoot branch --show-current).Trim()
$reviewedFollowUpHead = (& git -C $followUpRoot rev-parse HEAD).Trim()
$actualMergeBase = (& git -C $followUpRoot merge-base $followUpBaseSha $reviewedFollowUpHead).Trim()
$followUpStatus = @(& git -C $followUpRoot status --porcelain --untracked-files=all)
if ($actualBranch -cne $followUpBranch -or
    $reviewedFollowUpHead -cnotmatch '\A(?!0{40}\z)[0-9a-f]{40}\z' -or
    $actualMergeBase -cne $followUpBaseSha -or
    $followUpStatus.Count -ne 0) {
    throw 'Reviewed Git-resolution follow-up identity or worktree state is invalid.'
}
$tempParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\')
$proofLeaf = 'GraniteEdgeAI-Stage0-Sparse-Proof-' + [Guid]::NewGuid().ToString('N')
$proofRoot = Join-Path $tempParent $proofLeaf
$controlRoot = Join-Path $proofRoot 'control'
$evaluatedRoot = Join-Path $proofRoot 'evaluated'
$expectedFeatureSha = 'cc2e57ceb94e73e49f34fc383d5440a9047fba21'

if (Test-Path -LiteralPath $proofRoot) { throw 'Owned sparse-proof root already exists.' }

try {
    $gitFailure = 'HI-RUNNER-STAGE0-GIT-INVALID: required Git capability is unavailable.'
    try {
        $gitCommand = Get-Command git -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
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
        [Console]::Error.WriteLine($gitFailure)
        exit 1
    }

    New-Item -ItemType Directory -Path $proofRoot -ErrorAction Stop | Out-Null
    $resolvedProofRoot = (Resolve-Path -LiteralPath $proofRoot).Path.TrimEnd('\')
    if ((Split-Path -Parent $resolvedProofRoot) -cne $tempParent -or
        (Split-Path -Leaf $resolvedProofRoot) -cne $proofLeaf) {
        throw 'Owned sparse-proof root escaped the OS temp directory.'
    }

    & git clone --no-checkout --no-local $followUpRoot $controlRoot
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
    $implementationSha = $reviewedFollowUpHead
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
        $contractOutput = ''
        $contractExitCode = -1
        $captureFailed = $false
        $savedErrorActionPreference = $ErrorActionPreference
        try {
            $ErrorActionPreference = 'Continue'
            $contractOutput = (& python -m unittest `
                tests.testing.hardware_inspection.test_intel_runner_stage0_contract `
                -v 2>&1 | Out-String)
            $contractExitCode = $LASTEXITCODE
        }
        catch {
            $captureFailed = $true
        }
        finally {
            $ErrorActionPreference = $savedErrorActionPreference
        }

        $regexOptions = [System.Text.RegularExpressions.RegexOptions]::CultureInvariant
        $ranCount = [System.Text.RegularExpressions.Regex]::Matches(
            $contractOutput,
            '(?m)^Ran 12 tests in [0-9]+(?:\.[0-9]+)?s\r?$',
            $regexOptions
        ).Count
        $okCount = [System.Text.RegularExpressions.Regex]::Matches(
            $contractOutput,
            '(?m)^OK\r?$',
            $regexOptions
        ).Count
        $failureMarkerCount = [System.Text.RegularExpressions.Regex]::Matches(
            $contractOutput,
            '(?m)(?:^FAILED\b|^FAIL:|^ERROR:|^OK \(|\.\.\. skipped\b)',
            $regexOptions
        ).Count
        if ($captureFailed -or
            $contractExitCode -ne 0 -or
            $ranCount -ne 1 -or
            $okCount -ne 1 -or
            $failureMarkerCount -ne 0) {
            throw 'Sparse control contracts were not exactly 12 passing tests.'
        }
    }
    finally {
        Pop-Location
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
    $dispatchText = [System.IO.File]::ReadAllText($dispatchOutputPath)
    $dispatchMatch = [regex]::Match(
        $dispatchText,
        '\Asource_ref=refs/heads/feature/hardware-inspection\napproved_sha=(?<approved_sha>(?!0{40}\n)[0-9a-f]{40})\nrepository_only=true\n\z',
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant
    )
    if (-not $dispatchMatch.Success) {
        throw 'Dispatch validator emitted unexpected output.'
    }
    $approvedSha = $dispatchMatch.Groups['approved_sha'].Value
    if ($approvedSha -cne $expectedFeatureSha) {
        throw 'Dispatch approved SHA differs from the pinned feature SHA.'
    }
    $expectedDispatch = "source_ref=refs/heads/feature/hardware-inspection`n" +
        "approved_sha=$approvedSha`nrepository_only=true`n"
    if ($dispatchText -cne $expectedDispatch) {
        throw 'Dispatch validator emitted unexpected output.'
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

Freshly rerun all Task 1 checks from `C:\hardware-inspection-stage0-git-fix`. Require branch `fix/hardware-inspection-stage0-git-resolution`, require merge-base `b985ec7d9fe11aedd83afa9ba657699ed115a19f`, capture the full 40-hex current HEAD as the reviewed follow-up head, and verify the range from that base contains only the design, plan, workflow, and contract test. Verify both this follow-up worktree and the frozen feature worktree have no tracked changes. The feature local and remote SHA must still equal `cc2e57ceb94e73e49f34fc383d5440a9047fba21`.

```powershell
$followUpRoot = 'C:\hardware-inspection-stage0-git-fix'
$followUpBranch = 'fix/hardware-inspection-stage0-git-resolution'
$followUpBaseSha = 'b985ec7d9fe11aedd83afa9ba657699ed115a19f'
$reviewedFollowUpHead = (& git -C $followUpRoot rev-parse HEAD).Trim()
$actualMergeBase = (& git -C $followUpRoot merge-base $followUpBaseSha $reviewedFollowUpHead).Trim()
$actualBranch = (& git -C $followUpRoot branch --show-current).Trim()
$expectedPaths = @(
    '.github/workflows/hardware-inspection-intel-runner-stage0.yml'
    'docs/superpowers/plans/2026-08-18-hardware-inspection-stage0-sparse-checkout-remediation.md'
    'docs/superpowers/specs/2026-08-18-hardware-inspection-stage0-sparse-checkout-remediation-design.md'
    'tests/testing/hardware_inspection/test_intel_runner_stage0_contract.py'
) | Sort-Object
$range = "$followUpBaseSha..$reviewedFollowUpHead"
$actualPaths = @(& git -C $followUpRoot diff --name-only $range) | Sort-Object
if ($reviewedFollowUpHead -cnotmatch '\A(?!0{40}\z)[0-9a-f]{40}\z' -or
    $actualMergeBase -cne $followUpBaseSha -or
    $actualBranch -cne $followUpBranch -or
    (Compare-Object -ReferenceObject $expectedPaths -DifferenceObject $actualPaths) -or
    @(& git -C $followUpRoot status --porcelain --untracked-files=all).Count -ne 0) {
    throw 'Pre-publication follow-up identity, scope, branch, or cleanliness check failed.'
}

$expectedFeatureSha = 'cc2e57ceb94e73e49f34fc383d5440a9047fba21'
$localFeatureSha = (& git -C 'C:\hardware-inspection' rev-parse HEAD).Trim()
$remoteFeatureSha = ((& git -C $followUpRoot ls-remote origin `
    refs/heads/feature/hardware-inspection) -split '\s+')[0]
if ($localFeatureSha -cne $expectedFeatureSha -or
    $remoteFeatureSha -cne $expectedFeatureSha -or
    @(& git -C 'C:\hardware-inspection' status --porcelain --untracked-files=all).Count -ne 0) {
    throw 'Frozen Hardware Inspection feature identity or cleanliness check failed.'
}
```

---

### Task 3: Publish, merge, and create a new Stage 0 run

**Files:**
- Publish: branch `fix/hardware-inspection-stage0-git-resolution`
- Create: one draft pull request targeting `main`
- Execute after review and authorized merge: `.github/workflows/hardware-inspection-intel-runner-stage0.yml`

**Interfaces:**
- Consumes: reviewed Git-resolution follow-up head, Task 2 verification evidence, and exact workflow digest.
- Produces: merged Git-resolution follow-up and one new first-attempt repository-only Stage 0 run.

- [ ] **Step 1: Push without force and open a draft PR**

Run from the clean Git-resolution follow-up worktree:

```powershell
$followUpRoot = 'C:\hardware-inspection-stage0-git-fix'
$followUpBranch = 'fix/hardware-inspection-stage0-git-resolution'
$followUpBaseSha = 'b985ec7d9fe11aedd83afa9ba657699ed115a19f'
if ((& git rev-parse --show-toplevel).Trim().Replace('\', '/') -cne
    $followUpRoot.Replace('\', '/')) {
    throw 'Publication is not running from the Git-resolution follow-up worktree.'
}
if ((& git branch --show-current).Trim() -cne $followUpBranch) {
    throw 'Publication is not running from the Git-resolution follow-up branch.'
}
$reviewedFollowUpHead = (& git rev-parse HEAD).Trim()
$actualMergeBase = (& git merge-base $followUpBaseSha $reviewedFollowUpHead).Trim()
if ($reviewedFollowUpHead -cnotmatch '\A(?!0{40}\z)[0-9a-f]{40}\z' -or
    $actualMergeBase -cne $followUpBaseSha) {
    throw 'Reviewed Git-resolution follow-up head or base is invalid.'
}
if (@(& git status --porcelain --untracked-files=all).Count -ne 0) {
    throw 'Follow-up worktree must be clean before publication.'
}
& git push --set-upstream origin $followUpBranch
if ($LASTEXITCODE -ne 0) { throw 'Follow-up branch push failed.' }
$remoteFollowUpHead = ((& git ls-remote --heads origin "refs/heads/$followUpBranch") -split '\s+')[0]
if ($remoteFollowUpHead -cne $reviewedFollowUpHead) {
    throw 'Remote follow-up ref does not match the reviewed head.'
}

$prBody = @"
## Summary
- fixes the first Git capability gate failure in attempt-1 run 32155463873
- selects the first PATH-ordered Git application, matching the pinned checkout action, while retaining strict Git 2.28+ and privacy-safe failure handling
- preserves the sparse checkout and immutable approved-SHA corrections introduced after failed run 32138539513
- preserves all Hardware Inspection, Gate 1, Gate 2, laptop, candidate, and network behavior

## Verification
- Stage 0 contracts: 12 passed, 0 failed, 0 skipped
- real and ordered multi-application PowerShell 5.1 Git matrices: passed
- canonical workflow SHA-256: db075979900b0e5ca5aef3588f64225221f91bba744018f20180470177061ab3
- local control/evaluated sparse-checkout proof: passed
- PowerShell parser, Python compile, whitespace, scope, and independent reviews: passed

This PR does not contact a self-hosted runner or Intel laptop, acquire or execute the candidate, change network state, or start Gate 1/Gate 2 evaluation.
"@
& gh pr create `
    --repo arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant `
    --base main `
    --head $followUpBranch `
    --draft `
    --title 'fix(hardware-inspection): select first Stage 0 Git application' `
    --body $prBody
if ($LASTEXITCODE -ne 0) { throw 'Draft PR creation failed.' }
```

Do not upload TRX, raw evidence, paths, or machine data.

- [ ] **Step 2: Wait for existing PR checks**

Resolve the PR number from its exact head branch, then watch every applicable check:

```powershell
$pr = gh pr view $followUpBranch `
    --repo arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant `
    --json number,headRefOid,isDraft,mergeable,mergeStateStatus | ConvertFrom-Json
if ($pr.headRefOid -cne $reviewedFollowUpHead) { throw 'PR head moved after review.' }
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
if ($readyPr.headRefOid -cne $reviewedFollowUpHead -or
    $readyPr.isDraft -or
    $readyPr.mergeable -cne 'MERGEABLE' -or
    $readyPr.mergeStateStatus -cne 'CLEAN') {
    throw 'Reviewed PR is not in an acceptable immutable merge state.'
}
gh pr merge $pr.number `
    --repo arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant `
    --merge `
    --match-head-commit $reviewedFollowUpHead
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
if ($parents.Count -ne 2 -or $parents[1] -cne $reviewedFollowUpHead) {
    throw 'Main is not the expected merge of the reviewed follow-up head.'
}
$followUpTree = (& git rev-parse "$reviewedFollowUpHead^{tree}").Trim()
$mergeTree = (& git rev-parse "$mergeSha^{tree}").Trim()
if ($followUpTree -cne $mergeTree) { throw 'Merged main tree differs from the reviewed follow-up tree.' }
$featureSha = (& git rev-parse origin/feature/hardware-inspection).Trim()
if ($featureSha -cne 'cc2e57ceb94e73e49f34fc383d5440a9047fba21') {
    throw 'Approved Hardware Inspection feature ref moved.'
}
```

Re-read the merged workflow from `origin/main` and confirm it remains manual-only with the canonical SHA-256 `db075979900b0e5ca5aef3588f64225221f91bba744018f20180470177061ab3`.

- [ ] **Step 5: Create a new manual dispatch**

Record the existing run IDs, then run exactly:

```powershell
$workflowName = 'hardware-inspection-intel-runner-stage0.yml'
$repository = 'arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant'
$existingRuns = gh run list --repo $repository --workflow $workflowName --limit 100 --json databaseId |
    ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw 'Existing Stage 0 run listing failed.' }
$beforeRunIds = @($existingRuns | ForEach-Object { [string]$_.databaseId })
gh workflow run hardware-inspection-intel-runner-stage0.yml `
    --repo $repository `
    --ref main `
    -f confirm_repository_only=true
if ($LASTEXITCODE -ne 0) { throw 'New Stage 0 dispatch failed.' }

$deadline = [DateTime]::UtcNow.AddMinutes(2)
do {
    Start-Sleep -Seconds 3
    $listedRuns = gh run list --repo $repository --workflow $workflowName --limit 20 `
        --json databaseId,event,headBranch,headSha,attempt,createdAt,status,conclusion |
        ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) { throw 'New Stage 0 run listing failed.' }
    $newRuns = @(
        $listedRuns | Where-Object {
            [string]$_.databaseId -notin $beforeRunIds -and
            $_.event -ceq 'workflow_dispatch' -and
            $_.headBranch -ceq 'main' -and
            $_.headSha -ceq $mergeSha -and
            [int]$_.attempt -eq 1 -and
            [string]$_.databaseId -notin @('32138539513', '32155463873')
        }
    )
} while ($newRuns.Count -eq 0 -and [DateTime]::UtcNow -lt $deadline)
if ($newRuns.Count -ne 1) { throw 'Could not identify exactly one new Stage 0 run.' }
$newRun = $newRuns[0]
if ($newRun.event -cne 'workflow_dispatch' -or
    $newRun.headBranch -cne 'main' -or
    $newRun.headSha -cne $mergeSha -or
    [int]$newRun.attempt -ne 1 -or
    [string]$newRun.databaseId -in @('32138539513', '32155463873')) {
    throw 'New Stage 0 run identity is invalid.'
}
```

Do not use GitHub's rerun operation on `32138539513` or `32155463873`.

- [ ] **Step 6: Verify the hosted result**

Wait for the exact new run, fail on a non-success conclusion, and capture its immutable metadata:

```powershell
gh run watch $newRun.databaseId --repo $repository --exit-status --interval 5
if ($LASTEXITCODE -ne 0) { throw 'New Stage 0 run failed.' }
$run = gh api "repos/$repository/actions/runs/$($newRun.databaseId)" | ConvertFrom-Json
$jobsResponse = gh api "repos/$repository/actions/runs/$($newRun.databaseId)/jobs?filter=all&per_page=100" |
    ConvertFrom-Json
$artifactFailure = 'HI-RUNNER-STAGE0-ARTIFACT-INVALID: artifact verification failed.'
function Stop-Stage0ArtifactVerification {
    [Console]::Error.WriteLine($artifactFailure)
    exit 1
}
$artifactApiLines = @(
    & gh api "repos/$repository/actions/runs/$($newRun.databaseId)/artifacts" 2>$null
)
$artifactApiExitCode = $LASTEXITCODE
if ($artifactApiExitCode -ne 0) {
    Stop-Stage0ArtifactVerification
}
$artifactJson = $artifactApiLines -join "`n"
$artifactResponse = $null
try {
    $artifactResponse = $artifactJson | ConvertFrom-Json -ErrorAction Stop
}
catch {
    Stop-Stage0ArtifactVerification
}
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
$totalCountProperties = @(
    $artifactResponse.PSObject.Properties |
        Where-Object { $_.Name -ceq 'total_count' }
)
$artifactInventoryProperties = @(
    $artifactResponse.PSObject.Properties |
        Where-Object { $_.Name -ceq 'artifacts' }
)
if ($null -eq $artifactResponse -or
    $totalCountProperties.Count -ne 1 -or
    $artifactInventoryProperties.Count -ne 1) {
    Stop-Stage0ArtifactVerification
}
$totalCountValue = $totalCountProperties[0].Value
$integralTypeCodes = @(
    [System.TypeCode]::SByte,
    [System.TypeCode]::Byte,
    [System.TypeCode]::Int16,
    [System.TypeCode]::UInt16,
    [System.TypeCode]::Int32,
    [System.TypeCode]::UInt32,
    [System.TypeCode]::Int64,
    [System.TypeCode]::UInt64
)
if ($null -eq $totalCountValue -or
    [System.Type]::GetTypeCode($totalCountValue.GetType()) -notin $integralTypeCodes) {
    Stop-Stage0ArtifactVerification
}
$artifactTotalCount = [decimal]$totalCountValue
$artifactInventory = $artifactInventoryProperties[0].Value
if ($artifactTotalCount -lt 0 -or
    $null -eq $artifactInventory -or
    $artifactInventory -isnot [System.Array] -or
    [decimal]@($artifactInventory).Count -ne $artifactTotalCount -or
    $artifactTotalCount -ne 0) {
    Stop-Stage0ArtifactVerification
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
- artifact API exit `0`, valid JSON, exactly one integral nonnegative `total_count` equal to `0`, and exactly one empty `artifacts` array;
- every artifact API, parse, schema, type, range, inventory, or nonzero-count failure emits only `HI-RUNNER-STAGE0-ARTIFACT-INVALID: artifact verification failed.`;
- no self-hosted job.

If the new run fails, stop, retain its exact logs, and return to root-cause analysis. Do not expand into Stage A, B, C, or D.

- [ ] **Step 7: Final handoff**

Report the Git-resolution remediation PR, merge SHA, successful new run URL, exact 12-test result, Git precheck matrix, immutable approved-SHA checkout proof, hashes, zero-artifact/self-hosted proof, and the unchanged Gate 1/Gate 2 boundary. Preserve the follow-up worktree and branch for audit; do not remove candidate or user artifacts.

---

### Task 4: Make Source cleanliness validation offline-safe

**Files:**
- Modify: `scripts/hardware-inspection/Validate-HardwareInspectionIntelRunnerStage0.ps1`
- Modify: `tests/testing/hardware_inspection/test_intel_runner_stage0_contract.py`
- Modify: `docs/superpowers/specs/2026-08-18-hardware-inspection-stage0-sparse-checkout-remediation-design.md`
- Modify: `docs/superpowers/plans/2026-08-18-hardware-inspection-stage0-sparse-checkout-remediation.md`

**Interfaces:**
- Consumes: the existing Source phase, approved immutable SHA, blobless evaluated sparse checkout, fixed generic stderr, and Git `2.28.0+` floor.
- Produces: network-free clean-source validation that still rejects staged, tracked, ordinary untracked, and normally ignored dirt, with exactly 12 test identities and no workflow-byte change.

- [ ] **Step 1: Reproduce RED inside the existing Source-validator identity**

Build a local origin containing a root `.gitignore` and one inert evaluated specification. Enable partial-clone filtering, create a blobless no-cone clone bounded to `/docs/superpowers/specs/`, prove the `.gitignore` blob is still missing with `git rev-list --objects --missing=print`, then rename the origin so it is unreachable. Prove the old `git status --porcelain --untracked-files=all` exits nonzero and observe the existing validator identity fail because the clean Source phase emits only `HI-RUNNER-STAGE0-INVALID: repository-only validation failed.`

- [ ] **Step 2: Implement the two-probe correction and reach GREEN**

Replace only the old Source cleanliness invocation with these ordered fail-closed probes:

```powershell
    $statusLines = @(& git -C $SourceCheckoutRoot status --porcelain --untracked-files=no 2>$null)
    if ($LASTEXITCODE -ne 0 -or ($statusLines -join [char]10).Length -ne 0) {
        throw 'Source checkout is not clean.'
    }
    $untrackedLines = @(& git -C $SourceCheckoutRoot ls-files --others -- 2>$null)
    if ($LASTEXITCODE -ne 0 -or ($untrackedLines -join [char]10).Length -ne 0) {
        throw 'Source checkout is not clean.'
    }
```

Do not add exclude flags or `GIT_NO_LAZY_FETCH`. Extend the same test identity to require the exact ordered probes, forbid the old invocation and incompatible environment control, accept the clean offline fixture, and reject tracked, ordinary untracked, and normally ignored dirt through the unchanged generic stderr.

- [ ] **Step 3: Verify and commit locally**

Run the exact 12-test module, Python compilation, Windows PowerShell 5.1 parser, relevant focused regression, workflow digest comparison against base, test-identity count, UTF-8/no-BOM/LF checks, `git diff --check`, and an exact four-path range/scope check. Require the branch/base identities above and a clean worktree after committing with a precise Source-validation message. Do not push, create a pull request, dispatch, contact the Intel laptop, acquire or execute the candidate, change adapters or network state, or advance Gate 1/Gate 2.
