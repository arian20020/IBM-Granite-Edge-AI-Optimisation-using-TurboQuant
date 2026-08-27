# Task 7 Report: Official Native Inspector and CPU Session Worker

## Status and scope

DONE_WITH_CONCERNS.

- Base: `fa6498ac092425b763e2d206da9e3baf9395b10d`.
- Focused protocol/interface correction: `14f36a3f fix(openvino): complete official worker protocol`.
- Native implementation commit message: `feat(openvino): add official CPU CLI worker`.
- Protocol remains `openvino.official/1`; both managed and native ends changed atomically on this branch.
- `FIX-01` is closed by the two independent clean-stage proofs recorded below.
- Task 5 fixture bytes, Task 6 feature-local handoff, dependency locks, GGUF/converter code, UI, solution, registries, and `DEP-02` were not changed.

The tracked Task 7 implementation consists only of the native worker, its build and
closed-manifest scripts, native tests/probes, managed official-process tests and
parent-loss fixture, plus the narrow authoritative-token validator correction.
No OpenVINO archive, extraction tree, build output, staged binary, model, or other
large generated artifact is tracked.

## Approved interface correction

The initial Task 2/4 wire could not carry the protected package location and exact
identity needed by a real native process. Per the Task 7 ruling, tests were changed
first and the following bounded closed shapes were implemented before native work:

- `startInspection` carries `inspectionRunId`, an absolute protected
  `packagePath`, `packageManifestDigest`, `modelSha256`, and `modelLengthBytes`.
- `startSession` carries `sessionId`, `inspectionRunId`, the same package/model
  identity, literal `CPU`, and the bounded context/new-token limits.
- Inspection emits `inspectionStarted`, the four ordered fixed progress stages,
  and one terminal event. Successful native evidence contains all three parse
  flags, the matching package/model identity, and exact Runtime/GenAI/Tokenizers
  build evidence plus the worker-manifest digest.
- Session start reports requested `CPU`, actual execution devices `[`CPU`]`, the
  protocol ID, and build evidence. `closeSession` is distinct from cancellation;
  managed `CloseAsync` and idle disposal use graceful close.
- `turnCompleted` carries authoritative native prompt/generated counts and the
  closed `completed`/`stopped` disposition.
- The native generated count includes EOS. `TokenEvent.Sequence` counts only
  nonempty decoded text fragments. The validator remembers the prompt request,
  requires generated count not to exceed it, enforces context/token/text bounds,
  and does not equate a fragment count with a native token count.

`packagePath` is accepted only on protected stdin. It is never accepted as a CLI
argument and is never returned on stdout/stderr or placed in diagnostics.

Interface TDD evidence:

- The existing managed real-process fixture initially failed because the new
  mandatory identities/events and close behavior did not exist.
- Contract/client/process suites were made green before native implementation:
  contracts 55/55, client 5/5, process 32/32 at the interface checkpoint.
- The later EOS authority test was RED because one `fixture` fragment with native
  generated count 2 was rejected by the old equality rule. Final contract coverage
  is 58/58, including valid fragment/count divergence, over-request rejection, and
  rejection of empty EOS token frames.

## Exact official closure and toolchain

All build inputs came from
`C:\Users\Arian\AppData\Local\Temp\granite-o1-task7-official-archives`, outside
tracked paths, and passed the Task 1 `Official` verifier before each scripted build.

| Component | Release / build identity | Commit | Archive length | Archive SHA-256 |
| --- | --- | --- | ---: | --- |
| OpenVINO Runtime | `2026.3.0-22451-8a17657b995-releases/2026/3` | `8a17657b995fd3b4a52f8484acfcf2bb61214623` | 207,538,323 | `4b26374eb342c3e0e4488b230cf8a16b6327e22b1ef12e45e5533cead06a66e3` |
| OpenVINO GenAI | `2026.3.0.0-3277-bd8d6542e3c` | `bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0` | 230,909,001 | `3d01daee17953a1b787841cff6a4d19b5de1d06d996af9433969139b2b5ea657` |
| OpenVINO Tokenizers | `2026.3.0.0-703-183c6f25cda` | `183c6f25cda2a469cba5eff8b72022d2d51ba0ca` | 5,458,557 | `4724214920485771d271c3fdcca9988bd2dd4e07c7f0437a36e38ee69e15b61f` |

The build used CMake 3.31.6-msvc6, Visual Studio 2022 Build Tools, MSVC
19.44.35215, Windows SDK 10.0.26100.0, Release, and x64. The script discovers
CMake/tooling and does not make the recorded local CMake location its only option.

## Native implementation

### Protocol and lifecycle

- The only accepted command line is the executable plus
  `--protocol openvino.official/1`.
- stdin/stdout use binary UTF-8 JSONL framing. The parser rejects duplicate/unknown
  fields, invalid UTF-8, NUL, lines over exactly 1 MiB, JSON deeper than 32, stale
  identifiers, noncanonical IDs/hashes, unbounded strings, and invalid states.
- Inspection uses real `ov::Core::read_model` for `openvino_model.xml`,
  `openvino_tokenizer.xml`, and `openvino_detokenizer.xml` without compiling a
  device. Package/model hashes and lengths must match before parse, and the package
  snapshot must remain identical afterward.
- Sessions are restricted to exact `CPU`; aliases, `AUTO`, `HETERO`, GPU, empty,
  and unknown device strings are rejected. The worker supports two and up to the
  closed 32 turns, idempotent active stop/cancel, idle close, terminal immutability,
  bounded context/text, and parent-pipe loss.

### Real LLMPipeline and streaming

The worker constructs the released native C++
`ov::genai::LLMPipeline(package, "CPU", {ATTENTION_BACKEND=SDPA})`. The literal CPU
selector is non-composite. A real CPU plugin/runtime closure, successful pipeline
construction, and real generation support `actualExecutionDevices=["CPU"]`; this
is constructor-bound evidence because the public pinned LLMPipeline has no compiled
model property to query.

