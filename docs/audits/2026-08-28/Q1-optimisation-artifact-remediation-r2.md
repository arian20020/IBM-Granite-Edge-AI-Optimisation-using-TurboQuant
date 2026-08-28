# Q1 optimisation and artifact identity remediation R2

## Disposition

The Q1 managed implementation is complete at evidence subject commit `2a64ddb6753b8db3bfc739375f91b75c09386301`, tree `10db41a5066d5d7dafed1bd097de0c54ff39840a`. It is based on the required integration commit `a5ef3558334e50587889140dafba194853938765` and frozen ancestor `4748fe04f19afdf6b27c4c12502b84db325e7294`.

The native/package disposition is `not-run`: H1 and M1 validate individually, but their ordering does not. M1 completed at 17:41:54Z with `nativeDisposition: not-run`; H1 completed later at 18:01:29Z. Q1 did not enter the native phase or stage/package an OpenVINO worker because the required H1→M1→Q1 chain is invalid. No trust, App Control, dependency, hash, or manifest control was weakened.

## Implemented result

- OpenVINO activation now returns bounded typed dispositions and sanitized failure categories. Raw exception text, paths, process output, and secrets are not retained in the result.
- Every terminal optimization result has an execution identity and carries the exact source, inspection run/handoff, hardware run/snapshot, plan, and configuration identities used for execution.
- The reducer rejects late, duplicate, stale, substituted-plan, changed-model, and changed-hardware terminal results.
- GGUF receipts and recovery bind source byte count, source digest, plan, execution, inspection, and hardware identities. Recovery rejects duplicate receipts, mutation, subdirectories, and reparse points.
- OpenVINO publication distinguishes persistent converted packages from runtime-only configuration profiles. Runtime profiles resolve to typed runtime options, have no model-file export path, and cannot masquerade as persistent artifacts.
- Chat target resolution is result-bound. A runtime-only OpenVINO target retains the exact source custody lease and applies its exact runtime options; a persistent target resolves only after package/provenance verification.
- Export accepts only an exact verified persistent result. It revalidates source/plan/execution/hardware identities plus digest and byte count, streams into a sibling temporary path, independently rehashes, atomically promotes, cleans up, and preserves an existing destination on failure.
- Large artifact copies and hashes use pooled 128 KiB buffers with explicit 1 TiB limits, cancellation, reparse rejection, temporary publication, atomic promotion, rollback, and cleanup. No large model path uses whole-file `ReadAllBytes`.
- Redundant OpenVINO model/package staging copies were removed. The route retains and revalidates the exact custody lease instead of producing an extra snapshot.
- The legacy ambient `LastPublishedDirectory` compatibility seam is fail-closed (`null`). Exact result APIs are available for C0 composition without changing Q1-excluded onboarding or presentation files.

## Verification

RED-GREEN executions were completed on the implementation tree before the implementation commit was created. The implementation commit contains that exact tested tree. A later evidence rerun demonstrated that Windows App Control had begun quarantining freshly rebuilt test assemblies; those reruns discovered zero tests and are recorded as blocked, not passes.

| Verification | Discovered | Executed | Passed | Failed | Skipped | Disposition |
|---|---:|---:|---:|---:|---:|---|
| OpenVINO contracts | 205 | 205 | 205 | 0 | 0 | Passed before policy quarantine |
| Q1 exact identity integration harness | 36 | 36 | 36 | 0 | 0 | Passed |
| OpenVINO optimization managed harness | 50 | 50 | 50 | 0 | 0 | Passed |
| OpenVINO activation harness | 3 | 3 | 3 | 0 | 0 | Passed |
| GGUF quantization contracts | 7 | 7 | 7 | 0 | 0 | Passed |
| GGUF quantization worker client | 8 | 8 | 8 | 0 | 0 | Passed |
| OpenVINO worker client | 13 | 13 | 5 | 8 | 0 | Blocked cases failed to load a policy-denied worker-client assembly (`0x800711C7`) |
| OpenVINO worker process | 0 | 0 | 0 | 0 | 0 | Build previously succeeded; execution blocked by App Control/test-host discovery |
| Original OpenVINO unit project | 0 | 0 | 0 | 0 | 0 | Build succeeded with zero warnings/errors; execution blocked by App Control |

