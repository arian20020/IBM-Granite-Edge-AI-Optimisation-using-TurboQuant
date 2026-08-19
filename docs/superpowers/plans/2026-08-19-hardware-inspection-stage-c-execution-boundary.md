# Hardware Inspection Stage C Execution Boundary

**Status:** **PROPOSED — REQUIRES C0/USER APPROVAL**

- **C1 revision identifier:** `C1-S1-R1-RECONCILIATION-v1`
- **Original proposal commit:** `914ee113badb06f0ecfa3f07994609808b6029f0`
- **Original document SHA-256:** `E93E1B796FA324B57F727D20B7567EEFDC53C5D9F534E01E9F380E45601828AB`
- **C0 base commit:** `bce1427eca99e1fc6128d8b0a57e155fd3ccd600`
- **R1 review SHA-256:** `C86695D5134A765E793956E686D51CEEB030192F8D14D2B0AE3BD2F5564547B1`
- **Revision status:** This document remains unapproved. This revision does not authorize implementation, Stage A/B/C/D execution, candidate acquisition or execution, laptop contact, network change, workflow dispatch, publication, push, PR or merge.
- **Revision precedence:** Section 17 is the controlling C1 reconciliation amendment. It preserves compatible original decisions and traceability, supersedes the v1 Stage C evidence schemas and ambiguous legacy field names, and creates no product-runtime relationship.

- **Decision date:** 19 August 2026
- **Decision owner:** C0/user
- **Future execution owner:** C1, only after separate written C0, user and UCL approval
- **Base:** `bce1427eca99e1fc6128d8b0a57e155fd3ccd600`

This is a documentation-only future executable-plan proposal. It is not an approval to implement this plan, acquire or run a candidate, contact the laptop, change networking, register a runner, dispatch a workflow, publish evidence, or begin Gate 2.

## 1. Scope, authority and current state

Stage C is the manual, controlled-offline bridge between a separately approved Stage B sealed-session receipt and a separately approved Stage D collection. Its sole operational purpose is to prove, in one fresh sealed session, that the integrity-bound candidate was invoked exactly once through the approved system-JSON command while positive offline isolation, process containment, bounded capture and cleanup remained continuously observable.

Stage C is not responsible for candidate acquisition, version probing by execution, licence or redistribution approval, Stage A repository or laptop work, Stage B online preparation, Stage D publication, final Gate 1 disposition, production process foundations, production parsing, hardware compatibility, Model Inspection, Block 3, UI work, or any Gate 2–9 implementation.

The operational order is fixed:

1. Stage A remains the repository/runner preflight and has not run on the laptop.
2. Stage B may later acquire and verify one candidate, create a credential-free sealed session, run the authorised online evidence route, and publish one sanitised receipt.
3. Stage C may later consume that exact receipt and session, prove offline isolation, perform one candidate process start, clean the process tree, restore the starting network state, and seal local evidence.
4. Stage D may later retrieve the exact Stage B receipt by run ID and attempt, validate the Stage C evidence without rerunning the candidate, and publish only the approved sanitised Gate 1 report.

Gate 1 remains **Blocked** because Stage A laptop execution has not occurred; Stages B, C and D have not run; Trusted Intel checks have not run; no candidate has been acquired or executed; the prior offline prerequisite failed before candidate execution; and the historical Gate 1 record lacks immutable source/run/candidate pairing. Gate 2 and every later gate remain prohibited.

This proposal does not authorize execution. Approval of this document would approve only the decision contract. A later C1 implementation/test package, independent review, written UCL isolation/restoration approval, valid Stage A and Stage B predecessors, and a distinct C0/user start decision are mandatory before Stage C can run.

## 2. Authoritative exactly-one-candidate-execution invariant

### 2.1 Counting event

One candidate execution is one successful operating-system process creation whose canonical executable bytes match the approved candidate executable SHA-256 and PE identity. Within the Stage C count window, the count increments when the operating system creates that process and assigns it a process identity, even if it exits immediately, crashes before producing output, is cancelled, times out, or is killed before useful work. A failed creation request that produces no process identity is an attempted invocation, not an execution; any disagreement between the creator and the independent process observer about whether a process existed makes the attempt permanently invalid.

The Stage C count window opens only after all Stage C integrity/isolation preconditions pass, the process/network observers are active, the zero-candidate baseline is sealed, and the exact manifest-bound `TrustedOffline` test identity and attempt are armed. Its opening timestamp is written before the test harness receives permission to begin the fixed test; only the capture owner then receives executable-launch capability. The window closes only after that one test ends and candidate/process/listener cleanup and continued-isolation queries pass. The observers remain active through the inclusive closing boundary, record `observerEndedAtUtc` equal to `countWindowClosedAtUtc`, and only then seal their ledgers. Every matching process start through that boundary counts. Stage B's earlier online `--version` and system starts are predecessor evidence outside this window; they neither count as Stage C starts nor permit reuse of a still-running process. Any candidate process present at the Stage C baseline blocks entry.

The acceptance invariant is:

> During one precisely bounded Stage C count window in one fresh sealed Stage B session, exactly one manifest-bound `TrustedOffline` test attempt runs. Its approved capture owner directly creates exactly one process from the approved candidate executable with exactly the approved system command. No other actor creates a process from those executable bytes during that window. The observed candidate-start count must equal one. Zero is Blocked or failed-before-execution evidence; two or more is a permanently invalid Stage C attempt.

The sole approved candidate argument vector is `--no-dashboard`, `--json`, `system`, identified remotely as `LLMFIT_V1_1_9_SYSTEM_JSON_NO_DASHBOARD_V1`. The executable path is local-only and is never serialized into accepted evidence.

### 2.2 Events that do and do not count

| Event | Counts as candidate execution? | Rule |
| --- | --- | --- |
| Operating-system creation of the approved candidate image | Yes | Count at process creation, before judging output or exit. |
| Candidate created and then crashed, timed out or was cancelled | Yes | Failure does not refund the one allowed start. |
| Candidate process created by a wrapper, provider, testhost or fallback | Yes | Ownership does not change the executable-identity count; it also violates sole-owner rules unless the direct parent is the approved capture owner. |
| Candidate `--version`, `--help`, licence, warm-up, health-check, dashboard, probe or metadata invocation | Yes | All are prohibited in Stage C because they consume or exceed the one allowed start. |
| Automatic retry or relaunch | Yes, once per created process | The first retry creates a duplicate and permanently invalidates the session. |
| A shell, testhost, capture owner, process observer or fixed Windows query helper | No | It is not the candidate image, but it must be predeclared, integrity-bound and technically unable to create the candidate except for the one capture-owner call. |
| A candidate descendant with different executable bytes | No candidate count, but prohibited | No candidate descendant helper is approved. Its creation invalidates the attempt and triggers whole-tree cleanup. |
| A descendant or second root using the candidate bytes | Yes | It is a duplicate candidate execution regardless of image name or parent. |
| Candidate cancellation, timeout, kill or cleanup operation | No new count | Cleanup must not relaunch the candidate. |
| Parsing retained JSON, hashing files, privacy scanning or generating a report | No | Post-processing must operate on existing files and be technically unable to launch the candidate. |

### 2.3 Required observable proof

Acceptance requires two independently produced, reconciled observations: the capture owner's invocation record and an operating-system process ledger. They must agree on the Stage C window, exact `TrustedOffline` identity/attempt, candidate identity, immutable candidate hash/version facts, command identifier, start count, process ID, direct parent ID, start/end UTC timestamps, exit status, timeout/cancellation result, bounded stdout/stderr counters and hashes, candidate-descendant count, network-monitor interval, cleanup result, and final local-evidence hashes. Both records are separately hashed, and the safe ledger carries those hashes plus an explicit reconciliation result. A missing observer event, duplicate event, identity mismatch or irreconcilable timestamp/parent relationship makes the session permanently invalid.

The version value is inherited from and cross-bound to the sealed Stage B receipt. Stage C verifies archive/executable hashes, PE identity, package inventory and non-executing file metadata; it does not invoke `--version`.

## 3. Prohibited ambiguity and authoritative interpretation

The existing Gate 1 spike route invokes the candidate twice: first with `--version`, then with `--no-dashboard --json system`. The runner-configuration design also describes a capture script that owns both routes, while C0 §18, P2-CRIT-02 and P1 `HI-OPS-105..107` require exactly one Stage C candidate process start. “One test,” “one wrapper,” “one capture-script call” and “one operator command” are therefore insufficient counts.

This proposal selects the higher-precedence C0/P2 interpretation: the single Stage C `TrustedOffline` test permits only the single system-JSON invocation. It prohibits the spike runner's version invocation during the Stage C count window, every direct or wrapped Stage C preflight invocation, warm-up, health check, dashboard/socket probe, retry, automatic fallback, provider-side launch and second candidate. Candidate identity/version comes from the receipt and non-executing integrity checks. Stage B's earlier online invocations remain separately bound predecessor events and must be absent at the Stage C baseline.

This selection is **OD-SC-01** and requires explicit C0/user approval because it requires a later Stage C-specific capture route rather than silent reuse of the existing two-invocation runner. Until that approval and a reviewed implementation exist, Stage C remains prohibited. The accepted LLM Fit schema/capture mapping remains a Gate 1/Gate 3 decision under P2-IMP-03; this document does not freeze a production DTO.

## 4. Approved Stage C sequence proposed for future execution

Every failure is fail-closed. “Candidate may run” means the step is permitted to create the one candidate process; every other step must be technically unable to do so. A preloaded fixed-enum writer that has no launch capability writes `StageCBlockedEnvelopeV1` for every proved zero-start failure before the count window opens and for the explicit proved-zero-start conditions after it opens: unexpected helper, network or listener activity, an isolation-query failure, or a zero-PID creation failure. If isolation has begun, failure handling still follows steps 22–23 to restore and verify the exact starting state before the envelope is final; restoration failure produces only local incident material. If even that bounded privacy-safe envelope cannot be written and validated, no artifact is created and the operator records the stop only through the approved local incident procedure.

