# Q1 R3 Export Custody and Chat Identity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close Q1-owned R3-009 through R3-013 with production-reachable, cancellation-aware export custody and provide the exact result-bound Chat/export seam C0 must consume for R3-008/R3-014.

**Architecture:** Preserve the R2 result-bound registry but split private publication custody from external export custody. Make registration/resolution asynchronous and cancellation-aware, reject topology before traversal, expose cleanup failure through typed results/exceptions, and remove the ambient publication property from the Q1 executor.

**Tech Stack:** C# 13, .NET 8 Windows, MSTest 4/Microsoft.Testing.Platform, WinUI application sources, SHA-256 incremental hashing, Windows filesystem reparse-point semantics.

**Spec:** `docs/superpowers/specs/2026-08-29-q1-r3-export-custody-and-chat-identity-design.md`

## Global Constraints

- Base commit is `09c7ce1f348df45e89297fe4c134715fda690a94`; do not import stale R2 receipts as R3 evidence.
- Use RED-GREEN TDD. Commit executable regression tests before production corrections and record the exact RED commit/tree.
- Preserve exact source, plan, execution, inspection, hardware, digest, and byte-count binding.
- Use 128 KiB pooled streaming buffers and a 1 TiB maximum artifact bound; no model-sized allocation.
- Validate every existing path ancestor and reject reparse points before traversal.
- Never recursively enumerate an untrusted publication or export destination.
- Never overwrite an existing destination; publish with a same-parent temporary and atomic non-overwriting move.
- Cleanup failure must be observable and sanitized; original model bytes remain read-only.
- Do not edit C0-owned onboarding/navigation/composition or frontend presentation.
- Do not weaken App Control, signing, manifests, trust, certificates, firewall, or native integrity.
- SDK 10.0.301 is absent. Managed commands may invoke installed SDK 10.0.400 explicitly and must record the deviation. Native/package work remains gated by ordered R3 receipts and lock state.

---

### Task 1: Commit executable RED regressions for R3-009 through R3-013

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/Optimization/OpenVinoOptimizationTests.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests/PersistentOutputRecoveryIntegrationTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.OpenVino.Optimization.Tests/GraniteEdgeAI.OpenVino.Optimization.Tests.csproj`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.Q1.Identity.Tests/GraniteEdgeAI.Q1.Identity.Tests.csproj`

**Interfaces:**
- Consumes: existing `OpenVinoPublishedOutputRegistry`, `OptimizationExporter`, and `StoragePathGuard` behavior from the R2 base.
- Produces: committed behavioral tests that fail for the named defects and compile against wished-for R3 signatures where the missing API itself is the defect.

- [ ] **Step 1: Read the test-quality rules before editing tests**

Read `superpowers/test-driven-development/writing-good-tests.md` completely and name the exact production change that would make each regression pass.

- [ ] **Step 2: Add the external destination regression**

Add an OpenVINO persistent-publication test that creates the registry under one temporary root and supplies an absent final destination under a distinct user-selected root. Assign the awaited production return to `object` so the same test executes both the R2 `bool` API and the R3 typed API without a compile-only RED:

```csharp
object export = await registry.ExportPersistentAsync(
    result, externalDestination, 1UL << 30, CancellationToken.None);
Assert.IsNotNull(export);
Assert.IsTrue(Directory.Exists(externalDestination));
```

Also create the destination first and assert `DestinationExists` without mutation.

- [ ] **Step 3: Add topology-before-traversal and complete-ancestor regressions**

Use real temporary directories and a Windows directory junction helper. After registering a valid publication, add an immediate junction whose controlled target contains thousands of empty long-name files. Invoke the existing export API and assert rejection with a strict bounded-allocation ceiling; R2 fails because `AllDirectories` materializes the target tree, while R3 rejects the immediate junction before entering it. Separately call the existing `StoragePathGuard.RequireRoot` through the committed Q1 identity project and assert that a path below a junction ancestor is rejected:

