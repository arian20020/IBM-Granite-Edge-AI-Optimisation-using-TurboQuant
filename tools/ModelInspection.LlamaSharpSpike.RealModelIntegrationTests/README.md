# LLamaSharp trusted real-model integration tests

**Status:** Local trusted target-machine gate verified — 20/20 passed  
**Verification date:** 2026-08-05  
**CI tier:** Tier 2 — local trusted run now; manual repository-owned workflow later  
**Shared support:** [Probe process test support](../ModelInspection.LlamaSharpSpike.TestSupport/README.md)  
**Coverage register:** [LLamaSharp Runtime Test Coverage Matrix](../../docs/testing/LLamaSharp-Runtime-Test-Coverage-Matrix.md)  
**Tier 1 evidence:** [Hosted model-free verification](../../docs/testing/evidence/2026-08-04-llamasharp-tier1-verification.md)  
**Tier 2 evidence:** [Local trusted verification](../../docs/testing/evidence/2026-08-05-llamasharp-tier2-local-verification.md)

## Purpose

This Microsoft.Testing.Platform project verifies the exact LLamaSharp CPU
runtime against the controlled Granite 4.1 3B Q4_K_M model and committed
malformed GGUF fixtures.

All native/model scenarios execute the published feasibility tool as a bounded
child process. The MSTest host never loads llama.cpp directly. This prevents a
native abort in one scenario from terminating the complete test campaign.

## Verified result

The complete `RealModelIntegration` category ran locally on the target Windows
x64 laptop:

```text
Result:          Passed
Total:           20
Succeeded:       20
Failed:          0
Skipped:         0
Duration:        approximately 3 minutes 9 seconds
```

Independent model and evidence gates also passed:

```text
Model SHA-256 before/after: unchanged
Evidence files scanned:     56
Privacy findings:           0
GGUF files in evidence:      0
Model-sized evidence files: 0
Exact model-copy hashes:     0
```

The retained ignored evidence root was:

```text
artifacts/model-inspection/llamasharp/real-model-local/20260805-010601/
```

## Controlled model identity

```text
Filename:                    granite-4.1-3b-Q4_K_M.gguf
Byte length:                 2,099,501,664
SHA-256:                     662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29
Architecture:                granite
Model name:                  Granite 4.1 3b
GGUF file type:              15
Quantisation version:        2
Tokenizer model:             gpt2
Declared context:            131,072
Embedding size:              2,560
Layers:                      40
Attention heads:             40
KV heads:                    8
Metadata count:              31
Vocabulary count:            100,352
Tokenizer smoke token count: 1
Chat template:               present
```

The model remains outside Git. Only its expected identity and safe-depth
contract are committed in:

```text
ControlledModels/granite-4.1-3b-q4-k-m.json
```

## Required environment

```text
LLAMASHARP_SPIKE_PUBLISH_DIR
    Release / win-x64 published feasibility tool

GRANITE_TEST_MODEL_PATH
    Readable path to the exact controlled Granite model

LLAMASHARP_REAL_MODEL_EVIDENCE_DIR
    Optional retained evidence root
```

Missing configuration, wrong filename, wrong byte length, wrong SHA-256 or an
unreadable file fails before the native runtime is called.

## Test hierarchy

```text
RealModelIntegrationTests/
├── AssemblyInfo.cs
├── ControlledModels/
├── Support/
│   ├── ControlledModelConfigurationTests.cs
│   ├── RealModelEvidenceDirectory.cs
│   └── RealModelTestContext.cs
├── RealModel/
│   ├── GraniteVocabOnlySuccessTests.cs
│   └── GraniteVocabOnlyRepeatabilityTests.cs
├── Cancellation/
│   ├── WholeOperationCancellationTests.cs
│   └── NativeLoadCancellationTests.cs
├── MalformedModels/
│   └── MalformedModelProcessTests.cs
├── FileAccess/
│   ├── MissingAndDirectoryModelProcessTests.cs
│   ├── LockedModelProcessTests.cs
│   ├── LockedOutputProcessTests.cs
│   └── UnsafeOutputProcessTests.cs
└── Security/
    ├── EvidencePrivacyTests.cs
    └── NetworkObservationTests.cs
```