Every turn supplies a fresh explicit `ChatHistory` containing the complete retained
route-owned user/assistant history. It uses the minimal route template

```text
{{ bos_token }}{% for message in messages %}{{ message['content'] }}{% endfor %}
```

with `apply_chat_template=true`, deterministic generation, and no deprecated
`start_chat()`/`finish_chat()`. No EOS suppression, forced minimum generation, or
fixture-byte change is used.

Each turn reconstructs the exact CPU/SDPA pipeline and then rechecks loaded-module
closure. This correctness-first choice follows bounded real evidence: reusing one
pipeline for the fixture's second explicit history threw the pinned native exception
`Request contains no states` at GenAI `utils.cpp:575`. A fresh pipeline with the
full explicit history succeeds on both turns.

Streaming uses the public string callback `StreamerVariant`, polls stop/cancel
before publishing every fragment, and never decodes reentrantly. A token-ID callback
that called `Tokenizer::decode` from inside the callback deadlocked in the bounded
probe; it was removed. Authoritative counts come from
`DecodedResults.perf_metrics.get_num_input_tokens()` and
`get_num_generated_tokens()` and are cross-checked against the prompt request,
context, and output bounds.

### Loader and staged closure

- OpenVINO/GenAI imports are delay-loaded. Before their first API call, the worker
  clears current/ambient DLL search, enables system32 plus the verified worker
  directory, and validates the manifest, every length/hash, exact topology, and
  absence of reparse points.
- After initialization and every generation, loaded non-Windows modules must resolve
  under the verified worker closure. No PATH, developer setup, Python binding,
  user-package, network, cache, converter, or fallback runtime is used at execution.
- The manifest scripts require the exact eight root binaries and eight notices,
  closed ordinal paths, positive lengths, lowercase SHA-256, x64 PE machine
  `0x8664`, no unexpected executable content, no additions/omissions, no reparse
  point, and no alternate data stream on the root, directory, manifest, or files.

Manifest TDD first proved that a re-manifested SysWOW64 x86 PE was incorrectly
accepted by the earlier MZ-only check (`verify_exit=0`). The x64 machine check made
that same case fail. Final operation-owned hostile results were:

| Case | Result |
| --- | --- |
| Valid x64 closure | `worker_manifest_valid`, exit 0 |
| Missing required DLL | `worker_manifest_invalid`, exit 1 |
| Unexpected added file | `worker_manifest_invalid`, exit 1 |
| One-byte content tamper | `worker_manifest_invalid`, exit 1 |
| Directory junction/reparse | `worker_manifest_invalid`, exit 1 |
| Re-manifested x86 PE as required DLL | `worker_manifest_invalid`, exit 1 |
| Root ADS | `worker_manifest_invalid`, exit 1 |
| `licenses` directory ADS | `worker_manifest_invalid`, exit 1 |
| DLL ADS | `worker_manifest_invalid`, exit 1 |

## Native and lifecycle TDD evidence

- Managed official-process RED: the missing native worker produced the expected
  controlled launch failure.
- Native build RED: CMake named the intentionally missing worker sources. Protocol
  and state tests then drove the closed parser/state core.
- Binary framing RED: managed parsing rejected CR introduced by Windows text mode;
  switching stdin/stdout to binary preserved exact LF JSONL.
- Pipe RED: the generation loop could block on stdin. A Win32 `PeekNamedPipe` probe
  test first failed to compile on the absent API, then covered empty, available,
  drained/closed pipe states.
- Chat probe: content-only ChatHistory generated EOS only. Adding normal BOS template
  semantics produced one nonempty `fixture` fragment with native input count 2 and
  generated count 2 (text token plus EOS).
- Second-turn RED: cached native pipeline state threw the exact `states.size() > 0`
  exception above. Fresh per-turn construction with retained explicit history made
  the bounded two-turn probe and CTest green.
- STOP RED: full-output equality incorrectly treated a suppressed partial callback
  as inconsistent. The correction preserves metric/bound checks but compares full
  text only for normal completion. STOP now returns one stopped turn and the next
  prompt succeeds.
- Parent helper compile RED used nonexistent JSON helper/string overloads, then a
  test path omitted the x64 output segment. Correct source-generated byte framing
  and configuration-aware x64 path made the real parent-loss test green.

Focused lifecycle acceptance on the real fixture passed:

- STOP immediately after `generationStarted` produces a successful stopped terminal;
  the same session then returns `fixture` on the next prompt.
- CANCEL owns the session and exposes zero actionable token/turn events before the
  terminal `operation_cancelled` managed result.
- Context limit 1 fails before generation with `runtime_context_exceeded`.
- Operation-owned copies with truncated main/tokenizer XML fail with the fixed
  path-free `package_inconsistent_resource` result.
- A managed parent exits after `generationStarted`; closed protected pipes cause the
  worker to exit within the five-second bound. All cases leave zero worker residue.

## Independent clean-stage FIX-01 proof

The build script was run twice, sequentially, from the same verified archives into
two initially absent build and stage roots:

| Closure | Build/stage | CTest | Worker EXE identity | Manifest identity |
| --- | --- | --- | --- | --- |
| A | `granite-o1-task7-final-build-a-20260820` / `granite-o1-task7-final-stage-a-20260820` | 3/3 | 355,328 bytes, `9f1e4d8ebf09d58926f4d073905c60119905250829018726f81eaf878937b275` | 4,059 bytes, `688857e911b6c7f6dfc8388b990a4bdf63fdabe90531d140b9bc73b3d0463199` |
| B | `granite-o1-task7-final-build-b-20260821` / `granite-o1-task7-final-stage-b-20260821` | 3/3 | 355,328 bytes, `79cf887e97613901cab01ba60d3213298bd5832d752a8f3137751edeaa213535` | 4,059 bytes, `60605f0e75a5f7142790843c30dc3c455b75793c85e1668475dbecea1b27dd2e` |

