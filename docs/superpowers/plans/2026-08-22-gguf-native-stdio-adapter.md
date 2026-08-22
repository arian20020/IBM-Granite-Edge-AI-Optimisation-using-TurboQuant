# GGUF Native Stdio Adapter Closure Plan

**Status:** Approved architecture amendment on 2026-08-22

**Goal:** Replace the unsupported stock `llama-cli --simple-io` assumption with a G1-owned, pinned, port-free adapter that restores exact user/assistant history and streams bounded structured events.

## Runtime boundary

- Keep the existing WinUI-to-supervisor framed protocol and Windows Job containment.
- Replace the raw interactive CLI child with `granite-edge-stdio-adapter.exe`.
- The production baseline is a .NET stdio adapter using LLamaSharp 0.27.0 and its pinned CPU backend. LLamaSharp 0.27.0 maps to upstream llama.cpp commit `3f7c29d318e317b63f54c558bc69803963d7d88c`. Normal restore/build never downloads a model.
- Child stdin/stdout uses a bounded line protocol. Content is UTF-8 encoded as Base64, so prompts never become command-line arguments and embedded newlines cannot alter framing.
- Initial user and assistant turns are sent before `G1START`; the adapter applies the model's own chat template to the ordered transcript.
- Stop always returns through the supervisor's reload path. The partial response is persisted, but it is not injected as a completed assistant turn.

## Child protocol

Supervisor to adapter:

```text
G1TURN U <base64-utf8>
G1TURN A <base64-utf8>
G1START
G1PROMPT <base64-utf8>
G1STOP
```

Adapter to supervisor:

```text
G1READY
G1RESPONSE
G1DELTA <base64-utf8>
G1DONE
G1FAIL <fixed-code>
```

Unknown, malformed, oversized, or invalid UTF-8 frames fail closed. Stderr is diagnostic-only and never enters chat text.

## TurboQuant boundary

The repository evidence selects the AtomicBot fork at commit `519f0c594a8e31467d2e2f2cf17054c9e7e11536` as an Experimental candidate. A separate native package variant may build against that pin and expose only explicitly configured cache types. The recorded Granite 3B CPU profiles are:

- `turbo4/turbo4`, 4096 context, zero GPU layers (AB-05)
- `turbo3/turbo3`, 4096 context, zero GPU layers (AB-06)

TurboQuant is never an implicit fallback. The upstream LLamaSharp package rejects Turbo2/3/4 with `turboquant-runtime-required`; it is reported active only when a future native package source commit, manifest build identity, requested cache types, and controlled activation evidence all match. Upstream F16/Q8/Q4 remains the approved baseline. This separation reflects the repository tests: AtomicBot Turbo3/Turbo4 are experimental and its CPU implementation is incomplete or fallback-heavy, so shipping those labels on the upstream backend would be false activation.

TurboVec remains outside this runtime: it operates on retrieval vectors for the knowledge-file feature, not model KV cache.

## Execution sequence

1. Add failing child-protocol, exact-history, manifest-role, and stop-reload tests.
2. Implement the bounded protocol codec and update the deterministic fixture.
3. Add the stdio adapter and reproducible LLamaSharp CPU package.
4. Replace CLI package roles/paths with adapter roles/paths.
5. Build the pinned package and run two-turn, stop, reload, cleanup, and privacy probes with the controlled Granite model.
6. Run the complete GGUF, WinUI, and protected Model Inspection regression set.
