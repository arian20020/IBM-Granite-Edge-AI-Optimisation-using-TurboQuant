# Hardware Inspection Intel Runner Stage A Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the separately authorised, deterministic-only Stage A workflow that can run the frozen Hardware Inspection Gate 1 test boundary on one UCL-approved ephemeral Intel-laptop runner without acquiring or executing LLM Fit or exporting raw machine data.

**Architecture:** A GitHub-hosted preflight reads the existing default-branch approval manifest, validates the dispatch and one-time label, and proves the approved feature-tip identity. Each job begins with the same profile-free Windows PowerShell 5.1 and fail-closed Git/workspace gate before checkout; every Git boundary resolves, version-validates, and retains the same first `git` Application identity rather than later re-resolving `git.exe`. Only after hosted preflight succeeds may a one-job self-hosted job check out the approved exact SHA. After RunnerContext validation, default-branch PowerShell creates one-run SDK and NuGet state as fresh direct children of the canonical Stage A phase root, binds setup-dotnet and all evaluated children to it, and keeps it inside the exact residue/cleanup boundary. Default-branch PowerShell also owns environment validation, creation-time Windows Job Object containment, native cancellation state, local test execution, strict TRX parsing, privacy scanning, and the allowlisted count summary; evaluated code is limited to restore/build and the two deterministic test categories.

**Tech Stack:** GitHub Actions, Windows PowerShell 5.1, PowerShell 7 where already installed, Python 3.12 standard-library contract tests, .NET SDK from `global.json`, Microsoft.Testing.Platform TRX, Git.

---

## Non-negotiable entry gate

Do not implement, merge, dispatch, register a runner, or touch the Intel laptop until all of these are recorded outside repository source:

- written UCL approval for the dedicated standard Windows account, ephemeral runner registration, repository/dependency execution, and local evidence storage;
- confirmation that every repository writer is UCL-authorised/trusted for laptop code execution, or that non-operator writers are read-only for the complete dispatch/queue/registration/job window;
- confirmation that the ordinary Workbook/TurboQuant runner will be stopped and will not share the one-time label;
- approval of this exact plan.

Failure of any item is a hard stop. Stage A never acquires or executes the candidate, performs hardware capture, changes network adapters, runs `TrustedWindowsIntel` or `TrustedOffline`, or starts Gate 2.

The Hardware Inspection production/UI contract remains unchanged. In particular, no task may edit `Features/ModelInspection/**`, a Hardware Inspection production page, onboarding integration, the seven progress stages, expanded Inspection details, nested Technical information for IT, the four outcomes, evidence semantics, or the approved responsive/accessibility behavior. Those remain locked for Gate 8 by `docs/superpowers/specs/2026-08-15-hardware-inspection-production-design.md` in the evaluated feature branch.

## File map

Create:

- `.github/workflows/hardware-inspection-intel-runner-stage-a.yml` — manual hosted-preflight plus deterministic-only self-hosted job.
- `scripts/hardware-inspection/Validate-HardwareInspectionIntelRunnerStageA.ps1` — strict dispatch, approval-manifest, label, runner-context, checkout, and fixed-output validation.
- `scripts/hardware-inspection/Invoke-HardwareInspectionIntelRunnerStageA.ps1` — local-only restore/build/test/TRX/leak/privacy pipeline and sanitised summary writer.
- `tests/testing/hardware_inspection/test_intel_runner_stage_a_contract.py` — dependency-free executable workflow/script mutation contracts.
- `docs/testing/runbooks/Hardware-Inspection-Intel-Runner-Stage-A-Runbook.md` — permission, queue inspection, interactive runner, result, deregistration, and cleanup procedure.

Modify:

- `docs/testing/README.md` — index Stage A as permission-gated deterministic evidence, not Gate 1 evidence.
- `scripts/README.md` — document the two default-branch control scripts and their privacy boundary.
- `tests/testing/hardware_inspection/test_intel_runner_stage0_contract.py` — broaden only the existing inventory identity so a sanitised Git-index query plus bounded on-disk discovery sees tracked sparse-checkout aliases and untracked workflow/script/runbook aliases. Script candidates are classified from case-insensitive hardware/Intel/runner identity plus operational markers such as inspection, `intelrunner`, `llmfit`, `gate1`, `stage[a-d]`, `offline`, candidate/acquisition, or network-adapter control; runbook candidates include both hardware-inspection-Gate-1 and llmfit/Gate-1 token variants regardless of punctuation or token order. Its exact allowlist still admits only the canonical Stage A workflow and two controls.

