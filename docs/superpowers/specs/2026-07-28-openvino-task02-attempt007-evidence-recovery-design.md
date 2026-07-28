# OpenVINO Task 02 Attempt 007 Evidence Recovery Design

## Purpose

Accept or reject the already-implemented Task 02 CPU allocation observer using
a new, minimal, reproducible evidence run. Attempt 007 does not change the
observer implementation. It exists because Attempt 006 mixed successful and
failed retries in one namespace and never produced its required terminal
receipt.

## Frozen implementation

- Derived repository:
  `C:\ov-wb04\2026-07-19\openvino-cpu-state-observer`
- Branch: `project/cpu-state-allocation-observer`
- Base commit: `ede283a88e35465f0d680dabbf1f44080f8fc387`
- Task 02 commit: `222ad430d201ac4ebf7add6fe4009551c216c378`
- Task 02 tree: `c7ec0322ec6e8c259c683454868458d8ca0a1ad5`
- Subject: `feat(cpu): capture physical variable-state allocations`
- Changed paths: exactly the five paths recorded in the Task 02 receipt.

Any source, index, commit, tree, branch, or path-set drift aborts Attempt 007.

## Isolation and cleanup

Attempt 006 and older directories are immutable historical diagnostics. They
are excluded from Attempt 007 acceptance and are not renamed, copied, or
deleted. Attempt 007 writes only beneath:

`experiments/raw-results/openvino-turboquant/2026-07-28/guards/task02-attempt-007`

The directory must not exist at start. A failed guarded command closes Attempt
007. Retrying requires `task02-attempt-008`; labels are never reused.

No broad cleanup command is allowed. Before and after every heavyweight
command, the guard must prove that its Job Object has zero active processes and
that no owned process survives.

## Execution architecture

Use the committed wrapper
`scripts/testing/invoke_guarded_command.ps1` and its committed Python owned
process guard. Every configure, build, compiled test, and compiled probe uses:

- minimum available RAM: 2,048 MiB;
- a unique Attempt 007 label;
- suspended creation and Job assignment before resume;
- a 7,200-second maximum runtime for builds and 300 seconds for test/probe
  commands;
- disabled MSBuild node reuse;
- no CPU affinity or rate cap;
- a validated log SHA-256 and zero survivor PIDs.

The build uses the already validated Visual Studio configuration and
`lld-link.exe` where the Microsoft linker previously exceeded the laptop's
safe memory margin. Only Task 02-affected object files and the CPU unit/plugin
outputs may be invalidated. Recursive clean and full-solution rebuilds are
forbidden.

## Acceptance sequence

1. Freeze repository, receipt, guard-blob, toolchain, RAM, and process
   identities.
2. Verify the exact Task 02 five-path diff and clean derived repository.
3. Configure the existing CPU debug-capability build with functional tests
   enabled.
4. Recompile the affected Task 02 production and unit sources.
5. Relink the CPU unit executable and CPU plugin with the validated LLD route.
6. List tests and require exactly 25 `StateAllocationsDump.*` cases.
7. Run that exact selection twice; each run must report 25 passed and zero
   failed.
8. Verify the unit executable, CPU plugin, and OpenVINO runtime hashes and
   ensure the plugin binary is usable by the validated compiled probe.
9. Replay the Task 02 delta on the immutable base in an owned temporary
   checkout and require the replay tree to equal the frozen Task 02 tree.
10. Publish one `openvino-cpu-observer-task02-attempt007-receipt/v1` JSON
    receipt with exact command labels, guard/log hashes, artifact hashes,
    commit/tree identities, test counts, and replay identity.
11. Obtain independent read-only `SPEC PASS` followed by a different
    independent read-only `QUALITY PASS`, both naming the frozen Task 02
    commit and the Attempt 007 receipt hash.

## Failure behavior

The attempt fails closed if RAM drops below the configured floor, a command
times out, any guard validation fails, a process survives, a log hash differs,
the test selection is not exactly 25, either test run is not 25/25, an
artifact is missing or changes unexpectedly between verification steps, the
replay tree differs, or a reviewer returns anything other than an explicit
pass.

No missing result is represented as passed, zero, inferred, or `N/A`. A
failure remains recorded with its exact reason, and the next run receives a
new attempt number.

## Scope boundary

Attempt 007 accepts only Task 02 implementation and evidence. It does not
modify the workbook, begin Task 03 source edits, publish the final project
patch, push a branch, or create a pull request. Those actions resume only after
both Attempt 007 review gates pass.
