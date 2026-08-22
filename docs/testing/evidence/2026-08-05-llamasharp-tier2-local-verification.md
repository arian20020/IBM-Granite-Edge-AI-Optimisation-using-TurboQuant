# LLamaSharp Tier 2 Local Verification Evidence

**Date:** 2026-08-05  
**Branch:** `feature/model-inspection`  
**Verified commit:** `0c6b417a66478ddf5ebc0ccc64f5f76bc3ac2ca9`  
**Environment:** Visual Studio 2026 Developer PowerShell 18.7.3, Windows x64 target laptop  
**Execution mode:** Local trusted real-model integration suite

## Controlled model

```text
Filename: granite-4.1-3b-Q4_K_M.gguf
Length:   2,099,501,664 bytes
SHA-256:  662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29
```

The model path was outside the repository. The test campaign verified the exact filename, byte length and SHA-256 before publishing or executing the trusted suite.

## Command boundary

The run:

1. restored the isolated feasibility project;
2. restored the trusted real-model test project;
3. published the exact framework-dependent `win-x64` feasibility executable;
4. ran every test categorised as `RealModelIntegration` in child processes;
5. recalculated the model SHA-256 after test execution;
6. scanned all retained evidence files for model leakage.

The published probe remained CPU-only:

```text
LLamaSharp                  0.27.0
LLamaSharp.Backend.Cpu      0.27.0
Mapped llama.cpp commit     3f7c29d318e317b63f54c558bc69803963d7d88c
CUDA                        disabled
Vulkan                      disabled
GPU layers                  0
```

## Test result

```text
Test project:
ModelInspection.LlamaSharpSpike.RealModelIntegrationTests

Result:       Passed
Total:        20
Succeeded:    20
Failed:       0
Skipped:      0
Duration:     3 minutes 9 seconds
```

The suite exercised:

- the exact controlled Granite success contract;
- three sequential repeatability runs;
- post-preflight cancellation;
- native-load-scoped cancellation;
- all committed malformed GGUF fixtures represented by the suite;
- deterministic random-byte GGUF input;
- missing model input;
- directory-as-model input;
- locked model input;
- locked evidence output;
- invalid output-parent handling;
- model/output collision protection;
- evidence privacy;
- artifact privacy;
- process-owned TCP endpoint observation.

Native/model work ran in bounded child processes, so native termination in one case could not terminate the complete MSTest host.

## Model integrity

Independent PowerShell SHA-256 values were identical:

```text
Before:
662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29

After:
662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29
```

Result:

```text
Controlled model preserved: PASS
```

## Retained-evidence privacy scan

The first audit-script attempt used `System.IO.Path.GetRelativePath`, which was unavailable in the active Developer PowerShell runtime. This was a verification-script compatibility defect, not a test or runtime failure.

The scan was rerun using normalised full paths plus prefix and substring checks.

```text
Evidence files scanned: 56
Suspicious findings:     0
GGUF files found:        0
Model-sized files found: 0
Model-hash matches:      0
Files above 100 MB:      0
```

Result:

```text
Retained-evidence privacy scan: PASS
```

The retained local evidence root was:

```text
artifacts/model-inspection/llamasharp/real-model-local/20260805-010601/
```

The ignored local evidence included controlled success, repeatability, cancellation, malformed-input, file-access, privacy and network-observation reports. The model itself was not copied into the evidence tree.

## Final Tier 2 conclusion

```text
Trusted local real-model gate: PASS
```

The evidence supports these claims for the tested runtime and model:

- the matched LLamaSharp CPU runtime recognises and inspects the controlled Granite GGUF at `VocabOnly` depth;
- the inspected success path is repeatable across three sequential runs;
- cancellation is reported through controlled process results;
- malformed and hostile input cases remain contained in child processes;
- file-access and unsafe-output scenarios are handled by the trusted suite;
- the original model remains byte-for-byte unchanged;
- retained evidence does not contain a GGUF, model-sized file or exact model copy;
- no prohibited TCP endpoint observation caused a trusted-test failure.

## Explicit non-claims

This result does not prove:

- full tensor allocation;
- context or KV-cache creation;
- token generation;
- inference quality;
- full CPU performance;
- Hardware Fit;
- Vulkan initialisation or GPU offload;
- TurboQuant, PolarQuant or QJL operation;
- OpenVINO inspection;
- WinUI runtime integration;
- functional WinUI cancellation.

Those remain separate engineering and application gates.

## Follow-up

1. Reconcile the coverage matrix and source-adjacent READMEs with this result.
2. Replace the incompatible relative-path command in the trusted runbook.
3. Update draft PR #44 with the 20/20 result and privacy evidence.
4. Complete a whole-branch static review.
5. Design the production application boundary beginning with `ILlamaModelProbe` and a protected worker-process implementation.
