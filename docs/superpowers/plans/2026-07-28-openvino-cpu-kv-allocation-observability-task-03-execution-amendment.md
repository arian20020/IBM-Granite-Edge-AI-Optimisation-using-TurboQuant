# OpenVINO CPU KV Allocation Observability Task 03 Execution Amendment

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> `superpowers:executing-plans`. This amendment supersedes only the execution,
> evidence, and build-directory mechanics of the full Task 03 plan. Its exact
> nine-path implementation, 21 writer tests, 25 dump tests, production
> behavior, commit subject, replay requirements, and independent review
> criteria remain controlling.

**Goal:** Make the approved Task 03 implementation plan executable with the
current committed guard while preserving complete, independently auditable
evidence and avoiding stale build state.

**Architecture:** Execute Task 03 as serial, boundary-checked phases. Every
guarded command persists the wrapper-verification JSON beside its guard record
and log. Manual `apply_patch` phases are bracketed by exact repository and
boundary checks. Independent review remains outside the execution phases.

**Tech Stack:** Windows PowerShell 5.1, Python 3.11, CMake, Visual Studio 18
2026 MSBuild, LLVM 22.1.8 LLD, GTest, Git.

## Frozen prerequisites

- Controller branch:
  `testing/openvino-turboquant-recovery`
- Accepted Task 02 commit:
  `222ad430d201ac4ebf7add6fe4009551c216c378`
- Accepted Task 02 tree:
  `c7ec0322ec6e8c259c683454868458d8ca0a1ad5`
- Task 02 base:
  `ede283a88e35465f0d680dabbf1f44080f8fc387`
- Attempt 010 terminal receipt:
  `.superpowers/sdd/task02-attempt010-receipt.json`
- Attempt 010 receipt SHA-256:
  `4c2eed9abd46c45f24f60eafae936f22034daddde790eaad0731e2cf4db9d593`
- Task 02 spec and quality verdicts must both name that commit and receipt
  hash in `.superpowers/sdd/progress.md`.
- Derived source:
  `C:\ov-wb04\2026-07-19\openvino-cpu-state-observer`, exposed by the existing
  `O:` substitution.
- The derived source must be clean at the accepted Task 02 commit before
  Phase 1.

## Current guard identities

Task 02 historical evidence remains bound to the guard blobs in its receipt.
Task 03 intentionally uses the current committed guard set:

- `scripts/testing/official_openvino/owned_process_guard.py`:
  `f9199294e4e0d50947803ce6468c6ba1cf2fc389`
- `scripts/testing/official_openvino/guarded_build.py`:
  `f27242b4e1f2976e7eeef94cd21616979473c968`
- `scripts/testing/invoke_guarded_command.ps1`:
  `caef8c7f73d7e5c35d5573d0e33e661d3ad48139`

The wrapper change is limited to optional persistence of its already-emitted
verification JSON and is covered by 48 passing focused guard tests. Task 03
must prove each worktree guard hash equals its committed `HEAD:<path>` blob
before every guarded phase.

## Attempt 003 recovery

Attempt 001 is permanently closed. Its only guarded command,
`task3a1-configure-red`, completed CMake but failed guard validation because a
short-lived child exited between the Job PID snapshot and its memory query.
The record and log remain immutable. The guard now retries a PID that remains
in the Job and permits only a PID confirmed absent from the Job; an unreadable
live PID still fails closed. All 49 focused guard tests pass.

The active execution namespace is Attempt 003.

## Attempt-scoped paths

