# Project engineering tools

**Status:** Living source-adjacent documentation  
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
    └── planned by the approved Tier 2 implementation plan
```

## Current LLamaSharp tool chain

- [Model Inspection LLamaSharp feasibility tool](./ModelInspection.LlamaSharpSpike/README.md)
  - [CPU VocabOnly model probe](./ModelInspection.LlamaSharpSpike/ModelProbe/README.md)
- [Deterministic feasibility tests](./ModelInspection.LlamaSharpSpike.Tests/README.md)
- [Shared child-process test support](./ModelInspection.LlamaSharpSpike.TestSupport/README.md)
- [Hosted native integration tests](./ModelInspection.LlamaSharpSpike.NativeIntegrationTests/README.md)

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
    contained CPU-native success
    missing native DLL
    invalid native image
    model-free evidence contract

Tier 2 — manual trusted Windows runner
    controlled Granite success and repeatability
    cancellation
    malformed GGUF matrix
    file-access failures
    privacy and network observations
    original-file integrity
```

Tier 1 source and workflow are implemented. Fresh workflow evidence is required
before the expanded suite is marked verified. Tier 2 remains governed by its
separate implementation plan.

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
