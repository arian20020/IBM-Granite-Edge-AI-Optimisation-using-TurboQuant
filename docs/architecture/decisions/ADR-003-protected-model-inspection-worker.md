# ADR-003: Protect Model Inspection native work in a dedicated worker process

- **Status:** Accepted; Gate 2 worker boundary implemented and verified
- **Date:** 2026-08-05
- **Last reconciled:** 2026-08-06
- **Owner:** Granite Edge AI Model Inspection
- **Affected areas:** Model Inspection infrastructure, packaging, diagnostics, future application integration
- **Related decisions:** [ADR-001](ADR-001-llamasharp-application-runtime.md), [ADR-002](ADR-002-core-inspection-versus-backend-verification.md)
- **Related specification:** [`2026-08-05-model-inspection-worker-integration-design.md`](../../superpowers/specs/2026-08-05-model-inspection-worker-integration-design.md)
- **Verification record:** [`2026-08-05-model-inspection-worker-gate2-verification.md`](../../testing/evidence/2026-08-05-model-inspection-worker-gate2-verification.md)

## Context

Model Inspection will eventually examine untrusted local GGUF files with LLamaSharp and its matched native llama.cpp backend. A native runtime failure can terminate its hosting process before ordinary managed exception handling can recover. Loading that runtime inside WinUI would therefore place the complete desktop application, page lifecycle, native loading, file inspection, cancellation and evidence collection inside one failure domain.

The application also requires local/offline execution, no listening HTTP port, no model path on a command line, bounded diagnostics, exact runtime identity, and no native handles or LLamaSharp types crossing into WinUI.

## Decision

Native inspection work runs in one short-lived `win-x64` worker process per request. The application and worker communicate with strict bounded UTF-8 JSON lines over redirected standard input and output. Standard error is drained independently and retained only within a byte limit.

Gate 2 implements the process and protocol boundary, but deliberately installs an `UnavailableWorkerInspectionEngine`. The production worker therefore returns an honest controlled operational failure rather than opening a model or fabricating evidence.

```text
Future application service
    ↓ application-owned request/result mapping (Gate 3)
InspectionWorkerClient
    ↓ verified executable + minimal environment
CreateProcessW + STARTUPINFOEX
    ↓ PROC_THREAD_ATTRIBUTE_JOB_LIST
    ↓ PROC_THREAD_ATTRIBUTE_HANDLE_LIST
GraniteEdgeAI.ModelInspection.Worker.exe
    ↓ bounded protocol v1
UnavailableWorkerInspectionEngine (Gate 2)
```

## Implemented Gate 2 invariants

### Launch and containment

- The executable is resolved beneath a fixed approved root; `PATH` search is not used.
- Reparse points and non-AMD64 Portable Executable inputs are rejected.
- The child receives a minimal allowlisted environment with .NET diagnostics disabled.
- `CreateProcessW` is called with `STARTUPINFOEX`.
- The worker enters a Job Object at creation through `PROC_THREAD_ATTRIBUTE_JOB_LIST`.
- `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE` is enabled and no breakaway flag is allowed.
- `PROC_THREAD_ATTRIBUTE_HANDLE_LIST` permits exactly the three redirected child stream handles.
- Unrelated inheritable handles are proven not to reach the child.
- Win32 resources use explicit `SafeHandle` and async ownership boundaries.

### Protocol and transport

- The worker sends one `hello` before request-scoped output.
- The client sends one start command and accepts one terminal result.
- UTF-8 is strict; BOM, CR/CRLF, empty lines, partial EOF, duplicate JSON properties, oversized lines and invalid sequence transitions fail closed.
- A protocol message is limited to 1 MiB of UTF-8.
- Retained standard error is limited to 256 KiB and drained concurrently.
- Worker identity, protocol version, process ID, architecture and runtime profile are verified.
- Terminal status and process exit code must agree.

### Cancellation, timeout and cleanup

- Caller cancellation and the overall safety timeout are independent signals.
- After a request is active, cancellation sends at most one protocol cancel command.
- Cooperative cancellation must produce a trusted `Cancelled` terminal and exit code 3.
- An ignored cancellation, timeout, hang, crash, malformed conversation or live descendant triggers Job termination and a controlled infrastructure failure.
- The client verifies that the Job contains zero active processes before successful completion.
- The first failure remains primary; cleanup failures are retained only as secondary diagnostics.

### Dependency boundary

The production Transport, Worker and WorkerClient projects contain no LLamaSharp, Windows App SDK, OpenVINO, TurboQuant or abnormal-fixture dependency. WinUI does not reference WorkerClient in Gate 2. Executable architecture fitness tests enforce these constraints in CI.

## Why this option was selected

A separate process contains native crashes, makes launch/cleanup behaviour testable, avoids a local network listener and preserves a typed evidence boundary. A deterministic abnormal-process fixture can reproduce malformed output, hangs, crashes, cancellation refusal, descendant leakage and stream flooding without placing those behaviours in production code.

## Alternatives rejected

- **LLamaSharp inside WinUI:** a native abort could terminate the application and would mix UI and runtime responsibilities.
- **Local HTTP service:** adds a listening port, endpoint protection, conflicts and a larger attack surface without a remote-access requirement.
- **`llama-cli` as the inspection contract:** human-oriented text output is not a stable typed evidence protocol.
- **One host for inspection and chat:** the tasks have different lifetimes, outputs, cancellation semantics and performance needs.
- **Custom parser as the complete solution:** a bounded quick scan cannot prove that the selected runtime recognises and can initialise the model.

## Consequences

The design adds an executable, protocol versioning, startup cost and explicit build/publish responsibilities. In return, native failure containment, diagnostic bounds, dependency direction and process cleanup become independently testable and auditable.

The first boundary is `win-x64` only. Supporting x86, ARM64, Vulkan or another backend requires separate dependency, packaging and trusted-runtime evidence.

## Gate 2 non-claims

Gate 2 does **not** prove or implement:

- LLamaSharp loading or factual GGUF evidence extraction in the worker;
- application request/result mappers, classifier, service or ViewModel integration;
- live WinUI progress, cancellation or result cards;
- MSIX/package inclusion of the worker and native runtime;
- a real-model pass through the production application route;
- OpenVINO, TurboQuant, Hardware Fit, GPU acceleration or chat.

Those are later gates and must not be inferred from the worker-boundary tests.

## Review triggers

Review or supersede this ADR when the protocol, LLamaSharp/runtime version, architecture/backend support, packaging model, one-request-per-worker rule, cancellation contract or containment mechanism changes.
