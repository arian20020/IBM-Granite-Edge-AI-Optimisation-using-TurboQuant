# Hardware Inspection Intel Runner Configuration Design

**Status:** Approved design; written specification awaiting user review

**Date:** 2026-08-18

**Branch:** `feature/hardware-inspection`
**Related runbook:** `docs/testing/runbooks/Hardware-Inspection-LLM-Fit-Gate-1-Runbook.md`

## 1. Purpose

This design keeps Hardware Inspection development on the authoring computer while using the loaned UCL Intel laptop only for tests that require its physical Intel hardware. GitHub Actions provides the manual dispatch and result status. The laptop does not need Visual Studio Code or Codex.

The design is intentionally staged because permission to execute the pinned LLM Fit candidate and temporarily disconnect the laptop's non-loopback network interfaces has not yet been confirmed. Uncertain permission is treated as no permission for those operations.

## 2. Decisions

- Continue coding, review, commits, and ordinary tests in the existing Hardware Inspection worktree.
- Use the same Intel laptop as the target, but run Hardware Inspection under a dedicated standard Windows account whose runner, workspace, session root, and credentials are inaccessible to the existing Workbook/TurboQuant account. Do not alter the existing runner registration.
- Use one-job ephemeral Hardware Inspection runner registrations with one-time routing labels. Run only one runner application on the laptop at a time.
- Enable repository-only workflow configuration and hosted validation first. Self-hosted restore, build, and test execution also requires UCL authorisation because MSBuild and tests execute repository and dependency code.
- Do not download or execute LLM Fit, capture target hardware evidence, or change network state until UCL permission is confirmed.
- After permission, use separate trusted-online preparation and post-offline collection workflows, with a manual offline bridge between them.
- Keep raw candidate, hardware, reference, and TRX evidence on the laptop. Upload only a strict sanitised summary or Markdown report.
- Keep Gate 1 `Blocked` and do not start Gate 2 until both trusted Intel and controlled offline evidence pass the existing disposition rules.

## 3. Current repository constraints

- Hardware Inspection is developed in `C:\hardware-inspection` on `feature/hardware-inspection`.
- The current feature branch is not yet available as a remote branch and has no upstream.
- The repository default branch does not yet contain the proposed manual dispatcher.
- GitHub permits manual dispatch only for a workflow present on the default branch.
- The existing `.github/workflows/hardware-inspection-llmfit-spike.yml` is a GitHub-hosted deterministic workflow. It must remain candidate-free and must not be repurposed as the self-hosted Intel workflow.
- The existing Intel laptop runner also serves Workbook/TurboQuant workflows. Its broad labels are not an adequate isolation boundary for Hardware Inspection.
- The current private-repository plan does not provide a protection rule or approval environment that can be assumed by this design, and another collaborator currently has write access. An actor check and custom label reduce accidental dispatch but do not make unreviewed workflow code safe.
- The current repository setting permits all actions and does not enforce full-SHA action pinning. The Hardware Inspection workflow must enforce its own full-SHA pins, while repository-wide hardening is handled explicitly rather than assumed.
- The current Gate 1 report is truthfully `Blocked` because the authoring host was not the Windows Intel target and was not offline.

## 4. Architecture

```text
Authoring computer
  Codex + source + commits + ordinary tests
                 |
                 | push reviewed feature commit
                 v
Private GitHub repository
  default-branch manual dispatcher
  exact evaluated commit SHA
                 |
                 | one manually queued job
                 v
UCL Intel laptop
  dedicated standard account
  one-job ephemeral runner, interactive and normally absent
  persistent ignored Gate 1 session outside runner workspace
                 |
       +---------+---------+
       |                   |
  authorised          additional permission-gated route
  deterministic       trusted online -> manual offline -> collect
  tests
```

The GitHub runner requires network connectivity to receive and report a job. Therefore the controlled offline test cannot be an ordinary end-to-end GitHub Actions job. The offline operation is a local, operator-controlled bridge between two online workflow runs.

## 5. Runner isolation

### 5.1 Dedicated account and ephemeral registration

The hardware setup does not reuse the active Workbook runner instance or Windows account. UCL must approve a dedicated standard local account with restrictive NTFS access to its runner, workspace, and session root. The Hardware Inspection account must not be able to read or modify the Workbook runner's registration, workspaces, diagnostics, credentials, or unrelated UCL/user data; the Workbook account must likewise be unable to modify the Gate 1 session.