Both scripts emitted `official_worker_built`; each performed Release compile/link,
CTest, manifest creation, and manifest verification. The two worker hashes may differ
because they are independent Visual Studio builds; each manifest binds its own exact
binary. Managed evidence is compared to the SHA-256 of the actual corresponding
manifest, not merely checked for shape.

The managed canonical test then inspected the committed Task 5 package and generated
two turns through each clean closure. Every turn produced exactly one nonempty
`fixture` fragment, authoritative `generatedTokenCount=2` including EOS, a positive
bounded prompt count, and a successful graceful close. The four-test official-native
acceptance passed 4/4. This is real pinned native C++ LLMPipeline execution, not a
mock, Python binding, substituted executable, or single lucky run; therefore
`FIX-01` is closed.

Canonical fixture identities remained:

- package manifest: `b5316ac62e1e846b33ec92e5ad555859238af70ffd6b75aa50500995fbe15372`
- model SHA-256: `894dd0aac21e588d5cf78994d90aa0dcba8284626c976a4e0c89c0273b452c1c`
- model length: 88 bytes

## Final sequential verification

| Verification | Result |
| --- | --- |
| Clean A native CTest | 3/3 passed |
| Clean B native CTest | 3/3 passed |
| Managed official native acceptance | 4/4 passed |
| Exact-manifest canonical follow-up | 1/1 passed |
| Task 5 fixture verifier | `fixture_valid` |
| Task 1 official dependency verifier | `dependency_lock_valid` |
| OpenVINO contracts, Release | 58/58 passed |
| OpenVINO managed client, Release | 5/5 passed |
| OpenVINO full process suite, Release | 36/36 passed |
| Task 6/static OpenVINO suite, Release | 124/124 passed |
| Legacy ModelInspection managed client, Release | 119/119 passed |
| Legacy ModelInspection process suite, Release | 31/32 passed; known timing concern below |
| `git diff --check` | exit 0; only existing LF-to-CRLF working-copy notices |
| Process residue query | zero official/fixture processes |

The legacy process suite's sole failure was the already documented load-sensitive
`OverallTimeoutStartsAfterStartNotDuringDelayedHandshake`: expected fixture exit 3,
observed exit 2 after 4.412 seconds. This is the same pre-existing Task 4/6 timing
concern, outside all Task 7/OpenVINO native paths. It was recorded once and not blind
rerun or used to justify changing a deadline.

## Concerns and handoff

- Reconstructing the pipeline every turn is correctness-first and avoids hidden
  GenAI incremental state, but adds model-load latency. A later optimization must
  preserve full explicit history and prove equivalent state behavior against both
  stateless fixtures and real stateful models.
- CPU actual-device evidence is bound to the exact non-composite `CPU` constructor,
  verified CPU plugin/runtime closure, and successful generation. The pinned public
  API exposes no compiled-model device property. Task 10 must obtain explicit GPU
  actual-device proof before exposing a GPU route.
- The route-owned minimal template is a raw-content fallback demonstrated by this
  fixture. It is not a claim that arbitrary model-specific chat formatting is
  supported.
- Package-native parse failures intentionally collapse to the fixed path-free
  `package_inconsistent_resource` result; raw OpenVINO exceptions never cross the
  protocol or diagnostic boundary.
- The known unrelated legacy delayed-handshake timing test remains 31/32 in this
  final run. `DEP-02` remains unresolved and untouched.

## Independent review fix round 1

This section supersedes the earlier security/lifetime, Hello-evidence, and final
test-count statements wherever they differ. Review implementation is commit
`4fa4200b` (`fix(openvino): retain verified native closures`). No Task 8 source,
UI/solution/registry wiring, Task 5 fixture byte, Task 6 feature-local handoff type,
GGUF path, converter path, dependency lock, or `DEP-02` state changed.

### Protocol-governance ruling

Before the native implementation, the root explicitly approved the narrow Task 2/4
wire correction because the earlier unpublished feature-branch shape could not
represent a protected arbitrary package selection, native parse/build evidence, or
graceful session close. Producer and all consumers changed atomically on
`feature/openvino-route`; no released or external compatibility consumer exists.
The authoritative identifier therefore remains `openvino.official/1`. Bumping it
solely for this pre-release atomic correction would manufacture a migration that no
consumer can perform. The Task 2 report now records the corrected v1 baseline.

### Review RED matrix

| Review requirement | Failing evidence before production correction |
| --- | --- |
| Combined terminal context bound | Contract sequence test accepted prompt 63 plus generated 2 under context 64 (0/1). |
| Hello build identity | Literal test did not compile because the two-argument `HelloEvent` did not exist. |
| Independent runtime trust and lease | Two managed resolver tests did not compile: no resolver/lease and no installation trust-anchor parameters. |
| Native package transaction | Native lease test did not compile: no lease/stage observers or retained transaction API. |
| Partial STOP and reuse | Real canonical process case failed `runtime_protocol_failed` because generation completed before STOP ownership. |
| Typed corrupt startup | Real process returned `runtime_protocol_failed` instead of the fixed package-integrity result; after native mapping, managed startup still collapsed `sessionFailed` until its mapping was corrected. |
| Hello mismatch containment | The fake worker had no wrong-build-evidence scenario and startup accepted no caller-known comparison. |

The tests exercise deterministic behavior and real handles/processes, not only
source-shape assertions. Production changes followed the captured RED cases.

### Independent runtime trust and retained closure

`OpenVinoWorkerInstallation` now carries caller-known exact build evidence,
including the approved worker-manifest digest, plus an explicit closed AMD64 binary
inventory. `OpenVinoWorkerClosureResolver` validates that trusted digest before
manifest entries, enforces the closed ordered topology, checks reparse points and
alternate streams, validates final handle identities and containment, hashes and
lengths every accepted file through held handles, and checks the exact x64 policy.
The policy inventory is copied after validation so callers cannot mutate it during
startup.