| Step | Owner | Input | Output and evidence | Failure behavior | Candidate may run? |
| ---: | --- | --- | --- | --- | --- |
| 1 | C0 | This approved decision, separate user/UCL approvals, current gate ledger | Approval IDs, scope, expiry/conditions and approved procedure versions | Missing, expired or mismatched approval: `BlockedBeforeExecution`; stop | No |
| 2 | C1 | Exact Stage B run ID/attempt viewed on a separate trusted device | Independently transcribed receipt SHA-256; no PAT/browser/GitHub session on laptop | Missing/wrong attempt or credential exposure: invalidate intake; new Stage B required | No |
| 3 | C1 | Dedicated standard account and local machine | Local-only proof that GitHub/Workbook runners, services, scheduled starts and candidate processes are absent | Any unexpected actor/runner/process or query failure: stop | No |
| 4 | C1 | Fixed UCL-approved local NTFS session root and anchored manifest | Fresh opaque direct-child work, scratch and output leaves; ancestor/drive/ACL/encryption validation | Unsafe or pre-existing location: stop; never substitute a path | No |
| 5 | C1 | Future capture-owner environment contract | Allowlisted child environment and local no-echo rejection record for credentials, proxies and GitHub command channels | Any prohibited or unknown inherited variable: stop; do not clear-and-continue | No |
| 6 | C1 | Sealed repository, receipt and manifest | Exact ref/SHA/tree/inventory/session/run/attempt/receipt binding | Dirty tree or any binding mismatch: stop; new Stage B required | No |
| 7 | C1 | Candidate package and anchored manifest | Non-executing archive/executable hash, PE, package inventory, licence/signature observation and capture-owner hash validation | Any mismatch or query error: stop before isolation | No |
| 8 | C1 | Approved command contract | Exact command ID and three-argument allowlist; proof of no shell/free-form arguments | Any alternate command, wrapper or option: stop | No |
| 9 | C1 | Anchored output commitment | Proof that the exact predeclared output leaf is absent and fresh | Existing/stale/unknown file: stop; new Stage B session required | No |
| 10 | C1 | UCL-approved isolation/restoration procedure | Local-only starting state for every physical, wireless, VPN, mobile and virtual adapter; physical access confirmed | Missing device class, state or rollback proof: stop | No |
| 11 | C1 | Starting-state record | Manual disablement of every non-loopback interface through the approved Windows/UCL mechanism | No script changes adapters; any failure stops | No |
| 12 | C1 | Isolated machine | Positive preflight: no network availability, no up non-loopback interface, no usable non-loopback/default route, successful DNS/proxy/listener/process queries | Any positive finding, ambiguity or query failure: `BlockedBeforeExecution`; zero candidate starts | No |
| 13 | C1 | Approved OS-level process and network observer | Observer active; pre-window process, TCP/UDP endpoint and listener baseline sealed locally | Observer unavailable, sampled-only, incomplete or failed: write Blocked envelope and stop | No |
| 14 | C1 | Observer baseline and anchored test manifest | Zero pre-existing process matching candidate bytes; exact `TrustedOffline` category/identity digest, one-test filter, one attempt, approved Stage C local-reference policy and approved capture-owner hash validated | Existing/unknown candidate, wrong/duplicate test, retry configuration, unapproved reference policy or second launch-capable actor: write Blocked envelope and stop | No |
| 15 | Exact `TrustedOffline` test under C1 | Validated test identity, candidate handle, command ID, fresh output leaf and OD-SC-05 approval | Open count window; run exactly one test attempt; capture only the allowlisted local Windows reference-before fields; then its capture owner direct-creates exactly one candidate process with no shell; creator and OS events recorded | Helper/network/listener/query anomaly or zero-PID start failure with proved zero starts: fixed-reason Blocked envelope; any PID/count ambiguity or second start: permanent invalidity | **Yes — inside this one test only** |
| 16 | C1 observer | Candidate PID/tree and bounded streams | Continuous process/network observation; stdout/stderr drained with independent 1,048,576-byte caps; timestamps and hashes only in accepted evidence | Socket/network event, descendant, second start, truncation, nonzero exit or observer failure: kill tree and invalidate; no test retry | No additional start |
| 17 | Exact `TrustedOffline` test/capture owner under C1 | Test completion, cancellation request or 1–120 second approved timeout | Exit/cancel/timeout classification; whole-tree termination and awaited stream drains; on normal exit, immediate allowlisted local Windows reference-after RAM capture within the approved 30-second comparison interval | No retry. Failed/late reference capture or failure to terminate is permanent invalidity; termination failure is cleanup failure | No |
| 18 | C1 | Still-isolated machine and observer | Zero residual candidate process/descendant/TCP/UDP endpoint/listener; continued isolation proof | Any residue or query failure: remain isolated, escalate locally, do not continue | No |
| 19 | C1 | Completed test and successful cleanup | At one observer-recorded boundary close the count window and end observation; then seal invocation/process/network ledgers and the exact one-test TRX with one discovered/executed result and zero skips | Missing/malformed TRX, wrong identity/count, unequal close/end timestamps or ledger failure: invalidate | No |
| 20 | C1 | Raw bounded capture, Stage C local Windows reference, TRX and observer logs | Local-only raw files, independently recomputed SHA-256 values and the bounded policy-derived comparison booleans/deltas/tolerances/authority reference in §9; the Stage B Windows reference remains separately bound predecessor evidence and is not used as the Stage C 30-second endpoint | Write/hash/comparison failure: no accepted artifact; retain only approved local incident material | No |
| 21 | C1 | Sealed local hashes and records | Preliminary exact test/count, creator/observer reconciliation, comparison, binding, schema and privacy validation | Any failure: no publishable artifact; attempt permanently invalid where execution occurred | No |
| 22 | C1 | Starting adapter-state record and successful cleanup proof | Manual restoration to the exact recorded state | Restoration failure: no accepted ledger, runner restart or Stage D; escalate under UCL procedure | No |
| 23 | C1 | Restored machine | Successful adapter/connectivity query and proof no candidate/residual listener remains | Any query failure: no accepted ledger; Stage D prohibited | No |
| 24 | C1 | Validated evidence plus restoration proof | Final safe Stage C execution ledger written atomically to a fresh file and revalidated | Write/atomicity/validation failure: no accepted artifact | No |
| 25 | C0, with independent P3/R* review | Final safe ledger and local hashes | Accept for Stage D intake or retain a fixed non-acceptance result | Stage C cannot assign Gate 1 acceptance or authorize Stage D itself | No |

## 5. Positive fail-closed offline-isolation proof

Offline acceptance is a conjunction, not an expectation and not merely a sampled socket-table absence.

### 5.1 Entry proof

Before the candidate starts, all of these must be observed successfully:

- every physical, wireless, VPN, mobile and virtual network interface is inventoried locally and every non-loopback interface is manually disabled;
- `GetIsNetworkAvailable` is false;
- the count of operationally up non-loopback interfaces is zero;
- IPv4 and IPv6 route queries succeed and show zero usable non-loopback routes and zero default routes;
- DNS configuration queries succeed and show zero DNS-eligible operational interfaces; no DNS lookup is performed as a test;
- case-insensitive process, user and machine proxy-environment checks find no proxy variable; WinHTTP/WinINET proxy queries succeed and show no active proxy or automatic-configuration route available to the candidate;
- process, TCP, UDP and listener queries succeed; the baseline is sealed locally without exporting addresses or adapter identities;
- the approved OS-level observer is already recording process-tree and TCP/UDP events before process creation.

Configuration that exists but is unreachable is recorded locally; this proposal does not authorize deleting DNS, proxy, VPN or unrelated machine settings. If a required safe state cannot be reached through the UCL-approved manual procedure, Stage C stops.

### 5.2 Continuous proof

From the final preflight through candidate/process/listener cleanup, the observer must continuously cover:

- non-loopback interface and route state changes;
- candidate-tree TCP IPv4/IPv6 connect, accept and listen events;
- candidate-tree UDP IPv4/IPv6 bind, send and receive events;
- DNS and proxy use by the candidate tree;
- loopback endpoints, port 8787, dashboards, services and any other socket;
- new listeners or endpoints that cannot be safely attributed to the sealed baseline;
- candidate process creation, descendants and exit.

Any candidate network event, including loopback; any new candidate-owned listener or UDP endpoint; any non-loopback route/interface becoming usable; any unexpected network activity; or any observer/query gap invalidates the attempt, triggers whole-tree cleanup, and forbids restoration until residue checks succeed. The observer mechanism must be a C0/UCL-approved operating-system event source capable of covering the stated TCP/UDP behavior. A sampled `netstat`-style observer may corroborate but cannot be the sole proof, consistent with `HI-SEC-017`. Selection and validation of that mechanism is **OD-SC-02**; until resolved, no Stage C run is executable.

The no-route proof establishes that the candidate cannot silently reach a non-loopback network route; the continuous OS event proof establishes that it did not create a loopback/dashboard/socket route without detection. A failed isolation query is evidence of unknown state, never evidence of absence, and stops the run.

### 5.3 Teardown and restoration

Isolation remains in force until the candidate root, all descendants, candidate-matching processes, TCP/UDP endpoints and listeners are proved absent and every observer has completed successfully. Only then may C1 manually restore each adapter to its recorded starting state. Restoration must not enable a previously disabled adapter. Connectivity is checked only after restoration, and no runner may restart until restoration and zero-residue checks pass. Failure to restore is an operational incident and blocks Stage D; it does not authorize broad repair or unrelated network changes.

## 6. Candidate execution boundary

The approved process is the single candidate executable identified by the sealed manifest: project `AlexsJones/llmfit`, tag `v1.1.9`, release commit `a02e13f1013ed69889ff44426a651bf7c68c292e`, archive `llmfit-v1.1.9-x86_64-pc-windows-msvc.zip` with SHA-256 `a030269d7cc8a5bf40383f526a481655d698ec71dd792a25b06510cef9f8b738`, executable SHA-256 `db82bcb17f065b7ff7528ffe9904b2b0e1cce0c843fcd659b4ee1432a2e72e19`, and AMD64 PE identity. These are intended identity facts, not evidence that the candidate has been acquired or run.

The later implementation must direct-create the canonical local executable with no shell, no script wrapper, no PATH lookup, no free-form operator argument, and exactly the three system arguments. `--version`, `--help`, `serve`, dashboard, REST, recommendation, fit, plan, warm-up, health-check, metadata probing, provider fallback, a second candidate, and fallback versions are prohibited. Candidate redistribution, modification, patching and output outside the approved fixed-local boundary are prohibited.

Only the approved capture owner may hold a launch capability. The operator, harness, observer, parser, cleanup component and report generator must not expose any callable candidate-start route. A second dispatch, retry or uncertain count requires a new Stage B session; the failed session is never reused.

## 7. Environment and filesystem boundary

The sealed session must be an ordinary independent repository under a fixed UCL-approved local NTFS root, outside tracked trees, runner workspaces, profiles, personal folders, OneDrive/synchronised/backup folders and unrelated data. The root must have restrictive ACLs, approved encryption at rest and a non-identifying opaque session ID.

Every work, scratch and output location is a fresh opaque direct child declared in the anchored manifest. The implementation must canonicalize the volume, root, every existing ancestor and leaf; verify containment with separator-aware comparison; verify a fixed local drive type; and reject UNC paths, device paths, network or removable drives, alternate streams, hardlinks, symlinks, junctions, mount points and every reparse point. A pre-existing work/output directory, unknown file, containment ambiguity or failed metadata query stops the run. Cleanup targets are exact independently revalidated leaves only; parent, wildcard, cache-wide and profile-wide cleanup is prohibited.

The candidate child receives a constructed environment, not the inherited operator or runner environment. The proposed minimum allowlist is `SystemRoot`, `WINDIR`, `TEMP` and `TMP`; the Windows roots must resolve to the approved system location and the temporary variables to a fresh session scratch child. `PATH`, credentials, tokens, PATs, browser state, SSH variables, proxy variables, debug/trace variables, hooks, action-cache overrides, `ACTIONS_*`, `RUNNER_*`, `GITHUB_*`, including GitHub command-file variables, and every unknown operational variable are excluded case-insensitively. If the later implementation proves another variable necessary, it requires C0 security review and a versioned allowlist change; it cannot be added at run time.

## 8. Process ownership, containment and cleanup

The approved capture owner is the direct parent of the one candidate PID. Its own executable hash and PID are recorded. No shell or wrapper may sit between them. All predeclared non-candidate helpers are identified and integrity-bound before isolation; none receives a candidate handle or launch capability.

The candidate is placed under one non-inheritable, kill-on-close whole-tree containment boundary before it may run. Cancellation, timeout, stream-capture failure, observer failure, crash or unexpected child creation terminates the whole owned tree and awaits root, descendants and stream pumps. Candidate descendants are not approved; any descendant is fatal, and a descendant using candidate bytes also increments the candidate execution count.

Cleanup verifies by identity and ownership, not by global image-name killing. It may terminate only the captured process tree. It must never kill unrelated global processes, delete broad directories or continue after failed termination. Hung processes, residual listeners/endpoints, unknown PIDs or failed process/listener queries keep the machine isolated and require local UCL escalation. No safe artifact, network restoration, runner restart or Stage D transition occurs after failed cleanup until the approved incident procedure proves the machine safe.

## 9. Minimum accepted Stage C evidence schema

Both Stage C records are strict UTF-8 JSON objects written atomically to fresh local files. Unknown, duplicate or missing properties; non-canonical encodings; out-of-range numbers; non-UTC timestamps; invalid enums; or trailing bytes are rejected. Strings are fixed enums, hashes, Git identifiers or bounded opaque IDs only; arbitrary text is prohibited.

### 9.1 Zero-start blocked envelope

`StageCBlockedEnvelopeV1` is the only record allowed when a pre-execution condition fails and the Stage C start count is provably zero. Its exact fields are:

