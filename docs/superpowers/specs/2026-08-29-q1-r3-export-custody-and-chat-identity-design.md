# Q1 R3 Export Custody and Chat Identity Design

## Scope and issue ownership

Q1 owns R3-009 through R3-013. Q1 supports R3-008, R3-014, R3-018,
R3-019, and R3-022 by publishing stable exact-result APIs and reproducible
evidence. Shared onboarding and application composition remain C0-owned, so
Q1 must not claim the live Chat or export journey is closed until C0 consumes
those APIs and T1 proves the integrated behavior.

The implementation base is Q1 R2 commit
`09c7ce1f348df45e89297fe4c134715fda690a94`, tree
`c81d60a3c91b55bf2b60a5139446e4ea5d2cdd34`. R2 receipts are not reused as
R3 evidence.

## Root causes

### R3-009: external export destinations

`OpenVinoPublishedOutputRegistry.ExportPersistentAsync` passes a genuine
user-selected destination to `RequireDirectChild`, which intentionally admits
only children of the private publication root. The guard is correct for the
registry but is the wrong trust boundary for an export destination.

### R3-010: traversal before rejection

Runtime-profile validation calls `Directory.GetFiles` with
`SearchOption.AllDirectories` before confirming that the publication has no
child directories. A child directory or junction can therefore be entered
before the topology is rejected.

### R3-011: incomplete ancestor validation

`StoragePathGuard.RequireNoReparsePoint` starts at the caller-provided root.
If that root sits below a reparse-point ancestor, the ancestor is never
examined.

### R3-012: suppressed cleanup failure

Both GGUF export and OpenVINO package export catch cleanup failures and discard
them. A caller can therefore observe cancellation or validation failure while
an operation-owned temporary output remains.

### R3-013: cancellation-insensitive identity work

OpenVINO registry validation hashes artifacts synchronously without a
`CancellationToken`. Exact-result Chat and export resolution can therefore
perform model-sized reads after the user cancels.

### R3-014: ambient production Chat selection

Q1 R2 exposes exact-result target construction, but the C0-owned onboarding
shell still reads `LastPublishedDirectory`. That compatibility property is
fail-closed and the exact target method has no live production caller.

## Architecture

### Separate private custody from user export custody

Private publication validation continues to require an exact direct child of
the registry root. A new `ExportDestinationGuard` validates external export
paths independently:

1. require a fully qualified local path;
2. normalize the final destination and require it to be absent;
3. start at the volume root and inspect every existing ancestor in order;
4. reject any reparse point, non-directory ancestor, or ambiguous missing
   intermediate component;
5. require the selected parent to exist as an ordinary directory;
6. create one unpredictable temporary sibling under that exact parent; and
7. use a non-overwriting same-volume atomic move for final promotion.

The guard never interprets the external path as a child of the private output
root.

### Validate topology without recursive discovery

Publication validation enumerates only immediate entries. It checks the
attributes of each immediate entry before opening it. Any directory or reparse
point rejects the publication immediately. Only ordinary top-level files are
then matched to the exact provenance allow-list. No validation path uses
`AllDirectories`.

### Cancellation-aware identity pipeline

Registry registration and resolution become asynchronous. All artifact hashes
use an `ArrayPool<byte>` 128 KiB buffer, `FileStream.ReadAsync`, an explicit
1 TiB aggregate limit, and the caller token on every read. The APIs are:

```csharp
Task<bool> RegisterAsync(
    OptimizationExecutionResult result,
    string publicationDirectory,
    CancellationToken cancellationToken);

Task<OpenVinoPublishedOutput?> ResolveAsync(
    OptimizationExecutionResult result,
    CancellationToken cancellationToken);

Task<OpenVinoOptimizationChatTarget?> CreateChatTargetAsync(
    OptimizationExecutionResult result,
    CancellationToken cancellationToken);

Task<OpenVinoExportResult> ExportPersistentAsync(
    OptimizationExecutionResult result,
    string destinationDirectory,
    ulong maximumBytes,
    CancellationToken cancellationToken);
```

Cancellation remains an `OperationCanceledException` unless cleanup also
fails. If cleanup fails, a sanitized `OpenVinoExportCleanupException` reports
that the operation was cancelled or unsuccessful and that the exact temporary
output could not be removed. It contains no path, filename, provider output,
or host detail.

### Observable, bounded cleanup

`TemporaryExportDirectory` owns exactly one generated sibling directory. Its
non-recursive cleanup procedure:

- rejects a reparse-point temporary root;
- enumerates only immediate entries;
- rejects child directories and reparse points instead of traversing them;
- deletes only verified ordinary immediate files;
- deletes the now-empty exact root; and
- throws the sanitized cleanup exception if any step fails.

The production export method invokes this cleanup on every unsuccessful or
cancelled path. Tests exercise the real filesystem implementation, including a
deliberately invalid owned topology; cleanup behavior is not replaced with a
source-string assertion.

### Exact-result C0 seam

`OpenVinoOptimizationExecutor` removes `LastPublishedDirectory`. Its exact
result APIs are the only Q1-supported consumer boundary:

```csharp
Task<OpenVinoOptimizationChatTarget?> CreateChatTargetAsync(
    OptimizationExecutionResult result,
    CancellationToken cancellationToken);

Task<OpenVinoExportResult> ExportPersistentAsync(
    OptimizationExecutionResult result,
    string destinationDirectory,
    ulong maximumBytes,
    CancellationToken cancellationToken);
```