Do not modify the approved-source manifest, Stage 0 workflow/validator/runbook, frozen feature branch, Gate 1 evidence record, production design, Model Inspection, application projects, or any Gate 2–9 file. The only approved Stage 0 transition is to amend the existing Stage 0 inventory contract so its exact allowlist admits this canonical Stage A workflow and its two default-branch Stage A control scripts alongside Stage 0. That contract change must retain case-insensitive namespace detection and reject every alias, later stage, alternative operational script, and Gate 1 runbook; it does not authorise any other Stage 0 change.

### Task 1: Lock the Stage A repository contract

**Files:**
- Create: `tests/testing/hardware_inspection/test_intel_runner_stage_a_contract.py`

- [ ] **Step 1: Add the exact contract identities**

Use `unittest` and create exactly these twelve `RunnerStageAContract` tests:

```python
TEST_NAMES = (
    "test_stage_a_workflow_is_manual_default_branch_owner_and_first_attempt_only",
    "test_stage_a_workflow_uses_hosted_preflight_then_exact_one_time_label",
    "test_stage_a_workflow_reads_only_the_default_branch_approval_manifest",
    "test_stage_a_workflow_pins_actions_and_drops_checkout_credentials",
    "test_stage_a_workflow_executes_only_the_two_deterministic_categories",
    "test_stage_a_workflow_has_no_candidate_capture_offline_or_adapter_path",
    "test_stage_a_workflow_has_bounded_timeout_and_non_cancelling_concurrency",
    "test_stage_a_validator_rejects_invalid_context_with_fixed_output",
    "test_stage_a_runner_rejects_dirty_wrong_sha_or_operational_environment",
    "test_stage_a_runner_requires_exact_trx_identities_and_zero_nonpassing",
    "test_stage_a_summary_is_allowlisted_and_raw_artifacts_stay_local",
    "test_stage_a_inventory_cannot_activate_stage_b_c_d_or_gate_2",
)
```

The test module must parse executable YAML structurally with a narrow standard-library loader/helper, inspect PowerShell AST through `Parser.ParseFile`, and test rejecting mutations. It must not satisfy positive assertions from comments. Mutations must cover an added push trigger, a fixed/shared runner label, a second self-hosted job, branch-ref checkout, persisted credentials, unpinned action, candidate command, `TrustedWindowsIntel`, `TrustedOffline`, adapter command, raw TRX upload, path/host output, relaxed result count, and an extra Stage B–D workflow.

- [ ] **Step 2: Run the focused RED**

```powershell
python -m unittest tests.testing.hardware_inspection.test_intel_runner_stage_a_contract -v
```

Expected: twelve tests discovered; failures are only missing Stage A workflow/scripts/runbook. Existing Stage 0 tests must remain green.

- [ ] **Step 3: Commit the RED contract**

```powershell
git add tests/testing/hardware_inspection/test_intel_runner_stage_a_contract.py
git commit -m "test(hardware-inspection): define Intel runner Stage A contract"
```

### Task 2: Implement strict Stage A validation

**Files:**
- Create: `scripts/hardware-inspection/Validate-HardwareInspectionIntelRunnerStageA.ps1`
- Test: `tests/testing/hardware_inspection/test_intel_runner_stage_a_contract.py`

- [ ] **Step 1: Implement the guarded interface**

Use this public parameter surface. Every known string is intentionally non-`Mandatory`, and there is no binder-level `ValidateSet`, so omitted and empty known values reach in-script validation inside the fixed-output `try/catch` instead of producing binder diagnostics:

```powershell
[CmdletBinding()]
param(
    [string]$Phase,
    [string]$ControlRoot,
    [string]$WorkflowRef,
    [string]$DefaultBranch,
    [string]$Actor,
    [string]$TriggeringActor,
    [string]$RepositoryOwner,
    [string]$RunAttempt,
    [string]$Confirmation,
    [string]$RunnerLabel,
    [string]$SourceCheckoutRoot,
    [string]$EvaluatedRoot,
    [string]$ApprovedSha,
    [string]$GitHubOutputPath,
    [string]$RunnerTemp,
    [string]$RunnerWorkspace,
    [string]$DependencyEnvironmentPath
)
```

Inside one `try/catch`, require:

- `Phase` is exactly `Hosted`, `RunnerContext`, or `Runner`;
- `main`, `refs/heads/main`, owner/actor/triggering actor `arian20020`, attempt `1`, and confirmation `true`;
- label exactly `hardware-gate1-[0-9a-f]{16}`;
- the existing manifest is strict UTF-8/no BOM, at most 4096 bytes, and exactly the three approved properties;
- source ref exactly `refs/heads/feature/hardware-inspection` and SHA exactly the manifest's lowercase nonzero 40-hex value;
- Hosted phase receives an identity-only checkout of that remote feature ref and proves its current `HEAD` is still the approved SHA; a moved branch fails before the self-hosted job becomes eligible;
- RunnerContext validates the manifest-bound SHA, dispatch context, label, and operational environment. It re-canonicalises the workspace parent represented by `ControlRoot` and the anticipated `EvaluatedRoot`, plus `RunnerTemp` and `RunnerWorkspace`, as existing normal fixed-local paths with no reparse point in any ancestor; requires `ControlRoot` to be the exact direct `control` child created by the preceding checkout; and requires the exact direct `evaluated` child plus `RunnerTemp\hardware-inspection-stage-a` to remain absent before evaluated checkout or Stage A output/work-directory creation. Only after those checks, it creates one-run SDK and NuGet state under the phase root using `DOTNET_INSTALL_DIR`/`sdk`, `DOTNET_CLI_HOME`/`cli-home`, `NUGET_PACKAGES`/`nuget-packages`, `NUGET_HTTP_CACHE_PATH`/`nuget-http-cache`, `NUGET_PLUGINS_CACHE_PATH`/`nuget-plugins-cache`, and `NUGET_SCRATCH`/`nuget-scratch`; binds the supported telemetry/first-time/multilevel/global-tools controls; and writes those bindings only to the validated local dependency environment command file. Runner then receives that exact checked-out SHA, resolves a normal local evaluated directory, proves `git rev-parse HEAD` equality, and proves no tracked or untracked dirt using the existing offline-compatible split probes;
- all six `GRANITE_LLMFIT_*` operational variables are absent;
- `third-party/bin/llmfit/v1.1.9/win-x64` is absent before test execution.

Every failure writes exactly:

```text
HI-RUNNER-STAGEA-INVALID: authorised deterministic validation failed.
```

Do not echo exceptions, values, paths, labels, users, hosts, or Git output. Hosted success may write only `approved_sha`, `source_ref`, `runner_label`, and `eligible=true` to an absent local fixture target or GitHub's pre-created empty `GITHUB_OUTPUT`. Immediately before atomic replacement it must revalidate the target as the same empty ordinary non-reparse file with a normal fixed-drive ancestor chain; an existing nonempty file is preserved and rejected. Validators emit no completion summary. The runner publishes fixed local Markdown and JSON only after contained-process cleanup succeeds; a default-control step validates both exact artifacts before JSON upload, and a later default-control publication step revalidates and appends the Markdown to the GitHub job summary.

- [ ] **Step 2: Extend tests before implementation where each rule is absent**

Cover invalid phase, malformed/multiline label, wrong actor/ref/attempt, extra manifest property, wrong SHA, dirty tracked/untracked checkout, present operational variable, candidate directory present, Git failure, and output-write failure. Assert fixed stderr and absence of injected canaries.

- [ ] **Step 3: Run validator GREEN**

```powershell
python -m unittest tests.testing.hardware_inspection.test_intel_runner_stage_a_contract.IntelRunnerStageAContractTests.test_stage_a_validator_rejects_invalid_context_with_fixed_output -v
```

Expected: pass.

- [ ] **Step 4: Commit**

```powershell
git add scripts/hardware-inspection/Validate-HardwareInspectionIntelRunnerStageA.ps1 tests/testing/hardware_inspection/test_intel_runner_stage_a_contract.py
git commit -m "feat(hardware-inspection): validate Intel runner Stage A"
```

### Task 3: Implement the deterministic runner and strict result parser

**Files:**
- Create: `scripts/hardware-inspection/Invoke-HardwareInspectionIntelRunnerStageA.ps1`
- Test: `tests/testing/hardware_inspection/test_intel_runner_stage_a_contract.py`

- [ ] **Step 1: Write fixture-driven RED tests**

Create temporary synthetic TRX inputs with DTD prohibited. Require exactly:

- deterministic: 174 unique `Passed` results, matching definitions/entries, zero failed/skipped/not-executed;
- Task 8: these three unique `Passed` identities and no others:

```text
ArtifactStringShape_RejectsPathsAndFreeTextWithGenericDiagnostics
CaptureInterval_ThirtySecondsPlusOneTickIsOutsideBoundary
StableFileIdentityAndProcessTreeCleanup_AreFailClosed
```

Reject stale/additional/duplicate/relabelled/missing result-definition-entry bindings, DTD/XML expansion, non-zero counters, path-bearing output, altered SHA, and any summary property outside:

```json
{"schemaVersion":"1.0","evaluatedSha":"<40 lowercase hex>","deterministicPassed":174,"task8DeterministicPassed":3,"nonPassing":0}
```

Keep the public contract at exactly the twelve Task 1 test identities. Within the existing runner/lifecycle identities, add mutation seams for cancellation installation and removal failure, cleanup-time cancellation, publication-time cancellation, and any fallback to a PowerShell `ConsoleCancelEventHandler` delegate. On Windows, launch an isolated runner process in a new process group, wait for a long-lived contained descendant to report readiness, deliver a real `CTRL_BREAK_EVENT`, and require bounded exit with the single fixed stderr line, empty stdout, no JSON/Markdown publication, and the descendant gone. The fixture must clean up only its recorded explicit PIDs if an assertion fails; it must not skip the live cancellation regression or use a global process-name kill.

