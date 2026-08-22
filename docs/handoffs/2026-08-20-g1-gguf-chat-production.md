# G1 GGUF chat production handoff

## Branch and scope

- Branch: `feature/gguf-cli-chat-production`
- Base: `feature/gguf-cli-chat-runtime` at design closure `06d19eaa`
- Runtime: Windows x64 CPU, one local session, no server or listener
- Preview: self-contained deterministic WinUI stream with persistent history

## Runnable commands

Launch the visual preview:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Run-ChatPreview.ps1
```

Run the complete local verification set:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\gguf-runtime\Invoke-GgufChatVerification.ps1
```

## Packaged production runtime

The x64 application build now publishes:

- the protected supervisor;
- the Granite Edge stdio adapter;
- LLamaSharp 0.27.0 and its CPU native backend;
- exact manifest hashes, roles, source commit, build flags, and licenses.

The baseline source identity is upstream llama.cpp commit
`3f7c29d318e317b63f54c558bc69803963d7d88c`. It accepts F16, Q8_0, and Q4_0 KV
cache and uses no server or listener. A local inference-capable GGUF model is
still user-supplied and is hashed before launch; the repository never downloads
one.

The controlled Granite 4.1 3B run passed direct adapter load plus packaged
two-turn streaming, Stop/reload, Close, and model-integrity checks. The regular
verification suite skips this gate when its external model/config variables are
not present.

## Explicitly deferred

Vulkan, SYCL, artifact quantization, attachment ingestion, and cross-route
ranking remain outside the CPU chat MVP. Turbo3/Turbo4 are represented as an
explicit gated runtime seam based on the researched AtomicBot fork at commit
`519f0c594a8e31467d2e2f2cf17054c9e7e11536`; the upstream package rejects them
with `turboquant-runtime-required` and never reports false activation.
