# Infrastructure adapters

`infrastructure/` contains operating-system and external-runtime adapters. These components implement application-independent mechanics and must not contain WinUI presentation logic or user-facing classification policy.

The current Gate 2 component is [`GraniteEdgeAI.ModelInspection.WorkerClient`](./GraniteEdgeAI.ModelInspection.WorkerClient/README.md), which launches and supervises the protected Model Inspection worker on Windows x64.

Infrastructure changes are guarded by architecture fitness tests, focused unit tests, real out-of-process scenarios and the complete Windows regression. See [ADR-003](../docs/architecture/decisions/ADR-003-protected-model-inspection-worker.md).
