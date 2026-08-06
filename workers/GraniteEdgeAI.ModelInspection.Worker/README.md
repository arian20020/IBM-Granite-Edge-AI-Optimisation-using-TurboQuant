# GraniteEdgeAI.ModelInspection.Worker

## Purpose

This `win-x64` executable is the protected process endpoint for Model Inspection protocol version 1. It owns worker lifecycle and protocol sequencing; it does not own application classification or WinUI state.

## Gate 2 behaviour

1. Write one verified `hello` message.
2. Accept one bounded start command.
3. Write `started`, optional progress, and exactly one terminal message.
4. Exit with a code consistent with the terminal status.
5. Observe parent loss and cooperative cancellation.

The installed `UnavailableWorkerInspectionEngine` returns a controlled operational failure. Gate 2 therefore proves the host and process boundary without claiming LLamaSharp model inspection.

## Security and dependency rules

- No listener, local server or named pipe.
- No model path on the process command line.
- No abnormal-fixture scenarios in production code.
- No LLamaSharp, OpenVINO, TurboQuant or Windows App SDK dependency in Gate 2.
- Diagnostics are fixed and non-sensitive.

## Tests

`tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests` verifies hello-first sequencing, one-request lifecycle, terminal arbitration, parent monitoring and the controlled unavailable-engine seam.

Process-level launch, crash, hang, cancellation and cleanup tests live separately under `tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests`.

See [ADR-003](../../docs/architecture/decisions/ADR-003-protected-model-inspection-worker.md) and [Gate 2 evidence](../../docs/testing/evidence/2026-08-05-model-inspection-worker-gate2-verification.md).
