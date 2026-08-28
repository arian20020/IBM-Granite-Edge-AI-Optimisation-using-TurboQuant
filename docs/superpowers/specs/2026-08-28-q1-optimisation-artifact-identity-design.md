# Q1 optimisation and artifact identity design

## Authority and scope

This design implements the user-approved Q1 remediation on commit
`a5ef3558334e50587889140dafba194853938765` and tree
`90c34ab009b744d7b00866fb93e8dbc86363f1b2`. Reference documents describe
requirements only. Q1 owns optimisation planning/execution, OpenVINO
activation and staging, produced-artifact identity, runtime-only configuration
identity, exact Chat/export target selection, cancellation/retry/publication,
and Q1 evidence. Shared onboarding/navigation/composition and frontend
presentation remain unchanged.

## Identity model

Every accepted execution has an immutable `ExecutionId` that is distinct from
its plan ID and attempt generation. A successful result is exactly one of:

- `GgufPersistentFile`: a receipt-backed `.gguf` file;
- `OpenVinoPersistentPackage`: a receipt-backed converted package directory;
- `OpenVinoRuntimeConfiguration`: the exact selected runtime configuration,
  with no path, bytes, or export capability.

The result repeats the selected plan, source, hardware, configuration, and
execution identities needed by consumers. A target resolver accepts only the
exact result and returns a typed Chat or export target. It never consults a
last-published field, filename convention, ambient configuration, or mutable
global default. Runtime-only targets can activate Chat but cannot become export
targets.

## Publication and consumption

Persistent outputs are written below an operation-owned temporary directory on
the destination volume. Q1 rejects reparse points and non-regular files,
enforces per-file and aggregate byte limits, hashes and copies through bounded
buffers with cancellation checks, writes a durable receipt, and atomically
promotes the temporary item. Any failure or cancellation removes only the
verified operation-owned temporary item; an unreceipted promoted item is never
observable and is quarantined during recovery.

Consumption reopens the exact receipt-backed output and streams its digest
again. Resolution fails if bytes, size, source identity, plan identity,
configuration identity, hardware identity, output identity, or execution
identity differs. Export repeats those checks while streaming to a temporary
destination, verifies the destination bytes, and atomically promotes it. The
original source is always opened read-only and is re-attested after terminal
cleanup.

## Activation and failure privacy

OpenVINO Chat activation returns a bounded discriminated result rather than a
boolean. Failure categories are stable support codes such as unavailable,
stale target, inspection rejected, worker unavailable, runtime rejected,
cancelled, and unexpected failure. No exception message, path, native output,
username, hostname, or provider payload crosses that boundary. Cancellation is
kept distinct and never converted to an unexpected failure.

## Concurrency and freshness

The coordinator admits one execution identity per confirmed plan generation.
Progress and terminal results must match the plan ID, configuration digest,
execution ID, source digest/length, and hardware run/snapshot. Duplicate or
late results, retired generations, stale retries, and changed evidence are
ignored or converted to `ReplanRequired`; they cannot publish or replace a
previous result.

## Native staging gate

Managed implementation may proceed immediately. Native OpenVINO construction,
worker staging, or package execution requires valid H1 and M1 receipts and the
Q1 native lock in H1 to M1 to Q1 order. If receipts or authorized dependency
closures are absent, Q1 records an honest blocked native disposition and does
not download tools/models or weaken package, App Control, hash, bound, or
manifest checks.

## Verification

Each production behavior is introduced by a focused failing test, observed RED,
implemented minimally, and rerun GREEN. Final verification covers OpenVINO
contracts/unit/client/process, affected GGUF and T1 suites, exact Chat/export
identity, sparse-file streaming, cancellation/timeout/stale/rollback/reparse/
cleanup/package-stage cases, Debug x64 construction when authorized, privacy,
duplication, package contents, `git diff --check`, and evidence-schema checks.