Each online phase uses a fresh one-job ephemeral runner registration in a separate local directory, configured without GitHub's default labels. It receives exactly one custom, non-identifying label derived from a fresh routing nonce that is independent of the persistent session ID. Stage A, B, and D each use a new nonce; a label is never reused. The value must match `hardware-gate1-[0-9a-f]{16}`, for example:

```text
hardware-gate1-7f3a2c19d4e5a6b7
```

The corresponding workflow dispatch supplies that exact one-time label. A GitHub-hosted preflight validates the syntax and exposes the validated label as its output; only the dependent self-hosted job uses that output for `runs-on`. A malformed value, a GitHub-hosted label, or a label with any other syntax therefore never becomes a runner selector. The runner is registered only after the operator has inspected the queued job, accepts one job, and then deregisters. No fixed shared hardware label is assigned, because GitHub's subset matching would make a runner carrying that label eligible for an anchor-only job.

The GitHub-visible runner name is also a one-time non-identifying value. It must not contain the Windows hostname, UCL asset tag, username, laptop model, or location.

### 5.2 Runtime rules

- Obtain written UCL approval before creating the dedicated account, registering the agent, storing the repository/evidence, or executing repository/dependency code. Until then, stop after repository-side configuration and GitHub-hosted validation.
- Download the runner only from GitHub, verify the download hash shown by GitHub, and use GitHub's current supported runner version.
- Use the time-limited registration token only in the transient setup shell. Clear it immediately; never save or echo it.
- Run the hardware runner interactively with `run.cmd`; do not install it as a Windows service or scheduled task.
- Dispatch from the default branch, inspect the exact queued workflow, actor, triggering actor, approved SHA, one-time label, session ID, and inputs, and only then register/start the ephemeral runner.
- Confirm the Workbook runner is stopped, has no service or scheduled auto-start, and has no `Runner.Listener` or `Runner.Worker` process before starting the hardware runner.
- Confirm that no unexpected job uses the one-time label.
- After the job, verify automatic deregistration; if normal removal is still required, use GitHub's time-limited removal flow. Confirm no runner service/process remains.
- Ephemeral deregistration does not delete local `_work`, `_temp`, `_diag`, action, checkout, cache, or log data. After a bounded diagnostic window, inspect the phase directory, preserve only explicitly approved diagnostics, then remove only the canonical validated phase directory through the UCL-approved cleanup procedure. Stage B's separately rooted sealed session is not part of this cleanup.
- Do not place repository secrets, personal access tokens, SSH keys, browser sessions, mapped credentials, or unrelated source trees in the runner environment.
- Never paste the one-hour registration token into chat, source files, scripts, logs, or shell history retained as evidence.

Labels, ephemeral registration, and queue inspection reduce exposure but are not authorisation boundaries. No self-hosted stage may run until the repository-writer trust gate in section 8 is satisfied.

## 6. Workflow stages

### 6.1 Stage 0: repository-only preparation

Stage 0 is the only enabled stage while UCL permission remains uncertain. It changes repository configuration on the authoring computer and uses GitHub-hosted Windows runners only. It does not configure, connect to, or execute anything on the UCL laptop.

Stage 0 adds and statically verifies the manual dispatcher, approval manifest, every default-branch validator executed by the hosted preflight, and operator documentation. The existing hosted deterministic workflow remains candidate-free and supplies the regression baseline. The approval manifest has a fixed minimal schema containing only its schema version, the exact remote feature ref, and one lowercase 40-character approved tip SHA.

### 6.2 Stage A: authorised self-hosted deterministic workflow

Stage A restores, builds, and runs tests from the evaluated commit. These operations can execute MSBuild targets, test code, dependencies, and GitHub action code; they are not a machine-security sandbox. Stage A therefore requires both written UCL runner/repository-code authorisation and the repository-writer trust gate in section 8.

The Stage A workflow:

- is triggered only by `workflow_dispatch`;
- has `permissions: contents: read`;
- accepts a required one-time runner label matching `hardware-gate1-[0-9a-f]{16}` and an explicit confirmation input;
- runs only when `github.ref` is the default branch, `github.actor` and `github.triggering_actor` are the repository owner, and `github.run_attempt` is `1`;
- reads the single approved lowercase 40-character source SHA from a reviewed default-branch approval manifest rather than trusting a free-form dispatch SHA;
- uses a GitHub-hosted preflight to fetch the approved remote feature ref, prove the approved SHA is the reviewed tip recorded by that manifest, and publish only a fixed non-secret eligibility result for the self-hosted job;
- checks out exactly that approved SHA with credentials not persisted;
- verifies that `HEAD` equals the requested SHA and that the tracked tree is clean;
- uses SHA-pinned GitHub actions;
- uses a bounded job timeout and a workflow-level concurrency group with `cancel-in-progress: false`;
- restores and builds the Hardware Inspection LLM Fit test projects;
- runs the existing deterministic suite with a floor of 174 passing tests;
- runs the three `Task8Deterministic` guards with their exact identities;
- never calls the acquisition script, trusted capture script, Gate 1 CLI, or offline category;
- uploads only a hand-built privacy-scanned count summary.

Raw TRX files remain runner-local and are not uploaded.

GitHub concurrency prevents overlapping running jobs but does not preserve an older pending job when a newer member of the same group is queued. Therefore the operator must prevent a second dispatch and revalidate that the exact expected run ID remains queued immediately before registering the ephemeral runner. If the run ID or state changed, the runner is not started.

GitHub job logs are outbound data even when no artifact is uploaded. Every self-hosted stage masks the fixed local roots and non-identifying account value before checkout, disables debug logging, redirects detailed restore/build/test/capture output to local files, emits only fixed privacy-safe remote diagnostics, and performs a log-privacy review. Claims in this design that files are not uploaded do not exempt job logs from this boundary.

### 6.3 Stage B: trusted Intel preparation

Stage B remains absent or hard-disabled until explicit UCL permission is recorded. Enabling it requires a separate reviewed change.

After approval, the trusted-online workflow inherits all Stage A controls, uses a bounded job timeout, and joins the same workflow-level non-cancelling concurrency group. The operator still revalidates its exact queued run ID because concurrency cannot protect an older pending run. It will:

1. Validate and check out an exact reviewed commit.
2. Use an authenticated workspace checkout with full non-shallow history, prove `git rev-parse --is-shallow-repository` is `false`, and create a validated local source-branch ref at the approved SHA. Create a credential-free Git bundle from that named ref, verify the bundle has no missing prerequisites, and prove from a fresh empty repository that the bundle can fetch and check out the exact named ref and tree without network access. Initialise the persistent session outside the runner's `_work` and temporary directories from that verified bundle as an independent ordinary repository, remove the bundle remote and any credential/extra-header configuration, and check out at the exact evaluated SHA the source branch name derived by stripping `refs/heads/` from the approval manifest's remote feature ref (currently `feature/hardware-inspection`). A raw-SHA bundle revision, shallow bundle, synthetic session branch, native authenticated clone into the external root, detached HEAD, linked worktree, hardlinked local clone, UNC/device/network path, sync folder, and reparse-point root are prohibited.
3. Acquire the single pinned LLM Fit v1.1.9 Windows x64 package through the committed acquisition script.
4. Verify archive length and hash, executable hash, AMD64 PE architecture, fixed package inventory, and Authenticode observation before execution.
5. Restore and build both `HardwareInspection.LlmFitSpike.Tests` and `HardwareInspection.LlmFitSpike.IntegrationTests`, publish/configure the harmless fake-tool fixture required by the deterministic runner tests, and retain all build output only inside the sealed session or validated transient directories.
6. Run the 174-test deterministic category from `HardwareInspection.LlmFitSpike.Tests` and the three Task 8 deterministic guards, retaining fresh deterministic TRX files inside the sealed session; Stage A's uploaded count summary cannot substitute for the 174-test TRX required by the report.
7. Invoke `Capture-HardwareInspectionWindowsReference.ps1` exactly once. That script owns the single bounded `--version` and `--no-dashboard --json system` route and brackets the same run with Windows reference readings. The workflow must not execute the CLI separately before or after it.
8. Require exactly three `TrustedWindowsIntel` tests to validate the retained capture with zero failures or skips; the tests do not rerun the candidate.
9. Preserve the ignored candidate, raw evidence, Windows reference, deterministic TRX, trusted TRX, and their hashes locally for the offline and collection stages.
10. Store and upload the same exact bytes of a uniquely named sanitised online-session receipt containing the evaluated SHA, safe session ID, approved branch identity, Stage B GitHub run ID and attempt, fixed test counts, and a digest of the immutable online manifest. Record the non-sensitive run ID/attempt in the local manifest. The receipt contains no path, hardware identity, or raw evidence, and its SHA-256 is written to the privacy-safe GitHub job summary for the operator's independent comparison before Stage C.