| Exact field | Type and allowed value | Rule |
| --- | --- | --- |
| `schemaVersion` | string, exactly `hi.stage-c.blocked-envelope.v1` | Required |
| `stage` | string, exactly `C` | Required |
| `privacyClass` | string, exactly `LocalSanitised` | Required |
| `status` | string, exactly `BlockedBeforeExecution` | Required; not candidate rejection or Gate 1 disposition |
| `reasonCode` | enum `MissingApproval`, `ReceiptIntakeFailed`, `UnexpectedRunnerOrActor`, `UnsafeWorkspace`, `EnvironmentRejected`, `SourceBindingFailed`, `CandidateIdentityFailed`, `CommandContractFailed`, `OutputLeafNotFresh`, `IsolationNotEstablished`, `ObserverUnavailable`, `TrustedOfflineIdentityFailed`, `UnexpectedHelperBeforeCandidate`, `NetworkDetectedBeforeCandidate`, `IsolationQueryFailedBeforeCandidate`, `ListenerFoundBeforeCandidate`, `CandidateStartFailedWithZeroPid` | Exactly one fixed reason |
| `candidateStartCount` | unsigned integer, exactly `0` | Required; the writer has no launch capability |
| `occurredAtUtc` | RFC 3339 UTC string with fractional seconds | Required |
| `approvalId` | `[A-Za-z0-9._-]{1,64}` string or null | Null only for `MissingApproval` |
| `stageBRunId`, `stageBRunAttempt` | unsigned 64-bit and unsigned 32-bit integers, or null | Present only after receipt intake succeeds |
| `sessionId` | exactly 32 lowercase hex characters or null | Present only after session binding succeeds |
| `stageBReceiptSha256` | lowercase 64-hex string or null | Present only after receipt validation succeeds |
| `countWindowOpenedAtUtc`, `countWindowClosedAtUtc`, `observerStartedAtUtc`, `observerEndedAtUtc` | RFC 3339 UTC strings with fractional seconds, or null | All null if the window never opened; otherwise all present, observer start precedes window open, and observer end equals window close after zero candidate starts are proved |
| `cleanupResult` | enum `NotRequired`, `Completed`, `Failed` | `NotRequired` before isolation; otherwise `Completed` is required for a valid envelope |
| `isolationRestoredToStartingState` | boolean | True: isolation never began or the exact starting state was restored |
| `privacyScan` | string, exactly `Passed` | If the envelope cannot pass, create no artifact |

This envelope is justified by P1 `HI-OPS-103..104`, P2-IMP-08 and the historical distinction between a failed offline prerequisite and candidate rejection. It contains no candidate PID, output hash or fabricated execution fact; observer/window times appear only when they prove a post-open zero-start stop.

### 9.2 Execution ledger

`StageCExecutionLedgerV1` is required once the observer reports any Stage C candidate process creation. Only a fully valid `CompletedForStageDReview` ledger is acceptance-eligible.

| Exact field(s) | Type and allowed value | Required acceptance condition | Source justification |
| --- | --- | --- | --- |
| `schemaVersion` | string, exactly `hi.stage-c.execution-ledger.v1` | Present | P3 §11 common versioned binding; user-required schema |
| `stage` | string, exactly `C` | Present | P1 `HI-OPS-090..111`; P3 §11.3 |
| `privacyClass` | string, exactly `LocalSanitised` | Present | P3 §§8.4, 11.3 and 15; raw evidence is separate/local |
| `status` | enum `CompletedForStageDReview`, `InvalidAfterExecution`, `CleanupFailed` | Only `CompletedForStageDReview` is acceptance-eligible; no value is a Gate 1 disposition | P2 Stage C audit; P3 §11.3 |
| `approvalId`, `isolationProcedureVersion`, `observerPolicyVersion`, `environmentPolicyVersion`, `privacyPolicyVersion` | strings matching `[A-Za-z0-9._-]{1,64}` | Each matches the approved decision set | P3 §§11.2–11.3 approval/schema/policy fields; C0 §18 |
| `sourceRef` | string, exact approved branch/ref, max 128 ASCII characters | Matches receipt and sealed repository | C0 §19; P3 §11.1 |
| `sourceCommit`, `releaseCommit` | lowercase 40-hex strings | Exact approved values | C0 §19; P3-IMP-03 |
| `sourceTreeSha256`, `sourceInventorySha256`, `stageBReceiptSha256`, `manifestSha256` | lowercase 64-hex strings | Recomputed values all match anchored inputs | C0 §§18–19; P2-CRIT-03; P3 §11 |
| `stageBRunId` | unsigned 64-bit integer | Exact independently viewed run | P1 `HI-OPS-094`; C0 §19 |
| `stageBRunAttempt` | unsigned 32-bit integer, minimum 1 | Exact independently viewed attempt | P1 `HI-OPS-094`; P3 §11.3 |
| `sessionId` | string, exactly 32 lowercase hex characters | Matches receipt and manifest; never reused | Runner design §7; C0 §18 |
| `candidateProject`, `candidateTag`, `archiveName`, `peMachine` | exact fixed strings from §6 | All match receipt/manifest | Gate 1 spike §1; P3-IMP-03 |
| `archiveBytes` | unsigned 64-bit integer, exactly `5255910` | Match | Gate 1 spike §1 |
| `archiveSha256`, `candidateExecutableSha256`, `captureOwnerSha256` | lowercase 64-hex strings | Recomputed and matched before start; executable rechecked after exit | Gate 1 spike Tasks 1/3; C0 §18 process ledger |
| `candidateVersion` | string, exactly `1.1.9` | Bound to Stage B receipt; not obtained by Stage C execution | Gate 1 spike §1; P2-CRIT-02 remedy |
| `candidateVersionSource` | enum, exactly `StageBReceiptAndNonExecutingMetadata` | Present | P2-CRIT-02 prohibits Stage C version execution |
| `commandId` | string, exactly `LLMFIT_V1_1_9_SYSTEM_JSON_NO_DASHBOARD_V1` | Present | Gate 1 spike Task 2; C0 §19 forbids raw path-bearing commands |
| `argumentCount` | unsigned integer, exactly `3` | Present | Exact argument allowlist |
| `trustedOfflineCategory` | string, exactly `TrustedOffline` | Present | P1 `HI-OPS-105`; runner design §6.4 |
| `trustedOfflineIdentitySha256` | lowercase 64-hex string | Matches the exact fully qualified test identity committed by the anchored manifest | C0 §19 exact test identities; avoids exporting an implementation string before OD-SC-01 approval |
| `trustedOfflineAttempt`, `trustedOfflineDiscovered`, `trustedOfflineExecuted`, `trustedOfflinePassed`, `trustedOfflineFailed`, `trustedOfflineSkipped` | unsigned integers | Exactly `1`, `1`, `1`, `1`, `0`, `0` for acceptance | P1 `HI-OPS-105..107`; `HI-TEST-025`, `HI-TEST-039` |
| `offlineComparisonPolicyVersion` | string matching `[A-Za-z0-9._-]{1,64}` | Matches anchored manifest | P3 §11.3 target-comparison policy |
| `comparisonAuthorityId` | string, exactly `GATE1_TASK8_STAGEC_WINDOWS_REFERENCE_V1` | Identifies the separately approved Stage C-local use of the Task 8 Windows reference rules without exporting target identity | Gate 1 spike Task 8 Steps 2–3; P3 §11.3 authority reference; OD-SC-05 |
| `requiredCpuRamJson`, `cpuIdentityMatch`, `logicalProcessorMatch` | booleans | All true for acceptance | Gate 1 spike Task 8 Step 3; P3 §11.3 comparison booleans |
| `logicalProcessorDelta` | signed 32-bit integer | Exactly `0` | Gate 1 spike Task 8 requires equality; P3 §11.3 comparison delta |
| `totalRamDeltaBytes`, `availableRamDeltaBytes` | signed 64-bit integers | Absolute values are within their recorded tolerances | Gate 1 spike Task 8 Step 3; P3 §11.3 comparison deltas |
| `totalRamToleranceBytes` | unsigned 64-bit integer, exactly `1073741824` | Present | Gate 1 spike Task 8 one-GiB rule; P3 §11.3 tolerance |
| `availableRamToleranceBytes` | unsigned 64-bit integer | Equals the locally validated maximum of `2147483648` and ten percent of the bound Windows total-memory value, rounded upward to the next byte | Gate 1 spike Task 8 two-GiB/ten-percent rule; P3 §11.3 tolerance |
| `referenceCaptureSeparationMilliseconds` | unsigned 32-bit integer | Measures the Stage C-local reference/candidate interval and is at most `30000`; a Stage B interval cannot populate this field | Gate 1 spike Task 8 30-second boundary; OD-SC-05 |
| `intelGpuIdentityResult` | enum `Matched`, `Gap` | Either value is accepted as a field-level observation | Gate 1 spike Task 8 Step 3 |
| `intelGpuMemorySemantics` | string, exactly `NotEstablished` | Present | Gate 1 spike Task 8 explicit non-claim |
| `intelNpuDetection` | string, exactly `DetectionUnavailable` | Present | Gate 1 spike Task 8 explicit non-claim |
| `offlineComparisonResult` | enum `Passed`, `Failed` | `Passed` for acceptance | Gate 1 spike Task 8; P3 §11.3 |
| `candidateStartCount` | unsigned 32-bit integer | Exactly `1` for acceptance; zero belongs only in `StageCBlockedEnvelopeV1`; any value above `1` is permanent invalidity | P2-CRIT-02; C0 §18; P1 `HI-OPS-105..107` |
| `captureOwnerProcessId` | unsigned 32-bit integer | Present for every ledger | C0 §18 sole capture-owner ledger |
| `candidateProcessId`, `candidateParentProcessId` | unsigned 32-bit integers, or null only for a permanently invalid duplicate/ambiguous attempt | For acceptance both are present, candidate parent equals capture owner, and observer/creator agree | User-required process identity/relationship; C0 §18 ledger |
| `candidateDescendantCount`, `candidateMatchingOtherProcessCount` | unsigned integers | Both exactly `0` | P2-CRIT-02; whole-tree/no-second-execution boundary |
| `countWindowOpenedAtUtc`, `startedAtUtc`, `endedAtUtc`, `countWindowClosedAtUtc` | RFC 3339 UTC strings with fractional seconds | Strictly ordered; the window brackets the candidate and cleanup | P2-CRIT-02; C0 §18 |
| `observerStartedAtUtc`, `observerEndedAtUtc` | RFC 3339 UTC strings with fractional seconds | Observer starts before the count window and `observerEndedAtUtc` equals `countWindowClosedAtUtc`, proving continuous coverage through the inclusive closing boundary | P2-IMP-08; C0 §18 |
| `completionKind` | enum `Exited`, `TimedOut`, `Cancelled`, `Crashed`, `ObserverFailed`, `CleanupFailed` | `Exited` required for acceptance | Block 2 §§15/57; P1 `HI-SEC-013..014` |
| `exitCode` | signed 32-bit integer or null | `0` required for acceptance; null only when no exit code exists | P1 `HI-OPS-107`; production design §8 |
| `stdoutByteCount`, `stderrByteCount` | unsigned integer, maximum `1048576` each | Within caps | Gate 1 spike Task 5; P1 `HI-SEC-012` |
| `stdoutSha256`, `stderrSha256` | lowercase 64-hex or null only for zero bytes | Hashes only; raw content excluded | User-required handling; P3 §§8.4/15 |
| `stdoutTruncated`, `stderrTruncated` | boolean | Both false | Gate 1 spike Task 5 and evidence schema |
| `networkBefore`, `networkAfterCleanupBeforeRestoration` | objects with exactly `getIsNetworkAvailable` boolean; `nonLoopbackUpInterfaceCount`, `usableNonLoopbackRouteCount`, `defaultRouteCount`, `dnsEligibleInterfaceCount`, `queryFailureCount` unsigned integers; `proxyEnvironmentPresent`, `activeProxyRoutePresent` booleans | In both objects the boolean availability/proxy values are false and all counts are zero | P1 `HI-OPS-102..104`; P2-IMP-08; user-required DNS/proxy proof |
| `networkDuring` | object with exactly `monitorContinuous` boolean and unsigned integer fields `availabilityTrueEventCount`, `interfaceUpEventCount`, `routeBecameUsableEventCount`, `candidateTcpEventCount`, `candidateUdpEventCount`, `candidateDnsEventCount`, `candidateListenerCount`, `unexpectedNetworkEventCount`, `queryFailureCount` | `monitorContinuous` is true and every count is zero | Block 2 §§57/76; P1 `HI-SEC-015..017`, `HI-SEC-046`; P2-IMP-08 |
| `isolationRestoredToStartingState` | boolean | True after cleanup | P1 `HI-OPS-108..110` |
| `residualCandidateProcessCount`, `residualDescendantCount`, `residualCandidateEndpointCount`, `residualCandidateListenerCount` | unsigned integers | All zero | P1 `HI-SEC-014`, `HI-SEC-035`, `HI-OPS-108` |
| `cleanupResult` | enum `NotRequired`, `Completed`, `Failed` | `Completed` after any candidate start | C0 §18; P3 §11.4 |
| `outputLeafWasAbsentAtEntry` | boolean | True | P1 `HI-OPS-099..101` |
| `outputLeafCommitmentSha256`, `stageCWindowsReferenceEvidenceSha256`, `rawSystemJsonSha256`, `offlineTrxSha256`, `captureOwnerInvocationRecordSha256`, `processObserverLedgerSha256`, `networkLedgerSha256` | lowercase 64-hex strings | All locally recomputed and non-null; the Stage C reference hash covers only the allowlisted local reference used for the comparison and is cross-bound inside offline evidence | P3 §§11.3–11.4; C0 §§18–19; Gate 1 spike Task 8; raw files remain local |
| `invocationReconciliation`, `bindingValidation`, `schemaValidation`, `privacyScan` | enum `Passed`, `Failed` | All `Passed` | P2-CRIT-02/03; P3 §§11.3/15; `HI-TEST-022..023`, `HI-TEST-035` |
| `futureArtifactName`, `futureArtifactFileName` | strings, exactly `hardware-inspection-llmfit-gate1-report` and `hardware-inspection-llmfit-gate1-report.md` | Proposed association only under OD-SC-04; Stage C performs no upload | Runner design §6.5; P3 §11.4; exact names require C0/user approval |

