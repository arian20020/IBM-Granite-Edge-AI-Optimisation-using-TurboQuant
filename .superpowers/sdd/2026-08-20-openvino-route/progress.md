# SDD ledger — plan: docs/superpowers/plans/2026-08-20-openvino-route.md

Workspace: `C:\openvino-o1`
Branch: `feature/openvino-route`
Merge base: `f3e7793c8783be7a541f5daefec8f7f2cc78bc64`
Spec: `docs/superpowers/specs/2026-08-20-openvino-route-design.md`

## Setup evidence

- Isolated linked worktree confirmed: git dir differs from common dir; no submodule.
- Plan commit `d982578a2e673181d57a6761dbca0a3f2ae2e5ac` is an ancestor of the merge base.
- Baseline command: `dotnet test tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj --configuration Release --minimum-expected-tests 357`.
- Baseline result: 356 passed, 1 failed, 0 skipped. `CurrentCleanupScopeIsFullyInventoried` reports two Model Import files added before this branch are absent from the cleanup inventory.
- Ruling: Preserve the pre-existing cleanup-inventory failure and continue with unaffected gates — Task 1 does not touch that inventory, the plan forbids O1 from independently editing generated/shared inventory, and Task 18 assigns final reconciliation to I0 — if wrong, the branch may carry a known shared contract failure until the Task 18 integration window.

## Preflight consistency scan

### Per-task internal consistency

| Task | Tests versus implementation and file lifecycle | Finding / ruling |
| --- | --- | --- |
| 1 | Dependency lock behavior is tested before lock/script creation; files are consumed later by Tasks 7, 11, 15, 16, and 18. | Consistent. Exact third-party bytes are external inputs, so lock generation must fail closed rather than invent hashes. |
| 2 | Protocol, sequence, handoff, and taxonomy tests precede the shared contract types consumed by all clients/workers. | Consistent. |
| 3 | Protected-worker behavior tests precede facade extraction and the existing client is regression-tested. | Consistent; this is the explicit GGUF/OpenVINO common ground. |
| 4 | Fake-worker hostile scenarios precede the managed OpenVINO worker client. | Consistent; it consumes Task 2 contracts and Task 3 facade. |
| 5 | Fixture contract tests precede deterministic fixture generation and independent validation. | Consistent; native validation is deliberately completed in Task 7 before `FIX-01` closes. |
| 6 | Malformed-package and handoff tests precede static inspection. | Consistent; `Ready` remains withheld until Task 7 native parsing. |
| 7 | Native and .NET process tests precede the official CLI worker and runtime manifest. | Consistent; CPU first and `ChatHistory` are explicit. |
| 8 | State/adapter and I0 integration tests precede route/UI/packaging registration. | Consistent; shared edits are serial I0-owned commits. |
| 9 | Workflow/privacy contracts precede hosted and UCL workflows. | Consistent; dispatch remains gated by `UCL-01`. |
| 10 | Device mismatch/negative tests precede explicit GPU exposure. | Consistent; CPU remains available if GPU stays hidden. |
| 11 | Source and isolation tests precede the sealed converter. | Consistent; it consumes the Task 1 wheel closure. |
| 12 | Transaction failure matrix precedes conversion publication and UI action. | Consistent; it consumes Tasks 6, 7, and 11. |
| 13 | Artifact/cache separation tests precede standard optimization. | Consistent; C1 registration remains serial and evidence-gated. |
| 14 | Stable-route acceptance fails until Tasks 1–13 evidence exists. | Consistent; it adds no hidden fallback. |
| 15 | Codec/dispatch conformance tests precede the narrow TBQ4 runtime patch. | Consistent; recovery-versus-port choice is controlled by `TQ-01`. |
| 16 | Activation and forced-negative tests precede the separate TurboQuant worker/workflow. | Consistent; typed counters are required. |
| 17 | Exposure/fallback tests precede app registration. | Consistent; it consumes Task 16 proof and keeps official OpenVINO isolated. |
| 18 | Architecture, packaging, accessibility, cleanup, privacy, and RTM gates precede release closure. | Consistent; I0 owns shared inventory/RTM updates. |

### Shared file and interface pairs

