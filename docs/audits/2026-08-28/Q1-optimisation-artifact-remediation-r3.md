# Q1 optimisation and artifact identity remediation R3

## Disposition

Q1-owned R3-009 through R3-013 are implemented at evidence subject commit `31302b4f46cf562271767e0bbcfd241bb4db28f5`, tree `1b9e14c5b5a2d9a981caeae7ebae020554a6ec76`. The RED commit is `4fd9ec3f6758b866fba40e0028d4280164b25c5d`, tree `a0a441d267ad9e6e1ecfe0542343c65ace0fe839`.

The overall Q1 disposition is mixed. Managed Q1 behavior is verified, but Q1 does not claim R3-008, R3-014, R3-018, R3-019, or R3-022 closure. The live shared shell still calls the fail-closed `LastPublishedDirectory` compatibility seam, and the required H1 then M1 R3 receipts and native lock were absent. Q1 therefore did not enter native staging or package construction and did not publish a handoff receipt.

## Implemented behavior

- External OpenVINO export now accepts a fully qualified absent user-selected destination outside the private publication root. It refuses an existing destination without changing it.
- Runtime-profile and persistent-package validation enumerate immediate entries only, inspect attributes before opening files, and reject directories and reparse points without traversing child topology.
- Storage custody walks every existing path component from the volume root and rejects an intermediate reparse point before granting root, child, or ordinary-file access.
- OpenVINO temporary export cleanup is exact, non-recursive, reparse-safe, observable, and sanitized. Cleanup failure is no longer suppressed, including when it supersedes cancellation.
- Publication registration, resolution, identity hashing, exact Chat target creation, and export are asynchronous and cancellation-aware. Large-file SHA-256 and copies use a pooled 128 KiB buffer, explicit expected lengths, a 1 TiB ceiling, and per-read cancellation.
- Export resolves only the exact execution result, copies only verified top-level package members into a same-parent temporary directory, re-registers and re-resolves the copied package, and atomically promotes without overwrite.
- Export returns a bounded typed disposition containing only plan and execution identities. Runtime-only results return `RuntimeOnly` and cannot masquerade as model files.
- `CreateChatTargetAsync` resolves the exact result and preserves exact runtime options. Runtime-only Chat retains the exact source custody lease; persistent Chat uses the verified package directory.
- `ExecuteAsync` awaits exact publication registration. Late, duplicate, changed-model, changed-hardware, substituted-plan, and stale-result protections from the shared execution contract remain covered by the Q1 identity suite.

## Issue accounting

| Issue | Q1 disposition | Evidence |
|---|---|---|
| R3-009 | Passed | External destination and existing-destination preservation execute in the 55-test optimization suite. |
| R3-010 | Passed | A rejected child containing 4,096 long-name entries remains below the allocation ceiling; only top-level topology is inspected. |
| R3-011 | Passed | The Q1 identity suite rejects an intermediate directory link and preserves its controlled target. |
| R3-012 | Passed | A real export race injects an unexpected child; cleanup produces a sanitized observable failure rather than suppressing it. |
| R3-013 | Passed | Pre-cancelled identity does not open a locked artifact; cancellation during a sparse 512 MiB identity pass is prompt and allocates less than 32 MiB. |
| R3-008 / R3-014 | C0 pending | Q1 provides compiled `CreateChatTargetAsync` and typed `ExportPersistentAsync` seams. The C0-owned shell has not consumed them and still references the null ambient compatibility seam. |
| R3-018 | T1/C0 pending | Exact-result tests pass in Q1 harnesses; live cross-feature composition remains outside Q1 ownership. |
| R3-019 | Blocked | No H1 or M1 R3 receipt and no native-phase lock were present at the gate. |
| R3-022 | Blocked | Native/package/App Control end-to-end acceptance was not authorized by the ordered receipt gate. |

## RED-GREEN evidence

The committed RED tests executed against the defective production baseline at the RED commit. Four OpenVINO regressions failed for the expected reasons: private-root destination rejection, recursive child materialization, cancellation-insensitive identity access, and suppressed cleanup failure. The complete-ancestor regression also failed because an intermediate link was accepted. Totals were 5 discovered, 5 executed, 0 passed, 5 failed, 0 skipped.

The exact subject commit was then checked out directly and rerun through the installed signed .NET host:

| Verification | Discovered | Executed | Passed | Failed | Skipped | Disposition |
|---|---:|---:|---:|---:|---:|---|
| OpenVINO optimization and R3 regressions | 55 | 55 | 55 | 0 | 0 | Passed |
| Q1 identity and recovery | 37 | 37 | 37 | 0 | 0 | Passed |
| OpenVINO activation | 3 | 3 | 3 | 0 | 0 | Passed |
| OpenVINO contracts | 205 | 205 | 205 | 0 | 0 | Passed |
| OpenVINO worker client | 13 | 13 | 13 | 0 | 0 | Passed |
| GGUF quantization contracts | 7 | 7 | 7 | 0 | 0 | Passed |
| GGUF quantization worker client | 8 | 8 | 8 | 0 | 0 | Passed |
| Debug x64 managed application compile, design-time packaging disabled | 0 | 0 | 0 | 0 | 0 | Passed with zero warnings/errors |
| OpenVINO worker process | 69 | 69 | 18 | 36 | 15 | Blocked/mixed: pinned SDK and authorized native stages absent |

The seven passing executable suites total 328 discovered, 328 executed, 328 passed, 0 failed, and 0 skipped. The worker-process project compiled with zero warnings/errors after Q1 source-link correction, but execution honestly records 18 passed, 36 failed, and 15 skipped: fixture publication requested the absent pinned SDK, and native tests required authorized converter, official-worker, or TurboQuant-worker stages. No SDK, model, tool, or worker dependency was downloaded.

The broader OpenVINO unit host compiled with zero warnings/errors. Its diagnostic execution produced 425 discovered, 414 passed, 4 failed, and 7 skipped; the four failures were pinned-SDK subprocess checks and the skips declared missing native stages. It is not counted as exact-subject pass evidence. The full cross-feature host compiled but its DLL was denied by App Control, so it is also not counted as a pass. Policy was not weakened.

## Production reachability and C0 integration register

`RegisterAsync` has one production journey caller: `OpenVinoOptimizationExecutor.ExecuteAsync`. `ResolveAsync` is called by the exact Chat seam and exact export transaction. The application project semantically compiled these sources with zero warnings/errors.

The two final consumers are deliberately recorded as pending:

- C0 must call `CreateChatTargetAsync(state.Result, cancellationToken)`, pass the resulting exact package/source and runtime options to model inspection, and retain/dispose the returned target for the activation lifetime.
- C0 must call `ExportPersistentAsync(state.Result, selectedDestination, maximumBytes, cancellationToken)` and branch on its typed disposition. It must not pre-copy from an ambient directory.

Q1 did not edit onboarding, navigation, XAML, composition, or presentation. The compatibility `LastPublishedDirectory` property remains fail-closed as `null` only because C0-owned source still references it; it is not a functional target selector.

## Native and package gate

At the preflight gate, the handoff directory contained only an S1 R3 receipt. H1 R3 and M1 R3 receipts and a native-phase lock were absent. The pinned SDK requested by repository configuration was also absent; managed verification used the installed SDK explicitly. In accordance with the required H1-to-M1-to-Q1 order, no Q1 native stage, Debug x64 package, package-content acceptance, or native App Control execution was attempted.

## Scope, integrity, and privacy

- Frozen ancestor `4748fe04f19afdf6b27c4c12502b84db325e7294` is an ancestor of the subject.
- `git diff --check` passed for the complete R3 subject range.
- Changed-path scans found no onboarding, navigation, XAML, presentation, binary, or model artifact changes.
- The changed OpenVINO registry contains no recursive enumeration or whole-file model read.
- The two schema snapshots committed after the subject match the supplied reference bytes and SHA-256 values exactly.
- The evidence manifest validates against the committed draft-2020-12 schema using the committed locked validator; every command satisfies `executed = passed + failed + skipped` and `discovered >= executed`.
- Evidence contains no username, hostname, absolute local path, model filename, model bytes, prompt, credential, token, raw provider output, or downloaded dependency.

## Nonclaims

This report does not claim live shared-shell Chat/export integration, H1/M1 native ordering, a validated native worker stage, Debug x64 package construction, package-content acceptance, or end-to-end App Control acceptance. It does not claim that a runtime-only configuration is an exportable model file. Because those gates remain open, no Q1 R3 handoff receipt is published.
