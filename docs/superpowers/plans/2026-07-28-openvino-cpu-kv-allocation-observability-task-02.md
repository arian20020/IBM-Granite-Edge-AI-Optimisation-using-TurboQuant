# Task 02: CPU State Allocation Snapshot Domain Model Micro-Plan

## Task 1: Execute the CPU State Allocation Snapshot Domain Model

> **For the implementer:** Use `superpowers:test-driven-development` and
> `superpowers:subagent-driven-development`. Apply only the exact changes below
> in the derived OpenVINO core checkout. Stop after the task commit and return
> the complete evidence bundle for independent spec and quality review. Do not
> export or edit the tracked parent patch yet; the roadmap reserves the single
> binary-safe patch export for Task 7.

**Roadmap task:** Task 2 in
`docs/superpowers/plans/2026-07-28-openvino-cpu-kv-allocation-observability.md`
(especially lines 22-60, 260-344, 346-707, 943-1031, and 2085-2212 in the
2026-07-28 revision).

**Goal:** Add a private, `CPU_DEBUG_CAPS`-only allocation snapshot model that
measures physical retained backing from owner capacity, preserves active
descriptor bytes as a separate field, deduplicates aliases by tagged shared
ownership, captures the three concrete CPU variable-state classes, and
serializes deterministic bounded JSON without addresses, prompts, tensor
contents, generated text, or public ABI.

**Acceptance gates satisfied:** immutable official source; exact derived-source
identity; strict RED then GREEN; retained-capacity rather than descriptor-size
physical accounting; global tagged-owner alias deduplication; owned beam and
scale/ZP accounting; fail-closed unknown/external handling; checked arithmetic;
complete bounded JSON; no ordinary-build symbols or marker strings; exact
CMake rediscovery; every Python test, compiled test/probe, configure, and build
under the independently accepted revision-5 Job Object guard with an exact
2,048 MiB RAM floor and zero survivors; a hash-pinned caller-approved Python
runtime; a three-artifact wrapper/record/log binding for every command; focused
tests twice; production plugin build; diff/process/marker audits; focused
commit; later patch-range/replay identity; independent Spec PASS and Quality
PASS.

## Revision history

- **Revision 12 (2026-07-28):** repairs a second safe pre-controller launcher
  failure from the exact committed revision 11 without changing Task 2 source,
  build, test, Job, memory-safety, Job-ready v2, boundary v4, or receipt v5
  contracts. The failed launch again created no attempt-005 artifact,
  controller process, heavy process, or source change. A fresh disposable empty
  Job in Windows PowerShell 5.1 reproduced the complete class-9 readback:
  `LimitFlags=8192`, every checked time, working-set, active-process, affinity,
  and memory-limit field zero, `PriorityClass=32`, and `SchedulingClass=5`.
  Those final two values are the canonical inactive defaults returned by
  Windows when neither `JOB_OBJECT_LIMIT_PRIORITY_CLASS` nor
  `JOB_OBJECT_LIMIT_SCHEDULING_CLASS` is enabled. The launcher and GREEN timeout
  verifier now require those exact inactive defaults instead of incorrectly
  requiring zero; the sole active limit remains exactly kill-on-close
  (`0x00002000`).
- **Revision 11 (2026-07-28):** repairs a safe pre-controller launcher failure
  from the exact committed revision 10 without changing Task 2 source, build,
  test, Job, memory-safety, Job-ready v2, boundary v4, or receipt v5 contracts.
  The failed launch created no attempt-005 artifact, controller process, heavy
  process, or source change. Fresh Windows PowerShell 5.1 reproduction showed
  that each variable holding a managed `System.Type` selected
  `Marshal.SizeOf(object)` at `Marshal.SizeOf($type)` and failed exactly with
  `Exception calling "SizeOf" with "1" argument(s): "Type
  'System.RuntimeType' cannot be marshaled as an unmanaged structure; no
  meaningful size or offset can be computed."` The paired uncast
  `PtrToStructure($buffer, $type)` calls likewise failed exactly with
  `Exception calling "PtrToStructure" with "2" argument(s): "The specified
  structure must be blittable or have layout information. Parameter name:
  structure"` because PowerShell again selected the object overload. The eight
  class-9/accounting overload sites in the launcher and GREEN signal now
  explicitly select the type overload with `SizeOf([type]$type)` and
  `PtrToStructure($buffer, [type]$type)`. All six `SizeOf` struct-instance
  calls and all five already explicit `SizeOf` type calls remain unchanged.
- **Revision 10 (2026-07-28):** closes the launcher-authentication and physical
  guard-orchestration gaps without changing the Task 2 source, build, test,
  Job, or memory-safety contracts. The direct prelaunch gate now owns every
  final control-script identity literal and passes them as mandatory launcher
  parameters. The launcher contains no downstream identity literals, validates
  its own exact path, ordinary-file status, byte count, and SHA-256 from the
  authenticated parameters, validates the three downstream scripts from their
  parameters, and publishes that authenticated launcher identity in Job-ready
  v2. The controller independently hard-pins the stable launcher, binds the
  identity into boundary v4, and final receipt v5 audits it again. Every guard
  wrapper invocation now resolves the wrapper and execution CWD through the
  exact physical parent recovery worktree; only evidence/materialization paths
  use `R:`.
- **Revision 9 (2026-07-28):** resolves the two final revision-8 review
  blockers without changing the build, test, Job, memory-safety, or source
  contracts. All orchestration and both direct execution gates now explicitly
  run from the exact physical parent recovery worktree; `R:` remains a
  separately validated evidence/materialization alias and is never the
  execution CWD. The derived Task 2 commit now uses only command-local
  `user.name=Arian B` and
  `user.email=194431897+arian20020@users.noreply.github.com`, accepts an absent
  ambient `git var GIT_AUTHOR_IDENT`, proves local/global configuration and
  process identity variables unchanged, and receipt-binds exact
  `%an/%ae/%cn/%ce`.
- **Revision 8 (2026-07-28):** corrects two deterministic revision-7
  verification mismatches without changing the safety architecture. The
  supervisor still passes only
  `QUOTA_LIMITS_HARDWS_MAX_ENABLE` (`set_flags=4`) to
  `SetProcessWorkingSetSizeEx`, but now requires the immutable-attempt-004
  getter state
  `QUOTA_LIMITS_HARDWS_MIN_DISABLE | QUOTA_LIMITS_HARDWS_MAX_ENABLE`
  (`readback_flags=6`) from `GetProcessWorkingSetSizeEx`. Those distinct
  values are explicitly logged, parsed, boundary-bound, and receipt-bound.
  Revision 8 also makes the final audit consume the cap event's actual
  `nested_job_member` property instead of the nonexistent
  `inherited_job_member` property. Finally, it eliminates the runnable
  controller's child-launch timeout race by atomically creating that
  controller inside a dedicated named kill-on-close Job through
  `STARTUPINFOEX/PROC_THREAD_ATTRIBUTE_JOB_LIST`. RED/GREEN timeout cleanup
  uses `TerminateJobObject`, then proves the root signalled, the Job PID list
  and active count are empty, and global heavy processes remain absent.
- **Revision 7 (2026-07-28):** replaces lossy process-table-only linker
  discovery with a non-elevated nested Job Object and I/O completion-port
  protocol. The reviewed supervisor creates the nested Job, configures and
  reads back a 6,144 MiB per-process and 7,168 MiB aggregate private-commit
  ceiling, creates the pinned
  `MSBuild.exe` root suspended, assigns it before first instruction, and only
  then resumes it. Every descendant process-start notification is consumed;
  every owned `link.exe` notification must resolve to one exact
  PID-plus-creation-time handle, pinned linker identity, 4,096 MiB
  working-set-cap application, and read-back. `ACTIVE_PROCESS_ZERO`, Job
  accounting, the final assigned-PID list, and exact start/exit reconciliation
  are mandatory. This revision deliberately retains
  `BuildProjectReferences=true`: a live dependency audit proved that forcing
  it false would omit required unit-test outputs and is therefore not an
  authorized shortcut. Revision 7 also eliminates numeric-PID tree killing,
  requires same-handle controller termination only when no live descendant or
  global heavy process exists, and directly gates and receipt-binds the exact
  launcher and GREEN-signal script bytes before either script executes.
- **Revision 6 (2026-07-28):** closes immutable attempt 004 from its exact
  failure receipt, starts fresh attempt 005, replaces the full-target RED build
  with a selected-file compile that must produce the exact missing-header
  `error C1083` with exit code 1, and executes the two heavy GREEN link steps
  serially under a reviewed, byte- and SHA-256-pinned, recursively
  descendant-aware 4,096 MiB `link.exe` working-set cap. The cap is explicitly
  a post-launch control: the plan does not claim that it exists before
  `link.exe` starts. Repeated empty build/test concurrency snapshots, repeated
  physical/virtual headroom gates, PID-plus-creation-time ancestry, inherited
  Job membership, and the outer 2,048 MiB floor close that bounded discovery
  window. This revision also restores the generated
  `O:\bin\intel64\Release` output paths and keeps the complete
  eight-label/24-artifact evidence contract.
- **Revision 5 (superseded for execution):** defined attempt 004 and the
  revision-5 guard boundary. Attempt 004 is now immutable historical evidence;
  none of its labels or files may be reused, extended, or rewritten.

### Revision 11 review-only Windows PowerShell 5.1 regression

This is review evidence only, not a workload and not materialized into the
launcher, controller, supervisor, or GREEN signal. A fresh no-profile Windows
PowerShell 5.1 process compiled the plan's four exact `Add-Type` bodies, bound
each of the following four nested struct types through a `$type` variable,
required the old uncast call to raise the exact revision-11 error above, and
required both the cast-variable and literal-type calls to return:

```text
Task02ControllerJobNative+JOBOBJECT_EXTENDED_LIMIT_INFORMATION SizeOf: old=FAIL; typed=144; literal=144
Task02ControllerJobNative+JOBOBJECT_EXTENDED_LIMIT_INFORMATION PtrToStructure: old=FAIL; typed=Task02ControllerJobNative+JOBOBJECT_EXTENDED_LIMIT_INFORMATION; literal=same
Task02ControllerJobNative+JOBOBJECT_BASIC_ACCOUNTING_INFORMATION SizeOf: old=FAIL; typed=48; literal=48
Task02ControllerJobNative+JOBOBJECT_BASIC_ACCOUNTING_INFORMATION PtrToStructure: old=FAIL; typed=Task02ControllerJobNative+JOBOBJECT_BASIC_ACCOUNTING_INFORMATION; literal=same
Task02ControllerJobTimeoutNative+JOBOBJECT_EXTENDED_LIMIT_INFORMATION SizeOf: old=FAIL; typed=144; literal=144
Task02ControllerJobTimeoutNative+JOBOBJECT_EXTENDED_LIMIT_INFORMATION PtrToStructure: old=FAIL; typed=Task02ControllerJobTimeoutNative+JOBOBJECT_EXTENDED_LIMIT_INFORMATION; literal=same
Task02ControllerJobTimeoutNative+JOBOBJECT_BASIC_ACCOUNTING_INFORMATION SizeOf: old=FAIL; typed=48; literal=48
Task02ControllerJobTimeoutNative+JOBOBJECT_BASIC_ACCOUNTING_INFORMATION PtrToStructure: old=FAIL; typed=Task02ControllerJobTimeoutNative+JOBOBJECT_BASIC_ACCOUNTING_INFORMATION; literal=same
```

The complete `Marshal.SizeOf` audit contains exactly 15 calls: these four
type-variable calls, five already explicit `[type]` calls, and six calls whose
arguments are concrete initialized struct instances. Only the four failing
type-variable `SizeOf` calls are changed. The complete `PtrToStructure` audit
contains exactly four calls, all four receive the same `$type` variables, and
all four now cast those variables explicitly. Only these eight failing
overload sites are changed.

## Fixed boundaries and assumptions

1. Run all orchestration and both direct launcher/GREEN gates from the exact
   physical parent recovery worktree
   `C:\Users\Student\IBM-Granite-Edge-AI-Optimisation-using-TurboQuant\.worktrees\openvino-turboquant-recovery`.
   `R:` is only a separately validated evidence and materialization alias for
   that same worktree; it is never an execution CWD or executable/script
   lookup root. Every guarded command uses the exact physical parent worktree
   as `-WorkingDirectory`, and the guard wrapper itself is loaded from the
   exact physical parent-root path. Resolve the derived core
   only from the accepted canonical identity at
   `R:\external\official-openvino\2026-07-19\openvino-cpu-state-observer.identity.json`.
   Task 01C supersedes the roadmap's earlier in-tree path: the accepted
   physical destination is exactly
   `C:\ov-wb04\2026-07-19\openvino-cpu-state-observer`, and its identity is the
   organized pointer; do not create a junction. `O:` must map to that exact
   physical directory before any configure. Never edit the shared clean source at
   `<shared-repository-root>\external\official-openvino\2026-07-19\openvino`.
2. The clean OpenVINO base is exactly
   `ede283a88e35465f0d680dabbf1f44080f8fc387`. The derived branch is exactly
   `project/cpu-state-allocation-observer`, and its Task 2 starting tree equals
   the clean base tree. This is a controlled resume: the only permitted derived
   worktree change at the boundary is the untracked RED fixture
   `src/plugins/intel_cpu/tests/unit/state_allocations_dump_test.cpp`, exactly
   22,516 bytes with SHA-256
   `a1e18eda017edec7f473dd8c885e172e7379e2f92f38894fda627dce1c6f608b`.
   Every other tracked, staged, or untracked change is rejected.
3. The independently accepted guard revision is exactly commit
   `37f4ea62326441233f34f5a2996d3c5667fae6ec`. It passed independent
   `SPEC PASS` and `QUALITY PASS` against the same exact files: 46/46 focused
   tests and 136/136 combined tests, with zero unowned `csc.exe` observations.
   The three accepted Git blobs are:
   `invoke_guarded_command.ps1` =
   `f47dbe4cabeab0e6c7d4363a01196a21eae147db`,
   `guarded_build.py` =
   `dffd7def889a1eef70d291009275be4ab078a7ee`, and
   `owned_process_guard.py` =
   `f9199294e4e0d50947803ce6468c6ba1cf2fc389`. Their exact worktree
   SHA-256 values are, respectively,
   `85e03b8cbd41b8821812004c00bf7f9b7525d9f3d8b666dd094a417e8af8fc3d`,
   `1a46fe30bbb622bc5887cb67bdaaeabf2113bf5531feef54bbd6b8ee86ae8d0a`,
   and
   `ab1fd112ee8b56922a9dba6a56a0b170d2fd7f02acb5ab9eb7b2ca88b3114da2`.
   The acceptance-test blob is
   `36f6f3e7eed6826c4e540f17dc4afc9f6d20f845` with worktree SHA-256
   `b32b74abb36c1d124281d44b200961d9833582b2c1027dc8ae252b47943e19b0`;
   this fourth file binds the stated 46/46 and 136/136 acceptance provenance
   even though it is not part of the runtime controller.
4. Every wrapper invocation uses exactly
   `C:\Users\Student\AppData\Local\Programs\Python\Python311\python.exe`
   (SHA-256
   `5f7b89a612c9b8af1d6456cdfcd1dbe5ca630849e79aebced9bee9a6694952ec`)
   and its sibling `python311.dll` (SHA-256
   `0817a2a657a24c0d5fbb60df56960f42fc66b3039d522ec952dab83e2d869364`).
   The wrapper starts it only with `-E -s -S -B`, an isolated pycache prefix,
   and `-m` from the hash-pinned repository root. The caller has approved this
   installed PSF-signed runtime and attests that no same-user process is
   mutating the Python installation, repository guard files, substitutions, or
   evidence directories while a command or its immediate audit is active. The
   caller also attests that no other build, compiler, linker, CMake, CTest, or
   `ov_cpu_unit_tests` process will be started while either heavy-build
   concurrency gate, guarded command, or immediate audit is active.
5. Revision 5 hard-codes `--minimum-available-ram-mib 2048` and independently
   rejects incomplete memory evidence, a minimum below `2147483648`, nonzero
   active Job PIDs after cleanup, any survivor PID, timeout, low-memory stop,
   emergency action, or forced termination. It requires the exact controller
   result (9 properties), guard record (35), wrapper receipt (15),
   `controller_runtime` (14), and `controller_binding` (9) schemas and JSON
   types before PowerShell casts anything. A wrapper failure or a post-return
   artifact mismatch is a task failure; never bypass it.
6. Git inspection, diff, add, and commit commands are direct because the
   roadmap classifies them as non-heavy. Every Python command, CMake configure,
   CMake build, compiled test, and compiled probe in this plan goes through
   `scripts/testing/invoke_guarded_command.ps1`. Do not launch `python`,
   `cmake`, `MSBuild`, `ctest`, or a test executable directly. This host's
   effective PowerShell policy is `Restricted` when every configured scope is
   `Undefined`. Each fresh no-profile controller is launched only with
   `-ExecutionPolicy Bypass`, proves its process scope is `Bypass`, and proves
   `CurrentUser` and `LocalMachine` are unchanged. Never call
   `Set-ExecutionPolicy` or change a persistent policy scope. The controller shell is
   trusted Windows PowerShell 5.1 loaded from the host's signed
   `PSHOME`/GAC surface; do not import caller modules, redefine commands, or
   run from another PowerShell edition.
7. The physical total is the sum of unique positive retained capacities:
   ordinary input/output/KV owners in `unique_reserved_bytes`, hidden beam
   owners in `beam_reserved_bytes`, and owned `PlainTensor` scale/ZP owners in
   `scale_zp_reserved_bytes`. `active_descriptor_bytes` is descriptive only and
   is never substituted for missing capacity.
8. Alias identity is `(AllocationOwnerDomain, shared-owner control block)`.
   Extraction converts first-seen shared owners to process-local positive
   ordinals; builder/JSON code never sees or serializes a pointer. DNNL and
   `PlainTensor` domains can never alias.
9. Shared owners are globally charged to the first emitted canonical record.
   Every later logical record retains its active descriptor and identical
   capacity but has `aliases_block_ordinal` set to the earlier canonical
   ordinal and contributes zero physical bytes. Per-state physical totals use
   the same canonical-first rule, so their sum equals request physical total.
10. An allocation with unknown retained capacity may exist in the in-memory
   snapshot for diagnosis, but serialization fails closed. A positive external
   DNNL allocation, zero-capacity emitted owner, ownership inconsistency, or
   capacity-changing alias is rejected before a snapshot is returned.
11. The only allowed phases are exact `fresh`, `seeded_no_infer`, and
    `post_infer`. Correlation IDs are exactly 32 lowercase hexadecimal
    characters. Trigger is exact `query_state`; plugin device is exact `CPU`.
12. The JSON limit is exactly 1 MiB (`1,048,576` bytes before the Task 3
    writer adds its newline). Field order is fixed by the implementation below.
13. This plan intentionally makes no CMake-file edit. The production target
    uses `file(GLOB_RECURSE ... src/*.cpp)` and the unit target uses
    `ov_add_test_target(ROOT ...)`; therefore a full reconfigure after adding
    each new `.cpp` is the exact integration action. Generated-project audits
    prove both files were discovered.
14. The complete execution uses fresh
    `experiments/raw-results/openvino-turboquant/2026-07-28/guards/task02-attempt-005`.
    Its eight short labels are unique, sequential, and exact:
    `t2-cfg-r`, `t2-red`, `t2-cfg-g`, `t2-unit-g`, `t2-list-g`,
    `t2-run-g1`, `t2-run-g2`, and `t2-plugin-g`. Each label produces
    one `.json` guard record, one `.log`, and one atomically published
    `.wrapper.json`: 8 triples and exactly 24 files. The wrapper stdout must be
    exactly one non-empty compact JSON line. The caller persists that line
    before any downstream use and then re-reads and SHA-256-verifies the public
    wrapper receipt, record, and log from the bytes it actually consumes.
15. `peak_working_set_bytes` and `peak_private_bytes` in guard records are
    250 ms sampled safety evidence over the guarded Job PIDs. They prove that
    memory observation occurred; they are not benchmark-grade instantaneous
    peak-RAM metrics and must never be copied into workbook performance fields
    or described as such.
16. Attempt 003 is an immutable bootstrap failure, not a guarded test attempt.
    It exited before RED-ready, before any guard artifact, and before any source
    edit because Windows PowerShell 5.1 promoted the expected `git cat-file`
    stderr for an absent new path to a terminating `NativeCommandError`. Its
    directory contains exactly `diagnostic.stdout.log` (0 bytes, SHA-256
    `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`),
    `diagnostic.stderr.log` (574 bytes, SHA-256
    `f7696a73d2e465870281987af6e480b9db0ce59f518b51daf5b783c7a0a18cea`),
    and `failure.json` (1,797 bytes, SHA-256
    `7d824034142720079dc02a456bad05b12a03fbebe20a2d457145725ebef2fecc`).
    Preserve all three files byte-for-byte. The corrected preimage probe uses
    exit-zero `git ls-tree` output; never reuse attempt 003.
17. Attempt 004 is immutable and contains exactly four files. Its sibling
    closure receipt is
    `experiments/raw-results/openvino-turboquant/2026-07-28/guards/task02-attempt-004-failure/failure.json`,
    exactly 13,042 bytes with SHA-256
    `57701e2bb243e9cf294662772f9d1e7a42cd892e57b1545e4bde43be58b9b313`.
    The attempt-only manifest is
    `94aea149cc38d826a62a8445eb6f19e907a003499f5927b5536619f54b4fc8c2`,
    the diagnostic-only manifest is
    `9c709913d23059def8ad31f0ec7d22b192907573cb0917fe395c1000adb551dd`,
    and the true global ordinal-sort combined manifest is exactly 2,742 bytes
    with SHA-256
    `e8b3dc46d45ac8dc43ba3f8b6bdc061fcf0f8512d01f7243c0d5398c87321bb4`.
    The receipt records that root cause was not captured; its successful
    selected-file and capped-link diagnostics are feasibility evidence only.
    Attempt 005 must independently reproduce RED and all GREEN gates.
18. The selected-file RED command is direct x64 `MSBuild.exe /t:ClCompile`
    for only `state_allocations_dump_test.cpp`, with project references,
    multiprocess compilation, PCH, and modules disabled. It must fail solely
    with the exact native diagnostic
    `error C1083: Cannot open include file: 'utils/state_allocations_dump.hpp': No such file or directory`.
19. The two heavy GREEN builds are serial direct project builds launched
    through the normal guard by the exact reviewed cap supervisor. The
    supervisor creates a private nested Job Object and completion port, applies
    and reads back a 6,144 MiB per-process and 7,168 MiB aggregate
    private-commit ceiling, creates the
    pinned x64 `MSBuild.exe` root suspended, assigns that exact handle to the
    nested Job, and resumes it only after the assignment and Job limits are
    verified. Every descendant start is delivered by
    `JOB_OBJECT_MSG_NEW_PROCESS`; every owned `link.exe` message must be opened,
    bound by PID plus creation file time, proven to be the pinned linker,
    working-set capped at 4,096 MiB with setter flags exactly `0x4`, and read
    back with the same minimum/maximum plus getter flags exactly `0x6`. Any
    unresolvable start, duplicate/reused PID, memory-limit message, unmatched
    start/exit, uncapped linker, missing root notification, missing
    `JOB_OBJECT_MSG_ACTIVE_PROCESS_ZERO`, accounting mismatch, non-empty final
    Job PID list, or surviving process fails closed. The nested Job's commit
    limits are set and queried before the suspended MSBuild root is assigned or
    resumed, and therefore provide a kernel-enforced safety boundary before
    its first instruction. Completion messages drive an additional same-handle
    4,096 MiB working-set cap on every observed linker; because ordinary Job
    completion messages are not guaranteed, a successful run additionally
    requires unique start and terminal counts to equal the Job's
    `TotalProcesses`.
    Immediately before the wrapper and again immediately before suspended
    `MSBuild.exe` creation, the exact build/test concurrency set must be empty,
    available physical memory must be at least 7,424 MiB, and free virtual
    memory at least 5,120 MiB. Both empty snapshots are bound into the final
    receipt. The guard's independent 2,048 MiB floor remains unchanged. The
    Job limits cap commit, while the linker limit caps resident working set;
    neither is a benchmark metric. The setter and getter flags are different
    API contracts: never compare the getter state to the setter input mask.
20. Attempt 005 has no fallback route. Do not substitute a full-target RED,
    parallel build, CMake `--build`, `lld-link`, disabled ICF/REF, incremental
    linking, altered debug/PDB semantics, a different linker, a higher cap, or
    any unguarded command. Do not set `BuildProjectReferences=false` on either
    heavy GREEN build: the reusable cache does not contain every dependency
    output required by those generated projects. If an exact gate cannot be
    met, preserve attempt 005 as incomplete and stop for a new reviewed
    addendum.
21. The no-admin nested-Job mechanism was dynamically proven inside the exact
    accepted outer guard on Windows build 26200 by ignored disposable probe
    `R:\.superpowers\sdd\nested-job-iocp-probe-v4.ps1`, exactly 18,533 bytes
    with SHA-256
    `6cb25741cacfa9bfa9b7ad9fa6d812fe91d5386b69e2389341e0bf1b9b4960bc`.
    The proof set/query-read back class-9 flags `0x2300`, 6 GiB process and
    7 GiB Job commit limits, assigned a suspended child to the nested Job
    before resume, observed two NEW/two EXIT/one ACTIVE_ZERO messages,
    reconciled accounting total 2/active 0 and an empty PID list, and returned
    through the accepted guard with zero survivors. This plan embeds the
    reviewed native definitions; execution never imports or depends on that
    ignored probe file.
22. The launcher creates a fresh named controller Job with class-9 flags
    exactly `0x2000` (`JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`) and every other
    limit zero. It rejects an existing name, performs the exact
    `InitializeProcThreadAttributeList` size probe (`ERROR_INSUFFICIENT_BUFFER`
    122), and creates the suspended controller with
    `PROC_THREAD_ATTRIBUTE_JOB_LIST=0x0002000D` and flags `0x08080004`. Thus the
    controller is born in the Job atomically; there is no crash window with an
    unassigned suspended process. Before resume the launcher proves exact Job
    membership and a one-PID class-3 list. `ResumeThread` must return one; only
    then does `DuplicateHandle(..., bInheritHandle=FALSE,
    DUPLICATE_SAME_ACCESS)` place a non-inheritable Job handle in the
    controller for its lifetime. The launcher closes its own handle and then
    atomically publishes the exact Job-ready handshake that confirms that
    completed close. The controller waits for and validates that handshake
    before any Git, child-process, build, test, or heavy action.
23. A RED- or GREEN-phase coordination deadline opens that exact named Job,
    revalidates class-9 flags and exact controller membership, then immediately
    calls `TerminateJobObject(125)`. It never delays termination on a
    fixed-capacity PID-list query, never calls `TerminateProcess`, and never
    relies on a descendant snapshot. The drain-side class-3 query grows
    dynamically from 16 through a hard 4,096-entry bound. Success requires the
    exact controller handle signalled, assigned/listed PID counts zero, class-1
    active count zero, and 60 consecutive 500 ms samples with no global heavy
    process within a five-minute stability deadline; each CIM process-table
    sample also has a ten-second operation timeout. On deadline expiry it
    reports the observed blockers, closes its Job handle through `finally`, and
    fails. The coordinator atomically publishes a create-new timeout-cleanup
    receipt only after successful cleanup and fails, permanently preserving
    attempt 005 for a reviewed next attempt.

## Files in the derived OpenVINO core

- Modify: `src/plugins/intel_cpu/src/cpu_memory.h`
- Modify: `src/plugins/intel_cpu/src/cpu_memory.cpp`
- Create: `src/plugins/intel_cpu/src/utils/state_allocations_dump.hpp`
- Create: `src/plugins/intel_cpu/src/utils/state_allocations_dump.cpp`
- Create: `src/plugins/intel_cpu/tests/unit/state_allocations_dump_test.cpp`

No parent-repository implementation file and no CMake file belongs to this
task commit.

## Step 1: Freeze immutable source, patch blobs, and guard provenance

Before execution, materialize all four reviewed control scripts: the
coordinating launcher, the controller, the link-cap supervisor, and the GREEN
signal. Materialize the executable PowerShell fences for Steps 1, 3, and 7-11
plus the explicit RED/GREEN wait gate below into one ignored controller script
under `R:\.superpowers\sdd`. Materialize the exact link-cap supervisor fence in
Step 3 separately as
`R:\.superpowers\sdd\task02-attempt005-link-cap-supervisor.ps1`, using UTF-8
without BOM and LF line endings; its expected byte count and SHA-256 are
hard-coded beside that fence. The direct prelaunch gate below starts the
launcher only after proving its exact non-reparse path, byte count, and SHA-256;
the launcher then proves the other three execution copies before it starts the
controller. The launcher must use native `CreateProcessW` with
`STARTUPINFOEX`, `PROC_THREAD_ATTRIBUTE_JOB_LIST`, `CREATE_SUSPENDED`,
`CREATE_NO_WINDOW`, and `EXTENDED_STARTUPINFO_PRESENT`; `Start-Process` is not
authorized. The exact command line remains
`-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File
C:\Users\Student\IBM-Granite-Edge-AI-Optimisation-using-TurboQuant\.worktrees\openvino-turboquant-recovery\.superpowers\sdd\task02-attempt005-controller.ps1`.
Do not paste the fences into an existing runspace and do not include the future
Task 7 snippet. The hidden controller runs Steps 1-3, publishes a cryptographically
correlated RED-ready receipt only after both guarded RED triples are complete
and have zero survivors, and waits with no heavy child alive. The coordinating
shell applies the exact Step 4-6 C++ edits, publishes the matching GREEN-ready
receipt described below, then waits for the same controller process to finish
Steps 7-11. If either process exits, the machine restarts, the wait times out,
or attempt 005 contains fewer than 24 final files, preserve attempt 005 and
every phase receipt as incomplete and stop. This revision authorizes no reset,
fallback, or retry. A next-numbered attempt requires a separately reviewed recovery
addendum that first hash-binds the preserved evidence and all five
derived-source paths, then uses explicit `apply_patch` changes to restore the
exact fixture-only boundary without `git checkout`, recursive deletion, or
reuse of any attempt-005 label. Never extend, delete, overwrite, or silently
resume attempts 004 or 005.

The exact controller concatenation is the PowerShell fences for Steps 1, 3,
the RED/GREEN wait, and Steps 7-11—in document order, excluding the direct
prelaunch gates, launcher, supervisor, GREEN signal, C++ bodies, and future
Task 7 fence. With LF newlines and UTF-8 without BOM it is exactly
`158,223` bytes with SHA-256
`9dcb8798ebc65357f8f049d216ba595b2e590378eda67a5877b0580b6004674b`.

This keeps one fresh no-profile controller state across RED and GREEN without
running GREEN against an absent API. It limits the execution-policy change to
that new process. Run these direct inspection commands from the exact physical
parent recovery worktree before editing; use `R:` only for the separately
validated evidence/materialization paths:

Materialize the next coordinating fence as
`R:\.superpowers\sdd\task02-attempt005-launcher.ps1`, also with LF newlines
and UTF-8 without BOM. It must be exactly `46,968` bytes with SHA-256
`e05a8dd26e2f38c9f2e2a5ebb6fa3a5441834fbd6dfbc8282b111f2a1be0fbf8`.
The launcher fence deliberately contains no expected byte-count or SHA-256
literal for itself, the controller, GREEN signal, or link-cap supervisor. Its
eight mandatory identity parameters come only from the separately materialized
direct prelaunch gate, which is excluded from launcher bytes. The launcher
validates its own execution copy from the authenticated launcher parameters
before inspecting or creating attempt state, then validates all three
downstream scripts from their corresponding parameters.
The coordinating shell first proves that attempt 005 and every phase, Job, and
timeout receipt does not already exist. It atomically creates the already
materialized controller suspended inside the fresh named kill-on-close Job,
verifies membership and limits, resumes it, duplicates a non-inheritable Job
handle into that controller, closes its launcher handle, and then publishes
the exact Job-ready handshake confirming that close. It waits for RED-ready
with a four-hour
deadline and validates the exact receipt before allowing any Step 4-6 edit.
This block itself is not copied into the hidden controller:

```powershell
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [long]$AuthenticatedLauncherBytes,
  [Parameter(Mandatory = $true)]
  [string]$AuthenticatedLauncherSha256,
  [Parameter(Mandatory = $true)]
  [long]$ExpectedControllerScriptBytes,
  [Parameter(Mandatory = $true)]
  [string]$ExpectedControllerScriptSha256,
  [Parameter(Mandatory = $true)]
  [long]$ExpectedGreenSignalScriptBytes,
  [Parameter(Mandatory = $true)]
  [string]$ExpectedGreenSignalScriptSha256,
  [Parameter(Mandatory = $true)]
  [long]$ExpectedLinkCapSupervisorScriptBytes,
  [Parameter(Mandatory = $true)]
  [string]$ExpectedLinkCapSupervisorScriptSha256
)

$ErrorActionPreference = "Stop"
$root =
  "C:\Users\Student\IBM-Granite-Edge-AI-Optimisation-using-TurboQuant\.worktrees\openvino-turboquant-recovery"
$controlDirectory = [IO.Path]::Combine($root, ".superpowers\sdd")
$launcherScript =
  "C:\Users\Student\IBM-Granite-Edge-AI-Optimisation-using-TurboQuant\.worktrees\openvino-turboquant-recovery\.superpowers\sdd\task02-attempt005-launcher.ps1"
$controllerScript =
  "C:\Users\Student\IBM-Granite-Edge-AI-Optimisation-using-TurboQuant\.worktrees\openvino-turboquant-recovery\.superpowers\sdd\task02-attempt005-controller.ps1"
$greenSignalScript =
  "C:\Users\Student\IBM-Granite-Edge-AI-Optimisation-using-TurboQuant\.worktrees\openvino-turboquant-recovery\.superpowers\sdd\task02-attempt005-green-signal.ps1"
$linkCapSupervisorScript =
  "C:\Users\Student\IBM-Granite-Edge-AI-Optimisation-using-TurboQuant\.worktrees\openvino-turboquant-recovery\.superpowers\sdd\task02-attempt005-link-cap-supervisor.ps1"
$controllerExecutable =
  "C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe"
foreach ($identityValue in @(
    [PSCustomObject]@{
      Name = "launcher"
      Bytes = $AuthenticatedLauncherBytes
      Sha256 = $AuthenticatedLauncherSha256
    },
    [PSCustomObject]@{
      Name = "controller"
      Bytes = $ExpectedControllerScriptBytes
      Sha256 = $ExpectedControllerScriptSha256
    },
    [PSCustomObject]@{
      Name = "GREEN signal"
      Bytes = $ExpectedGreenSignalScriptBytes
      Sha256 = $ExpectedGreenSignalScriptSha256
    },
    [PSCustomObject]@{
      Name = "link-cap supervisor"
      Bytes = $ExpectedLinkCapSupervisorScriptBytes
      Sha256 = $ExpectedLinkCapSupervisorScriptSha256
    }
  )) {
  if ([int64]$identityValue.Bytes -le 0 -or
      [string]$identityValue.Sha256 -cnotmatch "^[0-9a-f]{64}$") {
    throw "Task 2 $($identityValue.Name) authenticated identity is invalid"
  }
}
$launcherScriptItem = Get-Item -LiteralPath $launcherScript -Force
$launcherScriptSha256 = (
  Get-FileHash -LiteralPath $launcherScript -Algorithm SHA256
).Hash.ToLowerInvariant()
if ([string]::IsNullOrWhiteSpace([string]$PSCommandPath) -or
    -not [IO.Path]::GetFullPath($PSCommandPath).Equals(
      $launcherScript,
      [StringComparison]::OrdinalIgnoreCase
    ) -or
    -not [IO.Path]::GetFullPath($launcherScriptItem.FullName).Equals(
      $launcherScript,
      [StringComparison]::OrdinalIgnoreCase
    ) -or
    ($launcherScriptItem.Attributes -band
      [IO.FileAttributes]::ReparsePoint) -ne 0 -or
    [int64]$launcherScriptItem.Length -ne $AuthenticatedLauncherBytes -or
    $launcherScriptSha256 -cne $AuthenticatedLauncherSha256) {
  throw "Task 2 launcher authenticated self-identity failed"
}
$authenticatedLauncherIdentity = [ordered]@{
  path = $launcherScript
  bytes = [int64]$launcherScriptItem.Length
  sha256 = $launcherScriptSha256
  reparse = $false
  authentication =
    "direct-prelaunch-parameters-plus-launcher-self-readback"
}
$attempt005Path = [IO.Path]::Combine(
  $root,
  "experiments\raw-results\openvino-turboquant\2026-07-28\guards\task02-attempt-005"
)
$redReadyPath =
  [IO.Path]::Combine($controlDirectory, "task02-attempt005-red-ready.json")
$greenReadyPath =
  [IO.Path]::Combine($controlDirectory, "task02-attempt005-green-ready.json")
$boundaryPath =
  [IO.Path]::Combine($controlDirectory, "task02-attempt005-boundary.json")
$finalReceiptPath =
  [IO.Path]::Combine($controlDirectory, "task02-attempt005-receipt.json")
$controllerJobReadyPath =
  [IO.Path]::Combine(
    $controlDirectory, "task02-attempt005-controller-job-ready.json"
  )
$timeoutCleanupPath =
  [IO.Path]::Combine(
    $controlDirectory, "task02-attempt005-timeout-cleanup.json"
  )
$controllerJobName = "Local\OpenVINO-Task02-Attempt005-Controller"
$previewPatchPath =
  [IO.Path]::Combine($controlDirectory, "task02-attempt005-core-preview.patch")
$replayScratchPath =
  [IO.Path]::Combine($controlDirectory, "task02-attempt005-patch-replay")
$controlTempNamePattern =
  '^(?:task02-attempt005-(?:boundary|receipt)\.json\.[0-9a-f]{32}\.tmp|' +
  '\.task02-attempt005-(?:controller-job-ready|timeout-cleanup)\.[0-9a-f]{32}\.tmp|' +
  '\.task02-attempt005-(?:red|green)-ready\.json\.[0-9a-f]{32}\.tmp|' +
  '\.task02-green-ready\.[0-9a-f]{32}\.tmp)$'
$controlTempResidue = @(
  [IO.Directory]::EnumerateFiles(
    $controlDirectory, "*", [IO.SearchOption]::TopDirectoryOnly
  ) | Where-Object {
    [IO.Path]::GetFileName($_) -cmatch $controlTempNamePattern
  }
)
if ([IO.Directory]::Exists($attempt005Path) -or
    [IO.File]::Exists($redReadyPath) -or
    [IO.File]::Exists($greenReadyPath) -or
    [IO.File]::Exists($boundaryPath) -or
    [IO.File]::Exists($finalReceiptPath) -or
    [IO.File]::Exists($controllerJobReadyPath) -or
    [IO.File]::Exists($timeoutCleanupPath) -or
    [IO.File]::Exists($previewPatchPath) -or
    [IO.Directory]::Exists($replayScratchPath) -or
    $controlTempResidue.Count -ne 0) {
  throw "Task 2 attempt, phase, final, or replay evidence exists; preserve it and stop"
}
if (-not [IO.Path]::GetFullPath((Get-Location).ProviderPath).Equals(
    [IO.Path]::GetFullPath($root),
    [StringComparison]::OrdinalIgnoreCase
  )) {
  throw "Task 2 launcher must run from the exact recovery worktree"
}
foreach ($controlScript in @(
    [PSCustomObject]@{
      Name = "controller"
      Path = $controllerScript
      Bytes = $expectedControllerScriptBytes
      Sha256 = $expectedControllerScriptSha256
    },
    [PSCustomObject]@{
      Name = "GREEN signal"
      Path = $greenSignalScript
      Bytes = $expectedGreenSignalScriptBytes
      Sha256 = $expectedGreenSignalScriptSha256
    },
    [PSCustomObject]@{
      Name = "link-cap supervisor"
      Path = $linkCapSupervisorScript
      Bytes = $expectedLinkCapSupervisorScriptBytes
      Sha256 = $expectedLinkCapSupervisorScriptSha256
    }
  )) {
  $controlScriptItem = Get-Item -LiteralPath $controlScript.Path -Force
  if (($controlScriptItem.Attributes -band
        [IO.FileAttributes]::ReparsePoint) -ne 0 -or
      $controlScriptItem.Length -ne [int64]$controlScript.Bytes -or
      (Get-FileHash -LiteralPath $controlScript.Path -Algorithm SHA256).
        Hash.ToLowerInvariant() -cne [string]$controlScript.Sha256) {
    throw "Task 2 $($controlScript.Name) execution copy is not exact"
  }
}
function Get-Task2StringSha256 {
  param([Parameter(Mandatory = $true)][string]$Value)
  $bytes = [Text.UTF8Encoding]::new($false, $true).GetBytes($Value)
  $sha = [Security.Cryptography.SHA256]::Create()
  try {
    $digest = $sha.ComputeHash($bytes)
  } finally {
    $sha.Dispose()
  }
  [BitConverter]::ToString($digest).Replace("-", "").ToLowerInvariant()
}
if (-not ("Task02ControllerJobNative" -as [type])) {
  Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;

public static class Task02ControllerJobNative {
    [StructLayout(LayoutKind.Sequential)]
    public struct IO_COUNTERS {
        public UInt64 ReadOperationCount;
        public UInt64 WriteOperationCount;
        public UInt64 OtherOperationCount;
        public UInt64 ReadTransferCount;
        public UInt64 WriteTransferCount;
        public UInt64 OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct JOBOBJECT_BASIC_LIMIT_INFORMATION {
        public Int64 PerProcessUserTimeLimit;
        public Int64 PerJobUserTimeLimit;
        public UInt32 LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public UInt32 ActiveProcessLimit;
        public UIntPtr Affinity;
        public UInt32 PriorityClass;
        public UInt32 SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct JOBOBJECT_BASIC_ACCOUNTING_INFORMATION {
        public Int64 TotalUserTime;
        public Int64 TotalKernelTime;
        public Int64 ThisPeriodTotalUserTime;
        public Int64 ThisPeriodTotalKernelTime;
        public UInt32 TotalPageFaultCount;
        public UInt32 TotalProcesses;
        public UInt32 ActiveProcesses;
        public UInt32 TotalTerminatedProcesses;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct STARTUPINFO {
        public UInt32 cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public UInt32 dwX;
        public UInt32 dwY;
        public UInt32 dwXSize;
        public UInt32 dwYSize;
        public UInt32 dwXCountChars;
        public UInt32 dwYCountChars;
        public UInt32 dwFillAttribute;
        public UInt32 dwFlags;
        public UInt16 wShowWindow;
        public UInt16 cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct STARTUPINFOEX {
        public STARTUPINFO StartupInfo;
        public IntPtr lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_INFORMATION {
        public IntPtr hProcess;
        public IntPtr hThread;
        public UInt32 dwProcessId;
        public UInt32 dwThreadId;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr CreateJobObjectW(
        IntPtr jobAttributes,
        string name);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr OpenJobObjectW(
        UInt32 desiredAccess,
        bool inheritHandle,
        string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetInformationJobObject(
        IntPtr job,
        Int32 informationClass,
        IntPtr information,
        UInt32 informationLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool QueryInformationJobObject(
        IntPtr job,
        Int32 informationClass,
        IntPtr information,
        UInt32 informationLength,
        out UInt32 returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool InitializeProcThreadAttributeList(
        IntPtr attributeList,
        Int32 attributeCount,
        UInt32 flags,
        ref IntPtr size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UpdateProcThreadAttribute(
        IntPtr attributeList,
        UInt32 flags,
        IntPtr attribute,
        IntPtr value,
        IntPtr size,
        IntPtr previousValue,
        IntPtr returnSize);

    [DllImport("kernel32.dll")]
    public static extern void DeleteProcThreadAttributeList(
        IntPtr attributeList);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CreateProcessW(
        string applicationName,
        StringBuilder commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        bool inheritHandles,
        UInt32 creationFlags,
        IntPtr environment,
        string currentDirectory,
        ref STARTUPINFOEX startupInfo,
        out PROCESS_INFORMATION processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsProcessInJob(
        IntPtr process,
        IntPtr job,
        out bool result);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetProcessTimes(
        IntPtr process,
        out Int64 creationTime,
        out Int64 exitTime,
        out Int64 kernelTime,
        out Int64 userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern UInt32 ResumeThread(IntPtr thread);

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DuplicateHandle(
        IntPtr sourceProcess,
        IntPtr sourceHandle,
        IntPtr targetProcess,
        out IntPtr targetHandle,
        UInt32 desiredAccess,
        bool inheritHandle,
        UInt32 options);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool TerminateJobObject(
        IntPtr job,
        UInt32 exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr handle);
}
'@
}
if ([IntPtr]::Size -ne 8 -or
    [Runtime.InteropServices.Marshal]::SizeOf(
      [type][Task02ControllerJobNative+JOBOBJECT_EXTENDED_LIMIT_INFORMATION]
    ) -ne 144 -or
    [Runtime.InteropServices.Marshal]::SizeOf(
      [type][Task02ControllerJobNative+JOBOBJECT_BASIC_ACCOUNTING_INFORMATION]
    ) -ne 48 -or
    [Runtime.InteropServices.Marshal]::SizeOf(
      [type][Task02ControllerJobNative+STARTUPINFOEX]
    ) -ne 112) {
  throw "Task 2 controller-Job native layout is not exact x64"
}
if (@(Get-Process -Name csc -ErrorAction SilentlyContinue).Count -ne 0) {
  throw "Controller-Job native bootstrap left an unowned csc.exe"
}

$controllerJobLimitFlags = [uint32]0x00002000
$controllerCreateProcessFlags = [uint32]0x08080004
$controllerJobListAttribute = [IntPtr]::new(0x0002000D)
$duplicateSameAccess = [uint32]0x00000002
$jobObjectBasicAccountingInformation = 1
$jobObjectBasicProcessIdList = 3
$jobObjectExtendedLimitInformation = 9

function Get-Task2ControllerJobLimits {
  param([Parameter(Mandatory = $true)][IntPtr]$JobHandle)
  $type =
    [type][Task02ControllerJobNative+JOBOBJECT_EXTENDED_LIMIT_INFORMATION]
  $size = [Runtime.InteropServices.Marshal]::SizeOf([type]$type)
  $buffer = [Runtime.InteropServices.Marshal]::AllocHGlobal($size)
  try {
    $returned = [uint32]0
    if (-not [Task02ControllerJobNative]::QueryInformationJobObject(
        $JobHandle,
        $jobObjectExtendedLimitInformation,
        $buffer,
        [uint32]$size,
        [ref]$returned
      ) -or $returned -ne $size) {
      $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
      throw "Controller Job class-9 query failed: $errorCode/$returned"
    }
    [Runtime.InteropServices.Marshal]::PtrToStructure(
      $buffer, [type]$type
    )
  } finally {
    [Runtime.InteropServices.Marshal]::FreeHGlobal($buffer)
  }
}

function Assert-Task2ControllerJobLimits {
  param([Parameter(Mandatory = $true)][IntPtr]$JobHandle)
  $limits = Get-Task2ControllerJobLimits -JobHandle $JobHandle
  if ([uint32]$limits.BasicLimitInformation.LimitFlags -ne
        $controllerJobLimitFlags -or
      $limits.BasicLimitInformation.PerProcessUserTimeLimit -ne 0 -or
      $limits.BasicLimitInformation.PerJobUserTimeLimit -ne 0 -or
      $limits.BasicLimitInformation.MinimumWorkingSetSize.ToUInt64() -ne 0 -or
      $limits.BasicLimitInformation.MaximumWorkingSetSize.ToUInt64() -ne 0 -or
      $limits.BasicLimitInformation.ActiveProcessLimit -ne 0 -or
      $limits.BasicLimitInformation.Affinity.ToUInt64() -ne 0 -or
      $limits.BasicLimitInformation.PriorityClass -ne 32 -or
      $limits.BasicLimitInformation.SchedulingClass -ne 5 -or
      $limits.ProcessMemoryLimit.ToUInt64() -ne 0 -or
      $limits.JobMemoryLimit.ToUInt64() -ne 0) {
    throw "Controller Job class-9 limit readback was not exact"
  }
}

function Get-Task2ControllerJobPidList {
  param([Parameter(Mandatory = $true)][IntPtr]$JobHandle)
  $capacity = 16
  while ($capacity -le 4096) {
    $size = 8 + ($capacity * [IntPtr]::Size)
    $buffer = [Runtime.InteropServices.Marshal]::AllocHGlobal($size)
    try {
      [Runtime.InteropServices.Marshal]::WriteInt64($buffer, 0, [int64]0)
      $returned = [uint32]0
      $ok = [Task02ControllerJobNative]::QueryInformationJobObject(
        $JobHandle,
        $jobObjectBasicProcessIdList,
        $buffer,
        [uint32]$size,
        [ref]$returned
      )
      $errorCode =
        [Runtime.InteropServices.Marshal]::GetLastWin32Error()
      if (-not $ok) {
        if ($errorCode -ne 234) {
          throw "Controller Job PID-list query failed: $errorCode/$returned"
        }
        $required = [int64]($capacity * 2)
        if ($required -le $capacity -or $required -gt 4096) {
          throw "Controller Job PID-list growth exceeded 4096"
        }
        $capacity = [int]$required
        continue
      }
      if ($returned -lt 8) {
        throw "Controller Job PID-list query failed: $errorCode/$returned"
      }
      $assigned = [Runtime.InteropServices.Marshal]::ReadInt32($buffer, 0)
      $listed = [Runtime.InteropServices.Marshal]::ReadInt32($buffer, 4)
      if ($assigned -lt 0 -or $listed -lt 0 -or $assigned -lt $listed) {
        throw "Controller Job PID-list counts are invalid"
      }
      if ($assigned -gt $listed -or $assigned -gt $capacity -or
          $listed -gt $capacity) {
        $required = [Math]::Max(
          [int64]($capacity * 2),
          [int64]$assigned
        )
        if ($required -le $capacity -or $required -gt 4096) {
          throw "Controller Job PID-list growth exceeded 4096"
        }
        $capacity = [int]$required
        continue
      }
      if ($returned -lt (8 + ($listed * [IntPtr]::Size))) {
        throw "Controller Job PID-list success returned a truncated buffer"
      }
      $processIds = @()
      for ($index = 0; $index -lt $listed; ++$index) {
        $processIds += [int64][Runtime.InteropServices.Marshal]::ReadInt64(
          $buffer,
          8 + ($index * [IntPtr]::Size)
        )
      }
      return [PSCustomObject][ordered]@{
        returned_bytes = [uint32]$returned
        capacity = [int64]$capacity
        assigned_process_count = [int64]$assigned
        listed_process_count = [int64]$listed
        process_ids = @($processIds)
      }
    } finally {
      [Runtime.InteropServices.Marshal]::FreeHGlobal($buffer)
    }
  }
  throw "Controller Job PID-list query exhausted its bounded growth"
}

function Get-Task2ControllerJobAccounting {
  param([Parameter(Mandatory = $true)][IntPtr]$JobHandle)
  $type =
    [type][Task02ControllerJobNative+JOBOBJECT_BASIC_ACCOUNTING_INFORMATION]
  $size = [Runtime.InteropServices.Marshal]::SizeOf([type]$type)
  $buffer = [Runtime.InteropServices.Marshal]::AllocHGlobal($size)
  try {
    $returned = [uint32]0
    if (-not [Task02ControllerJobNative]::QueryInformationJobObject(
        $JobHandle,
        $jobObjectBasicAccountingInformation,
        $buffer,
        [uint32]$size,
        [ref]$returned
      ) -or $returned -ne $size) {
      $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
      throw "Controller Job accounting query failed: $errorCode/$returned"
    }
    [Runtime.InteropServices.Marshal]::PtrToStructure(
      $buffer, [type]$type
    )
  } finally {
    [Runtime.InteropServices.Marshal]::FreeHGlobal($buffer)
  }
}

function Write-Task2ControlJsonCreateNew {
  param(
    [Parameter(Mandatory = $true)][object]$Value,
    [Parameter(Mandatory = $true)][string]$Path,
    [Parameter(Mandatory = $true)][string]$TempStem
  )
  $serializer = $ExecutionContext.InvokeCommand.GetCommand(
    "Microsoft.PowerShell.Utility\ConvertTo-Json",
    [Management.Automation.CommandTypes]::Cmdlet
  )
  if ($null -eq $serializer -or
      $serializer -isnot [Management.Automation.CmdletInfo] -or
      $serializer.ModuleName -cne "Microsoft.PowerShell.Utility" -or
      $serializer.ImplementingType.FullName -cne
        "Microsoft.PowerShell.Commands.ConvertToJsonCommand") {
    throw "Controller Job JSON serializer is not trusted"
  }
  $bytes = [Text.UTF8Encoding]::new($false, $true).GetBytes(
    ($Value | & $serializer -Depth 12 -Compress) + "`n"
  )
  $temp = [IO.Path]::Combine(
    $controlDirectory,
    "." + $TempStem + "." + [Guid]::NewGuid().ToString("N") + ".tmp"
  )
  $stream = [IO.FileStream]::new(
    $temp,
    [IO.FileMode]::CreateNew,
    [IO.FileAccess]::Write,
    [IO.FileShare]::None
  )
  try {
    $stream.Write($bytes, 0, $bytes.Length)
    $stream.Flush($true)
  } finally {
    $stream.Dispose()
  }
  try {
    [IO.File]::Move($temp, $Path)
  } catch {
    if ([IO.File]::Exists($temp)) { [IO.File]::Delete($temp) }
    throw
  }
  if (-not [Collections.StructuralComparisons]::StructuralEqualityComparer.
      Equals($bytes, [IO.File]::ReadAllBytes($Path))) {
    throw "Controller Job control receipt differs after publication"
  }
}

function Wait-Task2ControllerJobEmpty {
  param(
    [Parameter(Mandatory = $true)][IntPtr]$JobHandle,
    [Parameter(Mandatory = $true)][Diagnostics.Process]$Process
  )
  if (-not $Process.WaitForExit(30000)) {
    throw "Terminated controller root handle did not become signaled"
  }
  $emptyDeadline = [DateTime]::UtcNow.AddSeconds(30)
  do {
    $pidList = Get-Task2ControllerJobPidList -JobHandle $JobHandle
    $accounting = Get-Task2ControllerJobAccounting -JobHandle $JobHandle
    if ($pidList.assigned_process_count -eq 0 -and
        $pidList.listed_process_count -eq 0 -and
        @($pidList.process_ids).Count -eq 0 -and
        $accounting.ActiveProcesses -eq 0) {
      break
    }
    if ([DateTime]::UtcNow -ge $emptyDeadline) {
      throw "Controller Job did not become empty after termination"
    }
    [Threading.Thread]::Sleep(100)
  } while ($true)
  [PSCustomObject][ordered]@{
    pid_list_returned_bytes = [uint32]$pidList.returned_bytes
    pid_list_capacity = [int64]$pidList.capacity
    assigned_process_count = [int64]$pidList.assigned_process_count
    listed_process_count = [int64]$pidList.listed_process_count
    accounting_total_processes = [uint32]$accounting.TotalProcesses
    accounting_active_processes = [uint32]$accounting.ActiveProcesses
    accounting_returned_bytes = 48
  }
}

function Wait-Task2StableHeavyEmpty {
  $heavyNames = [string[]]@(
    "MSBuild.exe",
    "link.exe",
    "cl.exe",
    "cmake.exe",
    "ctest.exe",
    "ninja.exe",
    "ov_cpu_unit_tests.exe"
  )
  $stableEmptySamples = 0
  $stableEmptyDeadline = [DateTime]::UtcNow.AddMinutes(5)
  while ($stableEmptySamples -lt 60) {
    $heavyRows = @(
      Get-CimInstance -ClassName Win32_Process -Property @(
        "ProcessId", "CreationDate", "Name"
      ) -OperationTimeoutSec 10 -ErrorAction Stop |
        Where-Object { $heavyNames -ccontains [string]$_.Name }
    )
    if ($heavyRows.Count -eq 0) {
      $stableEmptySamples++
    } else {
      $stableEmptySamples = 0
    }
    if ($stableEmptySamples -lt 60 -and
        [DateTime]::UtcNow -ge $stableEmptyDeadline) {
      $observedBlockers = @(
        $heavyRows | Sort-Object Name, ProcessId | ForEach-Object {
          "{0}:pid={1}:created={2}" -f
            [string]$_.Name,
            [int64]$_.ProcessId,
            [string]$_.CreationDate
        }
      )
      if ($observedBlockers.Count -eq 0) {
        $observedBlockers = @("<none-in-final-sample>")
      }
      throw (
        "Global heavy-process stability deadline expired at " +
        "$stableEmptySamples/60 empty samples; observed blockers: " +
        ($observedBlockers -join ", ")
      )
    }
    if ($stableEmptySamples -lt 60) {
      [Threading.Thread]::Sleep(500)
    }
  }
  $stableEmptySamples
}

function Stop-Task2ControllerJob {
  param(
    [Parameter(Mandatory = $true)][IntPtr]$JobHandle,
    [Parameter(Mandatory = $true)][Diagnostics.Process]$Process,
    [Parameter(Mandatory = $true)][string]$Phase,
    [Parameter(Mandatory = $true)][bool]$WriteCleanupReceipt
  )
  Assert-Task2ControllerJobLimits -JobHandle $JobHandle
  $inJob = $false
  if (-not [Task02ControllerJobNative]::IsProcessInJob(
      $Process.Handle,
      $JobHandle,
      [ref]$inJob
    ) -or -not $inJob) {
    throw "Exact controller root is not in its timeout Job"
  }
  if (-not [Task02ControllerJobNative]::TerminateJobObject(
      $JobHandle,
      [uint32]125
    )) {
    $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
    throw "TerminateJobObject failed for controller timeout: $errorCode"
  }
  $empty = Wait-Task2ControllerJobEmpty `
    -JobHandle $JobHandle -Process $Process
  $heavyEmptySamples = Wait-Task2StableHeavyEmpty
  if ($WriteCleanupReceipt) {
    $cleanup = [ordered]@{
      schema = "openvino-cpu-observer-task02-timeout-cleanup/v1"
      phase = $Phase
      controller_job_name = $controllerJobName
      controller_job_limit_flags = [uint32]$controllerJobLimitFlags
      controller_pid = [int64]$Process.Id
      controller_creation_filetime_utc =
        [int64]$controllerCreationFileTimeUtc
      terminate_job_exit_code = 125
      root_signaled = $true
      pid_list_returned_bytes = $empty.pid_list_returned_bytes
      pid_list_capacity = $empty.pid_list_capacity
      assigned_process_count = $empty.assigned_process_count
      listed_process_count = $empty.listed_process_count
      accounting_total_processes = $empty.accounting_total_processes
      accounting_active_processes = $empty.accounting_active_processes
      accounting_returned_bytes = $empty.accounting_returned_bytes
      stable_heavy_empty_samples = [int64]$heavyEmptySamples
      sample_interval_milliseconds = 500
      terminate_process_used = $false
      verified = $true
      created_utc = [DateTime]::UtcNow.ToString("o")
    }
    Write-Task2ControlJsonCreateNew `
      -Value $cleanup `
      -Path $timeoutCleanupPath `
      -TempStem "task02-attempt005-timeout-cleanup"
  }
}
$controllerJobHandle = [IntPtr]::Zero
$attributeList = [IntPtr]::Zero
$attributeListInitialized = $false
$jobHandleValue = [IntPtr]::Zero
$processInformation =
  New-Object Task02ControllerJobNative+PROCESS_INFORMATION
$controllerProcess = $null
$controllerStarted = $false
try {
  $controllerJobHandle = [Task02ControllerJobNative]::CreateJobObjectW(
    [IntPtr]::Zero,
    $controllerJobName
  )
  $createJobError =
    [Runtime.InteropServices.Marshal]::GetLastWin32Error()
  if ($controllerJobHandle -eq [IntPtr]::Zero -or $createJobError -eq 183) {
    throw "Fresh controller Job creation failed or name already exists: $createJobError"
  }
  $limits =
    New-Object Task02ControllerJobNative+JOBOBJECT_EXTENDED_LIMIT_INFORMATION
  $basicLimits =
    New-Object Task02ControllerJobNative+JOBOBJECT_BASIC_LIMIT_INFORMATION
  $basicLimits.LimitFlags = $controllerJobLimitFlags
  $limits.BasicLimitInformation = $basicLimits
  $limitSize = [Runtime.InteropServices.Marshal]::SizeOf($limits)
  $limitBuffer =
    [Runtime.InteropServices.Marshal]::AllocHGlobal($limitSize)
  try {
    [Runtime.InteropServices.Marshal]::StructureToPtr(
      $limits,
      $limitBuffer,
      $false
    )
    if (-not [Task02ControllerJobNative]::SetInformationJobObject(
        $controllerJobHandle,
        $jobObjectExtendedLimitInformation,
        $limitBuffer,
        [uint32]$limitSize
      )) {
      $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
      throw "Controller Job class-9 set failed: $errorCode"
    }
  } finally {
    [Runtime.InteropServices.Marshal]::FreeHGlobal($limitBuffer)
  }
  Assert-Task2ControllerJobLimits -JobHandle $controllerJobHandle

  $attributeListSize = [IntPtr]::Zero
  $attributeProbe =
    [Task02ControllerJobNative]::InitializeProcThreadAttributeList(
      [IntPtr]::Zero,
      1,
      [uint32]0,
      [ref]$attributeListSize
    )
  $attributeProbeError =
    [Runtime.InteropServices.Marshal]::GetLastWin32Error()
  if ($attributeProbe -or $attributeProbeError -ne 122 -or
      $attributeListSize -eq [IntPtr]::Zero) {
    throw (
      "Controller STARTUPINFOEX size probe was not exact: {0}/{1}/{2}" -f
        $attributeProbe, $attributeProbeError, $attributeListSize
    )
  }
  $attributeList =
    [Runtime.InteropServices.Marshal]::AllocHGlobal($attributeListSize)
  if (-not [Task02ControllerJobNative]::InitializeProcThreadAttributeList(
      $attributeList,
      1,
      [uint32]0,
      [ref]$attributeListSize
    )) {
    $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
    throw "Controller STARTUPINFOEX list initialization failed: $errorCode"
  }
  $attributeListInitialized = $true
  $jobHandleValue =
    [Runtime.InteropServices.Marshal]::AllocHGlobal([IntPtr]::Size)
  [Runtime.InteropServices.Marshal]::WriteInt64(
    $jobHandleValue,
    $controllerJobHandle.ToInt64()
  )
  if (-not [Task02ControllerJobNative]::UpdateProcThreadAttribute(
      $attributeList,
      [uint32]0,
      $controllerJobListAttribute,
      $jobHandleValue,
      [IntPtr]::new([IntPtr]::Size),
      [IntPtr]::Zero,
      [IntPtr]::Zero
    )) {
    $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
    throw "Controller JOB_LIST attribute update failed: $errorCode"
  }
  $startupInfo = New-Object Task02ControllerJobNative+STARTUPINFOEX
  $startupInfoBase = New-Object Task02ControllerJobNative+STARTUPINFO
  $startupInfoBase.cb = [uint32](
    [Runtime.InteropServices.Marshal]::SizeOf($startupInfo)
  )
  $startupInfo.StartupInfo = $startupInfoBase
  $startupInfo.lpAttributeList = $attributeList
  $nativeControllerCommandLine =
    '"' + $controllerExecutable +
    '" -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' +
    $controllerScript + '"'
  $commandLineBuilder =
    [Text.StringBuilder]::new($nativeControllerCommandLine)
  if (-not [Task02ControllerJobNative]::CreateProcessW(
      $controllerExecutable,
      $commandLineBuilder,
      [IntPtr]::Zero,
      [IntPtr]::Zero,
      $false,
      $controllerCreateProcessFlags,
      [IntPtr]::Zero,
      $root,
      [ref]$startupInfo,
      [ref]$processInformation
    )) {
    $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
    throw "Atomic controller CreateProcessW failed: $errorCode"
  }
  $controllerStarted = $true
  $controllerProcess = [Diagnostics.Process]::GetProcessById(
    [int]$processInformation.dwProcessId
  )
  $controllerInJob = $false
  if (-not [Task02ControllerJobNative]::IsProcessInJob(
      $processInformation.hProcess,
      $controllerJobHandle,
      [ref]$controllerInJob
    ) -or -not $controllerInJob) {
    throw "Suspended controller was not atomically born in its Job"
  }
  $suspendedPidList =
    Get-Task2ControllerJobPidList -JobHandle $controllerJobHandle
  if ($suspendedPidList.assigned_process_count -ne 1 -or
      $suspendedPidList.listed_process_count -ne 1 -or
      @($suspendedPidList.process_ids).Count -ne 1 -or
      [int64]$suspendedPidList.process_ids[0] -ne
        [int64]$processInformation.dwProcessId) {
    throw "Suspended controller Job PID-list proof was not exact"
  }
  $resumePreviousCount =
    [Task02ControllerJobNative]::ResumeThread($processInformation.hThread)
  if ($resumePreviousCount -ne [uint32]1) {
    $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
    throw "Controller ResumeThread was not exactly 1: $resumePreviousCount/$errorCode"
  }
  $remoteJobHandle = [IntPtr]::Zero
  if (-not [Task02ControllerJobNative]::DuplicateHandle(
      [Task02ControllerJobNative]::GetCurrentProcess(),
      $controllerJobHandle,
      $processInformation.hProcess,
      [ref]$remoteJobHandle,
      [uint32]0,
      $false,
      $duplicateSameAccess
    ) -or $remoteJobHandle -eq [IntPtr]::Zero) {
    $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
    throw "Non-inheritable controller-owned Job duplication failed: $errorCode"
  }

  $controllerProcess.Refresh()
  $controllerCreationFileTimeUtc = [int64]0
  $controllerExitFileTimeUtc = [int64]0
  $controllerKernelFileTimeUtc = [int64]0
  $controllerUserFileTimeUtc = [int64]0
  if (-not [Task02ControllerJobNative]::GetProcessTimes(
      $processInformation.hProcess,
      [ref]$controllerCreationFileTimeUtc,
      [ref]$controllerExitFileTimeUtc,
      [ref]$controllerKernelFileTimeUtc,
      [ref]$controllerUserFileTimeUtc
    ) -or $controllerCreationFileTimeUtc -le 0) {
    $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
    throw "Native controller creation FILETIME query failed: $errorCode"
  }
  $controllerStartFileTimeUtc = $controllerCreationFileTimeUtc
  $controllerRows = @(
    Get-CimInstance -ClassName Win32_Process -Property @(
      "ProcessId", "ExecutablePath", "CommandLine"
    ) | Where-Object {
      [int64]$_.ProcessId -eq [int64]$controllerProcess.Id
    }
  )
  if ($controllerRows.Count -ne 1 -or
      -not [IO.Path]::GetFullPath(
        [string]$controllerRows[0].ExecutablePath
      ).Equals(
        $controllerExecutable,
        [StringComparison]::OrdinalIgnoreCase
      ) -or
      [string]::IsNullOrWhiteSpace([string]$controllerRows[0].CommandLine)) {
    throw "Task 2 controller process identity was not observable"
  }
  $controllerCommandLine = [string]$controllerRows[0].CommandLine
  $escapedControllerExecutable = [regex]::Escape($controllerExecutable)
  $escapedControllerScript = [regex]::Escape($controllerScript)
  $controllerCommandPattern =
    '^"?' + $escapedControllerExecutable + '"? -NoLogo -NoProfile ' +
    '-NonInteractive -ExecutionPolicy Bypass -File "?' +
    $escapedControllerScript + '"?$'
  if ($controllerCommandLine -cnotmatch $controllerCommandPattern) {
    throw "Task 2 controller executable or exact command line is wrong"
  }
  $controllerCommandLineSha256 =
    Get-Task2StringSha256 -Value $controllerCommandLine
  $controllerScriptSha256 = (
    Get-FileHash -LiteralPath $controllerScript -Algorithm SHA256
  ).Hash.ToLowerInvariant()
  $jobReady = [ordered]@{
    schema = "openvino-cpu-observer-task02-controller-job-ready/v2"
    job_name = $controllerJobName
    job_limit_flags = [uint32]$controllerJobLimitFlags
    create_process_flags = [uint32]$controllerCreateProcessFlags
    proc_thread_attribute_job_list = 131085
    startupinfoex_size = 112
    size_probe_error = 122
    controller_pid = [int64]$controllerProcess.Id
    controller_start_filetime_utc = [int64]$controllerStartFileTimeUtc
    controller_creation_filetime_utc =
      [int64]$controllerCreationFileTimeUtc
    controller_command_line_sha256 = $controllerCommandLineSha256
    controller_script_sha256 = $controllerScriptSha256
    suspended_pid_list_returned_bytes =
      [uint32]$suspendedPidList.returned_bytes
    assigned_at_create = $true
    job_member_before_resume = $true
    resume_previous_suspend_count = [uint32]$resumePreviousCount
    duplicate_same_access = $true
    duplicate_inheritable = $false
    controller_owned_handle = $true
    launcher_handle_closed_before_publish = $true
    launcher_identity = $authenticatedLauncherIdentity
    created_utc = [DateTime]::UtcNow.ToString("o")
  }
  if (-not [Task02ControllerJobNative]::CloseHandle($controllerJobHandle)) {
    $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
    throw "Launcher controller-Job handle close failed: $errorCode"
  }
  $controllerJobHandle = [IntPtr]::Zero
  Write-Task2ControlJsonCreateNew `
    -Value $jobReady `
    -Path $controllerJobReadyPath `
    -TempStem "task02-attempt005-controller-job-ready"
} catch {
  $launchFailure = $_
  if ($controllerStarted -and $null -ne $controllerProcess) {
    $cleanupJobHandle = $controllerJobHandle
    $cleanupHandleWasOpened = $false
    try {
      $controllerProcess.Refresh()
      if (-not $controllerProcess.HasExited) {
        if ($cleanupJobHandle -eq [IntPtr]::Zero) {
          $cleanupJobHandle = [Task02ControllerJobNative]::OpenJobObjectW(
            [uint32]0x000C,
            $false,
            $controllerJobName
          )
          if ($cleanupJobHandle -eq [IntPtr]::Zero) {
            $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
            $controllerProcess.Refresh()
            if (-not $controllerProcess.HasExited) {
              throw "Launch cleanup could not reopen controller Job: $errorCode"
            }
          } else {
            $cleanupHandleWasOpened = $true
          }
        }
        $controllerProcess.Refresh()
        if (-not $controllerProcess.HasExited) {
          Stop-Task2ControllerJob `
            -JobHandle $cleanupJobHandle `
            -Process $controllerProcess `
            -Phase "Launch" `
            -WriteCleanupReceipt $false
        }
      }
    } catch {
      throw "Controller launch failed: $launchFailure; Job cleanup failed: $_"
    } finally {
      if ($cleanupHandleWasOpened -and
          $cleanupJobHandle -ne [IntPtr]::Zero) {
        [void][Task02ControllerJobNative]::CloseHandle($cleanupJobHandle)
      }
    }
  }
  throw $launchFailure
} finally {
  if ($attributeListInitialized) {
    [Task02ControllerJobNative]::DeleteProcThreadAttributeList($attributeList)
  }
  if ($attributeList -ne [IntPtr]::Zero) {
    [Runtime.InteropServices.Marshal]::FreeHGlobal($attributeList)
  }
  if ($jobHandleValue -ne [IntPtr]::Zero) {
    [Runtime.InteropServices.Marshal]::FreeHGlobal($jobHandleValue)
  }
  if ($processInformation.hThread -ne [IntPtr]::Zero) {
    [void][Task02ControllerJobNative]::CloseHandle($processInformation.hThread)
  }
  if ($processInformation.hProcess -ne [IntPtr]::Zero) {
    [void][Task02ControllerJobNative]::CloseHandle($processInformation.hProcess)
  }
  if ($controllerJobHandle -ne [IntPtr]::Zero) {
    [void][Task02ControllerJobNative]::CloseHandle($controllerJobHandle)
  }
}
$redDeadline = [DateTime]::UtcNow.AddHours(4)
$redTimedOut = $false
while (-not [IO.File]::Exists($redReadyPath) -and
       -not $controllerProcess.HasExited) {
  if ([DateTime]::UtcNow -ge $redDeadline) {
    $redTimedOut = $true
    break
  }
  [Threading.Thread]::Sleep(500)
  $controllerProcess.Refresh()
}
if ($redTimedOut) {
  $timeoutJobHandle = [Task02ControllerJobNative]::OpenJobObjectW(
    [uint32]0x000C,
    $false,
    $controllerJobName
  )
  if ($timeoutJobHandle -eq [IntPtr]::Zero) {
    $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
    throw "Unable to open exact controller Job after RED timeout: $errorCode"
  }
  try {
    Stop-Task2ControllerJob `
      -JobHandle $timeoutJobHandle `
      -Process $controllerProcess `
      -Phase "RED" `
      -WriteCleanupReceipt $true
  } finally {
    [void][Task02ControllerJobNative]::CloseHandle($timeoutJobHandle)
  }
  throw (
    "Timed out waiting for RED-ready; the exact controller Job was terminated " +
    "and its empty PID-list/accounting/heavy-process cleanup was receipt-bound. " +
    "Preserve attempt 005 and require a reviewed next attempt."
  )
}
if ($controllerProcess.HasExited) {
  throw "Task 2 controller exited before RED-ready; preserve attempt 005"
}

$utf8Strict = [Text.UTF8Encoding]::new($false, $true)
$convertFromJson = $ExecutionContext.InvokeCommand.GetCommand(
  "Microsoft.PowerShell.Utility\ConvertFrom-Json",
  [Management.Automation.CommandTypes]::Cmdlet
)
if ($null -eq $convertFromJson -or
    $convertFromJson -isnot [Management.Automation.CmdletInfo] -or
    $convertFromJson.ModuleName -cne "Microsoft.PowerShell.Utility" -or
    $convertFromJson.ImplementingType.FullName -cne
      "Microsoft.PowerShell.Commands.ConvertFromJsonCommand") {
  throw "RED-ready validator did not bind the trusted PSHOME JSON parser"
}
$redReadyBytes = [IO.File]::ReadAllBytes($redReadyPath)
$redReady = $utf8Strict.GetString($redReadyBytes) |
  & $convertFromJson -ErrorAction Stop
$expectedRedProperties = @(
  "schema", "token", "controller_pid", "controller_start_filetime_utc",
  "controller_creation_filetime_utc", "controller_command_line_sha256",
  "controller_script_sha256", "task_plan_sha256",
  "boundary_sha256", "evidence_root", "completed_labels",
  "artifact_sha256", "record_run_ids", "created_utc"
)
$actualRedProperties = @(
  $redReady.PSObject.Properties | ForEach-Object { [string]$_.Name }
)
if ($redReady -isnot [PSCustomObject] -or
    $actualRedProperties.Count -ne $expectedRedProperties.Count) {
  throw "RED-ready receipt shape is not exact"
}
foreach ($property in $expectedRedProperties) {
  if ($actualRedProperties -cnotcontains $property) {
    throw "RED-ready receipt is missing '$property'"
  }
}
if ($redReady.schema -isnot [string] -or
    $redReady.token -isnot [string] -or
    $redReady.task_plan_sha256 -isnot [string] -or
    $redReady.boundary_sha256 -isnot [string] -or
    $redReady.evidence_root -isnot [string] -or
    $redReady.created_utc -isnot [string] -or
    ($redReady.controller_pid -isnot [int] -and
      $redReady.controller_pid -isnot [long]) -or
    ($redReady.controller_start_filetime_utc -isnot [int] -and
      $redReady.controller_start_filetime_utc -isnot [long]) -or
    ($redReady.controller_creation_filetime_utc -isnot [int] -and
      $redReady.controller_creation_filetime_utc -isnot [long]) -or
    $redReady.controller_command_line_sha256 -isnot [string] -or
    $redReady.controller_script_sha256 -isnot [string] -or
    $redReady.completed_labels -isnot [object[]] -or
    $redReady.artifact_sha256 -isnot [PSCustomObject] -or
    $redReady.record_run_ids -isnot [PSCustomObject] -or
    $redReady.schema -cne
      "openvino-cpu-observer-task02-phase-red-ready/v2" -or
    $redReady.token -cnotmatch "^[0-9a-f]{64}$" -or
    $redReady.task_plan_sha256 -cnotmatch "^[0-9a-f]{64}$" -or
    $redReady.boundary_sha256 -cnotmatch "^[0-9a-f]{64}$" -or
    [int64]$redReady.controller_pid -ne [int64]$controllerProcess.Id -or
    [int64]$redReady.controller_start_filetime_utc -ne
      [int64]$controllerStartFileTimeUtc -or
    [int64]$redReady.controller_creation_filetime_utc -ne
      [int64]$controllerCreationFileTimeUtc -or
    $redReady.controller_command_line_sha256 -cne
      $controllerCommandLineSha256 -or
    $redReady.controller_script_sha256 -cne
      $expectedControllerScriptSha256 -or
    $redReady.completed_labels.Count -ne 2 -or
    $redReady.completed_labels[0] -cne "t2-cfg-r" -or
    $redReady.completed_labels[1] -cne "t2-red" -or
    -not [IO.Path]::GetFullPath($redReady.evidence_root).Equals(
      [IO.Path]::GetFullPath($attempt005Path),
      [StringComparison]::OrdinalIgnoreCase
    )) {
  throw "RED-ready receipt type/value/correlation validation failed"
}
$taskPlanPath = [IO.Path]::Combine(
  $root,
  "docs\superpowers\plans\2026-07-28-openvino-cpu-kv-allocation-observability-task-02.md"
)
if ((Get-FileHash -LiteralPath $taskPlanPath -Algorithm SHA256).
      Hash.ToLowerInvariant() -cne $redReady.task_plan_sha256) {
  throw "RED-ready receipt does not bind the execution-copy plan"
}
$boundaryPath =
  [IO.Path]::Combine($controlDirectory, "task02-attempt005-boundary.json")
if ((Get-FileHash -LiteralPath $boundaryPath -Algorithm SHA256).
      Hash.ToLowerInvariant() -cne $redReady.boundary_sha256) {
  throw "RED-ready receipt does not bind the regenerated boundary"
}
if ($controllerProcess.HasExited -or
    -not [IO.Path]::GetFullPath($controllerProcess.MainModule.FileName).Equals(
      $controllerExecutable,
      [StringComparison]::OrdinalIgnoreCase
    )) {
  throw "RED-ready controller process identity changed"
}
$redLabels = @("t2-cfg-r", "t2-red")
$artifactProperties = @(
  $redReady.artifact_sha256.PSObject.Properties |
    ForEach-Object { [string]$_.Name }
)
$runIdProperties = @(
  $redReady.record_run_ids.PSObject.Properties |
    ForEach-Object { [string]$_.Name }
)
if ($artifactProperties.Count -ne 2 -or $runIdProperties.Count -ne 2) {
  throw "RED-ready receipt does not bind two exact labels"
}
foreach ($label in $redLabels) {
  if ($artifactProperties -cnotcontains $label -or
      $runIdProperties -cnotcontains $label) {
    throw "RED-ready receipt is missing label '$label'"
  }
  $triple = $redReady.artifact_sha256.PSObject.Properties[$label].Value
  $tripleProperties = @(
    $triple.PSObject.Properties | ForEach-Object { [string]$_.Name }
  )
  if ($triple -isnot [PSCustomObject] -or
      $tripleProperties.Count -ne 3 -or
      $tripleProperties -cnotcontains "wrapper" -or
      $tripleProperties -cnotcontains "record" -or
      $tripleProperties -cnotcontains "log") {
    throw "RED-ready artifact triple for '$label' is not exact"
  }
  foreach ($kind in @("wrapper", "record", "log")) {
    $hash = $triple.PSObject.Properties[$kind].Value
    if ($hash -isnot [string] -or $hash -cnotmatch "^[0-9a-f]{64}$") {
      throw "RED-ready $label/$kind hash is invalid"
    }
  }
  $runId = $redReady.record_run_ids.PSObject.Properties[$label].Value
  if ($runId -isnot [string] -or $runId -cnotmatch "^[0-9a-f]{64}$") {
    throw "RED-ready run ID for '$label' is invalid"
  }
}
$expectedRedFiles = @(
  "t2-cfg-r.json",
  "t2-cfg-r.log",
  "t2-cfg-r.wrapper.json",
  "t2-red.json",
  "t2-red.log",
  "t2-red.wrapper.json"
)
$actualRedFiles = @(
  [IO.Directory]::GetFiles($attempt005Path) |
    ForEach-Object { [IO.Path]::GetFileName($_) } |
    Sort-Object
)
if ($actualRedFiles.Count -ne $expectedRedFiles.Count) {
  throw "Attempt 005 does not contain the exact six RED artifacts"
}
for ($index = 0; $index -lt $expectedRedFiles.Count; ++$index) {
  if ($actualRedFiles[$index] -cne $expectedRedFiles[$index]) {
    throw "Attempt 005 RED artifact differs at index $index"
  }
}
foreach ($label in $redLabels) {
  $triple = $redReady.artifact_sha256.PSObject.Properties[$label].Value
  foreach ($kind in @("wrapper", "record", "log")) {
    $suffix = if ($kind -ceq "wrapper") { ".wrapper.json" }
      elseif ($kind -ceq "record") { ".json" }
      else { ".log" }
    $path = [IO.Path]::Combine($attempt005Path, $label + $suffix)
    $actualHash = (
      Get-FileHash -LiteralPath $path -Algorithm SHA256
    ).Hash.ToLowerInvariant()
    if ($actualHash -cne $triple.PSObject.Properties[$kind].Value) {
      throw "RED-ready hash differs from $label/$kind"
    }
  }
}
```

Do not invoke the launcher with `-File` from an already-running shell. From the
exact physical parent recovery worktree, run this direct prelaunch gate. The
fresh trusted Windows PowerShell 5.1 process verifies that inherited physical
CWD plus the launcher's exact path, ordinary-file identity, byte count, and
SHA-256 before dot-free invocation in that same process, and supplies the final
launcher/controller/GREEN/supervisor identities as mandatory arguments.
The launcher immediately self-reads and matches its authenticated arguments;
therefore neither a stale launcher, an `R:` execution CWD, nor a reparse-point
swap reaches attempt-state inspection:

```powershell
$trustedWindowsPowerShell =
  "C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe"
& $trustedWindowsPowerShell `
  -NoLogo `
  -NoProfile `
  -NonInteractive `
  -ExecutionPolicy Bypass `
  -Command {
    $ErrorActionPreference = "Stop"
    $launcher =
      "C:\Users\Student\IBM-Granite-Edge-AI-Optimisation-using-TurboQuant\.worktrees\openvino-turboquant-recovery\.superpowers\sdd\task02-attempt005-launcher.ps1"
    $expectedBytes = [int64]46968
    $expectedSha256 =
      "e05a8dd26e2f38c9f2e2a5ebb6fa3a5441834fbd6dfbc8282b111f2a1be0fbf8"
    $expectedControllerBytes = [int64]158223
    $expectedControllerSha256 =
      "9dcb8798ebc65357f8f049d216ba595b2e590378eda67a5877b0580b6004674b"
    $expectedGreenSignalBytes = [int64]37175
    $expectedGreenSignalSha256 =
      "9174b0c8bf89457f91e832b2b495caa19d8584f7e230205908fb1c9ba9c5664b"
    $expectedLinkCapSupervisorBytes = [int64]38077
    $expectedLinkCapSupervisorSha256 =
      "fd005d235b27a532ac5d0536ccb5b429e9c4f554efcd1f6ed5c9a5cf08e0f90b"
    $expectedRoot =
      "C:\Users\Student\IBM-Granite-Edge-AI-Optimisation-using-TurboQuant\.worktrees\openvino-turboquant-recovery"
    if ($PSVersionTable.PSEdition -cne "Desktop" -or
        $PSVersionTable.PSVersion.Major -ne 5 -or
        $PSVersionTable.PSVersion.Minor -ne 1 -or
        -not [IO.Path]::GetFullPath((Get-Process -Id $PID).Path).Equals(
          "C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",
          [StringComparison]::OrdinalIgnoreCase
        ) -or
        -not [IO.Path]::GetFullPath((Get-Location).ProviderPath).Equals(
          $expectedRoot,
          [StringComparison]::OrdinalIgnoreCase
        )) {
      throw "Task 2 launcher gate requires exact Windows PowerShell and root"
    }
    $item = Get-Item -LiteralPath $launcher -Force
    if (-not $item.FullName.Equals(
          $launcher,
          [StringComparison]::OrdinalIgnoreCase
        ) -or
        ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
        $item.Length -ne $expectedBytes -or
        (Get-FileHash -LiteralPath $launcher -Algorithm SHA256).
          Hash.ToLowerInvariant() -cne $expectedSha256) {
      throw "Task 2 launcher direct prelaunch identity gate failed"
    }
    & $launcher `
      -AuthenticatedLauncherBytes $expectedBytes `
      -AuthenticatedLauncherSha256 $expectedSha256 `
      -ExpectedControllerScriptBytes $expectedControllerBytes `
      -ExpectedControllerScriptSha256 $expectedControllerSha256 `
      -ExpectedGreenSignalScriptBytes $expectedGreenSignalBytes `
      -ExpectedGreenSignalScriptSha256 $expectedGreenSignalSha256 `
      -ExpectedLinkCapSupervisorScriptBytes $expectedLinkCapSupervisorBytes `
      -ExpectedLinkCapSupervisorScriptSha256 $expectedLinkCapSupervisorSha256
  }
if ($LASTEXITCODE -ne 0) {
  throw "Task 2 trusted launcher process failed with exit $LASTEXITCODE"
}
```

```powershell
$ErrorActionPreference = "Stop"
$controllerExecutable =
  "C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe"
$controllerScriptPath =
  "C:\Users\Student\IBM-Granite-Edge-AI-Optimisation-using-TurboQuant\.worktrees\openvino-turboquant-recovery\.superpowers\sdd\task02-attempt005-controller.ps1"
$launcherScriptPath =
  "C:\Users\Student\IBM-Granite-Edge-AI-Optimisation-using-TurboQuant\.worktrees\openvino-turboquant-recovery\.superpowers\sdd\task02-attempt005-launcher.ps1"
$expectedLauncherScriptBytes = [int64]46968
$expectedLauncherScriptSha256 =
  "e05a8dd26e2f38c9f2e2a5ebb6fa3a5441834fbd6dfbc8282b111f2a1be0fbf8"
$greenSignalScriptPath =
  "C:\Users\Student\IBM-Granite-Edge-AI-Optimisation-using-TurboQuant\.worktrees\openvino-turboquant-recovery\.superpowers\sdd\task02-attempt005-green-signal.ps1"
$expectedControllerWorkingDirectory =
  "C:\Users\Student\IBM-Granite-Edge-AI-Optimisation-using-TurboQuant\.worktrees\openvino-turboquant-recovery"
$controllerJobReadyPath =
  "C:\Users\Student\IBM-Granite-Edge-AI-Optimisation-using-TurboQuant\.worktrees\openvino-turboquant-recovery\.superpowers\sdd\task02-attempt005-controller-job-ready.json"
$timeoutCleanupPath =
  "C:\Users\Student\IBM-Granite-Edge-AI-Optimisation-using-TurboQuant\.worktrees\openvino-turboquant-recovery\.superpowers\sdd\task02-attempt005-timeout-cleanup.json"
$controllerJobName = "Local\OpenVINO-Task02-Attempt005-Controller"
$jobReadyDeadline = [DateTime]::UtcNow.AddSeconds(60)
while (-not [IO.File]::Exists($controllerJobReadyPath)) {
  if ([DateTime]::UtcNow -ge $jobReadyDeadline) {
    throw "Controller Job-ready handshake was not published before deadline"
  }
  [Threading.Thread]::Sleep(50)
}
if ([IO.File]::Exists($timeoutCleanupPath)) {
  throw "Controller timeout-cleanup receipt exists before execution"
}
$jobReadyBytes = [IO.File]::ReadAllBytes($controllerJobReadyPath)
$jobReadyText =
  [Text.UTF8Encoding]::new($false, $true).GetString($jobReadyBytes)
$jobReadyJson = $ExecutionContext.InvokeCommand.GetCommand(
  "Microsoft.PowerShell.Utility\ConvertFrom-Json",
  [Management.Automation.CommandTypes]::Cmdlet
)
if ($null -eq $jobReadyJson -or
    $jobReadyJson -isnot [Management.Automation.CmdletInfo] -or
    $jobReadyJson.ModuleName -cne "Microsoft.PowerShell.Utility" -or
    $jobReadyJson.ImplementingType.FullName -cne
      "Microsoft.PowerShell.Commands.ConvertFromJsonCommand") {
  throw "Controller Job-ready parser is not trusted"
}
$controllerJobReady = $jobReadyText | & $jobReadyJson -ErrorAction Stop
$expectedJobReadyProperties = @(
  "schema", "job_name", "job_limit_flags", "create_process_flags",
  "proc_thread_attribute_job_list", "startupinfoex_size", "size_probe_error",
  "controller_pid", "controller_start_filetime_utc",
  "controller_creation_filetime_utc", "controller_command_line_sha256",
  "controller_script_sha256", "suspended_pid_list_returned_bytes",
  "assigned_at_create", "job_member_before_resume",
  "resume_previous_suspend_count", "duplicate_same_access",
  "duplicate_inheritable", "controller_owned_handle",
  "launcher_handle_closed_before_publish", "launcher_identity", "created_utc"
)
$actualJobReadyProperties = @(
  $controllerJobReady.PSObject.Properties |
    ForEach-Object { [string]$_.Name }
)
if ($controllerJobReady -isnot [PSCustomObject] -or
    $actualJobReadyProperties.Count -ne $expectedJobReadyProperties.Count) {
  throw "Controller Job-ready shape is not exact"
}
foreach ($property in $expectedJobReadyProperties) {
  if ($actualJobReadyProperties -cnotcontains $property) {
    throw "Controller Job-ready is missing '$property'"
  }
}
$controllerScriptBootstrapSha256 = (
  Get-FileHash -LiteralPath $controllerScriptPath -Algorithm SHA256
).Hash.ToLowerInvariant()
$launcherScriptItem = Get-Item -LiteralPath $launcherScriptPath -Force
$launcherScriptBootstrapSha256 = (
  Get-FileHash -LiteralPath $launcherScriptPath -Algorithm SHA256
).Hash.ToLowerInvariant()
$expectedLauncherIdentityProperties = @(
  "path", "bytes", "sha256", "reparse", "authentication"
)
$actualLauncherIdentityProperties = @(
  $controllerJobReady.launcher_identity.PSObject.Properties |
    ForEach-Object { [string]$_.Name }
)
if ($controllerJobReady.schema -cne
      "openvino-cpu-observer-task02-controller-job-ready/v2" -or
    $controllerJobReady.job_name -cne $controllerJobName -or
    [uint32]$controllerJobReady.job_limit_flags -ne 8192 -or
    [uint32]$controllerJobReady.create_process_flags -ne 134742020 -or
    [uint32]$controllerJobReady.proc_thread_attribute_job_list -ne 131085 -or
    [uint32]$controllerJobReady.startupinfoex_size -ne 112 -or
    [uint32]$controllerJobReady.size_probe_error -ne 122 -or
    [int64]$controllerJobReady.controller_pid -ne [int64]$PID -or
    [int64]$controllerJobReady.controller_start_filetime_utc -le 0 -or
    [int64]$controllerJobReady.controller_creation_filetime_utc -le 0 -or
    $controllerJobReady.controller_command_line_sha256 -cnotmatch
      "^[0-9a-f]{64}$" -or
    $controllerJobReady.controller_script_sha256 -cne
      $controllerScriptBootstrapSha256 -or
    [uint32]$controllerJobReady.suspended_pid_list_returned_bytes -lt 16 -or
    [bool]$controllerJobReady.assigned_at_create -ne $true -or
    [bool]$controllerJobReady.job_member_before_resume -ne $true -or
    [uint32]$controllerJobReady.resume_previous_suspend_count -ne 1 -or
    [bool]$controllerJobReady.duplicate_same_access -ne $true -or
    [bool]$controllerJobReady.duplicate_inheritable -ne $false -or
    [bool]$controllerJobReady.controller_owned_handle -ne $true -or
    [bool]$controllerJobReady.launcher_handle_closed_before_publish -ne
      $true -or
    $controllerJobReady.launcher_identity -isnot [PSCustomObject] -or
    $actualLauncherIdentityProperties.Count -ne
      $expectedLauncherIdentityProperties.Count -or
    @(
      $expectedLauncherIdentityProperties | Where-Object {
        $actualLauncherIdentityProperties -cnotcontains $_
      }
    ).Count -ne 0 -or
    -not [IO.Path]::GetFullPath(
      [string]$controllerJobReady.launcher_identity.path
    ).Equals(
      $launcherScriptPath,
      [StringComparison]::OrdinalIgnoreCase
    ) -or
    -not [IO.Path]::GetFullPath($launcherScriptItem.FullName).Equals(
      $launcherScriptPath,
      [StringComparison]::OrdinalIgnoreCase
    ) -or
    ($launcherScriptItem.Attributes -band
      [IO.FileAttributes]::ReparsePoint) -ne 0 -or
    [int64]$launcherScriptItem.Length -ne $expectedLauncherScriptBytes -or
    $launcherScriptBootstrapSha256 -cne $expectedLauncherScriptSha256 -or
    [int64]$controllerJobReady.launcher_identity.bytes -ne
      $expectedLauncherScriptBytes -or
    [string]$controllerJobReady.launcher_identity.sha256 -cne
      $expectedLauncherScriptSha256 -or
    [bool]$controllerJobReady.launcher_identity.reparse -ne $false -or
    [string]$controllerJobReady.launcher_identity.authentication -cne
      "direct-prelaunch-parameters-plus-launcher-self-readback") {
  throw "Controller Job-ready value contract is not exact"
}
$controllerJobReadySha256 = (
  Get-FileHash -LiteralPath $controllerJobReadyPath -Algorithm SHA256
).Hash.ToLowerInvariant()
if (-not ("Task02ControllerIdentityNative" -as [type])) {
  Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class Task02ControllerIdentityNative {
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetProcessTimes(
        IntPtr process,
        out Int64 creationTime,
        out Int64 exitTime,
        out Int64 kernelTime,
        out Int64 userTime);
}
'@
}
if (@(Get-Process -Name csc -ErrorAction SilentlyContinue).Count -ne 0) {
  throw "Controller identity bootstrap left an unowned csc.exe"
}
$parentWorktreePhysical = [IO.Path]::GetFullPath(
  ((& git rev-parse --show-toplevel).Trim() -replace '/', '\')
)
if ($PSVersionTable.PSEdition -cne "Desktop" -or
    $PSVersionTable.PSVersion.Major -ne 5 -or
    $PSVersionTable.PSVersion.Minor -ne 1 -or
    -not [IO.Path]::GetFullPath(
      (Get-Process -Id $PID).Path
    ).Equals(
      "C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",
      [StringComparison]::OrdinalIgnoreCase
    ) -or
    (Get-ExecutionPolicy -Scope Process) -ne "Bypass") {
  throw "Task 2 requires fresh no-profile Windows PowerShell 5.1"
}
if (-not $parentWorktreePhysical.Equals(
      [IO.Path]::GetFullPath($expectedControllerWorkingDirectory),
      [StringComparison]::OrdinalIgnoreCase
    ) -or
    -not [IO.Path]::GetFullPath((Get-Location).ProviderPath).Equals(
      [IO.Path]::GetFullPath($expectedControllerWorkingDirectory),
      [StringComparison]::OrdinalIgnoreCase
    )) {
  throw "Task 2 controller working directory is not the exact recovery worktree"
}
$controllerScriptItem = Get-Item -LiteralPath $controllerScriptPath -Force
if (($controllerScriptItem.Attributes -band
      [IO.FileAttributes]::ReparsePoint) -ne 0) {
  throw "Task 2 controller script must not be a reparse point"
}
$controllerScriptSha256 = (
  Get-FileHash -LiteralPath $controllerScriptPath -Algorithm SHA256
).Hash.ToLowerInvariant()
$controllerProcess = Get-Process -Id $PID
$controllerCreationFileTimeUtc = [int64]0
$controllerExitFileTimeUtc = [int64]0
$controllerKernelFileTimeUtc = [int64]0
$controllerUserFileTimeUtc = [int64]0
if (-not [Task02ControllerIdentityNative]::GetProcessTimes(
    $controllerProcess.Handle,
    [ref]$controllerCreationFileTimeUtc,
    [ref]$controllerExitFileTimeUtc,
    [ref]$controllerKernelFileTimeUtc,
    [ref]$controllerUserFileTimeUtc
  ) -or $controllerCreationFileTimeUtc -le 0) {
  $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
  throw "Controller native creation FILETIME query failed: $errorCode"
}
$controllerStartFileTimeUtc = $controllerCreationFileTimeUtc
$controllerRows = @(
  Get-CimInstance -ClassName Win32_Process -Property @(
    "ProcessId", "ExecutablePath", "CommandLine"
  ) | Where-Object { [int64]$_.ProcessId -eq [int64]$PID }
)
if ($controllerRows.Count -ne 1 -or
    -not [IO.Path]::GetFullPath(
      [string]$controllerRows[0].ExecutablePath
    ).Equals(
      $controllerExecutable,
      [StringComparison]::OrdinalIgnoreCase
    ) -or
    [string]::IsNullOrWhiteSpace([string]$controllerRows[0].CommandLine)) {
  throw "Task 2 controller process identity was not observable"
}
$controllerCommandLine = [string]$controllerRows[0].CommandLine
$controllerCommandPattern =
  '^"?' + [regex]::Escape($controllerExecutable) +
  '"? -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "?' +
  [regex]::Escape($controllerScriptPath) + '"?$'
if ($controllerCommandLine -cnotmatch $controllerCommandPattern) {
  throw "Task 2 controller executable or exact command line is wrong"
}
$controllerCommandBytes =
  [Text.UTF8Encoding]::new($false, $true).GetBytes($controllerCommandLine)
$controllerCommandSha = [Security.Cryptography.SHA256]::Create()
try {
  $controllerCommandLineSha256 = (
    [BitConverter]::ToString(
      $controllerCommandSha.ComputeHash($controllerCommandBytes)
    )
  ).Replace("-", "").ToLowerInvariant()
} finally {
  $controllerCommandSha.Dispose()
}
if ([int64]$controllerJobReady.controller_start_filetime_utc -ne
      [int64]$controllerStartFileTimeUtc -or
    [int64]$controllerJobReady.controller_creation_filetime_utc -ne
      [int64]$controllerCreationFileTimeUtc -or
    $controllerJobReady.controller_command_line_sha256 -cne
      $controllerCommandLineSha256) {
  throw "Controller Job-ready process identity differs from the live controller"
}
$initialControlDirectory = [IO.Path]::Combine(
  $parentWorktreePhysical, ".superpowers\sdd"
)
$controlTempNamePattern =
  '^(?:task02-attempt005-(?:boundary|receipt)\.json\.[0-9a-f]{32}\.tmp|' +
  '\.task02-attempt005-(?:controller-job-ready|timeout-cleanup)\.[0-9a-f]{32}\.tmp|' +
  '\.task02-attempt005-(?:red|green)-ready\.json\.[0-9a-f]{32}\.tmp|' +
  '\.task02-green-ready\.[0-9a-f]{32}\.tmp)$'
function Get-Task2ControlTempResidue {
  @(
    [IO.Directory]::EnumerateFiles(
      $initialControlDirectory, "*", [IO.SearchOption]::TopDirectoryOnly
    ) | Where-Object {
      [IO.Path]::GetFileName($_) -cmatch $controlTempNamePattern
    }
  )
}
$initialControlTempResidue = @(Get-Task2ControlTempResidue)
if ($initialControlTempResidue.Count -ne 0) {
  throw "Task 2 attempt 005 control temporary residue exists; preserve it"
}
$currentUserPolicyBefore = Get-ExecutionPolicy -Scope CurrentUser
$localMachinePolicyBefore = Get-ExecutionPolicy -Scope LocalMachine
$roadmap = "docs\superpowers\plans\2026-07-28-openvino-cpu-kv-allocation-observability.md"
$roadmapHash = (Get-FileHash -LiteralPath $roadmap -Algorithm SHA256).Hash.ToLowerInvariant()
if ($roadmapHash -ne "9497cba857247af42bc7f889c17fc2e248edd6a02cf5703906aec23434504a3e") {
  throw "Task 2 roadmap changed; regenerate and re-review this micro-plan"
}
$taskPlan =
  "docs/superpowers/plans/2026-07-28-openvino-cpu-kv-allocation-observability-task-02.md"
$guardCommit = "37f4ea62326441233f34f5a2996d3c5667fae6ec"
& git merge-base --is-ancestor $guardCommit HEAD
if ($LASTEXITCODE -ne 0) {
  throw "Parent HEAD does not contain the independently accepted guard revision"
}
& git diff --quiet -- $taskPlan
if ($LASTEXITCODE -ne 0) {
  throw "Task 2 plan must be committed before regenerating the resume boundary"
}
$parentHead = (& git rev-parse HEAD).Trim()
$taskPlanBlob = (& git rev-parse "HEAD:$taskPlan").Trim()
$taskPlanWorktreeBlob = (& git hash-object $taskPlan).Trim()
if ($taskPlanBlob -ne $taskPlanWorktreeBlob) {
  throw "Committed Task 2 plan blob differs from the execution copy"
}
$taskPlanSha256 = (
  Get-FileHash -LiteralPath $taskPlan -Algorithm SHA256
).Hash.ToLowerInvariant()

$commonDirRaw = (& git rev-parse --git-common-dir).Trim()
if (-not $commonDirRaw) { throw "git-common-dir is empty" }
$commonDir = if ([IO.Path]::IsPathRooted($commonDirRaw)) {
  [IO.Path]::GetFullPath($commonDirRaw)
} else {
  (Resolve-Path -LiteralPath $commonDirRaw).Path
}
$sharedRepoRoot = Split-Path -Parent $commonDir
$sharedTop = (& git -C $sharedRepoRoot rev-parse --show-toplevel).Trim()
if (-not [IO.Path]::GetFullPath($sharedTop).Equals(
    [IO.Path]::GetFullPath($sharedRepoRoot),
    [StringComparison]::OrdinalIgnoreCase)) {
  throw "Shared repository root resolution failed"
}

$cleanCore = Join-Path $sharedRepoRoot "external\official-openvino\2026-07-19\openvino"
$identityPath = "R:\external\official-openvino\2026-07-19\openvino-cpu-state-observer.identity.json"
$base = "ede283a88e35465f0d680dabbf1f44080f8fc387"
$identity = Get-Content -LiteralPath $identityPath -Raw | ConvertFrom-Json
$expectedDerivedCore =
  "C:\ov-wb04\2026-07-19\openvino-cpu-state-observer"
if (-not [IO.Path]::GetFullPath([string]$identity.destination_path).Equals(
    [IO.Path]::GetFullPath($expectedDerivedCore),
    [StringComparison]::OrdinalIgnoreCase)) {
  throw "Canonical identity does not name the accepted Task 01C destination"
}
$derivedCore = (Resolve-Path -LiteralPath $identity.destination_path).Path

if ((& git -C $cleanCore rev-parse HEAD).Trim() -ne $base) {
  throw "Clean OpenVINO HEAD is not the exact pin"
}
if (& git -C $cleanCore status --porcelain --untracked-files=all) {
  throw "Clean OpenVINO source is dirty"
}
if ((& git -C $derivedCore branch --show-current).Trim() -ne
    "project/cpu-state-allocation-observer") {
  throw "Derived core is on the wrong branch"
}
$redFixtureRelative =
  "src/plugins/intel_cpu/tests/unit/state_allocations_dump_test.cpp"
$redFixture = Join-Path $derivedCore ($redFixtureRelative -replace '/', '\')
$derivedStatus = @(
  & git -C $derivedCore status --porcelain=v1 --untracked-files=all
)
if ($derivedStatus.Count -ne 1 -or
    [string]$derivedStatus[0] -cne "?? $redFixtureRelative") {
  throw "Resume boundary permits only the exact untracked RED fixture"
}
$redFixtureItem = Get-Item -LiteralPath $redFixture -Force
if (-not (Test-Path -LiteralPath $redFixture -PathType Leaf) -or
    ($redFixtureItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
    $redFixtureItem.Length -ne 22516 -or
    (Get-FileHash -LiteralPath $redFixture -Algorithm SHA256).Hash.ToLowerInvariant() -ne
      "a1e18eda017edec7f473dd8c885e172e7379e2f92f38894fda627dce1c6f608b") {
  throw "Task 2 RED fixture does not match the accepted resume boundary"
}
& git -C $derivedCore merge-base --is-ancestor $base HEAD
if ($LASTEXITCODE -ne 0) { throw "Derived core is not based on the exact pin" }

$derivedHead = (& git -C $derivedCore rev-parse HEAD).Trim()
$derivedTree = (& git -C $derivedCore rev-parse "HEAD^{tree}").Trim()
if ($identity.base_commit -ne $base -or
    $identity.patch_commit -ne $derivedHead -or
    $identity.derived_tree -ne $derivedTree -or
    $identity.dirty -ne $false) {
  throw "Derived identity JSON does not match the exact clean checkout"
}

$taskPaths = @(
  "src/plugins/intel_cpu/src/cpu_memory.h",
  "src/plugins/intel_cpu/src/cpu_memory.cpp",
  "src/plugins/intel_cpu/src/utils/state_allocations_dump.hpp",
  "src/plugins/intel_cpu/src/utils/state_allocations_dump.cpp",
  "src/plugins/intel_cpu/tests/unit/state_allocations_dump_test.cpp"
)
$preimages = [ordered]@{}
foreach ($path in $taskPaths) {
  $entry = @(& git -C $derivedCore ls-tree HEAD -- $path)
  if ($LASTEXITCODE -ne 0) {
    throw "Unable to inspect derived preimage for $path"
  }
  if ($entry.Count -eq 0) {
    $preimages[$path] = "ABSENT"
  } else {
    if ($entry.Count -ne 1 -or
        [string]$entry[0] -cnotmatch
          '^100644 blob (?<blob>[0-9a-f]{40})\t(?<path>.+)$' -or
        $Matches.path -cne $path) {
      throw "Derived preimage tree entry is not exact for $path"
    }
    $preimages[$path] = $Matches.blob
  }
}
$cleanPreimages = [ordered]@{}
foreach ($path in $taskPaths) {
  $entry = @(& git -C $cleanCore ls-tree $base -- $path)
  if ($LASTEXITCODE -ne 0) {
    throw "Unable to inspect clean preimage for $path"
  }
  if ($entry.Count -eq 0) {
    $cleanPreimages[$path] = "ABSENT"
  } else {
    if ($entry.Count -ne 1 -or
        [string]$entry[0] -cnotmatch
          '^100644 blob (?<blob>[0-9a-f]{40})\t(?<path>.+)$' -or
        $Matches.path -cne $path) {
      throw "Clean preimage tree entry is not exact for $path"
    }
    $cleanPreimages[$path] = $Matches.blob
  }
  if ($cleanPreimages[$path] -ne $preimages[$path]) {
    throw "Derived Task 2 preimage differs from immutable base: $path"
  }
}
if ($preimages["src/plugins/intel_cpu/src/utils/state_allocations_dump.hpp"] -ne "ABSENT" -or
    $preimages["src/plugins/intel_cpu/src/utils/state_allocations_dump.cpp"] -ne "ABSENT" -or
    $preimages["src/plugins/intel_cpu/tests/unit/state_allocations_dump_test.cpp"] -ne "ABSENT") {
  throw "A supposedly new Task 2 path already exists"
}

$guardExpected = [ordered]@{
  "scripts/testing/invoke_guarded_command.ps1" = [ordered]@{
    blob = "f47dbe4cabeab0e6c7d4363a01196a21eae147db"
    sha256 =
      "85e03b8cbd41b8821812004c00bf7f9b7525d9f3d8b666dd094a417e8af8fc3d"
  }
  "scripts/testing/official_openvino/guarded_build.py" = [ordered]@{
    blob = "dffd7def889a1eef70d291009275be4ab078a7ee"
    sha256 =
      "1a46fe30bbb622bc5887cb67bdaaeabf2113bf5531feef54bbd6b8ee86ae8d0a"
  }
  "scripts/testing/official_openvino/owned_process_guard.py" = [ordered]@{
    blob = "f9199294e4e0d50947803ce6468c6ba1cf2fc389"
    sha256 =
      "ab1fd112ee8b56922a9dba6a56a0b170d2fd7f02acb5ab9eb7b2ca88b3114da2"
  }
  "scripts/testing/tests/test_official_openvino_guarded_build.py" = [ordered]@{
    blob = "36f6f3e7eed6826c4e540f17dc4afc9f6d20f845"
    sha256 =
      "b32b74abb36c1d124281d44b200961d9833582b2c1027dc8ae252b47943e19b0"
  }
}
$guardBlobs = [ordered]@{}
$guardSha256 = [ordered]@{}
foreach ($path in $guardExpected.Keys) {
  & git cat-file -e "HEAD:$path"
  if ($LASTEXITCODE -ne 0) { throw "Missing committed guard prerequisite: $path" }
  $actualBlob = (& git rev-parse "HEAD:$path").Trim()
  $actualSha256 = (
    Get-FileHash -LiteralPath $path -Algorithm SHA256
  ).Hash.ToLowerInvariant()
  if ($actualBlob -ne $guardExpected[$path].blob -or
      $actualSha256 -ne $guardExpected[$path].sha256) {
    throw "Guard prerequisite drifted from accepted revision 5: $path"
  }
  $guardBlobs[$path] = $actualBlob
  $guardSha256[$path] = $actualSha256
}

$pythonExecutable =
  "C:\Users\Student\AppData\Local\Programs\Python\Python311\python.exe"
$pythonDll =
  "C:\Users\Student\AppData\Local\Programs\Python\Python311\python311.dll"
$pythonSha256 =
  "5f7b89a612c9b8af1d6456cdfcd1dbe5ca630849e79aebced9bee9a6694952ec"
$pythonDllSha256 =
  "0817a2a657a24c0d5fbb60df56960f42fc66b3039d522ec952dab83e2d869364"
if ((Get-FileHash -LiteralPath $pythonExecutable -Algorithm SHA256).Hash.ToLowerInvariant() -ne
      $pythonSha256 -or
    (Get-FileHash -LiteralPath $pythonDll -Algorithm SHA256).Hash.ToLowerInvariant() -ne
      $pythonDllSha256) {
  throw "Caller-approved Python controller runtime drifted"
}

$linkCapSupervisorPath = [IO.Path]::Combine(
  $parentWorktreePhysical,
  ".superpowers\sdd\task02-attempt005-link-cap-supervisor.ps1"
)
$linkCapSupervisorItem =
  Get-Item -LiteralPath $linkCapSupervisorPath -Force
if (($linkCapSupervisorItem.Attributes -band
      [IO.FileAttributes]::ReparsePoint) -ne 0 -or
    $linkCapSupervisorItem.Length -ne 38077 -or
    (Get-FileHash -LiteralPath $linkCapSupervisorPath -Algorithm SHA256).
      Hash.ToLowerInvariant() -cne
      "fd005d235b27a532ac5d0536ccb5b429e9c4f554efcd1f6ed5c9a5cf08e0f90b") {
  throw "Task 02 attempt 005 link-cap supervisor bytes drifted"
}

function Get-Task2ControlScriptIdentity {
  param(
    [Parameter(Mandatory = $true)][string]$Name,
    [Parameter(Mandatory = $true)][string]$Path
  )
  $expectedPath = [IO.Path]::GetFullPath(
    [IO.Path]::Combine(
      $parentWorktreePhysical,
      ".superpowers\sdd",
      [IO.Path]::GetFileName($Path)
    )
  )
  $item = Get-Item -LiteralPath $Path -Force
  if (-not [IO.Path]::GetFullPath($item.FullName).Equals(
        $expectedPath,
        [StringComparison]::OrdinalIgnoreCase
      ) -or
      ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
      $item.Length -le 0) {
    throw "Task 2 $Name control script path/bytes are not exact"
  }
  [ordered]@{
    path = $expectedPath
    bytes = [int64]$item.Length
    sha256 = (
      Get-FileHash -LiteralPath $expectedPath -Algorithm SHA256
    ).Hash.ToLowerInvariant()
    reparse = $false
  }
}

$controlScripts = [ordered]@{
  launcher = Get-Task2ControlScriptIdentity `
    -Name "launcher" `
    -Path $launcherScriptPath
  controller = Get-Task2ControlScriptIdentity `
    -Name "controller" `
    -Path $controllerScriptPath
  green_signal = Get-Task2ControlScriptIdentity `
    -Name "GREEN signal" `
    -Path $greenSignalScriptPath
  link_cap_supervisor = Get-Task2ControlScriptIdentity `
    -Name "link-cap supervisor" `
    -Path $linkCapSupervisorPath
}
if (-not [IO.Path]::GetFullPath(
      [string]$controlScripts.launcher.path
    ).Equals(
      $launcherScriptPath,
      [StringComparison]::OrdinalIgnoreCase
    ) -or
    [int64]$controlScripts.launcher.bytes -ne
      $expectedLauncherScriptBytes -or
    [string]$controlScripts.launcher.sha256 -cne
      $expectedLauncherScriptSha256 -or
    [bool]$controlScripts.launcher.reparse -ne $false -or
    $controlScripts.controller.sha256 -cne $controllerScriptSha256 -or
    $controlScripts.link_cap_supervisor.bytes -ne
      38077 -or
    $controlScripts.link_cap_supervisor.sha256 -cne
      "fd005d235b27a532ac5d0536ccb5b429e9c4f554efcd1f6ed5c9a5cf08e0f90b") {
  throw "Task 2 control-script identity set did not close"
}

$boundary = [ordered]@{
  schema = "openvino-cpu-observer-task02-boundary/v4"
  generated_utc = [DateTime]::UtcNow.ToString("o")
  parent_plan_commit = $parentHead
  task02_plan_blob = $taskPlanBlob
  task02_plan_sha256 = $taskPlanSha256
  roadmap_sha256 = $roadmapHash
  clean_core_commit = (& git -C $cleanCore rev-parse HEAD).Trim()
  clean_core_tree = (& git -C $cleanCore rev-parse "HEAD^{tree}").Trim()
  derived_start_commit = $derivedHead
  derived_start_tree = $derivedTree
  derived_identity_sha256 = (
    Get-FileHash -LiteralPath $identityPath -Algorithm SHA256
  ).Hash.ToLowerInvariant()
  task_preimage_blobs = $preimages
  clean_preimage_blobs = $cleanPreimages
  resume_fixture = [ordered]@{
    path = $redFixtureRelative
    bytes = 22516
    sha256 =
      "a1e18eda017edec7f473dd8c885e172e7379e2f92f38894fda627dce1c6f608b"
  }
  accepted_guard_commit = $guardCommit
  guard_blobs = $guardBlobs
  guard_sha256 = $guardSha256
  guard_acceptance = [ordered]@{
    spec = "PASS"
    quality = "PASS"
    focused_tests = "46/46"
    combined_tests = "136/136"
    unowned_csc_processes = 0
  }
  controller_runtime = [ordered]@{
    python_executable = $pythonExecutable
    python_sha256 = $pythonSha256
    python_dll = $pythonDll
    python_dll_sha256 = $pythonDllSha256
  }
  control_scripts = $controlScripts
  controller_process = [ordered]@{
    executable = $controllerExecutable
    script = $controllerScriptPath
    script_sha256 = $controllerScriptSha256
    pid = [int64]$PID
    start_filetime_utc = [int64]$controllerStartFileTimeUtc
    creation_filetime_utc = [int64]$controllerCreationFileTimeUtc
    command_line_sha256 = $controllerCommandLineSha256
    working_directory = $parentWorktreePhysical
  }
  controller_job = [ordered]@{
    name = $controllerJobName
    limit_flags = 8192
    create_process_flags = 134742020
    proc_thread_attribute_job_list = 131085
    startupinfoex_size = 112
    size_probe_error = 122
    assigned_at_create = $true
    job_member_before_resume = $true
    resume_previous_suspend_count = 1
    duplicate_same_access = $true
    duplicate_inheritable = $false
    controller_owned_handle = $true
    launcher_handle_closed_before_publish = $true
    launcher_identity = [ordered]@{
      path = [string]$controllerJobReady.launcher_identity.path
      bytes = [int64]$controllerJobReady.launcher_identity.bytes
      sha256 = [string]$controllerJobReady.launcher_identity.sha256
      reparse = [bool]$controllerJobReady.launcher_identity.reparse
      authentication =
        [string]$controllerJobReady.launcher_identity.authentication
    }
    timeout_action = "TerminateJobObject"
    timeout_exit_code = 125
    job_ready = [ordered]@{
      path =
        ".superpowers/sdd/task02-attempt005-controller-job-ready.json"
      bytes = [int64]$jobReadyBytes.Length
      sha256 = $controllerJobReadySha256
    }
    timeout_cleanup_path =
      ".superpowers/sdd/task02-attempt005-timeout-cleanup.json"
  }
  link_cap_supervisor = [ordered]@{
    path = ".superpowers/sdd/task02-attempt005-link-cap-supervisor.ps1"
    bytes = 38077
    sha256 =
      "fd005d235b27a532ac5d0536ccb5b429e9c4f554efcd1f6ed5c9a5cf08e0f90b"
    powershell = $controllerExecutable
    maximum_working_set_bytes = 4294967296
    minimum_working_set_bytes = 1048576
    set_flags = 4
    readback_flags = 6
    nested_job = $true
    job_limit_flags = 8960
    process_memory_limit_bytes = 6442450944
    job_memory_limit_bytes = 7516192768
    create_process_flags = 134217732
    completion_poll_milliseconds = 100
    completion_messages = [ordered]@{
      active_process_zero = 4
      new_process = 6
      exit_process = 7
      abnormal_exit_process = 8
    }
    completion_delivery_reconciled_to_job_accounting = $true
    application_timing =
      "commit limits before suspended-root assignment/resume; linker working-set cap after exact nested-Job NEW_PROCESS"
    blocking_process_names = @(
      "MSBuild.exe",
      "link.exe",
      "cl.exe",
      "cmake.exe",
      "ctest.exe",
      "ninja.exe",
      "ov_cpu_unit_tests.exe"
    )
    minimum_physical_headroom_bytes = 7784628224
    minimum_virtual_headroom_bytes = 5368709120
    link_executable =
      "C:\Program Files\Microsoft Visual Studio\18\Community\VC\Tools\MSVC\14.51.36231\bin\Hostx64\x64\link.exe"
    link_sha256 =
      "e8c524347b8bc87fba790d254c8a3b902bf1a4b63807093b816d992940af3791"
    link_file_version = "14.51.36248.0"
    msbuild_executable =
      "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe"
    msbuild_sha256 =
      "106cac9dc67569fe80102cb4a11f49dc48ad44529cabcaf2259e1aa74ad19bec"
    child_commands = [ordered]@{
      Unit = @(
        "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe",
        "C:\ov-build\state-observer\src\plugins\intel_cpu\tests\unit\ov_cpu_unit_tests.vcxproj",
        "/t:Build",
        "/p:Configuration=Release",
        "/p:Platform=x64",
        "/p:BuildProjectReferences=true",
        "/p:MultiProcCL=false",
        "/p:UseMultiToolTask=false",
        "/p:TrackFileAccess=false",
        "/m:1",
        "/nr:false",
        "/v:minimal"
      )
      Plugin = @(
        "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe",
        "C:\ov-build\state-observer\src\plugins\intel_cpu\openvino_intel_cpu_plugin.vcxproj",
        "/t:Build",
        "/p:Configuration=Release",
        "/p:Platform=x64",
        "/p:BuildProjectReferences=true",
        "/p:MultiProcCL=false",
        "/p:UseMultiToolTask=false",
        "/p:TrackFileAccess=false",
        "/m:1",
        "/nr:false",
        "/v:minimal"
      )
    }
    guarded_supervisor_commands = [ordered]@{
      Unit = @(
        $controllerExecutable,
        "-NoLogo",
        "-NoProfile",
        "-NonInteractive",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        $linkCapSupervisorPath,
        "-Mode",
        "Unit"
      )
      Plugin = @(
        $controllerExecutable,
        "-NoLogo",
        "-NoProfile",
        "-NonInteractive",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        $linkCapSupervisorPath,
        "-Mode",
        "Plugin"
      )
    }
  }
}
```

The `.superpowers/sdd` boundary record is ignored scratch, not a task change.
It is published only after the substitutions, historical attempts, and reusable
cache below have also been bound. Do not proceed if any precondition fails.

Establish or validate `O:` with a crash-resumable collision check. Task 01C may
have left `O:` mapped to the now-retired in-tree campaign directory. That one
known mapping may be retired only after proving the accepted identity and
absence of the rejected checkout/replacement identity; every other collision
is rejected:

```powershell
$expectedOTarget = [IO.Path]::GetFullPath($derivedCore).TrimEnd('\')
$parentWorktreePhysical = [IO.Path]::GetFullPath(
  ((& git rev-parse --show-toplevel).Trim() -replace '/', '\')
)
$retiredCampaignTarget = [IO.Path]::GetFullPath(
  (Join-Path $parentWorktreePhysical `
    "external\official-openvino\2026-07-19")
).TrimEnd('\')
$existingOLines = @(
  @(& subst.exe) | Where-Object { $_ -match '^O:\\: => ' }
)
if ($existingOLines.Count -gt 1) {
  throw "More than one O: substitution was reported"
}
if ($existingOLines.Count -eq 1) {
  if ($existingOLines[0] -notmatch '^O:\\: => (?<target>.+)$') {
    throw "Unable to parse the existing O: substitution"
  }
  $actualOTarget = [IO.Path]::GetFullPath($Matches.target).TrimEnd('\')
  if (-not $actualOTarget.Equals(
      $expectedOTarget, [StringComparison]::OrdinalIgnoreCase)) {
    if (-not $actualOTarget.Equals(
        $retiredCampaignTarget,
        [StringComparison]::OrdinalIgnoreCase)) {
      throw "O: has an unknown collision at '$actualOTarget'"
    }
    if ((Test-Path -LiteralPath "O:\openvino-cpu-state-observer") -or
        (Test-Path -LiteralPath `
          "O:\openvino-cpu-state-observer.replacement.identity.json") -or
        $identity.destination_path -ne $expectedDerivedCore) {
      throw "Task 01C retirement is incomplete; refusing to remap O:"
    }
    & subst.exe O: /D
    if ($LASTEXITCODE -ne 0) {
      throw "Unable to retire the exact Task 01C campaign mapping"
    }
    & subst.exe O: $expectedOTarget
    if ($LASTEXITCODE -ne 0) {
      throw "Unable to map accepted derived core to O:"
    }
  }
} else {
  & subst.exe O: $expectedOTarget
  if ($LASTEXITCODE -ne 0) { throw "Unable to map derived core to O:" }
}

$substLines = @(& subst.exe)
$rMappings = @()
$oMappings = @()
foreach ($line in $substLines) {
  if ($line -match '^R:\\: => (?<target>.+)$') {
    $rMappings += [IO.Path]::GetFullPath($Matches.target).TrimEnd('\')
  }
  if ($line -match '^O:\\: => (?<target>.+)$') {
    $oMappings += [IO.Path]::GetFullPath($Matches.target).TrimEnd('\')
  }
}
if ($rMappings.Count -ne 1 -or
    -not $rMappings[0].Equals(
      $parentWorktreePhysical.TrimEnd('\'),
      [StringComparison]::OrdinalIgnoreCase
    ) -or
    $oMappings.Count -ne 1 -or
    -not $oMappings[0].Equals(
      $expectedOTarget,
      [StringComparison]::OrdinalIgnoreCase
    )) {
  throw "Task 2 R:/O: substitution identity is not exact"
}

$legacyExpected = [ordered]@{
  "task02-attempt-001" = [ordered]@{
    outcome = "safe HETERO configure blocker; not intended RED"
    files = [ordered]@{
      "task2-configure-red.json" = [ordered]@{
        bytes = 3434
        sha256 =
          "e5ce87fb234734693d61db6fbb32f79d2b5db6fe6c2ff1aa1c95f79c9df4457c"
      }
      "task2-configure-red.log" = [ordered]@{
        bytes = 13000
        sha256 =
          "5eca5dba8c004944c5191acea461396ebc5a9dd1839b47b7fdf119df6f9a4c05"
      }
    }
  }
  "task02-attempt-002" = [ordered]@{
    outcome =
      "successful old-contract configure then wrapper subst rejection; incomplete"
    files = [ordered]@{
      "task2-configure-red.json" = [ordered]@{
        bytes = 3402
        sha256 =
          "47803dbb03722289287d7a5e45e86054f133d39a53c8c4f6ff5146e02f8f9b35"
      }
      "task2-configure-red.log" = [ordered]@{
        bytes = 9234
        sha256 =
          "5f4e56928a4ed129fd229aa1488f9eb1a4b8118aeff9400a68c8c96eddc41d8d"
      }
    }
  }
  "task02-attempt-003" = [ordered]@{
    outcome =
      "PowerShell preimage bootstrap failure before RED-ready, guard launch, or source edit"
    files = [ordered]@{
      "diagnostic.stdout.log" = [ordered]@{
        bytes = 0
        sha256 =
          "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
      }
      "diagnostic.stderr.log" = [ordered]@{
        bytes = 574
        sha256 =
          "f7696a73d2e465870281987af6e480b9db0ce59f518b51daf5b783c7a0a18cea"
      }
      "failure.json" = [ordered]@{
        bytes = 1797
        sha256 =
          "7d824034142720079dc02a456bad05b12a03fbebe20a2d457145725ebef2fecc"
      }
    }
  }
}
$legacyRoot =
  "R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards"
foreach ($attemptName in $legacyExpected.Keys) {
  $attemptPath = Join-Path $legacyRoot $attemptName
  $actualNames = @(
    Get-ChildItem -LiteralPath $attemptPath -File -Force |
      ForEach-Object { $_.Name } | Sort-Object
  )
  $expectedNames = @($legacyExpected[$attemptName].files.Keys | Sort-Object)
  if ($actualNames.Count -ne $expectedNames.Count -or
      (Compare-Object $expectedNames $actualNames -CaseSensitive)) {
    throw "$attemptName does not contain its exact immutable file set"
  }
  foreach ($fileName in $legacyExpected[$attemptName].files.Keys) {
    $filePath = Join-Path $attemptPath $fileName
    $expected = $legacyExpected[$attemptName].files[$fileName]
    if ((Get-Item -LiteralPath $filePath).Length -ne $expected.bytes -or
        (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash.ToLowerInvariant() -ne
          $expected.sha256) {
      throw "$attemptName/$fileName drifted"
    }
  }
}

# Close attempt 004 from its final immutable sibling receipt before creating
# attempt 005. The receipt hash authenticates the complete historical record;
# the checks below independently re-read every manifest member from disk.
$attempt004Path = Join-Path $legacyRoot "task02-attempt-004"
$attempt004FailurePath = Join-Path `
  $legacyRoot "task02-attempt-004-failure\failure.json"
$attempt004FailureItem = Get-Item -LiteralPath $attempt004FailurePath -Force
if (($attempt004FailureItem.Attributes -band
      [IO.FileAttributes]::ReparsePoint) -ne 0 -or
    $attempt004FailureItem.Length -ne 13042 -or
    (Get-FileHash -LiteralPath $attempt004FailurePath -Algorithm SHA256).
      Hash.ToLowerInvariant() -cne
      "57701e2bb243e9cf294662772f9d1e7a42cd892e57b1545e4bde43be58b9b313") {
  throw "Attempt 004 final closure receipt drifted"
}
$attempt004Closure =
  Get-Content -LiteralPath $attempt004FailurePath -Raw |
    ConvertFrom-Json -ErrorAction Stop
if ($attempt004Closure.schema -cne
      "openvino-cpu-observer-task02-attempt-failure/v1" -or
    $attempt004Closure.attempt_id -cne "task02-attempt-004" -or
    $attempt004Closure.classification -cne
      "incomplete_after_valid_configure_before_build_guard_publication" -or
    $attempt004Closure.root_cause_status -cne "not_captured" -or
    $attempt004Closure.phase_state.source_edited -ne $false -or
    $attempt004Closure.attempt_evidence.file_count -ne 4 -or
    $attempt004Closure.attempt_evidence.canonical_manifest_sha256 -cne
      "94aea149cc38d826a62a8445eb6f19e907a003499f5927b5536619f54b4fc8c2" -or
    $attempt004Closure.diagnostic_evidence.directory_count -ne 11 -or
    $attempt004Closure.diagnostic_evidence.file_count -ne 21 -or
    $attempt004Closure.diagnostic_evidence.canonical_manifest_sha256 -cne
      "9c709913d23059def8ad31f0ec7d22b192907573cb0917fe395c1000adb551dd" -or
    $attempt004Closure.diagnostic_evidence.
      combined_with_attempt_004.directory_count -ne 12 -or
    $attempt004Closure.diagnostic_evidence.
      combined_with_attempt_004.file_count -ne 25 -or
    $attempt004Closure.diagnostic_evidence.
      combined_with_attempt_004.canonical_bytes -ne 2742 -or
    $attempt004Closure.diagnostic_evidence.
      combined_with_attempt_004.canonical_manifest_sha256 -cne
      "e8b3dc46d45ac8dc43ba3f8b6bdc061fcf0f8512d01f7243c0d5398c87321bb4") {
  throw "Attempt 004 closure receipt values are not exact"
}

$expectedAttempt004Files = [ordered]@{
  ".task2-build-red.log.tmp-uq_aegvp" = [ordered]@{
    bytes = 3437
    sha256 =
      "5a5cd73cb48671aca8325d3ba7bfb384c0f6a3f88ead9766a8c2d69d76fe8593"
  }
  "task2-configure-red.json" = [ordered]@{
    bytes = 3405
    sha256 =
      "82cd6c5a84a6578d5035ca054737269181ad633b59429dd50f9c12b535d3514a"
  }
  "task2-configure-red.log" = [ordered]@{
    bytes = 6834
    sha256 =
      "61c1fdce077a38941ab1d7b84e76d4837abbca640e77a3e494a7a35852475539"
  }
  "task2-configure-red.wrapper.json" = [ordered]@{
    bytes = 4030
    sha256 =
      "8d9ebd5c9744d8abf3dbd136dba9739c03ce34e0808e3020d20728f07653520b"
  }
}
$attempt004ActualNames = [string[]]@(
  Get-ChildItem -LiteralPath $attempt004Path -File -Force |
    ForEach-Object { $_.Name }
)
$attempt004ExpectedNames = [string[]]@($expectedAttempt004Files.Keys)
[Array]::Sort($attempt004ActualNames, [StringComparer]::Ordinal)
[Array]::Sort($attempt004ExpectedNames, [StringComparer]::Ordinal)
if ($attempt004ActualNames.Count -ne 4 -or
    (Compare-Object `
      $attempt004ExpectedNames $attempt004ActualNames -CaseSensitive)) {
  throw "Attempt 004 no longer contains exactly its four immutable files"
}
foreach ($fileName in $expectedAttempt004Files.Keys) {
  $filePath = Join-Path $attempt004Path $fileName
  $fileItem = Get-Item -LiteralPath $filePath -Force
  $expected = $expectedAttempt004Files[$fileName]
  if (($fileItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
      $fileItem.Length -ne $expected.bytes -or
      (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).
        Hash.ToLowerInvariant() -cne $expected.sha256) {
    throw "Attempt 004 immutable member drifted: $fileName"
  }
}

function Get-CanonicalManifestEvidence {
  param([Parameter(Mandatory = $true)][string[]]$Lines)
  $copy = [string[]]@($Lines)
  [Array]::Sort($copy, [StringComparer]::Ordinal)
  $manifestBytes = [Text.UTF8Encoding]::new($false, $true).GetBytes(
    [string]::Join("`n", $copy) + "`n"
  )
  $sha = [Security.Cryptography.SHA256]::Create()
  try {
    $manifestSha256 = (
      [BitConverter]::ToString($sha.ComputeHash($manifestBytes))
    ).Replace("-", "").ToLowerInvariant()
  } finally {
    $sha.Dispose()
  }
  [ordered]@{
    lines = $copy
    bytes = $manifestBytes.Length
    sha256 = $manifestSha256
  }
}

$combinedLines = [string[]]@(
  $attempt004Closure.diagnostic_evidence.canonical_lines |
    ForEach-Object { [string]$_ }
)
if ($combinedLines.Count -ne 25) {
  throw "Attempt 004 combined manifest does not contain 25 lines"
}
foreach ($line in $combinedLines) {
  if ($line -cnotmatch
      '^(?<relative>[^|]+)\|(?<bytes>[0-9]+)\|(?<sha>[0-9a-f]{64})$' -or
      $Matches.relative.Contains("\") -or
      [IO.Path]::IsPathRooted($Matches.relative) -or
      $Matches.relative.Split("/") -contains "..") {
    throw "Attempt 004 closure contains an unsafe canonical line"
  }
  $manifestMember = [IO.Path]::GetFullPath(
    [IO.Path]::Combine($legacyRoot, $Matches.relative.Replace("/", "\"))
  )
  if (-not $manifestMember.StartsWith(
      [IO.Path]::GetFullPath($legacyRoot).TrimEnd("\") + "\",
      [StringComparison]::OrdinalIgnoreCase
    )) {
    throw "Attempt 004 canonical member escaped the evidence root"
  }
  $manifestItem = Get-Item -LiteralPath $manifestMember -Force
  if (($manifestItem.Attributes -band
      [IO.FileAttributes]::ReparsePoint) -ne 0 -or
      $manifestItem.Length -ne [int64]$Matches.bytes -or
      (Get-FileHash -LiteralPath $manifestMember -Algorithm SHA256).
        Hash.ToLowerInvariant() -cne $Matches.sha) {
    throw "Attempt 004 canonical member drifted: $($Matches.relative)"
  }
}
$attempt004Lines = [string[]]@(
  $combinedLines | Where-Object { $_.StartsWith(
    "task02-attempt-004/", [StringComparison]::Ordinal
  ) }
)
$diagnosticLines = [string[]]@(
  $combinedLines | Where-Object { -not $_.StartsWith(
    "task02-attempt-004/", [StringComparison]::Ordinal
  ) }
)
$attempt004Manifest =
  Get-CanonicalManifestEvidence -Lines $attempt004Lines
$diagnosticManifest =
  Get-CanonicalManifestEvidence -Lines $diagnosticLines
$combinedManifest =
  Get-CanonicalManifestEvidence -Lines $combinedLines
if ($attempt004Manifest.lines.Count -ne 4 -or
    $attempt004Manifest.sha256 -cne
      "94aea149cc38d826a62a8445eb6f19e907a003499f5927b5536619f54b4fc8c2" -or
    $diagnosticManifest.lines.Count -ne 21 -or
    $diagnosticManifest.sha256 -cne
      "9c709913d23059def8ad31f0ec7d22b192907573cb0917fe395c1000adb551dd" -or
    $combinedManifest.bytes -ne 2742 -or
    $combinedManifest.sha256 -cne
      "e8b3dc46d45ac8dc43ba3f8b6bdc061fcf0f8512d01f7243c0d5398c87321bb4") {
  throw "Attempt 004 independent canonical-manifest recomputation failed"
}
$expectedDiagnosticDirectories = [string[]]@(
  "t2d-full-mp1",
  "t2d-full-nopdb",
  "t2d-ovcap1",
  "t2d-ovcap2",
  "t2d-ovdirect",
  "t2d-ovnoicf",
  "t2d-ovtrim1",
  "t2d-selected",
  "task02-attempt-004-diagnostic",
  "task02-attempt-004-diagnostic-selected",
  "task02-attempt-004-diagnostic-serial"
)
foreach ($directoryName in $expectedDiagnosticDirectories) {
  $directoryItem = Get-Item -LiteralPath (
    Join-Path $legacyRoot $directoryName
  ) -Force
  if (-not $directoryItem.PSIsContainer -or
      ($directoryItem.Attributes -band
        [IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw "Attempt 004 diagnostic directory is not exact: $directoryName"
  }
}
$actualDiagnosticDirectoryNames = [string[]]@(
  Get-ChildItem -LiteralPath $legacyRoot -Directory -Force |
    Where-Object {
      $_.Name -cmatch '^(?:t2d-|task02-attempt-004-diagnostic)'
    } |
    ForEach-Object { $_.Name }
)
$expectedDiagnosticDirectoryNames =
  [string[]]@($expectedDiagnosticDirectories)
[Array]::Sort(
  $actualDiagnosticDirectoryNames,
  [StringComparer]::Ordinal
)
[Array]::Sort(
  $expectedDiagnosticDirectoryNames,
  [StringComparer]::Ordinal
)
if ($actualDiagnosticDirectoryNames.Count -ne 11 -or
    (Compare-Object `
      $expectedDiagnosticDirectoryNames `
      $actualDiagnosticDirectoryNames `
      -CaseSensitive)) {
  throw "Attempt 004 diagnostic directory set is not exact"
}
$actualDiagnosticLines = [Collections.Generic.List[string]]::new()
foreach ($directoryName in $expectedDiagnosticDirectories) {
  $directoryPath = Join-Path $legacyRoot $directoryName
  foreach ($file in Get-ChildItem -LiteralPath $directoryPath -File -Recurse -Force) {
    if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
      throw "Attempt 004 diagnostic file is a reparse point: $($file.FullName)"
    }
    $relative = $file.FullName.Substring(
      [IO.Path]::GetFullPath($legacyRoot).TrimEnd("\").Length + 1
    ).Replace("\", "/")
    $actualDiagnosticLines.Add(
      "{0}|{1}|{2}" -f
        $relative,
        $file.Length,
        (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).
          Hash.ToLowerInvariant()
    )
  }
}
$actualAttempt004Lines = [Collections.Generic.List[string]]::new()
foreach ($fileName in $expectedAttempt004Files.Keys) {
  $expected = $expectedAttempt004Files[$fileName]
  $actualAttempt004Lines.Add(
    "task02-attempt-004/{0}|{1}|{2}" -f
      $fileName,
      $expected.bytes,
      $expected.sha256
  )
}
$actualDiagnosticManifest = Get-CanonicalManifestEvidence `
  -Lines ([string[]]$actualDiagnosticLines)
$actualAttempt004Manifest = Get-CanonicalManifestEvidence `
  -Lines ([string[]]$actualAttempt004Lines)
$actualCombinedManifest = Get-CanonicalManifestEvidence `
  -Lines ([string[]]@(
    @($actualDiagnosticLines) + @($actualAttempt004Lines)
  ))
if ($actualDiagnosticManifest.lines.Count -ne 21 -or
    $actualDiagnosticManifest.sha256 -cne
      "9c709913d23059def8ad31f0ec7d22b192907573cb0917fe395c1000adb551dd" -or
    $actualAttempt004Manifest.lines.Count -ne 4 -or
    $actualAttempt004Manifest.sha256 -cne
      "94aea149cc38d826a62a8445eb6f19e907a003499f5927b5536619f54b4fc8c2" -or
    $actualCombinedManifest.lines.Count -ne 25 -or
    $actualCombinedManifest.bytes -ne 2742 -or
    $actualCombinedManifest.sha256 -cne
      "e8b3dc46d45ac8dc43ba3f8b6bdc061fcf0f8512d01f7243c0d5398c87321bb4") {
  throw "Attempt 004 actual evidence differs from its closure manifests"
}
$attempt004Manifest = $actualAttempt004Manifest
$diagnosticManifest = $actualDiagnosticManifest
$combinedManifest = $actualCombinedManifest

$focusedSelected = $attempt004Closure.diagnostic_evidence.focused_selected_red
$capFeasibility =
  $attempt004Closure.diagnostic_evidence.working_set_cap_feasibility
if ($focusedSelected.valid -ne $true -or
    $focusedSelected.exit_code -ne 1 -or
    $focusedSelected.diagnostic -cne
      "error C1083: Cannot open include file: 'utils/state_allocations_dump.hpp': No such file or directory" -or
    $focusedSelected.minimum_available_ram_bytes -ne 7073488896 -or
    @($focusedSelected.survivor_pids_after_cleanup).Count -ne 0 -or
    $capFeasibility.valid -ne $true -or
    $capFeasibility.exit_code -ne 0 -or
    $capFeasibility.per_link_maximum_working_set_bytes -ne 4294967296 -or
    $capFeasibility.observed_working_set_flags -ne 6 -or
    $capFeasibility.cap_verified -ne $true -or
    @($capFeasibility.survivor_pids_after_cleanup).Count -ne 0) {
  throw "Attempt 004 diagnostic feasibility anchors are not exact"
}
foreach ($historicalPathBinding in @(
    $capFeasibility.helper,
    $capFeasibility.link_command_tlog
  )) {
  $historicalPath = if ([IO.Path]::IsPathRooted(
      [string]$historicalPathBinding.path
    )) {
    [string]$historicalPathBinding.path
  } else {
    Join-Path $parentWorktreePhysical (
      [string]$historicalPathBinding.path -replace '/', '\'
    )
  }
  if ((Get-Item -LiteralPath $historicalPath -Force).Length -ne
        [int64]$historicalPathBinding.bytes -or
      (Get-FileHash -LiteralPath $historicalPath -Algorithm SHA256).
        Hash.ToLowerInvariant() -cne
        [string]$historicalPathBinding.sha256) {
    throw "Attempt 004 historical cap evidence drifted: $historicalPath"
  }
}
foreach ($outputName in @("openvino.dll", "openvino.lib", "openvino.exp")) {
  $outputBinding = $capFeasibility.outputs.PSObject.Properties[$outputName].Value
  $outputPath = Join-Path "O:\bin\intel64\Release" $outputName
  if ((Get-Item -LiteralPath $outputPath -Force).Length -ne
        [int64]$outputBinding.bytes -or
      (Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).
        Hash.ToLowerInvariant() -cne [string]$outputBinding.sha256) {
    throw "Attempt 004 historical cap output drifted: $outputName"
  }
}
$attempt004ClosureBinding = [ordered]@{
  receipt = [ordered]@{
    path =
      "experiments/raw-results/openvino-turboquant/2026-07-28/guards/task02-attempt-004-failure/failure.json"
    bytes = 13042
    sha256 =
      "57701e2bb243e9cf294662772f9d1e7a42cd892e57b1545e4bde43be58b9b313"
  }
  attempt_file_count = 4
  attempt_manifest_sha256 = $attempt004Manifest.sha256
  diagnostic_directory_count = 11
  diagnostic_file_count = 21
  diagnostic_manifest_sha256 = $diagnosticManifest.sha256
  combined_file_count = 25
  combined_manifest_bytes = $combinedManifest.bytes
  combined_manifest_sha256 = $combinedManifest.sha256
  root_cause_status = "not_captured"
  selected_red = [ordered]@{
    diagnostic = [string]$focusedSelected.diagnostic
    valid = $true
    exit_code = 1
  }
  cap_feasibility = [ordered]@{
    maximum_working_set_bytes = 4294967296
    set_flags = 4
    readback_flags = 6
    valid = $true
    scope = "historical feasibility only; not Task 2 GREEN"
  }
}

$buildCachePath = "C:\ov-build\state-observer\CMakeCache.txt"
$buildRootItem = Get-Item -LiteralPath "C:\ov-build\state-observer" -Force
$buildReparsePoints = @(
  Get-ChildItem -LiteralPath "C:\ov-build\state-observer" -Recurse -Force |
    Where-Object {
      ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0
    }
)
if (($buildRootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
    $buildReparsePoints.Count -ne 0) {
  throw "Reusable state-observer build tree contains a reparse point"
}
$buildCache = Get-Content -LiteralPath $buildCachePath
$requiredBuildCache = @(
  "CMAKE_HOME_DIRECTORY:INTERNAL=O:/",
  "CMAKE_GENERATOR:INTERNAL=Visual Studio 18 2026",
  "CMAKE_GENERATOR_PLATFORM:INTERNAL=x64",
  "ENABLE_DEBUG_CAPS:BOOL=ON",
  "ENABLE_CPU_DEBUG_CAPS:BOOL=ON",
  "ENABLE_TESTS:BOOL=ON",
  "ENABLE_FUNCTIONAL_TESTS:BOOL=OFF",
  "ENABLE_HETERO:BOOL=OFF",
  "ENABLE_SAMPLES:BOOL=OFF",
  "ENABLE_PYTHON:BOOL=OFF",
  "ENABLE_INTEL_GPU:BOOL=OFF",
  "ENABLE_INTEL_NPU:BOOL=OFF",
  "ENABLE_OV_ONNX_FRONTEND:BOOL=OFF",
  "ENABLE_OV_PADDLE_FRONTEND:BOOL=OFF",
  "ENABLE_OV_TF_FRONTEND:BOOL=OFF",
  "ENABLE_LTO:INTERNAL=OFF",
  "BUILD_SHARED_LIBS:BOOL=ON"
)
foreach ($required in $requiredBuildCache) {
  if ($buildCache -cnotcontains $required) {
    throw "Reusable build cache is missing '$required'"
  }
}

$boundary["substitutions"] = [ordered]@{
  R = $rMappings[0]
  O = $oMappings[0]
}
$boundary["legacy_attempts"] = $legacyExpected
$boundary["attempt004_closure"] = $attempt004ClosureBinding
$boundary["reusable_build_cache"] = [ordered]@{
  path = $buildCachePath
  sha256 = (
    Get-FileHash -LiteralPath $buildCachePath -Algorithm SHA256
  ).Hash.ToLowerInvariant()
  required_entries = $requiredBuildCache
  root_reparse = $false
  nested_reparse_count = 0
}
$boundaryPath = [IO.Path]::Combine(
  $parentWorktreePhysical,
  ".superpowers\sdd\task02-attempt005-boundary.json"
)
$boundaryTemp =
  $boundaryPath + "." + [Guid]::NewGuid().ToString("N") + ".tmp"
$utf8NoBom = [Text.UTF8Encoding]::new($false, $true)
[IO.File]::WriteAllText(
  $boundaryTemp,
  ($boundary | ConvertTo-Json -Depth 12) + "`n",
  $utf8NoBom
)
if (Test-Path -LiteralPath $boundaryPath) {
  [IO.File]::Delete($boundaryTemp)
  throw "Fresh attempt 005 boundary already exists; preserve it and stop"
}
[IO.File]::Move($boundaryTemp, $boundaryPath)
$boundaryCheck = Get-Content -LiteralPath $boundaryPath -Raw | ConvertFrom-Json
$boundaryCompletionMessages =
  $boundaryCheck.link_cap_supervisor.completion_messages
$boundaryCompletionReconciled = $boundaryCheck.link_cap_supervisor.completion_delivery_reconciled_to_job_accounting
if ($boundaryCheck.schema -cne
      "openvino-cpu-observer-task02-boundary/v4" -or
    $boundaryCheck.parent_plan_commit -ne $parentHead -or
    $boundaryCheck.task02_plan_blob -ne $taskPlanBlob -or
    $boundaryCheck.accepted_guard_commit -ne $guardCommit -or
    $boundaryCheck.controller_runtime.python_sha256 -ne $pythonSha256 -or
    $boundaryCheck.controller_process.script_sha256 -cne
      $controllerScriptSha256 -or
    [int64]$boundaryCheck.controller_process.start_filetime_utc -ne
      [int64]$controllerStartFileTimeUtc -or
    [int64]$boundaryCheck.controller_process.creation_filetime_utc -ne
      [int64]$controllerCreationFileTimeUtc -or
    $boundaryCheck.controller_process.command_line_sha256 -cne
      $controllerCommandLineSha256 -or
    -not [IO.Path]::GetFullPath(
      [string]$boundaryCheck.controller_process.working_directory
    ).Equals(
      $parentWorktreePhysical,
      [StringComparison]::OrdinalIgnoreCase
    ) -or
    $boundaryCheck.controller_job.name -cne $controllerJobName -or
    [uint32]$boundaryCheck.controller_job.limit_flags -ne 8192 -or
    [uint32]$boundaryCheck.controller_job.create_process_flags -ne
      134742020 -or
    [uint32]$boundaryCheck.controller_job.proc_thread_attribute_job_list -ne
      131085 -or
    [uint32]$boundaryCheck.controller_job.startupinfoex_size -ne 112 -or
    [uint32]$boundaryCheck.controller_job.size_probe_error -ne 122 -or
    [bool]$boundaryCheck.controller_job.assigned_at_create -ne $true -or
    [bool]$boundaryCheck.controller_job.job_member_before_resume -ne $true -or
    [uint32]$boundaryCheck.controller_job.resume_previous_suspend_count -ne 1 -or
    [bool]$boundaryCheck.controller_job.duplicate_same_access -ne $true -or
    [bool]$boundaryCheck.controller_job.duplicate_inheritable -ne $false -or
    [bool]$boundaryCheck.controller_job.controller_owned_handle -ne $true -or
    [bool]$boundaryCheck.controller_job.launcher_handle_closed_before_publish -ne
      $true -or
    -not [IO.Path]::GetFullPath(
      [string]$boundaryCheck.controller_job.launcher_identity.path
    ).Equals(
      $launcherScriptPath,
      [StringComparison]::OrdinalIgnoreCase
    ) -or
    [int64]$boundaryCheck.controller_job.launcher_identity.bytes -ne
      $expectedLauncherScriptBytes -or
    [string]$boundaryCheck.controller_job.launcher_identity.sha256 -cne
      $expectedLauncherScriptSha256 -or
    [bool]$boundaryCheck.controller_job.launcher_identity.reparse -ne
      $false -or
    [string]$boundaryCheck.controller_job.launcher_identity.authentication -cne
      "direct-prelaunch-parameters-plus-launcher-self-readback" -or
    $boundaryCheck.controller_job.timeout_action -cne
      "TerminateJobObject" -or
    [uint32]$boundaryCheck.controller_job.timeout_exit_code -ne 125 -or
    [int64]$boundaryCheck.controller_job.job_ready.bytes -ne
      [int64]$jobReadyBytes.Length -or
    $boundaryCheck.controller_job.job_ready.sha256 -cne
      $controllerJobReadySha256 -or
    @($boundaryCheck.control_scripts.PSObject.Properties).Count -ne 4 -or
    $boundaryCheck.control_scripts.launcher.sha256 -cne
      $expectedLauncherScriptSha256 -or
    [int64]$boundaryCheck.control_scripts.launcher.bytes -ne
      $expectedLauncherScriptBytes -or
    -not [IO.Path]::GetFullPath(
      [string]$boundaryCheck.control_scripts.launcher.path
    ).Equals(
      $launcherScriptPath,
      [StringComparison]::OrdinalIgnoreCase
    ) -or
    [bool]$boundaryCheck.control_scripts.launcher.reparse -ne $false -or
    [string]$boundaryCheck.controller_job.launcher_identity.sha256 -cne
      [string]$boundaryCheck.control_scripts.launcher.sha256 -or
    [int64]$boundaryCheck.controller_job.launcher_identity.bytes -ne
      [int64]$boundaryCheck.control_scripts.launcher.bytes -or
    $boundaryCheck.control_scripts.controller.sha256 -cne
      $controlScripts.controller.sha256 -or
    [int64]$boundaryCheck.control_scripts.controller.bytes -ne
      [int64]$controlScripts.controller.bytes -or
    $boundaryCheck.control_scripts.green_signal.sha256 -cne
      $controlScripts.green_signal.sha256 -or
    [int64]$boundaryCheck.control_scripts.green_signal.bytes -ne
      [int64]$controlScripts.green_signal.bytes -or
    $boundaryCheck.control_scripts.link_cap_supervisor.sha256 -cne
      $controlScripts.link_cap_supervisor.sha256 -or
    [int64]$boundaryCheck.control_scripts.link_cap_supervisor.bytes -ne
      [int64]$controlScripts.link_cap_supervisor.bytes -or
    $boundaryCheck.resume_fixture.sha256 -ne
      "a1e18eda017edec7f473dd8c885e172e7379e2f92f38894fda627dce1c6f608b" -or
    $boundaryCheck.attempt004_closure.receipt.sha256 -cne
      "57701e2bb243e9cf294662772f9d1e7a42cd892e57b1545e4bde43be58b9b313" -or
    $boundaryCheck.attempt004_closure.combined_manifest_sha256 -cne
      "e8b3dc46d45ac8dc43ba3f8b6bdc061fcf0f8512d01f7243c0d5398c87321bb4" -or
    $boundaryCheck.link_cap_supervisor.bytes -ne 38077 -or
    $boundaryCheck.link_cap_supervisor.sha256 -cne
      "fd005d235b27a532ac5d0536ccb5b429e9c4f554efcd1f6ed5c9a5cf08e0f90b" -or
    $boundaryCheck.link_cap_supervisor.msbuild_sha256 -cne
      "106cac9dc67569fe80102cb4a11f49dc48ad44529cabcaf2259e1aa74ad19bec" -or
    [bool]$boundaryCheck.link_cap_supervisor.nested_job -ne $true -or
    [uint32]$boundaryCheck.link_cap_supervisor.job_limit_flags -ne 8960 -or
    [uint32]$boundaryCheck.link_cap_supervisor.set_flags -ne 4 -or
    [uint32]$boundaryCheck.link_cap_supervisor.readback_flags -ne 6 -or
    [uint64]$boundaryCheck.link_cap_supervisor.process_memory_limit_bytes -ne
      6442450944 -or
    [uint64]$boundaryCheck.link_cap_supervisor.job_memory_limit_bytes -ne
      7516192768 -or
    [uint32]$boundaryCheck.link_cap_supervisor.create_process_flags -ne
      134217732 -or
    [uint32]$boundaryCompletionMessages.active_process_zero -ne 4 -or
    [uint32]$boundaryCompletionMessages.new_process -ne 6 -or
    [uint32]$boundaryCompletionMessages.exit_process -ne 7 -or
    [uint32]$boundaryCompletionMessages.abnormal_exit_process -ne 8 -or
    [bool]$boundaryCompletionReconciled -ne $true -or
    $boundaryCheck.link_cap_supervisor.application_timing -cne
      "commit limits before suspended-root assignment/resume; linker working-set cap after exact nested-Job NEW_PROCESS" -or
    @($boundaryCheck.link_cap_supervisor.blocking_process_names).Count -ne 7 -or
    @($boundaryCheck.link_cap_supervisor.child_commands.Unit).Count -ne 12 -or
    @($boundaryCheck.link_cap_supervisor.child_commands.Plugin).Count -ne 12 -or
    $boundaryCheck.substitutions.R -ne $rMappings[0] -or
    $boundaryCheck.substitutions.O -ne $oMappings[0] -or
    $boundaryCheck.reusable_build_cache.sha256 -ne
      $boundary["reusable_build_cache"]["sha256"]) {
  throw "Regenerated Task 2 boundary did not round-trip exact anchors"
}
```

Keep `O:` mapped through Tasks 2-6. Task 7 owns exact-map revalidation against
the canonical identity destination and removal after every build/test process
has stopped. Its older literal in-tree comparison must be amended to this
accepted Task 01C physical destination before Task 7 executes.

## Step 2: Revalidate the complete RED test file

The crash-resume boundary already contains
`O:\src\plugins\intel_cpu\tests\unit\state_allocations_dump_test.cpp`. Do not
rewrite or normalize it. Reconfirm its 22,516-byte length and SHA-256
`a1e18eda017edec7f473dd8c885e172e7379e2f92f38894fda627dce1c6f608b`;
the exact accepted body is reproduced below for review:

```cpp
// Copyright (C) 2018-2026 Intel Corporation
// SPDX-License-Identifier: Apache-2.0
//

#include <gtest/gtest.h>

#include <cstdint>
#include <functional>
#include <limits>
#include <memory>
#include <optional>
#include <string>
#include <utility>
#include <vector>

#include "cpu_memory.h"
#include "memory_desc/cpu_blocked_memory_desc.h"
#include "memory_state.h"
#include "openvino/core/except.hpp"
#include "openvino/core/partial_shape.hpp"
#include "utils/plain_tensor.hpp"
#include "utils/state_allocations_dump.hpp"

#ifdef CPU_DEBUG_CAPS

namespace ov::intel_cpu {
namespace {

const SnapshotContext kContext{
    4242,
    1,
    "0123456789abcdef0123456789abcdef",
    "query_state",
    "post_infer",
    "CPU",
};

ObservedAllocation allocation(std::string state,
                              StateMemoryRole role,
                              AllocationOwnerDomain owner_domain,
                              size_t owner,
                              size_t active,
                              std::optional<size_t> reserved) {
    return {
        std::move(state),
        role == StateMemoryRole::KV || role == StateMemoryRole::BEAM ||
                role == StateMemoryRole::SCALE_ZP
            ? "VariableStateKVcache"
            : "VariableStateDoubleBuffer",
        role,
        owner_domain,
        owner,
        "f16",
        {1, 2, 3, 16},
        {0, 1, 2, 3},
        {96, 48, 16, 1},
        active,
        reserved,
        true,
        false,
    };
}

template <typename Callable>
void expect_ov_exception(Callable&& callable, const std::string& fragment) {
    try {
        callable();
        FAIL() << "Expected ov::Exception containing: " << fragment;
    } catch (const ov::Exception& error) {
        EXPECT_NE(std::string(error.what()).find(fragment), std::string::npos)
            << error.what();
    } catch (...) {
        FAIL() << "Expected ov::Exception containing: " << fragment;
    }
}

MemoryPtr make_memory(ov::element::Type precision, const Shape& shape) {
    dnnl::engine engine(dnnl::engine::kind::cpu, 0);
    auto descriptor = std::make_shared<CpuBlockedMemoryDesc>(precision, shape);
    return std::make_shared<Memory>(engine, descriptor);
}

std::shared_ptr<VariableStateKVcache> make_kv_state(const std::string& name) {
    const Shape dynamic_shape(ov::PartialShape{-1, -1, -1, -1});
    auto external = std::make_shared<CpuBlockedMemoryDesc>(
        ov::element::f16,
        dynamic_shape);
    auto internal = std::make_shared<CpuBlockedMemoryDesc>(
        ov::element::f16,
        dynamic_shape);
    return std::make_shared<VariableStateKVcache>(
        name,
        external,
        internal,
        false,
        0);
}

class UnsupportedMemoryBlock final : public IMemoryBlock {
public:
    void* getRawPtr() const noexcept override {
        return nullptr;
    }
    void setExtBuff(void*, size_t) override {}
    bool resize(size_t) override {
        return false;
    }
    bool hasExtBuffer() const noexcept override {
        return false;
    }
};

class UnsupportedVariableState final : public IVariableState {
public:
    UnsupportedVariableState() : IVariableState("unsupported") {}
    void commit() override {}
    MemoryPtr input_mem() override {
        return nullptr;
    }
    MemoryPtr output_mem() override {
        return nullptr;
    }
    MemoryDescPtr internal_desc() const override {
        return nullptr;
    }
    bool is_reset_state() const override {
        return false;
    }
};

TEST(StateAllocationsDump, ReportsRetainedCapacityOnlyForReuseOwner) {
    DnnlMemoryBlock supported(std::make_unique<MemoryBlockWithReuse>());
    ASSERT_TRUE(supported.getAllocatedSize().has_value());
    EXPECT_EQ(*supported.getAllocatedSize(), 0u);
    EXPECT_TRUE(supported.resize(192));
    ASSERT_TRUE(supported.getAllocatedSize().has_value());
    EXPECT_EQ(*supported.getAllocatedSize(), 192u);

    DnnlMemoryBlock unsupported(std::make_unique<UnsupportedMemoryBlock>());
    EXPECT_FALSE(unsupported.getAllocatedSize().has_value());
}

TEST(StateAllocationsDump, DeduplicatesAliasedInputAndOutputBacking) {
    const auto snapshot = build_state_allocation_snapshot(
        {
            allocation(
                "past_key",
                StateMemoryRole::INPUT,
                AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                40,
                192,
                192),
            allocation(
                "past_key",
                StateMemoryRole::OUTPUT,
                AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                40,
                192,
                192),
        },
        1,
        kContext);
    ASSERT_EQ(snapshot.records.size(), 2u);
    ASSERT_TRUE(snapshot.records[1].aliases_block_ordinal.has_value());
    EXPECT_EQ(
        *snapshot.records[1].aliases_block_ordinal,
        snapshot.records[0].block_ordinal);
    EXPECT_EQ(snapshot.unique_reserved_bytes, 192u);
    EXPECT_EQ(snapshot.total_physical_state_bytes, 192u);
    ASSERT_EQ(snapshot.state_totals.size(), 1u);
    EXPECT_EQ(snapshot.state_totals[0].active_descriptor_bytes, 384u);
    EXPECT_EQ(snapshot.state_totals[0].total_physical_state_bytes, 192u);
}

TEST(StateAllocationsDump, ReportsBothDoubleBuffersWithoutAssumingEqualCapacity) {
    const auto snapshot = build_state_allocation_snapshot(
        {
            allocation(
                "past_value",
                StateMemoryRole::INPUT,
                AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                11,
                64,
                64),
            allocation(
                "past_value",
                StateMemoryRole::OUTPUT,
                AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                12,
                128,
                128),
        },
        2,
        kContext);
    EXPECT_EQ(snapshot.unique_reserved_bytes, 192u);
    EXPECT_EQ(snapshot.total_physical_state_bytes, 192u);
    EXPECT_FALSE(snapshot.records[0].aliases_block_ordinal.has_value());
    EXPECT_FALSE(snapshot.records[1].aliases_block_ordinal.has_value());
}

TEST(StateAllocationsDump, UnsupportedBlockIsUnknownNotDescriptorGuess) {
    const auto snapshot = build_state_allocation_snapshot(
        {
            allocation(
                "past_key",
                StateMemoryRole::KV,
                AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                9,
                192,
                std::nullopt),
        },
        3,
        kContext);
    ASSERT_EQ(snapshot.records.size(), 1u);
    EXPECT_FALSE(snapshot.records.front().reserved_backing_bytes.has_value());
    expect_ov_exception(
        [&] {
            static_cast<void>(serialize_state_allocation_snapshot(snapshot));
        },
        "unknown retained capacity");
}

TEST(StateAllocationsDump, AddsOwnedBeamAndScaleZpExactlyOnce) {
    auto beam = allocation(
        "past_key",
        StateMemoryRole::BEAM,
        AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
        3,
        12,
        32);
    beam.element_type = "i32";
    auto scale = allocation(
        "past_key",
        StateMemoryRole::SCALE_ZP,
        AllocationOwnerDomain::PLAIN_TENSOR_OWNER,
        4,
        16,
        64);
    scale.element_type = "f32";
    const auto snapshot = build_state_allocation_snapshot(
        {
            allocation(
                "past_key",
                StateMemoryRole::KV,
                AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                1,
                96,
                128),
            beam,
            scale,
        },
        4,
        kContext);
    EXPECT_EQ(snapshot.unique_reserved_bytes, 128u);
    EXPECT_EQ(snapshot.beam_reserved_bytes, 32u);
    EXPECT_EQ(snapshot.scale_zp_reserved_bytes, 64u);
    EXPECT_EQ(snapshot.total_physical_state_bytes, 224u);
    ASSERT_EQ(snapshot.state_totals.size(), 1u);
    EXPECT_EQ(snapshot.state_totals[0].unique_reserved_backing_bytes, 128u);
    EXPECT_EQ(snapshot.state_totals[0].beam_reserved_bytes, 32u);
    EXPECT_EQ(snapshot.state_totals[0].scale_zp_reserved_bytes, 64u);
    EXPECT_EQ(snapshot.state_totals[0].total_physical_state_bytes, 224u);
}

TEST(StateAllocationsDump, RejectsCheckedAdditionOverflow) {
    expect_ov_exception(
        [] {
            static_cast<void>(build_state_allocation_snapshot(
                {
                    allocation(
                        "past_key",
                        StateMemoryRole::INPUT,
                        AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                        1,
                        std::numeric_limits<size_t>::max(),
                        std::numeric_limits<size_t>::max()),
                    allocation(
                        "past_key",
                        StateMemoryRole::OUTPUT,
                        AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                        2,
                        1,
                        1),
                },
                5,
                kContext));
        },
        "checked addition overflow");
}

TEST(StateAllocationsDump, RejectsEmptyStateName) {
    expect_ov_exception(
        [] {
            static_cast<void>(build_state_allocation_snapshot(
                {
                    allocation(
                        "",
                        StateMemoryRole::INPUT,
                        AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                        1,
                        1,
                        1),
                },
                6,
                kContext));
        },
        "state_name must be non-empty");
}

TEST(StateAllocationsDump, RejectsDuplicateLogicalStateRole) {
    expect_ov_exception(
        [] {
            static_cast<void>(build_state_allocation_snapshot(
                {
                    allocation(
                        "past_key",
                        StateMemoryRole::INPUT,
                        AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                        1,
                        1,
                        1),
                    allocation(
                        "past_key",
                        StateMemoryRole::INPUT,
                        AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                        2,
                        1,
                        1),
                },
                7,
                kContext));
        },
        "duplicate logical-state declaration");
}

TEST(StateAllocationsDump, RejectsInconsistentStateClass) {
    auto second = allocation(
        "past_key",
        StateMemoryRole::OUTPUT,
        AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
        2,
        1,
        1);
    second.state_class = "VariableStateSingleBuffer";
    expect_ov_exception(
        [&] {
            static_cast<void>(build_state_allocation_snapshot(
                {
                    allocation(
                        "past_key",
                        StateMemoryRole::INPUT,
                        AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                        1,
                        1,
                        1),
                    second,
                },
                8,
                kContext));
        },
        "inconsistent state class");
}

TEST(StateAllocationsDump, RejectsZeroPid) {
    auto context = kContext;
    context.pid = 0;
    expect_ov_exception(
        [&] {
            static_cast<void>(build_state_allocation_snapshot({}, 9, context));
        },
        "pid must be positive");
}

TEST(StateAllocationsDump, RejectsZeroObserverRequestId) {
    auto context = kContext;
    context.observer_request_id = 0;
    expect_ov_exception(
        [&] {
            static_cast<void>(build_state_allocation_snapshot({}, 10, context));
        },
        "observer_request_id must be positive");
}

TEST(StateAllocationsDump, RejectsInvalidCorrelationId) {
    auto context = kContext;
    context.correlation_id = "ABC";
    expect_ov_exception(
        [&] {
            static_cast<void>(build_state_allocation_snapshot({}, 11, context));
        },
        "correlation_id must be 32 lowercase hexadecimal characters");
}

TEST(StateAllocationsDump, RejectsUnknownTrigger) {
    auto context = kContext;
    context.trigger = "infer";
    expect_ov_exception(
        [&] {
            static_cast<void>(build_state_allocation_snapshot({}, 12, context));
        },
        "trigger must be query_state");
}

TEST(StateAllocationsDump, RejectsEmptyAndUnknownPhase) {
    for (const std::string phase : {"", "failed"}) {
        auto context = kContext;
        context.phase = phase;
        expect_ov_exception(
            [&] {
                static_cast<void>(
                    build_state_allocation_snapshot({}, 13, context));
            },
            "phase is not allowed");
    }
}

TEST(StateAllocationsDump, RejectsNonCpuObserverPluginDevice) {
    auto context = kContext;
    context.observer_plugin_device = "AUTO:CPU";
    expect_ov_exception(
        [&] {
            static_cast<void>(build_state_allocation_snapshot({}, 14, context));
        },
        "observer_plugin_device must be CPU");
}

TEST(StateAllocationsDump, RejectsInvalidUtf8AndControlCharacters) {
    auto invalid_utf8 = allocation(
        std::string("past_") + static_cast<char>(0xC3) + static_cast<char>(0x28),
        StateMemoryRole::INPUT,
        AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
        1,
        1,
        1);
    expect_ov_exception(
        [&] {
            static_cast<void>(
                build_state_allocation_snapshot({invalid_utf8}, 15, kContext));
        },
        "state_name must be valid UTF-8 without controls");

    auto control = allocation(
        "past_\nkey",
        StateMemoryRole::INPUT,
        AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
        1,
        1,
        1);
    expect_ov_exception(
        [&] {
            static_cast<void>(
                build_state_allocation_snapshot({control}, 16, kContext));
        },
        "state_name must be valid UTF-8 without controls");
}

TEST(StateAllocationsDump, RejectsAliasCapacityChange) {
    expect_ov_exception(
        [] {
            static_cast<void>(build_state_allocation_snapshot(
                {
                    allocation(
                        "past_key",
                        StateMemoryRole::INPUT,
                        AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                        8,
                        64,
                        64),
                    allocation(
                        "past_key",
                        StateMemoryRole::OUTPUT,
                        AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                        8,
                        64,
                        128),
                },
                17,
                kContext));
        },
        "alias changes retained capacity");
}

TEST(StateAllocationsDump, RejectsExternallyBackedDnnlAllocation) {
    auto external = allocation(
        "past_key",
        StateMemoryRole::INPUT,
        AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
        1,
        64,
        64);
    external.owned_backing = false;
    external.external_backing = true;
    expect_ov_exception(
        [&] {
            static_cast<void>(
                build_state_allocation_snapshot({external}, 18, kContext));
        },
        "external DNNL backing is not physical observer evidence");
}

TEST(StateAllocationsDump, RejectsUnsupportedStateSubclass) {
    std::vector<MemStatePtr> states{
        std::make_shared<UnsupportedVariableState>()};
    expect_ov_exception(
        [&] {
            static_cast<void>(capture_state_allocations(states, 19, kContext));
        },
        "unsupported CPU variable state subclass");
}

TEST(StateAllocationsDump, BeamAliasesDeduplicateGloballyAcrossKeyAndValue) {
    auto key = allocation(
        "past_key",
        StateMemoryRole::BEAM,
        AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
        50,
        32,
        32);
    key.element_type = "i32";
    auto value = key;
    value.state_name = "past_value";
    const auto snapshot =
        build_state_allocation_snapshot({key, value}, 20, kContext);
    EXPECT_EQ(snapshot.beam_reserved_bytes, 32u);
    EXPECT_EQ(snapshot.total_physical_state_bytes, 32u);
    ASSERT_TRUE(snapshot.records[1].aliases_block_ordinal.has_value());
    EXPECT_EQ(
        *snapshot.records[1].aliases_block_ordinal,
        snapshot.records[0].block_ordinal);
}

TEST(StateAllocationsDump, CaptureCoversDoubleAndSingleBufferOwnership) {
    auto first = make_memory(ov::element::f16, Shape{1, 2, 2, 4});
    auto second = make_memory(ov::element::f16, Shape{1, 2, 2, 8});
    auto state = std::make_shared<VariableStateDoubleBuffer>(
        "past_key",
        first,
        second,
        first->getDescPtr());
    const auto snapshot =
        capture_state_allocations({state}, 21, kContext);
    ASSERT_EQ(snapshot.records.size(), 2u);
    EXPECT_FALSE(snapshot.records[0].aliases_block_ordinal.has_value());
    EXPECT_FALSE(snapshot.records[1].aliases_block_ordinal.has_value());
    EXPECT_EQ(snapshot.unique_reserved_bytes, 96u);

    auto single_memory =
        make_memory(ov::element::f16, Shape{1, 2, 2, 4});
    auto single = std::make_shared<VariableStateSingleBuffer>(
        "past_value",
        single_memory,
        single_memory->getDescPtr());
    const auto single_snapshot =
        capture_state_allocations({single}, 210, kContext);
    ASSERT_EQ(single_snapshot.records.size(), 2u);
    EXPECT_FALSE(
        single_snapshot.records[0].aliases_block_ordinal.has_value());
    ASSERT_TRUE(
        single_snapshot.records[1].aliases_block_ordinal.has_value());
    EXPECT_EQ(
        *single_snapshot.records[1].aliases_block_ordinal,
        single_snapshot.records[0].block_ordinal);
    EXPECT_EQ(single_snapshot.unique_reserved_bytes, 32u);
    EXPECT_EQ(single_snapshot.total_physical_state_bytes, 32u);
}

TEST(StateAllocationsDump, CaptureDeduplicatesSharedBeamAndScaleOwners) {
    auto key = make_kv_state("past_key");
    auto value = make_kv_state("past_value");
    key->assign_internal_state(
        make_memory(ov::element::f16, Shape{1, 2, 2, 4}));
    value->assign_internal_state(
        make_memory(ov::element::f16, Shape{1, 2, 2, 4}));

    auto shared_beam = make_memory(ov::element::i32, Shape{1, 8});
    key->assign_hidden_state(shared_beam);
    value->assign_hidden_state(shared_beam);

    PlainTensor shared_scale;
    shared_scale.resize<float>({2, 2});
    key->set_scale_zp(shared_scale);
    value->set_scale_zp(shared_scale);

    const auto snapshot =
        capture_state_allocations({key, value}, 22, kContext);
    EXPECT_EQ(snapshot.unique_reserved_bytes, 64u);
    EXPECT_EQ(snapshot.beam_reserved_bytes, 32u);
    EXPECT_EQ(snapshot.scale_zp_reserved_bytes, 16u);
    EXPECT_EQ(snapshot.total_physical_state_bytes, 112u);

    size_t beam_aliases = 0;
    size_t scale_aliases = 0;
    for (const auto& record : snapshot.records) {
        if (record.role == StateMemoryRole::BEAM &&
            record.aliases_block_ordinal.has_value()) {
            ++beam_aliases;
        }
        if (record.role == StateMemoryRole::SCALE_ZP &&
            record.aliases_block_ordinal.has_value()) {
            ++scale_aliases;
        }
    }
    EXPECT_EQ(beam_aliases, 1u);
    EXPECT_EQ(scale_aliases, 1u);
}

TEST(StateAllocationsDump, CaptureSkipsNonOwningScaleView) {
    auto state = make_kv_state("past_key");
    state->assign_internal_state(
        make_memory(ov::element::f16, Shape{1, 2, 2, 4}));
    state->assign_hidden_state(
        make_memory(ov::element::i32, Shape{1, 8}));

    float external_scale[4] = {};
    PlainTensor view;
    view.resize<float>({2, 2}, external_scale);
    ASSERT_EQ(view.m_capacity, 0u);
    state->set_scale_zp(view);

    const auto snapshot =
        capture_state_allocations({state}, 23, kContext);
    for (const auto& record : snapshot.records) {
        EXPECT_NE(record.role, StateMemoryRole::SCALE_ZP);
    }
    EXPECT_EQ(snapshot.scale_zp_reserved_bytes, 0u);
}

TEST(StateAllocationsDump, SerializesDeterministicCompleteBoundedJson) {
    const auto snapshot = build_state_allocation_snapshot(
        {
            allocation(
                "past_key",
                StateMemoryRole::INPUT,
                AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                1,
                192,
                192),
        },
        24,
        kContext);
    const auto first = serialize_state_allocation_snapshot(snapshot);
    const auto second = serialize_state_allocation_snapshot(snapshot);
    EXPECT_EQ(first, second);
    EXPECT_LE(first.size(), kMaxStateAllocationSnapshotBytes);
    EXPECT_NE(first.find("\"schema_version\":1"), std::string::npos);
    EXPECT_NE(first.find("\"sequence\":24"), std::string::npos);
    EXPECT_NE(first.find("\"owner_domain\":\"dnnl_memory_block\""),
              std::string::npos);
    EXPECT_NE(first.find("\"reserved_backing_bytes\":192"),
              std::string::npos);
    EXPECT_NE(first.find("\"total_physical_state_bytes\":192"),
              std::string::npos);
    EXPECT_EQ(first.find("0x"), std::string::npos);
    EXPECT_EQ(first.find("raw_address"), std::string::npos);
    ASSERT_FALSE(first.empty());
    EXPECT_EQ(first.front(), '{');
    EXPECT_EQ(first.back(), '}');

    std::vector<ObservedAllocation> oversized;
    oversized.reserve(10000);
    for (size_t index = 0; index < 10000; ++index) {
        oversized.push_back(allocation(
            "state_" + std::to_string(index),
            StateMemoryRole::INPUT,
            AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
            index + 1,
            1,
            1));
    }
    const auto oversized_snapshot = build_state_allocation_snapshot(
        std::move(oversized),
        240,
        kContext);
    expect_ov_exception(
        [&] {
            static_cast<void>(
                serialize_state_allocation_snapshot(oversized_snapshot));
        },
        "state allocation JSON exceeds 1048576 bytes");
}

TEST(StateAllocationsDump, AcceptsEveryAllowedPhase) {
    for (const std::string phase :
         {"fresh", "seeded_no_infer", "post_infer"}) {
        auto context = kContext;
        context.phase = phase;
        const auto snapshot =
            build_state_allocation_snapshot({}, 25, context);
        EXPECT_EQ(snapshot.phase, phase);
    }
}

}  // namespace
}  // namespace ov::intel_cpu

#endif  // CPU_DEBUG_CAPS
```

This is the only test source for Task 2. Its exact bytes are the accepted RED
fixture. Do not weaken an assertion or touch this file to make GREEN;
implementation must satisfy the exact contract.

## Step 3: Reconfigure and prove RED under the process guard

Use the reviewed CPU-unit configure route. A prior label is never overwritten.
Attempt 001 stopped safely before the intended RED build because upstream
HETERO adds its functional-test subdirectory whenever `ENABLE_TESTS=ON`, even
when `ENABLE_FUNCTIONAL_TESTS=OFF`; that target then requires the deliberately
absent `openvino::funcSharedTests`. Preserve and audit that failure evidence.
Attempt 002 then configured successfully with HETERO disabled, but its
pre-revision-5 wrapper rejected the subst spelling after controller success and
did not emit a wrapper sidecar. It is historical evidence, not a completed
attempt. Attempt 003 then failed in the PowerShell bootstrap before guard
launch or source edit, as frozen above. Attempt 004 ended after its successful
configure and is closed by the exact sibling receipt audited in Step 1.
Preserve all four historical attempts and their diagnostic evidence
byte-for-byte. Reuse the already valid, non-reparse
`C:\ov-build\state-observer` cache, reconfigure it in place, and use fresh
attempt 005 for the complete execution. Within attempt 005 the eight labels
and their ordering are exact; an interrupted or extra-label attempt is
incomplete and must never be extended under the same directory.

Materialize the following fence exactly, with LF newlines and UTF-8 without
BOM, as
`R:\.superpowers\sdd\task02-attempt005-link-cap-supervisor.ps1`.
The execution copy must be exactly `38,077` bytes with SHA-256
`fd005d235b27a532ac5d0536ccb5b429e9c4f554efcd1f6ed5c9a5cf08e0f90b`;
the controller refuses any other bytes. This supervisor owns only the one
`MSBuild.exe` root and nested Job it creates. The process table is used only
for the immediate empty-concurrency gate. All descendant actions are scoped
through the private nested Job or an exact handle obtained from that Job's
completion message; it never acts on, closes, or modifies Visual Studio Code,
Codex, Edge, or any other unrelated user process.

```powershell
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [ValidateSet("Unit", "Plugin")]
  [string]$Mode
)

$ErrorActionPreference = "Stop"
$powerShellPath =
  "C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe"
$msbuildPath =
  "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe"
$msbuildSha256 =
  "106cac9dc67569fe80102cb4a11f49dc48ad44529cabcaf2259e1aa74ad19bec"
$linkPath =
  "C:\Program Files\Microsoft Visual Studio\18\Community\VC\Tools\MSVC\14.51.36231\bin\Hostx64\x64\link.exe"
$linkSha256 =
  "e8c524347b8bc87fba790d254c8a3b902bf1a4b63807093b816d992940af3791"
$linkFileVersion = "14.51.36248.0"
$minimumWorkingSetBytes = [uint64]1048576
$maximumWorkingSetBytes = [uint64]4294967296
$workingSetSetFlags = [uint32]0x4
$workingSetReadbackFlags = [uint32]0x6
$minimumPhysicalHeadroomBytes = [uint64]7784628224
$minimumVirtualHeadroomBytes = [uint64]5368709120
$blockingProcessNames = [string[]]@(
  "MSBuild.exe",
  "link.exe",
  "cl.exe",
  "cmake.exe",
  "ctest.exe",
  "ninja.exe",
  "ov_cpu_unit_tests.exe"
)

if ($PSVersionTable.PSEdition -cne "Desktop" -or
    $PSVersionTable.PSVersion.Major -ne 5 -or
    $PSVersionTable.PSVersion.Minor -ne 1 -or
    [IntPtr]::Size -ne 8 -or
    -not [IO.Path]::GetFullPath((Get-Process -Id $PID).Path).Equals(
      $powerShellPath,
      [StringComparison]::OrdinalIgnoreCase
    ) -or
    (Get-ExecutionPolicy -Scope Process) -ne "Bypass") {
  throw "Task 02 cap supervisor requires 64-bit Windows PowerShell 5.1"
}
if ((Get-FileHash -LiteralPath $msbuildPath -Algorithm SHA256).
      Hash.ToLowerInvariant() -cne $msbuildSha256) {
  throw "Pinned x64 MSBuild executable drifted"
}
if ((Get-FileHash -LiteralPath $linkPath -Algorithm SHA256).
      Hash.ToLowerInvariant() -cne $linkSha256 -or
    (Get-Item -LiteralPath $linkPath).VersionInfo.FileVersion -cne
      $linkFileVersion) {
  throw "Pinned x64 link.exe identity drifted"
}

$operatingSystem = Get-CimInstance -ClassName Win32_OperatingSystem
$freePhysicalBytes = [uint64]$operatingSystem.FreePhysicalMemory * 1024
$freeVirtualBytes = [uint64]$operatingSystem.FreeVirtualMemory * 1024
if ($freePhysicalBytes -lt $minimumPhysicalHeadroomBytes -or
    $freeVirtualBytes -lt $minimumVirtualHeadroomBytes) {
  throw (
    "Task 02 heavy-build headroom gate failed: physical={0}, virtual={1}" -f
      $freePhysicalBytes, $freeVirtualBytes
  )
}

if (-not ("Task02CappedJobNative" -as [type])) {
  Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;

public static class Task02CappedJobNative {
    [StructLayout(LayoutKind.Sequential)]
    public struct IO_COUNTERS {
        public UInt64 ReadOperationCount;
        public UInt64 WriteOperationCount;
        public UInt64 OtherOperationCount;
        public UInt64 ReadTransferCount;
        public UInt64 WriteTransferCount;
        public UInt64 OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct JOBOBJECT_BASIC_LIMIT_INFORMATION {
        public Int64 PerProcessUserTimeLimit;
        public Int64 PerJobUserTimeLimit;
        public UInt32 LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public UInt32 ActiveProcessLimit;
        public UIntPtr Affinity;
        public UInt32 PriorityClass;
        public UInt32 SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct JOBOBJECT_BASIC_ACCOUNTING_INFORMATION {
        public Int64 TotalUserTime;
        public Int64 TotalKernelTime;
        public Int64 ThisPeriodTotalUserTime;
        public Int64 ThisPeriodTotalKernelTime;
        public UInt32 TotalPageFaultCount;
        public UInt32 TotalProcesses;
        public UInt32 ActiveProcesses;
        public UInt32 TotalTerminatedProcesses;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct JOBOBJECT_ASSOCIATE_COMPLETION_PORT {
        public IntPtr CompletionKey;
        public IntPtr CompletionPort;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct STARTUPINFO {
        public UInt32 cb;
        public IntPtr lpReserved;
        public IntPtr lpDesktop;
        public IntPtr lpTitle;
        public UInt32 dwX;
        public UInt32 dwY;
        public UInt32 dwXSize;
        public UInt32 dwYSize;
        public UInt32 dwXCountChars;
        public UInt32 dwYCountChars;
        public UInt32 dwFillAttribute;
        public UInt32 dwFlags;
        public UInt16 wShowWindow;
        public UInt16 cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_INFORMATION {
        public IntPtr hProcess;
        public IntPtr hThread;
        public UInt32 dwProcessId;
        public UInt32 dwThreadId;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr CreateJobObjectW(
        IntPtr jobAttributes,
        string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetInformationJobObject(
        IntPtr job,
        Int32 informationClass,
        IntPtr information,
        UInt32 informationLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool QueryInformationJobObject(
        IntPtr job,
        Int32 informationClass,
        IntPtr information,
        UInt32 informationLength,
        out UInt32 returnLength);

    [DllImport(
        "kernel32.dll",
        EntryPoint = "QueryInformationJobObject",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool QueryExtendedLimits(
        IntPtr job,
        Int32 informationClass,
        out JOBOBJECT_EXTENDED_LIMIT_INFORMATION information,
        UInt32 informationLength,
        out UInt32 returnLength);

    [DllImport(
        "kernel32.dll",
        EntryPoint = "QueryInformationJobObject",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool QueryBasicAccounting(
        IntPtr job,
        Int32 informationClass,
        out JOBOBJECT_BASIC_ACCOUNTING_INFORMATION information,
        UInt32 informationLength,
        out UInt32 returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr CreateIoCompletionPort(
        IntPtr fileHandle,
        IntPtr existingCompletionPort,
        IntPtr completionKey,
        UInt32 numberOfConcurrentThreads);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CreateProcessW(
        string applicationName,
        StringBuilder commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        bool inheritHandles,
        UInt32 creationFlags,
        IntPtr environment,
        string currentDirectory,
        ref STARTUPINFO startupInfo,
        out PROCESS_INFORMATION processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AssignProcessToJobObject(
        IntPtr job,
        IntPtr process);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern UInt32 ResumeThread(IntPtr thread);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetQueuedCompletionStatus(
        IntPtr completionPort,
        out UInt32 completionCode,
        out IntPtr completionKey,
        out IntPtr overlapped,
        UInt32 milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(
        UInt32 desiredAccess,
        bool inheritHandle,
        UInt32 processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool QueryFullProcessImageNameW(
        IntPtr process,
        UInt32 flags,
        StringBuilder executableName,
        ref UInt32 size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetProcessWorkingSetSizeEx(
        IntPtr process,
        UIntPtr minimumWorkingSetSize,
        UIntPtr maximumWorkingSetSize,
        UInt32 flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetProcessWorkingSetSizeEx(
        IntPtr process,
        out UIntPtr minimumWorkingSetSize,
        out UIntPtr maximumWorkingSetSize,
        out UInt32 flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetProcessTimes(
        IntPtr process,
        out Int64 creationTime,
        out Int64 exitTime,
        out Int64 kernelTime,
        out Int64 userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsProcessInJob(
        IntPtr process,
        IntPtr job,
        out bool result);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetExitCodeProcess(
        IntPtr process,
        out UInt32 exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool TerminateJobObject(
        IntPtr job,
        UInt32 exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool TerminateProcess(
        IntPtr process,
        UInt32 exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern UInt32 WaitForSingleObject(
        IntPtr handle,
        UInt32 milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GetStdHandle(Int32 standardHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr handle);
}
'@
}

function Set-Task02JobStructure {
  param(
    [Parameter(Mandatory = $true)][IntPtr]$JobHandle,
    [Parameter(Mandatory = $true)][int]$InformationClass,
    [Parameter(Mandatory = $true)][object]$Value
  )
  $size = [Runtime.InteropServices.Marshal]::SizeOf($Value)
  $buffer = [Runtime.InteropServices.Marshal]::AllocHGlobal($size)
  try {
    [Runtime.InteropServices.Marshal]::StructureToPtr($Value, $buffer, $false)
    if (-not [Task02CappedJobNative]::SetInformationJobObject(
        $JobHandle,
        $InformationClass,
        $buffer,
        [uint32]$size
      )) {
      $win32Error = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
      throw "SetInformationJobObject($InformationClass) failed: $win32Error"
    }
  } finally {
    [Runtime.InteropServices.Marshal]::FreeHGlobal($buffer)
  }
}

function Get-Task02JobStructure {
  param(
    [Parameter(Mandatory = $true)][IntPtr]$JobHandle,
    [Parameter(Mandatory = $true)][int]$InformationClass,
    [Parameter(Mandatory = $true)][type]$StructureType
  )
  $returned = [uint32]0
  if ($StructureType -eq
      [Task02CappedJobNative+JOBOBJECT_EXTENDED_LIMIT_INFORMATION]) {
    $value =
      New-Object Task02CappedJobNative+JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    $size = [uint32][Runtime.InteropServices.Marshal]::SizeOf($value)
    $ok = [Task02CappedJobNative]::QueryExtendedLimits(
      $JobHandle,
      $InformationClass,
      [ref]$value,
      $size,
      [ref]$returned
    )
  } elseif ($StructureType -eq
      [Task02CappedJobNative+JOBOBJECT_BASIC_ACCOUNTING_INFORMATION]) {
    $value =
      New-Object Task02CappedJobNative+JOBOBJECT_BASIC_ACCOUNTING_INFORMATION
    $size = [uint32][Runtime.InteropServices.Marshal]::SizeOf($value)
    $ok = [Task02CappedJobNative]::QueryBasicAccounting(
      $JobHandle,
      $InformationClass,
      [ref]$value,
      $size,
      [ref]$returned
    )
  } else {
    throw "Unsupported nested Job query structure: $StructureType"
  }
  if (-not $ok -or $returned -ne $size) {
    $win32Error = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
    throw "QueryInformationJobObject($InformationClass) failed: $win32Error"
  }
  $value | Add-Member `
    -NotePropertyName query_returned_bytes `
    -NotePropertyValue ([uint32]$returned) `
    -PassThru
}

function Get-Task02JobProcessIdList {
  param([Parameter(Mandatory = $true)][IntPtr]$JobHandle)
  $size = 8 + ([IntPtr]::Size * 16)
  $buffer = [Runtime.InteropServices.Marshal]::AllocHGlobal($size)
  try {
    $returned = [uint32]0
    if (-not [Task02CappedJobNative]::QueryInformationJobObject(
        $JobHandle,
        3,
        $buffer,
        [uint32]$size,
        [ref]$returned
      ) -or
      $returned -lt 8) {
      $win32Error = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
      throw "JobObjectBasicProcessIdList query failed: $win32Error"
    }
    $assigned = [Runtime.InteropServices.Marshal]::ReadInt32($buffer, 0)
    $listed = [Runtime.InteropServices.Marshal]::ReadInt32($buffer, 4)
    if ($assigned -lt 0 -or $listed -lt 0 -or $listed -gt 16) {
      throw "Nested Job process-ID list is not bounded or well formed"
    }
    $processIds = @()
    for ($index = 0; $index -lt $listed; ++$index) {
      $offset = 8 + ($index * [IntPtr]::Size)
      $processIds += if ([IntPtr]::Size -eq 8) {
        [Runtime.InteropServices.Marshal]::ReadInt64($buffer, $offset)
      } else {
        [Runtime.InteropServices.Marshal]::ReadInt32($buffer, $offset)
      }
    }
    [PSCustomObject][ordered]@{
      returned_bytes = [uint32]$returned
      assigned_process_count = [int64]$assigned
      listed_process_count = [int64]$listed
      process_ids = @($processIds)
    }
  } finally {
    [Runtime.InteropServices.Marshal]::FreeHGlobal($buffer)
  }
}

function Get-Task02OwnedProcessIdentity {
  param(
    [Parameter(Mandatory = $true)][IntPtr]$Handle,
    [Parameter(Mandatory = $true)][int]$ProcessId,
    [Parameter(Mandatory = $true)][IntPtr]$JobHandle
  )
  $creationFileTimeUtc = [int64]0
  $exitFileTimeUtc = [int64]0
  $kernelFileTimeUtc = [int64]0
  $userFileTimeUtc = [int64]0
  if (-not [Task02CappedJobNative]::GetProcessTimes(
      $Handle,
      [ref]$creationFileTimeUtc,
      [ref]$exitFileTimeUtc,
      [ref]$kernelFileTimeUtc,
      [ref]$userFileTimeUtc
    ) -or
    $creationFileTimeUtc -le 0) {
    throw "PID $ProcessId creation identity is unavailable"
  }
  $pathBuilder = [Text.StringBuilder]::new(32768)
  $pathLength = [uint32]$pathBuilder.Capacity
  if (-not [Task02CappedJobNative]::QueryFullProcessImageNameW(
      $Handle,
      [uint32]0,
      $pathBuilder,
      [ref]$pathLength
    ) -or
    $pathLength -le 0) {
    throw "PID $ProcessId executable identity is unavailable"
  }
  $nestedMember = $false
  if (-not [Task02CappedJobNative]::IsProcessInJob(
      $Handle,
      $JobHandle,
      [ref]$nestedMember
    ) -or
    -not $nestedMember) {
    throw "PID $ProcessId is not a member of the exact nested Job"
  }
  [PSCustomObject][ordered]@{
    pid = [int64]$ProcessId
    creation_filetime_utc = $creationFileTimeUtc
    executable = [IO.Path]::GetFullPath($pathBuilder.ToString())
    nested_job_member = $true
  }
}

function Assert-Task02SupervisorOuterJobMembership {
  $handle = [Task02CappedJobNative]::OpenProcess(
    [uint32](0x0400 -bor 0x1000),
    $false,
    [uint32]$PID
  )
  if ($handle -eq [IntPtr]::Zero) {
    throw "Unable to open the cap supervisor for outer-Job verification"
  }
  try {
    $inOuterJob = $false
    if (-not [Task02CappedJobNative]::IsProcessInJob(
        $handle,
        [IntPtr]::Zero,
        [ref]$inOuterJob
      ) -or
      -not $inOuterJob) {
      throw "Cap supervisor did not inherit the accepted outer guard Job"
    }
  } finally {
    [void][Task02CappedJobNative]::CloseHandle($handle)
  }
}

function Receive-Task02JobMessage {
  param(
    [Parameter(Mandatory = $true)][IntPtr]$CompletionPort,
    [Parameter(Mandatory = $true)][int]$TimeoutMilliseconds
  )
  $message = [uint32]0
  $key = [IntPtr]::Zero
  $overlapped = [IntPtr]::Zero
  $ok = [Task02CappedJobNative]::GetQueuedCompletionStatus(
    $CompletionPort,
    [ref]$message,
    [ref]$key,
    [ref]$overlapped,
    [uint32]$TimeoutMilliseconds
  )
  if (-not $ok) {
    $win32Error = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
    if ($win32Error -eq 258) {
      return $null
    }
    throw "GetQueuedCompletionStatus failed with Win32 $win32Error"
  }
  [PSCustomObject][ordered]@{
    message = $message
    completion_key = $key
    process_id = [int64]$overlapped.ToInt64()
  }
}

$projectPath = if ($Mode -ceq "Unit") {
  "C:\ov-build\state-observer\src\plugins\intel_cpu\tests\unit\ov_cpu_unit_tests.vcxproj"
} else {
  "C:\ov-build\state-observer\src\plugins\intel_cpu\openvino_intel_cpu_plugin.vcxproj"
}
$msbuildArguments = @(
  $projectPath,
  "/t:Build",
  "/p:Configuration=Release",
  "/p:Platform=x64",
  "/p:BuildProjectReferences=true",
  "/p:MultiProcCL=false",
  "/p:UseMultiToolTask=false",
  "/p:TrackFileAccess=false",
  "/m:1",
  "/nr:false",
  "/v:minimal"
)
$jobObjectBasicAccountingInformation = 1
$jobObjectBasicProcessIdList = 3
$jobObjectAssociateCompletionPortInformation = 7
$jobObjectExtendedLimitInformation = 9
$jobObjectLimitFlags = [uint32]0x00002300
$processMemoryLimitBytes = [uint64]6442450944
$jobMemoryLimitBytes = [uint64]7516192768
$completionKeyValue = [int64]0x5430324C
$createSuspended = [uint32]0x00000004
$createNoWindow = [uint32]0x08000000
$jobObjectMessageActiveProcessZero = [uint32]4
$jobObjectMessageNewProcess = [uint32]6
$jobObjectMessageExitProcess = [uint32]7
$jobObjectMessageAbnormalExitProcess = [uint32]8
$jobHandle = [IntPtr]::Zero
$completionPort = [IntPtr]::Zero
$processInformation =
  New-Object Task02CappedJobNative+PROCESS_INFORMATION
$rootCreated = $false
$rootAssigned = $false
$rootResumed = $false
$startedProcesses = @{}
$terminalProcesses = @{}
$cappedLinkProcessIdentities = [Collections.Generic.HashSet[string]]::new(
  [StringComparer]::Ordinal
)
$capEvents = [Collections.Generic.List[object]]::new()
$state = [PSCustomObject][ordered]@{
  event_sequence = [int64]0
  active_process_zero_count = [int64]0
  abnormal_exit_count = [int64]0
}

try {
  $preflightRows = @(
    Get-CimInstance -ClassName Win32_Process -Property @(
      "ProcessId", "CreationDate", "Name", "ExecutablePath", "CommandLine"
    ) -ErrorAction Stop
  )
  $blockingRows = @(
    $preflightRows | Where-Object {
      $blockingProcessNames -ccontains [string]$_.Name
    }
  )
  $preflightOperatingSystem =
    Get-CimInstance -ClassName Win32_OperatingSystem
  $preflightPhysicalBytes =
    [uint64]$preflightOperatingSystem.FreePhysicalMemory * 1024
  $preflightVirtualBytes =
    [uint64]$preflightOperatingSystem.FreeVirtualMemory * 1024
  if ($blockingRows.Count -ne 0 -or
      $preflightPhysicalBytes -lt $minimumPhysicalHeadroomBytes -or
      $preflightVirtualBytes -lt $minimumVirtualHeadroomBytes) {
    throw "Task 02 immediate heavy-build concurrency/headroom preflight failed"
  }
  Assert-Task02SupervisorOuterJobMembership
  $preflightFileTimeUtc = [DateTime]::UtcNow.ToFileTimeUtc()
  Write-Output (
    "[task02-heavy-preflight] mode={0} blocking_count=0 physical_bytes={1} virtual_bytes={2} observed_filetime_utc={3} job_member=true verified=true" -f
      $Mode,
      $preflightPhysicalBytes,
      $preflightVirtualBytes,
      $preflightFileTimeUtc
  )

  $jobHandle = [Task02CappedJobNative]::CreateJobObjectW(
    [IntPtr]::Zero,
    $null
  )
  if ($jobHandle -eq [IntPtr]::Zero) {
    $win32Error = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
    throw "CreateJobObjectW failed with Win32 $win32Error"
  }
  $limits =
    New-Object Task02CappedJobNative+JOBOBJECT_EXTENDED_LIMIT_INFORMATION
  $basicLimits =
    New-Object Task02CappedJobNative+JOBOBJECT_BASIC_LIMIT_INFORMATION
  $basicLimits.LimitFlags = $jobObjectLimitFlags
  $limits.BasicLimitInformation = $basicLimits
  $limits.ProcessMemoryLimit = [UIntPtr]::new($processMemoryLimitBytes)
  $limits.JobMemoryLimit = [UIntPtr]::new($jobMemoryLimitBytes)
  Set-Task02JobStructure `
    -JobHandle $jobHandle `
    -InformationClass $jobObjectExtendedLimitInformation `
    -Value $limits
  $readLimits = Get-Task02JobStructure `
    -JobHandle $jobHandle `
    -InformationClass $jobObjectExtendedLimitInformation `
    -StructureType (
      [Task02CappedJobNative+JOBOBJECT_EXTENDED_LIMIT_INFORMATION]
    )
  if ([uint32]$readLimits.query_returned_bytes -ne 144 -or
      [uint32]$readLimits.BasicLimitInformation.LimitFlags -ne
        $jobObjectLimitFlags -or
      $readLimits.BasicLimitInformation.MinimumWorkingSetSize.ToUInt64() -ne
        0 -or
      $readLimits.BasicLimitInformation.MaximumWorkingSetSize.ToUInt64() -ne
        0 -or
      $readLimits.ProcessMemoryLimit.ToUInt64() -ne
        $processMemoryLimitBytes -or
      $readLimits.JobMemoryLimit.ToUInt64() -ne $jobMemoryLimitBytes) {
    throw "Nested Job private-commit limit read-back was not exact"
  }
  Write-Output (
    "[task02-job-cap] mode={0} process_memory_bytes={1} job_memory_bytes={2} limit_flags={3} set_before_assign=true read_back=true verified=true" -f
      $Mode,
      $processMemoryLimitBytes,
      $jobMemoryLimitBytes,
      $jobObjectLimitFlags
  )

  $completionPort = [Task02CappedJobNative]::CreateIoCompletionPort(
    [IntPtr]::new(-1),
    [IntPtr]::Zero,
    [IntPtr]::Zero,
    [uint32]1
  )
  if ($completionPort -eq [IntPtr]::Zero) {
    $win32Error = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
    throw "CreateIoCompletionPort failed with Win32 $win32Error"
  }
  $association =
    New-Object Task02CappedJobNative+JOBOBJECT_ASSOCIATE_COMPLETION_PORT
  $association.CompletionKey = [IntPtr]::new($completionKeyValue)
  $association.CompletionPort = $completionPort
  Set-Task02JobStructure `
    -JobHandle $jobHandle `
    -InformationClass $jobObjectAssociateCompletionPortInformation `
    -Value $association

  foreach ($argument in $msbuildArguments) {
    if ([string]$argument -cmatch '["\r\n]') {
      throw "MSBuild argument cannot be represented by the exact native launcher"
    }
  }
  $nativeCommandLine = [Text.StringBuilder]::new(
    '"' + $msbuildPath + '" ' +
      (($msbuildArguments | ForEach-Object { '"' + [string]$_ + '"' }) -join ' ')
  )
  $startupInfo = New-Object Task02CappedJobNative+STARTUPINFO
  $startupInfo.cb =
    [uint32][Runtime.InteropServices.Marshal]::SizeOf($startupInfo)
  $startupInfo.dwFlags = [uint32]0x00000100
  $startupInfo.hStdInput =
    [Task02CappedJobNative]::GetStdHandle([int32]-10)
  $startupInfo.hStdOutput =
    [Task02CappedJobNative]::GetStdHandle([int32]-11)
  $startupInfo.hStdError =
    [Task02CappedJobNative]::GetStdHandle([int32]-12)
  foreach ($standardHandle in @(
      $startupInfo.hStdInput,
      $startupInfo.hStdOutput,
      $startupInfo.hStdError
    )) {
    if ($standardHandle -eq [IntPtr]::Zero -or
        $standardHandle -eq [IntPtr]::new(-1)) {
      throw "Guard-captured standard handle is unavailable for MSBuild"
    }
  }
  $createResult = [Task02CappedJobNative]::CreateProcessW(
    $msbuildPath,
    $nativeCommandLine,
    [IntPtr]::Zero,
    [IntPtr]::Zero,
    $true,
    [uint32]($createSuspended -bor $createNoWindow),
    [IntPtr]::Zero,
    "C:\ov-build\state-observer",
    [ref]$startupInfo,
    [ref]$processInformation
  )
  if (-not $createResult) {
    $win32Error = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
    throw "Suspended CreateProcessW failed with Win32 $win32Error"
  }
  $rootCreated = $true
  if (-not [Task02CappedJobNative]::AssignProcessToJobObject(
      $jobHandle,
      $processInformation.hProcess
    )) {
    $win32Error = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
    throw "Nested Job assignment failed with Win32 $win32Error"
  }
  $rootAssigned = $true
  $rootInNestedJob = $false
  if (-not [Task02CappedJobNative]::IsProcessInJob(
      $processInformation.hProcess,
      $jobHandle,
      [ref]$rootInNestedJob
    ) -or
    -not $rootInNestedJob) {
    throw "Suspended MSBuild root is not in the exact nested Job"
  }

  $consumeJobMessage = {
    param([Parameter(Mandatory = $true)][object]$Message)
    if ($Message.completion_key.ToInt64() -ne $completionKeyValue) {
      throw "Nested Job completion key changed"
    }
    $state.event_sequence++
    if ([uint32]$Message.message -eq $jobObjectMessageNewProcess) {
      $processId = [int64]$Message.process_id
      $pidKey = [string]$processId
      if ($processId -le 0 -or $startedProcesses.ContainsKey($pidKey)) {
        throw "Nested Job NEW_PROCESS PID is invalid, duplicated, or reused"
      }
      $processHandle = [Task02CappedJobNative]::OpenProcess(
        [uint32](0x0100 -bor 0x0400 -bor 0x1000),
        $false,
        [uint32]$processId
      )
      if ($processHandle -eq [IntPtr]::Zero) {
        throw "NEW_PROCESS exited before exact identity/cap audit: $processId"
      }
      try {
        $identity = Get-Task02OwnedProcessIdentity `
          -Handle $processHandle `
          -ProcessId ([int]$processId) `
          -JobHandle $jobHandle
        $identity | Add-Member -NotePropertyName event_sequence `
          -NotePropertyValue ([int64]$state.event_sequence)
        $startedProcesses[$pidKey] = $identity
        if ([IO.Path]::GetFileName($identity.executable) -ceq "link.exe") {
          $linkItem = Get-Item -LiteralPath $identity.executable -Force
          if (-not $identity.executable.Equals(
                $linkPath,
                [StringComparison]::OrdinalIgnoreCase
              ) -or
              ($linkItem.Attributes -band
                [IO.FileAttributes]::ReparsePoint) -ne 0 -or
              (Get-FileHash -LiteralPath $identity.executable -Algorithm SHA256).
                Hash.ToLowerInvariant() -cne $linkSha256 -or
              $linkItem.VersionInfo.FileVersion -cne $linkFileVersion) {
            throw "Owned link.exe did not match the pinned x64 linker"
          }
          $setResult =
            [Task02CappedJobNative]::SetProcessWorkingSetSizeEx(
              $processHandle,
              [UIntPtr]::new($minimumWorkingSetBytes),
              [UIntPtr]::new($maximumWorkingSetBytes),
              $workingSetSetFlags
            )
          if (-not $setResult) {
            $win32Error =
              [Runtime.InteropServices.Marshal]::GetLastWin32Error()
            throw "SetProcessWorkingSetSizeEx failed with Win32 $win32Error"
          }
          $readMinimum = [UIntPtr]::Zero
          $readMaximum = [UIntPtr]::Zero
          $readFlags = [uint32]0
          if (-not [Task02CappedJobNative]::GetProcessWorkingSetSizeEx(
              $processHandle,
              [ref]$readMinimum,
              [ref]$readMaximum,
              [ref]$readFlags
            ) -or
            $readMinimum.ToUInt64() -ne $minimumWorkingSetBytes -or
            $readMaximum.ToUInt64() -ne $maximumWorkingSetBytes -or
            $readFlags -ne $workingSetReadbackFlags) {
            throw "Owned link.exe working-set cap read-back was not exact"
          }
          $processIdentity = "{0}|{1}" -f
            $processId, $identity.creation_filetime_utc
          if (-not $cappedLinkProcessIdentities.Add($processIdentity)) {
            throw "Owned link.exe cap identity was duplicated"
          }
          $capEvents.Add([PSCustomObject][ordered]@{
            mode = $Mode
            pid = $processId
            creation_filetime_utc = $identity.creation_filetime_utc
            executable = $identity.executable
            executable_sha256 = $linkSha256
            minimum_bytes = $readMinimum.ToUInt64()
            maximum_bytes = $readMaximum.ToUInt64()
            set_flags = $workingSetSetFlags
            readback_flags = $readFlags
            nested_job_member = $true
            event_sequence = [int64]$state.event_sequence
            verified = $true
          })
          Write-Output (
            "[task02-link-cap] mode={0} pid={1} creation_filetime_utc={2} minimum_bytes={3} maximum_bytes={4} set_flags={5} readback_flags={6} job_member=true event_sequence={7} verified=true" -f
              $Mode,
              $processId,
              $identity.creation_filetime_utc,
              $readMinimum.ToUInt64(),
              $readMaximum.ToUInt64(),
              $workingSetSetFlags,
              $readFlags,
              $state.event_sequence
          )
        }
      } finally {
        [void][Task02CappedJobNative]::CloseHandle($processHandle)
      }
      return
    }
    if ([uint32]$Message.message -eq $jobObjectMessageExitProcess -or
        [uint32]$Message.message -eq
          $jobObjectMessageAbnormalExitProcess) {
      $pidKey = [string][int64]$Message.process_id
      if (-not $startedProcesses.ContainsKey($pidKey) -or
          $terminalProcesses.ContainsKey($pidKey)) {
        throw "Nested Job terminal notification is unmatched or duplicated"
      }
      $terminalProcesses[$pidKey] = [PSCustomObject][ordered]@{
        pid = [int64]$Message.process_id
        message = [uint32]$Message.message
        event_sequence = [int64]$state.event_sequence
      }
      if ([uint32]$Message.message -eq
          $jobObjectMessageAbnormalExitProcess) {
        $state.abnormal_exit_count++
      }
      return
    }
    if ([uint32]$Message.message -eq
        $jobObjectMessageActiveProcessZero) {
      if ($state.active_process_zero_count -ne 0) {
        throw "Nested Job emitted duplicate ACTIVE_PROCESS_ZERO notifications"
      }
      $state.active_process_zero_count++
      return
    }
    throw "Unexpected nested Job completion message: $($Message.message)"
  }

  $rootMessage = Receive-Task02JobMessage `
    -CompletionPort $completionPort `
    -TimeoutMilliseconds 5000
  if ($null -eq $rootMessage -or
      [uint32]$rootMessage.message -ne $jobObjectMessageNewProcess -or
      [int64]$rootMessage.process_id -ne
        [int64]$processInformation.dwProcessId) {
    throw "Suspended MSBuild root lacked its exact NEW_PROCESS notification"
  }
  & $consumeJobMessage $rootMessage
  $rootIdentity =
    $startedProcesses[[string][int64]$processInformation.dwProcessId]
  if (-not $rootIdentity.executable.Equals(
        $msbuildPath,
        [StringComparison]::OrdinalIgnoreCase
      )) {
    throw "Suspended MSBuild root executable identity was not exact"
  }
  $resumeResult =
    [Task02CappedJobNative]::ResumeThread($processInformation.hThread)
  if ($resumeResult -ne [uint32]1) {
    $win32Error = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
    throw "ResumeThread previous suspend count was $resumeResult/$win32Error"
  }
  $rootResumed = $true

  $completionDeadline = [DateTime]::UtcNow.AddHours(4)
  $rootExitWithoutActiveZeroDeadline = $null
  while ($state.active_process_zero_count -eq 0) {
    if ([DateTime]::UtcNow -ge $completionDeadline) {
      throw "Nested Job did not reach ACTIVE_PROCESS_ZERO before deadline"
    }
    $message = Receive-Task02JobMessage `
      -CompletionPort $completionPort `
      -TimeoutMilliseconds 100
    if ($null -ne $message) {
      & $consumeJobMessage $message
    }
    $rootWait = [Task02CappedJobNative]::WaitForSingleObject(
      $processInformation.hProcess,
      [uint32]0
    )
    if ($rootWait -eq 0 -and
        $null -eq $rootExitWithoutActiveZeroDeadline) {
      $rootExitWithoutActiveZeroDeadline =
        [DateTime]::UtcNow.AddSeconds(30)
    } elseif ($rootWait -ne 0 -and $rootWait -ne 258) {
      throw "Nonblocking MSBuild root wait returned $rootWait"
    }
    if ($null -ne $rootExitWithoutActiveZeroDeadline -and
        [DateTime]::UtcNow -ge $rootExitWithoutActiveZeroDeadline) {
      throw "MSBuild exited without timely ACTIVE_PROCESS_ZERO evidence"
    }
  }
  if ($state.active_process_zero_count -ne 1) {
    throw "Nested Job emitted duplicate ACTIVE_PROCESS_ZERO notifications"
  }
  $drainDeadline = [DateTime]::UtcNow.AddSeconds(2)
  while ([DateTime]::UtcNow -lt $drainDeadline) {
    $message = Receive-Task02JobMessage `
      -CompletionPort $completionPort `
      -TimeoutMilliseconds 100
    if ($null -ne $message) {
      & $consumeJobMessage $message
    }
  }
  if ($state.active_process_zero_count -ne 1) {
    throw "Nested Job ACTIVE_PROCESS_ZERO evidence changed during drain"
  }

  $waitResult = [Task02CappedJobNative]::WaitForSingleObject(
    $processInformation.hProcess,
    [uint32]30000
  )
  if ($waitResult -ne 0) {
    throw "MSBuild root handle wait returned $waitResult"
  }
  $rootExitCode = [uint32]0
  if (-not [Task02CappedJobNative]::GetExitCodeProcess(
      $processInformation.hProcess,
      [ref]$rootExitCode
    ) -or
    $rootExitCode -eq 259) {
    throw "MSBuild root exit code was unavailable"
  }

  $accounting = Get-Task02JobStructure `
    -JobHandle $jobHandle `
    -InformationClass $jobObjectBasicAccountingInformation `
    -StructureType (
      [Task02CappedJobNative+JOBOBJECT_BASIC_ACCOUNTING_INFORMATION]
    )
  $processIdList = Get-Task02JobProcessIdList -JobHandle $jobHandle
  $linkStarts = @(
    $startedProcesses.Values | Where-Object {
      [IO.Path]::GetFileName([string]$_.executable) -ceq "link.exe"
    }
  )
  if ($accounting.TotalProcesses -lt 1 -or
      $accounting.ActiveProcesses -ne 0 -or
      [uint32]$accounting.query_returned_bytes -ne 48 -or
      [int64]$accounting.TotalProcesses -ne $startedProcesses.Count -or
      $startedProcesses.Count -ne $terminalProcesses.Count -or
      $processIdList.assigned_process_count -ne 0 -or
      $processIdList.returned_bytes -lt 8 -or
      $processIdList.listed_process_count -ne 0 -or
      @($processIdList.process_ids).Count -ne 0 -or
      $state.abnormal_exit_count -ne 0 -or
      $linkStarts.Count -lt 1 -or
      $linkStarts.Count -ne $capEvents.Count) {
    throw "Nested Job completion/accounting/link-cap reconciliation failed"
  }
  foreach ($linkStart in $linkStarts) {
    $identity = "{0}|{1}" -f
      $linkStart.pid, $linkStart.creation_filetime_utc
    if (-not $cappedLinkProcessIdentities.Contains($identity)) {
      throw "A nested-Job linker start lacks its exact cap event"
    }
  }
  Write-Output (
    "[task02-link-cap-summary] mode={0} link_count={1} job_start_count={2} job_terminal_count={3} accounting_total_processes={4} accounting_active_processes=0 accounting_returned_bytes={5} assigned_process_count=0 listed_process_count=0 pid_list_returned_bytes={6} process_memory_bytes={7} job_memory_bytes={8} job_limit_flags={9} maximum_bytes={10} preflight_blocking_count=0 preflight_physical_bytes={11} preflight_virtual_bytes={12} preflight_filetime_utc={13} active_process_zero=true verified=true child_exit={14}" -f
      $Mode,
      $capEvents.Count,
      $startedProcesses.Count,
      $terminalProcesses.Count,
      $accounting.TotalProcesses,
      $accounting.query_returned_bytes,
      $processIdList.returned_bytes,
      $processMemoryLimitBytes,
      $jobMemoryLimitBytes,
      $jobObjectLimitFlags,
      $maximumWorkingSetBytes,
      $preflightPhysicalBytes,
      $preflightVirtualBytes,
      $preflightFileTimeUtc,
      $rootExitCode
  )
  exit $rootExitCode
} catch {
  $supervisorFailure = $_
  $cleanupFailure = $null
  try {
    if ($rootAssigned -and $jobHandle -ne [IntPtr]::Zero -and
        $state.active_process_zero_count -eq 0) {
      if (-not [Task02CappedJobNative]::TerminateJobObject(
          $jobHandle,
          [uint32]125
        )) {
        $win32Error = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
        throw "TerminateJobObject failed with Win32 $win32Error"
      }
    } elseif ($rootCreated -and -not $rootAssigned -and
        $processInformation.hProcess -ne [IntPtr]::Zero) {
      if (-not [Task02CappedJobNative]::TerminateProcess(
          $processInformation.hProcess,
          [uint32]125
        )) {
        $win32Error = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
        throw "Suspended-root TerminateProcess failed with Win32 $win32Error"
      }
    }
    if ($rootCreated -and
        $processInformation.hProcess -ne [IntPtr]::Zero) {
      $cleanupWait = [Task02CappedJobNative]::WaitForSingleObject(
        $processInformation.hProcess,
        [uint32]30000
      )
      if ($cleanupWait -ne 0) {
        throw "Owned Job/root cleanup wait returned $cleanupWait"
      }
    }
  } catch {
    $cleanupFailure = $_
  }
  if ($null -ne $cleanupFailure) {
    throw (
      "Task 02 supervisor failed: {0}; exact owned cleanup also failed: {1}" -f
        $supervisorFailure, $cleanupFailure
    )
  }
  throw $supervisorFailure
} finally {
  if ($processInformation.hThread -ne [IntPtr]::Zero) {
    [void][Task02CappedJobNative]::CloseHandle($processInformation.hThread)
  }
  if ($processInformation.hProcess -ne [IntPtr]::Zero) {
    [void][Task02CappedJobNative]::CloseHandle($processInformation.hProcess)
  }
  if ($jobHandle -ne [IntPtr]::Zero) {
    [void][Task02CappedJobNative]::CloseHandle($jobHandle)
  }
  if ($completionPort -ne [IntPtr]::Zero) {
    [void][Task02CappedJobNative]::CloseHandle($completionPort)
  }
}
```

The nested Job's 6 GiB per-process and 7 GiB aggregate private-commit limits
are applied and read back before the suspended MSBuild root is assigned or
resumed. The additional 4,096 MiB linker limit applies to each verified
`link.exe` resident working set immediately after its exact nested-Job
NEW_PROCESS message; it remains honestly post-launch. The setter input is
exactly `set_flags=4`, while its verified getter state is exactly
`readback_flags=6`; these are intentionally distinct Win32 contracts. The
completion stream is not trusted by itself: success requires unique NEW and
terminal messages to
equal `JobObjectBasicAccountingInformation.TotalProcesses`, active count zero,
one ACTIVE_PROCESS_ZERO, and an empty final BasicProcessIdList. Every linker
message must resolve to the pinned path/hash/version and one successful
same-handle cap/read-back. Any uncertainty terminates only the private nested
Job (or the still-suspended exact root handle if assignment never succeeded)
and fails the guarded command. The two exact empty concurrency/headroom
snapshots remain mandatory, and an up-to-date/no-link build is not accepted.
Do not report these safety controls as workbook performance measurements.

```powershell
$attempt001 =
  "R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards\task02-attempt-001"
$attempt001RecordPath = Join-Path $attempt001 "task2-configure-red.json"
$attempt001LogPath = Join-Path $attempt001 "task2-configure-red.log"
$attempt001Files = @(
  Get-ChildItem -LiteralPath $attempt001 -File -Force |
    ForEach-Object { $_.Name } | Sort-Object
)
if (Compare-Object @("task2-configure-red.json", "task2-configure-red.log") `
      $attempt001Files -CaseSensitive) {
  throw "Task 2 attempt 001 must contain exactly its two immutable files"
}
if ((Get-Item -LiteralPath $attempt001RecordPath).Length -ne 3434 -or
    (Get-Item -LiteralPath $attempt001LogPath).Length -ne 13000 -or
    (Get-FileHash -LiteralPath $attempt001RecordPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne
      "e5ce87fb234734693d61db6fbb32f79d2b5db6fe6c2ff1aa1c95f79c9df4457c" -or
    (Get-FileHash -LiteralPath $attempt001LogPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne
      "5eca5dba8c004944c5191acea461396ebc5a9dd1839b47b7fdf119df6f9a4c05") {
  throw "Task 2 attempt 001 evidence drifted"
}
$attempt001Record = Get-Content -LiteralPath $attempt001RecordPath -Raw |
  ConvertFrom-Json
if ($attempt001Record.schema -ne "official-openvino-owned-process-guard/v1" -or
    $attempt001Record.valid -ne $false -or
    [int]$attempt001Record.exit_code -ne 1 -or
    @($attempt001Record.validation_errors).Count -ne 1 -or
    [string]$attempt001Record.validation_errors[0] -ne
      "child exit code did not match expected_exit=zero" -or
    [string]$attempt001Record.log_sha256 -ne
      "5eca5dba8c004944c5191acea461396ebc5a9dd1839b47b7fdf119df6f9a4c05") {
  throw "Task 2 attempt 001 is not the accepted safe pre-RED blocker"
}
$attempt001Log = Get-Content -LiteralPath $attempt001LogPath -Raw
if ($attempt001Log -notmatch
    'Target "ov_hetero_func_tests" links to:[\s\S]*openvino::funcSharedTests') {
  throw "Task 2 attempt 001 no longer proves the HETERO configure blocker"
}

$attempt002 =
  "R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards\task02-attempt-002"
$attempt002RecordPath = Join-Path $attempt002 "task2-configure-red.json"
$attempt002LogPath = Join-Path $attempt002 "task2-configure-red.log"
$attempt002Files = @(
  Get-ChildItem -LiteralPath $attempt002 -File -Force |
    ForEach-Object { $_.Name } | Sort-Object
)
if (Compare-Object @("task2-configure-red.json", "task2-configure-red.log") `
      $attempt002Files -CaseSensitive) {
  throw "Task 2 attempt 002 must contain exactly its two immutable files"
}
if ((Get-Item -LiteralPath $attempt002RecordPath).Length -ne 3402 -or
    (Get-Item -LiteralPath $attempt002LogPath).Length -ne 9234 -or
    (Get-FileHash -LiteralPath $attempt002RecordPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne
      "47803dbb03722289287d7a5e45e86054f133d39a53c8c4f6ff5146e02f8f9b35" -or
    (Get-FileHash -LiteralPath $attempt002LogPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne
      "5f4e56928a4ed129fd229aa1488f9eb1a4b8118aeff9400a68c8c96eddc41d8d") {
  throw "Task 2 attempt 002 evidence drifted"
}
$attempt002Record = Get-Content -LiteralPath $attempt002RecordPath -Raw |
  ConvertFrom-Json
if ($attempt002Record.schema -ne "official-openvino-owned-process-guard/v1" -or
    $attempt002Record.valid -ne $true -or
    [int]$attempt002Record.exit_code -ne 0 -or
    [string]$attempt002Record.expected_exit -ne "zero" -or
    [string]$attempt002Record.log_sha256 -ne
      "5f4e56928a4ed129fd229aa1488f9eb1a4b8118aeff9400a68c8c96eddc41d8d" -or
    @($attempt002Record.validation_errors).Count -ne 0) {
  throw "Task 2 attempt 002 is not the accepted successful old-contract configure"
}
$attempt002Log = Get-Content -LiteralPath $attempt002LogPath -Raw
if ($attempt002Log -notmatch '(?m)^-- Configuring done \(' -or
    $attempt002Log -notmatch '(?m)^-- Generating done \(' -or
    $attempt002Log -notmatch
      '(?m)^-- Build files have been written to: C:/ov-build/state-observer\r?$' -or
    (Test-Path -LiteralPath (Join-Path $attempt002 "task2-configure-red.wrapper.json"))) {
  throw "Task 2 attempt 002 outcome or old-contract sidecar absence drifted"
}

$attempt003 =
  "R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards\task02-attempt-003"
$attempt003FailurePath = Join-Path $attempt003 "failure.json"
$attempt003StdoutPath = Join-Path $attempt003 "diagnostic.stdout.log"
$attempt003StderrPath = Join-Path $attempt003 "diagnostic.stderr.log"
$attempt003Files = @(
  Get-ChildItem -LiteralPath $attempt003 -File -Force |
    ForEach-Object { $_.Name } | Sort-Object
)
if (Compare-Object @(
      "diagnostic.stderr.log", "diagnostic.stdout.log", "failure.json"
    ) $attempt003Files -CaseSensitive) {
  throw "Task 2 attempt 003 must contain exactly its three bootstrap files"
}
$attempt003Failure =
  Get-Content -LiteralPath $attempt003FailurePath -Raw | ConvertFrom-Json
if ($attempt003Failure.schema -ne
      "openvino-cpu-observer-task02-bootstrap-failure/v1" -or
    [int]$attempt003Failure.initial_launcher_exit_code -ne 1 -or
    $attempt003Failure.red_ready_created -ne $false -or
    $attempt003Failure.green_ready_created -ne $false -or
    $attempt003Failure.guard_artifact_created -ne $false -or
    $attempt003Failure.source_edited -ne $false -or
    $attempt003Failure.guard_command_started -ne $false -or
    [int]$attempt003Failure.diagnostic_exit_code -ne 1 -or
    [string]$attempt003Failure.diagnostic_stdout.sha256 -ne
      "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855" -or
    [string]$attempt003Failure.diagnostic_stderr.sha256 -ne
      "f7696a73d2e465870281987af6e480b9db0ce59f518b51daf5b783c7a0a18cea") {
  throw "Task 2 attempt 003 bootstrap receipt drifted"
}
if ((Get-Item -LiteralPath $attempt003StdoutPath).Length -ne 0 -or
    (Get-Content -LiteralPath $attempt003StderrPath -Raw) -notmatch
      "NativeCommandError" -or
    (Get-Content -LiteralPath $attempt003StderrPath -Raw) -notmatch
      "state_allocations_dump\.hpp.*does not exist in 'HEAD'") {
  throw "Task 2 attempt 003 no longer proves the preimage-probe failure"
}

$buildTree = [IO.Path]::GetFullPath("C:\ov-build\state-observer")
$buildTreeItem = Get-Item -LiteralPath $buildTree -Force
$nestedReparsePoints = @(
  Get-ChildItem -LiteralPath $buildTree -Recurse -Force |
    Where-Object {
      ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0
    }
)
if (($buildTreeItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
    $nestedReparsePoints.Count -ne 0) {
  throw "Reusable Task 2 build tree contains a reparse point"
}
$existingCache =
  Get-Content -LiteralPath (Join-Path $buildTree "CMakeCache.txt")
foreach ($required in @(
  "CMAKE_HOME_DIRECTORY:INTERNAL=O:/",
  "CMAKE_GENERATOR:INTERNAL=Visual Studio 18 2026",
  "CMAKE_GENERATOR_PLATFORM:INTERNAL=x64",
  "ENABLE_DEBUG_CAPS:BOOL=ON",
  "ENABLE_CPU_DEBUG_CAPS:BOOL=ON",
  "ENABLE_TESTS:BOOL=ON",
  "ENABLE_FUNCTIONAL_TESTS:BOOL=OFF",
  "ENABLE_HETERO:BOOL=OFF",
  "ENABLE_SAMPLES:BOOL=OFF",
  "ENABLE_PYTHON:BOOL=OFF",
  "ENABLE_INTEL_GPU:BOOL=OFF",
  "ENABLE_INTEL_NPU:BOOL=OFF",
  "ENABLE_OV_ONNX_FRONTEND:BOOL=OFF",
  "ENABLE_OV_PADDLE_FRONTEND:BOOL=OFF",
  "ENABLE_OV_TF_FRONTEND:BOOL=OFF",
  "ENABLE_LTO:INTERNAL=OFF",
  "BUILD_SHARED_LIBS:BOOL=ON"
)) {
  if ($existingCache -cnotcontains $required) {
    throw "Reusable observer cache is missing '$required'"
  }
}

$guardEvidence =
  "R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards\task02-attempt-005"
$expectedWorkingDirectoryPhysical = [IO.Path]::GetFullPath(
  "C:\Users\Student\IBM-Granite-Edge-AI-Optimisation-using-TurboQuant\.worktrees\openvino-turboquant-recovery"
)
$expectedAttempt005Physical = [IO.Path]::GetFullPath(
  [IO.Path]::Combine(
    $expectedWorkingDirectoryPhysical,
    "experiments\raw-results\openvino-turboquant\2026-07-28\guards\task02-attempt-005"
  )
)
if ((Test-Path -LiteralPath $guardEvidence) -and
    @(Get-ChildItem -LiteralPath $guardEvidence -Force).Count -ne 0) {
  throw "Task 2 attempt 005 is not fresh; preserve it and stop"
}
$wrapper =
  "C:\Users\Student\IBM-Granite-Edge-AI-Optimisation-using-TurboQuant\.worktrees\openvino-turboquant-recovery\scripts\testing\invoke_guarded_command.ps1"
$convertFromJson = $ExecutionContext.InvokeCommand.GetCommand(
  "Microsoft.PowerShell.Utility\ConvertFrom-Json",
  [Management.Automation.CommandTypes]::Cmdlet
)
if ($null -eq $convertFromJson -or
    $convertFromJson -isnot [Management.Automation.CmdletInfo] -or
    $null -eq $convertFromJson.ImplementingType) {
  throw "ConvertFrom-Json did not resolve to CmdletInfo"
}
$jsonAssembly = $convertFromJson.ImplementingType.Assembly
$jsonAssemblyName = $jsonAssembly.GetName()
$jsonAssemblyToken = [BitConverter]::ToString(
  $jsonAssemblyName.GetPublicKeyToken()
).Replace("-", "").ToLowerInvariant()
$trustedGacRoot = [IO.Path]::GetFullPath(
  [IO.Path]::Combine(
    [Environment]::GetFolderPath(
      [Environment+SpecialFolder]::Windows
    ),
    "Microsoft.Net\assembly\GAC_MSIL"
  )
).TrimEnd([char[]]"\/")
$trustedUtilityModuleRoot = [IO.Path]::GetFullPath(
  [IO.Path]::Combine($PSHOME, "Modules", "Microsoft.PowerShell.Utility")
).TrimEnd([char[]]"\/")
if ($null -eq $convertFromJson -or
    $convertFromJson -isnot [Management.Automation.CmdletInfo] -or
    $convertFromJson.CommandType -ne
      [Management.Automation.CommandTypes]::Cmdlet -or
    $convertFromJson.Name -cne "ConvertFrom-Json" -or
    $convertFromJson.ModuleName -cne "Microsoft.PowerShell.Utility" -or
    $convertFromJson.Source -cne "Microsoft.PowerShell.Utility" -or
    $convertFromJson.ImplementingType.FullName -cne
      "Microsoft.PowerShell.Commands.ConvertFromJsonCommand" -or
    $jsonAssemblyName.Name -cne "Microsoft.PowerShell.Commands.Utility" -or
    $jsonAssemblyToken -cne "31bf3856ad364e35" -or
    $jsonAssembly.GlobalAssemblyCache -ne $true -or
    -not [IO.Path]::GetFullPath($jsonAssembly.Location).StartsWith(
      $trustedGacRoot + [IO.Path]::DirectorySeparatorChar,
      [StringComparison]::OrdinalIgnoreCase
    ) -or
    $null -eq $convertFromJson.Module -or
    -not [IO.Path]::GetFullPath($convertFromJson.Module.Path).StartsWith(
      $trustedUtilityModuleRoot + [IO.Path]::DirectorySeparatorChar,
      [StringComparison]::OrdinalIgnoreCase
    )) {
  throw "ConvertFrom-Json did not bind to the trusted PSHOME cmdlet"
}
$convertToJson = $ExecutionContext.InvokeCommand.GetCommand(
  "Microsoft.PowerShell.Utility\ConvertTo-Json",
  [Management.Automation.CommandTypes]::Cmdlet
)
if ($null -eq $convertToJson -or
    $convertToJson -isnot [Management.Automation.CmdletInfo] -or
    $convertToJson.CommandType -ne
      [Management.Automation.CommandTypes]::Cmdlet -or
    $convertToJson.Name -cne "ConvertTo-Json" -or
    $convertToJson.ModuleName -cne "Microsoft.PowerShell.Utility" -or
    $convertToJson.Source -cne "Microsoft.PowerShell.Utility" -or
    $convertToJson.ImplementingType.FullName -cne
      "Microsoft.PowerShell.Commands.ConvertToJsonCommand" -or
    $convertToJson.ImplementingType.Assembly -ne $jsonAssembly) {
  throw "ConvertTo-Json did not bind to the trusted PSHOME cmdlet"
}

$utf8Strict = [Text.UTF8Encoding]::new($false, $true)
$wrapperProperties = @(
  "schema", "run_id", "command", "working_directory", "log_path",
  "evidence_path", "requested_path_provenance", "path_identity_verified",
  "expected_exit", "actual_exit_code", "log_sha256", "evidence_sha256",
  "controller_runtime", "controller_binding", "valid"
)
$controllerRuntimeProperties = @(
  "trusted_install_root", "interpreter_path", "interpreter_sha256",
  "signature_status", "signature_type", "signer_subject",
  "signer_thumbprint", "runtime_dll_path", "runtime_dll_sha256",
  "runtime_dll_signature_status", "runtime_dll_signature_type",
  "runtime_dll_signer_subject", "runtime_dll_signer_thumbprint",
  "isolation_flags"
)
$controllerResultProperties = @(
  "schema", "record_schema", "run_id", "evidence_path",
  "evidence_sha256", "log_path", "log_sha256", "actual_exit_code", "valid"
)
$recordProperties = @(
  "schema", "run_id", "command", "working_directory", "log_path",
  "evidence_path", "requested_path_provenance", "path_identity_verified",
  "expected_exit", "started_utc", "ended_utc", "elapsed_seconds",
  "configured_minimum_available_ram_bytes", "poll_interval_seconds",
  "cleanup_timeout_seconds", "maximum_runtime_seconds",
  "observed_available_ram_bytes", "memory_sample_count",
  "peak_working_set_bytes", "peak_private_bytes",
  "memory_query_failed_pids", "root_pid", "observed_pids",
  "child_process_observed", "exit_code", "timed_out", "low_memory_stop",
  "termination_reason", "msbuild_disable_node_reuse",
  "launch_governance", "job_object", "emergency_actions",
  "validation_errors", "log_sha256", "valid"
)

function Assert-ExactJsonObject {
  param(
    [object]$Value,
    [string]$Name,
    [string[]]$Properties
  )
  if ($null -eq $Value -or $Value -isnot [PSCustomObject]) {
    throw "$Name must be a JSON object"
  }
  $actual = @()
  foreach ($property in $Value.PSObject.Properties) {
    $actual += [string]$property.Name
  }
  if ($actual.Count -ne $Properties.Count) {
    throw "$Name must contain exactly $($Properties.Count) properties"
  }
  foreach ($property in $Properties) {
    if ($actual -cnotcontains $property) {
      throw "$Name is missing exact property '$property'"
    }
  }
}

function Assert-JsonString {
  param([object]$Value, [string]$Name)
  if ($Value -isnot [string]) { throw "$Name must be a JSON string" }
}

function Assert-JsonBoolean {
  param([object]$Value, [string]$Name)
  if ($Value -isnot [bool]) { throw "$Name must be a JSON boolean" }
}

function Assert-JsonInteger {
  param([object]$Value, [string]$Name)
  if ($Value -isnot [int] -and $Value -isnot [long]) {
    throw "$Name must be a JSON integer"
  }
}

function Assert-JsonNumber {
  param([object]$Value, [string]$Name)
  if ($Value -isnot [int] -and
      $Value -isnot [long] -and
      $Value -isnot [decimal] -and
      $Value -isnot [double]) {
    throw "$Name must be a JSON number"
  }
  $number = [double]$Value
  if ([double]::IsNaN($number) -or [double]::IsInfinity($number)) {
    throw "$Name must be a finite JSON number"
  }
}

function Assert-JsonArray {
  param([object]$Value, [string]$Name)
  if ($Value -isnot [object[]]) { throw "$Name must be a JSON array" }
}

function Assert-JsonNull {
  param([object]$Value, [string]$Name)
  if ($null -ne $Value) { throw "$Name must be JSON null" }
}

function Get-BytesSha256 {
  param([byte[]]$Bytes)
  $sha = [Security.Cryptography.SHA256]::Create()
  try {
    $digest = $sha.ComputeHash($Bytes)
  } finally {
    $sha.Dispose()
  }
  return [BitConverter]::ToString($digest).Replace("-", "").ToLowerInvariant()
}

function Convert-StrictJsonBytes {
  param([byte[]]$Bytes, [string]$Name)
  try {
    $text = $utf8Strict.GetString($Bytes)
    return $text | & $convertFromJson -ErrorAction Stop
  } catch {
    throw "$Name is not strict UTF-8 JSON"
  }
}

function Assert-StringArrayEquals {
  param(
    [object]$Value,
    [string[]]$Expected,
    [string]$Name
  )
  Assert-JsonArray -Value $Value -Name $Name
  if ($Value.Count -ne $Expected.Count) {
    throw "$Name has the wrong argv count"
  }
  for ($index = 0; $index -lt $Expected.Count; ++$index) {
    Assert-JsonString -Value $Value[$index] -Name "$Name[$index]"
    if ([string]$Value[$index] -cne $Expected[$index]) {
      throw "$Name differs at index $index"
    }
  }
}

function Assert-Task2ArtifactTriple {
  param(
    [string]$Label,
    [ValidateSet("zero", "nonzero")]
    [string]$ExpectedExit,
    [string[]]$ExpectedCommand,
    [string]$CanonicalArtifactDirectory
  )

  $recordName = $Label + ".json"
  $logName = $Label + ".log"
  $wrapperName = $Label + ".wrapper.json"
  if (-not [IO.Path]::IsPathRooted($CanonicalArtifactDirectory)) {
    throw "$Label canonical artifact directory is not absolute"
  }
  $wrapperPath =
    [IO.Path]::Combine($CanonicalArtifactDirectory, $wrapperName)
  if (-not [IO.File]::Exists($wrapperPath)) {
    throw "Missing canonical wrapper receipt for $Label"
  }
  $wrapperBytes = [IO.File]::ReadAllBytes($wrapperPath)
  if ($wrapperBytes.Length -lt 2 -or
      $wrapperBytes[$wrapperBytes.Length - 1] -ne 10) {
    throw "$Label wrapper receipt must end in exactly one LF"
  }
  $wrapperText = $utf8Strict.GetString($wrapperBytes)
  $wrapperLine = $wrapperText.Substring(0, $wrapperText.Length - 1)
  if ($wrapperLine.IndexOf("`r") -ge 0 -or
      $wrapperLine.IndexOf("`n") -ge 0 -or
      [string]::IsNullOrWhiteSpace($wrapperLine)) {
    throw "$Label wrapper receipt is not exactly one compact JSON line"
  }
  $receipt = Convert-StrictJsonBytes -Bytes $wrapperBytes -Name "$Label wrapper"
  Assert-ExactJsonObject -Value $receipt -Name "$Label wrapper receipt" `
    -Properties $wrapperProperties
  foreach ($field in @(
    "schema", "run_id", "working_directory", "log_path", "evidence_path",
    "expected_exit", "log_sha256", "evidence_sha256"
  )) {
    Assert-JsonString -Value $receipt.$field -Name "$Label wrapper.$field"
  }
  Assert-JsonInteger -Value $receipt.actual_exit_code `
    -Name "$Label wrapper.actual_exit_code"
  Assert-JsonBoolean -Value $receipt.valid -Name "$Label wrapper.valid"
  Assert-StringArrayEquals -Value $receipt.command -Expected $ExpectedCommand `
    -Name "$Label wrapper.command"
  Assert-ExactJsonObject -Value $receipt.requested_path_provenance `
    -Name "$Label wrapper.requested_path_provenance" `
    -Properties @("working_directory", "log_path", "evidence_path")
  Assert-ExactJsonObject -Value $receipt.path_identity_verified `
    -Name "$Label wrapper.path_identity_verified" `
    -Properties @("working_directory", "log_path", "evidence_path")
  foreach ($field in @("working_directory", "log_path", "evidence_path")) {
    Assert-JsonString -Value $receipt.requested_path_provenance.$field `
      -Name "$Label wrapper.requested_path_provenance.$field"
    Assert-JsonBoolean -Value $receipt.path_identity_verified.$field `
      -Name "$Label wrapper.path_identity_verified.$field"
    if ($receipt.path_identity_verified.$field -ne $true) {
      throw "$Label wrapper did not verify $field identity"
    }
  }
  if ($receipt.schema -cne "official-openvino-wrapper-verification/v1" -or
      $receipt.run_id -cnotmatch "^[0-9a-f]{64}$" -or
      $receipt.expected_exit -cne $ExpectedExit -or
      $receipt.valid -ne $true -or
      $receipt.log_sha256 -cnotmatch "^[0-9a-f]{64}$" -or
      $receipt.evidence_sha256 -cnotmatch "^[0-9a-f]{64}$") {
    throw "$Label wrapper scalar contract failed"
  }
  $requestedRecordPath =
    [IO.Path]::GetFullPath([IO.Path]::Combine($guardEvidence, $recordName))
  $requestedLogPath =
    [IO.Path]::GetFullPath([IO.Path]::Combine($guardEvidence, $logName))
  if (-not [IO.Path]::GetFullPath($receipt.working_directory).Equals(
        $expectedWorkingDirectoryPhysical,
        [StringComparison]::OrdinalIgnoreCase
      ) -or
      -not [IO.Path]::GetFullPath(
        $receipt.requested_path_provenance.working_directory
      ).Equals(
        $expectedWorkingDirectoryPhysical,
        [StringComparison]::OrdinalIgnoreCase
      ) -or
      -not [IO.Path]::GetFullPath(
        $receipt.requested_path_provenance.evidence_path
      ).Equals($requestedRecordPath, [StringComparison]::OrdinalIgnoreCase) -or
      -not [IO.Path]::GetFullPath(
        $receipt.requested_path_provenance.log_path
      ).Equals($requestedLogPath, [StringComparison]::OrdinalIgnoreCase)) {
    throw "$Label wrapper working/requested path provenance failed"
  }

  Assert-ExactJsonObject -Value $receipt.controller_runtime `
    -Name "$Label controller_runtime" -Properties $controllerRuntimeProperties
  foreach ($field in @(
    "trusted_install_root", "interpreter_path", "interpreter_sha256",
    "signature_status", "signature_type", "signer_subject",
    "signer_thumbprint", "runtime_dll_path", "runtime_dll_sha256",
    "runtime_dll_signature_status", "runtime_dll_signature_type",
    "runtime_dll_signer_subject", "runtime_dll_signer_thumbprint"
  )) {
    Assert-JsonString -Value $receipt.controller_runtime.$field `
      -Name "$Label controller_runtime.$field"
  }
  $runtime = $receipt.controller_runtime
  $expectedPythonSigner =
    "CN=Python Software Foundation, O=Python Software Foundation, " +
    "L=Beaverton, S=Oregon, C=US"
  if (-not [IO.Path]::GetFullPath($runtime.trusted_install_root).Equals(
        [IO.Path]::GetDirectoryName($pythonExecutable),
        [StringComparison]::OrdinalIgnoreCase
      ) -or
      -not [IO.Path]::GetFullPath($runtime.interpreter_path).Equals(
        $pythonExecutable, [StringComparison]::OrdinalIgnoreCase
      ) -or
      -not [IO.Path]::GetFullPath($runtime.runtime_dll_path).Equals(
        $pythonDll, [StringComparison]::OrdinalIgnoreCase
      ) -or
      $runtime.interpreter_sha256 -cne $pythonSha256 -or
      $runtime.runtime_dll_sha256 -cne $pythonDllSha256 -or
      $runtime.signature_status -cne "Valid" -or
      $runtime.signature_type -cne "Authenticode" -or
      $runtime.signer_subject -cne $expectedPythonSigner -or
      $runtime.runtime_dll_signature_status -cne "Valid" -or
      $runtime.runtime_dll_signature_type -cne "Authenticode" -or
      $runtime.runtime_dll_signer_subject -cne $expectedPythonSigner -or
      $runtime.signer_thumbprint -cnotmatch "^[0-9A-F]{40}$" -or
      $runtime.runtime_dll_signer_thumbprint -cnotmatch "^[0-9A-F]{40}$") {
    throw "$Label controller runtime provenance failed"
  }
  Assert-JsonArray -Value $runtime.isolation_flags `
    -Name "$Label controller_runtime.isolation_flags"
  foreach ($flag in $runtime.isolation_flags) {
    Assert-JsonString -Value $flag -Name "$Label isolation flag"
  }
  if ($runtime.isolation_flags.Count -ne 7 -or
      $runtime.isolation_flags[0] -cne "-E" -or
      $runtime.isolation_flags[1] -cne "-s" -or
      $runtime.isolation_flags[2] -cne "-S" -or
      $runtime.isolation_flags[3] -cne "-B" -or
      $runtime.isolation_flags[4] -cne "-X" -or
      $runtime.isolation_flags[5] -cnotmatch "^pycache_prefix=.+$" -or
      $runtime.isolation_flags[6] -cne "-m") {
    throw "$Label controller isolation flags are not exact"
  }
  $pycachePrefix = $runtime.isolation_flags[5].Substring(
    "pycache_prefix=".Length
  )
  if (-not [IO.Path]::IsPathRooted($pycachePrefix) -or
      -not [IO.Path]::GetFileName($pycachePrefix).StartsWith(
        "official-openvino-controller-pycache-",
        [StringComparison]::Ordinal
      ) -or
      [IO.File]::Exists($pycachePrefix) -or
      [IO.Directory]::Exists($pycachePrefix)) {
    throw "$Label isolated pycache prefix is not absolute and absent"
  }

  Assert-ExactJsonObject -Value $receipt.controller_binding `
    -Name "$Label controller_binding" -Properties $controllerResultProperties
  $binding = $receipt.controller_binding
  foreach ($field in @(
    "schema", "record_schema", "run_id", "evidence_path", "evidence_sha256",
    "log_path", "log_sha256"
  )) {
    Assert-JsonString -Value $binding.$field `
      -Name "$Label controller_binding.$field"
  }
  Assert-JsonInteger -Value $binding.actual_exit_code `
    -Name "$Label controller_binding.actual_exit_code"
  Assert-JsonBoolean -Value $binding.valid `
    -Name "$Label controller_binding.valid"
  if ($binding.schema -cne "official-openvino-controller-result/v1" -or
      $binding.record_schema -cne
        "official-openvino-owned-process-guard/v1" -or
      $binding.run_id -cne $receipt.run_id -or
      $binding.evidence_path -cne $receipt.evidence_path -or
      $binding.evidence_sha256 -cne $receipt.evidence_sha256 -or
      $binding.log_path -cne $receipt.log_path -or
      $binding.log_sha256 -cne $receipt.log_sha256 -or
      [int64]$binding.actual_exit_code -ne [int64]$receipt.actual_exit_code -or
      $binding.valid -ne $true) {
    throw "$Label exact nine-field controller result binding failed"
  }

  $recordPath = [string]$receipt.evidence_path
  $logPath = [string]$receipt.log_path
  if (-not [IO.Path]::IsPathRooted($recordPath) -or
      -not [IO.Path]::IsPathRooted($logPath) -or
      -not [IO.Path]::GetFullPath($recordPath).Equals(
        $recordPath, [StringComparison]::OrdinalIgnoreCase
      ) -or
      -not [IO.Path]::GetFullPath($logPath).Equals(
        $logPath, [StringComparison]::OrdinalIgnoreCase
      ) -or
      [IO.Path]::GetFileName($recordPath) -cne $recordName -or
      [IO.Path]::GetFileName($logPath) -cne $logName -or
      -not [IO.Path]::GetDirectoryName($recordPath).Equals(
        [IO.Path]::GetDirectoryName($logPath),
        [StringComparison]::OrdinalIgnoreCase
      ) -or
      -not [IO.Path]::GetDirectoryName($recordPath).Equals(
        [IO.Path]::GetDirectoryName($wrapperPath),
        [StringComparison]::OrdinalIgnoreCase
      ) -or
      -not [IO.Path]::GetDirectoryName($recordPath).Equals(
        $expectedAttempt005Physical,
        [StringComparison]::OrdinalIgnoreCase
      )) {
    throw "$Label public artifact paths are not one canonical triple"
  }
  $recordBytes = [IO.File]::ReadAllBytes($recordPath)
  $logBytes = [IO.File]::ReadAllBytes($logPath)
  $recordSha256 = Get-BytesSha256 -Bytes $recordBytes
  $logSha256 = Get-BytesSha256 -Bytes $logBytes
  $wrapperSha256 = Get-BytesSha256 -Bytes $wrapperBytes
  if ($recordSha256 -cne $receipt.evidence_sha256 -or
      $logSha256 -cne $receipt.log_sha256) {
    throw "$Label public record/log changed after wrapper validation"
  }
  $record = Convert-StrictJsonBytes -Bytes $recordBytes -Name "$Label record"
  Assert-ExactJsonObject -Value $record -Name "$Label record" `
    -Properties $recordProperties

  foreach ($field in @(
    "schema", "run_id", "working_directory", "log_path", "evidence_path",
    "expected_exit", "started_utc", "ended_utc",
    "msbuild_disable_node_reuse", "log_sha256"
  )) {
    Assert-JsonString -Value $record.$field -Name "$Label record.$field"
  }
  foreach ($field in @(
    "configured_minimum_available_ram_bytes", "memory_sample_count",
    "peak_working_set_bytes", "peak_private_bytes", "root_pid", "exit_code"
  )) {
    Assert-JsonInteger -Value $record.$field -Name "$Label record.$field"
  }
  foreach ($field in @(
    "elapsed_seconds", "poll_interval_seconds", "cleanup_timeout_seconds",
    "maximum_runtime_seconds"
  )) {
    Assert-JsonNumber -Value $record.$field -Name "$Label record.$field"
  }
  foreach ($field in @(
    "child_process_observed", "timed_out", "low_memory_stop", "valid"
  )) {
    Assert-JsonBoolean -Value $record.$field -Name "$Label record.$field"
  }
  Assert-JsonNull -Value $record.termination_reason `
    -Name "$Label record.termination_reason"
  foreach ($field in @(
    "memory_query_failed_pids", "observed_pids", "emergency_actions",
    "validation_errors"
  )) {
    Assert-JsonArray -Value $record.$field -Name "$Label record.$field"
  }
  foreach ($pidField in @("memory_query_failed_pids", "observed_pids")) {
    foreach ($pidValue in $record.$pidField) {
      Assert-JsonInteger -Value $pidValue -Name "$Label $pidField element"
      if ([int64]$pidValue -le 0) {
        throw "$Label $pidField contains a non-positive PID"
      }
    }
  }
  Assert-StringArrayEquals -Value $record.command -Expected $ExpectedCommand `
    -Name "$Label record.command"

  Assert-ExactJsonObject -Value $record.requested_path_provenance `
    -Name "$Label record.requested_path_provenance" `
    -Properties @("working_directory", "log_path", "evidence_path")
  Assert-ExactJsonObject -Value $record.path_identity_verified `
    -Name "$Label record.path_identity_verified" `
    -Properties @("working_directory", "log_path", "evidence_path")
  foreach ($field in @("working_directory", "log_path", "evidence_path")) {
    Assert-JsonString -Value $record.requested_path_provenance.$field `
      -Name "$Label record.requested_path_provenance.$field"
    Assert-JsonBoolean -Value $record.path_identity_verified.$field `
      -Name "$Label record.path_identity_verified.$field"
    if ($record.path_identity_verified.$field -ne $true) {
      throw "$Label record did not verify $field identity"
    }
  }
  if (-not [IO.Path]::GetFullPath($record.working_directory).Equals(
        $expectedWorkingDirectoryPhysical,
        [StringComparison]::OrdinalIgnoreCase
      ) -or
      $record.working_directory -cne $receipt.working_directory) {
    throw "$Label receipt/record working-directory binding failed"
  }
  foreach ($field in @("working_directory", "log_path", "evidence_path")) {
    if ($record.requested_path_provenance.$field -cne
          $receipt.requested_path_provenance.$field -or
        $record.path_identity_verified.$field -ne
          $receipt.path_identity_verified.$field) {
      throw "$Label receipt/record path provenance differs for $field"
    }
  }
  Assert-ExactJsonObject -Value $record.observed_available_ram_bytes `
    -Name "$Label observed_available_ram_bytes" `
    -Properties @("before", "minimum", "after")
  foreach ($field in @("before", "minimum", "after")) {
    Assert-JsonInteger -Value $record.observed_available_ram_bytes.$field `
      -Name "$Label observed_available_ram_bytes.$field"
  }
  Assert-ExactJsonObject -Value $record.launch_governance `
    -Name "$Label launch_governance" `
    -Properties @(
      "created_suspended", "assigned_before_resume",
      "cpu_affinity_mask", "cpu_rate_hard_cap_percent"
    )
  Assert-JsonBoolean -Value $record.launch_governance.created_suspended `
    -Name "$Label launch_governance.created_suspended"
  Assert-JsonBoolean -Value $record.launch_governance.assigned_before_resume `
    -Name "$Label launch_governance.assigned_before_resume"
  Assert-JsonNull -Value $record.launch_governance.cpu_affinity_mask `
    -Name "$Label launch_governance.cpu_affinity_mask"
  Assert-JsonNull -Value $record.launch_governance.cpu_rate_hard_cap_percent `
    -Name "$Label launch_governance.cpu_rate_hard_cap_percent"
  Assert-ExactJsonObject -Value $record.job_object -Name "$Label job_object" `
    -Properties @(
      "setup_ok", "query_ok", "terminate_job_called",
      "queried_active_process_count_after_cleanup",
      "survivor_pids_after_cleanup"
    )
  foreach ($field in @("setup_ok", "query_ok", "terminate_job_called")) {
    Assert-JsonBoolean -Value $record.job_object.$field `
      -Name "$Label job_object.$field"
  }
  Assert-JsonInteger `
    -Value $record.job_object.queried_active_process_count_after_cleanup `
    -Name "$Label job_object.queried_active_process_count_after_cleanup"
  Assert-JsonArray -Value $record.job_object.survivor_pids_after_cleanup `
    -Name "$Label job_object.survivor_pids_after_cleanup"

  if ($record.schema -cne "official-openvino-owned-process-guard/v1" -or
      $record.run_id -cne $receipt.run_id -or
      $record.evidence_path -cne $recordPath -or
      $record.log_path -cne $logPath -or
      $record.expected_exit -cne $ExpectedExit -or
      [int64]$record.exit_code -ne [int64]$receipt.actual_exit_code -or
      $record.log_sha256 -cne $logSha256 -or
      $record.valid -ne $true -or
      $record.timed_out -ne $false -or
      $record.low_memory_stop -ne $false -or
      $record.msbuild_disable_node_reuse -cne "1" -or
      [int64]$record.configured_minimum_available_ram_bytes -ne 2147483648 -or
      [int64]$record.observed_available_ram_bytes.minimum -lt 2147483648 -or
      [int64]$record.memory_sample_count -lt 1 -or
      [int64]$record.peak_working_set_bytes -le 0 -or
      [int64]$record.peak_private_bytes -le 0 -or
      [double]$record.elapsed_seconds -lt 0.0 -or
      [double]$record.poll_interval_seconds -ne 0.25 -or
      [double]$record.cleanup_timeout_seconds -ne 15.0 -or
      [double]$record.maximum_runtime_seconds -ne 7200.0 -or
      [string]::IsNullOrWhiteSpace($record.started_utc) -or
      [string]::IsNullOrWhiteSpace($record.ended_utc) -or
      [int64]$record.observed_available_ram_bytes.before -lt 0 -or
      [int64]$record.observed_available_ram_bytes.minimum -lt 0 -or
      [int64]$record.observed_available_ram_bytes.after -lt 0 -or
      [int64]$record.observed_available_ram_bytes.minimum -gt
        [int64]$record.observed_available_ram_bytes.before -or
      [int64]$record.observed_available_ram_bytes.minimum -gt
        [int64]$record.observed_available_ram_bytes.after -or
      [int64]$record.root_pid -le 0 -or
      $record.observed_pids.Count -lt 1 -or
      $record.observed_pids -notcontains [int64]$record.root_pid -or
      $record.launch_governance.created_suspended -ne $true -or
      $record.launch_governance.assigned_before_resume -ne $true -or
      $record.job_object.setup_ok -ne $true -or
      $record.job_object.query_ok -ne $true -or
      $record.job_object.terminate_job_called -ne $false -or
      [int64]$record.job_object.queried_active_process_count_after_cleanup -ne 0 -or
      $record.job_object.survivor_pids_after_cleanup.Count -ne 0 -or
      $record.memory_query_failed_pids.Count -ne 0 -or
      $record.emergency_actions.Count -ne 0 -or
      $record.validation_errors.Count -ne 0) {
    throw "$Label guard safety contract failed"
  }
  if (($ExpectedExit -ceq "zero" -and [int64]$record.exit_code -ne 0) -or
      ($ExpectedExit -ceq "nonzero" -and [int64]$record.exit_code -eq 0)) {
    throw "$Label exit code does not match its exact expected-exit class"
  }
  try {
    $logText = $utf8Strict.GetString($logBytes)
  } catch {
    throw "$Label log is not strict UTF-8"
  }
  return [PSCustomObject]@{
    Label = $Label
    Receipt = $receipt
    Record = $record
    WrapperSha256 = $wrapperSha256
    RecordSha256 = $recordSha256
    LogSha256 = $logSha256
    WrapperBytes = [int64]$wrapperBytes.Length
    RecordBytes = [int64]$recordBytes.Length
    LogBytes = [int64]$logBytes.Length
    LogText = $logText
  }
}

$task2LabelSequence = @(
  "t2-cfg-r",
  "t2-red",
  "t2-cfg-g",
  "t2-unit-g",
  "t2-list-g",
  "t2-run-g1",
  "t2-run-g2",
  "t2-plugin-g"
)
$wrapperTemporaryLengths = [ordered]@{}
foreach ($label in $task2LabelSequence) {
  $wrapperTemporaryLengths[$label] = [IO.Path]::Combine(
    $expectedAttempt005Physical,
    "." + $label + "." + ("0" * 32) + ".tmp"
  ).Length
}
if (($wrapperTemporaryLengths.Values | Measure-Object -Maximum).Maximum -ne
      237 -or
    $wrapperTemporaryLengths["t2-plugin-g"] -ne 237) {
  throw "Attempt 005 short-label path budget is not the reviewed 237 chars"
}
$script:task2LabelIndex = 0
$script:task2FirstArtifactHashes = [ordered]@{}
$script:task2CanonicalArtifactDirectory = $null

function Invoke-Task2Guarded {
  param(
    [string]$Label,
    [ValidateSet("Zero", "NonZero")]
    [string]$ExpectedExit,
    [string[]]$Command
  )
  if ($script:task2LabelIndex -ge $task2LabelSequence.Count -or
      $Label -cne $task2LabelSequence[$script:task2LabelIndex]) {
    throw "Task 2 guard labels are missing, duplicated, or out of order"
  }
  $stdout = @(
    & $wrapper `
      -Label $Label `
      -WorkingDirectory $expectedWorkingDirectoryPhysical `
      -EvidenceRoot $guardEvidence `
      -ExpectedExit $ExpectedExit `
      -PythonExecutable $pythonExecutable `
      -PythonSha256 $pythonSha256 `
      -PythonDllSha256 $pythonDllSha256 `
      -Command $Command
  )
  if ($stdout.Count -ne 1 -or
      $stdout[0] -isnot [string] -or
      [string]::IsNullOrWhiteSpace([string]$stdout[0]) -or
      ([string]$stdout[0]).IndexOf("`r") -ge 0 -or
      ([string]$stdout[0]).IndexOf("`n") -ge 0) {
    throw "$Label wrapper must emit exactly one compact stdout JSON string"
  }
  $receiptLine = [string]$stdout[0]
  try {
    $candidate = $receiptLine | & $convertFromJson -ErrorAction Stop
  } catch {
    throw "$Label wrapper stdout is not JSON"
  }
  Assert-ExactJsonObject -Value $candidate -Name "$Label wrapper candidate" `
    -Properties $wrapperProperties
  Assert-JsonString -Value $candidate.evidence_path `
    -Name "$Label wrapper candidate.evidence_path"
  Assert-JsonString -Value $candidate.log_path `
    -Name "$Label wrapper candidate.log_path"
  if (-not [IO.Path]::IsPathRooted($candidate.evidence_path) -or
      -not [IO.Path]::IsPathRooted($candidate.log_path) -or
      -not [IO.Path]::GetFullPath($candidate.evidence_path).Equals(
        [string]$candidate.evidence_path,
        [StringComparison]::OrdinalIgnoreCase
      ) -or
      -not [IO.Path]::GetFullPath($candidate.log_path).Equals(
        [string]$candidate.log_path,
        [StringComparison]::OrdinalIgnoreCase
      ) -or
      [IO.Path]::GetFileName($candidate.evidence_path) -cne
        ($Label + ".json") -or
      [IO.Path]::GetFileName($candidate.log_path) -cne ($Label + ".log") -or
      -not [IO.Path]::GetDirectoryName($candidate.evidence_path).Equals(
        [IO.Path]::GetDirectoryName($candidate.log_path),
        [StringComparison]::OrdinalIgnoreCase
      ) -or
      -not [IO.Path]::GetDirectoryName($candidate.evidence_path).Equals(
        $expectedAttempt005Physical,
        [StringComparison]::OrdinalIgnoreCase
      )) {
    throw "$Label wrapper did not bind one canonical artifact directory"
  }

  # Derive the sidecar destination only from the controller-bound canonical
  # evidence path. Never reopen the mutable R: spelling to publish it.
  $canonicalArtifactDirectory =
    [IO.Path]::GetDirectoryName([string]$candidate.evidence_path)
  if ($null -eq $script:task2CanonicalArtifactDirectory) {
    $script:task2CanonicalArtifactDirectory = $canonicalArtifactDirectory
  } elseif (-not $script:task2CanonicalArtifactDirectory.Equals(
      $canonicalArtifactDirectory,
      [StringComparison]::OrdinalIgnoreCase
    )) {
    throw "$Label canonical artifact directory differs from the first label"
  }
  $wrapperPath =
    [IO.Path]::Combine($canonicalArtifactDirectory, $Label + ".wrapper.json")
  if ([IO.File]::Exists($wrapperPath)) {
    throw "$Label canonical wrapper sidecar already exists"
  }
  $tempPath = [IO.Path]::Combine(
    $canonicalArtifactDirectory,
    "." + $Label + "." + [Guid]::NewGuid().ToString("N") + ".tmp"
  )
  $receiptBytes = $utf8Strict.GetBytes($receiptLine + "`n")
  $stream = [IO.FileStream]::new(
    $tempPath,
    [IO.FileMode]::CreateNew,
    [IO.FileAccess]::Write,
    [IO.FileShare]::None
  )
  try {
    $stream.Write($receiptBytes, 0, $receiptBytes.Length)
    $stream.Flush($true)
  } finally {
    $stream.Dispose()
  }
  try {
    [IO.File]::Move($tempPath, $wrapperPath)
  } catch {
    if ([IO.File]::Exists($tempPath)) { [IO.File]::Delete($tempPath) }
    throw
  }
  $publishedReceiptBytes = [IO.File]::ReadAllBytes($wrapperPath)
  if (-not [Collections.StructuralComparisons]::StructuralEqualityComparer.Equals(
      $receiptBytes,
      $publishedReceiptBytes
    )) {
    throw "$Label published wrapper sidecar differs from captured stdout bytes"
  }

  $expectedExitValue = if ($ExpectedExit -ceq "Zero") {
    "zero"
  } else {
    "nonzero"
  }
  $audit = Assert-Task2ArtifactTriple `
    -Label $Label `
    -ExpectedExit $expectedExitValue `
    -ExpectedCommand $Command `
    -CanonicalArtifactDirectory $canonicalArtifactDirectory
  $script:task2FirstArtifactHashes[$Label] = [ordered]@{
    wrapper = $audit.WrapperSha256
    record = $audit.RecordSha256
    log = $audit.LogSha256
  }
  ++$script:task2LabelIndex
  return $audit
}

$cmake = "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
$configureCommand = @(
  $cmake, "-S", "O:\", "-B", "C:\ov-build\state-observer",
  "-G", "Visual Studio 18 2026", "-A", "x64",
  "-DENABLE_DEBUG_CAPS=ON", "-DENABLE_CPU_DEBUG_CAPS=ON",
  "-DENABLE_TESTS=ON", "-DENABLE_FUNCTIONAL_TESTS=OFF",
  "-DENABLE_HETERO=OFF",
  "-DENABLE_SAMPLES=OFF", "-DENABLE_PYTHON=OFF",
  "-DENABLE_INTEL_GPU=OFF", "-DENABLE_INTEL_NPU=OFF",
  "-DENABLE_OV_ONNX_FRONTEND=OFF", "-DENABLE_OV_PADDLE_FRONTEND=OFF",
  "-DENABLE_OV_TF_FRONTEND=OFF", "-DENABLE_LTO=OFF",
  "-DBUILD_SHARED_LIBS=ON"
)
$msbuild =
  "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe"
$unitProject =
  "C:\ov-build\state-observer\src\plugins\intel_cpu\tests\unit\ov_cpu_unit_tests.vcxproj"
$pluginProject =
  "C:\ov-build\state-observer\src\plugins\intel_cpu\openvino_intel_cpu_plugin.vcxproj"
if ((Get-FileHash -LiteralPath $msbuild -Algorithm SHA256).
      Hash.ToLowerInvariant() -cne
      "106cac9dc67569fe80102cb4a11f49dc48ad44529cabcaf2259e1aa74ad19bec") {
  throw "Pinned x64 MSBuild executable drifted"
}
$linkCapSupervisorPath = [IO.Path]::Combine(
  $expectedWorkingDirectoryPhysical,
  ".superpowers\sdd\task02-attempt005-link-cap-supervisor.ps1"
)
if ((Get-Item -LiteralPath $linkCapSupervisorPath -Force).Length -ne
      38077 -or
    (Get-FileHash -LiteralPath $linkCapSupervisorPath -Algorithm SHA256).
      Hash.ToLowerInvariant() -cne
      "fd005d235b27a532ac5d0536ccb5b429e9c4f554efcd1f6ed5c9a5cf08e0f90b") {
  throw "Attempt 005 link-cap supervisor no longer matches its boundary"
}
$selectedRedCommand = @(
  $msbuild,
  $unitProject,
  "/t:ClCompile",
  "/p:Configuration=Release",
  "/p:Platform=x64",
  "/p:SelectedFiles=O:\src\plugins\intel_cpu\tests\unit\state_allocations_dump_test.cpp",
  "/p:SelectedFilesBuildPCH=false",
  "/p:SelectedFilesBuildModules=false",
  "/p:BuildProjectReferences=false",
  "/p:MultiProcCL=false",
  "/m:1",
  "/nr:false",
  "/v:minimal"
)
$unitGreenCommand = @(
  $controllerExecutable,
  "-NoLogo",
  "-NoProfile",
  "-NonInteractive",
  "-ExecutionPolicy",
  "Bypass",
  "-File",
  $linkCapSupervisorPath,
  "-Mode",
  "Unit"
)
$pluginGreenCommand = @(
  $controllerExecutable,
  "-NoLogo",
  "-NoProfile",
  "-NonInteractive",
  "-ExecutionPolicy",
  "Bypass",
  "-File",
  $linkCapSupervisorPath,
  "-Mode",
  "Plugin"
)
$unitChildCommand = @(
  $msbuild,
  $unitProject,
  "/t:Build",
  "/p:Configuration=Release",
  "/p:Platform=x64",
  "/p:BuildProjectReferences=true",
  "/p:MultiProcCL=false",
  "/p:UseMultiToolTask=false",
  "/p:TrackFileAccess=false",
  "/m:1",
  "/nr:false",
  "/v:minimal"
)
$pluginChildCommand = @(
  $msbuild,
  $pluginProject,
  "/t:Build",
  "/p:Configuration=Release",
  "/p:Platform=x64",
  "/p:BuildProjectReferences=true",
  "/p:MultiProcCL=false",
  "/p:UseMultiToolTask=false",
  "/p:TrackFileAccess=false",
  "/m:1",
  "/nr:false",
  "/v:minimal"
)
Assert-StringArrayEquals `
  -Value $boundaryCheck.link_cap_supervisor.blocking_process_names `
  -Expected @(
    "MSBuild.exe",
    "link.exe",
    "cl.exe",
    "cmake.exe",
    "ctest.exe",
    "ninja.exe",
    "ov_cpu_unit_tests.exe"
  ) `
  -Name "boundary heavy-build blocking process names"
Assert-StringArrayEquals `
  -Value $boundaryCheck.link_cap_supervisor.child_commands.Unit `
  -Expected $unitChildCommand `
  -Name "boundary link-cap Unit child command"
Assert-StringArrayEquals `
  -Value $boundaryCheck.link_cap_supervisor.child_commands.Plugin `
  -Expected $pluginChildCommand `
  -Name "boundary link-cap Plugin child command"
Assert-StringArrayEquals `
  -Value $boundaryCheck.link_cap_supervisor.guarded_supervisor_commands.Unit `
  -Expected $unitGreenCommand `
  -Name "boundary guarded Unit supervisor command"
Assert-StringArrayEquals `
  -Value $boundaryCheck.link_cap_supervisor.guarded_supervisor_commands.Plugin `
  -Expected $pluginGreenCommand `
  -Name "boundary guarded Plugin supervisor command"

$configureRed = Invoke-Task2Guarded `
  -Label "t2-cfg-r" `
  -ExpectedExit Zero `
  -Command $configureCommand

$cache = Get-Content -LiteralPath "C:\ov-build\state-observer\CMakeCache.txt"
foreach ($required in @(
  "ENABLE_DEBUG_CAPS:BOOL=ON",
  "ENABLE_CPU_DEBUG_CAPS:BOOL=ON",
  "ENABLE_TESTS:BOOL=ON",
  "ENABLE_FUNCTIONAL_TESTS:BOOL=OFF",
  "ENABLE_HETERO:BOOL=OFF",
  "BUILD_SHARED_LIBS:BOOL=ON"
)) {
  if ($cache -notcontains $required) {
    throw "Observer configure cache is missing '$required'"
  }
}

$buildRed = Invoke-Task2Guarded `
  -Label "t2-red" `
  -ExpectedExit NonZero `
  -Command $selectedRedCommand

$redLog = $buildRed.LogText
if ([int64]$buildRed.Record.exit_code -ne 1 -or
    [int64]$buildRed.Receipt.actual_exit_code -ne 1) {
  throw "RED must return exact MSBuild exit code 1"
}
$missingHeaderPattern =
  '(?mi)^.*state_allocations_dump_test\.cpp.*error C1083: ' +
  'Cannot open include file: ''utils/state_allocations_dump\.hpp'': ' +
  'No such file or directory.*$'
$environmentalFailurePattern =
  "(?i)CMake Error|could not create named generator|" +
  "generator .+ does not match|VULKAN_SDK|SDK .+ not found|" +
  "ov_hetero_func_tests|openvino::funcSharedTests|Access is denied|" +
  "permission denied|timed out|low[- ]memory|out of memory|" +
  "not enough memory|cannot find the path specified|No space left|disk full"
$nativeDiagnostics = [regex]::Matches(
  $redLog,
  '(?mi)^.*(?:fatal error C\d{4}|error (?:C|LNK)\d{4}):.*$'
)
$allErrorClassDiagnostics = [regex]::Matches(
  $redLog,
  '(?mi)^.*(?:fatal error|error [A-Z]+\d{4}|CMake Error|:\s*error(?:\s|:)).*$'
)
$unexpectedMsBuildPattern =
  '(?mi)^.*error MSB(?!3073:|8066:)\d+:.*$'
if ($redLog -notmatch $missingHeaderPattern -or
    $redLog -match $environmentalFailurePattern -or
    $redLog -match $unexpectedMsBuildPattern -or
    $nativeDiagnostics.Count -ne 1 -or
    $nativeDiagnostics[0].Value -cnotmatch $missingHeaderPattern -or
    $allErrorClassDiagnostics.Count -ne 1 -or
    $allErrorClassDiagnostics[0].Value -cnotmatch $missingHeaderPattern) {
  throw "RED was not exclusively the exact missing Task 2 header compile cause"
}
```

Expected:

- configure exits zero;
- the cache proves both debug options and unit tests are enabled;
- the guarded selected-file `ClCompile` exits nonzero exclusively because the exact fixture cannot
  include the absent `utils/state_allocations_dump.hpp`; the log contains the
  exact MSVC C1083 cause and none of the explicit environment/SDK/generator/
  HETERO/access/timeout/OOM/path-failure patterns;
- both attempt-005 artifact triples pass exact receipt/record/log schema, type,
  path, runtime, and SHA-256 binding; both guard records show
  `configured_minimum_available_ram_bytes == 2147483648`,
  observed minimum RAM at or above that floor,
  `queried_active_process_count_after_cleanup == 0`, empty
  `survivor_pids_after_cleanup`, and `valid == true`;
- no compiler, linker, MSBuild, or test child survives.

Do not proceed if the RED reason is environmental, a path collision, OOM,
timeout, or any failure other than the absent Task 2 contract.

The hidden controller now publishes RED-ready and waits. Include this block in
that controller immediately after the RED assertions and before its Step 7
section:

```powershell
function Write-AtomicControlJson {
  param([object]$Value, [string]$Path)
  $json = ($Value | & $convertToJson -Depth 12 -Compress) + "`n"
  $bytes = $utf8Strict.GetBytes($json)
  $temp = [IO.Path]::Combine(
    [IO.Path]::GetDirectoryName($Path),
    "." + [IO.Path]::GetFileName($Path) + "." +
      [Guid]::NewGuid().ToString("N") + ".tmp"
  )
  $stream = [IO.FileStream]::new(
    $temp,
    [IO.FileMode]::CreateNew,
    [IO.FileAccess]::Write,
    [IO.FileShare]::None
  )
  try {
    $stream.Write($bytes, 0, $bytes.Length)
    $stream.Flush($true)
  } finally {
    $stream.Dispose()
  }
  try {
    [IO.File]::Move($temp, $Path)
  } catch {
    if ([IO.File]::Exists($temp)) { [IO.File]::Delete($temp) }
    throw
  }
  $published = [IO.File]::ReadAllBytes($Path)
  if (-not [Collections.StructuralComparisons]::StructuralEqualityComparer.Equals(
      $bytes,
      $published
    )) {
    throw "Atomic control receipt differs after publication"
  }
}

$controlDirectory = [IO.Path]::Combine(
  $expectedWorkingDirectoryPhysical,
  ".superpowers\sdd"
)
$redReadyPath =
  [IO.Path]::Combine($controlDirectory, "task02-attempt005-red-ready.json")
$greenReadyPath =
  [IO.Path]::Combine($controlDirectory, "task02-attempt005-green-ready.json")
if ([IO.File]::Exists($redReadyPath) -or
    [IO.File]::Exists($greenReadyPath)) {
  throw "Stale Task 2 attempt-005 phase receipt exists"
}
$boundaryPhysical =
  [IO.Path]::Combine($controlDirectory, "task02-attempt005-boundary.json")
$boundaryBytes = [IO.File]::ReadAllBytes($boundaryPhysical)
$boundarySha256 = Get-BytesSha256 -Bytes $boundaryBytes
$tokenBytes = [byte[]]::new(32)
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
try {
  $rng.GetBytes($tokenBytes)
} finally {
  $rng.Dispose()
}
$phaseToken =
  [BitConverter]::ToString($tokenBytes).Replace("-", "").ToLowerInvariant()
$redReady = [ordered]@{
  schema = "openvino-cpu-observer-task02-phase-red-ready/v2"
  token = $phaseToken
  controller_pid = [int64]$PID
  controller_start_filetime_utc = [int64]$controllerStartFileTimeUtc
  controller_creation_filetime_utc = [int64]$controllerCreationFileTimeUtc
  controller_command_line_sha256 = $controllerCommandLineSha256
  controller_script_sha256 = $controllerScriptSha256
  task_plan_sha256 = $taskPlanSha256
  boundary_sha256 = $boundarySha256
  evidence_root = $script:task2CanonicalArtifactDirectory
  completed_labels = @("t2-cfg-r", "t2-red")
  artifact_sha256 = [ordered]@{
    "t2-cfg-r" = $script:task2FirstArtifactHashes["t2-cfg-r"]
    "t2-red" = $script:task2FirstArtifactHashes["t2-red"]
  }
  record_run_ids = [ordered]@{
    "t2-cfg-r" = $configureRed.Record.run_id
    "t2-red" = $buildRed.Record.run_id
  }
  created_utc = [DateTime]::UtcNow.ToString("o")
}
Write-AtomicControlJson -Value $redReady -Path $redReadyPath

$greenDeadline = [DateTime]::UtcNow.AddHours(4)
while (-not [IO.File]::Exists($greenReadyPath)) {
  if ([DateTime]::UtcNow -ge $greenDeadline) {
    throw "Timed out waiting for cryptographically correlated GREEN-ready"
  }
  [Threading.Thread]::Sleep(500)
}
$greenBytes = [IO.File]::ReadAllBytes($greenReadyPath)
$greenReady = Convert-StrictJsonBytes `
  -Bytes $greenBytes -Name "Task 2 GREEN-ready receipt"
Assert-ExactJsonObject -Value $greenReady -Name "GREEN-ready receipt" `
  -Properties @(
    "schema", "token", "controller_pid", "controller_start_filetime_utc",
    "controller_creation_filetime_utc", "controller_command_line_sha256",
    "controller_script_sha256", "task_plan_sha256",
    "boundary_sha256", "evidence_root", "changed_paths", "file_sha256",
    "created_utc"
  )
foreach ($field in @(
  "schema", "token", "controller_command_line_sha256",
  "controller_script_sha256", "task_plan_sha256", "boundary_sha256",
  "evidence_root", "created_utc"
)) {
  Assert-JsonString -Value $greenReady.$field -Name "GREEN-ready.$field"
}
Assert-JsonInteger -Value $greenReady.controller_pid `
  -Name "GREEN-ready.controller_pid"
Assert-JsonInteger -Value $greenReady.controller_start_filetime_utc `
  -Name "GREEN-ready.controller_start_filetime_utc"
Assert-JsonInteger -Value $greenReady.controller_creation_filetime_utc `
  -Name "GREEN-ready.controller_creation_filetime_utc"
Assert-JsonArray -Value $greenReady.changed_paths `
  -Name "GREEN-ready.changed_paths"
Assert-ExactJsonObject -Value $greenReady.file_sha256 `
  -Name "GREEN-ready.file_sha256" -Properties $taskPaths
if ($greenReady.schema -cne
      "openvino-cpu-observer-task02-phase-green-ready/v2" -or
    $greenReady.token -cne $phaseToken -or
    [int64]$greenReady.controller_pid -ne [int64]$PID -or
    [int64]$greenReady.controller_start_filetime_utc -ne
      [int64]$controllerStartFileTimeUtc -or
    [int64]$greenReady.controller_creation_filetime_utc -ne
      [int64]$controllerCreationFileTimeUtc -or
    $greenReady.controller_command_line_sha256 -cne
      $controllerCommandLineSha256 -or
    $greenReady.controller_script_sha256 -cne $controllerScriptSha256 -or
    $greenReady.task_plan_sha256 -cne $taskPlanSha256 -or
    $greenReady.boundary_sha256 -cne $boundarySha256 -or
    -not [IO.Path]::GetFullPath($greenReady.evidence_root).Equals(
      $script:task2CanonicalArtifactDirectory,
      [StringComparison]::OrdinalIgnoreCase
    )) {
  throw "GREEN-ready receipt is stale or belongs to another controller"
}
[string[]]$greenExpectedPaths = @($taskPaths)
[Array]::Sort($greenExpectedPaths, [StringComparer]::Ordinal)
if ($greenReady.changed_paths.Count -ne $greenExpectedPaths.Count) {
  throw "GREEN-ready changed path count is not exact"
}
for ($index = 0; $index -lt $greenExpectedPaths.Count; ++$index) {
  Assert-JsonString -Value $greenReady.changed_paths[$index] `
    -Name "GREEN-ready.changed_paths[$index]"
  if ($greenReady.changed_paths[$index] -cne $greenExpectedPaths[$index]) {
    throw "GREEN-ready changed path differs at index $index"
  }
  $path = $greenExpectedPaths[$index]
  $hashValue = $greenReady.file_sha256.PSObject.Properties[$path].Value
  Assert-JsonString -Value $hashValue -Name "GREEN-ready hash for $path"
  $actualBytes = [IO.File]::ReadAllBytes(
    [IO.Path]::Combine($derivedCore, $path -replace '/', '\')
  )
  if ($hashValue -cne (Get-BytesSha256 -Bytes $actualBytes)) {
    throw "GREEN-ready source hash differs for $path"
  }
}
$resumeChanged = @(
  (& git -C $derivedCore diff --name-only) +
  (& git -C $derivedCore ls-files --others --exclude-standard)
) | Where-Object { $_ } | Sort-Object -Unique
if ($resumeChanged.Count -ne $greenExpectedPaths.Count) {
  throw "Derived tree does not contain the exact five GREEN-ready paths"
}
for ($index = 0; $index -lt $greenExpectedPaths.Count; ++$index) {
  if ($resumeChanged[$index] -cne $greenExpectedPaths[$index]) {
    throw "Derived GREEN-ready path differs at index $index"
  }
}
```

At this wait point the two completed guards already prove zero live Job PIDs,
so the only extra process is the hidden no-profile PowerShell controller. Do not
signal GREEN until all exact Step 4-6 source bodies are present.

## Step 4: Add the retained-capacity accessor exactly

In `O:\src\plugins\intel_cpu\src\cpu_memory.h`, add this include immediately
after `#include <memory>`:

```cpp
#ifdef CPU_DEBUG_CAPS
#    include <optional>
#endif
```

In the public section of `DnnlMemoryBlock`, immediately after
`hasExtBuffer() const noexcept`, add:

```cpp
#ifdef CPU_DEBUG_CAPS
    [[nodiscard]] std::optional<size_t> getAllocatedSize() const noexcept;
#endif
```

In `O:\src\plugins\intel_cpu\src\cpu_memory.cpp`, immediately after
`DnnlMemoryBlock::hasExtBuffer()`, add:

```cpp
#ifdef CPU_DEBUG_CAPS
std::optional<size_t> DnnlMemoryBlock::getAllocatedSize() const noexcept {
    const auto* reuse =
        dynamic_cast<const MemoryBlockWithReuse*>(m_pMemBlock.get());
    if (reuse == nullptr) {
        return std::nullopt;
    }
    return reuse->size();
}
#endif
```

Do not add an accessor to `IMemoryBlock`, export a symbol, expose a public
runtime property, or fall back to `Memory::getSize()`. A non-reuse owner is
unknown, not descriptor-sized physical evidence.

## Step 5: Create the exact private domain-model header

Create `O:\src\plugins\intel_cpu\src\utils\state_allocations_dump.hpp` with
this exact body:

```cpp
// Copyright (C) 2018-2026 Intel Corporation
// SPDX-License-Identifier: Apache-2.0
//

#pragma once

#ifdef CPU_DEBUG_CAPS

#include <cstddef>
#include <cstdint>
#include <optional>
#include <string>
#include <vector>

#include "cpu_types.h"
#include "memory_state.h"

namespace ov::intel_cpu {

inline constexpr size_t kMaxStateAllocationSnapshotBytes = 1024U * 1024U;

enum class StateMemoryRole {
    INPUT,
    OUTPUT,
    KV,
    BEAM,
    SCALE_ZP,
};

struct SnapshotContext {
    uint32_t pid;
    uint64_t observer_request_id;
    std::string correlation_id;
    std::string trigger = "query_state";
    std::string phase;
    std::string observer_plugin_device = "CPU";
};

enum class AllocationOwnerDomain {
    DNNL_MEMORY_BLOCK,
    PLAIN_TENSOR_OWNER,
};

struct ObservedAllocation {
    std::string state_name;
    std::string state_class;
    StateMemoryRole role;
    AllocationOwnerDomain source_owner_domain;
    size_t source_owner_ordinal;
    std::string element_type;
    VectorDims shape;
    VectorDims order;
    VectorDims strides;
    size_t active_descriptor_bytes;
    std::optional<size_t> reserved_backing_bytes;
    bool owned_backing;
    bool external_backing;
};

struct StateAllocationRecord {
    std::string state_name;
    std::string state_class;
    StateMemoryRole role;
    AllocationOwnerDomain owner_domain;
    size_t block_ordinal;
    std::optional<size_t> aliases_block_ordinal;
    std::string element_type;
    VectorDims shape;
    VectorDims order;
    VectorDims strides;
    size_t active_descriptor_bytes;
    std::optional<size_t> reserved_backing_bytes;
    bool owned_backing;
    bool external_backing;
};

struct StateAllocationTotal {
    std::string state_name;
    size_t active_descriptor_bytes = 0;
    size_t unique_reserved_backing_bytes = 0;
    size_t beam_reserved_bytes = 0;
    size_t scale_zp_reserved_bytes = 0;
    size_t total_physical_state_bytes = 0;
};

struct StateAllocationSnapshot {
    uint32_t schema_version = 1;
    uint64_t sequence = 0;
    uint32_t pid = 0;
    uint64_t observer_request_id = 0;
    std::string correlation_id;
    std::string trigger;
    std::string phase;
    std::string observer_plugin_device;
    std::vector<StateAllocationRecord> records;
    std::vector<StateAllocationTotal> state_totals;
    size_t unique_reserved_bytes = 0;
    size_t beam_reserved_bytes = 0;
    size_t scale_zp_reserved_bytes = 0;
    size_t total_physical_state_bytes = 0;
};

StateAllocationSnapshot build_state_allocation_snapshot(
    std::vector<ObservedAllocation> observed,
    uint64_t sequence,
    const SnapshotContext& context);

StateAllocationSnapshot capture_state_allocations(
    const std::vector<MemStatePtr>& states,
    uint64_t sequence,
    const SnapshotContext& context);

std::string serialize_state_allocation_snapshot(
    const StateAllocationSnapshot& snapshot);

}  // namespace ov::intel_cpu

#endif  // CPU_DEBUG_CAPS
```

Every declaration and marker is lexically inside `CPU_DEBUG_CAPS`; the ordinary
translation unit sees no observer declaration.

## Step 6: Create the exact extraction, builder, and serializer

Create `O:\src\plugins\intel_cpu\src\utils\state_allocations_dump.cpp` with
this exact body:

```cpp
// Copyright (C) 2018-2026 Intel Corporation
// SPDX-License-Identifier: Apache-2.0
//

#include "utils/state_allocations_dump.hpp"

#ifdef CPU_DEBUG_CAPS

#include <algorithm>
#include <array>
#include <cstdint>
#include <limits>
#include <map>
#include <memory>
#include <numeric>
#include <optional>
#include <set>
#include <sstream>
#include <string>
#include <tuple>
#include <utility>
#include <vector>

#include "memory_desc/blocked_memory_desc.h"
#include "openvino/core/except.hpp"
#include "utils/plain_tensor.hpp"

namespace ov::intel_cpu {
namespace {

using OwnerKey = std::pair<AllocationOwnerDomain, size_t>;

struct CanonicalOwner {
    size_t block_ordinal;
    std::optional<size_t> reserved_backing_bytes;
    bool owned_backing;
    bool external_backing;
};

size_t checked_add(size_t left, size_t right, const char* field) {
    if (right > std::numeric_limits<size_t>::max() - left) {
        OPENVINO_THROW("state allocation checked addition overflow: ", field);
    }
    return left + right;
}

size_t checked_multiply(size_t left, size_t right, const char* field) {
    if (left != 0 && right > std::numeric_limits<size_t>::max() / left) {
        OPENVINO_THROW(
            "state allocation checked multiplication overflow: ",
            field);
    }
    return left * right;
}

bool is_continuation(uint8_t byte) {
    return (byte & 0xC0U) == 0x80U;
}

bool is_valid_utf8_without_controls(const std::string& value) {
    size_t index = 0;
    while (index < value.size()) {
        const auto first = static_cast<uint8_t>(value[index]);
        uint32_t code_point = 0;
        size_t width = 0;

        if (first <= 0x7FU) {
            code_point = first;
            width = 1;
        } else if (first >= 0xC2U && first <= 0xDFU) {
            code_point = first & 0x1FU;
            width = 2;
        } else if (first >= 0xE0U && first <= 0xEFU) {
            code_point = first & 0x0FU;
            width = 3;
        } else if (first >= 0xF0U && first <= 0xF4U) {
            code_point = first & 0x07U;
            width = 4;
        } else {
            return false;
        }

        if (index + width > value.size()) {
            return false;
        }
        for (size_t offset = 1; offset < width; ++offset) {
            const auto next = static_cast<uint8_t>(value[index + offset]);
            if (!is_continuation(next)) {
                return false;
            }
            code_point = (code_point << 6U) | (next & 0x3FU);
        }

        if ((width == 2 && code_point < 0x80U) ||
            (width == 3 && code_point < 0x800U) ||
            (width == 4 && code_point < 0x10000U) ||
            (code_point >= 0xD800U && code_point <= 0xDFFFU) ||
            code_point > 0x10FFFFU) {
            return false;
        }
        if ((code_point <= 0x1FU) ||
            (code_point >= 0x7FU && code_point <= 0x9FU)) {
            return false;
        }
        index += width;
    }
    return true;
}

void require_text(
    const std::string& value,
    const char* field,
    bool require_non_empty = true) {
    if (require_non_empty && value.empty()) {
        OPENVINO_THROW(field, " must be non-empty");
    }
    if (!is_valid_utf8_without_controls(value)) {
        OPENVINO_THROW(field, " must be valid UTF-8 without controls");
    }
}

bool is_lower_hex_correlation(const std::string& value) {
    if (value.size() != 32) {
        return false;
    }
    return std::all_of(value.begin(), value.end(), [](char character) {
        return (character >= '0' && character <= '9') ||
               (character >= 'a' && character <= 'f');
    });
}

bool is_allowed_phase(const std::string& phase) {
    return phase == "fresh" || phase == "seeded_no_infer" ||
           phase == "post_infer";
}

void validate_context(
    uint64_t sequence,
    uint32_t pid,
    uint64_t observer_request_id,
    const std::string& correlation_id,
    const std::string& trigger,
    const std::string& phase,
    const std::string& observer_plugin_device) {
    if (sequence == 0) {
        OPENVINO_THROW("sequence must be positive");
    }
    if (pid == 0) {
        OPENVINO_THROW("pid must be positive");
    }
    if (observer_request_id == 0) {
        OPENVINO_THROW("observer_request_id must be positive");
    }
    if (!is_lower_hex_correlation(correlation_id)) {
        OPENVINO_THROW(
            "correlation_id must be 32 lowercase hexadecimal characters");
    }
    if (trigger != "query_state") {
        OPENVINO_THROW("trigger must be query_state");
    }
    if (!is_allowed_phase(phase)) {
        OPENVINO_THROW("phase is not allowed");
    }
    if (observer_plugin_device != "CPU") {
        OPENVINO_THROW("observer_plugin_device must be CPU");
    }
}

bool is_known_state_class(const std::string& state_class) {
    return state_class == "VariableStateDoubleBuffer" ||
           state_class == "VariableStateSingleBuffer" ||
           state_class == "VariableStateKVcache";
}

bool role_is_valid_for_class(
    const std::string& state_class,
    StateMemoryRole role) {
    if (state_class == "VariableStateDoubleBuffer" ||
        state_class == "VariableStateSingleBuffer") {
        return role == StateMemoryRole::INPUT ||
               role == StateMemoryRole::OUTPUT;
    }
    if (state_class == "VariableStateKVcache") {
        return role == StateMemoryRole::KV ||
               role == StateMemoryRole::BEAM ||
               role == StateMemoryRole::SCALE_ZP;
    }
    return false;
}

const char* role_name(StateMemoryRole role) {
    switch (role) {
    case StateMemoryRole::INPUT:
        return "input";
    case StateMemoryRole::OUTPUT:
        return "output";
    case StateMemoryRole::KV:
        return "kv";
    case StateMemoryRole::BEAM:
        return "beam";
    case StateMemoryRole::SCALE_ZP:
        return "scale_zp";
    }
    OPENVINO_THROW("unknown state memory role");
}

const char* owner_domain_name(AllocationOwnerDomain domain) {
    switch (domain) {
    case AllocationOwnerDomain::DNNL_MEMORY_BLOCK:
        return "dnnl_memory_block";
    case AllocationOwnerDomain::PLAIN_TENSOR_OWNER:
        return "plain_tensor_owner";
    }
    OPENVINO_THROW("unknown allocation owner domain");
}

void validate_layout(const ObservedAllocation& item) {
    if (item.shape.size() != item.order.size() ||
        item.shape.size() != item.strides.size()) {
        OPENVINO_THROW("shape/order/strides rank mismatch");
    }
    std::vector<size_t> sorted_order = item.order;
    std::sort(sorted_order.begin(), sorted_order.end());
    for (size_t index = 0; index < sorted_order.size(); ++index) {
        if (sorted_order[index] != index) {
            OPENVINO_THROW("order is not a rank permutation");
        }
    }
}

void validate_observed(const ObservedAllocation& item) {
    require_text(item.state_name, "state_name");
    require_text(item.state_class, "state_class");
    require_text(item.element_type, "element_type");
    if (!is_known_state_class(item.state_class)) {
        OPENVINO_THROW("unsupported CPU variable state subclass: ",
                       item.state_class);
    }
    if (!role_is_valid_for_class(item.state_class, item.role)) {
        OPENVINO_THROW("state role is invalid for concrete state class");
    }
    if (item.source_owner_ordinal == 0) {
        OPENVINO_THROW("source owner ordinal must be positive");
    }
    if (item.external_backing &&
        item.source_owner_domain ==
            AllocationOwnerDomain::DNNL_MEMORY_BLOCK) {
        OPENVINO_THROW(
            "external DNNL backing is not physical observer evidence");
    }
    if (!item.owned_backing || item.external_backing) {
        OPENVINO_THROW("emitted allocation must have owned backing");
    }
    if (item.role == StateMemoryRole::SCALE_ZP &&
        item.source_owner_domain !=
            AllocationOwnerDomain::PLAIN_TENSOR_OWNER) {
        OPENVINO_THROW("scale/ZP allocation must use PlainTensor owner domain");
    }
    if (item.role != StateMemoryRole::SCALE_ZP &&
        item.source_owner_domain ==
            AllocationOwnerDomain::PLAIN_TENSOR_OWNER) {
        OPENVINO_THROW("PlainTensor owner domain is reserved for scale/ZP");
    }
    if (item.reserved_backing_bytes.has_value()) {
        if (*item.reserved_backing_bytes == 0) {
            OPENVINO_THROW("retained capacity must be positive");
        }
        if (item.active_descriptor_bytes >
            *item.reserved_backing_bytes) {
            OPENVINO_THROW(
                "active descriptor bytes exceed retained capacity");
        }
    }
    validate_layout(item);
}

template <typename Pointer>
size_t shared_owner_ordinal(
    const Pointer& owner,
    std::vector<Pointer>& first_seen) {
    if (!owner) {
        return 0;
    }
    const std::owner_less<Pointer> less;
    for (size_t index = 0; index < first_seen.size(); ++index) {
        if (!less(owner, first_seen[index]) &&
            !less(first_seen[index], owner)) {
            return index + 1;
        }
    }
    first_seen.push_back(owner);
    return first_seen.size();
}

size_t plain_tensor_active_bytes(const PlainTensor& tensor) {
    if (tensor.m_rank == 0) {
        return 0;
    }
    size_t greatest_element_offset = 0;
    for (size_t index = 0; index < tensor.m_rank; ++index) {
        if (tensor.m_dims[index] == 0) {
            return 0;
        }
        const auto extent = checked_multiply(
            tensor.m_dims[index] - 1,
            tensor.m_strides[index],
            "PlainTensor active extent");
        greatest_element_offset = checked_add(
            greatest_element_offset,
            extent,
            "PlainTensor active extent");
    }
    const auto element_count = checked_add(
        greatest_element_offset,
        1,
        "PlainTensor active elements");
    const auto unpacked_bytes = checked_multiply(
        element_count,
        tensor.m_element_size,
        "PlainTensor active bytes");
    return checked_add(
               unpacked_bytes,
               tensor.m_sub_byte_multiplier - 1,
               "PlainTensor packed rounding") /
           tensor.m_sub_byte_multiplier;
}

ObservedAllocation observe_memory(
    const std::string& state_name,
    const std::string& state_class,
    StateMemoryRole role,
    const MemoryPtr& memory,
    size_t owner_ordinal) {
    if (!memory) {
        OPENVINO_THROW("observe_memory requires a non-null memory");
    }
    const auto blocked =
        memory->getDescWithType<BlockedMemoryDesc>();
    if (!blocked) {
        OPENVINO_THROW("state memory descriptor is not blocked");
    }
    const auto owner = memory->getMemoryBlock();
    if (!owner || owner_ordinal == 0) {
        OPENVINO_THROW("state memory has no shared backing owner");
    }
    const auto dnnl_owner =
        std::dynamic_pointer_cast<DnnlMemoryBlock>(owner);
    const auto retained = dnnl_owner
                              ? dnnl_owner->getAllocatedSize()
                              : std::optional<size_t>{};
    return {
        state_name,
        state_class,
        role,
        AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
        owner_ordinal,
        memory->getPrecision().get_type_name(),
        memory->getStaticDims(),
        blocked->getOrder(),
        blocked->getStrides(),
        memory->getSize(),
        retained,
        !owner->hasExtBuffer(),
        owner->hasExtBuffer(),
    };
}

void append_memory(
    std::vector<ObservedAllocation>& observed,
    std::vector<MemoryBlockPtr>& owners,
    const std::string& state_name,
    const std::string& state_class,
    StateMemoryRole role,
    const MemoryPtr& memory) {
    if (!memory) {
        return;
    }
    const auto owner = memory->getMemoryBlock();
    const auto ordinal = shared_owner_ordinal(owner, owners);
    auto item = observe_memory(
        state_name,
        state_class,
        role,
        memory,
        ordinal);
    if (!item.external_backing &&
        item.reserved_backing_bytes.has_value() &&
        *item.reserved_backing_bytes == 0) {
        return;
    }
    observed.push_back(std::move(item));
}

void append_scale_zp(
    std::vector<ObservedAllocation>& observed,
    std::vector<std::shared_ptr<uint8_t>>& owners,
    const std::string& state_name,
    const PlainTensor& tensor) {
    if (!tensor.m_ptr || tensor.m_capacity == 0) {
        return;
    }
    const auto ordinal = shared_owner_ordinal(tensor.m_ptr, owners);
    VectorDims order(tensor.m_rank);
    std::iota(order.begin(), order.end(), size_t{0});
    observed.push_back({
        state_name,
        "VariableStateKVcache",
        StateMemoryRole::SCALE_ZP,
        AllocationOwnerDomain::PLAIN_TENSOR_OWNER,
        ordinal,
        tensor.get_precision().get_type_name(),
        tensor.shape(),
        std::move(order),
        tensor.get_strides<size_t>(),
        plain_tensor_active_bytes(tensor),
        tensor.m_capacity,
        true,
        false,
    });
}

StateAllocationTotal& state_total(
    StateAllocationSnapshot& snapshot,
    std::map<std::string, size_t>& indexes,
    const std::string& state_name) {
    const auto found = indexes.find(state_name);
    if (found != indexes.end()) {
        return snapshot.state_totals[found->second];
    }
    const auto index = snapshot.state_totals.size();
    indexes.emplace(state_name, index);
    snapshot.state_totals.push_back({state_name});
    return snapshot.state_totals.back();
}

void add_physical_capacity(
    StateAllocationSnapshot& snapshot,
    StateAllocationTotal& total,
    StateMemoryRole role,
    size_t capacity) {
    if (role == StateMemoryRole::BEAM) {
        snapshot.beam_reserved_bytes = checked_add(
            snapshot.beam_reserved_bytes,
            capacity,
            "beam_reserved_bytes");
        total.beam_reserved_bytes = checked_add(
            total.beam_reserved_bytes,
            capacity,
            "state beam_reserved_bytes");
    } else if (role == StateMemoryRole::SCALE_ZP) {
        snapshot.scale_zp_reserved_bytes = checked_add(
            snapshot.scale_zp_reserved_bytes,
            capacity,
            "scale_zp_reserved_bytes");
        total.scale_zp_reserved_bytes = checked_add(
            total.scale_zp_reserved_bytes,
            capacity,
            "state scale_zp_reserved_bytes");
    } else {
        snapshot.unique_reserved_bytes = checked_add(
            snapshot.unique_reserved_bytes,
            capacity,
            "unique_reserved_bytes");
        total.unique_reserved_backing_bytes = checked_add(
            total.unique_reserved_backing_bytes,
            capacity,
            "state unique_reserved_backing_bytes");
    }
    snapshot.total_physical_state_bytes = checked_add(
        snapshot.total_physical_state_bytes,
        capacity,
        "total_physical_state_bytes");
    total.total_physical_state_bytes = checked_add(
        total.total_physical_state_bytes,
        capacity,
        "state total_physical_state_bytes");
}

class BoundedJson {
public:
    void append(const std::string& value) {
        if (value.size() > kMaxStateAllocationSnapshotBytes - text.size()) {
            OPENVINO_THROW(
                "state allocation JSON exceeds 1048576 bytes");
        }
        text += value;
    }

    void append(char value) {
        if (text.size() == kMaxStateAllocationSnapshotBytes) {
            OPENVINO_THROW(
                "state allocation JSON exceeds 1048576 bytes");
        }
        text.push_back(value);
    }

    template <typename Integer>
    void append_number(Integer value) {
        append(std::to_string(value));
    }

    void append_string(const std::string& value) {
        append('"');
        for (const auto raw : value) {
            const auto byte = static_cast<uint8_t>(raw);
            switch (byte) {
            case '"':
                append("\\\"");
                break;
            case '\\':
                append("\\\\");
                break;
            default:
                append(static_cast<char>(byte));
                break;
            }
        }
        append('"');
    }

    std::string take() {
        return std::move(text);
    }

private:
    std::string text;
};

void append_vector(BoundedJson& json, const VectorDims& values) {
    json.append('[');
    for (size_t index = 0; index < values.size(); ++index) {
        if (index != 0) {
            json.append(',');
        }
        json.append_number(values[index]);
    }
    json.append(']');
}

void validate_serializable_record(const StateAllocationRecord& record) {
    require_text(record.state_name, "state_name");
    require_text(record.state_class, "state_class");
    require_text(record.element_type, "element_type");
    if (!record.reserved_backing_bytes.has_value()) {
        OPENVINO_THROW(
            "cannot serialize allocation with unknown retained capacity");
    }
    if (*record.reserved_backing_bytes == 0) {
        OPENVINO_THROW("cannot serialize zero retained capacity");
    }
    if (!record.owned_backing || record.external_backing) {
        OPENVINO_THROW("cannot serialize non-owned allocation");
    }
}

void append_record(BoundedJson& json, const StateAllocationRecord& record) {
    validate_serializable_record(record);
    json.append("{\"state_name\":");
    json.append_string(record.state_name);
    json.append(",\"state_class\":");
    json.append_string(record.state_class);
    json.append(",\"role\":");
    json.append_string(role_name(record.role));
    json.append(",\"owner_domain\":");
    json.append_string(owner_domain_name(record.owner_domain));
    json.append(",\"block_ordinal\":");
    json.append_number(record.block_ordinal);
    json.append(",\"aliases_block_ordinal\":");
    if (record.aliases_block_ordinal.has_value()) {
        json.append_number(*record.aliases_block_ordinal);
    } else {
        json.append("null");
    }
    json.append(",\"element_type\":");
    json.append_string(record.element_type);
    json.append(",\"shape\":");
    append_vector(json, record.shape);
    json.append(",\"order\":");
    append_vector(json, record.order);
    json.append(",\"strides\":");
    append_vector(json, record.strides);
    json.append(",\"active_descriptor_bytes\":");
    json.append_number(record.active_descriptor_bytes);
    json.append(",\"reserved_backing_bytes\":");
    json.append_number(*record.reserved_backing_bytes);
    json.append(",\"owned_backing\":true,\"external_backing\":false}");
}

void append_total(BoundedJson& json, const StateAllocationTotal& total) {
    json.append("{\"state_name\":");
    json.append_string(total.state_name);
    json.append(",\"active_descriptor_bytes\":");
    json.append_number(total.active_descriptor_bytes);
    json.append(",\"unique_reserved_backing_bytes\":");
    json.append_number(total.unique_reserved_backing_bytes);
    json.append(",\"beam_reserved_bytes\":");
    json.append_number(total.beam_reserved_bytes);
    json.append(",\"scale_zp_reserved_bytes\":");
    json.append_number(total.scale_zp_reserved_bytes);
    json.append(",\"total_physical_state_bytes\":");
    json.append_number(total.total_physical_state_bytes);
    json.append('}');
}

}  // namespace

StateAllocationSnapshot build_state_allocation_snapshot(
    std::vector<ObservedAllocation> observed,
    uint64_t sequence,
    const SnapshotContext& context) {
    validate_context(
        sequence,
        context.pid,
        context.observer_request_id,
        context.correlation_id,
        context.trigger,
        context.phase,
        context.observer_plugin_device);

    StateAllocationSnapshot snapshot;
    snapshot.sequence = sequence;
    snapshot.pid = context.pid;
    snapshot.observer_request_id = context.observer_request_id;
    snapshot.correlation_id = context.correlation_id;
    snapshot.trigger = context.trigger;
    snapshot.phase = context.phase;
    snapshot.observer_plugin_device = context.observer_plugin_device;
    snapshot.records.reserve(observed.size());

    std::set<std::pair<std::string, StateMemoryRole>> declarations;
    std::map<std::string, std::string> state_classes;
    std::map<std::string, size_t> state_indexes;
    std::map<OwnerKey, CanonicalOwner> owners;
    size_t next_block_ordinal = 1;

    for (auto& item : observed) {
        validate_observed(item);
        if (!declarations.emplace(item.state_name, item.role).second) {
            OPENVINO_THROW("duplicate logical-state declaration");
        }
        const auto class_entry =
            state_classes.emplace(item.state_name, item.state_class);
        if (!class_entry.second &&
            class_entry.first->second != item.state_class) {
            OPENVINO_THROW("inconsistent state class for state_name");
        }

        auto& total =
            state_total(snapshot, state_indexes, item.state_name);
        total.active_descriptor_bytes = checked_add(
            total.active_descriptor_bytes,
            item.active_descriptor_bytes,
            "state active_descriptor_bytes");

        const OwnerKey key{
            item.source_owner_domain,
            item.source_owner_ordinal,
        };
        const auto found = owners.find(key);
        std::optional<size_t> alias;
        size_t block_ordinal = 0;
        if (found == owners.end()) {
            block_ordinal = next_block_ordinal++;
            owners.emplace(
                key,
                CanonicalOwner{
                    block_ordinal,
                    item.reserved_backing_bytes,
                    item.owned_backing,
                    item.external_backing,
                });
            if (item.reserved_backing_bytes.has_value()) {
                add_physical_capacity(
                    snapshot,
                    total,
                    item.role,
                    *item.reserved_backing_bytes);
            }
        } else {
            block_ordinal = found->second.block_ordinal;
            alias = block_ordinal;
            if (found->second.reserved_backing_bytes !=
                item.reserved_backing_bytes) {
                OPENVINO_THROW("alias changes retained capacity");
            }
            if (found->second.owned_backing != item.owned_backing ||
                found->second.external_backing != item.external_backing) {
                OPENVINO_THROW("alias changes backing ownership");
            }
        }

        snapshot.records.push_back({
            std::move(item.state_name),
            std::move(item.state_class),
            item.role,
            item.source_owner_domain,
            block_ordinal,
            alias,
            std::move(item.element_type),
            std::move(item.shape),
            std::move(item.order),
            std::move(item.strides),
            item.active_descriptor_bytes,
            item.reserved_backing_bytes,
            item.owned_backing,
            item.external_backing,
        });
    }
    return snapshot;
}

StateAllocationSnapshot capture_state_allocations(
    const std::vector<MemStatePtr>& states,
    uint64_t sequence,
    const SnapshotContext& context) {
    std::vector<ObservedAllocation> observed;
    std::vector<MemoryBlockPtr> memory_owners;
    std::vector<std::shared_ptr<uint8_t>> plain_owners;

    for (const auto& state : states) {
        if (!state) {
            OPENVINO_THROW("state list contains a null state");
        }
        const auto& name = state->get_name();
        if (const auto kv =
                std::dynamic_pointer_cast<VariableStateKVcache>(state)) {
            append_memory(
                observed,
                memory_owners,
                name,
                "VariableStateKVcache",
                StateMemoryRole::KV,
                kv->input_mem());
            append_memory(
                observed,
                memory_owners,
                name,
                "VariableStateKVcache",
                StateMemoryRole::BEAM,
                kv->hidden_state_mem());
            append_scale_zp(
                observed,
                plain_owners,
                name,
                kv->get_scale_zp());
        } else if (const auto double_buffer =
                       std::dynamic_pointer_cast<
                           VariableStateDoubleBuffer>(state)) {
            append_memory(
                observed,
                memory_owners,
                name,
                "VariableStateDoubleBuffer",
                StateMemoryRole::INPUT,
                double_buffer->input_mem());
            append_memory(
                observed,
                memory_owners,
                name,
                "VariableStateDoubleBuffer",
                StateMemoryRole::OUTPUT,
                double_buffer->output_mem());
        } else if (const auto single_buffer =
                       std::dynamic_pointer_cast<
                           VariableStateSingleBuffer>(state)) {
            append_memory(
                observed,
                memory_owners,
                name,
                "VariableStateSingleBuffer",
                StateMemoryRole::INPUT,
                single_buffer->input_mem());
            append_memory(
                observed,
                memory_owners,
                name,
                "VariableStateSingleBuffer",
                StateMemoryRole::OUTPUT,
                single_buffer->output_mem());
        } else {
            OPENVINO_THROW(
                "unsupported CPU variable state subclass: ",
                name);
        }
    }

    return build_state_allocation_snapshot(
        std::move(observed),
        sequence,
        context);
}

std::string serialize_state_allocation_snapshot(
    const StateAllocationSnapshot& snapshot) {
    if (snapshot.schema_version != 1) {
        OPENVINO_THROW("unsupported state allocation schema_version");
    }
    validate_context(
        snapshot.sequence,
        snapshot.pid,
        snapshot.observer_request_id,
        snapshot.correlation_id,
        snapshot.trigger,
        snapshot.phase,
        snapshot.observer_plugin_device);

    const auto recomputed_total = checked_add(
        checked_add(
            snapshot.unique_reserved_bytes,
            snapshot.beam_reserved_bytes,
            "serialized physical total"),
        snapshot.scale_zp_reserved_bytes,
        "serialized physical total");
    if (recomputed_total != snapshot.total_physical_state_bytes) {
        OPENVINO_THROW("snapshot physical totals are inconsistent");
    }
    for (const auto& total : snapshot.state_totals) {
        require_text(total.state_name, "state_name");
        const auto state_recomputed = checked_add(
            checked_add(
                total.unique_reserved_backing_bytes,
                total.beam_reserved_bytes,
                "serialized state physical total"),
            total.scale_zp_reserved_bytes,
            "serialized state physical total");
        if (state_recomputed != total.total_physical_state_bytes) {
            OPENVINO_THROW("state physical totals are inconsistent");
        }
    }

    BoundedJson json;
    json.append("{\"schema_version\":");
    json.append_number(snapshot.schema_version);
    json.append(",\"sequence\":");
    json.append_number(snapshot.sequence);
    json.append(",\"pid\":");
    json.append_number(snapshot.pid);
    json.append(",\"observer_request_id\":");
    json.append_number(snapshot.observer_request_id);
    json.append(",\"correlation_id\":");
    json.append_string(snapshot.correlation_id);
    json.append(",\"trigger\":");
    json.append_string(snapshot.trigger);
    json.append(",\"phase\":");
    json.append_string(snapshot.phase);
    json.append(",\"observer_plugin_device\":");
    json.append_string(snapshot.observer_plugin_device);
    json.append(",\"records\":[");
    for (size_t index = 0; index < snapshot.records.size(); ++index) {
        if (index != 0) {
            json.append(',');
        }
        append_record(json, snapshot.records[index]);
    }
    json.append("],\"state_totals\":[");
    for (size_t index = 0; index < snapshot.state_totals.size(); ++index) {
        if (index != 0) {
            json.append(',');
        }
        append_total(json, snapshot.state_totals[index]);
    }
    json.append("],\"unique_reserved_bytes\":");
    json.append_number(snapshot.unique_reserved_bytes);
    json.append(",\"beam_reserved_bytes\":");
    json.append_number(snapshot.beam_reserved_bytes);
    json.append(",\"scale_zp_reserved_bytes\":");
    json.append_number(snapshot.scale_zp_reserved_bytes);
    json.append(",\"total_physical_state_bytes\":");
    json.append_number(snapshot.total_physical_state_bytes);
    json.append('}');
    return json.take();
}

}  // namespace ov::intel_cpu

#endif  // CPU_DEBUG_CAPS
```

The implementation deliberately never calls `VariableStateKVcache::get_state()`
and never uses public tensor materialization. It observes only live private
owners: input/output/KV `MemoryBlockPtr`, hidden beam `MemoryBlockPtr`, and
positive-capacity owned scale/ZP `PlainTensor::m_ptr` control blocks.

After applying the exact Step 4-6 bodies, change to the exact physical parent
recovery worktree and run the following coordinator block as a second fresh
`C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe -NoLogo -NoProfile
-NonInteractive -ExecutionPolicy Bypass -Command <the direct gate below>`.
Materialize the GREEN-signal fence with LF newlines and UTF-8 without BOM as
`R:\.superpowers\sdd\task02-attempt005-green-signal.ps1`. It must be exactly
`37,175` bytes with SHA-256
`9174b0c8bf89457f91e832b2b495caa19d8584f7e230205908fb1c9ba9c5664b`.
The direct gate
checks those exact bytes and invokes the script in the same fresh trusted
process. It signals only the exact still-running hidden controller:

```powershell
$trustedWindowsPowerShell =
  "C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe"
& $trustedWindowsPowerShell `
  -NoLogo `
  -NoProfile `
  -NonInteractive `
  -ExecutionPolicy Bypass `
  -Command {
    $ErrorActionPreference = "Stop"
    $greenSignal =
      "C:\Users\Student\IBM-Granite-Edge-AI-Optimisation-using-TurboQuant\.worktrees\openvino-turboquant-recovery\.superpowers\sdd\task02-attempt005-green-signal.ps1"
    $expectedBytes = [int64]37175
    $expectedSha256 =
      "9174b0c8bf89457f91e832b2b495caa19d8584f7e230205908fb1c9ba9c5664b"
    $expectedRoot =
      "C:\Users\Student\IBM-Granite-Edge-AI-Optimisation-using-TurboQuant\.worktrees\openvino-turboquant-recovery"
    if ($PSVersionTable.PSEdition -cne "Desktop" -or
        $PSVersionTable.PSVersion.Major -ne 5 -or
        $PSVersionTable.PSVersion.Minor -ne 1 -or
        -not [IO.Path]::GetFullPath((Get-Process -Id $PID).Path).Equals(
          "C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",
          [StringComparison]::OrdinalIgnoreCase
        ) -or
        -not [IO.Path]::GetFullPath((Get-Location).ProviderPath).Equals(
          $expectedRoot,
          [StringComparison]::OrdinalIgnoreCase
        )) {
      throw "Task 2 GREEN gate requires exact Windows PowerShell and root"
    }
    $item = Get-Item -LiteralPath $greenSignal -Force
    if (-not $item.FullName.Equals(
          $greenSignal,
          [StringComparison]::OrdinalIgnoreCase
        ) -or
        ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
        $item.Length -ne $expectedBytes -or
        (Get-FileHash -LiteralPath $greenSignal -Algorithm SHA256).
          Hash.ToLowerInvariant() -cne $expectedSha256) {
      throw "Task 2 GREEN-signal direct prelaunch identity gate failed"
    }
    & $greenSignal
  }
if ($LASTEXITCODE -ne 0) {
  throw "Task 2 trusted GREEN-signal process failed with exit $LASTEXITCODE"
}
```

```powershell
$ErrorActionPreference = "Stop"
$root =
  "C:\Users\Student\IBM-Granite-Edge-AI-Optimisation-using-TurboQuant\.worktrees\openvino-turboquant-recovery"
$derivedCore =
  "C:\ov-wb04\2026-07-19\openvino-cpu-state-observer"
$controllerExecutable =
  "C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe"
$controllerScript =
  "C:\Users\Student\IBM-Granite-Edge-AI-Optimisation-using-TurboQuant\.worktrees\openvino-turboquant-recovery\.superpowers\sdd\task02-attempt005-controller.ps1"
$launcherScript =
  "C:\Users\Student\IBM-Granite-Edge-AI-Optimisation-using-TurboQuant\.worktrees\openvino-turboquant-recovery\.superpowers\sdd\task02-attempt005-launcher.ps1"
$expectedLauncherScriptBytes = [int64]46968
$expectedLauncherScriptSha256 =
  "e05a8dd26e2f38c9f2e2a5ebb6fa3a5441834fbd6dfbc8282b111f2a1be0fbf8"
$greenSignalScript =
  "C:\Users\Student\IBM-Granite-Edge-AI-Optimisation-using-TurboQuant\.worktrees\openvino-turboquant-recovery\.superpowers\sdd\task02-attempt005-green-signal.ps1"
$expectedControllerScriptSha256 =
  "9dcb8798ebc65357f8f049d216ba595b2e590378eda67a5877b0580b6004674b"
$controlDirectory = [IO.Path]::Combine($root, ".superpowers\sdd")
$redReadyPath =
  [IO.Path]::Combine($controlDirectory, "task02-attempt005-red-ready.json")
$greenReadyPath =
  [IO.Path]::Combine($controlDirectory, "task02-attempt005-green-ready.json")
$controllerJobReadyPath =
  [IO.Path]::Combine(
    $controlDirectory, "task02-attempt005-controller-job-ready.json"
  )
$timeoutCleanupPath =
  [IO.Path]::Combine(
    $controlDirectory, "task02-attempt005-timeout-cleanup.json"
  )
$controllerJobName = "Local\OpenVINO-Task02-Attempt005-Controller"
if (-not [IO.File]::Exists($redReadyPath) -or
    -not [IO.File]::Exists($controllerJobReadyPath) -or
    [IO.File]::Exists($greenReadyPath) -or
    [IO.File]::Exists($timeoutCleanupPath)) {
  throw "RED-ready is missing or GREEN-ready already exists"
}
$greenSignalItem = Get-Item -LiteralPath $greenSignalScript -Force
if ([string]::IsNullOrWhiteSpace([string]$PSCommandPath) -or
    -not [IO.Path]::GetFullPath($PSCommandPath).Equals(
      $greenSignalScript,
      [StringComparison]::OrdinalIgnoreCase
    ) -or
    ($greenSignalItem.Attributes -band
      [IO.FileAttributes]::ReparsePoint) -ne 0) {
  throw "GREEN-signal script path is not the exact ordinary execution copy"
}
$utf8Strict = [Text.UTF8Encoding]::new($false, $true)
$redReadyText = $utf8Strict.GetString([IO.File]::ReadAllBytes($redReadyPath))
$convertFromJson = $ExecutionContext.InvokeCommand.GetCommand(
  "Microsoft.PowerShell.Utility\ConvertFrom-Json",
  [Management.Automation.CommandTypes]::Cmdlet
)
if ($null -eq $convertFromJson -or
    $convertFromJson -isnot [Management.Automation.CmdletInfo] -or
    $convertFromJson.ModuleName -cne "Microsoft.PowerShell.Utility" -or
    $convertFromJson.ImplementingType.FullName -cne
      "Microsoft.PowerShell.Commands.ConvertFromJsonCommand") {
  throw "Coordinator JSON parser is not the trusted PSHOME cmdlet"
}
$redReady = $redReadyText | & $convertFromJson -ErrorAction Stop
$redProperties = @()
foreach ($property in $redReady.PSObject.Properties) {
  $redProperties += [string]$property.Name
}
$expectedRedProperties = @(
  "schema", "token", "controller_pid", "controller_start_filetime_utc",
  "controller_creation_filetime_utc", "controller_command_line_sha256",
  "controller_script_sha256", "task_plan_sha256",
  "boundary_sha256", "evidence_root", "completed_labels",
  "artifact_sha256", "record_run_ids", "created_utc"
)
if ($redReady -isnot [PSCustomObject] -or
    $redProperties.Count -ne $expectedRedProperties.Count) {
  throw "RED-ready shape is not exact"
}
foreach ($property in $expectedRedProperties) {
  if ($redProperties -cnotcontains $property) {
    throw "RED-ready is missing '$property'"
  }
}
if ($redReady.schema -isnot [string] -or
    $redReady.token -isnot [string] -or
    $redReady.task_plan_sha256 -isnot [string] -or
    $redReady.boundary_sha256 -isnot [string] -or
    $redReady.evidence_root -isnot [string] -or
    $redReady.created_utc -isnot [string] -or
    ($redReady.controller_pid -isnot [int] -and
      $redReady.controller_pid -isnot [long]) -or
    ($redReady.controller_start_filetime_utc -isnot [int] -and
      $redReady.controller_start_filetime_utc -isnot [long]) -or
    ($redReady.controller_creation_filetime_utc -isnot [int] -and
      $redReady.controller_creation_filetime_utc -isnot [long]) -or
    $redReady.controller_command_line_sha256 -isnot [string] -or
    $redReady.controller_script_sha256 -isnot [string] -or
    $redReady.completed_labels -isnot [object[]] -or
    $redReady.artifact_sha256 -isnot [PSCustomObject] -or
    $redReady.record_run_ids -isnot [PSCustomObject] -or
    $redReady.schema -cne
      "openvino-cpu-observer-task02-phase-red-ready/v2" -or
    $redReady.token -cnotmatch "^[0-9a-f]{64}$" -or
    $redReady.task_plan_sha256 -cnotmatch "^[0-9a-f]{64}$" -or
    $redReady.boundary_sha256 -cnotmatch "^[0-9a-f]{64}$" -or
    $redReady.controller_command_line_sha256 -cnotmatch "^[0-9a-f]{64}$" -or
    $redReady.controller_script_sha256 -cne
      $expectedControllerScriptSha256 -or
    $redReady.completed_labels.Count -ne 2 -or
    $redReady.completed_labels[0] -cne "t2-cfg-r" -or
    $redReady.completed_labels[1] -cne "t2-red") {
  throw "RED-ready type/value contract failed"
}
function Get-Task2StringSha256 {
  param([Parameter(Mandatory = $true)][string]$Value)
  $bytes = [Text.UTF8Encoding]::new($false, $true).GetBytes($Value)
  $sha = [Security.Cryptography.SHA256]::Create()
  try {
    $digest = $sha.ComputeHash($bytes)
  } finally {
    $sha.Dispose()
  }
  [BitConverter]::ToString($digest).Replace("-", "").ToLowerInvariant()
}
function Assert-ExactTask2Controller {
  param([Parameter(Mandatory = $true)][Diagnostics.Process]$Process)
  $Process.Refresh()
  if ($Process.HasExited -or
      -not [IO.Path]::GetFullPath($Process.MainModule.FileName).Equals(
        $controllerExecutable,
        [StringComparison]::OrdinalIgnoreCase
      )) {
    throw "Hidden Task 2 controller process handle is not exact"
  }
  $nativeCreationFileTimeUtc = [int64]0
  $nativeExitFileTimeUtc = [int64]0
  $nativeKernelFileTimeUtc = [int64]0
  $nativeUserFileTimeUtc = [int64]0
  if (-not [Task02ControllerJobTimeoutNative]::GetProcessTimes(
      $Process.Handle,
      [ref]$nativeCreationFileTimeUtc,
      [ref]$nativeExitFileTimeUtc,
      [ref]$nativeKernelFileTimeUtc,
      [ref]$nativeUserFileTimeUtc
    ) -or
    $nativeCreationFileTimeUtc -ne
      [int64]$redReady.controller_start_filetime_utc -or
    $nativeCreationFileTimeUtc -ne
      [int64]$redReady.controller_creation_filetime_utc) {
    throw "Hidden Task 2 controller native creation FILETIME is not exact"
  }
  $rows = @(
    Get-CimInstance -ClassName Win32_Process -Property @(
      "ProcessId", "ExecutablePath", "CommandLine"
    ) | Where-Object { [int64]$_.ProcessId -eq [int64]$Process.Id }
  )
  if ($rows.Count -ne 1 -or
      -not [IO.Path]::GetFullPath(
        [string]$rows[0].ExecutablePath
      ).Equals(
        $controllerExecutable,
        [StringComparison]::OrdinalIgnoreCase
      ) -or
      (Get-Task2StringSha256 -Value ([string]$rows[0].CommandLine)) -cne
        [string]$redReady.controller_command_line_sha256 -or
      (Get-FileHash -LiteralPath $controllerScript -Algorithm SHA256).
        Hash.ToLowerInvariant() -cne
        [string]$redReady.controller_script_sha256) {
    throw "Hidden Task 2 controller creation, command, or script identity changed"
  }
}

if (-not ("Task02ControllerJobTimeoutNative" -as [type])) {
  Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class Task02ControllerJobTimeoutNative {
    [StructLayout(LayoutKind.Sequential)]
    public struct IO_COUNTERS {
        public UInt64 ReadOperationCount;
        public UInt64 WriteOperationCount;
        public UInt64 OtherOperationCount;
        public UInt64 ReadTransferCount;
        public UInt64 WriteTransferCount;
        public UInt64 OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct JOBOBJECT_BASIC_LIMIT_INFORMATION {
        public Int64 PerProcessUserTimeLimit;
        public Int64 PerJobUserTimeLimit;
        public UInt32 LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public UInt32 ActiveProcessLimit;
        public UIntPtr Affinity;
        public UInt32 PriorityClass;
        public UInt32 SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct JOBOBJECT_BASIC_ACCOUNTING_INFORMATION {
        public Int64 TotalUserTime;
        public Int64 TotalKernelTime;
        public Int64 ThisPeriodTotalUserTime;
        public Int64 ThisPeriodTotalKernelTime;
        public UInt32 TotalPageFaultCount;
        public UInt32 TotalProcesses;
        public UInt32 ActiveProcesses;
        public UInt32 TotalTerminatedProcesses;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr OpenJobObjectW(
        UInt32 desiredAccess,
        bool inheritHandle,
        string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool QueryInformationJobObject(
        IntPtr job,
        Int32 informationClass,
        IntPtr information,
        UInt32 informationLength,
        out UInt32 returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsProcessInJob(
        IntPtr process,
        IntPtr job,
        out bool result);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetProcessTimes(
        IntPtr process,
        out Int64 creationTime,
        out Int64 exitTime,
        out Int64 kernelTime,
        out Int64 userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool TerminateJobObject(
        IntPtr job,
        UInt32 exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr handle);
}
'@
}
if ([IntPtr]::Size -ne 8 -or
    [Runtime.InteropServices.Marshal]::SizeOf(
      [type][Task02ControllerJobTimeoutNative+JOBOBJECT_EXTENDED_LIMIT_INFORMATION]
    ) -ne 144 -or
    [Runtime.InteropServices.Marshal]::SizeOf(
      [type][Task02ControllerJobTimeoutNative+JOBOBJECT_BASIC_ACCOUNTING_INFORMATION]
    ) -ne 48 -or
    @(Get-Process -Name csc -ErrorAction SilentlyContinue).Count -ne 0) {
  throw "GREEN controller-Job native bootstrap was not exact"
}
$controllerJobLimitFlags = [uint32]0x00002000
$jobObjectBasicAccountingInformation = 1
$jobObjectBasicProcessIdList = 3
$jobObjectExtendedLimitInformation = 9

function Get-Task2TimeoutJobLimits {
  param([Parameter(Mandatory = $true)][IntPtr]$JobHandle)
  $type =
    [type][Task02ControllerJobTimeoutNative+JOBOBJECT_EXTENDED_LIMIT_INFORMATION]
  $size = [Runtime.InteropServices.Marshal]::SizeOf([type]$type)
  $buffer = [Runtime.InteropServices.Marshal]::AllocHGlobal($size)
  try {
    $returned = [uint32]0
    if (-not [Task02ControllerJobTimeoutNative]::QueryInformationJobObject(
        $JobHandle, $jobObjectExtendedLimitInformation, $buffer,
        [uint32]$size, [ref]$returned
      ) -or $returned -ne $size) {
      $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
      throw "GREEN controller Job class-9 query failed: $errorCode/$returned"
    }
    [Runtime.InteropServices.Marshal]::PtrToStructure(
      $buffer, [type]$type
    )
  } finally {
    [Runtime.InteropServices.Marshal]::FreeHGlobal($buffer)
  }
}

function Assert-Task2TimeoutJobLimits {
  param([Parameter(Mandatory = $true)][IntPtr]$JobHandle)
  $limits = Get-Task2TimeoutJobLimits -JobHandle $JobHandle
  if ([uint32]$limits.BasicLimitInformation.LimitFlags -ne
        $controllerJobLimitFlags -or
      $limits.BasicLimitInformation.PerProcessUserTimeLimit -ne 0 -or
      $limits.BasicLimitInformation.PerJobUserTimeLimit -ne 0 -or
      $limits.BasicLimitInformation.MinimumWorkingSetSize.ToUInt64() -ne 0 -or
      $limits.BasicLimitInformation.MaximumWorkingSetSize.ToUInt64() -ne 0 -or
      $limits.BasicLimitInformation.ActiveProcessLimit -ne 0 -or
      $limits.BasicLimitInformation.Affinity.ToUInt64() -ne 0 -or
      $limits.BasicLimitInformation.PriorityClass -ne 32 -or
      $limits.BasicLimitInformation.SchedulingClass -ne 5 -or
      $limits.ProcessMemoryLimit.ToUInt64() -ne 0 -or
      $limits.JobMemoryLimit.ToUInt64() -ne 0) {
    throw "GREEN controller Job limit readback was not exact"
  }
}

function Get-Task2TimeoutJobPidList {
  param([Parameter(Mandatory = $true)][IntPtr]$JobHandle)
  $capacity = 16
  while ($capacity -le 4096) {
    $size = 8 + ($capacity * [IntPtr]::Size)
    $buffer = [Runtime.InteropServices.Marshal]::AllocHGlobal($size)
    try {
      [Runtime.InteropServices.Marshal]::WriteInt64($buffer, 0, [int64]0)
      $returned = [uint32]0
      $ok =
        [Task02ControllerJobTimeoutNative]::QueryInformationJobObject(
          $JobHandle, $jobObjectBasicProcessIdList, $buffer,
          [uint32]$size, [ref]$returned
        )
      $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
      if (-not $ok) {
        if ($errorCode -ne 234) {
          throw "GREEN controller Job PID-list query failed: $errorCode/$returned"
        }
        $required = [int64]($capacity * 2)
        if ($required -le $capacity -or $required -gt 4096) {
          throw "GREEN controller Job PID-list growth exceeded 4096"
        }
        $capacity = [int]$required
        continue
      }
      if ($returned -lt 8) {
        throw "GREEN controller Job PID-list query failed: $errorCode/$returned"
      }
      $assigned = [Runtime.InteropServices.Marshal]::ReadInt32($buffer, 0)
      $listed = [Runtime.InteropServices.Marshal]::ReadInt32($buffer, 4)
      if ($assigned -lt 0 -or $listed -lt 0 -or $assigned -lt $listed) {
        throw "GREEN controller Job PID-list counts are invalid"
      }
      if ($assigned -gt $listed -or $assigned -gt $capacity -or
          $listed -gt $capacity) {
        $required = [Math]::Max(
          [int64]($capacity * 2),
          [int64]$assigned
        )
        if ($required -le $capacity -or $required -gt 4096) {
          throw "GREEN controller Job PID-list growth exceeded 4096"
        }
        $capacity = [int]$required
        continue
      }
      if ($returned -lt (8 + ($listed * [IntPtr]::Size))) {
        throw "GREEN controller Job PID-list success returned a truncated buffer"
      }
      $processIds = @()
      for ($index = 0; $index -lt $listed; ++$index) {
        $processIds += [int64][Runtime.InteropServices.Marshal]::ReadInt64(
          $buffer, 8 + ($index * [IntPtr]::Size)
        )
      }
      return [PSCustomObject][ordered]@{
        returned_bytes = [uint32]$returned
        capacity = [int64]$capacity
        assigned_process_count = [int64]$assigned
        listed_process_count = [int64]$listed
        process_ids = @($processIds)
      }
    } finally {
      [Runtime.InteropServices.Marshal]::FreeHGlobal($buffer)
    }
  }
  throw "GREEN controller Job PID-list query exhausted bounded growth"
}

function Get-Task2TimeoutJobAccounting {
  param([Parameter(Mandatory = $true)][IntPtr]$JobHandle)
  $type =
    [type][Task02ControllerJobTimeoutNative+JOBOBJECT_BASIC_ACCOUNTING_INFORMATION]
  $size = [Runtime.InteropServices.Marshal]::SizeOf([type]$type)
  $buffer = [Runtime.InteropServices.Marshal]::AllocHGlobal($size)
  try {
    $returned = [uint32]0
    if (-not [Task02ControllerJobTimeoutNative]::QueryInformationJobObject(
        $JobHandle, $jobObjectBasicAccountingInformation, $buffer,
        [uint32]$size, [ref]$returned
      ) -or $returned -ne $size) {
      $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
      throw "GREEN controller Job accounting query failed: $errorCode/$returned"
    }
    [Runtime.InteropServices.Marshal]::PtrToStructure(
      $buffer, [type]$type
    )
  } finally {
    [Runtime.InteropServices.Marshal]::FreeHGlobal($buffer)
  }
}

function Wait-Task2TimeoutJobEmpty {
  param(
    [Parameter(Mandatory = $true)][IntPtr]$JobHandle,
    [Parameter(Mandatory = $true)][Diagnostics.Process]$Process
  )
  if (-not $Process.WaitForExit(30000)) {
    throw "Timed-out controller root did not become signaled"
  }
  $deadline = [DateTime]::UtcNow.AddSeconds(30)
  do {
    $pidList = Get-Task2TimeoutJobPidList -JobHandle $JobHandle
    $accounting = Get-Task2TimeoutJobAccounting -JobHandle $JobHandle
    if ($pidList.assigned_process_count -eq 0 -and
        $pidList.listed_process_count -eq 0 -and
        @($pidList.process_ids).Count -eq 0 -and
        $accounting.ActiveProcesses -eq 0) {
      break
    }
    if ([DateTime]::UtcNow -ge $deadline) {
      throw "Timed-out controller Job did not become empty"
    }
    [Threading.Thread]::Sleep(100)
  } while ($true)
  [PSCustomObject][ordered]@{
    pid_list_returned_bytes = [uint32]$pidList.returned_bytes
    pid_list_capacity = [int64]$pidList.capacity
    assigned_process_count = [int64]$pidList.assigned_process_count
    listed_process_count = [int64]$pidList.listed_process_count
    accounting_total_processes = [uint32]$accounting.TotalProcesses
    accounting_active_processes = [uint32]$accounting.ActiveProcesses
    accounting_returned_bytes = 48
  }
}

function Wait-Task2TimeoutStableHeavyEmpty {
  $heavyNames = [string[]]@(
    "MSBuild.exe", "link.exe", "cl.exe", "cmake.exe", "ctest.exe",
    "ninja.exe", "ov_cpu_unit_tests.exe"
  )
  $stable = 0
  $stableDeadline = [DateTime]::UtcNow.AddMinutes(5)
  while ($stable -lt 60) {
    $heavyRows = @(
      Get-CimInstance -ClassName Win32_Process -Property @(
        "ProcessId", "CreationDate", "Name"
      ) -OperationTimeoutSec 10 -ErrorAction Stop |
        Where-Object { $heavyNames -ccontains [string]$_.Name }
    )
    if ($heavyRows.Count -eq 0) { $stable++ } else { $stable = 0 }
    if ($stable -lt 60 -and [DateTime]::UtcNow -ge $stableDeadline) {
      $observedBlockers = @(
        $heavyRows | Sort-Object Name, ProcessId | ForEach-Object {
          "{0}:pid={1}:created={2}" -f
            [string]$_.Name,
            [int64]$_.ProcessId,
            [string]$_.CreationDate
        }
      )
      if ($observedBlockers.Count -eq 0) {
        $observedBlockers = @("<none-in-final-sample>")
      }
      throw (
        "GREEN global-heavy stability deadline expired at $stable/60 empty " +
        "samples; observed blockers: " + ($observedBlockers -join ", ")
      )
    }
    if ($stable -lt 60) { [Threading.Thread]::Sleep(500) }
  }
  $stable
}

$controller = [Diagnostics.Process]::GetProcessById([int]$redReady.controller_pid)
Assert-ExactTask2Controller -Process $controller
$controllerJobHandle =
  [Task02ControllerJobTimeoutNative]::OpenJobObjectW(
    [uint32]0x000C,
    $false,
    $controllerJobName
  )
if ($controllerJobHandle -eq [IntPtr]::Zero) {
  $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
  throw "GREEN could not open the exact named controller Job: $errorCode"
}
try {
  Assert-Task2TimeoutJobLimits -JobHandle $controllerJobHandle
  $controllerInJob = $false
  if (-not [Task02ControllerJobTimeoutNative]::IsProcessInJob(
      $controller.Handle,
      $controllerJobHandle,
      [ref]$controllerInJob
    ) -or -not $controllerInJob) {
    throw "GREEN controller handle is not in the exact named Job"
  }
  $controllerJobPidList =
    Get-Task2TimeoutJobPidList -JobHandle $controllerJobHandle
  if ($controllerJobPidList.process_ids -notcontains [int64]$controller.Id) {
    throw "GREEN controller Job PID list omits the exact controller"
  }
} finally {
  if ($controllerJobHandle -ne [IntPtr]::Zero) {
    if (-not [Task02ControllerJobTimeoutNative]::CloseHandle(
        $controllerJobHandle
      )) {
      $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
      throw "GREEN pre-signal controller Job handle close failed: $errorCode"
    }
    $controllerJobHandle = [IntPtr]::Zero
  }
}
if ($controllerJobHandle -ne [IntPtr]::Zero) {
  throw "GREEN retained a coordinator Job handle before signalling"
}

$taskPaths = @(
  "src/plugins/intel_cpu/src/cpu_memory.h",
  "src/plugins/intel_cpu/src/cpu_memory.cpp",
  "src/plugins/intel_cpu/src/utils/state_allocations_dump.hpp",
  "src/plugins/intel_cpu/src/utils/state_allocations_dump.cpp",
  "src/plugins/intel_cpu/tests/unit/state_allocations_dump_test.cpp"
)
[string[]]$changed = @(
  (& git -C $derivedCore diff --name-only) +
  (& git -C $derivedCore ls-files --others --exclude-standard)
) | Where-Object { $_ } | Sort-Object -Unique
[string[]]$expectedChanged = @($taskPaths)
[Array]::Sort($expectedChanged, [StringComparer]::Ordinal)
if ($changed.Count -ne $expectedChanged.Count) {
  throw "Coordinator found a non-exact derived change count"
}
for ($index = 0; $index -lt $expectedChanged.Count; ++$index) {
  if ($changed[$index] -cne $expectedChanged[$index]) {
    throw "Coordinator derived path differs at index $index"
  }
}

function Get-FileSha256 {
  param([string]$Path)
  $stream = [IO.File]::Open(
    $Path,
    [IO.FileMode]::Open,
    [IO.FileAccess]::Read,
    [IO.FileShare]::Read
  )
  $sha = [Security.Cryptography.SHA256]::Create()
  try {
    $digest = $sha.ComputeHash($stream)
  } finally {
    $sha.Dispose()
    $stream.Dispose()
  }
  return [BitConverter]::ToString($digest).Replace("-", "").ToLowerInvariant()
}

$fileSha256 = [ordered]@{}
foreach ($path in $expectedChanged) {
  $fileSha256[$path] = Get-FileSha256 -Path (
    [IO.Path]::Combine($derivedCore, $path -replace '/', '\')
  )
}
$boundaryPath =
  [IO.Path]::Combine($controlDirectory, "task02-attempt005-boundary.json")
$planPath = [IO.Path]::Combine(
  $root,
  "docs\superpowers\plans\2026-07-28-openvino-cpu-kv-allocation-observability-task-02.md"
)
$controllerJobReadyBytes =
  [IO.File]::ReadAllBytes($controllerJobReadyPath)
$controllerJobReady =
  $utf8Strict.GetString($controllerJobReadyBytes) |
    & $convertFromJson -ErrorAction Stop
$controllerJobReadySha256 =
  Get-FileSha256 -Path $controllerJobReadyPath
$expectedJobReadyProperties = @(
  "schema", "job_name", "job_limit_flags", "create_process_flags",
  "proc_thread_attribute_job_list", "startupinfoex_size", "size_probe_error",
  "controller_pid", "controller_start_filetime_utc",
  "controller_creation_filetime_utc", "controller_command_line_sha256",
  "controller_script_sha256", "suspended_pid_list_returned_bytes",
  "assigned_at_create", "job_member_before_resume",
  "resume_previous_suspend_count", "duplicate_same_access",
  "duplicate_inheritable", "controller_owned_handle",
  "launcher_handle_closed_before_publish", "launcher_identity", "created_utc"
)
$actualJobReadyProperties = @(
  $controllerJobReady.PSObject.Properties |
    ForEach-Object { [string]$_.Name }
)
$expectedLauncherIdentityProperties = @(
  "path", "bytes", "sha256", "reparse", "authentication"
)
$actualLauncherIdentityProperties = @(
  $controllerJobReady.launcher_identity.PSObject.Properties |
    ForEach-Object { [string]$_.Name }
)
$launcherScriptItem = Get-Item -LiteralPath $launcherScript -Force
$launcherScriptSha256 = Get-FileSha256 -Path $launcherScript
if ($controllerJobReady -isnot [PSCustomObject] -or
    $actualJobReadyProperties.Count -ne $expectedJobReadyProperties.Count -or
    @(
      $expectedJobReadyProperties | Where-Object {
        $actualJobReadyProperties -cnotcontains $_
      }
    ).Count -ne 0 -or
    $controllerJobReady.schema -cne
      "openvino-cpu-observer-task02-controller-job-ready/v2" -or
    $controllerJobReady.job_name -cne $controllerJobName -or
    [uint32]$controllerJobReady.job_limit_flags -ne 8192 -or
    [uint32]$controllerJobReady.create_process_flags -ne 134742020 -or
    [uint32]$controllerJobReady.proc_thread_attribute_job_list -ne 131085 -or
    [uint32]$controllerJobReady.startupinfoex_size -ne 112 -or
    [uint32]$controllerJobReady.size_probe_error -ne 122 -or
    [int64]$controllerJobReady.controller_pid -ne
      [int64]$redReady.controller_pid -or
    [int64]$controllerJobReady.controller_start_filetime_utc -ne
      [int64]$redReady.controller_start_filetime_utc -or
    [int64]$controllerJobReady.controller_creation_filetime_utc -ne
      [int64]$redReady.controller_creation_filetime_utc -or
    $controllerJobReady.controller_command_line_sha256 -cne
      [string]$redReady.controller_command_line_sha256 -or
    $controllerJobReady.controller_script_sha256 -cne
      [string]$redReady.controller_script_sha256 -or
    [bool]$controllerJobReady.assigned_at_create -ne $true -or
    [bool]$controllerJobReady.job_member_before_resume -ne $true -or
    [uint32]$controllerJobReady.resume_previous_suspend_count -ne 1 -or
    [bool]$controllerJobReady.duplicate_same_access -ne $true -or
    [bool]$controllerJobReady.duplicate_inheritable -ne $false -or
    [bool]$controllerJobReady.controller_owned_handle -ne $true -or
    [bool]$controllerJobReady.launcher_handle_closed_before_publish -ne
      $true -or
    $controllerJobReady.launcher_identity -isnot [PSCustomObject] -or
    $actualLauncherIdentityProperties.Count -ne
      $expectedLauncherIdentityProperties.Count -or
    @(
      $expectedLauncherIdentityProperties | Where-Object {
        $actualLauncherIdentityProperties -cnotcontains $_
      }
    ).Count -ne 0 -or
    -not [IO.Path]::GetFullPath(
      [string]$controllerJobReady.launcher_identity.path
    ).Equals(
      $launcherScript,
      [StringComparison]::OrdinalIgnoreCase
    ) -or
    -not [IO.Path]::GetFullPath($launcherScriptItem.FullName).Equals(
      $launcherScript,
      [StringComparison]::OrdinalIgnoreCase
    ) -or
    ($launcherScriptItem.Attributes -band
      [IO.FileAttributes]::ReparsePoint) -ne 0 -or
    [int64]$launcherScriptItem.Length -ne $expectedLauncherScriptBytes -or
    $launcherScriptSha256 -cne $expectedLauncherScriptSha256 -or
    [int64]$controllerJobReady.launcher_identity.bytes -ne
      $expectedLauncherScriptBytes -or
    [string]$controllerJobReady.launcher_identity.sha256 -cne
      $expectedLauncherScriptSha256 -or
    [bool]$controllerJobReady.launcher_identity.reparse -ne $false -or
    [string]$controllerJobReady.launcher_identity.authentication -cne
      "direct-prelaunch-parameters-plus-launcher-self-readback") {
  throw "GREEN controller Job-ready contract is not exact"
}
$boundary = $utf8Strict.GetString([IO.File]::ReadAllBytes($boundaryPath)) |
  & $convertFromJson -ErrorAction Stop
$greenSignalSha256 = Get-FileSha256 -Path $greenSignalScript
if ($boundary.schema -cne
      "openvino-cpu-observer-task02-boundary/v4" -or
    @($boundary.control_scripts.PSObject.Properties).Count -ne 4 -or
    -not [IO.Path]::GetFullPath(
      [string]$boundary.control_scripts.green_signal.path
    ).Equals(
      $greenSignalScript,
      [StringComparison]::OrdinalIgnoreCase
    ) -or
    [int64]$boundary.control_scripts.green_signal.bytes -ne
      [int64]$greenSignalItem.Length -or
    [string]$boundary.control_scripts.green_signal.sha256 -cne
      $greenSignalSha256 -or
    [bool]$boundary.control_scripts.green_signal.reparse -ne $false -or
    -not [IO.Path]::GetFullPath(
      [string]$boundary.control_scripts.launcher.path
    ).Equals(
      $launcherScript,
      [StringComparison]::OrdinalIgnoreCase
    ) -or
    [int64]$boundary.control_scripts.launcher.bytes -ne
      $expectedLauncherScriptBytes -or
    [string]$boundary.control_scripts.launcher.sha256 -cne
      $expectedLauncherScriptSha256 -or
    [bool]$boundary.control_scripts.launcher.reparse -ne $false -or
    [string]$boundary.control_scripts.controller.sha256 -cne
      $expectedControllerScriptSha256 -or
    -not [IO.Path]::GetFullPath(
      [string]$boundary.controller_process.working_directory
    ).Equals(
      $root,
      [StringComparison]::OrdinalIgnoreCase
    ) -or
    $boundary.controller_job.name -cne $controllerJobName -or
    -not [IO.Path]::GetFullPath(
      [string]$boundary.controller_job.launcher_identity.path
    ).Equals(
      $launcherScript,
      [StringComparison]::OrdinalIgnoreCase
    ) -or
    [int64]$boundary.controller_job.launcher_identity.bytes -ne
      $expectedLauncherScriptBytes -or
    [string]$boundary.controller_job.launcher_identity.sha256 -cne
      $expectedLauncherScriptSha256 -or
    [bool]$boundary.controller_job.launcher_identity.reparse -ne $false -or
    [string]$boundary.controller_job.launcher_identity.authentication -cne
      "direct-prelaunch-parameters-plus-launcher-self-readback" -or
    [uint32]$boundary.controller_job.limit_flags -ne 8192 -or
    [uint32]$boundary.controller_job.create_process_flags -ne 134742020 -or
    $boundary.controller_job.timeout_action -cne "TerminateJobObject" -or
    [uint32]$boundary.controller_job.timeout_exit_code -ne 125 -or
    [int64]$boundary.controller_job.job_ready.bytes -ne
      [int64]$controllerJobReadyBytes.Length -or
    $boundary.controller_job.job_ready.sha256 -cne
      $controllerJobReadySha256 -or
    (Get-FileSha256 -Path $boundaryPath) -cne $redReady.boundary_sha256 -or
    (Get-FileSha256 -Path $planPath) -cne $redReady.task_plan_sha256) {
  throw "Coordinator plan/boundary/control-script identity differs from RED-ready"
}
$greenReady = [ordered]@{
  schema = "openvino-cpu-observer-task02-phase-green-ready/v2"
  token = [string]$redReady.token
  controller_pid = [int64]$redReady.controller_pid
  controller_start_filetime_utc =
    [int64]$redReady.controller_start_filetime_utc
  controller_creation_filetime_utc =
    [int64]$redReady.controller_creation_filetime_utc
  controller_command_line_sha256 =
    [string]$redReady.controller_command_line_sha256
  controller_script_sha256 = [string]$redReady.controller_script_sha256
  task_plan_sha256 = [string]$redReady.task_plan_sha256
  boundary_sha256 = [string]$redReady.boundary_sha256
  evidence_root = [string]$redReady.evidence_root
  changed_paths = $expectedChanged
  file_sha256 = $fileSha256
  created_utc = [DateTime]::UtcNow.ToString("o")
}
$convertToJson = $ExecutionContext.InvokeCommand.GetCommand(
  "Microsoft.PowerShell.Utility\ConvertTo-Json",
  [Management.Automation.CommandTypes]::Cmdlet
)
if ($null -eq $convertToJson -or
    $convertToJson -isnot [Management.Automation.CmdletInfo] -or
    $convertToJson.ModuleName -cne "Microsoft.PowerShell.Utility" -or
    $convertToJson.ImplementingType.FullName -cne
      "Microsoft.PowerShell.Commands.ConvertToJsonCommand") {
  throw "Coordinator JSON serializer is not the trusted PSHOME cmdlet"
}
$bytes = $utf8Strict.GetBytes(
  ($greenReady | & $convertToJson -Depth 10 -Compress) + "`n"
)
$temp = [IO.Path]::Combine(
  $controlDirectory,
  ".task02-green-ready." + [Guid]::NewGuid().ToString("N") + ".tmp"
)
$stream = [IO.FileStream]::new(
  $temp,
  [IO.FileMode]::CreateNew,
  [IO.FileAccess]::Write,
  [IO.FileShare]::None
)
try {
  $stream.Write($bytes, 0, $bytes.Length)
  $stream.Flush($true)
} finally {
  $stream.Dispose()
}
try {
  [IO.File]::Move($temp, $greenReadyPath)
} catch {
  if ([IO.File]::Exists($temp)) { [IO.File]::Delete($temp) }
  throw
}
if (-not [Collections.StructuralComparisons]::StructuralEqualityComparer.Equals(
    $bytes,
    [IO.File]::ReadAllBytes($greenReadyPath)
  )) {
  throw "Published GREEN-ready bytes differ"
}

$completionDeadline = [DateTime]::UtcNow.AddHours(4)
while (-not $controller.HasExited -and
       [DateTime]::UtcNow -lt $completionDeadline) {
  [Threading.Thread]::Sleep(500)
  $controller.Refresh()
}
$timeoutTerminated = $false
$controller.Refresh()
if (-not $controller.HasExited) {
  $controllerJobHandle =
    [Task02ControllerJobTimeoutNative]::OpenJobObjectW(
      [uint32]0x000C,
      $false,
      $controllerJobName
    )
  if ($controllerJobHandle -eq [IntPtr]::Zero) {
    $openJobError = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
    $controller.Refresh()
    if (-not $controller.HasExited) {
      throw "Timed-out controller Job reopen failed: $openJobError"
    }
  }
  try {
    $controller.Refresh()
    if (-not $controller.HasExited) {
      Assert-Task2TimeoutJobLimits -JobHandle $controllerJobHandle
      Assert-ExactTask2Controller -Process $controller
      $controllerInJob = $false
      if (-not [Task02ControllerJobTimeoutNative]::IsProcessInJob(
          $controller.Handle,
          $controllerJobHandle,
          [ref]$controllerInJob
        ) -or -not $controllerInJob) {
        throw "Timed-out controller is not in the exact named Job"
      }
      if (-not [Task02ControllerJobTimeoutNative]::TerminateJobObject(
          $controllerJobHandle,
          [uint32]125
        )) {
        $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
        throw "GREEN controller TerminateJobObject failed: $errorCode"
      }
      $empty = Wait-Task2TimeoutJobEmpty `
        -JobHandle $controllerJobHandle -Process $controller
      $stableHeavyEmptySamples = Wait-Task2TimeoutStableHeavyEmpty
      $cleanup = [ordered]@{
        schema = "openvino-cpu-observer-task02-timeout-cleanup/v1"
        phase = "GREEN"
        controller_job_name = $controllerJobName
        controller_job_limit_flags = [uint32]$controllerJobLimitFlags
        controller_pid = [int64]$controller.Id
        controller_creation_filetime_utc =
          [int64]$redReady.controller_creation_filetime_utc
        terminate_job_exit_code = 125
        root_signaled = $true
        pid_list_returned_bytes = $empty.pid_list_returned_bytes
        pid_list_capacity = $empty.pid_list_capacity
        assigned_process_count = $empty.assigned_process_count
        listed_process_count = $empty.listed_process_count
        accounting_total_processes = $empty.accounting_total_processes
        accounting_active_processes = $empty.accounting_active_processes
        accounting_returned_bytes = $empty.accounting_returned_bytes
        stable_heavy_empty_samples = [int64]$stableHeavyEmptySamples
        sample_interval_milliseconds = 500
        terminate_process_used = $false
        verified = $true
        created_utc = [DateTime]::UtcNow.ToString("o")
      }
      $cleanupBytes = $utf8Strict.GetBytes(
        ($cleanup | & $convertToJson -Depth 10 -Compress) + "`n"
      )
      $cleanupTemp = [IO.Path]::Combine(
        $controlDirectory,
        ".task02-attempt005-timeout-cleanup." +
          [Guid]::NewGuid().ToString("N") + ".tmp"
      )
      $cleanupStream = [IO.FileStream]::new(
        $cleanupTemp,
        [IO.FileMode]::CreateNew,
        [IO.FileAccess]::Write,
        [IO.FileShare]::None
      )
      try {
        $cleanupStream.Write($cleanupBytes, 0, $cleanupBytes.Length)
        $cleanupStream.Flush($true)
      } finally {
        $cleanupStream.Dispose()
      }
      try {
        [IO.File]::Move($cleanupTemp, $timeoutCleanupPath)
      } catch {
        if ([IO.File]::Exists($cleanupTemp)) {
          [IO.File]::Delete($cleanupTemp)
        }
        throw
      }
      if (-not [Collections.StructuralComparisons]::StructuralEqualityComparer.
          Equals(
            $cleanupBytes,
            [IO.File]::ReadAllBytes($timeoutCleanupPath)
          )) {
        throw "GREEN timeout-cleanup receipt differs after publication"
      }
      $timeoutTerminated = $true
    }
  } finally {
    if ($controllerJobHandle -ne [IntPtr]::Zero) {
      if (-not [Task02ControllerJobTimeoutNative]::CloseHandle(
          $controllerJobHandle
        )) {
        $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
        throw "GREEN timeout controller Job handle close failed: $errorCode"
      }
      $controllerJobHandle = [IntPtr]::Zero
    }
  }
}
if ($controllerJobHandle -ne [IntPtr]::Zero) {
  throw "GREEN retained a coordinator Job handle during normal validation"
}
if ($timeoutTerminated) {
  throw (
    "Task 2 completion timed out; TerminateJobObject atomically stopped the " +
    "controller hierarchy and exact empty cleanup was receipt-bound. " +
    "Preserve attempt 005 and require a reviewed next attempt."
  )
}
$controller.Refresh()
if ($controller.ExitCode -ne 0) {
  throw "Task 2 hidden controller failed after GREEN-ready; preserve attempt 005"
}
$finalReceiptPath =
  [IO.Path]::Combine($controlDirectory, "task02-attempt005-receipt.json")
if (-not [IO.File]::Exists($finalReceiptPath)) {
  throw "Task 2 controller exited zero without its final receipt"
}
```

The fresh GREEN coordinator briefly opens the exact named controller Job,
verifies the live controller handle and dynamically bounded PID list, then
closes that handle before signalling GREEN so it cannot defeat the
controller-owned handle's kill-on-close backstop. It requires both a zero exit
and the final receipt. If the deadline elapses, it reopens and revalidates the
named Job; `TerminateJobObject(125)` then atomically stops the entire
controller Job hierarchy without a pre-termination PID-list capacity gate.
The coordinator requires the exact root handle signalled, a dynamically grown
class-3 list with assigned/listed counts zero, class-1 active count zero, and
60 consecutive 500 ms global-heavy-empty samples within a five-minute
stability deadline, with a ten-second operation timeout on each CIM sample,
before publishing the immutable timeout-cleanup receipt and failing attempt
005. Deadline failure reports the observed blockers, and every coordinator Job
handle is unconditionally closed through `finally` before normal exit-code or
receipt validation. It may be launched as a
yielded/background command and polled at intervals of at most 30 seconds so
the supervising agent can continue status updates; do not depend on an
in-memory process object from the earlier RED launcher, run Step 7 manually,
or start a second controller.

## Step 7: Reconfigure, build the unit target, and prove GREEN twice

The production source and test source are glob-discovered, so reconfiguration
is mandatory after both `.cpp` files exist. Do not reuse the RED-generated
project without this configure.

```powershell
$task2BlockingProcessNames = [string[]]@(
  "MSBuild.exe",
  "link.exe",
  "cl.exe",
  "cmake.exe",
  "ctest.exe",
  "ninja.exe",
  "ov_cpu_unit_tests.exe"
)

function Wait-Task2HeavyBuildHeadroom {
  param([Parameter(Mandatory = $true)][string]$Label)
  $deadline = [DateTime]::UtcNow.AddMinutes(20)
  do {
    $os = Get-CimInstance -ClassName Win32_OperatingSystem
    $freePhysicalBytes = [uint64]$os.FreePhysicalMemory * 1024
    $freeVirtualBytes = [uint64]$os.FreeVirtualMemory * 1024
    $blockingRows = @(
      Get-CimInstance -ClassName Win32_Process -Property @(
        "ProcessId", "CreationDate", "Name", "ExecutablePath", "CommandLine"
      ) | Where-Object {
        $task2BlockingProcessNames -ccontains [string]$_.Name
      }
    )
    if ($blockingRows.Count -ne 0) {
      throw "$Label found a pre-existing build/test process"
    }
    if ($freePhysicalBytes -ge 7784628224 -and
        $freeVirtualBytes -ge 5368709120) {
      return [PSCustomObject]@{
        label = $Label
        observed_utc = [DateTime]::UtcNow.ToString("o")
        free_physical_bytes = $freePhysicalBytes
        free_virtual_bytes = $freeVirtualBytes
        blocking_process_names = $task2BlockingProcessNames
        blocking_process_count = 0
        blocking_processes = @()
        verified = $true
      }
    }
    if ([DateTime]::UtcNow -ge $deadline) {
      throw (
        "$Label headroom remained below 7424 MiB physical or 5120 MiB virtual"
      )
    }
    [Threading.Thread]::Sleep(5000)
  } while ($true)
}

function Assert-LinkCapSupervisorBinding {
  $item = Get-Item -LiteralPath $linkCapSupervisorPath -Force
  if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
      $item.Length -ne 38077 -or
      (Get-FileHash -LiteralPath $linkCapSupervisorPath -Algorithm SHA256).
        Hash.ToLowerInvariant() -cne
        "fd005d235b27a532ac5d0536ccb5b429e9c4f554efcd1f6ed5c9a5cf08e0f90b") {
    throw "Attempt 005 link-cap supervisor drifted after boundary capture"
  }
}

function Assert-NoLinkCapTemporaryResidue {
  $residue = @(
    Get-ChildItem -LiteralPath "C:\ov-build\state-observer" -File -Force |
      Where-Object {
        $_.Name -cmatch
          '^\.task02-attempt005-.+\.(?:stdout|stderr)\.log$'
      }
  )
  if ($residue.Count -ne 0) {
    throw "Attempt 005 link-cap temporary output survived: $($residue.Name)"
  }
}

function Get-Task2CapProof {
  param(
    [Parameter(Mandatory = $true)][object]$Audit,
    [Parameter(Mandatory = $true)]
    [ValidateSet("Unit", "Plugin")]
    [string]$Mode
  )
  $events = [Collections.Generic.List[object]]::new()
  $preflight = $null
  $jobCap = $null
  $summary = $null
  foreach ($line in @($Audit.LogText -split '\r?\n')) {
    if ($line -cmatch
        '^\[task02-link-cap\] mode=(?<mode>Unit|Plugin) pid=(?<pid>[0-9]+) creation_filetime_utc=(?<creation>[0-9]+) minimum_bytes=(?<minimum>[0-9]+) maximum_bytes=(?<maximum>[0-9]+) set_flags=(?<set_flags>[0-9]+) readback_flags=(?<readback_flags>[0-9]+) job_member=true event_sequence=(?<sequence>[0-9]+) verified=true$') {
      if ($Matches.mode -cne $Mode -or
          [int64]$Matches.pid -le 0 -or
          [int64]$Matches.creation -le 0 -or
          [uint64]$Matches.minimum -ne 1048576 -or
          [uint64]$Matches.maximum -ne 4294967296 -or
          [uint32]$Matches.set_flags -ne 4 -or
          [uint32]$Matches.readback_flags -ne 6 -or
          [int64]$Matches.sequence -le 0) {
        throw "$Mode cap event is not exact"
      }
      $events.Add([PSCustomObject][ordered]@{
        pid = [int64]$Matches.pid
        creation_filetime_utc = [int64]$Matches.creation
        minimum_working_set_bytes = [uint64]$Matches.minimum
        maximum_working_set_bytes = [uint64]$Matches.maximum
        set_flags = [uint32]$Matches.set_flags
        readback_flags = [uint32]$Matches.readback_flags
        nested_job_member = $true
        event_sequence = [int64]$Matches.sequence
        verified = $true
      })
      continue
    }
    if ($line -cmatch
        '^\[task02-job-cap\] mode=(?<mode>Unit|Plugin) process_memory_bytes=(?<process>[0-9]+) job_memory_bytes=(?<job>[0-9]+) limit_flags=(?<flags>[0-9]+) set_before_assign=true read_back=true verified=true$') {
      if ($null -ne $jobCap -or
          $Matches.mode -cne $Mode -or
          [uint64]$Matches.process -ne 6442450944 -or
          [uint64]$Matches.job -ne 7516192768 -or
          [uint32]$Matches.flags -ne 8960) {
        throw "$Mode nested Job cap event is not exact"
      }
      $jobCap = [PSCustomObject][ordered]@{
        mode = $Mode
        process_memory_limit_bytes = [uint64]$Matches.process
        job_memory_limit_bytes = [uint64]$Matches.job
        limit_flags = [uint32]$Matches.flags
        set_before_assign = $true
        read_back = $true
        verified = $true
      }
      continue
    }
    if ($line -cmatch
        '^\[task02-heavy-preflight\] mode=(?<mode>Unit|Plugin) blocking_count=0 physical_bytes=(?<physical>[0-9]+) virtual_bytes=(?<virtual>[0-9]+) observed_filetime_utc=(?<observed>[0-9]+) job_member=true verified=true$') {
      if ($null -ne $preflight -or
          $Matches.mode -cne $Mode -or
          [uint64]$Matches.physical -lt 7784628224 -or
          [uint64]$Matches.virtual -lt 5368709120 -or
          [int64]$Matches.observed -le 0) {
        throw "$Mode immediate supervisor preflight is not exact"
      }
      $preflight = [PSCustomObject][ordered]@{
        mode = $Mode
        blocking_process_names = $task2BlockingProcessNames
        blocking_process_count = 0
        blocking_processes = @()
        free_physical_bytes = [uint64]$Matches.physical
        free_virtual_bytes = [uint64]$Matches.virtual
        observed_filetime_utc = [int64]$Matches.observed
        supervisor_job_member = $true
        verified = $true
      }
      continue
    }
    if ($line -cmatch
        '^\[task02-link-cap-summary\] mode=(?<mode>Unit|Plugin) link_count=(?<count>[0-9]+) job_start_count=(?<starts>[0-9]+) job_terminal_count=(?<terminals>[0-9]+) accounting_total_processes=(?<accounting>[0-9]+) accounting_active_processes=0 accounting_returned_bytes=(?<accounting_bytes>[0-9]+) assigned_process_count=0 listed_process_count=0 pid_list_returned_bytes=(?<pid_list_bytes>[0-9]+) process_memory_bytes=(?<process>[0-9]+) job_memory_bytes=(?<job>[0-9]+) job_limit_flags=(?<flags>[0-9]+) maximum_bytes=(?<maximum>[0-9]+) preflight_blocking_count=0 preflight_physical_bytes=(?<physical>[0-9]+) preflight_virtual_bytes=(?<virtual>[0-9]+) preflight_filetime_utc=(?<observed>[0-9]+) active_process_zero=true verified=true child_exit=(?<exit>-?[0-9]+)$') {
      if ($null -ne $summary) {
        throw "$Mode cap log contains duplicate summaries"
      }
      $summary = [PSCustomObject][ordered]@{
        mode = [string]$Matches.mode
        link_count = [int64]$Matches.count
        job_start_count = [int64]$Matches.starts
        job_terminal_count = [int64]$Matches.terminals
        accounting_total_processes = [int64]$Matches.accounting
        accounting_active_processes = 0
        accounting_returned_bytes = [uint32]$Matches.accounting_bytes
        assigned_process_count = 0
        listed_process_count = 0
        pid_list_returned_bytes = [uint32]$Matches.pid_list_bytes
        process_memory_limit_bytes = [uint64]$Matches.process
        job_memory_limit_bytes = [uint64]$Matches.job
        job_limit_flags = [uint32]$Matches.flags
        maximum_working_set_bytes = [uint64]$Matches.maximum
        preflight_physical_bytes = [uint64]$Matches.physical
        preflight_virtual_bytes = [uint64]$Matches.virtual
        preflight_filetime_utc = [int64]$Matches.observed
        child_exit = [int64]$Matches.exit
      }
    }
  }
  $eventIdentities = @(
    $events | ForEach-Object {
      "{0}|{1}" -f $_.pid, $_.creation_filetime_utc
    } | Sort-Object -Unique
  )
  $eventSequences = @(
    $events | ForEach-Object { [int64]$_.event_sequence } |
      Sort-Object -Unique
  )
  if ($events.Count -lt 1 -or
      $null -eq $preflight -or
      $null -eq $jobCap -or
      $null -eq $summary -or
      $summary.mode -cne $Mode -or
      $summary.link_count -ne $events.Count -or
      $eventIdentities.Count -ne $events.Count -or
      $eventSequences.Count -ne $events.Count -or
      $summary.job_start_count -lt $summary.link_count -or
      $summary.job_start_count -ne $summary.job_terminal_count -or
      $summary.job_start_count -ne $summary.accounting_total_processes -or
      $summary.accounting_active_processes -ne 0 -or
      $summary.accounting_returned_bytes -ne 48 -or
      $summary.assigned_process_count -ne 0 -or
      $summary.listed_process_count -ne 0 -or
      $summary.pid_list_returned_bytes -lt 8 -or
      $summary.process_memory_limit_bytes -ne
        $jobCap.process_memory_limit_bytes -or
      $summary.job_memory_limit_bytes -ne
        $jobCap.job_memory_limit_bytes -or
      $summary.job_limit_flags -ne $jobCap.limit_flags -or
      $summary.maximum_working_set_bytes -ne 4294967296 -or
      $summary.preflight_physical_bytes -ne
        $preflight.free_physical_bytes -or
      $summary.preflight_virtual_bytes -ne
        $preflight.free_virtual_bytes -or
      $summary.preflight_filetime_utc -ne
        $preflight.observed_filetime_utc -or
      $summary.child_exit -ne 0) {
    throw "$Mode heavy build lacks one exact verified cap summary"
  }
  [ordered]@{
    mode = $Mode
    supervisor_path = $linkCapSupervisorPath
    supervisor_bytes = 38077
    supervisor_sha256 =
      "fd005d235b27a532ac5d0536ccb5b429e9c4f554efcd1f6ed5c9a5cf08e0f90b"
    maximum_working_set_bytes = 4294967296
    set_flags = 4
    readback_flags = 6
    nested_job_commit_cap = $jobCap
    completion_reconciliation = [ordered]@{
      unique_start_count = $summary.job_start_count
      unique_terminal_count = $summary.job_terminal_count
      accounting_total_processes = $summary.accounting_total_processes
      accounting_active_processes = 0
      accounting_returned_bytes = $summary.accounting_returned_bytes
      assigned_process_count = 0
      listed_process_count = 0
      pid_list_returned_bytes = $summary.pid_list_returned_bytes
      active_process_zero = $true
      verified = $true
    }
    application_timing =
      "commit limits before suspended-root assignment/resume; linker working-set cap after exact nested-Job NEW_PROCESS"
    supervisor_preflight = $preflight
    link_count = $events.Count
    events = @($events)
    verified = $true
  }
}

$configureGreen = Invoke-Task2Guarded `
  -Label "t2-cfg-g" `
  -ExpectedExit Zero `
  -Command $configureCommand

$cache = Get-Content -LiteralPath "C:\ov-build\state-observer\CMakeCache.txt"
if ($cache -cnotcontains "ENABLE_LTO:INTERNAL=OFF") {
  throw "Attempt 005 must preserve ENABLE_LTO=OFF"
}
foreach ($project in @($unitProject, $pluginProject)) {
  $projectText = Get-Content -LiteralPath $project -Raw
  if ($projectText -cnotmatch
        '<PreferredToolArchitecture>x64</PreferredToolArchitecture>' -or
      $projectText -cnotmatch
        '(?s)<OutDir Condition="[^"]*Release\|x64[^"]*">O:\\bin\\intel64\\Release\\</OutDir>' -or
      $projectText -cnotmatch
        '(?s)<LinkIncremental Condition="[^"]*Release\|x64[^"]*">false</LinkIncremental>' -or
      $projectText -cnotmatch
        '(?s)<ItemDefinitionGroup Condition="[^"]*Release\|x64[^"]*">.*?<Link>.*?<EnableCOMDATFolding>true</EnableCOMDATFolding>.*?<GenerateDebugInformation>false</GenerateDebugInformation>.*?<OptimizeReferences>true</OptimizeReferences>.*?</Link>') {
    throw "Generated Release project semantics drifted: $project"
  }
}

$script:task2HeavyHeadroom = [ordered]@{}
$script:task2CapProof = [ordered]@{}
Assert-NoLinkCapTemporaryResidue
Assert-LinkCapSupervisorBinding
$script:task2HeavyHeadroom["Unit"] =
  Wait-Task2HeavyBuildHeadroom -Label "t2-unit-g"
$buildUnitGreen = Invoke-Task2Guarded `
  -Label "t2-unit-g" `
  -ExpectedExit Zero `
  -Command $unitGreenCommand
Assert-NoLinkCapTemporaryResidue
$script:task2CapProof["Unit"] =
  Get-Task2CapProof -Audit $buildUnitGreen -Mode "Unit"

$unitExecutable =
  "O:\bin\intel64\Release\ov_cpu_unit_tests.exe"
if (-not [IO.File]::Exists($unitExecutable)) {
  throw "Attempt 005 unit executable is missing from generated Release OutDir"
}
$listCommand = @(
  $unitExecutable,
  "--gtest_filter=StateAllocationsDump.*",
  "--gtest_list_tests"
)
$listGreen = Invoke-Task2Guarded `
  -Label "t2-list-g" `
  -ExpectedExit Zero `
  -Command $listCommand

$expectedTestNames = @(
  "ReportsRetainedCapacityOnlyForReuseOwner",
  "DeduplicatesAliasedInputAndOutputBacking",
  "ReportsBothDoubleBuffersWithoutAssumingEqualCapacity",
  "UnsupportedBlockIsUnknownNotDescriptorGuess",
  "AddsOwnedBeamAndScaleZpExactlyOnce",
  "RejectsCheckedAdditionOverflow",
  "RejectsEmptyStateName",
  "RejectsDuplicateLogicalStateRole",
  "RejectsInconsistentStateClass",
  "RejectsZeroPid",
  "RejectsZeroObserverRequestId",
  "RejectsInvalidCorrelationId",
  "RejectsUnknownTrigger",
  "RejectsEmptyAndUnknownPhase",
  "RejectsNonCpuObserverPluginDevice",
  "RejectsInvalidUtf8AndControlCharacters",
  "RejectsAliasCapacityChange",
  "RejectsExternallyBackedDnnlAllocation",
  "RejectsUnsupportedStateSubclass",
  "BeamAliasesDeduplicateGloballyAcrossKeyAndValue",
  "CaptureCoversDoubleAndSingleBufferOwnership",
  "CaptureDeduplicatesSharedBeamAndScaleOwners",
  "CaptureSkipsNonOwningScaleView",
  "SerializesDeterministicCompleteBoundedJson",
  "AcceptsEveryAllowedPhase"
)
$listedTests = @()
$insideSuite = $false
foreach ($line in @($listGreen.LogText -split '\r?\n')) {
  if (-not $insideSuite) {
    if ($line -ceq "StateAllocationsDump.") { $insideSuite = $true }
    continue
  }
  if ($line -cmatch '^  (?<name>[A-Za-z0-9_]+)$') {
    $listedTests += [string]$Matches.name
    continue
  }
  if (-not [string]::IsNullOrWhiteSpace($line)) { break }
}
if ($listedTests.Count -ne $expectedTestNames.Count) {
  throw "Expected exactly 25 named StateAllocationsDump tests"
}
for ($index = 0; $index -lt $expectedTestNames.Count; ++$index) {
  if ($listedTests[$index] -cne $expectedTestNames[$index]) {
    throw "StateAllocationsDump enumeration differs at index $index"
  }
}

$dumpCommand = @(
  $unitExecutable,
  "--gtest_filter=StateAllocationsDump.*"
)
foreach ($run in 1..2) {
  $runLabel = if ($run -eq 1) { "t2-run-g1" } else { "t2-run-g2" }
  $dumpGreen = Invoke-Task2Guarded `
    -Label $runLabel `
    -ExpectedExit Zero `
    -Command $dumpCommand
  $runLog = $dumpGreen.LogText
  if ($runLog -notmatch '(?m)^\[\s*PASSED\s*\]\s+25 tests?\.\r?$') {
    throw "Task 2 focused run $run did not report exactly 25 passed"
  }
  if ($runLog -match '(?m)^\[\s*(FAILED|SKIPPED|DISABLED)\s*\]') {
    throw "Task 2 focused run $run contains a non-passing result"
  }
}
```

Expected: guarded configure and build exit zero; enumeration proves exactly 25
selected tests; each independent fresh test process reports exactly 25 passed,
zero failed, skipped, or disabled; every guard record proves the 2,048 MiB
floor and zero survivors.

## Step 8: Build the production plugin and prove CMake integration

Build the actual production target through the same guard:

```powershell
Assert-NoLinkCapTemporaryResidue
Assert-LinkCapSupervisorBinding
$script:task2HeavyHeadroom["Plugin"] =
  Wait-Task2HeavyBuildHeadroom -Label "t2-plugin-g"
$pluginGreen = Invoke-Task2Guarded `
  -Label "t2-plugin-g" `
  -ExpectedExit Zero `
  -Command $pluginGreenCommand
Assert-NoLinkCapTemporaryResidue
$script:task2CapProof["Plugin"] =
  Get-Task2CapProof -Audit $pluginGreen -Mode "Plugin"
if ($script:task2LabelIndex -ne 8) {
  throw "Task 2 did not complete its exact eight-label sequence"
}
```

Then inspect the generated Visual Studio projects directly:

```powershell
$productionProjects = @(
  & rg -l --fixed-strings "state_allocations_dump.cpp" `
    "C:\ov-build\state-observer" -g "*.vcxproj"
)
if ($LASTEXITCODE -ne 0 -or $productionProjects.Count -lt 1) {
  throw "Generated projects did not discover state_allocations_dump.cpp"
}
$testProjects = @(
  & rg -l --fixed-strings "state_allocations_dump_test.cpp" `
    "C:\ov-build\state-observer" -g "*.vcxproj"
)
if ($LASTEXITCODE -ne 0 -or $testProjects.Count -ne 1 -or
    [IO.Path]::GetFileName($testProjects[0]) -ne "ov_cpu_unit_tests.vcxproj") {
  throw "Unit source was not discovered exactly once by ov_cpu_unit_tests"
}
$expectedPluginProjects = @(
  "openvino_intel_cpu_plugin.vcxproj",
  "openvino_intel_cpu_plugin_obj.vcxproj"
) | Sort-Object
$actualPluginProjects = @(
  $productionProjects |
    ForEach-Object { [IO.Path]::GetFileName($_) }
) | Sort-Object -Unique
if (Compare-Object $expectedPluginProjects $actualPluginProjects) {
  throw "Production source was not discovered in the exact shared plugin and object projects"
}

$pluginDll =
  "O:\bin\intel64\Release\openvino_intel_cpu_plugin.dll"
if (-not (Test-Path -LiteralPath $pluginDll)) {
  throw "Observer CPU plugin DLL is missing"
}
$observerObjects = @(
  Get-ChildItem -LiteralPath "C:\ov-build\state-observer" `
    -Recurse -File -Filter "state_allocations_dump.obj"
)
if ($observerObjects.Count -lt 1 -or
    @($observerObjects | Where-Object { $_.Length -le 0 }).Count -ne 0) {
  throw "Task 2 production source did not emit a non-empty object file"
}
$observerObjectSha256 = [ordered]@{}
foreach ($object in $observerObjects) {
  $observerObjectSha256[$object.FullName] = (
    Get-FileHash -LiteralPath $object.FullName -Algorithm SHA256
  ).Hash.ToLowerInvariant()
}
$pluginDllSha256 = (
  Get-FileHash -LiteralPath $pluginDll -Algorithm SHA256
).Hash.ToLowerInvariant()

function Get-ReleaseLinkCommandProof {
  param(
    [Parameter(Mandatory = $true)][string]$Path,
    [Parameter(Mandatory = $true)][string]$ExpectedOutput
  )
  $bytes = [IO.File]::ReadAllBytes($Path)
  if ($bytes.Length -lt 2) {
    throw "Release link command tlog is empty: $Path"
  }
  $text = [Text.Encoding]::Unicode.GetString($bytes)
  $commandRecords = @(
    $text -split '\r?\n' | Where-Object { $_.StartsWith("^") }
  )
  $expectedOutputPattern =
    '(?i)(?:^|\s)/OUT:"?' + [regex]::Escape($ExpectedOutput) + '"?(?:\s|$)'
  if ($commandRecords.Count -ne 1 -or
      $text -cnotmatch $expectedOutputPattern) {
    throw "Release target must have one exact output-bound link record: $Path"
  }
  $requiredOptions = @(
    "/INCREMENTAL:NO",
    "/OPT:REF",
    "/OPT:ICF",
    "/MACHINE:X64",
    "/guard:cf"
  )
  $forbiddenOptions = @(
    "/LTCG",
    "/OPT:NOREF",
    "/OPT:NOICF"
  )
  foreach ($option in $requiredOptions) {
    if ($text.IndexOf($option, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
      throw "Release link command is missing required option '$option': $Path"
    }
  }
  foreach ($option in $forbiddenOptions) {
    if ($text.IndexOf($option, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
      throw "Release link command contains forbidden option '$option': $Path"
    }
  }
  [ordered]@{
    path = $Path
    bytes = $bytes.Length
    sha256 = Get-BytesSha256 -Bytes $bytes
    command_record_count = 1
    expected_output = $ExpectedOutput
    required_options = $requiredOptions
    forbidden_options = $forbiddenOptions
    preferred_tool_architecture = "x64"
    link_incremental = $false
    generate_debug_information = $false
    enable_comdat_folding = $true
    optimize_references = $true
    lto = $false
  }
}

$unitLinkTlog =
  "C:\ov-build\state-observer\src\plugins\intel_cpu\tests\unit\ov_cpu_unit_tests.dir\Release\ov_cpu_unit_tests.tlog\link.command.1.tlog"
$pluginLinkTlog =
  "C:\ov-build\state-observer\src\plugins\intel_cpu\openvino_intel_cpu_plugin.dir\Release\openvino_intel_cpu_plugin.tlog\link.command.1.tlog"
$script:task2ReleaseLinkProof = [ordered]@{
  Unit = Get-ReleaseLinkCommandProof `
    -Path $unitLinkTlog `
    -ExpectedOutput "O:\bin\intel64\Release\ov_cpu_unit_tests.exe"
  Plugin = Get-ReleaseLinkCommandProof `
    -Path $pluginLinkTlog `
    -ExpectedOutput "O:\bin\intel64\Release\openvino_intel_cpu_plugin.dll"
}
```

There is intentionally no CMake file change. The fresh configure plus the two
generated-project membership checks are the complete CMake integration proof.
The non-empty object and its hash prove the Task 2 translation unit compiled.
Task 2 has no production caller yet, so Release `/Gy` plus `/OPT:REF` may
legitimately remove its private functions and literals from the final DLL.
Task 3 owns the first production reference and the positive DLL marker proof.
The ordinary-build absence proof is not faked here: roadmap Task 7 builds a
separate both-options-OFF route and requires a no-match binary scan, `dumpbin`
symbol scan, and lexical preprocessor audit.

## Step 9: Run exact static, diff, marker, immutable-source, and process audits

All commands in this step are direct read-only Git/text/JSON inspections; no
Python, build, test, or compiled probe is launched.

First require only the five Task 2 paths:

```powershell
$expectedChanged = @(
  "src/plugins/intel_cpu/src/cpu_memory.cpp",
  "src/plugins/intel_cpu/src/cpu_memory.h",
  "src/plugins/intel_cpu/src/utils/state_allocations_dump.cpp",
  "src/plugins/intel_cpu/src/utils/state_allocations_dump.hpp",
  "src/plugins/intel_cpu/tests/unit/state_allocations_dump_test.cpp"
) | Sort-Object
$actualChanged = @(
  (& git -C $derivedCore diff --name-only) +
  (& git -C $derivedCore ls-files --others --exclude-standard)
) | Where-Object { $_ } | Sort-Object -Unique
if (Compare-Object $expectedChanged $actualChanged) {
  throw "Derived Task 2 diff contains an unexpected or missing path"
}
& git -C $derivedCore diff --check
if ($LASTEXITCODE -ne 0) { throw "Task 2 whitespace audit failed" }
& git -C $derivedCore diff --stat
& git -C $derivedCore diff -- @expectedChanged
```

Require the new implementation and test bodies to be lexically guarded and
free of forbidden markers/address conversion:

```powershell
$headerText = Get-Content -LiteralPath `
  "$derivedCore\src\plugins\intel_cpu\src\utils\state_allocations_dump.hpp" -Raw
$sourceText = Get-Content -LiteralPath `
  "$derivedCore\src\plugins\intel_cpu\src\utils\state_allocations_dump.cpp" -Raw
$memoryHeaderText = Get-Content -LiteralPath `
  "$derivedCore\src\plugins\intel_cpu\src\cpu_memory.h" -Raw
$memorySourceText = Get-Content -LiteralPath `
  "$derivedCore\src\plugins\intel_cpu\src\cpu_memory.cpp" -Raw

if ($headerText -notmatch '(?s)#ifdef CPU_DEBUG_CAPS.*StateAllocationSnapshot.*#endif\s+// CPU_DEBUG_CAPS') {
  throw "Observer header is not wholly guarded by CPU_DEBUG_CAPS"
}
if ($sourceText -notmatch '(?s)#ifdef CPU_DEBUG_CAPS.*build_state_allocation_snapshot.*serialize_state_allocation_snapshot.*#endif\s+// CPU_DEBUG_CAPS') {
  throw "Observer definitions are not wholly guarded by CPU_DEBUG_CAPS"
}
if ($memoryHeaderText -notmatch '(?s)#ifdef CPU_DEBUG_CAPS\s+\[\[nodiscard\]\]\s+std::optional<size_t>\s+getAllocatedSize\(\)\s+const noexcept;\s+#endif') {
  throw "Capacity declaration is not guarded by CPU_DEBUG_CAPS"
}
if ($memorySourceText -notmatch '(?s)#ifdef CPU_DEBUG_CAPS\s+std::optional<size_t>\s+DnnlMemoryBlock::getAllocatedSize\(\)\s+const noexcept.*?#endif') {
  throw "Capacity definition is not guarded by CPU_DEBUG_CAPS"
}
if ($sourceText -match 'get_state\s*\(') {
  throw "Physical capture must not materialize public variable-state tensors"
}
if ($sourceText -match 'Memory::getSize\(\).*reserved|reserved.*Memory::getSize\(') {
  throw "Descriptor size appears to be used as retained capacity"
}

& rg -n "TQDBG|raw_address|uintptr_t|reinterpret_cast.*(long|int|size_t)" `
  "$derivedCore\src\plugins\intel_cpu\src\utils\state_allocations_dump.hpp" `
  "$derivedCore\src\plugins\intel_cpu\src\utils\state_allocations_dump.cpp" `
  "$derivedCore\src\plugins\intel_cpu\tests\unit\state_allocations_dump_test.cpp"
$forbiddenExit = $LASTEXITCODE
if ($forbiddenExit -eq 0) { throw "Forbidden marker or address conversion found" }
if ($forbiddenExit -ne 1) { throw "Forbidden marker scan failed" }

& rg -n "OPENVINO_API|OPENVINO_RUNTIME_API|OPENVINO_C_API" `
  "$derivedCore\src\plugins\intel_cpu\src\utils\state_allocations_dump.hpp" `
  "$derivedCore\src\plugins\intel_cpu\src\utils\state_allocations_dump.cpp"
$apiExit = $LASTEXITCODE
if ($apiExit -eq 0) { throw "Task 2 introduced an exported/public API marker" }
if ($apiExit -ne 1) { throw "Public API marker scan failed" }
```

Revalidate immutable clean source against the Step 1 boundary:

```powershell
$boundary = Get-Content -LiteralPath `
  "R:\.superpowers\sdd\task02-attempt005-boundary.json" -Raw |
    ConvertFrom-Json
if ($boundary.schema -cne "openvino-cpu-observer-task02-boundary/v4" -or
    $boundary.attempt004_closure.receipt.sha256 -cne
      "57701e2bb243e9cf294662772f9d1e7a42cd892e57b1545e4bde43be58b9b313" -or
    $boundary.attempt004_closure.combined_manifest_sha256 -cne
      "e8b3dc46d45ac8dc43ba3f8b6bdc061fcf0f8512d01f7243c0d5398c87321bb4" -or
    $boundary.link_cap_supervisor.sha256 -cne
      "fd005d235b27a532ac5d0536ccb5b429e9c4f554efcd1f6ed5c9a5cf08e0f90b") {
  throw "Task 2 attempt 005 boundary contract is not exact"
}
Assert-LinkCapSupervisorBinding
if ((& git -C $cleanCore rev-parse HEAD).Trim() -ne
    $boundary.clean_core_commit) {
  throw "Clean core commit changed during Task 2"
}
if ((& git -C $cleanCore rev-parse "HEAD^{tree}").Trim() -ne
    $boundary.clean_core_tree) {
  throw "Clean core tree changed during Task 2"
}
if (& git -C $cleanCore status --porcelain --untracked-files=all) {
  throw "Clean core became dirty during Task 2"
}
foreach ($path in $taskPaths) {
  $expected = [string](
    $boundary.clean_preimage_blobs.PSObject.Properties[$path].Value
  )
  $entry = @(& git -C $cleanCore ls-tree $base -- $path)
  if ($LASTEXITCODE -ne 0) {
    throw "Unable to revalidate clean-source tree entry for $path"
  }
  $actual = if ($entry.Count -eq 0) {
    "ABSENT"
  } else {
    if ($entry.Count -ne 1 -or
        [string]$entry[0] -cnotmatch
          '^100644 blob (?<blob>[0-9a-f]{40})\t(?<path>.+)$' -or
        $Matches.path -cne $path) {
      throw "Clean-source tree entry is not exact for $path"
    }
    $Matches.blob
  }
  if ($actual -ne $expected) {
    throw "Clean-source blob boundary changed for $path"
  }
}
```

Finally re-read and rehash every attempt-005 public artifact from the canonical
physical directory. This is a second complete audit after all eight commands,
not reuse of the objects returned by the first audit:

```powershell
$guardExpectations = [ordered]@{
  "t2-cfg-r" = [PSCustomObject]@{
    Exit = "zero"; Command = $configureCommand
  }
  "t2-red" = [PSCustomObject]@{
    Exit = "nonzero"; Command = $selectedRedCommand
  }
  "t2-cfg-g" = [PSCustomObject]@{
    Exit = "zero"; Command = $configureCommand
  }
  "t2-unit-g" = [PSCustomObject]@{
    Exit = "zero"; Command = $unitGreenCommand
  }
  "t2-list-g" = [PSCustomObject]@{
    Exit = "zero"; Command = $listCommand
  }
  "t2-run-g1" = [PSCustomObject]@{
    Exit = "zero"; Command = $dumpCommand
  }
  "t2-run-g2" = [PSCustomObject]@{
    Exit = "zero"; Command = $dumpCommand
  }
  "t2-plugin-g" = [PSCustomObject]@{
    Exit = "zero"; Command = $pluginGreenCommand
  }
}
[string[]]$expectedArtifactNames = @()
foreach ($label in $task2LabelSequence) {
  $expectedArtifactNames += $label + ".json"
  $expectedArtifactNames += $label + ".log"
  $expectedArtifactNames += $label + ".wrapper.json"
}
[Array]::Sort($expectedArtifactNames, [StringComparer]::Ordinal)
[string[]]$actualArtifactNames = @()
foreach ($artifactPath in [IO.Directory]::EnumerateFiles(
    $script:task2CanonicalArtifactDirectory,
    "*",
    [IO.SearchOption]::TopDirectoryOnly
  )) {
  $actualArtifactNames += [IO.Path]::GetFileName($artifactPath)
}
[Array]::Sort($actualArtifactNames, [StringComparer]::Ordinal)
if ($actualArtifactNames.Count -ne 24) {
  throw "Attempt 005 must contain exactly eight artifact triples (24 files)"
}
for ($index = 0; $index -lt $expectedArtifactNames.Count; ++$index) {
  if ($actualArtifactNames[$index] -cne $expectedArtifactNames[$index]) {
    throw "Attempt 005 artifact name differs at index $index"
  }
}

$guardArtifactSha256 = [ordered]@{}
$guardArtifactBytes = [ordered]@{}
foreach ($entry in $guardExpectations.GetEnumerator()) {
  $audit = Assert-Task2ArtifactTriple `
    -Label $entry.Key `
    -ExpectedExit $entry.Value.Exit `
    -ExpectedCommand $entry.Value.Command `
    -CanonicalArtifactDirectory $script:task2CanonicalArtifactDirectory
  $first = $script:task2FirstArtifactHashes[$entry.Key]
  if ($null -eq $first -or
      $first.wrapper -cne $audit.WrapperSha256 -or
      $first.record -cne $audit.RecordSha256 -or
      $first.log -cne $audit.LogSha256) {
    throw "Attempt 005 artifact changed after first validation: $($entry.Key)"
  }
  $guardArtifactSha256[$entry.Key] = [ordered]@{
    wrapper = $audit.WrapperSha256
    record = $audit.RecordSha256
    log = $audit.LogSha256
  }
  $guardArtifactBytes[$entry.Key] = [ordered]@{
    wrapper = $audit.WrapperBytes
    record = $audit.RecordBytes
    log = $audit.LogBytes
  }
}
Assert-NoLinkCapTemporaryResidue
if ($script:task2CapProof.Count -ne 2 -or
    $script:task2CapProof["Unit"].verified -ne $true -or
    $script:task2CapProof["Plugin"].verified -ne $true -or
    $script:task2HeavyHeadroom.Count -ne 2 -or
    $script:task2ReleaseLinkProof.Count -ne 2) {
  throw "Attempt 005 heavy-build safety proof is incomplete"
}
foreach ($mode in @("Unit", "Plugin")) {
  if ($script:task2HeavyHeadroom[$mode].blocking_process_count -ne 0 -or
      $script:task2HeavyHeadroom[$mode].verified -ne $true -or
      $script:task2CapProof[$mode].supervisor_preflight.
        blocking_process_count -ne 0 -or
      $script:task2CapProof[$mode].supervisor_preflight.
        supervisor_job_member -ne $true -or
      @($script:task2CapProof[$mode].events | Where-Object {
        $_.creation_filetime_utc -le 0 -or
        $_.nested_job_member -ne $true -or
        $_.verified -ne $true
      }).Count -ne 0) {
    throw "Attempt 005 $mode concurrency, creation, or Job proof is incomplete"
  }
}
if ((Get-ExecutionPolicy -Scope Process) -ne "Bypass" -or
    (Get-ExecutionPolicy -Scope CurrentUser) -ne $currentUserPolicyBefore -or
    (Get-ExecutionPolicy -Scope LocalMachine) -ne $localMachinePolicyBefore) {
  throw "Task 2 changed a persistent execution-policy scope"
}
```

No audit may be waived. A missing sidecar/record/log, non-exact schema or JSON
type, changed first-to-final hash, mutable-alias reopen, non-24-file directory,
ambiguous `rg` exit, unexpected path, changed clean blob, survivor, or
RAM-floor breach keeps Task 2 failed.

## Step 10: Commit the derived-core task boundary

Stage only the five derived-core files and recheck the index:

```powershell
$approvedCommitUserName = "Arian B"
$approvedCommitUserEmail =
  "194431897+arian20020@users.noreply.github.com"
$identityEnvironmentNames = [string[]]@(
  "GIT_AUTHOR_NAME",
  "GIT_AUTHOR_EMAIL",
  "GIT_COMMITTER_NAME",
  "GIT_COMMITTER_EMAIL"
)
$identityEnvironmentBefore = [ordered]@{}
foreach ($name in $identityEnvironmentNames) {
  $identityEnvironmentBefore[$name] =
    [Environment]::GetEnvironmentVariable($name, "Process")
  if (-not [string]::IsNullOrEmpty(
      [string]$identityEnvironmentBefore[$name]
    )) {
    throw "Task 2 commit identity environment override is not empty: $name"
  }
}
$derivedLocalConfigBefore = @(
  & git -C $derivedCore config --local --show-origin --list 2>&1
)
if ($LASTEXITCODE -ne 0) {
  throw "Unable to snapshot derived repository-local configuration"
}
$derivedLocalConfigSnapshotBefore = (
  $derivedLocalConfigBefore | ForEach-Object { [string]$_ }
) -join "`n"
$globalConfigBefore = @(
  & git config --global --show-origin --list 2>&1
)
if ($LASTEXITCODE -ne 0) {
  throw "Unable to snapshot global Git configuration"
}
$globalConfigSnapshotBefore = (
  $globalConfigBefore | ForEach-Object { [string]$_ }
) -join "`n"
$ambientAuthorIdentErrorActionPreference = $ErrorActionPreference
try {
  # Windows PowerShell 5.1 promotes redirected native stderr to a terminating
  # NativeCommandError under Stop. Continue only for this expected probe, then
  # restore the controller's fail-closed preference unconditionally.
  $ErrorActionPreference = "Continue"
  $ambientAuthorIdentOutput = @(
    & git -C $derivedCore var GIT_AUTHOR_IDENT 2>&1
  )
  $ambientAuthorIdentExitCode = $LASTEXITCODE
} finally {
  $ErrorActionPreference = $ambientAuthorIdentErrorActionPreference
}
if ($ambientAuthorIdentExitCode -notin @(0, 128) -or
    ($ambientAuthorIdentExitCode -eq 0 -and
      ($ambientAuthorIdentOutput.Count -ne 1 -or
        [string]::IsNullOrWhiteSpace(
          [string]$ambientAuthorIdentOutput[0]
        ))) -or
    ($ambientAuthorIdentExitCode -eq 128 -and
      $ambientAuthorIdentOutput.Count -eq 0)) {
  throw "Derived ambient GIT_AUTHOR_IDENT preflight was not explainable"
}
$ambientAuthorIdentAvailable = $ambientAuthorIdentExitCode -eq 0

& git -C $derivedCore add `
  src/plugins/intel_cpu/src/cpu_memory.h `
  src/plugins/intel_cpu/src/cpu_memory.cpp `
  src/plugins/intel_cpu/src/utils/state_allocations_dump.hpp `
  src/plugins/intel_cpu/src/utils/state_allocations_dump.cpp `
  src/plugins/intel_cpu/tests/unit/state_allocations_dump_test.cpp
if ($LASTEXITCODE -ne 0) { throw "Unable to stage Task 2 files" }

$staged = @(
  @(& git -C $derivedCore diff --cached --name-only) |
    Where-Object { $_ } | Sort-Object
)
if (Compare-Object $expectedChanged $staged) {
  throw "Staged Task 2 path set is not exact"
}
& git -C $derivedCore diff --cached --check
if ($LASTEXITCODE -ne 0) { throw "Staged whitespace audit failed" }
& git -C $derivedCore diff --cached --stat
& git -C $derivedCore `
  -c "user.name=Arian B" `
  -c "user.email=194431897+arian20020@users.noreply.github.com" `
  commit -m `
  "feat(cpu): capture physical variable-state allocations"
if ($LASTEXITCODE -ne 0) { throw "Task 2 commit failed" }

$derivedLocalConfigAfter = @(
  & git -C $derivedCore config --local --show-origin --list 2>&1
)
if ($LASTEXITCODE -ne 0) {
  throw "Unable to re-read derived repository-local configuration"
}
$derivedLocalConfigSnapshotAfter = (
  $derivedLocalConfigAfter | ForEach-Object { [string]$_ }
) -join "`n"
$globalConfigAfter = @(
  & git config --global --show-origin --list 2>&1
)
if ($LASTEXITCODE -ne 0) {
  throw "Unable to re-read global Git configuration"
}
$globalConfigSnapshotAfter = (
  $globalConfigAfter | ForEach-Object { [string]$_ }
) -join "`n"
if ($derivedLocalConfigSnapshotAfter -cne
      $derivedLocalConfigSnapshotBefore -or
    $globalConfigSnapshotAfter -cne $globalConfigSnapshotBefore) {
  throw "Task 2 command-local commit mutated local or global Git configuration"
}
foreach ($name in $identityEnvironmentNames) {
  if ([Environment]::GetEnvironmentVariable($name, "Process") -cne
      $identityEnvironmentBefore[$name]) {
    throw "Task 2 command-local commit mutated identity environment: $name"
  }
}

$task2Head = (& git -C $derivedCore rev-parse HEAD).Trim()
$task2Tree = (& git -C $derivedCore rev-parse "HEAD^{tree}").Trim()
if ($task2Head.Length -ne 40 -or $task2Tree.Length -ne 40) {
  throw "Task 2 commit/tree identity is not full length"
}
$task2CommitIdentityFields = @(
  & git -C $derivedCore show -s `
    --format="%an%n%ae%n%cn%n%ce" $task2Head
)
if ($LASTEXITCODE -ne 0 -or
    $task2CommitIdentityFields.Count -ne 4 -or
    [string]$task2CommitIdentityFields[0] -cne
      $approvedCommitUserName -or
    [string]$task2CommitIdentityFields[1] -cne
      $approvedCommitUserEmail -or
    [string]$task2CommitIdentityFields[2] -cne
      $approvedCommitUserName -or
    [string]$task2CommitIdentityFields[3] -cne
      $approvedCommitUserEmail) {
  throw "Task 2 commit %an/%ae/%cn/%ce identity is not exact"
}
$task2CommitIdentity = [ordered]@{
  format = "%an%n%ae%n%cn%n%ce"
  author_name = [string]$task2CommitIdentityFields[0]
  author_email = [string]$task2CommitIdentityFields[1]
  committer_name = [string]$task2CommitIdentityFields[2]
  committer_email = [string]$task2CommitIdentityFields[3]
  scope = "command-local git -c"
  command_local_user_name = $approvedCommitUserName
  command_local_user_email = $approvedCommitUserEmail
  ambient_author_ident_exit_code = [int64]$ambientAuthorIdentExitCode
  ambient_author_ident_available = [bool]$ambientAuthorIdentAvailable
  local_config_unchanged = $true
  global_config_unchanged = $true
  process_identity_environment_unchanged = $true
}
if (& git -C $derivedCore status --porcelain --untracked-files=all) {
  throw "Derived core is not clean after the Task 2 commit"
}
```

Expected: one focused local commit with the exact message, five paths, and
`%an/%cn = Arian B`,
`%ae/%ce = 194431897+arian20020@users.noreply.github.com`. An absent ambient
`git var GIT_AUTHOR_IDENT` is explicitly acceptable because the commit alone
uses the two exact command-local `git -c` values. Never run a mutating
`git config` command, set identity environment variables, or otherwise mutate
repository/global identity state; the read-only configuration snapshots above
are the only authorized `git config` calls. Do not push the commit, merge it,
rerun the parent patch controller, or edit the materialization identity JSON.
That JSON remains the immutable Task 1 materialization receipt; the Task 2
development commit/tree and exact author/committer identity are recorded
separately below until Task 7 exports and replays the full patch.

## Step 11: Create a scratch patch/replay identity receipt without publishing

This step proves that the current development tree is expressible as a
binary-safe base-to-head patch while preserving the roadmap's Task 7 ownership
of the tracked patch. It writes only ignored `.superpowers/sdd` scratch. Final
receipt v5 additionally binds the exact physical controller CWD, the
controller-hard-pinned launcher path/bytes/SHA-256/ordinary-file identity, and the
command-locally produced `%an/%ae/%cn/%ce` identity, including unchanged
local/global configuration and process identity variables.

```powershell
$previewPatch = "R:\.superpowers\sdd\task02-attempt005-core-preview.patch"
if (Test-Path -LiteralPath $previewPatch) {
  throw "Task 2 attempt 005 preview patch already exists; preserve it and stop"
}
& git -C $derivedCore diff --binary $base $task2Head `
  --output=$previewPatch
if ($LASTEXITCODE -ne 0 -or
    -not (Test-Path -LiteralPath $previewPatch) -or
    (Get-Item -LiteralPath $previewPatch).Length -eq 0) {
  throw "Task 2 binary-safe preview patch is empty"
}

$scratch = "R:\.superpowers\sdd\task02-attempt005-patch-replay"
if (Test-Path -LiteralPath $scratch) {
  throw "Task 2 replay scratch already exists; preserve it and stop"
}
& git clone --local --no-hardlinks --no-checkout $cleanCore $scratch
if ($LASTEXITCODE -ne 0) { throw "Task 2 replay clone failed" }
& git -C $scratch checkout --detach $base
if ($LASTEXITCODE -ne 0) { throw "Task 2 replay base checkout failed" }
& git -C $scratch apply --cached --check $previewPatch
if ($LASTEXITCODE -ne 0) { throw "Task 2 preview patch check failed" }
& git -C $scratch apply --cached $previewPatch
if ($LASTEXITCODE -ne 0) { throw "Task 2 preview patch apply failed" }
$replayedTree = (& git -C $scratch write-tree).Trim()
if ($replayedTree -ne $task2Tree) {
  throw "Task 2 preview replay tree differs from development tree"
}

if (@(Get-Task2ControlTempResidue).Count -ne 0) {
  throw "Task 2 control temporary residue exists before final receipt"
}
$finalControlScripts = [ordered]@{
  launcher = Get-Task2ControlScriptIdentity `
    -Name "launcher" `
    -Path $launcherScriptPath
  controller = Get-Task2ControlScriptIdentity `
    -Name "controller" `
    -Path $controllerScriptPath
  green_signal = Get-Task2ControlScriptIdentity `
    -Name "GREEN signal" `
    -Path $greenSignalScriptPath
  link_cap_supervisor = Get-Task2ControlScriptIdentity `
    -Name "link-cap supervisor" `
    -Path $linkCapSupervisorPath
}
foreach ($name in @(
    "launcher", "controller", "green_signal", "link_cap_supervisor"
  )) {
  $expectedIdentity =
    $boundary.control_scripts.PSObject.Properties[$name].Value
  $actualIdentity = $finalControlScripts[$name]
  if (-not [IO.Path]::GetFullPath(
        [string]$actualIdentity.path
      ).Equals(
        [IO.Path]::GetFullPath([string]$expectedIdentity.path),
        [StringComparison]::OrdinalIgnoreCase
      ) -or
      [int64]$actualIdentity.bytes -ne [int64]$expectedIdentity.bytes -or
      [string]$actualIdentity.sha256 -cne [string]$expectedIdentity.sha256 -or
      [bool]$actualIdentity.reparse -ne $false -or
      [bool]$expectedIdentity.reparse -ne $false) {
    throw "Task 2 final control-script identity drifted: $name"
  }
}
$receipt = [ordered]@{
  schema = "openvino-cpu-observer-task02-receipt/v5"
  parent_plan_commit = $boundary.parent_plan_commit
  task02_plan_blob = $boundary.task02_plan_blob
  task02_plan_sha256 = $boundary.task02_plan_sha256
  roadmap_sha256 = $roadmapHash
  clean_core_commit = $base
  clean_core_tree = (& git -C $cleanCore rev-parse "$base^{tree}").Trim()
  derived_start_commit = $boundary.derived_start_commit
  derived_start_tree = $boundary.derived_start_tree
  task2_commit = $task2Head
  task2_tree = $task2Tree
  task2_commit_subject = (
    & git -C $derivedCore show -s --format=%s $task2Head
  ).Trim()
  task2_commit_identity = $task2CommitIdentity
  changed_paths = @(
    & git -C $derivedCore diff --name-only $base $task2Head
  )
  preview_patch_path = $previewPatch
  preview_patch_bytes = (Get-Item -LiteralPath $previewPatch).Length
  preview_patch_sha256 = (
    Get-FileHash -LiteralPath $previewPatch -Algorithm SHA256
  ).Hash.ToLowerInvariant()
  replayed_tree = $replayedTree
  accepted_guard_commit = $boundary.accepted_guard_commit
  boundary_schema = $boundary.schema
  guard_blobs = $boundary.guard_blobs
  guard_sha256 = $boundary.guard_sha256
  controller_runtime = $boundary.controller_runtime
  controller_process = $boundary.controller_process
  controller_job = $boundary.controller_job
  control_scripts = $finalControlScripts
  legacy_attempts = $boundary.legacy_attempts
  attempt004_closure = $boundary.attempt004_closure
  reusable_build_cache = $boundary.reusable_build_cache
  link_cap_supervisor = $boundary.link_cap_supervisor
  guard_requested_evidence_root = $guardEvidence
  guard_canonical_evidence_root =
    $script:task2CanonicalArtifactDirectory
  guard_label_count = 8
  guard_artifact_count = 24
  guard_labels = @($guardExpectations.Keys)
  guard_commands = [ordered]@{
    "t2-cfg-r" = $configureCommand
    "t2-red" = $selectedRedCommand
    "t2-cfg-g" = $configureCommand
    "t2-unit-g" = $unitGreenCommand
    "t2-list-g" = $listCommand
    "t2-run-g1" = $dumpCommand
    "t2-run-g2" = $dumpCommand
    "t2-plugin-g" = $pluginGreenCommand
  }
  guard_artifact_sha256 = $guardArtifactSha256
  guard_artifact_bytes = $guardArtifactBytes
  guard_memory_sampling = [ordered]@{
    interval_seconds = 0.25
    classification = "sampled Job-PID safety evidence only"
    benchmark_peak_metric = $false
  }
  heavy_build_headroom = $script:task2HeavyHeadroom
  nested_job_commit_cap = [ordered]@{
    classification =
      "pre-resume kernel safety control over per-process and aggregate private commit"
    benchmark_metric = $false
    limit_flags = $boundary.link_cap_supervisor.job_limit_flags
    process_memory_limit_bytes =
      $boundary.link_cap_supervisor.process_memory_limit_bytes
    job_memory_limit_bytes =
      $boundary.link_cap_supervisor.job_memory_limit_bytes
    completion_delivery_reconciled_to_job_accounting = $true
  }
  link_working_set_cap = [ordered]@{
    classification =
      "post-launch exact-handle resident working-set safety control after nested-Job NEW_PROCESS"
    benchmark_metric = $false
    set_flags = [uint32]$boundary.link_cap_supervisor.set_flags
    readback_flags = [uint32]$boundary.link_cap_supervisor.readback_flags
    unit = $script:task2CapProof["Unit"]
    plugin = $script:task2CapProof["Plugin"]
  }
  release_link_commands = $script:task2ReleaseLinkProof
  fallback_routes_used = @()
  control_temp_residue_audit = [ordered]@{
    name_pattern = $controlTempNamePattern
    residue_count = 0
    verified = $true
  }
  phase_receipts = [ordered]@{
    red_ready_path = $redReadyPath
    red_ready_sha256 = (
      Get-BytesSha256 -Bytes ([IO.File]::ReadAllBytes($redReadyPath))
    )
    green_ready_path = $greenReadyPath
    green_ready_sha256 = (
      Get-BytesSha256 -Bytes ([IO.File]::ReadAllBytes($greenReadyPath))
    )
  }
  production_plugin_path = $pluginDll
  production_plugin_sha256 = $pluginDllSha256
  observer_object_sha256 = $observerObjectSha256
}
if ($receipt.schema -cne "openvino-cpu-observer-task02-receipt/v5" -or
    $receipt.task2_commit_subject -cne
      "feat(cpu): capture physical variable-state allocations" -or
    @($receipt.task2_commit_identity.Keys).Count -ne 13 -or
    $receipt.task2_commit_identity.format -cne
      "%an%n%ae%n%cn%n%ce" -or
    $receipt.task2_commit_identity.author_name -cne "Arian B" -or
    $receipt.task2_commit_identity.author_email -cne
      "194431897+arian20020@users.noreply.github.com" -or
    $receipt.task2_commit_identity.committer_name -cne "Arian B" -or
    $receipt.task2_commit_identity.committer_email -cne
      "194431897+arian20020@users.noreply.github.com" -or
    $receipt.task2_commit_identity.scope -cne "command-local git -c" -or
    $receipt.task2_commit_identity.command_local_user_name -cne "Arian B" -or
    $receipt.task2_commit_identity.command_local_user_email -cne
      "194431897+arian20020@users.noreply.github.com" -or
    [int64]$receipt.task2_commit_identity.ambient_author_ident_exit_code -notin
      @(0, 128) -or
    [bool]$receipt.task2_commit_identity.ambient_author_ident_available -ne
      ([int64]$receipt.task2_commit_identity.ambient_author_ident_exit_code -eq
        0) -or
    [bool]$receipt.task2_commit_identity.local_config_unchanged -ne $true -or
    [bool]$receipt.task2_commit_identity.global_config_unchanged -ne $true -or
    [bool]$receipt.task2_commit_identity.process_identity_environment_unchanged -ne
      $true -or
    -not [IO.Path]::GetFullPath(
      [string]$receipt.controller_process.working_directory
    ).Equals(
      $expectedWorkingDirectoryPhysical,
      [StringComparison]::OrdinalIgnoreCase
    ) -or
    $receipt.controller_process.script_sha256 -cne
      $controllerScriptSha256 -or
    $receipt.guard_labels.Count -ne 8 -or
    $receipt.guard_commands.Count -ne 8 -or
    $receipt.guard_artifact_sha256.Count -ne 8 -or
    $receipt.guard_artifact_bytes.Count -ne 8 -or
    $receipt.boundary_schema -cne
      "openvino-cpu-observer-task02-boundary/v4" -or
    $receipt.controller_job.name -cne $controllerJobName -or
    [uint32]$receipt.controller_job.limit_flags -ne 8192 -or
    [uint32]$receipt.controller_job.create_process_flags -ne 134742020 -or
    $receipt.controller_job.timeout_action -cne "TerminateJobObject" -or
    [uint32]$receipt.controller_job.timeout_exit_code -ne 125 -or
    $receipt.controller_job.job_ready.sha256 -cne
      $controllerJobReadySha256 -or
    (Get-FileHash -LiteralPath $controllerJobReadyPath -Algorithm SHA256).
      Hash.ToLowerInvariant() -cne $controllerJobReadySha256 -or
    [IO.File]::Exists($timeoutCleanupPath) -or
    @($receipt.control_scripts.Keys).Count -ne 4 -or
    -not [IO.Path]::GetFullPath(
      [string]$receipt.control_scripts.launcher.path
    ).Equals(
      $launcherScriptPath,
      [StringComparison]::OrdinalIgnoreCase
    ) -or
    [int64]$receipt.control_scripts.launcher.bytes -ne
      $expectedLauncherScriptBytes -or
    [string]$receipt.control_scripts.launcher.sha256 -cne
      $expectedLauncherScriptSha256 -or
    [bool]$receipt.control_scripts.launcher.reparse -ne $false -or
    -not [IO.Path]::GetFullPath(
      [string]$receipt.controller_job.launcher_identity.path
    ).Equals(
      $launcherScriptPath,
      [StringComparison]::OrdinalIgnoreCase
    ) -or
    [int64]$receipt.controller_job.launcher_identity.bytes -ne
      $expectedLauncherScriptBytes -or
    [string]$receipt.controller_job.launcher_identity.sha256 -cne
      $expectedLauncherScriptSha256 -or
    [bool]$receipt.controller_job.launcher_identity.reparse -ne $false -or
    [string]$receipt.controller_job.launcher_identity.authentication -cne
      "direct-prelaunch-parameters-plus-launcher-self-readback" -or
    [string]$receipt.controller_job.launcher_identity.sha256 -cne
      [string]$receipt.control_scripts.launcher.sha256 -or
    [int64]$receipt.controller_job.launcher_identity.bytes -ne
      [int64]$receipt.control_scripts.launcher.bytes -or
    $receipt.attempt004_closure.receipt.sha256 -cne
      "57701e2bb243e9cf294662772f9d1e7a42cd892e57b1545e4bde43be58b9b313" -or
    $receipt.link_working_set_cap.unit.verified -ne $true -or
    $receipt.link_working_set_cap.plugin.verified -ne $true -or
    $receipt.link_working_set_cap.set_flags -ne 4 -or
    $receipt.link_working_set_cap.readback_flags -ne 6 -or
    $receipt.link_working_set_cap.unit.set_flags -ne 4 -or
    $receipt.link_working_set_cap.unit.readback_flags -ne 6 -or
    $receipt.link_working_set_cap.plugin.set_flags -ne 4 -or
    $receipt.link_working_set_cap.plugin.readback_flags -ne 6 -or
    $receipt.nested_job_commit_cap.limit_flags -ne 8960 -or
    $receipt.nested_job_commit_cap.process_memory_limit_bytes -ne
      6442450944 -or
    $receipt.nested_job_commit_cap.job_memory_limit_bytes -ne
      7516192768 -or
    $receipt.nested_job_commit_cap.completion_delivery_reconciled_to_job_accounting -ne $true -or
    $receipt.control_temp_residue_audit.residue_count -ne 0 -or
    $receipt.control_temp_residue_audit.verified -ne $true -or
    @($receipt.fallback_routes_used).Count -ne 0) {
  throw "Task 2 receipt does not bind eight exact artifact triples"
}
$receiptPath = [IO.Path]::Combine(
  $expectedWorkingDirectoryPhysical,
  ".superpowers\sdd\task02-attempt005-receipt.json"
)
$receiptTemp = $receiptPath + "." + [Guid]::NewGuid().ToString("N") + ".tmp"
$receiptBytes = $utf8Strict.GetBytes(
  ($receipt | & $convertToJson -Depth 14) + "`n"
)
$receiptStream = [IO.FileStream]::new(
  $receiptTemp,
  [IO.FileMode]::CreateNew,
  [IO.FileAccess]::Write,
  [IO.FileShare]::None
)
try {
  $receiptStream.Write($receiptBytes, 0, $receiptBytes.Length)
  $receiptStream.Flush($true)
} finally {
  $receiptStream.Dispose()
}
if ([IO.File]::Exists($receiptPath)) {
  [IO.File]::Delete($receiptTemp)
  throw "Fresh attempt 005 final receipt already exists; preserve it and stop"
}
[IO.File]::Move($receiptTemp, $receiptPath)
if (-not [Collections.StructuralComparisons]::StructuralEqualityComparer.Equals(
    $receiptBytes,
    [IO.File]::ReadAllBytes($receiptPath)
  )) {
  throw "Task 2 final receipt differs after atomic publication"
}
if (@(Get-Task2ControlTempResidue).Count -ne 0) {
  throw "Task 2 control temporary residue exists after final publication"
}
```

Review the receipt and require its `changed_paths` to be the exact five-file
set. The scratch replay can remain for review; it is never a source checkout.

At roadmap Task 7, require:

```powershell
& git -C $core merge-base --is-ancestor $task2Head $coreDevelopmentHead
if ($LASTEXITCODE -ne 0) {
  throw "Final core development head does not contain approved Task 2"
}
& git -C $core diff --binary $base $coreDevelopmentHead `
  --output="R:\experiments\patches\openvino-cpu-state-observer\0001-cpu-state-allocation-observer.patch"
```

Task 7 must then commit that one tracked patch in the parent, replay it from the
immutable base with the controller, and prove
`replay write-tree == coreDevelopmentHead^{tree}` plus exact patch tracked blob,
SHA-256, source commit/tree, derived commit/tree, clean status, ordinary
both-options-OFF absence, and observer both-options-ON presence. The Task 2
preview is evidence only and must never be copied into the tracked patch path.

## Step 12: Independent spec and quality review gates

The implementer records the regenerated revision-10 `/v4` boundary JSON, all
four immutable historical attempts, the exact attempt-004 closure receipt and
three independently recomputed manifest hashes, the exact selected-file RED
reason, the RED/GREEN phase-receipt hashes, all 24 attempt-005
wrapper/record/log hashes and byte counts, both heavy-build headroom snapshots,
all verified recursive linker-cap events, both Release link-command tlog
hashes/options, the exact 25-name test list, both 25-pass outputs, production
DLL hash, observer object
paths/hashes, generated-project membership, static/diff/marker/process audits,
commit/tree, preview patch hash, and replayed tree in
`.superpowers/sdd/progress.md`.

Then request two independent reviews in order:

1. **Spec reviewer (independent agent, read-only):**
   - read the complete roadmap and this complete micro-plan;
   - inspect exact commit `$task2Head`, all five blobs, Task 2 receipt, and every
     guard artifact triple and both correlated phase receipts;
   - verify every roadmap Task 2 requirement is present, no requirement was
     silently deferred except the explicitly Task 7-owned ordinary/replay
     gates, and no public tensor materialization or capacity inference exists;
   - verify the clean base remained immutable and the commit/patch replay tree
     identities are exact;
   - return `SPEC PASS` or `SPEC FAIL` with concrete file/line/evidence
     references. Silence, conditional approval, or `looks good` is not PASS.
2. **Quality reviewer (different independent agent, read-only, only after
   `SPEC PASS`):**
   - review overflow/UTF-8/JSON-bound correctness, alias-domain identity,
     canonical-first total accounting, external/unknown fail-closed behavior,
     supported state extraction, `CPU_DEBUG_CAPS` lexical containment,
     test strength, CMake membership, guard completeness, and absence of raw
     addresses/content/public ABI;
   - independently recompute the expected example totals
     (`192`, `192`, `224`, and shared-owner capture `112`);
   - verify exact 25-test selection and two clean runs, production target build,
     non-empty observer object paths/hashes, the explicit Task 3 positive-DLL
     marker deferral, exact 8-label/24-file closure, hash-pinned Python and
     cap-supervisor provenance, both empty build/test concurrency snapshots,
     both 7,424/5,120 MiB pre-link headroom checks, post-launch 4,096 MiB
     verified per-link resident working-set caps with `set_flags=4` and
     `readback_flags=6`, PID/creation/`nested_job_member` provenance, preserved
     Release linker options, 2,048 MiB guard minimum on every command, and zero
     survivors;
   - return `QUALITY PASS` or `QUALITY FAIL` with concrete references.

Task 2 is accepted only when both verdicts are explicit PASS against the same
40-hex `$task2Head`. If either reviewer reports a defect, do not continue to
Task 3 and do not append labels to attempt 005. Preserve the exact reviewed
commit and evidence, then write and independently approve a correction
addendum. That addendum must freeze the resulting five-file source boundary,
select a fresh next-numbered full attempt and exact label set, add a regression
first, implement the minimum fix, rerun the fresh configure/build/list plus the
full focused suite twice and production build, and rerun every audit. If the
original commit has not left this private derived branch, the addendum may
authorize amending it and regenerating the receipt; otherwise it uses a
separately reviewed fix commit and makes Task 7 include both. Both independent
reviews restart against the new HEAD.

## Completion checklist

- [ ] Roadmap SHA-256 is exact; guard commit `37f4ea6...` and all four
      runtime/acceptance blobs and SHA-256 values are exact; independent guard
      Spec/Quality are PASS at 46/46 focused and 136/136 combined with `csc=0`.
- [ ] Clean core commit/tree/status and all five preimage blobs are frozen; the
      only resume-boundary change is the exact 22,516-byte RED fixture.
- [ ] Orchestration and both direct gates run only from the exact physical
      parent recovery worktree; every wrapper path and guarded-command
      `-WorkingDirectory` is the exact physical parent root; `R:` is only its
      separately validated evidence/materialization alias, `O:` maps only to
      the exact no-hardlink derived core, both mappings are boundary-bound, and
      final receipt v5 revalidates the physical controller CWD.
- [ ] Attempts 001 and 002 each remain exactly two hash/size-bound files:
      attempt 001 is only the safe HETERO configure blocker; attempt 002 is
      only a successful old-contract configure followed by wrapper subst
      rejection and is not a complete run.
- [ ] Attempt 003 remains exactly the three hash/size-bound bootstrap-diagnostic
      files and proves that no guard command or source edit began.
- [ ] Attempt 004 remains exactly four files; its exact 13,042-byte sibling
      receipt has SHA-256 `57701e2b...`, root cause `not_captured`, and
      independently revalidated attempt/diagnostic/combined manifest hashes
      `94aea149...`, `9c709913...`, and `e8b3dc46...`.
- [ ] The normal, non-reparse `C:\ov-build\state-observer` cache has every
      exact ON/OFF/generator/source flag and was reconfigured in place, never
      deleted.
- [ ] Exact 25-test file exists; RED exits exactly 1 and its sole error-class
      diagnostic is the absent Task 2 API.
- [ ] Capacity accessor is supported-owner-only and debug-capability-only.
- [ ] Header/source bodies match this plan and contain no public ABI/address.
- [ ] Fresh CMake configure discovers both new `.cpp` files.
- [ ] Unit target builds serially through the pinned amd64 MSBuild and reviewed
      recursive link-cap supervisor after two empty concurrency snapshots and
      the repeated 7,424/5,120 MiB headroom gates; every linker cap records
      setter flags 4, getter flags 6, and `nested_job_member=true`.
- [ ] All 25 exact test names are listed in exact order and pass twice in
      fresh guarded processes.
- [ ] Production CPU plugin target builds the same way; generated projects name
      the source, at least one non-empty observer object is hash-bound, and both
      link tlogs retain `/INCREMENTAL:NO /OPT:REF /OPT:ICF /MACHINE:X64
      /guard:cf` with upstream Release property semantics.
- [ ] Attempt 005 contains exactly the short sequential labels `t2-cfg-r`,
      `t2-red`, `t2-cfg-g`, `t2-unit-g`, `t2-list-g`, `t2-run-g1`,
      `t2-run-g2`, and `t2-plugin-g` and exactly 24 files; every
      wrapper/record/log triple passes exact 15/14/9/35 schema/type checks,
      canonical path and SHA-256 binding, 2,048 MiB minimum, and zero survivors.
- [ ] Every wrapper call names the exact PSF-signed Python executable and DLL
      hashes, and its isolated `-E -s -S -B -X ... -m` provenance is exact.
- [ ] Atomic RED/GREEN phase receipts correlate one hidden no-profile
      controller by PID, start/creation filetime, exact command-line hash,
      controller-script hash, and token across the source-edit pause; no heavy
      child exists while it waits and incomplete attempt 005 is never resumed;
      a coordination timeout revalidates and atomically terminates the exact
      named kill-on-close Job, proves dynamic PID-list/accounting emptiness, and
      obtains 60 consecutive 500 ms global-heavy-empty samples within five
      minutes or reports the observed blockers and fails after closing every
      coordinator Job handle.
- [ ] The launcher contains no downstream identity literals; the direct
      prelaunch gate passes all four final identities as mandatory parameters,
      the launcher self-validates and publishes its authenticated identity in
      Job-ready v2, the controller hard-pins it in boundary v4, and final
      receipt v5 revalidates the same physical path, bytes, SHA-256, and
      non-reparse identity.
- [ ] The post-launch exact 4,096 MiB cap is read back on at least one
      recursively owned PID-plus-creation-bound, nested-Job-member pinned x64
      `link.exe` for each heavy build with setter flags 4 and readback flags 6;
      no supervisor or control temp output remains and no fallback route was
      used.
- [ ] Guard 250 ms RAM samples are classified only as safety evidence and were
      not copied into benchmark/workbook instantaneous-peak metrics.
- [ ] Wrapper shells use only process-scoped `Bypass`; persistent execution
      policy scopes remain unchanged.
- [ ] Diff/path/whitespace/source-marker/API/immutable-source audits pass.
- [ ] Exact five-file derived commit is clean, has the required subject, and
      final receipt v5 validates `%an/%cn = Arian B` and
      `%ae/%ce = 194431897+arian20020@users.noreply.github.com`; only exact
      command-local `git -c` identity was used, absent ambient
      `GIT_AUTHOR_IDENT` was accepted, and local/global configuration plus
      process identity environment remained unchanged.
- [ ] Scratch binary patch applies to the immutable base and writes the exact
      Task 2 tree.
- [ ] Task 7 export/replay expectations are recorded; no tracked patch was
      prematurely written.
- [ ] Independent `SPEC PASS` and `QUALITY PASS` name the same Task 2 commit.