C0 must call `CreateChatTargetAsync(state.Result, token)`, pass the returned
target to
`ModelInspectionPage.ActivateOpenVinoOptimizationTargetAsync(target, token)`,
and await the typed activation result. For save, C0 must pass the exact
`state.Result` and picker-created absent destination to
`ExportPersistentAsync`, await it, and observe cleanup failure. Runtime-only
targets remain non-exportable.

Q1 will prove these methods behaviorally and prove their Q1 production callers:

- `OpenVinoOptimizationExecutor.ExecuteAsync` calls `RegisterAsync` exactly
  once;
- `CreateChatTargetAsync` calls `ResolveAsync` and produces the target consumed
  by the existing production activation method; and
- `ExportPersistentAsync` calls `ResolveAsync`, the external destination guard,
  streamed copy/verification, observable cleanup, and atomic promotion.

The C0-owned shell registration/call sites are an explicit external closure
gate. A Q1 test seam and exact signatures do not alone close R3-008 or R3-014.

## RED-GREEN evidence strategy

Regression tests are committed in a RED-only commit before production edits.
That exact commit and tree are recorded, and each test must fail for the
expected R2 defect rather than a compilation typo. Production changes follow
in separate commits; the same committed tests are rerun against the corrected
implementation subject.

Tests cover:

- R3-009: a verified persistent package exports to a genuine external
  user-selected directory and never overwrites an existing destination;
- R3-010: a child directory/reparse topology is rejected by the top-level
  validator without recursive enumeration;
- R3-011: a destination below an existing reparse-point ancestor is rejected;
- R3-012: a real invalid temporary topology produces observable cleanup
  failure and no success result;
- R3-013: cancellation during sparse-file hashing terminates promptly without
  model-sized allocation or published output; and
- R3-014 support: exact execution A creates a target for A, substituted result
  B is rejected, runtime-only options remain attached, and no ambient property
  exists in the Q1 API.

Affected existing OpenVINO contracts, unit, worker-client, worker-process,
GGUF shared planning, T1 recovery/Chat/export, sparse-file, cancellation,
rollback, reparse, cleanup, and package-stage suites are rerun. Zero discovery,
build-only, blocked, mock-only, and skipped rows are never reported as passes.

## Production reachability register

| Definition | Q1 production caller | Composition point | Behavioral test | Registration count |
|---|---|---|---|---:|
| `OpenVinoPublishedOutputRegistry.RegisterAsync` | `OpenVinoOptimizationExecutor.ExecuteAsync` | executor constructed by onboarding shell | exact execution registration and stale-result rejection | 1 |
| `OpenVinoPublishedOutputRegistry.ResolveAsync` | executor Chat/export methods | executor-owned registry | mutation, cancellation, runtime/persistent identity tests | 1 |
| `ExportDestinationGuard` | registry export | registry export transaction | external path and ancestor-reparse tests | 1 |
| `TemporaryExportDirectory` | registry export | one per export attempt | real cleanup failure/zero-descendant tests | 1 per attempt |
| `OpenVinoOptimizationExecutor.CreateChatTargetAsync` | pending C0 shell consumption | existing active executor field | exact-result target tests | 0 until C0 |
| `OpenVinoOptimizationExecutor.ExportPersistentAsync` | pending C0 shell consumption | existing active executor field | exact-result external export tests | 0 until C0 |

The final two rows are intentionally not described as production-reachable
until C0 integrates them.

## Security, privacy, and failure behavior

- Digest, byte count, source, plan, execution, inspection, and hardware
  identities remain exact.
- No large model or artifact is loaded whole into memory.
- Every existing path ancestor is checked before custody or traversal.
- No recursive traversal occurs before or after custody validation.
- Final destinations are absent and never overwritten.
- Cleanup failures are observable and sanitized.
- Original sources are opened read-only and are never replaced or modified.
- Successful completion leaves zero operation-owned descendants.
- Evidence contains no username, hostname, local path, model filename, raw
  provider output, credential, token, prompt, or model data.
- App Control, signing, manifests, trust roots, certificates, firewall, and
  native integrity are unchanged.

## Environment and native gate

The worktree preflight found approximately 5.7 GB free after checkout. .NET 8
and 10 runtimes and SDK 10.0.400 exist, but the repository-pinned SDK 10.0.301
does not. Managed verification may use the installed SDK explicitly and must
record that deviation. Native converter and official-worker stages exist, but
the R3 handoff directory and native lock/receipt chain are absent. Native or
package acceptance is therefore blocked until the exact ordered R3 receipts,
lock, dependencies, and adequate disk are available.

## Evidence and handoff

R3 report, manifest, and receipt are regenerated only after the exact
implementation subject is committed and freshly tested. Q1 commits exact
SHA-256-verified snapshots of the supplied evidence and receipt schemas under
`docs/audits/2026-08-29/schemas/`, so validation is reproducible solely from
the branch. Validation verifies command arithmetic, Git blobs, bytes,
SHA-256, ancestry, subject commit/tree, final tip/tree, remote ref, clean
state, native join, and stale-receipt rejection. Only the Q1 R3 branch is
pushed; main is neither merged nor pushed.