- [ ] **Step 2: Implement the runner**

The script accepts only the five intentionally non-`Mandatory` string parameters `EvaluatedRoot`, `ApprovedSha`, `LocalWorkRoot`, `SummaryJsonPath`, and `SummaryMarkdownPath`, so missing/empty known arguments converge on the fixed in-script failure. It must:

1. canonicalise all roots, reject device/UNC/reparse paths, and create only a fresh direct child under the supplied runner-local work root;
2. revalidate exact SHA, clean tracked tree, absent operational variables, absent candidate, every exact one-run SDK/NuGet binding, and a first `dotnet` Application located directly in `DOTNET_INSTALL_DIR`;
3. redirect detailed restore/build/test output into local files and emit only fixed remote failures;
4. run Release/win-x64 restore and build for exactly these projects from the evaluated checkout:

```text
tools/HardwareInspection.LlmFitSpike.Tests/HardwareInspection.LlmFitSpike.Tests.csproj
tools/HardwareInspection.LlmFitSpike.IntegrationTests/HardwareInspection.LlmFitSpike.IntegrationTests.csproj
```

5. run exactly `TestCategory=Deterministic` with floor 174 and `TestCategory=Task8Deterministic` with floor 3, using `--no-restore --no-build --runtime win-x64 --report-trx --no-ansi`;
6. parse the two local TRXs from the same bytes that are hashed, with DTD disabled; for each `Results`, `TestDefinitions`, and `TestEntries` container reject every direct element child whose namespace or local name differs, require exact direct-child totals, and require exact result/definition/entry identities, counters, and bindings;
7. verify before the first Git/.NET child and again after the final test that no `llmfit`/fake-tool process and no TCP 8787 listener remains;
8. strip inherited `GITHUB_*`, `ACTIONS_*`, `RUNNER_*`, and `STAGEA_*` variables case-insensitively from every evaluated child, while retaining ordinary safe environment values;
9. initialise the native runtime and owned collection only inside the guarded lifecycle. Create a non-inheritable Windows Job Object with `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE` before any evaluated child. Start each child suspended in a single `CreateProcessW` call whose `STARTUPINFOEX` contains both `PROC_THREAD_ATTRIBUTE_JOB_LIST` and the explicit `PROC_THREAD_ATTRIBUTE_HANDLE_LIST`, so job binding is atomic at creation time and no child can execute outside the job. Retain the stable process handle and resume only after creation returns with the job attribute applied. Attribute-list, creation, or resume failure must close/terminate the owned job and boundedly wait for the still-suspended root where one exists; final cleanup must explicitly terminate the job, query a bounded zero-active-process drain, and then attempt identity-safe direct-root cleanup without a global name kill;
10. implement cancellation in the same compiled C# `Add-Type` runtime as static native-safe state, never as a PowerShell script-block callback. `Install` resets an atomic `Active`/`Cancelled`/`Completed` state and subscribes a compiled `Console.CancelKeyPress` handler; the handler first attempts `Active` to `Cancelled` and then sets `eventArgs.Cancel = true`; `Request` uses the same compare-and-swap without overwriting `Completed`; `IsCancellationRequested` is true only for `Cancelled`; and `Remove` unsubscribes with fail-closed state. PowerShell polls cancellation after the final child, before TRX parsing, throughout cleanup/residue checks, and before and after each atomic publication. Keep the handler installed through job cleanup, bounded drain, residue checks, and both publications. Only on that otherwise-success path, after the last publication check, `TryComplete` atomically changes `Active` to `Completed`; a concurrent cancellation and completion have exactly one winner. Remove the handler only after this linearisation boundary, then make the final fixed decision from the completion result plus any removal failure. Any install, start, cleanup, cancellation, publication, removal, or residue failure emits the one fixed error, prevents workflow upload, and still attempts bounded owned-process cleanup;
11. only after successful contained cleanup write the five-property summary as UTF-8 without BOM through same-directory `CreateNew` temps and atomic renames;
12. scan the summary for drive/UNC paths, user/host/hardware names, stdout/stderr, candidate names, raw JSON, TRX XML, IP/MAC/device identifiers, and additional fields;
13. leave TRX and detailed logs local and never place them in the upload directory.

Cancellation or any failure must attempt bounded process cleanup and retain the original failure while emitting only:

```text
HI-RUNNER-STAGEA-TESTS-FAILED: deterministic validation failed.
```

- [ ] **Step 3: Run focused and full GREEN**

