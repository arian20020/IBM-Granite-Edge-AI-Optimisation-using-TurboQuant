# Model Inspection LLamaSharp feasibility tool

**Status:** Tier 1 and local trusted Tier 2 verified  
**Last reviewed:** 2026-08-05  
**Runtime decision:** [ADR-001](../../docs/architecture/decisions/ADR-001-llamasharp-application-runtime.md)  
**Inspection-depth decision:** [ADR-002](../../docs/architecture/decisions/ADR-002-core-inspection-versus-backend-verification.md)  
**Runtime-test design:** [LLamaSharp runtime test architecture](../../docs/superpowers/specs/2026-08-04-llamasharp-runtime-test-architecture-design.md)  
**Tier 1 evidence:** [Hosted verification](../../docs/testing/evidence/2026-08-04-llamasharp-tier1-verification.md)  
**Tier 2 evidence:** [Local trusted verification](../../docs/testing/evidence/2026-08-05-llamasharp-tier2-local-verification.md)

## Purpose

This isolated console project proves and diagnoses the selected managed/native
application runtime before LLamaSharp enters the WinUI application.

```text
LLamaSharp 0.27.0
        ↓
LLamaSharp.Backend.Cpu 0.27.0
        ↓
llama.cpp 3f7c29d318e317b63f54c558bc69803963d7d88c
```

It supports two runtime depths:

```text
Mode 1 — native CPU backend smoke
    → no model
    → proves native-library discovery and runtime identity

Mode 2 — CPU VocabOnly model probe
    → one controlled local GGUF
    → reads proportionate metadata, vocabulary, tokenizer and chat-template evidence
    → verifies disposal and original-file integrity
```

Neither mode is referenced by the WinUI application.

## Why this remains isolated

Native loading can terminate a process before managed exception handling runs.
The first Granite experiment exposed that risk when an unsafe `VocabOnly`
hyperparameter getter reached `GGML_ABORT`. The collector was corrected to use
safe GGUF metadata, and automated native/model scenarios now execute the
published tool as child processes.

```text
MSTest host
    ↓
ProbeProcessRunner
    ↓
feasibility executable
    ↓
LLamaSharp / llama.cpp
```

This preserves the future application boundary:

```text
ModelInspectionPage
    ↓
ModelInspectionViewModel
    ↓
IModelInspectionService
    ↓
ILlamaModelProbe
```

## Source structure

```text
ModelInspection.LlamaSharpSpike/
├── README.md
├── ModelInspection.LlamaSharpSpike.csproj
├── Program.cs
├── PinnedApplicationRuntime.cs
├── SpikeOptionsParser.cs
├── CpuNativeRuntimeConfiguration.cs
├── NativeBackendSmokeProbe.cs
├── NativeBackendSmokeResult.cs
├── JsonEvidenceWriter.cs
└── ModelProbe/
    ├── README.md
    ├── IModelFileHasher.cs
    ├── Sha256ModelFileHasher.cs
    ├── ModelFileSnapshot.cs
    ├── ModelFileSnapshotService.cs
    ├── ModelProbeSafetyValidator.cs
    ├── NativeLoadProgressRecorder.cs
    ├── ProbeFailure.cs
    ├── ProbeFailureMapper.cs
    ├── SensitiveTextRedactor.cs
    ├── ProbeResultFinalizer.cs
    ├── ChatTemplateEvidenceFactory.cs
    ├── VocabOnlyMetadataProjection.cs
    ├── VocabOnlyModelProbeResult.cs
    ├── VocabOnlyEvidenceCollector.cs
    └── VocabOnlyModelProbe.cs
```

## Exact runtime identity

| Item | Value |
|---|---|
| Managed package | `LLamaSharp` `0.27.0` |
| CPU backend package | `LLamaSharp.Backend.Cpu` `0.27.0` |
| LLamaSharp tag | `v0.27.0` |
| LLamaSharp release commit | `7cbbc45e421d55794d5050d126e0b96511007007` |
| Mapped llama.cpp commit | `3f7c29d318e317b63f54c558bc69803963d7d88c` |
| Intended first application RID | `win-x64` |

The separate upstream research runtime remains:

```text
b9870 / 2d973636e292ee6f75fadcf08d29cb33511f509f
```

It is research evidence, not the runtime loaded by this tool.

## Native-smoke flow

```text
Program
    → NativeBackendSmokeProbe
    → CpuNativeRuntimeConfiguration
    → NativeLibraryConfig.LLama.DryRun(...)
    → NativeBackendSmokeResult
    → JsonEvidenceWriter
```

The report records package/native identity, operating system, architecture,
selected library, AVX level, CUDA/Vulkan flags, logs and controlled operational
failure information.

## VocabOnly model-probe flow

```text
Program
    → parse model/output/cancellation options
    → reject output/model collision
    → capture read-only model snapshot before
    → dry-run matched CPU backend
    → LLamaWeights.LoadFromFileAsync
         VocabOnly = true
         GpuLayerCount = 0
    → collect evidence safe at VocabOnly depth
    → dispose native weights
    → capture model snapshot after
    → apply integrity precedence
    → redact canonical local path
    → write project-owned JSON
```

## Cancellation scopes

```text
--cancel-after-ms <positive integer>
    timer begins after the initial integrity snapshot

--cancel-native-after-ms <positive integer>
    timer begins immediately before LLamaWeights.LoadFromFileAsync
```

The options are mutually exclusive. `Ctrl+C` cancels the outer operation.