```csharp
Assert.ThrowsExactly<InvalidOperationException>(() =>
    StoragePathGuard.RequireRoot(pathBelowJunction, create: false));
```

The junction target must remain unchanged and cleanup must remove only test-owned roots.

- [ ] **Step 4: Add observable cleanup failure regression**

Use `FileSystemWatcher` to observe the real export temporary sibling and create an unexpected child directory inside that exact operation-owned root before cancellation completes. Invoke the existing export API, assert an observable sanitized `InvalidOperationException`, and assert no success result. R2 fails because it suppresses cleanup and returns/leaves the temporary root.

- [ ] **Step 5: Add cancellation-during-hash regression**

Create a sparse 256 MiB ordinary artifact, register it, then hold the artifact with `FileShare.None`. Pass an already-cancelled token to the existing export API and assert `OperationCanceledException`, no destination, and bounded allocation. R2 attempts synchronous identity resolution before consulting the token and returns identity failure; R3 observes cancellation before opening the artifact. Add a second GREEN-stage test that cancels after asynchronous hashing starts to prove per-read cancellation:

```csharp
long before = GC.GetTotalAllocatedBytes(true);
await Assert.ThrowsExactlyAsync<OperationCanceledException>(
    () => registry.ExportPersistentAsync(
        result, destination, 1UL << 30, cancellation.Token));
Assert.IsTrue(GC.GetTotalAllocatedBytes(true) - before < 32L * 1024 * 1024);
```

- [ ] **Step 6: Run the RED tests against the defective production baseline**

Run the two focused projects with SDK 10.0.400. Expected: non-zero execution with failures attributable to private-root rejection, missing async/typed APIs, incomplete ancestor validation, suppressed cleanup, or cancellation-insensitive hashing. Zero discovery is a blocker, not RED evidence.

- [ ] **Step 7: Commit the RED tests without production edits**

```powershell
git add -- tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/Optimization/OpenVinoOptimizationTests.cs `
  tests/IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests/PersistentOutputRecoveryIntegrationTests.cs `
  tests/UnitTests/GraniteEdgeAI.OpenVino.Optimization.Tests/GraniteEdgeAI.OpenVino.Optimization.Tests.csproj `
  tests/IntegrationTests/GraniteEdgeAI.Q1.Identity.Tests/GraniteEdgeAI.Q1.Identity.Tests.csproj
git commit -m "test: reproduce Q1 R3 custody defects"
```

Record the RED commit, tree, exact command, exit code, discovered/executed/passed/failed/skipped counts, and sanitized output digest.

### Task 2: Validate all ancestors and own temporary cleanup explicitly

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Storage/ExportDestinationGuard.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Optimization/TemporaryExportDirectory.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Storage/StagedSourceSnapshot.cs`
- Test: files from Task 1

**Interfaces:**
- Consumes: fully qualified absent final destination and caller token.
- Produces: `ExportDestinationGuard.RequireAbsentDirectory`, `StoragePathGuard.RequireNoReparseAncestors`, and `TemporaryExportDirectory.Cleanup`.

- [ ] **Step 1: Promote complete ancestor validation into the production storage guard**

Implement a root-to-leaf walk over existing components:

```csharp
internal static void RequireNoReparseAncestors(string path)
{
    string full = Path.GetFullPath(path);
    string root = Path.GetPathRoot(full)
        ?? throw new ArgumentException("A rooted local path is required.", nameof(path));
    string cursor = Path.TrimEndingDirectorySeparator(root);
    foreach (string component in Path.GetRelativePath(root, full).Split(
        [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
        StringSplitOptions.RemoveEmptyEntries))
    {
        cursor = Path.Combine(cursor, component);
        if (!File.Exists(cursor) && !Directory.Exists(cursor)) break;
        if ((File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Reparse-point ancestry is not accepted.");
    }
}
```

Call it from `RequireRoot`, `RequireChild`, and `RequireRegularFile` before custody is granted.

- [ ] **Step 2: Implement the external destination guard**

Require a fully qualified local path, an existing ordinary parent, every existing ancestor ordinary, and the final path absent. Return normalized destination and a random absent temporary sibling. Never call the private-root `RequireChild` API.