The physical test assembly name is shortened to:

```text
GraniteEdgeAI.LlamaSharp.RealModelTests
```

The namespace remains descriptive. Only the physical assembly name is short so
Windows process-start paths remain within supported limits.

The assembly is non-parallel because scenarios share one large read-only model
and native-runtime package while retaining separate child processes and
scenario-specific evidence paths.

## Covered and verified scenarios

### Controlled success and repeatability

- exact package/runtime/model evidence;
- real vocabulary, tokenizer and chat-template evidence;
- safe nullable parameter count;
- genuine native progress fractions;
- deterministic native disposal;
- before/after model SHA-256;
- three sequential independent child runs;
- stable model evidence and chat-template hash;
- no stale temporary writer files.

### Cancellation

```text
--cancel-after-ms
    timer begins after the initial integrity snapshot

--cancel-native-after-ms
    timer begins immediately before LLamaWeights.LoadFromFileAsync
```

The trusted suite verifies controlled cancellation, structured evidence and
model preservation at both scopes. `Ctrl+C` remains a later production
ViewModel/UI integration concern rather than being simulated in this project.

### Malformed and hostile input

Each represented `I-*` fixture runs in an independent child process. The suite
records:

- fixture identity and expected hash;
- child termination and exit code;
- structured failure evidence where JSON is available;
- native termination where managed evidence cannot be produced;
- before/after fixture hash;
- matrix JSON and Markdown summaries.

A deterministic random-byte `.gguf` is also tested outside the retained
evidence tree and deleted afterwards.

### File and output failures

- missing model;
- directory supplied as model;
- model held with `FileShare.None`;
- evidence output held with `FileShare.None`;
- output parent that is a normal file;
- evidence output resolving to the model path.

The controlled model is never used as a writable fixture.

### Privacy and network observations

- canonical model path absent from JSON, stdout and stderr;
- no `modelPath` or full chat-template property;
- only chat-template presence, length and SHA-256 are retained;
- no `.gguf`, model-sized, oversized or model-hash-matching evidence file;
- `netstat.exe` observes TCP `LISTENING` and `ESTABLISHED` endpoints owned by
  the exact child PID;
- no prohibited endpoint observation caused a test failure.

The network scenario is an observation rather than a physical
network-disconnection test. A disconnected-machine run remains explicitly
deferred.

## Reproduce locally

Use the maintained runbook:

```text
docs/testing/runbooks/LLamaSharp-Trusted-Real-Model-Runbook.md
```

The runbook verifies the model identity, publishes the exact runtime, runs at
least 20 trusted tests, independently checks the model hash and performs a
PowerShell-compatible retained-evidence privacy scan.

## Trusted self-hosted workflow

The workflow remains `workflow_dispatch` only and targets:

```text
self-hosted
Windows
X64
workbook05
intel-target
```

It becomes dispatchable after the workflow file exists on the repository
default branch. Before upload it rechecks model integrity and scans retained
evidence. Upload is fail-closed: it runs only when both the final model-integrity
step and artifact-privacy scan have explicit success outcomes.

The future service-account workflow is a deployment/workflow verification step.
It does not replace the verified local target-machine runtime evidence.

## Non-claims

This suite does not test:

- WinUI Cancel button wiring;
- production `ILlamaModelProbe` or inspection service;
- full tensor checking or model allocation;
- context or KV-cache creation;
- generation, quality or CPU performance;
- Vulkan or GPU offload;
- TurboQuant, PolarQuant or QJL;
- OpenVINO;
- Hardware Fit.

Those remain separate application and backend-verification gates.