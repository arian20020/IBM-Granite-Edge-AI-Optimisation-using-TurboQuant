# S1 R4.1 security and packaging remediation

## Outcome and immutable identities

Candidate-controlled R4.1 work is complete at implementation subject `4e76c9f55a787ea8f51ba81d4c870743d41f9d24`, tree `b09c181930048c7a15b652fe2af17c46a5db5bde`. Both independent reviews report zero Critical and zero Important findings. External native, signed-package, authoritative visual, and performance acceptance remain blocked and are not claimed.

| Identity | Commit | Tree |
|---|---|---|
| Frozen campaign | `4748fe04f19afdf6b27c4c12502b84db325e7294` | `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91` |
| R4.1 base | `29f52dc4f70f9b9c6612a880299bb3b28dcc2d10` | `0c7ed991bb5ba8f74c00c668f60e5fbc2fd77304` |
| R4.1 implementation subject | `4e76c9f55a787ea8f51ba81d4c870743d41f9d24` | `b09c181930048c7a15b652fe2af17c46a5db5bde` |

The implementation subject descends from the exact R4.1 base. Evidence began only after the subject was committed. The durable evidence commit follows the subject and contains documentation only.

## Changed paths and ownership

The base-to-subject change contains 47 files: 3,202 insertions and 405 deletions. Production changes are confined to the app package project and directory build policy; GGUF runtime and quantization clients; Hardware Inspection process/security primitives; Model Inspection worker-client result and process handling; and OpenVINO worker-client launch, conversation, and environment handling. Verification changes comprise the committed package-policy/generator, handoff validator, Security Audit tests/fixtures, focused worker-client and Foundation tests, process privacy fixture, and the R4.1 implementation plan. There are no shell/navigation, product-UX, compatibility-policy, or optimization-result-schema redesigns.

## Requirement and evidence matrix

| Requirement | Implemented boundary | Evidence on exact subject |
|---|---|---|
| All-stage cleanup | `BoundedCleanupCoordinator` attempts every registered owned stage once, is concurrency-safe/idempotent, caps typed facts, and never retains raw exception messages. Job-empty proof precedes operation-environment deletion. | Managed aggregate 410/410; focused cleanup/race matrix 73/73 repeated three times; requirements and adversarial reviews. |
| Failure/cancellation precedence | Route-specific accumulators retain bounded primary kind and distinct cleanup facts. Exact caller cancellation token remains on `OperationCanceledException`; policy exceptions are classified before base types. | Precedence, cancellation-plus-cleanup, runtime/protocol-plus-cleanup, and mutation tests in the passing suites. |
| Early ownership transfer | Creators retain ownership until successful session return; every failed pre-transfer path invokes complete bounded cleanup and maps cleanup-integrity facts. | Focused creation/transfer failure tests and Model/GGUF process integration. |
| Quantizer containment | Verified argument-list launch is created suspended, assigned to a kill-on-close non-breakaway Job, then resumed. Output drains are immediate and bounded. Every terminal path terminates and proves zero Job members before environment cleanup. | Quantizer suite 19/19 and repeated containment tests. Intel-native execution remains blocked. |
| Temp/root custody | Canonical local-volume roots are held by non-delete-sharing handles. Final path and file identity, private ACL, reparse and ADS state are verified. Operation directories retain exact deletion authority; traversal is bounded by entries, depth, bytes, and time. | File/directory ADS, deterministic pre-open swap, reparse/identity/ACL, poisoned environment, bounded cleanup, and concurrent-custody tests. |
| Concurrent custody | Root handles request only read attributes while denying delete sharing; independent operations can hold compatible root custody. | A genuine integration regression was corrected; final Model process integration is 32/32. |
| Native environment authority | Official native OpenVINO route receives only canonical Windows roots, private TEMP/TMP, and closed diagnostics variables. Validated .NET roots remain only on managed routes. | OpenVINO unit suite 14/14 and hostile-environment tests. |
| Public stderr privacy | Worker stderr remains continuously drained and bounded, but no worker-authored text is retained by the public OpenVINO exception; only typed truncation state survives. | Hostile forward-slash path, host, username, provider, and token mutation passes 1/1 through the public failure boundary. |
| Package privacy/closure | A committed exact path/reason policy is the sole evaluated-membership authority. PDB, recipe, DebugFixtures, test/evidence/model/private, unresolved, duplicate, stale, and unclassified members fail closed. | Canonical closure of 200 evaluated members; all stable scans zero; policy path/hash/bytes embedded in closure. |
| Binary/source identity | PE metadata verification accepts only the exact framework informational-version attribute in the known strong-name AssemblyRef and exact implementation subject. | 24 first-party closure records validated; stale `cc7aee17` and `da49c3d1` identities rejected; hostile same-name and token-mutation fixtures pass. |
| Schemas/arithmetic/privacy | Semantic arithmetic uses schema-valid wide integers; diagnostics and durable evidence are bounded and path-private. | Receipt/schema mutation tests, exact byte comparison, privacy scans, and `git diff --check`. |