The resulting `VerifiedOpenVinoWorkerClosure` retains the already-verified worker
executable, manifest, root, directories, DLLs, and resources with write/delete
sharing denied. Ownership transfers to the conversation and lasts until terminal
process/tree/session disposal; inspection owns the same lease through clean exit.
Tests prove both re-manifesting against a rewritten closure and post-verification
DLL replacement fail before execution, and prove exclusive access returns after
disposal. The worker-reported digest is evidence checked against this independent
caller-known anchor; it is never the anchor itself.

Native `runtime_context` independently leases the exact runtime root, manifest,
parent directories, and every manifest file before the first dependent version or
load call, and retains them until worker exit. Its real observer test attempts to
truncate `worker-manifest.json` and `openvino.dll` after native verification but
before dependent loading; both attempts are denied, while exclusive manifest access
succeeds after context disposal. The build wires the verified Tokenizers runtime
beside this test reproducibly rather than relying on ambient PATH or a reused cache.

### Native package transaction and failure ownership

The native `package_lease` is move-only and retains the reparse-safe package root,
every accepted directory, and every closed-manifest resource with write/delete
sharing denied. It validates alternate streams, final-handle containment and
identity, length, SHA-256, package identity, and model identity. The lease spans all
three real `read_model` calls, `LLMPipeline` construction, and each correctness-first
per-turn pipeline reconstruction. A real canonical-fixture regression attempts
replacement at main-model read, tokenizer read, detokenizer read, constructor, and
generation stages; every attempt is denied, generation returns `fixture` with two
native generated tokens, and deletion works after lease disposal.

`runtime_context_exceeded` now comes only from the explicit checked context
equation. Package parse/integrity faults map to fixed package codes; requested-token
or output/protocol bounds map to `runtime_protocol_failed`; module/evidence escape
maps to fatal `runtime_integrity_failed`; and unknown load/generation faults map to
fatal `runtime_load_failed`. A startup failure emits exactly one typed
`sessionFailed` and then exits nonzero. Managed startup preserves that typed support
code rather than collapsing it to protocol failure. Session-present pipe I/O remains
protocol failure; pre-launch closure-policy failures remain integrity/policy results.

Managed terminal validation is subtraction-safe: prompt tokens must fit the session
context and generated tokens must be no greater than the remaining context. The
63+2-under-64 RED case is rejected without overflow-prone addition.

### Hello, partial STOP, and residue proof

The atomic v1 Hello now contains both protocol ID and the exact Runtime, GenAI,
Tokenizers, and manifest identities. Managed startup compares the full value to the
trusted installation evidence before accepting Hello, retains it for later
inspection/session evidence comparisons, and rejects the fake worker's
`wrong-build-evidence` case.

The real-process STOP proof waits for at least one nonempty native callback fragment,
then issues STOP, observes a successful partial `turnCompleted` with `stopped`, and
reuses the same session for another successful prompt. For the tiny canonical model,
the worker uses a bounded two-second condition-variable handoff after its first real
callback fragment so the control command can acquire ownership; no text is faked,
the fixture is unchanged, and EOS/generation policy is unchanged. CANCEL still emits
no actionable token/turn after ownership and terminates as `sessionCancelled`.

The residue regression runs from operation-owned copies of the entire stage and
package with isolated TEMP/TMP roots. After close/disposal it proves no named worker
remains, exclusively opens the manifest, executable, `openvino.dll`, and model for
read/write with `FileShare.None`, proves the isolated temp root has no listener,
cache, or temporary artifacts, deletes the copied package/stage/temp trees, and
asserts all roots are absent. The worker creates no listener or cache root.

### Independent clean A/B review proof

Both roots began absent and were built/staged from the exact verified Task 1
archives outside tracked paths. Each build script completed Release x64 compile,
link, stage, manifest generation/verification, and the five-test native suite.

| Closure | Result | Worker identity | Manifest identity |
| --- | --- | --- | --- |
| Review A (`granite-o1-task7-review1-build-a` / `...stage-a`) | CTest 5/5; `worker_manifest_valid` | 379,904 bytes; `d465bfeb1f327d1dde0adc98041e9e3a7260c1801147064dbacfb853702de712` | 4,059 bytes; `d6ef5c266a6019e84d996cd3d0ac6c68a7644ac040c1c1046b16c3951a6ee2bf` |
| Review B (`granite-o1-task7-review1-build-b` / `...stage-b`) | CTest 5/5; `worker_manifest_valid` | 379,904 bytes; `beb45613b0301c5eae7b668f67354381efd93867c078076c487e2a3b1acbd2a7` | 4,059 bytes; `a26b6d6b3509a8c8901febd35e1a44aaae10391830db3a7587e394a0173ba07f` |

The official managed suite passed 6/6 across distinct A/B closures, including real
inspection, two-turn generation, typed hostile package failures, parent loss,
post-fragment STOP/reuse, CANCEL, context rejection, graceful close, and strengthened
residue ownership. This independently reconfirms `FIX-01` closed.

### Final review verification

| Verification | Result |
| --- | --- |
| Clean review A native CTest | 5/5 passed |
| Clean review B native CTest | 5/5 passed |
| Managed official native acceptance | 6/6 passed in 20.280s |
| OpenVINO contracts, Release | 60/60 passed in 34.130s |
| OpenVINO managed client, Release | 7/7 passed; final focused ownership rerun 7/7 in 2.080s |
| OpenVINO full process suite, Release | 39/39 passed in 63.123s |
| Hostile Hello suite | 7/7 passed, including wrong build evidence |
| Strengthened residue case | 1/1 passed |
| Real STOP/CANCEL/context case | 1/1 passed |
| Real corrupt startup case | 1/1 passed |
| Task 6/static OpenVINO suite | 124/124 passed in 18.879s |
| Legacy ModelInspection managed client | 119/119 passed in 9.473s |
| Legacy ModelInspection process suite | 31/32; sole documented delayed-handshake timing failure, expected exit 3/actual 2 at 4.747s; not rerun |
| Review A and B manifest verifiers | `worker_manifest_valid` for each |
| Task 1 official dependency verifier | `dependency_lock_valid` |
| Task 5 fixture verifier | `fixture_valid` |
| Process residue query | zero official/fake workers |

