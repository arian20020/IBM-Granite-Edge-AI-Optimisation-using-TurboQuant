# S1 security and packaging remediation R2

Date: 2026-08-28
Worker: S1
Branch: `audit/ucl-s1-security-remediation-r2`
Authoritative base: `a5ef3558334e50587889140dafba194853938765` (`90c34ab009b744d7b00866fb93e8dbc86363f1b2`)
Frozen ancestor: `4748fe04f19afdf6b27c4c12502b84db325e7294` (`fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91`)
Evidence manifest: `null`

## Outcome

S1-owned production launch boundaries now use fully-qualified executables and closed child environments, bounded and strict manifest/bootstrap readers reject ambiguous or substituted inputs, large artifacts are hashed through streams, and a tracked raw TRX was removed. C0's GGUF quantizer environment correction was retained unchanged and independently passed its real child-process sentinel regression.

No App Control, signing, certificate, firewall, trust-root, or pinned digest was weakened. The managed Release x64 application build passed when package generation and unavailable native-stage packaging were explicitly disabled. A production package/native acceptance run is **blocked**, not passed, because the required independently verified OpenVINO/quantizer stages and UCL App Control/signing phase were not supplied to S1. No native model execution was performed or needed.

The repository-wide privacy census also found inherited research/audit material containing absolute user paths in 594 tracked files (568 evidence files, zero production-source files). Purging or rewriting historical evidence would invalidate cross-worker evidence identities and is therefore a C0/UCL evidence-governance decision, not an S1 product-code change. Production source, the S1 diff, and the evaluated design-time package closure contain no such path. This is recorded as an external policy block rather than a product pass.

## C0 quantizer verification

The C0 correction in `GgufQuantizerEnvironmentPolicy` clears the inherited environment and admits only `SystemRoot`, `WINDIR`, `TEMP`, `TMP`, optional `DOTNET_ROOT`/`DOTNET_ROOT_X64`, and four .NET diagnostics-disable variables. S1 did not duplicate or replace it.

Independent Release verification of `GraniteEdgeAI.GgufQuantization.WorkerClient.Tests.exe` passed 13/13. This includes the real fake-quantizer sentinel proving that `PATH`, proxy variables, secrets, profiler/startup hooks, and arbitrary parent variables do not reach the child, while diagnostics remain disabled.

## Launch-boundary census

| Boundary | Executable control | Environment | argument/output bounds | cancellation and descendants | disposition |
|---|---|---|---|---|---|
| Hardware `llmfit` / llama.cpp probe | Manifested absolute regular file, exact streamed hash, custody handle, suspended `CreateProcessW` | New minimal Unicode environment block; no `PATH`, proxy, token, profiler, or startup inheritance | Command allowlists, 32-argument/command-line bounds, bounded streams | Job assigned before resume; timeout/cancel/descendant cleanup; handles disposed | Fixed and tested by S1 |
| GGUF quantizer | Manifested absolute regular non-reparse file, exact bytes/hash; reverified immediately before start | Existing correct C0 closed policy | Fixed 3/4 arguments; source/output and diagnostic bounds | timeout/cancel kills entire tree and waits boundedly | C0 fix retained; manifest parser hardened by S1 |
| GGUF runtime supervisor | Absolute verified worker/adapter/package paths; exact manifest/package hashes | Closed native environment and explicit inherited-handle list | Bootstrap now 64 KiB, 32 override arguments, 4096 chars each | Job containment, cancellation, timeouts, drain/handle cleanup | Hardened and tested by S1 |
| GGUF CLI child | Absolute bootstrap executable; contained under supervisor job | Inherits the already closed supervisor environment | Bootstrap override bounds plus protocol/diagnostic bounds | entire-tree termination; async disposal/drain | Contract hardened by S1 |
| Model-inspection worker | Absolute verified executable; exact inventory/hash/byte/case checks; custody handles | Closed environment | bounded bootstrap/protocol/streams | suspended launch, job containment, handle allowlist | Existing controls verified; composition manifest reads bounded by S1 |
| OpenVINO worker/converter | Absolute verified closure; exact topology, hash, bytes, case, PE machine, ADS/final path checks | Closed environment | bounded manifest/protocol/streams | suspended launch, job containment, handle allowlist | Existing client controls verified; package acceptance blocked on absent official stages |
| Windows Task Manager | Fixed `%SystemRoot%\System32\Taskmgr.exe`; no caller arguments | OS shell boundary only | immutable empty arguments/verb | process handle disposed | PATH/App Paths resolution defect fixed by S1 |

