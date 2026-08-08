# GraniteEdgeAI.ModelInspection.Worker

## Purpose

This `win-x64` executable is the protected process endpoint for Model Inspection protocol version 1. It owns worker lifecycle and protocol sequencing; it does not own application classification or WinUI state.

## Production behaviour

1. Write one verified `hello` message.
2. Accept one bounded start command.
3. Write `started`, optional progress, and exactly one terminal message.
4. Exit with a code consistent with the terminal status.
5. Observe parent loss and cooperative cancellation.

`Program` composes `LlamaSharpInspectionEngine` with the pinned production
CPU/VocabOnly runtime. The engine verifies caller-observed file continuity
before native configuration, reports the five protocol stages, maps only
project-owned factual evidence, and converts runtime diagnostics to a fixed
allowlist of privacy-safe operational failures. The unavailable engine remains
only as a direct host-test seam.

## Security and dependency rules

- No listener, local server or named pipe.
- No model path on the process command line.
- No abnormal-fixture scenarios in production code.
- The only native runtime dependency is the production
  `GraniteEdgeAI.ModelInspection.LlamaSharp` CPU/VocabOnly library.
- No GPU backend, inference, context, benchmark, OpenVINO, TurboQuant or
  Windows App SDK dependency.
- Diagnostics are fixed and non-sensitive.

## Tests

`tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests` verifies
hello-first sequencing, lifecycle and terminal arbitration plus exact runtime
request forwarding, progress translation, evidence mapping, integrity
precedence, disposal/runtime identity enforcement, concurrency and diagnostic
privacy.

Process-level launch, crash, hang, cancellation and cleanup tests live
separately under
`tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests`.
That boundary also publishes this exact worker and proves all five stages plus
Completed evidence through the 800-byte, zero-tensor N-001 SentencePiece GGUF.
The fixture proves native VocabOnly loading and tokenization only; it makes no
inference, tensor, performance, or quality claim.

## Approval-required contract gap

The approved integration design lists `WorkerId` in terminal runtime identity,
while protocol v1 currently carries it only in `WorkerHelloMessage` and
`WorkerRuntimeIdentity` has no such field. This engine does not invent or add
that serialized field. Reconciling the design, application handshake consumer
and terminal schema requires a separately approved additive protocol change.

See [ADR-003](../../docs/architecture/decisions/ADR-003-protected-model-inspection-worker.md) and [Gate 2 evidence](../../docs/testing/evidence/2026-08-05-model-inspection-worker-gate2-verification.md).