The job does not run the report generator because the offline evidence does not exist yet.

### 6.4 Stage C: manual controlled offline bridge

Stage C is never remotely automated and remains prohibited until UCL permission for temporary network isolation is confirmed.

UCL must provide the approved elevation method, rollback procedure, and confirmation that temporary interface disablement is compatible with its management, Defender/EDR, and asset policy. The operator records the enabled/disabled state of every adapter before isolation and retains local physical access so restoration does not depend on the disabled network.

The operator procedure is:

1. Stop the GitHub runner.
2. On a separate trusted device, open the exact Stage B GitHub run ID/attempt, verify its success, and transcribe only the displayed receipt SHA-256. Do not introduce a PAT, browser credential, or GitHub session into the dedicated runner account.
3. Enter the sealed persistent session and verify the branch equals the approved source branch from the manifest, the commit is exact, and the tracked tree is clean.
4. Recompute the exact relative allowlisted inventory and candidate/package/build-harness/deterministic/trusted evidence hashes, hash the local receipt, compare that hash with the independently transcribed GitHub value, and verify the resulting immutable manifest digest before executing any ignored file.
5. Verify that the exact predeclared offline output leaf in the anchored manifest is a fresh direct child and does not exist. A retry requires a new Stage B session and receipt; the anchored manifest is never amended to select another path.
6. Through the normal UCL/Windows interface, disable every non-loopback network interface.
7. Confirm `NetworkInterface.GetIsNetworkAvailable()` is `False` and no non-loopback interface is `Up`.
8. Run the single prepared `TrustedOffline` test using that exact predeclared output leaf.
9. Record the exit code.
10. Immediately restore every interface to its recorded starting state through the same approved method, including after command, test, or process failure.
11. Confirm the expected adapters and GitHub connectivity are restored before restarting any runner.

No repository script enables or disables network adapters. A failed offline precondition produces only the existing minimal `Blocked` envelope and never touches the candidate.

### 6.5 Stage D: collection

After reconnecting, the operator manually dispatches the collection workflow with the same session identity, evaluated SHA, and exact Stage B run ID/attempt, inspects the queued job and one-time label, and only then registers/starts the ephemeral hardware runner.

Stage D inherits the complete Stage A control set: manual and default-branch-only execution, owner checks for both actor contexts, first-attempt-only execution, approval-manifest binding, full-SHA action pins, bounded timeout, masked/privacy-safe logs, and the shared workflow-level `cancel-in-progress: false` concurrency group. It adds only `actions: read` to `contents: read` so it can retrieve the exact Stage B receipt. Concurrency serialises online jobs only; the absent runner, one-time labels, sealed session, anchored receipt, and operator checks protect the manual Stage C interval.

The collection workflow:

- does not execute the candidate or rerun the offline test;
- downloads the uniquely named receipt from the exact supplied Stage B run ID/attempt, never by a latest/name search, then verifies its artifact identity, receipt digest, session ID, evaluated SHA, safe branch, manifest digest, and original run attempt;
- validates the persistent session manifest, approved source branch, commit, clean tracked tree, exact relative allowlisted artifact inventory, and freshly recomputed file hashes rather than trusting manifest values;
- verifies the deterministic, trusted Intel, and offline test identities and counters;
- runs the existing Gate 1 report generator with the six exact local inputs;
- writes the generated report to a fresh dedicated untracked export location outside the sealed session repository and outside `docs/testing/evidence`, then performs a privacy scan over that exact file;
- uploads only the sanitised Markdown report and, if useful, a tiny allowlisted JSON status summary.

It never uploads candidate binaries, raw JSON, Windows reference JSON, TRX files, absolute paths, command output, usernames, hostnames, adapter identifiers, IP addresses, MAC addresses, or hardware names.

## 7. Persistent session

Trusted and offline operations use a unique session beneath a fixed ordinary local NTFS root owned by the dedicated Hardware Inspection account and outside the runner installation/workspace, conceptually:

```text
Gate1Sessions/<opaque-session-id>/
  repo/                 exact clean evaluated commit
  session-manifest.json privacy-safe relative inventory and hashes
```