```powershell
python -m unittest tests.testing.hardware_inspection.test_intel_runner_stage_a_contract -v
python -m unittest tests.testing.hardware_inspection.test_intel_runner_stage0_contract -v
```

Expected: 12/12 Stage A and 12/12 Stage 0, zero skips/failures.

- [ ] **Step 4: Commit**

```powershell
git add scripts/hardware-inspection/Invoke-HardwareInspectionIntelRunnerStageA.ps1 tests/testing/hardware_inspection/test_intel_runner_stage_a_contract.py
git commit -m "feat(hardware-inspection): run deterministic Intel Stage A"
```

### Task 4: Add the manual Stage A workflow

**Files:**
- Create: `.github/workflows/hardware-inspection-intel-runner-stage-a.yml`
- Test: `tests/testing/hardware_inspection/test_intel_runner_stage_a_contract.py`
- Modify: `tests/testing/hardware_inspection/test_intel_runner_stage0_contract.py` — only the existing inventory-contract identity, to allow the exact canonical Stage A workflow plus `Validate-HardwareInspectionIntelRunnerStageA.ps1` and `Invoke-HardwareInspectionIntelRunnerStageA.ps1`, with no aliases.

- [ ] **Step 1: Make the workflow tests RED for missing executable YAML**

Require exactly two jobs: `hosted-preflight` on `windows-latest`, then `deterministic-runner` with `needs: hosted-preflight` and `runs-on: ${{ needs.hosted-preflight.outputs.runner_label }}` only. Direct use of the unvalidated input in `runs-on` is forbidden. Do not add fixed `self-hosted`, `Windows`, or `X64` labels; the operator registers the ephemeral runner with only the fresh one-time label.

Keep the public Stage A module at exactly twelve test identities. Inside those identities, require every inline `run` step to use the one exact custom shell below, and add rejecting mutations for a missing profile flag, a PowerShell terminating error that is swallowed, and a native non-zero exit that is swallowed. Exercise the decoded shell through an isolated hostile-profile fixture that writes a canary if profile loading is permitted; assert the canary is absent and never alter the operator's real profile. For each job's first executable step, cover inherited mixed-case `GIT_*`, missing Git, Git older than 2.28, malformed or multiline `git --version`, and ordered duplicate Git Application results: first-valid/second-invalid must use only the first and succeed, while first-invalid/second-valid must use only the first and fail. Each failure must be bounded, emit only the relevant fixed diagnostic, and occur before checkout.

Broaden the existing Stage 0 inventory selector, not its allowlist. Build candidates from the sanitised Git index plus bounded on-disk discovery, normalise case, separators, punctuation, and token order, and classify:

- `.ps1` files below `scripts/` when a hardware/Intel/runner identity is paired with an operational marker such as inspection/inspect, `intelrunner`, `llmfit`, `gate1`, `stage[a-d]`, `offline`, candidate/acquisition, or network-adapter control;
- `.md` files below `docs/testing/runbooks/` when `runbook` is paired with either hardware + inspection/inspect + gate + 1 or an llmfit/Gate-1 token variant.

The exact allowlist remains Stage 0 plus only the canonical Stage A workflow and its two controls. Add direct tracked and on-disk alias fixtures including `scripts/hardware-inspect/Invoke-IntelRunnerStageB.ps1`, `Hardware-Inspection-Gate-1-Runbook.md`, and punctuation-inserted/reordered variants; every alias must fail closed.

- [ ] **Step 2: Implement canonical workflow behavior**

The workflow must have only `workflow_dispatch` with required inputs `runner_label` (string) and `confirm_authorised_runner` (boolean false by default), `permissions: contents: read`, a shared `hardware-inspection-llmfit-authorised` concurrency group with `cancel-in-progress: false`, full actor/ref/attempt/confirmation guards, and bounded 15-minute hosted/35-minute self-hosted timeouts.

Every inline `run` step in both jobs must use Windows PowerShell 5.1 through this exact decoded custom-shell template; built-in `powershell`/`pwsh` shells and profile-loading wrappers are forbidden:

```text
C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe -NoLogo -NoProfile -NonInteractive -Command "$ErrorActionPreference = 'Stop'; $global:LASTEXITCODE = 0; & '{0}'; if (-not $?) { if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }; exit 1 }; exit $LASTEXITCODE"
```

The YAML quoting may escape the inner quotes but must decode to those exact arguments. The contract must prove `-NoLogo`, `-NoProfile`, and `-NonInteractive` precede the generated script, profile canaries cannot run, a PowerShell error returns non-zero, and a native exit code such as 23 is preserved. `-ExecutionPolicy` is forbidden: the UCL runner's effective PowerShell policy, AppLocker, and EDR controls must remain authoritative and fail closed.

