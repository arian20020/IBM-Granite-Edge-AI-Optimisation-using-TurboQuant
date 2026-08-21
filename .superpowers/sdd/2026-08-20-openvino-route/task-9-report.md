# Task 9 Report: Hosted and Trusted UCL CPU Evidence Gates

## Status and scope

DONE_WITH_CONCERNS, awaiting independent review.

- Clean Task 9 base: `b3bf717a58d6b1f57b380f8a4d85765921267788`.
- Carried Task 8 prerequisite: `e7f803b62754718f2089d2734a09ea8e4915f314`
  (`fix(openvino): serialize exact turn cancellation`).
- Task 9 CI slice: `b125e5c32d8faa2ee1fdf835ee7d8aef585afac5`
  (`ci(openvino): add official and UCL CPU evidence gates`).
- Branch/worktree: `feature/openvino-route` in the isolated linked worktree
  `C:\openvino-o1`.
- Scope is only the Task 8 exact-turn correction, the two Task 9 workflow
  definitions, the closed evidence generator/privacy verifier, and their
  contract tests. No GPU, converter, TurboQuant, protocol, model byte,
  dependency lock, product limit, or deadline was changed.
- No workflow was dispatched, no action artifact was published, and no UCL
  runner was used. Hosted and UCL execution remain pending external
  authorization/state. The MVP and UCL gate are not accepted by this task.

## Mandatory carried Task 8 terminal-gate correction

### Root cause

The final Task 8 ruling was reproduced in the exact app adapter. STOP acquired
`turnTerminalGate` and held it through exact-ID state claim and asynchronous
channel dispatch. Prompt completion/failure also entered that gate. Exact-ID
CANCEL, however, published and claimed terminal teardown under `teardownLock`
and `stateLock` without entering `turnTerminalGate`. It could therefore
terminalize and dispose the channel while a STOP write still owned the same
turn and was paused in flight.

No monitor lock covered an asynchronous call in the existing code, and the
correction preserves that property. The defect was missing async gate
membership rather than a missing monitor lock.

### Focused RED

`PausedStopConcurrentExactCancelAndPromptTerminalShareOneTurnGate` was added
with two deterministic branches: the worker returns either exact
`TurnCompleted(stopped)` or exact `TurnFailed`. Both branches:

- start and worker-confirm one exact turn;
- pause exact STOP inside channel dispatch;
- reject a wrong CANCEL ID before channel contact;
- queue exact-ID CANCEL before releasing the worker terminal;
- release completion or failure while STOP remains paused;
- attempt a next prompt and a late token;
- check terminal/event, channel-command, teardown, and residue counts.

The first focused command was:

```powershell
dotnet test --project tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/GraniteEdgeAI.OpenVino.Tests.csproj `
  --configuration Release `
  --filter FullyQualifiedName~PausedStopConcurrentExactCancelAndPromptTerminalShareOneTurnGate `
  --no-restore
