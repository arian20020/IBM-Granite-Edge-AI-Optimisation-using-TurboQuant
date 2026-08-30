# S1 R4.1 Security and Packaging Remediation Plan

> **Execution note:** Execute this plan continuously with TDD, systematic debugging, immutable-subject verification, two independent reviews, and verification-before-completion. Do not merge to `main`.

**Goal:** Correct every candidate-controlled R4.1 Critical and Important finding while preserving strict process, path, package, privacy, cancellation, and evidence boundaries.

**Authorized worktree/branch:** `C:\R4-S1-1` / `audit/ucl-s1-security-remediation-r4-1`

**Base:** commit `29f52dc4f70f9b9c6612a880299bb3b28dcc2d10`, tree `0c7ed991bb5ba8f74c00c668f60e5fbc2fd77304`

**Architecture:** Put bounded, typed, path-free cleanup facts and exception precedence in the shared Hardware Inspection Foundation. Keep route-specific support-code mapping at each worker boundary. Use Windows creation attributes for Job containment before quantizer code can execute. Treat raw build output, evaluated AppX membership, and an actual staged/signed package as separate evidence concepts. Bind all final build, closure, launch, test, and review evidence to one immutable implementation subject.

## Requirement matrix

| Finding | Production correction | RED / mutation evidence | Final evidence |
|---|---|---|---|
| CRIT-001 stale evidence | Commit corrected implementation first; remove only owned outputs; rebuild from the immutable subject; reject prior SourceLink identities; launch only rebuilt output | stale-binary identity mutation | closure, index, report, receipt |
| IMP-001 all-stage cleanup | Shared idempotent/concurrency-safe outcome attempts each registered stage once, preserves primary/cancellation facts, and emits bounded typed facts only | every stage independently throws; later stages still run; cancellation/runtime precedence | focused tests repeated three times |
| IMP-002 early-launch cleanup | Ownership transfers only after successful return; construction failures execute full cleanup and surface cleanup integrity | launch and partial-construction fault tests | route test arithmetic and review |
| IMP-003 quantizer Job | Replace `Process.Start` with creation-time Job-list process launch; bounded streams; terminate and prove empty before temp cleanup | success, descendant/breakaway, output bounds, timeout, cancellation, nonzero, mutation, launch/Job/empty failures | quantizer tests and process scans |
| IMP-004 temp/root custody | Validate local canonical root, open without following reparses, compare final path and file identity, verify ACL, retain root/operation custody, and bound cleanup by entries/depth/bytes/time | poisoned roots, namespaces, reparses, swaps, ACL, ADS, deep/wide/large/time cases | Foundation tests repeated three times |
| IMP-005 package privacy | Exclude Optimization DebugFixtures from app items; add deterministic evaluated-membership generator/validator; exclude PDB/recipe/test/private/unclassified artifacts | inclusion and privacy mutations | subject-bound closure and scans |
| IMP-006 OpenVINO authority | Native route omits `DOTNET_ROOT*`; managed routes retain only validated roots where required | hostile parent and managed/native distinction | environment tests and review |
| diagnostics/minors | Path-free bounded diagnostics, `git diff --check`, schema identity mutations and 64-bit arithmetic extremes | well-formed wrong identities and numeric extremes | schema validation and index |

## Task 1: Establish RED tests and deterministic seams

**Files:**
- Add `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Processes/BoundedCleanupOutcomeTests.cs`
- Modify `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests/Processes/TrustedToolEnvironmentPolicyTests.cs`
- Modify worker-client tests for OpenVINO, GGUF runtime, quantization, and Model Inspection
- Modify Security Audit and R4 handoff validator tests for package membership and arithmetic extremes

1. Add deterministic fault-injection seams that cannot be reached by public production callers.
2. Add cleanup-order, exactly-once, idempotency, concurrent-disposal, exception-precedence, and path-redaction tests.
3. Add handle-custody, environment-authority, Job-containment, output-bound, and package-membership tests.
4. Run focused discovery and tests; record nonzero discovery and expected assertion failures rather than compilation failures.
5. Commit the RED test specification.

## Task 2: Implement shared cleanup and trusted temp custody

**Files:**
- Add shared cleanup outcome/model files under `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/Processes/`
- Modify `TrustedToolOperationEnvironment.cs`, `TrustedToolEnvironmentPolicy.cs`, and Windows process primitives
- Modify the Foundation project only as needed for narrow internal friend access

