# Shared project-owned boundaries

**Status:** Model Inspection contract project shell created; executable protocol behaviour not yet implemented or verified

## Purpose

`shared/` contains small framework-neutral projects that define stable data and protocol boundaries used by more than one executable or application project.

The first shared project is the Model Inspection worker contract assembly. It will provide the versioned language used by the WinUI application and the protected Model Inspection worker without importing either side's implementation details.

## Boundary rules

Shared projects may contain:

- immutable data contracts;
- protocol identifiers and limits;
- deterministic validation with no operating-system side effects;
- JSON contract support that remains independent of a specific process host.

Shared projects must not contain:

- WinUI or XAML types;
- pages, ViewModels, navigation, or application services;
- LLamaSharp, llama.cpp, TurboQuant, Vulkan, or OpenVINO dependencies;
- worker-process launch code;
- native handles, pointers, or model bytes;
- executable entry points.

## Current hierarchy

```text
shared/
└── GraniteEdgeAI.ModelInspection.Contracts/
    └── framework-neutral Model Inspection worker contracts
```

## Evidence rule

A project or source file being present does not prove that the protocol, worker, packaging, or real-model integration works. Verified claims require executable test/build evidence recorded under `docs/testing/evidence/`.

## Related design

- `docs/superpowers/specs/2026-08-05-model-inspection-worker-integration-design.md`
- `docs/superpowers/plans/2026-08-05-model-inspection-worker-gate-1-contracts-protocol.md`
