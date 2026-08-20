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
