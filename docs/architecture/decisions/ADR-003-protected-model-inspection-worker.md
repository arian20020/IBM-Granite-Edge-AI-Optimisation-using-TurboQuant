# ADR-003: Protect Model Inspection native work in a dedicated worker process

- **Status:** Accepted
- **Date:** 2026-08-05
- **Owner:** Granite Edge AI Model Inspection
- **Affected features:** Model Import, Onboarding, Model Inspection, packaging, diagnostics
- **Related decisions:** [ADR-001](ADR-001-llamasharp-application-runtime.md), [ADR-002](ADR-002-core-inspection-versus-backend-verification.md)
- **Related specification:** [`2026-08-05-model-inspection-worker-integration-design.md`](../../superpowers/specs/2026-08-05-model-inspection-worker-integration-design.md)

## Context

Model Inspection must examine untrusted local GGUF files with LLamaSharp and its matched native llama.cpp backend. Feasibility work verified the selected CPU runtime and a lightweight Granite `VocabOnly` probe, but it also observed that a native llama.cpp failure can terminate a process before ordinary managed exception handling can recover.

Loading that native runtime directly inside the WinUI process would therefore allow one malformed, unsupported, or runtime-triggering model to terminate the complete desktop application. It would also mix page lifecycle, native dependency loading, file inspection, cancellation, evidence collection, and user-facing classification inside one failure domain.

The application also has explicit privacy and deployment requirements:

- local/offline operation;
- no listening HTTP port;
- no model path on the command line;
- bounded diagnostic output;
- exact runtime identity;
- no native handles or LLamaSharp types crossing into the UI;
- operational failures kept separate from model outcomes.

## Decision

Model Inspection native work runs in a dedicated short-lived worker process.

The WinUI application communicates over bounded JSON lines on redirected standard streams.

The worker returns technical evidence; application code classifies outcomes.

Chat later uses a separate pinned `llama-cli` process.

The first production boundary is explicitly `win-x64` and uses:

```text
GraniteEdgeAI.exe
    ↓ application request/result domain
ModelInspectionViewModel
    ↓
IModelInspectionService
    ↓
ILlamaModelProbe
    ↓
WorkerProcessLlamaModelProbe
    ↓ bounded protocol v1
GraniteEdgeAI.ModelInspection.Worker.exe
    ↓
LLamaSharp 0.27.0
LLamaSharp.Backend.Cpu 0.27.0
matched llama.cpp CPU runtime
```

## Protocol and dependency boundaries

### Process lifecycle

- one short-lived worker per inspection request;
- one `hello` handshake before request-scoped output;
- one start command and one terminal result;
- cooperative cancellation first;
- a five-second cooperative-cancellation grace contract;
- forced termination only as fallback cleanup;
- forced termination maps to operational failure because terminal evidence integrity is unavailable;
- parent and worker identities are checked by later process-host gates.

### Transport

- redirected standard input and output;
- bounded, continuously drained standard error;
- compact UTF-8 JSON, one object per line;
- protocol version exactly `1`;
- maximum command/message exactly `1 MiB` of UTF-8;
- maximum retained stderr exactly `256 KiB` of UTF-8;
- startup timeout contract exactly five seconds;
- overall lightweight inspection timeout contract exactly five minutes;
- unknown additive fields tolerated within protocol version 1;
- duplicate properties, malformed UTF-8, invalid discriminators, and invalid sequence transitions rejected.

### Trust boundary

- worker executable is resolved from a fixed build/package location;
- no `PATH` search;
- model path is sent in the bounded start command, never on the command line;
- output evidence contains no canonical path, model bytes, complete chat-template text, environment variables, native pointers, or handles;
- stderr is bounded and redacted before retention;
- no worker observation is silently ignored by later mappers/classifiers.

### Application versus worker domains

The application owns:

- `ModelInspectionRequest`;
- progress and execution state;
- findings and classified outcomes;
- Hardware Fit continuation rules;
- user-facing messages.

The shared worker contract owns:

- commands, messages, runtime identity, and raw technical evidence;
- strict bounded JSON;
- command/message sequence validation.

Only `WorkerRequestMapper` and `WorkerResultMapper` may bridge these domains. WinUI pages and controls do not reference worker protocol records.