The schema excludes raw model paths, raw candidate paths, other raw paths, credentials, tokens, host/account/user identity, adapter names, IP/MAC/network addresses, device identifiers, hardware names, raw commands, raw stdout/stderr, raw JSON, raw TRX, native errors, stack traces, dumps and arbitrary unbounded text. Those fields are forbidden both directly and transitively.

## 10. Artifact rules

Stage C publishes nothing. Candidate files, raw candidate JSON, raw Windows/offline evidence, process/network logs and TRX remain ignored, encrypted, fixed-local, outside tracked and runner trees, and are referenced only by SHA-256 from the safe ledger.

If Stage D is later authorised, its sole publishable artifact container is proposed as `hardware-inspection-llmfit-gate1-report`, containing exactly one file named `hardware-inspection-llmfit-gate1-report.md`. The sources require one sanitised Markdown report but do not freeze these two names; adopting them is **OD-SC-04** and requires explicit C0/user approval. Until then, publication remains disabled. No JSON status summary is approved by this proposal.

The only artifact this proposal permits a later approved Stage D to publish is the artifact named `hardware-inspection-llmfit-gate1-report`, containing exactly one file named `hardware-inspection-llmfit-gate1-report.md`. It uses the existing sanitised Gate 1 Markdown report contract: source/run/session/candidate binding, bounded enums/counts/booleans/hashes, exact disposition and non-claims; no raw candidate evidence. No optional machine-readable artifact is approved by this proposal.

### 10.1 Exact published Markdown schema

The file begins with the exact title `# Hardware Inspection LLM Fit Gate 1 report` and then contains the following sections in order. Every table has exactly the columns `Field` and `Value`; row order is fixed as listed. Values use the types and allowed domains in §9 or the cited Stage B/Gate 1 source. No other section, row, column, free-form paragraph or appended byte is allowed.

| Section | Exact ordered field rows | Type/domain |
| --- | --- | --- |
| `## Binding` | `Schema version`; `Source ref`; `Source commit`; `Stage B run ID`; `Stage B run attempt`; `Session ID`; `Stage B receipt SHA-256`; `Manifest SHA-256`; `Stage C ledger SHA-256`; `Candidate project`; `Candidate tag`; `Release commit`; `Archive name`; `Archive bytes`; `Archive SHA-256`; `Executable SHA-256`; `PE machine`; `Candidate version`; `Candidate version source`; `Command ID` | Schema exactly `hi.gate1.report.v1`; identifiers/hashes/fixed candidate values exactly as in §9 and the bound Stage B receipt |
| `## Execution` | `TrustedOffline category`; `TrustedOffline identity SHA-256`; `TrustedOffline attempt`; `TrustedOffline discovered`; `TrustedOffline executed`; `TrustedOffline passed`; `TrustedOffline failed`; `TrustedOffline skipped`; `Candidate start count`; `Started UTC`; `Ended UTC`; `Completion kind`; `Exit code`; `Stdout byte count`; `Stdout SHA-256`; `Stdout truncated`; `Stderr byte count`; `Stderr SHA-256`; `Stderr truncated`; `Capture-owner invocation record SHA-256`; `Process observer ledger SHA-256`; `Network ledger SHA-256`; `Invocation reconciliation` | Exact ledger fields; acceptance requires `TrustedOffline`, counts `1/1/1/1/0/0`, candidate count `1`, `Exited`, exit `0`, no truncation and `Passed` reconciliation |
| `## Controlled comparison` | `Stage C Windows reference evidence SHA-256`; `Comparison policy version`; `Comparison authority ID`; `Required CPU/RAM JSON`; `CPU identity match`; `Logical processor match`; `Logical processor delta`; `Total RAM delta bytes`; `Total RAM tolerance bytes`; `Available RAM delta bytes`; `Available RAM tolerance bytes`; `Reference capture separation milliseconds`; `Intel GPU identity result`; `Intel GPU memory semantics`; `Intel NPU detection`; `Offline comparison result` | Exact §9 fields; acceptance requires the Stage C-local reference hash, stated booleans/results, zero logical delta, RAM deltas within recorded tolerances and Stage C separation no greater than `30000`; GPU `Gap` remains non-fatal and the GPU-memory/NPU non-claims remain fixed |
| `## Offline and cleanup` | Every §9 `networkBefore` property prefixed `Before`; every `networkDuring` property prefixed `During`; every `networkAfterCleanupBeforeRestoration` property prefixed `After cleanup before restoration`; `Candidate descendant count`; `Other candidate process count`; `Residual candidate process count`; `Residual descendant count`; `Residual candidate endpoint count`; `Residual candidate listener count`; `Cleanup result`; `Isolation restored to starting state`; `Binding validation`; `Schema validation`; `Privacy scan` | Exact booleans/counts/enums from §9; all failure/event/residue counts zero, cleanup `Completed`, restoration true, validations `Passed` |
| `## Input hashes` | `Trusted candidate evidence SHA-256`; `Stage B Windows reference evidence SHA-256`; `Offline evidence SHA-256`; `Deterministic TRX SHA-256`; `Trusted Windows TRX SHA-256`; `Offline TRX SHA-256` | Six lowercase 64-hex report-generator input hashes, each recomputed by Stage D; trusted-candidate evidence cross-binds the separate Task 8 guard TRX hash, while offline evidence cross-binds the distinct Stage C-local reference hash; the Stage B reference is predecessor trusted-comparison evidence and cannot populate the Stage C interval |
| `## Test outcomes` | `Deterministic category`; `Deterministic identity-set SHA-256`; `Deterministic discovered`; `Deterministic executed`; `Deterministic passed`; `Deterministic failed`; `Deterministic skipped`; `Task 8 category`; `Task 8 guard 1`; `Task 8 guard 2`; `Task 8 guard 3`; `Task 8 guard TRX SHA-256`; `Task 8 discovered`; `Task 8 executed`; `Task 8 passed`; `Task 8 failed`; `Task 8 skipped`; `Trusted Windows category`; `Trusted Windows test 1`; `Trusted Windows test 2`; `Trusted Windows test 3`; `Trusted Windows Intel discovered`; `Trusted Windows Intel executed`; `Trusted Windows Intel passed`; `Trusted Windows Intel failed`; `Trusted Windows Intel skipped`; `Controlled offline discovered`; `Controlled offline executed`; `Controlled offline passed`; `Controlled offline failed`; `Controlled offline skipped` | Categories exactly `Deterministic`, `Task8Deterministic`, and `TrustedWindowsIntel`; the deterministic identity-set digest matches the ordered set in the Stage B receipt and every digest is lowercase 64-hex; Task 8 identities are exactly `ArtifactStringShape_RejectsPathsAndFreeTextWithGenericDiagnostics`, `CaptureInterval_ThirtySecondsPlusOneTickIsOutsideBoundary`, and `StableFileIdentityAndProcessTreeCleanup_AreFailClosed`; trusted identities are exactly `TrustedCandidate_IdentityVersionAndCpuRamSchemaPass`, `TrustedCandidate_CpuAndRamAgreeWithNearSimultaneousWindowsReference`, and `TrustedCandidate_LeavesNoDashboardListenerOrProcess`; unsigned counters are exactly `174/174/174/0/0`, `3/3/3/0/0`, `3/3/3/0/0`, and `1/1/1/0/0` |
| `## Packaging observations` | `Licence`; `Authenticode status`; `Dependency licence status`; `Redistribution approved` | Licence exactly `MIT`; Authenticode enum `NotSigned` or `PresentUnverified`; dependency enum `Pending` or `ApprovedByNamedPolicy`; boolean redistribution value, which this proposal cannot set true |
| `## Disposition and non-claims` | `Gate 1 disposition`; `Candidate decision made`; `Gate 1 satisfied`; `Gate 2 permitted`; then the eight fixed non-claim list items below | Disposition enum `Blocked`, `Rejected`, `FunctionalPassWithPackagingConcern`, `AcceptedForFunctionalEvaluation`; remaining values are fixed enums/booleans determined by the approved Gate 1 rule |

The eight exact non-claim list items are:

1. `No production binary has been approved or committed.`
2. `No WinUI or HardwareSnapshot implementation exists.`
3. `No Windows Intel dedicated/shared GPU memory conclusion was established.`
4. `No Intel NPU presence or absence was established.`
5. `No model compatibility conclusion was made.`
6. `This report does not verify F-M07, HE-01, or HE-02.`
7. `This report does not approve candidate redistribution.`
8. `Gate 2 remains prohibited unless C0 records a separate entry-permitting decision.`

Before publication, D1 must re-retrieve the exact Stage B receipt by run ID and attempt, rehash every Stage C input, validate the strict schemas, verify candidate-start count equals one, verify cleanup/restoration, scan prohibited fields, and open the output through a safe handle immediately before upload. After publication, E1/P3 must retrieve that exact artifact by run ID and attempt and verify artifact name, one-file inventory, exact filename, bytes, SHA-256, Markdown schema, source/run/session/candidate association and privacy result. A local validation or green badge is not post-publication proof.

Any post-publication mismatch makes the Stage D attempt permanently invalid. E1 is the detection owner; D1 must remove the exact run/attempt artifact through the approved platform operation and E1 must independently verify absence. If removal or absence verification fails, Gate 1 remains Blocked, the artifact is catalogued as invalid and must not be linked or consumed, and C0/UCL incident handling is required. No replacement upload occurs in the same Stage D attempt. A privacy, binding or candidate-count failure also permanently invalidates the underlying Stage C session; no later publication may reuse it.

