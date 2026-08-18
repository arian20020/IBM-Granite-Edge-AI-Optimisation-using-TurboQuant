# Hardware Inspection Intel Runner Stage 0 Runbook

## Purpose

Stage 0 is repository-only hosted identity validation. The UCL Intel laptop must remain disconnected from this stage. No candidate, hardware, raw-evidence, or network action is allowed. Gate 1 remains Blocked, and Gate 2 must not start. No LLM Fit candidate is acquired or executed.

## Authority

This runbook is governed by the copied design specification at `docs/superpowers/specs/2026-08-18-hardware-inspection-intel-runner-configuration-design.md`. The plain path docs/testing/runbooks/Hardware-Inspection-LLM-Fit-Gate-1-Runbook.md exists only in the exact evaluated feature checkout and must not be copied onto main.

## Preconditions

- The evaluated feature is frozen and pinned by the default-branch approval manifest.
- The Stage 0 workflow and validator are present on the default branch.
- Confirm the dispatch actor and triggering actor are the repository owner, and that this is attempt 1.
- Stage 0 neither checks nor changes laptop or self-hosted-runner state.
- The existing Workbook/TurboQuant runner must not be stopped, removed, relabelled, or contacted.

## Future permission gates

Before any later self-hosted stage, obtain written UCL approval for the dedicated account, runner registration, repository and dependency execution, and evidence storage. every repository writer must be UCL-authorised and trusted, or a future non-operator read-only design must be explicitly approved. Actor, ref, label, environment, and approval-manifest checks are defence in depth, not substitutes for those approvals and the repository-writer trust boundary.

## Local contract verification

Run these commands from the integration checkout:

```text
python -m unittest tests.testing.hardware_inspection.test_intel_runner_stage0_contract -v
powershell.exe -NoProfile -Command "$tokens = $null; $errors = $null; [System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path scripts/hardware-inspection/Validate-HardwareInspectionIntelRunnerStage0.ps1).Path, [ref]$tokens, [ref]$errors) | Out-Null; if ($errors.Count) { $errors | ForEach-Object { $_.ToString() }; exit 1 }"
git diff --check
```

The PowerShell command is the PowerShell 5.1 `Parser.ParseFile` syntax check for the validator. Exactly 12 tests must pass.

## Manual hosted dispatch only

```powershell
gh workflow run hardware-inspection-intel-runner-stage0.yml `
    --repo arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant `
    --ref main `
    -f confirm_repository_only=true
```

No runner label is used or reserved in Stage 0.

## Expected result

Exactly one `hosted-preflight` job runs on `windows-latest`. The control checkout executes the validators and tests; the evaluated checkout is identity-only. Its summary is limited to fixed safe bullets:

- Stage 0 only.
- The Intel laptop was not contacted.
- The LLM Fit candidate was not acquired or executed.
- Gate 1 remains Blocked.
- Gate 2 is prohibited.

No artifact is uploaded.

## Stop conditions

Stop immediately if any path would involve a self-hosted runner, runner registration or service, candidate acquisition or execution, hardware capture, evidence, adapter or network action, upload, or evaluated-code execution.

## Deferred stages

Stages A, B, C, and D each require separate approval. A future Stage A requires a separate approved plan. Each future Stage A, B, and D uses a fresh one-time label. Stage C is manual offline work and never adapter automation.
