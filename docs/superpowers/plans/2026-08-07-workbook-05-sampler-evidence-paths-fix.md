# Workbook 05 Sampler and Evidence-Path Repair Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Repair the three deterministic Workbook 05 orchestration defects exposed by Route A Runtime run `31195528209` without changing OpenVINO source/build controls or weakening independent evidence validation.

**Architecture:** Keep the existing shared `Workbook05.Build.psm1` process adapter and hosted validator contracts. Make the sampler end normally when the sampled process disappears between observations, make command-log references explicitly bundle-root relative, and apply the already-proven null-safe empty-stdout contract to the two later orchestrators that still duplicate the unsafe helper.

**Tech Stack:** Windows PowerShell 5.1, Python 3.12 `unittest`, GitHub Actions, existing Workbook 05 PowerShell/Python build/evidence tooling.

## Global Constraints

- Runtime source stays `openvinotoolkit/openvino@b9a1f201c109e0bed74763934f79483cf6c4cbf4`.
- GenAI source stays `openvinotoolkit/openvino.genai@05e5c7670b597746f858946974d11f38e3baf42f`.
- Route B source and BR8 prerequisite/owner-acceptance rules do not change.
- CMake generator, CPU-only flags, `--parallel 2`, short workspace roots, install roots, and tool pins do not change.
- GitHub runner labels/timeouts, read-only permissions, immutable action SHAs, and text/data-only artifact boundaries do not change.
- The hosted artifact validator remains fail-closed and unchanged.
- No model execution, QJL/PolarQuant/TurboQuant activation, packed-storage, no-fallback, memory/context, TTFT/tok/s, or quality claim is authorised by this repair.
- New/changed code and tests must include beginner-readable comments around each logical block.

---

### Task 1: Add RED regressions for the three proven defects

**Files:**
- Create: `tests/testing/workbook05/test_build_sampler_evidence_path_integrity.py`

**Interfaces:**
- Consumes: committed text of `scripts/testing/workbook05/Workbook05.Build.psm1`, `Invoke-Workbook05RouteARuntimeBuild.ps1`, `Invoke-Workbook05RouteAGenAIBuild.ps1`, and `Invoke-Workbook05RouteBBuild.ps1`.
- Produces: three deterministic regression tests that fail on current `main`/branch production code and turn green only when the approved repair is present.

- [ ] **Step 1: Create the focused regression test file**

