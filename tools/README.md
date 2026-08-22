# Project engineering tools

**Status:** LLamaSharp Tier 1 and local trusted Tier 2 verified  
**Last reviewed:** 2026-08-05  
**Current branch:** `feature/model-inspection`

## Purpose

This folder contains isolated engineering utilities used to prove feasibility,
collect evidence and test native/runtime boundaries without allowing
experimental dependencies to enter the shipped WinUI application prematurely.

## Current hierarchy

```text
tools/
├── README.md
├── ModelInspection.LlamaSharpSpike/
│   └── README.md
├── ModelInspection.LlamaSharpSpike.Tests/
│   └── README.md
├── ModelInspection.LlamaSharpSpike.TestSupport/
│   └── README.md
├── ModelInspection.LlamaSharpSpike.NativeIntegrationTests/
│   └── README.md
└── ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/
    └── README.md
```

## LLamaSharp tool chain

- [Model Inspection LLamaSharp feasibility tool](./ModelInspection.LlamaSharpSpike/README.md)
  - [Production CPU VocabOnly runtime](../runtime/GraniteEdgeAI.ModelInspection.LlamaSharp/ModelProbe/README.md)
- [Deterministic feasibility tests](./ModelInspection.LlamaSharpSpike.Tests/README.md)
- [Shared child-process test support](./ModelInspection.LlamaSharpSpike.TestSupport/README.md)
- [Hosted native integration tests](./ModelInspection.LlamaSharpSpike.NativeIntegrationTests/README.md)
- [Trusted real-model integration tests](./ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/README.md)

The runtime tool supports:

```text
Native CPU backend smoke
    → no model

CPU VocabOnly model probe
    → controlled local GGUF
    → read-only evidence and integrity verification

Diagnostic cancellation scopes
    → whole operation
    → native model-load boundary
```

## Verification tiers

### Tier 1 — normal hosted Windows CI

```text
Deterministic tests:             170 / 170 passed
Release win-x64 build/publish:   passed
Direct CPU smoke:                passed
Contained native tests:          4 / 4 passed
No-GGUF artifact scan:           passed
Privacy-gated evidence upload:   passed
```

Evidence:

- [Tier 1 verification](../docs/testing/evidence/2026-08-04-llamasharp-tier1-verification.md)

### Tier 2 — local trusted target machine

```text
Trusted tests:                   20 / 20 passed
Failed / skipped:                0 / 0
Model SHA-256 unchanged:         yes
Evidence files scanned:          56
Privacy findings:                0
```

The trusted campaign covered the exact Granite success contract, three-run
repeatability, two cancellation scopes, malformed inputs, random bytes,
file-access failures, unsafe output paths, evidence privacy and process-owned
TCP observation.

Evidence:

- [Tier 2 verification](../docs/testing/evidence/2026-08-05-llamasharp-tier2-local-verification.md)
- [Trusted execution runbook](../docs/testing/runbooks/LLamaSharp-Trusted-Real-Model-Runbook.md)

The future self-hosted GitHub Actions service-account run remains a
workflow/deployment check after the manual workflow exists on the default
branch. It does not replace the verified local target-machine result.

## Boundary rules

- A tool project is not an application feature merely because it is in the same
  repository.
- The feasibility CLI owns only command-line parsing and JSON output; the
  production CPU/VocabOnly implementation and exact LLamaSharp packages live
  in `runtime/GraniteEdgeAI.ModelInspection.LlamaSharp`.
- Native/model integration tests execute the feasibility tool as a child
  process; llama.cpp never loads into the MSTest host.
- Destructive DLL tests use disposable copies of published output.
- The LLamaSharp probe opens selected models read-only and rejects evidence
  paths that would overwrite input.
- Tool evidence must not contain full local model paths, full chat-template
  text, model files, native pointers or native handles.
- Artifact upload is fail-closed behind explicit integrity and privacy success.
- The WinUI application must not reference an experimental console tool.
- Production behaviour will use project-owned interfaces such as
  `ILlamaModelProbe`.
- Vulkan and TurboQuant remain later backend-verification gates.

## Coverage register

Known, automated, verified, deferred and out-of-scope scenarios are tracked in:

- [LLamaSharp Runtime Test Coverage Matrix](../docs/testing/LLamaSharp-Runtime-Test-Coverage-Matrix.md)

## Evidence rule

Source presence proves only that a check exists. Claims about native loading,
model recognition, cancellation, disposal, networking or file preservation
require fresh executable output and retained structured evidence.

## Source-of-truth order

```text
1. Tool source and executable tests
2. Nearest README beside the source
3. Approved ADR and design specification
4. Coverage matrix
5. Recorded runtime evidence
6. Historical discussion or exploratory notes
```