The three focused Q1 projects were rebuilt after the final source edit with zero warnings and zero errors. The final post-commit rerun of contracts, focused Q1, GGUF, and OpenVINO worker-client assemblies returned Testing Platform exit code 5 with zero discovery after policy quarantine. The worker-process rebuild then stopped because the drive had no free space. Only ignored rebuildable `bin`/`obj` outputs inside the isolated Q1 worktree were removed, recovering approximately 963 MB; no source or evidence was removed.

Coverage includes exact plan/result binding, runtime-only and persistent target separation, Chat runtime option application, persistent-only export, digest/byte/source/hardware/execution verification, sparse 64 MiB streaming allocation, cancellation, timeout, stale results, duplicate results, mutation, recovery, rollback, reparse points, cleanup, and atomic destination preservation.

The main WinUI build remains independently blocked by pre-existing `GgufRuntime.Capabilities` namespace configuration and ChatComposer/KnowledgeAttachment XAML errors. Q1 did not edit those shared presentation/composition areas.

## Native and package gate

M1 receipt validation succeeded:

- final commit `0880253b44f9319bf5707185bfe3560eed1bf8b8`
- final tree `fd8b3a12fed94f4f1aa7f881cbd2fc2dbf39a280`
- report SHA-256 `d92743404f927e83010ad0ea777e56a5d9e08a0933dc26a30febcbe3933aa4fa`, 5115 bytes
- evidence SHA-256 `6bc307a09cd55fdbfeeca014391f5364532515e3275884460388a7a70f8d4e8e`, 4212 bytes

H1 receipt validation also succeeded: final commit `e000ee4f7b1ecc68cac79d774d47e659c96b661c`, tree `dc40d2effa2a0363094a069c118fda486b836260`, report SHA-256 `4578e7033ac4cae5ed9d43c966e6320cfc76f65f96002d36b317ae46efabc021` (6850 bytes), and evidence SHA-256 `5e8a8a47a9372b9e80de0d10cf5c7686a3e91ac1771993fae8cb52161bf737a5` (4961 bytes).

The predecessor sequence nevertheless fails closed because H1 completed after M1 and M1 did not run its native phase. There is no valid H1→M1 lock authorization for Q1 native work. Consequently this report makes no Debug x64 package-construction, staged-worker, package-content, native-execution, or Application Control acceptance claim.

## Evidence integrity and privacy

The requested `10-EVIDENCE-MANIFEST-SCHEMA.json` is not present in the authoritative base, reachable repository history, handoff directory, or supplied local reference material. The companion evidence follows the established worker manifest v1 structure and is locally validated for required fields, command arithmetic, hashes, byte counts, commit/tree identity, JSON parsing, and required Q1 input/output kinds. Exact validation against the missing schema is honestly blocked.

Changed-file scans find no onboarding, navigation, XAML, or presentation edits. Q1 evidence contains no local model path, model filename, username, hostname, secret, prompt, model bytes, raw command transcript, or downloaded tool/model. The synthetic activation-sanitization test intentionally supplies a fake private path/token string and verifies it is absent from the typed result.

## C0 handoff

C0 should consume `ResolveChatTargetAsync(plan, result, ...)` and `ExportAsync(plan, result, ...)` from the Q1-owned journey infrastructure. It must not restore ambient selection through filenames, mutable defaults, or recency. Runtime-only OpenVINO results provide runtime options and an exact retained source lease; they never provide an exportable model path.

The managed remediation is ready for integration. Native staging/package acceptance remains pending a correctly ordered H1→M1 handoff with M1 native validation, followed by dependency authorization.