All candidate and evidence paths inside `repo` remain covered by the repository ignore rules. The session ID contains no user, host, asset, or hardware identity. Each attempt uses a new session or output leaf; stale or partially populated output is never reused.

The session ID follows a fixed lowercase hexadecimal syntax. Every operation canonicalises the fixed root and requested child, proves containment, and rejects UNC, device, network, OneDrive/synchronised, junction, symlink, mount-point, or other reparse paths. Candidate and evidence paths are relative allowlisted paths only. Trusted and offline output remain unique direct children of `artifacts/hardware-inspection/llmfit` and physically disjoint from the candidate.

The manifest records only a safe session ID, evaluated commit, approved source branch, Stage B GitHub run ID/attempt, relative allowlisted artifact paths, candidate/package/build-harness hashes, trusted output leaf, predeclared offline output leaf, and deterministic/trusted TRX hashes. Stage C validates the immutable online subset against the independently viewed receipt hash before execution. Stage D reopens and re-hashes the actual files and compares them to the exact receipt retrieved from that recorded GitHub run.

The session root must use UCL-approved encryption at rest and restrictive ACLs and must be outside personal sync, backup, and unrelated data locations. A retention deadline is recorded at session creation and may not extend beyond laptop return; a shorter UCL policy wins. The session is retained only until the sanitised report has been independently reviewed. Cleanup is a separate explicit UCL-approved action, uses canonical validated targets, and is not performed automatically by a failed workflow. If UCL requires reimaging before return, that process is authoritative.

## 8. Repository delivery

The Hardware Inspection feature must not be merged wholesale merely to make manual dispatch available.

Delivery is split:

1. Push `feature/hardware-inspection` so the reviewed evaluated commit is fetchable by GitHub.
2. Create a small integration branch from the current remote default branch.
3. Add only the reviewed dispatcher, the single approved-SHA manifest, every validator/script/action executed by the hosted preflight, and supporting documentation/tests to that integration branch. The preflight executes only files from its default-branch `github.sha`, never validators from the evaluated feature commit.
4. Merge that small change into the default branch through review. The writer trust gate below still blocks self-hosted execution.
5. The hosted preflight reads the approved SHA from the default-branch manifest, verifies the expected remote feature ref and tip, and passes only that fixed SHA to the self-hosted job.

For this personal repository-level runner, default-branch protection and workflow-declared environments do not prevent a writer from creating another branch workflow that targets the one-time label. Before any self-hosted Stage A job, every account with repository write access must therefore be explicitly UCL-authorised and trusted to execute code on the loan laptop, or non-operator accounts must be reduced to read for the complete dispatch/queue/registration/job window. Effective organisation runner-group policy restricted to selected workflows could replace this requirement in a future organisation-owned design, but it is not available or assumed here.

If this writer gate cannot be met, only Stage 0 may proceed. Repository-owner actor checks, branch checks, approval manifests, custom labels, environments, and manual queue inspection are defence in depth and do not replace the writer gate. Restrict every used action to a reviewed full commit SHA and narrow repository Actions policy where the settings support it.

## 9. Failure handling

The setup fails closed:

- Missing or mismatched default-branch approval manifest, remote feature ref, or approved tip SHA: no self-hosted checkout execution.
- Dirty tracked tree or mismatched commit: stop.
- Wrong target, missing Intel CPU/GPU, or unsupported Windows architecture: stop before candidate execution and create no trusted output.
- Candidate package, hash, PE, version, layout, or observation mismatch: stop before accepting evidence.
- Unexpected process, listener, socket, timeout, truncation, schema, privacy, or evidence mismatch: reject the route.
- Missing UCL runner/repository-code permission or unresolved writer trust: do not enter Stage A, B, C, or D.
- Missing candidate/network permission: do not enter Stage B or C.
- Stage B receipt, manifest digest, ignored inventory, candidate/package/harness hash, or predeclared offline-leaf mismatch: do not isolate the network or execute the offline test.
- Offline precondition not satisfied: candidate not judged; retain a `Blocked` result.
- Network restoration cannot be confirmed: do not restart the runner or proceed to collection.
- Session identity, inventory, or hash mismatch: do not generate or upload a report.
- Any artifact or job-log privacy scan/review failure: upload nothing further and retain the local diagnostic for authorised review.

Gate 2 remains prohibited after any such failure.

## 10. Verification strategy

### Before repository delivery