- [ ] **Step 3: Implement observable non-recursive temporary cleanup**

`TemporaryExportDirectory.Cleanup` validates the exact owned root, enumerates immediate entries only, rejects any child directory/reparse point, deletes verified ordinary files, then deletes the empty root. Translate filesystem failures to sanitized `OpenVinoExportCleanupException`.

- [ ] **Step 4: Run ancestor and cleanup tests GREEN**

Expected: external destination guard, junction ancestor, invalid temporary topology, absent destination, and existing destination tests all pass with non-zero discovery.

- [ ] **Step 5: Commit the path and cleanup correction**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Storage/ExportDestinationGuard.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Storage/StagedSourceSnapshot.cs' `
  'IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Optimization/TemporaryExportDirectory.cs' tests
git commit -m "fix: validate export custody before traversal"
```

### Task 3: Make OpenVINO identity resolution asynchronous and cancellation-aware

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Optimization/OpenVinoExportResult.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Optimization/OpenVinoPublishedOutputRegistry.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Execution/OpenVino/OpenVinoOptimizationJourneyInfrastructure.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/Optimization/OpenVinoOptimizationTests.cs`

**Interfaces:**
- Consumes: exact `OptimizationExecutionResult`, normalized external destination, size bound, and cancellation token.
- Produces: `RegisterAsync`, `ResolveAsync`, `CreateChatTargetAsync`, typed `OpenVinoExportResult`, and sanitized `OpenVinoExportCleanupException`.

- [ ] **Step 1: Add typed export outcomes**

Define closed dispositions `Succeeded`, `ResultRejected`, `RuntimeOnly`, `DestinationRejected`, `DestinationExists`, `IdentityMismatch`, and `CleanupFailed`. The result carries only disposition, execution ID, and plan ID; it carries no path or provider detail.

- [ ] **Step 2: Replace recursive runtime-profile discovery**

Enumerate `Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.TopDirectoryOnly)`. Read attributes before opening an entry, reject directories/reparse points, and require exactly the expected metadata file.

- [ ] **Step 3: Convert registry validation and hashing to async cancellation-aware operations**

Implement `ComputeSha256Async` with `ArrayPool<byte>`, 128 KiB buffer, `ReadAsync`, per-read token checks, explicit expected length, and maximum bound. Thread the token through registration, resolution, runtime validation, persistent validation, and every artifact loop.

- [ ] **Step 4: Rebuild external package export transaction**

Resolve the exact result asynchronously, validate the external destination, create the exact temporary sibling, copy and independently rehash every allowed top-level file, verify aggregate artifact bytes and provenance, atomically move to the absent destination, and invoke observable cleanup on every unsuccessful/cancelled path.

- [ ] **Step 5: Update executor production calls and remove ambient state**

Await `RegisterAsync` from `ExecuteAsync`. Replace `TryCreateChatTarget` with `CreateChatTargetAsync`. Remove `LastPublishedDirectory`; do not add a renamed ambient equivalent. Preserve exact runtime options and retained source lease.

- [ ] **Step 6: Run all focused R3 tests GREEN**

Expected: the same committed RED tests pass, including cancellation during sparse hashing and external atomic export. Existing mutation, stale-result, runtime-only, destination-preservation, rollback, and cleanup tests remain green.

- [ ] **Step 7: Commit the async exact-result correction**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Optimization' `
  'IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Execution/OpenVino/OpenVinoOptimizationJourneyInfrastructure.cs' tests