- Attempt 002 is closed. Its configure command completed successfully under
  the guard, but the wrapper receipt could not be persisted because the caller
  supplied the 260-character physical evidence path instead of the required
  short `R:\` path. Preserve its JSON/log pair as quarantined evidence; do not
  reuse its label, evidence directory, or build directory.
- Evidence:
  `R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards\task03-attempt-003`
- Main build:
  `C:\ov-build\task03-attempt-003-main`
- Functional build:
  `C:\ov-build\task03-attempt-003-functional`
- Boundary:
  `R:\.superpowers\sdd\task03-attempt003-boundary.json`
- Receipt:
  `R:\.superpowers\sdd\task03-attempt003-receipt.json`
- Delta replay:
  `R:\.superpowers\sdd\replay\task03-attempt003-delta`
- Cumulative replay:
  `R:\.superpowers\sdd\replay\task03-attempt003-cumulative`

Every path above must be absent before Phase 1. A failed `task3a3-*` guarded
command closes Attempt 003; a retry requires Attempt 004 and new paths.

## Phase protocol

Each PowerShell phase:

1. captures `CurrentUser` and `LocalMachine` execution policies;
2. sets only `Process=Bypass`;
3. verifies both persistent policies are unchanged;
4. revalidates Task 02 commit/tree, the nine Task 03 preimages or current
   phase state, current controller guard blobs, controlling-plan blobs, and
   Attempt 010 receipt/verdict hash;
5. runs its allowed commands serially;
6. revalidates policy, source status, guard records, verification JSON, and
   zero survivors before returning.

The single-process requirement in the original plan is replaced by this
phase protocol. It applies to Phases 1–6 below. Independent reviewers in
Phase 7 are necessarily separate processes.

## Guard invocation contract

Every wrapper call includes:

```powershell
-MinimumAvailableRamMiB 2048
-PythonExecutable C:\Users\Student\AppData\Local\Programs\Python\Python311\python.exe
-PythonSha256 5f7b89a612c9b8af1d6456cdfcd1dbe5ca630849e79aebced9bee9a6694952ec
-PythonDllSha256 0817a2a657a24c0d5fbb60df56960f42fc66b3039d522ec952dab83e2d869364
-VerificationOutputPath "$guardEvidence\$Label.verification.json"
```

For every label, acceptance requires exactly three files:

- `$Label.json`
- `$Label.log`
- `$Label.verification.json`

The verification file must parse as
`official-openvino-wrapper-verification/v1`; its `run_id`, command,
canonical paths, expected/actual exit, evidence/log hashes,
`controller_binding`, and `valid` value must exactly match the guard record
and wrapper contract. Its bytes must equal the wrapper's single stdout JSON
line.

## Build rules

- Both build directories are new and empty at their first configure.
- Use `ENABLE_DEBUG_CAPS=ON`, `ENABLE_CPU_DEBUG_CAPS=ON`,
  `ENABLE_TESTS=ON`, `ENABLE_FUNCTIONAL_TESTS=ON`,
  `BUILD_SHARED_LIBS=ON`, and the original disabled backend/frontend options.
- Use MSBuild `/m:1`, `/nr:false`, `/p:BuildInParallel=false`,
  `/p:UseMultiToolTask=false`, and `/p:TrackFileAccess=false`.
- Compile changed sources one `SelectedFiles` value per invocation. Never pass
  semicolon-delimited source paths.
- Relink large targets with:
  `/p:LinkToolExe=lld-link.exe` and
  `/p:LinkToolPath=C:\Program Files\LLVM\bin`.
- Do not run a recursive clean or full solution build.

## Phase 1: Boundary and RED

- [ ] Verify both controlling plan files are tracked at controller `HEAD`,
      clean, and record their Git blobs and SHA-256 values.
- [ ] Verify the Attempt 010 receipt and exact verdict lines.
- [ ] Freeze the original Task 03 preimage blobs and write the boundary using
      `apply_patch`.
- [ ] Create only the exact writer test body from Step 2 of the full plan.
- [ ] Configure the new main build directory using label
      `task3a3-configure-red`.
- [ ] Compile only
      `state_allocations_writer_test.cpp` using label
      `task3a3-build-red`, expecting nonzero for the absent Task 03 contracts.
- [ ] Require the RED log to name at least one missing Task 03 contract and
      contain no memory, path, generator, or linker failure.

## Phase 2: Production implementation

- [ ] Revalidate boundary and exact RED evidence.
- [ ] Apply the exact Step 4–7 source edits from the full plan to the eight
      production paths using `apply_patch`.
- [ ] Require the derived worktree delta to contain exactly the nine Task 03
      paths and the new test.
- [ ] Run the full static containment, forbidden-field, API, and marker audits
      before compiling.

## Phase 3: Focused GREEN

- [ ] Reconfigure the main build with label `task3a3-configure-green`.
- [ ] In one guarded, encoded PowerShell child, compile each changed production
      and unit source serially with one `SelectedFiles` value, then LLD-link
      `ov_cpu_unit_tests`; label it `task3a3-build-unit-green`.
- [ ] List exactly 21 `StateAllocationsWriter.*` and 25
      `StateAllocationsDump.*` tests with label `task3a3-list-green`.
- [ ] Run the exact combined 46-test filter twice using labels
      `task3a3-writer-green-1` and `task3a3-writer-green-2`.
- [ ] Require both runs to report 46 passed and zero failed.

## Phase 4: Plugin and stock gates

- [ ] In one guarded, encoded PowerShell child, compile the changed production
      sources serially and LLD-link `openvino_intel_cpu_plugin`; label it
      `task3a3-plugin-green`.
- [ ] Configure the new functional build with label
      `task3a3-configure-functional`.
- [ ] Build the required stock CPU variable-state functional target serially,
      using LLD for its final link, with label `task3a3-build-functional`.
- [ ] List the exact stock query-state selection using
      `task3a3-list-stock`.
- [ ] Run the stock selection with the observer environment variable absent
      using `task3a3-stock-unset`; require pass and no JSONL.
- [ ] Run the exact invalid `.txt` path gate using
      `task3a3-stock-invalid`; require the planned nonzero result and no output
      file.

## Phase 5: Final audits and commit

- [ ] Replace the original Step 10 pre-final warning with a final audit over
      all 13 labels and all 39 files.
- [ ] Validate exact label order, argv, working directory, timeout, RAM floor,
      samples, peaks, Job setup/query, zero survivors, exit class, log hash,
      verification bytes/schema/run ID/controller binding, and absence of node
      reuse or resource caps.
- [ ] Run every original source, CMake membership, diff, containment,
      preprocessor, disabled-projection, and ordinary-route deferral audit.
- [ ] Require exact nine-path status.
- [ ] Commit only those nine derived-core paths with subject
      `feat(cpu): emit opt-in state allocation snapshots`.

## Phase 6: Replay and receipt

- [ ] Revalidate all Phase 5 identities and the committed Task 03 plan blobs.
- [ ] Create sentinel-owned, non-reparse delta and cumulative replay clones.
- [ ] Require both replay trees to equal the committed Task 03 tree.
- [ ] Write the receipt using `apply_patch`.
- [ ] The receipt includes original/amendment plan blobs and SHA-256 values,
      Attempt 010 receipt hash, Task 02 and Task 03 identities, all 13
      record/log/verification hashes, test counts, artifact hashes, and both
      replay trees.
- [ ] Round-trip every receipt field and require the derived core is clean.

## Phase 7: Independent acceptance

- [ ] Obtain independent `SPEC PASS` naming the exact Task 03 commit and
      receipt SHA-256.
- [ ] Only afterward obtain a different independent `QUALITY PASS` naming the
      same identities.
- [ ] Anything else prevents Task 04 and the workbook run.

## Completion criteria

Task 03 is complete only when all 13 guarded commands have exact triplets,
both focused runs pass 46/46, the stock unset gate passes, the invalid-path
gate fails for the exact planned reason without creating a file, both replay
trees match, the nine-path commit is clean, and independent spec and quality
passes name the same commit and receipt hash.