## RED, GREEN, and mutation record

Behavioral RED cases were established with nonzero discovery before their fixes. Independent cleanup-stage failures showed later stages could be skipped; cancellation and primary failures could hide cleanup integrity; quantizer creation exposed a post-start containment window; environment construction exposed check/open and partial-cleanup weaknesses; package membership admitted recipes/PDBs/DebugFixtures; identity parsing admitted a hostile same-short-name attribute; and schema arithmetic narrowed valid integer ranges.

The adversarial review then established a further genuine RED: hostile forward-slash path, host, username, provider, and token values could survive in the public OpenVINO exception's retained stderr. The correction keeps draining and truncation accounting but exposes an empty retained-text field. Its public-boundary mutation passes 1/1. No regex redaction claim is used.

The earlier integration RED was also useful: concurrent trusted-root custody produced two Model `worker_crashed` failures and exposed stale ownership assumptions in GGUF integration. The corrected root handle requests read attributes while continuing to deny delete sharing. On the final subject, Model process integration is 32/32 and GGUF production ownership transfer is exercised through a real `TrustedToolOperationEnvironment`.

Mutation coverage includes reversed exception-type ordering, hostile attribute namespace/type/AssemblyRef and public-key-token changes, stale subject metadata, path/reparse/ADS/identity swaps, closure hash/length/path/reason changes, duplicate and case collisions, unresolved expressions, malformed/additional JSON, wrong receipt identities, and arithmetic extremes.

## Verification arithmetic

All passing rows have nonzero discovery, `discovered == executed`, `executed == passed + failed + skipped`, zero failures, and zero skips.

| Managed suite | Discovered / executed / passed / failed / skipped |
|---|---|
| Security Audit | 12 / 12 / 12 / 0 / 0 |
| GGUF runtime WorkerClient | 10 / 10 / 10 / 0 / 0 |
| GGUF quantization WorkerClient | 19 / 19 / 19 / 0 / 0 |
| Model Inspection WorkerClient | 121 / 121 / 121 / 0 / 0 |
| OpenVINO WorkerClient | 14 / 14 / 14 / 0 / 0 |
| Hardware Inspection Foundation | 234 / 234 / 234 / 0 / 0 |
| **Aggregate** | **410 / 410 / 410 / 0 / 0** |

All five relevant cleanup/race/cancellation/containment worker-client and Foundation suites were repeated in full: 398/398 passed per repetition, three repetitions without inserted sleeps, for 1,194/1,194 executions.

Process/integration evidence is reported independently from the zero-skip aggregate. One timing-sensitive Model full run observed 31/32; its focused rerun passed 1/1 and the final full rerun passed 32/32:


| Integration row | Result | Disposition |
|---|---|---|
| Model Inspection process | 32/32 passed | Passed |
| GGUF runtime process | 24 passed, 1 skipped of 25 | Controlled runtime/model not configured; blocked row, not a pass |
| OpenVINO process | 52 passed, 15 skipped, 2 failed of 69 | Official worker/converter/GPU/TurboQuant stages absent; blocked, not a pass |
| Cross-feature | 52/52 passed | Passed |

## Release build, closure, and binary identity

The strongest ordinary Release/x64 package gate ran first and stopped at the exact first prerequisite: `GgufQuantizerStageDirectory is required when GGUF quantizer packaging is enabled.` It produced no actual package and is not a pass.

A separate clean Release/x64/win-x64 managed build, with unavailable native stages explicitly disabled and package generation off, succeeded. Its evaluated AppX membership is not described as a staged or signed package. The canonical committed closure blob is `docs/audits/2026-08-30/S1-package-closure-r4-1.json`: 84,447 bytes, SHA-256 `2e38e8da4014c489a3e1b626643e095f82e4a80969a75884f074d3579603b104`, 200 evaluated members, and `actualPackage: null`. The committed policy is `scripts/verification/S1PackageApprovedPaths.txt`: 18,704 bytes, SHA-256 `3dbad9457d8dfe3c5f588b1c16da30972dc982ea8e053f5b43a8de2c72d5bf43`.

The closure records 24 first-party DLL placements with bytes and SHA-256. The verifier confirmed each assembly's repository identity equals the implementation subject and rejected prior subjects. The exact app executable is 152,576 bytes with SHA-256 `73dfb19ef30e4a7f2827d36b92be75750b66d2690788c0beb0f7601fe81b4646`; the principal app DLL is 1,884,160 bytes with SHA-256 `22ed97da71c7e9b892e51208c0445257cc704a5bc6144c81ef8176c9b9e389fb`. Full per-assembly records are in the verification index and closure.

