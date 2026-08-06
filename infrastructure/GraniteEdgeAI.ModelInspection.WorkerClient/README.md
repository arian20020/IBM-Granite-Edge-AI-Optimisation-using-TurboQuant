# GraniteEdgeAI.ModelInspection.WorkerClient

## Purpose

This `win-x64` infrastructure library owns the protected worker process lifecycle. It converts one validated protocol command into either one trusted terminal message or one controlled infrastructure failure. It does not classify a model and is not connected to WinUI in Gate 2.

## Launch invariants

- Resolve an AMD64 worker beneath a fixed approved root.
- Reject reparse-point/path escapes and avoid `PATH` search.
- Build a minimal allowlisted child environment with .NET diagnostics disabled.
- Create three redirected pipe pairs and allow the child to inherit only stdin, stdout and stderr endpoints.
- Launch with `CreateProcessW` and `STARTUPINFOEX`.
- Apply both `PROC_THREAD_ATTRIBUTE_JOB_LIST` and `PROC_THREAD_ATTRIBUTE_HANDLE_LIST` before the first child instruction executes.
- Configure `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`; never request breakaway.

## Conversation invariants

- Verify protocol version, worker identity, process ID, architecture and runtime profile in the hello message.
- Send one start command and accept one started message, zero or more progress messages and one terminal result.
- Drain stdout and stderr concurrently with strict byte bounds.
- Require terminal/exit consistency.
- Preserve the first failure as primary and cleanup issues as secondary diagnostics.

## Cancellation and cleanup

After a request is active, caller cancellation sends at most one cancel command. A cooperative worker must return `Cancelled` and exit 3. A hang, timeout, ignored cancellation, crash, malformed conversation or surviving descendant triggers Job termination. Successful completion requires the Job to report zero active processes.

## Test boundaries

- `GraniteEdgeAI.ModelInspection.WorkerClient.Tests`: policy, path, environment, Win32 layout, handle ownership and client state tests.
- `GraniteEdgeAI.ModelInspection.WorkerProcess.Tests`: real worker/fixture processes, crash/hang/flood/cancellation/timeout/tree/concurrency scenarios.
- `Gate2ArchitectureFitnessTests`: dependency direction, no listener, atomic Job/handle attributes and fixture isolation.

See [ADR-003](../../docs/architecture/decisions/ADR-003-protected-model-inspection-worker.md) and [Gate 2 evidence](../../docs/testing/evidence/2026-08-05-model-inspection-worker-gate2-verification.md).
