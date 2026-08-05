# Model Inspection worker contracts

**Status:** Project shell only; protocol identity, messages, validators, worker host, process adapter, packaging and runtime integration are not yet implemented

## Purpose

This project defines the framework-neutral data and protocol language shared by:

```text
GraniteEdgeAI WinUI application
        ↕ versioned bounded protocol contracts
GraniteEdgeAI.ModelInspection.Worker
```

It performs no model inspection and starts no process.

## Allowed responsibilities

- protocol version and stable worker/runtime identity;
- command, message and evidence records;
- deterministic record validation;
- compact bounded JSON serialization rules;
- command/message sequence validation.

## Forbidden responsibilities

- WinUI, XAML, pages, ViewModels or navigation;
- LLamaSharp, llama.cpp, TurboQuant or GPU backends;
- process creation, stream ownership, timeouts or process termination;
- model file access, hashing or evidence collection;
- user-facing outcome classification;
- application service implementation.

## Dependency rule

The project targets `net8.0`, treats warnings as errors and uses only .NET framework libraries. It must remain safe to reference from both the application and worker without introducing either implementation into the other.

## Planned protocol identity

```text
Protocol version: 1
Worker ID: GraniteEdgeAI.ModelInspection.Worker
Runtime profile: llamasharp-0.27.0-cpu-win-x64-vocab-only-v1
```

## Testing

The adjacent pure contract-test project verifies the assembly dependency graph and will later verify protocol records, bounded JSON and state-machine rules.

## Non-claims

This README does not claim that a worker executable exists, that LLamaSharp is connected, that a model can be inspected, or that package deployment is complete.

## Related documentation

- `../../docs/superpowers/specs/2026-08-05-model-inspection-worker-integration-design.md`
- `../../docs/superpowers/plans/2026-08-05-model-inspection-worker-gate-1-contracts-protocol.md`