The exact official identities remain Runtime
`2026.3.0-22451-8a17657b995-releases/2026/3`, GenAI
`2026.3.0.0-3277-bd8d6542e3c`, and Tokenizers
`2026.3.0.0-703-183c6f25cda`. The only known unrelated regression exception remains
the pre-existing legacy delayed-handshake timing test. The correctness-first
per-turn pipeline reconstruction has a load-latency cost, and the tiny-fixture STOP
handoff is a bounded test seam; neither weakens native evidence or deadlines. CPU
actual-device evidence remains constructor-bound because the pinned public API has
no compiled-model property; Task 10 must separately prove GPU actual-device
selection. `DEP-02` remains unresolved and Task 8 remains pending.
The round-1 fixes are implemented and verified, but Task 7 remains in progress until
independent re-review accepts them.

## Independent review fix round 2/5

Round 2 began from clean commit `4625eeca` and is implemented by `1a0ac799`.
The review was accepted as three
concrete implementation defects: namespace-to-handle gaps and incomplete module
membership, a first-fragment CANCEL publication race, and subset-only binary
machine policy. No Task 8 files or behavior were changed.

### RED evidence

The managed closure tests first ran 5 cases with 3 passing and 2 failing:
`ChildInsertedAfterAcquisitionInvalidatesTheClosedInventory` accepted an inserted
child, and `BinaryOmittedFromCallerPolicyInvalidatesTheClosure` accepted a manifest
binary omitted from caller policy. The desired exact machine-map API then produced
the expected `CS0246` compile RED for `OpenVinoWorkerBinaryMachine`. Native desired
observer/identity/cancellation tests produced compile REDs `C2660` (the runtime and
package acquisitions lacked the path-open observer overload), `C2664` (module
verification lacked a runtime identity context), and `C2039`
(`turn_control.first_fragment_buffered` did not exist). The review's independent
official run supplied the behavioral cancellation RED: 5/6, with one token
published where immediate CANCEL required zero.

The first compiled native behavior tests correctly exposed the test-fixture gap:
the original directory handle access denied the intended rename before the reparse
swap could occur. Reducing directory access to `FILE_READ_ATTRIBUTES` allowed the
actual junction swap and proved that the handle-first check rejects it. The full
fake-process compatibility run initially failed 0/33 at startup with the same fixed
`RuntimeIntegrityFailed` outcome. Direct PE header inspection isolated a test-policy
error: `Platform=x64` publishes the route's two project IL assemblies with COFF
machine `0x8664`, while only `Microsoft.Windows.SDK.NET.dll` and `WinRT.Runtime.dll`
are `0x014c`. Correcting those exact fixture identities made a representative
two-turn process test pass before the complete suite was rerun.

### Handle-first closure and topology membership

The caller policy is now an exact, closed relative-path-to-COFF-machine map.
Manifest `.exe`/`.dll` inventory must equal it exactly; omission, extra declaration,
or machine mismatch is rejected. Official binaries are all required to be AMD64;
the managed process fixture explicitly declares each published PE's real machine.

Managed runtime resolution opens the root, manifest, every directory, and every
manifest file with `CreateFileW` plus `FILE_FLAG_OPEN_REPARSE_POINT`, denies
write/delete sharing, then validates `FileAttributeTagInfo`, final-path containment,
ADS, identity, length, hash, and PE machine from that retained handle. Both topology
scans reopen every enumerated entry handle-first and compare its file identity with
the retained approved identity, including the manifest. The verified executable is
constructed from its already-retained manifest-entry handle. Deterministic tests
cover a directory path-to-junction swap between enumeration/open, child insertion,
accepted-file removal/restoration denial, and exact binary omission/addition/machine
mismatch.

The native runtime and package acquisitions use the same open-reparse/deny-write-
delete/validate-exact-handle ordering for roots, manifests, directories, and files.
They retain every accepted handle and identity for the complete inspection or
session lifetime. Exact topology is rescanned immediately before and after the
Tokenizers extension load, all three `read_model` calls, Runtime/GenAI/Tokenizers
identity loads, and every validation/per-turn `LLMPipeline` construction. Exceptional
load exits also perform the post scan before propagating the failure. The native
tests deterministically exercise namespace-to-junction swaps, child insertion at a
load boundary, and replacement/removal denial at tokenizer, model, and pipeline
boundaries.

Loaded non-system modules are no longer accepted by worker-root prefix. Their
volume serial and file ID must equal one of the retained approved manifest-file
identities. The behavior test loads a copied but unlisted DLL inside the staged root
and proves exact membership rejects it as `runtime_integrity_failed`.

### Deterministic first-fragment ownership

The real GenAI string callback buffers its first nonempty fragment before
publication and signals a bounded handoff. It holds the control mutex for up to
100 ms so an already-sent or immediate CANCEL can acquire ownership; on CANCEL the
real fragment is discarded and the callback returns `CANCEL`. Once published, the
separate bounded canonical-fixture handoff remains available for a test to observe
the real fragment and send STOP. Nothing synthesizes text, generation policy and
fixture bytes remain unchanged, and all waits remain bounded.

Three fresh sequential official runs passed 6/6 each (18/18 aggregate). Each run
proved that STOP follows at least one nonempty native fragment, returns a partial
`turnCompleted` with `stopped`, and permits a subsequent completed `fixture` prompt;
immediate CANCEL published exactly zero token events and terminated with
`OperationCancelled`.

### Independent clean A/B proof and exact identities

