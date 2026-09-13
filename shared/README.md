# Shared project-owned boundaries

**Status:** Gate 1 contracts and Gate 2 bounded transport implemented and tested
**Last reviewed:** 2026-08-06

## Purpose

`shared/` contains framework-neutral projects used by more than one executable. These projects define stable data and transport boundaries without importing WinUI, native model runtimes or process-host policy.

```text
shared/
├── GraniteEdgeAI.ModelInspection.Contracts/
│   ├── Evidence/
│   └── Protocol/
└── GraniteEdgeAI.ModelInspection.Transport/
```

## Boundary rules

Shared projects may contain immutable contracts, protocol identities and limits, deterministic validation, strict JSON, and bounded UTF-8 stream primitives.

They must not contain WinUI/XAML, navigation, product classification policy, process launch/termination, model-file access, LLamaSharp, llama.cpp, OpenVINO, TurboQuant, native handles, pointers or executable entry points.

## Implemented boundaries

### Contracts — Gate 1

- protocol version and exact worker/runtime identities;
- command, message and evidence records;
- compact strict JSON;
- duplicate-property, discriminator, enum and invariant rejection;
- command/message sequence validation;
- completion, cancellation and operational-failure separation.

### Transport — Gate 2

- fixed byte-bounded asynchronous reads;
- strict UTF-8 without replacement decoding;
- rejection of BOM, CR/CRLF, empty frames, partial EOF and oversized frames;
- one-LF serialized writes with explicit flush;
- private payload snapshots so validated queued data cannot be mutated by callers;
- bounded standard-error retention.

Both projects target pure `net8.0`, enable nullable/analyzers/deterministic builds, and treat warnings as errors.

## Evidence and non-claims

Source presence is not proof of a working executable boundary. Hosted verification is recorded under [`docs/testing/evidence/`](../docs/testing/evidence/).

These shared projects do not claim LLamaSharp evidence extraction, application classification, WinUI integration, worker packaging, a real-model application route, OpenVINO, TurboQuant, Hardware Fit or chat.

## Related design

- [ADR-003 — protected Model Inspection worker](../docs/architecture/decisions/ADR-003-protected-model-inspection-worker.md)
- [Model Inspection worker contracts](./GraniteEdgeAI.ModelInspection.Contracts/README.md)
- [Gate 2 verification](../docs/testing/evidence/2026-08-05-model-inspection-worker-gate2-verification.md)
