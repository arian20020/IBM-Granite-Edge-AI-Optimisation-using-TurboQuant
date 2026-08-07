# Workbook 05 PowerShell Runner-Shell Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the manual Workbook 05 documented-build workflow execute its self-hosted Windows `run:` steps on the Lenovo runner without changing persistent machine execution-policy settings.

**Architecture:** Reuse the already-proven Workbook 05 runner-smoke/preflight PowerShell command template at the job level for the four self-hosted collection jobs only. Remove those jobs' step-level `shell: powershell` overrides because GitHub gives the step-level declaration precedence over the job default. Protect the integration with a workflow contract regression test and prove RED/GREEN through the PR-hosted documented-build gate before re-running the live Route A Runtime stage.

**Tech Stack:** GitHub Actions YAML, Windows PowerShell 5.1, Python 3.12 `unittest`, GitHub-hosted Windows validation, Lenovo self-hosted Windows/X64 Workbook 05 runner.

## Global Constraints

- Work only on `fix/workbook-05-powershell-runner-shell`; never implement directly on `main`.
- Preserve read-only workflow permissions and immutable action SHAs.
- Preserve `persist-credentials: false`.
- Preserve self-hosted runner labels exactly: `[self-hosted, Windows, X64, workbook05, intel-target]`.
- Preserve Runtime source pin `b9a1f201c109e0bed74763934f79483cf6c4cbf4`.
- Preserve GenAI source pin `05e5c7670b597746f858946974d11f38e3baf42f`.
- Do not alter OpenVINO build flags, build parallelism, evidence schemas, artifact boundaries, Route B BR8 gate, or model/non-claim boundaries.
- Do not change `LocalMachine`, `CurrentUser`, `MachinePolicy`, or `UserPolicy` PowerShell execution policy.
- Keep GitHub-hosted validation jobs on their existing shell behavior.
- Do not claim the Route A Runtime build succeeds until a fresh post-merge workflow-dispatch run produces independently validated evidence.

---

### Task 1: Add the regression contract and prove RED

**Files:**
- Modify: `tests/testing/workbook05/test_build_workflow_contract.py`
- Reference: `.github/workflows/workbook-05-documented-build.yml`

**Interfaces:**
- Consumes: the documented-build workflow as plain text through existing `DocumentedBuildWorkflowContractTests.workflow`.
- Produces: `test_self_hosted_collection_jobs_use_process_scoped_powershell_shell`, a regression contract that fails unless every self-hosted collection job uses the approved custom shell without a conflicting step-level shell override.

- [ ] **Step 1: Add a helper that isolates one job block**

Add this helper inside `DocumentedBuildWorkflowContractTests`:

```python
    def _job_block(self, job_id: str) -> str:
        # Locate the requested top-level job declaration in the workflow text.
        marker = f"  {job_id}:\n"
        start = self.workflow.index(marker)

        # Find the next top-level job declaration so assertions stay scoped to
        # this job instead of accidentally passing because another job matches.
        next_job = self.workflow.find("\n  ", start + len(marker))
        while next_job != -1:
            candidate_line = self.workflow[next_job + 1 :].splitlines()[0]
            if candidate_line.startswith("  ") and candidate_line.endswith(":"):
                break
            next_job = self.workflow.find("\n  ", next_job + 3)

        # The final job extends to end-of-file when there is no following job.
        end = len(self.workflow) if next_job == -1 else next_job
        return self.workflow[start:end]
```

- [ ] **Step 2: Add the failing self-hosted shell contract**

Add:

```python
    def test_self_hosted_collection_jobs_use_process_scoped_powershell_shell(self) -> None:
        # This is the exact shell already proven by the runner-smoke and
        # preflight workflows on the Lenovo self-hosted runner.
        expected_shell = (
            'shell: powershell -NoLogo -NoProfile -ExecutionPolicy Bypass '
            '-Command ". \'{0}\'"'
        )

        # Every live collection job runs on the same restricted self-hosted
        # Windows runner and therefore needs the process-scoped shell default.
        for job_id in (
            "collect-route-a-runtime",
            "collect-route-a-genai",
            "collect-route-b-runtime",
            "collect-route-b-genai",
        ):
            with self.subTest(job_id=job_id):
                block = self._job_block(job_id)
                self.assertIn("defaults:\n      run:\n        " + expected_shell, block)

                # A step-level built-in shell would override the job default and
                # recreate the exact PSSecurityException seen in run 31177582965.
                self.assertNotIn("\n        shell: powershell\n", block)
```

- [ ] **Step 3: Commit the test before changing the workflow**

Commit message:

```text
test(workbook-05): cover self-hosted PowerShell shell policy
```

- [ ] **Step 4: Open a draft PR to `main`**

The PR body must state that the first CI failure is intentional TDD RED evidence and must include run `31177582965` / job `92863425373` as the production reproduction.

