# Model Inspection Worker Gate 2 Verification

> Living evidence record for the protected Windows x64 worker boundary. Gate 2 is complete only when the final documentation head passes the unified workflow and the exact run metadata is recorded below.

## Scope

Gate 2 establishes a hardened process and protocol boundary for future Model Inspection native work:

- strict bounded UTF-8 transport;
- trusted executable and minimal environment policy;
- reviewed Win32 ownership primitives;
- creation-time Job Object containment and exact handle inheritance;
- one-request worker host with an honest unavailable-engine seam;
- deterministic abnormal-process fixture;
- handshake, conversation and exit integrity;
- cancellation, timeout, descendant cleanup, stream-flood and concurrency proof;
- executable architecture fitness checks and unified CI evidence.

## Non-claims

Gate 2 does not implement or prove LLamaSharp model loading, factual GGUF evidence extraction, application mappers/classifier/service/ViewModel, live WinUI progress or cancellation, MSIX inclusion, a real-model application route, OpenVINO, TurboQuant, Hardware Fit, GPU acceleration or chat.

## Source identity

- Repository: `arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant`
- Branch: `feature/model-inspection-worker-host`
- Draft PR: `#47`
- Stacked base: `feature/model-inspection-runtime-integration` at `0d50f27405d66be944e1352284b68928ae228e74`
- SDK selection: repository `global.json` on the .NET 10 SDK line; Gate 2 projects deliberately target .NET 8 and the process boundary targets `win-x64`.

## Task evidence

### Tasks 1–4 — project graph, transport, client domain, trust policy

- Task 1 head `c247559f4bdcc7504621374a176830c5d6f4eaee`; hosted workflow `31040807199`, job `92424504782`.
- Task 2 head `f577a3589566614a430b2c39323f34118f6221d9`; transport workflow `31054971261`, job `92470388749`; 20/20 tests passed.
- Task 3 head `0d0e486967a2cbc46b4b618e1b91e6a0f5428d8a`; client workflow `31061909890`, job `92491436316`; 13/13 domain tests passed.
- Task 4 head `4fd837ff31914d5e6686d42363cd115da4b525a3`; workflow `31065161555`, job `92501258960`; 33/33 trust-policy tests passed.
- Each task also passed the complete Windows regression with 69/69 contracts and 207/207 packaged tests at its verified code head.

### Task 5 — safe Win32 process primitives

Verified implementation includes x64 Win32 structures/constants, owning SafeHandles, three redirected pipe pairs, a kill-on-close Job Object, managed `PROC_THREAD_ATTRIBUTE_LIST`, cancellable asynchronous process waiting and mutable UTF-16 `CreateProcessW` input. A reviewer regression fixed premature release of the Job/handle-list backing buffers.

- Verified code head: `93aacbebb4411c2e99c2fe3ad2a4e263c763d663`
- Focused native-boundary tests: 49/49 passed.
- Complete Windows regression: passed.

### Task 6 — atomic launch and exact inheritance

`CreateProcessW` uses `STARTUPINFOEX`, `PROC_THREAD_ATTRIBUTE_JOB_LIST` and `PROC_THREAD_ATTRIBUTE_HANDLE_LIST`. There is no `Process.Start`, post-start Job assignment, breakaway or uncontained fallback. Real child processes prove an unrelated inheritable handle is excluded and the Job becomes empty after exit.

- Verified code head: `331a94373b452406c5cc4d3d31f911d6d949565d`
- WorkerClient tests: 54/54 passed.
- Initial real-process tests: 2/2 passed.
- Complete Windows regression: passed.

### Tasks 7–9 — worker host, fixture and conversation integrity

The production worker implements hello-first lifecycle, one start command, started/progress/terminal sequencing, one-winner terminal arbitration, exact exit codes, parent monitoring and the controlled unavailable-engine seam. The fixture contains malformed output, crash, hang, cancellation, descendant, flood, environment and handle scenarios without leaking those switches into production code. The client verifies hello identity, request sequencing, bounded stderr and terminal/exit agreement.

- Worker-host focused tests: 4/4 passed.
- Task 9 integration implementation reached code head `74a961e75b47a5bdb71f9be7c38973d979667e6d` before Task 10 cancellation/timeout closure.
- Compiler/analyzer closure head: `84e11ef5fcf496e5f08004b88d38fb763dee3d28`.
- Focused process workflow `31105370641`, job `92628994776`: 27/27 passed.

### Task 10 — cancellation, timeout and process-tree stability

- Exact verified head: `6aa486751cf8a31d28469dbdfb898244f675d24b`
- Focused workflow: `31105788735`
- Focused job: `92630406714`
- Full process suite: 27/27 passed.
- Timing-sensitive stability set: 9/9 passed across five additional repetitions.
- Total focused process executions: 72 passed, 0 failed, 0 skipped.
- Fixture orphan check: passed after every repetition.

Complete Windows regression on the same exact head:

- Workflow: `31105788612`
- Job: `92630448748`
- Contract tests: 69/69 passed.
- All eight Gate 2 projects restored and built.
- WinUI application build: succeeded.
- Packaged unit/UI-thread tests: 207/207 passed, 0 failed, 0 skipped.
- Unit-test artifact: `unit-test-results-31105788612-1`
- Artifact ID: `8969661936`
- Size: `54,157` bytes
- Digest: `sha256:c381db9137a87bd5e090b00ef21ea89b8c1d1f70a309b34a7d96d3da6e42ec20`

## Task 11 closure design

The final unified workflow now requires at least 75 contract/fitness tests; builds all eight Gate 2 projects; publishes the production worker and fixture to separate roots; records distinct Contract, Transport, Worker, WorkerClient and WorkerProcess TRX reports; performs an always-run orphan check and bounded privacy scan; preserves a publish hash manifest; then executes the unchanged WinUI packaged regression.

Architecture fitness checks enforce:

- WinUI remains disconnected from WorkerClient in Gate 2;
- abnormal fixture tokens do not enter production source;
- no production `Process.Start` or local listener exists;
- both creation-time Job and exact handle attributes remain present;
- Gate 3 runtime packages do not enter Gate 2 projects.

## Final exact-head closure

**Pending:** replace this paragraph with the exact final documentation commit, unified workflow/run/job IDs, per-TRX counters, artifact identity/digest, zero-orphan result, privacy scan result and warnings/errors. Until that exact-head run succeeds, PR #47 remains draft and Gate 3 is blocked.

## Definition of Done

- [x] Strict bounded transport.
- [x] Trusted path and minimal environment.
- [x] Explicit Win32 resource ownership.
- [x] Creation-time Job containment and exact handle allowlist.
- [x] Honest production worker host.
- [x] Isolated abnormal fixture.
- [x] Handshake, sequence and exit integrity.
- [x] Cooperative cancellation and forced cleanup.
- [x] Timeout, stderr flood, descendant and concurrency proof.
- [x] Executable architecture fitness tests.
- [x] Unified workflow and separate publish roots implemented.
- [ ] Unified workflow passes on the exact final documentation head.
- [ ] Final artifacts, privacy scan and zero-orphan evidence recorded on that same head.
- [ ] Final Superpowers verification and code review completed.