```python
from __future__ import annotations

import re
import unittest
from pathlib import Path


# Resolve the repository files whose exact ordering/contracts failed in the live
# Windows run. These are static integrity tests because the process-exit race is
# nondeterministic; the later Lenovo rerun remains the behavioral proof.
REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
MODULE_TEXT = (
    REPOSITORY_ROOT / "scripts/testing/workbook05/Workbook05.Build.psm1"
).read_text(encoding="utf-8", errors="strict")
RUNTIME_TEXT = (
    REPOSITORY_ROOT
    / "scripts/testing/workbook05/Invoke-Workbook05RouteARuntimeBuild.ps1"
).read_text(encoding="utf-8", errors="strict")
GENAI_TEXT = (
    REPOSITORY_ROOT
    / "scripts/testing/workbook05/Invoke-Workbook05RouteAGenAIBuild.ps1"
).read_text(encoding="utf-8", errors="strict")
ROUTE_B_TEXT = (
    REPOSITORY_ROOT
    / "scripts/testing/workbook05/Invoke-Workbook05RouteBBuild.ps1"
).read_text(encoding="utf-8", errors="strict")


def _function_body(text: str, function_name: str) -> str:
    """Return one PowerShell function body up to the next function declaration."""

    start = text.index(f"function {function_name}")
    next_function = re.search(r"(?m)^function\s+", text[start + 1 :])
    if next_function is None:
        return text[start:]
    return text[start : start + 1 + next_function.start()]


class BuildSamplerEvidencePathIntegrityTests(unittest.TestCase):
    def test_sampler_stops_before_sum_when_resolved_process_tree_is_empty(self) -> None:
        # The production race happened after process resolution and before the
        # first aggregate. Require an explicit normal-exit guard in that window.
        processes_start = MODULE_TEXT.index("$processes = @(")
        first_sum = MODULE_TEXT.index(
            "Measure-Object -Property WorkingSet64 -Sum",
            processes_start,
        )
        guarded_window = MODULE_TEXT[processes_start:first_sum]
        self.assertIn("if ($processes.Count -eq 0)", guarded_window)
        self.assertRegex(
            guarded_window,
            r"if\s*\(\$processes\.Count\s*-eq\s*0\)\s*\{\s*break\s*\}",
        )

    def test_command_log_references_are_relative_to_the_bundle_root(self) -> None:
        # The shared producer must write portable references from the artifact
        # root, while still storing command files physically under commands/.
        adapter = _function_body(MODULE_TEXT, "Invoke-Wb05LoggedProcess")
        self.assertIn("[string]$EvidenceRoot", adapter)
        self.assertIn(
            "stdout_path = Get-Wb05RelativePath -Root $EvidenceRoot -Path $stdoutPath",
            adapter,
        )
        self.assertIn(
            "stderr_path = Get-Wb05RelativePath -Root $EvidenceRoot -Path $stderrPath",
            adapter,
        )

        # Every live caller must pass the bundle root explicitly; otherwise one
        # route could silently reintroduce the producer/validator mismatch.
        for name, text in (
            ("runtime", RUNTIME_TEXT),
            ("genai", GENAI_TEXT),
            ("route-b", ROUTE_B_TEXT),
        ):
            with self.subTest(route=name):
                self.assertIn("-EvidenceRoot $OutputDirectory", text)

    def test_genai_and_route_b_preserve_legitimate_empty_stdout(self) -> None:
        # Both later stages perform the same clean git-status read that already
        # failed in Runtime, so they must share the proven null-safe semantics.
        for name, text, function_name in (
            ("genai", GENAI_TEXT, "Read-Result"),
            ("route-b", ROUTE_B_TEXT, "Read-CommandText"),
        ):
            with self.subTest(route=name):
                body = _function_body(text, function_name)
                captured = body.index("$capturedOutput = Get-Content")
                null_check = body.index("if ($null -eq $capturedOutput)", captured)
                empty_return = body.index("return ''", null_check)
                trim_return = body.index("return $capturedOutput.Trim()", empty_return)
                self.assertLess(captured, null_check)
                self.assertLess(null_check, empty_return)
                self.assertLess(empty_return, trim_return)
                self.assertNotRegex(
                    body,
                    r"\(Get-Content[\s\S]*?\)\.Trim\(\)",
                )


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Commit only the plan/spec/test state**

```bash
git add docs/superpowers/specs/2026-08-07-workbook-05-sampler-evidence-paths-fix-design.md \
        docs/superpowers/plans/2026-08-07-workbook-05-sampler-evidence-paths-fix.md \
        tests/testing/workbook05/test_build_sampler_evidence_path_integrity.py
git commit -m "test: reproduce Workbook 05 sampler evidence defects"
```

- [ ] **Step 3: Open a draft PR to trigger exact-head CI and verify RED**

Run the focused test through the repository's `Workbook 05 documented build` pull-request gate.

Expected RED behavior on the test-only head:

```text
test_sampler_stops_before_sum_when_resolved_process_tree_is_empty ... FAIL
  reason: no `$processes.Count -eq 0` guard exists before the first Sum

test_command_log_references_are_relative_to_the_bundle_root ... FAIL
  reason: shared adapter lacks EvidenceRoot and callers do not pass it

test_genai_and_route_b_preserve_legitimate_empty_stdout ... FAIL
  reason: both later helpers still use direct Get-Content(...).Trim()