- [ ] **Step 5: Verify RED through the PR-hosted documented-build workflow**

Expected result:

```text
DocumentedBuildWorkflowContractTests.test_self_hosted_collection_jobs_use_process_scoped_powershell_shell ... FAIL
```

The failure must be caused by the missing custom self-hosted job default, not by syntax/import/setup errors. Do not proceed until that is confirmed.

---

### Task 2: Apply the minimal self-hosted workflow repair and prove GREEN

**Files:**
- Modify: `.github/workflows/workbook-05-documented-build.yml`
- Test: `tests/testing/workbook05/test_build_workflow_contract.py`

**Interfaces:**
- Consumes: GitHub Actions `jobs.<job_id>.defaults.run.shell` and the four existing self-hosted collection jobs.
- Produces: the exact process-scoped shell command used by all `run:` steps in those jobs.

- [ ] **Step 1: Add the approved shell default to `collect-route-a-runtime`**

Immediately after `timeout-minutes: 360`, add:

```yaml
    defaults:
      run:
        shell: powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -Command ". '{0}'"
```

Remove the job's three explicit `shell: powershell` lines from:
- `Run the repository gate on the Intel runner`
- `Execute Route A Runtime build`
- `Bind bundle metadata to this exact attempt`

- [ ] **Step 2: Apply the identical change to `collect-route-a-genai`**

Add the same job default and remove only that job's explicit `shell: powershell` declarations from its `run:` steps.

- [ ] **Step 3: Apply the identical change to `collect-route-b-runtime`**

Add the same job default and remove only that job's explicit `shell: powershell` declarations from its `run:` steps.

- [ ] **Step 4: Apply the identical change to `collect-route-b-genai`**

Add the same job default and remove only that job's explicit `shell: powershell` declarations from its `run:` steps.

- [ ] **Step 5: Do not edit hosted validators**

Confirm `verify-build-contract`, `validate-route-a-runtime`, `validate-route-a-genai`, `validate-route-b-runtime`, and `validate-route-b-genai` retain their existing hosted runner and shell declarations.

- [ ] **Step 6: Commit the minimal implementation**

Commit message:

```text
fix(workbook-05): use process-scoped PowerShell shell on runner
```

- [ ] **Step 7: Verify GREEN through the PR workflow**

Require:
- the new regression test passes;
- the complete Workbook 05 suite passes;
- PowerShell module import checks pass;
- workflow/security contracts pass;
- `git diff --check` passes;
- final marker `WORKBOOK05_BUILD_STAGE_GATE_PASS` appears.

---

### Task 3: Review scope, complete the PR record, and land only after verification

**Files:**
- Review: `.github/workflows/workbook-05-documented-build.yml`
- Review: `tests/testing/workbook05/test_build_workflow_contract.py`
- Review: `docs/superpowers/specs/2026-08-07-workbook-05-powershell-runner-shell-fix-design.md`
- Review: `docs/superpowers/plans/2026-08-07-workbook-05-powershell-runner-shell-fix.md`

**Interfaces:**
- Consumes: RED/GREEN CI run IDs, exact branch head SHA, PR diff, review state.
- Produces: a merge-ready PR whose evidence is sufficient to justify re-dispatching Route A Runtime from `main`.

- [ ] **Step 1: Inspect the final diff**

Require all of the following to be unchanged:
- workflow permissions;
- action SHAs;
- checkout credential behavior;
- source pins;
- build flags and parallelism;
- runner labels/timeouts;
- artifact names/retention/data-only boundary;
- Route B gating logic;
- model/inference/performance/quality non-claims.

- [ ] **Step 2: Confirm review state**

Require no unresolved review threads and no unexpected changed files.

- [ ] **Step 3: Update the PR body with complete evidence**

Record:
- original production failure run/job;
- first divergence (`PSSecurityException` before repository gate execution);
- root cause;
- GitHub step-vs-job shell precedence nuance;
- why process scope is safer than machine-wide policy changes;
- exact RED commit/run/test;
- exact GREEN commit/run/test counts and final gate marker;
- final changed-file boundary;
- explicit non-claims;
- next action: merge, then manually dispatch `route-a-runtime` from `main`.

- [ ] **Step 4: Mark ready only after checks are green**

Do not merge a draft or failing head.

- [ ] **Step 5: Merge with expected-head protection**

Merge only the exact verified head SHA so a later branch mutation cannot bypass verification.

- [ ] **Step 6: Verify post-merge `main` regression before live build**

Require the ordinary post-merge repository/application regression to pass before authorising a new manual Route A Runtime dispatch.

- [ ] **Step 7: Re-dispatch Route A Runtime manually from `main`**

Use:

```text
stage = route-a-runtime
run_identity = phase2
runtime_install_directory = blank
runtime_decision_path = blank
BR8 inputs = blank/default
```

Then inspect the self-hosted job and independent hosted validator before making any Runtime acceptance claim.
