# Hardware Inspection Intel Runner Stage A Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the separately authorised, deterministic-only Stage A workflow that can run the frozen Hardware Inspection Gate 1 test boundary on one UCL-approved ephemeral Intel-laptop runner without acquiring or executing LLM Fit or exporting raw machine data.

**Architecture:** A GitHub-hosted preflight reads the existing default-branch approval manifest, validates the dispatch and one-time label, and proves the approved feature-tip identity. Only after it succeeds may a one-job self-hosted job check out that exact SHA. Default-branch PowerShell owns environment validation, local test execution, strict TRX parsing, privacy scanning, and the allowlisted count summary; evaluated code is limited to restore/build and the two deterministic test categories.

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
- `tests/testing/hardware_inspection/test_intel_runner_stage0_contract.py` — broaden only the existing inventory identity so a sanitised Git-index query plus bounded on-disk discovery sees tracked sparse-checkout aliases and untracked workflow/script/runbook aliases, while its exact allowlist admits only the canonical Stage A workflow and two controls.

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
    [string]$RunnerWorkspace
)
```

Inside one `try/catch`, require:

- `Phase` is exactly `Hosted`, `RunnerContext`, or `Runner`;
- `main`, `refs/heads/main`, owner/actor/triggering actor `arian20020`, attempt `1`, and confirmation `true`;
- label exactly `hardware-gate1-[0-9a-f]{16}`;
- the existing manifest is strict UTF-8/no BOM, at most 4096 bytes, and exactly the three approved properties;
- source ref exactly `refs/heads/feature/hardware-inspection` and SHA exactly the manifest's lowercase nonzero 40-hex value;
- Hosted phase receives an identity-only checkout of that remote feature ref and proves its current `HEAD` is still the approved SHA; a moved branch fails before the self-hosted job becomes eligible;
- RunnerContext validates the manifest-bound SHA, dispatch context, label, operational environment, and both `RunnerTemp` and `RunnerWorkspace` as existing normal non-reparse directories on fixed local drives before evaluated checkout or Stage A output/work-directory creation. The canonical `RunnerTemp\hardware-inspection-stage-a` child must still be absent. Runner then receives that exact checked-out SHA, resolves a normal local evaluated directory, proves `git rev-parse HEAD` equality, and proves no tracked or untracked dirt using the existing offline-compatible split probes;
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

- [ ] **Step 2: Implement the runner**

The script accepts only the five intentionally non-`Mandatory` string parameters `EvaluatedRoot`, `ApprovedSha`, `LocalWorkRoot`, `SummaryJsonPath`, and `SummaryMarkdownPath`, so missing/empty known arguments converge on the fixed in-script failure. It must:

1. canonicalise all roots, reject device/UNC/reparse paths, and create only a fresh direct child under the supplied runner-local work root;
2. revalidate exact SHA, clean tracked tree, absent operational variables, and absent candidate;
3. redirect detailed restore/build/test output into local files and emit only fixed remote failures;
4. run Release/win-x64 restore and build for exactly these projects from the evaluated checkout:

```text
tools/HardwareInspection.LlmFitSpike.Tests/HardwareInspection.LlmFitSpike.Tests.csproj
tools/HardwareInspection.LlmFitSpike.IntegrationTests/HardwareInspection.LlmFitSpike.IntegrationTests.csproj
```

5. run exactly `TestCategory=Deterministic` with floor 174 and `TestCategory=Task8Deterministic` with floor 3, using `--no-restore --no-build --runtime win-x64 --report-trx --no-ansi`;
6. parse the two local TRXs from the same bytes that are hashed, with DTD disabled and exact identities/counters;
7. verify before the first Git/.NET child and again after the final test that no `llmfit`/fake-tool process and no TCP 8787 listener remains;
8. strip inherited `GITHUB_*`, `ACTIONS_*`, `RUNNER_*`, and `STAGEA_*` variables case-insensitively from every evaluated child, while retaining ordinary safe environment values;
9. initialise the native runtime and owned collection only inside the guarded lifecycle. Start each child suspended with `STARTUPINFOEX` and an explicit inherited-handle allowlist, retain its stable process handle, assign it to a non-inheritable Windows Job Object with `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`, and only then resume it. Assignment/start failure must terminate and boundedly wait for the still-suspended root; final cleanup must terminate the job, query a bounded zero-active-process drain, and then attempt identity-safe direct-root cleanup without a global name kill;
10. retain the allowlisted JSON/Markdown content in trusted parent memory, keep the cancellation handler registered through job cleanup, residue checks, and atomic publication, check cancellation after the final process, before TRX parsing, before and between both writes, then unregister last. Any start, handler, cleanup, cancellation, unregister, or residue failure emits the one fixed error and prevents workflow upload;
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

- [ ] **Step 2: Implement canonical workflow behavior**

The workflow must have only `workflow_dispatch` with required inputs `runner_label` (string) and `confirm_authorised_runner` (boolean false by default), `permissions: contents: read`, a shared `hardware-inspection-llmfit-authorised` concurrency group with `cancel-in-progress: false`, full actor/ref/attempt/confirmation guards, and bounded 15-minute hosted/35-minute self-hosted timeouts.

Use only reviewed full-SHA pins. Stage A deliberately moves checkout from the Stage 0 v7.0.0 pin to the signed v7.0.1 cleanup fix; verify every upstream commit identity during implementation and lock it in the contract:

```text
actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1
actions/setup-python@a26af69be951a213d495a4c3e4e4022e16d87065
actions/setup-dotnet@d4c94342e560b34958eacfc5d055d21461ed1c5d
actions/upload-artifact@bbbca2ddaa5d8feaa63e36b76fdaad77386f024f
```

Hosted preflight first rejects any nonempty workflow-debug control (including a runner-context value of `false`) before it can emit eligibility, checks out only default-branch controls, runs exactly the Stage 0 and Stage A contract modules, parses the manifest, performs an identity-only checkout of the approved remote feature ref, and requires that ref's current tip to equal the approved SHA before emitting eligibility. It never runs evaluated scripts or builds. The self-hosted checkout subsequently uses the immutable approved SHA, never the branch name. The contract locks the final LF/no-BOM workflow bytes by SHA-256, the exact hosted/self-hosted step-name lists, and positive PowerShell-AST command/argument bindings so comments or manual/no-op replacements cannot satisfy the executable chain.

Canonical Stage A workflow SHA-256: `bff2441dcd72177e3adb8a052ac3a3bbff606995e4b5fcae64032152a951cb0d`.

Self-hosted execution order is fixed:

1. with a two-minute bound, reject every debug control, query and reject pre-existing `llmfit`/fake-tool processes or TCP 8787 listeners, and only then mask the fixed runner/session roots and dedicated account value;
2. check out default-branch controls with credentials disabled;
3. validate Runner context, including fixed-local normal non-reparse `RUNNER_TEMP` and `RUNNER_WORKSPACE`, before evaluated checkout or Stage A output/work-directory creation;
4. check out exactly `needs.hosted-preflight.outputs.approved_sha` with credentials disabled;
5. revalidate exact SHA/clean tree/absent candidate and operational variables;
6. set up .NET from `evaluated/global.json`;
7. revalidate the workspace and runner-temp parents, require both direct output/work children to be absent, create those exact fresh children with error-on-preexistence semantics, revalidate their parent identities, and invoke only the default-branch Stage A runner script;
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

The runbook must require: approvals first and writer trust maintained for the complete dispatch/queue/registration/job window; the Workbook runner stopped with no service, scheduled auto-start, `Runner.Listener`, or `Runner.Worker`; a dedicated non-admin account with restrictive mutual NTFS isolation so neither Hardware Inspection nor Workbook accounts can access or alter the other's runner, workspace, diagnostics, credentials, or unrelated data; no browser/PAT/secrets; download only GitHub's current supported runner and verify GitHub's displayed hash; dispatch while the hardware runner is absent and prohibit any second dispatch; inspect the exact queued run ID, default-branch ref, approved SHA, actor, triggering actor, attempt `1`, confirmation input, and one-time label; immediately before registration prove that exact run is still the sole expected queued run; use a fresh non-identifying runner name; register interactively with `--ephemeral --no-default-labels --labels <fresh-label>` using a transient one-hour token that is cleared and never pasted into retained history; execute only interactive `run.cmd` and prohibit service or scheduled-task installation; start only after the final queue verification; observe one job; require exact 174+3 results; inspect the privacy-safe artifact and remote log; verify auto-deregistration and no runner service/process, candidate/fake process, or 8787 listener. If automatic deregistration fails, use only GitHub's time-limited removal flow. After a bounded diagnostic window, inspect `_work`, `_temp`, `_diag`, actions, checkout, cache, and log remnants, preserve only explicitly approved diagnostics, then remove only the canonical validated Stage A phase directory through the UCL-approved cleanup procedure. It must state that Stage A provides no hardware, candidate, trusted Intel, offline, or Gate 1 evidence and does not permit Gate 2.

- [ ] **Step 2: Add explicit stop conditions**

Stop for missing approval, writer-trust failure at any point through job completion, a second dispatch, wrong/changed/non-sole queued run, actor/triggering-actor/ref/SHA/attempt/confirmation mismatch, rerun attempt, fixed/reused label, unexpected runner, candidate presence, operational environment variable, debug logging, raw upload, privacy failure, process/listener residue, unverified cleanup target, or any request to continue into Stage B.

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

Allow only the seven Stage A paths in this plan plus this implementation plan and the existing Stage 0 inventory contract. Confirm the Stage 0 workflow, validator, runbook, and approved-source manifest are byte-identical to their `origin/main` versions. Confirm the Stage 0 contract change is limited to admitting exactly `.github/workflows/hardware-inspection-intel-runner-stage-a.yml`, `Validate-HardwareInspectionIntelRunnerStageA.ps1`, and `Invoke-HardwareInspectionIntelRunnerStageA.ps1` alongside Stage 0 while rejecting aliases, later stages, alternative operational scripts, and the Gate 1 runbook. Confirm no evaluated feature, Model Inspection, Gate 1 evidence, application, UI, candidate, raw artifact, or Stage B–D file changed.

- [ ] **Step 2: Run dependency-free verification**

```powershell
python -m unittest tests.testing.hardware_inspection.test_intel_runner_stage0_contract -v
python -m unittest tests.testing.hardware_inspection.test_intel_runner_stage_a_contract -v
git diff --check origin/main...HEAD
```

Expected: 12 Stage 0 plus 12 Stage A tests, all passed, zero skips; clean range diff.

- [ ] **Step 3: Parse every PowerShell file and workflow**

Use Windows PowerShell 5.1 `Parser.ParseFile` for both new scripts, `py_compile` for both contract modules, and the contract's canonical YAML parser. Require strict UTF-8 without BOM and LF-only bytes for the workflow/digest. Preserve the repository's `.gitattributes` PowerShell policy: checked-out `.ps1` files may be canonical all-LF or all-CRLF, semantic assertions normalise CRLF to LF, and lone/mixed carriage returns are rejected. Require bounded file sizes and no forbidden tokens.

- [ ] **Step 4: Independently review security and specification**

Review the exact committed range for runner-routing, untrusted evaluated-code execution, raw-log leakage, manifest/SHA binding, TRX spoofing, process cleanup, scope, and UI/Gate non-deviation. Fix every Critical or Important finding test-first before requesting merge.

- [ ] **Step 5: Stop**

Do not push, open/merge a PR, dispatch Stage A, request a registration token, register/start a runner, contact the laptop, acquire a candidate, or change the network until the user separately confirms the completed plan implementation and the external UCL/writer approvals are recorded.

## Completion boundary

This plan is complete when the Stage A repository change is reviewed and locally green. Stage A itself completes only after the separately authorised Intel-laptop run passes exactly 174 deterministic tests plus the three exact Task 8 guards, exports only the privacy-safe five-property summary, leaves no process/listener/runner residue, and is independently audited. Even then, Gate 1 remains Blocked and Stage B requires its own plan, review, candidate permission, fresh label, and dispatch.