The review-2 build and stage roots began absent and remained outside tracked paths.
Both scripts rebuilt Release x64 from only the Task 1 official archive closure,
generated and verified a new closed manifest, and passed native CTest 5/5.

| Closure | Native result | Worker identity | Manifest identity |
| --- | --- | --- | --- |
| Review 2 A (`granite-o1-task7-review2-build-a` / `...stage-a`) | 5/5 in 9.93s; `official_worker_built`; `worker_manifest_valid` | 412,672 bytes; `7b8121c6e8ca519e76fdd43cda09c8ed568222d5584d5984063a2637753ebd6b` | 4,059 bytes; `fa7687cffaf0352b9d1ced01cdd8d69d7579e6ec3594513859dacba3ccccc2ab` |
| Review 2 B (`granite-o1-task7-review2-build-b` / `...stage-b`) | 5/5 in 9.85s; `official_worker_built`; `worker_manifest_valid` | 412,672 bytes; `dd72937b7979a4c40e75a8c250d8f4bcacf14f9c84d9838a51193517824bcddd` | 4,059 bytes; `3422c5d18ac2a999a554b03aab01c99d5a684028f31f9ee0a756cff1a87f3360` |

The runtime identities remain exactly Runtime
`2026.3.0-22451-8a17657b995-releases/2026/3`, GenAI
`2026.3.0.0-3277-bd8d6542e3c`, and Tokenizers
`2026.3.0.0-703-183c6f25cda`.

### Round-2 verification

| Verification | Result |
| --- | --- |
| Focused native suite after exceptional post-rescan hardening | 5/5 passed in 10.27s |
| Clean review-2 A / B native CTest | 5/5 in 9.93s / 5/5 in 9.85s |
| Official managed stability runs | 6/6, 6/6, 6/6; 18/18 aggregate |
| OpenVINO contracts, Release | 60/60 passed in 40.48s |
| OpenVINO managed client, Release | 13/13 passed in 2.80s |
| Full fake process containment | 33/33 passed in 45.81s |
| Full OpenVINO process suite, Release | 39/39 passed in 74.65s |
| Task 6/static OpenVINO suite | 124/124 passed in 21.50s |
| Legacy ModelInspection managed client | 119/119 passed in 10.99s |
| Legacy ModelInspection process suite | 31/32; sole documented delayed-handshake timing failure, expected exit 3/actual 2; not rerun |
| Task 1 official dependency verifier | `dependency_lock_valid` |
| Task 5 fixture verifier | `fixture_valid` |
| Review-2 A and B manifest verifiers | `worker_manifest_valid` for each |
| Process and tracked-artifact audit | zero official/fake workers; no build, stage, archive, or binary artifact tracked; `git diff --check` clean |

`FIX-01` remains closed by the two independent clean native closures and identical
managed proof. The protocol-governance ruling remains unchanged: the atomic,
pre-approved correction stays `openvino.official/1` because this feature-branch
contract has no released compatibility consumer. `DEP-02` is untouched. Task 7
remains in progress until independent round-2 re-review accepts these fixes, and
Task 8 remains pending with no dependent work performed.

## Independent review fix round 3/5

Round 3 was received and evaluated against the native code at `d68a3068`.
The three Important findings were technically valid. No Task 8 work was
started, no protocol shape changed, and the official protocol remains the
approved unpublished atomic `openvino.official/1` contract. The scoped
implementation commit is `1cdc4507`.

### Round-3 RED evidence

Strict TDD established the missing behavior before production edits:

| Finding | RED evidence |
| --- | --- |
| Transient namespace/module changes | A real copied system DLL was inserted, loaded, unloaded, deleted, and exact final topology restored inside the runtime load observer. The existing boundary accepted both return/throw variants. The analogous package insert/delete observer was also accepted (`0/1`, `transient package mutation escaped the load boundary`). Desired runtime observer/classifier APIs initially failed to link with two `LNK2019` errors. |
| Deterministic CANCEL ownership | With the first genuine decoded fragment buffered and the input pump delayed 250ms, the old fixed 100ms wait published `fixture`; the regression failed `first-fragment cancellation leaked output`. |
| System-module exception | The prior classifier admitted every module below the broad Windows directory. The new behavior test required only validated System32 and WinSxS roots and rejected a copied DLL below a sibling `Windows\\Temp` tree. |

### Round-3 implementation

- Added an overlapped Windows `ReadDirectoryChangesW` namespace monitor owned
  for the complete runtime/package lease lifetime. It is armed on a separately
  opened, identity-matched root handle before closed enumeration, monitors the
  subtree and file/directory name, size, write, creation, security, and NTFS
  stream changes, and makes any notification, overflow, API error, or
  termination sticky-fatal. `CancelIoEx` plus bounded event completion closes
  the already-armed request without a watcher-thread scheduling gap.
- The first implementation reused the deny-delete root handle, which blocked a
  valid directory-rename test. The evidence-driven correction retains the
  deny-write/delete lease handle and arms a second share-delete monitor handle
  only after validating its final path, attributes, and file identity against
  the retained root.
- Each initial-acquisition finalization, native pre/post load boundary, success
  path, catch path, and cleanup checks the sticky monitor plus exact topology;
  module membership is independently checked even if another integrity check
  already failed. A 50ms wait is used only once per monitor at completed
  boundaries to drain the request already registered with the kernel; pre-load
  checks remain nonblocking. This reduced native CTest from the initial 27s
  implementation to 15-16s without changing any deadline.
- Runtime behavior tests now insert and load a real unlisted DLL, unload/delete
  it, restore exact topology, then exercise both return and throw variants.
  Package tests exercise the analogous transient insert/delete return/throw
  variants. All fail with the fixed integrity result despite a clean final
  scan/module set.
- Replaced the fixed 100ms first-fragment publication window with an explicit
  mutex/condition-variable handshake. The real callback buffers the first
  genuine fragment. Production main first drains ready control input; CANCEL
  takes ownership and discards the fragment, while an observed-empty pipe
  explicitly releases it. The existing post-publication STOP handoff remains
  separate, so STOP after one observed real fragment completes a partial turn
  and the session remains reusable. The deterministic native test records
  CANCEL as already written, delays the input pump 250ms, and proves zero
  streamed output with no deadlock.
