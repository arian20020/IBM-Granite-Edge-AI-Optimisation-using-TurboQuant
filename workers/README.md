# Worker executables

`workers/` contains short-lived executables that isolate failure-prone or native work from the WinUI process.

The current Gate 2 worker is [`GraniteEdgeAI.ModelInspection.Worker`](./GraniteEdgeAI.ModelInspection.Worker/README.md). It communicates only through bounded redirected standard streams, accepts one inspection request, and exits after one terminal result. It has no local listener and is not referenced by WinUI yet.

Production worker code must not contain abnormal-fixture scenarios or depend on LLamaSharp, OpenVINO, TurboQuant or Windows App SDK until a later gate adds and verifies those capabilities.

See [ADR-003](../docs/architecture/decisions/ADR-003-protected-model-inspection-worker.md) and the [Gate 2 evidence record](../docs/testing/evidence/2026-08-05-model-inspection-worker-gate2-verification.md).