1. Implement bounded typed cleanup stages/facts and primary/cancellation precedence.
2. Make disposal exactly-once and safe under repeated/concurrent calls.
3. Add canonical local-root, no-namespace/no-format-character, handle final-path/identity, reparse, volume, and ACL verification.
4. Retain root and operation handles; delete verified children without following reparses/ADS; enforce entries/depth/bytes/time bounds.
5. Make partial-construction cleanup observable and path-free.
6. Run Foundation focused tests and commit GREEN implementation.

## Task 3: Correct every worker lifecycle

**Files:**
- Modify OpenVINO client/conversation/terminal cleanup
- Modify GGUF runtime client/session/process session/launcher
- Modify Model Inspection client/protected session/process session
- Modify GGUF quantization client and add/use a protected quantizer process session

1. Route every stream/channel/session/closure/Job/environment stage through all-stage cleanup.
2. Preserve an exact caller `OperationCanceledException`; attach bounded cleanup integrity without converting it to generic failure.
3. Preserve protocol/runtime primary failure while surfacing cleanup integrity through route-specific typed models.
4. Transfer environment and closure ownership only after complete successful construction.
5. Launch quantizer directly into a kill-on-close Job at creation time; prove tree empty before temp cleanup.
6. Keep source/output/manifest/hash/length/reparse/publication custody unchanged.
7. Run each focused suite, repeat race/cancellation/containment/cleanup suites three times, and commit.

## Task 4: Correct package membership and evidence tooling

**Files:**
- Modify `IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj`
- Add deterministic closure generator/validator under `tools/` or `scripts/` with tests
- Modify R4 validator tests without changing supplied schemas

1. Remove all Model Optimization DebugFixtures from normal app item types.
2. Evaluate AppX/package membership from MSBuild/package inputs, never by enumerating `bin`.
3. Classify each member by allowlist reason and fail closed on PDB, recipe, DebugFixtures, tests, TRX, dumps, evidence, models, secrets/private paths, duplicate workers, unresolved expressions, or unclassified additions.
4. Represent actual package/layout identity only when a real package/layout exists.
5. Validate schema counts as 64-bit schema-valid values and add arithmetic overflow/extreme mutations.
6. Run package/security tests and commit.

## Task 5: Create the immutable implementation subject and verify cleanly

1. Run formatting, `git diff --check`, focused and aggregate managed tests.
2. Commit all code and tests; record the immutable implementation commit/tree.
3. Remove only exact branch-owned build/test outputs and prove the worktree clean.
4. Attempt the strongest ordinary package gate first and record its first exact missing/native/signing stage if blocked.
5. Build the main app with package generation disabled only after proving required native stages absent; never call it a package.
6. Build/discover/run serially the mandated Security Audit and worker/Foundation suites; require nonzero tests, zero failed, zero skipped.
7. Repeat cleanup/race/containment suites three times without sleeps.
8. Record changed first-party assemblies and exact app outputs with bytes, SHA-256, and SourceLink/repository identity equal to the immutable subject; reject all prior identities.
9. Launch only the exact rebuilt output if activation is available; otherwise record the external blocker and nonclaim.
10. Run privacy, duplicate, capability, unresolved-expression, process, listener, temp, and lock scans.

## Task 6: Generate artifacts and obtain two independent reviews

**Files:**
- `docs/audits/2026-08-30/S1-package-closure-r4-1.json`
- `docs/audits/2026-08-30/evidence/S1-R4-1-verification-index.json`
- `docs/audits/2026-08-30/reviews/S1-R4-1-requirements-review.md`
- `docs/audits/2026-08-30/reviews/S1-R4-1-adversarial-security-review.md`
- `docs/audits/2026-08-30/S1-security-packaging-r4-1.md`
- `docs/audits/2026-08-30/handoffs/R4-S1-R4-1.json`

1. Generate subject-bound closure and verification index using sanitized commands and relative paths.
2. Request an independent requirements/architecture review against the exact subject and evidence.
3. Request a distinct adversarial security/code/test/evidence review covering cleanup skipping, precedence, cancellation, Job escape, reparse/TOCTOU, environment authority, leakage, stale binaries, and false evidence.
4. Fix every Critical/Important issue. If code/tests change, create a new immutable subject and regenerate affected evidence/reviews.
5. Finalize report, reviews, and receipt. Keep receipt `evidenceManifest` null; validate byte-identical supplied schemas and semantic arithmetic.
6. Commit only artifacts after the implementation subject.

## Task 7: Push and prove durable handoff

1. Confirm clean state and exact branch.
2. Push `audit/ucl-s1-security-remediation-r4-1` without force.
3. Use direct `git ls-remote` to prove local/remote equality.
4. Prove ancestry, expected tree, and exact artifact/schema blobs.
5. Report candidate-controlled completion separately from Intel-native, performance, signing, package, and app-host nonclaims.
