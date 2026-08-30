# S1 security and packaging remediation R4

Date: 2026-08-30  
Worker: S1  
Branch: `audit/ucl-s1-security-remediation-r4`  
Base commit: `95e52cd313520a565caebd1e8f88f00e046e3682`  
Base tree: `ba20178faf9926b98be3af32bc83734b385f7610`  
Frozen campaign commit: `4748fe04f19afdf6b27c4c12502b84db325e7294`  
Frozen campaign tree: `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91`  
Implementation-subject commit: `da49c3d1b3feaf18d9cb6199d6ff3612f86cd64a`  
Implementation-subject tree: `9724b1d426da840d03b67de6a8fec55a32955320`  
Evidence manifest: `null`

## Outcome

The managed S1 trust-boundary remediation is complete at the implementation subject above. Native child processes now receive a closed environment, canonical `SystemRoot`/`WINDIR`, and an application-owned per-operation `TEMP`/`TMP` directory. The directory has a protected current-user ACL, non-reparse ancestry, bounded cleanup, observable cleanup failure, and a retained directory handle that denies rename/delete while the operation is live. GGUF runtime, GGUF quantization, Model Inspection, OpenVINO, and hardware-process clients use the shared primitive for their complete process/session lifetime. Managed tools receive `DOTNET_ROOT*` only on the two managed-worker paths that require it.

The supplied R4 receipt and evidence schemas are committed byte-for-byte and are exercised with draft-2020-12 structure validation plus shared semantic arithmetic checks. The Release x64 managed application and six affected test assemblies build successfully; the serial managed test aggregate is 377 discovered, 377 executed, 377 passed, 0 failed, 0 skipped (`377 = 377 + 0 + 0`).

This machine is not an authoritative Intel-native, signed-package, App Control, performance, or visual-acceptance host. Native disposition is `blocked`, and no such acceptance is claimed.

## Requirement disposition

| Requirement | Disposition | Evidence |
|---|---|---|
| Closed child environment and canonical Windows roots | Complete | `TrustedToolEnvironmentPolicy` copies only explicit approved keys, requires `SystemRoot` and `WINDIR` to resolve to the same canonical local Windows directory, validates every ancestor, and forces all .NET diagnostic entry points off. Hostile-parent, case-confusion, relative-root, mismatched-root, and canonical-root tests pass. |
| Private per-operation TEMP/TMP | Complete | `TrustedToolOperationEnvironment` creates `%LOCALAPPDATA%\GraniteEdgeAI\TrustedToolTemp\operation-{GUID}`, applies a protected current-user ACL, rejects reparse ancestry, retains rename/delete-denying custody, bounds cleanup at 512 entries and depth 16, and exposes `CleanupSucceeded`. Poisoned parent TEMP/TMP, ACL, cleanup bound, and race-replacement tests pass. |
| Every affected consumer | Complete for S1-owned managed clients | Hardware external process, GGUF runtime session, quantizer operation, Model Inspection worker session, and OpenVINO inspection/conversation sessions own the operation environment until process cleanup. Cleanup failure maps to stable closed diagnostics or exceptions. No full parent environment is copied. |
| Explicit native launch and containment review | Preserved | Existing exact executable resolution, argument construction without a shell, asynchronous bounded output drains, cancellation/timeouts, Job/process-tree containment, and zero-descendant checks remain covered by the affected suites. No alternate shell or PATH lookup was introduced. |
| Package/verifier custody | Preserved | S1 R3 bounded non-recursive inventory, intermediate-directory reparse rejection, exact member/PE/hash/length checks, and build-time verifier behavior remain green, including wide/oversized and mutation cases. |
| Package capability | Complete for declared policy | Evaluated manifest capabilities are exactly `internetClient`, `runFullTrust`, and `systemAIModels`. `internetClient` is the sole general network capability for the user-initiated verified-download route. Source audit found no upload/cloud-proxy path added by this branch. |
| R4 campaign schemas | Complete | Receipt schema: 4,713 bytes, SHA-256 `e6ad2e98c09ba93ed2492ac8d6adec8353e309d031f9e16bf01736ce4e04f895`. Evidence schema: 3,663 bytes, SHA-256 `f7626490186450d60de0c9b22c0d2ddb72c2f6815f38930bb23b0199f275b37e`. Tests cover worker/branch/ref/subject/report identity, arithmetic, native disposition, evidence status/grade, additional properties, and remote/bundle mutations. |
| Evidence arithmetic primitive | Complete | `GraniteEdgeAI.R4Handoff.Validation.R4HandoffSemanticValidator` validates receipt totals and every evidence-command total, and is consumed by the security audit tests. |
| Security review | Complete | Independent review raised no Critical findings. Four initial Important findings and two follow-up Important findings were corrected; the final exact-subject re-review found no remaining Critical or Important issue. |

