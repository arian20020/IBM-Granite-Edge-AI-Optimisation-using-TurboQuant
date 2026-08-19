# Hardware Inspection Intel Runner Stage A Runbook

## Purpose and boundary

Stage A is a separately authorised, deterministic-only repository test on one UCL-approved Intel laptop. It produces permission-gated deterministic-contract evidence only. It provides no hardware evidence, no candidate evidence, no trusted Intel evidence, no offline evidence, and no Gate 1 evidence. Gate 1 remains Blocked. Stage A does not permit Gate 2, Stage B, candidate acquisition or execution, hardware capture, network-adapter changes, or an offline run.

The fresh label and all workflow checks are defence in depth; they are not authority to execute repository code. Follow this runbook only after the external approvals and trust boundary below are recorded. Stop rather than repairing, clearing, relabelling, redispatching, or broadening an unexpected state.

## 1. Record authority and maintain writer trust

Before dispatch, record outside repository source:

- written UCL approval for the dedicated standard Windows account, ephemeral runner registration, repository and dependency execution, and local evidence storage;
- approval of the exact Stage A implementation plan;
- confirmation that every repository writer is UCL-authorised and trusted to execute code on the laptop, or that every non-operator writer is read-only; and
- confirmation that the Workbook/TurboQuant runner will be stopped and isolated for the complete Stage A window.

Writer trust must be rechecked and maintained continuously for the complete dispatch, queue, registration, and job window through job completion. Stop immediately if repository permissions, writers, the queued run, or any other precondition changes. Actor, ref, SHA, manifest, confirmation, and runner-label checks are defence in depth and never replace UCL approval or continuous writer trust.

## 2. Isolate the laptop accounts and stop the Workbook runner

Use a dedicated non-admin Hardware Inspection Windows account. Before Stage A, a UCL-authorised administrator must establish restrictive mutual NTFS isolation: the Hardware Inspection and Workbook accounts must be unable to read, access, or alter each other's runner installation, workspaces, diagnostics, credentials, caches, logs, or unrelated data.

Stop the Workbook/TurboQuant runner before dispatch and keep it stopped until Stage A cleanup is complete. Prove there is no runner service, scheduled auto-start, scheduled task, `Runner.Listener`, or `Runner.Worker` for either the Workbook runner or an earlier Hardware Inspection runner. Do not stop processes globally or by process name; investigate any unexpected process and stop the procedure.

The dedicated laptop account must have no browser, PAT, or retained secret. Perform GitHub sign-in, dispatch, queue inspection, and registration/removal-token retrieval on a separate trusted operator device. The only credential transferred to the laptop is GitHub's transient one-hour registration token, or a time-limited removal token when the exceptional removal flow is required. Transfer it through the UCL-approved secure procedure, enter it directly, clear it immediately, and never paste it into retained command history, a file, a log, a screenshot, or repository content.

The laptop's effective Defender, EDR, AppLocker, firewall, TLS, and PowerShell policy must remain authoritative. Do not disable, bypass, weaken, or reconfigure any of these controls to make setup or execution succeed. A control that blocks Stage A is a stop condition.

## 3. Prepare two fresh, isolated directories

On the separate trusted operator device, download only GitHub's current supported runner package from GitHub's official public runner download/release instructions. Verify the archive against the GitHub-displayed SHA-256 before extraction. Do not use a cached package. No registration or removal token may be requested, copied, or transferred during package preparation. If GitHub does not make the package and displayed hash available without entering a token flow, defer package preparation until after the final sole-queue verification; never request or retain an early token.

Transfer the verified archive, never an extracted runner tree, to the laptop through the UCL-approved transfer procedure. Under the dedicated laptop account, recompute its SHA-256 on the laptop immediately before extraction and require exact equality with the same GitHub-displayed SHA-256 recorded on the trusted device. Stop on a transfer, read, or digest mismatch.

Create both of these as distinct targets:

1. a fresh, empty, one-run runner installation directory; and
2. a separately fresh, empty, dedicated work directory for the exact `--work` value.

Each directory must be local to a fixed drive, outside OneDrive, network, and profile locations, accessible only to the dedicated account, and have no reparse point in its ancestor chain. Canonicalise and inspect every ancestor before use. Neither directory, name, ACL, configuration, registration, nor prior contents may be reused. The work directory must not be inside the runner installation directory.

Choose independently:

- a fresh one-time label matching exactly `hardware-gate1-[0-9a-f]{16}`; and
- a fresh non-identifying runner name that reveals no person, university, laptop, host, asset, room, or location.

The label and runner name are different values. The label is routing only and does not establish authority.

## 4. Dispatch with the hardware runner absent

Dispatch while the hardware runner is absent. On the separate trusted operator device, verify that no runner is registered with the fresh label and that no other queued or running job targets the fresh label. Dispatch the default-branch `hardware-inspection-intel-runner-stage-a.yml` workflow from `main` exactly once, with the fresh label and the confirmation input set to `true`. A second dispatch is prohibited; `cancel-in-progress: false` does not make a second pending run safe.

Wait for `hosted-preflight` to succeed. Do not register or start the laptop runner if hosted preflight fails, is cancelled, or has not completed.

Inspect GitHub's queued run and record, without laptop-identifying information:

- the exact queued run ID and workflow identity;
- the default-branch ref `refs/heads/main`;
- the approved lowercase 40-hex feature SHA from `approvedTipSha` in `.github/hardware-inspection/llmfit-gate1-approved-source.json` at the exact default-branch run commit;
- actor `arian20020` and triggering actor `arian20020`;
- attempt `1` with no rerun;
- confirmation input `true`; and
- the exact fresh one-time label.

Inspect the approval manifest at the exact default-branch run commit on the trusted device, then correlate its `approvedTipSha` with the successful hosted preflight and queued job. Do not assume an internal hosted-preflight output is displayed in the GitHub UI. Prove this is the sole expected queued run, no other queued or running job targets the label, and there is no other runner with the label. Keep the runner absent while inspecting it.

## 5. Recheck the queue, then register interactively

Immediately before requesting a registration token and immediately before registration, recheck writer trust and prove that the exact run ID is still the sole expected queued run. Recheck its workflow, `refs/heads/main`, approved SHA, actor, triggering actor, attempt `1`, confirmation input, and fresh label, and prove no other queued or running job targets the fresh label. Stop on any mismatch or additional queued or running run.

On the separate trusted operator device, request a new transient one-hour registration token only after the final queue verification. In the fresh installation directory on the laptop, use GitHub's displayed configuration command with the fresh non-identifying name and this exact flag sequence:

```text
--ephemeral --no-default-labels --labels <fresh-label> --work <fresh-work-directory>
```

Enter the transient token without retaining it. Do not add default, fixed, shared, `self-hosted`, `Windows`, or `X64` labels. Do not configure unattended execution. Do not install a service or scheduled task. Never reuse the runner installation, configuration, token, label, name, or work directory.

Supply `--name <fresh-non-identifying-runner-name>` as a separate configuration value. Never substitute the runner name for the exact fresh label or derive one from the other.

After registration, perform the same exact queue and writer-trust check once more. Only if it remains the sole expected queued run, start exactly one interactive `run.cmd` session from the dedicated account. Observe that it accepts exactly one job. Stop if another job is offered or any identity changes.

## 6. Verify the one deterministic job

The accepted job may execute only the repository's Stage A deterministic boundary. Require all of the following:

- exactly 174 deterministic tests passed;
- exactly 3 Task 8 deterministic tests passed, with these identities and no others:
  - `ArtifactStringShape_RejectsPathsAndFreeTextWithGenericDiagnostics`;
  - `CaptureInterval_ThirtySecondsPlusOneTickIsOutsideBoundary`;
  - `StableFileIdentityAndProcessTreeCleanup_AreFailClosed`;
- zero non-passing results;
- one JSON-only privacy-safe artifact named exactly `hardware-inspection-stage-a-summary-<run-id>-1`, retained for 3 days, containing exactly one JSON file whose only properties are `schemaVersion`, `evaluatedSha`, `deterministicPassed`, `task8DeterministicPassed`, and `nonPassing`; no other properties, appended bytes, or files are permitted;
- a sanitised remote log and fixed safe Markdown summary; and
- raw TRX, detailed restore/build/test logs, and diagnostic logs remain local and are never uploaded.

The JSON values are exact: `schemaVersion` must equal `"1.0"`; `evaluatedSha` must equal the exact approved SHA read from the manifest at the run commit; `deterministicPassed` must equal `174`; `task8DeterministicPassed` must equal `3`; and `nonPassing` must equal `0`. Reject any type, value, property, file, or byte outside that exact object.