| Producer task | Consumer task | Shared file/interface | Finding / ruling |
| --- | --- | --- | --- |
| 1 | 7 | Official runtime archive locks/manifests/licenses | Compatible; native build must consume only the verified closure. |
| 1 | 2 | `tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/GraniteEdgeAI.OpenVino.Contracts.Tests.csproj` | Conflict found during next-brief preparation: both tasks say Create. Ruling: Task 1 creates the executable dependency-lock test shell; Task 2 modifies that existing project to reference the new production contracts and add protocol tests — this preserves TDD ordering and one test project — if wrong, Task 2 may need a mechanical project-file reconciliation rather than a second project. |
| 1 | 11 | Python runtime and wheel locks | Compatible; converter may not resolve ambient packages. |
| 1 | 15–16 | License and source-identity gate pattern | Compatible; TurboQuant has a separate closure rather than extending official locks. |
| 2 | 4 | Strict OpenVINO JSONL contracts | Compatible; WorkerClient parses but does not redefine protocol types. |
| 2 | 6 | `ModelInspectionHandoffV2` | Compatible; static/native inspection produces exact schema-v2 data. |
| 2 | 7, 16 | Official and TurboQuant protocol identifiers/events | Compatible; distinct identifiers share bounded transport only. |
| 3 | 4 | `IProtectedWorkerSessionFactory` | Compatible; facade remains in the existing ModelInspection WorkerClient assembly. |
| 3 | 8 | Existing GGUF client behavior | Compatible; full regression gate prevents OpenVINO refactor drift. |
| 4 | 7 | Session/inspection JSONL protocol | Compatible; fake process scenarios define the native boundary. |
| 4 | 8 | `IOpenVinoWorkerClient` | Compatible; app adapter owns route-neutral translation. |
| 5 | 6–7 | Tiny deterministic GenAI fixture and manifest | Compatible; managed malformed inspection and real native pipeline use the same locked fixture. |
| 6 | 8 | Inspection outcome and schema-v2 handoff | Compatible; UI receives no filesystem path and does not claim hardware compatibility. |
| 7 | 10 | Official native session/device evidence | Compatible; GPU extends explicit device values without changing CPU behavior. |
| 7 | 12–14 | Official CPU inspection/smoke/session | Compatible; conversion and stable acceptance depend on the exact official worker. |
| 8 | 17 | Existing shared prompt lifecycle/template and capability registry | Compatible; TurboQuant is another route adapter, not another page. |
| 9 | 10, 14 | UCL official workflow/evidence schema | Compatible; GPU and stable checks extend the same official workflow. |
| 11 | 12–13 | Sealed converter and source lock | Compatible; transaction/optimization controls publication around it. |
| 12 | 13 | Atomic package publication and provenance | Compatible; optimization reuses transaction semantics for new artifacts. |
| 13 | 14 | Evidenced standard candidates | Compatible; stable acceptance checks only registered tuples. |
| 15 | 16 | TBQ4 codec/runtime patch identity and counters | Compatible; worker consumes a separate custom runtime closure. |
| 16 | 17 | Typed TurboQuant activation evidence | Compatible; app exposure requires every proof field and forced negative. |
| 8, 9, 14, 17 | 18 | Packaging, UI, CI evidence, cleanup, RTM | Compatible; Task 18 is the sole broad release/inventory reconciliation point. |

## Task progress