- Removed the broad `%WINDIR%` module exemption. Every candidate module and
  allowed root is opened and validated by final handle. Only exact System32 and
  canonical WinSxS component-boundary descendants are exempt; all other
  non-system modules must match an approved retained manifest file identity.
- Hardened official test cleanup so every recursively deleted root must be a
  strict descendant of the system temp root and can be neither equal to nor an
  ancestor/descendant of its source. A/B boundary failure diagnostics remain
  path-free.

### Systematic debugging notes

The first continuous monitor missed immediate create/delete completion when
polled at zero timeout. A bounded wait on the already-armed OVERLAPPED event
proved the kernel notification and made both transient return/throw variants
deterministic. A subsequent blanket drain at every nested check made the real
5-second startup gate fail; limiting the drain to one per monitor at completed
boundaries restored the unchanged real deadline.

During final stability, the first clean B stage was externally reduced after a
successful 6/6 run to only its executable and manifest. The managed resolver
correctly failed closed with `RuntimeIntegrityFailed` before launch. Raw A,
retained-lease raw launch, and retained-lease protected launch were each green;
a new clean B was then tested method-by-method. Canonical and each other
official method left the replacement B at exactly 17 files and
`worker_manifest_valid`. Three subsequent guarded full official runs also
preserved that exact inventory. No test-owned source-stage deletion reproduced;
the damaged stage is excluded from acceptance evidence.

### Exact final native closures

Both accepted closures were built independently by the scripted Release/x64
builder from absent build/stage roots and only the Task 1 verified official
archives.

| Closure | Native CTest | Worker executable | Manifest |
| --- | --- | --- | --- |
| Review 3 A (`granite-o1-task7-review3-release-build-a` / `...release-stage-a`) | 5/5 in 16.20s | 413,184 bytes; `37b166a13ac3eec05fb43e69e29c9ddef20caa840831ca131484fc65d3442eaa` | 4,059 bytes; `65fa9a4bbad5d6c832d7a2ab0f5edfb8ab11d52bad1bf6de54f7502406fb2bd3` |
| Review 3 B (`granite-o1-task7-review3-release2-build-b` / `...release2-stage-b`) | 5/5 in 16.22s | 413,184 bytes; `fb5c1803a7d3c5f1431c61cfab9ba21bd64b5311ac5ba4a09a29a52c358622c9` | 4,059 bytes; `9e24fce8f982f512a2677f1ba5a40beacd3fe3c429f89e66560deef995b559b2` |

The pinned build identities remain exactly Runtime
`2026.3.0-22451-8a17657b995-releases/2026/3`, GenAI
`2026.3.0.0-3277-bd8d6542e3c`, and Tokenizers
`2026.3.0.0-703-183c6f25cda`.

### Round-3 verification

| Verification | Result |
| --- | --- |
| Current-source focused native CTest after final exceptional-path audit | 5/5 in 16.28s |
| Exact clean review-3 A / B native CTest | 5/5 in 16.20s / 5/5 in 16.22s |
| Guarded official managed stability runs | 6/6, 6/6, 6/6; 18/18 aggregate; B stayed 17 files and manifest-valid after every run |
| Final full OpenVINO process suite | 39/39 in 80.703s |
| OpenVINO contracts, Release | 60/60 in 36.311s |
| OpenVINO managed client, Release | 13/13 in 3.976s |
| Task 6/static OpenVINO suite | 124/124 in 21.019s |
| Legacy ModelInspection managed client | 119/119 in 10.650s |
| Legacy ModelInspection process suite | 31/32; sole documented delayed-handshake timing failure, expected exit 3/actual 2; not rerun |
| Task 1 official dependency verifier | `dependency_lock_valid` |
| Task 5 fixture verifier | `fixture_valid` |
| Exact-final A and B manifest verifiers | `worker_manifest_valid` for each |

`FIX-01` remains closed by these two independent clean native closures and the
identical managed proof. `DEP-02` remains unresolved and untouched. The
correctness-first per-turn pipeline reconstruction remains a known load-latency
concern for later measured optimization, and Task 10 must still establish
explicit GPU actual-device evidence before GPU exposure. Task 7 remains in
progress until independent round-3 re-review accepts these fixes; Task 8 remains
pending with no dependent work performed.

## Independent review fix round 4/5

Round 4 was received and re-evaluated against the native implementation at
`a0a90317`. Both Important findings and the named-stream hardening finding were
technically valid. No Task 8 work, protocol change, fixture change, dependency
change, or UI/registration work was performed. The scoped implementation commit
is `aff60a55`.

### Round-4 RED evidence

Strict TDD established the missing behavior before production changes:

| Finding | RED evidence |
| --- | --- |
| Safe overlapped teardown | The new ownership tests initially failed to compile because no instance-scoped lifecycle/fault seam, terminal barrier, or safe-detach behavior existed. Source inspection confirmed the destructor closed the directory/event and freed OVERLAPPED/buffer storage after a 5-second `WAIT_TIMEOUT`. |
| Final terminal integrity | Three real-package publication cases initially failed to compile because there was no terminal-publication gate. After the gate first compiled, a transient insert/delete was detected but exposed that `package_changed` was incorrectly nonfatal. |
| Named-stream fallback | The existing constructor retried without stream flags on every `ERROR_INVALID_PARAMETER`, without proving the volume lacked named streams. Desired supported-volume rejection and unsupported-volume fallback cases initially had no API or behavior. |

The final RED/GREEN matrix contains nine monitor behaviors plus three terminal
publication behaviors. The terminal cases cover inspection return and genuine
canonical-fixture normal/stopped `official_session.generate` returns; both turn
cases emitted the real `fixture` fragment before the test mutated the namespace
between the earlier native boundary and terminal publication.

