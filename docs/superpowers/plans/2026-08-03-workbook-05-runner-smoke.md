# Workbook 05 Intel Runner Smoke-Test Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a read-only GitHub Actions smoke test that proves Workbook 05 jobs reach the intended Intel laptop and that the runner service account can access the required toolchain.

**Architecture:** A single self-contained workflow routes through cumulative self-hosted-runner labels, gathers target identity and toolchain evidence with Windows PowerShell, uploads immutable diagnostic artifacts, and fails after evidence capture when a required check is not satisfied. No repository checkout or write permission is used.

**Tech Stack:** GitHub Actions YAML, Windows PowerShell 5.1+, WMI/CIM, `vswhere.exe`, GitHub Actions artifacts.

## Global Constraints

- Repository: `arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant`.
- Base branch: `main`.
- Implementation branch: `testing/workbook-05-runner-smoke`.
- Expected runner name: `lenovo-pf4hmd0t-wb05`.
- Expected computer name: `LENOVO-PF4HMD0T`.
- Expected processor identifier: `i5-12450H`.
- Required labels: `self-hosted`, `Windows`, `X64`, `workbook05`, `intel-target`.
- Workflow token permission: `contents: read` only.
- No OpenVINO build, model download, inference, benchmark, workbook mutation, repository push, or system installation.
- Artifact retention: 30 days.
- All third-party GitHub Actions must be pinned to an immutable commit SHA.

---

### Task 1: Record the smoke-test design and implementation boundary

**Files:**
- Create: `docs/superpowers/specs/2026-08-03-workbook-05-runner-smoke-design.md`
- Create: `docs/superpowers/plans/2026-08-03-workbook-05-runner-smoke.md`

**Interfaces:**
- Consumes: Workbook 05 target-machine record and the registered runner labels.
- Produces: An approved scope and acceptance gate for the workflow implementation.

- [x] **Step 1: Create the dedicated branch from `main`**

Branch:

```text
testing/workbook-05-runner-smoke
```

- [x] **Step 2: Write the design specification**

The design must define target identity, trigger restrictions, permissions, evidence, failure behaviour, acceptance criteria, and the boundary that blocks OpenVINO/model work.

- [x] **Step 3: Self-review the specification**

Verify that it contains no `TBD`, `TODO`, unbounded permissions, implicit repository writes, or ambiguous target-machine requirements.

- [x] **Step 4: Commit the design**

Commit message:

```text
docs(workbook-05): specify Intel runner smoke test
```

---

### Task 2: Add the read-only runner smoke workflow

**Files:**
- Create: `.github/workflows/workbook-05-runner-smoke.yml`

**Interfaces:**
- Consumes: GitHub runner contexts, Windows CIM providers, `git`, `py` or `python`, `cmake`, `vswhere.exe`, and MSBuild.
- Produces: `runner-smoke-report.json`, `runner-smoke-summary.md`, `workspace-write-probe.txt`, a GitHub step summary, and a workflow pass/fail result.

- [x] **Step 1: Define controlled triggers**

Add a path-limited `pull_request` trigger for the first trusted branch and a `workflow_dispatch` trigger for manual execution after merge.

The pull-request job-level condition must require:

```text
github.event.pull_request.head.repo.full_name == github.repository
github.event.pull_request.head.ref == 'testing/workbook-05-runner-smoke'
```

- [x] **Step 2: Restrict permissions and routing**

Use:

```yaml
permissions:
  contents: read
```

Route the job through all five required labels and apply a ten-minute timeout.

- [x] **Step 3: Create the evidence directory before checks**

Use:

```text
${RUNNER_TEMP}/workbook-05-runner-smoke
```

This guarantees that diagnostic output can be uploaded even when later checks fail.

- [x] **Step 4: Implement target identity checks**

Verify the runner name, runner OS, runner architecture, computer name, processor identifier, minimum physical memory, and 64-bit Windows state.

Each check emits a structured object containing:

```text
Name
Required
Passed
Expected
Actual
```

- [x] **Step 5: Implement toolchain discovery**

Record PowerShell, Git, Python, CMake, `vswhere.exe`, and MSBuild versions. Python discovery prefers `py -3.12` and falls back to `python --version`. MSBuild discovery uses `vswhere.exe` rather than assuming MSBuild is on `PATH`.

- [x] **Step 6: Implement the write/read probe**

Create `workspace-write-probe.txt` under the evidence directory, read it back, and fail the corresponding check if the content differs.

- [x] **Step 7: Write JSON and Markdown evidence before failing**

The structured report includes run identifiers, runner contexts, hardware, tool versions, disk information, service account, and every check result.

- [x] **Step 8: Upload evidence on both success and failure**

Use the repository's existing immutable `actions/upload-artifact` v7 pin:

```text
bbbca2ddaa5d8feaa63e36b76fdaad77386f024f
```

Set `if-no-files-found: error` and `retention-days: 30`.

- [x] **Step 9: Fail only after evidence is written**

If any required check has `Passed = false`, throw one final error listing the failed check names.

- [x] **Step 10: Validate workflow structure**

Parse the YAML and inspect the generated file to verify:

```text
workflow_dispatch exists
pull_request is path-limited
permissions are read-only
all five labels are present
no checkout step exists
artifact upload is pinned
```

- [x] **Step 11: Commit the workflow**

Commit message:

```text
ci(workbook-05): add Intel runner smoke test
```

---

### Task 3: Open, observe, and review the pull request

**Files:**
- Review: `.github/workflows/workbook-05-runner-smoke.yml`
- Review: `docs/superpowers/specs/2026-08-03-workbook-05-runner-smoke-design.md`
- Review: `docs/superpowers/plans/2026-08-03-workbook-05-runner-smoke.md`

**Interfaces:**
- Consumes: The branch commits and the connected self-hosted runner.
- Produces: A detailed pull request and the first target-laptop GitHub Actions evidence.

- [ ] **Step 1: Open a detailed draft pull request into `main`**

The pull-request body must explain the purpose, security boundary, exact checks, evidence files, failure semantics, and explicit exclusions.

- [ ] **Step 2: Observe the pull-request workflow run**

Confirm that the job is assigned to `lenovo-pf4hmd0t-wb05` rather than remaining queued or using a GitHub-hosted runner.

- [ ] **Step 3: Inspect every job step and artifact**

Required job outcome:

```text
All required checks passed
Artifact uploaded
No repository files changed
```

If the job fails, use the artifact and logs to identify the exact service-account, identity, or tool-path issue before changing the workflow.

- [ ] **Step 4: Review the pull-request diff**

Verify the implementation against every acceptance criterion in the design specification.

- [ ] **Step 5: Merge only after evidence review**

After merge, `workflow_dispatch` becomes available from the Actions page because the workflow then exists on the default branch.
