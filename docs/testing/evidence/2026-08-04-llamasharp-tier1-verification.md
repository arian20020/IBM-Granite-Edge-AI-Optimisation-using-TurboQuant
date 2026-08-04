# LLamaSharp Tier 1 Runtime Verification

**Evidence ID:** MI-LLAMASHARP-TIER1-2026-08-04  
**Date:** 2026-08-04  
**Branch:** `feature/model-inspection`  
**Pull request:** Draft PR `#44`  
**Workflow:** `LLamaSharp Tier 1 runtime tests`  
**Successful workflow run:** `30939159409`  
**Successful job:** `92092825645`

## Purpose

This document records fresh execution evidence for the model-free LLamaSharp
runtime test tier. It separates verified results from source-presence claims and
from the trusted real-model tier that has not yet been executed.

## Runtime boundary under test

```text
LLamaSharp 0.27.0
        ↓
LLamaSharp.Backend.Cpu 0.27.0
        ↓
llama.cpp 3f7c29d318e317b63f54c558bc69803963d7d88c
```

The Tier 1 boundary deliberately keeps:

```text
VocabOnly                  true
GPU layer count            0
CUDA                       disabled
Vulkan                     disabled
External GGUF model        not required
Context / KV cache         not created
Inference                  not executed
TurboQuant                 not loaded
```

The separate upstream `b9870` build remains research evidence and is not the
runtime exercised by this workflow.

## Verified results

### Deterministic contracts

```text
Test category:             Deterministic
Total tests:               170
Succeeded:                 170
Failed:                    0
Skipped:                   0
Result:                    PASS
```

The deterministic suite verifies dependency policy, command-line parsing, path
safety, read-only hashing, cancellation during hashing, metadata projection,
chat-template evidence, progress normalization, diagnostic mapping, path
redaction, result precedence, atomic JSON writing, evidence-type isolation and
bounded child-process support.

### Trusted-suite compile gate

```text
Project:                   GraniteEdgeAI.LlamaSharp.RealModelTests
Configuration:             Release / win-x64
Execution of model tests:  no
Compilation:               PASS
Analyzer result:           PASS
```

This proves the trusted real-model test project and its support code compile
before the self-hosted runner or controlled Granite model is used. It does not
prove any real-model scenario passes.

### Feasibility executable

```text
Configuration:             Release / win-x64
Build warnings:            0
Build errors:              0
Build:                     PASS
Framework-dependent publish: PASS
```

### Direct CPU native smoke

```text
Managed package:           LLamaSharp 0.27.0
Backend package:           LLamaSharp.Backend.Cpu 0.27.0
Mapped llama.cpp commit:   3f7c29d318e317b63f54c558bc69803963d7d88c
Process architecture:      X64
CUDA selected:             false
Vulkan selected:           false
Result:                    PASS
Evidence schema:           1.0
```

### Contained native integration

```text
Test category:             NativeIntegration
Total tests:               4
Succeeded:                 4
Failed:                    0
Skipped:                   0
Result:                    PASS
```

The four child-process tests cover the published CPU backend, missing native
libraries, a corrupted native image and the model-free runtime evidence/privacy
contract. Native entry points execute outside the MSTest host so an unmanaged
abort cannot terminate the complete test run.

### Artifact privacy gate

```text
GGUF files under retained artifact root: 0
Privacy scan:                         PASS
Upload condition:                     scan outcome must equal success
Uploaded evidence:                    model-free runtime-smoke JSON only
Result:                               PASS
```

The workflow upload step cannot execute after a failed artifact scan. The
trusted workflow uses the same fail-closed principle and additionally requires
the final controlled-model integrity gate to pass.

## Failure-driven corrections made before the green run

The verification loop preserved each first failure and changed the smallest
responsible contract:

1. MSTest 4 asynchronous exception assertions were updated to the supported
   `ThrowsAsync` / `ThrowsExactlyAsync` APIs.
2. File-system tests stopped over-specifying whether Windows reports a failed
   atomic move as `IOException` or `UnauthorizedAccessException`; both represent
   the required controlled write failure.
3. Cancellation tests accept `TaskCanceledException` as a valid derived
   `OperationCanceledException`.
4. The Unicode argument test uses a temporary PowerShell `-File` script instead
   of allowing `-Command` to reinterpret the final argument as command text.
5. Native and trusted test assembly names were shortened after Windows rejected
   a generated executable path longer than the supported process-start path.
6. The trusted compile gate found and corrected namespace shadowing of
   `System.IO.FileAccess` and one expected/actual assertion-role error before the
   controlled model was touched.

## Security controls verified in source and workflow

- exact managed/native dependency versions;
- no LLamaSharp or TurboQuant dependency in the WinUI application project;
- no CUDA, Vulkan or TurboQuant dependency in the CPU feasibility tool;
- model-free Tier 1 execution;
- bounded child-process timeouts and process-tree termination;
- canonical-path redaction from evidence and logs;
- no full chat-template serialization;
- atomic evidence replacement;
- no `.gguf` artifact upload;
- evidence upload gated on successful privacy scanning.

## Not verified by this evidence

Tier 1 does not prove:

- controlled Granite success under the expanded real-model suite;
- operation-scoped or native-load-scoped real-model cancellation;
- malformed GGUF containment against every committed fixture;
- locked controlled-model and locked-output behaviour;
- process-owned TCP endpoint observations during a real-model probe;
- the WinUI Cancel button;
- full CPU tensor allocation, context creation or generation;
- Hardware Fit;
- Vulkan;
- TurboQuant.

Those items belong to the manual trusted real-model workflow or later backend
and application-integration gates.

## Next verification gate

Run `.github/workflows/llamasharp-real-model-integration.yml` manually on the
trusted runner labels:

```text
self-hosted
Windows
X64
workbook05
intel-target
```

The repository variable `GRANITE_TEST_MODEL_PATH` must point to the exact
controlled model from the runner service account's perspective:

```text
Filename:     granite-4.1-3b-Q4_K_M.gguf
Length:       2,099,501,664 bytes
SHA-256:      662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29
```

The model must remain outside the checked-out repository and must not be copied
into uploaded evidence.