Test-only spike utilities are not registered or packaged by production composition and were excluded from product launch policy.

## RED-GREEN corrections

| Correction | RED evidence | GREEN evidence |
|---|---|---|
| Hardware child environment | Real child observed an arbitrary parent sentinel (exit 92) | Closed-environment process and policy tests pass |
| Task Manager executable | Isolated linked-source test observed `taskmgr.exe` instead of the exact System32 path | Exact-path contract passes 1/1 |
| Quantizer manifest trust | Duplicate JSON, case-confused identity, reparse manifest/ancestor, and oversized sparse manifest were accepted or failed through an unbounded read | All cases fail closed with bounded/privacy-safe exceptions; full suite 13/13 |
| Runtime manifest trust | Nested duplicate property accepted; sparse detached manifest reached `ReadAllBytes` failure | Recursive duplicate rejection and bounded exact stream read pass 15/15 suite |
| GGUF bootstrap | Duplicate `cliExecutable`, 33 arguments, and 4097-character arguments accepted | Duplicate/size/count/NUL/path bounds pass 17/17 transport suite |
| Runtime model path | Symbolic-link model path accepted | File and every ancestor must be regular/non-reparse; client suite 10/10 |
| Shared app manifest reads | Hardware/OpenVINO composition used unbounded `ReadAllBytes` before strict parsers | `TrustedManifestFile` rejects oversize, reparse, relative, and unstable inputs; hardware suite 209/209 |
| Raw test receipt | Tracked `GraniteEdgeAI.UnitTests.trx` found by privacy scan | File deleted; remaining tracked `.trx/.dmp/.dump/.mdmp` working-tree count is zero |

All new corrections were introduced with a failing test or scan before implementation.

## Manifest, path, and package controls

The S1 readers now require fully-qualified regular files, reject reparse points in the file and every existing ancestor, bound file size before allocation, open with write/delete sharing denied, and perform exact reads. JSON manifests/bootstrap values reject duplicate properties at every object depth. Quantizer identities additionally reject rooted paths, backslashes, NUL, empty/dot/dot-dot segments, overlong names, case-confused identities, and inventories over 256 files. Payload/executable identity remains exact-byte and streamed-SHA-256 checked.

The evaluated Release/x64 design-time closure contained 27 `Content`, zero `EmbeddedResource`, and 11 unique `ProjectReference` items. It had zero test/fixture/audit/report/TRX/dump/debug matches, zero duplicate project references, and exactly one reference to each of the four production worker-client implementations. Eleven duplicate `Content` identities are existing WinUI asset item evaluation, not duplicate workers or alternate implementations.

The complete dynamic package closure could not be produced without verified native stages. The build failed closed when an official OpenVINO stage was absent. S1 did not invent a stage, alter a manifest digest, or disable a control to claim a package pass.

## Privacy and secret disposition

- Deleted the sole tracked raw TRX; Git history remains the recovery source.
- Found no tracked dump/minidump file in the resulting working tree.
- Found no username, hostname, absolute user/model path, credential, secret, raw native output, or private-key marker in production source, the S1 diff, or evaluated package items.
- Existing secret-like strings under test/script fixtures are synthetic sentinels used to verify non-inheritance; they are not credentials and do not enter production closure.
- Inherited research/audit evidence contains absolute user/model paths and raw native evidence. C0/UCL must either approve the historical evidence exception or coordinate a provenance-preserving redaction/purge. S1 did not rewrite signed/hashed evidence or its manifest digests.

## Verification totals

| Executable/suite | Discovered | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| HardwareInspection.Foundation.Tests | 209 | 209 | 0 | 0 |
| GgufQuantization.WorkerClient.Tests | 13 | 13 | 0 | 0 |
| GgufRuntime.Capabilities.Tests | 15 | 15 | 0 | 0 |
| GgufRuntime.Transport.Tests | 17 | 17 | 0 | 0 |
| GgufRuntime.WorkerClient.Tests | 10 | 10 | 0 | 0 |
| GgufRuntime.Worker.Tests | 21 | 21 | 0 | 0 |
| ModelInspection.WorkerClient.Tests | 119 | 119 | 0 | 0 |
| OpenVino.WorkerClient.Tests | 13 | 13 | 0 | 0 |
| SecurityAudit.Tests | 1 | 1 | 0 | 0 |
| GgufQuantization.Capabilities.Tests | 21 | 21 | 0 | 0 |
| GgufRuntime.NativeAdapter.Tests | 44 | 40 | 0 | 4 |
| **Total** | **483** | **479** | **0** | **4** |