## RED/GREEN and managed verification

Behavioral RED was recorded before each causal implementation:

| Phase | Check | Result |
|---|---|---|
| RED | Canonical Windows root and `WINDIR` agreement | 2 new tests failed against base behavior. |
| RED | Poisoned inherited TEMP/TMP | Fake tool observed the poisoned parent directory and exited with marker code 92. |
| RED | Directory replacement race | `Directory.Move` succeeded while the operation was live. |
| Mutation | Canonical-root predicate deliberately inverted | Focused suite failed, then the production predicate was restored. |
| GREEN repeat | Focused trusted-environment campaign | 23/23 passed on each of three runs before the final custody addition. |

Fresh final serial verification after the complete implementation subject was built:

| Suite | Discovered | Executed | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|---:|
| Security audit | 4 | 4 | 4 | 0 | 0 |
| GGUF runtime worker client | 10 | 10 | 10 | 0 | 0 |
| GGUF quantization worker client | 15 | 15 | 15 | 0 | 0 |
| Model Inspection worker client | 119 | 119 | 119 | 0 | 0 |
| OpenVINO worker client | 13 | 13 | 13 | 0 | 0 |
| Hardware Inspection foundation | 216 | 216 | 216 | 0 | 0 |
| **Aggregate** | **377** | **377** | **377** | **0** | **0** |

All six projects were first built Release/x64 with VS 2026 MSBuild, then their produced assemblies were run one at a time with VS 2026 VSTest. Serial execution is intentional: the quantizer and hardware suites each contain an oversized-file test and parallel assembly execution can contend for more than 4 GiB of temporary disk.

## Build, launch, and native disposition

The Release x64 managed application build passed with package generation disabled and unavailable native stages explicitly marked non-required:

`MSBuild "IBM Granite with TurboQuant (Intel).csproj" /t:Build /p:Configuration=Release /p:Platform=x64 /p:GenerateAppxPackageOnBuild=false /p:GgufQuantizerPackagingRequired=false /p:OpenVinoOfficialWorkerPackagingRequired=false /p:OpenVinoConverterPackagingRequired=false /p:OpenVinoTurboQuantPackagingRequired=false /p:PublishReadyToRun=false`

The strongest package gate failed closed as designed. With normal requirements it first required `GgufQuantizerStageDirectory`; after disabling only that absent stage it required `OpenVinoOfficialWorkerStageDirectory`. No fabricated stage, unsigned substitute, trust change, or policy bypass was used.

The exact built executable was launched at `2026-08-30T04:44:14Z`. It exited before presenting a window with code `-532462766` and left zero exact executable processes. Because no app window existed, the required route/file-13 visual smoke and screenshot inspection were externally blocked; no visual pass is claimed. This launch result is recorded as an activation-host block, not as application success. No App Control, execution-policy, certificate, signing, firewall, registry, or external-installation setting was changed.

Native disposition: `blocked`. Intel-native execution, signed-package acceptance, App Control acceptance, performance measurement, and final end-to-end route acceptance remain for the authoritative C0/E1 environment.

## Package closure

Machine-readable closure: `docs/audits/2026-08-30/S1-package-closure-r4.json`  
Bytes: 71,749  
SHA-256: `ddefc21a1f01b6b2f12caf0361f1dc4dd332c2cd8221b95f7f2649837ebdd4e6`

The manifest records all 240 files under the exact managed Release output with normalized relative path, byte length, and SHA-256. A fresh independent comparison found 240 manifest entries, 240 output files, and zero missing, extra, length-mismatched, or hash-mismatched entries.