## Why this option was selected

### Failure containment

A native crash ends the worker rather than the complete WinUI application. The application can report an operational failure and clean up the child process without inventing a model classification.

### Clear responsibility and testability

The process adapter can be tested with a deterministic fixture that simulates hello, progress, malformed output, hangs, cancellation, crash, and timeout behavior without requiring LLamaSharp in every test.

### Port-free local execution

Redirected standard streams preserve the offline/no-listening-port requirement and avoid an unnecessary local server lifecycle.

### Evidence-first classification

The worker focuses on technical facts. The application classifier can combine worker evidence with the validated quick-scan snapshot and apply explicit outcome precedence without embedding product policy in native infrastructure.

### Independent chat route

Inspection is a bounded evidence-gathering task. Chat is a long-running token-generation task. Keeping `llama-cli` separate prevents their lifecycle, protocol, packaging, and failure assumptions from becoming coupled.

## Alternatives considered

### Load LLamaSharp directly in the WinUI process

**Rejected.** It is simpler initially, but a native abort can terminate the entire application. It also makes containment, crash testing, cancellation, and native dependency isolation substantially weaker.

### Run a local HTTP service

**Rejected.** It introduces a listening port, server lifecycle, endpoint protection, port conflicts, and a larger attack surface without a requirement for remote access.

### Use `llama-cli` for Model Inspection

**Rejected.** CLI text output is not a stable, typed, versioned evidence contract. It would make structured validation and model-outcome classification dependent on human-oriented output formatting.

### Use one process route for inspection and chat

**Rejected.** The tasks have different lifetimes, outputs, cancellation semantics, performance needs, and evidence requirements. Shared lifecycle would increase coupling and obscure failures.

### Implement a custom GGUF/runtime parser only

**Rejected as the complete solution.** The existing bounded quick scan is useful for early validation, but it cannot prove that the selected production runtime can recognise the model, tokenizer, chat setup, and structural metadata.

## Consequences

### Benefits

- native failures are isolated from WinUI;
- application/runtime dependency direction remains explicit;
- protocol and process lifecycle can be tested independently;
- no HTTP port is required;
- output and diagnostics are bounded;
- application classification remains deterministic and auditable;
- chat remains independently evolvable.

### Costs and trade-offs

- an additional executable must be built, packaged, located, and verified;
- protocol compatibility becomes an explicit maintenance responsibility;
- process startup adds overhead;
- cancellation and cleanup need careful state handling;
- build, publish, and MSIX outputs must carry matched managed/native dependencies;
- x86 and ARM64 are not supported by the first production worker boundary.

## x64-only first boundary

Gate 1 records the identity `llamasharp-0.27.0-cpu-win-x64-vocab-only-v1`. Later worker and packaging gates must fail clearly on unsupported process architecture rather than silently selecting an unverified runtime.

Expanding to x86, ARM64, Vulkan, or another native backend requires separate dependency, packaging, runtime, and trusted-model evidence.

## Chat separation

The planned chat route is:

```text
Chat service
    ↓
pinned llama-cli.exe
```

It does not call the Model Inspection worker and does not reuse the inspection JSON protocol. No chat implementation claim is made by this decision.

## Review triggers

Review or supersede this ADR when:

- LLamaSharp or its mapped llama.cpp revision changes;
- worker protocol version changes;
- the worker must support another architecture or backend;
- packaging changes from the current Windows/MSIX direction;
- the application requires more than one request per worker;
- cancellation or integrity requirements change;
- an alternative sandbox/process-containment mechanism becomes available;
- chat architecture changes in a way that could justify a shared runtime host;
- evidence shows the worker boundary cannot meet latency or reliability requirements.

## Non-claims

This accepted decision and Gate 1 contracts do not prove that:

- the worker executable has been implemented;
- a process is launched or contained in production;
- LLamaSharp evidence is extracted by the worker;
- worker/app mappers, classifier, service, or ViewModel exist;
- live WinUI progress or cancellation works;
- the worker is present in build, publish, or MSIX output;
- a real model passes the production application route;
- Vulkan, TurboQuant, Hardware Fit, or chat is implemented.

Those claims require later executable gates and recorded evidence.
