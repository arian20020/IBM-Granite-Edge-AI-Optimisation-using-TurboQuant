# Local GGUF chat

The current executable preview includes the approved chat layout, streaming UI,
centered Stop control, multiple conversations, dated history groups, full saved
prompt/response transcripts, and history restoration after an app restart.

From the repository root, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Run-ChatPreview.ps1
```

When Granite Edge AI opens, select **Preview Chat** in the upper-right corner.
The preview runtime is deterministic and does not require a downloaded model.
Its history is stored under `%LOCALAPPDATA%\GraniteEdgeAI\ChatHistory`.

The message composer can select `.txt` and `.md` knowledge files. These
selections are presentation-only: every selected file is labelled **Not
indexed**, and its contents do not influence prompts or model context yet.
Only the file name is shown in the chat interface.

The protected command-line runtime packages an owned port-free stdio adapter,
LLamaSharp 0.27.0, and its pinned llama.cpp CPU backend. The build does not
download a model. A controlled Granite 4.1 3B GGUF test covers load, two streamed
turns, Stop/reload, Close, package verification, and unchanged model hashing.

Production chat is composed from an explicit `GgufChatLaunchRequest` containing
the verified package root, trusted manifest snapshot, inspected model path, and
complete approved runtime configuration. `MainWindow.OpenProductionChatAsync`
verifies that request and opens the same persistent chat UI without silently
falling back to preview. The seam enforces the CPU-only MVP, matches runtime
build/source identity, and re-hashes the model before launch. Untrusted,
changed, or mismatched inputs return to onboarding.
Each new or selected conversation starts an isolated CLI session reconstructed
from that conversation's durable turns, and a Stop result that requires reload
rebuilds the session before another prompt.

Run all local verification with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\gguf-runtime\Invoke-GgufChatVerification.ps1
```

Production packaging is automatic for x64 builds and records the exact upstream
source commit, LLamaSharp build identity, build flags, hashes, architecture,
roles, and license references. The standard package supports F16, Q8_0, and
Q4_0 KV cache. Turbo3/Turbo4 remain a gated AtomicBot-fork package variant and
are rejected by the standard backend rather than silently falling back.

`OpenProductionChatAsync` is the trusted application handoff for an inspected
model. The current onboarding button labelled **Preview Chat** intentionally
opens the deterministic preview; it must not be described as real inference.
