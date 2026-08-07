# Workbook 05 PowerShell Runner-Shell Fix Design

## Purpose

Restore the manual Workbook 05 documented-build workflow on the Lenovo self-hosted Windows runner without weakening the machine-wide PowerShell execution policy.

The failed Route A Runtime run was GitHub Actions run `31177582965`, job `92863425373`. The hosted repository contract passed, the self-hosted checkout succeeded on runner `lenovo-pf4hmd0t-wb05`, and the first self-hosted `run:` step failed before the repository gate could start because Windows PowerShell refused to load GitHub's generated temporary `.ps1` wrapper under the runner service account.

## Root cause

The documented-build workflow uses the built-in `shell: powershell` on self-hosted jobs. GitHub therefore starts Windows PowerShell and dot-sources a generated temporary `.ps1` file. On this Lenovo runner, the effective execution policy blocks that generated script, producing `PSSecurityException: running scripts is disabled on this system`.

This is a workflow/runner-shell integration defect, not an OpenVINO, CMake, source, model, or resource-capacity failure. The actual Route A Runtime build step was skipped, so no OpenVINO build claim can be made from run `31177582965`.

## Existing proven pattern

Two existing Workbook 05 self-hosted workflows already use a process-scoped custom shell successfully:

```yaml
defaults:
  run:
    shell: powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -Command ". '{0}'"
```

The runner-smoke and preflight workflows use this pattern on the same runner class and preserve the machine's persistent execution-policy configuration.

## GitHub shell-precedence correction

GitHub applies the most specific `shell` declaration. A step-level `shell:` overrides a job-level `defaults.run.shell` value.

The current documented-build workflow gives its self-hosted `run:` steps explicit `shell: powershell` declarations. Therefore, adding only a job-level custom shell would not repair the failure: those step-level declarations would continue to select the restricted built-in PowerShell invocation.

The repair must therefore do both of the following inside each affected self-hosted collection job:

1. Add the approved job-level custom PowerShell shell default.
2. Remove the redundant step-level `shell: powershell` declarations from that job's `run:` steps so the job default actually governs those steps.

Hosted validation jobs are not changed; their explicit `shell: powershell` declarations remain intact.

## Selected approach

Apply the existing process-scoped custom PowerShell shell only to these four self-hosted documented-build collection jobs:

- `collect-route-a-runtime`
- `collect-route-a-genai`
- `collect-route-b-runtime`
- `collect-route-b-genai`

For each of those jobs, use:

```yaml
defaults:
  run:
    shell: powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -Command ". '{0}'"
```

and remove only that job's step-level `shell: powershell` overrides.

Do not change the hosted validation jobs. They already execute successfully on GitHub-hosted Windows runners and do not need the exception.

Do not change the Lenovo machine's `LocalMachine`, `CurrentUser`, `MachinePolicy`, or `UserPolicy` execution-policy configuration.

## Security boundary

`-ExecutionPolicy Bypass` is deliberately scoped to the PowerShell process created for an individual GitHub Actions `run:` step. It is not a persistent machine-wide policy change.

Existing protections remain unchanged:

- workflow permissions stay read-only;
- actions remain pinned to immutable SHAs;
- checkout keeps `persist-credentials: false`;
- self-hosted jobs keep the exact `self-hosted`, `Windows`, `X64`, `workbook05`, `intel-target` labels;
- Route A remains build/evidence only;
- Route B remains fail-closed unless its existing BR8 acceptance contract is satisfied;
- no model execution, Granite inference, TurboQuant activation, packed-KV, performance, memory, context, or quality claim is added by this fix.

## Regression test design

Update `tests/testing/workbook05/test_build_workflow_contract.py` with a focused contract test that proves all four self-hosted collection jobs use the approved process-scoped shell and cannot silently override it with a step-level `shell: powershell` declaration.

The test should isolate each self-hosted job block from the workflow text, then assert that:

- the block contains the exact approved custom `defaults.run.shell` command; and
- the block contains no step-level `shell: powershell` declaration.

The test must fail against the current `main` workflow. After the minimal workflow change, it must pass. Then the complete Workbook 05 documented-build gate must pass to prove no existing security, source-admission, bundle-validation, Route B, or workflow contracts were broken.

## Verification sequence

1. Add the regression test only and run the PR-hosted documented-build gate; record the expected RED failure.
2. Add the minimal custom shell defaults to the four self-hosted jobs and remove only their step-level `shell: powershell` overrides.
3. Re-run the focused workflow contract test; require pass.
4. Run the complete documented-build gate; require pass.
5. Run `git diff --check`; require pass.
6. Review the final diff to confirm hosted jobs, source pins, build flags, runner labels, permissions, artifact boundaries, Route B gates, and non-claims are unchanged.
7. Open/update a detailed PR explaining the original run, first divergence, root cause, GitHub precedence nuance, security scope, RED/GREEN evidence, and unchanged non-claims.
8. After merge, manually dispatch `route-a-runtime` again from `main` and inspect the new self-hosted build evidence before authorising Route A GenAI.

## Non-goals

This fix does not:

- change OpenVINO Runtime or GenAI source pins;
- change build flags or build parallelism;
- change runner labels or hardware requirements;
- change machine-wide PowerShell policy;
- start Route A GenAI;
- unblock Route B;
- run a Granite model;
- establish any optimisation or quality result.

## Professional basis

GitHub Actions supports custom shell templates containing `{0}`, which GitHub replaces with the generated temporary script path. GitHub also documents that the most specific default wins and that a step-level shell can override a job-level default. Microsoft documents that a PowerShell execution policy supplied for a new process/session is process-scoped rather than a persistent machine configuration. This makes the existing Workbook 05 runner-smoke/preflight pattern, applied only to the self-hosted collection jobs with conflicting step overrides removed, the narrowest compatible repair.

The engineering method follows the project's debugging and testing standards: establish the first reproducible divergence, form one falsifiable root-cause hypothesis, add a regression test, make the smallest fix, and re-run the complete regression gate before claiming completion.