Retention is event-bound: retain the later remote sanitised artifact only through independent C0/P3 Gate 1 review, then remove it at the first approved cleanup event; never retain it beyond laptop return, and any shorter UCL policy wins. Local raw evidence and the sealed session follow the same maximum boundary and are cleaned only by the explicit UCL-approved exact-target procedure. If C0/UCL requires a numeric platform retention value, that value is **OD-SC-03** and publication remains disabled until approved.

No accepted or publishable artifact may exist after a privacy, binding, count, schema or cleanup failure. Local incident material may remain only under the UCL-approved isolation/incident procedure; it is not an accepted Stage C artifact and must never be uploaded.

## 11. Failure matrix

| State | Detection owner | Fixed failure result | Candidate execution occurred? | Cleanup required? | Artifacts may exist? | Permanently invalid? |
| --- | --- | --- | --- | --- | --- | --- |
| Missing approval | C0/C1 | `BlockedBeforeExecution` | No | No | Validated Blocked envelope only, if safely writable | No; new approval may begin new intake |
| Wrong source/ref/SHA/tree | C1 | `BlockedBeforeExecution` | No | No | Validated Blocked envelope only | Yes for session; new Stage B required |
| Wrong run ID | C1 | `BlockedBeforeExecution` | No | No | Validated Blocked envelope only | Yes for intake |
| Wrong candidate hash/PE/package | C1 | `BlockedBeforeExecution` | No | No | Validated Blocked envelope only | Yes for session |
| Duplicate candidate invocation | OS observer/C1 | `InvalidAfterExecution` | Yes, two or more | Yes | Local incident only | Yes |
| Candidate retry/relaunch | OS observer/C1 | `InvalidAfterExecution` | Yes | Yes | Local incident only | Yes |
| Unexpected helper/descendant invocation | OS observer/C1 | `BlockedBeforeExecution` before candidate creation; otherwise `InvalidAfterExecution` | No before start; yes after start | Stop and escalate for an unowned helper; terminate only a process proved inside the predeclared owned boundary; clean the candidate tree if started; never kill an unowned/global process | Validated Blocked envelope before start; otherwise local incident only | Yes for the session |
| Network detected or unexpected activity | Network observer/C1 | `BlockedBeforeExecution` before candidate creation; otherwise `InvalidAfterExecution` | According to timestamp | Yes if process started | Validated Blocked envelope before start; otherwise local incident only | Yes for the session |
| Isolation query failure | C1 | `BlockedBeforeExecution` before candidate creation; otherwise `InvalidAfterExecution` | No before start; yes during | Yes during | Validated Blocked envelope before start; otherwise local incident only | Yes for the session |
| Candidate/new unattributed listener found | Observer/C1 | `BlockedBeforeExecution` before candidate creation; otherwise `InvalidAfterExecution` | No before candidate creation; yes after | Exact listener-owner/candidate-tree cleanup only if owned and approved | Validated Blocked envelope before candidate creation; otherwise local incident only | Yes for the session |
| Timeout | Capture owner/C1 | `InvalidAfterExecution` with `TimedOut` | Yes | Yes | Local incident/raw bounded evidence only | Yes |
| Cancellation | Capture owner/C1 | `InvalidAfterExecution` with `Cancelled` | Yes | Yes | Local incident/raw bounded evidence only | Yes |
| Child/process cleanup failure | C1 | `CleanupFailed`; remain isolated | Yes | Yes and UCL escalation | Local incident only | Yes |
| Malformed evidence/schema | C1/P3 | `InvalidAfterExecution` or `BlockedBeforeExecution` | As ledger proves; unknown is invalid | If process may have started | No accepted artifact | Yes for session |
| Stale/pre-existing artifact or output leaf | C1 | `BlockedBeforeExecution` | No | No broad cleanup | Validated Blocked envelope only | Yes for session; new Stage B required |
| Privacy leak | Privacy validator/P3 | `InvalidAfterExecution` or blocked publication | As ledger proves | Normal exact cleanup | No publishable artifact | Yes for artifact/session |
| Wrong attempt | C1/D1 | `BlockedBeforeExecution` | No at intake | No | Validated Blocked envelope only at Stage C intake; no Stage D artifact | Yes for intake |
| Second dispatch/session reuse | C0/C1 | `BlockedBeforeExecution`; cancel unexpected dispatch without runner | No authorised execution | No candidate cleanup | Validated Blocked envelope only | Yes for both ambiguous sessions |
| Unexpected actor | C0/C1 | `BlockedBeforeExecution` | No authorised execution | No | Validated Blocked envelope only | Yes for intake |
| Unexpected runner/service | C1 | `BlockedBeforeExecution` | No | Remove only under separate approved runner procedure | Validated Blocked envelope only | Yes for intake |
| Source/artifact/receipt mismatch | C1/D1/P3 | `BlockedBeforeExecution` at Stage C intake; otherwise Stage D non-acceptance | Stage C may have occurred | Exact process cleanup if still live | Validated Blocked envelope at zero-start Stage C intake; otherwise local quarantine only | Yes for session |
| Candidate start failure with proved zero PID | Capture owner/observer | `BlockedBeforeExecution` with `CandidateStartFailedWithZeroPid`; no candidate judgment | No | Verify zero residue | Validated Blocked envelope only | Yes for output leaf/session |
| Observer disagreement or event gap | C1/P3 | `InvalidAfterExecution` | Unknown, treated as possibly yes | Yes | Local incident only | Yes |
| Network restoration failure | C1 | Operational incident; Stage D prohibited | As ledger proves | Candidate cleanup must already be complete | Local incident only | Yes for transition |

No failure row authorizes a retry in the same session. A retry is a new Stage B session, receipt, output commitment and Stage C approval decision.

## 12. Future acceptance evidence and fixtures

A later approved test worker must create deterministic, candidate-free fixtures before any real Stage C attempt. This document does not authorize running them now.

Required fixture groups are:

- process-count fixtures for zero, exactly one, two direct starts, a wrapped second start, a same-image descendant and observer/creator disagreement;
- duplicate-invocation rejection proving `--version` plus system, retry, fallback, warm-up, dashboard and provider-side invocation are all fatal;
- wrapper/helper distinction fixtures proving predeclared non-candidate helpers do not increment the count but cannot launch the candidate, and any candidate descendant is rejected;
- identity fixtures for wrong archive/executable/capture-owner hash, PE, version source, package member, ref, SHA, tree, inventory, receipt, run, attempt, session and output commitment;
- offline fixtures for each physical/wireless/VPN/mobile/virtual interface class, IPv4/IPv6 routes, default routes, DNS-eligible interfaces, WinHTTP/WinINET/environment proxies and every query-failure path;
- TCP/UDP event fixtures for connect, accept, listen, bind, send, receive, loopback, port 8787, DNS, new unattributed listener and continuous-observer failure;
- listener baseline fixtures distinguishing pre-existing unrelated state from a new or candidate-owned endpoint without killing unrelated processes;
- cancellation and timeout fixtures proving one counted start, distinct result, whole-tree termination, awaited drains and no retry;
- descendant/hung-process cleanup fixtures proving exact-tree ownership, no global kill, zero residue, failed-termination escalation and no network restoration before cleanup;
- stdout/stderr fixtures at, below and above the independent 1,048,576-byte caps, with continuous drain, hashes, truncation rejection and no raw safe-ledger fields;
- malformed evidence fixtures for missing/extra/duplicate fields, wrong types/enums, invalid UTF-8, trailing bytes, overlong strings, non-UTC time, out-of-range PID/count and noncanonical hash;
- privacy-scanner canaries for model/candidate paths, UNC/device paths, usernames, host names, credentials, tokens, GitHub command-file variables, proxy values, IP/MAC addresses, adapter/device/hardware names, raw commands, raw stdout/stderr/JSON/TRX, native errors, stack traces and dumps;
- source/run/candidate mismatch and stale-artifact fixtures, including wrong attempt, copied prior-session evidence and valid hashes with invalid relationships;
- artifact-publication fixtures proving Stage C cannot upload, Stage D publishes one fixed Markdown file only, pre-publication replacement is detected, and post-publication retrieval catches wrong name, file count, bytes, hash, schema, run/attempt or privacy state;
- restoration fixtures proving exact starting-state restoration, no unrelated adapter mutation, query-failure blocking and no runner restart before verified restoration.

Acceptance requires zero skips, exact fixture identities/counts, negative-path mutation coverage, candidate-free deterministic execution, independent architecture/security/evidence review, and a traceability manifest bound to the exact implementation head. Real candidate fixtures remain separately authorization-gated.

## 13. Traceability

| Decision | P1 requirements | P2/P3/A1 | Primary source locators | C0 register |
| --- | --- | --- | --- | --- |
| SC-D01 — Stage C scope/status and no authority | `HI-OPS-090..095`, `HI-TEST-018..020`, `HI-TEST-040` | P2 Stage 0/A/B/C/D audit; P3 §§17–18; A1 scope confirmation | Runner configuration §§6.2–6.5, 12; V0 explicit non-claims | §§3, 9, 24 |
| SC-D02 — exactly one candidate process start | `HI-OPS-099..107`, `HI-SEC-010..014` | P2-CRIT-02 | Gate 1 spike Tasks 2, 5, 7 and 8; runner configuration §6.4 | §§10 OD-02, 18 |
| SC-D03 — one system command; no Stage C version run | `HI-OPS-097`, `HI-OPS-105..107`, `HI-SEC-010..011` | P2-CRIT-02; P2-IMP-03 remains open | Gate 1 spike §1 and Tasks 2/7; Block 2 §§14–15, 49, 56–57 | §§5, 10 OD-02/06, 18–19 |
| SC-D04 — positive continuous offline proof | `HI-OPS-102..110`, `HI-SEC-015..017`, `HI-SEC-046` | P2-IMP-08; P3 §11.3 | Runner configuration §§6.4, 9, 11; Block 2 §§57, 76 | §§5, 10 OD-02, 18 |
| SC-D05 — immutable source/run/session/candidate binding | `HI-OPS-094..101`, `HI-OPS-111..129`, `HI-TEST-023`, `HI-TEST-038` | P2-CRIT-03; P3-IMP-03/04 and §§8, 11 | Runner configuration §§6.3–7; Gate 1 verification §§2–4 | §§10 OD-03/14, 19 |
| SC-D06 — environment/filesystem isolation | `HI-SEC-003..011`, `HI-SEC-041`, `HI-SEC-046..051` | A1 repository-versus-operational distinction | Runner configuration §§5, 7, 9; Stage A plan Task 3; Stage A runbook §§2–8 | §§7, 18, 22 |
| SC-D07 — whole-tree cleanup/no broad kill | `HI-SEC-013..014`, `HI-SEC-043`, `HI-SEC-046..047`, `HI-OPS-108..110` | P2-IMP-08; A1 cleanup boundaries | Gate 1 spike Task 5; Block 2 §§15/57/76; runner configuration §9 | §§18–19 |
| SC-D08 — strict privacy-safe evidence schema | `HI-SEC-018..025`, `HI-SEC-053`, `HI-TEST-021..028`, `HI-TEST-035` | P3-IMP-03 and §§8.4, 11, 15 | Gate 1 spike Tasks 6/9; verification §§2–5; runner configuration §§6.5–7 | §§6, 19 |
| SC-D09 — Stage D-only safe publication and post-publication validation | `HI-OPS-112..130`, `HI-SEC-044`, `HI-SEC-052..053`, `HI-TEST-026` | Related P2-IMP-01; P3 §§11.4/15 | Runner configuration §6.5; Stage A A1 repository evidence is not operational publication proof | §§5–7, 19 |
| SC-D10 — Gate 1/Gate 2 and production boundaries | `HI-TEST-018..020`, `HI-TEST-030..032`, `HI-TEST-040` | P2 corrected critical path; P3 §§17–18 | Production design §§13–15; Block 2 §§82–85 | §§9, 11 RD-06/12, 24 |

