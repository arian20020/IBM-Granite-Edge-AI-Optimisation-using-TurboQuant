# GGUF runtime supervisor

This Windows x64 process owns one stdio-adapter child at a time. It receives framed
commands over inherited standard streams, launches without a shell, keeps the
process tree in a kill-on-close Job, bounds output, and separates Stop from
session Close.

The child protocol uses `G1READY`, `G1RESPONSE`, Base64 `G1DELTA`, and `G1DONE`
frames. Initial user and assistant turns are replayed before `G1START`, so a
reloaded process receives the exact durable transcript. Unknown or oversized
stdout fails closed; stderr remains diagnostic-only.

The packaged baseline is the owned LLamaSharp 0.27.0 CPU adapter mapped to
upstream llama.cpp commit `3f7c29d318e317b63f54c558bc69803963d7d88c`.
Turbo3/Turbo4 are explicit contract values but this baseline rejects them with
`turboquant-runtime-required`; only a separately pinned and tested fork package
may claim those modes.