Closure findings:

- Zero filenames contain unresolved MSBuild expressions.
- Zero `.trx`, dump, credential, secret, or evidence artifacts are present.
- Worker inventory is one GGUF runtime executable/DLL pair and one Model Inspection executable/DLL pair; no duplicate alternate worker implementation is present.
- The branch diff contains no local user path or credential/token/proxy value. The only credential-shaped text is an intentional hostile-parent test marker.
- The existing Release artifact `Features/ModelOptimization/DebugFixtures/OptimizationFixtureGalleryPage.xbf` remains an Important C0-owned closure defect. S1 did not alter Model Optimization presentation ownership.
- Native stages are absent, so comparison against an authoritative fully staged/signed R3 package is unavailable. The manifest records the exact reason and source-level base-to-candidate changes.

## Diagnostics and privacy

The operation-environment API emits one bounded stable failure message and never echoes rejected paths or inherited values. Consumer cleanup failures map to existing stable diagnostic codes or bounded exception text. The final source diff and output closure were scanned for username paths, credentials, tokens, proxy assignments, raw evidence, dumps, and model assets; no private value added by this branch was found. No model or hardware-data upload path was introduced.

## Independent review

The first exact base-to-subject review reported no Critical findings and four Important findings. After those corrections, a second review identified child-entry cleanup TOCTOU and test-only/overflowing semantic arithmetic. The final implementation retains non-delete-sharing handles for the root and every child, inspects each opened identity, rejects reparse points, deletes by handle, widens arithmetic to `long`, and supplies a bounded schema-plus-semantic CLI. Final exact-subject re-review of `95e52cd313520a565caebd1e8f88f00e046e3682..da49c3d1b3feaf18d9cb6199d6ff3612f86cd64a`: no remaining Critical or Important findings.

## Exact C0 consumption list

1. Merge implementation subject `da49c3d1b3feaf18d9cb6199d6ff3612f86cd64a` (tree `9724b1d426da840d03b67de6a8fec55a32955320`) without replacing the closed-environment primitive with parent-environment enumeration.
2. Preserve `TrustedToolOperationEnvironment` ownership for the entire native process/session lifetime in hardware, GGUF runtime, quantizer, Model Inspection, and OpenVINO composition. Treat `CleanupSucceeded == false` as a closed failure.
3. For any additional managed worker, call `CreateCurrent(includeDotnetRoots: true)` only when its verified runtime genuinely requires `DOTNET_ROOT`/`DOTNET_ROOT_X64`; native tools must use `false`.
4. Consume `GraniteEdgeAI.R4Handoff.Validation` when validating final worker receipts/evidence so arithmetic is checked in addition to JSON Schema structure.
5. Consume the committed byte-identical R4 schemas at their exact paths and do not reintroduce the v1 self-referential final-tip field.
6. Preserve manifest capabilities as the exact reviewed set. Do not add broader private-network/server/upload capabilities; independently connect `internetClient` only to the verified user-initiated download route during integration.
7. Remove or Release-gate `Features/ModelOptimization/DebugFixtures/OptimizationFixtureGalleryPage.xbf`, then regenerate the 240-entry closure allowlist and require zero DebugFixtures artifacts.
8. Supply independently verified GGUF quantizer and OpenVINO official worker/converter/TurboQuant stage roots, rerun the strongest package gate, produce the signed package, and repeat closure/hash verification on the authoritative host.
9. Rerun the exact app/file-13 smoke, cancellation/timeout, zero-descendant, screenshot/privacy, Intel-native, App Control, and performance acceptance on the authoritative C0/E1 machine. Do not convert this report's blocked rows into passes.

No shared-shell or `ModelInspectionServiceComposition` edit is proposed. The reusable implementation is confined to the narrow foundation primitive and explicit project references.

## Changed-path inventory

The implementation subject changes the package manifest; package-closure and schema artifacts; the reusable trusted-operation environment and hardware runner; GGUF runtime, quantizer, Model Inspection, and OpenVINO worker clients; the shared semantic validator; focused security and process tests; and the committed execution plan. `git diff --check` is clean. The coordinator worktree and all machine/user security policy remain untouched.
