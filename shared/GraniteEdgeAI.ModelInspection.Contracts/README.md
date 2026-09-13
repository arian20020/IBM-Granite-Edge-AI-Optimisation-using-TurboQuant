# Model Inspection worker contracts

**Status:** Protocol identity, immutable command/message/evidence records, invariant validation, strict bounded JSON, and deterministic sequence validation implemented and tested; worker host and runtime integration remain later gates
**Last reviewed:** 2026-08-05

## Purpose

This pure `net8.0` project defines the framework-neutral language shared by the future application process adapter and protected worker.

```text
GraniteEdgeAI WinUI application
        ↕ versioned bounded protocol contracts
GraniteEdgeAI.ModelInspection.Worker
```

It performs no model inspection, opens no model file, and starts no process.

## Owned responsibilities

- protocol version and stable worker/runtime identity;
- immutable commands, messages, and technical-evidence records;
- deterministic property invariant validation;
- compact UTF-8 JSON serialization;
- strict bounded deserialization;
- command and message sequence validation.

## Forbidden responsibilities

- WinUI, XAML, pages, ViewModels, or navigation;
- application model-outcome classification;
- LLamaSharp, llama.cpp, TurboQuant, or GPU backends;
- process creation, stream ownership, timeouts, or process termination;
- file access, hashing, or evidence collection;
- worker implementation or packaging.

## Dependency boundary

The project:

- targets `net8.0`;
- treats warnings as errors;
- uses only .NET framework libraries;
- references no WinUI or native-runtime package;
- can be referenced by both sides without coupling their implementations.

## Protocol identity and limits

```text
Protocol version:                  1
Worker ID:                         GraniteEdgeAI.ModelInspection.Worker
Runtime profile:                   llamasharp-0.27.0-cpu-win-x64-vocab-only-v1
Maximum command/message:           1 MiB UTF-8
Maximum retained stderr:           256 KiB UTF-8
Startup handshake timeout:         5 seconds
Overall lightweight inspection:    5 minutes
Cooperative-cancellation grace:    5 seconds
```

Timeout and stderr constants are contracts for the later process host. This assembly does not implement those mechanisms.

## Execution semantics

```text
Completed
    → reliable technical evidence exists
    → application code decides the model outcome

Cancelled
    → cooperative cancellation completed
    → no model result or operational failure exists

OperationalFailure
    → trustworthy classification evidence could not be produced
    → no model outcome is invented
```

Forced process termination cannot be represented as successful cancellation.

## Data minimisation

Worker output contracts contain no:

- canonical model path;
- model bytes;
- full chat-template text;
- prompt or health data;
- environment variables;
- native pointers or handles.

Unavailable values remain nullable rather than being guessed from filenames.

## Strict JSON rules

`WorkerProtocolJson`:

- accepts one UTF-8 JSON object per payload;
- rejects empty or over-one-megabyte payloads before parsing;
- rejects malformed UTF-8, comments, trailing commas, and non-object roots;
- limits JSON depth;
- rejects duplicate property names recursively;
- requires exact camel-case protocol discriminators;
- writes camel-case string enums and rejects numeric enums;
- uses case-sensitive property matching;
- tolerates unknown additive fields within protocol version 1;
- validates records after deserialization and before serialization;
- emits compact UTF-8 without indentation or line terminator.

Later stream readers must enforce the same limit before unbounded buffering.

## Sequence rules

`WorkerCommandSequenceValidator` enforces:

```text
one StartInspection
    ↓
zero or more matching CancelInspection commands
    ↓
one terminal mark
```

`WorkerMessageSequenceValidator` enforces:

```text
one Hello
    ↓
application establishes expected request ID
    ↓
one Started
    ↓
zero or more monotonic Progress messages
    ↓
one Completed
```

Wrong request IDs, backward progress, duplicate terminals, and post-terminal traffic are rejected.

## Testing

The pure contract-test project covers:

- dependency isolation;
- protocol identity and limits;
- record invariants and completion exclusivity;
- nullable unavailable evidence and data minimisation;
- compact command/message round trips;
- malformed UTF-8, duplicate properties, invalid versions/discriminators/enums;
- unknown additive fields;
- pre-serialization validation and output-size enforcement;
- command and message ordering, monotonic progress, and terminal uniqueness;
- hosted CI inclusion.

## Gate relationship

Gate 1 establishes this transport language and the application-owned request/result language. [ADR-003](../../docs/architecture/decisions/ADR-003-protected-model-inspection-worker.md) accepts the dedicated worker architecture.

Gate 2 must implement the executable/process boundary and prove bounded streams, handshake, crash/hang/timeout containment, cooperative cancellation, forced cleanup, and trusted executable resolution.

## Non-claims

The contracts do not prove that:

- a worker executable exists;
- any process is launched or contained;
- LLamaSharp is invoked;
- evidence is mapped or classified by the application;
- the worker is packaged;
- a real model is inspected through the production route.

## Related documentation

- [Shared boundaries](../README.md)
- [ADR-003](../../docs/architecture/decisions/ADR-003-protected-model-inspection-worker.md)