## Project-owned safety components

### `ProbeFailureMapper`

Maps file, runtime and model-load exceptions to stable `MI-*` diagnostics. It
never creates a final application model outcome.

### `SensitiveTextRedactor`

Removes the canonical model path from failure messages and native logs. The
filename and canonical-path fingerprint may remain useful evidence.

### `ProbeResultFinalizer`

Applies final precedence:

```text
Succeeded/Cancelled + changed model
    → integrity failure

Succeeded/Cancelled + unverifiable integrity
    → integrity-verification failure

Existing runtime/model failure
    → original cause retained
```

Cancellation cannot conceal a changed or unverifiable model.

### `ChatTemplateEvidenceFactory`

Stores only presence, character length and SHA-256. It never serialises the
full chat template.

### `NativeLoadProgressRecorder`

```text
NaN                     → ignored
negative infinity       → 0
positive infinity       → 1
finite value            → clamped to 0..1
consecutive duplicate   → omitted
```

Only genuine callbacks are recorded; no synthetic percentage is created.

## Verified Granite baseline

```text
Model:                      granite-4.1-3b-Q4_K_M.gguf
Model bytes:                2,099,501,664
Model SHA-256:              662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29
Result:                     PASS
Process exit code:          0
Evidence schema:            1.1
Completion status:          Succeeded
Architecture:               granite
Declared context:           131,072
Embedding size:             2,560
Layer count:                40
Attention heads:            40
KV heads:                   8
Vocabulary count:           100,352
Tokenizer smoke:            PASS / 1 token
Embedded chat template:     present
Native handle closed:       true
Original GGUF preserved:    true
```

The parameter count was unavailable at this safe depth and remains `null`, not
zero or a filename-derived guess.

## Expanded verification architecture

### Tier 1 — model-free hosted CI

Verified workflow result:

```text
Deterministic tests:             170 / 170 passed
Trusted test assembly compile:   passed without model execution
Release win-x64 build:           passed, 0 warnings, 0 errors
Framework-dependent publish:     passed
Direct CPU runtime smoke:        passed
Contained native tests:          4 / 4 passed
No-GGUF artifact scan:           passed
Privacy-gated evidence upload:   passed
```

Tier 1 covers dependency policy, CLI contracts, path and hashing safety,
metadata/template projection, progress normalisation, failure mapping,
redaction, result precedence, evidence contracts and contained native-backend
failures.

### Tier 2 — trusted real-model target machine

Verified local result:

```text
Trusted tests:                   20 / 20 passed
Failed / skipped:                0 / 0
Duration:                        approximately 3 minutes 9 seconds
Model SHA-256 unchanged:         yes
Retained evidence files:         56
Privacy findings:                0
```

The trusted suite verifies:

- exact Granite success and three-run repeatability;
- post-preflight and native-load cancellation;
- malformed GGUF fixtures and deterministic random bytes;
- missing, directory, locked-model and locked-output cases;
- unsafe output-path handling;
- path and chat-template privacy;
- process-owned TCP endpoint observation;
- retained-evidence model-leak prevention.

The future self-hosted service-account workflow remains a workflow/deployment
check after the workflow exists on the default branch. It does not replace the
verified local runtime result.

See:

- [Tier 1 implementation plan](../../docs/superpowers/plans/2026-08-04-llamasharp-tier1-runtime-tests.md)
- [Tier 2 implementation plan](../../docs/superpowers/plans/2026-08-04-llamasharp-tier2-real-model-tests.md)
- [Coverage matrix](../../docs/testing/LLamaSharp-Runtime-Test-Coverage-Matrix.md)
- [Tier 1 evidence](../../docs/testing/evidence/2026-08-04-llamasharp-tier1-verification.md)
- [Tier 2 evidence](../../docs/testing/evidence/2026-08-05-llamasharp-tier2-local-verification.md)
- [Trusted execution runbook](../../docs/testing/runbooks/LLamaSharp-Trusted-Real-Model-Runbook.md)

## Build and deterministic tests

```powershell
$SpikeProject =
    "tools\ModelInspection.LlamaSharpSpike\ModelInspection.LlamaSharpSpike.csproj"

$SpikeTests =
    "tools\ModelInspection.LlamaSharpSpike.Tests\ModelInspection.LlamaSharpSpike.Tests.csproj"

dotnet restore $SpikeTests --runtime win-x64

dotnet test $SpikeTests `
    --configuration Release `
    --no-restore `
    --runtime win-x64 `
    --filter "TestCategory=Deterministic" `
    --minimum-expected-tests 170

dotnet build $SpikeProject `
    --configuration Release `
    --no-restore `
    --runtime win-x64
```

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Requested smoke/probe succeeded and JSON was written |
| `1` | Controlled runtime, probe, integrity or evidence-writing failure |
| `2` | Invalid or unsafe command-line arguments |
| `3` | VocabOnly model probe was cancelled and JSON was written |

## Safety and non-claims

- CPU is the only selected backend.
- CUDA and Vulkan are disabled.
- Models are opened read-only.
- Evidence output may not equal the model path.
- No context, KV cache, inference, save, conversion or quantisation occurs.
- Native/runtime failure is not classified as an invalid model.
- Full local paths, full chat templates, native pointers and handles are not
  serialised.
- The WinUI application still has no LLamaSharp package reference.
- Full CPU execution, Vulkan, TurboQuant and WinUI integration remain later
  gates.