The four skips require a separately configured controlled Granite GGUF model and are reported as skipped, not passed. The managed Release/x64 application build completed with 0 warnings and 0 errors under `GenerateAppxPackageOnBuild=false` and native-stage packaging disabled. Component builds for every affected boundary also completed with 0 warnings and 0 errors. `git diff --check` passed.

Native disposition: **blocked**. Native execution: **not run**. Managed native-boundary simulations: **passed**. Package/App Control/signing acceptance: **blocked by absent verified stages and external UCL phase**, not passed.

## File-by-file disposition

| File(s) | Security disposition |
|---|---|
| `FixedHardwareToolAcquisition.cs`, `LlamaCppProbeManifestParser.cs` | Replace unbounded manifest read with the shared bounded trusted reader; expose only the parser's existing bound internally. |
| `WindowsCompatibilityMemoryRecovery.cs` | Replace PATH/App Paths lookup with exact System32 executable identity; preserve fixed empty arguments and existing UX semantics. |
| `ModelInspectionServiceComposition.cs` | Bound and reparse-protect official/converter manifest digest inputs without changing M1/Q1 product semantics. |
| `GgufQuantizerPackageVerifier.cs` | Add bounded exact manifest reads, recursive duplicate rejection, ancestor/file reparse rejection, strict ordinal identity/inventory validation, and streamed payload hashing. |
| `GgufRuntimeClient.cs` | Reject model files and ancestors that are reparse points before launch. |
| `TrustedToolEnvironmentPolicy.cs`, `TrustedToolEnvironmentBlock.cs`, `WindowsSuspendedProcess.cs` | Construct/zero a minimal UTF-16 environment block and pass it explicitly with `CREATE_UNICODE_ENVIRONMENT`. |
| `TrustedManifestFile.cs` | Add reusable absolute, bounded, regular/non-reparse, exact-read manifest input policy. |
| `GgufRuntimeManifestJson.cs`, `GgufRuntimePackageLoader.cs` | Reject duplicate properties and replace unbounded detached-manifest reads with bounded exact streaming. |
| `GgufWorkerBootstrap.cs` | Add payload, path, argument-count, argument-length, NUL, and recursive duplicate-property bounds. |
| `GraniteEdgeAI.UnitTests.trx` | Remove raw tracked test receipt. |
| Fake tool and all S1/unit/security test files | Add real child-process environment assertions and regressions for duplicate, oversize, traversal/case, reparse, exact executable, and large-file behavior. No test asset enters production closure. |
| S1 implementation plan and this report | Process/audit documentation only; excluded from production package closure. |

## Findings requiring another owner

- **Q1:** `OpenVinoOptimizationProvenance.cs`, `OpenVinoProvenance.cs`, and `SealedOpenVinoConversionPipeline.cs` still materialize product-owned output/provenance files with `File.ReadAllBytes`. Q1 should convert large artifact hashing to streamed hashing and apply its own schema bounds without changing optimization/conversion semantics.
- **M1:** `OptimizationCommitJournal.cs` performs an unbounded JSON read. M1 should define the journal's product retention/size semantics and then use a bounded duplicate-rejecting reader.
- **C0/UCL evidence governance:** decide whether the 594 inherited tracked files containing absolute user paths are approved historical evidence or require coordinated redaction/purge and identity regeneration. S1 will not rewrite evidence identities unilaterally.
- **F1/C0 release phase:** provide the independently verified OpenVINO official/converter/TurboQuant and GGUF quantizer stages, then run the fail-closed dynamic package-content, signing, and App Control checks. Absence is blocked, never passed.
- **H1:** no H1 product-semantics change is required; the S1 hardware launch utility contract is covered by 209 passing tests.

## Security-control statement

No security policy, certificate, firewall rule, trust root, package-signing requirement, App Control rule, or trusted manifest digest was changed. The branch is intended for C0 review and integration only through its pushed S1 ref.