Use only reviewed full-SHA pins. Stage A deliberately moves checkout from the Stage 0 v7.0.0 pin to the signed v7.0.1 cleanup fix; verify every upstream commit identity during implementation and lock it in the contract:

```text
actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1
actions/setup-python@a26af69be951a213d495a4c3e4e4022e16d87065
actions/setup-dotnet@d4c94342e560b34958eacfc5d055d21461ed1c5d
actions/upload-artifact@bbbca2ddaa5d8feaa63e36b76fdaad77386f024f
```

The first executable step of each job is a combined precheckout guard and the first checkout is the next step. Before invoking Git, the guard enumerates inherited environment names case-insensitively and rejects every `GIT_*`. It resolves Applications with `Get-Command -CommandType Application -All`, selects only the first result without concatenating `.Source`, requires exactly one strictly parsed `git --version` line from that result, and requires version 2.28 or newer. Duplicate PATH entries are allowed, but a later valid Git may never rescue an invalid first result. Missing, old, malformed, multiline, or failed first Git emits only:

```text
HI-RUNNER-STAGEA-GIT-INVALID: required Git capability is unavailable.
```

This check is mandatory before `actions/checkout`, preventing its old/missing-Git REST fallback from broadening materialisation. In the same first step, canonicalise `GITHUB_WORKSPACE` as an existing local fixed-drive directory with no reparse point in its ancestor chain and require its exact direct `control` and `evaluated` children to be absent. The hosted guard still rejects every nonempty workflow-debug control (including a runner-context value of `false`) before eligibility. The self-hosted guard still rejects debug controls and pre-existing `llmfit`/fake-tool processes or TCP 8787 listeners, then masks runner/session roots only after every guard succeeds. After the control checkout, RunnerContext independently rechecks the workspace identity, exact direct `control` child, absent exact direct `evaluated` child, runner roots, and fresh work child before evaluated checkout.

Hosted preflight checks out only default-branch controls, runs exactly the Stage 0 and Stage A contract modules, parses the manifest, performs an identity-only checkout of the approved remote feature ref, and requires that ref's current tip to equal the approved SHA before emitting eligibility. It never runs evaluated scripts or builds. The self-hosted checkout subsequently uses the immutable approved SHA, never the branch name. The contract locks the final LF/no-BOM workflow bytes by SHA-256, the exact hosted/self-hosted step-name lists, the exact decoded custom shell, first-step ordering, and positive PowerShell-AST command/argument bindings so comments or manual/no-op replacements cannot satisfy the executable chain.

The workflow digest recorded before these hardening changes is superseded. After all Task 4 workflow edits stabilise, run this against the final bytes, copy the emitted 64-lowercase-hex digest into this plan and the contract constant, and rerun both contract modules before the Task 4 commit:

```powershell
python -c "from pathlib import Path; import hashlib; b=Path('.github/workflows/hardware-inspection-intel-runner-stage-a.yml').read_bytes(); assert not b.startswith(b'\xef\xbb\xbf') and b'\r' not in b; print(hashlib.sha256(b).hexdigest())"
```

Canonical Stage A workflow SHA-256: `953167cdfcb983ae6d0ca00831d35fcdf826570001ab7721f1b835ab423c37a5`.

Self-hosted execution order is fixed:

1. with a two-minute bound in the first executable step, reject every debug control and inherited case-insensitive `GIT_*`, require the fixed Git Application contract, validate the fixed-local non-reparse workspace and absent exact `control`/`evaluated` children, query and reject pre-existing `llmfit`/fake-tool processes or TCP 8787 listeners, and only then mask the fixed runner/session roots and dedicated account value;
2. check out default-branch controls with credentials disabled;
3. validate Runner context again, including exact workspace/control/evaluated-child identity and fixed-local normal non-reparse `RUNNER_TEMP` and `RUNNER_WORKSPACE`; require the anticipated evaluated child and phase root to be absent, then create and bind the canonical one-run SDK/NuGet phase root and its six fresh dependency children before setup-dotnet;
4. check out exactly `needs.hosted-preflight.outputs.approved_sha` with credentials disabled;
5. revalidate exact SHA/clean tree/absent candidate and operational variables;
6. set up .NET from `evaluated/global.json` using the RunnerContext-bound SDK/NuGet paths;
7. revalidate the workspace and runner-temp parents; revalidate the existing phase root and all six dependency children; require the direct output child to be absent, create that exact fresh output child with error-on-preexistence semantics, revalidate its parent identity, and invoke only the default-branch Stage A runner script;
8. validate the exact ordinary fixed-drive JSON and Markdown bytes under an exclusive read handle immediately before upload;
9. upload only the already privacy-scanned JSON summary with a unique run-ID/attempt name and a short retention period;
10. append the fixed safe Markdown summary;
11. always perform bounded leak checks, treating query failure as failure; never automatically delete an unverified path.