Raw build output, evaluated membership, and an actual staged/signed package are three distinct concepts. Raw output was not used as membership. Evaluated membership passed zero-match scans for forbidden members, unresolved expressions, duplicates, private source paths/content, and stale first-party identities. No actual package was available.

## Launch, privacy, and residual-state disposition

Only the exact rebuilt closure executable was used for final launch. At `2026-08-30T17:14:47Z`, PID 6572 remained running after a bounded five-second wait. It had zero observed descendants and zero remaining owned processes after controlled shutdown. This was a hidden managed launch, not authoritative file-13 visual acceptance; no visual pass is claimed.

Final stable scans found zero private source paths/content, forbidden members, unresolved expressions, duplicate targets, stale identities, operation temp directories, package-member lock failures, partial app-output files, owned processes, or owned listeners. Manifest capabilities remain exactly `internetClient`, `runFullTrust`, and `systemAIModels`.

During managed verification, a malformed local `compact.exe` invocation briefly began NTFS compression outside the isolated worktree and was terminated promptly. No files were deleted or content altered; compression attributes outside the worktree may have changed, and no broad reversal was attempted. Two exact branch-owned ephemeral operation-test directories were separately removed after root and reparse verification; those disposable directories are not recoverable. Neither event is used as evidence of product behavior.

## Independent reviews

- Requirements/architecture review: `docs/audits/2026-08-30/reviews/S1-R4-1-requirements-review.md`; Pass with external verification blocks; Critical 0, Important 0.
- Adversarial security/code/test/evidence review: `docs/audits/2026-08-30/reviews/S1-R4-1-adversarial-security-review.md`; exact hash/bytes and verdict are bound in the verification index after completion.

Both reviews target the exact implementation subject/tree. Any Critical or Important candidate-controlled finding would have required a new subject and affected-evidence regeneration.

## Exact C0 consumption rules

C0 must consume this work from pushed ref `refs/remotes/origin/audit/ucl-s1-security-remediation-r4-1`, verify that its remote tip descends from the immutable subject above, and verify exact report, receipt, closure, verification-index, review, and schema blobs before integration. C0 must preserve the production security primitives and their tests when resolving overlaps:

1. Use `TrustedToolEnvironmentPolicy` and `TrustedToolOperationEnvironment` for closed environment construction, canonical local-root and handle custody, private operation TEMP/TMP, ACL/reparse/ADS/identity verification, bounded cleanup, and managed-only validated .NET roots.
2. Use `BoundedCleanupCoordinator` plus route-specific bounded cleanup-fact projection; preserve all-stage exactly-once cleanup, exact cancellation token, primary classification, Job-empty-before-temp ordering, and the 16-fact cap.
3. Preserve creation-time Job containment and ownership transfer in GGUF quantization/runtime, Model Inspection, and OpenVINO process launch. Do not replace it with post-start Job assignment or ambient process-tree killing.
4. Preserve the exact committed `S1PackageApprovedPaths.txt` path/reason policy and `New-S1PackageClosure.ps1`; do not treat raw `bin` output, recipes, PDBs, DebugFixtures, or stage-disabled output as package membership.
5. Preserve `AssemblyRepositoryIdentityVerifier` exact framework/strong-name metadata checks and rebuild affected first-party outputs at the integrated subject. The R4.1 closure hashes are evidence for this subject only and must not be reused after integration.
6. Preserve manifest capabilities exactly. Do not introduce parent-environment inheritance, private-network/server/upload/cloud-proxy capability, or a model/hardware upload path.
7. Validate other workers' v2 receipts against the byte-identical schema blobs on this ref. S1 has no v2 evidence manifest; its separate verification index is supporting evidence.
8. Re-run closure, privacy, identity, process/temp/lock, managed, focused, and available integration gates at C0's new immutable integration subject. Keep native/package/visual/performance blockers explicit until an authoritative host supplies them.

No apply-checkable cross-owner code proposal remains; the required seams are committed production primitives. C0 retains ownership of project/solution/package composition, conflict resolution, and the final closure ledger.

## External blockers and nonclaims

Blocked prerequisites are the GGUF quantizer stage; official OpenVINO worker/converter/TurboQuant stages; GPU-01 authorization; controlled GGUF runtime/model configuration; actual staged/signed packaging; authoritative file-13 visual smoke; and Intel-native/performance hardware.

Accordingly, this handoff makes no signed-package, Intel-native, performance, or authoritative visual acceptance claim. It does not call the stage-disabled managed output a package, and it does not convert skipped/failed native integration rows into passes.