Inspect the privacy-safe artifact and sanitised remote log on the separate trusted operator device. Stop if either contains a user, host, path, hardware name, device identifier, IP or MAC address, stdout/stderr, candidate data, raw JSON beyond the five-property summary, raw TRX, or detailed logs. A privacy failure is a failed run and must not be worked around by uploading another file.

Do not acquire or run a candidate. Do not clear a candidate directory or operational environment variable and continue. Do not enable debug logging, capture hardware, change an adapter, alter networking, invoke a trusted/offline category, or upload local evidence.

## 7. Verify deregistration and residue

After the single job finishes, require GitHub to show automatic deregistration of the ephemeral runner. Verify no runner service, scheduled task, `Runner.Listener`, `Runner.Worker`, candidate/fake-tool process, or TCP 8787 listener remains. Query failure is a stop condition, not evidence of absence.

If automatic deregistration fails, do not reuse the runner. On the separate trusted operator device, use only GitHub's time-limited removal flow for this exact runner. Transfer and clear the removal token using the same approved transient procedure. Do not use a PAT or a long-lived secret.

## 8. Inspect and clean only exact targets

Keep a finite, bounded diagnostic window approved by UCL; it must not exceed 60 minutes after job completion. Inspect each recorded location separately:

- runner installation directory: inspect `_diag`, runner configuration, and runner log remnants;
- work directory: inspect `_temp`, actions, checkout, cache, and work-tree log remnants; and
- canonical Stage A phase directory: inspect exactly `$RUNNER_TEMP\hardware-inspection-stage-a`.

Preserve only diagnostics that UCL explicitly approves, in the approved local evidence location, after the same privacy review. Do not upload raw diagnostics.

After the bounded window:

1. independently canonicalise and revalidate the exact one-run runner installation directory, the separately fresh work directory, and the canonical Stage A phase directory;
2. prove each is the recorded fixed-local, non-reparse, restrictive-ACL target and not a parent, profile, OneDrive, network, shared, or reused path;
3. prove no unexpected runner/process/listener remains; and
4. remove only those three exact targets through the UCL-approved cleanup procedure.

An unverified cleanup target must remain untouched and the procedure must stop. Never use a wildcard, recursive parent deletion, global process-name kill, broad cache cleanup, or an unrelated runner removal. Record cleanup and the final absence checks outside repository source without laptop-identifying data.

## Stop conditions

Stop immediately for any of the following:

- missing or withdrawn approval, or a writer-trust failure at any point through job completion;
- a second dispatch; a wrong, changed, or non-sole queued run; hosted-preflight failure; an unexpected runner; or another job;
- actor, triggering actor, ref, approved SHA, attempt, confirmation input, workflow, run ID, or label mismatch;
- a rerun attempt, fixed or reused label, identifying/reused runner name, or any reused installation or work directory;
- a pre-existing, reused, non-local, non-fixed-drive, shared, profile/OneDrive/network, incorrectly permissioned, nested, or reparse runner/work path;
- candidate presence, a fake-tool process, an operational environment variable, debug logging, a TCP 8787 listener, or any pre-existing, unexpected, additional, service, scheduled, or residual runner process outside the one authorised interactive `run.cmd` session;
- any browser login, PAT, retained secret, token retention, unattended execution, service installation, or scheduled task on the dedicated laptop account;
- any raw upload, unexpected artifact, privacy failure, raw TRX/log exposure, result other than exact 174 deterministic plus 3 Task 8 deterministic passes, or non-passing result;
- process/listener residue, automatic-deregistration/removal failure, an unverified cleanup target, or a request for broad/global cleanup; or
- any request to acquire/execute a candidate, capture hardware, change networking, produce Gate 1 evidence, continue into Stage B, or start Gate 2.

Do not redispatch, rerun, relabel, reuse, repair in place, weaken a guard, clear the unexpected state and continue, or extend this authority. A new attempt requires a separate review, fresh approval confirmation, fresh label, fresh install, fresh work directory, and new dispatch.

## Completion boundary

Repository implementation of Stage A is not permission to run it. A Stage A execution is complete only when the one authorised run passes the exact 174+3 boundary, publishes only its privacy-safe summary, automatically deregisters (or is removed through GitHub's time-limited flow), leaves no residue, and completes exact bounded cleanup. Even then, `F-M07`, `HE-01`, and `HE-02` are not verified, Gate 1 remains Blocked, Stage B requires a separate approved plan and fresh authority, and Gate 2 is prohibited.
