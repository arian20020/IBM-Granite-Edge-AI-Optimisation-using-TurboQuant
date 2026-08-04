# Project engineering tools

**Status:** Tier 1 LLamaSharp runtime boundary verified; trusted Tier 2 execution pending  
**Last reviewed:** 2026-08-04  
**Current branch:** `feature/model-inspection`

## Purpose

This folder contains isolated engineering utilities used to prove feasibility,
collect evidence, and test native/runtime boundaries without allowing
experimental dependencies to enter the shipped WinUI application prematurely.

## Current hierarchy

```text
tools/
├── README.md
├── ModelInspection.LlamaSharpSpike/
│   ├── README.md
│   └── ModelProbe/README.md
├── ModelInspection.LlamaSharpSpike.Tests/
│   └── README.md
├── ModelInspection.LlamaSharpSpike.TestSupport/
│   └── README.md
├── ModelInspection.LlamaSharpSpike.NativeIntegrationTests/
│   └── README.md
└── ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/
    └── README.md
```

## Current LLamaSharp tool chain

- [Model Inspection LLamaSharp feasibility tool](./ModelInspection.LlamaSharpSpike/README.md)
  - [CPU VocabOnly model probe](./ModelInspection.LlamaSharpSpike/ModelProbe/README.md)
- [Deterministic feasibility tests](./ModelInspection.LlamaSharpSpike.Tests/README.md)
- [Shared child-process test support](./ModelInspection.LlamaSharpSpike.TestSupport/README.md)
- [Hosted native integration tests](./ModelInspection.LlamaSharpSpike.NativeIntegrationTests/README.md)
- [Trusted real-model integration tests](./ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/README.md)

The runtime tool supports:

```text
Native CPU backend smoke
    → no model

CPU VocabOnly model probe
    → one controlled local GGUF
    → read-only evidence and integrity verification

Diagnostic cancellation scopes
    → whole operation
    → native model-load boundary
```

## Test tiers

```text
Tier 1 — every relevant push and pull request
    deterministic contracts
    Release win-x64 build/publish
    trusted-suite model-free compile gate
    contained CPU-native success
    missing native DLL
    invalid native image
    model-free evidence contract
    no-GGUF artifact scan

Tier 2 — manual trusted Windows runner
    controlled Granite success and repeatability
    cancellation
    malformed GGUF matrix
    file-access failures
    privacy and network observations
    original-file integrity
```

### Tier 1 verified result

Fresh hosted workflow run `30939159409` produced:

```text
Deterministic tests:             170 / 170 passed
Trusted real-model assembly:     compiled with analyzers, not executed
Release win-x64 build/publish:   passed
Direct CPU smoke:                passed
Contained native tests:          4 / 4 passed
No-GGUF artifact scan:           passed
Privacy-gated evidence upload:   passed
```

The formal record is:

- [LLamaSharp Tier 1 Runtime Verification](../docs/testing/evidence/2026-08-04-llamasharp-tier1-verification.md)

Tier 2 source is compile-ready, but no expanded real-model, cancellation,
malformed-input, file-access, privacy, or network scenario is marked verified
until the manual trusted workflow executes and its evidence is reviewed.

## Boundary rules

- A tool project is not an application feature merely because it is in the same
  repository.
- Native or experimental dependencies remain here until safety,
  compatibility, and packaging gates pass.
- Native/model integration tests execute the feasibility tool as a child
  process; llama.cpp never loads into the MSTest host.
- Destructive DLL tests use disposable copies of published output.
- The LLamaSharp probe opens selected models read-only and rejects an evidence
  path that would overwrite the input.
- Tool evidence must not contain full local model paths, full chat-template
  text, model files, native pointers, or native handles.
- Artifact upload is fail-closed: required integrity/privacy scans must have an
  explicit successful outcome.
- The application project must not reference a console tool project.
  Production behavior will later be extracted behind project-owned interfaces
  such as `ILlamaModelProbe`.
- Vulkan and TurboQuant remain later backend-verification gates.

## Coverage register

Known, automated, pending, deferred, and out-of-scope scenarios are tracked in:

- [LLamaSharp Runtime Test Coverage Matrix](../docs/testing/LLamaSharp-Runtime-Test-Coverage-Matrix.md)

## Evidence rule

Source presence proves only that a check exists. Claims about native loading,
model recognition, cancellation, disposal, memory, networking, or file
preservation require fresh command/workflow output and retained structured
evidence.

## Source-of-truth order

```text
1. Tool source and executable tests
2. Nearest README beside the tool source
3. Approved ADR and design specification
4. Coverage matrix
5. Recorded runtime evidence
6. Historical discussion or exploratory notes
```
