# Shared project-owned boundaries

**Status:** Framework-neutral Model Inspection worker contracts implemented and tested; no worker executable is present yet  
**Last reviewed:** 2026-08-05

## Purpose

`shared/` contains small framework-neutral projects that define stable data and protocol boundaries used by more than one executable or application project.

The first shared project is the Model Inspection worker contract assembly. It provides the versioned language used later by the WinUI application and protected worker without importing either side's implementation details.

## Boundary rules

Shared projects may contain:

- immutable data contracts;
- protocol identities, discriminators, and limits;
- deterministic invariant and sequence validation;
- strict bounded JSON support independent of a process host.

Shared projects must not contain:

- WinUI/XAML types, pages, controls, or ViewModels;
- application navigation, services, or classification policy;
- LLamaSharp, llama.cpp, TurboQuant, Vulkan, or OpenVINO dependencies;
- worker-process launch, stream ownership, timeout, or kill logic;
- model file access, hashing, evidence collection, native handles, pointers, or model bytes;
- executable entry points.

## Current hierarchy

```text
shared/
└── GraniteEdgeAI.ModelInspection.Contracts/
    ├── Evidence/
    └── Protocol/
```

The shared contract project targets pure `net8.0`, treats warnings as errors, and is covered by an adjacent framework-neutral contract-test project.

## Implemented Gate 1 boundary

- protocol version and exact worker/runtime identity;
- bounded command, message, and evidence records;
- compact UTF-8 JSON;
- one-megabyte message ceiling;
- malformed UTF-8, duplicate-property, discriminator, enum, and invariant rejection;
- command and message sequence validators;
- connection-scoped hello and request-scoped identity rules;
- completion/cancellation/operational-failure separation.

## Evidence rule

A project or source file being present does not prove that a worker, package, or runtime integration works. Verified claims require executable test/build evidence under `docs/testing/evidence/`.

## Non-claims

The shared contracts do not prove:

- process launch or containment;
- LLamaSharp evidence extraction;
- worker packaging;
- application mapping, classification, service, ViewModel, or UI behavior;
- real-model inspection through the production app.

## Related design

- [ADR-003 — protected Model Inspection worker](../docs/architecture/decisions/ADR-003-protected-model-inspection-worker.md)
- [Worker integration specification](../docs/superpowers/specs/2026-08-05-model-inspection-worker-integration-design.md)
- [Gate 1 implementation plan](../docs/superpowers/plans/2026-08-05-model-inspection-worker-gate-1-contracts-protocol.md)
- [Model Inspection worker contracts](./GraniteEdgeAI.ModelInspection.Contracts/README.md)
