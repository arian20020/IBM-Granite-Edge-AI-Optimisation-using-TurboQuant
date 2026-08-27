# G1 GGUF Native Closure — R2

## Final route state

Branch `validation/ucl-gguf-native-r2` contains G1-only changes. The final replay repair is `6f457c127c60546b15691e8ed74c2fd5f52fe4e5` (the final documentation commit is recorded in coordination after push). No application, package-registration, signing, or end-to-end claim is made here.

## Candidate audit

`57ab54c5d0701f2fcaa8f4d23108e71aaadb12f4` was preserved at `archive/ucl-g1-57ab54c5`, is a descendant of frozen `f599c358181bd4da44087ab0c64d36d02d23316a`, and contains only permitted GGUF scripts/tests. It resolved the Visual Studio MASM discovery seam without relying on an ambient `ml64` path. The archive ref was pushed.

`57df57c8e958eb73d0af3d2661e7df911a2d938f` and `5dfbfb9399db057a7055cefa86c4445d4c4c2e3c` were externally supplied with unknown provenance. Their G1-only diff was reviewed. `57df` changed the transform so that it drains the bounded source after a Length result; `5df` strengthened the synthetic continuation regression. Both were retained only after causal testing.

The final externally supplied candidate, adopted in `6f457c12`, contains only these G1 paths:

- `runtime/GraniteEdgeAI.GgufRuntime.NativeAdapter/LlamaSharpInferenceEngine.cs`
- `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufRealModelSmokeTests.cs`
- `tests/UnitTests/GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests/LlamaSharpInferenceEngineTests.cs`
- `tests/UnitTests/GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests/LlamaSharpRealModelSmokeTests.cs`

It introduces no network acquisition, relaxed validation, model/output logging, private path, or gate bypass.

## Empty-delta root cause and TDD evidence

The real model produced `ResponseStartedEvent` followed by `ResponseCompletedEvent(Stop)` with no text after a forced Length completion. LLamaSharp stores only output-transform text in `ChatHistory`, but its stateful executor had already consumed the hidden probe token. The next incremental prompt therefore used a KV cache ahead of the visible assistant history.

The minimal repair marks the session for replay only after `Length`, then before the next turn clones the visible `ChatHistory`, disposes the old context, and recreates the prompt/template/output-transform state. Ordinary `Stop` turns preserve the existing context. This avoids a temporary double-context memory peak; normal `DisposeAsync` disposes the retained context and weights.

The focused real-model regression was run against frozen `f599` before the repair: 1 total, 1 failed, with `Continuation completed as Stop without text`. The same regression on the repair: 1 total, 1 passed. The decision-level test was also RED on frozen code (the replay decision did not exist) and GREEN on the repair: 1 total, 1 passed.

## Fresh managed verification

Using SDK `10.0.301`, `Invoke-GgufChatVerification.ps1 -SkipApplicationBuild` recorded:

- Runtime contracts: 12/12 passed.
- Transport: 15/15 passed.
- Capabilities: 13/13 passed.
- Native adapter: 40 passed, 4 controlled-model skips, 44 total.
- Worker: 21/21 passed.
- Worker client: 9/9 passed.

The WorkerProcess project remains blocked by a C0-owned visual contract expecting `PromptTextBox_KeyDown`; without a configured real runtime its controlled test is skipped. Its configured fresh-replay attempt also failed before model load because Code Integrity blocked the new evidence adapter DLL, not because of a protocol or source failure.

## Stage/evidence identities

- Approved model: `granite-4.1-3b-Q4_K_M.gguf`, length `2099501664`, SHA-256 `662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29`.
- C0 detached runtime manifest: `86e537cd5f13d979d8b3856ed964cc041b8e948e70e08e30cdf0754aa0ca74a9`.
- G1 replay evidence closure: 52 verified members; detached manifest SHA-256 `95229785ad8b821fc268234afb40848dddbf63855b60632a046566dce318797b`.
- Quantizer: `be44b38ca5ce66470a657f7d41d99b11c9233a5846f83a71c99b87672021765d`.
- Hardware probe: `cb5b1afd28916c4e0467c884836adcaba0bd76d466fc572854ec402e05c0a25d`.
- llmfit: `592852e2f19bde606f848a972edefa7060ce78525985b2ac038f2eb1567f379d`.

The fresh closure was built outside Git from the route source, verified with `New-GgufRuntimeManifest.ps1`, `Test-GgufRuntimeManifest.ps1`, and `Test-GgufRuntimePackageClosure.ps1`, then received an exact packaged manifest copy only in the evidence-owned layout.

## Native status and handoff

The direct model regression proves the replay repair. The packaged worker rerun cannot be claimed because Code Integrity blocked the newly built unsigned adapter DLL (policy event evidence supplied to C0). C0 owns the approved development-signing functional rerun. No G1-owned worker/native processes remain.
