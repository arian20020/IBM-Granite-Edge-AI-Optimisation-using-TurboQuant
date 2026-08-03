# Workbook 05 Intel Runner Smoke-Test Design

**Date:** 3 August 2026  
**Status:** Approved through the Workbook 05 automation discussion and runner setup  
**Target repository:** `arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant`  
**Target runner:** `lenovo-pf4hmd0t-wb05`

## 1. Purpose

Before compiling an experimental OpenVINO runtime or running any model benchmark, prove that GitHub Actions can route a job to the intended Intel laptop and that the service account running the job can access the minimum required toolchain.

This is a diagnostic gate only. It must not build OpenVINO, download models, execute inference, edit workbook results, commit generated evidence, or push to the repository.

## 2. Target identity

The smoke test must require all runner labels below:

- `self-hosted`
- `Windows`
- `X64`
- `workbook05`
- `intel-target`

The job must also verify the runtime identity rather than trusting labels alone:

- Runner name: `lenovo-pf4hmd0t-wb05`
- Windows computer name: `LENOVO-PF4HMD0T`
- Processor name contains: `i5-12450H`
- Installed physical memory is at least 15 GiB
- Operating system is 64-bit

A mismatch is a hard failure because benchmark evidence from a different machine would not satisfy Workbook 05.

## 3. Trigger design

The workflow supports two controlled triggers:

1. A path-limited pull-request trigger for this first trusted in-repository branch, so the new workflow can prove the runner connection before merge.
2. `workflow_dispatch`, which becomes available after the workflow exists on the default branch and allows later manual smoke tests.

The pull-request job must run only when:

- the pull request comes from the same repository rather than a fork; and
- the triggering GitHub actor is `arian20020`.

This prevents unrelated pull requests from gaining routine access to the self-hosted machine.

## 4. Permissions and security boundary

The workflow receives only:

```yaml
permissions:
  contents: read
```

It performs no repository checkout because the diagnostic is self-contained. Avoiding checkout reduces exposure to repository-controlled scripts and avoids unnecessary work in the runner workspace.

The workflow must not:

- use a personal access token;
- expose secrets;
- request `contents: write` or `pull-requests: write`;
- start arbitrary repository scripts;
- install software;
- change system configuration;
- modify the GitHub runner registration;
- write outside the GitHub runner temporary directory.

## 5. Diagnostic checks

The workflow records and checks:

- GitHub runner name, OS, and architecture;
- Windows computer name;
- processor name;
- installed RAM;
- operating-system caption, version, build, and 64-bit state;
- current Windows service account;
- free space on the system drive;
- PowerShell version;
- Git version;
- Python version, preferring Python 3.12 through the `py` launcher and falling back to `python`;
- CMake version;
- MSBuild location and version, discovered through `vswhere.exe`;
- ability to create and read a probe file under `RUNNER_TEMP`.

The required tool checks deliberately run under the runner service account. A tool that works only in the interactive user account is not sufficient for unattended Workbook 05 automation.

## 6. Evidence output

The workflow writes evidence to:

```text
${RUNNER_TEMP}/workbook-05-runner-smoke/
```

The directory contains:

- `runner-smoke-report.json` — structured machine-readable evidence;
- `runner-smoke-summary.md` — beginner-readable summary;
- `workspace-write-probe.txt` — proof that the runner service account can write and read temporary evidence.

The evidence directory is uploaded as an immutable GitHub Actions artifact with a 30-day retention period. The upload step runs even when a required check fails so the failure can be diagnosed.

## 7. Failure behaviour

The workflow first gathers as much evidence as possible, writes the report, and only then fails if any required check did not pass.

A failed smoke test blocks all later Workbook 05 automation. It does not imply the laptop is unsuitable; it identifies the exact missing identity, permission, or toolchain prerequisite to fix.

## 8. Acceptance criteria

The smoke-test stage is accepted only when:

1. GitHub routes the job to `lenovo-pf4hmd0t-wb05`.
2. Every target-identity check passes.
3. Git, Python, CMake, and MSBuild are accessible to the Windows service account.
4. The temporary write/read probe passes.
5. The workflow completes successfully.
6. The uploaded JSON and Markdown evidence accurately record the run.
7. No repository file or system configuration is changed by the workflow.

## 9. Follow-on boundary

Passing this smoke test authorises design and implementation of the next gate only: a Workbook 05 prerequisite inventory and source-pin verification workflow. It does not authorise the full 36-pair codec sweep or formal performance measurements.

## 10. Engineering basis

- *Engineering Software Products*, Chapter 10: keep automated build/test operations controlled through code management and DevOps automation.
- *The Art of Unit Testing*, Chapter 10: define a test recipe and introduce expensive test levels only after lower-risk gates are reliable.
- *Systems Engineering Principles and Practice*, Chapter 17: plan test configuration, traceability, evidence, and operational realism before total-system evaluation.
- *Designing Secure Software*, Chapters 3–4: minimise attack surface, privilege, information exposure, and default permissions.
