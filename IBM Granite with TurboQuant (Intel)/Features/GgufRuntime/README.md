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

The protected command-line runtime is implemented and exercised against a
deterministic fake CLI. Real GGUF inference still requires an authorized local
`llama.cpp` CPU build and an inference-capable GGUF model; neither artifact is
downloaded or committed by this repository.
