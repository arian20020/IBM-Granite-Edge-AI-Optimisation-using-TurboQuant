# Model Inspection worker contracts

**Status:** Protocol identity, immutable command/message/evidence records, invariant validation, strict bounded JSON, and deterministic command/message sequence validation are implemented and verified; worker host, process adapter, packaging and runtime integration remain deferred to later gates

## Purpose

This pure `net8.0` project defines the framework-neutral language shared by:

```text
GraniteEdgeAI WinUI application
        ↕ versioned bounded protocol contracts
GraniteEdgeAI.ModelInspection.Worker
```

It performs no model inspection, opens no model file and starts no process.

## Owned responsibilities

- protocol version and stable worker/runtime identity;
- immutable command, message and technical-evidence records;
- deterministic property-oriented invariant validation;
- compact UTF-8 JSON serialization;
- strict bounded command/message deserialization;
- deterministic command and message sequence validation.

## Forbidden responsibilities

- WinUI, XAML, pages, ViewModels or navigation;
- LLamaSharp, llama.cpp, TurboQuant or GPU backends;
- process creation, stream ownership, timeouts or process termination;
- model file access, hashing or evidence collection;
- user-facing outcome classification;
- application service implementation.

## Dependency boundary

The project:

- targets `net8.0`;
- treats warnings as errors;
- uses only .NET framework libraries;
- references no WinUI or native-runtime package;
- is safe for both the application and worker to reference without coupling either implementation to the other.

## Verified protocol identity and limits

```text
Protocol version:                  1
Worker ID:                         GraniteEdgeAI.ModelInspection.Worker
Runtime profile:                   llamasharp-0.27.0-cpu-win-x64-vocab-only-v1
Maximum command/message:           1 MiB of UTF-8
Maximum retained stderr:           256 KiB of UTF-8
Startup handshake timeout:         5 seconds
Overall lightweight inspection:    5 minutes
Cooperative-cancellation grace:    5 seconds
```

The timeout and retained-stderr constants are contracts for later process integration. This project does not yet own process lifecycle or stderr collection.

## Record invariants

The records separate worker execution from model classification:

```text
Completed
    reliable technical evidence exists
    application classifier decides the model outcome

Cancelled
    cooperative cancellation completed
    no model evidence or outcome exists

OperationalFailure
    reliable classification evidence could not be produced
    no model outcome exists
```

Validation also requires:

- exact protocol/message/command kinds;
- non-empty request IDs;
- positive process and file identities where required;
- UTC timestamps;
- one fully qualified model path only in the start command;
- a successful GGUF quick-scan snapshot;
- five truthful progress stages and finite fractions in `0..1`;
- path-minimised output evidence;
- no complete chat-template text;
- no unavailable metadata guessed from filenames.

Validation messages identify the invalid property but do not echo supplied model paths or other values.

## Strict JSON rules

`WorkerProtocolJson` is the only approved JSON mapper for these records.

It:

- accepts one compact UTF-8 JSON object per payload;
- rejects empty payloads and payloads above 1 MiB before parsing;
- rejects malformed UTF-8 rather than replacing invalid bytes;
- rejects comments, trailing commas and roots that are not objects;
- limits JSON depth to 32;
- recursively rejects duplicate property names in every nested object;
- requires the exact camel-case `protocolVersion`, `commandType` or `messageType` properties;
- accepts only the approved command/message discriminator values;
- writes enum values as camel-case strings and disallows numeric enum values;
- uses case-sensitive property matching;
- permits unknown additive properties within protocol version 1;
- calls each record's invariant validator after deserialization and before serialization;
- emits compact UTF-8 without indentation or a line terminator;
- rejects serialized output above the same 1 MiB ceiling;
- returns only approved worker command or message record types.

Later process-stream readers must stop reading and fail before buffering more than the same 1 MiB limit. Deserialization is already bounded, but this contract project does not own stream I/O.

## Sequence rules

`WorkerCommandSequenceValidator` enforces:

```text
one StartInspection command
    ↓
zero or more matching CancelInspection commands
    ↓
one terminal mark
```

- cancellation before start is rejected;
- a second start is rejected;
- repeated matching cancellation is idempotent;
- a mismatched request ID is rejected;
- no command is accepted after terminal state.

`WorkerMessageSequenceValidator` enforces:

```text
one Hello
    ↓
one application-supplied expected request ID
    ↓
one Started
    ↓
zero or more monotonic Progress messages
    ↓
one Completed
```

- request-scoped output before the request identity is established is rejected;
- wrong request IDs are rejected;
- stage and completed-count progress cannot move backwards;
- completion may occur after Started even when no progress was emitted;
- duplicate terminal messages and all output after terminal state are rejected.

These validators model protocol order only. They do not read streams, start processes, manage timeouts or inspect files.

## Testing and evidence

The adjacent pure contract-test project currently covers:

- assembly dependency isolation;
- protocol identity and time/size limits;
- record invariants and completion-status exclusivity;
- nullable unavailable evidence;
- chat-template data minimisation;
- valid additive JSON fields;
- malformed UTF-8;
- comment and trailing-comma rejection;
- non-object roots;
- missing, wrongly cased and unknown discriminators;
- invalid versions and enums;
- duplicate nested properties;
- compact command/message round trips;
- pre-serialization validation and output-size enforcement;
- command ordering, matching request identity and idempotent cancellation;
- hello/request/started ordering;
- monotonic progress;
- terminal uniqueness and post-terminal rejection.

Verified implementation checkpoints:

```text
Strict JSON:
Commit:  4c01903692fa0d446ff5764920d5f0bf36599eb5
Run:     31009200037
Result:  contract tests, WinUI build and packaged application tests passed

Sequence validation:
Commit:  047fa2eb135009022ef560aa9cfae644c8f4a5f4
Run:     31011809050
Result:  contract tests, WinUI build and packaged application tests passed
```

## Deferred work

This project does not yet provide:

- stream framing or bounded stream readers;
- a worker executable;
- process crash, timeout or cancellation containment;
- LLamaSharp evidence collection;
- x64 publish/MSIX packaging;
- application classification, services, ViewModels or UI wiring.

## Non-claims

The implemented contracts do not prove that a production worker exists, that LLamaSharp is connected, that a model can be inspected through the application, or that the worker is packaged. Those claims require later executable gates.

## Related documentation

- `../../docs/superpowers/specs/2026-08-05-model-inspection-worker-integration-design.md`
- `../../docs/superpowers/plans/2026-08-05-model-inspection-worker-gate-1-contracts-protocol.md`