```

No production/workflow repair may be present at this checkpoint. If failures are syntax/harness failures instead, fix the tests and repeat RED before Task 2.

---

### Task 2: Apply the minimal shared production repair

**Files:**
- Modify: `scripts/testing/workbook05/Workbook05.Build.psm1`
- Modify: `scripts/testing/workbook05/Invoke-Workbook05RouteARuntimeBuild.ps1`
- Modify: `scripts/testing/workbook05/Invoke-Workbook05RouteAGenAIBuild.ps1`
- Modify: `scripts/testing/workbook05/Invoke-Workbook05RouteBBuild.ps1`
- Test: `tests/testing/workbook05/test_build_sampler_evidence_path_integrity.py`

**Interfaces:**
- `Invoke-Wb05LoggedProcess(..., [string]$EvidenceDirectory, [string]$EvidenceRoot, ...)` writes files under `EvidenceDirectory` and serializes `stdout_path`/`stderr_path` relative to `EvidenceRoot`.
- Runtime, GenAI, and Route B pass `-EvidenceRoot $OutputDirectory`.
- `Read-Result` and `Read-CommandText` return `''` for a readable zero-byte stdout file, but still throw for missing/unreadable files through `-ErrorAction Stop`.

- [ ] **Step 1: Guard the resource sampler before either aggregate**

Insert immediately after the `$processes = @(...)` block and before `$workingSet`:

```powershell
# A native process can exit after the first root-process check but before this
# second process-tree snapshot resolves. That is a normal end-of-sampling
# boundary, so stop cleanly instead of inventing a zero-memory measurement.
if ($processes.Count -eq 0) {
    break
}
```

Do not remove `Set-StrictMode`, suppress background-job errors, or replace missing samples with `0` rows.

- [ ] **Step 2: Extend the shared process-adapter evidence-root contract**

Add the mandatory parameter directly after `EvidenceDirectory`:

```powershell
[Parameter(Mandatory)]
[string]$EvidenceDirectory,

[Parameter(Mandatory)]
[string]$EvidenceRoot,
```

Replace only the two serialized command-record references:

```powershell
stdout_path = Get-Wb05RelativePath -Root $EvidenceRoot -Path $stdoutPath
stderr_path = Get-Wb05RelativePath -Root $EvidenceRoot -Path $stderrPath
```

Keep the physical `stdoutPath`, `stderrPath`, and `recordPath` under `EvidenceDirectory` unchanged.

- [ ] **Step 3: Update all three live callers**

Runtime, GenAI, and Route B must each contain this adjacent call boundary:

```powershell
-EvidenceDirectory (Join-Path $OutputDirectory 'commands') `
-EvidenceRoot $OutputDirectory `
```

Do not change route IDs, component IDs, environment allowlists, process monitoring, build flags, or output-directory creation rules.

- [ ] **Step 4: Make GenAI's output reader null-safe**

Replace the body of `Read-Result` with:

```powershell
function Read-Result {
    param([object]$Result)

    # Preserve a legitimate readable zero-byte stdout as an empty string while
    # keeping missing or unreadable evidence fail-closed through ErrorAction Stop.
    $capturedOutput = Get-Content `
        -LiteralPath $Result.stdout_path `
        -Raw `
        -ErrorAction Stop
    if ($null -eq $capturedOutput) {
        return ''
    }
    return $capturedOutput.Trim()
}
```

- [ ] **Step 5: Make Route B's output reader null-safe**

Replace the body of `Read-CommandText` with:

```powershell
function Read-CommandText {
    param([Parameter(Mandatory = $true)] [object]$Result)

    # Preserve a legitimate readable zero-byte stdout as an empty string while
    # keeping missing or unreadable evidence fail-closed through ErrorAction Stop.
    $capturedOutput = Get-Content `
        -LiteralPath $Result.stdout_path `
        -Raw `
        -ErrorAction Stop
    if ($null -eq $capturedOutput) {
        return ''
    }
    return $capturedOutput.Trim()
}
```

- [ ] **Step 6: Run the focused regression and verify GREEN**

Run:

```bash
python -m unittest -v tests.testing.workbook05.test_build_sampler_evidence_path_integrity
```

Expected:

```text
Ran 3 tests
OK
```

- [ ] **Step 7: Commit the minimal production repair**

```bash
git add scripts/testing/workbook05/Workbook05.Build.psm1 \
        scripts/testing/workbook05/Invoke-Workbook05RouteARuntimeBuild.ps1 \
        scripts/testing/workbook05/Invoke-Workbook05RouteAGenAIBuild.ps1 \
        scripts/testing/workbook05/Invoke-Workbook05RouteBBuild.ps1