Task 1: pending
Task 1: minor (deferred): retain path-safe RED/GREEN/tamper command evidence for later audit without retaining third-party binaries; Task 18 owns durable evidence packaging.
Task 1: fix round 1/5 (3 addressed, 0 open — executable verifier/tamper coverage; CPython length/SHA verification; reviewer sentinel rejection; commits 1422f7e5..b494938c).
Task 1: Ruling: complete the official dependency foundation while keeping `DEP-02` fail-closed because the approved direct pins are unsatisfiable; Tasks 2–10 do not consume the converter wheel closure, and Task 11 cannot start until a compatible exact Optimum/Optimum Intel train is lock-resolved — this preserves honest evidence and forward progress — if wrong, converter work will require an earlier dependency-only correction commit.
Task 1: complete (commits f3e7793c..b494938c, review clean; `DEP-02` remains an explicit gate).
Task 2: pending
Task 2: minor (deferred): retain an explicit standalone terminal-immutability and stale-TurnId regression pair if fix round 1 does not already add them; final review must confirm these behaviors are directly covered.
Task 2: fix round 1/5 (2 addressed, 0 open — unified command/event lifecycle; independent literal wire expectations; commits 3f307e3f..44a22ff9).
Task 2: minor resolved in fix round 1: `ProtocolSequenceTests` directly covers terminal immutability and stale TurnId/session rejection.
Task 2: complete (commits b494938c..44a22ff9, review clean).
Task 3: pending
Task 3: Ruling: preserve the existing Gate 2 `PROC_THREAD_ATTRIBUTE_JOB_LIST` creation-time job assignment and do not add `CREATE_SUSPENDED`/`ResumeThread`; tests must prove the worker is job-bound before any child user code can execute — the repository's approved creation-time mechanism is race-free and stronger than the plan's inherited suspended-launch wording — if wrong, a later security review may require a deliberate launcher-semantics migration with new native evidence rather than a facade refactor.
Task 3: fix round 1/5 (4 addressed, 0 open — closed fixed-argument grammar; bounded public stdin/stdout; unified <=5s cleanup; creation-time/parent/stderr-flood proofs; commits 4ca561a3..19dcfa58).
Task 3: complete (commits 44a22ff9..19dcfa58, review clean).
Task 4: pending
Task 4: fix round 1/5 (2 of 4 findings addressed, 2 open — absolute startup/turn deadlines and terminal EOF/exact PID cleanup addressed; cancellation ownership and watchdog publication remained; commits 69dbbcf1..63b9f908).
Task 4: fix round 2/5 (2 addressed, 0 open — bounded cancellation write/actionable-output suppression; atomic watchdog outcome publication; commits 63b9f908..5e2e03dc).
Task 4: Ruling: accept the unchanged legacy delayed-handshake test as a scheduling-sensitive out-of-scope observation for this OpenVINO-only fix because no legacy file/project dependency changed, the exact test passes 1/1, and prior reviewed heads passed the full 32/32 suite; rerun it at Task 18 and do not weaken its budget — if wrong, an underlying shared timing regression may surface in final release verification and require a dedicated legacy fix.
Task 4: complete (commits 19dcfa58..5e2e03dc, review clean).
Task 5: pending
Task 5: Ruling: authorize the single fixture-scoped `.gitattributes` byte-preservation rule even though the plan did not name the shared file; exact generated JSON/XML/binary hashes must survive Windows checkout, the rule is restricted to `TinySyntheticV1`, and no broader attributes may change — if wrong, Task 18 must replace it with an equally deterministic repository-level checkout policy before release.
Task 5: fix round 1/5 (2 findings materially addressed; re-review found 1 remaining strict numeric-domain gap — semantic source/license binding and directory alternate-data-stream closure are otherwise complete; commits 049a67db..8eee7e09).
Task 5: fix round 2/5 (1 addressed, 0 open — canonical nonnegative in-range integral Int64 manifest lengths; commits 8eee7e09..3cf5c341).
Task 5: complete (commits 5e2e03dc..3cf5c341, independent review clean; `FIX-01` remains reserved for Task 7 and `DEP-02` remains unresolved).
Task 6: pending
Task 6: fix round 1/5 (Critical atomic-snapshot finding and original four Important areas materially addressed; re-review found 7 remaining edge/consistency/test findings; commits 62816f63..7a8d0a72).
Task 6: fix round 2/5 (5 of 7 fully addressed; tokenizer unknown-token reconciliation and Result destination-port proof remain; commits 7a8d0a72..ba502fa1).
Task 6: fix round 3/5 (2 addressed, 0 open — normalized `unk_token` facts and unique declared Result destination-port edges; commits ba502fa1..fa6498ac).
Task 6: complete (commits 3cf5c341..fa6498ac, independent review clean; final Task 6 124/124 and aggregate regressions 359/359).
Task 7: pending
Task 7: Ruling: reopen the Task 2/4 interface narrowly before native implementation because the reviewed wire carries no package path or native/runtime evidence and cannot support arbitrary selected packages or Task 6 handoff gating; add protected-stdin package identity/path, typed build/native/device evidence, inspection progress, close-session, and authoritative turn completion atomically with contract/client tests — a fixed-cwd package workaround is forbidden — if wrong, the protocol version/compatibility contract must be deliberately migrated before Task 8 rather than hiding package selection out of band.
Task 7: Ruling: `generatedTokenCount` is the native metric and includes non-text EOS tokens, while `TokenEvent.sequence` orders nonempty decoded text fragments; validate authoritative count against the requested maximum rather than equating it to frame count — if wrong, the UI/evidence layer would undercount EOS and other fragment coalescing and must add a distinct frame metric later.
Task 7: Protocol-governance ruling: the root explicitly approved the narrow Task 2/4 correction before native implementation because the previous unpublished wire could not represent protected arbitrary-package selection, native evidence, or graceful close. All producers and consumers changed atomically on this feature branch, so retain `openvino.official/1`; there is no released compatibility consumer and no synthetic version migration is warranted.
Task 7: fix round 1/5 (7 addressed, 0 open - independent caller-known runtime-manifest trust, managed/native retained closure leases, native package transaction, typed failure ownership, subtraction-safe terminal context validation, real post-fragment STOP/session reuse, strengthened residue proof, and build evidence in atomic Hello; implementation commit `4fa4200b`).
Task 7: in progress after independent review fix round 1 (implementation commits `14f36a3f`, `c688f948`, `4fa4200b`, plus report commit; all round-1 findings have fixes and verification evidence, but independent re-review is still required; `FIX-01` remains closed by two independent clean native closures, `DEP-02` remains unresolved and untouched, Task 8 remains pending).
Task 7: fix round 2/5 (3 addressed, 0 open - handle-first namespace and identity-bound topology/module closure; deterministic first-fragment CANCEL buffering with post-fragment STOP/reuse; exact manifest binary path-to-COFF-machine equality; implementation commit `1a0ac799`; all focused and clean A/B gates passed).
Task 7: in progress after independent review fix round 2 (round-2 fixes and verification evidence are recorded; independent re-review is still required; the approved atomic unpublished protocol remains `openvino.official/1`, `FIX-01` remains closed by two new independent clean native closures, `DEP-02` remains unresolved and untouched, and Task 8 remains pending with no dependent work performed).
Task 7: fix round 3/5 (3 addressed, 0 open - continuous sticky runtime/package namespace monitoring across success and exceptional load paths; explicit first-fragment CANCEL release ownership with delayed-input-pump proof and preserved post-fragment STOP/reuse; final-handle System32/WinSxS-only system-module classification; exact-final clean A/B native and guarded 18/18 official managed evidence recorded; implementation commit `1cdc4507`).
Task 7: in progress after independent review fix round 3 (round-3 fixes and verification evidence are recorded; independent re-review is still required; the approved atomic unpublished protocol remains `openvino.official/1`, `FIX-01` remains closed by two exact-final independent clean native closures, `DEP-02` remains unresolved and untouched, and Task 8 remains pending with no dependent work performed).
Task 7: fix round 4/5 (3 addressed, 0 open - safe terminally-observed overlapped teardown with process-lifetime detach on bounded cleanup failure; continuous double-buffer namespace handoff with handle-proven named-stream policy; final package/runtime/module integrity gates before successful and terminal publication; implementation commit `aff60a55`; clean A/B native and 18/18 official managed evidence recorded).
Task 7: complete (independent round-4 rereview APPROVED with no Critical or Important findings; reviewer verification: native 7/7, official CPU 6/6, managed client 13/13, contracts 60/60; the approved atomic unpublished protocol remains `openvino.official/1`; `FIX-01` remains closed by two independent clean native closures; `DEP-02` remains unresolved and untouched; Task 8 remains pending with no dependent work performed).
Task 8: fix round 3/5 implemented and verified (all concurrent Cancel/Dispose callers join one teardown; active STOP/CANCEL is gated by the exact worker-confirmed generation turn; retirement failure commits an interactive subscribed recovery destination and app-close joins the same owner; exact RED/GREEN and release evidence are recorded in `task-8-report.md`; prior closures remain closed; retained operation-owned temp-stage residue is documented; awaiting scoped rereview; Task 9/GPU/conversion/TurboQuant remain untouched).
Task 8: fix round 4/5 implemented and verified (reentrant Cancel/Dispose joins a placeholder published before observer or teardown work; one exact worker-confirmed turn gate arbitrates STOP, CANCEL, prompt success, and prompt failure; one navigation placeholder is published before Frame/retirement callbacks so reentrant/concurrent Return and shutdown join it; exact RED/GREEN, native E2E, Release, verifier, and audit evidence are recorded in `task-8-report.md`; prior closures remain closed; the retained policy-owned temp-stage residue is documented and untouched; awaiting scoped rereview; Task 9/GPU/conversion/TurboQuant remain untouched).
Task 8: fix round 5/5 implemented and verified (all worker turn events validate exact session/turn identity; STOP carries the expected confirmed ID through the protected conversation and holds one async terminal-operation gate through dispatch; shutdown publishes one lifecycle owner before joining navigation and deterministically rejects later Return publication while retiring/detaching the exact owned page once; focused/full, protected process, packaged recovery/native, affected navigation, pinned Release, verifier, and audit evidence are recorded in `task-8-report.md`; prior closures remain closed; the retained policy-owned temp-stage residue is documented and untouched; awaiting scoped rereview; Task 9/GPU/conversion/TurboQuant remain untouched).
Task 8: Ruling: the round-5 reviewer confirmed exact worker-event identity, exact STOP propagation, and shutdown/navigation publication, but found one real load-bearing residual: CANCEL can claim terminal ownership without joining the `turnTerminalGate` already held by an in-flight STOP. The five-round breaker is reached, so no sixth Task 8 fix round is authorized. Task 9 must begin with a focused TDD correction that makes STOP, CANCEL, prompt completion, and prompt failure share the same exact-turn terminal-operation gate before any hosted or UCL evidence may be generated; if wrong, a STOP/CANCEL race could misclassify or cross turn ownership and all Task 9 evidence would be invalid.
Task 8: complete (commits 875e6c08..ade05c7e, five reviewed fix rounds, 1 load-bearing finding carried by ruling into Task 9; all other Task 8 findings addressed; final implementation evidence 360/360 plus Release/verifier gates).
Task 9: prerequisite correction complete (`e7f803b6` - STOP, exact-ID CANCEL,
prompt completion, and prompt failure now share one exact-turn async terminal
gate through external dispatch/settlement; deterministic completion/failure RED
0/2 and GREEN 2/2; route 177/177, contracts 60/60, client 13/13, process 40/40;
no monitor lock held across async I/O).
Task 9: in progress after implementation (`b125e5c3` - hosted and manual-only
trusted UCL CPU workflows, closed typed evidence generator, hostile privacy
verifier, and workflow contracts; final local gates contracts 84/84, route
177/177, native 7/7, client 13/13, process 40/40, adapter 35/35, tamper 5/5,
workflow/privacy 24/24, YAML 2/2; UCL local gate exit 1/no output). Independent
review is required; hosted/UCL dispatch and publication are pending external
authorization, `UCL-01` remains open, and MVP/UCL acceptance is not claimed.
Task 9: fix round 1/5 implemented and locally verified (`dbfc5a45` - dispatch
inputs isolated through environment nodes; exact single-file evidence lifecycle;
closed measured results and native floor 7; external read-only fixture consumed
by native/managed UCL gates; final-handle path closure; exact typed privacy;
GitHub/protected-environment provenance; structural YAML/path contracts; inverse
terminal reservation race corrected). Final gates: contracts 131/131, workflow
71/71, privacy/evidence 62/62, route 179/179, terminal arbitration 4/4, client
13/13, hosted process 41/41, clean external-fixture process 41/41, native 7/7,
app Release x64 passed, worker residue zero. Awaiting independent re-review;
hosted/UCL dispatch and upload remain pending external authorization, `UCL-01`
remains open, no acceptance is claimed, and Task 10 remains pending.
Task 9: fix round 2/5 design ruling: use one fixed single-process UCL campaign
owner that acquires and validates both external-root leases before any consumer,
retains them across all native/managed/measurement/post-integrity work, and
releases only after the closed campaign result is finalized; build/test outputs
must remain outside the leased roots, while only outer always-run
cleanup/status/privacy/upload steps may follow. Evidence is route-neutral
`cpu.json` with no self-asserted UCL/trust field and external GitHub workflow/run
provenance is the trust boundary. A binding verifier may validate externally
supplied metadata plus digest shape/correlation but must never claim to
authenticate it, and local output is not UCL-acceptable without that external
run record. Exact test-only `Microsoft.PowerShell.SDK` 7.4.18 is approved for
PowerShell AST workflow checks, with its license/source and deterministic
restore recorded and no production dependency. If wrong, a retained-lease owner
defect would invalidate UCL evidence; the test-only SDK adds restore and license
surface.
Task 9: fix round 2/5 implemented inline (route-neutral `cpu.json`; strict
decoded-name JSON parser; external non-authenticating run-record binding;
single retained trusted-root campaign with final-handle/file-identity/ADS,
sticky topology, and boundary snapshot checks; PowerShell-AST workflow
contracts with exact locked test dependency; disposal joins permanent teardown
after a losing turn reservation). Final local gates: contracts 142/142, route
181/181 with zero skipped, disposal 2/2, client 13/13, hosted process 41/41,
external-fixture process 41/41 plus exact consumption, native A/B 7/7 each,
Release x64 app passed, eight scripts/modules parse cleanly, worker residue
zero. User directed inline execution, so round-two review was an inline
security/diff audit rather than an independent subagent review. Hosted/UCL
dispatch remain unauthorized, `UCL-01` remains open, and no MVP/UCL acceptance
is claimed.
Task 9: complete locally (implementation and fail-closed gates are ready;
external hosted/UCL execution remains an explicit acceptance prerequisite, not
a local implementation blocker).
Task 10: complete locally (explicit CPU/GPU/GPU.n grammar; exact native device
enumeration, compile probe, execution-device equality, and no CPU fallback;
GPU plugin sealed into worker/app packaging; optional GPU-01 UCL evidence binds
commit, measured device, Intel adapter/driver, plugin/runtime/config, named
negative/positive tests, cancellation, and cleanup. Final Release gates:
contracts 162/162, route 181/181, client 13/13, process 42 passed plus one
deliberately skipped physical-GPU test, native A/B 7/7, packaged app build
passed, parser/diff/residue checks clean. This host has no Intel GPU, so GPU-01
remains open, GPU stays app-hidden, and no physical GPU acceptance is claimed.)
Task 11: complete locally (dense Granite source inspection with retained
snapshot leases and exact Safetensors/tokenizer validation; approved
`optimum==2.3.0` correction closes `DEP-02` with a verified 55-wheel CPython
3.13 Windows x64 closure; isolated/offline/socket-denied converter exports
complete FP16 model/tokenizer/detokenizer IR; final Stage J contains 23,756
manifested files and passes a real repository-fixture export. Final gates:
source 15/15, dependency contracts 6/6, route 196/196, process 47/47 applicable
with only physical GPU-01 skipped, and Release x64 app build passed. Inline
security review corrected fixture ignore/byte preservation and root overlap.)
Task 12: complete locally (`97e2d65f`, `c814d362`, `62a83f31` - sealed offline
conversion, atomic publication, and shared UI conversion action; external hosted
and UCL evidence remains a release prerequisite).
Task 13: complete locally (`0fc4c287`, `a4e1652f` - independently published
FP16/INT8/INT4 optimization and evidence-gated standard candidates).
Task 14: complete locally (`9cb832c2` - stable official-route acceptance filter,
closed typed evidence binding, and hosted/UCL workflow integration; workflows
were not externally dispatched).
Task 15: complete locally (exact released upstream recovery; empty patch ledger;
sealed MSVC x64 runtime closure; codec and real two-turn stateful CPU-SDPA
conformance; external security/license approval and Granite activation evidence
remain open and prevent registration).
Task 16: complete locally (distinct `openvino.turboquant/1` worker and sealed
runtime closure; typed TBQ4/TBQ4 activation/build evidence; real two-turn
stateful CPU-SDPA probe; exact nested manifest binding; strict activation and
forced-negative gates; manual trusted UCL workflow. Final local evidence:
Release worker/native 1/1, contracts 167/167, client 13/13, TurboQuant 8/8,
official native regression 7/7, tamper rejection, and synthetic-fixture
activation rejection. No UCL dispatch occurred; real pinned-Granite matched
quality/memory/performance evidence and external security/licence approval
remain open, so registration and any Active claim stay forbidden.)
Task 17: complete locally (gated `openvino.turboquant` Experimental adapter,
exact tuple/campaign activation policy, bounded evidence, and explicit
single-use official/GGUF fallback; central registration and TurboQuant
packaging remain intentionally absent because the pinned-Granite UCL campaign
and external security/licence approvals are open, so `F-M21`, `F-M22`,
`N-M11`, and `DR-WF-011` are not claimed closed.)
Task 18: complete locally (ordered fail-closed release orchestration; bounded
artifact-privacy and exact-owned-root cleanup scanners; architecture,
packaging, accessibility, lifecycle, and evidence-admission contracts; cleanup
ledger reconciled at 638/638. Final local gates include OpenVINO contracts
187/187, Model Inspection contracts 357/357, OpenVINO app 272/272, final
process integration 61 passed/zero failed/one physical-GPU skip, and packaged
affected WinUI 580/580. With external evidence absent the gate returns exactly
`openvino_release_blocked`; hosted/UCL candidate evidence, real pinned-Granite
TurboQuant campaign, external security/license approval, optional GPU-01, and
I0-controlled 398-atom RTM regeneration remain open, so release acceptance,
central TurboQuant registration, and TurboQuant packaging are not claimed.)
