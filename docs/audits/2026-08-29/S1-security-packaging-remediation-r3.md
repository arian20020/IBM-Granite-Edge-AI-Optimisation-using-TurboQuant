# S1 security and packaging remediation R3

Date: 2026-08-29  
Worker: S1  
Branch: `audit/ucl-s1-security-remediation-r3`  
Base commit: `13044fa89d156a9127eadeef639993e7155975b5`  
Base tree: `e33a79b1e57ca41460b519c3348a0d54c28cf9f0`  
Frozen ancestor: `4748fe04f19afdf6b27c4c12502b84db325e7294`  
Frozen tree: `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91`  
Evidence-subject commit: `6c21dc3a80f14522b8638804c1c478ca7702739d`  
Evidence-subject tree: `84132eb2c86902170c0483ae977b81dae7c4e431`  
Evidence manifest: `null`

## Scope and issue disposition

S1 owns R3-015. S1 supports the security/package checks for R3-016 and R3-022 but does not own final shared composition, signed package production, App Control policy, or native/end-to-end acceptance.

| Issue | Disposition | Evidence |
|---|---|---|
| R3-015 | Closed in S1 production code and the production packaging verifier | Both verifiers reject an intermediate package-directory reparse point before traversal; the behavioral regression is RED on the base behavior and GREEN on this branch. |
| R3-016 | Not reproduced in the managed Release output; final disposition remains external | The evaluated/output closure contains zero unresolved package-expression filenames and zero occurrences in text artifacts. The exact six-file quantizer output validates against its exact manifest digest. A full signed package was not produced. |
| R3-022 | Blocked outside S1 | No signed package, App Control run, shared native phase, cleanup acceptance, or end-to-end acceptance was available. No native run was performed and no pass is claimed. |

## Production correction and reachability

`GgufQuantizerPackageVerifier` now validates every intermediate directory for every manifested member, rejects reparse points before entering directories, and inventories the package with an explicit bounded stack rather than recursive traversal. Inventory is bounded to 256 package directories and 257 files (256 manifested members plus the manifest). Existing exact byte-count and streamed SHA-256 checks are unchanged.

The production PowerShell package verifier applies the same pre-traversal ancestor check and bounded non-recursive inventory. It is the verifier invoked by `GgufQuantization.WorkerPackaging.targets`; no alternate verifier or digest bypass was introduced.

Production call chain:

- Definition: `infrastructure/GraniteEdgeAI.GgufQuantization.WorkerClient/GgufQuantizerPackageVerifier.cs`.
- Execution revalidation: `GgufQuantizationWorkerClient.ExecuteAsync` calls `Reverify`, which calls `Verify`.
- Real runner caller: `VerifiedGgufQuantizationRunner` calls `Verify` before constructing `GgufQuantizationWorkerClient`.
- Single runner registration: `OnboardingShellPage.xaml.cs` constructs `VerifiedGgufQuantizationRunner` once.
- Capability caller: `GgufOptimizationProductionAuthority` calls `Verify`; its production `TryCreate` route is composed once from `OnboardingShellPage.xaml.cs`.
- Packaging caller: `GgufQuantization.WorkerPackaging.targets` invokes `Test-GgufQuantizerPackage.ps1` before including quantizer package content.

## RED-GREEN evidence

Defective-base behavior was exercised with the new behavioral test while the production implementation remained at the base behavior:

| Phase | Suite | Discovered | Executed | Passed | Failed | Skipped | Result |
|---|---|---:|---:|---:|---:|---:|---|
| RED 1 | WorkerClient, managed verifier intermediate-directory link | 14 | 14 | 13 | 1 | 0 | Base verifier accepted the redirected intermediate directory. |
| RED 2 | WorkerClient, production PowerShell verifier branch marker | 15 | 15 | 14 | 1 | Base script failed later for a missing member and did not emit the pre-traversal rejection marker. |
| GREEN | R3-015 managed verifier regression | 1 | 1 | 1 | 0 | 0 | Public verifier rejects the redirected intermediate directory. |
| GREEN | R3-015 production-script regression | 1 | 1 | 1 | 0 | 0 | Production packaging verifier rejects the directory before traversal. |
| GREEN | Quantization capabilities | 21 | 21 | 21 | 0 | 0 | Existing capability contract remains green. |
| BLOCKED | Complete WorkerClient suite under App Control | 15 | 15 | 13 | 2 | 0 | All verifier tests pass; two fake-native success cases fail closed because App Control blocks the unsigned fake executable. |