### Round-4 implementation

- Replaced default `unique_ptr` teardown with explicit heap-state ownership. A
  monitor request is destroyed only after `CancelIoEx` and a terminal
  `GetOverlappedResult` outcome. Timeout, wait error, or still-pending completion
  makes integrity sticky-fatal and intentionally detaches the complete state,
  handles, events, OVERLAPPED objects, and buffers until process termination.
  Move assignment uses the same safe release path. Capacity is reserved before
  arming requests so allocation failure cannot destroy kernel-owned storage.
- Added instance-scoped lifecycle and fault options with no mutable global hook.
  Tests prove normal cancellation destroys/closes exactly once, while forced
  shutdown timeout/error records a detach and performs zero state/handle/event
  destruction.
- Replaced the former 50ms notification wait with a continuous double-buffer
  handoff. A new event/OVERLAPPED/buffer request is armed on the same validated
  root before the old request is cancelled and terminally consumed. Exact
  topology is scanned while the replacement is already armed, then a second
  handoff consumes changes from the scan interval. Notifications, overflow,
  errors, or delayed terminal completion are sticky failures. A deterministic
  create/delete between handoffs proves the replacement catches the mutation.
- `GetVolumeInformationByHandleW` now supplies the authoritative
  `FILE_NAMED_STREAMS` capability. `ERROR_INVALID_PARAMETER` from stream-filter
  registration fails closed on a named-stream-capable volume; base-filter
  fallback is permitted only when that handle proves named streams unsupported.
- Added `verify_terminal_topology` to package/runtime leases and
  `verify_terminal_integrity` to sessions. The terminal publication helper runs
  every supplied package/runtime/module check independently, buffers the event,
  and calls `write_event` only after all checks succeed. Inspection retains its
  package lease through the write. Hello, SessionStarted, InspectionCompleted,
  SessionCompleted, SessionCancelled, and completed/stopped TurnCompleted now
  use the gate. A failing package monitor is fatal `package_changed`, producing
  the existing single typed inspection/session failure and nonzero worker exit,
  never the success event.

The Windows API contract requires OVERLAPPED storage to remain valid until
cancellation completion is observed. The double-buffer design also uses the
documented directory-handle notification buffer semantics: the replacement
request is registered before the old request is retired, eliminating a watcher
registration gap rather than adding a timing window.

The timeout, wait-error, overflow, and I/O-error monitor cases above are
deterministic instance-scoped fault-injection tests; they are not claims that a
real filesystem driver produced those faults during this run. Genuine
kernel/integration coverage is provided by clean overlapped cancellation, the
real create/delete mutation caught across a double-buffer handoff, transient
runtime DLL load/unload and package namespace mutation tests, and the staged
native worker/process integration suites.

### Exact round-4 native closures

Both closures were built independently by the scripted Release/x64 builder from
absent build/stage roots and only the Task 1 verified official archives. The
final post-build test strengthening changed test source only; the staged worker
production source was unchanged, so no production rebuild was required by the
root ruling.

| Closure | Native CTest | Worker executable | Manifest |
| --- | --- | --- | --- |
| Review 4 A (`granite-o1-task7-review4-build-a` / `...stage-a`) | 7/7 in 10.99s | 436,736 bytes; `3d4e5254ff1dc061366fb2a45de7a536f60daafd575ff1f31d8d8871c902095c` | 4,059 bytes; `b09feb0c73969d5e13a1d755937fc580d1507ee5c5f0397ebd44d305dfb6613b` |
| Review 4 B (`granite-o1-task7-review4-build-b` / `...stage-b`) | 7/7 in 11.03s | 436,736 bytes; `51f5c5579251b2d9a81bb1c743acdc3f361abd6e297782701d04ea4691311c94` | 4,059 bytes; `0f656f6f2afe0b7246d0746f458ad6ed02e23be943145779b0160dd69aff2ebe` |

Each closure has exactly 17 files and 16 closed manifest entries. Pinned build
identities remain Runtime `2026.3.0-22451-8a17657b995-releases/2026/3`, GenAI
`2026.3.0.0-3277-bd8d6542e3c`, and Tokenizers
`2026.3.0.0-703-183c6f25cda`.

### Round-4 verification

| Verification | Result |
| --- | --- |
| Current-source native CTest, including strengthened real-turn terminal seam | 7/7 in 13.45s |
| Exact clean review-4 A / B native CTest | 7/7 in 10.99s / 7/7 in 11.03s |
| Official managed stability runs | 6/6, 6/6, 6/6; 18/18 aggregate |
| OpenVINO contracts, Release | 60/60 in 35.747s |
| OpenVINO managed client, Release | 13/13 in 3.417s |
| Full OpenVINO process suite, Release | 39/39 in 63.490s |
| Task 6/static OpenVINO suite | 124/124 in 17.830s |
| Legacy ModelInspection managed client | 119/119 in 8.598s |
| Legacy ModelInspection process suite | 31/31 in 97.105s with only the documented delayed-handshake timing case excluded and not rerun |
| Task 1 official dependency verifier | `dependency_lock_valid` |
| Task 5 fixture verifier | `fixture_valid` |
| Review-4 A and B manifest verifiers | `worker_manifest_valid` for each |
| Residue/tracked-artifact audit | zero official worker processes; no tracked build, stage, archive, DLL, or worker executable; `git diff --check` clean |

Independent round-4 rereview was APPROVED with no Critical or Important
findings. The reviewer independently verified native 7/7, official CPU 6/6,
managed client 13/13, and contracts 60/60. `FIX-01` remains closed by the two
independent real native closures and three identical managed proof runs.
`DEP-02` remains unresolved and untouched. The correctness-first per-turn
reconstruction remains a known latency concern, and Task 10 must still
establish explicit GPU actual-device proof. Task 7 is complete; Task 8 remains
pending and no dependent work was performed.