GitHub pre-creates `GITHUB_STEP_SUMMARY` as an empty ordinary file. Both runner output targets are therefore fresh and absent-only in the local export directory. Only after exact JSON/Markdown validation and the JSON upload may a default-control publication step validate that exact local Markdown and write it through a held handle to the pre-created empty GitHub summary file after validating its ordinary non-reparse target and ancestor chain.

No step may contain acquisition/capture/report commands, candidate arguments or URL, `TrustedWindowsIntel`, `TrustedOffline`, `GRANITE_LLMFIT_*` assignment, adapter/network commands, raw artifact paths, `Start-Process`, shell indirection, or execution from an evaluated `working-directory` except the exact `dotnet` project commands owned by the default-branch runner script.

- [ ] **Step 3: Run canonical/mutation GREEN**

```powershell
python -m unittest tests.testing.hardware_inspection.test_intel_runner_stage_a_contract -v
```

Expected: 12/12.

- [ ] **Step 4: Commit**

```powershell
git add .github/workflows/hardware-inspection-intel-runner-stage-a.yml tests/testing/hardware_inspection/test_intel_runner_stage_a_contract.py tests/testing/hardware_inspection/test_intel_runner_stage0_contract.py
git commit -m "build(hardware-inspection): add authorised Intel runner Stage A"
```

### Task 5: Add the operator runbook and indexes

**Files:**
- Create: `docs/testing/runbooks/Hardware-Inspection-Intel-Runner-Stage-A-Runbook.md`
- Modify: `docs/testing/README.md`
- Modify: `scripts/README.md`
- Test: `tests/testing/hardware_inspection/test_intel_runner_stage_a_contract.py`

- [ ] **Step 1: Document the exact stopped-runner procedure**

The runbook must require: approvals first and writer trust maintained for the complete dispatch/queue/registration/job window; the Workbook runner stopped with no service, scheduled auto-start, `Runner.Listener`, or `Runner.Worker`; a dedicated non-admin account with restrictive mutual NTFS isolation so neither Hardware Inspection nor Workbook accounts can access or alter the other's runner, workspace, diagnostics, credentials, or unrelated data; no browser/PAT/secrets; download only GitHub's current supported runner and verify GitHub's displayed hash; dispatch while the hardware runner is absent and prohibit any second dispatch; inspect the exact queued run ID, default-branch ref, approved SHA, actor, triggering actor, attempt `1`, confirmation input, and one-time label; immediately before registration prove that exact run is still the sole expected queued run; use a fresh non-identifying runner name and a separately fresh, empty, dedicated work directory that is local to a fixed drive, outside OneDrive/network/profile locations, accessible only to the dedicated account, and has no reparse point in its ancestor chain; before registration use a local no-echo procedure to require the actual Windows computer name and expected runner-group display to be explicitly UCL-approved and non-identifying, because workflow masks cannot remediate machine/group pre-step metadata, and do not clear, reconfigure, or rename anything to continue; before registration and start perform the same no-echo boundary over case-insensitive `HTTP_PROXY`/`HTTPS_PROXY`/`NO_PROXY`, runner `.env`/service proxy sources, `ACTIONS_RUNNER_DEBUG`/`ACTIONS_STEP_DEBUG`/`RUNNER_DEBUG`, and runner trace/print-log controls, with unknown or unsafe metadata a hard stop; before registration and again after registration immediately before `run.cmd`, require exact absence of `ACTIONS_RUNNER_HOOK_JOB_STARTED`, `ACTIONS_RUNNER_HOOK_JOB_COMPLETED`, `ACTIONS_RUNNER_ACTION_ARCHIVE_CACHE`, and `ACTIONS_RUNNER_SYMLINK_CACHED_ACTIONS` from the effective process/user/system environment and fresh runner-root `.env`, because hooks and action-cache overrides can act before workflow steps, and never execute, clear, repair, or override them; register interactively with `--ephemeral --no-default-labels --labels <fresh-label> --work <fresh-work-directory>` using that exact one-run work directory, omit `--token`, require `ACTIONS_RUNNER_INPUT_TOKEN` absent, and enter the transient one-hour token only at the hidden secret prompt before clearing it; never reuse the runner installation or work directory; execute only interactive `run.cmd` and prohibit service or scheduled-task installation; start only after the final queue verification; observe one job; require exact 174+3 results; inspect the privacy-safe artifact and remote log; verify auto-deregistration and no runner service/process, candidate/fake process, or 8787 listener. If automatic deregistration fails, use only GitHub's time-limited removal flow with `--token` omitted and its hidden prompt. After a bounded diagnostic window, inspect the exact fresh work directory, `_temp`, `_diag`, actions, checkout, cache, and log remnants, preserve only explicitly approved diagnostics, then independently revalidate and remove only the exact one-run runner installation, work directory, and canonical Stage A phase directory through the UCL-approved cleanup procedure. It must state that Stage A provides no hardware, candidate, trusted Intel, offline, or Gate 1 evidence and does not permit Gate 2.

