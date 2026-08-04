# Project engineering tools

**Status:** Living source-adjacent documentation  
**Last reviewed:** 2026-08-04  
**Current branch:** `feature/model-inspection`

## Purpose

This folder contains isolated engineering utilities used to prove feasibility,
collect evidence or support development without becoming part of the shipped
WinUI application by accident.

A tool belongs here when it has a different lifecycle from the application and
should be built or executed explicitly.

## Current hierarchy

```text
tools/
├── README.md
├── ModelInspection.LlamaSharpSpike/
│   ├── README.md
│   └── ModelProbe/
│       └── README.md
└── ModelInspection.LlamaSharpSpike.Tests/
    └── README.md
```

## Current tools

- [Model Inspection LLamaSharp feasibility tool](./ModelInspection.LlamaSharpSpike/README.md)
  - [CPU VocabOnly model probe](./ModelInspection.LlamaSharpSpike/ModelProbe/README.md)
  - [Deterministic feasibility tests](./ModelInspection.LlamaSharpSpike.Tests/README.md)

The LLamaSharp tool currently supports two isolated gates:

```text
Native CPU backend smoke
    → no model

CPU VocabOnly model probe
    → one controlled local GGUF
    → read-only evidence and integrity verification
```

Neither gate is referenced by the WinUI application.

## Boundary rules

- A tool project is not an application feature merely because it is in the same
  repository.
- Native or experimental dependencies remain here until safety,
  compatibility and packaging gates pass.
- Tool output distinguishes ignored local artifacts from controlled formal
  evidence.
- The LLamaSharp probe opens the selected model read-only and rejects an
  evidence path that would overwrite it.
- A successful tool experiment does not automatically prove WinUI integration.
- The application project must not reference a console executable project.
  Production behavior will later be extracted behind project-owned interfaces
  such as `ILlamaModelProbe`.
- Vulkan and TurboQuant remain later backend-verification gates rather than
  being added to the CPU lightweight inspection tool prematurely.

## Evidence rule

Source presence proves only that the experiment is implemented. Claims about
native loading, model recognition, progress, cancellation, disposal, memory or
file preservation require fresh command output from the target environment.

## Source-of-truth order

```text
1. Tool source and executable tests
2. Nearest README beside the tool source
3. Approved ADR and design specification
4. Recorded runtime evidence
5. Historical discussion or exploratory notes
```