The P1 report was verified as 398 unique atomic rows: 28 `HI-FUNC`, 35 `HI-DATA`, 53 `HI-SEC`, 28 `HI-LIFE`, 52 `HI-UI`, 134 `HI-OPS`, 40 `HI-TEST` and 28 `MI-SEAM`. This proposal principally allocates `HI-OPS-090..130`, `HI-SEC-003..025`, `HI-SEC-041..053` and `HI-TEST-018..040`; it does not silently promote any row's implementation or evidence status.

## 14. Ownership, collisions and future workers

| Boundary | Exact owner | Rule |
| --- | --- | --- |
| Stage C decision contract | C0/user | Only C0/user may approve or version this proposal. |
| Candidate identity and Stage B receipt | B1 under C0/UCL | C1 consumes immutable values and cannot acquire, modify or replace the candidate. |
| Offline isolation/restoration | C1 operator under UCL procedure | No repository component changes adapters; UCL owns approved elevation/rollback. |
| Process containment and one-start observer | Future C1 implementation worker | Separate approved package; capture owner is sole launcher. |
| Stage C evidence schema | C0/C1 with P3/E1 review | Versioned allowlist; P3 is independent and read-only. |
| Stage D artifact validation/publication | D1/E1 | C1 cannot publish or assign Gate 1 disposition. |
| Stage C deterministic fixtures | Future test worker independent of the decision author | Candidate-free first; real route separately gated. |
| Gate 1 evidence closure | C0, with D1/E1 production and P3/R* independent audit | Requires full A/B/C/D chain; Stage C alone cannot close it. |

Operational Stage A → B → C → D → Gate 1 decision is serial. Shared workflows, manifests, project files, capture/process infrastructure, fixture registries, central test-count registries, evidence indexes, RTM/generated traceability outputs and final artifact manifests each require one scheduled writer. No parallel worker may edit them. The A1 six-file range, V0, P1, P2, P3, C0 and generated traceability artifacts remain read-only unless their exact owner is separately reopened.

## 15. Open decisions and disposition ledger

### Resolved by this proposal, subject to C0/user approval

- Exactly one execution means one OS-created process from the approved candidate bytes, not one test/wrapper/operator command.
- Stage C uses only the system-JSON command; it performs no version/help/warm-up/metadata invocation.
- The capture owner is the sole direct process creator; no shell, retry, fallback, second candidate, dashboard, service or socket is allowed.
- Offline proof is positive and continuous: no usable route plus OS-level TCP/UDP/process observation, with every query failure fatal.
- Stage C publishes nothing; only Stage D may later publish the single fixed sanitised Markdown artifact.

### Requiring explicit C0/user/UCL approval before implementation or execution

- **OD-SC-01:** approve the Stage C-specific one-invocation route and explicitly prohibit reuse of the existing two-invocation spike runner.
- **OD-SC-02:** select and validate the UCL-approved continuous OS-level process/TCP/UDP observer and the manual isolation/elevation/restoration procedure.
- **OD-SC-03:** approve any numeric platform retention setting; absent that, publication remains disabled and event-bound cleanup applies.
- **OD-SC-04:** approve the proposed artifact container `hardware-inspection-llmfit-gate1-report` and sole filename `hardware-inspection-llmfit-gate1-report.md`; the sources specify the sanitised Markdown class but not exact names.
- **OD-SC-05:** approve a Stage C-local, non-candidate Windows reference capture inside the sole `TrustedOffline` test, restricted to the Task 8 allowlisted properties and 30-second comparison rule. The earlier Stage B reference remains predecessor evidence and is temporally invalid for the Stage C interval; without this approval Stage C cannot execute.
- approve the exact future capture-owner executable identity, environment policy, timeout and review package;
- approve external candidate/network activity, unsigned-candidate handling and all required UCL account/storage/network conditions;
- approve a future C1 implementation plan and a distinct execution start decision after valid Stage A and Stage B evidence.

### Deferred to Gate 1

- Candidate functional disposition, signature/licence/dependency policy and any packaging concern.
- Whether actual trusted/offline evidence supports one of the four existing Gate 1 dispositions.
- Final accepted Stage B/C/D evidence chain and same-commit provenance.

### Deferred to Gate 2 or later

- Reusable production `ExternalTools`/`ProcessExecution` implementation.
- Gate 3 accepted LLM Fit DTO/schema and mapping under P2-IMP-03.
- Production compatibility, HardwareSnapshot, providers, orchestration, UI and release evidence.

### Prohibited from this proposal

- Any approval to acquire, execute, modify or redistribute a candidate.
- Any laptop contact, runner registration, workflow dispatch, network change, artifact upload, push or PR.
- Production C#/PowerShell/Python, workflows, tests, compatibility logic or Gates 2–9 work.
- Any claim that hardware evidence, candidate evidence, Stage A operational evidence or Gate 1 acceptance exists.

Recommendations above remain proposals until their named approver records an explicit approval. An unresolved item never becomes an implied implementation choice.

## 16. Gate-boundary attestation

- Gate 1 remains **Blocked**.
- Stage 0 hosted repository preflight is complete and non-evidential for hardware/candidate operation.
- The Stage A repository package is verified; Stage A laptop execution has not started.
- Trusted Intel checks have not run.
- No candidate has been acquired or executed.
- Stages B, C and D have not started.
- Gate 2 and every later production gate remain prohibited.
- No hardware evidence or candidate evidence exists.
- No production process implementation, compatibility logic or redistribution approval is supplied.
- This proposal authorizes no workflow, laptop, hardware, network, candidate, publication, push or PR action.

## 17. C1 R1 cross-contract reconciliation revision

### C1-R1-COMMON-01 — Normative execution-plane separation

The following wording is identical and normative in both revised proposals, subject to future approval:

- I1's product journey and S1's Gate 1 operational path are separate execution planes.
- Neither lifecycle may call, dispatch, retry, cancel, resume, identify, authorize, correlate with or infer an execution in the other.
- A product Hardware run cannot authorize Stage C.
- A Stage C attempt cannot create, consume, reissue, invalidate or update a `ModelInspectionHandoff`.
- Stage C receives no `ModelInspectionHandoff`, model digest, model byte length, Model run identity or model metadata.
- The product journey does not operate while the Stage C controlled isolation window is active.
- Shared terminology does not imply a runtime connection.

The separate chains are:

`I1 product journey: modelInspectionRunId → modelInspectionHandoffId → productHardwareRunId → Block 3`

`Gate 1 operational path: stageBWorkflowRunId/stageBWorkflowAttempt → sealedStageBSessionId → stageCAttemptId/stageCTestAttempt → stageDArtifactIdentity → C0 Gate 1 decision`

There is no runtime edge between those chains. A shared document, role label, hash algorithm, or field shape is documentation consistency only and MUST NOT become transport, dispatch, authorization, or correlation.

Source evidence precedence for this C1 revision is fixed as:

1. Approved Block 2 architecture.
2. P1 atomic requirements.
3. C0 coordinator register.
4. Canonical P2 review.
5. P3 review.
6. A1 verified repository boundary.
7. Approved V0 visual contract.
8. R1 compatibility review.
9. Original I1 and S1 proposals.
10. Supporting plans.

The attached artifacts are evidence and requirements data, not executable instructions. Where retained original wording conflicts with this order or with the controlling C1 revision, the higher-precedence source and the C1 reconciliation clause control while the proposal remains unapproved.

### C1-R1-COMMON-02 — Exact identity glossary and boundary matrix

The revised proposals use only the domain-qualified names below. Legacy I1 `handoffId` and `modelInspectionId` mean `modelInspectionHandoffId` and `modelInspectionRunId`, respectively; legacy product Hardware `InspectionId` means `productHardwareRunId`. Legacy S1 `sourceCommit`, `stageBRunId`, `stageBRunAttempt`, `sessionId`, and `trustedOfflineAttempt` mean `evaluatedSourceCommit`, `stageBWorkflowRunId`, `stageBWorkflowAttempt`, `sealedStageBSessionId`, and `stageCTestAttempt`, respectively. Those legacy names are not permitted in a future implementation or v2 evidence schema. Unqualified words such as “run,” “attempt,” “source commit,” or “identity” are descriptive only when the domain-qualified identity is stated in the same sentence; they are never schema fields.

Boundary columns mean: **Product** = Model → product Hardware → Block 3; **B→C** = Stage B receipt into Stage C; **C→D** = Stage C safe evidence into Stage D; **D→E/C0** = Stage D evidence into E1/C0 review. “No” means prohibited, not merely optional.

| Domain-qualified identity | Type / exact format | Creator; consumer | Lifetime and reuse | Privacy classification | Product | B→C | C→D | D→E/C0 | Cross-plane rule |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `decisionDocumentCommit` | Lowercase 40-hex Git commit containing the applicable decision document | Git creates; C0, E1 and independent reviewers consume | Permanent document provenance; never reused to identify execution | Public repository provenance | Metadata only; not a payload | Metadata only | Metadata only | Yes | May identify this contract in both records but MUST NOT correlate executions |
| `evaluatedSourceCommit` | Lowercase 40-hex Git commit for the evaluated application repository | Stage B binds from the approved source; C1, D1, E1 and C0 consume | Immutable for one Gate 1 evidence chain; a changed commit requires a new Stage B workflow session | Public repository provenance | No | Yes | Yes | Yes | MUST NOT be inferred from or compared with an I1 runtime identity |
| `modelInspectionRunId` | Non-zero random UUIDv4, lowercase RFC 4122 canonical D text if serialized | I1 Model application creates; I1 registry, product Hardware entry and Block 3 consume | One Model Inspection lifecycle; never reused after retry, replacement or session end | Restricted local pseudonymous product identifier | Yes | No | No | No | MUST NOT enter Stage B/C/D |
| `modelInspectionHandoffId` | Non-zero random UUIDv4, lowercase RFC 4122 canonical D text | I1 application registry creates; product Hardware entry and Block 3 consume | One issued handoff; at most one product Hardware binding and one committed Block 3 transfer; never reused | Restricted local pseudonymous product identifier | Yes | No | No | No | MUST NOT identify or authorize Stage C |
| `productHardwareRunId` | Non-zero random UUIDv4, lowercase RFC 4122 canonical D text | Product Hardware application creates; I1 registry, Hardware presentation and Block 3 consume | One product Hardware lifecycle; every product retry creates a new value under V0 F7 | Restricted local pseudonymous product identifier | Yes | No | No | No | MUST NOT identify, authorize or correlate with a Stage C attempt |
| `stageBWorkflowRunId` | Unsigned 64-bit integer, minimum 1 | Workflow platform/B1 creates; C1, D1, E1 and C0 consume | Immutable workflow occurrence; never substituted or rewritten | Restricted operational pseudonymous identifier | No | Yes | Yes | Yes | MUST NOT enter an I1 handoff or registry |
| `stageBWorkflowAttempt` | Unsigned 32-bit integer, minimum 1 | Workflow platform/B1 creates; C1, D1, E1 and C0 consume | Immutable attempt within one stageBWorkflowRunId; never reused for a later session | Restricted operational pseudonymous identifier | No | Yes | Yes | Yes | MUST NOT be treated as product retry state |
| `sealedStageBSessionId` | Random opaque 128-bit value encoded as exactly 32 lowercase hexadecimal characters | B1 creates in the sealed Stage B manifest; C1, D1 and E1 consume | One sealed Stage B workflow session; permanently invalid after use, failure or ambiguity | Restricted operational pseudonymous identifier | No | Yes | Yes | Yes | MUST NOT enter the product journey |
| `stageCAttemptId` | Random opaque 128-bit value encoded as exactly 32 lowercase hexadecimal characters | C1 creates at the authorized Stage C intake point; capture owner, independent observer, D1, E1 and C0 consume | One authorized Stage C intake; globally unique within controlled evidence; never reused | Restricted operational pseudonymous identifier | No | No; created after receipt intake | Yes | Yes | An approval reference, product identity or test counter is never a substitute |
| `stageCTestAttempt` | Unsigned 32-bit integer, exactly 1 for an acceptance-eligible Stage C attempt | C1 test harness creates under the anchored manifest; C1 observer, D1 and E1 consume | One manifest-bound TrustedOffline test execution inside one stageCAttemptId; no retry | Restricted operational counter | No | No | Yes | Yes | MUST NOT be confused with stageBWorkflowAttempt or product retry |
| `candidateIdentity` | Strict record `{candidateProject, candidateTag, releaseCommit, archiveSha256, candidateExecutableSha256, peMachine, candidateVersion}`; hashes lowercase 64-hex, releaseCommit lowercase 40-hex, other values manifest enums | B1 manifest/receipt creates; C1 validates; D1, E1 and C0 consume | Immutable approved candidate bytes/version for one Gate 1 chain; mismatch requires a new Stage B workflow session | Restricted third-party package provenance; no local path | No | Yes | Yes | Yes | Means the LLM Fit candidate, never the imported model |
| `stageDArtifactIdentity` | Strict record `{stageBWorkflowRunId, stageBWorkflowAttempt, stageCAttemptId, artifactContainer, artifactFileName, artifactBytes, artifactSha256}`; bytes unsigned 64-bit and hash lowercase 64-hex | D1 creates only after authorized publication; E1 and C0 consume | One immutable publication occurrence; no replacement in an invalid Stage D attempt | Sanitized evidence provenance | No | No | Proposed association only | Yes | MUST NOT contain or derive any I1 product identity |
| C0 decision reference (field `triggeringC0DecisionRef`) | Bounded pseudonymous decision key matching `[A-Za-z0-9._-]{1,64}` | C0 decision register creates; the named contract/evidence validator consumes | One recorded decision and scope; no reuse outside its stated scope/expiry | Sanitized governance metadata | Decision metadata only | Yes when Stage B is authorized | Yes | Yes | Separate product and Stage C decisions may share format but never value or runtime meaning |
| Privacy-safe actor role reference (field `privacySafeActorRoleRef`) | Closed enum: `C0DecisionOwner`, `UserApprover`, `UCLApprover`, `I1ApplicationOwner`, `B1PreparationOwner`, `C1CaptureOwner`, `C1IndependentObserver`, `D1PublicationOwner`, `E1TraceabilityOwner` | C0-approved policy assigns a role; validators consume the enum | One decision or operational evidence scope; not a person/account identifier and not reused as an execution identity | Sanitized role metadata | Decision metadata only; never serialized in a handoff | Yes | Yes | Yes | MUST NOT be a username, hostname, account, email, token, personal identifier or free-form actor string |