Fresh GREEN aggregate: discovered 23, executed 23, passed 23, failed 0, skipped 0. Arithmetic: `23 = 23 + 0 + 0`. The separate full-suite policy-blocked row is not included in the passing aggregate and is not presented as green.

The main application test assembly was also built and run unpackaged to expose integration constraints. It discovered and executed 1,313 tests: passed 71, failed 1,242, skipped 0 (`1,313 = 71 + 1,242 + 0`). The failures are not reported as product passes. They are dominated by absent Windows App Runtime package activation, absent WinUI dispatcher initialization, signed-package-only assertions, and unavailable process/App Control host conditions. The focused affected `GgufOptimizationExecutorTests` likewise cannot initialize the unpackaged Windows App Runtime host. This is an external acceptance-host block, not evidence against the R3-015 verifier behavior.

## Build and package-content checks

- Release x64 main-app/test composition build: succeeded; 0 errors and 14 pre-existing nullable warnings in `ModelImportDropAccessibilityTests`.
- Build-time GGUF runtime manifest verification: passed.
- Build-time GGUF quantizer verification: passed at manifest SHA-256 `be44b38ca5ce66470a657f7d41d99b11c9233a5846f83a71c99b87672021765d`.
- Managed Release output closure: 227 files; zero unresolved package-expression filenames; zero unresolved package-expression occurrences in inspected text artifacts.
- Quantizer output closure: exactly 6 files; production verifier exit 0 against the exact manifest digest above.
- Worker binary inventory: one GGUF runtime worker executable/DLL pair and one model-inspection worker executable/DLL pair; no duplicate alternate worker implementation found.
- Private artifact scan: no tracked TRX, dump, audit archive, or evidence archive; no username, local user path, credential/token marker, proxy assignment, prompt, model data, or raw provider output added by this branch.
- Security policy: no App Control, signing, certificate, trust-root, firewall, or manifest-digest control was modified.

R3-016 cannot be given a final signed-package disposition from this output-only build. Production Appx generation remains blocked until the verified OpenVINO official worker, converter, and TurboQuant stages and the shared packaging phase are available.

## File-by-file security disposition

| Path | Finding and disposition |
|---|---|
| `infrastructure/GraniteEdgeAI.GgufQuantization.WorkerClient/GgufQuantizerPackageVerifier.cs` | S1 correction: validates intermediate directories, rejects reparse entries before traversal, and bounds non-recursive inventory. Exact-length and streamed hash checks retained. |
| `scripts/gguf-quantization/Test-GgufQuantizerPackage.ps1` | S1 correction: production packaging verifier now performs equivalent ancestor and bounded inventory checks before recursive custody could be lost. |
| `tests/UnitTests/GraniteEdgeAI.GgufQuantization.WorkerClient.Tests/GgufQuantizationWorkerClientTests.cs` | Behavioral RED-GREEN coverage for both production verifier paths. Verifier fixtures copy package bytes without loading the unsigned fake-native assembly. Child PowerShell uses an exact executable, closed minimal environment, diagnostics-disabled variables, bounded wait, async stream draining, and tree termination on timeout. |
| `Features/ModelOptimization/DebugFixtures/OptimizationFixtureGalleryPage.xbf` in Release output | Existing non-S1 production-closure defect corresponding to R3-003. M1/C0 must remove or gate the production DebugFixtures closure; S1 did not cross ownership. |
| Final signed package/App Control/native closure | R3-022 remains with the integration/native owners (C0/E1 and phase-ordered H1/M1/Q1/F1 dependencies). No policy was weakened and no false receipt was created. |

## Native and handoff disposition

Native disposition: `blocked`. No native execution was required for the R3-015 managed/package-verifier correction, no shared native lock was acquired, and no native acceptance result is claimed. The collision-safe S1 R3 receipt is published separately after exact-commit verification and remote-ref validation. `evidenceManifest` is `null`.