git commit -m "fix: resolve OpenVINO results with cancellable identity"
```

### Task 4: Verify production reachability and affected suites

**Files:**
- Modify if required for compilation only: Q1-owned project/test project files
- Do not modify: `Features/Onboarding/**`, navigation, XAML, or presentation files

**Interfaces:**
- Consumes: corrected production APIs from Tasks 2-3.
- Produces: non-zero managed verification and an exact C0 integration register.

- [ ] **Step 1: Prove Q1 production call sites and registration counts**

Use semantic compilation plus repository search to list definition, caller, composition point, behavioral test, and registration count for every new production type/method. Confirm `RegisterAsync` has exactly one Q1 production caller and the executor has exactly one production construction site. Record the two C0-pending consumer methods as not production-reachable.

- [ ] **Step 2: Run affected managed suites**

Run OpenVINO contracts, focused optimization, original unit build/test, worker-client, worker-process, Q1 identity, GGUF quantization contracts/client, T1 recovery, sparse-file, cancellation, reparse, rollback, and cleanup coverage. Record every row honestly; blocked, skipped, zero-discovery, and build-only rows are not passes.

- [ ] **Step 3: Run Debug x64 app/package construction only if preflight is authorized**

Recheck disk, pinned SDK, R3 H1/M1 receipts, native lock, package stages, and App Control. If any required condition is absent, do not enter native/package work; record the exact blocker. Do not download SDKs/tools/models or weaken policy.

- [ ] **Step 4: Run privacy, duplication, content, and scope scans**

Run `git diff --check`, changed-file privacy patterns, duplicate production registration search, binary/model/build-output scan, reparse/recursive-enumeration scan, and verify there are no onboarding/navigation/XAML/presentation edits.

- [ ] **Step 5: Commit any Q1-owned compilation corrections**

Commit only if suite execution identifies a Q1-owned correction; add its regression first and repeat RED-GREEN.

### Task 5: Bind evidence to the exact implementation subject and publish R3 handoff

**Files:**
- Create: `docs/audits/2026-08-29/schemas/10-EVIDENCE-MANIFEST-SCHEMA.json`
- Create: `docs/audits/2026-08-29/schemas/10-HANDOFF-RECEIPT-SCHEMA.json`
- Create: `docs/audits/2026-08-29/Q1-optimisation-artifact-remediation-r3.md`
- Create: `docs/audits/2026-08-29/evidence/Q1-optimisation-artifact-evidence-r3-v1.json`
- Publish outside Git: Q1 R3 handoff receipt at the coordinator-provided handoff path only after every receipt gate passes

**Interfaces:**
- Consumes: committed schema snapshots, RED commit/tree, exact implementation commit/tree, fresh command results, production-reachability register, and any ordered native receipts.
- Produces: schema-valid committed report/evidence and atomic Q1 R3 receipt.

- [ ] **Step 1: Commit exact supplied schema snapshots**

Copy the two supplied schemas byte-for-byte with `apply_patch`, verify their reference SHA-256/bytes, parse them, and commit them before generating evidence.

- [ ] **Step 2: Fix the implementation subject**

Commit all implementation/test changes. Record implementation commit/tree separately from later evidence commits. Rerun the required executable tests against that exact commit; do not change production code afterward without creating a new subject and rerunning.

- [ ] **Step 3: Generate and validate report/evidence**

Include owned/support issue IDs, base/RED/subject identities, changed paths, real production callers, RED/GREEN evidence, non-zero totals, package/native disposition, blockers, exact C0 seam, and privacy nonclaims. Validate against the committed schema and verify every command arithmetic equation.

- [ ] **Step 4: Verify committed blobs and final Git identity**

Verify report/evidence working bytes and hashes equal `git show` blob bytes; verify frozen ancestry, subject commit/tree, final tip/tree, clean state, and that no stale R2 receipt is referenced as R3 evidence.

- [ ] **Step 5: Push only the Q1 R3 branch**

```powershell
git push -u origin audit/ucl-q1-optimisation-remediation-r3
```

Verify the remote ref resolves to the exact local tip/tree. Do not merge or push main.

- [ ] **Step 6: Publish receipt atomically only if every gate passes**

Validate the receipt against the committed schema, bind exact report/evidence hashes and bytes, bind the exact native handoff receipt bytes if native ran, verify totals and remote identity, write a same-directory temporary receipt, then atomically rename it to the final coordinator path. If any arithmetic, hash, ancestry, native join, or reachability gate fails, publish no receipt and report the blocker.