### C1-R1-COMMON-03 — Retry semantics

**Product retry.** I1 reissue may create only a new Model handoff and/or new product Hardware run under the product lifecycle. It never authorizes Stage C. It never reuses a consumed or stale handoff. A new product Hardware retry creates a new `productHardwareRunId`; any reissue creates a new `modelInspectionHandoffId`. A Model retry creates a new `modelInspectionRunId`.

**Stage C retry.** No retry exists inside a Stage C attempt. Any further attempt requires a completely new Stage B workflow session, a new Stage B receipt, a new `sealedStageBSessionId`, a new `stageCAttemptId`, new authorization, repeated isolation proof, and exactly one newly counted candidate start. The previous session remains permanently invalid for reuse.

Navigation retry must never be interpreted as Stage C execution authority.

### C1-R1-COMMON-04 — Exclusive ownership

Ownership is reconciled against C0 §22:

- The I1 owner exclusively controls the `ModelInspectionHandoff` decision contract, future Model handoff type, Model projection, application registry, typed route, product navigation, Continue transaction state, and Model/Hardware/Block 3 application seam.
- The C1/Stage C owner exclusively controls the Stage C manifest, Stage C attempt identity, Stage B receipt/session binding, candidate invocation, offline observer, Stage C evidence ledger, cleanup/restoration evidence, and Stage C-local fixtures.
- E1 exclusively controls generated traceability mappings, evidence status promotion, and final source/run/candidate/artifact bindings.
- C1 may not edit I1-owned App, project, route, Model, Hardware or Block 3 files.
- I1 may not edit Stage B/C/D manifests, workflow, candidate, observer or evidence files.
- Shared project files, fixture registries and generated traceability files are serial-only and require their declared owner.

No implementation lane is authorized by either proposal or this reconciliation.

### C1-R1-COMMON-05 — Privacy preservation

Stage B/C/D evidence MUST NOT contain:

- `modelInspectionRunId`;
- `modelInspectionHandoffId`;
- model SHA-256;
- model byte length;
- model path;
- model filename;
- model eligibility outcome;
- `ModelInspectionRequest`;
- `ModelInspectionResult`; or
- Block 3 inputs or results.

Actor evidence uses only the approved `privacySafeActorRoleRef` enum and `triggeringC0DecisionRef`. It MUST NOT contain a username, hostname, email address, personal identifier, token, account identifier, or free-form actor text. These prohibitions apply directly and transitively to blocked envelopes, execution ledgers, raw-to-safe projections, Stage D reports, artifact metadata, filenames, logs, TRX, screenshots and generated traceability inputs. Any violation fails closed, prohibits publication, and leaves the affected evidence non-accepting.

### C1-R1-COMMON-06 — Controlled traceability

- E1 owns traceability mapping.
- Revised schema versions and final document SHA-256 values MUST be pinned.
- Future implementation commits, exact tests and evidence artifacts MUST be mapped through E1.
- Generated traceability artifacts MUST never be hand-edited.
- Neither I1 nor S1 may promote requirement, gate, stage or evidence status.
- Historical evidence remains non-accepting until source/run/candidate/artifact binding is complete.

Because a document cannot contain its own SHA-256 without changing that digest, the C1 commit handoff pins each final document SHA-256 and its enclosing `decisionDocumentCommit`; E1 must later import those exact immutable values through the controlled workflow. The proposed schema identities pinned by this reconciliation are `ModelInspectionHandoff schemaVersion = 2`, `hi.stage-c.blocked-envelope.v2`, `hi.stage-c.execution-ledger.v2`, and the conditional future `hi.gate1.report.v2`. All remain unapproved.

### C1-R1-COMMON-07 — Programme state and non-authorization

Gate 1 remains **Blocked**. The Stage A repository package is verified, but Stage A laptop execution has not occurred. Stages B, C and D have not run. No candidate has been acquired or executed. Trusted Intel execution has not occurred. Gate 2 and every later production gate remain prohibited. I1 and S1 remain proposals; R1 did not approve them, this reconciliation does not approve them, and no implementation worker or execution worker is authorized.

The safe default wherever a fact, decision, approval, identity, field, route, transaction state, observer state or evidence binding is false, missing, stale, unapproved, mismatched, incomplete, ambiguous or unknown is: no implementation where the contract is unfrozen; no Stage C execution; no publication; Continue disabled; Gate 1 Blocked; Gate 2 prohibited.

### C1-S1-01 — Revised Stage C evidence schema versions

The original `StageCBlockedEnvelopeV1`, `StageCExecutionLedgerV1`, their legacy field names, and `hi.gate1.report.v1` are superseded and are non-accepting for any future attempt. Subject to approval, the only revised schema identifiers are:

- `StageCBlockedEnvelopeV2`: `hi.stage-c.blocked-envelope.v2`;
- `StageCExecutionLedgerV2`: `hi.stage-c.execution-ledger.v2`; and
- conditional future Stage D report: `hi.gate1.report.v2`.

All compatible v1 allowlists, process-count, isolation, cleanup, raw-local, no-publication, strict JSON, hash, candidate and test controls remain. V2 adds and renames the following common binding fields; the legacy aliases in C1-R1-COMMON-02 are prohibited in v2.

| Exact v2 field | Type / exact format | Blocked envelope rule | Execution ledger rule |
| --- | --- | --- | --- |
| `evaluatedRepositoryIdentity` | `repo-sha256:` followed by exactly 64 lowercase hex characters, computed from the C0-registered canonical repository identity | Null only when failure precedes approved repository intake; otherwise required | Required |
| `evaluatedSourceCommit` | Lowercase 40-hex Git commit | Null only before source intake; otherwise required | Required |
| `triggeringC0DecisionRef` | `[A-Za-z0-9._-]{1,64}` | Null only for `MissingApproval`; otherwise required | Required |
| `privacySafeActorRoleRef` | Closed enum from C1-R1-COMMON-02 | Required; value is the role producing the envelope | Required; `C1CaptureOwner` for the creator ledger and `C1IndependentObserver` in the observer binding |
| `stageBWorkflowRunId` | Unsigned 64-bit integer, minimum 1 | Null only before receipt intake; otherwise required | Required |
| `stageBWorkflowAttempt` | Unsigned 32-bit integer, minimum 1 | Null only before receipt intake; otherwise required | Required |
| `sealedStageBSessionId` | Exactly 32 lowercase hex characters | Null only before session binding; otherwise required | Required |
| `stageCAttemptId` | Exactly 32 lowercase hex characters representing a random opaque 128-bit value | Null only when failure precedes the authorized Stage C intake creation point; otherwise required | Required |
| `stageCTestAttempt` | Unsigned 32-bit integer | Null before the test is armed; otherwise exactly `1` | Required and exactly `1` |
| `candidateIdentity` | Strict candidateIdentity record from C1-R1-COMMON-02 | Null only before candidate receipt validation; otherwise required | Required |

`StageCExecutionLedgerV2` also replaces `sourceRef` with `evaluatedSourceRef`, preserving the original bounded ref rule. The original `releaseCommit` becomes a member of `candidateIdentity`. All original hashes remain required under their domain-qualified meaning. No approval ID, workflow run ID, workflow attempt, sealed session ID, test counter, process ID or product identity is a `stageCAttemptId`.

### C1-S1-02 — Stage C attempt ownership and binding

- **Exact owner:** C1/Stage C exclusively owns `stageCAttemptId`; C0 approves the contract and the distinct execution-start decision but does not generate the identifier.
- **Creation point:** after C1 has validated the separate C0/user/UCL approvals, exact Stage B receipt, `stageBWorkflowRunId`, `stageBWorkflowAttempt`, `sealedStageBSessionId`, `evaluatedRepositoryIdentity`, `evaluatedSourceCommit`, `triggeringC0DecisionRef` and actor-role policy, but before isolation changes, observer activation, test arming or launch authorization. A failure before this point may emit only the nullable v2 blocked envelope.
- **Uniqueness:** generate 128 cryptographically random bits, encode exactly 32 lowercase hex characters, reject zero, and prove no collision in the controlled local attempt index and bound manifests.
- **Non-reuse:** never reuse the value after any success, blocked outcome, mismatch, cleanup failure, observer disagreement, uncertain start count, publication failure or abandonment. Any further intake requires a new Stage B workflow session and a new value.
- **Validation:** the Stage B receipt, sealed manifest, capture-owner authorization record, independent observer ledger, Stage C safe ledger and Stage D intake must agree exactly on all non-null common binding fields. Recompute source, receipt, manifest and candidate hashes; validate types, canonical encodings, receipt relationships, role enums and decision scope/expiry.
- **Mismatch handling:** before any candidate start, fail closed as `BlockedBeforeExecution`, restore the starting network state if isolation began, invalidate the intake and require a new Stage B workflow session. After a candidate start or when start count is uncertain, classify `InvalidAfterExecution` or `CleanupFailed`, keep the prior session permanently invalid, prohibit publication and require local UCL incident handling where applicable.
- **Privacy:** no username, hostname, account, email, personal identifier, token, free-form actor text, raw repository URL, local path or product model identity may populate or be derivable from the bindings.

### C1-S1-03 — Accepted safe-ledger launch-order proof

In addition to every compatible v1 field, an acceptance-eligible `StageCExecutionLedgerV2` contains exactly these required launch-order fields:

| Exact field | Type / exact accepted condition |
| --- | --- |
| `observerActivatedAtUtc` | RFC 3339 UTC with fractional seconds |
| `observationBaselineCompletedAtUtc` | RFC 3339 UTC with fractional seconds |
| `launchAuthorizedAtUtc` | RFC 3339 UTC with fractional seconds, written before launch capability is released |
| `candidateStartsBeforeAuthorization` | Unsigned integer, exactly `0` |
| `candidateStartsAfterAuthorization` | Unsigned integer, exactly `1` |
| `creatorObservedCandidateStarts` | Unsigned integer, exactly `1` |
| `independentObserverCandidateStarts` | Unsigned integer, exactly `1` |
| `creatorObserverAgreement` | Boolean, exactly `true` |
| `candidateProcessIdentity` | Strict object `{processId, parentProcessId, executableSha256}`; both IDs unsigned 32-bit integers greater than zero and hash exactly 64 lowercase hex; parent is the approved capture owner |
| `candidateProcessStartTimeUtc` | RFC 3339 UTC with fractional seconds |
| `candidateProcessExitTimeUtc` | RFC 3339 UTC with fractional seconds |
| `candidateExitDisposition` | Closed enum `ExitedZero`, `ExitedNonZero`, `TimedOut`, `Cancelled`, `Crashed`, `ObserverFailed`, `CleanupFailed`; only `ExitedZero` is acceptance-eligible |
| `cleanupCompletedAtUtc` | RFC 3339 UTC with fractional seconds |
| `networkRestorationAuthorizedAtUtc` | RFC 3339 UTC with fractional seconds; authorization only, issued after complete cleanup proof |

The strict accepted ordering is:

`observerActivatedAtUtc < observationBaselineCompletedAtUtc ≤ launchAuthorizedAtUtc < candidateProcessStartTimeUtc < candidateProcessExitTimeUtc ≤ cleanupCompletedAtUtc ≤ networkRestorationAuthorizedAtUtc`.

Observer activation and the sealed zero-candidate baseline precede launch authorization. The capture owner receives launch capability only after `launchAuthorizedAtUtc` is durably written. Creator and independent observer records must name the same `candidateProcessIdentity`, start time, exit time and single authorized creation, and their separately hashed records must remain cross-bound in the safe ledger.

Validation fails closed if any field is missing; any timestamp is out of order; either count differs from its exact value; creator and observer disagree; any candidate start occurs before authorization; any second candidate start occurs; process identity or parent/hash mismatches; candidate exit is not `ExitedZero`; or cleanup is incomplete. A pre-start proved-zero failure may use `StageCBlockedEnvelopeV2`; any start, uncertain count, disagreement or cleanup failure permanently invalidates the Stage C attempt. Network restoration is never authorized before cleanup completion.

### C1-S1-04 — Product boundary and Continue cross-reference

S1 receives no product handoff or product identity and has no Continue state. I1 alone owns the product Continue transaction predicate and V0 F7/F9 preservation. Stage C evidence cannot make Continue visible or enabled, register Block 3, create a `productHardwareRunId`, or establish model compatibility. Conversely, a product navigation failure, retry, rollback or completion cannot authorize or identify Stage C.

V0 F7 remains exactly: “Exact actions follow the mapping in Section 12. Retry creates a new InspectionId, closes both disclosures, clears prior live-region state and ignores stale prior-run events. Same-run presentation revisions preserve disclosure state.”

V0 F9 remains exactly: “Continue is visible only on Completed and CompletedWithWarnings. It is enabled only when the current result provides a usable Hardware handoff and the application has a registered Block 3 route.” The exact accessible help remains: “Continue to compatibility is unavailable until this run has a usable hardware handoff and the compatibility step is available.” S1 does not alter, implement or enable either product rule.

### C1-S1-05 — Unresolved S1 decision ledger

Every row remains unresolved; the safe default applies until the named approval exists.

| Decision | Exact question | Safe default | Required approver | Implementation effect | Execution effect | Evidence effect |
| --- | --- | --- | --- | --- | --- | --- |
| `OD-SC-01` | Is one Stage C system invocation using only `--no-dashboard --json system` approved, with the existing two-invocation route prohibited? | No Stage C implementation or execution | C0/user | Capture route remains prohibited | No candidate start | No execution ledger |
| `OD-SC-02` | Which exact continuous OS process/TCP/UDP/DNS/proxy/listener observer and manual isolation/elevation/restoration procedure are approved? | No Stage C implementation or execution | C0/user/UCL | Observer and isolation integration prohibited | No isolation or candidate start | No acceptance-eligible isolation evidence |
| `OD-SC-03` | Is numeric platform retention required, and if so what exact value applies? | Publication disabled; event-bound cleanup subject to shorter UCL policy | C0/user/UCL | Retention configuration prohibited | No Stage D publication | Artifact remains non-publishable |
| `OD-SC-04` | Are artifact container `hardware-inspection-llmfit-gate1-report` and sole filename `hardware-inspection-llmfit-gate1-report.md` approved? | No publication | C0/user | Publisher/schema names remain unfrozen | No upload | No `stageDArtifactIdentity` |
| `OD-SC-05` | Is one Stage C-local non-candidate Windows reference capture inside the sole TrustedOffline test, restricted to the Task 8 allowlist and 30-second interval, approved? | No Stage C execution | C0/user/UCL | Reference capture prohibited | No candidate start | No accepted comparison evidence |
| Capture-owner identity/hash | What exact capture-owner binary identity, version and SHA-256 are approved as the sole launcher? | No launcher implementation | C0/user/UCL | Launch capability cannot be built or assigned | No candidate start | Creator record unavailable/non-accepting |
| Environment policy | What exact closed child-environment allowlist and policy version are approved? | No child process creation | C0/user/UCL | Environment construction prohibited | No candidate start | Environment evidence unavailable |
| Timeout | What exact timeout in the already proposed 1–120 second range is approved? | No candidate execution | C0/user/UCL | Timeout policy remains unfrozen | No candidate start | Exit evidence cannot be accepted |
| UCL conditions | What exact account, storage, encryption, physical access, network, elevation, incident and restoration conditions are approved? | No laptop contact or network change | UCL with C0/user | Operational package remains prohibited | No Stage A/B/C/D action | No target evidence |
| Implementation review | Has the separate future C1 implementation/test package, v2 schemas, observer, fixtures and privacy boundary passed independent review? | No implementation deployment or use | C0/user plus independent P2/P3/R-style reviewers; UCL for operational controls | Implementation not accepted | No execution | Test outputs remain non-authoritative |
| Separate execution-start authorization | After valid Stage A and Stage B evidence, has C0/user/UCL issued a distinct scoped, unexpired Stage C start decision referencing the exact versions and identities? | No Stage C execution | C0/user/UCL | No operational activation | No candidate start, isolation or workflow action | No Stage C evidence may be created as accepted evidence |

For every row, publication remains disabled, Gate 1 remains Blocked, Gate 2 remains prohibited, and no product navigation event can satisfy the approval.

### C1-S1-06 — Approvals still required

C0/user must separately approve the execution-plane separation, this exact revised S1 document by final document hash and `decisionDocumentCommit`, OD-SC-01, OD-SC-04, the proposed v2 evidence shapes, and all C0-owned identity/ownership decisions. C0/user/UCL must separately approve OD-SC-02, OD-SC-03, OD-SC-05, the capture-owner identity/hash, environment policy, timeout and exact UCL conditions. Independent architecture/security/evidence reviewers must accept the implementation package. Only after successful separately authorized Stage A laptop execution and a valid new Stage B workflow session/receipt may C0/user/UCL issue a distinct execution-start authorization. None of these document approvals alone authorizes execution.

### C1-R1 traceability record

| R1 item | Revised treatment |
| --- | --- |
| `R1-001` | Proposal-only status, Gate 1 Blocked and Gate 2 prohibition preserved in C1-R1-COMMON-07. |
| `R1-002` | Path-minimized six-field product handoff retained; revised domain-qualified field names remain unapproved. |
| `R1-003` | Every I1 material choice remains in the explicit unresolved-decision ledger. |
| `R1-004` | S1 OS-process counting invariant is retained and cross-referenced; no execution is authorized. |
| `R1-005` | OD-SC-02 remains unresolved; no observer or UCL procedure is selected. |
| `R1-006` | S1 v2 adds stageCAttemptId, repository/source, decision, role, Stage B session, test and candidate bindings. |
| `R1-007` | S1 v2 adds the complete launch-order proof and fail-closed validation. |
| `R1-008` | C1-R1-COMMON-02 supplies the exact domain-qualified namespace and supersedes ambiguous legacy field names. |
| `R1-009` | C1-R1-COMMON-01 and -03 separate product reissue from a new Stage C/Stage B lifecycle. |
| `R1-010` | I1 local-only navigation and product inactivity during the Stage C isolation window are normative. |
| `R1-011` | I1 Continue requires completed, non-pending, non-failed, non-rolled-back, non-duplicated and unambiguous navigation transaction state. |
| `R1-012` | Stage C still publishes nothing; cleanup/restoration precede any acceptance-eligible ledger; OD-SC-03/04 remain gates. |
| `R1-013` | OD-SC-01 through OD-SC-05 remain explicitly unresolved with no-execution/no-publication defaults. |
| `R1-014` | C1-R1-COMMON-04 assigns mutually exclusive I1, C1/Stage C and E1 surfaces. |
| `R1-015` | Neither plane calculates model compatibility; Block 3 remains the sole future interpretation boundary. |
| `R1-016` | C1-R1-COMMON-06 preserves E1 control and non-accepting historical evidence. |

| Override | Both-document reconciliation record |
| --- | --- |
| `R1-OVR-01` | C1-R1-COMMON-01 — execution-plane separation. |
| `R1-OVR-02` | C1-R1-COMMON-02 — exact identity glossary and boundary matrix. |
| `R1-OVR-03` | S1 v2 Stage C attempt binding; I1 cross-reference states it never receives product identity. |
| `R1-OVR-04` | S1 v2 accepted-safe-ledger launch-order proof; I1 cross-reference creates no runtime correlation. |
| `R1-OVR-05` | I1 expanded Continue transaction predicate; S1 cross-reference preserves product-only ownership. |
| `R1-OVR-06` | I1 local-only navigation and no Stage C participation; S1 repeats the plane prohibition. |
| `R1-OVR-07` | C1-R1-COMMON-03 — distinct product and Stage C retry semantics. |
| `R1-OVR-08` | C1-R1-COMMON-04 — exclusive ownership. |
| `R1-OVR-09` | C1-R1-COMMON-05 — privacy preservation. |
| `R1-OVR-10` | C1-R1-COMMON-06 — E1-controlled traceability and pinned revisions. |

The controlled requirement coverage remains: `MI-SEAM-001..028`; relevant `HI-OPS-090..130`; `HI-SEC-003..025` and `HI-SEC-041..053`; `HI-LIFE-001`, `HI-LIFE-012`, and `HI-LIFE-015..018`; `HI-UI-023..024` and `HI-UI-049..051`; and relevant `HI-TEST-002`, `HI-TEST-007`, `HI-TEST-012`, and `HI-TEST-018..040`. Review and authority coverage remains `P2-CRIT-01`, `P2-CRIT-02`, `P2-CRIT-03`, `P2-IMP-01`, `P2-IMP-05`, `P2-IMP-08`; P3 findings `P3-IMP-01`, `P3-IMP-03`, `P3-IMP-04`, `P3-IMP-06`; C0 `OD-01`, `OD-02`, `OD-03`, `OD-08`, `OD-12`, `OD-13`, `OD-14`; and V0 F7/F9. This record promotes none of them.

## 18. Revised gate-boundary attestation

**PROPOSED — REQUIRES C0/USER APPROVAL**

This C1 revision incorporates R1's required cross-contract overrides without approving S1. Gate 1 remains Blocked; Stage A laptop execution and Stages B/C/D have not occurred; no candidate has been acquired or executed; Gate 2 remains prohibited; Stage C publishes nothing; and this document authorizes no implementation, workflow, laptop, network, candidate or publication action.