- [ ] **Step 2: Add explicit stop conditions**

Stop for missing approval, writer-trust failure at any point through job completion, a second dispatch, wrong/changed/non-sole queued run, actor/triggering-actor/ref/SHA/attempt/confirmation mismatch, rerun attempt, fixed/reused label, unexpected runner, a pre-existing/reused/non-local/reparse runner work directory, candidate presence, operational environment variable, debug logging, raw upload, privacy failure, process/listener residue, unverified cleanup target, or any request to continue into Stage B.

- [ ] **Step 3: Update indexes without changing evidence claims**

Link the runbook and scripts. Do not change the current Blocked report or mark `F-M07`, `HE-01`, or `HE-02` verified.

- [ ] **Step 4: Run docs/contracts and commit**

```powershell
python -m unittest tests.testing.hardware_inspection.test_intel_runner_stage_a_contract -v
git diff --check
git add docs/testing/runbooks/Hardware-Inspection-Intel-Runner-Stage-A-Runbook.md docs/testing/README.md scripts/README.md tests/testing/hardware_inspection/test_intel_runner_stage_a_contract.py
git commit -m "docs(hardware-inspection): document Intel runner Stage A"
```

### Task 6: Verify locally and stop before laptop execution

**Files:** No production edits.

- [ ] **Step 1: Verify exact repository scope**

Allow only the seven Stage A paths in this plan plus this implementation plan and the existing Stage 0 inventory contract. Confirm the Stage 0 workflow, validator, runbook, and approved-source manifest are byte-identical to their `origin/main` versions. Confirm the Stage 0 contract change is limited to admitting exactly `.github/workflows/hardware-inspection-intel-runner-stage-a.yml`, `Validate-HardwareInspectionIntelRunnerStageA.ps1`, and `Invoke-HardwareInspectionIntelRunnerStageA.ps1` alongside Stage 0 while the broader sanitised-index/on-disk operational-marker discovery rejects every alias, later stage, alternative operational script, and hardware-inspection-Gate-1 or llmfit/Gate-1 runbook variant. Confirm no evaluated feature, Model Inspection, Gate 1 evidence, application, UI, candidate, raw artifact, or Stage B–D file changed.

- [ ] **Step 2: Run dependency-free verification**

```powershell
python -m unittest tests.testing.hardware_inspection.test_intel_runner_stage0_contract -v
python -m unittest tests.testing.hardware_inspection.test_intel_runner_stage_a_contract -v
git diff --check origin/main...HEAD
```

Expected: 12 Stage 0 plus 12 Stage A tests, all passed, zero skips; clean range diff.

- [ ] **Step 3: Parse every PowerShell file and workflow**

Use Windows PowerShell 5.1 `Parser.ParseFile` for both new scripts, `py_compile` for both contract modules, and the contract's canonical YAML parser. Require strict UTF-8 without BOM and LF-only bytes for the workflow/digest, recompute the final workflow SHA-256 after the last YAML edit, and require the plan and contract to contain that same exact digest. Structurally prove every inline step has the exact profile-free Windows PowerShell 5.1 custom shell, then exercise its hostile-profile, PowerShell-error, and native-exit fixtures. Preserve the repository's `.gitattributes` PowerShell policy: checked-out `.ps1` files may be canonical all-LF or all-CRLF, semantic assertions normalise CRLF to LF, and lone/mixed carriage returns are rejected. Require bounded file sizes and no forbidden tokens.

- [ ] **Step 4: Independently review security and specification**

Review the exact committed range for runner-routing, precheckout Git/workspace/profile isolation, untrusted evaluated-code execution, raw-log leakage, manifest/SHA binding, TRX spoofing, atomic creation-time job containment, compiled native cancellation and publication ordering, process cleanup, scope, and UI/Gate non-deviation. Fix every Critical or Important finding test-first before requesting merge.

- [ ] **Step 5: Stop**

Do not push, open/merge a PR, dispatch Stage A, request a registration token, register/start a runner, contact the laptop, acquire a candidate, or change the network until the user separately confirms the completed plan implementation and the external UCL/writer approvals are recorded.

## Completion boundary

This plan is complete when the Stage A repository change is reviewed and locally green. Stage A itself completes only after the separately authorised Intel-laptop run passes exactly 174 deterministic tests plus the three exact Task 8 guards, exports only the privacy-safe five-property summary, leaves no process/listener/runner residue, and is independently audited. Even then, Gate 1 remains Blocked and Stage B requires its own plan, review, candidate permission, fresh label, and dispatch.