git commit -m "fix: repair Workbook 05 sampler evidence boundaries"
```

---

### Task 3: Verify, review, merge, and return to the live Runtime boundary

**Files:**
- Review only: all files changed by Tasks 1-2.
- PR documentation: update the same draft PR opened in Task 1.

**Interfaces:**
- Consumes: exact implementation head after Task 2.
- Produces: verified PR, merged `main`, fresh post-merge regression evidence, and authorization to dispatch a new Route A Runtime workflow from the new `main` commit.

- [ ] **Step 1: Run the complete Workbook 05 gate on the exact implementation head**

Require GitHub Actions evidence for:

```text
complete tests/testing/workbook05 discovery: all pass
focused workflow/security contracts: all pass
PowerShell module import: pass
git diff --check: pass
final marker: WORKBOOK05_BUILD_STAGE_GATE_PASS
```

The new three-test module must be visible in the full-suite log and pass.

- [ ] **Step 2: Run the normal WinUI merge-candidate regression**

Require:

```text
application restore/build: pass
unit-test restore/build: pass
packaged VSTest: all tests pass
build errors: 0
```

The existing known non-fatal `NETSDK1198` warning may remain; this repair must not introduce a new warning class.

- [ ] **Step 3: Independently verify the uploaded WinUI TRX artifact**

Download the exact artifact from the exact green run, calculate its ZIP SHA-256, compare it with GitHub's digest, parse the single TRX, and require:

```text
total == executed == passed
failed == 0
error == 0
aborted == 0
timeout == 0
notExecuted == 0
```

- [ ] **Step 4: Review the exact PR diff**

Expected implementation boundary:

```text
docs/superpowers/specs/2026-08-07-workbook-05-sampler-evidence-paths-fix-design.md
docs/superpowers/plans/2026-08-07-workbook-05-sampler-evidence-paths-fix.md
tests/testing/workbook05/test_build_sampler_evidence_path_integrity.py
scripts/testing/workbook05/Workbook05.Build.psm1
scripts/testing/workbook05/Invoke-Workbook05RouteARuntimeBuild.ps1
scripts/testing/workbook05/Invoke-Workbook05RouteAGenAIBuild.ps1
scripts/testing/workbook05/Invoke-Workbook05RouteBBuild.ps1
```

Reject any unrelated WinUI production change, OpenVINO source change, workflow-permission/action-pin change, CMake flag change, Route B acceptance change, or scientific/model claim change.

- [ ] **Step 5: Update the PR description into the audit record**

Record:

```text
failed run: 31195528209
collector job: 92923024297
hosted validator job: 92931650930
artifact ID: 9001706844
artifact SHA-256: 91d031c2505876721c5d9361d95f7afe6034b3d11544fc24c1755f98df2fd16c
sampler first divergence: PropertyNotFoundStrict surfaced by Receive-Job
validator first divergence: bundle-root command paths missing commands/ prefix
RED head/run/job and exact failure reasons
GREEN head/run/job and all test counts
WinUI run/job/artifact/TRX evidence
unchanged security/scientific boundaries
```

- [ ] **Step 6: Merge only the exact verified PR head**

Use expected-head protection. Abort the merge if the PR head moved after verification.

- [ ] **Step 7: Verify fresh `main` after merge**

Require a new `Build and test` run on the merge commit and independently validate its TRX artifact using the same counters/digest rules from Step 3.

- [ ] **Step 8: Dispatch a new Route A Runtime run from the new `main` commit**

Use:

```text
stage = route-a-runtime
run_identity = phase2
runtime_install_directory = blank
runtime_decision_path = blank
BR8 string inputs = blank
br8_accepted_by_project_owner = false
```

If the available GitHub connector still cannot create a new `workflow_dispatch`, stop only at this one-click UI boundary and give the project owner the exact inputs above.

- [ ] **Step 9: Observe the rerun without making premature claims**

Require the following evidence in order:

```text
repository contract gate
Lenovo repository gate
exact Runtime source/submodule verification
CMake configure command record + resource summary
build command record + resource summary
install command record + resource summary
complete decision.json + manifest
text-only artifact upload
independent hosted validation
```

Any new failure is a new debugging problem. Do not classify it as an OpenVINO configure/build/install result unless the corresponding command record and exit code were successfully finalized and independently validated.

---

## Plan self-review

- **Spec coverage:** Defect A is Task 2 Steps 1/6; Defect B is Task 2 Steps 2-3/6; Defect C is Task 2 Steps 4-5/6; security/scientific boundaries are enforced globally and in Task 3 diff review.
- **Placeholder scan:** no TODO/TBD/"similar to" steps remain; all production/test changes are shown explicitly.
- **Type/signature consistency:** `EvidenceRoot` is a mandatory `[string]` in the shared adapter and every live caller passes `$OutputDirectory`; later output readers use the same null-safe semantic contract as Runtime.