```

Result: 0/2 passed. Both branches failed at the intended assertion because
CANCEL had already completed while STOP dispatch remained paused.

### GREEN and ownership semantics

All cancellation entry points now join the same asynchronous exact-turn
terminal-operation gate used by STOP and prompt completion/failure. Exact-ID
CANCEL publishes its one teardown task before external work, waits without a
caller-cancellable abandonment for the gate, then claims the exact confirmed
turn and retains the gate through CANCEL dispatch, terminal state settlement,
and exactly-once channel disposal. Generic session cancellation/disposal also
enters the gate.

The implementation does not hold `stateLock` or `teardownLock` across STOP,
CANCEL, observer, or disposal awaits. Wrong IDs still fail synchronously before
channel contact. A repeated owning ID joins the published teardown task without
a second command. Prompt-owned stale IDs remain rejected. Once cancellation
owns settlement, competing completion/failure and late text are suppressed.

Focused result: 2/2 passed. The complete adapter/state arbitration partition
passed 35/35. Required pre-commit regressions passed:

| Gate | Result |
| --- | --- |
| OpenVINO route/static/app/package suite | 177/177 |
| OpenVINO contracts before Task 9 tests | 60/60 |
| Managed OpenVINO WorkerClient | 13/13 |
| Protected OpenVINO process suite | 40/40 |
| Worker residue | zero |

This correction was committed separately before any Task 9 workflow result was
used as evidence.

## Task 9 test-first chronology

### Workflow/schema RED

`WorkflowContractTests.cs` was written before either workflow or evidence
script. It requires:

- existence of the exact four production files;
- only 40-character repository-approved action pins;
- least-privilege `contents: read`;
- exact immutable checkout and recorded commit comparison;
- Release x64 builds and explicit job/step timeouts;
- dependency, fixture, worker-manifest, native, contract, static, client,
  process, app-adapter, and package-tamper gates with positive floors;
- hosted and UCL cleanup/integrity under `if: always()`;
- only privacy-validated JSON upload with 30-day retention;
- manual-only UCL triggering, exact trusted labels, environment approval,
  external read-only controlled fixture/model identity, Intel CPU, and
  pre/post integrity.

The first focused command returned 0/23 because the two workflows and both
scripts were absent. The failure was the intended missing-file/contract RED.

### Evidence privacy RED/GREEN

The first implemented privacy matrix passed 17/17: one closed valid document
plus hostile absolute path, UNC path, username, hostname, prompt, generated
text, environment, secret, raw stdout, raw stderr, model bytes, unexpected
nested property, duplicate property, noncanonical order, zero test count, and
performance overflow cases.

A subsequent typed-schema audit added a Boolean-as-count case. Windows
PowerShell numeric coercion exposed a real RED: 16/17 hostile cases were
rejected, but `contracts: true` was accepted as numeric one. The bounded integer
validator was narrowed to exact JSON `Int32`/`Int64`. The valid document plus
all 17 hostile cases then passed 18/18.

The final focused workflow/privacy suite passed 24/24. The complete contract
suite passed 84/84.

## Hosted Windows workflow

`.github/workflows/openvino-official-ci.yml` supplies a hosted Windows 2022
Release x64 gate for push, pull request, and manual dispatch. It:

- selects the immutable PR-head or event commit, checks it out with depth one
  and `persist-credentials: false`, compares `git rev-parse HEAD`, and requires
  a clean checkout;
- uses only the repository-authoritative full SHA pins for checkout, .NET,
  MSBuild, and artifact upload;
- downloads the three official lock-declared archives into runner temp, then
  independently verifies exact length, SHA-256, archive inventory, reviews,
  and fixture identity;
- builds two absent-root independent Release x64 native closures, validates
  each closed worker manifest, runs native CTest, and retains only an ephemeral
  JUnit count input;
- runs contracts (floor 60), route/static inspection (177), WorkerClient (13),
  protected process containment (40), app adapter/state (35), and package
  tamper (5), then builds the Release x64 app against the caller-pinned worker
  manifest digest;
- generates one closed sanitized JSON file, checks zero worker/fixture/parent
  process residue, revalidates integrity, and deletes archives, builds, stages,
  raw TRX, and native JUnit from checked runner-temp descendants;
- runs privacy validation under `if: always()` and uploads only
  `artifacts/openvino/evidence/*.json` after evidence, integrity, and privacy
  all succeed, with `retention-days: 30`.

Raw test results, paths, native logs, model files, build trees, and dependency
archives are never artifact inputs.

## Trusted UCL workflow

`.github/workflows/openvino-ucl-intel.yml` contains only
`workflow_dispatch`. Its exact labels are:

```text
[self-hosted, Windows, X64, workbook05, intel-target]
```

Before checkout or repository code it requires:

- the protected `openvino-ucl-01` environment;
- environment authorization value `UCL-01-approved` and explicit dispatch
  input `UCL-01`;
- an exact lowercase 40-character reviewed commit;
- configured external archive and controlled fixture/model identities.

After exact credential-free checkout it requires X64 plus an Intel
manufacturer, proves controlled archives and fixture are outside the workspace,
requires every controlled file read-only and non-reparse, and verifies the
model's exact positive length/lowercase SHA-256. It builds two operation-owned
closures from the immutable external archives and runs three sequential full
protected-process campaigns. Those campaigns include real one-turn/two-turn,
STOP/reuse, CANCEL, hostile input, manifest tamper, parent loss, containment,
and zero-residue coverage already present in the 40-test official process
suite. The controlled external fixture is independently proven byte-identical
to the locked canonical fixture used by that suite.

The final always-run gate revalidates both worker manifests, archive locks,
the complete controlled fixture, model length/hash/read-only state, and zero
process residue before deleting only operation-owned runner-temp roots. It
never deletes or mutates the external controlled archives/fixture.

No UCL workflow run occurred. A local fail-closed invocation deliberately used
the repository fixture in UCL mode. It returned `official_evidence_failed`, exit
1, and created no output because the fixture was not external. This is gate
behavior only, not UCL evidence.

## Closed evidence schema and privacy audit

The evidence document is at most 16 KiB, strict UTF-8, closed, typed, ordered,
and contains exactly:

1. `schemaVersion`, `evidenceKind`, and exact `commitSha`;
2. SHA-256 identities for the Runtime, GenAI, and Tokenizers lock files;
3. fixture-manifest and worker-manifest SHA-256;
4. requested `CPU`, actual `['CPU']`, and sanitized CPU architecture/vendor;
5. exact Runtime/GenAI/Tokenizers build identities;
6. positive bounded counts for contracts, static inspection, native unit,
   WorkerClient, process containment, app adapter, and package tamper;
7. bounded count/minimum/median/maximum duration aggregates;
8. fixed `passed` cancellation and `zero_residue` cleanup dispositions.

For UCL evidence the vendor must be exactly `Intel`; no processor model, serial,
machine name, runner name, account, or host identifier is accepted. Every
property name occurs exactly once and in canonical order. Counts and durations
must be JSON integers, not coercible strings, floats, or Boolean values.

The verifier rejects unknown/duplicate/reordered fields, malformed or oversized
JSON, absolute or UNC paths, usernames/hostnames, prompts, generated text,
environment/secrets/passwords, raw stdout/stderr, model/package paths, model
bytes, non-approved runtime identities, device mismatch, zero/overflow counts,
unbounded or unordered performance values, and any non-approved disposition.

The final local-only hosted-shaped sample was generated from real passing
results at exact commit `b125e5c32d8faa2ee1fdf835ee7d8aef585afac5`.
It was 1,205 bytes, had SHA-256
`8a9fb352e921187418771e0b3f3ac20d178fc93883731b5ba26a09f256574693`,
and returned `evidence_privacy_valid`. It stayed outside the repository and was
not uploaded or accepted as hosted execution evidence.

## Exact identities

| Identity | Value |
| --- | --- |
| Runtime lock file | `418233649c425e98da8776b0c03da5426dc60611aa1ace9a82f2c905ac4395d7` |
| GenAI lock file | `ce57013587d098e3f1dc672e4a47565669a6a5316a89924153371aed3ba7c32f` |
| Tokenizers lock file | `1b7f3c66b14cba56c3934249d1548ee4f8e9c930ee121ea094703813923320e5` |
| Canonical fixture manifest | `dc5ef5060a0e8242291d98701ab2b65286169c863a12e573dd1ad7921ac92648` |
| Approved Task 7 stage-B worker manifest | `0f656f6f2afe0b7246d0746f458ad6ed02e23be943145779b0160dd69aff2ebe` |
| Approved Task 7 stage-B worker executable | `51f5c5579251b2d9a81bb1c743acdc3f361abd6e297782701d04ea4691311c94` |
| Runtime build | `2026.3.0-22451-8a17657b995-releases/2026/3` |
| GenAI build | `2026.3.0.0-3277-bd8d6542e3c` |
| Tokenizers build | `2026.3.0.0-703-183c6f25cda` |

The workflow action identities reused from repository workflows are checkout
`9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0`, setup-dotnet
`d4c94342e560b34958eacfc5d055d21461ed1c5d`, setup-msbuild
`30375c66a4eea26614e0d39710365f22f8b0af57`, and upload-artifact
`bbbca2ddaa5d8feaa63e36b76fdaad77386f024f`.

## Final local verification

All final test commands were sequential and used approved Task 7 stage A/B
where native process evidence required them.

| Gate | Result |
| --- | --- |
| Task 8 carried RED | 0/2, exact expected early-CANCEL failure |
| Task 8 carried focused GREEN | 2/2 |
| Adapter/state arbitration | 35/35 |
| Workflow/privacy focused | 24/24 |
| Full OpenVINO contracts, Release | 84/84 in 42.184 s |
| Full route/static/app/package suite, Release | 177/177 in 31.181 s |
| Explicit app adapter/state partition | 35/35 in 1.292 s |
| Explicit package-tamper partition | 5/5 in 15.841 s |
| Managed OpenVINO WorkerClient | 13/13 in 3.515 s |
| Protected OpenVINO process suite | 40/40 in 1 m 04.447 s |
| Native CTest | 7/7 in 13.14 s |
| Evidence generation/privacy | `official_evidence_created`; `evidence_privacy_valid` |
| UCL local fail-closed check | exit 1; no output file |
| YAML unique-key parse | 2/2 documents |
| PowerShell AST parse | zero syntax errors in both scripts |

After the report commit, two non-authoritative focused probes supplied a
VSTest-style `FullyQualifiedName` filter to the wrong unit-test assembly and
then to this Microsoft.Testing.Platform contracts assembly; each correctly
reported zero selected tests and exit 5. `--list-tests` then confirmed all 84
contracts, and the exact workflow-form contracts command (x64 Release,
`--minimum-expected-tests 60`, TRX reporting) passed 84/84 in 42.470 s. This
was a command-selection correction only; it caused no source change. Its
single operation-owned TRX directory was removed after verifying it contained
only `contracts.trx`.

The known legacy
`OverallTimeoutStartsAfterStartNotDuringDelayedHandshake` ruling is preserved.
No legacy deadline/budget was changed and that unrelated load-sensitive test was
not blind-rerun. No Model Inspection legacy production file changed.

## Concerns and handoff

- Hosted and UCL workflows are definitions only. A definition, local parser,
  local sample, or queued run is not hosted/UCL execution evidence.
- `UCL-01` is not authorized. The trusted workflow must not be dispatched until
  runner, repository code, dependency/model transfer, privacy, 30-day retention,
  and cleanup authorization are independently confirmed.
- Hosted dispatch/publish is also pending external authorization. The locally
  generated sanitized sample must not be substituted for a completed hosted run.
- MVP/UCL acceptance remains false until completed hosted and UCL evidence refer
  to the same reviewed immutable commit and every design section 24.1 criterion
  is independently satisfied.
- The UCL workflow intentionally records only the coarse sanitized vendor
  `Intel` and architecture `X64`; detailed processor or runner identity remains
  local and is excluded from artifacts.
- No downloaded archive, DLL, executable, wheel, build output, TRX, JUnit, local
  evidence sample, or YAML parser dependency is tracked.
- Three operation-owned Task 9 temporary directories remain outside the
  repository because the command safety policy rejected their recursive
  removal even when each literal path was named and verified. They are
  `granite-o1-task9-local-evidence-20260821`,
  `granite-o1-task9-final-evidence-20260821`, and
  `granite-o1-task9-yaml-parser-20260821` under the current account's local
  temporary directory. None is tracked, published, or consumed by a workflow;
  an authorized operator should remove them. Repository evidence residue and
  worker-process residue are both zero.

Task 9 remains in progress pending independent review.

## Fix round 1/5 - receiving-code-review verification and correction

Implementation commit: `dbfc5a454a1af857331603585187950c58ee467e`.

The ten review findings were checked against the exact workflow, script, and
adapter code before changes. All were reproducible:

1. the UCL authorization input was interpolated inside a quoted PowerShell
   `run` block and the checkout ref consumed the dispatch input directly;
2. privacy checked one JSON file while upload selected an unrestricted JSON
   wildcard;
3. the external controlled UCL fixture was audited but neither native CTest nor
   the managed campaign consumed it;
4. an exact CANCEL published permanent teardown ownership before it waited for
   an already-paused STOP terminal gate;
5. UCL-shaped evidence could be generated locally and several evidence values
   were constants rather than measured results;
6. Windows PowerShell JSON coercion admitted a Boolean schema version and a
   scalar one-element actual-device value;
7. the native floor was one rather than the reviewed seven;
8. outside-workspace checks were lexical and did not close reparse/final-handle
   substitution;
9. hosted path filters omitted dependency locks and influential application,
   protected-launcher, process-fixture, build, and packaging inputs; and
10. workflow tests were substring checks rather than YAML-node contracts.

### Focused RED/GREEN chronology

The inverse terminal-race test was written first. With STOP paused in dispatch,
completion-first poisoned the cached exact-CANCEL task and failure-first was not
deterministically ordered. The focused test was 0/2 at RED. A test-only
non-owning terminal-wait observer made both branches deterministic. Exact
CANCEL now publishes only a turn-scoped pending reservation, waits the same
`turnTerminalGate` as STOP/completion/failure, promotes to permanent teardown
only after it claims the exact active turn, and clears a losing reservation.
It never holds `stateLock` or `teardownLock` across channel I/O. The inverse
test passed 2/2 and the combined STOP/CANCEL/completion/failure matrix passed
4/4. Completion permits the next turn and a fresh exact cancellation; failure
permits unpoisoned disposal. Both branches prove one terminal event, no wrong
turn command, late output suppression, and one channel disposal.

Structural workflow tests using the exact pinned `YamlDotNet` `18.1.0` parser
were then added. The initial structural set passed only 1/3: exact evidence
selection, influential path filters, and correct-node requirements failed.
Adversarial valid YAML with a quote/newline dispatch payload, misleading
comments, wrong-node values, and misplaced `always()` is rejected. The final
workflow partition passes 71/71 and verifies parsed triggers, job labels,
permissions, timeouts, step ownership/order, environment indirection, exact
upload path, cleanup, native floor, fixture override, and the complete hosted
path set. Unit tests do not shell out to parse YAML. The only accepted action
identities are the four reviewed full-SHA pins.

The exact evidence-set test was RED while its verifier was absent, then passed
1/1. The generator requires an initially absent operation-owned evidence
directory and exact `official.json` or `ucl.json`, creates it only after all
other validation, privacy-checks that exact file, and removes its partial
output on failure. Both workflows count the directory immediately before
privacy validation, upload the exact filename without a glob, then always
remove the exact local file and empty operation-owned parents after upload.
A second hostile JSON makes selection fail.

The native JUnit boundary was test-first: six tests are rejected and exactly
seven accepted. The first implementation exposed an arithmetic implementation
error and was corrected. A later real CTest 7/7 JUnit file supplied a second
RED because CTest legitimately omits the optional `errors` attribute. The
parser now accepts the actual closed CTest shape, requires exact passing
suite/testcase counts, rejects failure/error/skipped/disabled content, and
retains a minimum rather than an exact count. Synthetic 6/7 and real 7/7 gates
are green.

The exact-type hostile matrix initially rejected 17/19 cases: Boolean
`schemaVersion` and scalar `actualExecutionDevices` were accepted. Exact
integer/string/Boolean/array/object validation and recursively closed objects
made the original matrix 19/19. A separate every-node matrix passes 35/35,
including every object/leaf/array element, nonfinite input, and Int64 overflow.
The final privacy/evidence-focused partition passes 62/62, including Windows,
UNC, and POSIX absolute paths, account/machine labels, prompts/generated text,
environment/secrets, raw stdout/stderr, model bytes, duplicates, unexpected
fields, ordering, types, count bounds, and performance bounds.

The trusted-path verifier was absent at RED, then passed its focused 1/1
matrix. It opens roots and descendants with Win32 handles, normalizes final
paths, uses component-boundary containment, rejects equality and containment in
either direction, rejects root/descendant/file reparse points, requires every
controlled file read-only, and checks the exact model length/SHA-256 and exact
fixture-manifest SHA-256. Junction/symlink root, descendant link, model-file
substitution, and final-path-to-workspace cases fail closed. The clean external
fixture and controlled archive probe returned `trusted_inputs_valid` both
before and after the managed campaign.

The UCL fixture override first failed 0/1 because the repository fixture was
accepted. After adding the override, an external-environment execution exposed
two additional real REDs: the self-test incorrectly derived its repository
path through the active override, and read-only source attributes propagated
into operation-owned mutable test copies. The repository path is now resolved
independently, copied test files have only their copy's read-only bit cleared,
and the controlled source remains unchanged. Native builds receive both the
external fixture root and its expected manifest SHA; the CMake/CTest package
argument therefore uses that exact external package. The managed campaign
requires the same external root and emits only the exact fixture-manifest SHA
marker. A closed TRX verifier accepts that marker once for the exact named
passing test. Repository-fixture override rejection is green, and a clean
read-only external-fixture campaign passes 41/41 with pre/post integrity and
`ucl_fixture_consumption_valid`.

The closed measurement builder was absent at RED and then derived requested
CPU, actual CPU, all three runtime builds, seven test counts, bounded sorted
duration aggregates, cancellation, and cleanup solely from passing JUnit/TRX
inputs. A missing measured marker fails and creates no output. An added raw-log
file produced a focused RED; the builder now accepts only the exact seven hosted
inputs or those seven plus the exact three UCL campaign TRXs, and requires the
canonical UCL result to be byte-identical to campaign three. The closed hosted
and three-run UCL forms pass; partial, extra, and substituted inputs fail.

UCL evidence generation was locally forgeable at RED. It now also requires the
exact GitHub Actions event, repository/workflow ref, workflow SHA, checkout SHA,
numeric run ID and attempt, exact job, protected-environment authorization,
explicit dispatch authorization, X64 runner, and Intel manufacturer. The UCL
mode performs final controlled-input verification again before writing. Local
mode and partial GitHub context both return `official_evidence_failed`, exit 1,
and produce no UCL JSON. These checks bind claims to expected GitHub workflow
context and protected-environment state; they are not claimed to be
cryptographic attestation, because a local actor can spoof all environment
variables.

### Final local verification after fix commit

| Gate / command shape | Result |
| --- | --- |
| `dotnet test` full OpenVINO contracts, Release x64 | 131/131 |
| Parsed workflow partition | 71/71 |
| Privacy/type/artifact/native/path/provenance partition | 62/62 |
| Exact terminal arbitration regression | 4/4 |
| Full OpenVINO route/static/app/package suite | 179/179 |
| Explicit app adapter/state partition | 37/37 |
| Explicit package-tamper partition | 5/5 |
| Managed OpenVINO WorkerClient | 13/13 |
| Hosted canonical protected process campaign | 41/41 |
| Clean read-only external-fixture protected process campaign | 41/41 |
| Native CTest and real JUnit verifier | 7/7; `native_junit_valid:7` |
| Release x64 app build against pinned stage manifest | passed |
| PowerShell AST parse | 0 errors across 8 affected scripts |
| Worker-process residue | zero |
| UCL local provenance gate | exit 1; no output |

The real closed result set produced measured counts
`131/179/7/13/41/37/5` for contracts/static/native/client/process/adapter/tamper,
and only the expected CPU, runtime, cancellation, and zero-residue markers.
After the fix commit, a local hosted-shaped smoke document at exact commit
`dbfc5a454a1af857331603585187950c58ee467e` was 1,206 bytes, had SHA-256
`de223572c7ecfb0f269de1d2bb0863aafde95b2367c056aece6845f9ebc66efb`,
and returned both `official_evidence_created` and
`evidence_artifact_set_valid`. It was not uploaded and is not hosted execution
evidence.

### Exact identities retained by the correction

| Identity | Value |
| --- | --- |
| Runtime lock | `418233649c425e98da8776b0c03da5426dc60611aa1ace9a82f2c905ac4395d7` |
| GenAI lock | `ce57013587d098e3f1dc672e4a47565669a6a5316a89924153371aed3ba7c32f` |
| Tokenizers lock | `1b7f3c66b14cba56c3934249d1548ee4f8e9c930ee121ea094703813923320e5` |
| Fixture manifest | `dc5ef5060a0e8242291d98701ab2b65286169c863a12e573dd1ad7921ac92648` |
| Model | length `88`; SHA-256 `894dd0aac21e588d5cf78994d90aa0dcba8284626c976a4e0c89c0273b452c1c` |
| Local reviewed stage A manifest | `b09feb0c73969d5e13a1d755937fc580d1507ee5c5f0397ebd44d305dfb6613b` |
| Local reviewed stage B manifest | `0f656f6f2afe0b7246d0746f458ad6ed02e23be943145779b0160dd69aff2ebe` |
| Runtime build | `2026.3.0-22451-8a17657b995-releases/2026/3` |
| GenAI build | `2026.3.0.0-3277-bd8d6542e3c` |
| Tokenizers build | `2026.3.0.0-703-183c6f25cda` |

### Privacy and residue audit

Evidence remains the same closed fourteen-field schema named in the Task 9
brief. No provenance environment, repository/account identity, runner/machine
identity, processor model, username, hostname, credential, proxy, path,
prompt, generated text, raw stdout/stderr, model bytes, archive, build output,
TRX, JUnit, or log is in the JSON or selected by either artifact upload. The
only CPU identity is sanitized `X64` plus coarse vendor. Artifacts retain for
30 days and upload only after evidence, integrity, and privacy succeed.

Repository evidence residue is zero, tracked binaries/downloads are zero, and
worker-process residue is zero. Nine round-one operation-owned test directories
remain under system temp because the command safety policy rejected their exact
recursive cleanup: the initial/clean controlled fixtures, dummy controlled
archive root, failed and passing process-result roots, clean UCL campaign
results, and pre/post-commit evidence/result roots. None is tracked, uploaded,
or consumed by a workflow. The three previously reported Task 9 temp roots also
remain. An authorized operator may remove those exact temp directories.

The legacy delayed-handshake ruling is unchanged; no deadline or budget was
weakened and no blind rerun was performed. Hosted dispatch/upload and trusted
UCL dispatch remain pending external authorization. `UCL-01` remains open,
this local campaign is not UCL evidence, and neither MVP nor UCL acceptance is
claimed. Task 9 remains in progress awaiting independent re-review; Task 10 was
not started.