- Validate workflow YAML and PowerShell syntax.
- Assert manual-only triggers, default-branch-only execution, owner checks for both actor contexts, first-attempt-only execution, Stage D's exact `actions: read` exception, hosted-preflight validation of the sole one-time runner label, approval-manifest binding, exact-SHA checkout, credentials disabled, bounded timeouts, workflow-level non-cancelling concurrency, exact-run receipt retrieval, and SHA-pinned actions.
- Assert Stage A contains no acquisition, candidate execution, trusted capture, offline test, network-change, or raw-artifact upload path.
- Run the existing 174 deterministic tests and three Task 8 deterministic guards locally.
- Run `git diff --check` and confirm only intended Hardware Inspection files changed.

### Authorised deterministic runner acceptance

- UCL runner/repository-code permission and the repository-writer trust gate are recorded before execution.
- The ephemeral hardware runner under the dedicated account is the only active runner instance.
- The intended Stage A job, default-branch ref, owner actor contexts, approval-manifest SHA, and one-time label are inspected before the runner is registered or started.
- The evaluated SHA and clean-tree checks pass.
- Exactly the required deterministic tests pass with zero non-passing results.
- The uploaded summary contains only the allowlisted schema and counts.
- No candidate file, raw evidence, TRX, path, user, host, or hardware identity is uploaded.
- The runner accepts one job, deregisters, and leaves no runner service or process after completion.
- Detailed command output remains local, remote diagnostics are fixed/privacy-safe, and the phase runner directory receives explicit inspected retention or canonical cleanup after its diagnostic window.

### Permission-gated session acceptance

- Stage B freshly builds both test projects, publishes the harmless fixture, runs the deterministic/trusted contracts, and invokes the trusted capture script exactly once.
- The external session is an independent repository created credential-free from a local Git bundle, with the approval manifest's source branch checked out at the exact SHA.
- The Stage B receipt is uniquely bound to its run ID/attempt and its hash is independently visible before Stage C.
- Stage C verifies the local receipt, immutable manifest digest, exact ignored inventory and hashes, and absent predeclared offline leaf before network isolation or execution.
- Stage D downloads the receipt from that exact run with read-only Actions permission, recomputes all hashes, and writes only to a fresh untracked export location.

### Permission-gated acceptance

Stage B and C acceptance remains the exact contract in the Gate 1 runbook: three trusted Windows Intel tests and one controlled offline test must pass with zero skips, the candidate must leave no process or socket behind, and the generated report must satisfy the existing Gate 1 disposition and privacy rules.

## 11. Scope and non-goals

This design configures remote orchestration for the already implemented Gate 1 test boundary. It does not:

- change the Hardware Inspection product architecture or approved UI;
- modify Model Inspection;
- add Gate 2 production integration;
- approve LLM Fit redistribution;
- treat sampled socket evidence as proof that no network syscall occurred;
- automate UCL permission, network-adapter changes, or physical-machine control;
- move Codex or normal development onto the Intel laptop;
- expose raw target evidence through GitHub.

## 12. Approval and rollout gates

The rollout order is fixed:

1. Review and approve this specification.
2. Create and review a detailed implementation plan.
3. Implement and verify repository-only Stage 0.
4. Push and integrate the small default-branch dispatcher and approval manifest through review.
5. Obtain written UCL permission for the dedicated runner account, repository/dependency execution, and evidence storage; resolve the repository-writer trust gate.
6. Configure and smoke-test one ephemeral Stage A runner job.
7. Obtain explicit UCL permission for the observed-unsigned pinned candidate execution and temporary network isolation, including the approved elevation and rollback procedure. Never bypass Defender, AppLocker, EDR, firewall, or TLS controls.
8. Implement and review Stages B-D.
9. Run the trusted Intel and controlled offline evidence sequence.
10. Generate and independently review the sanitised Gate 1 report.
11. Remove transient runner registrations, verify processes/services are absent, and perform UCL-approved retention cleanup or reimage before laptop return.
12. Start Gate 2 only if the report disposition permits it.

## 13. References

- [GitHub: Adding self-hosted runners](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/add-runners)
- [GitHub: Using self-hosted runners in a workflow](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/use-in-a-workflow)
- [GitHub: Self-hosted runners reference](https://docs.github.com/en/actions/reference/runners/self-hosted-runners)
- [GitHub: Secure use reference](https://docs.github.com/en/actions/reference/security/secure-use)
- [GitHub: Manually running a workflow](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/manually-run-a-workflow)
